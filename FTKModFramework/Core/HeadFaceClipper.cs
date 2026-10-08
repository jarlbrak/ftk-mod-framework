using System;
using System.Collections.Generic;
using UnityEngine;

namespace FTKModFramework.Core
{
    // The selected interior is evaluated in worn helmet MeshFilter local coordinates.
    internal sealed class HeadClipPlane
    {
        internal Vector3 Normal;
        internal float Distance;
        internal float Signed(Vector3 point) { return Vector3.Dot(Normal, point) - Distance; }
    }
    internal static class HeadFaceClipper
    {
        const float Epsilon = 0.000001f;
        const float ContainmentMargin = 0.00001f;
        const int MaxVertices = 60000;
        const int MaxTriangles = 80000;

        internal static void RunSyntheticTests()
        {
            HeadClipPlane[] box = {
                new HeadClipPlane { Normal = Vector3.right, Distance = 1f },
                new HeadClipPlane { Normal = Vector3.left, Distance = 0f },
                new HeadClipPlane { Normal = Vector3.up, Distance = 1f },
                new HeadClipPlane { Normal = Vector3.down, Distance = 0f }
            };
            Vertex a = new Vertex { Posed = new Vector3(.2f,.2f,0), Weight = new BoneWeight { boneIndex0=0, weight0=1f } };
            Vertex b = new Vertex { Posed = new Vector3(.8f,.2f,0), Weight = new BoneWeight { boneIndex0=0, weight0=1f } };
            Vertex c = new Vertex { Posed = new Vector3(.5f,.8f,0), Weight = new BoneWeight { boneIndex0=1, weight0=1f } };
            List<List<Vertex>> outside = new List<List<Vertex>>();
            if (!Subtract(new List<Vertex>{a,b,c},box,outside) || outside.Count != 0 ||
                StrictHeadTriangle(a,b,c,0) ||
                !RemoveContainedOriginal(true,true,false,BoundaryUncertaintyMask(a,b,c,box),outside.Count))
                throw new InvalidOperationException("Face containment synthetic check failed");
            c.Posed = new Vector3(1.1f,.8f,0); outside.Clear();
            if (!Subtract(new List<Vertex>{a,b,c},box,outside) || outside.Count == 0 ||
                RemoveContainedOriginal(true,true,false,BoundaryUncertaintyMask(a,b,c,box),outside.Count))
                throw new InvalidOperationException("Face boundary synthetic check failed");
            if (MaxVertices > 65535 || MaxTriangles < 1)
                throw new InvalidOperationException("Face output budget synthetic check failed");
        }

        internal static Mesh EmptyLowerHair(Mesh source)
        {
            if (source == null || source.subMeshCount < 1 || source.subMeshCount > 16)
                throw new InvalidOperationException("Exact lower hair source required");
            int triangles = 0;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                if (source.GetTopology(sub) != MeshTopology.Triangles)
                    throw new InvalidOperationException("Nontriangle lower hair source");
                int count = source.GetIndices(sub).Length;
                if (count % 3 != 0 || count / 3 > MaxTriangles)
                    throw new InvalidOperationException("Lower hair triangle bound failed");
                triangles += count / 3;
            }
            if (triangles == 0) throw new InvalidOperationException("Lower hair already empty");
            Mesh output = UnityEngine.Object.Instantiate(source);
            try
            {
                output.name = "ftkmf_head_hidden_lower:" + source.name;
                for (int sub = 0; sub < source.subMeshCount; sub++)
                    output.SetIndices(new int[0], MeshTopology.Triangles, sub);
                output.bounds = source.bounds;
                if (output.vertexCount != source.vertexCount || output.subMeshCount != source.subMeshCount ||
                    output.bindposes.Length != source.bindposes.Length)
                    throw new InvalidOperationException("Empty lower hair changed binding layout");
                return output;
            }
            catch { UnityEngine.Object.Destroy(output); throw; }
        }

