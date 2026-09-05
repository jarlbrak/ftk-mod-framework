#!/usr/bin/env bash
# Maintainer: publish a GitHub release that install.sh can download.
#
#   ./release.sh v0.2.0                 # build, verify, checksum, publish (asks before publishing)
#   ./release.sh v0.2.0 --notes-file f  # release notes from a file
#   ./release.sh v0.2.0 --dry-run       # everything except the publish step
#   ./release.sh v0.2.0 --prerelease    # preview, excluded from automatic updates
#   ./release.sh v0.2.0 --draft         # upload for review, keep unpublished
#
# The release carries the installer assets plus platform launcher archives:
#   FTKModFramework.dll   the plugin (built locally against the game's DLLs; CI cannot build it)
#   SHA256SUMS            checksums the installer verifies the DLL against
#   install.sh            a copy of the installer, so the release page is self-contained
# Game assemblies are copyrighted and must never be attached or committed; this script checks.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
DLL="$ROOT/FTKModFramework/bin/Release/net35/FTKModFramework.dll"
REPO="jarlbrak/ftk-mod-framework"

TAG="${1:-}"
[ -n "$TAG" ] || { echo "usage: release.sh vX.Y.Z [--notes-file FILE] [--dry-run] [--prerelease] [--draft]" >&2; exit 1; }
shift
NOTES_FILE=""; DRY_RUN=0; PRERELEASE=0; DRAFT=0
while [ $# -gt 0 ]; do
  case "$1" in
    --notes-file) [ $# -ge 2 ] || { echo "--notes-file needs a path" >&2; exit 1; }; NOTES_FILE="$2"; shift 2 ;;
    --dry-run) DRY_RUN=1; shift ;;
    --prerelease) PRERELEASE=1; shift ;;
    --draft) DRAFT=1; shift ;;
    *) echo "unknown option: $1" >&2; exit 1 ;;
  esac
done

[[ "$TAG" =~ ^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]] || { echo "tag must look like v1.2.3 (got $TAG)" >&2; exit 1; }
[ -z "$NOTES_FILE" ] || [ -f "$NOTES_FILE" ] || { echo "release notes file does not exist" >&2; exit 1; }

# 1) The plugin's own version string must match the tag, or the release lies about what it ships.
PLUGIN_VERSION="$(sed -n 's/.*public const string Version = "\(.*\)";/\1/p' "$ROOT/FTKModFramework/Plugin.cs")"
if [ "v$PLUGIN_VERSION" != "$TAG" ]; then
  echo "Plugin.cs says Version = \"$PLUGIN_VERSION\" but the tag is $TAG. Bump Plugin.cs and the .csproj <Version> first." >&2
  exit 1
fi

ASSEMBLY_VERSION="$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$ROOT/FTKModFramework/FTKModFramework.csproj")"
if [ "$ASSEMBLY_VERSION" != "$PLUGIN_VERSION" ]; then
  echo "Plugin.cs and the framework project version differ; align them before releasing." >&2
  exit 1
fi

# 2) Never ship from a dirty tree.
if [ -n "$(git -C "$ROOT" status --porcelain)" ]; then
  echo "working tree is not clean; commit or stash first." >&2
  exit 1
fi

# 3) Guard: no copyrighted game assembly tracked.
SOURCE_COMMIT="$(git -C "$ROOT" rev-parse HEAD)"
# Refuse an existing tag: release assets must correspond to this exact source commit.
if [ -n "$(git -C "$ROOT" ls-remote --tags origin "refs/tags/$TAG" "refs/tags/$TAG^{}")" ]; then
  echo "tag $TAG already exists on origin; choose a new version." >&2
  exit 1
