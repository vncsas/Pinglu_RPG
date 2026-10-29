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

    // o palco fica bem longe do mapa, para nada do jogo aparecer atrás
    private static readonly Vector3 LUGAR = new Vector3(0f, -3000f, 0f);

    private Transform raiz;
    private Camera cam;
    private Camera camPrincipal;
    private int mascaraPrincipal;
    private FichaPersonagem atual;
    private readonly Dictionary<FichaPersonagem, RenderTexture> retratos = new Dictionary<FichaPersonagem, RenderTexture>();

    public bool Pronto { get; private set; }

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

        // luzes: principal quente, contraluz carmesim, preenchimento frio e brilho no chão
        Luz("Luz Principal", LightType.Spot, new Vector3(-2.5f, 4.5f, 4f), new Color(1f, 0.86f, 0.72f), 9f, 14f, 45f, true);
        Luz("Contraluz", LightType.Spot, new Vector3(2.2f, 3.2f, -3.2f), new Color(1f, 0.16f, 0.12f), 14f, 12f, 60f, false);
        Luz("Preenchimento", LightType.Point, new Vector3(3.5f, 1.8f, 3f), new Color(0.45f, 0.55f, 0.9f), 1.6f, 9f, 0f, false);
        Luz("Brasas no Chao", LightType.Point, new Vector3(0f, 0.25f, 0.6f), new Color(1f, 0.3f, 0.1f), 1.5f, 3.5f, 0f, false);

        Particulas(true);
        Particulas(false);

        StartCoroutine(TirarRetratos(fichas));
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
        // câmera respirando de leve (e se afastando quando entra mais gente)
        if (cam != null)
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
