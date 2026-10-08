namespace FTKModFramework.Core
{
    /// <summary>
    /// One conditional apparel assignment on an assembled player avatar. An absent exact path is skipped;
    /// a present path must have one skinned renderer with the exact expected native mesh name.
    /// </summary>
    public sealed class PlayerApparelMesh
    {
        public string RendererPath { get; private set; }
        public string ExpectedNativeMeshName { get; private set; }
        public string GlbFileName { get; private set; }
        public string TextureFileName { get; private set; }
        public bool Matte { get; private set; }
        public string MetallicGlossTextureFileName { get; private set; }

        public PlayerApparelMesh(string rendererPath, string expectedNativeMeshName, string glbFileName,
            string textureFileName = null)
            : this(rendererPath, expectedNativeMeshName, glbFileName, textureFileName, null) { }

        public PlayerApparelMesh(string rendererPath, string expectedNativeMeshName, string glbFileName,
            string textureFileName, string metallicGlossTextureFileName)
            : this(rendererPath, expectedNativeMeshName, glbFileName, textureFileName, metallicGlossTextureFileName, false) { }

        public PlayerApparelMesh(string rendererPath, string expectedNativeMeshName, string glbFileName,
            string textureFileName, bool matte)
            : this(rendererPath, expectedNativeMeshName, glbFileName, textureFileName, null, matte) { }

        public PlayerApparelMesh(string rendererPath, string expectedNativeMeshName, string glbFileName,
            string textureFileName, string metallicGlossTextureFileName, bool matte)
        {
            RendererPath = rendererPath;
            ExpectedNativeMeshName = expectedNativeMeshName;
            GlbFileName = glbFileName;
            TextureFileName = textureFileName;
            MetallicGlossTextureFileName = metallicGlossTextureFileName;
            Matte = matte;
        }
    }
}
