using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using FTKModFramework.Core.UI.Skyharbor;
using GridEditor;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FTKModFramework.Core
{
    // SKY SPIKE airship boat types (env-gated live-test spike, not a supported API). Four boat types, each with
    // its own item and FTK_boat row at deterministic ids, its own model on the overworld and its own model under
    // the party in sea combat. Models are generated GLBs read from
    // <plugins>/FTKModFramework_content/skyspike/airships/<key>.glb; a missing or unreadable GLB falls back to the
    // embedded Skyharbor airship. Placement is tunable per key without a rebuild through airships.json beside them.
    internal static partial class SkySpike
    {
        internal sealed class ShipType
        {
            public string Key, Name, Tagline, Description, ItemId, RowId;
            public int HealthDelta, ArmorDelta, BaseSlotDelta, TerrainSlotDelta;
            public float GoldScale;
            public int Item = -1, Row = -1;
        }

        // Clipper keeps the first spike's item and row ids, so existing spike saves resolve to it.
        static readonly ShipType[] Ships =
        {
            new ShipType
            {
                Key = "clipper", Name = "Skyreach Clipper", Tagline = "A merchant clipper under a cream envelope",
                Description = "A wooden merchant hull under a cream cigar envelope. A dependable starter airship for crossing the cloud sea.",
                ItemId = "skyspike_airship", RowId = "skyspike_airship_boat",
                HealthDelta = 2, ArmorDelta = 0, BaseSlotDelta = 0, TerrainSlotDelta = 0, GoldScale = 1f
            },
            new ShipType
            {
                Key = "stormbreaker", Name = "Stormbreaker", Tagline = "An armored warship under twin envelopes",
                Description = "An armored war hull slung beneath twin grey-green envelopes, bristling with cannons. Very tough, but slow to travel.",
                ItemId = "skyspike_ship_stormbreaker", RowId = "skyspike_ship_stormbreaker_boat",
                HealthDelta = 7, ArmorDelta = 3, BaseSlotDelta = -1, TerrainSlotDelta = -1, GoldScale = 2.5f
            },
            new ShipType
            {
                Key = "dandelion", Name = "Dandelion Skiff", Tagline = "A light gondola under a patchwork balloon",
                Description = "A small gondola beneath a patchwork balloon. Fragile, but it rides the winds quickly.",
                ItemId = "skyspike_ship_dandelion", RowId = "skyspike_ship_dandelion_boat",
                HealthDelta = -1, ArmorDelta = -1, BaseSlotDelta = 2, TerrainSlotDelta = 2, GoldScale = 0.8f
            },
            new ShipType
            {
                Key = "sunwing", Name = "Sunwing Galleon", Tagline = "An ornate galleon with golden wing sails",
                Description = "An ornate galleon with golden wing sails and a griffin figurehead. Sturdy, swift and costly.",
                ItemId = "skyspike_ship_sunwing", RowId = "skyspike_ship_sunwing_boat",
                HealthDelta = 5, ArmorDelta = 2, BaseSlotDelta = 1, TerrainSlotDelta = 1, GoldScale = 4f
            },
        };

        static readonly Dictionary<int, ShipType> ShipsByItem = new Dictionary<int, ShipType>();
        // FTK_SKY_SPIKE_RESKIN_ALL=1 also dresses vanilla boats in the spike adventure (as the TEST_SHIP type).
        static readonly bool ReskinAll = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_RESKIN_ALL") == "1";
        const string OverworldShipName = "SkySpike Airship";
        const string CombatShipPrefix = "SkySpike combat ship ";
        const string LegacyCombatHullName = "SkySpike airship combat hull";
        const string CombatEnemyPrefix = "SkySpike combat enemy ";

        // ---- registration ------------------------------------------------------------------------------

        static void RegisterShipTypes()
        {
            FTK_boatDB boats;
            FTK_boat template;
            FTK_items templateItem;
            try
            {
                boats = Content.Db<FTK_boatDB>();
                template = boats.GetEntry(FTK_boat.ID.boatA);
                templateItem = Content.Db<FTK_itemsDB>().GetEntry(FTK_itembase.ID.boatA);
                if (template == null || templateItem == null) throw new InvalidOperationException("boatA boat row or item row is missing.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[SkySpike] airship registration skipped; boats stay vanilla: " + e);
                return;
            }
            foreach (ShipType ship in Ships)
            {
                try { RegisterShip(ship, boats, template, templateItem); }
                catch (Exception e) { Plugin.Log.LogError("[SkySpike] airship " + ship.Key + " registration failed: " + e); }
            }
        }

        static void RegisterShip(ShipType ship, FTK_boatDB boats, FTK_boat template, FTK_items templateItem)
        {
            int gold = Math.Max(1, Mathf.RoundToInt(templateItem._goldValue * ship.GoldScale));
            Content.AddItem(Plugin.Guid, ship.ItemId, FTK_itembase.ID.boatA, ship.Name, row => { row._goldValue = gold; });
            int item;
            if (!ContentRegistry.TryGetSyntheticId(ship.ItemId, out item, typeof(FTK_itemsDB)))
                throw new InvalidOperationException("Item id did not register.");

            int health = Math.Max(1, template.m_BoatHealth + ship.HealthDelta);
            int armor = Math.Max(0, template.m_BoatArmor + ship.ArmorDelta);
            int baseSlots = Math.Max(1, template.m_BaseSlotRoll + ship.BaseSlotDelta);
            int terrainSlots = Math.Max(0, template.m_TerrainSlotRoll + ship.TerrainSlotDelta);
            // CopyFields shares boatA's m_BoatPrefab and m_BrokenAsset references; neither asset is edited.
            ContentRegistry.Register(boats, Plugin.Guid, ship.RowId, template, o =>
            {
                FTK_boat row = (FTK_boat)o;
                row.m_AssociatedItem = (FTK_itembase.ID)item;
                row.m_DisplayName = ship.Name;
                row.m_Top = ship.Tagline;
                row.m_Bottom = "Health " + health + ", armor " + armor;
                row.m_BoatHealth = health;
                row.m_BoatArmor = armor;
                row.m_BaseSlotRoll = baseSlots;
                row.m_TerrainSlotRoll = terrainSlots;
            });
            int boatRow;
            if (!ContentRegistry.TryGetSyntheticId(ship.RowId, out boatRow, typeof(FTK_boatDB)))
                throw new InvalidOperationException("Boat row id did not register.");

            ship.Item = item;
            ship.Row = boatRow;
            ShipsByItem[item] = ship;
            Plugin.Log.LogInfo("[SkySpike] airship boat registered: " + ship.Key + " '" + ship.Name + "' item " + item +
                ", boat row " + boatRow + " (health " + health + ", armor " + armor + ", slots " + baseSlots + "/" +
                terrainSlots + ", gold " + gold + "; boatA health " + template.m_BoatHealth + ", armor " + template.m_BoatArmor +
                ", slots " + template.m_BaseSlotRoll + "/" + template.m_TerrainSlotRoll + ", gold " + templateItem._goldValue + ").");
        }

        internal static ShipType ShipForItem(FTK_itembase.ID item)
        {
            ShipType ship;
            return ShipsByItem.TryGetValue((int)item, out ship) ? ship : null;
        }

        internal static bool IsAirshipItem(FTK_itembase.ID item)
        {
            return ShipsByItem.ContainsKey((int)item);
        }

        static ShipType ShipByKey(string key)
        {
            foreach (ShipType ship in Ships)
                if (string.Equals(ship.Key, key, StringComparison.OrdinalIgnoreCase)) return ship;
            return null;
        }

        static int RegisteredShipCount()
        {
            return ShipsByItem.Count;
        }

        internal static string DescriptionFor(FTK_itembase.ID item)
        {
            ShipType ship = ShipForItem(item);
            return ship != null ? ship.Description : null;
        }

        // ---- models --------------------------------------------------------------------------------------

        sealed class ShipPart { public string Name; public Mesh Mesh; public Material Material; public Vector3 Pivot; }

        sealed class ShipModel
        {
            public string Source;
            public bool Generated;
            public readonly List<ShipPart> Parts = new List<ShipPart>();
            public readonly List<Material> Materials = new List<Material>();
            public Bounds Bounds;
            public int Vertices;
            public float DeckFraction = -1f;
            public string DeckMethod;
            public SkySpikeAssetContract Contract;
        }

        sealed class ModelEntry { public ShipModel Model; public bool Exists; public DateTime Stamp, ContractStamp; }

        static readonly Dictionary<string, ModelEntry> _models = new Dictionary<string, ModelEntry>(StringComparer.Ordinal);
        static ShipModel _skyharbor;
        static bool _skyharborFailed;

        static string AirshipDir
        {
            get
            {
                string plugins = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                return Path.Combine(Path.Combine(Path.Combine(plugins, "FTKModFramework_content"), "skyspike"), "airships");
            }
        }

        // Reloads a key's GLB when the file appears or its timestamp changes, so models can be swapped between
        // map loads without a restart. Replaced models are left alive: boats built earlier still reference them.
        static ShipModel ModelFor(ShipType ship)
        {
            return ModelFor(ship.Key, SkyharborModel);
        }

        // key is the GLB file stem (a player type key or enemy_<model>); fallback supplies the stand-in model.
        static ShipModel ModelFor(string key, Func<ShipModel> fallback)
        {
            string path = Path.Combine(AirshipDir, key + ".glb");
            bool exists = File.Exists(path);
            DateTime stamp = exists ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            string contractPath = Path.Combine(AirshipDir, key + ".ship.json");
            bool authored = File.Exists(contractPath);
            DateTime contractStamp = authored ? File.GetLastWriteTimeUtc(contractPath) : DateTime.MinValue;
            ModelEntry entry;
            if (_models.TryGetValue(key, out entry) && entry.Exists == exists && entry.Stamp == stamp && entry.ContractStamp == contractStamp)
                return entry.Model;

            ShipModel model = null;
            if (exists)
            {
                SkySpikeGlb.Model glb = null;
                try
                {
                    Shader standard = Shader.Find("Standard");
                    if (standard == null) throw new InvalidOperationException("Standard shader unavailable.");
                    SkySpikeAssetContract contract = authored ? SkySpikeAssetContract.Parse(File.ReadAllText(contractPath)) : null;
                    glb = SkySpikeGlb.Load(path, standard);
                    model = new ShipModel { Source = key + ".glb", Generated = true, Bounds = glb.Bounds, Vertices = glb.Vertices, Contract = contract };
                    foreach (SkySpikeGlb.Part part in glb.Parts)
                        model.Parts.Add(new ShipPart { Name = part.Name, Mesh = part.Mesh, Material = part.Material, Pivot = Vector3.zero });
                    model.Materials.AddRange(glb.Materials);
                    if (contract != null)
                    {
                        contract.ValidateBounds(model.Bounds.min.x, model.Bounds.max.x, model.Bounds.min.y, model.Bounds.max.y,
                            model.Bounds.min.z, model.Bounds.max.z);
                        contract.ValidateUpperworkNodes(glb.SourceNodes, glb.AmbiguousNodes);
                        contract.ValidateRotorNodes(glb.SourceNodes, glb.AmbiguousNodes);
                        foreach (string node in contract.Upperworks)
                        {
                            bool found = false;
                            foreach (ShipPart part in model.Parts)
                                if (SkySpikeAssetContract.MatchesNode(part.Name, node)) found = true;
                            if (!found) throw new InvalidDataException("Declared upperwork node missing: " + node);
                        }
                        foreach (SkySpikeAssetContract.Rotor rotor in contract.Rotors)
                        {
                            bool found = false;
                            foreach (ShipPart part in model.Parts)
                                if (SkySpikeAssetContract.MatchesNode(part.Name, rotor.Node)) found = true;
                            if (!found) throw new InvalidDataException("Declared rotor has no rendered parts: " + rotor.Node);
                        }
                        model.DeckFraction = (contract.DeckY - model.Bounds.min.y) / Mathf.Max(model.Bounds.size.y, 1e-4f);
                        model.DeckMethod = "authored ship contract";
                    }
                    else EstimateDeck(model);
                    Plugin.Log.LogInfo("[SkySpike] airship " + key + " model loaded from " + path + ": " + glb.Parts.Count +
                        " meshes from " + glb.Primitives + " primitives, " + glb.Vertices + " vertices, " + glb.Triangles +
                        " triangles, " + glb.Textures + " textures, bounds center " + Fmt(glb.Bounds.center) + " size " +
                        Fmt(glb.Bounds.size) + ", deck fraction " + F2(model.DeckFraction) + " (" + model.DeckMethod + ")" +
                        (glb.Notes.Count > 0 ? "; notes: " + string.Join("; ", glb.Notes.ToArray()) : "") + ".");
                }
                catch (Exception e)
                {
                    if (glb != null) glb.Dispose();
                    model = null;
                    Plugin.Log.LogWarning("[SkySpike] airship " + key + " model " + path + " failed (" + e.Message + "); " +
                        (authored ? "retaining vanilla presentation." : "using a stand-in."));
                }
            }
            else
            {
                Plugin.Log.LogInfo("[SkySpike] airship " + key + " model " + path + " missing; " +
                    (authored ? "retaining vanilla presentation." : "using a stand-in."));
            }
            if (model == null && !authored)
            {
                model = fallback != null ? fallback() : null;
                if (model != null) Plugin.Log.LogInfo("[SkySpike] airship " + key + " stand-in: " + model.Source + ".");
            }
            _models[key] = new ModelEntry { Model = model, Exists = exists, Stamp = stamp, ContractStamp = contractStamp };
            return model;
        }

        // Loads only the Skyharbor airship and propeller parts from the embedded diorama, once per session.
        static ShipModel SkyharborModel()
        {
            if (_skyharbor != null) return _skyharbor;
            if (_skyharborFailed) return null;
            List<Object> created = new List<Object>();
            try
            {
                SceneData data;
                using (Stream resource = SkyharborBackground.OpenResource("diorama.json.gz"))
                using (GZipStream gzip = new GZipStream(resource, CompressionMode.Decompress))
                    data = JsonConvert.DeserializeObject<SceneData>(Encoding.UTF8.GetString(SkyharborBackground.ReadBounded(gzip, 64000000)));
                if (data == null || data.meshes == null) throw new InvalidDataException("Skyharbor scene missing meshes.");
                Shader standard = Shader.Find("Standard");
                if (standard == null) throw new InvalidOperationException("Standard shader unavailable.");

                ShipModel model = new ShipModel { Source = "Skyharbor airship" };
                Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
                bool hasBounds = false;
                foreach (MeshData part in data.meshes)
                {
                    if (part == null || (part.motion != "airship" && part.motion != "propeller")) continue;
                    Vector3[] vertices = SkyharborBackground.Vectors(part.vertices);
                    if (vertices.Length < 3 || vertices.Length > 65000 || part.triangles == null || part.triangles.Length % 3 != 0)
                        throw new InvalidDataException("Airship part '" + part.name + "' has an invalid layout.");
                    foreach (int index in part.triangles)
                        if (index < 0 || index >= vertices.Length) throw new InvalidDataException("Triangle index out of range.");
                    foreach (Vector3 vertex in vertices)
                    {
                        if (!hasBounds) { model.Bounds = new Bounds(vertex, Vector3.zero); hasBounds = true; }
                        else model.Bounds.Encapsulate(vertex);
                    }
                    Vector3 pivot = part.pivot == null ? Vector3.zero : SkyharborBackground.Vector(part.pivot);
                    for (int i = 0; i < vertices.Length; i++) vertices[i] -= pivot;

                    Mesh mesh = new Mesh();
                    created.Add(mesh);
                    mesh.name = "SkySpike " + part.name;
                    mesh.vertices = vertices;
                    mesh.triangles = part.triangles;
                    Vector3[] normals = part.normals != null ? SkyharborBackground.Vectors(part.normals) : null;
                    if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
                    else mesh.RecalculateNormals();
                    if (part.uv != null && part.uv.Length == vertices.Length * 2)
                    {
                        Vector2[] uv = new Vector2[vertices.Length];
                        for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(part.uv[2 * i], part.uv[2 * i + 1]);
                        mesh.uv = uv;
                    }
                    mesh.RecalculateBounds();

                    Material material = new Material(standard);
                    created.Add(material);
                    material.name = "SkySpike " + part.name;
                    material.color = part.color != null && part.color.Length == 3
                        ? new Color(part.color[0], part.color[1], part.color[2], 1f) : Color.white;
                    material.SetFloat("_Glossiness", 0.1f);
                    if (!string.IsNullOrEmpty(part.texture) && part.uv != null)
                    {
                        Texture2D texture;
                        if (!textures.TryGetValue(part.texture, out texture))
                        {
                            byte[] bytes;
                            using (Stream resource = SkyharborBackground.OpenResource(part.texture))
                                bytes = SkyharborBackground.ReadBounded(resource, 16000000);
                            texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                            created.Add(texture);
                            if (!texture.LoadImage(bytes)) throw new InvalidDataException("Airship texture decode failed.");
                            textures.Add(part.texture, texture);
                        }
                        material.mainTexture = texture;
                    }
                    model.Parts.Add(new ShipPart { Name = part.name ?? "Airship part", Mesh = mesh, Material = material, Pivot = pivot });
                    model.Materials.Add(material);
                    model.Vertices += vertices.Length;
                }
                if (model.Parts.Count == 0 || !hasBounds) throw new InvalidDataException("No airship parts in the Skyharbor scene.");
                EstimateDeck(model);
                _skyharbor = model;
                Plugin.Log.LogInfo("[SkySpike] Skyharbor airship loaded: " + model.Parts.Count + " parts, " + model.Vertices +
                    " vertices, bounds center " + Fmt(model.Bounds.center) + " size " + Fmt(model.Bounds.size) +
                    ", deck fraction " + F2(model.DeckFraction) + " (" + model.DeckMethod + ").");
                return model;
            }
            catch (Exception e)
            {
                _skyharborFailed = true;
                foreach (Object owned in created) if (owned != null) Object.Destroy(owned);
                Plugin.Log.LogError("[SkySpike] Skyharbor airship unavailable; airships without a GLB stay vanilla: " + e);
                return null;
            }
        }

        // Deck height heuristic, checked against the generated clipper: scan up from the keel for the first sharp
        // drop in horizontal extent (the top of the hull, where rigging or struts begin below the envelope), then
        // take the densest vertex band in the eight slices under it (a flat deck is a dense horizontal band).
        // Falls back to 0.35 of the height. Always overridable with combatDeckFraction.
        static void EstimateDeck(ShipModel model)
        {
            const int slices = 64;
            int[] counts = new int[slices];
            float[] minX = new float[slices], maxX = new float[slices], minZ = new float[slices], maxZ = new float[slices];
            for (int s = 0; s < slices; s++) { minX[s] = minZ[s] = float.MaxValue; maxX[s] = maxZ[s] = float.MinValue; }
            float min = model.Bounds.min.y, height = Mathf.Max(model.Bounds.size.y, 1e-4f);
            foreach (ShipPart part in model.Parts)
                foreach (Vector3 local in part.Mesh.vertices)
                {
                    Vector3 v = local + part.Pivot;
                    int s = Mathf.Clamp((int)((v.y - min) / height * slices), 0, slices - 1);
                    counts[s]++;
                    minX[s] = Mathf.Min(minX[s], v.x); maxX[s] = Mathf.Max(maxX[s], v.x);
                    minZ[s] = Mathf.Min(minZ[s], v.z); maxZ[s] = Mathf.Max(maxZ[s], v.z);
                }
            float[] extent = new float[slices];
            float largest = 0f;
            for (int s = 0; s < slices; s++)
            {
                extent[s] = counts[s] > 0 ? Mathf.Max(maxX[s] - minX[s], maxZ[s] - minZ[s]) : 0f;
                largest = Mathf.Max(largest, extent[s]);
            }
            float running = 0f;
            int peak = 0, top = -1;
            for (int s = 0; s < slices; s++)
            {
                if (extent[s] > running) { running = extent[s]; peak = s; }
                if (running >= 0.2f * largest && s > peak && (counts[s] == 0 || extent[s] < 0.75f * running)) { top = s; break; }
            }
            if (top > 0 && top < (int)(slices * 0.8f))
            {
                int from = Mathf.Max(peak, top - 8), densest = 0;
                for (int s = from; s < top; s++) densest = Mathf.Max(densest, counts[s]);
                int deck = from;
                for (int s = from; s < top; s++) if (counts[s] >= 0.8f * densest) deck = s;
                model.DeckFraction = (deck + 0.5f) / slices;
                model.DeckMethod = "auto: hull top at slice " + top + "/" + slices + ", deck band " + deck;
            }
            else
            {
                model.DeckFraction = 0.35f;
                model.DeckMethod = "auto fallback 0.35 (no hull shoulder found)";
            }
        }

        // ---- tuning (airships.json beside the GLBs) ------------------------------------------------------

        sealed class ShipTuning
        {
            public float OverworldScale, OverworldLift, OverworldYaw, CombatScale, CombatYaw;
            public Vector3 CombatOffset;
            public float CombatDeckFraction = -1f, Metallic = -1f, Smoothness = -1f, CombatLength = -1f;
            public bool SeeThrough = true;
            public float SeeThroughClearance = 0.06f, SeeThroughAlpha = 0.2f, SeeThroughHeadHeight = 1.2f;
            // "max" or "centroid" (see SplitFor).
            public string SeeThroughSplit = "max";
            // Hull-band sizing cut above the deck; independent of the see-through cut so fade tuning never resizes.
            public float HullBandClearance = 0.12f;
            // Hull-band sizing. LongAxis null picks the longer hull axis; a max span <= 0 disables that clamp;
            // HullFlare <= 0 keeps every hull-band slice (no overhang exclusion).
            public string LongAxis;
            public float OverworldMaxSpan = 1.6f, CombatMaxSpan = 2.2f, HullFlare = 1.5f;
            // Combat placement: "arena" (default, both crews on one host ship's deck; see SkySpikeArena), "broadside"
            // (two ships rail to rail across the crews' midline) or "legacy" (each ship centered under its crew).
            public string CombatPlacement = "arena";
            public float DeckMargin = 1f, CombatGap = 0f;
            // Arena: free deck beyond the outermost slots, maximum beam widening, and deck plane offset from the slots.
            public float ArenaMargin = 2.5f, ArenaMaxStretch = 1.6f, ArenaDeckLift = 0f;
            // Arena see-through: cut just above the deck so rails and props fade as whole pieces, in a grid of
            // chunks (model x, y, z) so only the chunks between the camera and a combatant fade.
            public float ArenaSeeThroughClearance = 0.01f;
            public int[] SeeThroughCells = { 1, 1, 1 }, ArenaSeeThroughCells = { 6, 3, 6 };
            // Multiplies every material's base color (combat and overworld share materials, so tint per model key).
            public Color Tint = Color.white;
            public string Source;
        }

        static JObject _tuning;
        static DateTime _tuningStamp = DateTime.MinValue;
        static bool _tuningExists;
        static readonly HashSet<string> _loggedTuning = new HashSet<string>();

        static string TuningPath { get { return Path.Combine(AirshipDir, "airships.json"); } }

        // Cheap timestamp check; the file is re-parsed only when it changes (each map load and encounter asks).
        static void RefreshTuning()
        {
            try
            {
                string path = TuningPath;
                bool exists = File.Exists(path);
                DateTime stamp = exists ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                if (exists == _tuningExists && stamp == _tuningStamp) return;
                _tuningExists = exists;
                _tuningStamp = stamp;
                _loggedTuning.Clear();
                _loggedSizing.Clear();
                if (!exists)
                {
                    _tuning = null;
                    Plugin.Log.LogInfo("[SkySpike] airship tuning " + path + " not found; using defaults.");
                    return;
                }
                _tuning = JObject.Parse(File.ReadAllText(path));
                List<string> keys = new List<string>();
                foreach (JProperty p in _tuning.Properties()) keys.Add(p.Name);
                Plugin.Log.LogInfo("[SkySpike] airship tuning loaded from " + path + ": keys [" + string.Join(", ", keys.ToArray()) + "].");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[SkySpike] airship tuning unreadable (" + e.Message + "); keeping the previous values.");
            }
        }

        static ShipTuning TuningFor(ShipType ship, ShipModel model)
        {
            return TuningFor(ship.Key, model);
        }

        static ShipTuning TuningFor(string key, ShipModel model)
        {
            ShipTuning t = new ShipTuning
            {
                OverworldScale = EnvFloat("FTK_SKY_SPIKE_AIRSHIP_SCALE", 1f),
                OverworldLift = EnvFloat("FTK_SKY_SPIKE_AIRSHIP_LIFT", 0.08f),
                // The Skyharbor mesh's bow points to -Z; glTF models face +Z after conversion.
                OverworldYaw = EnvFloat("FTK_SKY_SPIKE_AIRSHIP_YAW", model.Generated ? 0f : 180f),
                CombatScale = 1f, CombatYaw = 0f, CombatOffset = Vector3.zero, Source = "defaults",
                SeeThrough = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_SEE_THROUGH") != "0"
            };
            if (_tuning != null)
            {
                Apply(t, _tuning["*"] as JObject, "*");
                Apply(t, _tuning[key] as JObject, key);
            }
            string logKey = key + "|" + model.Source;
            if (_loggedTuning.Add(logKey))
                Plugin.Log.LogInfo("[SkySpike] airship tuning " + key + " (" + t.Source + ", model " + model.Source + "): overworldScale " +
                    F2(t.OverworldScale) + ", overworldLift " + F2(t.OverworldLift) + ", overworldYaw " + F2(t.OverworldYaw) +
                    ", combatScale " + F2(t.CombatScale) + ", combatOffset " + Fmt(t.CombatOffset) + ", combatYaw " + F2(t.CombatYaw) +
                    ", combatDeckFraction " + (t.CombatDeckFraction >= 0f ? F2(t.CombatDeckFraction) : "auto " + F2(model.DeckFraction)) +
                    ", metallic " + (t.Metallic >= 0f ? F2(t.Metallic) : "auto") + ", smoothness " + (t.Smoothness >= 0f ? F2(t.Smoothness) : "auto") +
                    ", combatLength " + (t.CombatLength > 0f ? F2(t.CombatLength) : "auto") + ", seeThrough " + t.SeeThrough +
                    ", seeThroughClearance " + F2(t.SeeThroughClearance) + ", seeThroughSplit " + t.SeeThroughSplit +
                    ", hullBandClearance " + F2(t.HullBandClearance) + ", seeThroughAlpha " + F2(t.SeeThroughAlpha) +
                    ", seeThroughHeadHeight " + F2(t.SeeThroughHeadHeight) + ", longAxis " + (t.LongAxis ?? "auto") +
                    ", overworldMaxSpan " + (t.OverworldMaxSpan > 0f ? F2(t.OverworldMaxSpan) : "off") +
                    ", combatMaxSpan " + (t.CombatMaxSpan > 0f ? F2(t.CombatMaxSpan) : "off") +
                    ", hullFlare " + (t.HullFlare > 0f ? F2(t.HullFlare) : "off") + ", combatPlacement " + t.CombatPlacement +
                    ", deckMargin " + F2(t.DeckMargin) + ", combatGap " + F2(t.CombatGap) + ", arenaMargin " + F2(t.ArenaMargin) +
                    ", arenaMaxStretch " + F2(t.ArenaMaxStretch) + ", arenaDeckLift " + F2(t.ArenaDeckLift) + ".");
            return t;
        }

        static void Apply(ShipTuning t, JObject o, string label)
        {
            if (o == null) return;
            t.Source = t.Source == "defaults" ? "airships.json[" + label + "]" : t.Source + "+[" + label + "]";
            t.OverworldScale = Num(o, "overworldScale", t.OverworldScale);
            t.OverworldLift = Num(o, "overworldLift", t.OverworldLift);
            t.OverworldYaw = Num(o, "overworldYaw", t.OverworldYaw);
            t.CombatScale = Num(o, "combatScale", t.CombatScale);
            t.CombatYaw = Num(o, "combatYaw", t.CombatYaw);
            t.CombatDeckFraction = Num(o, "combatDeckFraction", t.CombatDeckFraction);
            t.Metallic = Num(o, "metallic", t.Metallic);
            t.Smoothness = Num(o, "smoothness", t.Smoothness);
            t.CombatLength = Num(o, "combatLength", t.CombatLength);
            t.SeeThroughClearance = Mathf.Clamp(Num(o, "seeThroughClearance", t.SeeThroughClearance), 0f, 1f);
            t.HullBandClearance = Mathf.Clamp(Num(o, "hullBandClearance", t.HullBandClearance), 0f, 1f);
            JToken split = o["seeThroughSplit"];
            if (split != null && split.Type == JTokenType.String)
            {
                string m = ((string)split).Trim().ToLowerInvariant();
                if (m == "max" || m == "centroid") t.SeeThroughSplit = m;
            }
            t.SeeThroughAlpha = Mathf.Clamp01(Num(o, "seeThroughAlpha", t.SeeThroughAlpha));
            t.SeeThroughHeadHeight = Num(o, "seeThroughHeadHeight", t.SeeThroughHeadHeight);
            t.OverworldMaxSpan = Num(o, "overworldMaxSpan", t.OverworldMaxSpan);
            t.CombatMaxSpan = Num(o, "combatMaxSpan", t.CombatMaxSpan);
            // Below 1 the rule would drop the median slice itself; any positive value is floored to 1.
            float flare = Num(o, "hullFlare", t.HullFlare);
            t.HullFlare = flare > 0f ? Mathf.Max(flare, 1f) : 0f;
            JToken axis = o["longAxis"];
            if (axis != null && axis.Type == JTokenType.String)
            {
                string a = ((string)axis).Trim().ToLowerInvariant();
                if (a == "x" || a == "z") t.LongAxis = a;
                else if (a == "auto") t.LongAxis = null;
            }
            t.DeckMargin = Mathf.Max(Num(o, "deckMargin", t.DeckMargin), 0f);
            t.CombatGap = Num(o, "combatGap", t.CombatGap);
            t.ArenaMargin = Mathf.Max(Num(o, "arenaMargin", t.ArenaMargin), 0f);
            t.ArenaMaxStretch = Mathf.Max(Num(o, "arenaMaxStretch", t.ArenaMaxStretch), 1f);
            t.ArenaDeckLift = Num(o, "arenaDeckLift", t.ArenaDeckLift);
            t.ArenaSeeThroughClearance = Mathf.Clamp(Num(o, "arenaSeeThroughClearance", t.ArenaSeeThroughClearance), 0f, 1f);
            t.SeeThroughCells = Cells(o["seeThroughCells"], t.SeeThroughCells);
            t.ArenaSeeThroughCells = Cells(o["arenaSeeThroughCells"], t.ArenaSeeThroughCells);
            JArray tint = o["tint"] as JArray;
            if (tint != null && tint.Count == 3)
                t.Tint = new Color(Mathf.Clamp((float)tint[0], 0f, 2f), Mathf.Clamp((float)tint[1], 0f, 2f), Mathf.Clamp((float)tint[2], 0f, 2f), 1f);
            JToken placement = o["combatPlacement"];
            if (placement != null && placement.Type == JTokenType.String)
            {
                string m = ((string)placement).Trim().ToLowerInvariant();
                if (m == "arena" || m == "broadside" || m == "legacy") t.CombatPlacement = m;
            }
            JToken see = o["seeThrough"];
            if (see != null && see.Type == JTokenType.Boolean) t.SeeThrough = (bool)see;
            JArray offset = o["combatOffset"] as JArray;
            if (offset != null && offset.Count == 3)
                t.CombatOffset = new Vector3((float)offset[0], (float)offset[1], (float)offset[2]);
        }

        static int[] Cells(JToken token, int[] fallback)
        {
            JArray a = token as JArray;
            if (a == null || a.Count != 3) return fallback;
            int[] cells = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (a[i].Type != JTokenType.Integer && a[i].Type != JTokenType.Float) return fallback;
                cells[i] = Mathf.Clamp((int)(float)a[i], 1, 16);
            }
            return cells;
        }

        static float Num(JObject o, string name, float fallback)
        {
            JToken v = o[name];
            if (v == null || (v.Type != JTokenType.Float && v.Type != JTokenType.Integer)) return fallback;
            float f = (float)v;
            return float.IsNaN(f) || float.IsInfinity(f) ? fallback : f;
        }

        static readonly Dictionary<Material, Color> _baseColors = new Dictionary<Material, Color>();

        static void ApplySurface(ShipModel model, ShipTuning t)
        {
            foreach (Material m in model.Materials)
            {
                if (m == null) continue;
                Color baseColor;
                if (!_baseColors.TryGetValue(m, out baseColor)) { baseColor = m.color; _baseColors[m] = baseColor; }
                m.color = new Color(baseColor.r * t.Tint.r, baseColor.g * t.Tint.g, baseColor.b * t.Tint.b, baseColor.a);
                if (t.Metallic >= 0f) m.SetFloat("_Metallic", Mathf.Clamp01(t.Metallic));
                if (t.Smoothness >= 0f) m.SetFloat("_Glossiness", Mathf.Clamp01(t.Smoothness));
            }
        }

        // ---- hull-band sizing ----------------------------------------------------------------------------

        // Model-space horizontal hull measurements from the vertices at or below HullThreshold (deck +
        // hullBandClearance). Wings and planks at deck height are inside that band, so SkySpikeHullMath drops
        // the slices they widen; envelopes, masts and sails lie above it and overhang freely.
        sealed class HullBand
        {
            public float Threshold, Flare, MinX, MaxX, MinZ, MaxZ, MedianX, MedianZ;
            public int Vertices, ExcludedX, ExcludedZ;
            public string Fallback;
        }

        // Per placement: the band plus this key's axis choice.
        sealed class HullFrame
        {
            public HullBand Band;
            public bool LongZ;
            public string AxisSource;
            public float Length, Beam, FullSpan, AxisYaw;
            public Vector3 Center;
        }

        const int HullSlices = 16;
        static readonly Dictionary<ShipModel, List<HullBand>> _hullBands = new Dictionary<ShipModel, List<HullBand>>();
        static readonly HashSet<string> _loggedSizing = new HashSet<string>();

        // Cached per model, threshold and flare; the overworld and combat of one key share an entry.
        static HullBand HullBandFor(ShipModel model, ShipTuning tune)
        {
            if (model.Contract != null)
            {
                SkySpikeAssetContract c = model.Contract;
                return new HullBand { Threshold = c.DeckY, MinX = c.Hull.MinX, MaxX = c.Hull.MaxX,
                    MinZ = c.Hull.MinZ, MaxZ = c.Hull.MaxZ };
            }
            float threshold = HullThreshold(model, tune);
            List<HullBand> list;
            if (!_hullBands.TryGetValue(model, out list)) { list = new List<HullBand>(); _hullBands[model] = list; }
            foreach (HullBand cached in list)
                if (Mathf.Abs(cached.Threshold - threshold) < 1e-4f && Mathf.Abs(cached.Flare - tune.HullFlare) < 1e-4f) return cached;
            if (list.Count >= 8) list.Clear(); // only repeated live retuning grows this
            HullBand band = MeasureHullBand(model, threshold, tune.HullFlare);
            list.Add(band);
            Plugin.Log.LogInfo("[SkySpike] hull band " + model.Source + ": vertices at y <= " + F2(threshold) + " (deck " +
                F2(DeckFractionFor(model, tune)) + " + hullBandClearance " + F2(tune.HullBandClearance) + " of height " + F2(model.Bounds.size.y) +
                "): " + band.Vertices + " vertices in " + HullSlices + " slices, hull x [" + F2(band.MinX) + ", " + F2(band.MaxX) +
                "] z [" + F2(band.MinZ) + ", " + F2(band.MaxZ) + "], median slice width x " + F2(band.MedianX) + " z " + F2(band.MedianZ) +
                ", overhang slices dropped (wider than hullFlare " + (band.Flare > 0f ? F2(band.Flare) : "off") + " x median) x " +
                band.ExcludedX + " z " + band.ExcludedZ + ", model bounds x " + F2(model.Bounds.size.x) + " z " + F2(model.Bounds.size.z) +
                (band.Fallback != null ? "; FALLBACK to full model bounds: " + band.Fallback : "") + ".");
            return band;
        }

        static HullBand MeasureHullBand(ShipModel model, float threshold, float flare)
        {
            HullBand band = new HullBand { Threshold = threshold, Flare = flare };
            float bottom = model.Bounds.min.y;
            float height = Mathf.Max(threshold - bottom, 1e-4f);
            int[] counts = new int[HullSlices];
            float[] minX = new float[HullSlices], maxX = new float[HullSlices], minZ = new float[HullSlices], maxZ = new float[HullSlices];
            for (int s = 0; s < HullSlices; s++) { minX[s] = minZ[s] = float.MaxValue; maxX[s] = maxZ[s] = float.MinValue; }
            foreach (ShipPart part in model.Parts)
                foreach (Vector3 local in part.Mesh.vertices)
                {
                    Vector3 v = local + part.Pivot;
                    if (v.y > threshold) continue;
                    int s = Mathf.Clamp((int)((v.y - bottom) / height * HullSlices), 0, HullSlices - 1);
                    counts[s]++;
                    band.Vertices++;
                    if (v.x < minX[s]) minX[s] = v.x;
                    if (v.x > maxX[s]) maxX[s] = v.x;
                    if (v.z < minZ[s]) minZ[s] = v.z;
                    if (v.z > maxZ[s]) maxZ[s] = v.z;
                }
            bool okX = SkySpikeHullMath.KeepRange(counts, minX, maxX, flare, out band.MinX, out band.MaxX, out band.MedianX, out band.ExcludedX);
            bool okZ = SkySpikeHullMath.KeepRange(counts, minZ, maxZ, flare, out band.MinZ, out band.MaxZ, out band.MedianZ, out band.ExcludedZ);
            float fullSpan = Mathf.Max(model.Bounds.size.x, model.Bounds.size.z);
            if (band.Vertices < 3 || !okX || !okZ) band.Fallback = band.Vertices + " vertices in the band";
            else if (Mathf.Max(band.MaxX - band.MinX, band.MaxZ - band.MinZ) < 0.05f * fullSpan)
                band.Fallback = "hull band under 5% of the model span";
            if (band.Fallback != null)
            {
                band.MinX = model.Bounds.min.x; band.MaxX = model.Bounds.max.x;
                band.MinZ = model.Bounds.min.z; band.MaxZ = model.Bounds.max.z;
                band.ExcludedX = band.ExcludedZ = 0;
            }
            return band;
        }

        // Forward axis: the longer hull axis unless airships.json sets "longAxis". A model whose hull runs
        // along x turns -90 degrees so its +x lies along the placement forward.
        static HullFrame FrameFor(ShipModel model, ShipTuning tune)
        {
            HullBand band = HullBandFor(model, tune);
            float sx = band.MaxX - band.MinX, sz = band.MaxZ - band.MinZ;
            HullFrame f = new HullFrame { Band = band };
            if (model.Contract != null) { f.LongZ = model.Contract.LongAxis == "z"; f.AxisSource = "authored ship contract"; }
            else if (tune.LongAxis != null) { f.LongZ = tune.LongAxis == "z"; f.AxisSource = "longAxis override"; }
            else { f.LongZ = sz >= sx; f.AxisSource = "longer hull axis"; }
            f.AxisYaw = f.LongZ ? 0f : -90f;
            f.Length = Mathf.Max(f.LongZ ? sz : sx, 0.01f);
            f.Beam = Mathf.Max(f.LongZ ? sx : sz, 0.01f);
            f.FullSpan = Mathf.Max(Mathf.Max(model.Bounds.size.x, model.Bounds.size.z), 0.01f);
            f.Center = new Vector3((band.MinX + band.MaxX) * 0.5f, (model.Bounds.min.y + band.Threshold) * 0.5f, (band.MinZ + band.MaxZ) * 0.5f);
            return f;
        }

        // One line per context, key, model and deciding rule; cleared when airships.json reloads.
        static void LogSizing(string context, string key, ShipModel model, HullFrame f, string rule, float worldScale, string detail)
        {
            if (!_loggedSizing.Add(context + "|" + key + "|" + model.Source + "|" + F2(f.Band.Threshold) + "|" + rule)) return;
            Plugin.Log.LogInfo("[SkySpike] airship sizing " + context + " " + key + " (" + model.Source + "): hull band y <= " +
                F2(f.Band.Threshold) + ", L_hull " + F2(f.Length) + " B_hull " + F2(f.Beam) + ", full span " + F2(f.FullSpan) +
                " (model units), axis " + (f.LongZ ? "z" : "x") + " (" + f.AxisSource + "), rule: " + rule + " -> world hull length " +
                F2(f.Length * worldScale) + ", beam " + F2(f.Beam * worldScale) + ", full span " + F2(f.FullSpan * worldScale) +
                (detail != null ? "; " + detail : "") + ".");
        }

        // ---- overworld ---------------------------------------------------------------------------------

        internal static void OnBoatArtInstantiated(MiniHexBoat boat)
        {
            if (boat == null) return;
            // Airship art belongs to the airship boat types. Vanilla boats keep vanilla art unless the reskin-all
            // override is set, and then only inside the spike adventure.
            ShipType ship = ShipForItem(boat.m_BoatItemID);
            if (ship == null && !(ReskinAll && SpikeActive())) return;
            ShipType visual = ship ?? ShipByKey(string.IsNullOrEmpty(TestShipKey) ? "clipper" : TestShipKey) ?? Ships[0];
            GameObject art = boat.m_AttachedObject;
            if (art == null || art.transform.Find(OverworldShipName) != null) return;
            RefreshTuning();
            ShipModel model = ModelFor(visual);
            if (model == null) return; // keep the vanilla boat visible
            if (AttachOverworld(art, visual.Key, model, TuningFor(visual.Key, model)) && ship != null)
                Plugin.Log.LogInfo("[SkySpike] airship boat art applied (item " + ship.Item + ", " + ship.Key + ").");
        }

        // Hides the vanilla boat renderers under art and attaches the model centered on the boat root, sized to the
        // boat's footprint. Shared by player airships and enemy airship camps.
        static bool AttachOverworld(GameObject art, string key, ShipModel model, ShipTuning tune)
        {
            if (art == null || art.transform.Find(OverworldShipName) != null) return false;
            Renderer[] original = art.GetComponentsInChildren<Renderer>(true);
            bool[] visible = new bool[original.Length];
            for (int i = 0; i < original.Length; i++) visible[i] = original[i] != null && original[i].enabled;
            try { return AttachOverworldPrepared(art, key, model, tune); }
            catch (Exception e)
            {
                Transform failed = art.transform.Find(OverworldShipName);
                if (failed != null) { failed.gameObject.SetActive(false); Object.Destroy(failed.gameObject); }
                for (int i = 0; i < original.Length; i++) if (original[i] != null) original[i].enabled = visible[i];
                Plugin.Log.LogWarning("[SkySpike] overworld ship placement failed; restored vanilla presentation: " + e.Message);
                return false;
            }
        }

        static bool AttachOverworldPrepared(GameObject art, string key, ShipModel model, ShipTuning tune)
        {
            ApplySurface(model, tune);

            // Measure the boat's footprint before hiding it. Particle bounds follow emitted FX, not the hull.
            Renderer[] boatRenderers = art.GetComponentsInChildren<Renderer>(true);
            bool measured = false;
            Bounds boatBounds = new Bounds(art.transform.position, Vector3.zero);
            foreach (Renderer renderer in boatRenderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer || !renderer.enabled) continue;
                if (!measured) { boatBounds = renderer.bounds; measured = true; }
                else boatBounds.Encapsulate(renderer.bounds);
            }
            float footprint = measured ? Mathf.Max(boatBounds.size.x, boatBounds.size.z) : 0f;
            if (footprint < 0.1f || footprint > 50f) footprint = 2f;

            // The hull length fits the footprint, so wings and envelopes overhang; the full span is then held to
            // footprint x overworldMaxSpan.
            HullFrame frame = FrameFor(model, tune);
            float worldScale = footprint * tune.OverworldScale / frame.Length;
            string rule = "L_hull fit to footprint x overworldScale " + F2(tune.OverworldScale);
            if (tune.OverworldMaxSpan > 0f && frame.FullSpan * worldScale > footprint * tune.OverworldMaxSpan)
            {
                worldScale = footprint * tune.OverworldMaxSpan / frame.FullSpan;
                rule = "full span clamped to footprint x overworldMaxSpan " + F2(tune.OverworldMaxSpan);
            }
            if (!SkySpikeAssetContract.Finite(worldScale) || worldScale <= 0f)
                throw new InvalidOperationException("Invalid overworld ship scale.");

            List<Transform> parts;
            GameObject root = BuildShipObject(OverworldShipName, model, art.layer, art.transform, out parts);
            float parentScale = model.Contract != null ? AuthoredParentScale(art.transform) : Mathf.Abs(art.transform.lossyScale.x);
            if (parentScale < 0.0001f) parentScale = 1f;
            float localScale = worldScale / parentScale;
            root.transform.localScale = Vector3.one * localScale;
            root.transform.localRotation = Quaternion.Euler(0f, tune.OverworldYaw + frame.AxisYaw, 0f);
            root.transform.localPosition = Vector3.zero;

            // Center the HULL on the boat ROOT (the pivot the boat turns about) after scale and yaw, so lopsided
            // overhang does not shift it; keep the keel just above the root (the camera tilt reads height as up).
            Bounds shipBounds = WorldBounds(model, parts);
            Vector3 hullCenter = root.transform.TransformPoint(frame.Center);
            Vector3 pivot = art.transform.position;
            Vector3 offset = new Vector3(pivot.x - hullCenter.x, pivot.y + footprint * tune.OverworldLift - shipBounds.min.y,
                pivot.z - hullCenter.z);
            root.transform.position += offset;
            if (!SkySpikeAssetContract.Finite(shipBounds.size.x) || !SkySpikeAssetContract.Finite(shipBounds.size.y) ||
                !SkySpikeAssetContract.Finite(shipBounds.size.z) || shipBounds.size.sqrMagnitude < 0.0001f ||
                !SkySpikeAssetContract.Finite(root.transform.position.x) || !SkySpikeAssetContract.Finite(root.transform.position.y) ||
                !SkySpikeAssetContract.Finite(root.transform.position.z))
                throw new InvalidOperationException("Overworld ship has no usable geometry.");
            root.SetActive(true);
            LogSizing("overworld", key, model, frame, rule, worldScale, "footprint " + F2(footprint) + " (full span " +
                F2(frame.FullSpan * worldScale / footprint) + " x footprint)");
            Vector3 boatBoundsOffset = measured ? boatBounds.center - pivot : Vector3.zero;

            // Disable, never destroy: BoatPrefab keeps its Animator, FX and sail renderer for native callers.
            int hidden = 0;
            foreach (Renderer renderer in boatRenderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                renderer.enabled = false;
                hidden++;
            }

            Plugin.Log.LogInfo("[SkySpike] airship " + key + " attached (" + model.Source + "): " + model.Parts.Count + " meshes, " +
                model.Vertices + " vertices, footprint " + F2(footprint) + ", scale " + localScale.ToString("0.0000", CultureInfo.InvariantCulture) +
                " (" + rule + "), hull length " + F2(frame.Length * worldScale) + ", full span " + F2(frame.FullSpan * worldScale) +
                ", yaw " + F2(tune.OverworldYaw) + (frame.AxisYaw != 0f ? " + axis " + F2(frame.AxisYaw) : "") + ", lift " +
                F2(tune.OverworldLift) + ", world size " + Fmt(shipBounds.size) + ", hid " + hidden + " boat renderers.");
            Plugin.Log.LogInfo("[SkySpike] airship hull center offset applied " + Fmt(offset) + " (world), boat renderer bounds center was " +
                Fmt(boatBoundsOffset) + " from the boat root.");
            return true;
        }

        static GameObject BuildShipObject(string name, ShipModel model, int layer, Transform parent, out List<Transform> parts)
        {
            GameObject root = new GameObject(name);
            root.SetActive(false);
            root.layer = layer;
            root.transform.SetParent(parent, false);
            parts = new List<Transform>();
            foreach (ShipPart part in model.Parts)
            {
                GameObject child = new GameObject(part.Name);
                child.layer = layer;
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = part.Pivot;
                child.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = part.Material;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                parts.Add(child.transform);
            }
            try { SkySpikeRotors.Attach(root, model.Contract); }
            catch { Object.Destroy(root); throw; }
            return root;
        }

        // World AABB of the owned parts from mesh bounds and current matrices, so no renderer bounds refresh is assumed.
        static Bounds WorldBounds(ShipModel model, List<Transform> parts)
        {
            bool any = false;
            Bounds result = new Bounds();
            for (int p = 0; p < parts.Count && p < model.Parts.Count; p++)
                Encapsulate(ref result, ref any, model.Parts[p].Mesh.bounds, parts[p].localToWorldMatrix);
            return result;
        }

        static void Encapsulate(ref Bounds result, ref bool any, Bounds local, Matrix4x4 m)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z);
                Vector3 world = m.MultiplyPoint3x4(corner);
                if (!any) { result = new Bounds(world, Vector3.zero); any = true; }
                else result.Encapsulate(world);
            }
        }

        static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.000", CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.000", CultureInfo.InvariantCulture) +
                ", " + v.z.ToString("0.000", CultureInfo.InvariantCulture) + ")";
        }

        static string F2(float v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // ---- sea combat ----------------------------------------------------------------------------------

        sealed class Placement
        {
            public Vector3 Target;
            public float Spread, TargetLength, WorldScale, DeckFraction, KeelY = float.NaN;
            public HullFrame Frame;
            public string LengthBasis, Mode = "legacy";
            // Broadside only (world units).
            public float Beam, MinBeam, CrewOffset, InnerEdge, MeasuredInner, CrewInnermost, CrewOutermost;
            public bool BeamRaised;
        }

        // One encounter's shared geometry, identical for the player and the enemy ship.
        sealed class CombatFrame
        {
            public Vector3 Along, ToEnemy, Midpoint, PartyCentroid, FoeCentroid;
            public bool HasBoth;
            public float HullExtent, ReferenceLength, Separation;
        }

        // Sea combat shows one m_BoatGeos hull per FTK_boat.ID; an airship row has no entry, so vanilla has
        // already hidden every hull. Put the ship's own model under the party: an owned object beside
        // m_BoatGeos[boatA] (never inserted into the dictionary), with its deck at the party's standing height.
        // In the spike adventure an enemy airship likewise goes under the enemy targets of a boat-camp fight.
        internal static void OnBoatDioramaInit(DioramaBoat diorama)
        {
            SkySpikeCamera.Clear();
            if (diorama == null || diorama.m_BoatGeos == null) return;
            bool spike = SpikeActive();
            if (spike)
            {
                try { ProbeDiorama(diorama); }
                catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] probe failed: " + e.Message); }
            }
            RestoreHiddenOthers();
            Transform source;
            if (!diorama.m_BoatGeos.TryGetValue(FTK_boat.ID.boatA, out source) || source == null) return;
            Transform parent = source.parent;
            EncounterSession session = EncounterSession.Instance;
            MiniHexBoat boat = session != null ? session.m_Boat : null;
            ShipType ship = boat != null ? ShipForItem(boat.m_BoatItemID) : null;

            // Every encounter starts from vanilla: hide all spike combat objects, then show what applies.
            if (parent != null)
                foreach (Transform child in parent)
                    if (child.name == LegacyCombatHullName || child.name.StartsWith(CombatShipPrefix, StringComparison.Ordinal) ||
                        child.name.StartsWith(CombatEnemyPrefix, StringComparison.Ordinal))
                        child.gameObject.SetActive(false);

            // Reference frame: the vanilla hull's mesh bounds (inactive-safe) and the party-to-enemy direction.
            bool hasHull = false;
            Bounds hull = new Bounds();
            foreach (MeshFilter mf in source.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) Encapsulate(ref hull, ref hasHull, mf.sharedMesh.bounds, mf.transform.localToWorldMatrix);
            foreach (SkinnedMeshRenderer smr in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) Encapsulate(ref hull, ref hasHull, smr.sharedMesh.bounds, smr.transform.localToWorldMatrix);
            float hullExtent = hasHull ? Mathf.Max(hull.size.x, hull.size.z) : 0f;
            Vector3 align = diorama.m_AlignmentDir;
            align.y = 0f;
            if (align.sqrMagnitude < 1e-6f) { align = source.forward; align.y = 0f; }
            if (align.sqrMagnitude < 1e-6f) align = Vector3.forward;
            // Broadside: both long axes run perpendicular to the party-to-enemy direction, so the ships lie alongside.
            Vector3 along = Vector3.Cross(Vector3.up, align.normalized);

            List<Vector3> party = TargetPositions(diorama.m_PlayerTargets);
            List<Vector3> foes = TargetPositions(diorama.m_EnemyTargets);
            CombatFrame frame = new CombatFrame
            {
                Along = along, HullExtent = hullExtent,
                ReferenceLength = Mathf.Max(Mathf.Max(hullExtent, Spread(party) * 1.6f), 1f),
                HasBoth = party.Count > 0 && foes.Count > 0
            };
            // The midline is the plane through the crews' midpoint, normal to the alignment axis; ToEnemy is that
            // axis signed from the party toward the enemies.
            frame.ToEnemy = align.normalized;
            if (frame.HasBoth)
            {
                frame.PartyCentroid = Centroid(party);
                frame.FoeCentroid = Centroid(foes);
                Vector3 gap = frame.FoeCentroid - frame.PartyCentroid;
                gap.y = 0f;
                frame.Separation = gap.magnitude;
                if (Vector3.Dot(gap, frame.ToEnemy) < 0f) frame.ToEnemy = -frame.ToEnemy;
                frame.Midpoint = (frame.PartyCentroid + frame.FoeCentroid) * 0.5f;
            }

            // Ships first, each fail-safe on its own, so the cloud floor can drop below the lowest keel.
            float keel = float.NaN;
            bool arena = false;
            if (spike)
            {
                RefreshTuning();
                ShipTuning global = new ShipTuning();
                if (_tuning != null) Apply(global, _tuning["*"] as JObject, "*");
                arena = global.CombatPlacement == "arena";
            }
            if (arena)
            {
                try
                {
                    keel = PlaceArena(diorama, parent, source.gameObject.layer, frame, ship, party, foes);
                    if (SkySpikeAssetContract.Finite(keel))
                        foreach (Transform geo in diorama.m_BoatGeos.Values)
                            if (geo != null) geo.gameObject.SetActive(false);
                }
                catch (Exception e) { Plugin.Log.LogError("[SkySpike] arena combat failed: " + e); }
                if (!SkySpikeAssetContract.Finite(keel)) RestoreCombatFallback(diorama, parent, source);
            }
            else if (ship != null)
            {
                try
                {
                    RefreshTuning();
                    ShipModel model = ModelFor(ship);
                    if (model == null) RestoreCombatFallback(diorama, parent, source);
                    else
                    {
                        ShipTuning tune = TuningFor(ship, model);
                        Vector3 fallback;
                        string deckSource;
                        if (party.Count > 0) { fallback = Vector3.zero; deckSource = party.Count + " party targets"; }
                        else if (hasHull) { fallback = new Vector3(hull.center.x, hull.center.y + hull.extents.y * 0.5f, hull.center.z); deckSource = "hull bounds (no party targets)"; }
                        else { fallback = source.position; deckSource = "hull transform (no targets, no mesh)"; }
                        GameObject go = CombatObject(CombatShipPrefix + ship.Key, model, tune, source.gameObject.layer, parent, diorama);
                        Placement placed = PlaceCombatShip(go, model, tune, party, fallback, frame, false, parent, ship.Key);
                        if (!SkySpikeAssetContract.Finite(placed.KeelY)) throw new InvalidOperationException("Combat ship has no usable geometry.");
                        foreach (Transform geo in diorama.m_BoatGeos.Values)
                            if (geo != null) geo.gameObject.SetActive(false);
                        keel = LowerKeel(keel, placed.KeelY);
                        Plugin.Log.LogInfo("[SkySpike] airship combat ship active: " + ship.Key + " (" + model.Source + ") deck at " +
                            Fmt(placed.Target) + " from " + deckSource + ", party spread " + F2(placed.Spread) + ", vanilla hull extent " +
                            F2(hullExtent) + (hasHull ? " (hull bounds center " + Fmt(hull.center) + " size " + Fmt(hull.size) + ")" : "") +
                            ", " + PlacementText(placed, model, tune) + ".");
                    }
                }
                catch (Exception e)
                {
                    RestoreCombatFallback(diorama, parent, source);
                    Plugin.Log.LogError("[SkySpike] airship combat ship failed: " + e);
                }
            }

            if (spike && !arena)
            {
                try { keel = LowerKeel(keel, PlaceEnemyShip(diorama, parent, source.gameObject.layer, frame)); }
                catch (Exception e) { Plugin.Log.LogError("[SkySpike] enemy combat ship failed: " + e); }
            }

            try
            {
                if (spike && SkySpikeAssetContract.Finite(keel)) ApplyCloudFloor(diorama, keel);
                else RestoreCloudFloor(diorama);
            }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] combat cloud floor failed: " + e); }
        }

        // Native exit calls CleanUpDiorama without ResetDioramaAssets. Restore pooled scene state at both
        // boundaries, independently of which adventure or camera is currently active.
        internal static void CleanupCombatPresentation(DioramaBoat diorama)
        {
            if (diorama == null) return;
            bool cameraRestored = false;
            int hiddenShips = 0, restoredScenery = 0;
            try { cameraRestored = SkySpikeCamera.ClearFor(diorama); }
            catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] arena camera cleanup failed: " + e.Message); }
            try { RestoreCloudFloor(diorama); }
            catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] arena floor cleanup failed: " + e.Message); }
            for (int i = _hiddenOthers.Count - 1; i >= 0; i--)
            {
                Renderer renderer = _hiddenOthers[i];
                if (renderer == null) { _hiddenOthers.RemoveAt(i); continue; }
                if (!renderer.transform.IsChildOf(diorama.transform)) continue;
                try
                {
                    renderer.enabled = true;
                    _hiddenOthers.RemoveAt(i);
                    restoredScenery++;
                }
                catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] arena scenery cleanup failed: " + e.Message); }
            }
            foreach (SkySpikeSeeThrough ship in diorama.GetComponentsInChildren<SkySpikeSeeThrough>(true))
            {
                if (ship == null || !ship.gameObject.activeSelf ||
                    (!ship.name.StartsWith(CombatShipPrefix, StringComparison.Ordinal) &&
                     !ship.name.StartsWith(CombatEnemyPrefix, StringComparison.Ordinal))) continue;
                try { ship.gameObject.SetActive(false); hiddenShips++; }
                catch (Exception e) { Plugin.Log.LogWarning("[SkySpike] arena ship cleanup failed: " + e.Message); }
            }
            if (cameraRestored || hiddenShips > 0 || restoredScenery > 0)
                Plugin.Log.LogInfo("[SkySpike] arena lifecycle restored on '" + diorama.name + "': camera " + cameraRestored +
                    ", " + hiddenShips + " owned ships hidden, " + restoredScenery + " scenery flags restored.");
        }

        static void RestoreCombatFallback(DioramaBoat diorama, Transform parent, Transform source)
        {
            SkySpikeCamera.Clear();
            if (parent != null)
                foreach (Transform child in parent)
                    if (child.name.StartsWith(CombatShipPrefix, StringComparison.Ordinal) ||
                        child.name.StartsWith(CombatEnemyPrefix, StringComparison.Ordinal)) child.gameObject.SetActive(false);
            RestoreHiddenOthers();
            bool visible = false;
            foreach (Transform geo in diorama.m_BoatGeos.Values)
                if (geo != null && geo.gameObject.activeSelf) visible = true;
            // Native _initForEncounter selects geos by the boat row ID. Synthetic rows have no native entry.
            if (!visible) source.gameObject.SetActive(true);
            Plugin.Log.LogWarning("[SkySpike] custom staging unavailable; using the native combat deck.");
        }

        static float LowerKeel(float current, float candidate)
        {
            if (float.IsNaN(candidate)) return current;
            return float.IsNaN(current) ? candidate : Mathf.Min(current, candidate);
        }

        static float AuthoredParentScale(Transform parent)
        {
            if (parent == null) return 1f;
            Vector3 scale = parent.lossyScale;
            if (!SkySpikeAssetContract.Finite(scale.x) || !SkySpikeAssetContract.Finite(scale.y) || !SkySpikeAssetContract.Finite(scale.z) ||
                scale.x <= 0.0001f || Mathf.Abs(scale.y - scale.x) > scale.x * 0.001f || Mathf.Abs(scale.z - scale.x) > scale.x * 0.001f)
                throw new InvalidOperationException("Authored airships require a uniform positive parent scale.");
            return scale.x;
        }

        // Lowest world point of a placed combat ship, from mesh bounds and current matrices.
        static float KeelOf(GameObject go)
        {
            bool any = false;
            Bounds world = new Bounds();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) Encapsulate(ref world, ref any, mf.sharedMesh.bounds, mf.transform.localToWorldMatrix);
            return any ? world.min.y : float.NaN;
        }

        // Combat ships are built from the see-through split of the model (hull below the deck clearance, upper
        // above) and carry SkySpikeSeeThrough, which fades the upper parts when they hide a combatant.
        static GameObject CombatObject(string name, ShipModel model, ShipTuning tune, int layer, Transform parent, Diorama diorama)
        {
            SplitModel split = SplitFor(model, tune);
            Transform existing = parent != null ? parent.Find(name) : null;
            SkySpikeSeeThrough current = existing != null ? existing.GetComponent<SkySpikeSeeThrough>() : null;
            GameObject go;
            if (current != null && current.Split == split) go = existing.gameObject;
            else
            {
                if (existing != null) Object.Destroy(existing.gameObject); // built from a replaced model or split
                go = BuildSplitObject(name, split, layer, parent);
            }
            SkySpikeSeeThrough fader = go.GetComponent<SkySpikeSeeThrough>();
            fader.Configure(diorama, tune.SeeThrough, tune.SeeThroughAlpha, tune.SeeThroughHeadHeight);
            return go;
        }

        static Vector3 Centroid(List<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 v in points) sum += v;
            return points.Count > 0 ? sum / points.Count : sum;
        }

        static List<Vector3> TargetPositions(List<Transform> targets)
        {
            List<Vector3> result = new List<Vector3>();
            if (targets != null) foreach (Transform t in targets) if (t != null) result.Add(t.position);
            return result;
        }

        static float Spread(List<Vector3> points)
        {
            float spread = 0f;
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                {
                    Vector3 d = points[i] - points[j];
                    d.y = 0f;
                    spread = Mathf.Max(spread, d.magnitude);
                }
            return spread;
        }

        // Broadside needs both crews to find the midline; without them, or with combatPlacement "legacy", each ship
        // is centered under its own crew as before.
        static Placement PlaceCombatShip(GameObject go, ShipModel model, ShipTuning tune, List<Vector3> stand, Vector3 fallback,
            CombatFrame c, bool enemySide, Transform parent, string key)
        {
            if (tune.CombatPlacement != "legacy" && c.HasBoth && stand.Count > 0 && c.Separation > 0.01f)
                return PlaceBroadside(go, model, tune, stand, c, enemySide, parent, key);
            Placement p = PlaceOnTargets(go, model, tune, stand, fallback, c.Along, c.ReferenceLength, c.Separation, parent, key);
            if (tune.CombatPlacement != "legacy") p.Mode = "legacy (broadside needs both crews)";
            return p;
        }

        // Two ships lashed rail to rail: each hull's long axis runs parallel to the midline between the crews, and its
        // INNER hull-band edge sits at midline + combatGap / 2 on its own crew's side, so the two decks meet at the
        // midline and mirror each other across it. Length is max(vanilla hull extent, crew spread x 2.5) x
        // combatScale (or combatLength); beam follows the hull aspect and is raised, aspect kept, until the deck
        // reaches past the crew by deckMargin. Wings and envelopes overhang; the see-through fade handles occlusion.
        static Placement PlaceBroadside(GameObject go, ShipModel model, ShipTuning tune, List<Vector3> stand, CombatFrame c,
            bool enemySide, Transform parent, string key)
        {
            Placement p = new Placement { Mode = "broadside" };
            Vector3 crew = Centroid(stand);
            p.Target = crew;
            p.Spread = Spread(stand);
            HullFrame f = FrameFor(model, tune);
            p.Frame = f;
            Vector3 outward = enemySide ? c.ToEnemy : -c.ToEnemy; // from the midline toward this ship's crew
            Vector3 rel = crew - c.Midpoint;
            rel.y = 0f;
            p.CrewOffset = Vector3.Dot(rel, outward);
            float alongCrew = Vector3.Dot(rel, c.Along);
            p.CrewInnermost = float.MaxValue;
            p.CrewOutermost = float.MinValue;
            foreach (Vector3 v in stand)
            {
                Vector3 d = v - c.Midpoint;
                d.y = 0f;
                float o = Vector3.Dot(d, outward);
                p.CrewInnermost = Mathf.Min(p.CrewInnermost, o);
                p.CrewOutermost = Mathf.Max(p.CrewOutermost, o);
            }

            float length;
            if (tune.CombatLength > 0f) { length = tune.CombatLength; p.LengthBasis = "combatLength override"; }
            else
            {
                float crewLength = p.Spread * 2.5f;
                length = Mathf.Max(Mathf.Max(c.HullExtent, crewLength), 1f) * tune.CombatScale;
                p.LengthBasis = (c.HullExtent >= crewLength ? "vanilla hull extent " + F2(c.HullExtent) : "crew spread " + F2(p.Spread) + " x 2.5") +
                    " x combatScale " + F2(tune.CombatScale);
            }
            float beam = length * f.Beam / f.Length;
            // The crew only has to stand ON the deck: its line (crewOffset from the midline, so crewOffset - gap / 2
            // from the inner rail) must be at least deckMargin inside the outer rail.
            p.InnerEdge = tune.CombatGap * 0.5f;
            p.MinBeam = Mathf.Max(p.CrewOffset - p.InnerEdge, 0f) + tune.DeckMargin;
            if (beam < p.MinBeam)
            {
                length *= p.MinBeam / beam;
                beam = p.MinBeam;
                p.BeamRaised = true;
                p.LengthBasis += ", beam raised to crewOffset - gap/2 + deckMargin " + F2(tune.DeckMargin) + " (aspect kept)";
            }
            p.TargetLength = length;
            p.Beam = beam;
            p.WorldScale = length / f.Length;
            p.DeckFraction = DeckFractionFor(model, tune);

            Vector3 hullCenter = c.Midpoint + outward * (p.InnerEdge + beam * 0.5f) + c.Along * alongCrew;
            hullCenter.y = crew.y;
            Quaternion rotation = Quaternion.LookRotation(c.Along, Vector3.up) * Quaternion.Euler(0f, f.AxisYaw + tune.CombatYaw, 0f);
            float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
            if (parentScale < 0.0001f) parentScale = 1f;
            float deckY = model.Bounds.min.y + p.DeckFraction * model.Bounds.size.y;
            Vector3 anchor = new Vector3(f.Center.x, deckY, f.Center.z);
            go.transform.rotation = rotation;
            go.transform.localScale = Vector3.one * (p.WorldScale / parentScale);
            go.transform.position = hullCenter + rotation * tune.CombatOffset - rotation * (anchor * p.WorldScale);
            go.SetActive(true);

            // Measure the placed hull band's beam-axis edges back in world space, so a yaw or axis mistake shows up.
            Vector3 edgeA = f.LongZ ? new Vector3(f.Band.MinX, deckY, f.Center.z) : new Vector3(f.Center.x, deckY, f.Band.MinZ);
            Vector3 edgeB = f.LongZ ? new Vector3(f.Band.MaxX, deckY, f.Center.z) : new Vector3(f.Center.x, deckY, f.Band.MaxZ);
            Vector3 wa = go.transform.TransformPoint(edgeA) - c.Midpoint, wb = go.transform.TransformPoint(edgeB) - c.Midpoint;
            wa.y = 0f;
            wb.y = 0f;
            p.MeasuredInner = Mathf.Min(Vector3.Dot(wa, outward), Vector3.Dot(wb, outward));
            p.KeelY = KeelOf(go);

            LogSizing("combat broadside", key, model, f, p.LengthBasis, p.WorldScale, "separation " + F2(c.Separation) + ", crewOffset " +
                F2(p.CrewOffset) + ", crew spread " + F2(p.Spread));
            Plugin.Log.LogInfo("[SkySpike] broadside " + (enemySide ? "enemy" : "player") + " " + key + ": mode broadside, L " + F2(p.TargetLength) +
                ", B " + F2(p.Beam) + " (B_hull/L_hull " + F2(f.Beam / f.Length) + "), crewOffset " + F2(p.CrewOffset) + ", inner edge " +
                F2(p.InnerEdge) + " from midline (measured " + F2(p.MeasuredInner) + "), outer edge " + F2(p.MeasuredInner + p.Beam) +
                ", crew outward " + F2(p.CrewInnermost) + " to " + F2(p.CrewOutermost) + ", beam " +
                (p.BeamRaised ? "RAISED to fit crew (min " + F2(p.MinBeam) + ")" : "not raised (min " + F2(p.MinBeam) + ")") +
                ", along offset " + F2(alongCrew) + ", keel y " + F2(p.KeelY) + ".");
            return p;
        }

        // LEGACY: deck (model-local height deckFraction) at the hull-band center goes to the targets' centroid at their
        // mean height; the hull's forward axis runs along `along`; TargetLength is the world hull length (L_hull).
        static Placement PlaceOnTargets(GameObject go, ShipModel model, ShipTuning tune, List<Vector3> stand, Vector3 fallback,
            Vector3 along, float referenceLength, float separation, Transform parent, string key)
        {
            Placement p = new Placement();
            if (stand.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (Vector3 v in stand) sum += v;
                p.Target = sum / stand.Count;
            }
            else p.Target = fallback;
            p.Spread = Spread(stand);
            HullFrame f = FrameFor(model, tune);
            p.Frame = f;
            Quaternion rotation = Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(0f, f.AxisYaw + tune.CombatYaw, 0f);
            // Natural hull length covers the vanilla hull and the standing spread; when the other side's targets are
            // known, the HULL beam is kept within 0.45 of the gap between the two groups so the hulls cannot overlap.
            float natural = Mathf.Max(referenceLength, p.Spread * 1.6f);
            p.LengthBasis = "reference/spread";
            if (separation > 0.01f)
            {
                float beamLimited = 0.45f * separation * f.Length / f.Beam;
                if (beamLimited < natural) { natural = beamLimited; p.LengthBasis = "B_hull <= 0.45 x separation " + F2(separation); }
            }
            if (tune.CombatLength > 0f) { p.TargetLength = tune.CombatLength; p.LengthBasis = "combatLength override"; }
            else
            {
                p.TargetLength = natural * tune.CombatScale;
                p.LengthBasis += " x combatScale " + F2(tune.CombatScale);
                // Overhang (wings, envelopes) is free up to combatMaxSpan x separation, so it cannot reach far into the
                // other ship. An explicit combatLength is taken as is.
                if (separation > 0.01f && tune.CombatMaxSpan > 0f)
                {
                    float maxSpan = tune.CombatMaxSpan * separation;
                    if (f.FullSpan * p.TargetLength / f.Length > maxSpan)
                    {
                        p.TargetLength = maxSpan * f.Length / f.FullSpan;
                        p.LengthBasis += ", full span clamped to combatMaxSpan " + F2(tune.CombatMaxSpan) + " x separation " + F2(separation);
                    }
                }
            }
            p.WorldScale = p.TargetLength / f.Length;
            float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
            if (parentScale < 0.0001f) parentScale = 1f;
            p.DeckFraction = DeckFractionFor(model, tune);
            Vector3 anchor = new Vector3(f.Center.x, model.Bounds.min.y + p.DeckFraction * model.Bounds.size.y, f.Center.z);
            LogSizing("combat", key, model, f, p.LengthBasis, p.WorldScale, "separation " + F2(separation) + ", party/enemy spread " + F2(p.Spread) +
                ", reference length " + F2(referenceLength));
            go.transform.rotation = rotation;
            go.transform.localScale = Vector3.one * (p.WorldScale / parentScale);
            go.transform.position = p.Target + rotation * tune.CombatOffset - rotation * (anchor * p.WorldScale);
            go.SetActive(true);
            p.KeelY = KeelOf(go);
            return p;
        }

        static string PlacementText(Placement p, ShipModel model, ShipTuning tune)
        {
            HullFrame f = p.Frame;
            return "mode " + p.Mode + ", hull axis " + (f.LongZ ? "z" : "x") + " (" + f.AxisSource + ") L_hull " + F2(f.Length) + " B_hull " + F2(f.Beam) +
                " full span " + F2(f.FullSpan) + ", target hull length " + F2(p.TargetLength) + " (" + p.LengthBasis + ")" +
                ", world beam " + F2(f.Beam * p.WorldScale) + ", world full span " + F2(f.FullSpan * p.WorldScale) +
                ", world scale " + p.WorldScale.ToString("0.0000", CultureInfo.InvariantCulture) + ", deck fraction " + F2(p.DeckFraction) +
                (tune.CombatDeckFraction >= 0f ? " (override)" : " (" + model.DeckMethod + ")") + ", yaw " + F2(tune.CombatYaw) +
                ", offset " + Fmt(tune.CombatOffset) + ", ship world size " + Fmt(model.Bounds.size * p.WorldScale);
        }

        // Last resort when no model can be built: stand the party on an owned clone of boatA's hull.
        static void ActivateRaftFallback(Transform source, Transform parent)
        {
            Transform existing = parent != null ? parent.Find(LegacyCombatHullName) : null;
            GameObject hull = existing != null ? existing.gameObject : null;
            if (hull == null)
            {
                hull = Object.Instantiate(source.gameObject, parent);
                hull.name = LegacyCombatHullName;
                hull.transform.localPosition = source.localPosition;
                hull.transform.localRotation = source.localRotation;
                hull.transform.localScale = source.localScale;
            }
            hull.SetActive(true);
            Plugin.Log.LogInfo("[SkySpike] airship combat hull active (fallback clone of boatA geometry under '" +
                (parent != null ? parent.name : "<root>") + "').");
        }
    }

    // The item card reads FTKItem.GetDescription, which looks up "STR_" + the item enum; for a synthetic id that is
    // a number with no text row. The framework has no item-description hook yet, so the spike supplies one.
    [HarmonyPatch(typeof(FTKItemName.FTKItem), "GetDescription")]
    internal static class SkySpikeItemDescriptionPatch
    {
        static bool Prepare() { return SkySpike.Enabled; }

        static void Postfix(FTK_itembase.ID ___m_ItemID, ref string __result)
        {
            try
            {
                string description = SkySpike.DescriptionFor(___m_ItemID);
                if (description != null) __result = description;
            }
            catch (Exception e) { Plugin.Log.LogError("[SkySpike] item description failed: " + e); }
        }
    }
}
