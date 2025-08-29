using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem; // Nécessaire pour manipuler les InputAction du joueur
using System.Collections; // Requis pour l'utilisation des coroutines

public class TimelineManager : MonoBehaviour
{
    public static TimelineManager Instance { get; private set; }

    /// <summary>
    /// Référence de la Timeline en cours.
    /// </summary>
    private PlayableDirector currentDirector;

    /// <summary>
    /// Indique si une Timeline est en train de jouer.
    /// </summary>
    public bool IsTimelinePlaying { get; private set; }

    /// <summary>
    /// Durée nécessaire de maintien de l'input <c>Cancel</c> pour passer la Timeline.
    /// Configurable dans l'inspecteur pour s'adapter facilement aux besoins de gameplay.
    /// </summary>
    [SerializeField]
    private float skipHoldDuration = 3f;

    /// <summary>
    /// Référence locale vers l'action <c>Cancel</c> afin de pouvoir l'activer
    /// même lorsque toute la map <c>World</c> est désactivée.
    /// </summary>
    private InputAction cancelAction;

    /// <summary>
    /// Coroutine utilisée pour attendre le maintien continu de l'input Cancel.
    /// Si elle arrive à son terme, la Timeline est interrompue immédiatement.
    /// </summary>
    private Coroutine skipCoroutine;

    /// <summary>
    /// Active ou désactive l'ensemble des entrées de la map <c>World</c> pendant
    /// l'exécution d'une Timeline. Cela garantit qu'aucune action du joueur ne
    /// vient perturber une cinématique en cours.
    /// </summary>
    /// <param name="enable">True pour réactiver les inputs, false pour les bloquer.</param>
    private void ToggleWorldInputs(bool enable)
    {
        // Vérifie que l'InputsManager et la map World existent avant de manipuler les actions.
        if (InputsManager.Instance == null) return;

        // Récupère la map complète afin de pouvoir l'activer ou la désactiver en un seul appel.
        var worldMap = InputsManager.Instance.playerInputs.World;

        if (enable)
        {
            // Réactivation globale de toutes les actions World une fois la Timeline terminée.
            worldMap.Enable();
        }
        else
        {
            // Désactive immédiatement toutes les actions World pour empêcher toute interaction.
            worldMap.Disable();
        }
    }

    /// <summary>
    /// Active l'écoute de l'input <c>Cancel</c> pour permettre au joueur de passer
    /// la cinématique en maintenant le bouton pendant une durée définie.
    /// </summary>
    private void EnableTimelineSkip()
    {
        // Sécurise l'accès au singleton et à l'action Cancel
        if (InputsManager.Instance == null) return;

        cancelAction = InputsManager.Instance.playerInputs.World.Cancel;

        // On s'assure que seule l'action Cancel soit active : même si la map
        // World est désactivée, l'action individuelle peut être réactivée.
        cancelAction.started += OnCancelStarted;
        cancelAction.canceled += OnCancelCanceled;
        cancelAction.Enable();
    }

    /// <summary>
    /// Désactive l'écoute de l'input Cancel et arrête la coroutine éventuelle.
    /// Appelé à la fin de la Timeline ou si elle est stoppée prématurément.
    /// </summary>
    private void DisableTimelineSkip()
    {
        if (cancelAction == null) return;

        cancelAction.started -= OnCancelStarted;
        cancelAction.canceled -= OnCancelCanceled;
        cancelAction.Disable();
        cancelAction = null;

        if (skipCoroutine != null)
        {
            StopCoroutine(skipCoroutine);
            skipCoroutine = null;
        }
    }

    /// <summary>
    /// Déclenché lors de l'appui initial sur Cancel. Lance une coroutine
    /// qui attend la durée complète de maintien pour passer la Timeline.
    /// </summary>
    private void OnCancelStarted(InputAction.CallbackContext ctx)
    {
        // Si une attente était déjà en cours, on la relance proprement.
        if (skipCoroutine != null)
            StopCoroutine(skipCoroutine);

        skipCoroutine = StartCoroutine(SkipTimelineAfterHold());
    }

