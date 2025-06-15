using UnityEngine;

/// <summary>
/// Corrige le twist causé par la rotation root Motion de Mixamo.
/// Force Y rotation à zéro.
/// </summary>
public class RootRotationFixer : MonoBehaviour
{
    void LateUpdate()
    {
        Vector3 fixedRotation = transform.eulerAngles;
        fixedRotation.y = 0f;  // ou la valeur de ta direction globale
        transform.eulerAngles = fixedRotation;
    }
}
