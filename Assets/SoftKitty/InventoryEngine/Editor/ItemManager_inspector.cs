using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;


namespace SoftKitty.InventoryEngine
{
    [CustomEditor(typeof(ItemManager))]
    public class ItemManager_inspector : Editor
    {
        bool _generalExpand = false;
        bool _typeExpand = false;
        bool _qualityExpand = false;
        bool _currencyExpand = false;
        bool _attExpand = false;
        bool _entExpand = false;
        bool _craftExpand = false;
        bool _upgradeExpand = false;
        bool _itemExpand = false;
        bool _socketingExpand = false;
        int _newAttSel = 0;
        Color _activeColor = new Color(0.1F, 0.3F, 0.5F);
        Color _disableColor = new Color(0F, 0.1F, 0.3F);
        Color _activeColor2 = new Color(0.3F, 0.25F, 0.5F);
        Color _disableColor2 = new Color(0.1F, 0F, 0.3F);
        Color _buttonColor = new Color(0F, 0.6F, 1F);
        Color _attColor = new Color(1F,0.5F,0.3F);
        Color _actionColor = new Color(1F, 0.8F, 0.4F);
        Color _matColor = new Color(0.4F, 0.7F, 1F);
        Color _tagColor = new Color(0.4F, 1F, 0.8F);
        Color _titleColor = new Color(0.3F,0.5F,1F);
        GUIStyle _titleButtonStyle;
        GUIStyle _toolButtonStyle;
        List<string> ClipboardStringList = new List<string>();
        List<Vector2> ClipboardVector2List = new List<Vector2>();
        List<Attribute> ClipboardAttributeList = new List<Attribute>();
        Item ClipboardItem = null;
        EditorAssetData ClipboardAsset =null;
        ItemEditorAssets EditorAsset;
        Texture logoIcon ;
        Texture warningIcon ;
        Texture copyIcon;
        Texture pasteIcon;
        Texture resetIcon;
        Texture visibleIcon0;
        Texture visibleIcon1 ;
        Texture upIcon ;
        Texture downIcon;
#if MASTER_CHARACTER_CREATOR
        MasterCharacterCreator.CharacterDataSetting MccData;
#endif

        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            bool _valueChanged = false;
            _titleButtonStyle = new GUIStyle(GUI.skin.button);
            _titleButtonStyle.alignment = TextAnchor.MiddleLeft;
            _toolButtonStyle = new GUIStyle(GUI.skin.button);
            _toolButtonStyle.border = new RectOffset() {  bottom=0,left=0,top=0,right=0};
            _toolButtonStyle.padding = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
           

