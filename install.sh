#!/usr/bin/env bash
# FTK Mod Framework installer for macOS and Linux (SteamOS, Bazzite, and other distros).
#
# One command sets up everything For The King needs to run mods:
#   1. finds your Steam copy of For The King,
#   2. installs the BepInEx mod loader that matches your build (macOS, native Linux, or Proton),
#   3. installs the FTK Mod Framework plugin,
#   4. sets the Steam launch option that turns the loader on.
#
# Run it as:   bash install.sh          (or:  curl -fsSL <raw url>/install.sh | bash)
# See --help for options. Needs only curl (or wget), unzip (or bsdtar / python3), and a shell.
#
# Compatible with the bash 3.2 that ships on macOS: no associative arrays, no mapfile, no ${var,,}.
set -euo pipefail

INSTALLER_VERSION="1.0.0"

# ---------------------------------------------------------------------------------------------------
# Pinned versions and checksums. Bump these together.
# ---------------------------------------------------------------------------------------------------
STEAM_APP_ID="527230"
STEAM_INSTALL_DIR="For The King"

BEPINEX_VERSION="5.4.23.5"
BEPINEX_BASE_URL="${FTKMF_BEPINEX_BASE_URL:-https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}}"
BEPINEX_ASSET_MAC="BepInEx_macos_universal_${BEPINEX_VERSION}.zip"
BEPINEX_SHA_MAC="01c2ae782eb016dfd6c345a18dbd2dcafffb3d9d318449d6486689f426b4a323"
BEPINEX_ASSET_LINUX="BepInEx_linux_x64_${BEPINEX_VERSION}.zip"
BEPINEX_SHA_LINUX="e538560be65739f562519ab518a75f9c65b3f57f87457403ae7cde683c12dab7"
BEPINEX_ASSET_WIN="BepInEx_win_x64_${BEPINEX_VERSION}.zip"
BEPINEX_SHA_WIN="82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4"

FRAMEWORK_REPO="${FTKMF_REPO:-jarlbrak/ftk-mod-framework}"
FRAMEWORK_DLL_NAME="FTKModFramework.dll"
FRAMEWORK_SUMS_NAME="SHA256SUMS"
FRAMEWORK_PLUGIN_GUID="com.ftkmf.framework"

STATE_FILE_NAME="ftkmf-install.state"

# ---------------------------------------------------------------------------------------------------
# Options (defaults; see usage)
# ---------------------------------------------------------------------------------------------------
OPT_GAME_DIR="${FTK_DIR:-}"
OPT_FRAMEWORK="${FTKMF_DLL:-}"
OPT_HELPER=""
OPT_RELEASE="${FTKMF_RELEASE:-latest}"
OPT_DEV=0
OPT_NO_LAUNCH=0
OPT_ACTION="install"     # install | status | uninstall
OPT_PURGE=0
OPT_DRY_RUN=0
OPT_YES=0
OPT_REINSTALL_BEPINEX=0
OPT_STEAM_ROOT="${FTKMF_STEAM_ROOT:-}"   # testing aid: force one Steam root

# ---------------------------------------------------------------------------------------------------
# Output helpers
# ---------------------------------------------------------------------------------------------------
if [ -t 1 ] && [ -z "${NO_COLOR:-}" ] && [ -z "${FTKMF_NO_COLOR:-}" ]; then
  C_BOLD="$(printf '\033[1m')"; C_DIM="$(printf '\033[2m')"; C_RED="$(printf '\033[31m')"
  C_GREEN="$(printf '\033[32m')"; C_YELLOW="$(printf '\033[33m')"; C_CYAN="$(printf '\033[36m')"
  C_OFF="$(printf '\033[0m')"
else
  C_BOLD=""; C_DIM=""; C_RED=""; C_GREEN=""; C_YELLOW=""; C_CYAN=""; C_OFF=""
fi

say()  { printf '%s\n' "$*"; }
step() { printf '\n%s==>%s %s%s%s\n' "$C_CYAN" "$C_OFF" "$C_BOLD" "$*" "$C_OFF"; }
ok()   { printf '  %s✓%s %s\n' "$C_GREEN" "$C_OFF" "$*"; }
info() { printf '  %s\n' "$*"; }
note() { printf '  %s%s%s\n' "$C_DIM" "$*" "$C_OFF"; }
warn() { printf '  %s!%s %s\n' "$C_YELLOW" "$C_OFF" "$*" >&2; }
die()  { printf '\n%sERROR:%s %s\n' "$C_RED" "$C_OFF" "$*" >&2; exit 1; }

usage() {
  cat <<EOF
FTK Mod Framework installer ${INSTALLER_VERSION} (macOS + Linux)

Usage: install.sh [options]

  (no options)          Install: BepInEx ${BEPINEX_VERSION} + the latest FTK Mod Framework release,
                        and set the Steam launch option for For The King.
  --status              Show what is installed and exit.
  --uninstall           Remove the framework and restore the Steam launch option.
  --purge               With --uninstall: also remove BepInEx (only if this installer put it there).

  --game-dir PATH       Game folder (default: found through your Steam libraries).
  --framework PATH      Install this FTKModFramework.dll (or a .zip holding it) instead of downloading.
  --release TAG         Framework release tag to download (default: latest).
  --helper PATH         Install a trusted local marketplace helper alongside a local framework.
  --dev                 Developer mode: turn on the framework's load-time self-tests in its config.
  --player              Player mode (default): disable diagnostics and forced test encounters/enemies.
  --reinstall-bepinex   Replace an existing BepInEx with the pinned ${BEPINEX_VERSION}.
  --no-launch-options   Do not touch Steam's launch options (prints the line to paste instead).
  --dry-run             Show what would change without changing anything.
  -y, --yes             Assume "yes" for questions (for example, closing Steam to write settings).
  -h, --help            This text.

Environment: FTK_DIR (same as --game-dir), FTKMF_DLL (same as --framework), FTKMF_RELEASE.
EOF
}

# ---------------------------------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------------------------------
while [ $# -gt 0 ]; do
  case "$1" in
    --game-dir) [ $# -ge 2 ] || die "--game-dir needs a path"; OPT_GAME_DIR="$2"; shift 2 ;;
    --game-dir=*) OPT_GAME_DIR="${1#*=}"; shift ;;
    --framework) [ $# -ge 2 ] || die "--framework needs a path"; OPT_FRAMEWORK="$2"; shift 2 ;;
    --framework=*) OPT_FRAMEWORK="${1#*=}"; shift ;;
    --helper) [ $# -ge 2 ] || die "--helper needs a path"; OPT_HELPER="$2"; shift 2 ;;
    --release) [ $# -ge 2 ] || die "--release needs a tag"; OPT_RELEASE="$2"; shift 2 ;;
    --release=*) OPT_RELEASE="${1#*=}"; shift ;;
    --dev) OPT_DEV=1; shift ;;
    --player) OPT_DEV=0; shift ;;
    --reinstall-bepinex) OPT_REINSTALL_BEPINEX=1; shift ;;
    --no-launch-options) OPT_NO_LAUNCH=1; shift ;;
    --status) OPT_ACTION="status"; shift ;;
    --launcher-status) OPT_ACTION="launcher-status"; shift ;;
    --uninstall) OPT_ACTION="uninstall"; shift ;;
    --purge) OPT_PURGE=1; shift ;;
    --dry-run) OPT_DRY_RUN=1; shift ;;
    -y|--yes) OPT_YES=1; shift ;;
    -h|--help) usage; exit 0 ;;
    --version) say "$INSTALLER_VERSION"; exit 0 ;;
    *) usage >&2; die "unknown option: $1" ;;
  esac
