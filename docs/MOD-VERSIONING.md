# Mod and framework versions

A content mod's [manifest](../marketplace/packages/paladin/manifest.json) records its own release version and the minimum framework version it declares compatible. Before publication, gameplay must confirm that declaration. The unpublished Paladin 1.1.0 source candidate declares:

```json
{
  "modGuid": "com.ftkmf.paladin",
  "name": "Paladin",
  "version": "1.1.0",
  "frameworkVersion": "1.0.3"
}
```

Both fields use numeric `major.minor.patch` versions without a `v` prefix. `version` identifies the exact mod release. `frameworkVersion` sets a minimum within the same framework major. A framework older than that minimum or from a different major blocks the mod's content and declared behavior DLL, while keeping the mod visible with an explanation. A compatibility declaration records the author's confirmation; it does not prove every save, platform, mod combination, or co-op session works.

For a mod declaring framework `1.0.3`, framework `1.0.3` and later compatible `1.x` versions are eligible. A `0.x`, `1.0.0`, `1.0.1`, `1.0.2`, or `2.x` framework is blocked. To support a new framework major, test the mod against it, update `frameworkVersion`, and publish a new mod version. Published package bytes and SHA-256 hashes are immutable, so even a manifest-only compatibility change needs a new version and archive.

The marketplace descriptor repeats the manifest's `frameworkVersion` and retains a canonical `frameworkRange` for older clients. The range cannot lower the minimum or cross the declared major. Dependencies pin exact mod versions independently. Paladin's [manifest](../marketplace/packages/paladin/manifest.json) and [marketplace listing](../marketplace/packages/paladin/listing.json) show the 1.1.0 source declaration; the production catalog retains the immutable published 1.0.1 release.

An absent or invalid declaration does not imply compatibility. Get an updated manifest from the author instead of editing it to bypass the check. These checks apply to manifest-loaded content and managed marketplace packages; arbitrary BepInEx plugins load separately and are not verified by this policy. The launcher checks active and pending managed packages and discovered manifest mods before a framework update. Disabled manual mods are checked conservatively because their in-game preference may not be available to the launcher.
