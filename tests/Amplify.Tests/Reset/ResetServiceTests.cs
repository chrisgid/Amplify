using Amplify.Core.Auth;
using Amplify.Core.Reset;
using Amplify.Core.Settings;
using NSubstitute;

namespace Amplify.Tests.Reset;

public sealed class ResetServiceTests
{
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private ResetService NewService() => new(_settings, _auth);

    [Fact]
    public async Task ResetResetsSettingsThenDisconnects()
    {
        await NewService().ResetAsync();

        _settings.Received(1).Reset();
        await _auth.Received(1).DisconnectAsync();
    }

    [Fact]
    public async Task ResetsSettingsBeforeDisconnecting()
    {
        // The order matters: settings are reset first so even a failing disconnect leaves defaults
        // applied, and the disconnect's state change is the last thing observers see.
        var calls = new List<string>();
        _settings.When(s => s.Reset()).Do(_ => calls.Add("reset"));
        _auth.DisconnectAsync().Returns(_ =>
        {
            calls.Add("disconnect");
            return Task.CompletedTask;
        });

        await NewService().ResetAsync();

        Assert.Equal(["reset", "disconnect"], calls);
    }

    [Fact]
    public async Task ResetStillDisconnectsWhenAlreadyDisconnected()
    {
        // Reset while disconnected is valid; the disconnect is a no-op on the auth side but must run.
        await NewService().ResetAsync();

        _settings.Received(1).Reset();
        await _auth.Received(1).DisconnectAsync();
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ResetService(null!, _auth));
        Assert.Throws<ArgumentNullException>(() => new ResetService(_settings, null!));
    }
}
