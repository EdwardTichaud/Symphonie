
using UnityEngine.EventSystems;


namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// Disabled drag to scroll function for ScrollRect.
    /// </summary>
    public class ScrollViewNoDragging : UnityEngine.UI.ScrollRect
    {
        public override void OnBeginDrag(PointerEventData eventData) { }
        public override void OnDrag(PointerEventData eventData) { }
        public override void OnEndDrag(PointerEventData eventData) { }
    }
}
