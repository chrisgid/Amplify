using Amplify.Core.Notifications;
using Amplify.Core.Settings;
using Amplify.Core.Tray;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Amplify.Tests.Notifications;

public sealed class NotificationServiceTests
{
    private static readonly TrayHintCopy _copy = new("Amplify is still running", "Find it in the system tray.");

    [Fact]
    public async Task FirstHideShowsHintOnceAndSetsFlag()
    {
        Harness h = Harness.Create();
        await h.Service.OnLaunchedAsync(CancellationToken.None);

        h.RaiseHidden();

        h.Tray.Received(1).ShowTrayNotification(_copy.Title, _copy.Message);
        Assert.True(h.Settings.Current.TrayHintShown);
    }

    [Fact]
    public async Task SubsequentHidesDoNotShowAgain()
    {
        Harness h = Harness.Create();
        await h.Service.OnLaunchedAsync(CancellationToken.None);

        h.RaiseHidden();
        h.RaiseHidden();
        h.RaiseHidden();

        h.Tray.Received(1).ShowTrayNotification(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AlreadyShownFlagSuppressesTheHint()
    {
        Harness h = Harness.Create(trayHintShown: true);
        await h.Service.OnLaunchedAsync(CancellationToken.None);

        h.RaiseHidden();

        h.Tray.DidNotReceive().ShowTrayNotification(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task FailedShowLeavesFlagUnsetForRetry()
    {
        Harness h = Harness.Create();
        h.Tray.When(t => t.ShowTrayNotification(Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("balloon failed"));
        await h.Service.OnLaunchedAsync(CancellationToken.None);

        h.RaiseHidden();   // must not throw

        Assert.False(h.Settings.Current.TrayHintShown);
    }

    [Fact]
    public async Task RetriesOnNextHideAfterAnEarlierFailure()
    {
        Harness h = Harness.Create();
        var shouldThrow = true;
        h.Tray.When(t => t.ShowTrayNotification(Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ =>
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException("balloon failed");
                }
            });
        await h.Service.OnLaunchedAsync(CancellationToken.None);

        h.RaiseHidden();   // fails, flag stays unset
        shouldThrow = false;
        h.RaiseHidden();   // succeeds this time

        h.Tray.Received(2).ShowTrayNotification(_copy.Title, _copy.Message);
        Assert.True(h.Settings.Current.TrayHintShown);
    }

    private sealed class Harness
    {
        public required ITrayService Tray { get; init; }

        public required ISettingsService Settings { get; init; }

        public required NotificationService Service { get; init; }

        public void RaiseHidden() =>
            Tray.HiddenToTray += Raise.Event<EventHandler>(Tray, EventArgs.Empty);

        public static Harness Create(bool trayHintShown = false)
        {
            ITrayService tray = Substitute.For<ITrayService>();

            var backing = new AppSettings { TrayHintShown = trayHintShown };
            ISettingsService settings = Substitute.For<ISettingsService>();
            settings.Current.Returns(backing);
            // Update mutates the backing settings in place, so the flag it sets is visible on Current
            // to the next read — mirroring the real service's single in-memory instance.
            settings.When(s => s.Update(Arg.Any<Action<AppSettings>>()))
                .Do(ci => ci.Arg<Action<AppSettings>>()(backing));

            var service = new NotificationService(
                tray, settings, _copy, Substitute.For<ILogger<NotificationService>>());

            return new Harness { Tray = tray, Settings = settings, Service = service };
        }
    }
}
