#!/usr/bin/env bash
# End-to-end tests for install.sh against MOCK Steam layouts (no game, no Steam client needed).
#
# Each scenario builds a throwaway $HOME with a fake Steam library, a fake game folder (ELF or PE
# FTK.exe on Linux, FTK.app on macOS), and fake localconfig.vdf files, then runs the installer with a
# fake framework DLL and asserts on the files it wrote. BepInEx archives are real (downloaded once into
# a cache and served to the installer through a file:// URL so the checksum path is exercised too).
#
#   bash tests/installer/test-install.sh            # run every scenario for this OS
#   FTKMF_TEST_CACHE=/path bash tests/installer/test-install.sh   # reuse downloaded archives
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
INSTALLER="$REPO/install.sh"
OS="$(uname -s)"
CACHE="${FTKMF_TEST_CACHE:-${TMPDIR:-/tmp}/ftkmf-test-cache}"
# Resolve the work dir the same way the installer resolves paths (cd -P), so expected launch options
# match byte for byte even when TMPDIR is a symlink (/var -> /private/var on macOS).
WORK="$(mktemp -d "${TMPDIR:-/tmp}/ftkmf-test.XXXXXX")"
WORK="$(cd -P "$WORK" && pwd -P)"
mkdir -p "$WORK/bin"
# Mock Steam folders do not isolate the real Steam process or application launcher.
printf '#!/bin/sh\nexit 1\n' > "$WORK/bin/pgrep"
for tool in osascript open steam flatpak; do
  # shellcheck disable=SC2016
  printf '#!/bin/sh\necho "Unexpected host application command: $0" >&2\ntouch "%s/host-command-called"\nexit 99\n' "$WORK" > "$WORK/bin/$tool"
done
chmod +x "$WORK/bin/"*
export PATH="$WORK/bin:$PATH"
trap 'rm -rf "$WORK"' EXIT

BEPINEX_VERSION="$(sed -n 's/^BEPINEX_VERSION="\(.*\)"/\1/p' "$INSTALLER")"
FAILED=0
PASSED=0

pass() { PASSED=$((PASSED + 1)); printf '  ok   %s\n' "$*"; }
fail() { FAILED=$((FAILED + 1)); printf '  FAIL %s\n' "$*" >&2; }
assert_file()   { if [ -f "$1" ]; then pass "file exists: ${1#"$WORK"/}"; else fail "missing file: ${1#"$WORK"/}"; fi; }
assert_nofile() { if [ ! -e "$1" ]; then pass "absent: ${1#"$WORK"/}"; else fail "should be absent: ${1#"$WORK"/}"; fi; }
assert_grep()   { if grep -qF -- "$2" "$1"; then pass "'$2' in ${1#"$WORK"/}"; else fail "'$2' not in ${1#"$WORK"/}"; fi; }
assert_nogrep() { if ! grep -qF -- "$2" "$1"; then pass "'$2' absent from ${1#"$WORK"/}"; else fail "'$2' should be absent from ${1#"$WORK"/}"; fi; }
assert_exec()   { if [ -x "$1" ]; then pass "executable: ${1#"$WORK"/}"; else fail "not executable: ${1#"$WORK"/}"; fi; }

# ---- fixtures ----------------------------------------------------------------------------------------
fetch_bepinex() {
  mkdir -p "$CACHE"
  local a
  for a in "BepInEx_macos_universal_${BEPINEX_VERSION}.zip" "BepInEx_linux_x64_${BEPINEX_VERSION}.zip" "BepInEx_win_x64_${BEPINEX_VERSION}.zip"; do
    [ -s "$CACHE/$a" ] && continue
    printf 'fetching %s into the test cache\n' "$a"
    curl -fsSL --retry 3 -o "$CACHE/$a" "https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}/$a"
  done
}

fake_dll() { printf 'MZ\x90\x00fake FTKModFramework for tests\n' > "$1"; }

# A localconfig.vdf with the same shape Steam writes. $1 = path, $2 = variant:
#   noapp     : apps block has other apps but no 527230
#   noopts    : 527230 block without LaunchOptions
#   opts      : 527230 block with LaunchOptions "-popupwindow"
#   optscmd   : 527230 block with LaunchOptions "MANGOHUD=1 %command% -popupwindow"
write_localconfig() {
  local path="$1" variant="$2" appblock=""
  case "$variant" in
    noapp) appblock="" ;;
    noopts) appblock='					"527230"
					{
						"LastPlayed"		"1785704611"
						"Playtime"		"11994"
					}
