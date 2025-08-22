using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public class TimelinePauseResumeOnInteract : MonoBehaviour
{
    [Tooltip("Instance de PlayerInputs. Si vide, on prendra celle de InputsManager, sinon on en créera une locale.")]
    [SerializeField] private PlayerInputs playerInputs;

    private PlayableDirector director;
    private bool ownsLocalInputs = false; // vrai si on a créé notre propre instance locale

    private void Awake()
    {
        director = GetComponent<PlayableDirector>();

        // Si rien n'est référencé dans l'inspecteur, on tente d'utiliser l'instance globale
        if (playerInputs == null)
        {
            if (InputsManager.Instance != null && InputsManager.Instance.playerInputs != null)
            {
                playerInputs = InputsManager.Instance.playerInputs; // on réutilise celle du jeu
                ownsLocalInputs = false;
            }
            else
            {
                playerInputs = new PlayerInputs(); // on crée une instance locale
                ownsLocalInputs = true;
            }
        }
        else
        {
            // Une instance a été assignée manuellement dans l'inspecteur
            // On considère qu'elle est "locale" à ce composant.
            ownsLocalInputs = true;
        }
    }

    private void OnEnable()
    {
        if (playerInputs == null)
        {
            Debug.LogWarning("[TimelinePauseResumeOnInteract] PlayerInputs introuvable.");
            return;
        }

        // On s'assure que la map World est active pour recevoir l'input Interact
        playerInputs.World.Enable();

        // Abonnement à l'input Interact
        playerInputs.World.Interact.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (playerInputs != null)
        {
            // Désabonnement propre
            playerInputs.World.Interact.performed -= OnInteract;

            // Si on possède une instance locale, on peut la désactiver proprement
            if (ownsLocalInputs)
            {
                playerInputs.World.Disable();
            }
            // Si l'instance vient d'InputsManager, on ne touche pas à son lifecycle
        }
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (director == null || TimelineManager.Instance == null)
            return;

        // Bascule : si Paused -> Resume, si Playing -> Pause (sinon, ne rien faire)
        if (director.state == PlayState.Paused)
        {
            TimelineManager.Instance.ResumeCurrentTimeline();
        }
        else if (director.state == PlayState.Playing)
        {
            TimelineManager.Instance.PauseCurrentTimeline();
        }
    }
}
