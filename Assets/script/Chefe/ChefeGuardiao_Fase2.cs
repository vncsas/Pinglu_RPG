using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 2ª fase e ataques novos do Guardião das Lápides.
//  - ARREMESSO DE PEDRA (fases 1 e 2): se os heróis estão longe, ele arranca uma pedra enorme do chão, levanta sobre a cabeça e joga.
//  - RAIO (só fase 2): junta energia no peito, marca a linha no chão e varre um raio poderoso.
//  - FASE 2: com metade da vida ele se transforma (fica invulnerável por uns 3 s, ruge, uma onda empurra todo mundo e a aura
//    sinistra acende). Depois anda mais rápido, ataca num ritmo bem mais agressivo, bate mais forte e dá pisão duplo.
public partial class ChefeGuardiao
{
    public bool Fase2 { get; private set; }
    public float danoPedra = 46f, danoRaio = 21f;

    private bool transformando;
    private float ritmo = 1f, mulVel = 1f, proximaPedra, proximoRaio;
    private BarraDeChefe barra;
    private AuraDoGuardiao aura;
    private Vector3 raioOrigem, raioDir = Vector3.forward;

    // objetos temporários dos ataques (raio, pedra...): se o chefe morrer no meio, somem junto
    private readonly List<GameObject> efemeros = new List<GameObject>();
    private GameObject Ef(GameObject g) { efemeros.RemoveAll(x => x == null); efemeros.Add(g); return g; }
    private void LimparEfemeros() { foreach (GameObject g in efemeros) if (g != null) Destroy(g); efemeros.Clear(); }

    // só no editor: F6 deixa o chefe com 52% da vida (para ver a 2ª fase logo)
    private void AtalhoDeTeste()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F6) && !Fase2 && Vida != null && !Vida.Morto) Vida.DefinirAtual(Vida.Maxima * 0.52f);
