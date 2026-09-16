# Core engine instructions

Read the root and `FTKModFramework/AGENTS.md` first.

- Verify exact game types and behavior from the installed assembly before changing a patch or DB
  integration. Use the portable `decompile-lookup` workflow.
- Keep Harmony patches narrow, idempotent, and fail-safe. Preserve vanilla behavior when a custom
  registration is missing, invalid, or incompatible.
- Never mutate a vanilla row, prefab, mesh, material, or shared resource in place.
- Preserve deterministic IDs, registration ordering, save resolution, and multiplayer behavior.
- Keep resource ownership explicit. Transactional visual changes must preflight, roll back, and
  release only allocations the framework owns.
- Do not leak Core-only implementation types into the public authoring surface without an explicit
  API decision and architecture review.
