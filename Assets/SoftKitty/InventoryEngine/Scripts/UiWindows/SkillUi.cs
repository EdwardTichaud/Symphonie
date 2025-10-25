using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace SoftKitty.InventoryEngine
{
    public class SkillUi : HiddenContainer
    {
        #region Variables
        public string[] TagList;
        public ListItem TabPrefab;
        public InputField SearchInput;
        public GameObject ClearFilterButton;
        public GameObject FavActiveIcon;
        private ListItem[] TabItems;
        private int selectedTab;
        private bool filterFav = false;
        private bool wasDragging = false;
        #endregion

        # region MonoBehaviour
        void Start()
        {
            FavActiveIcon.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
        }
        public override void Update()
        {
            if (!inited) return;
            base.Update();
            CheckDragging();
            FilterItems();
        }

        #endregion
         
        public override void Initialize(InventoryHolder _inventoryHolder, InventoryHolder _equipHolder, string _name = "Inventory")//Initialize this interface
        {
            base.Initialize(_inventoryHolder, _equipHolder, _name);
            List<ListItem> TabList = new List<ListItem>();
            selectedTab = 0;
            for (int i = 0; i < TagList.Length; i++)
            {
                GameObject _newItem = Instantiate(TabPrefab.gameObject, TabPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.SetActive(true);
                _newItem.GetComponent<ListItem>().mTexts[0].text =  TagList[i];
                _newItem.GetComponent<ListItem>().mObjects[0].SetActive(i == selectedTab);
                _newItem.GetComponent<ListItem>().mID =i;
                TabList.Add(_newItem.GetComponent<ListItem>());

            }

            float _width = (GetComponent<RectTransform>().sizeDelta.x - 42F - 90F) / (TabList.Count - 1);
            for (int i = 0; i < TabList.Count; i++)
            {
                TabList[i].GetComponent<RectTransform>().sizeDelta = new Vector2(i == 0 ? 90F : _width, 44F);
            }
            TabItems = TabList.ToArray();
            inited = true;
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

        public void SwitchTab(int _id)//Switch bwteen different tags
        {
            selectedTab = _id;
            for (int i = 0; i < TabItems.Length; i++)
            {
                TabItems[i].mObjects[0].SetActive(i == selectedTab);
            }
            SoundManager.Play2D("Tab");
        }

        public void FilterItems()//Filter items by the tag
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].SetVisible(Items[i].isNameMatch(SearchInput.text) && Items[i].isTagMatchText(TagList[selectedTab]) && (!filterFav || Items[i].Fav.activeSelf));
            }
            ClearFilterButton.SetActive(SearchInput.text != "");
        }

        public void ClearFilter()//Cleart all filters
        {
            SearchInput.text = "";
            ClearFilterButton.SetActive(false);
            filterFav = false;
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].SetVisible(true);
            }
        }

        public void ShowFavItems()//Toggle to only show the favorite items
        {
            filterFav = !filterFav;
            FavActiveIcon.SetActive(filterFav);
            SoundManager.Play2D("Tab");
        }
    }
}