done

# ---------------------------------------------------------------------------------------------------
# Small utilities
# ---------------------------------------------------------------------------------------------------
have() { command -v "$1" >/dev/null 2>&1; }

# Absolute, symlink-resolved directory path (portable: no readlink -f on macOS).
abs_dir() { (cd -P -- "$1" 2>/dev/null && pwd -P); }

# Ask a yes/no question on the terminal even when stdin is a pipe (curl | bash). $2 is the default
# answer when there is no terminal or the user just presses Enter.
ask_yes_no() {
  local question="$1" default="${2:-n}" answer=""
  if [ "$OPT_YES" = "1" ]; then return 0; fi
  if [ -r /dev/tty ] && [ -w /dev/tty ]; then
    if [ "$default" = "y" ]; then
      printf '  %s [Y/n] ' "$question" >/dev/tty
    else
      printf '  %s [y/N] ' "$question" >/dev/tty
    fi
    read -r answer </dev/tty || answer=""
  fi
  [ -z "$answer" ] && answer="$default"
  case "$answer" in
    y|Y|yes|YES|Yes) return 0 ;;
    *) return 1 ;;
  esac
}

# Prompt for a line of text on the terminal; empty when there is no terminal.
ask_line() {
  local prompt="$1" answer=""
  if [ -r /dev/tty ] && [ -w /dev/tty ]; then
    printf '  %s ' "$prompt" >/dev/tty
    read -r answer </dev/tty || answer=""
  fi
  printf '%s' "$answer"
}

sha256_of() {
  if have shasum; then shasum -a 256 "$1" | awk '{print $1}'
  elif have sha256sum; then sha256sum "$1" | awk '{print $1}'
  elif have openssl; then openssl dgst -sha256 "$1" | awk '{print $NF}'
  else die "need shasum, sha256sum, or openssl to verify downloads"
  fi
}

download() {
  local url="$1" dest="$2"
  if have curl; then
    curl -fL --retry 3 --retry-delay 2 --connect-timeout 20 -o "$dest" "$url"
  elif have wget; then
    wget -q -O "$dest" "$url"
  else
    die "need curl or wget to download files"
  fi
}

# HTTP status probe (used to give a clear message when a release does not exist yet).
http_status() {
  local url="$1"
  if have curl; then
    curl -sIL -o /dev/null -w '%{http_code}' --connect-timeout 20 "$url" 2>/dev/null || printf '000'
  else
    printf '000'
  fi
}

extract_zip() {
  local zip="$1" dest="$2"
  mkdir -p "$dest"
  if have unzip; then unzip -q -o "$zip" -d "$dest"
  elif have bsdtar; then bsdtar -xf "$zip" -C "$dest"
  elif have python3; then python3 -m zipfile -e "$zip" "$dest"
  elif tar --version 2>/dev/null | grep -qi bsdtar; then tar -xf "$zip" -C "$dest"
  else die "need unzip, bsdtar, or python3 to extract archives"
  fi
}

# First bytes of a file as lowercase hex (for ELF / PE detection).
magic_hex() { head -c 4 "$1" 2>/dev/null | od -An -tx1 | tr -d ' \n'; }

# Escape a string for use inside a double-quoted VDF value.
vdf_escape() { printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'; }
vdf_unescape() { printf '%s' "$1" | sed -e 's/\\"/"/g' -e 's/\\\\/\\/g'; }

# Replace a file's contents atomically with the contents of $2, keeping $1's permissions.
replace_file() {
  local target="$1" new="$2"
  if [ -f "$target" ]; then cat "$new" > "$target"; else mv "$new" "$target"; fi
}

timestamp() { date +%Y%m%d-%H%M%S; }

TMP_DIR=""
cleanup() { [ -n "$TMP_DIR" ] && [ -d "$TMP_DIR" ] && rm -rf "$TMP_DIR"; }
trap cleanup EXIT
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ftkmf-install.XXXXXX")"

# ---------------------------------------------------------------------------------------------------
# State file: key=value lines under BepInEx/, so uninstall can put things back exactly.
# ---------------------------------------------------------------------------------------------------
state_path() { printf '%s/BepInEx/%s' "$GAME_DIR" "$STATE_FILE_NAME"; }
state_get() {
  local f; f="$(state_path)"
  [ -f "$f" ] || return 0
  awk -F= -v k="$1" '$1==k { sub(/^[^=]*=/, ""); print; exit }' "$f"
}
state_set() {
  local f key="$1" value="$2" tmp
  f="$(state_path)"
  [ "$OPT_DRY_RUN" = "1" ] && return 0
  mkdir -p "$(dirname "$f")"
  tmp="$TMP_DIR/state.$$"
  if [ -f "$f" ]; then awk -F= -v k="$key" '$1!=k' "$f" > "$tmp"; else : > "$tmp"; fi
  printf '%s=%s\n' "$key" "$value" >> "$tmp"
  cat "$tmp" > "$f"
}
state_del() {
  local f key="$1" tmp
  f="$(state_path)"
  [ -f "$f" ] || return 0
  [ "$OPT_DRY_RUN" = "1" ] && return 0
  tmp="$TMP_DIR/state.$$"
  awk -F= -v k="$key" '$1!=k' "$f" > "$tmp"
  cat "$tmp" > "$f"
}

# ---------------------------------------------------------------------------------------------------
# Platform
# ---------------------------------------------------------------------------------------------------
OS=""
detect_platform() {
  case "$(uname -s)" in
    Darwin) OS="macos" ;;
    Linux)  OS="linux" ;;
    *) die "unsupported operating system: $(uname -s). This installer covers macOS and Linux." ;;
  esac
}

# ---------------------------------------------------------------------------------------------------
# Steam discovery
# ---------------------------------------------------------------------------------------------------
STEAM_ROOTS=""        # newline-separated, resolved, deduplicated
STEAM_ROOT_PRIMARY="" # the root whose userdata/ we edit
STEAM_IS_FLATPAK=0

add_steam_root() {
  local r
  [ -d "$1" ] || return 0
  r="$(abs_dir "$1")" || return 0
  [ -n "$r" ] || return 0
  case "$STEAM_ROOTS" in
    *"$r"*) return 0 ;;
  esac
  STEAM_ROOTS="${STEAM_ROOTS}${r}
"
  [ -z "$STEAM_ROOT_PRIMARY" ] && STEAM_ROOT_PRIMARY="$r"
  case "$r" in *"/.var/app/com.valvesoftware.Steam/"*) STEAM_IS_FLATPAK=1 ;; esac
}

