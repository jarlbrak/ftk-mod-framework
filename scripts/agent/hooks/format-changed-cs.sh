#!/usr/bin/env bash
# PostToolUse hook: format C# files an agent just wrote, so style never becomes
# review noise. Skips silently when the SDK is absent, which is the normal state
# of a docs-only or launcher-only clone.
set -u

command -v dotnet >/dev/null 2>&1 || exit 0

for path in ${CLAUDE_FILE_PATHS:-}; do
  case "$path" in
    *.cs) dotnet format --include "$path" >/dev/null 2>&1 || true ;;
  esac
done
exit 0
