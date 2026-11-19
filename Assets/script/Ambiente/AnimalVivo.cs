using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Animal de pasto com esqueleto (ovelha, cavalo): fica parado, anda devagar pelo pasto, abaixa a cabeça para comer
// (entrar -> mastigar -> levantar) e, se alguém chega perto ou há monstros por perto, sai trotando ou galopando.
// A velocidade da animação acompanha a velocidade do corpo (sem "patinar") e o corpo inclina com o relevo.
// Só enfeita o mapa: não é rede, não ataca e não tem vida. Longe da câmera fica desligado (não pesa).
public class AnimalVivo : MonoBehaviour
{
    [Header("Animações (o menu Pinglu > 75 preenche)")]
    public AnimationClip parado, andando, trote, galope, comerEntrar, comer, comerSair;

    [Header("Movimento")]
    public float raio = 12f;                   // até onde se afasta de onde nasceu
    public float velAndar = 0.8f, velTrote = 2.4f, velGalope = 5.2f;
    [Tooltip("Velocidade (m/s) em que cada ciclo de pernas não patina; a animação acelera/desacelera em volta disso")]
    public float passoDoAndar = 0.9f, passoDoTrote = 2.6f, passoDoGalope = 6.0f;
    public bool fugirGalopando = true;

    [Header("Susto")]
    public float distanciaDeSusto = 8f;        // gente por perto
    public float distanciaDeMonstro = 22f;     // monstros por perto
    public float distanciaParaAnimar = 130f;

    enum Est { Parado, Andando, ComerEntrando, Comendo, ComerSaindo, Fugindo }
    Est est = Est.Parado;
    float fimDoEstado, inicioFuga;
    Vector3 casa, destino, dirFuga;
    Animator animador; PlayableGraph grafo; AnimationMixerPlayable mixer; AnimationClipPlayable[] clipes = new AnimationClipPlayable[7];
    readonly float[] peso = new float[7], alvoPeso = new float[7];
    bool temGrafo, ativo = true;
    Terrain terreno; Camera cam; float proximaChecagem, proximoMedo, ultimoMedo;
    float pitch, yaw;

    static readonly List<Transform> gente = new List<Transform>(), monstros = new List<Transform>();
    static float proximaBusca = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { gente.Clear(); monstros.Clear(); proximaBusca = -1f; }

    void Start()
    {
        casa = transform.position; destino = casa; yaw = transform.eulerAngles.y;
        terreno = Terrain.activeTerrain;
        animador = GetComponentInChildren<Animator>();
        if (animador == null) animador = gameObject.AddComponent<Animator>();
        animador.applyRootMotion = false; animador.cullingMode = AnimatorCullingMode.CullCompletely;
        if (parado != null)
        {
            grafo = PlayableGraph.Create("AnimalVivo");
            grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(grafo, 7);
            AnimationClip[] c = { parado, andando, trote, galope, comerEntrar, comer, comerSair };
            for (int i = 0; i < 7; i++)
            {
                AnimationClip clip = c[i];
                if (clip == null) clip = i == 3 ? (trote != null ? trote : andando) : (i == 2 ? andando : null);       // sem galope usa o trote, sem trote usa o andar
                if (clip == null) clip = parado;
                clipes[i] = AnimationClipPlayable.Create(grafo, clip);
                clipes[i].SetTime(Random.value * Mathf.Max(0.1f, clip.length));
                grafo.Connect(clipes[i], 0, mixer, i);
            }
            AnimationPlayableOutput.Create(grafo, "Saida", animador).SetSourcePlayable(mixer);
            grafo.Play(); temGrafo = true;
        }
        alvoPeso[0] = peso[0] = 1f; Aplicar();
        fimDoEstado = Time.time + Random.Range(0.5f, 6f);
    }

    void OnDestroy() { if (temGrafo && grafo.IsValid()) grafo.Destroy(); }

    void Aplicar() { if (temGrafo && mixer.IsValid()) for (int i = 0; i < 7; i++) mixer.SetInputWeight(i, peso[i]); }

