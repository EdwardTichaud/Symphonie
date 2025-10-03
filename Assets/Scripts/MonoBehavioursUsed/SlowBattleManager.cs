using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Gestionnaire expérimental de combats "lents" permettant d'enchaîner des tours
/// au ralenti tout en conservant un ressenti temps réel.
/// Cette version se concentre sur l'ossature générale : sélection des unités,
/// prévisualisation des actions, verrouillage/validation puis passage au tour
/// suivant. Le but est d'offrir un terrain d'expérimentation sans impacter le
/// <see cref="NewBattleManager"/> historique.
/// </summary>
public class SlowBattleManager : MonoBehaviour
{
    /// <summary>
    /// Singleton léger pour permettre aux autres systèmes (menus, UI…) de
    /// récupérer rapidement le gestionnaire en cours.
    /// </summary>
    public static SlowBattleManager Instance { get; private set; }

    [Header("Références")] 
    [Tooltip("Lien optionnel vers le NewBattleManager existant afin de réutiliser ses données (unités, cibles…).")]
    [SerializeField] private NewBattleManager legacyBattleManager;

    [Header("Rythme général")]
    [Tooltip("Durée maximale (en secondes temps réel) accordée à la prévisualisation avant validation automatique.")]
    [SerializeField, Min(0f)] private float previewTimeout = 1.25f;
    [Tooltip("Petite pause (en secondes temps réel) entre deux tours pour laisser respirer la mise en scène.")]
    [SerializeField, Min(0f)] private float downtimeBetweenTurns = 0.35f;
    [Tooltip("Facteur de ralenti appliqué durant la prévisualisation d'une action.")]
    [SerializeField, Range(0.01f, 1f)] private float previewTimeScale = 0.25f;
    [Tooltip("Lorsque activé, replace automatiquement les unités à leur point d'origine au changement de manche.")]
    [SerializeField] private bool resetPositionsAtRoundStart = true;

    [Header("Configuration")] 
    [Tooltip("Liste fixe des unités utilisées pour calculer l'ordre de jeu. L'initiative la plus élevée agira en dernier.")]
    [SerializeField] private List<CharacterUnit> orderedUnits = new();

    /// <summary>
    /// Constante utilisée pour simuler un incrément d'ATB identique à celui du <see cref="NewBattleManager"/>.
    /// La valeur est volontairement privée au gestionnaire lent afin d'éviter les dépendances croisées tout
    /// en conservant une référence facile à ajuster si la boucle historique venait à évoluer.
    /// </summary>
    private const float SimulatedAtbThreshold = 100f;

    /// <summary>
    /// Valeur de repli appliquée lorsqu'une unité dispose d'une initiative nulle ou négative. Sans ce garde-fou,
    /// la simulation d'ordre de jeu pourrait tourner indéfiniment, ce qui bloquerait l'ensemble de la boucle lente.
    /// </summary>
    private const float MinimumInitiativeStep = 0.1f;

    /// <summary>
    /// File courante des unités à jouer pendant la manche.
    /// </summary>
    private readonly Queue<CharacterUnit> turnQueue = new();

    /// <summary>
    /// Historique des positions initiales afin de restaurer le champ de bataille.
    /// </summary>
    private readonly Dictionary<CharacterUnit, TransformSnapshot> originByUnit = new();

    /// <summary>
    /// Action en attente transmise par l'interface (menus, IA…).
    /// </summary>
    private SlowBattleAction pendingAction;

    /// <summary>
    /// Indique si le gestionnaire attend encore la décision du joueur/IA.
    /// </summary>
    private bool waitingForAction;

    /// <summary>
    /// Unité actuellement en train de jouer son tour.
    /// </summary>
    private CharacterUnit currentUnit;

    /// <summary>
    /// Routine principale du combat (utilisée pour interrompre proprement la boucle).
    /// </summary>
    private Coroutine battleRoutine;

    /// <summary>
    /// Cache la valeur d'origine des time scales afin de pouvoir les restaurer.
    /// </summary>
    private float cachedTimeScale = 1f;
    private float cachedFixedDeltaTime = 0.02f;
    private bool timeScaleOverridden;

    /// <summary>
    /// Sémaphore indiquant que la prévisualisation est terminée (via timer ou appel explicite).
    /// </summary>
    private bool previewComplete;

