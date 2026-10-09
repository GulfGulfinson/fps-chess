#!/usr/bin/env bash
# ~/Gulfslab/legal/adapters/chess/build.sh
#
# Renders dist/chess/{impressum,datenschutz}.html for chess.gulfgulfinson.de, reusing the gitea
# adapter's renderer and template with the host name swapped. Idempotent.
set -euo pipefail

cd "$(dirname "$0")"
gitea=../gitea
dist=../../dist/chess
mkdir -p "$dist"

if [[ ! -d $gitea/node_modules/markdown-it ]]; then
  (cd "$gitea" && npm install --no-audit --no-fund --silent)
fi

# The gitea renderer writes next to its own template into dist/git; give it a chess copy of both.
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
sed "s/git\.gulfgulfinson\.de/chess.gulfgulfinson.de/g" "$gitea/template.html" > "$work/template.html"
sed "s#join(ROOT, 'dist', 'git')#join(ROOT, 'dist', 'chess')#; s#const ROOT = .*#const ROOT = '$(cd ../.. && pwd)';#" \
  "$gitea/render.mjs" > "$work/render.mjs"
ln -s "$(cd "$gitea" && pwd)/node_modules" "$work/node_modules"

node "$work/render.mjs" --inputs ../../content/impressum.md --title "Impressum" --out impressum.html
node "$work/render.mjs" --inputs ../../content/datenschutz.base.md datenschutz.chess.md \
  --title "Datenschutzerklärung" --out datenschutz.html

chmod 644 "$dist"/*.html
ls -la "$dist"
