using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty.InventoryEngine
{
    public class PlayerStats : MonoBehaviour
    {
        private IEnumerator Start()
        {
            while (ItemManager.PlayerEquipmentHolder == null) yield return 1;
            yield return new WaitForSecondsRealtime(0.5F);
            GetComponentInChildren<StatsUi>(true).Init(ItemManager.PlayerEquipmentHolder);
        }
    }
}
