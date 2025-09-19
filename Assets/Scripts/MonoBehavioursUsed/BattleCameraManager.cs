using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Gere l'activation des CinemachineCamera durant les combats.
/// Les transitions s'effectuent via <see cref="CinemachineBlendSwitcher"/>.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    /// <summary>Acces global au gestionnaire de camera de combat.</summary>
    public static BattleCameraManager Instance { get; private set; }

    // Composant responsable du changement de camera via les priorites.
    private CinemachineBlendSwitcher blendSwitcher;

    // Rig dédié qui anime les caméras selon des rôles précis.
    private BattleCameraRig cameraRig;

    // Ensemble des cameras Cinemachine disponibles pour les moves.
    private readonly List<CinemachineCamera> availableCameras = new();

    // Mapping role -> nom de caméra utilisé par le blend switcher.
    private readonly Dictionary<BattleCameraRole, string> roleToCameraName = new();
    private readonly Dictionary<string, BattleCameraRole> nameToRole = new();

    // Permet de connaître le plan actuellement prioritaire.
    private BattleCameraRole currentRole = BattleCameraRole.None;

    void Awake()
    {
        // Mise en place du singleton classique.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Recherche du CinemachineBlendSwitcher present dans la scene.
        blendSwitcher = FindFirstObjectByType<CinemachineBlendSwitcher>();
        if (!blendSwitcher)
            Debug.LogWarning("[BattleCameraManager] Aucun CinemachineBlendSwitcher trouve dans la scene.");

        // Recense toutes les CinemachineCamera presentes (angles speciaux).
        foreach (var cam in FindObjectsOfType<CinemachineCamera>())
        {
            if (cam != null)
                availableCameras.Add(cam);
        }

        cameraRig = FindFirstObjectByType<BattleCameraRig>();
        if (!cameraRig)
            Debug.LogWarning("[BattleCameraManager] Aucun BattleCameraRig détecté : les rôles caméra ne seront pas configurés.");
        else
            BuildRoleMappings();

        // Au demarrage du combat on revient sur la camera principale taggee "BattleCamera".
        // On force une transition immediate (duree 0) pour eviter un fondu au lancement.
        if (blendSwitcher)
            blendSwitcher.DisplayCamera(null, 0f);
    }

    /// <summary>
    /// Met à jour les correspondances rôle &lt;-&gt; nom de caméra à partir du rig présent dans la scène.
    /// </summary>
    private void BuildRoleMappings()
    {
        roleToCameraName.Clear();
        nameToRole.Clear();

        foreach (BattleCameraRole role in System.Enum.GetValues(typeof(BattleCameraRole)))
        {
            if (role == BattleCameraRole.None)
                continue;

            if (cameraRig.TryGetCameraName(role, out var cameraName))
            {
                roleToCameraName[role] = cameraName;
                if (!nameToRole.ContainsKey(cameraName))
                    nameToRole.Add(cameraName, role);
            }
        }
    }

    /// <summary>
    /// Fournit les cibles au rig pour positionner correctement les plans.
    /// </summary>
    /// <param name="caster">Unité qui initie l'action.</param>
    /// <param name="target">Unité subissant l'action (peut être <c>null</c> pour les selfs casts).</param>
    /// <param name="midpoint">Point manuel optionnel utilisé par certains moves.</param>
    /// <param name="casterAnchor">Ancre précise à suivre pour le lanceur (poitrine, tête...).</param>
    /// <param name="targetAnchor">Ancre précise pour la cible.</param>
    public void ConfigureActionTargets(
        CharacterUnit caster,
        CharacterUnit target,
        Vector3? midpoint = null,
        Transform casterAnchor = null,
        Transform targetAnchor = null)
    {
        cameraRig?.ConfigureTargets(caster, target, midpoint, casterAnchor, targetAnchor);
    }

    /// <summary>
    /// Efface les cibles connues du rig (fin de move ou retour au neutre).
    /// </summary>
    public void ClearRigTargets()
    {
        cameraRig?.ClearTargets();
    }

    /// <summary>
    /// Active une caméra en s'appuyant sur un rôle cinématique.
    /// </summary>
    public void SwitchToCamera(BattleCameraRole role, float blendTime = -1f, CinemachineBlendDefinition.Styles? overrideStyle = null)
    {
        if (role == BattleCameraRole.None)
        {
            currentRole = BattleCameraRole.None;
            cameraRig?.NotifyActiveRole(BattleCameraRole.None);
            SwitchToCamera((string)null, blendTime, overrideStyle);
            return;
        }

        if (!roleToCameraName.TryGetValue(role, out var cameraName))
        {
            Debug.LogWarning($"[BattleCameraManager] Aucun GameObject associé au rôle caméra {role}.");
            return;
        }

        float duration = blendTime >= 0f ? blendTime : ComputeBlendDuration(currentRole, role);
        var style = overrideStyle ?? ComputeBlendStyle(currentRole, role);

        currentRole = role;
        cameraRig?.NotifyActiveRole(role);
        SwitchToCamera(cameraName, duration, style);
    }

    /// <summary>
    /// Active la camera correspondant au nom fourni.
    /// - <c>null</c>  : retour a la camera de combat par defaut (tag "BattleCamera").
    /// - chaine vide : selection d'une camera aleatoire.
    /// </summary>
    /// <param name="cameraName">Nom de la camera souhaitee.</param>
    /// <param name="blendTime">
    /// Duree du fondu en secondes. Utiliser une valeur negative pour conserver
    /// la duree definie dans le <see cref="CinemachineBlendSwitcher"/>.
    /// </param>
    public void SwitchToCamera(string cameraName, float blendTime = -1f, CinemachineBlendDefinition.Styles? overrideStyle = null)
    {
        if (!blendSwitcher)
            return; // Impossible de switcher sans blendSwitcher

        // Cas 1 : aucun move/item en cours -> on revient sur la camera par defaut.
        if (cameraName == null)
        {
            currentRole = BattleCameraRole.None;
            cameraRig?.NotifyActiveRole(BattleCameraRole.None);
            if (blendTime >= 0f)
                blendSwitcher.DisplayCamera(null, blendTime, overrideStyle); // Transition forcee
            else
                blendSwitcher.DisplayCamera(null, blendTime, overrideStyle); // Duree par defaut
            return;
        }

        if (nameToRole.TryGetValue(cameraName, out var resolvedRole))
        {
            // Permet de rester compatible avec les appels legacy basés sur le nom.
            SwitchToCamera(resolvedRole, blendTime, overrideStyle);
            return;
        }

        // Cas 2 : nom vide -> choix d'une camera aleatoire.
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            if (availableCameras.Count > 0)
            {
                var randomCam = availableCameras[Random.Range(0, availableCameras.Count)];
                cameraName = randomCam.gameObject.name;
            }
            else
            {
                currentRole = BattleCameraRole.None;
                cameraRig?.NotifyActiveRole(BattleCameraRole.None);
                // Si aucune camera speciale n'est disponible, on retourne sur la camera
                // principale avec la duree de blend souhaitee ou celle par defaut.
                if (blendTime >= 0f)
                    blendSwitcher.DisplayCamera(null, blendTime, overrideStyle);
                else
                    blendSwitcher.DisplayCamera(null, blendTime, overrideStyle);
                return;
            }
        }

        currentRole = BattleCameraRole.None;
        cameraRig?.NotifyActiveRole(BattleCameraRole.None);
        // Affiche la camera demandee avec la duree de blend appropriee.
        if (blendTime >= 0f)
            blendSwitcher.DisplayCamera(cameraName, blendTime, overrideStyle);
        else
            blendSwitcher.DisplayCamera(cameraName, blendTime, overrideStyle);
    }

    private float ComputeBlendDuration(BattleCameraRole from, BattleCameraRole to)
    {
        if ((from == BattleCameraRole.ClosePushCaster && to == BattleCameraRole.TargetReaction) ||
            (from == BattleCameraRole.TargetReaction && to == BattleCameraRole.ClosePushCaster))
            return 0.08f; // Cut nerveux pour les contres.

        if ((from == BattleCameraRole.WideEstablish && to == BattleCameraRole.OverShoulderCasterToTarget) ||
            (from == BattleCameraRole.OverShoulderCasterToTarget && to == BattleCameraRole.WideEstablish))
            return 0.4f; // Transition douce entre plan large et épaule.

        if (from == BattleCameraRole.Victory || to == BattleCameraRole.Victory)
            return 1f; // Plan final plus ample.

        return -1f; // Conserve la durée par défaut du BlendSwitcher.
    }

    private CinemachineBlendDefinition.Styles? ComputeBlendStyle(BattleCameraRole from, BattleCameraRole to)
    {
        if ((from == BattleCameraRole.ClosePushCaster && to == BattleCameraRole.TargetReaction) ||
            (from == BattleCameraRole.TargetReaction && to == BattleCameraRole.ClosePushCaster))
            return CinemachineBlendDefinition.Styles.Cut;

        if (from == BattleCameraRole.Victory || to == BattleCameraRole.Victory)
            return CinemachineBlendDefinition.Styles.EaseOut;

        if ((from == BattleCameraRole.WideEstablish && to == BattleCameraRole.OverShoulderCasterToTarget) ||
            (from == BattleCameraRole.OverShoulderCasterToTarget && to == BattleCameraRole.WideEstablish))
            return CinemachineBlendDefinition.Styles.EaseInOut;

        return null;
    }
}
