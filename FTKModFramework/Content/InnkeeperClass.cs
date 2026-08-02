using GridEditor;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Spec #78 Phase 1 / FR-7 showcase class: the Innkeeper, an unshakeable barkeep cloned from the
    /// Blacksmith template. A pure tank identity, Toughness/Vitality-forward with deliberately low Speed
    /// and low offense, and the two showcase passives bound DORMANT via the public
    /// <see cref="Content.AddPassive"/> API (they do NOTHING in Phase 1; the trigger patches that read
    /// them land in Phases 2 and 3). The gameplay registration is authored SOLELY through the public
    /// Content.* surface; only the load-time self-test reaches for the framework-internal Reflect idiom (to
    /// read the live Blacksmith row for its field-by-field asserts), mirroring ThiefClass.VerifyThief.
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
                    // The Innkeeper is NOT a second Blacksmith. Toughness (physical armor) is the Blacksmith's
                    // signature stat, so it stays at the cloned baseline; the weight goes on the two stats a
                    // publican actually earns behind a bar.
                    c._vitality  += 0.10f; // PRIMARY: built like a barrel, the deepest HP pool in the party.
                    c._fortitude += 0.08f; // CO-PRIMARY: a lifetime of bad brew and worse magic bounces off.
                    c._awareness += 0.04f; // reads a room, and sees the brawl coming before it starts.

                    // Paid for in the places a barkeep has no business being good.
                    c._quickness -= 0.12f; // LOW Speed: a lumbering host, always last to act.
                    c._talent    -= 0.10f; // unrefined in weapon finesse and skill checks.

                    c.m_DLC = FTK_dlc.ID.None; // base-game class (matches the Blacksmith it clones).
                    c.m_Release = true;        // available in release builds, not gated to dev/test.

                    // SKILLS: take a PRIVATE copy before touching a single flag. ContentRegistry clones the
                    // template row with Reflect.CopyFields, a SHALLOW copy, so the cloned row's
                    // m_CharacterSkills initially points at the LIVE Blacksmith's object. Mutating it in place
                    // would edit the real Blacksmith (breaking a vanilla class and violating clone-and-register).
                    // CharacterSkills has a public copy constructor, so this is the game's own idiom.
                    c.m_CharacterSkills = new CharacterSkills(c.m_CharacterSkills);

                    // Steadfast is the Blacksmith's signature defence: a shield-gated chance to negate an
                    // incoming physical hit (DamageCalculator, AttackResponse.SteadFast). Inheriting it made the
                    // Innkeeper a second-rate Blacksmith AND randomly pre-empted its own passives, so it goes.
                    // The Innkeeper's defence is a cast-iron stomach, not a shield.
                    c.m_CharacterSkills.m_SteadFast = false;

                    // m_StartWeapon / m_Skinsets stay inherited from the Blacksmith clone (both valid), so the
                    // class is immediately usable in-game. m_StartItems is the inherited set PLUS a rum: an
                    // innkeeper never travels dry, and the rum is the vehicle BOTH consumable passives are
                    // verified against in-game (its buffs share, its enConfuse downside does not).
                    FTK_itembase.ID[] baseItems = c.m_StartItems ?? new FTK_itembase.ID[0];
                    FTK_itembase.ID[] withRum = new FTK_itembase.ID[baseItems.Length + 1];
                    System.Array.Copy(baseItems, withRum, baseItems.Length);
                    withRum[baseItems.Length] = FTK_itembase.ID.conRum;
                    c.m_StartItems = withRum;
                });

            Localization.SetClassFlavor(ClassKey,
                "The innkeeper has poured for every kind of trouble that ever shouldered through his door, and " +
                "swallowed worse from his own cellar. Nothing in a cup can touch him now, and no one drinks " +
                "alone at his table. Slow to move and slower to anger, he sets them up and keeps them coming.");

            // 2) The two showcase passives, both LIVE (spec #78 Phase 3 + the redesign). Both sit on the
            //    consumable path, which no vanilla CharacterSkills flag contests: the class earns its identity
            //    where nothing else in the game competes, rather than fighting the crowded damage-negate lane.
            //    The returned def's Key is modGuid + ":" + passiveId (the stable trait identity).
            PassiveTraitDef ironBelly = Content.AddPassive(
                Plugin.Guid, "iron_belly", innkeeper, PassiveTrigger.ConsumableDebuff, "Iron Belly");
            PassiveTraitDef roundOnTheHouse = Content.AddPassive(
                Plugin.Guid, "round_on_the_house", innkeeper, PassiveTrigger.ConsumableBuff, "Round on the House");

            // 3) Feedback string templates under the synthetic keys the Phase 2/3 Core trigger patches will read
            //    back via Localization.TryGetName. Convention: traitKey + ":hud" (the floating combat popup) and
            //    traitKey + ":log" (the combat-log line), where traitKey is the AddPassive Key. We derive the keys
            //    from the returned def.Key so they always match what the future patch computes. Board-game
            //    storybook voice; no em dashes; the log prose avoids the word "Steadfast" (a vanilla skill name).
            if (ironBelly != null)
            {
                Localization.SetName(ironBelly.Key + ":hud", "Iron Belly!");
                Localization.SetName(ironBelly.Key + ":log", "The Innkeeper's cast-iron stomach shrugs it off.");
            }
            if (roundOnTheHouse != null)
            {
                Localization.SetName(roundOnTheHouse.Key + ":hud", "Round on the House!");
                Localization.SetName(roundOnTheHouse.Key + ":log",
                    "The Innkeeper passes the bottle down the line, and nobody drinks alone.");
            }

            VerifyInnkeeper(ironBelly, roundOnTheHouse);
        }

        /// <summary>
        /// SELF-TEST idiom (mirrors ThiefClass.VerifyThief): four independent checks, each emitting its own
        /// PASS/FAIL line. Nothing is hardcoded about the Blacksmith: the stat-identity check reads the LIVE
        /// Blacksmith row and compares it field to field against the LIVE Innkeeper row. The passive-bound check
        /// verifies through the public return values of AddPassive (and the public Localization path), never any
        /// Core internal registry. Check 4 guards the shallow-clone trap that this class exists to demonstrate.
        /// </summary>
        private static void VerifyInnkeeper(PassiveTraitDef ironBelly, PassiveTraitDef roundOnTheHouse)
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

            // --- check 2: stat identity vs the LIVE Blacksmith row. The Innkeeper is DISTINCT from the Blacksmith,
            //     not a tougher one: it must out-last on vitality/fortitude, pay for it on speed/talent, and must
            //     NOT encroach on toughness, which is the Blacksmith's signature stat.
            FTK_playerGameStart bs = db.GetEntry(FTK_playerGameStart.ID.blacksmith);
            bool statOk = false;
            if (ink != null && bs != null)
            {
                bool enduranceOk =
                    ink._vitality  > bs._vitality  &&
                    ink._fortitude > bs._fortitude;
                bool costOk =
                    ink._quickness < bs._quickness &&
                    ink._talent    < bs._talent;
                bool notATankOk = ink._toughness <= bs._toughness; // toughness stays the Blacksmith's lane
                statOk = enduranceOk && costOk && notATankOk;

                if (statOk)
                    Plugin.Log.LogInfo("SELF-TEST PASS: Innkeeper stat identity (endurance > Blacksmith, cost paid " +
                        "on speed/talent, toughness NOT encroached) [vit " + bs._vitality + "->" + ink._vitality +
                        ", fort " + bs._fortitude + "->" + ink._fortitude + ", aware " + bs._awareness + "->" +
                        ink._awareness + " | speed " + bs._quickness + "->" + ink._quickness + ", talent " +
                        bs._talent + "->" + ink._talent + ", tough " + bs._toughness + "->" + ink._toughness + "].");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper stat identity; enduranceOk=" + enduranceOk +
                        " costOk=" + costOk + " notATankOk=" + notATankOk + " (vit " + bs._vitality + "->" +
                        ink._vitality + ", fort " + bs._fortitude + "->" + ink._fortitude + ", speed " +
                        bs._quickness + "->" + ink._quickness + ", talent " + bs._talent + "->" + ink._talent +
                        ", tough " + bs._toughness + "->" + ink._toughness + ").");
            }
            else
            {
                Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper stat identity; innkeeper=" +
                    (ink == null ? "null" : "ok") + " blacksmith=" + (bs == null ? "null" : "ok") + ".");
            }

            // --- check 3: both passives bound LIVE via the public API, names + feedback templates registered ---
            string iName, rName, iHud, iLog, rHud, rLog;
            bool passivesOk =
                ironBelly != null &&
                roundOnTheHouse != null &&
                ironBelly.Key == Plugin.Guid + ":iron_belly" &&
                roundOnTheHouse.Key == Plugin.Guid + ":round_on_the_house" &&
                ironBelly.Trigger == PassiveTrigger.ConsumableDebuff &&
                roundOnTheHouse.Trigger == PassiveTrigger.ConsumableBuff &&
                ink != null && ironBelly.ClassId == id && roundOnTheHouse.ClassId == id &&
                // trait display names resolve through the Localization path
                Localization.TryGetName(ironBelly.Key, out iName) && iName == "Iron Belly" &&
                Localization.TryGetName(roundOnTheHouse.Key, out rName) && rName == "Round on the House" &&
                // the four hud/log feedback templates the Core trigger patches read back
                Localization.TryGetName(ironBelly.Key + ":hud", out iHud) && iHud == "Iron Belly!" &&
                Localization.TryGetName(ironBelly.Key + ":log", out iLog) && !string.IsNullOrEmpty(iLog) &&
                Localization.TryGetName(roundOnTheHouse.Key + ":hud", out rHud) && rHud == "Round on the House!" &&
                Localization.TryGetName(roundOnTheHouse.Key + ":log", out rLog) && !string.IsNullOrEmpty(rLog);

            if (passivesOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: Innkeeper passives bound (iron_belly, round_on_the_house) [" +
                    ironBelly.Key + " @ ConsumableDebuff, " + roundOnTheHouse.Key + " @ ConsumableBuff; classId=" +
                    id + "; names + hud/log templates registered].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper passives bound (iron_belly, round_on_the_house); " +
                    "ironBelly=" + (ironBelly == null ? "null" : ironBelly.Key + "/" + ironBelly.Trigger) +
                    " roundOnTheHouse=" + (roundOnTheHouse == null
                        ? "null" : roundOnTheHouse.Key + "/" + roundOnTheHouse.Trigger) + " classId=" + id + ".");

            // --- check 4: the Innkeeper's skills are its OWN object and Steadfast is gone, while the LIVE
            //     Blacksmith still HAS Steadfast. This is the shallow-clone guard: ContentRegistry copies rows with
            //     Reflect.CopyFields, so without the defensive copy in Register() these two rows would share one
            //     CharacterSkills instance and clearing the flag here would silently disarm the real Blacksmith.
            bool skillsOk = false;
            if (ink != null && bs != null && ink.m_CharacterSkills != null && bs.m_CharacterSkills != null)
            {
                bool ownObject = !ReferenceEquals(ink.m_CharacterSkills, bs.m_CharacterSkills);
                bool innkeeperClean = !ink.m_CharacterSkills.m_SteadFast;
                bool blacksmithIntact = bs.m_CharacterSkills.m_SteadFast;
                skillsOk = ownObject && innkeeperClean && blacksmithIntact;

                if (skillsOk)
                    Plugin.Log.LogInfo("SELF-TEST PASS: Innkeeper skills are a private copy (no Blacksmith overlap) " +
                        "[distinct CharacterSkills instance, innkeeper.m_SteadFast=false, blacksmith.m_SteadFast=true].");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper skills private copy; ownObject=" + ownObject +
                        " innkeeperClean=" + innkeeperClean + " blacksmithIntact=" + blacksmithIntact +
                        " (a false blacksmithIntact means the shallow clone leaked and a VANILLA class was edited).");
            }
            else
            {
                Plugin.Log.LogError("SELF-TEST FAIL: Innkeeper skills private copy; innkeeper=" +
                    (ink == null ? "null" : "ok") + " blacksmith=" + (bs == null ? "null" : "ok") + ".");
            }
        }
    }
}
