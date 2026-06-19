using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the load-time campaign <see cref="QuestValidator"/> (#43, spec #37 P4). It proves,
    /// WITHOUT gameplay, the two headline guarantees and emits a single <c>SELF-TEST PASS [quest-validator]</c>
    /// line (or a matching FAIL):
    /// <list type="bullet">
    /// <item><b>Valid campaign passes CLEAN:</b> author a small linear campaign (the #38 demo shape: kill ->
    /// visit -> clear), round-trip it through the game's EXACT settings, and assert <see cref="QuestValidator"/>
    /// reports ZERO errors (warnings allowed; none expected here).</item>
    /// <item><b>Broken campaign is caught PRECISELY:</b> author a 3-quest campaign with an UNCONDITIONAL branch
    /// on the middle quest back to the start, forming a cycle that can never reach the victory quest. Assert the
    /// validator FAILS with the victory-reachability ERROR that NAMES the offending quest -> the precise
    /// load-time diagnostic an author would see in-game.</item>
    /// </list>
    ///
    /// It uses the SAME shared <see cref="ValidationReport"/> channel the data loader uses (asserting the report
    /// contains the expected FAIL text), mirroring the negative-fixture style of the #34/#35 self-tests. The two
    /// campaigns are registered via <see cref="Adventures.AddCampaignFromTemplate"/> (which now runs the
    /// validator as its load pre-pass), so this also exercises the end-to-end wiring. Gated like the other
    /// campaign self-tests (run from Content/AdventureContent.cs under EnableSampleContent), since it registers
    /// real selectable demo adventures. Unique quest keys avoid colliding with the other self-tests in the
    /// process-wide <see cref="BranchSidecar"/>.
    /// </summary>
    internal static class QuestValidatorSelfTest
    {
        private const string Template = "DungeonCrawl";
        private const string DestRealm = "GuardianForest"; // present in DungeonCrawl's m_RealmStages (see CampaignSelfTest)

        // ---- valid campaign (must pass clean) -------------------------------------------------------------
        private const string ValidSave = "FtkmfValidatorOk";
        private const string V1 = "ftkmf_val_ok_kill";
        private const string V2 = "ftkmf_val_ok_visit";
        private const string V3 = "ftkmf_val_ok_clear";

        // ---- broken campaign (unreachable victory via an unconditional cycle) -----------------------------
        private const string BrokenSave = "FtkmfValidatorBroken";
        private const string B1 = "ftkmf_val_bad_q1";
        private const string B2 = "ftkmf_val_bad_q2";
        private const string B3 = "ftkmf_val_bad_q3"; // the victory quest (last quest of the last stage)

        public static void Run()
        {
            try
            {
                bool validClean = CheckValidCampaignPassesClean();
                string brokenFail;
                bool brokenCaught = CheckBrokenCampaignFailsPrecisely(out brokenFail);

                if (validClean && brokenCaught)
                    Plugin.Log.LogInfo("SELF-TEST PASS [quest-validator]: valid campaign '" + ValidSave +
                        "' validated with 0 errors; broken campaign '" + BrokenSave +
                        "' caught with precise FAIL: " + brokenFail);
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [quest-validator]: validClean=" + validClean +
                        " brokenCaught=" + brokenCaught + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [quest-validator]: " + e);
            }
        }

        // The valid campaign mirrors the #38 demo: three reuse-verb quests in a clean linear chain. The
        // validator must report ZERO errors over its round-tripped GameDefinition + (empty) sidecar.
        private static bool CheckValidCampaignPassesClean()
        {
            GameDefinition gd = BuildCampaign(ValidSave,
                "Validator OK Demo", "A clean linear campaign that the QuestValidator passes.", AuthorValid);
            if (gd == null) return false; // BuildCampaign logged the FAIL reason

            ValidationReport report = new ValidationReport();
            bool ok = QuestValidator.Validate(gd, ValidSave, report);

            if (!ok || report.Errors.Count != 0)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [quest-validator] (valid): expected 0 errors, got " +
                    report.Errors.Count + " (first: " +
                    (report.Errors.Count > 0 ? report.Errors[0] : "<none>") + ").");
                return false;
            }
            return true;
        }

        // The broken campaign cannot reach victory: Q2 has an UNCONDITIONAL branch back to Q1, so the chain
        // cycles Q1<->Q2 and never reaches Q3 (the victory quest). The validator must FAIL with the
        // victory-reachability ERROR naming a reachable quest that cannot reach victory.
        private static bool CheckBrokenCampaignFailsPrecisely(out string failLine)
        {
            failLine = null;

            GameDefinition gd = BuildCampaign(BrokenSave,
                "Validator Broken Demo", "A campaign with a cycle that can never reach victory.", AuthorBroken);
            if (gd == null) return false;

            ValidationReport report = new ValidationReport();
            bool ok = QuestValidator.Validate(gd, BrokenSave, report);

            if (ok || report.Errors.Count == 0)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [quest-validator] (broken): expected a FAIL, but the " +
                    "validator passed (errors=" + report.Errors.Count + ").");
                return false;
            }

            // Assert the report contains the precise victory-reachability diagnostic naming the offending quest.
            // B1 is reachable from start and trapped in the cycle, so it is the representative the walk names.
            foreach (string e in report.Errors)
            {
                if (e.Contains("cannot reach the victory quest") && e.Contains(B1))
                {
                    failLine = e;
                    return true;
                }
            }

            Plugin.Log.LogError("SELF-TEST FAIL [quest-validator] (broken): FAILed, but no victory-reachability " +
                "error named the expected quest '" + B1 + "'. Errors: " + string.Join(" | ", report.Errors.ToArray()));
            return false;
        }

        private static void AuthorValid(CampaignBuilder campaign)
        {
            StageBuilder stage1 = campaign.AddStage("FtkmfValOk_Stage1");
            stage1.AddKillQuest(V1, "bounty1A", DestRealm);
            stage1.AddVisitQuest(V2, DestRealm);

            StageBuilder stage2 = campaign.AddStage("FtkmfValOk_Stage2");
            stage2.AddClearDungeonQuest(V3, "Cave", DestRealm);
        }

        private static void AuthorBroken(CampaignBuilder campaign)
        {
            StageBuilder stage = campaign.AddStage("FtkmfValBad_Stage1");
            stage.AddVisitQuest(B1, DestRealm);
            QuestBuilder q2 = stage.AddVisitQuest(B2, DestRealm);
            stage.AddVisitQuest(B3, DestRealm); // victory quest (last of the last stage)

            // Q2 -> Q1 UNCONDITIONALLY (no conditions = the default edge that always fires). The router would
            // always loop back, so the linear successor Q3 (victory) is never reachable: R={Q1,Q2}, V={Q3}.
            q2.BranchTo(B1);
        }

        /// <summary>
        /// Author + register a campaign via <see cref="Adventures.AddCampaignFromTemplate"/> (which runs the
        /// validator as its load pre-pass), then deserialize its authored bytes with the EXACT game settings into
        /// a real <see cref="GameDefinition"/> the self-test re-validates directly. Returns null (and logs) on a
        /// missing template or a failed round-trip, matching the other campaign self-tests.
        /// </summary>
        private static GameDefinition BuildCampaign(
            string saveFileName, string displayName, string infoText, Action<CampaignBuilder> author)
        {
            GameDefinitionPreview preview = Adventures.AddCampaignFromTemplate(
                Plugin.Guid, saveFileName, Template, displayName, infoText, author);

            if (preview == null || string.IsNullOrEmpty(preview.m_FullFileData))
            {
                Plugin.Log.LogError("SELF-TEST FAIL [quest-validator]: AddCampaignFromTemplate('" + saveFileName +
                    "') returned " + (preview == null ? "null (template '" + Template + "' not installed?)" :
                    "no m_FullFileData") + ".");
                return null;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;
            settings.Converters.Add(new StringEnumConverter());
            GameDefinition gd = JsonConvert.DeserializeObject<GameDefinition>(preview.m_FullFileData, settings);
            if (gd == null || gd.m_Stages == null || gd.m_Stages.Count < 1)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [quest-validator]: round-trip of '" + saveFileName +
                    "' produced a null GameDefinition/m_Stages.");
                return null;
            }
            return gd;
        }
    }
}
