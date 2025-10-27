using UnityEngine;
using UnityEngine.VFX;

public class TestVFXBurst : MonoBehaviour
{
    public VisualEffect vfx;

    void Update()
    {
        // appuie sur la barre espace pour déclencher le burst
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector3 center = transform.position + Vector3.up * 1.5f;

            vfx.SetVector3("burstCenter", center);
            vfx.SetFloat("burstRadius", 3f);
            vfx.SetInt("spawnCount", 4000);

            vfx.SendEvent("OnBurst");
        }
    }
}
