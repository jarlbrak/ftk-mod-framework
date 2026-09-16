---
name: verify-change
description: Select and run the proportional FTK verification matrix for the current diff.
---

# Verify change

Use for implementation handoff, review, or whenever the required checks are unclear.

1. Read the root and nearest `AGENTS.md` files.
2. Inspect `git status`, changed paths, and the diff. Preserve unrelated work.
3. Run the narrowest test that exercises the changed behavior first.
4. Add path-triggered checks:
   - `FTKModFramework/**/*.cs`: `cd FTKModFramework && dotnet build -c Release`.
   - Player mod discovery, compatibility, marketplace, or update runtime:
     `dotnet run --project FTKModFramework/Tests/PlayerMods/PlayerMods.csproj -c Release`.
   - `FTKPerfProbe*`: `dotnet test FTKPerfProbe.Tests/FTKPerfProbe.Tests.csproj -c Release`.
   - `install.sh` or installer fixtures: `bash tests/installer/test-install.sh`.
   - `launcher/helper`: run `go test ./...` from that directory.
   - Marketplace catalog: run the helper's `marketplace-catalog-validate` command.
   - Model pipeline: run the focused Python or .NET suite named by the nearest guide, then
     current-ledger checks for generated output.
   - Documentation: check relative links and stale renamed paths.
5. Always run `git diff --check`.
6. Before commit or PR handoff, inspect staged names and stop if a game DLL, local overlay,
   credential, capture, save, or decompiled source is present.
7. Report command, result, and scope. List live-game and platform checks separately as passed,
   failed, or not run with the exact prerequisite.

Do not weaken a test or reinterpret a nonzero exit as success. A build cannot satisfy an in-game gate.
