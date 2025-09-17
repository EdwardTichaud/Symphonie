using UnityEngine;

public enum FadeType
{
    Fade, Smooth
}

public class CinemachineFadeConfiguration : MonoBehaviour
{
    public FadeType fadeType = FadeType.Smooth;
}


