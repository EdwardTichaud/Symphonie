using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire centralisé des succès du jeu.
/// Un singleton simple pour enregistrer et débloquer les succès
/// depuis n'importe quel endroit du projet.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    /// <summary>
    /// Instance unique accessible globalement.
    /// </summary>
    public static AchievementManager Instance { get; private set; }

    [Tooltip("Liste de tous les succès disponibles dans le jeu.")]
    public List<AchievementSO> achievements = new List<AchievementSO>();

    private void Awake()
    {
        // Mise en place du singleton. Si une autre instance existe, on la détruit
        // pour conserver un gestionnaire unique persistant entre les scènes.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Débloque le succès passé en paramètre s'il ne l'est pas déjà.
    /// </summary>
    /// <param name="achievement">Succès à débloquer.</param>
    public void Unlock(AchievementSO achievement)
    {
        if (achievement == null)
        {
            Debug.LogWarning("[AchievementManager] Succès référencé nul.");
            return;
        }

        if (!achievements.Contains(achievement))
        {
            Debug.LogWarning($"[AchievementManager] Le succès {achievement.name} n'est pas enregistré dans la liste globale.");
        }

        if (achievement.unlocked)
        {
            // Déjà obtenu : on ignore sans erreur.
            return;
        }

        achievement.unlocked = true;

        // Ici on pourrait déclencher des effets : affichage d'une UI, sauvegarde, son, etc.
        Debug.Log($"Succès débloqué : {achievement.nom}");
    }
}
