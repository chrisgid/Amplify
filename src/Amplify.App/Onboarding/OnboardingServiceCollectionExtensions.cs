using Amplify.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Onboarding;

/// <summary>DI registration for the onboarding screen's view-model.</summary>
internal static class OnboardingServiceCollectionExtensions
{
    /// <summary>Registers <see cref="OnboardingViewModel"/>, mirroring the settings screen's lifetime.</summary>
    public static IServiceCollection AddOnboarding(this IServiceCollection services)
    {
        services.AddSingleton<OnboardingViewModel>();
        return services;
    }
}
