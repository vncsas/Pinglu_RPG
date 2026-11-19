#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 81. Chão definitivo (castelo e objetos do mapa inteiro)
//  O 79 media as peças pela "caixa" delas e deixava o castelo quase de fora. Este mede a GEOMETRIA de verdade
//  (cada ponto da base da peça contra a altura do terreno naquele ponto):
//   1) CASTELO: se ele inteiro está no ar, desce de uma vez (rígido: muralhas, torres, props, luzes e efeitos juntos);
//      o que ainda sobrar vazio embaixo é preenchido levantando o terreno por baixo, com um talude suave (portões e arcos não são tapados);
//   2) OBJETOS (casas, ruínas, troncos, pedras, estacas, barricadas, props do castelo...): se o ponto mais baixo não encosta
//      em nada, desce até encostar; se está todo debaixo da terra, sobe. Quem está apoiado em outra peça (piso, degrau) fica;
//   3) ÁRVORES do terreno são encaixadas de novo;
//   4) CONFERÊNCIA: no fim ele mede tudo de novo e lista o que ainda ficou no ar.
//  O relevo (morros) não é achatado. Guarda cópia da cena e do terreno em Backups. Dá para desfazer com Ctrl+Z. Pode rodar de novo.
// ==========================================================================================
public static class PingluChaoCastelo
{
    static readonly string[] IGNORAR = { "Vitoria", "Lustre", "Tocha", "Lampada", "Lanterna", "Estandarte", "Bandeira", "Corda", "Nevoa", "Horda", "Clima", "Lobby", "Animais Novos", "Monstros do Pantano", "Encontro", "Luz", "Fogo", "Fumaca", "Vagalume", "Portal", "Efeito", "Particula", "Sala de Espera", "Menu Inicial", "Cenario do Lobby", "Mapa do Mundo", "Mini Mapa", "Camera", "Cutscene" };

    static Terrain ter; static float baseY;
    static Vector3 corC, corDir; static bool temCorredor;
    // passagem livre: faixa de 18 m de largura que vai do centro do castelo, passa pela entrada e segue 45 m para fora
    static bool NoCorredor(float x, float z, float folga)
    {
        if (!temCorredor) return false;
        Vector2 rel = new Vector2(x - corC.x, z - corC.z), dir = new Vector2(corDir.x, corDir.z), perp = new Vector2(-dir.y, dir.x);
        float ao = Vector2.Dot(rel, dir), la = Mathf.Abs(Vector2.Dot(rel, perp));
        return ao > -10f && ao < corAte + folga && la < 9f + folga;
    }
    static float corAte = 60f;
    static float Chao(float x, float z) { return ter.SampleHeight(new Vector3(x, 0f, z)) + baseY; }
    static long Chave(int ix, int iz) { return ((long)(ix + 100000) << 24) | (long)(iz + 100000); }

