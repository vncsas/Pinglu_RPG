using UnityEngine;

// Morador da vila (Civil 2, Civil 3, Mulher 1 do colega). Faz uma atividade parado (varrer, conversar, ouvir...) ou
// passeia por uma ronda; olha para o jogador quando ele chega perto e FOGE de monstros por perto, voltando depois.
// Usa só os estados do Animator que o menu "Civil2/Civil/Woman1 > Montar tudo" cria (Parado, Andar_Calmo, Falar...).
[RequireComponent(typeof(Animator))]
public class NpcDaVila : MonoBehaviour
{
    public enum Modo { Parado, Ronda }
    public Modo modo = Modo.Parado;
    [Tooltip("Estado do Animator tocado enquanto está parado (Varrer, Falar, Ouvir, Parado...)")]
    public string atividade = "Parado";
    public Vector3[] pontos;                 // ronda (posições no mundo)
    public float velocidade = 1.1f;
    public float pausaNosPontos = 3.5f;
    public bool temVassoura;
    public bool reagirAoJogador = true;
    public bool foge = true;
    public Transform olharPara;              // quem está conversando com ele (opcional)

    enum Est { Atividade, Andando, Pausa, Fugindo, OuvindoJogador, Voltando }
    Est est = Est.Atividade;
    Animator anim; string atual = "";
    Vector3 casa; Quaternion giroCasa;
    int proximoPonto; float pausaAte, ultimoPerigo = -99f, proximaBusca; Vector3 foraDoPerigo;
    Transform jogador;
    Transform maoD; Transform vassoura; Transform cerdas;

    void Awake() { anim = GetComponent<Animator>(); }

    void Start()
    {
        casa = transform.position; giroCasa = transform.rotation;
        if (modo == Modo.Ronda && pontos != null && pontos.Length > 0) { est = Est.Pausa; pausaAte = Time.time + Random.Range(0.5f, 2.5f); Tocar("Parado"); }
        else Atividade(true);
        if (temVassoura) CriarVassoura();
    }

    void Tocar(string estado, float mistura = 0.2f)
    {
        if (anim == null || atual == estado) return;
        if (!anim.HasState(0, Animator.StringToHash(estado))) { if (estado != "Parado") Tocar("Parado", mistura); return; }
        atual = estado; anim.CrossFadeInFixedTime(estado, mistura);
    }

    void Atividade(bool inicio)
    {
        est = Est.Atividade;
        if (inicio && anim.HasState(0, Animator.StringToHash(atividade))) { atual = atividade; anim.Play(atividade, 0, Random.value); }   // cada um começa num ponto diferente
        else Tocar(atividade);
    }

    void Update()
    {
        if (Time.time >= proximaBusca) { proximaBusca = Time.time + 0.7f; Observar(); }

        switch (est)
        {
            case Est.Atividade:
                if (olharPara != null) Virar(olharPara.position - transform.position, 240f);
                break;
            case Est.Pausa:
                if (Time.time >= pausaAte) { est = Est.Andando; Tocar("Andar_Calmo"); }
                break;
            case Est.Andando:
                if (pontos != null && pontos.Length > 0)
                {
                    Vector3 alvo = pontos[proximoPonto];
                    if (Mover(alvo, velocidade, 0.4f)) { proximoPonto = (proximoPonto + 1) % pontos.Length; est = Est.Pausa; pausaAte = Time.time + pausaNosPontos * Random.Range(0.6f, 1.4f); Tocar(Random.value < 0.3f ? "Falar" : "Parado"); }
                }
                break;
            case Est.Fugindo:
                if (Time.time - ultimoPerigo > 6f) { est = Est.Voltando; Tocar("Andar_Calmo"); }
                else { Mover(foraDoPerigo, 4.3f, 1f); Tocar(anim.HasState(0, Animator.StringToHash("Correr_Desesperado")) ? "Correr_Desesperado" : "Correr"); }
                break;
            case Est.Voltando:
                if (Mover(casa, velocidade * 1.2f, 0.5f)) { transform.rotation = giroCasa; if (modo == Modo.Ronda) { est = Est.Pausa; pausaAte = Time.time + 1f; Tocar("Parado"); } else Atividade(false); }
                break;
            case Est.OuvindoJogador:
                if (jogador != null) Virar(jogador.position - transform.position, 300f);
                break;
        }
        AjustarAoChao();
    }

