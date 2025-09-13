using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Représente une unité de combat. L'ajout d'un <see cref="CharacterController"/>
/// permet de déléguer la gestion de la gravité à Unity pour plus de cohérence.
/// </summary>
[RequireComponent(typeof(CharacterController))]
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
    private AwakeState awakeState;

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
    /// Même en plein vol, cela permet aux attaques terrestres de la toucher,
    /// comme le veut la légende contée dans l'Histoire de Symphonie où les
    /// résonances du sol atteignent les cieux.
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

    private void Awake()
    {
        // S'assure qu'un CharacterController est présent pour gérer la physique.
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();
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
        animator = GetComponentInChildren<Animator>();
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
        Animator animator = GetComponentInChildren<Animator>();
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

        CharacterUnit randomAlly = allies[Random.Range(0, allies.Count)];
        AudioClip clip = GetWeepClip(randomAlly.Data, Data.characterName);
        if (clip != null)
            AudioManager.Instance?.PlayVoice(clip);
    }

    AudioClip GetWeepClip(CharacterData allyData, string deadName)
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
        AudioClip clip = (isDevastating && Data.criticalHitSound != null)
            ? Data.criticalHitSound
            : Data.hitSound;

        if (clip != null)
            audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Joue le son spécifique lorsqu'une interception touche cette unité.
    /// </summary>
    public void PlayInterceptedSound()
    {
        if (Data.interceptedSound != null && audioSource != null)
            audioSource.PlayOneShot(Data.interceptedSound);
    }

    /// <summary>
    /// Joue le son spécifique lorsqu'une interception réussit et que
    /// cette unité est celle qui intercepte.
    /// </summary>
    public void PlayInterceptionSound()
    {
        if (Data.interceptionSound != null && audioSource != null)
            audioSource.PlayOneShot(Data.interceptionSound);
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
            float x = Random.Range(-magnitude, magnitude);
            float y = Random.Range(-magnitude, magnitude);
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

        if (animator != null && Data.prepareToUndergoAnimation != null)
        {
            // Utilise CrossFade pour garantir la bonne transition
            animator.CrossFade(Data.prepareToUndergoAnimation.name, 0.05f);
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

        int index = Random.Range(0, availableAttacks.Length);
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
        return squad[Random.Range(0, squad.Count)];
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
            animator.Play("Idle_Battle");
        }
    }

    #endregion
}
