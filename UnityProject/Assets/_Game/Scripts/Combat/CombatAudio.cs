using RogueLike.Core;
using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Combat
{
    /// <summary>命中音效；SO 可替换 AudioClip，未配置时使用一次生成并缓存的短音。</summary>
    public static class CombatAudio
    {
        private static AudioSource _source;
        private static AudioClip _enemyFallback;
        private static AudioClip _criticalFallback;
        private static AudioClip _playerFallback;
        private static float _nextEnemySound;
        private static float _nextPlayerSound;

        public static void PlayEnemyHit(bool critical)
        {
            if (Time.unscaledTime < _nextEnemySound) return;
            _nextEnemySound = Time.unscaledTime + (critical ? 0.06f : 0.045f);
            var balance = GameDatabase.Balance;
            var clip = critical ? balance != null ? balance.criticalHitSound : null
                : balance != null ? balance.enemyHitSound : null;
            if (clip == null)
                clip = critical
                    ? (_criticalFallback ?? (_criticalFallback = MakeImpact("CriticalHit", 0.16f, 110f, 0.66f, 97)))
                    : (_enemyFallback ?? (_enemyFallback = MakeImpact("EnemyHit", 0.085f, 260f, 0.42f, 31)));
            Play(clip, balance != null ? balance.hitSoundVolume : 0.24f);
        }

        public static void PlayPlayerHit()
        {
            if (Time.unscaledTime < _nextPlayerSound) return;
            _nextPlayerSound = Time.unscaledTime + 0.13f;
            var balance = GameDatabase.Balance;
            var clip = balance != null ? balance.playerHitSound : null;
            if (clip == null)
                clip = _playerFallback ?? (_playerFallback = MakeImpact("PlayerHit", 0.15f, 145f, 0.54f, 59));
            Play(clip, balance != null ? balance.playerHitSoundVolume : 0.33f);
        }

        private static void Play(AudioClip clip, float volume)
        {
            if (clip == null || volume <= 0f) return;
            if (_source == null)
            {
                var go = new GameObject("CombatAudio");
                go.transform.SetParent(PoolRoot.Root);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static AudioClip MakeImpact(string name, float seconds, float pitch, float noiseMix, int seed)
        {
            const int sampleRate = 22050;
            int count = Mathf.CeilToInt(seconds * sampleRate);
            var samples = new float[count];
            var random = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / count;
                float envelope = Mathf.Pow(1f - progress, 3f) * Mathf.Min(1f, t * 260f);
                float tone = Mathf.Sin(2f * Mathf.PI * (pitch * t - pitch * t * t * 2f));
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                samples[i] = (tone * (1f - noiseMix) + noise * noiseMix) * envelope * 0.7f;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
