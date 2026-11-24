using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// O CAIO REAL: o personagem "Caio, o Ancião" ganha o rosto do nosso coordenador do curso (óculos, cabelo castanho levantado, barba curta),
// com túnica azul, cinto, botas e cajado. É feito inteiro por código (peças simples + esqueleto de juntas) e tem animação própria:
// respira, olha para o jogador que chega perto, pisca, gesticula e mexe a boca quando fala (conversa do DialogoUI), anda e comemora.
// Entra sozinho no lugar do Caio antigo (o corpo antigo só fica escondido). Os dados do modelo estão em CaioReal_Dados.cs (gerado).
public class CaioReal : MonoBehaviour
{
    // ------------------------------------------------------------------ instalação automática
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar() { SceneManager.sceneLoaded -= AoCarregar; SceneManager.sceneLoaded += AoCarregar; Instalar(); }
    static void AoCarregar(Scene s, LoadSceneMode m) { Instalar(); }

    static void Instalar()
    {
        foreach (NpcFalante n in FindObjectsByType<NpcFalante>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (n == null || n.nomeExibido != "Caio" || n.GetComponentInChildren<CaioReal>(true) != null) continue;
            GameObject g = new GameObject("Caio real"); g.transform.SetParent(n.transform, false); g.AddComponent<CaioReal>();
            EsconderCorpoAntigo(n.gameObject);
        }
    }

    // o corpo antigo (uma malha só, na raiz do Caio) fica escondido; a etiqueta com o nome continua
    public static void EsconderCorpoAntigo(GameObject raiz)
    {
        MeshRenderer mr = raiz.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
    }

    // ------------------------------------------------------------------ estado e controle externo
    string forcado = ""; float velForcada = 1f;
    /// <summary>Força um estado: "Parado", "Andar_Calmo", "Falar" ou "Comemorar" (vazio = automático). Falar é automático quando o Caio está falando no diálogo.</summary>
    public void Forcar(string estado, float velocidade = 1f) { forcado = estado ?? ""; velForcada = velocidade; }

    readonly Dictionary<string, Transform> juntas = new Dictionary<string, Transform>();
    readonly Dictionary<string, List<Transform>> porTag = new Dictionary<string, List<Transform>>();
    readonly Dictionary<Transform, Vector3> basePos = new Dictionary<Transform, Vector3>();
    Transform modelo, cajado; Light luzCristal; Material matCristal;
    float proximoPiscar = 2f, piscando = -1f, olharY, tFase; Transform jogador; float proxBuscaJogador;

    void Start() { Montar(); }

    // ------------------------------------------------------------------ montagem
    void Montar()
    {
        Transform existente = transform.Find("Modelo");
        if (existente != null) { modelo = existente; Religar(); return; }                  // cópia (cena final): o modelo já veio pronto
        modelo = new GameObject("Modelo").transform; modelo.SetParent(transform, false);
        foreach (CaioRealDados.Junta j in CaioRealDados.Juntas)
        {
            Transform t = new GameObject(j.nome).transform; t.SetParent(string.IsNullOrEmpty(j.pai) ? modelo : juntas[j.pai], false); t.localPosition = j.pos; juntas[j.nome] = t;
        }
        cajado = new GameObject("Cajado").transform; cajado.SetParent(modelo, false);
        foreach (CaioRealDados.Parte p in CaioRealDados.Partes)
        {
            bool ehCajado = p.nome.StartsWith("cajado") || p.nome == "cristal";
            GameObject g = new GameObject(p.nome); Transform t = g.transform;
            t.SetParent(ehCajado ? cajado : juntas[p.junta], false);
            Vector3 pos = p.pos; Vector3 esc = p.esc;
            if (ehCajado)                                   // o cajado fica fincado no chão, vertical, na mão do Caio
            {
                if (p.nome == "cajado") { pos = new Vector3(0f, 0.975f, 0f); esc = new Vector3(0.032f, 1.95f, 0.032f); }
                else if (p.nome == "cajadoTopo") pos = new Vector3(0f, 1.94f, 0f);
                else pos = new Vector3(0f, 2.05f, 0f);
            }
            t.localPosition = pos; t.localRotation = ehCajado ? Quaternion.identity : Quaternion.Euler(p.rot);
            CaioRealDados.Parte q = p; q.esc = esc;
            MeshFilter mf = g.AddComponent<MeshFilter>(); mf.sharedMesh = Malha(q);
            MeshRenderer mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Material3(p.mat);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; mr.receiveShadows = true;
            if (!string.IsNullOrEmpty(p.tag)) { if (!porTag.ContainsKey(p.tag)) porTag[p.tag] = new List<Transform>(); porTag[p.tag].Add(t); }
            if (p.tag == "cristal") matCristal = mr.sharedMaterial;
        }
        GameObject lg = new GameObject("Luz do cristal"); lg.transform.SetParent(cajado, false); lg.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        luzCristal = lg.AddComponent<Light>(); luzCristal.type = LightType.Point; luzCristal.color = new Color(0.45f, 0.7f, 1f); luzCristal.range = 7f; luzCristal.intensity = 1.6f; luzCristal.shadows = LightShadows.None;
        GuardarBase();
    }

