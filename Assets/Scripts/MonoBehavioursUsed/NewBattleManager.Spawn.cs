using System;
using System.Collections.Generic;
using UnityEngine;

public partial class NewBattleManager
{
    #region Initialisation du champs de bataille
    public void SpawnAll()
    {
        // Nettoie les références nulles pouvant persister après un précédent combat
        activeCharacterUnits.RemoveAll(u => u == null);
        unitsInBattle.RemoveAll(u => u == null);

        // Si des unités valides existent encore, on évite de doubler le spawn
        bool hasLiveUnits = unitsInBattle.Exists(u => u != null && u.gameObject.activeInHierarchy);
        if (hasLiveUnits || activeCharacterUnits.Exists(u => u != null && u.gameObject.activeInHierarchy))
        {
            Debug.LogWarning("[NewBattleManager] SpawnAll déjà exécuté ou unités déjà présentes.");
            return;
        }

        // S'assure que les listes sont complètement vides avant d'instancier de nouvelles unités
        activeCharacterUnits.Clear();
        unitsInBattle.Clear();
        SpawnSquadUnits();
        SpawnEnemies();
    }

    /// <summary>
    /// Assure la résolution (et la mise en cache) d'un point de spawn afin d'éviter les recherches répétées.
    /// </summary>
    private Transform ResolveSpawnRoot(ref Transform cache, string requiredTag)
    {
        if (cache != null)
            return cache;

        var taggedRoot = GameObject.FindGameObjectWithTag(requiredTag);
        if (taggedRoot == null)
        {
            Debug.LogWarning($"[NewBattleManager] Aucun objet avec le tag '{requiredTag}' n'a été trouvé dans la scène.");
            return null;
        }

        cache = taggedRoot.transform;
        return cache;
    }

    private void SpawnSquadUnits()
    {
        playerSpawnPoints.Clear();
        var spawnRoot = ResolveSpawnRoot(ref playerSpawnRoot, "PlayerSpawn");
        if (spawnRoot == null)
            return;

        for (int i = 0; i < spawnRoot.childCount; i++)
        {
            var child = spawnRoot.GetChild(i);
            if (child != null)
            {
                playerSpawnPoints.Add(child);

                // Crée (ou recycle) le point de visée utilisé par la caméra épaulière.
                EnsureCasterLookTargetAnchor_SquadUnit(child);
            }
        }

        // Seuls les trois premiers membres de la squad peuvent participer au combat
        var squad = SquadManager.Instance != null ? SquadManager.Instance.SquadCharacters : new List<CharacterData>();
        int maxSquadMembers = Mathf.Min(3, squad.Count);
        for (int i = 0; i < maxSquadMembers && i < playerSpawnPoints.Count; i++)
        {
            var pc = squad[i];
            var spawnPoint = playerSpawnPoints[i];

            if (pc.characterBattleModel == null)
            {
                Debug.LogWarning($"[SpawnPlayers] Aucun modèle défini pour {pc.characterName}, annulation du spawn.");
                continue;
            }

            // 🧍 Apparition immédiate de l'unité à sa position de combat.
            //     Nous utilisons désormais une méthode dédiée afin d'appliquer
            //     automatiquement une hauteur supplémentaire pour les unités
            //     aériennes, garantissant que les modèles ne "tombent" plus sur
            //     le sol lorsque la scène démarre.
            var spawnPosition = ComputeSpawnPosition(pc, spawnPoint);
            var unitGO = Instantiate(pc.characterBattleModel, spawnPosition, Quaternion.identity);
            unitGO.transform.SetParent(spawnPoint, worldPositionStays: true);
            unitGO.name = $"SquadUnit_{i}";

            // ✅ Génère l'effet visuel du rayon directement à l'emplacement final.
            if (squadUnitRay != null)
                Instantiate(squadUnitRay, spawnPosition, Quaternion.identity);

            var unit = unitGO.GetComponent<CharacterUnit>();
            unit.Initialize(pc);
            unitsInBattle.Add(unit);
        }
    }

    private void SpawnEnemies()
    {
        enemySpawnPoints.Clear();
        var spawnRoot = ResolveSpawnRoot(ref enemySpawnRoot, "EnemySpawn");
        if (spawnRoot == null)
            return;

        for (int i = 0; i < spawnRoot.childCount; i++)
        {
            var child = spawnRoot.GetChild(i);
            if (child != null)
            {
                enemySpawnPoints.Add(child);

                // Génère également le point de visée pour les ennemis afin que les items/moves
                // puissent demander une caméra épaulière même si la position est vide au départ.
                EnsureCasterLookTargetAnchor_EnemyUnit(child);
            }
        }

        for (int i = 0; i < enemyTemplates.Count && i < enemySpawnPoints.Count; i++)
        {
            var enemyData = Instantiate(enemyTemplates[i]);
            var spawnPoint = enemySpawnPoints[i];

            if (enemyData.characterBattleModel == null)
            {
                Debug.LogWarning($"[SpawnEnemies] Aucun modèle défini pour {enemyData.characterName}, annulation du spawn.");
                continue;
            }

            // 🧍 Apparition immédiate de l'ennemi à sa position de combat.
            //     Comme pour l'équipe du joueur, la position est recalculée afin
            //     d'offrir un point d'apparition cohérent avec le type d'unité.
            var spawnPosition = ComputeSpawnPosition(enemyData, spawnPoint);
            var unitGO = Instantiate(enemyData.characterBattleModel, spawnPosition, Quaternion.Euler(0f, 180f, 0f));
            unitGO.transform.SetParent(spawnPoint, worldPositionStays: true);
            unitGO.name = $"EnemyUnit_{i}";

            // ✅ Génère l'effet du rayon directement à l'emplacement final.
            if (enemyUnitRay != null)
                Instantiate(enemyUnitRay, spawnPosition, Quaternion.identity);

            var eu = unitGO.GetComponent<CharacterUnit>();
            eu.Initialize(enemyData);
            unitsInBattle.Add(eu);
        }
    }
    #endregion