    // olha à volta: monstros por perto (foge) e jogador por perto (vira e ouve)
    void Observar()
    {
        if (foge)
        {
            Vector3 pior = Vector3.zero; float melhor = 24f * 24f; bool achou = false;
            foreach (InimigoEsqueleto e in FindObjectsByType<InimigoEsqueleto>(FindObjectsSortMode.None)) { if (e.Vida != null && e.Vida.Morto) continue; float d = (e.transform.position - transform.position).sqrMagnitude; if (d < melhor) { melhor = d; pior = e.transform.position; achou = true; } }
            foreach (ChefeGuardiao e in FindObjectsByType<ChefeGuardiao>(FindObjectsSortMode.None)) { if (!e.Lutando) continue; float d = (e.transform.position - transform.position).sqrMagnitude; if (d < melhor * 4f) { melhor = d / 4f; pior = e.transform.position; achou = true; } }
            if (achou)
            {
                ultimoPerigo = Time.time;
                Vector3 fuga = transform.position - pior; fuga.y = 0f; if (fuga.sqrMagnitude < 0.01f) fuga = -transform.forward;
                foraDoPerigo = transform.position + fuga.normalized * 16f;
                if (est != Est.Fugindo) { est = Est.Fugindo; atual = ""; }
                return;
            }
        }
        if (est == Est.Fugindo || est == Est.Voltando) return;
        if (!reagirAoJogador || olharPara != null) return;
        if (jogador == null || !jogador.gameObject.activeInHierarchy)
        {
            jogador = null;
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) { jogador = p.transform; break; }
        }
        if (jogador == null) return;
        float dj = Vector3.Distance(jogador.position, transform.position);
        if (dj < 3.4f && (est == Est.Atividade || est == Est.Pausa)) { est = Est.OuvindoJogador; Tocar("Ouvir", 0.3f); }
        else if (dj > 5f && est == Est.OuvindoJogador) { if (modo == Modo.Ronda) { est = Est.Pausa; pausaAte = Time.time + 1f; Tocar("Parado"); } else Atividade(false); }
    }

    void Virar(Vector3 dir, float grausPorSeg)
    {
        dir.y = 0f; if (dir.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), grausPorSeg * Time.deltaTime);
    }

    // anda em linha reta até o ponto; devolve true ao chegar
    bool Mover(Vector3 alvo, float vel, float parar)
    {
        Vector3 d = alvo - transform.position; d.y = 0f;
        if (d.magnitude <= parar) return true;
        Virar(d, 360f);
        transform.position += transform.forward * vel * Time.deltaTime;
        return false;
    }

    // acompanha o chão (terreno e calçada)
    void AjustarAoChao()
    {
        Vector3 o = transform.position + Vector3.up * 2f; float y = transform.position.y;
        RaycastHit[] hits = Physics.RaycastAll(o, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        bool achou = false; float melhor = float.NegativeInfinity;
        foreach (RaycastHit r in hits) { if (r.collider.transform.IsChildOf(transform) || r.collider.GetComponentInParent<Vida>() != null || r.collider.GetComponentInParent<PlayerLocomotion>() != null) continue; if (r.point.y > melhor) { melhor = r.point.y; achou = true; } }
        if (achou) y = melhor; else { Terrain t = Terrain.activeTerrain; if (t != null) y = t.SampleHeight(transform.position) + t.transform.position.y; }
        Vector3 p = transform.position; p.y = Mathf.Lerp(p.y, y, Time.deltaTime * 12f); transform.position = p;
    }

    // ---------------- vassoura (só enfeite: sempre do punho até o chão, à frente dele)
    void CriarVassoura()
    {
        maoD = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (maoD == null) return;
        GameObject g = new GameObject("Vassoura"); vassoura = g.transform;
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default"));
        GameObject cabo = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(cabo.GetComponent<Collider>());
        cabo.transform.SetParent(vassoura, false); cabo.transform.localScale = new Vector3(0.035f, 0.75f, 0.035f);
        Material mc = new Material(m); Color cm = new Color(0.4f, 0.27f, 0.14f); mc.color = cm; if (mc.HasProperty("_BaseColor")) mc.SetColor("_BaseColor", cm); cabo.GetComponent<Renderer>().sharedMaterial = mc;
        GameObject feixe = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(feixe.GetComponent<Collider>()); cerdas = feixe.transform;
        feixe.transform.SetParent(vassoura, false); feixe.transform.localPosition = new Vector3(0f, -0.78f, 0f); feixe.transform.localScale = new Vector3(0.26f, 0.3f, 0.12f);
        Material mb = new Material(m); Color cb = new Color(0.72f, 0.6f, 0.3f); mb.color = cb; if (mb.HasProperty("_BaseColor")) mb.SetColor("_BaseColor", cb); feixe.GetComponent<Renderer>().sharedMaterial = mb;
    }

    void LateUpdate()
    {
        if (vassoura == null) return;
        if (atual != "Varrer") { vassoura.gameObject.SetActive(false); return; }
        vassoura.gameObject.SetActive(true);
        Vector3 mao = maoD.position;
        float vai = Mathf.Sin(Time.time * 3.1f) * 0.35f;                                             // vai e vem do varrer
        Vector3 chao = transform.position + transform.forward * (0.85f + vai) + transform.right * 0.1f;
        Vector3 topo = mao - (chao - mao).normalized * 0.25f;                                       // o cabo passa um pouco da mão
        Vector3 v = chao - topo; float L = Mathf.Max(0.6f, v.magnitude);
        vassoura.position = (topo + chao) * 0.5f; vassoura.rotation = Quaternion.FromToRotation(Vector3.up, v.normalized);
        Transform cabo = vassoura.GetChild(0);
        cabo.localScale = new Vector3(0.035f, (L - 0.3f) * 0.5f, 0.035f); cabo.localPosition = new Vector3(0f, 0.15f, 0f);
        cerdas.localPosition = new Vector3(0f, -(L * 0.5f - 0.15f), 0f);
    }
}
