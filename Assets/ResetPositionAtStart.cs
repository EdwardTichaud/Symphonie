using UnityEngine;

public class ResetPositionAtStart : MonoBehaviour
{
    void Start()
    {
        transform.position = Vector3.zero;
    }
}
