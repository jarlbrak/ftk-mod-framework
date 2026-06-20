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

            // CAMPAIGN LOAD PRE-PASS (#43): when a campaign was authored (configureCampaign != null) and the
            // engine is on, validate the authored GameDefinition + its BranchSidecar BEFORE registering the
            // preview, emitting precise diagnostics through the SAME ValidationReport channel the data loader
            // uses. This is a load pre-pass (campaigns are not in ContentLoader's collection, so validating here
            // right after authoring is the cleanest load-time seam). Diagnostics are surfaced; registration still
            // proceeds (tolerate-and-report, matching the data loader) so a broken campaign's FAIL is visible
            // in-game without changing the demo path. The validator itself is gated by EnableCampaignEngine.
            if (configureCampaign != null)
                ValidateCampaign(preview, saveFileName);

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
            // FinalizeCampaign() runs ONCE after all stages are authored and BEFORE the preview round-trip: it
            // extends m_MapLayoutOptions with per-stage RealmCasterData so EVERY authored stage index generates
            // hexes (without it, multi-stage campaigns soft-lock; #37, see CampaignBuilder.FinalizeCampaign).
            if (configureCampaign != null)
            {
                CampaignBuilder builder = new CampaignBuilder(jo);
                configureCampaign(builder);
                builder.FinalizeCampaign();
            }

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

        /// <summary>
        /// Run the load-time campaign validator over the authored preview, emitting diagnostics through a fresh
        /// <see cref="Data.ValidationReport"/> and logging a one-line summary (mirroring the data loader's
        /// LogSummary). Gated by <c>EnableCampaignEngine</c> (null-guarded for test contexts, where it defaults
        /// to running). The campaign is deserialized from <c>m_FullFileData</c> with the EXACT game settings
        /// (<c>TypeNameHandling.Auto</c> + <c>StringEnumConverter</c>, as GameDefJSONMapper.Start does), giving
        /// the validator the concrete quest subtypes it needs for per-type id resolution. Never throws.
        /// </summary>
        internal static void ValidateCampaign(GameDefinitionPreview preview, string saveFileName)
        {
            // Engine off => skip validation entirely (the patches aren't installed; nothing to validate against).
            if (Plugin.EnableCampaignEngine != null && !Plugin.EnableCampaignEngine.Value) return;
            if (preview == null || string.IsNullOrEmpty(preview.m_FullFileData)) return;

            Data.ValidationReport report = new Data.ValidationReport();
            try
            {
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.TypeNameHandling = TypeNameHandling.Auto;
                settings.Converters.Add(new StringEnumConverter());
                GameDefinition gd = JsonConvert.DeserializeObject<GameDefinition>(preview.m_FullFileData, settings);

                QuestValidator.Validate(gd, saveFileName, report);
            }
            catch (Exception e)
            {
                report.Error("[campaign '" + saveFileName + "'] validator threw: " + e.Message);
            }

            // One-line summary, then per-item detail, exactly like ContentLoader.LogSummary.
            Plugin.Log.LogInfo("Campaign validation '" + saveFileName + "': " +
                report.Errors.Count + " error(s), " + report.Warnings.Count + " warning(s).");
            foreach (string w in report.Warnings) Plugin.Log.LogWarning("Campaign warning: " + w);
            foreach (string er in report.Errors) Plugin.Log.LogError("Campaign error: " + er);
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

        // ---- custom UserNPCs (data-only narrative speakers) -------------------------------------------------

        /// <summary>
        /// Give an adventure its OWN writable mod folder containing a custom UserNPC, and repoint the adventure's
        /// preview at it so the game scans the NPC at game start. PURE DATA + filesystem: no Harmony patch.
        ///
        /// WHY A NEW FOLDER: <see cref="BuildPreview"/> sets <c>preview.m_ModFolderPath</c> to the cloned TEMPLATE's
        /// directory (under StreamingAssets, read-only) only to load the attract art, which it does immediately and
        /// caches as an IN-MEMORY <c>Sprite</c> on <c>preview.m_AttractImage</c>. After that, the template dir is no
        /// longer needed by the preview; the only later reader of <c>m_ModFolderPath</c> is the game's
        /// <c>GameDefinition._findAllUserNPCs</c>, which scans <c>m_ModFolderPath/npcs/&lt;key&gt;/</c> at gamedef
        /// init. So we point <c>m_ModFolderPath</c> at a WRITABLE folder next to the plugin DLL, drop the NPC there,
        /// and the attract art is preserved (it is already a loaded Sprite). The preview object is held by reference
        /// in the adventure cache, so <see cref="EnsureLoaded"/> re-injects this SAME instance on any cache rebuild
        /// and the repointed path persists.
        ///
        /// The NPC is written as the game expects (<c>GameDefinition._findAllUserNPCs</c>): under
        /// <c>&lt;adventureFolder&gt;/npcs/&lt;npcKey&gt;/</c>, a single <c>.txt</c> holding the bare-deserialized
        /// <c>UserNPC</c> JSON (Name/Title; the game ignores extra fields), plus a <c>portrait.png</c> loaded from
        /// disk into the NPC's Sprite. <paramref name="npcKey"/> MUST equal the <c>m_UserNPC</c> value the narrative
        /// references (the folder name IS the key the game keys the NPC by).
        ///
        /// All IO is guarded; on failure it logs and returns the unmodified preview (the adventure still loads, just
        /// without the custom speaker, which falls back to a portrait-less popup). Idempotent: it overwrites the
        /// json + portrait each call.
        /// </summary>
        /// <param name="preview">The adventure preview returned by <see cref="AddCampaignFromTemplate"/>/<see cref="AddFromTemplate"/>.</param>
        /// <param name="adventureFolderName">A unique subfolder name for this adventure's mod folder (e.g. "HollowMire").</param>
        /// <param name="npcKey">The UserNPC folder key (== the narrative's <c>m_UserNPC</c>); non-empty.</param>
        /// <param name="npcName">The NPC's display name (verbatim in the popup header).</param>
        /// <param name="npcTitle">The NPC's title/subtitle (verbatim in the popup header).</param>
        /// <param name="portraitResourceName">Embedded-resource name of the portrait PNG in THIS assembly
        /// (e.g. "FTKModFramework.assets.npcs.reeve_maddow.portrait.png"); extracted to <c>portrait.png</c>.</param>
        /// <returns>The same <paramref name="preview"/>, for chaining (null if it was null).</returns>
        public static GameDefinitionPreview RegisterUserNpc(
            GameDefinitionPreview preview, string adventureFolderName, string npcKey,
            string npcName, string npcTitle, string portraitResourceName)
        {
            if (preview == null)
            {
                Plugin.Log.LogWarning("Adventures.RegisterUserNpc: preview is null; skipping NPC '" +
                    (npcKey ?? "?") + "'.");
                return null;
            }
            if (string.IsNullOrEmpty(adventureFolderName) || string.IsNullOrEmpty(npcKey))
            {
                Plugin.Log.LogWarning("Adventures.RegisterUserNpc: adventureFolderName/npcKey must be non-empty; " +
                    "skipping NPC for adventure folder '" + (adventureFolderName ?? "?") + "'.");
                return preview;
            }

            bool portraitWritten = false;
            string npcDir = null;
            try
            {
                // A writable mod folder next to the plugin DLL: <pluginDir>/FTKModFramework_content/<adventureFolderName>.
                string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                string adventureDir = Path.Combine(Path.Combine(pluginDir, "FTKModFramework_content"), adventureFolderName);
                npcDir = Path.Combine(Path.Combine(adventureDir, "npcs"), npcKey);
                Directory.CreateDirectory(npcDir); // idempotent; creates the whole chain

                // The bare UserNPC JSON the game deserializes (NO $type, NO settings). Portrait is loaded from disk,
                // never from JSON, so it is not present here. Build via JObject so values are escaped correctly.
                JObject npcJson = new JObject();
                npcJson["Name"] = npcName ?? string.Empty;
                npcJson["Title"] = npcTitle ?? string.Empty;
                File.WriteAllText(Path.Combine(npcDir, npcKey + ".txt"), npcJson.ToString());

                portraitWritten = ExtractEmbeddedPortrait(portraitResourceName, Path.Combine(npcDir, "portrait.png"));

                // Repoint AFTER the preview already cached its attract Sprite (BuildPreview loaded it from the
                // template dir into m_AttractImage). The only later reader of m_ModFolderPath is _findAllUserNPCs.
                preview.m_ModFolderPath = adventureDir;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Adventures.RegisterUserNpc: failed to write NPC '" + npcKey + "' for adventure '" +
                    adventureFolderName + "': " + e.Message);
            }

            Plugin.Log.LogInfo("[narrative] " + npcKey + " npc written to " +
                (npcDir ?? "(unwritten)") + ", portrait=" + portraitWritten);
            return preview;
        }

        /// <summary>
        /// Extract an embedded-resource PNG from this assembly to <paramref name="destPath"/> (overwrite). Returns
        /// true on success. On a name miss it logs the available manifest names so a rename is immediately visible.
        /// </summary>
        private static bool ExtractEmbeddedPortrait(string resourceName, string destPath)
        {
            if (string.IsNullOrEmpty(resourceName)) return false;
            System.Reflection.Assembly asm = typeof(Plugin).Assembly;
            try
            {
                using (Stream src = asm.GetManifestResourceStream(resourceName))
                {
                    if (src == null)
                    {
                        Plugin.Log.LogWarning("Adventures.RegisterUserNpc: embedded portrait '" + resourceName +
                            "' not found. Manifest resources: " + string.Join(", ", asm.GetManifestResourceNames()));
                        return false;
                    }
                    using (FileStream dst = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[8192];
                        int read;
                        while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                            dst.Write(buffer, 0, read);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Adventures.RegisterUserNpc: extracting portrait '" + resourceName +
                    "' to '" + destPath + "' failed: " + e.Message);
                return false;
            }
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
