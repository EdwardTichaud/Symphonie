using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CharacterUnit : MonoBehaviour, IDamageable, IHealable, IBuffable, IDebuffable
{
    public CharacterData Data;

    [Header("UI Components")]
    public HPBar hpBar;
    public MPBar mpBar;
    public CustomBar customBar;

    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Animator animator;

    public CharacterType characterType;

    public float currentHP;
    public float currentMP;
    public float currentRage;

    public float currentStrength;
    public float currentDefense;
    public float currentReflex;
    public float currentMobility;
    public float currentPower;
    public float currentStability;
    public float currentVitality;
    public float currentSagacity;

    public float currentMusicalGauge;
    public float currentFatigue;

    // Gestion de l'initiative
    public float currentInitiative;
    public float currentATB = 0f;
    public float ATBMax = 100f;
    public bool IsReady => currentATB >= ATBMax && currentHP > 0;

    private bool deathTriggered;
    public bool isReadyToParry;

    #region Cycle de Vie
    /// <summary>
    /// Initialise toutes les statistiques du personnage selon sa fiche.
    /// </summary>
    public void Initialize(CharacterData characterData)
    {
        Data = characterData;
        Data.owner = this;
        characterType = characterData.characterType;

        // Initialisation des stats
        currentPower = Data.basePower;
        currentStability = Data.baseStability;
        currentVitality = Data.baseVitality;
        currentSagacity = Data.baseSagacity;
        currentHP = Data.baseHP + currentVitality;
        Data.currentHP = currentHP;
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
        Data.currentHP = currentHP;
        if (hpBar != null) hpBar.SetValue(currentHP);
        PlayDamageFeedback();
        GetComponent<RageSystem>()?.AddRage(amount);
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
    }

    /// <summary>
    /// Soigne l'unité et met à jour la barre de vie.
    /// </summary>
    public void Heal(float amount)
    {
        currentHP = Mathf.Min(currentHP + amount, Data.baseHP + currentVitality);
        Data.currentHP = currentHP;
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

        // Priorité 1 : cible avec le moins de PV
        var lowestHPUnit = squad.OrderBy(u => u.Data.currentHP).FirstOrDefault();
        if (lowestHPUnit != null)
            return lowestHPUnit;

        // Priorité 2 : cible ayant infligé le plus de dégâts à cet ennemi (si tu as un système de suivi de dégâts)
        // Exemple fictif : si tu avais un dictionnaire `Dictionary<CharacterUnit, float> damageReceivedFrom`
        // Tu peux remplacer par ta propre logique de suivi.
        /*
        if (damageReceivedFrom.Count > 0)
        {
            var topAggro = damageReceivedFrom.OrderByDescending(kvp => kvp.Value).First().Key;
            return topAggro;
        }
        */

        return squad[Random.Range(0, squad.Count)]; // Fallback random
    }

    #endregion
}