find_steam_roots() {
  if [ -n "$OPT_STEAM_ROOT" ]; then add_steam_root "$OPT_STEAM_ROOT"; return 0; fi
  if [ "$OS" = "macos" ]; then
    add_steam_root "$HOME/Library/Application Support/Steam"
  else
    add_steam_root "$HOME/.local/share/Steam"
    add_steam_root "$HOME/.steam/steam"
    add_steam_root "$HOME/.steam/root"
    add_steam_root "$HOME/.steam/debian-installation"
    add_steam_root "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
    add_steam_root "$HOME/.var/app/com.valvesoftware.Steam/.steam/steam"
    add_steam_root "$HOME/snap/steam/common/.local/share/Steam"
  fi
}

# Print every "path" value from a libraryfolders.vdf (one per line, unescaped).
vdf_library_paths() {
  [ -f "$1" ] || return 0
  awk '
    /^[ \t]*"path"[ \t]+"/ {
      line=$0
      sub(/^[ \t]*"path"[ \t]+"/, "", line)
      sub(/"[ \t\r]*$/, "", line)
      gsub(/\\\\/, "\\", line)
      print line
    }' "$1"
}

# Print candidate game folders from every Steam library, one per line.
candidate_game_dirs() {
  local root lib libs="" installdir manifest
  printf '%s' "$STEAM_ROOTS" | while IFS= read -r root; do
    [ -n "$root" ] || continue
    libs="$root
$(vdf_library_paths "$root/steamapps/libraryfolders.vdf")
$(vdf_library_paths "$root/config/libraryfolders.vdf")"
    printf '%s\n' "$libs" | while IFS= read -r lib; do
      [ -n "$lib" ] || continue
      [ -d "$lib/steamapps" ] || continue
      manifest="$lib/steamapps/appmanifest_${STEAM_APP_ID}.acf"
      installdir="$STEAM_INSTALL_DIR"
      if [ -f "$manifest" ]; then
        installdir="$(awk '/^[ \t]*"installdir"[ \t]+"/ { line=$0; sub(/^[ \t]*"installdir"[ \t]+"/, "", line); sub(/"[ \t\r]*$/, "", line); print line; exit }' "$manifest")"
        [ -n "$installdir" ] || installdir="$STEAM_INSTALL_DIR"
      fi
      [ -d "$lib/steamapps/common/$installdir" ] && printf '%s\n' "$lib/steamapps/common/$installdir"
    done
  done
}

# ---------------------------------------------------------------------------------------------------
# Game folder + build detection
# ---------------------------------------------------------------------------------------------------
GAME_DIR=""
BUILD=""            # mac | linux | proton
GAME_EXE_NAME=""    # FTK.app | FTK.exe

is_game_dir() {
  [ -d "$1" ] || return 1
  [ -d "$1/FTK.app" ] && return 0
  [ -f "$1/FTK.exe" ] && [ -d "$1/FTK_Data" ] && return 0
  return 1
}

normalize_game_dir() {
  local d="$1"
  d="${d%/}"
  case "$d" in
    */FTK.app) d="${d%/FTK.app}" ;;
    */FTK.app/Contents/MacOS) d="${d%/FTK.app/Contents/MacOS}" ;;
  esac
  printf '%s' "$d"
}

find_game_dir() {
  local d found="" count=0 typed
  if [ -n "$OPT_GAME_DIR" ]; then
    d="$(normalize_game_dir "$OPT_GAME_DIR")"
    is_game_dir "$d" || die "no For The King install at: $d (expected FTK.app, or FTK.exe + FTK_Data)"
    GAME_DIR="$(abs_dir "$d")"
    return 0
  fi

  find_steam_roots
  while IFS= read -r d; do
    [ -n "$d" ] || continue
    is_game_dir "$d" || continue
    d="$(abs_dir "$d")"
    case "$found" in *"$d"*) continue ;; esac
    found="${found}${d}
"
    count=$((count + 1))
  done <<EOF
$(candidate_game_dirs)
EOF

  if [ "$count" -eq 0 ]; then
    warn "could not find For The King in your Steam libraries."
    if [ -z "$STEAM_ROOTS" ]; then
      warn "no Steam installation found either (looked in the usual places)."
    fi
    [ "$OPT_ACTION" != "launcher-status" ] || die "For The King not found. Install it through Steam first, or set FTK_DIR to its game folder."
    typed="$(ask_line "Enter the game folder path (the one holding FTK.app or FTK.exe), or leave blank to stop:")"
    [ -n "$typed" ] || die "For The King not found. Install it through Steam first, or re-run with --game-dir PATH."
    d="$(normalize_game_dir "$typed")"
    is_game_dir "$d" || die "no For The King install at: $d"
    GAME_DIR="$(abs_dir "$d")"
    return 0
  fi

  if [ "$count" -gt 1 ]; then
    warn "found more than one For The King install; using the first. Pass --game-dir to choose:"
    printf '%s' "$found" | while IFS= read -r d; do [ -n "$d" ] && info "  $d" >&2; done
  fi
  GAME_DIR="$(printf '%s' "$found" | head -n 1)"
}

detect_build() {
  local magic
  if [ -d "$GAME_DIR/FTK.app" ]; then
    [ "$OS" = "macos" ] || die "this is the macOS build of the game (FTK.app) but we are not on macOS."
    BUILD="mac"; GAME_EXE_NAME="FTK.app"
    return 0
  fi
  [ "$OS" = "linux" ] || die "found a Windows/Linux build (FTK.exe) but we are on macOS; only FTK.app is supported here."
  magic="$(magic_hex "$GAME_DIR/FTK.exe")"
  case "$magic" in
    7f454c46*) BUILD="linux"; GAME_EXE_NAME="FTK.exe" ;;   # ELF: IronOak ships the native Linux binary as FTK.exe
    4d5a*)     BUILD="proton"; GAME_EXE_NAME="FTK.exe" ;;  # PE: the Windows depot, run through Proton
    *) die "cannot tell what kind of executable $GAME_DIR/FTK.exe is (magic: $magic)." ;;
  esac
}

describe_build() {
  case "$BUILD" in
    mac)    printf 'macOS build (FTK.app)' ;;
    linux)  printf 'native Linux build (ELF FTK.exe)' ;;
    proton) printf 'Windows build run through Proton (PE FTK.exe)' ;;
  esac
}

# ---------------------------------------------------------------------------------------------------
# BepInEx
# ---------------------------------------------------------------------------------------------------
bepinex_asset()  { case "$BUILD" in mac) printf '%s' "$BEPINEX_ASSET_MAC" ;; linux) printf '%s' "$BEPINEX_ASSET_LINUX" ;; proton) printf '%s' "$BEPINEX_ASSET_WIN" ;; esac; }
bepinex_sha()    { case "$BUILD" in mac) printf '%s' "$BEPINEX_SHA_MAC" ;; linux) printf '%s' "$BEPINEX_SHA_LINUX" ;; proton) printf '%s' "$BEPINEX_SHA_WIN" ;; esac; }

# The loader piece that must sit next to the game for this build to be usable.
bepinex_launcher_ok() {
  [ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ] || return 1
  case "$BUILD" in
    mac)    [ -f "$GAME_DIR/run_bepinex.sh" ] && [ -f "$GAME_DIR/libdoorstop.dylib" ] ;;
    linux)  [ -f "$GAME_DIR/run_bepinex.sh" ] && [ -f "$GAME_DIR/libdoorstop.so" ] ;;
    proton) [ -f "$GAME_DIR/winhttp.dll" ] && [ -f "$GAME_DIR/doorstop_config.ini" ] ;;
  esac
}

