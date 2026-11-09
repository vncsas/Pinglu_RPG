using System;
using System.Collections.Generic;
using UnityEngine;

// O cristal guardado pelo chefe: sobe do buraco brilhando, fica flutuando e girando.
// Quando o jogador chega perto, aparece "[" + tecla + "] Pegar o cristal"; ao apertar a tecla (B), o cristal voa até o jogador e a história continua.
// É todo feito por código (malha, brilho, luz e partículas), sem precisar de modelo.
public class CristalDeHistoria : MonoBehaviour
{
    private enum Fase { Subindo, Flutuando, Voando, Controlado, Preso }
    private Fase fase = Fase.Subindo;
    private Vector3 fundo, flutua, partida;
    private Color cor;
    private Action aoPegar;
    private PracaDoGuardiao praca;
    private Transform corpo;
    private Transform[] cacos;
    private Light luz;
    private Material mat;
    private float t, alcance = 16f;
    private Transform jogador;
    public bool podePegar = true;
    public KeyCode tecla = KeyCode.B;
    public Action aoApertar;            // se tiver, apertar a tecla chama isto (e o cristal fica parado esperando a cena); senão o cristal voa até o jogador
    public bool manter;                 // no fim do voo, fica preso no alvo em vez de sumir
    public float tempoVoo = 1.1f;
    public Func<Vector3> alvoCustom;     // se tiver, o voo termina neste ponto (ex.: em cima do dedo do ladrão)
    public bool encolher = true;
    private float auraK = -1f, auraSeg = 1f; private GameObject auraEsfera; private Material auraMat;

    // aura negra em volta do cristal: uma bola escura que cresce e fumaça preta
    public void AuraNegra(float seg)
    {
        if (auraEsfera != null) return;
        auraSeg = Mathf.Max(0.1f, seg); auraK = 0f;
        auraEsfera = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(auraEsfera.GetComponent<Collider>());
        auraEsfera.transform.SetParent(transform, false); auraEsfera.transform.localScale = Vector3.zero;
        auraMat = new Material(Shader.Find("Sprites/Default")); auraMat.color = new Color(0.04f, 0f, 0.08f, 0f);
        Renderer r = auraEsfera.GetComponent<Renderer>(); r.sharedMaterial = auraMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void AtualizarAura()
    {
        if (auraK < 0f) return;
        auraK = Mathf.Min(1f, auraK + Time.deltaTime / auraSeg);
        float e = Mathf.SmoothStep(0f, 1f, auraK);
        auraEsfera.transform.localScale = Vector3.one * (3.0f * e) * (1f + 0.06f * Mathf.Sin(t * 6f));
        auraMat.color = new Color(0.05f, 0f, 0.1f, 0.4f * e);
        MudarCor(Color.Lerp(new Color(0.25f, 1f, 0.45f), new Color(0.5f, 0.08f, 0.7f), e));
        for (int i = 0; i < 2; i++)
            Brasa(transform.position + UnityEngine.Random.onUnitSphere * 1.5f * e, UnityEngine.Random.insideUnitSphere * 0.7f + Vector3.up * 0.4f, new Color(0.05f, 0.01f, 0.09f, 0.85f), UnityEngine.Random.Range(0.35f, 0.8f));
    }


    public void MudarCor(Color c) { cor = c; if (luz != null) luz.color = c; if (mat != null) mat.SetColor("_BaseColor", Color.Lerp(Color.black, c, 0.35f)); }

    // a cena leva o cristal para um ponto (o resto do tempo ele fica girando)
    public System.Collections.IEnumerator PlanarPara(Vector3 destino, float seg)
    {
        fase = Fase.Controlado; Vector3 a = transform.position;
        for (float k = 0f; k < seg; k += Time.deltaTime) { transform.position = Vector3.Lerp(a, destino, Mathf.SmoothStep(0f, 1f, k / seg)); yield return null; }
        transform.position = destino;
    }

    // pula a subida (usado no atalho de teste)
    public void PularSubida() { transform.position = flutua; fase = Fase.Flutuando; t = 0f; luz.intensity = 5f; }

    // some numa explosão de brasas
    public void Largar()
    {
        for (int i = 0; i < 60; i++) Brasa(transform.position, UnityEngine.Random.onUnitSphere * 7f, cor, UnityEngine.Random.Range(0.12f, 0.4f));
        Destroy(gameObject);
    }

    // alguém leva o cristal: ele voa até o alvo e some (chama aoChegar)
    public void Roubar(Transform alvo, Action aoChegar) { fase = Fase.Voando; t = 0f; partida = transform.position; jogador = alvo; aoPegar = aoChegar; }

    public static CristalDeHistoria Criar(PracaDoGuardiao praca, Material molde, Vector3 fundo, Vector3 flutua, Color cor, Action aoPegar)
    {
        GameObject g = new GameObject("Cristal da História");
        CristalDeHistoria c = g.AddComponent<CristalDeHistoria>();
        c.praca = praca; c.fundo = fundo; c.flutua = flutua; c.cor = cor; c.aoPegar = aoPegar;
        c.Montar(molde);
        return c;
    }

    private static Mesh MalhaCristal()
    {
        // bipirâmide de 6 lados, alongada, com faces chapadas (cara de cristal lapidado)
        const int L = 6; float r = 0.36f, alto = 0.95f, baixo = -0.62f, meio = 0.12f;
        List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); List<int> t = new List<int>();
        Vector3 topo = new Vector3(0f, alto, 0f), ponta = new Vector3(0f, baixo, 0f);
        for (int i = 0; i < L; i++)
        {
            float a0 = i * Mathf.PI * 2f / L, a1 = (i + 1) * Mathf.PI * 2f / L;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, meio, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, meio, Mathf.Sin(a1) * r);
            AddTri(v, n, t, p0, topo, p1); AddTri(v, n, t, p0, p1, ponta);
        }
        Mesh m = new Mesh { name = "CristalProcedural" };
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    // triângulo com normal para fora (a partir da origem do cristal)
    private static void AddTri(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
        Vector3 centro = (a + b + c) / 3f - new Vector3(0f, 0.12f, 0f);
        if (Vector3.Dot(nrm, centro) < 0f) { Vector3 x = b; b = c; c = x; nrm = -nrm; }
        int i0 = v.Count; v.Add(a); v.Add(b); v.Add(c); n.Add(nrm); n.Add(nrm); n.Add(nrm);
        t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
    }

