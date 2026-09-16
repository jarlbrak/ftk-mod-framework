# Legacy singular native HUD lifetime

One exact Reefstrider/fishA01 singular-body lineage supports source → native HUD clone → clone-first release → final source release. Architect joined lease5 and initial IDs through frame142891 to149161 in session5856168eea154be3adecc2a5d1cb2330. All three exact owned Mesh/Texture2D/Material wrappers become Unity-null; five pinned native assets remain alive. The HUD output Texture2D is destroyed and its dictionary entry removed; the native cached camera/render texture intentionally survives with cleared pointers.

Tint was NOT EXERCISED: matLoot triggered the existing weapon-material exclusion, so measured _Color stayed white. This does not validate combined singular+tint material ownership.

Three complete120-frame recordings preserve native pass, ordinary58→50 hit, and explicit KillSingle death. Two native Collect actions lead to strict Ready0/3. Explicit death is not ordinary lethal damage. No direct Destroy/Release/prune commands were used in the reviewed cleanup sequence.

Root reviewed six selected PNGs: body and HUD portraits visible, raised-arm pose, native -8 hit with hero occlusion, falling corpse, and later loot-panel occlusion. Neither every frame nor every angle was visually accepted. No source-first/never-active clone proof, global leak claim, floor collision or physics sleeping acceptance.

All raw capture metadata and journals are lossless gzip. All360 source PNG hashes are retained; six selected originals and three120-frame MP4 derivatives are archived. Native payload, DLLs and decompiled C# are excluded; source hashes remain provenance references. Reproduction is offline and refuses an existing output directory.
