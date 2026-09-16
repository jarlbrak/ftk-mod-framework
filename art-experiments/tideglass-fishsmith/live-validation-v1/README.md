# Tideglass Fishsmith — V1 native preview record

This immutable record preserves the first isolated FTK Party Select validation
of Tideglass Fishsmith before any art revision. It captures the real native
Create Game route, visible selection of class 115, the assembled
`blacksmith_Fish` preview, and a settled 24-frame fixed-step idle sample.

The technical result is positive: FTK bound the authored `playerFIsh` body and
`hairBottom` mantle to the game-owned preview avatar, and the body remained
visible throughout the sampled native idle. The authored six-bone `hairTop`
crest is also bound to its exact renderer, but that renderer is inactive in
this default native Fish preview. Native Blacksmith armor, boots, helm, and
backpack remain part of the assembled presentation.

V1 is deliberately **not** a final-art acceptance. The pale snout and eye
contrast read too much like a mask in the real menu, so the root authoring
package will receive a separately validated V2 refinement. This archive keeps
V1's authored inputs, runtime records, screenshots, and derived video stable
for comparison. It contains no game binary, Unity asset, native mesh, or log.

Run `python3 archive.py` only before `validation.json` exists. It refuses to
replace a completed archive unless `FTK_ARCHIVE_REBUILD=1` is deliberately
set. Run `python3 verify.py` to verify the archive independently of later
changes to the root authoring package.
