using UnityEditor;

namespace RogueLike.EditorTools
{
    /// <summary>让 Resources/Art 中的新图片自动成为可直接使用的 2D Sprite。</summary>
    public sealed class GeneratedArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Game/Resources/Art/")) return;
            if (!(assetImporter is TextureImporter importer)) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // AI 原图通常是 1K/2K，不能使用 Unity 默认 100 PPU，否则角色会有十几个世界单位大。
            // 按资源类别把最大边归一到稳定的游戏内尺寸。
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            float maxSide = UnityEngine.Mathf.Max(width, height);
            if (assetPath.Contains("/Characters/"))
                // 原图有透明留白；放大可见角色，使其接近玩家 0.64 世界单位直径的碰撞圈。
                importer.spritePixelsPerUnit = maxSide / 1.58f;
            else if (assetPath.Contains("/Enemies/"))
                importer.spritePixelsPerUnit = maxSide / 0.95f;
            else if (assetPath.Contains("/Weapons/"))
                importer.spritePixelsPerUnit = maxSide / 1.0f;
            else if (assetPath.Contains("/Projectiles/"))
                importer.spritePixelsPerUnit = maxSide / 0.22f;
            else if (assetPath.EndsWith("/pickup_experience.png"))
                importer.spritePixelsPerUnit = maxSide / 0.44f;
            else if (assetPath.Contains("/UI/"))
            {
                // 统一 UI 图采用 9-slice，边角固定、中央可拉伸。
                importer.spritePixelsPerUnit = maxSide;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = UnityEngine.SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = new UnityEngine.Vector4(145f, 145f, 145f, 145f);
            }
            else
                importer.spritePixelsPerUnit = 100f;
        }
    }
}
