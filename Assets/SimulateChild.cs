using UnityEngine;

/// <summary>
/// Superpose ce GameObject sur un target (position / rotation / échelle en option).
/// - Par défaut : copie parfaite (aucun lissage).
/// - Optionnel : offsets + lissage position/rotation.
/// - Peut suivre en espace Monde ou Local.
/// </summary>
[DisallowMultipleComponent]
public class SimulateChild : MonoBehaviour
{
    public enum FollowSpace { World, Local }
    public enum Tick { Update, LateUpdate, FixedUpdate }

    [Header("Target")]
    public Transform target;

    [Header("What to copy")]
    public bool copyPosition = true;
    public bool copyRotation = true;
    public bool copyScale = false;   // rarement nécessaire, mais dispo

    [Header("Space & Offsets")]
    public FollowSpace space = FollowSpace.World;
    public Vector3 positionOffset = Vector3.zero;       // appliqué après la copie
    public Vector3 rotationOffsetEuler = Vector3.zero;  // appliqué après la copie

    [Header("Smoothing (optional)")]
    public bool smooth = false;
    [Tooltip("Vitesse de lissage position (unités/seconde). Ignoré si smooth=false.")]
    public float positionLerpSpeed = 12f;
    [Tooltip("Vitesse de lissage rotation (degrés/seconde environ). Ignoré si smooth=false.")]
    public float rotationLerpSpeed = 12f;

    [Header("Update phase")]
    public Tick tick = Tick.LateUpdate; // LateUpdate = suit proprement les objets animés

    // --- API ---
    /// <summary>Aligne immédiatement sans lissage.</summary>
    public void SnapNow()
    {
        if (!target) return;
        ApplyFollow(true);
    }

    // --- Unity ---
    void Reset()
    {
        // Valeurs par défaut pour superposition parfaite
        copyPosition = true;
        copyRotation = true;
        copyScale = false;
        smooth = false;
        space = FollowSpace.World;
        tick = Tick.LateUpdate;
    }

    void Update() { if (tick == Tick.Update) ApplyFollow(false); }
    void LateUpdate() { if (tick == Tick.LateUpdate) ApplyFollow(false); }
    void FixedUpdate() { if (tick == Tick.FixedUpdate) ApplyFollow(false); }

    // --- Core ---
    void ApplyFollow(bool forceSnap)
    {
        if (!target) return;

        // 1) Position
        if (copyPosition)
        {
            if (space == FollowSpace.World)
            {
                Vector3 targetPos = target.position + positionOffset;
                if (smooth && !forceSnap)
                    transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime));
                else
                    transform.position = targetPos;
            }
            else // Local
            {
                Vector3 targetLocal = target.localPosition + positionOffset;
                if (smooth && !forceSnap)
                    transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocal, 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime));
                else
                    transform.localPosition = targetLocal;
            }
        }

        // 2) Rotation
        if (copyRotation)
        {
            Quaternion offsetQ = Quaternion.Euler(rotationOffsetEuler);

            if (space == FollowSpace.World)
            {
                Quaternion targetRot = target.rotation * offsetQ;
                if (smooth && !forceSnap)
                {
                    // interpolation exponentielle douce
                    float t = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
                }
                else
                {
                    transform.rotation = targetRot;
                }
            }
            else // Local
            {
                Quaternion targetLocalRot = target.localRotation * offsetQ;
                if (smooth && !forceSnap)
                {
                    float t = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRot, t);
                }
                else
                {
                    transform.localRotation = targetLocalRot;
                }
            }
        }

        // 3) Échelle (facultatif)
        if (copyScale)
        {
            if (space == FollowSpace.World)
            {
#if UNITY_6000_0_OR_NEWER
                // En Unity 6, localScale reste l’API standard (pas d’échelle monde native publique)
#endif
                transform.localScale = target.lossyScale; // approximation si parents diffèrent
            }
            else
            {
                transform.localScale = target.localScale;
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Option pratique dans l’éditeur : si pas de lissage, aligne en direct
        if (Application.isEditor && !Application.isPlaying && target && !smooth)
            ApplyFollow(true);
    }
#endif
}
