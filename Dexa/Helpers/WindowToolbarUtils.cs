using System.Runtime.InteropServices;

namespace Else.PhoneMirror.ViewModels;

public static class WindowToolbarUtils
{
    private const uint MF_BYCOMMAND = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_ENABLED = 0x00000000;
    private const uint SC_CLOSE = 0xF060;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DrawMenuBar(IntPtr hWnd);

    /// <summary>
    /// Włącza albo wyłącza (szarzy) przycisk zamykania okna.
    /// </summary>
    /// <param name="hwnd">Handles do okna</param>
    /// <param name="enable">true = włącz, false = wyłącz</param>
    public static void SetCloseButtonEnabled(IntPtr hwnd, bool enable)
    {
        // Pobierz uchwyt do menu systemowego okna
        IntPtr hMenu = GetSystemMenu(hwnd, false);
        if (hMenu == IntPtr.Zero)
            return;

        // Ustaw stan pozycji SC_CLOSE
        uint flags = MF_BYCOMMAND | (enable ? MF_ENABLED : MF_GRAYED);
        if (!EnableMenuItem(hMenu, SC_CLOSE, flags))
        {
        }

        // Odśwież pasek tytułu, by zmiany były widoczne
        if (!DrawMenuBar(hwnd))
        {
        }
    }
}