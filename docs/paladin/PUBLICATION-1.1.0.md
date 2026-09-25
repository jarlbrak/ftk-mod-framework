# Paladin 1.1.0 publication receipt

The reviewed source is merge commit `d7be6630dd01a5a91430a5aeaa4bc532c67f871f`.
[Framework v1.0.3](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.0.3)
is the latest stable framework release. Its public DLL SHA-256 is
`f2ab444bd389c97f64786bc8ba7e8c77c010b2c4aa3edb778d5e4a6c866fce7f`;
its public `update.json` SHA-256 is
`41c778771b8f2792e6606d6a789a268cf5c5baf498e45846aee860ba2db5f907`.
The framework release contains 13 assets. The complete draft asset set was
downloaded and hash-matched to local staging before publication. Public DLL and
manifest URLs were downloaded and hashed again after publication. GitHub reports
the release as immutable.

[Paladin 1.1.0](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/paladin-v1.1.0)
is a separate immutable release and is excluded from the framework latest feed.
The content archive `paladin-1.1.0-b5501fc3708f.zip` has SHA-256
`b5501fc3708f5ab6c53502252e7738bf2054c87cdb27eb22ae97e80c26f0774b`,
4,973,415 compressed bytes, 18,824,344 expanded bytes and 177 files. The
original promotional banner has SHA-256
`ab1d493c687b3f4ff65895d334ed60cd823880e72fe77c1ee9f78fa2d06ff919`.
Both release assets were downloaded through their public URLs after publication
and matched the frozen local hashes. The archive inventory contains only the
manifest, content JSON, GLB models and PNG assets. The native helper validated
the exact public archive against the catalog descriptor.

The [Cleansing March tile trial](cleansing-march-live-2026-09-24.json) used the
exact published Paladin archive with an earlier framework 1.0.3 build. A
[prerelease framework smoke](release-smoke-2026-09-24.json) registered all 54
entries and reached a native single-player session. The final published
framework DLL also loaded the same Paladin archive in an isolated macOS game:
54/54 entries registered with zero content errors or warnings, and five
development self-tests passed with no self-test failures. These observations
do not repeat Poison or Curse entry on the final framework DLL.

After the [production catalog change](https://github.com/jarlbrak/ftk-mod-framework/pull/198)
merged, the isolated macOS game refreshed public Discover. The Paladin card
showed version 1.1.0, framework 1.0.3, Cleansing March, the published banner
and the stated platform limitations. The game removed Paladin through the Mods
panel, then prepared and applied a fresh installation from the public catalog.
For a second installation, the existing archive cache was moved aside in the
isolated copy before using the Mods panel. The helper restored the missing
archive at the catalog's SHA-256 path; its bytes hashed to
`b5501fc3708f5ab6c53502252e7738bf2054c87cdb27eb22ae97e80c26f0774b`.
The resulting marketplace generation contained Paladin 1.1.0. After a full
game exit and restart, the title screen reported one enabled mod, Paladin
1.1.0, with FTK Mod Framework 1.0.3. The game registered all 54 entries with
zero content errors or warnings. This is a public catalog install and restart
smoke on the locally verified macOS game build.

Disable/enable, online co-op and Windows/Linux gameplay remain live gates. The
Windows game fingerprint was player-reported. The final framework DLL has not
been used to repeat the Poison and Curse tile trial.
