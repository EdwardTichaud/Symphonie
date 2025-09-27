using System;
using UnityEngine;
using UnityEngine.Playables; // 📽️ Gestion des timelines propres à l'unité
using UnityEngine.Timeline;  // 🎼 Lecture des TimelineAsset assignés au PlayableDirector local
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Représente une unité de combat. L'ajout d'un <see cref="CharacterController"/>
/// permet de déléguer la gestion de la gravité à Unity pour plus de cohérence.
/// Chaque unité embarque désormais son propre <see cref="PlayableDirector"/>
/// afin de conserver l'ensemble des effets narratifs décrits dans l'Histoire de
/// Symphonie tout en gagnant en modularité lors de la lecture des timelines.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayableDirector))]
public class CharacterUnit : MonoBehaviour, IDamageable, IHealable, IBuffable, IDebuffable
{
    public CharacterData Data;

    [Header("UI Components")]
    public HPBar hpBar;
    public CustomBar customBar;

    [Header("Animations")]
    public AnimationClip hurtAnimation;
    public AnimationClip interceptedAnimation;
    public AnimationClip interceptionAnimation;

    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    [HideInInspector] public Animator animator;
    // Indicateur évitant les multiples avertissements lorsque l'Animator enfant est introuvable.
    private bool hasLoggedMissingChildAnimator;
    // Indicateur évitant de répéter les avertissements lorsqu'on détecte plusieurs Animator valides chez les enfants.
    private bool hasLoggedMultipleChildAnimatorsWithController;
    private AwakeState awakeState;

    /// <summary>Hash du state Animator "Idle_Battle" pour accélérer les vérifications de disponibilité.</summary>
    private static readonly int AnimatorStateIdleBattle = Animator.StringToHash("Idle_Battle");
    /// <summary>Durée standard (en secondes) appliquée aux fondus d'animations idle.</summary>
    private const float IdleCrossFadeDurationSeconds = 0.1f;

    // Mise en cache des ancres caméra pour éviter de reparcourir toute la hiérarchie à chaque requête.
    private readonly Dictionary<string, Transform> cachedCameraAnchors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// PlayableDirector individuel utilisé pour jouer les timelines de combat
    /// propres à cette unité. Ce composant remplace l'ancien système centralisé
    /// et garantit que les pistes "Caster" restent toujours correctement liées.
    /// </summary>
    private PlayableDirector battleDirector;

    /// <summary>
    /// Expose le PlayableDirector de combat à des fins de diagnostic tout en
    /// conservant la protection d'accès pour éviter toute réaffectation externe.
    /// </summary>
    public PlayableDirector BattleDirector => battleDirector;

    /// <summary>
    /// Indique si l'unité est en état Awake (fusion avec l'ange gardien).
    /// </summary>
    public bool IsAwake => awakeState != null && awakeState.IsAwake;

    // Gestionnaire de physique pour déléguer les collisions et la gravité à Unity.
    private CharacterController controller;
    // Vitesse verticale utilisée pour la chute des unités terrestres.
    private Vector3 fallVelocity = Vector3.zero;
    // Intensité de la gravité appliquée.
    private const float gravity = -9.81f;

    [Header("Détection du sol")]
    [Tooltip("Distance maximale pour vérifier la présence d'un sol sous l'unité.")]
    public float groundCheckDistance = 2f;
    [Tooltip("Layers considérés comme du sol pendant les combats.")]
    public LayerMask battleGroundLayer = 0;

    /// <summary>
    /// Indique si l'unité touche actuellement un support solide.
    /// </summary>
    public bool IsGrounded => controller != null && controller.isGrounded;

    /// <summary>
    /// Indique si l'unité est de type aérien.
    /// </summary>
    public bool IsAirUnit => Data != null && Data.isAirUnit;

