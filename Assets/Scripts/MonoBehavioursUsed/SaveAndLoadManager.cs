using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SaveAndLoadManager : MonoBehaviour
{
    public static SaveAndLoadManager Instance { get; private set; }

    private string saveDirectory;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
        if (!Directory.Exists(saveDirectory))
            Directory.CreateDirectory(saveDirectory);
    }

    public IEnumerable<string> GetAllSaveNames()
    {
        if (!Directory.Exists(saveDirectory))
            yield break;

        foreach (var file in Directory.GetFiles(saveDirectory, "*.json"))
            if(!file.EndsWith(".info.json"))
                yield return Path.GetFileNameWithoutExtension(file);
    }

    public IEnumerable<SaveInfo> GetAllSaveInfos()
    {
        foreach (var file in Directory.GetFiles(saveDirectory, "*.info.json"))
        {
            string json = File.ReadAllText(file);
            SaveInfo info = JsonUtility.FromJson<SaveInfo>(json);
            info.saveName = Path.GetFileNameWithoutExtension(file).Replace(".info", "");
            yield return info;
        }
    }

    public void SaveGame(string saveName, bool confirmOverwrite = true)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[SaveAndLoadManager] GameManager introuvable.");
            return;
        }

        string relativePath = Path.Combine("Saves", saveName + ".json");
        string fullPath = Path.Combine(Application.persistentDataPath, relativePath);

        if (File.Exists(fullPath) && confirmOverwrite && InfoBoxManager.Instance != null)
        {
            StartCoroutine(SaveWithConfirmation(saveName, relativePath));
        }
        else
        {
            StartCoroutine(SaveRoutine(saveName, relativePath));
        }
    }

    private IEnumerator SaveWithConfirmation(string saveName, string relativePath)
    {
        InfoBoxManager.Instance.OpenInfoBox("Sauvegarde", "Écraser la sauvegarde existante ?", null);
        while (!InfoBoxManager.Instance.choix.HasValue)
            yield return null;

        if (InfoBoxManager.Instance.choix.Value)
            yield return SaveRoutine(saveName, relativePath);
    }

    private IEnumerator SaveRoutine(string saveName, string relativePath)
    {
        GameManager.Instance.gameData.SaveToFile(relativePath);

        yield return new WaitForEndOfFrame();

        SaveInfo info = new SaveInfo
        {
            saveName = saveName,
            zoneName = ZoneManager.Instance != null && ZoneManager.Instance.currentZone != null ? ZoneManager.Instance.currentZone.zoneName : "Inconnue",
            dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            screenshotFile = saveName + ".png"
        };

        string screenshotPath = Path.Combine(saveDirectory, info.screenshotFile);
        ScreenCapture.CaptureScreenshot(screenshotPath);

        string infoPath = Path.Combine(saveDirectory, saveName + ".info.json");
        File.WriteAllText(infoPath, JsonUtility.ToJson(info, true));
    }

    public void LoadGame(string saveName)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[SaveAndLoadManager] GameManager introuvable.");
            return;
        }

        string relativePath = Path.Combine("Saves", saveName + ".json");
        GameManager.Instance.gameData.LoadFromFile(relativePath);
    }

    public void AutoSave()
    {
        if (GameManager.Instance == null)
            return;

        string name = "autosave_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        StartCoroutine(SaveRoutine(name, Path.Combine("Saves", name + ".json")));
    }
}
