using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Luz e neblina de DIA nos campos (estilo Zelda/Fable): sol quente e forte, céu azulado, sombras suaves, névoa leve e clara,
// cores vivas. Na Floresta Sombria, no cemitério e nos biomas sombrios (pantano, terras queimadas, reino destruído)
// o clima sombrio de sempre continua: a mistura é gradual, conforme o jogador anda.
// Funciona por cima do "Clima Sombrio" e do "Clima dos Biomas": guarda os valores deles e só mistura.
[DefaultExecutionOrder(500)]
public class ClimaDeDia : MonoBehaviour
{
    [Header("Sol e céu")]
    public float intensidadeSol = 2.5f;
    public Color corDoSol = new Color(1f, 0.95f, 0.83f);
    [Range(20f, 80f)] public float alturaDoSol = 46f;
    [Range(0f, 1f)] public float forcaDasSombras = 0.9f;
    public Color ceuAmbiente = new Color(0.50f, 0.66f, 0.92f);
    public Color horizonteAmbiente = new Color(0.62f, 0.68f, 0.64f);
    public Color chaoAmbiente = new Color(0.36f, 0.34f, 0.28f);
    public float intensidadeAmbiente = 1.25f;
    public float exposicaoDoCeu = 1.35f;
    public Color tomDoCeu = new Color(0.62f, 0.76f, 1f);

    [Header("Neblina")]
    public Color corDaNeblina = new Color(0.74f, 0.83f, 0.93f);
    public float densidadeDaNeblina = 0.0024f;

    [Header("Imagem (pós-processamento)")]
    public float exposicaoPos = 0.7f;
    public float saturacaoPos = 16f;
    public float contrastePos = 12f;
    public float vinheta = 0.1f;

    [Header("Teste")]
    [Tooltip("Liga o dia em qualquer lugar do mapa (para testar a aparência)")]
    public bool forcarDia = false;
    [Tooltip("Quanto é dia onde o jogador está agora (0 = sombrio, 1 = dia). Só para olhar.")]
    [Range(0f, 1f)] public float pesoAtual;

    [Header("Onde é dia")]
    [Tooltip("Distância (m) além do raio de cada bioma sombrio onde já é dia completo")]
    public float distanciaAteODia = 120f;
    [Tooltip("Velocidade da mudança entre dia e clima sombrio")]
    public float velocidade = 0.6f;

    private Light sol;
    private Volume volume;
    private ClimaDosBiomas biomas;
    private LimiteDaFloresta limite;
    private MiniMapa mapa;
    private Light apoio;
    private ParticleSystem nevoaBaixa;
    private float peso, pesoAplicado = -1f, proximoUpdateGI;
    private bool capturado; private float proximoLog;

    // valores do clima sombrio (guardados uma vez)
    private float solI, solSombra, solX, solY, ambI, reflexo, expCeu;
    private Color solCor, ambCeu, ambEq, ambChao, tomCeu0;
    private Material ceu;
    private float posExp, posSat, posCon, posVin;
    private ColorAdjustments ca; private Vignette vg;

    private void Start()
    {
        biomas = FindFirstObjectByType<ClimaDosBiomas>();
        limite = FindFirstObjectByType<LimiteDaFloresta>();
        mapa = FindFirstObjectByType<MiniMapa>();
        ClimaSombrio cs = FindFirstObjectByType<ClimaSombrio>();
        if (cs != null) { sol = cs.sol; volume = cs.volume; }
        if (sol == null) sol = RenderSettings.sun;
        peso = AlvoDoPeso();
    }

    private void Capturar()
    {
        capturado = true;
        if (sol != null) { solI = sol.intensity; solCor = sol.color; solSombra = sol.shadowStrength; solX = sol.transform.eulerAngles.x; solY = sol.transform.eulerAngles.y; }
        ambI = RenderSettings.ambientIntensity; reflexo = RenderSettings.reflectionIntensity;
        ambCeu = RenderSettings.ambientSkyColor; ambEq = RenderSettings.ambientEquatorColor; ambChao = RenderSettings.ambientGroundColor;
        ceu = RenderSettings.skybox;
        if (ceu != null) { if (ceu.HasProperty("_Exposure")) expCeu = ceu.GetFloat("_Exposure"); if (ceu.HasProperty("_Tint")) tomCeu0 = ceu.GetColor("_Tint"); }
        if (volume != null)
        {
            VolumeProfile perfil = volume.profile;      // cópia do perfil: não altera o arquivo
            perfil.TryGet(out ca); perfil.TryGet(out vg);
            if (ca != null) { posExp = ca.postExposure.value; posSat = ca.saturation.value; posCon = ca.contrast.value; }
            if (vg != null) posVin = vg.intensity.value;
        }
        GameObject g = GameObject.Find("Luz de Apoio"); if (g != null) apoio = g.GetComponent<Light>();
        GameObject n = GameObject.Find("Nevoa Baixa"); if (n != null) nevoaBaixa = n.GetComponent<ParticleSystem>();
    }

