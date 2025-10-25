using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class WindowsManager : MonoBehaviour
    {
        /// <summary>
        /// Returns true if there is any UI window exists. 
        /// </summary>
        public static bool anyWindowExists
        {
            get
            {
                return windowsDic.Keys.Count > 0;
            }
        }

        #region Variables
        public static UiWindow ActiveWindow;
        private static WindowsManager instance;
        private static Dictionary<InventoryHolder, UiWindow> windowsDic = new Dictionary<InventoryHolder, UiWindow>();
        #endregion

        #region Internal Methods
        void Awake()
        {
            instance = this;
            windowsDic.Clear();
        }

        IEnumerator ActiveWindowLater(UiWindow _window)
        {
            yield return 1;
            if(_window!=null)_window.ActiveWindow();
        }

        public static GameObject GetMainCanvas(GameObject _base=null)
        {
            if (ItemManager.instance.CanvasTag.Replace(" ", "").Length > 0)
            {
                Canvas[] _canvas = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
                foreach (var obj in _canvas) {
                    if (obj.gameObject.tag == ItemManager.instance.CanvasTag) return obj.gameObject;
                }
                Debug.LogError("[InventoryEngine] Could not find Canvas GameObject with the tag you specified!");
            }
            //If it fails to get canvas with the specified tag or the tag is null, search canvas from the parent of the base gameObject.
            if (_base != null && _base.GetComponentInParent<CanvasScaler>()) return _base.GetComponentInParent<CanvasScaler>().gameObject;
            //If there is no base gameObject, or could not find root canvas from the parents, then search canvas from the scene.
            //The reason we use <CanvasScaler> instead of <Canvas> is becuase Canvas can be nested, we only aiming to find the root canvas which usually come with <CanvasScaler> together.
            if (FindFirstObjectByType<CanvasScaler>()!=null) return FindFirstObjectByType<CanvasScaler>().gameObject;
            Debug.LogError("[InventoryEngine] Could not find Canvas GameObject in your scene! Creating one...");
            //If there is no canvas at all in the scene, we're going to create one.
            GameObject _tempCanvasObj = new GameObject("InventoryEngineCanvas");
            Canvas _tempCanvas= _tempCanvasObj.AddComponent<Canvas>();
            _tempCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (ItemManager.instance.CanvasTag.Replace(" ", "").Length > 0) _tempCanvasObj.tag = ItemManager.instance.CanvasTag;
            CanvasScaler _tempScaler= _tempCanvasObj.AddComponent<CanvasScaler>();
            _tempScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _tempScaler.referenceResolution = new Vector2(1920F,1080F);
            _tempScaler.matchWidthOrHeight = 1F;
            _tempCanvasObj.AddComponent<GraphicRaycaster>();
            return _tempCanvasObj;
        }


        public UiWindow OpenWindow(string _prefabName, InventoryHolder _holder, bool _bringToTop = true)
        {
            if (windowsDic.ContainsKey(_holder))
            {
                windowsDic[_holder].Close();
                return null;
            }

            GameObject _newWindow = Instantiate(Resources.Load<GameObject>("InventoryEngine/UiWindows/" + _prefabName), transform);
            if (!_newWindow.GetComponent<UiWindow>().FixedDefaultPosition)
            {
                float _width = _newWindow.GetComponent<RectTransform>().sizeDelta.x * 0.5F;
                float _screenWidth = GetMainCanvas(_newWindow).GetComponent<RectTransform>().sizeDelta.x * 0.5F;
                _newWindow.transform.localPosition = new Vector3(Mathf.Clamp(-450F + (windowsDic.Count + 1) * 50F, _width - _screenWidth + 270F, _screenWidth - _width)
                    , _newWindow.transform.localPosition.y - windowsDic.Count * 50F, 0F);
            }
            windowsDic.Add(_holder, _newWindow.GetComponent<UiWindow>());
            _newWindow.GetComponent<UiWindow>().RegisterCloseCallback(OnWindowClose, _holder);
            if (_bringToTop) _newWindow.GetComponent<UiWindow>().ActiveWindow();
            return _newWindow.GetComponent<UiWindow>();
        }

        public void OnWindowClose(InventoryHolder _key)
        {
            if (windowsDic.ContainsKey(_key)) windowsDic.Remove(_key);
            InventoryHolder lastKey = null;
            foreach (var key in windowsDic.Keys)
            {
                if (windowsDic[key] != null) lastKey = key;
            }
            if (lastKey != null) StartCoroutine(ActiveWindowLater(windowsDic[lastKey]));
        }
        #endregion

        /// <summary>
        /// Close all active windows
        /// </summary>
        public static void CloseAllWindows()
        {
            List<UiWindow> _windows = new List<UiWindow>();
            foreach (var obj in windowsDic.Keys) _windows.Add(windowsDic[obj]);
            windowsDic.Clear();
            foreach (var obj in _windows) obj.Close();
        }



        /// <summary>
        /// Open an window by the ui prefab name and the InventoryHolder component, you can find the prefabs in SoftKitty/InventoryEngine/Resources/InventoryEngine/UiWindows/
        /// </summary>
        /// <param name="_prefabName"></param>
        /// <param name="_holder"></param>
        /// <param name="_bringToTop"></param>
        /// <returns></returns>
        public static UiWindow GetWindow(string _prefabName,InventoryHolder _holder, bool _bringToTop=true)
        {
            if (instance == null)
            {
                GameObject newObj = Instantiate(Resources.Load<GameObject>("InventoryEngine/WindowsManager"),GetMainCanvas().transform);
                newObj.transform.SetAsLastSibling();
                newObj.transform.localPosition = Vector3.zero;
                newObj.transform.localScale = Vector3.one;
                instance = newObj.GetComponent<WindowsManager>();
            }
            return instance.OpenWindow(_prefabName, _holder, _bringToTop);
        }

        /// <summary>
        /// Check if the window of an InventoryHolder is opened
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public static bool isWindowOpen(InventoryHolder _key)
        {
            if (_key == null) return false;
            return windowsDic.ContainsKey(_key);
        }

        /// <summary>
        /// Returns the window class by an InventoryHolder as the key.
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public static UiWindow getOpenedWindow(InventoryHolder _key)
        {
            if (_key == null) return null;
            if (windowsDic.ContainsKey(_key))
                return windowsDic[_key];
            else
                return null;
        }

       

    }
}
