#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 1 ]]; then
  echo "Usage: $0 /path/to/repository/scratch/isolated-game" >&2
  exit 2
fi
if [[ "$(uname -s)" != Darwin ]]; then
  echo "This developer experiment builds only on macOS." >&2
  exit 1
fi
script_dir="$(cd -- "$(dirname -- "$0")" && pwd -P)"
game_root="$(cd -- "$1" && pwd -P)"
if [[ "$(basename -- "$(dirname -- "$game_root")")" != scratch || ! -f "$game_root/FTK.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll" ]]; then
  echo "Pass an isolated macOS game root directly under scratch." >&2
  exit 1
fi
dotnet build "$script_dir/NativeWater.csproj" -c Release -p:TestGameRoot="$game_root"
out_dir="$script_dir/bin/Release/net35"
xcrun clang++ -arch x86_64 -std=c++11 -O2 -fno-fast-math -ffp-contract=off \
  -fno-associative-math -fno-unsafe-math-optimizations -fvisibility=hidden -dynamiclib \
  -Wl,-install_name,@rpath/libftk_water_native.dylib \
  "$script_dir/WaterKernel.cpp" -o "$out_dir/libftk_water_native.dylib"
shasum -a 256 "$script_dir/WaterKernel.cpp" "$out_dir/FtkNativeWater.dll" "$out_dir/libftk_water_native.dylib"
echo "Build only. Stop the isolated game before copying the two binaries into its BepInEx/plugins/native-water directory."
