using UnityEngine;

public class InstantiateGameObject : MonoBehaviour
{
    public GameObject gameObjectToInstantiate;

    void Start()
    {
        Instantiate(gameObjectToInstantiate, transform.position, Quaternion.identity);
    }
}
