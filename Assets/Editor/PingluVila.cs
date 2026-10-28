// =====================================================================
//  PingluVila.cs  (versão 2)  -  Vila medieval sombria para o Pinglu RPG
// ---------------------------------------------------------------------
//  Inspirada nas referências: chalé de pedra com telhado de escamas,
//  casa alta em enxaimel com térreo de pedra rústica, capela com torre
//  do sino, ferraria, campo de treino e objetos de guerreiro.
//
//  COMO USAR:
//   1. Extraia o .zip na pasta do projeto (cria Assets/Cenario/Vila/Texturas
//      e substitui Assets/Editor/PingluVila.cs)
//   2. Abra a SampleScene e clique em:  Pinglu > 3. Construir vila
//      (se a vila antiga existir, ele pergunta e troca pela nova)
//   3. Dê Play, olhe o resultado e salve a cena (Ctrl + S)
//
//  Para remover:  Pinglu > 4. Desfazer vila
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    // ------------------------------------------------------------------
    //  AJUSTES
    // ------------------------------------------------------------------
    const int SEMENTE = 77;              // mude para uma vila diferente
    const float DISTANCIA_DO_JOGADOR = 95f;
    const float RAIO_PRACA = 10f;
    const int LUZES_MAXIMAS = 8;

    // ------------------------------------------------------------------
    const string PASTA = "Assets/Cenario/Vila";
    const string PASTA_TEX = PASTA + "/Texturas";
    const string PASTA_MAT = PASTA + "/Materiais_v2";
    const string PASTA_MALHAS = PASTA + "/Malhas";
    const string BACKUP = PASTA + "/Backup_Terreno_Vila.asset";
    const string NOME_RAIZ = "Vila_Gerada";
    const string PREFIXO_OCULTO = "[oculto pela vila] ";
    const string CAMADA_ESTRADA = "Assets/TerrainSampleAssets/TerrainLayers/Soil_Rocks_TerrainLayer.terrainlayer";
    const string CAMADA_PRACA = "Assets/TerrainSampleAssets/TerrainLayers/Pebbles_A_TerrainLayer.terrainlayer";

    // índices dos materiais
    const int PEDRA_BLOCOS = 0, PEDRA_RUSTICA = 1, PEDRA_ESCURA = 2, REBOCO_CREME = 3, REBOCO_CINZA = 4,
              MAD_ESC = 5, MAD_VERM = 6, TABUAS = 7, TELHA_ESCAMA = 8, TELHA_ARDOSIA = 9, TELHA_MUSGO = 10,
              FERRO = 11, LAMINA = 12, COURO = 13, PALHA = 14, TEC_VERM = 15, TEC_AZUL = 16, TEC_OCRE = 17,
              AGUA = 18, FOLHAGEM = 19, BRASA = 20, LANTERNA = 21,
              P_JANELA_ARCO = 22, P_VITRAL = 23, P_GRADE = 24, P_VENEZIANA = 25, P_PORTA_ARCO = 26,
              P_PORTA = 27, P_BRASAO = 28, P_ARCO_SINO = 29;
    const int TOTAL_MATS = 30;

    // nome, textura, cor, lisura, metal, metros por repetição (0 = painel), veio no comprimento, recorte, emissão
    struct DefMat
    {
        public string nome, tex, emisTex; public Color cor, emis; public float lisura, metal, tile, normal; public bool veio, recorte;
    }

    static Material[] mats;
    static bool[] veioMat;
    static bool faltouTextura;
    static System.Random rng;
    static Terrain terreno;
    static TerrainData dados;
    static Transform raiz;
    static int luzesCriadas;
    static int contadorMalhas;
    static readonly List<string> avisos = new List<string>();

    struct Forma { public Vector2 a, b; public float raio; public int camada; } // camada: 0 praça, 1 terra
    static readonly List<Forma> formas = new List<Forma>();

    // tipos: 0 chalé de pedra, 1 casa alta, 2 taverna, 3 capela, 4 celeiro, 5 ferraria
    class Casa
    {
        public Vector3 pos; public Quaternion rot; public float W, D; public int tipo, telhado; public float inclinacao;
        public bool horta, brasao;
    }

    static Vector3 posTreino; static Quaternion rotTreino; static bool temTreino;

    // ==================================================================
    //  MENU
    // ==================================================================
    [MenuItem("Pinglu/3. Construir vila")]
    public static void Construir()
    {
        terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain na cena. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;

        if (GameObject.Find(NOME_RAIZ) != null)
        {
            if (!EditorUtility.DisplayDialog("Pinglu", "Já existe uma vila na cena. Quer trocar pela nova versão?", "Sim, trocar", "Cancelar")) return;
            Desfazer(false);
        }
        if (!EditorUtility.DisplayDialog("Pinglu - Construir vila",
            "Vou construir a vila medieval: chalés de pedra, casas altas em enxaimel, taverna, capela com torre, celeiro, ferraria, " +
            "campo de treino, praça com poço e feira, hortas, postes e estrada até onde o jogador nasce.\n\n" +
            "Antes, guardo um backup do terreno para poder desfazer.",
            "Pode construir", "Cancelar")) return;

        avisos.Clear(); formas.Clear(); luzesCriadas = 0; contadorMalhas = 0; temTreino = false;
        malhaProp = new Mesh[8]; matsProp = new Material[8][]; pastaMalhas = PASTA_MALHAS; prefixoOculto = PREFIXO_OCULTO;
        rng = new System.Random(SEMENTE);

        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando texturas e materiais...", 0.05f);
            PrepararPastas();
            ConfigurarTexturas();
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP) != null) AssetDatabase.DeleteAsset(BACKUP);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dados), BACKUP)) throw new System.Exception("Não consegui fazer backup do terreno.");
            CriarMateriais();
            if (faltouTextura) avisos.Add("Algumas texturas não foram encontradas em " + PASTA_TEX + ". Extraiu o .zip na pasta do projeto? A vila foi feita só com cores.");

            Vector3 spawn = AcharJogador();
            EditorUtility.DisplayProgressBar("Pinglu", "Procurando um lugar plano...", 0.12f);
            Vector3 centro = AcharLugar(spawn);
            Vector3 paraJogador = Plano(spawn - centro).normalized;

            Undo.IncrementCurrentGroup();
            int grupo = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(dados, "Construir vila");
            GameObject r = new GameObject(NOME_RAIZ);
            Undo.RegisterCreatedObjectUndo(r, "Construir vila");
            raiz = r.transform;

            EditorUtility.DisplayProgressBar("Pinglu", "Planejando a vila...", 0.2f);
            List<Casa> casas = PlanejarCasas(centro, paraJogador);
            List<Vector3> estrada = PlanejarEstrada(centro, paraJogador, spawn);
            PlanejarTreino(estrada, centro);

            EditorUtility.DisplayProgressBar("Pinglu", "Aplainando o terreno...", 0.3f);
            Aplainar(centro);
            centro.y = Altura(centro.x, centro.z);

            EditorUtility.DisplayProgressBar("Pinglu", "Limpando árvores e grama...", 0.4f);
            LimparArvores(centro, 44f);
            LimparDetalhes(centro);
            EditorUtility.DisplayProgressBar("Pinglu", "Pintando estrada e praça...", 0.5f);
            PintarChao();
            OcultarCenarioNaArea(centro, 46f);

            Transform pastaCasas = Pasta("Construcoes");
            for (int i = 0; i < casas.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Pinglu", "Construindo... (" + (i + 1) + "/" + casas.Count + ")", 0.55f + 0.25f * i / casas.Count);
                ConstruirCasa(casas[i], pastaCasas);
            }

            EditorUtility.DisplayProgressBar("Pinglu", "Montando a praça e os detalhes...", 0.85f);
            MontarPraca(centro, paraJogador);
            MontarEstrada(estrada, centro);
            MontarTreino();
            DetalhesDasCasas(casas);

            terreno.Flush();
            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
            Undo.CollapseUndoOperations(grupo);
            Selection.activeGameObject = r;
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.LookAt(centro + Vector3.up * 6f, Quaternion.Euler(30f, 30f, 0f), 50f);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console). Use Pinglu > 4. Desfazer vila.");
        }
        finally { EditorUtility.ClearProgressBar(); }

        string msg = "Vila pronta! Siga a estrada de terra a partir de onde o jogador nasce.\n\nSe gostar, salve com Ctrl + S.\nSe não gostar, use Pinglu > 4. Desfazer vila.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        foreach (string a in avisos) Debug.LogWarning("[Pinglu] " + a);
    }

    [MenuItem("Pinglu/4. Desfazer vila")]
    public static void DesfazerMenu() { Desfazer(true); }

    static void Desfazer(bool perguntar)
    {
        if (perguntar && !EditorUtility.DisplayDialog("Pinglu", "Remover a vila e restaurar o terreno como estava antes dela?", "Sim", "Cancelar")) return;
        terreno = Terrain.activeTerrain;

        GameObject r = GameObject.Find(NOME_RAIZ);
        if (r != null) Undo.DestroyObjectImmediate(r);

        TerrainData bk = AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP);
        if (terreno != null && bk != null)
        {
            TerrainData td = terreno.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Desfazer vila");
            int hr = bk.heightmapResolution;
            td.SetHeights(0, 0, bk.GetHeights(0, 0, hr, hr));
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
        }

        foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g == null || !g.scene.IsValid() || !g.name.StartsWith(PREFIXO_OCULTO)) continue;
            Undo.RecordObject(g, "Desfazer vila");
            g.name = g.name.Substring(PREFIXO_OCULTO.Length);
            g.SetActive(true);
        }

        AssetDatabase.SaveAssets();
        if (terreno != null) EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        if (perguntar) EditorUtility.DisplayDialog("Pinglu", "Vila removida. Salve a cena (Ctrl + S) para confirmar.", "OK");
    }

    // ==================================================================
    //  PREPARAÇÃO, TEXTURAS E MATERIAIS
    // ==================================================================
    static void PrepararPastas()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Cenario")) AssetDatabase.CreateFolder("Assets", "Cenario");
        if (!AssetDatabase.IsValidFolder(PASTA)) AssetDatabase.CreateFolder("Assets/Cenario", "Vila");
        if (!AssetDatabase.IsValidFolder(PASTA_MAT)) AssetDatabase.CreateFolder(PASTA, "Materiais_v2");
        if (AssetDatabase.IsValidFolder(PASTA_MALHAS)) AssetDatabase.DeleteAsset(PASTA_MALHAS);
        AssetDatabase.CreateFolder(PASTA, "Malhas");
    }

    static void ConfigurarTexturas()
    {
        if (!AssetDatabase.IsValidFolder(PASTA_TEX)) return;
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PASTA_TEX });
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string g in guids)
            {
                string caminho = AssetDatabase.GUIDToAssetPath(g);
                TextureImporter ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
                if (ti == null) continue;
                string nome = System.IO.Path.GetFileNameWithoutExtension(caminho);
                bool mudou = false;
                bool normal = nome.EndsWith("_normal");
                bool painel = nome.StartsWith("p_");
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; mudou = true; }
                TextureWrapMode wrap = painel ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                if (ti.wrapMode != wrap) { ti.wrapMode = wrap; mudou = true; }
                if (painel && !normal && nome.EndsWith("_cor") && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; mudou = true; }
                if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; mudou = true; }
                if (ti.anisoLevel < 4) { ti.anisoLevel = 4; mudou = true; }
                if (mudou) ti.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
    }

    static DefMat D(string nome, string tex, Color cor, float lisura, float tile)
    {
        return new DefMat { nome = nome, tex = tex, cor = cor, lisura = lisura, tile = tile, normal = 1f, emis = Color.black };
    }

    static void CriarMateriais()
    {
        faltouTextura = false;
        DefMat[] d = new DefMat[TOTAL_MATS];
        d[PEDRA_BLOCOS] = D("Pedra_Blocos", "pedra_blocos", Color.white, 0.12f, 3f);
        d[PEDRA_RUSTICA] = D("Pedra_Rustica", "pedra_rustica", Color.white, 0.1f, 2.2f);
        d[PEDRA_ESCURA] = D("Pedra_Escura", "pedra_escura", Color.white, 0.12f, 2.5f);
        d[REBOCO_CREME] = D("Reboco_Creme", "reboco_creme", Color.white, 0.05f, 3f);
        d[REBOCO_CINZA] = D("Reboco_Cinza", "reboco_cinza", Color.white, 0.05f, 3f);
        d[MAD_ESC] = D("Madeira_Escura", "madeira_escura", Color.white, 0.12f, 2f); d[MAD_ESC].veio = true;
        d[MAD_VERM] = D("Madeira_Avermelhada", "madeira_avermelhada", Color.white, 0.15f, 2f); d[MAD_VERM].veio = true;
        d[TABUAS] = D("Tabuas", "tabuas", Color.white, 0.1f, 2f);
        d[TELHA_ESCAMA] = D("Telhado_Escamas", "telhado_escamas", Color.white, 0.2f, 2f);
        d[TELHA_ARDOSIA] = D("Telhado_Ardosia", "telhado_ardosia", Color.white, 0.18f, 2.2f);
        d[TELHA_MUSGO] = D("Telhado_Musgo", "telhado_musgo", Color.white, 0.12f, 2.2f);
        d[FERRO] = D("Ferro", "ferro", Color.white, 0.35f, 1f); d[FERRO].metal = 0.7f;
        d[LAMINA] = D("Lamina", null, new Color(0.62f, 0.64f, 0.67f), 0.7f, 1f); d[LAMINA].metal = 0.9f;
        d[COURO] = D("Couro", "tecido", new Color(0.30f, 0.18f, 0.11f), 0.2f, 0.5f);
        d[PALHA] = D("Palha", "palha", Color.white, 0.05f, 1f);
        d[TEC_VERM] = D("Tecido_Vermelho", "tecido", new Color(0.45f, 0.08f, 0.07f), 0.05f, 1f);
        d[TEC_AZUL] = D("Tecido_Azul", "tecido", new Color(0.16f, 0.22f, 0.30f), 0.05f, 1f);
        d[TEC_OCRE] = D("Tecido_Ocre", "tecido", new Color(0.55f, 0.42f, 0.20f), 0.05f, 1f);
        d[AGUA] = D("Agua", null, new Color(0.05f, 0.10f, 0.12f), 0.95f, 1f);
        d[FOLHAGEM] = D("Folhagem", "palha", new Color(0.35f, 0.55f, 0.25f), 0.05f, 0.6f);
        d[BRASA] = D("Brasa", null, new Color(0.15f, 0.04f, 0.02f), 0.3f, 1f); d[BRASA].emis = new Color(1f, 0.35f, 0.08f) * 3f;
        d[LANTERNA] = D("Lanterna", null, new Color(1f, 0.85f, 0.5f), 0.5f, 1f); d[LANTERNA].emis = new Color(1f, 0.65f, 0.28f) * 2.5f;
        d[P_JANELA_ARCO] = D("Janela_Arco", "p_janela_arco", Color.white, 0.6f, 0f); d[P_JANELA_ARCO].recorte = true; d[P_JANELA_ARCO].emisTex = "p_janela_arco_emissao"; d[P_JANELA_ARCO].emis = Color.white * 1.3f;
        d[P_VITRAL] = D("Vitral", "p_vitral", Color.white, 0.6f, 0f); d[P_VITRAL].emisTex = "p_vitral_emissao"; d[P_VITRAL].emis = Color.white * 1.3f;
        d[P_GRADE] = D("Janela_Grade", "p_grade", Color.white, 0.3f, 0f);
        d[P_VENEZIANA] = D("Veneziana", "p_veneziana", Color.white, 0.15f, 0f);
        d[P_PORTA_ARCO] = D("Porta_Arco", "p_porta_arco", Color.white, 0.15f, 0f); d[P_PORTA_ARCO].recorte = true;
        d[P_PORTA] = D("Porta", "p_porta", Color.white, 0.15f, 0f);
        d[P_BRASAO] = D("Brasao", "p_brasao", Color.white, 0.3f, 0f); d[P_BRASAO].recorte = true;
        d[P_ARCO_SINO] = D("Arco_Sino", "p_arco_sino", Color.white, 0.1f, 0f); d[P_ARCO_SINO].recorte = true;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new System.Exception("Não achei o shader URP/Lit.");
        mats = new Material[TOTAL_MATS];
        veioMat = new bool[TOTAL_MATS];
        for (int i = 0; i < TOTAL_MATS; i++)
        {
            DefMat def = d[i];
            veioMat[i] = def.veio;
            string caminho = PASTA_MAT + "/" + def.nome + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            bool novo = m == null;
            if (novo) m = new Material(lit);
            else m.shader = lit;

            m.SetColor("_BaseColor", def.cor);
            m.SetFloat("_Smoothness", def.lisura);
            m.SetFloat("_Metallic", def.metal);
            Vector2 escala = def.tile > 0f ? Vector2.one / def.tile : Vector2.one;

            if (!string.IsNullOrEmpty(def.tex))
            {
                Texture2D cor = AssetDatabase.LoadAssetAtPath<Texture2D>(PASTA_TEX + "/" + def.tex + "_cor.png");
                Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(PASTA_TEX + "/" + def.tex + "_normal.png");
                if (cor == null) faltouTextura = true;
                m.SetTexture("_BaseMap", cor); m.SetTexture("_MainTex", cor);
                m.SetTextureScale("_BaseMap", escala); m.SetTextureScale("_MainTex", escala);
                if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", def.normal); m.EnableKeyword("_NORMALMAP"); }
                else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            }

            if (def.recorte)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            if (def.emis.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", def.emis);
                if (!string.IsNullOrEmpty(def.emisTex))
                {
                    Texture2D em = AssetDatabase.LoadAssetAtPath<Texture2D>(PASTA_TEX + "/" + def.emisTex + ".png");
                    m.SetTexture("_EmissionMap", em);
                    if (em == null) m.SetColor("_EmissionColor", Color.black);
                }
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            if (novo) AssetDatabase.CreateAsset(m, caminho);
            else EditorUtility.SetDirty(m);
            mats[i] = m;
        }
        AssetDatabase.SaveAssets();
    }

    // ==================================================================
    //  PLANEJAMENTO
    // ==================================================================
    static List<Casa> PlanejarCasas(Vector3 c, Vector3 paraJogador)
    {
        List<Casa> casas = new List<Casa>();
        float anguloEntrada = Mathf.Atan2(paraJogador.x, paraJogador.z) * Mathf.Rad2Deg;

        // anel de dentro: 8 construções viradas para a praça (a vaga 0 é a entrada)
        for (int k = 1; k <= 8; k++)
        {
            Casa h = new Casa();
            if (k == 4) { h.tipo = 3; h.W = 6.2f; h.D = 10f; h.inclinacao = 48f; h.telhado = TELHA_MUSGO; }
            else if (k == 2) { h.tipo = 2; h.W = 8.2f; h.D = 13f; h.inclinacao = 55f; h.telhado = TELHA_ARDOSIA; h.brasao = true; }
            else NovaCasa(h, k % 2 == 0 ? 1 : 0);
            float ang = anguloEntrada + k * 40f + Faixa(-3f, 3f);
            float extra = h.tipo == 3 ? 1.5f : 0f; // degraus da capela
            float raio = RAIO_PRACA + 6.5f + h.D * 0.5f + extra + Faixa(0f, 1.2f);
            PosicionarVirado(h, c, ang, raio);
            casas.Add(h);
        }

        // anel de fora
        float[] angs = { 62f, 118f, 205f, 250f, 302f };
        int[] tipos = { 5, 0, 4, 1, 0 };
        for (int k = 0; k < angs.Length; k++)
        {
            Casa h = new Casa();
            if (tipos[k] == 4) { h.tipo = 4; h.W = 8f; h.D = 11f; h.inclinacao = 46f; h.telhado = TELHA_ARDOSIA; }
            else if (tipos[k] == 5) { h.tipo = 5; h.W = 5f; h.D = 6f; h.inclinacao = 32f; h.telhado = TELHA_ARDOSIA; }
            else { NovaCasa(h, tipos[k]); h.horta = true; }
            float raio = 34f + h.D * 0.5f;
            PosicionarVirado(h, c, anguloEntrada + angs[k] + Faixa(-4f, 4f), raio);
            casas.Add(h);
        }

        foreach (Casa h in casas)
        {
            float largura = h.tipo == 3 ? h.W + 4f : h.W;
            formas.Add(Circulo(h.pos, Mathf.Max(largura, h.D) * 0.5f + 1.2f, 1));
            Vector3 frente = h.rot * Vector3.forward;
            Vector3 porta = h.pos + frente * (h.D * 0.5f + (h.tipo == 3 ? 2f : 0.8f));
            Vector3 dirPraca = Plano(c - porta).normalized;
            Vector3 fim = c - dirPraca * (RAIO_PRACA - 1f);
            formas.Add(Segmento(porta, fim, 1.1f, 1));
            if (h.horta) formas.Add(Circulo(h.pos - frente * (h.D * 0.5f + 4f), 4.2f, 1));
        }
        formas.Add(Circulo(c, RAIO_PRACA, 0));
        return casas;
    }

    static void NovaCasa(Casa h, int tipo)
    {
        h.tipo = tipo;
        if (tipo == 0)
        {
            h.W = Faixa(5.2f, 6.2f); h.D = Faixa(5.8f, 7f); h.inclinacao = Faixa(39f, 44f);
            h.telhado = rng.NextDouble() < 0.65 ? TELHA_ESCAMA : TELHA_ARDOSIA;
        }
        else
        {
            h.W = Faixa(6.4f, 7.3f); h.D = Faixa(10f, 12f); h.inclinacao = Faixa(53f, 58f);
            h.telhado = rng.NextDouble() < 0.6 ? TELHA_ARDOSIA : TELHA_MUSGO;
            h.brasao = rng.NextDouble() < 0.5;
        }
    }

    static void PosicionarVirado(Casa h, Vector3 c, float angulo, float raio)
    {
        Vector3 dir = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward;
        h.pos = c + dir * raio;
        h.rot = Quaternion.LookRotation(-dir, Vector3.up) * Quaternion.Euler(0f, Faixa(-5f, 5f), 0f);
    }

    static void PlanejarTreino(List<Vector3> estrada, Vector3 centro)
    {
        // campo de treino ao lado da estrada, logo depois do portal da vila
        for (int i = 1; i < estrada.Count; i++)
        {
            if (Plano(estrada[i] - centro).magnitude < 50f) continue;
            Vector3 d = Plano(estrada[i] - estrada[i - 1]).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, d);
            posTreino = estrada[i] + lado * 9f;
            rotTreino = Quaternion.LookRotation(-lado);
            temTreino = true;
            formas.Add(Circulo(posTreino, 6.5f, 1));
            return;
        }
    }

    static Vector3 AcharJogador()
    {
        CharacterController cc = UnityEngine.Object.FindFirstObjectByType<CharacterController>();
        if (cc != null) return cc.transform.position;
        avisos.Add("Não achei o jogador; usei a posição (605, 286).");
        return new Vector3(605f, Altura(605f, 286f), 286f);
    }

    // ==================================================================
    //  ESCOLHA DO LUGAR E PLANEJAMENTO
    // ==================================================================
    static Vector3 AcharLugar(Vector3 spawn)
    {
        List<Vector3> evitar = PontosDeInteresseDoCenario();
        Vector3 melhor = Vector3.zero; float melhorNota = float.MaxValue; bool achou = false;
        for (int a = 0; a < 360; a += 15)
        {
            for (float dist = 80f; dist <= 130f; dist += 12.5f)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Vector3 c = spawn + dir * dist;
                if (!DentroDoTerreno(c, 70f)) continue;
                bool perto = false;
                foreach (Vector3 p in evitar) if (Plano(p - c).magnitude < 58f) perto = true;
                if (perto) continue;

                float min = float.MaxValue, max = float.MinValue;
                for (float x = -42f; x <= 42f; x += 6f)
                    for (float z = -42f; z <= 42f; z += 6f)
                    {
                        if (x * x + z * z > 42f * 42f) continue;
                        float h = Altura(c.x + x, c.z + z);
                        if (h < min) min = h; if (h > max) max = h;
                    }
                float nota = (max - min) + Mathf.Abs(dist - DISTANCIA_DO_JOGADOR) * 0.04f;
                if (nota < melhorNota) { melhorNota = nota; melhor = c; achou = true; }
            }
        }
        if (!achou)
        {
            avisos.Add("Não achei um lugar ideal; a vila ficou perto do jogador.");
            melhor = spawn + Vector3.forward * DISTANCIA_DO_JOGADOR;
        }
        if (melhorNota > 14f) avisos.Add("O terreno escolhido é um pouco inclinado; algumas casas ficaram com alicerce mais alto.");
        melhor.y = Altura(melhor.x, melhor.z);
        return melhor;
    }

    static List<Vector3> PontosDeInteresseDoCenario()
    {
        List<Vector3> lista = new List<Vector3>();
        GameObject cen = GameObject.Find("Cenario_Gerado");
        if (cen == null) return lista;
        Transform poi = cen.transform.Find("Pontos de Interesse");
        if (poi == null) return lista;
        foreach (Transform t in poi)
        {
            Bounds b;
            if (MedirBounds(t.gameObject, out b)) lista.Add(b.center);
        }
        return lista;
    }

    static List<Vector3> PlanejarEstrada(Vector3 c, Vector3 paraJogador, Vector3 spawn)
    {
        Vector3 inicio = c + paraJogador * (RAIO_PRACA - 1f);
        Vector3 fim = spawn - paraJogador * 14f;
        Vector3 lado = Vector3.Cross(Vector3.up, paraJogador);
        Vector3 meio = (inicio + fim) * 0.5f + lado * Faixa(-12f, 12f);
        List<Vector3> pts = new List<Vector3>();
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            Vector3 p = (1 - t) * (1 - t) * inicio + 2 * (1 - t) * t * meio + t * t * fim;
            pts.Add(p);
        }
        for (int i = 0; i < pts.Count - 1; i++) formas.Add(Segmento(pts[i], pts[i + 1], 1.9f, 1));
        return pts;
    }

    // ==================================================================
    //  TERRENO
    // ==================================================================
    static void Aplainar(Vector3 c)
    {
        float soma = 0f; int n = 0;
        for (float x = -34f; x <= 34f; x += 4f)
            for (float z = -34f; z <= 34f; z += 4f)
                if (x * x + z * z <= 34f * 34f) { soma += Altura(c.x + x, c.z + z); n++; }
        float alvo = (soma / n - terreno.transform.position.y) / dados.size.y;

        int res = dados.heightmapResolution;
        Vector3 o = terreno.transform.position, s = dados.size;
        float r = 56f;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - r - o.x) / s.x * (res - 1)), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - r - o.z) / s.z * (res - 1)), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + r - o.x) / s.x * (res - 1)), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + r - o.z) / s.z * (res - 1)), 0, res - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        float[,] alt = dados.GetHeights(x0, z0, w, h);
        for (int zz = 0; zz < h; zz++)
            for (int xx = 0; xx < w; xx++)
            {
                float wx = o.x + (x0 + xx) / (float)(res - 1) * s.x;
                float wz = o.z + (z0 + zz) / (float)(res - 1) * s.z;
                float d = new Vector2(wx - c.x, wz - c.z).magnitude;
                float peso = 0.92f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(36f, 55f, d)));
                alt[zz, xx] = Mathf.Lerp(alt[zz, xx], alvo, peso);
            }
        dados.SetHeights(x0, z0, alt);
    }

    static void LimparArvores(Vector3 c, float raio)
    {
        Vector3 o = terreno.transform.position, s = dados.size;
        List<TreeInstance> manter = new List<TreeInstance>();
        foreach (TreeInstance ti in dados.treeInstances)
        {
            Vector3 p = new Vector3(o.x + ti.position.x * s.x, 0f, o.z + ti.position.z * s.z);
            if (Plano(p - c).magnitude < raio) continue;
            bool naEstrada = false;
            foreach (Forma f in formas) if (Distancia(f, p) < f.raio + 1.5f) { naEstrada = true; break; }
            if (naEstrada) continue;
            manter.Add(ti);
        }
        dados.SetTreeInstances(manter.ToArray(), true); // "true" também recoloca as árvores no chão aplainado
    }

    static void LimparDetalhes(Vector3 c)
    {
        int res = dados.detailResolution;
        int camadas = dados.detailPrototypes.Length;
        if (camadas == 0) return;
        int x0, z0, w, h;
        RegiaoDasFormas(res, c, 48f, out x0, out z0, out w, out h);
        Vector3 o = terreno.transform.position;
        float cel = dados.size.x / res;
        for (int l = 0; l < camadas; l++)
        {
            int[,] mapa = dados.GetDetailLayer(x0, z0, w, h, l);
            for (int zz = 0; zz < h; zz++)
                for (int xx = 0; xx < w; xx++)
                {
                    if (mapa[zz, xx] == 0) continue;
                    Vector3 p = new Vector3(o.x + (x0 + xx + 0.5f) * cel, 0f, o.z + (z0 + zz + 0.5f) * cel);
                    bool limpar = false;
                    foreach (Forma f in formas) if (Distancia(f, p) < f.raio + 0.6f) { limpar = true; break; }
                    if (limpar) mapa[zz, xx] = 0;
                    else if (Plano(p - c).magnitude < 40f) mapa[zz, xx] = mapa[zz, xx] / 2; // grama pisada dentro da vila
                }
            dados.SetDetailLayer(x0, z0, l, mapa);
        }
    }

    static void PintarChao()
    {
        TerrainLayer estrada = AssetDatabase.LoadAssetAtPath<TerrainLayer>(CAMADA_ESTRADA);
        TerrainLayer praca = AssetDatabase.LoadAssetAtPath<TerrainLayer>(CAMADA_PRACA);
        if (estrada == null || praca == null) { avisos.Add("Não achei as texturas de terra/pedrinhas; a estrada não foi pintada."); return; }

        int res = dados.alphamapResolution;
        float[,,] antigo = dados.GetAlphamaps(0, 0, res, res);
        List<TerrainLayer> camadas = new List<TerrainLayer>(dados.terrainLayers);
        int iEstrada = camadas.IndexOf(estrada); if (iEstrada < 0) { camadas.Add(estrada); iEstrada = camadas.Count - 1; }
        int iPraca = camadas.IndexOf(praca); if (iPraca < 0) { camadas.Add(praca); iPraca = camadas.Count - 1; }
        int nAntigo = antigo.GetLength(2);
        dados.terrainLayers = camadas.ToArray();

        float[,,] novo = new float[res, res, camadas.Count];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                for (int l = 0; l < nAntigo; l++) novo[z, x, l] = antigo[z, x, l];

        Vector3 o = terreno.transform.position;
        float cel = dados.size.x / res;
        int rx0, rz0, rw, rh;
        RegiaoDasFormas(res, new Vector3(formas[0].a.x, 0f, formas[0].a.y), 1f, out rx0, out rz0, out rw, out rh);
        for (int z = rz0; z < rz0 + rh; z++)
            for (int x = rx0; x < rx0 + rw; x++)
            {
                Vector3 p = new Vector3(o.x + (x + 0.5f) * cel, 0f, o.z + (z + 0.5f) * cel);
                float ruido = (Mathf.PerlinNoise(p.x * 0.25f, p.z * 0.25f) - 0.5f) * 1.4f;
                float wTerra = 0f, wPraca = 0f;
                foreach (Forma f in formas)
                {
                    if (f.camada < 0) continue;
                    float d = Distancia(f, p) + ruido;
                    float wgt = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(f.raio - 1f, f.raio + 0.6f, d));
                    if (f.camada == 0) wPraca = Mathf.Max(wPraca, wgt); else wTerra = Mathf.Max(wTerra, wgt);
                }
                if (wTerra <= 0f && wPraca <= 0f) continue;
                wTerra = Mathf.Min(wTerra, 1f - wPraca);
                float total = wTerra + wPraca;
                for (int l = 0; l < camadas.Count; l++) novo[z, x, l] *= (1f - total);
                novo[z, x, iEstrada] += wTerra;
                novo[z, x, iPraca] += wPraca;
            }
        dados.SetAlphamaps(0, 0, novo);
    }

    static void OcultarCenarioNaArea(Vector3 c, float raio)
    {
        GameObject cen = GameObject.Find("Cenario_Gerado");
        if (cen == null) return;
        foreach (Transform t in cen.GetComponentsInChildren<Transform>())
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && t.GetComponent<ParticleSystem>() == null) continue;
            Vector3 p = t.position;
            bool ocultar = Plano(p - c).magnitude < raio;
            if (!ocultar) foreach (Forma f in formas) if (Distancia(f, p) < f.raio + 1.5f) { ocultar = true; break; }
            if (!ocultar) continue;
            Undo.RecordObject(t.gameObject, "Construir vila");
            t.gameObject.name = prefixoOculto + t.gameObject.name;
            t.gameObject.SetActive(false);
        }
    }

    // ==================================================================
    // ==================================================================
    //  CONSTRUÇÕES
    // ==================================================================
    static void ConstruirCasa(Casa h, Transform pai)
    {
        // pega o ponto mais baixo e o mais alto do chão embaixo da construção
        float meiaX = h.W * 0.5f + 0.3f, meiaZ = h.D * 0.5f + 0.3f, desX = 0f, desZ = 0f;
        if (h.tipo == 3) { meiaX = h.W * 0.5f + 1.6f; desX = 1.4f; meiaZ = h.D * 0.5f + 0.9f; desZ = 0.6f; }
        if (h.tipo == 1 || h.tipo == 2) { meiaZ += 0.3f; desZ = 0.3f; }
        float min = float.MaxValue, max = float.MinValue;
        for (int i = -2; i <= 2; i++)
            for (int j = -2; j <= 2; j++)
            {
                Vector3 p = h.pos + h.rot * new Vector3(desX + i * meiaX * 0.5f, 0f, desZ + j * meiaZ * 0.5f);
                float a = Altura(p.x, p.z);
                min = Mathf.Min(min, a); max = Mathf.Max(max, a);
            }
        float piso = max + 0.3f;
        float alicerce = piso - min + 0.6f;

        Malha m = new Malha();
        string nome;
        switch (h.tipo)
        {
            case 0: ChalePedra(m, h, alicerce); nome = "Chale de Pedra"; break;
            case 1: CasaAlta(m, h, alicerce, false); nome = "Casa Alta"; break;
            case 2: CasaAlta(m, h, alicerce, true); nome = "Taverna"; break;
            case 3: Capela(m, h, alicerce); nome = "Capela"; break;
            case 4: Celeiro(m, h, alicerce); nome = "Celeiro"; break;
            default: Ferraria(m, h, alicerce); nome = "Ferraria"; break;
        }
        GameObject g = Instanciar(m, nome, new Vector3(h.pos.x, piso, h.pos.z), h.rot, pai, true);
        if (h.tipo == 5) Luz(g.transform, new Vector3(-h.W * 0.25f + 0.2f, 1.4f, -h.D * 0.5f + 1.0f), new Color(1f, 0.45f, 0.15f), 7f, 3f, true);
        if (h.tipo == 2) Luz(g.transform, new Vector3(0f, 2.6f, h.D * 0.5f + 1.2f), new Color(1f, 0.7f, 0.4f), 8f, 2f, false);
    }

    // ---------------- 1) chalé de pedra (referência 1)
    static void ChalePedra(Malha m, Casa h, float alicerce)
    {
        float W = h.W, D = h.D, hP = 3.0f;
        m.Caixa(new Vector3(0f, -alicerce * 0.5f + 0.02f, 0f), new Vector3(W + 0.3f, alicerce + 0.04f, D + 0.3f), PEDRA_ESCURA);
        m.Caixa(new Vector3(0f, hP * 0.5f, 0f), new Vector3(W, hP, D), PEDRA_BLOCOS);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.Caixa(new Vector3(sx * (W * 0.5f + 0.02f), hP * 0.5f, sz * (D * 0.5f + 0.02f)), new Vector3(0.34f, hP, 0.34f), MAD_VERM);
        m.Caixa(new Vector3(0f, hP - 0.14f, D * 0.5f + 0.06f), new Vector3(W + 0.1f, 0.28f, 0.16f), MAD_VERM);
        m.Caixa(new Vector3(0f, hP - 0.14f, -D * 0.5f - 0.06f), new Vector3(W + 0.1f, 0.28f, 0.16f), MAD_VERM);
        m.Caixa(new Vector3(W * 0.5f + 0.06f, hP - 0.14f, 0f), new Vector3(0.16f, 0.28f, D + 0.1f), MAD_VERM);
        m.Caixa(new Vector3(-W * 0.5f - 0.06f, hP - 0.14f, 0f), new Vector3(0.16f, 0.28f, D + 0.1f), MAD_VERM);

        float yC = Telhado(m, new Vector3(0f, hP, 0f), W, D, h.inclinacao, true, 0.45f, 0.5f, h.telhado, PEDRA_BLOCOS, MAD_VERM, false, false);

        // treliça de madeira nos frontões
        float sobe = yC - hP;
        for (int e = -1; e <= 1; e += 2)
        {
            float z = e * (D * 0.5f + 0.07f);
            float y1 = hP + sobe * 0.42f;
            float meia = (yC - y1) / Mathf.Tan(h.inclinacao * Mathf.Deg2Rad) - 0.15f;
            m.Caixa(new Vector3(0f, y1, z), new Vector3(meia * 2f, 0.22f, 0.14f), MAD_VERM);
            m.Caixa(new Vector3(0f, (y1 + yC - 0.25f) * 0.5f, z), new Vector3(0.2f, yC - 0.25f - y1, 0.14f), MAD_VERM);
            for (int s = -1; s <= 1; s += 2)
                Diagonal(m, new Vector3(s * meia * 0.62f, y1 + 0.1f, z), new Vector3(s * 0.08f, y1 + (yC - y1) * 0.62f, z), 0.16f, MAD_VERM);
        }

        PortaRet(m, new Vector3(0f, 0f, D * 0.5f), Vector3.forward, 1.15f, 2.2f, MAD_VERM);
        JanelaGrade(m, new Vector3(W * 0.5f, 1.5f, 0f), Vector3.right, 0.85f, 0.9f, MAD_VERM);
        JanelaGrade(m, new Vector3(-W * 0.5f, 1.5f, 0f), Vector3.left, 0.85f, 0.9f, MAD_VERM);
        JanelaGrade(m, new Vector3(0f, 1.5f, -D * 0.5f), Vector3.back, 0.85f, 0.9f, MAD_VERM);

        // chaminé baixa perto do beiral, como na referência
        float tg = Mathf.Tan(h.inclinacao * Mathf.Deg2Rad);
        float xCh = W * 0.3f, zCh = -D * 0.5f + 0.9f;
        float topoCh = yC - xCh * tg + 1.1f;
        m.Caixa(new Vector3(xCh, (hP - 0.6f + topoCh) * 0.5f, zCh), new Vector3(0.75f, topoCh - (hP - 0.6f), 0.75f), PEDRA_BLOCOS);
        m.Caixa(new Vector3(xCh, topoCh + 0.07f, zCh), new Vector3(0.9f, 0.14f, 0.9f), PEDRA_ESCURA);
    }

    // ---------------- 2) casa alta em enxaimel e taverna (referências 2, 3 e 4)
    static void CasaAlta(Malha m, Casa h, float alicerce, bool taverna)
    {
        float W = h.W, D = h.D, h1 = 3.3f, h2 = 2.9f, j = 0.6f;
        m.Caixa(new Vector3(0f, -alicerce * 0.5f + 0.02f, 0f), new Vector3(W + 0.3f, alicerce + 0.04f, D + 0.3f), PEDRA_ESCURA);
        m.Caixa(new Vector3(0f, h1 * 0.5f, 0f), new Vector3(W, h1, D), PEDRA_RUSTICA);
        m.Caixa(new Vector3(W * 0.5f + 0.05f, 0.55f, 0f), new Vector3(0.1f, 0.18f, D), MAD_ESC);
        m.Caixa(new Vector3(-W * 0.5f - 0.05f, 0.55f, 0f), new Vector3(0.1f, 0.18f, D), MAD_ESC);
        m.Caixa(new Vector3(0f, 0.55f, -D * 0.5f - 0.05f), new Vector3(W, 0.18f, 0.1f), MAD_ESC);

        // térreo da frente: postes, arcos e porta
        int nb = W >= 7f ? 4 : 3;
        float baia = W / nb;
        for (int i = 0; i <= nb; i++)
            m.Caixa(new Vector3(-W * 0.5f + i * baia, h1 * 0.5f, D * 0.5f + 0.07f), new Vector3(0.24f, h1, 0.2f), MAD_ESC);
        m.Caixa(new Vector3(0f, h1 - 0.13f, D * 0.5f + 0.08f), new Vector3(W + 0.1f, 0.26f, 0.2f), MAD_ESC);
        int baiaPorta = taverna ? nb / 2 : rng.Next(0, nb);
        for (int i = 0; i < nb; i++)
        {
            float xc = -W * 0.5f + (i + 0.5f) * baia;
            if (i == baiaPorta)
            {
                m.Painel(new Vector3(xc, 1.25f, D * 0.5f + 0.025f), Vector3.forward, 1.2f, 2.5f, P_PORTA_ARCO);
                m.Caixa(new Vector3(xc, -0.15f, D * 0.5f + 0.45f), new Vector3(1.5f, 0.3f, 0.6f), PEDRA_ESCURA);
            }
            else
            {
                m.Caixa(new Vector3(xc, 0.55f, D * 0.5f + 0.06f), new Vector3(baia - 0.24f, 0.18f, 0.14f), MAD_ESC);
                m.Painel(new Vector3(xc, 1.75f, D * 0.5f + 0.025f), Vector3.forward, 0.72f, 1.45f, P_JANELA_ARCO);
                m.Caixa(new Vector3(xc, 1.0f, D * 0.5f + 0.07f), new Vector3(0.95f, 0.1f, 0.2f), PEDRA_ESCURA);
            }
        }
        // janelas em arco nas laterais
        float[] zsLado = taverna ? new[] { -D * 0.25f, D * 0.2f } : new[] { D * 0.2f };
        foreach (float z in zsLado)
            for (int s = -1; s <= 1; s += 2)
            {
                m.Painel(new Vector3(s * (W * 0.5f + 0.025f), 1.75f, z), new Vector3(s, 0f, 0f), 0.72f, 1.45f, P_JANELA_ARCO);
                m.Caixa(new Vector3(s * (W * 0.5f + 0.07f), 1.0f, z), new Vector3(0.2f, 0.1f, 0.95f), PEDRA_ESCURA);
            }

        // andar de cima em balanço (só para a frente), com vigas aparentes
        m.Caixa(new Vector3(0f, h1 + 0.15f, j * 0.5f), new Vector3(W + 0.2f, 0.3f, D + j + 0.2f), MAD_ESC);
        for (float x = -W * 0.5f + 0.25f; x < W * 0.5f - 0.15f; x += 0.45f)
            m.Caixa(new Vector3(x, h1 - 0.08f, D * 0.5f + j * 0.5f - 0.05f), new Vector3(0.13f, 0.16f, j + 0.25f), MAD_ESC);
        for (int i = 0; i <= nb; i++)
        {
            float x = -W * 0.5f + i * baia;
            Diagonal(m, new Vector3(x, h1 - 1.0f, D * 0.5f + 0.14f), new Vector3(x, h1 - 0.06f, D * 0.5f + j - 0.05f), 0.14f, MAD_ESC);
        }

        float y0 = h1 + 0.3f, topo = y0 + h2, zc = j * 0.5f, Dt = D + j;
        m.Caixa(new Vector3(0f, y0 + h2 * 0.5f, zc), new Vector3(W, h2, Dt), REBOCO_CREME);
        EnxaimelLaterais(m, y0, topo, W, Dt, zc, 0.95f, MAD_ESC);

        // frente de cima: janelas com grade e venezianas entalhadas
        float zf = zc + Dt * 0.5f;
        int nJ = W >= 6.9f ? 3 : 2;
        float passoJ = nJ == 3 ? W / 3.3f : W * 0.4f;
        m.Caixa(new Vector3(0f, y0 + 0.78f, zf + 0.05f), new Vector3(W + 0.06f, 0.16f, 0.12f), MAD_ESC);
        m.Caixa(new Vector3(0f, y0 + 2.05f, zf + 0.05f), new Vector3(W + 0.06f, 0.16f, 0.12f), MAD_ESC);
        for (int k = 0; k < nJ; k++)
        {
            float x = (k - (nJ - 1) * 0.5f) * passoJ;
            JanelaVeneziana(m, new Vector3(x, y0 + 1.42f, zf), Vector3.forward, 0.62f, 1.0f);
            if (k < nJ - 1) m.Caixa(new Vector3(x + passoJ * 0.5f, y0 + h2 * 0.5f, zf + 0.05f), new Vector3(0.18f, h2, 0.12f), MAD_ESC);
        }
        // fundo de cima
        m.Caixa(new Vector3(0f, y0 + 0.78f, zc - Dt * 0.5f - 0.05f), new Vector3(W + 0.06f, 0.16f, 0.12f), MAD_ESC);
        for (int s = -1; s <= 1; s += 2)
            JanelaVeneziana(m, new Vector3(s * W * 0.22f, y0 + 1.42f, zc - Dt * 0.5f), Vector3.back, 0.55f, 0.9f);

        // telhado bem inclinado com beiral virado e acabamento serrilhado
        float yC = Telhado(m, new Vector3(0f, topo, zc), W + 0.1f, Dt, h.inclinacao, true, 0.35f, 0.55f, h.telhado, REBOCO_CREME, MAD_ESC, true, true);
        float tg = Mathf.Tan(h.inclinacao * Mathf.Deg2Rad);
        for (int e = -1; e <= 1; e += 2)
        {
            float zg = zc + e * (Dt * 0.5f + 0.06f);
            Vector3 n = new Vector3(0f, 0f, e);
            float y1 = topo + 1.55f;
            float meia = (yC - y1) / tg - 0.12f;
            m.Caixa(new Vector3(0f, topo + 0.08f, zg), new Vector3(W + 0.08f, 0.16f, 0.12f), MAD_ESC);
            m.Caixa(new Vector3(0f, y1, zg), new Vector3(meia * 2f, 0.2f, 0.12f), MAD_ESC);
            float[] xs = { 0f, -W * 0.25f, W * 0.25f };
            foreach (float x in xs) m.Caixa(new Vector3(x, (topo + y1) * 0.5f, zg), new Vector3(0.16f, y1 - topo, 0.12f), MAD_ESC);
            m.Caixa(new Vector3(0f, (y1 + yC - 0.3f) * 0.5f, zg), new Vector3(0.16f, yC - 0.3f - y1, 0.12f), MAD_ESC);
            for (int s = -1; s <= 1; s += 2)
                Diagonal(m, new Vector3(s * meia * 0.8f, y1 + 0.1f, zg), new Vector3(s * 0.08f, y1 + (yC - y1) * 0.55f, zg), 0.14f, MAD_ESC);
            if (e == 1)
            {
                for (int s = -1; s <= 1; s += 2)
                    m.Painel(new Vector3(s * W * 0.125f, topo + 0.8f, zg + 0.01f), n, 0.68f, 0.85f, P_VITRAL);
                if (h.brasao && yC - y1 > 1.8f)
                    m.Painel(new Vector3(0.0f, y1 + 0.85f, zg + 0.07f), n, 0.72f, 0.9f, P_BRASAO);
            }
            else m.Painel(new Vector3(W * 0.125f, topo + 0.8f, zg - 0.01f), n, 0.6f, 0.75f, P_VITRAL);
        }

        // chaminé(s)
        int nCh = taverna ? 2 : (rng.NextDouble() < 0.6 ? 1 : 0);
        for (int c = 0; c < nCh; c++)
        {
            float xCh = (c == 0 ? 1f : -1f) * W * 0.18f, zCh = zc + (c == 0 ? -Dt * 0.25f : Dt * 0.15f);
            float topoCh = yC - Mathf.Abs(xCh) * tg + 1.7f;
            m.Caixa(new Vector3(xCh, (topo - 0.5f + topoCh) * 0.5f, zCh), new Vector3(0.8f, topoCh - topo + 0.5f, 0.8f), PEDRA_RUSTICA);
            m.Caixa(new Vector3(xCh, topoCh + 0.08f, zCh), new Vector3(0.95f, 0.16f, 0.95f), PEDRA_ESCURA);
        }

        if (taverna)
        {
            // placa pendurada embaixo do balanço
            float x = W * 0.5f - 0.5f, z = D * 0.5f + j;
            m.Caixa(new Vector3(x, h1 - 0.15f, z + 0.5f), new Vector3(0.08f, 0.08f, 1.0f), FERRO);
            m.Caixa(new Vector3(x, h1 - 0.32f, z + 0.25f), new Vector3(0.02f, 0.3f, 0.02f), FERRO);
            m.Caixa(new Vector3(x, h1 - 0.32f, z + 0.85f), new Vector3(0.02f, 0.3f, 0.02f), FERRO);
            m.Caixa(new Vector3(x, h1 - 0.75f, z + 0.55f), new Vector3(0.06f, 0.6f, 0.8f), TABUAS);
            m.Painel(new Vector3(x + 0.035f, h1 - 0.75f, z + 0.55f), Vector3.right, 0.42f, 0.52f, P_BRASAO);
            m.Painel(new Vector3(x - 0.035f, h1 - 0.75f, z + 0.55f), Vector3.left, 0.42f, 0.52f, P_BRASAO);
        }
    }

    // ---------------- 3) capela com torre do sino (referências 5 e 6)
    static void Capela(Malha m, Casa h, float alicerce)
    {
        float W = h.W, D = h.D, hP = 4.6f, T = 3.3f;
        float tx = W * 0.5f + T * 0.5f - 0.5f, tz = D * 0.5f - T * 0.5f + 0.5f;

        m.Caixa(new Vector3(0f, -alicerce * 0.5f + 0.02f, 0f), new Vector3(W + 0.3f, alicerce + 0.04f, D + 0.3f), PEDRA_ESCURA);
        m.Caixa(new Vector3(0f, hP * 0.5f, 0f), new Vector3(W, hP, D), REBOCO_CINZA);
        // rodapé de pedra (com abertura da porta)
        m.Caixa(new Vector3(0f, 0.3f, -D * 0.5f - 0.04f), new Vector3(W + 0.1f, 0.6f, 0.1f), PEDRA_ESCURA);
        m.Caixa(new Vector3(-W * 0.5f - 0.04f, 0.3f, 0f), new Vector3(0.1f, 0.6f, D + 0.1f), PEDRA_ESCURA);
        m.Caixa(new Vector3(W * 0.5f + 0.04f, 0.3f, 0f), new Vector3(0.1f, 0.6f, D + 0.1f), PEDRA_ESCURA);
        float ladoPorta = (W * 0.5f - 0.75f);
        m.Caixa(new Vector3(-(0.75f + ladoPorta * 0.5f), 0.3f, D * 0.5f + 0.04f), new Vector3(ladoPorta + 0.05f, 0.6f, 0.1f), PEDRA_ESCURA);
        m.Caixa(new Vector3(0.75f + ladoPorta * 0.5f, 0.3f, D * 0.5f + 0.04f), new Vector3(ladoPorta + 0.05f, 0.6f, 0.1f), PEDRA_ESCURA);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Cunhal(m, sx * W * 0.5f, sz * D * 0.5f, sx, sz, 0.6f, hP, PEDRA_ESCURA);

        // enxaimel nas laterais, com travessas diagonais e janelas
        int nPainel = 6;
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (W * 0.5f + 0.05f);
            m.Caixa(new Vector3(x, 0.68f, 0f), new Vector3(0.12f, 0.16f, D - 0.6f), MAD_VERM);
            m.Caixa(new Vector3(x, 2.5f, 0f), new Vector3(0.12f, 0.16f, D - 0.6f), MAD_VERM);
            m.Caixa(new Vector3(x, hP - 0.12f, 0f), new Vector3(0.14f, 0.2f, D + 0.1f), MAD_VERM);
            for (int k = 1; k < nPainel; k++)
                m.Caixa(new Vector3(x, (0.6f + hP) * 0.5f, -D * 0.5f + k * D / nPainel), new Vector3(0.12f, hP - 0.6f, 0.16f), MAD_VERM);
            for (int k = 0; k < nPainel; k++)
            {
                float z0 = -D * 0.5f + k * D / nPainel + 0.1f, z1 = -D * 0.5f + (k + 1) * D / nPainel - 0.1f;
                bool janela = k == 1 || k == 4;
                if (janela)
                {
                    float zm = (z0 + z1) * 0.5f;
                    m.Painel(new Vector3(s * (W * 0.5f + 0.02f), 3.5f, zm), new Vector3(s, 0f, 0f), 0.75f, 1.2f, P_VITRAL);
                    m.Caixa(new Vector3(s * (W * 0.5f + 0.06f), 3.5f, zm), new Vector3(0.08f, 1.3f, 0.08f), MAD_VERM);
                    m.Caixa(new Vector3(s * (W * 0.5f + 0.06f), 2.85f, zm), new Vector3(0.12f, 0.1f, 0.95f), MAD_VERM);
                    Diagonal(m, new Vector3(x, 0.8f, z0), new Vector3(x, 2.4f, z1), 0.12f, MAD_VERM);
                }
                else if (k % 2 == 0) Diagonal(m, new Vector3(x, 2.6f, z0), new Vector3(x, hP - 0.25f, z1), 0.12f, MAD_VERM);
                else Diagonal(m, new Vector3(x, 2.6f, z1), new Vector3(x, hP - 0.25f, z0), 0.12f, MAD_VERM);
            }
        }
        // frente: porta em arco, alpendre e degraus
        m.Caixa(new Vector3(0f, hP - 0.12f, D * 0.5f + 0.05f), new Vector3(W + 0.1f, 0.2f, 0.14f), MAD_VERM);
        m.Caixa(new Vector3(-1.0f, (0.6f + hP) * 0.5f, D * 0.5f + 0.05f), new Vector3(0.16f, hP - 0.6f, 0.12f), MAD_VERM);
        m.Caixa(new Vector3(1.0f, (0.6f + hP) * 0.5f, D * 0.5f + 0.05f), new Vector3(0.16f, hP - 0.6f, 0.12f), MAD_VERM);
        Diagonal(m, new Vector3(-W * 0.5f + 0.3f, 0.7f, D * 0.5f + 0.05f), new Vector3(-1.1f, hP - 0.25f, D * 0.5f + 0.05f), 0.12f, MAD_VERM);
        Diagonal(m, new Vector3(W * 0.5f - 0.3f, 0.7f, D * 0.5f + 0.05f), new Vector3(1.1f, hP - 0.25f, D * 0.5f + 0.05f), 0.12f, MAD_VERM);
        m.Painel(new Vector3(0f, 1.3f, D * 0.5f + 0.03f), Vector3.forward, 1.3f, 2.6f, P_PORTA_ARCO);
        Telhado(m, new Vector3(0f, 3.05f, D * 0.5f + 0.6f), 2.3f, 1.2f, 50f, true, 0.15f, 0.08f, TELHA_MUSGO, TABUAS, MAD_VERM, false, false);
        for (int s = -1; s <= 1; s += 2)
            Diagonal(m, new Vector3(s * 0.95f, 2.0f, D * 0.5f + 0.06f), new Vector3(s * 1.0f, 2.98f, D * 0.5f + 1.05f), 0.12f, MAD_VERM);
        for (int k = 0; k < 3; k++)
        {
            float topoDegrau = -0.02f - 0.22f * k;
            float alt = alicerce + topoDegrau;
            m.Caixa(new Vector3(0f, topoDegrau - alt * 0.5f, D * 0.5f + 0.2f + 0.4f * k + 0.2f), new Vector3(2.4f, alt, 0.4f), PEDRA_ESCURA);
        }

        float yC = Telhado(m, new Vector3(0f, hP, 0f), W + 0.1f, D, h.inclinacao, true, 0.45f, 0.4f, h.telhado, TABUAS, MAD_VERM, true, false);
        m.Painel(new Vector3(0f, hP + 1.3f, D * 0.5f + 0.02f), Vector3.forward, 0.7f, 0.8f, P_VITRAL);

        // ---- torre
        m.Caixa(new Vector3(tx, -alicerce * 0.5f + 0.02f, tz), new Vector3(T + 0.3f, alicerce + 0.04f, T + 0.3f), PEDRA_ESCURA);
        m.Caixa(new Vector3(tx, 0.3f, tz), new Vector3(T + 0.12f, 0.6f, T + 0.12f), PEDRA_ESCURA);
        m.Caixa(new Vector3(tx, 3f, tz), new Vector3(T, 6f, T), REBOCO_CINZA);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Cunhal(m, tx + sx * T * 0.5f, tz + sz * T * 0.5f, sx, sz, 0.6f, 6f, PEDRA_ESCURA);
        // vigas na frente e no lado de fora da torre
        float zF = tz + T * 0.5f + 0.05f, xF = tx + T * 0.5f + 0.05f;
        float[] ysT = { 0.68f, 2.6f, 4.45f };
        foreach (float y in ysT)
        {
            m.Caixa(new Vector3(tx, y, zF), new Vector3(T - 0.7f, 0.16f, 0.12f), MAD_VERM);
            m.Caixa(new Vector3(xF, y, tz), new Vector3(0.12f, 0.16f, T - 0.7f), MAD_VERM);
        }
        for (int k = 1; k <= 2; k++)
        {
            float off = -T * 0.5f + k * T / 3f;
            m.Caixa(new Vector3(tx + off, 2.56f, zF), new Vector3(0.14f, 3.9f, 0.12f), MAD_VERM);
            m.Caixa(new Vector3(xF, 2.56f, tz + off), new Vector3(0.12f, 3.9f, 0.14f), MAD_VERM);
        }
        for (int k = 0; k < 3; k++)
        {
            float a = -T * 0.5f + 0.35f + k * (T - 0.7f) / 3f, b = a + (T - 0.7f) / 3f;
            Diagonal(m, new Vector3(xF, 0.75f, tz + a), new Vector3(xF, 2.52f, tz + b), 0.11f, MAD_VERM);
            Diagonal(m, new Vector3(xF, 0.75f, tz + b), new Vector3(xF, 2.52f, tz + a), 0.11f, MAD_VERM);
        }
        // sacada (janela saliente) com toldo na frente da torre
        Vector3 sac = new Vector3(tx, 3.45f, tz + T * 0.5f + 0.32f);
        m.Caixa(sac, new Vector3(1.4f, 1.5f, 0.6f), REBOCO_CINZA);
        m.Caixa(sac + new Vector3(-0.66f, 0f, 0.27f), new Vector3(0.12f, 1.5f, 0.12f), MAD_VERM);
        m.Caixa(sac + new Vector3(0.66f, 0f, 0.27f), new Vector3(0.12f, 1.5f, 0.12f), MAD_VERM);
        m.Caixa(sac + new Vector3(0f, -0.7f, 0.05f), new Vector3(1.5f, 0.14f, 0.75f), MAD_VERM);
        m.Painel(sac + new Vector3(0f, 0.05f, 0.31f), Vector3.forward, 0.7f, 0.85f, P_VITRAL);
        for (int s = -1; s <= 1; s += 2)
            Diagonal(m, new Vector3(tx + s * 0.5f, 2.3f, tz + T * 0.5f + 0.06f), new Vector3(tx + s * 0.5f, 2.72f, tz + T * 0.5f + 0.55f), 0.1f, MAD_VERM);
        Barra(m, new Vector3(tx, 4.5f, tz + T * 0.5f + 0.02f), new Vector3(tx, 4.18f, tz + T * 0.5f + 0.95f), Vector3.right, 1.7f, 0.06f, TABUAS, 0.03f);
        // sacada do sino, campanário e pináculo
        m.Caixa(new Vector3(tx, 6.1f, tz), new Vector3(T + 0.7f, 0.22f, T + 0.7f), MAD_VERM);
        for (int k = -1; k <= 1; k++)
        {
            m.Caixa(new Vector3(tx + k * 1.1f, 5.92f, tz + T * 0.5f + 0.18f), new Vector3(0.14f, 0.16f, 0.4f), MAD_VERM);
            m.Caixa(new Vector3(tx + T * 0.5f + 0.18f, 5.92f, tz + k * 1.1f), new Vector3(0.4f, 0.16f, 0.14f), MAD_VERM);
        }
        float Tb = T - 0.2f;
        m.Caixa(new Vector3(tx, 6.21f + 1.2f, tz), new Vector3(Tb, 2.4f, Tb), REBOCO_CINZA);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Cunhal(m, tx + sx * Tb * 0.5f, tz + sz * Tb * 0.5f, sx, sz, 6.21f, 8.61f, PEDRA_ESCURA);
        Vector3[] faces = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        foreach (Vector3 f in faces)
            m.Painel(new Vector3(tx, 7.4f, tz) + f * (Tb * 0.5f + 0.025f), f, 1.0f, 1.5f, P_ARCO_SINO);
        m.Caixa(new Vector3(tx, 8.71f, tz), new Vector3(T + 0.2f, 0.2f, T + 0.2f), MAD_VERM);
        m.Tronco(new Vector3(tx, 8.81f, tz), 2.0f, 1.55f, 0.55f, TELHA_ARDOSIA);
        m.Piramide(new Vector3(tx, 9.36f, tz), 1.55f, 5.0f, TELHA_ARDOSIA);
        m.Piramide(new Vector3(tx, 14.15f, tz), 0.2f, 1.4f, FERRO);
        m.Caixa(new Vector3(tx, 14.75f, tz), new Vector3(0.16f, 0.16f, 0.16f), FERRO);
    }

    // ---------------- 4) celeiro
    static void Celeiro(Malha m, Casa h, float alicerce)
    {
        float W = h.W, D = h.D, hP = 3.8f;
        m.Caixa(new Vector3(0f, -alicerce * 0.5f + 0.02f, 0f), new Vector3(W + 0.3f, alicerce + 0.04f, D + 0.3f), PEDRA_ESCURA);
        m.Caixa(new Vector3(0f, 0.3f, 0f), new Vector3(W + 0.1f, 0.6f, D + 0.1f), PEDRA_ESCURA);
        m.Caixa(new Vector3(0f, (0.6f + hP) * 0.5f, 0f), new Vector3(W, hP - 0.6f, D), TABUAS);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.Caixa(new Vector3(sx * (W * 0.5f + 0.03f), hP * 0.5f, sz * (D * 0.5f + 0.03f)), new Vector3(0.28f, hP, 0.28f), MAD_ESC);
        m.Caixa(new Vector3(0f, hP - 0.12f, 0f), new Vector3(W + 0.14f, 0.24f, D + 0.14f), MAD_ESC);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (W * 0.5f + 0.05f);
            m.Caixa(new Vector3(x, 2.1f, 0f), new Vector3(0.12f, 0.16f, D), MAD_ESC);
            for (float z = -D * 0.5f + 2.2f; z < D * 0.5f - 1f; z += 2.2f)
            {
                m.Caixa(new Vector3(x, (0.6f + hP) * 0.5f, z), new Vector3(0.12f, hP - 0.6f, 0.18f), MAD_ESC);
                Diagonal(m, new Vector3(x, 0.7f, z - 2.1f), new Vector3(x, 2.0f, z - 0.1f), 0.12f, MAD_ESC);
            }
        }
        // portão duplo
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 c = new Vector3(s * 0.78f, 1.6f, D * 0.5f + 0.07f);
            m.Caixa(c, new Vector3(1.5f, 3.0f, 0.1f), TABUAS);
            Diagonal(m, c + new Vector3(-0.6f * s, -1.3f, 0.07f), c + new Vector3(0.6f * s, 1.3f, 0.07f), 0.12f, MAD_ESC);
            m.Caixa(c + new Vector3(0f, 1.3f, 0.07f), new Vector3(1.5f, 0.14f, 0.06f), MAD_ESC);
            m.Caixa(c + new Vector3(0f, -1.3f, 0.07f), new Vector3(1.5f, 0.14f, 0.06f), MAD_ESC);
            m.Caixa(c + new Vector3(-0.55f * s, 0.6f, 0.1f), new Vector3(0.5f, 0.06f, 0.03f), FERRO);
            m.Caixa(c + new Vector3(-0.55f * s, -0.6f, 0.1f), new Vector3(0.5f, 0.06f, 0.03f), FERRO);
        }
        m.Caixa(new Vector3(0f, 3.2f, D * 0.5f + 0.09f), new Vector3(3.5f, 0.24f, 0.2f), MAD_ESC);
        float yC = Telhado(m, new Vector3(0f, hP, 0f), W + 0.1f, D, h.inclinacao, true, 0.5f, 0.5f, h.telhado, TABUAS, MAD_ESC, true, false);
        float hF = yC - hP;
        m.Caixa(new Vector3(0f, hP + hF * 0.32f, D * 0.5f + 0.06f), new Vector3(1.3f, 1.2f, 0.08f), MAD_ESC);
        m.Caixa(new Vector3(0f, hP + hF * 0.32f + 1.0f, D * 0.5f + 0.5f), new Vector3(0.16f, 0.16f, 1.1f), MAD_ESC);
        m.Caixa(new Vector3(0f, hP + hF * 0.32f + 0.4f, D * 0.5f + 0.95f), new Vector3(0.03f, 1.1f, 0.03f), PALHA);
    }

    // ---------------- 5) ferraria (galpão aberto com forja)
    static void Ferraria(Malha m, Casa h, float alicerce)
    {
        float W = h.W, D = h.D, hP = 3.0f;
        m.Caixa(new Vector3(0f, -alicerce * 0.5f + 0.02f, 0f), new Vector3(W + 0.3f, alicerce + 0.04f, D + 0.3f), PEDRA_ESCURA);
        float[] xs = { -W * 0.5f, W * 0.5f }; float[] zs = { -D * 0.5f, 0f, D * 0.5f };
        foreach (float x in xs) foreach (float z in zs)
                m.Caixa(new Vector3(x, hP * 0.5f, z), new Vector3(0.26f, hP, 0.26f), MAD_ESC);
        m.Caixa(new Vector3(0f, hP - 0.12f, 0f), new Vector3(W + 0.2f, 0.24f, D + 0.2f), MAD_ESC);
        foreach (float x in xs)
            foreach (float z in zs)
            {
                if (z == 0f) continue;
                float sz = Mathf.Sign(-z);
                Diagonal(m, new Vector3(x, hP - 0.9f, z), new Vector3(x, hP - 0.15f, z + sz * 0.8f), 0.12f, MAD_ESC);
            }
        // parede baixa do fundo e das laterais
        m.Caixa(new Vector3(0f, 0.8f, -D * 0.5f), new Vector3(W, 1.6f, 0.12f), TABUAS);
        m.Caixa(new Vector3(-W * 0.5f, 0.6f, -D * 0.25f), new Vector3(0.12f, 1.2f, D * 0.5f), TABUAS);
        float yC = Telhado(m, new Vector3(0f, hP, 0f), D + 0.1f, W, h.inclinacao, false, 0.5f, 0.4f, h.telhado, TABUAS, MAD_ESC, false, false);
        // forja
        Vector3 f = new Vector3(-W * 0.25f + 0.2f, 0f, -D * 0.5f + 1.0f);
        m.Caixa(f + new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.9f, 1.2f), PEDRA_BLOCOS);
        m.Caixa(f + new Vector3(0f, 0.93f, 0f), new Vector3(1.15f, 0.08f, 0.85f), BRASA);
        m.Tronco(f + new Vector3(0f, 1.85f, 0f), 0.75f, 0.32f, 1.1f, PEDRA_ESCURA);
        m.Caixa(f + new Vector3(0f, (2.95f + yC + 0.8f) * 0.5f, 0f), new Vector3(0.62f, yC + 0.8f - 2.95f, 0.62f), PEDRA_ESCURA);
        for (int s = -1; s <= 1; s += 2) m.Caixa(f + new Vector3(s * 0.5f, 1.4f, 0.4f), new Vector3(0.14f, 0.95f, 0.14f), PEDRA_ESCURA);
        // fole
        m.Caixa(f + new Vector3(1.05f, 0.75f, 0f), new Vector3(0.5f, 0.12f, 0.8f), COURO);
        // bigorna no toco
        Vector3 b = new Vector3(W * 0.2f, 0f, -0.2f);
        m.Cilindro(b, 0.32f, 0.55f, 10, MAD_VERM, Quaternion.identity);
        m.Caixa(b + new Vector3(0f, 0.65f, 0f), new Vector3(0.26f, 0.2f, 0.2f), FERRO);
        m.Caixa(b + new Vector3(0f, 0.81f, 0f), new Vector3(0.6f, 0.13f, 0.24f), FERRO);
        m.Caixa(b + new Vector3(0.36f, 0.82f, 0f), new Vector3(0.16f, 0.09f, 0.14f), FERRO);
        // pedra de amolar
        Vector3 a = new Vector3(W * 0.5f - 0.7f, 0f, D * 0.25f);
        m.Caixa(a + new Vector3(-0.2f, 0.4f, 0f), new Vector3(0.08f, 0.8f, 0.08f), MAD_ESC);
        m.Caixa(a + new Vector3(0.2f, 0.4f, 0f), new Vector3(0.08f, 0.8f, 0.08f), MAD_ESC);
        m.Cilindro(a + new Vector3(-0.07f, 0.75f, 0f), 0.38f, 0.14f, 14, PEDRA_BLOCOS, Quaternion.Euler(0f, 0f, -90f));
        // tina de água
        Vector3 t = new Vector3(-W * 0.25f + 1.6f, 0f, -D * 0.5f + 0.8f);
        m.Cilindro(t, 0.38f, 0.7f, 12, TABUAS, Quaternion.identity);
        m.Cilindro(t + Vector3.up * 0.12f, 0.395f, 0.05f, 12, FERRO, Quaternion.identity);
        m.Cilindro(t + Vector3.up * 0.55f, 0.395f, 0.05f, 12, FERRO, Quaternion.identity);
        m.Cilindro(t + Vector3.up * 0.62f, 0.34f, 0.03f, 12, AGUA, Quaternion.identity);
    }

    // ==================================================================
    //  PEÇAS DE CONSTRUÇÃO
    // ==================================================================
    // Telhado de duas águas. c = centro em cima das paredes. Retorna a altura da cumeeira.
    static float Telhado(Malha m, Vector3 c, float S, float L, float inclinacao, bool cumeeiraZ, float beiral, float beiralFrontao,
                         int matTelha, int matFrontao, int matBorda, bool virado, bool serrilhado)
    {
        Vector3 eC = cumeeiraZ ? Vector3.forward : Vector3.right;
        Vector3 eL = cumeeiraZ ? Vector3.right : Vector3.forward;
        float p = inclinacao * Mathf.Deg2Rad, tg = Mathf.Tan(p);
        float topo = c.y;
        float yC = topo + S * 0.5f * tg;
        float b = virado ? S * 0.5f * 0.86f : S * 0.5f;
        float yB = yC - b * tg;
        float tg2 = virado ? Mathf.Tan((inclinacao - 16f) * Mathf.Deg2Rad) : tg;
        float xE = S * 0.5f + beiral;
        float yE = yB - (xE - b) * tg2;
        float yW = yB - (S * 0.5f - b) * tg2;
        float comp = L + 2f * beiralFrontao, t = 0.14f;
        Vector3 cc = new Vector3(c.x, 0f, c.z);

        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 R = cc + Vector3.up * yC, B = cc + eL * (s * b) + Vector3.up * yB, E = cc + eL * (s * xE) + Vector3.up * yE;
            if (virado) { Barra(m, R, B, eC, comp, t, matTelha, t * 0.5f); Barra(m, B, E, eC, comp, t, matTelha, t * 0.5f); }
            else Barra(m, R, E, eC, comp, t, matTelha, t * 0.5f);
            if (matBorda >= 0)
                m.Caixa(E + eL * (s * 0.02f) - Vector3.up * 0.06f, cumeeiraZ ? new Vector3(0.1f, 0.26f, comp) : new Vector3(comp, 0.26f, 0.1f), matBorda);
        }
        m.Caixa(cc + Vector3.up * (yC + 0.11f), cumeeiraZ ? new Vector3(0.28f, 0.2f, comp + 0.04f) : new Vector3(comp + 0.04f, 0.2f, 0.28f), matTelha);

        for (int e = -1; e <= 1; e += 2)
        {
            Vector3 baseF = cc + eC * (e * L * 0.5f);
            if (matFrontao >= 0)
            {
                Vector3[] pts = {
                    P(baseF, eL, -S * 0.5f, topo), P(baseF, eL, -S * 0.5f, yW), P(baseF, eL, -b, yB), P(baseF, eL, 0f, yC),
                    P(baseF, eL, b, yB), P(baseF, eL, S * 0.5f, yW), P(baseF, eL, S * 0.5f, topo) };
                Vector3 centroBase = P(baseF, eL, 0f, topo);
                for (int i = 0; i < pts.Length - 1; i++)
                    if ((pts[i] - pts[i + 1]).sqrMagnitude > 0.0001f) m.Triangulo2Lados(centroBase, pts[i], pts[i + 1], matFrontao);
            }
            Vector3 baseB = cc + eC * (e * (L * 0.5f + beiralFrontao - 0.05f));
            if (matBorda >= 0)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    if (virado)
                    {
                        Barra(m, P(baseB, eL, 0f, yC), P(baseB, eL, s * b, yB), eC, 0.1f, 0.3f, matBorda, -0.05f);
                        Barra(m, P(baseB, eL, s * b, yB), P(baseB, eL, s * xE, yE), eC, 0.1f, 0.3f, matBorda, -0.05f);
                    }
                    else Barra(m, P(baseB, eL, 0f, yC), P(baseB, eL, s * xE, yE), eC, 0.12f, 0.34f, matBorda, -0.05f);
                }
            }
            if (serrilhado)
            {
                Vector3 baseS = cc + eC * (e * (L * 0.5f + beiralFrontao - 0.09f));
                for (int s = -1; s <= 1; s += 2)
                    for (float x = 0.25f; x < xE - 0.1f; x += 0.26f)
                    {
                        float y = x <= b ? yC - x * tg : yB - (x - b) * tg2;
                        m.Caixa(P(baseS, eL, s * x, y - 0.36f), cumeeiraZ ? new Vector3(0.09f, 0.2f, 0.06f) : new Vector3(0.06f, 0.2f, 0.09f), matBorda >= 0 ? matBorda : MAD_ESC);
                    }
            }
        }
        return yC;
    }

    static Vector3 P(Vector3 origem, Vector3 eixo, float x, float y) { return origem + eixo * x + Vector3.up * y; }

    // viga/placa entre dois pontos. "lado" é o eixo da largura.
    static void Barra(Malha m, Vector3 a, Vector3 b, Vector3 lado, float largura, float espessura, int mat, float deslocNormal)
    {
        Vector3 d = b - a; float comp = d.magnitude;
        if (comp < 0.001f) return;
        Vector3 f = d / comp;
        Vector3 up = Vector3.Cross(f, lado).normalized;
        if (up.y < 0f) up = -up;
        m.Caixa((a + b) * 0.5f + up * deslocNormal, new Vector3(largura, espessura, comp), Quaternion.LookRotation(f, up), mat);
    }

    // pedras de quina alternadas
    static void Cunhal(Malha m, float cx, float cz, int sx, int sz, float y0, float y1, int mat)
    {
        int n = Mathf.FloorToInt((y1 - y0) / 0.44f);
        for (int i = 0; i < n; i++)
        {
            float y = y0 + 0.22f + i * 0.44f;
            bool longoX = i % 2 == 0;
            float lx = longoX ? 0.7f : 0.38f, lz = longoX ? 0.38f : 0.7f;
            m.Caixa(new Vector3(cx - sx * (lx * 0.5f - 0.04f), y, cz - sz * (lz * 0.5f - 0.04f)), new Vector3(lx, 0.38f, lz), mat);
        }
    }

    static void EnxaimelLaterais(Malha m, float y0, float y1, float W, float D, float zc, float passo, int mat)
    {
        float h = y1 - y0, meio = (y0 + y1) * 0.5f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.Caixa(new Vector3(sx * (W * 0.5f + 0.02f), meio, zc + sz * (D * 0.5f + 0.02f)), new Vector3(0.24f, h, 0.24f), mat);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (W * 0.5f + 0.05f);
            m.Caixa(new Vector3(x, y0 + 0.1f, zc), new Vector3(0.12f, 0.2f, D + 0.1f), mat);
            m.Caixa(new Vector3(x, y1 - 0.1f, zc), new Vector3(0.12f, 0.2f, D + 0.1f), mat);
            m.Caixa(new Vector3(x, y0 + h * 0.48f, zc), new Vector3(0.1f, 0.14f, D), mat);
            int n = Mathf.Max(2, Mathf.RoundToInt(D / passo));
            for (int k = 1; k < n; k++)
                m.Caixa(new Vector3(x, meio, zc - D * 0.5f + k * D / n), new Vector3(0.1f, h, 0.16f), mat);
        }
    }

    static void PortaRet(Malha m, Vector3 basePorta, Vector3 normal, float largura, float altura, int matMoldura)
    {
        Quaternion r = Quaternion.LookRotation(normal, Vector3.up);
        m.Painel(basePorta + normal * 0.025f + Vector3.up * (altura * 0.5f), normal, largura, altura, P_PORTA);
        Local(m, basePorta, r, new Vector3(-largura * 0.5f - 0.11f, altura * 0.5f + 0.1f, 0.08f), new Vector3(0.22f, altura + 0.2f, 0.18f), matMoldura);
        Local(m, basePorta, r, new Vector3(largura * 0.5f + 0.11f, altura * 0.5f + 0.1f, 0.08f), new Vector3(0.22f, altura + 0.2f, 0.18f), matMoldura);
        Local(m, basePorta, r, new Vector3(0f, altura + 0.14f, 0.1f), new Vector3(largura + 0.75f, 0.26f, 0.22f), matMoldura);
        Local(m, basePorta, r, new Vector3(0f, -0.15f, 0.5f), new Vector3(largura + 0.6f, 0.3f, 0.65f), PEDRA_ESCURA);
    }

    static void JanelaGrade(Malha m, Vector3 centro, Vector3 normal, float lw, float lh, int matMoldura)
    {
        Quaternion r = Quaternion.LookRotation(normal, Vector3.up);
        m.Painel(centro + normal * 0.02f, normal, lw, lh, P_GRADE);
        Local(m, centro, r, new Vector3(0f, lh * 0.5f + 0.08f, 0.07f), new Vector3(lw + 0.32f, 0.16f, 0.16f), matMoldura);
        Local(m, centro, r, new Vector3(0f, -lh * 0.5f - 0.08f, 0.1f), new Vector3(lw + 0.4f, 0.16f, 0.22f), matMoldura);
        Local(m, centro, r, new Vector3(-lw * 0.5f - 0.08f, 0f, 0.07f), new Vector3(0.16f, lh, 0.16f), matMoldura);
        Local(m, centro, r, new Vector3(lw * 0.5f + 0.08f, 0f, 0.07f), new Vector3(0.16f, lh, 0.16f), matMoldura);
    }

    static void JanelaVeneziana(Malha m, Vector3 centro, Vector3 normal, float lw, float lh)
    {
        Quaternion r = Quaternion.LookRotation(normal, Vector3.up);
        Vector3 dir = r * Vector3.right;
        m.Painel(centro + normal * 0.02f, normal, lw, lh, P_GRADE);
        float wv = lw * 0.55f;
        m.Painel(centro + normal * 0.035f + dir * (lw * 0.5f + 0.09f + wv * 0.5f), normal, wv, lh + 0.04f, P_VENEZIANA);
        m.Painel(centro + normal * 0.035f - dir * (lw * 0.5f + 0.09f + wv * 0.5f), normal, wv, lh + 0.04f, P_VENEZIANA);
        Local(m, centro, r, new Vector3(0f, lh * 0.5f + 0.06f, 0.06f), new Vector3(lw + 0.2f, 0.12f, 0.12f), MAD_ESC);
        Local(m, centro, r, new Vector3(0f, -lh * 0.5f - 0.06f, 0.08f), new Vector3(lw + 0.26f, 0.12f, 0.16f), MAD_ESC);
        Local(m, centro, r, new Vector3(-lw * 0.5f - 0.05f, 0f, 0.06f), new Vector3(0.1f, lh, 0.12f), MAD_ESC);
        Local(m, centro, r, new Vector3(lw * 0.5f + 0.05f, 0f, 0.06f), new Vector3(0.1f, lh, 0.12f), MAD_ESC);
    }

    static void Luz(Transform pai, Vector3 local, Color cor, float alcance, float intensidade, bool obrigatoria)
    {
        if (!obrigatoria && luzesCriadas >= LUZES_MAXIMAS) return;
        GameObject lg = new GameObject("Luz");
        lg.transform.SetParent(pai, false);
        lg.transform.localPosition = local;
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = alcance;
        l.intensity = intensidade;
        l.color = cor;
        l.shadows = LightShadows.None;
        luzesCriadas++;
    }

    // ==================================================================
    //  PRAÇA, ESTRADA, CAMPO DE TREINO E DETALHES
    // ==================================================================
    static void MontarPraca(Vector3 c, Vector3 paraJogador)
    {
        Transform pasta = Pasta("Praca");
        Quaternion rEntrada = Quaternion.LookRotation(paraJogador, Vector3.up);

        Malha poco = new Malha(); Poco(poco);
        Instanciar(poco, "Poco", NoChao(c), rEntrada, pasta, true);

        Malha banco = new Malha(); Banco(banco);
        Mesh mBanco = null; Material[] matsBanco = null;
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, 45f + i * 90f, 0f) * paraJogador;
            Instanciar(mBanco == null ? banco : null, "Banco", NoChao(c + dir * 4.3f), Quaternion.LookRotation(-dir), pasta, true, ref mBanco, ref matsBanco);
        }

        int[] cores = { TEC_VERM, TEC_AZUL, TEC_OCRE };
        float[] angsBarraca = { 95f, 140f, -100f };
        for (int i = 0; i < 3; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, angsBarraca[i], 0f) * paraJogador;
            Malha b = new Malha(); Barraca(b, cores[i], i);
            Instanciar(b, "Barraca de Feira", NoChao(c + dir * 7.3f), Quaternion.LookRotation(-dir), pasta, true);
            Vector3 lado = Quaternion.Euler(0f, 90f, 0f) * dir;
            Props(pasta, c + dir * 8.7f + lado * Faixa(-1.4f, -0.5f), i == 1 ? 3 : 1);
            Props(pasta, c + dir * 8.7f + lado * Faixa(0.5f, 1.4f), 0);
        }

        Vector3 ladoE = Vector3.Cross(Vector3.up, paraJogador);
        Malha carroca = new Malha(); Carroca(carroca);
        Instanciar(carroca, "Carroca", NoChao(c + paraJogador * (RAIO_PRACA + 1.5f) - ladoE * 5.5f), Quaternion.LookRotation(-ladoE) * Quaternion.Euler(0f, -20f, 0f), pasta, true);

        for (int i = 0; i < 6; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, 30f + i * 60f, 0f) * paraJogador;
            Poste(pasta, c + dir * (RAIO_PRACA + 0.6f), Quaternion.LookRotation(-dir));
        }
        for (int s = -1; s <= 1; s += 2)
        {
            Malha est = new Malha(); Estandarte(est);
            Instanciar(est, "Estandarte", NoChao(c + paraJogador * (RAIO_PRACA - 0.5f) + ladoE * (s * 2.6f)), rEntrada, pasta, true);
        }
    }

    static void MontarEstrada(List<Vector3> pts, Vector3 centro)
    {
        Transform pasta = Pasta("Estrada");
        int idx = 1;
        for (int i = 1; i < pts.Count; i++) { idx = i; if (Plano(pts[i] - centro).magnitude > 40f) break; }
        Vector3 dirEstrada = Plano(pts[idx] - pts[idx - 1]).normalized;
        Malha portal = new Malha(); PortalDaVila(portal);
        Instanciar(portal, "Portal da Vila", NoChao(pts[idx]), Quaternion.LookRotation(dirEstrada), pasta, true);

        float prox = 14f, acum = 0f; int lado = 1;
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = pts[i - 1], b = pts[i];
            float seg = Plano(b - a).magnitude;
            while (acum + seg >= prox)
            {
                Vector3 p = Vector3.Lerp(a, b, (prox - acum) / seg);
                Vector3 l = Vector3.Cross(Vector3.up, Plano(b - a).normalized) * lado;
                if (Plano(p - centro).magnitude > RAIO_PRACA + 4f) Poste(pasta, p + l * 2.6f, Quaternion.LookRotation(-l));
                lado = -lado; prox += 16f;
            }
            acum += seg;
        }
    }

    static void MontarTreino()
    {
        if (!temTreino) return;
        Transform pasta = Pasta("Campo de Treino");
        Vector3 dir = rotTreino * Vector3.right, frente = rotTreino * Vector3.forward;
        for (int i = -1; i <= 1; i++)
            Props(pasta, posTreino + dir * (i * 2.2f) + frente * 1.2f, 6, rotTreino * Quaternion.Euler(0f, Faixa(-12f, 12f), 0f));
        for (int i = -1; i <= 1; i += 2)
            Props(pasta, posTreino + dir * (i * 1.8f) - frente * 3.5f, 7, rotTreino);
        Props(pasta, posTreino + dir * 4.3f, 5, rotTreino * Quaternion.Euler(0f, -90f, 0f));
        Props(pasta, posTreino - dir * 4.3f + frente * 1.5f, 3, rotTreino * Quaternion.Euler(0f, 90f, 0f));
        Props(pasta, posTreino - dir * 4.2f - frente * 1.5f, 2);
        Props(pasta, posTreino - dir * 4.0f - frente * 2.6f, 2);
        // cerca em volta, aberta para a estrada
        Malha cerca = new Malha();
        float w = 11f, d = 9f;
        Vector3[] c = { new Vector3(-w / 2, 0, d / 2), new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0, -d / 2), new Vector3(w / 2, 0, d / 2) };
        for (int l = 0; l < 3; l++) CercaReta(cerca, c[l], c[l + 1], 2f);
        CercaReta(cerca, c[3], new Vector3(1.6f, 0f, d / 2), 2f);
        CercaReta(cerca, new Vector3(-1.6f, 0f, d / 2), c[0], 2f);
        Instanciar(cerca, "Cerca do Treino", NoChao(posTreino), rotTreino, pasta, true);
    }

    static void DetalhesDasCasas(List<Casa> casas)
    {
        Transform pasta = Pasta("Detalhes");
        foreach (Casa h in casas)
        {
            Vector3 frente = h.rot * Vector3.forward, direita = h.rot * Vector3.right;
            float ladoProps = rng.NextDouble() < 0.5 ? -1f : 1f;
            switch (h.tipo)
            {
                case 4: // celeiro
                    for (int i = 0; i < 6; i++)
                        Props(pasta, h.pos + frente * (h.D * 0.5f + 1.2f) + direita * (h.W * 0.5f + Faixa(0.3f, 3f)) - frente * Faixa(0f, 3.5f), 2);
                    Props(pasta, h.pos + frente * (h.D * 0.5f + 0.8f) - direita * (h.W * 0.5f - 0.4f), 1);
                    continue;
                case 5: // ferraria
                    Props(pasta, h.pos - frente * (h.D * 0.5f - 0.45f) + direita * (h.W * 0.25f), 5, h.rot);
                    Props(pasta, h.pos + direita * (h.W * 0.5f + 0.5f) + frente * 0.8f, 5, h.rot * Quaternion.Euler(0f, 90f, 0f));
                    Escudo(pasta, h.pos + direita * (h.W * 0.5f + 0.3f) - frente * 1.6f, direita);
                    Escudo(pasta, h.pos + direita * (h.W * 0.5f + 0.3f) - frente * 0.6f, direita);
                    Props(pasta, h.pos + frente * (h.D * 0.5f + 0.6f) - direita * (h.W * 0.5f - 0.5f), 3, h.rot);
                    Props(pasta, h.pos + frente * (h.D * 0.5f + 0.7f) + direita * (h.W * 0.5f - 0.4f), 0);
                    continue;
                case 3: // capela
                    Poste(pasta, h.pos + frente * (h.D * 0.5f + 1.6f) - direita * 1.9f, h.rot * Quaternion.Euler(0f, 90f, 0f));
                    continue;
            }
            // barris, caixas e baús num canto da frente
            Vector3 canto = h.pos + frente * (h.D * 0.5f + 0.7f) + direita * ladoProps * (h.W * 0.5f - 0.3f);
            int qtd = rng.Next(1, 4);
            for (int i = 0; i < qtd; i++) Props(pasta, canto - direita * ladoProps * i * 0.8f - frente * Faixa(0f, 0.3f), rng.Next(0, 2));
            if (rng.NextDouble() < 0.35) Props(pasta, canto - direita * ladoProps * qtd * 0.9f, 3, h.rot);
            // escudo encostado na parede
            if (rng.NextDouble() < 0.5)
                Escudo(pasta, h.pos - direita * ladoProps * (h.W * 0.5f + 0.3f) + frente * (h.D * 0.3f), -direita * ladoProps);
            // lenha na lateral
            if (h.tipo == 0 || rng.NextDouble() < 0.4)
            {
                Malha lenha = new Malha(); Lenha(lenha);
                Vector3 p = h.pos - direita * ladoProps * (h.W * 0.5f + 0.75f) - frente * (h.D * 0.15f);
                Instanciar(lenha, "Lenha", NoChao(p), h.rot * Quaternion.Euler(0f, 90f, 0f), pasta, true);
            }
            if (h.tipo == 2) // taverna
            {
                Malha banco = new Malha(); Banco(banco);
                Instanciar(banco, "Banco", NoChao(h.pos + frente * (h.D * 0.5f + 1.5f) - direita * (h.W * 0.28f)), h.rot * Quaternion.Euler(0f, 180f, 0f), pasta, true);
                for (int i = 0; i < 4; i++) Props(pasta, h.pos + frente * (h.D * 0.5f + 0.7f) + direita * (h.W * 0.5f + 0.5f + (i % 2) * 0.8f) - frente * (i / 2) * 0.8f, 0);
                Props(pasta, h.pos + direita * (h.W * 0.5f + 0.35f) + frente * (h.D * 0.15f), 5, h.rot * Quaternion.Euler(0f, 90f, 0f));
            }
            if (h.horta)
            {
                Malha horta = new Malha(); Horta(horta, 7f, 5.2f);
                Instanciar(horta, "Horta", NoChao(h.pos - frente * (h.D * 0.5f + 4f)), h.rot, pasta, true);
            }
        }
    }

    // ------------------------------------------------------------------
    //  objetos repetidos (a malha é criada uma vez e reaproveitada)
    //  0 barril, 1 caixote, 2 feno, 3 baú, 4 escudo, 5 suporte de armas, 6 boneco de treino, 7 alvo
    // ------------------------------------------------------------------
    static string pastaMalhas = PASTA_MALHAS;   // o script do mapa usa outra pasta
    static string prefixoOculto = PREFIXO_OCULTO;
    static Mesh[] malhaProp = new Mesh[8];
    static Material[][] matsProp = new Material[8][];
    static readonly string[] nomesProp = { "Barril", "Caixote", "Feno", "Bau", "Escudo", "Suporte de Armas", "Boneco de Treino", "Alvo" };

    static void Props(Transform pai, Vector3 pos, int tipo) { Props(pai, pos, tipo, Quaternion.Euler(0f, Faixa(0f, 360f), 0f)); }

    static GameObject Props(Transform pai, Vector3 pos, int tipo, Quaternion rot)
    {
        Malha m = null;
        if (malhaProp[tipo] == null)
        {
            m = new Malha();
            switch (tipo)
            {
                case 0: Barril(m); break;
                case 1: Caixote(m); break;
                case 2: Feno(m); break;
                case 3: Bau(m); break;
                case 4: EscudoRedondo(m); break;
                case 5: SuporteDeArmas(m); break;
                case 6: Boneco(m); break;
                default: Alvo(m); break;
            }
        }
        return Instanciar(m, nomesProp[tipo], NoChao(pos), rot, pai, true, ref malhaProp[tipo], ref matsProp[tipo]);
    }

    static void Escudo(Transform pai, Vector3 pos, Vector3 paraFora)
    {
        Quaternion r = Quaternion.LookRotation(paraFora, Vector3.up) * Quaternion.Euler(-14f, Faixa(-8f, 8f), Faixa(-10f, 10f));
        Props(pai, pos, 4, r);
    }

    static void Poste(Transform pai, Vector3 pos, Quaternion rot)
    {
        Malha m = new Malha();
        m.Caixa(new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.4f, 0.55f), PEDRA_ESCURA);
        m.Caixa(new Vector3(0f, 1.8f, 0f), new Vector3(0.2f, 2.9f, 0.2f), MAD_ESC);
        m.Caixa(new Vector3(0f, 3.15f, 0.42f), new Vector3(0.1f, 0.1f, 0.95f), MAD_ESC);
        Diagonal(m, new Vector3(0f, 2.55f, 0.08f), new Vector3(0f, 3.1f, 0.5f), 0.08f, MAD_ESC);
        m.Caixa(new Vector3(0f, 3.0f, 0.82f), new Vector3(0.02f, 0.22f, 0.02f), FERRO);
        Vector3 l = new Vector3(0f, 2.68f, 0.82f);
        m.Caixa(l + Vector3.up * -0.2f, new Vector3(0.3f, 0.05f, 0.3f), FERRO);
        m.Caixa(l, new Vector3(0.2f, 0.34f, 0.2f), LANTERNA);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.Caixa(l + new Vector3(sx * 0.12f, 0f, sz * 0.12f), new Vector3(0.035f, 0.4f, 0.035f), FERRO);
        m.Piramide(l + Vector3.up * 0.2f, 0.2f, 0.22f, FERRO);
        GameObject g = Instanciar(m, "Poste", NoChao(pos), rot, pai, true);
        Luz(g.transform, new Vector3(0f, 2.7f, 0.82f), new Color(1f, 0.72f, 0.42f), 8f, 2.5f, false);
    }

    // ------------------------------------------------------------------
    //  peças
    // ------------------------------------------------------------------
    static void Poco(Malha m)
    {
        int seg = 12; float r = 0.95f;
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            m.Caixa(d * r + Vector3.up * 0.45f, new Vector3(2f * Mathf.PI * r / seg * 1.08f, 0.9f, 0.34f), Quaternion.LookRotation(d), PEDRA_BLOCOS);
            m.Caixa(d * r + Vector3.up * 0.95f, new Vector3(2f * Mathf.PI * r / seg * 1.1f, 0.1f, 0.42f), Quaternion.LookRotation(d), PEDRA_ESCURA);
        }
        m.Cilindro(new Vector3(0f, 0.45f, 0f), 0.82f, 0.04f, 12, AGUA, Quaternion.identity);
        for (int s = -1; s <= 1; s += 2) m.Caixa(new Vector3(s * 1.0f, 1.35f, 0f), new Vector3(0.18f, 2.7f, 0.18f), MAD_ESC);
        m.Cilindro(new Vector3(-1.15f, 2.0f, 0f), 0.07f, 2.3f, 8, MAD_VERM, Quaternion.Euler(0f, 0f, -90f));
        m.Caixa(new Vector3(1.25f, 1.85f, 0f), new Vector3(0.06f, 0.35f, 0.06f), FERRO);
        Telhado(m, new Vector3(0f, 2.7f, 0f), 1.6f, 2.0f, 42f, false, 0.25f, 0.15f, TELHA_ARDOSIA, -1, MAD_ESC, false, false);
        m.Caixa(new Vector3(0f, 1.6f, 0f), new Vector3(0.025f, 0.8f, 0.025f), PALHA);
        m.Cilindro(new Vector3(0f, 1.0f, 0f), 0.17f, 0.26f, 8, TABUAS, Quaternion.identity);
        m.Cilindro(new Vector3(0f, 1.2f, 0f), 0.18f, 0.03f, 8, FERRO, Quaternion.identity);
    }

    static void Banco(Malha m)
    {
        m.Caixa(new Vector3(0f, 0.46f, 0f), new Vector3(1.6f, 0.08f, 0.42f), TABUAS);
        m.Caixa(new Vector3(-0.62f, 0.21f, 0f), new Vector3(0.12f, 0.42f, 0.36f), MAD_ESC);
        m.Caixa(new Vector3(0.62f, 0.21f, 0f), new Vector3(0.12f, 0.42f, 0.36f), MAD_ESC);
    }

    static void Barraca(Malha m, int cor, int variante)
    {
        float w = 2.6f, d = 1.8f;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            m.Caixa(new Vector3(sx * w * 0.5f, 1.3f, d * 0.5f), new Vector3(0.12f, 2.6f, 0.12f), MAD_ESC);
            m.Caixa(new Vector3(sx * w * 0.5f, 1.1f, -d * 0.5f), new Vector3(0.12f, 2.2f, 0.12f), MAD_ESC);
        }
        Barra(m, new Vector3(0f, 2.62f, d * 0.5f + 0.35f), new Vector3(0f, 2.22f, -d * 0.5f - 0.1f), Vector3.right, w + 0.3f, 0.05f, cor, 0.03f);
        for (int i = 0; i < 6; i++)
            m.Caixa(new Vector3(-w * 0.5f + 0.25f + i * (w - 0.5f) / 5f, 2.45f, d * 0.5f + 0.36f), new Vector3(0.38f, 0.3f, 0.03f), cor);
        m.Caixa(new Vector3(0f, 0.45f, d * 0.35f), new Vector3(w - 0.1f, 0.9f, 0.6f), TABUAS);
        m.Caixa(new Vector3(0f, 0.92f, d * 0.35f), new Vector3(w, 0.06f, 0.72f), MAD_ESC);
        if (variante == 0)
        {
            // armas à venda
            for (int i = 0; i < 3; i++) Espada(m, new Vector3(-1.1f + i * 0.35f, 0.97f, d * 0.35f + 0.26f), Quaternion.Euler(0f, -60f, 0f) * Quaternion.Euler(-90f, 0f, 0f));
            m.Cilindro(new Vector3(0.85f, 0.95f, d * 0.35f), 0.25f, 0.05f, 12, TABUAS, Quaternion.identity);
        }
        else if (variante == 1)
        {
            for (int i = 0; i < 5; i++) m.Cilindro(new Vector3(-0.9f + i * 0.45f, 0.95f, d * 0.35f), 0.1f, 0.24f, 8, COURO, Quaternion.identity);
        }
        else
        {
            for (int i = 0; i < 6; i++) m.Caixa(new Vector3(-0.95f + i * 0.38f, 1.02f, d * 0.35f + ((i % 2) * 0.16f - 0.08f)), new Vector3(0.22f, 0.14f, 0.3f), i % 2 == 0 ? PALHA : TEC_OCRE);
        }
    }

    static void Carroca(Malha m)
    {
        m.Caixa(new Vector3(0f, 0.75f, 0f), new Vector3(1.4f, 0.12f, 2.2f), TABUAS);
        m.Caixa(new Vector3(0.68f, 0.98f, 0f), new Vector3(0.07f, 0.36f, 2.2f), TABUAS);
        m.Caixa(new Vector3(-0.68f, 0.98f, 0f), new Vector3(0.07f, 0.36f, 2.2f), TABUAS);
        m.Caixa(new Vector3(0f, 0.98f, -1.07f), new Vector3(1.4f, 0.36f, 0.07f), TABUAS);
        m.Caixa(new Vector3(0f, 0.98f, 1.07f), new Vector3(1.4f, 0.36f, 0.07f), TABUAS);
        for (int s = -1; s <= 1; s += 2)
        {
            m.Cilindro(new Vector3(s * 0.86f, 0.5f, -0.3f), 0.5f, 0.12f, 14, MAD_ESC, Quaternion.Euler(0f, 0f, s * 90f));
            m.Cilindro(new Vector3(s * 0.875f, 0.5f, -0.3f), 0.52f, 0.09f, 14, FERRO, Quaternion.Euler(0f, 0f, s * 90f));
            Diagonal(m, new Vector3(s * 0.45f, 0.7f, 1.0f), new Vector3(s * 0.4f, 0.1f, 2.6f), 0.08f, MAD_ESC);
        }
        m.Caixa(new Vector3(0f, 0.5f, -0.3f), new Vector3(1.6f, 0.08f, 0.08f), FERRO);
        m.Caixa(new Vector3(0.1f, 1.05f, -0.3f), new Vector3(1.1f, 0.5f, 1.2f), Quaternion.Euler(0f, 8f, 0f), PALHA);
        m.Cilindro(new Vector3(-0.35f, 0.81f, 0.6f), 0.3f, 0.75f, 10, TABUAS, Quaternion.identity);
    }

    static void Barril(Malha m)
    {
        m.Cilindro(Vector3.zero, 0.34f, 0.9f, 12, TABUAS, Quaternion.identity);
        m.Cilindro(new Vector3(0f, 0.12f, 0f), 0.355f, 0.06f, 12, FERRO, Quaternion.identity);
        m.Cilindro(new Vector3(0f, 0.72f, 0f), 0.355f, 0.06f, 12, FERRO, Quaternion.identity);
    }

    static void Caixote(Malha m)
    {
        m.Caixa(new Vector3(0f, 0.35f, 0f), new Vector3(0.7f, 0.7f, 0.7f), TABUAS);
        m.Caixa(new Vector3(0f, 0.06f, 0f), new Vector3(0.74f, 0.1f, 0.74f), MAD_ESC);
        m.Caixa(new Vector3(0f, 0.64f, 0f), new Vector3(0.74f, 0.1f, 0.74f), MAD_ESC);
    }

    static void Feno(Malha m)
    {
        m.Caixa(new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.6f, 0.7f), PALHA);
        m.Caixa(new Vector3(-0.3f, 0.3f, 0f), new Vector3(0.04f, 0.62f, 0.72f), COURO);
        m.Caixa(new Vector3(0.3f, 0.3f, 0f), new Vector3(0.04f, 0.62f, 0.72f), COURO);
    }

    // baú com cintas de ferro (referência dos objetos)
    static void Bau(Malha m)
    {
        float w = 1.1f, d = 0.6f, hc = 0.5f;
        m.Caixa(new Vector3(0f, 0.08f + hc * 0.5f, 0f), new Vector3(w, hc, d), TABUAS);
        m.Caixa(new Vector3(0f, 0.08f + hc + 0.09f, 0f), new Vector3(w + 0.04f, 0.18f, d + 0.04f), TABUAS);
        for (int s = -1; s <= 1; s += 2) m.Caixa(new Vector3(s * (w * 0.5f - 0.12f), 0.04f, 0f), new Vector3(0.16f, 0.08f, d), MAD_ESC);
        float[] xs = { -w * 0.32f, 0f, w * 0.32f };
        foreach (float x in xs) m.Caixa(new Vector3(x, 0.08f + (hc + 0.18f) * 0.5f, 0f), new Vector3(0.06f, hc + 0.2f, d + 0.06f), FERRO);
        m.Caixa(new Vector3(0f, 0.08f + hc * 0.45f, 0f), new Vector3(w + 0.03f, 0.05f, d + 0.03f), FERRO);
        m.Caixa(new Vector3(0f, 0.08f + hc - 0.02f, d * 0.5f + 0.03f), new Vector3(0.14f, 0.18f, 0.03f), FERRO);
        for (int s = -1; s <= 1; s += 2)
            m.Caixa(new Vector3(s * (w * 0.5f + 0.03f), 0.08f + hc * 0.6f, 0f), new Vector3(0.03f, 0.05f, 0.2f), FERRO);
    }

    // escudo redondo de madeira com aro de ferro. Origem na borda de baixo.
    static void EscudoRedondo(Malha m)
    {
        float r = 0.45f;
        Vector3 c = new Vector3(0f, r, 0f);
        m.Cilindro(c + new Vector3(0f, 0f, -0.025f), r, 0.05f, 16, TABUAS, Quaternion.Euler(90f, 0f, 0f));
        int n = 16;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            m.Caixa(c + d * (r - 0.01f) + new Vector3(0f, 0f, 0.01f), new Vector3(2f * Mathf.PI * r / n * 1.12f, 0.06f, 0.08f), Quaternion.LookRotation(Vector3.forward, d), FERRO);
        }
        m.Cilindro(c + new Vector3(0f, 0f, 0.02f), 0.12f, 0.06f, 10, FERRO, Quaternion.Euler(90f, 0f, 0f));
        m.Caixa(c + new Vector3(0f, 0f, 0.025f), new Vector3(0.05f, r * 1.8f, 0.02f), FERRO);
    }

    static void Espada(Malha m, Vector3 baseCabo, Quaternion r)
    {
        // eixo da espada = Y local (cabo embaixo)
        m.Caixa(baseCabo + r * new Vector3(0f, 0.1f, 0f), new Vector3(0.035f, 0.18f, 0.035f), r, COURO);
        m.Caixa(baseCabo + r * new Vector3(0f, 0.0f, 0f), new Vector3(0.06f, 0.05f, 0.06f), r, FERRO);
        m.Caixa(baseCabo + r * new Vector3(0f, 0.21f, 0f), new Vector3(0.24f, 0.04f, 0.05f), r, FERRO);
        m.Caixa(baseCabo + r * new Vector3(0f, 0.66f, 0f), new Vector3(0.055f, 0.86f, 0.012f), r, LAMINA);
    }

    static void SuporteDeArmas(Malha m)
    {
        m.Caixa(new Vector3(0f, 0.06f, 0f), new Vector3(1.6f, 0.12f, 0.45f), MAD_ESC);
        for (int s = -1; s <= 1; s += 2) m.Caixa(new Vector3(s * 0.75f, 0.65f, -0.1f), new Vector3(0.1f, 1.2f, 0.1f), MAD_ESC);
        m.Caixa(new Vector3(0f, 1.15f, -0.1f), new Vector3(1.6f, 0.1f, 0.12f), MAD_ESC);
        m.Caixa(new Vector3(0f, 0.16f, 0.05f), new Vector3(1.4f, 0.08f, 0.18f), MAD_VERM);
        for (int i = 0; i < 3; i++)
            Espada(m, new Vector3(-0.5f + i * 0.28f, 0.2f, 0.05f), Quaternion.Euler(-8f, 0f, 0f));
        // machado
        Vector3 b = new Vector3(0.38f, 0.16f, 0.06f);
        Quaternion r = Quaternion.Euler(-7f, 0f, 0f);
        m.Cilindro(b, 0.025f, 1.05f, 6, MAD_VERM, r);
        m.Caixa(b + r * new Vector3(0.09f, 0.95f, 0f), new Vector3(0.18f, 0.16f, 0.03f), r, FERRO);
        // lança
        Vector3 l = new Vector3(0.62f, 0.16f, 0.06f);
        Quaternion rl = Quaternion.Euler(-5f, 0f, 0f);
        m.Cilindro(l, 0.022f, 1.75f, 6, MAD_VERM, rl);
        m.Piramide(l + rl * new Vector3(0f, 1.75f, 0f), 0.035f, 0.28f, LAMINA);
    }

    static void Boneco(Malha m)
    {
        m.Caixa(new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 0.14f), MAD_ESC);
        m.Caixa(new Vector3(0f, 0.05f, 0f), new Vector3(0.14f, 0.1f, 0.9f), MAD_ESC);
        m.Caixa(new Vector3(0f, 1.0f, 0f), new Vector3(0.12f, 1.9f, 0.12f), MAD_ESC);
        m.Caixa(new Vector3(0f, 1.45f, 0f), new Vector3(1.3f, 0.1f, 0.1f), MAD_ESC);
        m.Cilindro(new Vector3(0f, 0.75f, 0f), 0.27f, 0.8f, 10, PALHA, Quaternion.identity);
        m.Cilindro(new Vector3(0f, 0.95f, 0f), 0.28f, 0.05f, 10, COURO, Quaternion.identity);
        m.Cilindro(new Vector3(0f, 1.35f, 0f), 0.28f, 0.05f, 10, COURO, Quaternion.identity);
        m.Cilindro(new Vector3(0f, 1.62f, 0f), 0.16f, 0.32f, 8, TEC_OCRE, Quaternion.identity);
        m.Cilindro(new Vector3(0f, 1.6f, 0f), 0.17f, 0.04f, 8, COURO, Quaternion.identity);
        for (int s = -1; s <= 1; s += 2) m.Cilindro(new Vector3(s * 0.55f, 1.32f, 0f), 0.09f, 0.26f, 8, PALHA, Quaternion.identity);
        // escudo velho preso no braço
        m.Caixa(new Vector3(-0.45f, 1.05f, 0.18f), new Vector3(0.45f, 0.55f, 0.05f), Quaternion.Euler(0f, 0f, 8f), TABUAS);
    }

    static void Alvo(Malha m)
    {
        for (int s = -1; s <= 1; s += 2)
            Diagonal(m, new Vector3(s * 0.45f, 0f, -0.15f), new Vector3(s * 0.2f, 1.5f, 0.05f), 0.07f, MAD_ESC);
        Diagonal(m, new Vector3(0f, 0f, -0.7f), new Vector3(0f, 1.35f, -0.05f), 0.07f, MAD_ESC);
        Vector3 c = new Vector3(0f, 1.1f, 0.1f);
        Quaternion r = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 0f);
        Quaternion inc = Quaternion.Euler(-10f, 0f, 0f);
        m.Cilindro(c + inc * new Vector3(0f, 0f, -0.1f), 0.55f, 0.2f, 18, PALHA, inc * r);
        m.Cilindro(c + inc * new Vector3(0f, 0f, 0.1f), 0.4f, 0.01f, 18, TEC_VERM, inc * r);
        m.Cilindro(c + inc * new Vector3(0f, 0f, 0.11f), 0.27f, 0.01f, 18, TEC_OCRE, inc * r);
        m.Cilindro(c + inc * new Vector3(0f, 0f, 0.12f), 0.12f, 0.01f, 14, TEC_VERM, inc * r);
        // flechas cravadas
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = c + inc * new Vector3(-0.15f + i * 0.14f, 0.1f - i * 0.08f, 0.12f);
            m.Caixa(p + inc * new Vector3(0f, 0f, 0.22f), new Vector3(0.015f, 0.015f, 0.45f), inc, MAD_VERM);
            m.Caixa(p + inc * new Vector3(0f, 0f, 0.42f), new Vector3(0.06f, 0.06f, 0.08f), inc, TEC_OCRE);
        }
    }

    static void Lenha(Malha m)
    {
        int[] porFila = { 5, 4, 3 };
        for (int f = 0; f < porFila.Length; f++)
            for (int i = 0; i < porFila[f]; i++)
            {
                float x = (i - (porFila[f] - 1) * 0.5f) * 0.27f;
                m.Cilindro(new Vector3(x, 0.14f + f * 0.24f, -0.7f), 0.13f, 1.4f, 7, MAD_VERM, Quaternion.Euler(90f, 0f, 0f));
            }
        for (int s = -1; s <= 1; s += 2) m.Caixa(new Vector3(s * 0.8f, 0.45f, 0f), new Vector3(0.1f, 0.9f, 0.1f), MAD_ESC);
        m.Cilindro(new Vector3(1.25f, 0f, 0.3f), 0.25f, 0.45f, 9, MAD_VERM, Quaternion.identity);
        m.Cilindro(new Vector3(1.25f, 0.45f, 0.3f), 0.022f, 0.6f, 6, MAD_ESC, Quaternion.Euler(20f, 0f, 10f));
    }

    static void Horta(Malha m, float w, float d)
    {
        Vector3[] c = { new Vector3(-w / 2, 0, d / 2), new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0, -d / 2), new Vector3(w / 2, 0, d / 2) };
        for (int lado = 0; lado < 3; lado++) CercaReta(m, c[lado], c[lado + 1], 1.75f);
        CercaReta(m, c[3], new Vector3(0.9f, 0f, d / 2), 1.75f);
        CercaReta(m, new Vector3(-0.9f, 0f, d / 2), c[0], 1.75f);
        for (float x = -w / 2 + 1f; x <= w / 2 - 0.9f; x += 1.2f)
        {
            m.Caixa(new Vector3(x, 0.1f, -0.2f), new Vector3(0.65f, 0.2f, d - 1.8f), TABUAS);
            for (float z = -d / 2 + 1.1f; z <= d / 2 - 1.5f; z += 0.5f)
                m.Caixa(new Vector3(x, 0.3f, z), new Vector3(0.3f, 0.22f, 0.3f), Quaternion.Euler(0f, Faixa(0f, 90f), 0f), FOLHAGEM);
        }
    }

    static void CercaReta(Malha m, Vector3 a, Vector3 b, float passo)
    {
        Vector3 d = b - a; float comp = d.magnitude; d.Normalize();
        int n = Mathf.Max(1, Mathf.RoundToInt(comp / passo));
        for (int i = 0; i <= n; i++)
            m.Caixa(a + d * (comp * i / n) + Vector3.up * 0.55f, new Vector3(0.13f, 1.1f, 0.13f), Quaternion.Euler(0f, Faixa(-4f, 4f), Faixa(-3f, 3f)), MAD_ESC);
        Quaternion r = Quaternion.LookRotation(d);
        m.Caixa((a + b) * 0.5f + Vector3.up * 0.45f, new Vector3(0.05f, 0.1f, comp), r, MAD_VERM);
        m.Caixa((a + b) * 0.5f + Vector3.up * 0.88f, new Vector3(0.05f, 0.1f, comp), r, MAD_VERM);
    }

    static void PortalDaVila(Malha m)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            m.Caixa(new Vector3(s * 2.4f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.9f), PEDRA_ESCURA);
            m.Caixa(new Vector3(s * 2.4f, 2.5f, 0f), new Vector3(0.4f, 5f, 0.4f), MAD_ESC);
            Diagonal(m, new Vector3(s * 2.35f, 3.6f, 0f), new Vector3(s * 1.5f, 4.55f, 0f), 0.2f, MAD_ESC);
            m.Caixa(new Vector3(s * 2.4f, 1.2f, 0f), new Vector3(0.44f, 0.1f, 0.44f), FERRO);
            m.Caixa(new Vector3(s * 2.4f, 3.8f, 0f), new Vector3(0.44f, 0.1f, 0.44f), FERRO);
            // estandartes pendurados nos postes
            m.Caixa(new Vector3(s * 2.4f, 4.25f, 0.42f), new Vector3(0.9f, 0.05f, 0.05f), MAD_ESC);
            m.Caixa(new Vector3(s * 2.4f, 3.45f, 0.44f), new Vector3(0.75f, 1.55f, 0.02f), TEC_VERM);
            m.Painel(new Vector3(s * 2.4f, 3.55f, 0.46f), Vector3.forward, 0.45f, 0.56f, P_BRASAO);
        }
        m.Caixa(new Vector3(0f, 4.7f, 0f), new Vector3(5.9f, 0.34f, 0.42f), MAD_ESC);
        Telhado(m, new Vector3(0f, 4.87f, 0f), 1.0f, 5.8f, 40f, false, 0.25f, 0.2f, TELHA_ARDOSIA, -1, MAD_ESC, false, false);
        m.Caixa(new Vector3(-0.6f, 4.25f, 0f), new Vector3(0.03f, 0.6f, 0.03f), FERRO);
        m.Caixa(new Vector3(0.6f, 4.25f, 0f), new Vector3(0.03f, 0.6f, 0.03f), FERRO);
        m.Caixa(new Vector3(0f, 3.75f, 0f), new Vector3(1.8f, 0.6f, 0.08f), TABUAS);
        m.Painel(new Vector3(0f, 3.75f, 0.045f), Vector3.forward, 0.4f, 0.5f, P_BRASAO);
        m.Painel(new Vector3(0f, 3.75f, -0.045f), Vector3.back, 0.4f, 0.5f, P_BRASAO);
    }

    static void Estandarte(Malha m)
    {
        m.Caixa(new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f), PEDRA_ESCURA);
        m.Cilindro(new Vector3(0f, 0.4f, 0f), 0.05f, 4.2f, 8, MAD_ESC, Quaternion.identity);
        m.Caixa(new Vector3(0f, 4.3f, 0.35f), new Vector3(0.05f, 0.05f, 0.8f), MAD_ESC);
        m.Caixa(new Vector3(0f, 3.4f, 0.35f), new Vector3(0.02f, 1.75f, 0.68f), TEC_VERM);
        m.Painel(new Vector3(0.015f, 3.5f, 0.35f), Vector3.right, 0.42f, 0.52f, P_BRASAO);
        m.Painel(new Vector3(-0.015f, 3.5f, 0.35f), Vector3.left, 0.42f, 0.52f, P_BRASAO);
        m.Piramide(new Vector3(0f, 4.6f, 0f), 0.06f, 0.25f, FERRO);
    }

    //  MALHAS E OBJETOS
    // ==================================================================
    static GameObject Instanciar(Malha m, string nome, Vector3 pos, Quaternion rot, Transform pai, bool colisor)
    {
        Mesh mesh = null; Material[] ms = null;
        return Instanciar(m, nome, pos, rot, pai, colisor, ref mesh, ref ms);
    }

    // se "mesh" já vier preenchida, reaproveita (para objetos repetidos)
    static GameObject Instanciar(Malha m, string nome, Vector3 pos, Quaternion rot, Transform pai, bool colisor, ref Mesh mesh, ref Material[] ms)
    {
        if (mesh == null)
        {
            mesh = m.Gerar(nome + "_" + (++contadorMalhas).ToString("000"));
            ms = m.Materiais(mats);
            AssetDatabase.CreateAsset(mesh, pastaMalhas + "/" + mesh.name + ".asset");
        }
        GameObject g = new GameObject(nome);
        g.transform.SetParent(pai, false);
        g.transform.position = pos;
        g.transform.rotation = rot;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterials = ms;
        if (colisor) g.AddComponent<MeshCollider>().sharedMesh = mesh;
        g.isStatic = true;
        return g;
    }

    static Transform Pasta(string nome)
    {
        Transform t = raiz.Find(nome);
        if (t != null) return t;
        GameObject g = new GameObject(nome);
        g.transform.SetParent(raiz, false);
        return g.transform;
    }

    static void Local(Malha m, Vector3 origem, Quaternion r, Vector3 offset, Vector3 tamanho, int mat)
    {
        m.Caixa(origem + r * offset, tamanho, r, mat);
    }

    static void Diagonal(Malha m, Vector3 a, Vector3 b, float esp, int mat)
    {
        Vector3 d = b - a;
        m.Caixa((a + b) * 0.5f, new Vector3(esp, d.magnitude, esp), Quaternion.FromToRotation(Vector3.up, d.normalized), mat);
    }

    // Monta malhas com vários materiais. As UVs são em metros (o material
    // define quantos metros cada repetição da textura ocupa), com "para cima"
    // da textura sempre apontando para cima no mundo, e o veio da madeira
    // acompanhando o comprimento das vigas.
    class Malha
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly SortedDictionary<int, List<int>> tri = new SortedDictionary<int, List<int>>();

        List<int> Lista(int mat)
        {
            List<int> l;
            if (!tri.TryGetValue(mat, out l)) { l = new List<int>(); tri[mat] = l; }
            return l;
        }

        static void Eixos(Vector3 normal, int mat, Vector3 eixoLongo, out Vector3 eu, out Vector3 ev)
        {
            ev = Vector3.zero;
            if (veioMat != null && mat < veioMat.Length && veioMat[mat] && eixoLongo.sqrMagnitude > 0.5f && Mathf.Abs(Vector3.Dot(eixoLongo, normal)) < 0.9f)
                ev = (eixoLongo - normal * Vector3.Dot(eixoLongo, normal)).normalized;
            else if (Mathf.Abs(normal.y) < 0.95f)
                ev = (Vector3.up - normal * normal.y).normalized;
            else
                ev = (Vector3.forward - normal * normal.z).normalized;
            eu = Vector3.Cross(normal, ev).normalized;
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int mat, Vector3 eixoLongo)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { Vector3 t = b; b = d; d = t; }
            Vector3 eu, ev; Eixos(normal, mat, eixoLongo, out eu, out ev);
            int i0 = v.Count;
            Vector3[] p = { a, b, c, d };
            foreach (Vector3 q in p) { v.Add(q); n.Add(normal); uv.Add(new Vector2(Vector3.Dot(q, eu), Vector3.Dot(q, ev))); }
            List<int> l = Lista(mat);
            l.Add(i0); l.Add(i0 + 1); l.Add(i0 + 2);
            l.Add(i0); l.Add(i0 + 2); l.Add(i0 + 3);
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, int mat)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { Vector3 t = b; b = c; c = t; }
            Vector3 eu, ev; Eixos(normal, mat, Vector3.zero, out eu, out ev);
            int i0 = v.Count;
            Vector3[] p = { a, b, c };
            foreach (Vector3 q in p) { v.Add(q); n.Add(normal); uv.Add(new Vector2(Vector3.Dot(q, eu), Vector3.Dot(q, ev))); }
            List<int> l = Lista(mat);
            l.Add(i0); l.Add(i0 + 1); l.Add(i0 + 2);
        }

        public void Triangulo2Lados(Vector3 a, Vector3 b, Vector3 c, int mat)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
            Tri(a, b, c, nrm, mat);
            Tri(a, b, c, -nrm, mat);
        }

        public void Caixa(Vector3 c, Vector3 s, int mat) { Caixa(c, s, Quaternion.identity, mat); }

        public void Caixa(Vector3 c, Vector3 s, Quaternion r, int mat)
        {
            Vector3 h = s * 0.5f;
            Vector3[] eixos = { Vector3.right, Vector3.up, Vector3.forward };
            float[] meio = { h.x, h.y, h.z };
            int maior = s.x >= s.y && s.x >= s.z ? 0 : (s.y >= s.z ? 1 : 2);
            Vector3 longo = r * eixos[maior];
            for (int e = 0; e < 3; e++)
            {
                Vector3 ex = eixos[(e + 1) % 3] * meio[(e + 1) % 3];
                Vector3 ey = eixos[(e + 2) % 3] * meio[(e + 2) % 3];
                for (int sinal = -1; sinal <= 1; sinal += 2)
                {
                    Vector3 f = eixos[e] * meio[e] * sinal;
                    Quad(c + r * (f - ex - ey), c + r * (f - ex + ey), c + r * (f + ex + ey), c + r * (f + ex - ey), r * (eixos[e] * sinal), mat, longo);
                }
            }
        }

        // cilindro (eixo Y local); "baseCentro" é o centro da base
        public void Cilindro(Vector3 baseCentro, float raio, float altura, int lados, int mat, Quaternion r)
        {
            Vector3 eixo = r * Vector3.up;
            Vector3 topo = baseCentro + eixo * altura;
            for (int i = 0; i < lados; i++)
            {
                float a0 = i * Mathf.PI * 2f / lados, a1 = (i + 1) * Mathf.PI * 2f / lados;
                Vector3 d0 = r * new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * raio;
                Vector3 d1 = r * new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * raio;
                Quad(baseCentro + d0, topo + d0, topo + d1, baseCentro + d1, (d0 + d1).normalized, mat, eixo);
                Tri(topo, topo + d0, topo + d1, eixo, mat);
                Tri(baseCentro, baseCentro + d1, baseCentro + d0, -eixo, mat);
            }
        }

        // pirâmide de base quadrada (pináculo da torre, ponta de lança...)
        public void Piramide(Vector3 baseCentro, float meia, float altura, int mat)
        {
            Vector3 topo = baseCentro + Vector3.up * altura;
            Vector3[] c = {
                baseCentro + new Vector3(-meia, 0f, -meia), baseCentro + new Vector3(meia, 0f, -meia),
                baseCentro + new Vector3(meia, 0f, meia), baseCentro + new Vector3(-meia, 0f, meia) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = c[i], b = c[(i + 1) % 4];
                Vector3 nrm = Vector3.Cross(b - a, topo - a).normalized;
                Vector3 centroF = (a + b + topo) / 3f - baseCentro;
                if (Vector3.Dot(nrm, centroF) < 0f) nrm = -nrm;
                Tri(a, b, topo, nrm, mat);
            }
            Quad(c[0], c[1], c[2], c[3], Vector3.down, mat, Vector3.zero);
        }

        // tronco de pirâmide (base do pináculo, coifa da forja)
        public void Tronco(Vector3 baseCentro, float meiaBaixo, float meiaCima, float altura, int mat)
        {
            Vector3 cima = baseCentro + Vector3.up * altura;
            Vector3[] dirs = { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = baseCentro + dirs[i] * meiaBaixo, b = baseCentro + dirs[(i + 1) % 4] * meiaBaixo;
                Vector3 c2 = cima + dirs[(i + 1) % 4] * meiaCima, d = cima + dirs[i] * meiaCima;
                Vector3 nrm = Vector3.Cross(b - a, d - a).normalized;
                Vector3 meioF = (a + b + c2 + d) * 0.25f - (baseCentro + cima) * 0.5f;
                if (Vector3.Dot(nrm, meioF) < 0f) nrm = -nrm;
                Quad(a, b, c2, d, nrm, mat, Vector3.zero);
            }
            Quad(cima + dirs[0] * meiaCima, cima + dirs[1] * meiaCima, cima + dirs[2] * meiaCima, cima + dirs[3] * meiaCima, Vector3.up, mat, Vector3.zero);
        }

        // painel com a textura inteira (janela, porta, brasão), virado para "normal"
        public void Painel(Vector3 centro, Vector3 normal, float largura, float altura, int mat)
        {
            normal = normal.normalized;
            Vector3 cima = Mathf.Abs(normal.y) > 0.95f ? Vector3.forward : (Vector3.up - normal * normal.y).normalized;
            Vector3 dir = Vector3.Cross(normal, cima).normalized;
            Vector3 a = centro - dir * (largura * 0.5f) - cima * (altura * 0.5f);
            Vector3 b = centro + dir * (largura * 0.5f) - cima * (altura * 0.5f);
            Vector3 c = centro + dir * (largura * 0.5f) + cima * (altura * 0.5f);
            Vector3 d = centro - dir * (largura * 0.5f) + cima * (altura * 0.5f);
            Vector2[] uvs = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            Vector3[] p = { a, b, c, d };
            int i0 = v.Count;
            for (int i = 0; i < 4; i++) { v.Add(p[i]); n.Add(normal); uv.Add(uvs[i]); }
            List<int> l = Lista(mat);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
            { l.Add(i0); l.Add(i0 + 1); l.Add(i0 + 2); l.Add(i0); l.Add(i0 + 2); l.Add(i0 + 3); }
            else
            { l.Add(i0); l.Add(i0 + 2); l.Add(i0 + 1); l.Add(i0); l.Add(i0 + 3); l.Add(i0 + 2); }
        }

        public Mesh Gerar(string nome)
        {
            Mesh mesh = new Mesh();
            mesh.name = nome;
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = tri.Count;
            int i = 0;
            foreach (KeyValuePair<int, List<int>> kv in tri) mesh.SetTriangles(kv.Value, i++);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public Material[] Materiais(Material[] todos)
        {
            Material[] r = new Material[tri.Count];
            int i = 0;
            foreach (KeyValuePair<int, List<int>> kv in tri) r[i++] = todos[kv.Key];
            return r;
        }
    }

    //  APOIO
    // ==================================================================
    static Forma Circulo(Vector3 c, float raio, int camada)
    {
        return new Forma { a = new Vector2(c.x, c.z), b = new Vector2(c.x, c.z), raio = raio, camada = camada };
    }

    static Forma Segmento(Vector3 a, Vector3 b, float raio, int camada)
    {
        return new Forma { a = new Vector2(a.x, a.z), b = new Vector2(b.x, b.z), raio = raio, camada = camada };
    }

    static float Distancia(Forma f, Vector3 p)
    {
        Vector2 q = new Vector2(p.x, p.z), ab = f.b - f.a;
        float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(q - f.a, ab) / ab.sqrMagnitude);
        return (f.a + ab * t - q).magnitude;
    }

    static void RegiaoDasFormas(int res, Vector3 c, float raioMin, out int x0, out int z0, out int w, out int h)
    {
        float minX = c.x - raioMin, maxX = c.x + raioMin, minZ = c.z - raioMin, maxZ = c.z + raioMin;
        foreach (Forma f in formas)
        {
            minX = Mathf.Min(minX, Mathf.Min(f.a.x, f.b.x) - f.raio - 2f); maxX = Mathf.Max(maxX, Mathf.Max(f.a.x, f.b.x) + f.raio + 2f);
            minZ = Mathf.Min(minZ, Mathf.Min(f.a.y, f.b.y) - f.raio - 2f); maxZ = Mathf.Max(maxZ, Mathf.Max(f.a.y, f.b.y) + f.raio + 2f);
        }
        Vector3 o = terreno.transform.position; float cel = dados.size.x / res;
        x0 = Mathf.Clamp(Mathf.FloorToInt((minX - o.x) / cel), 0, res - 1);
        z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - o.z) / cel), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - o.x) / cel), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - o.z) / cel), 0, res - 1);
        w = x1 - x0 + 1; h = z1 - z0 + 1;
    }

    static Vector3 NoChao(Vector3 p) { p.y = Altura(p.x, p.z); return p; }
    static float Altura(float x, float z) { return terreno.SampleHeight(new Vector3(x, 0f, z)) + terreno.transform.position.y; }
    static bool DentroDoTerreno(Vector3 p, float margem)
    {
        Vector3 o = terreno.transform.position, s = dados.size;
        return p.x > o.x + margem && p.z > o.z + margem && p.x < o.x + s.x - margem && p.z < o.z + s.z - margem;
    }
    static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }
    static float Faixa(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    static bool MedirBounds(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        bool achou = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!achou) { b = r.bounds; achou = true; } else b.Encapsulate(r.bounds);
        }
        return achou;
    }
}
#endif
