using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SoftKitty
{

    public class HoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform _targetGraphic;
        public bool _usePosition = false;
        public Vector2 _hoverPosition = Vector2.zero;
        public bool _useAngle = false;
        public float _hoverAngle = 0F;
        public bool _useColor = false;
        public Color _hoverColor = new Color(1F, 1F, 1F, 1F);
        public bool _useScale = false;
        public Vector3 _hoverScale = Vector3.one;
        
        public string _soundHover = "bt_hover";
        public string _soundDown = "bt_down";
        public string _soundUp = "bt_up";

        #region Variables

        [HideInInspector]
        public bool isHover = false;
        Button mButton;
        Vector2 _oriPos;
        float _oriAngle;
        Color _oriColor;
        Color _oriColorSub;
        Vector3 _oriScale;
        bool inited = false;
        #endregion

        #region Internal Methods
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (mButton)
            {
                if (!mButton.interactable) return;
            }
            isHover = true;
            SoundManager.Play2D(_soundHover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHover = false;
        }

        void Start()
        {
            Init();
        }

        void OnEnable()
        {

            Init();
        }



        void Init()
        {
            if (inited)
            {
                ResetColor();
                return;
            }
            if (GetComponent<Button>()) mButton = GetComponent<Button>();
            if (_targetGraphic == null) _targetGraphic = GetComponent<RectTransform>();

            _oriPos = _targetGraphic.anchoredPosition;
            _oriAngle = _targetGraphic.localEulerAngles.z;
            _oriScale = _targetGraphic.localScale;
            _oriColor = GetColor(_targetGraphic);

            inited = true;
        }



        void OnDisable()
        {
            ResetColor();
        }

        void ResetColor()
        {
            if (!inited)
            {
                return;
            }
            isHover = false;
            if (_usePosition) _targetGraphic.anchoredPosition = _oriPos;
            if (_useAngle) _targetGraphic.localEulerAngles = new Vector3(0F, 0F, _oriAngle);
            if (_useScale) _targetGraphic.localScale = _oriScale;
            if (_useColor)
            {
                SetColor(_targetGraphic, _oriColor);
            }
        }

        void Update()
        {
            if (!inited) return;

            if (_usePosition) _targetGraphic.anchoredPosition = Vector2.Lerp(_targetGraphic.anchoredPosition, isHover ? _hoverPosition : _oriPos, Time.unscaledDeltaTime * 10F);
            if (_useAngle) _targetGraphic.localEulerAngles = new Vector3(0F, 0F, Mathf.Lerp(_targetGraphic.localEulerAngles.z, isHover ? _hoverAngle : _oriAngle, Time.unscaledDeltaTime * 10F));
            if (_useScale) _targetGraphic.localScale = Vector3.Lerp(_targetGraphic.localScale, isHover ? _hoverScale : _oriScale, Time.unscaledDeltaTime * 10F);
            if (_useColor)
            {
                SetColor(_targetGraphic, Color.Lerp(GetColor(_targetGraphic), isHover ? _hoverColor : _oriColor, Time.unscaledDeltaTime * 10F));
            }

        }

        public void ClickButton()
        {
            if (mButton) mButton.onClick.Invoke();
        }

        private Color GetColor(RectTransform _item)
        {
            if (_item.GetComponent<MaskableGraphic>())
            {
                return _item.GetComponent<MaskableGraphic>().color;
            }
            else
            {
                return Color.white;
            }
        }

        private void SetColor(RectTransform _item, Color _color)
        {
            if (_item.GetComponent<MaskableGraphic>())
            {
                _item.GetComponent<MaskableGraphic>().color = _color;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SoundManager.Play2D(_soundDown);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SoundManager.Play2D(_soundUp);
        }
        #endregion
    }
}
