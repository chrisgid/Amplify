using Amplify.App.Auth;
using Amplify.Core.Auth;
using Microsoft.UI.Xaml;

namespace Amplify.App;

/// <summary>
/// The single main window that hosts Amplify's screens. Currently a bare shell with a temporary
/// connect test; the Mica backdrop, custom title bar, and routing are added later.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly IAuthService _authService;
    private readonly DevClientIdSource _clientIdSource;

    public MainWindow(IAuthService authService, DevClientIdSource clientIdSource)
    {
        _authService = authService;
        _clientIdSource = clientIdSource;
        InitializeComponent();
    }

    // Temporary handler for the walking-skeleton connect test; removed with the rest of the
    // throwaway UI when onboarding lands. Resumes on the UI thread after each await.
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
                { Success: true } => $"Connected. State: {_authService.State}.",
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
