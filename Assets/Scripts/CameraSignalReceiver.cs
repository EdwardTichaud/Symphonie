using UnityEngine;

public class CameraSignalReceiver : MonoBehaviour
{
    public void StartOrbit(OrbitAroundTriggerSO orbitTrigger)
    {
        if (orbitTrigger != null)
            orbitTrigger.StartOrbit();
    }

    public void StopOrbit(OrbitAroundTriggerSO orbitTrigger)
    {
        if (orbitTrigger != null)
            orbitTrigger.StopOrbit();
    }
}
