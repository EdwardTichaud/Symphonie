using UnityEngine;

[ExecuteAlways]
public class LookAtMiddleEnemy : MonoBehaviour
{
    public Transform currentTarget;
    [Tooltip("Décalage appliqué lors du ciblage de la cible.")]
    public Vector3 offset;

    void LateUpdate()
    {
        if (NewBattleManager.Instance.currentBattleState == BattleState.BattleIntro)
        {
            if (currentTarget == null)
            {
                currentTarget = GameObject.Find("EnemyPosition_01").transform;
            }

            if (currentTarget != null)
            {
                Vector3 lookPosition = currentTarget.transform.position + offset;
                transform.LookAt(lookPosition);
            }
        }
    }
}
