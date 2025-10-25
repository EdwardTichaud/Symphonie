using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    #region System Class

    public enum MouseButtons
    {
        LeftClick,
        RightClick,
        MiddleClick
    }

    public enum AlterKeys
    {
        LeftCtrl,
        LeftShift,
        LeftAlt,
        None
    }

    public enum ClickFunctions
    {
        Use,
        Split,
        Drop,
        MarkFavorite
    }
    [System.Serializable]
    public class ClickSetting
    {
        public AlterKeys key;
        public MouseButtons mouseButton;
        public ClickFunctions function;
    }
    #endregion


    #region Enchantment Class
    [System.Serializable]
    public class Enchantment
    {
        /// <summary>
        /// The unique id of this Enchantment.
        /// </summary>
        public int uid;
        /// <summary>
        /// The display name of this Enchantment.
        /// </summary>
        public string name;
        /// <summary>
        /// The prefixes string of this Enchantment, the prefixes string will be displayed in the front of the item name.
        /// </summary>
        public string prefixes="";
        /// <summary>
        /// Only one prefixes string with highest priority will be displayed if an item has multiple enchantments.
        /// </summary>
        public int prefixesPriority = 0;
        /// <summary>
        /// The suffixes string of this Enchantment, the suffixes string will be displayed in the end of the item name.
        /// </summary>
        public string suffixes="";
        /// <summary>
        /// Only one suffixes string with highest priority will be displayed if an item has multiple enchantments.
        /// </summary>
        public int suffixesPriority = 0;
        /// <summary>
        /// The attributes list this enchantment will add to the item.
        /// </summary>
        public List<Attribute> attributes=new List<Attribute>();
        [HideInInspector]
        public bool fold = true;

        /// <summary>
        /// Get the description string of this enchantment, will looks like "[Deadly] Attack +10 Speed +5"
        /// </summary>
        /// <returns></returns>
        public string GetDescription()
        {
            string _result = "[<color=#" + ColorUtility.ToHtmlStringRGB(ItemManager.instance.EnchantingNameColor) + ">"+ name + "</color>]  ";
            foreach(var obj in attributes) {
                _result += "<color=#" + ColorUtility.ToHtmlStringRGB(ItemManager.instance.AttributeNameColor) + ">" + obj.name + "</color>"+ (obj.GetFloat() > 0 ? " + " : " ") + obj.value+"  ";
            }
            return _result;
        }
    }
    #endregion

    #region Currency Class
    [System.Serializable]
    public class Currency
    {
        /// <summary>
        /// The display name of this currency.
        /// </summary>
        public string name;
        /// <summary>
        /// The icon sprite of this currency.
        /// </summary>
        public Sprite icon;
        /// <summary>
        /// The value text color of this currency.
        /// </summary>
        public Color color;
        /// <summary>
        /// The exchange rate settings of this currency. 
        /// x: the target currency id | y: the exchange rate | z: 0= Disable auto exchange  1=Enable auto exchange
        /// </summary>
        public List<Vector3> ExchangeRate=new List<Vector3>();
        public bool fold = true;
    }

    [System.Serializable]
    public class CurrencySet
    {
        public List<int> Currency = new List<int>();
        public bool KeepPostive = true;

   
        public CurrencySet(){
            Currency = new List<int>();
        }

        public CurrencySet(List<int> _value)
        {
            Currency = new List<int>();
            Init(_value.ToArray());
        }

        public CurrencySet(int [] _value)
        {
            Currency = new List<int>();
            Init(_value);
        }

        /// <summary>
        /// Copy the data of this instance so you can assign it to another instance.
        /// </summary>
        /// <returns></returns>
        public CurrencySet Copy()
        {
            CurrencySet _copy = new CurrencySet();
            _copy.Currency = new List<int>();
            for (int i = 0; i < Currency.Count; i++)
            {
                _copy.Currency.Add(Currency[i]);
            }
            return _copy;
        }

        /// <summary>
        /// Add a new currency.
        /// </summary>
        /// <param name="_value"></param>
        public void Add(int _value)
        {
            Currency.Add(_value);
        }

        /// <summary>
        /// Get the count of currency list.
        /// </summary>
        public int Count
        {
            get
            {
                return Currency.Count;
            }
        }

        /// <summary>
        /// Get the array of currency values.
        /// </summary>
        /// <returns></returns>
        public int[] GetCurrencyArray()
        {
            return Currency.ToArray();
        }


        public void Init()
        {
            for (int i = 0; i < ItemManager.instance.currencies.Count; i++)
            {
                if (Currency.Count <= i)
                {
                    Currency.Add(0);
                }
            }
        }

        public void Init(int [] _currency)
        {
            Init();
            for (int i = 0; i < _currency.Length; i++)
            {
                if (Currency.Count < i + 1)
                    Currency.Add(_currency[i]);
                else
                    Currency[i] = _currency[i];
            }
        }


        public void Reset()
        {
            Currency = new List<int>();
        }


        /// <summary>
        /// Get the exchange rate from _source to _target. 
        /// </summary>
        /// <param name="_source"></param>
        /// <param name="_target"></param>
        /// <param name="_onlyAutoExchange"></param>
        /// <returns></returns>
        private int GetExchangeRate(int _source, int _target, bool _onlyAutoExchange = false)
        {
            for (int i = 0; i < ItemManager.instance.currencies[_target].ExchangeRate.Count; i++)
            {
                if (Mathf.FloorToInt(ItemManager.instance.currencies[_target].ExchangeRate[i].x) == _source
                    && (!_onlyAutoExchange || ItemManager.instance.currencies[_target].ExchangeRate[i].z > 0F))
                {
                    return Mathf.FloorToInt(ItemManager.instance.currencies[_target].ExchangeRate[i].y);
                }
            }
            return 0;
        }

        /// <summary>
        /// Retrieves the currency value by its index number, Set _includeExchangedValue to true if you want to get the total amount of this currency including the exchanged value from other currencies.
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_includeExchangedValue"></param>
        /// <returns></returns>
        public int GetCurrency(int _type, bool _includeExchangedValue = false)
        {
            if (_type < Currency.Count)
            {
                int _value = Currency[_type];
                if (_includeExchangedValue)
                {
                    for (int i = 0; i < Currency.Count; i++)
                    {
                        if (_type != i && GetExchangeRate(_type, i) > 0)
                        {
                            _value += GetExchangeRate(_type, i) * GetCurrency(i, true);
                        }
                    }
                }
                return _value;
            }
            else
                return 0;
        }

   
        /// <summary>
        /// Adds to the currency value by its index number. The _add value can be negative.
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_add"></param>
        public void AddCurrency(int _type, int _add)
        {
            if (_type < Currency.Count)
            {
                Currency[_type] += _add;
            }
            else
            {
                for (int i = Currency.Count; i < _type; i++)
                {
                    Currency.Add(0);
                }
                Currency.Add(_add);
            }
            AutoExchange(_type);
        }


        /// <summary>
        /// Overrides the currency value by its index number.
        /// </summary>
        /// <param name="_type"></param>
        /// <param name="_value"></param>
        public void SetCurrency(int _type, int _value)
        {
            if (_type < Currency.Count)
            {
                Currency[_type] = _value;
            }
            else
            {
                for (int i = Currency.Count; i < _type; i++)
                {
                    Currency.Add(0);
                }
                Currency.Add(_value);
            }
            AutoExchange(_type);
        }

        /// <summary>
        /// Exchange all currencies into higher value currency if possible.
        /// </summary>
        public void AutoExchange()
        {
            for (int u = 0; u < Currency.Count; u++)
            {
                for (int i = 0; i < Currency.Count; i++)
                {
                    AutoExchange(i);
                }
            }
        }

        /// <summary>
        /// Collapse specified currency by its index number into lowest value currency.
        /// </summary>
        /// <param name="_type"></param>
        public void CollapseToLowestCurrency(int _type)
        {
            for (int u = 0; u < ItemManager.instance.currencies[_type].ExchangeRate.Count; u++)
            {
                int _target = Mathf.FloorToInt(ItemManager.instance.currencies[_type].ExchangeRate[u].x);
                if (ItemManager.instance.currencies[_type].ExchangeRate[u].z > 0F
                    && ItemManager.instance.currencies[_type].ExchangeRate[u].y > 0F
                    && _target != _type
                    && Currency[_type] > 0)
                {
                    Currency[_target] += Mathf.FloorToInt(Currency[_type] * ItemManager.instance.currencies[_type].ExchangeRate[u].y);
                    Currency[_type] = 0;
                    CollapseToLowestCurrency(_type);
                }
            }
        }

        /// <summary>
        /// Collapse all currencies into lowest value currency.
        /// </summary>
        public void CollapseAllToLowestCurrenc()
        {
            for (int i = 0; i < Currency.Count; i++) CollapseToLowestCurrency(i);
        }

        /// <summary>
        /// Exchange specified currencies into higher value currency if possible.
        /// </summary>
        /// <param name="_type"></param>
        private void AutoExchange(int _type)
        {
            if (Currency[_type] == 0) return;
            for (int i = 0; i < Currency.Count; i++)
            {
                if (i != _type)
                {
                    int _rate = GetExchangeRate(_type, i, true);
                    if (Currency[_type] > 0)
                    {
                        if (Currency[_type] >= _rate && _rate > 0)
                        {
                            int _exchangeAmount = Mathf.FloorToInt(Currency[_type] * 1F / _rate);
                            Currency[_type] = Currency[_type] % _rate;
                            AddCurrency(i, _exchangeAmount);
                        }
                    }
                    else
                    {
                        if (_rate > 0)
                        {
                            float _amount = -Currency[_type] * 1F / _rate;
                            int _exchangeAmount = KeepPostive? Mathf.CeilToInt(_amount): Mathf.FloorToInt(_amount);
                            Currency[_type] += _exchangeAmount * _rate;
                            AddCurrency(i, -_exchangeAmount);
                        }
                    }
                }
            }
        }
    }
    #endregion

    #region Item Class
    [System.Serializable]
    public class Item
    {
        /// <summary>
        /// The unique id  of this Item, in most scenarios you will need this uid to access an item.
        /// </summary>
        public int uid;
        /// <summary>
        /// The display name of this Item.
        /// </summary>
        public string name;
        /// <summary>
        /// The description text of this Item.
        /// </summary>
        public string description;
        /// <summary>
        /// The category index of this item. You can manage the categories on the "InventoryEngine" prefab.
        /// </summary>
        public int type;
        /// <summary>
        /// Please use GetIcon(), access 'icon' property directly will get null result if iconLoadMethod is not set to DirectReference.
        /// </summary>
        public Texture2D icon;
        /// <summary>
        /// The loading method of the icon.
        /// </summary>
        public LoadMethod iconLoadMethod;
        /// <summary>
        /// The path of the icon texture. 
        /// The AssetLoader will use this path to load icon textures from resources folder when needed
        /// You could inherit AssetLoader class to override the loading method with your own, for example you could use Unity Addressables.
        /// </summary>
        public string iconPath;
        /// <summary>
        /// The quality index of this item. You can mange the quality levels on the "InventoryEngine" prefab.
        /// </summary>
        public int quality = 0;
        /// <summary>
        /// Whether this item is tradable with NPC merchant.
        /// </summary>
        public bool tradeable = true;
        /// <summary>
        /// Whether this item is deletable when drag out from inventory.
        /// </summary>
        public bool deletable = true;
        /// <summary>
        /// Whether this item is useable when right click or triggered from hot bar.
        /// </summary>
        public bool useable = false;
        /// <summary>
        /// Whether this item will be consumed when use.
        /// </summary>
        public bool consumable = false;
        /// <summary>
        /// Whether this item is visible on the interface. If an item is not visible, it wll be put into HiddenStacks of InventoryHolder component.
        /// </summary>
        public bool visible = true;
        /// <summary>
        /// The base price of this item when trading.
        /// </summary>
        public int price = 0;
        /// <summary>
        /// The currency index of the trading price.
        /// </summary>
        public int currency = 0;
        /// <summary>
        /// The maximum  number this item can be stack.
        /// </summary>
        public int maxiumStack = 99;
        /// <summary>
        /// The enchancement level of this item.
        /// </summary>
        public int upgradeLevel = 0;
        /// <summary>
        /// The weight value of this item.
        /// </summary>
        public float weight = 0.1F;
        /// <summary>
        /// The drop rate of this item (0~100)%
        /// </summary>
        public int dropRates = 0;
        /// <summary>
        /// Whether this item is in player's favorite list.
        /// </summary>
        public bool favorite = false;
        /// <summary>
        /// The attributes list of this item,
        /// </summary>
        public List<Attribute> attributes = new List<Attribute>();
        /// <summary>
        /// Maximum number of random attributes this item can have.
        /// </summary>
        public int maximumRandomAttributes = 5;
        /// <summary>
        /// The enchantments list of this item.
        /// </summary>
        public List<int> enchantments = new List<int>();
        /// <summary>
        /// The materials list required to craft this item.
        /// </summary>
        public List<Vector2> craftMaterials = new List<Vector2>();
        /// <summary>
        /// The action string list of this item.
        /// </summary>
        public List<string> actions = new List<string>();
        /// <summary>
        /// The tag string list of this item.
        /// </summary>
        public List<string> tags = new List<string>();

        /// <summary>
        /// Custom data. You could use them to link audio clips, prefabs, images, etc.
        /// </summary>
        public List<CustomField> customData = new List<CustomField>();

        public float lastUseTimeStamp {
            get
            {
                lock (_lastUseTimeStampLock)
                {
                    return _lastUseTimeStamp;
                }
            }
            set
            {
                lock (_lastUseTimeStampLock)
                {
                    _lastUseTimeStamp = value;
                }
            }
        }
        private float _lastUseTimeStamp;
        private readonly object _lastUseTimeStampLock = new object();

        /// <summary>
        /// The attribute key for use/equip restriction.
        /// </summary>
        public string restrictionKey;
        /// <summary>
        /// The attribute value for use/equip restriction.
        /// </summary>
        public int restrictionValue;

        /// <summary>
        /// The socketing slots number, if Socketing module is enabled, player will be able to socketing other items with specified tag and category into this item to boost its attributes.
        /// </summary>
        public int socketingSlots = 0;
        /// <summary>
        /// The list of socketed items, if Socketing module is enabled, player will be able to socketing other items with specified tag and category into this item to boost its attributes.
        /// When the value of the list is -2, means this socket is locked, if it is -1 means this socket is empty, any number larger or equal to 0 represents the uid of this item.
        /// </summary>
        public List<int> socketedItems = new List<int>();

        /// <summary>
        /// This item will only recieve skocketing items with any tag in this list.
        /// </summary>
        public List<string> socketingTag = new List<string>();

        /// <summary>
        /// Retrieve the item display name with prefixes and suffixes.
        /// </summary>
        public string nameWithAffixing
        {
            get
            {
                string _prefixes = "";
                string _suffixes = "";
                int _prefixesLevel = -1;
                int _suffixesLevel = -1;
                foreach (var obj in enchantments) {
                    Enchantment _enchantment = ItemManager.TryGetEnchantmentById(obj);
                    if (_enchantment!=null) {
                        if (_enchantment.prefixesPriority > _prefixesLevel && _enchantment.prefixes != "") {
                            _prefixes = "<color=#" + ColorUtility.ToHtmlStringRGB(ItemManager.instance.EnchantingPrefixesColor) + ">" + _enchantment.prefixes + "</color> ";
                            _prefixesLevel = _enchantment.prefixesPriority;
                        }
                        if (_enchantment.suffixesPriority > _suffixesLevel && _enchantment.suffixes != "")
                        {
                            _suffixes = " <color=#" + ColorUtility.ToHtmlStringRGB(ItemManager.instance.EnchantingSuffxesColor) + ">" + _enchantment.suffixes + "</color>";
                            _suffixesLevel = _enchantment.suffixesPriority;
                        }
                    }
                }
                return _prefixes + nameWithColorCode + _suffixes;
            }
        }

        public string nameWithColorCode
        {
            get
            {
                return ItemManager.instance.UseQualityColorForItemName ? ("<color=#" + ColorUtility.ToHtmlStringRGB(GetQualityColor()) + ">" + name + "</color>") : name;
            }
        }

        public string compiledDescription
        {
            get
            {
                string _result = description.Replace("<br>","\n");
                Dictionary<string, Attribute> _attDic = new Dictionary<string, Attribute>();
                foreach (var att in attributes) {
                    if (!_attDic.ContainsKey(att.key))
                    {
                        _attDic.Add(att.key, att.Copy());
                    }
                    else
                    {
                        if (!att.stringValue)
                        {
                            _attDic[att.key].UpdateValue(_attDic[att.key].GetFloat()+ att.GetFloat());
                        }
                    }
                }
                int _index = 0;
                while(_index<_result.Length) {
                    int _startIndex = _result.IndexOf("{", _index);
                    int _endIndex = _result.IndexOf("}", _startIndex>-1?(_startIndex +1):(_index+1));
                    if (_startIndex > -1 && _endIndex>-1)
                    {
                        string _attText = _result.Substring(_startIndex + 1, _endIndex - _startIndex - 1);
                        if (_attDic.ContainsKey(_attText))
                        {
                            _result=_result.Replace(_result.Substring(_startIndex, _endIndex + 1 - _startIndex), _attDic[_attText].GetFloat(upgradeLevel).ToString());
                            _index = _startIndex + 1;
                        }
                        else
                        {
                            _index = _endIndex + 1;
                        }
                    }
                    else
                    {
                        _index = _result.Length;
                    }
                }
                _attDic.Clear();
                return _result;
            }
        }

#if MASTER_CHARACTER_CREATOR
        /// <summary>
        /// The MCC equipment binding
        /// </summary>
        [HideInInspector]
        public MasterCharacterCreator.EquipmentAppearance equipAppearance=null;
#endif
        [HideInInspector]
        public bool fold = true;
        /// <summary>
        /// If you want to create an Item with specified UID, try use:  new Item(int _uid);
        /// </summary>
        public Item()
        {
            fold = true;
        }

        /// <summary>
        /// Create an Item by the UID of the database on the "InventoryEngine" prefab. 
        /// When _initializeEnchantments is set to true, depending on your Enchantments settings on the "InventoryEngine" prefab the enchantments of this item could be randomized.
        /// When _initializeSocketingSlots is set to true, depending on your Socketing settings on the "InventoryEngine" prefab the socketing slots of this item could be randomized.
        /// </summary>
        /// <returns></returns>
        public Item (int _uid,bool _initializeEnchantments=true, bool _initializeSocketingSlots= true)
        {
            Item _ref = ItemManager.TryGetItem(_uid);
            if (_ref!=null)
            {
                uid = _ref.uid;
                name = _ref.name;
                description = _ref.description;
                type = _ref.type;
                iconLoadMethod = _ref.iconLoadMethod;
                iconPath = _ref.iconPath;
                icon = _ref.icon;
                quality = _ref.quality;
                tradeable = _ref.tradeable;
                deletable = _ref.deletable;
                useable = _ref.useable;
                consumable = _ref.consumable;
                price = _ref.price;
                currency = _ref.currency;
                maxiumStack = _ref.maxiumStack;
                upgradeLevel = _ref.upgradeLevel;
                weight = _ref.weight;
                dropRates = _ref.dropRates;
                favorite = _ref.favorite;
                attributes.Clear();
                maximumRandomAttributes = _ref.maximumRandomAttributes;
                restrictionKey = _ref.restrictionKey;
                restrictionValue = _ref.restrictionValue;
                lastUseTimeStamp = _ref.lastUseTimeStamp;
               
                int _attCount = 0;
                foreach (var att in _ref.attributes)
                {
                    Attribute _newAtt = att.Copy();
                    _newAtt.Init();
                    if(att.isFixed || _attCount< maximumRandomAttributes)attributes.Add(_newAtt);
                    if (!att.isFixed && !_newAtt.locked) _attCount++;
                }
                craftMaterials.Clear();
                craftMaterials.AddRange(_ref.craftMaterials);
                enchantments.Clear();
                enchantments.AddRange(_ref.enchantments);
                actions.Clear();
                actions.AddRange(_ref.actions);
                tags.Clear();
                tags.AddRange(_ref.tags);
                socketingTag.Clear();
                socketingTag.AddRange(_ref.socketingTag);
                socketingSlots = _ref.socketingSlots;
                socketedItems = new List<int>();
                socketedItems.AddRange(_ref.socketedItems);
                customData.Clear();
                customData.AddRange(_ref.customData);
                if (_initializeEnchantments) RandomEnchantment();
                if (_initializeSocketingSlots) ResetocketingSlots();
#if MASTER_CHARACTER_CREATOR
            equipAppearance= new MasterCharacterCreator.EquipmentAppearance();
            equipAppearance.Type = _ref.equipAppearance.Type;
            equipAppearance.UseCustomColor = _ref.equipAppearance.UseCustomColor;
            equipAppearance.CustomColor1 = _ref.equipAppearance.CustomColor1;
            equipAppearance.CustomColor2 = _ref.equipAppearance.CustomColor2;
            equipAppearance.CustomColor3 = _ref.equipAppearance.CustomColor3;
            equipAppearance.MaleMeshId = _ref.equipAppearance.MaleMeshId;
            equipAppearance.FemaleMeshId = _ref.equipAppearance.FemaleMeshId;
            equipAppearance.uiFold = _ref.equipAppearance.uiFold;
#endif
                fold = true;
            }
            else
            {
                Debug.LogError("Using invalid Item UID when creating a new item.");
            }
        }

        /// <summary>
        /// Get an instance of this item data. 
        /// When _initializeEnchantments is set to true, depending on your Enchantments settings on the "InventoryEngine" prefab the enchantments of this item could be randomized.
        /// When _initializeSocketingSlots is set to true, depending on your Socketing settings on the "InventoryEngine" prefab the socketing slots of this item could be randomized.
        /// </summary>
        /// <returns></returns>
        public Item Copy()
        {
            Item _newItem = new Item();
            _newItem.uid = uid;
            _newItem.name = name;
            _newItem.description = description;
            _newItem.type = type;
            _newItem.icon = icon;
            _newItem.iconLoadMethod = iconLoadMethod;
            _newItem.iconPath = iconPath;
            _newItem.quality = quality;
            _newItem.tradeable = tradeable;
            _newItem.deletable = deletable;
            _newItem.useable = useable;
            _newItem.consumable = consumable;
            _newItem.price = price;
            _newItem.currency = currency;
            _newItem.maxiumStack = maxiumStack;
            _newItem.upgradeLevel = upgradeLevel;
            _newItem.weight = weight;
            _newItem.dropRates = dropRates;
            _newItem.favorite = favorite;
            _newItem.restrictionKey = restrictionKey;
            _newItem.restrictionValue = restrictionValue;
            _newItem.maximumRandomAttributes = maximumRandomAttributes;
            _newItem.lastUseTimeStamp = lastUseTimeStamp;
            _newItem.attributes.Clear();
            foreach (var att in attributes)
            {
                _newItem.attributes.Add(att.Copy());
            }
            _newItem.craftMaterials.Clear();
            _newItem.craftMaterials.AddRange(craftMaterials);
            _newItem.enchantments.Clear();
            _newItem.enchantments.AddRange(enchantments);
            _newItem.actions.Clear();
            _newItem.actions.AddRange(actions);
            _newItem.tags.Clear();
            _newItem.tags.AddRange(tags);
            _newItem.socketingSlots = socketingSlots;
            _newItem.socketedItems = new List<int>();
            _newItem.socketedItems.AddRange(socketedItems);
            _newItem.socketingTag.Clear();
            _newItem.socketingTag.AddRange(socketingTag);
            _newItem.customData.Clear();
            for (int i=0;i< customData.Count;i++) {
                _newItem.customData.Add(customData[i].Copy());
            }
#if MASTER_CHARACTER_CREATOR
            _newItem.equipAppearance= new MasterCharacterCreator.EquipmentAppearance();
            _newItem.equipAppearance.Type = equipAppearance.Type;
            _newItem.equipAppearance.UseCustomColor = equipAppearance.UseCustomColor;
            _newItem.equipAppearance.CustomColor1 = equipAppearance.CustomColor1;
            _newItem.equipAppearance.CustomColor2 = equipAppearance.CustomColor2;
            _newItem.equipAppearance.CustomColor3 = equipAppearance.CustomColor3;
            _newItem.equipAppearance.MaleMeshId = equipAppearance.MaleMeshId;
            _newItem.equipAppearance.FemaleMeshId = equipAppearance.FemaleMeshId;
            _newItem.equipAppearance.uiFold = equipAppearance.uiFold;
#endif
            _newItem.fold = true;
            return _newItem;
        }

        /// <summary>
        /// Return whether this item can be use or equip by compare the attribute value of the InventoryHolder with the restriction attribute setting.
        /// </summary>
        /// <param name="_holder"></param>
        /// <returns></returns>
        public bool AbleToUse(InventoryHolder _holder)
        {
            if (restrictionKey != "" && restrictionValue > 0)
                return _holder.GetAttributeValue(restrictionKey,true) >= restrictionValue;
            else
                return true;
        }


        public Texture2D GetIcon()
        {
            if (iconLoadMethod == LoadMethod.DirectReference)
            {
                return icon;
            }
            else
            {
                string _key = iconPath.Trim().Replace("\r", "").Replace("\n", "").Normalize();
                if (!ItemManager.instance.myAssetLoader.LoadedIcon.ContainsKey(_key))
                {
                    if (iconLoadMethod == LoadMethod.Resources)
                    {
                        ItemManager.instance.myAssetLoader.LoadedIcon.Add(_key, Resources.Load<Texture2D>(iconPath));
                    }
                    else
                    {
                        ItemManager.instance.myAssetLoader.LoadedIcon.Add(_key, ItemManager.instance.myAssetLoader.Load<Texture2D>(iconPath));
                    }
                }
                return ItemManager.instance.myAssetLoader.LoadedIcon[_key];
            }
        }

        /// <summary>
        /// Get custom data by its key string.
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public T GetCustomData<T>(string _key) where T : UnityEngine.Object
        {
            foreach (var obj in customData) {
                if (obj.key == _key)
                {
                    if (obj.loadMethod == LoadMethod.DirectReference)
                    {
                        return (T)obj.value;
                    }
                    else
                    {
                        string _pathKey = obj.loadPath.Trim().Replace("\r", "").Replace("\n", "").Normalize();
                        if (!ItemManager.instance.myAssetLoader.LoadedCustomAsset.ContainsKey(_pathKey))
                        {
                            if (obj.loadMethod == LoadMethod.Resources)
                            {
                                ItemManager.instance.myAssetLoader.LoadedCustomAsset.Add(_pathKey, Resources.Load<T>(obj.loadPath));
                            }
                            else if (obj.loadMethod == LoadMethod.Custom)
                            {
                                ItemManager.instance.myAssetLoader.LoadedCustomAsset.Add(_pathKey, ItemManager.instance.myAssetLoader.Load<T>(obj.loadPath));
                            }
                        }
                        return (T)ItemManager.instance.myAssetLoader.LoadedCustomAsset[_pathKey];
                    }
                }
            }
            return null;
        }


        /// <summary>
        /// Whenever the icon or CustomData is accessed, it will be loaded into memory by its loading method, call this method to unload it when necessary.
        /// </summary>
        public void ReleaseLoadedAsset()
        {
            if (iconLoadMethod != LoadMethod.DirectReference)
            {
                string _key = iconPath.Trim().Replace("\r", "").Replace("\n", "").Normalize();
                if (ItemManager.instance.myAssetLoader.LoadedIcon.ContainsKey(_key)) ItemManager.instance.myAssetLoader.LoadedIcon.Remove(_key);
                ItemManager.instance.myAssetLoader.ReleaseAsset(iconPath);
            }
            
            foreach (var obj in customData)
            {
                if (obj.loadMethod != LoadMethod.DirectReference)
                {
                    string _pathKey = obj.loadPath.Trim().Replace("\r", "").Replace("\n", "").Normalize();
                    if (ItemManager.instance.myAssetLoader.LoadedCustomAsset.ContainsKey(_pathKey)) ItemManager.instance.myAssetLoader.LoadedCustomAsset.Remove(_pathKey);
                    ItemManager.instance.myAssetLoader.ReleaseAsset(obj.loadPath);
                }
            }
        }

        /// <summary>
        /// Get the total cool down time of this item.
        /// </summary>
        /// <returns></returns>
        public float GetCoolDownTime()
        {
            return GetAttributeFloat(ItemManager.instance.CoolDownAttributeKey);
        }

        /// <summary>
        /// Get the remaining cool down time of this item.
        /// </summary>
        /// <returns></returns>
        public float GetRemainCoolDownTime()
        {
            return Mathf.Max(0F, lastUseTimeStamp + GetCoolDownTime() - Time.time);
        }

        /// <summary>
        /// Return whether this item is being cool down.
        /// </summary>
        /// <returns></returns>
        public bool isCoolDown()
        {
            return GetRemainCoolDownTime() > 0F;
        }

        /// <summary>
        /// When the item is used, call this function to set the time stamp.
        /// </summary>
        public void SetCoolDownTimeStamp()
        {
            lastUseTimeStamp = Time.time;
        }

        /// <summary>
        /// Override the remaining cool down time.
        /// </summary>
        /// <param name="_coolDownTime"></param>
        public void SetRemainCoolDownTime(float _coolDownTime)
        {
            lastUseTimeStamp = Time.time - GetCoolDownTime() + _coolDownTime;
        }

        /// <summary>
        /// Add time in seconds to the remaining cool down time, the _addValue can be either postive or negative.
        /// </summary>
        /// <param name="_addValue"></param>
        public void AddRemainCoolDownTime(float _addValue)
        {
            lastUseTimeStamp = Time.time - (GetCoolDownTime()- GetRemainCoolDownTime())+ _addValue;
        }

       
  

        /// <summary>
        /// Get the float value of an Attribute of this item with the attribute key.
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public float GetAttributeFloat(string _key)
        {
            float _result = 0F;
            foreach (Attribute att in attributes)
            {
                if(att.key == _key) _result += att.GetFloat(upgradeLevel);
            }
            foreach (int _ent in enchantments) {
                if (ItemManager.TryGetEnchantmentById(_ent)!=null)
                {
                    foreach (Attribute att in ItemManager.TryGetEnchantmentById(_ent).attributes) {
                        if (att.key == _key) _result += att.GetFloat();
                    }
                }
            }
            foreach (int _socket in socketedItems)
            {
                if (_socket>=0 && ItemManager.TryGetItem(_socket)!=null)
                {
                    foreach (Attribute att in ItemManager.TryGetItem(_socket).attributes)
                    {
                        if (att.key == _key) _result += att.GetFloat();
                    }
                }
            }
            return _result;
        }

        /// <summary>
        /// Get the int value of an Attribute of this item with the attribute key.
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public int GetAttributeInt(string _key)
        {
            int _result = 0;
            foreach (Attribute att in attributes)
            {
                if (att.key == _key) _result += att.GetInt(upgradeLevel);
            }
            foreach (int _ent in enchantments)
            {
                if (ItemManager.TryGetEnchantmentById(_ent)!=null)
                {
                    foreach (Attribute att in ItemManager.TryGetEnchantmentById(_ent).attributes)
                    {
                        if (att.key == _key) _result += att.GetInt();
                    }
                }
            }
            foreach (int _socket in socketedItems)
            {
                if (_socket >= 0 && ItemManager.TryGetItem(_socket)!=null)
                {
                    foreach (Attribute att in ItemManager.TryGetItem(_socket).attributes)
                    {
                        if (att.key == _key) _result += att.GetInt();
                    }
                }
            }
            return _result;
        }


        /// <summary>
        /// Get the string value of an Attribute of this item with the attribute key.
        /// </summary>
        /// <param name="_key"></param>
        /// <returns></returns>
        public string GetAttributeString(string _key)
        {
            string _result = "None";
            foreach (Attribute att in attributes)
            {
                if (att.key == _key) _result = att.GetString();
            }
           return _result;
        }

        /// <summary>
        /// Change the value of an Attribute of this item with the attribute key.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void UpdateAttribute(string _key, float _value)
        {
            foreach (Attribute att in attributes)
            {
                if (att.key == _key) att.UpdateValue(_value);
            }     
        }

        /// <summary>
        /// Change the value of an Attribute of this item with the attribute key.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void UpdateAttribute(string _key, int _value)
        {
            foreach (Attribute att in attributes)
            {
                if (att.key == _key) att.UpdateValue(_value);
            }
        }

        /// <summary>
        /// Change the value of an Attribute of this item with the attribute key.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void UpdateAttribute(string _key, string _value)
        {
            foreach (Attribute att in attributes)
            {
                if (att.key == _key) att.UpdateValue(_value);
            }
        }


        /// <summary>
        /// Add an Attribute to this item with the attribute key and float value.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void AddAttribute(string _key, float _value)
        {
            foreach (Attribute att in ItemManager.instance.itemAttributes)
            {
                if (att.key == _key)
                {
                    Attribute _newAtt = att.Copy();
                    _newAtt.value = _value.ToString();
                    attributes.Add(_newAtt);
                }
            }
        }

        /// <summary>
        /// Add an Attribute to this item with the attribute key and int value.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void AddAttribute(string _key, int _value)
        {
            foreach (Attribute att in ItemManager.instance.itemAttributes)
            {
                if (att.key == _key)
                {
                    Attribute _newAtt = att.Copy();
                    _newAtt.value = _value.ToString();
                    attributes.Add(_newAtt);
                }
            }
        }

        /// <summary>
        /// Add an Attribute to this item with the attribute key and string value.
        /// </summary>
        /// <param name="_key"></param>
        /// <param name="_value"></param>
        public void AddAttribute(string _key, string _value)
        {
            foreach (Attribute att in ItemManager.instance.itemAttributes)
            {
                if (att.key == _key)
                {
                    Attribute _newAtt = att.Copy();
                    _newAtt.value = _value;
                    attributes.Add(_newAtt);
                }
            }
        }

        /// <summary>
        /// Remove an Attribute of this item with the attribute key.
        /// </summary>
        /// <param name="_key"></param>
        public void RemoveAttribute(string _key)
        {
            for (int i=0;i<attributes.Count;i++) {
                if (attributes[i].key == _key) attributes.RemoveAt(i);
            }
        }

        /// <summary>
        /// Remove all Attributes of this this item.
        /// </summary>
        public void ClearAttribute()
        {
            attributes.Clear();
        }

        /// <summary>
        /// Retrieve the tag string of this item which contains the specified "_text".
        /// </summary>
        /// <param name="_text"></param>
        /// <param name="_caseSensitive"></param>
        /// <returns></returns>
        public string GetTagContainText(string _text, bool _caseSensitive = true)
        {
            if (!_caseSensitive) _text = _text.ToLower();
            foreach (string obj in tags)
            {
                if (_caseSensitive)
                {
                    if (obj.Contains(_text)) return obj;
                }
                else
                {
                    if (obj.ToLower().Contains(_text)) return obj;
                }
            }
            return "";
        }

        /// <summary>
        /// Whether any tag of this item contains the specified "_text".
        /// </summary>
        /// <param name="_text"></param>
        /// <param name="_caseSensitive"></param>
        /// <returns></returns>
        public bool isTagContainText(string _text, bool _caseSensitive=true)
        {
            if (!_caseSensitive) _text = _text.ToLower();
            foreach (string obj in tags)
            {
                if (_caseSensitive)
                {
                    if (obj.Contains(_text)) return true;
                }
                else
                {
                    if (obj.ToLower().Contains(_text)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether any tag of this item matchs the specified "_tag", "#1","#2","#3" in the tags will be ignored. For example, an item with tag:"Weapon#2" will return true if you call isTagMatchText("Weapon");
        /// </summary>
        /// <param name="_tag"></param>
        /// <returns></returns>
        public bool isTagMatchText(string _tag)
        {
            return tags.Contains(_tag.Replace("#1", "").Replace("#2", "").Replace("#3", ""));
        }

        public bool isTagsMatchList(List<string> _tags, bool _allMatch)
        {
            foreach (string obj in _tags) {
                if (!tags.Contains(obj) && _allMatch) return false;
                if (!_allMatch && tags.Contains(obj)) return true;
            }
            return _allMatch?true:false;
        }

        /// <summary>
        /// Add an enchantment to this item by its UID.
        /// </summary>
        /// <param name="_uid"></param>
        public void AddEnchantment(int _uid)
        {
            if (ItemManager.TryGetEnchantmentById(_uid)!=null)
            {
                enchantments.Add(_uid);
            }
            else
            {
                Debug.LogError("Trying to add enchantment with invalid uid.");
            }
        }

        /// <summary>
        /// Replace the enchantments with a new set of enchantments by a list of their UID.
        /// </summary>
        /// <param name="_enchantments"></param>
        public void ReplaceEnchantment(List<int> _enchantments)
        {
            enchantments.Clear();
            for (int i=0;i< _enchantments.Count;i++) {
                AddEnchantment(_enchantments[i]);
            }
        }

        /// <summary>
        /// Remove an enchantment by its UID.
        /// </summary>
        /// <param name="_uid"></param>
        public void RemoveEnchantment(int _uid)
        {
            if (ItemManager.TryGetEnchantmentById(_uid)!=null)
            {
                enchantments.Remove(_uid);
            }
            else
            {
                Debug.LogError("Trying to remove enchantment with invalid uid.");
            }
        }

        /// <summary>
        /// Set random enchantments for this item, and return the list of uid. Please note there're chances for no enchantment at all.
        /// </summary>
        /// <returns></returns>
        public List<int> RandomEnchantment()
        {
            if (ItemManager.enchantmentDic.Count>0 && ItemManager.instance.EnableEnchanting && ItemManager.instance.RandomEnchantmentsForNewItem && type== ItemManager.instance.EnchantingCategoryID && Random.Range(0, 100) < ItemManager.instance.EnchantingSuccessRate)
            {
                List<int> _newEnchantments = new List<int>();
                int _count = Random.Range(Mathf.FloorToInt(ItemManager.instance.EnchantmentNumberRange.x), Mathf.FloorToInt(ItemManager.instance.EnchantmentNumberRange.y));
                for (int i = 0; i < _count; i++)
                {
                    int _uid = Random.Range(0, ItemManager.enchantmentDic.Count);
                    if (!_newEnchantments.Contains(_uid) && ItemManager.TryGetEnchantmentById(_uid)!=null) _newEnchantments.Add(_uid);
                }
                ReplaceEnchantment(_newEnchantments);
                return _newEnchantments;
            }
            return enchantments;
        }

        /// <summary>
        /// Remove all enchantments of this item.
        /// </summary>
        public void ResetEnchantment()
        {
            enchantments.Clear();
        }

       
        /// <summary>
        /// Reset the socketing slots of this item.
        /// </summary>
        public void ResetocketingSlots()
        {
            if (ItemManager.instance.EnableSocketing && type== ItemManager.instance.SocketedCategoryFilter)
            {
                if (ItemManager.instance.RandomSocketingSlotsNummber)
                {
                    socketingSlots = Random.Range(ItemManager.instance.MinimalSocketingSlotsNumber, ItemManager.instance.MaxmiumSocketingSlotsNumber);
                    socketedItems = new List<int>();
                    bool _locked = false;
                    for (int i=0;i< socketingSlots;i++) {
                        if ((ItemManager.instance.LockSocketingSlotsByDefault && Random.Range(0, 100) < ItemManager.instance.RandomChanceToLockSocketingSlots) || _locked)
                        {
                            _locked = true;
                            socketedItems.Add(-2);
                        }
                        else
                        {
                            socketedItems.Add(-1);
                        }
                    }
                }
            }
            else
            {
                socketingSlots = 0;
                socketedItems = new List<int>();
            }
        }

        /// <summary>
        /// Override the socketed item list.
        /// </summary>
        /// <param name="_sockets"></param>
        public void ReplaceSocketing(List<int> _sockets)
        {
            socketedItems.Clear();
            socketingSlots = _sockets.Count;
            for (int i = 0; i < _sockets.Count; i++)
            {
                socketedItems.Add(_sockets[i]);
            }
        }

        /// <summary>
        /// Reset the enhancement level of this item to 0.
        /// </summary>
        /// 
        public void ResetUpgrade()
        {
            upgradeLevel = 0;
        }

        /// <summary>
        /// Pop the enhancement level of this item by 1.
        /// </summary>
        public int Upgrade()
        {
            upgradeLevel++;
            return upgradeLevel;
        }

        /// <summary>
        /// Reset the attributes of this item to match the database.
        /// </summary>
        public void ResetAttributes()
        {
            attributes.Clear();
            Dictionary<string, bool> _attLock = new Dictionary<string, bool>();
            foreach (var obj in attributes)
            {
                if (!_attLock.ContainsKey(obj.key)) _attLock.Add(obj.key, obj.locked);
            }
            if (ItemManager.TryGetItem(uid)!=null) {
                foreach (var _att in ItemManager.TryGetItem(uid).attributes)
                {
                    attributes.Add(_att.Copy());
                }
            }
            foreach (var obj in attributes)
            {
                if (_attLock.ContainsKey(obj.key)) obj.locked = _attLock[obj.key];
            }
            _attLock.Clear();
        }

        /// <summary>
        /// Reset all settings of this item to match the database
        /// </summary>
        public void ResetAll()
        {
            if(ItemManager.TryGetItem(uid)!=null) price = ItemManager.TryGetItem(uid).price;
            favorite = false;
            ResetUpgrade();
            ResetAttributes();
            ResetEnchantment();
            ResetocketingSlots();
        }

        /// <summary>
        /// Get the color of the quality level of this item. 
        /// </summary>
        /// <returns></returns>
        public Color GetQualityColor()
        {
            if (ItemManager.TryGetItemQualityById(quality)!=null)
            {
                return ItemManager.TryGetItemQualityById(quality).color;
            }
            else return Color.gray;
        }

        /// <summary>
        /// Get the display name of the quality level of this item. 
        /// </summary>
        /// <returns></returns>
        public string GetQualityName()
        {
            if (ItemManager.TryGetItemQualityById(quality)!=null)
            {
                return ItemManager.TryGetItemQualityById(quality).name;
            }
            else return "Unknown";
        }

        /// <summary>
        /// Get the color of the category of this item. 
        /// </summary>
        /// <returns></returns>
        public Color GetTypeColor()
        {
            if (ItemManager.TryGetItemTypesById(type)!=null)
            {
                return ItemManager.TryGetItemTypesById(type).color;
            }
            else return Color.gray;
        }

        /// <summary>
        /// Get the display name of the category of this item. 
        /// </summary>
        /// <returns></returns>
        public string GetTypeName()
        {
            if (ItemManager.TryGetItemTypesById(type)!=null)
            {
                return ItemManager.TryGetItemTypesById(type).name;
            }
            else return "Unknown";
        }
    }
    #endregion

    #region InventoryStack Class
    [System.Serializable]
    public class InventoryStack
    {
        /// <summary>
        /// The Item object in this InventoryStack.
        /// </summary>
        public Item Item;
        /// <summary>
        /// The number of items in this InventoryStack.
        /// </summary>
        public int Number = 0;
        /// <summary>
        /// Wehther this InventoryStack is empty
        /// </summary>
        public bool Empty = true;
        public bool Fold = true;

        public InventoryStack(ItemSave _item)
        {
            if (ItemManager.TryGetItem(_item.uid)!=null)
            {
                Item = ItemManager.TryGetItem(_item.uid).Copy();
                Item.upgradeLevel = _item.upgrade;
                Item.enchantments.Clear();
                Item.enchantments.AddRange(_item.enchantments);
                Item.socketedItems.Clear();
                if(_item.socketedItem!=null) Item.socketedItems.AddRange(_item.socketedItem);
                Item.socketingSlots = Item.socketedItems.Count;
                if (_item.attributeLock != null && _item.attributeValue!=null)
                {
                    for (int i = 0; i < Mathf.Min(_item.attributeValue.Length, _item.attributeLock.Length, Item.attributes.Count); i++)
                    {
                        Item.attributes[i].locked = _item.attributeLock[i];
                        Item.attributes[i].value = _item.attributeValue[i];
                    }
                }
                Item.favorite = _item.fav;
                Number = _item.number;
                Empty = false;
            }
            else
            {
                Item = null;
                Empty = true;
                Number = 0;
                Debug.LogError("Failed to create InventoryItem with invalid item uid!");
            }
            Fold = true;
        }
        public InventoryStack(int _uid, int _number)
        {
            if (ItemManager.TryGetItem(_uid)!=null)
            {
                Item = ItemManager.TryGetItem(_uid).Copy();
                Empty = false;
                Number = _number;
            }
            else
            {
                Item = null;
                Empty = true;
                Number = 0;
                Debug.LogError("Failed to create InventoryItem with invalid item uid!");
            }
            Fold = true;
        }

        public InventoryStack(Item _item, int _number)
        {
            if (_item != null)
                Item = _item.Copy();
            else
                Item = null;
            Empty = false;
            Number = _number;
            Fold = true;
        }

        public InventoryStack()
        {
            Empty = true;
            Item = null;
            Number = 0;
            Fold = true;
        }

        /// <summary>
        /// Get an instance of this InventoryStack.
        /// </summary>
        /// <returns></returns>
        public InventoryStack Copy()
        {
            InventoryStack _newStack = new InventoryStack();
            if (Item != null)
                _newStack.Item = Item.Copy();
            else
                _newStack.Item = null;
            _newStack.Number = Number;
            _newStack.Empty = Empty;
            _newStack.Fold = true;
            return _newStack;
        }


        /// <summary>
        /// Merges another InventoryStack (_source) with this one if they contain the same item; otherwise, it swaps them. Returns the resulting stack. You should assign the result to the source.
        /// The resulting stack is what's left after merge. 
        /// For example, if an InventoryStack has 80 "HP Potion", and the maxmium stack number of this "HP Potion" is 99, you trying to merge another InventoryStack object with 30 "HP Potion", 
        /// it will make this InventoryStack has 99  "HP Potion" and return a new InventoryStack object with 11 "HP Potion".
        /// </summary>
        /// <param name="_source"></param>
        /// <returns></returns>
        public InventoryStack Merge(InventoryStack _source)
        {
            bool _swap = false;
            if (Item != null && !Empty)
            {
                if (Item.uid != _source.Item.uid || Number >= Item.maxiumStack)
                {
                    _swap = true;
                }
            }
            else
            {
                _swap = true;
            }

            if (_swap) {
                InventoryStack _mCopy = Copy();
                Item = _source.Item.Copy();
                Number = _source.Number;
                Empty = _source.Empty;
                return _mCopy;
            }

            int _free = Mathf.Max(0, _source.Item.maxiumStack - Number);
            int _add = Mathf.Min(_source.Number, _free);
            AddNumber(_add);
            InventoryStack _result = _source.Copy();
            _result.AddNumber(-_add);
            return _result;
        }

        /// <summary>
        /// Split this InventoryStack into a new InventoryStack object with specified number.
        /// </summary>
        /// <param name="_number"></param>
        /// <returns>Return the new InventoryStack object with specified number split out from this InventoryStack</returns>
        public InventoryStack Split(int _number)
        {
            if (isEmpty() || _number <= 0) return null;
            InventoryStack _split = Copy();
            _number = Mathf.Min(Number, _number);
            _split.OverideNumber(_number);
            AddNumber(-_number);
            return _split;
        }

        /// <summary>
        /// Set the data of this InventoryStack.
        /// </summary>
        /// <param name="_data"></param>
        public void Set(InventoryStack _data)
        {
            if (_data.Item != null)
                Item = _data.Item.Copy();
            else
                Item = null;
            Number = _data.Number;
            Empty = _data.Empty;
        }

        /// <summary>
        /// Get the total weight of the items in this InventoryStack.
        /// </summary>
        /// <returns></returns>
        public float GetWeight()
        {
            if (isEmpty()) return 0F;
            return Item.weight * Number;
        }

        /// <summary>
        /// Returns whether the items in this InventoryStack have the specified tag.
        /// </summary>
        /// <param name="_tag"></param>
        /// <returns></returns>
        public bool isTagMatchText(string _tag)
        {
            if (Item != null)
                return Item.isTagMatchText(_tag);
            else
                return false;
        }

        /// <summary>
        /// Returns whether the tags of this item match the specified tag list.
        /// </summary>
        /// <param name="_tags"></param>
        /// <param name="_allMatch"></param>
        /// <returns></returns>
        public bool isTagsMatchList(List<string> _tags, bool _allMatch = true)
        {
            if (Item == null) return false;
            return Item.isTagsMatchList(_tags, _allMatch);
        }

        /// <summary>
        /// Returns whether the items in this InventoryStack have any tag contains the specified text.
        /// </summary>
        /// <param name="_text"></param>
        /// <param name="_caseSensitive"></param>
        /// <returns></returns>
        public bool isTagContainText(string _text, bool _caseSensitive = true)
        {
            if (Item == null) return false;
            return Item.isTagContainText(_text, _caseSensitive);
        }

        /// <summary>
        /// Returns the tag of the item which contains the specified text.
        /// </summary>
        /// <param name="_text"></param>
        /// <param name="_caseSensitive"></param>
        /// <returns></returns>
        public string GetTagContainText(string _text, bool _caseSensitive = true)
        {
            if (Item == null) return "";
            return Item.GetTagContainText(_text, _caseSensitive);
        }

        /// <summary>
        /// Returns the UID of the items in this InventoryStack. Returns -1 if the stack is empty.
        /// </summary>
        /// <returns></returns>
        public int GetItemId()
        {
            if (Item != null)
                return Item.uid;
            else
                return -1;
        }

        /// <summary>
        /// Returns the category ID of the items in this InventoryStack. Returns _emptyResult if the stack is empty.
        /// </summary>
        /// <param name="_emptyResult"></param>
        /// <returns></returns>
        public int GetType(int _emptyResult=0)
        {
            if (!isEmpty())
            {
                return Item.type;
            }
            else
            {
                return _emptyResult;
            }
        }

        /// <summary>
        /// Returns the upgrade level of the items in this InventoryStack.
        /// </summary>
        /// <returns></returns>
        public int GetUpgradeLevel()
        {
            if (!isEmpty())
            {
                return Item.upgradeLevel;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Returns the available space in this stack. For example, if the maximum stack size is 99 and the current quantity is 10, the available space would be 89.
        /// </summary>
        /// <returns></returns>
        public int GetAvailableSpace()
        {
            if (Item != null && !Empty)
            {
                return Mathf.Max(0, Item.maxiumStack - Number);
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Returns whether the items in this InventoryStack match the provided conditions
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_upgradeLevel"></param>
        /// <param name="_enchantments"></param>
        /// <param name="_sockets"></param>
        /// <returns></returns>
        public bool isSameItem(int _uid, int _upgradeLevel,List<int> _enchantments, List<int> _sockets)
        {
            if (!isEmpty())
            {
                return Item.uid == _uid && Item.upgradeLevel == _upgradeLevel 
                    && Item.enchantments.TrueForAll(_enchantments.Contains) && Item.enchantments.Count == _enchantments.Count
                    && Item.socketedItems.TrueForAll(_sockets.Contains) && Item.socketedItems.Count == _sockets.Count;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns whether the items in this InventoryStack match the provided conditions
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_upgradeLevel"></param>
        /// <param name="_enchantments"></param>
        /// <returns></returns>
        public bool isSameItem(int _uid, int _upgradeLevel, List<int> _enchantments)
        {
            if (!isEmpty())
            {
                return Item.uid == _uid && Item.upgradeLevel == _upgradeLevel
                    && Item.enchantments.TrueForAll(_enchantments.Contains) && Item.enchantments.Count == _enchantments.Count;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns whether the items in this InventoryStack match the provided conditions
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_upgradeLevel"></param>
        /// <returns></returns>
        public bool isSameItem(int _uid, int _upgradeLevel)
        {
            if (!isEmpty())
            {
                return Item.uid == _uid && Item.upgradeLevel== _upgradeLevel;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns whether the items in this InventoryStack match the provided conditions
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public bool isSameItem(int _uid)
        {
            if (!isEmpty())
            {
                return Item.uid == _uid;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns whether this InventoryStack is empty.
        /// </summary>
        /// <returns></returns>
        public bool isEmpty()
        {
            return Item == null || Number <= 0 || Empty;
        }

        /// <summary>
        /// Returns whether the items in this InventoryStack can be merged with the provided item UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public bool canBeMerged(int _uid)
        {
            if (!isEmpty())
            {
                return Item.uid == _uid;
            }
            else
            {
                return true;
            }
        }
        
        /// <summary>
        /// Delete the items in this InventoryStack.
        /// </summary>
        public void Delete()
        {
            Empty = true;
            Item = null;
            Number = 0;
        }

        /// <summary>
        /// Overide the number of the items in this InventoryStack.
        /// </summary>
        /// <param name="_number"></param>
        public void OverideNumber(int _number)
        {
            Number = Mathf.Max(0, _number);
        }

        /// <summary>
        /// Add number to the items in this InventoryStack. The added number can be negative.
        /// </summary>
        /// <param name="_number"></param>
        public void AddNumber(int _number)
        {
            Number = Mathf.Max(0, Number + _number);
        }

        /// <summary>
        /// Cosume the items in this InventoryStack. Returns the result of success, if the items in this InventoryStack is not enough to consume, returns false.
        /// </summary>
        /// <param name="_number"></param>
        /// <returns></returns>
        public bool Consume(int _number)
        {
            if (Number >= _number)
            {
                Number -= _number;
                return true;
            }
            else
            {
                return false;
            }
        }

       
    }
    #endregion

    #region Item Save Data Class
    [System.Serializable]
    public class ItemSave
    {
        public int uid;
        public int upgrade;
        public int number;
        public bool fav=false;
        public int[] enchantments;
        public int[] socketedItem;
        public bool[] attributeLock=new bool[0];
        public string[] attributeValue=new string[0];
    }

    [System.Serializable]
    public class ItemSaveRoot
    {
        public ItemSave[] items;
        public ItemSave[] hiddenItems;
        public int[] currency;
        public string[] baseAttributeKey = new string[0];
        public string[] baseAttributeValue = new string[0];
    }

    [System.Serializable]
    public class ActionBarSlotSave
    {
        public int key;
        public int itemId = -1;
        public int upgradeLevel = 0;
        public List<int> enchantments = new List<int>();
        public List<int> sockets = new List<int>();
    }

    [System.Serializable]
    public class ActionBarSave
    {
        public ActionBarSlotSave[] slots;
    }

    [System.Serializable]
    public class ActionBarSaveRoot
    {
        public ActionBarSave[] sets;
    }

    #endregion

    #region InventorySnapShot
    [System.Serializable]
    public class InventorySnapShot
    {
        public List<InventoryStack> Stacks = new List<InventoryStack>();
        public List<InventoryStack> HiddenStacks = new List<InventoryStack>();
        public CurrencySet Currency = new CurrencySet();
        public float Weight = 0F;
        public List<int> CurrencyValue
        {
            get
            {
                return Currency.Currency;
            }
        }
        public int GetCurrency(int _type, bool _includeExchangedValue = false)
        {
            return Currency.GetCurrency(_type, _includeExchangedValue);
        }

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

        public InventorySnapShot(InventoryHolder _holder)
        {
            Weight = _holder.GetWeight();
            Stacks = new List<InventoryStack>();
            HiddenStacks = new List<InventoryStack>();
            for (int i = 0; i < _holder.Stacks.Count; i++)
            {
               Stacks.Add(_holder.Stacks[i].Copy());
            }
            for (int i = 0; i < _holder.HiddenStacks.Count; i++)
            {
                HiddenStacks.Add(_holder.HiddenStacks[i].Copy());
            }
            Currency = _holder.Currency.Copy();
        }


    }
    #endregion

    #region Misc Class
    [System.Serializable]
    public class ItemJsonData
    {
        public Item[] items;
    }

    [System.Serializable]
    public class AttributeJsonData
    {
        public Attribute[] attributes;
    }

    [System.Serializable]
    public class EnchantmentJsonData
    {
        public Enchantment[] enchantments;
    }


    [System.Serializable]
    public class StringColorData
    {
        public string name;
        public Color color;
        public bool visible = true;
    }

    [System.Serializable]
    public class ActionSlotData
    {
        public KeyCode key;
        public int itemId = -1;
        public int upgradeLevel=0;
        public List<int> enchantments = new List<int>();
        public List<int> sockets = new List<int>();
        public ActionSlotData()
        {
            itemId = -1;
            upgradeLevel = 0;
            enchantments = new List<int>();
            sockets = new List<int>();
            key = KeyCode.None;
        }
    }

    [System.Serializable]
    public class ActionSlotSet
    {
        public List<ActionSlotData> slots = new List<ActionSlotData>();
    }

    [System.Serializable]
    public class GraphicsSet
    {
        public string name;
        public Color color;
        public Color defaultColor;
        public MaskableGraphic[] graphics;
        public bool visible = true;
        public bool visibleAdjustable = false;
    }

    [System.Serializable]
    public class RectSet
    {
        public RectTransform rect;
        public Vector2 widthMinMax;
        public Vector2 heightMinMax;
    }

    [System.Serializable]
    public class RectSetting
    {
        public string name;
        public float widthLerp = 0.5F;
        public float heightLerp = 0.5F;
        public RectSet [] set;
        public bool widthAdjustable = false;
        public bool heightAdjustable = false;
    }

    [System.Serializable]
    public class BlockByTag
    {
        public string tag="";
        public ItemIcon blockIcon;
        public InventoryItem blockedItem;
    }

    [System.Serializable]
    public class CustomField
    {
        public string key;
        public LoadMethod loadMethod= LoadMethod.DirectReference;
        public string loadPath;
        public Object value;
        public CustomField Copy()
        {
            CustomField _newObj = new CustomField();
            _newObj.key = key;
            _newObj.value = value;
            _newObj.loadMethod = loadMethod;
            _newObj.loadPath = loadPath;
            return _newObj;
        }
    }
    #endregion



}
