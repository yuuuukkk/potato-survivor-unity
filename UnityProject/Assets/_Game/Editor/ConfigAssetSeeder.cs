using System;
using System.Collections.Generic;
using RogueLike.Core;
using RogueLike.Data;
using UnityEditor;
using UnityEngine;

namespace RogueLike.EditorTools
{
    /// <summary>一次性把旧 JSON 迁到可直接在 Inspector 编辑的 SO；已有 SO 永不覆盖。</summary>
    [InitializeOnLoad]
    public static class ConfigAssetSeeder
    {
        private const string Root = "Assets/_Game/Resources/Config";

        [Serializable] private class WeaponList { public List<WeaponData> weapons; }
        [Serializable] private class EnemyList { public List<EnemyData> enemies; }
        [Serializable] private class ItemList { public List<ItemData> items; }
        [Serializable] private class CharacterList { public List<CharacterData> characters; }
        [Serializable] private class WaveWrapper { public WaveConfig config; }

        static ConfigAssetSeeder()
        {
            EditorApplication.delayCall += SeedMissing;
        }

        [MenuItem("土豆幸存者/生成缺失的 SO 配置")]
        public static void SeedMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/_Game/Resources", "Config");

            bool changed = false;
            if (AssetDatabase.LoadAssetAtPath<GameContentSO>(Root + "/GameContent.asset") == null)
            {
                var content = ScriptableObject.CreateInstance<GameContentSO>();
                content.weapons = Read<WeaponList>("Data/weapons")?.weapons ?? new List<WeaponData>();
                content.enemies = Read<EnemyList>("Data/enemies")?.enemies ?? new List<EnemyData>();
                content.items = Read<ItemList>("Data/items")?.items ?? new List<ItemData>();
                AssetDatabase.CreateAsset(content, Root + "/GameContent.asset");
                changed = true;
            }
            if (AssetDatabase.LoadAssetAtPath<CharacterRosterSO>(Root + "/CharacterRoster.asset") == null)
            {
                var roster = ScriptableObject.CreateInstance<CharacterRosterSO>();
                roster.characters = Read<CharacterList>("Data/characters")?.characters ?? new List<CharacterData>();
                ConfigureDefaultRoles(roster.characters);
                AssetDatabase.CreateAsset(roster, Root + "/CharacterRoster.asset");
                changed = true;
            }
            if (AssetDatabase.LoadAssetAtPath<GameBalanceSO>(Root + "/GameBalance.asset") == null)
            {
                var balance = ScriptableObject.CreateInstance<GameBalanceSO>();
                balance.waves = Read<WaveWrapper>("Data/waveconfig")?.config ?? new WaveConfig();
                balance.offerCount = 3;
                balance.weaponOfferChance = 0.5f;
                balance.sellReturnRate = 0.5f;
                AssetDatabase.CreateAsset(balance, Root + "/GameBalance.asset");
                changed = true;
            }
            if (AssetDatabase.LoadAssetAtPath<GameSettingsSO>(Root + "/GameSettings.asset") == null)
            {
                var settings = ScriptableObject.CreateInstance<GameSettingsSO>();
                settings.xpConfig = Resources.Load<XpConfig>("Data/XpConfig");
                AssetDatabase.CreateAsset(settings, Root + "/GameSettings.asset");
                changed = true;
            }
            if (AssetDatabase.LoadAssetAtPath<UIThemeSO>(Root + "/UITheme.asset") == null)
            {
                var theme = ScriptableObject.CreateInstance<UIThemeSO>();
                AssetDatabase.CreateAsset(theme, Root + "/UITheme.asset");
                changed = true;
            }
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Config] 已生成缺失 SO 配置。之后请直接编辑 Resources/Config 中的资产；旧 JSON 不再优先生效。");
            }
        }

        private static T Read<T>(string path)
        {
            var text = Resources.Load<TextAsset>(path);
            return text != null ? JsonUtility.FromJson<T>(text.text) : default;
        }

        private static void ConfigureDefaultRoles(List<CharacterData> characters)
        {
            if (characters == null) return;
            foreach (var character in characters)
            {
                if (character == null) continue;
                character.allowedWeaponKinds = new List<WeaponKind>();
                if (character.id == "farmer")
                {
                    character.maxWeaponSlots = 6;
                    character.projectileDamageMultiplier = 1.15f;
                    character.description = "枪械专家：6 个武器槽，枪械伤害提高；仍可使用近战。";
                }
                else if (character.id == "chili")
                {
                    character.maxWeaponSlots = 6;
                    character.allowedWeaponKinds.Add(WeaponKind.Melee);
                    character.startingWeapons = new List<WeaponEntry> { new WeaponEntry { weaponId = "fist", level = 1 } };
                    character.description = "近战专家：6 个武器槽，只能使用近战武器。";
                }
                else if (character.id == "mushroom")
                {
                    character.maxWeaponSlots = 1;
                    character.startingWeapons = new List<WeaponEntry> { new WeaponEntry { weaponId = "minigun", level = 1 } };
                    character.baseStats = new List<StatModifier>
                    {
                        new StatModifier(StatType.MaxHp, 25f, 0f),
                        new StatModifier(StatType.DamageMult, 0f, 0.15f)
                    };
                    character.description = "强化单武器角色：仅 1 个武器槽，加特林起手，生命与伤害更高。";
                }
            }
        }
    }
}
