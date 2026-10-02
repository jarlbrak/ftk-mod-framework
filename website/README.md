# Public player website

Astro + Starlight site at https://jarlbrak.github.io/ftk-mod-framework/.
Run commands from this directory unless stated otherwise.

```sh
npm ci
npm run build
npm run preview -- --host 127.0.0.1
npm test
```

The browser checks use Playwright Chromium (`npx playwright install chromium`). They exercise
all pages at desktop and mobile widths, local links and fragments, images, search, mobile
navigation, tier and slot filters, and visible ability text. `SITE_URL` can point the same
checks at the deployed site. Screenshots go to the system temporary directory, not the site.

## Published data boundary

`src/data/catalog.json` is a reviewed snapshot of the production catalog. `paladin.json` and `thief.json` are
public-field projection of `content.json` from the downloaded, SHA-256 verified Paladin and Thief archives.
Equipment cards render from this projection; they do not read a working development package.
Update both deliberately when a new public package is ready. Never copy private manifests,
logs, game binaries, extracted game assets, or unreleased package claims into `public/`.

Use `node scripts/sync-published.mjs` to refresh the public catalog and package projection. It
requires immutable public download hashes to match and verifies manifest identity and minimum
framework. During an authorized release, pass a reviewed local catalog path, for example
`node scripts/sync-published.mjs ../marketplace/catalog.json`, after its release assets are public.
This uses the same public-download hash checks and allows the catalog and site to merge together.
Review authored guides when mechanics change; regenerated cards alone cannot update
explanations. The published projections remain Paladin 2.0.0 and Thief 1.0.0. Authored Paladin 2.0.1 guide copy may be prepared in a release branch, but do not merge it until the immutable 2.0.1 archive and framework 1.8.0 are public and the catalog projection is regenerated. Thief content stays unchanged.

`src/data/catalog.ts` applies author-approved website wording to the immutable catalog snapshot.
Thief 1.0.0 is labeled as a release; its compatibility limitations and package identity are retained.

No workflow creates a GitHub release or changes the repository's latest release. Mod release
publishing must continue to use `--latest=false`. Framework download links are explicitly pinned.

## Media provenance

`src/data/media-provenance.json` records sources and hashes for compressed stills.
`src/data/film-provenance.json` records the published original model/atlas hashes, encoding, and
output hashes for studio films. Native capture scope is in the source evidence README, linked
from the credits page. The site intentionally does not publish raw verification receipts.

`node scripts/prepare-media.mjs` verifies the committed native capture hashes, downloads approved
release banners selected in `src/data/library-art.json`, and regenerates WebP derivatives.
The Possum banner has a retained website-only edited source and generation prompt in `artwork/`;
its local source hash is verified during media preparation. Published package assets are unchanged.
Library artwork is presentation metadata, separate from the current package screenshot. Cards
use complete promotional banners and version, category, and framework chips. Keep the artwork notice in `public/media/`.
No image-generation transformations are applied to captures. Hero and cards use CSS cropping;
embedded screenshots preserve their complete source composition.

Studio turntables render only original equipment from an extracted, hash-verified public archive.
From the repository root, use Blender with the script arguments documented in
`scripts/render-turntable.py`. It records the reproducible scene, lighting, camera, and 96-frame
rotation. Encode each frame directory from this directory:

```sh
ffmpeg -framerate 24 -i "$FRAMES/%04d.png" -c:v libx264 -crf 25 -pix_fmt yuv420p -movflags +faststart -an public/media/kingsfall.mp4
ffmpeg -framerate 24 -i "$FRAMES/%04d.png" -c:v libvpx-vp9 -pix_fmt yuv420p -crf 36 -b:v 0 -an public/media/kingsfall.webm
```

Repeat for `bastion` using the Last Bastion model. Posters use frame zero in WebP. These clips are
studio rotations, not native gameplay animation. All videos have controls, posters, no autoplay,
and `preload="none"`; motion starts only with an explicit play action.

## Deployment

`.github/workflows/website.yml` builds and tests pull requests. Changes merged to `master` deploy
through GitHub Actions to the `github-pages` environment. Configure repository Pages to use Actions.
The base URL is `/ftk-mod-framework/`; do not change it without testing nested routes and media.
The artifact budget is 50 MiB, with a 10 MiB single-file cap, comfortably below the GitHub Pages
1 GB published-site limit. Packages remain release downloads rather than copies in the site.

Before publishing, inspect desktop and mobile screenshots, validate external downloads, run
`git diff --check`, and verify that the repository latest release still identifies the framework.

## Integrated mod showcases

Each mod has one page with its promotional banner, class guide, and searchable HTML equipment cards. The old gallery and armory URLs lead to the complete Paladin guide.
Cards display artwork, stats, and compact ability lines directly as flat HTML content. Without JavaScript all cards remain visible. Search filters names and equipped ability explanations.
The test suite checks all 96 published HTML item cards at desktop and mobile sizes, including
images, inline ability text, search, tier and slot filtering. The cards are original CSS inspired by native tooltips,
not extracted game UI assets.

`node scripts/prepare-items.mjs --package Paladin` downloads the catalog's hash-verified Paladin
archive and prepares only its item media; other package media and provenance are preserved. A
different catalog package can be named with `--package NAME`. For a reviewed local archive before
publication, use `--package Paladin --archive ZIP --archive-sha256 SHA256 --version VERSION`.
This verifies the archive and manifest but does not publish the package or update catalog data.

Each item with a package icon uses that icon. Each item without one needs a studio still at
`artwork/items/ITEM_ID.png` and a matching `artwork/items/ITEM_ID.receipt.json`. The receipt must
declare `id`, `output` (the PNG basename or `artwork/items/` path), `outputSha256`, the actual `renderer`, and the
equipped root `itemModels` paths and SHA-256 values as `model`/`modelSha256`,
`texture`/`textureSha256`, and `metallicGlossTexture`/`metallicGlossTextureSha256`. The script
checks the PNG and all three package assets before writing WebP or provenance. Fourteen currently
published Paladin hammers use studio stills; future renders must have receipts for the exact
selected package. `src/data/item-art-provenance.json` records package, source, receipt, model,
albedo, metallic mask, and derivative hashes.
Only icon derivatives reach the deployed site; no raw models or atlases do. These stills show
geometry in studio lighting and do not establish native fit or animation coverage.

## Coming Soon: Blacksmith

`mods/blacksmith/` is an explicitly unreleased, preview with 32 HTML tooltip cards, search, tier and slot filters, and a promotional banner.
It has no package download and does not alter the published catalog. Studio artwork is labeled
and preview stats may change. The cards use selectable text with a browser font substitute.

Regenerate the public-safe projection and WebP artwork from the approved local gallery with
`node scripts/prepare-blacksmith-preview.mjs GALLERY_DIRECTORY`. Source and output image hashes
are retained in `src/data/blacksmith-art-provenance.json`; private captures and receipts are excluded.
At release, replace this preview with the verified public archive projection and update its
Coming Soon navigation and library link.

Paladin, Thief, and Blacksmith share `TooltipGallery.astro`. Paladin and Thief use their verified
published projections and retain release/download information. Blacksmith remains a Coming Soon
preview. Native screenshots and turntable sections are omitted from these mod pages.
