using UnityEngine;

/// <summary>
/// Objet scriptable représentant un succès à débloquer.
/// Permet de définir un identifiant, un nom visible et une
/// description. L'état de déblocage est également stocké
/// pour pouvoir persister cette information.
/// </summary>
[CreateAssetMenu(fileName = "NewAchievement", menuName = "Achievements/Achievement")]
public class AchievementSO : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Identifiant unique du succès, utilisé pour le débloquer via le code.")]
    public string id;

    [Tooltip("Nom affiché au joueur lorsque le succès est obtenu.")]
    public string nom;

    [TextArea]
    [Tooltip("Description détaillant comment obtenir ce succès.")]
    public string description;

    [Header("État")] 
    [Tooltip("Indique si le succès a déjà été débloqué.")]
    public bool unlocked;
}
