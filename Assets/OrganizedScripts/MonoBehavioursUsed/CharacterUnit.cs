using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CharacterUnit : MonoBehaviour, IDamageable, IHealable, IBuffable, IDebuffable
{
    public CharacterData Data;

    [Header("UI Components")]
    public HPBar hpBar;
    public CustomBar customBar;

    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Animator animator;

    public CharacterType characterType => Data.characterType;

    public float currentHP { get => Data.currentHP; set => Data.currentHP = value; }
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
    public float currentFatigue { get => Data.currentFatigue; set => Data.currentFatigue = value; }

    // Gestion de l'initiative
    public float currentInitiative { get => Data.currentInitiative; set => Data.currentInitiative = value; }
    public float currentATB = 0f;
    public float ATBMax = 100f;
    public bool IsReady => currentATB >= ATBMax && currentHP > 0;

    private bool deathTriggered;
    public bool isReadyToParry;

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

        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();

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
            customBar.SetMaxValue(Data.maxFatigue);
            customBar.SetValue(currentFatigue);
        }
    }

    /// <summary>
    /// Vérifie régulièrement l'état de mort du personnage.
    /// </summary>
    void Update()
    {
        HandleDeath();
        HandleCustomBarValue();
    }

    void HandleCustomBarValue()
    {
        if (customBar != null)
        {
            customBar.SetValue(currentFatigue);
        }
    }

    /// <summary>
    /// Inflige des dégâts et met à jour l'UI correspondante.
    /// </summary>
    public void TakeDamage(float amount)
    {
        currentHP = Mathf.Max(currentHP - amount, 0);
        if (hpBar != null) hpBar.SetValue(currentHP);
        DamagePopupManager.Instance?.ShowDamage(transform.position, Mathf.RoundToInt(amount));
        PlayDamageFeedback();
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
        // This method should be called when the currentTargetCharacter successfully parries an attack
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

    public void PlayHitSound()
    {
        if (Data.hitSound != null && audioSource != null)
            audioSource.PlayOneShot(Data.hitSound);
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

    void PlayDamageFeedback()
    {
        PlayHitSound();

        if (Data.hitEffect != null)
            Instantiate(Data.hitEffect, transform.position, Quaternion.identity);

        StartCoroutine(PlayDamageFlash());
        StartCoroutine(PlayShake());
        StartCoroutine(PlayKnockback(Vector3.zero)); // Tu peux adapter la direction
    }

    public MusicalMoveSO GetRandomMusicalAttack()
    {
        var availableAttacks = Data.musicalAttacks;

        if (availableAttacks == null || availableAttacks.Length == 0)
        {
            Debug.LogWarning($"[CharacterUnit] {Data.characterName} n'a aucune attaque musicale disponible !");
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

    #endregion
}
