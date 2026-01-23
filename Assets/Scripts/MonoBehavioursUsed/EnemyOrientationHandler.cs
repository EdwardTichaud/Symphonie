using UnityEngine;

public class EnemyOrientationHandler : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SceneBindings sceneBindings;

    [Header("Réglages")]
    public float rotationSpeed = 5f;
    public float detectionRange = 10f;
    private Transform player;

    void Start()
    {
        if (sceneBindings == null)
            sceneBindings = ServiceRegistry.GetOrFind<SceneBindings>(FindObjectsInactive.Include);
        if (sceneBindings != null && sceneBindings.Player != null)
            player = sceneBindings.Player.transform;

        if (player == null)
        {
            Debug.LogError("Player manquant !");
            enabled = false;
        }
    }

    void Update()
    {
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude <= detectionRange)
        {
            Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }
}
