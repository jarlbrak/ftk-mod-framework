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
    internal static class TweakSessionRecordHooks
    {
        internal const string LoadVia = "GameFlow.StateDataDeserialize";
        private static bool _saveFailureWarned, _loadFailureWarned;

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
            if (!(self is GameFlow)) return state;
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

        internal static void Read(FTKNetworkObject self, string content)
        {
            if (!(self is GameFlow)) return;
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
    [HarmonyPatch(typeof(FTKNetworkObject), "StateDataDeserialize", new[] { typeof(string), typeof(bool) })]
    internal static class TweakSessionRecordLoadPatch
    {
        private static void Prefix(FTKNetworkObject __instance, string _s)
        {
            TweakSessionRecordHooks.Read(__instance, _s);
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