' ;;
    opts) appblock='					"527230"
					{
						"LastPlayed"		"1785704611"
						"LaunchOptions"		"-popupwindow"
						"Playtime"		"11994"
					}
' ;;
    optscmd) appblock='					"527230"
					{
						"LaunchOptions"		"MANGOHUD=1 %command% -popupwindow"
					}
' ;;
  esac
  mkdir -p "$(dirname "$path")"
  printf '%s' '"UserLocalConfigStore"
{
	"friends"
	{
		"PersonaName"		"Tester"
	}
	"Software"
	{
		"Valve"
		{
			"Steam"
			{
				"apps"
				{
					"240"
					{
						"LastPlayed"		"1325404800"
						"LaunchOptions"		"-novid \"quoted arg\""
					}
' > "$path"
  printf '%s' "$appblock" >> "$path"
  printf '%s' '				}
				"SteamAppBlocked"		"0"
			}
		}
	}
	"system"
	{
		"EnableGameOverlay"		"1"
	}
}
' >> "$path"
}

# Build a Steam root at $1 with a library at $2 (defaults to the root) holding the game.
# $3 = game flavour: elf | pe | app
make_steam() {
  local root="$1" flavour="$3"
  local lib="${2:-$1}"
  local game="$lib/steamapps/common/For The King"
  mkdir -p "$root/config" "$root/steamapps" "$lib/steamapps/common" "$game"
  printf '"libraryfolders"\n{\n\t"0"\n\t{\n\t\t"path"\t\t"%s"\n\t\t"apps"\n\t\t{\n\t\t\t"527230"\t\t"1"\n\t\t}\n\t}\n}\n' "$lib" > "$root/steamapps/libraryfolders.vdf"
  cp "$root/steamapps/libraryfolders.vdf" "$root/config/libraryfolders.vdf"
  printf '"AppState"\n{\n\t"appid"\t\t"527230"\n\t"name"\t\t"For The King"\n\t"installdir"\t\t"For The King"\n}\n' > "$lib/steamapps/appmanifest_527230.acf"
  case "$flavour" in
    elf) printf '\x7fELF\x02\x01\x01\x00fake native linux binary\n' > "$game/FTK.exe"; mkdir -p "$game/FTK_Data/Managed" ;;
    pe)  printf 'MZ\x90\x00fake windows binary\n' > "$game/FTK.exe"; mkdir -p "$game/FTK_Data/Managed" "$lib/steamapps/compatdata/527230" ;;
    app) mkdir -p "$game/FTK.app/Contents/MacOS" "$game/FTK.app/Contents/Resources/Data/Managed"
         printf '#!/bin/sh\necho fake\n' > "$game/FTK.app/Contents/MacOS/FTK"; chmod +x "$game/FTK.app/Contents/MacOS/FTK"
         printf '<?xml version="1.0" encoding="UTF-8"?>\n<plist version="1.0"><dict><key>CFBundleExecutable</key><string>FTK</string></dict></plist>\n' > "$game/FTK.app/Contents/Info.plist" ;;
  esac
  printf '%s' "$game"
}

run_installer() {
  # $1 = HOME for the run; remaining args pass through. Runs quietly, prints output only on failure.
  local fixture_home="$1"; shift
  local out="$WORK/out.txt" rc=0
  HOME="$fixture_home" FTKMF_NO_COLOR=1 FTKMF_BEPINEX_BASE_URL="file://$CACHE" \
    bash "$INSTALLER" --yes "$@" > "$out" 2>&1 || rc=$?
  if [ "$rc" != "0" ]; then
    printf '  installer exited %s:\n' "$rc"; sed 's/^/    | /' "$out"
  fi
  return "$rc"
}

