using Amplify.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Amplify.App.Views;

/// <summary>
/// Settings screen: groups the user preferences into native cards bound to <see cref="SettingsViewModel"/>,
/// shows a read-only view of the account and stored Client ID, and routes back to the main screen.
/// All user-facing text comes from the shared resource file via <c>x:Uid</c> / the view-model.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ShellViewModel _shell;

    public SettingsPage()
    {
        // Resolve the view-model before InitializeComponent so x:Bind (including one-time bindings)
        // sees it as the bindings are wired up.
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        _shell = App.Services.GetRequiredService<ShellViewModel>();
        InitializeComponent();
    }

    /// <summary>The bound settings view-model; public so generated <c>x:Bind</c> code can read it.</summary>
    public SettingsViewModel ViewModel { get; }

    private void OnBackClick(object sender, RoutedEventArgs e) => _shell.GoBackCommand.Execute(null);
}
