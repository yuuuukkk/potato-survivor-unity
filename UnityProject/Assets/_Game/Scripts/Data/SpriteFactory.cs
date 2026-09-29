using System.Collections.Generic;
using UnityEngine;

namespace RogueLike.Data
{
    /// <summary>运行时生成占位美术（纯色圆片/方块），零美术资源依赖。换皮时替换此层即可。</summary>
    public static class SpriteFactory
    {
        private static readonly Dictionary<(Color32, int), Sprite> CircleCache = new Dictionary<(Color32, int), Sprite>();
        private static readonly Dictionary<(int, int), Sprite> SquareCache = new Dictionary<(int, int), Sprite>();

        private const float Ppu = 64f;

        /// <summary>生成世界半径 radius 的纯色圆片。</summary>
        public static Sprite Circle(Color color, float radius)
        {
            int px = Mathf.Max(4, Mathf.CeilToInt(radius * Ppu * 2f));
            var key = ((Color32)color, px);
            if (CircleCache.TryGetValue(key, out var cached)) return cached;

            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float r = px * 0.5f;
            var c = (Color32)color;
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    bool inside = dx * dx + dy * dy <= r * r;
                    tex.SetPixel(x, y, inside ? c : (Color32)Color.clear);
                }
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, px, px), new Vector2(0.5f, 0.5f), Ppu);
            CircleCache[key] = sprite;
            return sprite;
        }

        /// <summary>生成世界尺寸 width×height 的纯色方块。</summary>
        public static Sprite Square(float width, float height)
        {
            int px = Mathf.Max(1, Mathf.CeilToInt(width * Ppu));
            int py = Mathf.Max(1, Mathf.CeilToInt(height * Ppu));
            var key = (px, py);
            if (SquareCache.TryGetValue(key, out var cached)) return cached;

            var tex = new Texture2D(px, py, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var white = (Color32)Color.white;
            for (int y = 0; y < py; y++)
                for (int x = 0; x < px; x++)
                    tex.SetPixel(x, y, white);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, px, py), new Vector2(0.5f, 0.5f), Ppu);
            SquareCache[key] = sprite;
            return sprite;
        }
    }
}
