using UnityEngine;

/// <summary>
/// Paramètres utilisés lors de l'instanciation d'un prefab depuis une Timeline.
/// Ce composant doit être placé sur les prefabs qui seront instanciés afin de
/// préciser s'ils doivent apparaître sur le lanceur (caster) ou sur la cible
/// actuelle (target).
/// </summary>
public class TimelineInstantiationParameters : MonoBehaviour
{
    [Tooltip("Si vrai, le prefab sera instancié sur la cible (target) actuelle. Sinon il apparaîtra sur le lanceur (caster).")]
    public bool spawnOnTarget = false;
}
