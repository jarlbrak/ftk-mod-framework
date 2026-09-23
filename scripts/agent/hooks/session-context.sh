#!/usr/bin/env bash
# SessionStart hook: give an agent the smallest useful orientation for this
# repository. Portable across harnesses and clean clones. Every step is
# optional and the hook always succeeds, because a missing tool or a detached
# checkout must not block a session.
set -u

repo_root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"

echo "=== FTK context ==="
git -C "$repo_root" log --oneline -3 2>/dev/null | sed 's/^/Recent: /'
git -C "$repo_root" status --short 2>/dev/null | head -5 | sed 's/^/Status: /'

if command -v gh >/dev/null 2>&1; then
  echo "--- Open issues:"
  gh issue list --state open -L 5 2>/dev/null | sed 's/^/Issue: /'
fi

echo "--- Read AGENTS.md, the nearest nested AGENTS.md, and docs/README.md before starting."
exit 0
