using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// Destory the gameobject after a few seconds 
    /// </summary>
    public class DestroyLater : MonoBehaviour
    {
        public float WaitTime = 2.5F;
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(WaitTime);
            Destroy(gameObject);
        }
    }
}
