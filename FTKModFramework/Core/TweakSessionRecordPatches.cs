using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FullInspector;
using HarmonyLib;

namespace FTKModFramework.Core
{
    // Save-bound Session record hooks (Spec #253 FR-2, FR-3). The record lives under
    // TweakSessionRecord.Key in the GameFlow state dictionary. No type overrides
    // StateDataSerialize or StateDataDeserialize, so both hooks sit on FTKNetworkObject and run for
    // every serialized network object at every save, autosave and load; the non-GameFlow path is a
    // single type check. Every hook catches its own exceptions and never skips vanilla.
    //
    // The same two hooks carry fix.poison-decay-resume (Spec #260 FR-1, FR-2): the save adds
    // PoisonCountdown.Key to a poisoned CharacterStats mid-countdown, the load prefix stashes it
    // during a resume, and a CharacterStats.StateDataDeserializeDone prefix applies it.
    internal static class TweakSessionRecordHooks
    {
        internal const string LoadVia = "GameFlow.StateDataDeserialize";
        internal const string PoisonVia = "CharacterStats.StateDataDeserializeDone";
        private static bool _saveFailureWarned, _loadFailureWarned, _poisonFailureWarned;

        // CharacterStats.m_PoisonTimeCounter is private. The field is looked up once, on first use
        // inside a try block, so a renamed field warns instead of failing the type initializer. A
        // FieldInfo rather than a FieldRefAccess delegate: these paths run once per character per save
        // or load, so boxing costs nothing that matters, and the game-free hooks test can run the
        // same accessor on a modern runtime, where HarmonyX 2.7 cannot emit field refs.
        private static FieldInfo _poisonCounter;

        private static FieldInfo PoisonCounter()
        {
            if (_poisonCounter == null)
            {
                FieldInfo field = AccessTools.Field(typeof(CharacterStats), "m_PoisonTimeCounter");
                if (field == null || field.FieldType != typeof(int))
                    throw new MissingFieldException("CharacterStats", "m_PoisonTimeCounter");
                _poisonCounter = field;
            }
            return _poisonCounter;
        }

        internal static void Warn(string message)
        {
            Action<string> warn = Tweaks.Warn;
            if (warn != null) warn(message);
        }

        /// <summary>Called from the transpiled StateDataSerialize with the finished state dictionary,
        /// just before FullSerializer writes it. Returns the same dictionary; on any failure it is
        /// returned unchanged, so the save is vanilla's.</summary>
        internal static Dictionary<string, object> Decorate(Dictionary<string, object> state, FTKNetworkObject self)
        {
            if (!(self is GameFlow))
            {
                CharacterStats stats = self as CharacterStats;
                if (stats != null) DecoratePoison(state, stats);
                return state;
            }
            try { Tweaks.Session.WriteRecord(state); }
            catch (Exception e)
            {
                if (!_saveFailureWarned)
                {
                    _saveFailureWarned = true;
                    Warn("Tweaks: writing the Session record failed; this save has no record and is otherwise unaffected: " + e);
                }
            }
            return state;
        }

        /// <summary>FR-1. With the tweak off, outside a locked run or without poison this is a few
        /// field reads and writes nothing, so the state matches vanilla.</summary>
        private static void DecoratePoison(Dictionary<string, object> state, CharacterStats stats)
        {
            try
            {
                bool on = Tweaks.IsOn(FrameworkTweaks.PoisonDecayResume);
                bool locked = Tweaks.Registry.SessionState == TweakSessionState.Locked;
                int level = stats.m_PoisonLvl;
                if (state == null || !on || !locked || level <= 0) return;
                string value = PoisonCountdown.PoisonToWrite(locked, on, level, (int)PoisonCounter().GetValue(stats));
                if (value != null) state[PoisonCountdown.Key] = value;
            }
            catch (Exception e)
            {
                WarnPoison("saving the poison countdown failed; this save keeps vanilla's character state", e);
            }
        }

        /// <summary>FR-2 stash, before vanilla reads a CharacterStats state. Parses the string again
        /// only inside a resume's players window and only when it contains the key: at most once per
        /// saved character.</summary>
        internal static void StashPoison(FTKNetworkObject self, string content)
        {
            CharacterStats stats = self as CharacterStats;
            if (stats == null) return;
            try
            {
                TweakSessionLifecycle session = Tweaks.Session;
                if (!session.ResumePlayersOpen || !stats.m_IsSerialize || !PoisonCountdown.MayCarry(content)) return;
                Dictionary<string, object> state =
                    SerializationHelpers.DeserializeFromContent<Dictionary<string, object>, FullSerializerSerializer>(content);
                object value;
                if (state != null && state.TryGetValue(PoisonCountdown.Key, out value)) session.StashPoison(stats, value);
            }
            catch (Exception e)
            {
                WarnPoison("reading the saved poison countdown failed; the character keeps vanilla's countdown and vanilla's load is unaffected", e);
            }
        }

