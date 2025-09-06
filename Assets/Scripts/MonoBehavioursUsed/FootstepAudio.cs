using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère les bruits de pas du joueur.
/// Chaque fois qu'un pied touche le sol (événement d'animation),
/// un son est joué selon la surface et l'état (marche/course).
/// Les clips sont lus séquentiellement (1,2,3,1,2,3...).
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
        [Tooltip("Tag du sol, par ex. 'Concrete', 'Sand', 'Wood'.")]
        public string groundTag;
        [Tooltip("Clips joués lorsque le joueur marche sur cette surface.")]
        public AudioClip[] walkClips;
        [Tooltip("Clips joués lorsque le joueur court sur cette surface.")]
        public AudioClip[] runClips;
    }

    [Header("Références")]
    [Tooltip("Source audio locale si l'AudioManager n'est pas disponible.")]
    [SerializeField] private AudioSource fallbackSource;

    [Header("Bruits de pas")]
    [Tooltip("Configuration des sons pour chaque type de surface.")]
    [SerializeField] private SurfaceClips[] surfaceClips;

    [Header("Sons par défaut (si aucun tag n'est reconnu)")]
    [SerializeField] private AudioClip[] defaultWalkClips;
    [SerializeField] private AudioClip[] defaultRunClips;

    // Référence au contrôleur pour connaître l'état de course.
    private ThirdPersonPlayerController movement;

    // Index d’avancement par surface et par mode (walk/run).
    // Clé = tag de surface en lower-case (ou "default"), valeur = index courant (prochaine lecture).
    private readonly Dictionary<string, int> _walkIndices = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _runIndices = new Dictionary<string, int>();

    // Accès rapide: tag -> SurfaceClips
    private readonly Dictionary<string, SurfaceClips> _surfaceMap = new Dictionary<string, SurfaceClips>();

    void Awake()
    {
        movement = GetComponent<ThirdPersonPlayerController>();

        if (fallbackSource == null)
            fallbackSource = GetComponent<AudioSource>();

        // Construire un index pour retrouver vite les surfaces par tag
        _surfaceMap.Clear();
        if (surfaceClips != null)
        {
            foreach (var s in surfaceClips)
            {
                if (string.IsNullOrWhiteSpace(s.groundTag)) continue;
                var key = s.groundTag.Trim().ToLowerInvariant();
                if (!_surfaceMap.ContainsKey(key))
                    _surfaceMap.Add(key, s);
            }
        }
    }

    /// <summary>
    /// Méthode appelée par un événement d'animation à chaque contact de pied.
    /// </summary>
    public void PlayStep()
    {
        string groundTag = GetGroundTag();

        // Sélectionne un clip approprié selon la surface et l'état (marche/course).
        AudioClip clip = GetNextClipSequential(groundTag, IsRunning());

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
    /// Retourne true si le joueur est en course.
    /// </summary>
    private bool IsRunning()
    {
        return movement != null && movement.isRunning;
    }

    /// <summary>
    /// Retourne le tag du sol sous le joueur ou une chaîne vide si aucun sol n'est détecté.
    /// </summary>
    private string GetGroundTag()
    {
        // Raycast vertical depuis le centre du personnage pour détecter le sol.
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 2f))
            return hit.collider.tag;
        return string.Empty;
    }

    /// <summary>
    /// Renvoie le prochain clip à lire en mode séquentiel pour la surface et l'état donnés.
    /// Boucle automatiquement quand la fin de la liste est atteinte.
    /// </summary>
    private AudioClip GetNextClipSequential(string groundTag, bool running)
    {
        // Normaliser la clé de surface
        string key = NormalizeTag(groundTag);

        // Récupérer les tableaux de clips (surface spécifique ou défaut)
        AudioClip[] clips = GetClipsFor(key, running);

        if (clips == null || clips.Length == 0)
            return null;

        // Choisir le bon dictionnaire d'indices (walk/run)
        var dict = running ? _runIndices : _walkIndices;

        // Obtenir l'index courant pour cette surface
        if (!dict.TryGetValue(key, out int index))
            index = 0;

        // Clamp & boucle de sécurité
        if (index < 0 || index >= clips.Length)
            index = 0;

        AudioClip next = clips[index];

        // Avancer l'index pour le prochain pas
        index = (index + 1) % clips.Length;
        dict[key] = index;

        return next;
    }

    /// <summary>
    /// Retourne le tableau de clips correspondant à la surface (ou défaut) et à l'état (walk/run).
    /// </summary>
    private AudioClip[] GetClipsFor(string normalizedKey, bool running)
    {
        // Si la surface est connue, utiliser ses clips.
        if (_surfaceMap.TryGetValue(normalizedKey, out var surface))
        {
            return running ? surface.runClips : surface.walkClips;
        }

        // Sinon, utiliser les clips par défaut.
        return running ? defaultRunClips : defaultWalkClips;
    }

    /// <summary>
    /// Transforme un tag Unity en clé normalisée pour les dictionnaires.
    /// Renvoie "default" si tag null/vide/"Untagged".
    /// </summary>
    private static string NormalizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return "default";

        string t = tag.Trim();
        if (t.Equals("Untagged", System.StringComparison.OrdinalIgnoreCase))
            return "default";

        return t.ToLowerInvariant();
    }

#if UNITY_EDITOR
    // Aide au debug dans l'Inspector
    private void OnValidate()
    {
        // S’assurer que les doublons de tag ne cassent pas la map (info éditeur uniquement).
        var tmp = new HashSet<string>();
        if (surfaceClips != null)
        {
            foreach (var s in surfaceClips)
            {
                if (string.IsNullOrWhiteSpace(s.groundTag)) continue;
                string k = s.groundTag.Trim().ToLowerInvariant();
                if (!tmp.Add(k))
                {
                    Debug.LogWarning($"[FootstepAudio] Tag dupliqué détecté: '{s.groundTag}'. Le premier rencontré sera utilisé.");
                }
            }
        }
    }
#endif
}
