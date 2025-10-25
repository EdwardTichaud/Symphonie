using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace SoftKitty.InventoryEngine
{
    public class UiStyle : MonoBehaviour
    {
        #region Variables
        [HideInInspector]
        public bool ApplyStyle = false;
        public GraphicsSet[] References;
        public RectSetting[] Rects;
        #endregion


        #region Internal Methods
        public void UpdatePrefab()
        {
         #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
         #endif
        }

        public void SetColor(int _index, Color _color)
        {
            //if (Application.isPlaying) return;
            References[_index].color = _color;
            foreach (MaskableGraphic obj in References[_index].graphics) {
                obj.color = new Color(_color.r, _color.g, _color.b,obj.color.a);
            }
        }

        public void SetVisible(int _index,bool _visible)
        {
            //if (Application.isPlaying) return;
            if (References[_index].visibleAdjustable)
            {
                References[_index].visible = _visible;
                foreach (MaskableGraphic obj in References[_index].graphics)
                {
                    obj.gameObject.SetActive(_visible);
                }
            }
        }

        public void SetWidth(int _index, float _value)
        {
            if (_index >= Rects.Length) return;
            Rects[_index].widthLerp = _value;
            foreach (RectSet obj in Rects[_index].set)
            {
                if (Rects[_index].widthAdjustable) obj.rect.sizeDelta = new Vector2(Mathf.Lerp(obj.widthMinMax.x, obj.widthMinMax.y, Rects[_index].widthLerp), obj.rect.sizeDelta.y);
            }
        }

        public void SetHeight(int _index, float _value)
        {
            if (_index>=Rects.Length) return;
            Rects[_index].heightLerp = _value;
            foreach (RectSet obj in Rects[_index].set)
            {
                if (Rects[_index].heightAdjustable) obj.rect.sizeDelta = new Vector2(obj.rect.sizeDelta.x,Mathf.Lerp(obj.heightMinMax.x, obj.heightMinMax.y, Rects[_index].heightLerp));
            }
        }
        #endregion
    }
}
