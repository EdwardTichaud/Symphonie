using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class ConcentrationSystem : MonoBehaviour
{
    public float maxConcentration = 100f;
    public float decayPerSecond = 5f;
    [HideInInspector] public float currentConcentration = 0f;

    private CharacterUnit unit;
    private float lastDamageTime;

    public bool IsFull => currentConcentration >= maxConcentration;

    private void Awake()
    {
        unit = GetComponent<CharacterUnit>();
    }

    private void Update()
    {
        if (currentConcentration > 0f && Time.time > lastDamageTime)
        {
            currentConcentration = Mathf.Max(currentConcentration - decayPerSecond * Time.deltaTime, 0f);
            UpdateBar();
        }
    }

    public void OnDamageTaken(float damage)
    {
        lastDamageTime = Time.time;
        currentConcentration = Mathf.Clamp(currentConcentration + damage * 0.1f, 0f, maxConcentration);
        UpdateBar();
    }

    public float CalculateBonusDamage(float baseDamage)
    {
        return IsFull ? baseDamage : 0f;
    }

    private void UpdateBar()
    {
        if (unit != null && unit.customBar != null)
            unit.customBar.SetValue(currentConcentration);
    }
}
