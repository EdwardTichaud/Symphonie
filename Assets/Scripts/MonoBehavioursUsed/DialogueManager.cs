using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;
    public string text;
}

/// <summary>
/// DialogueManager complet :
/// - Lecture manuelle et auto-avance
/// - Séquences de DialogueContainer avec NextDialogue() pour passer au container suivant
/// - Option NextLine() pour passer à la ligne suivante du container courant
/// - Verrouillage des contrôles joueur
/// - Positionnement de la bulle
/// </summary>
/// <remarks>
/// L'attribut <see cref="ExecuteAlways"/> permet de prévisualiser les dialogues
/// directement depuis l'éditeur Unity (notamment lors de l'utilisation des Timelines
/// et de leurs signaux) sans devoir lancer le Play Mode.
/// </remarks>
[ExecuteAlways]
public class DialogueManager : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI nameText;
    [Tooltip("Temps entre chaque caractère (utilisé par TypeLine).")]
    public float typingSpeed = 0.01f;

    [Header("State (read-only)")]
    public bool isOpen;

    // Runtime flags
    private bool isTyping = false;
    private bool skipRequested = false;     // pour terminer instantanément la frappe en cours
    private bool nextRequested = false;     // pour passer à la ligne suivante (mode manuel) si pas en frappe

    // Singleton
    public static DialogueManager Instance { get; private set; }

    // Callback de fin pour PlayDialogue/PlayDialogueAuto (conteneur unique)
    private System.Action onDialogueEndCallback;

    // RectTransform du panneau de dialogue (pour le positionnement)
    private RectTransform rectTransform;

    // --- Séquence de DialogueContainer ---
    private List<DialogueContainer> sequence = new List<DialogueContainer>();
    private int sequenceIndex = -1;

    // Paramètres par défaut pour l'auto-advance à l'intérieur des containers de la séquence
    private bool sequenceAuto = false;
    private float seq_timePerChar = 0.03f, seq_minHold = 0.5f, seq_maxHold = 2.5f;
    private bool seq_unscaled = true;

    // --- Prévisualisation de l'éditeur ---
    // Stocke temporairement le container et l'index courant afin
    // d'avancer manuellement ligne par ligne via un signal.
    private DialogueContainer previewContainer;
    private int previewLineIndex = -1;

    // Séquence active + verrouillage des contrôles pendant toute la séquence
    private bool inSequence = false;
    private bool lockControlsDuringSequence = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // En mode éditeur, on détruit immédiatement pour éviter des restes de preview
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;

        // Évite l'avertissement "DontDestroyOnLoad" hors Play Mode
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        rectTransform = GetComponent<RectTransform>();
    }

    //==============================================================
    // Contrôles joueur
    //==============================================================

    /// <summary>
    /// Active ou désactive les actions de déplacement du joueur.
    /// Cela empêche Lucian de se déplacer pendant les dialogues.
    /// </summary>
    private void TogglePlayerMovement(bool enable)
    {
        if (InputsManager.Instance == null) return;

        var world = InputsManager.Instance.playerInputs.World;

        if (enable)
        {
            world.Move.Enable();
            world.Run.Enable();
            world.Jump.Enable();
            world.Dash.Enable();
        }
        else
        {
            world.Move.Disable();
            world.Run.Disable();
            world.Jump.Disable();
            world.Dash.Disable();
        }
    }

    /// <summary>
    /// Réinitialise tous les paramètres liés aux dialogues.
    /// Cette méthode ferme la boîte de dialogue, stoppe les coroutines en cours
    /// et remet à zéro les séquences et flags afin d'éviter tout mélange de
    /// dialogues lorsque plusieurs timelines ou PNJ se succèdent.
    /// </summary>
    private void ResetDialogueState()
    {
        // Coupe immédiatement toutes les coroutines de dialogue actives.
        StopAllCoroutines();

        // Ferme visuellement la boîte de dialogue pour effacer l'ancien texte.
        GetComponentInChildren<Animator>()?.Play("DialogueBoxClose");
        isOpen = false;

        // Réinitialise les flags internes.
        isTyping = false;
        skipRequested = false;
        nextRequested = false;

        // Réinitialise complètement les informations de séquence en cours.
        sequence.Clear();
        sequenceIndex = -1;
        inSequence = false;

        // Restaure les contrôles joueur au cas où ils seraient restés verrouillés.
        TogglePlayerMovement(true);
    }

    /// <summary>
    /// Force la fermeture immédiate et complète de tout dialogue en cours.
    /// Utile lorsque l'on souhaite interrompre brutalement une séquence
    /// (par exemple lors du passage d'une Timeline) sans laisser de
    /// bulles persistantes ou de coroutines actives.
    /// </summary>
    public void ForceCloseDialogue()
    {
        // Réutilise la logique centralisée afin de garantir que tous les
        // états internes et contrôles joueur sont correctement réinitialisés.
        ResetDialogueState();
    }

    //==============================================================
    // API publique : LECTURE D'UN CONTAINER (MANUELLE)
    //==============================================================

    /// <summary>
    /// Joue un DialogueContainer en mode manuel (input pour passer la ligne).
    /// </summary>
    public void PlayDialogue(DialogueContainer container, System.Action onEnd = null)
    {
        // En mode Éditeur (Timeline preview), on affiche simplement les lignes de façon statique
        // et on conserve le container pour permettre l'avancement via un signal NextLine.
        if (!Application.isPlaying)
        {
            previewContainer = container;
            previewLineIndex = 0;

            if (container != null && container.lines != null && container.lines.Length > 0)
            {
                nameText.text = container.lines[0].speakerName;
                dialogueText.text = container.lines[0].text;

                // Force l'ouverture visuelle de la boîte de dialogue
                var animator = GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.Play("DialogueBoxOpen", 0, 1f); // sauter directement à la fin de l'anim
                    animator.Update(0f); // applique immédiatement l'état
                }

                isOpen = true;
            }
            else
            {
                isOpen = false;
            }

            onEnd?.Invoke();
            return;
        }

        // Réinitialise les éventuels dialogues précédents pour éviter les overlaps.
        ResetDialogueState();

        onDialogueEndCallback = onEnd;
        StartCoroutine(StartDialogue(container));
    }

    /// <summary>
    /// Coroutine de lecture manuelle d'un container.
    /// </summary>
    public IEnumerator StartDialogue(DialogueContainer container)
    {
        // Ouverture UI
        GetComponentInChildren<Animator>()?.Play("DialogueBoxOpen");
        isOpen = true;

        // Verrouille les déplacements
        TogglePlayerMovement(false);

        yield return null; // Laisse l'UI s'init
        PositionDialogueBox(container);

        foreach (DialogueLine line in container.lines)
        {
            nameText.text = line.speakerName;
            yield return StartCoroutine(TypeLine(line.text));

            // Attente input OU nextRequested
            yield return new WaitUntil(() =>
            {
                bool pressed = InputsManager.Instance != null &&
                               InputsManager.Instance.playerInputs.World.Action.WasPressedThisFrame();

                if (pressed || nextRequested)
                {
                    nextRequested = false; // consomme le next
                    return true;
                }
                return false;
            });
        }

        // Fermeture UI
        GetComponentInChildren<Animator>()?.Play("DialogueBoxClose");
        isOpen = false;

        // Réactive les contrôles si on n'est pas en séquence (ou si on n'a pas choisi de les garder lockés)
        if (!(inSequence && lockControlsDuringSequence))
        {
            TogglePlayerMovement(true);
        }

        onDialogueEndCallback?.Invoke();
        onDialogueEndCallback = null;
    }

    /// <summary>
    /// Saisie "typewriter" avec possibilité de skip via input ou NextLine().
    /// </summary>
    private IEnumerator TypeLine(string line)
    {
        dialogueText.text = "";
        isTyping = true;
        skipRequested = false;

        foreach (char letter in line.ToCharArray())
        {
            if (skipRequested)
            {
                dialogueText.text = line;
                break;
            }

            dialogueText.text += letter;
            float t = 0f;
            while (t < typingSpeed)
            {
                // Input ou NextLine() pendant la frappe => skip
                if ((InputsManager.Instance != null &&
                     InputsManager.Instance.playerInputs.World.Action.WasPressedThisFrame())
                    || nextRequested)
                {
                    nextRequested = false;
                    skipRequested = true;
                    break;
                }

                t += Time.deltaTime;
                yield return null;
            }
        }

        isTyping = false;
    }

    public void CloseDialogue()
    {
        // Fermeture UI
        GetComponentInChildren<Animator>()?.Play("DialogueBoxClose");
        isOpen = false;
    }

    //==============================================================
    // API publique : LECTURE D'UN CONTAINER (AUTO-AVANCE)
    //==============================================================

    /// <summary>
    /// Joue un DialogueContainer en auto-avance.
    /// Temps d'affichage par ligne = clamp(minHold, len * timePerChar, maxHold).
    /// </summary>
    public void PlayDialogueAuto(
        DialogueContainer container,
        float timePerChar = 0.03f,
        float minHold = 0.5f,
        float maxHold = 2.5f,
        bool useUnscaledTime = true,
        System.Action onEnd = null)
    {
        // Assure que tout dialogue antérieur est correctement fermé avant d'en lancer un nouveau.
        ResetDialogueState();

        onDialogueEndCallback = onEnd;
        StartCoroutine(StartDialogueAuto(container, timePerChar, minHold, maxHold, useUnscaledTime));
    }

    /// <summary>
    /// Coroutine d'auto-avance pour un container.
    /// </summary>
    public IEnumerator StartDialogueAuto(
        DialogueContainer container,
        float timePerChar = 0.03f,
        float minHold = 0.5f,
        float maxHold = 2.5f,
        bool useUnscaledTime = true)
    {
        // Ouverture UI
        GetComponentInChildren<Animator>()?.Play("DialogueBoxOpen");
        isOpen = true;

        // Verrouille les déplacements
        TogglePlayerMovement(false);

        yield return null; // init UI
        PositionDialogueBox(container);

        foreach (DialogueLine line in container.lines)
        {
            nameText.text = line.speakerName;

            // Frappe avec skip possible
            yield return StartCoroutine(TypeLineAllowSkip(line.text, useUnscaledTime));

            // Attente d'affichage auto avec possibilité de skip (input) ou NextLine()
            float target = Mathf.Clamp(line.text?.Length * timePerChar ?? 0f, minHold, maxHold);
            float t = 0f;

            while (t < target)
            {
                if ((InputsManager.Instance != null &&
                     InputsManager.Instance.playerInputs.World.Action.WasPressedThisFrame())
                    || nextRequested)
                {
                    nextRequested = false; // consomme
                    break;
                }

                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }

        // Fermeture UI
        GetComponentInChildren<Animator>()?.Play("DialogueBoxClose");
        isOpen = false;

        if (!(inSequence && lockControlsDuringSequence))
        {
            TogglePlayerMovement(true);
        }

        onDialogueEndCallback?.Invoke();
        onDialogueEndCallback = null;
    }

    /// <summary>
    /// Variante typewriter compatible unscaled/ scaled + skip pendant frappe.
    /// </summary>
    private IEnumerator TypeLineAllowSkip(string line, bool useUnscaledTime)
    {
        dialogueText.text = "";
        isTyping = true;
        skipRequested = false;

        float wait = typingSpeed;

        foreach (char letter in line.ToCharArray())
        {
            if (skipRequested)
            {
                dialogueText.text = line;
                break;
            }

            dialogueText.text += letter;

            float t = 0f;
            while (t < wait)
            {
                if ((InputsManager.Instance != null &&
                     InputsManager.Instance.playerInputs.World.Action.WasPressedThisFrame())
                    || nextRequested)
                {
                    nextRequested = false;
                    skipRequested = true;
                    break;
                }

                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }

        isTyping = false;
    }

    /// <summary>
    /// Raccourci pratique avec paramètres par défaut (pour Signals).
    /// </summary>
    public void PlayDialogueAuto(DialogueContainer container)
    {
        PlayDialogueAuto(container, 0.03f, 0.5f, 2.5f, true, null);
    }

    //==============================================================
    // API rétro-compat (tableaux de lignes)
    //==============================================================

    public void PlayDialogue(DialogueLine[] lines, System.Action onEnd = null)
    {
        var temp = ScriptableObject.CreateInstance<DialogueContainer>();
        temp.lines = lines;
        temp.randomPosition = false;
        PlayDialogue(temp, onEnd);
    }

    public IEnumerator StartDialogue(DialogueLine[] lines)
    {
        var temp = ScriptableObject.CreateInstance<DialogueContainer>();
        temp.lines = lines;
        temp.randomPosition = false;
        return StartDialogue(temp);
    }

    public void PlayDialogue(DialogueLine[] lines)
    {
        PlayDialogue(lines, null);
    }

    public void PlayDialogue(DialogueContainer container)
    {
        PlayDialogue(container, null);
    }

    //==============================================================
    // Séquences de DialogueContainer (NextDialogue = prochain container)
    //==============================================================

    /// <summary>
    /// Prépare une séquence de containers. Appelle ensuite StartDialogueSequence() pour lancer.
    /// </summary>
    public void SetDialogueSequence(
        IList<DialogueContainer> containers,
        bool autoAdvancePerContainer = false,
        float timePerChar = 0.03f,
        float minHold = 0.5f,
        float maxHold = 2.5f,
        bool useUnscaledTime = true,
        bool keepControlsLocked = true)
    {
        // Ferme proprement tout dialogue en cours avant de préparer une nouvelle séquence.
        ResetDialogueState();

        sequence.Clear();
        if (containers != null) sequence.AddRange(containers);
        sequenceIndex = -1;

        sequenceAuto = autoAdvancePerContainer;
        seq_timePerChar = timePerChar;
        seq_minHold = minHold;
        seq_maxHold = maxHold;
        seq_unscaled = useUnscaledTime;

        lockControlsDuringSequence = keepControlsLocked;
    }

    /// <summary>
    /// Lance la séquence (équivaut à appeler NextDialogue une première fois).
    /// </summary>
    public void StartDialogueSequence()
    {
        if (sequence == null || sequence.Count == 0) return;

        inSequence = true;

        // Verrouille au début de la séquence si demandé
        if (lockControlsDuringSequence)
            TogglePlayerMovement(false);

        sequenceIndex = -1;
        NextDialogue(); // va afficher le 1er container
    }

    /// <summary>
    /// Passe au DialogueContainer suivant dans la séquence.
    /// Si un container est en cours, on le coupe proprement avant.
    /// </summary>
    public void NextDialogue()
    {
        if (sequence == null || sequence.Count == 0) return;

        // Stoppe le container en cours
        if (isOpen)
        {
            StopAllCoroutines();
            GetComponentInChildren<Animator>()?.Play("DialogueBoxClose");
            isOpen = false;
        }

        sequenceIndex++;

        if (sequenceIndex >= 0 && sequenceIndex < sequence.Count)
        {
            var container = sequence[sequenceIndex];

            if (sequenceAuto)
            {
                StartCoroutine(StartDialogueAuto(container, seq_timePerChar, seq_minHold, seq_maxHold, seq_unscaled));
            }
            else
            {
                StartCoroutine(StartDialogue(container));
            }
        }
        else
        {
            // Fin de séquence
            inSequence = false;

            if (lockControlsDuringSequence)
                TogglePlayerMovement(true);

            sequence.Clear();
            sequenceIndex = -1;
        }
    }

    //==============================================================
    // Option : passer à la ligne suivante à l'intérieur du container courant
    //==============================================================

    /// <summary>
    /// Passe à la ligne suivante si un dialogue est ouvert :
    /// - en frappe : skip la frappe
    /// - sinon : déclenche l'attente pour passer à la prochaine ligne (mode manuel)
    /// </summary>
    public void NextLine()
    {
        if (!isOpen) return;

        // Mode Éditeur : avance simplement dans le container prévisualisé
        if (!Application.isPlaying)
        {
            if (previewContainer == null || previewContainer.lines == null)
                return;

            previewLineIndex++;

            if (previewLineIndex < previewContainer.lines.Length)
            {
                var line = previewContainer.lines[previewLineIndex];
                nameText.text = line.speakerName;
                dialogueText.text = line.text;
            }
            else
            {
                // Fin de prévisualisation : on ferme visuellement le dialogue
                previewContainer = null;
                isOpen = false;
            }

            return;
        }

        // Mode Play : logique habituelle
        if (isTyping)
        {
            skipRequested = true; // affiche la ligne instantanément
        }
        else
        {
            nextRequested = true; // consomme l'attente (manuel) ou casse l'attente (auto)
        }
    }

    //==============================================================
    // Positionnement de la bulle
    //==============================================================

    /// <summary>
    /// Positionne la bulle de dialogue soit aléatoirement, soit à la position custom (container.customPosition).
    /// </summary>
    private void PositionDialogueBox(DialogueContainer container)
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        RectTransform canvasRect = rectTransform != null ? rectTransform.parent as RectTransform : null;

        if (canvasRect == null)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();
            }
            else
            {
                Debug.LogWarning("DialogueManager : aucun Canvas parent trouvé pour positionner la bulle de dialogue.");
                return;
            }
        }

        Vector2 newPos = Vector2.zero;
        float margin = 50f;

        if (container.randomPosition)
        {
            float xMin = -canvasRect.rect.width / 2 + rectTransform.rect.width / 2 + margin;
            float xMax = canvasRect.rect.width / 2 - rectTransform.rect.width / 2 - margin;
            float yMin = -canvasRect.rect.height / 2 + rectTransform.rect.height / 2 + margin;
            float yMax = canvasRect.rect.height / 2 - rectTransform.rect.height / 2 - margin;

            newPos = new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
        }
        else
        {
            newPos = new Vector2(container.customPosition.x, container.customPosition.y);
        }

        rectTransform.anchoredPosition = newPos;
    }
}
