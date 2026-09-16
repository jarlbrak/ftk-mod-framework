# Explicit material option contract checks

Run with `dotnet run --project tools/ai-model-pipeline/material-option-tests -c Release -p:TestGameRoot=/absolute/path/to/scratch/game`.

The tests link the actual descriptor, material policy and JSON validation code.
Material stand-ins verify default preservation, source/copy independence,
keyword/color/map changes, unrelated-property preservation, missing shader
properties, idempotence, old constructor compatibility and strict JSON booleans.
They do not prove Unity cloning, native renderer hooks, transactional rollback,
resource destruction, or visual quality. The existing transaction owns the copy
before the policy runs. Live full inventory records shared material state for
the isolated fixture's subsequent validation.
