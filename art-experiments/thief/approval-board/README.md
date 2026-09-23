# Thief art approval board

`thief-art-approval.png` presents every authored equipment family in one image:
seven male and female full sets, seven paired daggers, seven bows, three artifact
weapons, and all 46 distinct package icons. Action entries reuse their associated
weapon icon. The adjacent JSON receipt records the package definition hash and
the SHA256 of every tile source. `build.py --require-complete` fails if a tile is
missing.

The armor tiles show exported apparel on a neutral bind pose without native
face, hair, body, or hands. The weapon tiles show exported geometry at a shared
camera scale. Source assets and additional front, side, back, and equipped views
are in `../ranged-apparel/` and `../full-weapons/`. The `renders/` directory holds
byte-identical copies of the 14 armor and 17 primary weapon previews used by
the board; supplementary weapon views may also be present there.

`native-street-male.png` is a separate native fit check. It shows the actual
avatar, including player appearance settings outside these authored assets.
Native fit across the remaining tiers, animation, and bow draw remain gameplay
review gates after art approval.