    static bool Contem(string nome, string[] lista) { foreach (string p in lista) if (nome.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }

    static List<MeshFilter> Malhas(Transform t)
    {
        var l = new List<MeshFilter>();
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(false))
        {
            if (mf.sharedMesh == null) continue;
            Renderer r = mf.GetComponent<Renderer>(); if (r == null || !r.enabled) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.StartsWith("Pinglu/Agua")) continue;
            l.Add(mf);
        }
        return l;
    }

    // "chão da peça": para cada quadradinho (cel metros) no plano, o ponto mais baixo da malha ali.
    // triangulos = true rasteriza as faces inteiras (castelo); false usa só os vértices (objetos pequenos).
    static Dictionary<long, float> BaseDe(List<MeshFilter> malhas, float cel, bool triangulos, out Bounds caixa)
    {
        var baseMin = new Dictionary<long, float>(); Bounds cx = new Bounds(); bool primeiro = true;
        void Ponto(Vector3 w)
        {
            long k = Chave(Mathf.FloorToInt(w.x / cel), Mathf.FloorToInt(w.z / cel));
            float v; if (!baseMin.TryGetValue(k, out v) || w.y < v) baseMin[k] = w.y;
            if (primeiro) { cx = new Bounds(w, Vector3.zero); primeiro = false; } else cx.Encapsulate(w);
        }
        foreach (MeshFilter mf in malhas)
        {
            Mesh m = mf.sharedMesh; Matrix4x4 M = mf.transform.localToWorldMatrix;
            Vector3[] vs; try { vs = m.vertices; } catch { continue; }
            if (!triangulos) { for (int i = 0; i < vs.Length; i++) Ponto(M.MultiplyPoint3x4(vs[i])); continue; }
            var w = new Vector3[vs.Length]; for (int i = 0; i < vs.Length; i++) w[i] = M.MultiplyPoint3x4(vs[i]);
            for (int sm = 0; sm < m.subMeshCount; sm++)
            {
                int[] tr = m.GetTriangles(sm);
                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    Vector3 a = w[tr[i]], b = w[tr[i + 1]], c = w[tr[i + 2]];
                    Ponto(a); Ponto(b); Ponto(c);
                    float step = cel * 0.5f;
                    Amostrar(a, b, step, Ponto); Amostrar(b, c, step, Ponto); Amostrar(c, a, step, Ponto);
                    // interior da face (só quando ela tem área vista de cima)
                    float area = Mathf.Abs((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) * 0.5f;
                    if (area > cel * cel * 0.25f)
                    {
                        int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(area) / step), 2, 40);
                        for (int p = 1; p < n; p++) for (int q = 1; q < n - p; q++)
                        {
                            float u = p / (float)n, v2 = q / (float)n;
                            Ponto(a + (b - a) * u + (c - a) * v2);
                        }
                    }
                }
            }
        }
        caixa = cx;
        return baseMin;
    }
    static void Amostrar(Vector3 a, Vector3 b, float step, System.Action<Vector3> f)
    {
        float d = new Vector2(b.x - a.x, b.z - a.z).magnitude; int n = Mathf.Min(200, Mathf.CeilToInt(d / step));
        for (int i = 1; i < n; i++) f(Vector3.Lerp(a, b, i / (float)n));
    }

    // folga de cada ponto da base: >0 no ar, <0 dentro da terra
    static void Folgas(Dictionary<long, float> baseMin, float cel, out float minimo, out float maximo, out float media)
    {
        minimo = float.MaxValue; maximo = float.MinValue; media = 0f; int n = 0;
        foreach (var kv in baseMin)
        {
            int ix = (int)(kv.Key >> 24) - 100000, iz = (int)(kv.Key & 0xFFFFFF) - 100000;
            float g = kv.Value - Chao((ix + 0.5f) * cel, (iz + 0.5f) * cel);
            minimo = Mathf.Min(minimo, g); maximo = Mathf.Max(maximo, g); media += g; n++;
        }
        media = n > 0 ? media / n : 0f;
    }

    [MenuItem("Pinglu/82. Castelo: morros embaixo (com passagem na entrada)")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        ter = Terrain.activeTerrain;
        if (ter == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o terreno na cena aberta. Abra a cena do jogo (SampleScene_Ambientacao) e rode de novo.", "OK"); return; }
        baseY = ter.transform.position.y;
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou colocar tudo no chão de verdade, no mapa inteiro:\n\n• o castelo desce até encostar e o vazio embaixo dele é preenchido com terra (talude suave)\n• casas, ruínas, estacas, pedras, troncos e props: os que flutuam descem, os enterrados sobem\n• árvores do terreno são encaixadas\n\nO relevo (morros) continua. Guardo uma cópia da cena e do terreno em Backups. Pode demorar alguns minutos.", "Continuar", "Cancelar")) return;

        var cena = EditorSceneManager.GetActiveScene();
        Directory.CreateDirectory("Backups");
        if (!string.IsNullOrEmpty(cena.path) && File.Exists(cena.path)) { string c = "Backups/" + Path.GetFileNameWithoutExtension(cena.path) + "_antes_do_morro_castelo.unity"; if (!File.Exists(c)) File.Copy(cena.path, c, false); }
        TerrainData td = ter.terrainData;
        string pt = AssetDatabase.GetAssetPath(td);
        if (!string.IsNullOrEmpty(pt) && File.Exists(pt)) { string c = "Backups/" + Path.GetFileNameWithoutExtension(pt) + "_antes_do_morro_castelo.asset"; if (!File.Exists(c)) File.Copy(pt, c, false); }

        var log = new StringBuilder();
        try
        {
            Undo.IncrementCurrentGroup(); int grupoUndo = Undo.GetCurrentGroup();
            Physics.SyncTransforms();

            EditorUtility.DisplayProgressBar("Chão do castelo", "Castelo...", 0.05f);
            Transform estrutura = null;
            string resumoCastelo = Castelo(td, log, out estrutura);

            EditorUtility.DisplayProgressBar("Chão do castelo", "Árvores do terreno...", 0.35f);
            int arvores = td.treeInstanceCount;
            if (arvores > 0) { Undo.RegisterCompleteObjectUndo(td, "Chão do castelo"); td.SetTreeInstances(td.treeInstances, true); }
            ter.Flush(); Physics.SyncTransforms();

            EditorUtility.DisplayProgressBar("Chão do castelo", "Objetos...", 0.4f);
            int analisados = 0, desceu = 0, subiu = 0, apoiados = 0, ignorados = 0;
            var correcoes = new List<(string nome, float dy, string raiz)>();
            Objetos(estrutura, correcoes, ref analisados, ref desceu, ref subiu, ref apoiados, ref ignorados);
            Physics.SyncTransforms();

            EditorUtility.DisplayProgressBar("Chão do castelo", "Conferindo...", 0.95f);
            string conferencia = Conferir(estrutura);

            Undo.CollapseUndoOperations(grupoUndo);
            EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();

            foreach (var c in correcoes.OrderByDescending(x => Mathf.Abs(x.dy)).Take(15)) log.AppendLine("  " + (c.dy > 0 ? "subiu " : "desceu ") + Mathf.Abs(c.dy).ToString("0.0") + " m: " + c.nome + "  (" + c.raiz + ")");
            string resumo = resumoCastelo + "\n\n• Árvores do terreno encaixadas: " + arvores + "\n• Objetos analisados: " + analisados + "\n   - desceram (flutuavam): " + desceu + "\n   - subiram (enterrados): " + subiu + "\n   - apoiados em outra peça (deixei): " + apoiados + "\n   - ignorados (luzes, tochas, bichos...): " + ignorados + "\n\n" + conferencia;
            Debug.Log("[Chão definitivo]\n" + resumo + "\n\nDetalhes:\n" + log);
            EditorUtility.DisplayDialog("Pinglu", resumo + "\n\nOs detalhes estão no Console. Se algo ainda estiver voando, me diga o nome do objeto.", "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ------------------------------------------------------------------ castelo
    static Transform AcharCastelo()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t.name == "Castelo Destruido" && t.gameObject.activeInHierarchy) return t;
        return null;
    }

    static string Castelo(TerrainData td, StringBuilder log, out Transform estrutura)
    {
        estrutura = null;
        Transform raiz = AcharCastelo();
        if (raiz == null) return "• Castelo: não achei \"Castelo Destruido\" na cena (pulei).";
        estrutura = raiz.Find("Estrutura");
        if (estrutura == null) return "• Castelo: não achei a pasta \"Estrutura\" (pulei).";
        temCorredor = false;
        Transform entrada = raiz.Find("Entrada");
        if (entrada != null)
        {
            Vector3 dv = entrada.position - estrutura.position; dv.y = 0f;
            if (dv.sqrMagnitude > 25f) { corC = estrutura.position; corDir = dv.normalized; corAte = dv.magnitude + 45f; temCorredor = true; }
        }

        const float CEL = 1f;
        Bounds caixa;
        var baseMin = BaseDe(Malhas(estrutura), CEL, true, out caixa);
        if (baseMin.Count == 0) return "• Castelo: a estrutura não tem malhas (pulei).";
        float gMin, gMax, gMed; Folgas(baseMin, CEL, out gMin, out gMax, out gMed);

        // 1) o castelo inteiro no ar: desce rígido
        float desce = 0f;
        if (gMin > 0.05f)
        {
            if (gMin > 40f) log.AppendLine("- castelo: no ar " + gMin.ToString("0.0") + " m, alto demais; não desci (confira se o terreno certo está ativo).");
            else
            {
                desce = gMin;
                Undo.RecordObject(raiz, "Chão definitivo (castelo)");
                raiz.position += Vector3.down * desce;
                Physics.SyncTransforms();
                var nova = new Dictionary<long, float>(baseMin.Count); foreach (var kv in baseMin) nova[kv.Key] = kv.Value - desce; baseMin = nova;
                log.AppendLine("- castelo: estava todo no ar; desceu " + desce.ToString("0.00") + " m.");
            }
        }

        // 2) o que sobrou vazio embaixo: levanta o terreno (talude suave)
        Folgas(baseMin, CEL, out gMin, out gMax, out gMed);
        int res = td.heightmapResolution; Vector3 o = ter.transform.position, size = td.size;
        // distância de cada quadradinho até a borda de fora da pegada (para saber quem está na periferia)
        var dentro = new HashSet<long>(baseMin.Keys);
        var dist = new Dictionary<long, int>(); var fila = new Queue<long>();
        foreach (long k in dentro)
        {
            int ix = (int)(k >> 24) - 100000, iz = (int)(k & 0xFFFFFF) - 100000; bool borda = false;
            for (int d = 0; d < 4 && !borda; d++) if (!dentro.Contains(Chave(ix + (d == 0 ? 1 : d == 1 ? -1 : 0), iz + (d == 2 ? 1 : d == 3 ? -1 : 0)))) borda = true;
            if (borda) { dist[k] = 0; fila.Enqueue(k); }
        }
        while (fila.Count > 0)
        {
            long k = fila.Dequeue(); int ix = (int)(k >> 24) - 100000, iz = (int)(k & 0xFFFFFF) - 100000, dk = dist[k];
            for (int d = 0; d < 4; d++)
            {
                long v = Chave(ix + (d == 0 ? 1 : d == 1 ? -1 : 0), iz + (d == 2 ? 1 : d == 3 ? -1 : 0));
                if (dentro.Contains(v) && !dist.ContainsKey(v)) { dist[v] = dk + 1; fila.Enqueue(v); }
            }
        }

        // alvos: onde a malha está acima do chão
        var alvo = new Dictionary<long, float>(); int vazios = 0; float maiorVazio = 0f; int arcos = 0;
        foreach (var kv in baseMin)
        {
            int ix = (int)(kv.Key >> 24) - 100000, iz = (int)(kv.Key & 0xFFFFFF) - 100000;
            float chao = Chao((ix + 0.5f) * CEL, (iz + 0.5f) * CEL), gap = kv.Value - chao;
            if (gap <= 0.2f) continue;
            if (NoCorredor((ix + 0.5f) * CEL, (iz + 0.5f) * CEL, 0f)) continue;
            float teto = dist[kv.Key] <= 5 ? 8f : 2.5f;     // portões e arcos ficam abertos; só preenche a periferia mais alto
            if (gap > teto) { arcos++; continue; }
            alvo[kv.Key] = kv.Value - 0.12f; vazios++; maiorVazio = Mathf.Max(maiorVazio, gap);
        }
        int levantados = 0; float maiorLev = 0f;
        if (alvo.Count > 0)
        {
            Undo.RegisterCompleteObjectUndo(td, "Chão definitivo (terreno)");
            const float RAIO = 7f, DECLIVE = 0.55f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((caixa.min.x - RAIO - o.x) / size.x * (res - 1)), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((caixa.min.z - RAIO - o.z) / size.z * (res - 1)), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((caixa.max.x + RAIO - o.x) / size.x * (res - 1)), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((caixa.max.z + RAIO - o.z) / size.z * (res - 1)), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            float[,] alt = td.GetHeights(x0, z0, w, h);
            int rc = Mathf.CeilToInt(RAIO / CEL);
            for (int zz = 0; zz < h; zz++)
                for (int xx = 0; xx < w; xx++)
                {
                    float wx = o.x + (x0 + xx) / (float)(res - 1) * size.x, wz = o.z + (z0 + zz) / (float)(res - 1) * size.z;
                    if (NoCorredor(wx, wz, 0f)) continue;
                    int cx = Mathf.FloorToInt(wx / CEL), cz = Mathf.FloorToInt(wz / CEL);
                    float melhor = float.MinValue;
                    for (int dx = -rc; dx <= rc; dx++)
                        for (int dz = -rc; dz <= rc; dz++)
                        {
                            float a;
                            if (!alvo.TryGetValue(Chave(cx + dx, cz + dz), out a)) continue;
                            float d = Mathf.Sqrt(dx * dx + dz * dz) * CEL; if (d > RAIO) continue;
                            float v = a - d * DECLIVE; if (v > melhor) melhor = v;
                        }
                    if (melhor == float.MinValue) continue;
                    float novo = (melhor - baseY) / size.y;
                    if (novo > alt[zz, xx] + 0.00001f)
                    {
                        levantados++; maiorLev = Mathf.Max(maiorLev, (novo - alt[zz, xx]) * size.y);
                        alt[zz, xx] = novo;
                    }
                }
            td.SetHeights(x0, z0, alt);
            ter.Flush(); Physics.SyncTransforms();
        }
        log.AppendLine("- castelo: pegada de " + baseMin.Count + " m²; folga antes do ajuste mín " + gMin.ToString("0.0") + " / média " + gMed.ToString("0.0") + " / máx " + gMax.ToString("0.0") + " m; " + vazios + " pontos vazios preenchidos, " + arcos + " pontos altos (arcos/portões) deixados abertos.");
        return "• Castelo: " + (desce > 0f ? "desceu " + desce.ToString("0.0") + " m" : "já encostava no ponto mais baixo") + "; terreno levantado em " + levantados + " pontos (até " + maiorLev.ToString("0.0") + " m) para fechar o vazio embaixo.";
    }

    // ------------------------------------------------------------------ objetos
    static bool Ignorado(Transform t)
    {
        for (Transform x = t; x != null; x = x.parent)
        {
            if (Contem(x.name, IGNORAR)) return true;
            if (x.GetComponent<Light>() != null || x.GetComponent<Vida>() != null || x.GetComponent<PlayerLocomotion>() != null || x.GetComponent<CharacterController>() != null || x.GetComponent<Animator>() != null || x.GetComponent<Terrain>() != null) return true;
        }
        return false;
    }

    static List<Transform> Unidades(Transform estrutura)
    {
        var cacheB = new Dictionary<Transform, Bounds>();
        Bounds BoundsDe(Transform t)
        {
            Bounds bb;
            if (!cacheB.TryGetValue(t, out bb))
            {
                var rs = t.GetComponentsInChildren<Renderer>(false).Where(q => !(q is ParticleSystemRenderer) && !(q is TrailRenderer) && !(q is LineRenderer)).ToArray();
                bb = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);
                foreach (var q in rs) bb.Encapsulate(q.bounds);
                cacheB[t] = bb;
            }
            return bb;
        }
        var feitos = new HashSet<Transform>(); var lista = new List<Transform>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SkinnedMeshRenderer) continue;
            if (r.GetComponent<MeshFilter>() == null || r.GetComponent<Terrain>() != null) continue;
            if (estrutura != null && r.transform.IsChildOf(estrutura)) continue;        // a estrutura do castelo já foi tratada
            if (r.bounds.center.y < -500f) continue;
            if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.StartsWith("Pinglu/Agua")) continue;
            Transform topo = r.transform;
            while (topo.parent != null)
            {
                if (estrutura != null && topo.parent == estrutura.parent) break;
                Bounds bp = BoundsDe(topo.parent);
                if (bp.size.x < 12f && bp.size.z < 12f && bp.size.y < 30f) topo = topo.parent; else break;
            }
            if (feitos.Add(topo)) lista.Add(topo);
        }
        return lista;
    }

    static void Objetos(Transform estrutura, List<(string, float, string)> correcoes, ref int analisados, ref int desceu, ref int subiu, ref int apoiados, ref int ignorados)
    {
        List<Transform> unidades = Unidades(estrutura);
        Vector3 cc = estrutura != null ? estrutura.position : Vector3.zero;
        int n = 0;
        foreach (Transform u in unidades)
        {
            if (++n % 300 == 0) EditorUtility.DisplayProgressBar("Chão do castelo", "Objetos... " + n + "/" + unidades.Count, 0.4f + 0.5f * n / unidades.Count);
            if (u == null || !u.gameObject.activeInHierarchy) continue;
            if (estrutura == null || new Vector2(u.position.x - cc.x, u.position.z - cc.z).magnitude > 130f) continue;     // só em volta do castelo
            analisados++;
            if (Ignorado(u) || Contem(u.name, new[] { "Ruas", "Estrada", "Praca", "Calcada", "Lajes", "Piso" }) || Contem(u.root.name == u.name ? "" : u.parent != null ? u.parent.name : "", new[] { "Ruas", "Estrada" })) { ignorados++; continue; }
            var malhas = Malhas(u); if (malhas.Count == 0) continue;
            Bounds b; var baseMin = BaseDe(malhas, 0.5f, false, out b);
            if (baseMin.Count == 0 || b.size.y > 40f || b.size.x > 25f || b.size.z > 25f) { ignorados++; continue; }

            // só os pontos da faixa de baixo (o "pé" da peça): os de cima, como telhado, não contam
            float faixa = Mathf.Clamp(b.size.y * 0.25f, 0.4f, 2.5f), pe = b.min.y + faixa;
            float gMin = float.MaxValue, gMax = float.MinValue, gMed = 0f; int cont = 0;
            foreach (var kv in baseMin)
            {
                if (kv.Value > pe) continue;
                int ix = (int)(kv.Key >> 24) - 100000, iz = (int)(kv.Key & 0xFFFFFF) - 100000;
                float g = kv.Value - Chao((ix + 0.5f) * 0.5f, (iz + 0.5f) * 0.5f);
                gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g); gMed += g; cont++;
            }
            if (cont == 0) continue;
            gMed /= cont;
            bool grande = b.size.x > 20f || b.size.z > 20f;
            const float TOL_AR = 0.1f;

            if (gMin > TOL_AR)                    // nenhum ponto toca o chão: está no ar
            {
                if (gMin > 60f) { ignorados++; continue; }
                // apoiado em outra peça (piso, degrau, mesa)? então não é flutuação
                bool apoiado = false;
                Vector3 c = b.center; float alcance = Mathf.Min(gMin + 0.6f, 4f);
                for (int ponto = 0; ponto < 5 && !apoiado; ponto++)
                {
                    Vector3 p = new Vector3(c.x + (ponto == 1 ? b.extents.x * 0.6f : ponto == 2 ? -b.extents.x * 0.6f : 0f), b.min.y + 0.25f, c.z + (ponto == 3 ? b.extents.z * 0.6f : ponto == 4 ? -b.extents.z * 0.6f : 0f));
                    foreach (RaycastHit rh in Physics.RaycastAll(p, Vector3.down, alcance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (rh.collider is TerrainCollider || rh.collider.transform.IsChildOf(u)) continue;
                        if (rh.collider.GetComponentInParent<Vida>() != null || rh.collider.GetComponentInParent<CharacterController>() != null) continue;
                        apoiado = true; break;
                    }
                }
                if (apoiado) { apoiados++; continue; }
                float d = gMin - 0.02f;
                Undo.RecordObject(u, "Chão do castelo"); u.position += Vector3.down * d;
                desceu++; correcoes.Add((u.name, -d, u.root.name));
            }
            else if (gMax < -0.3f)                // todo o pé dentro da terra: sobe
            {
                float sobe = -gMax - 0.1f;
                if (grande && sobe > 4f) { ignorados++; continue; }
                Undo.RecordObject(u, "Chão do castelo"); u.position += Vector3.up * sobe;
                subiu++; correcoes.Add((u.name, sobe, u.root.name));
            }
        }
    }

    // ------------------------------------------------------------------ conferência final
    static string Conferir(Transform estrutura)
    {
        int ar = 0, total = 0; var piores = new List<(float, string)>();
        foreach (Transform u in Unidades(estrutura))
        {
            if (u == null || !u.gameObject.activeInHierarchy || Ignorado(u)) continue;
            var malhas = Malhas(u); if (malhas.Count == 0) continue;
            Bounds b; var baseMin = BaseDe(malhas, 0.5f, false, out b);
            if (baseMin.Count == 0) continue;
            float faixa = Mathf.Clamp(b.size.y * 0.25f, 0.4f, 2.5f), pe = b.min.y + faixa, gMin = float.MaxValue;
            foreach (var kv in baseMin)
            {
                if (kv.Value > pe) continue;
                int ix = (int)(kv.Key >> 24) - 100000, iz = (int)(kv.Key & 0xFFFFFF) - 100000;
                gMin = Mathf.Min(gMin, kv.Value - Chao((ix + 0.5f) * 0.5f, (iz + 0.5f) * 0.5f));
            }
            total++;
            if (gMin > 0.5f && gMin < 60f) { ar++; piores.Add((gMin, u.name + " (" + u.root.name + ")")); }
        }
        string castelo = "";
        if (estrutura != null)
        {
            Bounds cx; var bm = BaseDe(Malhas(estrutura), 1f, true, out cx);
            float mn, mx, md; Folgas(bm, 1f, out mn, out mx, out md);
            int noAr = 0; foreach (var kv in bm) { int ix = (int)(kv.Key >> 24) - 100000, iz = (int)(kv.Key & 0xFFFFFF) - 100000; if (kv.Value - Chao((ix + 0.5f), (iz + 0.5f)) > 1.5f) noAr++; }
            castelo = "Conferência do castelo: folga mínima " + mn.ToString("0.0") + " m (perto de 0 = encostado); " + noAr + " de " + bm.Count + " pontos da base ainda a mais de 1,5 m do chão (arcos, portões e janelas altas contam aqui).\n";
        }
        piores.Sort((x, y) => y.Item1.CompareTo(x.Item1));
        var sb = new StringBuilder(castelo + "Conferência dos objetos: " + ar + " de " + total + " ainda no ar (apoiados em outras peças contam aqui).");
        foreach (var p in piores.Take(8)) sb.Append("\n   " + p.Item1.ToString("0.0") + " m: " + p.Item2);
        return sb.ToString();
    }
}
#endif
