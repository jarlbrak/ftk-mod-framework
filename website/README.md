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
navigation, reduced-motion defaults, and actual video playback. `SITE_URL` can point the same
checks at the deployed site. Screenshots go to the system temporary directory, not the site.

## Published data boundary

`src/data/catalog.json` is a reviewed snapshot of the production catalog. `paladin.json` is a
public-field projection of `content.json` from the downloaded, SHA-256 verified Paladin archive.
Equipment tables render from this projection; they do not read a working development package.
Update both deliberately when a new public package is ready. Never copy private manifests,
logs, game binaries, extracted game assets, or unreleased package claims into `public/`.

Use `node scripts/sync-published.mjs` to refresh the public catalog and package projection. It
requires immutable public download hashes to match and verifies manifest identity and minimum
framework. Review authored guides when mechanics change; regenerated tables alone cannot update
explanations. The current guide is explicitly scoped to Paladin 1.3.0.

No workflow creates a GitHub release or changes the repository's latest release. Mod release
publishing must continue to use `--latest=false`. Framework download links are explicitly pinned.

## Media provenance

`src/data/media-provenance.json` records sources and hashes for compressed stills.
`src/data/film-provenance.json` records the published original model/atlas hashes, encoding, and
output hashes for studio films. Native capture scope is in the source evidence README, linked
from the credits page. The site intentionally does not publish raw verification receipts.

`node scripts/prepare-media.mjs` verifies the committed native capture hashes, downloads approved
release previews, and regenerates WebP derivatives. Keep the artwork notice in `public/media/`.
No image-generation transformations are applied to captures. Hero and cards use CSS cropping;
gallery images preserve their complete source composition.

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
