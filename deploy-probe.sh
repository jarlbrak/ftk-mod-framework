#!/usr/bin/env bash
# Build FTKPerfProbe and copy it into FTK's BepInEx/plugins folder.
# Override the game path with: FTK_DIR="/path/to/For The King" ./deploy-probe.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME="${FTK_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/For The King}"
PLUGINS="$GAME/BepInEx/plugins"

dotnet build "$ROOT/FTKPerfProbe/FTKPerfProbe.csproj" -c Release

if [ ! -d "$GAME/BepInEx" ]; then
  echo "!! BepInEx not found at: $GAME/BepInEx"
  echo "   Install it first (see README), then re-run."
  exit 1
fi

mkdir -p "$PLUGINS"
cp "$ROOT/FTKPerfProbe/bin/Release/net35/FTKPerfProbe.dll" "$PLUGINS/"
echo "Deployed FTKPerfProbe.dll -> $PLUGINS"
echo "Launch FTK, reach the overworld, then: F9 overlay / F10 capture / F11 census."
echo "Output + log: $GAME/BepInEx/FTKPerfProbe/  and  $GAME/BepInEx/LogOutput.log"
