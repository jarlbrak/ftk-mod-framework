#!/usr/bin/env bash
# Maintainer: publish a GitHub release that install.sh can download.
#
#   ./release.sh v0.2.0                 # build, verify, checksum, publish (asks before publishing)
#   ./release.sh v0.2.0 --notes-file f  # release notes from a file
#   ./release.sh v0.2.0 --dry-run       # everything except the publish step
#
# The release carries three assets the installer relies on:
#   FTKModFramework.dll   the plugin (built locally against the game's DLLs; CI cannot build it)
#   SHA256SUMS            checksums the installer verifies the DLL against
#   install.sh            a copy of the installer, so the release page is self-contained
# Game assemblies are copyrighted and must never be attached or committed; this script checks.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
DLL="$ROOT/FTKModFramework/bin/Release/net35/FTKModFramework.dll"
REPO="jarlbrak/ftk-mod-framework"

TAG="${1:-}"
[ -n "$TAG" ] || { echo "usage: release.sh vX.Y.Z [--notes-file FILE] [--dry-run]" >&2; exit 1; }
shift
NOTES_FILE=""; DRY_RUN=0
while [ $# -gt 0 ]; do
  case "$1" in
    --notes-file) NOTES_FILE="$2"; shift 2 ;;
    --dry-run) DRY_RUN=1; shift ;;
    *) echo "unknown option: $1" >&2; exit 1 ;;
  esac
done

case "$TAG" in v[0-9]*.[0-9]*.[0-9]*) ;; *) echo "tag must look like v1.2.3 (got $TAG)" >&2; exit 1 ;; esac

# 1) The plugin's own version string must match the tag, or the release lies about what it ships.
PLUGIN_VERSION="$(sed -n 's/.*public const string Version = "\(.*\)";/\1/p' "$ROOT/FTKModFramework/Plugin.cs")"
if [ "v$PLUGIN_VERSION" != "$TAG" ]; then
  echo "Plugin.cs says Version = \"$PLUGIN_VERSION\" but the tag is $TAG. Bump Plugin.cs and the .csproj <Version> first." >&2
  exit 1
fi

# 2) Never ship from a dirty tree.
if [ -n "$(git -C "$ROOT" status --porcelain)" ]; then
  echo "working tree is not clean; commit or stash first." >&2
  exit 1
fi

# 3) Guard: no copyrighted game assembly tracked.
if git -C "$ROOT" ls-files | grep -Eq '(Assembly-CSharp[^/]*|UnityEngine[^/]*|Newtonsoft\.Json)\.dll$'; then
  echo "ABORT: a copyrighted game assembly is tracked in git." >&2
  exit 1
fi

# 4) Clean Release build.
dotnet build "$ROOT/FTKModFramework" -c Release
[ -f "$DLL" ] || { echo "build produced no $DLL" >&2; exit 1; }

# 5) Assets + checksums in a scratch dir (never committed).
DIST="$(mktemp -d "${TMPDIR:-/tmp}/ftkmf-release.XXXXXX")"
cp "$DLL" "$DIST/FTKModFramework.dll"
cp "$ROOT/install.sh" "$DIST/install.sh"
(cd "$DIST" && if command -v sha256sum >/dev/null 2>&1; then sha256sum FTKModFramework.dll install.sh; else shasum -a 256 FTKModFramework.dll install.sh; fi > SHA256SUMS)
echo "assets:"; (cd "$DIST" && ls -la && cat SHA256SUMS)

# 6) The installer must accept the DLL we are about to publish.
bash "$ROOT/install.sh" --help >/dev/null
case "$(head -c 2 "$DIST/FTKModFramework.dll")" in MZ) ;; *) echo "DLL is not a PE image" >&2; exit 1 ;; esac

if [ "$DRY_RUN" = "1" ]; then echo "dry run: not publishing $TAG"; exit 0; fi

printf 'Publish release %s to github.com/%s with these assets? [y/N] ' "$TAG" "$REPO"
read -r answer
case "$answer" in y|Y|yes) ;; *) echo "not published."; exit 0 ;; esac

if [ -n "$NOTES_FILE" ]; then
  gh release create "$TAG" --repo "$REPO" --title "$TAG" --notes-file "$NOTES_FILE" \
    "$DIST/FTKModFramework.dll" "$DIST/SHA256SUMS" "$DIST/install.sh"
else
  gh release create "$TAG" --repo "$REPO" --title "$TAG" --generate-notes \
    "$DIST/FTKModFramework.dll" "$DIST/SHA256SUMS" "$DIST/install.sh"
fi
echo "published: https://github.com/$REPO/releases/tag/$TAG"
echo "players can now run:  curl -fsSL https://raw.githubusercontent.com/$REPO/master/install.sh | bash"
