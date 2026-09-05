#!/usr/bin/env bash
# Developer deploy: build the framework, then install THIS build into For The King through install.sh
# (BepInEx + plugin + Steam launch option), with the framework's load-time self-tests switched on.
#
#   ./deploy.sh                       # build + install into the Steam copy it finds
#   ./deploy.sh --game-dir /path      # any install.sh option passes through
#   FTK_DIR="/path/to/For The King" ./deploy.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"

dotnet build "$ROOT/FTKModFramework" -c Release

exec bash "$ROOT/install.sh" \
  --framework "$ROOT/FTKModFramework/bin/Release/net35/FTKModFramework.dll" \
  --dev "$@"
