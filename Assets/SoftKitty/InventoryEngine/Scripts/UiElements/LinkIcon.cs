using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SoftKitty.InventoryEngine
{
    public class LinkIcon : ItemIcon
    {
        [Tooltip("Determines if this item slot is draggable.")]
        public bool Draggable = true;
        [Tooltip("When set to true, the shortcut will automatically re-link to the highest upgrade level of the same item from the player's inventory.")]
        public bool AutoLinkToHighestUpgradeLevel=true;
        [Tooltip("If this item slot only accepts items with a specific tag, specify that tag here.")]
        public string LimitedByTag = "";
        [Tooltip("If this item slot only accepts items from a specific owner, specify that owner here.")]
        public InventoryHolder LimitedByOwner;
        [Tooltip("Whether this item slot only accepts useable items.")]
        public bool OnlyRecieveUseableItem = true;

        #region Variables
        private InventoryHolder Holder;
        private float mouseDownTime = 0F;
        private Vector3 mouseDownPos;
        private bool isDragging = false;
        private bool waittingForDrop = false;
        private Item LinkedItem;
        private int ItemNumber = 0;
        private int ItemLinkId = 0;
        public delegate void OnItemLinked(int _index, Item _item);
        public Image CoolDownMask;
        public Text CoolDownText;
        protected OnItemLinked LinkedCallback;
        #endregion

        #region Internal Methods
        public void OnHolderItemChanged(Dictionary<Item, int> _changedItems)
        {
            if (itemId >= 0)
            {
                ItemNumber = Holder.GetItemNumberWithHighestUpgradeLevel(itemId);
                if (AutoLinkToHighestUpgradeLevel) LinkedItem.upgradeLevel = Holder.GetHighestUpgradeLevel(itemId);
            }
        }
        public override void OnHover()
        {
            if (HoverInfoAnchorPoint != null && !Empty) HoverInformation.ShowHoverInfo(this, LinkedItem, ItemNumber, HoverInfoAnchorPoint, PriceMultiplier, RightClickActionHint,false,false,false);
        }
        public override void ResetState()
        {
            SetItemId(-2);
            number = 0;
            upgrade = -1;
            isDragging = false;
            mouseDownTime = 0F;
            mouseDownPos = InputProxy.mousePosition;
            ToggleOutline(false);
            SetFavorate(false);
        }

        public override void DoUpdate()
        {
            if (!inited) return;
            StateUpdate();
            if (!Enabled) return;
            CheckDropping();
            if (itemId < 0) return;
            CheckDragging();
            CheckCoolDown();
        }

        private void CheckCoolDown()
        {
            if (!Empty && itemId >= 0 && number > 0 && CoolDownMask && CoolDownText)
            {
                float _cd = ItemManager.TryGetItem(itemId).GetCoolDownTime();
                if (_cd > 0F )
                {
                    if (CoolDownMask.gameObject.activeSelf != ItemManager.TryGetItem(itemId).isCoolDown()) CoolDownMask.gameObject.SetActive(ItemManager.TryGetItem(itemId).isCoolDown());
                    if (ItemManager.TryGetItem(itemId).isCoolDown())
                    {
                        CoolDownMask.fillAmount = ItemManager.TryGetItem(itemId).GetRemainCoolDownTime() / _cd;
                        if (ItemManager.TryGetItem(itemId).GetRemainCoolDownTime() >= 1F)
                        {
                            CoolDownText.text = Mathf.CeilToInt(ItemManager.TryGetItem(itemId).GetRemainCoolDownTime()).ToString();
                        }
                        else
                        {
                            CoolDownText.text = ItemManager.TryGetItem(itemId).GetRemainCoolDownTime().ToString("0.0");
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            UnLinkHolder();
        }

        private void StateUpdate()
        {
            if (!Empty)
            {
                if (itemId != LinkedItem.uid)
                {
                    SetAppearance(LinkedItem.GetIcon(), LinkedItem.GetTypeColor(), LinkedItem.GetQualityColor(), true, true);
                }
                if (number != ItemNumber) SetItemNumber(ItemNumber);
                if (upgrade != LinkedItem.upgradeLevel) SetUpgradeLevel(LinkedItem.upgradeLevel);
                if (Fav.activeSelf != LinkedItem.favorite) SetFavorate(LinkedItem.favorite);
                SetItemId(LinkedItem.uid);
            }
            else
            {
                if (itemId >= 0)
                {
                    SetEmpty();
                }
            }
        }

        private void CheckDropping()
        {
            if (ItemDragManager.isDragging)
            {
                waittingForDrop = true;
            }
            else
            {
                if (waittingForDrop && CanBeLinked && (ItemDragManager.DraggingSource.Type == IconType.Item || ItemDragManager.DraggingSource.Type == IconType.Link))
                {
                    if (Vector3.Distance(ItemDragManager.DropPos, transform.position) < Mathf.Min(Rect.sizeDelta.x * 0.48F, Rect.sizeDelta.y * 0.48F) * Rect.lossyScale.x)
                    {
                        ItemDragManager.DropReceived = true;
                        if (ItemDragManager.DraggingSource.GetStackHolder() != null)
                        {
                            ItemIcon _source = (ItemIcon)ItemDragManager.DraggingSource;
                            InventoryItem _itemSource = ItemDragManager.DraggingSource.GetComponent<InventoryItem>();
                            if (LimitedByTag.Length > 0 && !_source.isTagMatchText(LimitedByTag))
                            {
                                DynamicMsg.PopMsg(ItemManager.instance.msgItemAssign);
                            }
                            else if (LimitedByOwner != null && _itemSource!=null && _itemSource.Holder != LimitedByOwner)
                            {
                                DynamicMsg.PopMsg(ItemManager.instance.msgItemAssign);
                            }
                            else if (ItemDragManager.DraggingSource.Type == IconType.Link)
                            {
                                SwapLink((LinkIcon)ItemDragManager.DraggingSource);
                                Icon.transform.localScale = Vector3.one * 0.5F;
                                ItemDragManager.DraggingSource.Icon.transform.localScale = Vector3.one * 0.5F;
                            }
                            else if (ItemDragManager.DraggingSource.Type == IconType.Item)
                            {
                                if (ItemDragManager.DraggingSource.GetItem().useable || !OnlyRecieveUseableItem)
                                    LinkItem(ItemDragManager.DraggingSource.GetStackHolder(), ItemDragManager.DraggingSource.GetItem(), ItemDragManager.DraggingSource.GetNumber(), ItemDragManager.DraggingSource.isEmpty());
                            }
                        }
                    }
                }
                waittingForDrop = false;
            }
        }

        public override void EndDrag(int _add)
        {
            StartCoroutine(EndDragCo(_add));
        }
        IEnumerator EndDragCo(int _add)
        {
            yield return 2;
            if (!ItemDragManager.DropReceived)
            {

                if (!EventSystem.current.IsPointerOverGameObject())
                {
                    SetHolder(null);
                    Empty = true;
                }
            }
            ItemDragManager.DropReceived = true;
        }

        private void CheckDragging()
        {
            if (ItemDragManager.isDragging || !Draggable) return;
            if (isHover)
            {
                if (InputProxy.GetMouseButtonDown(0))
                {
                    mouseDownPos = InputProxy.mousePosition;
                    isDragging = true;
                }
                if (InputProxy.GetMouseButton(0))
                {
                    mouseDownTime += Time.unscaledDeltaTime;
                }
                else
                {
                    mouseDownTime = 0F;
                }

            }
            else
            {
                mouseDownTime = 0F;
            }

            if (isDragging)
            {
                if (InputProxy.GetMouseButton(0))
                {
                    if (mouseDownTime > 0.5F || Vector3.Distance(InputProxy.mousePosition, mouseDownPos) > 40F)
                    {
                        isDragging = false;
                        ItemDragManager.StartDragging(this, GetComponent<RectTransform>());
                    }
                }
                else
                {
                    isDragging = false;
                }
            }
        }
        #endregion

        /// <summary>
        /// Retrieves the InventoryHolder of this slot.
        /// </summary>
        /// <returns></returns>
        public override InventoryHolder GetStackHolder()
        {
            return Holder;
        }

        /// <summary>
        /// Retrieves the Item in this slot.
        /// </summary>
        /// <returns></returns>
        public override Item GetItem()
        {
            return Empty?null: LinkedItem;
        }

        /// <summary>
        /// Initialize by an item
        /// </summary>
        /// <param name="_item"></param>
        /// <param name="_empty"></param>
        public void Initialize(Item _item,bool _empty)
        {
            ResetState();
            LinkedItem = _item;
            Empty = _empty;
            inited = true;
        }

        /// <summary>
        /// Sets the InventoryHolder for this slot.
        /// </summary>
        /// <param name="_holder"></param>
        public void SetHolder(InventoryHolder _holder)
        {
            UnLinkHolder();
            Holder = _holder;
            if (Holder != null) Holder.RegisterItemChangeCallback(OnHolderItemChanged);
        }

        /// <summary>
        /// Disconnect from the linked InventoryHolder.
        /// </summary>
        public void UnLinkHolder()
        {
            if (Holder != null) Holder.UnRegisterItemChangeCallback(OnHolderItemChanged);
        }

        /// <summary>
        /// Registers a callback for when this icon is linked with an item.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterLinkedCallback(int _index, OnItemLinked _callback)
        {
            ItemLinkId = _index;
            LinkedCallback = _callback;
        }


        /// <summary>
        /// Swaps the linked item with another LinkIcon.
        /// </summary>
        /// <param name="_source"></param>
        public void SwapLink(LinkIcon _source)
        {
            Item _item = _source.LinkedItem.Copy();
            int _num = _source.ItemNumber;
            InventoryHolder _holder = _source.Holder;
            bool _empty = _source.Empty;
            if (LinkedItem != null && itemId>=0)
            {
                _source.LinkItem(Holder, LinkedItem.Copy(), ItemNumber, Empty);
            }
            else
            {
                _source.SetEmpty();
            }
            LinkItem(_holder, _item, _num, _empty);
        }

        /// <summary>
        /// Links this icon with the provided InventoryHolder and item.
        /// </summary>
        /// <param name="_holder"></param>
        /// <param name="_item"></param>
        /// <param name="_num"></param>
        /// <param name="_empty"></param>
        public void LinkItem(InventoryHolder _holder, Item _item, int _num, bool _empty)
        {
            inited = false;
            if (_item != null)
            {
                ResetState();
                LinkedItem = _item.Copy();
                SetHolder(_holder);
                Empty = _empty;
            }
            inited = true;
            StateUpdate();
            OnHolderItemChanged(new Dictionary<Item, int>());
            if (LinkedCallback!=null) LinkedCallback(ItemLinkId, _item);
        }



    }
}
