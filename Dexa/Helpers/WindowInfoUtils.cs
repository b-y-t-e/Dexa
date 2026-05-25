using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Dexa;

public static class WindowInfoUtils
{
    /// <summary>
    /// Pobiera wszystkie interesujące nas informacje o oknie.
    /// </summary>
    public static WindowInfo? GetWindowInfo(IntPtr hwnd)
    {
        // 1. Position from DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS).
        //    On Windows 10/11 GetWindowRect includes the invisible DWM resize border
        //    which causes position drift on each save/restore cycle (scrcpy uses
        //    SDL_SetWindowPosition which positions the visible frame, not the extended
        //    frame). DWMWA_EXTENDED_FRAME_BOUNDS gives coordinates closer to what SDL
        //    uses, reducing drift significantly.
        var extRect = new NativeMethods.RECT();
        int hr = NativeMethods.DwmGetWindowAttribute(
            hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out extRect, Marshal.SizeOf<NativeMethods.RECT>());
        if (hr != 0)
        {
            // Fallback: DWM unavailable (e.g. compositor disabled)
            if (!NativeMethods.GetWindowRect(hwnd, out extRect))
                return null;
        }

        // Size from client area (matches SDL_SetWindowSize / scrcpy --window-width/height)
        var clientRect = new NativeMethods.RECT();
        if (!NativeMethods.GetClientRect(hwnd, out clientRect))
            return null;

        var bounds = new Rectangle(
            extRect.Left, extRect.Top,
            clientRect.Right,   // clientRect.Left is always 0
            clientRect.Bottom); // clientRect.Top is always 0

        // 2. State
        var placement = new NativeMethods.WINDOWPLACEMENT();
        placement.length = Marshal.SizeOf(placement);
        if (!NativeMethods.GetWindowPlacement(hwnd, ref placement))
            return null;

        FormWindowState state = placement.showCmd switch
        {
            NativeMethods.ShowCmd.Maximized => FormWindowState.Maximized,
            NativeMethods.ShowCmd.Minimized => FormWindowState.Minimized,
            _ => FormWindowState.Normal,
        };

        return new WindowInfo
        {
            Bounds = bounds,
            State = state
        };
    }

    public static Rectangle ValidateWindowBounds(Rectangle bounds)
    {
        var screen = Screen.FromRectangle(bounds);
        var mon = screen.Bounds;
        int captionHeight = SystemInformation.CaptionHeight;

        int width = Math.Min(bounds.Width, mon.Width);
        int height = Math.Min(bounds.Height, mon.Height);
        int x = Math.Max(mon.Left, Math.Min(mon.Right - width, bounds.X));
        int y = Math.Max(mon.Top, Math.Min(mon.Bottom - captionHeight, bounds.Y));

        return new Rectangle(x, y, width, height);
    }

}
