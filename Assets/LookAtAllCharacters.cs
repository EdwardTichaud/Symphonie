using UnityEngine;

public class LookAtAllCharacters : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage.")]
    public Vector3 offset;
    [Tooltip("Vitesse de lissage du regard.")]
    public float smoothSpeed = 5f;

    private Vector3 smoothedLookPosition;

    void LateUpdate()
    {
        if (NewBattleManager.Instance != null &&
            NewBattleManager.Instance.currentBattleState == BattleState.Initialization)
        {
            CharacterUnit[] allUnits = FindObjectsOfType<CharacterUnit>();

            if (allUnits.Length > 0)
            {
                // Calcul du barycentre
                Vector3 center = Vector3.zero;
                foreach (CharacterUnit unit in allUnits)
                {
                    center += unit.transform.position;
                }
                center /= allUnits.Length;

                // Ajout offset
                Vector3 targetLookPosition = center + offset;

                // Lerp pour lisser le mouvement
                smoothedLookPosition = Vector3.Lerp(
                    smoothedLookPosition == Vector3.zero ? targetLookPosition : smoothedLookPosition,
                    targetLookPosition,
                    smoothSpeed * Time.deltaTime
                );

                // LookAt vers la position lissée
                transform.LookAt(smoothedLookPosition);
            }
        }
    }
}
