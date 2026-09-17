# Probe test instructions

Read the root `AGENTS.md` and `FTKPerfProbe/AGENTS.md` first.

- This project targets `net10.0` and covers `FTKPerfProbe/Pure/` only. It must keep running with no
  game, no Unity, and no BepInEx.
- If a behavior cannot be tested from here, the logic is in the wrong layer. Move it into `Pure/`
  rather than widening this project's dependencies.
- Add the failing case before the fix, and assert the boundary, not just the happy value.
- These tests are game-free evidence. They never establish in-game timing, frame cost, or budget
  compliance.
- CI runs `dotnet test FTKPerfProbe.Tests/FTKPerfProbe.Tests.csproj -c Release`. Keep it green.
