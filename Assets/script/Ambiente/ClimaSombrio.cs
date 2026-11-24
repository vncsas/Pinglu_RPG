using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Clima sombrio e nublado. Mexa em "Escuridão" e "Neblina" no Inspector e
// veja o resultado na hora (funciona também fora do Play).
//   Escuridão 0 = dia nublado   |   1 = quase noite
[ExecuteAlways]
public class ClimaSombrio : MonoBehaviour
{
    [Header("Ajustes principais")]
    [Range(0f, 1f)] public float escuridao = 0.4f;
    [Range(0f, 1f)] public float neblina = 0.45f;
    [Tooltip("0 = cores normais, 1 = bem acinzentado")]
    [Range(0f, 1f)] public float cinzento = 0.45f;
    [Tooltip("Ajuste fino: aumente se estiver escuro demais para jogar")]
    [Range(-1f, 1f)] public float brilho = 0f;

    [Header("Luz de apoio (luar fraco que acompanha o jogador)")]
    public bool luzDeApoio = true;
    [Range(0f, 3f)] public float forcaLuzDeApoio = 1.2f;

    [Header("Cores da luz")]
    public Color luzDiaNublado = new Color(0.78f, 0.80f, 0.84f);
    public Color luzNoite = new Color(0.55f, 0.62f, 0.78f);
    public Color neblinaDia = new Color(0.56f, 0.59f, 0.64f);
    public Color neblinaNoite = new Color(0.24f, 0.27f, 0.33f);

    [Header("Efeitos no ar (só durante o Play)")]
    public bool nevoaBaixa = true;
    public bool poeiraNoAr = true;

    [Header("Referências (o menu Pinglu > 12 preenche)")]
    public Light sol;
    public Material ceu;
    public Volume volume;
    public Material materialParticula;

    // neblina definida pelo clima geral (os biomas misturam o tom deles por cima)
    public static Color NeblinaAtual;
    public static float DensidadeAtual;

    private float ultEscuridao = -1f, ultNeblina = -1f, ultCinzento = -1f, ultBrilho = -9f;
    private Transform efeitos;
    private Light apoio;

    private void OnEnable() { Aplicar(); }

    private void Update()
    {
        if (escuridao != ultEscuridao || neblina != ultNeblina || cinzento != ultCinzento || brilho != ultBrilho) Aplicar();
        if (efeitos != null && Camera.main != null)
            efeitos.position = Camera.main.transform.position;
        if (apoio != null)
        {
            Transform alvo = jogador != null ? jogador : (Camera.main != null ? Camera.main.transform : null);
            if (alvo != null) apoio.transform.position = alvo.position + Vector3.up * 3.5f - alvo.forward * 1.5f;
            apoio.intensity = forcaLuzDeApoio * Mathf.Lerp(0.3f, 1.2f, escuridao);
            apoio.enabled = luzDeApoio;
        }
    }

    private Transform jogador;

    private void Start()
    {
        if (!Application.isPlaying) return;
        CriarEfeitos();
        CharacterController cc = FindFirstObjectByType<CharacterController>();
        if (cc != null) jogador = cc.transform;
        GameObject g = new GameObject("Luz de Apoio");
        g.transform.SetParent(efeitos, true);
        apoio = g.AddComponent<Light>();
        apoio.type = LightType.Point;
        apoio.range = 14f;
        apoio.color = new Color(0.62f, 0.70f, 0.85f);
        apoio.shadows = LightShadows.None;
    }

