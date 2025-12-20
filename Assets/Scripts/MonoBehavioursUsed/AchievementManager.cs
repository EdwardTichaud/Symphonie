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

    [Tooltip("Liste de tous les succès disponibles et non encore débloqués.")]
    public List<AchievementSO> achievements = new List<AchievementSO>();

    [Tooltip("Succès déjà débloqués par le joueur.")]
    public List<AchievementSO> unlockedAchievements = new List<AchievementSO>();

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

        // Applique immédiatement l'état chargé si une sauvegarde a déjà été lue.
        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
            GameManager.Instance.gameData.ApplyAchievementsTo(this);
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

        if (achievement.unlocked)
        {
            // Déjà obtenu : on ignore sans erreur.
            return;
        }

        achievement.unlocked = true;

        // Lorsque le succès est débloqué, on le retire de la liste
        // des succès disponibles et on l'ajoute à la liste des succès
        // débloqués afin de garder l'inspecteur organisé.
        if (achievements.Contains(achievement))
        {
            achievements.Remove(achievement);
            unlockedAchievements.Add(achievement);
        }
        else if (!unlockedAchievements.Contains(achievement))
        {
            // Si le succès n'est dans aucune liste, on l'ajoute tout de même
            // aux débloqués pour ne pas perdre la référence.
            unlockedAchievements.Add(achievement);
            Debug.LogWarning($"[AchievementManager] Le succès {achievement.name} n'était référencé dans aucune liste.");
        }

        // Ici on pourrait déclencher des effets : affichage d'une UI, sauvegarde, son, etc.
        Debug.Log($"Succès débloqué : {achievement.nom}");
    }

    /// <summary>
    /// Construit la liste des IDs à persister à partir des succès débloqués.
    /// </summary>
    public List<string> BuildUnlockedAchievementIds()
    {
        var ids = new HashSet<string>();
        foreach (var achievement in GetAllAchievements())
        {
            if (achievement == null || !achievement.unlocked)
                continue;

            string id = ResolveAchievementId(achievement);
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        return new List<string>(ids);
    }

    /// <summary>
    /// Applique une liste d'IDs sauvegardés et reconstruit les listes internes.
    /// </summary>
    public void ApplyUnlockedAchievementIds(IEnumerable<string> ids)
    {
        var idSet = new HashSet<string>(ids ?? System.Array.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);

        var locked = new List<AchievementSO>();
        var unlocked = new List<AchievementSO>();

        foreach (var achievement in GetAllAchievements())
        {
            if (achievement == null)
                continue;

            string id = ResolveAchievementId(achievement);
            bool shouldUnlock = !string.IsNullOrEmpty(id) && idSet.Contains(id);
            achievement.unlocked = shouldUnlock;

            if (shouldUnlock)
                unlocked.Add(achievement);
            else
                locked.Add(achievement);
        }

        achievements = locked;
        unlockedAchievements = unlocked;
    }

    private IEnumerable<AchievementSO> GetAllAchievements()
    {
        var unique = new HashSet<AchievementSO>();

        if (achievements != null)
        {
            foreach (var achievement in achievements)
            {
                if (achievement != null && unique.Add(achievement))
                    yield return achievement;
            }
        }

        if (unlockedAchievements != null)
        {
            foreach (var achievement in unlockedAchievements)
            {
                if (achievement != null && unique.Add(achievement))
                    yield return achievement;
            }
        }
    }

    private static string ResolveAchievementId(AchievementSO achievement)
    {
        if (achievement == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(achievement.id))
            return achievement.id;

        // Fallback: utiliser le nom d'asset si l'ID n'a pas été renseigné.
        return achievement.name;
    }
}
