using System.Diagnostics;
using System.Runtime.InteropServices;
using Else.PhoneMirror.ViewModels;

namespace Dexa;

public static class KeyboardInterceptorWinForms
{
    // Definicja struktury KBDLLHOOKSTRUCT
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // Stałe WinAPI
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    // Kody wybranych klawiszy - można dodać więcej według potrzeb
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

    // Flaga do włączania logowania diagnostycznego
    private static bool _enableLogging = true;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    static private LowLevelKeyboardProc _proc; // referencja, by GC nie zebrał
    static private IntPtr _hookID = IntPtr.Zero;

    // Publiczna klasa dla informacji o zdarzeniu klawiatury
    public class KeyboardEventArgs
    {
        public int KeyCode { get; set; }
        public bool IsKeyDown { get; set; }
        public bool IsSystemKey { get; set; }
        public IntPtr WindowHandle { get; set; }
    }

    // Delegat do powiadamiania o naciśnięciu klawisza
    public delegate void KeyboardEventDelegate(KeyboardEventArgs e);

    // Zdarzenie wywoływane, gdy naciśnięty zostanie dowolny klawisz
    public static event KeyboardEventDelegate KeyboardEvent;

    // Stare zdarzenie F11 - zachowane dla kompatybilności
    public delegate void F11KeyPressedDelegate(IntPtr windowHandle);

    // Flaga informująca czy inicjalizacja została wykonana
    private static bool _initialized = false;

    static KeyboardInterceptorWinForms()
    {
        InitializeHook();
        ThreadPool.QueueUserWorkItem(_ => { KeysQueue(); });
    }

    // Publiczna metoda do jawnej inicjalizacji hooka
    public static void InitializeHook()
    {
        if (!_initialized)
        {
            Console.WriteLine("KeyboardInterceptor: Rozpoczęcie inicjalizacji hooka");

            // Zapisujemy referencję do procedury callback, żeby GC jej nie zebrał
            _proc = HookCallback;

            // Ustawiamy hook
            _hookID = SetHook(_proc);

            if (_enableLogging)
            {
                Console.WriteLine($"KeyboardInterceptor: Inicjalizacja, uchwyt hooka: 0x{_hookID.ToInt64():X}");
                if (_hookID == IntPtr.Zero)
                {
                    Console.WriteLine(
                        $"KeyboardInterceptor: Błąd podczas ustawiania hooka. Kod błędu: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Console.WriteLine("KeyboardInterceptor: Hook ustawiony pomyślnie");
                }
            }

            _initialized = true;
        }
    }

    static private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        try
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr hMod = GetModuleHandle(curModule.ModuleName);
                IntPtr hookId = SetWindowsHookEx(WH_KEYBOARD_LL, proc, hMod, 0);

                if (hookId == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"KeyboardInterceptor: Błąd podczas ustawiania hooka. Kod błędu: {error}");
                }

                return hookId;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"KeyboardInterceptor: Wyjątek podczas ustawiania hooka: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    static private List<KeyParam> _keyParams = new List<KeyParam>();

    class KeyParam
    {
        public int nCode;
        public IntPtr wParam;
        public IntPtr lParam;
    }

