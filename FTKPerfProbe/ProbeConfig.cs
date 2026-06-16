using BepInEx.Configuration;
using UnityEngine;

namespace FTKPerfProbe
{
    /// <summary>BepInEx config: hotkeys, capture window, output dir, per-bucket toggles, overlay default.</summary>
    internal sealed class ProbeConfig
    {
        public readonly ConfigEntry<KeyboardShortcut> OverlayKey;
        public readonly ConfigEntry<KeyboardShortcut> CaptureKey;
        public readonly ConfigEntry<KeyboardShortcut> CensusKey;
        public readonly ConfigEntry<int> CaptureSeconds;
        public readonly ConfigEntry<string> OutputDir;
        public readonly ConfigEntry<bool> IdResolution;
        public readonly ConfigEntry<bool> Overworld;
        public readonly ConfigEntry<bool> PlayMaker;
        public readonly ConfigEntry<bool> Canvas;
        public readonly ConfigEntry<bool> ShowOverlayOnStart;

        public ProbeConfig(ConfigFile c)
        {
            OverlayKey = c.Bind("Hotkeys", "Overlay", new KeyboardShortcut(KeyCode.F9),
                "Toggle the on-screen overlay.");
            CaptureKey = c.Bind("Hotkeys", "Capture", new KeyboardShortcut(KeyCode.F10),
                "Start/stop a fixed-window CSV capture.");
            CensusKey = c.Bind("Hotkeys", "Census", new KeyboardShortcut(KeyCode.F11),
                "Dump a one-shot scene census.");

            CaptureSeconds = c.Bind("Capture", "Seconds", 30, "Capture window length in seconds.");
            OutputDir = c.Bind("Capture", "OutputDir", "BepInEx/FTKPerfProbe",
                "Output folder for CSV/summary/census (relative to game root, or absolute).");

            IdResolution = c.Bind("Probes", "IdResolution", true,
                "Probe FTK_*.GetEnum / GetIntFromID string->enum resolution.");
            Overworld = c.Bind("Probes", "Overworld", true,
                "Probe OverworldCamera.Update and overworld UI LateUpdates.");
            PlayMaker = c.Bind("Probes", "PlayMaker", true, "Probe PlayMakerFSM.Update.");
            Canvas = c.Bind("Probes", "Canvas", true, "Probe uGUI CanvasUpdateRegistry.PerformUpdate.");

            ShowOverlayOnStart = c.Bind("Overlay", "ShowOnStart", false,
                "Show the overlay immediately on load.");
        }
    }
}