#endif
    }

    private void IniciarFase2()
    {
        Vida.FiltroDeDano = (d, o) => transformando ? 0f : d;       // invulnerável enquanto se transforma
        proximaPedra = Time.time + 7f;
        danoGolpe *= multDano; danoPisao *= multDano;                 // chefe mais pesado (ajuste multDano no Inspector)
    }

    // chamado a cada quadro pelo Update: devolve true se começou um ataque novo
    private bool EscolherAtaqueNovo(float dist)
    {
        if (alvo == null) return false;
        if (dist > 13f && dist < 60f && Time.time >= proximaPedra) { StartCoroutine(AtaquePedra()); return true; }
        if (Fase2 && dist > 7f && dist < 36f && Time.time >= proximoRaio && (dist > 12f || UnityEngine.Random.value < 0.35f)) { StartCoroutine(AtaqueRaio()); return true; }
        if (Fase2 && dist >= 10f && dist <= 22f && Time.time >= proximoSalto && UnityEngine.Random.value < 0.5f) { StartCoroutine(AtaqueSalto()); return true; }
        if (dist <= raioPisao * 0.9f) return EscolherAtaquePerto(dist);       // perto: varredura, combo, pisão, golpe (e giro/pulso na fase 2)
        return false;
    }

    // ------------------------------------------------------------ utilidades
    private static float ChaoEm(Vector3 p)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, 160f, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.NegativeInfinity;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<Vida>() != null) continue;           // gente e monstros não são chão
            if (h.point.y > melhor) melhor = h.point.y;
        }
        return float.IsNegativeInfinity(melhor) ? p.y : melhor;
    }

    private static Material materialDePedra;
    private static Material MaterialDePedra()
    {
        if (materialDePedra == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Sprites/Default");
            materialDePedra = new Material(sh); Color c = new Color(0.3f, 0.28f, 0.26f);
            materialDePedra.color = c; if (materialDePedra.HasProperty("_BaseColor")) materialDePedra.SetColor("_BaseColor", c);
            if (materialDePedra.HasProperty("_Smoothness")) materialDePedra.SetFloat("_Smoothness", 0.08f);
        }
        return materialDePedra;
    }

    // pedra irregular (uma esfera amassada por ruído)
    private GameObject CriarRocha(Vector3 pos, float diametro)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Collider col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
        Mesh src = g.GetComponent<MeshFilter>().sharedMesh; Mesh m = new Mesh(); float sem = UnityEngine.Random.value * 50f;
        Vector3[] v = src.vertices;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 n = v[i].normalized;
            float f = 1f + 0.34f * (Mathf.PerlinNoise(n.x * 2.3f + sem, n.y * 2.1f + n.z * 1.9f) - 0.5f) + 0.16f * (Mathf.PerlinNoise(n.z * 5.2f + sem, n.x * 4.7f + n.y * 3f) - 0.5f);
            v[i] = n * 0.5f * f;
        }
        m.vertices = v; m.triangles = src.triangles; m.uv = src.uv; m.RecalculateNormals(); m.RecalculateBounds();
        g.GetComponent<MeshFilter>().sharedMesh = m;
        g.GetComponent<Renderer>().sharedMaterial = MaterialDePedra();
        g.name = "Pedra do Guardião"; g.transform.position = pos; g.transform.localScale = Vector3.one * diametro;
        g.transform.rotation = UnityEngine.Random.rotation;
        return g;
    }

    private Transform Osso(string n) { Transform t; return ossos.TryGetValue(n, out t) ? t : transform; }

    private void GiraPara(Vector3 pos, float grausPorSeg)
    {
        Vector3 d = pos - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), grausPorSeg * Time.deltaTime);
    }

    // ------------------------------------------------------------ ARREMESSO DE PEDRA
    private IEnumerator AtaquePedra()
    {
        estado = Estado.Pedra; proximaPedra = Time.time + (Fase2 ? 6f : 10f);
        Transform meuAlvo = alvo;
        Transform maoD = Osso("Mao_D"), maoE = Osso("Mao_E");

        // 1) vira para o alvo, se abaixa e arranca a pedra do chão à sua frente
        Vector3 pedraPonto = transform.position + transform.forward * 3.4f; pedraPonto.y = ChaoEm(pedraPonto);
        GameObject rocha = Ef(CriarRocha(pedraPonto + Vector3.down * 2f, 0.4f));
        float T1 = 1.1f;
        for (float t = 0f; t < T1; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T1);
            if (meuAlvo != null) GiraPara(meuAlvo.position, 160f);
            P("Chest", 38f * k, 0f); P("Spine", 14f * k); P("Head", 14f * k);
            P("BracoSup_D", -62f * k, 0f, -8f * k); P("BracoSup_E", -62f * k, 0f, 8f * k); P("Antebraco_D", -28f * k); P("Antebraco_E", -28f * k);
            P("Coxa_D", -24f * k); P("Coxa_E", -24f * k); P("Canela_D", 30f * k); P("Canela_E", 30f * k);
            if (esqueleto != null) esqueleto.localPosition = new Vector3(0f, -0.7f * k, 0f);
            rocha.transform.position = pedraPonto + Vector3.up * Mathf.Lerp(-1.8f, 0.5f, k);
            rocha.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 3.4f, k);
            if (Time.frameCount % 3 == 0) { Poeira(pedraPonto, 2f); TremorDeCamera.Tremer(0.1f * k + 0.03f, 0.2f); }
            if (Time.frameCount % 5 == 0) Lasca(pedraPonto + UnityEngine.Random.insideUnitSphere * 1.6f, new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(2f, 5f), UnityEngine.Random.Range(-2f, 2f)), UnityEngine.Random.Range(0.15f, 0.4f));
            SimularPedacos(pedraPonto.y); yield return null;
        }
        Poeira(pedraPonto, 3.6f); TremorDeCamera.Tremer(0.3f, 0.5f);

        // 2) levanta a pedra sobre a cabeça
        Vector3 ini = rocha.transform.position; float T2 = 1.0f;
        for (float t = 0f; t < T2; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T2);
            if (meuAlvo != null) GiraPara(meuAlvo.position, 160f);
            P("Chest", Mathf.Lerp(38f, -18f, k)); P("Spine", Mathf.Lerp(14f, -6f, k)); P("Head", Mathf.Lerp(14f, -10f, k));
            P("BracoSup_D", Mathf.Lerp(-62f, -162f, k), 0f, Mathf.Lerp(-8f, -10f, k)); P("BracoSup_E", Mathf.Lerp(-62f, -162f, k), 0f, Mathf.Lerp(8f, 10f, k));
            P("Antebraco_D", Mathf.Lerp(-28f, -22f, k)); P("Antebraco_E", Mathf.Lerp(-28f, -22f, k));
            P("Coxa_D", Mathf.Lerp(-24f, -6f, k)); P("Coxa_E", Mathf.Lerp(-24f, -6f, k)); P("Canela_D", Mathf.Lerp(30f, 10f, k)); P("Canela_E", Mathf.Lerp(30f, 10f, k));
            if (esqueleto != null) esqueleto.localPosition = new Vector3(0f, Mathf.Lerp(-0.7f, 0f, k), 0f);
            Vector3 alto = (maoD.position + maoE.position) * 0.5f + Vector3.up * 1.7f;
            rocha.transform.position = Vector3.Lerp(ini, alto, k);
            rocha.transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
            SimularPedacos(pedraPonto.y); yield return null;
        }

        // 3) segura, mira e puxa o braço para trás
        float T3 = 0.55f;
        for (float t = 0f; t < T3; t += Time.deltaTime)
        {
            float k = t / T3;
            if (meuAlvo != null) GiraPara(meuAlvo.position, 220f);
            P("Chest", Mathf.Lerp(-18f, -34f, k)); P("Head", -14f); P("BracoSup_D", -170f - 18f * k, 0f, -10f); P("BracoSup_E", -170f - 18f * k, 0f, 10f);
            Vector3 alto = (maoD.position + maoE.position) * 0.5f + Vector3.up * 1.7f;
            rocha.transform.position = alto + Random.insideUnitSphere * 0.03f * k;
            if (Fase2 && Time.frameCount % 2 == 0) Brasas(rocha.transform.position, 2, 2.5f);
            yield return null;
        }

        // 4) arremessa: o alvo é onde o herói vai estar (mira um pouco à frente do movimento dele)
        Vector3 origem = rocha.transform.position;
        Vector3 destino = meuAlvo != null ? meuAlvo.position : transform.position + transform.forward * 20f;
        CharacterController ccAlvo = meuAlvo != null ? meuAlvo.GetComponent<CharacterController>() : null;
        float voo = Mathf.Clamp(Vector3.Distance(origem, destino) / 24f, 0.85f, 2.0f);
        if (ccAlvo != null) { Vector3 vel = ccAlvo.velocity; vel.y = 0f; destino += vel * voo * 0.65f; }
        destino.y = ChaoEm(destino);
        Vector3 v0 = (destino - origem) / voo + Vector3.up * 0.5f * 20f * voo;
        Vector3 pontoFinal = destino;
        PerigoNoChao.Golpe(() => pontoFinal, 5.4f, voo + 0.05f);                       // avisa os companheiros
        for (float t = 0f; t < 0.26f; t += Time.deltaTime)
        {
            float k = t / 0.26f;
            P("Chest", Mathf.Lerp(-34f, 34f, k)); P("Spine", Mathf.Lerp(-6f, 12f, k)); P("Head", Mathf.Lerp(-14f, 14f, k));
            P("BracoSup_D", Mathf.Lerp(-188f, 22f, k), 0f, -6f); P("BracoSup_E", Mathf.Lerp(-188f, 22f, k), 0f, 6f);
            if (k > 0.45f && rocha != null && rocha.transform.parent == null && !voando(rocha)) { StartCoroutine(VooDaPedra(rocha, v0, voo)); rocha = null; }
            yield return null;
        }
        if (rocha != null) StartCoroutine(VooDaPedra(rocha, v0, voo));
        TremorDeCamera.Tremer(0.2f, 0.4f);
        for (float t = 0f; t < 0.7f; t += Time.deltaTime) { PoseDeRepouso(0.2f); SimularPedacos(pedraPonto.y); yield return null; }
        proximoAtaque = Time.time + UnityEngine.Random.Range(1.2f, 2.0f) * ritmo;
        estado = Estado.Perseguindo;
    }

    private readonly HashSet<GameObject> pedrasVoando = new HashSet<GameObject>();
    private bool voando(GameObject g) { return pedrasVoando.Contains(g); }

    private IEnumerator VooDaPedra(GameObject rocha, Vector3 v, float duracao)
    {
        pedrasVoando.Add(rocha);
        GameObject luz = new GameObject("Luz da pedra"); luz.transform.SetParent(rocha.transform, false);
        Light l = luz.AddComponent<Light>(); l.color = new Color(1f, 0.5f, 0.2f); l.range = 9f; l.intensity = Fase2 ? 4f : 1.5f; l.shadows = LightShadows.None;
        Vector3 pos = rocha.transform.position; float t = 0f; bool bateu = false;
        while (!bateu && t < duracao + 1.5f)
        {
            float dt = Time.deltaTime; t += dt;
            v += Vector3.down * 20f * dt; pos += v * dt;
            rocha.transform.position = pos; rocha.transform.Rotate(new Vector3(180f, 90f, 40f) * dt, Space.World);
            if (Fase2 && Time.frameCount % 2 == 0) Brasas(pos, 1, 2f);
            if (t > 0.25f)
            {
                if (pos.y <= ChaoEm(pos) + 1.2f) bateu = true;
                else foreach (Collider c in Physics.OverlapSphere(pos, 1.8f, ~0, QueryTriggerInteraction.Ignore))
                    if (c.GetComponentInParent<PlayerLocomotion>() != null) { bateu = true; break; }
            }
            yield return null;
        }
        Vector3 c0 = pos; c0.y = ChaoEm(c0);
        pedrasVoando.Remove(rocha);
        Impacto(c0 + Vector3.up * 0.4f, 4.8f, danoPedra * (Fase2 ? 1.2f : 1f));
        TremorDeCamera.Tremer(0.4f, 0.8f); Poeira(c0, 3.4f); Brasas(c0 + Vector3.up, Fase2 ? 30 : 14, 6f);
        for (int i = 0; i < 16; i++)
            Lasca(c0 + Vector3.up * 0.8f + UnityEngine.Random.insideUnitSphere * 0.8f, new Vector3(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(4f, 10f), UnityEngine.Random.Range(-6f, 6f)), UnityEngine.Random.Range(0.25f, 0.8f));
        StartCoroutine(AnelVisual(c0, 6f, 20f, new Color(0.9f, 0.7f, 0.45f, 0.45f)));
        Destroy(rocha);
        for (float r = 0f; r < 4f; r += Time.deltaTime) { SimularPedacos(c0.y); yield return null; }
    }

    // anel que se abre no chão só como efeito (sem dano)
    private IEnumerator AnelVisual(Vector3 c, float raioMax, float vel, Color cor)
    {
        GameObject anel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(anel.GetComponent<Collider>());
        Renderer rd = anel.GetComponent<Renderer>(); rd.sharedMaterial = new Material(Shader.Find("Sprites/Default")); rd.sharedMaterial.color = cor;
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (float r = 0.5f; r < raioMax; r += vel * Time.deltaTime)
        {
            anel.transform.position = c + Vector3.up * 0.15f; anel.transform.localScale = new Vector3(r * 2f, 0.06f, r * 2f);
            Color k = cor; k.a = cor.a * (1f - r / raioMax); rd.sharedMaterial.color = k;
            yield return null;
        }
        Destroy(anel);
    }

    // ------------------------------------------------------------ RAIO
    private IEnumerator AtaqueRaio()
    {
        estado = Estado.Raio; proximoRaio = Time.time + 12f;
        Transform meuAlvo = alvo; Transform peito = Osso("Chest");
        // esfera de energia diante do peito
        GameObject orbe = Ef(AuraDoGuardiao.Brilho(peito.position + transform.forward * 1.4f, 0.1f, new Color(3.5f, 1.2f, 0.4f, 1f)));
        GameObject luzO = Ef(new GameObject("Luz do raio")); Light lz = luzO.AddComponent<Light>(); lz.color = new Color(1f, 0.45f, 0.15f); lz.range = 20f; lz.intensity = 0f; lz.shadows = LightShadows.None;
        raioOrigem = peito.position + transform.forward * 1.2f; raioDir = transform.forward;
        PerigoNoChao.Linha(() => raioOrigem, () => raioDir, 30f, 2.8f, 1.1f, 4.4f);      // os companheiros saem da linha

        // linha de aviso no chão
        LineRenderer aviso = NovaLinha("Aviso do raio", AuraDoGuardiao.TexFeixe, new Color(2.4f, 0.25f, 0.1f, 0.9f), 0.5f);

        // 1) carrega: braços juntos na frente do peito; o corpo ainda acompanha o alvo
        const float T1 = 1.9f;
        for (float t = 0f; t < T1; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T1);
            if (meuAlvo != null && t < 1.2f) GiraPara(meuAlvo.position, 170f);
            P("Chest", -14f * k); P("Head", -10f * k); P("Spine", -4f * k);
            P("BracoSup_D", -78f * k, 14f * k, -6f * k); P("BracoSup_E", -78f * k, -14f * k, 6f * k); P("Antebraco_D", -84f * k, 0f, 30f * k); P("Antebraco_E", -84f * k, 0f, -30f * k);
            P("Coxa_D", -8f * k); P("Coxa_E", 8f * k);
            raioDir = transform.forward; raioOrigem = peito.position + transform.forward * 1.2f;
            orbe.transform.position = raioOrigem + Vector3.up * 0.2f; orbe.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 3.4f, k) * (0.9f + 0.1f * Mathf.Sin(Time.time * 30f));
            luzO.transform.position = raioOrigem; lz.intensity = 12f * k;
            if (t > 0.6f) { Vector3 fim = FimDoRaio(raioOrigem, raioDir); aviso.SetPosition(0, new Vector3(raioOrigem.x, ChaoEm(raioOrigem) + 0.12f, raioOrigem.z)); aviso.SetPosition(1, new Vector3(fim.x, ChaoEm(fim) + 0.12f, fim.z)); aviso.enabled = true; aviso.widthMultiplier = Mathf.Lerp(0.2f, 1.6f, k); }
            else aviso.enabled = false;
            if (Time.frameCount % 2 == 0) Brasas(raioOrigem + UnityEngine.Random.onUnitSphere * 3.5f, 1, 0f);
            if (Time.frameCount % 4 == 0) TremorDeCamera.Tremer(0.03f + 0.1f * k, 0.25f);
            yield return null;
        }

        // 2) trava a mira: um instante de tensão
        Quaternion travada = transform.rotation;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            transform.rotation = travada;
            orbe.transform.localScale = Vector3.one * (3.4f + Mathf.Sin(t * 70f) * 0.3f);
            TremorDeCamera.Tremer(0.15f, 0.2f);
            yield return null;
        }
        Destroy(aviso.gameObject);

        // 3) dispara e varre o raio de um lado ao outro
        LineRenderer nucleo = NovaLinha("Raio (núcleo)", AuraDoGuardiao.TexFeixe, new Color(5f, 3.6f, 2f, 1f), 0.8f);
        LineRenderer brilho = NovaLinha("Raio (brilho)", AuraDoGuardiao.TexFeixe, new Color(3f, 0.7f, 0.2f, 0.9f), 3.6f);
        GameObject clarao = Ef(AuraDoGuardiao.Brilho(raioOrigem, 5f, new Color(3f, 1.2f, 0.4f, 1f)));
        Vector3 baseDir = travada * Vector3.forward; baseDir.y = 0f; baseDir.Normalize();
        Dictionary<Vida, float> proximoDano = new Dictionary<Vida, float>();
        const float T3 = 2.3f; float tremor = 0f;
        TremorDeCamera.Tremer(0.5f, T3);
        for (float t = 0f; t < T3; t += Time.deltaTime)
        {
            float k = t / T3, s = Mathf.Lerp(-34f, 34f, Mathf.SmoothStep(0f, 1f, k));
            raioDir = Quaternion.Euler(0f, s, 0f) * baseDir;
            transform.rotation = Quaternion.LookRotation(raioDir);
            P("Chest", -16f); P("Head", -14f); P("BracoSup_D", -92f, 18f, -4f); P("BracoSup_E", -92f, -18f, 4f); P("Antebraco_D", -70f, 0f, 20f); P("Antebraco_E", -70f, 0f, -20f);
            raioOrigem = peito.position + raioDir * 1.2f;
            Vector3 fim = FimDoRaio(raioOrigem, raioDir);
            float larg = (0.9f + 0.15f * Mathf.Sin(Time.time * 45f)) * Mathf.Clamp01(Mathf.Min(k * 14f, (1f - k) * 9f));
            foreach (LineRenderer lr in new[] { nucleo, brilho }) { lr.SetPosition(0, raioOrigem); lr.SetPosition(1, fim); }
            nucleo.widthMultiplier = 0.8f * larg; brilho.widthMultiplier = 3.6f * larg;
            orbe.transform.position = raioOrigem; orbe.transform.localScale = Vector3.one * (3.6f + Mathf.Sin(Time.time * 40f) * 0.4f);
            clarao.transform.position = fim; clarao.transform.localScale = Vector3.one * (5f + Mathf.Sin(Time.time * 33f) * 0.8f) * Mathf.Clamp01(larg);
            luzO.transform.position = Vector3.Lerp(raioOrigem, fim, 0.5f); lz.intensity = 14f;
            // dano: quem estiver perto do raio (a altura do corpo) leva a cada 0.3 s
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            {
                Vida vv = p.GetComponent<Vida>(); if (vv == null || vv.Morto) continue;
                Vector3 centro = p.transform.position + Vector3.up * 1.0f;
                Vector3 ab = fim - raioOrigem; float tt = Mathf.Clamp01(Vector3.Dot(centro - raioOrigem, ab) / ab.sqrMagnitude);
                if (Vector3.Distance(centro, raioOrigem + ab * tt) > 1.3f) continue;
                float prox; if (proximoDano.TryGetValue(vv, out prox) && Time.time < prox) continue;
                proximoDano[vv] = Time.time + 0.3f; vv.ReceberDano(danoRaio, transform.position);
            }
            // onde o raio toca o chão: poeira, fagulhas e pedras saltando
            tremor += Time.deltaTime;
            if (Time.frameCount % 2 == 0) { Brasas(fim + Vector3.up * 0.3f, 3, 5f); Poeira(fim, 1.6f); }
            if (Time.frameCount % 7 == 0) Lasca(fim + Vector3.up * 0.4f, new Vector3(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(3f, 7f), UnityEngine.Random.Range(-4f, 4f)), UnityEngine.Random.Range(0.12f, 0.3f));
            SimularPedacos(fim.y);
            yield return null;
        }
        Destroy(nucleo.gameObject); Destroy(brilho.gameObject); Destroy(clarao); Destroy(orbe); Destroy(luzO);

        // 4) se recompõe (aberto a ataques)
        for (float t = 0f; t < 1.0f; t += Time.deltaTime) { PoseDeRepouso(0.18f); SimularPedacos(transform.position.y); yield return null; }
        proximoAtaque = Time.time + UnityEngine.Random.Range(1.4f, 2.2f) * ritmo;
        estado = Estado.Perseguindo;
    }

    // até onde o raio vai: 30 m, descendo para a altura do corpo dos heróis, parando em paredes
    private Vector3 FimDoRaio(Vector3 origem, Vector3 dir)
    {
        Vector3 fim = origem + dir * 30f;
        fim.y = ChaoEm(fim) + 0.9f;
        RaycastHit h;
        if (Physics.Linecast(origem, fim, out h, ~0, QueryTriggerInteraction.Ignore) && h.collider.GetComponentInParent<Vida>() == null && h.collider.GetComponentInParent<ChefeGuardiao>() != this) fim = h.point;
        return fim;
    }

    private LineRenderer NovaLinha(string nome, Texture2D tex, Color cor, float largura)
    {
        GameObject g = new GameObject(nome);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.positionCount = 2; lr.useWorldSpace = true; lr.textureMode = LineTextureMode.Stretch; lr.numCapVertices = 6;
        lr.widthMultiplier = largura; lr.sharedMaterial = AuraDoGuardiao.Mat(tex, true, cor);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.alignment = LineAlignment.View;
        Ef(g); return lr;
    }

    // ------------------------------------------------------------ TRANSFORMAÇÃO (2ª fase)
    private void SetFuria(float k) { if (pele != null && pele.HasProperty("_Furia")) pele.SetFloat("_Furia", k); }

    private IEnumerator Transformacao()
    {
        transformando = true; estado = Estado.Transformando;
        TremorDeCamera.Tremer(0.2f, 1.2f);
        Vector3 centro = transform.position + Vector3.up * 2.5f;

        // 1) se encolhe, as rachaduras acendem e a energia do cemitério é sugada para dentro dele
        const float T1 = 1.5f;
        for (float t = 0f; t < T1; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T1), tr = Mathf.Sin(Time.time * 42f) * 2.2f * k;
            P("Chest", 32f * k + tr * 0.3f, tr); P("Spine", 12f * k); P("Head", 22f * k);
            P("BracoSup_D", -24f * k, 0f, -14f * k); P("BracoSup_E", -24f * k, 0f, 14f * k); P("Antebraco_D", -40f * k); P("Antebraco_E", -40f * k);
            P("Coxa_D", -18f * k); P("Coxa_E", -18f * k); P("Canela_D", 24f * k); P("Canela_E", 24f * k);
            if (esqueleto != null) esqueleto.localPosition = new Vector3(UnityEngine.Random.Range(-0.04f, 0.04f) * k, -0.5f * k, UnityEngine.Random.Range(-0.04f, 0.04f) * k);
            Rachar(Mathf.Lerp(Mathf.Max(0.4f, Vida.Fracao), 1.25f, k)); SetFuria(k * 0.5f);
            if (Time.frameCount % 2 == 0)
            {
                Vector3 p = centro + UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(5f, 9f);
                PracaDoGuardiao pr = PracaDoGuardiao.Instancia;
                if (pr != null) pr.EmitirBrasa(p, (centro - p).normalized * 7f, new Color(1f, 0.45f, 0.12f, 1f), UnityEngine.Random.Range(0.15f, 0.4f));
            }
            if (Time.frameCount % 5 == 0) TremorDeCamera.Tremer(0.06f + 0.14f * k, 0.3f);
            yield return null;
        }

        // 2) ergue-se, ruge de braços abertos; a aura explode e uma onda empurra todos para longe
        aura = AuraDoGuardiao.Criar(transform, peles != null && peles.Length > 0 ? peles[0] : null);
        aura.Ligar(2.4f);
        HUDJogador.Mostrar("O Guardião se enfurece!", 3.5f);
        Brasas(centro, 60, 8f); Poeira(transform.position + Vector3.up * 0.3f, 4f);
        StartCoroutine(AnelVisual(transform.position, 20f, 26f, new Color(1f, 0.45f, 0.15f, 0.6f)));
        StartCoroutine(AnelVisual(transform.position, 14f, 18f, new Color(0.5f, 0.2f, 0.9f, 0.5f)));
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            if (Vector3.Distance(p.transform.position, transform.position) < 16f) p.ReagirAoDano(transform.position, false);       // só empurra, sem dano
        TremorDeCamera.Tremer(0.6f, 2.4f);
        GameObject flash = Ef(AuraDoGuardiao.Brilho(centro, 14f, new Color(3f, 1.1f, 0.4f, 1f)));
        const float T2 = 2.0f;
        for (float t = 0f; t < T2; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.55f)), tr = Mathf.Sin(Time.time * 30f) * 3f * (1f - t / T2);
            P("Chest", Mathf.Lerp(32f, -24f, k) + tr, tr); P("Spine", Mathf.Lerp(12f, -8f, k)); P("Head", Mathf.Lerp(22f, -42f, k));
            P("BracoSup_D", Mathf.Lerp(-24f, -150f, k), 0f, Mathf.Lerp(-14f, -58f, k)); P("BracoSup_E", Mathf.Lerp(-24f, -150f, k), 0f, Mathf.Lerp(14f, 58f, k));
            P("Antebraco_D", Mathf.Lerp(-40f, -24f, k)); P("Antebraco_E", Mathf.Lerp(-40f, -24f, k));
            P("Coxa_D", Mathf.Lerp(-18f, -4f, k)); P("Coxa_E", Mathf.Lerp(-18f, -4f, k)); P("Canela_D", Mathf.Lerp(24f, 6f, k)); P("Canela_E", Mathf.Lerp(24f, 6f, k));
            if (esqueleto != null) esqueleto.localPosition = Vector3.Lerp(esqueleto.localPosition, Vector3.zero, Time.deltaTime * 8f);
            Rachar(1.3f); SetFuria(Mathf.Lerp(0.5f, 1f, k));
            flash.transform.localScale = Vector3.one * Mathf.Lerp(14f, 0.1f, Mathf.Clamp01(t / 0.8f));
            if (Time.frameCount % 4 == 0) Brasas(PontoDoCorpo(0.5f, 5f, 1.5f), 3, 5f);
            if (Time.frameCount % 6 == 0) TremorDeCamera.Tremer(0.2f, 0.3f);
            yield return null;
        }
        Destroy(flash);

        // 3) pronto: mais rápido, mais bravo, mais forte
        Fase2 = true; transformando = false;
        ritmo = 0.55f; mulVel = 1.35f; danoGolpe *= 1.25f; danoPisao *= 1.2f;
        proximoAtaque = Time.time + 0.9f; proximaPedra = Time.time + 3.5f; proximoRaio = Time.time + 5f;
        if (barra != null) barra.Cores(new Color(0.85f, 0.18f, 0.1f), new Color(0.3f, 0.04f, 0.04f));
        estado = Estado.Perseguindo;
    }
}
