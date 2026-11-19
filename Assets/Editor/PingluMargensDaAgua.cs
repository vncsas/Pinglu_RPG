using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 80. Margens e fundo dos lagos
//  Antes a água era só uma "tinta" colada em cima do terreno: dava para ver o mundo inteiro (grama, árvores, pedras)
//  por baixo dela e a beira não tinha forma. Este menu faz de cada lago uma BACIA de verdade:
//   - cava o fundo (raso na beira, cada vez mais fundo para o meio), sem mexer nas colinas em volta;
//   - levanta uma MARGEM suave em volta (um barranco baixo, como no lago central) para a água ficar "dentro" de algo;
//   - pinta o fundo e a beira de lama, tira a grama e as árvores que estavam debaixo d'água e esconde objetos afogados;
//   - planta juncos na beira e deixa a água mais escura para o fundo não aparecer.
//  Roda em todas as águas da cena (o lago central e o pequeno). Guarda cópia da cena e do terreno em Backups. Pode rodar de novo.
// ==========================================================================================
public static class PingluMargensDaAgua
{
    const float CEL = 1f;                    // grade de trabalho (m)
    const float FOLGA = 36f;                 // quanto além da água a bacia/margem alcança
    const float RIM_ALTURA = 0.5f;           // altura do barranco da margem
    const float RIM_DIST = 3f;               // a que distância da água fica o topo do barranco

    class Agua { public Renderer r; public float nivel; public Bounds b; }

