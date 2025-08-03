using UnityEngine;

public class InstantiateGameObject : MonoBehaviour
{
    public bool instantiateAtStart = false;
    public GameObject gameObjectToInstantiate;

    void Start()
    {
        if (instantiateAtStart)
        {
            Instantiate(gameObjectToInstantiate, transform.position, Quaternion.identity);
        }
    }

    public void InstantiateFromTimeline(GameObject gameObjectToInstantiate)
    {
        Instantiate(gameObjectToInstantiate, NewBattleManager.Instance.currentCharacterUnit.transform.position, Quaternion.identity);
    }
}
