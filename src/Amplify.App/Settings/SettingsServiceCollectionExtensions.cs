using Amplify.App.ViewModels;
using Amplify.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace Amplify.App.Settings;

/// <summary>DI registration for the settings persistence layer and its view-model.</summary>
internal static class SettingsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the file-backed <see cref="ISettingsService"/> (rooted at the per-user local data
    /// folder) and the <see cref="SettingsViewModel"/> the settings screen binds to.
    /// </summary>
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService>(sp =>
            new SettingsService(ResolveDataDirectory(), sp.GetRequiredService<ILogger<SettingsService>>()));
        services.AddSingleton<SettingsViewModel>();
        return services;
    }

    private static string ResolveDataDirectory()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch (InvalidOperationException)
        {
            // No package identity (unpackaged run): keep settings working off a temp location,
            // mirroring where the file logger falls back to.
            return Path.Combine(Path.GetTempPath(), "Amplify");
        }
    }
}
