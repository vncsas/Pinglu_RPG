using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Terreno de teste do ambiente: relevo, camadas de textura por bioma, grama com variacao de cor, props espalhados em grupos
// (grande / medio / pequeno), agua no pantano e neblina. Menu: Ambiente > Montar tudo (ou -executeMethod MontarAmbiente.Montar)
public static partial class MontarAmbiente
{
    const string Raiz = "Assets/Ambiente";
    const string Cena = Raiz + "/Teste_Terreno.unity";
    const int Tam = 240;          // metros
    const float AltT = 45f;      // altura maxima do terreno (m)
    const int Res = 513;
    public const float NivelAgua = 8.8f;
    static System.Random rnd;

    [MenuItem("Ambiente/Montar tudo")]
    public static void Montar()
    {
        rnd = new System.Random(7);
        ConfigurarURP();
        var mats = Materiais();
        var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Luz();
        var terreno = CriarTerreno(mats);
        GramaInstanciada(terreno, mats);
        if (System.Environment.GetEnvironmentVariable("AMBIENTE_SEM_PROPS") == null) Props(terreno);
        Agua(terreno, mats);
        if (UsarKit) Kit(terreno); else Arvores(terreno);
        Vila(terreno);
        Atmosfera();
        Bordas();
        Jogador(terreno);
        Cameras();
        EditorSceneManager.SaveScene(cena, Cena);
        Debug.Log("[Ambiente] PRONTO: " + Cena);
    }

    // ---------------------------------------------------------------- URP: depth texture (agua)
    static void ConfigurarURP()
    {
        var rp = GraphicsSettings.defaultRenderPipeline;
        if (rp == null) return;
        var so = new SerializedObject(rp);
        var p = so.FindProperty("m_RequireDepthTexture"); if (p != null) { p.boolValue = true; so.ApplyModifiedProperties(); }
        var s = so.FindProperty("m_SupportsSoftShadows");
    }

    // ---------------------------------------------------------------- materiais e camadas
    class Mats { public Material grama, agua; public TerrainLayer[] camadas; public Dictionary<string, Material> props = new(); }

