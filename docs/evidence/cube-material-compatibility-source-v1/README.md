# Cube material compatibility source evidence

CubeA requires two distinct material surfaces. Its native UV animation component
updates material slot 1, while the currently pinned explicit mesh replacement
assigns one slot. This is a source-proven compatibility gap, with live behavior
still unmeasured. The evidence pins preserve the implementation before repair.

The intended faithful extension uses separate original stationary interior and
scrolling jelly submeshes with explicit native material-slot mapping. Appending a
second material to one submesh would redraw geometry. Silently disabling or
remapping the native scroller would change the authored behavior. Material
ownership after the native `Renderer.materials` access needs live verification.

CubeE shares the exact three-joint palette, bind matrices and controller but has
one native material and no scroller in its inspected subtree. It provides a
separate baseline candidate, not evidence that CubeA is supported.

See [the source record](validation.json) for exact identities, hashes, required
checks and limits. No native meshes, textures, animation data or decompiled code
are packaged here. Neither candidate has live acceptance in this record.
