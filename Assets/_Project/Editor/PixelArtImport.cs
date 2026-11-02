using UnityEditor;

// Art 폴더의 픽셀 아트를 16px = 1유닛, 필터 없음, 압축 없음으로 들여온다.
public class PixelArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/_Project/Art/")) return;
        if (assetPath.Contains("/Portraits/") || assetPath.Contains("/Bodies/") || assetPath.Contains("/UI/Xianxia/")) return; // 일러스트·HD UI는 부드럽게 들여온다
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Sprite;
        t.spriteImportMode = SpriteImportMode.Single;
        t.spritePixelsPerUnit = 16;
        t.filterMode = UnityEngine.FilterMode.Point;
        t.mipmapEnabled = false;
        t.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