    /// <summary>
    /// Appelé lorsque l'utilisateur relâche l'input Cancel avant la fin de
    /// la durée requise. On annule donc le passage de la timeline.
    /// </summary>
    private void OnCancelCanceled(InputAction.CallbackContext ctx)
    {
        if (skipCoroutine != null)
        {
            StopCoroutine(skipCoroutine);
            skipCoroutine = null;
        }
    }

    /// <summary>
    /// Coroutine qui attend <see cref="skipHoldDuration"/> secondes.
    /// Si l'attente se termine, la Timeline est accélérée afin de se terminer en
    /// une seule frame tout en laissant s'exécuter les événements restants.
    /// </summary>
    private IEnumerator SkipTimelineAfterHold()
    {
        yield return new WaitForSeconds(skipHoldDuration);

        // Lance la lecture ultra-rapide de la Timeline plutôt qu'un arrêt brutal.
        // Cela garantit que tous les signaux et animations prévus soient exécutés.
        StartCoroutine(FastForwardTimeline());

        // On n'écoute plus l'input Cancel une fois le passage déclenché.
        DisableTimelineSkip();

        // La coroutine de maintien est terminée : on libère la référence.
        skipCoroutine = null;
    }

    /// <summary>
    /// Accélère fortement la Timeline courante pour qu'elle atteigne sa fin en un
    /// clin d'œil tout en jouant ses derniers événements.
    /// </summary>
    private IEnumerator FastForwardTimeline()
    {
        if (currentDirector == null)
            yield break; // Aucun directeur, rien à accélérer

        // Récupère le Playable racine pour manipuler sa vitesse.
        var rootPlayable = currentDirector.playableGraph.GetRootPlayable(0);
        double originalSpeed = rootPlayable.GetSpeed();

        // Vitesse très élevée : la Timeline se termine généralement en une frame.
        rootPlayable.SetSpeed(1000f);

        // Attend que la Timeline signale sa fin.
        while (currentDirector.state == PlayState.Playing)
            yield return null;

        // Restaure la vitesse par sécurité pour les prochaines timelines.
        rootPlayable.SetSpeed(originalSpeed);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Joue une nouvelle Timeline. Arrête proprement la précédente.
    /// </summary>
    public void PlayTimeline(PlayableDirector newDirector)
    {
        if (newDirector == null)
        {
            Debug.LogWarning("[TimelineManager] PlayTimeline appelé avec null !");
            return;
        }

        // Arrête la timeline en cours si elle est différente
        if (currentDirector != null && currentDirector != newDirector && currentDirector.state == PlayState.Playing)
        {
            Debug.Log("[TimelineManager] Arrêt de la Timeline en cours avant de jouer la nouvelle.");
            currentDirector.Stop();
        }

        // Abonnement aux events
        newDirector.played -= OnPlayed;
        newDirector.stopped -= OnStopped;
        newDirector.played += OnPlayed;
        newDirector.stopped += OnStopped;

        currentDirector = newDirector;
        currentDirector.Play();
    }

    /// <summary>
    /// Arrête explicitement la Timeline en cours.
    /// </summary>
    public void StopCurrentTimeline()
    {
        if (currentDirector != null && currentDirector.state == PlayState.Playing)
        {
            currentDirector.Stop();
        }
    }

    /// <summary>
    /// Met en pause la Timeline en cours (appelé par un Signal).
    /// </summary>
    public void PauseCurrentTimeline()
    {
        if (currentDirector == null) return;
        if (currentDirector.state != PlayState.Playing) return;

        currentDirector.Pause();
        // Stabilise l'état visuel/audio au frame exact de pause.
        currentDirector.Evaluate();

        // On reste en mode "cinématique" pendant la pause :
        // => on NE réactive PAS les déplacements ici.
        // (si tu veux autoriser un prompt/QTE, gère-le dans ton UI/Input dédié)
        Debug.Log("[TimelineManager] PauseCurrentTimeline()");
    }

    /// <summary>
    /// Reprend la Timeline en cours (appelé par une action joueur / QTE / autre).
    /// </summary>
    public void ResumeCurrentTimeline()
    {
        if (currentDirector == null) return;
        if (currentDirector.state != PlayState.Paused) return;

        currentDirector.Resume();
        Debug.Log("[TimelineManager] ResumeCurrentTimeline()");
    }

    /// <summary>
    /// Callback quand une Timeline démarre.
    /// </summary>
    private void OnPlayed(PlayableDirector pd)
    {
        IsTimelinePlaying = true;
        // Bloque immédiatement toutes les actions "World" pendant l'exécution de la timeline.
        ToggleWorldInputs(false);
        // Réactive uniquement l'action Cancel pour permettre un éventuel passage de la Timeline.
        EnableTimelineSkip();
        // Désactive le CameraController pour laisser la Timeline contrôler totalement la caméra
        if (CameraController.Instance != null)
            CameraController.Instance.enabled = false;
        Debug.Log($"[TimelineManager] Timeline jouée : {pd.name}");

        // Lance un fondu noir pour encadrer le début de la timeline
        StartCoroutine(FadeBlackRoutine());
    }

    /// <summary>
    /// Callback quand une Timeline s'arrête.
    /// </summary>
    private void OnStopped(PlayableDirector pd)
    {
        if (currentDirector == pd)
        {
            // On délègue la fin de timeline à une coroutine afin de chaîner le fondu
            StartCoroutine(HandleTimelineEnd(pd));
        }
    }

    /// <summary>
    /// Gère la séquence de fin de timeline avec fondu noir, masquage des repositionnements
    /// puis restitution progressive des commandes au joueur.
    /// </summary>
    private IEnumerator HandleTimelineEnd(PlayableDirector pd)
    {
        Debug.Log($"[TimelineManager] Timeline stoppée : {pd.name}");
        // On n'a plus besoin d'écouter l'input Cancel une fois la cinématique terminée.
        DisableTimelineSkip();


        // 1) Fondu vers le noir pour cacher le "snap" de fin de Timeline et rester en noir.
        //    Pendant cette étape, l'écran devient totalement noir puis y reste tant que
        //    l'on n'a pas relancé la séquence de retour.
        yield return FadeBlackRoutine(true);

        // 2) La cinématique est terminée : on redonne le contrôle de Lucian en réactivant les inputs "World".
        IsTimelinePlaying = false;
        currentDirector = null;
        ToggleWorldInputs(true);

        // 3) Réactivation du CameraController pour rendre la main après la cinématique
        if (CameraController.Instance != null)
            CameraController.Instance.enabled = true;

        // 4) Une fois tout rétabli en arrière-plan, on peut revenir progressivement à l'image.
        yield return FadeFromBlackRoutine();
    }

    /// <summary>
    /// Sequence de fondu : transparent -> noir (1s), maintien (1s) puis optionnellement noir -> transparent (1s).
    /// Utilise le <see cref="FadeChildrenOpacity"/> dont l'enfant 0 doit être un panneau noir couvrant l'écran.
    /// </summary>
    /// <param name="stayBlack">
    ///     True pour rester sur un écran noir à la fin du fondu (utile en fin de timeline),
    ///     False pour revenir à l'image en fin de séquence.
    /// </param>
    private IEnumerator FadeBlackRoutine(bool stayBlack = false)
    {
        var fader = FadeChildrenOpacity.Instance;
        if (fader == null)
            yield break; // Aucun fader disponible, on quitte silencieusement

        // 1) transparent vers noir
        fader.ChangeOpacity(0, 1f, 1f);
        yield return new WaitForSeconds(1f);

        // 2) pause d'une seconde à opacité maximale
        yield return new WaitForSeconds(1f);

        // 3) noir vers transparent (sauf si on veut rester en noir)
        if (!stayBlack)
        {
            fader.ChangeOpacity(0, 0f, 1f);
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// Fait revenir l'écran du noir vers la transparence en 1 seconde.
    /// Utilisé après <see cref="FadeBlackRoutine(bool)"/> lorsque <c>stayBlack</c> est vrai.
    /// </summary>
    private IEnumerator FadeFromBlackRoutine()
    {
        var fader = FadeChildrenOpacity.Instance;
        if (fader == null)
            yield break;

        fader.ChangeOpacity(0, 0f, 1f);
        yield return new WaitForSeconds(1f);
    }
}
