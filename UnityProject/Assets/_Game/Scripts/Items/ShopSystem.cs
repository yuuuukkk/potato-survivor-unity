using System.Collections.Generic;
using RogueLike.Combat;
using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Items
{
    /// <summary>
    /// 商店：总共三项混合报价（数量由 GameBalanceSO 配置），支持刷新、锁定、按角色限制刷武器。
    /// </summary>
    public class ShopSystem : MonoBehaviour
    {
        public class ShopSlot
        {
            public bool IsWeapon;
            public bool IsHeal;
            public bool IsModification;
            public WeaponData Weapon;
            public WeaponModificationData Modification;
            public ItemData Item;
            public Rarity Rarity;
            public int Price;
            public bool Locked;
            public int WeaponLevel; // 本次报价的武器等级，与 Rarity 对应
        }

        private readonly List<ShopSlot> _slots = new List<ShopSlot>();
        private readonly List<DirectorChallengeOffer> _challengeOffers = new List<DirectorChallengeOffer>();
        private readonly HashSet<string> _lastChallengeModifierIds = new HashSet<string>();
        private readonly string[] _lastChallengeTierIds = new string[4];
        private readonly List<ShopSlot> _lockedForNextShop = new List<ShopSlot>();
        private int _wave;
        private int _rerolls;
        private readonly HashSet<string> _previousShopKeys = new HashSet<string>();
        private readonly HashSet<string> _lastRollKeys = new HashSet<string>();
        private bool _freeRerollAvailable;
        private int _shopRevision;
        private bool _directorAIUsed;

        public IReadOnlyList<ShopSlot> Slots => _slots;
        public IReadOnlyList<DirectorChallengeOffer> ChallengeOffers => _challengeOffers;
        public bool DirectorAIUsed => _directorAIUsed;
        public int Wave => _wave;
        public int RefreshCost
        {
            get
            {
                if (_freeRerollAvailable) return 0;
                var cfg = GameDatabase.Balance;
                float basePerWave = cfg != null ? cfg.rerollBasePerWave : 0.75f;
                float increasePerWave = cfg != null ? cfg.rerollIncreasePerWave : 0.40f;
                int increment = Mathf.Max(1, Mathf.FloorToInt(increasePerWave * _wave));
                return Mathf.Max(1, Mathf.FloorToInt(basePerWave * _wave) + increment * (_rerolls + 1));
            }
        }

        public void Open(int wave)
        {
            _shopRevision++;
            _wave = wave;
            _rerolls = 0;
            _freeRerollAvailable = false;
            _directorAIUsed = false;
            _previousShopKeys.Clear();
            _slots.Clear();
            _slots.AddRange(_lockedForNextShop);
            _lockedForNextShop.Clear();
            // A locked modification is still only an offer. If its target weapon was sold,
            // removed, or modified incompatibly before this shop, discard the stale offer.
            var ws = PlayerWeaponSystem();
            for (int i = _slots.Count - 1; i >= 0; i--)
                if ((_slots[i].IsModification && (ws == null || !ws.CanApplyModification(_slots[i].Modification))) ||
                    (_slots[i].Item != null && _slots[i].Item.requiresProjectileWeapon &&
                     (ws == null || !ws.HasStandardProjectileWeapon)))
                    _slots.RemoveAt(i);
            foreach (var slot in _slots)
            {
                var key = OfferKey(slot);
                if (!string.IsNullOrEmpty(key)) _previousShopKeys.Add(key);
            }
            FillOffers();
            BuildLocalChallengeOffers();
        }

        public void Close()
        {
            _shopRevision++;
            _lastRollKeys.Clear();
            foreach (var key in _previousShopKeys) _lastRollKeys.Add(key);
            _lockedForNextShop.Clear();
            foreach (var slot in _slots)
                if (slot.Locked) _lockedForNextShop.Add(slot);
            _slots.Clear();
            _challengeOffers.Clear();
        }

        public void ResetForNewRun()
        {
            _shopRevision++;
            _slots.Clear();
            _challengeOffers.Clear();
            _lastChallengeModifierIds.Clear();
            for (int i = 0; i < _lastChallengeTierIds.Length; i++) _lastChallengeTierIds[i] = null;
            _lockedForNextShop.Clear();
            _previousShopKeys.Clear();
            _lastRollKeys.Clear();
            _rerolls = 0;
            _freeRerollAvailable = false;
            _directorAIUsed = false;
        }

        private void BuildLocalChallengeOffers()
        {
            _challengeOffers.Clear();
            var balance = GameDatabase.Balance;
            if (balance == null || _wave + 1 >= GameDatabase.WaveConfig.waveCount ||
                _wave < balance.firstChallengeAfterWave ||
                (_wave - balance.firstChallengeAfterWave) % Mathf.Max(1, balance.challengeShopInterval) != 0 ||
                balance.directorChallengeTiers == null || balance.directorChallengeTiers.Length != 4)
                return;
            var eligible = new List<DirectorChallengeData>();
            foreach (var entry in GameDatabase.DirectorChallengeModifiers.Values)
                if (entry != null && entry.IsValid && entry.minWave <= _wave + 1)
                    eligible.Add(entry);
            if (eligible.Count == 0) return;
            // Shuffle before prioritizing modules absent from the previous challenge shop.
            // Each tier gets a distinct module whenever the catalog has at least four.
            for (int i = eligible.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                var temp = eligible[i];
                eligible[i] = eligible[swap];
                eligible[swap] = temp;
            }
            var prioritized = new List<DirectorChallengeData>(eligible.Count);
            foreach (var candidate in eligible)
                if (!_lastChallengeModifierIds.Contains(candidate.id)) prioritized.Add(candidate);
            foreach (var candidate in eligible)
                if (_lastChallengeModifierIds.Contains(candidate.id)) prioritized.Add(candidate);
            var used = new HashSet<string>();
            for (int i = 0; i < 4; i++)
            {
                var tier = balance.directorChallengeTiers[i];
                if (tier == null) { _challengeOffers.Clear(); return; }
                DirectorChallengeData selected = null;
                foreach (var candidate in prioritized)
                    if (!used.Contains(candidate.id) && candidate.id != _lastChallengeTierIds[i])
                    { selected = candidate; break; }
                if (selected == null)
                    foreach (var candidate in prioritized)
                        if (!used.Contains(candidate.id)) { selected = candidate; break; }
                if (selected == null) selected = prioritized[i % prioritized.Count];
                used.Add(selected.id);
                _challengeOffers.Add(new DirectorChallengeOffer(selected, tier, i));
            }
            RecordChallengeOfferHistory();
        }

        private void RecordChallengeOfferHistory()
        {
            _lastChallengeModifierIds.Clear();
            foreach (var offer in _challengeOffers)
            {
                _lastChallengeModifierIds.Add(offer.Modifier.id);
                _lastChallengeTierIds[offer.TierIndex] = offer.Modifier.id;
            }
        }

        public bool ConfirmChallenge(int index)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Shop || index < 0 || index >= _challengeOffers.Count)
                return false;
            gm.CloseShop(_challengeOffers[index]);
            return true;
        }

        /// <summary>Optional, explicitly paid AI call. Invalid/stale responses never replace local offers.</summary>
        public void RequestDirectorChallenges(System.Action<bool, string> completed)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Shop || gm.Player == null ||
                _challengeOffers.Count == 0 || _directorAIUsed)
            {
                completed?.Invoke(false, "当前不能重新生成挑战。");
                return;
            }
            var settings = GameConfig.Instance != null ? GameConfig.Instance.Settings :
                Resources.Load<GameSettingsSO>("Config/GameSettings");
            if (settings == null || string.IsNullOrWhiteSpace(settings.weaponForgeApiKey))
            {
                completed?.Invoke(false, "未配置 AI 测试 Key，当前是本地挑战。");
                return;
            }
            var allowed = new List<DirectorChallengeData>();
            foreach (var data in GameDatabase.DirectorChallengeModifiers.Values)
                if (data != null && data.IsValid && data.minWave <= _wave + 1) allowed.Add(data);
            allowed.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            if (allowed.Count == 0)
            {
                completed?.Invoke(false, "合法挑战模块不足，保留本地挑战。");
                return;
            }
            _directorAIUsed = true;
            var advisor = GetComponent<WeaponForgeAIAdvisor>();
            if (advisor == null) advisor = gameObject.AddComponent<WeaponForgeAIAdvisor>();
            int waveAtRequest = _wave;
            int revisionAtRequest = _shopRevision;
            var playerAtRequest = gm.Player;
            var ws = playerAtRequest.GetComponent<WeaponSystem>();
            StartCoroutine(advisor.RequestDirectorChallenges(settings.weaponForgeApiKey,
                settings.weaponForgeModel, settings.weaponForgeTimeoutSeconds, _wave + 1,
                gm.LastWaveKills, gm.LastWaveHpPercent, playerAtRequest.Stats.Character,
                ws != null ? ws.Weapons : null, allowed, GameDatabase.Balance.directorChallengeTiers,
                (success, response, error) =>
                {
                    if (gm != GameManager.Instance || gm.State != GameState.Shop ||
                        gm.Player != playerAtRequest || _wave != waveAtRequest ||
                        _shopRevision != revisionAtRequest)
                    {
                        completed?.Invoke(false, "商店已关闭，过期挑战已忽略。");
                        return;
                    }
                    if (!success || response == null)
                    {
                        completed?.Invoke(false, error);
                        return;
                    }
                    var proposed = new List<DirectorChallengeOffer>(4);
                    var seenModifiers = new HashSet<string>();
                    for (int i = 0; i < response.offers.Length; i++)
                    {
                        var entry = response.offers[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.modifierId) ||
                            !GameDatabase.DirectorChallengeModifiers.TryGetValue(entry.modifierId, out var modifier) ||
                            modifier.minWave > _wave + 1 || entry.modifierId.Length > 80 ||
                            entry.title == null || entry.title.Length > 24)
                        {
                            completed?.Invoke(false, "AI 使用了无效挑战，保留本地方案。");
                            return;
                        }
                        seenModifiers.Add(modifier.id);
                        proposed.Add(new DirectorChallengeOffer(modifier,
                            GameDatabase.Balance.directorChallengeTiers[i], i, entry.title));
                    }
                    if (seenModifiers.Count < Mathf.Min(4, allowed.Count))
                    {
                        completed?.Invoke(false, "AI 挑战类型缺少变化，保留本地方案。");
                        return;
                    }
                    _challengeOffers.Clear();
                    _challengeOffers.AddRange(proposed);
                    RecordChallengeOfferHistory();
                    completed?.Invoke(true, "AI 已改写四档挑战；可选择，或返回商店直接进入下一波。");
                }));
        }

        /// <summary>在商店阶段请求 AI 推荐；AI 只能从本地可用 SO 改造目录中选择 ID。</summary>
        public void RequestForgeSuggestion(System.Action<bool, string> completed)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Shop || gm.Player == null)
            {
                completed?.Invoke(false, "只能在商店阶段请求构筑建议。");
                return;
            }
            var ws = gm.Player.GetComponent<WeaponSystem>();
            if (ws == null)
            {
                completed?.Invoke(false, "当前角色没有武器系统。");
                return;
            }
            bool hasReplaceableSlot = false;
            foreach (var slot in _slots)
                if (!slot.Locked && !slot.IsModification) { hasReplaceableSlot = true; break; }
            if (!hasReplaceableSlot)
            {
                completed?.Invoke(false, "没有未锁定的普通商品位，未发起 AI 请求。");
                return;
            }

            var candidates = new List<WeaponModificationData>();
            foreach (var modification in GameDatabase.WeaponModifications.Values)
                if (ws.CanApplyModification(modification) && !IsModificationAlreadyOffered(modification.id))
                    candidates.Add(modification);
            candidates.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            if (candidates.Count == 0)
            {
                completed?.Invoke(false, "当前没有可用的新改造，或商店中没有未锁定位置。");
                return;
            }

            var settings = GameConfig.Instance != null ? GameConfig.Instance.Settings :
                Resources.Load<GameSettingsSO>("Config/GameSettings");
            string prompt = $"第{_wave}波，请根据当前角色、武器、道具和材料，从允许的候选改造中给出最多两条不同构筑路线，并说明各自代价。";
            bool directDeepSeek = settings != null && !string.IsNullOrWhiteSpace(settings.weaponForgeApiKey);
            if (!directDeepSeek && (settings == null || string.IsNullOrWhiteSpace(settings.weaponForgeEndpoint)))
            {
                // Transparent offline path: deliberately not presented as AI output.
                var localNames = new List<string>();
                for (int i = 0; i < candidates.Count && localNames.Count < 2; i++)
                    if (OfferModification(candidates[i], ws)) localNames.Add(candidates[i].displayName);
                if (localNames.Count == 0)
                {
                    completed?.Invoke(false, "没有可替换的未锁定商店位置；请先解锁一个商品位。");
                    return;
                }
                completed?.Invoke(true, "本地目录选择（非 AI）：" + string.Join("、", localNames));
                return;
            }

            var advisor = GetComponent<WeaponForgeAIAdvisor>();
            if (advisor == null) advisor = gameObject.AddComponent<WeaponForgeAIAdvisor>();
            int requestedRevision = _shopRevision;
            var requestedPlayer = gm.Player;
            System.Action<bool, WeaponForgeAIAdvisor.ResponseData, string> onResult = (success, response, error) =>
                {
                    if (GameManager.Instance != gm || gm.State != GameState.Shop ||
                        gm.Player != requestedPlayer || _shopRevision != requestedRevision)
                    {
                        completed?.Invoke(false, "商店内容已变化，这条 AI 建议已作废；如需建议请重新请求。");
                        return;
                    }
                    if (!success || response == null)
                    {
                        completed?.Invoke(false, error);
                        return;
                    }

                    var selected = new List<WeaponModificationData>();
                    foreach (var candidateId in response.candidateIds)
                    {
                        var candidate = GameDatabase.GetWeaponModification(candidateId);
                        if (candidate == null || !candidates.Exists(x => x.id == candidateId))
                        {
                            completed?.Invoke(false, "AI 返回了目录之外的改造，已拒绝该建议。");
                            return;
                        }
                        if (!ws.CanApplyModification(candidate))
                        {
                            completed?.Invoke(false, "AI 的候选与当前武器状态不匹配，未改变商店。");
                            return;
                        }
                        if (selected.Count < 2) selected.Add(candidate);
                    }
                    if (selected.Count == 0)
                    {
                        completed?.Invoke(false, "AI 的候选与当前武器状态不匹配，未应用任何改造。");
                        return;
                    }
                    var offeredNames = new List<string>();
                    foreach (var modification in selected)
                        if (OfferModification(modification, ws)) offeredNames.Add(modification.displayName);
                    if (offeredNames.Count == 0)
                    {
                        completed?.Invoke(false, "没有可替换的未锁定商店位置；请先解锁一个商品位。");
                        return;
                    }
                    string explanation = string.IsNullOrWhiteSpace(response.explanation)
                        ? string.Empty : $" AI 说明（仅供参考）：{response.explanation}";
                    completed?.Invoke(true, string.Join("、", offeredNames) + explanation);
                };
            if (directDeepSeek)
                StartCoroutine(advisor.RequestDirectDeepSeek(settings.weaponForgeApiKey,
                    settings.weaponForgeModel, settings.weaponForgeTimeoutSeconds, prompt,
                    gm.Player.Stats.Character, ws.Weapons, candidates, onResult));
            else
                StartCoroutine(advisor.Request(settings.weaponForgeEndpoint,
                    settings.weaponForgeTimeoutSeconds, prompt, gm.Player.Stats.Character,
                    ws.Weapons, candidates, onResult));
        }

        private bool IsModificationAlreadyOffered(string id)
        {
            foreach (var slot in _slots)
                if (slot.IsModification && slot.Modification != null && slot.Modification.id == id)
                    return true;
            return false;
        }

        private bool OfferModification(WeaponModificationData modification, WeaponSystem ws)
        {
            if (modification == null || ws == null || !ws.CanApplyModification(modification)) return false;
            var replaceable = new List<int>();
            for (int i = 0; i < _slots.Count; i++)
                if (!_slots[i].Locked && !_slots[i].IsModification) replaceable.Add(i);
            if (replaceable.Count == 0) return false;

            WeaponData source = null;
            for (int i = 0; i < ws.Weapons.Count; i++)
                if (modification.AppliesTo(ws.Weapons[i].Data)) { source = ws.Weapons[i].Data; break; }
            if (source == null) return false;

            int price = ModificationPrice(modification, source, ws);
            int index = replaceable[Random.Range(0, replaceable.Count)];
            var oldKey = OfferKey(_slots[index]);
            if (!string.IsNullOrEmpty(oldKey)) _previousShopKeys.Remove(oldKey);
            _slots[index] = new ShopSlot
            {
                IsModification = true,
                Modification = modification,
                Rarity = modification.rarity,
                Price = Mathf.Max(0, price)
            };
            _previousShopKeys.Add(OfferKey(_slots[index]));
            _shopRevision++;
            return true;
        }

        // ---------- 生成报价 ----------

        private Rarity RollRarity(Rarity maximum)
        {
            var cfg = GameDatabase.WaveConfig;
            var gm = GameManager.Instance;
            float luck = gm != null && gm.Player != null ? gm.Player.Stats.Get(StatType.Luck) : 0f;

            // Roll nested tier thresholds from highest down; red is a rare endgame offer.
            float multiplier = Mathf.Max(0f, 1f + luck / 100f);
            float ChanceAt(int tier)
            {
                if (tier <= 1) return 100f;
                int index = tier - 1;
                float perWave = index < cfg.shopTierChancePerWave.Count ? cfg.shopTierChancePerWave[index] : 0f;
                int firstWave = index < cfg.shopTierFirstWave.Count ? cfg.shopTierFirstWave[index] : int.MaxValue;
                float cap = index < cfg.shopTierChanceCap.Count ? cfg.shopTierChanceCap[index] : 0f;
                float waveSteps = Mathf.Max(0, _wave - firstWave + 1);
                return Mathf.Clamp(perWave * waveSteps * multiplier, 0f, cap);
            }

            float roll = Random.value * 100f;
            for (int tier = (int)maximum + 1; tier >= 2; tier--)
                if (roll < ChanceAt(tier)) return (Rarity)(tier - 1);
            return Rarity.Common;
        }

        private ShopSlot RollWeaponSlot(bool avoidLastRoll = true, bool allowCurrentDuplicate = false)
        {
            // Weapons have four upgrade tiers; gold and red quality are reserved for items.
            Rarity rarity = RollRarity(Rarity.Epic);
            var candidates = new List<WeaponData>();
            var ws = PlayerWeaponSystem();
            int offerLevel = Mathf.Clamp((int)rarity + 1, 1, GameDatabase.WaveConfig.weaponMaxLevel);
            foreach (var kv in GameDatabase.Weapons)
            {
                var weapon = kv.Value;
                if (weapon == null || weapon.rarity > rarity || ws != null && !ws.CanUse(weapon)) continue;
                string key = "W:" + weapon.id + ":" + offerLevel;
                if ((!allowCurrentDuplicate && IsOffered(key)) || avoidLastRoll && _lastRollKeys.Contains(key)) continue;
                candidates.Add(weapon);
            }
            if (candidates.Count == 0) return null;
            var data = PickWeaponWithOwnedBias(candidates, ws);

            var slot = new ShopSlot { IsWeapon = true, Weapon = data, Rarity = rarity,
                WeaponLevel = offerLevel };
            slot.Price = Price(data.basePrice * RarityInfo.PriceMult(rarity));
            return slot;
        }

        private WeaponData PickWeaponWithOwnedBias(List<WeaponData> candidates, WeaponSystem ws)
        {
            if (ws == null || ws.Weapons.Count == 0) return candidates[Random.Range(0, candidates.Count)];
            var sameWeapon = new List<WeaponData>();
            var sameKind = new List<WeaponData>();
            foreach (var candidate in candidates)
                foreach (var owned in ws.Weapons)
                {
                    if (owned.Data.id == candidate.id) sameWeapon.Add(candidate);
                    if (owned.Data.kind == candidate.kind) sameKind.Add(candidate);
                    if (owned.Data.id == candidate.id || owned.Data.kind == candidate.kind) break;
                }
            var cfg = GameDatabase.Balance;
            float sameChance = cfg != null ? cfg.sameWeaponOfferChance : 0.20f;
            float kindChance = cfg != null ? cfg.sameKindOfferChance : 0.15f;
            if (_wave <= 5)
                kindChance += Mathf.Max(0f, (cfg != null ? cfg.earlySameKindBonus : 0.15f) -
                    (_wave - 1) * (cfg != null ? cfg.earlySameKindBonusDecay : 0.03f));
            float roll = Random.value;
            if (roll < sameChance && sameWeapon.Count > 0)
                return sameWeapon[Random.Range(0, sameWeapon.Count)];
            if (roll < sameChance + kindChance && sameKind.Count > 0)
                return sameKind[Random.Range(0, sameKind.Count)];
            return candidates[Random.Range(0, candidates.Count)];
        }

        private ShopSlot RollItemSlot(bool avoidLastRoll = true, bool allowCurrentDuplicate = false)
        {
            Rarity rarity = RollRarity(Rarity.Red);
            var candidates = new List<ItemData>();
            var gm = GameManager.Instance;
            var inventory = gm != null && gm.Player != null ? gm.Player.GetComponent<Inventory>() : null;
            var ws = PlayerWeaponSystem();
            for (int tier = (int)rarity; tier >= 0 && candidates.Count == 0; tier--)
            {
                foreach (var kv in GameDatabase.Items)
                {
                    var item = kv.Value;
                    if (item == null || (int)item.rarity != tier) continue;
                    if (item.requiresProjectileWeapon && (ws == null || !ws.HasStandardProjectileWeapon)) continue;
                    if (!item.isConsumable && inventory != null && inventory.GetCount(item.id) >= item.maxStack) continue;
                    string key = "I:" + item.id;
                    if ((!allowCurrentDuplicate && IsOffered(key)) || avoidLastRoll && _lastRollKeys.Contains(key)) continue;
                    candidates.Add(item);
                }
            }
            if (candidates.Count == 0) return null;
            var data = candidates[Random.Range(0, candidates.Count)];
            return new ShopSlot
            {
                IsWeapon = false,
                IsHeal = data.isConsumable,
                Item = data,
                Rarity = data.rarity,
                Price = Price(data.basePrice)
            };
        }

        private ShopSlot RollModificationSlot()
        {
            var ws = PlayerWeaponSystem();
            if (ws == null) return null;
            var candidates = new List<WeaponModificationData>();
            var unlockWaves = GameDatabase.WaveConfig.rarityUnlockWave;
            foreach (var modification in GameDatabase.WeaponModifications.Values)
            {
                if (modification == null || !ws.CanApplyModification(modification))
                    continue;
                int tier = (int)modification.rarity;
                if (tier < 0 || tier >= unlockWaves.Count || _wave < unlockWaves[tier]) continue;
                var key = "M:" + modification.id;
                if (IsOffered(key) || _lastRollKeys.Contains(key)) continue;
                candidates.Add(modification);
            }
            if (candidates.Count == 0) return null;
            var pick = candidates[Random.Range(0, candidates.Count)];
            WeaponInstance source = null;
            for (int i = 0; i < ws.Weapons.Count; i++)
                if (pick.AppliesTo(ws.Weapons[i].Data)) { source = ws.Weapons[i]; break; }
            if (source == null) return null;
            int offerPrice = ModificationPrice(pick, source.Data, ws);
            return new ShopSlot
            {
                IsModification = true,
                Modification = pick,
                Rarity = pick.rarity,
                Price = offerPrice
            };
        }

        private int ModificationPrice(WeaponModificationData modification, WeaponData source, WeaponSystem ws)
        {
            float basePrice = modification.price > 0 ? modification.price : source.basePrice;
            int ownedLevel = ws != null ? ws.ModificationLevel(modification.id) : 0;
            return Price(basePrice * (1f + ownedLevel * Mathf.Max(0f, modification.upgradePriceMultiplier)));
        }

        private int Price(float basePrice)
        {
            var cfg = GameDatabase.Balance;
            float flat = cfg != null ? cfg.shopPriceFlatPerWave : 1f;
            float percent = cfg != null ? cfg.shopPricePercentPerWave : 0.10f;
            return Mathf.Max(1, Mathf.FloorToInt(basePrice + flat * _wave + basePrice * percent * _wave));
        }

        // ---------- 操作 ----------

        private void FillOffers()
        {
            int count = GameDatabase.Balance != null ? GameDatabase.Balance.offerCount : 3;
            float weaponChance = GameDatabase.Balance != null ? GameDatabase.Balance.weaponOfferChance : 0.5f;
            int targetCount = Mathf.Clamp(count, 1, 6);
            int guaranteedWeapons = _wave <= 2
                ? (GameDatabase.Balance != null ? GameDatabase.Balance.earlyShopGuaranteedWeapons : 2)
                : _wave <= 5
                    ? (GameDatabase.Balance != null ? GameDatabase.Balance.midShopGuaranteedWeapons : 1) : 0;
            guaranteedWeapons = Mathf.Clamp(guaranteedWeapons, 0, targetCount);
            while (_slots.Count < targetCount)
            {
                int weaponCount = 0;
                foreach (var current in _slots) if (current.IsWeapon) weaponCount++;
                bool tryWeapon = weaponCount < guaranteedWeapons ||
                    _wave > 2 && Random.value < Mathf.Clamp01(weaponChance);
                if (_wave <= 2 && weaponCount >= guaranteedWeapons) tryWeapon = false;
                var slot = tryWeapon ? RollWeaponSlot() :
                    Random.value < (GameDatabase.Balance != null ? GameDatabase.Balance.modificationOfferChance : 0.18f)
                        ? RollModificationSlot() ?? RollItemSlot()
                        : RollItemSlot();
                // A character restricted to one weapon type still gets the early weapon
                // guarantee. Duplicate offers are allowed when the eligible pool is tiny.
                if (slot == null && weaponCount < guaranteedWeapons && tryWeapon)
                    slot = RollWeaponSlot(false, true);
                if (slot == null) slot = tryWeapon ? RollItemSlot() : RollWeaponSlot();
                // A tiny allowed pool can have no fresh alternatives; reusing an older
                // offer is preferable to an empty shop, while this page stays unique.
                if (slot == null) slot = tryWeapon ? RollWeaponSlot(false) : RollItemSlot(false);
                if (slot == null) slot = tryWeapon ? RollItemSlot(false) : RollWeaponSlot(false);
                // Never leave a blank slot merely because every eligible product appeared
                // on the current or previous page. Consumables remain purchasable.
                if (slot == null) slot = RollItemSlot(false, true) ?? RollWeaponSlot(false, true);
                if (slot == null) break;
                var key = OfferKey(slot);
                if (!string.IsNullOrEmpty(key)) _previousShopKeys.Add(key);
                _slots.Add(slot);
            }
        }

        private bool IsOffered(string key) => _previousShopKeys.Contains(key);

        private static string OfferKey(ShopSlot slot)
        {
            if (slot.IsWeapon && slot.Weapon != null) return "W:" + slot.Weapon.id + ":" + slot.WeaponLevel;
            if (slot.Item != null) return "I:" + slot.Item.id;
            if (slot.IsModification && slot.Modification != null) return "M:" + slot.Modification.id;
            return null;
        }

        public bool Buy(int index)
        {
            if (index < 0 || index >= _slots.Count) return false;
            var slot = _slots[index];
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Shop || gm.Player == null) return false;
            if (slot.IsWeapon)
            {
                var currentWs = gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
                if (currentWs == null || !currentWs.CanAcquire(slot.Weapon, slot.WeaponLevel)) return false;
            }
            if (slot.IsModification)
            {
                var currentWs = gm.Player.GetComponent<WeaponSystem>();
                if (currentWs == null || !currentWs.CanApplyModification(slot.Modification)) return false;
            }
            if (slot.Item != null && slot.Item.requiresProjectileWeapon)
            {
                var currentWs = gm.Player.GetComponent<WeaponSystem>();
                if (currentWs == null || !currentWs.HasStandardProjectileWeapon) return false;
            }
            if (!gm.TrySpend(slot.Price)) return false;

            if (slot.IsHeal)
            {
                // 消耗品：立即生效（急救包回血），不占背包
                if (slot.Item != null && slot.Item.healAmount > 0)
                    gm.Player.Health.Heal(slot.Item.healAmount);
            }
            else if (slot.IsWeapon)
            {
                var ws = gm.Player.GetComponent<WeaponSystem>();
                if (ws == null) { Refund(slot); return false; }
                if (!ws.AddWeapon(slot.Weapon, slot.WeaponLevel, slot.Price)) { Refund(slot); return false; }
            }
            else if (slot.IsModification)
            {
                var ws = gm.Player.GetComponent<WeaponSystem>();
                if (ws == null || !ws.TryApplyModification(slot.Modification))
                {
                    Refund(slot);
                    return false;
                }
            }
            else
            {
                var inv = gm.Player.GetComponent<Inventory>();
                if (inv == null || !inv.AddItem(slot.Item))
                {
                    Refund(slot);
                    return false;
                }
            }

            _slots.RemoveAt(index);
            _shopRevision++;
            if (_slots.Count == 0) _freeRerollAvailable = true;
            return true;
        }

        public bool Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Shop) return false;
            bool free = _freeRerollAvailable;
            if (!free && !gm.TrySpend(RefreshCost)) return false;
            _shopRevision++;

            var kept = new List<ShopSlot>();
            foreach (var s in _slots) if (s.Locked) kept.Add(s);
            if (free) _freeRerollAvailable = false;
            else _rerolls++;
            _slots.Clear();
            _slots.AddRange(kept);
            _lastRollKeys.Clear();
            foreach (var key in _previousShopKeys) _lastRollKeys.Add(key);
            _previousShopKeys.Clear();
            foreach (var slot in _slots)
            {
                var key = OfferKey(slot);
                if (!string.IsNullOrEmpty(key)) _previousShopKeys.Add(key);
            }
            FillOffers();
            return true;
        }

        public void ToggleLock(int index)
        {
            if (index >= 0 && index < _slots.Count)
            {
                _slots[index].Locked = !_slots[index].Locked;
                _shopRevision++;
            }
        }

        private void Refund(ShopSlot slot)
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.AddMaterials(slot.Price);
        }

        private WeaponSystem PlayerWeaponSystem()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.Player != null ? gm.Player.GetComponent<WeaponSystem>() : null;
        }
    }
}
