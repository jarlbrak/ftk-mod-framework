# Possum 1.0.0 validation

Tested on macOS against game assembly SHA-256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.
Framework candidate 1.1.0 reported SELF-TEST PASS.

Final body SHA-256: `5e5b6666d349a8f6e8eb1f63e4bbd53963185a9d5d9496d71544341be3b8ec38`.
Final package SHA-256: `c10a86cc0eeb80cee6ca6b9aeccaebf343469a61cb4e46562c925c59fbfc2b40`.

Native character selection cycled from Cat to Possum. Front, back and three-quarter
views were inspected. A Hunter using the final smaller-hand mesh entered the
world and a nearby Beastman Warrior encounter through ordinary game controls.
An 80-frame capture at 10 fps observed cidle_bow, attack_bow and return to
cidle_bow. Enemy HP changed from 9 to 5 and the turn advanced to the Scholar.
No health, damage, equipment or position fixtures were applied. Tutorial prompts
were suppressed only for this process; capture used fixed-step timing.

The package has fourteen native class bindings. Other classes, equipment swaps,
hit/death, save/reload and online co-op remain unverified. The tail follows the
torso. The banner is generated promotional artwork based on the real final
Blender render, not gameplay evidence. Raw local captures are retained outside
the committed source.
