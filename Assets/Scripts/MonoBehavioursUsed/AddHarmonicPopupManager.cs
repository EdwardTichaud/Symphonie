using UnityEngine;

/// <summary>
/// Gestionnaire responsable d'afficher les popups d'ajout d'harmonique.
/// Inspiré du <see cref="DamagePopupManager"/>.
/// </summary>
public class AddHarmonicPopupManager : MonoBehaviour
{
    private static AddHarmonicPopupManager _instance;

    /// <summary>
    /// Accès global au gestionnaire. Recherche automatiquement une instance dans la scène si nécessaire.
    /// </summary>
    public static AddHarmonicPopupManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<AddHarmonicPopupManager>(); // Utilise la nouvelle API; l'ancienne méthode FindObjectOfType est obsolète
            return _instance;
        }
    }

    [SerializeField] private GameObject addHarmonicPrefab;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        transform.localScale = Vector3.one; // S'assure d'une échelle correcte
    }

    /// <summary>
    /// Crée un popup de gain d'harmonique au-dessus de la cible.
    /// </summary>
    /// <param name="target">Transform de l'unité concernée.</param>
    /// <param name="amount">Montant d'harmonique à afficher.</param>
    public void ShowAddHarmonic(Transform target, int amount)
    {
        if (addHarmonicPrefab == null)
        {
            Debug.LogWarning("[AddHarmonicPopupManager] Prefab manquant.");
            return;
        }

        GameObject popup = Instantiate(addHarmonicPrefab, transform, true);
        AddHarmonicPopup popupScript = popup.GetComponent<AddHarmonicPopup>();
        if (popupScript == null)
        {
            Debug.LogError("[AddHarmonicPopupManager] Le prefab ne contient pas de composant AddHarmonicPopup.");
            Destroy(popup);
            return;
        }

        popupScript.Initialize(amount, target);
    }
}
