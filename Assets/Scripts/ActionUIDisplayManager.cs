using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ActionUIDisplayManager : MonoBehaviour
{
    public static ActionUIDisplayManager Instance { get; private set; }

    [Header("UI Elements")]
    public CanvasGroup displayGroup;
    public TextMeshProUGUI actionText;
    public float displayDuration = 5f;
    public float fadeDuration = 0.5f;

    private Coroutine currentDisplayRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (displayGroup != null)
            displayGroup.alpha = 0f;
    }

    public void DisplayAttackName(string attackName)
    {
        if (currentDisplayRoutine != null)
            StopCoroutine(currentDisplayRoutine);

        currentDisplayRoutine = StartCoroutine(DisplayRoutine(attackName));
    }

    private IEnumerator DisplayRoutine(string name)
    {
        actionText.text = name;
        displayGroup.alpha = 1;
        yield return new WaitForSeconds(displayDuration);
        displayGroup.alpha = 0;
    }

    public void DisplayMelodyDiscovery(string melodyName)
    {
        string message = $"♪ Nouvelle mélodie découverte : {melodyName} ♪";
        StartCoroutine(DisplayRoutine(message));
    }

    public void DisplayInstruction(string instruction)
    {
        ShowMessage(instruction);
    }

    public void DisplayInstruction_SelectItemOrSkill()
    {
        ShowMessage("Choisissez un objet ou une compétence");
    }

    public void DisplayInstruction_SelectItem()
    {
        ShowMessage("Choisissez un objet");
    }

    public void DisplayInstruction_SelectSkill()
    {
        ShowMessage("Choisissez une compétence");
    }

    public void DisplayInstruction_SelectGroup()
    {
        ShowMessage("Choisissez un groupe sur lequel affecter l'action : Ennemis (←) ou Alliés (→)");
    }

    public void DisplayInstruction_SelectTarget()
    {
        ShowMessage("Sélectionnez une cible");
    }

    public void DisplayInstruction_ConfirmOrCancel()
    {
        ShowMessage("Validez (A/Enter) ou Annulez (B/Echap)");
    }

    public void DisplayInstruction_ExecuteQTE()
    {
        ShowMessage("Appuyez en rythme !");
    }

    private void ShowMessage(string message)
    {
        if (currentDisplayRoutine != null)
            StopCoroutine(currentDisplayRoutine);

        currentDisplayRoutine = StartCoroutine(DisplayRoutine(message));
    }
}
