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
        // 0. Parametry do korekty
        var screen = Screen.FromRectangle(info.Bounds);
        var mon = screen.Bounds; // pełny wymiar monitora
        int captionHeight = SystemInformation.CaptionHeight; // wysokość belki tytułu

        // 1. Skoryguj rozmiar, jeśli okno jest większe od monitora
        int width = Math.Min(info.Bounds.Width, mon.Width);
        int height = Math.Min(info.Bounds.Height, mon.Height);

        // 2. Skoryguj pozycję X (całe okno na szerokość)
        int x = Math.Max(mon.Left, Math.Min(mon.Right - width, info.Bounds.X));

        // 3. Skoryguj pozycję Y tak, żeby belka tytułu była widoczna
        //    — nie wyżej niż mon.Top, nie niżej niż mon.Bottom - captionHeight
        int maxY = mon.Bottom - captionHeight;
        int y = Math.Max(mon.Top, Math.Min(maxY, info.Bounds.Y));

        // 4. Zaktualizuj prostokąt
        info.Bounds = new Rectangle(x, y, width, height);

        // ———————— poniżej bez zmian z oryginału ————————

        // Jeśli stan != Normal, przywróć go najpierw przez SetWindowPlacement
        if (info.State != FormWindowState.Normal)
        {
            var placement = new NativeMethods.WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>(),
                showCmd = (info.State == FormWindowState.Maximized)
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
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return;
        }

        // Gdy Normal — przesuń/zmień rozmiar
        if (!NativeMethods.MoveWindow(
                hwnd,
                info.Bounds.Left,
                info.Bounds.Top,
                info.Bounds.Width,
                info.Bounds.Height,
                true))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