        /// <summary>FR-2 apply, from CharacterStats.StateDataDeserializeDone: vanilla has restored
        /// m_PoisonLvl, and nothing after it has read the countdown yet.</summary>
        internal static void ApplyPoison(CharacterStats stats)
        {
            try
            {
                TweakSessionLifecycle session = Tweaks.Session;
                if (stats == null || session.PoisonPending == 0) return;
                int counter = session.TakePoison(stats, Tweaks.IsOn(FrameworkTweaks.PoisonDecayResume), stats.m_IsSerialize,
                    stats.m_PoisonLvl, PoisonVia);
                if (counter > 0) PoisonCounter().SetValue(stats, counter);
            }
            catch (Exception e)
            {
                WarnPoison("restoring the poison countdown failed; the character keeps vanilla's countdown and vanilla's load is unaffected", e);
            }
        }

        private static void WarnPoison(string message, Exception e)
        {
            if (_poisonFailureWarned) return;
            _poisonFailureWarned = true;
            Warn("Tweaks: " + message + ": " + e);
        }

        internal static void Read(FTKNetworkObject self, string content)
        {
            if (!(self is GameFlow))
            {
                StashPoison(self, content);
                return;
            }
            try
            {
                // Checked before parsing, so the extra parse happens at most once per resume, on the
                // host's real load, and never for a PunRPC or a later load of a locked run.
                if (!Tweaks.Session.WantsRecord || !self.m_IsSerialize || string.IsNullOrEmpty(content)) return;
                Dictionary<string, object> state =
                    SerializationHelpers.DeserializeFromContent<Dictionary<string, object>, FullSerializerSerializer>(content);
                Tweaks.Session.ReadState(state, LoadVia);
            }
            catch (Exception e)
            {
                if (!_loadFailureWarned)
                {
                    _loadFailureWarned = true;
                    Warn("Tweaks: reading the Session record failed; the resumed run's Session tweaks follow the capture and vanilla's load is unaffected: " + e);
                }
            }
        }
    }

    // FR-2. A transpiler rather than a postfix: a postfix only sees the serialized string, and
    // adding the key there means parsing and re-serializing the whole state, which round-trips
    // object-typed values such as GameFlow.Rules2 through a path nobody has proven faithful. Here
    // the key joins the dictionary vanilla already built, so with nothing to add the output is
    // vanilla's byte for byte. The insertion is
    //     ldloc.0                  (vanilla, the branch target of the loop's leave)
    //     ldarg.0; call Decorate   (inserted)
    //     call SerializeToContent<Dictionary<string, object>, FullSerializerSerializer>
    // and is made only when exactly one such call exists. On any mismatch the original IL is
    // returned, one warning is logged and saves carry no record. The transpiler never throws,
    // because an exception here would abort the plugin's single PatchAll.
    [HarmonyPatch(typeof(FTKNetworkObject), "StateDataSerialize", new[] { typeof(bool) })]
    internal static class TweakSessionRecordSavePatch
    {
        private static bool _mismatchWarned;

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> original = new List<CodeInstruction>(instructions);
            try
            {
                int count;
                int match = TweakSessionRecord.SingleMatch(original, IsStateSerializeCall, out count);
                if (match < 0)
                {
                    WarnMismatch(count + " matching SerializeToContent calls, expected 1");
                    return original;
                }
                MethodInfo decorate = typeof(TweakSessionRecordHooks).GetMethod("Decorate", BindingFlags.Static | BindingFlags.NonPublic);
                if (decorate == null)
                {
                    WarnMismatch("the Decorate hook is missing");
                    return original;
                }
                CodeInstruction call = original[match];
                CodeInstruction self = new CodeInstruction(OpCodes.Ldarg_0);
                List<CodeInstruction> code = new List<CodeInstruction>(original);
                code.Insert(match, self);
                code.Insert(match + 1, new CodeInstruction(OpCodes.Call, decorate));
                // A branch into the call must now land on the inserted load. The audited IL has no
                // label there, since the leave targets the ldloc.0 before it.
                self.labels.AddRange(call.labels);
                call.labels.Clear();
                return code;
            }
            catch (Exception e)
            {
                WarnMismatch(e.Message);
                return original;
            }
        }

