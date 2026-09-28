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
        /// <summary>Item apparel only. Null supplies the fallback for this renderer path.</summary>
        public GridEditor.FTK_playerGameStart.SkinType? NativeSkinType { get; private set; }

        public PlayerApparelMesh(string rendererPath, string expectedNativeMeshName, string glbFileName,
            string textureFileName = null)
            : this(rendererPath, expectedNativeMeshName, glbFileName, textureFileName, null) { }

        public PlayerApparelMesh(string rendererPath, string expectedNativeMeshName, string glbFileName,
            string textureFileName, GridEditor.FTK_playerGameStart.SkinType? nativeSkinType)
        {
            RendererPath = rendererPath;
            ExpectedNativeMeshName = expectedNativeMeshName;
            GlbFileName = glbFileName;
            TextureFileName = textureFileName;
            NativeSkinType = nativeSkinType;
        }
    }
}
