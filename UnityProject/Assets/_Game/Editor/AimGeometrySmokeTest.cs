using System;
using System.Collections.Generic;
using System.Reflection;
using RogueLike.Combat;
using RogueLike.Data;
using RogueLike.Player;
using UnityEditor;
using UnityEngine;

namespace RogueLike.EditorTools
{
    /// <summary>验证远处枪口射线对准目标，近身时枪不会反向。</summary>
    public static class AimGeometrySmokeTest
    {
        [MenuItem("土豆幸存者/验证枪口瞄准")]
        public static void RunFromMenu()
        {
            try { Validate(); EditorUtility.DisplayDialog("枪口瞄准验证", "通过", "确定"); }
            catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("枪口瞄准验证", ex.Message, "确定"); }
        }

        public static void RunBatch()
        {
            try { Validate(); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        private static void Validate()
        {
            GameObject root = null;
            try
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Resources/Art/Weapons/weapon_pistol.png");
                if (sprite == null) throw new InvalidOperationException("手枪素材未加载");
                root = new GameObject("AimTestPlayer");
                var visuals = root.AddComponent<WeaponVisuals>();
                var gun = new GameObject("Gun");
                gun.transform.SetParent(root.transform, false);
                var renderer = gun.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                var weapon = new WeaponInstance(new WeaponData { id = "pistol", basePrice = 1f }, 1);

                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var renderers = (Dictionary<WeaponInstance, SpriteRenderer>)typeof(WeaponVisuals)
                    .GetField("_weaponRenderers", flags).GetValue(visuals);
                var anchors = (Dictionary<WeaponInstance, Vector3>)typeof(WeaponVisuals)
                    .GetField("_anchors", flags).GetValue(visuals);
                renderers[weapon] = renderer;
                anchors[weapon] = new Vector3(0.55f, 0.28f, 0f);

                CheckFar(visuals, renderer, weapon, new Vector2(3f, 0.8f));
                CheckFar(visuals, renderer, weapon, new Vector2(-3f, -0.8f));
                CheckNear(visuals, renderer, weapon, new Vector2(0.25f, 0f), 1f);
                CheckNear(visuals, renderer, weapon, new Vector2(-0.25f, 0f), -1f);
                Debug.Log("[AimGeometrySmokeTest] PASS: far targets align with muzzle; point-blank targets do not reverse the gun.");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckFar(WeaponVisuals visuals, SpriteRenderer renderer, WeaponInstance weapon, Vector2 target)
        {
            if (!visuals.AimAtTarget(weapon, target, out var dir, out bool close) || close)
                throw new InvalidOperationException("远距离目标被错误归类为贴身");
            if (!visuals.TryGetMuzzle(weapon, out var muzzle)) throw new InvalidOperationException("无法定位枪口");
            Vector2 toTarget = target - (Vector2)muzzle;
            float miss = Mathf.Abs(toTarget.x * dir.y - toTarget.y * dir.x);
            if (miss > 0.01f || Vector2.Dot(toTarget, dir) <= 0f || Vector2.Dot(renderer.transform.right, dir) < 0.99f)
                throw new InvalidOperationException($"枪口未对准 {target}，偏差 {miss}");
        }

        private static void CheckNear(WeaponVisuals visuals, SpriteRenderer renderer, WeaponInstance weapon,
            Vector2 target, float side)
        {
            if (!visuals.AimAtTarget(weapon, target, out var dir, out _))
                throw new InvalidOperationException("贴身目标无法瞄准");
            if (dir.x * side <= 0.9f || renderer.transform.right.x * side <= 0.9f)
                throw new InvalidOperationException("枪在贴身目标前反向了");
        }
    }
}
