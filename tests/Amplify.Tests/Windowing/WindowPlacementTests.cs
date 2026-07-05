using Amplify.Core.Settings;
using Amplify.Core.Windowing;

namespace Amplify.Tests.Windowing;

public class WindowPlacementTests
{
    private const int _minWidth = 560;
    private const int _minHeight = 480;

    private static readonly PixelRect _primary = new(0, 0, 1920, 1040);

    [Fact]
    public void RestoresSavedBoundsWhenFullyOnAConnectedDisplay()
    {
        WindowState saved = new(Width: 600, Height: 760, X: 400, Y: 100);

        bool restored = WindowPlacement.TryGetRestoreBounds(saved, [_primary], _minWidth, _minHeight, out PixelRect bounds);

        Assert.True(restored);
        Assert.Equal(new PixelRect(400, 100, 600, 760), bounds);
    }

    [Fact]
    public void GrowsSavedSizeUpToTheMinimum()
    {
        WindowState saved = new(Width: 300, Height: 200, X: 400, Y: 100);

        bool restored = WindowPlacement.TryGetRestoreBounds(saved, [_primary], _minWidth, _minHeight, out PixelRect bounds);

        Assert.True(restored);
        Assert.Equal(new PixelRect(400, 100, _minWidth, _minHeight), bounds);
    }

    [Fact]
    public void RejectsPlacementWhoseTitleBarIsOffToTheLeftOfEveryDisplay()
    {
        // A monitor that was unplugged leaves the window at negative coordinates no display covers.
        WindowState saved = new(Width: 600, Height: 760, X: -2000, Y: 100);

        bool restored = WindowPlacement.TryGetRestoreBounds(saved, [_primary], _minWidth, _minHeight, out PixelRect bounds);

        Assert.False(restored);
        Assert.Equal(default, bounds);
    }

    [Fact]
    public void RejectsPlacementWhoseTitleBarIsAboveEveryDisplay()
    {
        WindowState saved = new(Width: 600, Height: 760, X: 400, Y: -500);

        bool restored = WindowPlacement.TryGetRestoreBounds(saved, [_primary], _minWidth, _minHeight, out _);

        Assert.False(restored);
    }

    [Fact]
    public void RestoresOntoASecondaryDisplay()
    {
        PixelRect secondary = new(1920, 0, 1920, 1080);
        WindowState saved = new(Width: 600, Height: 760, X: 2200, Y: 50);

        bool restored = WindowPlacement.TryGetRestoreBounds(
            saved, [_primary, secondary], _minWidth, _minHeight, out PixelRect bounds);

        Assert.True(restored);
        Assert.Equal(new PixelRect(2200, 50, 600, 760), bounds);
    }

    [Fact]
    public void CenterPlacesTheWindowInTheMiddleOfTheWorkArea()
    {
        PixelRect centred = WindowPlacement.Center(600, 760, _primary);

        Assert.Equal(new PixelRect(660, 140, 600, 760), centred);
    }
}
