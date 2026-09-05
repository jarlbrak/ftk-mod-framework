#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
if [ "$#" -ne 0 ]; then
  printf 'For a custom game folder, set FTK_DIR before running setup.command. Advanced options are available through install.sh.\n' >&2
  exit 1
fi
STATUS="$(bash "$ROOT/install.sh" --launcher-status)"
IFS= read -r GAME_DIR <<< "$STATUS"
# Explicit repair uses the same lock and refreshes the verified installation receipt.
"$ROOT/ftkmf-launcher-helper" prepare-launch --game-dir "$GAME_DIR" --bundle-dir "$ROOT" --repair-only
exec bash "$ROOT/launch.sh"
