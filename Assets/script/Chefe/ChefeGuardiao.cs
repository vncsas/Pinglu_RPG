using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// O Guardião das Lápides: golem de pedra gigante, chefe do cemitério.
// O corpo tem um esqueleto simples (criado pelo menu Pinglu > 64) e todas as animações são feitas aqui, por código:
// surgir do buraco, andar, golpe de cima para baixo, pisão com onda de choque e desmoronar ao morrer.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Vida))]
public partial class ChefeGuardiao : MonoBehaviour
{
    public string nome = "Guardião das Lápides";
    public float vidaBase = 1800f;
    public float velocidade = 2.6f;
    public float danoGolpe = 38f, danoPisao = 26f;
    public float alcanceGolpe = 6.5f, raioPisao = 11f;

    public event Action<ChefeGuardiao> AoMorrer;
    public Vida Vida { get; private set; }
    public bool Surgiu { get; private set; }
    public bool Lutando { get { return Surgiu && estado != Estado.Morto && estado != Estado.Escondido; } }

    private enum Estado { Escondido, Surgindo, Parado, Perseguindo, Golpe, Pisao, Morto, Transformando, Pedra, Raio }
    private Estado estado = Estado.Escondido;
    private CharacterController cc;
    private readonly Dictionary<string, Transform> ossos = new Dictionary<string, Transform>();
    private readonly Dictionary<string, Quaternion> repouso = new Dictionary<string, Quaternion>();
    private Transform esqueleto;
    private float fase, proximoAtaque, tempoEstado, vy, tranco, flinch;
    private Transform alvo;
    private bool pisaoNoRodizio;

    static readonly string[] NOMES = { "Pelvis", "Spine", "Chest", "Head", "BracoSup_D", "BracoSup_E", "Antebraco_D", "Antebraco_E", "Mao_D", "Mao_E", "Coxa_D", "Coxa_E", "Canela_D", "Canela_E", "Pe_D", "Pe_E" };

