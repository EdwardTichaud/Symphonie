using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty.InventoryEngine
{
    [System.Serializable]
    public class EditorAssetData
    {
        public int uid;
        public Texture2D icon;
        public EditorAssetData Copy()
        {
            EditorAssetData _copy = new EditorAssetData();
            _copy.uid = uid;
            _copy.icon = icon;
            return _copy;
        }
    }
    //[CreateAssetMenu(fileName = "ItemEditorAssets", menuName = "SoftKitty/InventoryEngine/ItemEditorAssets")]
    public class ItemEditorAssets : ScriptableObject
    {
        public List<EditorAssetData> ItemAssets = new List<EditorAssetData>();


    }
}
