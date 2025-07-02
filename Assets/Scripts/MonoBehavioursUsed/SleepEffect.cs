using System.Collections;
using UnityEngine;

/// <summary>
/// Gère un effet d'endormissement en faisant apparaître des lettres "Z" qui montent avec un léger bruit.
/// </summary>
public class SleepEffect : MonoBehaviour
{
    public Font textFont;
    public Color textColor = Color.white;
    public float letterSpawnInterval = 0.2f;
    public int letterCount = 5;
    public Vector3 spawnOffset = Vector3.zero;

    private void Start()
    {
        StartCoroutine(SpawnLetters());
    }

    private IEnumerator SpawnLetters()
    {
        for (int i = 0; i < letterCount; i++)
        {
            SpawnLetter(i % 2 == 0 ? "Z" : "z");
            yield return new WaitForSeconds(letterSpawnInterval);
        }
    }

    private void SpawnLetter(string letter)
    {
        GameObject go = new GameObject("SleepLetter");
        go.transform.SetParent(transform);
        go.transform.localPosition = spawnOffset;

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = letter;
        tm.font = textFont;
        tm.color = textColor;

        StartCoroutine(MoveAndDestroy(go));
    }

    private IEnumerator MoveAndDestroy(GameObject go)
    {
        float duration = 1.5f;
        float time = 0f;
        Vector3 startPos = go.transform.localPosition;
        while (time < duration)
        {
            time += Time.deltaTime;
            float progress = time / duration;
            float noiseX = Mathf.PerlinNoise(Time.time, progress) - 0.5f;
            go.transform.localPosition = startPos + new Vector3(noiseX, progress * 2f, 0f);
            yield return null;
        }
        Destroy(go);
    }
}
