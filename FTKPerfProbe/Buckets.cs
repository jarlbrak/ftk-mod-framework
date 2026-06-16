using System.Diagnostics;

namespace FTKPerfProbe
{
    /// <summary>
    /// The four per-frame bucket accumulators and the shared Harmony probe prefix/postfix methods
    /// that feed them. Probes are READ-ONLY timers — they never touch game state. Each bucket uses an
    /// Accumulator (outermost-span inclusive wall time + call count). Methods are static so a single
    /// HarmonyMethod can be attached to many originals (see ProbeInstaller).
    /// </summary>
    internal static class Buckets
    {
        public static readonly Accumulator Id = new Accumulator();
        public static readonly Accumulator Ow = new Accumulator();
        public static readonly Accumulator Pm = new Accumulator();
        public static readonly Accumulator Canvas = new Accumulator();

        public static void IdPre()  { Id.Enter(Stopwatch.GetTimestamp()); }
        public static void IdPost() { Id.Exit(Stopwatch.GetTimestamp()); }

        public static void OwPre()  { Ow.Enter(Stopwatch.GetTimestamp()); }
        public static void OwPost() { Ow.Exit(Stopwatch.GetTimestamp()); }

        public static void PmPre()  { Pm.Enter(Stopwatch.GetTimestamp()); }
        public static void PmPost() { Pm.Exit(Stopwatch.GetTimestamp()); }

        public static void CanvasPre()  { Canvas.Enter(Stopwatch.GetTimestamp()); }
        public static void CanvasPost() { Canvas.Exit(Stopwatch.GetTimestamp()); }
    }
}
