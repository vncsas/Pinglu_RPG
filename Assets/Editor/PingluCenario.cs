// =====================================================================
//  PingluCenario.cs  -  Popula o cenário automaticamente (Pinglu RPG)
// ---------------------------------------------------------------------
//  COMO USAR:
//   1. Coloque este arquivo em  Assets/Editor/  (crie a pasta "Editor")
//   2. Abra a Unity e a cena com o terreno (SampleScene)
//   3. No menu de cima clique em:  Pinglu > 1. Popular cenário
//   4. Olhe o resultado e salve a cena (Ctrl + S)
//
//  Para voltar ao que era antes:  Pinglu > 2. Desfazer cenário gerado
//
//  O script usa só assets que já estão no projeto (Holotna,
//  TerrainSampleAssets e a ruína do portal de pedra).
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PingluCenario
{
    // ------------------------------------------------------------------
    //  AJUSTES (pode mudar esses números e rodar de novo)
    // ------------------------------------------------------------------
    const float RAIO_AREA = 140f;        // tamanho da área caprichada em volta do jogador (metros)
    const float RAIO_LIVRE_SPAWN = 12f;  // área limpa onde o jogador nasce
    const int SEMENTE = 2026;            // mude para gerar uma distribuição diferente

    const int GRUPOS_DE_ARVORES = 30;
    const int ARBUSTOS_SOLTOS = 90;
    const int GRUPOS_DE_PEDRAS = 28;
    const int CANTEIROS_DE_FLORES = 45;
    const int TUFOS_DE_GRAMA = 160;

    // ------------------------------------------------------------------
    //  Caminhos dos assets do projeto
    // ------------------------------------------------------------------
    const string PASTA = "Assets/Cenario";
    const string BACKUP_TERRENO = PASTA + "/Backup_Terreno.asset";
    const string BACKUP_LUZ = PASTA + "/Backup_Luz.json";
    const string PERFIL_POS = PASTA + "/Pinglu_PosProcessamento.asset";
    const string NOME_RAIZ = "Cenario_Gerado";

    const string H = "Assets/Holotna/Mountain/Prefabs/";
    static readonly string[] ARVORES = { H + "Tree01A.prefab", H + "Tree01B.prefab" };
    const string ARBUSTO = H + "Bush01.prefab";
    static readonly string[] PEDRAS = { H + "Rock01.prefab", H + "Rock02.prefab" };
    const string PEDRINHAS = H + "Pebbles01.prefab";
    static readonly string[] FLORES = { H + "Flowers01A.prefab", H + "Flowers01B.prefab", H + "Flower01A.prefab", H + "Flower01B.prefab" };
    static readonly string[] GRAMAS = { H + "Grass01A.prefab", H + "Grass01B.prefab", H + "Grass01C.prefab" };

    const string T = "Assets/TerrainSampleAssets/Prefabs/";
    // (prefab, densidade) - plantas pintadas direto no terreno (leve para o jogo)
    static readonly string[] DETALHES = { T + "Fern_A.prefab", T + "Fern_B.prefab", T + "Heather_A.prefab", T + "Heather_B.prefab", T + "Plant_A.prefab", T + "Bush_A.prefab" };
    static readonly float[] DENSIDADES = { 0.35f, 0.35f, 0.5f, 0.5f, 0.25f, 0.08f };

    const string PORTAL = "Assets/Personagens/Maps/ancient-stone-gate-ruin-moss-covered/source/Zyklus4_Zeile9/tripo_convert_7554d447-81d6-4be4-8541-ffc25d5d5324.fbx";
    const string MATERIAL_PARTICULA = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    // ------------------------------------------------------------------
    //  Estado interno
    // ------------------------------------------------------------------
    static System.Random rng;
    static Terrain terreno;
    static TerrainData dados;
    static Grade ocupado;
    static readonly Dictionary<string, Vector3> tamanhoNativo = new Dictionary<string, Vector3>();
    static readonly List<string> avisos = new List<string>();

    struct PontoDeInteresse { public Vector3 pos; public float raio; }
    static readonly List<PontoDeInteresse> pois = new List<PontoDeInteresse>();
    static Vector3 trilhaInicio, trilhaFim;
    static bool temTrilha;

    [System.Serializable]
    class LuzSalva { public Quaternion rotacao; public Color cor; public float intensidade; public LightShadows sombras; }

    // ==================================================================
    //  MENU 1 - POPULAR
    // ==================================================================
    [MenuItem("Pinglu/1. Popular cenário")]
    public static void Popular()
    {
        terreno = Terrain.activeTerrain;
        if (terreno == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei nenhum Terrain na cena aberta. Abra a cena do mapa (SampleScene) e tente de novo.", "OK");
            return;
        }
        dados = terreno.terrainData;

        if (GameObject.Find(NOME_RAIZ) != null)
        {
            if (!EditorUtility.DisplayDialog("Pinglu", "O cenário já foi gerado antes. Quer desfazer o anterior e gerar de novo?", "Sim, gerar de novo", "Cancelar"))
                return;
            Desfazer(false);
        }

        if (!EditorUtility.DisplayDialog("Pinglu - Popular cenário",
            "Vou adicionar árvores, arbustos, plantas, pedras, flores, um portal de ruína, um círculo de pedras, vagalumes, vento, " +
            "e ajustar o sol e o pós-processamento em volta de onde o jogador nasce.\n\n" +
            "Antes, vou guardar um backup do terreno em Assets/Cenario para poder desfazer depois.",
            "Pode fazer", "Cancelar"))
            return;

        avisos.Clear(); pois.Clear(); temTrilha = false;
        rng = new System.Random(SEMENTE);

        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Fazendo backup...", 0.05f);
            FazerBackup();

            Undo.IncrementCurrentGroup();
            int grupo = Undo.GetCurrentGroup();

            Vector3 spawn; Vector3 frente;
            AcharJogador(out spawn, out frente);

            GameObject raiz = new GameObject(NOME_RAIZ);
            Undo.RegisterCreatedObjectUndo(raiz, "Popular cenário");
            Transform pastaPedras = NovaPasta(raiz, "Pedras");
            Transform pastaFlores = NovaPasta(raiz, "Flores e Grama");
            Transform pastaPOI = NovaPasta(raiz, "Pontos de Interesse");
            Transform pastaEfeitos = NovaPasta(raiz, "Efeitos");

            EditorUtility.DisplayProgressBar("Pinglu", "Ajustando luz e pós-processamento...", 0.1f);
            AjustarSol();
            CriarPosProcessamento(pastaEfeitos);
            CriarVento(pastaEfeitos);

            EditorUtility.DisplayProgressBar("Pinglu", "Escolhendo pontos de interesse...", 0.2f);
            EscolherPontosDeInteresse(spawn, frente);

            EditorUtility.DisplayProgressBar("Pinglu", "Plantando árvores...", 0.3f);
            Undo.RegisterCompleteObjectUndo(dados, "Popular cenário");
            PlantarArvores(spawn);

            EditorUtility.DisplayProgressBar("Pinglu", "Pintando plantas no terreno...", 0.5f);
            PintarDetalhes(spawn);

            EditorUtility.DisplayProgressBar("Pinglu", "Criando pontos de interesse...", 0.65f);
            CriarPortal(pastaPOI, spawn);
            CriarCirculoDePedras(pastaPOI, pastaEfeitos);
            CriarClareira(pastaPOI);
            CriarTrilha(pastaPOI);

            EditorUtility.DisplayProgressBar("Pinglu", "Espalhando pedras e flores...", 0.8f);
            EspalharPedras(pastaPedras, spawn);
            EspalharFlores(pastaFlores, spawn);
            EspalharGrama(pastaFlores, spawn);

            terreno.Flush();
            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
            Undo.CollapseUndoOperations(grupo);
            Selection.activeGameObject = raiz;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro no meio do processo: " + e.Message + " (veja o Console). Use 'Pinglu > 2. Desfazer' para voltar.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string msg = "Pronto! Dê Play para andar pelo cenário.\n\nSe gostar, salve a cena com Ctrl + S.\nSe não gostar, use Pinglu > 2. Desfazer cenário gerado.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        foreach (var a in avisos) Debug.LogWarning("[Pinglu] " + a);
    }

    // ==================================================================
    //  MENU 2 - DESFAZER
    // ==================================================================
    [MenuItem("Pinglu/2. Desfazer cenário gerado")]
    public static void DesfazerMenu() { Desfazer(true); }

    static void Desfazer(bool perguntar)
    {
        terreno = Terrain.activeTerrain;
        if (perguntar && !EditorUtility.DisplayDialog("Pinglu", "Remover tudo que o script adicionou e restaurar o terreno e o sol como estavam antes?", "Sim", "Cancelar"))
            return;

        GameObject raiz = GameObject.Find(NOME_RAIZ);
        if (raiz != null) Undo.DestroyObjectImmediate(raiz);

        TerrainData backup = AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP_TERRENO);
        if (terreno != null && backup != null)
        {
            TerrainData td = terreno.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Desfazer cenário");
            td.treePrototypes = backup.treePrototypes;
            td.SetTreeInstances(backup.treeInstances, true);
            td.detailPrototypes = backup.detailPrototypes;
            int res = backup.detailResolution;
            for (int i = 0; i < backup.detailPrototypes.Length; i++)
                td.SetDetailLayer(0, 0, i, backup.GetDetailLayer(0, 0, res, res, i));
            td.RefreshPrototypes();
            terreno.Flush();
            EditorUtility.SetDirty(td);
        }

        if (System.IO.File.Exists(BACKUP_LUZ))
        {
            Light sol = AcharSol();
            if (sol != null)
            {
                LuzSalva l = JsonUtility.FromJson<LuzSalva>(System.IO.File.ReadAllText(BACKUP_LUZ));
                Undo.RecordObject(sol.transform, "Desfazer cenário");
                Undo.RecordObject(sol, "Desfazer cenário");
                sol.transform.rotation = l.rotacao;
                sol.color = l.cor;
                sol.intensity = l.intensidade;
                sol.shadows = l.sombras;
            }
        }

        AssetDatabase.SaveAssets();
        if (terreno != null) EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        if (perguntar) EditorUtility.DisplayDialog("Pinglu", "Tudo desfeito. Salve a cena (Ctrl + S) para confirmar.", "OK");
    }

    // ==================================================================
    //  BACKUP
    // ==================================================================
    static void FazerBackup()
    {
        if (!AssetDatabase.IsValidFolder(PASTA)) AssetDatabase.CreateFolder("Assets", "Cenario");

        string caminhoTerreno = AssetDatabase.GetAssetPath(dados);
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP_TERRENO) != null) AssetDatabase.DeleteAsset(BACKUP_TERRENO);
        if (!AssetDatabase.CopyAsset(caminhoTerreno, BACKUP_TERRENO))
            throw new System.Exception("Não consegui fazer backup do terreno (" + caminhoTerreno + ").");

        Light sol = AcharSol();
        if (sol != null)
        {
            LuzSalva l = new LuzSalva { rotacao = sol.transform.rotation, cor = sol.color, intensidade = sol.intensity, sombras = sol.shadows };
            System.IO.File.WriteAllText(BACKUP_LUZ, JsonUtility.ToJson(l, true));
            AssetDatabase.ImportAsset(BACKUP_LUZ);
        }
    }

    // ==================================================================
    //  LUZ, PÓS-PROCESSAMENTO E VENTO
    // ==================================================================
    static Light AcharSol()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        GameObject go = GameObject.Find("Directional Light");
        return go != null ? go.GetComponent<Light>() : null;
    }

    static void AjustarSol()
    {
        Light sol = AcharSol();
        if (sol == null) { avisos.Add("Não achei a Directional Light, o sol não foi ajustado."); return; }
        Undo.RecordObject(sol.transform, "Popular cenário");
        Undo.RecordObject(sol, "Popular cenário");
        sol.transform.rotation = Quaternion.Euler(38f, -35f, 0f);  // sol de fim de manhã/tarde
        sol.color = new Color(1f, 0.93f, 0.82f);                    // levemente quente
        sol.intensity = 1.6f;
        sol.shadows = LightShadows.Soft;
        RenderSettings.sun = sol;
    }

    static void CriarPosProcessamento(Transform pai)
    {
        VolumeProfile perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PERFIL_POS);
        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, PERFIL_POS);

            Tonemapping tone = perfil.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            Bloom bloom = perfil.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.0f);

            ColorAdjustments cor = perfil.Add<ColorAdjustments>(true);
            cor.postExposure.Override(0.15f);
            cor.contrast.Override(12f);
            cor.saturation.Override(15f);

            Vignette vinheta = perfil.Add<Vignette>(true);
            vinheta.intensity.Override(0.25f);
            vinheta.smoothness.Override(0.45f);

            foreach (VolumeComponent c in perfil.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, perfil);
            }
            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();
        }

        GameObject go = new GameObject("Pos-Processamento (Global Volume)");
        go.transform.SetParent(pai, false);
        Volume vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.sharedProfile = perfil;
    }

    static void CriarVento(Transform pai)
    {
        GameObject go = new GameObject("Vento (Wind Zone)");
        go.transform.SetParent(pai, false);
        go.transform.rotation = Quaternion.Euler(0f, 60f, 0f);
        WindZone w = go.AddComponent<WindZone>();
        w.mode = WindZoneMode.Directional;
        w.windMain = 0.5f;
        w.windTurbulence = 0.4f;
        w.windPulseMagnitude = 0.4f;
        w.windPulseFrequency = 0.08f;
    }

    // ==================================================================
    //  JOGADOR E PONTOS DE INTERESSE
    // ==================================================================
    static void AcharJogador(out Vector3 spawn, out Vector3 frente)
    {
        Transform t = null;
        CharacterController cc = UnityEngine.Object.FindFirstObjectByType<CharacterController>(); // o jogador usa CharacterController
        if (cc != null) t = cc.transform;
        if (t == null) { GameObject g = GameObject.Find("X Bot"); if (g != null) t = g.transform; }

        if (t != null)
        {
            spawn = t.position;
            frente = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
        }
        else
        {
            avisos.Add("Não achei o personagem, usei a posição (605, 286) como centro.");
            spawn = new Vector3(605f, 0f, 286f);
            frente = Vector3.forward;
        }
        spawn.y = Altura(spawn.x, spawn.z);

        ocupado = new Grade(4f);
        ocupado.Adicionar(spawn, RAIO_LIVRE_SPAWN);

        // árvores que já existem viram obstáculo
        Vector3 tam = dados.size, origem = terreno.transform.position;
        foreach (TreeInstance ti in dados.treeInstances)
            ocupado.Adicionar(new Vector3(origem.x + ti.position.x * tam.x, 0f, origem.z + ti.position.z * tam.z), 1.5f);
    }

    static void EscolherPontosDeInteresse(Vector3 spawn, Vector3 frente)
    {
        // 1) Portal de ruína: na frente do jogador, visível logo ao começar
        Vector3 portal = AcharLugarPlano(spawn + frente * 38f, 12f);
        pois.Add(new PontoDeInteresse { pos = portal, raio = 10f });
        trilhaInicio = spawn + frente * 4f; trilhaFim = portal; temTrilha = true;

        // 2) Círculo de pedras: à direita, mais longe
        Vector3 dirCirculo = Quaternion.Euler(0f, 115f, 0f) * frente;
        pois.Add(new PontoDeInteresse { pos = AcharLugarPlano(spawn + dirCirculo * 70f, 18f), raio = 11f });

        // 3) Clareira com flores: à esquerda
        Vector3 dirClareira = Quaternion.Euler(0f, -110f, 0f) * frente;
        pois.Add(new PontoDeInteresse { pos = AcharLugarPlano(spawn + dirClareira * 55f, 15f), raio = 12f });
    }

    static Vector3 AcharLugarPlano(Vector3 alvo, float raioBusca)
    {
        Vector3 melhor = alvo; float melhorNota = float.MaxValue;
        for (int i = 0; i < 50; i++)
        {
            Vector2 d = DentroDoCirculo() * raioBusca;
            Vector3 p = new Vector3(alvo.x + d.x, 0f, alvo.z + d.y);
            if (!DentroDoTerreno(p, 15f)) continue;
            float nota = 0f;
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                nota += Inclinacao(p.x + Mathf.Cos(a) * 5f, p.z + Mathf.Sin(a) * 5f);
            }
            nota += d.magnitude * 0.3f; // prefere ficar perto do alvo
            if (nota < melhorNota) { melhorNota = nota; melhor = p; }
        }
        melhor.y = Altura(melhor.x, melhor.z);
        return melhor;
    }

    static bool PertoDeAlgumPOI(Vector3 p, float margem)
    {
        foreach (var poi in pois)
            if (Plano(p - poi.pos).magnitude < poi.raio + margem) return true;
        return false;
    }

    static float DistanciaDaTrilha(Vector3 p)
    {
        if (!temTrilha) return float.MaxValue;
        Vector2 a = new Vector2(trilhaInicio.x, trilhaInicio.z), b = new Vector2(trilhaFim.x, trilhaFim.z), q = new Vector2(p.x, p.z);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
        return (a + ab * t - q).magnitude;
    }

    // ==================================================================
    //  ÁRVORES (do próprio terreno, leves para o jogo)
    // ==================================================================
    static void PlantarArvores(Vector3 spawn)
    {
        List<TreePrototype> protos = new List<TreePrototype>(dados.treePrototypes);
        int[] idxArvore = new int[ARVORES.Length];
        for (int i = 0; i < ARVORES.Length; i++) idxArvore[i] = IndiceDoPrototipo(protos, ARVORES[i]);
        int idxArbusto = IndiceDoPrototipo(protos, ARBUSTO);
        dados.treePrototypes = protos.ToArray();
        dados.RefreshPrototypes();

        Vector3 tam = dados.size, origem = terreno.transform.position;
        List<TreeInstance> lista = new List<TreeInstance>();

        // remove árvores antigas de cima dos pontos de interesse, da trilha e do spawn
        foreach (TreeInstance ti in dados.treeInstances)
        {
            Vector3 w = new Vector3(origem.x + ti.position.x * tam.x, 0f, origem.z + ti.position.z * tam.z);
            if (PertoDeAlgumPOI(w, 0f) || DistanciaDaTrilha(w) < 3f || Plano(w - spawn).magnitude < RAIO_LIVRE_SPAWN) continue;
            lista.Add(ti);
        }

        // bosques
        for (int g = 0; g < GRUPOS_DE_ARVORES; g++)
        {
            Vector3 centro = PontoNaArea(spawn, RAIO_LIVRE_SPAWN + 18f, RAIO_AREA, 12f, 8f);
            if (centro.x == float.MinValue) continue;
            float raioBosque = Faixa(8f, 16f);
            int qtd = rng.Next(5, 13);
            for (int i = 0; i < qtd; i++)
            {
                Vector2 d = DentroDoCirculo() * raioBosque;
                Vector3 p = new Vector3(centro.x + d.x, 0f, centro.z + d.y);
                if (!PodeColocar(p, 3.5f, 30f, 6f, 4f)) continue;
                float s = Faixa(0.85f, 1.25f);
                lista.Add(NovaArvore(idxArvore[rng.Next(idxArvore.Length)], p, s));
                ocupado.Adicionar(p, 3.5f);
            }
            // arbustos na borda do bosque
            int qtdArb = rng.Next(3, 7);
            for (int i = 0; i < qtdArb; i++)
            {
                float a = Faixa(0f, Mathf.PI * 2f);
                float r = raioBosque * Faixa(0.9f, 1.3f);
                Vector3 p = new Vector3(centro.x + Mathf.Cos(a) * r, 0f, centro.z + Mathf.Sin(a) * r);
                if (!PodeColocar(p, 1.8f, 30f, 4f, 2.5f)) continue;
                lista.Add(NovaArvore(idxArbusto, p, Faixa(0.7f, 1.3f)));
                ocupado.Adicionar(p, 1.8f);
            }
        }

        // arbustos soltos
        for (int i = 0; i < ARBUSTOS_SOLTOS; i++)
        {
            Vector3 p = PontoNaArea(spawn, RAIO_LIVRE_SPAWN, RAIO_AREA, 3f, 2.5f);
            if (p.x == float.MinValue || !PodeColocar(p, 1.8f, 30f, 3f, 2.5f)) continue;
            lista.Add(NovaArvore(idxArbusto, p, Faixa(0.6f, 1.2f)));
            ocupado.Adicionar(p, 1.8f);
        }

        dados.SetTreeInstances(lista.ToArray(), true);
    }

    static int IndiceDoPrototipo(List<TreePrototype> protos, string caminho)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab == null) throw new System.Exception("Não achei o prefab " + caminho);
        for (int i = 0; i < protos.Count; i++) if (protos[i].prefab == prefab) return i;
        protos.Add(new TreePrototype { prefab = prefab });
        return protos.Count - 1;
    }

    static TreeInstance NovaArvore(int prototipo, Vector3 mundo, float escala)
    {
        Vector3 tam = dados.size, origem = terreno.transform.position;
        float nx = (mundo.x - origem.x) / tam.x, nz = (mundo.z - origem.z) / tam.z;
        TreeInstance ti = new TreeInstance();
        ti.prototypeIndex = prototipo;
        ti.position = new Vector3(nx, dados.GetInterpolatedHeight(nx, nz) / tam.y, nz);
        ti.widthScale = escala;
        ti.heightScale = escala * Faixa(0.92f, 1.08f);
        ti.rotation = Faixa(0f, Mathf.PI * 2f);
        ti.color = new Color32(255, 255, 255, 255);
        ti.lightmapColor = new Color32(255, 255, 255, 255);
        return ti;
    }

    // ==================================================================
    //  PLANTAS PINTADAS NO TERRENO (samambaias, urze, plantas)
    // ==================================================================
    static void PintarDetalhes(Vector3 spawn)
    {
        int res = dados.detailResolution;
        DetailPrototype[] antigos = dados.detailPrototypes;

        // guarda as camadas atuais (a grama que já existe)
        List<int[,]> camadasAntigas = new List<int[,]>();
        for (int i = 0; i < antigos.Length; i++) camadasAntigas.Add(dados.GetDetailLayer(0, 0, res, res, i));

        List<DetailPrototype> protos = new List<DetailPrototype>(antigos);
        List<int> novos = new List<int>();
        List<float> sementes = new List<float>();
        for (int i = 0; i < DETALHES.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DETALHES[i]);
            if (prefab == null) { avisos.Add("Não achei " + DETALHES[i]); continue; }

            int existente = -1;
            for (int k = 0; k < protos.Count; k++) if (protos[k].prototype == prefab) existente = k;
            if (existente < 0)
            {
                DetailPrototype dp = new DetailPrototype();
                dp.prototype = prefab;
                dp.usePrototypeMesh = true;
                dp.renderMode = DetailRenderMode.VertexLit;
                dp.useInstancing = true;
                dp.minWidth = 0.8f; dp.maxWidth = 1.3f;
                dp.minHeight = 0.8f; dp.maxHeight = 1.3f;
                dp.noiseSpread = 0.3f;
                dp.healthyColor = Color.white;
                dp.dryColor = new Color(0.9f, 0.88f, 0.75f);
                dp.density = DENSIDADES[i];
                dp.alignToGround = 0.6f;
                string erro;
                if (!dp.Validate(out erro)) { avisos.Add("A planta " + prefab.name + " não pôde ser usada no terreno: " + erro); continue; }
                protos.Add(dp);
                existente = protos.Count - 1;
            }
            novos.Add(existente);
            sementes.Add(Faixa(0f, 1000f));
        }

        dados.detailPrototypes = protos.ToArray();
        for (int i = 0; i < camadasAntigas.Count; i++) dados.SetDetailLayer(0, 0, i, camadasAntigas[i]);

        // região a pintar (quadrado em volta do spawn)
        Vector3 tam = dados.size, origem = terreno.transform.position;
        float celula = tam.x / res;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((spawn.x - RAIO_AREA - origem.x) / celula), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((spawn.z - RAIO_AREA - origem.z) / celula), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((spawn.x + RAIO_AREA - origem.x) / celula), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((spawn.z + RAIO_AREA - origem.z) / celula), 0, res - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;

        bool modoCobertura = dados.detailScatterMode == DetailScatterMode.CoverageMode;

        for (int n = 0; n < novos.Count; n++)
        {
            int[,] camada = dados.GetDetailLayer(x0, z0, w, h, novos[n]);
            for (int zz = 0; zz < h; zz++)
            {
                for (int xx = 0; xx < w; xx++)
                {
                    float wx = origem.x + (x0 + xx + 0.5f) * celula;
                    float wz = origem.z + (z0 + zz + 0.5f) * celula;
                    Vector3 p = new Vector3(wx, 0f, wz);
                    float dist = Plano(p - spawn).magnitude;
                    if (dist > RAIO_AREA || dist < RAIO_LIVRE_SPAWN * 0.5f) continue;
                    if (DistanciaDaTrilha(p) < 1.8f) continue;
                    if (Inclinacao(wx, wz) > 35f) continue;

                    float ruido = Mathf.PerlinNoise(wx * 0.045f + sementes[n], wz * 0.045f + sementes[n] * 0.7f);
                    float limiar = 0.58f;
                    if (ruido < limiar) continue;
                    float forca = Mathf.Clamp01((ruido - limiar) / (1f - limiar) * 2.5f);
                    forca *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RAIO_AREA * 0.75f, RAIO_AREA, dist));

                    int valor = modoCobertura ? Mathf.RoundToInt(forca * 255f) : Mathf.RoundToInt(forca * 3f);
                    camada[zz, xx] = Mathf.Max(camada[zz, xx], valor);
                }
            }
            dados.SetDetailLayer(x0, z0, novos[n], camada);
        }
        dados.RefreshPrototypes();
    }

    // ==================================================================
    //  PONTOS DE INTERESSE
    // ==================================================================
    static void CriarPortal(Transform pai, Vector3 spawn)
    {
        PontoDeInteresse poi = pois[0];
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(PORTAL);
        if (modelo == null)
        {
            avisos.Add("Não achei o modelo do portal de pedra. No lugar dele coloquei um arco de pedras.");
            CriarArcoDePedras(pai, poi.pos, spawn);
            return;
        }

        GameObject portal = (GameObject)PrefabUtility.InstantiatePrefab(modelo, pai.gameObject.scene);
        portal.name = "Portal de Ruina";
        portal.transform.SetParent(pai, true);
        portal.transform.position = poi.pos;
        Vector3 olhar = Plano(spawn - poi.pos);
        if (olhar.sqrMagnitude > 0.01f) portal.transform.rotation = Quaternion.LookRotation(olhar.normalized, Vector3.up) * modelo.transform.rotation;

        Bounds b;
        if (MedirBounds(portal, out b) && b.size.y > 0.001f)
        {
            float escala = Mathf.Clamp(7f / b.size.y, 0.001f, 1000f); // portal com ~7 m de altura
            portal.transform.localScale *= escala;
            MedirBounds(portal, out b);
            portal.transform.position += Vector3.up * (poi.pos.y - b.min.y - 0.3f);
        }
        if (portal.GetComponentInChildren<Collider>() == null)
            foreach (MeshFilter mf in portal.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>();

        // pedras e flores em volta
        for (int i = 0; i < 7; i++)
        {
            float a = Faixa(0f, Mathf.PI * 2f), r = Faixa(4.5f, 8f);
            Vector3 p = poi.pos + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (DistanciaDaTrilha(p) < 2f) continue;
            Colocar(Sortear(PEDRAS), pai, p, Faixa(0.8f, 2.2f), false, 0.15f, true);
        }
        Canteiro(pai, poi.pos + Plano(spawn - poi.pos).normalized * 3f + Vector3.Cross(Vector3.up, Plano(spawn - poi.pos).normalized) * 3.5f, 2.5f, 7);
        Canteiro(pai, poi.pos + Plano(spawn - poi.pos).normalized * 3f - Vector3.Cross(Vector3.up, Plano(spawn - poi.pos).normalized) * 3.5f, 2.5f, 7);
    }

    static void CriarArcoDePedras(Transform pai, Vector3 centro, Vector3 spawn)
    {
        Vector3 lado = Vector3.Cross(Vector3.up, Plano(spawn - centro).normalized);
        Colocar(PEDRAS[0], pai, centro + lado * 2.2f, 4.5f, true, 0.2f, false);
        Colocar(PEDRAS[0], pai, centro - lado * 2.2f, 4.5f, true, 0.2f, false);
    }

    static void CriarCirculoDePedras(Transform pai, Transform pastaEfeitos)
    {
        PontoDeInteresse poi = pois[1];
        Transform grupo = NovaPasta(pai.gameObject, "Circulo de Pedras");
        grupo.SetParent(pai, false);
        int qtd = 9;
        float raio = 7f;
        for (int i = 0; i < qtd; i++)
        {
            float a = i * Mathf.PI * 2f / qtd + Faixa(-0.08f, 0.08f);
            Vector3 p = poi.pos + new Vector3(Mathf.Cos(a) * raio, 0f, Mathf.Sin(a) * raio);
            GameObject pedra = Colocar(PEDRAS[0], grupo, p, Faixa(2.6f, 3.6f), true, 0.25f, false);
            if (pedra != null)
            {
                Vector3 paraCentro = Plano(poi.pos - p).normalized;
                pedra.transform.rotation = Quaternion.LookRotation(paraCentro, Vector3.up) * Quaternion.Euler(Faixa(-4f, 4f), 0f, Faixa(-4f, 4f));
                Vector3 s = pedra.transform.localScale;
                pedra.transform.localScale = new Vector3(s.x * 0.7f, s.y * 1.7f, s.z * 0.7f); // pedras em pé
            }
        }
        Colocar(PEDRAS[1], grupo, poi.pos, 1.6f, false, 0.3f, false); // pedra-altar no meio
        Canteiro(grupo, poi.pos, 4f, 14);
        CriarVagalumes(pastaEfeitos, poi.pos + Vector3.up * 1.5f, 8f);
    }

    static void CriarClareira(Transform pai)
    {
        PontoDeInteresse poi = pois[2];
        Transform grupo = NovaPasta(pai.gameObject, "Clareira de Flores");
        grupo.SetParent(pai, false);
        for (int i = 0; i < 6; i++)
        {
            Vector2 d = DentroDoCirculo() * 8f;
            Canteiro(grupo, poi.pos + new Vector3(d.x, 0f, d.y), 3f, 10);
        }
        Colocar(PEDRAS[1], grupo, poi.pos + new Vector3(1.5f, 0f, -1f), 3.2f, false, 0.4f, false);
        Colocar(PEDRINHAS, grupo, poi.pos + new Vector3(-2f, 0f, 2f), 2.5f, false, 0.05f, false);
        CriarVagalumes(grupo, poi.pos + Vector3.up * 1.2f, 9f);
    }

    static void CriarTrilha(Transform pai)
    {
        if (!temTrilha) return;
        Transform grupo = NovaPasta(pai.gameObject, "Trilha ate o Portal");
        grupo.SetParent(pai, false);
        Vector3 dir = Plano(trilhaFim - trilhaInicio);
        float comp = dir.magnitude; dir.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, dir);
        for (float t = 2f; t < comp - 6f; t += Faixa(3f, 4.5f))
        {
            float s = rng.NextDouble() < 0.5 ? 1f : -1f;
            Vector3 p = trilhaInicio + dir * t + lado * s * Faixa(1.6f, 2.4f);
            Colocar(PEDRINHAS, grupo, p, Faixa(1.2f, 2f), false, 0.05f, false);
            if (rng.NextDouble() < 0.6)
                Colocar(Sortear(GRAMAS), grupo, p + lado * s * 0.8f, Faixa(0.5f, 0.9f), true, 0f, false);
        }
    }

    static void CriarVagalumes(Transform pai, Vector3 centro, float raio)
    {
        GameObject go = new GameObject("Vagalumes");
        go.transform.SetParent(pai, true);
        go.transform.position = centro;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.45f), new Color(0.75f, 1f, 0.4f));
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emissao = ps.emission;
        emissao.rateOverTime = 9f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = raio;

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.6f;
        ruido.frequency = 0.4f;

        var corVida = ps.colorOverLifetime;
        corVida.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        corVida.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PARTICULA);
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh != null)
            {
                string caminho = PASTA + "/Vagalumes.mat";
                mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
                if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, caminho); }
            }
        }
        if (mat != null) rend.sharedMaterial = mat;
        else avisos.Add("Não achei material para os vagalumes; eles podem aparecer cor-de-rosa.");
    }

    // ==================================================================
    //  ESPALHAR PEDRAS, FLORES E GRAMA
    // ==================================================================
    static void EspalharPedras(Transform pai, Vector3 spawn)
    {
        for (int g = 0; g < GRUPOS_DE_PEDRAS; g++)
        {
            Vector3 c = PontoNaArea(spawn, RAIO_LIVRE_SPAWN + 4f, RAIO_AREA, 4f, 3f);
            if (c.x == float.MinValue) continue;
            Transform grupo = NovaPasta(pai.gameObject, "Grupo de Pedras " + (g + 1));
            grupo.SetParent(pai, false);
            Colocar(Sortear(PEDRAS), grupo, c, Faixa(1.8f, 4.5f), false, 0.2f, true);
            int menores = rng.Next(2, 5);
            for (int i = 0; i < menores; i++)
            {
                Vector2 d = DentroDoCirculo() * 3.5f;
                Colocar(Sortear(PEDRAS), grupo, c + new Vector3(d.x, 0f, d.y), Faixa(0.5f, 1.4f), false, 0.12f, true);
            }
            if (rng.NextDouble() < 0.7)
            {
                Vector2 d = DentroDoCirculo() * 3f;
                Colocar(PEDRINHAS, grupo, c + new Vector3(d.x, 0f, d.y), Faixa(1.5f, 2.5f), false, 0.05f, false);
            }
            ocupado.Adicionar(c, 4f);
        }
    }

    static void EspalharFlores(Transform pai, Vector3 spawn)
    {
        for (int i = 0; i < CANTEIROS_DE_FLORES; i++)
        {
            Vector3 c = PontoNaArea(spawn, RAIO_LIVRE_SPAWN * 0.6f, RAIO_AREA * 0.85f, 2f, 0f);
            if (c.x == float.MinValue) continue;
            Canteiro(pai, c, Faixa(1.5f, 3.5f), rng.Next(4, 10));
        }
    }

    static void EspalharGrama(Transform pai, Vector3 spawn)
    {
        for (int i = 0; i < TUFOS_DE_GRAMA; i++)
        {
            // mais tufos perto do jogador, menos longe
            float r = Mathf.Lerp(RAIO_LIVRE_SPAWN * 0.4f, RAIO_AREA * 0.7f, (float)(rng.NextDouble() * rng.NextDouble()));
            float a = Faixa(0f, Mathf.PI * 2f);
            Vector3 p = spawn + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (Inclinacao(p.x, p.z) > 30f || DistanciaDaTrilha(p) < 1.2f) continue;
            Colocar(Sortear(GRAMAS), pai, p, Faixa(0.5f, 1f), true, 0f, false);
        }
    }

    static void Canteiro(Transform pai, Vector3 centro, float raio, int qtd)
    {
        string tipo = Sortear(FLORES); // cada canteiro tem uma flor principal
        for (int i = 0; i < qtd; i++)
        {
            Vector2 d = DentroDoCirculo() * raio;
            Vector3 p = centro + new Vector3(d.x, 0f, d.y);
            if (Inclinacao(p.x, p.z) > 28f || DistanciaDaTrilha(p) < 1.2f) continue;
            string flor = rng.NextDouble() < 0.75 ? tipo : Sortear(FLORES);
            Colocar(flor, pai, p, Faixa(0.35f, 0.7f), true, 0f, false);
        }
    }

    // ==================================================================
    //  FUNÇÕES DE APOIO
    // ==================================================================

    // Coloca um prefab no chão. "tamanho" é a altura (usarAltura) ou a largura em metros.
    static GameObject Colocar(string caminho, Transform pai, Vector3 pos, float tamanho, bool usarAltura, float afundar, bool inclinar)
    {
        if (!DentroDoTerreno(pos, 1f)) return null;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab == null) { if (!avisos.Contains("Não achei " + caminho)) avisos.Add("Não achei " + caminho); return null; }

        Vector3 nativo = TamanhoNativo(caminho, prefab);
        float referencia = usarAltura ? nativo.y : Mathf.Max(nativo.x, nativo.z);
        float escala = referencia > 0.0001f ? tamanho / referencia : 1f;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.gameObject.scene);
        go.transform.SetParent(pai, true);

        float nx = (pos.x - terreno.transform.position.x) / dados.size.x;
        float nz = (pos.z - terreno.transform.position.z) / dados.size.z;
        Vector3 normal = dados.GetInterpolatedNormal(nx, nz);
        Quaternion giro = Quaternion.Euler(0f, Faixa(0f, 360f), 0f);
        Quaternion alinhar = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, normal, 0.6f).normalized);
        Quaternion torto = inclinar ? Quaternion.Euler(Faixa(-10f, 10f), 0f, Faixa(-10f, 10f)) : Quaternion.identity;

        go.transform.rotation = alinhar * giro * torto * prefab.transform.rotation;
        go.transform.localScale = prefab.transform.localScale * escala;
        go.transform.position = new Vector3(pos.x, Altura(pos.x, pos.z) - afundar * tamanho, pos.z);
        return go;
    }

    static Vector3 TamanhoNativo(string caminho, GameObject prefab)
    {
        Vector3 t;
        if (tamanhoNativo.TryGetValue(caminho, out t)) return t;
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = prefab.transform.rotation;
        Bounds b;
        t = MedirBounds(temp, out b) ? b.size : Vector3.one;
        Object.DestroyImmediate(temp);
        tamanhoNativo[caminho] = t;
        return t;
    }

    static bool MedirBounds(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        bool achou = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!achou) { b = r.bounds; achou = true; } else b.Encapsulate(r.bounds);
        }
        return achou;
    }

    // Sorteia um ponto livre dentro da área (anel entre raioMin e raioMax)
    static Vector3 PontoNaArea(Vector3 centro, float raioMin, float raioMax, float espaco, float margemPOI)
    {
        for (int tentativa = 0; tentativa < 40; tentativa++)
        {
            float a = Faixa(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Faixa(raioMin * raioMin, raioMax * raioMax));
            Vector3 p = centro + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (PodeColocar(p, espaco, 30f, margemPOI, 3f)) return p;
        }
        return new Vector3(float.MinValue, 0f, 0f);
    }

    static bool PodeColocar(Vector3 p, float espaco, float inclinacaoMax, float margemPOI, float margemTrilha)
    {
        if (!DentroDoTerreno(p, 5f)) return false;
        if (Inclinacao(p.x, p.z) > inclinacaoMax) return false;
        if (PertoDeAlgumPOI(p, margemPOI)) return false;
        if (DistanciaDaTrilha(p) < margemTrilha) return false;
        if (ocupado.Ocupado(p, espaco)) return false;
        return true;
    }

    static float Altura(float x, float z) { return terreno.SampleHeight(new Vector3(x, 0f, z)) + terreno.transform.position.y; }

    static float Inclinacao(float x, float z)
    {
        float nx = Mathf.Clamp01((x - terreno.transform.position.x) / dados.size.x);
        float nz = Mathf.Clamp01((z - terreno.transform.position.z) / dados.size.z);
        return dados.GetSteepness(nx, nz);
    }

    static bool DentroDoTerreno(Vector3 p, float margem)
    {
        Vector3 o = terreno.transform.position, s = dados.size;
        return p.x > o.x + margem && p.z > o.z + margem && p.x < o.x + s.x - margem && p.z < o.z + s.z - margem;
    }

    static Transform NovaPasta(GameObject pai, string nome)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(pai.transform, false);
        return go.transform;
    }

    static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }
    static float Faixa(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static string Sortear(string[] lista) { return lista[rng.Next(lista.Length)]; }
    static Vector2 DentroDoCirculo()
    {
        float a = Faixa(0f, Mathf.PI * 2f), r = Mathf.Sqrt((float)rng.NextDouble());
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    // Grade simples para evitar objetos uns em cima dos outros
    class Grade
    {
        readonly float celula;
        readonly Dictionary<long, List<Vector3>> celulas = new Dictionary<long, List<Vector3>>(); // x, z = posição, y = raio
        float maiorRaio;
        public Grade(float tamanhoCelula) { celula = tamanhoCelula; }
        long Chave(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public void Adicionar(Vector3 p, float raio)
        {
            int cx = Mathf.FloorToInt(p.x / celula), cz = Mathf.FloorToInt(p.z / celula);
            List<Vector3> l;
            long k = Chave(cx, cz);
            if (!celulas.TryGetValue(k, out l)) { l = new List<Vector3>(); celulas[k] = l; }
            l.Add(new Vector3(p.x, raio, p.z));
            if (raio > maiorRaio) maiorRaio = raio;
        }

        public bool Ocupado(Vector3 p, float raio)
        {
            int alcance = Mathf.CeilToInt((raio + maiorRaio) / celula);
            int cx = Mathf.FloorToInt(p.x / celula), cz = Mathf.FloorToInt(p.z / celula);
            for (int x = cx - alcance; x <= cx + alcance; x++)
                for (int z = cz - alcance; z <= cz + alcance; z++)
                {
                    List<Vector3> l;
                    if (!celulas.TryGetValue(Chave(x, z), out l)) continue;
                    foreach (Vector3 q in l)
                    {
                        float dx = q.x - p.x, dz = q.z - p.z, min = Mathf.Min(raio, q.y) + 0.5f * Mathf.Max(raio, q.y);
                        if (dx * dx + dz * dz < min * min) return true;
                    }
                }
            return false;
        }
    }
}
#endif
