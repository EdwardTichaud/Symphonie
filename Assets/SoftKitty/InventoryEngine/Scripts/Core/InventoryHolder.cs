using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// The InventoryHolder component is responsible for managing all items and currencies for each unit.
    /// </summary>
    public class InventoryHolder : MonoBehaviour
    {
        #region Variables
        /// <summary>
        /// The display name of this InventoryHolder component.
        /// </summary>
        public string Name = "Inventory";
       
        public enum HolderType
        {
            Crate,
            Merchant,
            PlayerInventory,
            PlayerEquipment,
            NpcInventory,
            NpcEquipment
        }
        /// <summary>
        ///  The type of this InventoryHolder component.
        ///  - Crate: Represents a crate or container.
        ///  - Merchant: Represents a merchant or store inventory.
        ///  - PlayerInventory: Represents the player¡¯s inventory.
        ///  - PlayerEquipment: Represents the player¡¯s equipped items.
        ///  - NpcInventory: Represents an NPC¡¯s inventory.
        ///  - NpcEquipment: Represents an NPC¡¯s equipped items.
        /// </summary>
        public HolderType Type;
        /// <summary>
        /// The list of InventoryStack objects. All normal items are stored here.
        /// </summary>
        public List<InventoryStack> Stacks = new List<InventoryStack>();
        /// <summary>
        /// The list of InventoryStack objects where all hidden items are stored
        /// </summary>
        public List<InventoryStack> HiddenStacks = new List<InventoryStack>();
        /// <summary>
        /// The class to managing the currencies. Use the following functions to manipulate it:
        /// Currency.GetCurrency, Currency.SetCurrency, Currency.AddCurrency
        /// </summary>
        public CurrencySet Currency = new CurrencySet();
        /// <summary>
        /// The list of the currency values.
        /// </summary>
        public List<int> CurrencyValue
        {
            get
            {
                return Currency.Currency;
            }
        }
        /// <summary>
        /// The size of the Stacks. Items will be rejected if the inventory is full.
        /// </summary>
        public int InventorySize = 10;
        /// <summary>
        /// The maximum carry weight for this InventoryHolder.
        /// </summary>
        public float MaxiumCarryWeight = 1000F;
        /// <summary>
        /// This multiplier is applied when selling items. For example, a merchant NPC with a SellPriceMultiplier of 1.2 will sell an item priced at 1000 for 1200.
        /// </summary>
        public float SellPriceMultiplier = 1F;
        /// <summary>
        /// This multiplier is applied when buying items. For example, a merchant NPC with a BuyPriceMultiplier of 0.8 will buy an item priced at 1000 for 800.
        /// </summary>
        public float BuyPriceMultiplier = 1F;
        /// <summary>
        /// A list to specific price multiplier for certain items.x=item id, y=sell price multiplier, z=buy price multiplier
        /// </summary>
        public List<Vector3> SpecificPriceMultiplier = new List<Vector3>();
        /// <summary>
        /// (For merchant only)When set to false, this merchant will accept items within the 'TradeList' and 'TradeCategoryList'.
        /// </summary>
        public bool TradeAllItems = true;
        /// <summary>
        /// (For merchant only)When 'TradeAllItems'set to false, this merchant will accept items within this list.
        /// </summary>
        public List<int> TradeList = new List<int>();
        /// <summary>
        /// (For merchant only)When 'TradeAllItems'set to false, this merchant will accept items from categories within this list.
        /// </summary>
        public List<int> TradeCategoryList = new List<int>();
        /// <summary>
        /// The base stats of this character without any equipment.
        /// </summary>
        public List<Attribute> BaseStats = new List<Attribute>();
        /// <summary>
        /// Enable/Disable the auto save function.
        /// </summary>
        public bool AutoSave = true;
        /// <summary>
        /// Enable/Disable the auto load function.
        /// </summary>
        public bool AutoLoad = true;
        /// <summary>
        /// The full path for auto save function.
        /// </summary>
        public string SavePath="";
        /// <summary>
        /// Temporary prevent the AutoSave function when set to "true".
        /// </summary>
        public bool BlockAutoSave = false;
        /// <summary>
        /// Callback function when an item is being used.
        /// </summary>
        /// <param name="_action"></param>
        /// <param name="_id"></param>
        /// <param name="_index"></param>
        public delegate void ItemUseCallback(string _action, int _id, int _index);
        private ItemUseCallback mItemUseCallbacks;
        /// <summary>
        /// Call back function when Items in this InventoryHolder have been changed.
        /// </summary>
        /// <param name="_changedItems"></param>
        public delegate void ItemChangeCallback(Dictionary<Item,int> _changedItems);
        /// <summary>
        /// Call back function when Item in this InventoryHolder has been dropped by dragging out the window.
        /// </summary>
        /// <param name="_droppedItem"></param>
        /// <param name="_number"></param>
        public delegate void ItemDropCallback(Item _droppedItem,int _number);
        private ItemChangeCallback mItemChangeCallbacks ;
        public ItemDropCallback mItemDropCallback;
        private CraftingUi mCraftingWindow;
        private float mCraftingProgress = 0F;
        private int mCraftingItemId;
        private int mCraftingItemNumber;
        private int mCraftedItemNumber;
        private bool mCraftingFailed = false;
        private bool mCrafting = false;
        private Coroutine mCraftingCo;
        private Coroutine SaveCo;
        private float _weight = 0F;
        
        #endregion

        #region Internal Methods
        private void Awake()
        {
            if (Type == HolderType.PlayerInventory)
            {
                if (ItemManager.PlayerInventoryHolder != null)
                {
                    Debug.LogError("Found duplicated PlayerInventoryHolder,  make sure to delete the old one before creating a new one.");
                    return;
                }
                ItemManager.PlayerInventoryHolder = this;
            }
            else if (Type == HolderType.PlayerEquipment)
            {
                if (ItemManager.PlayerEquipmentHolder != null)
                {
                    Debug.LogError("Found duplicated PlayerEquipmentHolder,  make sure to delete the old one before creating a new one.");
                    return;
                }
                ItemManager.PlayerEquipmentHolder = this;
                HoverInformation.SetCompareHolder(this);
            }
        }

        private IEnumerator Start()
        {
            yield return 1;//Wait one frame so your other script will have time to set the Save Data path for this component.
            Currency.Init();
            if (SavePath.Length > 0 && AutoLoad)//Check if the save path is null;
            {
                if (File.Exists(ItemManager.GetFullSavePath(SavePath)))//Check if the save file exist
                {
                    string _data = File.ReadAllText(ItemManager.GetFullSavePath(SavePath), System.Text.Encoding.UTF8);
                    Load(_data);
                }
            }
            RefreshItemData();
            Currency.AutoExchange();
            
        }


        /// <summary>
        /// Return the difference value of currency between this InventoryHolder and the target.
        /// </summary>
        /// <param name="_targetList"></param>
        /// <returns></returns>
        public List<int> GetCurrencyDifference(List<int> _targetList)// 
        {
            CurrencySet _source = new CurrencySet(Currency.Currency);
            _source.KeepPostive = false;
            _source.CollapseAllToLowestCurrenc();

            CurrencySet _target = new CurrencySet(_targetList);
            _target.CollapseAllToLowestCurrenc();

            int _maxLength = Mathf.Max(_target.Count, Currency.Count);
            for (int i = 0; i < _maxLength; i++)
            {
                _source.AddCurrency(i,-_target.GetCurrency(i));
            }
            return _source.Currency;
        }

        /// <summary>
        /// In case the saved data is outdated, pull newest settings from ItemManager by the uid of items.
        /// </summary>
        public void RefreshItemData()
        {
            for (int i = 0; i <Stacks.Count; i++)
            {
                if (!Stacks[i].Empty && Stacks[i].Item != null)
                {
                    int _uid = Stacks[i].GetItemId();
                    if (ItemManager.TryGetItem(_uid) != null)
                    {
                        int _level = Stacks[i].Item.upgradeLevel;
                        int _socketingCount = Stacks[i].Item.socketingSlots;
                        List<int> _socketedItems = new List<int>();
                        _socketedItems.AddRange(Stacks[i].Item.socketedItems);
                        List<int> _enchantments = new List<int>();
                        _enchantments.AddRange(Stacks[i].Item.enchantments);
                        Dictionary<string, bool> _attLock = new Dictionary<string, bool>();
                        Dictionary<string, string> _attValue = new Dictionary<string, string>();
                        foreach (var obj in Stacks[i].Item.attributes) {
                            if (!_attLock.ContainsKey(obj.key))
                            {
                                _attLock.Add(obj.key, obj.locked);
                                _attValue.Add(obj.key, obj.value);
                            } 
                        }
                        Stacks[i].Item = ItemManager.TryGetItem(_uid).Copy();
                        Stacks[i].Item.upgradeLevel = _level;
                        Stacks[i].Item.socketingSlots = _socketingCount;
                        Stacks[i].Item.socketedItems = _socketedItems;
                        Stacks[i].Item.enchantments = _enchantments;
                        foreach (var obj in Stacks[i].Item.attributes)
                        {
                            if (_attLock.ContainsKey(obj.key))
                            {
                                obj.locked = _attLock[obj.key];
                                obj.value= _attValue[obj.key];
                                _attLock.Remove(obj.key);
                            }
                        }
                        _attLock.Clear();
                        _attValue.Clear();
                    }
                    else
                    {
                        Stacks[i].Delete();
                    }
                }
            }
            for (int i = 0; i < HiddenStacks.Count; i++)
            {
                if (!HiddenStacks[i].Empty && HiddenStacks[i].Item != null)
                {
                    int _uid = HiddenStacks[i].GetItemId();
                    if (ItemManager.TryGetItem(_uid) != null)
                    {
                        int _level = HiddenStacks[i].Item.upgradeLevel;
                        int _socketingCount = HiddenStacks[i].Item.socketingSlots;
                        List<int> _socketedItems = new List<int>();
                        _socketedItems.AddRange(HiddenStacks[i].Item.socketedItems);
                        List<int> _enchantments = new List<int>();
                        _enchantments.AddRange(HiddenStacks[i].Item.enchantments);
                        Dictionary<string, bool> _attLock = new Dictionary<string, bool>();
                        Dictionary<string, string> _attValue = new Dictionary<string, string>();
                        foreach (var obj in HiddenStacks[i].Item.attributes)
                        {
                            if (!_attLock.ContainsKey(obj.key))
                            {
                                _attLock.Add(obj.key, obj.locked);
                                _attValue.Add(obj.key, obj.value);
                            }
                        }
                        HiddenStacks[i].Item = ItemManager.TryGetItem(_uid).Copy();
                        HiddenStacks[i].Item.upgradeLevel = _level;
                        HiddenStacks[i].Item.socketingSlots = _socketingCount;
                        HiddenStacks[i].Item.socketedItems = _socketedItems;
                        HiddenStacks[i].Item.enchantments = _enchantments;
                        foreach (var obj in HiddenStacks[i].Item.attributes)
                        {
                            if (_attLock.ContainsKey(obj.key))
                            {
                                obj.locked = _attLock[obj.key];
                                obj.value = _attValue[obj.key];
                                _attLock.Remove(obj.key);
                            }
                        }
                        _attLock.Clear();
                        _attValue.Clear();
                    }
                    else
                    {
                        HiddenStacks[i].Delete();
                    }
                }
            }
            if (Currency.Count != ItemManager.instance.currencies.Count)
            {
                List<int> _temp = new List<int>();
                _temp.AddRange(Currency.GetCurrencyArray());
                Currency.Reset();
                for (int i=0;i< ItemManager.instance.currencies.Count;i++) {
                    if (i < _temp.Count)
                        Currency.Add(_temp[i]);
                    else
                        Currency.Add(0);
                }
            }

        }

        


        IEnumerator SaveCoroutine()
        {
            yield return new WaitForSecondsRealtime(1F);//Wait for one second to prevent unnecessary multiple save actions within a few frames.
            Save();
        }

        public void StartCrafting(int _itemId,int _number, List<Vector2> _materials, float _craftingTime)
        {
            if (mCraftingCo != null) StopCoroutine(mCraftingCo);
            mCraftingItemId = _itemId;
            mCraftingItemNumber = _number;
            mCraftingFailed = false;
            mCraftedItemNumber = 0;
            mCraftingCo =StartCoroutine(CraftingProcess(_itemId,_number,_materials,_craftingTime));
        }

        IEnumerator CraftingProcess(int _itemId, int _number, List<Vector2> _materials, float _craftingTime)
        {
            mCrafting = true;
            for (int u = 0; u < _number; u++)
            {
                mCraftingProgress = 0F;
                while (mCraftingProgress < 1F)
                {
                    yield return 1;
                    mCraftingProgress = Mathf.MoveTowards(mCraftingProgress, 1F, Time.unscaledDeltaTime * (1F / _craftingTime));
                }
                
                bool _goodToGo = true;
                for (int i = 0; i < _materials.Count; i++)
                {
                    if (GetItemNumber(Mathf.FloorToInt(_materials[i].x)) < Mathf.FloorToInt(_materials[i].y)) _goodToGo = false;
                }
                if (_goodToGo)
                {
                    for (int i = 0; i < _materials.Count; i++)
                    {
                        RemoveItem(Mathf.FloorToInt(_materials[i].x), Mathf.FloorToInt(_materials[i].y));
                    }
                    InventoryStack _leftStack = AddItem(new Item(_itemId,true,true), 1);
                    if (_leftStack.isEmpty())
                    {
                        mCraftedItemNumber++;
                        mCraftingFailed = false;
                    }
                    else
                    {
                        DynamicMsg.PopMsg(ItemManager.instance.msgBagFull);
                        mCraftingFailed = true;
                        u = _number;
                    }
                }
                else
                {
                    mCraftingFailed = true;
                    yield return 1;
                    mCrafting = false;
                    yield break;
                }
                yield return 1;
            }
            if(Type== HolderType.PlayerInventory && mCraftedItemNumber>0) DynamicMsg.PopItem(ItemManager.TryGetItem(_itemId), mCraftedItemNumber);
            yield return 1;
            mCrafting = false;
        }

        public void CalWeight()
        {
            _weight = 0F;
            for (int i = 0; i < Stacks.Count; i++) _weight += Stacks[i].GetWeight();
        }

        public void UpdatePrefab()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        #endregion

        /// <summary>
        /// Works similar with GetComponent, but only return the InventoryHolder Component of specified HolderType of the providing GameObject.
        /// </summary>
        /// <param name="_gameObject"></param>
        /// <param name="_type"></param>
        /// <returns></returns>
        public static InventoryHolder GetInventoryHolderByType(GameObject _gameObject, HolderType _type) //
        {
            InventoryHolder[] _holders = _gameObject.GetComponents<InventoryHolder>();
            foreach (var obj in _holders) {
                if (obj.Type == _type) return obj;
            }
            return GetInventoryHolderInChildrenByType(_gameObject, _type);
        }

        /// <summary>
        /// Works similar with GetComponentInChildren, but only return the InventoryHolder Component of specified HolderType of the providing GameObject's children.
        /// </summary>
        /// <param name="_gameObject"></param>
        /// <param name="_type"></param>
        /// <returns></returns>
        public static InventoryHolder GetInventoryHolderInChildrenByType(GameObject _gameObject, HolderType _type)
        {
            InventoryHolder[] _holders = _gameObject.GetComponentsInChildren<InventoryHolder>();
            foreach (var obj in _holders)
            {
                if (obj.Type == _type) return obj;
            }
            return null;
        }

        /// <summary>
        /// Returns if a crafting job is processing.
        /// </summary>
        /// <returns>true = a crafting job is processing   |   false = no crafting job is procssing</returns>
        public bool isCrafting()
        { return mCrafting; }

        /// <summary>
        /// Returns the crafting item id.
        /// </summary>
        /// <returns> returns -1 if there is no crafting job is processing.</returns>
        public int GetCraftingItemId()
        { return mCrafting?mCraftingItemId:-1; }

        /// <summary>
        /// Returns the crafting item number. 
        /// </summary>
        /// <returns>Returns 0 if there is no crafting job is processing.</returns>
        public int GetCraftingItemNumber()
        { return mCrafting ? mCraftingItemNumber :0; }

        /// <summary>
        /// Returns the crafted item number. 
        /// </summary>
        /// <returns>Returns 0 if there is no crafting job is processing.</returns>
        public int GetCraftedItemNumber()
        { return mCrafting ? mCraftedItemNumber : 0; }

        /// <summary>
        /// Returns the crafting progress(0~1). 
        /// </summary>
        /// <returns>Returns 0 if there is no crafting job is processing.</returns>
        public float GetCraftingProgress()
        { return mCrafting ? mCraftingProgress : 0; }

        /// <summary>
        /// Returns true if the crafting failed.
        /// </summary>
        /// <returns></returns>
        public bool GetCraftingFailed()
        { return mCraftingFailed; }


        /// <summary>
        /// Opens the appropriate interface based on the type of this InventoryHolder.
        /// </summary>
        /// <returns></returns>
        public UiWindow OpenWindow()
        {
            if (this == null)
            {
                Debug.LogError("The InventoryHolder you are trying to access is missing.");
                return null;
            }
            UiWindow _newWindow = null;
            switch (Type)
            {
                case HolderType.PlayerInventory:
                    _newWindow = WindowsManager.GetWindow("Inventory",this);
                    if (_newWindow != null) _newWindow.Initialize(this, ItemManager.PlayerEquipmentHolder, Name);
                    break;
                case HolderType.PlayerEquipment:
                    _newWindow = WindowsManager.GetWindow("Equipment", this);
                    if (_newWindow != null) _newWindow.Initialize(ItemManager.PlayerInventoryHolder, this, Name);
                    break;
                case HolderType.Merchant:
                    _newWindow = WindowsManager.GetWindow("Merchant"+(ItemManager.instance.MerchantStyle>0? ItemManager.instance.MerchantStyle.ToString():""), this);
                    if (_newWindow != null) _newWindow.Initialize(ItemManager.PlayerInventoryHolder, this, Name);
                    break;
                case HolderType.Crate:
                    _newWindow = WindowsManager.GetWindow("Storage", this);
                    if (_newWindow != null) _newWindow.Initialize( this, null,Name);
                    break;
                case HolderType.NpcInventory:
                    _newWindow = WindowsManager.GetWindow("NpcInventory", this);
                    if (_newWindow != null) _newWindow.Initialize(this, null, Name);
                    break;
                case HolderType.NpcEquipment:
                    InventoryHolder _inventory = null;
                    foreach (var obj in GetComponents<InventoryHolder>()) {
                        if (obj.Type == HolderType.NpcInventory) _inventory = obj;
                    }
                    if (_inventory == null)
                    {
                        _inventory = ItemManager.PlayerInventoryHolder;
                    }
                    _newWindow = WindowsManager.GetWindow("Equipment", this);
                    if (_newWindow != null)
                    {
                        _newWindow.Initialize(_inventory, this, Name);
                        _newWindow.GetComponent<EquipmentUi>().SetPlayerName(Name);
                        _newWindow.GetComponent<EquipmentUi>().SetPlayerLevelText("<NPC>");
                    }
                    break;
            }
            return _newWindow;
        }

        /// <summary>
        /// Close the opened window associated with this InventoryHolder.
        /// </summary>
        public void CloseWindow()
        {
            if(WindowsManager.getOpenedWindow(this)!=null) WindowsManager.getOpenedWindow(this).Close();
        }

  
        /// <summary>
        /// Opens the crafting window for this InventoryHolder, using the items in Stacks as materials.
        /// </summary>
        /// <returns></returns>
        public UiWindow OpenForgeWindow(bool _enableCrafting=true, bool _enableEnhancing = true, bool _enableEnchanting=true, bool _enableSocketing=true,float _craftingTimeMultiplier=1F,string _name="Forge")
        {
            UiWindow _newWindow = WindowsManager.GetWindow("Forge", this);
            if (_newWindow != null)
            {
                _newWindow.GetComponent<CraftingUi>().EnableCrafting = _enableCrafting;
                _newWindow.GetComponent<CraftingUi>().EnableEnhancing = _enableEnhancing;
                _newWindow.GetComponent<CraftingUi>().EnableEnchanting = _enableEnchanting;
                _newWindow.GetComponent<CraftingUi>().EnableSocketing = _enableSocketing;
                _newWindow.GetComponent<CraftingUi>().CraftingTimeMultiplier = _craftingTimeMultiplier;
                mCraftingWindow = _newWindow.GetComponent<CraftingUi>();
                mCraftingWindow.Initialize(this, null, _name);
            }
            else
            {
                mCraftingWindow = null;
            }
            return _newWindow;
        }


        /// <summary>
        /// Opens a window prefab inheriting from UiWindow.cs and sets its title.
        /// </summary>
        /// <param name="_prefabName"></param>
        /// <param name="_displayName"></param>
        /// <returns></returns>
        public UiWindow OpenWindowByName(string _prefabName,string _displayName)
        {
            UiWindow _newWindow = WindowsManager.GetWindow(_prefabName, this);
            if (_newWindow != null) _newWindow.Initialize(this, null, _displayName);
            return _newWindow;
        }

        /// <summary>
        /// Retrieves the base stats value by the attribute ScriptKey.
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public float GetBaseStatsValue(string _key)
        {
            foreach (var obj in BaseStats) {
                if (obj.key == _key) return obj.GetFloat();
            }
            return 0F;
        }
        /// <summary>
        /// Retrieves the base stats string value by the attribute ScriptKey.
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public string GetBaseStatsString(string _key)
        {
            foreach (var obj in BaseStats)
            {
                if (obj.key == _key) return obj.GetString();
            }
            return "";
        }

        /// <summary>
        /// Add value to the base stats value by the attribute ScriptKey. Return the final result. Becareful to not frequently call this function with '_save' set to true.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_addValue"></param>
        /// <returns></returns>
        public float AddBaseStatsValue(string _key,float _addValue,bool _save=true)
        {
            foreach (var obj in BaseStats)
            {
                if (obj.key == _key)
                {
                    obj.UpdateValue(obj.GetFloat()+ _addValue);
                    if(_save) CheckAutoSave();
                    return obj.GetFloat();
                }
            }
            return 0F;
        }

        /// <summary>
        /// Add value to the base stats value by the attribute ScriptKey.Return the final result. Becareful to not frequently call this function with '_save' set to true.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_addValue"></param>
        /// <returns></returns>
        public int AddBaseStatsValue(string _key, int _addValue, bool _save = true)
        {
            foreach (var obj in BaseStats)
            {
                if (obj.key == _key)
                {
                    obj.UpdateValue(obj.GetInt() + _addValue);
                    if (_save) CheckAutoSave();
                    return obj.GetInt();
                }
            }
            return 0;
        }

        /// <summary>
        /// Override the base stats value by the attribute ScriptKey.Becareful to not frequently call this function with '_save' set to true.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void SetBaseStatsValue(string _key,string _value, bool _save = true)
        {
            foreach (var obj in BaseStats)
            {
                if (obj.key == _key) obj.UpdateValue(_value);
            }
            if (_save) CheckAutoSave();
        }
        /// <summary>
        /// Set the base stats value by the attribute ScriptKey. Becareful to not frequently call this function with '_save' set to true.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void SetBaseStatsValue(string _key, int _value, bool _save = true)
        {
            foreach (var obj in BaseStats)
            {
                if (obj.key == _key) obj.UpdateValue(_value);
            }
            if (_save) CheckAutoSave();
        }
        /// <summary>
        /// Set the base stats value by the attribute ScriptKey. Becareful to not frequently call this function with '_save' set to true.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void SetBaseStatsValue(string _key, float _value, bool _save = true)
        {
            foreach (var obj in BaseStats)
            {
                if (obj.key == _key) obj.UpdateValue(_value);
            }
            if (_save) CheckAutoSave();
        }


        /// <summary>
        /// Retrieves the total value of an attribute by its ScriptKey from all equipped items and the base stats. 
        /// </summary>
        /// <param name="_attributeKey"></param>
        /// <returns></returns>
        public float GetAttributeValue(string _attributeKey,bool _includeBaseStats=false)
        {
            float _value = 0F;
            if (_includeBaseStats)
            {
                foreach (var obj in BaseStats)
                {
                    if (obj.key == _attributeKey && obj.isNumber()) _value += obj.GetFloat();
                }
            }
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (!Stacks[i].isEmpty())
                {
                    _value += Stacks[i].Item.GetAttributeFloat(_attributeKey);
                }
            }
            return _value;
        }



        /// <summary>
        /// Retrieves the total value of an attribute by its ScriptKey, counting only equipment with matching tags.
        /// </summary>
        /// <param name="_attributeKey"></param>
        /// <param name="_tags"></param>
        /// <returns></returns>
        public float GetAttributeValueByTag(string _attributeKey, List<string> _tags)
        {
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (!Stacks[i].isEmpty() && Stacks[i].Item.isTagsMatchList(_tags,false))
                {
                    return Stacks[i].Item.GetAttributeFloat(_attributeKey);
                }
            }
            return 0F;
        }

        /// <summary>
        /// Retrieves the number of items with a specific UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetItemNumber(int _uid)
        {
            int _result = 0;
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (Stacks[i].isSameItem(_uid))
                {
                    _result += Stacks[i].Number;
                }
            }
            for (int i = 0; i < HiddenStacks.Count; i++)
            {
                if (HiddenStacks[i].isSameItem(_uid))
                {
                    _result += HiddenStacks[i].Number;
                }
            }
            return _result;
        }


        /// <summary>
        /// Retrieves the number of items with a specific UID, but only counts the items with the highest upgrade level if there are multiple items with the same UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetItemNumberWithHighestUpgradeLevel(int _uid)
        {
            int _result = 0;
            int _level = GetHighestUpgradeLevel(_uid);
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (Stacks[i].isSameItem(_uid) && Stacks[i].GetUpgradeLevel() >= _level)
                {
                    _result += Stacks[i].Number;
                }
            }
            for (int i = 0; i < HiddenStacks.Count; i++)
            {
                if (HiddenStacks[i].isSameItem(_uid) && HiddenStacks[i].GetUpgradeLevel() >= _level)
                {
                    _result += HiddenStacks[i].Number;
                }
            }
            return _result;
        }

        /// <summary>
        /// Retrieves the highest upgrade level of the item with a specific UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public int GetHighestUpgradeLevel(int _uid)
        {
            int _level = 0;
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (Stacks[i].isSameItem(_uid) && Stacks[i].GetUpgradeLevel() > _level)
                {
                    _level = Stacks[i].GetUpgradeLevel();
                }
            }
            return _level;
        }

        /// <summary>
        /// Returns how many more items with the specified UID can be stacked in this InventoryHolder. Set a cap number to improve performance.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_maxmiumNumber"></param>
        /// <returns></returns>
        public int GetAvailableSpace(int _uid, int _maxmiumNumber = 999)
        {
            if (ItemManager.TryGetItem(_uid)==null)
            {
                Debug.LogError("Trying to access item with invalid item uid.");
                return 0;
            }
            int _result = 0;
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (Stacks[i].isSameItem(_uid))
                {
                    _result += Stacks[i].GetAvailableSpace();
                }
                else if (Stacks[i].isEmpty())
                {
                    _result += ItemManager.TryGetItem(i).maxiumStack;
                }

                if (_result >= _maxmiumNumber) break;
            }
            return _result;
        }


        /// <summary>
        /// Retrieves the currency value by its index number.Set _includeExchangedValue to true if you want to get the total amount of this currency including the exchanged value from other currencies.
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_includeExchangedValue"></param>
        /// <returns></returns>
        public int GetCurrency(int _type, bool _includeExchangedValue=false)
        {
            return Currency.GetCurrency(_type, _includeExchangedValue);
        }

 
        /// <summary>
        /// Adds to the currency value by its index number. The _add value can be negative.
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_add"></param>
        public void AddCurrency(int _type,int _add)
        {
            Currency.AddCurrency(_type,_add);
        }

        /// <summary>
        /// Overrides the currency value by its index number.
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_value"></param>
        public void SetCurrency(int _type, int _value)
        {
            Currency.SetCurrency(_type, _value);
        }

        /// <summary>
        /// Check if auto save should perform
        /// </summary>
        public void CheckAutoSave()
        {
            if (AutoSave && !BlockAutoSave)
            {
                if (SaveCo != null) StopCoroutine(SaveCo);
                SaveCo = StartCoroutine(SaveCoroutine());
            }
        }

        /// <summary>
        /// Save the data to the "SavePath".
        /// </summary>
        public void Save()
        {
            if(SavePath.Length>0) File.WriteAllText(ItemManager.GetFullSavePath(SavePath), GetSaveDataJsonString(), System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Returns a JSON string of all the item data in this InventoryHolder.
        /// </summary>
        /// <returns></returns>
        public string GetSaveDataJsonString()
        {
            ItemSave[] _saveItems = new ItemSave[Stacks.Count];
            for (int i = 0; i < _saveItems.Length; i++)
            {
                _saveItems[i] = new ItemSave();
                if (Stacks[i].isEmpty())
                {
                    _saveItems[i].uid = -1;
                    _saveItems[i].upgrade = 0;
                    _saveItems[i].enchantments = new int[0];
                    _saveItems[i].socketedItem = new int[0];
                    _saveItems[i].number = 0;
                    _saveItems[i].attributeLock = new bool[0];
                    _saveItems[i].attributeValue = new string[0];
                    _saveItems[i].fav = false;
                }
                else
                {
                    _saveItems[i].uid = Stacks[i].Item.uid;
                    _saveItems[i].upgrade = Stacks[i].Item.upgradeLevel;
                    _saveItems[i].enchantments= Stacks[i].Item.enchantments.ToArray();
                    _saveItems[i].socketedItem = Stacks[i].Item.socketedItems.ToArray();
                    _saveItems[i].number = Stacks[i].Number;
                    _saveItems[i].attributeLock = new bool[Stacks[i].Item.attributes.Count];
                    _saveItems[i].attributeValue = new string[Stacks[i].Item.attributes.Count];
                    for (int u = 0; u < Stacks[i].Item.attributes.Count; u++)
                    {
                        _saveItems[i].attributeLock[u] = Stacks[i].Item.attributes[u].locked;
                        _saveItems[i].attributeValue[u] = Stacks[i].Item.attributes[u].value;
                    }
                    _saveItems[i].fav = Stacks[i].Item.favorite;
                }
            }
            ItemSave[] _hiddenItems = new ItemSave[HiddenStacks.Count];
            for (int i = 0; i < _hiddenItems.Length; i++)
            {
                _hiddenItems[i] = new ItemSave();
                if (HiddenStacks[i].isEmpty())
                {
                    _hiddenItems[i].uid = -1;
                    _hiddenItems[i].upgrade = 0;
                    _hiddenItems[i].enchantments = new int[0];
                    _hiddenItems[i].socketedItem = new int[0];
                    _hiddenItems[i].number = 0;
                    _hiddenItems[i].attributeLock = new bool[0];
                    _hiddenItems[i].attributeValue = new string[0];
                    _hiddenItems[i].fav = false;
                }
                else
                {
                    _hiddenItems[i].uid = HiddenStacks[i].Item.uid;
                    _hiddenItems[i].upgrade = HiddenStacks[i].Item.upgradeLevel;
                    _hiddenItems[i].enchantments = HiddenStacks[i].Item.enchantments.ToArray();
                    _hiddenItems[i].socketedItem = HiddenStacks[i].Item.socketedItems.ToArray();
                    _hiddenItems[i].number = HiddenStacks[i].Number;
                    _hiddenItems[i].attributeLock = new bool[HiddenStacks[i].Item.attributes.Count];
                    _hiddenItems[i].attributeValue = new string[HiddenStacks[i].Item.attributes.Count];
                    for (int u = 0; u < HiddenStacks[i].Item.attributes.Count; u++)
                    {
                        _hiddenItems[i].attributeLock[u] = HiddenStacks[i].Item.attributes[u].locked;
                        _hiddenItems[i].attributeValue[u] = HiddenStacks[i].Item.attributes[u].value;
                    }
                    _hiddenItems[i].fav = HiddenStacks[i].Item.favorite;
                }
            }
            int[] _currency = Currency.GetCurrencyArray();
            ItemSaveRoot _saveRoot = new ItemSaveRoot();
            _saveRoot.items = _saveItems;
            _saveRoot.hiddenItems = _hiddenItems;
            _saveRoot.currency = _currency;
            if (BaseStats.Count>0 && (Type == HolderType.PlayerEquipment || Type == HolderType.NpcEquipment))
            {
                _saveRoot.baseAttributeKey = new string[BaseStats.Count];
                _saveRoot.baseAttributeValue = new string[BaseStats.Count];
                for (int i=0;i< BaseStats.Count;i++) {
                    _saveRoot.baseAttributeKey[i] = BaseStats[i].key;
                    _saveRoot.baseAttributeValue[i] = BaseStats[i].value;
                }
            }
            else
            {
                _saveRoot.baseAttributeKey = new string[0];
                _saveRoot.baseAttributeValue = new string[0];
            }
            return JsonUtility.ToJson(_saveRoot);
        }

        /// <summary>
        /// Loads item data into this InventoryHolder with the "Save Data Path" set in the inspector.
        /// </summary>
        public void Load()
        {
            LoadByPath(ItemManager.GetFullSavePath(SavePath));
        }


        /// <summary>
        /// Loads item data into this InventoryHolder from a JSON string.
        /// </summary>
        /// <param name="_data"></param>
        public void Load(string _data)
        {
            ItemSaveRoot _saveRoot = JsonUtility.FromJson<ItemSaveRoot>(_data);
            Stacks.Clear();
            HiddenStacks.Clear();
            for (int i = 0; i < _saveRoot.items.Length; i++)
            {
                if (ItemManager.TryGetItem(_saveRoot.items[i].uid)!=null)
                {
                    Stacks.Add(new InventoryStack(_saveRoot.items[i]));
                } 
                else {
                    Stacks.Add ( new InventoryStack());
                }
            }
            for (int i = 0; i < _saveRoot.hiddenItems.Length; i++)
            {
                if (ItemManager.TryGetItem(_saveRoot.hiddenItems[i].uid)!=null)
                {
                    HiddenStacks.Add(new InventoryStack(_saveRoot.hiddenItems[i]));
                }
                else
                {
                    HiddenStacks.Add(new InventoryStack());
                }
            }
            Currency.Reset();
            Currency.Init(_saveRoot.currency);

            if ((Type== HolderType.PlayerEquipment || Type== HolderType.NpcEquipment) 
                &&_saveRoot.baseAttributeKey!=null && _saveRoot.baseAttributeValue!=null 
                && _saveRoot.baseAttributeKey.Length>0 && _saveRoot.baseAttributeValue.Length > 0 && _saveRoot.baseAttributeValue.Length== _saveRoot.baseAttributeKey.Length) {
                Dictionary<string, string> _keyValue = new Dictionary<string, string>();
                for (int i = 0; i < _saveRoot.baseAttributeKey.Length; i++)
                {
                    if (!_keyValue.ContainsKey(_saveRoot.baseAttributeKey[i])) _keyValue.Add(_saveRoot.baseAttributeKey[i], _saveRoot.baseAttributeValue[i]);
                }
                for (int i = 0; i < BaseStats.Count; i++) {
                    if (_keyValue.ContainsKey(BaseStats[i].key)) {
                        BaseStats[i].value = _keyValue[BaseStats[i].key];
                        _keyValue.Remove(BaseStats[i].key);
                    }
                }
                foreach (var key in _keyValue.Keys) {
                    if (ItemManager.instance.GetAtttibute(key) != null) {
                        Attribute _newAtt = ItemManager.instance.GetAtttibute(key).Copy();
                        _newAtt.value = _keyValue[key];
                        BaseStats.Add(_newAtt);
                    }
                }
            }
        }


        /// <summary>
        /// Loads item data into this InventoryHolder by a full path of the save file.
        /// </summary>
        /// <param name="_path"></param>
        public void LoadByPath(string _path)
        {
            if (File.Exists(_path))//Check if the save file exist
            {
                string _data = File.ReadAllText(_path, System.Text.Encoding.UTF8);
                Load(_data);
            }
            else
            {
                Debug.LogError("Trying to load a file doesn't not exist.");
            }
        }

        /// <summary>
        /// Whenever you manually change the item data of an InventoryHolder, be sure to call ItemChanged() to inform other scripts to update linked data.
        /// </summary>
        /// <param name="_changedItems"></param>
        public void ItemChanged(Dictionary<Item, int> _changedItems)
        {
            CalWeight();
            if(mItemChangeCallbacks!=null) mItemChangeCallbacks(_changedItems);
            CheckAutoSave();
        }



        /// <summary>
        /// Captures a snapshot of the current data state of this InventoryHolder. You can revert to this snapshot later if needed.
        /// </summary>
        /// <returns></returns>
        public InventorySnapShot GetSnapShot()
        {
            return new InventorySnapShot(this);
        }

        /// <summary>
        /// Reverts all item data to the provided snapshot.
        /// </summary>
        /// <param name="_snapshot"></param>
        public void RevertSnapShot(InventorySnapShot _snapshot)
        {
            for (int i = 0; i < Stacks.Count; i++)
            {
                Stacks[i].Set(_snapshot.Stacks[i]);
            }
            for (int i = 0; i < HiddenStacks.Count; i++)
            {
                HiddenStacks[i].Set(_snapshot.HiddenStacks[i]);
            }
            Currency = _snapshot.Currency.Copy();
            _weight = _snapshot.Weight;
        }

        /// <summary>
        /// Registers a callback that is triggered when an item from this InventoryHolder is used. Remember to call UnRegisterItemUseCallback() when the game object of your script is destroyed.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterItemUseCallback(ItemUseCallback _callback)
        {
            mItemUseCallbacks+= _callback;
        }


        /// <summary>
        /// Unregisters the callback. It¡¯s best to call this in OnDestroy() when the callback receiver is destroyed.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterItemUseCallback(ItemUseCallback _callback)
        {
            try
            {
                mItemUseCallbacks -= _callback;
            }
            catch
            {

            }
        }

  
        /// <summary>
        /// Clears all registered callbacks for item use.
        /// </summary>
        public void ClearAllItemUseCallback()
        {
            mItemUseCallbacks=null;
        }


        /// <summary>
        /// Registers a callback that is triggered when items in this InventoryHolder are changed. Remember to call UnRegisterItemChangeCallback() when the game object of your script is destroyed.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterItemChangeCallback(ItemChangeCallback _callback)
        {
            mItemChangeCallbacks+=_callback;
        }


        /// <summary>
        /// Unregisters the callback. It¡¯s best to call this in OnDestroy() when the callback receiver is destroyed.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterItemChangeCallback(ItemChangeCallback _callback)
        {
            try
            {
                mItemChangeCallbacks -= _callback;
            }
            catch
            {

            }
        }

        /// <summary>
        /// Clears all registered callbacks for item changes.
        /// </summary>
        public void ClearAllItemChangeCallback()
        {
            mItemChangeCallbacks = null;
        }

        /// <summary>
        /// Registers a callback that is triggered when items in this InventoryHolder has been dropped by dragging out of the window. Remember to call UnRegisterItemDropCallback() when the game object of your script is destroyed.
        /// </summary>
        /// <param name="_callback"></param>
        public void RegisterItemDropCallback(ItemDropCallback _callback)
        {
            mItemDropCallback += _callback;
        }


        /// <summary>
        /// Unregisters the callback. It¡¯s best to call this in OnDestroy() when the callback receiver is destroyed.
        /// </summary>
        /// <param name="_callback"></param>
        public void UnRegisterItemDropCallback(ItemDropCallback _callback)
        {
            try
            {
                mItemDropCallback -= _callback;
            }
            catch
            {

            }
        }


        /// <summary>
        /// Clears all registered callbacks for item drop.
        /// </summary>
        public void ClearAllItemDropCallback()
        {
            mItemDropCallback = null;
        }


        /// <summary>
        /// Clears all normal items in this InventoryHolder.
        /// </summary>
        public void ClearInventoryItems()
        {
            Dictionary<Item, int> _changedItems = new Dictionary<Item, int>();
            foreach (var obj in Stacks) {
                if (!obj.isEmpty()) _changedItems.Add(obj.Item, -obj.Number);
            }
            Stacks.Clear();
            for (int i = 0; i < InventorySize; i++)
            {
                Stacks.Add(new InventoryStack());
            }
            ItemChanged(_changedItems);
        }

        /// <summary>
        /// Clears all hidden items in this InventoryHolder.
        /// </summary>
        public void ClearHiddenItems()
        {
            HiddenStacks.Clear();
        }

 
        /// <summary>
        /// Retrieves the total weight of all items in this InventoryHolder (hidden items are not counted).
        /// </summary>
        /// <returns></returns>
        public float GetWeight()
        {
            return _weight;
        }


        /// <summary>
        /// Equip an item from _inventoryHolder to _equipmentHolder by specific item uid and slot index of inventory.
        /// </summary>
        /// <param name="_inventoryHolder"></param>
        /// <param name="_equipmentHolder"></param>
        /// <param name="_itemId"></param>
        /// <param name="_inventorySlotIndex"></param>
        public static void EquipItem(InventoryHolder _inventoryHolder,InventoryHolder _equipmentHolder,int _itemId, int _inventorySlotIndex)
        {
            EquipmentUi _equipment = Resources.Load<GameObject>("InventoryEngine/UiWindows/Equipment").GetComponent<EquipmentUi>();

            foreach (InventoryItem item in _equipment.EquipItems)
            {
                if ( ItemManager.TryGetItem(_itemId).isTagMatchText(item.LimitedByTag))
                {
                    if (_inventorySlotIndex != -1)
                    {
                        if (_inventoryHolder.Stacks[_inventorySlotIndex].GetItemId() == _itemId)
                        {
                            int _matchIndex = -1;
                            int _emptyIndex = -1;
                           
                            for (int i=0;i< _equipmentHolder.Stacks.Count;i++)
                            {
                                if (_equipmentHolder.Stacks[i].isTagMatchText(item.LimitedByTag) && _matchIndex == -1)
                                {
                                    _matchIndex = i;
                                } else if (_equipmentHolder.Stacks[i].isEmpty() && _emptyIndex==-1) {
                                    _emptyIndex = i;
                                }

                            }
                            if (_matchIndex != -1 || _emptyIndex != -1)
                            {
                                 Dictionary<Item, int> _swapDic = new Dictionary<Item, int>();
                                 Dictionary<Item, int> _equipDic = new Dictionary<Item, int>();
                                _swapDic.Add(_inventoryHolder.Stacks[_inventorySlotIndex].Item,-1);
                                _equipDic.Add(_inventoryHolder.Stacks[_inventorySlotIndex].Item, 1);
                                _inventoryHolder.Stacks[_inventorySlotIndex].Set(_equipmentHolder.Stacks[_matchIndex != -1 ? _matchIndex : _emptyIndex].Merge(_inventoryHolder.Stacks[_inventorySlotIndex]));
                                _equipmentHolder.ItemChanged(_equipDic);
                                _inventoryHolder.ItemChanged(_swapDic);
                                return;
                            }
                            
                        }
                       
                    }
                }
            }
           
        }


        /// <summary>
        /// Adds a specified number of items. Returns any items that could not be received (if any).
        /// </summary>
        /// <param name="_item"></param>
        /// <param name="_number"></param>
        /// <returns></returns>
        public InventoryStack AddItem(Item _item, int _number = 1)
        {
            if (!_item.visible)
            {
                return AddHiddenItem(_item, _number);
            }

            InventoryStack _newStack = new InventoryStack(_item, _number);
            //First find if there is any slot has same item to stack.
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (_newStack.Number > 0)
                {
                    if (Stacks[i].isSameItem(_item.uid,_item.upgradeLevel,_item.enchantments,_item.socketedItems))
                    {
                       if(Stacks[i].Number< Stacks[i].Item.maxiumStack) _newStack = Stacks[i].Merge(_newStack);
                    }
                }
                else
                {
                    break;
                }
            }

            //Find empty slots to stack the rest of the items.
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (_newStack.Number > 0)
                {
                    if (Stacks[i].isEmpty())
                    {
                        _newStack = Stacks[i].Merge(_newStack);
                    }
                }
                else
                {
                    break;
                }
            }
            Dictionary<Item, int> _changedItems = new Dictionary<Item, int>();
            _changedItems.Add(_item, _number);
            ItemChanged(_changedItems);
            //return the items not been able to recieve due to the bag size. return 0 number stack if all items recieved
            if (_newStack.Number <= 0) {
                _newStack.Empty = true;
                _newStack.Item = null;
            }
            return _newStack;
        }


        /// <summary>
        /// Adds a specified number of hidden items. Returns any items that could not be received (if any).
        /// </summary>
        /// <param name="_item"></param>
        /// <param name="_number"></param>
        /// <returns></returns>
        public InventoryStack AddHiddenItem(Item _item, int _number = 1)
        {
            InventoryStack _newStack = new InventoryStack(_item, _number);
            //First find if there is any slot has same item to stack.
            bool _foundSame = false;
            for (int i = 0; i < HiddenStacks.Count; i++){
                if (_newStack.Number > 0)
                {
                    if (HiddenStacks[i].isSameItem(_item.uid, _item.upgradeLevel,_item.enchantments,_item.socketedItems))
                    {
                        _newStack = HiddenStacks[i].Merge(_newStack);
                        _foundSame = true;
                    }
                }
                else
                {
                    break;
                }
            }
            //Add new stack if we did not found any slot has same item .
            if (!_foundSame)
            {
                _newStack.Number = Mathf.Min(_newStack.Number,_item.maxiumStack);
                HiddenStacks.Add(_newStack.Copy());
                _newStack.Number = Mathf.Max(0,_number- _newStack.Number);
            }
            Dictionary<Item, int> _changedItems = new Dictionary<Item, int>();
            _changedItems.Add(_item, _number);
            ItemChanged(_changedItems);
            return _newStack;
        }


        /// <summary>
        /// Moves an item from one index to another.
        /// </summary>
        /// <param name="sourceIndex"></param>
        /// <param name="_targetIndex"></param>
        public void MoveItem(int sourceIndex, int _targetIndex)
        {
            Stacks[sourceIndex] = Stacks[_targetIndex].Merge(Stacks[sourceIndex]);
        }

  
        /// <summary>
        /// Splits the stack and returns the split stack.
        /// </summary>
        /// <param name="sourceIndex"></param>
        /// <param name="_number"></param>
        /// <returns></returns>
        public InventoryStack Split(int sourceIndex, int _number)
        {
            //return the temp InventoryStack took from the source
            return Stacks[sourceIndex].Split(_number);
        }


        /// <summary>
        /// Deletes an item with a specific UID, upgrade level, socketing and enchantments.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_upgradeLevel"></param>
        /// <param name="_enchantments"></param>
        /// <param name="_sockets"></param>
        public void DeleteItem(int _uid, int _upgradeLevel, List<int> _enchantments, List<int> _sockets)
        {
            bool _found = false;
            Dictionary<Item, int> _changedItems = new Dictionary<Item, int>();
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (!_found && Stacks[i].isSameItem(_uid, _upgradeLevel, _enchantments, _sockets))
                {
                    _changedItems.Add(Stacks[i].Item, -Stacks[i].Number);
                    Stacks[i].Delete();
                    _found = true;
                    break;
                }
            }
            if(_found)ItemChanged(_changedItems);
        }

        /// <summary>
        /// Returns the item matches the specific tag list, set _allMatch to false if you require the item matches any tag in the list, set _allMatch to true if require the item matches all tags in the list.
        /// </summary>
        /// <param name="_tags"></param>
        /// <param name="_allMatch"></param>
        /// <returns></returns>
        public Item GetItemByTag(List<string> _tags, bool _allMatch=true)
        {
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (Stacks[i].isTagsMatchList(_tags, _allMatch))
                {
                    return Stacks[i].Item;
                }
            }
            return null;
        }
        /// <summary>
        /// Returns the slot index of an item with a specific UID, upgrade level, socketing and enchantments.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_upgradeLevel"></param>
        /// <param name="_enchantments"></param>
        /// <param name="_sockets"></param>
        /// <returns></returns>
        public int GetItemIndex(int _uid, int _upgradeLevel, List<int> _enchantments, List<int> _sockets) {
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (Stacks[i].isSameItem(_uid, _upgradeLevel, _enchantments, _sockets))
                {
                    return i;
                }
            }
            return -1;
        }


        /// <summary>
        /// Returns an item with a specific UID, upgrade level, socketing and enchantments.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_upgradeLevel"></param>
        /// <param name="_enchantments"></param>
        /// <param name="_sockets"></param>
        /// <returns></returns>
        public Item FindItem(int _uid, int _upgradeLevel, List<int> _enchantments, List<int> _sockets)
        {
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (Stacks[i].isSameItem(_uid, _upgradeLevel, _enchantments, _sockets))
                {
                   return  Stacks[i].Item;
                }
            }
            return null;
        }



        /// <summary>
        /// Removes a specified number of items with a specific UID and index. Set _index to -1 if you don¡¯t know the index. Returns the total number of items removed.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_number"></param>
        /// <param name="_index"></param>
        /// <returns></returns>
        public int RemoveItem(int _uid, int _number, int _index=-1)
        {
            int _totalRemoved = 0;
            Dictionary<Item, int> _changedItems = new Dictionary<Item, int>();
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (_number > 0)
                {
                    if (Stacks[i].isSameItem(_uid) && (_index==-1 || i== _index))
                    {
                        int _tookNumber = Mathf.Min(_number, Stacks[i].Number);
                        _totalRemoved += _tookNumber;
                        Stacks[i].AddNumber(-_tookNumber);
                        _number -= _tookNumber;
                    }
                }
                else
                {
                    break;
                }
            }
            for (int i = HiddenStacks.Count-1; i >=0; i--)
            {
                if (_number > 0)
                {
                    if (HiddenStacks[i].isSameItem(_uid))
                    {
                        int _tookNumber = Mathf.Min(_number, HiddenStacks[i].Number);
                        _totalRemoved += _tookNumber;
                        HiddenStacks[i].AddNumber(-_tookNumber);
                        _number -= _tookNumber;
                        if (HiddenStacks[i].Number <= 0) HiddenStacks.RemoveAt(i);
                    }
                }
                else
                {
                    break;
                }
            }
            _changedItems.Add(ItemManager.TryGetItem(_uid).Copy(),-_totalRemoved);
            ItemChanged(_changedItems);
            // return the actual removed number of this item (the require number may larger than the number this holder has);
            return _totalRemoved;
        }

        /// <summary>
        /// Uses a specified number of items with a specific UID and index. Set _index to -1 if you don¡¯t know the index.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_number"></param>
        /// <param name="_index"></param>
        public void UseItem(int _uid, int _number, int _index = -1)
        {
            if (ItemManager.TryGetItem(_uid)!=null)
            {
                if (Type == HolderType.PlayerInventory)
                {
                    if (!ItemManager.TryGetItem(_uid).AbleToUse(GetInventoryHolderByType(gameObject, HolderType.PlayerEquipment)))
                    {
                        if (ItemManager.instance.GetAtttibute(ItemManager.TryGetItem(_uid).restrictionKey)!=null) DynamicMsg.PopMsg(ItemManager.instance.msgItemUseRestricted.Replace("{name}", ItemManager.instance.GetAtttibute(ItemManager.TryGetItem(_uid).restrictionKey).name).Replace("{value}", ItemManager.TryGetItem(_uid).restrictionValue.ToString()));
                        return;
                    }
                }
                else if (Type == HolderType.NpcInventory)
                {
                    if (!ItemManager.TryGetItem(_uid).AbleToUse(GetInventoryHolderByType(gameObject, HolderType.NpcEquipment)))
                    {
                        if (ItemManager.instance.GetAtttibute(ItemManager.TryGetItem(_uid).restrictionKey) != null) DynamicMsg.PopMsg(ItemManager.instance.msgItemUseRestricted.Replace("{name}", ItemManager.instance.GetAtttibute(ItemManager.TryGetItem(_uid).restrictionKey).name).Replace("{value}", ItemManager.TryGetItem(_uid).restrictionValue.ToString()));
                        return;
                    }
                }
                if (!ItemManager.TryGetItem(_uid).useable) return;
                if (ItemManager.TryGetItem(_uid).isCoolDown()) return;
                int _count = ItemManager.TryGetItem(_uid).consumable?RemoveItem(_uid, _number, _index):1;
                ItemManager.TryGetItem(_uid).SetCoolDownTimeStamp();
                ItemManager.SetSharedGlobalCoolDown(ItemManager.instance.SharedGlobalCoolDown);
                for (int i = 0; i < _count; i++)
                {
                    if (mItemUseCallbacks != null)
                    {
                        foreach (var _action in ItemManager.TryGetItem(_uid).actions) mItemUseCallbacks(_action, _uid, _index);
                    }
                }
                Dictionary<Item, int> _changedItems = new Dictionary<Item, int>();
                _changedItems.Add(ItemManager.TryGetItem(_uid).Copy(),-_count);
                ItemChanged(_changedItems);
            }
            else
            {
                Debug.LogError("Trying to use item with invalid item uid.");
            }
        }

        
    }
}
