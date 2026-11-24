using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// DRAKTHAR, O DEVASTADOR: o dragão, chefe final. DUAS FASES: normal (100% a 50% da vida) e SOMBRIA (abaixo de 50%, depois da cena em que o Ladrão se funde a ele).
//   NO AR    - voa em círculos. Ataques: Sopro Varrido, Chuva de Meteoros, Leque de Bolas de Fogo.
//   NO CHÃO  - a cada ataque aéreo ou dois ele DESCE: Investida (corre em linha reta e derrapa, ficando ATORDOADO), Mergulho (despenca e pousa).
//              No chão ele fica vulnerável por ~12 s: Mordida, Varrida de Cauda, Bater de Asas e Sopro curto.
//   FASE 2   - maior, com aura sombria e ataques novos: Raios Sombrios, Vórtice (puxa e explode) e CARGA SOMBRIA: ele pousa e junta uma nova gigante;
//              se o grupo causar dano suficiente a tempo, a carga é interrompida e ele TOMBA ATORDOADO (leva dano extra por ~9 s).
// Todo ataque é anunciado por um aviso no chão. Rolar tira o dano dos golpes.
public class ChefeDragao : MonoBehaviour, IAfetavelPorHabilidade
{
    [Header("Modelo (o menu Pinglu > 84 preenche)")]
    public GameObject modelo;
    public AnimationClip animVoo, animPlanar, animSopro;
    public Material materialParticula;
    [Header("Números")]
    public float vidaBase = 6500f;
    public float comprimento = 26f;                  // maior medida do modelo, em metros

    public Vida Vida { get; private set; }
    public bool Despertou { get { return despertou; } }
    public bool Lutando { get { return despertou && !morto; } }
    public bool Morto { get { return morto; } }
    public int Fase { get { return sombrio ? 2 : 1; } }
    public bool Sombrio { get { return sombrio; } }
    public event Action<ChefeDragao> AoMorrer;
    public event Action<ChefeDragao> AoFase2;          // chega a 50% da vida: quem ouvir toca a cena e depois chama IniciarFase2()

    // ---- IAfetavelPorHabilidade: chefe não é provocado nem empurrado
    public void Provocar(Transform quem, float s) { }
    public void Empurrar(Vector3 v) { }
    public void Lentidao(float f, float s) { }

    Vector3 centro; float raioArena = 70f, chaoCentro;
    bool despertou, morto, sombrio, pediuFase2, atordoado; float esc = 1f, balanco, cargaProg = -1f;
    GameObject modeloGo; AuraSombria aura; readonly List<GameObject> estrelas = new List<GameObject>();
    readonly List<Material> matsModelo = new List<Material>(); readonly List<Color> coresOrig = new List<Color>();
    Transform inclinacao, corpoModelo, bocaT, ossoMandibula;
    readonly List<Transform> asasOssos = new List<Transform>(), maosOssos = new List<Transform>(); readonly List<int> ladoAsa = new List<int>();
    PlayableGraph grafo; AnimationMixerPlayable mixer; readonly float[] peso = new float[3], alvoPeso = new float[3]; bool grafoOk;
    ParticleSystem jato, fumaca; Light luzJato;
    float abertura, aberturaAlvo, fasePulso, ampAsa = 30f, rolagem, arfagem;
    Vector3 ultimaPos; float anguloVoo;
    readonly List<GameObject> avisos = new List<GameObject>();
    static Material matLinha;

    float MulDano { get { return 1f + 0.3f * (Fase - 1); } }
    float MulRitmo { get { return 1f + 0.28f * (Fase - 1); } }
    float AltChao { get { return 3.4f * esc; } }

    // ------------------------------------------------------------------ preparo
    public void Preparar(Vector3 centroArena, float raio, int jogadores)
    {
        centro = centroArena; raioArena = raio; chaoCentro = Chao(centro);
        Vida = GetComponent<Vida>(); if (Vida == null) Vida = gameObject.AddComponent<Vida>();
        Vida.DefinirMaxima(vidaBase * (1f + 0.6f * (Mathf.Max(1, jogadores) - 1)));
        Vida.AoMorrer += Morrer;
        Vida.FiltroDeDano = (d, o) => atordoado ? d * 1.45f : d;               // atordoado = leva dano extra
        BarraDeChefe.Criar(gameObject, Vida, "Drakthar, o Devastador", ApresentacaoDoChefe.Dragao().fonte, () => Lutando).Cores(new Color(0.95f, 0.38f, 0.08f), new Color(0.35f, 0.08f, 0.03f));
        MontarModelo();
        BoxCollider b = gameObject.AddComponent<BoxCollider>(); b.center = new Vector3(0f, 0f, 0f); b.size = new Vector3(comprimento * 0.28f, comprimento * 0.22f, comprimento * 0.6f);
        BoxCollider c = gameObject.AddComponent<BoxCollider>(); c.center = new Vector3(0f, comprimento * 0.08f, comprimento * 0.42f); c.size = new Vector3(comprimento * 0.12f, comprimento * 0.12f, comprimento * 0.3f);   // pescoço e cabeça
        CriarSopro();
        ultimaPos = transform.position;
    }