bepinex_present() { [ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ]; }

# Version of the installed BepInEx: from our state file, else inferred from the Doorstop version the
# archives ship (BepInEx 5.4.23.x each bundle one specific Doorstop), else unknown.
installed_bepinex_version() {
  local v doorstop=""
  v="$(state_get bepinex_version)"
  if [ -n "$v" ]; then printf '%s' "$v"; return 0; fi
  [ -f "$GAME_DIR/.doorstop_version" ] && doorstop="$(tr -d '[:space:]' < "$GAME_DIR/.doorstop_version")"
  case "$doorstop" in
    4.3.0) v="5.4.23.2" ;;
    4.4.0) v="5.4.23.3" ;;
    4.4.1) v="5.4.23.4" ;;
    4.5.0) v="5.4.23.5" ;;
    "")    v="unknown version" ;;
    *)     v="unknown version (Doorstop $doorstop)" ;;
  esac
  printf '%s (not installed by this tool)' "$v"
}

# Set executable_name in run_bepinex.sh without touching anything else in the upstream script.
configure_run_script() {
  local script="$GAME_DIR/run_bepinex.sh" tmp="$TMP_DIR/run_bepinex.sh"
  [ -f "$script" ] || return 0
  awk -v exe="$GAME_EXE_NAME" '
    /^executable_name=/ && !done { print "executable_name=\"" exe "\""; done=1; next }
    { print }' "$script" > "$tmp"
  [ "$OPT_DRY_RUN" = "1" ] && return 0
  replace_file "$script" "$tmp"
  chmod +x "$script"
}

clear_quarantine() {
  [ "$OS" = "macos" ] || return 0
  [ "$OPT_DRY_RUN" = "1" ] && return 0
  have xattr || return 0
  local p
  for p in "$GAME_DIR/BepInEx" "$GAME_DIR/run_bepinex.sh" "$GAME_DIR/libdoorstop.dylib"; do
    if [ -e "$p" ]; then
      xattr -dr com.apple.quarantine "$p" >/dev/null 2>&1 || true
    fi
  done
}

# A hardened-runtime or library-validated signature on FTK.app blocks the DYLD injection BepInEx
# relies on. Steam's build is unsigned today; this guards against a future signed build.
check_codesign() {
  [ "$BUILD" = "mac" ] || return 0
  have codesign || return 0
  local flags
  if ! codesign -d "$GAME_DIR/FTK.app" >/dev/null 2>&1; then
    ok "FTK.app is not code-signed (BepInEx can inject)."
    return 0
  fi
  flags="$(codesign -d --verbose=2 "$GAME_DIR/FTK.app" 2>&1 | awk -F= '/^CodeDirectory/ { for (i=1;i<=NF;i++) if ($i ~ /flags$/) print $(i+1) }' | head -n 1)"
  case "$flags" in
    *runtime*|*library-validation*)
      warn "FTK.app is signed with a hardened runtime ($flags); macOS would refuse to load BepInEx into it."
      if ask_yes_no "Remove the app's code signature so mods can load? (Steam's Verify Integrity restores it)" y; then
        [ "$OPT_DRY_RUN" = "1" ] || codesign --remove-signature "$GAME_DIR/FTK.app"
        ok "signature removed."
      else
        warn "left as is; BepInEx will probably not load."
      fi ;;
    *) ok "FTK.app signature does not block injection." ;;
  esac
}

