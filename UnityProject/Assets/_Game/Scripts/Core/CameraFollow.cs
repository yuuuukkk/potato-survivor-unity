using UnityEngine;

namespace RogueLike.Core
{
    /// <summary>相机跟随：世界大、视野小（orthographicSize=5.2），平滑跟随主角移动。</summary>
    public class CameraFollow : MonoBehaviour
    {
        public float smooth = 8f;
        public Vector3 offset = new Vector3(0f, 0f, -10f);
        private Vector3 _followPosition;

        private void Start()
        {
            _followPosition = transform.position;
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return;
            Vector3 target = gm.Player.transform.position + offset;
            _followPosition = Vector3.Lerp(_followPosition, target, Mathf.Clamp01(smooth * Time.deltaTime));
            // 先钳制正常跟随位置；靠近场地边界时，将朝场外的震屏偏移反射回场内。
            // 否则最终钳制会把震屏完全吃掉，受击只在场地中央才看得出来。
            Vector3 basePosition = ClampToArena(_followPosition);
            Vector3 shake = CameraShake.Instance != null
                ? CameraShake.Instance.CurrentOffset : Vector3.zero;
            Vector3 position = ClampToArena(basePosition + shake);
            if (Mathf.Abs(position.x - basePosition.x) < 0.0001f && Mathf.Abs(shake.x) > 0.0001f)
                position.x = ClampToArena(basePosition - new Vector3(shake.x, 0f, 0f)).x;
            if (Mathf.Abs(position.y - basePosition.y) < 0.0001f && Mathf.Abs(shake.y) > 0.0001f)
                position.y = ClampToArena(basePosition - new Vector3(0f, shake.y, 0f)).y;
            transform.position = position;
        }

        private Vector3 ClampToArena(Vector3 position)
        {
            var camera = GetComponent<Camera>();
            if (camera == null || !camera.orthographic) return position;

            Rect bounds = ArenaBounds.Rect;
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;

            // 视野比场地还大时，该轴固定在场地中心；否则边缘始终与视口对齐，
            // 不会随着玩家继续移动而把场外区域和天空盒带进画面。
            position.x = bounds.width <= halfWidth * 2f
                ? bounds.center.x
                : Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            position.y = bounds.height <= halfHeight * 2f
                ? bounds.center.y
                : Mathf.Clamp(position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
            return position;
        }
    }
}