    private void Montar(Material molde)
    {
        if (molde != null) mat = new Material(molde);
        else { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetFloat("_Smoothness", 0.9f); }
        mat.SetColor("_BaseColor", Color.Lerp(Color.black, cor, 0.35f));
        mat.SetColor("_EmissionColor", cor * 2.2f);
        mat.EnableKeyword("_EMISSION");
        Mesh m = MalhaCristal();
        GameObject c = new GameObject("Corpo"); c.transform.SetParent(transform, false);
        c.AddComponent<MeshFilter>().sharedMesh = m;
        MeshRenderer mr = c.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        c.transform.localScale = Vector3.one * 2.1f;
        corpo = c.transform;
        cacos = new Transform[4];
        for (int i = 0; i < cacos.Length; i++)
        {
            GameObject k = new GameObject("Caco"); k.transform.SetParent(transform, false);
            k.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer kr = k.AddComponent<MeshRenderer>(); kr.sharedMaterial = mat; kr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            k.transform.localScale = Vector3.one * 0.5f;
            cacos[i] = k.transform;
        }
        GameObject lg = new GameObject("Luz"); lg.transform.SetParent(transform, false);
        luz = lg.AddComponent<Light>(); luz.type = LightType.Point; luz.color = cor; luz.range = 18f; luz.intensity = 0f; luz.shadows = LightShadows.None;
        transform.position = fundo;
    }

    private void Update()
    {
        t += Time.deltaTime;
        AtualizarAura();
        float giro = fase == Fase.Flutuando ? 45f : 120f;
        corpo.Rotate(0f, giro * Time.deltaTime, 0f, Space.Self);
        for (int i = 0; i < cacos.Length; i++)
        {
            float a = t * 1.3f + i * Mathf.PI * 0.5f;
            cacos[i].localPosition = new Vector3(Mathf.Cos(a) * 1.5f, 0.2f + Mathf.Sin(t * 2f + i) * 0.3f, Mathf.Sin(a) * 1.5f);
            cacos[i].Rotate(70f * Time.deltaTime, 110f * Time.deltaTime, 0f);
        }
        float pulso = 0.85f + Mathf.Sin(t * 3.1f) * 0.15f;
        mat.SetColor("_EmissionColor", cor * (2.2f * pulso));

        if (Time.frameCount % 2 == 0)
            Brasa(transform.position + UnityEngine.Random.insideUnitSphere * 0.7f, new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(0.8f, 2.4f), UnityEngine.Random.Range(-0.6f, 0.6f)), cor, UnityEngine.Random.Range(0.1f, 0.28f));

