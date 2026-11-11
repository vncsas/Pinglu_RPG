using UnityEngine;

// Luz de fogo / lanterna que treme de leve (ruido), para fogueira e postes.
[RequireComponent(typeof(Light))]
public class FogoLuz : MonoBehaviour
{
    public float forca = 0.25f, velocidade = 7f;
    Light luz; float baseInt; float semente;
    void OnEnable() { luz = GetComponent<Light>(); baseInt = luz.intensity; semente = Random.value * 100f; }
    void Update()
    {
        float n = Mathf.PerlinNoise(Time.time * velocidade, semente) * 0.6f + Mathf.PerlinNoise(Time.time * velocidade * 2.7f, semente + 9f) * 0.4f;
        luz.intensity = baseInt * (1f + (n - 0.5f) * 2f * forca);
    }
}
