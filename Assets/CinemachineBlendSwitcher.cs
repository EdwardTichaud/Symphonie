using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// Permet de changer de CinemachineCamera en ajustant les priorites.
/// </summary>
[DisallowMultipleComponent]
public class CinemachineBlendSwitcher : MonoBehaviour
{
    [Header("Brain (auto si vide)")]
    [SerializeField] private CinemachineBrain brain;

    [Header("Cameras (auto-scan si vide)")]
    [SerializeField] private List<CinemachineCamera> cameras = new();

    [Header("Priorites")]
    [SerializeField] private int activePriority = 100;
    [SerializeField] private int inactivePriority = 10;

    [Header("Style de blend par defaut")]
    [SerializeField]
    private CinemachineBlendDefinition.Styles blendStyle =
        CinemachineBlendDefinition.Styles.EaseInOut;

    [Tooltip("Duree par defaut du blend ou du fondu entre deux cameras (en secondes).")]
    [SerializeField] private float defaultBlendDuration = 1f;

    [Header("Fondu visuel")] // Permet de remplacer le mouvement par un fondu
    [Tooltip("Active la possibilite de realiser un fondu visuel entre deux plans.")]
    [SerializeField] private bool useVisualCrossFade = true;

    [Tooltip("Courbe d'evolution de l'opacite durant le fondu (0 = transparent, 1 = opaque).")]
    [SerializeField] private AnimationCurve crossFadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Canvas optionnel contenant l'image de fondu. Laisse vide pour une creation automatique.")]
    [SerializeField] private Canvas crossFadeCanvas;

    [Tooltip("Image RawImage optionnelle utilisee pour afficher le screenshot du plan precedent.")]
    [SerializeField] private RawImage crossFadeImage;

    private readonly Dictionary<string, CinemachineCamera> _byName = new();
    private CinemachineCamera _current; // camera actuellement active
    private Coroutine crossFadeRoutine; // coroutine de fondu en cours
    private Texture2D lastCapturedFrame; // reference du screenshot utilise pendant le fondu

    void Awake()
    {
        // Recuperation du CinemachineBrain sur la camera de rendu.
        if (!brain && Camera.main) brain = Camera.main.GetComponent<CinemachineBrain>();
        if (!brain) Debug.LogWarning("[BlendSwitcher] Aucun CinemachineBrain trouve sur la camera de rendu.");

        // Collecte automatique des CinemachineCamera si la liste est vide.
        if (cameras == null || cameras.Count == 0)
            cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).ToList();

        cameras = cameras.Where(c => c != null).Distinct().ToList();

        // Prepare l'overlay de fondu si necessaire (creation automatique si references vides).
        if (useVisualCrossFade) EnsureCrossFadeOverlay();

        // Construction du dictionnaire d'acces rapide par nom.
        _byName.Clear();
        foreach (var c in cameras)
        {
            // S'assure que chaque camera dispose du composant definissant son type de transition.
            EnsureFadeConfiguration(c);

            var key = c.gameObject.name;
            if (!_byName.ContainsKey(key)) _byName.Add(key, c);
        }

        // Toutes les cameras commencent avec une priorite inactive.
        foreach (var c in cameras) c.Priority = inactivePriority;

