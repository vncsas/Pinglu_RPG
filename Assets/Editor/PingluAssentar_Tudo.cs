using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 79. Assentar tudo no terreno
//  O mapa é cheio de morros (isso continua!), mas depois que o relevo foi mexido sobraram coisas voando e coisas enterradas
//  e água que ficou por cima dos morros. Este menu NÃO mexe no relevo; ele ajusta o resto ao relevo:
//   1) ÁGUA: refaz a malha do lago só onde o terreno está abaixo do nível (a beira acompanha o morro) e tira poças rasas soltas;
//   2) ÁRVORES do terreno: encaixa todas na altura do terreno;
//   3) OBJETOS (troncos, pedras, plantas, casas, props...): quem está flutuando desce até o chão; quem está muito enterrado sobe.
//      Cemitério, castelo e vila (que tiveram o terreno ajustado em volta) só mudam em erros grandes.
//  Uma cópia da cena fica em Backups. Dá para desfazer com Ctrl+Z. Pode rodar de novo.
// ==========================================================================================
public static class PingluAssentarTudo
{
    // pedaços protegidos: o terreno foi moldado em volta deles, só corrige erros grandes
    static readonly string[] PROTEGIDOS = { "Cemiterio", "Castelo", "Vila", "Praca", "Ruas", "Estrada", "Muralha", "Reino", "Ponte", "Acampamento", "Camposanto" };
    // coisas que ficam no ar de propósito ou boiam na água
    static readonly string[] IGNORAR = { "Vitoria", "Lustre", "Tocha", "Lampada", "Lanterna", "Estandarte", "Bandeira", "Corda", "Nevoa", "Horda", "Clima", "Lobby", "Animais Novos", "Monstros do Pantano", "Encontro" };

    class Agua { public Renderer r; public float nivel; public Bounds b; }

    [MenuItem("Pinglu/79. Assentar tudo no terreno (água, árvores, objetos)")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        Terrain terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o terreno na cena aberta. Abra a cena do jogo e rode de novo.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou ajustar ao relevo do mapa (sem achatar nada):\n\n• a água dos lagos (a margem acompanha os morros)\n• as árvores do terreno\n• objetos flutuando ou enterrados\n\nEsses ajustes mexem em muita coisa. Uma cópia da cena fica em Backups e dá para desfazer com Ctrl+Z.", "Continuar", "Cancelar")) return;

        var cena = EditorSceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(cena.path) && File.Exists(cena.path))
        {
            Directory.CreateDirectory("Backups");
            string copia = "Backups/" + Path.GetFileNameWithoutExtension(cena.path) + "_antes_de_assentar.unity";
            if (!File.Exists(copia)) File.Copy(cena.path, copia, false);
        }

