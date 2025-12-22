using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Active ce checkpoint automatiquement au demarrage.")]
    [SerializeField] private bool activateOnStart = false;

    [Tooltip("Transform optionnel utilise pour le respawn. Si vide, utilise ce GameObject.")]
    [SerializeField] private Transform respawnPoint;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Start()
    {
        if (activateOnStart)
            RegisterCheckpoint();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        RegisterCheckpoint();
    }

    private void RegisterCheckpoint()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[Checkpoint] GameManager introuvable, checkpoint ignore.");
            return;
        }

        Transform target = respawnPoint != null ? respawnPoint : transform;
        GameManager.Instance.SetCheckpoint(target);
    }
}
