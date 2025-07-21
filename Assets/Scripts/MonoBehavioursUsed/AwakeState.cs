using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class AwakeState : UnitStateEffects
{
    [Tooltip("Multiplicateur appliqué aux caractéristiques en mode Awake")]
    public float statMultiplier = 1.5f;

    [Header("Aura")]
    [Tooltip("Prefab de l'aura affichée en mode Awake")] public GameObject auraPrefab;
    private GameObject auraInstance;

    [Header("FireWing")]
    [Tooltip("Référence à l'objet 'FireWing' à activer en mode Awake")]
    [SerializeField] private GameObject fireWing; // Peut rester vide : recherché automatiquement

    private CharacterUnit unit;
    private bool isAwake;

    public bool IsAwake => isAwake;

    protected override void Awake()
    {
        base.Awake();
        unit = GetComponent<CharacterUnit>();
        // Si aucune référence n'est fournie, on recherche l'enfant nommé "FireWing"
        if (fireWing == null)
        {
            Transform child = transform.Find("FireWing");
            if (child != null)
                fireWing = child.gameObject;
        }

        // Au départ l'objet FireWing doit être désactivé
        if (fireWing != null)
            fireWing.SetActive(false);
    }

    public void EnterAwake()
    {
        if (isAwake) return;
        isAwake = true;
        ApplyStatBonus();
        // Activation des ailes de feu lorsque le personnage entre en mode Awake
        if (fireWing != null)
            fireWing.SetActive(true);
        if (auraPrefab != null && auraInstance == null)
            auraInstance = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);
        EnterState();
    }

    public void ExitAwake()
    {
        if (!isAwake) return;
        isAwake = false;
        RemoveStatBonus();
        // Désactivation des ailes de feu quand on quitte le mode Awake
        if (fireWing != null)
            fireWing.SetActive(false);
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
