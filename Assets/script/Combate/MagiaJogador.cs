using System.Collections.Generic;
using UnityEngine;

// Combate da Maga. As animações chamam estes métodos por evento:
//   LancarMagia() -> esfera arcana sai da palma esquerda (osso MagicHand_L)
//   SoltarRaio()  -> relâmpago sai do cristal do cajado (osso StaffTip) e
//                    atinge todos os inimigos na linha
// Segurando o botão direito, aparece a barreira mágica (bloqueia o dano).
public class MagiaJogador : MonoBehaviour
{
    [Header("Aparência (o menu Pinglu > 24 preenche)")]
    public Material materialBrilho;
    public Material materialRastro;
    public Material materialBarreira;
    [ColorUsage(false, true)] public Color cor = new Color(0.62f, 0.32f, 1f);

    [Header("Esfera arcana")]
    public float danoEsfera = 20f;
    public float velocidadeEsfera = 24f;
    public float alcanceEsfera = 35f;

    [Header("Relâmpago do cajado")]
    public float danoRaio = 40f;
    public float alcanceRaio = 14f;
    public float larguraRaio = 0.9f;

    [Header("Mira automática")]
    [Tooltip("Ângulo em volta do centro da câmera em que ela mira sozinha no inimigo")]
    public float anguloMira = 22f;

    private Transform mao, ponta;
    private Animator anim;
    private Transform barreira;
    private float tamBarreira;
    private static readonly int HASH_BLOCK = Animator.StringToHash("Block");

    private void Awake()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "MagicHand_L") mao = t;
            if (t.name == "StaffTip") ponta = t;
        }
        foreach (Animator a in GetComponentsInChildren<Animator>(true)) if (a.runtimeAnimatorController != null) { anim = a; break; }
        CriarBarreira();
    }

    // ------------------------------------------------------------------
    //  eventos das animações
    // ------------------------------------------------------------------
    public void LancarMagia()
    {
        if (!Dono()) return;
        Vector3 de = mao != null ? mao.position : transform.position + Vector3.up * 1.3f + transform.forward * 0.5f;
        Vector3 dir = Mira(de, alcanceEsfera);
        EsferaArcana.Criar(de, dir * velocidadeEsfera, alcanceEsfera / velocidadeEsfera, danoEsfera, this);
    }

    public void SoltarRaio()
    {
        Vector3 de = ponta != null ? ponta.position : transform.position + Vector3.up * 1.6f;
        Vector3 dir = Mira(de, alcanceRaio);
        // o desenho do raio é o do colega (classe Raio, em MagiaMaga.cs)
        System.Type tipo = System.Type.GetType("Raio");
        if (tipo != null)
        {
            System.Reflection.MethodInfo criar = tipo.GetMethod("Criar");
            if (criar != null) criar.Invoke(null, new object[] { ponta != null ? ponta : transform, dir * alcanceRaio, cor, 0.6f });
        }
        if (!Dono()) return;
        ThirdPersonCameraFollow.Tremer(0.12f, 0.25f);
        HashSet<Vida> atingidos = new HashSet<Vida>();
        foreach (RaycastHit h in Physics.SphereCastAll(de, larguraRaio, dir, alcanceRaio, ~0, QueryTriggerInteraction.Ignore))
        {
            Vida v = h.collider.GetComponentInParent<Vida>();
            if (v == null || v.Morto || EhAliado(v.transform) || !atingidos.Add(v)) continue;
            if (v.ReceberDano(danoRaio, transform.position)) Acertou();
        }
    }

    // ------------------------------------------------------------------
    // só quem controla o personagem causa dano (no multiplayer, as cópias dos outros só mostram o efeito)
    private bool Dono()
    {
        PlayerLocomotion l = GetComponent<PlayerLocomotion>();
        return l == null || l.enabled;
    }

    public bool EhAliado(Transform t)
    {
        return t == transform || t.IsChildOf(transform) || t.GetComponentInParent<PlayerLocomotion>() != null;
    }

    public void Acertou()
    {
        PlayerLocomotion l = GetComponent<PlayerLocomotion>();
        if (l != null) l.MarcarCombate();
        CombateJogador c = GetComponent<CombateJogador>();
        if (c != null) c.GanharEspecial(6f);
    }

    // para onde a câmera aponta, puxando para o inimigo mais perto do centro
    private Vector3 Mira(Vector3 de, float alcance)
    {
        Camera cam = Camera.main;
        Vector3 frente = cam != null ? cam.transform.forward : transform.forward;
        frente.y = Mathf.Clamp(frente.y, -0.2f, 0.25f);
        frente.Normalize();
        Vida melhor = null; float melhorAng = anguloMira;
        foreach (Collider c in Physics.OverlapSphere(transform.position, alcance, ~0, QueryTriggerInteraction.Ignore))
        {
            Vida v = c.GetComponentInParent<Vida>();
            if (v == null || v.Morto || EhAliado(v.transform)) continue;
            Vector3 alvo = v.transform.position + Vector3.up * 1.1f - de;
            float ang = Vector3.Angle(new Vector3(frente.x, 0f, frente.z), new Vector3(alvo.x, 0f, alvo.z));
            if (ang < melhorAng) { melhorAng = ang; melhor = v; }
        }
        if (melhor != null) return (melhor.transform.position + Vector3.up * 1.1f - de).normalized;
        return frente;
    }

    // ------------------------------------------------------------------
    //  barreira mágica (aparece com o parâmetro "Block", que vai pela rede)
    // ------------------------------------------------------------------
    private void CriarBarreira()
    {
        if (materialBarreira == null) return;
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(b.GetComponent<Collider>());
        b.name = "Barreira Magica";
        b.transform.SetParent(transform, false);
        b.transform.localPosition = Vector3.up * 1f;
        b.transform.localScale = Vector3.zero;
        MeshRenderer r = b.GetComponent<MeshRenderer>();
        r.sharedMaterial = materialBarreira;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        barreira = b.transform;
    }

    private void Update()
    {
        if (barreira == null || anim == null) return;
        bool ativa = anim.GetBool(HASH_BLOCK);
        tamBarreira = Mathf.MoveTowards(tamBarreira, ativa ? 1f : 0f, Time.deltaTime * 7f);
        float pulso = 1f + Mathf.Sin(Time.time * 6f) * 0.03f;
        barreira.localScale = Vector3.one * 2.3f * tamBarreira * pulso;
        barreira.gameObject.SetActive(tamBarreira > 0.01f);
    }
}

