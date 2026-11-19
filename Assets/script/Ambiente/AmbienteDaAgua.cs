using System.Collections.Generic;
using UnityEngine;

// Tudo que acontece NA água do lago do Pântano (fica no objeto "Lago do Pantano"; o menu Pinglu > 78 coloca):
//  - quem entra anda mais devagar; onde o fundo passa de ~1,3 m o personagem NADA (boia na superfície, sem pular nem rolar);
//  - ao entrar/sair e a cada passo na água nascem ondas na superfície (o shader Pinglu/AguaViva desenha) e respingos com som;
//  - o lago tem ondinhas sozinhas, som de água e, quando a CÂMERA fica debaixo d'água, a tela escurece esverdeada com névoa e bolhas;
//  - outros scripts (chefe, monstros) chamam AmbienteDaAgua.Ondular / Respingo para a água reagir a eles.
public class AmbienteDaAgua : MonoBehaviour
{
    public static AmbienteDaAgua Instancia { get; private set; }

    public Renderer superficie;                 // a malha "Agua" do lago
    public Material materialGota;               // gotas e bolhas (círculo macio, Resources/Agua/Gota)
    [Header("Nado")]
    public float profundidadeParaNadar = 1.35f;
    public float alturaDoCorpoNadando = 1.05f;  // quanto do corpo fica DENTRO da água ao nadar
    public float fatorAndandoNaAgua = 0.7f;
    public float fatorNadando = 0.5f;

    Vector3 centro; float nivel; Bounds caixa; Terrain terreno; float baseTerreno;
    bool pronto;
    static readonly Vector4[] ondas = new Vector4[8];
    static int proxOnda;
    ParticleSystem gotas, bolhas;
    AudioSource somAmbiente, somEfeito; AudioClip clipeRespingo;

    class Estado { public bool naAgua, nadando; public float proximoPasso; public Vector3 ultima; }
    readonly Dictionary<PlayerLocomotion, Estado> jogadores = new Dictionary<PlayerLocomotion, Estado>();
    float proximaBusca, proximaOnda;
    bool submersa; float tempoSubmersa;
    bool fogGuardado; bool fogLigado; FogMode fogModo; float fogDens; Color fogCor;

