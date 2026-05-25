using System.Runtime.InteropServices;

namespace Dexa;

/// <summary>
/// Klasa pomocnicza zawierająca funkcje Win32 API do zarządzania oknami
/// </summary>
public static class NativeWindowHelper
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    public static extern bool SetWindowText(IntPtr hWnd, string lpString);

    /// <summary>
    /// Struktura przechowująca informacje o wymiarach okna
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        /// <summary>
        /// Szerokość prostokąta
        /// </summary>
        public int Width => Right - Left;

        /// <summary>
        /// Wysokość prostokąta
        /// </summary>
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Pobiera wymiary prostokąta okna
    /// </summary>
    /// <param name="hWnd">Uchwyt do okna</param>
    /// <param name="lpRect">Referencja do struktury RECT, która zostanie wypełniona danymi</param>
    /// <returns>True jeśli operacja się powiodła, w przeciwnym razie false</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

    /// <summary>
    /// Ustawia okno na wierzchu (na pierwszym planie)
    /// </summary>
    /// <param name="hWnd">Uchwyt do okna</param>
    /// <returns>True jeśli operacja się powiodła, w przeciwnym razie false</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Pokazuje okno
    /// </summary>
    /// <param name="hWnd">Uchwyt do okna</param>
    /// <param name="nCmdShow">Flaga określająca jak okno ma być pokazane</param>
    /// <returns>True jeśli okno było wcześniej widoczne</returns>
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Przywraca okno, które zostało zminimalizowane lub zmaksymalizowane
    /// </summary>
    /// <param name="hWnd">Uchwyt do okna</param>
    /// <returns>True jeśli okno było wcześniej widoczne</returns>
    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const uint WM_SETICON = 0x0080;
    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_SHOWNOACTIVATE = 4;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_SHOWMINNOACTIVE = 7;
    public const int SW_SHOWNA = 8;
    public const int SW_RESTORE = 9;
    public const int SW_SHOWDEFAULT = 10;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST   = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint SWP_NOSIZE     = 0x0001;
    public const uint SWP_NOMOVE     = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
}
