using Amplify.App.ViewModels;
using Amplify.Core.Hotkeys;
using Amplify.Core.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Hotkeys;

/// <summary>DI registration for the global-hotkey feature.</summary>
public static class HotkeyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the global-hotkey service, its launch-time registrar, and the hotkeys view-model.
    /// The service is a singleton (it owns the keyboard hook and the live registrations) and the
    /// container disposes it on shutdown, removing the hook.
    /// </summary>
    public static IServiceCollection AddHotkeys(this IServiceCollection services)
    {
        services.AddSingleton<IHotkeyService, KeyboardHookHotkeyService>();
        services.AddSingleton<IStartupInitializer, HotkeyRegistrar>();
        services.AddSingleton<HotkeysViewModel>();
        return services;
    }
}
