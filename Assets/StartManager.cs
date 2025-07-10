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
}
