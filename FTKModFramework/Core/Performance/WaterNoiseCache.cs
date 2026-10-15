using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FTKModFramework.Core.Performance
{
    // Only the pure noise result is cached. Cursors, vertices and mesh uploads remain native.
    internal static class WaterNoiseCache
    {
        internal const int MaximumVertices = 65536;
        internal const int MaximumArrays = 128;
        internal const int MaximumRetainedBytes = 4 * 1024 * 1024;
        private static readonly Dictionary<Vector3[], Entry> Entries = new Dictionary<Vector3[], Entry>();
        private static Entry recent;
        internal static int RetainedVertices { get; private set; }
        internal static int RetainedBytes { get; private set; }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] internal float Value;
            [FieldOffset(0)] internal int Bits;
        }

        internal struct Arguments : IEquatable<Arguments>
        {
            private readonly int x, y;
            internal Arguments(float first, float second)
            {
                FloatBits bits = new FloatBits();
                bits.Value = first; x = bits.Bits;
                bits.Value = second; y = bits.Bits;
            }
            public bool Equals(Arguments other) { return x == other.x && y == other.y; }
            public override bool Equals(object other) { return other is Arguments && Equals((Arguments)other); }
            public override int GetHashCode() { unchecked { return x * 397 ^ y; } }
        }

        private sealed class Entry
        {
            internal readonly Vector3[] Vertices;
            internal readonly int[] Representative;
            internal readonly Arguments[] Inputs;
            internal readonly float[] Results;
            internal readonly bool[] Initialized;
            internal Entry(Vector3[] vertices)
            {
                Vertices = vertices;
                Representative = new int[vertices.Length];
                Inputs = new Arguments[vertices.Length];
                Results = new float[vertices.Length];
                Initialized = new bool[vertices.Length];
                Dictionary<Arguments, int> first = new Dictionary<Arguments, int>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    Arguments position = new Arguments(vertices[i].x, vertices[i].z);
                    int index;
                    if (!first.TryGetValue(position, out index)) { index = i; first.Add(position, i); }
                    Representative[i] = index;
                }
            }
        }

        internal static float Sample(float x, float y, Vector3[] vertices, int index)
        {
            if (!WaterMeshPerformance.Enabled || !(x >= -float.MaxValue && x <= float.MaxValue && y >= -float.MaxValue && y <= float.MaxValue) ||
                vertices == null || index < 0 || index >= vertices.Length) return Mathf.PerlinNoise(x, y);
            Entry entry = recent;
            if (entry == null || !ReferenceEquals(entry.Vertices, vertices))
            {
                if (!Entries.TryGetValue(vertices, out entry))
                {
                    // Includes the retained source array, cache arrays and a conservative per-entry allowance.
                    long bytes = 32L * vertices.Length + 256;
                    if (Entries.Count >= MaximumArrays || vertices.Length > MaximumVertices - RetainedVertices ||
                        bytes > MaximumRetainedBytes - RetainedBytes) return Mathf.PerlinNoise(x, y);
                    entry = new Entry(vertices);
                    Entries.Add(vertices, entry);
                    RetainedVertices += vertices.Length;
                    RetainedBytes += (int)bytes;
                }
                recent = entry;
            }
            Arguments input = new Arguments(x, y);
            int source = entry.Representative[index];
            // Actual argument bits, rather than topology or frame identity, establish safe reuse.
            if (entry.Initialized[source] && entry.Inputs[source].Equals(input)) return entry.Results[source];
            float result = Mathf.PerlinNoise(x, y);
            if (source == index)
            {
                entry.Inputs[index] = input;
                entry.Results[index] = result;
                entry.Initialized[index] = true;
            }
            return result;
        }

        internal static void Clear()
        {
            Entries.Clear(); recent = null; RetainedVertices = 0; RetainedBytes = 0;
        }
    }
}
