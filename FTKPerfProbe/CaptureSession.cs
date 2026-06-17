using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace FTKPerfProbe
{
    /// <summary>
    /// Fixed-window per-frame CSV writer. Toggle() starts/stops; auto-stops after CaptureSeconds.
    /// On stop, writes a one-line summary to the log and a "-summary.txt" sibling file.
    /// </summary>
    internal sealed class CaptureSession
    {
        private readonly ProbeConfig _cfg;
        private readonly ManualLogSource _log;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _counter;

        private StreamWriter _writer;
        private string _path;
        private FrameStats _stats;
        private int _frames;
        private int _gcFrames;
        private double _sumMs;
        private double _maxMs;
        private float _endTime;

        public bool IsActive { get; private set; }

        public CaptureSession(ProbeConfig cfg, ManualLogSource log) { _cfg = cfg; _log = log; }

        public void Toggle() { if (IsActive) Stop("manual"); else Start(); }

        private void Start()
        {
            try
            {
                string dir = _cfg.OutputDir.Value;
                Directory.CreateDirectory(dir);
                _counter++;
                _path = Path.Combine(dir, "capture-" + _counter + ".csv");
                _writer = new StreamWriter(_path, false, new UTF8Encoding(false));
                _writer.WriteLine(CsvFormat.Header);
                _frames = 0; _gcFrames = 0; _sumMs = 0; _maxMs = 0; _stats = null;
                _endTime = Time.unscaledTime + _cfg.CaptureSeconds.Value;
                IsActive = true;
                _log.LogInfo("capture started -> " + _path + " (" + _cfg.CaptureSeconds.Value + "s)");
            }
            catch (Exception e) { _log.LogError("capture start failed: " + e); IsActive = false; }
        }

        public void Record(CaptureRow row, FrameStats stats)
        {
            if (!IsActive) return;
            try
            {
                _writer.WriteLine(CsvFormat.Row(row));
                _stats = stats;
                _frames++;
                if (row.GcFired) _gcFrames++;
                _sumMs += row.UnscaledDtMs;
                if (row.UnscaledDtMs > _maxMs) _maxMs = row.UnscaledDtMs;
                if (Time.unscaledTime >= _endTime) Stop("window elapsed");
            }
            catch (Exception e) { _log.LogError("capture record failed: " + e); Stop("error"); }
        }

        private void Stop(string why)
        {
            IsActive = false;
            try
            {
                if (_writer != null) { _writer.Flush(); _writer.Dispose(); _writer = null; }
                double mean = _frames > 0 ? _sumMs / _frames : 0.0;
                double gcPct = _frames > 0 ? 100.0 * _gcFrames / _frames : 0.0;
                double onePctLow = _stats != null ? _stats.OnePercentLowMs() : 0.0;
                string summary = "capture done (" + why + "): frames=" + _frames +
                    " meanMs=" + mean.ToString("F2", Inv) +
                    " 1%lowMs(window)=" + onePctLow.ToString("F2", Inv) +
                    " maxMs=" + _maxMs.ToString("F2", Inv) +
                    " gcFrames%=" + gcPct.ToString("F1", Inv) +
                    " -> " + _path;
                _log.LogInfo(summary);
                if (_path != null)
                    File.WriteAllText(_path.Substring(0, _path.Length - 4) + "-summary.txt", summary);
            }
            catch (Exception e) { _log.LogError("capture stop failed: " + e); }
        }
    }
}