        if (fase == Fase.Subindo)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 4f);
            transform.position = Vector3.Lerp(fundo, flutua, k) + new Vector3(Mathf.Sin(t * 40f), 0f, Mathf.Cos(t * 37f)) * 0.03f * (1f - k);
            luz.intensity = Mathf.Lerp(0f, 5f, k);
            if (Time.frameCount % 5 == 0) TremorDeCamera.Tremer(0.05f * (1f - k) + 0.01f, 0.3f);
            if (t >= 4f) { fase = Fase.Flutuando; t = 0f; }
        }
        else if (fase == Fase.Flutuando)
        {
            transform.position = flutua + Vector3.up * Mathf.Sin(t * 1.7f) * 0.25f;
            luz.intensity = 5f + Mathf.Sin(t * 5f) * 0.6f;
            jogador = JogadorPerto();
            if (podePegar && jogador != null && !SessaoDeJogo.MenuAberto && !DialogoUI.Aberto && Input.GetKeyDown(tecla))
            {
                if (aoApertar != null) { Action ap = aoApertar; aoApertar = null; fase = Fase.Controlado; ap(); return; }
                fase = Fase.Voando; t = 0f; partida = transform.position;
                for (int i = 0; i < 40; i++) Brasa(transform.position, UnityEngine.Random.onUnitSphere * 5f, cor, UnityEngine.Random.Range(0.12f, 0.35f));
            }
        }
        else if (fase == Fase.Controlado) { luz.intensity = Mathf.Lerp(luz.intensity, 7f, Time.deltaTime * 3f); }
        else if (fase == Fase.Preso)
        {
            if (alvoCustom != null) transform.position = alvoCustom() + Vector3.up * (0.1f * Mathf.Sin(t * 2f));
            else if (jogador != null) transform.position = jogador.position + Vector3.up * 1.9f + jogador.forward * 0.6f;
            transform.localScale = Vector3.one * (encolher ? 0.45f : 0.55f); luz.intensity = 4f + Mathf.Sin(t * 5f);
        }
        else
        {
            float k = Mathf.Clamp01(t / tempoVoo), e = k * k * (3f - 2f * k);
            Vector3 alvo = alvoCustom != null ? alvoCustom() : (jogador != null ? jogador.position + Vector3.up * 1.4f : partida);
            Vector3 p = Vector3.Lerp(partida, alvo, e); p.y += Mathf.Sin(e * Mathf.PI) * (alvoCustom != null ? 1.2f : 2.5f);
            transform.position = p; transform.localScale = Vector3.one * (encolher ? Mathf.Lerp(1f, 0.25f, e) : Mathf.Lerp(1f, 0.55f, e));
            luz.intensity = Mathf.Lerp(5f, 9f, e);
            if (k >= 1f && manter) { fase = Fase.Preso; Action am = aoPegar; aoPegar = null; if (am != null) am(); }
            else if (k >= 1f)
            {
                for (int i = 0; i < 60; i++) Brasa(transform.position, UnityEngine.Random.onUnitSphere * 7f, cor, UnityEngine.Random.Range(0.12f, 0.4f));
                Action a = aoPegar; Destroy(gameObject); if (a != null) a();
            }
        }
    }

    private ParticleSystem ps;
    private void Brasa(Vector3 pos, Vector3 vel, Color c, float tam)
    {
        if (praca != null) { praca.EmitirBrasa(pos, vel, c, tam); return; }
        if (ps == null)
        {
            GameObject g = new GameObject("Brasas do cristal"); ps = g.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule m = ps.main; m.playOnAwake = false; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 400; m.startSpeed = 0f; m.startSize = 0.2f; m.startLifetime = 2f;
            ParticleSystem.EmissionModule em = ps.emission; em.enabled = false; ParticleSystem.ShapeModule sh = ps.shape; sh.enabled = false;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime; col.enabled = true;
            Gradient gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) }); col.color = gr;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            ps.Play();
        }
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams(); ep.position = pos; ep.velocity = vel; ep.startSize = tam * 1.6f; ep.startLifetime = UnityEngine.Random.Range(1.2f, 2.4f); ep.startColor = c;
        ps.Emit(ep, 1);
    }

    private Transform JogadorPerto()
    {
        Transform melhor = null; float d = alcance * alcance;
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            if (!p.enabled || p.Companheiro) continue;
            float dd = (p.transform.position - flutua).sqrMagnitude;
            if (dd < d) { d = dd; melhor = p.transform; }
        }
        return melhor;
    }

    private void OnDestroy() { if (mat != null) Destroy(mat); if (ps != null) Destroy(ps.gameObject, 3f); }

    private void OnGUI()
    {
        if (!podePegar || fase != Fase.Flutuando || jogador == null || SessaoDeJogo.MenuAberto || DialogoUI.Aberto) return;
        float e = Screen.height / 1080f;
        GUIStyle s = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(34 * e) };
        Rect r = new Rect(0f, Screen.height * 0.78f, Screen.width, 50f * e);
        s.normal.textColor = new Color(0f, 0f, 0f, 0.85f); GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), "[" + tecla + "] Pegar o cristal", s);
        s.normal.textColor = Color.Lerp(Color.white, cor, 0.35f); GUI.Label(r, "[" + tecla + "] Pegar o cristal", s);
    }
}
