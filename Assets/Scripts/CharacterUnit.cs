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
    public RageBar rageBar;

    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Animator animator;

    public CharacterType characterType;

    public int currentHP;
    public int currentMP;
    public int currentRage;

    public int currentStrength;
    public int currentDefense;
    public int currentReflex;
    public float currentMobility;

    public int currentMusicalGauge;

    // Gestion de l'initiative
    public int currentInitiative;
    public float currentATB = 0f;
    public float ATBMax = 100f;
    public bool IsReady => currentATB >= ATBMax && currentHP > 0;

    private bool deathTriggered;
    public bool isReadyToParry;

    public void Initialize(CharacterData characterData)
    {
        Data = characterData;
        Data.owner = this;
        characterType = characterData.characterType;

        // Initialisation des stats
        currentHP = Data.baseHP;
        currentMP = Data.baseMP;
        currentRage = Data.baseRage;
        currentInitiative = Data.baseInitiative;
        currentStrength = Data.baseStrength;
        currentDefense = Data.baseDefense;
        currentReflex = Data.baseReflex;
        currentMobility = Data.baseMobility;
        currentMusicalGauge = Data.baseMusicalGauge;

        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();

        // Setup graphique
        if (spriteRenderer != null && Data.portrait != null)
            spriteRenderer.sprite = Data.portrait;

        //// Setup de l’Animator
        //if (animator != null)
        //{
        //    if (Data.animatorController != null)
        //        animator.runtimeAnimatorController = Data.animatorController;

        //    if (Data.animatorAvatar != null)
        //        animator.avatar = Data.animatorAvatar;
        //}

        // UI HP/MP
        if (hpBar != null)
        {
            hpBar.SetMaxValue(Data.baseHP);
            hpBar.SetValue(Data.currentHP);
        }
        if (mpBar != null)
        {
            mpBar.SetMaxValue(Data.baseMP);
            mpBar.SetValue(Data.currentMP);
        }
        if (rageBar != null)
        {
            rageBar.SetMaxValue(Data.maxRage);
            rageBar.SetValue(currentRage);
        }

        // Instanciation de l’UI personnalisée
        if (Data.uiPrefab != null && Data.characterType == CharacterType.SquadUnit)
        {
            GameObject uiInstance = Instantiate(Data.uiPrefab, transform);
            uiInstance.name = "SquadUnit_UI";

            // Tu peux ajuster la position par défaut si nécessaire :
            uiInstance.transform.localPosition = Vector3.zero;
            uiInstance.transform.localRotation = Quaternion.identity;
            uiInstance.transform.localScale = Vector3.one;
        }
        else if(Data.characterType == CharacterType.EnemyUnit)
        {
            return; // Pas d'UI pour les ennemis
        }
    }

    void Update()
    {
        HandleDeath();
    }

    public void TakeDamage(int amount)
    {
        currentHP = Mathf.Max(currentHP - amount, 0);
        if (hpBar != null) hpBar.SetValue(currentHP);
        PlayDamageFeedback();
        GetComponent<RageSystem>()?.AddRage(amount);
    }

    public void TakeParry()
    {
        // This method should be called when the currentTargetCharacter successfully parries an attack
    }

    void HandleDeath()
    {
        if (currentHP <= 0 && !deathTriggered)
        {
            PlayDeath();
        }
    }

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

    public void Heal(int amount)
    {
        Data.currentHP = Mathf.Min(Data.currentHP + amount, Data.baseHP);
        if (hpBar != null) hpBar.SetValue(Data.currentHP);
    }

    public void ApplyBuff(int value)
    {

    }
    public void RemoveBuff(int value)
    {

    }

    public void ApplyDebuff(int value)
    {

    }
    public void RemoveDebuff(int value)
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
        // Exemple fictif : si tu avais un dictionnaire `Dictionary<CharacterUnit, int> damageReceivedFrom`
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

}
