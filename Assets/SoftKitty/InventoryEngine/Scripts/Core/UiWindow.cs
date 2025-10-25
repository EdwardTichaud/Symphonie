using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// Inherit from this class to make a window managed by WindowManager.cs.
    /// </summary>
    public class UiWindow : MonoBehaviour,IPointerDownHandler
    {
        #region Variables
        public KeyCode CloseKey = KeyCode.Escape;
        public bool Draggable = true;
        public bool FixedDefaultPosition = false;
        public Vector2 DefaultPosition;
        [HideInInspector]
        public bool ChildWindow = false;
        public delegate void OnWindowClose(InventoryHolder _key);
        private OnWindowClose closeCallback;
        private InventoryHolder windowKey;
        #endregion

        #region Internal Methods
        
        public void RegisterCloseCallback(OnWindowClose _callback, InventoryHolder _key)
        {
            windowKey = _key;
            closeCallback = _callback;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ActiveWindow();
        }

        public void ActiveWindow()
        {
            if (!ChildWindow)
            {
                transform.SetAsLastSibling();
                WindowsManager.ActiveWindow = this;
            }
        }
        #endregion

        #region MonoBehaviour
        private void OnEnable()
        {
            SoundManager.Play2D("MenuOn");
            if (GetComponentInChildren<DragableUi>()) GetComponentInChildren<DragableUi>().enabled = Draggable;
            if(FixedDefaultPosition) GetComponent<RectTransform>().anchoredPosition = DefaultPosition;
        }
        public virtual void Update()
        {
            if (!NumberInput.isVisible() && !ItemDragManager.isVisible() && InputProxy.GetKeyDown(CloseKey) && WindowsManager.ActiveWindow==this)
            {
                Close();
            }
        }
        #endregion


        public virtual void Close()//Close this window.
        {
            SoundManager.Play2D("MenuOff");
            if (closeCallback != null) closeCallback(windowKey);
            Destroy(gameObject);
        }
        public virtual void Initialize(InventoryHolder _inventoryHolder, InventoryHolder _equipHolder, string _name = "")//Initialize this interface
        {

        }
    }
}
