using Amplify.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.ConnectionStatus;

/// <summary>Registers the connection status block's view-model.</summary>
internal static class ConnectionStatusServiceCollectionExtensions
{
    public static IServiceCollection AddConnectionStatus(this IServiceCollection services)
    {
        services.AddSingleton<StatusViewModel>();
        return services;
    }
}
