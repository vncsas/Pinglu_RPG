using UnityEngine;

// Muda o clima conforme o bioma em que a câmera está: cor e força da
// neblina, vento e partículas no ar. A transição entre biomas é suave.
// O clima geral continua vindo do "Clima Sombrio"; aqui só se mistura por cima.
public class ClimaDosBiomas : MonoBehaviour
{
    public enum Efeito { Nenhum, Brasas, Vagalumes, Poeira, Esporos }

    [System.Serializable]
    public class Bioma
    {
        public string nome;
        public Vector3 centro;
        public float raio = 200f;
        public Color corDaNeblina = Color.gray;
        [Tooltip("1 = igual ao clima geral; 2 = neblina duas vezes mais forte")]
        public float forcaDaNeblina = 1f;
        public float vento = 0.5f;
        public Efeito efeito;
    }

    public Bioma[] biomas = new Bioma[0];
    [Tooltip("Largura da faixa de transição entre biomas (metros)")]
    public float transicao = 90f;
    [Tooltip("Quão rápido o clima muda ao entrar num bioma (maior = mais rápido)")]
    public float velocidade = 0.8f;
    public Material materialParticula;

    private Color corAtual;
    private float densidadeAtual;
    private WindZone vento;
    private float ventoBase;
    private ParticleSystem[] particulas;
    private Transform efeitos;
    private float[] pesos;

    private void Start()
    {
        corAtual = RenderSettings.fogColor;
        densidadeAtual = RenderSettings.fogDensity;
        vento = FindFirstObjectByType<WindZone>();
        if (vento != null) ventoBase = vento.windMain;
        pesos = new float[biomas.Length];
        efeitos = new GameObject("Efeitos dos Biomas").transform;
        particulas = new ParticleSystem[biomas.Length];
        for (int i = 0; i < biomas.Length; i++)
            if (biomas[i].efeito != Efeito.Nenhum) particulas[i] = CriarEfeito(biomas[i].efeito);
    }

    private void OnDestroy() { if (efeitos != null) Destroy(efeitos.gameObject); }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        efeitos.position = p;

        Color baseCor = ClimaSombrio.NeblinaAtual.a > 0f ? ClimaSombrio.NeblinaAtual : RenderSettings.fogColor;
        float baseDens = ClimaSombrio.DensidadeAtual > 0f ? ClimaSombrio.DensidadeAtual : RenderSettings.fogDensity;
        Color cor = baseCor; float dens = baseDens; float v = ventoBase;

        for (int i = 0; i < biomas.Length; i++)
        {
            Bioma b = biomas[i];
            Vector2 d = new Vector2(p.x - b.centro.x, p.z - b.centro.z);
            float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(b.raio - transicao * 0.5f, b.raio + transicao * 0.5f, d.magnitude));
            pesos[i] = w;
            // mantém o quão escuro o clima geral está, mudando só o tom
            float brilho = Mathf.Max(baseCor.maxColorComponent, 0.01f) / Mathf.Max(b.corDaNeblina.maxColorComponent, 0.01f);
            Color tom = b.corDaNeblina * brilho; tom.a = 1f;
            cor = Color.Lerp(cor, Color.Lerp(baseCor, tom, 0.75f), w);
            dens = Mathf.Lerp(dens, baseDens * b.forcaDaNeblina, w);
            v = Mathf.Lerp(v, b.vento, w);
            if (particulas[i] != null) { var em = particulas[i].emission; em.rateOverTime = TaxaDoEfeito(b.efeito) * w; }
        }

        float k = 1f - Mathf.Exp(-velocidade * Time.deltaTime);
        corAtual = Color.Lerp(corAtual, cor, k);
        densidadeAtual = Mathf.Lerp(densidadeAtual, dens, k);
        RenderSettings.fogColor = corAtual;
        RenderSettings.fogDensity = densidadeAtual;
        if (vento != null) vento.windMain = Mathf.Lerp(vento.windMain, v, k);
    }

    private static float TaxaDoEfeito(Efeito e)
    {
        switch (e)
        {
            case Efeito.Brasas: return 60f;
            case Efeito.Vagalumes: return 12f;
            case Efeito.Poeira: return 70f;
            case Efeito.Esporos: return 25f;
        }
        return 0f;
    }

    private ParticleSystem CriarEfeito(Efeito e)
    {
        GameObject go = new GameObject("Efeito " + e);
        go.transform.SetParent(efeitos, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 500;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
        var em = ps.emission; em.rateOverTime = 0f;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var ruido = ps.noise; ruido.enabled = true;

        switch (e)
        {
            case Efeito.Brasas:      // brasas subindo
                sh.scale = new Vector3(30f, 2f, 30f); go.transform.localPosition = Vector3.down * 2f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f); main.gravityModifier = -0.08f;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.1f), new Color(1f, 0.6f, 0.2f));
                ruido.strength = 0.6f; ruido.frequency = 0.5f;
                break;
            case Efeito.Vagalumes:
                sh.scale = new Vector3(30f, 4f, 30f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 1f, 0.4f), new Color(1f, 0.95f, 0.5f));
                ruido.strength = 0.5f; ruido.frequency = 0.4f;
                break;
            case Efeito.Poeira:      // poeira levada pelo vento
                sh.scale = new Vector3(40f, 6f, 40f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f); main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                main.startColor = new Color(0.75f, 0.7f, 0.6f, 0.6f);
                go.transform.localRotation = Quaternion.Euler(0f, 60f, 0f);
                ruido.strength = 1f; ruido.frequency = 0.3f;
                break;
            default:                 // esporos flutuando na floresta
                sh.scale = new Vector3(25f, 6f, 25f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                main.startColor = new Color(0.8f, 0.9f, 0.75f, 0.5f);
                ruido.strength = 0.3f; ruido.frequency = 0.3f;
                break;
        }
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        if (materialParticula != null) r.sharedMaterial = materialParticula;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }
}
