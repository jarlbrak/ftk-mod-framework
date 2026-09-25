# Public website instructions

Read the root `AGENTS.md` first.

- This Astro + Starlight subtree is a player website. Keep internal evidence logs, personal paths,
  credentials, game assemblies, extracted native assets, and development fixtures out of the site.
- `src/data/catalog.json` is a reviewed published catalog snapshot. `src/data/paladin.json` is
  projected from the hash-verified public archive by `scripts/sync-published.mjs`. Update the
  generator or its source inputs rather than hand-editing derived stats.
- Keep published versions distinct from playtests and unreleased previews. A declaration of
  platform support is not live validation. Match player claims to published metadata and evidence.
- Keep compressed media source/output hashes, accurate capture or studio labels, and the artwork
  notice. Do not imply a studio rotation proves native animation or a review inventory proves drops.
- Run `npm ci`, `npm run build`, the preview server, and `npm test` for site changes. Inspect desktop
  and mobile screenshots after visual changes. Check external downloads when updating release links.
- Preserve `/ftk-mod-framework/` base-path handling and the 50 MiB artifact budget. The Pages
  workflow may deploy from master but must never create a release or change the latest framework tag.
