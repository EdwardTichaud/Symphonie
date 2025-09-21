using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Gere l'activation des CinemachineCamera durant les combats.
/// Les transitions s'effectuent via <see cref="CinemachineBlendSwitcher"/>.
/// </summary>
public class BattleCameraManager : MonoBehaviour
{
    /// <summary>Nom de la caméra d'introduction explicitement référencé dans les scripts.</summary>
    private const string IntroCameraName = "CMV_BattleIntro";

    /// <summary>Temps minimal pour considérer qu'une direction est valide.</summary>
    private const float IntroLookDirectionThreshold = 0.0001f;

    /// <summary>Acces global au gestionnaire de camera de combat.</summary>
    public static BattleCameraManager Instance { get; private set; }

    // Composant responsable du changement de camera via les priorites.
    private CinemachineBlendSwitcher blendSwitcher;

    // Rig dédié qui anime les caméras selon des rôles précis.
    private BattleCameraRig cameraRig;

    // Ensemble des cameras Cinemachine disponibles pour les moves.
    private readonly List<CinemachineCamera> availableCameras = new();

    // Accès direct aux caméras via leur nom pour faciliter les repositionnements ponctuels.
    private readonly Dictionary<string, CinemachineCamera> cameraByName = new();

    // Mapping role -> nom de caméra utilisé par le blend switcher.
    private readonly Dictionary<BattleCameraRole, string> roleToCameraName = new();
    private readonly Dictionary<string, BattleCameraRole> nameToRole = new();

    // Permet de connaître le plan actuellement prioritaire.
    private BattleCameraRole currentRole = BattleCameraRole.None;

    // ------------------------------------------------------------------------------
    // Gestion dédiée à la Cinemachine d'introduction
    // ------------------------------------------------------------------------------

    /// <summary>Transform de la caméra d'introduction pour manipuler directement sa position.</summary>
    private Transform introCameraTransform;

    /// <summary>Position locale de référence de la caméra d'introduction.</summary>
    private Vector3 introCameraInitialLocalPosition;

    /// <summary>Rotation locale de référence de la caméra d'introduction.</summary>
    private Quaternion introCameraInitialLocalRotation;

    /// <summary>Coroutine en charge du déplacement continu de la caméra d'intro.</summary>
    private Coroutine introCameraMoveRoutine;

    /// <summary>Coroutine utilisée pour différer la remise à zéro après un fondu.</summary>
    private Coroutine introCameraResetRoutine;

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

        RefreshCameraCache();

