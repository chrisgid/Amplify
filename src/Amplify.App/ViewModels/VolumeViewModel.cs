using Amplify.Core.Spotify;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace Amplify.App.ViewModels;

/// <summary>
/// Backs the volume card on the main screen: the current level as a percentage, the slider, and
/// whether the control is usable. It is a thin, marshalling projection of <see cref="IVolumeController"/>
/// — all the step/optimistic/coalescing logic lives in the controller (and is unit-tested there); this
/// view-model only forwards changes to the UI thread for x:Bind.
/// </summary>
public sealed partial class VolumeViewModel : ObservableObject, IDisposable
{
    private readonly IVolumeController _controller;
    private readonly DispatcherQueue? _dispatcher;
    private bool _disposed;

    public VolumeViewModel(IVolumeController controller)
    {
        _controller = controller;

        // Captured on the UI thread (resolved while the main page is built); the controller may raise
        // its events from a background write/reconcile continuation, so bindable updates marshal here.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _controller.VolumeChanged += OnVolumeChanged;
        _controller.StateChanged += OnStateChanged;
    }

    /// <summary>Whether the card is interactive — connected with an active device.</summary>
    public bool CanControl => _controller.CanControl;

    /// <summary>The current volume as display text, or a placeholder when nothing can be controlled.</summary>
    public string VolumeText => _controller.CanControl ? $"{_controller.Volume}%" : " ";

    /// <summary>
    /// The slider's value, bound two-way. Setting it (user drag) pushes through the controller, which
    /// updates optimistically; a programmatic source update from the controller does not loop back.
    /// </summary>
    public double SliderValue
    {
        get => _controller.Volume;
        set
        {
            int target = (int)Math.Round(value);
            if (target != _controller.Volume)
            {
                _ = _controller.SetVolumeAsync(target);
            }
        }
    }

    /// <summary>Re-reads the current player state — called when the main page is shown.</summary>
    public Task RefreshAsync() => _controller.RefreshAsync();

    [RelayCommand]
    private Task NudgeUp() => _controller.NudgeAsync(1);

    [RelayCommand]
    private Task NudgeDown() => _controller.NudgeAsync(-1);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _controller.VolumeChanged -= OnVolumeChanged;
        _controller.StateChanged -= OnStateChanged;
    }

    private void OnVolumeChanged(object? sender, int volume) => _dispatcher.RunOnUi(NotifyVolume);

    private void OnStateChanged(object? sender, EventArgs e) => _dispatcher.RunOnUi(NotifyAll);

    private void NotifyVolume()
    {
        OnPropertyChanged(nameof(SliderValue));
        OnPropertyChanged(nameof(VolumeText));
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(CanControl));
        NotifyVolume();
    }
}
