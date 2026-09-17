#!/usr/bin/env python3
"""
Bumps patch version, commits version.txt, pushes tag -> GitHub Actions builds the
Velopack package and publishes a GitHub Release (Setup.exe + update feed).
"""
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).parent

# Windows console defaults to a legacy codepage; never let printing stop a release.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass


def run(cmd: list[str], check: bool = True) -> subprocess.CompletedProcess:
    print(f"$ {' '.join(cmd)}")
    return subprocess.run(cmd, check=check, cwd=ROOT)


def main() -> None:
    version_file = ROOT / "version.txt"
    version = version_file.read_text().strip()
    major, minor, patch = version.split(".")
    new_version = f"{major}.{minor}.{int(patch) + 1}"
    tag = f"v{new_version}"

    print(f"Version: {version} -> {new_version}")

    version_file.write_text(new_version + "\n")

    run(["git", "add", "version.txt"])
    run(["git", "commit", "-m", f"chore: version {new_version}"])
    run(["git", "push"])

    run(["git", "tag", tag])
    run(["git", "push", "origin", tag])

    print(f"\nTag {tag} pushed - GitHub Actions will build and publish the release.")


if __name__ == "__main__":
    main()
