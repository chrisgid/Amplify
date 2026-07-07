using Amplify.Core.Notifications;
using Amplify.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Amplify.App.Notifications;

/// <summary>DI registration for the first-run tray-hint notification feature.</summary>
public static class NotificationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the notification service (one singleton exposed as both <see cref="INotificationService"/>
    /// and the launch-time <see cref="IStartupInitializer"/> that subscribes it to the tray's hide event)
    /// along with the localised hint copy resolved from the shared resource file.
    /// </summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton(_ =>
        {
            var strings = new ResourceLoader();
            return new TrayHintCopy(
                strings.GetString("Notification_TrayHint_Title"),
                strings.GetString("Notification_TrayHint_Message"));
        });
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<IStartupInitializer>(sp => sp.GetRequiredService<NotificationService>());
        return services;
    }
}
