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
        // Place la bulle selon la configuration du dialogue
        PositionDialogueBox(container);

        GetComponent<Animator>()?.Play("DialogueBoxOpen");
        isOpen = true;

        foreach (DialogueLine line in container.lines)
        {
            nameText.text = line.speakerName;
            yield return StartCoroutine(TypeLine(line.text));
            yield return new WaitUntil(() => InputsManager.Instance.playerInputs.World.Action.WasPressedThisFrame());
        }

        GetComponent<Animator>()?.Play("DialogueBoxClose");
        isOpen = false;

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
        temp.randomPosition = true; // Par défaut, position aléatoire
        PlayDialogue(temp, onEnd);
    }

    public IEnumerator StartDialogue(DialogueLine[] lines)
    {
        var temp = ScriptableObject.CreateInstance<DialogueContainer>();
        temp.lines = lines;
        temp.randomPosition = true;
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
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                canvasRect = parentCanvas.GetComponent<RectTransform>();
            }
            else
            {
                // Si aucun Canvas n'est présent, on abandonne proprement en avertissant.
                Debug.LogWarning("DialogueManager : aucun Canvas parent trouvé pour positionner la bulle de dialogue.");
                return;
            }
        }

        Vector2 newPos = Vector2.zero;
        float margin = 50f; // Marge pour éviter les bords

        if (container.randomPosition)
        {
            // Calcule des limites sûres à l'intérieur du canvas
            float xMin = -canvasRect.rect.width / 2 + rectTransform.rect.width / 2 + margin;
            float xMax = canvasRect.rect.width / 2 - rectTransform.rect.width / 2 - margin;
            float yMin = -canvasRect.rect.height / 2 + rectTransform.rect.height / 2 + margin;
            float yMax = canvasRect.rect.height / 2 - rectTransform.rect.height / 2 - margin;

            newPos = new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
        }
        else
        {
            // Utilise la position fournie (x, y) sans toucher au z
            newPos = new Vector2(container.customPosition.x, container.customPosition.y);
        }

        rectTransform.anchoredPosition = newPos;
    }
}
