using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    const string EquipRpcDiagnosticFlag = "FTK_MODEL_TEST_EQUIP_RPC_TRACE";
    bool equipRpcDiagnosticEnabled;

    sealed class EquipRpcTrace
    {
        internal RuntimeModelTest owner;
        internal JObject identity;
    }

    EquipRpcTrace CreateEquipRpcTrace(JObject command, int ordinal)
    {
        if (!equipRpcDiagnosticEnabled) return null;
        return new EquipRpcTrace { owner = this, identity = new JObject {
            { "commandId", Str(command, "id") }, { "session", sessionId },
            { "configSha256", packageGearConfigHash }, { "ordinal", ordinal },
            { "op", "package-gear-equip" }, { "diagnostic", true } } };
    }

    static void ObserveEquipRpc(EquipRpcTrace trace, CharacterOverworld hero, BlacksmithGearEntry entry, string phase)
    {
        if (ReferenceEquals(trace, null)) return;
        // Called synchronously on the existing command thread. Never wrap or replace ForceEquip.
        try
        {
            JObject record = (JObject)trace.identity.DeepClone();
            record["phase"] = phase;
            record["utc"] = DateTime.UtcNow.ToString("o");
            record["frame"] = Time.frameCount;
            record["managedThread"] = System.Threading.Thread.CurrentThread.ManagedThreadId;
            record["item"] = entry.stringId;
            record["numericId"] = entry.numericId;
            record["arguments"] = new JArray((int)entry.itemId, false);
            record["argumentTypes"] = new JArray(typeof(GridEditor.FTK_itembase.ID).FullName, typeof(bool).FullName);
            record["heroInstanceId"] = hero.GetInstanceID();
            record["heroType"] = hero.GetType().AssemblyQualifiedName;
            record["classId"] = (int)hero.m_CharacterStats.m_CharacterClass;
            record["classKey"] = hero.GetDBEntry().m_ID;
            record["container"] = entry.container.ToString();
            record["equippedCount"] = ExactCount(hero.m_PlayerInventory.Get(entry.container), entry.itemId);
            record["backpackCount"] = ExactCount(hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack), entry.itemId);
            if (phase == "before-force-equip")
            {
                try { record["rpcSnapshot"] = EquipRpcSnapshot(hero); }
                catch (Exception error) { record["snapshotError"] = error.ToString(); }
            }
            trace.owner.WriteEquipRpcRecord(record);
        }
        catch (Exception error)
        {
            try { trace.owner.Logger.LogWarning("EQUIP RPC DIAGNOSTIC UNAVAILABLE: " + error); } catch (Exception) { }
        }
    }

    void WriteEquipRpcRecord(JObject record)
    {
        // Dispose each record before native execution; WriteThrough requests OS write-through as well.
        string path = Path.Combine(output, sessionId + ".equip-rpc.jsonl");
        using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.WriteLine(record.ToString(Newtonsoft.Json.Formatting.None));
            writer.Flush();
            stream.Flush();
        }
    }

    static JObject EquipRpcSnapshot(CharacterOverworld hero)
    {
        Type peerType = typeof(CharacterOverworld).Assembly.GetType("NetworkingPeer", true);
        FieldInfo peerField = typeof(PhotonNetwork).GetField("networkingPeer", Statics);
        object peer = peerField.GetValue(null);
        IDictionary methods = (IDictionary)peerType.GetField("monoRPCMethodsCache", Members).GetValue(peer);
        Type extensions = typeof(CharacterOverworld).Assembly.GetType("Extensions", true);
        IDictionary parameters = (IDictionary)extensions.GetField("ParametersOfMethods", Statics).GetValue(null);
        PhotonView view = hero.GetComponent<PhotonView>();
        if (view == null) throw new InvalidOperationException("Exact hero PhotonView unavailable.");
        JArray components = new JArray();
        Array cachedComponents = (Array)typeof(PhotonView).GetField("RpcMonoBehaviours", Members).GetValue(view);
        IList observed = (IList)typeof(PhotonView).GetField("ObservedComponents", Members).GetValue(view);
        if (cachedComponents != null)
            foreach (object component in cachedComponents)
            {
                if (ReferenceEquals(component, null)) { components.Add(new JValue((object)null)); continue; }
                Type type = component.GetType();
                JArray candidates = new JArray();
                bool present = methods.Contains(type);
                if (present)
                    foreach (MethodInfo method in (IEnumerable)methods[type])
                        if (method.Name == "EquipItemRPC")
                        {
                            JObject candidate = EquipRpcMethod(method);
                            candidate["parameterCachePresent"] = parameters.Contains(method);
                            if (parameters.Contains(method)) candidate["cachedParameters"] = EquipRpcParameters((ParameterInfo[])parameters[method]);
                            candidates.Add(candidate);
                        }
                components.Add(new JObject { { "type", type.AssemblyQualifiedName },
                    { "instanceId", ((Component)component).GetInstanceID() }, { "isExactHero", ReferenceEquals(component, hero) },
                    { "isObservedFirst", observed != null && observed.Count > 0 && ReferenceEquals(component, observed[0]) },
                    { "methodCachePresent", present }, { "equipCandidates", candidates } });
            }
        JArray nativeMethods = new JArray();
        Type[] equipArgs = { typeof(GridEditor.FTK_itembase.ID), typeof(bool) };
        foreach (string name in new[] { "ForceEquip", "EquipItem", "EquipItemRPC" })
            nativeMethods.Add(EquipRpcMethod(typeof(CharacterOverworld).GetMethod(name, Members, null, equipArgs, null)));
        nativeMethods.Add(EquipRpcMethod(peerType.GetMethod("ExecuteRpc", Members)));
        return new JObject { { "viewInstanceId", view.GetInstanceID() }, { "viewId", view.viewID },
            { "ownerId", view.ownerId }, { "prefixBackup", view.prefixBackup },
            { "componentCachePresent", cachedComponents != null }, { "cachedComponents", components },
            { "nativeMethods", nativeMethods }, { "scope", "Existing cache and metadata only; no refresh, invocation, pointer preparation or cache population." } };
    }

    static JArray EquipRpcParameters(ParameterInfo[] parameters)
    {
        JArray result = new JArray();
        foreach (ParameterInfo parameter in parameters)
            result.Add(new JObject { { "name", parameter.Name }, { "type", parameter.ParameterType.AssemblyQualifiedName }, { "position", parameter.Position } });
        return result;
    }

    static JObject EquipRpcMethod(MethodInfo method)
    {
        if (method == null) throw new InvalidOperationException("Expected native method unavailable.");
        byte[] il = method.GetMethodBody() == null ? null : method.GetMethodBody().GetILAsByteArray();
        string hash = null;
        if (il != null) using (SHA256 sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(il)).Replace("-", "").ToLowerInvariant();
        JArray owners = new JArray();
        var patches = Harmony.GetPatchInfo(method);
        if (patches != null) foreach (string owner in patches.Owners) owners.Add(owner);
        return new JObject { { "name", method.Name }, { "declaringType", method.DeclaringType.AssemblyQualifiedName },
            { "moduleMvid", method.Module.ModuleVersionId.ToString() }, { "metadataToken", method.MetadataToken },
            { "returnType", method.ReturnType.AssemblyQualifiedName }, { "parameters", EquipRpcParameters(method.GetParameters()) },
            { "managedIlSha256", hash }, { "harmonyOwners", owners } };
    }
}
