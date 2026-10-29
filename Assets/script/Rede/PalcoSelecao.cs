using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Palco 3D da tela de seleção: o personagem escolhido aparece animado na
// frente de um cenário pintado, com luzes de cinema, brasas e fumaça.
// Também tira os retratos dos personagens para as cartas da seleção.
public class PalcoSelecao : MonoBehaviour
{
    public Material materialFundo;
    public Material materialChao;
    public Material materialParticula;
    [Header("Portal")]
    public GameObject modeloPortal;
    public Quaternion rotacaoPortal = Quaternion.identity;
    public Material materialPortal;
    public Color corPortal = new Color(0.55f, 0.45f, 1f);

    // o palco fica bem longe do mapa, para nada do jogo aparecer atrás
    private static readonly Vector3 LUGAR = new Vector3(0f, -3000f, 0f);

    private Transform raiz;
    private Camera cam;
    private Camera camPrincipal;
    private int mascaraPrincipal;
    private FichaPersonagem atual;
    private readonly Dictionary<FichaPersonagem, RenderTexture> retratos = new Dictionary<FichaPersonagem, RenderTexture>();

    public bool Pronto { get; private set; }

    // portal
    private const float PORTAL_X = -0.25f, PORTAL_Z = -7.5f, ALTURA_PORTAL = 6.2f;
    private Transform vortice;
    private Material matVortice;
    private Light luzPortal;
    private float intensidade = 1f, giro;
    private bool emTransicao;
    public float Clarao { get; private set; }   // 0..1: tela branca do fim da entrada no portal

    public void Montar(List<FichaPersonagem> fichas)
    {
        raiz = new GameObject("Palco da Selecao").transform;
        raiz.position = LUGAR;

        GameObject gc = new GameObject("Camera do Palco");
        gc.transform.SetParent(raiz, false);
        cam = gc.AddComponent<Camera>();
        cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.015f, 0.02f);
        cam.transform.localPosition = new Vector3(0.2f, 1.35f, 6.2f);
        cam.transform.LookAt(LUGAR + new Vector3(-0.25f, 1.05f, 0f));
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true; // brilho nas brasas e no contraluz
        camPrincipal = Camera.main;
        if (camPrincipal != null) { cam.depth = camPrincipal.depth + 10f; mascaraPrincipal = camPrincipal.cullingMask; camPrincipal.cullingMask = 0; }

        // cenário pintado ao fundo
        GameObject fundo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(fundo.GetComponent<Collider>());
        fundo.name = "Fundo";
        fundo.transform.SetParent(raiz, false);
        fundo.transform.localPosition = new Vector3(1.5f, 4f, -22f);
        fundo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        fundo.transform.localScale = new Vector3(44f, 22f, 1f);
        if (materialFundo != null) fundo.GetComponent<Renderer>().sharedMaterial = materialFundo;
        fundo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        // chão de pedra
        GameObject chao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(chao.GetComponent<Collider>());
        chao.name = "Chao";
        chao.transform.SetParent(raiz, false);
        chao.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        chao.transform.localScale = new Vector3(7f, 0.08f, 7f);
        if (materialChao != null) chao.GetComponent<Renderer>().sharedMaterial = materialChao;

        MontarPortal();

        // luzes: principal quente, contraluz carmesim, preenchimento frio e brilho no chão
        Luz("Luz Principal", LightType.Spot, new Vector3(-2.5f, 4.5f, 4f), new Color(1f, 0.86f, 0.72f), 9f, 14f, 45f, true);
        Luz("Contraluz", LightType.Spot, new Vector3(2.2f, 3.2f, -3.2f), new Color(1f, 0.16f, 0.12f), 14f, 12f, 60f, false);
        Luz("Preenchimento", LightType.Point, new Vector3(3.5f, 1.8f, 3f), new Color(0.45f, 0.55f, 0.9f), 1.6f, 9f, 0f, false);
        Luz("Brasas no Chao", LightType.Point, new Vector3(0f, 0.25f, 0.6f), new Color(1f, 0.3f, 0.1f), 1.5f, 3.5f, 0f, false);

        Particulas(true);
        Particulas(false);