install_bepinex() {
  local asset sha zip extract src
  local owned
  owned="$(state_get bepinex_installed_by_us)"
  if [ ! -d "$GAME_DIR/BepInEx" ]; then owned=1; fi
  asset="$(bepinex_asset)"; sha="$(bepinex_sha)"

  if bepinex_present && [ "$OPT_REINSTALL_BEPINEX" != "1" ]; then
    if [ "$(state_get bepinex_version)" = "$BEPINEX_VERSION" ] && bepinex_launcher_ok; then
      ok "BepInEx $BEPINEX_VERSION already installed."
      configure_run_script; clear_quarantine
      return 0
    fi
    if [ -z "$(state_get bepinex_version)" ] && bepinex_launcher_ok; then
      ok "keeping the BepInEx that is already installed (not put there by this tool)."
      note "pass --reinstall-bepinex to replace it with the pinned $BEPINEX_VERSION."
      configure_run_script; clear_quarantine
      return 0
    fi
    info "BepInEx present but incomplete or outdated for this build; installing $BEPINEX_VERSION over it"
    note "(BepInEx/config, plugins, and patchers are kept)."
  fi

  info "downloading BepInEx $BEPINEX_VERSION ($asset)"
  zip="$TMP_DIR/$asset"
  download "$BEPINEX_BASE_URL/$asset" "$zip"
  if [ "$(sha256_of "$zip")" != "$sha" ]; then
    die "checksum mismatch for $asset (download corrupted or tampered). Nothing was changed."
  fi
  ok "checksum verified"

  extract="$TMP_DIR/bepinex"
  extract_zip "$zip" "$extract"

  if [ "$OPT_DRY_RUN" = "1" ]; then
    info "[dry-run] would install BepInEx core into $GAME_DIR/BepInEx and the loader next to the game"
    return 0
  fi

  # Back up a launcher script someone customized (gib, hand edits) before overwriting it.
  if [ -f "$GAME_DIR/run_bepinex.sh" ] && ! cmp -s "$GAME_DIR/run_bepinex.sh" "$extract/run_bepinex.sh"; then
    cp -p "$GAME_DIR/run_bepinex.sh" "$GAME_DIR/run_bepinex.sh.ftkmf-backup-$(timestamp)"
    note "previous run_bepinex.sh saved as run_bepinex.sh.ftkmf-backup-*"
  fi

  mkdir -p "$GAME_DIR/BepInEx/core" "$GAME_DIR/BepInEx/plugins" "$GAME_DIR/BepInEx/config" "$GAME_DIR/BepInEx/patchers"
  cp -R "$extract/BepInEx/core/." "$GAME_DIR/BepInEx/core/"
  for src in "$extract"/* "$extract"/.doorstop_version; do
    [ -e "$src" ] || continue
    [ "$(basename "$src")" = "BepInEx" ] && continue
    cp -R "$src" "$GAME_DIR/"
  done
  configure_run_script
  clear_quarantine
  state_set bepinex_version "$BEPINEX_VERSION"
  state_set bepinex_installed_by_us "${owned:-0}"
  ok "BepInEx $BEPINEX_VERSION installed ($(describe_build))."
}

# ---------------------------------------------------------------------------------------------------
# Framework plugin
# ---------------------------------------------------------------------------------------------------
framework_release_url() {
  if [ "$OPT_RELEASE" = "latest" ]; then
    printf 'https://github.com/%s/releases/latest/download/%s' "$FRAMEWORK_REPO" "$1"
  else
    printf 'https://github.com/%s/releases/download/%s/%s' "$FRAMEWORK_REPO" "$OPT_RELEASE" "$1"
  fi
}

# Puts the DLL to install at $FRAMEWORK_SRC (a path inside TMP_DIR or the user's file).
FRAMEWORK_SRC=""
FRAMEWORK_ORIGIN=""
obtain_framework() {
  local src status sums expected actual
  if [ -n "$OPT_FRAMEWORK" ]; then
    src="$OPT_FRAMEWORK"
    [ -e "$src" ] || die "--framework path does not exist: $src"
    case "$src" in
      *.zip)
        extract_zip "$src" "$TMP_DIR/fw"
        src="$(find "$TMP_DIR/fw" -name "$FRAMEWORK_DLL_NAME" -type f | head -n 1)"
        [ -n "$src" ] || die "no $FRAMEWORK_DLL_NAME inside $OPT_FRAMEWORK" ;;
    esac
    FRAMEWORK_SRC="$src"
    FRAMEWORK_ORIGIN="local: $OPT_FRAMEWORK"
  else
    info "downloading FTK Mod Framework ($OPT_RELEASE release)"
    status="$(http_status "$(framework_release_url "$FRAMEWORK_DLL_NAME")")"
    if [ "$status" = "404" ]; then
      die "no FTK Mod Framework release '$OPT_RELEASE' with a $FRAMEWORK_DLL_NAME asset at github.com/$FRAMEWORK_REPO/releases.
       Either the project has not published a release yet, or the tag is wrong.
       Builders can install a local build instead:  install.sh --framework path/to/$FRAMEWORK_DLL_NAME"
    fi
    download "$(framework_release_url "$FRAMEWORK_DLL_NAME")" "$TMP_DIR/$FRAMEWORK_DLL_NAME"
    FRAMEWORK_SRC="$TMP_DIR/$FRAMEWORK_DLL_NAME"
    FRAMEWORK_ORIGIN="github.com/$FRAMEWORK_REPO release $OPT_RELEASE"
    # Every release must include a valid checksum for the framework DLL.
    sums="$TMP_DIR/$FRAMEWORK_SUMS_NAME"
    if download "$(framework_release_url "$FRAMEWORK_SUMS_NAME")" "$sums" 2>/dev/null; then
      expected="$(awk -v n="$FRAMEWORK_DLL_NAME" '$2==n || $2=="*"n { print $1; exit }' "$sums")"
      if [ "${#expected}" = 64 ] && ! printf '%s' "$expected" | LC_ALL=C grep -q '[^0-9a-fA-F]'; then
        expected="$(printf '%s' "$expected" | tr 'A-F' 'a-f')"
        actual="$(sha256_of "$FRAMEWORK_SRC")"
        [ "$actual" = "$expected" ] || die "checksum mismatch for $FRAMEWORK_DLL_NAME (expected $expected, got $actual). Nothing was changed."
        ok "checksum verified"
      else
        die "$FRAMEWORK_SUMS_NAME has no valid checksum for $FRAMEWORK_DLL_NAME."
      fi
    else
      die "could not download $FRAMEWORK_SUMS_NAME; refusing an unverified framework DLL."
    fi
  fi
  case "$(magic_hex "$FRAMEWORK_SRC")" in
    4d5a*) ;;
    *) die "$FRAMEWORK_SRC is not a .NET assembly (bad download?)." ;;
  esac
}

# Marketplace executable is framework-owned and outside all scanned plugin roots.
HELPER_SRC=""
HELPER_NAME="ftkmf-launcher-helper"
obtain_helper() {
  local asset expected actual count sibling
  case "$BUILD" in
    mac) asset="ftkmf-helper-macos-universal" ;;
    proton) asset="ftkmf-helper-windows-amd64.exe"; HELPER_NAME="ftkmf-launcher-helper.exe" ;;
    linux)
      case "$(uname -m)" in
        x86_64) asset="ftkmf-helper-linux-amd64" ;;
        aarch64|arm64) asset="ftkmf-helper-linux-arm64" ;;
        *) die "No marketplace helper is available for this architecture." ;;
      esac ;;
  esac
  if [ -n "$OPT_HELPER" ]; then
    [ -n "$OPT_FRAMEWORK" ] || die "--helper requires --framework; release installs use verified release assets."
    [ -f "$OPT_HELPER" ] || die "--helper path does not exist: $OPT_HELPER"
    HELPER_SRC="$OPT_HELPER"
  elif [ -n "$OPT_FRAMEWORK" ]; then
    sibling="$(dirname "$FRAMEWORK_SRC")/$HELPER_NAME"
    [ ! -f "$sibling" ] || HELPER_SRC="$sibling"
    if [ -z "$HELPER_SRC" ]; then
      warn "Local DLL only: marketplace helper was not supplied. Installed mods remain available; use --helper or the launcher Install / Repair to enable downloads."
      return 0
    fi
  else
    count="$(awk -v n="$asset" '$2==n || $2=="*"n {c++} END {print c+0}' "$TMP_DIR/$FRAMEWORK_SUMS_NAME")"
    expected="$(awk -v n="$asset" '$2==n || $2=="*"n {print $1}' "$TMP_DIR/$FRAMEWORK_SUMS_NAME")"
    if [ "$count" != 1 ] || [ "${#expected}" != 64 ] || printf '%s' "$expected" | LC_ALL=C grep -q '[^0-9a-fA-F]'; then
      die "SHA256SUMS must contain exactly one valid checksum for $asset."
    fi
    HELPER_SRC="$TMP_DIR/$asset"
    download "$(framework_release_url "$asset")" "$HELPER_SRC"
    actual="$(sha256_of "$HELPER_SRC")"
    expected="$(printf '%s' "$expected" | tr 'A-F' 'a-f')"
    [ "$actual" = "$expected" ] || die "checksum mismatch for $asset; framework was not replaced."
  fi
  [ -s "$HELPER_SRC" ] || die "Marketplace helper is empty."
}

install_helper() {
  [ -n "$HELPER_SRC" ] || return 0
  local dest="$GAME_DIR/BepInEx/ftkmf" hash
  mkdir -p "$dest"
  hash="$(sha256_of "$HELPER_SRC")"
  cp "$HELPER_SRC" "$dest/$HELPER_NAME.new.$$"
  chmod 755 "$dest/$HELPER_NAME.new.$$"
  mv "$dest/$HELPER_NAME.new.$$" "$dest/$HELPER_NAME"
  printf '{"schemaVersion":1,"protocolVersion":1,"sha256":"%s"}\n' "$hash" > "$TMP_DIR/helper.json"
  cp "$TMP_DIR/helper.json" "$dest/helper.json.new.$$"
  mv "$dest/helper.json.new.$$" "$dest/helper.json"
  ok "marketplace helper installed (protocol 1)."
}

# Set Key = Value inside [Section] of a BepInEx .cfg, creating the section or file as needed.
cfg_set() {
  local file="$1" section="$2" key="$3" value="$4" tmp="$TMP_DIR/cfg.$$"
  [ "$OPT_DRY_RUN" = "1" ] && return 0
  mkdir -p "$(dirname "$file")"
  [ -f "$file" ] || printf '' > "$file"
  awk -v S="$section" -v K="$key" -v V="$value" '
    function emit_kv() { print K " = " V; done=1 }
    /^[ \t]*\[/ {
      if (insec && !done) emit_kv()
      insec = ($0 ~ "^[ \t]*\\[" S "\\][ \t]*$")
      print; next
    }
    insec && !done && $0 ~ "^[ \t]*" K "[ \t]*=" { emit_kv(); next }
    { print }
    END {
      if (!done) {
        if (!insec) print "[" S "]"
        emit_kv()
      }
    }' "$file" > "$tmp"
  replace_file "$file" "$tmp"
}

install_framework() {
  local plugins="$GAME_DIR/BepInEx/plugins" cfg="$GAME_DIR/BepInEx/config/$FRAMEWORK_PLUGIN_GUID.cfg"
  obtain_framework
  obtain_helper
  if [ "$OPT_DRY_RUN" = "1" ]; then
    info "[dry-run] would copy $FRAMEWORK_DLL_NAME ($FRAMEWORK_ORIGIN) to $plugins/"
    return 0
  fi
  install_helper
  mkdir -p "$plugins"
  cp "$FRAMEWORK_SRC" "$plugins/$FRAMEWORK_DLL_NAME"
  state_set framework_origin "$FRAMEWORK_ORIGIN"
  state_set framework_sha256 "$(sha256_of "$plugins/$FRAMEWORK_DLL_NAME")"
  ok "FTK Mod Framework installed ($FRAMEWORK_ORIGIN)."
  if [ "$OPT_DEV" = "1" ]; then
    cfg_set "$cfg" "Diagnostics" "RunSelfTests" "true"
    ok "developer mode: Diagnostics/RunSelfTests = true in $(basename "$cfg")."
  else
    cfg_set "$cfg" "Diagnostics" "RunSelfTests" "false"
    cfg_set "$cfg" "Diagnostics" "EnableScaleBudgetGate" "false"
    cfg_set "$cfg" "Diagnostics" "SyntheticContentCount" "0"
    cfg_set "$cfg" "Enemies" "ForceCustomEnemy" "false"
    cfg_set "$cfg" "Adventures" "ForceCustomEncounter" "false"
    ok "player mode: developer fixtures, scale probes, and forced encounters/enemies are disabled."
  fi
}

# ---------------------------------------------------------------------------------------------------
# Steam launch options (localconfig.vdf)
# ---------------------------------------------------------------------------------------------------
launch_option_value() {
  case "$BUILD" in
    mac|linux) printf '"%s/run_bepinex.sh" %%command%%' "$GAME_DIR" ;;
    proton)    printf 'WINEDLLOVERRIDES="winhttp=n,b" %%command%%' ;;
  esac
}

# Does an existing launch option already run BepInEx?
launch_option_is_bepinex() {
  case "$1" in
    *run_bepinex.sh*|*winhttp=n,b*|*winhttp=n*) return 0 ;;
    *) return 1 ;;
  esac
}

# Merge our wrapper into whatever the user already had (their options usually follow %command%).
compose_launch_option() {
  local ours="$1" existing="$2"
  [ -z "$existing" ] && { printf '%s' "$ours"; return 0; }
  case "$existing" in
    *%command%*)
      case "$BUILD" in
        proton) printf 'WINEDLLOVERRIDES="winhttp=n,b" %s' "$existing" ;;
        *)      printf '%s' "$existing" | sed -e "s|%command%|\"$GAME_DIR/run_bepinex.sh\" %command%|" ;;
      esac ;;
    *) printf '%s %s' "$ours" "$existing" ;;
  esac
}

# awk program shared by get/set/delete of ...Software/Valve/Steam/apps/<app>/LaunchOptions.
# The file is one token or token-pair per line, tab-indented (that is how Steam writes it).
# shellcheck disable=SC2016  # awk source, not shell: nothing here should expand
VDF_AWK='
function ind(n,  s, i) { s=""; for (i=0; i<n; i++) s=s "\t"; return s }
function in_apps() { return depth>=5 && path[1]=="userlocalconfigstore" && path[2]=="software" && path[3]=="valve" && path[4]=="steam" && path[5]=="apps" }
function in_app()  { return depth>=6 && in_apps() && path[6]==app }
function emit_kv(n) { print ind(n) "\"LaunchOptions\"\t\t\"" value "\""; done=1 }
BEGIN { depth=0; done=0; pending=""; app=tolower(app); value=ENVIRON["FTKMF_VDF_VALUE"] }
{
  line=$0; t=line
  sub(/^[ \t]+/, "", t); sub(/[ \t\r]+$/, "", t)
  if (t == "{") { depth++; path[depth]=tolower(pending); pending=""; if (mode!="get") print line; next }
  if (t == "}") {
    if (mode=="set" && !done && depth==6 && in_app()) emit_kv(depth)
    if (mode=="set" && !done && depth==5 && in_apps() && !appseen) {
      print ind(5) "\"" app "\""; print ind(5) "{"; emit_kv(6); print ind(5) "}"
    }
    if (depth==6 && in_app()) appseen=1
    delete path[depth]; depth--
    if (mode!="get") print line
    next
  }
  if (match(t, /^"([^"\\]|\\.)*"$/)) {
    pending=substr(t, 2, RLENGTH-2)
    if (mode!="get") print line
    next
  }
  if (match(t, /^"([^"\\]|\\.)*"[ \t]+"/)) {
    key=t; match(key, /^"([^"\\]|\\.)*"/); key=substr(key, 2, RLENGTH-2)
    if (in_app() && depth==6 && tolower(key)=="launchoptions") {
      if (mode=="get") { v=t; sub(/^"([^"\\]|\\.)*"[ \t]+"/, "", v); sub(/"$/, "", v); print v; exit }
      if (mode=="set") { if (!done) emit_kv(depth); next }
      if (mode=="delete") { next }
    }
    if (mode!="get") print line
    next
  }
  if (mode!="get") print line
}
END { if (mode=="set" && !done) exit 3 }
'

vdf_get_launch_options() {   # $1 = localconfig.vdf  -> prints the raw (escaped) value
  awk -v mode=get -v app="$STEAM_APP_ID" "$VDF_AWK" "$1"
}

vdf_write_launch_options() { # $1 = localconfig.vdf, $2 = mode (set|delete), $3 = raw escaped value
  local file="$1" mode="$2" tmp="$TMP_DIR/localconfig.$$" rc=0
  FTKMF_VDF_VALUE="${3:-}" awk -v mode="$mode" -v app="$STEAM_APP_ID" "$VDF_AWK" "$file" > "$tmp" || rc=$?
  if [ "$rc" = "3" ]; then
    warn "$file has no Software/Valve/Steam/apps block; cannot write the launch option there."
    return 1
  fi
  [ "$rc" = "0" ] || return 1
  # Sanity: brace balance must be unchanged and the file must not have shrunk unexpectedly.
  if [ "$(grep -c '^[[:space:]]*{' "$file")" != "$(grep -c '^[[:space:]]*{' "$tmp")" ] && [ "$mode" != "set" ]; then
    warn "unexpected structure change while editing $file; leaving it alone."
    return 1
  fi
  [ "$OPT_DRY_RUN" = "1" ] && return 0
  cp -p "$file" "$file.ftkmf-backup"
  replace_file "$file" "$tmp"
}

steam_running() {
  if [ "$OS" = "macos" ]; then pgrep -x steam_osx >/dev/null 2>&1
  else pgrep -x steam >/dev/null 2>&1
  fi
}

game_running() {
  if [ "$OS" = "macos" ]; then pgrep -x FTK >/dev/null 2>&1
  else pgrep -f '[/]FTK\.exe' >/dev/null 2>&1
  fi
}

steam_quit() {
  if [ "$OS" = "macos" ]; then
    osascript -e 'tell application "Steam" to quit' >/dev/null 2>&1 || true
  elif [ "$STEAM_IS_FLATPAK" = "1" ] && have flatpak; then
    flatpak run com.valvesoftware.Steam -shutdown >/dev/null 2>&1 || true
  elif have steam; then
    steam -shutdown >/dev/null 2>&1 || true
  else
    return 1
  fi
  local i=0
  while steam_running; do
    i=$((i + 1)); [ "$i" -gt 60 ] && return 1
    sleep 1
  done
  return 0
}

steam_start() {
  if [ "$OS" = "macos" ]; then open -a Steam >/dev/null 2>&1 || true
  elif [ "$STEAM_IS_FLATPAK" = "1" ] && have flatpak; then (nohup flatpak run com.valvesoftware.Steam >/dev/null 2>&1 &)
  elif have steam; then (nohup steam >/dev/null 2>&1 &)
  fi
}

# All localconfig.vdf files under every Steam root (one per Steam account on this machine).
localconfig_files() {
  local root f
  printf '%s' "$STEAM_ROOTS" | while IFS= read -r root; do
    [ -n "$root" ] || continue
    for f in "$root"/userdata/*/config/localconfig.vdf; do
      [ -f "$f" ] && printf '%s\n' "$f"
    done
  done
}

