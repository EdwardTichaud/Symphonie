using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère l'effet de verre brisé entre la WorldView et la BattleView.
/// </summary>
public class GlassShatterTransition : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Gestionnaire de texture de la MainCamera")]
    public MainCameraTextureManager cameraTextureManager;
    [Tooltip("Animator contenant l'animation de bris de verre")]
    public Animator shatterAnimator;
    [Tooltip("Image affichant la texture de la WorldView à briser (optionnel)")]
    public RawImage overlayImage;
    [Tooltip("Renderer affichant la texture de la WorldView à briser (optionnel)")]
    public Renderer overlayRenderer;
    [Tooltip("Source audio pour le son de bris")]
    public AudioSource audioSource;
    [Tooltip("Clip audio du bris de verre")]
    public AudioClip shatterClip;

    [Header("Paramètres")]
    [Tooltip("Durée de l'arrêt sur image avant l'animation de fissures")]
    public float freezeDuration = 0.3f;
    [Tooltip("Nom du trigger dans l'Animator pour lancer l'animation")]
    public string animatorTrigger = "Shatter";

    /// <summary>
    /// Lance l'effet de transition et attend sa fin.
    /// </summary>
    public IEnumerator Play()
    {
        yield return StartCoroutine(ShatterRoutine());
    }

    private IEnumerator ShatterRoutine()
    {
        // On applique la texture de la WorldView sur l'overlay avant de geler l'image
        ApplyWorldTexture();

        // Arrêt sur image
        float previousScale = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(freezeDuration);

        // Lancement de l'animation de fissures/bris
        if (shatterAnimator != null)
            shatterAnimator.SetTrigger(animatorTrigger);

        if (audioSource != null && shatterClip != null)
            audioSource.PlayOneShot(shatterClip);

        // Attente de la fin de l'animation principale si possible
        if (shatterAnimator != null)
        {
            AnimatorStateInfo state = shatterAnimator.GetCurrentAnimatorStateInfo(0);
            yield return new WaitForSecondsRealtime(state.length);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // On affiche la BattleView derrière les morceaux de verre
        if (cameraTextureManager != null)
            cameraTextureManager.ShowBattleView();

        // Reprise du temps normal
        Time.timeScale = previousScale;
    }

    private void ApplyWorldTexture()
    {
        if (cameraTextureManager == null)
            return;

        RenderTexture worldTex = cameraTextureManager.WorldTexture;
        if (overlayImage != null)
            overlayImage.texture = worldTex;
        else if (overlayRenderer != null)
            overlayRenderer.material.mainTexture = worldTex;
    }
}