            Color _backgroundColor = GUI.backgroundColor;
            var script = MonoScript.FromScriptableObject(this);
            ItemManager myTarget = (ItemManager)target;
            if (Application.isPlaying)
            {
                GUILayout.Box("Game is running.");
                return;
            }
            else
            {
                string _thePath = AssetDatabase.GetAssetPath(script);
                _thePath = _thePath.Replace("ItemManager_inspector.cs", "");
                if (logoIcon == null) logoIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Logo.png", typeof(Texture));
                if (warningIcon == null) warningIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "warning.png", typeof(Texture));
                if (copyIcon == null) copyIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "copy.png", typeof(Texture));
                if (pasteIcon == null) pasteIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "paste.png", typeof(Texture));
                if (resetIcon == null) resetIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "reset.png", typeof(Texture));
                if (visibleIcon0 == null) visibleIcon0 = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "visible0.png", typeof(Texture));
                if (visibleIcon1 == null) visibleIcon1 = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "visible1.png", typeof(Texture));
                if (upIcon == null) upIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Up.png", typeof(Texture));
                if (downIcon == null) downIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Down.png", typeof(Texture));
                if(EditorAsset==null) EditorAsset = (ItemEditorAssets)AssetDatabase.LoadAssetAtPath(_thePath + "ItemEditorAssets.asset", typeof(ItemEditorAssets));
                GUILayout.BeginHorizontal();
                GUILayout.Box(logoIcon);
                GUILayout.EndHorizontal();
                if (myTarget.itemTypes.Count <= 0)
                {
                    StringColorData _newType = new StringColorData();
                    _newType.name = "Default Category" + myTarget.itemTypes.Count.ToString();
                    _newType.color = Color.white;
                    _newType.visible = true;
                    myTarget.itemTypes.Add(_newType);
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }

                if (myTarget.itemQuality.Count <= 0)
                {
                    StringColorData _newType = new StringColorData();
                    _newType.name = "Default Quality" + myTarget.itemQuality.Count.ToString();
                    _newType.color = Color.white;
                    _newType.visible = true;
                    myTarget.itemQuality.Add(_newType);
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }

                if (myTarget.currencies.Count <= 0)
                {
                    Currency _newCurrency = new Currency();
                    _newCurrency.name = "Default Currency";
                    _newCurrency.color = Color.white;
                    _newCurrency.icon = null;
                    _newCurrency.ExchangeRate = new List<Vector3>();
                    _newCurrency.fold = true;
                    myTarget.currencies.Add(_newCurrency);
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }

                if (myTarget.itemAttributes.Count <= 0)
                {
                    Attribute _newAtt = new Attribute();
                    _newAtt.name = "Default Attribute" + myTarget.itemAttributes.Count.ToString();
                    _newAtt.key = "Att" + myTarget.itemAttributes.Count.ToString();
                    _newAtt.upgradeIncrement = 0F;
                    _newAtt.visible = true;
                    _newAtt.stringValue = false;
                    _newAtt.fold = true;
                    _newAtt.value = "";
                    myTarget.itemAttributes.Add(_newAtt);
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }

                if (myTarget.items.Count <= 0)
                {
                    Item _newItem = new Item();
                    _newItem.name = "Default Item";
                    _newItem.description = "No description yet.";
                    _newItem.type = 0;
                    _newItem.quality = 0;
                    _newItem.icon = null;
                    _newItem.maxiumStack = 99;
                    _newItem.price = 0;
                    _newItem.upgradeLevel = 0;
                    _newItem.useable = false;
                    _newItem.consumable = false;
                    _newItem.tradeable = true;
                    _newItem.deletable = true;
                    _newItem.attributes.Clear();
                    _newItem.enchantments.Clear();
                    _newItem.actions.Clear();
                    _newItem.craftMaterials.Clear();
                    _newItem.tags.Clear();
                    _newItem.uid = myTarget.items.Count;
                    _newItem.fold = true;
                    myTarget.items.Add(_newItem);
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }

                string[] _attOptions = new string[myTarget.itemAttributes.Count];
                for (int i = 0; i < myTarget.itemAttributes.Count; i++) _attOptions[i] = myTarget.itemAttributes[i].key;

                string[] _typeOptions = new string[myTarget.itemTypes.Count];
                for (int i = 0; i < myTarget.itemTypes.Count; i++) _typeOptions[i] = myTarget.itemTypes[i].name;

                string[] _qualityOptions = new string[myTarget.itemQuality.Count];
                for (int i = 0; i < myTarget.itemQuality.Count; i++) _qualityOptions[i] = myTarget.itemQuality[i].name;

                string[] _itemOption = new string[myTarget.items.Count];
                for (int i = 0; i < myTarget.items.Count; i++) _itemOption[i] = myTarget.items[i].name;

                string[] _currencyOption = new string[myTarget.currencies.Count];
                for (int i = 0; i < myTarget.currencies.Count; i++) _currencyOption[i] = myTarget.currencies[i].name;

                string[] _attFormatOption = new string[2];
                _attFormatOption[0] = "+";
                _attFormatOption[1] = ":";

                if (!myTarget.GetComponent<AssetLoader>())
                {
                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = Color.red;
                    EditorGUILayout.HelpBox("<AssetLoader> component is missing on this prefab, <AssetLoader> component is required " +
                        "in order to load icon and custom data object from Resources folder, AssetBundle or Addressable. Please click below" +
                        " button to add one.", MessageType.Warning, true);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Add <AssetLoader> Component"))
                    {
                        myTarget.gameObject.AddComponent<AssetLoader>();
                    }
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = Color.white;
                }


                GUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("You must have this prefab in your scene as a database to access it. " +
                    "This prefab will not be destroyed when switching scenes, and its script will prevent duplication.", MessageType.Info,true);
                GUILayout.EndHorizontal();

                

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = _buttonColor;
                if (GUILayout.Button(new GUIContent("Apply Changes", "Click to apply your changes"), GUILayout.Width(120)))
                {
                    myTarget.UpdatePrefab();
                    UnityEditor.EditorUtility.SetDirty(EditorAsset);
                }

                GUILayout.Space(20);
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button(new GUIContent("Reset", "Reset the database to blank."), GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("Reset the database", "Are you sure you want to RESET the whole database? All settins will be reset to default.", "Confirm", "Cancel"))
                    {
                        if (Directory.Exists(Application.dataPath + "/../" + myTarget.SavePathRoot))
                        {
                            Directory.Delete(Application.dataPath + "/../" + myTarget.SavePathRoot, true);
                        }
                       
                        myTarget.items.Clear();
                        myTarget.itemEnchantments.Clear();
                        myTarget.SocketedCategoryFilter = 0;
                        myTarget.EnchantingMaterial = new Vector2(0,1);
                        myTarget.EnhancingMaterials = new Vector2[2] { new Vector2(0, 1) , new Vector2(0, 1) };
                        myTarget.EnhancingCategoryID = 0;
                        myTarget.EnhancingCurrencyType = 0;
                        myTarget.EnchantingCategoryID = 0;
                        myTarget.EnchantingCurrencyType = 0;
                        myTarget.CraftingMaterialCategoryID = 0;
                        myTarget.EnableCrafting = false;
                        myTarget.EnableEnchanting = false;
                        myTarget.EnableEnhancing = false;
                        myTarget.EnableSocketing = false;
                        myTarget.itemAttributes.Clear();
                        myTarget.currencies.Clear();
                        myTarget.itemQuality.Clear();
                        myTarget.itemTypes.Clear();
                        myTarget.clickSettings.Clear();
                        EditorGUI.FocusTextInControl(null);
                        EditorAsset.ItemAssets.Clear();
                        UnityEditor.EditorUtility.SetDirty(EditorAsset);
                        myTarget.UpdatePrefab();
                        return;
                    }
                }
                GUI.backgroundColor = new Color(0.1F, 1F, 0.4F, 1F);
                if (GUILayout.Button(new GUIContent("Help", "Click to open the UserGuide.pdf"), GUILayout.Width(60)))
                {
                    string _path = Application.dataPath.Substring(0, Application.dataPath.Length - 6) + _thePath.Replace("Editor", "Documentation") + "UserGuide.pdf";
                    int pageNumber = 3;
                    string fileUrl = "file:///" + _path.Replace('\\', '/');
                    string urlWithPage = $"{fileUrl}#page={pageNumber}";
                    Application.OpenURL(urlWithPage);
                }
                GUI.backgroundColor = _backgroundColor;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button(new GUIContent("Clear Saved Data (Editor)", "Clear the data saved in the editor."), GUILayout.Width(280)))
                {
                    if (Directory.Exists(Application.dataPath + "/../" + myTarget.SavePathRoot))
                    {
                        Directory.Delete(Application.dataPath + "/../" + myTarget.SavePathRoot, true);
                        EditorUtility.DisplayDialog("Master Inventory Engine", "All saved data (Editor) has been deleted!", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Master Inventory Engine", "No saved data (Editor) found.", "OK");
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.Label("Make sure to 'Clear Saved Data' if you have made any changes.");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _generalExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _generalExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUI.color = Color.white;
                GUILayout.Label(_generalExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" General Settings",
                    "General Settings of the inventory system."
                    ), _titleButtonStyle))
                {
                    _generalExpand = !_generalExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.color = Color.white;
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                if (_generalExpand) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Canvas GameObject Tag:", "[Optional] Specify the tag of your main Canvas GameObject. This ensures the system finds the correct canvas, especially in scenes with multiple canvases."),GUILayout.Width(160));
                    GUILayout.Space(10);
                    myTarget.CanvasTag = GUILayout.TextField(myTarget.CanvasTag,GUILayout.Width(170));
                    if (GUILayout.Button("X",GUILayout.Width(30))) {
                        myTarget.CanvasTag = "";
                        EditorGUI.FocusTextInControl(null);
                        _valueChanged = true;

                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("[Optional] Specify the tag of your main Canvas GameObject. This ensures the system finds the correct canvas, especially in scenes with multiple canvases.", MessageType.Info, true);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("SaveData location:", "Specify the base location of the save data"), GUILayout.Width(160));
                    GUILayout.Space(10);
                    myTarget.SavePathLocation = (ItemManager.SavePathLocationType)EditorGUILayout.EnumPopup(myTarget.SavePathLocation, GUILayout.Width(200F));
                    GUILayout.EndHorizontal();

                    if (myTarget.SavePathLocation == ItemManager.SavePathLocationType.Custom)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        EditorGUILayout.HelpBox("With [Custom] Location, please set the absolute full path of the save root folder in the below field.\n" +
                            "You can also use ItemManager.instance.SavePathRoot=xxxx to set this path during runtime.", MessageType.Info,true);
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("SaveData root folder name:", "Specify the name of the save data root folder, the folder will be located in the [SaveData location]."), GUILayout.Width(160));
                    GUILayout.Space(10);
                    myTarget.SavePathRoot = GUILayout.TextField(myTarget.SavePathRoot, GUILayout.Width(200F));
                    GUILayout.EndHorizontal();
                    GUI.backgroundColor = Color.black;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.color = new Color(0F,0.7F,1F,1F);
                    GUILayout.Box("[Editor] ",_titleButtonStyle, GUILayout.Width(50));
                    GUI.color = Color.white;
                    GUILayout.Box("Project Root Folder/" + myTarget.SavePathRoot + "/", _titleButtonStyle, GUILayout.Width(320));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.color = new Color(0F, 1F, 0.5F, 1F);
                    GUILayout.Box("[Build] ", _titleButtonStyle, GUILayout.Width(50));
                    GUI.color = Color.white;
                    if (myTarget.SavePathLocation == ItemManager.SavePathLocationType.GameInstallFolder)
                    {
                        GUILayout.Box("Game Install Folder/" + myTarget.SavePathRoot + "/", _titleButtonStyle, GUILayout.Width(320));
                    }
                    else if (myTarget.SavePathLocation == ItemManager.SavePathLocationType.PersistentDataPath)
                    {
                        GUILayout.Box(" Application.persistentDataPath/" + myTarget.SavePathRoot + "/", _titleButtonStyle, GUILayout.Width(320));
                    }
                    else
                    {
                        GUILayout.Box(myTarget.SavePathRoot + "/", _titleButtonStyle, GUILayout.Width(320));
                    }
                    GUILayout.EndHorizontal();

                    GUI.backgroundColor = Color.white;
                    GUI.color = Color.white;
                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    myTarget.AllowDropItem = GUILayout.Toggle(myTarget.AllowDropItem, "Allow player drop item by dragging out of the window.");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label("Merchant interface style:",GUILayout.Width(160));
                    GUILayout.Space(10);
                    string[] _styleNames = new string[2] { "Simple Style","DOS Style"};
                    int _style = myTarget.MerchantStyle;
                    EditorGUI.BeginChangeCheck();
                    GUI.backgroundColor = _buttonColor;
                    _style =EditorGUILayout.Popup(_style, _styleNames, GUILayout.Width(150));
                    if (EditorGUI.EndChangeCheck()) {
                        myTarget.MerchantStyle = _style;
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    myTarget.HighlightEquipmentSlotWhenHoverItem = GUILayout.Toggle(myTarget.HighlightEquipmentSlotWhenHoverItem, "Highlight associated slot when hover over equipment item.");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    myTarget.AllowDropItem = GUILayout.Toggle(myTarget.AllowDropItem, "Allow player drop item by dragging out of the window.");
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();


                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    int _keySel = 0;
                    for (int i = 0; i < myTarget.itemAttributes.Count; i++)
                    {
                        if (myTarget.itemAttributes[i].key == myTarget.NameAttributeKey) _keySel = i;
                    }
                    GUILayout.Label(new GUIContent("Character Name Attribute:", "Select the attribute you've created to represent the character's name. Ensure it's a string value, with no visibility options enabled, and added to the Player/NPC's Base Stats in the InventoryHolder."), GUILayout.Width(200));
                    GUI.backgroundColor = _attColor;
                    EditorGUI.BeginChangeCheck();
                    _keySel = EditorGUILayout.Popup(_keySel, _attOptions, GUILayout.Width(100F));
                    if (EditorGUI.EndChangeCheck()) myTarget.NameAttributeKey = myTarget.itemAttributes[_keySel].key;
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("Select the attribute you've created to represent the character's name. Ensure it's a string value, with no visibility options enabled, and added to the Player/NPC's Base Stats in the InventoryHolder.", MessageType.Info,true);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    _keySel = 0;
                    for (int i = 0; i < myTarget.itemAttributes.Count; i++)
                    {
                        if (myTarget.itemAttributes[i].key == myTarget.LevelAttributeKey) _keySel = i;
                    }
                    GUILayout.Label(new GUIContent("Character Level Attribute:", "Select the attribute you've created to represent the character's level. Ensure no visibility options enabled, and added to the Player/NPC's Base Stats in the InventoryHolder."), GUILayout.Width(200));
                    GUI.backgroundColor = _attColor;
                    EditorGUI.BeginChangeCheck();
                    _keySel = EditorGUILayout.Popup(_keySel, _attOptions, GUILayout.Width(100F));
                    if (EditorGUI.EndChangeCheck()) myTarget.LevelAttributeKey = myTarget.itemAttributes[_keySel].key;
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("Select the attribute you've created to represent the character's level. Ensure no visibility options enabled, and added to the Player/NPC's Base Stats in the InventoryHolder.", MessageType.Info, true);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    _keySel = 0;
                    for (int i = 0; i < myTarget.itemAttributes.Count; i++)
                    {
                        if (myTarget.itemAttributes[i].key == myTarget.XpAttributeKey) _keySel = i;
                    }
                    GUILayout.Label(new GUIContent("Character XP Attribute:", "Select the attribute you've created to represent the character's XP points. Ensure no visibility options enabled, and added to the Player/NPC's Base Stats in the InventoryHolder."), GUILayout.Width(200));
                    GUI.backgroundColor = _attColor;
                    EditorGUI.BeginChangeCheck();
                    _keySel = EditorGUILayout.Popup(_keySel, _attOptions, GUILayout.Width(100F));
                    if (EditorGUI.EndChangeCheck()) myTarget.XpAttributeKey = myTarget.itemAttributes[_keySel].key;
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("Select the attribute you've created to represent the character's XP points. Ensure no visibility options enabled, and added to the Player/NPC's Base Stats in the InventoryHolder.", MessageType.Info, true);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    _keySel = 0;
                    for (int i = 0; i < myTarget.itemAttributes.Count; i++)
                    {
                        if (myTarget.itemAttributes[i].key == myTarget.MaxXpAttributeKey) _keySel = i;
                    }
                    GUILayout.Label(new GUIContent("Character Max XP Attribute:", "Select the attribute you've created to represent the character's Max XP points. Ensure no visibility options enabled, and added to the Player/NPC's Base Stats in the InventoryHolder."), GUILayout.Width(200));
                    GUI.backgroundColor = _attColor;
                    EditorGUI.BeginChangeCheck();
                    _keySel = EditorGUILayout.Popup(_keySel, _attOptions, GUILayout.Width(100F));
                    if (EditorGUI.EndChangeCheck()) myTarget.MaxXpAttributeKey = myTarget.itemAttributes[_keySel].key;
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("Select the attribute you've created to represent the character's Max XP points. Ensure no visibility options enabled, and added to the Player/NPC's Base Stats in the InventoryHolder.", MessageType.Info, true);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    _keySel = 0;
                    for (int i = 0; i < myTarget.itemAttributes.Count; i++)
                    {
                        if (myTarget.itemAttributes[i].key == myTarget.CoolDownAttributeKey) _keySel = i;
                    }
                    GUILayout.Label(new GUIContent("Item Cool Down Attribute:", "Select the numerical attribute you've created to represent cool down time. Ensure it's added to the items with cool down functionality."),GUILayout.Width(200));
                    GUI.backgroundColor = _attColor;
                    EditorGUI.BeginChangeCheck();
                    _keySel=EditorGUILayout.Popup(_keySel,_attOptions,GUILayout.Width(100F));
                    if (EditorGUI.EndChangeCheck()) myTarget.CoolDownAttributeKey = myTarget.itemAttributes[_keySel].key;
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("Select the numerical attribute you've created to represent cool down time. Ensure it's added to the items with cool down functionality.", MessageType.Info, true);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();


                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Shared Global Cool Down Time:", "Sets a global cool down. When any item/skill is used, all usable items/skills enter this shared cool down period."), GUILayout.Width(200));
                    myTarget.SharedGlobalCoolDown = Mathf.RoundToInt( GUILayout.HorizontalSlider(myTarget.SharedGlobalCoolDown, 0F, 2F, GUILayout.Width(100F))*10F)*0.1F;
                    GUILayout.Label(myTarget.SharedGlobalCoolDown.ToString("0.0")+" sec", GUILayout.Width(100));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("Sets a global cool down. When any item/skill is used, all usable items/skills enter this shared cool down period.", MessageType.Info, true);
                    GUILayout.EndHorizontal();


                    EditorGUILayout.Separator();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label("Input Binding For Inventory Items:", GUILayout.Width(200));
                    if (myTarget.clickSettings.Count < 4)
                    {
                        GUI.backgroundColor = _buttonColor;
                        if (GUILayout.Button("Add", GUILayout.Width(50)))
                        {
                            ClickSetting _newSetting = new ClickSetting();
                            _newSetting.key = AlterKeys.None;
                            _newSetting.mouseButton = MouseButtons.LeftClick;
                            _newSetting.function = ClickFunctions.Use;
                            myTarget.clickSettings.Add(_newSetting);
                            EditorGUI.FocusTextInControl(null);
                            _valueChanged = true;
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    GUILayout.EndHorizontal();

                    for (int i=0;i<myTarget.clickSettings.Count;i++) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(60);
                        GUI.backgroundColor = _buttonColor;
                        AlterKeys _key = myTarget.clickSettings[i].key;
                        EditorGUI.BeginChangeCheck();
                        _key = (AlterKeys)EditorGUILayout.EnumPopup(_key, GUILayout.Width(100));
                        if (EditorGUI.EndChangeCheck()) myTarget.clickSettings[i].key = _key;

                        GUILayout.Label("+", GUILayout.Width(15));

                        MouseButtons _mouse = myTarget.clickSettings[i].mouseButton;
                        EditorGUI.BeginChangeCheck();
                        _mouse = (MouseButtons)EditorGUILayout.EnumPopup(_mouse, GUILayout.Width(100));
                        if (EditorGUI.EndChangeCheck()) myTarget.clickSettings[i].mouseButton = _mouse;

                        GUILayout.Label("=", GUILayout.Width(15));

                        ClickFunctions _function = myTarget.clickSettings[i].function;
                        EditorGUI.BeginChangeCheck();
                        _function = (ClickFunctions)EditorGUILayout.EnumPopup(_function, GUILayout.Width(100));
                        if (EditorGUI.EndChangeCheck()) myTarget.clickSettings[i].function = _function;
                        GUILayout.Space(10);
                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            myTarget.clickSettings.RemoveAt(i);
                            EditorGUI.FocusTextInControl(null);
                            _valueChanged = true;
                        }
                        GUI.backgroundColor = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    EditorGUILayout.Separator();
                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label("Dynamic Messages:", GUILayout.Width(400));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.backgroundColor = _buttonColor;
                    GUILayout.Box("Adding item when bag is full:", _titleButtonStyle,GUILayout.Width(370));
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.backgroundColor = Color.black;
                    myTarget.msgBagFull= GUILayout.TextField(myTarget.msgBagFull);
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.backgroundColor = _buttonColor;
                    GUILayout.Box("Can not use item because of the restriction setting:", _titleButtonStyle, GUILayout.Width(370));
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("{name} = restriction attribute name; {value} = restriction attribute value required;");
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.backgroundColor = Color.black;
                    myTarget.msgItemUseRestricted = GUILayout.TextField(myTarget.msgItemUseRestricted);
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.backgroundColor = _buttonColor;
                    GUILayout.Box("Can not assign item to a slot:", _titleButtonStyle, GUILayout.Width(370));
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.backgroundColor = Color.black;
                    myTarget.msgItemAssign = GUILayout.TextField(myTarget.msgItemAssign);
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.backgroundColor = _buttonColor;
                    GUILayout.Box("Failed when enhancing an item:", _titleButtonStyle, GUILayout.Width(370));
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Label("{name} = the item name;");
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUI.backgroundColor = Color.black;
                    myTarget.msgEnhancingFail = GUILayout.TextField(myTarget.msgEnhancingFail);
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();
                }


                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _typeExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _typeExpand ? Color.white: new Color(0.65F, 0.65F, 0.65F);
                GUI.color = Color.white;
                GUILayout.Label(_typeExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Item Category Settings (" + myTarget.itemTypes.Count.ToString()+")",
                    "You can create as many categories for your items as you like (though it¡¯s recommended to keep it to fewer than 5 visible categories due to UI space limitations)."
                    ), _titleButtonStyle))
                {
                    _typeExpand = !_typeExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
                if (_typeExpand) {

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("You can create as many categories for your items as you like (though it¡¯s recommended to keep it to fewer than 5 visible categories due to UI space limitations).", MessageType.Info, true);
                    GUILayout.Space(30);
                    GUILayout.EndHorizontal();

                    //===Type List
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = _buttonColor;

                    if (GUILayout.Button(new GUIContent("Add New [Category]","Create new category for items."), GUILayout.Width(200)))
                    {
                        StringColorData _newType = new StringColorData();
                        _newType.name = "New Category" + myTarget.itemTypes.Count.ToString();
                        _newType.color = Color.white;
                        _newType.visible = true;
                        myTarget.itemTypes.Add(_newType);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                    for (int i=0;i<myTarget.itemTypes.Count;i++) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _titleColor;
                        GUILayout.Label(new GUIContent("(ID: " +i.ToString()+")","When accessing this catergory in your script, you will need to use this ID."), GUILayout.Width(40));
                        GUI.color = Color.white;
                        GUILayout.Label(new GUIContent("Category Name:", " This name will be displayed as tab cards at the top of the inventory UI."), GUILayout.Width(100));
                        myTarget.itemTypes[i].name=GUILayout.TextField(myTarget.itemTypes[i].name, GUILayout.Width(150));
                        myTarget.itemTypes[i].color = EditorGUILayout.ColorField(myTarget.itemTypes[i].color, GUILayout.Width(50));
                        myTarget.itemTypes[i].visible = GUILayout.Toggle(myTarget.itemTypes[i].visible, new GUIContent("visible","Determines if this category shows in inventory interface."), GUILayout.Width(70));
                        if (i > 0)
                        {
                            GUI.backgroundColor = Color.red;
                            if (GUILayout.Button(new GUIContent("X", "Delete this catergory."), GUILayout.Width(30)))
                            {
                                for (int w = 0; w < myTarget.items.Count; w++)
                                {
                                    if (myTarget.items[w].type>i)
                                    {
                                        myTarget.items[w].type -= 1;
                                    }
                                }
                                if (myTarget.SocketedCategoryFilter > i) myTarget.SocketedCategoryFilter -= 1;
                                if (myTarget.SocketingCategoryFilter > i) myTarget.SocketingCategoryFilter -= 1;
                                if (myTarget.EnhancingCategoryID > i) myTarget.EnhancingCategoryID -= 1;
                                if (myTarget.EnchantingCategoryID > i) myTarget.EnchantingCategoryID -= 1;
                                if (myTarget.CraftingMaterialCategoryID > i) myTarget.CraftingMaterialCategoryID -= 1;
                                myTarget.itemTypes.RemoveAt(i);
                                _valueChanged = true;
                                EditorGUI.FocusTextInControl(null);
                                myTarget.UpdatePrefab();
                                return;
                            }
                            
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();
                    }
                    //===Type List
                    EditorGUILayout.Separator();
                }


                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _qualityExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _qualityExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUI.color = Color.white;
                GUILayout.Label(_qualityExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Item Quality Settings (" + myTarget.itemQuality.Count.ToString() + ")",
                    "You can create as many quality levels as needed. Each quality will have an index displayed as blue text at the front. When your script accesses the quality attribute of an item, the value will correspond to the index of the quality levels you set here."
                    ), _titleButtonStyle))
                {
                    _qualityExpand = !_qualityExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
                if (_qualityExpand)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("You can create as many quality levels as needed. Each quality will have an index displayed as blue text at the front. When your script accesses the quality attribute of an item, the value will correspond to the index of the quality levels you set here.", MessageType.Info, true);
                    GUILayout.Space(30);
                    GUILayout.EndHorizontal();

                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    myTarget.UseQualityColorForItemName = GUILayout.Toggle(myTarget.UseQualityColorForItemName, new GUIContent("Use [Quality] Color for item name text.","When enabled, the item name text will use the same color as the quality level of this item."));
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    //===Quality List
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button(new GUIContent("Add New [Quality]","Create new quality level for items."), GUILayout.Width(200)))
                    {
                        StringColorData _newType = new StringColorData();
                        _newType.name = "New Quality" + myTarget.itemQuality.Count.ToString();
                        _newType.color = Color.white;
                        _newType.visible = true;
                        myTarget.itemQuality.Add(_newType);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                    for (int i = 0; i < myTarget.itemQuality.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _titleColor;
                        GUILayout.Label(new GUIContent("(ID: " + i.ToString() + ")","When accessing this quality level from your script, you will need to use this ID."), GUILayout.Width(40));
                        GUI.color = Color.white;
                        GUILayout.Label(new GUIContent("Quality Name:", "This name will be shown in the detailed information panel of items."), GUILayout.Width(100));
                        myTarget.itemQuality[i].name=GUILayout.TextField(myTarget.itemQuality[i].name, GUILayout.Width(150));
                        myTarget.itemQuality[i].color = EditorGUILayout.ColorField(myTarget.itemQuality[i].color, GUILayout.Width(50));
                        if (i > 0)
                        {
                            GUI.backgroundColor = Color.red;
                            if (GUILayout.Button(new GUIContent("X", "Delete this quality level."), GUILayout.Width(30)))
                            {
                                for (int w = 0; w < myTarget.items.Count; w++)
                                {
                                    if (myTarget.items[w].quality > i)
                                    {
                                        myTarget.items[w].quality -= 1;
                                    }
                                }
                               
                                myTarget.itemQuality.RemoveAt(i);
                                _valueChanged = true;
                                EditorGUI.FocusTextInControl(null);
                                myTarget.UpdatePrefab();
                                return;
                            }
                           
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();
                    }
                    EditorGUILayout.Separator();
                    //===Quality List
                }

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _currencyExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _currencyExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUI.color = Color.white;
                GUILayout.Label(_currencyExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Currency Settings (" + myTarget.currencies.Count.ToString() + ")",
                    "You can create as many currency types as needed. Each currency will have an index displayed as blue text at the front. When your script accesses the currency value of an <InventoryHolder> component, you¡¯ll need to call <InventoryHolder>().GetCurrency(int _type) using the index number as the parameter."
                    ), _titleButtonStyle))
                {
                    _currencyExpand = !_currencyExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
               

                if (_currencyExpand)
                {
                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = _activeColor;
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("You can create as many currency types as needed. Each currency will have an index displayed as blue text at the front. When your script accesses the currency value of an <InventoryHolder> component, you¡¯ll need to call <InventoryHolder>().GetCurrency(int _type) using the index number as the parameter. ", MessageType.Info, true);
                    GUILayout.Space(30);
                    GUILayout.EndHorizontal();

                    //===Currency List
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button(new GUIContent("Add New [Currency]","Create a new type of currency."), GUILayout.Width(200)))
                    {
                        Currency _newCurrency = new Currency();
                        _newCurrency.name = "Currency" + myTarget.currencies.Count.ToString();
                        _newCurrency.color = Color.white;
                        _newCurrency.icon = null;
                        _newCurrency.ExchangeRate = new List<Vector3>();
                        _newCurrency.fold=true;
                        myTarget.currencies.Add(_newCurrency);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                    for (int i = 0; i < myTarget.currencies.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.backgroundColor = myTarget.currencies[i].fold ? _disableColor: _activeColor;
                        if (GUILayout.Button(myTarget.currencies[i].fold ? "+":"-",GUILayout.Width(20))) {
                            myTarget.currencies[i].fold = !myTarget.currencies[i].fold;
                            EditorGUI.FocusTextInControl(null);
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUI.color = _titleColor;
                        GUILayout.Label(new GUIContent("(ID: " + i.ToString() + ")","When accessing this currency from you script, you will need to use this ID."), GUILayout.Width(40));
                        GUI.color = Color.white;
                        myTarget.currencies[i].name=GUILayout.TextField(myTarget.currencies[i].name, GUILayout.Width(120));
                        myTarget.currencies[i].color = EditorGUILayout.ColorField(myTarget.currencies[i].color, GUILayout.Width(50));
                        myTarget.currencies[i].icon = (Sprite)EditorGUILayout.ObjectField(myTarget.currencies[i].icon, typeof(Sprite), false, GUILayout.Width(100));
                        GUI.backgroundColor = Color.red;
                        if (i > 0)
                        {
                            if (GUILayout.Button(new GUIContent("X", "Delete this currency"), GUILayout.Width(30)))
                            {
                                for (int w = 0; w < myTarget.items.Count; w++)
                                {
                                    if (myTarget.items[w].currency > i)
                                    {
                                        myTarget.items[w].currency -= 1;
                                    }
                                }
                                if (myTarget.RemoveSocketingCurrency > i) myTarget.RemoveSocketingCurrency -= 1;
                                if (myTarget.UnlockSocketingSlotsCurrency > i) myTarget.UnlockSocketingSlotsCurrency -= 1;
                                if (myTarget.EnchantingCurrencyType > i) myTarget.EnchantingCurrencyType -= 1;
                                if (myTarget.EnhancingCurrencyType > i) myTarget.EnhancingCurrencyType -= 1;
                                myTarget.currencies.RemoveAt(i);
                                for (int u = 0; u < myTarget.currencies.Count; u++)
                                {
                                    for (int y = 0; y < myTarget.currencies[u].ExchangeRate.Count; y++)
                                    {
                                        if (Mathf.FloorToInt(myTarget.currencies[u].ExchangeRate[y].x) == i)
                                        {
                                            myTarget.currencies[u].ExchangeRate.RemoveAt(y);
                                        }
                                    }
                                }
                                _valueChanged = true;
                                EditorGUI.FocusTextInControl(null);
                                myTarget.UpdatePrefab();
                                return;
                            }
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();

                        if (!myTarget.currencies[i].fold)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUI.backgroundColor = _buttonColor;
                            if (GUILayout.Button("Add Exchange Rate", GUILayout.Width(130)))
                            {
                                EditorGUI.FocusTextInControl(null);
                                myTarget.currencies[i].ExchangeRate.Add(new Vector3(0F, 1F, 1F));
                                _valueChanged = true;
                            }
                            GUI.backgroundColor = _backgroundColor;
                            GUILayout.EndHorizontal();

                            for (int u = 0; u < myTarget.currencies[i].ExchangeRate.Count; u++)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label("1x = ",GUILayout.Width(30));
                                int _selCurrency = Mathf.FloorToInt(myTarget.currencies[i].ExchangeRate[u].x);
                                int _num = Mathf.FloorToInt(myTarget.currencies[i].ExchangeRate[u].y);
                                bool _autoExchange = (myTarget.currencies[i].ExchangeRate[u].z > 0F);
                                EditorGUI.BeginChangeCheck();
                                _selCurrency = EditorGUILayout.Popup(_selCurrency, _currencyOption, GUILayout.Width(80));
                                GUILayout.Label(" x", GUILayout.Width(20));
                                _num = EditorGUILayout.IntField(_num, GUILayout.Width(30));
                                GUILayout.Label("-",GUILayout.Width(10));
                                _autoExchange = GUILayout.Toggle(_autoExchange, "Auto Exchange",GUILayout.Width(115));
                                if (EditorGUI.EndChangeCheck()) {
                                    myTarget.currencies[i].ExchangeRate[u] =new Vector3(_selCurrency, (float)_num, _autoExchange?1F:0F);
                                }
                                GUI.backgroundColor = Color.red;
                                if (GUILayout.Button("X", GUILayout.Width(20))) {
                                    myTarget.currencies[i].ExchangeRate.RemoveAt(u);
                                    EditorGUI.FocusTextInControl(null);
                                    _valueChanged = true;
                                    myTarget.UpdatePrefab();
                                    return;
                                }
                                GUI.backgroundColor = _backgroundColor;
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                    //===Currency List
                    EditorGUILayout.Separator();
                }

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _attExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _attExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUI.color = Color.white;
                GUILayout.Label(_attExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Attributes Settings (" + myTarget.itemAttributes.Count.ToString() + ")",
                    "Attributes represent the stats of your characters, such as attack power, defense, running speed, maximum health points, etc. You can create as many attributes as needed, with each attribute being an integer, float, or string value."
                    ), _titleButtonStyle))
                {
                    _attExpand = !_attExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
                if (_attExpand)
                {
                    //===Attribute List

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    EditorGUILayout.HelpBox("Attributes represent the stats of your characters, such as attack power, defense, running speed, maximum health points, etc. You can create as many attributes as needed, with each attribute being an integer, float, or string value.", MessageType.Info, true);
                    GUILayout.Space(30);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Attribute Name Color:", "The color of the attribute name on the interface."), GUILayout.Width(150));
                    myTarget.AttributeNameColor = EditorGUILayout.ColorField(myTarget.AttributeNameColor, GUILayout.Width(50));
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label(new GUIContent("Attribute List:", "List of the attributes."), GUILayout.Width(110));
                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button("Export to Json", GUILayout.Width(120)))
                    {
                        string _path = EditorUtility.SaveFilePanel("Export to Json", Application.dataPath, "AttributeSetting.txt", "txt");
                        if (_path.Length != 0)
                        {
                            myTarget.ExportAttributeDataToJson(_path);
                        }
                    }
                    if (GUILayout.Button("Import from Json", GUILayout.Width(120)))
                    {
                        string _path = EditorUtility.OpenFilePanel("Import from Json", Application.dataPath, "txt");
                        if (_path.Length != 0)
                        {
                            myTarget.ImportAttributeDataFromJson(_path);
                            GUILayout.EndHorizontal();
                            EditorGUI.FocusTextInControl(null);
                            myTarget.UpdatePrefab();
                            UnityEditor.EditorUtility.SetDirty(EditorAsset);
                            return;
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button(new GUIContent("Add New [Attribute]","Create a new attrubute."), GUILayout.Width(150)))
                    {
                        Attribute _newAtt = new Attribute();
                        _newAtt.name = "New Attribute" + myTarget.itemAttributes.Count.ToString();
                        _newAtt.key = "Att"+ myTarget.itemAttributes.Count.ToString();
                        _newAtt.upgradeIncrement = 0F;
                        _newAtt.visible = true;
                        _newAtt.stringValue = false;
                        _newAtt.fold = true;
                        _newAtt.value = "";
                        myTarget.itemAttributes.Add(_newAtt);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    GUI.backgroundColor = _tagColor;
                    GUILayout.Space(40);
                    if (GUILayout.Button("Expand All", GUILayout.Width(80)))
                    {
                        for (int i = 0; i < myTarget.itemAttributes.Count; i++) myTarget.itemAttributes[i].fold = true;
                    }

                    if (GUILayout.Button("Fold All", GUILayout.Width(80)))
                    {
                        for (int i = 0; i < myTarget.itemAttributes.Count; i++) myTarget.itemAttributes[i].fold = false;
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                    for (int i = 0; i < myTarget.itemAttributes.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.backgroundColor = myTarget.itemAttributes[i].fold ? _activeColor2 : _disableColor2;
                        GUILayout.Label(myTarget.itemAttributes[i].fold?"[-]":"[+]", GUILayout.Width(20));
                        if (GUILayout.Button(new GUIContent(myTarget.itemAttributes[i].name + " (key:" + myTarget.itemAttributes[i].key + ")","Click to expand"),GUILayout.Width(300)))
                        {
                            myTarget.itemAttributes[i].fold = !myTarget.itemAttributes[i].fold;
                            EditorGUI.FocusTextInControl(null);
                        }

                        GUI.color = i > 0 ? Color.white : Color.gray;
                        if (GUILayout.Button(new GUIContent(upIcon, "Move this attribute up."), _toolButtonStyle, GUILayout.Width(15)))
                        {
                            if (i > 0)
                            {
                                Attribute _mAtt = myTarget.itemAttributes[i].Copy();
                                _mAtt.fold = false;
                                myTarget.itemAttributes.RemoveAt(i);
                                myTarget.itemAttributes.Insert(i - 1, _mAtt);
                                EditorGUI.FocusTextInControl(null);
                                myTarget.UpdatePrefab();
                                return;
                            }
                        }
                        GUI.color = i < myTarget.itemAttributes.Count - 1 ? Color.white : Color.gray;
                        if (GUILayout.Button(new GUIContent(downIcon, "Move this attribute down."), _toolButtonStyle, GUILayout.Width(15)))
                        {
                            if (i < myTarget.itemAttributes.Count - 1)
                            {
                                Attribute _mAtt = myTarget.itemAttributes[i].Copy();
                                _mAtt.fold = false;
                                myTarget.itemAttributes.RemoveAt(i);
                                myTarget.itemAttributes.Insert(i + 1, _mAtt);
                                EditorGUI.FocusTextInControl(null);
                                myTarget.UpdatePrefab();
                                return;
                            }
                        }
                        GUI.color = Color.white;
                        if (i > 0)
                        {
                            GUI.backgroundColor = Color.red;
                            if (GUILayout.Button(new GUIContent("X", "Delete this attribute."), GUILayout.Width(30)))
                            {
                                for (int w = 0; w < myTarget.items.Count; w++)
                                {
                                    for (int v = 0; v < myTarget.items[w].attributes.Count; v++)
                                    {
                                        if (myTarget.items[w].attributes[v].key == myTarget.itemAttributes[i].key)
                                        {
                                            myTarget.items[w].attributes.RemoveAt(v);
                                        }
                                    }
                                }
                                myTarget.itemAttributes.RemoveAt(i);
                                EditorGUI.FocusTextInControl(null);
                                myTarget.UpdatePrefab();
                                return;
                            }
                           
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();
                        if (myTarget.itemAttributes[i].fold) {
                            EditorGUI.BeginChangeCheck();
                            string _scriptKey = myTarget.itemAttributes[i].key;

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("Display Name:", "The name displayed in the interface for this attribute."), GUILayout.Width(130));
                            myTarget.itemAttributes[i].name = EditorGUILayout.TextField(myTarget.itemAttributes[i].name, GUILayout.Width(150));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("Script Key:", 
                                "A short key used to access the value of this attribute in your script. For example, if the script key for the attack power attribute is \"atk\" and you want to find out the player¡¯s total attack power, you can call <InventoryHolder>().GetAttributeValue(\"atk\").")
                                , GUILayout.Width(130));
                            myTarget.itemAttributes[i].key = EditorGUILayout.TextField(myTarget.itemAttributes[i].key, GUILayout.Width(150));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("String Value:", 
                                "Check this if the attribute's value is a string. For example, you might have an attribute called \"Creator\", and when a player crafts a weapon, you can set this attribute to the player¡¯s name by calling Item.UpdateAttribute(\"Creator\", \"Player Name\").")
                                , GUILayout.Width(130));
                            myTarget.itemAttributes[i].stringValue = EditorGUILayout.Toggle(myTarget.itemAttributes[i].stringValue, GUILayout.Width(150));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("Upgrade Increment:", "Items can be upgraded through the \"Enhancement\" system, and each upgrade level increases the item's attributes by the amount specified here."), GUILayout.Width(130));
                            myTarget.itemAttributes[i].upgradeIncrement = EditorGUILayout.FloatField(myTarget.itemAttributes[i].upgradeIncrement, GUILayout.Width(150));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("Display Format:", "How this attribute displayed in the mouse hover infomation panel."), GUILayout.Width(130));
                            myTarget.itemAttributes[i].displayFormat = EditorGUILayout.Popup(myTarget.itemAttributes[i].displayFormat, _attFormatOption, GUILayout.Width(50));
                            GUILayout.Label(" Suffixes:",GUILayout.Width(60));
                            myTarget.itemAttributes[i].suffixes = GUILayout.TextField(myTarget.itemAttributes[i].suffixes,GUILayout.Width(35));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(90);
                            GUILayout.Label("Preview:",GUILayout.Width(100));
                            GUI.color = _buttonColor;
                            GUILayout.Label(myTarget.itemAttributes[i].name+ " "+_attFormatOption[myTarget.itemAttributes[i].displayFormat]+" 5"+ myTarget.itemAttributes[i].suffixes);
                            GUI.color = Color.white;
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("Core Attribute:", "Determines whether the attribute is displayed in bold font and ahead of other attributes."), GUILayout.Width(130));
                            myTarget.itemAttributes[i].coreStats = EditorGUILayout.Toggle(myTarget.itemAttributes[i].coreStats, GUILayout.Width(150));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("Compare Info:", "Whether display compare information for this attribute in mouse hover information panel."), GUILayout.Width(130));
                            myTarget.itemAttributes[i].compareInfo = EditorGUILayout.Toggle(myTarget.itemAttributes[i].compareInfo, GUILayout.Width(150));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("Visible in Hover Info:", "Determines whether the attribute is visible in the mouse hover information panel."), GUILayout.Width(130));
                            myTarget.itemAttributes[i].visible = EditorGUILayout.Toggle(myTarget.itemAttributes[i].visible, GUILayout.Width(150));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(70);
                            GUILayout.Label(new GUIContent("Visible in Stats Panel:", "Determines whether the attribute is visible in the stats panel."), GUILayout.Width(130));
                            myTarget.itemAttributes[i].visibleInStatsPanel = EditorGUILayout.Toggle(myTarget.itemAttributes[i].visibleInStatsPanel, GUILayout.Width(150));
                            GUILayout.EndHorizontal();



                            if (EditorGUI.EndChangeCheck())
                            {
                                for (int w = 0; w < myTarget.items.Count; w++)
                                {
                                    for (int v = 0; v < myTarget.items[w].attributes.Count; v++)
                                    {
                                        if (myTarget.items[w].attributes[v].key == _scriptKey)
                                        {
                                            myTarget.items[w].attributes[v].name= myTarget.itemAttributes[i].name;
                                            myTarget.items[w].attributes[v].key = myTarget.itemAttributes[i].key;
                                            myTarget.items[w].attributes[v].stringValue = myTarget.itemAttributes[i].stringValue;
                                            myTarget.items[w].attributes[v].upgradeIncrement = myTarget.itemAttributes[i].upgradeIncrement;
                                            myTarget.items[w].attributes[v].visible = myTarget.itemAttributes[i].visible;
                                            myTarget.items[w].attributes[v].visibleInStatsPanel= myTarget.itemAttributes[i].visibleInStatsPanel;
                                            myTarget.items[w].attributes[v].displayFormat = myTarget.itemAttributes[i].displayFormat;
                                            myTarget.items[w].attributes[v].suffixes = myTarget.itemAttributes[i].suffixes;
                                            myTarget.items[w].attributes[v].coreStats = myTarget.itemAttributes[i].coreStats;
                                            myTarget.items[w].attributes[v].compareInfo = myTarget.itemAttributes[i].compareInfo;
                                        }
                                    }
                                }
                            }
                        }

                    }
                    //===Attribute List
                    EditorGUILayout.Separator();
                }

               

