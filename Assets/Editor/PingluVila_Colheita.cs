// =====================================================================
//  PingluVila_Colheita.cs  -  o vilarejo cresce: plantações, pomar, moinho, colmeias e feno em volta
// ---------------------------------------------------------------------
//  Tudo é feito pelo código, com formas redondas e orgânicas (nada de caixas), no mesmo estilo das outras construções:
//   - 3 campos de TRIGO (hastes com espigas douradas balançando com o vento), com espantalho, fardos redondos, palheiro e cerca
//   - 2 hortas: milho, repolho e abóbora (com cipó e folhas), em fileiras sobre terra arada
//   - 1 pomar de MACIEIRAS (maçãs vermelhas penduradas e caídas, cestos)
//   - 1 moinho de vento de pedra com pás que giram
//   - colmeias de palha (cestos de abelha)
//  Os lugares são escolhidos sozinhos: perto das estradas, terreno suave, longe das casas e do centro.
//
//  COMO USAR:  Pinglu > 60. Ampliar o vilarejo (colheita e arredores)   (Play desligado; depois rode o 54 e Ctrl+S)
//  Para tirar: Pinglu > 61. Remover a colheita e os arredores
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string NOME_COLHEITA = "Colheita e Arredores";
    const string PASTA_COL = "Assets/Cenario/Vila/Colheita";

    // ------------------------------------------------------------------ gerador de malha com cor por vértice
    class CGb
    {
        public List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>();
        public List<Color> c = new List<Color>(); public List<int> t = new List<int>();
        public int Add(Vector3 p, Vector3 nr, float alt, Color col) { v.Add(p); n.Add(nr); uv.Add(new Vector2(0.5f, alt)); c.Add(col); return v.Count - 1; }
        public void Quad(int a, int b, int cc, int d) { t.Add(a); t.Add(b); t.Add(cc); t.Add(a); t.Add(cc); t.Add(d); }
        public void Anexar(CGb o, Vector3 pos, Quaternion rot, float esc, float tom)
        {
            int b = v.Count;
            for (int i = 0; i < o.v.Count; i++)
            {
                v.Add(pos + rot * (o.v[i] * esc)); n.Add(rot * o.n[i]); uv.Add(o.uv[i]);
                c.Add(new Color(o.c[i].r * tom, o.c[i].g * tom, o.c[i].b * tom, 1f));
            }
            for (int i = 0; i < o.t.Count; i++) t.Add(b + o.t[i]);
        }
        public Mesh Gerar(string nome)
        {
            Mesh m = new Mesh { name = nome };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    static Color CC(float r, float g, float b) { return new Color(r, g, b, 1f).linear; }

    // peça de revolução (maçã, abóbora, tronco, espiga...): perfil (raio, altura) de baixo para cima
    static void Rev(CGb g, Vector3 baseC, Quaternion rot, float[] r, float[] y, int seg, Color c0, Color c1, float alt0, float alt1, float rib = 0f, int ribs = 0, float rug = 0f, int seed = 0)
    {
        int np = r.Length, b = g.v.Count;
        for (int i = 0; i < np; i++)
        {
            float tt = np == 1 ? 0f : i / (float)(np - 1);
            int ia = Mathf.Max(0, i - 1), ib = Mathf.Min(np - 1, i + 1);
            Vector2 tg = new Vector2(r[ib] - r[ia], y[ib] - y[ia]); if (tg.sqrMagnitude < 1e-8f) tg = Vector2.up; tg.Normalize();
            Vector2 nn = new Vector2(tg.y, -tg.x);
            for (int j = 0; j <= seg; j++)
            {
                float phi = j * Mathf.PI * 2f / seg, cs = Mathf.Cos(phi), sn = Mathf.Sin(phi);
                float rr = r[i] * (1f + rib * Mathf.Cos(ribs * phi));
                if (rug > 0f) rr *= 1f + rug * (Mathf.PerlinNoise(Mathf.Abs(Mathf.Cos(phi * 0.5f)) * 3f + seed, i * 0.8f + seed * 1.7f) - 0.5f) * 2f;
                Vector3 p = new Vector3(cs * rr, y[i], sn * rr);
                Vector3 nr = new Vector3(cs * nn.x, nn.y, sn * nn.x).normalized;
                Color col = Color.Lerp(c0, c1, tt) * (0.94f + 0.12f * Mathf.PerlinNoise(phi * 1.3f + seed, i * 0.5f));
                col.a = 1f;
                g.Add(baseC + rot * p, rot * nr, Mathf.Lerp(alt0, alt1, tt), col);
            }
        }
        for (int i = 0; i < np - 1; i++)
            for (int j = 0; j < seg; j++) { int a = b + i * (seg + 1) + j; g.Quad(a, a + 1, a + seg + 2, a + seg + 1); }
    }

    // elipsoide (maçã, repolho, folhagem, espiga de milho...)
    static void Elip(CGb g, Vector3 c, Quaternion rot, float rx, float ry, int linhas, int seg, Color c0, Color c1, float rib = 0f, int ribs = 0, float rug = 0f, int seed = 0)
    {
        float[] r = new float[linhas + 1], y = new float[linhas + 1];
        for (int k = 0; k <= linhas; k++) { float tt = Mathf.PI * k / linhas; r[k] = rx * Mathf.Sin(tt); y[k] = -ry * Mathf.Cos(tt); }
        Rev(g, c, rot, r, y, seg, c0, c1, 0f, 0f, rib, ribs, rug, seed);
    }

    // lâmina/fita curvada (folha de trigo, de milho, haste, cipó); taper 0 = afina até a ponta, 1 = largura constante; vento = uv.y sobe de 0 a 1
    static Vector3 Fol(CGb g, Vector3 raiz, float yaw, float comp, float larg, float curva, float inclina, int seg, Color c0, Color c1, float taper, bool vento, out Vector3 antes)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 lado = q * Vector3.right, frente = q * Vector3.forward;
        int p0 = -1, p1 = -1; Vector3 ult = raiz; antes = raiz;
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            float w = larg * 0.5f * Mathf.Lerp(Mathf.Pow(1f - t, 0.8f), 1f, taper);
            Vector3 centro = raiz + Vector3.up * (comp * t * Mathf.Cos(inclina)) + frente * (comp * Mathf.Sin(inclina) * t + curva * t * t);
            centro.y -= curva * t * t * 0.35f;
            Color col = Color.Lerp(c0, c1, t);
            Vector3 nor = (Vector3.up + frente * 0.3f).normalized;
            int a0 = g.Add(centro - lado * w, nor, vento ? t : 0f, col), a1 = g.Add(centro + lado * w, nor, vento ? t : 0f, col);
            if (p0 >= 0) g.Quad(p0, a0, a1, p1);
            p0 = a0; p1 = a1; antes = ult; ult = centro;
        }
        return ult;
    }
    static Vector3 Fol(CGb g, Vector3 raiz, float yaw, float comp, float larg, float curva, float inclina, int seg, Color c0, Color c1, float taper, bool vento)
    { Vector3 a; return Fol(g, raiz, yaw, comp, larg, curva, inclina, seg, c0, c1, taper, vento, out a); }

    // ------------------------------------------------------------------ plantas (modelos pequenos que depois são repetidos nos campos)
    static CGb ProtoTrigo(int seed)
    {
        System.Random r = new System.Random(seed); System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        CGb g = new CGb();
        Color cb = CC(0.46f, 0.44f, 0.15f), ct = CC(0.86f, 0.70f, 0.26f), esp0 = CC(0.80f, 0.60f, 0.18f), esp1 = CC(0.95f, 0.78f, 0.34f);
        for (int k = 0; k < 4; k++)
        {
            float yaw = F(0f, 360f), d = F(0f, 0.14f), h = F(0.85f, 1.08f), inc = F(0.04f, 0.3f);
            Vector3 raiz = new Vector3(Mathf.Cos(yaw * Mathf.Deg2Rad) * d, 0f, Mathf.Sin(yaw * Mathf.Deg2Rad) * d);
            Vector3 antes; Vector3 topo = Fol(g, raiz, yaw + 90f, h, 0.03f, h * F(0.05f, 0.25f), inc, 2, cb, ct, 0.6f, true, out antes);
            Vector3 dir = (topo - antes).normalized;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir);
            Rev(g, topo - dir * 0.015f, rot, new[] { 0.004f, 0.024f, 0.03f, 0.02f, 0.004f }, new[] { 0f, 0.035f, 0.09f, 0.15f, 0.19f }, 4, esp0, esp1, 1f, 1f);
            for (int a = 0; a < 2; a++)      // arestas finas da espiga
            {
                Vector3 b2; Fol(g, topo + dir * 0.15f, yaw + a * 180f, 0.12f, 0.007f, 0.03f, 0.3f, 1, esp1, esp1, 0.2f, true, out b2);
            }
            Fol(g, raiz + Vector3.up * 0.02f, yaw + 40f, h * 0.5f, 0.05f, h * 0.22f, 0.5f, 2, cb, ct * 0.9f, 0f, true, out antes);
        }
        return g;
    }

    static CGb ProtoMilho(int seed)
    {
        System.Random r = new System.Random(seed); System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        CGb g = new CGb();
        Color verde0 = CC(0.22f, 0.4f, 0.12f), verde1 = CC(0.45f, 0.64f, 0.2f), amar = CC(0.95f, 0.78f, 0.22f);
        float h = F(1.7f, 2.0f);
        Fol(g, Vector3.zero, 0f, h, 0.045f, 0.05f, 0.02f, 4, CC(0.3f, 0.45f, 0.15f), CC(0.4f, 0.55f, 0.2f), 0.5f, true);
        for (int k = 0; k < 7; k++)
        {
            float y = 0.2f + k * h * 0.12f;
            Fol(g, new Vector3(0f, y, 0f), k * 137f + F(-20f, 20f), F(0.75f, 1.05f), 0.11f, 0.55f, 0.9f, 3, verde0, verde1, 0.1f, true);
        }
        Quaternion tilt = Quaternion.Euler(0f, 0f, 14f);
        Elip(g, new Vector3(0.07f, h * 0.6f, 0f), tilt, 0.05f, 0.13f, 5, 6, amar * 0.9f, amar, 0f, 0, 0.03f, seed);
        for (int k = 0; k < 3; k++) Fol(g, new Vector3(0.06f, h * 0.6f - 0.1f, 0f), 90f + k * 120f, 0.26f, 0.08f, 0.04f, 0.25f, 2, verde0, verde1, 0.4f, true);
        for (int k = 0; k < 5; k++) Fol(g, new Vector3(0f, h, 0f), k * 72f, 0.3f, 0.02f, 0.1f, 0.5f, 2, CC(0.7f, 0.6f, 0.3f), CC(0.85f, 0.78f, 0.45f), 0.2f, true);
        return g;
    }

    static CGb ProtoRepolho(int seed)
    {
        System.Random r = new System.Random(seed); System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        CGb g = new CGb();
        Elip(g, new Vector3(0f, 0.15f, 0f), Quaternion.identity, 0.19f, 0.16f, 7, 12, CC(0.5f, 0.72f, 0.32f), CC(0.72f, 0.86f, 0.52f), 0.03f, 6, 0.02f, seed);
        for (int k = 0; k < 8; k++)
        {
            float yaw = k * 45f + F(-10f, 10f);
            Fol(g, new Vector3(0f, 0.05f + (k % 2) * 0.04f, 0f), yaw, F(0.26f, 0.34f), 0.26f, F(-0.1f, 0.08f), 0.85f - (k % 2) * 0.35f, 3, CC(0.22f, 0.42f, 0.16f), CC(0.5f, 0.68f, 0.3f), 0.8f, false);
        }
        return g;
    }

    static CGb ProtoAbobora(int seed)
    {
        System.Random r = new System.Random(seed); System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        CGb g = new CGb();
        Color l0 = CC(0.78f, 0.32f, 0.06f), l1 = CC(0.96f, 0.52f, 0.12f);
        float R = F(0.24f, 0.32f);
        Elip(g, new Vector3(0f, R * 0.72f, 0f), Quaternion.identity, R, R * 0.78f, 8, 20, l0, l1, 0.09f, 10, 0f, seed);
        Rev(g, new Vector3(0f, R * 1.42f, 0f), Quaternion.Euler(F(-12f, 12f), 0f, F(-12f, 12f)), new[] { 0.045f, 0.03f, 0.03f, 0.04f }, new[] { 0f, 0.05f, 0.1f, 0.13f }, 6, CC(0.34f, 0.3f, 0.12f), CC(0.5f, 0.45f, 0.2f), 0f, 0f);
        for (int k = 0; k < 2; k++) Fol(g, new Vector3(0f, R * 0.5f, 0f), k * 180f + F(-30f, 30f), F(0.9f, 1.4f), 0.03f, F(-0.4f, 0.4f), 1.45f, 4, CC(0.2f, 0.35f, 0.1f), CC(0.3f, 0.48f, 0.15f), 0.7f, false);
        for (int k = 0; k < 2; k++) Fol(g, new Vector3(0f, 0.04f, 0f), k * 180f + 70f, 0.45f, 0.36f, 0.1f, 1.1f, 3, CC(0.18f, 0.38f, 0.1f), CC(0.36f, 0.56f, 0.2f), 0.6f, false);
        return g;
    }

    static CGb ProtoMaca(int seed)
    {
        CGb g = new CGb();
        const int N = 8; float R = 0.075f;
        float[] r = new float[N + 1], y = new float[N + 1];
        for (int k = 0; k <= N; k++)
        {
            float tt = Mathf.PI * k / N;
            r[k] = R * Mathf.Sin(tt) * (1f + 0.1f * Mathf.Cos(tt));
            y[k] = -R * Mathf.Cos(tt) - 0.2f * R * Mathf.Exp(-Mathf.Pow((Mathf.PI - tt) / 0.5f, 2f)) + 0.12f * R * Mathf.Exp(-Mathf.Pow(tt / 0.4f, 2f));
        }
        Rev(g, new Vector3(0f, R, 0f), Quaternion.identity, r, y, 10, CC(0.55f, 0.55f, 0.15f), CC(0.82f, 0.1f, 0.08f), 0f, 0f, 0f, 0, 0.02f, seed);
        Rev(g, new Vector3(0f, R * 1.7f, 0f), Quaternion.identity, new[] { 0.006f, 0.005f }, new[] { 0f, 0.03f }, 4, CC(0.3f, 0.2f, 0.1f), CC(0.3f, 0.2f, 0.1f), 0f, 0f);
        return g;
    }

    // ------------------------------------------------------------------ materiais
    static Material MatOrganico(string nome, float vento, float transl)
    {
        string cam = PASTA_COL + "/" + nome + ".mat";
        Shader sh = Shader.Find("Pinglu/Capim");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(cam);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, cam); }
        m.shader = sh; m.SetFloat("_UsarCorDoVertice", 1f); m.SetFloat("_ForcaManchas", 0f); m.SetFloat("_VentoForca", vento); m.SetFloat("_Translucidez", transl); m.SetFloat("_Brilho", 1.1f);
        m.enableInstancing = true; EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatSolo()
    {
        string cam = PASTA_COL + "/Terra_Arada.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(cam);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, cam); }
        string cor = "Assets/Cenario/Terreno/Texturas/Caminho_Cor.jpg", nor = "Assets/Cenario/Terreno/Texturas/Caminho_Normal.jpg";
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(cor)); m.SetColor("_BaseColor", new Color(0.62f, 0.5f, 0.42f));
        Texture2D tn = AssetDatabase.LoadAssetAtPath<Texture2D>(nor);
        if (tn != null) { m.SetTexture("_BumpMap", tn); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1.3f); }
        m.SetFloat("_Smoothness", 0.08f); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject ObjetoDeMalha(string nome, Transform pai, Mesh mesh, Material[] ms, bool colisor)
    {
        string cam = PASTA_COL + "/Malhas/" + nome + "_" + (++contadorMalhas).ToString("000") + ".asset";
        mesh.name = nome; AssetDatabase.CreateAsset(mesh, cam);
        GameObject g = new GameObject(nome); g.transform.SetParent(pai, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterials = ms;
        if (colisor) g.AddComponent<MeshCollider>().sharedMesh = mesh;
        g.isStatic = true;
        return g;
    }

    // ------------------------------------------------------------------ escolha dos lugares
    class Sitio { public string tipo; public Vector3 pos; public float raio, yaw; }
    static List<Sitio> sitios;
    static List<Vector3[]> linhasDeEstrada;
    static List<Bounds> casasCol;
    static Vector3 centroCol;
    static List<Vector3> evitar;

    static float DistAoSegmento(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 q = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), AB = new Vector2(b.x, b.z) - A;
        float t = AB.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(q - A, AB) / AB.sqrMagnitude);
        return (A + AB * t - q).magnitude;
    }

    static float DistEstrada(Vector3 p)
    {
        float m = 1e9f;
        foreach (Vector3[] l in linhasDeEstrada) for (int i = 0; i + 1 < l.Length; i++) m = Mathf.Min(m, DistAoSegmento(p, l[i], l[i + 1]));
        return m;
    }

    static float Declive(Vector3 p, float raio)
    {
        float max = 0f;
        for (int k = 0; k < 9; k++)
        {
            Vector3 q = k == 0 ? p : p + new Vector3(Mathf.Cos(k * 0.785f), 0f, Mathf.Sin(k * 0.785f)) * raio * 0.8f;
            Vector3 o = terreno.transform.position, sz = dados.size;
            max = Mathf.Max(max, dados.GetSteepness((q.x - o.x) / sz.x, (q.z - o.z) / sz.z));
        }
        return max;
    }

    static bool LugarBom(Vector3 p, float raio, float declMax, float estMin, float estMax)
    {
        Vector3 o = terreno.transform.position, sz = dados.size;
        if (p.x - raio < o.x + 20f || p.z - raio < o.z + 20f || p.x + raio > o.x + sz.x - 20f || p.z + raio > o.z + sz.z - 20f) return false;
        if (Declive(p, raio) > declMax) return false;
        float de = DistEstrada(p) - raio;
        if (de < estMin || de > estMax) return false;
        foreach (Bounds b in casasCol) if (DistAoRetangulo(p, b) < raio + 5f) return false;
        foreach (Vector3 q in evitar) if (Plano(p - q).magnitude < raio + 6f) return false;
        foreach (Sitio s in sitios) if (Plano(p - s.pos).magnitude < raio + s.raio + 4f) return false;
        return true;
    }

    static Sitio Achar(string tipo, float raio, float declMax, float dMin, float dMax, float angCentro, float angAmp, bool alto, int tent)
    {
        Sitio melhor = null; float nota = -1e9f;
        for (int i = 0; i < tent; i++)
        {
            float ang = (angCentro + Faixa(-angAmp, angAmp)) * Mathf.Deg2Rad, d = Faixa(dMin, dMax);
            Vector3 p = centroCol + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * d; p.y = Altura(p.x, p.z);
            if (!LugarBom(p, raio, declMax, 6f, 55f)) continue;
            float n = alto ? p.y : -DistEstrada(p);
            if (n > nota) { nota = n; melhor = new Sitio { tipo = tipo, pos = p, raio = raio, yaw = Faixa(0f, 360f) }; }
            if (!alto && melhor != null && i > tent / 3) break;
        }
        if (melhor != null) sitios.Add(melhor);
        return melhor;
    }

    // ------------------------------------------------------------------ menu
    [MenuItem("Pinglu/60. Ampliar o vilarejo (colheita e arredores)")]
    public static void ColheitaMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        Colheita(true);
    }

    static void Colheita(bool interativo)
    {
        terreno = Terrain.activeTerrain;
        GameObject vila = GameObject.Find(NOME_RAIZ);
        if (terreno == null || vila == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain ou a vila (Vila_Gerada).", "OK"); return; }
        if (interativo && !EditorUtility.DisplayDialog("Pinglu - Ampliar o vilarejo",
            "Vou criar em volta da vila: campos de trigo, hortas (milho, repolho, abóbora), um pomar de macieiras, um moinho de vento, colmeias e fardos de feno.\n\nPode demorar um pouco. Depois rode o Pinglu > 54 e salve (Ctrl + S).", "Pode fazer", "Cancelar")) return;
        dados = terreno.terrainData;
        avisos.Clear(); formas.Clear(); rng = new System.Random(4242);
        raiz = vila.transform; contadorMalhas = 200;
        pastaMalhas = PASTA_MALHAS; prefixoOculto = PREFIXO_OCULTO;
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando...", 0.05f);
            ConfigurarTexturas(); CriarMateriais();
            Transform velha = vila.transform.Find(NOME_COLHEITA); if (velha != null) Object.DestroyImmediate(velha.gameObject);
            if (AssetDatabase.IsValidFolder(PASTA_COL + "/Malhas")) AssetDatabase.DeleteAsset(PASTA_COL + "/Malhas");
            AssetDatabase.CreateFolder(PASTA_COL, "Malhas");
            Transform pasta = new GameObject(NOME_COLHEITA).transform; pasta.SetParent(vila.transform, false);
            Undo.RegisterCreatedObjectUndo(pasta.gameObject, "Colheita e arredores");

            // dados do mundo
            centroCol = vila.transform.position; MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
            if (mm != null) foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Vila) centroCol = k.posicao;
            Transform poco = vila.transform.Find("Praca/Poco"); if (poco != null) centroCol = poco.position;
            linhasDeEstrada = new List<Vector3[]>();
            RedeDeEstradas rede = Object.FindFirstObjectByType<RedeDeEstradas>();
            if (rede != null) foreach (RedeDeEstradas.Estrada e in rede.estradas) if (e != null && e.pontos != null && e.pontos.Length > 1) linhasDeEstrada.Add(e.pontos);
            if (linhasDeEstrada.Count == 0) throw new System.Exception("Não achei as estradas (rode o Pinglu > 39).");
            casasCol = new List<Bounds>();
            foreach (string nomePasta in new[] { "Construcoes", NOME_CASAS_NOVAS })
            {
                Transform t = vila.transform.Find(nomePasta); if (t == null) continue;
                foreach (Transform c in t)
                {
                    bool ok = false; Bounds b = new Bounds(c.position, Vector3.zero);
                    foreach (Renderer r in c.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds); }
                    if (ok && b.size.y > 2f) casasCol.Add(b);
                }
            }
            evitar = new List<Vector3>();
            Transform est = vila.transform.Find("Estrada");
            if (est != null) foreach (Transform t in est) if (t.name == NOME_PORTAL || t.name == NOME_PLACA) evitar.Add(t.position);
            sitios = new List<Sitio>();

            // escolhe os lugares: espalhados em volta da vila, perto das estradas
            EditorUtility.DisplayProgressBar("Pinglu", "Escolhendo os lugares...", 0.12f);
            float a0 = Faixa(0f, 360f);
            Sitio moinho = Achar("moinho", 7f, 9f, 75f, 150f, a0, 180f, true, 900);
            for (int k = 0; k < 3; k++) Achar("trigo", Faixa(14f, 18f), 8f, 62f, 150f, a0 + 60f + k * 120f, 55f, false, 500);
            for (int k = 0; k < 2; k++) Achar("horta", Faixa(10f, 12.5f), 8f, 62f, 140f, a0 + 120f + k * 180f, 80f, false, 400);
            Achar("pomar", 16f, 8f, 65f, 145f, a0 + 200f, 160f, false, 500);
            for (int k = 0; k < 2; k++) Achar("colmeias", 4f, 8f, 60f, 140f, a0 + k * 180f, 180f, false, 300);
            foreach (Sitio s in sitios) if (s.tipo == "trigo" || s.tipo == "horta" || s.tipo == "pomar") TirarArvoresPerto(s.pos, s.raio * 1.12f); else TirarArvoresPerto(s.pos, s.raio + 2f);

            // modelos de plantas (cada um com 2 a 3 variações)
            EditorUtility.DisplayProgressBar("Pinglu", "Modelando as plantas...", 0.2f);
            CGb[] trigo = { ProtoTrigo(1), ProtoTrigo(2), ProtoTrigo(3) }, milho = { ProtoMilho(4), ProtoMilho(5) }, repolho = { ProtoRepolho(6), ProtoRepolho(7) }, abobora = { ProtoAbobora(8), ProtoAbobora(9), ProtoAbobora(10) }, maca = { ProtoMaca(11), ProtoMaca(12) };
            Material mTrigo = MatOrganico("Org_Trigo", 0.12f, 0.45f), mHorta = MatOrganico("Org_Horta", 0.05f, 0.3f), mRigido = MatOrganico("Org_Rigido", 0f, 0.1f), mSolo = MatSolo();

            int campo = 0, horta = 0, total = 0;
            foreach (Sitio s in sitios)
            {
                EditorUtility.DisplayProgressBar("Pinglu", "Construindo: " + s.tipo + "...", 0.25f + 0.6f * (++total / (float)Mathf.Max(1, sitios.Count)));
                if (s.tipo == "trigo") { CampoDeTrigo(s, pasta, trigo, mTrigo, mSolo, mRigido, ++campo); }
                else if (s.tipo == "horta") { Horta2(s, pasta, milho, repolho, abobora, mHorta, mSolo, ++horta); }
                else if (s.tipo == "pomar") Pomar(s, pasta, maca, mRigido, mSolo);
                else if (s.tipo == "colmeias") Colmeias(s, pasta);
                else if (s.tipo == "moinho") Moinho(s, pasta);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(vila.scene);
            avisos.Add(campo + " campos de trigo, " + horta + " hortas, " + (sitios.Exists(x => x.tipo == "pomar") ? "1 pomar" : "sem pomar (não coube)") + ", " + (moinho != null ? "1 moinho" : "sem moinho (não coube)") + ", " + sitios.FindAll(x => x.tipo == "colmeias").Count + " grupos de colmeias.");
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console)."); }
        finally { EditorUtility.ClearProgressBar(); }
        foreach (string a in avisos) Debug.Log("[Colheita] " + a);
        if (interativo)
        {
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog("Pinglu", "Vilarejo ampliado e cena salva!\n\n- " + string.Join("\n- ", avisos) + "\n\nAgora rode o Pinglu > 54 (o capim sai de baixo dos campos).", "OK");
        }
    }

    [MenuItem("Pinglu/61. Remover a colheita e os arredores")]
    public static void RemoverColheitaMenu()
    {
        GameObject vila = GameObject.Find(NOME_RAIZ); if (vila == null) return;
        Transform t = vila.transform.Find(NOME_COLHEITA); if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        EditorSceneManager.MarkSceneDirty(vila.scene);
        EditorUtility.DisplayDialog("Pinglu", "Colheita e arredores removidos. Rode o Pinglu > 54 e salve com Ctrl + S.", "OK");
    }

    // ------------------------------------------------------------------ terra arada (malha que acompanha o terreno, com sulcos)
    static void TerraArada(Sitio s, Transform pai, Material mSolo, float raio, float sulco, string nome, float sulcoAmp)
    {
        // malha POLAR: borda lisa e redonda (sem degraus), com sulcos; a beirada afunda no terreno para a terra "nascer" do chão
        int seg = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 2f * raio * 1.2f / 0.3f), 120, 480), aneis = Mathf.CeilToInt(raio * 1.2f / 0.35f);
        CGb g = new CGb(); Quaternion q = Quaternion.Euler(0f, s.yaw, 0f);
        float semente = s.pos.x * 0.37f + s.pos.z * 0.11f;
        int centro = -1;
        int[,] idx = new int[aneis + 1, seg];
        for (int a = 0; a <= aneis; a++)
        {
            float t = a / (float)aneis;
            for (int k = 0; k < seg; k++)
            {
                if (a == 0 && centro >= 0) { idx[a, k] = centro; continue; }
                float ang = k * Mathf.PI * 2f / seg;
                float lim = LimiteDoCampo(raio, ang, semente);
                float rr = lim * t;
                float lx = Mathf.Cos(ang) * rr, lz = Mathf.Sin(ang) * rr;
                Vector3 w = s.pos + q * new Vector3(lx, 0f, lz);
                float borda = Mathf.Clamp01((lim - rr) / 1.2f);
                float cume = sulcoAmp * (0.5f + 0.5f * Mathf.Cos(lx / sulco * Mathf.PI * 2f)) * borda;
                w.y = Altura(w.x, w.z) + 0.035f + cume - (a == aneis ? 0.06f : 0f);
                idx[a, k] = g.Add(w, Vector3.up, 0f, Color.white);
                g.uv[g.uv.Count - 1] = new Vector2(w.x / 3.2f, w.z / 3.2f);
                if (a == 0) centro = idx[a, k];
            }
        }
        for (int a = 0; a < aneis; a++)
            for (int k = 0; k < seg; k++)
            {
                int k2 = (k + 1) % seg;
                int p00 = idx[a, k], p01 = idx[a, k2], p10 = idx[a + 1, k], p11 = idx[a + 1, k2];
                if (a == 0) g.t.AddRange(new[] { p00, p10, p11 });
                else g.t.AddRange(new[] { p00, p10, p11, p00, p11, p01 });
            }
        Mesh m = g.Gerar(nome); m.RecalculateNormals(); m.RecalculateTangents();
        GameObject go = ObjetoDeMalha(nome, pai, m, new[] { mSolo }, false);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static float LimiteDoCampo(float raio, float ang, float semente)
    {
        return raio * (1f + 0.16f * (Mathf.PerlinNoise(Mathf.Cos(ang) * 1.3f + 5f + semente, Mathf.Sin(ang) * 1.3f + 5f) - 0.5f) * 2f);
    }

    // cerca fechada em volta do campo, com uma abertura (entrada) de ~30 graus e dois mourões grossos marcando a porteira
    static void CercaEmVolta(Malha cerca, Sitio s, float a0)
    {
        float semente = s.pos.x * 0.37f + s.pos.z * 0.11f; Quaternion q = Quaternion.Euler(0f, s.yaw, 0f);
        const float passo = 12f, abertura = 30f;
        Vector3 anterior = Vector3.zero; bool tem = false;
        for (float a = abertura; a <= 360f + 0.1f; a += passo)
        {
            float ang = (a0 + a) * Mathf.Deg2Rad; float angN = ang < 0f ? ang + 62.83f : ang; angN = Mathf.Repeat(angN, Mathf.PI * 2f);
            float r = LimiteDoCampo(s.raio, angN, semente) + 1.6f;
            Vector3 p = s.pos + q * new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r; p.y = Altura(p.x, p.z);
            if (tem) CercaReta(cerca, anterior, p, 1.5f);
            anterior = p; tem = true;
        }
        foreach (float a in new[] { 0f, abertura })      // mourões da porteira
        {
            float ang = (a0 + a) * Mathf.Deg2Rad, angN = Mathf.Repeat(ang, Mathf.PI * 2f);
            float r = LimiteDoCampo(s.raio, angN, semente) + 1.6f;
            Vector3 p = s.pos + q * new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r; p.y = Altura(p.x, p.z);
            cerca.Cilindro(p, 0.1f, 1.5f, 8, MAD_ESC, Quaternion.identity);
        }
    }

    static bool DentroDoCampo(Sitio s, Vector3 w, float fator)
    {
        Vector3 l = Quaternion.Inverse(Quaternion.Euler(0f, s.yaw, 0f)) * (w - s.pos);
        float ang = Mathf.Atan2(l.z, l.x), rr = Mathf.Sqrt(l.x * l.x + l.z * l.z), semente = s.pos.x * 0.37f + s.pos.z * 0.11f;
        if (ang < 0f) ang += Mathf.PI * 2f;
        float lim = LimiteDoCampo(s.raio, ang, semente);
        return rr < lim * fator;
    }

    // ------------------------------------------------------------------ campo de trigo
    static void CampoDeTrigo(Sitio s, Transform pasta, CGb[] trigo, Material mTrigo, Material mSolo, Material mRigido, int num)
    {
        Transform pai = new GameObject("Campo de Trigo " + num).transform; pai.SetParent(pasta, false);
        TerraArada(s, pai, mSolo, s.raio, 0.8f, "Terra do trigo", 0.04f);
        CGb g = new CGb(); Quaternion q = Quaternion.Euler(0f, s.yaw, 0f);
        float sp = 0.9f; int n = Mathf.CeilToInt(s.raio / sp);
        for (int i = -n; i <= n; i++)
            for (int j = -n; j <= n; j++)
            {
                Vector3 w = s.pos + q * new Vector3(i * sp + Faixa(-0.3f, 0.3f), 0f, j * sp + Faixa(-0.3f, 0.3f));
                if (!DentroDoCampo(s, w, 0.93f)) continue;
                if (Plano(w - s.pos).magnitude < 1.4f) continue;               // espaço do espantalho
                w.y = Altura(w.x, w.z) + 0.03f;
                float manchas = 0.88f + 0.2f * Mathf.PerlinNoise(w.x * 0.12f + 3f, w.z * 0.12f + 9f);
                g.Anexar(trigo[rng.Next(trigo.Length)], w, Quaternion.Euler(0f, Faixa(0f, 360f), 0f), Faixa(0.85f, 1.15f), manchas);
            }
        ObjetoDeMalha("Trigo", pai, g.Gerar("Trigo"), new[] { mTrigo }, false);
        avisos.Add("Campo de trigo " + num + ": " + g.v.Count + " vértices.");

        // espantalho, cerca em arco, fardos redondos e palheiro na beira
        Malha m = new Malha(); Espantalho(m);
        Instanciar(m, "Espantalho", NoChao(s.pos), Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, true);
        Malha cerca = new Malha(); float a0 = Faixa(0f, 360f); CercaEmVolta(cerca, s, a0);
        Instanciar(cerca, "Cerca do campo", Vector3.zero, Quaternion.identity, pai, false);
        float ab = (a0 + 200f) * Mathf.Deg2Rad;
        Vector3 pf = s.pos + q * new Vector3(Mathf.Cos(ab), 0f, Mathf.Sin(ab)) * (s.raio * 1.28f);
        Malha fardos = new Malha(); Fardos(fardos);
        Instanciar(fardos, "Fardos de feno", NoChao(pf), Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, true);
        Vector3 ph = s.pos + q * new Vector3(Mathf.Cos(ab + 0.7f), 0f, Mathf.Sin(ab + 0.7f)) * (s.raio * 1.32f);
        Malha palheiro = new Malha(); Palheiro(palheiro);
        Instanciar(palheiro, "Palheiro", NoChao(ph), Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, true);
    }

    static void Espantalho(Malha m)
    {
        m.Cilindro(new Vector3(0f, -0.1f, 0f), 0.05f, 2.35f, 10, MAD_ESC, Quaternion.identity);
        m.Cilindro(new Vector3(-0.85f, 1.55f, 0f), 0.04f, 1.7f, 8, MAD_ESC, Quaternion.Euler(0f, 0f, -90f));
        m.Torno(new Vector3(0f, 0.85f, 0f), new[] { 0.14f, 0.2f, 0.23f, 0.2f, 0.12f }, new[] { 0f, 0.2f, 0.5f, 0.75f, 0.95f }, 12, TEC_OCRE, Quaternion.identity);      // corpo de pano
        m.Torno(new Vector3(0f, 1.78f, 0f), new[] { 0.03f, 0.14f, 0.17f, 0.14f, 0.03f }, new[] { 0f, 0.07f, 0.2f, 0.34f, 0.4f }, 12, PALHA, Quaternion.identity);               // cabeça de palha
        m.Torno(new Vector3(0f, 2.05f, 0f), new[] { 0.34f, 0.3f, 0.17f, 0.15f, 0.01f }, new[] { 0f, 0.03f, 0.05f, 0.2f, 0.3f }, 14, COURO, Quaternion.identity);                  // chapéu
        for (int s = -1; s <= 1; s += 2)
        {
            m.Torno(new Vector3(s * 0.78f, 1.5f, 0f), new[] { 0.02f, 0.1f, 0.12f, 0.06f }, new[] { 0f, 0.1f, 0.25f, 0.35f }, 8, PALHA, Quaternion.Euler(0f, 0f, -s * 90f));      // mãos de palha
            m.Torno(new Vector3(s * 0.2f, 0.86f, 0f), new[] { 0.01f, 0.08f, 0.1f, 0.05f }, new[] { 0f, 0.1f, 0.22f, 0.3f }, 8, PALHA, Quaternion.Euler(0f, 0f, 180f));            // palha saindo da barra
        }
    }

    static void Fardos(Malha m)
    {
        Vector3[] pos = { new Vector3(-0.7f, 0.62f, 0f), new Vector3(0.7f, 0.62f, 0.1f), new Vector3(0f, 1.6f, 0.05f) };
        float[] gir = { 0f, 10f, 80f };
        for (int i = 0; i < 3; i++)
        {
            Quaternion r = Quaternion.Euler(0f, gir[i], 90f);
            Vector3 eixo = Quaternion.Euler(0f, gir[i], 0f) * Vector3.left;
            Vector3 b = pos[i] - eixo * 0.6f;
            m.Cilindro(b, 0.62f, 1.2f, 20, PALHA, r);
            foreach (float f in new[] { 0.3f, 0.9f }) m.Cilindro(b + eixo * f, 0.635f, 0.04f, 20, COURO, r);      // cordas
        }
    }

    static void Palheiro(Malha m)
    {
        m.Torno(new Vector3(0f, 0f, 0f), new[] { 1.5f, 1.58f, 1.5f, 1.2f, 0.85f, 0.5f, 0.2f, 0.02f }, new[] { 0f, 0.35f, 0.9f, 1.6f, 2.2f, 2.75f, 3.15f, 3.45f }, 20, PALHA, Quaternion.identity);
        m.Cilindro(new Vector3(0f, 3.3f, 0f), 0.04f, 0.8f, 6, MAD_ESC, Quaternion.identity);
        m.Torno(new Vector3(0f, 0.9f, 0f), new[] { 1.52f, 1.52f }, new[] { 0f, 0.05f }, 20, COURO, Quaternion.identity, false);
    }

    // ------------------------------------------------------------------ hortas
    static void Horta2(Sitio s, Transform pasta, CGb[] milho, CGb[] repolho, CGb[] abobora, Material mHorta, Material mSolo, int num)
    {
        Transform pai = new GameObject("Horta " + num).transform; pai.SetParent(pasta, false);
        TerraArada(s, pai, mSolo, s.raio, 0.9f, "Terra da horta", 0.1f);
        CGb g = new CGb(); Quaternion q = Quaternion.Euler(0f, s.yaw, 0f);
        float espFileira = 0.9f; int fileiras = Mathf.FloorToInt(s.raio * 2f / espFileira);
        for (int f = 0; f < fileiras; f++)
        {
            float lx = -s.raio + (f + 0.5f) * espFileira;
            int tipo = num == 1 ? (f < fileiras / 2 ? 0 : 1) : (f % 3 == 2 ? 1 : 2);        // 0 milho, 1 repolho, 2 abóbora
            float passo = tipo == 0 ? 0.55f : (tipo == 1 ? 0.55f : 1.25f);
            for (float lz = -s.raio; lz <= s.raio; lz += passo)
            {
                Vector3 w = s.pos + q * new Vector3(lx + Faixa(-0.04f, 0.04f), 0f, lz + Faixa(-0.08f, 0.08f));
                if (!DentroDoCampo(s, w, 0.9f) || Plano(w - s.pos).magnitude < 1.3f) continue;
                w.y = Altura(w.x, w.z) + 0.1f;
                CGb[] lista = tipo == 0 ? milho : (tipo == 1 ? repolho : abobora);
                g.Anexar(lista[rng.Next(lista.Length)], w, Quaternion.Euler(0f, Faixa(0f, 360f), 0f), Faixa(0.88f, 1.12f), Faixa(0.9f, 1.08f));
            }
        }
        ObjetoDeMalha("Plantação", pai, g.Gerar("Plantação"), new[] { mHorta }, false);
        Malha m = new Malha(); Espantalho(m);
        Instanciar(m, "Espantalho", NoChao(s.pos), Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, true);
        Malha cerca = new Malha(); float a0 = Faixa(0f, 360f); CercaEmVolta(cerca, s, a0);
        Instanciar(cerca, "Cerca da horta", Vector3.zero, Quaternion.identity, pai, false);
        // barris de água e cesto de colheita na entrada
        Malha b = new Malha(); Barril(b);
        Vector3 pb = s.pos + q * new Vector3(Mathf.Cos((a0 + 220f) * Mathf.Deg2Rad), 0f, Mathf.Sin((a0 + 220f) * Mathf.Deg2Rad)) * (s.raio * 1.2f);
        Instanciar(b, "Barris", NoChao(pb), Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, true);
    }

    // ------------------------------------------------------------------ pomar de macieiras
    static void Pomar(Sitio s, Transform pasta, CGb[] maca, Material mRigido, Material mSolo)
    {
        Transform pai = new GameObject("Pomar").transform; pai.SetParent(pasta, false);
        CGb g = new CGb(); Quaternion q = Quaternion.Euler(0f, s.yaw, 0f);
        float esp = 6.8f; int n = Mathf.CeilToInt(s.raio / esp), arvores = 0;
        List<Vector3> troncos = new List<Vector3>();
        for (int i = -n; i <= n; i++)
            for (int j = -n; j <= n; j++)
            {
                Vector3 l = new Vector3(i * esp + (j % 2) * esp * 0.5f + Faixa(-0.6f, 0.6f), 0f, j * esp * 0.87f + Faixa(-0.6f, 0.6f));
                if (l.magnitude > s.raio) continue;
                Vector3 w = s.pos + q * l; w.y = Altura(w.x, w.z);
                Macieira(g, w, maca);
                troncos.Add(w); arvores++;
            }
        ObjetoDeMalha("Macieiras", pai, g.Gerar("Macieiras"), new[] { mRigido }, false);
        foreach (Vector3 w in troncos)
        {
            GameObject c = new GameObject("Tronco"); c.transform.SetParent(pai, false); c.transform.position = w + Vector3.up * 1.1f;
            CapsuleCollider cap = c.AddComponent<CapsuleCollider>(); cap.radius = 0.28f; cap.height = 2.2f;
        }
        // cestos de maçãs e barril
        for (int k = 0; k < 3; k++)
        {
            Malha m = new Malha(); CestoDeMacas(m);
            float ang = Faixa(0f, 360f) * Mathf.Deg2Rad; Vector3 p = s.pos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Faixa(2f, s.raio * 0.8f);
            Instanciar(m, "Cesto de maçãs", NoChao(p), Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, false);
        }
        avisos.Add("Pomar: " + arvores + " macieiras, " + g.v.Count + " vértices.");
    }

    static void Macieira(CGb g, Vector3 p, CGb[] maca)
    {
        float esc = Faixa(0.9f, 1.15f), yaw = Faixa(0f, 360f);
        Quaternion inc = Quaternion.Euler(Faixa(-5f, 5f), yaw, Faixa(-5f, 5f));
        // tronco torto que se abre em galhos
        Rev(g, p, inc, new[] { 0.3f * esc, 0.24f * esc, 0.2f * esc, 0.17f * esc, 0.13f * esc }, new[] { 0f, 0.4f * esc, 1.0f * esc, 1.7f * esc, 2.3f * esc }, 9, CC(0.24f, 0.17f, 0.11f), CC(0.36f, 0.27f, 0.18f), 0f, 0f, 0.05f, 3);
        for (int k = 0; k < 4; k++)
        {
            float a = yaw + k * 90f + Faixa(-20f, 20f);
            Quaternion rb = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(Faixa(38f, 55f), 0f, 0f);
            Rev(g, p + Vector3.up * (1.8f * esc), rb, new[] { 0.09f * esc, 0.06f * esc, 0.035f * esc }, new[] { 0f, 0.8f * esc, 1.5f * esc }, 6, CC(0.26f, 0.19f, 0.12f), CC(0.33f, 0.25f, 0.16f), 0f, 0f);
        }
        // copa: bolas achatadas de folhas, de verdes variados
        Vector3 centro = p + Vector3.up * (3.1f * esc);
        List<Vector3> blobs = new List<Vector3>(); List<float> raios = new List<float>();
        int nb = 9;
        for (int k = 0; k < nb; k++)
        {
            float a = k * 2.4f + Faixa(0f, 1f), d = k == 0 ? 0f : Faixa(0.7f, 1.5f) * esc, hh = Faixa(-0.45f, 0.65f) * esc;
            Vector3 c = centro + new Vector3(Mathf.Cos(a) * d, hh + (k == 0 ? 0.5f : 0f), Mathf.Sin(a) * d);
            float rx = Faixa(1.0f, 1.45f) * esc, ry = rx * Faixa(0.68f, 0.85f);
            float tom = Faixa(0f, 1f);
            Elip(g, c, Quaternion.Euler(0f, Faixa(0f, 360f), 0f), rx, ry, 6, 9, Color.Lerp(CC(0.12f, 0.3f, 0.09f), CC(0.2f, 0.4f, 0.12f), tom), Color.Lerp(CC(0.3f, 0.5f, 0.16f), CC(0.5f, 0.68f, 0.24f), tom), 0f, 0, 0.12f, k + rng.Next(50));
            blobs.Add(c); raios.Add(rx);
        }
        // maçãs penduradas na parte de baixo e de fora da copa
        int nm = Mathf.RoundToInt(Faixa(14f, 22f));
        for (int k = 0; k < nm; k++)
        {
            int b = rng.Next(blobs.Count); float a = Faixa(0f, 6.28f), dd = raios[b] * 0.88f;
            Vector3 pm = blobs[b] + new Vector3(Mathf.Cos(a) * dd, -raios[b] * Faixa(0.3f, 0.62f), Mathf.Sin(a) * dd);
            g.Anexar(maca[rng.Next(maca.Length)], pm - Vector3.up * 0.075f, Quaternion.Euler(0f, Faixa(0f, 360f), 0f), Faixa(0.9f, 1.2f), 1f);
        }
        // algumas caídas no chão
        for (int k = 0; k < 5; k++)
        {
            float a = Faixa(0f, 6.28f), dd = Faixa(0.5f, 1.8f);
            Vector3 pm = p + new Vector3(Mathf.Cos(a) * dd, 0f, Mathf.Sin(a) * dd); pm.y = Altura(pm.x, pm.z);
            g.Anexar(maca[rng.Next(maca.Length)], pm - Vector3.up * 0.015f, Quaternion.Euler(Faixa(-30f, 30f), Faixa(0f, 360f), Faixa(-30f, 30f)), Faixa(0.9f, 1.1f), 0.95f);
        }
    }

    static void CestoDeMacas(Malha m)
    {
        // cesto de vime redondo (feito de palha), com alça de couro
        m.Torno(Vector3.zero, new[] { 0.22f, 0.3f, 0.36f, 0.4f }, new[] { 0f, 0.1f, 0.25f, 0.4f }, 14, PALHA, Quaternion.identity, true);
        m.Torno(new Vector3(0f, 0.38f, 0f), new[] { 0.41f, 0.41f }, new[] { 0f, 0.05f }, 14, MAD_ESC, Quaternion.identity, false);
        m.Torno(new Vector3(0f, 0.4f, 0f), new[] { 0.37f, 0.3f, 0.12f }, new[] { 0f, 0.12f, 0.2f }, 12, TEC_VERM, Quaternion.identity);       // maçãs amontoadas (cor vermelha)
        m.Cilindro(new Vector3(-0.4f, 0.4f, 0f), 0.02f, 0.5f, 6, COURO, Quaternion.Euler(0f, 0f, -75f));
        m.Cilindro(new Vector3(0.4f, 0.4f, 0f), 0.02f, 0.5f, 6, COURO, Quaternion.Euler(0f, 0f, 75f));
    }

    // ------------------------------------------------------------------ colmeias de palha
    static void Colmeias(Sitio s, Transform pasta)
    {
        Transform pai = new GameObject("Colmeias").transform; pai.SetParent(pasta, false);
        Malha m = new Malha();
        for (int k = 0; k < 4; k++)
        {
            float ang = k * 90f * Mathf.Deg2Rad + 0.4f; Vector3 c = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.5f;
            m.Torno(c + Vector3.up * 0.0f, new[] { 0.22f, 0.24f, 0.2f }, new[] { 0f, 0.05f, 0.08f }, 10, MAD_ESC, Quaternion.identity);                                // banquinho redondo
            for (int l = 0; l < 3; l++) m.Cilindro(c + new Vector3(Mathf.Cos(l * 2.1f), 0f, Mathf.Sin(l * 2.1f)) * 0.14f, 0.03f, 0.48f, 6, MAD_ESC, Quaternion.identity);
            m.Torno(c + Vector3.up * 0.5f, new[] { 0.3f, 0.36f, 0.34f, 0.28f, 0.2f, 0.11f, 0.03f }, new[] { 0f, 0.08f, 0.25f, 0.42f, 0.57f, 0.68f, 0.74f }, 14, PALHA, Quaternion.identity);   // cesto de palha
            foreach (float y in new[] { 0.18f, 0.36f, 0.52f }) m.Torno(c + Vector3.up * (0.5f + y), new[] { 0.31f, 0.31f }, new[] { 0f, 0.025f }, 14, COURO, Quaternion.identity, false);
        }
        Instanciar(m, "Colmeias de palha", NoChao(s.pos), Quaternion.Euler(0f, s.yaw, 0f), pai, true);
    }

    // ------------------------------------------------------------------ moinho de vento
    static void Moinho(Sitio s, Transform pasta)
    {
        Transform pai = new GameObject("Moinho de Vento").transform; pai.SetParent(pasta, false);
        Vector3 paraVila = Plano(centroCol - s.pos).normalized;
        Quaternion rot = Quaternion.LookRotation(paraVila);
        Malha m = new Malha();
        // torre redonda de pedra, afunilando
        m.Torno(new Vector3(0f, -1.2f, 0f), new[] { 3.6f, 3.5f, 3.3f, 2.7f, 2.2f, 1.9f }, new[] { 0f, 1.2f, 1.6f, 4.8f, 7.4f, 9.2f }, 22, PEDRA_BLOCOS, Quaternion.identity);
        m.Torno(new Vector3(0f, 0f, 0f), new[] { 3.5f, 3.62f, 3.62f, 3.45f }, new[] { 0f, 0.1f, 0.4f, 0.5f }, 22, PEDRA_RUSTICA, Quaternion.identity, false);      // soleira
        m.Torno(new Vector3(0f, 4.6f, 0f), new[] { 2.78f, 2.85f, 2.85f, 2.76f }, new[] { 0f, 0.06f, 0.2f, 0.26f }, 22, MAD_ESC, Quaternion.identity, false);        // cinta de madeira
        // porta e janelas
        m.Painel(new Vector3(0f, 1.15f, 3.36f), Vector3.forward, 1.5f, 2.3f, P_PORTA_ARCO);
        foreach (float a in new[] { 70f, -70f, 150f, -150f })
        {
            Quaternion r = Quaternion.Euler(0f, a, 0f); Vector3 nrm = r * Vector3.forward;
            m.Painel(r * new Vector3(0f, 5.2f, 2.5f) + nrm * 0.05f, nrm, 0.7f, 1.1f, P_JANELA_ARCO);
        }
        // capuz: telhado cônico de palha com beiral
        m.Torno(new Vector3(0f, 9.1f, 0f), new[] { 2.45f, 2.6f, 2.3f, 1.7f, 1.0f, 0.4f, 0.02f }, new[] { 0f, 0.1f, 0.5f, 1.3f, 2.0f, 2.5f, 2.8f }, 22, PALHA, Quaternion.identity);
        m.Torno(new Vector3(0f, 9.0f, 0f), new[] { 2.2f, 2.2f }, new[] { 0f, 0.22f }, 22, MAD_ESC, Quaternion.identity, false);
        // eixo das pás saindo pela frente do capuz
        m.Cilindro(new Vector3(0f, 10.2f, 1.5f), 0.22f, 1.5f, 10, MAD_ESC, Quaternion.Euler(90f, 0f, 0f));
        m.Torno(new Vector3(0f, 10.2f, 3.0f), new[] { 0.03f, 0.3f, 0.34f, 0.22f }, new[] { 0f, 0.12f, 0.3f, 0.42f }, 12, FERRO, Quaternion.Euler(90f, 0f, 0f));
        // cai um pouco dentro do chão para não ter vão em terreno inclinado
        GameObject torre = Instanciar(m, "Torre do moinho", NoChao(s.pos), rot, pai, true);

        // pás (objeto separado: gira)
        Malha p = new Malha();
        for (int k = 0; k < 4; k++)
        {
            Quaternion r = Quaternion.Euler(0f, 0f, k * 90f + 20f);
            p.Caixa(r * new Vector3(0f, 4.4f, 0f), new Vector3(0.22f, 8.8f, 0.14f), r, MAD_ESC);                    // braço longo
            for (int d = 0; d < 6; d++) p.Caixa(r * new Vector3(0f, 1.4f + d * 1.2f, 0f), new Vector3(1.45f, 0.06f, 0.1f), r, MAD_VERM);   // travessas
            p.Caixa(r * new Vector3(0.72f, 5.2f, -0.02f), new Vector3(1.3f, 6.2f, 0.03f), r, TEC_OCRE);              // vela de pano
            p.Caixa(r * new Vector3(0.72f, 5.2f, -0.02f), new Vector3(0.05f, 6.2f, 0.07f), r, MAD_ESC);
        }
        p.Torno(new Vector3(0f, 0f, -0.25f), new[] { 0.34f, 0.4f, 0.3f }, new[] { 0f, 0.3f, 0.5f }, 12, FERRO, Quaternion.Euler(90f, 0f, 0f));
        Vector3 cubo = torre.transform.TransformPoint(new Vector3(0f, 10.2f, 3.4f));
        GameObject pas = Instanciar(p, "Pás do moinho", cubo, rot, pai, false);
        pas.AddComponent<MoinhoGira>();
        pas.isStatic = false;
        // lamparina na porta
        avisos.Add("Moinho a " + Plano(s.pos - centroCol).magnitude.ToString("0") + " m do centro.");
    }
}
#endif
