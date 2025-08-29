using UnityEngine;

/// <summary>
/// Récepteur appelé depuis une Timeline pour lancer un QTE.
/// L'attribut <see cref="ExecuteAlways"/> autorise la prévisualisation
/// des QTE dans l'Éditeur afin de régler précisément leur placement
/// sans lancer le jeu.
/// </summary>
[ExecuteAlways]
public class QTESignalReceiver : MonoBehaviour
{
    /// <summary>
    /// Déclenche un QTE via le RhythmQTEManager en utilisant les données du QTETrigger.
    /// </summary>
    /// <param name="trigger">Données du QTE à jouer</param>
    public void TriggerQTE(QTETriggerSO trigger)
    {
        // Méthode utilisée aussi bien en mode Éditeur qu'en jeu. Les
        // vérifications permettent d'éviter les erreurs de référence
        // durant la prévisualisation.
        if (trigger == null)
        {
            Debug.LogWarning("[QTESignalReceiver] TriggerQTE appelé avec null.");
            return;
        }
        RhythmQTEManager.Instance?.TriggerQTE(trigger.windowDelay, trigger.inputIcon, trigger.uiPosition);
    }
}