    private void Awake()
    {
        cc = GetComponent<CharacterController>(); Vida = GetComponent<Vida>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Esqueleto") esqueleto = t;
            foreach (string n in NOMES) if (t.name == n) { ossos[n] = t; repouso[n] = t.localRotation; }
        }
        Vida.AoLevarDano += (d, o) => flinch = 1f;
        barra = BarraDeChefe.Criar(gameObject, Vida, nome, ApresentacaoDoChefe.Guardiao().fonte, () => Surgiu && estado != Estado.Morto);
        Vida.AoMorrer += Morrer;
        IniciarFase2();
        peles = GetComponentsInChildren<SkinnedMeshRenderer>(); if (peles.Length > 0) pele = peles[0].material;
        cc.slopeLimit = 85f; cc.stepOffset = 1.0f;     // precisa conseguir subir a rampa do buraco
        cc.enabled = false;
        // convidado no multiplayer: o host comanda o chefe (a rede do chefe ainda não existe)
    }

    // Faz o Guardião sair do chão em "segundos". "profundidade" é quanto ele começa enterrado.
    public void Surgir(float segundos = 6f, float profundidade = 7f) { if (estado == Estado.Escondido) StartCoroutine(RotinaSurgir(segundos, profundidade)); }

    private IEnumerator RotinaSurgir(float dur, float prof)
    {
        estado = Estado.Surgindo;
        Vector3 fim = transform.position, ini = fim - Vector3.up * prof;
        transform.position = ini;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / dur);
            transform.position = Vector3.Lerp(ini, fim, k);
            // subindo: braços abertos pedindo apoio nas bordas do buraco, cabeça para cima
            float a = Mathf.Sin(Time.time * 9f) * 3f * (1f - k);
            P("BracoSup_D", -50f + a, 0f, -35f); P("BracoSup_E", -50f - a, 0f, 35f);
            P("Antebraco_D", -30f); P("Antebraco_E", -30f);
            P("Chest", -8f); P("Head", -14f); P("Coxa_D", -10f); P("Coxa_E", -10f);
            if (Time.frameCount % 4 == 0) TremorDeCamera.Tremer(0.12f * (1f - k) + 0.04f, 0.3f);
            yield return null;
        }
        transform.position = fim;
        // ruge e bate no peito
        for (float r = 0f; r < 1.6f; r += Time.deltaTime)
        {
            float b = Mathf.Sin(r * 14f) * 12f * Mathf.Clamp01(1.6f - r);
            P("BracoSup_D", -80f + b, 0f, -10f); P("BracoSup_E", -80f - b, 0f, 10f); P("Antebraco_D", -90f); P("Antebraco_E", -90f);
            P("Chest", -14f); P("Head", -20f);
            if (r < 0.3f) TremorDeCamera.Tremer(0.25f, 0.8f);
            yield return null;
        }
        cc.enabled = true; Surgiu = true; estado = Estado.Perseguindo; proximoAtaque = Time.time + 1.5f;
    }

    // ------------------------------------------------------------ poses
    private void P(string osso, float x, float y = 0f, float z = 0f)
    {
        Transform t; Quaternion r;
        if (ossos.TryGetValue(osso, out t) && repouso.TryGetValue(osso, out r)) t.localRotation = r * Quaternion.Euler(x, y, z);
    }

    private void PoseDeRepouso(float suavizar)
    {
        foreach (string n in NOMES) { Transform t = ossos.ContainsKey(n) ? ossos[n] : null; if (t != null) t.localRotation = Quaternion.Slerp(t.localRotation, repouso[n], suavizar); }
    }

    private void Andar(float vel)
    {
        fase += Time.deltaTime * (3.2f + vel);
        float s = Mathf.Sin(fase), c = Mathf.Cos(fase);
        P("Coxa_D", -s * 26f); P("Coxa_E", s * 26f);
        P("Canela_D", Mathf.Max(0f, s) * 38f); P("Canela_E", Mathf.Max(0f, -s) * 38f);
        P("Pe_D", Mathf.Max(0f, -s) * -10f); P("Pe_E", Mathf.Max(0f, s) * -10f);
        P("BracoSup_D", s * 16f, 0f, -4f); P("BracoSup_E", -s * 16f, 0f, 4f);
        P("Antebraco_D", -10f - Mathf.Max(0f, s) * 12f); P("Antebraco_E", -10f - Mathf.Max(0f, -s) * 12f);
        P("Chest", 3f, s * 6f); P("Spine", 2f, -s * 3f); P("Head", -3f, -s * 5f);
        if (esqueleto != null) esqueleto.localPosition = new Vector3(0f, -Mathf.Abs(c) * 0.12f, 0f);
        // cada passo pesado faz o chão tremer um pouco
        float passo = Mathf.Abs(s);
        if (passo < 0.08f && Time.time - tranco > 0.35f) { tranco = Time.time; TremorDeCamera.Tremer(0.04f, 0.25f); }
    }

    private void Respirar()
    {
        float r = Mathf.Sin(Time.time * 1.6f);
        P("Chest", r * 1.5f); P("BracoSup_D", r * 2f, 0f, -4f); P("BracoSup_E", r * 2f, 0f, 4f); P("Head", -r * 1.5f);
        P("Antebraco_D", -8f); P("Antebraco_E", -8f);
        if (esqueleto != null) esqueleto.localPosition = Vector3.Lerp(esqueleto.localPosition, Vector3.zero, Time.deltaTime * 6f);
    }

    // ------------------------------------------------------------ comportamento
    private void Update()
    {
        if (estado == Estado.Escondido || estado == Estado.Surgindo || estado == Estado.Morto) return;
        AtalhoDeTeste(); AcompanharProximidade();
        flinch = Mathf.MoveTowards(flinch, 0f, Time.deltaTime * 3f);
        float rach = Mathf.Clamp01((0.92f - Vida.Fracao) / 0.92f) * 0.95f + flinch * 0.18f;
        if (Fase2) rach = Mathf.Max(rach, 1.15f);                       // na 2ª fase as rachaduras ficam sempre em brasa
        Rachar(rach);
        if (!Fase2 && !transformando && Vida.Fracao <= 0.5f && (estado == Estado.Perseguindo || estado == Estado.Parado)) { StartCoroutine(Transformacao()); return; }       // quanto mais ferido, mais rachado (e cada golpe faz as rachaduras piscarem)
        BuscarAlvo();

        // gravidade
        if (cc.isGrounded) vy = -1f; else vy += Physics.gravity.y * Time.deltaTime;

        Vector3 moveu = Vector3.zero;
        if (alvo == null) { Respirar(); cc.Move(Vector3.up * vy * Time.deltaTime); return; }

        Vector3 pa = alvo.position - transform.position; pa.y = 0f;
        float dist = pa.magnitude;

        if (estado == Estado.Perseguindo || estado == Estado.Parado)
        {
            if (pa.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(pa), 110f * mulVel * Time.deltaTime);
            if (dist > alcanceGolpe * 0.8f) { moveu = transform.forward * velocidade * mulVel; Andar(velocidade * mulVel); }
            else Respirar();
            cc.Move((moveu + Vector3.up * vy) * Time.deltaTime);
            if (Time.time >= proximoAtaque)
            {
                if (EscolherAtaqueNovo(dist)) { }
                else if (dist <= alcanceGolpe && !pisaoNoRodizio) StartCoroutine(AtaqueGolpe());
                else if (dist <= raioPisao * 0.9f && (pisaoNoRodizio || dist > alcanceGolpe)) StartCoroutine(AtaquePisao());
            }
        }
        // o golpe e o pisão controlam a própria pose
        if (flinch > 0.01f) { P("Chest", 10f * flinch); P("Head", 14f * flinch); }
    }

    private void BuscarAlvo()
    {
        alvo = null; float melhor = 1e9f;
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto) continue;
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < melhor) { melhor = d; alvo = p.transform; }
        }
    }

    private IEnumerator AtaqueGolpe()
    {
        estado = Estado.Golpe; pisaoNoRodizio = true;
        PerigoNoChao.Golpe(() => transform.position + transform.forward * 3.4f, 5.2f, 1.45f);   // avisa os companheiros
        // levanta os dois punhos acima da cabeça (e olha para o alvo)
        for (float t = 0f; t < 0.95f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.95f);
            P("BracoSup_D", -165f * k, 0f, -12f * k); P("BracoSup_E", -165f * k, 0f, 12f * k);
            P("Antebraco_D", -35f * k); P("Antebraco_E", -35f * k); P("Chest", -16f * k); P("Spine", -6f * k); P("Head", -10f * k);
            P("Coxa_D", -8f * k); P("Coxa_E", 8f * k); P("Canela_D", 8f * k);
            if (alvo != null) { Vector3 d = alvo.position - transform.position; d.y = 0f; if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 140f * Time.deltaTime); }
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
        // desce com tudo
        for (float t = 0f; t < 0.22f; t += Time.deltaTime)
        {
            float k = t / 0.22f;
            P("BracoSup_D", Mathf.Lerp(-165f, 18f, k), 0f, -4f); P("BracoSup_E", Mathf.Lerp(-165f, 18f, k), 0f, 4f);
            P("Antebraco_D", Mathf.Lerp(-35f, -5f, k)); P("Antebraco_E", Mathf.Lerp(-35f, -5f, k)); P("Chest", Mathf.Lerp(-16f, 24f, k)); P("Spine", Mathf.Lerp(-6f, 10f, k)); P("Head", Mathf.Lerp(-10f, 12f, k));
            yield return null;
        }
        Impacto(transform.position + transform.forward * 3.4f, 4.6f, danoGolpe);
        TremorDeCamera.Tremer(0.35f, 0.7f);
        yield return new WaitForSeconds(0.8f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { PoseDeRepouso(0.2f); yield return null; }
        proximoAtaque = Time.time + UnityEngine.Random.Range(1.4f, 2.4f) * ritmo;
        estado = Estado.Perseguindo;
    }

    private IEnumerator AtaquePisao()
    {
        estado = Estado.Pisao; pisaoNoRodizio = false;
        PerigoNoChao.Anel(() => transform.position, raioPisao, 1.15f, 22f);
        // ergue o pé, bate no chão e uma onda de choque se espalha
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
            P("Coxa_D", -70f * k); P("Canela_D", 80f * k); P("Coxa_E", 6f * k); P("Chest", -10f * k); P("Head", -8f * k);
            P("BracoSup_D", -30f * k, 0f, -30f * k); P("BracoSup_E", -30f * k, 0f, 30f * k);
            yield return null;
        }
        yield return new WaitForSeconds(0.15f);
        for (float t = 0f; t < 0.18f; t += Time.deltaTime) { float k = t / 0.18f; P("Coxa_D", Mathf.Lerp(-70f, 4f, k)); P("Canela_D", Mathf.Lerp(80f, 0f, k)); P("Chest", Mathf.Lerp(-10f, 12f, k)); yield return null; }
        OndaDeChoque();
        TremorDeCamera.Tremer(0.4f, 0.9f);
        if (Fase2)
        {
            // pisão duplo: uma segunda onda logo depois da primeira
            PerigoNoChao.Anel(() => transform.position, raioPisao, 0.55f, 22f);
            yield return new WaitForSeconds(0.55f);
            OndaDeChoque(); TremorDeCamera.Tremer(0.45f, 0.9f);
            yield return new WaitForSeconds(0.5f);
        }
        else yield return new WaitForSeconds(0.9f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { PoseDeRepouso(0.2f); yield return null; }
        proximoAtaque = Time.time + UnityEngine.Random.Range(1.2f, 2.0f) * ritmo;
        estado = Estado.Perseguindo;
    }

    private void Impacto(Vector3 centro, float raio, float dano)
    {
        foreach (Collider c in Physics.OverlapSphere(centro, raio, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.GetComponentInParent<ChefeGuardiao>() == this) continue;
            if (c.GetComponentInParent<PlayerLocomotion>() == null) continue;
            Vida v = c.GetComponentInParent<Vida>(); if (v != null && !v.Morto) v.ReceberDano(dano, transform.position);
        }
        Poeira(centro, raio * 0.6f);
    }

    private void OndaDeChoque() { StartCoroutine(AnelDeChoque()); }

    private IEnumerator AnelDeChoque()
    {
        Vector3 c = transform.position; float r = 0f; HashSet<Vida> acertados = new HashSet<Vida>();
        GameObject anel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(anel.GetComponent<Collider>());
        Renderer rd = anel.GetComponent<Renderer>(); rd.sharedMaterial = new Material(Shader.Find("Sprites/Default")); rd.sharedMaterial.color = new Color(0.9f, 0.7f, 0.45f, 0.5f); rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        while (r < raioPisao)
        {
            r += 22f * Time.deltaTime;
            anel.transform.position = c + Vector3.up * 0.1f; anel.transform.localScale = new Vector3(r * 2f, 0.08f, r * 2f);
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            {
                Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto || acertados.Contains(v)) continue;
                Vector3 d = p.transform.position - c; d.y = 0f;
                if (Mathf.Abs(d.magnitude - r) < 1.6f && p.transform.position.y - c.y < 1.2f) { acertados.Add(v); v.ReceberDano(danoPisao, c); }
            }
            yield return null;
        }
        Destroy(anel);
    }

    private void Poeira(Vector3 pos, float raio)
    {
        GameObject g = new GameObject("Poeira"); g.transform.position = pos;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        var main = ps.main; main.duration = 0.6f; main.loop = false; main.startLifetime = 1.6f; main.startSpeed = 3.5f; main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startColor = new Color(0.55f, 0.5f, 0.42f, 0.55f); main.gravityModifier = -0.02f; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = raio;
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        ps.Play(); Destroy(g, 3f);
    }

    // ------------------------------------------------------------ morte
    private void Morrer()
    {
        if (estado == Estado.Morto) return;
        estado = Estado.Morto; StopAllCoroutines(); transformando = false;
        if (aura != null) aura.Desligar();
        LimparEfemeros();
        StartCoroutine(Desmoronar());
        if (AoMorrer != null) AoMorrer(this);
    }

    // ------------------------------------------------------------ rachaduras e morte
    private class Pedaco { public Transform t; public Vector3 v, w; public float tam, nasceu, pousouEm = -1f; }
    private readonly List<Pedaco> pedacos = new List<Pedaco>();
    private SkinnedMeshRenderer[] peles;
    private Material pele;
    private static Material matPedaco;

    private void Rachar(float v) { if (pele != null && pele.HasProperty("_Rachar")) pele.SetFloat("_Rachar", v); }

    // lasca de pedra que cai do corpo (ou voa na explosão final)
    private void Lasca(Vector3 pos, Vector3 vel, float tam)
    {
        if (matPedaco == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Sprites/Default");
            matPedaco = new Material(sh); matPedaco.color = new Color(0.34f, 0.32f, 0.3f);
            if (matPedaco.HasProperty("_BaseColor")) matPedaco.SetColor("_BaseColor", new Color(0.34f, 0.32f, 0.3f));
            if (matPedaco.HasProperty("_Smoothness")) matPedaco.SetFloat("_Smoothness", 0.1f);
        }
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
        g.GetComponent<Renderer>().sharedMaterial = matPedaco;
        g.transform.position = pos; g.transform.rotation = UnityEngine.Random.rotation;
        g.transform.localScale = new Vector3(tam, tam * UnityEngine.Random.Range(0.6f, 1.1f), tam * UnityEngine.Random.Range(0.7f, 1.3f));
        pedacos.Add(new Pedaco { t = g.transform, v = vel, w = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1.5f, 6f), tam = tam, nasceu = Time.time });
    }

    private void SimularPedacos(float chaoPadrao)
    {
        float dt = Time.deltaTime;
        for (int i = pedacos.Count - 1; i >= 0; i--)
        {
            Pedaco p = pedacos[i];
            if (p.t == null) { pedacos.RemoveAt(i); continue; }
            if (p.pousouEm >= 0f)
            {
                float resta = 5f - (Time.time - p.pousouEm);
                if (resta < 1f) p.t.localScale *= Mathf.Clamp01(1f - dt * 2.5f);
                if (resta <= 0f) { Destroy(p.t.gameObject); pedacos.RemoveAt(i); }
                continue;
            }
            p.v += Vector3.down * 18f * dt;
            Vector3 pos = p.t.position + p.v * dt;
            float chao = chaoPadrao; RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out hit, 40f, ~0, QueryTriggerInteraction.Ignore)) chao = hit.point.y;
            if (pos.y <= chao + p.tam * 0.4f && p.v.y < 0f)
            {
                pos.y = chao + p.tam * 0.4f;
                if (p.v.y < -4f && p.tam > 0.35f) { p.v = new Vector3(p.v.x * 0.4f, -p.v.y * 0.25f, p.v.z * 0.4f); p.w *= 0.5f; TremorDeCamera.Tremer(Mathf.Clamp(p.tam * 0.1f, 0.02f, 0.12f), 0.3f); }
                else { p.pousouEm = Time.time; p.v = Vector3.zero; }
            }
            p.t.position = pos;
            if (p.pousouEm < 0f) p.t.rotation = Quaternion.AngleAxis(p.w.magnitude * Mathf.Rad2Deg * dt, p.w.normalized) * p.t.rotation;
        }
    }

    private Vector3 PontoDoCorpo(float alturaMin, float alturaMax, float raio)
    {
        Vector2 c = UnityEngine.Random.insideUnitCircle * raio;
        return transform.position + new Vector3(c.x, UnityEngine.Random.Range(alturaMin, alturaMax), c.y);
    }

    private void Brasas(Vector3 pos, int n, float forca)
    {
        PracaDoGuardiao pr = PracaDoGuardiao.Instancia; if (pr == null) return;
        for (int i = 0; i < n; i++)
            pr.EmitirBrasa(pos + UnityEngine.Random.insideUnitSphere * 0.6f, UnityEngine.Random.insideUnitSphere * forca + Vector3.up * forca * 0.8f, new Color(1f, UnityEngine.Random.Range(0.35f, 0.65f), 0.1f, 1f), UnityEngine.Random.Range(0.1f, 0.35f));
    }

    private IEnumerator Desmoronar()
    {
        cc.enabled = false;
        Vector3 ini = transform.position; float chaoIni = ini.y;
        Transform peito = ossos.ContainsKey("Chest") ? ossos["Chest"] : transform;
        GameObject lg = new GameObject("Luz da morte"); lg.transform.SetParent(peito, false);
        Light luz = lg.AddComponent<Light>(); luz.type = LightType.Point; luz.color = new Color(1f, 0.5f, 0.18f); luz.range = 22f; luz.intensity = 0f; luz.shadows = LightShadows.None;
        float proxLasca = 0f, proxTremor = 0f;

        // 1) cambaleia: o corpo treme, as rachaduras se abrem e começam a cair lascas
        const float T1 = 2.2f, T2 = 2.8f, T3 = 1.3f;
        TremorDeCamera.Tremer(0.2f, 1.5f);
        for (float t = 0f; t < T1; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T1), tr = Mathf.Sin(Time.time * 38f) * (1.2f + 3.5f * k);
            P("Chest", -12f * k + tr * 0.4f, tr); P("Spine", -4f * k); P("Head", -26f * k, tr * 0.8f);
            P("BracoSup_D", -22f * k, 0f, -55f * k); P("BracoSup_E", -22f * k, 0f, 55f * k); P("Antebraco_D", -20f * k); P("Antebraco_E", -20f * k);
            P("Coxa_D", 8f * k); P("Coxa_E", 8f * k); P("Canela_D", 14f * k); P("Canela_E", 14f * k);
            if (esqueleto != null) esqueleto.localPosition = UnityEngine.Random.insideUnitSphere * 0.07f * k;
            Rachar(Mathf.Lerp(0.8f, 1.2f, k)); luz.intensity = Mathf.Lerp(1f, 7f, k);
            if (Time.time >= proxLasca) { proxLasca = Time.time + 0.14f; Lasca(PontoDoCorpo(0.5f, 5f, 1.2f), UnityEngine.Random.insideUnitSphere * 1.5f, UnityEngine.Random.Range(0.12f, 0.35f)); }
            if (Time.time >= proxTremor) { proxTremor = Time.time + 0.25f; TremorDeCamera.Tremer(0.1f + 0.1f * k, 0.3f); }
            SimularPedacos(chaoIni); yield return null;
        }
        // 2) cai de joelhos, pesado, levantando poeira
        bool bateu = false;
        for (float t = 0f; t < T2; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T2), tr = Mathf.Sin(Time.time * 30f) * 2.5f * (1f - k);
            P("Coxa_D", Mathf.Lerp(8f, -88f, k)); P("Coxa_E", Mathf.Lerp(8f, -88f, k)); P("Canela_D", Mathf.Lerp(14f, 98f, k)); P("Canela_E", Mathf.Lerp(14f, 98f, k));
            P("Chest", Mathf.Lerp(-12f, 40f, k) + tr, tr); P("Spine", Mathf.Lerp(-4f, 14f, k)); P("Head", Mathf.Lerp(-26f, 36f, k));
            P("BracoSup_D", Mathf.Lerp(-22f, 14f, k), 0f, Mathf.Lerp(-55f, -10f, k)); P("BracoSup_E", Mathf.Lerp(-22f, 14f, k), 0f, Mathf.Lerp(55f, 10f, k));
            P("Antebraco_D", Mathf.Lerp(-20f, -4f, k)); P("Antebraco_E", Mathf.Lerp(-20f, -4f, k));
            transform.position = ini + Vector3.down * (1.9f * k);
            if (esqueleto != null) esqueleto.localPosition = UnityEngine.Random.insideUnitSphere * 0.05f * (1f - k);
            Rachar(Mathf.Lerp(1.2f, 1.5f, k)); luz.intensity = Mathf.Lerp(7f, 14f, k);
            if (Time.time >= proxLasca) { proxLasca = Time.time + 0.07f; Lasca(PontoDoCorpo(0.3f, 4.2f, 1.3f), UnityEngine.Random.insideUnitSphere * 2f, UnityEngine.Random.Range(0.12f, 0.45f)); }
            if (Time.frameCount % 9 == 0) Poeira(transform.position + Vector3.up * 0.4f + UnityEngine.Random.insideUnitSphere * 1.6f, 1.2f);
            if (!bateu && k > 0.93f) { bateu = true; TremorDeCamera.Tremer(0.4f, 1.2f); Poeira(transform.position + Vector3.up * 0.2f, 2.6f); Brasas(transform.position + Vector3.up, 25, 6f); }
            SimularPedacos(chaoIni); yield return null;
        }
        // 3) último brilho: as rachaduras queimam no máximo e o corpo vibra
        for (float t = 0f; t < T3; t += Time.deltaTime)
        {
            float k = t / T3;
            if (esqueleto != null) esqueleto.localPosition = UnityEngine.Random.insideUnitSphere * (0.05f + 0.18f * k);
            Rachar(1.5f); luz.intensity = 14f + 26f * k * k;
            TremorDeCamera.Tremer(0.12f + 0.25f * k, 0.25f);
            if (Time.frameCount % 2 == 0) Brasas(PontoDoCorpo(0.5f, 3.5f, 1f), 3, 3f);
            if (Time.time >= proxLasca) { proxLasca = Time.time + 0.05f; Lasca(PontoDoCorpo(0.3f, 3.8f, 1.4f), UnityEngine.Random.insideUnitSphere * 3f, UnityEngine.Random.Range(0.15f, 0.5f)); }
            SimularPedacos(chaoIni); yield return null;
        }
        // 4) estilhaça: o corpo some e vira uma chuva de pedras
        peles = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer r in peles) r.enabled = false;
        Vector3 centro = transform.position + Vector3.up * 2f;
        TremorDeCamera.Tremer(0.6f, 2.2f);
        for (int i = 0; i < 52; i++)
        {
            Vector3 pos = PontoDoCorpo(0.2f, 4.4f, 1.3f);
            Vector3 fora = pos - centro; fora.y = 0f; fora = fora.sqrMagnitude > 0.01f ? fora.normalized : UnityEngine.Random.insideUnitSphere;
            float tam = pos.y < 1.6f ? UnityEngine.Random.Range(0.5f, 1.2f) : UnityEngine.Random.Range(0.25f, 0.9f);
            Lasca(pos, fora * UnityEngine.Random.Range(2.5f, 8.5f) + Vector3.up * UnityEngine.Random.Range(3f, 10f), tam);
        }
        for (int i = 0; i < 9; i++) Poeira(transform.position + Vector3.up * UnityEngine.Random.Range(0.3f, 4f) + UnityEngine.Random.insideUnitSphere * 1.2f, 2.2f);
        Brasas(centro, 90, 9f);
        float inicio = Time.time;
        while (Time.time - inicio < 7f)
        {
            float k = (Time.time - inicio) / 7f;
            luz.intensity = Mathf.Lerp(45f, 0f, Mathf.Clamp01(k * 3f));
            if (Time.time - inicio < 1.2f && Time.frameCount % 6 == 0) Poeira(transform.position + Vector3.up * 0.5f + UnityEngine.Random.insideUnitSphere * 2f, 1.6f);
            SimularPedacos(chaoIni); yield return null;
        }
        foreach (Pedaco p in pedacos) if (p.t != null) Destroy(p.t.gameObject);
        pedacos.Clear();
        Destroy(gameObject);
    }
}