        // Active immediatement la camera d'indice 0 si elle existe afin
        // d'avoir une vue de combat par defaut pour les menus et le ciblage.
        ActivateDefaultCameraPriorities();
        UpdateActiveCameraStates(_current);
    }

    /// <summary>
    /// Active la <see cref="CinemachineCamera"/> nommee <paramref name="cameraName"/>.
    /// <para>Si <paramref name="cameraName"/> est <c>null</c>, la camera d'indice 0
    /// devient la camera par defaut.</para>
    /// <para>Si une chaine vide est passee, toutes les cameras sont desactivees
    /// pour revenir a la camera classique.</para>
    /// </summary>
    public void DisplayCamera(string cameraName)
    {
        // Redirige vers la surcharge avec durée explicite en utilisant
        // la durée de blend par défaut.
        DisplayCamera(cameraName, defaultBlendDuration, null);
    }

    /// <summary>
    /// Active la <see cref="CinemachineCamera"/> nommee <paramref name="cameraName"/>.
    /// </summary>
    /// <param name="cameraName">Nom de la camera a activer.</param>
    /// <param name="blendDuration">Durée du blend en secondes.</param>
    /// <param name="overrideStyle">Style de blend à forcer (null = style par défaut).</param>
    public void DisplayCamera(string cameraName, float blendDuration, CinemachineBlendDefinition.Styles? overrideStyle = null)
    {
        float resolvedDuration = blendDuration >= 0f ? blendDuration : defaultBlendDuration;

        // Si la camera cible exige un fondu visuel, on lance une coroutine dediee qui
        // capture l'image actuelle avant de basculer sur la nouvelle camera.
        if (ShouldUseCrossFade(cameraName, resolvedDuration))
        {
            // Si le composant est desactive ou si la coroutine n'a pas le droit de tourner,
            // on retombe sur le comportement classique pour eviter de bloquer la transition.
            if (!isActiveAndEnabled)
            {
                ApplyCameraSwitch(cameraName, resolvedDuration, false, overrideStyle);
                return;
            }

            // Si un fondu est deja en cours, on l'interrompt proprement afin de ne pas
            // accumuler les textures en memoire et de garantir un etat d'overlay propre.
            if (crossFadeRoutine != null)
            {
                StopCoroutine(crossFadeRoutine);
                crossFadeRoutine = null;
                CleanupCrossFadeResources();
            }

            // Lancement du fondu asynchrone (capture + bascule + interpolation).
            crossFadeRoutine = StartCoroutine(DisplayCameraWithCrossFade(cameraName, resolvedDuration, overrideStyle));
            return;
        }

        // Sinon (fondu desactive ou duree nulle) on applique directement la transition.
        ApplyCameraSwitch(cameraName, resolvedDuration, false, overrideStyle);
    }

    /// <summary>
    /// Active la camera placee a l'indice 0 de la liste <see cref="cameras"/>.
    /// Cette camera represente la vue de combat par defaut.
    /// </summary>
    private void ActivateDefaultCameraPriorities()
    {
        if (cameras == null || cameras.Count == 0)
            return; // aucune camera a activer

        foreach (var c in cameras)
            c.Priority = (c == cameras[0]) ? activePriority : inactivePriority;

        _current = cameras[0];
        UpdateActiveCameraStates(_current);
    }

    /// <summary>
    /// Reconstruit la liste et le dictionnaire des cameras disponibles.
    /// </summary>
    public void RebuildMap()
    {
        cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
            .Where(c => c != null).Distinct().ToList();
        _byName.Clear();
        foreach (var c in cameras)
        {
            EnsureFadeConfiguration(c);
            _byName[c.gameObject.name] = c;
        }
    }

    /// <summary>
    /// Applique immediatement le changement de camera en gerant les priorites
    /// et la configuration du <see cref="CinemachineBrain"/>.
    /// </summary>
    /// <param name="cameraName">Nom de la camera cible (ou <c>null</c>/<see cref="string.Empty"/> pour les cas speciaux).</param>
    /// <param name="blendDuration">Duree de blend desiree (utilisee si <paramref name="forceCut"/> est faux).</param>
    /// <param name="forceCut">Si vrai, impose un cut direct sans interpolation de position.</param>
    private void ApplyCameraSwitch(string cameraName, float blendDuration, bool forceCut, CinemachineBlendDefinition.Styles? overrideStyle)
    {
        float duration = Mathf.Max(0f, blendDuration >= 0f ? blendDuration : defaultBlendDuration);

        // Cas : retour a la camera par defaut (indice 0).
        if (cameraName == null)
        {
            ApplyBrainBlend(duration, forceCut, overrideStyle);
            ActivateDefaultCameraPriorities();
            return;
        }

        // Cas : aucune camera Cinemachine souhaitee -> toutes inactives.
        if (string.IsNullOrEmpty(cameraName))
        {
            foreach (var c in cameras)
                c.Priority = inactivePriority;

            _current = null;
            UpdateActiveCameraStates(null);
            return;
        }

        // Recherche de la camera a activer.
        if (!_byName.TryGetValue(cameraName, out var next))
        {
            RebuildMap();
            if (!_byName.TryGetValue(cameraName, out next))
            {
                Debug.LogWarning($"[BlendSwitcher] Aucune CinemachineCamera trouvee: {cameraName}");
                return;
            }
        }

        if (_current == next) return;

        // Definition du blend par defaut juste avant le switch.
        ApplyBrainBlend(duration, forceCut, overrideStyle);

        // Gestion des priorites : seule la camera cible obtient la priorite active.
        foreach (var c in cameras)
            c.Priority = (c == next) ? activePriority : inactivePriority;

        _current = next;
        UpdateActiveCameraStates(next);
    }

    /// <summary>
    /// Configure le <see cref="CinemachineBrain"/> pour utiliser soit un cut soit le blend habituel.
    /// </summary>
    /// <param name="blendDuration">Duree du blend si <paramref name="forceCut"/> est faux.</param>
    /// <param name="forceCut">Si vrai, force un cut instantane.</param>
    private void ApplyBrainBlend(float blendDuration, bool forceCut, CinemachineBlendDefinition.Styles? overrideStyle)
    {
        if (!brain) return; // Rien a configurer si aucun brain n'est disponible.

        // Lors d'un fondu visuel, on force un cut afin d'eviter le mouvement.
        var style = forceCut ? CinemachineBlendDefinition.Styles.Cut : (overrideStyle ?? blendStyle);
        var duration = forceCut ? 0f : Mathf.Max(0f, blendDuration);
        brain.DefaultBlend = new CinemachineBlendDefinition(style, duration);
    }

    /// <summary>
    /// Lance un fondu visuel : capture du frame courant, activation de la nouvelle camera,
    /// puis interpolation de l'opacite de l'image capturee.
    /// </summary>
    private IEnumerator DisplayCameraWithCrossFade(string cameraName, float fadeDuration, CinemachineBlendDefinition.Styles? overrideStyle)
    {
        // S'assure que l'overlay est pret. Si ce n'est pas possible, on retombe
        // sur le comportement standard afin de ne pas bloquer le changement de plan.
        if (!EnsureCrossFadeOverlay())
        {
            ApplyCameraSwitch(cameraName, fadeDuration, false, overrideStyle);
            crossFadeRoutine = null;
            yield break;
        }

        // Attend la fin du frame courant pour capturer exactement l'image visible.
        yield return new WaitForEndOfFrame();

        Texture2D screenshot = null;
        try
        {
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BlendSwitcher] Capture d'ecran impossible pour le fondu: {e.Message}");
        }

        if (screenshot == null)
        {
            // Aucune capture disponible : retour au comportement classique.
            ApplyCameraSwitch(cameraName, fadeDuration, false, overrideStyle);
            crossFadeRoutine = null;
            yield break;
        }

        // Conserve la reference pour pouvoir liberer la memoire ensuite.
        lastCapturedFrame = screenshot;

        // Active l'overlay avec l'image du plan precedent.
        crossFadeCanvas.gameObject.SetActive(true);
        crossFadeImage.texture = screenshot;
        SetCrossFadeAlpha(1f); // on commence totalement opaque

        // Bascule vers la nouvelle camera via un cut (aucun mouvement de camera).
        ApplyCameraSwitch(cameraName, fadeDuration, true, overrideStyle);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = fadeDuration > 0f ? Mathf.Clamp01(elapsed / Mathf.Max(fadeDuration, 0.0001f)) : 1f;
            float alpha = EvaluateCrossFadeAlpha(t);
            SetCrossFadeAlpha(alpha);

            yield return null;
        }

        // Termine proprement le fondu : transparence totale et nettoyage des references.
        SetCrossFadeAlpha(0f);
        crossFadeImage.texture = null;
        crossFadeCanvas.gameObject.SetActive(false);
        if (lastCapturedFrame != null)
        {
            Destroy(lastCapturedFrame);
            lastCapturedFrame = null;
        }

        crossFadeRoutine = null;
    }

    /// <summary>
    /// Calcule l'opacite a partir de la courbe definie dans l'inspecteur.
    /// </summary>
    private float EvaluateCrossFadeAlpha(float normalizedTime)
    {
        if (crossFadeCurve != null && crossFadeCurve.length >= 2)
            return Mathf.Clamp01(crossFadeCurve.Evaluate(normalizedTime));

        // Par defaut on effectue un lerp lineaire de 1 a 0.
        return Mathf.Clamp01(1f - normalizedTime);
    }

    /// <summary>
    /// Modifie l'opacite de l'image de fondu en preservant sa couleur.
    /// </summary>
    private void SetCrossFadeAlpha(float alpha)
    {
        if (!crossFadeImage) return;

        Color c = crossFadeImage.color;
        c.a = Mathf.Clamp01(alpha);
        crossFadeImage.color = c;
    }

    /// <summary>
    /// Cree (si besoin) le canvas et l'image utilises pour le fondu.
    /// Retourne vrai si les references sont valides a la fin de l'operation.
    /// </summary>
    private bool EnsureCrossFadeOverlay()
    {
        // Si le fondu est desactive, on n'a pas besoin d'overlay.
        if (!useVisualCrossFade)
            return false;

        // Recherche d'une RawImage deja referencee dans le canvas fourni.
        if (crossFadeCanvas && !crossFadeImage)
            crossFadeImage = crossFadeCanvas.GetComponentInChildren<RawImage>(true);

        // Creation automatique du canvas si aucune reference n'a ete definie.
        if (!crossFadeCanvas)
            crossFadeCanvas = CreateCrossFadeCanvas();

        // Creation automatique de l'image si necessaire.
        if (crossFadeCanvas && !crossFadeImage)
            crossFadeImage = CreateCrossFadeImage(crossFadeCanvas.transform);

        if (crossFadeCanvas)
            crossFadeCanvas.gameObject.SetActive(false); // l'overlay reste cache tant qu'il n'est pas utilise

        bool ready = crossFadeCanvas && crossFadeImage;
        if (!ready)
            Debug.LogWarning("[BlendSwitcher] Impossible de preparer l'overlay de fondu visuel.");

        return ready;
    }

    /// <summary>
    /// Cree dynamiquement un canvas destine a l'affichage du fondu visuel.
    /// </summary>
    private Canvas CreateCrossFadeCanvas()
    {
        var canvasGo = new GameObject("CinemachineCrossFadeCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvasComponent = canvasGo.AddComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComponent.sortingOrder = 5000; // ordre eleve pour rester devant l'interface

        // Ajout d'un CanvasScaler pour garantir que l'image couvre l'ecran
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Le CanvasGroup nous permet de desactiver l'interaction et le raycast.
        var group = canvasGo.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.ignoreParentGroups = true;

        return canvasComponent;
    }

    /// <summary>
    /// Cree l'image pleine ecran qui recevra le screenshot du plan precedent.
    /// </summary>
    private RawImage CreateCrossFadeImage(Transform parent)
    {
        var imageGo = new GameObject("CinemachineCrossFadeImage");
        imageGo.transform.SetParent(parent, false);

        var rawImage = imageGo.AddComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.color = new Color(1f, 1f, 1f, 0f);

        // L'image occupe tout l'ecran.
        var rect = rawImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rawImage;
    }

    /// <summary>
    /// Determine si la camera cible prefere l'utilisation d'un fondu visuel.
    /// </summary>
    private bool ShouldUseCrossFade(string cameraName, float blendDuration)
    {
        // Aucun fondu possible si l'option globale est desactivee ou si la duree est nulle.
        if (!useVisualCrossFade || blendDuration <= 0f)
            return false;

        var targetCamera = ResolveTargetCamera(cameraName);
        if (!targetCamera)
            return false;

        // Le composant est garanti present via EnsureFadeConfiguration, on peut donc l'utiliser.
        var configuration = EnsureFadeConfiguration(targetCamera);
        return configuration != null && configuration.fadeType == FadeType.Fade;
    }

    /// <summary>
    /// Retrouve la camera visee par <paramref name="cameraName"/> sans modifier les priorites.
    /// </summary>
    private CinemachineCamera ResolveTargetCamera(string cameraName)
    {
        if (cameras == null || cameras.Count == 0)
            return null;

        // Un nom nul designe la camera par defaut placee a l'indice 0.
        if (cameraName == null)
            return cameras[0];

        // Une chaine vide signifie qu'aucune camera Cinemachine n'est desiree.
        if (string.IsNullOrEmpty(cameraName))
            return null;

        if (!_byName.TryGetValue(cameraName, out var target))
        {
            // Rebat l'inventaire en cas de desynchronisation (cameras ajoutees ou renommees).
            RebuildMap();
            _byName.TryGetValue(cameraName, out target);
        }

        return target;
    }

    /// <summary>
    /// Ajoute le composant <see cref="CinemachineFadeConfiguration"/> si la camera n'en possede pas.
    /// </summary>
    private CinemachineFadeConfiguration EnsureFadeConfiguration(CinemachineCamera camera)
    {
        if (!camera)
            return null;

        var configuration = camera.GetComponent<CinemachineFadeConfiguration>();
        if (!configuration)
        {
            // Ajout automatique afin que toutes les cameras respectent la convention imposee.
            configuration = camera.gameObject.AddComponent<CinemachineFadeConfiguration>();
        }

        return configuration;
    }

    /// <summary>
    /// Active uniquement la camera souhaitee et coupe toutes les autres afin d'eviter
    /// qu'elles continuent a consommer des calculs alors qu'elles ne sont pas visibles.
    /// </summary>
    /// <param name="activeCamera">Camera qui doit rester active (ou <c>null</c> pour les desactiver toutes).</param>
    private void UpdateActiveCameraStates(CinemachineCamera activeCamera)
    {
        if (cameras == null)
            return;

        foreach (var cam in cameras)
        {
            if (!cam)
                continue;

            bool shouldBeActive = cam == activeCamera;
            if (cam.gameObject.activeSelf != shouldBeActive)
                cam.gameObject.SetActive(shouldBeActive);
        }
    }

    /// <summary>
    /// Nettoie les ressources utilisees par le fondu (texture temporaire et overlay).
    /// </summary>
    private void CleanupCrossFadeResources()
    {
        if (lastCapturedFrame != null)
        {
            Destroy(lastCapturedFrame);
            lastCapturedFrame = null;
        }

        if (crossFadeImage)
        {
            crossFadeImage.texture = null;
            SetCrossFadeAlpha(0f);
        }

        if (crossFadeCanvas)
            crossFadeCanvas.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        // Evite les fuites memoire si l'objet est detruit pendant un fondu.
        if (crossFadeRoutine != null)
        {
            StopCoroutine(crossFadeRoutine);
            crossFadeRoutine = null;
        }

        CleanupCrossFadeResources();
    }

    /// <summary>
    /// Retourne le nom de la camera actuellement prioritaire (<c>null</c> si aucune n'est active).
    /// </summary>
    public string CurrentCameraName => _current ? _current.gameObject.name : null;

    /// <summary>
    /// Indique si une camera Cinemachine dispose actuellement de la priorite.
    /// </summary>
    public bool HasActiveCamera => _current != null;

    /// <summary>
    /// Fournit l'instance de la <see cref="CinemachineCamera"/> actuellement prioritaire.
    /// </summary>
    public CinemachineCamera CurrentCamera => _current;
}