        StartCoroutine(TirarRetratos(fichas));
    }

    // ------------------------------------------------------------------
    //  portão de pedra com o redemoinho mágico
    // ------------------------------------------------------------------
    private static Bounds Medir(GameObject g)
    {
        Bounds b = new Bounds(g.transform.position, Vector3.zero); bool tem = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private void MontarPortal()
    {
        if (modeloPortal == null) return;
        GameObject p = Instantiate(modeloPortal, raiz);
        p.name = "Portal";
        foreach (Collider c in p.GetComponentsInChildren<Collider>()) Destroy(c);
        p.transform.localScale = Vector3.one;
        p.transform.rotation = rotacaoPortal;
        Bounds b = Medir(p);
        // o arco precisa ficar "de frente" para a câmera (largo no X, fino no Z)
        if (b.size.z > b.size.x) { p.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * rotacaoPortal; b = Medir(p); }
        float esc = ALTURA_PORTAL / Mathf.Max(0.01f, b.size.y);
        p.transform.localScale = Vector3.one * esc;
        b = Medir(p);
        Vector3 alvo = LUGAR + new Vector3(PORTAL_X, 0f, PORTAL_Z);
        p.transform.position += alvo - new Vector3(b.center.x, b.min.y, b.center.z);
        b = Medir(p);

        if (materialPortal == null) return;
        // abertura no formato do arco: do chão até a meia-lua
        float w = b.size.x * 0.46f, h = b.size.y * 0.72f;
        Transform abertura = new GameObject("Abertura").transform;
        abertura.SetParent(raiz, true);
        abertura.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.02f + h * 0.5f, b.center.z);
        abertura.rotation = Quaternion.Euler(0f, 180f, 0f);
        abertura.localScale = new Vector3(w, h, 1f);
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "Redemoinho";
        q.transform.SetParent(abertura, false);
        matVortice = new Material(materialPortal);
        if (matVortice.HasProperty("_Arco")) matVortice.SetFloat("_Arco", Mathf.Clamp(1f - (w * 0.5f) / h, 0.1f, 0.95f));
        if (matVortice.HasProperty("_Aspecto")) matVortice.SetFloat("_Aspecto", h / Mathf.Max(w, 0.01f));
        if (matVortice.HasProperty("_Cor")) matVortice.SetColor("_Cor", corPortal);
        Renderer rq = q.GetComponent<Renderer>();
        rq.sharedMaterial = matVortice;
        rq.shadowCastingMode = ShadowCastingMode.Off;
        vortice = q.transform;

        GameObject gl = new GameObject("Luz do Portal");
        gl.transform.SetParent(raiz, true);
        gl.transform.position = vortice.position + Vector3.forward * 1.2f;
        luzPortal = gl.AddComponent<Light>();
        luzPortal.type = LightType.Point; luzPortal.color = corPortal; luzPortal.range = 12f; luzPortal.intensity = 5f;
        luzPortal.shadows = LightShadows.None;

        // faíscas sendo sugadas para dentro
        GameObject gp = new GameObject("Faiscas do Portal");
        gp.transform.SetParent(vortice, false);
        ParticleSystem ps = gp.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = true; m.prewarm = true; m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.startLifetime = 1.4f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white, corPortal);
        var em = ps.emission; em.rateOverTime = 40f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.6f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.radial = new ParticleSystem.MinMaxCurve(-0.45f);
        if (materialParticula != null) gp.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialParticula;
        ps.Play();
    }

    private Vector3 CentroDoPortal { get { return vortice != null ? vortice.position : LUGAR + new Vector3(PORTAL_X, 2.6f, PORTAL_Z); } }

    // ------------------------------------------------------------------
    //  todos caminham até o portal, somem num clarão e a tela fica branca
    // ------------------------------------------------------------------
    public void EntrarNoPortal(System.Action aoTerminar)
    {
        if (vortice == null || cam == null) { Clarao = 1f; aoTerminar(); return; }
        StartCoroutine(RotinaPortal(aoTerminar));
    }

    private IEnumerator RotinaPortal(System.Action aoTerminar)
    {
        emTransicao = true;
        foreach (Lugar l in lugares) Caminhar(l);
        const float DURACAO = 3.4f;
        Vector3 camIni = cam.transform.position;
        Vector3 camFim = new Vector3(CentroDoPortal.x, LUGAR.y + 2.2f, LUGAR.z + 1.5f);
        float t = 0f;
        while (t < DURACAO)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            intensidade = Mathf.Lerp(1f, 3.2f, t / DURACAO);
            Vector3 entrada = new Vector3(CentroDoPortal.x, LUGAR.y, CentroDoPortal.z);
            for (int i = 0; i < lugares.Count; i++)
            {
                GameObject m = lugares[i].modelo;
                if (m == null || !m.activeSelf) continue;
                Vector3 dir = entrada - m.transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) m.transform.rotation = Quaternion.Slerp(m.transform.rotation, Quaternion.LookRotation(dir.normalized), dt * 6f);
                if (t > 0.35f + i * 0.12f) m.transform.position += dir.normalized * Mathf.Min(dir.magnitude, 2.7f * dt);
                if (dir.magnitude < 0.7f) { Explosao(m.transform.position + Vector3.up * 1.1f); m.SetActive(false); }
            }
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.4f) / (DURACAO - 0.4f)));
            cam.transform.position = Vector3.Lerp(camIni, camFim, k);
            cam.transform.LookAt(CentroDoPortal);
            Clarao = Mathf.Pow(Mathf.Clamp01((t - (DURACAO - 1.5f)) / 1.5f), 1.6f); // a tela vai clareando até ficar branca
            yield return null;
        }
        Clarao = 1f;
        yield return new WaitForSecondsRealtime(0.35f);
        aoTerminar();
    }

    private void Caminhar(Lugar l)
    {
        if (!l.grafo.IsValid() || l.ficha == null || l.ficha.animacaoAndar == null) return;
        l.grafo.Disconnect(l.mixer, 1);
        AnimationClipPlayable andar = AnimationClipPlayable.Create(l.grafo, l.ficha.animacaoAndar);
        andar.SetApplyFootIK(false);
        l.grafo.Connect(andar, 0, l.mixer, 1);
        l.fimEntrada = -1f;
        l.mixer.SetInputWeight(0, 0f);
        l.mixer.SetInputWeight(1, 1f);
    }

    private void Explosao(Vector3 pos)
    {
        GameObject g = new GameObject("Clarao");
        g.transform.position = pos;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white, corPortal);
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
        if (materialParticula != null) g.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialParticula;
        ps.Play(); ps.Emit(60);
        g.transform.SetParent(raiz, true);
        Destroy(g, 1.5f);
    }

    private void Luz(string nome, LightType tipo, Vector3 pos, Color cor, float intensidade, float alcance, float angulo, bool sombra)
    {
        GameObject g = new GameObject(nome);
        g.transform.SetParent(raiz, false);
        g.transform.localPosition = pos;
        g.transform.LookAt(LUGAR + Vector3.up * 1.1f);
        Light l = g.AddComponent<Light>();
        l.type = tipo; l.color = cor; l.intensity = intensidade; l.range = alcance;
        if (tipo == LightType.Spot) l.spotAngle = angulo;
        l.shadows = sombra ? LightShadows.Soft : LightShadows.None;
    }

    private void Particulas(bool brasas)
    {
        GameObject g = new GameObject(brasas ? "Brasas" : "Fumaca");
        g.transform.SetParent(raiz, false);
        g.transform.localPosition = brasas ? new Vector3(0f, 0.1f, 0f) : new Vector3(0f, 0.05f, 1f);
        g.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = brasas ? 3.5f : 4f;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        if (brasas)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.1f), new Color(1f, 0.65f, 0.25f));
            em.rateOverTime = 35f;
            var ru = ps.noise; ru.enabled = true; ru.strength = 0.4f; ru.frequency = 0.6f;
        }
        else
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startColor = new Color(0.25f, 0.12f, 0.12f, 0.35f);
            em.rateOverTime = 6f;
        }
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        if (materialParticula != null) r.sharedMaterial = materialParticula;
        r.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
    }

    // ------------------------------------------------------------------
    //  retratos das cartas: o próprio modelo, enquadrado no rosto
    // ------------------------------------------------------------------
    private IEnumerator TirarRetratos(List<FichaPersonagem> fichas)
    {
        GameObject gc = new GameObject("Camera do Retrato");
        gc.transform.SetParent(raiz, false);
        Camera cr = gc.AddComponent<Camera>();
        cr.fieldOfView = 22f; cr.nearClipPlane = 0.05f;
        cr.clearFlags = CameraClearFlags.SolidColor; cr.backgroundColor = new Color(0.08f, 0.03f, 0.03f);
        cr.enabled = false;
        foreach (FichaPersonagem f in fichas)
        {
            if (f == null || !f.disponivel || f.modeloVisual == null || retratos.ContainsKey(f)) continue;
            GameObject m = Instantiate(f.modeloVisual, LUGAR, Quaternion.Euler(0f, 20f, 0f));
            m.AddComponent<ReceptorDoPalco>();
            Animator a = m.GetComponentInChildren<Animator>();
            PlayableGraph g = default(PlayableGraph);
            if (a != null && f.animacaoParado != null)
            {
                g = PlayableGraph.Create("retrato");
                AnimationPlayableOutput o = AnimationPlayableOutput.Create(g, "a", a);
                AnimationClipPlayable c = AnimationClipPlayable.Create(g, f.animacaoParado);
                c.SetApplyFootIK(false); c.SetTime(0.4f);
                o.SetSourcePlayable(c);
                g.Evaluate(0f);
            }
            Transform cabeca = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 alvo = cabeca != null ? cabeca.position : m.transform.position + Vector3.up * 1.6f;
            Vector3 frente = m.transform.forward;
            cr.transform.position = alvo + frente * 1.15f + m.transform.right * -0.25f + Vector3.up * 0.02f;
            cr.transform.LookAt(alvo + Vector3.down * 0.12f);
            RenderTexture rt = new RenderTexture(256, 320, 16);
            cr.targetTexture = rt;
            cr.enabled = true;
            yield return null;            // a câmera desenha um quadro
            yield return new WaitForEndOfFrame();
            cr.enabled = false;
            cr.targetTexture = null;
            retratos[f] = rt;
            if (g.IsValid()) g.Destroy();
            Destroy(m);
        }
        Destroy(gc);
        Pronto = true;
        if (pendente != null) { List<FichaPersonagem> p = pendente; pendente = null; MostrarGrupo(p); }
    }

    public Texture Retrato(FichaPersonagem f)
    {
        RenderTexture rt;
        return f != null && retratos.TryGetValue(f, out rt) ? rt : null;
    }

    // ------------------------------------------------------------------
    //  personagens no palco (1 a 4, lado a lado)
    // ------------------------------------------------------------------
    private class Lugar
    {
        public FichaPersonagem ficha;
        public GameObject modelo;
        public PlayableGraph grafo;
        public AnimationMixerPlayable mixer;
        public float fimEntrada = -1f, pesoEntrada;
        public Transform cabeca;
    }
    private readonly List<Lugar> lugares = new List<Lugar>();
    private List<FichaPersonagem> pendente;
    private float distAlvo = 6.2f, distAtual = 6.2f;
    private const float ESPACO = 1.5f;

    public void Mostrar(FichaPersonagem f) { MostrarGrupo(new List<FichaPersonagem> { f }); }

    public void MostrarGrupo(List<FichaPersonagem> fichas)
    {
        atual = fichas.Count > 0 ? fichas[0] : null;
        if (!Pronto) { pendente = new List<FichaPersonagem>(fichas); return; }
        // tira os que sobraram
        while (lugares.Count > fichas.Count) { Limpar(lugares[lugares.Count - 1]); lugares.RemoveAt(lugares.Count - 1); }
        for (int i = 0; i < fichas.Count; i++)
        {
            if (i >= lugares.Count) lugares.Add(new Lugar());
            if (lugares[i].ficha != fichas[i] || lugares[i].modelo == null) Criar(lugares[i], fichas[i]);
        }
        Arrumar();
    }

    private void Limpar(Lugar l)
    {
        if (l.grafo.IsValid()) l.grafo.Destroy();
        if (l.modelo != null) Destroy(l.modelo);
        l.modelo = null; l.ficha = null;
    }

    private void Criar(Lugar l, FichaPersonagem f)
    {
        Limpar(l);
        l.ficha = f;
        if (f == null || f.modeloVisual == null) return;
        l.modelo = Instantiate(f.modeloVisual, LUGAR, Quaternion.identity);
        l.modelo.transform.SetParent(raiz, true);
        l.modelo.AddComponent<ReceptorDoPalco>();
        Animator a = l.modelo.GetComponentInChildren<Animator>();
        l.cabeca = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Head) : null;
        if (a == null || f.animacaoParado == null) return;
        a.applyRootMotion = false;
        l.grafo = PlayableGraph.Create("Selecao");
        l.grafo.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
        AnimationPlayableOutput saida = AnimationPlayableOutput.Create(l.grafo, "anim", a);
        l.mixer = AnimationMixerPlayable.Create(l.grafo, 2);
        saida.SetSourcePlayable(l.mixer);
        AnimationClipPlayable parado = AnimationClipPlayable.Create(l.grafo, f.animacaoParado);
        parado.SetApplyFootIK(false);
        l.grafo.Connect(parado, 0, l.mixer, 0);
        if (f.animacaoEntrada != null)
        {
            AnimationClipPlayable entrada = AnimationClipPlayable.Create(l.grafo, f.animacaoEntrada);
            entrada.SetApplyFootIK(false);
            l.grafo.Connect(entrada, 0, l.mixer, 1);
            l.pesoEntrada = 1f;
            l.fimEntrada = Time.unscaledTime + f.animacaoEntrada.length - 0.3f;
        }
        else { l.pesoEntrada = 0f; l.fimEntrada = -1f; }
        l.mixer.SetInputWeight(0, 1f - l.pesoEntrada);
        l.mixer.SetInputWeight(1, l.pesoEntrada);
        l.grafo.Play();
    }

    // espalha os personagens lado a lado e afasta a câmera para caber todos
    private void Arrumar()
    {
        int n = lugares.Count;
        distAlvo = 6.2f + Mathf.Max(0, n - 1) * 1.35f;
        Vector3 posCam = LUGAR + new Vector3(0.2f, 1.35f, distAlvo);
        for (int i = 0; i < n; i++)
        {
            if (lugares[i].modelo == null) continue;
            Vector3 p = LUGAR + new Vector3((i - (n - 1) * 0.5f) * ESPACO, 0f, 0f);
            Vector3 olhar = posCam - p; olhar.y = 0f;
            lugares[i].modelo.transform.SetPositionAndRotation(p, Quaternion.LookRotation(olhar.normalized) * Quaternion.Euler(0f, n == 1 ? -12f : 0f, 0f));
        }
    }

    // onde fica (na tela) o alto da cabeça de cada personagem, para as etiquetas P1, P2...
    public bool PosicaoDaCabeca(int i, out Vector2 tela)
    {
        tela = Vector2.zero;
        if (cam == null || i < 0 || i >= lugares.Count || lugares[i].modelo == null) return false;
        Vector3 c = lugares[i].cabeca != null ? lugares[i].cabeca.position : lugares[i].modelo.transform.position + Vector3.up * 1.7f;
        Vector3 s = cam.WorldToScreenPoint(c + Vector3.up * 0.45f);
        if (s.z <= 0f) return false;
        tela = new Vector2(s.x, Screen.height - s.y);
        return true;
    }

    private void Update()
    {
        foreach (Lugar l in lugares)
        {
            if (!l.grafo.IsValid() || l.fimEntrada <= 0f || Time.unscaledTime <= l.fimEntrada) continue;
            // depois do golpe de apresentação, volta suave para a pose parada
            l.pesoEntrada = Mathf.MoveTowards(l.pesoEntrada, 0f, Time.unscaledDeltaTime / 0.3f);
            l.mixer.SetInputWeight(0, 1f - l.pesoEntrada);
            l.mixer.SetInputWeight(1, l.pesoEntrada);
            if (l.pesoEntrada <= 0f) l.fimEntrada = -1f;
        }
        if (vortice != null)
        {
            float pulso = 1f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.08f;
            if (matVortice.HasProperty("_Intensidade"))
            {
                // o efeito no formato do arco gira sozinho
                float k = Mathf.Clamp01((intensidade - 1f) / 2.2f);              // 0 parado ... 1 fim da entrada
                matVortice.SetFloat("_Intensidade", Mathf.Lerp(1.3f, 4.5f, k * k) * pulso);
                matVortice.SetFloat("_Velocidade", Mathf.Lerp(0.35f, 4f, k * k));
                if (matVortice.HasProperty("_Abertura")) matVortice.SetFloat("_Abertura", 1f);
            }
            else
            {
                giro += Time.unscaledDeltaTime * (35f + 260f * (intensidade - 1f));
                vortice.localRotation = Quaternion.Euler(0f, 0f, giro);
                matVortice.SetColor("_BaseColor", corPortal * (2.2f * intensidade * pulso));
            }
            if (luzPortal != null) luzPortal.intensity = 5f * intensidade * pulso;
        }
        // câmera respirando de leve (e se afastando quando entra mais gente)
        if (cam != null && !emTransicao)
        {
            float t = Time.unscaledTime;
            distAtual = Mathf.Lerp(distAtual, distAlvo, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            cam.transform.localPosition = new Vector3(0.2f + Mathf.Sin(t * 0.25f) * 0.08f, 1.35f + Mathf.Sin(t * 0.33f) * 0.03f, distAtual);
            cam.transform.LookAt(LUGAR + new Vector3(-0.25f, 1.05f, 0f));
        }
    }

    public void Desmontar()
    {
        foreach (Lugar l in lugares) Limpar(l);
        lugares.Clear();
        if (raiz != null) Destroy(raiz.gameObject);
        if (camPrincipal != null) camPrincipal.cullingMask = mascaraPrincipal;
        foreach (RenderTexture rt in retratos.Values) if (rt != null) rt.Release();
        retratos.Clear();
    }

    private void OnDestroy() { foreach (Lugar l in lugares) if (l.grafo.IsValid()) l.grafo.Destroy(); }
}