        // On mémorise la position initiale de la caméra d'introduction dès que possible pour faciliter ses resets.
        CacheIntroCameraReference();

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
    /// Reconstitue le dictionnaire nom -> caméra afin de toujours disposer d'une référence valide.
    /// </summary>
    private void RefreshCameraCache()
    {
        cameraByName.Clear();

        // Nettoie les entrées nulles pouvant apparaître après un changement de scène.
        availableCameras.RemoveAll(cam => cam == null);

        foreach (var cam in availableCameras)
            cameraByName[cam.gameObject.name] = cam;

        // Si la caméra d'intro vient d'être (re)trouvée, on actualise ses informations de base immédiatement.
        CacheIntroCameraReference();
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
    /// Mémorise (ou met à jour) la référence vers la caméra d'introduction et sa transform initiale.
    /// </summary>
    private void CacheIntroCameraReference()
    {
        // Si aucune caméra d'intro n'est actuellement référencée, on purge les données obsolètes pour repartir proprement.
        if (!cameraByName.TryGetValue(IntroCameraName, out var camera) || camera == null)
        {
            introCameraTransform = null; // La caméra a peut-être été détruite : on oublie toute référence précédente.
            return;
        }

        Transform candidate = camera.transform;

        // Si nous pointons déjà sur cette transform, il est inutile de recalculer la position initiale.
        if (introCameraTransform == candidate)
            return;

        introCameraTransform = candidate;
        introCameraInitialLocalPosition = introCameraTransform.localPosition;
        introCameraInitialLocalRotation = introCameraTransform.localRotation;
    }

    /// <summary>
    /// Vérifie que la caméra d'introduction est accessible et mémorise sa position de base si nécessaire.
    /// </summary>
    /// <returns><c>true</c> si la caméra est disponible, <c>false</c> sinon.</returns>
    private bool EnsureIntroCameraReference()
    {
        if (introCameraTransform != null)
            return true; // Référence déjà valide.

        if (!TryGetCameraByName(IntroCameraName, out var camera) || camera == null)
        {
            Debug.LogWarning($"[BattleCameraManager] Impossible de localiser la caméra '{IntroCameraName}'.");
            introCameraTransform = null;
            return false;
        }

        introCameraTransform = camera.transform;
        introCameraInitialLocalPosition = introCameraTransform.localPosition;
        introCameraInitialLocalRotation = introCameraTransform.localRotation;
        return true;
    }

    /// <summary>
    /// Démarre le travelling linéaire de la caméra d'introduction pour dynamiser l'entrée en combat.
    /// </summary>
    /// <param name="moveSpeed">Vitesse de déplacement exprimée en unités monde/seconde.</param>
    /// <param name="lookAtPoint">Point vers lequel la caméra doit rester orientée (coordonnées monde).</param>
    public void StartBattleIntroCameraTravel(float moveSpeed = 5f, Vector3? lookAtPoint = null)
    {
        if (!EnsureIntroCameraReference())
            return; // Aucun traitement possible si la caméra n'existe pas.

        // Interrompt un éventuel déplacement encore actif afin d'éviter les doublons.
        if (introCameraMoveRoutine != null)
        {
            StopCoroutine(introCameraMoveRoutine);
            introCameraMoveRoutine = null;
        }

        // Si une remise à zéro était planifiée, on l'annule : un nouveau combat démarre, la caméra repart de zéro.
        if (introCameraResetRoutine != null)
        {
            StopCoroutine(introCameraResetRoutine);
            introCameraResetRoutine = null;
        }

        // Replace explicitement la caméra sur ses coordonnées d'origine pour garantir un démarrage cohérent.
        introCameraTransform.localPosition = introCameraInitialLocalPosition;
        introCameraTransform.localRotation = introCameraInitialLocalRotation;

        Vector3 targetPoint = lookAtPoint ?? Vector3.zero;

        // Oriente immédiatement la caméra vers le point désiré afin d'éviter un frame "hors cible".
        Vector3 lookDirection = targetPoint - introCameraTransform.position;
        if (lookDirection.sqrMagnitude > IntroLookDirectionThreshold)
            introCameraTransform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

        introCameraMoveRoutine = StartCoroutine(IntroCameraTravelRoutine(moveSpeed, targetPoint));
    }

    /// <summary>
    /// Stoppe le travelling de la caméra d'intro et programme sa remise en place après le fondu.
    /// </summary>
    /// <param name="delayBeforeReset">Temps (en secondes réelles) à attendre avant de restaurer la position initiale.</param>
    public void StopBattleIntroCameraTravel(float delayBeforeReset = 0f)
    {
        if (introCameraMoveRoutine != null)
        {
            StopCoroutine(introCameraMoveRoutine);
            introCameraMoveRoutine = null;
        }

        if (introCameraTransform == null)
            return; // Rien à restaurer si la caméra n'est pas disponible.

        if (introCameraResetRoutine != null)
        {
            StopCoroutine(introCameraResetRoutine);
            introCameraResetRoutine = null;
        }

        if (delayBeforeReset <= 0f)
        {
            // Remise immédiate pour les transitions instantanées.
            introCameraTransform.localPosition = introCameraInitialLocalPosition;
            introCameraTransform.localRotation = introCameraInitialLocalRotation;
        }
        else
        {
            introCameraResetRoutine = StartCoroutine(ResetIntroCameraAfterDelay(delayBeforeReset));
        }
    }

    /// <summary>
    /// Coroutine appliquant le déplacement linéaire le long de l'axe Z monde tout en gardant un point de mire fixe.
    /// </summary>
    private IEnumerator IntroCameraTravelRoutine(float moveSpeed, Vector3 lookAtPoint)
    {
        while (true)
        {
            if (introCameraTransform == null)
                yield break; // Sécurité : la caméra peut être détruite lors d'un changement de scène.

            float delta = Time.unscaledDeltaTime; // Utilisation du temps réel pour conserver une vitesse constante malgré les slow-mo.

            // Translation le long de l'axe Z monde.
            Vector3 worldPosition = introCameraTransform.position;
            worldPosition += Vector3.forward * moveSpeed * delta;
            introCameraTransform.position = worldPosition;

            // Maintien du regard vers le point spécifié.
            Vector3 lookDirection = lookAtPoint - worldPosition;
            if (lookDirection.sqrMagnitude > IntroLookDirectionThreshold)
                introCameraTransform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            yield return null; // Attente d'une frame avant de poursuivre le mouvement.
        }
    }

    /// <summary>
    /// Ramène la caméra d'introduction sur ses coordonnées d'origine après un fondu vers un autre plan.
    /// </summary>
    private IEnumerator ResetIntroCameraAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));

        if (introCameraTransform != null)
        {
            introCameraTransform.localPosition = introCameraInitialLocalPosition;
            introCameraTransform.localRotation = introCameraInitialLocalRotation;
        }

        introCameraResetRoutine = null;
    }

    /// <summary>
    /// Tente de récupérer une <see cref="CinemachineCamera"/> via son nom de GameObject.
    /// </summary>
    public bool TryGetCameraByName(string cameraName, out CinemachineCamera camera)
    {
        if (string.IsNullOrEmpty(cameraName))
        {
            camera = null;
            return false;
        }

        if (!cameraByName.TryGetValue(cameraName, out camera) || camera == null)
        {
            RefreshCameraCache();
            if (!cameraByName.TryGetValue(cameraName, out camera) || camera == null)
            {
                availableCameras.Clear();
                foreach (var cam in FindObjectsOfType<CinemachineCamera>())
                {
                    if (cam != null)
                        availableCameras.Add(cam);
                }

                RefreshCameraCache();
                cameraByName.TryGetValue(cameraName, out camera);
            }
        }

        return camera != null;
    }

    /// <summary>
    /// Aligne une Cinemachine sur un point de référence sans modifier ses priorités.
    /// Cette méthode est utilisée par les menus, le ciblage et les actions pour
    /// réutiliser les ancres placées sur chaque <see cref="CharacterUnit"/>.
    /// </summary>
    /// <param name="cameraName">Nom de la Cinemachine à repositionner.</param>
    /// <param name="anchor">Ancre (souvent un enfant du personnage) servant de repère.</param>
    /// <returns><c>true</c> si l'alignement a réussi, <c>false</c> sinon.</returns>
    public bool AlignCameraToAnchor(string cameraName, Transform anchor)
    {
        if (anchor == null)
        {
            Debug.LogWarning("[BattleCameraManager] Impossible d'aligner la caméra : ancre absente.");
            return false;
        }

        if (!TryGetCameraByName(cameraName, out var camera))
        {
            Debug.LogWarning($"[BattleCameraManager] Caméra inconnue : {cameraName}.");
            return false;
        }

        // Le positionnement immédiat garantit que le prochain blend partira du bon endroit.
        camera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        return true;
    }

    /// <summary>
    /// Replace une caméra statique sur une ancre donnée puis lui donne la priorité.
    /// Utile pour les menus qui disposent d'un point de vue par personnage.
    /// </summary>
    public void SwitchToCameraAndAlign(
        string cameraName,
        Transform anchor,
        float blendTime = 0f,
        CinemachineBlendDefinition.Styles? overrideStyle = null)
    {
        if (!AlignCameraToAnchor(cameraName, anchor))
            return;

        // Puis donne la priorité à cette caméra en privilégiant un cut (blendTime = 0 par défaut).
        SwitchToCamera(cameraName, blendTime, overrideStyle);
    }

    /// <summary>
    /// Active une caméra en s'appuyant sur un rôle cinématique.
    /// </summary>
    public void SwitchToCamera(BattleCameraRole role, float blendTime = -1f, CinemachineBlendDefinition.Styles? overrideStyle = null)
    {
        if (role == BattleCameraRole.None)
        {
            // Retour explicite sur la caméra de base : on ne passe surtout pas par la surcharge string
            // pour éviter tout aller-retour inutile.
            currentRole = BattleCameraRole.None;
            cameraRig?.NotifyActiveRole(BattleCameraRole.None);
            DisplayCameraWithBlend(null, blendTime, overrideStyle);
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
        // On replace immédiatement la caméra ciblée sur son ancre pour amorcer le blend depuis
        // une pose cohérente. Sans cela, la caméra peut parcourir une grande distance lors de
        // la première frame, donnant la sensation d'un cut brutal.
        cameraRig?.SnapToRolePose(role);
        DisplayCameraWithBlend(cameraName, duration, style);
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
            DisplayCameraWithBlend(null, blendTime, overrideStyle);
            return;
        }

        if (nameToRole.TryGetValue(cameraName, out var resolvedRole))
        {
            // Compatibilité avec les anciennes séquences qui adressaient les cams par nom :
            // on profite quand même des durées/styles personnalisés.
            float duration = blendTime >= 0f ? blendTime : ComputeBlendDuration(currentRole, resolvedRole);
            var style = overrideStyle ?? ComputeBlendStyle(currentRole, resolvedRole);

            currentRole = resolvedRole;
            cameraRig?.NotifyActiveRole(resolvedRole);
            cameraRig?.SnapToRolePose(resolvedRole);
            DisplayCameraWithBlend(cameraName, duration, style);
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
                DisplayCameraWithBlend(null, blendTime, overrideStyle);
                return;
            }
        }

        currentRole = BattleCameraRole.None;
        cameraRig?.NotifyActiveRole(BattleCameraRole.None);
        // Affiche la camera demandee avec la duree de blend appropriee.
        DisplayCameraWithBlend(cameraName, blendTime, overrideStyle);
    }

    /// <summary>
    /// Envoie la requête de blend au <see cref="CinemachineBlendSwitcher"/> en gérant le cas « durée par défaut ».
    /// </summary>
    /// <param name="cameraName">Nom de la caméra ciblée (ou <c>null</c> pour la caméra principale).</param>
    /// <param name="blendTime">Durée imposée, ou valeur négative pour conserver la configuration du blend switcher.</param>
    /// <param name="overrideStyle">Style de blend optionnel.</param>
    private void DisplayCameraWithBlend(string cameraName, float blendTime, CinemachineBlendDefinition.Styles? overrideStyle)
    {
        if (!blendSwitcher)
            return;

        // CinemachineBlendSwitcher gère déjà le cas d'une durée négative en retombant sur sa valeur par défaut.
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

    /// <summary>
    /// Nom de la caméra actuellement prioritaire (ou <c>null</c> si aucune Cinemachine n'est active).
    /// </summary>
    public string CurrentCinemachineCameraName => blendSwitcher ? blendSwitcher.CurrentCameraName : null;

    /// <summary>
    /// Indique si une Cinemachine est actuellement prioritaire dans le <see cref="CinemachineBrain"/>.
    /// </summary>
    public bool HasActiveCinemachineCamera => blendSwitcher && blendSwitcher.HasActiveCamera;
}
