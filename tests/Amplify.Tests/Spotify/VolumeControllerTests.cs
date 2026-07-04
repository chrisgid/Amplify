using Amplify.Core.Auth;
using Amplify.Core.Hotkeys;
using Amplify.Core.Settings;
using Amplify.Core.Spotify;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Amplify.Tests.Spotify;

public sealed class VolumeControllerTests
{
    [Fact]
    public async Task NudgeRaisesByConfiguredStep()
    {
        Harness h = Harness.Create(step: 10, startVolume: 50);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        await h.Controller.NudgeAsync(1);

        Assert.Equal(60, h.Controller.Volume);
        await h.Client.Received(1).SetVolumeAsync(60);
    }

    [Fact]
    public async Task NudgeDownLowersByStep()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        await h.Controller.NudgeAsync(-1);

        Assert.Equal(45, h.Controller.Volume);
    }

    [Fact]
    public async Task NudgeClampsAtHundred()
    {
        Harness h = Harness.Create(step: 5, startVolume: 98);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        await h.Controller.NudgeAsync(1);

        Assert.Equal(100, h.Controller.Volume);
        await h.Client.Received(1).SetVolumeAsync(100);
    }

    [Fact]
    public async Task NudgeClampsAtZero()
    {
        Harness h = Harness.Create(step: 5, startVolume: 3);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        await h.Controller.NudgeAsync(-1);

        Assert.Equal(0, h.Controller.Volume);
    }

    [Fact]
    public async Task NudgeAtBoundSendsNothing()
    {
        Harness h = Harness.Create(step: 5, startVolume: 100);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        await h.Controller.NudgeAsync(1);

        await h.Client.DidNotReceive().SetVolumeAsync(Arg.Any<int>());
        Assert.Equal(100, h.Controller.Volume);
    }

    [Fact]
    public async Task NudgeIsNoOpWhenDisconnected()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        h.Auth.State.Returns(ConnectionState.Disconnected);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        await h.Controller.NudgeAsync(1);

        Assert.False(h.Controller.CanControl);
        await h.Client.DidNotReceive().SetVolumeAsync(Arg.Any<int>());
        // A read can't help while disconnected — short-circuit before any request.
        await h.Provider.DidNotReceive().RefreshAsync();
    }

    [Fact]
    public async Task NudgeProbesOnceThenNoOpsWhenNoActiveDevice()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        h.Provider.Current.Returns(new PlayerState(false, 0, null));
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        await h.Controller.NudgeAsync(1);

        Assert.False(h.Controller.CanControl);
        // Connected but no known device → one on-demand read that still finds nothing → no write.
        await h.Provider.Received(1).RefreshAsync();
        await h.Client.DidNotReceive().SetVolumeAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task NudgeWakesAndAppliesWhenDeviceBecomesActive()
    {
        // The device starts playing while polling is suspended (window minimised): the first hotkey
        // nudge does an on-demand read that finds it, then nudges from the freshly-read volume.
        Harness h = Harness.Create(step: 5, startVolume: 0);
        h.Provider.Current.Returns(new PlayerState(false, 0, null));
        await h.Controller.OnLaunchedAsync(CancellationToken.None);
        Assert.False(h.Controller.CanControl);

        h.Provider.RefreshAsync().Returns(_ =>
        {
            h.Provider.PlayerStateChanged +=
                Raise.Event<EventHandler<PlayerState?>>(h.Provider, new PlayerState(true, 40, "Speaker"));
            return Task.CompletedTask;
        });

        await h.Controller.NudgeAsync(1);

        Assert.True(h.Controller.CanControl);
        Assert.Equal(45, h.Controller.Volume);
        await h.Client.Received(1).SetVolumeAsync(45);
    }

    [Fact]
    public async Task RepeatedNudgesWithNoDeviceAreThrottled()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        Harness h = Harness.Create(step: 5, startVolume: 50, time: time);
        h.Provider.Current.Returns(new PlayerState(false, 0, null));
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        // Nothing playing: each nudge would probe, but a mashed hotkey must not fire a burst of reads.
        await h.Controller.NudgeAsync(1);
        await h.Controller.NudgeAsync(1);
        await h.Provider.Received(1).RefreshAsync();

        // Past the throttle window a fresh probe is allowed again.
        time.Advance(TimeSpan.FromSeconds(5));
        await h.Controller.NudgeAsync(1);
        await h.Provider.Received(2).RefreshAsync();

        await h.Client.DidNotReceive().SetVolumeAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task DeviceBecomingActiveEnablesControl()
    {
        // The behaviour the shared provider buys: a device that starts playing while Amplify is open
        // is picked up by the next poll and the control enables without a manual refresh.
        Harness h = Harness.Create(step: 5, startVolume: 0);
        h.Provider.Current.Returns(new PlayerState(false, 0, null));
        await h.Controller.OnLaunchedAsync(CancellationToken.None);
        Assert.False(h.Controller.CanControl);

        var stateChanged = false;
        h.Controller.StateChanged += (_, _) => stateChanged = true;
        h.Provider.PlayerStateChanged +=
            Raise.Event<EventHandler<PlayerState?>>(h.Provider, new PlayerState(true, 30, "Speaker"));

        Assert.True(h.Controller.CanControl);
        Assert.Equal(30, h.Controller.Volume);
        Assert.True(stateChanged);
    }

    [Fact]
    public async Task OptimisticValueRevertsWhenWriteFails()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);
        h.Client.SetVolumeAsync(Arg.Any<int>()).Returns(_ => Task.FromException(new HttpRequestException()));

        await h.Controller.NudgeAsync(1);

        Assert.Equal(50, h.Controller.Volume);
    }

    [Fact]
    public async Task RejectedDeviceRevertsAndDisablesControl()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);
        h.Client.SetVolumeAsync(Arg.Any<int>())
            .Returns(_ => Task.FromException(new DeviceNotControllableException()));

        await h.Controller.NudgeAsync(1);

        Assert.Equal(50, h.Controller.Volume);
        Assert.False(h.Controller.CanControl);
    }

    [Fact]
    public async Task RapidNudgesCoalesceToTheLatestTarget()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        // Hold the first write open so the following nudges queue up behind it; only the latest of
        // those should be written once the first completes.
        var firstWrite = new TaskCompletionSource();
        var sent = new List<int>();
        h.Client.SetVolumeAsync(Arg.Any<int>()).Returns(ci =>
        {
            sent.Add(ci.Arg<int>());
            return sent.Count == 1 ? firstWrite.Task : Task.CompletedTask;
        });

        Task t1 = h.Controller.NudgeAsync(1);   // 55 — starts the (gated) write
        Task t2 = h.Controller.NudgeAsync(1);   // 60 — queued
        Task t3 = h.Controller.NudgeAsync(1);   // 65 — queued, supersedes 60
        firstWrite.SetResult();
        await Task.WhenAll(t1, t2, t3);

        Assert.Equal([55, 65], sent);
        Assert.Equal(65, h.Controller.Volume);
    }

    [Fact]
    public async Task HotkeyPressNudgesInTheRightDirection()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        h.Hotkeys.HotkeyPressed += Raise.Event<EventHandler<HotkeyAction>>(h.Hotkeys, HotkeyAction.VolumeUp);
        await h.Client.Received(1).SetVolumeAsync(55);

        h.Hotkeys.HotkeyPressed += Raise.Event<EventHandler<HotkeyAction>>(h.Hotkeys, HotkeyAction.VolumeDown);
        await h.Client.Received(1).SetVolumeAsync(50);
    }

    [Fact]
    public async Task RefreshDelegatesToTheSharedProvider()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);

        await h.Controller.RefreshAsync();

        await h.Provider.Received(1).RefreshAsync();
    }

    [Fact]
    public async Task TransientFailureKeepsTheLatestTargetInsteadOfReverting()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        var firstWrite = new TaskCompletionSource();
        var sent = new List<int>();
        h.Client.SetVolumeAsync(Arg.Any<int>()).Returns(ci =>
        {
            sent.Add(ci.Arg<int>());

            // The first write (55) is held open and then fails transiently; the queued newer target
            // (60) succeeds. A transient blip must not discard the user's latest input.
            return sent.Count == 1 ? FailWhenSignalled(firstWrite.Task) : Task.CompletedTask;
        });

        Task t1 = h.Controller.NudgeAsync(1);   // 55 — starts the gated write
        Task t2 = h.Controller.NudgeAsync(1);   // 60 — queued behind it
        firstWrite.SetResult();
        await Task.WhenAll(t1, t2);

        Assert.Equal([55, 60], sent);
        Assert.Equal(60, h.Controller.Volume);
    }

    [Fact]
    public async Task SteadyStatePollDoesNotRaiseStateChanged()
    {
        Harness h = Harness.Create(step: 5, startVolume: 50);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        var stateChanges = 0;
        h.Controller.StateChanged += (_, _) => stateChanges++;

        // A further poll with the device still present (no availability change) must not notify.
        h.Provider.PlayerStateChanged +=
            Raise.Event<EventHandler<PlayerState?>>(h.Provider, new PlayerState(true, 50, "Test device"));

        Assert.Equal(0, stateChanges);
    }

    [Fact]
    public async Task RecentWriteSuppressesAStalePollSnapBack()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        Harness h = Harness.Create(step: 5, startVolume: 50, time: time);
        await h.Controller.OnLaunchedAsync(CancellationToken.None);

        await h.Controller.NudgeAsync(1);   // writes 55 successfully; records the write time
        Assert.Equal(55, h.Controller.Volume);

        // A poll that read the device before it reflected the write reports the old value.
        h.Provider.PlayerStateChanged +=
            Raise.Event<EventHandler<PlayerState?>>(h.Provider, new PlayerState(true, 50, "dev"));
        Assert.Equal(55, h.Controller.Volume);   // suppressed — no snap-back

        // Past the settle window the same reading is honoured (a genuine external change).
        time.Advance(TimeSpan.FromSeconds(5));
        h.Provider.PlayerStateChanged +=
            Raise.Event<EventHandler<PlayerState?>>(h.Provider, new PlayerState(true, 50, "dev"));
        Assert.Equal(50, h.Controller.Volume);
    }

    private static async Task FailWhenSignalled(Task gate)
    {
        await gate;
        throw new HttpRequestException();
    }

    private sealed class Harness
    {
        public required ISpotifyClient Client { get; init; }

        public required IHotkeyService Hotkeys { get; init; }

        public required IAuthService Auth { get; init; }

        public required IPlayerStateProvider Provider { get; init; }

        public required VolumeController Controller { get; init; }

        public static Harness Create(int step, int startVolume, TimeProvider? time = null)
        {
            ISpotifyClient client = Substitute.For<ISpotifyClient>();
            client.SetVolumeAsync(Arg.Any<int>()).Returns(Task.CompletedTask);

            IHotkeyService hotkeys = Substitute.For<IHotkeyService>();

            ISettingsService settings = Substitute.For<ISettingsService>();
            settings.Current.Returns(new AppSettings { VolumeStep = step });

            IAuthService auth = Substitute.For<IAuthService>();
            auth.State.Returns(ConnectionState.Connected);

            IPlayerStateProvider provider = Substitute.For<IPlayerStateProvider>();
            provider.Current.Returns(new PlayerState(true, startVolume, "Test device"));

            var controller = new VolumeController(
                client, hotkeys, settings, auth, provider, Substitute.For<ILogger<VolumeController>>(), time);

            return new Harness
            {
                Client = client,
                Hotkeys = hotkeys,
                Auth = auth,
                Provider = provider,
                Controller = controller,
            };
        }
    }
}
