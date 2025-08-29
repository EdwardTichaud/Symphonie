using UnityEngine;

/// <summary>
/// Récepteur de signaux Timeline dédié au déblocage des succès.
/// Il suffit de l'ajouter à un objet de la scène et de relier
/// la méthode <see cref="TriggerAchievement"/> à un Signal Emitter
/// dans une Timeline pour débloquer facilement un succès.
/// </summary>
public class AchievementSignalReceiver : MonoBehaviour
{
    /// <summary>
    /// Appelé depuis la Timeline avec une référence vers le succès à débloquer.
    /// </summary>
    /// <param name="achievement">Succès ciblé.</param>
    public void TriggerAchievement(AchievementSO achievement)
    {
        if (AchievementManager.Instance == null)
        {
            Debug.LogWarning("[AchievementSignalReceiver] Aucun AchievementManager présent dans la scène.");
            return;
        }

        AchievementManager.Instance.Unlock(achievement);
    }
}
