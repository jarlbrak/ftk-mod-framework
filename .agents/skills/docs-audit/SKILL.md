---
name: docs-audit
description: Audit public documentation against recent code, releases, and repository paths.
---

# Documentation audit

1. Identify the code changes, merged PRs, or release range in scope.
2. Map each user-visible behavior to its README entry point and canonical detailed guide.
3. Search for stale capability, installation, compatibility, status, and evidence claims.
4. Check relative Markdown links across the public documentation surfaces.
5. Prefer one canonical explanation with links over repeated volatile detail.
6. Preserve distinctions between shipped, game-free tested, live verified, platform tested,
   co-op tested, and artistically approved.
7. Run `git diff --check` and the repository instruction checker.

Do not expose private paths, services, knowledge IDs, or machine setup in public docs.
