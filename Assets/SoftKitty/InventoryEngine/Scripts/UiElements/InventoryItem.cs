using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SoftKitty.InventoryEngine
{
    public class InventoryItem : ItemIcon
    {
        [Tooltip("Determines if this item slot is splittable.")]
        public bool Splittable = true;
        [Tooltip("Determines if this item slot is deletable.")]
        public bool Deletable = true;
        [Tooltip("Indicates whether this item slot can receive untradeable items. For example, merchant slots should have this set to false.")]
        public bool RecieveUntradeable = true;
        [Tooltip("Indicates whether this item slot can receive dragged items.")]
        public bool RecieveDragging = true;
        [Tooltip("If this item slot only accepts items with a specific tag, specify that tag here.")]
        public string LimitedByTag = "";
        [Tooltip("If this item slot only accepts items from a specific owner, specify that owner here.")]
        public InventoryHolder LimitedByOwner;
        [Tooltip("Whether this item slot can be drag.")]
        public bool Draggable = true;
       

        #region Variables
        [HideInInspector]
        public InventoryStack StackData;
        public delegate void OnItemChange(Item _item, int _changedNumber, InventoryItem _itemIcon);
        public Image CoolDownMask;
        public Text CoolDownText;
        private OnItemChange OnItemChangeCallback;
        private float mouseDownTime = 0F;
        private Vector3 mouseDownPos;
        private bool isDragging = false;
        private bool waittingForDrop = false;
        private bool firstUpdate = true;
        [HideInInspector]
        public InventoryHolder Holder;
        #endregion



        #region Internal Methods
        public override void EndDrag(int _add)
        {
            if (StackData.Item != null)
            {
                StartCoroutine(EndDragCo(_add));
            }
        }
        public override void OnHover()
        {
            if (HoverInfoAnchorPoint != null && !StackData.isEmpty()) HoverInformation.ShowHoverInfo(this, StackData.Item, StackData.Number, HoverInfoAnchorPoint, PriceMultiplier, RightClickActionHint,true,true,(Deletable && ItemManager.instance.AllowDropItem));
            if (ItemManager.instance.HighlightEquipmentSlotWhenHoverItem && !StackData.isEmpty() && (Holder.Type == InventoryHolder.HolderType.PlayerInventory || Holder.Type == InventoryHolder.HolderType.NpcInventory))
            {
                InventoryHolder _equipHolder = null;
                _equipHolder = InventoryHolder.GetInventoryHolderByType(Holder.gameObject, Holder.Type == InventoryHolder.HolderType.PlayerInventory? InventoryHolder.HolderType.PlayerEquipment: InventoryHolder.HolderType.NpcEquipment);
                if (_equipHolder != null && WindowsManager.getOpenedWindow(_equipHolder) != null && WindowsManager.getOpenedWindow(_equipHolder).GetComponent<EquipmentUi>())
                {
                    WindowsManager.getOpenedWindow(_equipHolder).GetComponent<EquipmentUi>().HighLightSlot(StackData.Item.tags);
                }
            }
           
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
                if (_cd > 0F)
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

        private void StateUpdate()
        {
            if (!StackData.isEmpty())
            {
                Empty = false;
                if ((number != StackData.Number || itemId != StackData.Item.uid) && OnItemChangeCallback != null && !firstUpdate)
                {
                    if(itemId>=0 && number>0 && itemId != StackData.Item.uid) OnItemChangeCallback(ItemManager.TryGetItem(itemId).Copy(), -number, this);
                    OnItemChangeCallback(StackData.Item, itemId != StackData.Item.uid?StackData.Number: StackData.Number-number, this);
                }
                if (number != StackData.Number) SetItemNumber(StackData.Number);
                if (upgrade != StackData.Item.upgradeLevel) SetUpgradeLevel(StackData.Item.upgradeLevel);
                if (Fav.activeSelf != StackData.Item.favorite) SetFavorate(StackData.Item.favorite);
                if (itemId != StackData.Item.uid)
                {
                    SetAppearance(StackData.Item.GetIcon(), StackData.Item.GetTypeColor(), StackData.Item.GetQualityColor(), true, true);
                    SetItemId(StackData.Item.uid);
                }
            }
            else
            {
                if (!Empty)
                {
                    int _uid = itemId;
                    int _number = -number;
                    SetEmpty();
                    if (OnItemChangeCallback != null && !firstUpdate && _uid >= 0) OnItemChangeCallback(ItemManager.TryGetItem(_uid).Copy(), _number, this);
                }

            }
            firstUpdate = false;
        }


        private void CheckDropping()
        {
            if (ItemDragManager.isDragging)
            {
                waittingForDrop = true;
            }
            else
            {
                if (waittingForDrop && ItemDragManager.DraggingSource.Type == IconType.Item && ItemDragManager.DraggingSource.Icon != Icon
                    && isHover && isVisible)
                {
                    waittingForDrop = false;
                    if (Vector3.Distance(ItemDragManager.DropPos, transform.position) < Mathf.Min(Rect.sizeDelta.x * 0.48F, Rect.sizeDelta.y * 0.48F)* Rect.lossyScale.x)
                    {
                        ItemDragManager.DropReceived = true;
                        InventoryItem _source = (InventoryItem)ItemDragManager.DraggingSource;
                        int _id = -1;
                        int _number = 0;
                        if (LimitedByOwner != null && _source.Holder != LimitedByOwner && _source.Holder!=Holder) return;
                        if (_source.LimitedByOwner != null && Holder != _source.LimitedByOwner && _source.Holder != Holder) return;
                        if ((Holder.Type == InventoryHolder.HolderType.PlayerEquipment || Holder.Type == InventoryHolder.HolderType.NpcEquipment) && !_source.GetItem().AbleToUse(Holder)) return;
                        if (GetItem()!=null && (_source.Holder.Type == InventoryHolder.HolderType.PlayerEquipment || _source.Holder.Type == InventoryHolder.HolderType.NpcEquipment) && !GetItem().AbleToUse(_source.Holder)) return;
                        if (LimitedByTag.Length > 0 && !_source.isTagMatchText(LimitedByTag)) return;
                        if (_source.LimitedByTag.Length > 0 && !isTagMatchText(_source.LimitedByTag) && !isEmpty()) return;
                        if (!RecieveDragging) return;
                        if (!RecieveUntradeable && !_source.isTradeable()) return;
                        if (ItemDragManager.SplitMode)
                        {
                            if (_source.Holder != Holder) _id = ItemDragManager.SplitData.Item.uid;
                            if ((itemId == ItemDragManager.SplitData.Item.uid && StackData.GetAvailableSpace() > 0) || itemId < 0)
                            {
                                _number = ItemDragManager.SplitData.Number;
                                InventoryStack _temp = StackData.Merge(ItemDragManager.SplitData);
                                _source.StackData.Number += _temp.Number;
                                _number -= _temp.Number;
                            }
                            else
                            {
                                _number = ItemDragManager.SplitData.Number;
                                _source.StackData.Number += ItemDragManager.SplitData.Number;
                                _source.Copy(StackData.Merge(_source.StackData));
                            }
                        }
                        else
                        {
                            if (_source.Holder != Holder) _id = _source.StackData.Item.uid;
                            _number = _source.StackData.Number;
                            _source.Copy(StackData.Merge(_source.StackData));
                        }

                        SetHolderChanged(_id, _number);
                        if (_source.Holder != Holder) _source.SetHolderChanged(_id, -_number);

                        Icon.transform.localScale = Vector3.one * 0.5F;
                        _source.Icon.transform.localScale = Vector3.one * 0.5F;
                        RecalculateWeight();
                        _source.RecalculateWeight();
                    }
                }
            }
        }

        IEnumerator EndDragCo(int _add)
        {
            yield return 2;
            if (!ItemDragManager.DropReceived)
            {
                int _id = StackData.GetItemId();
                int _number = StackData.Number;
                if (_add > 0)
                {
                    if (EventSystem.current.IsPointerOverGameObject() || !Deletable || !ItemManager.instance.AllowDropItem || !StackData.Item.deletable)
                    {
                        _id = -1;
                        StackData.AddNumber(_add);
                    }
                    else
                    {
                        _number = _add;
                        ItemDragManager.PlayDeleteAnimation(this, ItemDragManager.DropPos, _add);
                        if (Holder != null && Holder.mItemDropCallback != null) Holder.mItemDropCallback(StackData.Item != null ? StackData.Item.Copy() : ItemManager.TryGetItem(_id).Copy(), _number);
                        RecalculateWeight();
                        SetHolderChanged(_id, -_number);
                    }
                }
                else
                {
                    if (!EventSystem.current.IsPointerOverGameObject() && Deletable && ItemManager.instance.AllowDropItem)
                    {
                        ItemDragManager.PlayDeleteAnimation(this, ItemDragManager.DropPos);
                        if (Holder != null && Holder.mItemDropCallback != null) Holder.mItemDropCallback(StackData.Item != null ? StackData.Item.Copy() : ItemManager.TryGetItem(_id).Copy(), _number);
                        StackData.Delete();
                        RecalculateWeight();
                        SetHolderChanged(_id, -_number);
                    }
                   
                }
                
                ItemDragManager.DropReceived = true;
            }
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
                    if (isVisible && (mouseDownTime > 0.5F || Vector3.Distance(InputProxy.mousePosition, mouseDownPos) > 40F))
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
        /// Retrieves the InventoryStack data of this item.
        /// </summary>
        /// <returns></returns>
        public override InventoryStack GetStackData() 
        {
            return StackData;
        }

        /// <summary>
        /// Retrieves the InventoryHolder of this slot.
        /// </summary>
        /// <returns></returns>
        public override InventoryHolder GetStackHolder()
        {
            return Holder;
        }

        /// <summary>
        /// Registers a callback for when this item has changed.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterOnItemChangeCallback(OnItemChange _callback)
        {
            OnItemChangeCallback = _callback;
        }

        /// <summary>
        /// Unregisters the callback for when this item has changed.
        /// </summary>
        public void UnRegisterOnItemChangeCallback()
        {
            OnItemChangeCallback = null;
        }

        /// <summary>
        /// Pop up the split interface to split this item into two stacks.
        /// </summary>
        public void Split()
        {
            if (StackData.isEmpty() || StackData.Number <= 1 || !Splittable || !isVisible) return;
            NumberInput.GetNumber(StackData.Number, StackData.Number, Rect, Split);
        }

        /// <summary>
        /// Mark this item as Favorite item.
        /// </summary>
        public void MarkFav()
        {
            if (StackData.isEmpty()) return;
            StackData.Item.favorite = !StackData.Item.favorite;
            SetFavorate(StackData.Item.favorite);
            Holder.CheckAutoSave();
        }

        /// <summary>
        /// Drop this item.
        /// </summary>
        public void Drop()
        {
            if (!Deletable || !ItemManager.instance.AllowDropItem || StackData.isEmpty()) return;
            int _id = StackData.GetItemId();
            int _number = StackData.Number;
            if (Holder != null && Holder.mItemDropCallback != null) Holder.mItemDropCallback(StackData.Item != null ? StackData.Item.Copy() : ItemManager.TryGetItem(_id).Copy(), _number);
            StackData.Delete();
            RecalculateWeight();
            SetHolderChanged(_id, -_number);
        }


        /// <summary>
        /// Retrieves the Item in this slot.
        /// </summary>
        /// <returns></returns>
        public override Item GetItem()
        {
            return StackData.Item;
        }

        /// <summary>
        /// Sets the InventoryHolder for this slot.
        /// </summary>
        /// <param name="_holder"></param>
        public void SetHolder(InventoryHolder _holder)
        {
            Holder = _holder;
        }

        /// <summary>
        /// If the item has changed, inform the holder to update its data.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_number"></param>
        public void SetHolderChanged(int _id, int _number)
        {
            Dictionary<Item, int> _changedItems = new Dictionary<Item, int>();
            if(_id!=-1) _changedItems.Add(ItemManager.TryGetItem(_id).Copy(),_number);
            if (Holder != null ) Holder.ItemChanged(_changedItems);
        }

        /// <summary>
        /// Inform the holder to recalculate the total weight
        /// </summary>
        public void RecalculateWeight()
        {
            if (Holder != null) Holder.CalWeight();
        }

        /// <summary>
        /// Initialize the data.
        /// </summary>
        /// <param name="_data"></param>
        public void Initialize(InventoryStack _data) 
        {
            ResetState();
            StackData = _data;
            inited = true;
        }

        /// <summary>
        /// Copy data from an InventoryStack.
        /// </summary>
        /// <param name="_data"></param>
        public void Copy(InventoryStack _data)
        {
            inited = false;
            ResetState();
            if (_data.Item != null)
                StackData.Item = _data.Item.Copy();
            else
                StackData.Item = null;
            StackData.Number = _data.Number;
            StackData.Empty = _data.Empty;
            inited = true;
        }

        /// <summary>
        /// Returns whether the item name matches the provided string.
        /// </summary>
        /// <param name="_name"></param>
        /// <returns></returns>
        public bool isNameMatch(string _name)
        {
            if (_name == "" || itemId < 0 || !inited || StackData.isEmpty()) return true;
            return StackData.Item.name.ToLower().Contains(_name.ToLower());
        }

        /// <summary>
        /// Returns whether the item category matches the provided category ID.
        /// </summary>
        /// <param name="_type"></param>
        /// <returns></returns>
        public bool isTypeMatch(int _type)
        {
            if (_type == 0 || itemId < 0 || !inited || StackData.isEmpty()) return true;
            return StackData.Item.type == _type - 1;
        }

        /// <summary>
        /// Returns whether the item can be traded. Returns _emptyResult when the slot is empty.
        /// </summary>
        /// <param name="_emptyResult"></param>
        /// <returns></returns>
        public bool isTradeable(bool _emptyResult=true)
        {
            if (itemId < 0 || !inited || StackData.isEmpty()) return _emptyResult;
            return StackData.Item.tradeable;
        }

        /// <summary>
        /// Returns whether the item can be used. Returns _emptyResult when the slot is empty.
        /// </summary>
        /// <param name="_emptyResult"></param>
        /// <returns></returns>
        public bool isUseable(bool _emptyResult = true)
        {
            if (itemId < 0 || !inited || StackData.isEmpty()) return _emptyResult;
            return StackData.Item.useable;
        }

        /// <summary>
        /// Returns whether the item can be consumed. Returns _emptyResult when the slot is empty.
        /// </summary>
        /// <param name="_emptyResult"></param>
        /// <returns></returns>
        public bool isConsumable(bool _emptyResult = true)
        {
            if (itemId < 0 || !inited || StackData.isEmpty()) return _emptyResult;
            return StackData.Item.consumable;
        }

        /// <summary>
        /// Returns whether the item can be deleted. Returns _emptyResult when the slot is empty.
        /// </summary>
        /// <param name="_emptyResult"></param>
        /// <returns></returns>
        public bool isDeletable(bool _emptyResult = true)
        {
            if (itemId < 0 || !inited || StackData.isEmpty()) return _emptyResult;
            return StackData.Item.deletable;
        }

        /// <summary>
        /// Splits the item in this slot.
        /// </summary>
        /// <param name="_result"></param>
        public void Split(int _result)
        {
            if (_result > 0)
            {
                if (_result < StackData.Number)
                {
                    ItemDragManager.StartDragging(this, GetComponent<RectTransform>(), _result);
                }
                else
                {
                    ItemDragManager.StartDragging(this, GetComponent<RectTransform>());
                }
            }
        }

        /// <summary>
        /// Set the number of the item.
        /// </summary>
        /// <param name="_number"></param>
        public void SetDataNumber(int _number)
        {
            StackData.Number = _number;
        }

    }
}
