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
        // Vérifie les références avant d'instancier pour éviter les NullReferenceException
        Vector3 spawnPosition; // Position d'apparition du VFX

        if (NewBattleManager.Instance != null && NewBattleManager.Instance.currentCharacterUnit != null)
        {
            // Positionne le VFX sur l'unité actuellement active
            spawnPosition = NewBattleManager.Instance.currentCharacterUnit.transform.position;
        }
        else
        {
            // En cas d'absence du gestionnaire ou de l'unité, on le place sur le lanceur de timeline
            spawnPosition = transform.position;
            Debug.LogWarning("[InstantiateGameObject] NewBattleManager ou currentCharacterUnit introuvable, instanciation au niveau du TimelineLauncher.");
        }

        if (gameObjectToInstantiate != null)
        {
            Instantiate(gameObjectToInstantiate, spawnPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("[InstantiateGameObject] Aucun GameObject fourni pour l'instanciation.");
        }
    }
}
