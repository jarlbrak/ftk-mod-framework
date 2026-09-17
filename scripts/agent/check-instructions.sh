#!/usr/bin/env bash
# Validate the committed agent instruction graph.
#
# The checks are derived from the filesystem, not from a hand-maintained list,
# so adding a skill or a role without its adapters fails here instead of
# silently leaving one harness blind.
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

fail=0
report_failure() {
  echo "ERROR: $1" >&2
  fail=1
}

# --- Canonical entry points -------------------------------------------------

required=(
  AGENTS.md
  CLAUDE.md
  docs/AI-NATIVE.md
  .codex/README.md
  .claude/settings.json
  scripts/agent/check-instructions.sh
)
for path in "${required[@]}"; do
  [[ -f "$path" ]] || report_failure "missing canonical instruction file: $path"
done

if ! grep -q '^@AGENTS\.md$' CLAUDE.md; then
  report_failure "CLAUDE.md does not import the canonical AGENTS.md"
fi

# --- Subtree coverage -------------------------------------------------------
#
# Every top-level directory that carries tracked work an agent may edit needs
# instructions of its own. Data-only and adapter-only trees are exempt because
# the root file already governs them.

# Trees exempt from the rule. Adapter layers carry no policy of their own, and the asset tree is
# binary payload the root file already governs. Everything else must declare its own rules, so a
# newly added top-level tree fails here until someone writes them.
exempt_subtrees=(.claude .codex assets)

while IFS= read -r dir; do
  [[ -n "$dir" ]] || continue
  skip=0
  for exempt in "${exempt_subtrees[@]}"; do
    [[ "$dir" == "$exempt" ]] && skip=1
  done
  [[ "$skip" -eq 1 ]] && continue
  [[ -f "$dir/AGENTS.md" ]] ||
    report_failure "subtree has tracked work but no instructions: $dir/AGENTS.md"
done < <(git ls-files | awk -F/ 'NF > 1 { print $1 }' | sort -u)

# Nested trees that need rules of their own beyond their parent's.
nested_subtrees=(
  FTKModFramework/Core FTKModFramework/Content FTKModFramework/Agent tools/ai-model-pipeline
)
for dir in "${nested_subtrees[@]}"; do
  [[ -d "$dir" ]] || continue
  [[ -f "$dir/AGENTS.md" ]] ||
    report_failure "subtree has tracked work but no instructions: $dir/AGENTS.md"
done

while IFS= read -r path; do
  [[ -n "$path" ]] || continue
  grep -qE 'root.*AGENTS\.md' "$path" ||
    report_failure "nested instructions do not defer to the root file: $path"
done < <(git ls-files '*/AGENTS.md')

# --- Skills: canonical workflow plus a thin Claude adapter ------------------

while IFS= read -r skill_dir; do
  name="$(basename "$skill_dir")"
  canonical="$skill_dir/SKILL.md"
  adapter=".claude/skills/$name/SKILL.md"

  head -1 "$canonical" | grep -q '^---$' ||
    report_failure "canonical skill has no frontmatter: $canonical"
  grep -q "^name: $name\$" "$canonical" ||
    report_failure "canonical skill name does not match its directory: $canonical"
  grep -q '^description: .' "$canonical" ||
    report_failure "canonical skill has no description: $canonical"

  if [[ ! -f "$adapter" ]]; then
    report_failure "missing Claude skill adapter: $name"
    continue
  fi
  grep -qF ".agents/skills/$name/SKILL.md" "$adapter" ||
    report_failure "Claude skill adapter does not target the canonical skill: $name"
  grep -q "^name: $name\$" "$adapter" ||
    report_failure "Claude skill adapter name does not match its directory: $adapter"
  [[ "$(wc -l < "$adapter")" -le 15 ]] ||
    report_failure "Claude skill adapter is no longer thin: $name"

  # A tiered skill must say so in its adapter. The harness loads the adapter, and
  # its own directory holds no references, so an adapter that stays silent invites
  # the reader to pull the whole body or resolve reference paths against itself.
  if [[ -d "$skill_dir/references" ]] || [[ -d "skills/$name/references" ]]; then
    grep -qF 'references/' "$adapter" ||
      report_failure "skill has a references tier but its Claude adapter never mentions it: $adapter"
  fi
done < <(find .agents/skills -mindepth 1 -maxdepth 1 -type d | sort)

while IFS= read -r adapter_dir; do
  name="$(basename "$adapter_dir")"
  [[ -d ".agents/skills/$name" ]] ||
    report_failure "Claude skill adapter has no canonical skill: $name"
done < <(find .claude/skills -mindepth 1 -maxdepth 1 -type d | sort)

# --- Progressive disclosure -------------------------------------------------
#
# A skill body is the always-loaded tier. Once it grows past what a reader needs
# on every invocation, the detail belongs in a `references/` tier loaded per step.
# Every reference must be reachable from its entry point, or nothing will read it.

disclosure_limit=400

while IFS= read -r skill; do
  dir="$(dirname "$skill")"
  name="$(basename "$dir")"
  refs="$dir/references"
  body_lines="$(wc -l < "$skill" | tr -d "[:space:]")"

  if [[ ! -d "$refs" ]]; then
    [[ "$body_lines" -le "$disclosure_limit" ]] ||
      report_failure "skill body is $body_lines lines with no references/ tier: $skill"
    continue
  fi

  while IFS= read -r ref; do
    [[ -n "$ref" ]] || continue
    grep -qF "references/$(basename "$ref")" "$skill" ||
      report_failure "reference is unreachable from its skill entry point: $ref"
  done < <(find "$refs" -maxdepth 1 -name '*.md' | sort)

  [[ "$body_lines" -le "$disclosure_limit" ]] ||
    report_failure "skill body is $body_lines lines despite a references/ tier: $skill"
