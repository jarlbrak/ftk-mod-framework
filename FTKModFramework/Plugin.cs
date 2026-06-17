using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using FullSerializer;
using GridEditor;
using FTKModFramework.Core;
using FTKModFramework.Core.Data;

namespace FTKModFramework
{
    [BepInPlugin(Guid, Name, Version)]
    // NOTE: intentionally no [BepInProcess(...)]. The process is "FTK.exe" on Windows but "FTK"
    // on macOS/Linux; gating on one name would stop the plugin loading cross-platform. We only
    // ever install this into FTK's BepInEx/plugins, so the gate adds nothing anyway.
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.ftkmf.framework";
        public const string Name = "FTK Mod Framework";
        public const string Version = "0.1.0";

        public static Plugin Instance;
        public static ManualLogSource Log;

        /// <summary>
        /// Whether to register the bundled example content (Emberbrand weapon + Ember Lash ability,
        /// added to the Blacksmith's starting kit). Off = the framework only powers other mods.
        /// </summary>
        public static ConfigEntry<bool> EnableSampleContent;

        /// <summary>
        /// DEBUG verification aid: replace every overworld LAND enemy the game spawns with the custom
        /// "Cutpurse" so enemy injection is immediately visible in combat. Turn off for normal play.
        /// </summary>
        public static ConfigEntry<bool> ForceCustomEnemy;

        /// <summary>
        /// DEBUG verification aid: replace every overworld encounter the game spawns with the custom
        /// "Smuggler's Cache" so injection is immediately visible in a normal run. Turn off for normal play.
        /// </summary>
        public static ConfigEntry<bool> ForceCustomEncounter;

        /// <summary>
        /// Whether to run the JSON data-content loader: discover mod folders under
        /// <see cref="DataContentRoot"/>, parse their content files, and register them through the
        /// public Content.* API. Independent of <see cref="EnableSampleContent"/>: disabling the bundled
        /// demo never disables third-party data mods.
        /// </summary>
        public static ConfigEntry<bool> EnableDataContent;

        /// <summary>
        /// Folder the data loader scans for mod subfolders (each with a manifest.json). Defaults to
        /// BepInEx's plugins dir, so dropping a content-mod folder in alongside plugins just works.
        /// </summary>
        public static ConfigEntry<string> DataContentRoot;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            EnableSampleContent = Config.Bind("Demo", "EnableSampleContent", true,
                "Register the bundled example content (a custom weapon + ability, given to the Blacksmith). " +
                "Set false if you only want the framework as a dependency for other content mods.");

            ForceCustomEnemy = Config.Bind("Enemies", "ForceCustomEnemy", false,
                "DEBUG: replace every overworld LAND enemy that spawns with the custom 'Cutpurse' so enemy " +
                "injection is immediately visible in combat. Set false for normal play.");

            ForceCustomEncounter = Config.Bind("Adventures", "ForceCustomEncounter", false,
                "DEBUG: replace every overworld encounter that spawns with the custom 'Smuggler's Cache' so " +
                "encounter injection is immediately visible in-game. Set false for normal play.");

            EnableDataContent = Config.Bind("Data", "EnableDataContent", true,
                "Run the JSON data-content loader (discovers content-mod folders under DataContentRoot and " +
                "registers their content). Independent of EnableSampleContent.");

            DataContentRoot = Config.Bind("Data", "DataContentRoot", Paths.PluginPath,
                "Folder scanned for content-mod subfolders (each with a manifest.json). Defaults to the " +
                "BepInEx plugins directory.");

            // Save-safety: synthetic enum ids must round-trip through saves as their int value.
            fsConfig.SerializeEnumsAsInteger = true;

            _harmony = new Harmony(Guid);
            DbLookupPatcher.Init(_harmony);
            _harmony.PatchAll();

            Log.LogInfo(Name + " " + Version + " loaded (For The King / Unity 2017.2.2p2 / Mono).");
        }
    }

    /// <summary>
    /// Single content entry point. By the time TableManager.Initialize() returns, every
    /// FTK_*DB table is populated, so this is the one safe place to inject custom content.
    /// </summary>
    [HarmonyPatch(typeof(TableManager), "Initialize")]
    internal static class TableManager_Initialize_Patch
    {
        private static bool _done;

        private static void Postfix()
        {
            if (_done) return; // Initialize can be reached more than once; only seed content once.
            _done = true;

            // Bundled demo content (opt-in). Disabling it must NOT skip the data loader below.
            if (Plugin.EnableSampleContent.Value)
            {
                Run("sample weapon/ability", SampleContent.Register);
                Run("thief class", ThiefClass.Register);
                Run("cutpurse enemy", CutpurseEnemy.Register);
                Run("sample encounter + adventure", AdventureContent.Register);
            }

            // JSON data-content mods (opt-in, independent of the demo). Runs AFTER sample content so a
            // data mod can reference vanilla rows the same way the demo does.
            if (Plugin.EnableDataContent.Value)
                Run("data content", LoadDataContent);
        }

        private static void LoadDataContent()
        {
            ContentLoader.Load(Plugin.DataContentRoot.Value);
        }

        private static void Run(string what, Action register)
        {
            try { register(); }
            catch (Exception e) { Plugin.Log.LogError(what + " registration failed: " + e); }
        }
    }
}
