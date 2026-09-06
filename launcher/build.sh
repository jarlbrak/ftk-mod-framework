#!/usr/bin/env bash
# Build release launchers on macOS. Go and dotnet are build dependencies only.
set -euo pipefail
export COPYFILE_DISABLE=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/dist/launchers-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"
DLL="$ROOT/FTKModFramework/bin/Release/net35/FTKModFramework.dll"
[ -f "$DLL" ] || { echo 'Build FTKModFramework Release first.' >&2; exit 1; }
VERSION="$(sed -n 's/.*public const string Version = "\(.*\)";/\1/p' "$ROOT/FTKModFramework/Plugin.cs")"
helper() {
  (cd "$ROOT/launcher/helper" && CGO_ENABLED=0 GOOS="$1" GOARCH="$2" go build -trimpath -ldflags="-s -w -X main.bundledFrameworkVersion=$VERSION" -o "$3" .)
}
bundle_common() {
  mkdir -p "$1/assets/steam"
  cp "$ROOT"/assets/steam/*.png "$1/assets/steam/"
  cp "$DLL" "$1/FTKModFramework.dll"
  cp "$ROOT/launcher/README.md" "$1/README.md"
}
MAC="$OUT/macos/For The King Modded.app"
mkdir -p "$MAC/Contents/MacOS" "$MAC/Contents/Resources"
RES="$MAC/Contents/Resources"
bundle_common "$RES"
cp "$ROOT/install.sh" "$RES/"
cp "$ROOT/launcher/unix/launch.sh" "$ROOT/launcher/unix/setup.command" "$RES/"
cp "$ROOT/assets/steam/icon.icns" "$RES/"
helper darwin arm64 "$OUT/helper-mac-arm64"
helper darwin amd64 "$OUT/helper-mac-amd64"
lipo -create "$OUT/helper-mac-arm64" "$OUT/helper-mac-amd64" -output "$RES/ftkmf-launcher-helper"
codesign --force --sign - "$RES/ftkmf-launcher-helper"
cp "$RES/ftkmf-launcher-helper" "$OUT/ftkmf-helper-macos-universal"
helper windows amd64 "$OUT/ftkmf-helper-windows-amd64.exe"
cat > "$MAC/Contents/MacOS/For The King Modded" <<'SH'
#!/bin/bash
ROOT="$(cd "$(dirname "$0")/../Resources" && pwd)"
exec bash "$ROOT/launch.sh"
SH
chmod +x "$MAC/Contents/MacOS/For The King Modded"
cat > "$MAC/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>For The King Modded</string>
<key>CFBundleIdentifier</key><string>com.ftkmf.launcher</string>
<key>CFBundleName</key><string>For The King Modded</string>
<key>CFBundleDisplayName</key><string>For The King Modded</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$VERSION</string>
<key>CFBundleIconFile</key><string>icon.icns</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>NSHumanReadableCopyright</key><string>FTK Mod Framework community launcher. Requires For The King on Steam.</string>
</dict></plist>
PLIST
python3 "$ROOT/launcher/tools/release-manifest.py" bundle "$RES" "$VERSION" --platform macos-universal
cp "$ROOT/launcher/unix/add-to-steam.sh" "$OUT/macos/Add to Steam.command"
cp "$ROOT/launcher/README.md" "$OUT/macos/README.md"
cp "$ROOT/launcher/unix/restore-bundled.sh" "$OUT/macos/Restore bundled.command"
(cd "$OUT/macos" && zip -qr "$OUT/FTKModdedLauncher-macos-universal.zip" .)
for arch in amd64 arm64; do
  DIR="$OUT/linux-$arch/For The King Modded"
  mkdir -p "$DIR"
  bundle_common "$DIR"
  cp "$ROOT/install.sh" "$DIR/"
  cp "$ROOT/launcher/unix/launch.sh" "$ROOT/launcher/unix/setup.command" "$DIR/"
  cp "$ROOT/launcher/unix/launch.sh" "$DIR/For The King Modded.sh"
  cp "$ROOT/launcher/unix/add-to-steam.sh" "$DIR/Add to Steam.sh"
  cp "$ROOT/launcher/unix/restore-bundled.sh" "$DIR/Restore bundled.sh"
  helper linux "$arch" "$DIR/ftkmf-launcher-helper"
  cp "$DIR/ftkmf-launcher-helper" "$OUT/ftkmf-helper-linux-$arch"
  cp "$OUT/ftkmf-helper-windows-amd64.exe" "$DIR/ftkmf-launcher-helper.exe"
  python3 "$ROOT/launcher/tools/release-manifest.py" bundle "$DIR" "$VERSION" --platform "linux-$arch"
  (cd "$OUT/linux-$arch" && tar -czf "$OUT/FTKModdedLauncher-linux-$arch.tar.gz" 'For The King Modded')
done
WIN="$OUT/windows/For The King Modded"
mkdir -p "$WIN"
bundle_common "$WIN"
dotnet build "$ROOT/launcher/windows/FtkModdedLauncher.csproj" -c Release
cp "$ROOT/launcher/windows/bin/Release/net48/FtkModdedLauncher.exe" "$WIN/"
cp "$ROOT/launcher/windows/bin/Release/net48/FtkModdedLauncher.exe.config" "$WIN/"
cp "$ROOT/launcher/windows/install.ps1" "$WIN/"
helper windows amd64 "$WIN/ftkmf-launcher-helper.exe"
python3 "$ROOT/launcher/tools/release-manifest.py" bundle "$WIN" "$VERSION" --platform windows-amd64
(cd "$OUT/windows" && zip -qr "$OUT/FTKModdedLauncher-windows-x64.zip" .)
printf 'Launcher packages: %s\n' "$OUT"
