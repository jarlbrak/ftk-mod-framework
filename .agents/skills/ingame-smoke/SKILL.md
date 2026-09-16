---
name: ingame-smoke
description: Deploy and verify an FTK change in a configured local game installation.
---

# In-game smoke

Use only when a local overlay explicitly describes an authorized game installation and deployment
route. Read `.local/agents/game-validation.md` and `.local/agents/environment.md` if present.

1. Run the required game-free build and focused tests first.
2. Record the binary hash being deployed.
3. Deploy through the configured project script or isolated test copy. Do not overwrite unrelated
   plugins, saves, settings, or evidence.
4. Enable diagnostic self-tests only in a development configuration.
5. Launch through the configured local route and inspect the fresh log for framework load,
   expected `SELF-TEST PASS` lines, zero relevant failures, and the intended feature evidence.
6. For visual or gameplay claims, exercise and observe the exact route. Log success alone does not
   establish appearance, motion, save, progression, co-op, or lifecycle behavior.
7. Restore temporary configuration and verify no game process or isolated fixture remains when the
   local procedure requires cleanup.
8. Report the binary identity, steps, observations, and untested boundaries.

If the local files or game installation are absent, mark the gate unavailable. Never silently skip it.
