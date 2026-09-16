# Legacy Kraken portrait framing

The initiative UI takes a fresh native `OffscreenCamera.Snapshot` of each live enemy CEL. The exact `enkrakenhead` resource has a fixed `CameraRoot/PortraitCam` at `(0, 1.77, 2.75)` looking toward negative Z. Gloamfin's large custom head fills that narrow view with a small teal patch. The native offscreen camera in `sharedassets1.assets` uses 20 degrees, near 0.3, far 30; marker translation alone cannot conservatively fit the captured custom geometry.

For an exact custom row using that resource, with no explicit portrait marker, the framework measures the single leased explicit custom skinned mesh on the owned offscreen clone. It preserves the native marker orientation and derives a fit from the actual render texture dimensions, FOV and clip planes. If needed it uniformly reduces only that disposable clone root, bounded to 10%-100%. Bone-local poses, live/source objects, native camera settings, materials and controllers are untouched. Native positioning and cleanup own the generated marker and clone. Preparation failure restores the original clone scale and destroys temporary allocations. Unsupported projection, geometry, ownership or scale conditions retain native framing.

The current gate requires unit renderer world scale because Unity 2017 BakeMesh scale behavior is not generalized by this fix. Explicit marker registrations retain priority. This is an internal exact-resource compatibility fallback, not automatic framing for all custom enemies.

Run `dotnet run --project tools/ai-model-pipeline/portrait-framing-tests/PortraitFramingTests.csproj` for linked frustum arithmetic and source-boundary checks. The portrait scope suite tests explicit-marker priority and generated-marker selection. The actual mesh transaction suite tests rejection of visual-only leases and substituted native geometry.

Live validation remains required: fresh Gloamfin initiative and enemy-HUD portraits, visible face and unclipped silhouette, first-capture aspect, source/live/bone-local immutability, clone/marker cleanup and retained mesh lease counts. Screenshots and offline tests do not prove the new Unity rendering path.