    private static void KeysQueue()
    {
        while (true)
        {
            Thread.Sleep(5);

            KeyParam? key = null;
            lock (_keyParams)
            {
                key = _keyParams.FirstOrDefault();
                if (key != null)
                    _keyParams.Remove(key);
                else
                    continue;
            }

            try
            {
                if (_enableLogging)
                {
                    Console.WriteLine(
                        $"KeyboardInterceptor: HookCallback wywołany - nCode: {key.nCode}, wParam: 0x{key.wParam.ToInt64():X}");
                }

                if (key.nCode >= 0)
                {
                    // wParam może być WM_KEYDOWN/WM_KEYUP itd.
                    int msg = key.wParam.ToInt32();
                    bool isKeyDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
                    bool isSystemKey = (msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP);

                    if (_enableLogging)
                    {
                        Console.WriteLine(
                            $"KeyboardInterceptor: Typ wiadomości: 0x{msg:X} (KEYDOWN=0x100, SYSKEYDOWN=0x104)");
                    }

                    if (isKeyDown)
                    {
                        // odczytaj virtual-key z lParam
                        KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(key.lParam);
                        int vkCode = (int)hookStruct.vkCode;

                        if (_enableLogging)
                        {
                            Console.WriteLine(
                                $"KeyboardInterceptor: Wykryto klawisz o kodzie {vkCode} (0x{vkCode:X2})");
                        }

                        // Wywołanie zdarzenia dla dowolnego klawisza
                        OnKeyEvent(vkCode, isKeyDown, isSystemKey);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"KeyboardInterceptor: Wyjątek w HookCallback: {ex.Message}");
            }
        }
    }

    static private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        lock (_keyParams)
        {
            _keyParams.Add(new KeyParam()
            {
                nCode = nCode,
                wParam = wParam,
                lParam = lParam
            });
        }

        // przekaz dalej - zawsze musimy to zrobić, nawet jeśli wystąpił wyjątek
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    // Nowa metoda obsługująca zdarzenia klawiatury
    static private void OnKeyEvent(int keyCode, bool isKeyDown, bool isSystemKey)
    {
        // Pobierz aktualnie aktywne okno
        IntPtr foregroundWindow = GetForegroundWindow();

        if (_enableLogging)
        {
            Console.WriteLine(
                $"KeyboardInterceptor: Zdarzenie klawisza {keyCode} dla okna: 0x{foregroundWindow.ToInt64():X}");
            Console.WriteLine($"KeyboardInterceptor: Liczba monitorowanych okien: {_monitoredWindowHandles.Count}");
        }

        // Sprawdź, czy okno należy do monitorowanych
        if (_monitoredWindowHandles.Contains(foregroundWindow))
        {
            if (_enableLogging)
            {
                Console.WriteLine(
                    "KeyboardInterceptor: Znaleziono aktywne monitorowane okno, wywołuję zdarzenie KeyboardEvent");
            }

            // Wywołaj zdarzenie KeyboardEvent
            KeyboardEvent?.Invoke(new KeyboardEventArgs
            {
                KeyCode = keyCode,
                IsKeyDown = isKeyDown,
                IsSystemKey = isSystemKey,
                WindowHandle = foregroundWindow
            });
        }
        else if (_enableLogging)
        {
            Console.WriteLine("KeyboardInterceptor: Aktywne okno nie jest monitorowane");
        }
    }


    // Lista uchwytów okien, które monitorujemy
    static private HashSet<IntPtr> _monitoredWindowHandles = new HashSet<IntPtr>();

    // Dodaj uchwyt okna do monitorowanych
    static public void AddWindowHandle(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            _monitoredWindowHandles.Add(hwnd);
            if (_enableLogging)
            {
                Console.WriteLine($"KeyboardInterceptor: Dodano okno 0x{hwnd.ToInt64():X} do monitorowanych");
                Console.WriteLine($"KeyboardInterceptor: Liczba monitorowanych okien: {_monitoredWindowHandles.Count}");
            }
        }
        else if (_enableLogging)
        {
            Console.WriteLine("KeyboardInterceptor: Próba dodania pustego uchwytu okna");
        }
    }

    // Usuń uchwyt okna z monitorowanych
    static public void RemoveWindowHandle(IntPtr hwnd)
    {
        if (_monitoredWindowHandles.Remove(hwnd) && _enableLogging)
        {
            Console.WriteLine($"KeyboardInterceptor: Usunięto okno 0x{hwnd.ToInt64():X} z monitorowanych");
            Console.WriteLine($"KeyboardInterceptor: Liczba monitorowanych okien: {_monitoredWindowHandles.Count}");
        }
        else if (_enableLogging)
        {
            Console.WriteLine($"KeyboardInterceptor: Nie znaleziono okna 0x{hwnd.ToInt64():X} do usunięcia");
        }
    }

    // Zatrzymaj monitorowanie klawiszy
    static public void Stop()
    {
        if (_hookID != IntPtr.Zero)
        {
            Console.WriteLine("KeyboardInterceptor: Zatrzymywanie hooka");
            bool result = UnhookWindowsHookEx(_hookID);
            if (result)
            {
                Console.WriteLine("KeyboardInterceptor: Hook zatrzymany pomyślnie");
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"KeyboardInterceptor: Błąd podczas zatrzymywania hooka. Kod błędu: {error}");
            }

            _hookID = IntPtr.Zero;
            _initialized = false;
        }
    }

    // Metoda do ponownego uruchomienia hooka
    static public void Restart()
    {
        Console.WriteLine("KeyboardInterceptor: Ponowne uruchamianie hooka");
        Stop();
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
