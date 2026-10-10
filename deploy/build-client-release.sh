#!/usr/bin/env bash
# Exports the playable game for Windows and Linux and packs one zip per platform into export/.
# Needs the Godot 4.7.2 .NET export templates (Editor > Manage Export Templates, or the .tpz from GitHub).
#   deploy/build-client-release.sh [version]      (default: short git revision)
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
godot=${GODOT:-godot}
version=${1:-$(git -C "$root" rev-parse --short HEAD)}
out="$root/export"

rm -rf "$out/windows" "$out/linux"
mkdir -p "$out/windows" "$out/linux"
"$godot" --headless --path "$root/game" --export-release "Windows" "$out/windows/FPSChess.exe"
"$godot" --headless --path "$root/game" --export-release "Linux" "$out/linux/FPSChess.x86_64"
test -f "$out/windows/FPSChess.exe" && test -x "$out/linux/FPSChess.x86_64"

(cd "$out/windows" && rm -f "../FPSChess-$version-windows.zip" && zip -q -9 "../FPSChess-$version-windows.zip" FPSChess.exe)
(cd "$out/linux" && rm -f "../FPSChess-$version-linux.zip" && zip -q -9 "../FPSChess-$version-linux.zip" FPSChess.x86_64)
ls -la "$out"/*.zip
