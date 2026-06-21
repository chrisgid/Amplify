using Amplify.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace Amplify.App.Views;

/// <summary>
/// Settings screen. Currently a placeholder whose only working affordance is returning to the main
/// screen; the full settings content and its persistence layer replace this later. The footer shows
/// the app version from the packaged identity.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ShellViewModel _shell;

    public SettingsPage()
    {
        InitializeComponent();
        _shell = App.Services.GetRequiredService<ShellViewModel>();
        VersionText.Text = $"Amplify {AppVersion()} · Not affiliated with Spotify";
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => _shell.GoBackCommand.Execute(null);

    private static string AppVersion()
    {
        try
        {
            PackageVersion v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch (InvalidOperationException)
        {
            // No package identity (unpackaged run): fall back to a placeholder version.
            return "1.0.0";
        }
    }
}
