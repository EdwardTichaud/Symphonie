using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class AwakeState : UnitStateEffects
{
    [Tooltip("Multiplicateur appliqué aux caractéristiques en mode Awake")]
    public float statMultiplier = 1.5f;

    [Header("Aura")]
    [Tooltip("Prefab de l'aura affichée en mode Awake")] public GameObject auraPrefab;
    private GameObject auraInstance;

    private CharacterUnit unit;
    private bool isAwake;

    public bool IsAwake => isAwake;

    protected override void Awake()
    {
        base.Awake();
        unit = GetComponent<CharacterUnit>();
    }

    public void EnterAwake()
    {
        if (isAwake) return;
        isAwake = true;
        ApplyStatBonus();
        if (auraPrefab != null && auraInstance == null)
            auraInstance = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);
        EnterState();
    }

    public void ExitAwake()
    {
        if (!isAwake) return;
        isAwake = false;
        RemoveStatBonus();
        if (auraInstance != null)
        {
            Destroy(auraInstance);
            auraInstance = null;
        }
        ExitState();
    }

    private void ApplyStatBonus()
    {
        if (unit == null) return;
        unit.currentStrength *= statMultiplier;
        unit.currentDefense *= statMultiplier;
        unit.currentReflex *= statMultiplier;
        unit.currentMobility *= statMultiplier;
        unit.currentPower *= statMultiplier;
        unit.currentStability *= statMultiplier;
        unit.currentVitality *= statMultiplier;
        unit.currentSagacity *= statMultiplier;
    }

    private void RemoveStatBonus()
    {
        if (unit == null) return;
        unit.currentStrength /= statMultiplier;
        unit.currentDefense /= statMultiplier;
        unit.currentReflex /= statMultiplier;
        unit.currentMobility /= statMultiplier;
        unit.currentPower /= statMultiplier;
        unit.currentStability /= statMultiplier;
        unit.currentVitality /= statMultiplier;
        unit.currentSagacity /= statMultiplier;
    }
}
