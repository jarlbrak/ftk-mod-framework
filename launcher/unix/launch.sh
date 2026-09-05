#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
PLATFORM="$(uname -s)"
if [ "$PLATFORM" = Darwin ]; then
  APP="$(cd "$ROOT/../.." && pwd)"
else
  APP="$ROOT/For The King Modded.sh"
fi
show_error() {
  printf '%s\n' "$1" >&2
  if [ "$PLATFORM" = Darwin ] && command -v osascript >/dev/null 2>&1; then
    osascript -e 'on run argv' -e 'display alert "For The King Modded" message (item 1 of argv) as critical' -e 'end run' "$1" >/dev/null 2>&1 || true
  elif command -v zenity >/dev/null 2>&1; then
    zenity --error --title='For The King Modded' --text="$1" >/dev/null 2>&1 || true
  fi
}
HELPER="$ROOT/ftkmf-launcher-helper"
[ -x "$HELPER" ] || { show_error 'The bundled launcher helper is missing. Download the complete launcher again.'; exit 1; }
# Artwork is safe while Steam runs and does not edit shortcuts.
"$HELPER" artwork --launcher "$APP" --art "$ROOT/assets/steam" || true
if ! STATUS="$(bash "$ROOT/install.sh" --launcher-status)"; then
  show_error 'For The King could not be located. Install it in Steam first, or set FTK_DIR to its game folder.'
  exit 1
fi
{ IFS= read -r GAME_DIR; IFS= read -r BUILD; IFS= read -r FRAMEWORK_STATE; } <<< "$STATUS"
case "$BUILD:$FRAMEWORK_STATE" in mac:installed|mac:missing|linux:installed|linux:missing|proton:installed|proton:missing) ;;
  *) show_error 'The bundled installer returned an invalid game status.'; exit 1 ;;
esac
# The helper owns update/recovery and original Steam app dispatch under one lock.
# Never independently launch Steam after a timeout or unsafe update result.
if ! RESULT="$("$HELPER" prepare-launch --game-dir "$GAME_DIR" --bundle-dir "$ROOT" --install-if-missing --launch 2>&1)"; then
  show_error "${RESULT:-The mod update check failed. The game was not launched.}"
  exit 1
fi
printf '%s\n' "$RESULT"
