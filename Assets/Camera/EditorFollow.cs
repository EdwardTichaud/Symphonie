// EditorFollow.cs
// Fait suivre ce GameObject à une "target" avec offset uniquement en mode éditeur.
// Compatible Unity 6 (6000.x)

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor; // Nécessaire pour EditorApplication.update
#endif

[DisallowMultipleComponent]
[ExecuteAlways] // S'exécute aussi hors Play dans l'éditeur
[AddComponentMenu("Camera/Editor Tools/Editor Follow")] // Classement clair dans le menu Add Component
public class EditorFollow : MonoBehaviour
{
    // ====== Paramètres exposés ======

    [Header("Cible")]
    [SerializeField] private Transform target; // Objet suivi

    [Header("Options de suivi")]
    [SerializeField] private bool followPosition = true; // Suivre la position ?
    [SerializeField] private bool followRotation = true; // Suivre la rotation ?

    [Tooltip("Si vrai, l'offset est interprété dans l'espace local de la target (forward/right/up de la target). Sinon, dans l'espace monde.")]
    [SerializeField] private bool offsetInTargetLocalSpace = true;

    [Header("Offset")]
    [Tooltip("Décalage position (monde ou local selon l'option).")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    [Tooltip("Décalage rotation en Euler (appliqué après l'orientation de la target).")]
    [SerializeField] private Vector3 eulerRotationOffset = Vector3.zero;

    [Header("Confort")]
    [Tooltip("Applique un lissage léger dans l'éditeur pour éviter des à-coups lors des déplacements à la souris.")]
    [Range(0f, 1f)]
    [SerializeField] private float editorDamping = 0f; // 0 = instantané

#if UNITY_EDITOR
    // ====== État interne pour le damping ======

    private Vector3 _velPos;      // Vitesse actuelle pour SmoothDamp
    private Vector3 _currentEuler; // Angle courant pour le lissage de rotation

    private void OnEnable()
    {
        // Empêche tout effet si on passe en Play (même dans l'éditeur).
        if (Application.isPlaying)
        {
            enabled = false;
            return;
        }

        // Init rotation courante pour le damping angulaire (trivial)
        _currentEuler = transform.rotation.eulerAngles;

        // Réagit aux changements dans la scène même sans focus
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
    }

    private void OnDisable()
    {
        // Nettoyage : on retire le callback pour éviter les fuites
        EditorApplication.update -= EditorTick;
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        // Mise à jour immédiate quand on change une valeur dans l'inspector
        ApplyFollow(immediate: true);
    }

    // Tick d'éditeur (hors Play)
    private void EditorTick()
    {
        if (this == null) return;            // composant supprimé
        if (Application.isPlaying) return;   // ne rien faire en Play
        if (!isActiveAndEnabled) return;     // composant désactivé

        ApplyFollow(immediate: editorDamping <= 0f);
    }

    /// <summary>
    /// Applique le suivi de la target, avec ou sans lissage.
    /// </summary>
    private void ApplyFollow(bool immediate)
    {
        if (target == null) return;

        // --- Position ---
        if (followPosition)
        {
            Vector3 desiredPos;
            if (offsetInTargetLocalSpace)
                desiredPos = target.TransformPoint(positionOffset);
            else
                desiredPos = target.position + positionOffset;

            if (immediate)
            {
                transform.position = desiredPos; // Placement direct
            }
            else
            {
                // Damping type SmoothDamp
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredPos,
                    ref _velPos,
                    Mathf.Max(0.0001f, editorDamping * 0.2f) // petite constante
                );
            }
        }

        // --- Rotation ---
        if (followRotation)
        {
            Quaternion desiredRot = target.rotation * Quaternion.Euler(eulerRotationOffset);

            if (immediate)
            {
                transform.rotation = desiredRot; // Orientation directe
                _currentEuler = transform.rotation.eulerAngles;
            }
            else
            {
                // Damping angulaire simple sur les eulers (suffisant pour l'éditeur)
                Vector3 desiredEuler = desiredRot.eulerAngles;
                _currentEuler = Vector3.Lerp(
                    _currentEuler,
                    desiredEuler,
                    1f - Mathf.Exp(-10f * Mathf.Max(0.0001f, editorDamping) * Time.deltaTime)
                );
                transform.rotation = Quaternion.Euler(_currentEuler);
            }
        }
    }

    // ====== Utilitaires ======

    [ContextMenu("Capturer l'offset depuis la pose actuelle")]
    private void CaptureOffsetFromCurrentPose()
    {
        if (target == null) return;

        if (offsetInTargetLocalSpace)
        {
            // Convertit la position actuelle du follower en offset local par rapport à la target
            positionOffset = target.InverseTransformPoint(transform.position);

            // Décalage de rotation locale
            Quaternion delta = Quaternion.Inverse(target.rotation) * transform.rotation;
            eulerRotationOffset = delta.eulerAngles;
        }
        else
        {
            positionOffset = transform.position - target.position;
            Quaternion delta = Quaternion.Inverse(target.rotation) * transform.rotation;
            eulerRotationOffset = delta.eulerAngles; // reste relatif à la target
        }

        // Forcer mise à jour dans l'éditeur
        OnValidate();
    }

    [ContextMenu("Aligner immédiatement sur la target (ignorer offset)")]
    private void SnapToTargetNoOffset()
    {
        if (target == null) return;
        transform.position = target.position;
        transform.rotation = target.rotation;
    }
#endif // UNITY_EDITOR
}

