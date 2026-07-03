using GridEditor;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Spec #78 Phase 1 / FR-7 showcase class: the Innkeeper, an unshakeable barkeep cloned from the
    /// Blacksmith template. A pure tank identity, Toughness/Vitality-forward with deliberately low Speed
    /// and low offense, and the two showcase passives bound DORMANT via the public
    /// <see cref="Content.AddPassive"/> API (they do NOTHING in Phase 1; the trigger patches that read
    /// them land in Phases 2 and 3). This file is authored SOLELY through the public Content.* surface and
    /// touches nothing in Core/.
    ///
    /// Primary stat: TOUGHNESS (physical armor), co-primary VITALITY (HP). The stats are derived by DELTA
    /// off the cloned Blacksmith row, so the "survival at-or-above / offense below the Blacksmith" identity
    /// holds regardless of the exact vanilla numbers (the self-test asserts it against the LIVE Blacksmith
    /// row, comparing field to field with nothing hardcoded).
    /// </summary>
    internal static class InnkeeperClass
    {
        private const string ClassKey = "ftkmf_innkeeper";

        public static void Register()
        {
            // 1) The class: a clone of the Blacksmith (id == array index, per the AddClass doc comment). In the
            //    configure lambda the row already carries the Blacksmith's cloned stats (ContentRegistry copies
            //    the template BEFORE running configure), so we adjust each stat by a signed delta. This makes the
            //    identity relative to the live vanilla peer, never a hardcoded number, and keeps the class inside
            //    the roster's per-stat peer budget (net total delta is a small negative, so it stays in range).
            FTK_playerGameStart innkeeper = Content.AddClass(
                Plugin.Guid, ClassKey, FTK_playerGameStart.ID.blacksmith, "Innkeeper",
                c =>
                {
                    // SURVIVAL (at or above the Blacksmith): the tank profile.
                    c._toughness += 0.10f; // PRIMARY: the hardest body in the party (most physical armor).
                    c._vitality  += 0.08f; // CO-PRIMARY: the deepest HP pool.
                    c._fortitude += 0.03f; // steady against hexes and bad brew (magic resist).

                    // OFFENSE / MOBILITY (below the Blacksmith): a slow, low-offense wall.
                    c._quickness -= 0.12f; // LOW Speed: a lumbering barkeep, always last to act.
                    c._talent    -= 0.08f; // unrefined in weapon finesse and skill checks.
                    c._awareness -= 0.06f; // slow to notice trouble coming.

                    c.m_DLC = FTK_dlc.ID.None; // base-game class (matches the Blacksmith it clones).
                    c.m_Release = true;        // available in release builds, not gated to dev/test.
                    // m_StartWeapon / m_StartItems / m_CharacterSkills / m_Skinsets are inherited from the
                    // Blacksmith clone (all valid), so the class is immediately usable in-game.
                });

            // Phase 1 DORMANT rule: the flavor must NOT advertise either trait (they do nothing yet).
            Localization.SetClassFlavor(ClassKey,
                "The innkeeper has weathered every barroom brawl and every rancid keg to come through his " +
                "door, and he simply refuses to go down. Slow to move and slower to anger, he plants his " +
                "boots and outlasts whatever the road throws at him.");

            // 2) The two showcase passives, bound DORMANT through the public API (one AddPassive call each).
            //    Phase 1: this only records the binding + registers the trait display name; no combat patch
            //    reads it yet. The returned def's Key is modGuid + ":" + passiveId (the stable trait identity).
            PassiveTraitDef stonewall = Content.AddPassive(
                Plugin.Guid, "stonewall", innkeeper, PassiveTrigger.IncomingAttack, "Stonewall");
            PassiveTraitDef ironBelly = Content.AddPassive(
                Plugin.Guid, "iron_belly", innkeeper, PassiveTrigger.ConsumableDebuff, "Iron Belly");

            // 3) Feedback string templates under the synthetic keys the Phase 2/3 Core trigger patches will read
            //    back via Localization.TryGetName. Convention: traitKey + ":hud" (the floating combat popup) and
            //    traitKey + ":log" (the combat-log line), where traitKey is the AddPassive Key. We derive the keys
            //    from the returned def.Key so they always match what the future patch computes. Board-game
            //    storybook voice; no em dashes; the log prose avoids the word "Steadfast" (a vanilla skill name).
            if (stonewall != null)
            {
                Localization.SetName(stonewall.Key + ":hud", "Stonewall!");
                Localization.SetName(stonewall.Key + ":log", "Solid as a doorpost, the Innkeeper turns the blow aside.");
            }
            if (ironBelly != null)
            {
                Localization.SetName(ironBelly.Key + ":hud", "Iron Belly!");
                Localization.SetName(ironBelly.Key + ":log", "The Innkeeper's cast-iron stomach shrugs it off.");
            }

            VerifyInnkeeper(stonewall, ironBelly);
        }

        /// <summary>
        /// SELF-TEST idiom (mirrors ThiefClass.VerifyThief): three independent checks, each emitting its own
        /// PASS/FAIL line. Nothing is hardcoded about the Blacksmith: the stat-identity check reads the LIVE
        /// Blacksmith row and compares it field to field against the LIVE Innkeeper row. The passive-bound check
        /// verifies through the public return values of AddPassive (and the public Localization path), never any
        /// Core internal registry.
        /// </summary>
        private static void VerifyInnkeeper(PassiveTraitDef stonewall, PassiveTraitDef ironBelly)
        {
            FTK_playerGameStartDB db = Content.Db<FTK_playerGameStartDB>();

            // --- check 1: registered, id == last array index, name resolves ---
            int id = db.GetIntFromID(ClassKey);
            int lastIndex = ((System.Array)Reflect.GetField(db, "m_Array")).Length - 1;
            FTK_playerGameStart ink = id >= 0 ? db.GetEntry((FTK_playerGameStart.ID)id) : null;
            string name = ink != null ? ink.GetDisplayName() : "(null)";
            bool registeredOk = ink != null && id == lastIndex && name == "Innkeeper";
            if (registeredOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: Innkeeper class registered (id == index) [id=" + id +
                    ", lastIndex=" + lastIndex + ", name=\"" + name + "\"].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper class registered (id == index); id=" + id +
                    " lastIndex=" + lastIndex + " name=\"" + name + "\".");

            // --- check 2: stat identity vs the LIVE Blacksmith row (survival >= Blacksmith, offense < Blacksmith) ---
            FTK_playerGameStart bs = db.GetEntry(FTK_playerGameStart.ID.blacksmith);
            bool statOk = false;
            if (ink != null && bs != null)
            {
                bool survivalOk =
                    ink._toughness >= bs._toughness &&
                    ink._vitality  >= bs._vitality  &&
                    ink._fortitude >= bs._fortitude;
                bool offenseOk =
                    ink._quickness < bs._quickness &&
                    ink._talent    < bs._talent    &&
                    ink._awareness < bs._awareness;
                statOk = survivalOk && offenseOk;

                if (statOk)
                    Plugin.Log.LogInfo("SELF-TEST PASS: Innkeeper stat identity (survival >= Blacksmith, offense " +
                        "< Blacksmith) [tough " + bs._toughness + "->" + ink._toughness + ", vit " + bs._vitality +
                        "->" + ink._vitality + ", fort " + bs._fortitude + "->" + ink._fortitude + " | speed " +
                        bs._quickness + "->" + ink._quickness + ", talent " + bs._talent + "->" + ink._talent +
                        ", aware " + bs._awareness + "->" + ink._awareness + "].");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper stat identity; survivalOk=" + survivalOk +
                        " offenseOk=" + offenseOk + " (tough " + bs._toughness + "->" + ink._toughness + ", vit " +
                        bs._vitality + "->" + ink._vitality + ", fort " + bs._fortitude + "->" + ink._fortitude +
                        ", speed " + bs._quickness + "->" + ink._quickness + ", talent " + bs._talent + "->" +
                        ink._talent + ", aware " + bs._awareness + "->" + ink._awareness + ").");
            }
            else
            {
                Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper stat identity; innkeeper=" +
                    (ink == null ? "null" : "ok") + " blacksmith=" + (bs == null ? "null" : "ok") + ".");
            }

            // --- check 3: both passives bound DORMANT via the public API, names + feedback templates registered ---
            string sName, iName, sHud, sLog, iHud, iLog;
            bool passivesOk =
                stonewall != null &&
                ironBelly != null &&
                stonewall.Key == Plugin.Guid + ":stonewall" &&
                ironBelly.Key == Plugin.Guid + ":iron_belly" &&
                stonewall.Trigger == PassiveTrigger.IncomingAttack &&
                ironBelly.Trigger == PassiveTrigger.ConsumableDebuff &&
                ink != null && stonewall.ClassId == id && ironBelly.ClassId == id &&
                // trait display names resolve through the Localization path
                Localization.TryGetName(stonewall.Key, out sName) && sName == "Stonewall" &&
                Localization.TryGetName(ironBelly.Key, out iName) && iName == "Iron Belly" &&
                // the four hud/log feedback templates the future Core patches read back
                Localization.TryGetName(stonewall.Key + ":hud", out sHud) && sHud == "Stonewall!" &&
                Localization.TryGetName(stonewall.Key + ":log", out sLog) && !string.IsNullOrEmpty(sLog) &&
                Localization.TryGetName(ironBelly.Key + ":hud", out iHud) && iHud == "Iron Belly!" &&
                Localization.TryGetName(ironBelly.Key + ":log", out iLog) && !string.IsNullOrEmpty(iLog);

            if (passivesOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: Innkeeper passives bound (stonewall, iron_belly) [" +
                    stonewall.Key + " @ IncomingAttack, " + ironBelly.Key + " @ ConsumableDebuff; classId=" + id +
                    "; names + hud/log templates registered].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper passives bound (stonewall, iron_belly); stonewall=" +
                    (stonewall == null ? "null" : stonewall.Key + "/" + stonewall.Trigger) + " ironBelly=" +
                    (ironBelly == null ? "null" : ironBelly.Key + "/" + ironBelly.Trigger) + " classId=" + id + ".");
        }
    }
}
