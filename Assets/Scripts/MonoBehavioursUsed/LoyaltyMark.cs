using UnityEngine;

public class LoyaltyMark : MonoBehaviour
{
    public CharacterUnit protector;

    public void SetProtector(CharacterUnit unit)
    {
        protector = unit;
    }

    public bool RedirectDamage(float amount)
    {
        if (protector == null || protector.currentHP <= 0)
            return false;
        protector.TakeDamage(amount * 0.5f);
        return true;
    }
}
