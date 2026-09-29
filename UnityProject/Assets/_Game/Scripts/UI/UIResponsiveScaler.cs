using UnityEngine;
using UnityEngine.UI;

namespace RogueLike.UI
{
    /// <summary>始终按单一比例缩放画布；窄屏贴合宽度、宽屏贴合高度，避免弹窗裁切。</summary>
    public sealed class UIResponsiveScaler : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private float _lastAspect = -1f;

        private void Awake() => _scaler = GetComponent<CanvasScaler>();

        private void Update()
        {
            if (_scaler == null || Screen.height <= 0) return;
            float aspect = (float)Screen.width / Screen.height;
            if (Mathf.Abs(aspect - _lastAspect) < 0.001f) return;
            _lastAspect = aspect;
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1280f, 720f);
            _scaler.matchWidthOrHeight = aspect < 1280f / 720f ? 0f : 1f;
        }
    }
}
