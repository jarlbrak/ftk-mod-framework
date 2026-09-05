#!/usr/bin/env bash
# Exercise the installed fast path with enough status output to expose SIGPIPE regressions.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"
mkdir -p "$WORK/Test Launcher.app/Contents/Resources" "$WORK/bin"
RES="$WORK/Test Launcher.app/Contents/Resources"
cp "$ROOT/launch.sh" "$RES/launch.sh"
cat > "$RES/install.sh" <<'SH'
#!/bin/bash
printf 'FTK Mod Framework: installed\n'
for ((i=0;i<20000;i++)); do printf 'status diagnostics after installed marker\n'; done
SH
printf '#!/bin/sh\nexit 0\n' > "$RES/ftkmf-launcher-helper"
printf '#!/bin/sh\necho Darwin\n' > "$WORK/bin/uname"
cat > "$WORK/bin/open" <<'SH'
#!/bin/sh
[ "$#" = 1 ] && [ "$1" = 'steam://rungameid/527230' ] || exit 99
SH
chmod +x "$RES/ftkmf-launcher-helper" "$WORK/bin/"*
PATH="$WORK/bin:$PATH" bash "$RES/launch.sh"
printf 'PASS: installed launcher opens Steam without restarting setup\n'
# Keep generated diagnostics recoverable on macOS; CI uses an ephemeral runner.
if [ -d "${HOME}/.Trash" ]; then mv "$WORK" "${HOME}/.Trash/ftkmf-launch-test-$(basename "$WORK")"; fi
