using System;
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
    // Listes réutilisées pour limiter les allocations pendant Update
    // enemiesInFight : ennemis déjà engagés en combat
    // tempEnemies    : buffer pour les calculs de distance
    // ------------------------------------------------------------------
    public List<Enemy> enemiesInFight = new List<Enemy>();
    private readonly List<Enemy> tempEnemies = new List<Enemy>();

    public List<CharacterData> detectedEnemies = new List<CharacterData>();
    public bool detectionOn = true;

    // Buffers pour les raycasts sans allocation
    private const int MaxColliders = 50;
    private readonly Collider[] initialResults = new Collider[MaxColliders];
    private readonly Collider[] expandedResults = new Collider[MaxColliders];

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

        // Recherche sans allocation d'un ennemi proche
        int initialCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            currentDetectionRadius,
            initialResults
        );

        bool enemyFound = false;
        for (int i = 0; i < initialCount; i++)
        {
            if (initialResults[i].CompareTag(enemyTag))
            {
                enemyFound = true;
                break;
            }
        }

        // Si aucun ennemi n'est trouvé, on quitte immédiatement la fonction
        if (!enemyFound)
            return;

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

        // On récupère tous les colliders dans le rayon élargi sans allocation
        int allCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            currentDetectionRadius,
            expandedResults
        );

        tempEnemies.Clear();
        for (int i = 0; i < allCount; i++)
        {
            Collider col = expandedResults[i];
            if (!col.CompareTag(enemyTag))
                continue;

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null && !enemiesInFight.Contains(enemy) && !tempEnemies.Contains(enemy))
            {
                tempEnemies.Add(enemy);
            }
        }

        // Trie les ennemis par distance et ne garde que les trois plus proches
        tempEnemies.Sort((a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
                .CompareTo(Vector3.Distance(transform.position, b.transform.position)));

        detectedEnemies.Clear();
        for (int i = 0; i < tempEnemies.Count && i < 3; i++)
        {
            Enemy e = tempEnemies[i];
            detectedEnemies.Add(e.enemyData);
            // Ajout à la liste temporaire pour empêcher une redétection
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
        enemiesInFight.Clear(); // On s'assure qu'aucun ennemi du combat prcédent ne reste enregistré
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