# ---- scenarios ---------------------------------------------------------------------------------------
scenario_linux_native() {
  printf '\n[linux native build, two Steam accounts, existing options merged]\n'
  local home="$WORK/native" root game acct1 acct2
  root="$home/.local/share/Steam"
  game="$(make_steam "$root" "$root" elf)"
  acct1="$root/userdata/111/config/localconfig.vdf"; write_localconfig "$acct1" noapp
  acct2="$root/userdata/222/config/localconfig.vdf"; write_localconfig "$acct2" opts
  fake_dll "$WORK/fake.dll"

  run_installer "$home" --framework "$WORK/fake.dll" || fail "installer failed"
  assert_file "$game/BepInEx/core/BepInEx.dll"
  assert_file "$game/libdoorstop.so"
  assert_exec "$game/run_bepinex.sh"
  assert_grep "$game/run_bepinex.sh" 'executable_name="FTK.exe"'
  assert_file "$game/BepInEx/plugins/FTKModFramework.dll"
  assert_grep "$game/BepInEx/ftkmf-install.state" "bepinex_version=$BEPINEX_VERSION"
  assert_grep "$game/BepInEx/ftkmf-install.state" "build=linux"
  # account 111: no 527230 block existed -> one was inserted inside apps
  assert_grep "$acct1" "\"527230\""
  assert_grep "$acct1" "\"LaunchOptions\"		\"\\\"$game/run_bepinex.sh\\\" %command%\""
  assert_file "$acct1.ftkmf-backup"
  # account 222: existing "-popupwindow" merged after %command%
  assert_grep "$acct2" "\"LaunchOptions\"		\"\\\"$game/run_bepinex.sh\\\" %command% -popupwindow\""
  assert_nogrep "$acct2" "\"LaunchOptions\"		\"-popupwindow\""
  # untouched neighbours
  assert_grep "$acct1" '"LaunchOptions"		"-novid \"quoted arg\""'
  assert_grep "$acct2" '"EnableGameOverlay"		"1"'
  # brace balance preserved
  local open close
  open="$(grep -c '^[[:space:]]*{' "$acct1")"; close="$(grep -c '^[[:space:]]*}' "$acct1")"
  if [ "$open" = "$close" ]; then pass "braces balanced in account 111 ($open)"; else fail "brace imbalance in account 111: $open vs $close"; fi

  printf '\n[linux native: second run is idempotent]\n'
  run_installer "$home" --framework "$WORK/fake.dll" > /dev/null || fail "second run failed"
  if [ "$(grep -c 'run_bepinex.sh' "$acct2")" = "1" ]; then pass "no duplicate LaunchOptions on re-run"; else fail "duplicate LaunchOptions after re-run"; fi
  assert_nofile "$game/run_bepinex.sh.ftkmf-backup-placeholder"

  printf '\n[linux native: --status]\n'
  local out="$WORK/status.txt"
  HOME="$home" FTKMF_NO_COLOR=1 bash "$INSTALLER" --status > "$out" 2>&1 || fail "--status exited non-zero"
  assert_grep "$out" "BepInEx: $BEPINEX_VERSION"
  assert_grep "$out" "FTK Mod Framework: installed"
  assert_grep "$out" "account 222 launch option"

  printf '\n[linux native: --uninstall --purge restores everything]\n'
  run_installer "$home" --uninstall --purge || fail "uninstall failed"
  assert_nofile "$game/BepInEx/plugins/FTKModFramework.dll"
  assert_nofile "$game/BepInEx"
  assert_nofile "$game/run_bepinex.sh"
  assert_nofile "$game/libdoorstop.so"
  assert_nogrep "$acct1" "run_bepinex.sh"
  assert_grep "$acct2" '"LaunchOptions"		"-popupwindow"'
  assert_nogrep "$acct2" "run_bepinex.sh"
}

scenario_proton() {
  printf '\n[proton build in a flatpak Steam, library on an SD card, existing %%command%% option]\n'
  local home="$WORK/proton" root card game acct
  root="$home/.var/app/com.valvesoftware.Steam/.local/share/Steam"
  card="$WORK/run/media/deck/CARD"
  game="$(make_steam "$root" "$card" pe)"
  acct="$root/userdata/333/config/localconfig.vdf"; write_localconfig "$acct" optscmd
  mkdir -p "$WORK/proton-bundle"
  fake_dll "$WORK/proton-bundle/FTKModFramework.dll"
  fake_dll "$WORK/proton-bundle/ftkmf-launcher-helper.exe"

  run_installer "$home" --framework "$WORK/proton-bundle/FTKModFramework.dll" || fail "installer failed"
  assert_file "$game/BepInEx/core/BepInEx.dll"
  assert_file "$game/winhttp.dll"
  assert_file "$game/BepInEx/ftkmf/ftkmf-launcher-helper.exe"
  assert_nofile "$game/BepInEx/ftkmf/ftkmf-launcher-helper"
  assert_grep "$game/BepInEx/ftkmf/helper.json" '"protocolVersion":1'
  assert_file "$game/doorstop_config.ini"
  assert_nofile "$game/run_bepinex.sh"
  assert_grep "$game/BepInEx/ftkmf-install.state" "build=proton"
  assert_grep "$acct" '"LaunchOptions"		"WINEDLLOVERRIDES=\"winhttp=n,b\" MANGOHUD=1 %command% -popupwindow"'

  printf '\n[proton: --uninstall without --purge keeps BepInEx, restores the option]\n'
  run_installer "$home" --uninstall || fail "uninstall failed"
  assert_file "$game/BepInEx/core/BepInEx.dll"
  assert_nofile "$game/BepInEx/plugins/FTKModFramework.dll"
  assert_grep "$acct" '"LaunchOptions"		"MANGOHUD=1 %command% -popupwindow"'
}

