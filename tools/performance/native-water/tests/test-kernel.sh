#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "$0")" && pwd -P)"
build_dir="$(mktemp -d "${TMPDIR:-/tmp}/ftk-water-kernel.XXXXXX")"
trap 'rm -rf -- "$build_dir"' EXIT
compiler="${CXX:-c++}"
set -- "$script_dir/kernel_safety.cpp" -o "$build_dir/kernel-safety"
if [[ "$(uname -s)" == Linux ]]; then set -- "$@" -ldl; fi
"$compiler" -std=c++11 -O2 -fno-fast-math -ffp-contract=off \
  -fno-associative-math -fno-unsafe-math-optimizations \
  -Wall -Wextra -Werror -DFTK_WATER_KERNEL_TEST=1 \
  "$@"
"$build_dir/kernel-safety"
