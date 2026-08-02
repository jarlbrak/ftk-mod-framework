using System;
using System.Collections.Generic;
using GridEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FTKModFramework.Core
{
    /// <summary>
    /// THE GATING SPIKE for spec #57 / work item #58: does a SYNTHETIC realm id survive a full
    /// <see cref="GameDefinition"/> Newtonsoft round-trip and resolve through the game's own
    /// <see cref="GameDefinition.GetRealmProperties"/> dictionary-key conversion?
    ///
    /// Why this is the make-or-break: <c>GameStage.m_RealmStages</c> is a
    /// <c>Dictionary&lt;FTK_realm.ID, RealmProperties&gt;</c> keyed BY the realm id. The game deserializes
    /// the GameDefinition with <c>TypeNameHandling.Auto + StringEnumConverter</c> (AllowIntegerValues defaults
    /// true). A synthetic high-band id has NO enum NAME, so it can only appear in JSON as its DECIMAL INTEGER
    /// (e.g. the dict KEY string "1073741824"). Newtonsoft must convert that integer-string DICTIONARY KEY into
    /// the enum-typed key <c>(FTK_realm.ID)1073741824</c>. If it does, every realm-keyed read (GetRealmProperties,
    /// map gen, casters) works for custom realms; if it does not, the whole bespoke-realm approach is blocked.
    ///
    /// This runs ENTIRELY at plugin load with a pure Newtonsoft round-trip (no game run, no map gen): we author
    /// a minimal GameDefinition JSON that places the synthetic realm int at THREE sites and deserialize it with
    /// the game's exact settings, then call <c>GetRealmProperties</c>.
    ///
    /// Output (so the PASS/FAIL decision is readable straight from the log):
    ///   SELF-TEST PASS [realm-spike]: realmInt=&lt;n&gt;, deserialized=ok, GetRealmProperties=ok (...).
    ///   SELF-TEST FAIL [realm-spike]: &lt;exact failure&gt;.
    /// IMPORTANT: every path is wrapped so this NEVER throws out of registration; a failure is logged as FAIL.
    ///
    /// Gated by EnableSampleContent (wired from <see cref="AdventureContent"/>), like the other demo self-tests.
    /// </summary>
    internal static class RealmSpikeSelfTest
    {
        // A DISTINCT throwaway realm id for the spike, so the gating probe never collides with the shipped
        // "The Hollow Mire" adventure realm (RealmBossAdventure also registers under ftkmf_hollow_mire; sharing
        // the id would append a duplicate, dangling FTK_realmDB row even though the dictionary still resolves).
        private const string RealmId = "ftkmf_spike_realm";
        private const FTK_realm.ID Template = FTK_realm.ID.PoisonBog;  // bog-themed vanilla realm (ordinal 4)

        // A marker we set on the authored RealmProperties and read back after the round-trip, to prove the dict
        // value that comes out is the very one we keyed under the synthetic int (not a default / other entry).
        private const int MarkerRealmSize = 4242;

        public static void Run()
        {
            try
            {
                // 1) Register the custom realm cloned from PoisonBog and capture its synthetic int id.
                RealmBossRegistration.RegisterRealm(
                    Plugin.Guid, RealmId, Template,
                    r =>
                    {
                        // It is its own realm, not a part of the template's group, so the synthetic id is the
                        // sole member of its associated-realms set (avoids inheriting PoisonBog's m_PartOf).
                        r.m_PartOf = new FTK_realm.ID[0];
                    });

                int realmInt = Content.Db<FTK_realmDB>().GetIntFromID(RealmId);
                if (realmInt < 0 || !IdAllocator.IsCustom(realmInt))
                {
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-spike]: realm did not register (GetIntFromID('" +
                        RealmId + "')=" + realmInt + ").");
                    return;
                }

                // 2) Author a minimal GameDefinition JSON that puts the synthetic realm INT at three sites:
                //    a) m_Stages[0].m_RealmStages KEY  (the make-or-break dictionary-key conversion)
                //    b) m_MapLayoutOptions[0].m_RealmCasterData[0].m_PreferredRealm  (enum field, integer value)
                //    c) m_Stages[0].m_Quests[0].m_SpecifiedRealm                     (enum field, integer value)
                string json = BuildGameDefinitionJson(realmInt);

                // 3) Deserialize with the game's EXACT settings (GameDefJSONMapper.Start: TypeNameHandling.Auto
                //    + StringEnumConverter). This is the conversion the game itself performs on load.
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.TypeNameHandling = TypeNameHandling.Auto;
                settings.Converters.Add(new StringEnumConverter());

                GameDefinition gd;
                try
                {
                    gd = JsonConvert.DeserializeObject<GameDefinition>(json, settings);
                }
                catch (Exception de)
                {
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-spike]: deserialize threw (realmInt=" + realmInt +
                        "): " + de.Message);
                    return;
                }

                if (gd == null || gd.m_Stages == null || gd.m_Stages.Count < 1)
                {
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-spike]: deserialized GameDefinition/m_Stages is null/empty (realmInt=" +
                        realmInt + ").");
                    return;
                }

                Validate(gd, realmInt);
            }
            catch (Exception e)
            {
                // The whole point is to read PASS vs FAIL from the log, never to crash the plugin.
                Plugin.Log.LogError("SELF-TEST FAIL [realm-spike]: unexpected: " + e);
            }
        }

        /// <summary>
        /// Assert the dict-key round-trip: the synthetic-int KEY must be present in the deserialized
        /// m_RealmStages dictionary AND <c>GetRealmProperties((FTK_realm.ID)realmInt, 0)</c> must return the
        /// authored RealmProperties (identified by our marker m_RealmSize). Also reports the secondary caster /
        /// quest sites, but the PASS gate is the dictionary conversion + GetRealmProperties.
        /// </summary>
        private static void Validate(GameDefinition gd, int realmInt)
        {
            FTK_realm.ID realmKey = (FTK_realm.ID)realmInt;

            // (a) make-or-break: the integer-string dict KEY became the enum-typed key.
            Dictionary<FTK_realm.ID, RealmProperties> stages = gd.m_Stages[0].m_RealmStages;
            bool keyPresent = stages != null && stages.ContainsKey(realmKey);

            // GetRealmProperties is the game's own read path over that dictionary.
            RealmProperties props = null;
            try { props = gd.GetRealmProperties(realmKey, 0); }
            catch (Exception ge)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [realm-spike]: GetRealmProperties threw (realmInt=" +
                    realmInt + "): " + ge.Message);
                return;
            }
            bool getPropsOk = props != null && props.m_RealmSize == MarkerRealmSize;

            // (b) secondary: the caster m_PreferredRealm enum field carried the synthetic int.
            bool casterOk = false;
            if (gd.m_MapLayoutOptions != null)
                foreach (MapLayoutData layout in gd.m_MapLayoutOptions)
                {
                    if (layout == null || layout.m_RealmCasterData == null) continue;
                    foreach (RealmCasterData caster in layout.m_RealmCasterData)
                        if (caster != null && caster.m_PreferredRealm == realmKey) { casterOk = true; break; }
                    if (casterOk) break;
                }

            // (c) secondary: the quest m_SpecifiedRealm enum field carried the synthetic int (best-effort:
            // depends on the $type discriminator resolving; never gates the spike).
            bool questOk = false;
            List<QuestDefBase> quests = gd.m_Stages[0].m_Quests;
            if (quests != null && quests.Count > 0)
            {
                SingleQuestDefBase q = quests[0] as SingleQuestDefBase;
                questOk = q != null && q.m_SpecifiedRealm == realmKey;
            }

            // The spike PASSES iff the dictionary-key conversion AND GetRealmProperties succeed. The caster and
            // quest sites are reported for completeness but do not gate (they are ordinary enum-field reads).
            if (keyPresent && getPropsOk)
                Plugin.Log.LogInfo("SELF-TEST PASS [realm-spike]: realmInt=" + realmInt +
                    ", deserialized=ok, GetRealmProperties=ok (m_RealmSize marker=" + props.m_RealmSize +
                    ", m_BossEnemy=" + props.m_BossEnemy + "); secondary sites caster=" + casterOk +
                    ", quest=" + questOk + ". Dictionary integer-string KEY converts to the synthetic enum id.");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [realm-spike]: realmInt=" + realmInt +
                    " keyPresent=" + keyPresent + " GetRealmProperties=" +
                    (props == null ? "null" : ("returned (marker=" + props.m_RealmSize + ", expected " + MarkerRealmSize + ")")) +
                    " (deserialize succeeded; the integer-string dict KEY did NOT resolve to the synthetic enum id).");
        }

        /// <summary>
        /// Hand-author the smallest GameDefinition JSON that exercises all three realm-int sites. The realm int
        /// is written as a DECIMAL INTEGER everywhere: as the m_RealmStages dictionary KEY (a JSON string, since
        /// JSON object keys are always strings) and as the integer VALUE of the two enum fields. The single quest
        /// carries the runtime-computed Newtonsoft <c>$type</c> for <see cref="BountyQuestDef"/> so it round-trips
        /// under TypeNameHandling.Auto; if that resolution ever fails it only affects the secondary quest site,
        /// never the make-or-break dict-key assertion.
        /// </summary>
        private static string BuildGameDefinitionJson(int realmInt)
        {
            string key = realmInt.ToString();   // decimal-string dictionary KEY for the synthetic realm
            string val = realmInt.ToString();   // decimal-integer VALUE for the enum fields

            // Short assembly-qualified name, exactly the form Newtonsoft's default binder writes/reads under
            // TypeNameHandling.Auto ("Namespace.Type, AssemblyName").
            Type qt = typeof(BountyQuestDef);
            string questType = qt.FullName + ", " + qt.Assembly.GetName().Name;

            return
                "{" +
                  "\"m_OceanRealmID\": -1," +          // never collide with the GetRealmProperties ocean short-circuit
                  "\"m_Stages\": [" +
                    "{" +
                      "\"m_ThisStageID\": \"ftkmf_spike_stage0\"," +
                      "\"m_RealmStages\": {" +
                        "\"" + key + "\": {" +
                          "\"m_GameStartRealm\": true," +
                          "\"m_RealmSize\": " + MarkerRealmSize + "," +   // marker we read back to confirm identity
                          "\"m_BossEnemy\": -1" +                          // FTK_enemySet.ID.None
                        "}" +
                      "}," +
                      "\"m_Quests\": [" +
                        "{" +
                          "\"$type\": \"" + questType + "\"," +
                          "\"m_StoryQuestID\": \"ftkmf_spike_quest0\"," +
                          "\"m_SpecifiedRealm\": " + val + "," +
                          "\"m_SpecifiedRealmStageIndex\": 0," +
                          "\"m_EnemySet\": -1" +
                        "}" +
                      "]" +
                    "}" +
                  "]," +
                  "\"m_MapLayoutOptions\": [" +
                    "{" +
                      "\"m_MapLayoutID\": \"ftkmf_spike_layout\"," +
                      "\"m_RealmCasterData\": [" +
                        "{" +
                          "\"m_StageIndex\": 0," +
                          "\"m_PreferredRealm\": " + val +
                        "}" +
                      "]" +
                    "}" +
                  "]" +
                "}";
        }
    }
}
