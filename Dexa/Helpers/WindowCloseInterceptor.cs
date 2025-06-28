using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dexa;

public class WindowCloseInterceptor : IDisposable
{
    // Low-level hook IDs
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;

    // Wiadomości
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_SYSKEYDOWN = 0x0104;

    // Wyniki hit-testu
    private const int HTCLOSE = 20;

    // Klawisz F4
    private const int VK_F4 = 0x73;

    // ShowWindow
    private const int SW_MINIMIZE = 6;

    // Hook-proc delegate’y
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Hook handle’y
    private IntPtr _mouseHookId;
    private IntPtr _keyboardHookId;

    // Referencje, żeby callback’i nie były wyrzucone przez GC
    private readonly LowLevelMouseProc _mouseProc;
    private readonly LowLevelKeyboardProc _keyboardProc;

    // HWND targetu
    private readonly IntPtr _targetHwnd;

    public WindowCloseInterceptor(IntPtr targetHwnd)
    {
        _targetHwnd = targetHwnd;
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;

        // Instalujemy oba hooki
        _mouseHookId = SetHook(_mouseProc, WH_MOUSE_LL);
        _keyboardHookId = SetHook(_keyboardProc, WH_KEYBOARD_LL);
    }

    private IntPtr SetHook(Delegate proc, int hookId)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
        return SetWindowsHookEx(hookId, proc, moduleHandle, 0);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
        {
            var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int x = ms.pt.x, y = ms.pt.y;

            // Sprawdźmy, czy to close-button naszego okna
            IntPtr ht = SendMessage(_targetHwnd,
                WM_NCHITTEST,
                IntPtr.Zero,
                (IntPtr)((y << 16) | (x & 0xFFFF)));
            if ((int)ht == HTCLOSE)
            {
                // minimalizujemy i blokujemy oryginał
                ShowWindow(_targetHwnd, SW_MINIMIZE);
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_SYSKEYDOWN)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (kb.vkCode == VK_F4)
            {
                ShowWindow(_targetHwnd, SW_MINIMIZE);
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_mouseHookId != IntPtr.Zero) UnhookWindowsHookEx(_mouseHookId);
        if (_keyboardHookId != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHookId);
    }

    // === P/Invoke ===

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x, y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }
}