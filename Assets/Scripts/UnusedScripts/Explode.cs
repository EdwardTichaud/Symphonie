using UnityEngine;

public class Explode : MonoBehaviour
{
    public float speed = 2f;
    public float minScale = 0f;
    public float maxScale = 10f;

    void Update()
    {
        float scale = Mathf.Lerp(minScale, maxScale, Time.time * speed);
        transform.localScale = new Vector3(scale, scale, scale);
    }
}