    /// <summary>
    /// Compteur de manches jouées depuis le lancement du combat.
    /// </summary>
    private int roundIndex;

    /// <summary>
    /// Liste des séquences différées (reprise de Performing + retrait) à jouer en fin de manche.
    /// </summary>
    private readonly List<SlowBattleTimelineSequence> deferredTimelineSequences = new();

    /// <summary>
    /// Ensemble des unités dont la timeline de Performing a été suspendue via un Signal.
    /// Cette collection sert à identifier rapidement quelles timelines devront être reprises.
    /// </summary>
    private readonly HashSet<CharacterUnit> pausedPerformingUnits = new();

    #region Événements publics
    /// <summary>
    /// Notifie que la préparation d'une nouvelle manche débute.
    /// </summary>
    public event Action<int, IReadOnlyList<CharacterUnit>> OnRoundPrepared;

    /// <summary>
    /// Déclenché lorsqu'une unité devient active et que le temps est gelé pour choisir une action.
    /// </summary>
    public event Action<CharacterUnit> OnUnitSelectionBegan;

    /// <summary>
    /// Déclenché lorsque l'animation/prévisualisation d'une action démarre (ralenti actif).
    /// </summary>
    public event Action<CharacterUnit, SlowBattleAction> OnPreviewBegan;

    /// <summary>
    /// Déclenché juste avant la validation d'une action lorsque le temps est à nouveau figé.
    /// </summary>
    public event Action<CharacterUnit, SlowBattleAction> OnActionLocked;

    /// <summary>
    /// Déclenché une fois l'action officiellement résolue.
    /// </summary>
    public event Action<CharacterUnit, SlowBattleAction> OnActionResolved;

    /// <summary>
    /// Déclenché lorsque la boucle principale se termine (plus d'unités en état de combattre).
    /// </summary>
    public event Action OnBattleLoopCompleted;
    #endregion

