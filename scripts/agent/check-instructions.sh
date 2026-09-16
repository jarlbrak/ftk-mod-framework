#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

fail=0
report_failure() {
  echo "ERROR: $1" >&2
  fail=1
}

required=(
  AGENTS.md
  CLAUDE.md
  docs/AI-NATIVE.md
  .agents/skills/verify-change/SKILL.md
  .agents/skills/decompile-lookup/SKILL.md
  .agents/skills/ingame-smoke/SKILL.md
  .agents/skills/docs-audit/SKILL.md
  .agents/skills/ftk-release/SKILL.md
  .agents/skills/ftk-custom-models/SKILL.md
  .agents/roles/ftk-architect.md
  .agents/roles/csharp-harmony-engineer.md
  .agents/roles/game-decompile-analyst.md
  .agents/roles/content-author.md
  .agents/roles/game-designer.md
)

for path in "${required[@]}"; do
  if [[ ! -f "$path" ]]; then
    report_failure "missing canonical instruction file: $path"
  fi
done

if ! grep -q '^@AGENTS\.md$' CLAUDE.md; then
  report_failure "CLAUDE.md does not import the canonical AGENTS.md"
fi

while IFS= read -r path; do
  [[ -n "$path" ]] || continue
  if grep -nE '/Users/|/home/|[A-Za-z]:\\Users\\|Bearer[[:space:]]+[A-Za-z0-9._-]+|API_KEY[[:space:]]*=[[:space:]]*[^$<]' "$path" >/dev/null; then
    report_failure "private path or secret-shaped literal in public agent file: $path"
  fi
done < <(
  git ls-files --cached --others --exclude-standard |
    grep -E '(^|/)AGENTS\.md$|^CLAUDE\.md$|^docs/AI-NATIVE\.md$|^\.agents/|^\.claude/|^\.codex/' |
    sort
)

if grep -R -nE '\.claude/|\.codex/|CLAUDE\.md' AGENTS.md .agents >/dev/null; then
  report_failure "generic instructions depend on a harness adapter layer"
fi

role_names=(ftk-architect csharp-harmony-engineer game-decompile-analyst content-author game-designer)
for name in "${role_names[@]}"; do
  if [[ ! -f ".agents/roles/$name.md" ]]; then
    report_failure "missing canonical role: $name"
    continue
  fi
  [[ -f ".claude/agents/$name.md" ]] || report_failure "missing Claude role adapter: $name"
  [[ -f ".codex/agents/$name.toml" ]] || report_failure "missing Codex role adapter: $name"
  grep -qF ".agents/roles/$name.md" ".claude/agents/$name.md" || report_failure "Claude role adapter does not target canonical role: $name"
  grep -qF ".agents/roles/$name.md" ".codex/agents/$name.toml" || report_failure "Codex role adapter does not target canonical role: $name"
  [[ "$(wc -l < ".claude/agents/$name.md")" -le 30 ]] || report_failure "Claude role adapter is no longer thin: $name"
  [[ "$(wc -l < ".codex/agents/$name.toml")" -le 12 ]] || report_failure "Codex role adapter is no longer thin: $name"
done

skill_names=(
  verify-change decompile-lookup ingame-smoke docs-audit ftk-release ftk-custom-models
  create-epic create-spec create-workitem complete-workitems validate-spec validate-epic
  complexity-review compare-approaches
)
for name in "${skill_names[@]}"; do
  if [[ ! -f ".agents/skills/$name/SKILL.md" ]]; then
    report_failure "missing canonical skill: $name"
    continue
  fi
  [[ -f ".claude/skills/$name/SKILL.md" ]] || report_failure "missing Claude skill adapter: $name"
  grep -qF ".agents/skills/$name/SKILL.md" ".claude/skills/$name/SKILL.md" || report_failure "Claude skill adapter does not target canonical skill: $name"
  [[ "$(wc -l < ".claude/skills/$name/SKILL.md")" -le 15 ]] || report_failure "Claude skill adapter is no longer thin: $name"
done

local_paths=(AGENTS.local.md AGENTS.override.md CLAUDE.local.md .local/agents/.ignore-probe .claude/settings.local.json .codex/config.local.toml)
for path in "${local_paths[@]}"; do
  if ! git check-ignore -q "$path"; then
    report_failure "documented local path is not ignored: $path"
  fi
done

local_only_pattern='(^|/)\.local/|(^|/)(AGENTS\.local\.md|AGENTS\.override\.md|CLAUDE\.local\.md|settings\.local\.json|config\.local\.toml)$'
if ! printf '%s\n' '.local/agents/leak.md' | grep -Eq "$local_only_pattern"; then
  report_failure "internal local-only path classifier does not reject .local descendants"
fi
if git ls-files | grep -Eq "$local_only_pattern"; then
  report_failure "a local-only agent file is tracked"
fi

if [[ "$fail" -ne 0 ]]; then
  exit 1
fi

echo "Agent instruction graph OK"