scenario_foreign_bepinex() {
  printf '\n[existing BepInEx + existing BepInEx launch option are left alone]\n'
  local home="$WORK/foreign" root game acct
  root="$home/.local/share/Steam"
  game="$(make_steam "$root" "$root" elf)"
  mkdir -p "$game/BepInEx/core" "$game/BepInEx/plugins"
  printf 'foreign' > "$game/BepInEx/core/BepInEx.dll"
  printf 'foreign' > "$game/libdoorstop.so"
  printf '#!/bin/sh\nexecutable_name="FTK.exe"\n' > "$game/run_bepinex.sh"; chmod +x "$game/run_bepinex.sh"
  acct="$root/userdata/444/config/localconfig.vdf"
  write_localconfig "$acct" noopts
  sed -i.bak 's|"Playtime"		"11994"|"LaunchOptions"		"\\"/custom/run_bepinex.sh\\" %command%"|' "$acct" && rm -f "$acct.bak"
  fake_dll "$WORK/fake.dll"

  run_installer "$home" --framework "$WORK/fake.dll" || fail "installer failed"
  assert_grep "$game/BepInEx/core/BepInEx.dll" "foreign"
  assert_file "$game/BepInEx/plugins/FTKModFramework.dll"
  assert_grep "$acct" '"/custom/run_bepinex.sh\" %command%'
  assert_nogrep "$game/BepInEx/ftkmf-install.state" "bepinex_installed_by_us=1"

  printf '\n[--reinstall-bepinex replaces it and backs up the custom script]\n'
  run_installer "$home" --framework "$WORK/fake.dll" --reinstall-bepinex || fail "reinstall failed"
  assert_nogrep "$game/BepInEx/core/BepInEx.dll" "foreign"
  if ls "$game"/run_bepinex.sh.ftkmf-backup-* >/dev/null 2>&1; then pass "custom run_bepinex.sh backed up"; else fail "no backup of the custom run_bepinex.sh"; fi
  assert_grep "$game/run_bepinex.sh" 'executable_name="FTK.exe"'
  assert_nogrep "$game/BepInEx/ftkmf-install.state" "bepinex_installed_by_us=1"
  printf 'other mod' > "$game/BepInEx/plugins/OtherMod.dll"
  run_installer "$home" --uninstall --purge || fail "foreign purge failed"
  assert_file "$game/BepInEx/plugins/OtherMod.dll"
  assert_file "$game/BepInEx/core/BepInEx.dll"
}

scenario_dry_run_and_errors() {
  printf '\n[--dry-run changes nothing; wrong --game-dir fails cleanly]\n'
  local home="$WORK/dry" root game
  root="$home/.local/share/Steam"
  game="$(make_steam "$root" "$root" elf)"
  write_localconfig "$root/userdata/555/config/localconfig.vdf" noapp
  fake_dll "$WORK/fake.dll"
  run_installer "$home" --framework "$WORK/fake.dll" --dry-run || fail "dry run failed"
  assert_nofile "$game/BepInEx"
  assert_nogrep "$root/userdata/555/config/localconfig.vdf" "run_bepinex"
  if run_installer "$home" --framework "$WORK/fake.dll" --game-dir "$WORK/nowhere" >/dev/null 2>&1; then
    fail "bad --game-dir should fail"
  else
    pass "bad --game-dir fails"
  fi
  printf 'not a dll' > "$WORK/bad.dll"
  if run_installer "$home" --framework "$WORK/bad.dll" >/dev/null 2>&1; then fail "non-PE framework should be rejected"; else pass "non-PE framework rejected"; fi
  assert_nofile "$game/BepInEx/plugins/FTKModFramework.dll"
}

