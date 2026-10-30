using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// O dragão do fim do jogo (o que destruiu as vilas), voando longe no lobby.
// Dragão 3D montado por código: corpo, pescoço, cabeça com mandíbula e
// chifres, cauda em gomos que ondula e asas de morcego em duas partes que
// batem em onda. Voa inclinando nas curvas e, de tempos em tempos, abaixa a
// cabeça e solta um jato de fogo que acende o chão lá embaixo.
public class DragaoDistante : MonoBehaviour
{
    [Header("Modelo do dragão (o menu Pinglu > 30 preenche)")]
    public GameObject modelo;
    public AnimationClip animVoo, animPlanar, animSopro;
    [Tooltip("Corrige a direção do modelo, se ele voar de lado ou de costas")]
    public Vector3 rotacaoDoModelo = Vector3.zero;
    [Tooltip("Tamanho do dragão no lobby (maior medida, em metros)")]
    public float comprimentoDoModelo = 34f;

    [Header("Aparência (dragão feito por código, usado se não houver modelo)")]
    public Color corDasEscamas = new Color(0.28f, 0.11f, 0.07f);
    public Color corDaMembrana = new Color(0.42f, 0.14f, 0.06f);
    public float tamanho = 1.6f;            // 1 = envergadura de ~14 m
    public Material materialParticula;
    [HideInInspector] public Material material; // (versão antiga em imagem; não é mais usada)

    [Header("Voo")]
    // voa atrás das ruínas de castelo, antes do céu, numa altura que aparece na câmera do lobby
    public Vector3 centroDoVoo = new Vector3(0f, 0f, -118f);
    public Vector2 raiosDoVoo = new Vector2(75f, 22f);
    public float alturaDoVoo = 26f;
    public float velocidadeDoVoo = 13f;     // metros por segundo

    [Header("Fogo")]
    public float intervaloDoSopro = 11f;

    private Transform corpo, pescoco, cabeca, mandibula, asaE, asaD, pontaE, pontaD;
    private readonly List<Transform> cauda = new List<Transform>();
    private Material matEscamas, matMembrana;
    private float t, ang, rolagem, proximoFogo, fimDoFogo = -1f, mergulho;
    private ParticleSystem chamas, fumacaDoSopro;
    private Light luzDoSopro;
    // modelo importado
    private bool usandoModelo;
    private Transform osso_boca;
    private PlayableGraph grafo;
    private AnimationMixerPlayable mixer;
    private float pesoSopro, pesoPlanar;

