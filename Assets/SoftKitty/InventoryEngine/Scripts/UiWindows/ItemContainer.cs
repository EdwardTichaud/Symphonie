using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace SoftKitty.InventoryEngine
{
    public class ItemContainer : ContainerBase
    {
        #region Variables
        public bool DynamicSizeByItemCount = false;
        public ListItem TabPrefab;
        public Text TotalNumText;
        public InputField SearchInput;
        public GameObject ClearFilterButton;
        public GameObject FavActiveIcon;
        private ListItem[] TabItems;
        private int selectedTab;
        private bool filterFav = false;
        private bool wasDragging = false;
        #endregion

        #region MonoBehaviour
        private void Start()
        {
            FavActiveIcon.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
        }
        public override void Update()
        {
            if (!inited) return;
            base.Update();
            CheckDragging();
        }
        #endregion
        
        public override void Initialize(InventoryHolder _inventoryHolder, InventoryHolder _equipHolder, string _name = "Inventory")//Initialize this interface
        {
            base.Initialize(_inventoryHolder, _equipHolder, _name);
            UpdateTotalNumber();
            List<ListItem> TabList = new List<ListItem>();
            selectedTab = 0;
            for (int i = 0; i < ItemManager.instance.itemTypes.Count + 1; i++)
            {
                if (i == 0 || ItemManager.instance.itemTypes[i - 1].visible)
                {
                    GameObject _newItem = Instantiate(TabPrefab.gameObject, TabPrefab.transform.parent);
                    _newItem.transform.localScale = Vector3.one;
                    _newItem.SetActive(true);
                    _newItem.GetComponent<ListItem>().mTexts[0].text = i == 0 ? "All" : (DynamicSizeByItemCount? ItemManager.instance.itemTypes[i - 1].name.Substring(0,1).ToUpper(): ItemManager.instance.itemTypes[i - 1].name);
                    _newItem.GetComponent<ListItem>().mObjects[0].SetActive(i == selectedTab);
                    _newItem.GetComponent<ListItem>().mID = TabList.Count;
                    TabList.Add(_newItem.GetComponent<ListItem>());
                }
            }
            if (DynamicSizeByItemCount)
            {
                int _w = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Holder.InventorySize))-5,0,4);
                int _h = Mathf.Clamp(Mathf.CeilToInt(Holder.InventorySize * 1F / (_w+5))-3,0,5);
                GetComponent<RectTransform>().sizeDelta = new Vector2(390F+67F* _w,268F+67F* _h);
            }

            float _width = (GetComponent<RectTransform>().sizeDelta.x - 42F - 90F) / (TabList.Count - 1);
            for (int i = 0; i < TabList.Count; i++)
            {
                TabList[i].GetComponent<RectTransform>().sizeDelta = new Vector2(i == 0 ? 90F : _width, 44F);
            }
            TabItems = TabList.ToArray();
            
            inited = true;
        }

        public override void OnItemClick(int _index, int _button)//Callback for when player click an item
        {
            if (Items[_index].GetStackHolder() != null && !Items[_index].isEmpty())
            {
                foreach (var setting in ItemManager.instance.clickSettings)
                {
                    if (_button == (int)setting.mouseButton && isKeyMatch(setting.key))
                    {
                        if (setting.function == ClickFunctions.Use)
                        {
                            Items[_index].GetStackHolder().UseItem(Items[_index].GetItemId(), 1, _index);
                            Items[_index].Click();
                        }
                        else if (setting.function == ClickFunctions.Split)
                        {
                            Items[_index].Split();
                        }
                        else if (setting.function == ClickFunctions.Drop)
                        {
                            Items[_index].Drop();
                        }
                        else if (setting.function == ClickFunctions.MarkFavorite)
                        {
                            Items[_index].MarkFav();
                        }
                    }
                }
            }
        }

        private bool isKeyMatch(AlterKeys _key) {
            switch (_key)
            {
                case AlterKeys.None:
                    return true;
                case AlterKeys.LeftCtrl:
                    return InputProxy.GetKey(KeyCode.LeftControl);
                case AlterKeys.LeftAlt:
                    return InputProxy.GetKey(KeyCode.LeftAlt);
                case AlterKeys.LeftShift:
                    return InputProxy.GetKey(KeyCode.LeftShift);
            }
            return false;
        }

        private void CheckDragging()//Check if there is any item being dragged
        {
            if (ItemDragManager.isDragging)
            {
                wasDragging = true;
            }
            else
            {
                if (wasDragging) FilterItems();
                wasDragging = false;
            }
        }

        public void SwitchTab(int _id)//Switch catergory and filter items with this category
        {
            selectedTab = _id;
            for (int i = 0; i < TabItems.Length; i++)
            {
                TabItems[i].mObjects[0].SetActive(i == selectedTab);
            }
            FilterItems();
            SoundManager.Play2D("Tab");
        }

        public void FilterItems()//Filter items by different conditions
        {
            for (int i = 0; i < Items.Length; i++)
            {
                Items[i].SetVisible(Items[i].isNameMatch(SearchInput.text) && Items[i].isTypeMatch(selectedTab) && (!filterFav || Items[i].Fav.activeSelf));
            }
            ClearFilterButton.SetActive(SearchInput.text != "");
        }

        public void ClearFilter()//Clear all filters.
        {
            SearchInput.text = "";
            ClearFilterButton.SetActive(false);
            filterFav = false;
            for (int i = 0; i < Items.Length; i++)
            {
                Items[i].SetVisible(true);
            }
        }

        public void UpdateTotalNumber()//Update the total number of items inside this container.
        {
            int _num = 0;
            for (int i = 0; i < Holder.Stacks.Count; i++)
            {
                if (!Holder.Stacks[i].isEmpty()) _num++;
            }
            TotalNumText.text = "  " + _num + "/" + Holder.InventorySize;
        }

        public void Sort()//Sort all items by their quality and category
        {
            SoundManager.Play2D("Sort");
            Holder.Stacks.Sort(SortByTypeAndQuality);
            foreach (InventoryItem obj in Items)
            {
                Destroy(obj.gameObject);
            }
            ShowList();
            FilterItems();
            Holder.ItemChanged(new Dictionary<Item, int>());
        }

        private static int SortByTypeAndQuality(InventoryStack a, InventoryStack b)//Sort compare method
        {
            int _scoreA = a.isEmpty() ? 900000 : 0;
            if (!a.isEmpty())
            {
                if (a.Item.favorite) _scoreA -= 900000;
                _scoreA += a.Item.type * 30000 + a.Item.quality * 3000 + a.Item.uid;
            }
            int _scoreB = b.isEmpty() ? 900000 : 0;
            if (!b.isEmpty())
            {
                if (b.Item.favorite) _scoreB -= 900000;
                _scoreB += b.Item.type * 30000 + b.Item.quality * 3000 + b.Item.uid;
            }
            return _scoreA.CompareTo(_scoreB);
        }

        public void ShowFavItems()//Toggle to filter the favorite items.
        {
            SoundManager.Play2D("Tab");
            filterFav = !filterFav;
            FavActiveIcon.SetActive(filterFav);
            FilterItems();
        }

    }
}
