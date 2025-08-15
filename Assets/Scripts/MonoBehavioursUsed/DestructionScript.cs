using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestructionScript : MonoBehaviour
{
    // Temps avant la destruction automatique de l'objet
    public float timeBeforeDestroy;

    private void Start()
    {
        // 📝 Appel unique à Destroy pour éviter une allocation par frame
        // L'objet sera détruit au bout de `timeBeforeDestroy` secondes
        Destroy(gameObject, timeBeforeDestroy);
    }
}