    // ------------------------------------------------------------------
    //  montagem
    // ------------------------------------------------------------------
    private void Start()
    {
        foreach (Transform f in transform) Destroy(f.gameObject); // tira a versão antiga, se houver
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        matEscamas = new Material(lit); matEscamas.SetColor("_BaseColor", corDasEscamas); matEscamas.SetFloat("_Smoothness", 0.4f);
        matMembrana = new Material(lit); matMembrana.SetColor("_BaseColor", corDaMembrana); matMembrana.SetFloat("_Smoothness", 0.15f);
        matMembrana.SetFloat("_Cull", 0f); matMembrana.doubleSidedGI = true;

        corpo = new GameObject("Dragao 3D").transform;
        corpo.SetParent(transform, false);
        if (modelo != null) { MontarModelo(); return; }
        corpo.localScale = Vector3.one * tamanho;

        // tronco (da base da cauda até o peito), engrossando no peito
        Peca("Tronco", corpo, Tubo(new[] { new Vector3(0, 0, -3.2f), new Vector3(0, 0.1f, -1.5f), new Vector3(0, 0.15f, 0.5f), new Vector3(0, 0.1f, 2.2f), new Vector3(0, 0.25f, 3.2f) },
                                    new[] { 0.55f, 0.9f, 1.1f, 0.85f, 0.55f }, 10), matEscamas);
        // pernas encolhidas
        for (int s = -1; s <= 1; s += 2)
        {
            Peca("Perna", corpo, Tubo(new[] { new Vector3(s * 0.6f, -0.5f, 1.6f), new Vector3(s * 0.7f, -1.1f, 1.2f), new Vector3(s * 0.65f, -1.2f, 0.5f) }, new[] { 0.28f, 0.2f, 0.1f }, 6), matEscamas);
            Peca("Pata", corpo, Tubo(new[] { new Vector3(s * 0.55f, -0.4f, -1.6f), new Vector3(s * 0.7f, -1.0f, -2.2f), new Vector3(s * 0.6f, -1.1f, -3.0f) }, new[] { 0.32f, 0.22f, 0.1f }, 6), matEscamas);
        }
        // pescoço e cabeça
        pescoco = new GameObject("Pescoco").transform; pescoco.SetParent(corpo, false); pescoco.localPosition = new Vector3(0, 0.25f, 3.0f);
        Peca("Pescoco", pescoco, Tubo(new[] { Vector3.zero, new Vector3(0, 0.45f, 1.3f), new Vector3(0, 0.75f, 2.7f), new Vector3(0, 0.75f, 3.6f) }, new[] { 0.55f, 0.42f, 0.34f, 0.3f }, 8), matEscamas);
        cabeca = new GameObject("Cabeca").transform; cabeca.SetParent(pescoco, false); cabeca.localPosition = new Vector3(0, 0.75f, 3.6f);
        Peca("Cranio", cabeca, Tubo(new[] { new Vector3(0, 0.05f, -0.2f), new Vector3(0, 0.1f, 0.6f), new Vector3(0, 0.0f, 1.5f), new Vector3(0, -0.05f, 2.0f) }, new[] { 0.36f, 0.32f, 0.2f, 0.08f }, 8), matEscamas);
        for (int s = -1; s <= 1; s += 2)
            Peca("Chifre", cabeca, Tubo(new[] { new Vector3(s * 0.2f, 0.25f, 0.1f), new Vector3(s * 0.35f, 0.5f, -0.5f), new Vector3(s * 0.4f, 0.55f, -1.1f) }, new[] { 0.09f, 0.06f, 0.01f }, 5), matEscamas);
        mandibula = new GameObject("Mandibula").transform; mandibula.SetParent(cabeca, false); mandibula.localPosition = new Vector3(0, -0.18f, 0.2f);
        Peca("Mandibula", mandibula, Tubo(new[] { Vector3.zero, new Vector3(0, -0.05f, 0.9f), new Vector3(0, -0.02f, 1.6f) }, new[] { 0.2f, 0.16f, 0.05f }, 6), matEscamas);

        // cauda em gomos (cada gomo segue o anterior, para ondular)
        Transform pai = corpo; Vector3 inicio = new Vector3(0, 0, -3.1f);
        for (int i = 0; i < 7; i++)
        {
            Transform g = new GameObject("Cauda " + i).transform;
            g.SetParent(pai, false); g.localPosition = inicio;
            float r0 = Mathf.Lerp(0.5f, 0.08f, i / 7f), r1 = Mathf.Lerp(0.5f, 0.08f, (i + 1) / 7f);
            Peca("Gomo", g, Tubo(new[] { Vector3.zero, new Vector3(0, 0, -1.1f) }, new[] { r0, r1 }, 7), matEscamas);
            cauda.Add(g); pai = g; inicio = new Vector3(0, 0, -1.05f);
        }
        Peca("Ponta da Cauda", pai, Ponta(), matEscamas);

        // asas: parte de dentro (no ombro) e parte de fora (no "cotovelo"), cada lado
        asaD = Asa(1); asaE = Asa(-1);
        pontaD = asaD.GetChild(asaD.childCount - 1); pontaE = asaE.GetChild(asaE.childCount - 1);

        CriarSopro();
        proximoFogo = Time.unscaledTime + 5f;
        ang = Random.Range(0f, Mathf.PI * 2f);
    }

