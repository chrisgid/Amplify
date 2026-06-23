using Amplify.Core.Auth;
using Amplify.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Amplify.App.Views;

/// <summary>
/// First-run / connect screen. Currently a placeholder that hosts the temporary connect controls;
/// on a successful connection the shell routes to the main screen automatically. The guided setup
/// and real onboarding copy replace this screen later — at which point capturing the Client ID
/// becomes the onboarding flow's job rather than this throwaway text box.
/// </summary>
public sealed partial class OnboardingPage : Page
{
    private readonly IAuthService _authService;
    private readonly ISettingsService _settings;

    public OnboardingPage()
    {
        InitializeComponent();
        _authService = App.Services.GetRequiredService<IAuthService>();
        _settings = App.Services.GetRequiredService<ISettingsService>();
    }

    // Temporary handler for the connect test; removed with the rest of the placeholder controls when
    // the real onboarding flow lands. Resumes on the UI thread after the await.
    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        // Persist the per-user Client ID where auth reads it. Onboarding owns this capture later.
        _settings.Update(s => s.SpotifyClientId = ClientIdBox.Text.Trim());

        ConnectButton.IsEnabled = false;
        StatusText.Text = "Connecting…";
        try
        {
            AuthResult result = await _authService.ConnectAsync();
            StatusText.Text = result switch
            {
                { Success: true, NotPremium: true } => "Connected (Free — volume control needs Premium).",
                { Success: true } => "Connected.",
                { Denied: true } => "Access not granted. You can try again.",
                _ => result.Error ?? "Connection failed.",
            };
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }
}
