using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class CraftingUi : UiWindow
    {
        #region Variables
        public RectTransform HoverAnchorPoint;
        public Text TitleText;
        public Button[] TabButtons;
        public GameObject[] TabPanels;
        public GameObject InventoryPrefab;
        public RectTransform InventoryRect;
        public CurrenyInfo CurrencyPrefab;
        public Vector2 [] ItemGridPositionBySelectedTab;
        public Vector2[] ItemGridSizeBySelectedTab;


        [Header("<< Crafting >>")]
        public bool EnableCrafting = true;
        public float CraftingTimeMultiplier = 1F;
        public GameObject BlueprintPrefab;
        public Text CraftingResultNumber;
        public ItemIcon[] CraftingMaterialItems;
        public Text[] CraftingMaterialNumText;
        public Image[] CraftingMaterialIndicators;
        public ItemIcon CraftingResultItem;
        public InputField SearchField;
        public RectTransform CraftingProgressRoot;
        public RectTransform CraftingProgressBar;
        public CanvasGroup CraftButton;
        public GameObject ClearSearchButton;
        public NumberPanel CraftingNumberPanel;

        [Header("<< Enhancing >>")]
        public bool EnableEnhancing = true;
        public LinkIcon EnhancingResultItem;
        public ItemIcon[] EnhancingMaterialItems;
        public Text[] EnhancingMaterialNumText;
        public Image[] EnhancingMaterialIndicators;
        public CurrenyInfo EnhancingCurrencyCost;
        public CanvasGroup EnhancingButton;
        public RectTransform EnhancingProgressRoot;
        public RectTransform EnhancingProgressBar;
        public Text EnhancingButtonText;

        [Header("<< Enchanting >>")]
        public bool EnableEnchanting = true;
        public LinkIcon EnchantResultItem;
        public ItemIcon EnchantMaterialItem;
        public Text EnchantMaterialNumText;
        public Image EnchantMaterialIndicator;
        public CurrenyInfo EnchantCurrencyCost;
        public CanvasGroup EnchantButton;
        public RectTransform EnchantProgressRoot;
        public RectTransform EnchantProgressBar;
        public Text[] EnchantmentDetailTexts;
        public Text NoEnchantmentText;

        [Header("<< Socketing >>")]
        public bool EnableSocketing = true;
        public LinkIcon SocketingResultItem;
        public LinkIcon [] SocketingMaterialItem;
        public GameObject NoSlotText;


        private InventoryHolder Holder;
        private InventoryItem[] Items;
        private int SelectedTab = 0;
        private bool Crafting = false;


        private List<ItemIcon> BluePrintList=new List<ItemIcon>();
        private List<Vector2> CraftingMaterialRequired = new List<Vector2>();
        private int SelectedBlueprint = -1;
        private Item EnhancingItem = null;
        private Item EnchantItem = null;
        private Item SocketingItem = null;
        private bool inited = false;
        private int CraftNumber = 0;
        public InventoryHolder SocketingHolder;
        public Image BlockImage;
        #endregion


        #region Internal Methods
        public override void Update()
        {
            if (!inited) return;
            InventoryRect.anchoredPosition = Vector2.Lerp(InventoryRect.anchoredPosition, ItemGridPositionBySelectedTab[SelectedTab], Time.unscaledDeltaTime * 20F);
            InventoryRect.sizeDelta = Vector2.Lerp(InventoryRect.sizeDelta, ItemGridSizeBySelectedTab[SelectedTab], Time.unscaledDeltaTime * 20F);

            switch (SelectedTab)
            {
                case 0://Crafting
                    Crafting = Holder.isCrafting();
                    CraftingNumberPanel.Enabled = !Crafting;
                    if (Crafting)
                    {
                        CraftingSync();
                    } 
                    else if (CraftNumber != CraftingNumberPanel.GetNumber()) {
                        CraftNumber = CraftingNumberPanel.GetNumber();
                        RefreshCraftingArea();
                        RefreshInventoryItems();
                    }
                    break;
                case 1://Enchancing
                    if (EnhancingResultItem.GetItem() != EnhancingItem)
                    {
                        EnhancingItem = EnhancingResultItem.GetItem();
                        RefreshEnhancingArea();
                    }
                    break;
                case 2://Enchanting
                    if (EnchantResultItem.GetItem() != EnchantItem)
                    {
                        EnchantItem = EnchantResultItem.GetItem();
                        RefreshEnchantingArea();
                    }
                    break;
                case 3://Socketing
                    if (SocketingResultItem.GetItem() != SocketingItem)
                    {
                        SocketingItem = SocketingResultItem.GetItem();
                        RefreshSocketingArea();
                    }
                    break;

            }
            base.Update();
            CheckBlock();
        }

        private void CraftingSync()
        {
            CraftingProgressBar.localScale = new Vector3(Holder.GetCraftingProgress(), 1F, 1F);
            CraftingNumberPanel.SetNum(Holder.GetCraftingItemNumber() - Holder.GetCraftedItemNumber());
            CraftingResultItem.SetItemNumber(Holder.GetItemNumber(Holder.GetCraftingItemId()));
            if (Holder.GetCraftingProgress() >= 1F)
            {
                if (!Holder.GetCraftingFailed())
                {
                    CraftingResultNumber.text = "+ 1";
                    CraftingResultItem.GetComponentInChildren<AdvAnimation>().Stop();
                    CraftingResultItem.GetComponentInChildren<AdvAnimation>().Play();
                    SoundManager.Play2D("CraftSuccess");
                    RefreshCraftingArea();
                    RefreshInventoryItems();
                }
            }
            
        }

        private void CheckBlock()
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
        public void ClearFilter()
        {
            FilterBluePrint("");
            ClearSearchButton.SetActive(false);
        }
        private void FilterBluePrint(string _filter)
        {
            for (int i = 0; i < BluePrintList.Count; i++)
            {
                BluePrintList[i].SetVisible(_filter.Length <= 0 || ItemManager.TryGetItem(BluePrintList[i].GetItemId()).name.ToLower().Contains(_filter.ToLower()));
            }
        }
        public void OnSearchChange()
        {
            ClearSearchButton.SetActive(SearchField.text.Length > 0);
            FilterBluePrint(SearchField.text);
        }
        void CreateInventoryItems()
        {
            Items = new InventoryItem[Holder.Stacks.Count];
            for (int i = 0; i < Holder.Stacks.Count; i++)
            {
                GameObject _newItem = Instantiate(InventoryPrefab.gameObject, InventoryPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.GetComponent<InventoryItem>().Initialize(Holder.Stacks[i]);
                _newItem.GetComponent<InventoryItem>().SetHolder(Holder);
                _newItem.GetComponent<InventoryItem>().HoverInfoAnchorPoint = HoverAnchorPoint;
                _newItem.gameObject.SetActive(true);
                Items[i] = _newItem.GetComponent<InventoryItem>();
                Items[i].SetVisible(Holder.Stacks[i].GetType(GetFilterCategory()) == GetFilterCategory());
                _newItem.GetComponent<InventoryItem>().RegisterClickCallback(i, OnInventoryItemClick);
            }
            foreach (var obj in BluePrintList)
            {
                Destroy(obj.gameObject);
            }
            BluePrintList.Clear();
            for (int i = 0; i < Holder.HiddenStacks.Count; i++)
            {
                if (!Holder.HiddenStacks[i].isEmpty() && Holder.HiddenStacks[i].Item.isTagMatchText(ItemManager.instance.CraftingBlueprintTag))
                {
                    if (Holder.HiddenStacks[i].Item.craftMaterials.Count > 0)
                    {
                        int _id = Mathf.FloorToInt(Holder.HiddenStacks[i].Item.craftMaterials[0].x);
                        GameObject _newItem = Instantiate(BlueprintPrefab.gameObject, BlueprintPrefab.transform.parent);
                        _newItem.transform.localScale = Vector3.one;
                        _newItem.GetComponent<ItemIcon>().SetItemId(_id);
                        _newItem.GetComponent<ItemIcon>().SetAppearance(ItemManager.TryGetItem(_id).Copy(), false, false);
                        _newItem.GetComponent<ItemIcon>().HoverInfoAnchorPoint = HoverAnchorPoint;
                        _newItem.GetComponent<ItemIcon>().RegisterClickCallback(BluePrintList.Count, OnBlueprintClick);
                        _newItem.gameObject.SetActive(true);
                        BluePrintList.Add(_newItem.GetComponent<ItemIcon>());
                    }
                }
            }
            
        }
        void RefreshEnhancingArea()
        {
            bool _allMatch = (EnhancingItem != null && !EnhancingResultItem.isEmpty());
            for (int i = 0; i < ItemManager.instance.EnhancingMaterials.Length; i++)
            {
                int _id = Mathf.FloorToInt(ItemManager.instance.EnhancingMaterials[i].x);
                int _required = Mathf.FloorToInt(ItemManager.instance.EnhancingMaterials[i].y);
                int _num = Holder.GetItemNumber(_id);
                EnhancingMaterialItems[i].SetItemNumber(_num);
                EnhancingMaterialNumText[i].text = _num.ToString() + "/" + _required.ToString();
                if (_num >= _required)
                {
                    EnhancingMaterialNumText[i].color = new Color(0.1F, 0.7F, 0.35F, 1F);
                }
                else
                {
                    _allMatch = false;
                    EnhancingMaterialNumText[i].color = new Color(1F, 0.3F, 0.3F, 1F);
                }
                EnhancingMaterialIndicators[i].color = EnhancingMaterialNumText[i].color;
            }
            if (Holder.GetCurrency(ItemManager.instance.EnhancingCurrencyType,true) < ItemManager.instance.EnhancingCurrencyNeed)
            {
                _allMatch = false;
                EnhancingCurrencyCost.SetColor(new Color(1F, 0.3F, 0.3F, 1F));
            }
            else
            {
                EnhancingCurrencyCost.RevertColor();
            }
            if (EnhancingItem != null && EnhancingItem.upgradeLevel >= ItemManager.instance.MaxiumEnhancingLevel)
            {
                EnhancingButtonText.text = "MAX";
                _allMatch = false;
            }
            else
            {
                EnhancingButtonText.text = "ENHANCE";
            }
            EnhancingButton.interactable = _allMatch;
            EnhancingButton.alpha = _allMatch ? 1F : 0.4F;
        }

        public void OnSocketingItemClick(int _index, int _button)
        {
            if (_button == 1)
            {
                SocketingResultItem.SetEmpty();
                SocketingItem = null;
                RefreshSocketingArea();
                SoundManager.Play2D("ItemDrop");
            }
        }

        public void OnSocketingMaterialInsert(int _index, Item _item)
        {
            if (SocketingItem != null && _item != null)
            {
                List<int> _sockets = new List<int>();
                _sockets.AddRange(SocketingResultItem.GetItem().socketedItems);
                if (_sockets[_index] == -1)
                {
                    _sockets[_index] = _item.uid;
                    Holder.FindItem(SocketingResultItem.GetItemId(), SocketingResultItem.GetItem().upgradeLevel, SocketingResultItem.GetItem().enchantments, SocketingResultItem.GetItem().socketedItems).ReplaceSocketing(_sockets);
                    SocketingResultItem.GetItem().ReplaceSocketing(_sockets);
                    Holder.RemoveItem(_item.uid, 1);
                    RefreshSocketingArea();
                    SoundManager.Play2D("EquipOff");
                    Holder.ItemChanged(new Dictionary<Item, int>());
                }
            }
        }

        public void RemoveSocketingMaterial(int _id)
        {
            if (Holder.GetCurrency(ItemManager.instance.RemoveSocketingCurrency, true) >= ItemManager.instance.RemoveSocketingPrice) {
                Holder.AddCurrency(ItemManager.instance.RemoveSocketingCurrency,-ItemManager.instance.RemoveSocketingPrice);
                int _socketed = SocketingResultItem.GetItem().socketedItems[_id];
                List<int> _sockets = new List<int>();
                _sockets.AddRange(SocketingResultItem.GetItem().socketedItems);
                if (_sockets[_id]>=0) {
                    _sockets[_id] = -1;
                    Holder.FindItem(SocketingResultItem.GetItemId(), SocketingResultItem.GetItem().upgradeLevel, SocketingResultItem.GetItem().enchantments, SocketingResultItem.GetItem().socketedItems).ReplaceSocketing(_sockets);
                    SocketingResultItem.GetItem().ReplaceSocketing(_sockets);
                    if (_socketed >= 0 && !ItemManager.instance.DestroySocketItemWhenRemove) Holder.AddItem(new Item(_socketed));
                    RefreshSocketingArea();
                    SoundManager.Play2D("CraftFail");
                    Holder.ItemChanged(new Dictionary<Item, int>());
                }
            }
        }

        public void UnlockSocketingSlot(int _id)
        {
            if (Holder.GetCurrency(ItemManager.instance.UnlockSocketingSlotsCurrency, true) >= ItemManager.instance.UnlockSocketingSlotsPrice)
            {
                Holder.AddCurrency(ItemManager.instance.UnlockSocketingSlotsCurrency, -ItemManager.instance.UnlockSocketingSlotsPrice);
                List<int> _sockets = new List<int>();
                _sockets.AddRange(SocketingResultItem.GetItem().socketedItems);
                if (_sockets[_id]==-2) {
                    _sockets[_id] = -1;
                    Holder.FindItem(SocketingResultItem.GetItemId(), SocketingResultItem.GetItem().upgradeLevel, SocketingResultItem.GetItem().enchantments, SocketingResultItem.GetItem().socketedItems).ReplaceSocketing(_sockets);
                    SocketingResultItem.GetItem().ReplaceSocketing(_sockets);
                    RefreshSocketingArea();
                    SoundManager.Play2D("CraftSuccess");
                    Holder.ItemChanged(new Dictionary<Item, int>());
                }
            }
        }

        void RefreshSocketingArea()
        {

            if (SocketingItem != null)
            {
                NoSlotText.SetActive(SocketingItem.socketedItems.Count == 0);
                for (int i = 0; i < SocketingMaterialItem.Length; i++)
                {
                    if (i < SocketingItem.socketedItems.Count)
                    {
                        SocketingMaterialItem[i].gameObject.SetActive(true);
                        SocketingMaterialItem[i].GetComponent<ListItem>().mButtons[0].gameObject.SetActive(SocketingItem.socketedItems[i] == -2);
                        SocketingMaterialItem[i].GetComponent<ListItem>().mButtons[1].gameObject.SetActive(ItemManager.instance.EnableRemoveSocketing && SocketingItem.socketedItems[i] >= 0);
                        SocketingMaterialItem[i].CanBeLinked = (SocketingItem.socketedItems[i] == -1);


                        if (SocketingItem.socketedItems[i] >= 0)
                        {
                            if (ItemManager.instance.EnableRemoveSocketing)
                            {
                                SocketingMaterialItem[i].GetComponent<ListItem>().mButtons[1].interactable = Holder.GetCurrency(ItemManager.instance.RemoveSocketingCurrency, true) >= ItemManager.instance.RemoveSocketingPrice;
                                SocketingMaterialItem[i].GetComponent<ListItem>().mTexts[1].color =
                                    SocketingMaterialItem[i].GetComponent<ListItem>().mButtons[1].interactable ? ItemManager.instance.currencies[ItemManager.instance.RemoveSocketingCurrency].color : Color.red;
                                LayoutRebuilder.ForceRebuildLayoutImmediate(SocketingMaterialItem[i].GetComponent<ListItem>().mButtons[1].GetComponentInChildren<HorizontalLayoutGroup>(true).GetComponent<RectTransform>());
                            }
                            Item _item = ItemManager.TryGetItem(SocketingItem.socketedItems[i]).Copy();
                            SocketingMaterialItem[i].Initialize(_item, false);
                        }
                        else
                        {
                            if (SocketingItem.socketedItems[i] == -2)
                            {
                                SocketingMaterialItem[i].GetComponent<ListItem>().mButtons[0].interactable = Holder.GetCurrency(ItemManager.instance.UnlockSocketingSlotsCurrency, true) >= ItemManager.instance.UnlockSocketingSlotsPrice;
                                SocketingMaterialItem[i].GetComponent<ListItem>().mTexts[0].color =
                                    SocketingMaterialItem[i].GetComponent<ListItem>().mButtons[0].interactable ? ItemManager.instance.currencies[ItemManager.instance.UnlockSocketingSlotsCurrency].color : Color.red;
                                LayoutRebuilder.ForceRebuildLayoutImmediate(SocketingMaterialItem[i].GetComponent<ListItem>().mButtons[0].GetComponentInChildren<HorizontalLayoutGroup>(true).GetComponent<RectTransform>());
                            }
                            SocketingMaterialItem[i].SetEmpty();
                        }
                    }
                    else
                    {
                        SocketingMaterialItem[i].gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                NoSlotText.SetActive(false);
                for (int i = 0; i < SocketingMaterialItem.Length; i++)
                {
                    SocketingMaterialItem[i].SetEmpty();
                    SocketingMaterialItem[i].gameObject.SetActive(false);
                }
            }
            RefreshInventoryItems();
        }

        void RefreshEnchantingArea()
        {
            bool _allMatch = (EnchantItem != null && !EnchantResultItem.isEmpty());

            int _id = Mathf.FloorToInt(ItemManager.instance.EnchantingMaterial.x);
            int _required = Mathf.FloorToInt(ItemManager.instance.EnchantingMaterial.y);
            int _num = Holder.GetItemNumber(_id);
            EnchantMaterialItem.SetItemNumber(_num);
            EnchantMaterialNumText.text = _num.ToString() + "/" + _required.ToString();
            if (_num >= _required)
            {
                EnchantMaterialNumText.color = new Color(0.1F, 0.7F, 0.35F, 1F);
            }
            else
            {
                _allMatch = false;
                EnchantMaterialNumText.color = new Color(1F, 0.3F, 0.3F, 1F);
            }
            EnchantMaterialIndicator.color = EnchantMaterialNumText.color;

            if (Holder.GetCurrency(ItemManager.instance.EnchantingCurrencyType,true) < ItemManager.instance.EnchantingCurrencyNeed)
            {
                _allMatch = false;
                EnchantCurrencyCost.SetColor(new Color(1F, 0.3F, 0.3F, 1F));
            }
            else
            {
                EnchantCurrencyCost.RevertColor();
            }

            NoEnchantmentText.gameObject.SetActive(EnchantItem != null && EnchantItem.enchantments.Count <= 0);
            for (int i = 0; i < EnchantmentDetailTexts.Length; i++)
            {
                EnchantmentDetailTexts[i].gameObject.SetActive(EnchantItem != null && i < EnchantItem.enchantments.Count);
                if (EnchantItem != null && i < EnchantItem.enchantments.Count && ItemManager.TryGetEnchantmentById(EnchantItem.enchantments[i])!=null)
                {
                    EnchantmentDetailTexts[i].text = ItemManager.TryGetEnchantmentById(EnchantItem.enchantments[i]).GetDescription();
                }
            }

            EnchantButton.interactable = _allMatch;
            EnchantButton.alpha = _allMatch ? 1F : 0.4F;
        }

        void RefreshInventoryItems()
        {
            for (int i = 0; i < Holder.Stacks.Count; i++)
            {
                Items[i].SetVisible((SelectedTab==0 || Holder.Stacks[i].GetType(GetFilterCategory()) == GetFilterCategory()) && TagFilter(Holder.Stacks[i]));
                Items[i].Draggable = Items[i].Visible;
            }
        }

        void RefreshCraftingArea()
        {
            if (SelectedBlueprint == -1) return;
            bool _allMatch = true;
            for (int i = 0; i < CraftingMaterialItems.Length; i++)
            {
                if (CraftingMaterialItems[i].gameObject.activeSelf)
                {
                    int _num = Holder.GetItemNumber(Mathf.FloorToInt(CraftingMaterialRequired[i].x));
                    int _required = Mathf.FloorToInt(CraftingMaterialRequired[i].y) * CraftingNumberPanel.GetNumber();
                    CraftingMaterialNumText[i].text = _num.ToString() + "/" + _required.ToString();
                    CraftingMaterialItems[i].SetItemNumber(_num);
                    if (_num >= _required)
                    {
                        CraftingMaterialNumText[i].color = new Color(0.1F, 0.7F, 0.35F, 1F);
                    }
                    else
                    {
                        _allMatch = false;
                        CraftingMaterialNumText[i].color = new Color(1F, 0.3F, 0.3F, 1F);
                    }
                    CraftingMaterialIndicators[i].color = CraftingMaterialNumText[i].color;
                }
            }
            CraftingResultItem.SetItemNumber(Holder.GetItemNumber(BluePrintList[SelectedBlueprint].GetItemId()));
            CraftButton.interactable = _allMatch;
            CraftButton.alpha = _allMatch ? 1F : 0.4F;

        }
        #endregion 
        public override void Initialize(InventoryHolder _inventoryHolder, InventoryHolder _equipHolder, string _name = "Forge")//Initialize this interface
        {
            Holder = _inventoryHolder;
            TitleText.text = _name;
            CreateInventoryItems();
            CraftingResultItem.SetEmpty();
            CraftingResultItem.HoverInfoAnchorPoint = HoverAnchorPoint;

            TabButtons[0].gameObject.SetActive(ItemManager.instance.EnableCrafting && EnableCrafting);
            TabButtons[1].gameObject.SetActive(ItemManager.instance.EnableEnhancing && EnableEnhancing);
            TabButtons[2].gameObject.SetActive(ItemManager.instance.EnableEnchanting && EnableEnchanting);
            TabButtons[3].gameObject.SetActive(ItemManager.instance.EnableSocketing && EnableSocketing);
            int _defaultTab = -1;
            if (ItemManager.instance.EnableCrafting && EnableCrafting)
            {
                _defaultTab = 0;
            } 
            else if (ItemManager.instance.EnableEnhancing && EnableEnhancing) {
                _defaultTab = 1;
            }
            else if (ItemManager.instance.EnableEnchanting && EnableEnchanting)
            {
                _defaultTab = 2;
            }
            else if (ItemManager.instance.EnableSocketing && EnableSocketing)
            {
                _defaultTab = 3;
            }

            foreach (ItemIcon obj in CraftingMaterialItems) {
                obj.SetEmpty();
                obj.HoverInfoAnchorPoint = HoverAnchorPoint;
                obj.SetItemNumber(0);
                obj.Outline.color = InventorySkin.instance.ItemSelectedColor;
                obj.Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
            }

            for (int i = 0; i < ItemManager.instance.EnhancingMaterials.Length; i++)
            {
                int _id = Mathf.FloorToInt(ItemManager.instance.EnhancingMaterials[i].x);
                EnhancingMaterialItems[i].SetAppearance(ItemManager.TryGetItem(_id).Copy(), true);
                EnhancingMaterialItems[i].SetItemId(_id);
                EnhancingMaterialItems[i].HoverInfoAnchorPoint = HoverAnchorPoint;
                EnhancingMaterialItems[i].Outline.color = InventorySkin.instance.ItemSelectedColor;
                EnhancingMaterialItems[i].Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
            }
            EnhancingCurrencyCost.Initialize(ItemManager.instance.EnhancingCurrencyType,Holder,false);
            EnhancingCurrencyCost.SetValue(ItemManager.instance.EnhancingCurrencyNeed);
            EnhancingResultItem.Initialize(null, true);
            EnhancingResultItem.SetItemNumber(0);
            EnhancingResultItem.RegisterClickCallback(0,OnEnhancingItemClick);

            int _enchantId = Mathf.FloorToInt(ItemManager.instance.EnchantingMaterial.x);
            EnchantMaterialItem.SetAppearance(ItemManager.TryGetItem(_enchantId).Copy(), true);
            EnchantMaterialItem.SetItemId(_enchantId);
            EnchantMaterialItem.HoverInfoAnchorPoint = HoverAnchorPoint;
            EnchantMaterialItem.Outline.color = InventorySkin.instance.ItemSelectedColor;
            EnchantMaterialItem.Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;

            EnchantCurrencyCost.Initialize(ItemManager.instance.EnchantingCurrencyType, Holder, false);
            EnchantCurrencyCost.SetValue(ItemManager.instance.EnchantingCurrencyNeed);
            EnchantResultItem.Initialize(null, true);
            EnchantResultItem.SetItemNumber(0);
            EnchantResultItem.RegisterClickCallback(0, OnEnchantingItemClick);

            SocketingResultItem.Initialize(null, true);
            SocketingResultItem.SetItemNumber(0);
            SocketingResultItem.RegisterClickCallback(0, OnSocketingItemClick);
            for (int i = 0; i < SocketingMaterialItem.Length; i++)
            {
                SocketingMaterialItem[i].Initialize(null, true);
                SocketingMaterialItem[i].SetItemNumber(0);
                SocketingMaterialItem[i].LimitedByTag=ItemManager.instance.SocketingTagFilter;
                SocketingMaterialItem[i].SetHolder(SocketingHolder);
                SocketingMaterialItem[i].RegisterLinkedCallback(i,OnSocketingMaterialInsert);
                SocketingMaterialItem[i].GetComponent<ListItem>().mImages[1].sprite = ItemManager.instance.currencies[ItemManager.instance.RemoveSocketingCurrency].icon;
                SocketingMaterialItem[i].GetComponent<ListItem>().mTexts[1].text = ItemManager.instance.RemoveSocketingPrice.ToString();
                SocketingMaterialItem[i].GetComponent<ListItem>().mImages[0].sprite = ItemManager.instance.currencies[ItemManager.instance.UnlockSocketingSlotsCurrency].icon;
                SocketingMaterialItem[i].GetComponent<ListItem>().mTexts[0].text = ItemManager.instance.UnlockSocketingSlotsPrice.ToString();
                
            }

            for (int i = 0; i < Holder.Currency.Count; i++)
            {
                GameObject _newItem = Instantiate(CurrencyPrefab.gameObject, CurrencyPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.SetActive(true);
                _newItem.GetComponent<CurrenyInfo>().Initialize(i, Holder);
            }
            if(_defaultTab>-1) SwitchTab(_defaultTab);
            Holder.RegisterItemChangeCallback(OnMaterialChange);
            inited = true;
        }

        public void OnEnhancingItemClick(int _index, int _button)
        {
            if (_button == 1)
            {
                EnhancingResultItem.SetEmpty();
                RefreshEnhancingArea();
            }
        }

        public void OnEnchantingItemClick(int _index, int _button)
        {
            if (_button == 1)
            {
                EnchantResultItem.SetEmpty();
                RefreshEnchantingArea();
            }
        }

       

        public void OnInventoryItemClick(int _index, int _button)//Callback for when player click an item
        {
            if (_button == 0) return;
            int _id = Holder.Stacks[_index].GetItemId();
            if (_id>=0 && (SelectedTab==1 && ItemManager.TryGetItem(_id).type==ItemManager.instance.EnhancingCategoryID)
                || (SelectedTab == 2 && ItemManager.TryGetItem(_id).type == ItemManager.instance.EnchantingCategoryID)
                 || (SelectedTab == 3 && ItemManager.TryGetItem(_id).type == ItemManager.instance.SocketedCategoryFilter))
            {
                if (SelectedTab == 1)
                {
                    EnhancingResultItem.LinkItem(Holder, Holder.Stacks[_index].Item, Holder.Stacks[_index].Number, Holder.Stacks[_index].Empty);
                }
                else if(SelectedTab == 2)
                {
                    EnchantResultItem.LinkItem(Holder, Holder.Stacks[_index].Item, Holder.Stacks[_index].Number, Holder.Stacks[_index].Empty);
                } 
                else if(SelectedTab == 3 && SocketingItem==null)
                {
                    SocketingResultItem.LinkItem(Holder, Holder.Stacks[_index].Item, Holder.Stacks[_index].Number, Holder.Stacks[_index].Empty);
                }
                SoundManager.Play2D("ItemDrop");
            }
        }

        private void OnDestroy()//UnRegister the callback when this window is destroyed
        {
            Holder.UnRegisterItemChangeCallback(OnMaterialChange);
        }

        public void SwitchTab(int _tab)//Swith between [Crafting] [Enhancing] [Enchanting] Modes
        {
            if (Crafting) return;
            SelectedTab = _tab;
            for (int i=0;i<TabButtons.Length;i++) {
                TabButtons[i].interactable = (i != SelectedTab);
                TabPanels[i].SetActive(i == SelectedTab);
                RefreshInventoryItems();
            }
            switch (SelectedTab)
            {
                case 0:
                    for (int i = 0; i < BluePrintList.Count; i++)
                    {
                        if (Holder.isCrafting() && BluePrintList[i].GetItemId() == Holder.GetCraftingItemId())
                        {
                            BluePrintList[i].OnLeftClick();
                        }
                    }
                    RefreshCraftingArea();
                    break;
                case 1:
                    RefreshEnhancingArea();
                    break;
                case 2:
                    RefreshEnchantingArea();
                    break;
                case 3:
                    RefreshSocketingArea();
                    break;
            }
            SoundManager.Play2D("CraftSwitch");
        }

        private int GetFilterCategory()//Get the CategoryID of current mode.
        {
            switch (SelectedTab)
            {
                case 0:
                    return ItemManager.instance.CraftingMaterialCategoryID;
                case 1:
                    return ItemManager.instance.EnhancingCategoryID;
                case 2:
                    return ItemManager.instance.EnchantingCategoryID;
                case 3:
                    return SocketingItem==null?ItemManager.instance.SocketedCategoryFilter: ItemManager.instance.SocketingCategoryFilter;
            }
            return ItemManager.instance.CraftingMaterialCategoryID;
        }

        private bool TagFilter(InventoryStack _stack)
        {
            if (SelectedTab==3 && SocketingItem!=null)
            {
                bool _anyTagMatchs = false;
                foreach (string obj in SocketingItem.socketingTag) {
                    if (_stack.isTagMatchText(obj)) _anyTagMatchs = true;
                }
                 return _stack.isTagMatchText(ItemManager.instance.SocketingTagFilter) && _anyTagMatchs;
            }
            return true;
        }
        
        public void OnBlueprintClick(int _index, int _button)//When player click a blueprint
        {
            if (Crafting) return;
            SelectedBlueprint = _index;
            SoundManager.Play2D("paper");
            for (int i=0;i< BluePrintList.Count;i++) {
                BluePrintList[i].ToggleOutline(i== SelectedBlueprint);
            }
            CraftingResultItem.SetAppearance(ItemManager.TryGetItem(BluePrintList[SelectedBlueprint].GetItemId()), true,false);
            CraftingResultItem.SetItemId(BluePrintList[SelectedBlueprint].GetItemId());

            CraftingNumberPanel.Initialize(1, ItemManager.TryGetItem(BluePrintList[SelectedBlueprint].GetItemId()).maxiumStack);

            CraftingMaterialRequired.Clear();
            CraftingMaterialRequired.AddRange( ItemManager.TryGetItem(BluePrintList[SelectedBlueprint].GetItemId()).craftMaterials);
            for (int i=0;i< CraftingMaterialItems.Length;i++) {
                if (i < CraftingMaterialRequired.Count)
                {
                    CraftingMaterialItems[i].SetAppearance(ItemManager.TryGetItem(Mathf.FloorToInt(CraftingMaterialRequired[i].x)), true,false);
                    CraftingMaterialItems[i].SetItemId(Mathf.FloorToInt(CraftingMaterialRequired[i].x));
                    CraftingMaterialItems[i].gameObject.SetActive(true);
                    CraftingMaterialNumText[i].gameObject.SetActive(true);
                    CraftingMaterialIndicators[i].gameObject.SetActive(true);
                }
                else
                {
                    CraftingMaterialItems[i].SetEmpty();
                    CraftingMaterialItems[i].gameObject.SetActive(false);
                    CraftingMaterialNumText[i].gameObject.SetActive(false);
                    CraftingMaterialIndicators[i].gameObject.SetActive(false);
                }
            }

            CraftingProgressRoot.sizeDelta = new Vector2(93F+ (CraftingMaterialRequired.Count-1)*82F, 25F);
            RefreshCraftingArea();
            RefreshInventoryItems();
        }

        public void OnMaterialChange(Dictionary<Item, int> _changedItems)//Callback for when player's inventory has changed.
        {
            RefreshInventoryItems();
            switch (SelectedTab)
            {
                case 0:
                    RefreshCraftingArea();
                    break;
                case 1:
                    RefreshEnhancingArea();
                    break;
                case 2:
                    RefreshEnchantingArea();
                    break;
                case 3:
                    RefreshSocketingArea();
                    break;
            }
        }

        public void Craft()//Start to Craft an item
        {
            if (!Crafting)
            {
                SoundManager.Play2D("DoCraft");
                List<Vector2> _mats = new List<Vector2>();
                for (int i = 0; i < CraftingMaterialItems.Length; i++)
                {
                    if (CraftingMaterialItems[i].gameObject.activeSelf)
                    {
                        _mats.Add(CraftingMaterialRequired[i]);
                    }
                }

                Holder.StartCrafting(BluePrintList[SelectedBlueprint].GetItemId(), CraftingNumberPanel.GetNumber(), _mats, ItemManager.instance.CraftingTime * CraftingTimeMultiplier);
            }
        }

        public void Enhance()//Start to enhance an item
        {
            if (!Crafting) StartCoroutine(EnhanceCo());
        }

        IEnumerator EnhanceCo()//IEnumerator for Enhancing
        {
            Crafting = true;
            SoundManager.Play2D("DoCraft");
            float _progress = 0F;
            while (_progress < 1F)
            {
                yield return 1;
                _progress = Mathf.MoveTowards(_progress, 1F, Time.unscaledDeltaTime * (1F / ItemManager.instance.EnhancingTime));
                EnhancingProgressBar.localScale = new Vector3(_progress, 1F, 1F);
            }
            yield return 1;
            
            for (int i = 0; i < ItemManager.instance.EnhancingMaterials.Length; i++)
            {
                 Holder.RemoveItem(Mathf.FloorToInt(ItemManager.instance.EnhancingMaterials[i].x), Mathf.FloorToInt(ItemManager.instance.EnhancingMaterials[i].y));
            }
            Holder.AddCurrency(ItemManager.instance.EnhancingCurrencyType, -ItemManager.instance.EnhancingCurrencyNeed);
            EnhancingResultItem.GetComponentInChildren<AdvAnimation>().Stop();
            if (Random.Range(0, 100) < ItemManager.instance.EnhancingSuccessCurve.Evaluate(EnhancingResultItem.GetItem().upgradeLevel * 1F / ItemManager.instance.MaxiumEnhancingLevel))
            {
                EnhancingResultItem.GetComponentInChildren<AdvAnimation>().Play("CraftingSuccess");
                int _level= Holder.FindItem(EnhancingResultItem.GetItemId(), EnhancingResultItem.GetItem().upgradeLevel, EnhancingResultItem.GetItem().enchantments, EnhancingResultItem.GetItem().socketedItems).Upgrade();
                EnhancingResultItem.GetItem().upgradeLevel=_level;
                DynamicMsg.PopItem(EnhancingResultItem.GetItem());
                SoundManager.Play2D("CraftSuccess");
            }
            else
            {
                EnhancingResultItem.GetComponentInChildren<AdvAnimation>().Play("CraftingFailed");
               
                if (ItemManager.instance.DestroyEquipmentWhenFail && EnhancingResultItem.GetItem().upgradeLevel >= ItemManager.instance.DestroyEquipmentWhenFailLevel)
                {
                    string _name = EnhancingResultItem.GetItem().nameWithAffixing + (EnhancingResultItem.GetItem().upgradeLevel > 0 ? "+" + EnhancingResultItem.GetItem().upgradeLevel.ToString() : "");
                    Holder.DeleteItem(EnhancingResultItem.GetItemId(), EnhancingResultItem.GetItem().upgradeLevel, EnhancingResultItem.GetItem().enchantments, EnhancingResultItem.GetItem().socketedItems);
                    EnhancingResultItem.SetEmpty();
                    DynamicMsg.PopMsg(ItemManager.instance.msgEnhancingFail.Replace("{name}", _name));
                }
                SoundManager.Play2D("CraftFail");
            }
            Holder.ItemChanged(new Dictionary<Item, int>());
            Crafting = false;
            RefreshEnhancingArea();
            RefreshInventoryItems();
        }

        public void Enchant()//Start to enchant an item
        {
            if (!Crafting) StartCoroutine(EnchantCo());
        }

        IEnumerator EnchantCo()//IEnumerator for Enchanting
        {
            Crafting = true;
            SoundManager.Play2D("DoCraft");
            float _progress = 0F;
            while (_progress < 1F)
            {
                yield return 1;
                _progress = Mathf.MoveTowards(_progress, 1F, Time.unscaledDeltaTime * (1F/ ItemManager.instance.EnchantingTime));
                EnchantProgressBar.localScale = new Vector3(_progress, 1F, 1F);
            }
            yield return 1;

            Holder.RemoveItem(Mathf.FloorToInt(ItemManager.instance.EnchantingMaterial.x), Mathf.FloorToInt(ItemManager.instance.EnchantingMaterial.y));
            Holder.AddCurrency(ItemManager.instance.EnchantingCurrencyType, -ItemManager.instance.EnchantingCurrencyNeed);
            EnchantResultItem.GetComponentInChildren<AdvAnimation>().Stop();
            if (Random.Range(0, 100) < ItemManager.instance.EnchantingSuccessRate)
            {
                EnchantResultItem.GetComponentInChildren<AdvAnimation>().Play("CraftingSuccess");
                int _count = Random.Range(Mathf.FloorToInt(ItemManager.instance.EnchantmentNumberRange.x), Mathf.FloorToInt(ItemManager.instance.EnchantmentNumberRange.y));
                List<int> _newEnchantments = new List<int>();
                for (int i = 0; i < _count; i++) {
                    int _uid = Random.Range(0, ItemManager.enchantmentDic.Count);
                    if(!_newEnchantments.Contains(_uid)) _newEnchantments.Add(_uid);
                }
                Holder.FindItem(EnchantResultItem.GetItemId(), EnchantResultItem.GetItem().upgradeLevel, EnchantResultItem.GetItem().enchantments, EnchantResultItem.GetItem().socketedItems).ReplaceEnchantment(_newEnchantments);
                EnchantResultItem.GetItem().ReplaceEnchantment(_newEnchantments);
                SoundManager.Play2D("CraftSuccess");
            }
            else
            {
                EnchantResultItem.GetComponentInChildren<AdvAnimation>().Play("CraftingFailed");
                SoundManager.Play2D("CraftFail");
            }
            Holder.ItemChanged(new Dictionary<Item, int>());
            Crafting = false;
            RefreshEnchantingArea();
            RefreshInventoryItems();
        }


        
    }
}
