using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace SoftKitty.InventoryEngine
{
    public class DragableUi : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public bool Dragable = true; //Enable dragging
        public RectTransform DragableRectTransform; //The RectTransform to Drag

        #region Variables
        private RectTransform _parentTransform;
        private bool _isDraging = false;
        private Vector2 _dragOffset;
        #endregion

        #region Internal Methods
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Dragable && eventData.button == PointerEventData.InputButton.Left)
            {
                if (GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceCamera)
                {
                    _dragOffset = DragableRectTransform.InverseTransformPoint(eventData.pointerCurrentRaycast.worldPosition) - DragableRectTransform.position;
                }
                else
                {
                    _dragOffset = DragableRectTransform.InverseTransformPoint(eventData.pointerCurrentRaycast.screenPosition) * DragableRectTransform.localScale.x;
                }
                _isDraging = true;
            }

        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_parentTransform) _parentTransform = DragableRectTransform.parent.GetComponent<RectTransform>();
            if (_isDraging && Dragable)
            {
                Vector2 localPosition = Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentTransform,
                    eventData.position,
                    GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceCamera?GetComponentInParent<Canvas>().worldCamera:null,
                    out localPosition);
                DragableRectTransform.position = _parentTransform.TransformPoint(localPosition - _dragOffset);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDraging = false;
        }
        #endregion

    }

}