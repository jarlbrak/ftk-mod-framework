using System;
using System.Collections.Generic;
using UnityEngine;

namespace FTKModFramework.Core
{
    // Arena combat: one ship hosts the whole fight. Both crews stand on its main deck, whose measured plane sits
    // exactly at the targets' standing height, so every character's feet are on the planks. The host is the enemy
    // ship when the party boards a boat camp and the party's own ship otherwise (an ambush at sea).
    internal static partial class SkySpike
    {
        // Model-space main deck: the plane holding the most upward-facing triangle area in the lower hull, and the
        // central extent of that area along both horizontal axes.
        sealed class ArenaDeck
        {
            public float Y, MinX, MaxX, MinZ, MaxZ, Share;
            public string Note;
        }

        static readonly Dictionary<ShipModel, ArenaDeck> _arenaDecks = new Dictionary<ShipModel, ArenaDeck>();

        static ArenaDeck ArenaDeckFor(ShipModel model)
        {
            if (model.Contract != null)
            {
                SkySpikeAssetContract c = model.Contract;
                return new ArenaDeck { Y = c.DeckY, MinX = c.Deck.MinX, MaxX = c.Deck.MaxX,
                    MinZ = c.Deck.MinZ, MaxZ = c.Deck.MaxZ, Share = 1f, Note = "authored clear deck" };
            }
            ArenaDeck deck;
            if (_arenaDecks.TryGetValue(model, out deck)) return deck;
            deck = MeasureArenaDeck(model);
            _arenaDecks[model] = deck;
            Plugin.Log.LogInfo("[SkySpike] arena deck " + model.Source + ": plane y " + F2(deck.Y) + " (" +
                F2((deck.Y - model.Bounds.min.y) / Mathf.Max(model.Bounds.size.y, 1e-4f)) + " of height), flat area x [" + F2(deck.MinX) + ", " +
                F2(deck.MaxX) + "] z [" + F2(deck.MinZ) + ", " + F2(deck.MaxZ) + "], " + (deck.Share * 100f).ToString("0") +
                "% of lower up-facing area on the plane" + (deck.Note != null ? "; " + deck.Note : "") + ".");
            return deck;
        }

