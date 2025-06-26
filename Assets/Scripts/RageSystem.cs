using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class RageSystem : MonoBehaviour
{
    private CharacterUnit unit;

    private void Awake()
    {
        unit = GetComponent<CharacterUnit>();
    }

    public void AddRage(float damage)
    {
        if (unit == null || unit.Data == null) return;
        unit.currentRage = Mathf.Clamp(unit.currentRage + damage, unit.Data.baseRage, unit.Data.maxRage);
        if (unit.customBar != null)
            unit.customBar.SetValue(unit.currentRage);
    }

    public float CalculateBonusDamage()
    {
        if (unit == null || unit.Data == null) return 0;
        return Mathf.RoundToInt(unit.currentRage * unit.Data.rageDamageMultiplier);
    }
}
