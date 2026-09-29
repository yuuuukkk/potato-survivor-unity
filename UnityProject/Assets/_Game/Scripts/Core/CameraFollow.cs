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
            // 叠加屏幕震动偏移（CameraShake 只输出偏移、不再直接改相机位置）
            Vector3 position = _followPosition + (CameraShake.Instance != null
                ? CameraShake.Instance.CurrentOffset : Vector3.zero);
            transform.position = ClampToArena(position);
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
