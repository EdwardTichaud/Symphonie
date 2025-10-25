using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty
{
    /// <summary>
    /// This module displays short hint text in a small floating window, useful when the mouse hovers over buttons or icons. To use this module, simply add the <HintText> component to any UI element and fill in the hint text.
    /// </summary>
    public class HintManager : MonoBehaviour
    {
        #region Variables
        private static HintManager instance;
        public RectTransform Rect;
        public Text TextContent;
        public CanvasGroup HintBg;
        private float hintTime = 0F;
        private HintText hintScript;
        #endregion

        #region Internal Methods
        public void _showHint(string _text, HintText _hintScript)
        {
            TextContent.text = _text;
            hintScript = _hintScript;
            hintTime = 2F;
        }

        private void Update()
        {
            if (hintTime > 0F)
            {
                hintTime -= Time.unscaledDeltaTime;
                if (!HintBg.gameObject.activeSelf) HintBg.gameObject.SetActive(true);
                HintBg.alpha = Mathf.Clamp01(hintTime * 4F);
                HintBg.GetComponent<RectTransform>().sizeDelta = new Vector2(TextContent.rectTransform.sizeDelta.x + 50F, 35F);
                if (!hintScript.GetHover())
                {
                    hintTime = Mathf.Min(hintTime, 0.25F);
                }
                else
                {
                    HintBg.GetComponent<RectTransform>().position = TransferPos(MousePositionWithinScreenRect(), Rect, GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceCamera ? GetComponentInParent<Canvas>().worldCamera : null);
                }
            }
            else
            {
                if (HintBg.gameObject.activeSelf) HintBg.gameObject.SetActive(false);
            }
        }

        private Vector3 MousePositionWithinScreenRect()
        {
            Vector3 _pos = InputProxy.mousePosition;
            Vector2 _size = HintBg.GetComponent<RectTransform>().sizeDelta * 0.5F;
            _pos.x = Mathf.Clamp(_pos.x, _size.x * (Screen.width * 1F / 1920F), Screen.width- _size.x * (Screen.width * 1F/1920F));
            _pos.y = Mathf.Clamp(_pos.y, _size.y * (Screen.height * 1F / 1080F), Screen.height - (_size.y+35F)*(Screen.height * 1F / 1080F));
            return _pos;
        }

        #endregion

  
        /// <summary>
        /// Normally you don't need to call this, just attach <HintText.cs> to your ui elements, HintText.cs will call this method when mouse hover.
        /// </summary>
        /// <param name="_text"></param>
        /// <param name="_hintScript"></param>
        public static void ShowHint(string _text,HintText _hintScript)
        {
            if (instance == null)
            {
                GameObject newObj = Instantiate(Resources.Load<GameObject>("SoftKittyShared/HintManager"), _hintScript.GetComponentInParent<CanvasScaler>().transform);
                newObj.transform.SetAsLastSibling();
                newObj.transform.localPosition = Vector3.zero;
                newObj.transform.localScale = Vector3.one;
                instance = newObj.GetComponent<HintManager>();
            }
            instance._showHint(_text, _hintScript);
            instance.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Transfrom mouse position to rect position.
        /// </summary>
        /// <param name="_pos"></param>
        /// <param name="_parentTransform"></param>
        /// <param name="_camera"></param>
        /// <returns></returns>
        public static Vector3 TransferPos(Vector3 _pos, RectTransform _parentTransform,Camera _camera)
        {
            Vector2 localPosition = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentTransform,
                _pos,
                _camera,
                out localPosition);
            return _parentTransform.TransformPoint(localPosition);
        }

    }
}