done < <(git ls-files --cached --others --exclude-standard |
  grep -E '(^|/)SKILL\.md$' | grep -v '^\.claude/' | sort -u)

# Relative links inside the instruction graph must resolve.
while IFS= read -r doc; do
  [[ -f "$doc" ]] || continue
  while IFS= read -r target; do
    [[ -n "$target" ]] || continue
    [[ -e "$(dirname "$doc")/$target" ]] ||
      report_failure "broken relative link in $doc: $target"
  done < <(grep -oE '\]\([^)#: ]+\.md\)' "$doc" | sed 's/^](//; s/)$//')
done < <(git ls-files --cached --others --exclude-standard |
  grep -E '(^|/)SKILL\.md$|/references/[^/]+\.md$' | grep -v '^\.claude/' | sort -u)

# --- Roles: canonical contract plus thin Claude and Codex adapters ----------

while IFS= read -r canonical; do
  name="$(basename "$canonical" .md)"
  claude_adapter=".claude/agents/$name.md"
  codex_adapter=".codex/agents/$name.toml"

  if [[ -f "$claude_adapter" ]]; then
    grep -qF ".agents/roles/$name.md" "$claude_adapter" ||
      report_failure "Claude role adapter does not target the canonical role: $name"
    grep -q "^name: $name\$" "$claude_adapter" ||
      report_failure "Claude role adapter name does not match its file: $claude_adapter"
    [[ "$(wc -l < "$claude_adapter")" -le 30 ]] ||
      report_failure "Claude role adapter is no longer thin: $name"
  else
    report_failure "missing Claude role adapter: $name"
  fi

  if [[ -f "$codex_adapter" ]]; then
    grep -qF ".agents/roles/$name.md" "$codex_adapter" ||
      report_failure "Codex role adapter does not target the canonical role: $name"
    [[ "$(wc -l < "$codex_adapter")" -le 12 ]] ||
      report_failure "Codex role adapter is no longer thin: $name"
  else
    report_failure "missing Codex role adapter: $name"
  fi
done < <(find .agents/roles -maxdepth 1 -name '*.md' | sort)

while IFS= read -r adapter; do
  name="$(basename "$adapter" .md)"
  [[ -f ".agents/roles/$name.md" ]] ||
    report_failure "Claude role adapter has no canonical role: $name"
done < <(git ls-files '.claude/agents/*.md')

while IFS= read -r adapter; do
  name="$(basename "$adapter" .toml)"
  [[ -f ".agents/roles/$name.md" ]] ||
    report_failure "Codex role adapter has no canonical role: $name"
done < <(git ls-files '.codex/agents/*.toml')

# --- Shared harness settings ------------------------------------------------

if [[ -f .claude/settings.json ]]; then
  python3 -c 'import json,sys; json.load(open(".claude/settings.json"))' 2>/dev/null ||
    report_failure ".claude/settings.json is not valid JSON"
  while IFS= read -r script; do
    [[ -n "$script" ]] || continue
    if [[ ! -x "$script" ]]; then
      report_failure "shared settings reference a missing or non-executable hook: $script"
    elif git check-ignore -q "$script"; then
      report_failure "shared settings reference an ignored hook script: $script"
    fi
  done < <(grep -o 'scripts/agent/hooks/[A-Za-z0-9._-]*\.sh' .claude/settings.json | sort -u)
fi

# --- Boundaries -------------------------------------------------------------

while IFS= read -r path; do
  [[ -f "$path" ]] || continue
  if grep -nE '/Users/|/home/|[A-Za-z]:\\Users\\|Bearer[[:space:]]+[A-Za-z0-9._-]+|API_KEY[[:space:]]*=[[:space:]]*[^$<]' "$path" >/dev/null; then
    report_failure "private path or secret-shaped literal in public agent file: $path"
  fi
done < <(
  git ls-files --cached --others --exclude-standard |
    grep -E '(^|/)AGENTS\.md$|^CLAUDE\.md$|^docs/AI-NATIVE\.md$|^\.agents/|^\.claude/|^\.codex/|^scripts/agent/' |
    grep -v '^scripts/agent/check-instructions\.sh$' |
    sort
)

if grep -R -nE '\.claude/|\.codex/|CLAUDE\.md' AGENTS.md .agents >/dev/null; then
  report_failure "generic instructions depend on a harness adapter layer"
fi

# --- Local overlay stays local ---------------------------------------------

local_paths=(
  AGENTS.local.md AGENTS.override.md CLAUDE.local.md .local/agents/.ignore-probe
  .claude/settings.local.json .codex/config.local.toml .claude/hooks/example.sh
)
for path in "${local_paths[@]}"; do
  git check-ignore -q "$path" || report_failure "documented local path is not ignored: $path"
done

if git check-ignore -q .claude/settings.json; then
  report_failure "shared project settings are ignored; contributors cannot receive them"
fi

local_only_pattern='(^|/)\.local/|(^|/)\.claude/hooks/|(^|/)(AGENTS\.local\.md|AGENTS\.override\.md|CLAUDE\.local\.md|settings\.local\.json|config\.local\.toml)$'
if ! printf '%s\n' '.local/agents/leak.md' | grep -Eq "$local_only_pattern"; then
  report_failure "internal local-only path classifier does not reject .local descendants"
fi
if git ls-files | grep -Eq "$local_only_pattern"; then
  report_failure "a local-only agent file is tracked"
fi

[[ "$fail" -eq 0 ]] || exit 1
echo "Agent instruction graph OK"
