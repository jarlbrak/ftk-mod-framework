# Legacy skill-body instructions

Read the root `AGENTS.md` first.

- This directory predates the `.agents/skills/` convention and survives only because public model
  guides link to these stable paths. Do not add new skills here.
- `.agents/skills/ftk-custom-models/SKILL.md` is the canonical entry point and points into this
  body. Keep the two descriptions consistent when either changes.
- `SKILL.md` is the always-loaded tier: orientation, route choice, the working method, and the
  non-negotiables. Keep it lean. Route-specific and phase-specific detail belongs in
  `references/`, which is read only when a step needs it.
- Every file in `references/` must be reachable from the entry point's routing table, and each must
  stand on its own. A reader loading one reference has not read the others.
- `agents/openai.yaml` is display metadata for harnesses that surface a skill catalog. It carries no
  project policy and must not accumulate any.
- Moving this body requires updating every public document that links to it in the same change.
