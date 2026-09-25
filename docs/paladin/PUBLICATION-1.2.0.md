# Paladin 1.2.0 publication receipt

[Paladin 1.2.0](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/paladin-v1.2.0)
is public and immutable. The reviewed source is merge commit
`dfc549dd003d83f154d3994f9183346e1b76d882`, from
[PR #200](https://github.com/jarlbrak/ftk-mod-framework/pull/200), whose seven CI
jobs passed. The archive rebuilt from that merge is byte-identical to the
candidate used for the [native combat and resume trial](balance-1.2.0-live.json).

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| `paladin-1.2.0-c9d9e7c31ad7.zip` | 4,973,410 | `c9d9e7c31ad7fa69f6aee08bc1bf00ca104c9f0796cace7b5b2094404710bf39` |
| `paladin-censure-banner.png` | 1,534,614 | `ab1d493c687b3f4ff65895d334ed60cd823880e72fe77c1ee9f78fa2d06ff919` |

The archive expands to 18,824,357 bytes and 177 files. Its inventory consists
only of the manifest, content JSON and original PNG/GLB assets. Each file matches
the reviewed source. The banner is original promotional illustration, not a
gameplay screenshot; it retains the existing provenance.

Both assets were uploaded to a draft, independently downloaded and compared
byte-for-byte before publication. Both public URLs were downloaded again after
publication and matched the same hashes. The published framework 1.0.3 helper
accepted the exact archive and descriptor. GitHub reports the mod release as
immutable, with `draft=false`. Framework v1.0.3 remains the latest stable release;
Paladin was published with `--latest=false`.

The package minimum remains framework 1.0.3. The existing desktop installation
allowlist and player-reported Windows fingerprint are preserved; they do not
claim Windows/Linux gameplay evidence. Native macOS registration, the starting
class screen, solo combat, loot and same-version fresh-process resume are
recorded separately from the game-free helper lifecycle. One native Mono
startup crash in the framework reporting worker preceded a successful unchanged
retry. No cause or Paladin-specific regression was established.

The production descriptor merged in [PR #201](https://github.com/jarlbrak/ftk-mod-framework/pull/201)
after all seven CI jobs passed. Public in-game verification on native macOS then
passed using the published framework 1.0.3 and its verified launcher helper:

1. Installed public Paladin 1.1.0 through Mods and restarted to activate it.
2. Opened Browse after the catalog merge. The public 1.2.0 listing, promotional
   banner, compatibility notes and update action appeared.
3. Selected Update to 1.2.0 and Save for next launch. The downloaded cache archive
   matched the release SHA-256 above, and 1.2.0 was prepared as the pending generation.
4. Selected Quit and apply on next launch. The game exited. A fresh process
   activated the pending generation and cleared pending state.
5. Confirmed Installed showed version 1.2.0 and On for this adventure. All 177
   installed files matched the release archive byte-for-byte. Registration reported
   54/54 entries, zero content errors and zero content warnings.

The native audio focus exception prompted the framework report view on startup;
it was dismissed without submitting a report. This public upgrade trial did not
repeat combat or save resume: those were already exercised on the byte-identical
candidate. Existing saves were not migrated. Disable, removal and reinstall were
covered by game-free helper lifecycle checks, not repeated in this native trial.
The owned game process was stopped after verification.

Matched campaign balance, broad endgame gear trials, online co-op and
Windows/Linux gameplay remain unverified.
