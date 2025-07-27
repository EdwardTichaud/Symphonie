using UnityEngine;

/// <summary>
/// Récepteur appelé depuis une Timeline pour lancer un QTE.
/// </summary>
public class QTESignalReceiver : MonoBehaviour
{
    /// <summary>
    /// Événement global déclenché lorsqu'un QTE doit être lancé.
    /// Permet aux gestionnaires de QTE de s'abonner dynamiquement selon la timeline en cours.
    /// </summary>
    public static event System.Action<QTETriggerSO> OnQTERequested;
    /// <summary>
    /// Déclenche un QTE via le RhythmQTEManager en utilisant les données du QTETrigger.
    /// </summary>
    /// <param name="trigger">Données du QTE à jouer</param>
    public void TriggerQTE(QTETriggerSO trigger)
    {
        if (trigger == null)
        {
            Debug.LogWarning("[QTESignalReceiver] TriggerQTE appelé avec null.");
            return;
        }

        // Priorité à l'événement si des gestionnaires sont abonnés
        if (OnQTERequested != null)
        {
            OnQTERequested.Invoke(trigger);
            return;
        }

        // Fallback vers le gestionnaire global par défaut
        RhythmQTEManager.Instance?.TriggerQTE(trigger.windowDelay, trigger.inputIcon, trigger.uiPosition);
    }
}
