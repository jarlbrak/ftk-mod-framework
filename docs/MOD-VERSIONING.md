# Mod and framework versions

Every content mod has a `manifest.json`. A mod's own release version and its framework
compatibility declaration describe different things:

```json
{
  "modGuid": "com.example.wayfarer",
  "name": "Wayfarer",
  "version": "1.0.0",
  "frameworkVersion": "0.1.2",
  "description": "A new playable class.",
  "author": "Example author"
}
```

`version` identifies this exact mod release. `frameworkVersion` is the earliest framework
release on which the author has confirmed this mod works. Both use numeric `major.minor.patch`
versions, without a `v` prefix. Preview is a framework release channel, not part of these fields.

## Compatibility rule

The running framework must be at least the declared version and have the same major number.
For a declaration of `0.1.2`:

| Running framework | Result |
| --- | --- |
| `0.1.1` | Blocked: older than the confirmed minimum |
| `0.1.2` | Allowed |
| `0.1.3` | Allowed |
| `0.2.0` | Allowed |
| `1.0.0` | Blocked: author confirmation needed for the new major |

This rule deliberately applies to major zero too. Framework releases must preserve the mod
contract across minor and patch updates. A breaking change belongs in a new major release.
A manifest declaration records an author's confirmation; it is not proof that every mod
combination or existing save has been tested.

## Confirming a new major

Test the mod against the new framework major, update `frameworkVersion` to the earliest
version confirmed in that major, and publish a new mod version. For example, a compatibility
update can change the mod from `1.0.0` to `1.0.1` and declare framework `1.0.0`.
The old mod release remains available for players using the old framework major.

Published package bytes and hashes are immutable. Even a manifest-only compatibility change
requires a new mod version and archive hash. Never replace an existing published archive.

## Missing declarations and manual mods

An absent or invalid framework declaration does not imply compatibility. The mod remains
visible as blocked with an explanation, and its content and declared behavior DLL do not
load. The user's saved enabled preference remains unchanged. Obtain an updated manifest from
the author instead of changing it merely to bypass the check.

These checks cover content mods discovered through FTK's manifest loader and managed
marketplace packages. Arbitrary BepInEx plugins load separately and remain unverified by this
policy.

## Marketplace metadata and framework updates

A package descriptor repeats the archive manifest's `frameworkVersion`. They must match.
The descriptor also retains a canonical `frameworkRange` for older clients:

```json
{
  "frameworkVersion": "0.1.2",
  "frameworkRange": ">=0.1.2 <1.0.0"
}
```

The range cannot widen the declared major or lower the minimum. Dependencies continue to
pin exact mod versions independently of framework compatibility.

Before changing the framework, the launcher checks active and pending managed packages and
manifest mods under the configured content root. The manual check respects the data-loader
and development-fixture settings. It conservatively checks even disabled manual mods because
the launcher cannot reliably read their in-game enable preferences.
Choosing an exact framework version does not bypass compatibility. Legacy managed records
remain readable, but their old ranges cannot authorize a move across the framework major
recorded when that generation was prepared. Compatibility with a new major requires an
updated package declaration.

The manual preflight is part of launcher bundle 0.1.3 or newer. Older bundles do not gain it
from a framework-only update; replace the launcher package to obtain that check. The new
runtime checks still prevent incompatible manifest content from loading.
