using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Boîte de confirmation générique affichant une question au joueur
/// et récupérant sa réponse Oui/Non via l'Input System.
/// </summary>
public class ConfirmationBox : MonoBehaviour
{
    public static ConfirmationBox Instance { get; private set; }

    [Header("Références UI")]
    [Tooltip("Zone de texte où s'affiche la question posée au joueur.")]
    public TextMeshProUGUI messageText;

    private System.Action yesCallback;   // Action exécutée en cas de réponse positive
    private System.Action noCallback;    // Action exécutée en cas de réponse négative

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (!GameRoot.KeepManagersSceneBound)
            DontDestroyOnLoad(gameObject);
        gameObject.SetActive(false); // Masqué par défaut
    }

    void OnEnable()
    {
        // Abonne les entrées Oui/Non uniquement lorsque la boîte est visible
        var info = InputsManager.Instance.playerInputs.InfoBox;
        info.Confirm.performed += OnConfirm;
        info.Cancel.performed += OnCancel;
    }

    void OnDisable()
    {
        if (InputsManager.Instance == null) return;
        var info = InputsManager.Instance.playerInputs.InfoBox;
        info.Confirm.performed -= OnConfirm;
        info.Cancel.performed -= OnCancel;
    }

    /// <summary>
    /// Affiche la boîte avec le message donné et les callbacks associés.
    /// </summary>
    public void Show(string message, System.Action onYes, System.Action onNo)
    {
        messageText.text = message;
        yesCallback = onYes;
        noCallback = onNo;
        gameObject.SetActive(true);

        // Active uniquement la map InfoBox pour empêcher les déplacements
        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.InfoBox.Get());
    }

    /// <summary>
    /// Fonction appelée lorsque le joueur choisit "Oui".
    /// </summary>
    public void OnConfirm(InputAction.CallbackContext ctx) => Confirm();

    /// <summary>
    /// Fonction appelée lorsque le joueur choisit "Non".
    /// </summary>
    public void OnCancel(InputAction.CallbackContext ctx) => Cancel();

    private void Confirm()
    {
        Close();
        yesCallback?.Invoke();
    }

    private void Cancel()
    {
        Close();
        noCallback?.Invoke();
    }

    /// <summary>
    /// Ferme la boîte et réactive les déplacements du joueur.
    /// </summary>
    private void Close()
    {
        gameObject.SetActive(false);
        yesCallback = null;
        noCallback = null;

        // Réactive le gameplay classique
        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.World.Get());
    }
}
