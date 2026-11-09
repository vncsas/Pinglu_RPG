using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Animal de ambientação (vaca etc.): fica parado, anda devagar e come, sempre perto de onde nasceu.
// Só enfeita o mapa: não é rede, não ataca e não tem vida. Longe da câmera fica desligado (não pesa).
public class AnimalAmbiente : MonoBehaviour
{
    public AnimationClip parado, andando, comendo;
    public float raio = 18f;
    public float velocidade = 1.1f;
    public float distanciaParaAnimar = 140f;

    private enum Estado { Parado, Andando, Comendo }
    private Estado estado = Estado.Parado;
    private float fimDoEstado;
    private Vector3 casa, destino;
    private Animator animador;
    private PlayableGraph grafo;
    private AnimationMixerPlayable mixer;
    private readonly float[] pesos = new float[3];
    private bool temGrafo, ativo = true;
    private Terrain terreno;
    private Camera cam;
    private float proximaChecagem;

    private void Start()
    {
        casa = transform.position; destino = casa;
        terreno = Terrain.activeTerrain;
        animador = GetComponentInChildren<Animator>();
        if (animador == null) animador = gameObject.AddComponent<Animator>();
        animador.applyRootMotion = false;
        animador.cullingMode = AnimatorCullingMode.CullCompletely;
        AnimationClip[] c = { parado, andando, comendo };
        if (parado != null)
        {
            grafo = PlayableGraph.Create("Animal");
            grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(grafo, 3);
            for (int i = 0; i < 3; i++)
            {
                AnimationClip clip = c[i] != null ? c[i] : parado;
                AnimationClipPlayable p = AnimationClipPlayable.Create(grafo, clip);
                p.SetTime(Random.value * Mathf.Max(0.1f, clip.length));
                grafo.Connect(p, 0, mixer, i);
            }
            AnimationPlayableOutput.Create(grafo, "Saida", animador).SetSourcePlayable(mixer);
            grafo.Play();
            temGrafo = true;
        }
        pesos[0] = 1f; Aplicar();
        fimDoEstado = Time.time + Random.Range(1f, 6f);
    }

    private void OnDestroy() { if (temGrafo && grafo.IsValid()) grafo.Destroy(); }

    private void Aplicar() { if (temGrafo && mixer.IsValid()) for (int i = 0; i < 3; i++) mixer.SetInputWeight(i, pesos[i]); }

    private void Update()
    {
        if (Time.time > proximaChecagem)
        {
            proximaChecagem = Time.time + 1f;
            if (cam == null) cam = Camera.main;
            bool perto = cam == null || (cam.transform.position - transform.position).sqrMagnitude < distanciaParaAnimar * distanciaParaAnimar;
            if (perto != ativo)
            {
                ativo = perto;
                if (temGrafo) { if (ativo) grafo.Play(); else grafo.Stop(); }
            }
        }
        if (!ativo) return;

        float dt = Time.deltaTime;
        if (Time.time >= fimDoEstado)
        {
            float r = Random.value;
            if (r < 0.35f)
            {
                estado = Estado.Andando;
                Vector2 o = Random.insideUnitCircle * raio;
                destino = casa + new Vector3(o.x, 0f, o.y);
                fimDoEstado = Time.time + 25f;
            }
            else if (r < 0.75f) { estado = Estado.Comendo; fimDoEstado = Time.time + Random.Range(6f, 14f); }
            else { estado = Estado.Parado; fimDoEstado = Time.time + Random.Range(3f, 8f); }
        }
        if (estado == Estado.Andando)
        {
            Vector3 d = destino - transform.position; d.y = 0f;
            if (d.magnitude < 0.8f) { estado = Estado.Parado; fimDoEstado = Time.time + Random.Range(2f, 6f); }
            else
            {
                Quaternion alvo = Quaternion.LookRotation(d.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, 90f * dt);
                transform.position += transform.forward * velocidade * dt;
            }
        }
        if (terreno != null)
        {
            Vector3 p = transform.position;
            p.y = terreno.SampleHeight(p) + terreno.transform.position.y;
            transform.position = p;
        }
        float k = 1f - Mathf.Exp(-5f * dt);
        for (int i = 0; i < 3; i++) pesos[i] = Mathf.Lerp(pesos[i], (int)estado == i ? 1f : 0f, k);
        Aplicar();
    }
}
