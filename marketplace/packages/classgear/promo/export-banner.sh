#!/usr/bin/env bash
set -euo pipefail

promo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# Normalize the generated near-16:9 image without color quantization.
magick "$promo_dir/blacksmith-banner-aligned-source.png" \
  -resize '1920x1080!' -strip \
  -define png:compression-level=9 "$promo_dir/blacksmith-banner.png"
