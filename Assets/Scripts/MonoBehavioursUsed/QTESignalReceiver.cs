using UnityEngine;

/// <summary>
/// Récepteur appelé depuis une Timeline pour lancer un QTE.
/// </summary>
public class QTESignalReceiver : MonoBehaviour
{
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
        RhythmQTEManager.Instance?.TriggerQTE(trigger.windowDelay, trigger.inputIcon, trigger.uiPosition);
    }
}
