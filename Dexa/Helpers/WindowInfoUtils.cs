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
        // 1. Bounds
        var rect = new NativeMethods.RECT();
        if (!NativeMethods.GetWindowRect(hwnd, out rect))
            return null;

        var bounds = new Rectangle(
            rect.Left, rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top);

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

    /// <summary>
    /// Ustawia parametry okna zgodnie z obiektem WindowInfo.
    /// </summary>
    public static void SetWindowInfo(IntPtr hwnd, WindowInfo info)
    {
        // Ensure window is within screen boundaries
        var screens = Screen.AllScreens;
        var virtualScreenBounds = new Rectangle(
            SystemInformation.VirtualScreen.Left,
            SystemInformation.VirtualScreen.Top,
            SystemInformation.VirtualScreen.Width,
            SystemInformation.VirtualScreen.Height);

        if (!virtualScreenBounds.Contains(info.Bounds))
        {
            info.Bounds = new Rectangle(
                Math.Max(virtualScreenBounds.Left, Math.Min(virtualScreenBounds.Right - info.Bounds.Width, info.Bounds.X)),
                Math.Max(virtualScreenBounds.Top, Math.Min(virtualScreenBounds.Bottom - info.Bounds.Height, info.Bounds.Y)),
                info.Bounds.Width,
                info.Bounds.Height);
        }


        // 1. Jeśli trzeba: przywróć do normalnego stanu, żeby móc zmienić pozycję/rozmiar
        if (info.State != FormWindowState.Normal)
        {
            // Ustawiamy stan przez SetWindowPlacement
            var placement = new NativeMethods.WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>(),
                showCmd = info.State == FormWindowState.Maximized
                    ? NativeMethods.ShowCmd.Maximized
                    : NativeMethods.ShowCmd.Minimized,
                rcNormalPosition = new NativeMethods.RECT
                {
                    Left = info.Bounds.Left,
                    Top = info.Bounds.Top,
                    Right = info.Bounds.Right,
                    Bottom = info.Bounds.Bottom
                }
            };

            if (!NativeMethods.SetWindowPlacement(hwnd, ref placement))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            return; // po ustawieniu stanu nie musimy nic więcej robić
        }

        // 2. Jeśli stan NORMAL, to przesuwamy/skalujemy:
        if (!NativeMethods.MoveWindow(
                hwnd,
                info.Bounds.Left,
                info.Bounds.Top,
                info.Bounds.Width,
                info.Bounds.Height,
                true))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
