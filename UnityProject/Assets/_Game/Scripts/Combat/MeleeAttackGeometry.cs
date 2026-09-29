using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>索敌、可见动画与命中检测共用的剑柄姿态和剑尖轨迹。</summary>
    public static class MeleeAttackGeometry
    {
        public const int TrajectorySegments = 32;

        public static void EvaluatePose(WeaponData data, MeleeAnimationStyle style, Vector2 direction,
            float rangeMultiplier, float phase, out Vector2 handleOffset, out float angle)
        {
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            phase = Mathf.Clamp01(phase);
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float extension = Mathf.Sin(phase * Mathf.PI) * Mathf.Max(0.05f, rangeMultiplier);
            if (style == MeleeAnimationStyle.Thrust)
            {
                handleOffset = direction * (extension * Mathf.Max(0f, data.meleeThrustDistance));
                return;
            }

            float sweep = Mathf.Lerp(-0.5f, 0.5f, Mathf.SmoothStep(0f, 1f, phase));
            angle += sweep * Mathf.Max(0f, data.meleeSwingDegrees);
            Vector2 outward = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            handleOffset = outward * (extension * Mathf.Max(0f, data.meleeSwingOutwardDistance));
        }

        public static Vector2 TipPosition(Vector2 anchor, Vector2 tipOffset, Vector2 handleOffset, float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 facing = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            return anchor + handleOffset + facing * tipOffset.x + new Vector2(-facing.y, facing.x) * tipOffset.y;
        }

        /// <summary>检测尖端两帧之间扫过的路径；不是从剑柄到剑尖的整段剑身。</summary>
        public static bool TipSweepTouches(Vector2 target, float radius, Vector2 previousTip, Vector2 tip)
        {
            Vector2 segment = tip - previousTip;
            float lengthSq = segment.sqrMagnitude;
            float t = lengthSq > 0.000001f
                ? Mathf.Clamp01(Vector2.Dot(target - previousTip, segment) / lengthSq) : 0f;
            return (target - (previousTip + segment * t)).sqrMagnitude <= radius * radius;
        }

        public static float ContactRadius(WeaponData data, Vector3 ownerScale)
            => Mathf.Max(0.025f, data.meleeHitWidth * 0.5f) *
               Mathf.Max(Mathf.Abs(ownerScale.x), Mathf.Abs(ownerScale.y));
    }
}
