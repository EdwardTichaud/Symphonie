using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewCameraMotif", menuName = "Symphonie/Camera Motif")]
public class CameraMotifSO : ScriptableObject
{
    public enum ReferencePoint
    {
        Caster,
        Target
    }

    [Header("Blend")]
    [Min(0f)]
    [Tooltip("Durée de transition lorsque le motif devient actif.")]
    public float blendDuration = 0.25f;

    [Header("Reference")]
    [Tooltip("Point de référence utilisé pour positionner la caméra.")]
    public ReferencePoint referencePoint = ReferencePoint.Caster;

    [Header("Reference Offset")]
    [FormerlySerializedAs("casterOffsetLocal")]
    [FormerlySerializedAs("casterOffsetPosition")]
    [Tooltip("Position locale appliquée à la caméra autour du point de référence (X=right, Y=up, Z=forward).")]
    public Vector3 referenceOffsetPosition = Vector3.zero;
    [FormerlySerializedAs("casterOffsetSmoothTime")]
    [Tooltip("Temps de lissage (en secondes) pour suivre l'offset de référence. -1 = conserver la valeur de base.")]
    public float referenceOffsetSmoothTime = -1f;
    [Min(0f)]
    [Tooltip("Délai (en secondes) avant que la caméra suive le point de référence.")]
    public float referenceOffsetDelay = 0f;
    [FormerlySerializedAs("casterOffsetRotationEuler")]
    [FormerlySerializedAs("casterOffsetRotation")]
    [Tooltip("Rotation (en degrés) appliquée à la caméra, dans l'espace local du point de référence.")]
    public Vector3 referenceOffsetRotation = Vector3.zero;

    [Header("Orbit")]
    [Tooltip("Active une rotation continue de la caméra autour du point choisi.")]
    public bool orbitEnabled;

    [Tooltip("Point autour duquel la caméra orbitera.")]
    public ReferencePoint orbitReferencePoint = ReferencePoint.Caster;

    [Tooltip("Vitesse de rotation orbitale (en degrés par seconde).")]
    public float orbitSpeed;

    [Header("Look At")]
    [Tooltip("Force la caméra à regarder un point de référence.")]
    public bool lookAtEnabled;

    [Tooltip("Point regardé par la caméra lorsqu'un Look At est actif.")]
    public ReferencePoint lookAtReferencePoint = ReferencePoint.Caster;
}
