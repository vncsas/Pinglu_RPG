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

    // abre a cena nova ao iniciar o editor (usado por quem lanca o Unity por linha de comando)
    public static void AbrirCena() { EditorSceneManager.OpenScene("Assets/Scenes/SampleScene_Ambientacao.unity", OpenSceneMode.Single); }

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
        var velho = GameObject.Find(RAIZ);
        if (velho != null) { Debug.LogWarning("[Ambientacao] esta cena ja tem a ambientacao aplicada (o relevo mexe nos objetos). Abra a SampleScene original e rode de novo."); if (!Application.isBatchMode) EditorUtility.DisplayDialog("Pinglu", "Esta cena ja tem a ambientacao aplicada. Abra a SampleScene original e rode de novo.", "OK"); return; }
        ocupLocal = null; lagoWL = -999f;
        aguas.Clear();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) if (r.sharedMaterials.Any(m => m != null && m.name.ToLower().Contains("agua"))) { var b = r.bounds; b.Expand(new Vector3(6, 40, 6)); aguas.Add(b); }
        PrepararTerrenoNovo();
        var raiz = new GameObject(RAIZ).transform;
        aw = dados.alphamapWidth; ah = dados.alphamapHeight; alfa = dados.GetAlphamaps(0, 0, aw, ah);
        idxCamadas.Clear();
        for (int k = 0; k < dados.terrainLayers.Length; k++) { var n = dados.terrainLayers[k] ? dados.terrainLayers[k].name.Replace("_Ambient", "") : ""; if (!idxCamadas.ContainsKey(n)) idxCamadas[n] = new int[0]; idxCamadas[n] = idxCamadas[n].Append(k).ToArray(); }
        Physics.SyncTransforms();
        BakeMascara();
        Pavimentar(raiz);
        EnvelhecerConstrucoes();
        CriarLago(raiz);
        new GameObject("Atalhos do Mapa").transform.SetParent(raiz); raiz.Find("Atalhos do Mapa").gameObject.AddComponent<PingluAtalhosMapa>();
        AplicarGrama(); string rel = AplicarPedras(raiz); Plantas(raiz);
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
        terreno.basemapDistance = 300f; terreno.detailObjectDistance = 60f; terreno.heightmapPixelError = 5f;   // mais leve (o capim e a grama instanciada)

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

        // folhas do chao (detalhes 3 e 4): so onde ha arvore por perto (raio ~7 m); no resto do mapa saem
        {
            var perto = new bool[dh, dw];
            foreach (var ti in dados.treeInstances)
            {
                int cx = Mathf.Clamp((int)(ti.position.x * (dw - 1)), 0, dw - 1), cz = Mathf.Clamp((int)(ti.position.z * (dh - 1)), 0, dh - 1);
                for (int dz = -7; dz <= 7; dz++) for (int dx = -7; dx <= 7; dx++) { int i = cx + dx, j = cz + dz; if (i >= 0 && j >= 0 && i < dw && j < dh && dx * dx + dz * dz <= 49) perto[j, i] = true; }
            }
            for (int d = 3; d < Mathf.Min(5, dados.detailPrototypes.Length); d++)
            {
                var m = dados.GetDetailLayer(0, 0, dw, dh, d);
                for (int j = 0; j < dh; j++) for (int i = 0; i < dw; i++) if (!perto[j, i]) m[j, i] = 0;
                dados.SetDetailLayer(0, 0, d, m);
            }
        }

        // 3) pinta: estrada de barro mais larga e irregular + manchas de folhas caidas em volta das arvores
        int w = dados.alphamapWidth, h = dados.alphamapHeight; var a = dados.GetAlphamaps(0, 0, w, h);
        float wx = dados.size.x / w, wz = dados.size.z / h;
        var rd = new float[h, w];
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            bool naVila = temVila && vila.Contains(new Vector2(i * wx, j * wz));
            float r = a[j, i, 6] + a[j, i, 8]; if (r <= 0f) continue;
            a[j, i, barro] += r; a[j, i, 6] = 0f; a[j, i, 8] = 0f; if (!naVila) rd[j, i] = a[j, i, barro];
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
        RelevoNasAreasVazias(a, w, h, barro);
        RelevoMapaTodo();
        PintarVariedade(nL, barro, vila, temVila);
        EditorUtility.SetDirty(dados); AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ relevo: colinas suaves SO onde nao ha nada (longe de construcoes, estradas, agua, vila)
    static Vector2 lagoC; static float lagoWL = -999f;
    const int CELULA = 4; static float[,] vazio; static int vazioN;
    static float Vazio(float x, float z) { if (vazio == null) return 0f; int i = Mathf.Clamp((int)(x / CELULA), 0, vazioN - 1), j = Mathf.Clamp((int)(z / CELULA), 0, vazioN - 1); return vazio[j, i]; }

    static void RelevoNasAreasVazias(float[,,] a, int w, int h, int barro)
    {
        float W = dados.size.x, L = dados.size.z; vazioN = Mathf.CeilToInt(W / CELULA); var ocupado = new float[vazioN, vazioN];
        void Marca(Bounds b, float folga) { int i0 = Mathf.Max(0, (int)((b.min.x - folga) / CELULA)), i1 = Mathf.Min(vazioN - 1, (int)((b.max.x + folga) / CELULA)), j0 = Mathf.Max(0, (int)((b.min.z - folga) / CELULA)), j1 = Mathf.Min(vazioN - 1, (int)((b.max.z + folga) / CELULA)); for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) ocupado[j, i] = 1f; }
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r is TrailRenderer || r is ParticleSystemRenderer || r.GetComponent<Terrain>() != null) continue; var b = r.bounds;
            if (b.size.x > 140f || b.size.z > 140f || b.max.y - b.min.y > 90f) continue;                    // montanhas / cenarios enormes: nao contam
            if (b.center.y < -500f) continue;                                                          // cenario do lobby (longe do mapa)
            Marca(b, 16f);
        }
        // estradas e calcadas (qualquer camada de caminho) + vila inteira
        for (int j = 0; j < h; j += 2) for (int i = 0; i < w; i += 2) if (a[j, i, 6] + a[j, i, 8] + a[j, i, barro] + a[j, i, 7] > 0.12f) { var p = new Bounds(new Vector3(i * W / w, 0, j * L / h), Vector3.one); Marca(p, 22f); }
        foreach (var ag in aguas) Marca(ag, 20f);
        foreach (var ti in dados.treeInstances) { } // arvores acompanham o relevo sozinhas
        // suaviza: transicao gradual entre "ocupado" e "vazio"
        var cru = new float[vazioN, vazioN]; for (int j = 0; j < vazioN; j++) for (int i = 0; i < vazioN; i++) cru[j, i] = 1f - ocupado[j, i];
        vazio = Blur(Blur(Blur(cru, vazioN, vazioN, 3), vazioN, vazioN, 3), vazioN, vazioN, 3);
        for (int j = 0; j < vazioN; j++) for (int i = 0; i < vazioN; i++) vazio[j, i] = Mathf.Clamp01((vazio[j, i] - 0.15f) / 0.7f) ;
    }

    static float[,] Blur(float[,] src, int w, int h, int r)
    {
        var tmp = new float[h, w]; var dst = new float[h, w];
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) { float s = 0; int c = 0; for (int d = -r; d <= r; d++) { int x = i + d; if (x < 0 || x >= w) continue; s += src[j, x]; c++; } tmp[j, i] = s / c; }
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) { float s = 0; int c = 0; for (int d = -r; d <= r; d++) { int y = j + d; if (y < 0 || y >= h) continue; s += tmp[y, i]; c++; } dst[j, i] = s / c; }
        return dst;
    }

    // ------------------------------------------------------------------ calcamento da vila: pedras de verdade, uma a uma
    class Fundidor
    {
        public List<Mesh> pronto = new(); List<Vector3> v = new(), n = new(); List<Vector2> uv = new(); List<int> tri = new();
        public void Add(Mesh m, Vector3 pos, Quaternion rot, Vector3 esc, Vector2 uvOff)
        {
            var mv = m.vertices; var mn = m.normals; var mu = m.uv; var mt = m.triangles; int b = v.Count;
            for (int i = 0; i < mv.Length; i++)
            { v.Add(pos + rot * Vector3.Scale(mv[i], esc)); n.Add((rot * Vector3.Scale(mn[i], new Vector3(1f / esc.x, 1f / esc.y, 1f / esc.z))).normalized); uv.Add(mu[i] + uvOff); }
            foreach (var t in mt) tri.Add(b + t);
            if (v.Count > 60000) Fechar();
        }
        public void Fechar()
        {
            if (v.Count == 0) return;
            var me = new Mesh { name = "Calcamento" }; me.SetVertices(v); me.SetNormals(n); me.SetUVs(0, uv); me.SetTriangles(tri, 0); me.RecalculateBounds(); pronto.Add(me);
            v = new(); n = new(); uv = new(); tri = new();
        }
    }

    static Mesh MalhaLegivel(string path)
    {
        var mi = AssetImporter.GetAtPath(path) as ModelImporter; if (mi != null && (!mi.isReadable || mi.materialImportMode != ModelImporterMaterialImportMode.None)) { mi.isReadable = true; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>().sharedMesh;
    }

    static Material MatTex(string nome, string pasta, Color tint, float tile)
    {
        string baseNome = $"{PASTA}/Texturas/Chao/{pasta}/{pasta}_1K-JPG";
        var nm = AssetImporter.GetAtPath(baseNome + "_NormalGL.jpg") as TextureImporter; if (nm != null && nm.textureType != TextureImporterType.NormalMap) { nm.textureType = TextureImporterType.NormalMap; nm.SaveAndReimport(); }
        return Mat(nome, Shader.Find("Universal Render Pipeline/Lit"), m =>
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseNome + "_Color.jpg")); m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(baseNome + "_NormalGL.jpg"); if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetTextureScale("_BumpMap", new Vector2(tile, tile)); }
            m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f);
        });
    }

    // Calcamento da vila: piso de pedra com textura PBR (PavingStones149, ambientCG CC0: lajes grandes irregulares com pedrinhas nas juntas),
    // normal map forte + oclusao + paralaxe para dar relevo. Sem pedras soltas modeladas (nada flutua).
    static Material MatPavimento(string nome, float tile)
    {
        string baseNome = $"{PASTA}/Texturas/Chao/PavingStones149/PavingStones149_1K-JPG";
        var nm = AssetImporter.GetAtPath(baseNome + "_NormalGL.jpg") as TextureImporter; if (nm != null && nm.textureType != TextureImporterType.NormalMap) { nm.textureType = TextureImporterType.NormalMap; nm.SaveAndReimport(); }
        return Mat(nome, Shader.Find("Universal Render Pipeline/Lit"), m =>
        {
            var sc = new Vector2(tile, tile);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseNome + "_Color.jpg")); m.SetTextureScale("_BaseMap", sc); m.SetColor("_BaseColor", new Color(1.25f, 1.2f, 1.15f));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseNome + "_NormalGL.jpg")); m.SetTextureScale("_BumpMap", sc); m.SetFloat("_BumpScale", 1.6f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseNome + "_AmbientOcclusion.jpg")); m.SetTextureScale("_OcclusionMap", sc); m.SetFloat("_OcclusionStrength", 1f);
            m.SetTexture("_ParallaxMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseNome + "_Displacement.jpg")); m.SetTextureScale("_ParallaxMap", sc); m.SetFloat("_Parallax", 0.035f); m.EnableKeyword("_PARALLAXMAP");
            m.SetFloat("_Smoothness", 0.08f); m.SetFloat("_Metallic", 0f);
        });
    }

    static void Pavimentar(Transform raiz)
    {
        var alvos = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude).Where(f => f.sharedMesh != null && (f.name == "Ruas - lajes" || f.name == "Praca - anel" || f.name == "Praca - miolo")).ToList();
        if (alvos.Count == 0) return;
        var ruas = MatPavimento("Vila_Pavimento_Ruas", 1.3f);      // 1 repeticao = ~2,6 m (as ruas e o anel da praca usam ~3,4 m por unidade de UV)
        var miolo = MatPavimento("Vila_Pavimento_Praca", 0.7f);    // o miolo da praca usa ~1,8 m por unidade de UV
        AssetDatabase.SaveAssets();
        foreach (var f in alvos) f.GetComponent<Renderer>().sharedMaterial = f.name == "Praca - miolo" ? miolo : ruas;
        Debug.Log($"[Ambientacao] pavimento: material PBR aplicado em {alvos.Count} malhas da vila");
    }


    // ------------------------------------------------------------------ relevo no MAPA TODO
    // Colinas e vales em 3 escalas (~260 m, ~110 m, ~45 m), mais fortes onde o mapa e vazio. Nao mexe onde ha construcoes grandes
    // (vila, castelo, cemiterio, montanhas, calcadas): ali o relevo desce a zero com transicao suave de 30 m.
    // Objetos pequenos (rochas, arvores como objeto, barracas, animais...) sao reassentados: sobem/descem junto com o chao.
    static void RelevoMapaTodo()
    {
        float W = dados.size.x, Lz = dados.size.z; int G = Mathf.CeilToInt(W / CELULA);
        var prot = new bool[G, G];
        void Protege(Bounds b, float folga) { int i0 = Mathf.Max(0, (int)((b.min.x - folga) / CELULA)), i1 = Mathf.Min(G - 1, (int)((b.max.x + folga) / CELULA)), j0 = Mathf.Max(0, (int)((b.min.z - folga) / CELULA)), j1 = Mathf.Min(G - 1, (int)((b.max.z + folga) / CELULA)); for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) prot[j, i] = true; }
        string[] raizesProtegidas = { "Vila_Gerada", "Cavaleiro Antigo (mini-boss)", "Jogador (Cavaleiro Sombrio)" };
        string[] subProtegidos = { "Cemiterio", "Castelo Destruido", "Camposanto_Antigo_Oculto", "[oculto pelo castelo] Ruinas da Fortaleza" };
        var todos = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        foreach (var r in todos)
        {
            if (r is ParticleSystemRenderer || r.GetComponent<Terrain>() != null) continue; var b = r.bounds; if (b.center.y < -500f) continue;
            var raizT = r.transform.root; bool prote = raizesProtegidas.Contains(raizT.name) || r.transform.GetComponentsInParent<Transform>().Any(t => subProtegidos.Contains(t.name));
            if (b.size.x > 20f || b.size.z > 20f) prote = true;                                          // construcao ou montanha grande
            if (prote) Protege(b, 9f);
        }
        foreach (var ag in aguas) Protege(ag, 10f);
        foreach (var nome in new[] { "Horda do Cemiterio", "Limite da Floresta Sombria", "Encontro do Castelo", "Emboscada da Floresta" })
        { var go = GameObject.Find(nome); if (go == null) continue; foreach (var t in go.GetComponentsInChildren<Transform>()) Protege(new Bounds(t.position, Vector3.one), 14f); }
        var jog = GameObject.Find("Jogador (Cavaleiro Sombrio)"); if (jog != null) Protege(new Bounds(jog.transform.position, Vector3.one), 26f);
        // distancia (em celulas) ao protegido: transformada em duas passadas
        var dist = new float[G, G]; for (int j = 0; j < G; j++) for (int i = 0; i < G; i++) dist[j, i] = prot[j, i] ? 0f : 9999f;
        for (int j = 0; j < G; j++) for (int i = 0; i < G; i++) { if (j > 0) dist[j, i] = Mathf.Min(dist[j, i], dist[j - 1, i] + 1f); if (i > 0) dist[j, i] = Mathf.Min(dist[j, i], dist[j, i - 1] + 1f); if (i > 0 && j > 0) dist[j, i] = Mathf.Min(dist[j, i], dist[j - 1, i - 1] + 1.41f); if (i < G - 1 && j > 0) dist[j, i] = Mathf.Min(dist[j, i], dist[j - 1, i + 1] + 1.41f); }
        for (int j = G - 1; j >= 0; j--) for (int i = G - 1; i >= 0; i--) { if (j < G - 1) dist[j, i] = Mathf.Min(dist[j, i], dist[j + 1, i] + 1f); if (i < G - 1) dist[j, i] = Mathf.Min(dist[j, i], dist[j, i + 1] + 1f); if (i < G - 1 && j < G - 1) dist[j, i] = Mathf.Min(dist[j, i], dist[j + 1, i + 1] + 1.41f); if (i > 0 && j < G - 1) dist[j, i] = Mathf.Min(dist[j, i], dist[j + 1, i - 1] + 1.41f); }
        float Fator(float x, float z) { int i = Mathf.Clamp((int)(x / CELULA), 0, G - 1), j = Mathf.Clamp((int)(z / CELULA), 0, G - 1); float d = dist[j, i] * CELULA; float t = Mathf.Clamp01(d / 30f); return t * t * (3f - 2f * t); }
        int res = dados.heightmapResolution; var hm = dados.GetHeights(0, 0, res, res); float alt = dados.size.y; var delta = new float[res, res]; float soma = 0, mx = 0; int n = 0;
        for (int j = 0; j < res; j++) for (int i = 0; i < res; i++)
        {
            float x = i * W / (res - 1), z = j * Lz / (res - 1); float fa = Fator(x, z); if (fa <= 0.001f) continue;
            float f = (Mathf.PerlinNoise(x / 260f + 11.3f, z / 260f + 3.1f) - 0.5f) * 2f * 0.55f + (Mathf.PerlinNoise(x / 110f + 40f, z / 110f + 70f) - 0.5f) * 2f * 0.33f + (Mathf.PerlinNoise(x / 45f + 5f, z / 45f + 90f) - 0.5f) * 2f * 0.12f;
            float amp = 8f + 5f * (1f - Mathf.Clamp01(OcupacaoLocal(x, z)));                          // mais forte onde e vazio
            float d = f * amp * fa; float nh = Mathf.Clamp01(hm[j, i] + d / alt); d = (nh - hm[j, i]) * alt; hm[j, i] = nh; delta[j, i] = d; soma += Mathf.Abs(d); mx = Mathf.Max(mx, Mathf.Abs(d)); n++;
        }
        // ---- LAGO DO PANTANO (centro do bioma Pantano): bacia funda com anel de colinas dos dois lados
        {
            lagoC = new Vector2(190f, 500f); float R0 = 88f, somaH = 0; int cH = 0;
            for (int j = 0; j < res; j++) for (int i = 0; i < res; i++) { float x = i * W / (res - 1), z = j * Lz / (res - 1); if ((new Vector2(x, z) - lagoC).sqrMagnitude < 55f * 55f) { somaH += hm[j, i] * alt; cH++; } }
            lagoWL = somaH / Mathf.Max(1, cH) - 0.8f;
            for (int j = 0; j < res; j++) for (int i = 0; i < res; i++)
            {
                float x = i * W / (res - 1), z = j * Lz / (res - 1); float r = (new Vector2(x, z) - lagoC).magnitude * (1f + 0.34f * (Mathf.PerlinNoise(x * 0.045f + 3f, z * 0.045f + 9f) - 0.5f)); if (r >= R0) continue;
                float orig = hm[j, i] * alt; float bowl = lagoWL - 3.4f * (1f - Mathf.Pow(Mathf.Clamp01(r / 50f), 2.2f)) + 0.12f * (Mathf.PerlinNoise(x * 0.2f, z * 0.2f) - 0.5f);
                float tgt = r <= 50f ? bowl : lagoWL - 0.27f + 2.8f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((r - 50f) / 34f));
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 62f) / (R0 - 62f))); float nh = Mathf.Lerp(orig, tgt, k);
                delta[j, i] += nh - orig; hm[j, i] = Mathf.Clamp01(nh / alt);
            }
            // arvores do terreno dentro do lago saem (da copia)
            var arv = dados.treeInstances.Where(t => (new Vector2(t.position.x * W, t.position.z * Lz) - lagoC).magnitude > 66f).ToArray(); dados.SetTreeInstances(arv, true);
        }
        // ---- encosta o terreno nas malhas das ruas/praca da vila (sem "degrau" de terra) -- so sobe, ate 5 m para fora da malha
        {
            var chao = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude).Where(f => f.sharedMesh != null && (f.name.StartsWith("Ruas -") || f.name.StartsWith("Praca -"))).ToList(); var temps = new List<GameObject>(); Bounds? uni = null;
            foreach (var f in chao) { var g = new GameObject("tmp_chao"); g.layer = 31; g.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation); g.transform.localScale = f.transform.lossyScale; g.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh; temps.Add(g); var bb = f.GetComponent<Renderer>().bounds; if (uni == null) uni = bb; else { var u = uni.Value; u.Encapsulate(bb); uni = u; } }
            if (uni != null)
            {
                Physics.SyncTransforms(); var ub = uni.Value; int levantados = 0; float topo = ub.max.y + 60f;
                bool Raio(float x, float z, out float yy) { if (Physics.Raycast(new Vector3(x, topo, z), Vector3.down, out var hh, 200f, 1 << 31, QueryTriggerInteraction.Ignore)) { yy = hh.point.y; return true; } yy = 0; return false; }
                int i0 = Mathf.Max(0, (int)((ub.min.x - 6f) / W * (res - 1))), i1 = Mathf.Min(res - 1, (int)((ub.max.x + 6f) / W * (res - 1)) + 1), j0 = Mathf.Max(0, (int)((ub.min.z - 6f) / Lz * (res - 1))), j1 = Mathf.Min(res - 1, (int)((ub.max.z + 6f) / Lz * (res - 1)) + 1);
                var dirs = new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f), (0.7f, 0.7f), (-0.7f, 0.7f), (0.7f, -0.7f), (-0.7f, -0.7f) };
                for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++)
                {
                    float x = i * W / (res - 1), z = j * Lz / (res - 1); float ry, d = 0f; bool ok = Raio(x, z, out ry);
                    for (float dd = 1.5f; !ok && dd <= 5.5f; dd += 1.5f) foreach (var dr in dirs) if (Raio(x + dr.Item1 * dd, z + dr.Item2 * dd, out ry)) { ok = true; d = dd; break; }
                    if (!ok) continue; float alvo = ry - 0.05f, atual = hm[j, i] * alt; if (atual >= alvo) continue;
                    float nv = Mathf.Lerp(atual, alvo, 1f - Mathf.Clamp01(d / 5.5f)); nv = Mathf.Min(nv, atual + 1.2f); delta[j, i] += nv - atual; hm[j, i] = Mathf.Clamp01(nv / alt); levantados++;
                }
                Debug.Log($"[Ambientacao] terreno encostado nas ruas da vila: {levantados} pontos levantados");
            }
            foreach (var g in temps) Object.DestroyImmediate(g);
        }
        dados.SetHeights(0, 0, hm);
        // reassenta objetos pequenos
        float Delta(float x, float z) { float u = Mathf.Clamp01(x / W) * (res - 1), v = Mathf.Clamp01(z / Lz) * (res - 1); int i0 = Mathf.Min((int)u, res - 2), j0 = Mathf.Min((int)v, res - 2); float fu = u - i0, fv = v - j0; return Mathf.Lerp(Mathf.Lerp(delta[j0, i0], delta[j0, i0 + 1], fu), Mathf.Lerp(delta[j0 + 1, i0], delta[j0 + 1, i0 + 1], fu), fv); }
        var feitos = new HashSet<Transform>(); var cacheB = new Dictionary<Transform, Bounds>(); int movidos = 0;
        Bounds BoundsDe(Transform t) { if (!cacheB.TryGetValue(t, out var bb)) { var rs = t.GetComponentsInChildren<Renderer>(); bb = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero); foreach (var q in rs) bb.Encapsulate(q.bounds); cacheB[t] = bb; } return bb; }
        foreach (var r in todos)
        {
            if (r is ParticleSystemRenderer || r.GetComponent<Terrain>() != null) continue; var b0 = r.bounds; if (b0.center.y < -500f || b0.size.x > 20f || b0.size.z > 20f) continue;
            var raizT = r.transform.root; if (raizesProtegidas.Contains(raizT.name) || r.transform.GetComponentsInParent<Transform>().Any(t => subProtegidos.Contains(t.name))) continue;
            var topo = r.transform;
            while (topo.parent != null) { var bp = BoundsDe(topo.parent); if (bp.size.x < 20f && bp.size.z < 20f && bp.size.y < 40f) topo = topo.parent; else break; }
            if (!feitos.Add(topo)) continue;
            var c = BoundsDe(topo).center; float d = Delta(c.x, c.z); if (Mathf.Abs(d) < 0.005f) continue;
            topo.position += Vector3.up * d; movidos++;
        }
        Physics.SyncTransforms();
        Debug.Log($"[Ambientacao] relevo no mapa todo: {n} pontos, variacao media {(n > 0 ? soma / n : 0):0.0} m, maxima {mx:0.0} m; {movidos} objetos reassentados; protegidos: {Enumerable.Range(0, G).Sum(j => Enumerable.Range(0, G).Count(i => prot[j, i]))}/{G * G} celulas");
    }

    // 0 = vazio, 1 = bem ocupado (objetos + arvores numa janela de ~120 m)
    static float[,] ocupLocal; static int ocupG;
    static float OcupacaoLocal(float x, float z)
    {
        if (ocupLocal == null)
        {
            ocupG = 50; float W = dados.size.x; var g = new float[ocupG, ocupG];
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) { var b = r.bounds; if (b.center.y < -500f || r is ParticleSystemRenderer || b.size.x > 60f) continue; g[Mathf.Clamp((int)(b.center.z / W * ocupG), 0, ocupG - 1), Mathf.Clamp((int)(b.center.x / W * ocupG), 0, ocupG - 1)] += 1f; }
            foreach (var ti in dados.treeInstances) g[Mathf.Clamp((int)(ti.position.z * ocupG), 0, ocupG - 1), Mathf.Clamp((int)(ti.position.x * ocupG), 0, ocupG - 1)] += 1f;
            ocupLocal = Blur(Blur(g, ocupG, ocupG, 3), ocupG, ocupG, 3);
        }
        float v = ocupLocal[Mathf.Clamp((int)(z / dados.size.z * ocupG), 0, ocupG - 1), Mathf.Clamp((int)(x / dados.size.x * ocupG), 0, ocupG - 1)];
        return Mathf.Clamp01(v / 6f);
    }


    // ------------------------------------------------------------------ variedade do chao: camadas que estavam com 0% passam a aparecer em manchas
    static void PintarVariedade(int nL, int barro, Rect vila, bool temVila)
    {
        int w = dados.alphamapWidth, h = dados.alphamapHeight; var a = dados.GetAlphamaps(0, 0, w, h); float W = dados.size.x, Lz = dados.size.z; var rnd = new System.Random(7);
        void Garante(int j, int i, int k, float alvo)
        {
            float cur = a[j, i, k]; if (alvo <= cur) return; float resto = 1f - cur, novo = 1f - alvo; float f = resto > 1e-4f ? novo / resto : 0f;
            for (int q = 0; q < nL; q++) a[j, i, q] = q == k ? alvo : a[j, i, q] * f;
        }
        float SS(float a0, float a1, float v) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - a0) / (a1 - a0))); }
        int contados = 0;
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            float x = i * W / w, z = j * Lz / h; if (temVila && vila.Contains(new Vector2(x, z))) continue;
            float via = a[j, i, 6] + a[j, i, 8] + a[j, i, barro] + a[j, i, 7]; if (via > 0.2f) continue;
            float campo = a[j, i, 9] + a[j, i, 10], mata = a[j, i, 11] + a[j, i, 12] + a[j, i, 13], seco = a[j, i, 14];
            float n1 = Mathf.PerlinNoise(x / 38f + 21f, z / 38f + 5f), n2 = Mathf.PerlinNoise(x / 17f + 77f, z / 17f + 31f), n3 = Mathf.PerlinNoise(x / 60f + 140f, z / 60f + 9f);
            float decl = dados.GetSteepness(x / W, z / Lz);
            if (decl > 27f) { Garante(j, i, 4, 0.85f); contados++; continue; }
            if (decl > 14f && n3 > 0.48f) { Garante(j, i, 7, 0.65f * SS(0.48f, 0.62f, n3)); contados++; }
            if (mata > 0.4f && n1 > 0.56f) { Garante(j, i, 1, 0.8f * SS(0.56f, 0.74f, n1)); contados++; }
            if (mata > 0.3f && decl < 6f && n2 > 0.66f) { Garante(j, i, 2, 0.7f * SS(0.66f, 0.8f, n2)); contados++; }
            if (campo > 0.4f && n1 > 0.62f) { Garante(j, i, 5, 0.75f * SS(0.62f, 0.78f, n1)); contados++; }
            if ((campo > 0.3f || seco > 0.3f) && n2 > 0.64f) { Garante(j, i, 0, 0.7f * SS(0.64f, 0.8f, n2)); contados++; }
            // beira do lago: areia escura e lama
            if (lagoWL > -900f)
            {
                float hh = dados.GetInterpolatedHeight(x / W, z / Lz); float r = (new Vector2(x, z) - lagoC).magnitude;
                if (r < 100f && hh > lagoWL - 1.5f && hh < lagoWL + 1.1f) { Garante(j, i, 3, 0.85f); contados++; }
                else if (r < 100f && hh >= lagoWL + 1.1f && hh < lagoWL + 2.6f && n2 > 0.4f) { Garante(j, i, 2, 0.7f); contados++; }
            }
        }
        dados.SetAlphamaps(0, 0, a);
        Debug.Log($"[Ambientacao] variedade do chao: {contados} pontos pintados em camadas que estavam sem uso");
    }

    // ------------------------------------------------------------------ lago: agua turva do pantano + juncos e vitorias-regias
    static void CriarLago(Transform raiz)
    {
        if (lagoWL < -900f) return;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var shAgua = Shader.Find("Pinglu/AguaPantano");
        var agua = Mat("Lago_Agua", shAgua != null ? shAgua : sh, m =>
        {
            if (shAgua == null) { m.SetColor("_BaseColor", new Color(0.07f, 0.12f, 0.09f, 0.9f)); return; }
            var nt = AssetImporter.GetAtPath($"{PASTA}/Texturas/agua_normal.png") as TextureImporter; if (nt != null && nt.textureType != TextureImporterType.NormalMap) { nt.textureType = TextureImporterType.NormalMap; nt.SaveAndReimport(); }
            m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PASTA}/Texturas/agua_normal.png"));
            m.SetColor("_ShallowColor", new Color(0.24f, 0.27f, 0.12f, 0.92f)); m.SetColor("_DeepColor", new Color(0.07f, 0.09f, 0.045f, 0.995f)); m.SetFloat("_DepthDistance", 1.1f);
            m.SetFloat("_NormalScale", 0.05f); m.SetFloat("_FoamWidth", 0.25f); m.SetColor("_FoamColor", new Color(0.42f, 0.45f, 0.26f, 1f)); m.SetFloat("_Smoothness", 40f);
        });
        var cam = Camera.main; if (cam != null) { var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (cd != null) cd.requiresDepthTexture = true; }
        var pai = new GameObject("Lago do Pantano").transform; pai.SetParent(raiz);
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>(); const float cel = 2f;
        for (float x = lagoC.x - 100f; x < lagoC.x + 100f; x += cel) for (float z = lagoC.y - 100f; z < lagoC.y + 100f; z += cel)
        {
            bool baixo = false; foreach (var o in new[] { (0f, 0f), (cel, 0f), (0f, cel), (cel, cel), (cel / 2, cel / 2) }) if (terreno.SampleHeight(new Vector3(x + o.Item1, 0, z + o.Item2)) < lagoWL) { baixo = true; break; }
            if (!baixo) continue; int b = vs.Count; vs.Add(new Vector3(x, lagoWL, z)); vs.Add(new Vector3(x + cel, lagoWL, z)); vs.Add(new Vector3(x + cel, lagoWL, z + cel)); vs.Add(new Vector3(x, lagoWL, z + cel));
            uvs.Add(new Vector2(x, z) * 0.1f); uvs.Add(new Vector2(x + cel, z) * 0.1f); uvs.Add(new Vector2(x + cel, z + cel) * 0.1f); uvs.Add(new Vector2(x, z + cel) * 0.1f); tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }
        if (vs.Count == 0) return;
        var me = new Mesh { name = "LagoAgua", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; me.SetVertices(vs); me.SetUVs(0, uvs); me.SetTriangles(tris, 0); me.RecalculateNormals(); me.RecalculateBounds();
        Directory.CreateDirectory($"{PASTA}/Vila"); AssetDatabase.DeleteAsset($"{PASTA}/Vila/LagoAgua.asset"); AssetDatabase.CreateAsset(me, $"{PASTA}/Vila/LagoAgua.asset");
        var go = new GameObject("Agua"); go.transform.SetParent(pai); go.AddComponent<MeshFilter>().sharedMesh = me; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = agua; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // juncos na beira e vitorias-regias na agua rasa
        Material Verde(string nome, Color c) => Mat(nome, sh, m => { m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.1f); m.SetFloat("_Cull", 0f); });
        var mJ = Verde("Lago_Junco", new Color(0.20f, 0.27f, 0.11f)); var mV = Verde("Lago_Vitoria", new Color(0.12f, 0.26f, 0.10f));
        var rnd = new System.Random(606); int nj = 0, nv = 0;
        for (int t = 0; t < 20000 && (nj < 220 || nv < 90); t++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, rr = 12f + (float)rnd.NextDouble() * 82f; float x = lagoC.x + Mathf.Cos(ang) * rr, z = lagoC.y + Mathf.Sin(ang) * rr;
            float y = terreno.SampleHeight(new Vector3(x, 0, z)); bool junco = y > lagoWL - 0.35f && y < lagoWL + 0.55f;
            if (!junco && !(y < lagoWL - 0.4f && y > lagoWL - 2.2f)) continue;
            if (junco && nj >= 220) continue; if (!junco && nv >= 90) continue;
            if (Physics.OverlapSphere(new Vector3(x, y + 0.5f, z), 0.4f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider))) continue;
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(junco ? $"{PASTA}/Lago/Junco_{(rnd.Next(2) == 0 ? "A" : "B")}.fbx" : $"{PASTA}/Lago/Vitoria_{(rnd.Next(2) == 0 ? "A" : "B")}.fbx"); if (pf == null) continue;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf); g.transform.SetParent(pai); g.transform.position = new Vector3(x, junco ? y - 0.05f : lagoWL + 0.02f, z); g.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
            g.transform.localScale = Vector3.one * (junco ? Mathf.Lerp(0.9f, 1.7f, (float)rnd.NextDouble()) : Mathf.Lerp(0.8f, 1.4f, (float)rnd.NextDouble()));
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { var ms = r.sharedMaterials; for (int m = 0; m < ms.Length; m++) ms[m] = junco ? mJ : mV; r.sharedMaterials = ms; }
            if (junco) nj++; else nv++;
        }
        aguas.Add(new Bounds(new Vector3(lagoC.x, lagoWL, lagoC.y), new Vector3(190f, 50f, 190f)));
        Debug.Log($"[Ambientacao] lago: nivel {lagoWL:0.0} m, {vs.Count / 4} celulas de agua, {nj} juncos, {nv} vitorias-regias");
    }

    // ------------------------------------------------------------------ construcoes com cara de antigas (so nesta cena; os materiais originais nao sao alterados)
    static Material MatAntigo(string nome, string pasta, Color tint, float tile, float bump, float parallax)
    {
        string b = $"{PASTA}/Texturas/Chao/{pasta}/{pasta}_1K-JPG";
        var nm = AssetImporter.GetAtPath(b + "_NormalGL.jpg") as TextureImporter; if (nm != null && nm.textureType != TextureImporterType.NormalMap) { nm.textureType = TextureImporterType.NormalMap; nm.SaveAndReimport(); }
        return Mat(nome, Shader.Find("Universal Render Pipeline/Lit"), m =>
        {
            var sc = new Vector2(tile, tile);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(b + "_Color.jpg")); m.SetTextureScale("_BaseMap", sc); m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(b + "_NormalGL.jpg")); m.SetTextureScale("_BumpMap", sc); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(b + "_AmbientOcclusion.jpg"); if (ao) { m.SetTexture("_OcclusionMap", ao); m.SetTextureScale("_OcclusionMap", sc); m.SetFloat("_OcclusionStrength", 1f); }
            var dp = AssetDatabase.LoadAssetAtPath<Texture2D>(b + "_Displacement.jpg"); if (dp && parallax > 0) { m.SetTexture("_ParallaxMap", dp); m.SetTextureScale("_ParallaxMap", sc); m.SetFloat("_Parallax", parallax); m.EnableKeyword("_PARALLAXMAP"); }
            m.SetFloat("_Smoothness", 0.04f); m.SetFloat("_Metallic", 0f);
        });
    }

    static void EnvelhecerConstrucoes()
    {
        var troca = new Dictionary<string, Material>
        {
            ["Pedra_Blocos"] = MatAntigo("Vila_PedraVelha", "Rock064", new Color(1.0f, 1.0f, 1.05f), 0.33f, 1.8f, 0.03f),
            ["Pedra_Rustica"] = MatAntigo("Vila_PedraMusgo", "Rocks001", new Color(0.72f, 0.74f, 0.80f), 0.45f, 1.6f, 0.03f),
            ["Reboco_Creme"] = MatAntigo("Vila_RebocoVelho", "Plaster001", new Color(0.80f, 0.72f, 0.60f), 0.45f, 1.2f, 0f),
        };
        AssetDatabase.SaveAssets(); int n = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.transform.root.name.StartsWith("Vila_Gerada") && !r.transform.root.name.StartsWith("Mundo_Gerado")) continue;
            var ms = r.sharedMaterials; bool mud = false; for (int i = 0; i < ms.Length; i++) if (ms[i] != null && troca.TryGetValue(ms[i].name, out var nv)) { ms[i] = nv; mud = true; }
            if (mud) { r.sharedMaterials = ms; n++; }
        }
        Debug.Log($"[Ambientacao] construcoes envelhecidas: {n} renderers com pedra/reboco novos");
    }

    // ------------------------------------------------------------------ plantas do kit (arbustos, samambaias, flores, cogumelos) nas areas vazias
    static void Plantas(Transform raiz)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        Material Folha(string nome, string tex, Color tint) => Mat("Kit_" + nome, sh, m => { var t = Tex(tex); var pth = $"{PASTA}/Texturas/{tex}.png"; var ti = AssetImporter.GetAtPath(pth) as TextureImporter; if (ti != null && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ti.SaveAndReimport(); } m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(pth)); m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", 0.05f); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0f); m.renderQueue = 2450; });
        var dic = new Dictionary<string, Material>
        {
            ["Leaves"] = Folha("Plantas", "Leaves", new Color(0.62f, 0.78f, 0.52f)), ["Leaves_TwistedTree"] = Folha("ArbustoB", "Leaves_TwistedTree_C", new Color(0.62f, 0.74f, 0.46f)), ["Leaves_NormalTree"] = Folha("Arbusto", "Leaves_NormalTree_C", new Color(0.66f, 0.80f, 0.52f)),
            ["Flowers"] = Folha("Flores", "Flowers", new Color(0.85f, 0.82f, 0.8f)),
            ["Mushrooms"] = Mat("Kit_Cogumelos", sh, m => { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PASTA}/Texturas/Mushrooms.png")); m.SetColor("_BaseColor", new Color(0.8f, 0.78f, 0.74f)); m.SetFloat("_Smoothness", 0.1f); })
        };
        AssetDatabase.SaveAssets();
        var rnd = new System.Random(8080); float W = dados.size.x, L = dados.size.z; int total = 0;
        // nome(s), alvo de altura, quantidade, camadas (peso) , exige vazio
        var grupos = new (string[] nomes, float h0, float h1, int qtd, Dictionary<string, float> cam, float espaco)[]
        {
            (new[] { "Bush_Common", "Bush_Common_Flowers" }, 0.9f, 1.6f, 260, new() { {"Campo", 1f}, {"CampoVivo", 1f}, {"Grass_Moss", 0.6f}, {"Heather", 0.8f}, {"Mata", 0.3f} }, 6f),
            (new[] { "Fern_1", "Plant_1", "Plant_1_Big", "Plant_7", "Plant_7_Big" }, 0.55f, 1.1f, 380, new() { {"Mata", 1f}, {"MataSombria", 0.9f}, {"MusgoSombrio", 1f}, {"Grass_Moss", 0.6f}, {"CampoVivo", 0.35f}, {"FolhasChao", 0.8f} }, 2.5f),
            (new[] { "Flower_3_Group", "Flower_4_Group", "Flower_3_Single", "Flower_4_Single" }, 0.35f, 0.65f, 260, new() { {"Campo", 1f}, {"CampoVivo", 1f}, {"Heather", 0.8f} }, 3f),
            (new[] { "Mushroom_Common", "Mushroom_Laetiporus" }, 0.2f, 0.4f, 160, new() { {"MataSombria", 1f}, {"Mata", 1f}, {"MusgoSombrio", 1f}, {"FolhasChao", 1f} }, 3f),
        };
        var pai = new GameObject("Plantas do kit").transform; pai.SetParent(raiz); var usados = new List<Vector2>();
        foreach (var g in grupos)
        {
            int feitos = 0;
            for (int tent = 0; tent < g.qtd * 200 && feitos < g.qtd; tent++)
            {
                float x = 12 + (float)rnd.NextDouble() * (W - 24), z = 12 + (float)rnd.NextDouble() * (L - 24);
                float vz = Vazio(x, z); if (vz < 0.35f || (float)rnd.NextDouble() > vz) continue;
                int i = Mathf.Clamp((int)(x / W * (aw - 1)), 0, aw - 1), j = Mathf.Clamp((int)(z / L * (ah - 1)), 0, ah - 1);
                float p = 0; foreach (var kv in g.cam) p += PesoCamada(i, j, kv.Key) * kv.Value;
                if (rnd.NextDouble() > Mathf.Min(1f, p)) continue;
                if (PesoCamada(i, j, "Barro") > 0.05f || PesoCamada(i, j, "Caminho_Soil_Rocks") > 0.05f || Mascarado(x, z)) continue;
                if (dados.GetSteepness(x / W, z / L) > 32f) continue;
                float y = terreno.SampleHeight(new Vector3(x, 0, z)) + terreno.transform.position.y; var pos = new Vector3(x, y, z);
                if (aguas.Any(b => b.Contains(pos))) continue;
                if (usados.Any(u => (u.x - x) * (u.x - x) + (u.y - z) * (u.y - z) < g.espaco * g.espaco)) continue;
                if (Physics.CheckSphere(pos + Vector3.up * 0.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore) && Physics.OverlapSphere(pos + Vector3.up * 0.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider))) continue;
                usados.Add(new Vector2(x, z));
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{PASTA}/Plantas/{g.nomes[rnd.Next(g.nomes.Length)]}.fbx"); if (pf == null) break;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf); go.transform.SetParent(pai); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { var ms = r.sharedMaterials; for (int m = 0; m < ms.Length; m++) if (ms[m] != null && dic.TryGetValue(ms[m].name, out var nv)) ms[m] = nv; r.sharedMaterials = ms; }
                var rs = go.GetComponentsInChildren<Renderer>(); var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                go.transform.localScale = Vector3.one * (Mathf.Lerp(g.h0, g.h1, (float)rnd.NextDouble()) / Mathf.Max(0.01f, bb.size.y)); go.isStatic = true; feitos++; total++;
            }
        }
        Debug.Log($"[Ambientacao] plantas do kit: {total}");
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
        var mRocha = Mat("Kit_Rochas", shLit, m => { m.SetTexture("_BaseMap", Tex("Rocks_Diffuse")); m.SetColor("_BaseColor", new Color(1.55f, 1.5f, 1.4f)); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f); });
        var mTrilha = Mat("Kit_PedrasTrilha", shLit, m => { m.SetTexture("_BaseMap", Tex("PathRocks_Diffuse")); m.SetColor("_BaseColor", new Color(1.4f, 1.38f, 1.3f)); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f); });
        AssetDatabase.SaveAssets();
        string[] N(string pre, int n) => Enumerable.Range(1, n).Select(i => $"{pre}_{i}").ToArray();
        var tipos = new List<Tipo>
        {
            new Tipo { nomes = N("Rock_Medium", 3), hMin = 0.9f, hMax = 2.8f, colisor = true, grande = true, qtd = 320, espaco = 14, declMax = 30,
                camadas = new() { {"Rocha", 1f}, {"Campo", 0.22f}, {"CampoVivo", 0.2f}, {"Grass_Dry", 0.5f}, {"CampoSeco", 0.55f}, {"Heather", 0.5f}, {"Mata", 0.3f}, {"MataSombria", 0.35f}, {"TerraMorta", 0.5f}, {"MusgoSombrio", 0.5f}, {"Grass_Moss", 0.35f} } },
            new Tipo { nomes = N("Pebble_Round", 5).Concat(N("Pebble_Square", 6)).ToArray(), hMin = 0.16f, hMax = 0.42f, qtd = 900, declMax = 55,
                camadas = new() { {"Rocha", 1f}, {"Pebbles_A_TerrainLayer", 1f}, {"Campo", 0.35f}, {"CampoVivo", 0.3f}, {"Grass_Dry", 0.6f}, {"CampoSeco", 0.7f}, {"Heather", 0.5f}, {"Mata", 0.3f}, {"MataSombria", 0.3f}, {"TerraMorta", 0.5f}, {"MusgoSombrio", 0.4f}, {"Barro", 0.5f} } },
            new Tipo { nomes = new[] { "RockPath_Round_Wide", "RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3", "RockPath_Square_Wide", "RockPath_Square_Small_1", "RockPath_Square_Small_2", "RockPath_Square_Small_3" },
                trilha = true, hMin = 1.5f, hMax = 2.6f, qtd = 900, declMax = 30, espaco = 3.2f, camadas = new() { {"Barro", 1f} } },
            new Tipo { nomes = N("Rock_Medium", 3), hMin = 1.1f, hMax = 3.2f, colisor = true, grande = true, beira = true, qtd = 260, espaco = 7f, declMax = 28, camadas = new() { {"Barro", 1f} } },
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
                if (tp.grande && !tp.beira) p = p * 0.6f + 0.5f * Vazio(x, z);
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
                go.transform.position = pos; go.transform.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, nrm), tp.trilha ? 1f : 0.25f) * Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                var rs = go.GetComponentsInChildren<Renderer>(); var mat = tp.trilha ? mTrilha : mRocha;
                foreach (var r in rs) { var ms = r.sharedMaterials; for (int m = 0; m < ms.Length; m++) ms[m] = mat; r.sharedMaterials = ms; }
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                float esc;
                if (tp.trilha) { float larg = Mathf.Max(bb.size.x, bb.size.z); esc = Mathf.Clamp(h / Mathf.Max(0.01f, larg), 0.1f, 50f); }
                else esc = h / Mathf.Max(0.01f, bb.size.y);
                go.transform.localScale = Vector3.one * esc;
                float raioP = Mathf.Max(bb.size.x, bb.size.z) * esc * 0.45f, yMin = pos.y;
                for (int q = 0; q < 8; q++) { float aq = q * Mathf.PI / 4f; yMin = Mathf.Min(yMin, terreno.SampleHeight(new Vector3(pos.x + Mathf.Cos(aq) * raioP, 0, pos.z + Mathf.Sin(aq) * raioP)) + terreno.transform.position.y); }
                go.transform.position = new Vector3(pos.x, yMin - (tp.grande ? h * 0.10f : 0.03f), pos.z);
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
