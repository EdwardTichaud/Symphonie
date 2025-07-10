using UnityEngine;

public class RandomScale : MonoBehaviour
{
    public Vector3 minScale = Vector3.one * 0.5f;
    public Vector3 maxScale = Vector3.one * 2f;

    void Start()
    {
        float x = Random.Range(minScale.x, maxScale.x);
        float y = Random.Range(minScale.y, maxScale.y);
        float z = Random.Range(minScale.z, maxScale.z);
        transform.localScale = new Vector3(x, y, z);
    }
}
