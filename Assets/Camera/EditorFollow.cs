// EditorFollow.cs
// Fait suivre ce GameObject à une "target" avec offset uniquement en mode éditeur.
// Compatible Unity 6 (6000.x)

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Camera/Editor Tools/Editor Follow")]
public class EditorFollow : MonoBehaviour
{
    public bool Enabled = true; // Activer le suivi ?

    [Header("Cible")]
    [SerializeField] private Transform target;

    [Header("Options de suivi")]
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = true;

    [SerializeField] private bool offsetInTargetLocalSpace = true;

    [Header("Offset")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 eulerRotationOffset = Vector3.zero;

    [Header("Confort")]
    [Range(0f, 1f)]
    [SerializeField] private float editorDamping = 0f;

#if UNITY_EDITOR
    private Vector3 _velPos;
    private Vector3 _currentEuler;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            enabled = false;
            return;
        }

        _currentEuler = transform.rotation.eulerAngles;

        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorTick;
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // 🔥 Ajout : ignorer si non activé
        if (!Enabled) return;

        ApplyFollow(immediate: true);
    }

    private void EditorTick()
    {
        if (this == null) return;
        if (Application.isPlaying) return;
        if (!isActiveAndEnabled) return;

        // 🔥 Ajout : ignorer si non activé
        if (!Enabled) return;

        ApplyFollow(immediate: editorDamping <= 0f);
    }

    private void ApplyFollow(bool immediate)
    {
        // 🔥 Ajout : ignorer si non activé
        if (!Enabled) return;

        if (target == null) return;

        // --- Position ---
        if (followPosition)
        {
            Vector3 desiredPos = offsetInTargetLocalSpace
                ? target.TransformPoint(positionOffset)
                : target.position + positionOffset;

            if (immediate)
            {
                transform.position = desiredPos;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredPos,
                    ref _velPos,
                    Mathf.Max(0.0001f, editorDamping * 0.2f)
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

    [ContextMenu("Capturer l'offset depuis la pose actuelle")]
    private void CaptureOffsetFromCurrentPose()
    {
        if (target == null) return;

        if (offsetInTargetLocalSpace)
        {
            positionOffset = target.InverseTransformPoint(transform.position);

            Quaternion delta = Quaternion.Inverse(target.rotation) * transform.rotation;
            eulerRotationOffset = delta.eulerAngles;
        }
        else
        {
            positionOffset = transform.position - target.position;

            Quaternion delta = Quaternion.Inverse(target.rotation) * transform.rotation;
            eulerRotationOffset = delta.eulerAngles;
        }

        OnValidate();
    }

    [ContextMenu("Aligner immédiatement sur la target (ignorer offset)")]
    private void SnapToTargetNoOffset()
    {
        if (target == null) return;
        transform.position = target.position;
        transform.rotation = target.rotation;
    }
#endif
}
