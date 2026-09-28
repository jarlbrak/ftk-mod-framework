using System;
using GridEditor;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    static JToken NativeGearHierarchy(GameObject prefab)
    {
        if (prefab == null) return new JValue((object)null);
        JArray nodes = new JArray();
        foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
        {
            JObject value = ItemTransform(node, prefab.transform);
            MeshFilter filter = node.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skin = node.GetComponent<SkinnedMeshRenderer>();
            value["nativeMeshName"] = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name :
                skin != null && skin.sharedMesh != null ? skin.sharedMesh.name : null;
            value["rendererType"] = skin != null ? "skinned" : node.GetComponent<MeshRenderer>() != null ? "rigid" : null;
            nodes.Add(value);
        }
        return new JObject { { "name", prefab.name }, { "nodes", nodes } };
    }

    JObject NativeGearMetadata(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "item");
        RequireSinglePlayer(); CatalogNoLinks(root);
        string key = Str(command, "item");
        if (Array.IndexOf(new[] { "bluntSmithHammer", "bluntWarHammer", "helmetHeavy1", "helmetCrown",
                "shieldblacksmith", "trinketDefense1", "amuletVitality1", "armorHeavy1", "bootsHeavy3" }, key) < 0)
            throw new ArgumentException("Only the Blacksmith native template routes are supported.");
        FTK_itembase.ID id = (FTK_itembase.ID)Enum.Parse(typeof(FTK_itembase.ID), key, false);
        FTK_itembase row = FTK_itembase.GetItemBase(id);
        if (row == null) throw new InvalidOperationException("Exact native row unavailable.");
        return new JObject { { "ok", true }, { "readOnly", true }, { "item", key },
            { "prefab", NativeGearHierarchy(row.m_Prefab) },
            { "wearableFemale", NativeGearHierarchy(row.m_WearablePrefab) },
            { "wearableMale", NativeGearHierarchy(row.m_WearablePrefabM) },
            { "scope", "Native transform hierarchy and renderer/mesh names only. No instantiation, asset mutation or native surface extraction." } };
    }
}
