using Microsoft.UI.Xaml;

namespace Amplify.App;

/// <summary>
/// The single main window that hosts Amplify's screens. Currently a bare shell; the Mica backdrop,
/// custom title bar, and routing are added later.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
