using UnityEngine;

/// <summary>
/// Gère les bruits de pas du joueur.
/// Chaque fois qu'un pied touche le sol (événement d'animation),
/// un son est joué en fonction du type de surface et du déplacement
/// (marche ou course).
/// </summary>
[RequireComponent(typeof(ThirdPersonPlayerController))]
public class FootstepAudio : MonoBehaviour
{
    /// <summary>
    /// Regroupe les clips associés à un type de sol.
    /// </summary>
    [System.Serializable]
    public struct SurfaceClips
    {
        [Tooltip("Tag du sol, par exemple 'Concrete', 'Sand' ou 'Wood'.")]
        public string groundTag;           // Tag utilisé par le collider du sol.
        [Tooltip("Clips joués lorsque le joueur marche sur cette surface.")]
        public AudioClip[] walkClips;      // Sons de pas lors de la marche.
        [Tooltip("Clips joués lorsque le joueur court sur cette surface.")]
        public AudioClip[] runClips;       // Sons de pas lors de la course.
    }

    [Header("Références")]
    [Tooltip("Source audio locale si l'AudioManager n'est pas disponible.")]
    [SerializeField] private AudioSource fallbackSource; // Optionnel : utilisé en l'absence d'AudioManager.

    [Header("Bruits de pas")]
    [Tooltip("Configuration des sons pour chaque type de surface.")]
    [SerializeField] private SurfaceClips[] surfaceClips; // Tableau de surfaces gérées.

    private ThirdPersonPlayerController movement; // Référence au contrôleur pour connaître l'état de course.

    void Awake()
    {
        // Récupère le contrôleur de mouvement obligatoire.
        movement = GetComponent<ThirdPersonPlayerController>();

        // Si aucune AudioSource n'est assignée, on tente d'en récupérer une sur l'objet.
        if (fallbackSource == null)
            fallbackSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Méthode appelée par un événement d'animation à chaque contact de pied.
    /// </summary>
    public void PlayStep()
    {
        // Détermine le tag de la surface sous le joueur via un raycast.
        string groundTag = GetGroundTag();

        // Sélectionne un clip approprié selon la surface et l'état (marche/course).
        AudioClip clip = SelectClip(groundTag);

        // Joue le clip via l'AudioManager s'il est présent, sinon via la source locale.
        if (clip != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(clip);
            }
            else if (fallbackSource != null)
            {
                fallbackSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning("[FootstepAudio] Aucun moyen de jouer le son de pas.");
            }
        }
    }

    /// <summary>
    /// Retourne le tag du sol sous le joueur ou une chaîne vide si aucun sol n'est détecté.
    /// </summary>
    private string GetGroundTag()
    {
        // Raycast vertical depuis le centre du personnage pour détecter le sol.
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 2f))
            return hit.collider.tag; // Renvoie le tag du collider touché.
        return string.Empty; // Aucun sol détecté : tag vide.
    }

    /// <summary>
    /// Choisit un clip de pas correspondant au tag du sol et à l'état du joueur.
    /// </summary>
    private AudioClip SelectClip(string groundTag)
    {
        bool running = movement != null && movement.isRunning; // Détermine si le joueur court.

        // Recherche la surface correspondante dans la configuration.
        foreach (var surface in surfaceClips)
        {
            if (surface.groundTag == groundTag)
            {
                // Sélectionne la liste de clips appropriée.
                var clips = running ? surface.runClips : surface.walkClips;
                if (clips != null && clips.Length > 0)
                {
                    // Choisit un clip aléatoire pour éviter la répétition.
                    return clips[Random.Range(0, clips.Length)];
                }
            }
        }

        return null; // Aucun clip trouvé pour cette surface.
    }
}