        var log = new StringBuilder();
        int aguaRefeita = 0, aguaPocasRemovidas = 0, arvores = 0, desceu = 0, subiu = 0, analisados = 0, pulados = 0;
        try
        {
            Undo.IncrementCurrentGroup(); int grupoUndo = Undo.GetCurrentGroup();

            EditorUtility.DisplayProgressBar("Assentar", "Água...", 0.1f);
            List<Agua> aguas = AcharAguas();
            foreach (Agua a in aguas) aguaRefeita += RefazerAgua(terreno, a, log, ref aguaPocasRemovidas);

            EditorUtility.DisplayProgressBar("Assentar", "Árvores do terreno...", 0.3f);
            TerrainData td = terreno.terrainData;
            arvores = td.treeInstanceCount;
            if (arvores > 0) { Undo.RegisterCompleteObjectUndo(td, "Assentar árvores"); td.SetTreeInstances(td.treeInstances, true); }
            terreno.Flush();

            EditorUtility.DisplayProgressBar("Assentar", "Objetos...", 0.5f);
            Physics.SyncTransforms();
            var correcoes = new List<(string nome, float dy, string raiz)>();
            AssentarObjetos(terreno, aguas, correcoes, ref analisados, ref desceu, ref subiu, ref pulados);
            Physics.SyncTransforms();

            Undo.CollapseUndoOperations(grupoUndo);
            EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();

            string resumo = "Pronto! O relevo continua igual; o resto agora acompanha o chão.\n\n• Água: " + aguas.Count + " superfície(s) refeita(s) (" + aguaPocasRemovidas + " poça(s) rasa(s) solta(s) removida(s))\n• Árvores do terreno encaixadas: " + arvores + "\n• Objetos analisados: " + analisados + "\n   - desceram (flutuavam): " + desceu + "\n   - subiram (enterrados): " + subiu + "\n   - deixados como estavam (apoiados em outras peças, água, etc.): " + pulados;
            foreach (var c in correcoes.OrderByDescending(x => Mathf.Abs(x.dy)).Take(12)) log.AppendLine("  " + (c.dy > 0 ? "subiu " : "desceu ") + Mathf.Abs(c.dy).ToString("0.0") + " m: " + c.nome + "  (" + c.raiz + ")");
            Debug.Log("[Assentar]\n" + resumo + "\nMaiores correções:\n" + log);
            EditorUtility.DisplayDialog("Pinglu", resumo + "\n\nOs maiores ajustes estão no Console (Assentar). Se algo ainda estiver voando, me diga o nome do objeto.", "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ------------------------------------------------------------------ água
    static List<Agua> AcharAguas()
    {
        var lista = new List<Agua>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader == null) continue;
            string sn = r.sharedMaterial.shader.name;
            bool agua = sn.StartsWith("Pinglu/Agua") || (r.name == "Agua" && r.transform.parent != null && r.transform.parent.name.StartsWith("Lago"));
            if (!agua || r.GetComponent<MeshFilter>() == null) continue;
            lista.Add(new Agua { r = r, nivel = r.bounds.center.y, b = r.bounds });
        }
        return lista;
    }

    // refaz a malha: só células onde o terreno está abaixo do nível; poças pequenas ou rasas somem
    static int RefazerAgua(Terrain terreno, Agua a, StringBuilder log, ref int pocasRemovidas)
    {
        MeshFilter mf = a.r.GetComponent<MeshFilter>(); Mesh mesh = mf.sharedMesh; if (mesh == null) return 0;
        const float cel = 1f;
        Bounds b = a.b; b.Expand(new Vector3(24f, 0f, 24f));
        int nx = Mathf.CeilToInt(b.size.x / cel), nz = Mathf.CeilToInt(b.size.z / cel);
        if (nx < 2 || nz < 2 || nx * nz > 600000) { log.AppendLine("- água \"" + a.r.name + "\": área grande demais para refazer."); return 0; }
        float x0 = b.min.x, z0 = b.min.z, baseY = terreno.transform.position.y;
        float[,] h = new float[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++) for (int j = 0; j <= nz; j++) h[i, j] = terreno.SampleHeight(new Vector3(x0 + i * cel, 0f, z0 + j * cel)) + baseY;

        float limite = a.nivel - 0.02f;
        bool[,] dentro = new bool[nx, nz];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
        {
            int abaixo = (h[i, j] < limite ? 1 : 0) + (h[i + 1, j] < limite ? 1 : 0) + (h[i, j + 1] < limite ? 1 : 0) + (h[i + 1, j + 1] < limite ? 1 : 0);
            dentro[i, j] = abaixo >= 3 || (abaixo == 2 && (h[i, j] + h[i + 1, j] + h[i, j + 1] + h[i + 1, j + 1]) * 0.25f < limite);
        }

        // componentes ligados: tira poças com menos de 60 m² ou menos de 0,4 m de fundo
        int[,] comp = new int[nx, nz]; int total = 0; var manter = new List<bool>();
        var pilha = new Stack<(int, int)>();
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
        {
            if (!dentro[i, j] || comp[i, j] != 0) continue;
            total++; int area = 0; float fundo = 0f; pilha.Push((i, j)); comp[i, j] = total;
            while (pilha.Count > 0)
            {
                var (ci, cj) = pilha.Pop(); area++;
                fundo = Mathf.Max(fundo, a.nivel - Mathf.Min(h[ci, cj], h[ci + 1, cj + 1]));
                for (int d = 0; d < 4; d++)
                {
                    int ni = ci + (d == 0 ? 1 : d == 1 ? -1 : 0), nj = cj + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || !dentro[ni, nj] || comp[ni, nj] != 0) continue;
                    comp[ni, nj] = total; pilha.Push((ni, nj));
                }
            }
            bool ok = area * cel * cel >= 60f && fundo >= 0.4f;
            manter.Add(ok);
            if (!ok) pocasRemovidas++;
        }

        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
        {
            if (!dentro[i, j] || !manter[comp[i, j] - 1]) continue;
            float x = x0 + i * cel, z = z0 + j * cel; int k = vs.Count;
            vs.Add(mf.transform.InverseTransformPoint(new Vector3(x, a.nivel, z))); vs.Add(mf.transform.InverseTransformPoint(new Vector3(x + cel, a.nivel, z)));
            vs.Add(mf.transform.InverseTransformPoint(new Vector3(x + cel, a.nivel, z + cel))); vs.Add(mf.transform.InverseTransformPoint(new Vector3(x, a.nivel, z + cel)));
            uvs.Add(new Vector2(x, z) * 0.1f); uvs.Add(new Vector2(x + cel, z) * 0.1f); uvs.Add(new Vector2(x + cel, z + cel) * 0.1f); uvs.Add(new Vector2(x, z + cel) * 0.1f);
            tris.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
        }
        if (vs.Count == 0) { log.AppendLine("- água \"" + a.r.name + "\": nenhuma célula ficou (o terreno está todo acima do nível?). Deixei como estava."); return 0; }

