using UnityEngine;

namespace FTKPerfProbe
{
    /// <summary>Compact IMGUI HUD. Called from ProbeRunner.OnGUI when the overlay is toggled on.</summary>
    internal sealed class Overlay
    {
        private GUIStyle _style;

        public void Draw(FrameStats stats, GcFrame gc, double idMs, int idCalls, double owMs,
            double pmMs, int pmCalls, double canvasMs, int canvasCalls, bool capturing)
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = 14;
                _style.normal.textColor = Color.white;
            }

            string text =
                "FTKPerfProbe   fps " + stats.MeanFps().ToString("F0") +
                "   ms mean/1%low/max " + stats.MeanMs().ToString("F1") + "/" +
                    stats.OnePercentLowMs().ToString("F1") + "/" + stats.MaxMs().ToString("F1") +
                "\nGC " + (gc.GcFired ? "[*]" : "[ ]") +
                "  heap " + (gc.HeapBytes / (1024 * 1024)) + "MB" +
                "  alloc/frame " + (gc.AllocEstBytes >= 0 ? (gc.AllocEstBytes / 1024) + "KB" : "n/a") +
                "\nid " + idMs.ToString("F2") + "ms(" + idCalls + ")" +
                "   ow " + owMs.ToString("F2") + "ms" +
                "   pm " + pmMs.ToString("F2") + "ms(" + pmCalls + ")" +
                "   canvas " + canvasMs.ToString("F2") + "ms(" + canvasCalls + ")" +
                (capturing ? "\n[CAPTURING]" : "");

            GUI.Box(new Rect(8, 8, 780, 92), GUIContent.none);
            GUI.Label(new Rect(14, 12, 772, 86), text, _style);
        }
    }
}
