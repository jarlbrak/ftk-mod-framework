using System;

namespace FTKModFramework.Core
{
    /// <summary>The exact Unity renderer component an explicit assignment may replace.</summary>
    public enum EnemyRendererKind
    {
        SkinnedMeshRenderer = 0,
        MeshRenderer = 1
    }

    /// <summary>Explicit primitive to native material slot mapping, with private per-slot options.</summary>
    public sealed class EnemyRendererMaterial
    {
        public int PrimitiveIndex { get; private set; }
        public int NativeMaterialSlot { get; private set; }
        public string TextureFileName { get; private set; }
        public bool DisableNativeEmission { get; private set; }
        public EnemyRendererMaterial(int primitiveIndex, int nativeMaterialSlot, string textureFileName = null,
            bool disableNativeEmission = false)
        {
            PrimitiveIndex = primitiveIndex; NativeMaterialSlot = nativeMaterialSlot;
            TextureFileName = textureFileName; DisableNativeEmission = disableNativeEmission;
        }
    }

    /// <summary>One explicit, immutable mesh assignment relative to the spawned CharacterEventListener root.</summary>
    public sealed class EnemyRendererMesh
    {
        private EnemyRendererMaterial[] _materialSlots;
        public EnemyRendererMaterial[] MaterialSlots { get { return _materialSlots == null ? null : (EnemyRendererMaterial[])_materialSlots.Clone(); } }
        internal EnemyRendererMaterial[] Slots { get { return _materialSlots; } }

        /// <summary>Opt in to 2..4 genuine primitives, one per native slot. Existing constructors remain single-material.</summary>
        public static EnemyRendererMesh WithNativeMaterialSlots(string rendererPath, string glbFileName,
            EnemyRendererMaterial[] materialSlots)
        {
            if (materialSlots == null) throw new ArgumentNullException("materialSlots");
            EnemyRendererMesh value = new EnemyRendererMesh(rendererPath, glbFileName);
            value._materialSlots = (EnemyRendererMaterial[])materialSlots.Clone();
            return value;
        }

        public string RendererPath { get; private set; }
        public string GlbFileName { get; private set; }
        public string TextureFileName { get; private set; }
        public bool DisableNativeEmission { get; private set; }
        public EnemyRendererKind RendererKind { get; private set; }

        /// <param name="rendererPath">Exact slash-separated child names; "." targets the root itself.</param>
        /// <param name="glbFileName">FTK-contract GLB under FTKModFramework_content/models/.</param>
        /// <param name="textureFileName">Optional PNG under the same models directory.</param>
        public EnemyRendererMesh(string rendererPath, string glbFileName, string textureFileName = null)
            : this(rendererPath, glbFileName, textureFileName, false)
        {
        }

        /// <summary>Optionally remove native emission from the private replacement material.</summary>
        /// <param name="disableNativeEmission">False preserves native emission. True disables _EMISSION,
        /// clears _EmissionMap and sets _EmissionColor black where supported. Native materials are untouched.</param>
        public EnemyRendererMesh(string rendererPath, string glbFileName, string textureFileName, bool disableNativeEmission)
            : this(rendererPath, glbFileName, textureFileName, disableNativeEmission, EnemyRendererKind.SkinnedMeshRenderer)
        {
        }

        private EnemyRendererMesh(string rendererPath, string glbFileName, string textureFileName,
            bool disableNativeEmission, EnemyRendererKind rendererKind)
        {
            RendererPath = rendererPath;
            GlbFileName = glbFileName;
            TextureFileName = textureFileName;
            DisableNativeEmission = disableNativeEmission;
            RendererKind = rendererKind;
        }

        /// <summary>
        /// Create an explicit rigid MeshRenderer assignment. The exact target transform must contain one
        /// MeshRenderer and one MeshFilter with a native mesh. The GLB positions are authored in that
        /// MeshFilter transform's local space; it has no skin or bone remapping contract.
        /// </summary>
        public static EnemyRendererMesh ForStaticRenderer(string rendererPath, string glbFileName,
            string textureFileName = null, bool disableNativeEmission = false)
        {
            return new EnemyRendererMesh(rendererPath, glbFileName, textureFileName, disableNativeEmission,
                EnemyRendererKind.MeshRenderer);
        }
    }
}
