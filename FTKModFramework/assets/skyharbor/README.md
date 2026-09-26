# Skyharbor presentation assets

Original generated and authored scenery for the framework's front-end background.
`manifest.json` records the packaged resource hashes, mesh budgets and duplicate-texture
aliases. `provenance.json` records the original source generation identities and hashes.
No game geometry, textures or assemblies are included.

The scene is compressed JSON with Unity coordinates and indexed mesh data. Six unique
PNG textures include the distant sky plate; the citadel, dock, cliff and airship are real
3D geometry. Byte-identical source textures share one packaged resource and runtime texture.

The framework embeds the gzip and PNG resources directly in its DLL. Existing launcher
updates therefore deliver scenery and renderer together without separate file installation.
The loader owns its meshes, textures, materials, lights, canvas, camera and render target,
and restores native presentation if loading fails.

Repackage from the original approved art workspace with:

```sh
python3 tools/skyharbor-assets/package.py "$APPROVED_SCENE_DIRECTORY" FTKModFramework/assets/skyharbor --provenance "$SOURCE_BUILD_RECORD"
```

The build record's directory must also contain `fortress-arrival-concept-v1.png`.
Source work files are authoring inputs, not needed to build or run the framework.
Validate checked-in resources against the actual built DLL with:

```sh
dotnet run --project FTKModFramework/Tests/Skyharbor -c Release -- FTKModFramework/assets/skyharbor FTKModFramework/bin/Release/net35/FTKModFramework.dll
```

See [Skyharbor](../../../docs/SKYHARBOR.md) for presentation scope and configuration.
