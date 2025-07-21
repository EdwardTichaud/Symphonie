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
        // Aucun protecteur valide ou protecteur déjà hors combat : on ne redirige pas
        if (protector == null || protector.currentHP <= 0)
            return false;

        // On inflige la moitié des dégâts au protecteur sans déclencher à nouveau
        // la redirection pour éviter une récursion infinie.
        protector.TakeDamage(amount * 0.5f, transform, false);
        return true;
    }
}
