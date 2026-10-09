#!/usr/bin/env bash
# ~/Gulfslab/legal/adapters/chess/build.sh
#
# Renders dist/chess/{impressum,datenschutz}.html for chess.gulfgulfinson.de. Reuses the gitea
# adapter: its renderer resolves inputs next to itself and writes to dist/git, so this script
# generates a copy here with the output folder and host name swapped. Idempotent.
set -euo pipefail

cd "$(dirname "$0")"
gitea=../gitea

if [[ ! -d $gitea/node_modules/markdown-it ]]; then
  (cd "$gitea" && npm install --no-audit --no-fund --silent)
fi
ln -sfn "$gitea/node_modules" node_modules
sed "s#join(ROOT, 'dist', 'git')#join(ROOT, 'dist', 'chess')#" "$gitea/render.mjs" > render.generated.mjs
sed "s/git\.gulfgulfinson\.de/chess.gulfgulfinson.de/g" "$gitea/template.html" > template.html

node render.generated.mjs --inputs ../../content/impressum.md --title "Impressum" --out impressum.html
node render.generated.mjs --inputs ../../content/datenschutz.base.md datenschutz.chess.md \
  --title "Datenschutzerklärung" --out datenschutz.html

chmod 644 ../../dist/chess/*.html
ls -la ../../dist/chess/