account_of() { printf '%s' "$1" | sed -e 's|.*/userdata/\([^/]*\)/config/localconfig.vdf|\1|'; }

print_manual_launch_option() {
  say ""
  say "  Set this launch option yourself: Steam > Library > For The King > Manage (gear) > Properties"
  say "  > General > Launch Options, and paste exactly:"
  say ""
  say "      $(launch_option_value)"
  say ""
}

configure_launch_options() {
  local ours files f acct current composed wrote=0 skipped=0 need_write=0
  ours="$(launch_option_value)"
  [ -n "$STEAM_ROOTS" ] || find_steam_roots

  if [ "$OPT_NO_LAUNCH" = "1" ]; then
    info "skipping Steam launch options (--no-launch-options)."
    print_manual_launch_option
    return 0
  fi

  files="$(localconfig_files)"
  if [ -z "$files" ]; then
    warn "no Steam account data found (userdata/*/config/localconfig.vdf); cannot set the launch option."
    print_manual_launch_option
    return 0
  fi

  # First pass: is there anything to do?
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    current="$(vdf_unescape "$(vdf_get_launch_options "$f")")"
    if launch_option_is_bepinex "$current"; then skipped=$((skipped + 1)); else need_write=1; fi
  done <<EOF
$files
EOF
  if [ "$need_write" = "0" ]; then
    ok "Steam launch option already runs BepInEx for every account ($skipped); left unchanged."
    return 0
  fi

  if steam_running; then
    if game_running; then
      warn "For The King is running; close it first. Skipping the launch option for now."
      print_manual_launch_option
      return 0
    fi
    info "Steam is running. Steam only reads launch options at startup and overwrites them on exit,"
    info "so it has to be closed for a moment while the setting is written."
    if ask_yes_no "Close Steam now, write the launch option, and reopen Steam?" y; then
      if [ "$OPT_DRY_RUN" = "1" ]; then
        info "[dry-run] would close Steam, edit localconfig.vdf, and reopen Steam"
      else
        info "closing Steam..."
        if ! steam_quit; then
          warn "Steam did not close in time."
          print_manual_launch_option
          return 0
        fi
        STEAM_RESTART=1
      fi
    else
      print_manual_launch_option
      return 0
    fi
  fi

  while IFS= read -r f; do
    [ -n "$f" ] || continue
    acct="$(account_of "$f")"
    current="$(vdf_unescape "$(vdf_get_launch_options "$f")")"
    if launch_option_is_bepinex "$current"; then
      note "account $acct: launch option already runs BepInEx; unchanged."
      continue
    fi
    composed="$(compose_launch_option "$ours" "$current")"
    if [ "$OPT_DRY_RUN" = "1" ]; then
      info "[dry-run] account $acct: would set launch option to: $composed"
      continue
    fi
    if vdf_write_launch_options "$f" set "$(vdf_escape "$composed")"; then
      state_set "launch_backup_$acct" "$current"
      state_set "launch_set_$acct" "$composed"
      ok "account $acct: launch option set to: $composed"
      wrote=$((wrote + 1))
    else
      warn "account $acct: could not write the launch option."
      print_manual_launch_option
    fi
  done <<EOF
$files
EOF

  if [ "${STEAM_RESTART:-0}" = "1" ]; then
    info "reopening Steam..."
    steam_start
  fi
}

