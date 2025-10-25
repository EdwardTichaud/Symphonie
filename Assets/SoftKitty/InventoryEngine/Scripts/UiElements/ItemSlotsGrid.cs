using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class ItemSlotsGrid : MonoBehaviour
    {

        
        void Awake()
        {
            if (GetComponent<GridLayoutGroup>() && InventorySkin.instance != null)
            {
                GetComponent<GridLayoutGroup>().cellSize *= InventorySkin.instance.InventorySlotScale;
            }
        }

        
    }
}
