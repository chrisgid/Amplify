using Amplify.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Amplify.App.Views;

/// <summary>
/// Settings screen: groups the user preferences into native cards bound to <see cref="SettingsViewModel"/>,
/// shows the connected account with its disconnect/reconnect action and a read-only view of the
/// stored Client ID, and gates the destructive reset behind a confirmation dialog. Back navigation
/// lives in the window title bar (see <c>MainWindow</c>), not here. All user-facing text comes from
/// the shared resource file via <c>x:Uid</c> / the view-model.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ResourceLoader _strings = new();

    public SettingsPage()
    {
        // Resolve the view-model before InitializeComponent so x:Bind (including one-time bindings)
        // sees it as the bindings are wired up.
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }

    /// <summary>The bound settings view-model; public so generated <c>x:Bind</c> code can read it.</summary>
    public SettingsViewModel ViewModel { get; }

    // Reset is destructive and can't be undone, so it's gated behind a native confirmation dialog; the
    // view-model's reset only runs when the user picks the primary action.
    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _strings.GetString("Settings_Reset_Dialog_Title"),
            Content = new TextBlock
            {
                Text = _strings.GetString("Settings_Reset_Dialog_Body"),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = _strings.GetString("Settings_Reset_Dialog_Confirm"),
            CloseButtonText = _strings.GetString("Settings_Reset_Dialog_Cancel"),
            // Primary is the accented call-to-action; Cancel stays the secondary (standard) button,
            // matching the Windows dialog convention.
            DefaultButton = ContentDialogButton.Primary,
        };

        // Trim the dialog a little narrower than the platform default (548) so the short body doesn't
        // stretch across an over-wide surface.
        dialog.Resources["ContentDialogMaxWidth"] = 460d;

        // The dialog is hosted in a popup off the XamlRoot, so it doesn't inherit the theme override
        // applied to the window's content root — copy it across so a Light/Dark choice is honoured
        // (Default still follows the OS).
        if (XamlRoot?.Content is FrameworkElement root)
        {
            dialog.RequestedTheme = root.RequestedTheme;
        }

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ResetCommand.ExecuteAsync(null);
        }
    }
}