restore_launch_options() {
  local files f acct current backup
  [ -n "$STEAM_ROOTS" ] || find_steam_roots
  files="$(localconfig_files)"
  [ -n "$files" ] || return 0
  local any=0
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    acct="$(account_of "$f")"
    [ -n "$(state_get "launch_set_$acct")" ] || continue
    any=1
  done <<EOF
$files
EOF
  [ "$any" = "1" ] || { note "no launch options were set by this tool; nothing to restore."; return 0; }

  if steam_running && [ "$OPT_DRY_RUN" != "1" ]; then
    if ask_yes_no "Steam is running; close it to restore the launch option, then reopen it?" y; then
      steam_quit || { warn "Steam did not close; launch option left as is."; return 0; }
      STEAM_RESTART=1
    else
      warn "launch option left as is (remove it in Steam > Properties > Launch Options)."
      return 0
    fi
  fi

  while IFS= read -r f; do
    [ -n "$f" ] || continue
    acct="$(account_of "$f")"
    [ -n "$(state_get "launch_set_$acct")" ] || continue
    current="$(vdf_unescape "$(vdf_get_launch_options "$f")")"
    if [ "$current" != "$(state_get "launch_set_$acct")" ]; then
      note "account $acct: launch option was changed since we set it; leaving it alone."
      continue
    fi
    backup="$(state_get "launch_backup_$acct")"
    if [ "$OPT_DRY_RUN" = "1" ]; then
      info "[dry-run] account $acct: would restore launch option to: '${backup}'"
      continue
    fi
    if [ -n "$backup" ]; then
      vdf_write_launch_options "$f" set "$(vdf_escape "$backup")" && ok "account $acct: launch option restored to: $backup"
    else
      vdf_write_launch_options "$f" delete "" && ok "account $acct: launch option removed."
    fi
    state_del "launch_set_$acct"; state_del "launch_backup_$acct"
  done <<EOF
$files
EOF
  if [ "${STEAM_RESTART:-0}" = "1" ]; then info "reopening Steam..."; steam_start; fi
}

