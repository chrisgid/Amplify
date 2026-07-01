using System.Diagnostics;
using System.Runtime.InteropServices;
using Amplify.Core.Tray;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Amplify.App;

/// <summary>
/// Custom entry point (the generated <c>Main</c> is disabled via <c>DISABLE_XAML_GENERATED_MAIN</c>) so
/// single-instancing can be decided before any window — or the DI host — is created. A second launch
/// redirects its activation to the already-running instance and exits; that instance surfaces its window.
/// </summary>
public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (DecideRedirection())
        {
            // Another instance owns the key; we've handed our activation to it and can exit.
            return 0;
        }

        Application.Start(p =>
        {
            DispatcherQueueSynchronizationContext context = new(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }

    // Registers this process as the single "main" instance; if another already holds that key, this
    // returns true after redirecting our activation to it.
    private static bool DecideRedirection()
    {
        AppActivationArguments activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey(TrayConstants.SingleInstanceKey);

        if (keyInstance.IsCurrent)
        {
            return false;
        }

        RedirectActivationTo(activationArgs, keyInstance);
        return true;
    }

    // RedirectActivationToAsync must be awaited, but blocking the STA directly would deadlock, so the
    // redirect runs on the thread pool and this thread waits on a Win32 event via a non-pumping wait.
    private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        nint eventHandle = CreateEvent(nint.Zero, bManualReset: true, bInitialState: false, lpName: null);
        Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            SetEvent(eventHandle);
        });

        const uint cwmoDefault = 0;
        const uint infinite = 0xFFFFFFFF;
        _ = CoWaitForMultipleObjects(cwmoDefault, infinite, 1, [eventHandle], out _);

        // Nudge the existing instance's window to the foreground for good measure.
        using Process process = Process.GetProcessById((int)keyInstance.ProcessId);
        SetForegroundWindow(process.MainWindowHandle);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CreateEvent(nint lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetEvent(nint hEvent);

    [DllImport("ole32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint CoWaitForMultipleObjects(uint dwFlags, uint dwMilliseconds, ulong nHandles, nint[] pHandles, out uint dwIndex);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
