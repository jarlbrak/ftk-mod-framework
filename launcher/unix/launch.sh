#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
if [ "$(uname -s)" = Darwin ]; then
  APP="$(cd "$ROOT/../.." && pwd)"
else
  APP="$ROOT/For The King Modded.sh"
fi
# Artwork is safe to add while Steam runs. This never edits shortcuts or stops Steam.
"$ROOT/ftkmf-launcher-helper" artwork --launcher "$APP" --art "$ROOT/assets/steam" || true
STATUS="$(bash "$ROOT/install.sh" --status 2>/dev/null || true)"
if [[ "$STATUS" != *"FTK Mod Framework: installed"* ]]; then
  if [ "$(uname -s)" = Darwin ]; then
    exec open -a Terminal "$ROOT/setup.command"
  elif command -v konsole >/dev/null 2>&1; then
    exec konsole -e bash "$ROOT/setup.command"
  elif command -v x-terminal-emulator >/dev/null 2>&1; then
    exec x-terminal-emulator -e bash "$ROOT/setup.command"
  elif command -v gnome-terminal >/dev/null 2>&1; then
    exec gnome-terminal -- bash "$ROOT/setup.command"
  else
    printf 'Run this setup once in a terminal: bash "%s/setup.command"\n' "$ROOT" >&2
    exit 1
  fi
fi
if [ "$(uname -s)" = Darwin ]; then exec open 'steam://rungameid/527230'; fi
if command -v steam >/dev/null 2>&1; then exec steam 'steam://rungameid/527230'; fi
if command -v flatpak >/dev/null 2>&1 && flatpak info com.valvesoftware.Steam >/dev/null 2>&1; then
  exec flatpak run com.valvesoftware.Steam 'steam://rungameid/527230'
fi
exec xdg-open 'steam://rungameid/527230'
