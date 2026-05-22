"""
Aktualizuje scrcpy (+ bundlowany adb) do najnowszej wersji z GitHub.
Scrcpy dla Windows zawiera już adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll.

Użycie:
    python update_tools.py
"""

import io
import json
import os
import shutil
import sys
import tempfile
import urllib.request
import zipfile

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
TARGET_DIR = os.path.join(SCRIPT_DIR, "Dexa", "scrcpy")
GITHUB_API = "https://api.github.com/repos/Genymobile/scrcpy/releases/latest"


def fetch_json(url: str) -> dict:
    req = urllib.request.Request(url, headers={"User-Agent": "update_tools/1.0"})
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read())


def download(url: str, label: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "update_tools/1.0"})
    with urllib.request.urlopen(req, timeout=120) as resp:
        total = int(resp.headers.get("Content-Length", 0))
        data = bytearray()
        chunk = 65536
        while True:
            block = resp.read(chunk)
            if not block:
                break
            data.extend(block)
            if total:
                pct = len(data) * 100 // total
                print(f"\r  {label}: {pct:3d}% ({len(data)//1024} KB)", end="", flush=True)
        print()
    return bytes(data)


def current_version(exe: str) -> str:
    import subprocess
    try:
        r = subprocess.run([exe, "--version"], capture_output=True, text=True, timeout=5)
        first = (r.stdout or r.stderr).splitlines()[0]
        return first.strip()
    except Exception:
        return "(nieznana)"


def main():
    print("=== Pobieranie informacji o najnowszej wersji scrcpy ===")
    release = fetch_json(GITHUB_API)
    tag = release["tag_name"]          # np. "v3.1"
    print(f"Najnowsza wersja: {tag}")

    # Szukamy asseta win64
    asset = next(
        (a for a in release["assets"] if "win64" in a["name"] and a["name"].endswith(".zip")),
        None,
    )
    if asset is None:
        sys.exit("Nie znaleziono paczki win64 w release!")

    asset_name = asset["name"]
    asset_url = asset["browser_download_url"]
    print(f"Asset: {asset_name}")

    # Aktualna wersja (jeśli istnieje)
    scrcpy_exe = os.path.join(TARGET_DIR, "scrcpy.exe")
    adb_exe = os.path.join(TARGET_DIR, "adb.exe")
    if os.path.exists(scrcpy_exe):
        print(f"Bieżąca wersja scrcpy : {current_version(scrcpy_exe)}")
    if os.path.exists(adb_exe):
        print(f"Bieżąca wersja adb    : {current_version(adb_exe)}")

    print(f"\nPobieranie {asset_name} ...")
    data = download(asset_url, asset_name)

    print("Rozpakowywanie ...")
    os.makedirs(TARGET_DIR, exist_ok=True)

    with tempfile.TemporaryDirectory() as tmp:
        with zipfile.ZipFile(io.BytesIO(data)) as zf:
            zf.extractall(tmp)

        # Paczka scrcpy ma jeden podkatalog (np. scrcpy-win64-v3.1/)
        subdirs = [d for d in os.listdir(tmp) if os.path.isdir(os.path.join(tmp, d))]
        src_dir = os.path.join(tmp, subdirs[0]) if subdirs else tmp

        copied, skipped = [], []
        for fname in os.listdir(src_dir):
            src = os.path.join(src_dir, fname)
            dst = os.path.join(TARGET_DIR, fname)
            if os.path.isfile(src):
                shutil.copy2(src, dst)
                copied.append(fname)
            else:
                skipped.append(fname)

    print(f"\nSkopiowano {len(copied)} plik(ów) do {TARGET_DIR}")

    # Nowe wersje
    if os.path.exists(scrcpy_exe):
        print(f"Nowa wersja scrcpy : {current_version(scrcpy_exe)}")
    if os.path.exists(adb_exe):
        print(f"Nowa wersja adb    : {current_version(adb_exe)}")

    print("\nGotowe!")


if __name__ == "__main__":
    main()
