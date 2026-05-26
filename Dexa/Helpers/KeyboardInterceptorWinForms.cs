using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Else.PhoneMirror.ViewModels;

namespace Dexa;

public static class KeyboardInterceptorWinForms
{
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    public static class VirtualKeys
    {
        public const int F1 = 0x70;
        public const int F2 = 0x71;
        public const int F3 = 0x72;
        public const int F4 = 0x73;
        public const int F5 = 0x74;
        public const int F6 = 0x75;
        public const int F7 = 0x76;
        public const int F8 = 0x77;
        public const int F9 = 0x78;
        public const int F10 = 0x79;
        public const int F11 = 0x7A;
        public const int F12 = 0x7B;
        public const int LEFT = 0x25;
        public const int UP = 0x26;
        public const int RIGHT = 0x27;
        public const int DOWN = 0x28;
        public const int ESCAPE = 0x1B;
        public const int HOME = 36;
        public const int SPACE = 0x20;
        public const int RETURN = 0x0D;
        public const int TAB = 0x09;
        public const int CTRL = 0x11;
        public const int ALT = 0x12;
        public const int SHIFT = 0x10;
        public const int LWIN = 0x5B;
        public const int RWIN = 0x5C;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    static private LowLevelKeyboardProc _proc;
    static private IntPtr _hookID = IntPtr.Zero;
    private static volatile bool _initialized = false;

    public class KeyboardEventArgs
    {
        public int KeyCode { get; set; }
        public bool IsKeyDown { get; set; }
        public bool IsSystemKey { get; set; }
        public IntPtr WindowHandle { get; set; }
    }

    public delegate void KeyboardEventDelegate(KeyboardEventArgs e);
    public static event KeyboardEventDelegate KeyboardEvent;

    private class KeyParam
    {
        public int nCode;
        public IntPtr wParam;
        public KBDLLHOOKSTRUCT hookStruct;
    }

    static private readonly ConcurrentQueue<KeyParam> _keyParams = new();

    static private readonly object _handleLock = new object();
    static private readonly HashSet<IntPtr> _monitoredWindowHandles = new HashSet<IntPtr>();

    static private readonly object _hookLock = new object();

    private static CancellationTokenSource _cts = new CancellationTokenSource();

    static KeyboardInterceptorWinForms()
    {
        InitializeHook();
        ThreadPool.QueueUserWorkItem(_ => { KeysQueue(_cts.Token); });
    }

    public static void InitializeHook()
    {
        lock (_hookLock)
        {
            if (!_initialized)
            {
                _proc = HookCallback;
                _hookID = SetHook(_proc);
                _initialized = true;
            }
        }
    }

    static private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        try
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static void KeysQueue(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!_keyParams.TryDequeue(out var key))
            {
                Thread.Sleep(5);
                continue;
            }

            try
            {
                int vkCode = (int)key.hookStruct.vkCode;
                bool isSystemKey = (key.wParam.ToInt32() == WM_SYSKEYDOWN);
                OnKeyEvent(vkCode, true, isSystemKey);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"KeyboardInterceptor: {ex.Message}");
            }
        }
    }

    static private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                _keyParams.Enqueue(new KeyParam
                {
                    nCode = nCode,
                    wParam = wParam,
                    hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam)
                });
            }
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    static private void OnKeyEvent(int keyCode, bool isKeyDown, bool isSystemKey)
    {
        IntPtr foregroundWindow = GetForegroundWindow();

        bool isMonitored;
        lock (_handleLock)
        {
            isMonitored = _monitoredWindowHandles.Contains(foregroundWindow);
        }

        if (isMonitored)
        {
            KeyboardEvent?.Invoke(new KeyboardEventArgs
            {
                KeyCode = keyCode,
                IsKeyDown = isKeyDown,
                IsSystemKey = isSystemKey,
                WindowHandle = foregroundWindow
            });
        }
    }

    static public void AddWindowHandle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        lock (_handleLock)
        {
            _monitoredWindowHandles.Add(hwnd);
        }
    }

    static public void RemoveWindowHandle(IntPtr hwnd)
    {
        lock (_handleLock)
        {
            _monitoredWindowHandles.Remove(hwnd);
        }
    }

    static public void Stop()
    {
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
            _initialized = false;
        }

        _cts.Cancel();
    }

    static public void Restart()
    {
        Stop();
        _cts = new CancellationTokenSource();
        ThreadPool.QueueUserWorkItem(_ => { KeysQueue(_cts.Token); });
        InitializeHook();
    }

    #region PInvoke

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    #endregion
}
