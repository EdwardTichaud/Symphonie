using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class VictoryPanelManager : MonoBehaviour
{
    public TextMeshProUGUI xpText;
    public TextMeshProUGUI itemsText;

    void Awake()
    {
        if (xpText == null)
        {
            xpText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("xp"));
        }

        if (itemsText == null)
        {
            itemsText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("item"));
        }
    }

    public void DisplayRewards(int xp, List<ItemData> items)
    {
        if (xpText != null)
            xpText.text = $"+{xp} XP";

        if (itemsText != null)
            itemsText.text = items.Count > 0
                ? string.Join(", ", items.Select(i => i.itemName))
                : "";
    }
}
