using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Collections.Concurrent;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// The ItemManager component on the "InventoryEngine" prefab contains all the settings for the entire system. Use ItemManager.instance to access its data.
    /// </summary>
    public class ItemManager : MonoBehaviour
    {
        #region Variables
        public List<ClickSetting> clickSettings = new List<ClickSetting>();
        public List<StringColorData> itemTypes = new List<StringColorData>();
        public List<StringColorData> itemQuality = new List<StringColorData>();
        public List<Attribute> itemAttributes = new List<Attribute>();
        public List<Enchantment> itemEnchantments = new List<Enchantment>();
        public List<Currency> currencies = new List<Currency>();
        public List<Item> items = new List<Item>();
        public string SavePathRoot = "SaveData";
        public enum SavePathLocationType
        {
            GameInstallFolder,
            PersistentDataPath,
            Custom
        }
        public SavePathLocationType SavePathLocation;

        public string NameAttributeKey = "name";
        public string LevelAttributeKey = "lvl";
        public string XpAttributeKey = "xp";
        public string MaxXpAttributeKey = "mxp";
        public string CoolDownAttributeKey = "cd";
        public float SharedGlobalCoolDown = 0.5F;

        public Color AttributeNameColor=new Color(0.17F,0.53F,0.82F,1F);
        public bool UseQualityColorForItemName = true;

        public int MerchantStyle=0;
        public bool HighlightEquipmentSlotWhenHoverItem = true;
        public bool AllowDropItem = true;
        public string CanvasTag="";

        public bool EnableCrafting = true;
        public int CraftingMaterialCategoryID = 0;
        public string CraftingBlueprintTag = "Blueprint";
        public string PlayerName = "Player";
        public float CraftingTime = 0.5F;

        public Vector2[] EnhancingMaterials = new Vector2[2];
        public int EnhancingCurrencyType=0;
        public int EnhancingCurrencyNeed = 0;
        public bool EnableEnhancing = true;
        public bool DestroyEquipmentWhenFail = false;
        public int DestroyEquipmentWhenFailLevel = 3;
        public int MaxiumEnhancingLevel = 10;
        public AnimationCurve EnhancingSuccessCurve;
        public int EnhancingCategoryID = 0;
        public float EnhancingTime = 0.5F;
        public bool EnableEnhancingGlow = true;
        public AnimationCurve EnhancingGlowCurve;

        public Vector2 EnchantingMaterial = new Vector2(1,1);
        public int EnchantingCurrencyType = 0;
        public int EnchantingCurrencyNeed = 0;
        public Vector2 EnchantmentNumberRange = new Vector2(1,3);
        public bool EnableEnchanting = true;
        public bool RandomEnchantmentsForNewItem = false;
        public int EnchantingSuccessRate=30;
        public int EnchantingCategoryID = 0;
        public float EnchantingTime = 0.5F;
        public Color EnchantingPrefixesColor=new Color(0.32F, 0.55F,0.18F,1F);
        public Color EnchantingSuffxesColor = new Color(0.32F, 0.55F, 0.18F,1F);
        public Color EnchantingNameColor = new Color(0.32F, 0.55F, 0.18F,1F);

        public bool EnableSocketing = true;
        public string SocketingTagFilter = "";
        public int SocketingCategoryFilter = 0;
        public int SocketedCategoryFilter = 0;
        public bool EnableRemoveSocketing = true;
        public int RemoveSocketingPrice = 100;
        public int RemoveSocketingCurrency = 0;
        public bool DestroySocketItemWhenRemove = false;
        public bool RandomSocketingSlotsNummber = true;
        public int MinimalSocketingSlotsNumber = 0;
        public int MaxmiumSocketingSlotsNumber = 3;
        public bool LockSocketingSlotsByDefault = true;
        public int RandomChanceToLockSocketingSlots = 25;
        public int UnlockSocketingSlotsPrice = 50;
        public int UnlockSocketingSlotsCurrency = 0;

        public string msgBagFull = "Sorry, the bag is full.";
        public string msgItemUseRestricted = "Sorry, you can not use this item because of your {name} is less than {value}.";
        public string msgItemAssign = "Sorry, you can not assign this item here.";
        public string msgEnhancingFail = "Failed! [ <color=#FFA209>{name}</color> ] break into pieces.";
        public AssetLoader myAssetLoader;

        #endregion

        #region Internal Methods
        void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            GameObject.DontDestroyOnLoad(gameObject);
           
            if (GetComponent<AssetLoader>())
            {
                myAssetLoader = GetComponent<AssetLoader>();
            }
            else
            {
                Debug.LogWarning("<AssetLoader> component not found on InventoryEngine prefab, automatically added one.");
                myAssetLoader = gameObject.AddComponent<AssetLoader>();
            }
            ReInit();
        }

        public void ReInit()
        {
            itemTypesDic.Clear();
            itemQualityDic.Clear();
            itemDic.Clear();
            enchantmentDic.Clear();
            for (int i = 0; i < itemTypes.Count; i++)
            {
                itemTypesDic.TryAdd(i, itemTypes[i]);
            }
            for (int i = 0; i < itemQuality.Count; i++)
            {
                itemQualityDic.TryAdd(i, itemQuality[i]);
            }
            for (int i = 0; i < items.Count; i++)
            {
                itemDic.TryAdd(items[i].uid, items[i]);
            }
            for (int i = 0; i < itemEnchantments.Count; i++)
            {
                enchantmentDic.TryAdd(itemEnchantments[i].uid, itemEnchantments[i]);
            }
            SetCoolDownForAll(0F);
           
        }

        public void UpdatePrefab()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        #endregion

        /// <summary>
        /// Get the instance of the ItemManager component, so you can access its data.
        /// </summary>
        public static ItemManager instance;

        private static readonly object _coolDownModificationLock = new object();
        public static void SetCoolDownForAll(float _coolDownTime, bool _onlyUseableItem = true)
        {
            lock (_coolDownModificationLock)
            {
                foreach (var key in itemDic.Keys)
                {
                    if (itemDic[key].useable || !_onlyUseableItem) itemDic[key].SetRemainCoolDownTime(_coolDownTime);
                }
            }
        }

        public static void AddCoolDownForAll(float _addValue,bool _onlyUseableItem=true)
        {
            lock (_coolDownModificationLock)
            {
                foreach (var key in itemDic.Keys)
                {
                    if (itemDic[key].useable || !_onlyUseableItem) itemDic[key].AddRemainCoolDownTime(_addValue);
                }
            }
        }

        public static void SetSharedGlobalCoolDown(float _coolDownTime, bool _onlyUseableItem = true)
        {
            lock (_coolDownModificationLock)
            {
                foreach (var key in itemDic.Keys)
                {
                    if (itemDic[key].GetRemainCoolDownTime() < _coolDownTime && (itemDic[key].useable || !_onlyUseableItem)) itemDic[key].SetRemainCoolDownTime(_coolDownTime);
                }
            }
        }

        /// <summary>
        /// Access the [Item Catogory] data by the id.
        /// </summary>
        public static ConcurrentDictionary<int, StringColorData> itemTypesDic = new ConcurrentDictionary<int, StringColorData>();
        /// <summary>
        /// Try get the [Item Catogory] data by the id.(thread-safe)
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public static StringColorData TryGetItemTypesById(int _id)
        {
            if (instance == null) return null;
            StringColorData _setting;
            if (itemTypesDic.TryGetValue(_id, out _setting))
            {
                return _setting;
            }
            return null;
        }
        /// <summary>
        /// Access the [Item Quality] data by the id.
        /// </summary>
        public static ConcurrentDictionary<int, StringColorData> itemQualityDic = new ConcurrentDictionary<int, StringColorData>();
        /// <summary>
        /// Try get the [Item Quality] data by the id.(thread-safe)
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public static StringColorData TryGetItemQualityById(int _id)
        {
            if (instance == null) return null;
            StringColorData _setting;
            if (itemQualityDic.TryGetValue(_id, out _setting))
            {
                return _setting;
            }
            return null;
        }
        /// <summary>
        /// Access the item data by its UID.  Use itemDic[_uid].Copy() to get an instance of the item data with specified UID.
        /// </summary>
        public static ConcurrentDictionary<int, Item> itemDic = new ConcurrentDictionary<int, Item>();
        /// <summary>
        /// Try get the item data by uid. (thread-safe)
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public static Item TryGetItem(int _id)
        {
            if (instance == null) return null;
            if (_id < instance.items.Count && _id >= 0)
            {
                Item item;
                if (itemDic.TryGetValue(_id, out item))
                {
                    return item;
                }
                return null;
            }
            else
            {
                return null;
            }
        }
        
        /// <summary>
        /// Access the [Item Enchantment] data by the id.
        /// </summary>
        public static ConcurrentDictionary<int, Enchantment> enchantmentDic = new ConcurrentDictionary<int, Enchantment>();
        /// <summary>
        /// Try get the [Enchantment] data by the id.(thread-safe)
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public static Enchantment TryGetEnchantmentById(int _id)
        {
            if (instance == null) return null;
            Enchantment _enchantment;
            if (enchantmentDic.TryGetValue(_id, out _enchantment))
            {
                return _enchantment;
            }
            return null;
        }
        /// <summary>
        /// Get the InventoryHolder component of the player's equipments.
        /// </summary>
        public static InventoryHolder PlayerEquipmentHolder;
        /// <summary>
        /// Get the InventoryHolder component of the player's inventory.
        /// </summary>
        public static InventoryHolder PlayerInventoryHolder;

       
        /// <summary>
        /// Get the Attribute setting by its script key.
        /// </summary>
        /// <param name="_attributeKey"></param>
        /// <returns></returns>
        public  Attribute GetAtttibute(string _attributeKey)
        {
            foreach (var obj in itemAttributes)
            {
                if (obj.key == _attributeKey) return obj;
            }
            return null;
        }
  
        /// <summary>
        /// The InventoryHolder of player will auto call this method on Awake(), but if you can call this if you want to manual set this.
        /// </summary>
        /// <param name="_equipmentHolder"></param>
        /// <param name="_inventoryHolder"></param>
        public static void SetPlayer(InventoryHolder _equipmentHolder, InventoryHolder _inventoryHolder)
        {
            PlayerEquipmentHolder = _equipmentHolder;
            PlayerInventoryHolder = _inventoryHolder;
        }

 
        /// <summary>
        /// Set the player's nick name, so when they craft item, their name will show as the creator name attribute.
        /// </summary>
        /// <param name="_name"></param>
        public static void SetPlayerName(string _name)
        {
            instance.PlayerName = _name;
        }


        /// <summary>
        /// Return the full path by providing the sub path with the file name.
        /// </summary>
        /// <param name="_fileName"></param>
        /// <returns></returns>
        public static string GetFullSavePath(string _fileName)
        {
            string _root = "";
#if UNITY_EDITOR
            _root = Application.dataPath + "/../";
#else
            if (instance.SavePathLocation == SavePathLocationType.GameInstallFolder)
            {
                _root = Application.dataPath + "/../";
            }else if (instance.SavePathLocation == SavePathLocationType.PersistentDataPath) {
                _root = Application.persistentDataPath + "/";
            }
#endif
            string _path = _root + instance.SavePathRoot + "/" + _fileName;
            if (Path.IsPathRooted(_fileName)) _path= _fileName;
            string _dirPath = _path.Replace(Path.GetFileName(_fileName), "");
            if (!Directory.Exists(_dirPath)) Directory.CreateDirectory(_dirPath);//There might be some sub folder is missing within the save path, create them when needed.
            return _path;
        }

        /// <summary>
        /// Export the item data into a json txt file.
        /// </summary>
        /// <param name="_path"></param>
        public void ExportItemDataToJson(string _path)
        {
            ItemJsonData _root = new ItemJsonData();
            _root.items = new Item[items.Count];
            for (int i = 0; i < _root.items.Length; i++)
            {
                Item _newItem = items[i].Copy();
#if UNITY_EDITOR
                if (_newItem.iconLoadMethod == LoadMethod.DirectReference && _newItem.icon!=null)
                {
                    _newItem.iconPath = UnityEditor.AssetDatabase.GetAssetPath(_newItem.icon);
                }
                foreach (var obj in _newItem.customData) {
                    if (obj.loadMethod== LoadMethod.DirectReference && obj.value!=null) {
                        obj.loadPath= UnityEditor.AssetDatabase.GetAssetPath(obj.value);
                    }
                }
#endif
                _newItem.fold = false;
                _root.items[i] = _newItem;
            }
            string _json = JsonUtility.ToJson(_root);
            File.WriteAllText(_path, _json, System.Text.Encoding.UTF8);
        }


        /// <summary>
        /// Import item data from a json txt file. 
        /// You can store the json file in your game install folder and let modder to modify it, then import it back when game launch.
        /// </summary>
        /// <param name="_path"></param>
        public void ImportItemDataFromJson(string _path)
        {
            if (!File.Exists(_path)) return;
            string _json=File.ReadAllText(_path, System.Text.Encoding.UTF8);
            ItemJsonData _root=null;
            try
            {
                _root = JsonUtility.FromJson<ItemJsonData>(_json);

            }
            catch
            {
                return;
            }

            if (_root!=null) {
                for (int i = 0; i < _root.items.Length; i++)
                {
#if UNITY_EDITOR
                    if (_root.items[i].iconLoadMethod == LoadMethod.DirectReference &&_root.items[i].iconPath.Length>0)
                    {
                        _root.items[i].icon = (Texture2D)UnityEditor.AssetDatabase.LoadAssetAtPath(_root.items[i].iconPath,typeof(Texture2D));
                    }
                    foreach (var obj in _root.items[i].customData)
                    {
                        if (obj.loadMethod == LoadMethod.DirectReference && obj.loadPath.Length>0)
                        {
                            obj.value = UnityEditor.AssetDatabase.LoadAssetAtPath(obj.loadPath,typeof(UnityEngine.Object));
                        }
                    }
#endif
                    _root.items[i].fold = false;
                }
                items.Clear();
                items.AddRange(_root.items);
                if (Application.isPlaying)
                {
                    ReInit();
                }
            }

        }


        /// <summary>
        /// Export the Attribute data into a json txt file.
        /// </summary>
        /// <param name="_path"></param>
        public void ExportAttributeDataToJson(string _path)
        {
            AttributeJsonData _root = new AttributeJsonData();
            _root.attributes = new Attribute[itemAttributes.Count];
            for (int i = 0; i < _root.attributes.Length; i++)
            {
                Attribute _newObj = itemAttributes[i].Copy();
                _newObj.fold = false;
                _root.attributes[i] = _newObj;
            }
            string _json = JsonUtility.ToJson(_root);
            File.WriteAllText(_path, _json, System.Text.Encoding.UTF8);
        }


        /// <summary>
        /// Import Attribute data from a json txt file. 
        /// You can store the json file in your game install folder and let modder to modify it, then import it back when game launch.
        /// </summary>
        /// <param name="_path"></param>
        public void ImportAttributeDataFromJson(string _path)
        {
            if (!File.Exists(_path)) return;
            string _json = File.ReadAllText(_path, System.Text.Encoding.UTF8);
            AttributeJsonData _root = null;
            try
            {
                _root = JsonUtility.FromJson<AttributeJsonData>(_json);

            }
            catch
            {
                return;
            }

            if (_root != null)
            {
                for (int i = 0; i < _root.attributes.Length; i++)
                {
                    _root.attributes[i].fold = false;
                }
                itemAttributes.Clear();
                itemAttributes.AddRange(_root.attributes);
                if (Application.isPlaying)
                {
                    ReInit();
                }
            }
        }

        /// <summary>
        /// Export the Enchantment data into a json txt file.
        /// </summary>
        /// <param name="_path"></param>
        public void ExportEnchantmentDataToJson(string _path)
        {
            EnchantmentJsonData _root = new EnchantmentJsonData();
            _root.enchantments = new Enchantment[itemEnchantments.Count];
            for (int i = 0; i < _root.enchantments.Length; i++)
            {
                Enchantment _newObj = itemEnchantments[i];
                _newObj.fold = false;
                _root.enchantments[i] = _newObj;
            }
            string _json = JsonUtility.ToJson(_root);
            File.WriteAllText(_path, _json, System.Text.Encoding.UTF8);
        }


        /// <summary>
        /// Import Enchantment data from a json txt file. 
        /// You can store the json file in your game install folder and let modder to modify it, then import it back when game launch.
        /// </summary>
        /// <param name="_path"></param>
        public void ImportEnchantmentDataFromJson(string _path)
        {
            if (!File.Exists(_path)) return;
            string _json = File.ReadAllText(_path, System.Text.Encoding.UTF8);
            EnchantmentJsonData _root = null;
            try
            {
                _root = JsonUtility.FromJson<EnchantmentJsonData>(_json);

            }
            catch
            {
                return;
            }

            if (_root != null)
            {
                for (int i = 0; i < _root.enchantments.Length; i++)
                {
                    _root.enchantments[i].fold = false;
                }
                itemEnchantments.Clear();
                itemEnchantments.AddRange(_root.enchantments);
                if (Application.isPlaying)
                {
                    ReInit();
                }
            }

        }

    }
}
