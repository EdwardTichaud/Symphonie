using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Gère l'introduction de la caméra sur l'écran titre puis
/// informe le <see cref="MainMenuManager"/> lorsque celle-ci est terminée.
/// </summary>
public class MainMenuIntro : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("PlayableDirector jouant l'introduction de caméra. Laisser vide pour utiliser uniquement la durée.")]
    public PlayableDirector introDirector; // Timeline optionnelle de l'intro
    [Tooltip("Gestionnaire du menu principal à activer à la fin de l'intro.")]
    public MainMenuManager menuManager;

    [Header("Paramètres")]
    [Tooltip("Durée de l'introduction si aucun PlayableDirector n'est fourni.")]
    public float introDuration = 3f;

    private void Awake()
    {
        // Assure que le menu principal reste caché dès le chargement de la scène
        // pour éviter qu'il n'apparaisse avant la fin de l'intro de caméra.
        if (menuManager != null)
        {
            menuManager.HideAllUI();
        }
    }

    private void Start()
    {
        // Si un PlayableDirector est disponible, on attend sa fin
        if (introDirector != null)
        {
            introDirector.stopped += OnIntroStopped;
        }
        else
        {
            // Sinon, on utilise un simple délai correspondant à la durée de l'intro
            StartCoroutine(WaitAndActivate());
        }
    }

    private void OnDestroy()
    {
        if (introDirector != null)
        {
            introDirector.stopped -= OnIntroStopped;
        }
    }

    // Coroutine déclenchée lorsque l'on ne dispose pas d'un PlayableDirector
    private IEnumerator WaitAndActivate()
    {
        yield return new WaitForSecondsRealtime(introDuration); // Attente en temps réel pour ignorer les variations de timeScale.
        NotifyEnd();
    }

    // Appelée lorsque le PlayableDirector termine la lecture de la timeline
    private void OnIntroStopped(PlayableDirector _)
    {
        NotifyEnd();
    }

    // Active l'affichage du menu en fin d'intro
    private void NotifyEnd()
    {
        if (menuManager != null)
        {
            menuManager.OnIntroFinished();
        }
        else
        {
            Debug.LogWarning("[MainMenuIntro] MainMenuManager introuvable, impossible d'activer le menu.");
        }
    }
}
