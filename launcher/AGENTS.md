# Launcher instructions

Read the root `AGENTS.md` first.

- Preserve verified-install fallback, checksum enforcement, atomic replacement, and offline behavior.
- Framework and helper versions move as a compatible pair.
- Compatibility checks cover managed packages and configured manifest mods, not arbitrary BepInEx
  plugins. Do not broaden claims beyond that boundary.
- Run Go tests, release-manifest checks, relevant installer fixtures, and platform builds for changed
  surfaces. Cross-build success is not gameplay evidence on that platform.
- Never place credentials or private release state in launcher assets or logs.