    // ------------------------------------------------------------------
    //  modelo importado (o dragão que vocês escolheram)
    // ------------------------------------------------------------------
    private void MontarModelo()
    {
        usandoModelo = true;
        GameObject m = Instantiate(modelo);
        m.name = "Modelo do Dragao";
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        m.transform.SetParent(corpo, false);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.Euler(rotacaoDoModelo);
        m.transform.localScale = Vector3.one;
        // ajusta o tamanho e centraliza
        Bounds b = new Bounds(); bool tem = false;
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer) ((SkinnedMeshRenderer)r).updateWhenOffscreen = true;
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        float maior = tem ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) : 1f;
        float esc = comprimentoDoModelo / Mathf.Max(0.01f, maior);
        m.transform.localScale = Vector3.one * esc;
        if (tem) m.transform.position += corpo.position - (corpo.position + (b.center - corpo.position) * esc);

        osso_boca = Achar(m.transform, new[] { "jaw", "mouth", "tongue", "boca", "mandibula", "head", "cabeca" });
        // animações
        if (animVoo != null || animSopro != null || animPlanar != null)
        {
            Animator an = m.GetComponent<Animator>();
            if (an == null) an = m.AddComponent<Animator>();
            an.applyRootMotion = false;
            Animation leg = m.GetComponent<Animation>();
            if (leg != null) leg.enabled = false;
            grafo = PlayableGraph.Create("Dragao");
            grafo.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            mixer = AnimationMixerPlayable.Create(grafo, 3);
            AnimationClip[] c = { animVoo, animPlanar != null ? animPlanar : animVoo, animSopro != null ? animSopro : animVoo };
            for (int i = 0; i < 3; i++)
                if (c[i] != null) grafo.Connect(AnimationClipPlayable.Create(grafo, c[i]), 0, mixer, i);
            mixer.SetInputWeight(0, 1f);
            AnimationPlayableOutput.Create(grafo, "Saida", an).SetSourcePlayable(mixer);
            grafo.Play();
        }

        // o sopro sai da boca (segue o osso, mas aponta para frente e para baixo)
        mandibula = new GameObject("Boca").transform;
        mandibula.SetParent(corpo, false);
        mandibula.localPosition = new Vector3(0f, 0f, comprimentoDoModelo * 0.45f);
        CriarSopro();
        chamas.transform.localPosition = Vector3.zero;
        proximoFogo = Time.unscaledTime + 5f;
        ang = Random.Range(0f, Mathf.PI * 2f);
    }

    private static Transform Achar(Transform raiz, string[] nomes)
    {
        foreach (string n in nomes)
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains(n)) return t;
        return null;
    }

    private void OnDestroy() { if (grafo.IsValid()) grafo.Destroy(); }

    private void AnimarModelo(float dt, bool soprando, bool jato)
    {
        // posição da boca: segue o osso; o jato aponta para frente e para baixo
        if (osso_boca != null) mandibula.position = osso_boca.position + corpo.forward * comprimentoDoModelo * 0.02f;
        chamas.transform.rotation = Quaternion.LookRotation((corpo.forward + Vector3.down * 0.9f).normalized);
        if (!mixer.IsValid()) return;
        bool planando = Mathf.PerlinNoise(Time.unscaledTime * 0.15f, 3f) > 0.62f && !soprando && animPlanar != null;
        pesoSopro = Mathf.MoveTowards(pesoSopro, soprando && animSopro != null ? 1f : 0f, dt * 2.5f);
        pesoPlanar = Mathf.MoveTowards(pesoPlanar, planando ? 1f : 0f, dt * 1.2f);
        float voo = Mathf.Clamp01(1f - pesoSopro - pesoPlanar);
        mixer.SetInputWeight(0, voo);
        mixer.SetInputWeight(1, pesoPlanar * (1f - pesoSopro));
        mixer.SetInputWeight(2, pesoSopro);
        if (soprando && pesoSopro < 0.05f && mixer.GetInput(2).IsValid()) mixer.GetInput(2).SetTime(0);
    }

    private Transform Asa(int lado)
    {
        Transform ombro = new GameObject(lado > 0 ? "Asa Direita" : "Asa Esquerda").transform;
        ombro.SetParent(corpo, false); ombro.localPosition = new Vector3(lado * 0.7f, 0.6f, 1.6f);
        ombro.localScale = Vector3.one * 1.9f; // asas grandes: envergadura quase o dobro do corpo
        // parte de dentro: do ombro até o cotovelo, presa no flanco do corpo
        Peca("Membrana", ombro, Membrana(lado, new[] { new Vector3(0, 0, 0), new Vector3(lado * 3.2f, 0.4f, 0.7f), new Vector3(lado * 3.6f, 0.2f, -1.4f), new Vector3(lado * 2.2f, 0f, -2.6f), new Vector3(0, -0.1f, -3.6f) }), matMembrana);
        Peca("Osso", ombro, Tubo(new[] { Vector3.zero, new Vector3(lado * 3.2f, 0.4f, 0.7f) }, new[] { 0.18f, 0.12f }, 6), matEscamas);
        // parte de fora: dedos abertos com a borda recortada
        Transform cotovelo = new GameObject("Cotovelo").transform;
        cotovelo.SetParent(ombro, false); cotovelo.localPosition = new Vector3(lado * 3.2f, 0.4f, 0.7f);
        Vector3[] dedos = { new Vector3(lado * 4.6f, 0.2f, 0.9f), new Vector3(lado * 5.2f, 0f, -1.1f), new Vector3(lado * 4.3f, -0.1f, -2.9f), new Vector3(lado * 2.6f, -0.2f, -3.9f) };
        List<Vector3> borda = new List<Vector3> { Vector3.zero };
        for (int i = 0; i < dedos.Length; i++)
        {
            borda.Add(dedos[i]);
            if (i + 1 < dedos.Length) borda.Add(Vector3.Lerp(dedos[i], dedos[i + 1], 0.5f) * 0.78f); // recorte entre os dedos
        }
        borda.Add(new Vector3(lado * 0.4f, -0.2f, -2.1f));
        Peca("Membrana", cotovelo, Membrana(lado, borda.ToArray()), matMembrana);
        foreach (Vector3 d in dedos) Peca("Dedo", cotovelo, Tubo(new[] { Vector3.zero, d }, new[] { 0.1f, 0.03f }, 5), matEscamas);
        Peca("Garra", cotovelo, Tubo(new[] { Vector3.zero, new Vector3(lado * 0.3f, 0.5f, 0.4f) }, new[] { 0.08f, 0.01f }, 5), matEscamas);
        return ombro;
    }

    // ------------------------------------------------------------------
    //  geometria
    // ------------------------------------------------------------------
    private static void Peca(string nome, Transform pai, Mesh m, Material mat)
    {
        GameObject g = new GameObject(nome);
        g.transform.SetParent(pai, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        MeshRenderer r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // tubo que passa pelos pontos, com o raio de cada ponto (fechado nas pontas)
    private static Mesh Tubo(Vector3[] p, float[] raio, int lados)
    {
        List<Vector3> v = new List<Vector3>(); List<int> t = new List<int>();
        for (int i = 0; i < p.Length; i++)
        {
            Vector3 dir = (i + 1 < p.Length ? p[i + 1] - p[i] : p[i] - p[i - 1]).normalized;
            Vector3 a = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized, b = Vector3.Cross(dir, a);
            for (int k = 0; k < lados; k++)
            {
                float ang = k / (float)lados * Mathf.PI * 2f;
                v.Add(p[i] + (a * Mathf.Cos(ang) * 1.0f + b * Mathf.Sin(ang) * 0.85f) * raio[i]);
            }
        }
        for (int i = 0; i + 1 < p.Length; i++)
            for (int k = 0; k < lados; k++)
            {
                int a0 = i * lados + k, a1 = i * lados + (k + 1) % lados, b0 = a0 + lados, b1 = a1 + lados;
                t.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        int c0 = v.Count; v.Add(p[0]); int c1 = v.Count; v.Add(p[p.Length - 1]);
        int ult = (p.Length - 1) * lados;
        for (int k = 0; k < lados; k++)
        {
            t.AddRange(new[] { c0, (k + 1) % lados, k });
            t.AddRange(new[] { c1, ult + k, ult + (k + 1) % lados });
        }
        Mesh m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // membrana fina (leque saindo do primeiro ponto), vista dos dois lados
    private static Mesh Membrana(int lado, Vector3[] borda)
    {
        List<Vector3> v = new List<Vector3>(borda); List<int> t = new List<int>();
        for (int i = 1; i + 1 < borda.Length; i++) t.AddRange(new[] { 0, i, i + 1 });
        Mesh m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    private static Mesh Ponta()
    {
        Vector3[] v = { new Vector3(0, 0, 0), new Vector3(0.45f, 0, -0.5f), new Vector3(0, 0, -1.3f), new Vector3(-0.45f, 0, -0.5f), new Vector3(0, 0.12f, -0.5f), new Vector3(0, -0.12f, -0.5f) };
        int[] t = { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4, 0, 5, 1, 1, 5, 2, 2, 5, 3, 3, 5, 0 };
        Mesh m = new Mesh { vertices = v, triangles = t }; m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------------
    //  sopro de fogo
    // ------------------------------------------------------------------
    private void CriarSopro()
    {
        GameObject g = new GameObject("Sopro de Fogo");
        g.transform.SetParent(mandibula, false);
        g.transform.localPosition = new Vector3(0f, 0.1f, 1.4f);
        chamas = g.AddComponent<ParticleSystem>();
        chamas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = chamas.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.8f); m.startSpeed = new ParticleSystem.MinMaxCurve(26f, 34f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); m.maxParticles = 600;
        m.startColor = new Color(1f, 1f, 1f, 1f);
        var em = chamas.emission; em.rateOverTime = 220f;
        var sh = chamas.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5f; sh.radius = 0.25f;
        var tam = chamas.sizeOverLifetime; tam.enabled = true; tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 5.5f));
        var col = chamas.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.97f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.25f), 0.2f),
                           new GradientColorKey(new Color(1f, 0.4f, 0.08f), 0.5f), new GradientColorKey(new Color(0.6f, 0.12f, 0.05f), 0.8f), new GradientColorKey(new Color(0.15f, 0.1f, 0.1f), 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0.5f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        var ru = chamas.noise; ru.enabled = true; ru.strength = 2.5f; ru.frequency = 0.8f;
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 1.4f;
        if (materialParticula != null) r.sharedMaterial = materialParticula;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // fumaça escura que sobra do jato
        GameObject gf = new GameObject("Fumaca do Sopro");
        gf.transform.SetParent(g.transform, false);
        fumacaDoSopro = gf.AddComponent<ParticleSystem>();
        fumacaDoSopro.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var mf = fumacaDoSopro.main; mf.loop = true; mf.simulationSpace = ParticleSystemSimulationSpace.World;
        mf.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); mf.startSpeed = new ParticleSystem.MinMaxCurve(14f, 20f);
        mf.startSize = new ParticleSystem.MinMaxCurve(2f, 4f); mf.startColor = new Color(0.12f, 0.1f, 0.1f, 0.5f);
        var ef = fumacaDoSopro.emission; ef.rateOverTime = 30f;
        var sf = fumacaDoSopro.shape; sf.shapeType = ParticleSystemShapeType.Cone; sf.angle = 9f;
        var tf = fumacaDoSopro.sizeOverLifetime; tf.enabled = true; tf.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 3f));
        if (materialParticula != null) gf.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialParticula;

        luzDoSopro = g.AddComponent<Light>();
        luzDoSopro.type = LightType.Point; luzDoSopro.color = new Color(1f, 0.5f, 0.15f); luzDoSopro.range = 45f; luzDoSopro.intensity = 0f; luzDoSopro.shadows = LightShadows.None;
    }

    // incêndio onde o jato bate no chão
    private void FogoNoChao(Vector3 p)
    {
        GameObject g = new GameObject("Fogo do Dragao");
        g.transform.SetParent(transform, true);
        g.transform.position = p;
        g.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = false; m.duration = 3f; m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        m.startSize = new ParticleSystem.MinMaxCurve(2f, 5f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.2f, 0.9f), new Color(1f, 0.35f, 0.08f, 0.8f));
        var em = ps.emission; em.rateOverTime = 60f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 4f;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.3f, 0.1f, 0.08f), 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        if (materialParticula != null) g.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialParticula;
        ps.Play();
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.12f); l.range = 30f; l.intensity = 9f; l.shadows = LightShadows.None;
        g.AddComponent<SumirLuzDragao>();
        Destroy(g, 4.5f);
    }

    // ------------------------------------------------------------------
    //  voo e animação
    // ------------------------------------------------------------------
    private Vector3 Caminho(float a)
    {
        return transform.position + centroDoVoo + new Vector3(Mathf.Sin(a) * raiosDoVoo.x, alturaDoVoo + Mathf.Sin(a * 2f) * 3f - mergulho, Mathf.Cos(a) * raiosDoVoo.y);
    }

    private void Update()
    {
        if (corpo == null) return;
        float dt = Time.unscaledDeltaTime;
        float raioMedio = (raiosDoVoo.x + raiosDoVoo.y) * 0.5f;
        ang += velocidadeDoVoo / raioMedio * dt;
        bool soprando = Time.unscaledTime < fimDoFogo;
        mergulho = Mathf.MoveTowards(mergulho, soprando ? 5f : 0f, dt * 3f);

        Vector3 pos = Caminho(ang), prox = Caminho(ang + 0.05f), depois = Caminho(ang + 0.1f);
        Vector3 frente = (prox - pos).normalized;
        // inclina para dentro da curva
        Vector3 curva = Vector3.Cross(frente, (depois - prox).normalized);
        rolagem = Mathf.Lerp(rolagem, Mathf.Clamp(-curva.y * 900f, -32f, 32f), dt * 2f);

        if (usandoModelo)
        {
            corpo.position = pos;
            corpo.rotation = Quaternion.LookRotation(frente, Vector3.up) * Quaternion.Euler(soprando ? 12f : 0f, 0f, rolagem);
            bool jatoM = soprando && Time.unscaledTime > fimDoFogo - 2.0f;
            AnimarModelo(dt, soprando, jatoM);
            Sopro(soprando, dt);
            return;
        }

        // asas: bate em onda; de vez em quando plana com as asas abertas
        float plano = Mathf.PerlinNoise(Time.unscaledTime * 0.15f, 3f) > 0.62f && !soprando ? 0.15f : 1f;
        t += dt * Mathf.Lerp(3.2f, 4.2f, plano);
        float bater = Mathf.Sin(t) * plano;
        float atraso = Mathf.Sin(t - 0.9f) * plano;
        corpo.position = pos + Vector3.up * (-bater * 0.6f * tamanho);
        corpo.rotation = Quaternion.LookRotation(frente, Vector3.up) * Quaternion.Euler(-bater * 3f, 0f, rolagem);
        asaD.localRotation = Quaternion.Euler(0f, 0f, 12f + bater * 42f);
        asaE.localRotation = Quaternion.Euler(0f, 0f, -12f - bater * 42f);
        pontaD.localRotation = Quaternion.Euler(0f, 0f, atraso * 30f);
        pontaE.localRotation = Quaternion.Euler(0f, 0f, -atraso * 30f);

        // cauda ondulando e pescoço acompanhando
        for (int i = 0; i < cauda.Count; i++)
            cauda[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.unscaledTime * 1.6f - i * 0.6f) * 6f + 2f, Mathf.Sin(Time.unscaledTime * 1.1f - i * 0.5f) * 8f, 0f);
        float abaixar = soprando ? 28f : 0f;
        pescoco.localRotation = Quaternion.Slerp(pescoco.localRotation, Quaternion.Euler(abaixar + Mathf.Sin(t * 0.5f) * 3f, Mathf.Sin(Time.unscaledTime * 0.7f) * 6f, 0f), dt * 3f);
        cabeca.localRotation = Quaternion.Slerp(cabeca.localRotation, Quaternion.Euler(soprando ? 15f : -5f, 0f, 0f), dt * 3f);
        mandibula.localRotation = Quaternion.Slerp(mandibula.localRotation, Quaternion.Euler(soprando ? 32f : 4f, 0f, 0f), dt * 6f);

        Sopro(soprando, dt);
    }

    private void Sopro(bool soprando, float dt)
    {
        // sopro de fogo
        if (!soprando && Time.unscaledTime >= proximoFogo)
        {
            fimDoFogo = Time.unscaledTime + 2.4f;
            proximoFogo = fimDoFogo + intervaloDoSopro + Random.Range(-3f, 5f);
            Invoke(nameof(AcenderChao), 0.9f);
        }
        bool jato = soprando && Time.unscaledTime > fimDoFogo - 2.0f;
        if (jato && !chamas.isPlaying) { chamas.Play(); fumacaDoSopro.Play(); }
        if (!jato && chamas.isEmitting) { chamas.Stop(true, ParticleSystemStopBehavior.StopEmitting); fumacaDoSopro.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        luzDoSopro.intensity = jato ? 10f + Mathf.PerlinNoise(Time.unscaledTime * 10f, 1f) * 8f : Mathf.MoveTowards(luzDoSopro.intensity, 0f, dt * 25f);
    }

    private void AcenderChao()
    {
        if (chamas == null) return;
        Vector3 boca = chamas.transform.position, dir = chamas.transform.forward;
        float chao = transform.position.y + 0.5f;
        float k = dir.y < -0.05f ? (chao - boca.y) / dir.y : 25f;
        Vector3 alvo = boca + dir * Mathf.Clamp(k, 8f, 45f);
        alvo.y = chao;                       // o fogo sempre nasce no chão
        FogoNoChao(alvo);
    }
}

public class SumirLuzDragao : MonoBehaviour
{
    private Light l; private float inicio;
    private void Awake() { l = GetComponent<Light>(); inicio = Time.unscaledTime; }
    private void Update() { if (l != null) l.intensity = Mathf.Lerp(9f, 0f, (Time.unscaledTime - inicio) / 4f); }
}
