using UnityEngine;

// Portal do mundo (o portão "Portal de Ruina" da cena). Os jogadores chegam
// por ele: nascem dentro do arco e saem caminhando pela frente.
// Fica apagado; abre quando alguém chega e, depois, se fecha:
// o grupo fica preso no mundo.
public class PortalDoMundo : MonoBehaviour
{
    public static PortalDoMundo Atual { get; private set; }

    [Header("Aparência (o menu Pinglu > 23 preenche)")]
    public Material materialPortal;
    public Material materialParticula;
    public Color cor = new Color(0.55f, 0.45f, 1f);

    [Header("Tempo")]
    [Tooltip("Quanto tempo o portal fica aberto depois que alguém chega (segundos)")]
    public float tempoAberto = 3.2f;
    public float tempoParaFechar = 1.8f;

    [Header("Ajuste fino da abertura do arco")]
    public Vector3 deslocamentoDaAbertura = Vector3.zero;
    [Range(0.2f, 0.9f)] public float larguraDaAbertura = 0.46f;
    [Range(0.3f, 0.95f)] public float alturaDaAbertura = 0.72f;
    [Tooltip("Inverte o lado de onde os jogadores saem")]
    public bool inverterFrente;

    public Vector3 Frente { get; private set; }
    public Vector3 CentroNoChao { get; private set; }
    public Vector3 CentroDaAbertura { get; private set; }
    private Vector3 lateral;
    private float largura = 4f, altura = 6f;

    private Transform efeitos, abertura;
    private Vector3 escalaAbertura;
    private Material mat;
    private Light luz;
    private ParticleSystem faiscas;
    private Renderer rend;

    private enum Estado { Apagado, Aberto, Fechando }
    private Estado estado = Estado.Apagado;
    private float desde;
    private bool avisar;

    private void Awake()
    {
        Atual = this;
        Calcular();
        CriarEfeitos();
        Mostrar(false);
    }

    private void OnDestroy()
    {
        if (Atual == this) Atual = null;
        if (efeitos != null) Destroy(efeitos.gameObject);
    }

