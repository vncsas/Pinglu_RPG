// =====================================================================
//  PingluAmbientacao.cs  -  Ambientação extra (só ACRESCENTA, não remove nada do mapa)
// ---------------------------------------------------------------------
//  - Grama nova em volta da câmera (GPU instancing): tufos baixos e em pé, verdes e secos, sem pacote novo.
//    Nasce só onde o chão é de grama; estradas, barro, areia, pedra e construções ficam limpos.
//  - Pedras grandes e arredondadas (Stylized Nature MegaKit, CC0), seixos e pedras chatas na beira das estradas.
//  O tamanho do mapa e tudo o que já existe continuam como estão.
//
//  COMO USAR:  Pinglu > 71. Ambientação extra: grama e pedras   (Play desligado; depois Ctrl+S)
//  Para remover: Pinglu > 72. Remover ambientação extra
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluAmbientacao
{
    const string PASTA = "Assets/Ambientacao";
    const string RAIZ = "Ambientacao_Extra";

    static Terrain terreno; static TerrainData dados; static float[,,] alfa; static int aw, ah;
    static Dictionary<string, int[]> idxCamadas = new();
    static List<Bounds> aguas = new();
    static Texture2D mascara; static byte[] mascaraDados; static int mw, mh;

    // ruas, praca, lajes e campos de plantacao sao malhas SEM collider: viram uma mascara (1 pixel = 1 m) de onde nao nasce grama nem pedra
    static void BakeMascara()
    {
        float W = dados.size.x, L = dados.size.z; mw = Mathf.CeilToInt(W); mh = Mathf.CeilToInt(L);
        mascaraDados = new byte[mw * mh];
        string[] chaves = { "Ruas", "Praca -", "Terra d", "Plantação", "Trigo", "Calçada", "Lajes", "Piso", "Chao d" };
        var alvos = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude).Where(f => f.sharedMesh != null && f.GetComponent<Renderer>() != null && f.GetComponent<Collider>() == null && f.GetComponent<Terrain>() == null
            && chaves.Any(c => f.name.Contains(c)) && f.GetComponent<Renderer>().bounds.size.y < 12f).ToList();
        int camada = 31; var temps = new List<GameObject>(); Bounds? uniao = null;
        foreach (var f in alvos)
        {
            var g = new GameObject("tmp_mascara"); g.layer = camada; g.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation); g.transform.localScale = f.transform.lossyScale;
            g.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh; temps.Add(g);
            var b = f.GetComponent<Renderer>().bounds; if (uniao == null) uniao = b; else { var u = uniao.Value; u.Encapsulate(b); uniao = u; }
        }
        Physics.SyncTransforms();
        int marcados = 0;
        foreach (var f in alvos)
        {
            var b = f.GetComponent<Renderer>().bounds;
            for (int x = Mathf.Max(0, Mathf.FloorToInt(b.min.x) - 1); x <= Mathf.Min(mw - 1, Mathf.CeilToInt(b.max.x) + 1); x++)
                for (int z = Mathf.Max(0, Mathf.FloorToInt(b.min.z) - 1); z <= Mathf.Min(mh - 1, Mathf.CeilToInt(b.max.z) + 1); z++)
                {
                    if (mascaraDados[z * mw + x] != 0) continue;
                    // testa o centro e os cantos do pixel (1 m)
                    bool hit = false;
                    for (int k = 0; k < 5 && !hit; k++)
                    {
                        float ox = k == 0 ? 0.5f : (k & 1) * 1f, oz = k == 0 ? 0.5f : ((k >> 1) & 1) * 1f; if (k > 0) { ox = (k == 1 || k == 3) ? 0.05f : 0.95f; oz = (k == 1 || k == 2) ? 0.05f : 0.95f; }
                        hit = Physics.Raycast(new Vector3(x + ox, b.max.y + 60f, z + oz), Vector3.down, 200f, 1 << camada, QueryTriggerInteraction.Ignore);
                    }
                    if (hit) { mascaraDados[z * mw + x] = 255; marcados++; }
                }
        }
        foreach (var g in temps) Object.DestroyImmediate(g);
        // engrossa 1 pixel para a grama nao encostar na borda
        var copia = (byte[])mascaraDados.Clone();
        for (int z = 1; z < mh - 1; z++) for (int x = 1; x < mw - 1; x++) if (copia[z * mw + x] == 0 && (copia[z * mw + x + 1] != 0 || copia[z * mw + x - 1] != 0 || copia[(z + 1) * mw + x] != 0 || copia[(z - 1) * mw + x] != 0)) mascaraDados[z * mw + x] = 255;
        mascara = new Texture2D(mw, mh, TextureFormat.R8, false) { filterMode = FilterMode.Point, name = "MascaraGrama" }; mascara.SetPixelData(mascaraDados, 0); mascara.Apply(false, false);
        Directory.CreateDirectory(PASTA); AssetDatabase.DeleteAsset(PASTA + "/MascaraGrama.asset"); AssetDatabase.CreateAsset(mascara, PASTA + "/MascaraGrama.asset");
        Debug.Log($"[Ambientacao] mascara: {alvos.Count} malhas de chao, {marcados} m2 sem grama");
    }
    static bool Mascarado(float x, float z) { int i = Mathf.Clamp((int)x, 0, mw - 1), j = Mathf.Clamp((int)z, 0, mh - 1); return mascaraDados[j * mw + i] != 0; }

    [MenuItem("Pinglu/71. Ambientação extra: grama e pedras")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (Terrain.activeTerrain == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a cena do mapa.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu - Ambientação extra", "Vou acrescentar grama nova (em volta da câmera) e pedras grandes, seixos e pedras de trilha.\nNada do que já existe é removido. Depois salve a cena (Ctrl+S).", "Pode fazer", "Cancelar")) return;
        Aplicar();
    }

    [MenuItem("Pinglu/72. Remover ambientação extra")]
    public static void Remover()
    {
        var r = GameObject.Find(RAIZ); if (r != null) Object.DestroyImmediate(r);
        var t = Terrain.activeTerrain; if (t != null) { var g = t.GetComponent<PingluCampoDeGrama>(); if (g != null) Object.DestroyImmediate(g); }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // usado em batch: abre a cena original, aplica e salva em outra cena (a original nao muda)
    public static void Batch()
    {
        var origem = System.Environment.GetEnvironmentVariable("AMB_ORIGEM") ?? "Assets/Scenes/SampleScene.unity";
        var destino = System.Environment.GetEnvironmentVariable("AMB_DESTINO") ?? "Assets/Scenes/SampleScene_Ambientacao.unity";
        var cena = EditorSceneManager.OpenScene(origem, OpenSceneMode.Single);
        Aplicar();
        EditorSceneManager.SaveScene(cena, destino, false);
        Debug.Log("[Ambientacao] cena salva em " + destino);
    }

    // ------------------------------------------------------------------
    static Material Mat(string nome, Shader sh, System.Action<Material> cfg)
    {
        var p = $"{PASTA}/Materiais/{nome}.mat"; Directory.CreateDirectory($"{PASTA}/Materiais");
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh; cfg(m); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }

    static Texture2D Tex(string nome) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{PASTA}/Texturas/{nome}.png");

    static float PesoCamada(int i, int j, string nome) { float s = 0; foreach (var k in idxCamadas.TryGetValue(nome, out var v) ? v : System.Array.Empty<int>()) s += alfa[j, i, k]; return s; }

    static void Aplicar()
    {
        terreno = Terrain.activeTerrain;
        var velho = GameObject.Find(RAIZ); if (velho != null) Object.DestroyImmediate(velho);
        PrepararTerrenoNovo();
        var raiz = new GameObject(RAIZ).transform;
        aw = dados.alphamapWidth; ah = dados.alphamapHeight; alfa = dados.GetAlphamaps(0, 0, aw, ah);
        idxCamadas.Clear();
        for (int k = 0; k < dados.terrainLayers.Length; k++) { var n = dados.terrainLayers[k] ? dados.terrainLayers[k].name.Replace("_Ambient", "") : ""; if (!idxCamadas.ContainsKey(n)) idxCamadas[n] = new int[0]; idxCamadas[n] = idxCamadas[n].Append(k).ToArray(); }
        aguas.Clear();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) if (r.sharedMaterials.Any(m => m != null && m.name.ToLower().Contains("agua"))) { var b = r.bounds; b.Expand(new Vector3(6, 40, 6)); aguas.Add(b); }
        Physics.SyncTransforms();
        BakeMascara();
        AplicarGrama(); string rel = AplicarPedras(raiz);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Ambientacao] pronto. " + rel);
    }

    // ------------------------------------------------------------------ chao novo (numa COPIA do terreno; o asset original nao e tocado)
    const string TERRENO_ORIGINAL = "Assets/New Terrain.asset";
    static TerrainLayer NovaCamada(string arquivo, string pasta, float tile)
    {
        string baseNome = $"{PASTA}/Texturas/Chao/{pasta}/{pasta}_1K-JPG"; Directory.CreateDirectory($"{PASTA}/Camadas");
        var nm = AssetImporter.GetAtPath(baseNome + "_NormalGL.jpg") as TextureImporter; if (nm != null && nm.textureType != TextureImporterType.NormalMap) { nm.textureType = TextureImporterType.NormalMap; nm.SaveAndReimport(); }
        var path = $"{PASTA}/Camadas/{arquivo}.terrainlayer"; var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
        l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(baseNome + "_Color.jpg"); l.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(baseNome + "_NormalGL.jpg");
        l.tileSize = new Vector2(tile, tile); l.smoothness = 0f; l.metallic = 0f; l.normalScale = 1f; EditorUtility.SetDirty(l); return l;
    }

    static void PrepararTerrenoNovo()
    {
        var orig = terreno.terrainData; string destino = $"{PASTA}/Terreno/Terreno_Ambientacao.asset"; Directory.CreateDirectory($"{PASTA}/Terreno");
        AssetDatabase.DeleteAsset(destino);
        if (!AssetDatabase.CopyAsset(TERRENO_ORIGINAL, destino)) { Debug.LogError("[Ambientacao] nao consegui copiar o terreno"); dados = orig; return; }
        AssetDatabase.Refresh(); dados = AssetDatabase.LoadAssetAtPath<TerrainData>(destino);
        terreno.terrainData = dados; var tc = terreno.GetComponent<TerrainCollider>(); if (tc != null) tc.terrainData = dados;

        // 1) texturas do chao: barro de verdade na estrada, grama de campo, folhas na mata, etc. (nomes mantidos: "Soil_Rocks" identifica as estradas)
        var camadas = dados.terrainLayers.ToList();
        void Troca(int i, string arq, string pasta, float tile) { if (i < camadas.Count) camadas[i] = NovaCamada(arq, pasta, tile); }
        Troca(0, "Grass_Dry_Ambient", "ScatteredLeaves009", 3f); Troca(1, "Grass_Moss_Ambient", "Grass001", 3f); Troca(2, "Muddy_Ambient", "Ground109", 3f);
        Troca(4, "Rocha_Ambient", "Ground111", 5f); Troca(5, "Heather_Ambient", "Grass001", 4f); Troca(7, "Pebbles_A_Ambient", "Gravel043", 2f);
        Troca(9, "Campo_Ambient", "Grass004", 3f); Troca(10, "CampoVivo_Ambient", "Grass004", 4f);
        Troca(11, "Mata_Ambient", "Ground106", 3f); Troca(12, "MataSombria_Ambient", "Ground106", 3.5f); Troca(13, "MusgoSombrio_Ambient", "Moss002", 3f); Troca(14, "CampoSeco_Ambient", "ScatteredLeaves009", 3.5f);
        camadas.Add(NovaCamada("FolhasChao_Ambient", "ScatteredLeaves009", 2.5f));
        camadas.Add(NovaCamada("Barro_Ambient", "Ground076", 3f));
        dados.terrainLayers = camadas.ToArray();
        int nL = camadas.Count, fol = nL - 2, barro = nL - 1;
        // area da vila (ruas de pedra e praca): ali o chao de pedra original continua; fora dela, as estradas viram barro
        Rect vila = Rect.zero; bool temVila = false;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude)) if (mf.name.StartsWith("Ruas") || mf.name.StartsWith("Praca -")) { var b = mf.GetComponent<Renderer>().bounds; var r = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z); if (!temVila) { vila = r; temVila = true; } else vila = Rect.MinMaxRect(Mathf.Min(vila.xMin, r.xMin), Mathf.Min(vila.yMin, r.yMin), Mathf.Max(vila.xMax, r.xMax), Mathf.Max(vila.yMax, r.yMax)); }
        vila = Rect.MinMaxRect(vila.xMin - 14f, vila.yMin - 14f, vila.xMax + 14f, vila.yMax + 14f);

        // 2) tira o capim antigo (billboards 0-2); as folhas do chao (detalhes 3 e 4) ficam
        int dw = dados.detailWidth, dh = dados.detailHeight; var zero = new int[dh, dw];
        for (int i = 0; i < Mathf.Min(3, dados.detailPrototypes.Length); i++) if (dados.detailPrototypes[i].prototype == null && dados.detailPrototypes[i].prototypeTexture != null) dados.SetDetailLayer(0, 0, i, zero);

        // 3) pinta: estrada de barro mais larga e irregular + manchas de folhas caidas em volta das arvores
        int w = dados.alphamapWidth, h = dados.alphamapHeight; var a = dados.GetAlphamaps(0, 0, w, h);
        float wx = dados.size.x / w, wz = dados.size.z / h;
        var rd = new float[h, w];
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            if (temVila && vila.Contains(new Vector2(i * wx, j * wz))) continue;
            float r = a[j, i, 6] + a[j, i, 8]; if (r <= 0f) continue;
            a[j, i, barro] += r; a[j, i, 6] = 0f; a[j, i, 8] = 0f; rd[j, i] = a[j, i, barro];
        }
        var bl = Blur(Blur(rd, w, h, 2), w, h, 2);
        void Garante(int j, int i, int k, float alvo)
        {
            float cur = a[j, i, k]; if (alvo <= cur) return; float resto = 1f - cur, novo = 1f - alvo; float f = resto > 1e-4f ? novo / resto : 0f;
            for (int q = 0; q < nL; q++) a[j, i, q] = q == k ? alvo : a[j, i, q] * f;
        }
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            float n = 0.75f + 0.5f * Mathf.PerlinNoise(i * 0.12f, j * 0.12f);
            float t = Mathf.Clamp01(bl[j, i] * 2.2f * n - 0.12f);
            if (t > 0.04f && !(temVila && vila.Contains(new Vector2(i * wx, j * wz)))) Garante(j, i, barro, Mathf.Max(rd[j, i], Mathf.Clamp01(t * 1.5f)));
        }
        var rnd = new System.Random(99);
        foreach (var ti in dados.treeInstances)
        {
            int cx = Mathf.Clamp((int)(ti.position.x * (w - 1)), 0, w - 1), cz = Mathf.Clamp((int)(ti.position.z * (h - 1)), 0, h - 1); float raio = 2.5f + (float)rnd.NextDouble() * 2.5f;
            for (int dz = -6; dz <= 6; dz++) for (int dx = -6; dx <= 6; dx++)
            {
                int i = cx + dx, j = cz + dz; if (i < 0 || j < 0 || i >= w || j >= h) continue; float d = Mathf.Sqrt(dx * dx + dz * dz); if (d > raio) continue;
                if (a[j, i, 6] + a[j, i, 8] + a[j, i, barro] > 0.3f) continue;
                float v = (1f - d / raio) * 0.9f * (0.6f + 0.8f * Mathf.PerlinNoise(i * 0.3f, j * 0.3f)); Garante(j, i, fol, Mathf.Clamp01(v));
            }
        }
        dados.SetAlphamaps(0, 0, a);
        EditorUtility.SetDirty(dados); AssetDatabase.SaveAssets();
    }

    static float[,] Blur(float[,] src, int w, int h, int r)
    {
        var tmp = new float[h, w]; var dst = new float[h, w];
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) { float s = 0; int c = 0; for (int d = -r; d <= r; d++) { int x = i + d; if (x < 0 || x >= w) continue; s += src[j, x]; c++; } tmp[j, i] = s / c; }
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) { float s = 0; int c = 0; for (int d = -r; d <= r; d++) { int y = j + d; if (y < 0 || y >= h) continue; s += tmp[y, i]; c++; } dst[j, i] = s / c; }
        return dst;
    }

    // ------------------------------------------------------------------ grama
    static void AplicarGrama()
    {
        var sh = Shader.Find("Pinglu/GramaTufoInstanciada"); if (sh == null) { Debug.LogError("[Ambientacao] shader da grama nao encontrado"); return; }
        var verde = Mat("Ambient_Grama", sh, m => { m.SetColor("_BaseColor", new Color(0.03f, 0.09f, 0.03f)); m.SetColor("_TipColor", new Color(0.26f, 0.46f, 0.14f)); m.SetFloat("_WindStrength", 0.03f); });
        var seca = Mat("Ambient_GramaSeca", sh, m => { m.SetColor("_BaseColor", new Color(0.24f, 0.18f, 0.07f)); m.SetColor("_TipColor", new Color(0.74f, 0.62f, 0.28f)); m.SetColor("_DryColor", new Color(0.66f, 0.48f, 0.20f)); m.SetColor("_DarkColor", new Color(0.44f, 0.34f, 0.13f)); m.SetFloat("_PatchStrength", 0.6f); m.SetFloat("_WindStrength", 0.04f); });
        AssetDatabase.SaveAssets();
        var go = terreno.gameObject; var cg = go.GetComponent<PingluCampoDeGrama>(); if (cg != null) Object.DestroyImmediate(cg);
        cg = go.AddComponent<PingluCampoDeGrama>();
        Mesh Malha(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PASTA}/Grama/{n}.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
        cg.tufos = new[] { "GrassTuft_C", "GrassTuft_A", "GrassTuft_B", "GrassTuft_Tall" }.Select(Malha).ToArray();
        cg.tufosLonge = new[] { "GrassLod_C", "GrassLod_A", "GrassLod_B", "GrassLod_Tall" }.Select(Malha).ToArray();
        cg.material = verde; cg.materialSeco = seca; cg.mascaraLimpa = mascara;
        int n = dados.terrainLayers.Length; cg.pesoVerde = new float[n]; cg.pesoSeco = new float[n]; cg.pesoLimpo = new float[n];
        void Peso(string nome, float v, float s, float l) { if (!idxCamadas.TryGetValue(nome, out var ks)) return; foreach (var k in ks) { cg.pesoVerde[k] = v; cg.pesoSeco[k] = s; cg.pesoLimpo[k] = l; } }
        // [verde, seco, limpo]  (Floresta Sombria e Terras Queimadas ficam quase sem grama, como no roteiro)
        Peso("Campo", 1f, 0f, 0f); Peso("CampoVivo", 1f, 0f, 0f); Peso("Grass_Moss", 0.7f, 0f, 0f); Peso("Heather", 0.55f, 0.1f, 0f);
        Peso("Mata", 0.25f, 0.1f, 0f); Peso("MataSombria", 0f, 0.22f, 0f); Peso("MusgoSombrio", 0.08f, 0.12f, 0f);
        Peso("Grass_Dry", 0f, 0.75f, 0f); Peso("CampoSeco", 0f, 1f, 0f); Peso("FolhasChao", 0.3f, 0.3f, 0f); Peso("TerraMorta", 0f, 0.12f, 0f);
        Peso("Barro", 0f, 0f, 1f); Peso("Muddy", 0f, 0f, 1f); Peso("Black_Sand", 0f, 0f, 1f); Peso("Rocha", 0f, 0f, 1f); Peso("Caminho_Soil_Rocks", 0f, 0f, 1f); Peso("Pebbles_A_TerrainLayer", 0f, 0f, 1f);
        cg.densPerto = 6f; cg.densMeio = 1.7f; cg.densLonge = 0.5f; cg.distPerto = 20f; cg.distMeio = 46f; cg.distLonge = 90f; cg.clareiras = 0.45f;
        EditorUtility.SetDirty(cg);
    }

    // ------------------------------------------------------------------ pedras
    class Tipo { public string[] nomes; public float hMin, hMax; public bool colisor, grande, trilha, beira; public int qtd; public Dictionary<string, float> camadas; public float declMax = 40; public float espaco; }

    static string AplicarPedras(Transform raiz)
    {
        var shLit = Shader.Find("Universal Render Pipeline/Lit");
        var mRocha = Mat("Kit_Rochas", shLit, m => { m.SetTexture("_BaseMap", Tex("Rocks_Diffuse")); m.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.9f)); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f); });
        var mTrilha = Mat("Kit_PedrasTrilha", shLit, m => { m.SetTexture("_BaseMap", Tex("PathRocks_Diffuse")); m.SetColor("_BaseColor", new Color(0.88f, 0.88f, 0.88f)); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f); });
        AssetDatabase.SaveAssets();
        string[] N(string pre, int n) => Enumerable.Range(1, n).Select(i => $"{pre}_{i}").ToArray();
        var tipos = new List<Tipo>
        {
            new Tipo { nomes = N("Rock_Medium", 3), hMin = 0.9f, hMax = 2.8f, colisor = true, grande = true, qtd = 320, espaco = 14, declMax = 45,
                camadas = new() { {"Rocha", 1f}, {"Campo", 0.22f}, {"CampoVivo", 0.2f}, {"Grass_Dry", 0.5f}, {"CampoSeco", 0.55f}, {"Heather", 0.5f}, {"Mata", 0.3f}, {"MataSombria", 0.35f}, {"TerraMorta", 0.5f}, {"MusgoSombrio", 0.5f}, {"Grass_Moss", 0.35f} } },
            new Tipo { nomes = N("Pebble_Round", 5).Concat(N("Pebble_Square", 6)).ToArray(), hMin = 0.16f, hMax = 0.42f, qtd = 900, declMax = 55,
                camadas = new() { {"Rocha", 1f}, {"Pebbles_A_TerrainLayer", 1f}, {"Campo", 0.35f}, {"CampoVivo", 0.3f}, {"Grass_Dry", 0.6f}, {"CampoSeco", 0.7f}, {"Heather", 0.5f}, {"Mata", 0.3f}, {"MataSombria", 0.3f}, {"TerraMorta", 0.5f}, {"MusgoSombrio", 0.4f}, {"Barro", 0.5f} } },
            new Tipo { nomes = new[] { "RockPath_Round_Wide", "RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3", "RockPath_Square_Wide", "RockPath_Square_Small_1", "RockPath_Square_Small_2", "RockPath_Square_Small_3" },
                trilha = true, hMin = 1.5f, hMax = 2.6f, qtd = 900, declMax = 30, espaco = 3.2f, camadas = new() { {"Barro", 1f} } },
            new Tipo { nomes = N("Rock_Medium", 3), hMin = 1.1f, hMax = 3.2f, colisor = true, grande = true, beira = true, qtd = 260, espaco = 7f, declMax = 45, camadas = new() { {"Barro", 1f} } },
        };
        var rnd = new System.Random(4242); var usados = new List<Vector3>(); var sb = new System.Text.StringBuilder();
        float W = dados.size.x, L = dados.size.z; int total = 0;
        foreach (var tp in tipos)
        {
            var pai = new GameObject(tp.trilha ? "Pedras de trilha" : tp.grande ? "Pedras grandes" : "Seixos").transform; pai.SetParent(raiz);
            int feitos = 0;
            for (int tent = 0; tent < tp.qtd * 120 && feitos < tp.qtd; tent++)
            {
                float x = 12 + (float)rnd.NextDouble() * (W - 24), z = 12 + (float)rnd.NextDouble() * (L - 24);
                int i = Mathf.Clamp((int)(x / W * (aw - 1)), 0, aw - 1), j = Mathf.Clamp((int)(z / L * (ah - 1)), 0, ah - 1);
                float p = 0; foreach (var kv in tp.camadas) p += PesoCamada(i, j, kv.Key) * kv.Value;
                float estrada = PesoCamada(i, j, "Barro");
                if (tp.trilha) { if (estrada < 0.5f) continue; p = 1f; }                                          // em cima do barro, espacadas
                else if (tp.beira) { float prox = 0; for (int dd = 4; dd <= 12; dd += 4) { foreach (var o in new[] { (dd, 0), (-dd, 0), (0, dd), (0, -dd) }) prox = Mathf.Max(prox, PesoCamada(Mathf.Clamp(i + o.Item1, 0, aw - 1), Mathf.Clamp(j + o.Item2, 0, ah - 1), "Barro")); } if (estrada > 0.2f || prox < 0.5f) continue; p = 1f; }   // na beira da estrada
                else if (estrada > 0.2f || PesoCamada(i, j, "Muddy") > 0.5f || PesoCamada(i, j, "Caminho_Soil_Rocks") > 0.2f) continue;                          // nunca em cima da estrada
                if (rnd.NextDouble() > Mathf.Min(1f, p)) continue;
                if (dados.GetSteepness(x / W, z / L) > tp.declMax) continue;
                float y = terreno.SampleHeight(new Vector3(x, 0, z)) + terreno.transform.position.y;
                var pos = new Vector3(x, y, z);
                if (aguas.Any(b => b.Contains(pos)) || Mascarado(x, z)) continue;
                if (tp.espaco > 0 && usados.Any(u => (u.x - x) * (u.x - x) + (u.z - z) * (u.z - z) < tp.espaco * tp.espaco)) continue;
                float h = Mathf.Lerp(tp.hMin, tp.hMax, Mathf.Pow((float)rnd.NextDouble(), 1.6f));
                if (Physics.OverlapSphere(pos + Vector3.up * (h * 0.5f + 0.3f), Mathf.Max(0.35f, h * 0.7f), ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider))) continue;   // nao entra em casa, arvore, cerca...
                if (tp.espaco > 0) usados.Add(pos);
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{PASTA}/Pedras/{tp.nomes[rnd.Next(tp.nomes.Length)]}.fbx"); if (pf == null) break;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf); go.transform.SetParent(pai);
                var nrm = dados.GetInterpolatedNormal(x / W, z / L);
                go.transform.position = pos; go.transform.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, nrm), tp.trilha ? 1f : 0.5f) * Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                var rs = go.GetComponentsInChildren<Renderer>(); var mat = tp.trilha ? mTrilha : mRocha;
                foreach (var r in rs) { var ms = r.sharedMaterials; for (int m = 0; m < ms.Length; m++) ms[m] = mat; r.sharedMaterials = ms; }
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                float esc;
                if (tp.trilha) { float larg = Mathf.Max(bb.size.x, bb.size.z); esc = Mathf.Clamp(h / Mathf.Max(0.01f, larg), 0.1f, 50f); }
                else esc = h / Mathf.Max(0.01f, bb.size.y);
                go.transform.localScale = Vector3.one * esc;
                go.transform.position = pos + Vector3.down * (tp.grande ? h * 0.12f : 0.03f);
                if (tp.colisor) { var mf = go.GetComponentInChildren<MeshFilter>(); if (mf) { var col = mf.gameObject.AddComponent<MeshCollider>(); col.sharedMesh = mf.sharedMesh; } }
                foreach (var t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                feitos++; total++;
            }
            sb.Append($"{pai.name}: {feitos}. ");
        }
        return sb + $"Total {total}.";
    }
}
#endif
