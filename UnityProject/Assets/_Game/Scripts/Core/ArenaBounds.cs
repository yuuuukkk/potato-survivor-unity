using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>竞技场边界：固定单屏，所有实体位置被钳制在内，敌人从边界外生成。</summary>
    public static class ArenaBounds
    {
        public static Vector2 Size = new Vector2(24f, 13.5f);

        public static Rect Rect => new Rect(-Size.x * 0.5f, -Size.y * 0.5f, Size.x, Size.y);

        public static Vector3 Clamp(Vector3 p)
        {
            var r = Rect;
            p.x = Mathf.Clamp(p.x, r.xMin, r.xMax);
            p.y = Mathf.Clamp(p.y, r.yMin, r.yMax);
            return p;
        }

        public static bool Contains(Vector2 p, float pad = 0f)
        {
            var r = Rect;
            return p.x >= r.xMin + pad && p.x <= r.xMax - pad &&
                   p.y >= r.yMin + pad && p.y <= r.yMax - pad;
        }

        /// <summary>在边界外一小段距离随机取一个生成点。</summary>
        public static Vector2 RandomEdgePoint(float margin = 0.8f)
        {
            var r = Rect;
            float side = Random.Range(0, 4);
            switch ((int)side)
            {
                case 0: return new Vector2(Random.Range(r.xMin, r.xMax), r.yMax + margin);
                case 1: return new Vector2(Random.Range(r.xMin, r.xMax), r.yMin - margin);
                case 2: return new Vector2(r.xMin - margin, Random.Range(r.yMin, r.yMax));
                default: return new Vector2(r.xMax + margin, Random.Range(r.yMin, r.yMax));
            }
        }
    }
}