// Recebe os eventos das animações no palco (feitiços, espada...) para não dar
// aviso no Console. O relâmpago do cajado aparece de verdade, para enfeitar.
public class ReceptorDoPalco : MonoBehaviour
{
    public void LancarMagia() { }
    public void GuardarEspada() { }
    public void SacarEspada() { }
    public void SoltarRaio()
    {
        Transform ponta = null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == "StaffTip") ponta = t;
        System.Type tipo = System.Type.GetType("Raio");
        if (ponta == null || tipo == null) return;
        System.Reflection.MethodInfo criar = tipo.GetMethod("Criar");
        Vector3 dir = transform.forward * 6f + Vector3.right * 2f;
        if (criar != null) criar.Invoke(null, new object[] { ponta, dir, new Color(0.62f, 0.32f, 1f), 0.6f });
    }
}

// Chegada no mundo: o personagem sai de uma coluna de luz do portal.
public class ChegadaDoPortal : MonoBehaviour
{
    private Light luz;
    private float inicio;

    public static void Criar(Vector3 pos, Material mat, Color cor)
    {
        GameObject g = new GameObject("Chegada do Portal");
        g.transform.position = pos;
        ChegadaDoPortal c = g.AddComponent<ChegadaDoPortal>();
        c.inicio = Time.time;

        // coluna de luz subindo
        ParticleSystem col = Sistema(g.transform, "Coluna", mat, cor);
        var m = col.main; m.startLifetime = 0.7f; m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f); m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        var sh = col.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f;
        col.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var em = col.emission; em.rateOverTime = 160f;
        col.Play();
        c.StartCoroutine(c.Parar(col, 0.9f));