    // modelo copiado: acha de novo as juntas pelo nome
    void Religar()
    {
        Dictionary<string, Transform> todos = new Dictionary<string, Transform>();
        foreach (Transform t in modelo.GetComponentsInChildren<Transform>(true)) if (!todos.ContainsKey(t.name)) todos[t.name] = t;
        foreach (CaioRealDados.Junta j in CaioRealDados.Juntas) { Transform t; if (todos.TryGetValue(j.nome, out t)) juntas[j.nome] = t; }
        Transform c; todos.TryGetValue("Cajado", out c); cajado = c;
        foreach (CaioRealDados.Parte p in CaioRealDados.Partes)
        {
            if (string.IsNullOrEmpty(p.tag)) continue; Transform t;
            if (todos.TryGetValue(p.nome, out t)) { if (!porTag.ContainsKey(p.tag)) porTag[p.tag] = new List<Transform>(); porTag[p.tag].Add(t); if (p.tag == "cristal") { Renderer rr = t.GetComponent<Renderer>(); if (rr != null) matCristal = rr.sharedMaterial; } }
        }
        luzCristal = GetComponentInChildren<Light>(true);
        GuardarBase();
    }

    void GuardarBase() { foreach (var kv in porTag) foreach (Transform t in kv.Value) basePos[t] = t.localPosition; }

    // ------------------------------------------------------------------ animação
    void LateUpdate()
    {
        if (modelo == null || !juntas.ContainsKey("pelvis")) return;
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        bool falaNoDialogo = DialogoUI.Aberto && DialogoUI.FalanteAtual == "Caio";
        string est = falaNoDialogo ? "Falar" : (string.IsNullOrEmpty(forcado) ? "Parado" : forcado);
        float vel = falaNoDialogo ? 1f : velForcada;
        bool digitando = falaNoDialogo && DialogoUI.Digitando;
        Pose(est, t, vel);
        OlharParaOJogador(est, dt);
        Rosto(est, t, digitando);
        // cajado fincado no chão, na mão direita
        if (cajado != null)
        {
            Vector3 mao = juntas["punhoD"].position; cajado.position = new Vector3(mao.x, modelo.position.y, mao.z); cajado.rotation = modelo.rotation;
        }
        // cristal pulsando
        float pulso = 0.8f + 0.2f * Mathf.Sin(t * 2.4f);
        if (luzCristal != null) luzCristal.intensity = 1.6f * pulso;
        if (matCristal != null && matCristal.HasProperty("_EmissionColor")) { matCristal.EnableKeyword("_EMISSION"); matCristal.SetColor("_EmissionColor", new Color(0.35f, 0.65f, 1f) * (1.2f + 0.6f * pulso)); }
    }

    void R(string j, float x, float y = 0f, float z = 0f) { Transform t; if (juntas.TryGetValue(j, out t)) t.localRotation = Quaternion.Euler(x, y, z); }

