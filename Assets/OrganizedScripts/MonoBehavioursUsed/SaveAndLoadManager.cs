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
            yield return Path.GetFileNameWithoutExtension(file);
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
            StartCoroutine(SaveWithConfirmation(relativePath));
        }
        else
        {
            GameManager.Instance.gameData.SaveToFile(relativePath);
        }
    }

    private IEnumerator SaveWithConfirmation(string relativePath)
    {
        InfoBoxManager.Instance.OpenInfoBox("Sauvegarde", "Écraser la sauvegarde existante ?", null);
        while (!InfoBoxManager.Instance.choix.HasValue)
            yield return null;

        if (InfoBoxManager.Instance.choix.Value)
            GameManager.Instance.gameData.SaveToFile(relativePath);
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
        GameManager.Instance.gameData.SaveToFile(Path.Combine("Saves", name + ".json"));
    }
}
