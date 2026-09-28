using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core
{
    // Authored measurements are in the loader's Unity coordinates, after the glTF X reflection.
    // A missing sidecar preserves legacy assets; an invalid sidecar must never become guessed geometry.
    internal sealed class SkySpikeAssetContract
    {
        internal sealed class Rectangle
        {
            internal float MinX, MaxX, MinZ, MaxZ;
            internal float Width { get { return MaxX - MinX; } }
            internal float Length { get { return MaxZ - MinZ; } }
            internal bool Contains(float x, float z, float tolerance)
            {
                return Finite(x) && Finite(z) && x >= MinX - tolerance && x <= MaxX + tolerance &&
                    z >= MinZ - tolerance && z <= MaxZ + tolerance;
            }
        }

        internal string LongAxis, CombatModel;
        internal float DeckY;
        internal Rectangle Deck, Hull;
        internal string[] Upperworks;
        internal sealed class Rotor
        {
            internal string Node;
            internal float[] Pivot, Axis;
            internal float DegreesPerSecond;
        }
        internal Rotor[] Rotors = new Rotor[0];

        internal static SkySpikeAssetContract Parse(string json)
        {
            JObject o = JObject.Parse(json);
            if (Number(o, "schemaVersion") != 1f) throw new InvalidDataException("Unsupported ship schemaVersion.");
            SkySpikeAssetContract c = new SkySpikeAssetContract();
            c.LongAxis = (string)o["longAxis"];
            if (c.LongAxis != "x" && c.LongAxis != "z") throw new InvalidDataException("Ship longAxis must be x or z.");
            c.Deck = ReadRectangle(o["deck"] as JObject);
            c.Hull = ReadRectangle(o["hull"] as JObject);
            c.DeckY = Number((JObject)o["deck"], "y");
            if (!c.Hull.Contains(c.Deck.MinX, c.Deck.MinZ, 0.001f) || !c.Hull.Contains(c.Deck.MaxX, c.Deck.MaxZ, 0.001f))
                throw new InvalidDataException("Usable deck must lie inside the authored hull.");
            c.CombatModel = (string)o["combatModel"];
            if (c.CombatModel != null && !SafeKey(c.CombatModel)) throw new InvalidDataException("combatModel must be a local model key.");
            JArray upper = o["upperworks"] as JArray;
            if (o["upperworks"] != null && upper == null) throw new InvalidDataException("upperworks must be an array of node names.");
            c.Upperworks = new string[upper != null ? upper.Count : 0];
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < c.Upperworks.Length; i++)
            {
                if (upper[i].Type != JTokenType.String || string.IsNullOrEmpty((string)upper[i]))
                    throw new InvalidDataException("upperworks contains an empty or invalid node name.");
                c.Upperworks[i] = (string)upper[i];
                if (!names.Add(c.Upperworks[i])) throw new InvalidDataException("Duplicate upperworks node name.");
            }
            JArray rotors = o["rotors"] as JArray;
            if (o["rotors"] != null && rotors == null) throw new InvalidDataException("rotors must be an array.");
            if (rotors != null)
            {
                if (rotors.Count > 16) throw new InvalidDataException("Too many authored rotors.");
                c.Rotors = new Rotor[rotors.Count];
                HashSet<string> rotorNames = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < rotors.Count; i++)
                {
                    JObject r = rotors[i] as JObject;
                    string node = r != null && r["node"] != null && r["node"].Type == JTokenType.String ? (string)r["node"] : null;
                    if (string.IsNullOrEmpty(node) || !rotorNames.Add(node) || names.Contains(node))
                        throw new InvalidDataException("Rotor node must be unique and separate from upperworks.");
                    float[] pivot = ReadVector(r["pivot"]), axis = ReadVector(r["axis"]);
                    double length = Math.Sqrt((double)axis[0] * axis[0] + (double)axis[1] * axis[1] + (double)axis[2] * axis[2]);
                    if (length < 0.000001) throw new InvalidDataException("Rotor axis must be nonzero.");
                    for (int a = 0; a < 3; a++) axis[a] = (float)(axis[a] / length);
                    float speed = Number(r, "degreesPerSecond");
                    if (Math.Abs(speed) > 720f) throw new InvalidDataException("Rotor speed exceeds 720 degrees per second.");
                    c.Rotors[i] = new Rotor { Node = node, Pivot = pivot, Axis = axis, DegreesPerSecond = speed };
                }
            }
            return c;
        }

        internal bool IsUpperwork(string part)
        {
            foreach (string name in Upperworks)
                if (MatchesNode(part, name)) return true;
            return false;
        }

        internal void ValidateUpperworkNodes(IEnumerable<string> sourceNodes, IEnumerable<string> ambiguousNodes)
        {
            HashSet<string> sources = new HashSet<string>(sourceNodes, StringComparer.Ordinal);
            HashSet<string> ambiguous = new HashSet<string>(ambiguousNodes, StringComparer.Ordinal);
            foreach (string name in Upperworks)
            {
                if (!sources.Contains(name)) throw new InvalidDataException("Declared upperwork source node missing: " + name);
                if (ambiguous.Contains(name)) throw new InvalidDataException("Ambiguous upperwork source node: " + name);
            }
        }

        internal void ValidateRotorNodes(IEnumerable<string> sourceNodes, IEnumerable<string> ambiguousNodes)
        {
            HashSet<string> sources = new HashSet<string>(sourceNodes, StringComparer.Ordinal);
            HashSet<string> ambiguous = new HashSet<string>(ambiguousNodes, StringComparer.Ordinal);
            foreach (Rotor rotor in Rotors)
                if (!sources.Contains(rotor.Node) || ambiguous.Contains(rotor.Node))
                    throw new InvalidDataException("Rotor requires an unambiguous source node: " + rotor.Node);
        }

        internal static bool MatchesNode(string part, string name)
        {
            // Imported part labels always append a primitive suffix. Equality can alias a different
            // source node whose literal name happens to be another node's generated part label.
            string prefix = name + "/p";
            if (!part.StartsWith(prefix, StringComparison.Ordinal) || part.Length == prefix.Length) return false;
            int i = prefix.Length;
            while (i < part.Length && part[i] >= '0' && part[i] <= '9') i++;
            if (i == prefix.Length) return false;
            if (i == part.Length) return true;
            if (part[i++] != '#' || i == part.Length) return false;
            for (; i < part.Length; i++) if (part[i] < '0' || part[i] > '9') return false;
            return true;
        }

        internal float UniformScale(float requiredLength, float requiredBeam)
        {
            if (!Finite(requiredLength) || !Finite(requiredBeam) || requiredLength <= 0f || requiredBeam <= 0f)
                throw new InvalidDataException("Combat footprint is invalid.");
            float length = LongAxis == "z" ? Deck.Length : Deck.Width;
            float beam = LongAxis == "z" ? Deck.Width : Deck.Length;
            float scale = Math.Max(requiredLength / length, requiredBeam / beam);
            if (!Finite(scale) || scale <= 0f) throw new InvalidDataException("Combat scale exceeds the valid range.");
            return scale;
        }

        internal void ValidateBounds(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            const float tolerance = 0.02f;
            if (!Finite(minX) || !Finite(maxX) || !Finite(minY) || !Finite(maxY) || !Finite(minZ) || !Finite(maxZ) ||
                minX >= maxX || minY >= maxY || minZ >= maxZ || DeckY < minY - tolerance || DeckY > maxY + tolerance ||
                Hull.MinX < minX - tolerance || Hull.MaxX > maxX + tolerance ||
                Hull.MinZ < minZ - tolerance || Hull.MaxZ > maxZ + tolerance)
                throw new InvalidDataException("Authored ship measurements exceed the loaded model bounds.");
            foreach (Rotor rotor in Rotors)
                if (rotor.Pivot[0] < minX - tolerance || rotor.Pivot[0] > maxX + tolerance ||
                    rotor.Pivot[1] < minY - tolerance || rotor.Pivot[1] > maxY + tolerance ||
                    rotor.Pivot[2] < minZ - tolerance || rotor.Pivot[2] > maxZ + tolerance)
                    throw new InvalidDataException("Rotor pivot exceeds the loaded model bounds: " + rotor.Node);
        }

        static float[] ReadVector(JToken value)
        {
            JArray array = value as JArray;
            if (array == null || array.Count != 3) throw new InvalidDataException("Rotor vector requires three numbers.");
            float[] vector = new float[3];
            for (int i = 0; i < 3; i++)
            {
                if (array[i].Type != JTokenType.Float && array[i].Type != JTokenType.Integer)
                    throw new InvalidDataException("Rotor vector requires three numbers.");
                vector[i] = (float)array[i];
                if (!Finite(vector[i])) throw new InvalidDataException("Nonfinite rotor vector.");
            }
            return vector;
        }

        static Rectangle ReadRectangle(JObject o)
        {
            Rectangle r = new Rectangle { MinX = Number(o, "minX"), MaxX = Number(o, "maxX"),
                MinZ = Number(o, "minZ"), MaxZ = Number(o, "maxZ") };
            if (!Finite(r.Width) || !Finite(r.Length) || r.Width < 0.01f || r.Length < 0.01f)
                throw new InvalidDataException("Ship rectangle must have finite positive area.");
            return r;
        }

        static float Number(JObject o, string key)
        {
            JToken value = o != null ? o[key] : null;
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
                throw new InvalidDataException("Missing numeric ship field " + key + ".");
            float n = (float)value;
            if (!Finite(n)) throw new InvalidDataException("Nonfinite ship field " + key + ".");
            return n;
        }

        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        static bool SafeKey(string value)
        {
            if (value.Length == 0 || value.Length > 80) return false;
            foreach (char c in value)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            return true;
        }
    }
}
