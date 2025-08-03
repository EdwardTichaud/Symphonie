using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraBreathing : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.05f;
    [SerializeField] private float frequency = 1f;

    private Vector3 baseLocalPosition;
    private Vector3 lastWorldPosition;

    void Start()
    {
        baseLocalPosition = transform.localPosition;
        lastWorldPosition = transform.position;
    }

    void LateUpdate()
    {
        // Désactive l'effet si une Timeline ou un CameraPath est en cours d'exécution pour éviter les conflits
        if ((TimelineManager.Instance != null && TimelineManager.Instance.IsTimelinePlaying) || CameraController.IsAnyPathPlaying)
        {
            baseLocalPosition = transform.localPosition; // Nouvel ancrage sans respiration
            lastWorldPosition = transform.position;
            return; // Aucune oscillation
        }

        // Vérifie si la caméra a réellement bougé depuis la dernière frame
        bool moved = (transform.position - lastWorldPosition).sqrMagnitude > 1e-6f;

        if (moved)
        {
            // Si la caméra a été déplacée par un autre script, on redéfinit la base
            baseLocalPosition = transform.localPosition;
        }
        else
        {
            // Ajout doux d'une oscillation verticale simulant une respiration
            float offset = Mathf.Sin(Time.time * frequency) * amplitude;
            transform.localPosition = baseLocalPosition + Vector3.up * offset;
        }

        // Mise à jour après toutes les éventuelles modifications pour ne pas compter l'oscillation comme un mouvement
        lastWorldPosition = transform.position;
    }
}