    /// <summary>
    /// Détecte si un sol existe sous l'unité à portée de <see cref="groundCheckDistance"/>.
    /// Cette information sert non seulement à permettre aux attaques terrestres
    /// d'atteindre une cible en vol lorsqu'un support se trouve sous elle,
    /// mais aussi à empêcher toute attaque aérienne lorsque la moindre surface
    /// solide est détectée, fidèle au récit de l'Histoire de Symphonie où les
    /// résonances du sol répondent aux cieux.
    /// </summary>
    public bool HasGroundBelow()
    {
        // Définit un masque par défaut si aucun n'est précisé dans l'éditeur.
        if (battleGroundLayer == 0)
            battleGroundLayer = LayerMask.GetMask("Battle_Ground");

        // Lancement d'un rayon vers le bas pour vérifier la présence d'un sol.
        Vector3 origin = transform.position;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance, battleGroundLayer);
    }

    /// <summary>
    /// Recherche un Animator enfant à utiliser pour les timelines des MusicalMoves
    /// ou des Items. Les GameObjects inactifs sont examinés pour couvrir les
    /// modèles masqués dans l'éditeur, et l'objet racine portant le CharacterUnit
    /// est explicitement ignoré afin de respecter les consignes de binding.
    /// </summary>
    private Animator FindChildAnimatorForBindings()
    {
        var childAnimators = GetComponentsInChildren<Animator>(includeInactive: true);
        Animator animatorWithController = null;
        bool multipleControllersDetected = false; // Permet de réinitialiser l'avertissement si la hiérarchie redevient saine.

        foreach (var candidate in childAnimators)
        {
            if (candidate == null)
                continue; // Sécurité défensive : on ignore les références manquantes dans la hiérarchie.

            if (candidate.gameObject == gameObject)
                continue; // On passe l'objet racine pour se concentrer sur les véritables modèles.

            if (candidate.runtimeAnimatorController == null)
                continue; // L'Animator ne possède pas de controller : il ne peut pas piloter les animations requises.

            if (animatorWithController == null)
            {
                animatorWithController = candidate; // Premier candidat valide trouvé : on le met en mémoire.
                continue;
            }

            multipleControllersDetected = true; // Au moins deux Animator possèdent un controller dans la hiérarchie.

            if (!hasLoggedMultipleChildAnimatorsWithController)
            {
                Debug.LogWarning(
                    $"[CharacterUnit] Plusieurs Animator enfants avec un controller trouvés sur '{name}'. " +
                    $"Utilisation du premier détecté : '{animatorWithController.name}'.");
                hasLoggedMultipleChildAnimatorsWithController = true; // On évite de spammer la console si la situation persiste.
            }
        }

        if (!multipleControllersDetected)
            hasLoggedMultipleChildAnimatorsWithController = false; // Aucun doublon détecté : on autorise un futur avertissement si nécessaire.

        return animatorWithController;
    }

    /// <summary>
    /// Expose (et met en cache) l'Animator enfant servant de référence lors du
    /// lancement d'un MusicalMove ou de l'utilisation d'un objet. Un repli sur
    /// l'Animator du GameObject principal est effectué si aucun enfant n'est
    /// trouvé afin d'éviter de bloquer la progression du combat.
    /// </summary>
    public Animator GetCasterAnimator(bool forceRefresh = false)
    {
        bool cacheInvalid = forceRefresh || animator == null
            || (animator.gameObject == gameObject && !hasLoggedMissingChildAnimator);

        if (!cacheInvalid)
            return animator;

        Animator childAnimator = FindChildAnimatorForBindings();
        if (childAnimator != null)
        {
            animator = childAnimator;
            hasLoggedMissingChildAnimator = false;
            return animator;
        }

        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null)
        {
            animator = rootAnimator;

            if (!hasLoggedMissingChildAnimator)
            {
                Debug.LogWarning($"[CharacterUnit] Aucun Animator enfant trouvé sur '{name}'. Utilisation de l'Animator du GameObject racine par défaut.");
                hasLoggedMissingChildAnimator = true;
            }

            return animator;
        }

        if (!hasLoggedMissingChildAnimator)
        {
            Debug.LogWarning($"[CharacterUnit] Impossible de localiser un Animator pour '{name}'. Les timelines risquent de ne pas se jouer correctement.");
            hasLoggedMissingChildAnimator = true;
        }

        animator = null;
        return animator;
    }

    /// <summary>
    /// Fournit directement l'objet à utiliser comme binding "Caster" pour les
    /// timelines. En absence d'Animator enfant, l'objet racine est renvoyé pour
    /// éviter toute erreur bloquante.
    /// </summary>
    public GameObject GetCasterBindingTarget()
    {
        Animator casterAnimator = GetCasterAnimator();
        return casterAnimator != null ? casterAnimator.gameObject : gameObject;
    }

    /// <summary>
    /// Recherche (et met en cache) une ancre caméra particulière sur le CharacterUnit.
    /// </summary>
    /// <param name="anchorName">Nom exact du point de caméra recherché.</param>
    /// <param name="includeInactive">
    /// Vrai pour parcourir également les objets désactivés (utile lorsque certains repères sont masqués dans l'éditeur).
    /// </param>
    /// <returns>La transform correspondant à l'ancre ou <c>null</c> si elle est introuvable.</returns>
    public Transform GetCameraAnchor(string anchorName, bool includeInactive = true, bool logWarning = true)
    {
        if (string.IsNullOrEmpty(anchorName))
            return null;

        if (cachedCameraAnchors.TryGetValue(anchorName, out var cached))
        {
            // Si l'ancre existe encore, on la renvoie directement.
            if (cached != null)
                return cached;

            // Une valeur nulle indique que l'ancre est introuvable : on évite un nouveau parcours coûteux.
            return null;
        }

        Transform located = LocateCameraAnchor(anchorName, includeInactive);
        cachedCameraAnchors[anchorName] = located;

        if (located == null && logWarning)
        {
            Debug.LogWarning($"[CharacterUnit] Ancre caméra '{anchorName}' introuvable sur '{name}'.");
        }

        return located;
    }

    /// <summary>
    /// Rôle principal associé au point caméra recherché lorsqu'on souhaite
    /// fournir un override explicite au <see cref="BattleCameraManager"/>.
    /// </summary>
    public enum CameraAnchorPurpose
    {
        /// <summary>L'ancre doit suivre le lanceur de l'action.</summary>
        Caster,
        /// <summary>L'ancre doit suivre la cible de l'action.</summary>
        Target
    }

    /// <summary>
    /// Détermine un point caméra pertinent pour l'unité selon le rôle
    /// souhaité (lanceur ou cible). Les ancres déclarées via les GameObjects
    /// « CMVPoint_… » sont privilégiées afin de reproduire les cadrages
    /// définis par les artistes, puis un repli progressif est appliqué vers
    /// l'Animator de binding et, en dernier recours, vers le transform racine.
    /// </summary>
    /// <param name="purpose">Indique si l'on cherche un point côté lanceur ou côté cible.</param>
    /// <param name="includeInactive">
    /// Vrai pour considérer également les ancres désactivées dans la hiérarchie.
    /// </param>
    /// <returns>
    /// Le transform de l'ancre dédiée, celui de l'Animator utilisé pour les bindings,
    /// ou, à défaut, le transform racine de l'unité.
    /// </returns>
    public Transform GetDefaultCameraAnchor(CameraAnchorPurpose purpose, bool includeInactive = true)
    {
        // ⚖️ Liste de priorité différente selon le rôle recherché :
        //     * côté lanceur -> on privilégie les points « OverShoulder » pour cadrer l'action.
        //     * côté cible   -> on vise d'abord la réaction puis les autres plans utiles.
        string[] preferredAnchors = purpose == CameraAnchorPurpose.Caster
            ? new[]
            {
                "CMVPoint_OverShoulder_CasterToTarget",
                "CMVPoint_OverShoulder_CasterLookTarget",
                "CMVPoint_TargetReaction",
                "CMVPoint_OrbitAroundUnit"
            }
            : new[]
            {
                "CMVPoint_TargetReaction",
                "CMVPoint_OverShoulder_CasterLookTarget",
                "CMVPoint_OverShoulder_CasterToTarget",
                "CMVPoint_OrbitAroundUnit"
            };

        foreach (string anchorName in preferredAnchors)
        {
            Transform anchor = GetCameraAnchor(anchorName, includeInactive, logWarning: false);
            if (anchor != null)
                return anchor;
        }

        // 🎭 Aucun point spécifique n'est présent : on retombe sur l'Animator qui
        // sert déjà aux timelines, garantissant un pivot cohérent pour la caméra.
        Animator bindingAnimator = GetCasterAnimator();
        if (bindingAnimator != null)
            return bindingAnimator.transform;

        // 🔚 Dernier recours : le transform racine pour ne jamais renvoyer null.
        return transform;
    }

    /// <summary>
    /// Parcourt récursivement la hiérarchie pour trouver une ancre caméra.
    /// </summary>
    private Transform LocateCameraAnchor(string anchorName, bool includeInactive)
    {
        Transform[] children = GetComponentsInChildren<Transform>(includeInactive);
        foreach (var child in children)
        {
            if (string.Equals(child.name, anchorName, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        // 🔁 Aucun point n'est présent directement sur l'unité : on vérifie le parent immédiat
        // (PlayerPosition_X / EnemyPosition_X) qui peut désormais générer dynamiquement ces ancres.
        Transform parent = transform.parent;
        if (parent != null)
        {
            // Recherche d'abord l'enfant direct pour éviter une exploration trop large.
            Transform parentAnchor = parent.Find(anchorName);
            if (parentAnchor != null)
                return parentAnchor;

            foreach (Transform sibling in parent.GetComponentsInChildren<Transform>(includeInactive))
            {
                if (sibling == null || sibling == transform)
                    continue;

                if (string.Equals(sibling.name, anchorName, StringComparison.OrdinalIgnoreCase))
                    return sibling;
            }
        }

        return null;
    }

    /// <summary>
    /// Détermine l'Animator le plus adapté à partir d'un GameObject donné pour
    /// configurer les bindings d'une timeline. Cette méthode garantit que la
    /// recherche respecte la règle imposant l'utilisation d'un Animator enfant.
    /// </summary>
    private Animator ResolveAnimatorForBinding(GameObject bindingSource)
    {
        if (bindingSource == null)
            return GetCasterAnimator();

        if (bindingSource == gameObject)
            return GetCasterAnimator();

        Animator directAnimator = bindingSource.GetComponent<Animator>();
        if (directAnimator != null)
            return directAnimator;

        Animator nestedAnimator = bindingSource.GetComponentInChildren<Animator>(includeInactive: true);
        if (nestedAnimator != null)
            return nestedAnimator;

        return GetCasterAnimator();
    }

    private void Awake()
    {
        // S'assure qu'un CharacterController est présent pour gérer la physique.
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        // Centralise la récupération (ou l'ajout) du PlayableDirector afin que
        // chaque unité puisse lire ses propres timelines de MusicalMoves ou d'objets.
        battleDirector = GetComponent<PlayableDirector>();
        if (battleDirector == null)
            battleDirector = gameObject.AddComponent<PlayableDirector>();

        // Les timelines sont déclenchées manuellement, on désactive donc toute
        // lecture automatique pour éviter une répétition non désirée en scène.
        battleDirector.playOnAwake = false;
    }

    public CharacterType characterType => Data.characterType;

    private float _currentHP;
    public float currentHP
    {
        get => _currentHP;
        set
        {
            bool wasDead = _currentHP <= 0f;
            _currentHP = value;
            if (Data != null)
                Data.currentHP = value;

            if (wasDead && _currentHP > 0f && Data != null && Data.characterName == "Lucian")
            {
                BattleCameraShatter shatter = FindFirstObjectByType<BattleCameraShatter>();
                shatter?.ResetEffect();
            }
        }
    }
    public float currentMP;
    public float currentRage { get => Data.currentRage; set => Data.currentRage = value; }

    public float currentStrength { get => Data.currentStrength; set => Data.currentStrength = value; }
    public float currentDefense { get => Data.currentDefense; set => Data.currentDefense = value; }
    public float currentReflex { get => Data.currentReflex; set => Data.currentReflex = value; }
    public float currentMobility { get => Data.currentMobility; set => Data.currentMobility = value; }
    public float currentPower { get => Data.currentPower; set => Data.currentPower = value; }
    public float currentStability { get => Data.currentStability; set => Data.currentStability = value; }
    public float currentVitality { get => Data.currentVitality; set => Data.currentVitality = value; }
    public float currentSagacity { get => Data.currentSagacity; set => Data.currentSagacity = value; }

    public float currentMusicalGauge;
    // Nouvelle réserve d'harmoniques par type
    public Dictionary<HarmonicType, int> harmonicReserve = new();
    public Dictionary<MusicalMoveSO, int> moveCooldowns = new();
    // Compteurs d'utilisation des attaques musicales
    // Clé : move, Valeur : nombre d'utilisations
    public Dictionary<MusicalMoveSO, int> moveUsesThisTurn = new();
    public Dictionary<MusicalMoveSO, int> moveUsesThisBattle = new();
    public float currentFatigue { get => Data.currentFatigue; set => Data.currentFatigue = value; }

    // Gestion de l'initiative
    public float currentInitiative { get => Data.currentInitiative; set => Data.currentInitiative = value; }
    public float currentATB = 0f;
    public float ATBMax = 100f;
    public bool IsReady => currentATB >= ATBMax && currentHP > 0;

    private bool deathTriggered;
    /// <summary>
    /// Indique si l'unité est définitivement morte
    /// </summary>
    public bool IsDead => deathTriggered || currentHP <= 0f;
    public event System.Action<CharacterUnit> OnDeath;
    public bool isReadyToParry;
    // Indique si l'unité est immunisée à l'interception. Visible pour faciliter
    // le débogage pendant le combat.
    public bool isInterceptionImmune = false;
    // Nombre de tours restants pour l'immunité à l'interception. Visible pour
    // suivre précisément la durée de l'effet.
    public int interceptionImmunityTurns = 0;

    [Header("Récompenses de combat")]
    public List<ItemData> lootItems = new();
    public int experienceReward = 0;

    #region Cycle de Vie
    /// <summary>
    /// Initialise toutes les statistiques du personnage selon sa fiche.
    /// </summary>
    public void Initialize(CharacterData characterData)
    {
        Data = characterData;
        Data.owner = this;

        // Initialisation des stats
        currentPower = Data.basePower;
        currentStability = Data.baseStability;
        currentVitality = Data.baseVitality;
        currentSagacity = Data.baseSagacity;
        // Les HP doivent rester persistants entre les combats
        if (Data.currentHP <= 0)
            Data.currentHP = Data.baseHP + currentVitality;
        currentHP = Data.currentHP;
        currentRage = Data.baseRage;
        currentInitiative = Data.baseInitiative;
        currentStrength = Data.baseStrength;
        currentDefense = Data.baseDefense;
        currentReflex = Data.baseReflex;
        currentMobility = Data.baseMobility;
        currentFatigue = Data.baseFatigue;

        harmonicReserve.Clear();
        AddHarmonic(Data.harmonicType);

        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        // Recherche proactive de l'Animator dédié aux timelines dans les enfants (même inactifs).
        animator = GetCasterAnimator(forceRefresh: true);
        awakeState = GetComponent<AwakeState>();

        // Setup graphique
        if (spriteRenderer != null && Data.portrait != null)
            spriteRenderer.sprite = Data.portrait;

        // UI HP
        if (hpBar != null)
        {
            hpBar.SetMaxValue(Data.baseHP + currentVitality);
            hpBar.SetValue(currentHP);
        }

        if (customBar != null)
        {
            var concentration = GetComponent<ConcentrationSystem>();
            if (concentration != null)
            {
                customBar.SetMaxValue(concentration.maxConcentration);
                customBar.SetValue(concentration.currentConcentration);
            }
            else
            {
                customBar.SetMaxValue(Data.maxFatigue);
                customBar.SetValue(currentFatigue);
            }
        }
    }

    /// <summary>
    /// Assure la cohérence des liens entre une timeline et le PlayableDirector
    /// de l'unité avant de déclencher sa lecture. Les pistes portant la mention
    /// "Caster" sont reliées en priorité à l'Animator du lanceur afin de
    /// préserver l'intention chorégraphique des MusicalMoves.
    /// </summary>
    /// <param name="timeline">Timeline à jouer via le PlayableDirector local.</param>
    /// <param name="casterBinding">
    /// Objet de référence pour les pistes d'animation. S'il est omis, l'Animator
    /// principal de l'unité est utilisé automatiquement.
    /// </param>
    public void PlayBattleTimeline(TimelineAsset timeline, GameObject casterBinding = null)
    {
        if (timeline == null)
            return;

        // Sécurise l'accès au PlayableDirector même si le composant a été ajouté
        // dynamiquement après l'Awake (cas de duplication à l'éditeur par exemple).
        if (battleDirector == null)
            battleDirector = GetComponent<PlayableDirector>();

        if (battleDirector == null)
        {
            Debug.LogError($"[CharacterUnit] Aucun PlayableDirector disponible sur {name}. La timeline '{timeline.name}' ne peut pas être jouée.");
            return;
        }

        // Évite que plusieurs timelines se chevauchent sur la même unité.
        battleDirector.Stop();

        battleDirector.playableAsset = timeline;

        // Détermine les cibles de binding prioritaires.
        GameObject bindingRoot = casterBinding ?? GetCasterBindingTarget();
        // Sélectionne l'Animator le plus pertinent pour les pistes "Caster".
        Animator bindingAnimator = ResolveAnimatorForBinding(bindingRoot);

        if (bindingAnimator == null && animator != null)
            bindingAnimator = animator;

        foreach (var output in timeline.outputs)
        {
            BindTimelineOutput(output, bindingRoot, bindingAnimator);
        }

        battleDirector.time = 0d;
        battleDirector.Play();
    }

    /// <summary>
    /// Prépare la timeline d'introduction de combat en reliant explicitement
    /// les pistes "Root" et "Model" du <see cref="PlayableDirector"/> local.
    /// Les autres pistes potentiellement présentes sont nettoyées afin d'éviter
    /// qu'un binding issu d'une précédente timeline ne perturbe la mise en scène.
    /// </summary>
    /// <param name="introTimeline">Timeline d'introduction à jouer.</param>
    /// <returns>
    /// Le <see cref="PlayableDirector"/> prêt à être lancé, ou <c>null</c> si la
    /// configuration n'a pas pu être réalisée.
    /// </returns>
    public PlayableDirector PrepareIntroTimeline(TimelineAsset introTimeline)
    {
        if (introTimeline == null)
            return null;

        // Sécurise la récupération du PlayableDirector, indispensable pour jouer la timeline.
        if (battleDirector == null)
            battleDirector = GetComponent<PlayableDirector>();

        // Si pour une raison quelconque le PlayableDirector est absent, on le recrée afin de garantir la lecture.
        if (battleDirector == null)
            battleDirector = gameObject.AddComponent<PlayableDirector>();

        if (battleDirector == null)
        {
            Debug.LogError($"[CharacterUnit] Aucun PlayableDirector disponible sur {name}. La timeline d'introduction '{introTimeline.name}' ne peut pas être préparée.");
            return null;
        }

        // --- Préparation des Animator pour la timeline d'introduction --------------------------------------------
        // L'Animator du "Root" doit impérativement provenir du GameObject portant le CharacterUnit.
        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator == null && animator != null && animator.gameObject == gameObject)
        {
            // Si le champ "animator" référence déjà celui du root (cas de certaines unités), on le réutilise.
            rootAnimator = animator;
        }

        // L'Animator du "Model" doit être recherché exclusivement dans les enfants pour éviter toute confusion.
        Animator modelAnimator = null;

        if (animator != null && animator.gameObject != gameObject)
        {
            // On dispose déjà d'un Animator enfant enregistré via Initialize : on l'exploite directement.
            modelAnimator = animator;
        }
        else
        {
            // Recherche explicite parmi les enfants (y compris inactifs) tout en excluant l'objet racine.
            modelAnimator = GetComponentsInChildren<Animator>(includeInactive: true)
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject != gameObject);

            if (modelAnimator != null)
            {
                // On mémorise l'Animator du modèle pour les usages ultérieurs (attaques, VFX, etc.).
                animator = modelAnimator;
            }
        }

        // Coupe toute éventuelle lecture en cours pour éviter qu'une timeline précédente ne continue.
        battleDirector.Stop();

        battleDirector.playableAsset = introTimeline;

        // Parcourt toutes les pistes afin de ne conserver que les bindings nécessaires.
        foreach (var track in introTimeline.GetOutputTracks())
        {
            // On se concentre sur les AnimationTrack car seules les pistes "Root" et "Model" doivent être reliées.
            if (track is AnimationTrack)
            {
                string trackName = track.name;

                // La piste "Root" manipule le GameObject principal portant le CharacterUnit.
                if (string.Equals(trackName, "Root", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (rootAnimator != null)
                    {
                        // Binding explicite sur l'Animator du GameObject principal du CharacterUnit.
                        battleDirector.SetGenericBinding(track, rootAnimator);
                    }
                    else
                    {
                        // Aucun Animator sur le root : on nettoie pour éviter une référence fantôme.
                        battleDirector.ClearGenericBinding(track);
                    }

                    continue; // Binding traité, on passe à la piste suivante.
                }

                // La piste "Model" anime exclusivement un Animator situé dans les enfants (mesh, armature, ...).
                if (string.Equals(trackName, "Model", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (modelAnimator != null)
                    {
                        // On relie la piste à l'Animator enfant dédié au modèle visuel.
                        battleDirector.SetGenericBinding(track, modelAnimator);
                    }
                    else
                    {
                        // Aucun Animator enfant disponible : on nettoie pour éviter d'hériter d'un ancien binding.
                        battleDirector.ClearGenericBinding(track);
                    }

                    continue; // Binding géré, inutile de poursuivre les vérifications sur cette piste.
                }
            }

            // Pour toutes les autres pistes, on supprime tout binding résiduel pour éviter des références incohérentes.
            battleDirector.ClearGenericBinding(track);
        }

        // S'assure que la timeline repart du début lorsque le BattleManager la lancera.
        battleDirector.time = 0d;

        return battleDirector;
    }

    /// <summary>
    /// Arrête proprement la timeline de combat en cours sur cette unité. Cette
    /// méthode est utilisée notamment lors de la fermeture des menus d'objets.
    /// </summary>
    public void StopBattleTimeline()
    {
        if (battleDirector != null && battleDirector.state == PlayState.Playing)
            battleDirector.Stop();
    }

    /// <summary>
    /// Indique si la timeline de combat locale est actuellement en cours
    /// d'exécution. Utile pour synchroniser les différentes phases d'un move.
    /// </summary>
    public bool IsBattleTimelinePlaying => battleDirector != null && battleDirector.state == PlayState.Playing;

    /// <summary>
    /// Réalise le binding d'une piste individuelle d'une timeline vers les
    /// cibles adéquates (Animator, GameObject ou composant spécialisé).
    /// </summary>
    private void BindTimelineOutput(PlayableBinding output, GameObject bindingRoot, Animator bindingAnimator)
    {
        // Les pistes caméra restent gérées par le BattleCameraManager pour
        // conserver les transitions cinématographiques écrites dans la Bible du jeu.
        string streamName = output.streamName ?? string.Empty;
        if (streamName.IndexOf("camera", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        System.Type targetType = output.outputTargetType;

        // Les pistes de signaux doivent pointer vers les récepteurs attachés au director.
        if (targetType != null && typeof(Component).IsAssignableFrom(targetType) && targetType.Name.Contains("SignalReceiver"))
        {
            Component receiver = battleDirector.GetComponent(targetType);
            if (receiver != null)
                battleDirector.SetGenericBinding(output.sourceObject, receiver);
            else
                Debug.LogWarning($"[CharacterUnit] SignalReceiver {targetType.Name} introuvable sur {battleDirector.gameObject.name} pour la timeline '{battleDirector.playableAsset.name}'.");
            return;
        }

        // Priorité absolue : toute piste mentionnant explicitement le caster suit l'Animator principal.
        if (streamName.IndexOf("caster", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            streamName.IndexOf("pnj", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (bindingAnimator != null)
                battleDirector.SetGenericBinding(output.sourceObject, bindingAnimator);
            else if (bindingRoot != null)
                battleDirector.SetGenericBinding(output.sourceObject, bindingRoot);
            else
                battleDirector.SetGenericBinding(output.sourceObject, gameObject);
            return;
        }

        // Les pistes "Model" visent généralement l'objet contenant l'Animator.
        if (streamName.IndexOf("model", System.StringComparison.OrdinalIgnoreCase) >= 0 && bindingAnimator != null)
        {
            battleDirector.SetGenericBinding(output.sourceObject, bindingAnimator.gameObject);
            return;
        }

        // Fallback : on s'appuie sur le type de sortie pour trouver un composant compatible.
        if (targetType != null)
        {
            if (typeof(Animator).IsAssignableFrom(targetType))
            {
                if (bindingAnimator != null)
                {
                    battleDirector.SetGenericBinding(output.sourceObject, bindingAnimator);
                    return;
                }
            }
            else if (typeof(GameObject).IsAssignableFrom(targetType))
            {
                battleDirector.SetGenericBinding(output.sourceObject, bindingRoot ?? gameObject);
                return;
            }
            else if (typeof(Component).IsAssignableFrom(targetType))
            {
                Component component = null;

                if (bindingRoot != null)
                {
                    component = bindingRoot.GetComponent(targetType);
                    if (component == null)
                        component = bindingRoot.GetComponentInChildren(targetType);
                }

                if (component == null && bindingAnimator != null)
                    component = bindingAnimator.GetComponent(targetType);

                if (component == null)
                    component = battleDirector.GetComponent(targetType);

                if (component != null)
                {
                    battleDirector.SetGenericBinding(output.sourceObject, component);
                    return;
                }
            }
        }

        // Dernier recours : on relie la piste à l'objet principal de l'unité.
        battleDirector.SetGenericBinding(output.sourceObject, bindingRoot ?? gameObject);
    }

    /// <summary>
    /// Vérifie régulièrement l'état de mort du personnage.
    /// </summary>
    void Update()
    {
        HandleDeath();
        HandleCustomBarValue();
        ApplyGravity();
    }

    void HandleCustomBarValue()
    {
        if (customBar != null)
        {
            var concentration = GetComponent<ConcentrationSystem>();
            if (concentration != null)
                customBar.SetValue(concentration.currentConcentration);
            else
                customBar.SetValue(currentFatigue);
        }
    }

    /// <summary>
    /// Applique une gravité basique aux unités terrestres afin qu'elles tombent
    /// naturellement lorsqu'elles ne sont plus soutenues.
    /// </summary>
    private void ApplyGravity()
    {
        // Les unités aériennes ne sont pas soumises à la gravité.
        if (IsAirUnit || controller == null)
            return;

        if (controller.isGrounded)
        {
            // Lorsque l'unité touche le sol, on réinitialise la vitesse de chute
            // afin d'éviter une accumulation négative.
            if (fallVelocity.y < 0f)
                fallVelocity.y = -2f; // Petite force vers le bas pour coller au sol
        }
        else
        {
            // Accumule la gravité au fil du temps lorsque l'unité est en l'air.
            fallVelocity.y += gravity * Time.deltaTime;
            controller.Move(fallVelocity * Time.deltaTime);
        }
    }

    /// <summary>
    /// Implémentation minimale de <see cref="IDamageable"/> pour assurer la
    /// compatibilité avec l'interface. On redirige vers la version complète
    /// prenant en compte l'attaquant.
    /// </summary>
    /// <param name="amount">Quantité de dégâts subis.</param>
    public void TakeDamage(float amount)
    {
        // Redirige vers la version complète en autorisant par défaut
        // les éventuelles redirections de dégâts (LoyaltyMark, etc.).
        TakeDamage(amount, null);
    }

    /// <summary>
    /// Inflige des dégâts et met à jour l'UI correspondante.
    /// </summary>
    /// <param name="amount">Quantité de dégâts subis.</param>
    /// <param name="attacker">Transform de l'attaquant pour déterminer la direction.</param>
    public void TakeDamage(float amount, Transform attacker = null, bool allowRedirect = true)
    {
        // Si autorisé, on vérifie la présence d'une marque de loyauté qui
        // pourrait rediriger les dégâts vers un protecteur.
        if (allowRedirect)
        {
            var mark = GetComponent<LoyaltyMark>();
            if (mark != null && mark.RedirectDamage(amount))
                return;
        }

        currentHP = Mathf.Max(currentHP - amount, 0);
        if (hpBar != null) hpBar.SetValue(currentHP);

        // Calcul de la gravité du coup pour déterminer le son et le message
        float maxHP = Data.baseHP + currentVitality;
        bool devastating = amount > maxHP * 0.2f;

        // Affiche le nombre de dégâts au-dessus de cette unité
        DamagePopupManager.Instance?.ShowDamage(transform, Mathf.RoundToInt(amount));
        PlayDamageFeedback(devastating);

        // 🎥 Déclenche un tremblement de caméra court lorsque l'unité touchée appartient à l'escouade.
        if (Data != null && Data.characterType == CharacterType.SquadUnit)
            BattleCameraManager.Instance?.TriggerDamageShake(this, devastating, attacker);

        // Message indiquant la gravité des dégâts subis
        if (ActionUIDisplayManager.Instance != null)
        {
            ActionUIDisplayManager.Instance.DisplayDamage(Data.characterName, devastating);
        }

        // Si les PV tombent à zéro ou moins, on déclenche immédiatement la mort
        // pour éviter que l'animation de blessure n'écrase l'animation de mort
        if (currentHP <= 0 && !deathTriggered)
        {
            PlayDeath();
            return;
        }

        // Lance l'animation de blessure adaptée à la direction de l'attaquant
        PlayHurtAnimation(attacker);
        GetComponent<SleepStatus>()?.OnDamageTaken();
        GetComponent<ConcentrationSystem>()?.OnDamageTaken(amount);
        if (Data != null && Data.gameplayType == GameplayType.Rage)
        {
            GetComponent<RageSystem>()?.AddRage(amount);
        }
    }

    /// <summary>
    /// Appelé quand la cible pare une attaque.
    /// </summary>
    public void TakeParry()
    {
        // Affiche un message de parade via l'UI
        ActionUIDisplayManager.Instance?.DisplayParry(Data.characterName);
    }

    /// <summary>
    /// Appelé quand la cible esquive une attaque.
    /// </summary>
    public void TakeDodge()
    {
        ActionUIDisplayManager.Instance?.DisplayDodge(Data.characterName);
    }

    /// <summary>
    /// Déclenche la mort lorsque les PV atteignent zéro.
    /// </summary>
    void HandleDeath()
    {
        if (currentHP <= 0 && !deathTriggered)
        {
            PlayDeath();
        }
    }

    /// <summary>
    /// Joue l'animation et les effets de mort, puis retire l'unité du combat.
    /// </summary>
    void PlayDeath()
    {
        deathTriggered = true;
        if (Data.deathEffect != null)
        {
            Instantiate(Data.deathEffect, transform.position, Quaternion.identity);
        }
        Animator animator = GetCasterAnimator();
        Debug.Log(this + " handleDeath called, playing death animation.");
        if (animator != null)
        {
            animator.Play("Death");
        }
        NewBattleManager.Instance.RemoveFromTimeline(this);
        NewBattleManager.Instance.activeCharacterUnits.Remove(this); // facultatif

        if (Data.characterType == CharacterType.EnemyUnit)
        {
            GameManager.Instance?.IncrementEnemiesDefeated();
            NewBattleManager.Instance?.OnEnemyDefeated(this);
        }

        if (Data.isPlayerControlled)
        {
            PlayAllyWeep();

            if (Data.characterName == "Lucian")
            {
                BattleCameraShatter shatter = FindFirstObjectByType<BattleCameraShatter>();
                if (shatter != null)
                    shatter.Break();
            }
        }

        OnDeath?.Invoke(this);
    }

    void PlayAllyWeep()
    {
        var allies = NewBattleManager.Instance.activeCharacterUnits
            .Where(u => u.Data.isPlayerControlled && u != this && u.currentHP > 0)
            .ToList();
        if (allies.Count == 0)
            return;

        CharacterUnit randomAlly = allies[UnityEngine.Random.Range(0, allies.Count)];
        AudioClipSO clip = GetWeepClip(randomAlly.Data, Data.characterName);
        if (clip != null)
            AudioManager.Instance?.PlayVoice(clip);
    }

    AudioClipSO GetWeepClip(CharacterData allyData, string deadName)
    {
        return deadName switch
        {
            "Lucian" => allyData.weepForLucianDeath,
            "Thalia" => allyData.weepForThaliaDeath,
            "Kael" => allyData.weepForKaelDeath,
            "Link" => allyData.weepForLinkDeath,
            "Luna" => allyData.weepForLunaDeath,
            _ => null,
        };
    }

    /// <summary>
    /// Soigne l'unité et met à jour la barre de vie.
    /// </summary>
    public void Heal(float amount)
    {
        currentHP = Mathf.Min(currentHP + amount, Data.baseHP + currentVitality);
        if (hpBar != null) hpBar.SetValue(currentHP);
    }

    public void ApplyBuff(float value)
    {

    }
    public void RemoveBuff(float value)
    {

    }

    public void ApplyDebuff(float value)
    {

    }
    public void RemoveDebuff(float value)
    {

    }

    /// <summary>
    /// Joue le son d'impact. Utilise une voix différente si le coup est
    /// considéré comme dévastateur.
    /// </summary>
    /// <param name="isDevastating">True si le coup est dévastateur.</param>
    public void PlayHitSound(bool isDevastating = false)
    {
        if (audioSource == null)
            return;

        // Sélection du clip approprié
        AudioClipSO clip = (isDevastating && Data.criticalHitSound != null)
            ? Data.criticalHitSound
            : Data.hitSound;

        if (clip != null && clip.Clip != null)
            audioSource.PlayOneShot(clip.Clip, clip.Volume);
    }

    /// <summary>
    /// Joue le son spécifique lorsqu'une interception touche cette unité.
    /// </summary>
    public void PlayInterceptedSound()
    {
        if (Data.interceptedSound != null && Data.interceptedSound.Clip != null && audioSource != null)
            audioSource.PlayOneShot(Data.interceptedSound.Clip, Data.interceptedSound.Volume);
    }

    /// <summary>
    /// Joue le son spécifique lorsqu'une interception réussit et que
    /// cette unité est celle qui intercepte.
    /// </summary>
    public void PlayInterceptionSound()
    {
        if (Data.interceptionSound != null && Data.interceptionSound.Clip != null && audioSource != null)
            audioSource.PlayOneShot(Data.interceptionSound.Clip, Data.interceptionSound.Volume);
    }

    public void PlayMoveStartSound()
    {
        if (Data.moveStartClip != null)
        {
            AudioManager.Instance?.PlayTempSfx(Data.moveStartClip);
        }
    }

    public void PlayMoveEndSound()
    {
        if (Data.moveEndClip != null)
        {
            AudioManager.Instance?.PlayTempSfx(Data.moveEndClip);
        }
    }

    public IEnumerator PlayDamageFlash()
    {
        if (spriteRenderer == null) yield break;
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = originalColor;
    }

    public IEnumerator PlayShake(float duration = 0.15f, float magnitude = 0.1f)
    {
        Vector3 originalPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = UnityEngine.Random.Range(-magnitude, magnitude);
            float y = UnityEngine.Random.Range(-magnitude, magnitude);
            transform.localPosition = originalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
    }

    public IEnumerator PlayKnockback(Vector3 direction, float distance = 0.5f, float duration = 0.1f)
    {
        Vector3 start = transform.position;
        Vector3 end = start + direction.normalized * distance;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, end, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = start;
    }

    void PlayAnimationClip(AnimationClip clip)
    {
        // Ne rien jouer si l'unité est morte
        if (IsDead)
            return;

        if (animator != null && clip != null)
        {
            // CrossFade pour éviter d'interrompre brutalement l'animation en cours
            animator.CrossFade(clip.name, 0.05f);
        }
    }

    public void PlayHurtAnimation(Transform attacker = null)
    {
        // Si l'unité est morte ou qu'aucun Animator n'est disponible, on ne fait rien
        if (IsDead || animator == null)
            return;

        // Lorsque l'attaquant est connu, on calcule le côté touché
        if (attacker != null)
        {
            // Direction normalisée allant de cette unité vers l'attaquant
            Vector3 direction = (attacker.position - transform.position).normalized;

            // Angle signé autour de l'axe Y pour savoir si l'attaque vient de la
            // gauche (< 0) ou de la droite (> 0). La valeur absolue permet ensuite de
            // distinguer l'avant ou l'arrière.
            float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);

            // Nom de l'état à jouer. Par défaut on considère une attaque de face.
            string state = "Hit_F";

            // Avant : angle proche de 0°
            if (Mathf.Abs(angle) <= 45f)
            {
                state = "Hit_F";
            }
            // Arrière : angle > 135° ou < -135°
            else if (Mathf.Abs(angle) > 135f)
            {
                state = "Hit_B";
            }
            // Droite : angle positif (attaque venant de la droite)
            else if (angle > 0f)
            {
                state = "Hit_R";
            }
            // Gauche : angle négatif
            else
            {
                state = "Hit_L";
            }

            // Lance l'animation correspondante dans l'Animator
            animator.CrossFade(state, 0.05f);
            return;
        }

        // Si aucun attaquant n'est fourni, on se rabat sur l'animation générique
        PlayAnimationClip(Data?.hitAnimation ?? hurtAnimation);
    }
    public void PlayInterceptedAnimation() => PlayAnimationClip(interceptedAnimation);
    public void PlayInterceptionAnimation() => PlayAnimationClip(interceptionAnimation);
    public void PlayPrepareToUndergoAnimation()
    {
        // Pas d'animation si l'unité est morte
        if (IsDead)
            return;
        // On force l'actualisation du cache pour récupérer l'Animator enfant même si
        // l'objet racine avait été retenu auparavant (par exemple lorsque le modèle
        // n'était pas encore actif). L'objectif est de jouer l'animation sur le rig
        // enfant qui porte réellement les poses défensives.
        Animator anim = GetCasterAnimator(forceRefresh: true);

        // Si le cache renvoie toujours l'Animator du GameObject racine, on tente une
        // récupération directe de l'Animator enfant. Cela évite de lancer l'animation
        // sur le mauvais Animator, ce qui bloquerait la pose de garde.
        if (anim != null && anim.gameObject == gameObject)
        {
            Animator childAnimator = FindChildAnimatorForBindings();
            if (childAnimator != null && childAnimator.gameObject != gameObject)
            {
                animator = childAnimator; // Maintien du cache synchronisé.
                anim = childAnimator;
            }
            else
            {
                // Sans Animator enfant il est inutile d'aller plus loin : on préserve
                // l'état actuel plutôt que de déclencher un CrossFade sur un mauvais
                // contrôleur.
                return;
            }
        }

        // Même si le cache renvoie null (absence d'Animator valide) on évite toute
        // NullReferenceException en contrôlant Data et le clip demandé.
        if (anim != null && Data != null && Data.prepareToUndergoAnimation != null)
        {
            // Utilise CrossFade pour garantir la bonne transition
            anim.CrossFade(Data.prepareToUndergoAnimation.name, 0.05f);
        }
    }

    void PlayDamageFeedback(bool isDevastating)
    {
        // Joue le son correspondant à la gravité du coup
        PlayHitSound(isDevastating);

        if (Data.hitEffect != null)
            Instantiate(Data.hitEffect, transform.position, Quaternion.identity);

        StartCoroutine(PlayDamageFlash());
        StartCoroutine(PlayShake());
        StartCoroutine(PlayKnockback(Vector3.zero)); // Tu peux adapter la direction
    }

    public MusicalMoveSO GetRandomMusicalAttack()
    {
        var availableAttacks = Data.musicalAttacks
            .Where(m => !m.onlyAwake || IsAwake)
            .Where(m => !m.enterAwake || !IsAwake)
            .Where(m => !m.enterAwake || GetHarmonicCount(Data.harmonicType) >= Data.resonancePoint)
            .Where(m => CanUseMove(m))
            .ToArray();

        if (availableAttacks == null || availableAttacks.Length == 0)
        {
            Debug.LogWarning($"[CharacterUnit] {Data.characterName} n'a aucune attaque musicale disponible pour l'état actuel !");
            return null;
        }

        int index = UnityEngine.Random.Range(0, availableAttacks.Length);
        return availableAttacks[index];
    }

    public CharacterUnit SelectTargetFromSquad()
    {
        var squad = NewBattleManager.Instance.activeCharacterUnits
            .Where(u => u.Data.isPlayerControlled && u.Data.currentHP > 0)
            .ToList();

        if (squad == null || squad.Count == 0)
        {
            Debug.LogWarning("[EnemyAI] Aucun joueur valide à cibler.");
            return null;
        }

        // Priorité : cible ayant infligé le plus de dégâts au cours du combat
        var topDamageDealer = NewBattleManager.Instance.GetTopDamageDealer();
        if (topDamageDealer != null)
            return topDamageDealer;

        // Sinon, cible avec le moins de PV
        var lowestHPUnit = squad.OrderBy(u => u.Data.currentHP).FirstOrDefault();
        if (lowestHPUnit != null)
            return lowestHPUnit;

        // Fallback aléatoire
        return squad[UnityEngine.Random.Range(0, squad.Count)];
    }

    public void AddHarmonic(HarmonicType type, int amount = 1)
    {
        if (!harmonicReserve.ContainsKey(type))
            harmonicReserve[type] = 0;
        harmonicReserve[type] += amount;
        CheckDissonance();
    }

    public bool ConsumeHarmonic(HarmonicType type, int amount = 1)
    {
        if (!harmonicReserve.ContainsKey(type) || harmonicReserve[type] < amount)
            return false;
        harmonicReserve[type] -= amount;
        CheckDissonance();
        return true;
    }

    public int GetHarmonicCount(HarmonicType type)
    {
        return harmonicReserve.ContainsKey(type) ? harmonicReserve[type] : 0;
    }

    public void ClearAllHarmonics()
    {
        var keys = harmonicReserve.Keys.ToList();
        foreach (var key in keys)
            harmonicReserve[key] = 0;
        CheckDissonance();
    }

    public void ReduceCooldowns()
    {
        var keys = moveCooldowns.Keys.ToList();
        foreach (var key in keys)
            moveCooldowns[key] = Mathf.Max(0, moveCooldowns[key] - 1);
    }

    public bool IsMoveOnCooldown(MusicalMoveSO move)
    {
        return moveCooldowns.ContainsKey(move) && moveCooldowns[move] > 0;
    }

    public void SetMoveCooldown(MusicalMoveSO move)
    {
        if (move.cooldown > 0)
            moveCooldowns[move] = move.cooldown;
    }

    // ---------------------------------------------------------------------
    // Gestion des limitations d'utilisation des attaques musicales
    // ---------------------------------------------------------------------

    /// <summary>
    /// Vérifie si ce move peut être utilisé en fonction des limites par tour
    /// et par combat.
    /// </summary>
    public bool CanUseMove(MusicalMoveSO move)
    {
        if (move == null)
            return false;

        if (move.maxUsesPerTurn > 0)
        {
            moveUsesThisTurn.TryGetValue(move, out int usedTurn);
            if (usedTurn >= move.maxUsesPerTurn)
                return false;
        }

        if (move.maxUsesPerBattle > 0)
        {
            moveUsesThisBattle.TryGetValue(move, out int usedBattle);
            if (usedBattle >= move.maxUsesPerBattle)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Enregistre l'utilisation d'un move pour tenir à jour les compteurs.
    /// </summary>
    public void RegisterMoveUse(MusicalMoveSO move)
    {
        if (move.maxUsesPerTurn > 0)
        {
            moveUsesThisTurn.TryGetValue(move, out int usedTurn);
            moveUsesThisTurn[move] = usedTurn + 1;
        }

        if (move.maxUsesPerBattle > 0)
        {
            moveUsesThisBattle.TryGetValue(move, out int usedBattle);
            moveUsesThisBattle[move] = usedBattle + 1;
        }
    }

    /// <summary>
    /// Réinitialise les compteurs par tour. À appeler au début de chaque tour.
    /// </summary>
    public void ResetTurnMoveUsage()
    {
        moveUsesThisTurn.Clear();
    }

    /// <summary>
    /// Réinitialise les compteurs globaux du combat. À appeler au début du combat.
    /// </summary>
    public void ResetBattleMoveUsage()
    {
        moveUsesThisTurn.Clear();
        moveUsesThisBattle.Clear();
    }

    /// <summary>
    /// Vérifie si l'unité doit sortir de l'état Awake en fonction de ses harmoniques.
    /// </summary>
    private void CheckDissonance()
    {
        if (IsAwake && GetHarmonicCount(Data.harmonicType) < Data.dissonancePoint)
            ExitAwakeState();
    }

    /// <summary>
    /// Active l'état Awake et applique les bonus correspondants.
    /// </summary>
    public void EnterAwakeState()
    {
        awakeState?.EnterAwake();
    }

    /// <summary>
    /// Désactive l'état Awake et retire les bonus.
    /// </summary>
    public void ExitAwakeState()
    {
        awakeState?.ExitAwake();
    }

    public float GetAttackMultiplier()
    {
        if (TryGetComponent<SleepStatus>(out var sleep) && sleep.IsAsleep && Data != null && Data.gameplayType == GameplayType.Fatigue)
            return 2f;
        if (TryGetComponent<FatigueSystem>(out var fatigue) && fatigue.IsAsleep && Data != null && Data.gameplayType == GameplayType.Fatigue)
            return 2f;
        return 1f;
    }

    public void PlayIdleAnimation()
    {
        if (currentHP <= 0)
            return;

        if (TryGetComponent<SleepStatus>(out var sleep) && sleep.IsAsleep)
            return;

        if (TryGetComponent<FatigueSystem>(out var fatigue) && fatigue.IsAsleep)
            return;

        if (animator != null)
        {
            const int baseLayerIndex = 0;

            // Afin d'éviter tout changement brutal d'animation, on favorise un CrossFade lorsque l'état existe.
            if (animator.HasState(baseLayerIndex, AnimatorStateIdleBattle))
            {
                animator.CrossFade(AnimatorStateIdleBattle, IdleCrossFadeDurationSeconds, baseLayerIndex, 0f);
            }
            else
            {
                // En dernier recours, on conserve Play pour les rigs spéciaux n'ayant pas l'état Idle_Battle déclaré.
                animator.Play("Idle_Battle");
            }
        }
    }

    #endregion
}
