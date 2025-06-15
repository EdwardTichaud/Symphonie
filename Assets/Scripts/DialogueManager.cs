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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayDialogue(DialogueLine[] lines, System.Action onEnd = null)
    {
        StopAllCoroutines();
        onDialogueEndCallback = onEnd;
        StartCoroutine(StartDialogue(lines));
    }

    public IEnumerator StartDialogue(DialogueLine[] lines)
    {
        GetComponent<Animator>()?.Play("DialogueBoxOpen");
        isOpen = true;

        foreach (DialogueLine line in lines)
        {
            nameText.text = line.speakerName;
            yield return StartCoroutine(TypeLine(line.text));
            yield return new WaitUntil(() => InputsManager.Instance.playerInputs.Player.Action.WasPressedThisFrame());
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

            if (InputsManager.Instance.playerInputs.Player.Action.WasPressedThisFrame())
            {
                skipRequested = true;
            }
        }

        isTyping = false;
    }

    // Optionnel : dialogue sans pause timeline
    public void PlayDialogue(DialogueLine[] lines)
    {
        PlayDialogue(lines, null);
    }
}
