// Executable checks for the Spec #253 save and load hooks, run against stand-ins for the game
// types they name. The save transpiler rewrites a stand-in of vanilla's tail sequence, which is
// then compiled and run, so the inserted stack order is exercised. The real IL shape was verified
// against the installed assembly by decompilation; the live round trip remains work item 3.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using FTKModFramework.Core;
using FullInspector;
using HarmonyLib;

namespace FullInspector
{
    public abstract class BaseSerializer { }
    public sealed class FullSerializerSerializer : BaseSerializer { }

    // Stand-in format: "key=value" pairs in ordinal key order, joined by ';'.
    public static class SerializationHelpers
    {
        public static int Parses;

        public static string SerializeToContent<T, TSerializer>(T value) where TSerializer : BaseSerializer
        {
            var state = (Dictionary<string, object>)(object)value;
            var keys = state.Keys.ToList();
            keys.Sort(StringComparer.Ordinal);
            return string.Join(";", keys.Select(k => k + "=" + state[k]));
        }

        public static T DeserializeFromContent<T, TSerializer>(string content) where TSerializer : BaseSerializer
        {
            Parses++;
            if (content == "throw") throw new FormatException("stand-in parse failure");
            var state = new Dictionary<string, object>();
            foreach (string pair in content.Split(';'))
            {
                int eq = pair.IndexOf('=');
                state[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }
            return (T)(object)state;
        }
    }
}

public class FTKNetworkObject { public bool m_IsSerialize = true; }
public class GameFlow : FTKNetworkObject { }
public class uiStartGame { }

namespace FTKModFramework.Core
{
    internal static class TweakSessionHooks
    {
        internal static int Failures;
        internal static void Failed(string via, Exception e) { Failures++; }
    }
}

internal sealed class MemoryStore : ITweakPreferenceStore
{
    internal readonly Dictionary<string, TweakPreference> Values = new Dictionary<string, TweakPreference>();
    public TweakPreference Read(string id) { TweakPreference value; return Values.TryGetValue(id, out value) ? value : TweakPreference.Default; }
    public void Write(string id, TweakPreference preference) { Values[id] = preference; }
}

internal static class Program
{
    private static int _checks;
    private static readonly List<string> Warnings = new List<string>(), Infos = new List<string>(), Errors = new List<string>();
    private static readonly MethodInfo Serialize = typeof(SerializationHelpers).GetMethod("SerializeToContent")
        .MakeGenericMethod(typeof(Dictionary<string, object>), typeof(FullSerializerSerializer));

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("session record hooks: " + message);
    }