    public static bool Existe { get { return Instancia != null && Instancia.pronto; } }
    public static float Nivel { get { return Instancia != null ? Instancia.nivel : 0f; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar()
    {
        Instancia = null; proxOnda = 0;
        for (int i = 0; i < ondas.Length; i++) ondas[i] = new Vector4(0f, 0f, -100f, 0f);
    }

    // ------------------------------------------------------------------ consultas para outros scripts
    // quanto o fundo do lago está abaixo da água naquele ponto (0 = fora do lago)
    public static float Profundidade(Vector3 p)
    {
        AmbienteDaAgua a = Instancia; if (a == null || !a.pronto || a.terreno == null) return 0f;
        if (p.x < a.caixa.min.x || p.x > a.caixa.max.x || p.z < a.caixa.min.z || p.z > a.caixa.max.z) return 0f;
        float h = a.terreno.SampleHeight(p) + a.baseTerreno;
        return Mathf.Max(0f, a.nivel - h);
    }

    // uma onda na superfície (força ~0,3 a 2)
    public static void Ondular(Vector3 p, float forca)
    {
        if (!Existe) return;
        ondas[proxOnda] = new Vector4(p.x, p.z, Time.timeSinceLevelLoad, Mathf.Clamp(forca, 0.1f, 3f));
        proxOnda = (proxOnda + 1) % ondas.Length;
    }

    // gotas saltando + som (força ~0,5 a 3)
    public static void Respingo(Vector3 p, float forca)
    {
        if (!Existe) return;
        Instancia.EmitirGotas(new Vector3(p.x, Instancia.nivel, p.z), forca);
        Instancia.TocarRespingo(p, Mathf.Clamp01(forca * 0.4f));
    }

    // ------------------------------------------------------------------ início
    void Awake() { Instancia = this; }

    void Start()
    {
        terreno = Terrain.activeTerrain;
        if (superficie == null) { Transform t = transform.Find("Agua"); if (t != null) superficie = t.GetComponent<Renderer>(); }
        if (superficie == null || terreno == null) { enabled = false; return; }
        caixa = superficie.bounds; centro = caixa.center; nivel = caixa.center.y; baseTerreno = terreno.transform.position.y;
        for (int i = 0; i < ondas.Length; i++) ondas[i] = new Vector4(0f, 0f, -100f, 0f);
        CriarGotas();
        clipeRespingo = GerarRespingo();
        somEfeito = new GameObject("Som do respingo").AddComponent<AudioSource>(); somEfeito.transform.SetParent(transform); somEfeito.spatialBlend = 0.8f; somEfeito.playOnAwake = false;
        somAmbiente = new GameObject("Som do lago").AddComponent<AudioSource>(); somAmbiente.transform.SetParent(transform);
        somAmbiente.transform.position = centro; somAmbiente.clip = GerarAmbiente(); somAmbiente.loop = true; somAmbiente.spatialBlend = 1f;
        somAmbiente.rolloffMode = AudioRolloffMode.Linear; somAmbiente.minDistance = 30f; somAmbiente.maxDistance = 130f; somAmbiente.volume = 0.3f; somAmbiente.Play();
        pronto = true;
    }

    void OnDisable()
    {
        if (fogGuardado) SoltarNevoa();
    }

    // ------------------------------------------------------------------ a cada quadro
    void Update()
    {
        if (!pronto) return;
        Shader.SetGlobalVectorArray("_PingluOndas", ondas);
        Shader.SetGlobalFloat("_PingluOndasN", ondas.Length);

        if (Time.time >= proximaBusca)
        {
            proximaBusca = Time.time + 1f;
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.gameObject.activeInHierarchy && !jogadores.ContainsKey(p)) jogadores[p] = new Estado { ultima = p.transform.position };
            List<PlayerLocomotion> sumidos = null;
            foreach (PlayerLocomotion p in jogadores.Keys) if (p == null || !p.gameObject.activeInHierarchy) { if (sumidos == null) sumidos = new List<PlayerLocomotion>(); sumidos.Add(p); }
            if (sumidos != null) foreach (PlayerLocomotion p in sumidos) jogadores.Remove(p);
        }
        foreach (KeyValuePair<PlayerLocomotion, Estado> kv in jogadores) if (kv.Key != null) Atualizar(kv.Key, kv.Value);

        // ondinhas sozinhas (peixes, vento)
        if (Time.time >= proximaOnda)
        {
            proximaOnda = Time.time + Random.Range(0.6f, 1.8f);
            Camera cam = Camera.main;
            Vector3 ref0 = cam != null ? cam.transform.position : centro;
            for (int t = 0; t < 6; t++)
            {
                Vector2 c = Random.insideUnitCircle * 45f;
                Vector3 p = new Vector3(ref0.x + c.x, nivel, ref0.z + c.y);
                if (Profundidade(p) > 0.6f) { Ondular(p, Random.Range(0.25f, 0.5f)); break; }
            }
        }
        AtualizarSubmersao();
    }

    void Atualizar(PlayerLocomotion loco, Estado e)
    {
        Vector3 pos = loco.transform.position;
        float prof = Profundidade(pos);
        bool dentro = prof > 0.15f && !loco.Voando;
        bool nada = dentro && (e.nadando ? prof > profundidadeParaNadar - 0.2f : prof > profundidadeParaNadar);

        loco.Nadando = nada;
        loco.AlturaDeNado = nivel - alturaDoCorpoNadando;
        loco.FatorDeAgua = nada ? fatorNadando : dentro ? Mathf.Lerp(1f, fatorAndandoNaAgua, Mathf.Clamp01(prof / 0.9f)) : 1f;

        Vector3 ponto = new Vector3(pos.x, nivel, pos.z);
        if (dentro != e.naAgua)
        {
            Respingo(ponto, dentro ? 1.6f : 0.9f); Ondular(ponto, dentro ? 1.6f : 1f);
            e.naAgua = dentro;
        }
        e.nadando = nada;

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float vel = new Vector2(pos.x - e.ultima.x, pos.z - e.ultima.z).magnitude / dt;
        e.ultima = pos;
        if (dentro && vel > 0.6f && Time.time >= e.proximoPasso)
        {
            e.proximoPasso = Time.time + (nada ? 0.5f : Mathf.Lerp(0.5f, 0.26f, Mathf.Clamp01(vel / 5f)));
            Ondular(ponto, nada ? 0.9f : 0.5f + Mathf.Clamp01(vel / 5f) * 0.5f);
            if (vel > 2.4f || nada) { EmitirGotas(ponto, nada ? 0.7f : 0.9f); }
            if (vel > 3.2f) TocarRespingo(ponto, 0.25f);
        }
    }

    // ------------------------------------------------------------------ debaixo d'água (a câmera mergulhou)
    void AtualizarSubmersao()
    {
        Camera cam = Camera.main; bool agora = false;
        if (cam != null)
        {
            Vector3 c = cam.transform.position;
            agora = c.y < nivel - 0.03f && Profundidade(c) > 0.1f;
        }
        if (agora && !submersa) { GuardarNevoa(); tempoSubmersa = 0f; }
        if (!agora && submersa) SoltarNevoa();
        submersa = agora;
        if (submersa)
        {
            tempoSubmersa += Time.deltaTime;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.09f; RenderSettings.fogColor = new Color(0.05f, 0.2f, 0.15f);
            if (bolhas != null && cam != null)
            {
                bolhas.transform.position = cam.transform.position + cam.transform.forward * 2.5f;
                var em = bolhas.emission; em.enabled = true;
            }
        }
        else if (bolhas != null) { var em = bolhas.emission; em.enabled = false; }
    }

    void GuardarNevoa() { fogGuardado = true; fogLigado = RenderSettings.fog; fogModo = RenderSettings.fogMode; fogDens = RenderSettings.fogDensity; fogCor = RenderSettings.fogColor; }
    void SoltarNevoa()
    {
        if (!fogGuardado) return;
        RenderSettings.fog = fogLigado; RenderSettings.fogMode = fogModo; RenderSettings.fogDensity = fogDens; RenderSettings.fogColor = fogCor; fogGuardado = false;
    }

    void OnGUI()
    {
        if (!submersa || Event.current.type != EventType.Repaint) return;
        float a = Mathf.Clamp01(tempoSubmersa / 0.25f);
        Color antes = GUI.color;
        GUI.color = new Color(0.03f, 0.22f, 0.17f, 0.5f * a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(0f, 0.06f, 0.05f, 0.35f * a);                                   // escurece as bordas
        float b = Screen.height * 0.12f;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, b), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - b, Screen.width, b), Texture2D.whiteTexture);
        GUI.color = antes;
    }

    // ------------------------------------------------------------------ gotas, bolhas e sons
    void CriarGotas()
    {
        Material m = materialGota;
        if (m == null) { Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (sh == null) sh = Shader.Find("Sprites/Default"); m = new Material(sh); }

        gotas = NovoSistema("Gotas de água", m, 0.9f, 1.1f);
        bolhas = NovoSistema("Bolhas", m, 0.0f, 0.0f);
        var mb = bolhas.main; mb.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f); mb.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        mb.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f); mb.gravityModifier = -0.15f; mb.startColor = new Color(0.8f, 1f, 0.95f, 0.5f);
        var emb = bolhas.emission; emb.enabled = false; emb.rateOverTime = 22f;
        var sb = bolhas.shape; sb.enabled = true; sb.shapeType = ParticleSystemShapeType.Box; sb.scale = new Vector3(5f, 3f, 5f);
    }

    ParticleSystem NovoSistema(string nome, Material m, float gravidade, float _)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(transform);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 600;
        main.gravityModifier = gravidade; main.startSize = 0.14f; main.startLifetime = 1.1f; main.startSpeed = 0f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.95f, 0.92f), 0f), new GradientColorKey(new Color(0.7f, 0.85f, 0.8f), 1f) },
                   new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var rd = g.GetComponent<ParticleSystemRenderer>(); rd.sharedMaterial = m; rd.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
        return ps;
    }

    void EmitirGotas(Vector3 p, float forca)
    {
        if (gotas == null) return;
        int n = Mathf.RoundToInt(8f + 14f * Mathf.Clamp01(forca * 0.5f)) + (forca > 1.2f ? 10 : 0);
        for (int i = 0; i < n; i++)
        {
            Vector2 c = Random.insideUnitCircle * 0.35f * Mathf.Max(0.6f, forca);
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = p + new Vector3(c.x, 0.05f, c.y);
            ep.velocity = new Vector3(c.x * 3f, Random.Range(2.2f, 4.5f) * Mathf.Clamp(forca, 0.6f, 1.6f), c.y * 3f);
            ep.startSize = Random.Range(0.06f, 0.16f) * Mathf.Clamp(forca, 0.7f, 1.5f);
            ep.startLifetime = Random.Range(0.6f, 1.1f);
            gotas.Emit(ep, 1);
        }
    }

    void TocarRespingo(Vector3 p, float volume)
    {
        if (somEfeito == null || clipeRespingo == null) return;
        somEfeito.transform.position = p;
        somEfeito.pitch = Random.Range(0.85f, 1.15f);
        somEfeito.PlayOneShot(clipeRespingo, Mathf.Clamp01(volume + 0.2f));
    }

    // som de "splash" feito por código: ruído que cai rápido e vai ficando abafado
    static AudioClip GerarRespingo()
    {
        int sr = 22050, n = (int)(sr * 0.7f); float[] d = new float[n];
        System.Random r = new System.Random(5); float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float env = Mathf.Pow(1f - t, 2.4f) * (t < 0.015f ? t / 0.015f : 1f);
            float ruido = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (ruido - lp) * Mathf.Lerp(0.55f, 0.07f, t);
            d[i] = lp * env * 0.8f;
        }
        AudioClip c = AudioClip.Create("respingo", n, 1, sr, false); c.SetData(d, 0); return c;
    }

    // água batendo ao longe: ruído suave que sobe e desce devagar (volta ao começo sem estalo)
    static AudioClip GerarAmbiente()
    {
        int sr = 22050, n = sr * 6; float[] d = new float[n];
        System.Random r = new System.Random(11); float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float ruido = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (ruido - lp) * 0.04f; lp2 += (lp - lp2) * 0.2f;
            float onda = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 2f * 3f) * Mathf.Sin(t * Mathf.PI * 2f * 2f + 1f);
            d[i] = lp2 * onda * 2.2f;
        }
        int f = sr / 4;                                                                         // emenda: mistura o final com o começo
        for (int i = 0; i < f; i++) { float k = i / (float)f; d[i] = Mathf.Lerp(d[n - f + i], d[i], k); }
        AudioClip c = AudioClip.Create("lago", n - f, 1, sr, false); float[] cortado = new float[n - f]; System.Array.Copy(d, cortado, n - f); c.SetData(cortado, 0); return c;
    }
}
