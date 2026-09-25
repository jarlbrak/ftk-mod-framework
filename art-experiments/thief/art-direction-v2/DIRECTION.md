# Thief costume direction

Design proposal only. Production meshes and package assets are unchanged.

`thief-costume-concept.png` is an AI-generated visual study made with the built-in image generation tool, using the current Street native three-quarter and side captures as references. It is not a native render or an implementation target to trace. The useful ideas are the broad cloth overlap, restrained accent placement and consistent leather treatment. The generated hood is still larger than the compact target, the native face and proportions drift, and blade bevels remain too bright and wide. Its labeled thumbnail is illustrative rather than a measured native 120-pixel capture. These differences must be corrected during a single-outfit native prototype.

## Observed problems

Reviewed the Street native three-quarter, side and back captures, plus Nightblade front and three-quarter captures in `../redesign-review/`. The native face and backpack provide a useful comparison within the same rendered image. A local native party-select reference also shows Scholar and Hunter costumes; it is a reference only and is not redistributed here.

- The cap is a nearly regular polygon dome with a thick horizontal band. Its symmetry and equal facets suggest a rigid bowl rather than folded cloth.
- The torso has a narrow, straight outline. Small lapels, long thin strap, split vest tips and tiny pouches divide it into unrelated pieces without changing the overall silhouette.
- Bracers are large hard boxes with multiple thin straps. Exposed bands at the wrists and elbows interrupt the clothing and make it look assembled from primitives.
- The boots are flatter and narrower than the prominent hands and cap. Their bright cuffs make them read as another separate component.
- Daggers have broad leaf outlines and extremely bright bevels around dark centers. This creates an ivory frame that competes with the face, especially in the rear view. Front locomotion views sometimes reduce them to bright wedges.
- Changing blue to charcoal and adding a scarf does not create a distinct higher-tier silhouette. Additional tiny fastenings will not solve that problem at ordinary game distance.

The native comparison suggests broad continuous clothing masses, strong head shapes, and a few clear color breaks. The goal is deliberate low-poly construction, with each plane describing fabric or a functional edge.

## Recommended direction: compact travelling burglar

Keep the native expressive face and exaggerated hand/head proportions. Give the outfit three readable masses: compact head covering, tapered short jerkin, and sturdy boots. One small shoulder drape supplies asymmetry. A single useful pouch communicates the profession.

Prototype a compact asymmetric hood with a shallow face opening and three broad fold planes. The face must remain open. The previous raised hood was rejected for bulk, so this is a new silhouette study with a fit risk, not approval to restore that mesh. If scalp clearance forces a large shell, use a slouched cap with one broad fold and a lowered cowl instead. Never enlarge the complete crown uniformly to hide a local intersection.

The jerkin should have one broad diagonal overlapping front, a clear waist, and a short split hem. Avoid long narrow lapels and needle-thin decorative straps. Keep a single broad belt, one simple buckle and one blocky pouch. Use one strap per bracer and boots that share the same leather palette and construction language.

Palette starting point: approximately 60% charcoal/slate cloth, 20% muted teal, 17% warm brown leather and 3% restrained brass/oxblood accents by visible outfit area. These are art targets, not sampled colors or runtime values. Separate materials by broad value and hue changes. Broad blade faces should remain visibly darker than the skin highlights; reserve the brightest steel for a narrow cutting edge.

## Paired dagger direction

The two-handed equipment identity is a pair of hand daggers, matching the existing dual route. Keep one knife in each native hand. No change to actions or power budget is proposed.

Use a straight spine, a shallow clipped point and one narrow silver bevel. Start with blade width around half a native palm width; size the complete knife to roughly one forearm. These are initial visual ratios to adjust on the posed native chassis. Keep the grip inside the fist and use a short bolster, with no large guard or ornamental pommel. Preserve a continuous connection from grip to blade.

Progression should improve the precision of that same construction: cleaner profile, longer clipped point, darker grip wrap, a small functional brass collar. Reserve strong silhouette departures for artifacts. Do not rely on ever-larger blades, luminous edge borders or additional tiny components.

## Review sequence

1. Review one concept at full size and at 96 and 160 pixels in character height. Require recognizable head, torso and boot masses in grayscale. The face should remain the primary focus, with the weapon readable as the secondary focus.
2. Build one Street outfit and one dagger pair on the actual native chassis. Compare front, profile and back with the current version under identical camera and lighting. Review plain shaded geometry before adding small accents.
3. Capture the candidate at ordinary gameplay camera distance. Review the native face aperture, shoulder profile, waist taper and weapon value hierarchy. The concept image cannot prove these results.
4. Observe complete ordinary attack, hit, idle and locomotion intervals, plus female and alternate profile fit. Check hand contact, blade direction, head clearance and silhouette throughout the motion.
5. Expand to progression tiers only after the single outfit and weapon have visual acceptance. Show each tier together at game scale so progression is apparent without reading its item name.

Mesh validators establish technical properties. They do not approve proportion, character, material appearance or visual quality.