#if MASTER_CHARACTER_CREATOR
                int _outfitEnumCount = 4;
                int[] _outfitSlotsCountM = new int[_outfitEnumCount];
                int[] _outfitSlotsCountF = new int[_outfitEnumCount];
                string[] _outfitSlotsOption = new string[_outfitEnumCount];
                if (MccData != null)
                {
                    _outfitEnumCount = System.Enum.GetValues(typeof(MasterCharacterCreator.OutfitSlots)).Length;
                    _outfitSlotsCountM = new int[_outfitEnumCount];
                    _outfitSlotsCountF = new int[_outfitEnumCount];
                    _outfitSlotsOption = new string[_outfitEnumCount];
                    for (int i = 0; i < _outfitSlotsOption.Length; i++) _outfitSlotsOption[i] = ((MasterCharacterCreator.OutfitSlots)i).ToString();
                    for (int i = 0; i < _outfitSlotsCountM.Length; i++)
                    {
                        _outfitSlotsCountM[i] = MccData.GetOutfitSettings(MasterCharacterCreator.Sex.Male, (MasterCharacterCreator.OutfitSlots)i).Length;
                        _outfitSlotsCountF[i] = MccData.GetOutfitSettings(MasterCharacterCreator.Sex.Female, (MasterCharacterCreator.OutfitSlots)i).Length;
                    }
                }
