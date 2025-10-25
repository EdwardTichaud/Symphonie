using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SoftKitty.InventoryEngine
{
    [CustomEditor(typeof(LootPack))]
    public class LootPack_inspector : Editor
    {
        bool _itemExpand = false;
        bool _currencyExpand = false;
        Color _activeColor = new Color(0.1F, 0.3F, 0.5F);
        Color _disableColor = new Color(0F, 0.1F, 0.3F);
        Color _actionColor = new Color(0F, 1F, 0.4F);
        Color _titleColor = new Color(0.3F, 0.5F, 1F);
        Color _buttonColor = new Color(0F, 0.8F, 0.3F);
        GUIStyle _titleButtonStyle;
        ItemManager Manager;
        ItemEditorAssets EditorAsset;
        private Texture2D GetIcon(int _id)
        {
            if (EditorAsset != null && Manager != null)
            {
                if (_id >= 0 && _id < Manager.items.Count)
                {
                    if (Manager.items[_id].iconLoadMethod == LoadMethod.DirectReference)
                    {
                        return Manager.items[_id].icon;
                    }
                    else if (EditorAsset.ItemAssets.Count > _id)
                    {
                        return EditorAsset.ItemAssets[_id].icon;
                    }
                    else if (Manager.items[_id].icon != null)
                    {
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
            LootPack myTarget = (LootPack)target;

            string _thePath = AssetDatabase.GetAssetPath(script);
            _thePath = _thePath.Replace("LootPack_inspector.cs", "");
            Texture logoIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Logo.png", typeof(Texture));
            Texture warningIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "warning.png", typeof(Texture));
            if (EditorAsset == null) EditorAsset = (ItemEditorAssets)AssetDatabase.LoadAssetAtPath(_thePath + "ItemEditorAssets.asset", typeof(ItemEditorAssets));
            GUILayout.BeginHorizontal();
            GUILayout.Box(logoIcon);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("You can set up different <LootPack> prefabs with their own drop settings. When a player interacts with it, call LootPack.OpenPack() to open the loot UI."
               , MessageType.Info, true);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUI.backgroundColor = new Color(1F, 0.8F, 0.1F, 1F);
            if (GUILayout.Button(new GUIContent("Help","Open [User Guide] pdf."), GUILayout.Width(120)))
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
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label("UID:",GUILayout.Width(100));
            myTarget.UID = EditorGUILayout.TextField(myTarget.UID);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = _currencyExpand ? _activeColor : _disableColor;
            _titleButtonStyle.normal.textColor = _currencyExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
            GUI.color = (myTarget.CurrencyMin.Count <= 0 || myTarget.CurrencyMax.Count <= 0) ? Color.red : Color.white;
            GUILayout.Label(_currencyExpand ? "[-]" : "[+]", GUILayout.Width(20));
            if (GUILayout.Button(new GUIContent(" Currency","The currencies this loot pack contains."), _titleButtonStyle))
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
                bool _noMatch = false;
                if (myTarget.CurrencyMin.Count < Manager.currencies.Count) {
                    myTarget.CurrencyMin.Add(0);
                    _noMatch = true;
                } 
                else if (myTarget.CurrencyMin.Count > Manager.currencies.Count) {
                    myTarget.CurrencyMin.RemoveAt(0);
                    _noMatch = true;
                }
                if (myTarget.CurrencyMax.Count < Manager.currencies.Count)
                {
                    myTarget.CurrencyMax.Add(0);
                    _noMatch = true;
                }
                else if (myTarget.CurrencyMax.Count > Manager.currencies.Count)
                {
                    myTarget.CurrencyMax.RemoveAt(0);
                    _noMatch = true;
                }
                if (_noMatch) return;
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
                    if (myTarget.CurrencyMin.Count <= i) myTarget.CurrencyMin.Add(0);
                    if (myTarget.CurrencyMax.Count <= i) myTarget.CurrencyMax.Add(0);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);


                    GUILayout.Label("(ID:" + i.ToString() + ")", GUILayout.Width(40), GUILayout.Height(20));
                    GUILayout.Box(Manager.currencies[i].icon.texture, GUILayout.Width(20), GUILayout.Height(20));
                    GUI.color = Manager.currencies[i].color;
                    GUILayout.Label(Manager.currencies[i].name + " :", GUILayout.Width(100), GUILayout.Height(20));
                    GUI.color = Color.white;

                    GUI.backgroundColor = Manager.currencies[i].color;
                    myTarget.CurrencyMin[i] = EditorGUILayout.IntField(myTarget.CurrencyMin[i], GUILayout.Width(100), GUILayout.Height(20));
                    GUILayout.Label("~", GUILayout.Width(15), GUILayout.Height(20));
                    myTarget.CurrencyMax[i] = EditorGUILayout.IntField(myTarget.CurrencyMax[i], GUILayout.Width(100), GUILayout.Height(20));
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
            if (GUILayout.Button(new GUIContent(" Drop Items Pool (" +myTarget.ItemPool.Count.ToString()+")","When this loot pack drops, it will random pick items from this pool."), _titleButtonStyle))
            {
                _itemExpand = !_itemExpand;
                EditorGUI.FocusTextInControl(null);
            }
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;
            if (Manager.items.Count <= 0)
            {
                GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
            }
            GUILayout.EndHorizontal();


            string[] _itemOption = new string[Manager.items.Count];
            if (_itemExpand)
            {
                for (int i = 0; i < _itemOption.Length; i++) _itemOption[i] = Manager.items[i].name;
            }

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
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button(new GUIContent("Add new [Item]","Add an new item to the pool."), GUILayout.Width(150)))
                    {
                        myTarget.ItemPool.Add(0);
                        _valueChanged = true;
                    }
                    GUILayout.EndHorizontal();
                    for (int i = 0; i < myTarget.ItemPool.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUILayout.Box(GetIcon(myTarget.ItemPool[i]), GUILayout.Width(20), GUILayout.Height(20));
                        int _item = myTarget.ItemPool[i];
                        EditorGUI.BeginChangeCheck();
                        GUI.backgroundColor = Manager.itemTypes[Manager.items[myTarget.ItemPool[i]].type].color;
                        _item = EditorGUILayout.Popup("", _item, _itemOption, GUILayout.Width(180));

                        if (EditorGUI.EndChangeCheck()) myTarget.ItemPool[i] = _item;

                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(30)))
                        {
                            myTarget.ItemPool.RemoveAt(i);
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();
                    }
                }

            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label(new GUIContent("Maxmium Item Count:","When random pick items from the pool, this number will be the maxmium item count it could be"), GUILayout.Width(180));
            myTarget.MaxiumItemCount = EditorGUILayout.IntSlider(myTarget.MaxiumItemCount, Mathf.Min(myTarget.ItemPool.Count, 1), myTarget.ItemPool.Count);
            GUILayout.EndHorizontal();

            if (myTarget.ItemPool.Count <= 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                GUI.color = Color.yellow;
                GUILayout.Label("[Maxmium Item Count] can not be larger than the [Drop Items Pool] count");
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label(new GUIContent("Maxmium Count Each Item:", "The count of each item stack will be random from 1 to this number."), GUILayout.Width(180));
            myTarget.MaxiumCountEachItem = EditorGUILayout.IntSlider(myTarget.MaxiumCountEachItem, 1,99);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label(new GUIContent("Drop Chance Multiplier:","The drop chance of each item will be the drop chance in the [ItemManager] setting multiply this number."), GUILayout.Width(180));
            myTarget.DropChanceMultiplier = EditorGUILayout.Slider(myTarget.DropChanceMultiplier, 0.5F, 5F);
            GUILayout.Label("x", GUILayout.Width(15));
            GUILayout.EndHorizontal();

            if (Manager.EnableEnhancing)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.Label(new GUIContent("Random Enhancing Level:", "Will the items have random Enhancing Level?"), GUILayout.Width(180));
                myTarget.RandomLevel = EditorGUILayout.Toggle(myTarget.RandomLevel);
                GUILayout.EndHorizontal();

                if (myTarget.RandomLevel) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Maximum Enhancing Level:", "Maximum Enhancing Level the random result can be."), GUILayout.Width(180));
                    myTarget.MaxiumEnhancingLevel = EditorGUILayout.IntSlider(myTarget.MaxiumEnhancingLevel,1, Manager.MaxiumEnhancingLevel);
                    GUILayout.EndHorizontal();
                }
            }

            if (Manager.EnableEnchanting) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.Label(new GUIContent("Random Enchantments: ("+(myTarget.EnchantmentPool.Count<=0?"All": myTarget.EnchantmentPool.Count.ToString()) +")", 
                    "Will the items have random Enchantments?"), GUILayout.Width(180));
                myTarget.RandomEnchantment = EditorGUILayout.Toggle(myTarget.RandomEnchantment,GUILayout.Width(50));
                if (myTarget.RandomEnchantment && Manager.itemEnchantments.Count>0)
                {
                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button("Add",GUILayout.Width(50))) {
                        myTarget.EnchantmentPool.Add(0);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    GUI.backgroundColor = _titleColor;
                    if (GUILayout.Button("Add All", GUILayout.Width(80)))
                    {
                        myTarget.EnchantmentPool.Clear();
                        for (int i = 0; i < Manager.itemEnchantments.Count; i++) myTarget.EnchantmentPool.Add(i);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Clear", GUILayout.Width(50)))
                    {
                        myTarget.EnchantmentPool.Clear();
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();

                if (myTarget.RandomEnchantment) {
                    string[] _enchantmentOptions = new string[Manager.itemEnchantments.Count];
                    for (int i = 0; i < _enchantmentOptions.Length; i++) _enchantmentOptions[i] = Manager.itemEnchantments[i].name;
                    for (int i=0;i< myTarget.EnchantmentPool.Count;i++) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(50);
                        GUI.backgroundColor = _titleColor;
                        myTarget.EnchantmentPool[i] = EditorGUILayout.Popup(myTarget.EnchantmentPool[i], _enchantmentOptions,GUILayout.Width(120));
                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X",GUILayout.Width(18))) {
                            myTarget.EnchantmentPool.RemoveAt(i);
                            _valueChanged = true;
                            EditorGUI.FocusTextInControl(null);
                        }
                        GUI.backgroundColor = Color.white;
                        GUILayout.EndHorizontal();
                    }
                }
            }


            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label(new GUIContent("Destroy when player close loot window:","Will the loot pack be destroyed when player close the loot window?"), GUILayout.Width(250));
            myTarget.DestoryWhenPlayerCloseLootWindow = EditorGUILayout.Toggle(myTarget.DestoryWhenPlayerCloseLootWindow);
            GUILayout.EndHorizontal();

            if ((_valueChanged || GUI.changed) && !Application.isPlaying) myTarget.UpdatePrefab();

        }
    }
}