    [MenuItem("Pinglu/80. Margens e fundo dos lagos (bordas em volta da água)")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        Terrain terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o terreno na cena aberta.", "OK"); return; }
        List<Agua> aguas = AcharAguas();
        if (aguas.Count == 0) { EditorUtility.DisplayDialog("Pinglu", "Não achei nenhuma água na cena (abra a SampleScene_Ambientacao).", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou fazer as bordas de " + aguas.Count + " superfície(s) de água:\n\n• cavar o fundo e criar uma margem em volta\n• pintar o fundo de lama\n• tirar grama, árvores e objetos que ficam debaixo d'água\n• plantar juncos na beira\n\nIsso mexe no terreno só em volta dos lagos. Guardo uma cópia da cena e do terreno em Backups.", "Continuar", "Cancelar")) return;

        var cena = EditorSceneManager.GetActiveScene();
        TerrainData td = terreno.terrainData;
        Directory.CreateDirectory("Backups");
        if (!string.IsNullOrEmpty(cena.path) && File.Exists(cena.path)) { string c = "Backups/" + Path.GetFileNameWithoutExtension(cena.path) + "_antes_das_margens.unity"; if (!File.Exists(c)) File.Copy(cena.path, c, false); }
        string pt = AssetDatabase.GetAssetPath(td);
        if (!string.IsNullOrEmpty(pt) && File.Exists(pt)) { string c = "Backups/" + Path.GetFileNameWithoutExtension(pt) + "_antes_das_margens.asset"; if (!File.Exists(c)) File.Copy(pt, c, false); }

        var log = new StringBuilder();
        int bacias = 0, arvores = 0, escondidos = 0, juncos = 0;
        try
        {
            Undo.IncrementCurrentGroup(); int grupo = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(td, "Margens da água");
            GameObject pasta = GameObject.Find("Margens da Agua"); if (pasta != null) Object.DestroyImmediate(pasta);
            pasta = new GameObject("Margens da Agua"); Undo.RegisterCreatedObjectUndo(pasta, "Margens da água");

            foreach (Agua a in aguas)
            {
                EditorUtility.DisplayProgressBar("Margens", "Bacia de \"" + a.r.name + "\"...", 0.2f);
                int ar = 0, es = 0, ju = 0;
                if (Margear(terreno, a, pasta.transform, log, out ar, out es, out ju)) bacias++;
                arvores += ar; escondidos += es; juncos += ju;
                AguaMaisEscura(a.r.sharedMaterial);
            }
            terreno.Flush();
            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(grupo);
            EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();

            string msg = "Pronto! " + bacias + " lago(s) com bacia e margem.\n\n• árvores tiradas de debaixo d'água: " + arvores + "\n• objetos afogados escondidos: " + escondidos + "\n• juncos plantados na beira: " + juncos + "\n\nDê Play e confira a beira dos lagos. Se quiser a margem mais alta ou o fundo mais fundo, me diga.";
            if (log.Length > 0) msg += "\n\nAvisos:\n" + log;
            Debug.Log("[Margens]\n" + msg);
            EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ------------------------------------------------------------------ águas da cena
    static List<Agua> AcharAguas()
    {
        var lista = new List<Agua>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader == null) continue;
            string sn = r.sharedMaterial.shader.name;
            bool agua = sn.StartsWith("Pinglu/Agua") || (r.name == "Agua" && r.transform.parent != null && r.transform.parent.name.StartsWith("Lago"));
            if (agua && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null) lista.Add(new Agua { r = r, nivel = r.bounds.center.y, b = r.bounds });
        }
        return lista;
    }

    static void AguaMaisEscura(Material m)
    {
        if (m == null) return;
        if (m.HasProperty("_DeepColor")) { Color c = m.GetColor("_DeepColor"); c.a = 0.998f; m.SetColor("_DeepColor", c); }
        if (m.HasProperty("_ShallowColor")) { Color c = m.GetColor("_ShallowColor"); c.a = Mathf.Max(c.a, 0.9f); m.SetColor("_ShallowColor", c); }
        if (m.HasProperty("_DepthDistance")) m.SetFloat("_DepthDistance", 1.2f);
        EditorUtility.SetDirty(m);
    }

    // ------------------------------------------------------------------ uma água
    static bool Margear(Terrain terreno, Agua a, Transform pasta, StringBuilder log, out int arvoresTiradas, out int escondidos, out int juncos)
    {
        arvoresTiradas = escondidos = juncos = 0;
        TerrainData td = terreno.terrainData;
        Vector3 pos0 = terreno.transform.position, tam = td.size;
        Mesh mesh = a.r.GetComponent<MeshFilter>().sharedMesh;
        Transform tr = a.r.transform;

        // 1) máscara da água (a malha tem 4 vértices por célula)
        Bounds b = a.b; b.Expand(new Vector3(FOLGA * 2f, 0f, FOLGA * 2f));
        float x0 = Mathf.Max(pos0.x + 2f, b.min.x), z0 = Mathf.Max(pos0.z + 2f, b.min.z);
        float x1 = Mathf.Min(pos0.x + tam.x - 2f, b.max.x), z1 = Mathf.Min(pos0.z + tam.z - 2f, b.max.z);
        int nx = Mathf.CeilToInt((x1 - x0) / CEL), nz = Mathf.CeilToInt((z1 - z0) / CEL);
        if (nx < 8 || nz < 8 || nx * nz > 2000000) { log.AppendLine("- \"" + a.r.name + "\": tamanho estranho, ignorei."); return false; }
        bool[,] agua = new bool[nx, nz]; int celulas = 0;
        Vector3[] vs = mesh.vertices;
        for (int v = 0; v + 3 < vs.Length; v += 4)
        {
            Vector3 w0 = tr.TransformPoint(vs[v]), w2 = tr.TransformPoint(vs[v + 2]);
            Vector3 c = (w0 + w2) * 0.5f;
            int i = Mathf.FloorToInt((c.x - x0) / CEL), j = Mathf.FloorToInt((c.z - z0) / CEL);
            if (i >= 0 && j >= 0 && i < nx && j < nz && !agua[i, j]) { agua[i, j] = true; celulas++; }
        }
        if (celulas == 0) return false;

        // 2) distâncias: dentro (da beira para o meio) e fora (da água para fora)
        float[,] dDentro = Distancia(agua, nx, nz, true);
        float[,] dFora = Distancia(agua, nx, nz, false);
        float nivel = a.nivel;

        // 3) terreno: bacia + margem
        int res = td.heightmapResolution; float sx = tam.x / (res - 1), sz = tam.z / (res - 1);
        int i0 = Mathf.Max(0, Mathf.FloorToInt((x0 - pos0.x) / sx)), j0 = Mathf.Max(0, Mathf.FloorToInt((z0 - pos0.z) / sz));
        int i1 = Mathf.Min(res - 1, Mathf.CeilToInt((x1 - pos0.x) / sx)), j1 = Mathf.Min(res - 1, Mathf.CeilToInt((z1 - pos0.z) / sz));
        int w = i1 - i0 + 1, h = j1 - j0 + 1;
        float[,] hm = td.GetHeights(i0, j0, w, h);
        float[,] novo = (float[,])hm.Clone();
        float[,] banda = new float[h, w];
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            float wx = pos0.x + (i0 + i) * sx, wz = pos0.z + (j0 + j) * sz;
            int ci = Mathf.Clamp(Mathf.FloorToInt((wx - x0) / CEL), 0, nx - 1), cj = Mathf.Clamp(Mathf.FloorToInt((wz - z0) / CEL), 0, nz - 1);
            float atual = hm[j, i] * tam.y + pos0.y, alvo = atual;
            if (agua[ci, cj])
            {
                float d = dDentro[ci, cj];
                float prof = 0.25f + 2.4f * Mathf.SmoothStep(0f, 1f, d / 12f);            // raso na beira, fundo no meio
                alvo = Mathf.Min(atual, nivel - prof);
                banda[j, i] = 1f;
            }
            else
            {
                float d = dFora[ci, cj];
                if (d < 16f)
                {
                    float g = (d - RIM_DIST) / 2.6f;
                    float rim = nivel + RIM_ALTURA * Mathf.Exp(-g * g);                   // barranco baixo e arredondado
                    if (atual < nivel + 1.4f && atual < rim) alvo = Mathf.Lerp(atual, rim, Mathf.Clamp01(1f - (d - 10f) / 6f));
                    banda[j, i] = 1f;
                }
            }
            novo[j, i] = Mathf.Clamp01((alvo - pos0.y) / tam.y);
        }
        // suaviza a faixa mexida (2 passadas), sem deixar o fundo subir acima da água
        for (int passo = 0; passo < 2; passo++)
        {
            float[,] cp = (float[,])novo.Clone();
            for (int j = 1; j < h - 1; j++) for (int i = 1; i < w - 1; i++)
            {
                if (banda[j, i] < 0.5f) continue;
                float s = 0f; for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++) s += cp[j + dj, i + di];
                novo[j, i] = Mathf.Lerp(cp[j, i], s / 9f, 0.7f);
            }
        }
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            float wx = pos0.x + (i0 + i) * sx, wz = pos0.z + (j0 + j) * sz;
            int ci = Mathf.Clamp(Mathf.FloorToInt((wx - x0) / CEL), 0, nx - 1), cj = Mathf.Clamp(Mathf.FloorToInt((wz - z0) / CEL), 0, nz - 1);
            if (agua[ci, cj] && dDentro[ci, cj] > 1.5f) novo[j, i] = Mathf.Min(novo[j, i], Mathf.Clamp01((nivel - 0.3f - pos0.y) / tam.y));
        }
        td.SetHeights(i0, j0, novo);

