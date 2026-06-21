using Amplify.App.Auth;
using Amplify.Core.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Amplify.App.Views;

/// <summary>
/// First-run / connect screen. Currently a placeholder that hosts the temporary connect controls;
/// on a successful connection the shell routes to the main screen automatically. The guided setup
/// and real onboarding copy replace this screen later.
/// </summary>
public sealed partial class OnboardingPage : Page
{
    private readonly IAuthService _authService;
    private readonly DevClientIdSource _clientIdSource;

    public OnboardingPage()
    {
        InitializeComponent();
        _authService = App.Services.GetRequiredService<IAuthService>();
        _clientIdSource = App.Services.GetRequiredService<DevClientIdSource>();
    }

    // Temporary handler for the connect test; removed with the rest of the placeholder controls when
    // the real onboarding flow lands. Resumes on the UI thread after the await.
    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        _clientIdSource.ClientId = ClientIdBox.Text.Trim();
        ConnectButton.IsEnabled = false;
        StatusText.Text = "Connecting…";
        try
        {
            AuthResult result = await _authService.ConnectAsync();
            StatusText.Text = result switch
            {
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
