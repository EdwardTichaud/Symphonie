using UnityEngine;

public class StartManager : MonoBehaviour
{
    void Start()
    {
        ResetTimeScale();
    }

    void Update()
    {
        
    }

    void ResetTimeScale()
    {
        Debug.Log("Resetting Time Scale to 1");
        Time.timeScale = 1f;
    }

    void ResetCamerasDepth()
    {
        Debug.Log("Resetting Camera Depths");
        Camera.main.depth = 1;
        Camera battleCamera = GameObject.FindGameObjectWithTag("BattleCamera").GetComponent<Camera>();
        battleCamera.depth = 0;
    }
}
