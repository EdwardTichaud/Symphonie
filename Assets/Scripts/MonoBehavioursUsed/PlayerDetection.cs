using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDetection : MonoBehaviour
{
    [Header("Détection de l'environnement")]
    public LayerMask groundLayer;
    public Transform groundCheck;
    public float groundCheckRadius = 1.1f;
    public bool isGrounded;
    private bool wasGrounded;
    public float coyoteTime = 0.2f;

    [Header("Détection des ennemis (par tag)")]
    // On garde baseDetectionRadius et detectionExpansion, mais on ne se base plus sur un LayerMask
    public float currentDetectionRadius = 1.5f;
    public float baseDetectionRadius = 1.5f;
    public float detectionExpansion = 5.5f;
    // Le tag à rechercher
    [Tooltip("Tag utilisé pour identifier les ennemis (par exemple : \"Enemy\").")]
    public string enemyTag = "Enemy";

    // ------------------------------------------------------------------
    // Liste temporaire des ennemis actuellement engagés dans un combat
    // Permet d'éviter de redétecter en boucle les mêmes ennemis tant
    // qu'ils ne sont pas définitivement retirés du monde.
    // ------------------------------------------------------------------
    public List<Enemy> enemiesInFight = new List<Enemy>();

    public List<CharacterData> detectedEnemies = new List<CharacterData>();
    public bool detectionOn = true;

    [Header("Effets sonores")]
    [Tooltip("Clips vocaux joués lors de la première détection d'un ennemi")]
    public List<AudioClip> firstDetectionVoices = new List<AudioClip>();

    [Tooltip("Volume appliqué au clip de première détection")]
    [Range(0f, 1f)]
    public float firstDetectionVoiceVolume = 1f;

    private bool firstEnemyDetected;

    [Header("State")]
    public bool battleEngaged;

    private void Awake()
    {
        currentDetectionRadius = baseDetectionRadius;
        firstEnemyDetected = false;
    }

    private void Update()
    {
        HandleEnemyDetection();
        CleanupInvalidFields();
    }

    private void CleanupInvalidFields()
    {
        // Nettoie les références invalides pour éviter les fuites d'objets
        detectedEnemies.RemoveAll(c => c == null);
        enemiesInFight.RemoveAll(e => e == null);
    }

    private void HandleEnemyDetection()
    {
        if (!detectionOn)
            return;

        // On recherche tous les colliders dans le rayon de base (sans layer mask)
        // afin de savoir s'il y a réellement un ennemi à proximité avant de jouer une voix.
        Collider[] initialColliders = Physics.OverlapSphere(
            transform.position,
            currentDetectionRadius
        );

        // On filtre uniquement ceux qui portent le tag "Enemy"
        var initialHits = initialColliders
            .Where(c => c.CompareTag(enemyTag))
            .ToArray();

        // Si aucun ennemi n'est trouvé, on quitte immédiatement la fonction
        // pour éviter de jouer un clip vocal inutilement.
        if (initialHits.Length == 0)
            return; // aucun ennemi trouvé dans le rayon de base

        // Lecture du clip vocal uniquement lors de la première détection effective d'un ennemi
        if (!firstEnemyDetected && firstDetectionVoices.Count > 0)
        {
            // Sélection aléatoire d'un clip parmi la liste disponible
            AudioClip clip = firstDetectionVoices[UnityEngine.Random.Range(0, firstDetectionVoices.Count)];

            // Lecture du clip avec le volume choisi dans l'inspecteur
            AudioManager.Instance?.PlayVoice(clip, firstDetectionVoiceVolume);

            // On mémorise que la première détection a eu lieu afin de ne pas répéter la voix
            firstEnemyDetected = true;
        }

        // À partir d'ici, au moins un ennemi a été trouvé : on peut élargir le rayon de détection
        // et poursuivre la logique de mise en combat.
        detectionOn = false;
        currentDetectionRadius = currentDetectionRadius + detectionExpansion;

        // On récupère tous les colliders dans le rayon élargi
        Collider[] allColliders = Physics.OverlapSphere(
            transform.position,
            currentDetectionRadius
        );

        // On filtre encore par tag "Enemy"
        var allHits = allColliders
            .Where(c => c.CompareTag(enemyTag))
            .ToArray();

        // On récupère le composant Enemy (ou le parent contenant Enemy), en évitant les doublons
        var enemies = allHits
            .Select(c => c.GetComponentInParent<Enemy>())
            // On ignore les ennemis déjà engagés dans un combat
            .Where(e => e != null && !enemiesInFight.Contains(e))
            .Distinct()
            .ToList();

        // On ne garde que les 3 plus proches
        var closestThree = enemies
            .OrderBy(e => Vector3.Distance(transform.position, e.transform.position))
            .Take(3)
            .ToList();

        // On remplit detectedEnemies
        detectedEnemies.Clear();
        foreach (var e in closestThree)
        {
            detectedEnemies.Add(e.enemyData);

            // Ajout à la liste temporaire pour empêcher une redétection
            // et indiquer que cet ennemi est actuellement engagé dans ce combat
            enemiesInFight.Add(e);
        }

        // 9) On déclenche ou termine le combat
        if (detectedEnemies.Count > 0 && !battleEngaged)
        {
            var mgr = NewBattleManager.Instance;
            mgr.enemyTemplates.Clear();
            mgr.enemyTemplates.AddRange(detectedEnemies);

            battleEngaged = true;
            BattleTransitionManager.Instance.StartCombatTransition();
        }
        else if (detectedEnemies.Count == 0 && battleEngaged)
        {
            currentDetectionRadius = baseDetectionRadius;
            battleEngaged = false;
        }
    }

    /// <summary>
    /// Réinitialise tous les paramètres de détection aux valeurs de base,
    /// pour repartir proprement après un combat.
    /// </summary>
    public void ResetDetection(float delay = 1f)
    {
        StartCoroutine(ResetDetectionRoutine(delay));
    }

    private IEnumerator ResetDetectionRoutine(float delay)
    {
        currentDetectionRadius = baseDetectionRadius;
        battleEngaged = false;
        detectedEnemies.Clear();
        detectionOn = false;
        firstEnemyDetected = false;

        Debug.Log($"[PlayerDetection] Réinitialisation en cours, détection réactivée dans {delay} s.");

        yield return new WaitForSeconds(delay);

        detectionOn = true;
        Debug.Log("[PlayerDetection] Détection réinitialisée.");
    }


    private void OnDrawGizmos()
    {
        // Rayon de détection des ennemis (base)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f); // orange semi-transparent
        Gizmos.DrawWireSphere(transform.position, currentDetectionRadius);

        // Rayon agrandi si détection déclenchée
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // rouge
        Gizmos.DrawWireSphere(transform.position, currentDetectionRadius + detectionExpansion);

        // Détection du sol
        if (groundCheck != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f); // vert
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

}
