using UnityEngine;

public class RealTimeFPS : MonoBehaviour
{
    // Durée de la fenêtre pour la moyenne (en secondes)
    public float refreshRate = 0.5f;

    private int framesCount = 0;
    private float timePassed = 0f;
    private float fps = 0f;

    void Update()
    {
        framesCount++;
        timePassed += Time.unscaledDeltaTime;

        if (timePassed >= refreshRate)
        {
            fps = framesCount / timePassed;
            framesCount = 0;
            timePassed = 0f;
        }
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(10, 10, 200, 30), $"FPS : {fps:F1}", style);
    }
}
