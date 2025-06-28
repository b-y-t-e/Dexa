using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Dexa;

public static class IconHelper
{
    // Interfejsy COM do obsługi skrótów
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    public static Icon GetAppIcon()
    {
        string pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", "DexaLogo.png");
        return CreateIconFromPng(pngPath);
    }

    /// <summary>
    /// Tworzy ikonę z obrazu PNG z zachowaniem przezroczystości
    /// </summary>
    static Icon CreateIconFromPng(string pngFilePath)
    {
        using Bitmap bitmap = new Bitmap(pngFilePath);
        // Upewnij się, że format obrazu obsługuje przezroczystość
        Bitmap bitmapWithTransparency = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);

        using (Graphics g = Graphics.FromImage(bitmapWithTransparency))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
        }

        // Konwersja Bitmap na Icon
        IntPtr hicon = bitmapWithTransparency.GetHicon();
        Icon icon = Icon.FromHandle(hicon);

        // Tworzymy kopię ikony, aby zwolnić uchwyt systemowy
        Icon iconCopy = (Icon)icon.Clone();
        DestroyIcon(hicon); // Zwolnij uchwyt systemowy

        return iconCopy;
    }

    /// <summary>
    /// Zmienia ikonę okna procesu
    /// </summary>
    /// <param name="windowHandle">Uchwyt do okna procesu</param>
    /// <param name="ikona">Ikona do ustawienia</param>
    /// <returns>True jeśli operacja się powiodła</returns>
    public static bool ZmieńIkonęOkna(IntPtr windowHandle, Icon ikona)
    {
        try
        {
            if (windowHandle == IntPtr.Zero || ikona == null)
                return false;

            // Ustawienie małej ikony
            NativeWindowHelper.SendMessage(windowHandle, NativeWindowHelper.WM_SETICON,
                (IntPtr)NativeWindowHelper.ICON_SMALL, ikona.Handle);

            // Ustawienie dużej ikony
            NativeWindowHelper.SendMessage(windowHandle, NativeWindowHelper.WM_SETICON,
                (IntPtr)NativeWindowHelper.ICON_BIG, ikona.Handle);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas zmiany ikony okna: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Tworzy i zapisuje ikonę aplikacji do pliku
    /// </summary>
    /// <param name="ścieżkaDoPliku">Ścieżka, gdzie ikona ma zostać zapisana</param>
    /// <returns>Ścieżka do zapisanej ikony lub null w przypadku błędu</returns>
    static string ZapiszIkonęDoPliku(string ścieżkaDoPliku = null)
    {
        try
        {
            // Jeśli nie podano ścieżki, utwórz w katalogu aplikacji
            if (string.IsNullOrEmpty(ścieżkaDoPliku))
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                    return null;

                ścieżkaDoPliku = Path.Combine(Path.GetDirectoryName(exePath), "scrcpy.ico");
            }

            // Zapisz ikonę do pliku jeśli nie istnieje
            if (!File.Exists(ścieżkaDoPliku))
            {
                var icon = GetAppIcon();
                using (var fs = new FileStream(ścieżkaDoPliku, FileMode.Create))
                {
                    icon.Save(fs);
                }
            }

            return ścieżkaDoPliku;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas zapisywania ikony do pliku: {ex.Message}");
            return null;
        }
    }

    public static bool UstawIkonęWMenuStart(string exePath)
    {
        try
        {
            if (string.IsNullOrEmpty(exePath))
                return false;

            string iconPath = ZapiszIkonęDoPliku();
            if (string.IsNullOrEmpty(iconPath))
                return false;

            bool result = SetStartMenuIcon(exePath, iconPath);

            return result;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// Ustawia ikonę aplikacji w menu Start dla podanego pliku wykonywalnego
    /// </summary>
    /// <param name="exePath">Ścieżka do pliku wykonywalnego</param>
    /// <param name="iconPath">Ścieżka do pliku ikony</param>
    /// <returns>True jeśli operacja się powiodła</returns>
    public static bool SetStartMenuIcon(string exePath, string iconPath)
    {
        try
        {
            // Ścieżka do skrótu w menu Start
            string startMenuPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs",
                Path.GetFileNameWithoutExtension(exePath) + ".lnk");

            // Tworzenie lub aktualizacja skrótu
            IShellLink link = (IShellLink)new ShellLink();
            link.SetPath(exePath);
            link.SetIconLocation(iconPath, 0);
            link.SetWorkingDirectory(Path.GetDirectoryName(exePath));

            // Zapisanie skrótu
            IPersistFile file = (IPersistFile)link;
            file.Save(startMenuPath, false);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas ustawiania ikony w menu Start: {ex.Message}");
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