fi

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
cp "$ROOT/launcher/windows/install.ps1" "$DIST/install.ps1"
bash "$ROOT/launcher/build.sh" "$DIST/launchers"
cp "$DIST/launchers/"ftkmf-helper-* "$DIST/"
cp "$DIST/launchers/"*.zip "$DIST/launchers/"*.tar.gz "$DIST/"
python3 "$ROOT/launcher/tools/release-manifest.py" release "$DIST" "$PLUGIN_VERSION" --policy "$ROOT/launcher/update-policy.json"
(cd "$DIST" && if command -v sha256sum >/dev/null 2>&1; then sha256sum update.json FTKModFramework.dll install.sh install.ps1 ftkmf-helper-* ./*.zip ./*.tar.gz; else shasum -a 256 update.json FTKModFramework.dll install.sh install.ps1 ftkmf-helper-* ./*.zip ./*.tar.gz; fi > SHA256SUMS)
echo "assets:"; (cd "$DIST" && ls -la && cat SHA256SUMS)

# 6) The installer must accept the DLL we are about to publish.
bash "$ROOT/install.sh" --help >/dev/null
case "$(head -c 2 "$DIST/FTKModFramework.dll")" in MZ) ;; *) echo "DLL is not a PE image" >&2; exit 1 ;; esac

if [ "$DRY_RUN" = "1" ]; then echo "dry run: not publishing $TAG"; exit 0; fi

if [ "$(gh api user --jq .login)" != "jarlbrak" ]; then
  echo "publishing requires the jarlbrak GitHub account." >&2
  exit 1
fi
if ! gh api "repos/$REPO/commits/$SOURCE_COMMIT" --silent; then
  echo "source commit $SOURCE_COMMIT is not on GitHub; push the reviewed branch first." >&2
  exit 1
fi

# Upload as a draft so automatic updaters never observe half a release.
# With immutable releases enabled on GitHub, all assets must exist before publishing.
printf 'Upload release %s to github.com/%s (prerelease=%s, keep draft=%s)? [y/N] ' "$TAG" "$REPO" "$PRERELEASE" "$DRAFT"
read -r answer
case "$answer" in y|Y|yes) ;; *) echo "not uploaded."; exit 0 ;; esac

RELEASE_ARGS=(--repo "$REPO" --target "$SOURCE_COMMIT" --title "$TAG" --draft)
[ "$PRERELEASE" = 0 ] || RELEASE_ARGS+=(--prerelease)
if [ -n "$NOTES_FILE" ]; then RELEASE_ARGS+=(--notes-file "$NOTES_FILE"); else RELEASE_ARGS+=(--generate-notes); fi
gh release create "$TAG" "${RELEASE_ARGS[@]}" \
  "$DIST/FTKModFramework.dll" "$DIST/SHA256SUMS" "$DIST/update.json" "$DIST/install.sh" "$DIST/install.ps1" \
  "$DIST/"ftkmf-helper-* "$DIST/"*.zip "$DIST/"*.tar.gz

# Check every uploaded name and byte count before exposing this release to players.
gh release view "$TAG" --repo "$REPO" --json assets > "$DIST/uploaded-assets.json"
python3 - "$DIST" <<'PYTHON'
import json
import sys
from pathlib import Path
root = Path(sys.argv[1])
assets = json.loads((root / "uploaded-assets.json").read_text())["assets"]
actual = {asset["name"]: asset["size"] for asset in assets}
expected = {line.split(maxsplit=1)[1].strip().removeprefix("./") for line in (root / "SHA256SUMS").read_text().splitlines()}
expected.add("SHA256SUMS")
if set(actual) != expected or any(actual[name] != (root / name).stat().st_size for name in expected):
    raise SystemExit("Upload verification failed; release remains a draft. Inspect the assets before publishing.")
PYTHON
if [ "$DRAFT" = 1 ]; then
  echo "Verified draft ready for review: https://github.com/$REPO/releases"
  exit 0
fi
LATEST=true
[ "$PRERELEASE" = 0 ] || LATEST=false
gh release edit "$TAG" --repo "$REPO" --draft=false --latest="$LATEST"
echo "published: https://github.com/$REPO/releases/tag/$TAG"
echo "Players: download the platform launcher archive, extract permanently, and add its launcher to Steam."