# ---------------------------------------------------------------------------------------------------
# Status / verify
# ---------------------------------------------------------------------------------------------------
log_summary() {
  local log="$GAME_DIR/BepInEx/LogOutput.log" pass fail loaded
  [ -f "$log" ] || { note "no BepInEx/LogOutput.log yet (the game has not been launched with BepInEx)."; return 0; }
  loaded="$(grep -c 'FTK Mod Framework .* loaded' "$log" 2>/dev/null || true)"
  pass="$(grep -c 'SELF-TEST PASS' "$log" 2>/dev/null || true)"
  fail="$(grep -c 'SELF-TEST FAIL' "$log" 2>/dev/null || true)"
  info "last game log: $log"
  if [ "${loaded:-0}" -gt 0 ]; then ok "framework loaded on the last launch; SELF-TEST PASS: $pass, FAIL: $fail"
  else warn "the last launch did not load the framework (check the launch option, then relaunch)."; fi
}

show_status() {
  local files f acct current origin
  step "For The King mod status"
  info "game folder: $GAME_DIR"
  info "build: $(describe_build)"
  if bepinex_present; then
    if bepinex_launcher_ok; then ok "BepInEx: $(installed_bepinex_version)"; else warn "BepInEx present but its loader files for this build are missing"; fi
  else info "BepInEx: not installed"; fi
  if [ -f "$GAME_DIR/BepInEx/plugins/$FRAMEWORK_DLL_NAME" ]; then
    origin="$(state_get framework_origin)"
    [ -n "$origin" ] || origin="not installed by this tool"
    ok "FTK Mod Framework: installed ($origin)"
  else
    info "FTK Mod Framework: not installed"
  fi
  [ -n "$STEAM_ROOTS" ] || find_steam_roots
  files="$(localconfig_files)"
  if [ -z "$files" ]; then info "Steam launch option: (no Steam account data found)"; else
    while IFS= read -r f; do
      [ -n "$f" ] || continue
      acct="$(account_of "$f")"
      current="$(vdf_unescape "$(vdf_get_launch_options "$f")")"
      if launch_option_is_bepinex "$current"; then ok "Steam account $acct launch option: $current"
      elif [ -n "$current" ]; then warn "Steam account $acct launch option does not run BepInEx: $current"
      else info "Steam account $acct launch option: (none)"; fi
    done <<EOF
$files
EOF
  fi
  log_summary
}

# ---------------------------------------------------------------------------------------------------
# Uninstall
# ---------------------------------------------------------------------------------------------------
do_uninstall() {
  local p
  step "Removing FTK Mod Framework from $GAME_DIR"
  for p in "$GAME_DIR/BepInEx/plugins/$FRAMEWORK_DLL_NAME" \
           "$GAME_DIR/BepInEx/plugins/FTKModFramework_content" \
           "$GAME_DIR/BepInEx/config/$FRAMEWORK_PLUGIN_GUID.cfg"; do
    if [ -e "$p" ]; then
      if [ "$OPT_DRY_RUN" = "1" ]; then info "[dry-run] would remove $p"; else rm -rf "$p"; ok "removed $(basename "$p")"; fi
    fi
  done
  state_del framework_origin; state_del framework_sha256
  restore_launch_options
  if [ "$OPT_PURGE" = "1" ]; then
    if [ "$(state_get bepinex_installed_by_us)" = "1" ]; then
      for p in "$GAME_DIR/BepInEx" "$GAME_DIR/run_bepinex.sh" "$GAME_DIR/libdoorstop.dylib" "$GAME_DIR/libdoorstop.so" \
               "$GAME_DIR/.doorstop_version" "$GAME_DIR/changelog.txt" "$GAME_DIR/winhttp.dll" "$GAME_DIR/doorstop_config.ini"; do
        [ -e "$p" ] || continue
        if [ "$OPT_DRY_RUN" = "1" ]; then info "[dry-run] would remove $p"; else rm -rf "$p"; ok "removed $(basename "$p")"; fi
      done
      ok "BepInEx removed."
    else
      warn "BepInEx was not installed by this tool; leaving it in place (remove <game>/BepInEx yourself if you want it gone)."
    fi
  else
    note "BepInEx left in place (add --purge to remove it too)."
  fi
  say ""
  ok "Done. For The King is back to running unmodded."
}

# ---------------------------------------------------------------------------------------------------
# Install
# ---------------------------------------------------------------------------------------------------
do_install() {
  if [ "$OPT_DRY_RUN" != 1 ] && game_running; then
    die "Close For The King before installing or repairing the framework."
  fi
  step "Installing the mod loader (BepInEx)"
  check_codesign
  install_bepinex

  step "Installing the FTK Mod Framework"
  install_framework

  step "Configuring Steam"
  configure_launch_options

  [ "$OPT_DRY_RUN" = "1" ] || { state_set installer_version "$INSTALLER_VERSION"; state_set build "$BUILD"; state_set game_dir "$GAME_DIR"; }

  say ""
  if [ "$OPT_DRY_RUN" = "1" ]; then
    say "${C_BOLD}Dry run finished; nothing was changed.${C_OFF}"
    return 0
  fi
  say "${C_GREEN}${C_BOLD}All set.${C_OFF} Launch For The King from Steam as usual."
  say ""
  say "  What you get on the title screen: a new ${C_BOLD}Mods${C_OFF} button (toggle mods on and off)."
  say "  At character select: the ${C_BOLD}Thief${C_OFF} and ${C_BOLD}Innkeeper${C_OFF} classes."
  say "  In the adventure list: ${C_BOLD}Smuggler's Run${C_OFF}."
  say "  Drop other content mods into: $GAME_DIR/BepInEx/plugins/"
  say ""
  say "  Log (for troubleshooting): $GAME_DIR/BepInEx/LogOutput.log"
  say "  Check anytime with:  install.sh --status      Remove with:  install.sh --uninstall"
  case "$BUILD" in
    proton) say ""; note "Proton build: the launch option tells Wine to load BepInEx's winhttp.dll. If a Proton update
  ever breaks it, run install.sh --status and check the log path above." ;;
  esac
}

# ---------------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------------
main() {
  if [ "$OPT_ACTION" = "launcher-status" ]; then
    detect_platform
    find_game_dir
    detect_build
    case "$GAME_DIR" in *$'\n'*|*$'\r'*) die "Game folder contains unsupported line breaks." ;; esac
    printf '%s\n%s\n' "$GAME_DIR" "$BUILD"
    if [ -f "$GAME_DIR/BepInEx/plugins/$FRAMEWORK_DLL_NAME" ]; then printf 'installed\n'; else printf 'missing\n'; fi
    return
  fi
  say "${C_BOLD}FTK Mod Framework installer ${INSTALLER_VERSION}${C_OFF}"
  detect_platform
  step "Finding For The King"
  find_game_dir
  detect_build
  ok "game folder: $GAME_DIR"
  ok "build: $(describe_build)"
  if [ "$BUILD" = "mac" ] && [ "$(uname -m)" = "arm64" ]; then
    note "Apple Silicon: the game runs under Rosetta; BepInEx $BEPINEX_VERSION supports that."
  fi

  case "$OPT_ACTION" in
    status)    show_status ;;
    uninstall) do_uninstall ;;
    install)   do_install ;;
  esac
}

main
