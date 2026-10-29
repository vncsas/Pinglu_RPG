// =====================================================================
//  PingluVila_Biomas.cs  -  Biomas e terreno sombrio
// ---------------------------------------------------------------------
//  Floresta Sombria, Pântano, Terras Queimadas e Colinas de Pedra, com
//  campos de grama seca em volta do início e da vila.
//  Cada bioma tem chão, vegetação, objetos e clima próprios.
//
//  COMO USAR:  Pinglu > 20. Biomas e terreno
//  Para remover: Pinglu > 21. Desfazer biomas
//  (precisa dos scripts da vila e do mapa: PingluVila.cs e PingluVila_Mundo.cs)
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string PASTA_BIOMAS = "Assets/Cenario/Biomas";
    const string BACKUP_BIOMAS = PASTA_BIOMAS + "/Backup_Terreno_Biomas.asset";
    const string NOME_RAIZ_BIOMAS = "Biomas_Gerados";
    const string PREFIXO_OCULTO_BIOMAS = "[oculto pelos biomas] ";
    const string CAMADAS_TS = "Assets/TerrainSampleAssets/TerrainLayers/";
    const string DETALHES_TS = "Assets/TerrainSampleAssets/Prefabs/";
    const int CAMPOS = 0, FLORESTA = 1, PANTANO = 2, QUEIMADAS = 3, COLINAS = 4;
    static readonly string[] NOMES_BIOMAS = { "Campos", "Floresta Sombria", "Pantano", "Terras Queimadas", "Colinas de Pedra" };

    static Vector3[] centroBioma = new Vector3[5];
    static float[] raioBioma = new float[5];
    static float semX, semZ;
    static Material matCarvao, matAguaPantano;
    static Mesh[] malhaJunco = new Mesh[3]; static Material[][] matsJunco = new Material[3][];
    static Mesh malhaBrasa; static Material[] matsBrasa;

    [MenuItem("Pinglu/20. Biomas e terreno")]
    public static void ConstruirBiomas()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;
        if (GameObject.Find(NOME_RAIZ_BIOMAS) != null)
        {
            if (!EditorUtility.DisplayDialog("Pinglu", "Os biomas já foram criados. Quer refazer?", "Sim", "Cancelar")) return;
            DesfazerBiomas(false);
        }
        if (!EditorUtility.DisplayDialog("Pinglu - Biomas", "Vou criar 4 biomas (Floresta Sombria, Pântano, Terras Queimadas e Colinas de Pedra), " +
            "repintar o chão do mapa todo com cores mais sombrias e trocar a vegetação.\n\nAntes, guardo um backup do terreno. Pode demorar alguns minutos.", "Pode fazer", "Cancelar")) return;

        avisos.Clear(); formas.Clear(); zonas = new List<Zona>(); tamanhoPrefab.Clear();
        luzesCriadas = 0; contadorMalhas = 0;
        malhaProp = new Mesh[8]; matsProp = new Material[8][];
        malhaArvoreSeca = new Mesh[3]; matsArvoreSeca = new Material[3][];
        malhaTronco = new Mesh[3]; matsTronco = new Material[3][];
        malhaToco = new Mesh[2]; matsToco = new Material[2][];
        malhaJunco = new Mesh[3]; matsJunco = new Material[3][];
        malhaBrasa = null; matsBrasa = null;
        rng = new System.Random(5150);
        semX = Faixa(0f, 500f); semZ = Faixa(0f, 500f);
        pastaMalhas = PASTA_BIOMAS + "/Malhas";
        prefixoOculto = PREFIXO_OCULTO_BIOMAS;

        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando...", 0.02f);
            CriarPastaSeFaltar("Assets", "Cenario"); CriarPastaSeFaltar("Assets/Cenario", "Vila"); CriarPastaSeFaltar(PASTA, "Materiais_v2");
            CriarPastaSeFaltar("Assets/Cenario", "Biomas"); CriarPastaSeFaltar(PASTA_BIOMAS, "Camadas");
            if (AssetDatabase.IsValidFolder(pastaMalhas)) AssetDatabase.DeleteAsset(pastaMalhas);
            AssetDatabase.CreateFolder(PASTA_BIOMAS, "Malhas");
            ConfigurarTexturas();
            CriarMateriais();
            MateriaisDosBiomas();

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP_BIOMAS) != null) AssetDatabase.DeleteAsset(BACKUP_BIOMAS);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dados), BACKUP_BIOMAS)) throw new System.Exception("Não consegui fazer backup do terreno.");

            Undo.IncrementCurrentGroup();
            int grupo = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(dados, "Biomas");
            GameObject r = new GameObject(NOME_RAIZ_BIOMAS);
            Undo.RegisterCreatedObjectUndo(r, "Biomas");
            raiz = r.transform;

            EditorUtility.DisplayProgressBar("Pinglu", "Escolhendo onde fica cada bioma...", 0.05f);
            Vector3 spawn = AcharJogador();
            MontarZonasProtegidas(spawn);
            EscolherCentros(spawn);

            EditorUtility.DisplayProgressBar("Pinglu", "Tirando objetos antigos das áreas novas...", 0.1f);
            OcultarNaturezaAntiga();

            EditorUtility.DisplayProgressBar("Pinglu", "Levantando as colinas...", 0.18f);
            LevantarColinas();
            EditorUtility.DisplayProgressBar("Pinglu", "Cavando as lagoas do pântano...", 0.28f);
            CavarLagoas();
            EditorUtility.DisplayProgressBar("Pinglu", "Pintando o chão...", 0.4f);
            PintarChaoDosBiomas();
            EditorUtility.DisplayProgressBar("Pinglu", "Trocando a vegetação...", 0.55f);
            VegetacaoDosBiomas();
            EditorUtility.DisplayProgressBar("Pinglu", "Plantando e tirando árvores...", 0.68f);
            ArvoresDosBiomas();
            EditorUtility.DisplayProgressBar("Pinglu", "Espalhando os objetos de cada bioma...", 0.8f);
            ObjetosDosBiomas();
            EditorUtility.DisplayProgressBar("Pinglu", "Clima de cada bioma...", 0.95f);
            ClimaDeCadaBioma();

            terreno.Flush();
            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
            Undo.CollapseUndoOperations(grupo);
            Selection.activeGameObject = r;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console). Use Pinglu > 21. Desfazer biomas.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            pastaMalhas = PASTA_MALHAS; prefixoOculto = PREFIXO_OCULTO;
        }

        string onde = "";
        Vector3 ini = AcharJogadorSemAviso();
        for (int b = 1; b <= 4; b++) onde += "\n- " + NOMES_BIOMAS[b] + ": " + Direcao(ini, centroBioma[b]);
        string msg = "Biomas prontos! A partir de onde o jogador nasce:" + onde +
                     "\n\nNa Hierarchy, dentro de \"" + NOME_RAIZ_BIOMAS + "\", dê dois cliques numa pasta de bioma para a janela Scene ir até lá.\n\nSalve com Ctrl + S.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
    }

    [MenuItem("Pinglu/21. Desfazer biomas")]
    public static void DesfazerBiomasMenu() { DesfazerBiomas(true); }

    static void DesfazerBiomas(bool perguntar)
    {
        if (perguntar && !EditorUtility.DisplayDialog("Pinglu", "Remover os biomas e voltar o terreno como estava antes deles?", "Sim", "Cancelar")) return;
        terreno = Terrain.activeTerrain;
        GameObject r = GameObject.Find(NOME_RAIZ_BIOMAS);
        if (r != null) Undo.DestroyObjectImmediate(r);
        RestaurarTerreno(BACKUP_BIOMAS);
        foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g == null || !g.scene.IsValid() || !g.name.StartsWith(PREFIXO_OCULTO_BIOMAS)) continue;
            Undo.RecordObject(g, "Desfazer biomas");
            g.name = g.name.Substring(PREFIXO_OCULTO_BIOMAS.Length);
            g.SetActive(true);
        }
        AssetDatabase.SaveAssets();
        if (terreno != null) EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        if (perguntar) EditorUtility.DisplayDialog("Pinglu", "Biomas removidos. Salve a cena com Ctrl + S.", "OK");
    }

    // ==================================================================
    //  ONDE FICA CADA COISA
    // ==================================================================
    static void CriarPastaSeFaltar(string pai, string nome) { if (!AssetDatabase.IsValidFolder(pai + "/" + nome)) AssetDatabase.CreateFolder(pai, nome); }

    static Vector3 AcharJogadorSemAviso()
    {
        CharacterController cc = Object.FindFirstObjectByType<CharacterController>();
        return cc != null ? cc.transform.position : terreno.transform.position + dados.size * 0.5f;
    }

    // início, vila e pontos do mapa ficam protegidos (sem morros, lagoas ou objetos novos em cima)
    static void MontarZonasProtegidas(Vector3 spawn)
    {
        zonas.Add(new Zona { pos = spawn, raio = 150f });
        GameObject vila = GameObject.Find(NOME_RAIZ);
        Bounds b;
        if (vila != null && MedirBounds(vila, out b)) zonas.Add(new Zona { pos = b.center, raio = Mathf.Max(b.extents.x, b.extents.z) * 0.8f + 15f });
        GameObject mundo = GameObject.Find(NOME_RAIZ_MUNDO);
        if (mundo != null)
            foreach (Transform t in mundo.transform)
            {
                if (t.name == "Natureza") continue;
                if (MedirBounds(t.gameObject, out b)) zonas.Add(new Zona { pos = b.center, raio = Mathf.Max(b.extents.x, b.extents.z) + 12f });
            }
    }

    static Transform PontoDoMundo(string nome)
    {
        GameObject mundo = GameObject.Find(NOME_RAIZ_MUNDO);
        if (mundo == null) return null;
        foreach (Transform t in mundo.transform) if (t.name.StartsWith(nome)) return t;
        return null;
    }

    // cada bioma tenta ficar perto do lugar que combina com ele
    static void EscolherCentros(Vector3 spawn)
    {
        string[] ancoras = { null, "Acampamento de Bandidos 2", "Cemiterio", "Acampamento de Bandidos 1", "Ruinas da Fortaleza" };
        int[] ordem = { COLINAS, PANTANO, QUEIMADAS, FLORESTA };
        List<Vector3> escolhidos = new List<Vector3>();
        Vector3 o = terreno.transform.position, s = dados.size;
        Bounds bv; GameObject vila = GameObject.Find(NOME_RAIZ);
        Vector3 centroVila = vila != null && MedirBounds(vila, out bv) ? bv.center : spawn;
        foreach (int bio in ordem)
        {
            Transform ancora = PontoDoMundo(ancoras[bio]);
            Vector3 melhor = Vector3.zero; float nota = float.MaxValue;
            for (float x = o.x + 160f; x <= o.x + s.x - 160f; x += 30f)
                for (float z = o.z + 160f; z <= o.z + s.z - 160f; z += 30f)
                {
                    Vector3 c = new Vector3(x, 0f, z);
                    float dSpawn = Plano(c - spawn).magnitude, dVila = Plano(c - centroVila).magnitude;
                    if (dSpawn < 270f || dVila < 240f) continue;
                    bool perto = false;
                    foreach (Vector3 e in escolhidos) if (Plano(c - e).magnitude < 330f) perto = true;
                    if (perto) continue;
                    float n = Mathf.Abs(dSpawn - 380f) * 0.4f;
                    if (ancora != null) n += Plano(c - ancora.position).magnitude;
                    if (n < nota) { nota = n; melhor = c; }
                }
            if (nota == float.MaxValue)
            {
                // mapa apertado: usa a direção oposta ao início
                Vector3 dir = Quaternion.Euler(0f, 90f * escolhidos.Count, 0f) * Plano(o + s * 0.5f - spawn).normalized;
                melhor = spawn + dir * 330f;
                avisos.Add("Faltou espaço para o bioma " + NOMES_BIOMAS[bio] + "; ele ficou mais perto do início.");
            }
            melhor.y = Altura(melhor.x, melhor.z);
            centroBioma[bio] = melhor;
            raioBioma[bio] = 215f;
            escolhidos.Add(melhor);
        }
    }

    // peso de cada bioma num ponto (0..1), com bordas irregulares
    static void Pesos(Vector3 p, float[] w)
    {
        float wx = p.x + (Mathf.PerlinNoise(p.x * 0.006f + semX, p.z * 0.006f) - 0.5f) * 130f;
        float wz = p.z + (Mathf.PerlinNoise(p.x * 0.006f, p.z * 0.006f + semZ) - 0.5f) * 130f;
        float soma = 0f;
        for (int b = 1; b <= 4; b++)
        {
            float d = new Vector2(wx - centroBioma[b].x, wz - centroBioma[b].z).magnitude;
            w[b] = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(raioBioma[b] - 50f, raioBioma[b] + 50f, d));
            soma += w[b];
        }
        if (soma > 1f) for (int b = 1; b <= 4; b++) w[b] /= soma;
        w[0] = Mathf.Max(0f, 1f - Mathf.Min(1f, soma));
    }

    static float MascaraZonas(Vector3 p)
    {
        float m = 0f;
        foreach (Zona z in zonas)
            m = Mathf.Max(m, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(z.raio, z.raio + 30f, Plano(p - z.pos).magnitude)));
        return m;
    }

    static string Direcao(Vector3 de, Vector3 para)
    {
        Vector3 d = Plano(para - de);
        float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        string[] nomes = { "norte", "nordeste", "leste", "sudeste", "sul", "sudoeste", "oeste", "noroeste" };
        int i = Mathf.RoundToInt(((ang + 360f) % 360f) / 45f) % 8;
        return "a uns " + Mathf.RoundToInt(d.magnitude) + " m, para o " + nomes[i] + " (+Z = norte)";
    }

    // objetos de natureza antigos saem dos biomas que vão ganhar objetos próprios
    static void OcultarNaturezaAntiga()
    {
        GameObject mundo = GameObject.Find(NOME_RAIZ_MUNDO);
        if (mundo == null) return;
        Transform nat = mundo.transform.Find("Natureza");
        if (nat == null) return;
        float[] w = new float[5];
        List<GameObject> esconder = new List<GameObject>();
        foreach (Transform pasta in nat)
            foreach (Transform t in pasta)
            {
                Pesos(t.position, w);
                if (w[PANTANO] + w[QUEIMADAS] + w[COLINAS] > 0.45f) esconder.Add(t.gameObject);
            }
        foreach (GameObject g in esconder)
        {
            Undo.RecordObject(g, "Biomas");
            g.name = PREFIXO_OCULTO_BIOMAS + g.name;
            g.SetActive(false);
        }
    }

    // ==================================================================
    //  TERRENO
    // ==================================================================
    static void LevantarColinas()
    {
        int res = dados.heightmapResolution;
        Vector3 o = terreno.transform.position, s = dados.size;
        float[,] h = dados.GetHeights(0, 0, res, res);
        float[] w = new float[5];
        for (int zz = 0; zz < res; zz++)
            for (int xx = 0; xx < res; xx++)
            {
                Vector3 p = new Vector3(o.x + xx / (float)(res - 1) * s.x, 0f, o.z + zz / (float)(res - 1) * s.z);
                if (Plano(p - centroBioma[COLINAS]).magnitude > raioBioma[COLINAS] + 120f) continue;
                Pesos(p, w);
                if (w[COLINAS] < 0.01f) continue;
                float livre = 1f - MascaraZonas(p);
                if (livre <= 0f) continue;
                float crista = 1f - Mathf.Abs(Mathf.PerlinNoise(p.x * 0.011f + 31f, p.z * 0.011f + 7f) * 2f - 1f);
                float morro = crista * crista * 0.75f + Mathf.PerlinNoise(p.x * 0.028f + 5f, p.z * 0.028f + 9f) * 0.35f;
                h[zz, xx] += morro * 24f * w[COLINAS] * livre / s.y;
            }
        dados.SetHeights(0, 0, h);
    }

    static void CavarLagoas()
    {
        Transform pasta = Pasta(NOMES_BIOMAS[PANTANO]);
        float[] w = new float[5];
        List<Zona> lagoas = new List<Zona>();
        Vector3 c = centroBioma[PANTANO];
        for (int t = 0; t < 500 && lagoas.Count < 14; t++)
        {
            Vector2 d = Random2() * raioBioma[PANTANO] * 0.9f;
            Vector3 p = new Vector3(c.x + d.x, 0f, c.z + d.y);
            float raio = Faixa(6f, 13f);
            Pesos(p, w);
            if (w[PANTANO] < 0.6f || MascaraZonas(p) > 0.01f || !DentroDoTerreno(p, 40f) || Inclinacao(p.x, p.z) > 14f) continue;
            bool perto = false;
            foreach (Zona l in lagoas) if (Plano(p - l.pos).magnitude < l.raio + raio + 6f) perto = true;
            if (perto) continue;

            float media = 0f;
            for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; media += Altura(p.x + Mathf.Cos(a) * raio, p.z + Mathf.Sin(a) * raio); }
            media /= 8f;
            Cavar(p, raio, media - 1.1f);
            p.y = media;
            lagoas.Add(new Zona { pos = p, raio = raio });

            Malha agua = new Malha();
            agua.Cilindro(Vector3.zero, raio, 0.02f, 28, AGUA, Quaternion.identity);
            GameObject g = Instanciar(agua, "Lagoa", new Vector3(p.x, media - 0.35f, p.z), Quaternion.identity, pasta, false);
            TrocarMateriais(g, matAguaPantano);
            int juncos = rng.Next(5, 10);
            for (int k = 0; k < juncos; k++)
            {
                float a = Faixa(0f, Mathf.PI * 2f), rr = raio * Faixa(0.8f, 1.15f);
                ColocarJunco(pasta, new Vector3(p.x + Mathf.Cos(a) * rr, 0f, p.z + Mathf.Sin(a) * rr));
            }
        }
        foreach (Zona l in lagoas) zonas.Add(new Zona { pos = l.pos, raio = l.raio + 2f });
    }

    static void Cavar(Vector3 c, float raio, float fundo)
    {
        int res = dados.heightmapResolution;
        Vector3 o = terreno.transform.position, s = dados.size;
        float r = raio * 1.3f;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - r - o.x) / s.x * (res - 1)), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - r - o.z) / s.z * (res - 1)), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + r - o.x) / s.x * (res - 1)), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + r - o.z) / s.z * (res - 1)), 0, res - 1);
        int w = x1 - x0 + 1, hh = z1 - z0 + 1;
        float[,] h = dados.GetHeights(x0, z0, w, hh);
        float alvo = (fundo - o.y) / s.y;
        for (int zz = 0; zz < hh; zz++)
            for (int xx = 0; xx < w; xx++)
            {
                float wx = o.x + (x0 + xx) / (float)(res - 1) * s.x, wz = o.z + (z0 + zz) / (float)(res - 1) * s.z;
                float t = new Vector2(wx - c.x, wz - c.z).magnitude / r;
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, t));
                h[zz, xx] = Mathf.Min(h[zz, xx], Mathf.Lerp(h[zz, xx], alvo, k));
            }
        dados.SetHeights(x0, z0, h);
    }

    static Vector2 Random2()
    {
        float a = Faixa(0f, Mathf.PI * 2f), r = Mathf.Sqrt((float)rng.NextDouble());
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    // ------------------------------------------------------------------
    static TerrainLayer CamadaEscura(string nome, Color tom)
    {
        string destino = PASTA_BIOMAS + "/Camadas/" + nome + ".terrainlayer";
        TerrainLayer c = AssetDatabase.LoadAssetAtPath<TerrainLayer>(destino);
        if (c == null)
        {
            TerrainLayer orig = AssetDatabase.LoadAssetAtPath<TerrainLayer>(CAMADAS_TS + nome + "_TerrainLayer.terrainlayer");
            if (orig == null) { avisos.Add("Não achei a textura de chão " + nome); return null; }
            c = Object.Instantiate(orig);
            AssetDatabase.CreateAsset(c, destino);
        }
        c.diffuseRemapMax = new Vector4(tom.r, tom.g, tom.b, 1f);
        EditorUtility.SetDirty(c);
        return c;
    }

    static void PintarChaoDosBiomas()
    {
        // 0 grama seca, 1 musgo, 2 lama, 3 cinzas, 4 rocha, 5 urze | 6 estrada, 7 praça (mantidas)
        TerrainLayer[] novas = {
            CamadaEscura("Grass_Dry", new Color(0.72f, 0.70f, 0.60f)), CamadaEscura("Grass_Moss", new Color(0.58f, 0.66f, 0.52f)),
            CamadaEscura("Muddy", new Color(0.52f, 0.52f, 0.45f)), CamadaEscura("Black_Sand", new Color(0.55f, 0.52f, 0.50f)),
            CamadaEscura("Rock", new Color(0.72f, 0.72f, 0.75f)), CamadaEscura("Heather", new Color(0.68f, 0.62f, 0.62f)),
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(CAMADA_ESTRADA), AssetDatabase.LoadAssetAtPath<TerrainLayer>(CAMADA_PRACA) };
        foreach (TerrainLayer t in novas) if (t == null) { avisos.Add("Faltou alguma textura de chão; o chão não foi repintado."); return; }

        int res = dados.alphamapResolution;
        TerrainLayer[] antigas = dados.terrainLayers;
        float[,,] velho = dados.GetAlphamaps(0, 0, res, res);
        int iEstrada = -1, iPraca = -1;
        for (int i = 0; i < antigas.Length; i++)
        {
            if (antigas[i] == null) continue;
            if (antigas[i].name.Contains("Soil_Rocks")) iEstrada = i;
            if (antigas[i].name.Contains("Pebbles_A")) iPraca = i;
        }
        dados.terrainLayers = novas;
        float[,,] a = new float[res, res, novas.Length];
        Vector3 o = terreno.transform.position, s = dados.size;
        float[] w = new float[5];
        float[] mix = new float[6];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                Vector3 p = new Vector3(o.x + (x + 0.5f) / res * s.x, 0f, o.z + (z + 0.5f) / res * s.z);
                Pesos(p, w);
                float n1 = Mathf.PerlinNoise(p.x * 0.045f + 3f, p.z * 0.045f + 1f), n2 = Mathf.PerlinNoise(p.x * 0.09f + 8f, p.z * 0.09f + 4f);
                for (int k = 0; k < 6; k++) mix[k] = 0f;
                mix[0] += w[CAMPOS] * (0.55f + 0.35f * n1); mix[5] += w[CAMPOS] * (0.45f - 0.35f * n1);
                mix[1] += w[FLORESTA] * (0.65f + 0.2f * n2); mix[2] += w[FLORESTA] * (0.35f - 0.2f * n2);
                mix[2] += w[PANTANO] * (0.7f + 0.2f * n1); mix[1] += w[PANTANO] * (0.3f - 0.2f * n1);
                mix[3] += w[QUEIMADAS] * (0.75f + 0.2f * n2); mix[4] += w[QUEIMADAS] * (0.25f - 0.2f * n2);
                mix[5] += w[COLINAS] * (0.45f + 0.2f * n1); mix[4] += w[COLINAS] * 0.3f; mix[0] += w[COLINAS] * (0.25f - 0.2f * n1);
                float rocha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 40f, Inclinacao(p.x, p.z)));
                float soma = 0f;
                for (int k = 0; k < 6; k++) { mix[k] = Mathf.Lerp(mix[k], k == 4 ? 1f : 0f, rocha); soma += mix[k]; }
                float estrada = iEstrada >= 0 ? velho[z, x, iEstrada] : 0f, praca = iPraca >= 0 ? velho[z, x, iPraca] : 0f;
                float resto = Mathf.Max(0f, 1f - estrada - praca);
                for (int k = 0; k < 6; k++) a[z, x, k] = soma > 0f ? mix[k] / soma * resto : 0f;
                a[z, x, 6] = estrada; a[z, x, 7] = praca;
            }
        dados.SetAlphamaps(0, 0, a);
    }

    // ------------------------------------------------------------------
    //  vegetação rasteira: menos grama verde, mais plantas de cada bioma
    // ------------------------------------------------------------------
    // categorias: 0 grama, 1 grama seca, 2 samambaia, 3 urze, 4 planta, 5 arbusto
    static readonly float[][] FATOR_VEG = {
        new[] { 1f, 0.45f, 0.5f, 0.02f, 0.3f },
        new[] { 0.45f, 0.1f, 0.2f, 0.3f, 0.85f },
        new[] { 0.4f, 1f, 0.55f, 0f, 0.15f },
        new[] { 0.45f, 0.15f, 0.2f, 0f, 1f },
        new[] { 0.4f, 0.55f, 1f, 0f, 0.2f },
        new[] { 0.5f, 0.8f, 0.45f, 0f, 0.3f } };

    static int Categoria(DetailPrototype dp)
    {
        string n = dp.prototype != null ? dp.prototype.name : (dp.prototypeTexture != null ? dp.prototypeTexture.name : "");
        if (n.Contains("GrassDry")) return 1;
        if (n.Contains("Fern")) return 2;
        if (n.Contains("Heather")) return 3;
        if (n.Contains("Plant")) return 4;
        if (n.Contains("Bush")) return 5;
        return 0;
    }

    static int GarantirDetalhe(List<DetailPrototype> protos, string prefab, float densidade)
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(DETALHES_TS + prefab + ".prefab");
        if (go == null) return -1;
        for (int i = 0; i < protos.Count; i++) if (protos[i].prototype == go) return i;
        DetailPrototype dp = new DetailPrototype
        {
            prototype = go, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
            minWidth = 0.8f, maxWidth = 1.3f, minHeight = 0.8f, maxHeight = 1.3f, noiseSpread = 0.3f,
            healthyColor = new Color(0.75f, 0.75f, 0.68f), dryColor = new Color(0.7f, 0.62f, 0.5f), density = densidade, alignToGround = 0.6f
        };
        string erro;
        if (!dp.Validate(out erro)) { avisos.Add("A planta " + prefab + " não pôde ser usada: " + erro); return -1; }
        protos.Add(dp);
        return protos.Count - 1;
    }

    static void VegetacaoDosBiomas()
    {
        int res = dados.detailResolution;
        DetailPrototype[] antigos = dados.detailPrototypes;
        List<int[,]> camadas = new List<int[,]>();
        for (int i = 0; i < antigos.Length; i++) camadas.Add(dados.GetDetailLayer(0, 0, res, res, i));
        List<DetailPrototype> protos = new List<DetailPrototype>(antigos);
        int[] novos = {
            GarantirDetalhe(protos, "GrassDry_A", 0.6f), GarantirDetalhe(protos, "Fern_B", 0.35f),
            GarantirDetalhe(protos, "Heather_A", 0.5f), GarantirDetalhe(protos, "Plant_B", 0.3f), GarantirDetalhe(protos, "BushDry_A", 0.06f) };
        int[] catNovos = { 1, 2, 3, 4, 5 };
        dados.detailPrototypes = protos.ToArray();
        while (camadas.Count < protos.Count) camadas.Add(new int[res, res]);

        int[] cat = new int[protos.Count];
        for (int i = 0; i < protos.Count; i++) cat[i] = Categoria(protos[i]);
        bool cobertura = dados.detailScatterMode == DetailScatterMode.CoverageMode;
        float maximo = cobertura ? 255f : 3f;
        Vector3 o = terreno.transform.position;
        float cel = dados.size.x / res;
        float[] w = new float[5];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                Vector3 p = new Vector3(o.x + (x + 0.5f) * cel, 0f, o.z + (z + 0.5f) * cel);
                Pesos(p, w);
                bool protegido = MascaraZonas(p) > 0.5f;
                for (int l = 0; l < protos.Count; l++)
                {
                    float f = 0f;
                    for (int b = 0; b < 5; b++) f += w[b] * FATOR_VEG[cat[l]][b];
                    int velho = camadas[l][z, x];
                    int valor = protegido ? velho : Mathf.RoundToInt(velho * Mathf.Min(f, 1f));
                    // plantas novas em manchas, nas áreas de cada bioma
                    for (int k = 0; k < novos.Length; k++)
                        if (novos[k] == l && !protegido)
                        {
                            float mancha = Mathf.PerlinNoise(p.x * 0.05f + k * 13.7f, p.z * 0.05f + k * 7.1f);
                            float pinta = Mathf.Clamp01((mancha - 0.45f) * 2.5f) * Mathf.Clamp01(f - w[CAMPOS] * FATOR_VEG[catNovos[k]][CAMPOS]);
                            valor = Mathf.Max(valor, Mathf.RoundToInt(pinta * maximo));
                        }
                    camadas[l][z, x] = valor;
                }
            }
        for (int l = 0; l < protos.Count; l++) dados.SetDetailLayer(0, 0, l, camadas[l]);
        dados.RefreshPrototypes();
    }

    // ------------------------------------------------------------------
    static void ArvoresDosBiomas()
    {
        List<TreePrototype> protos = new List<TreePrototype>(dados.treePrototypes);
        int[] pinheiros = new int[ARVORES_MUNDO.Length];
        for (int i = 0; i < ARVORES_MUNDO.Length; i++) pinheiros[i] = Prototipo(protos, ARVORES_MUNDO[i]);
        int arbusto = Prototipo(protos, ARBUSTO_MUNDO);
        dados.treePrototypes = protos.ToArray();
        dados.RefreshPrototypes();

        Vector3 o = terreno.transform.position, s = dados.size;
        float[] w = new float[5];
        List<TreeInstance> lista = new List<TreeInstance>();
        foreach (TreeInstance ti in dados.treeInstances)
        {
            Vector3 p = new Vector3(o.x + ti.position.x * s.x, 0f, o.z + ti.position.z * s.z);
            Pesos(p, w);
            float tirar = w[PANTANO] * 0.85f + w[QUEIMADAS] * 0.97f + w[COLINAS] * 0.55f;
            if (MascaraZonas(p) < 0.5f && rng.NextDouble() < tirar) continue;
            lista.Add(ti);
        }
        // floresta fechada
        PlantarGrade(lista, FLORESTA, 5.5f, 0.55f, pinheiros, arbusto, 0.1f, 0.9f, 1.5f);
        // pinheiros esparsos nas colinas
        PlantarGrade(lista, COLINAS, 13f, 0.18f, pinheiros, arbusto, 0.05f, 0.8f, 1.2f);
        dados.SetTreeInstances(lista.ToArray(), true); // "true" recoloca tudo no chão (morros e lagoas novos)
    }

    static void PlantarGrade(List<TreeInstance> lista, int bio, float passo, float chance, int[] pinheiros, int arbusto, float chanceArbusto, float escMin, float escMax)
    {
        Vector3 o = terreno.transform.position, s = dados.size;
        Vector3 c = centroBioma[bio]; float r = raioBioma[bio] + 60f;
        float[] w = new float[5];
        for (float x = c.x - r; x <= c.x + r; x += passo)
            for (float z = c.z - r; z <= c.z + r; z += passo)
            {
                Vector3 p = new Vector3(x + Faixa(-passo * 0.4f, passo * 0.4f), 0f, z + Faixa(-passo * 0.4f, passo * 0.4f));
                if (!DentroDoTerreno(p, 20f)) continue;
                Pesos(p, w);
                if (w[bio] < 0.3f || !LivreNatureza(p, 3f) || Inclinacao(p.x, p.z) > 35f) continue;
                bool ehArbusto = rng.NextDouble() < chanceArbusto;
                if (!ehArbusto && rng.NextDouble() > chance * w[bio]) continue;
                float nx = (p.x - o.x) / s.x, nz = (p.z - o.z) / s.z;
                float esc = ehArbusto ? Faixa(0.7f, 1.2f) : Faixa(escMin, escMax);
                TreeInstance ti = new TreeInstance
                {
                    prototypeIndex = ehArbusto ? arbusto : pinheiros[rng.Next(pinheiros.Length)],
                    position = new Vector3(nx, dados.GetInterpolatedHeight(nx, nz) / s.y, nz),
                    widthScale = esc, heightScale = esc * Faixa(0.9f, 1.1f), rotation = Faixa(0f, Mathf.PI * 2f),
                    color = new Color32(255, 255, 255, 255), lightmapColor = new Color32(255, 255, 255, 255)
                };
                lista.Add(ti);
            }
    }

    // ==================================================================
    //  OBJETOS DE CADA BIOMA
    // ==================================================================
    static void MateriaisDosBiomas()
    {
        matCarvao = MaterialDerivado("Carvao", mats[MAD_ESC], new Color(0.24f, 0.21f, 0.2f), 0.05f);
        matAguaPantano = MaterialDerivado("Agua_Pantano", mats[AGUA], new Color(0.05f, 0.07f, 0.04f), 0.93f);
    }

    static Material MaterialDerivado(string nome, Material baseMat, Color cor, float lisura)
    {
        string caminho = PASTA_BIOMAS + "/" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, caminho); }
        else m.CopyPropertiesFromMaterial(baseMat);
        m.SetColor("_BaseColor", cor);
        m.SetFloat("_Smoothness", lisura);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void TrocarMateriais(GameObject g, Material m)
    {
        if (g == null || m == null) return;
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        if (r == null) return;
        Material[] arr = r.sharedMaterials;
        for (int i = 0; i < arr.Length; i++) arr[i] = m;
        r.sharedMaterials = arr;
    }

    static GameObject Ultimo(Transform pasta) { return pasta.childCount > 0 ? pasta.GetChild(pasta.childCount - 1).gameObject : null; }

    static bool PontoNoBioma(int bio, float pesoMin, float espaco, out Vector3 p)
    {
        float[] w = new float[5];
        for (int t = 0; t < 30; t++)
        {
            Vector2 d = Random2() * (raioBioma[bio] + 30f);
            p = new Vector3(centroBioma[bio].x + d.x, 0f, centroBioma[bio].z + d.y);
            if (!DentroDoTerreno(p, 25f)) continue;
            Pesos(p, w);
            if (w[bio] < pesoMin || !LivreNatureza(p, espaco) || Inclinacao(p.x, p.z) > 30f) continue;
            return true;
        }
        p = Vector3.zero;
        return false;
    }

    static void ObjetosDosBiomas()
    {
        Vector3 p;
        // floresta: troncos caídos, tocos e pedras
        Transform fl = Pasta(NOMES_BIOMAS[FLORESTA]);
        for (int i = 0; i < 45; i++) if (PontoNoBioma(FLORESTA, 0.5f, 3f, out p)) ColocarTronco(fl, p);
        for (int i = 0; i < 35; i++) if (PontoNoBioma(FLORESTA, 0.5f, 2f, out p)) ColocarToco(fl, p);
        for (int i = 0; i < 20; i++) if (PontoNoBioma(FLORESTA, 0.5f, 2f, out p)) ColocarPrefabMundo(PEDRAS_MUNDO[rng.Next(PEDRAS_MUNDO.Length)], fl, p, Faixa(0.8f, 1.8f), false, 0.15f);

        // pântano: árvores mortas e troncos apodrecendo
        Transform pa = Pasta(NOMES_BIOMAS[PANTANO]);
        for (int i = 0; i < 35; i++) if (PontoNoBioma(PANTANO, 0.5f, 3f, out p)) ColocarArvoreSeca(pa, p, Faixa(0.9f, 1.4f));
        for (int i = 0; i < 15; i++) if (PontoNoBioma(PANTANO, 0.5f, 3f, out p)) ColocarTronco(pa, p);
        for (int i = 0; i < 15; i++) if (PontoNoBioma(PANTANO, 0.5f, 2f, out p)) ColocarToco(pa, p);

        // terras queimadas: tudo carbonizado, focos de brasa com fumaça
        Transform qu = Pasta(NOMES_BIOMAS[QUEIMADAS]);
        for (int i = 0; i < 35; i++) if (PontoNoBioma(QUEIMADAS, 0.5f, 3f, out p)) { ColocarArvoreSeca(qu, p, Faixa(0.8f, 1.3f)); TrocarMateriais(Ultimo(qu), matCarvao); }
        for (int i = 0; i < 40; i++) if (PontoNoBioma(QUEIMADAS, 0.5f, 2f, out p)) { ColocarToco(qu, p); TrocarMateriais(Ultimo(qu), matCarvao); }
        for (int i = 0; i < 15; i++) if (PontoNoBioma(QUEIMADAS, 0.5f, 3f, out p)) { ColocarTronco(qu, p); TrocarMateriais(Ultimo(qu), matCarvao); }
        int fumacas = 0;
        for (int i = 0; i < 24; i++)
            if (PontoNoBioma(QUEIMADAS, 0.55f, 4f, out p))
            {
                GameObject b = ColocarBrasas(qu, p);
                if (b != null && fumacas < 7) { Fogo(b.transform, Vector3.up * 0.1f); fumacas++; }
            }
        for (int i = 0; i < 18; i++) if (PontoNoBioma(QUEIMADAS, 0.5f, 3f, out p)) ColocarPrefabMundo(PEDRAS_MUNDO[rng.Next(PEDRAS_MUNDO.Length)], qu, p, Faixa(1f, 3f), false, 0.2f);

        // colinas: rochedos grandes e grupos de pedras
        Transform co = Pasta(NOMES_BIOMAS[COLINAS]);
        for (int i = 0; i < 55; i++) if (PontoNoBioma(COLINAS, 0.45f, 6f, out p)) ColocarPrefabMundo(PEDRAS_MUNDO[rng.Next(PEDRAS_MUNDO.Length)], co, p, Faixa(3f, 8f), false, 0.25f);
        for (int i = 0; i < 40; i++)
            if (PontoNoBioma(COLINAS, 0.45f, 3f, out p))
            {
                int n = rng.Next(2, 5);
                for (int k = 0; k < n; k++)
                    ColocarPrefabMundo(PEDRAS_MUNDO[rng.Next(PEDRAS_MUNDO.Length)], co, p + new Vector3(Faixa(-3f, 3f), 0f, Faixa(-3f, 3f)), Faixa(0.7f, 2.2f), false, 0.15f);
            }
        for (int i = 0; i < 20; i++) if (PontoNoBioma(COLINAS, 0.45f, 2f, out p)) ColocarPrefabMundo(PEDRINHAS_MUNDO, co, p, Faixa(1.5f, 3f), false, 0.05f);
        for (int i = 0; i < 10; i++) if (PontoNoBioma(COLINAS, 0.45f, 3f, out p)) ColocarArvoreSeca(co, p, Faixa(0.8f, 1.1f));
    }

    static void ColocarJunco(Transform pai, Vector3 p)
    {
        int v = rng.Next(0, 3);
        Malha m = null;
        if (malhaJunco[v] == null)
        {
            m = new Malha();
            System.Random r2 = new System.Random(300 + v);
            for (int k = 0; k < 16; k++)
            {
                float a = (float)r2.NextDouble() * Mathf.PI * 2f, d = (float)r2.NextDouble() * 0.5f, alt = 0.9f + (float)r2.NextDouble() * 0.8f;
                Vector3 b = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                Quaternion inc = Quaternion.Euler(((float)r2.NextDouble() - 0.5f) * 20f, 0f, ((float)r2.NextDouble() - 0.5f) * 20f);
                m.Caixa(b + inc * (Vector3.up * alt * 0.5f), new Vector3(0.025f, alt, 0.025f), inc, FOLHAGEM);
                if (k % 4 == 0) m.Cilindro(b + inc * (Vector3.up * (alt - 0.25f)), 0.035f, 0.22f, 6, COURO, inc);
            }
        }
        Instanciar(m, "Juncos", NoChao(p), Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, false, ref malhaJunco[v], ref matsJunco[v]);
    }

    static GameObject ColocarBrasas(Transform pai, Vector3 p)
    {
        Malha m = null;
        if (malhaBrasa == null)
        {
            m = new Malha();
            System.Random r2 = new System.Random(77);
            for (int k = 0; k < 14; k++)
            {
                Vector3 c = new Vector3(((float)r2.NextDouble() - 0.5f) * 2.4f, 0.05f, ((float)r2.NextDouble() - 0.5f) * 2.4f);
                float t = 0.12f + (float)r2.NextDouble() * 0.25f;
                m.Caixa(c, new Vector3(t, t * 0.6f, t), Quaternion.Euler(0f, (float)r2.NextDouble() * 90f, 0f), k % 3 == 0 ? PEDRA_ESCURA : BRASA);
            }
        }
        return Instanciar(m, "Brasas", NoChao(p), Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, false, ref malhaBrasa, ref matsBrasa);
    }

    // ------------------------------------------------------------------
    static void ClimaDeCadaBioma()
    {
        GameObject g = new GameObject("Clima dos Biomas");
        g.transform.SetParent(raiz, false);
        ClimaDosBiomas c = g.AddComponent<ClimaDosBiomas>();
        c.materialParticula = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PARTICULA_URP);
        c.biomas = new[] {
            Bioma(FLORESTA, new Color(0.20f, 0.27f, 0.22f), 1.7f, 0.4f, ClimaDosBiomas.Efeito.Esporos),
            Bioma(PANTANO, new Color(0.25f, 0.28f, 0.19f), 2.1f, 0.3f, ClimaDosBiomas.Efeito.Vagalumes),
            Bioma(QUEIMADAS, new Color(0.34f, 0.22f, 0.17f), 1.5f, 0.6f, ClimaDosBiomas.Efeito.Brasas),
            Bioma(COLINAS, new Color(0.42f, 0.45f, 0.50f), 0.7f, 1.4f, ClimaDosBiomas.Efeito.Poeira) };
    }

    static ClimaDosBiomas.Bioma Bioma(int b, Color cor, float neblina, float vento, ClimaDosBiomas.Efeito efeito)
    {
        return new ClimaDosBiomas.Bioma { nome = NOMES_BIOMAS[b], centro = centroBioma[b], raio = raioBioma[b], corDaNeblina = cor, forcaDaNeblina = neblina, vento = vento, efeito = efeito };
    }
}
#endif