    /// <summary>
    /// S'assure qu'un point "CMVPoint_OverShoulder_CasterLookTarget" existe sur un emplacement donné
    /// afin que les caméras contextuelles puissent viser automatiquement la cible active.
    /// </summary>
    /// <param name="spawnPoint">Transform représentant PlayerPosition_X ou EnemyPosition_X.</param>
    /// <returns>Le transform correspondant au point de visée.</returns>
    private Transform EnsureCasterLookTargetAnchor_SquadUnit(Transform spawnPoint)
    {
        if (spawnPoint == null)
            return null;

        const string anchorName = "CMVPoint_OverShoulder_CasterLookTarget";

        // Vérifie si un point existe déjà (scène, prefab ou précédent spawn) pour éviter les doublons.
        Transform existingAnchor = null;
        foreach (Transform child in spawnPoint.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (child == null)
                continue;

            if (string.Equals(child.name, anchorName, StringComparison.OrdinalIgnoreCase))
            {
                existingAnchor = child;
                break;
            }
        }

        if (existingAnchor != null)
        {
            // Met à jour/ajoute le composant responsable d'orienter l'ancre vers la cible courante.
            EnsureLookAtComponent(existingAnchor);
            return existingAnchor;
        }

        // Aucun point n'existait : on instancie un nouvel objet utilitaire dédié au guidage caméra.
        GameObject anchorGO = new(anchorName);
        anchorGO.transform.SetParent(spawnPoint, worldPositionStays: false);
        anchorGO.transform.localPosition = overShoulderCasterLookPointPosition_SquadUnit;
        anchorGO.transform.localRotation = Quaternion.identity;

        // Lie systématiquement l'ancre à la cible actuelle afin que les caméras épaulières cadrent
        // correctement la scène, quel que soit le modèle visuel du personnage.
        EnsureLookAtComponent(anchorGO.transform);

        return anchorGO.transform;
    }
    private Transform EnsureCasterLookTargetAnchor_EnemyUnit(Transform spawnPoint)
    {
        if (spawnPoint == null)
            return null;

        const string anchorName = "CMVPoint_OverShoulder_CasterLookTarget";

        // Vérifie si un point existe déjà (scène, prefab ou précédent spawn) pour éviter les doublons.
        Transform existingAnchor = null;
        foreach (Transform child in spawnPoint.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (child == null)
                continue;

            if (string.Equals(child.name, anchorName, StringComparison.OrdinalIgnoreCase))
            {
                existingAnchor = child;
                break;
            }
        }

        if (existingAnchor != null)
        {
            // Garantit que le point continue d'orienter la caméra vers la cible active, même
            // lorsqu'il a été placé manuellement dans la scène.
            EnsureLookAtComponent(existingAnchor);
            return existingAnchor;
        }

        // Aucun point n'existait : on instancie un nouvel objet utilitaire dédié au guidage caméra.
        GameObject anchorGO = new(anchorName);
        anchorGO.transform.SetParent(spawnPoint, worldPositionStays: false);
        anchorGO.transform.localPosition = overShoulderCasterLookPointPosition_EnemyUnit;
        anchorGO.transform.localRotation = Quaternion.identity;

        EnsureLookAtComponent(anchorGO.transform);

        return anchorGO.transform;
    }

    /// <summary>
    /// S'assure qu'un composant <see cref="LookAtBattleTarget"/> est présent et configuré sur l'ancre donnée.
    /// </summary>
    /// <param name="anchor">Transform de l'ancre à valider.</param>
    private void EnsureLookAtComponent(Transform anchor)
    {
        if (anchor == null)
            return;

        // 🔄 Réutilise le composant existant lorsqu'il est déjà en place pour préserver les réglages
        // manuels éventuels, tout en rafraîchissant l'offset commun à toutes les ancres générées.
        LookAtBattleTarget lookAt = anchor.GetComponent<LookAtBattleTarget>();
        if (lookAt == null)
            lookAt = anchor.gameObject.AddComponent<LookAtBattleTarget>();

        lookAt.offset = overShoulderCasterLookOffset;
    }

}
