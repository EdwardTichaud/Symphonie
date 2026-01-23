using System;
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

    [Tooltip("Zone de texte où sera affichée la saisie (champ 'Output Text' dans la scène).")]
    [SerializeField] private TextMeshProUGUI currentVKWord;

    [Tooltip("Texte de sujet affiché au-dessus du clavier (ex: 'Subject').")]
    [SerializeField] private TextMeshProUGUI subjectText;

    [Header("Munin Name Storage")]
    [SerializeField] private bool storeMuninNameOnValidate = true;
    [SerializeField] private string muninNamePrompt = "Comment t'appelles-tu?";

    [Header("Paramètres")]
    [Tooltip("Nombre de colonnes dans la grille du clavier.")]
    [SerializeField] private int columns = 13;

    [Tooltip("Temps minimal entre deux déplacements du curseur (en secondes).")]
    [SerializeField] private float moveDelay = 0.1f;

    // Liste interne des touches disponibles sur le clavier.
    private readonly List<RectTransform> _keys = new();
    // Index courant dans la liste des touches.
    private int _currentIndex;
    // Mémorise le dernier moment où un déplacement a été effectué pour limiter la vitesse.
    private float _lastMoveTime;
    // Texte actuellement saisi via le clavier virtuel, accessible aux autres systèmes.
    public string currentVKWordText { get; private set; } = string.Empty;
    private bool shouldStoreMuninName;

    /// <summary>
    /// Évènement déclenché lorsque le joueur valide sa saisie via le bouton "OK".
    /// </summary>
    public event Action<string> WordValidated;

    /// <summary>
    /// Évènement déclenché à chaque fermeture du clavier, que ce soit après validation
    /// ou annulation (utile pour réactiver certains panneaux).
    /// </summary>
    public event Action KeyboardClosed;

    // Référence vers le système d'inputs. On privilégie l'instance centrale fournie par InputsManager
    // afin de conserver une cohérence des contrôles dans tout le projet.
    private PlayerInputs playerInputs;
    // Indique si nous avons dû créer une instance locale (cas rare lorsque InputsManager est absent).
    private bool ownsLocalInputs = false;

    private void Awake()
    {
        // Récupère l'instance globale d'inputs si disponible ; sinon, on en crée une localement.
        if (InputsManager.Instance != null)
        {
            playerInputs = InputsManager.Instance.playerInputs;
            ownsLocalInputs = false;
        }
        else
        {
            playerInputs = new PlayerInputs();
            ownsLocalInputs = true;
        }

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

        // Avertit si aucune instance d'inputs n'a pu être trouvée.
        if (playerInputs == null)
            Debug.LogWarning("[VirtualKeyboard] Aucun PlayerInputs disponible. Les entrées ne fonctionneront pas.");
    }

    /// <summary>
    /// Méthode publique permettant d'ouvrir le clavier virtuel.
    /// </summary>
    public void OpenVK(string subject)
    {
        if (keyboardRoot == null || cursor == null || playerInputs == null)
            return;

        keyboardRoot.SetActive(true);
        cursor.gameObject.SetActive(true);

        _currentIndex = 0;
        currentVKWordText = string.Empty; // Réinitialise systématiquement la saisie.
        if (currentVKWord != null)
            currentVKWord.text = string.Empty;
        UpdateCursor();

        // On s'assure que le mapping World est actif pour recevoir les entrées.
        // Si l'InputsManager gère déjà cette map, l'appel à Enable() est sans risque.
        if (playerInputs != null)
            playerInputs.World.Enable();

        // Abonnements aux différentes entrées du joueur.
        playerInputs.World.Move.performed += OnMove;
        playerInputs.World.Interact.performed += OnInteract;
        playerInputs.World.Cancel.performed += OnCancel;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
            animator.Play("VK_Open");

        TextMeshProUGUI subjectLabel = ResolveSubjectText();
        if (subjectLabel != null)
            subjectLabel.text = subject ?? string.Empty;

        shouldStoreMuninName = ShouldStoreMuninName(subject);
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

            // On ne désactive le mapping que si nous avons créé les inputs localement.
            if (ownsLocalInputs)
                playerInputs.World.Disable();
        }

        if (cursor != null)
            cursor.gameObject.SetActive(false);

        if (keyboardRoot != null)
            keyboardRoot.SetActive(false);

        shouldStoreMuninName = false;
        KeyboardClosed?.Invoke();
    }

    /// <summary>
    /// S'assure que les entrées sont correctement libérées si l'objet est détruit.
    /// </summary>
    private void OnDestroy()
    {
        // On ferme proprement le clavier pour retirer les abonnements et désactiver la map.
        CloseVK();

        // Libère les ressources uniquement si l'on possède une instance locale.
        if (ownsLocalInputs && playerInputs != null)
            playerInputs.Dispose();
    }

    /// <summary>
    /// Gestion du déplacement du curseur à l'intérieur de la grille du clavier.
    /// </summary>
    private void OnMove(InputAction.CallbackContext ctx)
    {
        // Empêche les déplacements trop rapides en vérifiant le délai minimal.
        if (Time.unscaledTime - _lastMoveTime < moveDelay)
            return;

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

            // Enregistre l'heure du déplacement pour appliquer la temporisation.
            _lastMoveTime = Time.unscaledTime;
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
            WordValidated?.Invoke(currentVKWordText);
            if (shouldStoreMuninName)
                MuninNameStore.SetName(currentVKWordText);
            CloseVK();
        }
        else
        {
            // Ajoute la lettre sélectionnée au mot en cours et met à jour l'affichage.
            currentVKWordText += keyName;
            if (currentVKWord != null)
                currentVKWord.text = currentVKWordText;
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
        if (currentVKWordText.Length == 0)
            return;

        // Retire le dernier caractère et met à jour le texte affiché.
        currentVKWordText = currentVKWordText.Substring(0, currentVKWordText.Length - 1);
        if (currentVKWord != null)
            currentVKWord.text = currentVKWordText;
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

    private TextMeshProUGUI ResolveSubjectText()
    {
        if (subjectText != null)
            return subjectText;

        subjectText = FindSubjectText(keyboardRoot != null ? keyboardRoot.transform : transform);
        if (subjectText == null && keyboardRoot != null && keyboardRoot.transform != transform)
            subjectText = FindSubjectText(transform);

        return subjectText;
    }

    private static TextMeshProUGUI FindSubjectText(Transform root)
    {
        if (root == null)
            return null;

        var labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var label in labels)
        {
            if (label != null && label.name == "VKSubject_Text")
                return label;
        }

        return null;
    }

    private bool ShouldStoreMuninName(string subject)
    {
        if (!storeMuninNameOnValidate)
            return false;

        if (string.IsNullOrWhiteSpace(muninNamePrompt))
            return true;

        string normalizedSubject = subject != null ? subject.Trim() : string.Empty;
        string normalizedPrompt = muninNamePrompt.Trim();

        if (normalizedPrompt.Length == 0)
            return true;

        return string.Equals(normalizedSubject, normalizedPrompt, StringComparison.OrdinalIgnoreCase);
    }
}

