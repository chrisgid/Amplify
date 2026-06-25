using Amplify.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Amplify.App.Views;

/// <summary>
/// First-run / connect screen: the setup guide for creating a Spotify developer app, the gated
/// Client ID field, and the connect attempt itself. All state lives in
/// <see cref="OnboardingViewModel"/>; on a successful connection the shell routes to the main
/// screen automatically.
/// </summary>
public sealed partial class OnboardingPage : Page
{
    public OnboardingPage()
    {
        // Resolve the view-model before InitializeComponent so x:Bind sees it as the bindings are
        // wired up.
        ViewModel = App.Services.GetRequiredService<OnboardingViewModel>();
        InitializeComponent();
    }

    /// <summary>The bound onboarding view-model; public so generated <c>x:Bind</c> code can read it.</summary>
    public OnboardingViewModel ViewModel { get; }
}
