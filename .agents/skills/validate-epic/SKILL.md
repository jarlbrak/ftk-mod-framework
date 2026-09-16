---
name: validate-epic
description: Validate an FTK epic against outcomes and all child specifications before closure.
---

# Validate epic

1. Enumerate exact child specs and verify their parent links.
2. Require each spec to pass `validate-spec`; sample implementation and evidence rather than relying
   only on closed status.
3. Evaluate the epic's user outcome, cross-spec integration, documentation, compatibility, and
   remaining risks against its original success measures.
4. Run or review end-to-end evidence appropriate to the epic. Keep platform and co-op limits explicit.
5. Create remediation specs/items for gaps. Close only when the integrated outcome is real.
