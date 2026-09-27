using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FTKPerfProbe
{
    /// <summary>Bounded in-memory sampling, followed by CSV serialization after sampling stops.</summary>
    internal sealed class CaptureSession
    {
        private const int MaxFrames = 120000;
        private readonly ProbeConfig _cfg;
        private readonly ManualLogSource _log;
        private readonly ProbeCounts _coverage;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private CaptureBuffer _buffer;
        private string _path, _metadata;
        private int _warmupFrames, _seconds;
        private float _endTime;

        public bool IsActive { get; private set; }
        public string LastPath { get; private set; }
        public string LastError { get; private set; }

        public CaptureSession(ProbeConfig cfg, ManualLogSource log, ProbeCounts coverage)
        { _cfg = cfg; _log = log; _coverage = coverage; }

        public void Toggle(bool overlayVisible) { if (IsActive) Stop("manual"); else Start("manual", _cfg.CaptureSeconds.Value, overlayVisible); }

        public bool Start(string scenario, int seconds, bool overlayVisible)
        {
            if (IsActive) return false;
            LastError = null;
            LastPath = null;
            try
            {
                if (seconds < 1 || seconds > 3600) throw new ArgumentOutOfRangeException("seconds");
                string dir = _cfg.OutputDir.Value;
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "capture-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", Inv) +
                    "-" + System.Guid.NewGuid().ToString("N") + ".csv");
                _buffer = new CaptureBuffer(MaxFrames);
                _metadata = Metadata(scenario, seconds, overlayVisible);
                _seconds = seconds;
                // Exclude setup allocations and the following delta-time interval from the capture.
                _warmupFrames = 2;
                IsActive = true;
                _log.LogInfo("capture armed -> " + _path + " (" + seconds + "s)");
                return true;
            }
            catch (Exception e)
            {
                LastError = e.ToString();
                _log.LogError("capture start failed: " + e);
                IsActive = false;
                _buffer = null;
                return false;
            }
        }

        public void Record(CaptureRow row)
        {
            if (!IsActive) return;
            if (_warmupFrames > 0)
            {
                _warmupFrames--;
                _endTime = Time.realtimeSinceStartup + _seconds;
                return;
            }
            _buffer.TryAdd(row);
            if (_buffer.Count == _buffer.Capacity) Stop("capacity reached");
            else if (Time.realtimeSinceStartup >= _endTime) Stop("window elapsed");
        }

        private string Metadata(string scenario, int seconds, bool overlayVisible)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("capture_schema=1");
            text.AppendLine("utc=" + DateTime.UtcNow.ToString("o", Inv));
            text.AppendLine("scenario=" + (scenario ?? "").Replace('\r', ' ').Replace('\n', ' '));
            text.AppendLine("requested_seconds=" + seconds + " capacity_frames=" + MaxFrames);
            text.AppendLine("scene=" + SceneManager.GetActiveScene().name);
            text.AppendLine("machine=" + SystemInfo.deviceModel + " os=" + SystemInfo.operatingSystem);
            text.AppendLine("cpu=" + SystemInfo.processorType + " gpu=" + SystemInfo.graphicsDeviceName);
            text.AppendLine("graphics=" + Screen.width + "x" + Screen.height + " fullscreen=" + Screen.fullScreen +
                " quality=" + QualitySettings.GetQualityLevel() + " vsync=" + QualitySettings.vSyncCount +
                " target_fps=" + Application.targetFrameRate);
            text.AppendLine("probe=" + Plugin.Version + " overlay_at_start=" + overlayVisible);
            text.AppendLine("attached_probes: id=" + _coverage.Id + " overworld=" + _coverage.Ow +
                " playmaker=" + _coverage.Pm + " canvas=" + _coverage.Canvas + " photon=" + _coverage.Photon);
            text.AppendLine("alloc_est_bytes is positive managed-heap growth, not total allocation; -1 on GC frames.");
            text.AppendLine("bucket timings include nested calls and instrumentation overhead; do not sum as exclusive CPU time.");
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                text.AppendLine("assembly=" + assembly.FullName + " mvid=" + assembly.ManifestModule.ModuleVersionId);
            return text.ToString();
        }

        public void Stop(string why)
        {
            if (!IsActive) return;
            IsActive = false;
            try
            {
                CaptureSummary stats = _buffer.Summarize();
                using (StreamWriter writer = new StreamWriter(_path, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine(CsvFormat.Header);
                    for (int i = 0; i < _buffer.Count; i++) writer.WriteLine(CsvFormat.Row(_buffer[i]));
                }
                string summary = "capture done (" + why + "): frames=" + _buffer.Count +
                    " meanMs=" + stats.MeanMs.ToString("F3", Inv) +
                    " p50Ms=" + stats.P50Ms.ToString("F3", Inv) +
                    " p95Ms=" + stats.P95Ms.ToString("F3", Inv) +
                    " p99Ms=" + stats.P99Ms.ToString("F3", Inv) +
                    " maxMs=" + stats.MaxMs.ToString("F3", Inv) +
                    " gcFrames=" + stats.GcFrames + " gcCollections=" + stats.GcCollections;
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(_path), Path.GetFileNameWithoutExtension(_path) + "-summary.txt"), _metadata + summary + Environment.NewLine);
                LastPath = _path;
                _log.LogInfo(summary + " -> " + _path);
            }
            catch (Exception e) { LastError = e.ToString(); _log.LogError("capture stop failed: " + e); }
            finally { _buffer = null; }
        }
    }
}
