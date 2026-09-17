# Performance probe instructions

Read the root `AGENTS.md` first.

- The probe is a standalone BepInEx plugin targeting `net35`. It ships and versions separately from
  the framework and must not become a framework dependency.
- Probes are read-only timers. A probe that mutates game state, allocates per frame, or changes
  execution order is a defect, not a measurement.
- Keep measurement logic in `Pure/`, free of Unity and Harmony types, so `FTKPerfProbe.Tests`
  (`net10.0`) can cover it without the game. Put the game-touching wiring in the outer files.
- Add the failing case to the pure tests before changing an accumulator, a budget rule, or a CSV
  column. CI runs that suite.
- A number from a probe run describes the machine, build, and scene that produced it. Record those
  alongside the number, and never compare runs that do not share them.
- The scale-budget gate is documented in `docs/SCALE-BUDGET.md`. Change the gate and its
  documentation together.
