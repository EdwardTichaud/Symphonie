using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère les bruits de pas du joueur.
/// Version révisée : au lieu de jouer un clip à chaque contact de pied,
/// on lance une boucle lorsque le joueur court. Lorsque le joueur
/// s'arrête ou change de type de sol, la boucle est stoppée puis relancée
/// avec un clip adapté. Cette approche évite la multiplication d'événements
/// d'animation tout en offrant un rendu continu.
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

    // Accès rapide: tag -> SurfaceClips
    private readonly Dictionary<string, SurfaceClips> _surfaceMap = new Dictionary<string, SurfaceClips>();

    // Tag de sol actuellement associé au son de course joué.
    private string _currentGroundTag = "default";

    // Indique si un clip est actuellement en cours de lecture en boucle.
    private bool _isLoopPlaying = false;

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
    /// Boucle principale qui surveille l'état de course et le type de sol.
    /// Un son de pas est joué en boucle lorsque le joueur court. Le son
    /// change ou s'arrête automatiquement selon les transitions.
    /// </summary>
    private void Update()
    {
        bool running = IsRunning();
        string groundTag = NormalizeTag(GetGroundTag());

        // Si le joueur court : lancer ou mettre à jour la boucle
        if (running)
        {
            // Si aucun son ne joue ou que le sol a changé, relancer la boucle
            if (!_isLoopPlaying || groundTag != _currentGroundTag)
            {
                StartLoop(groundTag, true);
            }
        }
        else if (_isLoopPlaying)
        {
            // Le joueur ne court plus : arrêter la boucle en cours
            StopLoop();
        }

        _currentGroundTag = groundTag;
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
    /// Lance la lecture en boucle d'un clip adapté à la surface courante.
    /// </summary>
    private void StartLoop(string groundTag, bool running)
    {
        // Choisir un clip (aléatoire) correspondant à la surface.
        AudioClip clip = GetRandomClip(groundTag, running);
        if (clip == null || fallbackSource == null)
            return;

        // Préparation de la source pour la lecture en boucle.
        fallbackSource.loop = true;
        fallbackSource.clip = clip;
        fallbackSource.Play();

        _isLoopPlaying = true;
        _currentGroundTag = NormalizeTag(groundTag);
    }

    /// <summary>
    /// Stoppe la lecture en boucle en cours.
    /// </summary>
    private void StopLoop()
    {
        if (fallbackSource != null)
        {
            fallbackSource.Stop();
            fallbackSource.clip = null;
        }

        _isLoopPlaying = false;
    }

    /// <summary>
    /// Sélectionne un clip aléatoire en fonction du type de sol et de l'état (marche/course).
    /// </summary>
    private AudioClip GetRandomClip(string groundTag, bool running)
    {
        string key = NormalizeTag(groundTag);
        AudioClip[] clips = GetClipsFor(key, running);

        if (clips == null || clips.Length == 0)
            return null;

        // Choix aléatoire pour varier légèrement les sons à chaque reprise.
        int index = Random.Range(0, clips.Length);
        return clips[index];
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
