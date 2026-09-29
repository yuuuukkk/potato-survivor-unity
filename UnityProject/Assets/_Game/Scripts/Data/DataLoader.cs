using System;
using System.Collections.Generic;
using RogueLike.Core;
using UnityEngine;

namespace RogueLike.Data
{
    /// <summary>
    /// 从 Resources/Data/*.json 加载全部内容配置（JsonUtility）。
    /// 枚举字段在 JSON 中一律存 int，对照表见 docs/03-实现清单与扩展路线.md。
    /// </summary>
    public static class DataLoader
    {
        [Serializable] private class WeaponList { public List<WeaponData> weapons; }
        [Serializable] private class EnemyList { public List<EnemyData> enemies; }
        [Serializable] private class ItemList { public List<ItemData> items; }
        [Serializable] private class CharacterList { public List<CharacterData> characters; }
        [Serializable] private class WaveConfigWrapper { public WaveConfig config; }

        public static void LoadAll()
        {
            var content = Resources.Load<GameContentSO>("Config/GameContent");
            var roster = Resources.Load<CharacterRosterSO>("Config/CharacterRoster");
            var balance = Resources.Load<GameBalanceSO>("Config/GameBalance");
            if (content != null && roster != null && balance != null)
            {
                GameDatabase.Weapons.Clear();
                GameDatabase.WeaponModifications.Clear();
                GameDatabase.DirectorChallengeModifiers.Clear();
                GameDatabase.Enemies.Clear();
                GameDatabase.Items.Clear();
                GameDatabase.Characters.Clear();
                foreach (var d in content.weapons)
                    if (d != null && !string.IsNullOrEmpty(d.id)) GameDatabase.Weapons[d.id] = d;
                if (content.weaponModifications != null)
                    foreach (var d in content.weaponModifications)
                        if (d != null && d.IsValid) GameDatabase.WeaponModifications[d.id] = d;
                if (content.directorChallengeModifiers != null)
                    foreach (var d in content.directorChallengeModifiers)
                        if (d != null && d.IsValid) GameDatabase.DirectorChallengeModifiers[d.id] = d;
                foreach (var d in content.enemies)
                    if (d != null && !string.IsNullOrEmpty(d.id)) GameDatabase.Enemies[d.id] = d;
                foreach (var d in content.items)
                    if (d != null && !string.IsNullOrEmpty(d.id)) GameDatabase.Items[d.id] = d;
                foreach (var d in roster.characters)
                    if (d != null && !string.IsNullOrEmpty(d.id)) GameDatabase.Characters.Add(d);
                GameDatabase.WaveConfig = balance.waves;
                GameDatabase.Balance = balance;
                return;
            }
            Debug.LogWarning("[Data] SO 配置尚未生成，临时读取旧 JSON；请在编辑器等待配置自动迁移完成。");
            LoadWeapons();
            LoadEnemies();
            LoadItems();
            LoadCharacters();
            LoadWaveConfig();
            GameDatabase.Balance = null;
        }

        private static void LoadWeapons()
        {
            var w = LoadJson<WeaponList>("Data/weapons");
            if (w == null || w.weapons == null) return;
            GameDatabase.Weapons.Clear();
            foreach (var d in w.weapons)
            {
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                if (!GameDatabase.Weapons.ContainsKey(d.id)) GameDatabase.Weapons.Add(d.id, d);
            }
        }

        private static void LoadEnemies()
        {
            var e = LoadJson<EnemyList>("Data/enemies");
            if (e == null || e.enemies == null) return;
            GameDatabase.Enemies.Clear();
            foreach (var d in e.enemies)
            {
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                if (!GameDatabase.Enemies.ContainsKey(d.id)) GameDatabase.Enemies.Add(d.id, d);
            }
        }

        private static void LoadItems()
        {
            var i = LoadJson<ItemList>("Data/items");
            if (i == null || i.items == null) return;
            GameDatabase.Items.Clear();
            foreach (var d in i.items)
            {
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                if (!GameDatabase.Items.ContainsKey(d.id)) GameDatabase.Items.Add(d.id, d);
            }
        }

        private static void LoadCharacters()
        {
            var c = LoadJson<CharacterList>("Data/characters");
            if (c == null || c.characters == null) return;
            GameDatabase.Characters.Clear();
            foreach (var d in c.characters)
            {
                if (d != null && !string.IsNullOrEmpty(d.id)) GameDatabase.Characters.Add(d);
            }
        }

        private static void LoadWaveConfig()
        {
            var w = LoadJson<WaveConfigWrapper>("Data/waveconfig");
            if (w != null && w.config != null) GameDatabase.WaveConfig = w.config;
        }

        private static T LoadJson<T>(string path) where T : class
        {
            var ta = Resources.Load<TextAsset>(path);
            if (ta == null)
            {
                Debug.LogError($"[Data] 缺少配置文件: {path}.json（应位于 Assets/_Game/Resources/Data/）");
                return null;
            }
            try
            {
                return JsonUtility.FromJson<T>(ta.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Data] 解析失败 {path}: {ex.Message}");
                return null;
            }
        }

        public static Color ParseColor(string hex)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return Color.white;
        }
    }
}
