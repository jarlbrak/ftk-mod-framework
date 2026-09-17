# Tooling instructions

Read the root `AGENTS.md` first.

- Tools here are authoring and evidence infrastructure, not shipped runtime. They do not target
  `net35` and may use the host Python or Go toolchain.
- A tool that writes a committed ledger is a generator. Change the generator and regenerate.
  Never hand-edit its output to make a check pass.
- Keep tool output deterministic for the same inputs. Record source hashes and route identity so a
  reviewer can tell what a ledger actually covers.
- A tool run is offline evidence. It never substitutes for the live-game gate it prepares.
- Subtrees add their own instructions. Follow `tools/ai-model-pipeline/AGENTS.md` when working there.
