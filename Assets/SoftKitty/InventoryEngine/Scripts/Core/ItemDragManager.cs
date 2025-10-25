using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// This module is used to drag items around.
    /// </summary>
    public class ItemDragManager : MonoBehaviour
    {
        #region Variables
        public static bool isDragging = false;
        public static Vector3 DropPos;
        public ItemIcon DragItem;
        public RectTransform Rect;
        private static ItemDragManager instance;
        public static ItemIcon DraggingSource;
        public static bool DropReceived = true;
        public static bool SplitMode = false;
        public static InventoryStack SplitData;
        private bool playingDeleteAnimation = false;
        private float dropSpeed = 9.8F;
        private float rotateSpeed = 90F;
        #endregion

        #region MonoBehaviour
        void Start()
        {
            DragItem.Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
        }

        void Update()
        {
            if (playingDeleteAnimation)
            {
                dropSpeed += 100F * Time.unscaledDeltaTime;
                if (DragItem.transform.position.y > -2000F)
                {
                    DragItem.transform.position += Vector3.down * 120F * dropSpeed * Time.unscaledDeltaTime;
                    DragItem.transform.localEulerAngles = new Vector3(0F, 0F, DragItem.transform.localEulerAngles.z + Time.unscaledDeltaTime * rotateSpeed);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }
            else
            {
                dropSpeed = 9.8F;
            }
            if (isDragging)
            {
                if (!DragItem.gameObject.activeSelf) DragItem.gameObject.SetActive(true);
                DragItem.Rect.position = TransferPos(InputProxy.mousePosition, Rect, GetComponentInParent<Canvas>().renderMode== RenderMode.ScreenSpaceCamera? GetComponentInParent<Canvas>().worldCamera:null);
                if (InputProxy.GetMouseButtonUp(0))
                {
                    EndDrag();
                }
            }
            else
            {
                if (DragItem.gameObject.activeSelf) DragItem.gameObject.SetActive(false);
            }
        }
        #endregion

        #region Internal Methods
        public void BeginPlayDeleteAnimation(InventoryItem _source, Vector3 _pos, int _overrideNum)
        {
            DragItem.SetAppearance(_source.StackData.Item.GetIcon(), _source.StackData.Item.GetTypeColor(), _source.StackData.Item.GetQualityColor(), true, true);
            DragItem.SetUpgradeLevel(_source.StackData.Item.upgradeLevel);
            DragItem.SetItemNumber(_overrideNum != 0 ? _overrideNum : _source.StackData.Number);
            DragItem.SetFavorate(_source.Fav.activeSelf);
            DragItem.transform.position = _pos;
            DragItem.transform.localScale = Vector3.one * InventorySkin.instance.InventorySlotScale;
            DragItem.gameObject.SetActive(true);
            rotateSpeed = Random.Range(-180F, 180F);
            playingDeleteAnimation = true;
            SoundManager.Play2D("ItemDelete");
        }

        public void BeginDrag(ItemIcon _source, int _overrideNum)
        {
            playingDeleteAnimation = false;
            DragItem.transform.localEulerAngles = Vector3.zero;
            DragItem.transform.localScale = Vector3.one * InventorySkin.instance.InventorySlotScale;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(DragItem.gameObject);
            if (_overrideNum != 0)
            {
                SplitData = _source.GetStackData().Split(_overrideNum);
                DragItem.SetAppearance(SplitData.Item.GetIcon(), SplitData.Item.GetTypeColor(), SplitData.Item.GetQualityColor(), true, true);
                DragItem.SetUpgradeLevel(SplitData.Item.upgradeLevel);
                DragItem.SetItemNumber(SplitData.Number);
            }
            else
            {
                DragItem.SetAppearance(_source.GetItem().GetIcon(), _source.GetItem().GetTypeColor(), _source.GetItem().GetQualityColor(), true, true);
                DragItem.SetUpgradeLevel(_source.GetItem().upgradeLevel);
                DragItem.SetItemNumber(_source.GetNumber());
            }
            DragItem.SetFavorate(_source.Fav.activeSelf);
            DraggingSource = _source;
            DraggingSource.ToggleOutline(true);
            SoundManager.Play2D("ItemDrag");
        }

        private void EndDrag()
        {
            DropPos = DragItem.transform.position;
            DraggingSource.ToggleOutline(false);
            DropReceived = false;
            isDragging = false;
            DraggingSource.EndDrag(SplitMode ? SplitData.Number : 0);
            SoundManager.Play2D("ItemDrop");
            Destroy(gameObject);
        }
        #endregion

        /// <summary>
        /// Wehther the system is visible, meaning wehther something being dragged.
        /// </summary>
        /// <returns></returns>
        public static bool isVisible()
        {
            return instance!=null && (isDragging || !DropReceived);
        }

        /// <summary>
        /// Plays the delete animation for an item icon, causing the icon to fall to the bottom of the screen.
        /// </summary>
        /// <param name="_source"></param>
        /// <param name="_pos"></param>
        /// <param name="_overrideNum"></param>
        public static void PlayDeleteAnimation(InventoryItem _source,Vector3 _pos, int _overrideNum = 0)
        {
            if (instance == null)
            {
                GameObject newObj = Instantiate(Resources.Load<GameObject>("InventoryEngine/ItemDragManager"), WindowsManager.GetMainCanvas(_source.gameObject).transform);
                newObj.transform.SetAsLastSibling();
                newObj.transform.localPosition = Vector3.zero;
                newObj.transform.localScale = Vector3.one;
                instance = newObj.GetComponent<ItemDragManager>();
            }
            instance.BeginPlayDeleteAnimation(_source, _pos, _overrideNum);
        }

        /// <summary>
        /// Starts dragging an item. After the drag-and-drop action, the _source will be called by EndDrag().
        /// </summary>
        /// <param name="_source"></param>
        /// <param name="_rect"></param>
        /// <param name="_overrideNum"></param>
        public static void StartDragging(ItemIcon _source, RectTransform _rect,int _overrideNum=0)
        {
            if (instance == null)
            {
                GameObject newObj = Instantiate(Resources.Load<GameObject>("InventoryEngine/ItemDragManager"), WindowsManager.GetMainCanvas(_rect.gameObject).transform);
                newObj.transform.SetAsLastSibling();
                newObj.transform.localPosition = Vector3.zero;
                newObj.transform.localScale = Vector3.one;
                instance = newObj.GetComponent<ItemDragManager>();
            }
            SplitMode = (_overrideNum>0);
            instance.BeginDrag(_source, _overrideNum);
            DropPos = _source.transform.position;
            isDragging = true;
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
