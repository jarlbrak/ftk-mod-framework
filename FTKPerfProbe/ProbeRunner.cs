using System;
using System.Collections;
using System.Diagnostics;
using BepInEx.Logging;
using UnityEngine;

namespace FTKPerfProbe
{
    /// <summary>
    /// Drives the per-frame roll-up. A WaitForEndOfFrame coroutine snapshots the bucket accumulators
    /// + GC counters each frame (a deterministic frame-end point), pushes to FrameStats, and feeds the
    /// active CaptureSession. Also reads hotkeys (Update) and hosts the overlay (OnGUI).
    /// </summary>
    internal sealed class ProbeRunner : MonoBehaviour
    {
        private ProbeConfig _cfg;
        private ManualLogSource _log;
        private readonly FrameStats _stats = new FrameStats(600);
        private readonly GcSample _gc = new GcSample();
        private Overlay _overlay;
        private CaptureSession _capture;
        private bool _showOverlay;
        private int _frame;

        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        private GcFrame _lastGc;
        private int _idCalls, _pmCalls, _canvasCalls, _photonCalls;
        private double _idMs, _owMs, _pmMs, _canvasMs, _photonMs;

        public void Init(ProbeConfig cfg, ManualLogSource log)
        {
            _cfg = cfg;
            _log = log;
            _overlay = new Overlay();
            _capture = new CaptureSession(cfg, log);
            _showOverlay = cfg.ShowOverlayOnStart.Value;
            StartCoroutine(EndOfFrameLoop());
        }

        private void Update()
        {
            if (_cfg.OverlayKey.Value.IsDown()) _showOverlay = !_showOverlay;
            if (_cfg.CaptureKey.Value.IsDown()) _capture.Toggle();
            if (_cfg.CensusKey.Value.IsDown()) SceneCensus.Dump(_cfg, _log);
        }

        private IEnumerator EndOfFrameLoop()
        {
            WaitForEndOfFrame eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                SnapshotFrame();
            }
        }

        private void SnapshotFrame()
        {
            _frame++;
            double dtMs = Time.unscaledDeltaTime * 1000.0;
            double fps = dtMs > 0.0 ? 1000.0 / dtMs : 0.0;
            _stats.Push(dtMs);

            _lastGc = _gc.Update(GC.CollectionCount(0), GC.GetTotalMemory(false));

            int idC, owC, pmC, canC, phC;
            long idT, owT, pmT, canT, phT;
            Buckets.Id.SnapshotAndReset(out idC, out idT);
            Buckets.Ow.SnapshotAndReset(out owC, out owT);
            Buckets.Pm.SnapshotAndReset(out pmC, out pmT);
            Buckets.Canvas.SnapshotAndReset(out canC, out canT);
            Buckets.Photon.SnapshotAndReset(out phC, out phT);

            _idCalls = idC; _pmCalls = pmC; _canvasCalls = canC; _photonCalls = phC;
            _idMs = idT * TicksToMs; _owMs = owT * TicksToMs;
            _pmMs = pmT * TicksToMs; _canvasMs = canT * TicksToMs; _photonMs = phT * TicksToMs;

            if (_capture.IsActive)
            {
                CaptureRow row = new CaptureRow
                {
                    Frame = _frame, UnscaledDtMs = dtMs, Fps = fps,
                    Gc0Delta = _lastGc.Gc0Delta, GcFired = _lastGc.GcFired,
                    HeapBytes = _lastGc.HeapBytes, AllocEstBytes = _lastGc.AllocEstBytes,
                    IdCalls = idC, IdMs = _idMs, OwMs = _owMs,
                    PmCalls = pmC, PmMs = _pmMs,
                    CanvasCalls = canC, CanvasMs = _canvasMs,
                    PhotonCalls = phC, PhotonMs = _photonMs
                };
                _capture.Record(row, _stats);
            }
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;
            _overlay.Draw(_stats, _lastGc, _idMs, _idCalls, _owMs, _pmMs, _pmCalls,
                _canvasMs, _canvasCalls, _photonMs, _photonCalls, _capture.IsActive);
        }
    }
}
