using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace SoftKitty.InventoryEngine
{
    public class LootUi : ContainerBase
    {
        #region Variables
        public CurrenyInfo CurrencyPrefab;
        private List<CurrenyInfo> currencyList = new List<CurrenyInfo>();
        private float checkTime = 0F;
        #endregion

        #region MonoBehaviour
        public override void Update()
        {
            base.Update();
            checkTime = Mathf.MoveTowards(checkTime, 1F, Time.unscaledDeltaTime * 2F);
            if (checkTime >= 1F)
            {
                checkTime = 0F;
                if (isEmpty()) Close();
            }
        }
        #endregion
        
        public static LootUi ShowLoot(InventoryHolder _holder)//Show the loot window
        {
            UiWindow _window = WindowsManager.GetWindow("Loot", _holder, true);
            _window.GetComponent<LootUi>().PopLoot(_holder);
            return _window.GetComponent<LootUi>();
        }

        public void PopLoot(InventoryHolder _holder)//Pop the window
        {
            Initialize(_holder, null);
            SoundManager.Play2D("LootPop");
        }

        public override void Initialize(InventoryHolder _inventoryHolder, InventoryHolder _equipHolder, string _name = "Loot")//Initialize the loot window
        {
            base.Initialize(_inventoryHolder, _equipHolder, _name);
            GetComponent<RectTransform>().sizeDelta = new Vector2(328F,122F+68F*Mathf.CeilToInt(Items.Length/4F));
            for (int i = 0; i < Holder.Currency.Count; i++)
            {
                if (Holder.Currency.GetCurrency(i,false) > 0)
                {
                    GameObject _newItem = Instantiate(CurrencyPrefab.gameObject, CurrencyPrefab.transform.parent);
                    _newItem.transform.localScale = Vector3.one;
                    _newItem.SetActive(true);
                    _newItem.GetComponent<CurrenyInfo>().Initialize(i, Holder);
                    currencyList.Add(_newItem.GetComponent<CurrenyInfo>());
                }
            }
            inited = true;
        }

        public override void OnItemClick(int _index, int _button)//Callback for when player click an item
        {
            LootItem(_index);
            SoundManager.Play2D("Loot");
        }

        private void LootItem(int _index)//Move this item to player's inventory
        {
            if (!Items[_index].isEmpty() && Items[_index].GetItem()!=null)
            {
                InventoryStack _leftStack = ItemManager.PlayerInventoryHolder.AddItem(Items[_index].GetItem(), Items[_index].GetNumber());
                if (!_leftStack.isEmpty())
                {
                    if(_leftStack.Number< Items[_index].GetNumber()) DynamicMsg.PopItem(Items[_index].GetItem(), Items[_index].GetNumber()- _leftStack.Number);
                    DynamicMsg.PopMsg(ItemManager.instance.msgBagFull);
                }
                else
                {
                    DynamicMsg.PopItem(Items[_index].GetItem(), Items[_index].GetNumber());
                }
                Items[_index].Copy(_leftStack);
                checkTime = 0F;
            }
        }

        public void CollectAll()//Move all items and currencies to player's inventory
        {
            for (int i=0;i< Items.Length;i++) {
                LootItem(i);
            }
            foreach (var obj in currencyList) {
                CollectCurrency(obj);
            }
            SoundManager.Play2D("Loot");
        }

        public void CollectCurrency(CurrenyInfo _info)//Move this currency to player's inventory
        {
            if (_info.GetCurrencyValue()>0) {
                ItemManager.PlayerInventoryHolder.AddCurrency(_info.GetCurrencyId(), _info.GetCurrencyValue());
                Holder.AddCurrency(_info.GetCurrencyId(),-_info.GetCurrencyValue());
                checkTime = 0F;
            }
            SoundManager.Play2D("ItemDrop");
        }

        public bool isEmpty()//Check if everything is collected
        {
            bool _empty = true;
            foreach (var obj in Holder.Stacks) {
                if (!obj.Empty) _empty = false;
            }
            foreach (var obj in currencyList)
            {
                if (obj.GetCurrencyValue() > 0) _empty = false;
            }
            return _empty;
        }

        private void OnDestroy()//Destroy LootPack prefab when this window is closed.
        {
            if (isEmpty()) if(Holder!=null)Destroy(Holder.gameObject);
        }

        
    }
}
