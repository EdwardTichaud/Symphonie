using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public CanvasGroup pressAnyKeyGroup;
    public GameObject menuContainer;
    public SaveLoadMenu loadMenu;
    public float fadeSpeed = 2f;

    private bool waitingForInput = true;
    private float timer = 0f;

    private void Start()
    {
        if (pressAnyKeyGroup != null)
            pressAnyKeyGroup.alpha = 0.5f;
        if (menuContainer != null)
            menuContainer.SetActive(false);
        if (loadMenu != null)
            loadMenu.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (waitingForInput)
        {
            timer += Time.deltaTime * fadeSpeed;
            float alpha = 0.25f + 0.25f * (1 + Mathf.Sin(timer));
            if (pressAnyKeyGroup != null)
                pressAnyKeyGroup.alpha = alpha;
            if (Keyboard.current.anyKey.wasPressedThisFrame)
                ShowMenu();
        }
    }

    private void ShowMenu()
    {
        waitingForInput = false;
        if (pressAnyKeyGroup != null)
            pressAnyKeyGroup.gameObject.SetActive(false);
        if (menuContainer != null)
            menuContainer.SetActive(true);
    }

    public void ContinueGame()
    {
        if (SaveAndLoadManager.Instance == null)
            return;

        SaveInfo latest = null;
        foreach (SaveInfo info in SaveAndLoadManager.Instance.GetAllSaveInfos())
        {
            if (latest == null || DateTime.Parse(info.dateTime) > DateTime.Parse(latest.dateTime))
                latest = info;
        }

        if (latest != null)
            SaveAndLoadManager.Instance.LoadGame(latest.saveName);
        else
            Debug.Log("Aucune sauvegarde trouvée.");
    }

    public void OpenLoadMenu()
    {
        if (loadMenu == null) return;
        loadMenu.gameObject.SetActive(true);
        loadMenu.RefreshList();
    }

    public void OpenOptions()
    {
        Debug.Log("Menu Options non implémenté.");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
