# Scoped enemy portrait checks

Run `dotnet run --project tools/ai-model-pipeline/portrait-tests -c Release -p:TestGameRoot=/absolute/path/to/scratch/game`.

These tests link the actual registry, Harmony patch methods and profile path
validator with minimal Unity/DB/Harmony stand-ins. They check exact custom-row
identity, safe proper-child paths, native-name uniqueness, live owner pairing,
row-to-CEL forwarding, unresolved nested captures, exception finalizers, independent
camera scopes, argument preservation on failure and native-source exclusion.
They do not run Harmony interception or Unity's native portrait positioning,
rendering, destruction or actual UI call sites. Both turn-order and enemy-panel
portraits still require live acceptance, with native framing and non-opt-in
portraits preserved.
