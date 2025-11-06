using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///     Applique l'enchantement propre au MusicalMove « Presto ».
///     Tant que l'effet est actif, la cible effectue automatiquement
///     une attaque basique après le tour de chaque unité en combat.
///     L'effet prend fin dès que le lanceur rejoue.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterUnit))]
public class PrestoForcedAttackStatus : MonoBehaviour
{
    /// <summary>Référence directe vers l'unité affectée pour éviter les <c>GetComponent</c> répétés.</summary>
    private CharacterUnit owner;

    /// <summary>Unité ayant lancé Presto. Permet de savoir quand mettre fin à l'effet.</summary>
    private CharacterUnit caster;

    /// <summary>Instance du prefab visuel instancié sur la cible pour matérialiser l'effet.</summary>
    private GameObject visualInstance;

    /// <summary>Prefab actuellement utilisé pour l'effet visuel (sert à détecter les changements de configuration).</summary>
    private GameObject currentPrefab;

    /// <summary>Décalage vertical appliqué au visuel afin qu'il flotte au-dessus du personnage.</summary>
    private float verticalOffset = 0.5f;

    /// <summary>
    /// Compteur du nombre de fois où le début du tour du lanceur a été observé
    /// depuis l'application de l'effet. Dès qu'on atteint la valeur 2
    /// (tour initial + tour suivant), on désactive le statut.
    /// </summary>
    private int casterTurnObservations = 0;

    /// <summary>État interne indiquant si l'instance est toujours valide.</summary>
    private bool isActive = false;

    private void Awake()
    {
        owner = GetComponent<CharacterUnit>();
    }

    private void LateUpdate()
    {
        // Maintient le visuel à la bonne hauteur même si la taille du personnage varie (animation, échelle dynamique...).
        UpdateVisualTransform();
    }

    /// <summary>
    /// Prépare le statut : enregistre le lanceur, instancie le visuel et reset le suivi de tour.
    /// </summary>
    /// <param name="source">Personnage ayant lancé Presto.</param>
    /// <param name="effectPrefab">Prefab visuel configuré sur le move.</param>
    /// <param name="additionalOffset">Décalage vertical supplémentaire défini sur le move.</param>
    public void Configure(CharacterUnit source, GameObject effectPrefab, float additionalOffset)
    {
        caster = source;
        verticalOffset = Mathf.Max(0f, additionalOffset);
        casterTurnObservations = 1; // On a déjà observé le tour en cours lors du lancement de Presto.
        isActive = true;

        EnsureVisual(effectPrefab);

        // Enregistre le statut auprès du gestionnaire global pour recevoir les événements de tour.
        PrestoForcedAttackSystem.Register(this);
    }

    /// <summary>
    /// Notifié par le gestionnaire lorsque n'importe quelle unité termine son tour.
    /// </summary>
    public void HandleTurnEnded(CharacterUnit endedUnit)
    {
        if (!isActive)
            return;

        if (owner == null || owner.currentHP <= 0)
        {
            // La cible n'est plus en état d'agir : on nettoie immédiatement le statut.
            Cleanup();
            return;
        }

        if (caster != null && caster.currentHP <= 0)
        {
            // Si le lanceur tombe au combat, l'effet se dissipe naturellement.
            Cleanup();
            return;
        }

        if (endedUnit == null)
            return;

        // Recherche l'attaque basique utilisable par la cible.
        MusicalMoveSO basicAttack = owner.GetBasicAttack();
        if (basicAttack == null)
            return; // Cible incapable d'attaquer : on laisse l'effet actif au cas où un set serait rétabli plus tard.

        // Recherche d'une victime valable dans le camp opposé.
        CharacterUnit forcedTarget = ResolveForcedAttackTarget();
        if (forcedTarget == null)
            return; // Aucun adversaire valide (ex : tous K.O.).

        if (NewBattleManager.Instance != null)
            NewBattleManager.Instance.OrientUnitTowardTarget(owner, forcedTarget);

        // Exécute immédiatement l'effet du move sans passer par un QTE.
        basicAttack.ApplyEffect(owner, forcedTarget, false);
    }

    /// <summary>
    /// Notifié quand l'unité active change. Utilisé pour détecter le prochain tour du lanceur.
    /// </summary>
    public void HandleActiveUnitChanged(CharacterUnit newUnit)
    {
        if (!isActive || caster == null)
            return;

        if (newUnit != caster)
            return;

        casterTurnObservations++;
        if (casterTurnObservations >= 2)
            Cleanup();
    }