    #region Cycle de vie
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        cachedTimeScale = Time.timeScale;
        cachedFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        RestoreGlobalTimeScale();
    }

    private void OnDisable()
    {
        // Sécurité : s'assure qu'aucun ralenti ne fuit lorsque le composant est désactivé.
        RestoreGlobalTimeScale();
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Connecte le gestionnaire historique afin de synchroniser les états (UI, caméras…).
    /// </summary>
    public void ConfigureWithClassicManager(NewBattleManager manager)
    {
        legacyBattleManager = manager;
    }

    /// <summary>
    /// Prépare les listes d'unités et les snapshots de position avant de lancer le combat.
    /// </summary>
    /// <param name="allUnits">Ensemble complet des unités présentes sur le champ de bataille.</param>
    /// <param name="activeUnits">Sous-ensemble filtré (HP &gt; 0) utilisé pour calculer l'ordre de jeu.</param>
    public void InitializeBattle(IReadOnlyList<CharacterUnit> allUnits, IReadOnlyList<CharacterUnit> activeUnits)
    {
        if (allUnits == null)
        {
            Debug.LogWarning("[SlowBattleManager] Impossible d'initialiser la bataille : aucune unité fournie.");
            return;
        }

        CacheOrigins(allUnits);

        orderedUnits = activeUnits != null
            ? activeUnits.Where(u => u != null && !u.IsDead).Distinct().ToList()
            : allUnits.Where(u => u != null && !u.IsDead).Distinct().ToList();

        // Tri personnalisé : initiative faible = agir en premier, initiative haute = agir en dernier.
        // On délègue néanmoins le calcul à une méthode dédiée pour reproduire le comportement du NewBattleManager
        // (accumulation d'ATB) tout en inversant le résultat final conformément au gameplay attendu.
        RefreshOrderedUnitsFromInitiative();

        roundIndex = 0;
    }
    #endregion

    #region Calcul d'ordre d'initiative
    /// <summary>
    /// Reconstruit la liste ordonnée d'unités en se basant sur leur initiative actuelle. L'objectif est de proposer un ordre
    /// cohérent avec la boucle historique (<see cref="NewBattleManager"/>) tout en inversant le résultat pour que les
    /// initiatives élevées agissent en fin de manche.
    /// </summary>
    private void RefreshOrderedUnitsFromInitiative()
    {
        if (orderedUnits == null)
            orderedUnits = new List<CharacterUnit>();

        // Si le gestionnaire classique est disponible, on récupère sa vision des unités pour rester parfaitement synchronisé.
        IEnumerable<CharacterUnit> sourceUnits = legacyBattleManager != null && legacyBattleManager.unitsInBattle != null
            ? legacyBattleManager.unitsInBattle
            : orderedUnits;

        List<CharacterUnit> aliveUnits = sourceUnits
            .Where(u => u != null && !u.IsDead)
            .Distinct()
            .ToList();

        orderedUnits.Clear();

        if (aliveUnits.Count == 0)
            return;

        List<CharacterUnit> simulatedOrder = SimulateInvertedInitiativeOrdering(aliveUnits);

        orderedUnits.AddRange(simulatedOrder);
    }

    /// <summary>
    /// Simule une boucle ATB identique à celle du <see cref="NewBattleManager"/> pour déterminer l'ordre naturel des unités.
    /// Le résultat est ensuite inversé afin que la plus grande initiative termine la manche, conformément à la variante lente.
    /// </summary>
    private List<CharacterUnit> SimulateInvertedInitiativeOrdering(List<CharacterUnit> aliveUnits)
    {
        // Copie locale de la liste pour garantir un parcours stable pendant la simulation.
        var workingList = aliveUnits.ToList();
        var remainingUnits = new HashSet<CharacterUnit>(workingList);
        var simulatedMeters = new Dictionary<CharacterUnit, float>(workingList.Count);
        var increments = new Dictionary<CharacterUnit, float>(workingList.Count);

        foreach (CharacterUnit unit in workingList)
        {
            float initiativeStep = GetEffectiveInitiativeStep(unit);
            simulatedMeters[unit] = 0f;
            increments[unit] = initiativeStep;
        }

        var orderedByInitiative = new List<CharacterUnit>(workingList.Count);
        const int safetyCap = 2048;
        int iterations = 0;

        while (remainingUnits.Count > 0 && iterations < safetyCap)
        {
            foreach (CharacterUnit unit in workingList)
            {
                if (!remainingUnits.Contains(unit))
                    continue;

                simulatedMeters[unit] += increments[unit];
                if (simulatedMeters[unit] >= SimulatedAtbThreshold)
                {
                    orderedByInitiative.Add(unit);
                    remainingUnits.Remove(unit);
                }
            }

            iterations++;
        }

        if (remainingUnits.Count > 0)
        {
            Debug.LogWarning("[SlowBattleManager] Seuil de sécurité atteint pendant le calcul d'initiative : " +
                "ajout des unités restantes en fin de liste.");

            // On trie les unités restantes selon leur incrément théorique pour rester le plus cohérent possible.
            orderedByInitiative.AddRange(remainingUnits.OrderBy(u => increments[u]));
        }

        // L'ordre obtenu correspond à celui du gestionnaire rapide (initiative forte = premier).
        // On inverse donc le tableau pour coller à la règle inverse : initiative forte = dernier.
        orderedByInitiative.Reverse();

        return orderedByInitiative;
    }

    /// <summary>
    /// Renvoie l'incrément d'initiative à utiliser dans la simulation. Les valeurs nulles ou négatives sont remplacées par un
    /// pas minimal afin d'éviter les boucles infinies tout en conservant une différence perceptible entre les unités.
    /// </summary>
    private static float GetEffectiveInitiativeStep(CharacterUnit unit)
    {
        if (unit == null)
            return MinimumInitiativeStep;

        float rawInitiative = unit.currentInitiative;

        if (float.IsNaN(rawInitiative) || float.IsInfinity(rawInitiative) || rawInitiative <= 0f)
            return MinimumInitiativeStep;

        return rawInitiative;
    }
    #endregion

    #region Boucle principale
    /// <summary>
    /// Lance la boucle de combat lente. Peut être rappelé pour relancer après une interruption.
    /// </summary>
    public void BeginBattleLoop()
    {
        // Petite synchronisation de sécurité : si l'initiative d'une unité a été modifiée juste avant le lancement
        // explicite de la boucle, on recalcule immédiatement l'ordre pour éviter un premier tour incohérent.
        RefreshOrderedUnitsFromInitiative();

        if (orderedUnits == null || orderedUnits.Count == 0)
        {
            Debug.LogWarning("[SlowBattleManager] Aucun combattant actif : la boucle lente ne démarrera pas.");
            return;
        }

        if (battleRoutine != null)
            StopCoroutine(battleRoutine);

        battleRoutine = StartCoroutine(BattleLoop());
    }

    /// <summary>
    /// Interrompt immédiatement la boucle et restaure les valeurs temporelles.
    /// </summary>
    public void AbortBattleLoop()
    {
        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }

        RestoreGlobalTimeScale();
        waitingForAction = false;
        currentUnit = null;
        pendingAction = null;
        turnQueue.Clear();
        roundIndex = 0;
        deferredTimelineSequences.Clear();
        pausedPerformingUnits.Clear();
    }

    private IEnumerator BattleLoop()
    {
        while (true)
        {
            // Retire les unités mortes avant de préparer la manche.
            orderedUnits.RemoveAll(u => u == null || u.IsDead);

            if (orderedUnits.Count == 0)
                break;

            PrepareNewRound();

            while (turnQueue.Count > 0)
            {
                CharacterUnit unit = turnQueue.Dequeue();
                if (unit == null || unit.IsDead)
                    continue;

                yield return HandleTurn(unit);
            }

            if (roundIndex == 0)
            {
                yield return PlayDeferredTimelineSequences();
            }

            roundIndex++;
        }

        OnBattleLoopCompleted?.Invoke();
        RestoreGlobalTimeScale();
    }

    /// <summary>
    /// Joue séquentiellement toutes les timelines différées à la fin du premier tour.
    /// </summary>
    private IEnumerator PlayDeferredTimelineSequences()
    {
        if (deferredTimelineSequences.Count == 0)
            yield break;

        // On restaure un timeScale normal pour laisser les timelines se dérouler correctement.
        RestoreGlobalTimeScale();

        foreach (SlowBattleTimelineSequence sequence in deferredTimelineSequences)
        {
            if (sequence == null)
                continue;

            if (RhythmQTEManager.Instance != null)
                yield return RhythmQTEManager.Instance.PlayDeferredSlowSequence(sequence);
        }

        deferredTimelineSequences.Clear();
        pausedPerformingUnits.Clear();
    }

    /// <summary>
    /// Appelé par un Signal de Performing pour mémoriser la pause sur l'unité concernée.
    /// </summary>
    public void NotifyPerformingTimelinePaused(CharacterUnit unit)
    {
        if (unit == null)
            return;

        pausedPerformingUnits.Add(unit);
    }

    /// <summary>
    /// Vérifie si une unité possède actuellement une Performing suspendue en attente de reprise.
    /// </summary>
    public bool IsPerformingTimelinePaused(CharacterUnit unit)
    {
        return unit != null && pausedPerformingUnits.Contains(unit);
    }

    /// <summary>
    /// Indique que la Performing de l'unité peut être retirée des pauses suivies (reprise effectuée).
    /// </summary>
    public void MarkPerformingTimelineResumed(CharacterUnit unit)
    {
        if (unit == null)
            return;

        pausedPerformingUnits.Remove(unit);
    }

    private void PrepareNewRound()
    {
        if (resetPositionsAtRoundStart)
            RestoreUnitsToOrigin();

        // Avant de constituer la file, on recalcule l'ordre complet pour intégrer les éventuelles variations d'initiative
        // (buffs, debuffs, objets...) survenues depuis la manche précédente.
        RefreshOrderedUnitsFromInitiative();

        turnQueue.Clear();
        foreach (CharacterUnit unit in orderedUnits)
            turnQueue.Enqueue(unit);

        OnRoundPrepared?.Invoke(roundIndex, orderedUnits);

        if (legacyBattleManager != null)
            legacyBattleManager.ChangeBattleState(BattleState.NewTurn);
    }

    private IEnumerator HandleTurn(CharacterUnit unit)
    {
        currentUnit = unit;
        waitingForAction = true;
        pendingAction = null;
        previewComplete = false;

        FreezeForDecision();
        SynchronizeWithLegacyManager_OnSelection(unit);
        OnUnitSelectionBegan?.Invoke(unit);

        // Dès que le tour d'une unité contrôlée par l'IA commence, on déclenche immédiatement
        // un comportement par défaut. Jusqu'à l'implémentation complète de la prise de décision
        // ennemie, le tour est automatiquement passé pour éviter de bloquer la boucle.
        TryAutoResolveTurnForNonPlayer(unit);

        // Attend qu'une action soit soumise par les menus/IA.
        while (waitingForAction && unit != null && !unit.IsDead)
            yield return null;

        if (unit == null || unit.IsDead)
        {
            // L'unité est morte pendant l'attente : on réinitialise proprement.
            RestoreGlobalTimeScale();
            currentUnit = null;
            pendingAction = null;
            waitingForAction = false;
            yield break;
        }

        if (pendingAction == null)
        {
            // Sécurité : crée une action "passer" par défaut pour éviter de bloquer la boucle.
            pendingAction = SlowBattleAction.CreateSkip(unit, "Aucune action définie");
        }

        if (!pendingAction.skipPreview)
        {
            ApplyPreviewTimeScale();
            OnPreviewBegan?.Invoke(unit, pendingAction);

            float elapsed = 0f;
            while (!previewComplete && (previewTimeout <= 0f || elapsed < previewTimeout))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        PauseBeforeResolution();
        OnActionLocked?.Invoke(unit, pendingAction);

        ResolveAction(pendingAction);
        OnActionResolved?.Invoke(unit, pendingAction);

        if (legacyBattleManager != null)
            legacyBattleManager.ChangeBattleState(BattleState.EndTurn);

        RestoreGlobalTimeScale();
        currentUnit = null;
        pendingAction = null;
        waitingForAction = false;

        if (downtimeBetweenTurns > 0f)
            yield return new WaitForSecondsRealtime(downtimeBetweenTurns);
    }
    #endregion

    #region Soumission et synchronisation
    /// <summary>
    /// Permet aux menus ou à l'IA d'enregistrer l'action à jouer pour l'unité courante.
    /// </summary>
    public void SubmitAction(SlowBattleAction action)
    {
        if (currentUnit == null)
        {
            Debug.LogWarning("[SlowBattleManager] Aucun tour en cours : impossible d'enregistrer une action.");
            return;
        }

        pendingAction = action ?? SlowBattleAction.CreateSkip(currentUnit, "Action nulle remplacée par un passage");
        waitingForAction = false;
    }

    /// <summary>
    /// Met de côté une séquence de timeline à jouer en fin de manche (reprise de Performing et retrait).
    /// </summary>
    /// <param name="sequence">Séquence construite par le <see cref="RhythmQTEManager"/>.</param>
    public void RegisterDeferredTimelineSequence(SlowBattleTimelineSequence sequence)
    {
        if (sequence == null)
            return;

        if (roundIndex > 0)
        {
            // Hors premier tour, on joue immédiatement la séquence pour éviter toute accumulation imprévue.
            if (RhythmQTEManager.Instance != null)
                StartCoroutine(RhythmQTEManager.Instance.PlayDeferredSlowSequence(sequence));
            return;
        }

        deferredTimelineSequences.Add(sequence);
    }

    /// <summary>
    /// Notifie que la prévisualisation manuelle est terminée (ex : fin d'une animation).
    /// </summary>
    public void NotifyPreviewComplete()
    {
        previewComplete = true;
    }

    /// <summary>
    /// Force le passage de tour (utilisé par un bouton "Fin de tour" ou pour gérer un KO).
    /// </summary>
    public void RequestSkipCurrentTurn(CharacterUnit forcedUnit = null, string reason = "Passage forcé")
    {
        if (currentUnit == null)
            return;

        if (forcedUnit != null && forcedUnit != currentUnit)
        {
            Debug.LogWarning("[SlowBattleManager] Tentative de forcer le tour d'une unité qui n'est pas active.");
            return;
        }

        pendingAction = SlowBattleAction.CreateSkip(currentUnit, reason);
        waitingForAction = false;
    }

    private void SynchronizeWithLegacyManager_OnSelection(CharacterUnit unit)
    {
        if (legacyBattleManager == null || unit == null)
            return;

        legacyBattleManager.ChangeCurrentCharacterUnit(unit);

        if (unit.Data != null && unit.Data.isPlayerControlled)
            legacyBattleManager.ChangeBattleState(BattleState.SquadUnit_MainMenu);
        else
            legacyBattleManager.ChangeBattleState(BattleState.EnemyUnit_Reflexion);
    }

    /// <summary>
    /// Résout immédiatement le tour des unités ennemies tant que l'IA n'a pas été branchée.
    /// Cela empêche la boucle principale de rester bloquée en attendant un ordre qui n'arrivera pas.
    /// </summary>
    /// <param name="unit">Unité actuellement active.</param>
    private void TryAutoResolveTurnForNonPlayer(CharacterUnit unit)
    {
        if (unit == null)
            return;

        // On ne touche jamais aux personnages contrôlés par le joueur : ils doivent conserver
        // l'accès au menu et aux interactions manuelles.
        if (unit.Data != null && unit.Data.isPlayerControlled)
            return;

        // Si une action est déjà en attente (par exemple définie par une future IA),
        // on n'écrase pas ce choix et on laisse la boucle attendre sa résolution naturelle.
        if (pendingAction != null || !waitingForAction)
            return;

        // Les ennemis passent simplement leur tour en attendant l'arrivée d'un véritable système d'IA.
        // Le libellé explicite facilite le débogage dans la console lorsque le comportement s'exécute.
        SubmitAction(SlowBattleAction.CreateSkip(unit, "Passage automatique de l'ennemi (IA non implémentée)"));
    }

    private void ResolveAction(SlowBattleAction action)
    {
        if (legacyBattleManager != null)
        {
            // Les états "performe" permettent de réutiliser les hooks existants (caméra, VFX…).
            if (currentUnit != null && currentUnit.Data != null && currentUnit.Data.isPlayerControlled)
                legacyBattleManager.ChangeBattleState(BattleState.SquadUnit_PerformingMusicalMove);
            else
                legacyBattleManager.ChangeBattleState(BattleState.EnemyUnit_PerformingMusicalMove);
        }

        if (action == null)
            return;

        Debug.Log($"[SlowBattleManager] Résolution de l'action '{action.debugLabel}' pour {currentUnit?.name}.");
    }
    #endregion

    #region Gestion du temps
    private void FreezeForDecision()
    {
        CacheTimeValues();
        Time.timeScale = 0f;
    }

    private void ApplyPreviewTimeScale()
    {
        CacheTimeValues();
        Time.timeScale = previewTimeScale;
        Time.fixedDeltaTime = cachedFixedDeltaTime * previewTimeScale;
    }

    private void PauseBeforeResolution()
    {
        CacheTimeValues();
        Time.timeScale = 0f;
    }

    private void CacheTimeValues()
    {
        if (timeScaleOverridden)
            return;

        cachedTimeScale = Time.timeScale;
        cachedFixedDeltaTime = Time.fixedDeltaTime;
        timeScaleOverridden = true;
    }

    private void RestoreGlobalTimeScale()
    {
        if (!timeScaleOverridden)
            return;

        Time.timeScale = cachedTimeScale;
        Time.fixedDeltaTime = cachedFixedDeltaTime;
        timeScaleOverridden = false;
    }
    #endregion

    #region Gestion des positions
    private void CacheOrigins(IReadOnlyList<CharacterUnit> units)
    {
        originByUnit.Clear();
        if (units == null)
            return;

        foreach (CharacterUnit unit in units)
        {
            if (unit == null)
                continue;

            originByUnit[unit] = new TransformSnapshot
            {
                position = unit.transform.position,
                rotation = unit.transform.rotation,
                parent = unit.transform.parent
            };
        }
    }

    private void RestoreUnitsToOrigin()
    {
        foreach (KeyValuePair<CharacterUnit, TransformSnapshot> kvp in originByUnit)
        {
            CharacterUnit unit = kvp.Key;
            TransformSnapshot snapshot = kvp.Value;

            if (unit == null)
                continue;

            if (snapshot.parent != null)
                unit.transform.SetParent(snapshot.parent, worldPositionStays: true);

            unit.transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
        }
    }
    #endregion

    #region Structures internes
    [Serializable]
    private struct TransformSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Transform parent;
    }
    #endregion
}
