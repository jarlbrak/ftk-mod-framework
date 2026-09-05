#!/usr/bin/env bash
# No network or real Steam: exercise the entrypoint with bundled-tool fixtures.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"
mkdir -p "$WORK/Test Launcher.app/Contents/Resources" "$WORK/bin" "$WORK/Game with spaces"
RES="$WORK/Test Launcher.app/Contents/Resources"
cp "$ROOT/launch.sh" "$RES/launch.sh"
export FTK_TEST_ROOT="$RES" FTK_TEST_GAME="$WORK/Game with spaces" FTK_TEST_TRACE="$WORK/trace"
cat > "$RES/install.sh" <<'STUB'
#!/bin/bash
set -eu
if [ "$1" = --launcher-status ]; then
  printf '%s\nmac\n%s\n' "$FTK_TEST_GAME" "$FTK_TEST_BASELINE"
  exit 0
fi
printf 'Unexpected direct installer call\n' >&2
exit 98
STUB
cat > "$RES/ftkmf-launcher-helper" <<'STUB'
#!/bin/bash
set -eu
[ "$1" != artwork ] || exit 0
[ "$#" = 7 ] && [ "$1" = prepare-launch ] && [ "$2" = --game-dir ] &&
[ "$3" = "$FTK_TEST_GAME" ] && [ "$4" = --bundle-dir ] && [ "$5" = "$FTK_TEST_ROOT" ] && [ "$6" = --install-if-missing ] && [ "$7" = --launch ] || exit 99
if [ "$FTK_TEST_BASELINE" = missing ]; then
  printf 'install\n' >> "$FTK_TEST_TRACE"
  [ "$FTK_TEST_FAILURE" != install ] || exit 1
fi
printf 'prepare\n' >> "$FTK_TEST_TRACE"
[ "$FTK_TEST_FAILURE" != update ] || { printf 'Unsafe baseline; launch blocked.\n' >&2; exit 1; }
printf 'launch\n' >> "$FTK_TEST_TRACE"
STUB
printf '#!/bin/sh\necho Darwin\n' > "$WORK/bin/uname"
printf '#!/bin/sh\nexit 0\n' > "$WORK/bin/osascript"
printf 'bundled-fixture\n' > "$RES/FTKModFramework.dll"
chmod +x "$RES/ftkmf-launcher-helper" "$WORK/bin/"*
export PATH="$WORK/bin:$PATH"
export FTK_TEST_BASELINE=installed FTK_TEST_FAILURE=none
: > "$FTK_TEST_TRACE"
bash "$RES/launch.sh"
[ "$(cat "$FTK_TEST_TRACE")" = $'prepare\nlaunch' ]
printf 'PASS: existing install checks updates and dispatches without reinstalling\n'
export FTK_TEST_BASELINE=missing
: > "$FTK_TEST_TRACE"
bash "$RES/launch.sh"
[ "$(cat "$FTK_TEST_TRACE")" = $'install\nprepare\nlaunch' ]
printf 'PASS: first Play installs the explicit bundled pair then checks updates and launches\n'
export FTK_TEST_FAILURE=install
: > "$FTK_TEST_TRACE"
if bash "$RES/launch.sh" > "$WORK/install-error" 2>&1; then exit 1; fi
[ "$(cat "$FTK_TEST_TRACE")" = install ]
printf 'PASS: failed first install prevents update and game dispatch\n'
export FTK_TEST_BASELINE=installed FTK_TEST_FAILURE=update
: > "$FTK_TEST_TRACE"
if bash "$RES/launch.sh" > "$WORK/update-error" 2>&1; then exit 1; fi
[ "$(cat "$FTK_TEST_TRACE")" = prepare ]
printf 'PASS: unsafe updater result never falls through to Steam\n'
mkdir -p "$FTK_TEST_GAME/FTK.app"
REAL_STATUS="$(bash "$ROOT/../../install.sh" --launcher-status --game-dir "$FTK_TEST_GAME")"
[ "$REAL_STATUS" = "$(cd "$FTK_TEST_GAME" && pwd -P)"$'\nmac\nmissing' ]
[ ! -e "$FTK_TEST_GAME/BepInEx" ]
printf 'PASS: real installer launcher status reads game path without modifying installation\n'
printf 'Fixtures retained: %s\n' "$WORK"
