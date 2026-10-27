using UnityEditor;

namespace WebSwing.Editor
{
    public sealed class CitySurfaceImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.EndsWith("/WebSwing/CitySurfaces.png")) return;
            var importer=(TextureImporter)assetImporter;
            importer.isReadable=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=1024;
        }
    }
}
