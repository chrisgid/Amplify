using Amplify.Core.Startup;
using Amplify.Core.Theming;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Theming;

/// <summary>DI registration for the theming service.</summary>
internal static class ThemeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ThemeService"/> as a single shared instance, exposed as
    /// <see cref="IThemeService"/> (preference API), <see cref="IStartupInitializer"/> (applied early
    /// in the launch sequence), and its concrete type (so the shell window can read the resolved
    /// theme to apply to its content root).
    /// </summary>
    public static IServiceCollection AddTheming(this IServiceCollection services)
    {
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        services.AddSingleton<IStartupInitializer>(sp => sp.GetRequiredService<ThemeService>());
        return services;
    }
}
