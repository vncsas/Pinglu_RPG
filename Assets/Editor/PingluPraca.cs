// =====================================================================
//  PingluPraca.cs  -  Prepara a praça do cemitério para a cena do Guardião
// ---------------------------------------------------------------------
//  Pinglu > 66: tira o monumento central e o piso do meio da malha grande do cemitério,
//               recria os dois como peças soltas (que caem na cena), abre o buraco gigante
//               (terreno + paredes + brilho) e liga tudo ao componente PracaDoGuardiao.
//  Pinglu > 67: testa a cena durante o Play.      Pinglu > 68: desfaz tudo (volta a malha original).
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluPraca
{
    const string PASTA = "Assets/Cenario/Cemiterio/Praca";
    static string CaminhoBackup(string malha) { return "Assets/Cenario/Cemiterio/Backup_" + malha + ".asset"; }
    const string NOME = "Praça do Guardião";
    const float RAIO_BURACO = 10f, PROF = 4.5f, RAIO_FUNDO = 2.6f, RAIO_BORDA_FUNDO = 3.4f, RAIO_MON = 9.5f;

    // ------------------------------------------------------------ construtor de malhas simples
    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int>[] tri;
        public MB(int sub) { tri = new List<int>[sub]; for (int i = 0; i < sub; i++) tri[i] = new List<int>(); }
        static Vector2 UV(Vector3 q, Vector3 nrm)
        {
            if (Mathf.Abs(nrm.y) > 0.7f) return new Vector2(q.x, q.z);
            Vector3 t = Vector3.Cross(Vector3.up, nrm).normalized;
            return new Vector2(Vector3.Dot(q, t), q.y);
        }
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 nrm, int s)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nrm) < 0f) { Vector3 t = b; b = c; c = t; }
            int i0 = v.Count;
            foreach (Vector3 q in new[] { a, b, c }) { v.Add(q); n.Add(nrm); uv.Add(UV(q, nrm)); }
            tri[s].Add(i0); tri[s].Add(i0 + 1); tri[s].Add(i0 + 2);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, int s) { Tri(a, b, c, nrm, s); Tri(a, c, d, nrm, s); }
        public Mesh Gerar(string nome, Vector3 pivo)
        {
            Mesh m = new Mesh { name = nome };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            Vector3[] p = v.ToArray(); for (int i = 0; i < p.Length; i++) p[i] -= pivo;
            m.vertices = p; m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = tri.Length;
            for (int i = 0; i < tri.Length; i++) m.SetTriangles(tri[i], i);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
    }

    struct T3 { public int s, a, b, c; }

    // ============================================================ 66
    [MenuItem("Pinglu/66. Cemitério: abrir a praça para o Guardião")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Procurando a malha do cemitério...", 0.05f);
            MeshFilter mf = AcharCemiterio();
            if (mf == null) { EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("Pinglu", "Não achei o cemitério na cena (objeto \"Cemiterio\" com malha).", "OK"); return; }
            Mesh alvo = mf.sharedMesh;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            Material[] mats = mr.sharedMaterials;

            // ---- backup da malha original (guardado com o nome da malha; só é criado enquanto a malha ainda está intacta)
            string caminhoBk = CaminhoBackup(alvo.name);
            Mesh bk = AssetDatabase.LoadAssetAtPath<Mesh>(caminhoBk);
            Mesh fonte = bk != null ? bk : alvo;

            EditorUtility.DisplayProgressBar("Pinglu", "Lendo a malha (" + fonte.vertexCount + " vértices)...", 0.12f);
            Vector3[] V = fonte.vertices, N = fonte.normals; Vector2[] U = fonte.uv;
            int sm = fonte.subMeshCount;
            int[][] T = new int[sm][];
            for (int s = 0; s < sm; s++) T[s] = fonte.GetTriangles(s);

            // ---- 1) acha o piso redondo: leque com um vértice no centro (malha intacta) ou anel com borda no raio do buraco (malha já editada)
            float topo = float.NegativeInfinity; int subTopo = -1;
            for (int s = 0; s < sm; s++)
                for (int i = 0; i + 2 < T[s].Length; i += 3)
                {
                    int k = Centro(V, T[s], i);
                    if (k < 0) continue;
                    int c = T[s][i + k];
                    if (N[c].y > 0.5f && V[c].y > topo) { topo = V[c].y; subTopo = s; }
                }
            bool editar = subTopo >= 0;
            if (!editar)
            {
                for (int s = 0; s < sm; s++)
                    for (int i = 0; i + 2 < T[s].Length; i += 3)
                    {
                        Vector3 a0 = V[T[s][i]], a1 = V[T[s][i + 1]], a2 = V[T[s][i + 2]];
                        if (Mathf.Abs(a0.y - a1.y) > 0.01f || Mathf.Abs(a0.y - a2.y) > 0.01f) continue;
                        bool borda = Mathf.Abs(Horiz(a0) - RAIO_BURACO) < 0.05f || Mathf.Abs(Horiz(a1) - RAIO_BURACO) < 0.05f || Mathf.Abs(Horiz(a2) - RAIO_BURACO) < 0.05f;
                        if (borda && N[T[s][i]].y > 0.5f && a0.y > topo) { topo = a0.y; subTopo = s; }
                    }
            }
            if (subTopo < 0)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Pinglu", "Não achei o piso redondo no centro da malha do cemitério.", "OK");
                return;
            }

            // ---- 2) e 3) malha intacta: refaz os leques como anéis (buraco) e tira o monumento
            int tirados = 0;
            if (editar)
            {
                if (bk == null)
                {
                    Mesh copia = Object.Instantiate(alvo); copia.name = alvo.name;
                    AssetDatabase.CreateAsset(copia, caminhoBk);
                }
                EditorUtility.DisplayProgressBar("Pinglu", "Cortando o buraco no piso e tirando o monumento...", 0.3f);
                List<Vector3> nv = new List<Vector3>(V), nn = new List<Vector3>(N); List<Vector2> nu = new List<Vector2>(U);
                List<int>[] nova = new List<int>[sm];
                for (int s = 0; s < sm; s++)
                {
                    nova[s] = new List<int>(T[s].Length);
                    for (int i = 0; i + 2 < T[s].Length; i += 3)
                    {
                        int a = T[s][i], b = T[s][i + 1], c = T[s][i + 2];
                        int k = Centro(V, T[s], i);
                        if (k >= 0)
                        {
                            int[] q = { a, b, c };
                            int cc = q[k], p = q[(k + 1) % 3], r = q[(k + 2) % 3];
                            float fp = RAIO_BURACO / Mathf.Max(0.01f, Horiz(V[p] - V[cc])), fr = RAIO_BURACO / Mathf.Max(0.01f, Horiz(V[r] - V[cc]));
                            int ap = nv.Count; nv.Add(Vector3.Lerp(V[cc], V[p], fp)); nn.Add(N[cc]); nu.Add(Vector2.Lerp(U[cc], U[p], fp));
                            int ar = nv.Count; nv.Add(Vector3.Lerp(V[cc], V[r], fr)); nn.Add(N[cc]); nu.Add(Vector2.Lerp(U[cc], U[r], fr));
                            nova[s].Add(ap); nova[s].Add(p); nova[s].Add(r);
                            nova[s].Add(ap); nova[s].Add(r); nova[s].Add(ar);
                            continue;
                        }
                        Vector3 cen = (V[a] + V[b] + V[c]) / 3f;
                        if (Horiz(cen) < RAIO_MON && cen.y > topo - 0.02f) { tirados++; continue; }
                        nova[s].Add(a); nova[s].Add(b); nova[s].Add(c);
                    }
                }
                EditorUtility.DisplayProgressBar("Pinglu", "Gravando a malha do cemitério...", 0.5f);
                alvo.Clear();
                alvo.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                alvo.SetVertices(nv); alvo.SetNormals(nn); alvo.SetUVs(0, nu);
                alvo.subMeshCount = sm;
                for (int s = 0; s < sm; s++) alvo.SetTriangles(nova[s], s);
                alvo.RecalculateBounds(); alvo.RecalculateTangents();
                EditorUtility.SetDirty(alvo);
                MeshCollider mc = mf.GetComponent<MeshCollider>();
                if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = alvo; }
                AssetDatabase.SaveAssets();
            }

            // ---- 4) objeto da praça
            EditorUtility.DisplayProgressBar("Pinglu", "Montando as peças soltas...", 0.62f);
            Transform velho = mf.transform.parent != null ? mf.transform.parent.Find(NOME) : null;
            GameObject velhoGo = velho != null ? velho.gameObject : GameObject.Find(NOME);
            if (velhoGo != null) Object.DestroyImmediate(velhoGo);
            if (AssetDatabase.IsValidFolder(PASTA)) { foreach (string g in AssetDatabase.FindAssets("t:Mesh", new[] { PASTA })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g)); }
            GameObject holder = new GameObject(NOME);
            holder.transform.SetParent(mf.transform.parent, false);
            holder.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
            holder.transform.localScale = mf.transform.localScale;
            PracaDoGuardiao praca = holder.AddComponent<PracaDoGuardiao>();

            int subPedra = 0;
            for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name == "Cem_Pedra") subPedra = i;
            Material matPedra = mats[subPedra];

            // 4a) monumento em pedaços (refeito por código, por faixa de altura e, embaixo, por quarto de círculo)
            Material[] matsMon;
            Mesh monMesh = PingluVila.MonumentoDaPraca(topo, out matsMon);
            Vector3[] MV = monMesh.vertices, MN = monMesh.normals; Vector2[] MU = monMesh.uv;
            int smM = monMesh.subMeshCount;
            List<T3> mon = new List<T3>();
            for (int s = 0; s < smM; s++) { int[] tt = monMesh.GetTriangles(s); for (int i = 0; i + 2 < tt.Length; i += 3) mon.Add(new T3 { s = s, a = tt[i], b = tt[i + 1], c = tt[i + 2] }); }
            Object.DestroyImmediate(monMesh);
            float[] faixas = { 1.0f, 2.2f, 3.6f, 5.6f, 7.4f, 9.0f, 11.0f };
            Dictionary<int, List<T3>> grupos = new Dictionary<int, List<T3>>();
            foreach (T3 t in mon)
            {
                Vector3 cen = (MV[t.a] + MV[t.b] + MV[t.c]) / 3f;
                float h = cen.y - topo; int band = 0; while (band < faixas.Length && h > faixas[band]) band++;
                int quarto = band <= 3 ? Mathf.FloorToInt((Mathf.Atan2(cen.z, cen.x) + Mathf.PI) / (Mathf.PI * 0.5f)) % 4 : 0;
                int key = band * 4 + quarto;
                List<T3> l; if (!grupos.TryGetValue(key, out l)) { l = new List<T3>(); grupos[key] = l; }
                l.Add(t);
            }
            List<Transform> pedMon = new List<Transform>();
            int idx = 0;
            foreach (KeyValuePair<int, List<T3>> kv in grupos)
            {
                MB mb = new MB(smM); Dictionary<int, int> mapa = new Dictionary<int, int>();
                Vector3 pivo = Vector3.zero; int cont = 0;
                foreach (T3 t in kv.Value)
                {
                    foreach (int vi in new[] { t.a, t.b, t.c })
                    {
                        int nvx;
                        if (!mapa.TryGetValue(vi, out nvx)) { nvx = mb.v.Count; mapa[vi] = nvx; mb.v.Add(MV[vi]); mb.n.Add(MN[vi]); mb.uv.Add(MU[vi]); }
                        mb.tri[t.s].Add(nvx);
                    }
                    pivo += (MV[t.a] + MV[t.b] + MV[t.c]) / 3f; cont++;
                }
                pivo /= Mathf.Max(1, cont);
                Mesh m = mb.Gerar("Monumento_" + idx.ToString("00"), pivo);
                AssetDatabase.CreateAsset(m, PASTA + "/" + m.name + ".asset");
                GameObject g = new GameObject("Monumento_" + idx.ToString("00"));
                g.transform.SetParent(holder.transform, false); g.transform.localPosition = pivo;
                g.AddComponent<MeshFilter>().sharedMesh = m;
                g.AddComponent<MeshRenderer>().sharedMaterials = matsMon;
                g.AddComponent<MeshCollider>().sharedMesh = m;
                pedMon.Add(g.transform); idx++;
            }

            // 4b) piso: placas em dois anéis (centro e fora), com espessura
            List<Transform> pedPiso = new List<Transform>();
            System.Random rnd = new System.Random(5);
            float[] raios = { 0f, 5.6f, RAIO_BURACO + 0.06f };
            int[] fatias = { 6, 10 };
            idx = 0;
            for (int anel = 0; anel < 2; anel++)
            {
                int nf = fatias[anel];
                float[] ang = new float[nf + 1];
                float off = (float)rnd.NextDouble() * Mathf.PI * 2f;
                for (int i = 0; i < nf; i++) ang[i] = off + (i + ((float)rnd.NextDouble() - 0.5f) * 0.5f) * Mathf.PI * 2f / nf;
                System.Array.Sort(ang, 0, nf); ang[nf] = ang[0] + Mathf.PI * 2f;
                for (int i = 0; i < nf; i++)
                {
                    Vector3 pivo;
                    Mesh m = PecaDoPiso(raios[anel], raios[anel + 1], ang[i], ang[i + 1], topo - 0.01f, 0.7f, sm, subTopo, subPedra, "Piso_" + idx.ToString("00"), out pivo);
                    AssetDatabase.CreateAsset(m, PASTA + "/" + m.name + ".asset");
                    GameObject g = new GameObject("Piso_" + idx.ToString("00"));
                    g.transform.SetParent(holder.transform, false); g.transform.localPosition = pivo;
                    g.AddComponent<MeshFilter>().sharedMesh = m;
                    g.AddComponent<MeshRenderer>().sharedMaterials = mats;
                    g.AddComponent<MeshCollider>().sharedMesh = m;
                    pedPiso.Add(g.transform); idx++;
                }
            }

            // 4c) o buraco
            EditorUtility.DisplayProgressBar("Pinglu", "Cavando o buraco...", 0.78f);
            GameObject cratera = new GameObject("Buraco"); cratera.transform.SetParent(holder.transform, false);
            Mesh mc2 = MalhaDaCratera(topo, sm, subPedra);
            AssetDatabase.CreateAsset(mc2, PASTA + "/Buraco_paredes.asset");
            GameObject paredes = new GameObject("Paredes"); paredes.transform.SetParent(cratera.transform, false);
            paredes.AddComponent<MeshFilter>().sharedMesh = mc2;
            Material[] mp = new Material[sm]; for (int i = 0; i < sm; i++) mp[i] = matPedra;
            paredes.AddComponent<MeshRenderer>().sharedMaterials = mp;
            paredes.AddComponent<MeshCollider>().sharedMesh = mc2;
            GameObject chao = new GameObject("ColisorDoFundo"); chao.transform.SetParent(cratera.transform, false);
            BoxCollider bc = chao.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, topo - PROF - 0.25f, 0f); bc.size = new Vector3(RAIO_BORDA_FUNDO * 1.7f, 0.5f, RAIO_BORDA_FUNDO * 1.7f);

            MB disco = new MB(1);
            float yd = topo - PROF - 0.8f;
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * Mathf.PI * 2f / 24f, a1 = (i + 1) * Mathf.PI * 2f / 24f;
                disco.Tri(new Vector3(0f, yd, 0f), new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (RAIO_FUNDO + 0.1f) + Vector3.up * yd, new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * (RAIO_FUNDO + 0.1f) + Vector3.up * yd, Vector3.up, 0);
            }
            Mesh md = disco.Gerar("Buraco_brilho", Vector3.zero);
            AssetDatabase.CreateAsset(md, PASTA + "/Buraco_brilho.asset");
            GameObject brilho = new GameObject("Brilho"); brilho.transform.SetParent(cratera.transform, false);
            brilho.AddComponent<MeshFilter>().sharedMesh = md;
            MeshRenderer brr = brilho.AddComponent<MeshRenderer>();
            brr.sharedMaterial = CriarMaterialBrilho();
            brr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject luzGo = new GameObject("Luz do buraco"); luzGo.transform.SetParent(cratera.transform, false);
            luzGo.transform.localPosition = new Vector3(0f, topo - PROF + 1.4f, 0f);
            Light luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point; luz.color = new Color(1f, 0.42f, 0.14f); luz.range = 38f; luz.intensity = 0f; luz.shadows = LightShadows.None;
            cratera.SetActive(false);

            // ---- liga tudo no componente
            praca.pedacosMonumento = pedMon.ToArray(); praca.pedacosPiso = pedPiso.ToArray();
            praca.posMonumento = new Vector3[pedMon.Count]; praca.rotMonumento = new Quaternion[pedMon.Count];
            for (int i = 0; i < pedMon.Count; i++) { praca.posMonumento[i] = pedMon[i].localPosition; praca.rotMonumento[i] = pedMon[i].localRotation; }
            praca.posPiso = new Vector3[pedPiso.Count]; praca.rotPiso = new Quaternion[pedPiso.Count];
            for (int i = 0; i < pedPiso.Count; i++) { praca.posPiso[i] = pedPiso[i].localPosition; praca.rotPiso[i] = pedPiso[i].localRotation; }
            praca.cratera = cratera; praca.brilhoDoFundo = brr; praca.luzDoBuraco = luz;
            praca.matPoeira = CriarMaterialParticula("Poeira", 5f, 10f, 1f); praca.matBrasa = CriarMaterialParticula("Brasa", 5f, 1f, 0f); praca.matPedra = matPedra;
            praca.raioDoBuraco = RAIO_BURACO; praca.profundidade = PROF; praca.raioDoFundo = RAIO_FUNDO; praca.topoDoPiso = topo;

            // ---- terreno: abre o buraco por baixo da praça
            EditorUtility.DisplayProgressBar("Pinglu", "Abrindo o buraco no terreno...", 0.9f);
            string terr = AbrirBuracoNoTerreno(holder.transform.position, true);

            // ---- aponta o chefe para a praça
            GerenciadorDeOndas g2 = Object.FindFirstObjectByType<GerenciadorDeOndas>();
            if (g2 != null) { SerializedObject so = new SerializedObject(g2); SerializedProperty pr = so.FindProperty("centroDaArena"); if (pr != null) { pr.objectReferenceValue = holder.transform; so.ApplyModifiedPropertiesWithoutUndo(); } }

            EditorUtility.SetDirty(praca);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(holder.scene); EditorSceneManager.SaveOpenScenes();
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Pinglu", "Praça pronta!\n\n- Monumento central: " + pedMon.Count + " pedaços soltos (monumento inteiro, " + mon.Count + " triângulos; " + (editar ? tirados + " tirados da malha do cemitério" : "a malha do cemitério já estava sem ele") + ")\n- Piso do meio: " + pedPiso.Count + " placas\n- Buraco: raio " + RAIO_BURACO + " m, " + PROF + " m de fundo\n- " + terr +
                "\n\nPara ver: aperte Play e use Pinglu > 67. Chefe: testar a cena do buraco.\nPara desfazer: Pinglu > 68.", "OK");
        }
        catch (System.Exception e) { Debug.LogException(e); EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("Pinglu", "Deu um erro: " + e.Message + "\n(veja o Console)", "OK"); }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ============================================================ 67
    [MenuItem("Pinglu/67. Chefe: testar a cena do buraco (durante o Play)")]
    public static void Testar()
    {
        if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Aperte Play primeiro e depois use este menu.", "OK"); return; }
        PracaDoGuardiao pr = Object.FindFirstObjectByType<PracaDoGuardiao>();
        GerenciadorDeOndas g = Object.FindFirstObjectByType<GerenciadorDeOndas>();
        PlayerLocomotion p = Object.FindFirstObjectByType<PlayerLocomotion>();
        if (pr == null || g == null) { Debug.LogWarning("[Praça] rode o Pinglu > 66 antes (sem praça ou sem gerenciador de ondas)."); return; }
        if (p != null)
        {
            Vector3 pos = pr.Centro() + pr.transform.forward * 30f + Vector3.up * 0.3f;
            CharacterController cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; p.transform.position = pos; if (cc != null) cc.enabled = true;
        }
        g.TestarChefeAgora();
    }

    // ============================================================ 68
    [MenuItem("Pinglu/68. Cemitério: desfazer a praça do Guardião")]
    public static void Desfazer()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        MeshFilter mf = AcharCemiterio();
        Mesh bk = mf != null ? AssetDatabase.LoadAssetAtPath<Mesh>(CaminhoBackup(mf.sharedMesh.name)) : null;
        if (bk == null || mf == null) { EditorUtility.DisplayDialog("Pinglu", "Não há o que desfazer (sem backup da malha ou sem cemitério).", "OK"); return; }
        Mesh alvo = mf.sharedMesh;
        EditorUtility.DisplayProgressBar("Pinglu", "Voltando a malha original...", 0.4f);
        List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); List<Vector2> uv = new List<Vector2>();
        bk.GetVertices(v); bk.GetNormals(n); bk.GetUVs(0, uv);
        int sm = bk.subMeshCount; List<int[]> t = new List<int[]>(); for (int s = 0; s < sm; s++) t.Add(bk.GetTriangles(s));
        alvo.Clear(); alvo.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        alvo.SetVertices(v); alvo.SetNormals(n); alvo.SetUVs(0, uv); alvo.subMeshCount = sm;
        for (int s = 0; s < sm; s++) alvo.SetTriangles(t[s], s);
        alvo.RecalculateBounds(); alvo.RecalculateTangents(); EditorUtility.SetDirty(alvo);
        MeshCollider mc = mf.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = alvo; }
        GameObject h = GameObject.Find(NOME);
        Vector3 c = h != null ? h.transform.position : mf.transform.position;
        if (h != null) Object.DestroyImmediate(h);
        AbrirBuracoNoTerreno(c, false);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveOpenScenes();
        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("Pinglu", "Praça original de volta (monumento e piso no lugar, terreno fechado).", "OK");
    }

    // ============================================================ apoio
    static MeshFilter AcharCemiterio()
    {
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (mf.name == "Cemiterio" && mf.sharedMesh != null && mf.gameObject.scene.IsValid() && mf.sharedMesh.name.StartsWith("Cemiterio_")) return mf;
        return null;
    }

    static float Horiz(Vector3 v) { return Mathf.Sqrt(v.x * v.x + v.z * v.z); }

    // se o triângulo (a partir de i) é um leque de tampa de cilindro, devolve qual dos 3 vértices é o centro; senão -1
    static int Centro(Vector3[] V, int[] T, int i)
    {
        Vector3 a = V[T[i]], b = V[T[i + 1]], c = V[T[i + 2]];
        if (Mathf.Abs(a.y - b.y) > 0.01f || Mathf.Abs(a.y - c.y) > 0.01f) return -1;
        Vector3[] q = { a, b, c };
        for (int k = 0; k < 3; k++)
        {
            if (Mathf.Abs(q[k].x) > 0.02f || Mathf.Abs(q[k].z) > 0.02f) continue;
            if (Horiz(q[(k + 1) % 3]) > RAIO_BURACO + 0.8f && Horiz(q[(k + 2) % 3]) > RAIO_BURACO + 0.8f) return k;
        }
        return -1;
    }

    static Vector3 P(float r, float a, float y) { return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }

    // uma placa do piso: setor de anel com espessura (coordenadas do cemitério; o pivô é o meio da placa)
    static Mesh PecaDoPiso(float r0, float r1, float a0, float a1, float topo, float esp, int sub, int subTopo, int subLado, string nome, out Vector3 pivo)
    {
        MB mb = new MB(sub);
        float bot = topo - esp;
        int m = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / (Mathf.PI / 24f)));
        for (int j = 0; j < m; j++)
        {
            float x0 = Mathf.Lerp(a0, a1, j / (float)m), x1 = Mathf.Lerp(a0, a1, (j + 1) / (float)m), xm = (x0 + x1) * 0.5f;
            mb.Quad(P(r0, x0, topo), P(r1, x0, topo), P(r1, x1, topo), P(r0, x1, topo), Vector3.up, subTopo);
            mb.Quad(P(r0, x0, bot), P(r1, x0, bot), P(r1, x1, bot), P(r0, x1, bot), Vector3.down, subLado);
            mb.Quad(P(r1, x0, topo), P(r1, x1, topo), P(r1, x1, bot), P(r1, x0, bot), new Vector3(Mathf.Cos(xm), 0f, Mathf.Sin(xm)), subLado);
            if (r0 > 0.01f) mb.Quad(P(r0, x0, topo), P(r0, x1, topo), P(r0, x1, bot), P(r0, x0, bot), -new Vector3(Mathf.Cos(xm), 0f, Mathf.Sin(xm)), subLado);
        }
        Vector3 t0 = new Vector3(-Mathf.Sin(a0), 0f, Mathf.Cos(a0)), t1 = new Vector3(-Mathf.Sin(a1), 0f, Mathf.Cos(a1));
        mb.Quad(P(r0, a0, topo), P(r1, a0, topo), P(r1, a0, bot), P(r0, a0, bot), -t0, subLado);
        mb.Quad(P(r0, a1, topo), P(r1, a1, topo), P(r1, a1, bot), P(r0, a1, bot), t1, subLado);
        pivo = P((r0 + r1) * 0.5f, (a0 + a1) * 0.5f, topo - esp * 0.5f);
        return mb.Gerar(nome, pivo);
    }

    // paredes do buraco: borda no raio RAIO_BURACO (cola no piso), desce em rampa irregular até o fundo e termina num colar em volta do poço
    static Mesh MalhaDaCratera(float topo, int sub, int subParede)
    {
        const int NR = 7, NS = 64;
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        for (int j = 0; j <= NR; j++)
            for (int i = 0; i <= NS; i++)
            {
                float a = i * Mathf.PI * 2f / NS, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float rad, y;
                if (j < NR)
                {
                    float t = j / (float)(NR - 1);
                    rad = Mathf.Lerp(RAIO_BURACO, RAIO_BORDA_FUNDO, t);
                    y = topo + 0.02f - (PROF + 0.02f) * Mathf.Pow(t, 1.5f);
                    if (j > 0 && j < NR - 1)
                    {
                        rad += (Mathf.PerlinNoise(ca * 2f + 5f + j * 0.7f, sa * 2f + 5f) - 0.5f) * 1.1f;
                        y += (Mathf.PerlinNoise(ca * 2.6f + 31f, sa * 2.6f + 31f + j * 1.3f) - 0.5f) * 0.45f;
                    }
                }
                else { rad = RAIO_FUNDO; y = topo - PROF; }
                Vector3 p = new Vector3(ca * rad, y, sa * rad);
                v.Add(p); uv.Add(new Vector2(p.x, p.z));
            }
        // sentido dos triângulos: o de cima precisa olhar para cima
        Vector3 q0 = v[1 * (NS + 1)], q1 = v[2 * (NS + 1)], q2 = v[2 * (NS + 1) + 1];
        bool inverte = Vector3.Cross(q1 - q0, q2 - q0).y < 0f;
        for (int j = 0; j < NR; j++)
            for (int i = 0; i < NS; i++)
            {
                int a = j * (NS + 1) + i, b = (j + 1) * (NS + 1) + i, c = (j + 1) * (NS + 1) + i + 1, d = j * (NS + 1) + i + 1;
                if (!inverte) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(d); }
                else { tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(a); tri.Add(d); tri.Add(c); }
            }
        // colar em volta do poço (olha para dentro)
        int baseIdx = v.Count;
        for (int i = 0; i <= NS; i++)
        {
            float a = i * Mathf.PI * 2f / NS;
            Vector3 c0 = new Vector3(Mathf.Cos(a) * RAIO_FUNDO, topo - PROF, Mathf.Sin(a) * RAIO_FUNDO);
            v.Add(c0); uv.Add(new Vector2(a * RAIO_FUNDO, c0.y));
            v.Add(c0 + Vector3.down * 2.5f); uv.Add(new Vector2(a * RAIO_FUNDO, c0.y - 2.5f));
        }
        for (int i = 0; i < NS; i++)
        {
            int a = baseIdx + i * 2, b = a + 1, c = a + 3, d = a + 2;
            float am = (i + 0.5f) * Mathf.PI * 2f / NS;
            Vector3 paraDentro = -new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));
            bool ok = Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), paraDentro) > 0f;
            if (ok) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(d); }
            else { tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(a); tri.Add(d); tri.Add(c); }
        }
        Mesh m = new Mesh { name = "Buraco_paredes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v); m.SetUVs(0, uv);
        m.subMeshCount = sub;
        for (int s = 0; s < sub; s++) m.SetTriangles(s == subParede ? tri : new List<int>(), s);
        m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    static Material CriarMaterialBrilho()
    {
        string caminho = PASTA + "/Buraco_brilho.mat";
        AssetDatabase.DeleteAsset(caminho);
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(sh);
        m.SetColor("_BaseColor", new Color(0.05f, 0.02f, 0.01f));
        m.SetColor("_EmissionColor", new Color(1f, 0.32f, 0.07f) * 2.4f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(m, caminho);
        return AssetDatabase.LoadAssetAtPath<Material>(caminho);
    }

    static Material CriarMaterialParticula(string nome, float src, float dst, float ambiente)
    {
        string caminho = PASTA + "/" + nome + ".mat";
        Shader sh = AssetDatabase.LoadAssetAtPath<Shader>(PASTA + "/PingluPoeira.shader");
        if (sh == null) sh = Shader.Find("Pinglu/Poeira");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, caminho); }
        m.shader = sh;
        m.SetFloat("_Src", src); m.SetFloat("_Dst", dst); m.SetFloat("_Ambiente", ambiente);
        m.SetColor("_Cor", nome == "Brasa" ? new Color(1.8f, 1.8f, 1.8f, 1f) : Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    // fecha (fechar=false) ou abre (abrir=true) o terreno em volta do ponto; as bordas ficam escondidas debaixo do piso da praça
    static string AbrirBuracoNoTerreno(Vector3 c, bool abrir)
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null) return "Terreno não encontrado (o buraco ficou só na praça).";
        TerrainData td = t.terrainData;
        int hr = td.holesResolution;
        float cx = td.size.x / hr, cz = td.size.z / hr;
        Vector3 rel = c - t.transform.position;
        float R = RAIO_BURACO + 1.8f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((rel.x - R) / cx) - 1), x1 = Mathf.Min(hr - 1, Mathf.CeilToInt((rel.x + R) / cx) + 1);
        int z0 = Mathf.Max(0, Mathf.FloorToInt((rel.z - R) / cz) - 1), z1 = Mathf.Min(hr - 1, Mathf.CeilToInt((rel.z + R) / cz) + 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        bool[,] holes = td.GetHoles(x0, z0, w, h);
        int abertas = 0;
        for (int zz = 0; zz < h; zz++)
            for (int xx = 0; xx < w; xx++)
            {
                float px = (x0 + xx + 0.5f) * cx, pz = (z0 + zz + 0.5f) * cz;
                float d = Mathf.Sqrt((px - rel.x) * (px - rel.x) + (pz - rel.z) * (pz - rel.z));
                if (d < RAIO_BURACO + Mathf.Max(cx, cz) * 0.75f) { holes[zz, xx] = !abrir; if (abrir) abertas++; }
            }
        td.SetHoles(x0, z0, holes);
        EditorUtility.SetDirty(td);
        return abrir ? "Terreno: " + abertas + " células abertas (cada uma com " + cx.ToString("0.0") + " m)." : "Terreno fechado.";
    }
}
#endif
