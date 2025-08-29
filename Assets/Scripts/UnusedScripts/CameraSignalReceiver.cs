using UnityEngine;

/// <summary>
/// Récepteur de signaux Timeline destiné à contrôler des mouvements de
/// caméra expérimentaux. Le tag <see cref="ExecuteAlways"/> permet de
/// tester ces mouvements directement dans l'Éditeur.
/// </summary>
[ExecuteAlways]
public class CameraSignalReceiver : MonoBehaviour
{
    public void StartOrbit(OrbitAroundTriggerSO orbitTrigger)
    {
        // Exécuté également lors de la prévisualisation pour voir le mouvement de caméra.
        if (orbitTrigger != null)
            orbitTrigger.StartOrbit();
    }

    public void StopOrbit(OrbitAroundTriggerSO orbitTrigger)
    {
        // Stoppe l'orbite en jeu ou dans l'Éditeur si l'orbite a été lancée.
        if (orbitTrigger != null)
            orbitTrigger.StopOrbit();
    }
}