        private static bool IsStateSerializeCall(CodeInstruction instruction)
        {
            if (instruction.opcode != OpCodes.Call) return false;
            MethodInfo method = instruction.operand as MethodInfo;
            if (method == null || method.Name != "SerializeToContent" || method.DeclaringType != typeof(SerializationHelpers)
                || !method.IsGenericMethod) return false;
            Type[] arguments = method.GetGenericArguments();
            return arguments.Length == 2 && arguments[0] == typeof(Dictionary<string, object>)
                && arguments[1] == typeof(FullSerializerSerializer);
        }

        private static void WarnMismatch(string reason)
        {
            if (_mismatchWarned) return;
            _mismatchWarned = true;
            TweakSessionRecordHooks.Warn("Tweaks: FTKNetworkObject.StateDataSerialize no longer matches the audited IL (" + reason
                + "); saves will carry no Session record and resumed runs follow the current preferences.");
        }
    }

    // FR-3. A prefix cannot see the dictionary vanilla parses into a local, so it parses the string
    // itself with the same FullSerializer helper, only for GameFlow and only while a resume wants
    // the record. The GameFlow state is a few ints, a bool and the Rules2 parameters, and vanilla
    // already parses it an extra time in GameSerialize.Load for saves with the old difficulty field,
    // so one more parse per resume is negligible. Returning void, it can never skip the original.
    // For CharacterStats it parses only a saved character's state that carries the poison key,
    // inside a resume's players window, which PlayerSerialize.Deserialize reaches after the lock.
    [HarmonyPatch(typeof(FTKNetworkObject), "StateDataDeserialize", new[] { typeof(string), typeof(bool) })]
    internal static class TweakSessionRecordLoadPatch
    {
        private static void Prefix(FTKNetworkObject __instance, string _s)
        {
            TweakSessionRecordHooks.Read(__instance, _s);
        }
    }

    // Spec #260 FR-2 apply. Not a StateDataDeserialize postfix: on the loading machine every RPC
    // in the character chain executes at once (NetworkingPeer.RPC calls ExecuteRpc for the local
    // player and the local master), so StateDataDeserializeDone's PlayerClientCreatedRPC,
    // CreatePlayerFinishedRPC and the next CreatePlayer, up to AllCowsCreated, all run inside the
    // first character's StateDataDeserialize. Its postfix would run last, after the window closed.
    // This prefix runs inside the original after the field loop and before that chain.
    [HarmonyPatch(typeof(CharacterStats), "StateDataDeserializeDone")]
    internal static class TweakPoisonApplyPatch
    {
        private static void Prefix(CharacterStats __instance)
        {
            TweakSessionRecordHooks.ApplyPoison(__instance);
        }
    }

    // Spec #260 FR-2 window close. The host sends AllCowsCreated from CreatePlayerFinishedRPC once
    // m_CreatePlayerCount reaches m_CreateUIs.Count. A saved character reaches that count only
    // through its own StateDataDeserializeDone, PlayerClientCreatedRPC and CreatePlayerFinishedRPC,
    // so every saved CharacterStats has been deserialized by the time this runs. A prefix, so the
    // window is closed before DeserializeFinal starts the resumed game.
    [HarmonyPatch(typeof(uiStartGame), "AllCowsCreated")]
    internal static class TweakResumePlayersClosePatch
    {
        private static void Prefix()
        {
            try { Tweaks.Session.CloseResumePlayers("uiStartGame.AllCowsCreated"); }
            catch (Exception e) { TweakSessionHooks.Failed("uiStartGame.AllCowsCreated", e); }
        }
    }

    // FR-3 arm. A prefix, so the arm precedes everything OnResumeGame2 starts, including a
    // synchronous offline room creation. Last priority so __runOriginal reflects every other
    // prefix: HotReloadSessionEntry and SaveNamespace.GuardResume can both veto a resume, and
    // HarmonyX still runs later prefixes after a veto.
    [HarmonyPatch(typeof(uiStartGame), "OnResumeGame")]
    internal static class TweakSessionResumeArmPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(bool __runOriginal)
        {
            if (!__runOriginal) return;
            try { Tweaks.Session.ArmResume("uiStartGame.OnResumeGame"); }
            catch (Exception e) { TweakSessionHooks.Failed("uiStartGame.OnResumeGame", e); }
        }
    }
}