    /// <summary>
    /// Détermine une cible adverse à frapper automatiquement.
    /// </summary>
    private CharacterUnit ResolveForcedAttackTarget()
    {
        if (NewBattleManager.Instance == null || owner == null || owner.Data == null)
            return null;

        bool ownerIsPlayer = owner.Data.isPlayerControlled;
        List<CharacterUnit> candidates = NewBattleManager.Instance.activeCharacterUnits
            .Where(u => u != null && u != owner && u.Data != null && u.Data.isPlayerControlled != ownerIsPlayer && u.currentHP > 0)
            .ToList();

        if (candidates.Count == 0)
            return null;

        // Sélection aléatoire pour éviter toute répétitivité et encourager des situations variées.
        int index = Random.Range(0, candidates.Count);
        return candidates[index];
    }

    /// <summary>
    /// Instancie ou met à jour le visuel associé au statut.
    /// </summary>
    private void EnsureVisual(GameObject prefab)
    {
        if (prefab == null)
        {
            RemoveVisual();
            currentPrefab = null;
            return;
        }

        if (visualInstance != null && currentPrefab != prefab)
            RemoveVisual();

        if (visualInstance == null)
        {
            visualInstance = Instantiate(prefab, transform);
            visualInstance.name = $"{prefab.name}_Presto";
        }

        currentPrefab = prefab;
        UpdateVisualTransform();
    }

    /// <summary>
    /// Positionne le visuel juste au-dessus de l'unité pour rester lisible en toutes circonstances.
    /// </summary>
    private void UpdateVisualTransform()
    {
        if (visualInstance == null || owner == null)
            return;

        Bounds bounds = owner.GetVisualBounds();
        float topY = bounds.center.y + bounds.extents.y;
        Transform visualTransform = visualInstance.transform;
        visualTransform.position = new Vector3(owner.transform.position.x, topY + verticalOffset, owner.transform.position.z);
        visualTransform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// Supprime l'effet et détruit le visuel associé.
    /// </summary>
    private void Cleanup()
    {
        if (!isActive)
            return;

        isActive = false;
        PrestoForcedAttackSystem.Unregister(this);
        RemoveVisual();
        Destroy(this);
    }
    private void RemoveVisual()
    {
        if (visualInstance == null)
            return;

        Destroy(visualInstance);
        visualInstance = null;
    }

    private void OnDestroy()
    {
        // Garantit la suppression dans les cas où Cleanup n'aurait pas été appelé explicitement (par exemple destruction de l'unité).
        PrestoForcedAttackSystem.Unregister(this);
        RemoveVisual();
        isActive = false;
    }
}

/// <summary>
/// Gestionnaire statique chargé de relayer les événements de combat aux statuts Presto actifs.
/// Sépare la logique de stockage de la logique d'effet afin de limiter les dépendances croisées.
/// </summary>
public static class PrestoForcedAttackSystem
{
    /// <summary>Liste des statuts actifs. Le HashSet évite les doublons et accélère les recherches.</summary>
    private static readonly HashSet<PrestoForcedAttackStatus> ActiveStatuses = new();

    /// <summary>Ajoute un statut à la collection suivie.</summary>
    public static void Register(PrestoForcedAttackStatus status)
    {
        if (status == null)
            return;

        ActiveStatuses.Add(status);
    }

    /// <summary>Retire un statut de la collection (appelé automatiquement lors de la fin d'effet).</summary>
    public static void Unregister(PrestoForcedAttackStatus status)
    {
        if (status == null)
            return;

        ActiveStatuses.Remove(status);
    }

    /// <summary>
    /// Applique (ou réapplique) l'effet Presto sur une cible donnée.
    /// </summary>
    public static void ApplyStatus(CharacterUnit target, CharacterUnit caster, GameObject effectPrefab, float verticalOffset)
    {
        // Sans lanceur la durée ne peut pas être évaluée correctement : on ignore l'application.
        if (target == null || caster == null)
            return;

        var status = target.GetComponent<PrestoForcedAttackStatus>();
        if (status == null)
            status = target.gameObject.AddComponent<PrestoForcedAttackStatus>();

        status.Configure(caster, effectPrefab, verticalOffset);
    }

    /// <summary>
    /// Doit être appelé lorsque n'importe quelle unité termine son tour afin de déclencher les attaques automatiques.
    /// </summary>
    public static void HandleTurnEnded(CharacterUnit endedUnit)
    {
        if (ActiveStatuses.Count == 0)
            return;

        foreach (var status in ActiveStatuses.ToArray())
            status?.HandleTurnEnded(endedUnit);
    }

    /// <summary>
    /// Doit être appelé à chaque changement d'unité active pour détecter le retour du lanceur.
    /// </summary>
    public static void HandleActiveUnitChanged(CharacterUnit newUnit)
    {
        if (ActiveStatuses.Count == 0)
            return;

        foreach (var status in ActiveStatuses.ToArray())
            status?.HandleActiveUnitChanged(newUnit);
    }
}
