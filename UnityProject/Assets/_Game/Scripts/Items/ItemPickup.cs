using RogueLike.Core;
using RogueLike.Data;
using RogueLike.Player;
using UnityEngine;

namespace RogueLike.Items
{
    /// <summary>掉落物拾取：进入玩家拾取半径后磁吸，接触即入账（ItemPool 管理，与子弹池独立）。</summary>
    public class ItemPickup : MonoBehaviour
    {
        private const float MagnetSpeed = 9f;
        private const float CollectDist = 0.3f;

        private int _value;
        private ObjectPool _pool;
        private bool _isExperience;
        private bool _collected;
        public int Value => _value;
        public bool IsExperience => _isExperience;

        public void Init(int value, ObjectPool pool, bool isExperience = false)
        {
            _value = value;
            _pool = pool;
            _isExperience = isExperience;
            _collected = false;
            ItemPool.Register(this);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing) return;

            Vector2 toPlayer = (Vector2)(gm.Player.transform.position - transform.position);
            float dist = toPlayer.magnitude;
            float pickupRange = gm.Player.Stats.Get(StatType.PickupRange);

            if (dist <= pickupRange)
            {
                Vector2 step = Vector2.MoveTowards(transform.position, gm.Player.transform.position, MagnetSpeed * Time.deltaTime);
                transform.position = step;
            }

            if (dist <= CollectDist) CollectNow();
        }

        /// <summary>玩家或拾取型武器共用的单次结算入口。</summary>
        public bool CollectNow()
        {
            if (_collected || !gameObject.activeInHierarchy) return false;
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.State != GameState.Playing) return false;
            if (_isExperience)
            {
                var xp = gm.Player.GetComponent<XpSystem>();
                if (xp == null) return false;
                _collected = true;
                xp.AddExp(_value);
            }
            else
            {
                _collected = true;
                gm.AddMaterials(_value);
            }
            Despawn();
            return true;
        }

        public void Despawn()
        {
            if (!gameObject.activeInHierarchy) return;
            _collected = true;
            ItemPool.Unregister(this);
            _pool?.Release(gameObject);
        }
    }
}
