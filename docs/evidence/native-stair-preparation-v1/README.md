# Native staircase preparation vote

This trial reached floor 1, room 0 through two distinct native Ready phases.
The first vote entered the Stair encounter at floor 0, room 5. A later screenshot
showed the new Next Level preparation panel with Party Rest and Ready controls.
The native `uiExploreDungeonMenu.Show` implementation creates a new Ready session;
its `UseInventoryContinue` callback reaches the native dungeon stair decision.
Submitting that preparation vote once led to the next floor's ordinary combat.

[Validation and lossless observations](validation.json) preserve the initial
uncertainty and the later source finding separately. The screenshot was viewed
in the task but is not retained as a local image artifact. Native source files
remain in ignored scratch; only source hashes and findings are packaged.

Do not infer a new vote from an unchanged Ready button or room number. The
existing fixture did not expose the continuation object's identity. Automated
repetition needs an observation of the actual preparation menu and its current
native owner/continuation; this trial used the visible menu and source analysis.
