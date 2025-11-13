using System;
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

    private readonly Dictionary<string, Texture2D> previewTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Sprite> previewSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    private void Start()
    {
        RefreshList();
    }

    private void OnDestroy()
    {
        foreach (var sprite in previewSprites.Values)
            Destroy(sprite);

        foreach (var texture in previewTextures.Values)
            Destroy(texture);

        previewSprites.Clear();
        previewTextures.Clear();
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
        if (info == null)
        {
            ClearPreview();
            return;
        }

        infoText.text = $"{info.zoneName}\n{info.dateTime}";

        if (string.IsNullOrEmpty(info.screenshotFile))
        {
            ClearPreviewImage();
            return;
        }

        if (previewSprites.TryGetValue(info.screenshotFile, out var cachedSprite))
        {
            previewImage.sprite = cachedSprite;
            return;
        }

        string path = Path.Combine(Application.persistentDataPath, "Saves", info.screenshotFile);
        if (!File.Exists(path))
        {
            ClearPreviewImage();
            return;
        }

        byte[] data = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(data))
        {
            Destroy(tex);
            ClearPreviewImage();
            return;
        }

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        previewTextures[info.screenshotFile] = tex;
        previewSprites[info.screenshotFile] = sprite;
        previewImage.sprite = sprite;
    }

    public void LoadSave(string saveName)
    {
        SaveAndLoadManager.Instance?.LoadGame(saveName);
    }

    private void ClearPreview()
    {
        infoText.text = string.Empty;
        ClearPreviewImage();
    }

    private void ClearPreviewImage()
    {
        if (previewImage != null)
            previewImage.sprite = null;
    }
}
