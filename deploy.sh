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

# The in-game marketplace needs the native helper even when launched from original Steam.
helper_out="$ROOT/FTKModFramework/bin/Release/net35"
case "$(uname -s)" in
  Darwin) helper_os=darwin ;;
  Linux) helper_os=linux ;;
  *) echo "Unsupported developer host." >&2; exit 1 ;;
esac
case "$(uname -m)" in
  arm64|aarch64) helper_arch=arm64 ;;
  x86_64) helper_arch=amd64 ;;
  *) echo "Unsupported developer architecture." >&2; exit 1 ;;
esac
(cd "$ROOT/launcher/helper" && CGO_ENABLED=0 GOOS="$helper_os" GOARCH="$helper_arch" go build -o "$helper_out/ftkmf-launcher-helper" .)
(cd "$ROOT/launcher/helper" && CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -o "$helper_out/ftkmf-launcher-helper.exe" .)
exec bash "$ROOT/install.sh" \
  --framework "$ROOT/FTKModFramework/bin/Release/net35/FTKModFramework.dll" \
  --dev "$@"
