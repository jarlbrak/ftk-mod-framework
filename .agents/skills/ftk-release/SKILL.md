---
name: ftk-release
description: Prepare and verify an FTK Mod Framework release without publishing until explicitly requested.
---

# FTK release

1. Read `docs/RELEASING.md`, root instructions, current release notes, tags, and working-tree state.
2. Resolve the intended version and channel from the user or existing release plan. Do not infer a
   stable promotion from a preview tag.
3. Run the full game-free matrix for framework, launcher helper, installers, catalog, packaging,
   and release-manifest integrity.
4. Require the documented live-game and platform evidence for claims included in the notes.
5. Build artifacts through `release.sh` or the documented packaging entry point. Verify hashes and
   inspect the archive inventory for game DLLs, credentials, local files, and unexpected binaries.
6. Prepare release notes with exact limitations and upgrade requirements.
7. Stop before tag creation, GitHub publication, or catalog mutation unless the user explicitly
   authorized that external write.

Use the repository's configured commit identity. Do not add vendor attribution unless public project
policy explicitly requires it.