    static Transform Achar(Transform raiz, string[] nomes)
    {
        foreach (string n in nomes) foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name.ToLower().Contains(n)) return t;
        return null;
    }

    void MontarModelo()
    {
        inclinacao = new GameObject("Inclinacao").transform; inclinacao.SetParent(transform, false);
        corpoModelo = new GameObject("Dragao 3D").transform; corpoModelo.SetParent(inclinacao, false);
        if (modelo == null) return;
        GameObject m = Instantiate(modelo); m.name = "Modelo do Dragao"; modeloGo = m;
        foreach (Collider col in m.GetComponentsInChildren<Collider>()) Destroy(col);
        m.transform.SetParent(corpoModelo, false); m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity; m.transform.localScale = Vector3.one;
        // vira o modelo para a cabeça apontar para +Z
        Transform cab = Achar(m.transform, new[] { "head" }), cauda = Achar(m.transform, new[] { "tail" });
        if (cab != null && cauda != null)
        {
            Vector3 v = corpoModelo.InverseTransformDirection(cab.position - cauda.position); v.y = 0f;
            if (v.sqrMagnitude > 0.01f) m.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 0f);
        }
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (r is SkinnedMeshRenderer) ((SkinnedMeshRenderer)r).updateWhenOffscreen = true;
        }
        // tamanho e centro a partir dos ossos
        var pts = new List<Vector3>(); foreach (Transform t in m.GetComponentsInChildren<Transform>()) pts.Add(corpoModelo.InverseTransformPoint(t.position));
        Vector3 mn = Vector3.zero, mx = Vector3.zero;
        if (pts.Count > 0)
        {
            float[] xs = new float[pts.Count], ys = new float[pts.Count], zs = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) { xs[i] = pts[i].x; ys[i] = pts[i].y; zs[i] = pts[i].z; }
            Array.Sort(xs); Array.Sort(ys); Array.Sort(zs);
            int lo = Mathf.FloorToInt(pts.Count * 0.03f), hi = pts.Count - 1 - lo;
            mn = new Vector3(xs[lo], ys[lo], zs[lo]); mx = new Vector3(xs[hi], ys[hi], zs[hi]);
        }
        Vector3 tam = mx - mn; float maior = Mathf.Max(0.01f, Mathf.Max(tam.x, Mathf.Max(tam.y, tam.z)));
        float esc = comprimento / maior;
        m.transform.localScale = Vector3.one * esc; m.transform.localPosition = -((mn + mx) * 0.5f) * esc;
        // ossos que o código mexe: asas e mandíbula
        foreach (Transform t in m.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.StartsWith("upperwing")) { asasOssos.Add(t); ladoAsa.Add(corpoModelo.InverseTransformPoint(t.position).x >= 0f ? 1 : -1); }
            else if (n.StartsWith("winghand")) maosOssos.Add(t);
            else if (n.StartsWith("jaw") && ossoMandibula == null) ossoMandibula = t;
        }
        // animações (voo, planar, sopro)
        if (animVoo != null || animSopro != null || animPlanar != null)
        {
            Animator an = m.GetComponent<Animator>(); if (an == null) an = m.AddComponent<Animator>(); an.applyRootMotion = false;
            Animation leg = m.GetComponent<Animation>(); if (leg != null) leg.enabled = false;
            grafo = PlayableGraph.Create("Drakthar"); grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(grafo, 3);
            AnimationClip[] cl = { animVoo, animPlanar != null ? animPlanar : animVoo, animSopro != null ? animSopro : animVoo };
            for (int i = 0; i < 3; i++) if (cl[i] != null) { var p = AnimationClipPlayable.Create(grafo, cl[i]); grafo.Connect(p, 0, mixer, i); }
            AnimationPlayableOutput.Create(grafo, "Saida", an).SetSourcePlayable(mixer);
            grafo.Play(); grafoOk = true; peso[0] = alvoPeso[0] = 1f; mixer.SetInputWeight(0, 1f);
        }
        bocaT = new GameObject("Boca").transform; bocaT.SetParent(corpoModelo, false); bocaT.localPosition = new Vector3(0f, comprimento * 0.02f, comprimento * 0.46f);
    }

    void OnDestroy()
    {
        if (grafoOk && grafo.IsValid()) grafo.Destroy(); foreach (GameObject g in avisos) if (g != null) Destroy(g);
        foreach (GameObject e in estrelas) if (e != null) Destroy(e);
        if (aura != null) Destroy(aura.gameObject);
        if (ThirdPersonCameraFollow.FocoDeChefe == transform) ThirdPersonCameraFollow.FocoDeChefe = null;
    }

    // 0 = voo (bater asas), 1 = planando/pose brava, 2 = soprando fogo
    void Pose(int i) { for (int k = 0; k < 3; k++) alvoPeso[k] = k == i ? 1f : 0f; }

    void Update()
    {
        // metade da vida: para tudo e avisa (a cena da fusão é tocada por quem ouve o evento)
        if (despertou && !morto && !sombrio && !pediuFase2 && Vida != null && Vida.Fracao <= 0.5f)
        {
            pediuFase2 = true; Vida.TornarImortal(900f); ParaTudo();
            if (AoFase2 != null) AoFase2(this); else IniciarFase2();
        }
        if (grafoOk)
        {
            for (int k = 0; k < 3; k++) { peso[k] = Mathf.MoveTowards(peso[k], alvoPeso[k], Time.deltaTime * 3.5f); mixer.SetInputWeight(k, peso[k]); }
        }
        // inclina o corpo conforme sobe/desce e curva
        Vector3 vel = (transform.position - ultimaPos) / Mathf.Max(0.0001f, Time.deltaTime); ultimaPos = transform.position;
        float sobe = Mathf.Clamp(vel.y / 25f, -1f, 1f);
        Vector3 lat = transform.InverseTransformDirection(vel); float curva = Mathf.Clamp(lat.x / 15f, -1f, 1f);
        arfagem = Mathf.Lerp(arfagem, -sobe * 28f, Time.deltaTime * 4f); rolagem = Mathf.Lerp(rolagem, -curva * 25f, Time.deltaTime * 3f);
        if (inclinacao != null) inclinacao.localRotation = Quaternion.Euler(arfagem, 0f, rolagem + balanco);
        abertura = Mathf.MoveTowards(abertura, aberturaAlvo, Time.deltaTime * 4f);
    }

    // bate as asas e abre a boca por cima da pose do arquivo
    void LateUpdate()
    {
        if (corpoModelo == null) return;
        fasePulso += Time.deltaTime;
        float onda = Mathf.Sin(fasePulso * Mathf.PI * 2f * 0.8f);
        Vector3 eixo = corpoModelo.forward;
        var pivos = new Vector3[asasOssos.Count]; for (int i = 0; i < asasOssos.Count; i++) pivos[i] = asasOssos[i].position;
        for (int i = 0; i < asasOssos.Count; i++)
        {
            Quaternion d = Quaternion.AngleAxis((onda * ampAsa + 6f) * ladoAsa[i], eixo);
            asasOssos[i].rotation = d * asasOssos[i].rotation;
            foreach (Transform h in maosOssos)
            {
                if ((corpoModelo.InverseTransformPoint(h.position).x >= 0f ? 1 : -1) != ladoAsa[i]) continue;
                h.position = pivos[i] + d * (h.position - pivos[i]); h.rotation = d * h.rotation;
            }
        }
        if (ossoMandibula != null && abertura > 0.001f) ossoMandibula.rotation = Quaternion.AngleAxis(32f * abertura, corpoModelo.right) * ossoMandibula.rotation;
    }

    // ------------------------------------------------------------------ utilidades
    static float Chao(Vector3 p) { Terrain t = Terrain.activeTerrain; return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y; }
    static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

    List<PlayerLocomotion> Jogadores()
    {
        var l = new List<PlayerLocomotion>();
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            if (!p.enabled || !p.gameObject.activeInHierarchy) continue; Vida v = p.GetComponent<Vida>(); if (v != null && v.Morto) continue; l.Add(p);
        }
        return l;
    }
    Transform AlvoAleatorio()
    {
        var l = Jogadores(); if (l.Count == 0) return null;
        var humanos = l.FindAll(p => !p.Companheiro); var fonte = humanos.Count > 0 && UnityEngine.Random.value < 0.75f ? humanos : l;
        return fonte[UnityEngine.Random.Range(0, fonte.Count)].transform;
    }
    Transform MaisPerto(Vector3 de)
    {
        Transform r = null; float m = float.MaxValue;
        foreach (PlayerLocomotion p in Jogadores()) { float d = Plano(p.transform.position - de).sqrMagnitude; if (d < m) { m = d; r = p.transform; } }
        return r;
    }

    // dano em um jogador (rolando = sem dano, pelo próprio filtro de dano dele)
    void Ferir(Transform jog, float dano, Vector3 origem, float empurra = 0f)
    {
        Vida v = jog.GetComponent<Vida>(); if (v == null || v.Morto) return;
        v.ReceberDano(dano * MulDano, origem);
        if (empurra > 0f) { Vector3 dir = Plano(jog.position - origem); if (dir.sqrMagnitude < 0.01f) dir = jog.forward; StartCoroutine(Empurrao(jog, dir.normalized, empurra)); }
    }
    IEnumerator Empurrao(Transform jog, Vector3 dir, float dist)
    {
        CharacterController cc = jog.GetComponent<CharacterController>(); if (cc == null) yield break;
        for (float t = 0f; t < 0.28f && cc != null && cc.enabled; t += Time.deltaTime) { cc.Move(dir * (dist / 0.28f) * Time.deltaTime); yield return null; }
    }
    void Tremor(float f, float s) { TremorDeCamera.Tremer(f, s); }

    // ---------- avisos vermelhos no chão
    static Material Linha() { if (matLinha == null) { Shader s = Shader.Find("Sprites/Default"); if (s != null) matLinha = new Material(s); } return matLinha; }
    LineRenderer NovaLinha(string nome, int n, float largura, Color cor)
    {
        GameObject g = new GameObject(nome); avisos.Add(g);
        LineRenderer lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = Linha(); lr.positionCount = n; lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = largura; lr.startColor = lr.endColor = cor; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return lr;
    }
    void AnelNoChao(LineRenderer lr, Vector3 c, float raio)
    {
        int n = lr.positionCount;
        for (int i = 0; i < n; i++) { float a = i / (float)(n - 1) * Mathf.PI * 2f; Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * raio; p.y = Chao(p) + 0.25f; lr.SetPosition(i, p); }
    }
    // círculo de aviso: o anel de fora fica parado e um de dentro cresce até encostar (a hora do golpe)
    IEnumerator AvisoCirculo(Vector3 c, float raio, float dur)
    {
        LineRenderer fora = NovaLinha("Aviso", 60, 0.5f, new Color(1f, 0.2f, 0.1f, 0.9f)), dentro = NovaLinha("Aviso interno", 60, 0.35f, new Color(1f, 0.6f, 0.2f, 0.9f));
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur; AnelNoChao(fora, c, raio); AnelNoChao(dentro, c, Mathf.Max(0.1f, raio * k));
            float pisca = 0.55f + 0.45f * Mathf.Sin(t * 14f); fora.startColor = fora.endColor = new Color(1f, 0.15f, 0.08f, pisca);
            yield return null;
        }
        avisos.Remove(fora.gameObject); avisos.Remove(dentro.gameObject); Destroy(fora.gameObject); Destroy(dentro.gameObject);
    }
    IEnumerator AvisoCone(Transform seguir, float comp, float ang, float dur)
    {
        LineRenderer lr = NovaLinha("Aviso cone", 20, 0.45f, new Color(1f, 0.2f, 0.1f, 0.9f));
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            Vector3 o = Plano(seguir.position), f = Plano(seguir.forward).normalized;
            int i = 0; lr.SetPosition(i++, Sobre(o));
            for (int k = 0; k <= 16; k++) { float a = Mathf.Lerp(-ang, ang, k / 16f); lr.SetPosition(i++, Sobre(o + Quaternion.Euler(0f, a, 0f) * f * comp)); }
            lr.SetPosition(i++, Sobre(o)); lr.SetPosition(i, Sobre(o));
            lr.startColor = lr.endColor = new Color(1f, 0.15f, 0.08f, 0.45f + 0.5f * Mathf.Sin(t * 14f) * Mathf.Sin(t * 14f));
            yield return null;
        }
        avisos.Remove(lr.gameObject); Destroy(lr.gameObject);
    }
    Vector3 Sobre(Vector3 p) { p.y = Chao(p) + 0.3f; return p; }
    IEnumerator AvisoLinha(Vector3 a, Vector3 b, float dur)
    {
        LineRenderer lr = NovaLinha("Aviso linha", 14, 0.7f, new Color(1f, 0.2f, 0.1f, 0.9f));
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            for (int i = 0; i < 14; i++) lr.SetPosition(i, Sobre(Vector3.Lerp(a, b, i / 13f)));
            lr.startColor = lr.endColor = new Color(1f, 0.15f, 0.08f, 0.35f + 0.55f * Mathf.Abs(Mathf.Sin(t * 10f)));
            yield return null;
        }
        avisos.Remove(lr.gameObject); Destroy(lr.gameObject);
    }

    // ---------- fogo e partículas
    void CriarSopro()
    {
        GameObject g = new GameObject("Jato de Fogo"); g.transform.SetParent(transform, false);
        jato = g.AddComponent<ParticleSystem>(); jato.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = jato.main; m.loop = false; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 1500; m.startLifetime = 0.6f; m.startSize = 1.5f;
        var em = jato.emission; em.enabled = false;
        var sh = jato.shape; sh.enabled = false;
        var tam = jato.sizeOverLifetime; tam.enabled = true; tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 3.2f));
        var col = jato.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.97f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.65f, 0.2f), 0.25f), new GradientColorKey(new Color(1f, 0.3f, 0.06f), 0.55f), new GradientColorKey(new Color(0.25f, 0.08f, 0.04f), 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        var noise = jato.noise; noise.enabled = true; noise.strength = 1.5f; noise.frequency = 0.7f;
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>(); if (materialParticula != null) r.sharedMaterial = materialParticula; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObject gl = new GameObject("Luz do Fogo"); gl.transform.SetParent(bocaT != null ? bocaT : transform, false);
        luzJato = gl.AddComponent<Light>(); luzJato.type = LightType.Point; luzJato.color = new Color(1f, 0.5f, 0.15f); luzJato.range = 60f; luzJato.intensity = 0f; luzJato.shadows = LightShadows.None;
    }
    void Emitir(Vector3 pos, Vector3 vel, float tam, float vida, int n = 1)
    {
        if (jato == null) return;
        for (int i = 0; i < n; i++)
        {
            var ep = new ParticleSystem.EmitParams(); ep.position = pos + UnityEngine.Random.insideUnitSphere * tam * 0.4f; ep.velocity = vel + UnityEngine.Random.insideUnitSphere * (vel.magnitude * 0.12f + 1f);
            ep.startLifetime = vida * UnityEngine.Random.Range(0.8f, 1.2f); ep.startSize = tam * UnityEngine.Random.Range(0.7f, 1.3f); ep.startColor = Color.white; jato.Emit(ep, 1);
        }
    }
    void Explosao(Vector3 p, float raio)
    {
        for (int i = 0; i < 60; i++) Emitir(p + Vector3.up * 0.5f, new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.2f, 1.2f), UnityEngine.Random.Range(-1f, 1f)).normalized * raio * UnityEngine.Random.Range(1.5f, 3.6f), raio * 0.45f, 0.9f);
        StartCoroutine(Clarao(p + Vector3.up * 2f, raio * 3f));
    }
    IEnumerator Clarao(Vector3 p, float alcance)
    {
        GameObject g = new GameObject("Clarao"); g.transform.position = p; Light l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.range = alcance; l.shadows = LightShadows.None;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { l.intensity = 9f * (1f - t / 0.5f); yield return null; }
        Destroy(g);
    }

    // poça de fogo que queima quem fica nela
    class ZonaDeFogo : MonoBehaviour
    {
        public float raio = 3f, dano = 7f, duracao = 4f; public ChefeDragao dono; ParticleSystem ps; float t0, proximoTick;
        static readonly List<ZonaDeFogo> ativas = new List<ZonaDeFogo>();
        public static void Criar(ChefeDragao dono, Vector3 pos, float raio, float dur, float dano)
        {
            if (ativas.Count > 40 && ativas[0] != null) Destroy(ativas[0].gameObject);
            GameObject g = new GameObject("Chamas no chao"); g.transform.position = pos;
            ZonaDeFogo z = g.AddComponent<ZonaDeFogo>(); z.raio = raio; z.duracao = dur; z.dano = dano; z.dono = dono; z.Montar();
        }
        void Montar()
        {
            t0 = Time.time; ativas.Add(this); ativas.RemoveAll(a => a == null);
            ps = gameObject.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f); m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(raio * 0.35f, raio * 0.7f); m.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 14f * raio;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = raio * 0.85f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var v = ps.velocityOverLifetime; v.enabled = true; v.y = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            var col = ps.colorOverLifetime; col.enabled = true; Gradient gr = new Gradient();
            bool sb = dono != null && dono.sombrio;
            gr.SetKeys(new[] { new GradientColorKey(sb ? new Color(0.85f, 0.6f, 1f) : new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(sb ? new Color(0.55f, 0.12f, 0.9f) : new Color(1f, 0.4f, 0.08f), 0.4f), new GradientColorKey(sb ? new Color(0.12f, 0.03f, 0.2f) : new Color(0.3f, 0.08f, 0.04f), 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            var tam = ps.sizeOverLifetime; tam.enabled = true; tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ParticleSystemRenderer r = GetComponent<ParticleSystemRenderer>(); if (dono != null && dono.materialParticula != null) r.sharedMaterial = dono.materialParticula; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play(); proximoTick = Time.time + 0.4f;
        }
        void Update()
        {
            float vivo = Time.time - t0;
            if (vivo > duracao - 0.8f) { var em = ps.emission; em.rateOverTime = 0f; }
            if (vivo > duracao) { Destroy(gameObject); return; }
            if (Time.time < proximoTick || dono == null) return;
            proximoTick = Time.time + 0.5f;
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            {
                if (!p.enabled || !p.gameObject.activeInHierarchy) continue;
                Vector3 d = p.transform.position - transform.position; if (new Vector2(d.x, d.z).magnitude < raio && Mathf.Abs(d.y) < 3f) dono.Ferir(p.transform, dano, transform.position);
            }
        }
        void OnDestroy() { ativas.Remove(this); }
    }

    // ------------------------------------------------------------------ movimento
    Quaternion Olhar(Vector3 dir) { dir.y = 0f; return dir.sqrMagnitude < 0.01f ? transform.rotation : Quaternion.LookRotation(dir.normalized); }

    // vai até o destino (suave) olhando para onde vai ou para "olhaPara"
    IEnumerator Mover(Vector3 destino, float dur, Transform olhaPara = null)
    {
        Vector3 de = transform.position; Quaternion r0 = transform.rotation;
        for (float t = 0f; t < dur && !morto; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dur); Vector3 p = Vector3.Lerp(de, destino, k);
            p.y = Mathf.Max(p.y, Chao(p) + 2.5f); transform.position = p;
            Vector3 alvoDir = olhaPara != null ? olhaPara.position - p : destino - de;
            if (Plano(alvoDir).sqrMagnitude > 1f) transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(alvoDir), Time.deltaTime * 3f);
            yield return null;
        }
    }

    // voa em círculos sobre a arena
    IEnumerator Circular(float segundos, float alt = 20f)
    {
        Pose(0); ampAsa = 30f; aberturaAlvo = 0f;
        Vector3 f = Plano(transform.position - centro); anguloVoo = Mathf.Atan2(f.z, f.x);
        float raio = raioArena * 0.55f, vel = 15f * MulRitmo;
        for (float t = 0f; t < segundos && !morto; t += Time.deltaTime)
        {
            anguloVoo += vel / raio * Time.deltaTime;
            Vector3 alvo = centro + new Vector3(Mathf.Cos(anguloVoo), 0f, Mathf.Sin(anguloVoo)) * raio; alvo.y = Chao(alvo) + alt + Mathf.Sin(Time.time * 0.8f) * 1.5f;
            Vector3 p = Vector3.Lerp(transform.position, alvo, 1f - Mathf.Exp(-2.2f * Time.deltaTime));
            Vector3 dir = alvo - transform.position; transform.position = p;
            if (Plano(dir).sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(dir), Time.deltaTime * 2.5f);
            yield return null;
        }
    }

    // ------------------------------------------------------------------ entrada em cena
    // chega voando de longe, passa sobre o grupo, ruge e pousa no centro da arena
    public IEnumerator Chegada(Vector3 deOnde, bool teleportar = true)
    {
        Vector3 pouso = centro; pouso.y = Chao(centro) + AltChao;
        if (teleportar) transform.position = deOnde; else deOnde = transform.position;
        transform.rotation = Olhar(centro - deOnde); Pose(0); ampAsa = 30f;
        Vector3 meio = centro + Vector3.up * 30f - Plano(centro - deOnde).normalized * 25f; meio.y = Chao(meio) + 28f;
        float t1 = Mathf.Clamp(Vector3.Distance(deOnde, meio) / 55f, 6f, 15f);
        for (float t = 0f; t < t1; t += Time.deltaTime)
        {
            float k = t / t1; Vector3 p = Vector3.Lerp(deOnde, meio, k); p.y += Mathf.Sin(k * Mathf.PI) * 6f; transform.position = p;
            transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(meio - deOnde), Time.deltaTime * 2f);
            if (t > t1 * 0.52f && t < t1 * 0.52f + 0.1f) { Rugir(1.2f); }
            yield return null;
        }
        // dá uma volta e desce
        Vector3 a = transform.position; float giro = 3.4f;
        for (float t = 0f; t < giro; t += Time.deltaTime)
        {
            float k = t / giro; float ang = Mathf.Lerp(0f, Mathf.PI * 1.2f, k);
            Vector3 raioV = Plano(a - centro); Vector3 rot = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f) * raioV;
            Vector3 p = centro + Vector3.Lerp(rot, rot * 0.1f, k * k); p.y = Mathf.Lerp(a.y, pouso.y + 6f, Mathf.SmoothStep(0f, 1f, k));
            Vector3 dir = p - transform.position; transform.position = p; if (Plano(dir).sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(dir), Time.deltaTime * 4f);
            yield return null;
        }
        yield return Pousar(pouso, null);
    }

    // sobe da cratera da montanha: começa escondido lá embaixo, rasga a poeira e se ergue ruidosamente
    public IEnumerator Emergir(Vector3 fundo, float alturaFinal, Vector3 olharPara)
    {
        transform.position = fundo; transform.rotation = Olhar(olharPara - fundo); Pose(1); ampAsa = 14f; aberturaAlvo = 0.4f;
        float dur = 6.5f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur; float e = Mathf.SmoothStep(0f, 1f, k);
            transform.position = new Vector3(fundo.x, Mathf.Lerp(fundo.y, alturaFinal, e), fundo.z);
            transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(olharPara - fundo), Time.deltaTime * 1.5f);
            if (k > 0.35f && peso[0] < 0.9f) { Pose(0); ampAsa = 36f; }
            for (int i = 0; i < 4; i++) Emitir(transform.position + UnityEngine.Random.insideUnitSphere * 9f, UnityEngine.Random.insideUnitSphere * 7f + Vector3.up * 5f, 3.5f, 1.1f);
            Tremor(0.22f, 0.2f);
            if (t > dur * 0.62f && t < dur * 0.62f + 0.1f) { Rugir(2.6f); aberturaAlvo = 1f; }
            yield return null;
        }
        aberturaAlvo = 0f;
    }

    // pousa em "onde": desce, bate as asas levantando poeira e fogo
    IEnumerator Pousar(Vector3 onde, Transform olhaPara)
    {
        Pose(1); ampAsa = 38f;
        Vector3 de = transform.position; Quaternion r0 = transform.rotation; float dur = 1.7f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dur); transform.position = Vector3.Lerp(de, onde, k);
            if (olhaPara != null) transform.rotation = Quaternion.Slerp(r0, Olhar(olhaPara.position - onde), k);
            Emitir(Sobre(onde) + Vector3.up * 0.3f, UnityEngine.Random.insideUnitSphere * 8f + Vector3.up * 3f, 3f, 1f, 2);
            yield return null;
        }
        transform.position = onde; ampAsa = 12f;
        Tremor(0.25f, 1.1f); Explosao(Sobre(onde), 5f);
        foreach (PlayerLocomotion p in Jogadores()) { float d = Plano(p.transform.position - onde).magnitude; if (d < 13f) Ferir(p.transform, 20f, onde, 5f); }
        yield return new WaitForSeconds(0.4f);
    }

    void Rugir(float dur)
    {
        StartCoroutine(RugidoVisual(dur));
    }
    IEnumerator RugidoVisual(float dur)
    {
        aberturaAlvo = 1f; Tremor(0.2f, dur);
        for (float t = 0f; t < dur; t += Time.deltaTime) { if (bocaT != null) Emitir(bocaT.position, bocaT.forward * 10f, 1.2f, 0.6f, 1); yield return null; }
        aberturaAlvo = 0f;
    }

    public void Despertar()
    {
        if (despertou) return; despertou = true;
        ThirdPersonCameraFollow.FocoDeChefe = transform;
        StartCoroutine(Luta());
    }

    // ------------------------------------------------------------------ a luta
    // Ritmo: ataque aéreo, ataque aéreo, DESCE (investida / mergulho) e fica vulnerável no chão. Ele nunca fica no ar para sempre.
    IEnumerator Luta()
    {
        int ciclo = 0;
        if (transform.position.y - Chao(transform.position) < 10f * esc) yield return Decolar();      // começa subindo do chão onde pousou
        while (!morto)
        {
            yield return Circular(UnityEngine.Random.Range(1.4f, 2.4f) / MulRitmo);
            if (morto) yield break;
            Transform alvo = AlvoAleatorio(); if (alvo == null) { yield return Circular(1.5f); continue; }
            if (!sombrio)
            {
                switch (ciclo % 6)
                {
                    case 0: yield return SoproVarrido(alvo); break;
                    case 1: yield return Investida(alvo, 1); break;
                    case 2: yield return LequeDeFogo(alvo, 3); break;
                    case 3: yield return MergulhoEPouso(alvo); break;
                    case 4: yield return ChuvaDeMeteoros(9); break;
                    default: yield return Investida(alvo, 1); break;
                }
            }
            else
            {
                switch (ciclo % 8)
                {
                    case 0: yield return SoproVarrido(alvo); yield return SoproVarrido(alvo); break;
                    case 1: yield return Investida(alvo, 2); break;
                    case 2: yield return RaiosSombrios(); break;
                    case 3: yield return CargaSombria(); break;
                    case 4: yield return LequeDeFogo(alvo, 4); break;
                    case 5: yield return Vortice(); break;
                    case 6: yield return MergulhoEPouso(alvo); break;
                    default: yield return ChuvaDeMeteoros(14); break;
                }
            }
            ciclo++;
        }
    }

    // para tudo o que está fazendo (usado na cena da fase 2)
    public void ParaTudo()
    {
        StopAllCoroutines();
        foreach (GameObject g in avisos) if (g != null) Destroy(g); avisos.Clear();
        foreach (GameObject e in estrelas) if (e != null) Destroy(e); estrelas.Clear();
        if (luzJato != null) luzJato.intensity = 0f;
        atordoado = false; balanco = 0f; cargaProg = -1f; aberturaAlvo = 0f; Pose(1); ampAsa = 12f;
    }

    IEnumerator Decolar()
    {
        Pose(0); ampAsa = 36f; aberturaAlvo = 0f; Rugir(1.4f);
        Vector3 de = transform.position, ate = new Vector3(transform.position.x, Chao(transform.position) + 22f, transform.position.z) + Plano(transform.forward) * 8f;
        float dur = 2.2f;
        for (float t = 0f; t < dur && !morto; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(de, ate, Mathf.SmoothStep(0f, 1f, t / dur));
            if (t < 1.2f) { Emitir(Sobre(transform.position) + Vector3.up * 0.3f, UnityEngine.Random.insideUnitSphere * 9f + Vector3.up * 2f, 3f, 1f, 2); Tremor(0.12f, 0.2f); }
            yield return null;
        }
    }

    // a fase mudou: sobe, ruge e a arena treme
    IEnumerator RugidoDeFase()
    {
        Vector3 alto = centro + Vector3.up * 0f; alto.y = Chao(centro) + 34f;
        yield return Mover(alto, 1.6f); Pose(1); Rugir(2.6f);
        foreach (PlayerLocomotion p in Jogadores()) { Vector3 d = Plano(p.transform.position - centro); if (d.sqrMagnitude < 1f) d = Vector3.right; StartCoroutine(Empurrao(p.transform, d.normalized, 5f)); }
        for (int i = 0; i < 12; i++) { Vector3 q = centro + UnityEngine.Random.insideUnitSphere * raioArena * 0.7f; q.y = Chao(q); Explosao(q, 2.4f); yield return new WaitForSeconds(0.2f); }
        Pose(0);
    }

    // ---- ataque 1: linha de fogo varrendo o chão
    IEnumerator SoproVarrido(Transform alvo)
    {
        Pose(0);
        Vector3 ap = alvo.position; Vector3 ang = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;
        Vector3 posAtq = Plano(ap) + ang * 42f; posAtq.y = Chao(posAtq) + 17f;
        if (Plano(posAtq - centro).magnitude > raioArena * 1.1f) posAtq = centro + Plano(posAtq - centro).normalized * raioArena * 1.1f + Vector3.up * 17f;
        yield return Mover(posAtq, 1.5f, alvo);
        // linha de fogo: atravessa a posição do alvo, de lado
        ap = alvo.position; Vector3 frente = Plano(ap - transform.position).normalized, lado = Vector3.Cross(Vector3.up, frente);
        float meia = 17f + 2f * (Fase - 1); Vector3 p0 = ap - lado * meia, p1 = ap + lado * meia;
        StartCoroutine(AvisoLinha(p0, p1, 1.6f));
        Pose(2); aberturaAlvo = 1f;
        for (float t = 0f; t < 1.6f; t += Time.deltaTime) { transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(p0 - transform.position), Time.deltaTime * 5f); yield return null; }
        float dur = 3.2f / MulRitmo, prox = 0f; luzJato.intensity = 6f;
        for (float t = 0f; t < dur && !morto; t += Time.deltaTime)
        {
            Vector3 ponto = Vector3.Lerp(p0, p1, t / dur); ponto.y = Chao(ponto);
            transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(ponto - transform.position), Time.deltaTime * 6f);
            Vector3 boca = bocaT != null ? bocaT.position : transform.position;
            Vector3 dir = (ponto - boca);
            for (int i = 0; i < 7; i++) Emitir(Vector3.Lerp(boca, ponto, UnityEngine.Random.value * 0.9f), dir.normalized * 22f, 2.2f, 0.55f);
            Emitir(ponto + Vector3.up * 0.5f, Vector3.up * 4f, 3.4f, 0.8f, 2);
            if (Time.time >= prox)
            {
                prox = Time.time + 0.28f; ZonaDeFogo.Criar(this, ponto, 3.1f, 4.2f, 7f);
                foreach (PlayerLocomotion p in Jogadores()) { if (Plano(p.transform.position - ponto).magnitude < 3.4f) Ferir(p.transform, 18f, ponto); }
            }
            yield return null;
        }
        luzJato.intensity = 0f; aberturaAlvo = 0f; Pose(0);
        yield return new WaitForSeconds(0.4f);
    }

    // ---- ataque 2: meteoros caindo
    IEnumerator ChuvaDeMeteoros(int n)
    {
        Vector3 alto = centro; alto.y = Chao(centro) + 32f;
        yield return Mover(alto, 1.5f); Pose(1); ampAsa = 24f; Rugir(1.6f);
        for (int i = 0; i < n && !morto; i++)
        {
            Vector3 alvo;
            var l = Jogadores();
            if (i % 3 == 0 && l.Count > 0) alvo = l[UnityEngine.Random.Range(0, l.Count)].transform.position + UnityEngine.Random.insideUnitSphere * 2f;
            else alvo = centro + UnityEngine.Random.insideUnitSphere * raioArena * 0.8f;
            alvo.y = Chao(alvo);
            StartCoroutine(Meteoro(alvo));
            yield return new WaitForSeconds(Mathf.Max(0.18f, 0.5f / MulRitmo));
        }
        yield return new WaitForSeconds(2.2f); Pose(0);
    }
    IEnumerator Meteoro(Vector3 alvo)
    {
        const float RAIO = 4.6f;
        yield return AvisoCirculo(alvo, RAIO, 1.5f);
        if (morto) yield break;
        // a bola de fogo cai do céu
        Vector3 inicio = alvo + new Vector3(UnityEngine.Random.Range(-14f, 14f), 55f, UnityEngine.Random.Range(-14f, 14f));
        GameObject bola = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(bola.GetComponent<Collider>()); bola.transform.localScale = Vector3.one * 2.4f;
        Renderer rr = bola.GetComponent<Renderer>(); if (materialParticula != null) { rr.sharedMaterial = new Material(materialParticula); rr.sharedMaterial.SetColor("_BaseColor", new Color(1f, 0.6f, 0.15f)); }
        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            bola.transform.position = Vector3.Lerp(inicio, alvo, t / 0.55f) + Vector3.up * 0.4f; Emitir(bola.transform.position, Vector3.up * 3f, 2.4f, 0.5f, 3); yield return null;
        }
        Destroy(bola);
        Explosao(alvo, RAIO * 0.7f); Tremor(0.14f, 0.4f); ZonaDeFogo.Criar(this, alvo, RAIO * 0.7f, 3f, 6f);
        foreach (PlayerLocomotion p in Jogadores()) { Vector3 d = p.transform.position - alvo; if (new Vector2(d.x, d.z).magnitude < RAIO) Ferir(p.transform, 36f, alvo, 3f); }
    }

    // ---- ataque 3: mergulho + pouso + luta no chão
    IEnumerator MergulhoEPouso(Transform alvo)
    {
        Pose(0);
        Vector3 impacto = alvo.position; impacto.y = Chao(impacto);
        Vector3 alto = impacto + Plano(centro - impacto).normalized * 20f; alto.y = Chao(alto) + 42f;
        StartCoroutine(AvisoCirculo(impacto, 11f, 2.2f));
        yield return Mover(alto, 1.6f);
        Rugir(0.8f); yield return new WaitForSeconds(0.5f);
        // despenca
        aberturaAlvo = 1f; Pose(1); ampAsa = 8f;
        Vector3 de = transform.position; Vector3 chao = impacto + Vector3.up * (AltChao - 0.2f); float dur = 0.95f;
        for (float t = 0f; t < dur && !morto; t += Time.deltaTime)
        {
            float k = t / dur; k = k * k; Vector3 p = Vector3.Lerp(de, chao, k);
            Vector3 dir = chao - transform.position; transform.position = p; if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 8f);
            Emitir(p, -dir.normalized * 6f, 3f, 0.6f, 2);
            yield return null;
        }
        transform.position = chao; aberturaAlvo = 0f;
        // impacto
        Tremor(0.4f, 1.2f); Explosao(impacto, 9f);
        for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2f; ZonaDeFogo.Criar(this, impacto + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 12.5f, 2.6f, 4f, 6f); }
        foreach (PlayerLocomotion p in Jogadores()) { float d = Plano(p.transform.position - impacto).magnitude; if (d < 11.5f) Ferir(p.transform, 55f, impacto, 7f); }
        StartCoroutine(OndaDeChoque(impacto, 12f));
        transform.rotation = Olhar(Plano(centro - impacto).sqrMagnitude > 1f ? centro - impacto : transform.forward);
        Pose(1); ampAsa = 10f;
        yield return LutaNoChao(Mathf.Max(9f, 13f - 2.5f * (Fase - 1)));
    }

    // no chão: vulnerável, ataca de perto; depois de um tempo (ou de levar muito dano) levanta voo de novo
    IEnumerator LutaNoChao(float duracaoChao)
    {
        float inicioChao = Time.time; float vidaInicio = Vida.Atual;
        Pose(1); ampAsa = 10f;
        yield return new WaitForSeconds(0.8f);
        while (Time.time - inicioChao < duracaoChao && !morto)
        {
            Transform perto = MaisPerto(transform.position); if (perto == null) break;
            // vira para o jogador mais perto
            float giroAte = Time.time + 0.6f;
            while (Time.time < giroAte && !morto) { transform.rotation = Quaternion.RotateTowards(transform.rotation, Olhar(perto.position - transform.position), 160f * Time.deltaTime); yield return null; }
            float escolha = UnityEngine.Random.value; float dist = Plano(perto.position - transform.position).magnitude;
            if (dist > 24f * esc) yield return SoproCurto();
            else if (escolha < 0.38f) yield return Mordida();
            else if (escolha < 0.68f) yield return VarridaDeCauda();
            else if (escolha < 0.85f) yield return BaterAsas();
            else yield return SoproCurto();
            yield return new WaitForSeconds(Mathf.Max(0.6f, 1.6f / MulRitmo));
            if (vidaInicio - Vida.Atual > Vida.Maxima * 0.2f) break;        // levou muito dano: se irrita e decola
        }
        if (!morto) { Rugir(1.2f); yield return new WaitForSeconds(0.6f); yield return Decolar(); }
    }

    // ---- ataque novo: INVESTIDA. Corre em linha reta pelo chão, derrapa e fica ATORDOADO
    IEnumerator AvisoFaixa(Vector3 a, Vector3 b, float largura, float dur)
    {
        LineRenderer lr = NovaLinha("Aviso faixa", 16, largura, new Color(1f, 0.2f, 0.1f, 0.5f));
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            for (int i = 0; i < 16; i++) lr.SetPosition(i, Sobre(Vector3.Lerp(a, b, i / 15f)));
            lr.startColor = lr.endColor = new Color(1f, 0.15f, 0.08f, 0.25f + 0.4f * Mathf.Abs(Mathf.Sin(t * 9f)));
            yield return null;
        }
        avisos.Remove(lr.gameObject); Destroy(lr.gameObject);
    }

    IEnumerator Investida(Transform alvo, int vezes)
    {
        Pose(0);
        var acertou = new HashSet<Transform>();
        for (int v = 0; v < vezes && !morto; v++)
        {
            if (alvo == null || alvo.GetComponent<Vida>() == null || alvo.GetComponent<Vida>().Morto) alvo = AlvoAleatorio(); if (alvo == null) yield break;
            Vector3 ap = Plano(alvo.position), inicio;
            if (v == 0)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle.normalized; inicio = ap - new Vector3(r.x, 0f, r.y) * 58f;
                if (Plano(inicio - centro).magnitude > raioArena * 1.15f) inicio = centro + Plano(inicio - centro).normalized * raioArena * 1.15f;
                inicio.y = Chao(inicio) + 9f;
                yield return Mover(inicio, 1.5f, alvo);
            }
            else inicio = transform.position;
            Vector3 dirC = Plano(ap - inicio); if (dirC.sqrMagnitude < 4f) dirC = Plano(transform.forward); dirC.Normalize();
            Vector3 fim = ap + dirC * 34f; fim.y = 0f;
            Vector3 ini2 = inicio; ini2.y = 0f;
            // aviso: faixa vermelha na direção da corrida; ele ruge e se prepara
            StartCoroutine(AvisoFaixa(ini2, fim, 9f * esc, 1.5f));
            Rugir(1.1f); aberturaAlvo = 1f; Pose(1);
            for (float t = 0f; t < 1.5f && !morto; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(fim - transform.position), Time.deltaTime * 6f);
                Vector3 pp = transform.position; pp.y = Mathf.Lerp(pp.y, Chao(pp) + AltChao + 1.5f, Time.deltaTime * 2f); transform.position = pp;
                Emitir(Sobre(transform.position) + Vector3.up * 0.3f, UnityEngine.Random.insideUnitSphere * 5f + Vector3.up * 2f, 2.4f, 0.8f, 1);      // raspa o chão
                yield return null;
            }
            // a corrida
            Vector3 partida = transform.position; partida.y = 0f; float distTotal = Vector3.Distance(partida, fim), dur = Mathf.Clamp(distTotal / (62f * MulRitmo), 0.9f, 2.4f); float proxChama = 0f;
            Pose(0); ampAsa = 8f; acertou.Clear();
            for (float t = 0f; t < dur && !morto; t += Time.deltaTime)
            {
                float u = t / dur, e = u * u * 0.7f + u * 0.3f; Vector3 p = Vector3.Lerp(partida, fim, e); p.y = Chao(p) + AltChao;
                transform.position = p; transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(fim - p), Time.deltaTime * 8f);
                for (int k = 0; k < 4; k++) Emitir(Sobre(p) + Vector3.up * 0.4f, UnityEngine.Random.insideUnitSphere * 7f + Vector3.up * 3f, 3f * esc, 0.8f);
                Tremor(0.1f, 0.15f);
                if (Time.time >= proxChama) { proxChama = Time.time + 0.18f; ZonaDeFogo.Criar(this, Sobre(p), 2.2f * esc, 2.4f, 5f); }
                foreach (PlayerLocomotion pl in Jogadores())
                {
                    if (acertou.Contains(pl.transform)) continue;
                    Vector3 d = pl.transform.position - p; if (new Vector2(d.x, d.z).magnitude < 7.5f * esc && Mathf.Abs(d.y) < 9f) { acertou.Add(pl.transform); Ferir(pl.transform, 42f, p - transform.forward * 2f, 10f); }
                }
                yield return null;
            }
            // derrapa até parar
            Vector3 f = Plano(transform.forward).normalized; Vector3 p1 = transform.position; float dd = 1.3f;
            for (float t = 0f; t < dd && !morto; t += Time.deltaTime)
            {
                float k = 1f - (1f - t / dd) * (1f - t / dd); Vector3 p = p1 + f * 20f * k; p.y = Chao(p) + AltChao; transform.position = p;
                Emitir(Sobre(p), UnityEngine.Random.insideUnitSphere * 8f + Vector3.up * 4f, 3.5f * esc, 0.9f, 3); yield return null;
            }
            Tremor(0.35f, 0.9f); Explosao(Sobre(transform.position), 4.5f); aberturaAlvo = 0f;
            if (v < vezes - 1) yield return new WaitForSeconds(0.5f);
        }
        if (morto) yield break;
        // errou o alvo e ficou tonto: é a hora de bater!
        yield return Atordoar(Fase == 1 ? 5f : 3.5f);
        yield return LutaNoChao(Fase == 1 ? 8f : 6f);
    }

    // tonto: balança, estrelinhas giram em volta da cabeça e ele leva DANO EXTRA
    IEnumerator Atordoar(float seg)
    {
        atordoado = true; Pose(1); ampAsa = 3f; aberturaAlvo = 0.5f; Rugir(0.6f);
        foreach (GameObject e in estrelas) if (e != null) Destroy(e); estrelas.Clear();
        for (int i = 0; i < 6; i++) estrelas.Add(FxCena.Nuvem(transform.position, 1.7f * esc, new Color(1f, 0.92f, 0.4f, 1f), FxCena.TexBrilho(), 6));
        float t0 = Time.time;
        while (Time.time - t0 < seg && !morto)
        {
            float u = Time.time - t0; balanco = Mathf.Sin(u * 2.6f) * 11f;
            Vector3 topo = (bocaT != null ? bocaT.position : transform.position) + Vector3.up * (3.4f * esc);
            for (int i = 0; i < estrelas.Count; i++)
            {
                if (estrelas[i] == null) continue; float a = u * 3.2f + i / (float)estrelas.Count * Mathf.PI * 2f;
                estrelas[i].transform.position = topo + new Vector3(Mathf.Cos(a), 0.15f * Mathf.Sin(a * 2f), Mathf.Sin(a)) * 3.2f * esc;
                FxCena.Pintar(estrelas[i], new Color(1f, 0.92f, 0.4f, 0.75f + 0.25f * Mathf.Sin(u * 9f + i)));
            }
            yield return null;
        }
        foreach (GameObject e in estrelas) if (e != null) Destroy(e); estrelas.Clear();
        atordoado = false; balanco = 0f; aberturaAlvo = 0f;
    }

    // ---- ataque novo: LEQUE DE BOLAS DE FOGO (voando)
    IEnumerator LequeDeFogo(Transform alvo, int levas)
    {
        Pose(0);
        Vector3 ap = alvo.position; Vector3 ang = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;
        Vector3 posAtq = Plano(ap) + ang * 40f; posAtq.y = Chao(posAtq) + 15f * esc;
        if (Plano(posAtq - centro).magnitude > raioArena * 1.1f) posAtq = centro + Plano(posAtq - centro).normalized * raioArena * 1.1f + Vector3.up * 15f * esc;
        yield return Mover(posAtq, 1.5f, alvo);
        Pose(2); aberturaAlvo = 1f;
        for (int v = 0; v < levas && !morto; v++)
        {
            if (alvo == null) break;
            float lim = 0.9f; for (float t = 0f; t < lim; t += Time.deltaTime) { transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(alvo.position - transform.position), Time.deltaTime * 7f); yield return null; }
            Vector3 boca = bocaT != null ? bocaT.position : transform.position; Vector3 baseDir = (alvo.position + Vector3.up * 0.8f - boca).normalized;
            int n = sombrio ? 7 : 5; float abre = sombrio ? 70f : 52f; luzJato.intensity = 7f;
            for (int i = 0; i < n; i++) StartCoroutine(BolaDeFogo(boca, Quaternion.AngleAxis(Mathf.Lerp(-abre * 0.5f, abre * 0.5f, i / (float)(n - 1)), Vector3.up) * baseDir));
            Tremor(0.1f, 0.25f); yield return new WaitForSeconds(0.25f); luzJato.intensity = 0f;
            yield return new WaitForSeconds(Mathf.Max(0.4f, 0.8f / MulRitmo));
        }
        aberturaAlvo = 0f; Pose(0);
    }
    IEnumerator BolaDeFogo(Vector3 pos, Vector3 dir)
    {
        Color cor = sombrio ? new Color(0.75f, 0.3f, 1f, 0.95f) : new Color(1f, 0.6f, 0.15f, 0.95f);
        GameObject g = FxCena.Nuvem(pos, 3.6f, cor, FxCena.TexBrilho(), 5); float vel = 36f * MulRitmo;
        for (float t = 0f; t < 4.5f && !morto; t += Time.deltaTime)
        {
            pos += dir * vel * Time.deltaTime; g.transform.position = pos; Emitir(pos, -dir * 3f, 2.4f, 0.5f, 2);
            bool bateu = false;
            foreach (PlayerLocomotion pl in Jogadores()) if (Vector3.Distance(pl.transform.position + Vector3.up, pos) < 2.6f) { Ferir(pl.transform, 26f, pos, 4f); bateu = true; break; }
            if (!bateu && pos.y < Chao(pos) + 0.8f) { bateu = true; ZonaDeFogo.Criar(this, new Vector3(pos.x, Chao(pos), pos.z), 2.6f, 3.2f, 5f); }
            if (bateu) break; yield return null;
        }
        Explosao(pos, 2.4f); Destroy(g);
    }

    // ---- fase 2: RAIOS SOMBRIOS caem em cada jogador
    IEnumerator RaiosSombrios()
    {
        Vector3 alto = centro; alto.y = Chao(centro) + 34f * esc; yield return Mover(alto, 1.5f); Pose(1); ampAsa = 24f; Rugir(1.4f);
        for (int r = 0; r < 6 && !morto; r++)
        {
            foreach (PlayerLocomotion p in Jogadores()) StartCoroutine(RaioNoPonto(p.transform.position));
            for (int e = 0; e < 2; e++) StartCoroutine(RaioNoPonto(centro + UnityEngine.Random.insideUnitSphere * raioArena * 0.7f));
            yield return new WaitForSeconds(Mathf.Max(0.8f, 1.4f / MulRitmo));
        }
        yield return new WaitForSeconds(1.2f); Pose(0);
    }
    IEnumerator RaioNoPonto(Vector3 alvo)
    {
        alvo.y = Chao(alvo);
        yield return AvisoCirculo(alvo, 4.2f, 1.15f); if (morto) yield break;
        FxCena.Raio(alvo + Vector3.up * 120f + new Vector3(UnityEngine.Random.Range(-14f, 14f), 0f, UnityEngine.Random.Range(-14f, 14f)), alvo, 2.4f, 0.28f, new Color(0.8f, 0.45f, 1f, 1f));
        Explosao(alvo, 2.6f); Tremor(0.12f, 0.3f); ZonaDeFogo.Criar(this, alvo, 3f, 3f, 6f);
        foreach (PlayerLocomotion p in Jogadores()) { Vector3 d = p.transform.position - alvo; if (new Vector2(d.x, d.z).magnitude < 4.6f) Ferir(p.transform, 34f, alvo, 4f); }
    }

    // ---- fase 2: VÓRTICE puxa todo mundo para o centro e explode (fuja para longe, ou role na hora)
    IEnumerator Vortice()
    {
        Vector3 alto = centro; alto.y = Chao(centro) + 30f * esc; yield return Mover(alto, 1.4f); Pose(1); ampAsa = 22f; Rugir(1.3f);
        Vector3 c = centro; c.y = Chao(c); float dur = 4.4f, raioV = 15f;
        StartCoroutine(AvisoCirculo(c, raioV, dur));
        for (float t = 0f; t < dur && !morto; t += Time.deltaTime)
        {
            foreach (PlayerLocomotion p in Jogadores())
            {
                CharacterController cc = p.GetComponent<CharacterController>(); if (cc == null || !cc.enabled) continue;
                Vector3 d = Plano(c - p.transform.position); if (d.magnitude > 2.5f) cc.Move(d.normalized * 6.6f * Time.deltaTime);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f, rr = UnityEngine.Random.Range(4f, raioV * 1.6f); Vector3 q = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr + Vector3.up * UnityEngine.Random.Range(0.5f, 7f);
                Vector3 tang = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)); Emitir(q, tang * 15f - new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 7f, 2.4f, 0.7f);
            }
            Tremor(0.06f + 0.1f * (t / dur), 0.2f); yield return null;
        }
        if (morto) yield break;
        Tremor(0.4f, 1f); Explosao(c, 9f); StartCoroutine(OndaDeChoque(c, 18f));
        foreach (PlayerLocomotion p in Jogadores()) { if (Plano(p.transform.position - c).magnitude < raioV) Ferir(p.transform, 46f, c, 9f); }
        yield return new WaitForSeconds(0.8f); Pose(0);
    }

    // ---- fase 2: CARGA SOMBRIA. Ele pousa e junta uma nova gigante. Dano suficiente a tempo INTERROMPE e derruba o dragão
    IEnumerator CargaSombria()
    {
        Vector3 alto = centro; alto.y = Chao(centro) + 30f * esc; yield return Mover(alto, 1.4f);
        Vector3 pouso = centro; pouso.y = Chao(centro) + AltChao; yield return Pousar(pouso, null);
        Pose(1); ampAsa = 6f; aberturaAlvo = 1f;
        float dur = 7.5f, meta = Vida.Maxima * 0.05f, vida0 = Vida.Atual; bool quebrou = false; float raioNova = raioArena * 0.95f;
        StartCoroutine(AvisoCirculo(centro, raioNova, dur));
        GameObject orbe = FxCena.Nuvem(transform.position, 1f, new Color(0.45f, 0.08f, 0.8f, 0.9f), FxCena.TexBrilho(), 6), nucleo = FxCena.Nuvem(transform.position, 1f, new Color(1f, 0.8f, 1f, 0.95f), FxCena.TexBrilho(), 7);
        if (aura != null) aura.intensidade = 1.5f;
        for (float t = 0f; t < dur && !morto; t += Time.deltaTime)
        {
            cargaProg = Mathf.Clamp01((vida0 - Vida.Atual) / meta);
            if (cargaProg >= 1f) { quebrou = true; break; }
            float k = t / dur; Vector3 boca = bocaT != null ? bocaT.position : transform.position;
            Vector3 po = boca + transform.forward * 4f * esc + Vector3.up * 2f * esc; orbe.transform.position = po; nucleo.transform.position = po;
            orbe.transform.localScale = Vector3.one * Mathf.Lerp(4f, 26f, k) * esc; nucleo.transform.localScale = Vector3.one * Mathf.Lerp(2f, 11f, k) * esc;
            for (int i = 0; i < 3; i++) { Vector3 o = po + UnityEngine.Random.onUnitSphere * 22f * esc; Emitir(o, (po - o).normalized * 18f, 2f, 0.8f); }
            Tremor(0.04f + 0.1f * k, 0.2f); transform.rotation = Quaternion.Slerp(transform.rotation, Olhar(MaisPerto(transform.position) != null ? MaisPerto(transform.position).position - transform.position : transform.forward), Time.deltaTime * 2f);
            yield return null;
        }
        cargaProg = -1f; Vector3 pf = orbe.transform.position; Destroy(orbe); Destroy(nucleo);
        if (morto) yield break;
        if (quebrou)
        {
            // interrompida: a energia estoura nele mesmo e ele tomba
            Explosao(pf, 7f); Tremor(0.4f, 1.2f); StartCoroutine(OndaDeChoque(transform.position, 14f)); aberturaAlvo = 0.6f;
            if (aura != null) aura.intensidade = 0.3f;
            Quaternion r0 = transform.rotation; float lado = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            for (float t = 0f; t < 0.9f && !morto; t += Time.deltaTime) { transform.rotation = r0 * Quaternion.Euler(0f, 0f, 62f * lado * Mathf.SmoothStep(0f, 1f, t / 0.9f)); yield return null; }
            Tremor(0.3f, 0.8f); Explosao(Sobre(transform.position), 5f);
            yield return Atordoar(9f);
            for (float t = 0f; t < 1.0f && !morto; t += Time.deltaTime) { transform.rotation = Quaternion.Slerp(transform.rotation, r0, t / 1.0f); yield return null; }
            transform.rotation = r0; if (aura != null) aura.intensidade = 1f;
            if (!morto) { Rugir(1.4f); yield return Decolar(); }
        }
        else
        {
            // a nova negra explode
            Tremor(0.6f, 1.6f); Explosao(pf, 11f); StartCoroutine(OndaDeChoque(centro, raioNova * 0.5f)); StartCoroutine(OndaDeChoque(centro, raioNova));
            FxCena.Raio(pf + Vector3.up * 90f, pf, 3f, 0.4f, new Color(0.8f, 0.45f, 1f, 1f));
            foreach (PlayerLocomotion p in Jogadores()) { if (Plano(p.transform.position - centro).magnitude < raioNova) Ferir(p.transform, 68f, centro, 12f); }
            if (aura != null) aura.intensidade = 1f; aberturaAlvo = 0f;
            yield return new WaitForSeconds(1.3f); yield return Decolar();
        }
    }

    // barra da carga sombria (quanto falta para interromper)
    static GUIStyle estiloCarga;
    void OnGUI()
    {
        if (cargaProg < 0f || morto) return;
        float w = Mathf.Min(520f, Screen.width * 0.6f), h = 16f; Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.8f, w, h);
        if (estiloCarga == null) estiloCarga = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold };
        Color c0 = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f); GUI.DrawTexture(new Rect(r.x - 4f, r.y - 4f, w + 8f, h + 8f), Texture2D.whiteTexture);
        GUI.color = Color.Lerp(new Color(0.9f, 0.2f, 0.3f), new Color(0.5f, 1f, 0.55f), cargaProg); GUI.DrawTexture(new Rect(r.x, r.y, w * cargaProg, h), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.85f, 1f); GUI.Label(new Rect(r.x, r.y - 34f, w, 28f), "CARGA SOMBRIA! Ataque o dragão para interrompê-la!", estiloCarga);
        GUI.color = c0;
    }

    IEnumerator OndaDeChoque(Vector3 c, float raio)
    {
        LineRenderer lr = NovaLinha("Onda", 60, 0.9f, new Color(1f, 0.6f, 0.25f, 0.8f));
        for (float t = 0f; t < 0.7f; t += Time.deltaTime) { float k = t / 0.7f; AnelNoChao(lr, c, Mathf.Lerp(2f, raio * 1.8f, k)); lr.startColor = lr.endColor = new Color(1f, 0.6f, 0.25f, 0.8f * (1f - k)); yield return null; }
        avisos.Remove(lr.gameObject); Destroy(lr.gameObject);
    }

    IEnumerator Mordida()
    {
        // aviso de cone à frente da cabeça
        StartCoroutine(AvisoCone(transform, 17f, 28f, 0.75f)); aberturaAlvo = 0.7f;
        yield return new WaitForSeconds(0.75f);
        // o bote: o corpo avança e volta
        Vector3 o = transform.position, f = Plano(transform.forward).normalized; aberturaAlvo = 1f;
        for (float t = 0f; t < 0.18f; t += Time.deltaTime) { transform.position = Vector3.Lerp(o, o + f * 4f, t / 0.18f); yield return null; }
        foreach (PlayerLocomotion p in Jogadores()) { Vector3 d = Plano(p.transform.position - o); if (d.magnitude < 18f && Vector3.Angle(f, d) < 30f) Ferir(p.transform, 48f, o, 3f); }
        Tremor(0.1f, 0.25f);
        for (float t = 0f; t < 0.4f; t += Time.deltaTime) { transform.position = Vector3.Lerp(o + f * 4f, o, t / 0.4f); yield return null; }
        aberturaAlvo = 0f;
    }
    IEnumerator VarridaDeCauda()
    {
        StartCoroutine(AvisoCirculo(transform.position, 17f, 1.0f)); aberturaAlvo = 0.3f;
        yield return new WaitForSeconds(1.0f);
        Quaternion r0 = transform.rotation; float dur = 0.6f;
        for (float t = 0f; t < dur; t += Time.deltaTime) { transform.rotation = r0 * Quaternion.Euler(0f, 360f * (t / dur), 0f); yield return null; }
        transform.rotation = r0;
        Tremor(0.15f, 0.3f); StartCoroutine(OndaDeChoque(transform.position, 10f));
        foreach (PlayerLocomotion p in Jogadores()) { if (Plano(p.transform.position - transform.position).magnitude < 17f) Ferir(p.transform, 40f, transform.position, 6f); }
        aberturaAlvo = 0f;
    }
    IEnumerator BaterAsas()
    {
        ampAsa = 46f; Pose(0); aberturaAlvo = 0.4f;
        StartCoroutine(AvisoCirculo(transform.position, 15f, 0.8f)); yield return new WaitForSeconds(0.8f);
        Tremor(0.18f, 0.6f);
        for (int i = 0; i < 30; i++) Emitir(Sobre(transform.position) + Vector3.up * 0.4f, new Vector3(UnityEngine.Random.Range(-1f, 1f), 0.2f, UnityEngine.Random.Range(-1f, 1f)).normalized * 16f, 3.2f, 0.9f);
        foreach (PlayerLocomotion p in Jogadores()) { if (Plano(p.transform.position - transform.position).magnitude < 15f) Ferir(p.transform, 18f, transform.position, 11f); }
        yield return new WaitForSeconds(0.6f); ampAsa = 10f; Pose(1); aberturaAlvo = 0f;
    }
    IEnumerator SoproCurto()
    {
        StartCoroutine(AvisoCone(transform, 24f, 20f, 0.7f)); aberturaAlvo = 0.8f; yield return new WaitForSeconds(0.7f);
        Pose(2); aberturaAlvo = 1f; luzJato.intensity = 6f; float prox = 0f;
        for (float t = 0f; t < 1.7f && !morto; t += Time.deltaTime)
        {
            Vector3 boca = bocaT != null ? bocaT.position : transform.position, f = transform.forward; f.y = Mathf.Min(f.y, -0.05f);
            for (int i = 0; i < 6; i++) Emitir(boca + f * UnityEngine.Random.value * 4f, f.normalized * 24f, 2.2f, 0.7f);
            if (Time.time >= prox)
            {
                prox = Time.time + 0.3f; Vector3 o = Plano(transform.position), ff = Plano(transform.forward).normalized;
                foreach (PlayerLocomotion p in Jogadores()) { Vector3 d = Plano(p.transform.position - o); if (d.magnitude < 25f && Vector3.Angle(ff, d) < 22f) Ferir(p.transform, 9f, o); }
                ZonaDeFogo.Criar(this, o + ff * UnityEngine.Random.Range(8f, 22f), 2.6f, 3f, 5f);
            }
            yield return null;
        }
        luzJato.intensity = 0f; aberturaAlvo = 0f; Pose(1);
    }

    // ------------------------------------------------------------------ fase 2 (usado pela cena da fusão)
    public float Escala { get { return esc; } }
    public Vector3 Centro { get { return centro; } }
    public void RugirCena(float dur) { Rugir(dur); }
    // empurra todo mundo para longe do dragão (onda da transformação)
    public void Repelir(float dist)
    {
        foreach (PlayerLocomotion p in Jogadores()) { Vector3 d = Plano(p.transform.position - transform.position); if (d.sqrMagnitude < 1f) d = Vector3.right; StartCoroutine(Empurrao(p.transform, d.normalized, dist)); }
    }

    // teste (F8): já "acorda" na metade da vida, esperando a cena da fusão
    public void PrepararParaFase2()
    {
        despertou = true; pediuFase2 = true; Vida.DefinirAtual(Vida.Maxima * 0.5f); Vida.TornarImortal(900f);
        ThirdPersonCameraFollow.FocoDeChefe = transform;
    }

    // desce voando até o chão e pousa onde está
    public IEnumerator IrAoChaoParaCena()
    {
        Vector3 pouso = transform.position; pouso.y = Chao(pouso) + AltChao;
        if (transform.position.y - pouso.y > 2f) { Pose(1); yield return Pousar(pouso, null); }
        Pose(1); ampAsa = 8f; aberturaAlvo = 0f;
    }

    public AuraSombria CriarAura()
    {
        if (aura != null) return aura;
        GameObject g = new GameObject("Aura sombria do Drakthar"); aura = g.AddComponent<AuraSombria>();
        aura.tamanho = comprimento * 1.5f; aura.centro = corpoModelo; aura.cabeca = bocaT;
        foreach (Transform t in asasOssos) aura.pontos.Add(t); foreach (Transform t in maosOssos) aura.pontos.Add(t);
        if (bocaT != null) aura.pontos.Add(bocaT);
        if (modeloGo != null)
        {
            int n = 0; foreach (Transform t in modeloGo.GetComponentsInChildren<Transform>())
            {
                string nome = t.name.ToLower();
                if ((nome.Contains("tail") || nome.Contains("spine") || nome.Contains("neck") || nome.Contains("hip")) && (n++ % 2 == 0)) aura.pontos.Add(t);
            }
        }
        return aura;
    }

    // cresce até a escala final, no chão (corpo, colisores e tudo)
    public IEnumerator Crescer(float dur, float escFinal)
    {
        float e0 = esc;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            esc = Mathf.Lerp(e0, escFinal, Mathf.SmoothStep(0f, 1f, t / dur)); transform.localScale = Vector3.one * esc;
            Vector3 p = transform.position; p.y = Chao(p) + AltChao; transform.position = p; yield return null;
        }
        esc = escFinal; transform.localScale = Vector3.one * esc;
    }

    // escurece o modelo (k de 0 a 1) e acende um brilho avermelhado
    public void Tinta(float k)
    {
        if (matsModelo.Count == 0 && modeloGo != null)
        {
            foreach (Renderer r in modeloGo.GetComponentsInChildren<Renderer>()) foreach (Material m in r.materials)
            {
                matsModelo.Add(m); coresOrig.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white));
            }
        }
        for (int i = 0; i < matsModelo.Count; i++)
        {
            Material m = matsModelo[i]; if (m == null) continue; Color c = Color.Lerp(coresOrig[i], new Color(0.30f, 0.24f, 0.36f, coresOrig[i].a), k * 0.85f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.45f, 0.04f, 0.2f) * (k * 1.6f)); }
        }
    }

    // trocou de fase: fogo roxo, escala nova, e a luta recomeça mais forte
    public void IniciarFase2()
    {
        if (sombrio) return;
        sombrio = true; pediuFase2 = true; despertou = true;
        CorDoJato(true); Tinta(1f); CriarAura().intensidade = 1f;
        ThirdPersonCameraFollow.FocoDeChefe = transform;
        Vida.DesfazerImortal(); if (!morto) StartCoroutine(Luta());
    }
    void CorDoJato(bool sombra)
    {
        if (jato == null) return; var col = jato.colorOverLifetime; Gradient gr = new Gradient();
        if (sombra) gr.SetKeys(new[] { new GradientColorKey(new Color(0.95f, 0.85f, 1f), 0f), new GradientColorKey(new Color(0.7f, 0.3f, 1f), 0.25f), new GradientColorKey(new Color(0.4f, 0.08f, 0.8f), 0.55f), new GradientColorKey(new Color(0.1f, 0.02f, 0.2f), 1f) },
                                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
        else gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.97f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.65f, 0.2f), 0.25f), new GradientColorKey(new Color(1f, 0.3f, 0.06f), 0.55f), new GradientColorKey(new Color(0.25f, 0.08f, 0.04f), 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        if (luzJato != null) luzJato.color = sombra ? new Color(0.65f, 0.25f, 1f) : new Color(1f, 0.5f, 0.15f);
    }

    // ------------------------------------------------------------------ morte
    void Morrer()
    {
        if (morto) return;
        morto = true; StopAllCoroutines(); inicioMorte = Time.time;
        foreach (GameObject g in avisos) if (g != null) Destroy(g); avisos.Clear();
        if (luzJato != null) luzJato.intensity = 0f;
        atordoado = false; balanco = 0f; cargaProg = -1f; foreach (GameObject e in estrelas) if (e != null) Destroy(e); estrelas.Clear();
        if (ThirdPersonCameraFollow.FocoDeChefe == transform) ThirdPersonCameraFollow.FocoDeChefe = null;
        foreach (BoxCollider b in GetComponents<BoxCollider>()) b.enabled = false;
        PontoDaQueda = new Vector3(transform.position.x, Chao(transform.position), transform.position.z);
        StartCoroutine(Queda());
        AoMorrer?.Invoke(this);
    }

    // ------------------------------------------------------------------ A MORTE DE DRAKTHAR (dura ~19 s; quem filma usa TempoDeMorte)
    //   0 -  1,6 s  despenca e se arrebenta no chão
    //   1,6 - 4,6 s CONVULSÃO: treme, a aura falha e solta raios, luz branco-violeta vaza por dentro do corpo
    //   4,6 - 7,6 s EXPULSÃO: uma coluna de fumaça negra (o poder do Ladrão) é cuspida para o céu, raios caem, ondas de choque
    //   7,6 - 14,6  CINZAS: o corpo racha (brilho laranja), encolhe e se desfaz em cinzas e brasas levadas pelo vento
    //  14,6 - 19 s  CALMA: um pilar de luz dourada desce do céu e almas brilhantes sobem devagar
    public float TempoDeMorte { get { return inicioMorte < 0f ? -1f : Time.time - inicioMorte; } }
    public bool MorteTerminou { get; private set; }
    public Vector3 PontoDaQueda { get; private set; }
    float inicioMorte = -1f;

    ParticleSystem PSMorte(string nome, int max, bool cresce)
    {
        GameObject g = new GameObject(nome); Destroy(g, 40f);
        ParticleSystem ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = max; m.startLifetime = 3f; m.startSize = 2f;
        var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true; Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        if (cresce) { var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.7f)); var nz = ps.noise; nz.enabled = true; nz.strength = 2f; nz.frequency = 0.25f; }
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        Material mt = new Material(FxCena.Sprite()); mt.mainTexture = FxCena.TexNuvem(); r.sharedMaterial = mt; ps.Play(); return ps;
    }
    static void Brota(ParticleSystem ps, Vector3 pos, Vector3 vel, float tam, float vida, Color cor)
    {
        var ep = new ParticleSystem.EmitParams(); ep.position = pos; ep.velocity = vel; ep.startSize = tam; ep.startLifetime = vida; ep.startColor = cor; ps.Emit(ep, 1);
    }
    Vector3 PontoDoCorpo()
    {
        if (aura != null && aura.pontos.Count > 0) { Transform t = aura.pontos[UnityEngine.Random.Range(0, aura.pontos.Count)]; if (t != null) return t.position; }
        return corpoModelo != null ? corpoModelo.position + UnityEngine.Random.insideUnitSphere * 3f * esc : transform.position;
    }
    void PrepararMats()
    {
        if (matsModelo.Count > 0 || modeloGo == null) return;
        foreach (Renderer r in modeloGo.GetComponentsInChildren<Renderer>()) foreach (Material m in r.materials)
        { matsModelo.Add(m); coresOrig.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white)); }
    }
    void CorEBrilho(Color cor, Color brilho)
    {
        PrepararMats();
        foreach (Material m in matsModelo)
        {
            if (m == null) continue; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor); else if (m.HasProperty("_Color")) m.SetColor("_Color", cor);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", brilho); }
        }
    }

    IEnumerator Queda()
    {
        Pose(1); ampAsa = 6f; aberturaAlvo = 1f;
        Vector3 de = transform.position, chao = de; chao.y = Chao(de) + AltChao * 0.7f; Quaternion r0 = transform.rotation; float lado = de.x > centro.x ? 1f : -1f;
        FxCena.GarantirTela();
        // 1) despenca
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float k = t / 1.6f; transform.position = Vector3.Lerp(de, chao, k * k); transform.rotation = r0 * Quaternion.Euler(0f, 0f, 28f * k * lado);
            Emitir(transform.position, Vector3.up * 5f + UnityEngine.Random.insideUnitSphere * 4f, 3.5f * esc, 0.9f, 3); yield return null;
        }
        transform.position = chao; Tremor(0.5f, 1.6f); Explosao(chao, 9f); StartCoroutine(OndaDeChoque(PontoDaQueda, 22f));
        for (int i = 0; i < 8; i++) { Vector3 q = chao + UnityEngine.Random.insideUnitSphere * 9f; q.y = Chao(q); ZonaDeFogo.Criar(this, q, 2.6f, 7f, 0f); }
        ParticleSystem coluna = PSMorte("Coluna negra do dragao", 2500, true), cinza = PSMorte("Cinzas do dragao", 4000, true);
        GameObject lg = new GameObject("Luz da morte do dragao"); Destroy(lg, 40f); Light luz = lg.AddComponent<Light>(); luz.type = LightType.Point; luz.range = 90f; luz.intensity = 0f; luz.shadows = LightShadows.None;
        Vector3 peito = chao + Vector3.up * 3f * esc;

        // 2) convulsão: treme, a aura falha, luz vaza pelo corpo
        Rugir(2.6f); float dc = 3f; Vector3 localBase = corpoModelo != null ? corpoModelo.localPosition : Vector3.zero;
        for (float t = 0f; t < dc && !false; t += Time.deltaTime)
        {
            float k = t / dc; if (corpoModelo != null) corpoModelo.localPosition = localBase + UnityEngine.Random.insideUnitSphere * (0.25f + 0.5f * k) * esc;
            balanco = Mathf.Sin(t * 38f) * (3f + 7f * k); if (aura != null) aura.intensidade = Mathf.Abs(Mathf.Sin(t * 11f)) * 1.5f * (1f - 0.6f * k);
            luz.transform.position = peito; luz.color = Color.Lerp(new Color(0.6f, 0.15f, 1f), new Color(0.95f, 0.85f, 1f), k); luz.intensity = Mathf.Lerp(2f, 34f, k) * (0.8f + 0.2f * Mathf.Sin(t * 30f));
            CorEBrilho(Color.Lerp(new Color(0.3f, 0.24f, 0.36f), new Color(0.4f, 0.34f, 0.46f), k), new Color(0.7f, 0.45f, 1f) * (k * 1.8f * (0.7f + 0.3f * Mathf.Sin(t * 25f))));
            for (int i = 0; i < 2 + (int)(5 * k); i++) Brota(cinza, PontoDoCorpo(), UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(8f, 18f), UnityEngine.Random.Range(0.8f, 1.8f), UnityEngine.Random.Range(0.5f, 1f), new Color(0.9f, 0.75f, 1f));
            if (UnityEngine.Random.value < 0.15f + 0.35f * k) { Vector3 a = PontoDoCorpo(), b = a + UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(8f, 20f) * esc; b.y = Mathf.Max(b.y, Chao(b) + 0.5f); FxCena.Raio(a, b, 0.5f, 0.12f, new Color(0.8f, 0.5f, 1f, 1f)); }
            Tremor(0.15f + 0.3f * k, 0.3f); yield return null;
        }

        // 3) expulsão: o poder negro sai do peito em direção ao céu
        float de3 = 3f; float proxRaio = 0f, proxOnda = 0f; if (corpoModelo != null) corpoModelo.localPosition = localBase;
        for (float t = 0f; t < de3; t += Time.deltaTime)
        {
            float k = t / de3; luz.intensity = Mathf.Lerp(34f, 60f, k); luz.color = Color.Lerp(new Color(0.95f, 0.85f, 1f), Color.white, k);
            if (aura != null) aura.intensidade = Mathf.Lerp(1f, 2.4f, k);
            for (int i = 0; i < 14; i++)
            {
                Vector2 c = UnityEngine.Random.insideUnitCircle * 2.4f * esc; Vector3 p = peito + new Vector3(c.x, UnityEngine.Random.Range(0f, 4f), c.y);
                Brota(coluna, p, new Vector3(c.x * 3f, UnityEngine.Random.Range(40f, 72f), c.y * 3f), UnityEngine.Random.Range(4f, 9f) * esc, UnityEngine.Random.Range(1.6f, 2.6f), new Color(0.08f, 0.02f, 0.13f));
                if (i % 3 == 0) Brota(cinza, p, new Vector3(c.x * 2f, UnityEngine.Random.Range(55f, 85f), c.y * 2f), UnityEngine.Random.Range(1.5f, 3f), UnityEngine.Random.Range(1f, 1.8f), new Color(0.95f, 0.8f, 1f));
            }
            proxRaio -= Time.deltaTime; if (proxRaio <= 0f) { proxRaio = 0.45f; FxCena.Raio(peito + Vector3.up * 150f + new Vector3(UnityEngine.Random.Range(-25f, 25f), 0f, UnityEngine.Random.Range(-25f, 25f)), peito, 2.4f, 0.25f, new Color(0.85f, 0.6f, 1f, 1f)); }
            proxOnda -= Time.deltaTime; if (proxOnda <= 0f) { proxOnda = 0.8f; StartCoroutine(OndaDeChoque(PontoDaQueda, 26f)); }
            balanco = Mathf.Sin(t * 30f) * 5f; CorEBrilho(Color.Lerp(new Color(0.4f, 0.34f, 0.46f), new Color(0.5f, 0.48f, 0.5f), k), new Color(1f, 0.9f, 1f) * (1.8f + 1.5f * k));
            Tremor(0.45f, 0.3f); yield return null;
        }
        if (aura != null) aura.intensidade = 0f; balanco = 0f;
        FxCena.Branco = 0.75f; StartCoroutine(ClaraoGrande(peito, 110f)); Explosao(chao, 12f); Tremor(0.7f, 1.5f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { FxCena.Branco = Mathf.Lerp(0.75f, 0f, t / 0.6f); yield return null; }
        FxCena.Branco = 0f;

        // 4) o corpo racha, encolhe e vira cinzas levadas pelo vento
        float dd = 7f; Vector3 vento = new Vector3(0.8f, 0f, 0.35f).normalized; Vector3 c0 = transform.position;
        for (float t = 0f; t < dd; t += Time.deltaTime)
        {
            float k = t / dd, e = Mathf.Pow(Mathf.SmoothStep(0f, 1f, k), 1.3f), sc = Mathf.Max(0.001f, 1f - e);
            if (corpoModelo != null) corpoModelo.localScale = Vector3.one * sc;
            transform.position = new Vector3(c0.x, Mathf.Lerp(c0.y, Chao(c0) + AltChao * 0.35f, e), c0.z);
            CorEBrilho(Color.Lerp(new Color(0.5f, 0.48f, 0.5f), new Color(0.22f, 0.2f, 0.2f), k), new Color(1f, 0.38f, 0.07f) * (2.4f * Mathf.Sin(Mathf.Clamp01(k * 1.15f) * Mathf.PI) + 0.2f));
            luz.transform.position = peito + Vector3.down * 1.5f * e; luz.color = new Color(1f, 0.55f, 0.18f); luz.intensity = Mathf.Lerp(24f, 2f, k) * (0.85f + 0.15f * Mathf.Sin(t * 17f));
            int n = Mathf.RoundToInt(Mathf.Lerp(520f, 180f, k) * Time.deltaTime * (0.5f + sc) + UnityEngine.Random.value);
            for (int i = 0; i < n; i++)
            {
                bool brasa = UnityEngine.Random.value < 0.3f;
                Brota(cinza, PontoDoCorpo() + UnityEngine.Random.insideUnitSphere * 1.2f, vento * UnityEngine.Random.Range(5f, 15f) + Vector3.up * UnityEngine.Random.Range(2f, 9f) + UnityEngine.Random.insideUnitSphere * 2f,
                    UnityEngine.Random.Range(brasa ? 0.5f : 1f, brasa ? 1.1f : 2.6f) * (0.6f + 0.6f * esc), UnityEngine.Random.Range(3f, 5.5f), brasa ? new Color(1f, 0.5f + 0.3f * UnityEngine.Random.value, 0.12f) : new Color(0.42f, 0.41f, 0.43f));
            }
            Tremor(0.08f, 0.3f); yield return null;
        }
        if (modeloGo != null) modeloGo.SetActive(false);
        luz.intensity = 0f;

        // 5) calma: pilar de luz dourada e almas subindo
        GameObject pilar = FxCena.Nuvem(PontoDaQueda + Vector3.up * 105f, 1f, new Color(1f, 0.9f, 0.6f, 0f), FxCena.TexBrilho(), 2); pilar.transform.localScale = new Vector3(26f, 240f, 1f);
        GameObject nucleoP = FxCena.Nuvem(PontoDaQueda + Vector3.up * 105f, 1f, new Color(1f, 1f, 0.9f, 0f), FxCena.TexBrilho(), 3); nucleoP.transform.localScale = new Vector3(9f, 240f, 1f);
        GameObject lp = new GameObject("Luz do pilar"); Light lz = lp.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.85f, 0.5f); lz.range = 80f; lz.intensity = 0f; lz.shadows = LightShadows.None; lp.transform.position = PontoDaQueda + Vector3.up * 8f;
        int nAlmas = 14; var almas = new GameObject[nAlmas]; var par = new Vector3[nAlmas];
        for (int i = 0; i < nAlmas; i++) { almas[i] = FxCena.Nuvem(PontoDaQueda, 1.6f, new Color(1f, 0.95f, 0.8f, 0f), FxCena.TexBrilho(), 4); float a = UnityEngine.Random.value * 6.28f; par[i] = new Vector3(Mathf.Cos(a) * UnityEngine.Random.Range(2f, 9f) * esc * 0.7f, UnityEngine.Random.Range(0f, 3f), Mathf.Sin(a) * UnityEngine.Random.Range(2f, 9f) * esc * 0.7f); }
        float dm = 4.4f;
        for (float t = 0f; t < dm; t += Time.deltaTime)
        {
            float k = t / dm, al = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI); Vector3 pc = PontoDaQueda;
            FxCena.Pintar(pilar, new Color(1f, 0.85f, 0.5f, 0.42f * al)); FxCena.Pintar(nucleoP, new Color(1f, 1f, 0.92f, 0.55f * al)); lz.intensity = 14f * al;
            for (int i = 0; i < nAlmas; i++)
            {
                float u = t * (1.1f + 0.05f * i) + par[i].y; Vector3 p = pc + new Vector3(par[i].x + Mathf.Sin(u * 1.3f + i) * 1.4f, 1f + u * 3.2f, par[i].z + Mathf.Cos(u * 1.1f + i) * 1.4f);
                almas[i].transform.position = p; FxCena.Pintar(almas[i], new Color(1f, 0.95f, 0.8f, 0.85f * al * (0.7f + 0.3f * Mathf.Sin(t * 5f + i))));
            }
            if (UnityEngine.Random.value < 0.5f) Brota(cinza, pc + UnityEngine.Random.insideUnitSphere * 10f * esc, vento * 3f + Vector3.up * 2f, UnityEngine.Random.Range(0.6f, 1.4f), 3f, new Color(0.45f, 0.44f, 0.45f));
            yield return null;
        }
        Destroy(pilar); Destroy(nucleoP); Destroy(lp); foreach (GameObject a in almas) if (a != null) Destroy(a);
        MorteTerminou = true; Destroy(gameObject, 3f);
    }
    IEnumerator ClaraoGrande(Vector3 p, float alcance)
    {
        GameObject g = new GameObject("Clarao grande"); g.transform.position = p; Light l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.9f, 0.8f, 1f); l.range = alcance; l.shadows = LightShadows.None;
        for (float t = 0f; t < 0.9f; t += Time.deltaTime) { l.intensity = 40f * (1f - t / 0.9f); yield return null; }
        Destroy(g);
    }

    // teste (F9): já está na fase 2, com a aura, e espera morrer
    public void PrepararParaTesteDeMorte()
    {
        despertou = true; pediuFase2 = true; sombrio = true; esc = 1.5f; transform.localScale = Vector3.one * esc;
        CorDoJato(true); Tinta(1f); CriarAura().intensidade = 1f;
        Vector3 p = transform.position; p.y = Chao(p) + AltChao; transform.position = p; Pose(1); ampAsa = 8f;
    }
}
