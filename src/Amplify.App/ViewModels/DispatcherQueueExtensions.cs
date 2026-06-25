using Microsoft.UI.Dispatching;

namespace Amplify.App.ViewModels;

/// <summary>
/// Shared helper for view-models that capture a <see cref="DispatcherQueue"/> on construction (while
/// resolved on the UI thread) so they can safely touch bindable state from a continuation that may
/// resume on a different thread.
/// </summary>
internal static class DispatcherQueueExtensions
{
    /// <summary>
    /// Runs <paramref name="action"/> immediately if already on <paramref name="dispatcher"/>'s thread
    /// (or there is no captured dispatcher), otherwise marshals it via <see cref="DispatcherQueue.TryEnqueue(DispatcherQueueHandler)"/>.
    /// </summary>
    public static void RunOnUi(this DispatcherQueue? dispatcher, Action action)
    {
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            dispatcher.TryEnqueue(() => action());
        }
    }
}