    void Alvo(int i) { for (int k = 0; k < 7; k++) alvoPeso[k] = k == i ? 1f : 0f; }

    // toca um clipe que não repete (entrar/sair da posição de comer) do começo
    void Reiniciar(int i) { if (temGrafo) { clipes[i].SetTime(0); clipes[i].SetSpeed(1); } }

    void Update()
    {
        if (Time.time > proximaChecagem)
        {
            proximaChecagem = Time.time + 1f;
            if (cam == null) cam = Camera.main;
            bool perto = cam == null || (cam.transform.position - transform.position).sqrMagnitude < distanciaParaAnimar * distanciaParaAnimar;
            if (perto != ativo) { ativo = perto; if (temGrafo) { if (ativo) grafo.Play(); else grafo.Stop(); } }
        }
        if (!ativo) return;
        float dt = Time.deltaTime;
        Susto();

        switch (est)
        {
            case Est.Parado:
                Alvo(0);
                if (Time.time >= fimDoEstado) Decidir();
                break;
            case Est.Andando:
                Alvo(1); Ritmo(1, velAndar, passoDoAndar);
                if (Mover(destino, velAndar, 0.6f) || Time.time >= fimDoEstado) Parar(Random.Range(2f, 7f));
                break;
            case Est.ComerEntrando:
                Alvo(4);
                if (Time.time >= fimDoEstado) { est = Est.Comendo; fimDoEstado = Time.time + Random.Range(5f, 12f); }
                break;
            case Est.Comendo:
                Alvo(5);
                if (Time.time >= fimDoEstado) { est = Est.ComerSaindo; Reiniciar(6); fimDoEstado = Time.time + Duracao(comerSair, 1.2f); }
                break;
            case Est.ComerSaindo:
                Alvo(6);
                if (Time.time >= fimDoEstado) Parar(Random.Range(1.5f, 5f));
                break;
            case Est.Fugindo:
                {
                    bool gal = fugirGalopando && galope != null;
                    float v = gal ? velGalope : velTrote;
                    Alvo(gal ? 3 : 2); Ritmo(gal ? 3 : 2, v, gal ? passoDoGalope : passoDoTrote);
                    Vector3 alvo = transform.position + dirFuga * 6f;
                    Mover(alvo, v, 0.1f);
                    bool calmo = Time.time - ultimoMedo > 2.5f || Time.time - inicioFuga > 9f;
                    if (calmo) Parar(Random.Range(1f, 3f));
                }
                break;
        }
        for (int i = 0; i < 7; i++) peso[i] = Mathf.MoveTowards(peso[i], alvoPeso[i], dt * 3.5f);
        Aplicar();
        Chao(dt);
    }

    float Duracao(AnimationClip c, float padrao) { return c != null ? Mathf.Max(0.3f, c.length * 0.97f) : padrao; }

    void Parar(float segundos) { est = Est.Parado; fimDoEstado = Time.time + segundos; }

    // decide o que fazer depois de ficar parado: pastar, andar até um ponto perto de casa ou continuar parado
    void Decidir()
    {
        float r = Random.value;
        if (r < 0.4f && comer != null) { est = Est.ComerEntrando; Reiniciar(4); fimDoEstado = Time.time + Duracao(comerEntrar, 1.2f); }
        else if (r < 0.85f)
        {
            Vector2 d = Random.insideUnitCircle * raio;
            destino = casa + new Vector3(d.x, 0f, d.y);
            est = Est.Andando; fimDoEstado = Time.time + 14f;
        }
        else Parar(Random.Range(3f, 8f));
    }

    // a animação de pernas acompanha a velocidade real do corpo
    void Ritmo(int i, float vel, float passo) { if (temGrafo) clipes[i].SetSpeed(Mathf.Clamp(vel / Mathf.Max(0.1f, passo), 0.5f, 1.6f)); }

