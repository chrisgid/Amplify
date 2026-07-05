using Amplify.Core.Settings;

namespace Amplify.Core.Windowing;

/// <summary>A screen rectangle in device (physical) pixels — the space the WinUI <c>AppWindow</c> uses.</summary>
/// <param name="X">Left edge in screen coordinates.</param>
/// <param name="Y">Top edge in screen coordinates.</param>
/// <param name="Width">Width in device pixels.</param>
/// <param name="Height">Height in device pixels.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>
/// Pure geometry for placing the main window: restoring a remembered footprint when it is still
/// usable, and centring on a display otherwise. Kept free of WinUI types so the "is the saved
/// placement still on-screen?" decision — the fiddly part — is unit-testable; the app supplies the
/// real display work areas from <c>DisplayArea</c>.
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// How far below the window's top edge to probe for a grabbable title-bar point, in device pixels.
    /// Comfortably inside the caption strip at any DPI, so a window whose caption sits on a connected
    /// display is treated as reachable (the user can drag the rest into view).
    /// </summary>
    private const int _captionProbeOffset = 16;

    /// <summary>
    /// Computes where to restore a remembered window, or reports that it is no longer usable. The saved
    /// size is grown to at least (<paramref name="minWidth"/>, <paramref name="minHeight"/>); the saved
    /// position is kept only when the title bar lands on one of <paramref name="workAreas"/> — otherwise
    /// the placement is off-screen (e.g. a monitor was unplugged or rearranged) and the caller should
    /// centre instead.
    /// </summary>
    /// <returns><see langword="true"/> with <paramref name="bounds"/> set when the window can be restored.</returns>
    public static bool TryGetRestoreBounds(
        WindowState saved,
        IReadOnlyList<PixelRect> workAreas,
        int minWidth,
        int minHeight,
        out PixelRect bounds)
    {
        ArgumentNullException.ThrowIfNull(workAreas);

        int width = Math.Max(saved.Width, minWidth);
        int height = Math.Max(saved.Height, minHeight);

        // Probe the middle of the caption strip: if that point is on a connected display, the window is
        // reachable and can be dragged fully into view, so the remembered position is safe to keep.
        int grabX = saved.X + width / 2;
        int grabY = saved.Y + _captionProbeOffset;

        if (workAreas.Any(area => Contains(area, grabX, grabY)))
        {
            bounds = new PixelRect(saved.X, saved.Y, width, height);
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>Centres a window of the given size within <paramref name="workArea"/>.</summary>
    public static PixelRect Center(int width, int height, PixelRect workArea) =>
        new(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height);

    private static bool Contains(PixelRect area, int x, int y) =>
        x >= area.X && x < area.X + area.Width && y >= area.Y && y < area.Y + area.Height;
}
