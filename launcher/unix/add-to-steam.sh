#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
if [ -d "$ROOT/For The King Modded.app" ]; then
  APP="$ROOT/For The King Modded.app"
  RES="$APP/Contents/Resources"
else
  APP="$ROOT/For The King Modded.sh"
  RES="$ROOT"
fi
printf 'Close Steam completely before adding the shortcut.\n'
"$RES/ftkmf-launcher-helper" register --launcher "$APP" --art "$RES/assets/steam"
printf 'Open Steam to see For The King Modded with its library artwork.\n'