    void Pose(string est, float t, float vel)
    {
        float resp = Mathf.Sin(t * 1.7f);
        // posição base de todas as juntas (as que não mexem ficam em repouso)
        foreach (var kv in juntas) if (kv.Key != "cabeca") kv.Value.localRotation = Quaternion.identity;
        modelo.localPosition = Vector3.zero; juntas["pelvis"].localPosition = new Vector3(0f, 0.95f, 0f);
        if (est == "Andar_Calmo")
        {
            float w = t * 6.2f * vel, s = Mathf.Sin(w), c = Mathf.Cos(w);
            juntas["pelvis"].localPosition = new Vector3(0f, 0.95f - 0.012f + Mathf.Abs(s) * 0.025f, 0f);
            R("quadrilE", -s * 30f); R("joelhoE", Mathf.Max(0f, c) * 38f); R("quadrilD", s * 30f); R("joelhoD", Mathf.Max(0f, -c) * 38f);
            R("ombroE", s * 22f, 0f, -4f); R("cotoveloE", -14f - Mathf.Max(0f, -s) * 10f);
            R("ombroD", -22f + s * 5f, 0f, 4f); R("cotoveloD", -45f);
            R("peito", 3f, s * 4f, 0f); R("pelvis", 0f, -s * 3f, 0f);
        }
        else if (est == "Falar")
        {
            R("ombroE", -40f + Mathf.Sin(t * 2.6f) * 14f, 0f, -12f); R("cotoveloE", -75f + Mathf.Sin(t * 3.4f) * 20f); R("punhoE", Mathf.Sin(t * 5f) * 10f, 0f, 0f);
            R("ombroD", -22f + Mathf.Sin(t * 1.3f) * 4f, 0f, 4f); R("cotoveloD", -45f);
            R("peito", Mathf.Sin(t * 2f) * 2f, Mathf.Sin(t * 1.1f) * 3f, 0f);
        }
        else if (est == "Comemorar")
        {
            float b = Mathf.Abs(Mathf.Sin(t * 4.5f));
            modelo.localPosition = new Vector3(0f, b * 0.14f, 0f);
            R("ombroE", -165f + Mathf.Sin(t * 8f) * 15f, 0f, -12f); R("cotoveloE", -20f + Mathf.Sin(t * 8f) * 10f);
            R("ombroD", -150f + Mathf.Sin(t * 8f + 1f) * 15f, 0f, 12f); R("cotoveloD", -25f);
            R("quadrilE", -10f * b); R("quadrilD", -6f * b); R("joelhoE", 12f * b); R("joelhoD", 12f * b);
            R("peito", Mathf.Sin(t * 4.5f) * 4f, Mathf.Sin(t * 2.2f) * 6f, 0f);
        }
        else   // Parado: respira, braço do cajado dobrado, o outro solto
        {
            R("peito", resp * 0.9f, 0f, 0f); R("pescoco", resp * 0.5f);
            R("ombroE", 3f + resp * 1.5f, 0f, -5f); R("cotoveloE", -8f);
            R("ombroD", -22f, 0f, 4f); R("cotoveloD", -45f);
            R("pelvis", 0f, Mathf.Sin(t * 0.5f) * 2f, 0f);
        }
    }

    // a cabeça acompanha o jogador que está por perto (até 60 graus para cada lado)
    void OlharParaOJogador(string est, float dt)
    {
        if (Time.unscaledTime >= proxBuscaJogador)
        {
            proxBuscaJogador = Time.unscaledTime + 0.5f; jogador = null; float melhor = 100f;
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            {
                if (p == null || !p.enabled || p.Companheiro) continue; float d = (p.transform.position - transform.position).sqrMagnitude; if (d < melhor) { melhor = d; jogador = p.transform; }
            }
        }
        float alvoY = Mathf.Sin(Time.unscaledTime * 0.45f) * 6f, alvoX = Mathf.Sin(Time.unscaledTime * 0.6f) * 2f;
        if (jogador != null && est != "Comemorar")
        {
            Vector3 d = jogador.position - modelo.position; d.y = 0f;
            if (d.sqrMagnitude > 0.5f) { float ang = Vector3.SignedAngle(modelo.forward, d, Vector3.up); alvoY = Mathf.Clamp(ang, -60f, 60f); }
        }
        if (est == "Falar") alvoX += Mathf.Sin(Time.unscaledTime * 3.8f) * 3f;
        olharY = Mathf.Lerp(olharY, alvoY, 1f - Mathf.Exp(-5f * dt));
        juntas["cabeca"].localRotation = Quaternion.Euler(alvoX, olharY, 0f);
    }

    // piscar, sobrancelhas e boca (falando, a boca abre e fecha acompanhando o texto)
    void Rosto(string est, float t, bool digitando)
    {
        if (t >= proximoPiscar && piscando < 0f) { piscando = t; proximoPiscar = t + Random.Range(2.5f, 5.5f); }
        float p = 0f; if (piscando >= 0f) { float k = (t - piscando) / 0.16f; p = k < 0.5f ? k * 2f : (k < 1f ? (1f - k) * 2f : 0f); if (k >= 1f) piscando = -1f; }
        List<Transform> lista;
        if (porTag.TryGetValue("palpebra", out lista)) foreach (Transform pl in lista) pl.localScale = new Vector3(1f, Mathf.Lerp(1f, 8f, p), 1f);
        float abre = digitando ? Mathf.Clamp01(Mathf.Abs(Mathf.Sin(t * 14f)) * 0.9f + 0.1f * Mathf.Sin(t * 23f)) : 0f;
        if (est == "Comemorar") abre = 0.7f;
        if (porTag.TryGetValue("boca", out lista)) foreach (Transform b in lista) b.localScale = new Vector3(1f, 1f + abre * 8f, 1f + abre * 0.4f);
        if (porTag.TryGetValue("labioInf", out lista)) foreach (Transform l in lista) { Vector3 b0; if (basePos.TryGetValue(l, out b0)) l.localPosition = b0 + Vector3.down * abre * 0.014f; }
        float sob = (est == "Falar" ? 0.004f + 0.003f * Mathf.Sin(t * 3f) : 0f) + (est == "Comemorar" ? 0.008f : 0f);
        if (porTag.TryGetValue("sobrancelha", out lista)) foreach (Transform s in lista) { Vector3 b0; if (basePos.TryGetValue(s, out b0)) s.localPosition = b0 + Vector3.up * sob; }
    }