// ----------------------------------------------------------------------
//  Esfera arcana: voa, brilha, deixa rastro e explode no que acertar
// ----------------------------------------------------------------------
public class EsferaArcana : MonoBehaviour
{
    private Vector3 vel;
    private float fim, dano;
    private MagiaJogador dono;

    public static void Criar(Vector3 pos, Vector3 velocidade, float vida, float dano, MagiaJogador dono)
    {
        GameObject g = new GameObject("Esfera Arcana");
        g.transform.position = pos;
        EsferaArcana e = g.AddComponent<EsferaArcana>();
        e.vel = velocidade; e.fim = Time.time + vida; e.dano = dano; e.dono = dono;

        // núcleo brilhante
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.startLifetime = 0.2f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white, dono.cor);
        var em = ps.emission; em.rateOverTime = 70f;
        var sh = ps.shape; sh.enabled = false;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = dono.materialBrilho;
        ps.Play();

        // rastro
        TrailRenderer tr = g.AddComponent<TrailRenderer>();
        tr.time = 0.25f; tr.minVertexDistance = 0.05f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 0f));
        tr.sharedMaterial = dono.materialRastro;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(dono.cor, 0.3f), new GradientColorKey(dono.cor, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = gr;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = dono.cor; l.range = 4f; l.intensity = 3f; l.shadows = LightShadows.None;
    }

    private void Update()
    {
        Vector3 passo = vel * Time.deltaTime;
        RaycastHit h;
        if (Physics.SphereCast(transform.position, 0.3f, passo.normalized, out h, passo.magnitude, ~0, QueryTriggerInteraction.Ignore)
            && (dono == null || !dono.EhAliado(h.collider.transform)))
        {
            transform.position = h.point;
            Vida v = h.collider.GetComponentInParent<Vida>();
            if (v != null && !v.Morto && v.ReceberDano(dano, transform.position - vel.normalized) && dono != null) dono.Acertou();
            Explodir();
            return;
        }
        transform.position += passo;
        if (Time.time > fim) Explodir();
    }

    private void Explodir()
    {
        GameObject g = new GameObject("Explosao Arcana");
        g.transform.position = transform.position;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white, dono != null ? dono.cor : Color.magenta);
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
        if (dono != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = dono.materialBrilho;
        ps.Play(); ps.Emit(30);
        Light l = g.AddComponent<Light>();
        l.color = dono != null ? dono.cor : Color.magenta; l.range = 6f; l.intensity = 6f;
        g.AddComponent<SumirLuz>();
        Destroy(g, 1f);
        Destroy(gameObject);
    }
}

public class SumirLuz : MonoBehaviour
{
    private Light l;
    private void Awake() { l = GetComponent<Light>(); }
    private void Update() { if (l != null) l.intensity = Mathf.MoveTowards(l.intensity, 0f, Time.deltaTime * 20f); }
}
