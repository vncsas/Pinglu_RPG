// =====================================================================
//  PingluVila_Mundo.cs  -  Espalha pontos de interesse pelo mapa todo
// ---------------------------------------------------------------------
//  - 2 acampamentos de bandidos (paliçada, tendas, fogueira, torre de vigia)
//  - ruínas de uma fortaleza de pedra (muralhas quebradas, torres, torre central)
//  - cemitério sombrio (muro, lápides, mausoléu, árvores secas)
//  - natureza no mapa todo (bosques, pedras com colisão, troncos caídos,
//    tocos e árvores secas)
//
//  Usa as mesmas texturas e peças da vila (precisa do PingluVila.cs novo).
//
//  COMO USAR:  Pinglu > 9. Espalhar pontos pelo mapa
//  Para remover:  Pinglu > 10. Desfazer pontos do mapa
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string PASTA_MUNDO = "Assets/Cenario/Mundo";
    const string BACKUP_MUNDO = PASTA_MUNDO + "/Backup_Terreno_Mundo.asset";
    const string NOME_RAIZ_MUNDO = "Mundo_Gerado";
    const string PREFIXO_OCULTO_MUNDO = "[oculto pelo mapa] ";
    const int SEMENTE_MUNDO = 4242;

    struct Zona { public Vector3 pos; public float raio; }
    static List<Zona> zonas = new List<Zona>();
    static Dictionary<string, Vector3> tamanhoPrefab = new Dictionary<string, Vector3>();

    // ==================================================================
    //  MENU
    // ==================================================================
    [MenuItem("Pinglu/9. Espalhar pontos pelo mapa")]
    public static void ConstruirMundo()
    {
        terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain na cena. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;

        if (GameObject.Find(NOME_RAIZ_MUNDO) != null)
        {
            if (!EditorUtility.DisplayDialog("Pinglu", "Os pontos do mapa já foram criados. Quer apagar e criar de novo?", "Sim", "Cancelar")) return;
            DesfazerMundo(false);
        }
        if (!EditorUtility.DisplayDialog("Pinglu - Espalhar pontos pelo mapa",
            "Vou criar 2 acampamentos de bandidos, as ruínas de uma fortaleza, um cemitério e espalhar bosques, pedras, troncos e árvores secas pelo mapa todo.\n\n" +
            "Antes, guardo um backup do terreno para poder desfazer. Pode demorar um pouco.", "Pode fazer", "Cancelar")) return;

        avisos.Clear(); formas.Clear(); zonas.Clear(); tamanhoPrefab.Clear();
        luzesCriadas = 0; contadorMalhas = 0;
        malhaProp = new Mesh[8]; matsProp = new Material[8][];
        rng = new System.Random(SEMENTE_MUNDO);
        pastaMalhas = PASTA_MUNDO + "/Malhas";
        prefixoOculto = PREFIXO_OCULTO_MUNDO;

        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando...", 0.03f);
            if (!AssetDatabase.IsValidFolder("Assets/Cenario")) AssetDatabase.CreateFolder("Assets", "Cenario");
            if (!AssetDatabase.IsValidFolder(PASTA)) AssetDatabase.CreateFolder("Assets/Cenario", "Vila");
            if (!AssetDatabase.IsValidFolder(PASTA_MAT)) AssetDatabase.CreateFolder(PASTA, "Materiais_v2");
            if (!AssetDatabase.IsValidFolder(PASTA_MUNDO)) AssetDatabase.CreateFolder("Assets/Cenario", "Mundo");
            if (AssetDatabase.IsValidFolder(pastaMalhas)) AssetDatabase.DeleteAsset(pastaMalhas);
            AssetDatabase.CreateFolder(PASTA_MUNDO, "Malhas");
            ConfigurarTexturas();
            CriarMateriais();
            if (faltouTextura) avisos.Add("Faltaram texturas em " + PASTA_TEX + " (o zip da vila foi extraído?). Usei só cores.");

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP_MUNDO) != null) AssetDatabase.DeleteAsset(BACKUP_MUNDO);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dados), BACKUP_MUNDO)) throw new System.Exception("Não consegui fazer backup do terreno.");

            Undo.IncrementCurrentGroup();
            int grupo = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(dados, "Pontos do mapa");
            GameObject r = new GameObject(NOME_RAIZ_MUNDO);
            Undo.RegisterCreatedObjectUndo(r, "Pontos do mapa");
            raiz = r.transform;

            // ---- áreas que já estão ocupadas
            Vector3 spawn = AcharJogador();
            zonas.Add(new Zona { pos = spawn, raio = 110f });
            Vector3 centroVila = Vector3.zero; bool temVila = false;
            GameObject vila = GameObject.Find(NOME_RAIZ);
            Bounds bv;
            if (vila != null && MedirBounds(vila, out bv))
            {
                centroVila = bv.center; temVila = true;
                zonas.Add(new Zona { pos = bv.center, raio = Mathf.Max(bv.extents.x, bv.extents.z) * 0.75f + 20f });
            }
            foreach (Vector3 p in PontosDeInteresseDoCenario()) zonas.Add(new Zona { pos = p, raio = 25f });

            // ---- escolhe os lugares
            EditorUtility.DisplayProgressBar("Pinglu", "Escolhendo os lugares...", 0.1f);
            List<Vector3> acampamentos = new List<Vector3>();
            Vector3 ruinas = Vector3.zero, cemiterio = Vector3.zero;
            bool temRuinas = EscolherLugar(30f, true, false, Vector3.zero, 0f, out ruinas);
            Vector3 a1, a2;
            if (EscolherLugar(16f, false, false, Vector3.zero, 0f, out a1)) acampamentos.Add(a1);
            if (EscolherLugar(16f, false, false, Vector3.zero, 0f, out a2)) acampamentos.Add(a2);
            bool temCemiterio = EscolherLugar(15f, false, temVila, centroVila, 170f, out cemiterio);

            // ---- terreno: aplaina, limpa e pinta o chão
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando o terreno...", 0.2f);
            if (temRuinas) { AplainarLocal(ruinas, 24f, 38f, 0.85f); formas.Add(Circulo(ruinas, 25f, 1)); }
            foreach (Vector3 a in acampamentos) { AplainarLocal(a, 13f, 22f, 0.9f); formas.Add(Circulo(a, 13.5f, 1)); }
            if (temCemiterio) { AplainarLocal(cemiterio, 13f, 21f, 0.9f); formas.Add(Circulo(cemiterio, 12.5f, 1)); }
            Vector3 longe = new Vector3(-100000f, 0f, -100000f);
            if (formas.Count > 0)
            {
                LimparArvores(longe, 0f);
                LimparDetalhesFormas();
                PintarChao();
                OcultarCenarioNaArea(longe, 0f);
            }

            // ---- construções
            EditorUtility.DisplayProgressBar("Pinglu", "Construindo as ruínas...", 0.3f);
            if (temRuinas) Ruinas(ruinas, Plano(spawn - ruinas).normalized);
            for (int i = 0; i < acampamentos.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Pinglu", "Montando acampamento " + (i + 1) + "...", 0.45f + i * 0.08f);
                Acampamento(acampamentos[i], Plano(spawn - acampamentos[i]).normalized, i + 1);
            }
            EditorUtility.DisplayProgressBar("Pinglu", "Construindo o cemitério...", 0.62f);
            if (temCemiterio) Cemiterio(cemiterio, Plano((temVila ? centroVila : spawn) - cemiterio).normalized);

            EditorUtility.DisplayProgressBar("Pinglu", "Espalhando a natureza pelo mapa...", 0.72f);
            Natureza();

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
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console). Use Pinglu > 10. Desfazer pontos do mapa.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            pastaMalhas = PASTA_MALHAS;
            prefixoOculto = PREFIXO_OCULTO;
        }

        string msg = "Pronto! Os pontos foram espalhados pelo mapa. Na Hierarchy, dentro de \"" + NOME_RAIZ_MUNDO + "\", " +
                     "dê dois cliques em cada um para a câmera da janela Scene ir até ele.\n\nSe gostar, salve com Ctrl + S.\nPara remover: Pinglu > 10. Desfazer pontos do mapa.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        foreach (string a in avisos) Debug.LogWarning("[Pinglu] " + a);
    }

    [MenuItem("Pinglu/10. Desfazer pontos do mapa")]
    public static void DesfazerMundoMenu() { DesfazerMundo(true); }

    static void DesfazerMundo(bool perguntar)
    {
        if (perguntar && !EditorUtility.DisplayDialog("Pinglu", "Remover os pontos do mapa e restaurar o terreno como estava antes?", "Sim", "Cancelar")) return;
        terreno = Terrain.activeTerrain;
        GameObject r = GameObject.Find(NOME_RAIZ_MUNDO);
        if (r != null) Undo.DestroyObjectImmediate(r);
        RestaurarTerreno(BACKUP_MUNDO);
        foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g == null || !g.scene.IsValid() || !g.name.StartsWith(PREFIXO_OCULTO_MUNDO)) continue;
            Undo.RecordObject(g, "Desfazer pontos do mapa");
            g.name = g.name.Substring(PREFIXO_OCULTO_MUNDO.Length);
            g.SetActive(true);
        }
        AssetDatabase.SaveAssets();
        if (terreno != null) EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        if (perguntar) EditorUtility.DisplayDialog("Pinglu", "Pontos do mapa removidos. Salve a cena (Ctrl + S) para confirmar.", "OK");
    }

    static void RestaurarTerreno(string caminho)
    {
        TerrainData bk = AssetDatabase.LoadAssetAtPath<TerrainData>(caminho);
        if (terreno == null || bk == null) return;
        TerrainData td = terreno.terrainData;
        Undo.RegisterCompleteObjectUndo(td, "Restaurar terreno");
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

    // ==================================================================
    //  LUGARES E TERRENO
    // ==================================================================
    static bool Livre(Vector3 p, float raio)
    {
        foreach (Zona z in zonas) if (Plano(p - z.pos).magnitude < z.raio + raio) return false;
        return DentroDoTerreno(p, raio + 25f);
    }

    static bool EscolherLugar(float raio, bool preferirAlto, bool usarPerto, Vector3 perto, float distPerto, out Vector3 escolhido)
    {
        escolhido = Vector3.zero;
        float melhor = float.MaxValue;
        Vector3 o = terreno.transform.position, s = dados.size;
        for (float x = o.x + 40f; x < o.x + s.x - 40f; x += 22f)
            for (float z = o.z + 40f; z < o.z + s.z - 40f; z += 22f)
            {
                Vector3 c = new Vector3(x + Faixa(-6f, 6f), 0f, z + Faixa(-6f, 6f));
                if (!Livre(c, raio + 25f)) continue;
                float min = float.MaxValue, max = float.MinValue;
                for (float dx = -raio; dx <= raio; dx += raio / 3f)
                    for (float dz = -raio; dz <= raio; dz += raio / 3f)
                    {
                        if (dx * dx + dz * dz > raio * raio) continue;
                        float h = Altura(c.x + dx, c.z + dz);
                        min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                    }
                float nota = (max - min) + Faixa(0f, 1.5f);
                if (preferirAlto) nota -= (Altura(c.x, c.z) - o.y) * 0.08f;
                if (usarPerto) nota += Mathf.Abs(Plano(c - perto).magnitude - distPerto) * 0.03f;
                if (nota < melhor) { melhor = nota; escolhido = c; }
            }
        if (melhor == float.MaxValue) { avisos.Add("Não achei espaço livre para um dos pontos."); return false; }
        escolhido.y = Altura(escolhido.x, escolhido.z);
        zonas.Add(new Zona { pos = escolhido, raio = raio + 15f });
        return true;
    }

    static void AplainarLocal(Vector3 c, float rNucleo, float rBorda, float peso)
    {
        float soma = 0f; int n = 0;
        for (float x = -rNucleo; x <= rNucleo; x += 3f)
            for (float z = -rNucleo; z <= rNucleo; z += 3f)
                if (x * x + z * z <= rNucleo * rNucleo) { soma += Altura(c.x + x, c.z + z); n++; }
        float alvo = (soma / Mathf.Max(1, n) - terreno.transform.position.y) / dados.size.y;
        int res = dados.heightmapResolution;
        Vector3 o = terreno.transform.position, s = dados.size;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - rBorda - o.x) / s.x * (res - 1)), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - rBorda - o.z) / s.z * (res - 1)), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + rBorda - o.x) / s.x * (res - 1)), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + rBorda - o.z) / s.z * (res - 1)), 0, res - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        float[,] alt = dados.GetHeights(x0, z0, w, h);
        for (int zz = 0; zz < h; zz++)
            for (int xx = 0; xx < w; xx++)
            {
                float wx = o.x + (x0 + xx) / (float)(res - 1) * s.x, wz = o.z + (z0 + zz) / (float)(res - 1) * s.z;
                float d = new Vector2(wx - c.x, wz - c.z).magnitude;
                float p = peso * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rNucleo, rBorda, d)));
                alt[zz, xx] = Mathf.Lerp(alt[zz, xx], alvo, p);
            }
        dados.SetHeights(x0, z0, alt);
    }

    static void LimparDetalhesFormas()
    {
        int res = dados.detailResolution;
        int camadas = dados.detailPrototypes.Length;
        if (camadas == 0) return;
        Vector3 o = terreno.transform.position;
        float cel = dados.size.x / res;
        foreach (Forma f in formas)
        {
            float r = f.raio + 1f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(f.a.x, f.b.x) - r - o.x) / cel), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(f.a.y, f.b.y) - r - o.z) / cel), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(f.a.x, f.b.x) + r - o.x) / cel), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(f.a.y, f.b.y) + r - o.z) / cel), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            for (int l = 0; l < camadas; l++)
            {
                int[,] mapa = dados.GetDetailLayer(x0, z0, w, h, l);
                for (int zz = 0; zz < h; zz++)
                    for (int xx = 0; xx < w; xx++)
                    {
                        Vector3 p = new Vector3(o.x + (x0 + xx + 0.5f) * cel, 0f, o.z + (z0 + zz + 0.5f) * cel);
                        float d = Distancia(f, p);
                        if (d < f.raio - 2f) mapa[zz, xx] = 0;
                        else if (d < f.raio + 1f) mapa[zz, xx] /= 3;
                    }
                dados.SetDetailLayer(x0, z0, l, mapa);
            }
        }
    }

    // altura do chão num ponto local de um objeto (para apoiar peças no terreno)
    static float ChaoLocal(Vector3 c, Quaternion rot, Vector3 local)
    {
        Vector3 w = c + rot * local;
        return Altura(w.x, w.z) - c.y;
    }

    // ==================================================================
    //  ACAMPAMENTO DE BANDIDOS
    // ==================================================================
    static void Acampamento(Vector3 c, Vector3 paraEntrada, int numero)
    {
        Transform pasta = Pasta("Acampamento de Bandidos " + numero);
        Quaternion rot = Quaternion.LookRotation(paraEntrada, Vector3.up); // +Z local = entrada
        float R = 12.5f;

        // paliçada de troncos pontudos, com abertura na frente
        Malha pal = new Malha();
        float passo = 0.34f / R;
        for (float a = 0f; a < Mathf.PI * 2f; a += passo * Faixa(0.9f, 1.1f))
        {
            float ang = Mathf.DeltaAngle(0f, a * Mathf.Rad2Deg);
            if (Mathf.Abs(ang) < 9f) continue; // portão
            Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 p = d * R;
            float y = ChaoLocal(c, rot, p) - 0.5f;
            float alt = Faixa(2.8f, 3.5f), raio = Faixa(0.13f, 0.18f);
            Quaternion inc = Quaternion.AngleAxis(Faixa(3f, 8f), Vector3.Cross(Vector3.up, d)) ;
            pal.Cilindro(new Vector3(p.x, y, p.z), raio, alt, 7, MAD_ESC, inc);
            pal.Piramide(new Vector3(p.x, y, p.z) + inc * (Vector3.up * alt), raio * 0.9f, 0.45f, MAD_ESC);
        }
        // amarras horizontais
        for (float a = 0f; a < Mathf.PI * 2f; a += 2.2f / R)
        {
            float ang = Mathf.DeltaAngle(0f, a * Mathf.Rad2Deg);
            if (Mathf.Abs(ang) < 14f) continue;
            float a2 = a + 2.2f / R;
            Vector3 p1 = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (R - 0.25f), p2 = new Vector3(Mathf.Sin(a2), 0f, Mathf.Cos(a2)) * (R - 0.25f);
            float y = ChaoLocal(c, rot, (p1 + p2) * 0.5f);
            foreach (float hy in new[] { 0.8f, 2.0f })
                Barra(pal, p1 + Vector3.up * (y + hy), p2 + Vector3.up * (y + hy), Vector3.up, 0.12f, 0.12f, MAD_VERM, 0f);
        }
        // portão: dois postes grossos, viga e pano rasgado
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 p = new Vector3(Mathf.Sin(s * 11f * Mathf.Deg2Rad), 0f, Mathf.Cos(s * 11f * Mathf.Deg2Rad)) * R;
            float y = ChaoLocal(c, rot, p) - 0.5f;
            pal.Cilindro(new Vector3(p.x, y, p.z), 0.25f, 4.6f, 8, MAD_ESC, Quaternion.identity);
            pal.Piramide(new Vector3(p.x, y + 4.6f, p.z), 0.24f, 0.5f, MAD_ESC);
        }
        float yPortao = ChaoLocal(c, rot, new Vector3(0f, 0f, R));
        pal.Caixa(new Vector3(0f, yPortao + 3.7f, R), new Vector3(5.2f, 0.3f, 0.3f), MAD_ESC);
        pal.Caixa(new Vector3(-0.7f, yPortao + 2.9f, R + 0.17f), new Vector3(0.9f, 1.4f, 0.02f), TEC_VERM);
        pal.Caixa(new Vector3(0.8f, yPortao + 3.1f, R + 0.17f), new Vector3(0.7f, 1.0f, 0.02f), TEC_VERM);
        Instanciar(pal, "Palicada", c, rot, pasta, true);

        // torre de vigia no fundo
        Malha torre = new Malha();
        TorreDeVigia(torre);
        Vector3 posTorre = c + rot * new Vector3(-4.5f, 0f, -8.6f);
        Instanciar(torre, "Torre de Vigia", NoChao(posTorre), rot, pasta, true);

        // tendas em volta da fogueira, com a abertura virada para o centro
        int[] cores = { TEC_VERM, TEC_OCRE, TEC_AZUL, TEC_OCRE, TEC_VERM };
        float[] angs = { 60f, 120f, 180f, 240f, 300f };
        for (int i = 0; i < angs.Length; i++)
        {
            Vector3 d = rot * (Quaternion.Euler(0f, angs[i], 0f) * Vector3.forward);
            bool grande = i == 2;
            Malha t = new Malha();
            Tenda(t, grande ? 4f : 2.6f, grande ? 4.5f : 3.4f, grande ? 2.8f : 2.0f, cores[i]);
            Vector3 p = c + d * (grande ? 8.3f : 7.3f);
            Instanciar(t, grande ? "Tenda do Chefe" : "Tenda", NoChao(p), Quaternion.LookRotation(-d, Vector3.up), pasta, true);
            Props(pasta, p + Quaternion.Euler(0f, 90f, 0f) * d * Faixa(1.6f, 2.2f) + d * 0.5f, rng.Next(0, 2));
            if (grande)
            {
                Props(pasta, p - d * 0.3f, 3, Quaternion.LookRotation(-d));
                Props(pasta, p - Quaternion.Euler(0f, 90f, 0f) * d * 2.5f, 5, Quaternion.LookRotation(-d));
            }
        }

        // fogueira com troncos de sentar
        Malha fog = new Malha();
        Fogueira(fog);
        GameObject gf = Instanciar(fog, "Fogueira", NoChao(c), rot, pasta, true);
        Fogo(gf.transform, new Vector3(0f, 0.25f, 0f));
        Luz(gf.transform, new Vector3(0f, 1.2f, 0f), new Color(1f, 0.5f, 0.2f), 11f, 3f, true);

        // saque, barris e caixas espalhados
        for (int i = 0; i < 8; i++)
        {
            float a = Faixa(0f, 360f);
            Vector3 p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Faixa(4.5f, 10.5f);
            if (Plano(p - posTorre).magnitude < 2.5f) continue;
            if (Mathf.Abs(Mathf.DeltaAngle(a, Mathf.Atan2(paraEntrada.x, paraEntrada.z) * Mathf.Rad2Deg)) < 20f) continue;
            Props(pasta, p, i % 3 == 0 ? 3 : rng.Next(0, 2));
        }
        Props(pasta, c + rot * new Vector3(-6f, 0f, -6f), 5, rot * Quaternion.Euler(0f, 45f, 0f));
        Malha carroca = new Malha(); Carroca(carroca);
        Instanciar(carroca, "Carroca", NoChao(c + rot * new Vector3(4.5f, 0f, 8.2f)), rot * Quaternion.Euler(0f, 70f, 0f), pasta, true);
    }

    static void TorreDeVigia(Malha m)
    {
        float l = 2.4f, h = 4.2f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.Caixa(new Vector3(sx * l * 0.5f, (h + 1.2f) * 0.5f - 0.3f, sz * l * 0.5f), new Vector3(0.22f, h + 1.2f + 0.6f, 0.22f), MAD_ESC);
        for (int s = -1; s <= 1; s += 2)
        {
            Diagonal(m, new Vector3(s * l * 0.5f, 0.2f, -l * 0.5f), new Vector3(-s * l * 0.5f, h - 0.3f, -l * 0.5f), 0.12f, MAD_ESC);
            Diagonal(m, new Vector3(s * l * 0.5f, 0.2f, l * 0.5f), new Vector3(-s * l * 0.5f, h - 0.3f, l * 0.5f), 0.12f, MAD_ESC);
        }
        m.Caixa(new Vector3(0f, h, 0f), new Vector3(l + 0.5f, 0.14f, l + 0.5f), TABUAS);
        for (int s = -1; s <= 1; s += 2)
        {
            m.Caixa(new Vector3(0f, h + 0.9f, s * (l * 0.5f + 0.2f)), new Vector3(l + 0.5f, 0.1f, 0.08f), MAD_VERM);
            m.Caixa(new Vector3(s * (l * 0.5f + 0.2f), h + 0.9f, 0f), new Vector3(0.08f, 0.1f, l + 0.5f), MAD_VERM);
        }
        Telhado(m, new Vector3(0f, h + 1.2f, 0f), l + 0.3f, l + 0.3f, 35f, true, 0.3f, 0.3f, TELHA_ARDOSIA, -1, MAD_ESC, false, false);
        // escada
        Vector3 b = new Vector3(0f, 0f, l * 0.5f + 0.9f);
        for (int s = -1; s <= 1; s += 2) Diagonal(m, b + new Vector3(s * 0.3f, 0f, 0f), new Vector3(s * 0.3f, h + 0.1f, l * 0.5f + 0.15f), 0.07f, MAD_VERM);
        for (int k = 1; k < 11; k++)
        {
            float t = k / 11f;
            Vector3 p = Vector3.Lerp(b, new Vector3(0f, h + 0.1f, l * 0.5f + 0.15f), t);
            m.Caixa(p, new Vector3(0.6f, 0.05f, 0.05f), MAD_VERM);
        }
        m.Caixa(new Vector3(0.6f, h + 0.45f, 0.3f), new Vector3(0.5f, 0.75f, 0.5f), TABUAS);
    }

    static void Tenda(Malha m, float w, float d, float h, int cor)
    {
        // tenda em "A": duas lonas, cumeeira, varas e fundo fechado. Abertura no +Z.
        for (int s = -1; s <= 1; s += 2)
            Barra(m, new Vector3(0f, h, 0f), new Vector3(s * w * 0.5f, 0.05f, 0f), Vector3.forward, d, 0.04f, cor, 0.02f);
        m.Triangulo2Lados(new Vector3(-w * 0.5f, 0.05f, -d * 0.5f), new Vector3(w * 0.5f, 0.05f, -d * 0.5f), new Vector3(0f, h, -d * 0.5f), cor);
        m.Cilindro(new Vector3(0f, h + 0.04f, -d * 0.5f - 0.2f), 0.04f, d + 0.4f, 6, MAD_ESC, Quaternion.Euler(90f, 0f, 0f));
        for (int s = -1; s <= 1; s += 2) m.Cilindro(new Vector3(0f, 0f, s * (d * 0.5f + 0.1f)), 0.045f, h + 0.25f, 6, MAD_ESC, Quaternion.identity);
        // abas da entrada meio abertas
        for (int s = -1; s <= 1; s += 2)
            m.Triangulo2Lados(new Vector3(0f, h, d * 0.5f), new Vector3(s * w * 0.5f, 0.05f, d * 0.5f), new Vector3(s * w * 0.3f, 0.05f, d * 0.5f + 0.5f), cor);
        // estacas e saco de dormir
        for (int s = -1; s <= 1; s += 2) for (int e = -1; e <= 1; e += 2)
                m.Caixa(new Vector3(s * (w * 0.5f + 0.15f), 0.1f, e * d * 0.35f), new Vector3(0.05f, 0.25f, 0.05f), MAD_VERM);
        m.Caixa(new Vector3(-w * 0.18f, 0.06f, 0f), new Vector3(0.7f, 0.12f, d * 0.7f), COURO);
    }

    static void Fogueira(Malha m)
    {
        for (int i = 0; i < 11; i++)
        {
            float a = i * Mathf.PI * 2f / 11f;
            m.Caixa(new Vector3(Mathf.Sin(a), 0.1f, Mathf.Cos(a)) * 0.75f, new Vector3(0.32f, 0.24f, 0.26f), Quaternion.Euler(Faixa(-15f, 15f), a * Mathf.Rad2Deg + Faixa(-20f, 20f), 0f), PEDRA_ESCURA);
        }
        m.Caixa(new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.08f, 0.9f), BRASA);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 20f;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Diagonal(m, d * 0.55f + Vector3.up * 0.05f, Vector3.up * 0.55f, 0.11f, MAD_VERM);
        }
        // troncos para sentar
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f + 40f;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, d);
            m.Cilindro(d * 2.8f - lado * 0.9f + Vector3.up * 0.22f, 0.22f, 1.8f, 8, MAD_ESC, Quaternion.FromToRotation(Vector3.up, lado));
        }
        // espeto
        for (int s = -1; s <= 1; s += 2) m.Caixa(new Vector3(s * 0.75f, 0.5f, 0f), new Vector3(0.05f, 1.0f, 0.05f), MAD_ESC);
        m.Cilindro(new Vector3(-0.9f, 0.95f, 0f), 0.025f, 1.8f, 6, MAD_ESC, Quaternion.Euler(0f, 0f, -90f));
        m.Caixa(new Vector3(0f, 0.95f, 0f), new Vector3(0.45f, 0.2f, 0.25f), COURO);
    }

    static void Fogo(Transform pai, Vector3 local)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PARTICULA_URP);
        for (int k = 0; k < 2; k++)
        {
            bool fumaca = k == 1;
            GameObject go = new GameObject(fumaca ? "Fumaca" : "Fogo");
            go.transform.SetParent(pai, false);
            go.transform.localPosition = local + (fumaca ? Vector3.up * 0.6f : Vector3.zero);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = fumaca ? new ParticleSystem.MinMaxCurve(2.5f, 4f) : new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = fumaca ? new ParticleSystem.MinMaxCurve(0.5f, 0.9f) : new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSize = fumaca ? new ParticleSystem.MinMaxCurve(0.5f, 0.9f) : new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = fumaca ? new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.25f, 0.25f, 0.35f), new Color(0.35f, 0.35f, 0.35f, 0.25f))
                                     : new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.15f), new Color(1f, 0.3f, 0.05f));
            main.maxParticles = fumaca ? 40 : 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = fumaca ? 8f : 35f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = fumaca ? 15f : 8f; sh.radius = fumaca ? 0.2f : 0.3f;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, fumaca ? AnimationCurve.Linear(0f, 0.6f, 1f, 2.5f) : AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            var col = ps.colorOverLifetime; col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            if (mat != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        }
    }

    const string MATERIAL_PARTICULA_URP = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    // ==================================================================
    //  RUÍNAS DA FORTALEZA
    // ==================================================================
    static void Ruinas(Vector3 c, Vector3 frente)
    {
        Transform pasta = Pasta("Ruinas da Fortaleza");
        Quaternion rot = Quaternion.LookRotation(frente, Vector3.up) * Quaternion.Euler(0f, Faixa(-15f, 15f), 0f);
        float L = 36f, T = 1.6f, H = 6f;
        Malha m = new Malha();

        // quatro muralhas (a da frente tem o portão)
        Vector3[] cantos = { new Vector3(-L / 2, 0, L / 2), new Vector3(L / 2, 0, L / 2), new Vector3(L / 2, 0, -L / 2), new Vector3(-L / 2, 0, -L / 2) };
        for (int lado = 0; lado < 4; lado++)
        {
            Vector3 a = cantos[lado], b = cantos[(lado + 1) % 4];
            Vector3 dir = (b - a).normalized;
            float comp = (b - a).magnitude - 6.5f;
            Vector3 ini = a + dir * 3.25f;
            Vector3 dentro = Vector3.Cross(Vector3.up, dir); // aponta para dentro do pátio
            int nSeg = Mathf.RoundToInt(comp / 3f);
            float seg = comp / nSeg;
            for (int i = 0; i < nSeg; i++)
            {
                Vector3 meio = ini + dir * (seg * (i + 0.5f));
                bool portao = lado == 0 && Mathf.Abs(i - (nSeg - 1) * 0.5f) < 0.75f;
                if (portao) continue;
                double sorte = rng.NextDouble();
                int estado = sorte < 0.45 ? 0 : (sorte < 0.82 ? 1 : 2); // inteiro, quebrado, desabado
                Muro(m, c, rot, meio, dir, dentro, seg, T, H, estado);
            }
            // escada de pedra na muralha de trás
            if (lado == 2)
            {
                for (int k = 0; k < 14; k++)
                {
                    Vector3 p = ini + dir * (comp * 0.3f + k * 0.45f) + dentro * (T * 0.5f + 0.6f);
                    float y0 = ChaoLocal(c, rot, p) - 0.4f, topo = 0.3f * (k + 1);
                    m.Caixa(new Vector3(p.x, (y0 + topo) * 0.5f, p.z), new Vector3(0.5f, topo - y0, 1.2f), Quaternion.LookRotation(dentro), PEDRA_BLOCOS);
                }
            }
        }
        // portão: pilares, lintel quebrado e grade de ferro caída
        Vector3 cp = new Vector3(0f, 0f, L / 2);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 p = cp + new Vector3(s * 2.6f, 0f, 0f);
            float y0 = ChaoLocal(c, rot, p) - 0.6f;
            float hp = s < 0 ? 8.2f : 6.5f;
            m.Caixa(new Vector3(p.x, (y0 + hp) * 0.5f, p.z), new Vector3(2.2f, hp - y0, T + 0.8f), PEDRA_BLOCOS);
            Ameias(m, new Vector3(p.x, hp, p.z), Vector3.right, 2.2f, T + 0.8f, s < 0);
        }
        m.Caixa(new Vector3(-1.3f, 7.3f, L / 2), new Vector3(2.6f, 0.8f, T), PEDRA_BLOCOS);
        for (int k = 0; k < 6; k++)
            m.Caixa(new Vector3(-1.2f + k * 0.45f, 0.25f, L / 2 - 1.5f + k * 0.1f), new Vector3(0.06f, 0.06f, 2.4f), Quaternion.Euler(Faixa(-5f, 5f), Faixa(-10f, 10f), 0f), FERRO);

        // torres nos cantos (uma inteira, uma quebrada, duas meio destruídas)
        float[] alturas = { 11f, 5.5f, 8f, 9.5f };
        for (int k = 0; k < 4; k++)
            TorreRuina(m, c, rot, cantos[k], 7f, alturas[k], k == 0);

        // torre de menagem no meio
        Menagem(m, c, rot, new Vector3(0f, 0f, -4f));

        // colunas quebradas de um antigo salão e entulho
        for (int k = 0; k < 5; k++)
        {
            Vector3 p = new Vector3(-8f, 0f, 10f - k * 4f);
            float y0 = ChaoLocal(c, rot, p) - 0.3f;
            float hc = k == 2 ? 0.4f : Faixa(0.8f, 3.2f);
            m.Cilindro(new Vector3(p.x, y0, p.z), 0.45f, hc + 0.3f, 10, PEDRA_BLOCOS, Quaternion.identity);
            m.Caixa(new Vector3(p.x, y0 + 0.15f, p.z), new Vector3(1.2f, 0.3f, 1.2f), PEDRA_ESCURA);
        }
        Vector3 caida = new Vector3(-5.5f, 0f, 2f);
        m.Cilindro(new Vector3(caida.x, ChaoLocal(c, rot, caida) + 0.42f, caida.z), 0.42f, 3.4f, 10, PEDRA_BLOCOS, Quaternion.Euler(0f, 0f, -90f) * Quaternion.Euler(0f, 30f, 0f));
        for (int k = 0; k < 45; k++)
        {
            Vector3 p = new Vector3(Faixa(-L / 2 + 3f, L / 2 - 3f), 0f, Faixa(-L / 2 + 3f, L / 2 - 3f));
            Entulho(m, c, rot, p, Faixa(0.3f, 1.1f));
        }
        Instanciar(m, "Fortaleza em Ruinas", c, rot, pasta, true);

        // estandarte rasgado na torre central e alguns objetos abandonados
        Malha est = new Malha();
        est.Cilindro(Vector3.zero, 0.05f, 3.5f, 6, MAD_ESC, Quaternion.identity);
        est.Caixa(new Vector3(0f, 3.4f, 0.35f), new Vector3(0.05f, 0.05f, 0.75f), MAD_ESC);
        est.Caixa(new Vector3(0f, 2.6f, 0.38f), new Vector3(0.02f, 1.5f, 0.6f), TEC_VERM);
        est.Painel(new Vector3(0.015f, 2.75f, 0.38f), Vector3.right, 0.4f, 0.5f, P_BRASAO);
        Vector3 topoMenagem = c + rot * new Vector3(3.5f, 0f, -4f);
        Instanciar(est, "Estandarte Rasgado", new Vector3(topoMenagem.x, alturaMenagem + c.y, topoMenagem.z), rot, pasta, true);
        Props(pasta, c + rot * new Vector3(6f, 0f, 8f), 3, rot);
        Props(pasta, c + rot * new Vector3(7f, 0f, 9f), 1);
        Escudo(pasta, c + rot * new Vector3(9f, 0f, 3f), rot * Vector3.right);
    }

    static float alturaMenagem;

    static void Muro(Malha m, Vector3 c, Quaternion rot, Vector3 meio, Vector3 dir, Vector3 dentro, float seg, float T, float H, int estado)
    {
        float y0 = Mathf.Min(ChaoLocal(c, rot, meio - dir * seg * 0.5f), ChaoLocal(c, rot, meio + dir * seg * 0.5f)) - 0.6f;
        Quaternion r = Quaternion.LookRotation(dentro);
        float h = estado == 0 ? H : (estado == 1 ? H * Faixa(0.4f, 0.85f) : Faixa(0.4f, 1.3f));
        m.Caixa(new Vector3(meio.x, (y0 + h) * 0.5f, meio.z), new Vector3(seg + 0.02f, h - y0, T), r, PEDRA_BLOCOS);
        m.Caixa(new Vector3(meio.x, y0 + 0.75f, meio.z), new Vector3(seg + 0.04f, 1.5f, T + 0.2f), r, PEDRA_ESCURA);
        if (estado == 0) Ameias(m, new Vector3(meio.x, h, meio.z), dir, seg, T, false);
        else if (estado == 1)
        {
            // topo irregular
            float x = -seg * 0.5f;
            while (x < seg * 0.5f - 0.3f)
            {
                float w = Faixa(0.6f, 1.3f);
                float hh = Faixa(0.2f, 1.2f);
                if (rng.NextDouble() < 0.6)
                    m.Caixa(new Vector3(meio.x, h + hh * 0.5f, meio.z) + dir * (x + w * 0.5f), new Vector3(Mathf.Min(w, seg * 0.5f - x), hh, T * Faixa(0.6f, 1f)), r, PEDRA_BLOCOS);
                x += w;
            }
        }
        else
        {
            for (int k = 0; k < 7; k++)
                Entulho(m, c, rot, meio + dir * Faixa(-seg * 0.5f, seg * 0.5f) + dentro * Faixa(-2.5f, 2.5f), Faixa(0.4f, 1.2f));
        }
    }

    static void Ameias(Malha m, Vector3 topoCentro, Vector3 dir, float comp, float T, bool quebradas)
    {
        Quaternion r = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir));
        int n = Mathf.Max(1, Mathf.FloorToInt(comp / 1.0f));
        for (int i = 0; i < n; i++)
        {
            if (i % 2 == 1) continue;
            if (quebradas && rng.NextDouble() < 0.4) continue;
            float x = -comp * 0.5f + (i + 0.5f) * comp / n;
            m.Caixa(topoCentro + dir * x + Vector3.up * 0.45f, new Vector3(comp / n, 0.9f, T), r, PEDRA_BLOCOS);
        }
    }

    static void TorreRuina(Malha m, Vector3 c, Quaternion rot, Vector3 centro, float lado, float h, bool inteira)
    {
        float y0 = ChaoLocal(c, rot, centro) - 1f;
        m.Caixa(new Vector3(centro.x, (y0 + h) * 0.5f, centro.z), new Vector3(lado, h - y0, lado), PEDRA_BLOCOS);
        m.Caixa(new Vector3(centro.x, y0 + 1.2f, centro.z), new Vector3(lado + 0.5f, 2.4f, lado + 0.5f), PEDRA_ESCURA);
        if (inteira)
        {
            m.Caixa(new Vector3(centro.x, h + 0.15f, centro.z), new Vector3(lado + 0.5f, 0.3f, lado + 0.5f), PEDRA_ESCURA);
            Vector3[] dirs = { Vector3.right, Vector3.forward };
            foreach (Vector3 d in dirs)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 perp = Vector3.Cross(Vector3.up, d);
                    Ameias(m, new Vector3(centro.x, h + 0.3f, centro.z) + perp * s * (lado * 0.5f - 0.4f), d, lado + 0.4f, 0.8f, false);
                }
        }
        else
        {
            for (int k = 0; k < 6; k++)
            {
                float w = Faixa(0.8f, 2.2f);
                m.Caixa(new Vector3(centro.x + Faixa(-lado * 0.35f, lado * 0.35f), h + Faixa(0.2f, 0.9f), centro.z + Faixa(-lado * 0.35f, lado * 0.35f)), new Vector3(w, Faixa(0.4f, 1.6f), w), PEDRA_BLOCOS);
            }
            for (int k = 0; k < 8; k++) Entulho(m, c, rot, centro + new Vector3(Faixa(-6f, 6f), 0f, Faixa(-6f, 6f)), Faixa(0.4f, 1.3f));
        }
        // seteiras
        Vector3[] faces = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        foreach (Vector3 f in faces)
            for (float y = 2.5f; y < h - 1f; y += 3f)
                m.Painel(new Vector3(centro.x, y, centro.z) + f * (lado * 0.5f + 0.02f), f, 0.4f, 1.0f, P_ARCO_SINO);
    }

    static void Menagem(Malha m, Vector3 c, Quaternion rot, Vector3 centro)
    {
        float lado = 11f, h = 13f;
        float y0 = ChaoLocal(c, rot, centro) - 1f;
        alturaMenagem = h + 0.2f + ChaoLocal(c, rot, centro) * 0f;
        m.Caixa(new Vector3(centro.x, (y0 + h) * 0.5f, centro.z), new Vector3(lado, h - y0, lado), PEDRA_BLOCOS);
        m.Caixa(new Vector3(centro.x, y0 + 1.5f, centro.z), new Vector3(lado + 0.6f, 3f, lado + 0.6f), PEDRA_ESCURA);
        m.Caixa(new Vector3(centro.x, h + 0.15f, centro.z), new Vector3(lado + 0.6f, 0.3f, lado + 0.6f), PEDRA_ESCURA);
        // topo: um lado com ameias, outro desabado
        Ameias(m, new Vector3(centro.x, h + 0.3f, centro.z + lado * 0.5f - 0.4f), Vector3.right, lado, 0.8f, true);
        Ameias(m, new Vector3(centro.x - lado * 0.5f + 0.4f, h + 0.3f, centro.z), Vector3.forward, lado, 0.8f, false);
        for (int k = 0; k < 5; k++)
            m.Caixa(new Vector3(centro.x + Faixa(0f, lado * 0.45f), h + Faixa(0.3f, 1.4f), centro.z + Faixa(-lado * 0.45f, 0f)), new Vector3(Faixa(1f, 2.5f), Faixa(0.5f, 2f), Faixa(1f, 2.5f)), PEDRA_BLOCOS);
        // porta em arco com degraus e janelas escuras
        Vector3 frenteP = centro + Vector3.forward * (lado * 0.5f + 0.03f);
        m.Painel(new Vector3(frenteP.x, 1.5f, frenteP.z), Vector3.forward, 1.7f, 3.2f, P_PORTA_ARCO);
        for (int k = 0; k < 3; k++)
            m.Caixa(new Vector3(centro.x, -0.12f - k * 0.25f, centro.z + lado * 0.5f + 0.3f + k * 0.4f), new Vector3(3f - k * 0.2f, 0.3f + k * 0.5f, 0.6f), PEDRA_ESCURA);
        Vector3[] faces = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        foreach (Vector3 f in faces)
        {
            Vector3 lat = Vector3.Cross(Vector3.up, f);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Painel(new Vector3(centro.x, 6.5f, centro.z) + f * (lado * 0.5f + 0.02f) + lat * s * 2.8f, f, 0.9f, 1.5f, P_ARCO_SINO);
                m.Painel(new Vector3(centro.x, 10.5f, centro.z) + f * (lado * 0.5f + 0.02f) + lat * s * 2.8f, f, 0.9f, 1.5f, P_ARCO_SINO);
            }
        }
    }

    static void Entulho(Malha m, Vector3 c, Quaternion rot, Vector3 p, float tam)
    {
        float y = ChaoLocal(c, rot, p);
        m.Caixa(new Vector3(p.x, y + tam * 0.25f, p.z), new Vector3(tam * Faixa(0.8f, 1.4f), tam * Faixa(0.5f, 0.9f), tam * Faixa(0.7f, 1.2f)),
                Quaternion.Euler(Faixa(-25f, 25f), Faixa(0f, 360f), Faixa(-25f, 25f)), rng.NextDouble() < 0.6 ? PEDRA_BLOCOS : PEDRA_RUSTICA);
    }

    // ==================================================================
    //  CEMITÉRIO
    // ==================================================================
    static void Cemiterio(Vector3 c, Vector3 frente)
    {
        Transform pasta = Pasta("Cemiterio");
        Quaternion rot = Quaternion.LookRotation(frente, Vector3.up);
        float W = 22f, D = 17f;
        Malha m = new Malha();

        // muro baixo com portão de ferro na frente
        Vector3[] cantos = { new Vector3(-W / 2, 0, D / 2), new Vector3(-W / 2, 0, -D / 2), new Vector3(W / 2, 0, -D / 2), new Vector3(W / 2, 0, D / 2) };
        for (int l = 0; l < 3; l++) MuroBaixo(m, c, rot, cantos[l], cantos[l + 1]);
        MuroBaixo(m, c, rot, cantos[3], new Vector3(1.4f, 0f, D / 2));
        MuroBaixo(m, c, rot, new Vector3(-1.4f, 0f, D / 2), cantos[0]);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 p = new Vector3(s * 1.6f, 0f, D / 2);
            float y0 = ChaoLocal(c, rot, p) - 0.4f;
            m.Caixa(new Vector3(p.x, (y0 + 2f) * 0.5f, p.z), new Vector3(0.7f, 2f - y0, 0.7f), PEDRA_RUSTICA);
            m.Caixa(new Vector3(p.x, 2.08f, p.z), new Vector3(0.85f, 0.16f, 0.85f), PEDRA_ESCURA);
            m.Piramide(new Vector3(p.x, 2.16f, p.z), 0.3f, 0.45f, PEDRA_ESCURA);
            // folha do portão (uma aberta, outra fechada)
            float ang = s < 0 ? 70f : 5f;
            Quaternion rg = Quaternion.Euler(0f, s * ang, 0f);
            Vector3 dobradica = new Vector3(s * 1.25f, 0f, D / 2);
            float yg = ChaoLocal(c, rot, dobradica) + 0.1f;
            for (int k = 0; k <= 5; k++)
            {
                Vector3 off = rg * new Vector3(-s * (k * 0.22f), 0f, 0f);
                m.Caixa(dobradica + off + Vector3.up * (yg + 0.85f), new Vector3(0.04f, 1.7f, 0.04f), FERRO);
            }
            m.Caixa(dobradica + rg * new Vector3(-s * 0.55f, 0f, 0f) + Vector3.up * (yg + 1.6f), new Vector3(1.15f, 0.06f, 0.05f), rg, FERRO);
            m.Caixa(dobradica + rg * new Vector3(-s * 0.55f, 0f, 0f) + Vector3.up * (yg + 0.2f), new Vector3(1.15f, 0.06f, 0.05f), rg, FERRO);
        }

        // lápides em fileiras, com um caminho no meio
        for (float z = D / 2 - 3.5f; z > -D / 2 + 5f; z -= 2.4f)
            for (float x = -W / 2 + 1.8f; x < W / 2 - 1.2f; x += 1.9f)
            {
                if (Mathf.Abs(x) < 1.6f) continue;
                if (rng.NextDouble() < 0.18) continue;
                Vector3 p = new Vector3(x + Faixa(-0.25f, 0.25f), 0f, z + Faixa(-0.2f, 0.2f));
                Lapide(m, p, ChaoLocal(c, rot, p), rng.Next(0, 4));
            }

        // mausoléu no fundo
        Mausoleu(m, new Vector3(0f, 0f, -D / 2 + 3.2f), ChaoLocal(c, rot, new Vector3(0f, 0f, -D / 2 + 3.2f)));
        Instanciar(m, "Cemiterio", c, rot, pasta, true);

        // árvores secas e um poste com lanterna no portão
        for (int k = 0; k < 4; k++)
        {
            Vector3 p = c + rot * new Vector3(k % 2 == 0 ? -W / 2 + 2f : W / 2 - 2f, 0f, k < 2 ? D / 2 - 2.5f : -D / 2 + 2.5f);
            ColocarArvoreSeca(pasta, p, Faixa(0.9f, 1.25f));
        }
        Poste(pasta, c + rot * new Vector3(2.4f, 0f, D / 2 + 1.2f), rot * Quaternion.Euler(0f, -90f, 0f));
    }

    static void MuroBaixo(Malha m, Vector3 c, Quaternion rot, Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; float comp = d.magnitude; d /= comp;
        int n = Mathf.Max(1, Mathf.RoundToInt(comp / 2.5f));
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = a + d * (comp * i / n), p1 = a + d * (comp * (i + 1) / n), meio = (p0 + p1) * 0.5f;
            float y0 = ChaoLocal(c, rot, meio) - 0.4f;
            float h = rng.NextDouble() < 0.15 ? Faixa(0.3f, 0.6f) : 0.95f;
            m.Caixa(new Vector3(meio.x, (y0 + h) * 0.5f, meio.z), new Vector3(0.55f, h - y0, comp / n + 0.02f), Quaternion.LookRotation(d), PEDRA_RUSTICA);
            if (h > 0.9f) m.Caixa(new Vector3(meio.x, h + 0.06f, meio.z), new Vector3(0.7f, 0.12f, comp / n + 0.04f), Quaternion.LookRotation(d), PEDRA_ESCURA);
        }
    }

    static void Lapide(Malha m, Vector3 p, float y, int tipo)
    {
        Quaternion r = Quaternion.Euler(Faixa(-9f, 9f), Faixa(-8f, 8f), Faixa(-7f, 7f));
        Vector3 b = new Vector3(p.x, y - 0.2f, p.z);
        switch (tipo)
        {
            case 0: // lápide arredondada
                m.Caixa(b + r * new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 1.1f, 0.15f), r, PEDRA_ESCURA);
                m.Cilindro(b + r * new Vector3(0f, 1.1f, -0.075f), 0.3f, 0.15f, 12, PEDRA_ESCURA, r * Quaternion.Euler(90f, 0f, 0f));
                break;
            case 1: // cruz de pedra
                m.Caixa(b + r * new Vector3(0f, 0.7f, 0f), new Vector3(0.16f, 1.4f, 0.14f), r, PEDRA_BLOCOS);
                m.Caixa(b + r * new Vector3(0f, 1.05f, 0f), new Vector3(0.65f, 0.15f, 0.14f), r, PEDRA_BLOCOS);
                break;
            case 2: // cruz de madeira velha
                m.Caixa(b + r * new Vector3(0f, 0.65f, 0f), new Vector3(0.1f, 1.3f, 0.08f), r, MAD_ESC);
                m.Caixa(b + r * new Vector3(0f, 1.0f, 0f), new Vector3(0.55f, 0.09f, 0.08f), r, MAD_ESC);
                break;
            default: // laje deitada sobre a cova
                m.Caixa(new Vector3(p.x, y + 0.05f, p.z - 0.6f), new Vector3(0.8f, 0.2f, 1.8f), Quaternion.Euler(Faixa(-3f, 3f), Faixa(-5f, 5f), Faixa(-3f, 3f)), PEDRA_RUSTICA);
                m.Caixa(b + r * new Vector3(0f, 0.45f, 0.3f), new Vector3(0.55f, 0.9f, 0.14f), r, PEDRA_ESCURA);
                break;
        }
    }

    static void Mausoleu(Malha m, Vector3 p, float y)
    {
        float W = 4.6f, D = 5.4f, H = 3.4f;
        Vector3 b = new Vector3(p.x, y, p.z);
        m.Caixa(b + new Vector3(0f, -0.2f, 0f), new Vector3(W + 0.6f, 0.8f, D + 0.6f), PEDRA_ESCURA);
        m.Caixa(b + new Vector3(0f, H * 0.5f + 0.2f, -0.4f), new Vector3(W, H, D - 0.8f), PEDRA_BLOCOS);
        for (int s = -1; s <= 1; s += 2)
            m.Cilindro(b + new Vector3(s * (W * 0.5f - 0.35f), 0.2f, D * 0.5f - 0.35f), 0.26f, H, 10, PEDRA_BLOCOS, Quaternion.identity);
        m.Caixa(b + new Vector3(0f, H + 0.32f, 0f), new Vector3(W + 0.3f, 0.24f, D + 0.3f), PEDRA_ESCURA);
        Telhado(m, b + new Vector3(0f, H + 0.44f, 0f), W + 0.3f, D + 0.3f, 24f, true, 0.2f, 0.15f, PEDRA_ESCURA, PEDRA_BLOCOS, -1, false, false);
        m.Painel(b + new Vector3(0f, 1.45f, D * 0.5f - 0.8f + 0.03f), Vector3.forward, 1.3f, 2.5f, P_PORTA_ARCO);
        for (int k = 0; k < 2; k++)
            m.Caixa(b + new Vector3(0f, 0.05f - k * 0.2f, D * 0.5f + 0.4f + k * 0.4f), new Vector3(2.2f, 0.3f, 0.45f), PEDRA_ESCURA);
    }

    // ==================================================================
    //  NATUREZA NO MAPA TODO
    // ==================================================================
    static Mesh[] malhaArvoreSeca = new Mesh[3]; static Material[][] matsArvoreSeca = new Material[3][];
    static Mesh[] malhaTronco = new Mesh[3]; static Material[][] matsTronco = new Material[3][];
    static Mesh[] malhaToco = new Mesh[2]; static Material[][] matsToco = new Material[2][];

    static void Natureza()
    {
        malhaArvoreSeca = new Mesh[3]; matsArvoreSeca = new Material[3][];
        malhaTronco = new Mesh[3]; matsTronco = new Material[3][];
        malhaToco = new Mesh[2]; matsToco = new Material[2][];
        Transform pasta = Pasta("Natureza");

        // --- bosques (árvores do próprio terreno, leves para o jogo)
        List<TreePrototype> protos = new List<TreePrototype>(dados.treePrototypes);
        int[] idxArv = new int[ARVORES_MUNDO.Length];
        for (int i = 0; i < ARVORES_MUNDO.Length; i++) idxArv[i] = Prototipo(protos, ARVORES_MUNDO[i]);
        int idxArb = Prototipo(protos, ARBUSTO_MUNDO);
        dados.treePrototypes = protos.ToArray();
        dados.RefreshPrototypes();

        Vector3 o = terreno.transform.position, s = dados.size;
        List<TreeInstance> lista = new List<TreeInstance>(dados.treeInstances);
        float semente = Faixa(0f, 500f);
        int novas = 0;
        for (float x = o.x + 30f; x < o.x + s.x - 30f; x += 7f)
            for (float z = o.z + 30f; z < o.z + s.z - 30f; z += 7f)
            {
                Vector3 p = new Vector3(x + Faixa(-3f, 3f), 0f, z + Faixa(-3f, 3f));
                float ruido = Mathf.PerlinNoise(p.x * 0.007f + semente, p.z * 0.007f + semente);
                if (ruido < 0.5f) continue;
                if (!LivreNatureza(p, 4f)) continue;
                if (Inclinacao(p.x, p.z) > 32f) continue;
                double chance = ruido > 0.6f ? 0.4 : 0.18;
                if (rng.NextDouble() > chance) continue;
                bool arbusto = ruido < 0.56f || rng.NextDouble() < 0.15;
                int idx = arbusto ? idxArb : idxArv[rng.Next(idxArv.Length)];
                float nx = (p.x - o.x) / s.x, nz = (p.z - o.z) / s.z;
                TreeInstance ti = new TreeInstance();
                ti.prototypeIndex = idx;
                ti.position = new Vector3(nx, dados.GetInterpolatedHeight(nx, nz) / s.y, nz);
                float esc = arbusto ? Faixa(0.7f, 1.3f) : Faixa(0.8f, 1.35f);
                ti.widthScale = esc; ti.heightScale = esc * Faixa(0.9f, 1.1f);
                ti.rotation = Faixa(0f, Mathf.PI * 2f);
                ti.color = new Color32(255, 255, 255, 255); ti.lightmapColor = new Color32(255, 255, 255, 255);
                lista.Add(ti); novas++;
            }
        dados.SetTreeInstances(lista.ToArray(), true);

        // --- grupos de pedras (objetos com colisão)
        Transform pastaPedras = NovaSubpasta(pasta, "Pedras");
        int grupos = 0;
        for (int tent = 0; tent < 600 && grupos < 80; tent++)
        {
            Vector3 p = new Vector3(o.x + Faixa(30f, s.x - 30f), 0f, o.z + Faixa(30f, s.z - 30f));
            if (!LivreNatureza(p, 6f)) continue;
            ColocarPrefabMundo(PEDRAS_MUNDO[rng.Next(PEDRAS_MUNDO.Length)], pastaPedras, p, Faixa(2f, 5.5f), false, 0.22f);
            int menores = rng.Next(1, 5);
            for (int k = 0; k < menores; k++)
            {
                Vector3 q = p + new Vector3(Faixa(-4f, 4f), 0f, Faixa(-4f, 4f));
                ColocarPrefabMundo(PEDRAS_MUNDO[rng.Next(PEDRAS_MUNDO.Length)], pastaPedras, q, Faixa(0.6f, 1.6f), false, 0.15f);
            }
            if (rng.NextDouble() < 0.5) ColocarPrefabMundo(PEDRINHAS_MUNDO, pastaPedras, p + new Vector3(Faixa(-3f, 3f), 0f, Faixa(-3f, 3f)), Faixa(1.5f, 3f), false, 0.05f);
            grupos++;
        }

        // --- troncos caídos, tocos e árvores secas
        Transform pastaMadeira = NovaSubpasta(pasta, "Troncos e Arvores Secas");
        int troncos = 0, tocos = 0, secas = 0;
        for (int tent = 0; tent < 900 && (troncos < 50 || tocos < 45 || secas < 30); tent++)
        {
            Vector3 p = new Vector3(o.x + Faixa(30f, s.x - 30f), 0f, o.z + Faixa(30f, s.z - 30f));
            if (!LivreNatureza(p, 4f) || Inclinacao(p.x, p.z) > 25f) continue;
            float ruido = Mathf.PerlinNoise(p.x * 0.007f + semente, p.z * 0.007f + semente);
            if (troncos < 50 && ruido > 0.45f) { ColocarTronco(pastaMadeira, p); troncos++; }
            else if (tocos < 45 && ruido > 0.4f) { ColocarToco(pastaMadeira, p); tocos++; }
            else if (secas < 30 && ruido < 0.45f) { ColocarArvoreSeca(pastaMadeira, p, Faixa(0.8f, 1.3f)); secas++; }
        }
        Debug.Log("[Pinglu] Natureza: " + novas + " árvores/arbustos, " + grupos + " grupos de pedras, " + troncos + " troncos, " + tocos + " tocos, " + secas + " árvores secas.");
    }

    static readonly string[] ARVORES_MUNDO = { "Assets/Holotna/Mountain/Prefabs/Tree01A.prefab", "Assets/Holotna/Mountain/Prefabs/Tree01B.prefab" };
    const string ARBUSTO_MUNDO = "Assets/Holotna/Mountain/Prefabs/Bush01.prefab";
    static readonly string[] PEDRAS_MUNDO = { "Assets/Holotna/Mountain/Prefabs/Rock01.prefab", "Assets/Holotna/Mountain/Prefabs/Rock02.prefab" };
    const string PEDRINHAS_MUNDO = "Assets/Holotna/Mountain/Prefabs/Pebbles01.prefab";

    static bool LivreNatureza(Vector3 p, float raio)
    {
        foreach (Zona z in zonas) if (Plano(p - z.pos).magnitude < z.raio + raio) return false;
        return DentroDoTerreno(p, 25f);
    }

    static int Prototipo(List<TreePrototype> protos, string caminho)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab == null) throw new System.Exception("Não achei " + caminho);
        for (int i = 0; i < protos.Count; i++) if (protos[i].prefab == prefab) return i;
        protos.Add(new TreePrototype { prefab = prefab });
        return protos.Count - 1;
    }

    static float Inclinacao(float x, float z)
    {
        float nx = Mathf.Clamp01((x - terreno.transform.position.x) / dados.size.x);
        float nz = Mathf.Clamp01((z - terreno.transform.position.z) / dados.size.z);
        return dados.GetSteepness(nx, nz);
    }

    static Transform NovaSubpasta(Transform pai, string nome)
    {
        GameObject g = new GameObject(nome);
        g.transform.SetParent(pai, false);
        return g.transform;
    }

    static void ColocarPrefabMundo(string caminho, Transform pai, Vector3 pos, float tamanho, bool usarAltura, float afundar)
    {
        if (!DentroDoTerreno(pos, 2f)) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab == null) { if (!avisos.Contains("Não achei " + caminho)) avisos.Add("Não achei " + caminho); return; }
        Vector3 nativo;
        if (!tamanhoPrefab.TryGetValue(caminho, out nativo))
        {
            GameObject tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.position = Vector3.zero;
            Bounds b;
            nativo = MedirBounds(tmp, out b) ? b.size : Vector3.one;
            Object.DestroyImmediate(tmp);
            tamanhoPrefab[caminho] = nativo;
        }
        float referencia = usarAltura ? nativo.y : Mathf.Max(nativo.x, nativo.z);
        float escala = referencia > 0.0001f ? tamanho / referencia : 1f;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.gameObject.scene);
        go.transform.SetParent(pai, true);
        Vector3 n = Normal(pos);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, n, 0.6f).normalized) *
                                Quaternion.Euler(Faixa(-10f, 10f), Faixa(0f, 360f), Faixa(-10f, 10f)) * prefab.transform.rotation;
        go.transform.localScale = prefab.transform.localScale * escala;
        go.transform.position = new Vector3(pos.x, Altura(pos.x, pos.z) - afundar * tamanho, pos.z);
        go.isStatic = true;
        // garante colisão
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            MeshCollider mc = mf.GetComponent<MeshCollider>();
            if (mc == null && mf.GetComponent<Collider>() == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            if (mc != null && mc.sharedMesh == null) mc.sharedMesh = mf.sharedMesh;
        }
    }

    static Vector3 Normal(Vector3 p)
    {
        float nx = Mathf.Clamp01((p.x - terreno.transform.position.x) / dados.size.x);
        float nz = Mathf.Clamp01((p.z - terreno.transform.position.z) / dados.size.z);
        return dados.GetInterpolatedNormal(nx, nz);
    }

    static void ColocarArvoreSeca(Transform pai, Vector3 p, float escala)
    {
        int v = rng.Next(0, 3);
        Malha m = null;
        if (malhaArvoreSeca[v] == null) { m = new Malha(); ArvoreSeca(m, v); }
        GameObject g = Instanciar(m, "Arvore Seca", NoChao(p) - Vector3.up * 0.2f, Quaternion.Euler(Faixa(-4f, 4f), Faixa(0f, 360f), Faixa(-4f, 4f)), pai, true, ref malhaArvoreSeca[v], ref matsArvoreSeca[v]);
        g.transform.localScale = Vector3.one * escala;
    }

    static void ColocarTronco(Transform pai, Vector3 p)
    {
        int v = rng.Next(0, 3);
        Malha m = null;
        if (malhaTronco[v] == null) { m = new Malha(); TroncoCaido(m, v); }
        Vector3 n = Normal(p);
        Quaternion r = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, Faixa(0f, 360f), 0f);
        Instanciar(m, "Tronco Caido", NoChao(p) - Vector3.up * 0.08f, r, pai, true, ref malhaTronco[v], ref matsTronco[v]);
    }

    static void ColocarToco(Transform pai, Vector3 p)
    {
        int v = rng.Next(0, 2);
        Malha m = null;
        if (malhaToco[v] == null) { m = new Malha(); Toco(m, v); }
        Instanciar(m, "Toco", NoChao(p) - Vector3.up * 0.1f, Quaternion.Euler(0f, Faixa(0f, 360f), 0f), pai, true, ref malhaToco[v], ref matsToco[v]);
    }

    // galhos retorcidos, sem folhas
    static void ArvoreSeca(Malha m, int variante)
    {
        System.Random r = new System.Random(100 + variante);
        System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        Vector3 p = Vector3.zero;
        Vector3 dir = Vector3.up;
        float raio = 0.3f;
        int segs = 5;
        float alturaSeg = F(1.1f, 1.5f);
        List<Vector3> pontos = new List<Vector3>();
        for (int i = 0; i < segs; i++)
        {
            Vector3 nd = (dir + new Vector3(F(-0.25f, 0.25f), 0f, F(-0.25f, 0.25f))).normalized;
            float r1 = raio * (1f - (i + 1f) / (segs + 1.5f));
            m.Cilindro(p, Mathf.Lerp(raio, r1, 0.5f) * 1.05f, alturaSeg * 1.04f, 7, MAD_ESC, Quaternion.FromToRotation(Vector3.up, nd));
            p += nd * alturaSeg; dir = nd; raio = r1;
            pontos.Add(p);
        }
        // raízes
        for (int k = 0; k < 4; k++)
        {
            Vector3 d = Quaternion.Euler(0f, k * 90f + F(-20f, 20f), 0f) * Vector3.forward;
            m.Cilindro(Vector3.up * 0.3f, 0.12f, 0.9f, 6, MAD_ESC, Quaternion.FromToRotation(Vector3.up, (d - Vector3.up * 0.6f).normalized));
        }
        // galhos a partir do meio para cima
        for (int i = 1; i < pontos.Count; i++)
        {
            int n = i == pontos.Count - 1 ? 3 : 2;
            for (int k = 0; k < n; k++)
            {
                Vector3 d = (Quaternion.Euler(0f, F(0f, 360f), 0f) * Vector3.forward * F(0.7f, 1.2f) + Vector3.up * F(0.4f, 1.1f)).normalized;
                float comp = F(1.0f, 2.2f) * (1.2f - i * 0.12f);
                float rg = Mathf.Max(0.04f, 0.12f - i * 0.015f);
                m.Cilindro(pontos[i] - dir * 0.2f, rg, comp, 5, MAD_ESC, Quaternion.FromToRotation(Vector3.up, d));
                Vector3 ponta = pontos[i] - dir * 0.2f + d * comp;
                Vector3 d2 = (d + new Vector3(F(-0.6f, 0.6f), F(-0.2f, 0.4f), F(-0.6f, 0.6f))).normalized;
                m.Cilindro(ponta, rg * 0.6f, comp * 0.6f, 4, MAD_ESC, Quaternion.FromToRotation(Vector3.up, d2));
            }
        }
    }

    static void TroncoCaido(Malha m, int variante)
    {
        float comp = 4f + variante * 1.3f, raio = 0.3f + variante * 0.06f;
        m.Cilindro(new Vector3(-comp * 0.5f, raio * 0.85f, 0f), raio, comp, 9, variante == 1 ? MAD_VERM : MAD_ESC, Quaternion.Euler(0f, 0f, -90f));
        m.Cilindro(new Vector3(-comp * 0.25f, raio * 1.5f, 0f), 0.07f, 0.9f, 5, MAD_ESC, Quaternion.FromToRotation(Vector3.up, new Vector3(0.3f, 1f, 0.4f).normalized));
        m.Cilindro(new Vector3(comp * 0.15f, raio * 1.2f, 0f), 0.06f, 0.7f, 5, MAD_ESC, Quaternion.FromToRotation(Vector3.up, new Vector3(-0.2f, 0.6f, -0.8f).normalized));
        // raízes numa das pontas
        for (int k = 0; k < 5; k++)
        {
            Vector3 d = Quaternion.Euler(k * 72f, 0f, 0f) * Vector3.up;
            m.Cilindro(new Vector3(-comp * 0.5f, raio * 0.85f, 0f), 0.09f, raio + 0.5f, 5, MAD_ESC, Quaternion.FromToRotation(Vector3.up, (d + Vector3.left * 0.6f).normalized));
        }
    }

    static void Toco(Malha m, int variante)
    {
        float raio = 0.38f + variante * 0.12f, alt = 0.5f + variante * 0.25f;
        m.Cilindro(Vector3.zero, raio, alt, 9, MAD_VERM, Quaternion.identity);
        for (int k = 0; k < 4; k++)
        {
            Vector3 d = Quaternion.Euler(0f, k * 90f + 30f, 0f) * Vector3.forward;
            m.Cilindro(Vector3.up * 0.2f, 0.1f, raio + 0.4f, 5, MAD_VERM, Quaternion.FromToRotation(Vector3.up, (d - Vector3.up * 0.5f).normalized));
        }
        if (variante == 1)
        {
            m.Cilindro(new Vector3(0f, alt, 0f), 0.02f, 0.7f, 5, MAD_ESC, Quaternion.Euler(25f, 0f, 10f));
            m.Caixa(new Vector3(0.12f, alt + 0.55f, 0.28f), new Vector3(0.05f, 0.14f, 0.22f), Quaternion.Euler(25f, 0f, 10f), FERRO);
        }
    }
}
#endif
