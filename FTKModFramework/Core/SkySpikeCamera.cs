using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    // Presentation owns only the idle camera pose. Native cuts, attacks, zoom transitions and lens settings stay native.
    internal static class SkySpikeCamera
    {
        static DioramaBoat _diorama;
        static GameObject _ship;
        static Vector3 _center, _along, _across;
        static Camera _camera;
        static Vector3 _nativePosition, _appliedPosition, _smoothLook, _smoothOutward;
        static float _smoothDistance, _nativeFarClip, _appliedFarClip;
        static bool _farClipApplied;
        static Quaternion _nativeRotation, _appliedRotation;
        static bool _applied, _smoothing, _logged, _wasEstablishing;
        static float _establishRemaining, _returnStarted;
        static bool _returning;
        static bool _rendered, _renderedAuthored, _nativeReturning;
        static int _renderFrame = -1, _traceFrames, _restoredPoses, _positionMisses, _rotationMisses;
        static float _nativeReturnStarted, _nativeReturnFarClip, _renderedFarClip;
        static Vector3 _renderedPosition, _nativeReturnOffset;
        static Quaternion _renderedRotation, _nativeReturnRotation;
        static StreamWriter _frameTrace;
        static Vector3 _returnOffset;
        static Quaternion _returnRotation;
        static readonly bool Diagnostics = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_CAMERA_DIAGNOSTICS") == "1";
        static string _diagnosticState;
        static bool _lensLogged;
        static CharacterDummy _lastActor;
        static readonly List<Vector3> Points = new List<Vector3>();
        static SkySpikeAssetContract _contract;
        static bool _revealTried, _revealValid;
        static float _revealDistance, _revealViewUp, _revealMinZ, _revealMaxZ, _revealExitRemaining;
        static Vector3 _revealOutward;
        static Camera _revealCamera;
        static float _revealFov, _revealAspect, _revealNear;

        internal static void Configure(DioramaBoat diorama, GameObject ship, Vector3 center, Vector3 along, SkySpikeAssetContract contract)
        {
            Clear();
            if (Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_CAMERA") == "0") return;
            _diorama = diorama; _ship = ship; _center = center;
            _contract = contract;
            _along = along.normalized; _across = Vector3.Cross(Vector3.up, _along).normalized;
            _establishRemaining = 1.8f;
            Camera.onPreCull += BeforeRender;
            if (Diagnostics)
            {
                try
                {
                    string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "airship-camera-frames-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".csv");
                    _frameTrace = new StreamWriter(path);
                    _frameTrace.WriteLine("frame,time,authored,transition,px,py,pz,qx,qy,qz,qw,npx,npy,npz,nqx,nqy,nqz,nqw,cut,action,restored,positionMisses,rotationMisses");
                    Plugin.Log.LogInfo("[SkySpike] rendered camera trace: " + path);
                }
                catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] camera trace unavailable: " + e.Message); }
            }
        }

        internal static bool OwnsArena(DioramaBoat diorama)
        {
            return _diorama == diorama && _ship != null && _ship.activeInHierarchy;
        }

        static bool OwnsRotation(Quaternion rotation)
        {
            return SkySpikeCameraMath.SameRotation(rotation.x, rotation.y, rotation.z, rotation.w,
                _appliedRotation.x, _appliedRotation.y, _appliedRotation.z, _appliedRotation.w);
        }

        internal static void ReleasePose()
        {
            // A native coroutine may already have written a newer pose. Never undo that write.
            if (_applied && _camera != null)
            {
                if ((_camera.transform.position - _appliedPosition).sqrMagnitude >= .000001f) _positionMisses++;
                else if (!OwnsRotation(_camera.transform.rotation)) _rotationMisses++;
                else
                {
                    _camera.transform.position = _nativePosition;
                    _camera.transform.rotation = _nativeRotation;
                    _restoredPoses++;
                }
            }
            if (_farClipApplied && _camera != null && _camera.farClipPlane == _appliedFarClip)
                _camera.farClipPlane = _nativeFarClip;
            _farClipApplied = false;
            _applied = false;
        }

        internal static bool ClearFor(DioramaBoat diorama)
        {
            if (_diorama != diorama || diorama == null) return false;
            Clear();
            return true;
        }

        internal static void Clear()
        {
            Camera.onPreCull -= BeforeRender;
            ReleasePose();
            StopFrameTrace();
            _rendered = false; _nativeReturning = false; _renderFrame = -1; _traceFrames = 0; _restoredPoses = 0; _positionMisses = 0; _rotationMisses = 0;
            _diorama = null; _ship = null; _camera = null; _lastActor = null;
            _smoothing = false; _logged = false; _returning = false; _diagnosticState = null; _lensLogged = false;
            _contract = null; _revealTried = false; _revealValid = false; _revealExitRemaining = 0;
            _revealCamera = null;
        }

        internal static void Update(CameraCutManager manager)
        {
            if (_ship == null || _diorama == null) return;
            try
            {
                EncounterSession session = EncounterSession.Instance;
                if (Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_CAMERA") == "0" || !_ship.activeInHierarchy ||
                    !_diorama.gameObject.activeInHierarchy || session == null || session.m_ActiveDiorama != _diorama)
                { Clear(); return; }
                CharacterDummy actor = null;
                if (session.m_Dummies != null)
                    foreach (CharacterDummy dummy in session.m_Dummies.Values)
                        if (dummy != null && dummy.m_IsAlive && dummy.m_IsAttacking) { actor = dummy; break; }
                // Native ShowTutorial owns this reference until CloseCurrentTutorial, including More Info.
                // Keep the opening budget unspent behind the modal and let the native pose remain visible.
                FTKHelp.FTKTutorial tutorial = FTKHelp.FTKTutorial.Instance;
                if (tutorial != null && tutorial.m_CurrentFlasher != null) { Trace(manager, actor, "native tutorial"); _smoothing = false; return; }
                if (!manager.enabled || !manager.m_CameraCutEnable || CameraCutManager.IsCameraCutRunning ||
                    manager.m_SequenceName == "Victory" || !session.m_PlayerDummiesInitiated || session.m_Dummies == null)
                { Trace(manager, actor, "native cut/transition"); _smoothing = false; return; }
                if (actor == null || actor.m_ActionAnimationPlayed) { Trace(manager, actor, "native action/no actor"); _smoothing = false; return; }
                OverlayCamera overlay = OverlayCamera.Instance;
                Camera camera = overlay != null ? overlay.m_Camera : null;
                if (camera == null || !camera.isActiveAndEnabled || camera.orthographic) return;
                _camera = camera;
                if (Diagnostics && !_lensLogged)
                {
                    _lensLogged = true;
                    TraceNativeLens(camera);
                }
                if (!_logged && Vector3.Dot(camera.transform.position - _center, _across) < 0f) _across = -_across;
                bool establishing = _establishRemaining > 0f;
                Vector3 look = _center + Vector3.up * 1.275f;
                Points.Clear();
                foreach (CharacterDummy dummy in session.m_Dummies.Values)
                {
                    if (dummy == null || !dummy.m_IsAlive || !dummy.gameObject.activeInHierarchy) continue;
                    Points.Add(dummy.transform.position - Vector3.up * 0.25f);
                    Points.Add(dummy.transform.position + Vector3.up * 2.8f);
                }
                if (establishing && !_revealTried) PrepareReveal(camera, look);
                if (establishing && _revealValid && (camera != _revealCamera || camera.fieldOfView != _revealFov ||
                    camera.aspect != _revealAspect || camera.nearClipPlane != _revealNear))
                {
                    _revealValid = false; _smoothing = false; _revealExitRemaining = 0;
                    Plugin.Log.LogInfo("[SkySpike] reveal lens changed; prior close reveal retained.");
                }
                // Stay on one broadside so combat left/right never flips. Turns orbit toward the current crew.
                float side = actor.FID.IsPlayer() ? -1f : 1f;
                float slot = Mathf.Clamp(Vector3.Dot(actor.transform.position - _center, _across) / 5f, -1f, 1f);
                Vector3 outward = (_across * 0.88f + _along * (establishing ? -0.55f : side * 0.30f + slot * 0.14f) +
                    Vector3.up * (establishing ? 0.55f : 0.52f)).normalized;
                if (establishing && _revealValid) outward = _revealOutward;
                float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.8f);
                bool startSmoothing = !_smoothing;
                if (startSmoothing)
                {
                    _smoothLook = look;
                    Vector3 initialOffset = (_rendered ? _renderedPosition : camera.transform.position) - look;
                    _returning = true;
                    _returnStarted = Time.unscaledTime;
                    _returnOffset = initialOffset;
                    _returnRotation = _rendered ? _renderedRotation : camera.transform.rotation;
                    // Begin the reveal at its authored angle. Inheriting the native intro angle made the
                    // conservative first fit enormous and carried that radius through the whole opening.
                    _smoothDistance = establishing ? 4f : Mathf.Max(initialOffset.magnitude, 4f);
                    _smoothOutward = establishing ? outward : (initialOffset.sqrMagnitude > .01f ? initialOffset.normalized : outward);
                    _smoothing = true;
                }
                _smoothLook = Vector3.Lerp(_smoothLook, look, blend);
                _smoothOutward = Vector3.Slerp(_smoothOutward, outward, blend).normalized;
                Quaternion rotation = Quaternion.LookRotation(-_smoothOutward, Vector3.up);
                Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
                float distance = 4f;
                foreach (Vector3 point in Points)
                {
                    Vector3 relative = point - _smoothLook;
                    distance = Mathf.Max(distance, SkySpikeCameraMath.FitViewportDistance(Vector3.Dot(relative, right),
                        Vector3.Dot(relative, up), Vector3.Dot(relative, _smoothOutward), camera.fieldOfView, camera.aspect, 0.55f, 0.82f, 0.80f));
                }
                // Orbit around the deck, never interpolate a chord through the hull. Fit every intermediate
                // angle too. Move camera and look point along view-up so content occupies the clear upper viewport.
                float requiredDistance = distance + camera.nearClipPlane + 0.5f;
                // Retain the previous close reveal if geometry cannot supply a bounded two-band fit.
                if (establishing) requiredDistance *= 1.12f;
                _smoothDistance = startSmoothing ? requiredDistance : SkySpikeCameraMath.EaseDistance(_smoothDistance, requiredDistance, Time.unscaledDeltaTime);
                float viewUpOffset = SkySpikeCameraMath.ViewUpOffset(_smoothDistance, camera.fieldOfView, .55f, .82f);
                if (establishing && _revealValid)
                {
                    requiredDistance = _smoothDistance = _revealDistance;
                    viewUpOffset = _revealViewUp;
                    _revealExitRemaining = SkySpikeCameraMath.TransitionDuration;
                }
                else if (_revealExitRemaining > 0f)
                {
                    // Ease only the reveal's different composition back into the established idle band.
                    // Fit the destination, then ease to it without snapping at the old reveal band boundary.
                    _revealExitRemaining = Mathf.Max(0f, _revealExitRemaining - Time.unscaledDeltaTime);
                    SkySpikeCameraMath.ViewportEnvelope actors = ActorEnvelope(camera, _smoothLook, right, up, _smoothOutward);
                    float low, high;
                    actors.OffsetInterval(_smoothDistance, out low, out high);
                    viewUpOffset = Mathf.Lerp(_revealViewUp, Mathf.Clamp(viewUpOffset, low, high),
                        SkySpikeCameraMath.ReturnBlend(SkySpikeCameraMath.TransitionDuration - _revealExitRemaining));
                }
                Vector3 position = _smoothLook + _smoothOutward * _smoothDistance + up * viewUpOffset;
                if (_returning)
                {
                    float alpha = SkySpikeCameraMath.ReturnBlend(Time.unscaledTime - _returnStarted);
                    Vector3 targetOffset = position - _smoothLook;
                    float startRadius = _returnOffset.magnitude, targetRadius = targetOffset.magnitude;
                    // Interpolate an orbital direction and radius, not a chord through the deck. The native
                    // starting pose is preserved at alpha zero; final HUD composition is exact at the end of the handoff.
                    if (startRadius > .01f && targetRadius > .01f)
                        position = _smoothLook + Vector3.Slerp(_returnOffset / startRadius, targetOffset / targetRadius, alpha) *
                            Mathf.Lerp(startRadius, targetRadius, alpha);
                    rotation = Quaternion.Slerp(_returnRotation, rotation, alpha);
                    if (alpha >= 1f) _returning = false;
                }
                float nearestDepth = float.MaxValue, farthestDepth = float.MinValue;
                foreach (Vector3 point in Points)
                {
                    float depth = Vector3.Dot(point - position, rotation * Vector3.forward);
                    nearestDepth = Mathf.Min(nearestDepth, depth);
                    farthestDepth = Mathf.Max(farthestDepth, depth);
                }
                if (establishing && _revealValid)
                {
                    nearestDepth = Mathf.Min(nearestDepth, _revealDistance - _revealMaxZ);
                    farthestDepth = Mathf.Max(farthestDepth, _revealDistance - _revealMinZ);
                }
                _nativeFarClip = camera.farClipPlane;
                float farClip = SkySpikeCameraMath.FarClipForPresentation(camera.nearClipPlane, _nativeFarClip, nearestDepth, farthestDepth, _returning);
                _nativePosition = camera.transform.position; _nativeRotation = camera.transform.rotation;
                camera.transform.position = _appliedPosition = position;
                camera.transform.rotation = _appliedRotation = rotation;
                _appliedPosition = camera.transform.position; _appliedRotation = camera.transform.rotation;
                _applied = true;
                // The native 140-unit far plane clipped the first wide shot at a 239-unit radius. Extend only
                // to this frame's fitted content depth; release independently so a newer native pose is safe.
                if (farClip > _nativeFarClip)
                {
                    _appliedFarClip = farClip;
                    _farClipApplied = true;
                    camera.farClipPlane = farClip;
                }
                Trace(manager, actor, _returning ? "authored return" : (establishing ? "authored reveal" : "authored idle"));
                _establishRemaining = Mathf.Max(0f, _establishRemaining - Time.unscaledDeltaTime);
                if (!_logged || _lastActor != actor || _wasEstablishing != establishing)
                {
                    Plugin.Log.LogInfo("[SkySpike] authored camera " + (establishing ? "establishing" : "idle") +
                        ": " + (actor.FID.IsPlayer() ? "player" : "enemy") + ", distance " + requiredDistance.ToString("0.00") +
                        ", view-up offset " + viewUpOffset.ToString("0.00") + ", clip " + camera.nearClipPlane.ToString("0.00") +
                        ".." + farClip.ToString("0.00") + " (native far " + _nativeFarClip.ToString("0.00") + "), viewport y[.55,.82], look " + look + ", fighters " + (Points.Count / 2) + ", native cinematics retained.");
                    _logged = true; _lastActor = actor; _wasEstablishing = establishing;
                }
            }
            catch (Exception e)
            {
                Clear();
                Plugin.Log.LogWarning("[SkySpike] authored camera disabled after failure: " + e.Message);
            }
        }

        // Native Update and cut coroutines finish before this camera's render. Only the presentation
        // pose is composed here; the next native evaluation receives its own unmodified target.
        static void BeforeRender(Camera camera)
        {
            try { ComposeBeforeRender(camera); }
            catch (Exception e)
            {
                Clear();
                Plugin.Log.LogWarning("[SkySpike] camera render composition disabled after failure: " + e.Message);
            }
        }

        static void StopFrameTrace()
        {
            StreamWriter trace = _frameTrace;
            _frameTrace = null;
            if (trace == null) return;
            try { trace.Dispose(); }
            catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] camera trace close failed: " + e.Message); }
        }

        static void ComposeBeforeRender(Camera camera)
        {
            if (_diorama == null || OverlayCamera.Instance == null || camera != OverlayCamera.Instance.m_Camera ||
                _renderFrame == Time.frameCount) return;
            _renderFrame = Time.frameCount;
            ReleasePose();
            CameraCutManager manager = CameraCutManager.Instance;
            if (manager == null) return;
            _camera = camera;
            Vector3 nativePosition = camera.transform.position;
            Quaternion nativeRotation = camera.transform.rotation;
            Update(manager);
            if (_diorama == null) return;
            bool authored = _applied;
            if (authored) _nativeReturning = false;
            else if (_rendered && _renderedAuthored)
            {
                _nativeReturning = true;
                _nativeReturnStarted = Time.unscaledTime;
                _nativeReturnFarClip = _renderedFarClip;
                _nativeReturnOffset = _renderedPosition - nativePosition;
                _nativeReturnRotation = _renderedRotation * Quaternion.Inverse(nativeRotation);
            }
            if (_nativeReturning)
            {
                float alpha = SkySpikeCameraMath.ReturnBlend(Time.unscaledTime - _nativeReturnStarted);
                _nativePosition = nativePosition; _nativeRotation = nativeRotation;
                camera.transform.position = _appliedPosition = nativePosition + _nativeReturnOffset * (1f - alpha);
                camera.transform.rotation = _appliedRotation = Quaternion.Slerp(_nativeReturnRotation, Quaternion.identity, alpha) * nativeRotation;
                _appliedPosition = camera.transform.position; _appliedRotation = camera.transform.rotation;
                _applied = true;
                _nativeFarClip = camera.farClipPlane;
                _appliedFarClip = Mathf.Max(_nativeFarClip, Mathf.Lerp(_nativeReturnFarClip, _nativeFarClip, alpha));
                if (_appliedFarClip > _nativeFarClip) { camera.farClipPlane = _appliedFarClip; _farClipApplied = true; }
                if (alpha >= 1f) _nativeReturning = false;
            }
            _renderedPosition = camera.transform.position; _renderedRotation = camera.transform.rotation;
            _rendered = true; _renderedAuthored = authored; _renderedFarClip = camera.farClipPlane;
            if (_frameTrace != null)
            {
                try
                {
                    Vector3 p = _renderedPosition; Quaternion q = _renderedRotation;
                    _frameTrace.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1:R},{2},{3},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R},{12:R},{13:R},{14:R},{15:R},{16:R},{17:R},{18},{19},{20},{21},{22}",
                        Time.frameCount, Time.unscaledTime, authored ? 1 : 0, (_returning && authored) || _nativeReturning ? 1 : 0,
                        p.x, p.y, p.z, q.x, q.y, q.z, q.w, nativePosition.x, nativePosition.y, nativePosition.z,
                        nativeRotation.x, nativeRotation.y, nativeRotation.z, nativeRotation.w,
                        CameraCutManager.IsCameraCutRunning ? 1 : 0, _lastActor != null && _lastActor.m_ActionAnimationPlayed ? 1 : 0,
                        _restoredPoses, _positionMisses, _rotationMisses));
                    if (++_traceFrames % 120 == 0) _frameTrace.Flush();
                    if (_traceFrames >= 60000) StopFrameTrace();
                }
                catch (Exception e)
                {
                    StopFrameTrace();
                    Plugin.Log.LogWarning("[SkySpike] camera frame trace stopped: " + e.Message);
                }
            }
        }

        internal static void BeforeNativeUpdate()
        {
            // Auto-look initializes its tracking origin inside Update. Give it the last visible pose
            // on action entry instead of the hidden idle baseline that presentation had replaced.
            if (_applied && _renderedAuthored && !CameraCutManager.IsCameraCutRunning && _lastActor != null && _lastActor.m_ActionAnimationPlayed)
                BeginNativeCut();
            else ReleasePose();
        }

        internal static void ExitToNativeZoom()
        {
            Camera camera = _camera;
            bool retainVisible = camera != null && _rendered && _applied &&
                (camera.transform.position - _appliedPosition).sqrMagnitude < .000001f &&
                OwnsRotation(camera.transform.rotation);
            Vector3 position = _renderedPosition;
            Quaternion rotation = _renderedRotation;
            Clear();
            // StartZoomOut captures its start immediately after this prefix. Ordinary teardown still
            // restores owned native state, but a visible zoom must start at the frame the player saw.
            if (retainVisible && camera != null)
            {
                camera.transform.position = position;
                camera.transform.rotation = rotation;
            }
        }

        internal static void BeginNativeCut()
        {
            if (_camera == null || !_rendered || CameraCutManager.IsCameraCutRunning ||
                (!_renderedAuthored && !_nativeReturning)) return;
            ReleasePose();
            _camera.transform.position = _renderedPosition;
            _camera.transform.rotation = _renderedRotation;
            // A newly seeded native cut must not receive the previous handoff's correction twice.
            _nativeReturning = false;
            _renderedAuthored = true;
            _smoothing = false;
        }

        static SkySpikeCameraMath.ViewportEnvelope ActorEnvelope(Camera camera, Vector3 look, Vector3 right, Vector3 up, Vector3 outward)
        {
            SkySpikeCameraMath.ViewportEnvelope envelope = new SkySpikeCameraMath.ViewportEnvelope(camera.fieldOfView, camera.aspect, .55f, .82f, .8f);
            foreach (Vector3 point in Points)
            {
                Vector3 p = point - look;
                envelope.Add(Vector3.Dot(p, right), Vector3.Dot(p, up), Vector3.Dot(p, outward));
            }
            return envelope;
        }

        static void PrepareReveal(Camera camera, Vector3 look)
        {
            _revealTried = true;
            try
            {
                if (_contract == null || Points.Count == 0) return;
                _revealOutward = (_across * .88f + _along * .1f + Vector3.up * .55f).normalized;
                Quaternion rotation = Quaternion.LookRotation(-_revealOutward, Vector3.up);
                Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
                SkySpikeCameraMath.ViewportEnvelope actors = ActorEnvelope(camera, look, right, up, _revealOutward);
                SkySpikeCameraMath.ViewportEnvelope geometry = new SkySpikeCameraMath.ViewportEnvelope(camera.fieldOfView, camera.aspect, .24f, .82f, .85f);
                int count = 0;
                foreach (MeshFilter filter in _ship.GetComponentsInChildren<MeshFilter>())
                {
                    Renderer renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable || mesh.vertexCount > 250000 || (count += mesh.vertexCount) > 500000)
                        throw new InvalidOperationException("reveal mesh is unreadable or exceeds the bounded vertex budget");
                    SkySpikeAssetContract.Rotor rotor = null;
                    foreach (SkySpikeAssetContract.Rotor candidate in _contract.Rotors)
                        if (SkySpikeAssetContract.MatchesNode(filter.name, candidate.Node)) { rotor = candidate; break; }
                    Vector3 pivot = Vector3.zero, axis = Vector3.zero;
                    if (rotor != null)
                    {
                        pivot = _ship.transform.TransformPoint(new Vector3(rotor.Pivot[0], rotor.Pivot[1], rotor.Pivot[2]));
                        axis = _ship.transform.TransformDirection(new Vector3(rotor.Axis[0], rotor.Axis[1], rotor.Axis[2])).normalized;
                    }
                    Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 p = matrix.MultiplyPoint3x4(vertex);
                        if (rotor == null)
                        {
                            p -= look;
                            geometry.Add(Vector3.Dot(p, right), Vector3.Dot(p, up), Vector3.Dot(p, _revealOutward));
                        }
                        else
                        {
                            Vector3 axial = axis * Vector3.Dot(p - pivot, axis);
                            Vector3 a = p - pivot - axial, b = Vector3.Cross(axis, a), center = pivot + axial - look;
                            geometry.AddSweep(Vector3.Dot(center, right), Vector3.Dot(center, up), Vector3.Dot(center, _revealOutward),
                                Vector3.Dot(a, right), Vector3.Dot(a, up), Vector3.Dot(a, _revealOutward),
                                Vector3.Dot(b, right), Vector3.Dot(b, up), Vector3.Dot(b, _revealOutward));
                        }
                    }
                }
                if (!SkySpikeCameraMath.TryFitReveal(actors, geometry, camera.nearClipPlane, out _revealDistance, out _revealViewUp))
                    throw new InvalidOperationException("no bounded two-band reveal fit");
                _revealMinZ = (float)geometry.MinZ; _revealMaxZ = (float)geometry.MaxZ;
                _revealCamera = camera; _revealFov = camera.fieldOfView; _revealAspect = camera.aspect; _revealNear = camera.nearClipPlane;
                _revealValid = true;
                Plugin.Log.LogInfo("[SkySpike] authored reveal cached: " + count + " visible vertices with full rotor sweeps, distance " +
                    _revealDistance.ToString("0.00") + ", view-up offset " + _revealViewUp.ToString("0.00") +
                    "; ship viewport x[.075,.925] y[.24,.82], actor y[.55,.82].");
            }
            catch (Exception e)
            {
                _revealValid = false;
                Plugin.Log.LogInfo("[SkySpike] geometry reveal unavailable; prior close reveal retained: " + e.Message);
            }
        }

        static void TraceNativeLens(Camera camera)
        {
            try
            {
                // The native effect lives in UnityScript-firstpass, outside the framework's compile references.
                Type type = AccessTools.TypeByName("DepthOfFieldScatter");
                Behaviour dof = type != null ? camera.GetComponent(type) as Behaviour : null;
                string lens = "FOV=" + camera.fieldOfView + ", near=" + camera.nearClipPlane + ", far=" + camera.farClipPlane;
                if (dof == null) lens += "; DOF absent";
                else
                {
                    lens += "; DOF enabled=" + dof.enabled + ", active=" + dof.isActiveAndEnabled;
                    foreach (string field in new[] { "focalLength", "focalSize", "aperture", "maxBlurSize", "nearBlur" })
                        lens += ", " + field + "=" + AccessTools.Field(type, field).GetValue(dof);
                    Transform focal = AccessTools.Field(type, "focalTransform").GetValue(dof) as Transform;
                    lens += ", focalTransform=" + (focal != null ? focal.name : "none");
                }
                Plugin.Log.LogInfo("[SkySpike camera diagnostic] utc=" + DateTime.UtcNow.ToString("o") +
                    " native lens before authored view: " + lens);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] native lens diagnostic unavailable: " + e.Message); }
        }

        internal static void StopDeadDefenderTracking(CameraCutManager manager, bool tracking, CharacterDummy defender)
        {
            if (!tracking || defender == null || defender.m_IsAlive || CameraCutManager.IsCameraCutRunning ||
                _diorama == null || !OwnsArena(_diorama)) return;
            try
            {
                EncounterSession session = EncounterSession.Instance;
                if (session == null || session.m_ActiveDiorama != _diorama || !_diorama.gameObject.activeInHierarchy ||
                    manager == null || !manager.enabled || !manager.m_CameraCutEnable ||
                    manager != CameraCutManager.Instance ||
                    Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_CAMERA") == "0") return;
                // Native auto-look checks life only when it starts. Stop before Update follows the dead
                // skeleton below the deck, retaining the last native shot and all attack/cut authority.
                manager.DoAutoLookAt(false);
                if (Diagnostics)
                    Plugin.Log.LogInfo("[SkySpike camera diagnostic] utc=" + DateTime.UtcNow.ToString("o") +
                        " t=" + Time.unscaledTime.ToString("0.000") + " native dead-defender tracking stopped; defender=" +
                        defender.FID + "; sequence=" + manager.m_SequenceName);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[SkySpike] dead-defender camera guard failed: " + e.Message);
            }
        }

        static void Trace(CameraCutManager manager, CharacterDummy actor, string ownership)
        {
            if (!Diagnostics) return;
            string state = ownership + "; sequence=" + manager.m_SequenceName + "; cut=" + CameraCutManager.IsCameraCutRunning +
                "; action=" + (actor != null && actor.m_ActionAnimationPlayed) + "; actor=" + (actor != null ? actor.FID.ToString() : "none");
            if (state == _diagnosticState) return;
            _diagnosticState = state;
            Camera camera = OverlayCamera.Instance != null ? OverlayCamera.Instance.m_Camera : null;
            Plugin.Log.LogInfo("[SkySpike camera diagnostic] utc=" + DateTime.UtcNow.ToString("o") + " t=" + Time.unscaledTime.ToString("0.000") + " " + state +
                (camera != null ? "; position=" + camera.transform.position + "; rotation=" + camera.transform.eulerAngles : "; no camera"));
        }
    }

    [HarmonyPatch(typeof(CameraCutManager), "CheckAutoCameraLookAt")]
    internal static class SkySpikeCameraDeadDefenderPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }
        static void Postfix(CameraCutManager __instance, bool ___m_AutoLookAtOn, CharacterDummy ___m_Defender)
        { SkySpikeCamera.StopDeadDefenderTracking(__instance, ___m_AutoLookAtOn, ___m_Defender); }
    }

    [HarmonyPatch(typeof(CameraCutManager), "Update")]
    internal static class SkySpikeCameraUpdatePatch
    {
        static bool Prepare() { return SkySpike.Enabled; }
        static void Prefix() { SkySpikeCamera.BeforeNativeUpdate(); }
    }

    [HarmonyPatch(typeof(OverlayCamera), "StartZoomOut")]
    internal static class SkySpikeCameraExitPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }
        static void Prefix() { SkySpikeCamera.ExitToNativeZoom(); }
    }

    [HarmonyPatch(typeof(CameraCutManager), "PlayCameraCutSequence")]
    internal static class SkySpikeCameraCutPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }
        static void Prefix() { SkySpikeCamera.BeginNativeCut(); }
    }
}
