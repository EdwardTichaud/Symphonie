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
    private Canvas parentCanvas;

    [Header("Nom de Munin")]
    public GameObject namePanel; // Panneau UI demandant le nom du joueur
    public TMP_InputField nameInputField; // Champ de saisie du nom
    public string defaultMuninName = "Munin"; // Nom par défaut si aucun n'est fourni

    void Awake()
    {
        playerInputs = new PlayerInputs();
    }

    private void Start()
    {
        if (pressA != null)
            pressA.alpha = 0.5f; // Pré-affiche le message "Press A"
        if (menuContainer != null)
            menuContainer.SetActive(false);
        if (loadMenu != null)
            loadMenu.gameObject.SetActive(false);
        if (menuCursor != null)
            menuCursor.SetActive(false);

        playerInputs.Menu.Enable();
        playerInputs.World.Enable();

        playerInputs.Menu.Confirm.performed += OnConfirm;
        playerInputs.Menu.Return.performed += OnReturn;

        parentCanvas = GetComponentInParent<Canvas>();

        if (menuContainer != null)
        {
            menuItems = new Transform[menuContainer.transform.childCount];
            for (int i = 0; i < menuItems.Length; i++)
                menuItems[i] = menuContainer.transform.GetChild(i);
        }

        // Chargement ou demande du nom de Munin
        if (PlayerPrefs.HasKey("MuninName"))
        {
            // Si un nom existe déjà, on le charge dans les données de jeu
            string savedName = PlayerPrefs.GetString("MuninName");
            if (GameManager.Instance != null && GameManager.Instance.gameData != null)
                GameManager.Instance.gameData.muninName = savedName;
        }
        else
        {
            // Aucun nom enregistré : on affiche le panneau de saisie
            ShowNamePanel();
        }
    }

    private void OnDestroy()
    {
        if (playerInputs.Menu.Confirm != null)
            playerInputs.Menu.Confirm.performed -= OnConfirm;
        if (playerInputs.Menu.Return != null)
            playerInputs.Menu.Return.performed -= OnReturn;
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
        // Si le panneau de saisie du nom est actif, le bouton de confirmation validera le nom
        if (namePanel != null && namePanel.activeSelf)
        {
            ConfirmName();
            return;
        }

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

    /// <summary>
    /// Affiche le panneau permettant au joueur de choisir le nom de Munin.
    /// </summary>
    private void ShowNamePanel()
    {
        waitingForInput = false; // Empêche l'ouverture du menu tant que le nom n'est pas choisi
        if (pressA != null)
            pressA.gameObject.SetActive(false); // Masque "Press A" durant la saisie
        if (namePanel != null)
            namePanel.SetActive(true);
        if (nameInputField != null)
            nameInputField.text = defaultMuninName; // Pré-remplit avec le nom par défaut
    }

    /// <summary>
    /// Valide le nom saisi, le sauvegarde et affiche de nouveau l'écran de démarrage.
    /// </summary>
    public void ConfirmName()
    {
        string chosenName = nameInputField != null ? nameInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(chosenName))
            chosenName = defaultMuninName; // Utilise le nom par défaut si aucune saisie

        // Met à jour les données de jeu
        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
            GameManager.Instance.gameData.muninName = chosenName;

        // Sauvegarde dans les préférences locales
        PlayerPrefs.SetString("MuninName", chosenName);
        PlayerPrefs.Save();

        // Cache le panneau et réaffiche "Press A"
        if (namePanel != null)
            namePanel.SetActive(false);
        if (pressA != null)
            pressA.gameObject.SetActive(true);

        waitingForInput = true; // Le joueur peut maintenant accéder au menu principal
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

    private void OnReturn(InputAction.CallbackContext ctx)
    {
        if (waitingForInput)
            return;

        if (loadMenu != null && loadMenu.gameObject.activeSelf)
        {
            CloseLoadMenu();
            return;
        }

        HideMenu();
    }

    private void CloseLoadMenu()
    {
        if (loadMenu != null)
            loadMenu.gameObject.SetActive(false);
    }

    private void HideMenu()
    {
        waitingForInput = true;

        if (pressA != null)
        {
            pressA.gameObject.SetActive(true);
            pressA.alpha = 0.5f;
        }

        if (menuContainer != null)
            menuContainer.SetActive(false);

        if (menuCursor != null)
            menuCursor.SetActive(false);
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

        if (parentCanvas != null)
        {
            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(
                parentCanvas.worldCamera,
                menuItems[currentIndex].position
            );
            screenPos += offset;

            RectTransform parentRect = menuCursor.transform.parent as RectTransform;
            Vector3 worldPos;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    parentRect,
                    screenPos,
                    parentCanvas.worldCamera,
                    out worldPos))
            {
                cursorTargetPosition = worldPos;
            }
            else
            {
                cursorTargetPosition = menuItems[currentIndex].position + offset;
            }
        }
        else
        {
            cursorTargetPosition = menuItems[currentIndex].position + offset;
        }

        // 🔥 Force la position immédiate au moment de l'activation du menu
        if (!menuCursor.activeInHierarchy || !menuCursor.activeSelf)
        {
            menuCursor.transform.position = cursorTargetPosition;
        }
    }
}
