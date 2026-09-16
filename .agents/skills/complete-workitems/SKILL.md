---
name: complete-workitems
description: Implement approved FTK work items in dependency order with review and proportional verification.
---

# Complete work items

1. Read the spec and all open child work items. Confirm dependencies and acceptance criteria.
2. Work in a dedicated branch/worktree. Preserve unrelated checkouts and changes.
3. For each item, obtain exact game-source facts first, then route implementation through the
   matching generic role.
4. Keep the slice minimal and add focused proof. Do not close an item because it merely compiles.
5. Run `verify-change`, then obtain architecture review for non-trivial changes.
6. Update public docs and issue evidence. State live-game gates accurately.
7. Commit and publish only when authorized by the requested workflow. Keep issue closure tied to
   landed, verified behavior rather than optimistic status.