    public void Aplicar()
    {
        ultEscuridao = escuridao; ultNeblina = neblina; ultCinzento = cinzento; ultBrilho = brilho;
        float t = escuridao;

        if (sol == null) sol = RenderSettings.sun;
        if (sol != null)
        {
            sol.intensity = Mathf.Lerp(1.6f, 0.75f, t);
            sol.color = Color.Lerp(luzDiaNublado, luzNoite, t);
            sol.shadowStrength = Mathf.Lerp(0.75f, 0.55f, t);
            Vector3 e = sol.transform.eulerAngles;
            sol.transform.rotation = Quaternion.Euler(Mathf.Lerp(40f, 24f, t), e.y, 0f);
        }

        // luz que vem "de todo lado" (ilumina o que está na sombra)
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.64f, 0.68f, 0.74f), new Color(0.34f, 0.39f, 0.50f), t);
        RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.52f, 0.54f, 0.57f), new Color(0.27f, 0.29f, 0.35f), t);
        RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.30f, 0.29f, 0.26f), new Color(0.14f, 0.14f, 0.16f), t);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.8f, 0.45f, t);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(neblinaDia, neblinaNoite, t);
        RenderSettings.fogDensity = Mathf.Lerp(0.002f, 0.012f, neblina);
        NeblinaAtual = RenderSettings.fogColor; NeblinaAtual.a = 1f;
        DensidadeAtual = RenderSettings.fogDensity;

        // recalcula a luz ambiente com as cores novas (sem isso, as sombras ficam pretas)
        DynamicGI.UpdateEnvironment();

        if (ceu != null)
        {
            RenderSettings.skybox = ceu;
            if (ceu.HasProperty("_Exposure")) ceu.SetFloat("_Exposure", Mathf.Lerp(1.05f, 0.45f, t));
            if (ceu.HasProperty("_Tint")) ceu.SetColor("_Tint", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.38f, 0.42f, 0.52f), t));
        }

        if (volume != null && volume.sharedProfile != null)
        {
            if (volume.sharedProfile.TryGet(out ColorAdjustments ca))
            {
                ca.postExposure.Override(Mathf.Lerp(0.3f, 0.1f, t) + brilho);
                ca.saturation.Override(Mathf.Lerp(0f, -45f, cinzento));
                ca.contrast.Override(10f);
            }
            if (volume.sharedProfile.TryGet(out Vignette v)) v.intensity.Override(Mathf.Lerp(0.18f, 0.3f, t));
            // o modo "ACES" escurece demais os tons médios; o "Neutral" mantém a leitura
            if (volume.sharedProfile.TryGet(out Tonemapping tm)) tm.mode.Override(TonemappingMode.Neutral);
        }
    }

    // ------------------------------------------------------------------
    //  Névoa rasteira e poeira, que acompanham a câmera
    // ------------------------------------------------------------------
    private void CriarEfeitos()
    {
        efeitos = new GameObject("Efeitos do Clima").transform;
        if (nevoaBaixa) Particulas("Nevoa Baixa", true);
        if (poeiraNoAr) Particulas("Poeira no Ar", false);
    }

    private void Particulas(string nome, bool nevoa)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(efeitos, false);
        go.transform.localPosition = nevoa ? new Vector3(0f, -1.2f, 0f) : Vector3.zero;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = nevoa ? new ParticleSystem.MinMaxCurve(10f, 16f) : new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = nevoa ? new ParticleSystem.MinMaxCurve(0.1f, 0.4f) : new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = nevoa ? new ParticleSystem.MinMaxCurve(7f, 13f) : new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        Color c = RenderSettings.fogColor;
        main.startColor = nevoa ? new Color(c.r * 1.3f, c.g * 1.3f, c.b * 1.3f, 0.10f) : new Color(0.75f, 0.72f, 0.65f, 0.6f);
        main.maxParticles = nevoa ? 120 : 300;
        main.prewarm = true;

        var em = ps.emission; em.rateOverTime = nevoa ? 9f : 40f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = nevoa ? new Vector3(70f, 1.5f, 70f) : new Vector3(40f, 10f, 40f);

        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        if (!nevoa)
        {
            var ruido = ps.noise; ruido.enabled = true; ruido.strength = 0.3f; ruido.frequency = 0.25f;
        }
        else
        {
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        }

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        if (materialParticula != null) r.sharedMaterial = materialParticula;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    private void OnDestroy()
    {
        if (efeitos != null) Destroy(efeitos.gameObject);
    }
}
