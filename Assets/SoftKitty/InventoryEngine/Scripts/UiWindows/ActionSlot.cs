using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class ActionSlot : MonoBehaviour
    {
        #region Variables
        public LinkIcon Item;
        public Image KeyBack;
        public Text KeyText;
        private ActionSlotData SlotData;
        private InventoryHolder Holder;
        int Number = 0;
        int Index = 0;
        bool inited = false;
        #endregion

        #region Internal Methods
        public void SetLock(bool _lock)
        {
            Item.Draggable = !_lock;
           
        }

        public void Initialize(int _index, ActionSlotData _data, InventoryHolder _holder)
        {
            inited = false;
            Index = _index;
            SlotData = _data;
            Holder = _holder;
            if (SlotData.itemId >= 0 && SlotData.itemId< ItemManager.instance.items.Count && ItemManager.TryGetItem(SlotData.itemId)!=null)
            {
                Item _item = ItemManager.TryGetItem(SlotData.itemId).Copy();
                _item.upgradeLevel = _data.upgradeLevel;
                _item.enchantments.Clear();
                _item.enchantments.AddRange(_data.enchantments);
                _item.socketedItems.Clear();
                _item.socketedItems.AddRange(_data.sockets);
                Number = 0;
                Item.Initialize(_item,false);
                Item.SetItemNumber(0);
                Item.SetHolder(Holder);
                Item.DoUpdate();
                Item.OnHolderItemChanged(new Dictionary<Item, int>());
            }
            else
            {
                Number = 0;
                Item.SetEmpty();
            }
            UpdateKey();
            Item.Group.alpha = (Number == 0 && SlotData.itemId >= 0) ? 0.3F : 1F;
            Item.LimitedByOwner = ItemManager.PlayerInventoryHolder;
            inited = true;
        }

        public void UpdateKey()
        {
            string _key = SlotData.key == KeyCode.None ? "?" : SlotData.key.ToString().Replace("Alpha", "").Replace("Left", "").Replace("Right", "").Replace("Mouse", "M");
            KeyText.text = _key.Substring(0, Mathf.Min(3, _key.Length));
        }

        void Update()
        {
            
            if (!inited) return;
            if (Number != Item.GetNumber() || SlotData.itemId != Item.GetItemId())
            {
                Number = Item.GetNumber();
                Item.Group.alpha = (Number == 0 && Item.GetItemId() >= 0) ? 0.3F : 1F;
                if (SlotData.itemId != Item.GetItemId())
                {
                    SlotData.itemId = Item.GetItemId();
                    SlotData.enchantments.Clear();
                    SlotData.sockets.Clear();
                    SlotData.upgradeLevel = 0;
                    if (Item.GetItem() != null)
                    {
                       
                        SlotData.upgradeLevel = Item.GetItem().upgradeLevel;
                        SlotData.enchantments.AddRange(Item.GetItem().enchantments);
                        SlotData.sockets.AddRange(Item.GetItem().socketedItems);
                    }
                    ActionBarUi.instance.Save();
                }
            }

            if (InputProxy.GetKeyDown(SlotData.key))
            {
                Use();
                ActionBarUi.instance.SetSelectedSlot(Index);
            }

        }
        #endregion
        public void Use() // Use the item in this slot.
        {
            if (Item.GetStackHolder() != null && SlotData.itemId >= 0 && Number > 0 )
            {
                int _index = ItemManager.PlayerInventoryHolder.GetItemIndex(Item.GetItemId(), Item.GetItem().upgradeLevel, Item.GetItem().enchantments, Item.GetItem().socketedItems);
                Item.GetStackHolder().UseItem(Item.GetItemId(), 1, _index);
                Item.Click();
            }
        }


    }
}