        static ArenaDeck MeasureArenaDeck(ShipModel model)
        {
            const int bins = 160;
            float bottom = model.Bounds.min.y, height = Mathf.Max(model.Bounds.size.y, 1e-4f);
            // Envelopes and cabin roofs face up too; the walkable deck is in the lower 60% of the model.
            float ceiling = bottom + 0.6f * height;
            float[] area = new float[bins];
            List<Vector4> faces = new List<Vector4>(); // centroid xyz, area
            foreach (ShipPart part in model.Parts)
            {
                Vector3[] v = part.Mesh.vertices;
                int[] t = part.Mesh.triangles;
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]] + part.Pivot, b = v[t[i + 1]] + part.Pivot, c = v[t[i + 2]] + part.Pivot;
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    float twice = cross.magnitude;
                    if (twice < 1e-9f || Mathf.Abs(cross.y) / twice < 0.9f) continue; // near-horizontal only (either winding)
                    Vector3 centroid = (a + b + c) / 3f;
                    if (centroid.y > ceiling) continue;
                    float s = twice * 0.5f;
                    area[Mathf.Clamp((int)((centroid.y - bottom) / height * bins), 0, bins - 1)] += s;
                    faces.Add(new Vector4(centroid.x, centroid.y, centroid.z, s));
                }
            }
            ArenaDeck deck = new ArenaDeck();
            float total = 0f;
            int peak = 0;
            for (int i = 0; i < bins; i++) { total += area[i]; if (area[i] > area[peak]) peak = i; }
            if (faces.Count == 0 || total <= 0f)
            {
                deck.Y = bottom + DeckFractionFor(model, new ShipTuning()) * height;
                deck.MinX = model.Bounds.min.x; deck.MaxX = model.Bounds.max.x;
                deck.MinZ = model.Bounds.min.z; deck.MaxZ = model.Bounds.max.z;
                deck.Note = "FALLBACK: no horizontal faces; deck fraction heuristic and full bounds";
                return deck;
            }
            // Refine inside the peak bin and its neighbours: area-weighted median height.
            float lo = bottom + (peak - 1) * height / bins, hi = bottom + (peak + 2) * height / bins;
            List<Vector4> plane = faces.FindAll(f => f.y >= lo && f.y <= hi);
            plane.Sort((p, q) => p.y.CompareTo(q.y));
            float planeArea = 0f;
            foreach (Vector4 f in plane) planeArea += f.w;
            float run = 0f;
            deck.Y = plane[plane.Count / 2].y;
            foreach (Vector4 f in plane) { run += f.w; if (run >= planeArea * 0.5f) { deck.Y = f.y; break; } }
            deck.Share = planeArea / total;
            // Area-weighted 3rd..97th percentile extents: rails and odd slivers on the plane do not widen it.
            deck.MinX = Percentile(plane, 0, 0.03f); deck.MaxX = Percentile(plane, 0, 0.97f);
            deck.MinZ = Percentile(plane, 2, 0.03f); deck.MaxZ = Percentile(plane, 2, 0.97f);
            return deck;
        }

        static float Percentile(List<Vector4> faces, int axis, float q)
        {
            List<Vector4> sorted = new List<Vector4>(faces);
            sorted.Sort((p, r) => p[axis].CompareTo(r[axis]));
            float total = 0f;
            foreach (Vector4 f in sorted) total += f.w;
            float run = 0f;
            foreach (Vector4 f in sorted) { run += f.w; if (run >= total * q) return f[axis]; }
            return sorted[sorted.Count - 1][axis];
        }

        // Arena models may ship as <key>_arena.glb (a wide flat-decked variant); otherwise the ship's own model.
        static ShipModel ArenaModelFor(string key, Func<ShipModel> fallback, out string source)
        {
            ShipModel primary = ModelFor(key, fallback);
            if (primary != null && primary.Contract != null)
            {
                source = primary.Contract.CombatModel ?? key;
                if (source == key) return primary;
                ShipModel variant = ModelFor(source, null);
                if (variant == null || variant.Contract == null)
                    throw new InvalidOperationException("Authored combat variant missing or without a ship contract: " + source);
                return variant;
            }
            if (primary == null && System.IO.File.Exists(System.IO.Path.Combine(AirshipDir, key + ".ship.json")))
            { source = key; return null; }
            string arenaKey = key + "_arena";
            if (System.IO.File.Exists(System.IO.Path.Combine(AirshipDir, arenaKey + ".glb")))
            {
                ShipModel arena = ModelFor(arenaKey, null);
                if (arena != null) { source = arenaKey; return arena; }
            }
            source = key;
            return primary;
        }

        // Hosts the fight on one ship. Returns its keel height, or NaN when nothing was placed.
        static float PlaceArena(DioramaBoat diorama, Transform parent, int layer, CombatFrame frame, ShipType partyShip,
            List<Vector3> party, List<Vector3> foes)
        {
            string how;
            FTKDict_EnemyBoats.EnemyBoats faction = EncounterEnemyBoat(out how);
            string host = ArenaHostOverride;
            if (host == null) host = faction != FTKDict_EnemyBoats.EnemyBoats.None ? "enemy" : "player";

            string key, label;
            ShipModel model;
            if (host == "enemy" && faction != FTKDict_EnemyBoats.EnemyBoats.None)
            {
                string enemyKey = "enemy_" + EnemyModelFor(faction.ToString());
                model = ArenaModelFor(enemyKey, DarkStandIn, out key);
                label = "enemy deck, faction " + faction + " (" + how + ")";
            }
            else if (partyShip != null)
            {
                model = ArenaModelFor(partyShip.Key, SkyharborModel, out key);
                label = "party deck, " + partyShip.Key + (faction == FTKDict_EnemyBoats.EnemyBoats.None ? " (no enemy boat: ambush)" : " (host override)");
            }
            else return float.NaN;
            if (model == null) return float.NaN;

            ShipTuning tune = TuningFor(key, model);
            ApplySurface(model, tune);
            ArenaDeck deck = ArenaDeckFor(model);
            // The see-through split must keep the floor itself solid: cut at the measured plane, not the heuristic.
            if (tune.CombatDeckFraction < 0f)
                tune.CombatDeckFraction = (deck.Y - model.Bounds.min.y) / Mathf.Max(model.Bounds.size.y, 1e-4f);
            tune.SeeThroughClearance = tune.ArenaSeeThroughClearance;
            tune.SeeThroughCells = tune.ArenaSeeThroughCells;

            List<Vector3> all = new List<Vector3>(party);
            all.AddRange(foes);
            if (all.Count == 0) return float.NaN;
            Vector3 toEnemy = frame.ToEnemy;
            Vector3 across = Vector3.Cross(Vector3.up, toEnemy);
            // Rectangle holding every slot (five a side in the Default layout), plus arenaMargin on each edge.
            float minA = float.MaxValue, maxA = float.MinValue, minC = float.MaxValue, maxC = float.MinValue, standY = 0f;
            Vector3 origin = all[0];
            foreach (Vector3 v in all)
            {
                Vector3 d = v - origin;
                float a = Vector3.Dot(d, toEnemy), c = Vector3.Dot(d, across);
                minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a);
                minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c);
                standY += v.y;
            }
            standY /= all.Count;
            float needLength = maxA - minA + 2f * tune.ArenaMargin, needBeam = maxC - minC + 2f * tune.ArenaMargin;
            Vector3 center = origin + toEnemy * ((minA + maxA) * 0.5f) + across * ((minC + maxC) * 0.5f);
            center.y = standY;

            HullFrame f = FrameFor(model, tune);
            float deckLength = Mathf.Max(f.LongZ ? deck.MaxZ - deck.MinZ : deck.MaxX - deck.MinX, 0.01f);
            float deckBeam = Mathf.Max(f.LongZ ? deck.MaxX - deck.MinX : deck.MaxZ - deck.MinZ, 0.01f);
            // Widen the beam (up to arenaMaxStretch) before scaling the whole ship, so the length stays sensible.
            float byLength = needLength / deckLength, byBeam = needBeam / deckBeam;
            float stretch = Mathf.Clamp(byBeam / byLength, 1f, Mathf.Max(tune.ArenaMaxStretch, 1f));
            float scale = Mathf.Max(byLength, byBeam / stretch) * tune.CombatScale;
            if (model.Contract != null)
            {
                stretch = 1f;
                scale = model.Contract.UniformScale(needLength, needBeam) * Mathf.Max(tune.CombatScale, 1f);
            }
            if (!SkySpikeAssetContract.Finite(scale) || scale <= 0f)
                throw new InvalidOperationException("Invalid arena scale.");

            float parentScale = model.Contract != null ? AuthoredParentScale(parent) : (parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f);
            if (parentScale < 0.0001f) parentScale = 1f;
            Vector3 local = f.LongZ ? new Vector3(scale * stretch, scale, scale) : new Vector3(scale, scale, scale * stretch);
            Quaternion rotation = Quaternion.LookRotation(toEnemy, Vector3.up) * Quaternion.Euler(0f, f.AxisYaw + tune.CombatYaw, 0f);
            Vector3 anchor = new Vector3((deck.MinX + deck.MaxX) * 0.5f, deck.Y, (deck.MinZ + deck.MaxZ) * 0.5f);

            GameObject go = CombatObject(CombatEnemyPrefix + "arena " + key, model, tune, layer, parent, diorama);
            go.transform.rotation = rotation;
            go.transform.localScale = local / parentScale;
            go.transform.position = center + Vector3.up * tune.ArenaDeckLift + rotation * tune.CombatOffset - rotation * Vector3.Scale(anchor, local);

            // Check back in world space: the deck plane height at the center and the worst slot's distance inside the deck.
            float planeY = go.transform.TransformPoint(anchor).y;
            float worstInside = float.MaxValue;
            Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
            foreach (Vector3 v in all)
            {
                Vector3 m = toLocal.MultiplyPoint3x4(v);
                float inside = Mathf.Min(Mathf.Min(m.x - deck.MinX, deck.MaxX - m.x) * local.x, Mathf.Min(m.z - deck.MinZ, deck.MaxZ - m.z) * local.z);
                worstInside = Mathf.Min(worstInside, inside);
            }
            float keel = KeelOf(go);
            if (!SkySpikeAssetContract.Finite(keel) || !SkySpikeAssetContract.Finite(worstInside) || worstInside < -0.05f ||
                !SkySpikeAssetContract.Finite(planeY) || Mathf.Abs(planeY - standY) > 0.15f)
                throw new InvalidOperationException("Arena placement does not support all native combat slots; retaining vanilla deck.");
            foreach (Vector3 v in all)
                if (Mathf.Abs(v.y - planeY) > 0.2f)
                    throw new InvalidOperationException("Native combat slots are not on one usable deck plane.");
            go.SetActive(true);
            Placement placed = new Placement { Target = center, Spread = Mathf.Max(needLength, needBeam), TargetLength = deckLength * scale };
            int hidden = HideEnemyScenery(diorama, placed);
            Plugin.Log.LogInfo("[SkySpike] arena combat: " + label + " -> " + key + " (" + model.Source + "), " + party.Count + " party + " +
                foes.Count + " enemy slots, need " + F2(needLength) + " x " + F2(needBeam) + " (margin " + F2(tune.ArenaMargin) + "), model deck " +
                F2(deckLength) + " x " + F2(deckBeam) + " (axis " + (f.LongZ ? "z" : "x") + "), scale " + F2(scale) + ", beam stretch " + F2(stretch) +
                " -> world deck " + F2(deckLength * scale) + " x " + F2(deckBeam * scale * stretch) + ", deck plane y " + F2(planeY) +
                " vs standing y " + F2(standY) + " (lift " + F2(tune.ArenaDeckLift) + "), worst slot " + F2(worstInside) +
                " inside the deck edge, world size " + Fmt(Vector3.Scale(model.Bounds.size, local)) + ", keel y " + F2(keel) +
                ", hid " + hidden + " layout renderers.");
            if (model.Contract != null)
            {
                try { SkySpikeCamera.Configure(diorama, go, center, toEnemy, model.Contract); }
                catch (Exception e) { SkySpikeCamera.Clear(); Plugin.Log.LogWarning("[SkySpike] authored camera unavailable: " + e.Message); }
            }
            return keel;
        }

        // FTK_SKY_SPIKE_ARENA_HOST=enemy|player forces the host ship (to preview the ambush deck on a boarding fight).
        static string ArenaHostOverride
        {
            get
            {
                string v = Environment.GetEnvironmentVariable("FTK_SKY_SPIKE_ARENA_HOST");
                return v == "enemy" || v == "player" ? v : null;
            }
        }
    }
}
