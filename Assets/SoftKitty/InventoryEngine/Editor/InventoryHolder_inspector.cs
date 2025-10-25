using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SoftKitty.InventoryEngine
{
    [CustomEditor(typeof(InventoryHolder))]
    public class InventoryHolder_inspector : Editor
    {
        bool _itemExpand = false;
        bool _hiddenExpand = false;
        bool _currencyExpand = false;
        bool _callbackExpand = false;
        bool _merchantExpand = false;
        bool _statsExpand = false;
        bool _tradeListExpand = false;
        bool _tradeCategoryListExpand = false;
        bool _specificPriceExpand = false;
        int _tradeItemId = 0;
        int _tradeType = 0;
        int _tradeCategoryType = 0;
        int _attIndex = 0;
        float _attValue = 0F;
        string _attStringValue = "";
        Color _activeColor = new Color(0.1F, 0.3F, 0.5F);
        Color _disableColor = new Color(0F, 0.1F, 0.3F);
        Color _actionColor = new Color(0F, 1F, 0.4F);
        Color _titleColor = new Color(0.3F, 0.5F, 1F);
        Color _buttonColor = new Color(0F, 0.8F, 0.3F);
        GUIStyle _titleButtonStyle;
        ItemManager Manager;
        ItemEditorAssets EditorAsset;
        string _search = "Search item by name";
        

        private Texture2D GetIcon(int _id)
        {
            if (EditorAsset != null && Manager!=null)
            {
                if (_id >= 0 && _id < Manager.items.Count)
                {
                    if (Manager.items[_id].iconLoadMethod == LoadMethod.DirectReference)
                    {
                        return Manager.items[_id].icon;
                    }
                    else if (EditorAsset.ItemAssets.Count > _id) {
                        return EditorAsset.ItemAssets[_id].icon;
                    } 
                    else if (Manager.items[_id].icon!=null) {
                        return Manager.items[_id].icon;
                    }
                }

            }
            return null;
        }

        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            bool _valueChanged = false;
            _titleButtonStyle = new GUIStyle(GUI.skin.button);
            _titleButtonStyle.alignment = TextAnchor.MiddleLeft;
            Color _backgroundColor = GUI.backgroundColor;
            var script = MonoScript.FromScriptableObject(this);
            InventoryHolder myTarget = (InventoryHolder)target;

            string _thePath = AssetDatabase.GetAssetPath(script);
            _thePath = _thePath.Replace("InventoryHolder_inspector.cs", "");
            Texture logoIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Logo.png", typeof(Texture));
            Texture warningIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "warning.png", typeof(Texture));
            if (EditorAsset == null) EditorAsset = (ItemEditorAssets)AssetDatabase.LoadAssetAtPath(_thePath + "ItemEditorAssets.asset", typeof(ItemEditorAssets));
            GUILayout.BeginHorizontal();
            GUILayout.Box(logoIcon);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("Each <InventoryHolder> is a unique container of items. The player should have two <InventoryHolder> components: Inventory & Equipment. " +
                "Other units that carry items should also have their own <InventoryHolder>, such as Merchant, Crates, or Monsters. " +
                "There is no performance cost to having this component on many units; it only functions as a database.", MessageType.Info, true);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUI.backgroundColor = new Color(1F, 0.8F, 0.1F, 1F);
            if (GUILayout.Button(new GUIContent("Help", "Open the [UserGuide] pdf."), GUILayout.Width(120)))
            {
                string _path = Application.dataPath.Substring(0, Application.dataPath.Length - 6) + _thePath.Replace("Editor", "Documentation") + "UserGuide.pdf";
                Application.OpenURL(_path);
            }
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            if (Manager == null)
            {
                Manager = FindObjectOfType<ItemManager>();
            }

            if (Manager == null)
            {
                GameObject _managerObj = AssetDatabase.LoadAssetAtPath<GameObject>(_thePath.Replace("Editor/", "Prefabs/System/InventoryEngine.prefab"));
                if (_managerObj != null)
                {
                    Manager = _managerObj.GetComponent<ItemManager>();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                    GUI.color = Color.red;
                    GUILayout.Label("You must have ItemManager prefab in the scene to modify this script.");
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                    return;
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Type:", "Make sure set matched type, this will change behavior of this component."), GUILayout.Width(150));
            myTarget.Type = (InventoryHolder.HolderType)EditorGUILayout.EnumPopup(myTarget.Type, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Display Name:", "This will be the name shows as the title of the window."), GUILayout.Width(150));
            myTarget.Name = GUILayout.TextField(myTarget.Name);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Auto Save:", "When any data changes, check this will auto save data to the [Save Data Path]."), GUILayout.Width(150));
            myTarget.AutoSave = GUILayout.Toggle(myTarget.AutoSave, "");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Auto Load:", "Check this will auto load data from the [Save Data Path]."), GUILayout.Width(150));
            myTarget.AutoLoad = GUILayout.Toggle(myTarget.AutoLoad, "");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Save Data Path:", "When auto-saving or you manually calling <InventoryHolder>().Save(), data will be saved into this path. (file name and extension required.)"), GUILayout.Width(150));
            myTarget.SavePath = GUILayout.TextField(myTarget.SavePath);
            GUILayout.EndHorizontal();

            if (myTarget.SavePath.Length > 0)
            {
                if (myTarget.SavePath.IndexOf(".")<=0 || myTarget.SavePath.IndexOf(".")> myTarget.SavePath.Length-4) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.color = Color.red;
                    GUILayout.Label("file name and extension required");
                    GUILayout.EndHorizontal();
                }
                GUI.backgroundColor = Color.black;
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.color = new Color(0F, 0.7F, 1F, 1F);
                GUILayout.Box("[Editor] ", _titleButtonStyle, GUILayout.Width(50));
                GUI.color = Color.white;
                GUILayout.Box("Project Root Folder/" + Manager.SavePathRoot + "/" + myTarget.SavePath, _titleButtonStyle, GUILayout.Width(320));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.color = new Color(0F, 1F, 0.5F, 1F);
                GUILayout.Box("[Build] ", _titleButtonStyle, GUILayout.Width(50));
                GUI.color = Color.white;
                GUILayout.Box("Game Install Folder/" + Manager.SavePathRoot + "/" + myTarget.SavePath, _titleButtonStyle, GUILayout.Width(320));
                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                GUI.color = Color.white;
            }


            EditorGUILayout.Separator();



            GUILayout.BeginHorizontal();
            GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
            GUILayout.Label("Click this if you have changed the item settings in DataBase:", GUILayout.Width(400));
            GUILayout.EndHorizontal();



            GUILayout.BeginHorizontal();
            GUI.backgroundColor = _buttonColor;
            GUILayout.Space(25);
            if (GUILayout.Button(new GUIContent("Copy data base settings to all items", "If you have changed the item settings in DataBase, click this to sync the changes."), GUILayout.Width(300)))
            {
                for (int i = 0; i < myTarget.Stacks.Count; i++)
                {
                    if (!myTarget.Stacks[i].Empty && myTarget.Stacks[i].Item != null)
                    {
                        int _level = myTarget.Stacks[i].Item.upgradeLevel;
                        myTarget.Stacks[i].Item = Manager.items[myTarget.Stacks[i].GetItemId()].Copy();
                        myTarget.Stacks[i].Item.upgradeLevel = _level;
                    }
                }
                for (int i = 0; i < myTarget.HiddenStacks.Count; i++)
                {
                    if (!myTarget.HiddenStacks[i].Empty && myTarget.HiddenStacks[i].Item != null)
                    {
                        int _level = myTarget.HiddenStacks[i].Item.upgradeLevel;
                        myTarget.HiddenStacks[i].Item = Manager.items[myTarget.HiddenStacks[i].GetItemId()].Copy();
                        myTarget.HiddenStacks[i].Item.upgradeLevel = _level;
                    }
                }
            }
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            if (myTarget.Type == InventoryHolder.HolderType.PlayerEquipment || myTarget.Type == InventoryHolder.HolderType.NpcEquipment)
            {

                for (int i = 0; i < myTarget.BaseStats.Count; i++)
                {
                    Attribute _att = Manager.GetAtttibute(myTarget.BaseStats[i].key);
                    if (_att == null)
                    {
                        myTarget.BaseStats.RemoveAt(i);
                    }
                    else if (_att.name != myTarget.BaseStats[i].name || _att.upgradeIncrement != myTarget.BaseStats[i].upgradeIncrement || _att.visible != myTarget.BaseStats[i].visible
                        || _att.displayFormat != myTarget.BaseStats[i].displayFormat || _att.suffixes != myTarget.BaseStats[i].suffixes || _att.coreStats != myTarget.BaseStats[i].coreStats 
                        || _att.compareInfo != myTarget.BaseStats[i].compareInfo || _att.visibleInStatsPanel != myTarget.BaseStats[i].visibleInStatsPanel || _att.stringValue != myTarget.BaseStats[i].stringValue)
                    {
                        myTarget.BaseStats[i].name = _att.name;
                        myTarget.BaseStats[i].upgradeIncrement = _att.upgradeIncrement;
                        myTarget.BaseStats[i].locked = false;
                        myTarget.BaseStats[i].visible = _att.visible;
                        myTarget.BaseStats[i].displayFormat = _att.displayFormat;
                        myTarget.BaseStats[i].suffixes = _att.suffixes;
                        myTarget.BaseStats[i].coreStats = _att.coreStats;
                        myTarget.BaseStats[i].compareInfo = _att.compareInfo;
                        myTarget.BaseStats[i].visibleInStatsPanel = _att.visibleInStatsPanel;
                        myTarget.BaseStats[i].stringValue = _att.stringValue;
                    }
                }

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _statsExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _statsExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_statsExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Base Stats", "The base stats of this character without any equipment."), _titleButtonStyle))
                {
                    _statsExpand = !_statsExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.color = Color.white;
                GUI.backgroundColor = _backgroundColor;
                GUILayout.EndHorizontal();

                if (_statsExpand)
                {
                    if (Manager.itemAttributes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(50);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one attribute setup in ItemManager prefab.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.HelpBox("Use <InventoryHolder>().GetBaseStatsValue(string _key) to get the base stats value.\n" +
                            "Use <InventoryHolder>().SetBaseStatsValue(string _key, float _value) to update the base stats value.\n" +
                            "For example:\n" +
                            "ItemManager.PlayerEquipmentHolder.SetBaseStatsValue('lvl',20);", MessageType.Info, true);
                        GUILayout.EndHorizontal();

                        string[] _attributeOptions = new string[Manager.itemAttributes.Count];
                        for (int i = 0; i < _attributeOptions.Length; i++) _attributeOptions[i] = Manager.itemAttributes[i].name;

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(40);
                        GUILayout.Label("New:", GUILayout.Width(50));
                        _attIndex = Mathf.Clamp(_attIndex, 0, Manager.itemAttributes.Count - 1);
                        GUI.backgroundColor = _titleColor;
                        _attIndex = EditorGUILayout.Popup("", _attIndex, _attributeOptions, GUILayout.Width(150));
                        GUI.backgroundColor = Color.white;
                        if (Manager.itemAttributes[_attIndex].stringValue)
                        {
                            _attStringValue = EditorGUILayout.TextField(_attStringValue, GUILayout.Width(100));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(100);
                            GUI.color = _buttonColor;
                            GUILayout.Label("String Value", GUILayout.Width(145));
                            GUI.color = Color.white;
                            GUI.backgroundColor = _buttonColor;
                            if (GUILayout.Button("Add", GUILayout.Width(100)))
                            {
                                Attribute _newAtt = Manager.itemAttributes[_attIndex].Copy();
                                _newAtt.locked = false;
                                bool _found = false;
                                foreach (var obj in myTarget.BaseStats)
                                {
                                    if (obj.key == _newAtt.key)
                                    {
                                        obj.UpdateValue(_attStringValue);
                                        _found = true;
                                    }
                                }
                                if (!_found)
                                {
                                    _newAtt.UpdateValue(_attStringValue);
                                    myTarget.BaseStats.Add(_newAtt);
                                }
                                _valueChanged = true;
                            }
                            GUI.backgroundColor = Color.white;
                            GUILayout.EndHorizontal();
                        }
                        else
                        {
                            _attValue = EditorGUILayout.FloatField(_attValue, GUILayout.Width(50));
                            GUI.backgroundColor = _buttonColor;
                            if (GUILayout.Button("Add", GUILayout.Width(70)))
                            {
                                Attribute _newAtt = Manager.itemAttributes[_attIndex].Copy();
                                _newAtt.locked = false;
                                bool _found = false;
                                foreach (var obj in myTarget.BaseStats)
                                {
                                    if (obj.key == _newAtt.key)
                                    {
                                        obj.UpdateValue(_attValue);
                                        _found = true;
                                    }
                                }
                                if (!_found)
                                {
                                    _newAtt.UpdateValue(_attValue);
                                    myTarget.BaseStats.Add(_newAtt);
                                }
                                _valueChanged = true;
                            }
                            GUI.backgroundColor = Color.white;
                            GUILayout.EndHorizontal();
                        }


                        EditorGUILayout.Separator();

                        for (int i = 0; i < myTarget.BaseStats.Count; i++)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(40);
                            GUI.backgroundColor = _titleColor;
                            GUILayout.Button(myTarget.BaseStats[i].name, _titleButtonStyle, GUILayout.Width(120));
                            GUI.backgroundColor = Color.white;
                            if (myTarget.BaseStats[i].stringValue)
                            {
                                myTarget.BaseStats[i].value = EditorGUILayout.TextField(myTarget.BaseStats[i].value, GUILayout.Width(100));
                            }
                            else
                            {
                                myTarget.BaseStats[i].value = EditorGUILayout.FloatField(float.Parse(myTarget.BaseStats[i].value), GUILayout.Width(100)).ToString();
                            }
                            GUI.color = _buttonColor;
                            GUILayout.Label("(Key: " + myTarget.BaseStats[i].key + ")", GUILayout.Width(100));
                            GUI.color = Color.white;
                            GUI.backgroundColor = Color.red;
                            if (GUILayout.Button("X", GUILayout.Width(30)))
                            {
                                myTarget.BaseStats.RemoveAt(i);
                                _valueChanged = true;
                            }
                            GUI.backgroundColor = Color.white;
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }

            if (myTarget.Type == InventoryHolder.HolderType.Merchant)
            {
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _merchantExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _merchantExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_merchantExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Merchant Settings", "The settings of merchant."), _titleButtonStyle))
                {
                    _merchantExpand = !_merchantExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.color = Color.white;
                GUI.backgroundColor = _backgroundColor;
                GUILayout.EndHorizontal();

                if (_merchantExpand)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Sell Price Multiplier:", "This multiplier is applied when selling items. For example, a merchant NPC with a SellPriceMultiplier of 1.2 will sell an item priced at 1000 for 1200"), GUILayout.Width(150));
                    myTarget.SellPriceMultiplier = EditorGUILayout.Slider(myTarget.SellPriceMultiplier, 0F, 2F);
                    GUILayout.Label("x", GUILayout.Width(15));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Buy Price Multiplier:", "This multiplier is applied when buying items. For example, a merchant NPC with a BuyPriceMultiplier of 0.8 will buy an item priced at 1000 for 800"), GUILayout.Width(150));
                    myTarget.BuyPriceMultiplier = EditorGUILayout.Slider(myTarget.BuyPriceMultiplier, 0F, 2F);
                    GUILayout.Label("x", GUILayout.Width(15));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    _specificPriceExpand = EditorGUILayout.Foldout(_specificPriceExpand, "Specific Price Multiplier (" + myTarget.SpecificPriceMultiplier .Count+ ")");
                    GUILayout.EndHorizontal();

                    if (_specificPriceExpand) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(50);
                        GUI.backgroundColor = _buttonColor;
                        if (GUILayout.Button("Add",GUILayout.Width(100))) {
                            myTarget.SpecificPriceMultiplier.Add(new Vector3(0F,myTarget.SellPriceMultiplier,myTarget.BuyPriceMultiplier));
                            _valueChanged = true;
                            EditorGUI.FocusTextInControl(null);
                        }
                        GUI.backgroundColor = Color.white;
                        GUILayout.EndHorizontal();
                        string[] _itemOption = new string[Manager.items.Count];
                        for (int i = 0; i < _itemOption.Length; i++) _itemOption[i] = Manager.items[i].name;
                        for (int i=0;i<myTarget.SpecificPriceMultiplier.Count;i++) {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(50);
                            int _itemId = Mathf.FloorToInt(myTarget.SpecificPriceMultiplier[i].x);
                            float _sell = myTarget.SpecificPriceMultiplier[i].y;
                            float _buy = myTarget.SpecificPriceMultiplier[i].z;
                            GUILayout.Box(GetIcon(_itemId) , GUILayout.Width(20), GUILayout.Height(20));
                            GUI.backgroundColor=  Manager.itemTypes[Manager.items[_itemId].type].color;
                            _itemId = EditorGUILayout.Popup("", _itemId, _itemOption, GUILayout.Width(180));
                            GUI.backgroundColor = Color.white;
                            GUILayout.Label("Sell:",GUILayout.Width(30F));
                            _sell= EditorGUILayout.FloatField(_sell,GUILayout.Width(30F));
                            GUILayout.Label("x", GUILayout.Width(20F));
                            GUILayout.Label("Buy:", GUILayout.Width(30F));
                            _buy = EditorGUILayout.FloatField(_buy, GUILayout.Width(30F));
                            GUILayout.Label("x", GUILayout.Width(20F));
                            myTarget.SpecificPriceMultiplier[i] = new Vector3(_itemId,_sell,_buy);
                            GUI.backgroundColor = Color.red;
                            if (GUILayout.Button("X",GUILayout.Width(18))) {
                                myTarget.SpecificPriceMultiplier.RemoveAt(i);
                                _valueChanged = true;
                                EditorGUI.FocusTextInControl(null);
                            }
                            GUI.backgroundColor = Color.white;
                            GUILayout.EndHorizontal();
                        }
                    }

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    myTarget.TradeAllItems = GUILayout.Toggle(myTarget.TradeAllItems, new GUIContent("Accept all tradeable items.", "When unchecked, this merchant will only accept tradeable items within a list."), GUILayout.Width(200));
                    GUILayout.EndHorizontal();

                    if (!myTarget.TradeAllItems)
                    {
                        string[] _typeOption = new string[Manager.itemTypes.Count];
                        for (int i = 0; i < _typeOption.Length; i++) _typeOption[i] = Manager.itemTypes[i].name;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _buttonColor;
                        _tradeCategoryListExpand = EditorGUILayout.Foldout(_tradeCategoryListExpand, "Accept items of these categories:");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();

                        if (_tradeCategoryListExpand)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(40);
                            GUILayout.Label("Category:", GUILayout.Width(60));
                            _tradeCategoryType = Mathf.Clamp(_tradeCategoryType, 0, Manager.itemTypes.Count - 1);
                            GUI.backgroundColor = Manager.itemTypes[_tradeCategoryType].color;
                            _tradeCategoryType = EditorGUILayout.Popup("", _tradeCategoryType, _typeOption, GUILayout.Width(150));

                            GUI.backgroundColor = _buttonColor;
                            if (GUILayout.Button("Add", GUILayout.Width(70)))
                            {
                                myTarget.TradeCategoryList.Add(_tradeCategoryType);
                                _valueChanged = true;
                                EditorGUI.FocusTextInControl(null);
                            }
                            GUI.backgroundColor = Color.white;
                            GUILayout.EndHorizontal();

                            for (int i = 0; i < myTarget.TradeCategoryList.Count; i++)
                            {
                                if (myTarget.TradeCategoryList[i] >= 0 && myTarget.TradeCategoryList[i] < Manager.itemTypes.Count)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(50);
                                    GUI.backgroundColor = Manager.itemTypes[myTarget.TradeCategoryList[i]].color; ;
                                    GUILayout.Button(Manager.itemTypes[myTarget.TradeCategoryList[i]].name, _titleButtonStyle, GUILayout.Width(150));
                                    GUI.backgroundColor = Color.red;
                                    if (GUILayout.Button("X", GUILayout.Width(30)))
                                    {
                                        myTarget.TradeCategoryList.RemoveAt(i);
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                    GUI.backgroundColor = Color.white;
                                    GUILayout.EndHorizontal();
                                }
                                else
                                {
                                    myTarget.TradeCategoryList.RemoveAt(i);
                                }
                            }
                        }


                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _buttonColor;
                        _tradeListExpand = EditorGUILayout.Foldout(_tradeListExpand, "Accept items within this list:");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                        if (_tradeListExpand)
                        {
                            if (Manager.items.Count <= 0)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(50);
                                GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                                GUI.color = Color.red;
                                GUILayout.Label("You must have at least one item setup in ItemManager prefab.");
                                GUI.color = Color.white;
                                GUILayout.EndHorizontal();
                            }
                            else
                            {
                                string[] _itemOption = new string[Manager.items.Count];
                                for (int i = 0; i < _itemOption.Length; i++) _itemOption[i] = Manager.items[i].name;


                                GUILayout.BeginHorizontal();
                                GUILayout.Space(40);
                                Color _color = Manager.itemTypes[Manager.items[_tradeItemId].type].color;
                                GUILayout.Box(GetIcon(_tradeItemId), GUILayout.Width(20), GUILayout.Height(20));
                                GUI.backgroundColor = _color;
                                _tradeItemId = EditorGUILayout.Popup("", _tradeItemId, _itemOption, GUILayout.Width(190));

                                GUI.backgroundColor = _buttonColor;
                                if (GUILayout.Button("Add", GUILayout.Width(70)))
                                {
                                    if (!myTarget.TradeList.Contains(_tradeItemId)) myTarget.TradeList.Add(_tradeItemId);
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }
                                GUI.backgroundColor = Color.white;
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(40);
                                GUILayout.Label("Category:", GUILayout.Width(60));
                                _tradeType = Mathf.Clamp(_tradeType, 0, Manager.itemTypes.Count - 1);
                                GUI.backgroundColor = Manager.itemTypes[_tradeType].color;
                                _tradeType = EditorGUILayout.Popup("", _tradeType, _typeOption, GUILayout.Width(150));

                                GUI.backgroundColor = _buttonColor;
                                if (GUILayout.Button("Add All", GUILayout.Width(70)))
                                {
                                    for (int i = 0; i < Manager.items.Count; i++)
                                    {
                                        if (Manager.items[i].type == _tradeType && !myTarget.TradeList.Contains(i)) myTarget.TradeList.Add(i);
                                    }
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }
                                GUI.backgroundColor = Color.red;
                                if (GUILayout.Button("Remove All", GUILayout.Width(85)))
                                {
                                    for (int i = myTarget.TradeList.Count - 1; i >= 0; i--)
                                    {
                                        if (Manager.items[myTarget.TradeList[i]].type == _tradeType) myTarget.TradeList.RemoveAt(i);
                                    }
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }
                                GUI.backgroundColor = Color.white;
                                GUILayout.EndHorizontal();

                                for (int i = 0; i < myTarget.TradeList.Count; i++)
                                {
                                    if (myTarget.TradeList[i] >= 0 && myTarget.TradeList[i] < Manager.items.Count)
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(50);
                                        Color _itemColor = Manager.itemTypes[Manager.items[myTarget.TradeList[i]].type].color;
                                        GUILayout.Box(GetIcon(myTarget.TradeList[i]), GUILayout.Width(20), GUILayout.Height(20));
                                        GUI.backgroundColor = _itemColor;
                                        GUILayout.Button(Manager.items[myTarget.TradeList[i]].name, _titleButtonStyle, GUILayout.Width(190));
                                        if (!Manager.items[myTarget.TradeList[i]].tradeable)
                                        {
                                            GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                                            GUI.color = Color.red;
                                            GUILayout.Label("NOT tradeable", GUILayout.Width(100));
                                            GUI.color = Color.white;
                                        }
                                        GUI.backgroundColor = Color.red;
                                        if (GUILayout.Button("X", GUILayout.Width(30)))
                                        {
                                            myTarget.TradeList.RemoveAt(i);
                                            _valueChanged = true;
                                            EditorGUI.FocusTextInControl(null);
                                        }
                                        GUI.backgroundColor = Color.white;
                                        GUILayout.EndHorizontal();
                                    }
                                    else
                                    {
                                        myTarget.TradeList.RemoveAt(i);
                                    }
                                }

                            }
                        }
                    }
                }
            }


            GUILayout.BeginHorizontal();
            GUI.backgroundColor = _currencyExpand ? _activeColor : _disableColor;
            _titleButtonStyle.normal.textColor = _currencyExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
            GUI.color = myTarget.Currency.Count <= 0 ? Color.red : Color.white;
            GUILayout.Label(_currencyExpand ? "[-]" : "[+]", GUILayout.Width(20));
            if (GUILayout.Button(new GUIContent(" Currency", "The currencies this unit carries."), _titleButtonStyle))
            {
                _currencyExpand = !_currencyExpand;
                EditorGUI.FocusTextInControl(null);
            }
            GUI.color = Color.white;
            GUI.backgroundColor = _backgroundColor;

            if (Manager.currencies.Count <= 0)
            {
                GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
            }
            else
            {
                if (myTarget.Currency.Count < Manager.currencies.Count)
                {
                    myTarget.Currency.Add(0);
                }
            }
            GUILayout.EndHorizontal();
            if (_currencyExpand)
            {
                if (Manager.currencies.Count <= 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                    GUI.color = Color.red;
                    GUILayout.Label("You must have at least one currency setup in ItemManager prefab.");
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }

                //===Currency List

                for (int i = 0; i < Manager.currencies.Count; i++)
                {
                    if (myTarget.Currency.Count <= i) myTarget.Currency.Add(0);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);


                    GUILayout.Label("(ID:" + i.ToString() + ")", GUILayout.Width(40), GUILayout.Height(20));
                    if (Manager.currencies[i].icon != null) GUILayout.Box(Manager.currencies[i].icon.texture, GUILayout.Width(20), GUILayout.Height(20));
                    GUI.color = Manager.currencies[i].color;
                    GUILayout.Label(Manager.currencies[i].name + " :", GUILayout.Width(100), GUILayout.Height(20));
                    GUI.color = Color.white;

                    GUI.backgroundColor = Manager.currencies[i].color;
                    myTarget.Currency.Currency[i] = EditorGUILayout.IntField(myTarget.Currency.Currency[i], GUILayout.Width(100), GUILayout.Height(20));
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                }
                //===Currency List

            }

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = _itemExpand ? _activeColor : _disableColor;
            _titleButtonStyle.normal.textColor = _itemExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
            GUI.color = Manager.items.Count <= 0 ? Color.red : Color.white;
            GUILayout.Label(_itemExpand ? "[-]" : "[+]", GUILayout.Width(20));
            if (GUILayout.Button(new GUIContent(" Inventory Items", "The items this unit carries."), _titleButtonStyle))
            {
                _itemExpand = !_itemExpand;
                _search = "Search item by name";
                EditorGUI.FocusTextInControl(null);
            }
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;
            if (Manager.items.Count <= 0)
            {
                GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
            }
            GUILayout.EndHorizontal();




            if (_itemExpand)
            {
                if (Manager.items.Count <= 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                    GUI.color = Color.red;
                    GUILayout.Label("You must have at least one item setup in ItemManager prefab.");
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
                else
                {
                    string[] _itemOption = new string[Manager.items.Count + 1];
                    if (_itemExpand || _hiddenExpand)
                    {
                        _itemOption[0] = "Empty";
                        for (int i = 1; i < _itemOption.Length; i++) _itemOption[i] = Manager.items[i - 1].name;
                    }
                    string[] _enchantOptions = new string[Manager.itemEnchantments.Count];
                    for (int i = 0; i < Manager.itemEnchantments.Count; i++) _enchantOptions[i] = Manager.itemEnchantments[i].name;

                    float _weight = 0F;

                    for (int i = 0; i < myTarget.Stacks.Count; i++) _weight += myTarget.Stacks[i].GetWeight();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Maxmium CarryWeight:", "The maxmium carry weight of this unit."), GUILayout.Width(130));
                    GUI.backgroundColor = _actionColor;
                    myTarget.MaxiumCarryWeight = EditorGUILayout.FloatField(myTarget.MaxiumCarryWeight, GUILayout.Width(70));
                    GUI.color = _weight <= myTarget.MaxiumCarryWeight ? Color.green : Color.red;
                    GUILayout.Label("  (" + _weight.ToString("0.0") + "/" + myTarget.MaxiumCarryWeight.ToString("0.0") + ")", GUILayout.Width(100));
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Inventory Size:", "This will be the total count of the slots in the bag."), GUILayout.Width(130));
                    myTarget.InventorySize = EditorGUILayout.IntSlider(myTarget.InventorySize, 1, 200, GUILayout.Width(300));
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = _backgroundColor;

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = _titleColor;
                    if (GUILayout.Button("Expand All", GUILayout.Width(100)))
                    {
                        for (int i = 0; i < myTarget.Stacks.Count; i++) myTarget.Stacks[i].Fold = false;
                    }
                    if (GUILayout.Button("Shrink All", GUILayout.Width(100)))
                    {
                        for (int i = 0; i < myTarget.Stacks.Count; i++) myTarget.Stacks[i].Fold = true;
                    }
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = _backgroundColor;

                    //===Item List

                    if (myTarget.Stacks.Count > myTarget.InventorySize)
                    {
                        for (int i = myTarget.InventorySize; i < myTarget.Stacks.Count; i++) myTarget.Stacks.RemoveAt(i);
                    }
                    else if (myTarget.Stacks.Count < myTarget.InventorySize)
                    {
                        for (int i = myTarget.Stacks.Count; i < myTarget.InventorySize; i++) myTarget.Stacks.Add(new InventoryStack());
                    }


                    for (int i = 0; i < myTarget.Stacks.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        int _item = 0;
                        Color _color = Color.white;
                        if (GUILayout.Button(myTarget.Stacks[i].Fold ? "+" : "-", GUILayout.Width(20)))
                        {
                            myTarget.Stacks[i].Fold = !myTarget.Stacks[i].Fold;
                            _search = "Search item by name";
                        }
                        if (myTarget.Stacks[i].Item != null && !myTarget.Stacks[i].Empty)
                        {
                            if (myTarget.Stacks[i].Item.uid >= Manager.items.Count)
                            {
                                myTarget.Stacks[i].Delete();
                            }
                            else
                            {
                                myTarget.Stacks[i].Item.type = Mathf.Clamp(myTarget.Stacks[i].Item.type, 0, Manager.itemTypes.Count - 1);
                                _item = myTarget.Stacks[i].Item.uid + 1;
                                _color = Manager.itemTypes[myTarget.Stacks[i].Item.type].color;
                                GUILayout.Box(GetIcon(myTarget.Stacks[i].Item.uid), GUILayout.Width(20), GUILayout.Height(20));
                            }
                        }
                        else
                        {
                            GUILayout.Box("-", GUILayout.Width(20), GUILayout.Height(20));
                        }
                        EditorGUI.BeginChangeCheck();
                        GUI.backgroundColor = _color;
                        _item = EditorGUILayout.Popup("", _item, _itemOption, GUILayout.Width(180));

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (_item == 0)
                            {
                                myTarget.Stacks[i].Empty = true;
                                myTarget.Stacks[i].Item = null;
                                myTarget.Stacks[i].Number = 0;
                            }
                            else
                            {
                                myTarget.Stacks[i] = new InventoryStack(Manager.items[_item - 1], Mathf.Max(1, myTarget.Stacks[i].Number));
                            }
                        }
                        GUILayout.Label(" x", GUILayout.Width(20));

                        int _maxStack = 0;
                        if (myTarget.Stacks[i].Item != null && !myTarget.Stacks[i].Empty)
                        {
                            _maxStack = myTarget.Stacks[i].Item.maxiumStack;
                        }

                        GUI.backgroundColor = _titleColor;
                        myTarget.Stacks[i].Number = Mathf.FloorToInt(GUILayout.HorizontalSlider(myTarget.Stacks[i].Number, 0, _maxStack, GUILayout.Width(120)));
                        if (_item == 0) myTarget.Stacks[i].Number = 0;
                        GUILayout.Label(myTarget.Stacks[i].Number.ToString(), GUILayout.Width(30));
                        GUI.backgroundColor = _backgroundColor;

                        GUI.color = _actionColor;
                        if (myTarget.Stacks[i].Item != null && myTarget.Stacks[i].Item.upgradeLevel > 0)
                        {
                            GUILayout.Label("Lv." + myTarget.Stacks[i].Item.upgradeLevel.ToString(), GUILayout.Width(40));
                        }
                        else
                        {
                            GUILayout.Space(40);
                        }
                        GUI.color = Color.white;

                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(30)))
                        {
                            myTarget.Stacks[i].Empty = true;
                            myTarget.Stacks[i].Item = null;
                            myTarget.Stacks[i].Number = 0;
                            _valueChanged = true;
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();

                        if (!myTarget.Stacks[i].Fold)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(80);
                            GUI.color = Color.gray;
                            _search = GUILayout.TextField(_search, GUILayout.Width(180));

                            GUILayout.EndHorizontal();
                            if (_search != "")
                            {
                                for (int u = 0; u < Manager.items.Count; u++)
                                {
                                    if (Manager.items[u].name.ToLower().Contains(_search.ToLower()))
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(100);
                                        if (GUILayout.Button(Manager.items[u].name, GUILayout.Width(160)))
                                        {
                                            myTarget.Stacks[i] = new InventoryStack(Manager.items[u], Mathf.Max(1, myTarget.Stacks[i].Number));
                                            myTarget.Stacks[i].Fold = true;
                                            EditorGUI.FocusTextInControl(null);
                                        }
                                        GUILayout.EndHorizontal();
                                    }
                                }
                            }
                            GUI.color = Color.white;

                            if (myTarget.Stacks[i].Item != null)
                            {
                                if (Manager.EnableEnhancing && myTarget.Stacks[i].Item.type == Manager.EnhancingCategoryID)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(80);
                                    GUILayout.Label("Enhancing Level: ", GUILayout.Width(120));
                                    myTarget.Stacks[i].Item.upgradeLevel = EditorGUILayout.IntSlider(myTarget.Stacks[i].Item.upgradeLevel, 0, Manager.MaxiumEnhancingLevel, GUILayout.Width(230));
                                    GUILayout.EndHorizontal();
                                }

                                if (Manager.EnableEnchanting && Manager.itemEnchantments.Count > 0 && myTarget.Stacks[i].Item.type == Manager.EnchantingCategoryID)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(80);
                                    GUILayout.Label("Enchantments (" + myTarget.Stacks[i].Item.enchantments.Count + "): ", GUILayout.Width(120));
                                    GUI.backgroundColor = _buttonColor;
                                    if (GUILayout.Button("+", GUILayout.Width(30)))
                                    {
                                        myTarget.Stacks[i].Item.enchantments.Add(0);
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();

                                    for (int u = myTarget.Stacks[i].Item.enchantments.Count - 1; u >= 0; u--)
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        int _ent = myTarget.Stacks[i].Item.enchantments[u];
                                        EditorGUI.BeginChangeCheck();

                                        _ent = EditorGUILayout.Popup("", _ent, _enchantOptions, GUILayout.Width(120));

                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            myTarget.Stacks[i].Item.enchantments[u] = _ent;
                                        }

                                        GUILayout.Label(Manager.itemEnchantments[myTarget.Stacks[i].Item.enchantments[u]].GetDescription(), GUILayout.Width(190));

                                        GUI.backgroundColor = Color.red;
                                        if (GUILayout.Button("X", GUILayout.Width(25)))
                                        {
                                            myTarget.Stacks[i].Item.enchantments.RemoveAt(u);
                                        }
                                        GUI.backgroundColor = _backgroundColor;
                                        GUILayout.EndHorizontal();
                                    }
                                }

                                if (Manager.EnableSocketing && myTarget.Stacks[i].Item.type == Manager.SocketedCategoryFilter)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(80);
                                    GUILayout.Label("Socketing Slots (" + myTarget.Stacks[i].Item.socketingSlots.ToString() + "):", GUILayout.Width(120));
                                    GUI.backgroundColor = myTarget.Stacks[i].Item.socketedItems.Count < Manager.MaxmiumSocketingSlotsNumber ? _buttonColor : Color.gray;
                                    if (GUILayout.Button("+", GUILayout.Width(30)))
                                    {
                                        if (myTarget.Stacks[i].Item.socketedItems.Count < Manager.MaxmiumSocketingSlotsNumber)
                                        {
                                            myTarget.Stacks[i].Item.socketedItems.Add(Manager.LockSocketingSlotsByDefault ? -2 : -1);
                                            myTarget.Stacks[i].Item.socketingSlots = myTarget.Stacks[i].Item.socketedItems.Count;
                                        }
                                    }
                                    GUI.backgroundColor = myTarget.Stacks[i].Item.socketedItems.Count > 0 ? Color.red : Color.gray;
                                    if (GUILayout.Button("-", GUILayout.Width(30)))
                                    {
                                        if (myTarget.Stacks[i].Item.socketedItems.Count > 0)
                                        {
                                            myTarget.Stacks[i].Item.socketedItems.RemoveAt(myTarget.Stacks[i].Item.socketedItems.Count - 1);
                                            myTarget.Stacks[i].Item.socketingSlots = myTarget.Stacks[i].Item.socketedItems.Count;
                                        }
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(100);
                                    for (int u = 0; u < myTarget.Stacks[i].Item.socketedItems.Count; u++)
                                    {
                                        GUI.backgroundColor = myTarget.Stacks[i].Item.socketedItems[u] == -2 ? Color.gray : _buttonColor;
                                        if (GUILayout.Button(new GUIContent(myTarget.Stacks[i].Item.socketedItems[u] == -2 ? "X" : "O", myTarget.Stacks[i].Item.socketedItems[u] == -2 ? "Locked" : "Available"), GUILayout.Width(30)))
                                        {
                                            if (myTarget.Stacks[i].Item.socketedItems[u] == -2)
                                            {
                                                myTarget.Stacks[i].Item.socketedItems[u] = -1;
                                            }
                                            else
                                            {
                                                myTarget.Stacks[i].Item.socketedItems[u] = -2;
                                            }
                                        }
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();
                                }
                            }
                        }
                    }



                    //===Item List
                }
            }


            GUILayout.BeginHorizontal();
            GUI.backgroundColor = _hiddenExpand ? _activeColor : _disableColor;
            _titleButtonStyle.normal.textColor = _hiddenExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
            GUI.color = Manager.items.Count <= 0 ? Color.red : Color.white;
            GUILayout.Label(_hiddenExpand ? "[-]" : "[+]", GUILayout.Width(20));
            if (GUILayout.Button(new GUIContent(" Hidden Items", "Hidden items will not show in the normal inventory window, but shows in the interfaces using scripts that inherit from HiddenContainer.cs"), _titleButtonStyle))
            {
                _hiddenExpand = !_hiddenExpand;
                _search = "Search item by name";
                EditorGUI.FocusTextInControl(null);
            }
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;
            if (Manager.items.Count <= 0)
            {
                GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
            }
            GUILayout.EndHorizontal();
            if (_hiddenExpand)
            {
                if (Manager.items.Count <= 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                    GUI.color = Color.red;
                    GUILayout.Label("You must have at least one item setup in ItemManager prefab.");
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
                else
                {
                    string[] _itemOption = new string[Manager.items.Count + 1];
                    if (_itemExpand || _hiddenExpand)
                    {
                        _itemOption[0] = "Empty";
                        for (int i = 1; i < _itemOption.Length; i++) _itemOption[i] = Manager.items[i - 1].name;
                    }

                    GUI.backgroundColor = _buttonColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    if (GUILayout.Button("Add Item", GUILayout.Width(200)))
                    {
                        myTarget.HiddenStacks.Add(new InventoryStack());
                        _valueChanged = true;
                    }
                    GUI.backgroundColor = _titleColor;
                    if (GUILayout.Button("Expand All", GUILayout.Width(100)))
                    {
                        for (int i = 0; i < myTarget.HiddenStacks.Count; i++) myTarget.HiddenStacks[i].Fold = false;
                    }
                    if (GUILayout.Button("Shrink All", GUILayout.Width(100)))
                    {
                        for (int i = 0; i < myTarget.HiddenStacks.Count; i++) myTarget.HiddenStacks[i].Fold = true;
                    }
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = _backgroundColor;

                    //===Hidden List
                    for (int i = 0; i < myTarget.HiddenStacks.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        if (GUILayout.Button(myTarget.HiddenStacks[i].Fold ? "+" : "-", GUILayout.Width(20)))
                        {
                            myTarget.HiddenStacks[i].Fold = !myTarget.HiddenStacks[i].Fold;
                            _search = "Search item by name";
                        }

                        int _item = 0;
                        Color _color = Color.white;
                        if (myTarget.HiddenStacks[i].Item != null && !myTarget.HiddenStacks[i].Empty)
                        {
                            if (myTarget.HiddenStacks[i].Item.uid >= Manager.items.Count)
                            {
                                myTarget.HiddenStacks[i].Delete();
                            }
                            else
                            {
                                myTarget.HiddenStacks[i].Item.type = Mathf.Clamp(myTarget.HiddenStacks[i].Item.type, 0, Manager.itemTypes.Count - 1);
                                _item = myTarget.HiddenStacks[i].Item.uid + 1;
                                _color = Manager.itemTypes[myTarget.HiddenStacks[i].Item.type].color;
                                GUILayout.Box(GetIcon(myTarget.HiddenStacks[i].Item.uid), GUILayout.Width(20), GUILayout.Height(20));
                            }
                        }
                        else
                        {
                            GUILayout.Box("-", GUILayout.Width(20), GUILayout.Height(20));
                        }
                        EditorGUI.BeginChangeCheck();
                        GUI.backgroundColor = _color;
                        _item = EditorGUILayout.Popup("", _item, _itemOption, GUILayout.Width(180));

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (_item == 0)
                            {
                                myTarget.HiddenStacks[i].Empty = true;
                                myTarget.HiddenStacks[i].Item = null;
                                myTarget.HiddenStacks[i].Number = 0;
                            }
                            else
                            {
                                myTarget.HiddenStacks[i] = new InventoryStack(Manager.items[_item - 1], Mathf.Max(1, myTarget.HiddenStacks[i].Number));
                            }
                        }
                        GUILayout.Label(" x", GUILayout.Width(20));

                        int _maxStack = 0;
                        if (myTarget.HiddenStacks[i].Item != null && !myTarget.HiddenStacks[i].Empty)
                        {
                            _maxStack = myTarget.HiddenStacks[i].Item.maxiumStack;
                        }

                        GUI.backgroundColor = _titleColor;
                        myTarget.HiddenStacks[i].Number = Mathf.FloorToInt(GUILayout.HorizontalSlider(myTarget.HiddenStacks[i].Number, 0, _maxStack, GUILayout.Width(120)));
                        if (_item == 0) myTarget.HiddenStacks[i].Number = 0;
                        GUILayout.Label(myTarget.HiddenStacks[i].Number.ToString(), GUILayout.Width(30));
                        GUI.backgroundColor = _backgroundColor;
                        GUI.color = _actionColor;
                        if (myTarget.HiddenStacks[i].Item != null && myTarget.HiddenStacks[i].Item.upgradeLevel > 0)
                        {
                            GUILayout.Label("Lv." + myTarget.HiddenStacks[i].Item.upgradeLevel.ToString(), GUILayout.Width(40));
                        }
                        else
                        {
                            GUILayout.Space(40);
                        }
                        GUI.color = Color.white;
                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(30)))
                        {
                            myTarget.HiddenStacks.RemoveAt(i);
                            _valueChanged = true;
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();

                        if (!myTarget.HiddenStacks[i].Fold)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(80);
                            GUI.color = Color.gray;
                            _search = GUILayout.TextField(_search, GUILayout.Width(180));

                            GUILayout.EndHorizontal();
                            if (_search != "")
                            {
                                for (int u = 0; u < Manager.items.Count; u++)
                                {
                                    if (Manager.items[u].name.ToLower().Contains(_search.ToLower()))
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(100);
                                        if (GUILayout.Button(Manager.items[u].name, GUILayout.Width(160)))
                                        {
                                            myTarget.HiddenStacks[i] = new InventoryStack(Manager.items[u], Mathf.Max(1, myTarget.HiddenStacks[i].Number));
                                            myTarget.HiddenStacks[i].Fold = true;
                                            EditorGUI.FocusTextInControl(null);
                                        }
                                        GUILayout.EndHorizontal();
                                    }
                                }
                            }
                            GUI.color = Color.white;
                        }


                    }



                    //===Hidden List



                }


            }

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = _callbackExpand ? _activeColor : _disableColor;
            _titleButtonStyle.normal.textColor = _callbackExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
            GUI.color = myTarget.Currency.Count <= 0 ? Color.red : Color.white;
            GUILayout.Label(_callbackExpand ? "[-]" : "[+]", GUILayout.Width(20));
            if (GUILayout.Button(new GUIContent(" Code Example", "Code example for this inventory holder."), _titleButtonStyle))
            {
                _callbackExpand = !_callbackExpand;
                EditorGUI.FocusTextInControl(null);
            }
            GUI.color = Color.white;
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            if (_callbackExpand)
            {
                if (myTarget.Type == InventoryHolder.HolderType.NpcInventory || myTarget.Type == InventoryHolder.HolderType.PlayerInventory || myTarget.Type == InventoryHolder.HolderType.Crate || myTarget.Type == InventoryHolder.HolderType.Merchant)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.color = Color.yellow;
                    GUILayout.Label("Add an item:");
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();

                    GUI.color = _buttonColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("ThisInventoryHolder.AddItem(new Item(int _uid),int _number);");
                    GUILayout.EndHorizontal();

                    GUI.color = Color.white;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//_uid: The unique ID of the item to add, \"_number\" is the number of item you want to add.");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//_number: The quantity of item to add.");
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.color = Color.yellow;
                    GUILayout.Label("Remove an item:");
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();

                    GUI.color = _buttonColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("ThisInventoryHolder.RemoveItem(int _uid, int _number, int _index=-1);");
                    GUILayout.EndHorizontal();

                    GUI.color = Color.white;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//_uid: The unique ID of the item to remove.");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//_number: The quantity of the item to remove. If the number exceeds the quantity available, it will be capped at the actual number.");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//_index: (Optional) If specified, removes the item from the slot at this index in the inventory interface. Use -1 to ignore the index.");
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.color = Color.yellow;
                    GUILayout.Label("Use an item:");
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();

                    GUI.color = _buttonColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("ThisInventoryHolder.UseItem(int _uid, int _number, int _index = -1);");
                    GUILayout.EndHorizontal();

                    GUI.color = Color.white;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//_uid: The unique ID of the item to use.");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//_number: The quantity of the item to use.If the number exceeds the available quantity, it will be capped at the actual number.");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//_index: (Optional) If specified, use the item from the slot at this index in the inventory interface. Use -1 to ignore the index.");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("//After using an item, the ItemUseCallback will be triggered. Refer to the Callbacks section for more details.");
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                }
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.color = Color.yellow;
                if (myTarget.Type == InventoryHolder.HolderType.PlayerEquipment || myTarget.Type == InventoryHolder.HolderType.NpcEquipment)
                {
                    GUILayout.Label("Callback for equip or unequip an item:");
                }
                else if (myTarget.Type == InventoryHolder.HolderType.NpcInventory || myTarget.Type == InventoryHolder.HolderType.PlayerInventory || myTarget.Type == InventoryHolder.HolderType.Crate || myTarget.Type == InventoryHolder.HolderType.Merchant)
                {
                    GUILayout.Label("Callback for get or remove an item:");
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

                GUI.color = _buttonColor;
                if (myTarget.Type == InventoryHolder.HolderType.PlayerEquipment || myTarget.Type == InventoryHolder.HolderType.NpcEquipment)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("ThisInventoryHolder.RegisterItemChangeCallback(OnItemEquip);");
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("public void OnItemEquip(Dictionary<Item, int> _changedItems){");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("    foreach (var _item in _changedItems.Keys) {");
                    GUILayout.EndHorizontal();
                    GUI.color = Color.white;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        //when _changedItems[_item]>0 means equip _item");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        //when _changedItems[_item]<0 means unequip _item");
                    GUILayout.EndHorizontal();

#if MASTER_CHARACTER_CREATOR
                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        //when work with Master Character Creator:");
                    GUILayout.EndHorizontal();

                    GUI.color = _buttonColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        if (_changedItems[_item] > 0){");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("            Player.Equip(_item.equipAppearance); //Equip");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("         }else{");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("            Player.Unequip(_item.equipAppearance.Type); //Unequip");
                    GUILayout.EndHorizontal();

                     GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("         }");
                    GUILayout.EndHorizontal();
#endif
                    GUI.color = _buttonColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("    }");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("}");
                    GUILayout.EndHorizontal();


                }
                else if (myTarget.Type == InventoryHolder.HolderType.NpcInventory || myTarget.Type == InventoryHolder.HolderType.PlayerInventory || myTarget.Type == InventoryHolder.HolderType.Crate || myTarget.Type == InventoryHolder.HolderType.Merchant)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("ThisInventoryHolder.RegisterItemChangeCallback(OnItemChange);");
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("public void OnItemChange(Dictionary<Item, int> _changedItems){");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("    foreach (var _item in _changedItems.Keys) {");
                    GUILayout.EndHorizontal();

                    GUI.color = Color.white;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        //when _changedItems[_item]>0 means get new _item");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        //when _changedItems[_item]<0 means _item been removed");
                    GUILayout.EndHorizontal();

                    GUI.color = _buttonColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("    }");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("}");
                    GUILayout.EndHorizontal();
                }

                if (myTarget.Type == InventoryHolder.HolderType.NpcInventory || myTarget.Type == InventoryHolder.HolderType.PlayerInventory || myTarget.Type == InventoryHolder.HolderType.Crate || myTarget.Type == InventoryHolder.HolderType.Merchant)
                {
                    EditorGUILayout.Separator();
                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.color = Color.yellow;
                    GUILayout.Label("Callback for use an item:");
                    GUILayout.EndHorizontal();

                    GUI.color = _buttonColor;

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("ThisInventoryHolder.RegisterItemUseCallback(OnItemUse);");
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("public void OnItemUse(string _action, int _id, int _index){");
                    GUILayout.EndHorizontal();

                    GUI.color = Color.white;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        //_action is the action string you set in the ItemManager, _id is the item UID");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        //_id is the item UID");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("        //_index is the slot index of inventory");
                    GUILayout.EndHorizontal();

                    GUI.color = _buttonColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("}");
                    GUILayout.EndHorizontal();
                }

                GUI.color = Color.white;
            }

            if ((_valueChanged || GUI.changed) && !Application.isPlaying) myTarget.UpdatePrefab();
        }
    }
}
