namespace Amplify.Core.Startup;

/// <summary>
/// Runs <see cref="IStartupInitializer"/> instances in ascending <see cref="IStartupInitializer.Order"/>.
/// This is the deterministic launch-sequence ordering the application shell drives; it is kept here,
/// free of any UI/platform dependency, so it can be unit-tested.
/// </summary>
public static class StartupInitializerRunner
{
    /// <summary>
    /// Invokes <see cref="IStartupInitializer.OnLaunchedAsync"/> on each initializer in ascending
    /// <see cref="IStartupInitializer.Order"/>, awaiting each before starting the next so ordering is
    /// observable (e.g. theme applied before the window is shown).
    /// </summary>
    /// <param name="initializers">The registered initializers, in any order.</param>
    /// <param name="ct">Cancels the sequence; checked before each initializer.</param>
    public static async Task RunAsync(IEnumerable<IStartupInitializer> initializers, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(initializers);

        foreach (IStartupInitializer initializer in initializers.OrderBy(i => i.Order))
        {
            ct.ThrowIfCancellationRequested();
            await initializer.OnLaunchedAsync(ct);
        }
    }
}