    // gira para o ponto e anda para a frente (desviando de obstáculos grandes); devolve true ao chegar
    bool Mover(Vector3 alvo, float vel, float parar)
    {
        Vector3 d = alvo - transform.position; d.y = 0f;
        if (d.magnitude <= parar) return true;
        Vector3 dir = d.normalized;
        Vector3 origem = transform.position + Vector3.up * 0.6f;
        RaycastHit h;
        if (Physics.Raycast(origem, dir, out h, 2.2f, ~0, QueryTriggerInteraction.Ignore) && h.normal.y < 0.6f && h.collider.GetComponentInParent<AnimalVivo>() != this)
        {
            // obstáculo (casa, pedra, árvore): tenta desviar para um dos lados
            Vector3 e = Quaternion.Euler(0f, 50f, 0f) * dir, dd = Quaternion.Euler(0f, -50f, 0f) * dir;
            dir = !Physics.Raycast(origem, e, 2.2f, ~0, QueryTriggerInteraction.Ignore) ? e : (!Physics.Raycast(origem, dd, 2.2f, ~0, QueryTriggerInteraction.Ignore) ? dd : -dir);
            if (est == Est.Andando) destino = casa;
        }
        float giro = est == Est.Fugindo ? 240f : 100f;
        yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, giro * Time.deltaTime);
        Vector3 frente = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float alinhado = Mathf.Clamp01(Vector3.Dot(frente, dir) * 1.5f);                   // só anda depressa quando já está virado
        transform.position += frente * vel * alinhado * Time.deltaTime;
        return false;
    }

    // ---------------- susto: gente ou monstros por perto
    static void AtualizarListas()
    {
        if (Time.time < proximaBusca) return;
        proximaBusca = Time.time + 0.6f;
        gente.Clear(); monstros.Clear();
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.gameObject.activeInHierarchy) gente.Add(p.transform);
        foreach (InimigoEsqueleto e in FindObjectsByType<InimigoEsqueleto>(FindObjectsSortMode.None)) if (e.gameObject.activeInHierarchy) monstros.Add(e.transform);
        foreach (ChefeGuardiao e in FindObjectsByType<ChefeGuardiao>(FindObjectsSortMode.None)) if (e.Lutando) monstros.Add(e.transform);
    }

    void Susto()
    {
        if (Time.time < proximoMedo) return;
        proximoMedo = Time.time + 0.35f;
        AtualizarListas();
        Vector3 pior = Vector3.zero; float melhor = float.MaxValue; bool achou = false;
        foreach (Transform t in gente) { float d = (t.position - transform.position).sqrMagnitude; if (d < distanciaDeSusto * distanciaDeSusto && d < melhor) { melhor = d; pior = t.position; achou = true; } }
        foreach (Transform t in monstros) { if (t == null) continue; float d = (t.position - transform.position).sqrMagnitude; if (d < distanciaDeMonstro * distanciaDeMonstro && d < melhor) { melhor = d; pior = t.position; achou = true; } }
        if (!achou) return;
        ultimoMedo = Time.time;
        Vector3 fuga = transform.position - pior; fuga.y = 0f; if (fuga.sqrMagnitude < 0.01f) fuga = -transform.forward;
        dirFuga = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * fuga.normalized;
        if (est != Est.Fugindo) { est = Est.Fugindo; inicioFuga = Time.time; }
    }

    // ---------------- chão: segue o relevo e inclina o corpo
    void Chao(float dt)
    {
        Vector3 p = transform.position; float y = p.y; float yFrente = y, yTras = y;
        if (terreno != null)
        {
            Vector3 o = terreno.transform.position;
            y = terreno.SampleHeight(p) + o.y;
            Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; Vector3 f = p + fw * 0.7f, t = p - fw * 0.7f;
            yFrente = terreno.SampleHeight(f) + o.y; yTras = terreno.SampleHeight(t) + o.y;
        }
        p.y = Mathf.Lerp(p.y, y, dt * 10f); transform.position = p;
        float alvoPitch = -Mathf.Atan2(yFrente - yTras, 1.4f) * Mathf.Rad2Deg;
        pitch = Mathf.Lerp(pitch, Mathf.Clamp(alvoPitch, -25f, 25f), dt * 4f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
