using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VictoryPanelManager : MonoBehaviour
{
    public Image xpIcon;
    public TextMeshProUGUI xpText;
    public Transform itemsContainer;
    public TextMeshProUGUI enemiesText;
    public TextMeshProUGUI maxDamageText;
    public Image mvpImage;
    public TextMeshProUGUI timeText;

    void Awake()
    {
        if (xpText == null)
        {
            xpText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("xp"));
        }
        if (xpIcon == null)
        {
            xpIcon = GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.name.ToLower().Contains("xpicon"));
        }
        if (itemsContainer == null)
        {
            var tr = transform
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("items"));
            if (tr != null) itemsContainer = tr;
        }
        if (enemiesText == null)
        {
            enemiesText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("ennemi"));
        }
        if (maxDamageText == null)
        {
            maxDamageText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("degats"));
        }
        if (mvpImage == null)
        {
            mvpImage = GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.name.ToLower().Contains("mvp"));
        }
        if (timeText == null)
        {
            timeText = GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.ToLower().Contains("temps"));
        }
    }

    public void DisplayVictory(int xp, List<ItemData> items, int totalEnemies, float duration, CharacterUnit mvp, int maxDamage)
    {
        if (xpText != null)
            xpText.text = $"+{xp} XP";
        if (xpIcon != null)
            xpIcon.enabled = xp > 0;

        if (itemsContainer != null)
        {
            foreach (Transform child in itemsContainer)
                Destroy(child.gameObject);

            foreach (var item in items)
            {
                GameObject entry = new GameObject(item.itemName);
                entry.transform.SetParent(itemsContainer, false);
                var img = entry.AddComponent<Image>();
                img.sprite = item.itemIcon;
                var txtObj = new GameObject("Text");
                txtObj.transform.SetParent(entry.transform, false);
                var txt = txtObj.AddComponent<TextMeshProUGUI>();
                txt.text = item.itemName;
            }
        }

        if (enemiesText != null)
            enemiesText.text = $"Ennemis vaincus : {totalEnemies}";

        if (maxDamageText != null)
            maxDamageText.text = $"Dégâts max : {maxDamage}";

        if (mvpImage != null && mvp != null && mvp.Data.portrait != null)
            mvpImage.sprite = mvp.Data.portrait;

        if (timeText != null)
            timeText.text = System.TimeSpan.FromSeconds(duration).ToString("mm':'ss");
    }
}
