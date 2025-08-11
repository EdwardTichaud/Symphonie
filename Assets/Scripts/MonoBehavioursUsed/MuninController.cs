using UnityEngine;

/// <summary>
/// Munin, l'observateur, applique une légère oscillation de "respiration" aux caméras
/// World et Battle pour donner plus de vie à la scène.
/// L'effet est totalement indépendant des déplacements imposés par d'autres scripts
/// (Timeline, CameraPath, contrôleur de caméra, etc.).
/// </summary>
public class MuninController : MonoBehaviour
{
    [Header("Caméras contrôlées par Munin")]
    [Tooltip("Caméra utilisée dans le monde exploration.")]
    public Camera worldCamera;
    [Tooltip("Caméra utilisée lors des combats.")]
    public Camera battleCamera;

    [Header("Paramètres de respiration")]
    [Tooltip("Amplitude verticale maximale de l'oscillation."), SerializeField]
    private float amplitude = 0.05f;
    [Tooltip("Fréquence de l'oscillation en hertz."), SerializeField]
    private float frequency = 1f;

    // Positions d'ancrage (locales) et dernières positions mondes enregistrées pour chaque caméra
    private Vector3 worldBaseLocalPos, battleBaseLocalPos;
    private Vector3 worldLastPos, battleLastPos;

    /// <summary>
    /// Initialise les points d'ancrage des caméras.
    /// </summary>
    void Start()
    {
        if (worldCamera != null)
        {
            worldBaseLocalPos = worldCamera.transform.localPosition;
            worldLastPos = worldCamera.transform.position;
        }

        if (battleCamera != null)
        {
            battleBaseLocalPos = battleCamera.transform.localPosition;
            battleLastPos = battleCamera.transform.position;
        }
    }

    /// <summary>
    /// Applique l'oscillation en fin de frame pour éviter de perturber les autres mouvements.
    /// </summary>
    void LateUpdate()
    {
        // Pause l'effet si une Timeline ou un CameraPath prend la main
        if ((TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying) ||
            CameraController.IsAnyPathPlaying)
        {
            ResetAnchors();
            return;
        }

        // Applique l'effet sur chaque caméra
        ApplyBreathing(worldCamera, ref worldBaseLocalPos, ref worldLastPos);
        ApplyBreathing(battleCamera, ref battleBaseLocalPos, ref battleLastPos);
    }

    /// <summary>
    /// Oscillation douce d'une caméra autour de son ancrage de base.
    /// </summary>
    /// <param name="cam">Caméra ciblée.</param>
    /// <param name="baseLocalPos">Position locale de référence.</param>
    /// <param name="lastPos">Dernière position monde enregistrée.</param>
    private void ApplyBreathing(Camera cam, ref Vector3 baseLocalPos, ref Vector3 lastPos)
    {
        if (cam == null)
            return;

        Transform t = cam.transform;

        // Détecte un déplacement externe de la caméra
        bool moved = (t.position - lastPos).sqrMagnitude > 1e-6f;
        if (moved)
        {
            // La caméra a été déplacée : on redéfinit l'ancrage
            baseLocalPos = t.localPosition;
        }
        else
        {
            // Ajoute une oscillation verticale simulant une respiration
            float offset = Mathf.Sin(Time.time * frequency) * amplitude;
            t.localPosition = baseLocalPos + Vector3.up * offset;
        }

        // Mise à jour de la dernière position
        lastPos = t.position;
    }

    /// <summary>
    /// Réinitialise les ancrages lorsque l'effet est suspendu.
    /// </summary>
    private void ResetAnchors()
    {
        if (worldCamera != null)
        {
            worldBaseLocalPos = worldCamera.transform.localPosition;
            worldLastPos = worldCamera.transform.position;
        }

        if (battleCamera != null)
        {
            battleBaseLocalPos = battleCamera.transform.localPosition;
            battleLastPos = battleCamera.transform.position;
        }
    }
}

