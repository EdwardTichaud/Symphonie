using UnityEngine;

/// <summary>
/// Fait bouger un GameObject en aller-retour horizontal et/ou vertical.
/// </summary>
/// 
[ExecuteAlways]
public class PingPongMover : MonoBehaviour
{
    [Header("Axes de mouvement")]
    public bool moveHorizontal = true;
    public bool moveVertical = false;

    [Header("Paramètres de mouvement horizontal")]
    public float horizontalAmplitude = 2f; // distance max depuis la position initiale
    public float horizontalSpeed = 2f;

    [Header("Paramètres de mouvement vertical")]
    public float verticalAmplitude = 2f;
    public float verticalSpeed = 2f;

    [Header("Options")]
    public bool localSpace = false; // si true → mouvement relatif au parent
    public bool startWithRandomOffset = false; // permet de désynchroniser plusieurs objets

    private Vector3 startPosition;
    private float timeOffset;

    void Start()
    {
        startPosition = localSpace ? transform.localPosition : transform.position;
        timeOffset = startWithRandomOffset ? Random.Range(0f, 10f) : 0f;
    }

    void Update()
    {
        float t = Time.time + timeOffset;

        float x = moveHorizontal ? Mathf.PingPong(t * horizontalSpeed, horizontalAmplitude * 2f) - horizontalAmplitude : 0f;
        float y = moveVertical ? Mathf.PingPong(t * verticalSpeed, verticalAmplitude * 2f) - verticalAmplitude : 0f;

        Vector3 newPos = startPosition + new Vector3(x, y, 0f);

        if (localSpace)
            transform.localPosition = newPos;
        else
            transform.position = newPos;
    }
}