    // 1 = campo aberto (dia completo), 0 = dentro de um lugar sombrio
    private float AlvoDoPeso()
    {
        Camera cam = Camera.main;
        if (cam == null) return peso;
        Vector3 p = cam.transform.position;
        float w = 1f;
        if (limite != null && limite.Valido) w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, -80f, limite.DistanciaDaBorda(p.x, p.z)));
        if (biomas != null)
            foreach (ClimaDosBiomas.Bioma b in biomas.biomas)
            {
                float d = new Vector2(p.x - b.centro.x, p.z - b.centro.z).magnitude;
                w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(b.raio - 20f, b.raio + distanciaAteODia, d));
            }
        if (mapa != null)
            foreach (MiniMapa.Marcador m in mapa.marcadores)
                if (m.tipo == MiniMapa.Tipo.Cemiterio)
                {
                    float d = new Vector2(p.x - m.posicao.x, p.z - m.posicao.z).magnitude;
                    w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(170f, 170f + distanciaAteODia * 1.5f, d));
                }
        return w;
    }

    private void LateUpdate()
    {
        if (!capturado) { Capturar(); }
        float alvo = forcarDia ? 1f : AlvoDoPeso();
        peso = Mathf.Lerp(peso, alvo, 1f - Mathf.Exp(-velocidade * Time.deltaTime));
        float w = peso; pesoAtual = peso;
        if (Time.time >= proximoLog) { proximoLog = Time.time + 8f; Debug.Log("[ClimaDeDia] dia = " + peso.ToString("0.00") + " (0 = sombrio, 1 = dia)" + (sol == null ? " | SEM SOL ligado" : "")); }

        if (sol != null)
        {
            sol.intensity = Mathf.Lerp(solI, intensidadeSol, w);
            sol.color = Color.Lerp(solCor, corDoSol, w);
            sol.shadowStrength = Mathf.Lerp(solSombra, forcaDasSombras, w);
            sol.transform.rotation = Quaternion.Euler(Mathf.Lerp(solX, alturaDoSol, w), solY, 0f);
        }
        RenderSettings.ambientSkyColor = Color.Lerp(ambCeu, ceuAmbiente, w);
        RenderSettings.ambientEquatorColor = Color.Lerp(ambEq, horizonteAmbiente, w);
        RenderSettings.ambientGroundColor = Color.Lerp(ambChao, chaoAmbiente, w);
        RenderSettings.ambientIntensity = Mathf.Lerp(ambI, intensidadeAmbiente, w);
        RenderSettings.reflectionIntensity = Mathf.Lerp(reflexo, 1f, w);
        // neblina: o clima dos biomas já escreveu o valor de agora; mistura com a neblina de dia
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, corDaNeblina, w);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, densidadeDaNeblina, w);
        if (ceu != null)
        {
            if (ceu.HasProperty("_Exposure")) ceu.SetFloat("_Exposure", Mathf.Lerp(expCeu, exposicaoDoCeu, w));
            if (ceu.HasProperty("_Tint")) ceu.SetColor("_Tint", Color.Lerp(tomCeu0, tomDoCeu, w));
        }
        if (ca != null)
        {
            ca.postExposure.Override(Mathf.Lerp(posExp, exposicaoPos, w));
            ca.saturation.Override(Mathf.Lerp(posSat, saturacaoPos, w));
            ca.contrast.Override(Mathf.Lerp(posCon, contrastePos, w));
        }
        if (vg != null) vg.intensity.Override(Mathf.Lerp(posVin, vinheta, w));
        if (apoio != null) apoio.intensity *= 1f - 0.9f * w;                 // sem "luar" de apoio de dia
        if (nevoaBaixa != null) { var em = nevoaBaixa.emission; em.rateOverTime = 9f * (1f - 0.8f * w); }

        if (Mathf.Abs(w - pesoAplicado) > 0.02f && Time.time >= proximoUpdateGI)
        {
            pesoAplicado = w; proximoUpdateGI = Time.time + 0.5f;
            DynamicGI.UpdateEnvironment();      // recalcula a luz ambiente com as cores novas
        }
    }
}
