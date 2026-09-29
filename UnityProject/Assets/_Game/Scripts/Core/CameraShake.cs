using RogueLike.Data;
using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>
    /// 屏幕震动（受击/爆炸反馈）。挂到摄像机上的单例。
    /// 只输出偏移量（CurrentOffset），不直接设置相机位置——
    /// 相机位置由 CameraFollow 负责（跟随主角），Shake 偏移叠加在其上，二者不再互相覆盖。
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        public Vector3 CurrentOffset { get; private set; } = Vector3.zero;

        private float _t, _dur, _mag;
        private float _seed;

        public static void Shake(float duration, float magnitude)
        {
            if (Instance != null) Instance.StartShake(duration, magnitude);
        }

        private void Awake()
        {
            Instance = this;
        }

        private void StartShake(float duration, float magnitude)
        {
            if (duration <= 0f || magnitude <= 0f) return;
            // 高频普通命中不能覆盖玩家受击等更强的镜头反馈。
            float remainingStrength = _t < _dur ? _mag * (1f - _t / _dur) : 0f;
            if (magnitude <= remainingStrength) return;
            _dur = duration;
            _mag = magnitude;
            _t = 0f;
            _seed = Random.value * 1000f;
        }

        private void Update()
        {
            if (_t < _dur)
            {
                _t += Time.deltaTime;
                float p = 1f - Mathf.Clamp01(_t / _dur);
                // Sample coherent noise instead of choosing an unrelated direction every frame.
                // This keeps impact shake smooth and guarantees a clean return to rest.
                float noiseTime = _seed + _t * 24f;
                float envelope = p * p * (3f - 2f * p);
                float x = Mathf.PerlinNoise(noiseTime, _seed + 17.3f) * 2f - 1f;
                float y = Mathf.PerlinNoise(_seed + 41.7f, noiseTime) * 2f - 1f;
                float scale = GameDatabase.Balance != null ?
                    Mathf.Clamp01(GameDatabase.Balance.screenShakeScale) : 0.75f;
                CurrentOffset = new Vector3(x, y, 0f) * (_mag * envelope * scale);
            }
            else
            {
                CurrentOffset = Vector3.zero;
            }
        }
    }
}
