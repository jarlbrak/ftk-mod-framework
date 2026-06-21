using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// PURE, DETERMINISTIC low-poly mesh generators for runtime-built creatures (no AssetBundle, no Unity editor).
    /// Each method returns a fresh <see cref="Mesh"/> with NO scene side effects, so callers own placement.
    ///
    /// WHY DETERMINISM- AND CO-OP-SAFE: a generated Mesh never networks and is never serialized (it lives only on
    /// the per-combat body clone, like the rest of the visual layer). But we still hold the bar of identical output
    /// on every client by deriving ALL "lumpiness" from the vertex/segment/ring INDEX via <see cref="Mathf.Sin"/>
    /// and an integer hash, NEVER from <see cref="UnityEngine.Random"/> or <see cref="System.Random"/>. So the same
    /// inputs produce a byte-identical mesh on every machine; if a future change ever DID surface mesh-derived state,
    /// it would still match across clients.
    ///
    /// FLAT-SHADED ON PURPOSE: FTK's art reads faceted, so we never share a vertex between two faces. Every triangle
    /// carries its own three verts; after the split we call <see cref="Mesh.RecalculateNormals"/>, which (because no
    /// vertex is shared) yields a hard per-face normal, giving the chunky low-poly look without hand-authoring normals.
    ///
    /// net35 / Mono 2017.2-safe: only Mesh / Vector3 / Mathf, all present in Unity 2017.2.2p2.
    /// </summary>
    internal static class ProceduralCreature
    {
        /// <summary>
        /// A tapered low-poly prism along local +Y, from y=0 (radius0) to y=length (radius1), with <paramref name="sides"/>
        /// radial faces. Each ring vertex is pushed out by a small INDEX-derived lumpiness factor (deterministic), and
        /// the side quads + the two end caps are emitted with NON-SHARED verts so the mesh reads flat-shaded/faceted.
        /// Use 5 or 6 sides for a low-poly golem limb.
        /// </summary>
        /// <param name="length">Local height along +Y.</param>
        /// <param name="radius0">Radius at the base (y=0).</param>
        /// <param name="radius1">Radius at the top (y=length).</param>
        /// <param name="sides">Radial face count (5 or 6 for the golem read).</param>
        /// <param name="lumpiness">0 = clean prism; ~0.15 = chunky bog-golem. Derived per ring/side index, no Random.</param>
        internal static Mesh BuildSegment(float length, float radius0, float radius1, int sides, float lumpiness)
        {
            if (sides < 3) sides = 3;

            // Two rings (base + top). Compute a per-ring-vertex radius with deterministic index-based lumpiness.
            Vector3[] ring0 = new Vector3[sides];
            Vector3[] ring1 = new Vector3[sides];
            for (int s = 0; s < sides; s++)
            {
                float ang = (Mathf.PI * 2f) * ((float)s / sides);
                float cx = Mathf.Cos(ang);
                float cz = Mathf.Sin(ang);

                float l0 = LumpFactor(s, 0, lumpiness);
                float l1 = LumpFactor(s, 1, lumpiness);

                float r0 = radius0 * l0;
                float r1 = radius1 * l1;

                ring0[s] = new Vector3(cx * r0, 0f, cz * r0);
                ring1[s] = new Vector3(cx * r1, length, cz * r1);
            }

            // Non-shared verts: each side quad = 2 tris = 6 verts; each end cap = (sides) tris = 3*sides verts.
            // We build flat lists, then RecalculateNormals yields hard per-face normals (no vertex is shared).
            int sideVerts = sides * 6;
            int capVerts = sides * 3;       // a triangle fan flattened into independent triangles
            int total = sideVerts + capVerts * 2;

            Vector3[] verts = new Vector3[total];
            int[] tris = new int[total];     // 1:1 because every vert is unique
            int vi = 0;

            // --- side quads (CCW seen from outside) ---
            for (int s = 0; s < sides; s++)
            {
                int sn = (s + 1) % sides;
                Vector3 a = ring0[s];   // base, this column
                Vector3 b = ring0[sn];  // base, next column
                Vector3 c = ring1[sn];  // top,  next column
                Vector3 d = ring1[s];   // top,  this column

                // tri 1: a, c, b   tri 2: a, d, c  (winding gives an outward normal after RecalculateNormals)
                verts[vi] = a; tris[vi] = vi; vi++;
                verts[vi] = c; tris[vi] = vi; vi++;
                verts[vi] = b; tris[vi] = vi; vi++;

                verts[vi] = a; tris[vi] = vi; vi++;
                verts[vi] = d; tris[vi] = vi; vi++;
                verts[vi] = c; tris[vi] = vi; vi++;
            }

            // --- bottom cap (fan around the base centroid, facing -Y) ---
            Vector3 center0 = new Vector3(0f, 0f, 0f);
            for (int s = 0; s < sides; s++)
            {
                int sn = (s + 1) % sides;
                verts[vi] = center0;  tris[vi] = vi; vi++;
                verts[vi] = ring0[s]; tris[vi] = vi; vi++;
                verts[vi] = ring0[sn];tris[vi] = vi; vi++;
            }

            // --- top cap (fan around the top centroid, facing +Y) ---
            Vector3 center1 = new Vector3(0f, length, 0f);
            for (int s = 0; s < sides; s++)
            {
                int sn = (s + 1) % sides;
                verts[vi] = center1;   tris[vi] = vi; vi++;
                verts[vi] = ring1[sn]; tris[vi] = vi; vi++;
                verts[vi] = ring1[s];  tris[vi] = vi; vi++;
            }

            Mesh mesh = new Mesh();
            mesh.name = "ftkmf_seg";
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();   // hard per-face normals: nothing is shared -> faceted look
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A low-poly lumpy blob (an octahedron subdivided <paramref name="subdiv"/> times, then projected to a sphere
        /// of <paramref name="radius"/> with INDEX-based lumpiness). Flat-shaded (non-shared verts + RecalculateNormals).
        /// Used for the head, joint balls, and torso mass. subdiv 0 = an 8-face octahedron; subdiv 1 = 32 faces (the
        /// chunky golem read); keep subdiv low.
        /// </summary>
        /// <param name="radius">Blob radius (local units).</param>
        /// <param name="subdiv">Subdivision passes (0..2; 1 is a good chunky default).</param>
        /// <param name="lumpiness">0 = clean; ~0.15 = mossy lumps. Derived per face/vertex, no Random.</param>
        internal static Mesh BuildBlob(float radius, int subdiv, float lumpiness)
        {
            if (subdiv < 0) subdiv = 0;
            if (subdiv > 3) subdiv = 3;

            // Octahedron: 6 unit corners, 8 triangular faces (each a corner triple).
            Vector3 px = new Vector3( 1f, 0f, 0f);
            Vector3 nx = new Vector3(-1f, 0f, 0f);
            Vector3 py = new Vector3( 0f, 1f, 0f);
            Vector3 ny = new Vector3( 0f,-1f, 0f);
            Vector3 pz = new Vector3( 0f, 0f, 1f);
            Vector3 nz = new Vector3( 0f, 0f,-1f);

            // 8 faces, wound so the normal points outward (CCW seen from outside).
            Vector3[][] faces = new Vector3[][]
            {
                new Vector3[] { py, pz, px },
                new Vector3[] { py, px, nz },
                new Vector3[] { py, nz, nx },
                new Vector3[] { py, nx, pz },
                new Vector3[] { ny, px, pz },
                new Vector3[] { ny, nz, px },
                new Vector3[] { ny, nx, nz },
                new Vector3[] { ny, pz, nx },
            };

            // Emit (subdivided) triangles into a flat, non-shared vert/tri list.
            System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();

            for (int f = 0; f < faces.Length; f++)
            {
                SubdivideFace(faces[f][0], faces[f][1], faces[f][2], subdiv, radius, lumpiness, verts, tris);
            }

            Mesh mesh = new Mesh();
            mesh.name = "ftkmf_blob";
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();   // flat-shaded
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- helpers (pure) ---------------------------------------------------------------------------------

        /// <summary>
        /// Recursively split a unit-sphere triangle into 4, projecting every corner onto the radius with a small
        /// index-derived lumpiness so the blob reads mossy/uneven, NOT a clean sphere. Terminal triangles are
        /// emitted as NON-SHARED verts (3 fresh verts per triangle) for flat shading.
        /// </summary>
        private static void SubdivideFace(Vector3 a, Vector3 b, Vector3 c, int depth, float radius, float lumpiness,
            System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris)
        {
            if (depth <= 0)
            {
                Vector3 pa = ProjectLumpy(a, radius, lumpiness);
                Vector3 pb = ProjectLumpy(b, radius, lumpiness);
                Vector3 pc = ProjectLumpy(c, radius, lumpiness);

                int baseIdx = verts.Count;
                verts.Add(pa); verts.Add(pb); verts.Add(pc);
                tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                return;
            }

            // Midpoints on the unit sphere (normalize so the split stays spherical before lumping).
            Vector3 ab = ((a + b) * 0.5f).normalized;
            Vector3 bc = ((b + c) * 0.5f).normalized;
            Vector3 ca = ((c + a) * 0.5f).normalized;

            SubdivideFace(a,  ab, ca, depth - 1, radius, lumpiness, verts, tris);
            SubdivideFace(ab, b,  bc, depth - 1, radius, lumpiness, verts, tris);
            SubdivideFace(ca, bc, c,  depth - 1, radius, lumpiness, verts, tris);
            SubdivideFace(ab, bc, ca, depth - 1, radius, lumpiness, verts, tris);
        }

        /// <summary>Project a unit-ish direction onto the blob surface with a deterministic, direction-hashed lump.</summary>
        private static Vector3 ProjectLumpy(Vector3 dir, float radius, float lumpiness)
        {
            Vector3 n = dir.normalized;
            // Direction-based, continuous, no Random: a sum of sines keyed off the components gives a smooth bumpy
            // surface that is identical for the same direction on every machine.
            float bump = Mathf.Sin(n.x * 7.13f) * Mathf.Sin(n.y * 5.71f) * Mathf.Sin(n.z * 6.37f);
            float r = radius * (1f + lumpiness * bump);
            return n * r;
        }

        /// <summary>
        /// Deterministic ring-vertex radius multiplier in roughly [1-lumpiness, 1+lumpiness], keyed off the side index
        /// and ring index via an integer hash funneled through Sin. No Random: identical on every client.
        /// </summary>
        private static float LumpFactor(int sideIndex, int ringIndex, float lumpiness)
        {
            if (lumpiness <= 0f) return 1f;
            int h = (sideIndex * 73856093) ^ (ringIndex * 19349663);
            // Map the hash into a stable [-1,1] via Sin of a scaled value (Sin is continuous + deterministic).
            float t = Mathf.Sin(h * 0.0001f + sideIndex * 1.97f + ringIndex * 2.41f);
            return 1f + lumpiness * t;
        }
    }
}
