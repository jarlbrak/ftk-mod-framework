using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Adds new selectable ADVENTURES / game-modes.
    ///
    /// Unlike items/classes/encounters (which are rows in GridEditor FTK_*DB tables), an FTK adventure
    /// is a <c>GameDefinition</c> deserialized from a <c>.ftk2</c> JSON file in StreamingAssets/mods,
    /// keyed by its string <c>m_SaveFileName</c>. The selection screen (<c>GameConfig.Show</c>) lists
    /// every loaded definition, but gates each through the single hardcoded whitelist
    /// <c>FTKHub.IsValidSaveFileName</c> — so a brand-new adventure needs exactly two things:
    ///   1. its preview present in <c>Cache.GameDefinitions._previews</c>, and
    ///   2. its name accepted by <c>IsValidSaveFileName</c>.
    /// Both are handled here. World generation, win condition, and save/serialization are entirely
    /// data-driven off the GameDefinition, so no generator patch is required for an adventure that
    /// reuses existing realms.
    ///
    /// We CLONE AT RUNTIME from an installed template (e.g. "DungeonCrawl") rather than shipping a copy
    /// of the game's content: read the player's own <c>.ftk2</c>, retune a few JSON fields, register the
    /// result. The clone reuses the template's mod folder, so it inherits the template's preview art.
    /// </summary>
    public static class Adventures
    {
        // saveFileName -> the preview we built (re-injected whenever the game (re)builds its cache).
        private static readonly Dictionary<string, GameDefinitionPreview> Registered =
            new Dictionary<string, GameDefinitionPreview>();

        // Uppercased save-file-names we want FTKHub.IsValidSaveFileName to accept.
        private static readonly HashSet<string> Whitelist = new HashSet<string>();

        /// <summary>
        /// Register a new adventure by cloning an installed one and retuning its JSON.
        /// </summary>
        /// <param name="modGuid">Your plugin GUID (reserved for future per-mod namespacing/save-stamping).</param>
        /// <param name="saveFileName">Unique key for the new adventure (also its save namespace + room-property id).</param>
        /// <param name="templateSaveFileName">An installed adventure to clone, e.g. "DungeonCrawl".</param>
        /// <param name="displayName">Shown on the start screen (literal — displayed verbatim).</param>
        /// <param name="infoText">The adventure's info/description text (literal — displayed verbatim).</param>
        /// <param name="configureJson">Optional hook to tweak the cloned GameDefinition JSON (e.g. multipliers).</param>
        public static GameDefinitionPreview AddFromTemplate(
            string modGuid, string saveFileName, string templateSaveFileName,
            string displayName, string infoText, Action<JObject> configureJson = null)
        {
            // AddFromTemplate is the no-campaign case of the shared pipeline: edit gamedef scalars, then
            // register, with no CampaignBuilder pass over m_Stages.
            return AddFromTemplateInternal(
                modGuid, saveFileName, templateSaveFileName, displayName, infoText, configureJson, null);
        }

        /// <summary>
        /// Register a new adventure as above, then author a multi-stage linear CAMPAIGN over the cloned
        /// definition's <c>m_Stages</c>. A STRICT SUPERSET of <see cref="AddFromTemplate"/>: it applies the
        /// same <paramref name="configureJson"/> gamedef-scalar retune first, then hands a
        /// <see cref="CampaignBuilder"/> over the (still-JSON) stage list so the caller can append valid
        /// stages and reuse-based objective quests as DATA, then deserializes/registers the result exactly
        /// like <see cref="AddFromTemplate"/>. The quests authored here are native <c>QuestDefBase</c> JSON
        /// objects carrying the correct Newtonsoft <c>$type</c> discriminator; no Harmony patch is involved.
        /// </summary>
        /// <param name="modGuid">Your plugin GUID (reserved for future per-mod namespacing/save-stamping).</param>
        /// <param name="saveFileName">Unique key for the new adventure (also its save namespace + room-property id).</param>
        /// <param name="templateSaveFileName">An installed adventure to clone, e.g. "DungeonCrawl".</param>
        /// <param name="displayName">Shown on the start screen (literal — displayed verbatim).</param>
        /// <param name="infoText">The adventure's info/description text (literal — displayed verbatim).</param>
        /// <param name="configure">Authors the campaign: append stages + quests over the cloned m_Stages.</param>
        /// <param name="configureJson">Optional gamedef-scalar retune, applied BEFORE <paramref name="configure"/> (same semantics as AddFromTemplate).</param>
        public static GameDefinitionPreview AddCampaignFromTemplate(
            string modGuid, string saveFileName, string templateSaveFileName,
            string displayName, string infoText,
            Action<CampaignBuilder> configure, Action<JObject> configureJson = null)
        {
            return AddFromTemplateInternal(
                modGuid, saveFileName, templateSaveFileName, displayName, infoText, configureJson, configure);
        }

        /// <summary>
        /// The single clone/edit/build/register path shared by <see cref="AddFromTemplate"/> and
        /// <see cref="AddCampaignFromTemplate"/>. <paramref name="configureCampaign"/> is null for the plain
        /// adventure case; when present it runs over the cloned <c>m_Stages</c> AFTER the gamedef-scalar
        /// <paramref name="configureJson"/> retune and BEFORE preview build/registration.
        /// </summary>
        private static GameDefinitionPreview AddFromTemplateInternal(
            string modGuid, string saveFileName, string templateSaveFileName,
            string displayName, string infoText,
            Action<JObject> configureJson, Action<CampaignBuilder> configureCampaign)
        {
            string templatePath = FindTemplate(templateSaveFileName);
            if (templatePath == null)
            {
                Plugin.Log.LogError("Adventures.AddFromTemplate: template '" + templateSaveFileName +
                    ".ftk2' not found under StreamingAssets/mods.");
                return null;
            }

            string edited = LoadAndEditTemplate(
                templatePath, saveFileName, displayName, infoText, configureJson, configureCampaign);
            if (edited == null) return null;

            GameDefinitionPreview preview = BuildPreview(edited, templatePath, saveFileName);
            if (preview == null) return null;

            // Adventures are string-keyed .ftk2 GameDefinitions registered by m_SaveFileName; they
            // intentionally bypass IdAllocator (which is only for synthetic GridEditor FTK_*DB row ids).
            Registered[saveFileName] = preview;
            Whitelist.Add(saveFileName.ToUpper());

            EnsureLoaded(); // inject immediately if the cache is already built
            Plugin.Log.LogInfo("Adventure '" + saveFileName + "' (\"" + displayName +
                "\") registered, cloned from '" + templateSaveFileName + "'.");
            return preview;
        }

        /// <summary>Locate + parse the template, apply identity/display fields, run the caller's gamedef-scalar
        /// hook, then (if present) the campaign builder over m_Stages, and return the edited JSON (null on
        /// parse failure).</summary>
        private static string LoadAndEditTemplate(
            string templatePath, string saveFileName, string displayName, string infoText,
            Action<JObject> configureJson, Action<CampaignBuilder> configureCampaign)
        {
            JObject jo;
            try { jo = JObject.Parse(File.ReadAllText(templatePath)); }
            catch (Exception e)
            {
                Plugin.Log.LogError("Adventures.AddFromTemplate: could not parse '" + templatePath + "': " + e.Message);
                return null;
            }

            // Identity + display. m_DisplayName/m_GameInfoText resolve through FTKHub.Localized<TextMenu>,
            // which returns the key itself when there's no text row, so literals display verbatim.
            jo["m_SaveFileName"] = saveFileName;
            if (displayName != null) jo["m_DisplayName"] = displayName;
            if (infoText != null) jo["m_GameInfoText"] = infoText;
            if (configureJson != null) configureJson(jo);

            // Campaign authoring runs AFTER the gamedef-scalar retune so a campaign can still observe/override
            // any scalar the configureJson hook set. The builder edits the live JObject's m_Stages in place.
            if (configureCampaign != null) configureCampaign(new CampaignBuilder(jo));

            return jo.ToString();
        }

        /// <summary>Deserialize the preview from edited JSON and load its attract art (null on failure).</summary>
        private static GameDefinitionPreview BuildPreview(string edited, string templatePath, string saveFileName)
        {
            // Build the preview exactly as Cache.GameDefinitions.Initialize does (StringEnumConverter,
            // NO TypeNameHandling — the preview only needs the base scalar fields + the raw JSON).
            GameDefinitionPreview preview;
            try
            {
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.Converters.Add(new StringEnumConverter());
                preview = JsonConvert.DeserializeObject(edited, typeof(GameDefinitionPreview), settings)
                    as GameDefinitionPreview;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Adventures.AddFromTemplate: preview deserialize failed for '" +
                    saveFileName + "': " + e.Message);
                return null;
            }
            if (preview == null) return null;

            preview.m_FullFileData = edited;                            // GetNewGameDefInstance() re-parses this
            preview.m_ModFolderPath = Path.GetDirectoryName(templatePath); // reuse the template's preview art
            TryLoadAttractImage(preview);
            return preview;
        }

        private static string FindTemplate(string templateSaveFileName)
        {
            string mods = Application.streamingAssetsPath + Path.DirectorySeparatorChar + "mods";
            if (!Directory.Exists(mods)) return null;
            string[] hits = Directory.GetFiles(mods, templateSaveFileName + ".ftk2", SearchOption.AllDirectories);
            return hits.Length > 0 ? hits[0] : null;
        }

        private static void TryLoadAttractImage(GameDefinitionPreview preview)
        {
            try
            {
                Texture2D tex = FTKUtil.LoadImage(
                    FTKUtil.GetImageFile(preview.m_ModFolderPath, GameDefinitionPreview.ATTRACT_IMAGE));
                if (tex != null)
                    preview.m_AttractImage = Sprite.Create(
                        tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Adventures: attract-image load failed: " + e.Message); }
        }

        /// <summary>Make sure every registered preview is present in the game's adventure cache.</summary>
        internal static void EnsureLoaded()
        {
            Dictionary<string, GameDefinitionPreview> previews = GameCache.Cache.GameDefinitions._previews;
            if (previews == null) return; // cache not built yet; a later hook will re-run this
            foreach (KeyValuePair<string, GameDefinitionPreview> kv in Registered)
                previews[kv.Key] = kv.Value;
        }

        internal static bool IsWhitelisted(string saveFileName)
        {
            return saveFileName != null && Whitelist.Contains(saveFileName.ToUpper());
        }
    }

    // The game builds its adventure cache once (lazily). Re-inject ours right after, so they survive
    // any rebuild and are present even if we registered before the cache existed.
    [HarmonyPatch(typeof(GameCache.Cache.GameDefinitions), "Initialize")]
    internal static class CacheGameDefsInitialize_Patch
    {
        private static void Postfix() { Adventures.EnsureLoaded(); }
    }

    // The new-game adventure list is built in GameConfig.Show(); guarantee our previews are present
    // the moment before it iterates them (covers registration that happened after the cache was built).
    [HarmonyPatch(typeof(StartGameFE.GameConfig), "Show")]
    internal static class GameConfigShow_Patch
    {
        private static void Prefix() { Adventures.EnsureLoaded(); }
    }

    // The single hardcoded whitelist that filters the adventure list (and the resume-button visibility).
    // Accept our registered names so a custom adventure isn't silently dropped.
    [HarmonyPatch(typeof(FTKHub), "IsValidSaveFileName")]
    internal static class IsValidSaveFileName_Patch
    {
        private static void Postfix(string pSaveFileName, ref bool __result)
        {
            if (!__result && Adventures.IsWhitelisted(pSaveFileName)) __result = true;
        }
    }
}
