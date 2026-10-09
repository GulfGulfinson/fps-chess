#!/usr/bin/env bash
# Builds the match server release on the developer machine.
# The release is the Godot project with its compiled C# assemblies and imported resources;
# on the server it runs with the Godot .NET editor binary in headless --server mode.
#   deploy/build-server-release.sh [output-dir]      (default: ./release)
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
out=${1:-"$root/release"}
godot=${GODOT:-godot}

dotnet build "$root/game/Chess.Game.csproj" -nologo -v quiet
# Importing refreshes .godot/imported (fonts); the editor exits non-zero on harmless warnings.
"$godot" --headless --path "$root/game" --import >/dev/null 2>&1 || true

rm -rf "$out"
mkdir -p "$out"
rsync -a --delete \
  --exclude '.godot/editor/' --exclude '.godot/shader_cache/' --exclude 'tools/' \
  --exclude 'obj/' --exclude 'bin/' \
  "$root/game/" "$out/game/"
git -C "$root" rev-parse --short HEAD > "$out/REVISION"
echo "release built in $out ($(du -sh "$out" | cut -f1), revision $(cat "$out/REVISION"))"
