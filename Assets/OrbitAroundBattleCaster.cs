using UnityEngine;

public class OrbitAroundBattleCaster : MonoBehaviour
{
    public float orbitSpeed = 20f;
    public float orbitRadius = 5f;
    public float heightOffset = 2f;

    void Update()
    {
        if(NewBattleManager.Instance.currentCharacterUnit != null)
        {
            transform.RotateAround(NewBattleManager.Instance.currentCharacterUnit.transform.position, new Vector3(0,heightOffset,0), 20 * Time.deltaTime);
        }
    }
}
