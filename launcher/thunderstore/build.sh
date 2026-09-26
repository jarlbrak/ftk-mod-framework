#!/usr/bin/env bash
# Build the independently versioned Thunderstore setup artifacts.
# Usage: build.sh OUTPUT_DIRECTORY [GAME_MANAGED_DIRECTORY]
# Publish these artifacts under bootstrap-v<bootstrap-version.txt>.
set -euo pipefail

BOOTSTRAP_ROOT="$(cd "$(dirname "$0")" && pwd)"
if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo 'Usage: build.sh OUTPUT_DIRECTORY [GAME_MANAGED_DIRECTORY]' >&2
  exit 1
fi
BOOTSTRAP_VERSION="$(tr -d '\r\n' < "$BOOTSTRAP_ROOT/bootstrap-version.txt")"
if [[ ! "$BOOTSTRAP_VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo 'bootstrap-version.txt must contain a stable semantic version.' >&2
  exit 1
fi
mkdir -p "$1"
BOOTSTRAP_OUT="$(cd "$1" && pwd)"
BOOTSTRAP_BUILD_ARGS=(build "$BOOTSTRAP_ROOT/FTKThunderstoreBootstrap.csproj" -c Release)
if [[ $# -eq 2 ]]; then
  BOOTSTRAP_BUILD_ARGS+=("-p:FtkManagedDir=$2")
fi
dotnet "${BOOTSTRAP_BUILD_ARGS[@]}"
(
  cd "$BOOTSTRAP_ROOT/../helper"
  CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -trimpath -ldflags='-s -w -H windowsgui' \
    -o "$BOOTSTRAP_OUT/ftkmf-bootstrap-helper.exe" .
)
cp "$BOOTSTRAP_ROOT/bin/Release/net35/FTKThunderstoreBootstrap.dll" "$BOOTSTRAP_OUT/"
(
  cd "$BOOTSTRAP_OUT"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum FTKThunderstoreBootstrap.dll ftkmf-bootstrap-helper.exe > SHA256SUMS
  else
    shasum -a 256 FTKThunderstoreBootstrap.dll ftkmf-bootstrap-helper.exe > SHA256SUMS
  fi
)
printf 'Bootstrap %s artifacts: %s\n' "$BOOTSTRAP_VERSION" "$BOOTSTRAP_OUT"
