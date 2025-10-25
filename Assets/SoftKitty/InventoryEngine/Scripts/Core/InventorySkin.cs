using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty.InventoryEngine
{
    public class InventorySkin : MonoBehaviour
    {

        #region Variables
        public static InventorySkin instance;
        public int UiStyle = 1;
        public float UiScale = 1F;
        public float InventorySlotScale = 1F;
        public Color EmptyItemBackColor = new Color(0.33F,0.33F,0.33F,1F);
        public Color ItemSelectedColor = new Color(1F,0.28F,0F,0.4F);
        public Color ItemHoverColor= new Color(1F, 0.54F, 0F, 0.1F);
        public Color FavoriteColor = new Color(1F,0.54F,0F,1F);
        #endregion

        #region Internal Methods
        private void Awake()
        {
            instance = this;
        }

        public void UpdatePrefab()
        {
         #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
         #endif
        }
        #endregion
    }
}