    sealed class Vertex
    {
        internal Vector3 Position, Posed, Normal;
        internal Vector4 Tangent;
        internal Vector2 Uv, Uv2, Uv3, Uv4;
        internal Color Color;
        internal BoneWeight Weight;
    }

    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    static bool FullBone(BoneWeight value, int index)
    {
        return value.boneIndex0 == index && Finite(value.weight0) && Finite(value.weight1) &&
            Finite(value.weight2) && Finite(value.weight3) &&
            Math.Abs(value.weight0 - 1f) <= Epsilon && Math.Abs(value.weight1) <= Epsilon &&
            Math.Abs(value.weight2) <= Epsilon && Math.Abs(value.weight3) <= Epsilon;
    }
    static bool StrictHeadTriangle(Vertex a, Vertex b, Vertex c, int head)
    { return FullBone(a.Weight,head) && FullBone(b.Weight,head) && FullBone(c.Weight,head); }
    static Vertex Blend(Vertex a, Vertex b, float t)
    {
        return new Vertex {
            Position = Vector3.LerpUnclamped(a.Position, b.Position, t),
            Posed = Vector3.LerpUnclamped(a.Posed, b.Posed, t),
            Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized,
            Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t),
            Uv = Vector2.LerpUnclamped(a.Uv, b.Uv, t),
            Uv2 = Vector2.LerpUnclamped(a.Uv2, b.Uv2, t),
            Uv3 = Vector2.LerpUnclamped(a.Uv3, b.Uv3, t),
            Uv4 = Vector2.LerpUnclamped(a.Uv4, b.Uv4, t),
            Color = Color.LerpUnclamped(a.Color,b.Color,t),
            Weight = a.Weight
        };
    }
    static void Split(List<Vertex> input, HeadClipPlane plane, List<Vertex> inside, List<Vertex> outside)
    {
        for (int i = 0; i < input.Count; i++)
        {
            Vertex a = input[i], b = input[(i + 1) % input.Count];
            float da = plane.Signed(a.Posed), db = plane.Signed(b.Posed);
            if (Math.Abs(da) <= Epsilon) da = 0f;
            if (Math.Abs(db) <= Epsilon) db = 0f;
            bool ia = da <= 0f, ib = db <= 0f;
            if (ia) inside.Add(a); else outside.Add(a);
            if (ia == ib) continue;
            float t = da / (da - db);
            if (!Finite(t) || t < -Epsilon || t > 1f + Epsilon)
                throw new InvalidOperationException("Invalid face-mask edge intersection");
            Vertex cross = Blend(a, b, Mathf.Clamp01(t));
            inside.Add(cross); outside.Add(cross);
        }
    }
    static bool HasArea(List<Vertex> polygon)
    {
        if (polygon.Count < 3) return false;
        Vector3 origin = polygon[0].Posed;
        float doubled = 0f;
        for (int i = 1; i + 1 < polygon.Count; i++)
            doubled += Vector3.Cross(polygon[i].Posed-origin,
                polygon[i+1].Posed-origin).magnitude;
        return Finite(doubled) && doubled > 0.00000001f;
    }
    static bool Subtract(List<Vertex> triangle, HeadClipPlane[] planes, List<List<Vertex>> outsidePieces)
    {
        List<Vertex> inside = triangle;
        foreach (HeadClipPlane plane in planes)
        {
            if (inside.Count == 0) break;
            List<Vertex> nextInside = new List<Vertex>(), outside = new List<Vertex>();
            Split(inside,plane,nextInside,outside);
            if (HasArea(outside)) outsidePieces.Add(outside);
            inside = nextInside;
        }
        return HasArea(inside);
    }
    static long BoundaryUncertaintyMask(Vertex a, Vertex b, Vertex c, HeadClipPlane[] planes)
    {
        long mask=0;
        for (int i=0;i<planes.Length;i++)
            if (planes[i].Signed(a.Posed)>-ContainmentMargin ||
                planes[i].Signed(b.Posed)>-ContainmentMargin ||
                planes[i].Signed(c.Posed)>-ContainmentMargin)
                mask|=1L<<i;
        return mask;
    }
    static bool RemoveContainedOriginal(bool requested, bool affected, bool strictHead,
        long boundaryUncertaintyMask, int outsidePieceCount)
    {
        if (!requested || !affected || strictHead || boundaryUncertaintyMask!=0) return false;
        if (outsidePieceCount!=0)
            throw new InvalidOperationException("Interior original triangle unexpectedly has outside pieces");
        return true;
    }
    static void Emit(List<Vertex> polygon, List<Vector3> positions, List<Vector3> normals,
        List<Vector4> tangents, List<Vector2>[] uvs, List<Color> colors,
        List<BoneWeight> weights, List<int> indices, bool[] channels)
    {
        if (polygon.Count < 3) return;
        int first = positions.Count;
        foreach (Vertex v in polygon)
        {
            if (!Finite(v.Position) || !Finite(v.Normal) || positions.Count >= MaxVertices)
                throw new InvalidOperationException("Face-mask output vertex bound or finite check failed");
            positions.Add(v.Position); normals.Add(v.Normal); weights.Add(v.Weight);
            if (channels[0]) tangents.Add(v.Tangent);
            if (channels[1]) uvs[0].Add(v.Uv);
            if (channels[2]) uvs[1].Add(v.Uv2);
            if (channels[3]) uvs[2].Add(v.Uv3);
            if (channels[4]) uvs[3].Add(v.Uv4);
            if (channels[5]) colors.Add(v.Color);
        }
        for (int i = 1; i + 1 < polygon.Count; i++)
        { indices.Add(first); indices.Add(first + i); indices.Add(first + i + 1); }
    }

        internal static Mesh Clip(SkinnedMeshRenderer renderer, Transform helmetMesh,
            HeadFaceOcclusion volume, Mesh expectedSource, bool removeContainedNonHead)
        {
            if (renderer == null || helmetMesh == null || volume == null || expectedSource == null ||
                renderer.sharedMesh != expectedSource || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Exact active face source required");
            HeadPlane[] declared = volume.Planes;
            HeadClipPlane[] planes = new HeadClipPlane[declared.Length];
            for (int i = 0; i < declared.Length; i++)
                planes[i] = new HeadClipPlane { Normal = new Vector3(declared[i].X, declared[i].Y, declared[i].Z),
                    Distance = declared[i].Distance };
            Mesh source = expectedSource;
            Vector3[] vertices = source.vertices, normals = source.normals;
            Vector4[] tangents = source.tangents;
            Vector2[][] uv = { source.uv, source.uv2, source.uv3, source.uv4 };
            Color[] colors = source.colors;
            BoneWeight[] weights = source.boneWeights;
            Matrix4x4[] bindposes = source.bindposes;
            int count = vertices.Length;
            if (count == 0 || count > MaxVertices || normals.Length != count || weights.Length != count ||
                bindposes.Length == 0 || source.blendShapeCount != 0 || source.subMeshCount < 1 ||
                source.subMeshCount > 16 || (tangents.Length != 0 && tangents.Length != count) ||
                (colors.Length != 0 && colors.Length != count))
                throw new InvalidOperationException("Unsupported face source channels or size");
            bool[] channels = new bool[6];
            channels[0] = tangents.Length == count;
            for (int i = 0; i < 4; i++)
            {
                if (uv[i].Length != 0 && uv[i].Length != count)
                    throw new InvalidOperationException("Partial face UV channel");
                channels[i + 1] = uv[i].Length == count;
                List<Vector4> full = new List<Vector4>(); source.GetUVs(i, full);
                if (full.Count != 0 && full.Count != count)
                    throw new InvalidOperationException("Unsupported face UV layout");
                foreach (Vector4 value in full)
                    if (!Finite(value.x) || !Finite(value.y) || Math.Abs(value.z) > Epsilon || Math.Abs(value.w) > Epsilon)
                        throw new InvalidOperationException("Face UV exceeds XY channels");
            }
            channels[5] = colors.Length == count;
            Transform[] bones = renderer.bones;
            if (bones.Length != bindposes.Length) throw new InvalidOperationException("Face bindpose/bone mismatch");
            int head = -1;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && bones[i].name == "Head_M")
                { if (head >= 0) throw new InvalidOperationException("Ambiguous Head_M"); head = i; }
            if (head < 0 || !helmetMesh.IsChildOf(bones[head]))
                throw new InvalidOperationException("Helmet is not under exact Head_M");
            Matrix4x4 toMask = helmetMesh.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            if (!Finite(toMask.determinant) || Math.Abs(toMask.determinant) < Epsilon)
                throw new InvalidOperationException("Singular face/helmet transform");
            Mesh baked = new Mesh(); Vector3[] posed;
            try { renderer.BakeMesh(baked); posed = baked.vertices; }
            finally { UnityEngine.Object.Destroy(baked); }
            if (posed.Length != count) throw new InvalidOperationException("Baked face source count changed");
            Vertex[] input = new Vertex[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 position = toMask.MultiplyPoint3x4(posed[i]);
                if (!Finite(position) || !Finite(vertices[i]) || !Finite(normals[i]))
                    throw new InvalidOperationException("Nonfinite face source channel");
                input[i] = new Vertex { Position = vertices[i], Posed = position, Normal = normals[i],
                    Tangent = channels[0] ? tangents[i] : Vector4.zero,
                    Uv = channels[1] ? uv[0][i] : Vector2.zero,
                    Uv2 = channels[2] ? uv[1][i] : Vector2.zero,
                    Uv3 = channels[3] ? uv[2][i] : Vector2.zero,
                    Uv4 = channels[4] ? uv[3][i] : Vector2.zero,
                    Color = channels[5] ? colors[i] : Color.white, Weight = weights[i] };
            }
            List<Vector3> outPosition = new List<Vector3>(), outNormal = new List<Vector3>();
            List<Vector4> outTangent = new List<Vector4>();
            List<Vector2>[] outUv = { new List<Vector2>(), new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
            List<Color> outColor = new List<Color>(); List<BoneWeight> outWeight = new List<BoneWeight>();
            List<int>[] outIndices = new List<int>[source.subMeshCount];
            bool changed = false, strictSelected = false;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                if (source.GetTopology(sub) != MeshTopology.Triangles)
                    throw new InvalidOperationException("Nontriangle face submesh");
                int[] indices = source.GetIndices(sub);
                if (indices.Length % 3 != 0 || indices.Length / 3 > MaxTriangles)
                    throw new InvalidOperationException("Face triangle bound failed");
                outIndices[sub] = new List<int>();
                for (int n = 0; n < indices.Length; n += 3)
                {
                    int a = indices[n], b = indices[n + 1], c = indices[n + 2];
                    if (a < 0 || b < 0 || c < 0 || a >= count || b >= count || c >= count)
                        throw new InvalidOperationException("Invalid face triangle index");
                    List<Vertex> triangle = new List<Vertex> { input[a], input[b], input[c] };
                    List<List<Vertex>> outside = new List<List<Vertex>>();
                    bool affected = Subtract(triangle, planes, outside);
                    bool strict = StrictHeadTriangle(input[a], input[b], input[c], head);
                    bool contained = affected && !strict && removeContainedNonHead &&
                        RemoveContainedOriginal(true, true, false,
                            BoundaryUncertaintyMask(input[a], input[b], input[c], planes), outside.Count);
                    if (strict && affected && channels[0] &&
                        (Math.Abs(input[a].Tangent.w - input[b].Tangent.w) > Epsilon ||
                         Math.Abs(input[a].Tangent.w - input[c].Tangent.w) > Epsilon))
                        throw new InvalidOperationException("Covered tangent handedness differs");
                    if (strict && affected) strictSelected = true;
                    if (contained || (strict && affected)) changed = true;
                    if (contained) continue;
                    if (!affected || !strict)
                        Emit(triangle, outPosition, outNormal, outTangent, outUv, outColor,
                            outWeight, outIndices[sub], channels);
                    else foreach (List<Vertex> piece in outside)
                        Emit(piece, outPosition, outNormal, outTangent, outUv, outColor,
                            outWeight, outIndices[sub], channels);
                }
            }
            // A body-only contained nonHead omission cannot establish mask coverage by itself.
            if (!changed || (removeContainedNonHead && !strictSelected)) return null;
            Mesh output = new Mesh();
            try
            {
                output.name = "ftkmf_face_occlusion:" + source.name;
                output.vertices = outPosition.ToArray(); output.normals = outNormal.ToArray();
                if (channels[0]) output.tangents = outTangent.ToArray();
                if (channels[1]) output.uv = outUv[0].ToArray();
                if (channels[2]) output.uv2 = outUv[1].ToArray();
                if (channels[3]) output.uv3 = outUv[2].ToArray();
                if (channels[4]) output.uv4 = outUv[3].ToArray();
                if (channels[5]) output.colors = outColor.ToArray();
                output.boneWeights = outWeight.ToArray(); output.bindposes = bindposes;
                output.subMeshCount = source.subMeshCount;
                for (int sub = 0; sub < source.subMeshCount; sub++)
                    output.SetIndices(outIndices[sub].ToArray(), MeshTopology.Triangles, sub);
                output.bounds = source.bounds;
                return output;
            }
            catch { UnityEngine.Object.Destroy(output); throw; }
        }
    }
}
