using UnityEngine;

/// <summary>
/// MuninController lit les instructions de NewBattleManager (desiredTransform & isFollowing)
/// et déplace la caméra Munin en douceur.
/// Aucun Cinemachine, 100% script.
/// </summary>
public class MuninController : MonoBehaviour
{
    public static MuninController Instance { get; private set; }

    [Header("Smooth Settings")]
    public float followSpeed = 5f;   // vitesse de suivi dynamique
    public float snapSpeed = 10f;    // vitesse de snap pour pose fixe

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (NewBattleManager.Instance == null)
        {
            Debug.LogWarning("[MuninController] Aucun NewBattleManager assigné.");
            return;
        }

        Transform target = NewBattleManager.Instance.desiredTransform;
        bool isFollowing = NewBattleManager.Instance.isFollowing;

        if (target == null) return;

        if (isFollowing)
        {
            // Suivi fluide en continu (caméra vivante)
            transform.position = Vector3.Lerp(transform.position, target.position, Time.deltaTime * followSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, Time.deltaTime * followSpeed);
        }
        else
        {
            // Transition douce même pour un snap fixe (évite coupure brutale)
            transform.position = Vector3.Lerp(transform.position, target.position, Time.deltaTime * snapSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, Time.deltaTime * snapSpeed);
        }
    }
}