        Undo.RegisterCompleteObjectUndo(mesh, "Refazer água");
        mesh.Clear(); mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vs); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        log.AppendLine("- água \"" + a.r.name + "\" nível " + a.nivel.ToString("0.0") + ": " + vs.Count / 4 + " células, " + manter.Count(m => m) + " lago(s) mantido(s).");
        return 1;
    }

    // ------------------------------------------------------------------ objetos
    static bool Contem(string nome, string[] lista) { foreach (string p in lista) if (nome.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }

    static bool Protegido(Transform t)
    {
        for (Transform x = t; x != null; x = x.parent) if (Contem(x.name, PROTEGIDOS)) return true;
        return false;
    }

    static bool Ignorado(Transform t)
    {
        for (Transform x = t; x != null; x = x.parent)
        {
            if (Contem(x.name, IGNORAR)) return true;
            if (x.GetComponent<Light>() != null || x.GetComponent<Vida>() != null || x.GetComponent<PlayerLocomotion>() != null || x.GetComponent<CharacterController>() != null || x.GetComponent<Animator>() != null) return true;
        }
        return false;
    }

    static void AssentarObjetos(Terrain terreno, List<Agua> aguas, List<(string, float, string)> correcoes, ref int analisados, ref int desceu, ref int subiu, ref int pulados)
    {
        float baseY = terreno.transform.position.y;
        float Alt(float x, float z) { return terreno.SampleHeight(new Vector3(x, 0f, z)) + baseY; }

        var feitos = new HashSet<Transform>(); var cacheB = new Dictionary<Transform, Bounds>();
        Bounds BoundsDe(Transform t)
        {
            if (!cacheB.TryGetValue(t, out Bounds bb))
            {
                var rs = t.GetComponentsInChildren<Renderer>(false).Where(q => !(q is ParticleSystemRenderer) && !(q is TrailRenderer) && !(q is LineRenderer)).ToArray();
                bb = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);
                foreach (var q in rs) bb.Encapsulate(q.bounds);
                cacheB[t] = bb;
            }
            return bb;
        }

        Renderer[] todos = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var unidades = new List<Transform>();
        foreach (Renderer r in todos)
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r.GetComponent<Terrain>() != null) continue;
            if (r.GetComponentInParent<Terrain>() != null && r.GetComponent<MeshFilter>() == null) continue;
            Bounds b0 = r.bounds; if (b0.center.y < -500f) continue;
            if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.StartsWith("Pinglu/Agua")) continue;
            // sobe para o "objeto inteiro" (enquanto o pai ainda for pequeno)
            Transform topo = r.transform;
            while (topo.parent != null)
            {
                Bounds bp = BoundsDe(topo.parent);
                if (bp.size.x < 12f && bp.size.z < 12f && bp.size.y < 30f) topo = topo.parent; else break;
            }
            if (feitos.Add(topo)) unidades.Add(topo);
        }

        int n = 0;
        foreach (Transform u in unidades)
        {
            if (++n % 400 == 0) EditorUtility.DisplayProgressBar("Assentar", "Objetos... " + n + "/" + unidades.Count, 0.5f + 0.5f * n / unidades.Count);
            if (u == null || !u.gameObject.activeInHierarchy) continue;
            analisados++;
            if (Ignorado(u)) { pulados++; continue; }
            Bounds b = BoundsDe(u);
            if (b.size.y > 60f || b.size.x > 150f || b.size.z > 150f) { pulados++; continue; }    // montanhas e blocos gigantes

            // altura do chão sob o objeto (média de 5 pontos)
            float ex = Mathf.Min(b.extents.x, 3f) * 0.7f, ez = Mathf.Min(b.extents.z, 3f) * 0.7f;
            float hc = Alt(b.center.x, b.center.z);
            float h = (hc * 2f + Alt(b.center.x + ex, b.center.z) + Alt(b.center.x - ex, b.center.z) + Alt(b.center.x, b.center.z + ez) + Alt(b.center.x, b.center.z - ez)) / 6f;
            float baseObj = b.min.y;
            float lacuna = baseObj - h;                                   // >0 flutuando, <0 enterrado

            bool protegido = Protegido(u) || b.size.x > 20f || b.size.z > 20f;
            float tolFlutua = protegido ? 1.2f : 0.12f, tolEnterra = protegido ? 2.5f : 0.6f;
            if (lacuna <= tolFlutua && lacuna >= -tolEnterra) continue;

            // boiando na água? (vitórias-régias, barcos...)
            bool naAgua = false;
            foreach (Agua a in aguas) if (b.center.x >= a.b.min.x && b.center.x <= a.b.max.x && b.center.z >= a.b.min.z && b.center.z <= a.b.max.z && hc < a.nivel - 0.1f && Mathf.Abs(baseObj - a.nivel) < 0.6f) { naAgua = true; break; }
            if (naAgua) { pulados++; continue; }

            if (lacuna > tolFlutua)
            {
                if (lacuna > 40f) { pulados++; continue; }                // alto demais: provavelmente é de propósito
                // apoiado em outra peça (mesa, degrau, telhado)? então não é flutuação
                bool apoiado = false;
                foreach (RaycastHit rh in Physics.RaycastAll(new Vector3(b.center.x, baseObj + 0.2f, b.center.z), Vector3.down, Mathf.Min(lacuna + 0.5f, 3f), ~0, QueryTriggerInteraction.Ignore))
                {
                    if (rh.collider is TerrainCollider || rh.collider.transform.IsChildOf(u)) continue;
                    if (rh.collider.GetComponentInParent<Vida>() != null) continue;
                    apoiado = true; break;
                }
                if (apoiado) { pulados++; continue; }
                Undo.RecordObject(u, "Assentar objeto");
                u.position += Vector3.down * (lacuna - 0.02f);
                desceu++; correcoes.Add((u.name, -(lacuna - 0.02f), u.root.name));
            }
            else
            {
                float sobe = -lacuna - 0.15f;
                Undo.RecordObject(u, "Assentar objeto");
                u.position += Vector3.up * sobe;
                subiu++; correcoes.Add((u.name, sobe, u.root.name));
            }
        }
    }
}
