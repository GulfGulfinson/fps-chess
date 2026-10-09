#!/usr/bin/env bash
# Builds the match server and deploys it to gulfinson through the house deploy script
# (new release directory, atomic "current" switch, unit restart, prune to five releases).
#   deploy/deploy.sh
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
host=chess.gulfgulfinson.de
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT

"$root/deploy/build-server-release.sh" "$stage/release"
remote=$(ssh lab mktemp -d)
rsync -a "$stage/release/" "lab:$remote/release/"
ssh lab "~/Gulfslab/scripts/deploy-release.sh $host $remote/release webapp-chess.service && rm -rf $remote"
ssh lab "sleep 4; systemctl is-active webapp-chess.service; tail -n 3 ~/websites/$host/shared/logs/webapp-chess.log"
