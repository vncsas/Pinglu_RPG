// =====================================================================
//  PingluVegetacao.cs  -  vegetação e ambientação do mapa inteiro
// ---------------------------------------------------------------------
//  Usa os pacotes de "Assets/Ambientação e Vegetação":
//   - converte materiais Standard / HDRP para URP (cópias em Assets/Cenario/Vegetacao)
//   - FLORESTA SOMBRIA: o contorno vermelho que você marcou no mapa (objeto
//     "Limite da Floresta Sombria"). Dentro: árvores sinistras + árvores secas + plantas estranhas.
//   - RESTO DO MAPA: floresta viva e variada (carvalhos, plátanos, pinheiros, coníferas, thujas...),
//     campos com capim e flores, arbustos, pedras e animais (vacas, estorninhos, esquilos, javalis)
//
//  COMO USAR:  Pinglu > 43. Vegetação do mapa   (Play desligado; depois Ctrl+S)
//  Para voltar: Pinglu > 44. Desfazer vegetação
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PingluVegetacao
{
    const string P = "Assets/Ambientação e Vegetação/";
    const string PASTA = "Assets/Cenario/Vegetacao";
    const string PASTA_PREFABS = PASTA + "/Prefabs";
    const string PASTA_MATS = PASTA + "/Materiais";
    const string PASTA_MALHAS = PASTA + "/Malhas";
    const string BACKUP = PASTA + "/Backup_Terreno_Vegetacao.asset";
    const string NOME_RAIZ = "Vegetacao_Gerada";
    const string NOME_LIMITE = "Limite da Floresta Sombria";
    const string CAMADAS_TS = "Assets/TerrainSampleAssets/TerrainLayers/";

    enum Tipo { Arvore, ArvoreSeca, Arbusto, Planta, Pedra, Grama, Flor }

    class Esp
    {
        public string id, caminho, grupo;
        public Tipo tipo;
        public float peso = 1f;
        public GameObject prefab;
        public float raio = 1f, altura = 1f;
        public int indice = -1;      // índice do prototype no terreno (árvores) ou do detalhe (grama/flor)
    }

    static Terrain terreno; static TerrainData dados;
    static System.Random rng;
    static List<string> avisos = new List<string>();
    static List<string> relatorio = new List<string>();
    static LimiteDaFloresta limite;
    static List<Vector4> zonas = new List<Vector4>();            // x, z, raio
    static bool[,] estrada; const float CEL_EST = 4f;            // grade de estradas (4 m)
    static Vector3 cemiterio;
    static Dictionary<string, Material> cacheMat = new Dictionary<string, Material>();

    static float Faixa(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static float Altura(float x, float z) { return terreno.SampleHeight(new Vector3(x, 0f, z)) + terreno.transform.position.y; }

    // ------------------------------------------------------------------
    //  MENUS
    // ------------------------------------------------------------------
    [MenuItem("Pinglu/43. Vegetação do mapa")]
    public static void Construir() { Executar(true); }

    // refaz a vegetação sem perguntar nada (usado depois que as estradas, o cemitério ou o castelo são refeitos,
    // porque eles devolvem o terreno a um backup antigo que não tem a vegetação)
    public static void RefazerSilencioso() { Executar(false); }
    public static bool JaFoiCriada() { return GameObject.Find(NOME_RAIZ) != null || Object.FindFirstObjectByType<LimiteDaFloresta>() != null; }

    static bool TemVegNoTerreno()
    {
        foreach (TreePrototype t in dados.treePrototypes) if (t.prefab != null && t.prefab.name.StartsWith("Veg_")) return true;
        foreach (DetailPrototype d in dados.detailPrototypes) if (d.prototype != null && d.prototype.name.StartsWith("Veg_")) return true;
        return false;
    }

    static void Executar(bool interativo)
    {
        if (EditorApplication.isPlaying) { if (interativo) EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) { if (interativo) EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;
        if (GameObject.Find(NOME_RAIZ) != null)
        {
            if (interativo && !EditorUtility.DisplayDialog("Pinglu", "A vegetação já foi criada. Quer refazer?", "Sim", "Cancelar")) return;
            Undo.DestroyObjectImmediate(GameObject.Find(NOME_RAIZ));   // o terreno não volta ao backup: a vegetação é refeita por cima do que existe agora
        }
        if (interativo && !EditorUtility.DisplayDialog("Pinglu - Vegetação",
            "Vou:\n- tirar as árvores antigas repetidas do mapa\n- encher o contorno vermelho (Floresta Sombria) com árvores sinistras, secas e plantas estranhas\n" +
            "- plantar o resto do mapa com os seus pacotes (árvores variadas, capim, flores, arbustos, pedras)\n- colocar animais (vacas, pássaros, esquilos, javalis)\n\n" +
            "Guardo um backup do terreno antes. Pode demorar vários minutos.", "Pode fazer", "Cancelar")) return;

        avisos.Clear(); relatorio.Clear(); cacheMat.Clear(); zonas.Clear();
        rng = new System.Random(4321);
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando pastas e backup...", 0.02f);
            Pastas();
            if (!TemVegNoTerreno() || AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP) == null)   // o backup é de ANTES da vegetação
            {
                if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP) != null) AssetDatabase.DeleteAsset(BACKUP);
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dados), BACKUP)) throw new System.Exception("Não consegui fazer backup do terreno.");
            }
            Undo.RegisterCompleteObjectUndo(dados, "Vegetação");
            GameObject raizGo = new GameObject(NOME_RAIZ);
            Undo.RegisterCreatedObjectUndo(raizGo, "Vegetação");

            EditorUtility.DisplayProgressBar("Pinglu", "Contorno da Floresta Sombria...", 0.05f);
            limite = AcharOuCriarLimite();
            MontarZonasEEstradas();

            List<Esp> cat = Catalogo();
            if (PingluTerreno.Ativo()) cat.RemoveAll(e => e.tipo == Tipo.Grama || e.tipo == Tipo.Flor);   // o capim e as flores vêm do terreno novo
            for (int i = 0; i < cat.Count; i++)
            {
                if (i % 5 == 0) EditorUtility.DisplayProgressBar("Pinglu", "Convertendo para URP: " + cat[i].id + " (" + (i + 1) + "/" + cat.Count + ")", 0.08f + 0.4f * i / cat.Count);
                Preparar(cat[i]);
            }
            int total = cat.Count;
            cat.RemoveAll(e => e.prefab == null);
            relatorio.Add("Espécies convertidas para URP: " + cat.Count + " de " + total + ".");

            EditorUtility.DisplayProgressBar("Pinglu", "Pintando o chão...", 0.5f);
            if (!PingluTerreno.Ativo()) PintarChao();      // com o terreno novo (menu 54) o chão e o capim vêm de lá
            EditorUtility.DisplayProgressBar("Pinglu", "Plantando árvores...", 0.6f);
            Arvores(cat);
            EditorUtility.DisplayProgressBar("Pinglu", "Capim e flores...", 0.8f);
            if (PingluTerreno.Ativo()) PingluTerreno.RefazerSilencioso(); else Detalhes(cat);
            EditorUtility.DisplayProgressBar("Pinglu", "Animais...", 0.92f);
            Animais(raizGo.transform);
            CriarFolhasCaindo(raizGo.transform);

            terreno.treeMaximumFullLODCount = 250;
            terreno.Flush();
            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
            Selection.activeGameObject = limite != null ? limite.gameObject : raizGo;
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console). Use Pinglu > 44. Desfazer vegetação."); }
        finally { EditorUtility.ClearProgressBar(); }

        try { relatorio.Add("Assentar: " + PingluAssentar.Executar(false).Split('\n')[0]); } catch (System.Exception ex) { Debug.LogException(ex); }
        foreach (string r in relatorio) Debug.Log("[Veg] " + r);
        foreach (string a in avisos) Debug.LogWarning("[Veg] " + a);
        if (!interativo) { Debug.Log("[Veg] vegetação refeita sem perguntas."); return; }
        string msg = "Vegetação pronta!\n\n" + string.Join("\n", relatorio) +
            "\n\nA área vermelha (Floresta Sombria) está no objeto \"" + NOME_LIMITE + "\" na Hierarchy: arraste os pontos P01, P02... na janela Scene para ajustar o contorno e rode este menu de novo.\n\nSalve com Ctrl + S.";
        if (avisos.Count > 0) msg += "\n\nAvisos (mais no Console):\n- " + string.Join("\n- ", avisos.GetRange(0, Mathf.Min(avisos.Count, 8)));
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
    }

    [MenuItem("Pinglu/44. Desfazer vegetação")]
    public static void DesfazerMenu() { Desfazer(true); }

    static void Desfazer(bool perguntar)
    {
        if (perguntar && !EditorUtility.DisplayDialog("Pinglu", "Remover a vegetação nova e voltar árvores, capim e chão como estavam antes do menu 43?", "Sim", "Cancelar")) return;
        terreno = Terrain.activeTerrain;
        GameObject r = GameObject.Find(NOME_RAIZ);
        if (r != null) Undo.DestroyObjectImmediate(r);
        TerrainData bk = AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP);
        if (terreno != null && bk != null)
        {
            TerrainData td = terreno.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Desfazer vegetação");
            td.terrainLayers = bk.terrainLayers;
            int ar = bk.alphamapResolution;
            td.SetAlphamaps(0, 0, bk.GetAlphamaps(0, 0, ar, ar));
            td.treePrototypes = bk.treePrototypes;
            td.SetTreeInstances(bk.treeInstances, true);
            td.detailPrototypes = bk.detailPrototypes;
            int dr = bk.detailResolution;
            for (int i = 0; i < bk.detailPrototypes.Length; i++) td.SetDetailLayer(0, 0, i, bk.GetDetailLayer(0, 0, dr, dr, i));
            td.RefreshPrototypes();
            terreno.Flush();
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        }
        if (perguntar) EditorUtility.DisplayDialog("Pinglu", "Vegetação removida. Salve a cena com Ctrl + S.", "OK");
    }

    static void Pastas()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Cenario")) AssetDatabase.CreateFolder("Assets", "Cenario");
        foreach (string n in new[] { "Vegetacao", "Vegetacao/Prefabs", "Vegetacao/Materiais", "Vegetacao/Malhas" })
        {
            string pai = "Assets/Cenario" + (n.Contains("/") ? "/" + n.Substring(0, n.IndexOf('/')) : ""), nome = n.Contains("/") ? n.Substring(n.IndexOf('/') + 1) : n;
            if (!AssetDatabase.IsValidFolder(pai + "/" + nome)) AssetDatabase.CreateFolder(pai, nome);
        }
    }

    // ------------------------------------------------------------------
    //  FOLHAS: textura feita por código, folhas caindo no ar e folhas no chão
    // ------------------------------------------------------------------
    static Texture2D TexturaDeFolha()
    {
        string caminho = PASTA + "/Folha.png";
        Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        if (t != null) return t;
        const int N = 64;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x / (float)(N - 1) - 0.5f) * 2f, v = (y / (float)(N - 1) - 0.5f) * 2f;     // -1..1
                float larg = 0.52f * Mathf.Sin((v * 0.5f + 0.5f) * Mathf.PI);                           // folha com pontas
                bool dentro = Mathf.Abs(u) < larg && Mathf.Abs(v) < 0.95f;
                float nervura = Mathf.Abs(u) < 0.045f ? 0.72f : 1f;
                float cor = dentro ? nervura * (0.88f + 0.12f * Mathf.Abs(Mathf.Sin(v * 9f + Mathf.Abs(u) * 14f))) : 1f;
                tex.SetPixel(x, y, new Color(cor, cor, cor, dentro ? 1f : 0f));
            }
        tex.Apply();
        System.IO.File.WriteAllBytes(caminho, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(caminho);
        TextureImporter ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (ti != null) { ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
    }

    static Material MaterialDeFolha(string nome, string shader, Color cor)
    {
        string caminho = PASTA_MATS + "/" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, caminho); }
        Texture2D t = TexturaDeFolha();
        m.SetTexture("_BaseMap", t); m.mainTexture = t;
        m.SetColor("_BaseColor", cor);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
        if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        m.renderQueue = 2450;
        EditorUtility.SetDirty(m);
        return m;
    }

    // sistema de partículas que acompanha o jogador (ver FolhasCaindo.cs)
    static void CriarFolhasCaindo(Transform pai)
    {
        Material mat = MaterialDeFolha("Folha_Particula", "Universal Render Pipeline/Particles/Unlit", Color.white);
        GameObject go = new GameObject("Folhas Caindo");
        go.transform.SetParent(pai, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true;
        main.startLifetime = 16f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.06f; main.maxParticles = 700;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.45f, 0.62f, 0.18f), 0f), new GradientColorKey(new Color(0.95f, 0.75f, 0.2f), 0.4f), new GradientColorKey(new Color(0.9f, 0.42f, 0.12f), 0.7f), new GradientColorKey(new Color(0.75f, 0.18f, 0.08f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(g);
        var em = ps.emission; em.rateOverTime = 22f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(70f, 1f, 70f);
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1.1f; lim.dampen = 0.25f;
        var nz = ps.noise; nz.enabled = true; nz.strength = 1.1f; nz.frequency = 0.25f; nz.scrollSpeed = 0.3f; nz.damping = true;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat; rend.renderMode = ParticleSystemRenderMode.Billboard; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<FolhasCaindo>();
    }

    // folhas caídas no chão: um punhado de folhas planas, usado como "detalhe" do terreno (laranja e vermelha)
    public static GameObject[] PrefabsFolhasNoChaoPublico() { return PrefabsFolhasNoChao(); }
    static GameObject[] PrefabsFolhasNoChao()
    {
        GameObject[] res = new GameObject[2];
        Color[] cores = { new Color(0.92f, 0.5f, 0.12f), new Color(0.75f, 0.2f, 0.08f) };
        string[] nomes = { "Veg_FolhasChao_laranja", "Veg_FolhasChao_vermelha" };
        for (int v = 0; v < 2; v++)
        {
            string pf = PASTA_PREFABS + "/" + nomes[v] + ".prefab";
            GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(pf);
            if (existente != null) { res[v] = existente; continue; }
            Mesh mesh = new Mesh { name = nomes[v] };
            System.Collections.Generic.List<Vector3> vs = new System.Collections.Generic.List<Vector3>(); System.Collections.Generic.List<Vector2> uv = new System.Collections.Generic.List<Vector2>(); System.Collections.Generic.List<int> tr = new System.Collections.Generic.List<int>();
            System.Random r = new System.Random(500 + v);
            for (int k = 0; k < 12; k++)
            {
                float cx = ((float)r.NextDouble() - 0.5f) * 1.5f, cz = ((float)r.NextDouble() - 0.5f) * 1.5f, yaw = (float)r.NextDouble() * 6.28f, sz = 0.11f + (float)r.NextDouble() * 0.1f, y = 0.02f + (float)r.NextDouble() * 0.04f;
                Quaternion q = Quaternion.Euler(((float)r.NextDouble() - 0.5f) * 25f, yaw * Mathf.Rad2Deg, ((float)r.NextDouble() - 0.5f) * 25f);
                int b = vs.Count;
                vs.Add(new Vector3(cx, y, cz) + q * new Vector3(-sz, 0f, -sz)); vs.Add(new Vector3(cx, y, cz) + q * new Vector3(-sz, 0f, sz));
                vs.Add(new Vector3(cx, y, cz) + q * new Vector3(sz, 0f, sz)); vs.Add(new Vector3(cx, y, cz) + q * new Vector3(sz, 0f, -sz));
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                tr.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            mesh.SetVertices(vs); mesh.SetUVs(0, uv); mesh.SetTriangles(tr, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, PASTA_MALHAS + "/" + nomes[v] + ".asset");
            GameObject go = new GameObject(nomes[v]);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialDeFolha("Folha_Chao_" + (v == 0 ? "laranja" : "vermelha"), "Universal Render Pipeline/Lit", cores[v]);
            res[v] = PrefabUtility.SaveAsPrefabAsset(go, pf);
            Object.DestroyImmediate(go);
        }
        return res;
    }

    // ------------------------------------------------------------------
    //  CATÁLOGO: o que vem de cada pacote
    // ------------------------------------------------------------------
    static void Add(List<Esp> l, string id, string caminho, Tipo tipo, string grupo, float peso = 1f)
    {
        l.Add(new Esp { id = id, caminho = caminho, tipo = tipo, grupo = grupo, peso = peso });
    }

    static List<Esp> Catalogo()
    {
        List<Esp> l = new List<Esp>();
        string zns = P + "ZNS3D/";
        // árvores vivas
        Add(l, "plane", zns + "PLANE_TREE/PREFABS/Plane_Tree.prefab", Tipo.Arvore, "folhosa", 1.2f);
        Add(l, "oak", zns + "FREE_VEGETATION_PACK/PREFABS/Oak_Tree.prefab", Tipo.Arvore, "folhosa", 1.5f);
        Add(l, "dreamtree", P + "DreamTree2/Prefab/DreamTree.prefab", Tipo.Arvore, "folhosa", 1f);
        Add(l, "pine_zns", zns + "FREE_VEGETATION_PACK/PREFABS/Pine_Tree.prefab", Tipo.Arvore, "conifera", 1f);
        for (int i = 1; i <= 6; i++) Add(l, "pinho" + i, P + "Hipernt/Pine Pack/Prefabs/Pine" + i + ".prefab", Tipo.Arvore, "conifera", 1f);
        string conif = P + "Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer ";
        Add(l, "conifera_alta", conif + "Tall BOTD URP.prefab", Tipo.Arvore, "conifera", 1.3f);
        Add(l, "conifera_media", conif + "Medium BOTD URP.prefab", Tipo.Arvore, "conifera", 1.3f);
        Add(l, "conifera_pequena", conif + "Small BOTD URP.prefab", Tipo.Arvore, "conifera", 0.8f);
        Add(l, "conifera_seca", conif + "Bare BOTD URP.prefab", Tipo.ArvoreSeca, "seca", 1f);
        for (int i = 1; i <= 20; i++) Add(l, "thuja" + i, P + "Thuja Plants Nature Asset/Prefabs/thuja " + i + ".prefab", Tipo.Arbusto, "thuja", 0.5f);
        // árvores secas
        foreach (string n in new[] { "Dry7509", "Dry4910", "Dry5818", "Dry3333", "Dry8872", "Dry4195", "Dry6524" })
            Add(l, "seca_" + n, P + "Dry_Trees/Prefab/" + n + ".prefab", Tipo.ArvoreSeca, "seca", 1f);
        // arbustos, plantas e pedras
        Add(l, "arbusto_zns", zns + "FREE_VEGETATION_PACK/PREFABS/Shrub.prefab", Tipo.Arbusto, "arbusto", 1f);
        Add(l, "planta1", P + "Hipernt/Pine Pack/Prefabs/Plant1.prefab", Tipo.Planta, "planta", 1f);
        Add(l, "planta2", P + "Hipernt/Pine Pack/Prefabs/Plant2.prefab", Tipo.Planta, "planta", 1f);
        Add(l, "planta_flor", P + "Hipernt/Pine Pack/Prefabs/PlantFlower.prefab", Tipo.Planta, "planta", 0.7f);
        for (int i = 1; i <= 3; i++) Add(l, "pedra" + i, zns + "FREE_VEGETATION_PACK/PREFABS/Rock_" + i + ".prefab", Tipo.Pedra, "pedra", 1f);
        // plantas estranhas (fotogrametria): uma variante de cada espécie
        string sp = P + "Splash of Color - Unique Photogrammetry Plants/Prefabs/Medium Resolution With Level Of Detail/";
        if (Directory.Exists(sp))
            foreach (string dir in Directory.GetDirectories(sp))
            {
                string nome = Path.GetFileName(dir);
                string pf = "Assets" + sp.Substring("Assets".Length) + nome + "/" + nome + " A1.prefab";
                Add(l, "estranha_" + nome.Replace(' ', '_'), pf, Tipo.Planta, "estranha", 1f);
            }
        // capim e flores (detalhes do terreno)
        for (int i = 1; i <= 4; i++) Add(l, "capim_zns" + i, zns + "FREE_VEGETATION_PACK/PREFABS/Grass_" + i + ".prefab", Tipo.Grama, "capim", 1f);
        for (int i = 1; i <= 3; i++) Add(l, "capim_dream" + i, P + "DreamTree2/Prefab/grass plant 0" + i + ".prefab", Tipo.Grama, "capim", 1f);
        foreach (string n in new[] { "lily_small", "whitecampion", "marigold_small", "pinkcarnation", "lily", "wild_sunflowers", "marigold" })
            Add(l, "flor_" + n, P + "Flowers And Garden Assets/Prefabs/flowers_" + n + ".prefab", Tipo.Flor, "flor", 1f);
        return l;
    }

    // ------------------------------------------------------------------
    //  CONVERSÃO PARA URP
    // ------------------------------------------------------------------
    class PropsMat
    {
        public string shaderGuid;
        public Dictionary<string, string> tex = new Dictionary<string, string>();
        public Dictionary<string, float> f = new Dictionary<string, float>();
        public Dictionary<string, Color> c = new Dictionary<string, Color>();
    }

    // lê o .mat como texto: funciona mesmo quando o shader (HDRP) não existe no projeto
    static PropsMat LerMat(string caminho)
    {
        PropsMat pm = new PropsMat();
        string[] linhas;
        try { linhas = File.ReadAllLines(caminho); } catch { return pm; }
        string secao = "", atual = "";
        Regex rShader = new Regex(@"m_Shader: \{fileID: -?\d+, guid: (\w+)");
        Regex rGuid = new Regex(@"m_Texture: \{fileID: -?\d+, guid: (\w+)");
        Regex rCor = new Regex(@"^\s+- (\w+): \{r: ([-\d\.eE]+), g: ([-\d\.eE]+), b: ([-\d\.eE]+), a: ([-\d\.eE]+)\}");
        Regex rFloat = new Regex(@"^\s+- (\w+): ([-\d\.eE]+)\s*$");
        Regex rNome = new Regex(@"^\s+- (\w+):\s*$");
        foreach (string ln in linhas)
        {
            Match m = rShader.Match(ln); if (m.Success) pm.shaderGuid = m.Groups[1].Value;
            if (ln.StartsWith("    m_TexEnvs:")) { secao = "tex"; continue; }
            if (ln.StartsWith("    m_Floats:") || ln.StartsWith("    m_Ints:")) { secao = "f"; continue; }
            if (ln.StartsWith("    m_Colors:")) { secao = "c"; continue; }
            if (ln.StartsWith("  m_") && !ln.StartsWith("  m_SavedProperties")) secao = "";
            if (secao == "tex")
            {
                Match mn = rNome.Match(ln); if (mn.Success) { atual = mn.Groups[1].Value; continue; }
                Match mg = rGuid.Match(ln);
                if (mg.Success && mg.Groups[1].Value != "00000000000000000000000000000000" && atual != "") pm.tex[atual] = mg.Groups[1].Value;
            }
            else if (secao == "f") { Match mf = rFloat.Match(ln); if (mf.Success) pm.f[mf.Groups[1].Value] = Num(mf.Groups[2].Value); }
            else if (secao == "c")
            {
                Match mc = rCor.Match(ln);
                if (mc.Success) pm.c[mc.Groups[1].Value] = new Color(Num(mc.Groups[2].Value), Num(mc.Groups[3].Value), Num(mc.Groups[4].Value), Num(mc.Groups[5].Value));
            }
        }
        return pm;
    }

    static float Num(string s) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f; }

    static Texture2D Tex(PropsMat pm, params string[] nomes)
    {
        foreach (string n in nomes)
        {
            string g; if (!pm.tex.TryGetValue(n, out g)) continue;
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(path)) continue;
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null) return t;
        }
        return null;
    }

    static bool JaEhUrp(Material m)
    {
        if (m == null || m.shader == null) return false;
        string n = m.shader.name;
        if (n.StartsWith("Hidden/InternalErrorShader") || n == "Standard" || n.StartsWith("Legacy") || n.StartsWith("Nature/") || n.StartsWith("HDRP")) return false;
        return n.StartsWith("Universal Render Pipeline") || n.Contains("URP") || n.StartsWith("Shader Graphs/") || n.StartsWith("Wild Boar/") || n.StartsWith("Squirrel/") || n.StartsWith("Unlit");
    }

    static Material ConverterMaterial(Material orig, string prefixo, bool foliagem)
    {
        if (orig == null) return null;
        if (JaEhUrp(orig)) return orig;
        string pathOrig = AssetDatabase.GetAssetPath(orig);
        string chave = pathOrig + "|" + orig.name;
        Material pronto;
        if (cacheMat.TryGetValue(chave, out pronto)) return pronto;

        PropsMat pm = string.IsNullOrEmpty(pathOrig) || !pathOrig.EndsWith(".mat") ? new PropsMat() : LerMat(Path.GetFullPath(pathOrig));
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        string nome = prefixo + "_" + Regex.Replace(orig.name, @"[^\w\-]", "_");
        string destino = PASTA_MATS + "/" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(destino);
        if (m == null) { m = new Material(urp); AssetDatabase.CreateAsset(m, destino); }
        m.shader = urp;

        Texture2D baseTex = Tex(pm, "_BaseColorMap", "_BaseMap", "_MainTex", "_Albedo", "_Diffuse", "_BaseColorTexture");
        Texture2D normal = Tex(pm, "_NormalMap", "_BumpMap");
        if (baseTex == null && orig.shader != null && !orig.shader.name.StartsWith("Hidden")) // material que o Unity leu direito
        {
            foreach (string n in new[] { "_BaseMap", "_MainTex", "_BaseColorMap" }) if (orig.HasProperty(n)) { baseTex = orig.GetTexture(n) as Texture2D; if (baseTex != null) break; }
            foreach (string n in new[] { "_BumpMap", "_NormalMap" }) if (orig.HasProperty(n)) { normal = orig.GetTexture(n) as Texture2D; if (normal != null) break; }
        }
        Color cor = Color.white; Color cc;
        if (pm.c.TryGetValue("_BaseColor", out cc) || pm.c.TryGetValue("_Color", out cc)) cor = cc;
        else if (orig.shader != null && !orig.shader.name.StartsWith("Hidden")) { if (orig.HasProperty("_BaseColor")) cor = orig.GetColor("_BaseColor"); else if (orig.HasProperty("_Color")) cor = orig.GetColor("_Color"); }
        cor.a = 1f;
        m.SetColor("_BaseColor", cor);
        m.SetTexture("_BaseMap", baseTex);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", foliagem ? 0.12f : 0.25f);

        string lower = (orig.name + " " + pathOrig).ToLower();
        bool pareceFolha = foliagem || Regex.IsMatch(lower, "leaf|leaves|folh|foliage|grass|plant|flower|frond|needle|fern|bush|branch|petal|weed|shrub|thuja|pine|vine");
        float f; float corte = -1f;
        if (pm.f.TryGetValue("_AlphaCutoffEnable", out f) && f > 0.5f) corte = pm.f.TryGetValue("_AlphaCutoff", out f) ? f : 0.5f;
        else if (pm.f.TryGetValue("_Mode", out f) && f >= 1f) corte = pm.f.TryGetValue("_Cutoff", out f) ? f : 0.4f;
        else if (pm.f.TryGetValue("_SurfaceType", out f) && f > 0.5f) corte = 0.4f;
        else if (pareceFolha && baseTex != null) corte = 0.45f;
        if (corte > 0f && baseTex != null)
        {
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", Mathf.Clamp(corte, 0.1f, 0.9f)); m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = 2450;
        }
        else { m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON"); m.renderQueue = -1; }
        bool doisLados = pareceFolha || (pm.f.TryGetValue("_DoubleSidedEnable", out f) && f > 0.5f) || (pm.f.TryGetValue("_Cull", out f) && f < 0.5f) || corte > 0f;
        m.SetFloat("_Cull", doisLados ? 0f : 2f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        cacheMat[chave] = m;
        return m;
    }

    static void ConverterRenderers(GameObject g, string prefixo, bool foliagem)
    {
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            Material[] ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = ConverterMaterial(ms[i], prefixo, foliagem);
            r.sharedMaterials = ms;
        }
    }

    // ------------------------------------------------------------------
    //  PREPARAR CADA ESPÉCIE: cópia URP, escala certa, base no chão, LOD e colisão
    // ------------------------------------------------------------------
    static int Triangulos(Renderer[] rs)
    {
        int t = 0;
        foreach (Renderer r in rs)
        {
            Mesh m = null;
            if (r is SkinnedMeshRenderer) m = ((SkinnedMeshRenderer)r).sharedMesh;
            else { MeshFilter mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
            if (m == null) continue;
            for (int i = 0; i < m.subMeshCount; i++) t += (int)(m.GetIndexCount(i) / 3);
        }
        return t;
    }

    static Renderer[] RenderersDoLod0(GameObject g, out LOD[] lods)
    {
        lods = null;
        LODGroup lg = g.GetComponentInChildren<LODGroup>();
        if (lg != null)
        {
            lods = lg.GetLODs();
            if (lods.Length > 0 && lods[0].renderers != null && lods[0].renderers.Length > 0)
            {
                List<Renderer> l = new List<Renderer>();
                foreach (Renderer r in lods[0].renderers) if (r != null) l.Add(r);
                if (l.Count > 0) return l.ToArray();
            }
        }
        List<Renderer> todos = new List<Renderer>();
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) if (!(r is ParticleSystemRenderer)) todos.Add(r);
        return todos.ToArray();
    }

    static void Faixas(Tipo t, out float min, out float max)
    {
        switch (t)
        {
            case Tipo.Arvore: min = 16f; max = 30f; break;          // árvores grandes, como nos jogos de referência
            case Tipo.ArvoreSeca: min = 6f; max = 14f; break;
            case Tipo.Arbusto: min = 1.2f; max = 3.0f; break;
            case Tipo.Planta: min = 0.7f; max = 1.9f; break;
            case Tipo.Pedra: min = 2.4f; max = 5.5f; break;          // pedras grandes (matacões)
            case Tipo.Grama: min = 0.6f; max = 1.25f; break;           // capim alto e cheio
            default: min = 0.45f; max = 1.0f; break;
        }
    }

    static void Preparar(Esp e)
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(e.caminho);
        if (src == null) { avisos.Add("Não achei " + e.caminho); return; }
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        try
        {
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity;
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Light l in inst.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            foreach (MonoBehaviour mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null && mb.GetType().Name.Contains("Billboard")) Object.DestroyImmediate(mb);
            bool folha = e.tipo != Tipo.Pedra;
            ConverterRenderers(inst, e.grupo, folha);

            LOD[] lods;
            Renderer[] lod0 = RenderersDoLod0(inst, out lods);
            if (lod0.Length == 0) { avisos.Add(e.id + ": sem malha, ignorado."); return; }
            int tris = Triangulos(lod0);
            int limiteTris = e.tipo == Tipo.Arvore || e.tipo == Tipo.ArvoreSeca ? (lods != null && lods.Length > 1 ? 130000 : 40000) : (e.tipo == Tipo.Pedra ? 25000 : (e.tipo == Tipo.Grama || e.tipo == Tipo.Flor ? 2500 : 14000));
            if (tris > limiteTris) { avisos.Add(e.id + ": pesado demais (" + tris + " triângulos), não foi usado."); return; }

            Bounds b = new Bounds(lod0[0].bounds.center, Vector3.zero);
            foreach (Renderer r in lod0) b.Encapsulate(r.bounds);
            float h = Mathf.Max(0.01f, b.size.y);
            float min, max; Faixas(e.tipo, out min, out max);
            float fator = e.grupo == "thuja" ? 1f : (h < min ? min / h : (h > max ? max / h : 1f));
            e.altura = h * fator;
            e.raio = Mathf.Max(0.25f, Mathf.Max(b.size.x, b.size.z) * fator * (e.tipo == Tipo.Arvore || e.tipo == Tipo.ArvoreSeca ? 0.22f : 0.35f));
            if (e.grupo == "thuja") e.tipo = e.altura > 3.5f ? Tipo.Arvore : Tipo.Arbusto;

            if (e.tipo == Tipo.Grama || e.tipo == Tipo.Flor) { e.prefab = CriarPrefabDeDetalhe(e, inst, lod0, fator, b); return; }

            // raiz limpa + modelo escalado, com a base no chão
            GameObject raiz = new GameObject("Veg_" + e.id);
            inst.transform.SetParent(raiz.transform, true);
            Vector3 escOriginal = inst.transform.localScale;
            inst.transform.localScale = escOriginal * fator;
            inst.transform.position = new Vector3(0f, -b.min.y * fator, 0f);
            LODGroup lgVelho = inst.GetComponent<LODGroup>();
            LODGroup[] todosLg = inst.GetComponentsInChildren<LODGroup>(true);
            foreach (LODGroup x in todosLg) Object.DestroyImmediate(x);
            LODGroup lg = raiz.AddComponent<LODGroup>();
            if (lods != null && lods.Length > 0) lg.SetLODs(lods);
            else
            {
                float corte = e.tipo == Tipo.Arvore || e.tipo == Tipo.ArvoreSeca ? 0.02f : (e.tipo == Tipo.Planta ? 0.04f : 0.03f);
                if (tris > 15000) corte *= 2.2f;
                lg.SetLODs(new[] { new LOD(corte, lod0) });
            }
            lg.RecalculateBounds();
            if (e.tipo == Tipo.Arvore || e.tipo == Tipo.ArvoreSeca || e.tipo == Tipo.Pedra)
            {
                CapsuleCollider cap = raiz.AddComponent<CapsuleCollider>();
                float alt = e.altura;
                cap.radius = e.tipo == Tipo.Pedra ? Mathf.Clamp(e.altura * 0.5f, 0.5f, 2.8f) : Mathf.Clamp(e.raio * 0.28f, 0.25f, 0.9f);
                cap.height = e.tipo == Tipo.Pedra ? Mathf.Max(cap.radius * 2f, e.altura) : Mathf.Min(alt, 7f);
                cap.center = Vector3.up * cap.height * 0.5f;
            }
            string pf = PASTA_PREFABS + "/Veg_" + e.id + ".prefab";
            e.prefab = PrefabUtility.SaveAsPrefabAsset(raiz, pf);
            Object.DestroyImmediate(raiz);
            inst = null;
        }
        catch (System.Exception ex) { avisos.Add(e.id + ": " + ex.Message); e.prefab = null; }
        finally { if (inst != null) Object.DestroyImmediate(inst); }
    }

    // capim e flores: uma malha só (LOD0 junto), com a escala já aplicada e a base na origem
    static GameObject CriarPrefabDeDetalhe(Esp e, GameObject inst, Renderer[] lod0, float fator, Bounds b)
    {
        List<CombineInstance> ci = new List<CombineInstance>();
        List<Material> mats = new List<Material>();
        Matrix4x4 baixa = Matrix4x4.Translate(new Vector3(0f, -b.min.y * fator, 0f)) * Matrix4x4.Scale(Vector3.one * fator);
        foreach (Renderer r in lod0)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            Mesh m = mf.sharedMesh;
            Material[] ms = r.sharedMaterials;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                CombineInstance c = new CombineInstance { mesh = m, subMeshIndex = s, transform = baixa * r.transform.localToWorldMatrix };
                ci.Add(c); mats.Add(ms[Mathf.Min(s, ms.Length - 1)]);
            }
        }
        if (ci.Count == 0) return null;
        // uma submalha por material
        List<Material> unicos = new List<Material>(); List<Mesh> partes = new List<Mesh>();
        foreach (Material mt in mats) if (!unicos.Contains(mt)) unicos.Add(mt);
        foreach (Material mt in unicos)
        {
            List<CombineInstance> deste = new List<CombineInstance>();
            for (int i = 0; i < ci.Count; i++) if (mats[i] == mt) deste.Add(ci[i]);
            Mesh pm = new Mesh { indexFormat = IndexFormat.UInt32 }; pm.CombineMeshes(deste.ToArray(), true, true); partes.Add(pm);
        }
        CombineInstance[] finais = new CombineInstance[partes.Count];
        for (int i = 0; i < partes.Count; i++) finais[i] = new CombineInstance { mesh = partes[i], subMeshIndex = 0, transform = Matrix4x4.identity };
        Mesh total = new Mesh { name = "Veg_" + e.id, indexFormat = IndexFormat.UInt32 };
        total.CombineMeshes(finais, false, false);
        total.RecalculateBounds();
        string mp = PASTA_MALHAS + "/Veg_" + e.id + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(mp) != null) AssetDatabase.DeleteAsset(mp);
        AssetDatabase.CreateAsset(total, mp);
        GameObject g = new GameObject("Veg_" + e.id);
        g.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterials = unicos.ToArray();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(g, PASTA_PREFABS + "/Veg_" + e.id + ".prefab");
        Object.DestroyImmediate(g);
        return prefab;
    }

    // ------------------------------------------------------------------
    //  LIMITE DA FLORESTA, ZONAS PROTEGIDAS E ESTRADAS
    // ------------------------------------------------------------------
    static LimiteDaFloresta AcharOuCriarLimite()
    {
        LimiteDaFloresta l = Object.FindFirstObjectByType<LimiteDaFloresta>();
        if (l == null)
        {
            GameObject g = new GameObject(NOME_LIMITE);
            Undo.RegisterCreatedObjectUndo(g, "Limite da floresta");
            l = g.AddComponent<LimiteDaFloresta>();
            // contorno que você marcou no mapa (x, z do mundo)
            Vector2[] def = {
                new Vector2(-30, 1030), new Vector2(725, 1030), new Vector2(735, 855), new Vector2(728, 655), new Vector2(722, 490),
                new Vector2(672, 465), new Vector2(597, 440), new Vector2(517, 392), new Vector2(467, 352), new Vector2(427, 292),
                new Vector2(380, 215), new Vector2(330, 125), new Vector2(270, 40), new Vector2(225, -30), new Vector2(-30, -30) };
            for (int i = 0; i < def.Length; i++)
            {
                GameObject p = new GameObject("P" + (i + 1).ToString("00"));
                p.transform.SetParent(g.transform, false);
                p.transform.position = new Vector3(def[i].x, Altura(Mathf.Clamp(def[i].x, 0f, 999f), Mathf.Clamp(def[i].y, 0f, 999f)) + 10f, def[i].y);
            }
        }
        l.Recalcular();
        EditorUtility.SetDirty(l);
        return l;
    }

    static Vector3 centroVila; static bool temVila;
    static void MontarZonasEEstradas()
    {
        temVila = false;
        CharacterController cc = Object.FindFirstObjectByType<CharacterController>();
        if (cc != null) zonas.Add(new Vector4(cc.transform.position.x, cc.transform.position.z, 110f));
        MiniMapa mm = Object.FindFirstObjectByType<MiniMapa>();
        AcharCastelo();
        cemiterio = new Vector3(220f, 0f, 790f);
        if (mm != null)
            foreach (MiniMapa.Marcador m in mm.marcadores)
            {
                float r = m.tipo == MiniMapa.Tipo.Cemiterio ? 140f : (m.tipo == MiniMapa.Tipo.Vila ? 130f : (m.tipo == MiniMapa.Tipo.Ruinas ? 118f : 60f));
                if (!(temCastelo && m.tipo == MiniMapa.Tipo.Ruinas)) zonas.Add(new Vector4(m.posicao.x, m.posicao.z, r));
                if (m.tipo == MiniMapa.Tipo.Cemiterio) cemiterio = m.posicao;
                if (m.tipo == MiniMapa.Tipo.Vila) { centroVila = m.posicao; temVila = true; }
            }
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t.name.ToLower().Contains("portal") && t.parent == null) zonas.Add(new Vector4(t.position.x, t.position.z, 35f));

        int n = Mathf.CeilToInt(dados.size.x / CEL_EST);
        estrada = new bool[n, n];
        RedeDeEstradas rede = Object.FindFirstObjectByType<RedeDeEstradas>();
        Vector3 o = terreno.transform.position;
        if (rede != null)
            foreach (RedeDeEstradas.Estrada e in rede.estradas)
            {
                if (e.pontos == null) continue;
                int raio = Mathf.CeilToInt((e.largura * 0.5f + 4f) / CEL_EST);
                for (int i = 0; i + 1 < e.pontos.Length; i++)
                {
                    Vector3 a = e.pontos[i], b = e.pontos[i + 1];
                    int passos = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 2f));
                    for (int k = 0; k <= passos; k++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, k / (float)passos);
                        int cx = Mathf.FloorToInt((p.x - o.x) / CEL_EST), cz = Mathf.FloorToInt((p.z - o.z) / CEL_EST);
                        for (int dx = -raio; dx <= raio; dx++)
                            for (int dz = -raio; dz <= raio; dz++)
                            { int x = cx + dx, z = cz + dz; if (x >= 0 && z >= 0 && x < n && z < n) estrada[x, z] = true; }
                    }
                }
            }
    }

    static bool NaEstrada(float x, float z)
    {
        Vector3 o = terreno.transform.position;
        int cx = Mathf.FloorToInt((x - o.x) / CEL_EST), cz = Mathf.FloorToInt((z - o.z) / CEL_EST);
        return estrada != null && cx >= 0 && cz >= 0 && cx < estrada.GetLength(0) && cz < estrada.GetLength(1) && estrada[cx, cz];
    }

    // castelo destruído: o terreno em volta fica com a vegetação do bioma; dentro do pátio entram só algumas árvores
    static bool temCastelo; static Vector3 castelo; static float casteloCos = 1f, casteloSen;
    static void AcharCastelo()
    {
        temCastelo = false;
        GameObject est = GameObject.Find("Mundo_Gerado/Castelo Destruido/Estrutura");
        if (est == null) return;
        castelo = est.transform.position; float yaw = est.transform.eulerAngles.y * Mathf.Deg2Rad;
        casteloCos = Mathf.Cos(yaw); casteloSen = Mathf.Sin(yaw); temCastelo = true;
    }
    static bool NoCastelo(float x, float z)
    {
        float dx = x - castelo.x, dz = z - castelo.z;
        float lx = dx * casteloCos - dz * casteloSen, lz = dx * casteloSen + dz * casteloCos;   // coordenadas locais do castelo
        const float HX = 70f, HZ = 60f;
        if (Mathf.Abs(lx) > HX + 7f || Mathf.Abs(lz) > HZ + 7f) return false;     // fora das muralhas: vegetação normal
        if (Mathf.Abs(lx) > HX - 9f || Mathf.Abs(lz) > HZ - 9f) return true;       // faixa junto aos muros: livre
        if (Mathf.Abs(lx) < 32f && lz < 6f && lz > -52f) return true;               // torre de menagem
        if (Mathf.Abs(lx) < 12f && lz > 0f) return true;                             // caminho da portaria
        uint h = (uint)(Mathf.RoundToInt(x * 0.5f) * 73856093) ^ (uint)(Mathf.RoundToInt(z * 0.5f) * 19349663);
        return (h % 100u) >= 8u;   // dentro do pátio só ~8% das árvores: ar de batalha antiga
    }

    static bool EmZona(float x, float z, float extra)
    {
        if (temCastelo && NoCastelo(x, z)) return true;
        foreach (Vector4 zn in zonas) { float dx = x - zn.x, dz = z - zn.y; float r = zn.z + extra; if (dx * dx + dz * dz < r * r) return true; }
        return false;
    }

    // peso "fora da floresta sombria": 0 dentro do contorno, 1 bem fora (com transição de ~40 m)
    static float PesoFora(float x, float z)
    {
        if (limite == null || !limite.Valido) return 1f;
        float d = limite.DistanciaDaBorda(x, z);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, -28f, d));
    }

    // ------------------------------------------------------------------
    //  CHÃO
    // ------------------------------------------------------------------
    static TerrainLayer CamadaOriginal(string nome)
    {
        TerrainLayer t = AssetDatabase.LoadAssetAtPath<TerrainLayer>(CAMADAS_TS + nome + "_TerrainLayer.terrainlayer");
        if (t == null) avisos.Add("Não achei a textura de chão " + nome);
        return t;
    }

    static void PintarChao()
    {
        TerrainLayer gA = CamadaOriginal("Grass_A"), gB = CamadaOriginal("Grass_B"), gS = CamadaOriginal("Grass_Soil"), pedras = CamadaOriginal("Pebbles_B");
        if (gA == null || gB == null || gS == null) { avisos.Add("O chão do resto do mapa não foi repintado (faltou textura)."); return; }
        List<TerrainLayer> camadas = new List<TerrainLayer>(dados.terrainLayers);
        System.Func<TerrainLayer, int> garantir = t =>
        {
            if (t == null) return -1;
            for (int i = 0; i < camadas.Count; i++) if (camadas[i] == t) return i;
            camadas.Add(t); return camadas.Count - 1;
        };
        int iA = garantir(gA), iB = garantir(gB), iS = garantir(gS), iP = garantir(pedras);
        int iRocha = -1, iEstrada = -1, iPraca = -1;
        for (int i = 0; i < camadas.Count; i++)
        {
            if (camadas[i] == null) continue;
            string n = camadas[i].name;
            if (n == "Rock" || n.StartsWith("Rock")) iRocha = i;
            if (n.Contains("Soil_Rocks")) iEstrada = i;
            if (n.Contains("Pebbles_A")) iPraca = i;
        }
        int res = dados.alphamapResolution, nOld = dados.alphamapLayers;
        float[,,] velho = dados.GetAlphamaps(0, 0, res, res);
        dados.terrainLayers = camadas.ToArray();
        int nNew = camadas.Count;
        float[,,] a = new float[res, res, nNew];
        Vector3 o = terreno.transform.position, s = dados.size;
        float[] alvo = new float[nNew];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float px = o.x + (x + 0.5f) / res * s.x, pz = o.z + (z + 0.5f) / res * s.z;
                float m = PesoFora(px, pz);
                float estr = iEstrada >= 0 && iEstrada < nOld ? velho[z, x, iEstrada] : 0f, praca = iPraca >= 0 && iPraca < nOld ? velho[z, x, iPraca] : 0f;
                if (m <= 0.001f)
                {
                    for (int k = 0; k < nOld; k++) a[z, x, k] = velho[z, x, k];
                    continue;
                }
                float n1 = Mathf.PerlinNoise(px * 0.012f + 3f, pz * 0.012f + 9f), n2 = Mathf.PerlinNoise(px * 0.05f + 7f, pz * 0.05f + 1f);
                for (int k = 0; k < nNew; k++) alvo[k] = 0f;
                alvo[iA] = 0.5f + 0.4f * n1; alvo[iB] = 0.45f - 0.35f * n1;
                alvo[iS] = Mathf.Clamp01((n2 - 0.62f) * 3f) * 0.8f;
                float incl = dados.GetSteepness((px - o.x) / s.x, (pz - o.z) / s.z);
                float rocha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(28f, 42f, incl));
                int iR = iRocha >= 0 ? iRocha : iP;
                if (iR >= 0) { for (int k = 0; k < nNew; k++) alvo[k] *= 1f - rocha; alvo[iR] += rocha; }
                float soma = 0f; for (int k = 0; k < nNew; k++) soma += alvo[k];
                float resto = Mathf.Max(0f, 1f - estr - praca);
                for (int k = 0; k < nNew; k++)
                {
                    float antes = k < nOld ? velho[z, x, k] : 0f;
                    float novo = k == iEstrada ? estr : (k == iPraca ? praca : (soma > 0f ? alvo[k] / soma * resto : 0f));
                    a[z, x, k] = Mathf.Lerp(antes, novo, m);
                }
            }
        dados.SetAlphamaps(0, 0, a);
        relatorio.Add("Chão: gramado verde fora da Floresta Sombria.");
    }

    // ------------------------------------------------------------------
    //  ÁRVORES
    // ------------------------------------------------------------------
    static Dictionary<long, List<Vector3>> hash = new Dictionary<long, List<Vector3>>();   // x, z, raio
    const float CEL_HASH = 10f;
    static long Chave(int gx, int gz) { return ((long)gx << 32) ^ (uint)gz; }

    static bool Livre(float x, float z, float raio)
    {
        int cx = Mathf.FloorToInt(x / CEL_HASH), cz = Mathf.FloorToInt(z / CEL_HASH);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                List<Vector3> l;
                if (!hash.TryGetValue(Chave(cx + dx, cz + dz), out l)) continue;
                foreach (Vector3 q in l) { float ddx = q.x - x, ddz = q.y - z, rr = (q.z + raio) * 0.85f; if (ddx * ddx + ddz * ddz < rr * rr) return false; }
            }
        return true;
    }

    static void Registrar(float x, float z, float raio)
    {
        long k = Chave(Mathf.FloorToInt(x / CEL_HASH), Mathf.FloorToInt(z / CEL_HASH));
        List<Vector3> l; if (!hash.TryGetValue(k, out l)) { l = new List<Vector3>(); hash[k] = l; }
        l.Add(new Vector3(x, z, raio));
    }

    static Esp Sortear(List<Esp> lista)
    {
        if (lista.Count == 0) return null;
        float soma = 0f; foreach (Esp e in lista) soma += e.peso;
        float r = Faixa(0f, soma);
        foreach (Esp e in lista) { r -= e.peso; if (r <= 0f) return e; }
        return lista[lista.Count - 1];
    }

    static List<Esp> Grupo(List<Esp> cat, params string[] grupos)
    {
        List<Esp> l = new List<Esp>();
        foreach (Esp e in cat) if (e.indice >= 0 && System.Array.IndexOf(grupos, e.grupo) >= 0 && e.tipo != Tipo.Grama && e.tipo != Tipo.Flor) l.Add(e);
        return l;
    }

    static void Arvores(List<Esp> cat)
    {
        Vector3 o = terreno.transform.position, s = dados.size;
        hash.Clear();
        // protótipos do terreno: mantém só as árvores sinistras; troca o resto pelas espécies novas
        TreeInstance[] instanciasAntigas = dados.treeInstances;
        TreePrototype[] antigos = dados.treePrototypes;
        List<TreePrototype> novos = new List<TreePrototype>();
        Dictionary<int, int> mapa = new Dictionary<int, int>();
        List<int> sinistras = new List<int>();
        for (int i = 0; i < antigos.Length; i++)
            if (antigos[i].prefab != null && antigos[i].prefab.name.StartsWith("ArvoreSinistra"))
            { mapa[i] = novos.Count; sinistras.Add(novos.Count); novos.Add(antigos[i]); }
        foreach (Esp e in cat)
            if (e.tipo != Tipo.Grama && e.tipo != Tipo.Flor) { e.indice = novos.Count; novos.Add(new TreePrototype { prefab = e.prefab, bendFactor = 0f }); }
        dados.treePrototypes = novos.ToArray();

        List<TreeInstance> todas = new List<TreeInstance>();
        int mantidas = 0, removidas = 0;
        foreach (TreeInstance ti in instanciasAntigas)
        {
            int novoIdx;
            if (!mapa.TryGetValue(ti.prototypeIndex, out novoIdx)) { removidas++; continue; }
            float x = o.x + ti.position.x * s.x, z = o.z + ti.position.z * s.z;
            if (limite != null && limite.Valido && !limite.Contem(x, z)) { removidas++; continue; }   // sinistra fora do contorno
            TreeInstance t2 = ti; t2.prototypeIndex = novoIdx; todas.Add(t2); Registrar(x, z, 1.8f); mantidas++;
        }
        // as árvores sinistras que são objetos da cena (ao redor do cemitério) também ocupam espaço
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t.name == "Arvore Sinistra") Registrar(t.position.x, t.position.z, 1.8f);

        List<Esp> folhosas = Grupo(cat, "folhosa"), coniferas = Grupo(cat, "conifera"), thujas = Grupo(cat, "thuja");
        List<Esp> secas = Grupo(cat, "seca"), arbustos = Grupo(cat, "arbusto"), plantas = Grupo(cat, "planta"), estranhas = Grupo(cat, "estranha"), pedras = Grupo(cat, "pedra");
        List<Esp> arbustosEPlantas = new List<Esp>(arbustos); arbustosEPlantas.AddRange(plantas);
        int contFora = 0, contSombria = 0, contSub = 0, contSubSombria = 0;
        const int TETO = 17000;
        System.Action<Esp, float, float, float, bool> plantar = (e, x, z, esc, forcar) =>
        {
            float nx = (x - o.x) / s.x, nz = (z - o.z) / s.z;
            todas.Add(new TreeInstance
            {
                prototypeIndex = e.indice, position = new Vector3(nx, 0f, nz),
                widthScale = esc, heightScale = esc * Faixa(0.92f, 1.1f), rotation = Faixa(0f, Mathf.PI * 2f),
                color = Color.white, lightmapColor = Color.white
            });
            Registrar(x, z, e.raio * esc);
        };

        System.Func<float, float, float, bool> terrenoOk = (x, z, inclMax) =>
        {
            if (x < o.x + 6f || z < o.z + 6f || x > o.x + s.x - 6f || z > o.z + s.z - 6f) return false;
            float nx = (x - o.x) / s.x, nz = (z - o.z) / s.z;
            if (dados.GetSteepness(nx, nz) > inclMax) return false;
            if (dados.GetInterpolatedHeight(nx, nz) < 0.6f) return false;
            return !EmZona(x, z, 0f) && !NaEstrada(x, z);
        };

        // ---- 1) floresta e campos FORA do contorno: mosaico de matas, pinhais e clareiras
        for (float gx = o.x + 4f; gx < o.x + s.x - 4f; gx += 6f)
            for (float gz = o.z + 4f; gz < o.z + s.z - 4f; gz += 6f)
            {
                if (todas.Count > TETO) break;
                float x = gx + Faixa(-2.4f, 2.4f), z = gz + Faixa(-2.4f, 2.4f);
                float fora = PesoFora(x, z);
                if (fora < 0.05f || rng.NextDouble() > fora) continue;
                float mata = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.64f, Mathf.PerlinNoise(x * 0.0035f + 11f, z * 0.0035f + 5f)));
                float pTree = Mathf.Lerp(0.05f, 0.55f, mata);      // campos abertos com árvores grandes espalhadas
                if (rng.NextDouble() > pTree) continue;
                float mistura = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.6f, Mathf.PerlinNoise(x * 0.006f + 31f, z * 0.006f + 17f)));
                Esp e;
                double r = rng.NextDouble();
                if (r < 0.07 && thujas.Count > 0) e = Sortear(thujas);
                else e = rng.NextDouble() < mistura ? Sortear(coniferas) : Sortear(folhosas);
                if (e == null) e = Sortear(coniferas.Count > 0 ? coniferas : folhosas);
                if (e == null || !terrenoOk(x, z, 34f)) continue;
                float esc = Faixa(0.9f, 1.35f);
                if (!Livre(x, z, e.raio * esc)) continue;
                plantar(e, x, z, esc, false); contFora++;
            }

        // ---- 2) sub-bosque fora do contorno: arbustos, plantas estranhas e pedras em manchas
        for (float gx = o.x + 4f; gx < o.x + s.x - 4f; gx += 4.5f)
            for (float gz = o.z + 4f; gz < o.z + s.z - 4f; gz += 4.5f)
            {
                if (todas.Count > TETO + 5000) break;
                float x = gx + Faixa(-1.8f, 1.8f), z = gz + Faixa(-1.8f, 1.8f);
                float fora = PesoFora(x, z);
                if (fora < 0.2f || rng.NextDouble() > fora) continue;
                float mancha = Mathf.PerlinNoise(x * 0.03f + 5f, z * 0.03f + 13f);
                float mata = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.64f, Mathf.PerlinNoise(x * 0.0035f + 11f, z * 0.0035f + 5f)));
                if (mancha < 0.5f || rng.NextDouble() > Mathf.Lerp(0.03f, 0.17f, mata)) continue;
                double r = rng.NextDouble();
                Esp e = r < 0.12 && pedras.Count > 0 ? Sortear(pedras) : (r < 0.30 && estranhas.Count > 0 ? Sortear(estranhas) : Sortear(arbustosEPlantas));
                if (e == null || !terrenoOk(x, z, e.tipo == Tipo.Pedra ? 50f : 38f)) continue;
                float esc = Faixa(0.8f, 1.35f);
                if (!Livre(x, z, e.raio * esc * 0.6f)) continue;
                plantar(e, x, z, esc, false); contSub++;
            }

        // ---- 3) DENTRO do contorno (Floresta Sombria): completa os vazios com árvores sinistras, secas e esqueletos de pinheiro
        if (limite != null && limite.Valido)
        {
            for (float gx = o.x + 4f; gx < o.x + s.x - 4f; gx += 7f)
                for (float gz = o.z + 4f; gz < o.z + s.z - 4f; gz += 7f)
                {
                    float x = gx + Faixa(-2.8f, 2.8f), z = gz + Faixa(-2.8f, 2.8f);
                    float dentro = 1f - PesoFora(x, z);
                    if (dentro < 0.1f || rng.NextDouble() > dentro) continue;
                    float dc = Mathf.Sqrt((x - cemiterio.x) * (x - cemiterio.x) + (z - cemiterio.z) * (z - cemiterio.z));
                    if (dc < 135f) continue;
                    float dens = Mathf.Lerp(0.62f, 0.4f, Mathf.InverseLerp(200f, 900f, dc));
                    if (rng.NextDouble() > dens) continue;
                    double r = rng.NextDouble();
                    int idx; float raio;
                    Esp e = null;
                    if (r < 0.58 && sinistras.Count > 0) { idx = sinistras[rng.Next(sinistras.Count)]; raio = 2.0f; }
                    else { e = Sortear(secas); if (e == null) continue; idx = e.indice; raio = e.raio; }
                    if (!terrenoOk(x, z, 36f)) continue;
                    float esc = e == null ? Faixa(1.0f, 2.1f) : Faixa(0.85f, 1.3f);
                    if (!Livre(x, z, raio * esc)) continue;
                    float nx = (x - o.x) / s.x, nz = (z - o.z) / s.z;
                    todas.Add(new TreeInstance { prototypeIndex = idx, position = new Vector3(nx, 0f, nz), widthScale = esc, heightScale = esc * Faixa(0.9f, 1.15f), rotation = Faixa(0f, Mathf.PI * 2f), color = Color.white, lightmapColor = Color.white });
                    Registrar(x, z, raio * esc); contSombria++;
                }
            // sub-bosque sombrio: plantas estranhas e pedras
            for (float gx = o.x + 4f; gx < o.x + s.x - 4f; gx += 6f)
                for (float gz = o.z + 4f; gz < o.z + s.z - 4f; gz += 6f)
                {
                    float x = gx + Faixa(-2.4f, 2.4f), z = gz + Faixa(-2.4f, 2.4f);
                    float dentro = 1f - PesoFora(x, z);
                    if (dentro < 0.5f || rng.NextDouble() > dentro) continue;
                    if (EmZona(x, z, 0f)) continue;
                    float mancha = Mathf.PerlinNoise(x * 0.04f + 41f, z * 0.04f + 23f);
                    if (mancha < 0.55f || rng.NextDouble() > 0.2) continue;
                    double r = rng.NextDouble();
                    Esp e = r < 0.1 && pedras.Count > 0 ? Sortear(pedras) : (r < 0.8 && estranhas.Count > 0 ? Sortear(estranhas) : Sortear(plantas));
                    if (e == null || !terrenoOk(x, z, 40f)) continue;
                    float esc = Faixa(0.8f, 1.3f);
                    if (!Livre(x, z, e.raio * esc * 0.6f)) continue;
                    plantar(e, x, z, esc, false); contSubSombria++;
                }
        }

        dados.SetTreeInstances(todas.ToArray(), true);
        relatorio.Add("Árvores do terreno: " + todas.Count + " no total (" + mantidas + " sinistras mantidas, " + removidas + " antigas removidas).");
        relatorio.Add("Fora da floresta sombria: " + contFora + " árvores e " + contSub + " arbustos/plantas/pedras. Dentro: " + contSombria + " árvores novas e " + contSubSombria + " plantas/pedras.");
        List<string> usados = new List<string>();
        foreach (Esp e in cat) if (e.indice >= 0 && e.tipo != Tipo.Grama && e.tipo != Tipo.Flor) usados.Add(e.id);
        relatorio.Add("Espécies usadas (" + usados.Count + "): " + string.Join(", ", usados.GetRange(0, Mathf.Min(usados.Count, 30))) + (usados.Count > 30 ? "..." : ""));
    }

    // ------------------------------------------------------------------
    //  CAPIM E FLORES (detalhes do terreno)
    // ------------------------------------------------------------------
    static void Detalhes(List<Esp> cat)
    {
        List<Esp> capins = new List<Esp>(), flores = new List<Esp>();
        int res = dados.detailResolution;
        // se a vegetação já foi feita antes, tira os detalhes velhos dela e não reduz o capim antigo outra vez
        DetailPrototype[] todosAntigos = dados.detailPrototypes;
        List<DetailPrototype> protos = new List<DetailPrototype>(); List<int[,]> camadas = new List<int[,]>();
        for (int i = 0; i < todosAntigos.Length; i++)
        {
            if (todosAntigos[i].prototype != null && todosAntigos[i].prototype.name.StartsWith("Veg_")) continue;
            protos.Add(todosAntigos[i]); camadas.Add(dados.GetDetailLayer(0, 0, res, res, i));
        }
        bool jaTinha = protos.Count < todosAntigos.Length;
        int nOld = protos.Count;
        foreach (Esp e in cat)
        {
            if (e.tipo != Tipo.Grama && e.tipo != Tipo.Flor) continue;
            DetailPrototype dp = new DetailPrototype
            {
                prototype = e.prefab, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                minWidth = 0.8f, maxWidth = 1.25f, minHeight = 0.8f, maxHeight = 1.3f, noiseSpread = 0.35f, density = 1f, alignToGround = 0.5f,
                healthyColor = new Color(0.9f, 0.95f, 0.85f), dryColor = new Color(0.85f, 0.8f, 0.6f)
            };
            string erro;
            if (!dp.Validate(out erro)) { avisos.Add(e.id + " não serve como detalhe: " + erro); continue; }
            e.indice = protos.Count; protos.Add(dp);
            if (e.tipo == Tipo.Grama) capins.Add(e); else flores.Add(e);
        }
        GameObject[] pfFolhas = PrefabsFolhasNoChao();
        List<int> folhasChao = new List<int>();
        foreach (GameObject pf in pfFolhas)
        {
            DetailPrototype dp = new DetailPrototype { prototype = pf, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true, minWidth = 0.9f, maxWidth = 1.5f, minHeight = 1f, maxHeight = 1f, noiseSpread = 0.5f, density = 1f, alignToGround = 0f, healthyColor = Color.white, dryColor = Color.white };
            string erro2;
            if (dp.Validate(out erro2)) { folhasChao.Add(protos.Count); protos.Add(dp); } else avisos.Add("Folhas no chão: " + erro2);
        }
        dados.detailPrototypes = protos.ToArray();
        while (camadas.Count < protos.Count) camadas.Add(new int[res, res]);
        bool cobertura = dados.detailScatterMode == DetailScatterMode.CoverageMode;
        float maxGrama = cobertura ? 230f : 5f, maxFlor = cobertura ? 150f : 3f;
        Vector3 o = terreno.transform.position;
        float cel = dados.size.x / res;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float px = o.x + (x + 0.5f) * cel, pz = o.z + (z + 0.5f) * cel;
                float fora = PesoFora(px, pz);
                if (EmZona(px, pz, 0f)) continue;   // vila, início, ruínas e acampamentos mantêm a vegetação que já tinham
                // capim antigo: menos no resto do mapa, para dar lugar às espécies novas
                if (fora > 0f && !jaTinha)
                    for (int l = 0; l < nOld; l++) camadas[l][z, x] = Mathf.RoundToInt(camadas[l][z, x] * Mathf.Lerp(1f, 0.35f, fora));
                if (fora < 0.05f || NaEstrada(px, pz)) continue;
                float incl = dados.GetSteepness((px - o.x) / dados.size.x, (pz - o.z) / dados.size.z);
                if (incl > 38f) continue;
                float mata = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.64f, Mathf.PerlinNoise(px * 0.0035f + 11f, pz * 0.0035f + 5f)));
                for (int k = 0; k < capins.Count; k++)
                {
                    float mancha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.5f, Mathf.PerlinNoise(px * 0.06f + k * 17f, pz * 0.06f + k * 5.3f)));
                    camadas[capins[k].indice][z, x] = Mathf.RoundToInt(maxGrama * mancha * fora * Mathf.Lerp(1f, 0.6f, mata) * 0.95f);
                }
                for (int k = 0; k < flores.Count; k++)
                {
                    // campos floridos: grandes manchas de uma só cor (amarelo, branco, rosa...) como nas referências
                    float mancha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.62f, Mathf.PerlinNoise(px * 0.022f + 100f + k * 23f, pz * 0.022f + 50f + k * 11f)));
                    camadas[flores[k].indice][z, x] = Mathf.RoundToInt(maxFlor * 1.6f * mancha * fora * (1f - mata * 0.6f));
                }
                // folhas caídas: manchas no chão das matas e dos campos (mais nas matas, onde há mais árvores)
                for (int k = 0; k < folhasChao.Count; k++)
                {
                    float ruido = Mathf.PerlinNoise(px * 0.035f + 300f + k * 31f, pz * 0.035f + 210f);
                    float mancha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.68f, ruido)) * Mathf.Lerp(0.35f, 1f, mata);
                    camadas[folhasChao[k]][z, x] = Mathf.RoundToInt((cobertura ? 90f : 2f) * mancha * fora);
                }
            }
        for (int l = 0; l < protos.Count; l++) dados.SetDetailLayer(0, 0, l, camadas[l]);
        dados.RefreshPrototypes();
        relatorio.Add("Capim: " + capins.Count + " tipos. Flores: " + flores.Count + " tipos.");
    }

    // ------------------------------------------------------------------
    //  ANIMAIS
    // ------------------------------------------------------------------
    static Vector3 PontoCampoAberto(float raioLivre, float inclMax)
    {
        Vector3 o = terreno.transform.position, s = dados.size;
        for (int t = 0; t < 4000; t++)
        {
            float x = Faixa(o.x + 30f, o.x + s.x - 30f), z = Faixa(o.z + 30f, o.z + s.z - 30f);
            if (PesoFora(x, z) < 0.99f || limite.DistanciaDaBorda(x, z) > -60f) continue;
            if (EmZona(x, z, 20f) || NaEstrada(x, z)) continue;
            float nx = (x - o.x) / s.x, nz = (z - o.z) / s.z;
            if (dados.GetSteepness(nx, nz) > inclMax || dados.GetInterpolatedHeight(nx, nz) < 1f) continue;
            if (!Livre(x, z, raioLivre)) continue;
            return new Vector3(x, Altura(x, z), z);
        }
        return Vector3.zero;
    }

    static GameObject PrefabURP(string caminho, string id)
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (src == null) { avisos.Add("Não achei o animal " + caminho); return null; }
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        ConverterRenderers(inst, id, false);
        foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        GameObject p = PrefabUtility.SaveAsPrefabAsset(inst, PASTA_PREFABS + "/Animal_" + id + ".prefab");
        Object.DestroyImmediate(inst);
        return p;
    }

    static AnimationClip Clipe(string fbx)
    {
        foreach (Object ob in AssetDatabase.LoadAllAssetsAtPath(fbx))
        {
            AnimationClip c = ob as AnimationClip;
            if (c != null && !c.name.StartsWith("__preview__")) return c;
        }
        return null;
    }

    // ------------------------------------------------------------------
    //  animais de fazenda (modelos Tripo: ovelha, cabra, porco e cavalo) em rebanhos nos campos abertos
    // ------------------------------------------------------------------
    static int AnimaisDeFazenda(Transform grupo)
    {
        // pasta, nome, comprimento do corpo (m), rebanhos, mínimo e máximo por rebanho, velocidade (m/s), balanço do passo
        object[][] def =
        {
            new object[] { "ovelha", "Ovelha", 1.3f, 3, 3, 5, 0.8f, 1.0f },
            new object[] { "cabra", "Cabra", 1.2f, 2, 2, 4, 1.0f, 1.1f },
            new object[] { "porco", "Porco", 1.5f, 2, 2, 3, 0.8f, 1.0f },
            new object[] { "cavalo", "Cavalo", 2.5f, 2, 1, 3, 1.4f, 1.2f },
        };
        int total = 0;
        foreach (object[] d in def)
        {
            string pasta = P + "Animais Fazenda/" + (string)d[0];
            string[] fbx = AssetDatabase.FindAssets("t:Model", new[] { pasta });
            string caminhoFbx = fbx.Length > 0 ? AssetDatabase.GUIDToAssetPath(fbx[0]) : null;
            if (caminhoFbx != null)
            {   // preciso ler os vértices para descobrir para que lado o animal olha
                ModelImporter mi = AssetImporter.GetAtPath(caminhoFbx) as ModelImporter;
                if (mi != null && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }
            }
            GameObject prefab = caminhoFbx != null ? AssetDatabase.LoadAssetAtPath<GameObject>(caminhoFbx) : null;
            if (prefab == null) { avisos.Add("Modelo do(a) " + (string)d[1] + " não encontrado em " + pasta + " (espere o Unity importar e rode de novo)."); continue; }
            Material pele = PeleDoAnimal(pasta, (string)d[1]);
            float giro = float.NaN;
            for (int h = 0; h < (int)d[3]; h++)
            {
                Vector3 c = PontoDePasto(9f); if (c == Vector3.zero) continue;
                int n = rng.Next((int)d[4], (int)d[5] + 1);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = c + new Vector3(Faixa(-6f, 6f), 0f, Faixa(-6f, 6f)); p.y = Altura(p.x, p.z);
                    if (NaEstrada(p.x, p.z)) continue;
                    GameObject pai = new GameObject((string)d[1]);
                    pai.transform.SetParent(grupo, false);
                    pai.transform.position = p;
                    GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.transform);
                    g.name = "Modelo";
                    foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
                    {
                        Material[] ms = new Material[r.sharedMaterials.Length];
                        for (int k = 0; k < ms.Length; k++) ms[k] = pele;
                        r.sharedMaterials = ms;
                    }
                    g.transform.localScale = Vector3.one;
                    g.transform.localPosition = Vector3.zero;
                    g.transform.localRotation = prefab.transform.localRotation;
                    if (float.IsNaN(giro)) giro = GiroParaFrente(g);       // calculado uma vez por animal (todos os modelos iguais)
                    g.transform.rotation = Quaternion.AngleAxis(giro, Vector3.up) * g.transform.rotation;   // cabeça para o +Z do pai
                    Bounds b = BoundsDe(g);
                    float comp = Mathf.Max(b.size.x, b.size.z);
                    float alvo = (float)d[2] * Faixa(0.9f, 1.1f);
                    if (comp > 0.0001f) g.transform.localScale = Vector3.one * (alvo / comp);
                    b = BoundsDe(g);
                    g.transform.position += Vector3.up * (p.y - b.min.y) + new Vector3(p.x - b.center.x, 0f, p.z - b.center.z);   // pés no chão, centro sobre o pai
                    AnimalDeFazenda a = pai.AddComponent<AnimalDeFazenda>();
                    a.raio = 14f; a.velocidade = (float)d[6]; a.comprimento = (float)d[2]; a.amplitudePasso = (float)d[7];
                    LODGroup lg = pai.AddComponent<LODGroup>();
                    lg.SetLODs(new[] { new LOD(0.02f, g.GetComponentsInChildren<Renderer>()) });   // some de longe (os modelos são pesados)
                    lg.RecalculateBounds();
                    total++;
                }
            }
        }
        Debug.Log("[Veg] Animais de fazenda colocados: " + total);
        return total;
    }

    // ângulo (graus, em torno de Y) que gira o modelo para a CABEÇA apontar para +Z.
    // Eixo do corpo = direção de maior comprimento no chão; cabeça = a ponta mais ALTA (cabeça/pescoço erguidos, garupa mais baixa).
    static float GiroParaFrente(GameObject inst)
    {
        List<Vector3> pts = new List<Vector3>();
        foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>())
        {
            Mesh me = mf.sharedMesh; if (me == null) continue;
            Vector3[] vs;
            try { vs = me.vertices; } catch { continue; }
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < vs.Length; i += 6) pts.Add(m.MultiplyPoint3x4(vs[i]));
        }
        if (pts.Count < 100) return 0f;
        float mx = 0f, mz = 0f;
        foreach (Vector3 p in pts) { mx += p.x; mz += p.z; }
        mx /= pts.Count; mz /= pts.Count;
        float cxx = 0f, czz = 0f, cxz = 0f;
        foreach (Vector3 p in pts) { float dx = p.x - mx, dz = p.z - mz; cxx += dx * dx; czz += dz * dz; cxz += dx * dz; }
        float th = 0.5f * Mathf.Atan2(2f * cxz, cxx - czz);
        Vector2 eixo = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
        float lo = float.MaxValue, hi = float.MinValue;
        float[] proj = new float[pts.Count];
        for (int i = 0; i < pts.Count; i++) { proj[i] = (pts[i].x - mx) * eixo.x + (pts[i].z - mz) * eixo.y; lo = Mathf.Min(lo, proj[i]); hi = Mathf.Max(hi, proj[i]); }
        float L = hi - lo, altoLo = float.MinValue, altoHi = float.MinValue;
        for (int i = 0; i < pts.Count; i++)
        {
            if (proj[i] <= lo + L * 0.25f) altoLo = Mathf.Max(altoLo, pts[i].y);
            if (proj[i] >= hi - L * 0.25f) altoHi = Mathf.Max(altoHi, pts[i].y);
        }
        Vector2 frente = altoHi >= altoLo ? eixo : -eixo;
        Vector3 f = new Vector3(frente.x, 0f, frente.y);
        return Vector3.SignedAngle(f, Vector3.forward, Vector3.up);
    }

    // pasto: campo aberto e plano um pouco depois da vila (onde o jogador passa), sem estrada, longe de zonas protegidas
    static Vector3 PontoDePasto(float raioLivre)
    {
        if (!temVila) return PontoCampoAberto(raioLivre, 10f);
        Vector3 o = terreno.transform.position, sz = dados.size;
        for (int t = 0; t < 250; t++)
        {
            float a = Faixa(0f, Mathf.PI * 2f), r = Faixa(140f, 280f);
            float x = centroVila.x + Mathf.Cos(a) * r, z = centroVila.z + Mathf.Sin(a) * r;
            if (x < o.x + 20f || z < o.z + 20f || x > o.x + sz.x - 20f || z > o.z + sz.z - 20f) continue;
            float nx = (x - o.x) / sz.x, nz = (z - o.z) / sz.z;
            if (dados.GetSteepness(nx, nz) > 14f || dados.GetInterpolatedHeight(nx, nz) < 0.8f) continue;
            if (EmZona(x, z, 10f) || NaEstrada(x, z)) continue;
            if (!Livre(x, z, raioLivre)) continue;
            Registrar(x, z, raioLivre);
            return new Vector3(x, Altura(x, z), z);
        }
        return PontoCampoAberto(raioLivre, 10f);
    }

    static Bounds BoundsDe(GameObject g)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(g.transform.position, Vector3.zero); bool primeiro = true;
        foreach (Renderer r in rs) { if (primeiro) { b = r.bounds; primeiro = false; } else b.Encapsulate(r.bounds); }
        return b;
    }

    // material URP com as texturas do próprio animal (cor, normal); cria uma vez e reaproveita
    static Material PeleDoAnimal(string pasta, string nome)
    {
        string caminho = pasta + "/Pele_" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, caminho); }
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { pasta }))
        {
            string tp = AssetDatabase.GUIDToAssetPath(guid); string tn = tp.ToLower();
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            if (tex == null) continue;
            if (tn.Contains("basecolor")) { m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); }
            else if (tn.Contains("normal"))
            {
                TextureImporter ti = AssetImporter.GetAtPath(tp) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); }
                m.SetTexture("_BumpMap", tex); m.EnableKeyword("_NORMALMAP");
            }
        }
        m.SetFloat("_Smoothness", 0.25f); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Animais(Transform pai)
    {
        Transform grupo = new GameObject("Animais").transform; grupo.SetParent(pai, false);
        int vacas = 0, bandos = 0, esquilos = 0, javalis = 0;

        // vacas em campos abertos, em rebanhos de 3 a 5
        GameObject vaca = PrefabURP(P + "Shepherd_Valley/HDRP/Prefabs/P_Cow.prefab", "vaca");
        if (vaca != null)
        {
            string anim = P + "Shepherd_Valley/HDRP/Animations/Cow/";
            AnimationClip parado = Clipe(anim + "A_Cow_Idle_01.fbx"), andando = Clipe(anim + "A_Cow_Walk_01.fbx"), comendo = Clipe(anim + "A_Cow_Eating_01.fbx");
            for (int h = 0; h < 4; h++)
            {
                Vector3 c = PontoCampoAberto(12f, 10f); if (c == Vector3.zero) continue;
                int n = rng.Next(3, 6);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = c + new Vector3(Faixa(-9f, 9f), 0f, Faixa(-9f, 9f)); p.y = Altura(p.x, p.z);
                    GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(vaca, grupo);
                    g.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Faixa(0f, 360f), 0f));
                    g.transform.localScale = Vector3.one * Faixa(0.95f, 1.1f);
                    AnimalAmbiente aa = g.AddComponent<AnimalAmbiente>();
                    aa.parado = parado; aa.andando = andando; aa.comendo = comendo; aa.raio = 16f;
                    vacas++;
                }
            }
        }

        int fazenda = AnimaisDeFazenda(grupo);

        // bandos de estorninhos sobre os campos
        GameObject passaro = AssetDatabase.LoadAssetAtPath<GameObject>(P + "MaekoFlock/Starling/Starling.prefab");
        if (passaro != null)
            for (int b = 0; b < 3; b++)
            {
                Vector3 c = PontoCampoAberto(4f, 25f); if (c == Vector3.zero) continue;
                GameObject g = new GameObject("Bando de Estorninhos " + (b + 1));
                g.transform.SetParent(grupo, false); g.transform.position = c + Vector3.up * 38f;
                MaekoStudio.Flock.MaekoFlock f = g.AddComponent<MaekoStudio.Flock.MaekoFlock>();
                f.birdPrefab = passaro; f.count = 28; f.anchor = g.transform; f.boundsRadius = 45f; f.swirlRadius = 30f;
                bandos++;
            }
        else avisos.Add("Não achei o prefab dos estorninhos.");

        // esquilos e javalis (parados) nas bordas da mata
        GameObject esq = PrefabURPSemConverter(P + "Furry Squirrel/Prefab/Squirrel_URP.prefab");
        RuntimeAnimatorController ctrlEsq = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(P + "Furry Squirrel/Animations/Animations_Squirrel.controller");
        for (int i = 0; i < 6 && esq != null; i++)
        {
            Vector3 c = PontoCampoAberto(2f, 20f); if (c == Vector3.zero) continue;
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(esq, grupo);
            g.transform.SetPositionAndRotation(c, Quaternion.Euler(0f, Faixa(0f, 360f), 0f));
            Animator a = g.GetComponentInChildren<Animator>();
            if (a != null && a.runtimeAnimatorController == null && ctrlEsq != null) a.runtimeAnimatorController = ctrlEsq;
            esquilos++;
        }
        GameObject jav = PrefabURPSemConverter(P + "Wild Boar/Prefabs/URP/Mobile/Wild_Boar.prefab");
        for (int i = 0; i < 6 && jav != null; i++)
        {
            Vector3 c = PontoCampoAberto(3f, 18f); if (c == Vector3.zero) continue;
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(jav, grupo);
            g.transform.SetPositionAndRotation(c, Quaternion.Euler(0f, Faixa(0f, 360f), 0f));
            javalis++;
        }
        relatorio.Add("Animais: " + fazenda + " animais de fazenda (ovelha, cabra, porco, cavalo), " + vacas + " vacas, " + bandos + " bandos de pássaros, " + esquilos + " esquilos, " + javalis + " javalis.");
    }

    static GameObject PrefabURPSemConverter(string caminho)
    {
        GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (g == null) avisos.Add("Não achei " + caminho);
        return g;
    }
}
#endif