scenario_dev_config() {
  printf '\n[--dev writes RunSelfTests into the framework config, merging with an existing file]\n'
  local home="$WORK/dev" root game cfg
  root="$home/.local/share/Steam"
  game="$(make_steam "$root" "$root" elf)"
  write_localconfig "$root/userdata/666/config/localconfig.vdf" noopts
  fake_dll "$WORK/fake.dll"
  cfg="$game/BepInEx/config/com.ftkmf.framework.cfg"
  mkdir -p "$(dirname "$cfg")"
  printf '[Demo]\nEnableSampleContent = true\n\n[Diagnostics]\nRunSelfTests = false\nEnableScaleBudgetGate = false\n' > "$cfg"
  run_installer "$home" --framework "$WORK/fake.dll" --dev || fail "dev install failed"
  assert_grep "$cfg" "RunSelfTests = true"
  assert_nogrep "$cfg" "RunSelfTests = false"
  assert_grep "$cfg" "EnableScaleBudgetGate = false"
  if [ "$(grep -c '^\[Diagnostics\]' "$cfg")" = "1" ]; then pass "single [Diagnostics] section"; else fail "duplicate [Diagnostics] sections"; fi
}

scenario_macos() {
  printf '\n[macOS build: FTK.app, absolute launch option, quarantine cleared]\n'
  local home="$WORK/mac" root game acct
  root="$home/Library/Application Support/Steam"
  game="$(make_steam "$root" "$root" app)"
  acct="$root/userdata/777/config/localconfig.vdf"; write_localconfig "$acct" noopts
  fake_dll "$WORK/fake.dll"
  run_installer "$home" --framework "$WORK/fake.dll" || fail "installer failed"
  assert_file "$game/libdoorstop.dylib"
  assert_exec "$game/run_bepinex.sh"
  assert_grep "$game/run_bepinex.sh" 'executable_name="FTK.app"'
  assert_grep "$game/BepInEx/ftkmf-install.state" "build=mac"
  assert_grep "$acct" "\"LaunchOptions\"		\"\\\"$game/run_bepinex.sh\\\" %command%\""
}

scenario_player_config() {
  printf '\n[player install resets development flags and preserves gameplay settings]\n'
  local fixture_home="$WORK/player" root game cfg
  if [ "$OS" = Darwin ]; then
    root="$fixture_home/Library/Application Support/Steam"
    game="$(make_steam "$root" "$root" app)"
  else
    root="$fixture_home/.local/share/Steam"
    game="$(make_steam "$root" "$root" elf)"
  fi
  cfg="$game/BepInEx/config/com.ftkmf.framework.cfg"
  mkdir -p "$(dirname "$cfg")"
  printf '[Diagnostics]\nRunSelfTests = true\nEnableScaleBudgetGate = true\nSyntheticContentCount = 50\n[Enemies]\nForceCustomEnemy = true\n[Adventures]\nForceCustomEncounter = true\n[Demo]\nEnableSampleContent = false\n' > "$cfg"
  fake_dll "$WORK/player.dll"
  run_installer "$fixture_home" --framework "$WORK/player.dll" --dev --player --no-launch-options || fail "player install failed"
  assert_grep "$cfg" "RunSelfTests = false"
  assert_grep "$cfg" "EnableScaleBudgetGate = false"
  assert_grep "$cfg" "SyntheticContentCount = 0"
  assert_grep "$cfg" "ForceCustomEnemy = false"
  assert_grep "$cfg" "ForceCustomEncounter = false"
  assert_grep "$cfg" "EnableSampleContent = false"
  run_installer "$fixture_home" --framework "$WORK/player.dll" --dev --no-launch-options || fail "developer install failed"
  assert_grep "$cfg" "RunSelfTests = true"
}

