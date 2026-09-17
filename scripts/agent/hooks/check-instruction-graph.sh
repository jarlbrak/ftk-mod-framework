#!/usr/bin/env bash
# PostToolUse hook: revalidate the agent instruction graph as soon as an agent
# edits part of it, instead of leaving the failure for CI. Reports through
# stderr with exit 2 so the harness feeds the diagnosis back to the agent.
set -u

repo_root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"
checker="$repo_root/scripts/agent/check-instructions.sh"
[ -x "$checker" ] || exit 0

touched=0
for path in ${CLAUDE_FILE_PATHS:-}; do
  case "$path" in
    *AGENTS.md|*/CLAUDE.md|CLAUDE.md|*/.agents/*|*/.claude/*|*/.codex/*|*docs/AI-NATIVE.md|*.gitignore)
      touched=1 ;;
  esac
done
[ "$touched" -eq 1 ] || exit 0

if ! output="$(bash "$checker" 2>&1)"; then
  printf '%s\n' "$output" >&2
  echo "The agent instruction graph is now invalid. Fix it before continuing." >&2
  exit 2
fi
exit 0
