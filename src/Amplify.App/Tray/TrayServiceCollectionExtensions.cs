using Amplify.Core.Startup;
using Amplify.Core.Tray;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Tray;

/// <summary>DI registration for the system-tray / background feature.</summary>
public static class TrayServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tray service (one singleton exposed as both <see cref="ITrayService"/> and the
    /// launch-time <see cref="IStartupInitializer"/>) and the launch-at-startup manager. The tray service
    /// owns the tray icon and the window's minimise/close-to-tray behaviour; the container disposes it on
    /// shutdown, removing the icon.
    /// </summary>
    public static IServiceCollection AddSystemTray(this IServiceCollection services)
    {
        services.AddSingleton<IStartupTaskManager, StartupTaskManager>();
        services.AddSingleton<TrayService>();
        services.AddSingleton<ITrayService>(sp => sp.GetRequiredService<TrayService>());
        services.AddSingleton<IStartupInitializer>(sp => sp.GetRequiredService<TrayService>());
        return services;
    }
}
