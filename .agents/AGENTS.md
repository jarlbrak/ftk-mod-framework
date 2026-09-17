# Portable instruction-layer instructions

Read the root `AGENTS.md` first.

- Everything here is harness-neutral. Use repository-relative paths and describe tools by what they
  do, not by a vendor tool name.
- Never reference an adapter directory from this tree. Adapters depend on these files; the
  dependency does not run the other way, and the instruction check enforces it.
- `skills/<name>/SKILL.md` is a workflow. `roles/<name>.md` is a specialist contract stating
  responsibility, authority, required evidence, boundaries, and return format.
- Every role contract states a same-session fallback, so no workflow depends on delegation support.
- Adding or renaming a skill or role means updating its adapters in the same change. Run
  `bash scripts/agent/check-instructions.sh`.
- Keep machine facts, private services, and account routing out of this tree entirely.
