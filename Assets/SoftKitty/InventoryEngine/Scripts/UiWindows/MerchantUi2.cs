using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class MerchantUi2 : UiWindow
    {
        #region Variables
        public RectTransform HoverInfoAnchorPlayer;
        public RectTransform HoverInfoAnchorMerchant;
        public RawImage MerchantAvatar;
        public InventoryItem PlayerItemPrefab;
        public InventoryItem MerchantItemPrefab;
        public InventoryItem [] PlayerTradeItems;
        public InventoryItem [] MerchantTradeItems;
        public InventoryHolder PlayerTradeHolder;
        public InventoryHolder NpcTradeHolder;
        public CurrenyInfo PlayerCurrencyPrefab;
        public CurrenyInfo MerchantCurrencyPrefab;
        public CurrenyInfo PlayerTradeCurrencyPrefab;
        public CurrenyInfo MerchantTradeCurrencyPrefab;
        public InputField SearchInput;
        public GameObject ClearFilterButton;
        public GameObject FavActiveIcon;
        public Text MerchantNameText;
        public Text SubtitleText;
        private InventoryItem[] playerItems;
        private InventoryItem[] merchantItems;
        private InventoryHolder playerHolder;
        private InventoryHolder merchantHolder;
        public Button TradeButton;
        private bool filterFav = false;
        private bool inited = false;
        private InventorySnapShot playerSnapshot;
        private InventorySnapShot merchantSnapshot;
        private List<CurrenyInfo> playerCurrencyItems = new List<CurrenyInfo>();
        private List<CurrenyInfo> merchantCurrencyItems = new List<CurrenyInfo>();
        private List<CurrenyInfo> playerTradeCurrencyItems = new List<CurrenyInfo>();
        private List<CurrenyInfo> merchantTradeCurrencyItems = new List<CurrenyInfo>();
        public Image BlockImage;
        #endregion

        #region MonoBehaviour
        void Start()
        {
            FavActiveIcon.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
        }

        public override void Update()
        {
            if (!inited) return;
            base.Update();
            FilterItems();
            CheckBlock();
        }
        #endregion

        public float GetSellPriceMultiplier(int _itemId)
        {
            foreach (var obj in merchantHolder.SpecificPriceMultiplier)
            {
                if (Mathf.FloorToInt(obj.x) == _itemId) return obj.y;
            }
            return merchantHolder.SellPriceMultiplier;
        }

        public float GetBuyPriceMultiplier(int _itemId)
        {
            foreach (var obj in merchantHolder.SpecificPriceMultiplier)
            {
                if (Mathf.FloorToInt(obj.x) == _itemId) return obj.z;
            }
            return merchantHolder.BuyPriceMultiplier;
        }

        public void SetMerchantAvatar(Texture _avatar) //Set the avatar image of the merchant to display on the top right of the window
        {
            MerchantAvatar.texture = _avatar;
        }

        public override void Initialize(InventoryHolder _playerHolder, InventoryHolder _merchantHolder, string _merchantName = "Merchant")//Initialize this interface
        {
            playerHolder = _playerHolder;
            merchantHolder = _merchantHolder;
            MerchantNameText.text = _merchantName;
            SubtitleText.text = " " + _merchantName.ToUpper();
            PlayerItemPrefab.Outline.color = InventorySkin.instance.ItemSelectedColor;
            PlayerItemPrefab.Hover.color = InventorySkin.instance.ItemHoverColor;
            PlayerItemPrefab.Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
            MerchantItemPrefab.Outline.color = InventorySkin.instance.ItemSelectedColor;
            MerchantItemPrefab.Hover.color = InventorySkin.instance.ItemHoverColor;
            MerchantItemPrefab.Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
            playerHolder.RegisterItemChangeCallback(OnItemChange);
            merchantHolder.RegisterItemChangeCallback(OnItemChange);
            PlayerTradeHolder.RegisterItemChangeCallback(OnItemChange);
            NpcTradeHolder.RegisterItemChangeCallback(OnItemChange);
            PlayerTradeHolder.RefreshItemData();
            NpcTradeHolder.RefreshItemData();

            ShowList();
            for (int i = 0; i < playerHolder.Currency.Count; i++)
            {
                GameObject _newItem = Instantiate(PlayerCurrencyPrefab.gameObject, PlayerCurrencyPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.SetActive(true);
                _newItem.GetComponent<CurrenyInfo>().AutoCalculateChanges = false;
                _newItem.GetComponent<CurrenyInfo>().Initialize(i, playerHolder);
                playerCurrencyItems.Add(_newItem.GetComponent<CurrenyInfo>());
            }
            for (int i = 0; i < merchantHolder.Currency.Count; i++)
            {
                GameObject _newItem = Instantiate(MerchantCurrencyPrefab.gameObject, MerchantCurrencyPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.SetActive(true);
                _newItem.GetComponent<CurrenyInfo>().AutoCalculateChanges = false;
                _newItem.GetComponent<CurrenyInfo>().Initialize(i, merchantHolder);
                merchantCurrencyItems.Add(_newItem.GetComponent<CurrenyInfo>());
            }
            for (int i = 0; i < PlayerTradeHolder.Currency.Count; i++)
            {
                GameObject _newItem = Instantiate(PlayerTradeCurrencyPrefab.gameObject, PlayerTradeCurrencyPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.SetActive(true);
                _newItem.GetComponent<CurrenyInfo>().AutoCalculateChanges = false;
                _newItem.GetComponent<CurrenyInfo>().Initialize(i, PlayerTradeHolder);
                playerTradeCurrencyItems.Add(_newItem.GetComponent<CurrenyInfo>());
            }
            for (int i = 0; i < NpcTradeHolder.Currency.Count; i++)
            {
                GameObject _newItem = Instantiate(MerchantTradeCurrencyPrefab.gameObject, MerchantTradeCurrencyPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.SetActive(true);
                _newItem.GetComponent<CurrenyInfo>().AutoCalculateChanges = false;
                _newItem.GetComponent<CurrenyInfo>().Initialize(i, NpcTradeHolder);
                merchantTradeCurrencyItems.Add(_newItem.GetComponent<CurrenyInfo>());
            }
            playerSnapshot = playerHolder.GetSnapShot();
            merchantSnapshot = merchantHolder.GetSnapShot();
            playerHolder.AutoSave = false;
            merchantHolder.BlockAutoSave = true;
            playerHolder.BlockAutoSave = true;
            inited = true;
        }



        private void ShowList()//Show item list for both side
        {
            playerItems = new InventoryItem[playerHolder.Stacks.Count];
            for (int i = 0; i < playerHolder.Stacks.Count; i++)
            {
                GameObject _newItem = Instantiate(PlayerItemPrefab.gameObject, PlayerItemPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.GetComponent<InventoryItem>().Initialize(playerHolder.Stacks[i]);
                _newItem.GetComponent<InventoryItem>().SetHolder(playerHolder);
                _newItem.GetComponent<InventoryItem>().HoverInfoAnchorPoint = HoverInfoAnchorPlayer;
                _newItem.GetComponent<InventoryItem>().RegisterClickCallback(i, OnPlayerItemClick);
                _newItem.GetComponent<InventoryItem>().PriceMultiplier = GetBuyPriceMultiplier(playerHolder.Stacks[i].GetItemId());
                _newItem.GetComponent<InventoryItem>().LimitedByOwner = PlayerTradeHolder;
                _newItem.gameObject.SetActive(true);
                playerItems[i] = _newItem.GetComponent<InventoryItem>();
            }

            merchantItems = new InventoryItem[merchantHolder.Stacks.Count];
            for (int i = 0; i < merchantHolder.Stacks.Count; i++)
            {
                GameObject _newItem = Instantiate(MerchantItemPrefab.gameObject, MerchantItemPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.GetComponent<InventoryItem>().Initialize(merchantHolder.Stacks[i]);
                _newItem.GetComponent<InventoryItem>().SetHolder(merchantHolder);
                _newItem.GetComponent<InventoryItem>().HoverInfoAnchorPoint = HoverInfoAnchorMerchant;
                _newItem.GetComponent<InventoryItem>().RegisterClickCallback(i, OnMerchantItemClick);
                _newItem.GetComponent<InventoryItem>().PriceMultiplier = GetSellPriceMultiplier(merchantHolder.Stacks[i].GetItemId());
                _newItem.GetComponent<InventoryItem>().RecieveUntradeable = false;
                _newItem.GetComponent<InventoryItem>().LimitedByOwner = NpcTradeHolder;
                _newItem.gameObject.SetActive(true);
                merchantItems[i] = _newItem.GetComponent<InventoryItem>();
            }

            for (int i = 0; i < PlayerTradeHolder.Stacks.Count; i++)
            {
                PlayerTradeItems[i].Initialize(PlayerTradeHolder.Stacks[i]);
                PlayerTradeItems[i].SetHolder(PlayerTradeHolder);
                PlayerTradeItems[i].HoverInfoAnchorPoint = HoverInfoAnchorPlayer;
                PlayerTradeItems[i].RegisterClickCallback(i, OnPlayerTradeItemClick);
                PlayerTradeItems[i].PriceMultiplier = GetBuyPriceMultiplier(PlayerTradeHolder.Stacks[i].GetItemId());
                PlayerTradeItems[i].LimitedByOwner = playerHolder;
                PlayerTradeItems[i].RecieveUntradeable = false;
                PlayerTradeItems[i].Outline.color = InventorySkin.instance.ItemSelectedColor;
                PlayerTradeItems[i].Hover.color = InventorySkin.instance.ItemHoverColor;
                PlayerTradeItems[i].Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
            }

            for (int i = 0; i < NpcTradeHolder.Stacks.Count; i++)
            {
                MerchantTradeItems[i].Initialize(NpcTradeHolder.Stacks[i]);
                MerchantTradeItems[i].SetHolder(NpcTradeHolder);
                MerchantTradeItems[i].HoverInfoAnchorPoint = HoverInfoAnchorMerchant;
                MerchantTradeItems[i].RegisterClickCallback(i, OnMerchantTradeItemClick);
                MerchantTradeItems[i].PriceMultiplier = GetSellPriceMultiplier(NpcTradeHolder.Stacks[i].GetItemId());
                MerchantTradeItems[i].LimitedByOwner = merchantHolder;
                MerchantTradeItems[i].RecieveUntradeable = false;
                MerchantTradeItems[i].Outline.color = InventorySkin.instance.ItemSelectedColor;
                MerchantTradeItems[i].Hover.color = InventorySkin.instance.ItemHoverColor;
                MerchantTradeItems[i].Fav.GetComponent<Image>().color = InventorySkin.instance.FavoriteColor;
            }
        }
        private bool isKeyMatch(AlterKeys _key)
        {
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

        public void OnPlayerTradeItemClick(int _index, int _button)//Callback for when player click an item in player's trading area.
        {
            if (PlayerTradeItems[_index].isTradeable() && !PlayerTradeItems[_index].isEmpty()) {
                foreach (var setting in ItemManager.instance.clickSettings)
                {
                    if (_button == (int)setting.mouseButton && isKeyMatch(setting.key))
                    {
                        if (setting.function == ClickFunctions.Use)
                        {
                            PlayerTradeHolder.RemoveItem(PlayerTradeItems[_index].GetItemId(), 1 - playerHolder.AddItem(PlayerTradeItems[_index].GetItem(), 1).Number, _index);
                            SoundManager.Play2D("ItemDrop");
                        }
                    }
                }
            }
        }

        public void OnMerchantTradeItemClick(int _index, int _button)//Callback for when player click an item in merchant's trading area.
        {
            if (MerchantTradeItems[_index].isTradeable() && !MerchantTradeItems[_index].isEmpty())
            {
                foreach (var setting in ItemManager.instance.clickSettings)
                {
                    if (_button == (int)setting.mouseButton && isKeyMatch(setting.key))
                    {
                        if (setting.function == ClickFunctions.Use)
                        {
                            NpcTradeHolder.RemoveItem(MerchantTradeItems[_index].GetItemId(), 1 - merchantHolder.AddItem(MerchantTradeItems[_index].GetItem(), 1).Number, _index);
                            SoundManager.Play2D("ItemDrop");
                        }
                    }
                }
            }
        }

        public void OnPlayerItemClick(int _index, int _button)//Callback for when player click an item in player's inventory.
        {
            if (playerItems[_index].isTradeable() && !playerItems[_index].isEmpty()
                 && (merchantHolder.TradeAllItems || merchantHolder.TradeList.Contains(playerItems[_index].GetItemId()) || merchantHolder.TradeCategoryList.Contains(playerItems[_index].GetCategory()))) {
                foreach (var setting in ItemManager.instance.clickSettings)
                {
                    if (_button == (int)setting.mouseButton && isKeyMatch(setting.key))
                    {
                        if (setting.function == ClickFunctions.Use)
                        {
                            playerHolder.RemoveItem(playerItems[_index].GetItemId(), 1 - PlayerTradeHolder.AddItem(playerItems[_index].GetItem(), 1).Number, _index);
                            SoundManager.Play2D("ItemDrop");
                        }
                        else if (setting.function == ClickFunctions.MarkFavorite)
                        {
                            playerItems[_index].MarkFav();
                        }
                    }
                }
            }
        }

        public void OnMerchantItemClick(int _index, int _button)//Callback for when player click an item in merchant's inventory.
        {
            if (merchantItems[_index].isTradeable() && !merchantItems[_index].isEmpty())
            {
                foreach (var setting in ItemManager.instance.clickSettings)
                {
                    if (_button == (int)setting.mouseButton && isKeyMatch(setting.key))
                    {
                        if (setting.function == ClickFunctions.Use)
                        {
                            merchantHolder.RemoveItem(merchantItems[_index].GetItemId(), 1 - NpcTradeHolder.AddItem(merchantItems[_index].GetItem(), 1).Number, _index);
                            SoundManager.Play2D("ItemDrop");
                        }
                    }
                }
            }
        }

        private void OnDestroy()//UnRegisterI all callbacks
        {
            playerHolder.UnRegisterItemChangeCallback(OnItemChange);
            merchantHolder.UnRegisterItemChangeCallback(OnItemChange);
            merchantHolder.BlockAutoSave = false;
            playerHolder.BlockAutoSave = false;
        }

        public void OnItemChange(Dictionary<Item, int> _changedItems)//Callback for when items in player's inventory has changed.
        {
            GetCurrencyChange();
        }


        private void GetCurrencyChange()//Compare the items with the snapeshot, calculate the difference by their cost.
        {
            int[] _playerTradeValue = new int[ItemManager.instance.currencies.Count];
            int[] _merchantTradeValue = new int[ItemManager.instance.currencies.Count];
            foreach (var obj in PlayerTradeHolder.Stacks)
            {
                if (!obj.isEmpty())
                {
                    _playerTradeValue[ItemManager.TryGetItem(obj.GetItemId()).currency] += Mathf.CeilToInt(ItemManager.TryGetItem(obj.GetItemId()).price * GetBuyPriceMultiplier(obj.GetItemId()) * obj.Number);
                }
            }
            foreach (var obj in NpcTradeHolder.Stacks)
            {
                if (!obj.isEmpty())
                {
                    _merchantTradeValue[ItemManager.TryGetItem(obj.GetItemId()).currency] += Mathf.CeilToInt(ItemManager.TryGetItem(obj.GetItemId()).price * GetSellPriceMultiplier(obj.GetItemId()) * obj.Number);
                }
            }

            int[] _currencyChange = new int[ItemManager.instance.currencies.Count];

            for (int i = 0; i < _currencyChange.Length; i++)
            {
                _currencyChange[i] = _playerTradeValue[i] - _merchantTradeValue[i];
            }

            bool _lackOfCurrency = false;
            for (int i = 0; i < _currencyChange.Length; i++)
            {
                playerHolder.SetCurrency(i, playerSnapshot.GetCurrency(i, false) + _currencyChange[i]);
                merchantHolder.SetCurrency(i, merchantSnapshot.GetCurrency(i, false) - _currencyChange[i]);
                PlayerTradeHolder.SetCurrency(i, _playerTradeValue[i]);
                NpcTradeHolder.SetCurrency(i, _merchantTradeValue[i]);
                if (playerHolder.GetCurrency(i, true) < 0 || merchantHolder.GetCurrency(i, true) < 0) _lackOfCurrency = true;
            }
            TradeButton.interactable = !_lackOfCurrency;
            TradeButton.GetComponent<CanvasGroup>().alpha = _lackOfCurrency ? 0.2F : 1F;
            List<int> _playerCurrencyDifference = playerHolder.GetCurrencyDifference(playerSnapshot.CurrencyValue);
            for (int i = 0; i < playerCurrencyItems.Count; i++) playerCurrencyItems[i].SetChangeText(_playerCurrencyDifference[i]);
            List<int> _merchantCurrencyDifference = merchantHolder.GetCurrencyDifference(merchantSnapshot.CurrencyValue);
            for (int i = 0; i < merchantCurrencyItems.Count; i++) merchantCurrencyItems[i].SetChangeText(_merchantCurrencyDifference[i]);
        }

        public override void Close()//Revert to the snapshot for both side if the window is closed.
        {
            playerHolder.RevertSnapShot(playerSnapshot);
            merchantHolder.RevertSnapShot(merchantSnapshot);
            base.Close();
        }

        public void Cancel()//Revert to the snapshot for both side
        {
            SoundManager.Play2D("Sort");
            foreach (var obj in PlayerTradeHolder.Stacks)
            {
                obj.Delete();
            }
            foreach (var obj in NpcTradeHolder.Stacks)
            {
                obj.Delete();
            }
            for (int i = 0; i < ItemManager.instance.currencies.Count; i++)
            {
                PlayerTradeHolder.SetCurrency(i, 0);
                NpcTradeHolder.SetCurrency(i, 0);
            }
            playerHolder.RevertSnapShot(playerSnapshot);
            merchantHolder.RevertSnapShot(merchantSnapshot);
            foreach (var obj in playerCurrencyItems)
            {
                obj.SetChangeText(0);
            }
            foreach (var obj in merchantCurrencyItems)
            {
                obj.SetChangeText(0);
            }
        }

        public void Purchase()//Confirm the purchase and close the window
        {
            foreach (var obj in PlayerTradeHolder.Stacks)
            {
                if (!obj.isEmpty())
                {
                    merchantHolder.AddItem(obj.Item,obj.Number);
                }
            }
            foreach (var obj in NpcTradeHolder.Stacks)
            {
                if (!obj.isEmpty())
                {
                    playerHolder.AddItem(obj.Item, obj.Number);
                }
            }
            foreach (var obj in PlayerTradeHolder.Stacks)
            {
                obj.Delete();
            }
            foreach (var obj in NpcTradeHolder.Stacks)
            {
                obj.Delete();
            }
            for (int i = 0; i < ItemManager.instance.currencies.Count; i++)
            {
                PlayerTradeHolder.SetCurrency(i, 0);
                NpcTradeHolder.SetCurrency(i, 0);
            }
            SoundManager.Play2D("Trade");
            SoundManager.Play2D("DoneDeal");
            merchantHolder.BlockAutoSave = false;
            playerHolder.BlockAutoSave = false;
            playerHolder.Save();
            merchantHolder.Save();
            playerSnapshot = playerHolder.GetSnapShot();
            merchantSnapshot = merchantHolder.GetSnapShot();
            foreach (var obj in playerCurrencyItems)
            {
                obj.SetChangeText(0);
            }
            foreach (var obj in merchantCurrencyItems)
            {
                obj.SetChangeText(0);
            }
        }

        public void FilterItems()//Filter the items by their name and "isTradeable" mark
        {
            for (int i = 0; i < playerItems.Length; i++)
            {
                playerItems[i].SetVisible(playerItems[i].isNameMatch(SearchInput.text) && (!filterFav || playerItems[i].Fav.activeSelf) && playerItems[i].isTradeable(true)
                    && (merchantHolder.TradeAllItems || merchantHolder.TradeList.Contains(playerItems[i].GetItemId()) || merchantHolder.TradeCategoryList.Contains(playerItems[i].GetCategory())));
            }
            for (int i = 0; i < merchantItems.Length; i++)
            {
                merchantItems[i].SetVisible(merchantItems[i].isNameMatch(SearchInput.text));
            }
            for (int i = 0; i < MerchantTradeItems.Length; i++)
            {
                MerchantTradeItems[i].SetVisible(MerchantTradeItems[i].isNameMatch(SearchInput.text));
            }
            for (int i = 0; i < PlayerTradeItems.Length; i++)
            {
                PlayerTradeItems[i].SetVisible(PlayerTradeItems[i].isNameMatch(SearchInput.text));
            }
            ClearFilterButton.SetActive(SearchInput.text != "");
        }

        public void ClearFilter()//Clear all filters.
        {
            SearchInput.text = "";
            ClearFilterButton.SetActive(false);
            filterFav = false;
            for (int i = 0; i < playerItems.Length; i++)
            {
                playerItems[i].SetVisible(true);
            }
            for (int i = 0; i < merchantItems.Length; i++)
            {
                merchantItems[i].SetVisible(true);
            }
            for (int i = 0; i < MerchantTradeItems.Length; i++)
            {
                MerchantTradeItems[i].SetVisible(true);
            }
            for (int i = 0; i < PlayerTradeItems.Length; i++)
            {
                PlayerTradeItems[i].SetVisible(true);
            }
        }

        public void ShowFavItems()//Only show favorite items
        {
            SoundManager.Play2D("Tab");
            filterFav = !filterFav;
            FavActiveIcon.SetActive(filterFav);
            FilterItems();
        }

        private void CheckBlock()//Check if the interface should be blocked.
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
