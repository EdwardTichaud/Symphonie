using UnityEngine;

public class StartManager : MonoBehaviour
{
    void Awake()
    {
        ResetTimeScale();
        ResetCamerasDepth();
        SetIdleInWorld();
        DialogueManager.Instance.CloseDialogue();
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
        Camera battleCamera = GameObject.FindGameObjectWithTag("BattleCamera").GetComponentInChildren<Camera>();
        battleCamera.depth = 0;
    }

    void SetIdleInWorld()
    {
        GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<Animator>().Play("Idle_World");
    }
}
