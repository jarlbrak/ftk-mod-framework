# Thief equipment candidate

This unreleased candidate contains 45 equipment items and nine weapon proficiencies, with paired daggers, Talent pistols, visible-armor roles and 18 universal Guild Token exchange offers. Stable `thief_bow_*` IDs are preserved. The former wearable Guild Token is now Guild Insignia, retaining its identity. Seven nose-and-mouth bandanas retain the `thief_hood_*` IDs, with exact profiles for seven native Thief skinsets and the optional registered Possum race.

[Candidate contract and item ledger](../../../docs/thief/CANDIDATE.md) describe provisional balance, acquisition, compatibility, validation boundaries and release-time website work. The [design direction](../../../docs/thief/NEXT-DESIGN.md) records the intended outcome.

Regenerate declarations with `python3 marketplace/packages/build_thief.py`, then run `python3 marketplace/packages/validate_thief.py`. Imported advanced asset hashes are recorded in `assets/advanced-import.provenance.json`. `../thief_head_fits.json` is the portable generator input for the eight wearer fits; `assets/thief-bandana.provenance.json` records the original source lineage. Retained legacy assets are not all active references and must not enter a new archive merely because they remain on disk.

## Compatibility and release status

Thief 1.1.0 is unreleased and requires framework 1.9.0 within the range `>=1.9.0 <2.0.0`, with the matching helper. It requires the equipment, head-profile and universal exchange APIs in that candidate. Earlier local framework 1.6.3 registration and Resume observations are historical evidence, not compatibility proof for the current manifest or the published framework. The exact final 1.9.0 framework/package pair must pass native validation, and matching public framework/helper availability remains a publication prerequisite.

Recorded native observations cover single-player macOS on game assembly SHA-256 `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`. Windows, Linux and online co-op gameplay are unverified. Current online exchange purchases are disabled. Historical receipts include bounded primary wearer fit/action samples, three-hero save/resume, title renderer retirement and isolated managed selection lifecycle checks; their exact binaries and limits remain recorded in the validation guide. A separate title-only absence test found no Thief registration, but its cached Resume reference was never used; loading a custom save without Thief is unverified. Complete appearance/animation coverage, ordinary acquisition frequency and long-campaign balance remain unclaimed. Observed startup and quit exceptions are recorded in the validation guide; the run is not described as error-free.

## Package preparation and artwork

`build_thief.py` regenerates declarations; `package_thief.py` builds a deterministic archive from the manifest, content and active model/texture/icon references. Pass the tested `--platform` and installed `--game-assembly` explicitly. `--release` prepares immutable versioned URLs without uploading or publishing, and never expands platform claims from listing metadata. The current candidate must not replace published 1.0.0 archive bytes.

The advanced, worn-coat/boot, bandana and coat-display provenance documents under `assets/` record original-source and adopted runtime identities. Native bodies, faces, hair and backpacks remain game-owned; game geometry and captured animation arrays are not redistribution or authoring inputs. MIT identifies the repository code/data license; provider-generated artwork rights depend on the generation plan and applicable provider terms, and must not be represented as an unrestricted third-party asset license. The release preparation record retains the scoped rights review. Final accepted previews and item media must match the immutable package.

Website impact: this candidate remains unreleased. Keep published Thief 1.0.0 data and media unchanged until the accepted archive is public and hash-verified; then regenerate derived item/library data and update the guide, framework minimum, compatibility, media provenance and release notes.
