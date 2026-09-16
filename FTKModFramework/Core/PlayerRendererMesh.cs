namespace FTKModFramework.Core
{
    /// <summary>One immutable player renderer assignment, relative to the assembled avatar's CEL root.</summary>
    public sealed class PlayerRendererMesh
    {
        public string RendererPath { get; private set; }
        public string GlbFileName { get; private set; }
        public string TextureFileName { get; private set; }

        public PlayerRendererMesh(string rendererPath, string glbFileName, string textureFileName = null)
        {
            RendererPath = rendererPath;
            GlbFileName = glbFileName;
            TextureFileName = textureFileName;
        }
    }
}
