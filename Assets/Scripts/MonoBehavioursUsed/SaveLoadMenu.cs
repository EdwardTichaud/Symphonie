using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveLoadMenu : MonoBehaviour
{
    [Header("UI Elements")]
    public Transform savesContainer;
    public GameObject saveItemPrefab;
    public Image previewImage;
    public TextMeshProUGUI infoText;

    private void Start()
    {
        RefreshList();
    }

    public void RefreshList()
    {
        foreach (Transform child in savesContainer)
            Destroy(child.gameObject);

        if (SaveAndLoadManager.Instance == null)
            return;

        foreach (SaveInfo info in SaveAndLoadManager.Instance.GetAllSaveInfos())
        {
            GameObject item = Instantiate(saveItemPrefab, savesContainer);
            TextMeshProUGUI text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = info.saveName;

            SaveSlotUI slot = item.AddComponent<SaveSlotUI>();
            slot.Init(info, this);
        }
    }

    public void DisplayInfo(SaveInfo info)
    {
        string path = Path.Combine(Application.persistentDataPath, "Saves", info.screenshotFile);
        if (File.Exists(path))
        {
            byte[] data = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(data);
            previewImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        infoText.text = $"{info.zoneName}\n{info.dateTime}";
    }

    public void LoadSave(string saveName)
    {
        SaveAndLoadManager.Instance?.LoadGame(saveName);
    }
}
