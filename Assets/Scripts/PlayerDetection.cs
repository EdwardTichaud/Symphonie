using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AnimationHandler))]
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

    public List<CharacterData> detectedEnemies = new List<CharacterData>();
    public bool detectionOn = true;

    [Header("State")]
    public bool battleEngaged;

    private void Awake()
    {
        currentDetectionRadius = baseDetectionRadius;
    }

    private void Update()
    {
        HandleEnemyDetection();
        CleanupInvalidFields();
    }

    private void CleanupInvalidFields()
    {
        detectedEnemies.RemoveAll(c => c == null);
    }

    private void HandleEnemyDetection()
    {
        if (!detectionOn)
            return;

        // 1) On recherche tous les colliders dans le rayon de base (sans layer mask)
        Collider[] initialColliders = Physics.OverlapSphere(
            transform.position,
            currentDetectionRadius
        );

        // 2) On filtre uniquement ceux qui portent le tag "Enemy"
        var initialHits = initialColliders
            .Where(c => c.CompareTag(enemyTag))
            .ToArray();

        if (initialHits.Length == 0)
            return; // aucun ennemi trouvé dans le rayon de base

        // 3) Premier ennemi détecté → on désactive detectionOn et on agrandit le rayon
        detectionOn = false;
        currentDetectionRadius = currentDetectionRadius + detectionExpansion;

        // 4) On récupère tous les colliders dans le rayon élargi
        Collider[] allColliders = Physics.OverlapSphere(
            transform.position,
            currentDetectionRadius
        );

        // 5) On filtre encore par tag "Enemy"
        var allHits = allColliders
            .Where(c => c.CompareTag(enemyTag))
            .ToArray();

        // 6) On récupère le composant Enemy (ou le parent contenant Enemy), en évitant les doublons
        var enemies = allHits
            .Select(c => c.GetComponentInParent<Enemy>())
            .Where(e => e != null)
            .Distinct()
            .ToList();

        // 7) On ne garde que les 3 plus proches
        var closestThree = enemies
            .OrderBy(e => Vector3.Distance(transform.position, e.transform.position))
            .Take(3)
            .ToList();

        // 8) On remplit detectedEnemies
        detectedEnemies.Clear();
        foreach (var e in closestThree)
        {
            detectedEnemies.Add(e.enemyData);

            // On marque l'ennemi comme ayant participé à la dernière bataille
            e.wasPartOfLastBattle = true;
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
    public void ResetDetection()
    {
        currentDetectionRadius = baseDetectionRadius;
        detectionOn = true;
        battleEngaged = false;
        detectedEnemies.Clear();
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
