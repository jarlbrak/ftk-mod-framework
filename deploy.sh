#!/usr/bin/env bash
# Build the framework and copy it into FTK's BepInEx/plugins folder.
# Requires BepInEx to already be installed in the game (see README).
# Override the game path with: FTK_DIR="/path/to/For The King" ./deploy.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME="${FTK_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/For The King}"
PLUGINS="$GAME/BepInEx/plugins"

dotnet build "$ROOT/FTKModFramework" -c Release

if [ ! -d "$GAME/BepInEx" ]; then
  echo "!! BepInEx not found at: $GAME/BepInEx"
  echo "   Install it first (see README), then re-run."
  exit 1
fi

mkdir -p "$PLUGINS"
cp "$ROOT/FTKModFramework/bin/Release/net35/FTKModFramework.dll" "$PLUGINS/"
echo "Deployed FTKModFramework.dll -> $PLUGINS"
echo "Launch FTK from Steam, then check: $GAME/BepInEx/LogOutput.log"