    private void Calcular()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero); bool tem = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        bool largoEmX = b.size.x >= b.size.z;
        lateral = largoEmX ? Vector3.right : Vector3.forward;
        Vector3 frente = largoEmX ? Vector3.forward : Vector3.right;
        largura = largoEmX ? b.size.x : b.size.z;
        altura = b.size.y;
        Vector3 baseCentro = new Vector3(b.center.x, b.min.y, b.center.z) + deslocamentoDaAbertura;

        // a frente é o lado virado para a vila (ou, sem vila, para o centro do mapa)
        Vector3 referencia = transform.position + frente;
        GameObject vila = GameObject.Find("Vila_Gerada");
        Terrain ter = Terrain.activeTerrain;
        if (vila != null) referencia = vila.transform.position;
        else if (ter != null) referencia = ter.transform.position + ter.terrainData.size * 0.5f;
        if (Vector3.Dot(referencia - baseCentro, frente) < 0f) frente = -frente;
        if (inverterFrente) frente = -frente;
        Frente = frente;
        CentroNoChao = baseCentro;
        CentroDaAbertura = baseCentro + Vector3.up * altura * (0.02f + alturaDaAbertura * 0.5f);
    }

    // lugar de chegada do jogador i (0 no meio, depois alternando para os lados)
    public Vector3 LugarDeChegada(int i)
    {
        float[] lados = { 0f, 0.9f, -0.9f, 1.8f, -1.8f };
        float lado = lados[Mathf.Clamp(i, 0, lados.Length - 1)];
        lado = Mathf.Clamp(lado, -largura * 0.3f, largura * 0.3f);
        Vector3 p = CentroNoChao + Frente * 0.4f + lateral * lado;
        Terrain t = Terrain.activeTerrain;
        if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y + 0.1f;
        return p;
    }

    public Quaternion RotacaoDeChegada { get { return Quaternion.LookRotation(Frente); } }

    // alguém está chegando: o portal abre (e depois se fecha sozinho)
    public void Clarao() { Clarao(true); }
    public void Clarao(bool avisarAoFechar)
    {
        avisar |= avisarAoFechar;
        estado = Estado.Aberto;
        desde = Time.time;
        Mostrar(true);
    }

    // ------------------------------------------------------------------
    private void CriarEfeitos()
    {
        if (materialPortal == null) return;
        // objeto separado: o arco tem escala e rotação próprias que distorceriam o efeito
        efeitos = new GameObject("Efeitos do Portal").transform;
        float w = largura * larguraDaAbertura, h = altura * alturaDaAbertura;
        abertura = new GameObject("Abertura").transform;
        abertura.SetParent(efeitos, false);
        abertura.position = CentroDaAbertura;
        abertura.rotation = Quaternion.LookRotation(-Frente);
        escalaAbertura = new Vector3(w, h, 1f);
        abertura.localScale = escalaAbertura;

        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "Redemoinho";
        q.transform.SetParent(abertura, false);
        mat = new Material(materialPortal);
        // a meia-lua do arco tem raio = metade da largura
        if (mat.HasProperty("_Arco")) mat.SetFloat("_Arco", Mathf.Clamp(1f - (w * 0.5f) / h, 0.1f, 0.95f));
        if (mat.HasProperty("_Aspecto")) mat.SetFloat("_Aspecto", h / Mathf.Max(w, 0.01f));
        if (mat.HasProperty("_Cor")) mat.SetColor("_Cor", cor);
        rend = q.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject gl = new GameObject("Luz do Portal");
        gl.transform.SetParent(efeitos, true);
        gl.transform.position = CentroDaAbertura + Frente * 1.2f;
        luz = gl.AddComponent<Light>();
        luz.type = LightType.Point; luz.color = cor; luz.range = 14f; luz.shadows = LightShadows.None;

        GameObject gp = new GameObject("Faiscas do Portal");
        gp.transform.SetParent(abertura, false);
        faiscas = gp.AddComponent<ParticleSystem>();
        faiscas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = faiscas.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.startLifetime = 1.4f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white, cor);
        var em = faiscas.emission; em.rateOverTime = 30f;
        var sh = faiscas.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f;
        var vel = faiscas.velocityOverLifetime; vel.enabled = true; vel.radial = new ParticleSystem.MinMaxCurve(-0.4f);
        if (materialParticula != null) gp.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialParticula;
    }

    private void Mostrar(bool ligado)
    {
        if (rend != null) rend.enabled = ligado;
        if (luz != null) luz.enabled = ligado;
        if (faiscas != null) { if (ligado) faiscas.Play(); else faiscas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    }

    private void Update()
    {
        if (mat == null || estado == Estado.Apagado) return;
        float t = Time.time - desde;
        float forca, tamanho, giro;
        bool temAbertura = mat.HasProperty("_Abertura");
        if (estado == Estado.Aberto)
        {
            // chega com o portal já aberto e forte (a tela ainda está clareando), e vai acalmando
            tamanho = Mathf.SmoothStep(0f, 1f, t / 0.25f);
            forca = Mathf.Lerp(3f, 1.8f, Mathf.Clamp01(t / tempoAberto));
            giro = Mathf.Lerp(2.5f, 0.6f, Mathf.Clamp01(t / 1.5f));
            if (t > tempoAberto) { estado = Estado.Fechando; desde = Time.time; }
        }
        else
        {
            // fechando: a luz é sugada para o centro, girando cada vez mais rápido, e apaga num clarão
            float k = Mathf.Clamp01(t / tempoParaFechar);
            tamanho = 1f - Mathf.SmoothStep(0f, 1f, k);
            forca = Mathf.Lerp(1.8f, 3.5f, k);
            giro = Mathf.Lerp(0.6f, 5f, k * k);
            if (k >= 1f)
            {
                estado = Estado.Apagado;
                Mostrar(false);
                Estouro();
                if (avisar) HUDJogador.Mostrar("O portal se fechou atrás de você...", 4f);
                avisar = false;
                return;
            }
        }
        float pulso = 1f + Mathf.Sin(Time.time * 2f) * 0.08f;
        if (temAbertura) mat.SetFloat("_Abertura", tamanho);   // o arco fica do tamanho da porta; a luz é que abre/fecha
        else abertura.localScale = new Vector3(escalaAbertura.x * Mathf.Max(tamanho, 0.001f), escalaAbertura.y * Mathf.Max(tamanho, 0.001f), 1f);
        if (mat.HasProperty("_Intensidade")) { mat.SetFloat("_Intensidade", forca * pulso); mat.SetFloat("_Velocidade", giro); }
        else mat.SetColor("_BaseColor", cor * (2.2f * forca * pulso));
        if (luz != null) luz.intensity = 3f * forca * pulso * Mathf.Max(tamanho, 0.2f);
    }

    // último clarão ao fechar
    private void Estouro()
    {
        GameObject g = new GameObject("Portal Fechando");
        g.transform.position = CentroDaAbertura;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white, cor);
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
        if (materialParticula != null) g.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialParticula;
        ps.Play(); ps.Emit(80);
        Light l = g.AddComponent<Light>();
        l.color = cor; l.range = 12f; l.intensity = 10f; l.shadows = LightShadows.None;
        g.AddComponent<SumirLuz>();
        Destroy(g, 2f);
    }
}
