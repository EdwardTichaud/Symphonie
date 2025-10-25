using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace SoftKitty.InventoryEngine
{
    public class ContainerBase : UiWindow
    {
        #region Variables
        public Text TitleText;
        public RectTransform HoverInfoAnchorPoint;
        public InventoryItem ItemPrefab;
        public Image BlockImage;
        protected InventoryHolder Holder;
        protected bool inited = false;
        protected InventoryItem[] Items;
        #endregion

        #region MonoBehaviour
        public override void Update()
        {
            if (!inited) return;
            base.Update();
            CheckBlock();
        }
        #endregion 
        public override void Initialize(InventoryHolder _inventoryHolder, InventoryHolder _equipHolder, string _name = "Inventory")//Initialize this interface
        {
            Holder = _inventoryHolder;
            TitleText.text = _name.ToUpper();
            ItemPrefab.Outline.color = InventorySkin.instance.ItemSelectedColor;
            ItemPrefab.Hover.color = InventorySkin.instance.ItemHoverColor;
            ItemPrefab.Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
            Holder.CalWeight();
            ShowList();
            inited = true;
        }

        protected void ShowList()//Show Item List
        {
            Items = new InventoryItem[Holder.Stacks.Count];
            for (int i = 0; i < Holder.Stacks.Count; i++)
            {
                GameObject _newItem = Instantiate(ItemPrefab.gameObject, ItemPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.GetComponent<InventoryItem>().Initialize(Holder.Stacks[i]);
                _newItem.GetComponent<InventoryItem>().SetHolder(Holder);
                _newItem.GetComponent<InventoryItem>().HoverInfoAnchorPoint = HoverInfoAnchorPoint;
                _newItem.GetComponent<InventoryItem>().RegisterClickCallback(i, OnItemClick);
                _newItem.gameObject.SetActive(true);
                Items[i] = _newItem.GetComponent<InventoryItem>();
            }
        }

        public virtual void OnItemClick(int _index, int _button)//Callback for when player click an item
        {
             
        }

        private void CheckBlock() //Check if the interface should be blocked.
        {
            if (NumberInput.instance != null)
            {
                BlockImage.gameObject.SetActive(true);
                BlockImage.color = new Color(0F, 0F, 0F, Mathf.Lerp(BlockImage.color.a, 0.94F, Time.unscaledDeltaTime * 3F));
            }
            else
            {
                if (BlockImage.color.a > 0F)
                {
                    BlockImage.color = new Color(0F, 0F, 0F, Mathf.MoveTowards(BlockImage.color.a, 0F, Time.unscaledDeltaTime * 2F));
                }
                else
                {
                    if (BlockImage.gameObject.activeSelf) BlockImage.gameObject.SetActive(false);
                }
            }
        }


    }
}