scenario_release_checksums() {
  printf '\n[release downloads require a valid matching checksum]\n'
  local fixture_home="$WORK/checksums" root game mode
  if [ "$OS" = Darwin ]; then
    root="$fixture_home/Library/Application Support/Steam"
    game="$(make_steam "$root" "$root" app)"
  else
    root="$fixture_home/.local/share/Steam"
    game="$(make_steam "$root" "$root" elf)"
  fi
  fake_dll "$WORK/release.dll"
  # Seed BepInEx from the cache before replacing curl with a release-only mock.
  run_installer "$fixture_home" --framework "$WORK/release.dll" --no-launch-options || fail "seed install failed"
  printf 'old framework' > "$game/BepInEx/plugins/FTKModFramework.dll"
  export FTKMF_TEST_RELEASE_DIR="$WORK"
  cat > "$WORK/bin/curl" <<'SH'
#!/bin/bash
dest=""; url=""; probe=0
while [ $# -gt 0 ]; do
  case "$1" in
    -o) dest="$2"; shift 2 ;;
    -w) probe=1; shift 2 ;;
    --retry|--retry-delay|--connect-timeout) shift 2 ;;
    https://*) url="$1"; shift ;;
    *) shift ;;
  esac
done
if [ "$probe" = 1 ]; then printf 200; exit 0; fi
case "$url" in
  */FTKModFramework.dll) cp "$FTKMF_TEST_RELEASE_DIR/release.dll" "$dest" ;;
  */ftkmf-helper-*) cp "$FTKMF_TEST_RELEASE_DIR/release.dll" "$dest" ;;
  */SHA256SUMS)
    case "$FTKMF_TEST_SUM_MODE" in
      missing) exit 22 ;;
      absent) printf '%064d  other.dll\n' 0 > "$dest" ;;
      malformed) printf 'bad  FTKModFramework.dll\n' > "$dest" ;;
      mismatch) printf '%064d  FTKModFramework.dll\n' 0 > "$dest" ;;
      valid) cp "$FTKMF_TEST_RELEASE_DIR/valid-sums" "$dest" ;;
      helper-absent) head -n 1 "$FTKMF_TEST_RELEASE_DIR/valid-sums" > "$dest" ;;
      helper-mismatch) sed '/ftkmf-helper-/s/^[^ ]*/0000000000000000000000000000000000000000000000000000000000000000/' "$FTKMF_TEST_RELEASE_DIR/valid-sums" > "$dest" ;;
    esac ;;
  *) exit 99 ;;
esac
SH
  chmod +x "$WORK/bin/curl"
  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$WORK/release.dll"
  else
    sha256sum "$WORK/release.dll"
  fi | awk '{print $1 "  FTKModFramework.dll"}' > "$WORK/valid-sums"
  local helper_hash helper_asset
  helper_hash="$(awk '{print $1}' "$WORK/valid-sums")"
  for helper_asset in ftkmf-helper-macos-universal ftkmf-helper-linux-amd64 ftkmf-helper-linux-arm64 ftkmf-helper-windows-amd64.exe; do
    printf '%s  %s\n' "$helper_hash" "$helper_asset" >> "$WORK/valid-sums"
  done
  for mode in missing absent malformed mismatch helper-absent helper-mismatch; do
    export FTKMF_TEST_SUM_MODE="$mode"
    if run_installer "$fixture_home" --no-launch-options >/dev/null 2>&1; then
      fail "$mode checksum accepted"
    else
      pass "$mode checksum rejected"
    fi
    assert_grep "$game/BepInEx/plugins/FTKModFramework.dll" "old framework"
  done
  export FTKMF_TEST_SUM_MODE=valid
  run_installer "$fixture_home" --no-launch-options || fail "valid checksum rejected"
  if cmp -s "$WORK/release.dll" "$game/BepInEx/plugins/FTKModFramework.dll"; then
    pass "verified release installed"
    assert_exec "$game/BepInEx/ftkmf/ftkmf-launcher-helper"
    assert_grep "$game/BepInEx/ftkmf/helper.json" "$helper_hash"
    assert_grep "$game/BepInEx/ftkmf/helper.json" '"protocolVersion":1'
  else
    fail "verified release not installed"
  fi
}

# ---- run ---------------------------------------------------------------------------------------------
printf 'install.sh tests (%s, BepInEx %s)\n' "$OS" "$BEPINEX_VERSION"
fetch_bepinex
if [ "$OS" = "Darwin" ]; then
  scenario_macos
else
  scenario_linux_native
  scenario_proton
  scenario_foreign_bepinex
  scenario_dry_run_and_errors
  scenario_dev_config
fi

scenario_player_config
scenario_release_checksums
assert_nofile "$WORK/host-command-called"

printf '\n%d passed, %d failed\n' "$PASSED" "$FAILED"
[ "$FAILED" = "0" ]
