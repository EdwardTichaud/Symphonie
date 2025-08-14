using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    private static DamagePopupManager _instance;

    /// <summary>
    /// Accès global au gestionnaire. Cherche automatiquement une instance
    /// existante dans la scène si aucune n'a encore été assignée.
    /// </summary>
    public static DamagePopupManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<DamagePopupManager>(); // Migration vers la nouvelle API de recherche d'objets
            return _instance;
        }
    }

    [SerializeField] private GameObject damagePopupPrefab;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            // Évite les doublons au démarrage
            Destroy(gameObject);
            return;
        }

        // S'assure que l'objet ne possède pas d'échelle étrange héritée d'un prefab
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Crée un popup de dégâts au-dessus de la cible donnée.
    /// </summary>
    /// <param name="target">Transform à suivre.</param>
    /// <param name="amount">Montant de dégâts.</param>
    public void ShowDamage(Transform target, int amount)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning("[DamagePopupManager] Prefab manquant.");
            return;
        }

        // Instancie le prefab comme enfant du gestionnaire tout en conservant
        // la même échelle que dans le prefab pour éviter qu'elle ne soit
        // multipliée par celle du parent.
        GameObject popup = Instantiate(damagePopupPrefab, transform, true);

        // Vérifie la présence du composant requis avant initialisation
        DamagePopup popupScript = popup.GetComponent<DamagePopup>();
        if (popupScript == null)
        {
            Debug.LogError("[DamagePopupManager] Le prefab ne contient pas de composant DamagePopup.");
            Destroy(popup);
            return;
        }

        // Initialisation avec la cible à suivre et le montant de dégâts
        popupScript.Initialize(amount, target);
    }
}