    /// <summary>Vanilla's tail: the finished dictionary (here argument 1), the serialize call, return.</summary>
    private static List<CodeInstruction> Vanilla()
    {
        return new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, Serialize),
            new CodeInstruction(OpCodes.Ret) };
    }

    private static Func<FTKNetworkObject, Dictionary<string, object>, string> Compile(List<CodeInstruction> code)
    {
        var method = new DynamicMethod("StateDataSerialize", typeof(string), new[] { typeof(FTKNetworkObject), typeof(Dictionary<string, object>) },
            typeof(Program).Module, true);
        ILGenerator il = method.GetILGenerator();
        foreach (CodeInstruction instruction in code)
        {
            if (instruction.operand is MethodInfo call) il.Emit(instruction.opcode, call);
            else il.Emit(instruction.opcode);
        }
        return (Func<FTKNetworkObject, Dictionary<string, object>, string>)method.CreateDelegate(typeof(Func<FTKNetworkObject, Dictionary<string, object>, string>));
    }

    private static Dictionary<string, object> State()
    {
        return new Dictionary<string, object> { { "m_RoundCount", 4 }, { "m_Chaos", 2 } };
    }

    private static int Mismatches()
    {
        return Warnings.Count(w => w.Contains("StateDataSerialize no longer matches"));
    }

    private static void Invoke(Type patch, params object[] args)
    {
        patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }

    private static void Main()
    {
        Tweaks.Warn = Warnings.Add;
        Tweaks.Info = Infos.Add;
        Tweaks.Error = Errors.Add;
        FrameworkTweaks.RegisterAll(Tweaks.Registry, true);
        string probe = FrameworkTweaks.SessionProbeDescriptor.Id;
        var store = new MemoryStore();
        store.Values[probe] = TweakPreference.On;
        Check(Tweaks.Registry.Initialize(store), "the registry initializes");

        Save();
        Drift();
        Load(probe);
        Check(Errors.Count == 0, "no self-test failure: " + string.Join(" | ", Errors));
        Console.WriteLine("SessionRecordHooks: " + _checks + " checks passed (save transpiler, IL drift, load prefix, resume arm; game stand-ins, no live proof).");
    }

    // FR-2: one insertion before the serialize call, the key only for a locked GameFlow, and
    // vanilla's output otherwise.
    private static void Save()
    {
        List<CodeInstruction> source = Vanilla();
        List<CodeInstruction> patched = TweakSessionRecordSavePatch.Transpiler(source).ToList();
        Check(source.Count == 3 && patched.Count == 5, "two instructions are inserted and the input is untouched");
        Check(patched[0].opcode == OpCodes.Ldarg_1 && patched[1].opcode == OpCodes.Ldarg_0 && patched[2].opcode == OpCodes.Call
            && ((MethodInfo)patched[2].operand).Name == "Decorate" && ReferenceEquals(patched[3], source[1]),
            "ldarg.0 and the Decorate call sit just before the serialize call");
        // Harmony always transpiles the original IL, so a second pass is never idempotence-relevant;
        // it only shows the Decorate call is not counted as a serialize call.
        Check(TweakSessionRecordSavePatch.Transpiler(patched).Count() == 7 && Mismatches() == 0, "the Decorate call is not counted as a serialize call");

        var original = Compile(Vanilla());
        var hooked = Compile(patched);
        var flow = new GameFlow();
        Check(hooked(flow, State()) == original(flow, State()) && !hooked(flow, State()).Contains(TweakSessionRecord.Key),
            "at the title screen the output is vanilla's");
        Tweaks.Session.Capture(0, "test");
        Check(hooked(flow, State()) == original(flow, State()), "during setup the output is vanilla's");
        Tweaks.Session.Lock(0, "test");
        string record = Tweaks.Session.RecordToWrite();
        Dictionary<string, object> expected = State();
        expected[TweakSessionRecord.Key] = record;
        Check(record != null && hooked(flow, State()) == original(flow, expected), "a locked GameFlow gains exactly the record: " + hooked(flow, State()));
        var other = new FTKNetworkObject();
        Check(hooked(other, State()) == original(other, State()), "other network objects are untouched in a locked run");
        Tweaks.Session.Clear(TweakClearTrigger.RunEnd, "test");
        Check(hooked(flow, State()) == original(flow, State()), "after the run ends the output is vanilla's again");
    }

    // FR-2 drift: no insertion and one warning unless exactly one call matches; labels move.
    private static void Drift()
    {
        var none = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ret) };
        MethodInfo wrongType = typeof(SerializationHelpers).GetMethod("SerializeToContent").MakeGenericMethod(typeof(string), typeof(FullSerializerSerializer));
        var foreign = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldstr, "x"), new CodeInstruction(OpCodes.Call, wrongType), new CodeInstruction(OpCodes.Ret) };
        List<CodeInstruction> twice = Vanilla();
        twice.InsertRange(0, new[] { new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, Serialize), new CodeInstruction(OpCodes.Pop) });
        foreach (List<CodeInstruction> bad in new[] { none, foreign, twice })
        {
            List<CodeInstruction> result = TweakSessionRecordSavePatch.Transpiler(bad).ToList();
            Check(result.Count == bad.Count && result.Zip(bad, ReferenceEquals).All(same => same), "a mismatch returns the original IL");
        }
        Check(Mismatches() == 1, "the mismatch is warned about once: " + Mismatches());

        Label label = new DynamicMethod("labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        List<CodeInstruction> labelled = Vanilla();
        labelled[1].labels.Add(label);
        List<CodeInstruction> moved = TweakSessionRecordSavePatch.Transpiler(labelled).ToList();
        Check(moved[1].labels.Count == 1 && moved[1].labels[0] == label && moved[3].labels.Count == 0,
            "a branch into the serialize call lands on the inserted load");
    }

    // FR-3: the arm honors a veto, and the load parses only for an armed, unlocked GameFlow.
    private static void Load(string probe)
    {
        Type arm = typeof(TweakSessionResumeArmPatch), load = typeof(TweakSessionRecordLoadPatch);
        Tweaks.Session.Capture(0, "test");
        Tweaks.Session.Lock(0, "test");
        var saved = new Dictionary<string, object>(State());
        saved[TweakSessionRecord.Key] = Tweaks.Session.RecordToWrite();
        string content = SerializationHelpers.SerializeToContent<Dictionary<string, object>, FullSerializerSerializer>(saved);
        Check(content.Contains("+" + probe), "the saved state records the probe on");
        Tweaks.Session.Clear(TweakClearTrigger.RunEnd, "test");

        int handle;
        Tweaks.Registry.TryGetHandle(probe, out handle);
        Tweaks.Registry.Toggle(handle);
        Invoke(arm, false);
        Check(!Tweaks.Session.ResumeArmed, "a vetoed resume does not arm");
        Invoke(arm, true);
        Check(Tweaks.Session.ResumeArmed && TweakSessionHooks.Failures == 0, "a resume arms");
        Tweaks.Session.Capture(0, "GameLogic.CreateOfflineRoom");
        Check(!Tweaks.IsOn(handle) && Tweaks.Registry.SessionSource == TweakRegistry.PreferencesSource, "the changed preference reaches the capture");

        SerializationHelpers.Parses = 0;
        Invoke(load, new FTKNetworkObject(), content);
        Invoke(load, new GameFlow { m_IsSerialize = false }, content);
        Invoke(load, new GameFlow(), string.Empty);
        Invoke(load, new GameFlow(), null);
        Check(SerializationHelpers.Parses == 0 && Tweaks.Session.ResumeRecord == null, "other objects, unserialized objects and empty content parse nothing");
        Invoke(load, new GameFlow(), content);
        Check(SerializationHelpers.Parses == 1 && Tweaks.IsOn(handle) && Tweaks.Registry.SessionSource == TweakRegistry.SaveSource,
            "the GameFlow load restores the saved set");
        Tweaks.Session.Lock(0, "uiStartGame.EnterFahrulRPC");
        Invoke(load, new GameFlow(), content);
        Check(SerializationHelpers.Parses == 1 && Tweaks.IsOn(handle), "a locked run parses nothing, as for a PunRPC");
        Tweaks.Session.Clear(TweakClearTrigger.RunEnd, "test");
        Invoke(load, new GameFlow(), content);
        Check(SerializationHelpers.Parses == 1, "an unarmed load parses nothing");

        Invoke(arm, true);
        Tweaks.Session.Capture(0, "test");
        int before = Warnings.Count;
        Invoke(load, new GameFlow(), "throw");
        Invoke(load, new GameFlow(), "throw");
        Check(Warnings.Count == before + 1 && Warnings[before].Contains("reading the Session record failed"), "a parse failure is caught and warned about once");
        Check(Tweaks.Registry.SessionSource == TweakRegistry.PreferencesSource && Tweaks.Session.ResumeRecord == null,
            "after a parse failure the run keeps its capture");
        Tweaks.Session.Clear(TweakClearTrigger.SceneReload, "test");
    }
}
