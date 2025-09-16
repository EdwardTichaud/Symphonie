using UnityEngine;

public class MoveToVector3 : MonoBehaviour
{
    [Header("Movement Settings")]
    public Vector3 direction = Vector3.forward; // direction du mouvement
    public float speed = 1f;                    // unités par seconde

    private Vector3 startPosition;

    void Start()
    {
        // On enregistre la position initiale
        startPosition = transform.position;
    }

    void LateUpdate()
    {
        if (NewBattleManager.Instance != null &&
            NewBattleManager.Instance.currentBattleState == BattleState.BattleIntro)
        {
            // Déplacement infini basé sur le temps écoulé
            transform.position += direction.normalized * speed * Time.deltaTime;
        }
        else
        {
            // Réinitialiser la position et empêcher tout mouvement
            transform.position = startPosition;
        }
    }
}