#endif

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _craftExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _craftExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_craftExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Crafting Settings",
                    "Players can craft items using other items as materials. They must also have the corresponding blueprint in their inventory as hidden items. For detailed setup instructions, refer to the \"Craft Materials\" section."
                    ), _titleButtonStyle))
                {
                    _craftExpand = !_craftExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.EndHorizontal();

                if (_craftExpand)
                {
                    if (myTarget.itemTypes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item category.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _buttonColor;
                        myTarget.EnableCrafting = GUILayout.Toggle(myTarget.EnableCrafting, new GUIContent("Enable Crafting", "If you don¡¯t plan to include a crafting system in your game, uncheck this to disable the feature."));
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUI.backgroundColor = _activeColor;
                        GUILayout.Space(30);
                        EditorGUILayout.HelpBox("Players can CRAFT items using other items as materials. They must also have the corresponding blueprint in their inventory as hidden items. For detailed setup instructions, refer to the Craft Materials section.", MessageType.Info, true);
                        GUILayout.Space(30);
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();

                        if (myTarget.EnableCrafting) {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Material Category:", "Items used as crafting materials must belong to this category."), GUILayout.Width(150));
                            myTarget.CraftingMaterialCategoryID = Mathf.Clamp(myTarget.CraftingMaterialCategoryID,0,myTarget.itemTypes.Count-1);
                            int _materialCategory = myTarget.CraftingMaterialCategoryID;
                            EditorGUI.BeginChangeCheck();
                            _materialCategory = EditorGUILayout.Popup("", _materialCategory, _typeOptions, GUILayout.Width(200));
                            if (EditorGUI.EndChangeCheck()) myTarget.CraftingMaterialCategoryID = _materialCategory;
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Blueprint Tag:", "Items used as blueprints must have this tag, refer to the Tags setting section."), GUILayout.Width(150));
                            myTarget.CraftingBlueprintTag = GUILayout.TextField(myTarget.CraftingBlueprintTag, GUILayout.Width(200));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Crafting Time:", "When crafting, a progress bar will fill from 1-100%. This setting determines how long the process will take until the item is created."), GUILayout.Width(150));
                            myTarget.CraftingTime = EditorGUILayout.Slider(myTarget.CraftingTime,0.2F,3F, GUILayout.Width(170));
                            GUILayout.Label("Sec", GUILayout.Width(30));
                            GUILayout.EndHorizontal();

                            EditorGUILayout.Separator();
                        }
                    }
                }

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _entExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _entExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_entExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Enchantment Setting (" + myTarget.itemEnchantments.Count.ToString() + ")",
                    "Players can enchant items to grant them additional attributes, which can be either positive or negative. Enchanting requires a specific type of currency and a particular item (such as a \"Magic Scroll\")."
                    ), _titleButtonStyle))
                {
                    _entExpand = !_entExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.EndHorizontal();

                if (_entExpand)
                {
                    if (myTarget.itemAttributes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item attribute.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else if (myTarget.itemTypes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item category.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else if (myTarget.items.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _buttonColor;
                        myTarget.EnableEnchanting= GUILayout.Toggle(myTarget.EnableEnchanting, new GUIContent("Enable Enchanting", "Uncheck this if you don't plan to include an enchanting system in your game to disable the feature."));
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUI.backgroundColor = _activeColor;
                        GUILayout.Space(30);
                        EditorGUILayout.HelpBox("Players can ENCHANT items to grant them additional attributes, which can be either positive or negative. Enchanting requires a specific type of currency and a particular item (such as a Magic Scroll).", MessageType.Info, true);
                        GUILayout.Space(30);
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();

                        if (myTarget.EnableEnchanting)
                        {
                            EditorGUILayout.Separator();
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enchanting Category:", "Only items in this category can be enchanted."), GUILayout.Width(150));
                            myTarget.EnchantingCategoryID = Mathf.Clamp(myTarget.EnchantingCategoryID,0,myTarget.itemTypes.Count-1);
                            int _enchantingCategory = myTarget.EnchantingCategoryID;
                            EditorGUI.BeginChangeCheck();
                            _enchantingCategory = EditorGUILayout.Popup("", _enchantingCategory, _typeOptions, GUILayout.Width(200));
                            if (EditorGUI.EndChangeCheck()) myTarget.EnchantingCategoryID = _enchantingCategory;
                            GUILayout.EndHorizontal();


                            EditorGUILayout.Separator();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Random number of enchantments player can get:", "When enchanting, player will randomly get enchantments by the number of this range."));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(60);
                            EditorGUILayout.MinMaxSlider(ref myTarget.EnchantmentNumberRange.x,ref myTarget.EnchantmentNumberRange.y,0,3, GUILayout.Width(280));
                            myTarget.EnchantmentNumberRange.x = Mathf.FloorToInt(myTarget.EnchantmentNumberRange.x);
                            myTarget.EnchantmentNumberRange.y = Mathf.FloorToInt(myTarget.EnchantmentNumberRange.y);
                            GUILayout.Label("("+ Mathf.FloorToInt(myTarget.EnchantmentNumberRange.x).ToString()+"~"+ Mathf.FloorToInt(myTarget.EnchantmentNumberRange.y).ToString()+")",GUILayout.Width(100));
                            GUILayout.EndHorizontal();

  
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enchanting Success Rate:", "The percentage chance of success when a player enchants an item."), GUILayout.Width(170));
                            myTarget.EnchantingSuccessRate=EditorGUILayout.IntSlider(myTarget.EnchantingSuccessRate,1,100, GUILayout.Width(180));
                            GUILayout.EndHorizontal();

                    

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Required Currency:", "Set the currency type and the cost required to enchant an item."), GUILayout.Width(170));
                            myTarget.EnchantingCurrencyType = Mathf.Clamp(myTarget.EnchantingCurrencyType, 0, myTarget.currencies.Count - 1);
                            int _entCurrency = myTarget.EnchantingCurrencyType;
                            EditorGUI.BeginChangeCheck();
                            _entCurrency = EditorGUILayout.Popup("", _entCurrency, _currencyOption, GUILayout.Width(100));
                            if (EditorGUI.EndChangeCheck())myTarget.EnchantingCurrencyType = _entCurrency;
                            myTarget.EnchantingCurrencyNeed = EditorGUILayout.IntField(myTarget.EnchantingCurrencyNeed, GUILayout.Width(80));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Required Item as Material:", "Select the material item and quantity needed for enchanting."), GUILayout.Width(170));
                            myTarget.EnchantingMaterial.x = Mathf.Clamp(myTarget.EnchantingMaterial.x,0,myTarget.items.Count-1);
                            int _entMat = Mathf.FloorToInt(myTarget.EnchantingMaterial.x);
                            GUILayout.Box(EditorAsset.ItemAssets[_entMat].icon, GUILayout.Width(20), GUILayout.Height(20));
                            EditorGUI.BeginChangeCheck();
                            _entMat = EditorGUILayout.Popup("", _entMat, _itemOption, GUILayout.Width(100));
                            if (EditorGUI.EndChangeCheck()) myTarget.EnchantingMaterial = new Vector2(_entMat, myTarget.EnchantingMaterial.y);
                            GUILayout.Label("x", GUILayout.Width(10));
                            myTarget.EnchantingMaterial.y = Mathf.Clamp(EditorGUILayout.IntField(Mathf.FloorToInt(myTarget.EnchantingMaterial.y), GUILayout.Width(40)),1,99);
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enchanting Time:", "When enchanting, a progress bar will fill from 1-100%. This setting determines how long the process will take to complete the enchantment."), GUILayout.Width(150));
                            myTarget.EnchantingTime = EditorGUILayout.Slider(myTarget.EnchantingTime, 0.2F, 3F, GUILayout.Width(170));
                            GUILayout.Label("Sec", GUILayout.Width(30));
                            GUILayout.EndHorizontal();

                            EditorGUILayout.Separator();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            myTarget.RandomEnchantmentsForNewItem = GUILayout.Toggle(myTarget.RandomEnchantmentsForNewItem, new GUIContent("Random Enchantments For New Item", "When using \"new Item(int _uid)\", the enchantments of the item will be randomized."));
                            GUILayout.EndHorizontal();

                            EditorGUILayout.Separator();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enchantment Name Color:", "The color of the enchantment name on the interface."), GUILayout.Width(150));
                            myTarget.EnchantingNameColor = EditorGUILayout.ColorField(myTarget.EnchantingNameColor, GUILayout.Width(50));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enchantment Prefixes Color:", "The prefixes string will be displayed in the front of the item name with this color."), GUILayout.Width(150));
                            myTarget.EnchantingPrefixesColor = EditorGUILayout.ColorField(myTarget.EnchantingPrefixesColor,GUILayout.Width(50));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enchantment Suffxes Color:", "The suffxes string will be displayed in the end of the item name with this color."), GUILayout.Width(150));
                            myTarget.EnchantingSuffxesColor = EditorGUILayout.ColorField(myTarget.EnchantingSuffxesColor, GUILayout.Width(50));
                            GUILayout.EndHorizontal();

                            EditorGUILayout.Separator();

                           

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enchantments List:", "Create a list of enchantments, each of which can have multiple attributes."),GUILayout.Width(130));
                            GUI.backgroundColor = _buttonColor;
                            if (GUILayout.Button("Export to Json", GUILayout.Width(110)))
                            {
                                string _path = EditorUtility.SaveFilePanel("Export to Json", Application.dataPath, "EnchantmentSetting.txt", "txt");
                                if (_path.Length != 0)
                                {
                                    myTarget.ExportEnchantmentDataToJson(_path);
                                }
                            }
                            if (GUILayout.Button("Import from Json", GUILayout.Width(110)))
                            {
                                string _path = EditorUtility.OpenFilePanel("Import from Json", Application.dataPath, "txt");
                                if (_path.Length != 0)
                                {
                                    myTarget.ImportEnchantmentDataFromJson(_path);
                                    GUILayout.EndHorizontal();
                                    EditorGUI.FocusTextInControl(null);
                                    myTarget.UpdatePrefab();
                                    UnityEditor.EditorUtility.SetDirty(EditorAsset);
                                    return;
                                }
                            }
                            GUILayout.EndHorizontal();

                            

                            //===Enchantment List
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUI.backgroundColor = _buttonColor;
                            if (GUILayout.Button(new GUIContent("Add New [Enchantment]", "Create a new enchantment to the pool."), GUILayout.Width(170)))
                            {
                                Enchantment _newAtt = new Enchantment();
                                _newAtt.name = "New Enchantment" + myTarget.itemEnchantments.Count.ToString();
                                if (myTarget.itemEnchantments.Count > 0)
                                {
                                    _newAtt.uid = myTarget.itemEnchantments[myTarget.itemEnchantments.Count - 1].uid + 1;
                                }
                                else
                                {
                                    _newAtt.uid = myTarget.itemEnchantments.Count;
                                }
                                _newAtt.fold = true;
                                _newAtt.attributes.Clear();
                                myTarget.itemEnchantments.Add(_newAtt);
                                _valueChanged = true;
                            }
                            GUI.backgroundColor = _tagColor;
                            GUILayout.Space(40);
                            if (GUILayout.Button("Expand All", GUILayout.Width(70)))
                            {
                                for (int i = 0; i < myTarget.itemEnchantments.Count; i++) myTarget.itemEnchantments[i].fold = true;
                            }

                            if (GUILayout.Button("Fold All", GUILayout.Width(70)))
                            {
                                for (int i = 0; i < myTarget.itemEnchantments.Count; i++) myTarget.itemEnchantments[i].fold = false;
                            }
                            GUI.backgroundColor = _backgroundColor;
                            GUILayout.EndHorizontal();
                            for (int i = 0; i < myTarget.itemEnchantments.Count; i++)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                GUI.backgroundColor = myTarget.itemEnchantments[i].fold ? _activeColor2 : _disableColor2;
                                GUILayout.Label(myTarget.itemEnchantments[i].fold ? "[-]" : "[+]", GUILayout.Width(20));
                                if (GUILayout.Button(new GUIContent(myTarget.itemEnchantments[i].name + " (UID:" + myTarget.itemEnchantments[i].uid.ToString() + ")","Click to expand."), GUILayout.Width(300)))
                                {
                                    myTarget.itemEnchantments[i].fold = !myTarget.itemEnchantments[i].fold;
                                    EditorGUI.FocusTextInControl(null);
                                }
                                GUI.backgroundColor = Color.red;
                                if (GUILayout.Button(new GUIContent("X","Delete this enchantment"), GUILayout.Width(30)))
                                {
                                    myTarget.itemEnchantments.RemoveAt(i);
                                    EditorGUI.FocusTextInControl(null);
                                    myTarget.UpdatePrefab();
                                    return;
                                }
                                GUI.backgroundColor = _backgroundColor;
                                GUILayout.EndHorizontal();
                                if (myTarget.itemEnchantments[i].fold)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    GUILayout.Label(new GUIContent("Display Name:","The name will be displayed in the floating panel of item detailed information."), GUILayout.Width(130));
                                    myTarget.itemEnchantments[i].name = EditorGUILayout.TextField(myTarget.itemEnchantments[i].name, GUILayout.Width(150));
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    GUILayout.Label(new GUIContent("Prefixes:", "The prefixes string will be displayed in the front of the item name."), GUILayout.Width(130));
                                    myTarget.itemEnchantments[i].prefixes = EditorGUILayout.TextField(myTarget.itemEnchantments[i].prefixes, GUILayout.Width(150));
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    GUILayout.Label(new GUIContent("Prefixes Priority:", "Only one prefixes string with highest priority will be displayed if an item has multiple enchantments."), GUILayout.Width(130));
                                    myTarget.itemEnchantments[i].prefixesPriority = EditorGUILayout.IntSlider(myTarget.itemEnchantments[i].prefixesPriority,0,100, GUILayout.Width(150));
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    GUILayout.Label(new GUIContent("Suffixes:", "The suffixes string will be displayed in the end of the item name."), GUILayout.Width(130));
                                    myTarget.itemEnchantments[i].suffixes = EditorGUILayout.TextField(myTarget.itemEnchantments[i].suffixes, GUILayout.Width(150));
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    GUILayout.Label(new GUIContent("Suffixes Priority:", "Only one suffixes string with highest priority will be displayed if an item has multiple enchantments."), GUILayout.Width(130));
                                    myTarget.itemEnchantments[i].suffixesPriority = EditorGUILayout.IntSlider(myTarget.itemEnchantments[i].suffixesPriority, 0, 100, GUILayout.Width(150));
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    GUILayout.Label(new GUIContent("Attributes: (" + myTarget.itemEnchantments[i].attributes.Count + ")","Items with this enchantment will add these attributes to their base attributes."), GUILayout.Width(130));
                                    GUI.color = Color.white;
                                    GUI.backgroundColor = _buttonColor;
                                    if (GUILayout.Button(new GUIContent("+","Add a new attribute."), GUILayout.Width(30)))
                                    {
                                        myTarget.itemEnchantments[i].attributes.Add(myTarget.itemAttributes[0].Copy());
                                        myTarget.itemEnchantments[i].attributes[myTarget.itemEnchantments[i].attributes.Count - 1].value = "0";
                                        _valueChanged = true;
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();
                                    for (int u = 0; u < myTarget.itemEnchantments[i].attributes.Count; u++)
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        int _sel = 0;
                                        for (int x = 0; x < myTarget.itemAttributes.Count; x++)
                                        {
                                            if (myTarget.itemAttributes[x].key == myTarget.itemEnchantments[i].attributes[u].key)
                                            {
                                                _sel = x;
                                                break;
                                            }
                                        }
                                        GUI.backgroundColor = _attColor;
                                        GUILayout.Label(">", GUILayout.Width(15));
                                        EditorGUI.BeginChangeCheck();
                                        _sel = EditorGUILayout.Popup(new GUIContent("","Click to select the attribute type."), _sel, _attOptions, GUILayout.Width(100));
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            string _value = myTarget.itemEnchantments[i].attributes[u].value;
                                            myTarget.itemEnchantments[i].attributes[u] = myTarget.itemAttributes[_sel].Copy();
                                            myTarget.itemEnchantments[i].attributes[u].value = _value;
                                        }

                                        GUILayout.Label(" :", GUILayout.Width(15));
                                        myTarget.itemEnchantments[i].attributes[u].value = EditorGUILayout.FloatField(float.Parse(myTarget.itemEnchantments[i].attributes[u].value), GUILayout.Width(60)).ToString();
                                        GUI.backgroundColor = Color.red;
                                        GUI.color = Color.white;
                                        if (GUILayout.Button(new GUIContent("X","Delete this attribute."), GUILayout.Width(30)))
                                        {
                                            myTarget.itemEnchantments[i].attributes.RemoveAt(u);
                                            myTarget.UpdatePrefab();
                                            return;
                                        }
                                        GUI.backgroundColor = _backgroundColor;

                                        GUILayout.EndHorizontal();
                                    }
                                }

                            }
                            //===Enchantment List
                            EditorGUILayout.Separator();
                        }
                    }
                }

               

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _upgradeExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _upgradeExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_upgradeExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Enhancement Settings",
                    "Player can upgrade an item with the enhancement system. The enhancing will cost one type of currency and two specific items. Each upgrade level will make the attributes of this items increased by the amount set by \"Upgrade Increment\" property in attributes setting"
                    ), _titleButtonStyle))
                {
                    _upgradeExpand = !_upgradeExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.EndHorizontal();
                if (_upgradeExpand)
                {
                    if (myTarget.items.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else if (myTarget.itemTypes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item category.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _buttonColor;
                        myTarget.EnableEnhancing = GUILayout.Toggle(myTarget.EnableEnhancing, new GUIContent("Enable Enhancing", " If you plan to not include Enhancing system in your game, uncheck this to disable this feature."));
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUI.backgroundColor = _activeColor;
                        GUILayout.Space(30);
                        EditorGUILayout.HelpBox("Player can upgrade an item with the ENHANCEMENT system. The enhancing will cost one type of currency and two specific items. Each upgrade level will make the attributes of this items increased by the amount set by Upgrade Increment property in attributes setting.", MessageType.Info, true);
                        GUILayout.Space(30);
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();

                        if (myTarget.EnableEnhancing)
                        {
                            EditorGUILayout.Separator();
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enhancing Category:", "Only items belong to this category can be enhanced."), GUILayout.Width(150));
                            myTarget.EnhancingCategoryID = Mathf.Clamp(myTarget.EnhancingCategoryID,0,myTarget.itemTypes.Count-1);
                            int _enhancingCategory = myTarget.EnhancingCategoryID;
                            EditorGUI.BeginChangeCheck();
                            _enhancingCategory = EditorGUILayout.Popup("", _enhancingCategory, _typeOptions, GUILayout.Width(200));
                            if (EditorGUI.EndChangeCheck()) myTarget.EnhancingCategoryID = _enhancingCategory;
                            GUILayout.EndHorizontal();

                            EditorGUILayout.Separator();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Maximum Enhancing Level:", "The maximum level an item can be enhanced."), GUILayout.Width(170));
                            myTarget.MaxiumEnhancingLevel = EditorGUILayout.IntSlider(myTarget.MaxiumEnhancingLevel, 1, 30, GUILayout.Width(180));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enhancing Success Curve:", "Success rate in percentage when player enhance an item. The rate will change by the curve from level.0~ maximum enhancing Level."), GUILayout.Width(170));
                            myTarget.EnhancingSuccessCurve = EditorGUILayout.CurveField(myTarget.EnhancingSuccessCurve, GUILayout.Width(180));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            myTarget.DestroyEquipmentWhenFail = GUILayout.Toggle(myTarget.DestroyEquipmentWhenFail, new GUIContent("Destroy item when fail ( Level>", "When check this option, the item will be destroyed when fail by upgrading to level x+ "), GUILayout.Width(200));
                            myTarget.DestroyEquipmentWhenFailLevel = EditorGUILayout.IntSlider(myTarget.DestroyEquipmentWhenFailLevel, 0, myTarget.MaxiumEnhancingLevel, GUILayout.Width(150));
                            GUILayout.Label(")", GUILayout.Width(10));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Enhancing Time:", "When enhancing, there will be a progress bar move from 1-100%, when it¡¯s done, the item will be enhanced, this is how long the process will take."), GUILayout.Width(150));
                            myTarget.EnhancingTime = EditorGUILayout.Slider(myTarget.EnhancingTime, 0.2F, 3F, GUILayout.Width(170));
                            GUILayout.Label("Sec", GUILayout.Width(30));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Required Currency:", "Set the currency type and cost when player enhance an item."), GUILayout.Width(170));
                            myTarget.EnhancingCurrencyType = Mathf.Clamp(myTarget.EnhancingCurrencyType, 0, myTarget.currencies.Count - 1);
                            int _enhancingCurrency = myTarget.EnhancingCurrencyType;
                            EditorGUI.BeginChangeCheck();
                            _enhancingCurrency = EditorGUILayout.Popup("", _enhancingCurrency, _currencyOption, GUILayout.Width(100));
                            if (EditorGUI.EndChangeCheck()) myTarget.EnhancingCurrencyType = _enhancingCurrency;
                            myTarget.EnhancingCurrencyNeed = EditorGUILayout.IntField(myTarget.EnhancingCurrencyNeed, GUILayout.Width(80));
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Required Item as Material:", "Select the material item and number of cost when player enhance an item."), GUILayout.Width(170));
                            GUILayout.EndHorizontal();
                            if (myTarget.EnhancingMaterials.Length < 2)
                            {
                                myTarget.EnhancingMaterials = new Vector2[2] { Vector2.one, Vector2.one };
                            }
                            for (int i = 0; i < myTarget.EnhancingMaterials.Length; i++)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(40);
                                myTarget.EnhancingMaterials[i].x = Mathf.Clamp(myTarget.EnhancingMaterials[i].x,0,myTarget.items.Count-1);
                                int _enhancingMat = Mathf.Max(0, Mathf.FloorToInt(myTarget.EnhancingMaterials[i].x));
                                GUILayout.Box(EditorAsset.ItemAssets[_enhancingMat].icon, GUILayout.Width(20), GUILayout.Height(20));
                                EditorGUI.BeginChangeCheck();
                                _enhancingMat = EditorGUILayout.Popup("", _enhancingMat, _itemOption, GUILayout.Width(100));
                                if (EditorGUI.EndChangeCheck()) myTarget.EnhancingMaterials[i] = new Vector2(_enhancingMat, myTarget.EnhancingMaterials[i].y);
                                GUILayout.Label("x", GUILayout.Width(10));
                                myTarget.EnhancingMaterials[i].y = Mathf.Clamp(EditorGUILayout.IntField(Mathf.FloorToInt(myTarget.EnhancingMaterials[i].y), GUILayout.Width(65)), 1, 99);
                                GUILayout.EndHorizontal();
                            }

                            EditorGUILayout.Separator();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            myTarget.EnableEnhancingGlow = GUILayout.Toggle(myTarget.EnableEnhancingGlow, new GUIContent("Enable Glowing Effect", "check this to make the icon glow by the enhancing Level."));
                            GUILayout.EndHorizontal();

                            if (myTarget.EnableEnhancingGlow) {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(50);
                                GUILayout.Label(new GUIContent("Glow Intensity Curve:", "The icon will glow by the curve from level.0~ maximum enhancing Level."), GUILayout.Width(170));
                                myTarget.EnhancingGlowCurve = EditorGUILayout.CurveField(myTarget.EnhancingGlowCurve, GUILayout.Width(180));
                                GUILayout.EndHorizontal();
                            }

                            EditorGUILayout.Separator();

                        }
                    }

                }


                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _socketingExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _socketingExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_socketingExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Socketing Settings",
                    "Players can socketing items with specified category and tag to other items to grant them additional attributes."
                    ), _titleButtonStyle))
                {
                    _socketingExpand = !_socketingExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.EndHorizontal();

                if (_socketingExpand)
                {
                    if (myTarget.itemAttributes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item attribute.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else if (myTarget.itemTypes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item category.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else if (myTarget.items.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _buttonColor;
                        myTarget.EnableSocketing = GUILayout.Toggle(myTarget.EnableSocketing, new GUIContent("Enable Socketing", "Toggle this option to enable or disable the socketing system in your game. If unchecked, the feature will not be available."));
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUI.backgroundColor = _activeColor;
                        GUILayout.Space(30);
                        EditorGUILayout.HelpBox("The SOCKETING system allows players to insert (socket) items of specified categories and tags into another item to boost its attributes. Follow the configuration options below to tailor the system to your game's needs.",MessageType.Info ,true);
                        GUILayout.Space(30);
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();

                        if (myTarget.EnableSocketing)
                        {
                            EditorGUILayout.Separator();
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Socketed Items (Receiver) Category:", "Defines the category of items that can have socketing slots (e.g., weapons, armor). Only items in this category can receive socketing items."), GUILayout.Width(250));
                            myTarget.SocketedCategoryFilter = Mathf.Clamp(myTarget.SocketedCategoryFilter,0,myTarget.itemTypes.Count-1);
                            int _socketedCategory = myTarget.SocketedCategoryFilter;
                            EditorGUI.BeginChangeCheck();
                            _socketedCategory = EditorGUILayout.Popup("", _socketedCategory, _typeOptions, GUILayout.Width(150));
                            if (EditorGUI.EndChangeCheck()) myTarget.SocketedCategoryFilter = _socketedCategory;
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Socketing Items (Plug-in) Category:", "Specifies the category of items that can be inserted (socketed) into socketed items (e.g., gems, runes)."), GUILayout.Width(250));
                            myTarget.SocketingCategoryFilter = Mathf.Clamp(myTarget.SocketingCategoryFilter, 0, myTarget.itemTypes.Count - 1);
                            int _socketingCategory = myTarget.SocketingCategoryFilter;
                            EditorGUI.BeginChangeCheck();
                            _socketingCategory = EditorGUILayout.Popup("", _socketingCategory, _typeOptions, GUILayout.Width(150));
                            if (EditorGUI.EndChangeCheck()) myTarget.SocketingCategoryFilter = _socketingCategory;
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label(new GUIContent("Socketing Items (Plug-in) Tag:", " Filters socketing items further by requiring them to have a specific tag (e.g., \"FireGem\" or \"MagicRune\"). Leave blank if no tag filtering is needed."), GUILayout.Width(250));
                            myTarget.SocketingTagFilter = GUILayout.TextField(myTarget.SocketingTagFilter, GUILayout.Width(150));
                            GUILayout.EndHorizontal();

                            EditorGUILayout.Separator();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            myTarget.RandomSocketingSlotsNummber = GUILayout.Toggle(myTarget.RandomSocketingSlotsNummber, new GUIContent("Random Skocketing Slots Number for New Item.", "Enable this to randomize the number of socketing slots when a new item is created using new Item(int _uid)."));
                            GUILayout.EndHorizontal();

                            if (myTarget.RandomSocketingSlotsNummber)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(60);
                                GUILayout.Label(new GUIContent("Minimal Socketing Slots Number:", "Sets the minimum number of socketing slots for newly created items when randomization is enabled."), GUILayout.Width(250));
                                myTarget.MinimalSocketingSlotsNumber = EditorGUILayout.IntSlider(myTarget.MinimalSocketingSlotsNumber,0, myTarget.MaxmiumSocketingSlotsNumber, GUILayout.Width(150));
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(60);
                                GUILayout.Label(new GUIContent("Maximum Socketing Slots Number:", " Sets the maximum number of socketing slots for newly created items when randomization is enabled."), GUILayout.Width(250));
                                myTarget.MaxmiumSocketingSlotsNumber = EditorGUILayout.IntSlider(myTarget.MaxmiumSocketingSlotsNumber, myTarget.MinimalSocketingSlotsNumber, 5, GUILayout.Width(150));
                                GUILayout.EndHorizontal();
                            }
                            EditorGUILayout.Separator();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            myTarget.LockSocketingSlotsByDefault = GUILayout.Toggle(myTarget.LockSocketingSlotsByDefault, new GUIContent("Lock Socketing Slots Number By Default.", "Enable this to lock socketing slots by default when a new item is created."));
                            GUILayout.EndHorizontal();

                            if (myTarget.LockSocketingSlotsByDefault)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(60);
                                GUILayout.Label(new GUIContent("Random Chance to Lock Socketing Slots:", "Specify the probability (as a percentage) of socketing slots being locked by default for newly created items."), GUILayout.Width(250));
                                myTarget.RandomChanceToLockSocketingSlots = EditorGUILayout.IntSlider(myTarget.RandomChanceToLockSocketingSlots, 0, 100, GUILayout.Width(150));
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(60);
                                GUILayout.Label(new GUIContent("Unlock Socketing Slot Cost:", "The cost for the player to unlock a locked socketing slot. This cost can represent in-game currency or another resource."), GUILayout.Width(250));
                                myTarget.UnlockSocketingSlotsCurrency = Mathf.Clamp(myTarget.UnlockSocketingSlotsCurrency, 0, myTarget.currencies.Count - 1);
                                int _socketingUnlockCurrency = myTarget.UnlockSocketingSlotsCurrency;
                                EditorGUI.BeginChangeCheck();
                                _socketingUnlockCurrency = EditorGUILayout.Popup("", _socketingUnlockCurrency, _currencyOption, GUILayout.Width(98));
                                if (EditorGUI.EndChangeCheck()) myTarget.UnlockSocketingSlotsCurrency = _socketingUnlockCurrency;
                                myTarget.UnlockSocketingSlotsPrice = EditorGUILayout.IntField(myTarget.UnlockSocketingSlotsPrice,GUILayout.Width(50));
                                GUILayout.EndHorizontal();
                            }
                            EditorGUILayout.Separator();

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            myTarget.EnableRemoveSocketing = GUILayout.Toggle(myTarget.EnableRemoveSocketing, new GUIContent("Allow Remove Socketing Items.", "Enable this to allow players to remove socketed items from their slots."));
                            GUILayout.EndHorizontal();

                            if (myTarget.EnableRemoveSocketing)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(60);
                                GUILayout.Label(new GUIContent("Remove Socketing Item Cost:", "The cost for the player to remove a socketed item."), GUILayout.Width(250));
                                myTarget.RemoveSocketingCurrency = Mathf.Clamp(myTarget.RemoveSocketingCurrency, 0, myTarget.currencies.Count - 1);
                                int _socketingRemoveCurrency = myTarget.RemoveSocketingCurrency;
                                EditorGUI.BeginChangeCheck();
                                _socketingRemoveCurrency = EditorGUILayout.Popup("", _socketingRemoveCurrency, _currencyOption, GUILayout.Width(98));
                                if (EditorGUI.EndChangeCheck()) myTarget.RemoveSocketingCurrency = _socketingRemoveCurrency;
                                myTarget.RemoveSocketingPrice = EditorGUILayout.IntField(myTarget.RemoveSocketingPrice, GUILayout.Width(50));
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(60);
                                myTarget.DestroySocketItemWhenRemove = GUILayout.Toggle(myTarget.DestroySocketItemWhenRemove, new GUIContent("Destroy Socketed Item When Remove", "Enable this option to destroy the socketed item when it is removed. Disable it if you want the item to return to the player's inventory instead."));
                                GUILayout.EndHorizontal();
                            }

                            EditorGUILayout.Separator();

                        }
                    }
                }


                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _itemExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _itemExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_itemExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(new GUIContent(" Item Settings (" + myTarget.items.Count.ToString() + ")",
                    "Setup items database."
                    ), _titleButtonStyle))
                {
                    _itemExpand = !_itemExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.EndHorizontal();
                GUI.backgroundColor = _backgroundColor;
                if (_itemExpand)
                {
                    bool _ready = true;
                    if (myTarget.itemTypes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item type.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                        _ready = false;
                    }
                    if (myTarget.itemQuality.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one item quality.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                        _ready = false;
                    }
                    if (myTarget.itemAttributes.Count <= 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                        GUI.color = Color.red;
                        GUILayout.Label("You must have at least one attribute.");
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                        _ready = false;
                    }
                    if (_ready)
                    {
                        

                        //===Item List
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.backgroundColor = _tagColor;
                        if (GUILayout.Button("Fold All", GUILayout.Width(80)))
                        {
                            for (int i = 0; i < myTarget.items.Count; i++) myTarget.items[i].fold = false;
                        }

                        GUILayout.Space(30);
                        GUI.backgroundColor = _buttonColor;
                        if (GUILayout.Button("Export to Json", GUILayout.Width(120)))
                        {
                            string _path= EditorUtility.SaveFilePanel("Export to Json", Application.dataPath, "ItemSetting.txt", "txt");
                            if (_path.Length != 0) {
                                myTarget.ExportItemDataToJson(_path);
                            }
                        }
                        if (GUILayout.Button("Import from Json", GUILayout.Width(120)))
                        {
                            string _path = EditorUtility.OpenFilePanel("Import from Json", Application.dataPath, "txt");
                            if (_path.Length != 0)
                            {
                                myTarget.ImportItemDataFromJson(_path);
                                GUILayout.EndHorizontal();
                                EditorGUI.FocusTextInControl(null);
                                myTarget.UpdatePrefab();
                                UnityEditor.EditorUtility.SetDirty(EditorAsset);
                                return;
                            }
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();
                        for (int i = 0; i < myTarget.items.Count; i++)
                        {
                            if (EditorAsset.ItemAssets.Count <= i)
                            {
                                EditorAssetData newData = new EditorAssetData();
                                newData.uid = myTarget.items[i].uid;
                                newData.icon = myTarget.items[i].icon;
                                EditorAsset.ItemAssets.Add(newData);
                            }
                            else
                            {
                               if (EditorAsset.ItemAssets[i].icon!= myTarget.items[i].icon && myTarget.items[i].iconLoadMethod== LoadMethod.DirectReference) 
                                    EditorAsset.ItemAssets[i].icon= myTarget.items[i].icon;
                            }

                            myTarget.items[i].uid = i;
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUI.backgroundColor = myTarget.items[i].fold ? _activeColor2 : _disableColor2;
                            GUILayout.Label(myTarget.items[i].fold ? "[-]" : "[+]", GUILayout.Width(20));
                            GUILayout.Box(EditorAsset.ItemAssets[i].icon, GUILayout.Width(20), GUILayout.Height(20));
                            if (GUILayout.Button(new GUIContent(myTarget.items[i].name + " (UID:" + myTarget.items[i].uid.ToString() + ")","Click to expand."), GUILayout.Width(300), GUILayout.Height(20)))
                            {
                                myTarget.items[i].fold = !myTarget.items[i].fold;
                                if (myTarget.items[i].fold) {
                                    for (int u = 0; u < myTarget.items.Count; u++)
                                    {
                                        if(u!=i)myTarget.items[u].fold = false;
                                    }
                                }
                                EditorGUI.FocusTextInControl(null);
                            }
                            if (i > 0)
                            {
                                GUI.backgroundColor = Color.red;
                                if (GUILayout.Button(new GUIContent("X", "Delete this item."), GUILayout.Width(30)))
                                {
                                    for (int w=0;w<myTarget.items.Count;w++) {
                                        for (int v=0;v< myTarget.items[w].craftMaterials.Count;v++) {
                                            if (Mathf.FloorToInt(myTarget.items[w].craftMaterials[v].x) == i)
                                            {
                                                myTarget.items[w].craftMaterials.RemoveAt(v);
                                            } 
                                            else if (myTarget.items[w].craftMaterials[v].x>i) {
                                                myTarget.items[w].craftMaterials[v] = new Vector2(myTarget.items[w].craftMaterials[v].x-1, myTarget.items[w].craftMaterials[v].y);
                                            }
                                        }
                                    }
                                    for (int w = 0; w < myTarget.EnhancingMaterials.Length; w++)
                                    {
                                        if (myTarget.EnhancingMaterials[w].x > i)
                                        {
                                            myTarget.EnhancingMaterials[w] = new Vector2(myTarget.EnhancingMaterials[w].x - 1, myTarget.EnhancingMaterials[w].y);
                                        }
                                    }
                                    if (myTarget.EnchantingMaterial.x>i) {
                                        myTarget.EnchantingMaterial = new Vector2(myTarget.EnchantingMaterial.x-1, myTarget.EnchantingMaterial.y);
                                    }
                                    EditorAsset.ItemAssets.RemoveAt(i);
                                    myTarget.items.RemoveAt(i);
                                    EditorGUI.FocusTextInControl(null);
                                    myTarget.UpdatePrefab();
                                    return;
                                }
                                GUI.backgroundColor = _backgroundColor;
                            }
                            GUILayout.EndHorizontal();
                            GUI.backgroundColor = _backgroundColor;

                            if (myTarget.items[i].fold)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                if (GUILayout.Button(new GUIContent( copyIcon,"Copy the settings of this item"), _toolButtonStyle,GUILayout.Width(25)))
                                {
                                    ClipboardItem = myTarget.items[i].Copy();
                                    ClipboardAsset = EditorAsset.ItemAssets[i].Copy();
                                }
                                if (GUILayout.Button(new GUIContent(pasteIcon, "Paste the settings of this item"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    if (ClipboardItem != null)
                                    {
                                        myTarget.items[i] = ClipboardItem.Copy();
                                        myTarget.items[i].uid = i;
                                        EditorAsset.ItemAssets[i] = ClipboardAsset.Copy();
                                        EditorAsset.ItemAssets[i].uid = i;
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                }
                                if (GUILayout.Button(new GUIContent(resetIcon, "Reset the settings of this item"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    myTarget.items[i] = new Item();
                                    myTarget.items[i].uid = i;
                                    _valueChanged = true;
                                    EditorAsset.ItemAssets[i].icon = null;
                                    EditorGUI.FocusTextInControl(null);
                                }
                                GUILayout.EndHorizontal();

                                myTarget.items[i].favorite = false;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Display Name:", "The name of the item."), GUILayout.Width(130));
                                myTarget.items[i].name = EditorGUILayout.TextField(myTarget.items[i].name);
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Description:", "The text displayed in the detailed information panel. Keep it as short as possible"), GUILayout.Width(130));
                                GUILayout.EndHorizontal();
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                myTarget.items[i].description = EditorGUILayout.TextField(myTarget.items[i].description);
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                EditorGUILayout.HelpBox("Use <br> for new line, use {attribute key} for dynamic value of an attribute, for example: {atk}",MessageType.Info ,true);
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Category: ", "The category to which this item belongs."), GUILayout.Width(130));
                                int _type = Mathf.Clamp(myTarget.items[i].type,0, myTarget.itemTypes.Count-1);
                                GUI.color = myTarget.itemTypes[_type].color;
                                EditorGUI.BeginChangeCheck();
                                _type = EditorGUILayout.Popup("", _type, _typeOptions);
                                if (EditorGUI.EndChangeCheck()) myTarget.items[i].type = _type;
                                GUI.color = Color.white;
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Quality: ", "The quality level of this item."), GUILayout.Width(130));
                                int _quality = Mathf.Clamp(myTarget.items[i].quality,0, myTarget.itemQuality.Count - 1);
                                GUI.color = myTarget.itemQuality[_quality].color;
                                EditorGUI.BeginChangeCheck();
                                _quality = EditorGUILayout.Popup("", _quality, _qualityOptions);
                                if (EditorGUI.EndChangeCheck()) myTarget.items[i].quality = _quality;
                                GUI.color = Color.white;
                                GUILayout.EndHorizontal();

                                EditorGUILayout.Separator();

                                LoadMethod _lastMethod = myTarget.items[i].iconLoadMethod;
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Icon Load Method:", "The icon can be directly referenced or load from Resources/AssetBundle/Custom Method during runtime for better performance."), GUILayout.Width(130));
                                myTarget.items[i].iconLoadMethod = (LoadMethod)EditorGUILayout.EnumPopup(myTarget.items[i].iconLoadMethod,GUILayout.Width(120));
                                GUILayout.EndHorizontal();

                                if (_lastMethod != myTarget.items[i].iconLoadMethod && myTarget.items[i].iconLoadMethod== LoadMethod.DirectReference)
                                {
                                    myTarget.items[i].icon = EditorAsset.ItemAssets[i].icon;
                                }

                                if (myTarget.items[i].iconLoadMethod != LoadMethod.DirectReference)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUILayout.Label(new GUIContent("Icon Path:", "The path of the icon texture."), GUILayout.Width(80));
                                    if (myTarget.items[i].iconLoadMethod == LoadMethod.Resources)
                                    {
                                        GUI.color = _buttonColor;
                                        GUILayout.Label("Resources/", GUILayout.Width(70));
                                        GUI.color = Color.white;
                                    }
                                    if (myTarget.items[i].iconPath == "" || myTarget.items[i].iconPath.Contains(" ")) GUI.color = Color.red;
                                    myTarget.items[i].iconPath = GUILayout.TextField(myTarget.items[i].iconPath);
                                    GUI.color = Color.white;
                                    if (myTarget.items[i].iconLoadMethod == LoadMethod.Custom)
                                    {
                                        GUI.color = _buttonColor;
                                        GUILayout.Label("Format:BundleName#AssetPath", GUILayout.Width(200));
                                        GUI.color = Color.white;
                                    }
                                    GUILayout.EndHorizontal();

                                    if (myTarget.items[i].iconPath == "" || myTarget.items[i].iconPath.Contains(" "))
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        GUI.backgroundColor = Color.red;
                                        EditorGUILayout.HelpBox("You must fill in the path of the icon texture, make sure there is no invalid characters in the path.", MessageType.Info, true);
                                        GUI.backgroundColor = Color.white;
                                        GUILayout.EndHorizontal();
                                    }
                                }

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(90);
                                GUILayout.Label(new GUIContent("Icon: ", "Select the item's icon texture. The icon should be a transparent texture. A variety of icons are available in the \"Assets/SoftKitty/InventoryEngine/Textures\" folder."), GUILayout.Width(80));
                                if (myTarget.items[i].iconLoadMethod == LoadMethod.DirectReference)
                                {
                                    myTarget.items[i].icon = (Texture2D)EditorGUILayout.ObjectField(myTarget.items[i].icon, typeof(Texture2D), false);
                                }
                                else {

                                    EditorAsset.ItemAssets[i].icon = (Texture2D)EditorGUILayout.ObjectField(EditorAsset.ItemAssets[i].icon, typeof(Texture2D), false);
                                    myTarget.items[i].icon = null;
                                    if (myTarget.items[i].iconLoadMethod == LoadMethod.Resources && EditorAsset.ItemAssets[i].icon != null)
                                    {
                                            GUI.backgroundColor = _buttonColor;
                                            if (GUILayout.Button("Apply Path", GUILayout.Width(100)))
                                            {
                                                string _iconPath = AssetDatabase.GetAssetPath(EditorAsset.ItemAssets[i].icon);
                                                int _resourceIndex = _iconPath.IndexOf("Resources");
                                                if (_resourceIndex >= 0)
                                                {
                                                    myTarget.items[i].iconPath = _iconPath.Substring(_resourceIndex + 10, _iconPath.Length - _resourceIndex - 10 - 4);
                                                }
                                                else
                                                {
                                                    EditorUtility.DisplayDialog("Warning", "Sorry, the icon texure is not inside a 'Resources' folder.", "OK");
                                                }
                                            }
                                            GUI.backgroundColor = Color.white;
                                    }
                                }
                                GUILayout.EndHorizontal();

                                if (myTarget.items[i].iconLoadMethod != LoadMethod.DirectReference)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    EditorGUILayout.HelpBox("The icon you selected here is just for editor preview only,it won't be loaded into memory during runtime.", MessageType.Info,true);
                                    GUILayout.EndHorizontal();
                                }
                                EditorGUILayout.Separator();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Maximum Stack:", "The maximum number of items that can stack in a single slot."), GUILayout.Width(130));
                                myTarget.items[i].maxiumStack = EditorGUILayout.IntField(myTarget.items[i].maxiumStack, GUILayout.Width(50));
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Price:", "The trading price and its associated currency type."), GUILayout.Width(130));
                                myTarget.items[i].currency = Mathf.Clamp(myTarget.items[i].currency, 0, myTarget.currencies.Count - 1);
                                myTarget.items[i].price = EditorGUILayout.IntField(myTarget.items[i].price, GUILayout.Width(50));
                                GUI.backgroundColor = myTarget.currencies[myTarget.items[i].currency].color;
                                EditorGUI.BeginChangeCheck();
                                int _currency = EditorGUILayout.Popup("", myTarget.items[i].currency, _currencyOption, GUILayout.Width(100));
                                if (EditorGUI.EndChangeCheck()) myTarget.items[i].currency = _currency;
                                GUI.backgroundColor = _backgroundColor;
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Weight:",
                                    "he weight of this item. The inventory interface includes a weight bar. You can create custom logic to slow down the player's movement or prevent them from moving when they are over-encumbered. Use <InventoryHolder>().GetWeight() to retrieve the current weight value of a character.")
                                    , GUILayout.Width(130));
                                myTarget.items[i].weight = EditorGUILayout.FloatField(myTarget.items[i].weight, GUILayout.Width(50));
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Drop Rates:", "When this item is in a loot pack's item pool, the drop rate determines the percentage chance of it dropping."), GUILayout.Width(130));
                                myTarget.items[i].dropRates = EditorGUILayout.IntSlider(myTarget.items[i].dropRates, 0, 100);
                                GUILayout.Label("%", GUILayout.Width(20));
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Useable:", "Toggle whether this item can be used/equipped via right-click or a key press on an action slot."), GUILayout.Width(130));
                                myTarget.items[i].useable = EditorGUILayout.Toggle(myTarget.items[i].useable);
                                GUILayout.EndHorizontal();

                                if (myTarget.items[i].useable) {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    GUILayout.Label(new GUIContent("Use/Equip restriction:", "Player won't be able to use/equip this item if their stats not fulfill the below setting."), GUILayout.Width(130));
                                    bool _useRestriction = EditorGUILayout.Toggle(myTarget.items[i].restrictionValue > 0 && myTarget.items[i].restrictionKey != "");
                                    GUILayout.EndHorizontal();

                                    
                                    if (_useRestriction)
                                    {
                                        if (myTarget.items[i].restrictionValue < 1) myTarget.items[i].restrictionValue = 1;
                                        if (myTarget.items[i].restrictionKey == "") myTarget.items[i].restrictionKey = myTarget.itemAttributes[0].key;
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        int _sel = 0;
                                        string _attName = "";
                                        for (int x = 0; x < myTarget.itemAttributes.Count; x++)
                                        {
                                            if (myTarget.itemAttributes[x].key == myTarget.items[i].restrictionKey)
                                            {
                                                _sel = x;
                                                _attName = myTarget.itemAttributes[x].name;
                                                break;
                                            }
                                        }
                                        GUI.backgroundColor = _attColor;
                                        EditorGUI.BeginChangeCheck();
                                        _sel = EditorGUILayout.Popup("", _sel, _attOptions, GUILayout.Width(70));
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            myTarget.items[i].restrictionKey = myTarget.itemAttributes[_sel].key;
                                        }
                                        GUILayout.Label(">=", GUILayout.Width(30));
                                        myTarget.items[i].restrictionValue = EditorGUILayout.IntField(myTarget.items[i].restrictionValue, GUILayout.Width(50));
                                        GUILayout.EndHorizontal();
                                        GUI.backgroundColor = Color.white;
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        EditorGUILayout.HelpBox("Player/NPC won't be able to use/equip this item if their "+ _attName +" is less than "+ myTarget.items[i].restrictionValue.ToString(),MessageType.Info,true);
                                        GUILayout.EndHorizontal();
                                    }
                                    else
                                    {
                                        myTarget.items[i].restrictionValue = 0;
                                        myTarget.items[i].restrictionKey = "";
                                    }
                                }

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Consumable:", "Toggle whether this item will be consumed upon use."), GUILayout.Width(130));
                                myTarget.items[i].consumable = EditorGUILayout.Toggle(myTarget.items[i].consumable);
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Tradable:", "Toggle whether this item can be traded between the player and merchants."), GUILayout.Width(130));
                                myTarget.items[i].tradeable = EditorGUILayout.Toggle(myTarget.items[i].tradeable);
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Deletable:", "Toggle whether this item can be deleted by the player."), GUILayout.Width(130));
                                myTarget.items[i].deletable = EditorGUILayout.Toggle(myTarget.items[i].deletable);
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Visible:", "If unchecked, the item is treated as a Hidden Item, which is useful for special items like [Skills]"), GUILayout.Width(130));
                                myTarget.items[i].visible = EditorGUILayout.Toggle(myTarget.items[i].visible);
                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Attributes: (" + myTarget.items[i].attributes.Count + ")", " The attributes of this item. Access the attributes using <InventoryHolder>().GetAttributeValue(key)."), GUILayout.Width(160));
                                GUI.color = Color.white;

                                if (GUILayout.Button(new GUIContent(copyIcon, "Copy the settings of the attributes"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    ClipboardAttributeList = new List<Attribute>();
                                    for (int x=0;x< myTarget.items[i].attributes.Count;x++) {
                                        ClipboardAttributeList.Add(myTarget.items[i].attributes[x].Copy());
                                    }
                                }
                                if (GUILayout.Button(new GUIContent(pasteIcon, "Paste the settings of the attributes"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    if (ClipboardAttributeList.Count>0)
                                    {
                                        myTarget.items[i].attributes.Clear();
                                        for (int y = 0; y < ClipboardAttributeList.Count; y++)
                                        {
                                            myTarget.items[i].attributes.Add(ClipboardAttributeList[y].Copy());
                                        }
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                }
                                if (GUILayout.Button(new GUIContent(resetIcon, "Reset the settings of the attributes"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    myTarget.items[i].attributes.Clear();
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }

                                List<string> _mAttList = new List<string>();
                                int _randomStats = 0;
                                foreach (var obj in myTarget.items[i].attributes) {
                                    if (!_mAttList.Contains(obj.key)) _mAttList.Add(obj.key);
                                    if (!obj.isFixed) _randomStats ++;
                                }
                                GUILayout.EndHorizontal();

                                List<string> _attOptionsList = new List<string>();
                              
                                foreach (var obj in myTarget.itemAttributes)
                                {
                                    if (!_mAttList.Contains(obj.key)) _attOptionsList.Add(obj.key);
                                }

                                if (_randomStats>0)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUILayout.Label(new GUIContent("Maximum non-static attributes: ", "The maximum number of non-static extra attributes this item can have."), GUILayout.Width(200));
                                    myTarget.items[i].maximumRandomAttributes = EditorGUILayout.IntSlider(myTarget.items[i].maximumRandomAttributes, 1, _randomStats, GUILayout.Width(160));
                                    GUILayout.Label("/"+ _randomStats.ToString(), GUILayout.Width(30));
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    EditorGUILayout.HelpBox("When an item is generated, it's assigned its static attributes, as defined by the 'Static' checkbox. " +
                                        "In addition, it may receive a random number of dynamic, non-static attributes. The chance of receiving each non-static attribute is configurable. " +
                                        "The specific values of these dynamic attributes are then randomized between the minimum and maximum values you set.", MessageType.Info, true);
                                    GUILayout.EndHorizontal();
                                }

                                if (_attOptionsList.Count > 0)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUI.color = _buttonColor;
                                    GUILayout.Label("New: ", GUILayout.Width(40));
                                    GUI.color = Color.white;
                                    List<string> _allAtt = new List<string>(_attOptions);
                                    _newAttSel = Mathf.Clamp(_newAttSel, 0, _attOptionsList.Count-1);
                                    GUI.backgroundColor = _attColor;
                                    _newAttSel = EditorGUILayout.Popup("", _newAttSel, _attOptionsList.ToArray(), GUILayout.Width(100));
                                    GUI.backgroundColor = _buttonColor;
                                    if (GUILayout.Button("Add", GUILayout.Width(80)))
                                    {
                                        Attribute _newAttribute = myTarget.itemAttributes[_allAtt.IndexOf(_attOptionsList[_newAttSel])].Copy();
                                        if (_newAttribute.stringValue)
                                            _newAttribute.value = "";
                                        else
                                            _newAttribute.value = "0";
                                        myTarget.items[i].attributes.Add(_newAttribute);
                                        EditorGUI.FocusTextInControl(null);
                                        _valueChanged = true;
                                    }
                                    GUI.backgroundColor = Color.white;
                                    GUILayout.EndHorizontal();
                                }

                               

                                for (int u = 0; u < myTarget.items[i].attributes.Count; u++)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);

                                    GUILayout.Box(myTarget.items[i].attributes[u].visible ? visibleIcon0 : visibleIcon1, GUILayout.Width(20), GUILayout.Height(20));

                                    GUI.color = _attColor;
                                    GUILayout.Box(new GUIContent( myTarget.items[i].attributes[u].name,"Script Key: "+ myTarget.items[i].attributes[u].key), _titleButtonStyle, GUILayout.Width(100));
                                    GUI.color = Color.white;
                                    GUILayout.Label(" :", GUILayout.Width(15));
                                    if (myTarget.items[i].attributes[u].stringValue)
                                    {
                                        myTarget.items[i].attributes[u].value = EditorGUILayout.TextField(myTarget.items[i].attributes[u].value, GUILayout.Width(122)).ToString();
                                    }
                                    else if (myTarget.items[i].attributes[u].isFixed)
                                    {
                                        myTarget.items[i].attributes[u].value = EditorGUILayout.FloatField(float.Parse(myTarget.items[i].attributes[u].value), GUILayout.Width(122)).ToString();
                                        myTarget.items[i].attributes[u].minValue = float.Parse(myTarget.items[i].attributes[u].value);
                                        myTarget.items[i].attributes[u].maxValue = float.Parse(myTarget.items[i].attributes[u].value);
                                    }
                                    else
                                    {
                                        myTarget.items[i].attributes[u].minValue = EditorGUILayout.FloatField(myTarget.items[i].attributes[u].minValue, GUILayout.Width(50));
                                        GUILayout.Label("~",GUILayout.Width(15));
                                        myTarget.items[i].attributes[u].maxValue = EditorGUILayout.FloatField(myTarget.items[i].attributes[u].maxValue, GUILayout.Width(50));
                                        myTarget.items[i].attributes[u].value = myTarget.items[i].attributes[u].minValue.ToString();
                                    }
                                    
                                    GUI.backgroundColor = Color.white;
                                    myTarget.items[i].attributes[u].isFixed = GUILayout.Toggle(myTarget.items[i].attributes[u].isFixed, new GUIContent("Static", "Whether this attribute is Static, uncheck it for rogue-like games to have random stats for this item."), GUILayout.Width(55));

                                    GUI.color = u > 0 ? Color.white : Color.gray;
                                    if (GUILayout.Button(new GUIContent(upIcon, "Move this attribute up."), _toolButtonStyle, GUILayout.Width(15)))
                                    {
                                        if (u > 0)
                                        {
                                            Attribute _mAtt = myTarget.items[i].attributes[u].Copy();
                                            myTarget.items[i].attributes.RemoveAt(u);
                                            myTarget.items[i].attributes.Insert(u-1,_mAtt);
                                            EditorGUI.FocusTextInControl(null);
                                            myTarget.UpdatePrefab();
                                            return;
                                        }
                                    }
                                    GUI.color = u < myTarget.items[i].attributes.Count-1 ? Color.white : Color.gray;
                                    if (GUILayout.Button(new GUIContent(downIcon, "Move this attribute down."), _toolButtonStyle,GUILayout.Width(15)))
                                    {
                                        if (u < myTarget.items[i].attributes.Count - 1)
                                        {
                                            Attribute _mAtt = myTarget.items[i].attributes[u].Copy();
                                            myTarget.items[i].attributes.RemoveAt(u);
                                            myTarget.items[i].attributes.Insert(u + 1, _mAtt);
                                            EditorGUI.FocusTextInControl(null);
                                            myTarget.UpdatePrefab();
                                            return;
                                        }
                                    }

                                    GUI.backgroundColor = Color.red;
                                    GUI.color = Color.white;
                                    if (GUILayout.Button(new GUIContent("X","Delete this attribute."), GUILayout.Width(30)))
                                    {
                                        myTarget.items[i].attributes.RemoveAt(u);
                                        EditorGUI.FocusTextInControl(null);
                                        myTarget.UpdatePrefab();
                                        return;
                                    }
                                    GUI.backgroundColor = _backgroundColor;

                                    GUILayout.EndHorizontal();

                                    if (!myTarget.items[i].attributes[u].isFixed)
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(110);
                                        myTarget.items[i].attributes[u].randomChange = Mathf.FloorToInt(GUILayout.HorizontalSlider(myTarget.items[i].attributes[u].randomChange, 1, 100, GUILayout.Width(60)));
                                        GUILayout.Label(myTarget.items[i].attributes[u].randomChange.ToString() + "% chance to have this extra attribute.", GUILayout.Width(250));
                                        GUILayout.EndHorizontal();
                                    }
                                }

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Use Actions: (" + myTarget.items[i].actions.Count + ")",
                                    "When this item is used, the commands in this list are sent to all registered callbacks. Register callbacks using ItemManager.PlayerInventoryHolder.RegisterItemUseCallback().")
                                    , GUILayout.Width(130));
                                GUI.backgroundColor = _buttonColor;
                                if (GUILayout.Button(new GUIContent("+","Create a new action command."), GUILayout.Width(30)))
                                {
                                    myTarget.items[i].actions.Add("New Action" + myTarget.items[i].actions.Count.ToString());
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }
                                GUI.backgroundColor = _backgroundColor;

                                if (GUILayout.Button(new GUIContent(copyIcon, "Copy the settings of the actions"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    ClipboardStringList = myTarget.items[i].actions;
                                }
                                if (GUILayout.Button(new GUIContent(pasteIcon, "Paste the settings of the actions"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    if (ClipboardStringList.Count>0)
                                    {
                                        myTarget.items[i].actions.Clear();
                                        for (int y = 0; y < ClipboardStringList.Count; y++)
                                        {
                                            myTarget.items[i].actions.Add(ClipboardStringList[y]);
                                        }
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                }
                                if (GUILayout.Button(new GUIContent(resetIcon, "Reset the settings of the actions"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    myTarget.items[i].actions.Clear();
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }

                                GUILayout.EndHorizontal();

                                for (int u = 0; u < myTarget.items[i].actions.Count; u++) {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUI.backgroundColor = _actionColor;
                                    GUILayout.Label(">", GUILayout.Width(15));
                                    myTarget.items[i].actions[u] = EditorGUILayout.TextField(myTarget.items[i].actions[u], GUILayout.Width(180));
                                    GUI.backgroundColor = Color.red;
                                    GUI.color = Color.white;
                                    if (GUILayout.Button(new GUIContent("X","Delete this action command."), GUILayout.Width(30)))
                                    {
                                        myTarget.items[i].actions.RemoveAt(u);
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                        myTarget.UpdatePrefab();
                                        return;
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();
                                }

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Tags: (" + myTarget.items[i].tags.Count + ")", 
                                    "Tags are useful for providing additional definitions to items. For example, for equipment, tags can define the slot it belongs to (Head, Torso, Legs, etc.), whether a weapon is two-handed or one-handed, or if an item is a crafting blueprint.")
                                    , GUILayout.Width(130));
                                GUI.backgroundColor = _buttonColor;
                                if (GUILayout.Button(new GUIContent("+","Create a new tag."), GUILayout.Width(30)))
                                {
                                    myTarget.items[i].tags.Add("New Tag" + myTarget.items[i].tags.Count.ToString());
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }
                                GUI.backgroundColor = _backgroundColor;

                                if (GUILayout.Button(new GUIContent(copyIcon, "Copy the settings of the tags"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    ClipboardStringList = myTarget.items[i].tags;
                                }
                                if (GUILayout.Button(new GUIContent(pasteIcon, "Paste the settings of the tags"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    if (ClipboardStringList.Count > 0)
                                    {
                                        myTarget.items[i].tags.Clear();
                                        for (int y = 0; y < ClipboardStringList.Count; y++)
                                        {
                                            myTarget.items[i].tags.Add(ClipboardStringList[y]);
                                        }
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                }
                                if (GUILayout.Button(new GUIContent(resetIcon, "Reset the settings of the tags"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    myTarget.items[i].tags.Clear();
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }

                                GUILayout.EndHorizontal();

                                for (int u = 0; u < myTarget.items[i].tags.Count; u++)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUI.backgroundColor = _tagColor;
                                    GUILayout.Label(">", GUILayout.Width(15));
                                    myTarget.items[i].tags[u] = EditorGUILayout.TextField(myTarget.items[i].tags[u], GUILayout.Width(180));
                                    GUI.backgroundColor = Color.red;
                                    GUI.color = Color.white;
                                    if (GUILayout.Button(new GUIContent("X","Delete this tag."), GUILayout.Width(30)))
                                    {
                                        myTarget.items[i].tags.RemoveAt(u);
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                        myTarget.UpdatePrefab();
                                        return;
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();
                                }

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Crafting Materials: (" + myTarget.items[i].craftMaterials.Count + ")", "Specify the items required as materials when crafting this item."), GUILayout.Width(130));
                                if (myTarget.items[i].craftMaterials.Count < 4)
                                {
                                    GUI.backgroundColor = _buttonColor;
                                    if (GUILayout.Button(new GUIContent("+","Add a new crafting material"), GUILayout.Width(30)))
                                    {
                                        myTarget.items[i].craftMaterials.Add(new Vector2(0, 1));
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                }

                                if (GUILayout.Button(new GUIContent(copyIcon, "Copy the settings of the materials"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    ClipboardVector2List = myTarget.items[i].craftMaterials;
                                }
                                if (GUILayout.Button(new GUIContent(pasteIcon, "Paste the settings of the materials"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    if (ClipboardVector2List.Count > 0)
                                    {
                                        myTarget.items[i].craftMaterials.Clear();
                                        for (int y = 0; y < ClipboardVector2List.Count; y++)
                                        {
                                            myTarget.items[i].craftMaterials.Add(ClipboardVector2List[y]);
                                        }
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                }
                                if (GUILayout.Button(new GUIContent(resetIcon, "Reset the settings of the materials"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    myTarget.items[i].craftMaterials.Clear();
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }

                                GUILayout.EndHorizontal();

                                for (int u = 0; u < myTarget.items[i].craftMaterials.Count; u++)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUI.backgroundColor = _matColor;
                                    GUILayout.Label(">", GUILayout.Width(15));
                                    
                                    int _mat = Mathf.Clamp( Mathf.FloorToInt(myTarget.items[i].craftMaterials[u].x),0, myTarget.items.Count-1);
                                    
                                    GUILayout.Box(EditorAsset.ItemAssets[_mat].icon, GUILayout.Width(20), GUILayout.Height(20));
                                    EditorGUI.BeginChangeCheck();
                                    _mat = EditorGUILayout.Popup("", _mat, _itemOption, GUILayout.Width(180), GUILayout.Height(20));
                                    if (EditorGUI.EndChangeCheck()) myTarget.items[i].craftMaterials[u] = new Vector2(_mat, myTarget.items[i].craftMaterials[u].y);
                                    GUILayout.Label("x", GUILayout.Width(30), GUILayout.Height(20));
                                    myTarget.items[i].craftMaterials[u] =new Vector2(myTarget.items[i].craftMaterials[u].x, EditorGUILayout.IntField(Mathf.FloorToInt(myTarget.items[i].craftMaterials[u].y), GUILayout.Width(70), GUILayout.Height(20)));
                                    GUI.backgroundColor = Color.red;
                                    GUI.color = Color.white;
                                    if (GUILayout.Button(new GUIContent("X","Remove this crafting material."), GUILayout.Width(30)))
                                    {
                                        myTarget.items[i].craftMaterials.RemoveAt(u);
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();
                                }

                                if (myTarget.EnableSocketing && myTarget.items[i].type==myTarget.SocketedCategoryFilter)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    GUILayout.Label(new GUIContent("Socketing Tags: (" + myTarget.items[i].socketingTag.Count + ")", "This item will only recieve skocketing items with any tag in this list."), GUILayout.Width(130));
                                    GUI.backgroundColor = _buttonColor;
                                    if (GUILayout.Button(new GUIContent("+", "Add a new socketing tag filter"), GUILayout.Width(30)))
                                    {
                                        myTarget.items[i].socketingTag.Add("");
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                    GUI.backgroundColor = _backgroundColor;

                                    if (GUILayout.Button(new GUIContent(copyIcon, "Copy the settings of the tag filter"), _toolButtonStyle, GUILayout.Width(25)))
                                    {
                                        ClipboardStringList = myTarget.items[i].socketingTag;
                                    }
                                    if (GUILayout.Button(new GUIContent(pasteIcon, "Paste the settings of the tag filter"), _toolButtonStyle, GUILayout.Width(25)))
                                    {
                                        if (ClipboardStringList.Count > 0)
                                        {
                                            myTarget.items[i].socketingTag.Clear();
                                            for (int y = 0; y < ClipboardStringList.Count; y++)
                                            {
                                                myTarget.items[i].socketingTag.Add(ClipboardStringList[y]);
                                            }
                                            _valueChanged = true;
                                            EditorGUI.FocusTextInControl(null);
                                        }
                                    }
                                    if (GUILayout.Button(new GUIContent(resetIcon, "Reset the settings of the tag filter"), _toolButtonStyle, GUILayout.Width(25)))
                                    {
                                        myTarget.items[i].socketingTag.Clear();
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }

                                    GUILayout.EndHorizontal();

                                    for (int u = 0; u < myTarget.items[i].socketingTag.Count; u++) {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        GUI.backgroundColor = _tagColor;
                                        GUILayout.Label(">", GUILayout.Width(15));
                                        myTarget.items[i].socketingTag[u] = EditorGUILayout.TextField(myTarget.items[i].socketingTag[u], GUILayout.Width(180));
                                        GUI.backgroundColor = Color.red;
                                        GUI.color = Color.white;
                                        if (GUILayout.Button(new GUIContent("X", "Delete this socketing tag filter."), GUILayout.Width(30)))
                                        {
                                            myTarget.items[i].socketingTag.RemoveAt(u);
                                            _valueChanged = true;
                                            EditorGUI.FocusTextInControl(null);
                                            myTarget.UpdatePrefab();
                                            return;
                                        }
                                        GUI.backgroundColor = _backgroundColor;
                                        GUILayout.EndHorizontal();
                                    }

                                   
                                }

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUILayout.Label(new GUIContent("Custom Data: (" + myTarget.items[i].customData.Count + ")", "Setup custom data for audio clips,prefabs,images,etc"), GUILayout.Width(130));
                                if (myTarget.items[i].customData.Count < 4)
                                {
                                    GUI.backgroundColor = _buttonColor;
                                    if (GUILayout.Button(new GUIContent("+", "Add a new custom data"), GUILayout.Width(30)))
                                    {
                                        myTarget.items[i].customData.Add(new CustomField() { key= "Custom" + myTarget.items[i].customData.Count.ToString(),value = null });
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                }
                                if (GUILayout.Button(new GUIContent(resetIcon, "Reset the settings of the custom data"), _toolButtonStyle, GUILayout.Width(25)))
                                {
                                    myTarget.items[i].customData.Clear();
                                    _valueChanged = true;
                                    EditorGUI.FocusTextInControl(null);
                                }
                                GUILayout.EndHorizontal();
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                EditorGUILayout.HelpBox("You can attach custom data of any Object type to items, identified by unique string keys.\n" +
                                    "Use the item.GetCustomData(string _key) function to retrieve this data at any time.", MessageType.Info, true);
                                GUILayout.EndHorizontal();

                                for (int u = 0; u < myTarget.items[i].customData.Count; u++)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUI.backgroundColor = _matColor;
                                    GUILayout.Label(">", GUILayout.Width(15));
                                    GUILayout.Label("Key:",GUILayout.Width(40));
                                    myTarget.items[i].customData[u].key = GUILayout.TextField(myTarget.items[i].customData[u].key,GUILayout.Width(80));

                                    GUILayout.Label("Load Method:", GUILayout.Width(90));
                                    myTarget.items[i].customData[u].loadMethod = (LoadMethod)EditorGUILayout.EnumPopup(myTarget.items[i].customData[u].loadMethod,GUILayout.Width(120));
                                    GUI.backgroundColor = Color.red;
                                    GUI.color = Color.white;
                                    if (GUILayout.Button(new GUIContent("X", "Remove this custom data."), GUILayout.Width(30)))
                                    {
                                        myTarget.items[i].customData.RemoveAt(u);
                                        _valueChanged = true;
                                        EditorGUI.FocusTextInControl(null);
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();

                                    if (u< myTarget.items[i].customData.Count) {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(110);
                                        if (myTarget.items[i].customData[u].loadMethod == LoadMethod.DirectReference)
                                        {
                                            GUILayout.Label("Data:", GUILayout.Width(40));
                                            myTarget.items[i].customData[u].value = EditorGUILayout.ObjectField(myTarget.items[i].customData[u].value, typeof(Object), false, GUILayout.Width(150));
                                        }
                                        else
                                        {
                                            myTarget.items[i].customData[u].value = null;
                                            GUILayout.Label("Data Path:", GUILayout.Width(70));
                                            if (myTarget.items[i].customData[u].loadMethod == LoadMethod.Resources)
                                            {
                                                GUI.color = _buttonColor;
                                                GUILayout.Label("Resources/", GUILayout.Width(70));
                                                GUI.color = Color.white;
                                            }
                                            if (myTarget.items[i].customData[u].loadPath == "" || myTarget.items[i].customData[u].loadPath.Contains(" ")) GUI.color = Color.red;
                                            myTarget.items[i].customData[u].loadPath = GUILayout.TextField(myTarget.items[i].customData[u].loadPath);
                                            GUI.color = Color.white;
                                            if (myTarget.items[i].customData[u].loadMethod == LoadMethod.Custom)
                                            {
                                                GUI.color = _buttonColor;
                                                GUILayout.Label("Format:BundleName#AssetPath", GUILayout.Width(200));
                                                GUI.color = Color.white;
                                            }
                                        }
                                        GUILayout.EndHorizontal();

                                        if (myTarget.items[i].customData[u].loadMethod != LoadMethod.DirectReference && (myTarget.items[i].customData[u].loadPath == "" || myTarget.items[i].customData[u].loadPath.Contains(" ")))
                                        {
                                            GUILayout.BeginHorizontal();
                                            GUILayout.Space(110);
                                            GUI.backgroundColor = Color.red;
                                            EditorGUILayout.HelpBox("You must fill in the path of the data object, make sure there is no invalid characters in the path.", MessageType.Info, true);
                                            GUI.backgroundColor = Color.white;
                                            GUILayout.EndHorizontal();
                                        }
                                    }
                                }

                                EditorGUILayout.Separator();

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUI.color = new Color(0F,0.7F,1F,1F);
                                GUILayout.Label(new GUIContent("[Master Character Creator] Appearnace:", "Specify the appearance mesh of this item."), GUILayout.Width(250));
                                GUI.color = Color.white;
                                GUILayout.EndHorizontal();

#if MASTER_CHARACTER_CREATOR
                                
                                if (MccData==null)
                                {
                                    GameObject _mccDataObj = Resources.Load<GameObject>("MasterCharacterCreator/Core/CharacterData");
                                    MccData = _mccDataObj.GetComponent<MasterCharacterCreator.CharacterDataSetting>();
                                }
                                

                                if (myTarget.items[i].equipAppearance == null)
                                {
                                    myTarget.items[i].equipAppearance = new MasterCharacterCreator.EquipmentAppearance();
                                    myTarget.items[i].equipAppearance.MaleMeshId = 0;
                                    myTarget.items[i].equipAppearance.FemaleMeshId = 0;
                                    myTarget.items[i].equipAppearance.UseCustomColor = false;
                                }
                                if (myTarget.items[i].equipAppearance.MaleMeshId == 0 && myTarget.items[i].equipAppearance.FemaleMeshId == 0)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUI.backgroundColor = _buttonColor;
                                    if (GUILayout.Button("Create Mesh Binding", GUILayout.Width(150)))
                                    {
                                        myTarget.items[i].equipAppearance.MaleMeshId = 1;
                                        myTarget.items[i].equipAppearance.FemaleMeshId = 1;
                                        myTarget.items[i].equipAppearance.UseCustomColor = false;
                                        myTarget.items[i].equipAppearance.CustomColor1 = Color.white;
                                        myTarget.items[i].equipAppearance.CustomColor2 = Color.white;
                                        myTarget.items[i].equipAppearance.CustomColor3 = Color.white;
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();
                                }
                                else
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(90);
                                    GUI.backgroundColor = Color.red;
                                    if (GUILayout.Button("Remove Mesh Binding", GUILayout.Width(150)))
                                    {
                                        myTarget.items[i].equipAppearance.MaleMeshId = 0;
                                        myTarget.items[i].equipAppearance.FemaleMeshId = 0;
                                        myTarget.items[i].equipAppearance.UseCustomColor = false;
                                        if (MeshPreview.instance != null) MeshPreview.instance.Close();
                                        return;
                                    }
                                    GUI.backgroundColor = _backgroundColor;
                                    GUILayout.EndHorizontal();
                                    if (MccData == null)
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(80);
                                        GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                                        GUI.color = Color.red;
                                        GUILayout.Label("Please drag out the CharacterData prefab from MCC prefabs folder.");
                                        GUI.color = Color.white;
                                        GUILayout.EndHorizontal();
                                    }
                                    else
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        GUILayout.Label("Slot:", GUILayout.Width(120));
                                        EditorGUI.BeginChangeCheck();
                                        int _sel = (int)myTarget.items[i].equipAppearance.Type;
                                        _sel = EditorGUILayout.Popup("", _sel, _outfitSlotsOption, GUILayout.Width(100));
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            myTarget.items[i].equipAppearance.Type = (MasterCharacterCreator.OutfitSlots)_sel;
                                            myTarget.items[i].equipAppearance.MaleMeshId = 1;
                                            myTarget.items[i].equipAppearance.FemaleMeshId = 1;
                                            if (MeshPreview.instance != null) MeshPreview.instance.Close();
                                        }
                                        GUILayout.EndHorizontal();

                                        MasterCharacterCreator.OutfitInfo _outfitInfoM = MccData.GetOutfitSettings(MasterCharacterCreator.Sex.Male, myTarget.items[i].equipAppearance.Type)[myTarget.items[i].equipAppearance.MaleMeshId];
                                        MasterCharacterCreator.OutfitInfo _outfitInfoF = MccData.GetOutfitSettings(MasterCharacterCreator.Sex.Female, myTarget.items[i].equipAppearance.Type)[myTarget.items[i].equipAppearance.FemaleMeshId];

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        GUILayout.Label("Male Mesh id:", GUILayout.Width(120));
                                        GUI.backgroundColor = Color.green;
                                        if (GUILayout.Button("Preview",GUILayout.Width(100)))
                                        {
                                            if(MeshPreview.instance!=null) MeshPreview.instance.Close();
                                            MeshPreview.ShowPreview(_outfitInfoM.MeshPath, _outfitInfoM.MaterialPath, _thePath.Replace("Editor/", "Prefabs/PreviewModel.prefab"));
                                        }
                                        GUI.backgroundColor = _backgroundColor;
                                        GUILayout.EndHorizontal();
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(120);
                                        int _maleId = myTarget.items[i].equipAppearance.MaleMeshId;
                                        myTarget.items[i].equipAppearance.MaleMeshId=EditorGUILayout.IntSlider(myTarget.items[i].equipAppearance.MaleMeshId, 1, _outfitSlotsCountM[_sel] -1, GUILayout.Width(200));
                                        GUILayout.Label(_outfitInfoM.DisplayName);
                                        GUILayout.EndHorizontal();
                                        if (MeshPreview.instance != null && _maleId != myTarget.items[i].equipAppearance.MaleMeshId)
                                        {
                                            if (MeshPreview.instance != null) MeshPreview.instance.Close();
                                            _outfitInfoM = MccData.GetOutfitSettings(MasterCharacterCreator.Sex.Male, myTarget.items[i].equipAppearance.Type)[myTarget.items[i].equipAppearance.MaleMeshId];
                                            MeshPreview.ShowPreview(_outfitInfoM.MeshPath, _outfitInfoM.MaterialPath, _thePath.Replace("Editor/", "Prefabs/PreviewModel.prefab"));
                                        }

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        GUILayout.Label("Female Mesh id:", GUILayout.Width(120));
                                        GUI.backgroundColor = Color.green;
                                        if (GUILayout.Button("Preview", GUILayout.Width(100)))
                                        {
                                            if (MeshPreview.instance != null) MeshPreview.instance.Close();
                                            MeshPreview.ShowPreview(_outfitInfoF.MeshPath, _outfitInfoF.MaterialPath, _thePath.Replace("Editor/", "Prefabs/PreviewModel.prefab"));
                                        }
                                        GUI.backgroundColor = _backgroundColor;
                                        GUILayout.EndHorizontal();
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(120);
                                        int _femaleId = myTarget.items[i].equipAppearance.FemaleMeshId;
                                        myTarget.items[i].equipAppearance.FemaleMeshId = EditorGUILayout.IntSlider(myTarget.items[i].equipAppearance.FemaleMeshId, 1, _outfitSlotsCountF[_sel] - 1, GUILayout.Width(200));
                                        GUILayout.Label(_outfitInfoF.DisplayName);
                                        GUILayout.EndHorizontal();

                                        if (MeshPreview.instance != null && _femaleId != myTarget.items[i].equipAppearance.FemaleMeshId)
                                        {
                                            if (MeshPreview.instance != null) MeshPreview.instance.Close();
                                            _outfitInfoF = MccData.GetOutfitSettings(MasterCharacterCreator.Sex.Female, myTarget.items[i].equipAppearance.Type)[myTarget.items[i].equipAppearance.FemaleMeshId];
                                            MeshPreview.ShowPreview(_outfitInfoF.MeshPath, _outfitInfoF.MaterialPath, _thePath.Replace("Editor/", "Prefabs/PreviewModel.prefab"));
                                        }

                                        bool _useCustomColor = myTarget.items[i].equipAppearance.UseCustomColor;
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Space(90);
                                        GUILayout.Label("Custom Color:", GUILayout.Width(120));
                                        myTarget.items[i].equipAppearance.UseCustomColor = GUILayout.Toggle(myTarget.items[i].equipAppearance.UseCustomColor,"",GUILayout.Width(30));
                                        GUILayout.EndHorizontal();




                                        if (myTarget.items[i].equipAppearance.UseCustomColor)
                                        {
                                            Color _color1 = myTarget.items[i].equipAppearance.CustomColor1;
                                            Color _color2 = myTarget.items[i].equipAppearance.CustomColor2;
                                            Color _color3 = myTarget.items[i].equipAppearance.CustomColor3;
                                            if (!_useCustomColor)
                                            {
                                                MasterCharacterCreator.OutfitColorSetting _colorSetting = MccData.GetOutfitColorSetting(
                                                    MasterCharacterCreator.Sex.Male, myTarget.items[i].equipAppearance.Type, myTarget.items[i].equipAppearance.MaleMeshId);
                                                myTarget.items[i].equipAppearance.CustomColor1 = _colorSetting.DefaultColor1;
                                                myTarget.items[i].equipAppearance.CustomColor2 = _colorSetting.DefaultColor2;
                                                myTarget.items[i].equipAppearance.CustomColor3 = _colorSetting.DefaultColor3;
                                            }
                                            GUILayout.BeginHorizontal();
                                            GUILayout.Space(90);
                                            GUILayout.Label("#1", GUILayout.Width(20));
                                            myTarget.items[i].equipAppearance.CustomColor1 = EditorGUILayout.ColorField(myTarget.items[i].equipAppearance.CustomColor1, GUILayout.Width(50));
                                            GUILayout.Space(10);
                                            GUILayout.Label("#2", GUILayout.Width(20));
                                            myTarget.items[i].equipAppearance.CustomColor2 = EditorGUILayout.ColorField(myTarget.items[i].equipAppearance.CustomColor2, GUILayout.Width(50));
                                            GUILayout.Space(10);
                                            GUILayout.Label("#3", GUILayout.Width(20));
                                            myTarget.items[i].equipAppearance.CustomColor3 = EditorGUILayout.ColorField(myTarget.items[i].equipAppearance.CustomColor3, GUILayout.Width(50));
                                            GUILayout.EndHorizontal();

                                            if (MeshPreview.renderer != null
                                                && (_color1 != myTarget.items[i].equipAppearance.CustomColor1
                                                || _color2 != myTarget.items[i].equipAppearance.CustomColor2
                                                || _color3 != myTarget.items[i].equipAppearance.CustomColor3))
                                            {
                                                MeshPreview.renderer.sharedMaterial.SetColor("_Color1", myTarget.items[i].equipAppearance.CustomColor1);
                                                MeshPreview.renderer.sharedMaterial.SetColor("_Color2", myTarget.items[i].equipAppearance.CustomColor2);
                                                MeshPreview.renderer.sharedMaterial.SetColor("_Color3", myTarget.items[i].equipAppearance.CustomColor3);
                                            }
                                        }
                                        
                                    }
                                }


#else
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(80);
                                GUILayout.Box(warningIcon, GUIStyle.none, GUILayout.Width(20));
                                GUILayout.Label("[Master Character Creator] is not installed.", GUILayout.Width(250));
                                GUILayout.EndHorizontal();
#endif

                            }

                        }
                        //===Item List

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(80);
                        GUI.backgroundColor = _buttonColor;
                        if (GUILayout.Button(new GUIContent("Add new [Item]","Create a new item."), GUILayout.Width(300)))
                        {
                            Item _newItem = new Item();
                            if (myTarget.items.Count < 1)
                            {
                                _newItem.name = "New Item" + myTarget.items.Count.ToString();
                                _newItem.description = "No description yet.";
                                _newItem.type = 0;
                                _newItem.quality = 0;
                                _newItem.icon = null;
                                _newItem.maxiumStack = 99;
                                _newItem.price = 0;
                                _newItem.upgradeLevel = 0;
                                _newItem.useable = false;
                                _newItem.consumable = false;
                                _newItem.tradeable = true;
                                _newItem.deletable = true;
                                _newItem.attributes.Clear();
                                _newItem.enchantments.Clear();
                                _newItem.actions.Clear();
                                _newItem.craftMaterials.Clear();
                                _newItem.tags.Clear();
                                _newItem.customData.Clear();
                            }
                            else
                            {
                                _newItem = myTarget.items[myTarget.items.Count - 1].Copy();
                            }
                            _newItem.uid = myTarget.items.Count;
                            _newItem.fold = true;

                            for (int u = 0; u < myTarget.items.Count; u++)
                            {
                                myTarget.items[u].fold = false;
                            }

                            myTarget.items.Add(_newItem);
                            _valueChanged = true;
                            EditorGUI.FocusTextInControl(null);
                        }
                        GUI.backgroundColor = _tagColor;
                        GUILayout.EndHorizontal();
                    }


                }
                
                if ((_valueChanged ) && !Application.isPlaying)
                {
                    myTarget.UpdatePrefab();
                    UnityEditor.EditorUtility.SetDirty(EditorAsset);
                }
            }
        }


    }
}
