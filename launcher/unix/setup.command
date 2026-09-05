#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
bash "$ROOT/install.sh" --framework "$ROOT/FTKModFramework.dll" "$@"
exec bash "$ROOT/launch.sh"
