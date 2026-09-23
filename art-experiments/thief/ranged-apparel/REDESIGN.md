# Thief open-face apparel candidate

This is an original art candidate for review, following [the current visual inspiration](../art-direction-v2/INSPIRATION.md). It supersedes the cap and raised-hood experiments. Native review records in `native-fit-review.json` and `../redesign-review/` describe earlier bytes and do not accept these assets.

`redesign.py` defines all seven coats, neck pieces, boots and charms. Street uses warm leather over slate cloth, a tapered waist, a short repaired hem, one tool pouch and one compact shoulder fold. Its low oxblood neckerchief has no crown shell. Later bands add deliberate changes in hem length, panel construction, cowl folds, tool storage and boot construction. Every crown remains open; actual native hair visibility depends on the equipped item and must be observed in game.

| Tier | Coat | Headpiece | Boots | Charm |
| --- | --- | --- | --- | --- |
| Street | Short leather jerkin, broad repair, one pouch | Low oxblood neckerchief, short tucked end | Low wrapped shoes | Bent copper token |
| Burglar | Dark overlapping jack, slightly longer hem | Side-tied slate scarf | Ankle boots, broad strap | Flat brass pick |
| Guild | Clean blue panels, deliberate split hem | Low blue cowl, small fastening | Fitted calf boots | Rook-stamped guild token |
| Masterwork | Layered blue-gray chest and waist, both shoulder folds | Stepped low cowl | Reinforced toe and taller cuff | Faceted silver rook |
| Locksmith | Petrol leather, one broad tool pouch and three visible picks | Asymmetric clasped low cowl | Reinforced work boots | Three keys with different wards |
| Nightblade | Short angled charcoal hem, sparse trim, angled collar | Wine scarf and dark lowered cowl | Close black boots with split cuffs | Cold wax and blackened candle socket |
| Wayfarer | Moss coat, longer rear skirt, broad rear reinforcement | Folded travel scarf | Tall laced boots | Compass with clear north pointer |

The exact female 19-joint and male 26-joint coat palettes and shared ten-joint boots are retained. The builder requires the previously recorded binding file hashes and renderer identities. The independent validator compares exported joint names and inverse matrices to those inputs; it can additionally compare the previous GLB bytes. Native surfaces, textures, weights, bounds and animation data are not used to construct art.

All 28 items use `thief-apparel-palette.png`, including equipped and display models. Bows retain their own existing palette and files. Content IDs, model filenames and native renderer bindings remain stable. The item displayed as Street Cap is now Street Neckerchief; its stable ID remains `thief_hood_street`.

## Review evidence

`apparel-contact-sheet.png` shows all 28 icons. `apparel-review/` contains all seven tiers and both coat branches in front, three-quarter, side and rear views. All 56 views share a 2.40-unit orthographic span, 640 by 640 resolution and the same lights. The 14 three-quarter files use `armor-{tier}-{male|female}.png`. `apparel-review/manifest.json` records exact inputs, hashes, camera settings and the headwear assembly convention.

These are exported apparel renders in the neutral bind pose. Native bodies, faces, hair, hands and backpacks are omitted. Headwear is placed at the recorded authored Head_M rest landmark for comparison, which does not establish the native crown mount. No mannequin or fabricated face stands in for native fit.

The initial front and side comparison exposed raised scarf ends that read as a bow. The final candidate replaces them with a continuous low folded band, a small side knot and one short tucked end. This corrects the offline silhouette; actual face clearance and neck motion remain native checks.

Offline checks cover original source equality, outward nondegenerate surfaces, complete normalized skin weights, exact bind preservation, icon framing and package hashes. Art approval, native hair visibility, male/female and race fit, ordinary motion, equipment changes, displays, and lifecycle remain separate gates owned by the native review task.
