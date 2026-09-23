---
name: publish-mod
description: Prepare, review, publish, and verify a curated FTK marketplace content package. Use for a mod release or production catalog entry, not for a framework or launcher release.
---

# Publish an FTK mod

Read the root and `marketplace/AGENTS.md` instructions, [publishing guide](../../../docs/PUBLISHING-MODS.md), [version policy](../../../docs/MOD-VERSIONING.md), and the package source. Use [Paladin](../../../marketplace/packages/paladin/) as the working example, not as a required package layout or permission to publish.

1. Separate an author's submission from maintainer actions. Review attribution, distribution rights, permanent package ID and mod GUID, new semantic version, exact dependencies, compatibility declarations, player-facing copy, and evidence for every advertised platform and game fingerprint.
2. Build a deterministic candidate archive and descriptor from the source. Keep manifest identity and framework minimum aligned with the descriptor; derive the same-major range and artifact hash, sizes, and file count from final bytes. Inspect the inventory and run the native helper's package validator. A passing validator or local fixture does not establish gameplay, author trust, or public download.
3. Check native registration, advertised behavior, presentation, and install lifecycle on each claimed game build. Record unverified behavior as a limitation; narrow platform and fingerprint claims rather than extrapolating from another build.
4. When publication is authorized, upload the reviewed archive and approved previews to a draft repository release. Confirm the complete asset set and its bytes before publication, then publish. Independently download and hash the public URLs before catalog inclusion. Preserve immutable versions; corrections need a new version and archive. A mod release must not displace the framework release as the repository's latest stable framework release.
5. Only after the release assets are public and verified, add the exact descriptor to `marketplace/catalog.json`. Run catalog validation, review and merge the catalog change, then verify the public Discover listing, preview, download, install, restart, and registered content in game.

Report the archive and release hashes, commands and outcomes, game evidence, public installation result, and remaining limits. Do not imply that catalog validation proves the linked archive or a live game path.