        // 4) pinta o fundo e a beira de lama
        PintarLama(td, pos0, tam, x0, z0, nx, nz, dDentro, dFora, agua, log);
        // 5) tira grama debaixo d'água
        TirarGrama(td, pos0, tam, x0, z0, nx, nz, agua, dFora);
        // 6) árvores do terreno dentro da água (e na beira rente)
        arvoresTiradas = TirarArvores(td, pos0, tam, x0, z0, nx, nz, agua, dFora);
        td.SetTreeInstances(td.treeInstances, true);
        // 7) objetos afogados
        escondidos = EsconderAfogados(x0, z0, nx, nz, agua, nivel);
        // 8) juncos na beira
        juncos = Juncos(terreno, pasta, x0, z0, nx, nz, agua, dFora, dDentro, nivel, log);
        log.AppendLine("- \"" + a.r.name + "\": " + celulas + " m² de água, bacia e margem feitas.");
        return true;
    }

    // distância (em m) de cada célula até a borda: dentro=true -> só células de água (distância até a terra); false -> só terra (distância até a água)
    static float[,] Distancia(bool[,] agua, int nx, int nz, bool dentro)
    {
        float[,] d = new float[nx, nz]; const float INF = 1e6f;
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) d[i, j] = (agua[i, j] == dentro) ? INF : 0f;
        const float D = 1.4142f;
        for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++)
        {
            float v = d[i, j]; if (v == 0f) continue;
            if (i > 0) v = Mathf.Min(v, d[i - 1, j] + 1f); if (j > 0) v = Mathf.Min(v, d[i, j - 1] + 1f);
            if (i > 0 && j > 0) v = Mathf.Min(v, d[i - 1, j - 1] + D); if (i < nx - 1 && j > 0) v = Mathf.Min(v, d[i + 1, j - 1] + D);
            d[i, j] = v;
        }
        for (int j = nz - 1; j >= 0; j--) for (int i = nx - 1; i >= 0; i--)
        {
            float v = d[i, j]; if (v == 0f) continue;
            if (i < nx - 1) v = Mathf.Min(v, d[i + 1, j] + 1f); if (j < nz - 1) v = Mathf.Min(v, d[i, j + 1] + 1f);
            if (i < nx - 1 && j < nz - 1) v = Mathf.Min(v, d[i + 1, j + 1] + D); if (i > 0 && j < nz - 1) v = Mathf.Min(v, d[i - 1, j + 1] + D);
            d[i, j] = v;
        }
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) if (d[i, j] >= INF * 0.5f) d[i, j] = 999f;
        return d;
    }

    static void PintarLama(TerrainData td, Vector3 pos0, Vector3 tam, float x0, float z0, int nx, int nz, float[,] dDentro, float[,] dFora, bool[,] agua, StringBuilder log)
    {
        TerrainLayer[] camadas = td.terrainLayers; int lama = -1;
        string[] chaves = { "lama", "barro", "mud", "muddy", "pantano", "swamp", "black_sand", "terramorta" };
        foreach (string k in chaves) { for (int i = 0; i < camadas.Length; i++) if (camadas[i] != null && camadas[i].name.ToLowerInvariant().Contains(k)) { lama = i; break; } if (lama >= 0) break; }
        if (lama < 0) { log.AppendLine("- não achei uma camada de lama/barro no terreno; o fundo ficou com a textura de antes."); return; }
        int aw = td.alphamapWidth, ah = td.alphamapHeight, nl = td.alphamapLayers;
        int ai0 = Mathf.Max(0, Mathf.FloorToInt((x0 - pos0.x) / tam.x * (aw - 1))), aj0 = Mathf.Max(0, Mathf.FloorToInt((z0 - pos0.z) / tam.z * (ah - 1)));
        int ai1 = Mathf.Min(aw - 1, Mathf.CeilToInt((x0 + nx * CEL - pos0.x) / tam.x * (aw - 1))), aj1 = Mathf.Min(ah - 1, Mathf.CeilToInt((z0 + nz * CEL - pos0.z) / tam.z * (ah - 1)));
        int w = ai1 - ai0 + 1, h = aj1 - aj0 + 1; if (w < 2 || h < 2) return;
        float[,,] al = td.GetAlphamaps(ai0, aj0, w, h);
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            float wx = pos0.x + (ai0 + i) / (float)(aw - 1) * tam.x, wz = pos0.z + (aj0 + j) / (float)(ah - 1) * tam.z;
            int ci = Mathf.Clamp(Mathf.FloorToInt((wx - x0) / CEL), 0, nx - 1), cj = Mathf.Clamp(Mathf.FloorToInt((wz - z0) / CEL), 0, nz - 1);
            float d = agua[ci, cj] ? dDentro[ci, cj] : -dFora[ci, cj];
            float peso = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2.2f, 0.8f, d));      // lama até ~2 m para fora da água
            if (peso <= 0.001f) continue;
            float soma = 0f; for (int l = 0; l < nl; l++) { if (l != lama) al[j, i, l] *= (1f - peso); soma += al[j, i, l]; }
            al[j, i, lama] = Mathf.Lerp(al[j, i, lama], 1f, peso);
            float total = 0f; for (int l = 0; l < nl; l++) total += al[j, i, l];
            if (total > 0.0001f) for (int l = 0; l < nl; l++) al[j, i, l] /= total;
        }
        td.SetAlphamaps(ai0, aj0, al);
    }

    static void TirarGrama(TerrainData td, Vector3 pos0, Vector3 tam, float x0, float z0, int nx, int nz, bool[,] agua, float[,] dFora)
    {
        int dw = td.detailWidth, dh = td.detailHeight; if (dw < 2 || dh < 2) return;
        int di0 = Mathf.Max(0, Mathf.FloorToInt((x0 - pos0.x) / tam.x * dw)), dj0 = Mathf.Max(0, Mathf.FloorToInt((z0 - pos0.z) / tam.z * dh));
        int di1 = Mathf.Min(dw - 1, Mathf.CeilToInt((x0 + nx * CEL - pos0.x) / tam.x * dw)), dj1 = Mathf.Min(dh - 1, Mathf.CeilToInt((z0 + nz * CEL - pos0.z) / tam.z * dh));
        int w = di1 - di0 + 1, h = dj1 - dj0 + 1; if (w < 1 || h < 1) return;
        for (int camada = 0; camada < td.detailPrototypes.Length; camada++)
        {
            int[,] m = td.GetDetailLayer(di0, dj0, w, h, camada); bool mexeu = false;
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
            {
                if (m[j, i] == 0) continue;
                float wx = pos0.x + (di0 + i + 0.5f) / dw * tam.x, wz = pos0.z + (dj0 + j + 0.5f) / dh * tam.z;
                int ci = Mathf.Clamp(Mathf.FloorToInt((wx - x0) / CEL), 0, nx - 1), cj = Mathf.Clamp(Mathf.FloorToInt((wz - z0) / CEL), 0, nz - 1);
                if (agua[ci, cj] || dFora[ci, cj] < 1.2f) { m[j, i] = 0; mexeu = true; }
            }
            if (mexeu) td.SetDetailLayer(di0, dj0, camada, m);
        }
    }

    static int TirarArvores(TerrainData td, Vector3 pos0, Vector3 tam, float x0, float z0, int nx, int nz, bool[,] agua, float[,] dFora)
    {
        TreeInstance[] todas = td.treeInstances; var ficam = new List<TreeInstance>(todas.Length); int tirou = 0;
        foreach (TreeInstance t in todas)
        {
            float wx = pos0.x + t.position.x * tam.x, wz = pos0.z + t.position.z * tam.z;
            int ci = Mathf.FloorToInt((wx - x0) / CEL), cj = Mathf.FloorToInt((wz - z0) / CEL);
            if (ci >= 0 && cj >= 0 && ci < nx && cj < nz && (agua[ci, cj] || dFora[ci, cj] < 1.5f)) { tirou++; continue; }
            ficam.Add(t);
        }
        if (tirou > 0) td.SetTreeInstances(ficam.ToArray(), true);
        return tirou;
    }

    // objetos inteiros debaixo da superfície (troncos, pedras, plantas afogadas...) saem de cena (ficam desativados, dá para voltar)
    static int EsconderAfogados(float x0, float z0, int nx, int nz, bool[,] agua, float nivel)
    {
        string[] poupar = { "Vitoria", "Junco", "Agua", "Margens", "Lago", "Chefe", "Angler", "Monstro", "Animais" };
        int n = 0; var feitos = new HashSet<GameObject>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r.GetComponent<Terrain>() != null) continue;
            Bounds b = r.bounds; if (b.center.y < -500f || b.size.x > 25f || b.size.z > 25f) continue;
            int ci = Mathf.FloorToInt((b.center.x - x0) / CEL), cj = Mathf.FloorToInt((b.center.z - z0) / CEL);
            if (ci < 0 || cj < 0 || ci >= nx || cj >= nz || !agua[ci, cj]) continue;
            if (b.max.y > nivel - 0.05f) continue;                                     // tem parte para fora da água: fica
            bool poupa = false; for (Transform t = r.transform; t != null; t = t.parent) { foreach (string p in poupar) if (t.name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) poupa = true; if (t.GetComponent<Vida>() != null || t.GetComponent<PlayerLocomotion>() != null) poupa = true; }
            if (poupa) continue;
            GameObject topo = r.gameObject;
            while (topo.transform.parent != null && topo.transform.parent.childCount <= 1 && topo.transform.parent.GetComponent<Terrain>() == null) topo = topo.transform.parent.gameObject;
            if (!feitos.Add(topo)) continue;
            Undo.RecordObject(topo, "Esconder afogado"); topo.SetActive(false); n++;
        }
        return n;
    }

    // juncos ao longo da beira (os modelos do lago do colega)
    static int Juncos(Terrain terreno, Transform pasta, float x0, float z0, int nx, int nz, bool[,] agua, float[,] dFora, float[,] dDentro, float nivel, StringBuilder log)
    {
        string[] nomes = { "Junco_A", "Junco_B" }; var prefabs = new List<GameObject>();
        foreach (string nm in nomes) foreach (string g in AssetDatabase.FindAssets(nm + " t:Model")) { string p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == nm) { prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(p)); break; } }
        if (prefabs.Count == 0) { log.AppendLine("- não achei os modelos de junco do lago; pulei os juncos."); return 0; }
        Material mat = null; foreach (string g in AssetDatabase.FindAssets("Lago_Junco t:Material")) { mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); break; }
        var rnd = new System.Random(80); int n = 0; float baseY = terreno.transform.position.y;
        GameObject grupo = new GameObject("Juncos"); grupo.transform.SetParent(pasta);
        for (int i = 1; i < nx - 1; i++) for (int j = 1; j < nz - 1; j++)
        {
            bool beira = agua[i, j] ? dDentro[i, j] <= 1.6f : dFora[i, j] <= 1.6f;
            if (!beira || rnd.NextDouble() > 0.07) continue;
            float x = x0 + (i + (float)rnd.NextDouble()) * CEL, z = z0 + (j + (float)rnd.NextDouble()) * CEL;
            float y = terreno.SampleHeight(new Vector3(x, 0f, z)) + baseY;
            if (y > nivel + 1.2f || y < nivel - 0.9f) continue;
            GameObject g2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rnd.Next(prefabs.Count)], grupo.transform);
            g2.transform.SetPositionAndRotation(new Vector3(x, y - 0.05f, z), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f));
            g2.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.8f, (float)rnd.NextDouble());
            if (mat != null) foreach (Renderer r in g2.GetComponentsInChildren<Renderer>()) { var ms = r.sharedMaterials; for (int m = 0; m < ms.Length; m++) ms[m] = mat; r.sharedMaterials = ms; }
            n++; if (n >= 900) return n;
        }
        return n;
    }
}
