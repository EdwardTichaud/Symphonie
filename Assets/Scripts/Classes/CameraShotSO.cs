using UnityEngine;

// Ce ScriptableObject est volontairement indépendant de Cinemachine afin de
// pouvoir être utilisé même si le package n'est pas présent dans le projet.

/// <summary>
/// ScriptableObject décrivant un plan de caméra réutilisable.
/// Permet aux game designers de configurer l'angle de vue
/// indépendamment des timelines d'animations.
/// </summary>
[CreateAssetMenu(fileName = "NewCameraShot", menuName = "Symphonie/Camera Shot")]
public class CameraShotSO : ScriptableObject
{
    /// <summary>
    /// Permet de choisir quel Transform la caméra doit suivre.
    /// </summary>
    public enum ShotTarget
    {
        /// <summary>Le lanceur de l'action.</summary>
        Caster,
        /// <summary>La cible principale de l'action.</summary>
        Target
    }

    [Header("Paramètres de cadrage")]

    [Tooltip("Cible suivie pendant ce plan : le lanceur (Caster) ou la cible (Target).")]
    public ShotTarget targetToFollow = ShotTarget.Target;

    [Tooltip("Distance à conserver par rapport à la cible suivie.")]
    public float distance = 5f;

    [Tooltip("Champ de vision de la caméra pendant ce plan.")]
    public float fieldOfView = 60f;

    [Tooltip("Décalage local appliqué par rapport à la cible suivie.")]
    public Vector3 offset = new Vector3(0f, 3f, 0f);

    [Tooltip("Durée du fondu lors de la transition vers ce plan.")]
    public float blendDuration = 1f;
}
