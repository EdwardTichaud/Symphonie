using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public CanvasGroup pressA;
    public GameObject menuContainer;
    public SaveLoadMenu loadMenu;
    public float fadeSpeed = 2f;
    public GameObject menuCursor;

    private bool waitingForInput = true;
    private float timer = 0f;
    private PlayerInputs playerInputs;
    private PlayerInputs.MenuActions menuActions;

    void Awake()
    {
        playerInputs = new PlayerInputs();
    }

    private void Start()
    {
        if (pressA != null)
            pressA.alpha = 0.5f;
        if (menuContainer != null)
            menuContainer.SetActive(false);
        if (loadMenu != null)
            loadMenu.gameObject.SetActive(false);

        menuActions = playerInputs.Menu;
        menuActions.Confirm.performed += OnConfirm;
    }

    private void OnDestroy()
    {
        if (menuActions.Confirm != null)
            menuActions.Confirm.performed -= OnConfirm;
    }

    private void Update()
    {
        if (waitingForInput)
        {
            timer += Time.deltaTime * fadeSpeed;
            float alpha = 0.25f + 0.25f * (1 + Mathf.Sin(timer));
            if (pressA != null)
                pressA.alpha = alpha;
        }
    }

    private void OnConfirm(InputAction.CallbackContext ctx)
    {
        if (waitingForInput)
            ShowMenu();
    }

    private void ShowMenu()
    {
        waitingForInput = false;
        if (pressA != null)
            pressA.gameObject.SetActive(false);
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