        // anel de faíscas no chão
        ParticleSystem anel = Sistema(g.transform, "Anel", mat, cor);
        var ma = anel.main; ma.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); ma.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f); ma.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        var sa = anel.shape; sa.shapeType = ParticleSystemShapeType.Circle; sa.radius = 0.3f;
        anel.transform.localPosition = Vector3.up * 0.1f;
        anel.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        anel.Play(); anel.Emit(70);

        c.luz = g.AddComponent<Light>();
        c.luz.type = LightType.Point; c.luz.color = cor; c.luz.range = 10f; c.luz.intensity = 8f; c.luz.shadows = LightShadows.None;
        Destroy(g, 4f);
    }

    private static ParticleSystem Sistema(Transform pai, string nome, Material mat, Color cor)
    {
        GameObject g = new GameObject(nome);
        g.transform.SetParent(pai, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white, cor);
        var em = ps.emission; em.rateOverTime = 0f;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    private IEnumerator Parar(ParticleSystem ps, float depois)
    {
        yield return new WaitForSeconds(depois);
        if (ps != null) { var em = ps.emission; em.rateOverTime = 0f; }
    }

    private void Update()
    {
        if (luz != null) luz.intensity = Mathf.Lerp(8f, 0f, (Time.time - inicio) / 2.5f);
    }
}
