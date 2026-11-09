using UnityEngine;

// Poste de rua: a luz só fica ligada perto do jogador (muitos postes não pesam) e treme de leve, como uma chama dentro da lanterna.
public class PosteDeLuz : MonoBehaviour
{
    public Light luz;
    public float distanciaLigada = 55f;
    public float intensidadeBase = 3f;
    public float tremor = 0.08f;

    private float semente, proximaChecagem;
    private Camera cam;
    private bool ligada = true;

    private void Start()
    {
        if (luz == null) luz = GetComponentInChildren<Light>();
        semente = Random.value * 100f;
        proximaChecagem = Random.value;
    }

    private void Update()
    {
        if (luz == null) return;
        if (Time.time >= proximaChecagem)
        {
            proximaChecagem = Time.time + 0.6f;
            if (cam == null) cam = Camera.main;
            ligada = cam == null || (cam.transform.position - transform.position).sqrMagnitude < distanciaLigada * distanciaLigada;
            if (luz.enabled != ligada) luz.enabled = ligada;
        }
        if (ligada) luz.intensity = intensidadeBase * (1f + (Mathf.PerlinNoise(Time.time * 2.2f, semente) - 0.5f) * 2f * tremor);
    }
}
