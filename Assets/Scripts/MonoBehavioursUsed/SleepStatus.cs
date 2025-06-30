using UnityEngine;

public class SleepStatus : MonoBehaviour
{
    private bool isAsleep;
    public bool IsAsleep => isAsleep;

    public void Sleep()
    {
        isAsleep = true;
    }

    public void WakeUp()
    {
        isAsleep = false;
    }

    public void OnDamageTaken()
    {
        if (isAsleep)
            WakeUp();
    }
}
