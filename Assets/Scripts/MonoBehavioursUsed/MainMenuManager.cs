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

    [Header("Navigation")]
    public Vector3 cursorOffset = new Vector3(-300f, 0f, 0f);
    public float navigationCooldown = 0.25f;

    [Header("Cursor Smooth Movement")]
    public float cursorLerpSpeed = 10f;
    private Vector3 cursorTargetPosition;

    private Transform[] menuItems;
    private int currentIndex = 0;
    private float lastNavTime = 0f;

    private bool waitingForInput = true;
    private float timer = 0f;
    private PlayerInputs playerInputs;

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
        if (menuCursor != null)
            menuCursor.SetActive(false);

        playerInputs.Menu.Enable();
        playerInputs.World.Enable();

        playerInputs.Menu.Confirm.performed += OnConfirm;

        if (menuContainer != null)
        {
            menuItems = new Transform[menuContainer.transform.childCount];
            for (int i = 0; i < menuItems.Length; i++)
                menuItems[i] = menuContainer.transform.GetChild(i);
        }
    }

    private void OnDestroy()
    {
        if (playerInputs.Menu.Confirm != null)
            playerInputs.Menu.Confirm.performed -= OnConfirm;
        playerInputs.World.Disable();
        playerInputs.Menu.Disable();
        if (menuCursor != null)
            menuCursor.SetActive(false);
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
        else
        {
            HandleNavigation();
        }

        if (menuCursor.activeSelf)
        {
            menuCursor.transform.position = Vector3.Lerp(
                menuCursor.transform.position,
                cursorTargetPosition,
                Time.deltaTime * cursorLerpSpeed
            );
        }
    }

    private void OnConfirm(InputAction.CallbackContext ctx)
    {
        if (waitingForInput)
        {
            Debug.Log("Bouton A pressé");
            ShowMenu();
            return;
        }

        switch (currentIndex)
        {
            case 0:
                ContinueGame();
                break;
            case 1:
                OpenLoadMenu();
                break;
            case 3:
                QuitGame();
                break;
        }
    }

    private void ShowMenu()
    {
        waitingForInput = false;

        if (pressA != null)
            pressA.gameObject.SetActive(false);

        if (menuContainer != null)
        {
            menuContainer.SetActive(true);
            Canvas.ForceUpdateCanvases(); // 🩹 <- ceci force le layout immédiatement
        }

        currentIndex = 0;
        UpdateCursor();
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

    private void HandleNavigation()
    {
        if (menuItems == null || menuItems.Length == 0) return;

        Vector2 input = playerInputs.World.Move.ReadValue<Vector2>();
        if (Time.time - lastNavTime < navigationCooldown)
            return;

        if (input.y > 0.5f)
        {
            currentIndex = (currentIndex - 1 + menuItems.Length) % menuItems.Length;
            lastNavTime = Time.time;
            UpdateCursor();
        }
        else if (input.y < -0.5f)
        {
            currentIndex = (currentIndex + 1) % menuItems.Length;
            lastNavTime = Time.time;
            UpdateCursor();
        }
    }

    private void UpdateCursor()
    {
        if (menuCursor == null || menuItems == null || currentIndex >= menuItems.Length)
            return;

        menuCursor.SetActive(true);

        Vector3 offset = cursorOffset;

        TextMeshProUGUI txt = menuItems[currentIndex].GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.ForceMeshUpdate();
            float textWidth = txt.preferredWidth;
            offset.x = -(textWidth / 2f + 100f);
        }

        cursorTargetPosition = menuItems[currentIndex].position + offset;

        // 🔥 Force la position immédiate au moment de l'activation du menu
        if (!menuCursor.activeInHierarchy || !menuCursor.activeSelf)
        {
            menuCursor.transform.position = cursorTargetPosition;
        }
    }
}
