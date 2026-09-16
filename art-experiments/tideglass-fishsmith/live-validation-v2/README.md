# Tideglass Fishsmith — V2 native preview record

This immutable record preserves the refined Tideglass Fishsmith in the actual
isolated FTK Party Select flow. The game created Player 1 through its native
Create Game callback; visible keyboard navigation selected class 115; and the
game-owned `blacksmith_Fish` avatar then rendered the V2 body through a
24-frame fixed-step native idle capture.

The archive records the selected preview owner, CEL, visible custom renderers,
and the inactive-but-bound `hairTop` renderer as structured evidence so the
shared validation ledger can distinguish a native preview observation from a
generic binding record.

V2 is accepted for this sampled Party Select presentation. Its compact teal and
brass fish-smith silhouette reads as a deliberate character at game scale and
does not reproduce V1's pale mask-like face. The body and lower mantle remain
visible throughout the sampled idle. `hairTop` is bound but inactive in the
native default Fish preview, as a property of that assembled preview rather
than an inferred missing binding.

This is not a broad player acceptance: combat, equipment branches, portrait,
overworld, culling, teardown, multiplayer, and every renderer visibility state
remain separate checks. The archive contains no game binary, Unity asset,
native mesh, local reference input, or log.

Run `python3 archive.py` only before `validation.json` exists. It refuses to
replace a completed archive unless `FTK_ARCHIVE_REBUILD=1` is deliberately set.
Run `python3 verify.py` to verify the archive independently of later changes to
the root authoring package.