    static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    static TerrainLayer Camada(string nome, string pasta, float tile)
    {
        var path = $"{Raiz}/Camadas/{nome}.terrainlayer";
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (!l) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
        string Achar(string sufixo) => Directory.GetFiles($"{Raiz}/Texturas/{pasta}").Select(f => f.Replace('\\', '/')).FirstOrDefault(f => f.EndsWith(sufixo));
        var cor = Achar("_Color.jpg"); var nor = Achar("_NormalGL.jpg");
        var ni = (TextureImporter)AssetImporter.GetAtPath(nor); if (ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
        l.diffuseTexture = Tex(cor); l.normalMapTexture = Tex(nor); l.normalScale = 1f; l.tileSize = new Vector2(tile, tile); l.smoothness = 0.15f;
        if (nome == "Lama") l.diffuseRemapMax = new Vector4(0.55f, 0.55f, 0.5f, 1f);
        if (nome == "Grama" || nome == "GramaEscura") l.diffuseRemapMax = new Vector4(0.74f, 1.0f, 0.70f, 1f);   // puxa o chao de grama para um verde mais vivo (de longe parecia deserto)
        EditorUtility.SetDirty(l); return l;
    }

    static Mats Materiais()
    {
        var m = new Mats();
        foreach (var d in new[] { "Camadas", "Materiais" }) Directory.CreateDirectory($"{Raiz}/{d}");
        AssetDatabase.Refresh();
        m.camadas = new[]
        {
            Camada("Grama", "Grass004", 5f),            // 0 campo
            Camada("GramaEscura", "Grass001", 5f),      // 1 floresta
            Camada("Barro", "Ground076", 4.5f),         // 2 caminho de barro / terra batida
            Camada("Lama", "Ground109", 5f),            // 3 pantano
            Camada("Pedra", "Ground111", 5f),           // 4 cascalho / encosta
            Camada("Folhas", "Ground106", 5f),          // 5 chao de floresta
            Camada("GramaSeca", "ScatteredLeaves009", 4f), // 6 terras secas
            Camada("Pavimento", "PavingStones149", 4.2f), // 7 praca da vila (lajes irregulares de pedra)
        };
        var shG = Shader.Find("Pinglu/GramaTufo"); m.grama = new Material(shG);
        m.grama.enableInstancing = true; AssetDatabase.CreateAsset(m.grama, $"{Raiz}/Materiais/GramaTufo.mat"); EditorUtility.SetDirty(m.grama); AssetDatabase.SaveAssets();
        var shA = Shader.Find("Pinglu/AguaSimples"); m.agua = new Material(shA);
        var nm = Tex($"{Raiz}/Texturas/agua_normal.png"); var ni = (TextureImporter)AssetImporter.GetAtPath($"{Raiz}/Texturas/agua_normal.png");
        if (ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
        m.agua.SetTexture("_NormalMap", Tex($"{Raiz}/Texturas/agua_normal.png"));
        m.agua.SetColor("_ShallowColor", new Color(0.24f, 0.27f, 0.12f, 0.92f)); m.agua.SetColor("_DeepColor", new Color(0.07f, 0.09f, 0.045f, 0.995f)); m.agua.SetFloat("_DepthDistance", 1.1f); m.agua.SetFloat("_NormalScale", 0.05f); m.agua.SetFloat("_FoamWidth", 0.25f); m.agua.SetColor("_FoamColor", new Color(0.42f, 0.45f, 0.26f, 1f)); m.agua.SetFloat("_Smoothness", 40f);
        AssetDatabase.CreateAsset(m.agua, $"{Raiz}/Materiais/Agua.mat");
        return m;
    }

    // material URP de um prop a partir das texturas "<id>_diff/_nor"
    static Material MatProp(string id, Mats m)
    {
        if (m.props.TryGetValue(id, out var existente)) return existente;
        var path = $"{Raiz}/Materiais/{id}.mat";
        var exist = AssetDatabase.LoadAssetAtPath<Material>(path); if (exist) { m.props[id] = exist; return exist; }
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        string T(string k) => Directory.GetFiles($"{Raiz}/Modelos/tex").Select(f => f.Replace('\\', '/')).FirstOrDefault(f => Path.GetFileName(f).StartsWith($"{id}_{k}."));
        var diff = T("diff"); var nor = T("nor");
        if (diff != null) mat.SetTexture("_BaseMap", Tex(diff));
        if (nor != null)
        {
            var ni = (TextureImporter)AssetImporter.GetAtPath(nor); if (ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
            mat.SetTexture("_BumpMap", Tex(nor)); mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Smoothness", 0.2f); mat.SetFloat("_Metallic", 0f);
        if (diff != null && diff.EndsWith(".png")) { mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.5f); mat.EnableKeyword("_ALPHATEST_ON"); mat.SetFloat("_Cull", 0f); mat.renderQueue = 2450; }
        mat.enableInstancing = true;
        AssetDatabase.CreateAsset(mat, path); m.props[id] = mat; return mat;
    }

    // ---------------------------------------------------------------- terreno
    static float SS(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
    static float Fbm(float x, float y, int oit, float lac = 2f, float gain = 0.5f)
    {
        float a = 1, f = 1, s = 0, n = 0;
        for (int i = 0; i < oit; i++) { s += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 17.3f * i); n += a; a *= gain; f *= lac; }
        return s / n;
    }

    // biomas por posicao (x,z em [0,1]): 0 campo (leste), 1 floresta (centro), 2 pantano (oeste), 3 terras queimadas (sul)
    public static float[] Bioma(float u, float v)
    {
        float w = Fbm(u * 3f, v * 3f, 3) * 0.12f;
        float pantano = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.40f, 0.22f, u + w));
        float campo = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.60f, 0.78f, u + w));
        float queimado = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.34f, 0.18f, v + w)) * (1 - pantano);
        float floresta = Mathf.Clamp01(1 - pantano - campo - queimado);
        return new[] { campo, floresta, pantano, queimado };
    }

    public const float VilaX = Tam * 0.82f;
    public static float VilaZ => CaminhoZ(VilaX);
    static float CaminhoZ(float x) => Tam * (0.55f + 0.10f * Mathf.Sin(x / Tam * 6.0f) + 0.05f * Mathf.Sin(x / Tam * 14f));
    // ---------------- terracos em volta da vila: degraus de ~1,15 m em aneis, so nos arcos norte e sul (o oeste fica livre para a estrada)
    static readonly float[] TerR = { 56f, 72f };
    const float PassoTerraco = 1.15f;
    static float AnguloVila(float x, float z) => Mathf.Atan2(z - VilaZ, x - VilaX) * Mathf.Rad2Deg;
    static float SetorTerraco(float x, float z)
    {
        float a = AnguloVila(x, z);
        float n = SS(30f, 45f, a) * (1 - SS(135f, 150f, a)), sul = SS(-150f, -135f, a) * (1 - SS(-45f, -30f, a));
        return Mathf.Max(n, sul);
    }
    static float RampaTerraco(float a)
    {
        float r = 0; foreach (var c in new[] { 75f, 112f, -75f, -112f }) { float d = Mathf.DeltaAngle(a, c) / 4f; r = Mathf.Max(r, Mathf.Exp(-d * d)); }
        return r;
    }
    public static float Terraco(float x, float z)
    {
        float dv = Vector2.Distance(new Vector2(x, z), new Vector2(VilaX, VilaZ)); float a = AnguloVila(x, z);
        float setor = SetorTerraco(x, z); if (setor <= 0.001f) return 0f;
        float w = Mathf.Lerp(1.4f, 10f, RampaTerraco(a)), soma = 0f;
        foreach (var r in TerR) soma += SS(r, r + w, dv);
        return soma * PassoTerraco * setor * (1 - SS(84f, 118f, dv));
    }

    // caminho: curva senoidal de leste a oeste ate a vila
    static float DistCaminho(float x, float z) // metros
    {
        float cz = Tam * (0.55f + 0.10f * Mathf.Sin(x / Tam * 6.0f) + 0.05f * Mathf.Sin(x / Tam * 14f));
        return Mathf.Abs(z - cz);
    }

    static Terrain CriarTerreno(Mats m)
    {
        var td = new TerrainData { heightmapResolution = Res, size = new Vector3(Tam, AltT, Tam) };
        AssetDatabase.DeleteAsset($"{Raiz}/Terreno.asset"); AssetDatabase.CreateAsset(td, $"{Raiz}/Terreno.asset");   // primeiro cria o asset: os mapas de camadas viram sub-assets salvos
        var h = new float[Res, Res];
        for (int j = 0; j < Res; j++)
            for (int i = 0; i < Res; i++)
            {
                float u = i / (float)(Res - 1), v = j / (float)(Res - 1);
                float x = u * Tam, z = v * Tam;
                var b = Bioma(u, v);
                float colinas = (Fbm(u * 3.2f, v * 3.2f, 5) - 0.5f) * 1.5f;                       // relevo
                float detalhe = (Fbm(u * 24f, v * 24f, 3) - 0.5f) * 0.04f;               // microrrelevo
                float alt = 0.30f + colinas * (0.30f + 0.15f * b[1]) + detalhe;
                // pantano: bacia baixa com poças
                float poca = Mathf.Clamp01(1.0f - Mathf.Abs(Fbm(u * 6f + 5, v * 6f + 9, 3) - 0.5f) * 5.0f);
                float lago = 1 - SS(0.03f, 0.17f, Vector2.Distance(new Vector2(u, v), new Vector2(0.20f, 0.50f)));              // lago grande no meio do pantano
                float pocas = 0f;                                          // poças menores
                float fundo = Mathf.Clamp01(lago + pocas * b[2]);
                // margem elevada em volta do lago (colinas dos dois lados), com uma abertura suave ao sul para chegar na agua
                float dl = Vector2.Distance(new Vector2(u, v), new Vector2(0.20f, 0.50f));
                float anel = SS(0.12f, 0.20f, dl) * (1 - SS(0.24f, 0.34f, dl));
                float ang = Mathf.Atan2(v - 0.50f, u - 0.20f); float abertura = SS(0.35f, 0.9f, Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, -90f)) / 90f);
                float ruidoAnel = 0.6f + 0.9f * Fbm(u * 9f + 3, v * 9f + 5, 3);
                alt = Mathf.Lerp(alt, 0.228f + colinas * 0.03f - fundo * 0.10f + anel * abertura * 0.075f * ruidoAnel, b[2] * 0.95f);
                // campo: mais plano; terras queimadas: cristas rachadas
                alt = Mathf.Lerp(alt, 0.32f + colinas * 0.12f, b[0] * 0.6f);
                alt += b[3] * (Fbm(u * 14f, v * 14f, 2) - 0.5f) * 0.06f;
                // caminho: aplaina
                float dc = DistCaminho(x, z); float cam = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.2f, 8f, dc));
                float cz = 0.32f;
                alt = Mathf.Lerp(alt, Mathf.Lerp(alt, cz + colinas * 0.1f, 0.85f), cam * (1 - b[2]));
                float dvv = Vector2.Distance(new Vector2(x, z), new Vector2(VilaX, VilaZ)); float plano = 1 - SS(40f, 70f, dvv);
                alt = Mathf.Lerp(alt, 0.30f + (Fbm(VilaX / Tam * 3.2f, VilaZ / Tam * 3.2f, 5) - 0.5f) * 0.1f, plano);
                alt += Terraco(x, z) / AltT;
                h[j, i] = Mathf.Clamp01(alt);
            }
        td.SetHeights(0, 0, h);
        td.alphamapResolution = 512;
        td.terrainLayers = m.camadas;
        var a = new float[512, 512, m.camadas.Length];
        int NL = m.camadas.Length;
        for (int j = 0; j < 512; j++)
            for (int i = 0; i < 512; i++)
            {
                float u = i / 511f, v = j / 511f; float x = u * Tam, z = v * Tam;
                var b = Bioma(u, v);
                float slope = td.GetSteepness(u, v);
                float ruido = Fbm(u * 18f, v * 18f, 3), ruido2 = Fbm(u * 5f + 3, v * 5f + 8, 3);
                float dc = DistCaminho(x, z); float cam = 1 - SS(2.6f, 5.2f, dc + (ruido - 0.5f) * 1.8f);
                var w = new float[NL];
                w[0] = b[0] * (0.5f + 0.5f * ruido2);                       // grama campo
                w[1] = b[1] * (0.45f + 0.55f * ruido2) + b[0] * (1 - ruido2) * 0.5f;   // grama escura
                w[5] = b[1] * (0.25f + 0.75f * ruido) * SS(0.35f, 0.7f, ruido2);          // folhas
                w[3] = b[2] * 1.0f;                                          // lama
                w[6] = b[3] * 1.0f;                                          // grama seca (terras secas)
                w[2] = b[3] * 0.22f * SS(0.45f, 0.75f, ruido2) + 0f;   // barro so no caminho
                float pedra = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18f, 32f, slope));
                float manchas = SS(0.62f, 0.78f, Fbm(u * 10f + 40, v * 10f + 4, 3));
                w[4] = Mathf.Max(pedra, manchas * 0.55f * (1 - b[2]));
                // caminho de terra com cascalho nas bordas
                w[2] += cam * 1.6f; w[4] += cam * 0.25f * SS(0.4f, 0.7f, ruido);   // caminho de barro, com cascalho nas bordas
                // margem do pantano: cascalho/lama
                // praca da vila (pavimento) e entorno de terra batida
                float dv = Vector2.Distance(new Vector2(x, z), new Vector2(VilaX, VilaZ));
                float praca = 1 - SS(11.5f, 14.0f, dv + (ruido - 0.5f) * 2f); float terraV = 1 - SS(14f, 26f, dv + (ruido - 0.5f) * 5f);
                if (praca > 0) { for (int k = 0; k < 7; k++) w[k] *= 1 - praca; w[7] = praca * 3f; }
                if (terraV > 0) { w[2] += terraV * 0.9f; for (int k = 0; k < 7; k++) if (k != 2) w[k] *= 1 - terraV * 0.8f; }
                float s2 = 0; for (int k = 0; k < NL; k++) s2 += w[k]; if (s2 < 1e-4f) { w[0] = 1; s2 = 1; }
                for (int k = 0; k < NL; k++) a[j, i, k] = w[k] / s2;
            }
        td.SetAlphamaps(0, 0, a);
        { var rb = td.GetAlphamaps(0, 0, 512, 512); Debug.Log("[Ambiente] alpha readback pantano: " + string.Join(" ", Enumerable.Range(0, 7).Select(k => rb[256, 90, k].ToString("0.00")))); }
        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
        var go = Terrain.CreateTerrainGameObject(td); go.name = "Terreno"; go.transform.position = Vector3.zero;
        var t = go.GetComponent<Terrain>(); t.drawInstanced = true; t.detailObjectDistance = 90; t.detailObjectDensity = 1f; t.heightmapPixelError = 6;
        t.basemapDistance = 200;
        return t;
    }

    static void GramaInstanciada(Terrain t, Mats m)
    {
        var cg = t.gameObject.AddComponent<CampoDeGrama>();
        Mesh Malha(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Modelos/Grama/{n}.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
        cg.tufos = new[] { "GrassTuft_C", "GrassTuft_A", "GrassTuft_B", "GrassTuft_Tall" }.Select(Malha).ToArray();
        cg.tufosLonge = new[] { "GrassLod_C", "GrassLod_A", "GrassLod_B", "GrassLod_Tall" }.Select(Malha).ToArray();
        cg.material = m.grama;
        var seca = new Material(m.grama.shader);
        seca.SetColor("_BaseColor", new Color(0.26f, 0.19f, 0.07f)); seca.SetColor("_TipColor", new Color(0.80f, 0.68f, 0.30f));
        seca.SetColor("_DryColor", new Color(0.72f, 0.52f, 0.22f)); seca.SetColor("_DarkColor", new Color(0.50f, 0.38f, 0.14f));
        seca.SetFloat("_PatchStrength", 0.6f); seca.SetFloat("_WindStrength", 0.04f); seca.enableInstancing = true;
        AssetDatabase.DeleteAsset($"{Raiz}/Materiais/GramaSeca.mat"); AssetDatabase.CreateAsset(seca, $"{Raiz}/Materiais/GramaSeca.mat");
        cg.materialSeco = seca;
    }

    // ---------------------------------------------------------------- (antigo) grama como detalhe de malha do Terrain - nao usado
    static void Grama(Terrain t, Mats m)
    {
        var td = t.terrainData;
        var nomes = new[] { "GrassTuft_C", "GrassTuft_A", "GrassTuft_B", "GrassTuft_Tall" };
        var protos = new List<DetailPrototype>();
        foreach (var n in nomes)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Modelos/Grama/{n}.fbx");
            var go = new GameObject(n); go.AddComponent<MeshFilter>().sharedMesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m.grama;
            var pp = $"{Raiz}/Modelos/Grama/{n}.prefab"; var pf = PrefabUtility.SaveAsPrefabAsset(go, pp); Object.DestroyImmediate(go);
            protos.Add(new DetailPrototype
            {
                prototype = pf, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                minWidth = 0.8f, maxWidth = 1.5f, minHeight = 0.7f, maxHeight = 1.4f, noiseSpread = 0.06f, holeEdgePadding = 0.1f,
                healthyColor = Color.white, dryColor = Color.white, density = 1f,
            });
        }
        td.detailPrototypes = protos.ToArray();
        td.SetDetailResolution(1024, 16);
        var layers = new int[protos.Count][];
        for (int k = 0; k < protos.Count; k++) layers[k] = new int[1024 * 1024];
        var al = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
        for (int j = 0; j < 1024; j++)
            for (int i = 0; i < 1024; i++)
            {
                float u = i / 1023f, v = j / 1023f; float x = u * Tam, z = v * Tam;
                int ai = Mathf.Clamp((int)(u * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1), aj = Mathf.Clamp((int)(v * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);
                float campo = al[aj, ai, 0], esc = al[aj, ai, 1], folha = al[aj, ai, 5], lama = al[aj, ai, 3], cam = al[aj, ai, 2] + al[aj, ai, 6], rocha = al[aj, ai, 4];
                float b = Bioma(u, v)[2];
                float clump = Fbm(u * 30f, v * 30f, 2);                                    // clareiras e zonas densas
                float dens = (campo * 1.0f + esc * 0.8f + folha * 0.35f + lama * 0.25f) * SS(0.30f, 0.62f, clump) * (1 - cam * 1.5f) * (1 - rocha);
                dens = Mathf.Clamp01(dens);
                if (dens < 0.05f) continue;
                int n = Mathf.RoundToInt(dens * 5f * (0.5f + Fbm(u * 80f + 5, v * 80f, 1)));
                float alto = SS(0.55f, 0.85f, Fbm(u * 12f + 9, v * 12f + 1, 3));
                for (int c = 0; c < n; c++)
                {
                    float r = (float)rnd.NextDouble();
                    int idx = r < 0.30f ? 0 : r < 0.62f ? 1 : r < 0.62f + 0.30f * (1 - alto * 0.3f) ? 2 : 3;
                    if (idx == 3 && (lama > 0.5f)) idx = 2;
                    layers[idx][j * 1024 + i] = Mathf.Min(16, layers[idx][j * 1024 + i] + 1);
                }
            }
        for (int k = 0; k < protos.Count; k++)
        {
            var arr = new int[1024, 1024];
            for (int j = 0; j < 1024; j++) for (int i = 0; i < 1024; i++) arr[j, i] = layers[k][j * 1024 + i];
            td.SetDetailLayer(0, 0, k, arr);
        }
        EditorUtility.SetDirty(td);
    }

    // ---------------------------------------------------------------- props em tres camadas
    class Regra
    {
        public string[] modelos; public float[] bioma = new float[4]; public float densidade; public float raioGrupo, porGrupo; public float escMin = 0.8f, escMax = 1.3f;
        public float declMax = 30f; public bool arvore = false; public bool evitaCaminho = true, noPantano = false, naVila = false, colidir = false; public float alinhar = 0.6f;
    }

    const float Esc = 0.5f;   // escala do numero de objetos pelo tamanho do mapa
    static void Props(Terrain t)
    {
        var mats = new Mats();
        var raiz = new GameObject("Props").transform;
        PrepararModelos();
        var regras = new List<Regra>();
        string[] Tem(params string[] prefixos) => Directory.GetFiles($"{Raiz}/Modelos", "*.fbx").Select(Path.GetFileNameWithoutExtension).Where(n => prefixos.Any(p => n.StartsWith(p))).ToArray();
        // [campo, floresta, pantano, seco]
        // pedras GRANDES e espacadas (nada de pedrinhas)
        regras.Add(new Regra { modelos = Tem("boulder_01"), colidir = true, bioma = new[] { 0.3f, 0.5f, 0.1f, 0.6f }, densidade = 22, raioGrupo = 14, porGrupo = 1.5f, escMin = 0.7f, escMax = 1.5f, declMax = 45 });
        regras.Add(new Regra { modelos = Tem("namaqualand_boulder"), colidir = true, bioma = new[] { 0.4f, 0.5f, 0.3f, 0.9f }, densidade = 30, raioGrupo = 12, porGrupo = 1.5f, escMin = 0.8f, escMax = 1.6f, declMax = 45 });
        regras.Add(new Regra { modelos = Tem("rock_moss_set_01"), colidir = true, bioma = new[] { 0.2f, 0.7f, 0.4f, 0.4f }, densidade = 18, raioGrupo = 12, porGrupo = 1.5f, escMin = 0.45f, escMax = 0.9f, declMax = 45 });
        regras.Add(new Regra { modelos = Tem("rock_moss_set_02"), colidir = true, bioma = new[] { 0.5f, 0.7f, 0.5f, 0.4f }, densidade = 40, raioGrupo = 9, porGrupo = 1.5f, escMin = 0.4f, escMax = 0.8f, declMax = 45 });
        // arvores
        regras.Add(new Regra { modelos = Tem("pine_tree_01"), arvore = true, bioma = new[] { 0.0f, 1f, 0.1f, 0.0f }, densidade = 90, raioGrupo = 12, porGrupo = 3, escMin = 0.9f, escMax = 1.4f, declMax = 30 });
        regras.Add(new Regra { modelos = Tem("fir_tree_01"), arvore = true, bioma = new[] { 0.0f, 0.8f, 0.0f, 0.0f }, densidade = 50, raioGrupo = 12, porGrupo = 3, escMin = 0.9f, escMax = 1.4f, declMax = 30 });
        regras.Add(new Regra { modelos = Tem("jacaranda_tree"), arvore = true, bioma = new[] { 0.35f, 0.1f, 0.0f, 0.0f }, densidade = 8, raioGrupo = 20, porGrupo = 1, escMin = 0.9f, escMax = 1.2f, declMax = 25 });
        regras.Add(new Regra { modelos = Tem("tree_small_02"), arvore = true, bioma = new[] { 0.4f, 0.6f, 0.2f, 0.0f }, densidade = 28, raioGrupo = 12, porGrupo = 2, escMin = 0.9f, escMax = 1.4f });
        regras.Add(new Regra { modelos = Tem("quiver_tree"), arvore = true, bioma = new[] { 0.0f, 0.0f, 0.0f, 1.0f }, densidade = 55, raioGrupo = 14, porGrupo = 2, escMin = 0.9f, escMax = 1.5f, declMax = 35 });
        regras.Add(new Regra { modelos = Tem("dead_quiver_trunk", "dead_tree_trunk_02"), bioma = new[] { 0.0f, 0.1f, 0.8f, 0.9f }, densidade = 40, raioGrupo = 14, porGrupo = 2, escMin = 0.8f, escMax = 1.3f });
        // medios
        regras.Add(new Regra { modelos = Tem("tree_stump"), colidir = true, bioma = new[] { 0.3f, 1f, 0.3f, 0.5f }, densidade = 55, raioGrupo = 9, porGrupo = 2, escMin = 0.7f, escMax = 1.1f });
        regras.Add(new Regra { modelos = Tem("shrub_02"), bioma = new[] { 0.8f, 1f, 0.2f, 0.1f }, densidade = 90, raioGrupo = 7, porGrupo = 3, escMin = 0.6f, escMax = 1.2f });
        // pequenos (sem pedrinhas): galhos, flores, samambaias
        regras.Add(new Regra { modelos = Tem("dry_branches", "bark_debris", "root_cluster"), bioma = new[] { 0.2f, 1f, 0.7f, 1.0f }, densidade = 200, raioGrupo = 4f, porGrupo = 3, escMin = 0.8f, escMax = 1.4f });
        regras.Add(new Regra { modelos = Tem("celandine", "dandelion"), bioma = new[] { 1f, 0.4f, 0.0f, 0.0f }, densidade = 220, raioGrupo = 3f, porGrupo = 8, escMin = 1.0f, escMax = 2.0f });
        regras.Add(new Regra { modelos = Tem("fern", "shrub_03", "nettle"), bioma = new[] { 0.2f, 1f, 0.7f, 0.0f }, densidade = 200, raioGrupo = 3.5f, porGrupo = 5, escMin = 0.9f, escMax = 1.8f });
        int total = 0;
        foreach (var r in regras)
        {
            if (r.modelos.Length == 0) continue;
            if (UsarKit && !r.modelos.Any(m => m.StartsWith("tree_stump") || m.StartsWith("dry_branches"))) continue;   // o kit cobre pedras, arvores, arbustos e flores
            var prefabs = r.modelos.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Modelos/{n}.fbx")).ToArray();
            int grupos = Mathf.Max(1, Mathf.RoundToInt(r.densidade * Esc));
            int tent = grupos * 10;
            for (int g = 0; g < tent && grupos > 0; g++)
            {
                float u = (float)rnd.NextDouble(), v = (float)rnd.NextDouble();
                var b = Bioma(u, v); float p = b[0] * r.bioma[0] + b[1] * r.bioma[1] + b[2] * r.bioma[2] + b[3] * r.bioma[3];
                if (rnd.NextDouble() > p) continue;
                if (Fbm(u * 9f + r.densidade, v * 9f, 2) < 0.35f && rnd.NextDouble() < 0.7) continue;     // vazios entre os grupos
                grupos--;
                int k = Mathf.Max(1, Mathf.RoundToInt(r.porGrupo * (0.5f + (float)rnd.NextDouble())));
                for (int q = 0; q < k; q++)
                {
                    float ang = (float)rnd.NextDouble() * Mathf.PI * 2, rr = r.raioGrupo * Mathf.Sqrt((float)rnd.NextDouble());
                    float x = u * Tam + Mathf.Cos(ang) * rr, z = v * Tam + Mathf.Sin(ang) * rr;
                    if (x < 3 || z < 3 || x > Tam - 3 || z > Tam - 3) continue;
                    float uu = x / Tam, vv = z / Tam;
                    if (r.evitaCaminho && DistCaminho(x, z) < 3.2f) continue;
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(VilaX, VilaZ)) < 50f && !r.naVila) continue;
                    float slope = t.terrainData.GetSteepness(uu, vv); if (slope > r.declMax) continue;
                    float y = t.SampleHeight(new Vector3(x, 0, z));
                    if (y < NivelAgua + 0.25f && uu < 0.5f) continue;
                    var n = t.terrainData.GetInterpolatedNormal(uu, vv);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rnd.Next(prefabs.Length)]);
                    go.transform.SetParent(raiz); go.transform.position = new Vector3(x, y - 0.03f, z);
                    var rot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, n), r.alinhar) * Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                    go.transform.rotation = rot; go.transform.localScale = Vector3.one * Mathf.Lerp(r.escMin, r.escMax, (float)rnd.NextDouble());
                    var id = IdTextura(go.name.Replace("(Clone)", ""));
                    var mt = MatProp(id, mats); foreach (var rd in go.GetComponentsInChildren<Renderer>()) rd.sharedMaterial = mt;
                    if (r.arvore) AddTronco(go); else if (r.colidir) AddCollider(go);
                    go.isStatic = true; total++;
                }
            }
        }
        PedrasDoCaminho(t, raiz);
        Debug.Log("[Ambiente] props espalhados: " + total);
    }

    // pedras grandes e espacadas ao longo do caminho de barro (estilo trilha de Zelda)
    static void PedrasDoCaminho(Terrain t, Transform pai)
    {
        if (UsarKit) { Pantano(t, pai); return; }
        TrilhaDeLajes(t, pai); Pantano(t, pai);
        var r2 = new System.Random(99);
        var nomes = Directory.GetFiles($"{Raiz}/Modelos", "*.fbx").Select(Path.GetFileNameWithoutExtension).Where(n => n.StartsWith("namaqualand_boulder") || n.StartsWith("rock_moss_set_02")).ToArray();
        if (nomes.Length == 0) return; int n = 0;
        for (float x = 10; x < Tam - 10; x += 7f + (float)r2.NextDouble() * 8f)
        {
            float cz = CaminhoZ(x);
            foreach (int lado in new[] { -1, 1 })
            {
                if (r2.NextDouble() < 0.35) continue;
                float z = cz + lado * (3.5f + (float)r2.NextDouble() * 2.2f); float xx = x + (float)(r2.NextDouble() * 4 - 2);
                if (Vector2.Distance(new Vector2(xx, z), new Vector2(VilaX, VilaZ)) < 36f) continue;
                float y = t.SampleHeight(new Vector3(xx, 0, z)); if (y < NivelAgua + 0.3f) continue;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Modelos/{nomes[r2.Next(nomes.Length)]}.fbx");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf); go.transform.SetParent(pai);
                var nrm = t.terrainData.GetInterpolatedNormal(xx / Tam, z / Tam);
                go.transform.position = new Vector3(xx, y - 0.05f, z);
                go.transform.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, nrm), 0.8f) * Quaternion.Euler(0, (float)r2.NextDouble() * 360f, 0);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.3f, (float)r2.NextDouble());
                var id = IdTextura(go.name.Replace("(Clone)", "")); var mt = MatProp(id, new Mats()); foreach (var rd in go.GetComponentsInChildren<Renderer>()) rd.sharedMaterial = mt;
                AddCollider(go); go.isStatic = true; n++;
            }
        }
        Debug.Log("[Ambiente] pedras do caminho: " + n);
    }

    // trilha de lajes: pedras chatas espacadas, em zigue-zague, no meio do caminho de barro (estilo Zelda)
    static void TrilhaDeLajes(Terrain t, Transform pai)
    {
        var mat = MatPBR("Laje", "Rock064", 1f, new Color(1f, 1f, 1f), 0.1f); var r2 = new System.Random(5); int n = 0;
        var fbx = new[] { "Laje_A", "Laje_B", "Laje_C", "Laje_D" }.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Trilha/{k}.fbx")).Where(g => g).ToArray();
        if (fbx.Length == 0) return;
        foreach (var f in Directory.GetFiles($"{Raiz}/Trilha", "Laje_*.fbx").Select(x => x.Replace("\\", "/")))
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(f); mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport();
        }
        float x = 12; int lado = 1;
        while (x < Tam - 8)
        {
            float cz = CaminhoZ(x); float z = cz + lado * (0.55f + (float)r2.NextDouble() * 0.5f); lado = -lado;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(VilaX, VilaZ)) > 15f)
            {
                float y = t.SampleHeight(new Vector3(x, 0, z)); var nrm = t.terrainData.GetInterpolatedNormal(x / Tam, z / Tam);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx[r2.Next(fbx.Length)]); go.transform.SetParent(pai);
                go.transform.position = new Vector3(x, y - 0.03f, z);
                go.transform.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, nrm), 0.9f) * Quaternion.Euler(0, (float)r2.NextDouble() * 360f, 0);
                go.transform.localScale = new Vector3(1f, 1f, 1f) * Mathf.Lerp(1.0f, 1.45f, (float)r2.NextDouble());
                foreach (var rd in go.GetComponentsInChildren<Renderer>()) rd.sharedMaterial = mat;
                go.isStatic = true; n++;
            }
            x += 1.7f + (float)r2.NextDouble() * 1.3f;      // espacadas
        }
        Debug.Log("[Ambiente] lajes da trilha: " + n);
    }

    // juncos e vitorias-regias no pantano
    static void Pantano(Terrain t, Transform pai)
    {
        var r2 = new System.Random(8); int nj = 0, nv = 0;
        var juncos = new[] { "Junco_A", "Junco_B" }.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Trilha/{k}.fbx")).Where(g => g).ToArray();
        var vit = new[] { "Vitoria_A", "Vitoria_B" }.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Trilha/{k}.fbx")).Where(g => g).ToArray();
        if (juncos.Length == 0) return;
        var mj = new Material(Shader.Find("Pinglu/GramaTufo")); mj.SetColor("_BaseColor", new Color(0.05f, 0.12f, 0.04f)); mj.SetColor("_TipColor", new Color(0.36f, 0.46f, 0.16f));
        mj.SetColor("_DryColor", new Color(0.55f, 0.50f, 0.22f)); mj.SetColor("_DarkColor", new Color(0.16f, 0.26f, 0.10f)); mj.SetFloat("_WindStrength", 0.05f); mj.enableInstancing = true;
        AssetDatabase.DeleteAsset($"{Raiz}/Materiais/Junco.mat"); AssetDatabase.CreateAsset(mj, $"{Raiz}/Materiais/Junco.mat");
        var mv = new Material(Shader.Find("Universal Render Pipeline/Lit")); mv.SetColor("_BaseColor", new Color(0.12f, 0.28f, 0.08f)); mv.SetFloat("_Smoothness", 0.5f); mv.SetFloat("_Cull", 0f); mv.enableInstancing = true;
        AssetDatabase.DeleteAsset($"{Raiz}/Materiais/Vitoria.mat"); AssetDatabase.CreateAsset(mv, $"{Raiz}/Materiais/Vitoria.mat");
        foreach (var g in juncos.Concat(vit)) { var mi = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(g)); if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport(); } }
        for (int i = 0; i < 9000 && (nj < 260 || nv < 90); i++)
        {
            float x = (0.04f + (float)r2.NextDouble() * 0.30f) * Tam, z = (0.30f + (float)r2.NextDouble() * 0.40f) * Tam;
            float y = t.SampleHeight(new Vector3(x, 0, z));
            if (nj < 260 && y > NivelAgua - 0.45f && y < NivelAgua + 0.55f)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(juncos[r2.Next(juncos.Length)]); go.transform.SetParent(pai);
                go.transform.position = new Vector3(x, y - 0.1f, z); go.transform.rotation = Quaternion.Euler(0, (float)r2.NextDouble() * 360f, 0); go.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.5f, (float)r2.NextDouble());
                foreach (var rd in go.GetComponentsInChildren<Renderer>()) rd.sharedMaterial = mj; go.isStatic = true; nj++;
            }
            else if (nv < 90 && vit.Length > 0 && y < NivelAgua - 0.5f && y > NivelAgua - 2.2f)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(vit[r2.Next(vit.Length)]); go.transform.SetParent(pai);
                go.transform.position = new Vector3(x, NivelAgua + 0.03f, z); go.transform.rotation = Quaternion.Euler(0, (float)r2.NextDouble() * 360f, 0); go.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.6f, (float)r2.NextDouble());
                foreach (var rd in go.GetComponentsInChildren<Renderer>()) rd.sharedMaterial = mv; go.isStatic = true; nv++;
            }
        }
        Debug.Log($"[Ambiente] pantano: {nj} juncos, {nv} vitorias-regias");
    }

    static string IdTextura(string nome)
    {
        var id = nome;
        while (id.Length > 0 && !Directory.GetFiles($"{Raiz}/Modelos/tex", id + "_diff.*").Any()) { int k = id.LastIndexOf('_'); if (k < 0) break; id = id.Substring(0, k); }
        return id;
    }
    static void PrepararModelos()
    {
        foreach (var f in Directory.GetFiles($"{Raiz}/Modelos", "*.fbx"))
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(f.Replace("\\", "/"));
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport(); }
        }
    }

    // ---------------------------------------------------------------- agua do pantano
    static void Agua(Terrain t, Mats m)
    {
        // malha de agua so onde o terreno esta abaixo do nivel (celulas de 1 m), sem retangulo flutuando no ar
        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>(); var mapa = new Dictionary<int, int>();
        int Id(int i, int j) { int k = j * (Tam + 1) + i; if (!mapa.TryGetValue(k, out var idx)) { idx = verts.Count; verts.Add(new Vector3(i, NivelAgua, j)); uvs.Add(new Vector2(i, j)); mapa[k] = idx; } return idx; }
        for (int j = 0; j < Tam; j++)
            for (int i = 0; i < Tam; i++)
            {
                float h = Mathf.Min(Mathf.Min(t.SampleHeight(new Vector3(i, 0, j)), t.SampleHeight(new Vector3(i + 1, 0, j))), Mathf.Min(t.SampleHeight(new Vector3(i, 0, j + 1)), t.SampleHeight(new Vector3(i + 1, 0, j + 1))));
                if (h > NivelAgua - 0.02f) continue;
                int a0 = Id(i, j), b0 = Id(i + 1, j), c0 = Id(i + 1, j + 1), d0 = Id(i, j + 1);
                tris.AddRange(new[] { a0, d0, c0, a0, c0, b0 });
            }
        if (verts.Count == 0) return;
        var me = new Mesh { name = "AguaMalha", indexFormat = IndexFormat.UInt32 }; me.SetVertices(verts); me.SetUVs(0, uvs); me.SetTriangles(tris, 0); me.RecalculateNormals(); me.RecalculateBounds();
        AssetDatabase.DeleteAsset($"{Raiz}/AguaMalha.asset"); AssetDatabase.CreateAsset(me, $"{Raiz}/AguaMalha.asset");
        var g = new GameObject("AguaPantano"); g.AddComponent<MeshFilter>().sharedMesh = me; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m.agua; r.shadowCastingMode = ShadowCastingMode.Off;
        g.transform.position = t.transform.position;
    }

    // ---------------------------------------------------------------- luz, neblina, cameras
    static void Luz()
    {
        var l = new GameObject("Sol").AddComponent<Light>(); l.type = LightType.Directional; l.transform.rotation = Quaternion.Euler(38, 140, 0);
        l.intensity = 1.35f; l.color = new Color(1f, 0.93f, 0.82f); l.shadows = LightShadows.Soft; RenderSettings.sun = l;
        RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(0.46f, 0.56f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.34f, 0.32f); RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.12f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.0075f; RenderSettings.fogColor = new Color(0.52f, 0.62f, 0.68f);
        var sky = new Material(Shader.Find("Skybox/Procedural")); sky.SetFloat("_AtmosphereThickness", 1.0f); sky.SetColor("_SkyTint", new Color(0.5f, 0.55f, 0.65f));
        RenderSettings.skybox = sky;
    }

    // lugares do mapa (atalhos 1-6 do jogador e pontos das fotos): nome, posicao do jogador, para onde olhar
    public static (string nome, Vector3 pos, Vector3 olhar)[] Lugares()
    {
        Vector3 P(float u, float v) => new Vector3(u * Tam, 0, v * Tam);
        float cz = CaminhoZ(Tam * 0.55f);
        return new[]
        {
            ("Vila", new Vector3(VilaX, 0, VilaZ - 6f), new Vector3(VilaX, 0, VilaZ + 10f)),
            ("Campo", P(0.68f, 0.66f), P(0.82f, 0.72f)),
            ("Estrada de barro", new Vector3(Tam * 0.55f, 0, cz), new Vector3(Tam * 0.7f, 0, CaminhoZ(Tam * 0.7f))),
            ("Floresta", P(0.44f, 0.64f), P(0.52f, 0.72f)),
            ("Pantano", P(0.34f, 0.5f), P(0.18f, 0.5f)),
            ("Terras secas", P(0.5f, 0.14f), P(0.6f, 0.2f)),
        };
    }
    public static readonly (string nome, Vector3 pos, Vector3 olhar)[] Pontos = {
        ("vila", Vector3.zero, Vector3.zero), ("vila2", Vector3.zero, Vector3.zero), ("casa", Vector3.zero, Vector3.zero), ("taverna", Vector3.zero, Vector3.zero),
        ("campo", Vector3.zero, Vector3.zero), ("estrada", Vector3.zero, Vector3.zero), ("floresta", Vector3.zero, Vector3.zero), ("pantano", Vector3.zero, Vector3.zero),
        ("seco", Vector3.zero, Vector3.zero), ("chao", Vector3.zero, Vector3.zero), ("terraco", Vector3.zero, Vector3.zero), ("terraco2", Vector3.zero, Vector3.zero), ("escada", Vector3.zero, Vector3.zero), ("escada2", Vector3.zero, Vector3.zero) };
    static Vector3 PosFoto((string nome, Vector3 pos, Vector3 olhar) p)
    {
        var L = Lugares();
        switch (p.nome)
        {
            case "vila": return new Vector3(VilaX - 2, 0, VilaZ - 30);
            case "terraco": return new Vector3(VilaX - 6, 0, VilaZ + 40);
            case "terraco2": return new Vector3(VilaX - 40, 0, VilaZ + 62);
            case "escada": return new Vector3(VilaX + 0.259f*48 + 3.0f, 0, VilaZ + 0.966f*48 + 0.8f);
            case "escada2": return new Vector3(VilaX + 0.259f*62 + 7.0f, 0, VilaZ + 0.966f*62 - 5.5f);
            case "vila2": return new Vector3(VilaX + 10, 0, VilaZ - 8);
            case "casa": return new Vector3(VilaX - 21 - 6, 0, VilaZ + 15 - 13);
            case "taverna": return new Vector3(VilaX + 2 - 5, 0, VilaZ + 24 - 16);
            case "campo": return L[1].pos; case "estrada": return L[2].pos; case "floresta": return L[3].pos; case "pantano": return L[4].pos; case "seco": return L[5].pos;
            default: return L[1].pos + new Vector3(6, 0, 4);
        }
    }
    static Vector3 OlharFoto((string nome, Vector3 pos, Vector3 olhar) p)
    {
        var L = Lugares();
        switch (p.nome)
        {
            case "vila": return new Vector3(VilaX, 0, VilaZ + 6);
            case "terraco": return new Vector3(VilaX - 2, 0, VilaZ + 80);
            case "terraco2": return new Vector3(VilaX + 10, 0, VilaZ + 70);
            case "escada": return new Vector3(VilaX + 0.259f*62, 0, VilaZ + 0.966f*62);
            case "escada2": return new Vector3(VilaX + 0.259f*57, 0, VilaZ + 0.966f*57);
            case "vila2": return new Vector3(VilaX - 14, 0, VilaZ + 12);
            case "casa": return new Vector3(VilaX - 21, 0, VilaZ + 15);
            case "taverna": return new Vector3(VilaX + 2, 0, VilaZ + 24);
            case "campo": return L[1].olhar; case "estrada": return L[2].olhar; case "floresta": return L[3].olhar; case "pantano": return L[4].olhar; case "seco": return L[5].olhar;
            default: return L[1].pos + new Vector3(9, 0, 6);
        }
    }
    static void Cameras()
    {
        var c = new GameObject("Main Camera").AddComponent<Camera>(); c.tag = "MainCamera"; c.fieldOfView = 62; c.farClipPlane = 600; c.clearFlags = CameraClearFlags.Skybox;
        c.gameObject.AddComponent<AudioListener>();
        var t = Object.FindFirstObjectByType<Terrain>();
        c.transform.position = new Vector3(VilaX, t.SampleHeight(new Vector3(VilaX, 0, VilaZ - 16)) + 3f, VilaZ - 16); c.transform.LookAt(new Vector3(VilaX, 2f, VilaZ));
    }

    // fotos de conferencia (batch): uma por ponto
    public static void Fotos()
    {
        EditorSceneManager.OpenScene(Cena);
        var cam = Camera.main; var t = Object.FindFirstObjectByType<Terrain>();
        var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; Directory.CreateDirectory("Fotos");
        foreach (var p in Pontos)
        {
            float alt = p.nome == "chao" ? 0.55f : 1.8f; var pp = PosFoto(p); var oo = OlharFoto(p);
            cam.transform.position = new Vector3(pp.x, t.SampleHeight(pp) + alt, pp.z); cam.transform.LookAt(new Vector3(oo.x, t.SampleHeight(oo) + (p.nome == "chao" ? 0.35f : (p.nome.StartsWith("vila") ? 3.5f : (p.nome == "casa" || p.nome == "taverna") ? 2.8f : 1.0f)), oo.z));
            cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes($"Fotos/ambiente_{p.nome}.png", tex.EncodeToPNG());
        }
        cam.targetTexture = null;
    }
}
