using RogueLike.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RogueLike.UI
{
    /// <summary>Brief, reversible visual response for pointer and focused button interaction.</summary>
    public sealed class ButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        private RectTransform _target;
        private Vector3 _restScale = Vector3.one;
        private float _pressAmount;
        private float _clickPulse;
        private bool _hovered;
        private bool _selected;
        private UnityEngine.UI.Button _button;
        private UIThemeSO _theme;

        public void Configure(RectTransform target, Vector3 restScale)
        {
            _target = target;
            _restScale = restScale;
            _button = GetComponent<UnityEngine.UI.Button>();
            _theme = Resources.Load<UIThemeSO>("Config/UITheme");
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; _pressAmount = 0f; }
        public void OnPointerDown(PointerEventData eventData) => _pressAmount = 1f;
        public void OnPointerUp(PointerEventData eventData) => _pressAmount = 0f;
        public void OnSelect(BaseEventData eventData) => _selected = true;
        public void OnDeselect(BaseEventData eventData) { _selected = false; _pressAmount = 0f; }
        public void PlayClick() => _clickPulse = 1f;

        private void OnDisable()
        {
            _pressAmount = 0f;
            _clickPulse = 0f;
            _hovered = false;
            _selected = false;
            if (_target != null) _target.localScale = _restScale;
        }

        private void Update()
        {
            if (_target == null) return;
            if (_theme != null && _theme.reduceUiMotion)
            {
                _target.localScale = _restScale;
                return;
            }
            _clickPulse = Mathf.MoveTowards(_clickPulse, 0f, Time.unscaledDeltaTime * 5f);
            bool interactive = _button == null || _button.interactable;
            float hover = _theme != null ? _theme.buttonHoverScale : 1.045f;
            float pressed = _theme != null ? _theme.buttonPressedScale : 0.95f;
            float scale = interactive && (_hovered || _selected) ? hover : 1f;
            if (interactive && _pressAmount > 0f) scale = pressed;
            scale += Mathf.Sin(_clickPulse * Mathf.PI) * 0.035f;
            float response = _theme != null ? _theme.buttonResponseSpeed : 18f;
            _target.localScale = Vector3.Lerp(_target.localScale, _restScale * scale,
                1f - Mathf.Exp(-Mathf.Max(1f, response) * Time.unscaledDeltaTime));
        }
    }
}
