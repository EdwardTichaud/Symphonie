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

        // On inflige la moitié des dégâts au protecteur sans déclencher une
        // nouvelle redirection afin d'éviter les boucles infinies en cas de
        // protection mutuelle.
        protector.TakeDamage(amount * 0.5f, null, false);
        return true;
    }
}
