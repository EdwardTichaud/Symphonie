using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;
    public string text;
}

public class DialogueManager : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI nameText;
    public float typingSpeed = 0.01f;

    private bool isTyping = false;
    private bool skipRequested = false;

    public bool isOpen;
    public static DialogueManager Instance { get; private set; }

    private System.Action onDialogueEndCallback;
    // Référence rapide au RectTransform pour repositionner la bulle
    private RectTransform rectTransform;

    /// <summary>
    /// Active ou désactive les actions de déplacement du joueur.
    /// Cela empêche Lucian de se déplacer pendant les dialogues.
    /// </summary>
    /// <param name="enable">True pour réactiver les actions de déplacement, false pour les bloquer.</param>
    private void TogglePlayerMovement(bool enable)
    {
        if (InputsManager.Instance == null) return;

        var world = InputsManager.Instance.playerInputs.World;

        if (enable)
        {
            // Réactivation des actions liées au déplacement.
            world.Move.Enable();
            world.Run.Enable();
            world.Jump.Enable();
            world.Dash.Enable();
        }
        else
        {
            // Désactivation des actions liées au déplacement pour figer le joueur.
            world.Move.Disable();
            world.Run.Disable();
            world.Jump.Disable();
            world.Dash.Disable();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        rectTransform = GetComponent<RectTransform>();
    }

    // --- Nouvelle API utilisant DialogueContainer ---
    public void PlayDialogue(DialogueContainer container, System.Action onEnd = null)
    {
        StopAllCoroutines();
        onDialogueEndCallback = onEnd;
        StartCoroutine(StartDialogue(container));
    }

    public IEnumerator StartDialogue(DialogueContainer container)
    {
        // Joue l'animation d'ouverture de la bulle
        GetComponentInChildren<Animator>()?.Play("DialogueBoxOpen");
        isOpen = true;

        // Pendant une interaction, on bloque les entrées de déplacement du joueur
        // afin qu'il ne puisse pas se déplacer pendant qu'un dialogue est affiché.
        TogglePlayerMovement(false);

        // Attendre une frame pour que l'animation initialise correctement l'UI
        // puis placer la bulle selon la configuration du dialogue.
        yield return null;

        // Position aléatoire ou personnalisée selon les paramètres du container
        PositionDialogueBox(container);

        foreach (DialogueLine line in container.lines)
        {
            nameText.text = line.speakerName;
            yield return StartCoroutine(TypeLine(line.text));
            yield return new WaitUntil(() => InputsManager.Instance.playerInputs.World.Action.WasPressedThisFrame());
        }

        GetComponentInChildren<Animator>()?.Play("DialogueBoxClose");
        isOpen = false;

        // Le dialogue est terminé : on réactive les entrées de déplacement.
        TogglePlayerMovement(true);

        onDialogueEndCallback?.Invoke();
        onDialogueEndCallback = null;
    }

    IEnumerator TypeLine(string line)
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
            yield return new WaitForSeconds(typingSpeed);

            if (InputsManager.Instance.playerInputs.World.Action.WasPressedThisFrame())
            {
                skipRequested = true;
            }
        }

        isTyping = false;
    }

    // --- Compatibilité rétroactive pour les anciens appels ---
    public void PlayDialogue(DialogueLine[] lines, System.Action onEnd = null)
    {
        var temp = ScriptableObject.CreateInstance<DialogueContainer>();
        temp.lines = lines;
        // Par défaut on place le DialoguePanel à la position personnalisée (0,0).
        // Pour un placement aléatoire il faudra activer randomPosition dans le container.
        temp.randomPosition = false; // false => position personnalisée, true => position aléatoire
        PlayDialogue(temp, onEnd);
    }

    public IEnumerator StartDialogue(DialogueLine[] lines)
    {
        var temp = ScriptableObject.CreateInstance<DialogueContainer>();
        temp.lines = lines;
        // Même logique que ci-dessus : par défaut la position est personnalisée
        // (0,0 correspond au centre de la caméra).
        temp.randomPosition = false;
        yield return StartDialogue(temp);
    }

    // Optionnel : dialogue sans pause timeline
    public void PlayDialogue(DialogueLine[] lines)
    {
        PlayDialogue(lines, null);
    }

    public void PlayDialogue(DialogueContainer container)
    {
        PlayDialogue(container, null);
    }

    /// <summary>
    /// Positionne la bulle de dialogue soit aléatoirement, soit à une position
    /// spécifique définie dans le DialogueContainer.
    /// </summary>
    private void PositionDialogueBox(DialogueContainer container)
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        RectTransform canvasRect = rectTransform.parent as RectTransform;

        // Évite les NullReference si le RectTransform parent n'est pas trouvé.
        // On tente de récupérer le Canvas parent pour calculer une position valide.
        if (canvasRect == null)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();
            }
            else
            {
                // Si aucun Canvas n'est présent, on abandonne proprement en avertissant.
                Debug.LogWarning("DialogueManager : aucun Canvas parent trouvé pour positionner la bulle de dialogue.");
                return;
            }
        }

        Vector2 newPos = Vector2.zero;
        float margin = 50f; // Marge pour éviter que la bulle ne soit coupée par les bords

        if (container.randomPosition)
        {
            // Si randomPosition estcoché, on calcule une position aléatoire
            // tout en respectant la marge de sécurité autour du cadre de la caméra
            float xMin = -canvasRect.rect.width / 2 + rectTransform.rect.width / 2 + margin;
            float xMax = canvasRect.rect.width / 2 - rectTransform.rect.width / 2 - margin;
            float yMin = -canvasRect.rect.height / 2 + rectTransform.rect.height / 2 + margin;
            float yMax = canvasRect.rect.height / 2 - rectTransform.rect.height / 2 - margin;

            newPos = new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
        }
        else
        {
            // Sinon on utilise la position personnalisée définie dans le DialogueContainer
            // (0,0,0 correspond au centre de la caméra)
            newPos = new Vector2(container.customPosition.x, container.customPosition.y);
        }

        rectTransform.anchoredPosition = newPos;
    }
}
