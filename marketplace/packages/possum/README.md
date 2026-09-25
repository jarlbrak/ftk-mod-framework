# Possum 1.0.0

Bring a little woodland mischief to your next adventure. Possum adds a cosmetic
race with an angular cream face, rounded ears, a pink nose and a long pink tail.
Class abilities, stats and native equipment remain unchanged.

Requires framework 1.1.0 within major version 1. Restart after installation or
removal. Do not downgrade the framework while using saves with custom races.

Fourteen native class bindings are included. The final smaller-hand model was observed in Hunter character selection,
the overworld and an ordinary starting-bow attack on macOS with framework 1.1.0.
The attack returned to idle and registered damage.
Other class layouts, armor swaps, other weapons, incoming hit/death, save/reload,
and online co-op are unverified. The tail follows the torso without an independent
animation chain. Appearance may clip with untested equipment.

The approved banner is promotional imagegen artwork based directly on an offline
Blender render of the final original mesh. It is not a screenshot. The separate
historical gameplay screenshot predates the smaller-hand revision.

## Attribution

Authored by JarlBrak. Original concept/reference and promotional artwork were
created with imagegen. Original geometry and texture were generated using Hyper3D
Rodin, then fitted, weighted and exported in Blender. No native game meshes,
textures, assemblies or animations are included in the install archive. Native
clothes and equipment visible in gameplay previews remain supplied by the game.

Hyper3D's [Rodin output terms](https://hyper3d.ai/legal/terms), section 5(b), were
reviewed for this publication: Rodin use is not limited except by the stated
terms, applicable law and third-party rights. The reference was original artwork.
The package uses the repository's MIT license for rights held by its author;
this does not claim exclusive copyright in generated output or rights to game
assets. See the repository LICENSE.

## Build

Run `python3 marketplace/packages/build_possum.py --output scratch/possum-release
--game-assembly /path/to/authorized/Assembly-CSharp.dll`. The game assembly is read
only to fingerprint it and is never archived. The helper must validate the final
archive and descriptor before publication. Release URLs remain inactive until
publication; do not insert an unpublished descriptor into the production catalog.
