#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
if [ -d "$ROOT/For The King Modded.app" ]; then
  ROOT="$ROOT/For The King Modded.app/Contents/Resources"
fi
printf 'Restoring the bundled framework and returning update choice to Stable.\n'
printf 'Close For The King before continuing. Your installed mods are preserved.\n'
exec bash "$ROOT/setup.command"
