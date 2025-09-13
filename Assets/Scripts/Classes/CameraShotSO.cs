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
    [Header("Paramètres de cadrage")]
    [Tooltip("Champ de vision de la caméra pendant ce plan.")]
    public float fieldOfView = 40f;

    [Tooltip("Décalage appliqué par rapport à la cible suivie.")]
    public Vector3 offset = new Vector3(0f, 3f, -5f);

    [Tooltip("Durée du fondu lors de la transition vers ce plan.")]
    public float blendDuration = 1f;
}
