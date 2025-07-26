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
    public float displayDuration = 5f;  // Durée potentielle d'affichage (non utilisée pour l'instant)
    public float fadeDuration = 0.5f;    // Durée de fondu éventuel

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

    // Affiche simplement le nom d'une attaque
    // Le message restera à l'écran jusqu'à ce qu'il soit remplacé
    public void DisplayAttackName(string attackName)
    {
        ShowMessage(attackName);
    }

    public void DisplayActionMessage(string actorName, string actionName, string targetName)
    {
        string message = $"{actorName} utilise {actionName}";
        if (!string.IsNullOrEmpty(targetName))
            message += $" sur {targetName}";
        ShowMessage(message);
    }


    /// <summary>
    /// Affiche un message lorsque le joueur découvre une nouvelle attaque musicale.
    /// </summary>
    /// <param name="melodyName">Nom de l'attaque</param>
    public void DisplayMoveDiscovery(string melodyName)
    {
        string message = $"Mouvement découvert : {melodyName}";
        ShowMessage(message);
    }

    /// <summary>
    /// Affiche la préparation de l'attaque d'un ennemi.
    /// Si moveName est null ou vide, indique qu'il s'agit d'un mouvement inconnu.
    /// </summary>
    public void DisplayEnemyPreparation(string enemyName, string moveName)
    {
        string message = string.IsNullOrEmpty(moveName)
            ? $"{enemyName} s'apprête à jouer un mouvement inconnu"
            : $"{enemyName} s'apprête à jouer {moveName}";
        ShowMessage(message);
    }

    public void DisplayInstruction(string instruction)
    {
        ShowMessage(instruction);
    }

    public void DisplayInstruction_SelectItemOrSkill()
    {
        ShowMessage("Choisissez un objet ou une compétence");
    }

    public void DisplayInstruction_SelectItemSkillOrPass()
    {
        ShowMessage("Choisissez un objet, une compétence ou passez le tour (Retour)");
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

    public void DisplayInstruction_TargetTooFar()
    {
        ShowMessage("Cible trop éloignée");
    }

    public void DisplayInstruction_NotEnoughHarmonics()
    {
        ShowMessage("Pas assez d'harmoniques");
    }

    public void DisplayInstruction_MoveOnCooldown()
    {
        ShowMessage("Compétence en recharge");
    }

    /// <summary>
    /// Affiche un message d'esquive.
    /// </summary>
    public void DisplayDodge(string unitName)
    {
        string message = $"{unitName} esquive !";
        ShowMessage(message);
    }

    /// <summary>
    /// Affiche un message de parade.
    /// </summary>
    public void DisplayParry(string unitName)
    {
        string message = $"{unitName} pare l'attaque !";
        ShowMessage(message);
    }

    /// <summary>
    /// Affiche un message lorsque l'unité subit des dégâts.
    /// Utilise coup dévastateur si l'attaque est très puissante.
    /// </summary>
    public void DisplayDamage(string unitName, bool devastating)
    {
        string message = devastating
            ? $"{unitName} subit un coup dévastateur !"
            : $"{unitName} subit des dégâts.";
        ShowMessage(message);
    }

    public void DisplayInterceptionResult(bool success)
    {
        string message = success ? "Interception réussie !" : "Interception échouée";
        ShowMessage(message);
    }

    public void DisplayInterceptionAttempt()
    {
        ShowMessage("Tentative d'interception...");
    }

    /// <summary>
    /// Affiche un message à l'écran sans délai d'expiration.
    /// Le texte reste visible jusqu'à ce qu'un autre message soit envoyé.
    /// </summary>
    private void ShowMessage(string message)
    {
        // Mise à jour immédiate du texte affiché
        actionText.text = message;

        // On s'assure que le groupe est visible
        if (displayGroup != null)
            displayGroup.alpha = 1f;
    }
}