    // ------------------------------------------------------------------ materiais
    static readonly Dictionary<string, Material> materiais = new Dictionary<string, Material>();
    static Material Material3(string nome)
    {
        Material m; if (materiais.TryGetValue(nome, out m) && m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard"); if (sh == null) sh = Shader.Find("Sprites/Default");
        m = new Material(sh); Color c = CaioRealDados.Cor(nome);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", nome == "oculos" || nome == "ouro" ? 0.55f : 0.12f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", nome == "ouro" ? 0.6f : 0f);
        materiais[nome] = m; return m;
    }

    // ------------------------------------------------------------------ geometria (a mesma do visualizador em Python)
    static readonly Dictionary<string, Mesh> malhas = new Dictionary<string, Mesh>();
    static Mesh Malha(CaioRealDados.Parte p)
    {
        string chave = p.forma + p.esc.ToString("F4") + p.extra.ToString("F3"); Mesh m;
        if (malhas.TryGetValue(chave, out m) && m != null) return m;
        List<Vector3> V = new List<Vector3>(), N = new List<Vector3>(); List<int> T = new List<int>();
        switch (p.forma)
        {
            case "ell": Elipse(p.esc, V, N, T); break;
            case "cap": Capsula(p.esc, V, N, T); break;
            case "fru": Tronco(p.esc, p.extra, V, N, T); break;
            case "box": Caixa(p.esc, V, N, T); break;
            default: Toro(p.esc, p.extra, V, N, T); break;
        }
        for (int i = 0; i + 2 < T.Count; i += 3)       // garante o sentido certo dos triângulos (frente para fora)
        {
            Vector3 a = V[T[i]], b = V[T[i + 1]], c = V[T[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), N[T[i]] + N[T[i + 1]] + N[T[i + 2]]) < 0f) { int x = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = x; }
        }
        m = new Mesh { name = "Caio " + p.forma }; m.SetVertices(V); m.SetNormals(N); m.SetTriangles(T, 0); m.RecalculateBounds();
        malhas[chave] = m; return m;
    }

    static void Grade(List<int> T, int linhas, int colunas, int inicio = 0)
    {
        for (int i = 0; i < linhas; i++) for (int j = 0; j < colunas; j++)
        {
            int a = inicio + i * (colunas + 1) + j, b = a + colunas + 1; T.Add(a); T.Add(b); T.Add(a + 1); T.Add(a + 1); T.Add(b); T.Add(b + 1);
        }
    }

    static void Elipse(Vector3 e, List<Vector3> V, List<Vector3> N, List<int> T)
    {
        const int lat = 12, lon = 20; Vector3 r = e * 0.5f;
        for (int i = 0; i <= lat; i++)
        {
            float th = Mathf.PI * i / lat;
            for (int j = 0; j <= lon; j++)
            {
                float ph = 2f * Mathf.PI * j / lon; Vector3 u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                V.Add(new Vector3(u.x * r.x, u.y * r.y, u.z * r.z)); N.Add(new Vector3(u.x / r.x, u.y / r.y, u.z / r.z).normalized);
            }
        }
        Grade(T, lat, lon);
    }

    static void Capsula(Vector3 e, List<Vector3> V, List<Vector3> N, List<int> T)
    {
        const int lon = 18, lat = 6; float rx = e.x * 0.5f, rz = e.z * 0.5f, ry = Mathf.Max(rx, rz) * 0.95f, h = Mathf.Max(0f, e.y - 2f * ry);
        List<Vector3> linhas = new List<Vector3>();        // (raio relativo, y relativo, deslocamento)
        for (int i = 0; i <= lat; i++) { float th = Mathf.PI * 0.5f * (1f - i / (float)lat); linhas.Add(new Vector3(Mathf.Cos(th), Mathf.Sin(th), h * 0.5f)); }
        for (int i = 0; i <= lat; i++) { float th = Mathf.PI * 0.5f * i / lat; linhas.Add(new Vector3(Mathf.Cos(th), -Mathf.Sin(th), -h * 0.5f)); }
        foreach (Vector3 l in linhas)
            for (int j = 0; j <= lon; j++)
            {
                float ph = 2f * Mathf.PI * j / lon, cx = Mathf.Cos(ph), cz = Mathf.Sin(ph);
                V.Add(new Vector3(cx * rx * l.x, l.z + l.y * ry, cz * rz * l.x)); N.Add(new Vector3(cx * l.x / rx, l.y / ry, cz * l.x / rz).normalized);
            }
        Grade(T, linhas.Count - 1, lon);
    }

    static void Tronco(Vector3 e, float k, List<Vector3> V, List<Vector3> N, List<int> T)
    {
        const int lon = 20; float rx = e.x * 0.5f, rz = e.z * 0.5f, h = e.y;
        for (int a = 0; a < 2; a++)
        {
            float y = a == 0 ? -h * 0.5f : h * 0.5f, s = a == 0 ? 1f : k;
            for (int j = 0; j <= lon; j++)
            {
                float ph = 2f * Mathf.PI * j / lon, cx = Mathf.Cos(ph), cz = Mathf.Sin(ph);
                V.Add(new Vector3(cx * rx * s, y, cz * rz * s)); N.Add(new Vector3(cx / rx, (1f - k) * (rx + rz) * 0.5f / Mathf.Max(h, 1e-4f) * 0.5f, cz / rz).normalized);
            }
        }
        for (int j = 0; j < lon; j++) { T.Add(j); T.Add(lon + 1 + j); T.Add(j + 1); T.Add(j + 1); T.Add(lon + 1 + j); T.Add(lon + 2 + j); }
        for (int a = 0; a < 2; a++)
        {
            float y = a == 0 ? -h * 0.5f : h * 0.5f, s = a == 0 ? 1f : k; int c = V.Count; Vector3 n = new Vector3(0f, a == 0 ? -1f : 1f, 0f);
            V.Add(new Vector3(0f, y, 0f)); N.Add(n);
            for (int j = 0; j <= lon; j++) { float ph = 2f * Mathf.PI * j / lon; V.Add(new Vector3(Mathf.Cos(ph) * rx * s, y, Mathf.Sin(ph) * rz * s)); N.Add(n); }
            for (int j = 0; j < lon; j++) { T.Add(c); T.Add(c + 1 + j); T.Add(c + 2 + j); }
        }
    }

    static void Caixa(Vector3 e, List<Vector3> V, List<Vector3> N, List<int> T)
    {
        float hx = e.x * 0.5f, hy = e.y * 0.5f, hz = e.z * 0.5f;
        Vector3[][] f =
        {
            new[] { new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz) },
            new[] { new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, -hy, -hz) },
            new[] { new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz) },
            new[] { new Vector3(-hx, -hy, hz), new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz) },
            new[] { new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz) },
            new[] { new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz) }
        };
        Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        for (int i = 0; i < 6; i++) { int c = V.Count; foreach (Vector3 v in f[i]) { V.Add(v); N.Add(n[i]); } T.Add(c); T.Add(c + 1); T.Add(c + 2); T.Add(c); T.Add(c + 2); T.Add(c + 3); }
    }

    static float Sgn(float v) { return v >= 0f ? 1f : -1f; }
    static void Toro(Vector3 e, float ex, List<Vector3> V, List<Vector3> N, List<int> T)
    {
        const int seg = 36, tubo = 8; float RX = e.x * 0.5f, RY = e.y * 0.5f, r = e.z * 0.5f;
        for (int i = 0; i <= seg; i++)
        {
            float a = 2f * Mathf.PI * i / seg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float cx = Sgn(ca) * Mathf.Pow(Mathf.Abs(ca), 2f / ex), cy = Sgn(sa) * Mathf.Pow(Mathf.Abs(sa), 2f / ex);
            for (int j = 0; j <= tubo; j++)
            {
                float b = 2f * Mathf.PI * j / tubo, cb = Mathf.Cos(b), sb = Mathf.Sin(b); Vector3 n = new Vector3(cx * cb, cy * cb, sb).normalized;
                V.Add(new Vector3(cx * RX + n.x * r, cy * RY + n.y * r, n.z * r)); N.Add(n);
            }
        }
        Grade(T, seg, tubo);
    }
}
