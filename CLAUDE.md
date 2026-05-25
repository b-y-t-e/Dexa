# Dexa — Claude Code onboarding

## Czym jest Dexa

WPF tray application (C#, .NET) do zarządzania urządzeniami Android z systemowego zasobnika.
Funkcje: mirroring ekranu (przez scrcpy), audio passthrough, auto-rotate, zarządzanie wieloma urządzeniami.

## Jak budować

### Dexa (C#)
```
dotnet build Dexa/Dexa.csproj
```

### scrcpy (C, MSYS2 MINGW64)
scrcpy jest dołączone jako patched binary w `Dexa/scrcpy/`. Rebuild tylko gdy zmieniasz patche.

```powershell
$bash = "C:\tools\msys64\usr\bin\bash.exe"
$buildDir = "D:/work/sources/scrcpy-src/build-dexa"
& $bash --login -c "export MSYSTEM=MINGW64 && source /etc/profile && ninja -C '$buildDir'"
Copy-Item "$buildDir\app\scrcpy.exe" "Dexa\scrcpy\scrcpy.exe" -Force
dotnet build Dexa/Dexa.csproj   # WYMAGANE — kopiuje scrcpy.exe do bin/Debug
```

**Ważne:** Dexa uruchamia scrcpy z `bin\Debug\net10.0-windows\scrcpy\`, nie z `Dexa\scrcpy\`.
Po każdej zmianie scrcpy.exe trzeba też przebudować Dexa, żeby nowy exe trafił do bin.

Pełna instrukcja (setup meson, zależności): patrz `D:\work\sources\scrcpy-src\scrcpy-patches.md`.

**Krytyczne:** scrcpy musi być zbudowane z `-Dportable=true` — bez tego szuka `scrcpy-server`
pod ścieżką instalacji MSYS2 zamiast obok `scrcpy.exe`.

## Architektura

```
App.xaml.cs                  — entry point, DeviceWatcherThread (10s poll)
  └─ ScrCpyRunner            — jeden per urządzenie, zarządza procesem scrcpy
       ├─ BuildScrcpyArguments (ScrCpy.cs) — buduje argumenty CLI
       ├─ SaveWindowPosition  — zapis pozycji okna przy zamknięciu
       └─ KeyboardInterceptorWinForms — globalny hook klawiszy (F9/F10/F11/F5)

Repositories/DeviceRepository — persystencja urządzeń (JSON)
Helpers/AppSettings           — konfiguracja aplikacji (JSON, z cache'owaniem)
Helpers/WindowInfoUtils       — ValidateWindowBounds, GetWindowInfo, SetWindowInfo
Entites/Device                — model urządzenia (DeviceWindow, DeviceMedia)
```

## Przepływ: uruchamianie scrcpy

1. `ScrCpyRunner` odpytuje co 1s czy `Device.IsRunning`
2. `BuildScrcpyArguments` — składa argumenty: serial, title, pozycja okna (z `Device.DeviceWindow`),
   opcje trybu (RATIO FREE → `--no-auto-resize --auto-rotate-on-resize`)
3. Scrcpy startuje z `--window-x/y/width/height` — okno pojawia się od razu w dobrej pozycji
4. `WaitForProcessExit` (500ms poll) → `NotifyDeviceStatus` co 30s
5. Przy zamknięciu: `SaveWindowPosition` zapisuje pozycję do `Device.DeviceWindow`

## Patche scrcpy

Źródła: `D:\work\sources\scrcpy-src` (v4.0, base commit `2322868`)
Dokumentacja patchów: `D:\work\sources\scrcpy-src\scrcpy-patches.md`

Aktywne patche:
- **`--no-auto-resize`** — blokuje automatyczne zmienianie rozmiaru okna przy rotacji urządzenia
- **`--auto-rotate-on-resize`** — gdy użytkownik zmienia rozmiar okna, po 1s debounce obraca telefon przez ADB

Przy upgrade scrcpy: `git pull` w scrcpy-src, nałożyć patche z `scrcpy-patches.md`, rebuild.

## Konwencje

- Ostrzeżenia nullable są preistniejące — nie dodawaj nowych, nie naprawiaj masowo
- Namespaces są niespójne (legacy `Else.PhoneMirror.*` w encjach i kilku helperach) — nie mieszać
  nowych zmian z namespace refactoringiem
- Każda zmiana: `dotnet build` dla weryfikacji przed commitem
- Logi do pliku przez `FileLogger.Log(...)` — nie Console.WriteLine
