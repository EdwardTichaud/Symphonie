using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestionnaire du clavier virtuel utilisé pour saisir du texte sans clavier physique.
/// </summary>
public class VirtualKeyboard : MonoBehaviour
{
    public static VirtualKeyboard Instance { get; private set; }

    [Header("Références UI")]
    [Tooltip("Racine du clavier virtuel.")]
    [SerializeField] private GameObject keyboardRoot;

    [Tooltip("Conteneur des boutons du clavier (doit utiliser GridLayoutGroup).")]
    [SerializeField] private RectTransform buttonsContainer;

    [Tooltip("Objet représentant le curseur de sélection.")]
    [SerializeField] private RectTransform cursor;

    [Tooltip("Zone de texte où sera affichée la saisie.")]
    [SerializeField] private TextMeshProUGUI outputText;

    [Header("Paramètres")]
    [Tooltip("Nombre de colonnes dans la grille du clavier.")]
    [SerializeField] private int columns = 13;

    // Liste interne des touches disponibles sur le clavier.
    private readonly List<RectTransform> _keys = new();
    // Index courant dans la liste des touches.
    private int _currentIndex;
    // Chaîne saisie par le joueur.
    private string _currentText = string.Empty;

    // Référence vers le système d'inputs généré par l'Input System.
    private PlayerInputs playerInputs;

    private void Awake()
    {
        playerInputs = new PlayerInputs();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Si aucune racine n'est précisée, on suppose que le script est placé sur celle-ci.
        if (keyboardRoot == null)
            keyboardRoot = gameObject;

        // Récupération automatique de toutes les touches enfants du conteneur.
        if (buttonsContainer != null)
        {
            foreach (Transform child in buttonsContainer)
            {
                if (child is RectTransform rt)
                    _keys.Add(rt);
            }
        }

        // Le clavier est caché au lancement pour éviter toute interaction accidentelle.
        keyboardRoot.SetActive(false);
        if (cursor != null)
            cursor.gameObject.SetActive(false);

        // Recherche d'une instance de PlayerInputs dans la scène.
        if (playerInputs == null)
            Debug.LogWarning("[VirtualKeyboard] Aucun PlayerInputs trouvé dans la scène.");
    }

    /// <summary>
    /// Méthode publique permettant d'ouvrir le clavier virtuel.
    /// </summary>
    public void OpenVK()
    {
        if (keyboardRoot == null || cursor == null || playerInputs == null)
            return;

        keyboardRoot.SetActive(true);
        cursor.gameObject.SetActive(true);

        _currentIndex = 0;
        UpdateCursor();

        // Abonnements aux différentes entrées du joueur.
        playerInputs.World.Move.performed += OnMove;
        playerInputs.World.Interact.performed += OnInteract;
        playerInputs.World.Cancel.performed += OnCancel;
    }

    /// <summary>
    /// Ferme le clavier et supprime les abonnements aux entrées.
    /// </summary>
    private void CloseVK()
    {
        if (playerInputs != null)
        {
            playerInputs.World.Move.performed -= OnMove;
            playerInputs.World.Interact.performed -= OnInteract;
            playerInputs.World.Cancel.performed -= OnCancel;
        }

        if (cursor != null)
            cursor.gameObject.SetActive(false);

        if (keyboardRoot != null)
            keyboardRoot.SetActive(false);
    }

    /// <summary>
    /// Gestion du déplacement du curseur à l'intérieur de la grille du clavier.
    /// </summary>
    private void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 input = ctx.ReadValue<Vector2>();
        int row = _currentIndex / columns;
        int col = _currentIndex % columns;

        if (input.x > 0.5f)        // Déplacement vers la droite
            col = Mathf.Min(col + 1, columns - 1);
        else if (input.x < -0.5f)  // Déplacement vers la gauche
            col = Mathf.Max(col - 1, 0);
        else if (input.y > 0.5f)   // Déplacement vers le haut
            row = Mathf.Max(row - 1, 0);
        else if (input.y < -0.5f)  // Déplacement vers le bas
            row = Mathf.Min(row + 1, (_keys.Count - 1) / columns);

        int newIndex = row * columns + col;
        newIndex = Mathf.Clamp(newIndex, 0, _keys.Count - 1);

        if (newIndex != _currentIndex)
        {
            _currentIndex = newIndex;
            UpdateCursor();
        }
    }

    /// <summary>
    /// Valide la touche courante lorsqu'on appuie sur l'input Interact.
    /// </summary>
    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (_currentIndex < 0 || _currentIndex >= _keys.Count)
            return;

        string keyName = _keys[_currentIndex].name;

        // Les boutons spéciaux demandent un traitement particulier.
        if (keyName == "Erase")
        {
            RemoveLastChar();
        }
        else if (keyName == "OK")
        {
            // Pour l'instant, la validation finale ferme simplement le clavier.
            CloseVK();
        }
        else
        {
            _currentText += keyName;
            if (outputText != null)
                outputText.text = _currentText;
        }
    }

    /// <summary>
    /// Supprime le dernier caractère lorsqu'on appuie sur Cancel.
    /// </summary>
    private void OnCancel(InputAction.CallbackContext ctx)
    {
        RemoveLastChar();
    }

    /// <summary>
    /// Supprime la dernière lettre saisie et met à jour l'affichage.
    /// </summary>
    private void RemoveLastChar()
    {
        if (_currentText.Length == 0)
            return;

        _currentText = _currentText.Substring(0, _currentText.Length - 1);
        if (outputText != null)
            outputText.text = _currentText;
    }

    /// <summary>
    /// Replace le curseur visuel sur la touche actuellement sélectionnée.
    /// </summary>
    private void UpdateCursor()
    {
        if (cursor == null || _currentIndex < 0 || _currentIndex >= _keys.Count)
            return;

        // Le curseur devient enfant de la touche afin de se positionner correctement.
        cursor.SetParent(_keys[_currentIndex], false);
        cursor.anchoredPosition = Vector2.zero;
    }
}

