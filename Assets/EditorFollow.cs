// EditorFollow.cs
// Fait suivre ce GameObject à une "target" avec offset, uniquement en mode Éditeur.
// Compatible Unity 6 (6000.x)

#if UNITY_EDITOR
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways] // S'exécute aussi hors Play dans l'Éditeur
[AddComponentMenu("Tools/Editor Only/Editor Follow")]
public class EditorFollow : MonoBehaviour
{
    [Header("Cible")]
    [SerializeField] private Transform target;

    [Header("Options de suivi")]
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = true;
    [Tooltip("Si vrai, l'offset est interprété dans l'espace local de la target (forward/right/up de la target). Sinon, dans l'espace monde.")]
    [SerializeField] private bool offsetInTargetLocalSpace = true;

    [Header("Offset")]
    [Tooltip("Décalage position (monde ou local selon l'option).")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [Tooltip("Décalage rotation en Euler (appliqué après l'orientation de la target).")]
    [SerializeField] private Vector3 eulerRotationOffset = Vector3.zero;

    [Header("Confort")]
    [Tooltip("Applique un lissage léger dans l'Éditeur pour éviter des à-coups lors des déplacements à la souris.")]
    [Range(0f, 1f)]
    [SerializeField] private float editorDamping = 0f; // 0 = instantané

    // État interne pour le damping
    private Vector3 _velPos;
    private Vector3 _currentEuler;

    private void OnEnable()
    {
        // Empêche tout effet si on passe en Play (même dans l'Éditeur).
        if (Application.isPlaying)
        {
            enabled = false;
            return;
        }

        // Init rotation courante pour le damping angulaire (trivial)
        _currentEuler = transform.rotation.eulerAngles;

        // Réagit aux changements dans la scène même sans focus
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.update += EditorTick;
    }

    private void OnDisable()
    {
        UnityEditor.EditorApplication.update -= EditorTick;
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        // Mise à jour immédiate quand on change une valeur dans l'inspecteur
        ApplyFollow(immediate: true);
    }

    // Tick d'Éditeur (hors Play)
    private void EditorTick()
    {
        if (this == null) return; // composant supprimé
        if (Application.isPlaying) return; // ne rien faire en Play
        if (!isActiveAndEnabled) return;

        ApplyFollow(immediate: editorDamping <= 0f);
    }

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
                transform.position = desiredPos;
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
                transform.rotation = desiredRot;
                _currentEuler = transform.rotation.eulerAngles;
            }
            else
            {
                // Damping angulaire simple sur les eulers (suffisant pour l'Éditeur)
                Vector3 desiredEuler = desiredRot.eulerAngles;
                _currentEuler = Vector3.Lerp(_currentEuler, desiredEuler, 1f - Mathf.Exp(-10f * Mathf.Max(0.0001f, editorDamping) * Time.deltaTime));
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

        // Forcer MAJ
        OnValidate();
    }

    [ContextMenu("Aligner immédiatement sur la target (ignorer offset)")]
    private void SnapToTargetNoOffset()
    {
        if (target == null) return;
        transform.position = target.position;
        transform.rotation = target.rotation;
    }
}
#endif
