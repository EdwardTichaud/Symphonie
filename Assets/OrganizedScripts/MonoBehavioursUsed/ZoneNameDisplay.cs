using UnityEngine;
using TMPro;

public class ZoneNameDisplay : MonoBehaviour
{
    public static ZoneNameDisplay Instance { get; private set; }

    [Header("UI References")]
    public TextMeshProUGUI sceneNameText;
    public TextMeshProUGUI sceneDescriptionText;

    private Animator animator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Récupère la ZoneSO actuelle via ZoneManager et met à jour le texte.
    /// </summary>
    public void ShowCurrentZoneInfo()
    {
        if (ZoneManager.Instance == null || ZoneManager.Instance.currentZone == null)
        {
            Debug.LogWarning("[LevelName] Aucune Zone courante trouvée !");
            return;
        }

        ZoneSO currentZone = ZoneManager.Instance.currentZone;

        sceneNameText.text = currentZone.zoneName;
        sceneDescriptionText.text = currentZone.description;

        if (animator != null)
            animator.SetTrigger("On");

        Debug.Log($"[LevelName] Affiche : {currentZone.zoneName} / {currentZone.description}");
    }
}
