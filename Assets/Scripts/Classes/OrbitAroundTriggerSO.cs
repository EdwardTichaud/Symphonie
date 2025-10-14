using UnityEngine;

[CreateAssetMenu(menuName = "Symphonie/Camera/OrbitAround Trigger")]
public class OrbitAroundTriggerSO : ScriptableObject
{
    [Header("Cible autour de laquelle orbiter")]
    public string targetName;
    [HideInInspector] public Transform target;

    [Header("Paramètres d'orbite")]
    public float distance = 5f;
    public float speed = 30f;
    public bool orbitX = false;
    public bool orbitY = true;
    public bool orbitZ = false;

    /// <summary>
    /// Déclenche l'orbite en demandant au <see cref="BattleCameraController"/> de tourner autour de la cible.
    /// </summary>
    public void StartOrbit()
    {
        if (BattleCameraController.Instance == null)
        {
            Debug.LogError("[OrbitAroundTriggerSO] BattleCameraController.Instance introuvable !");
            return;
        }

        target = GameObject.Find(targetName)?.transform;

        if (target == null)
        {
            Debug.LogError("[OrbitAroundTriggerSO] Target non définie !");
            return;
        }

        BattleCameraController.Instance.StartOrbit(target, distance, speed, orbitX, orbitY, orbitZ);
    }

    /// <summary>
    /// Stoppe l'orbite actuelle.
    /// </summary>
    public void StopOrbit()
    {
        if (BattleCameraController.Instance == null)
        {
            Debug.LogError("[OrbitAroundTriggerSO] BattleCameraController.Instance introuvable !");
            return;
        }

        BattleCameraController.Instance.StopOrbit();
    }
}
