// =====================================================================
//  PingluGuardiao.cs  -  prepara o chefe "Guardião das Lápides" (o golem de pedra do Thiago)
// ---------------------------------------------------------------------
//  O arquivo original (.glb) tem 2 MILHÕES de triângulos, 125 MB e nenhum esqueleto: pesado demais para o jogo e sem animação.
//  Este menu:
//   1) lê o .glb direto do arquivo e cria uma versão LEVE (~60 mil triângulos) mantendo as texturas
//   2) cria um esqueleto de 16 ossos (braços, pernas, tronco, cabeça) e distribui o "peso" de cada pedaço do corpo
//   3) monta o molde (prefab) com vida, colisor e o código do chefe (ChefeGuardiao), e liga ao gerenciador de ondas do cemitério
//
//  COMO USAR:  Pinglu > 64. Chefe: preparar o Guardião das Lápides   (Play desligado; demora um pouco)
//  Para ver o chefe agora:  Play e depois Pinglu > 65. Chefe: testar o Guardião
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluGuardiao
{
    const string GLB = "Assets/Personagens/Inimigos/BOSS/gravestone_golem_-_cemetery_guardian.glb";
    const string PASTA = "Assets/Personagens/Inimigos/BOSS/Guardiao";
    const string PREFAB = PASTA + "/Guardiao_Lapides.prefab";
    const float ALTURA_FINAL = 5.6f;      // metros

    // ---------- classes para ler o JSON do glTF
    [System.Serializable] class GAttr { public int POSITION = -1, NORMAL = -1, TEXCOORD_0 = -1; }
    [System.Serializable] class GPrim { public GAttr attributes; public int indices = -1, mode = 4, material = 0; }
    [System.Serializable] class GMesh { public GPrim[] primitives; }
    [System.Serializable] class GNode { public int[] children; public int mesh = -1; public float[] matrix, translation, rotation, scale; }
    [System.Serializable] class GAcc { public int bufferView, byteOffset, componentType, count; public string type; }
    [System.Serializable] class GView { public int byteOffset, byteLength, byteStride; }
    [System.Serializable] class GImg { public int bufferView = -1; public string mimeType, uri; }
    [System.Serializable] class GTex { public int source; }
    [System.Serializable] class GRef { public int index = -1; }
    [System.Serializable] class GPbr { public GRef baseColorTexture; }
    [System.Serializable] class GMat { public GPbr pbrMetallicRoughness; public GRef normalTexture; }
    [System.Serializable] class GRoot { public GNode[] nodes; public GMesh[] meshes; public GAcc[] accessors; public GView[] bufferViews; public GImg[] images; public GTex[] textures; public GMat[] materials; public int[] scenes; }

    static byte[] arq; static int binIni; static GRoot raiz;

    // ---------- esqueleto: nome, pai, início, fim, raio (em unidades do modelo original, altura total = 0.978)
    class Osso { public string nome; public int pai; public Vector3 a, b; public float raio; }

    static List<Osso> Esqueleto()
    {
        float xc = -0.02f;                       // o corpo não está exatamente no meio do arquivo
        List<Osso> l = new List<Osso>();
        System.Action<string, int, Vector3, Vector3, float> add = (n, p, a, b, r) => l.Add(new Osso { nome = n, pai = p, a = a, b = b, raio = r });
        add("Pelvis", -1, new Vector3(xc, 0.47f, 0f), new Vector3(xc, 0.56f, 0f), 0.18f);                //0
        add("Spine", 0, new Vector3(xc, 0.56f, 0f), new Vector3(xc, 0.68f, 0f), 0.2f);                   //1
        add("Chest", 1, new Vector3(xc, 0.68f, 0f), new Vector3(xc, 0.84f, 0f), 0.21f);                  //2
        add("Head", 2, new Vector3(xc, 0.84f, 0f), new Vector3(xc, 0.975f, 0f), 0.12f);                  //3
        foreach (int s in new[] { 1, -1 })
        {
            string d = s > 0 ? "_D" : "_E";
            int ib = l.Count;
            add("BracoSup" + d, 2, new Vector3(xc + s * 0.225f, 0.82f, 0f), new Vector3(xc + s * 0.325f, 0.575f, 0f), 0.095f);
            add("Antebraco" + d, ib, new Vector3(xc + s * 0.325f, 0.575f, 0f), new Vector3(xc + s * 0.36f, 0.4f, 0f), 0.085f);
            add("Mao" + d, ib + 1, new Vector3(xc + s * 0.36f, 0.4f, 0f), new Vector3(xc + s * 0.36f, 0.27f, 0.02f), 0.075f);
        }
        foreach (int s in new[] { 1, -1 })
        {
            string d = s > 0 ? "_D" : "_E";
            int ib = l.Count;
            add("Coxa" + d, 0, new Vector3(xc + s * 0.11f, 0.43f, 0f), new Vector3(xc + s * 0.15f, 0.21f, 0f), 0.1f);
            add("Canela" + d, ib, new Vector3(xc + s * 0.15f, 0.21f, 0f), new Vector3(xc + s * 0.15f, 0.05f, 0f), 0.09f);
            add("Pe" + d, ib + 1, new Vector3(xc + s * 0.15f, 0.05f, 0f), new Vector3(xc + s * 0.15f, 0.015f, 0.12f), 0.09f);
        }
        return l;
    }

    [MenuItem("Pinglu/64. Chefe: preparar o Guardião das Lápides")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (!File.Exists(GLB)) { EditorUtility.DisplayDialog("Pinglu", "Não achei o arquivo do chefe:\n" + GLB, "OK"); return; }
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Lendo o modelo (125 MB)...", 0.05f);
            if (!AssetDatabase.IsValidFolder(PASTA)) { AssetDatabase.CreateFolder("Assets/Personagens/Inimigos/BOSS", "Guardiao"); }
            LerArquivo();

            // ---- junta as 20 peças numa lista de vértices (já em coordenadas do Unity)
            EditorUtility.DisplayProgressBar("Pinglu", "Juntando as peças...", 0.15f);
            List<Vector3> P = new List<Vector3>(), N = new List<Vector3>(); List<Vector2> UV = new List<Vector2>(); List<int> T = new List<int>();
            Matrix4x4[] mundo = new Matrix4x4[raiz.nodes.Length];
            bool[] visto = new bool[raiz.nodes.Length];
            for (int i = 0; i < raiz.nodes.Length; i++) if (!TemPai(i)) Percorrer(i, Matrix4x4.identity, mundo, visto);
            for (int i = 0; i < raiz.nodes.Length; i++)
            {
                GNode nd = raiz.nodes[i]; if (nd.mesh < 0) continue;
                foreach (GPrim pr in raiz.meshes[nd.mesh].primitives)
                {
                    if (pr.mode != 4) continue;
                    Vector3[] p = LerV3(pr.attributes.POSITION), n = pr.attributes.NORMAL >= 0 ? LerV3(pr.attributes.NORMAL) : null;
                    Vector2[] uv = pr.attributes.TEXCOORD_0 >= 0 ? LerV2(pr.attributes.TEXCOORD_0) : null;
                    int[] idx = LerIdx(pr.indices);
                    int b0 = P.Count; Matrix4x4 m = mundo[i];
                    for (int k = 0; k < p.Length; k++)
                    {
                        Vector3 w = m.MultiplyPoint3x4(p[k]); P.Add(new Vector3(-w.x, w.y, w.z));
                        Vector3 nn = n != null ? m.MultiplyVector(n[k]).normalized : Vector3.up; N.Add(new Vector3(-nn.x, nn.y, nn.z));
                        UV.Add(uv != null ? uv[k] : Vector2.zero);
                    }
                    for (int k = 0; k + 2 < idx.Length; k += 3) { T.Add(b0 + idx[k]); T.Add(b0 + idx[k + 2]); T.Add(b0 + idx[k + 1]); }     // espelhou o X: inverte a ordem
                }
            }
            Debug.Log("[Guardião] original: " + P.Count + " vértices, " + (T.Count / 3) + " triângulos.");

            // ---- versão leve: agrupa vértices vizinhos (sem misturar pedaços de textura diferentes)
            EditorUtility.DisplayProgressBar("Pinglu", "Fazendo a versão leve...", 0.4f);
            List<Vector3> P2 = null, N2 = null; List<Vector2> U2 = null; List<int> T2 = null; List<Color32> C2 = null;
            foreach (float cel in new[] { 0.0030f, 0.0038f, 0.0050f, 0.0065f, 0.0085f, 0.011f, 0.015f })
            {
                Simplificar(P, N, UV, T, CoresOriginais(UV), cel, out P2, out N2, out U2, out T2, out C2);
                Debug.Log("[Guardião] célula " + cel + " -> " + P2.Count + " vértices, " + (T2.Count / 3) + " triângulos.");
                if (T2.Count / 3 <= 85000) break;
            }
            float esc = ALTURA_FINAL / 0.978f;

            // ---- esqueleto e pesos
            EditorUtility.DisplayProgressBar("Pinglu", "Criando o esqueleto...", 0.6f);
            List<Osso> osso = Esqueleto(); int nb = osso.Count;
            GameObject raizGo = new GameObject("Guardiao_Lapides");
            GameObject esq = new GameObject("Esqueleto"); esq.transform.SetParent(raizGo.transform, false);
            Transform[] tr = new Transform[nb];
            for (int i = 0; i < nb; i++)
            {
                GameObject g = new GameObject(osso[i].nome);
                Transform pai = osso[i].pai < 0 ? esq.transform : tr[osso[i].pai];
                g.transform.SetParent(pai, false);
                g.transform.position = osso[i].a * esc;
                tr[i] = g.transform;
            }
            Vector3[] pos = new Vector3[P2.Count]; BoneWeight[] bw = new BoneWeight[P2.Count];
            for (int v = 0; v < P2.Count; v++)
            {
                pos[v] = P2[v] * esc;
                float[] w = new float[nb]; int a1 = -1, a2 = -1, a3 = -1, a4 = -1;
                for (int i = 0; i < nb; i++)
                {
                    float d = Mathf.Max(0f, DistSeg(P2[v], osso[i].a, osso[i].b) - osso[i].raio) + 0.004f;
                    w[i] = 1f / (d * d * d * d);
                }
                int[] top = new int[4] { -1, -1, -1, -1 };
                for (int k = 0; k < 4; k++) { float m = -1f; for (int i = 0; i < nb; i++) { if (System.Array.IndexOf(top, i) >= 0) continue; if (w[i] > m) { m = w[i]; top[k] = i; } } }
                float soma = w[top[0]] + w[top[1]] + w[top[2]] + w[top[3]];
                a1 = top[0]; a2 = top[1]; a3 = top[2]; a4 = top[3];
                bw[v] = new BoneWeight { boneIndex0 = a1, boneIndex1 = a2, boneIndex2 = a3, boneIndex3 = a4, weight0 = w[a1] / soma, weight1 = w[a2] / soma, weight2 = w[a3] / soma, weight3 = w[a4] / soma };
            }
            Matrix4x4[] bind = new Matrix4x4[nb];
            for (int i = 0; i < nb; i++) bind[i] = tr[i].worldToLocalMatrix * raizGo.transform.localToWorldMatrix;

            Mesh mesh = new Mesh { name = "Guardiao_Lapides_Leve" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = pos; mesh.normals = N2.ToArray(); mesh.uv = U2.ToArray(); mesh.triangles = T2.ToArray();
            mesh.colors32 = C2.ToArray();
            mesh.boneWeights = bw; mesh.bindposes = bind;
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            string pm = PASTA + "/Guardiao_Lapides_Leve.asset";
            AssetDatabase.DeleteAsset(pm); AssetDatabase.CreateAsset(mesh, pm);

            // ---- texturas e material
            EditorUtility.DisplayProgressBar("Pinglu", "Texturas e material...", 0.8f);
            Material mat = CriarMaterial();

            GameObject skin = new GameObject("Skin"); skin.transform.SetParent(raizGo.transform, false);
            SkinnedMeshRenderer smr = skin.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh; smr.sharedMaterial = mat; smr.bones = tr; smr.rootBone = tr[0];
            smr.localBounds = new Bounds(new Vector3(0f, ALTURA_FINAL * 0.5f, 0f), new Vector3(8f, ALTURA_FINAL + 1f, 8f));
            smr.updateWhenOffscreen = false;

            CharacterController cc = raizGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, ALTURA_FINAL * 0.5f, 0f); cc.height = ALTURA_FINAL - 0.2f; cc.radius = 1.15f; cc.stepOffset = 0.6f;
            Vida vida = raizGo.AddComponent<Vida>();
            SerializedObject sv = new SerializedObject(vida); sv.FindProperty("vidaMaxima").floatValue = 1800f; sv.FindProperty("invulneravelAposDano").floatValue = 0.08f; sv.ApplyModifiedPropertiesWithoutUndo();
            raizGo.AddComponent<ChefeGuardiao>();

            AssetDatabase.DeleteAsset(PREFAB);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raizGo, PREFAB);
            Object.DestroyImmediate(raizGo);
            AssetDatabase.SaveAssets();

            // ---- liga ao gerenciador de ondas do cemitério
            string ligou = "Molde criado, mas não achei o gerenciador de ondas na cena.";
            GerenciadorDeOndas g2 = Object.FindFirstObjectByType<GerenciadorDeOndas>();
            if (g2 != null)
            {
                SerializedObject so = new SerializedObject(g2);
                so.FindProperty("prefabChefe").objectReferenceValue = prefab;
                GameObject cem = GameObject.Find("Cemiterio");
                if (cem != null) so.FindProperty("centroDaArena").objectReferenceValue = cem.transform;
                so.FindProperty("ondasAntesDoChefe").intValue = 5;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(g2.gameObject.scene); EditorSceneManager.SaveOpenScenes();
                ligou = "Ligado às ondas: 5 ondas e, na 6ª, o Guardião" + (cem != null ? " sai do centro do cemitério." : " (centro da arena: usei o próprio gerenciador, não achei o objeto Cemiterio).");
            }
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Pinglu", "Guardião das Lápides pronto!\n\n- Versão leve: " + (T2.Count / 3) + " triângulos (o original tinha " + (T.Count / 3) + ")\n- 16 ossos, andar, golpe, pisão, surgir e morrer\n- " + ligou +
                "\n\nPara ver agora: aperte Play e use Pinglu > 65. Chefe: testar o Guardião.", "OK");
        }
        catch (System.Exception e) { Debug.LogException(e); EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("Pinglu", "Deu um erro: " + e.Message + "\n(veja o Console)", "OK"); }
        finally { arq = null; raiz = null; coresOrig = null; coresOrigUV = null; EditorUtility.ClearProgressBar(); }
    }

    [MenuItem("Pinglu/65. Chefe: testar o Guardião (durante o Play)")]
    public static void Testar()
    {
        if (!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Aperte Play primeiro e depois use este menu.", "OK"); return; }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
        PlayerLocomotion p = Object.FindFirstObjectByType<PlayerLocomotion>();
        if (prefab == null || p == null) { Debug.LogWarning("[Guardião] sem molde (rode o 64) ou sem jogador."); return; }
        Vector3 pos = p.transform.position + p.transform.forward * 16f;
        Terrain t = Terrain.activeTerrain; if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y;
        GameObject go = Object.Instantiate(prefab, pos, Quaternion.LookRotation(-p.transform.forward));
        ChefeGuardiao c = go.GetComponent<ChefeGuardiao>(); c.Vida.DefinirMaxima(c.vidaBase); c.Surgir(6f, 7f);
    }

    // ============================================================ leitura do .glb
    static bool TemPai(int i) { foreach (GNode n in raiz.nodes) if (n.children != null && System.Array.IndexOf(n.children, i) >= 0) return true; return false; }

    static void Percorrer(int i, Matrix4x4 pai, Matrix4x4[] mundo, bool[] visto)
    {
        GNode n = raiz.nodes[i]; Matrix4x4 local = Matrix4x4.identity;
        if (n.matrix != null && n.matrix.Length == 16)
            local = new Matrix4x4(new Vector4(n.matrix[0], n.matrix[1], n.matrix[2], n.matrix[3]), new Vector4(n.matrix[4], n.matrix[5], n.matrix[6], n.matrix[7]),
                                  new Vector4(n.matrix[8], n.matrix[9], n.matrix[10], n.matrix[11]), new Vector4(n.matrix[12], n.matrix[13], n.matrix[14], n.matrix[15]));
        else
        {
            Vector3 t = n.translation != null && n.translation.Length == 3 ? new Vector3(n.translation[0], n.translation[1], n.translation[2]) : Vector3.zero;
            Quaternion q = n.rotation != null && n.rotation.Length == 4 ? new Quaternion(n.rotation[0], n.rotation[1], n.rotation[2], n.rotation[3]) : Quaternion.identity;
            Vector3 s = n.scale != null && n.scale.Length == 3 ? new Vector3(n.scale[0], n.scale[1], n.scale[2]) : Vector3.one;
            local = Matrix4x4.TRS(t, q, s);
        }
        mundo[i] = pai * local; visto[i] = true;
        if (n.children != null) foreach (int c in n.children) Percorrer(c, mundo[i], mundo, visto);
    }

    static string json;

    static void LerArquivo()
    {
        arq = File.ReadAllBytes(GLB);
        int tam = System.BitConverter.ToInt32(arq, 12);
        json = System.Text.Encoding.UTF8.GetString(arq, 20, tam);
        binIni = 20 + tam + 8;
        raiz = JsonUtility.FromJson<GRoot>(json);
        if (raiz == null || raiz.nodes == null) throw new System.Exception("Não consegui ler o JSON do .glb.");
    }

    static int Base(GAcc a, out int stride, int tamElem)
    {
        GView v = raiz.bufferViews[a.bufferView];
        stride = v.byteStride > 0 ? v.byteStride : tamElem;
        return binIni + v.byteOffset + a.byteOffset;
    }

    static Vector3[] LerV3(int acc)
    {
        GAcc a = raiz.accessors[acc]; if (a.componentType != 5126) throw new System.Exception("Formato de vértice não suportado (" + a.componentType + ").");
        int stride; int off = Base(a, out stride, 12); Vector3[] r = new Vector3[a.count];
        for (int i = 0; i < a.count; i++) { int o = off + i * stride; r[i] = new Vector3(System.BitConverter.ToSingle(arq, o), System.BitConverter.ToSingle(arq, o + 4), System.BitConverter.ToSingle(arq, o + 8)); }
        return r;
    }

    static Vector2[] LerV2(int acc)
    {
        GAcc a = raiz.accessors[acc]; if (a.componentType != 5126) throw new System.Exception("Formato de UV não suportado.");
        int stride; int off = Base(a, out stride, 8); Vector2[] r = new Vector2[a.count];
        for (int i = 0; i < a.count; i++) { int o = off + i * stride; r[i] = new Vector2(System.BitConverter.ToSingle(arq, o), 1f - System.BitConverter.ToSingle(arq, o + 4)); }      // glTF tem o V invertido em relação ao Unity
        return r;
    }

    static int[] LerIdx(int acc)
    {
        GAcc a = raiz.accessors[acc]; int tam = a.componentType == 5125 ? 4 : (a.componentType == 5123 ? 2 : 1);
        int stride; int off = Base(a, out stride, tam); int[] r = new int[a.count];
        for (int i = 0; i < a.count; i++) { int o = off + i * stride; r[i] = tam == 4 ? (int)System.BitConverter.ToUInt32(arq, o) : (tam == 2 ? System.BitConverter.ToUInt16(arq, o) : arq[o]); }
        return r;
    }

    // ============================================================ versão leve
    // sem emendas: junta os vértices só pela posição (célula 3D), então o modelo leve fica fechado, sem frestas.
    // A cor de cada vértice novo é a média das cores dos vértices originais que caíram na célula.
    static void Simplificar(List<Vector3> P, List<Vector3> N, List<Vector2> UV, List<int> T, Color32[] cores, float cel, out List<Vector3> P2, out List<Vector3> N2, out List<Vector2> U2, out List<int> T2, out List<Color32> C2)
    {
        Dictionary<long, int> mapa = new Dictionary<long, int>(); int[] remap = new int[P.Count];
        List<Vector3> sp = new List<Vector3>(), sn = new List<Vector3>(); List<Vector2> su = new List<Vector2>(); List<Vector3> sc = new List<Vector3>(); List<int> cnt = new List<int>();
        for (int v = 0; v < P.Count; v++)
        {
            Vector3 p = P[v];
            long ix = Mathf.FloorToInt(p.x / cel) + 4096, iy = Mathf.Max(0, Mathf.FloorToInt(p.y / cel)), iz = Mathf.FloorToInt(p.z / cel) + 4096;
            long key = ((ix * 8192L) + iy) * 8192L + iz;
            int id;
            if (!mapa.TryGetValue(key, out id)) { id = sp.Count; mapa[key] = id; sp.Add(Vector3.zero); sn.Add(Vector3.zero); su.Add(Vector2.zero); sc.Add(Vector3.zero); cnt.Add(0); }
            sp[id] += p; sn[id] += N[v]; su[id] += UV[v]; sc[id] += new Vector3(cores[v].r, cores[v].g, cores[v].b); cnt[id]++; remap[v] = id;
        }
        P2 = new List<Vector3>(sp.Count); N2 = new List<Vector3>(sp.Count); U2 = new List<Vector2>(sp.Count); C2 = new List<Color32>(sp.Count);
        for (int i = 0; i < sp.Count; i++)
        {
            P2.Add(sp[i] / cnt[i]); N2.Add(sn[i].sqrMagnitude > 1e-8f ? sn[i].normalized : Vector3.up); U2.Add(su[i] / cnt[i]);
            Vector3 c = sc[i] / cnt[i]; C2.Add(new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(c.x), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.y), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.z), 0, 255), 255));
        }
        T2 = new List<int>(T.Count / 6);
        for (int i = 0; i + 2 < T.Count; i += 3)
        {
            int a = remap[T[i]], b = remap[T[i + 1]], c = remap[T[i + 2]];
            if (a == b || b == c || a == c) continue;
            T2.Add(a); T2.Add(b); T2.Add(c);
        }
    }

    static Color32[] coresOrig; static List<Vector2> coresOrigUV;
    // cor da textura no UV de cada vértice original (uma amostra; a média da célula suaviza)
    static Color32[] CoresOriginais(List<Vector2> uv)
    {
        if (coresOrig != null && coresOrigUV == uv) return coresOrig;
        int idxCor = IndiceDeTextura(true);
        Color32[] r = new Color32[uv.Count];
        if (idxCor < 0) { for (int i = 0; i < r.Length; i++) r[i] = new Color32(150, 150, 150, 255); }
        else
        {
            GImg gi = raiz.images[idxCor]; GView v = raiz.bufferViews[gi.bufferView];
            byte[] dados = new byte[v.byteLength]; System.Array.Copy(arq, binIni + v.byteOffset, dados, 0, v.byteLength);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(dados);
            for (int i = 0; i < r.Length; i++) { Color c = tex.GetPixelBilinear(Mathf.Repeat(uv[i].x, 1f), Mathf.Repeat(uv[i].y, 1f)); r[i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255); }
            Object.DestroyImmediate(tex);
        }
        coresOrig = r; coresOrigUV = uv; return r;
    }

    static float DistSeg(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a; float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return (a + ab * t - p).magnitude;
    }

    // ============================================================ material
    static Material CriarMaterial()
    {
        string pm = PASTA + "/Guardiao_Lapides.mat";
        AssetDatabase.DeleteAsset(pm);
        Shader sh = AssetDatabase.LoadAssetAtPath<Shader>(PASTA + "/PingluGolem.shader");
        if (sh == null) sh = Shader.Find("Pinglu/Golem");
        Material m = new Material(sh);
        AssetDatabase.CreateAsset(m, pm);
        m.SetFloat("_Brilho", 1.3f); m.SetFloat("_Rachar", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static int IndiceDeTextura(bool cor)
    {
        if (raiz.materials == null || raiz.materials.Length == 0) return -1;
        GMat gm = raiz.materials[0];
        string chave = cor ? "baseColorTexture" : "normalTexture";
        if (!json.Contains(chave)) return -1;
        GRef r = cor ? (gm.pbrMetallicRoughness != null ? gm.pbrMetallicRoughness.baseColorTexture : null) : gm.normalTexture;
        if (r == null || r.index < 0) return -1;
        return raiz.textures[r.index].source;
    }

    static Texture2D SalvarImagem(int imagem, string nome, bool normal)
    {
        GImg gi = raiz.images[imagem]; if (gi.bufferView < 0) return null;
        GView v = raiz.bufferViews[gi.bufferView];
        byte[] dados = new byte[v.byteLength]; System.Array.Copy(arq, binIni + v.byteOffset, dados, 0, v.byteLength);
        string ext = gi.mimeType != null && gi.mimeType.Contains("png") ? ".png" : ".jpg";
        string cam = PASTA + "/" + nome + ext;
        File.WriteAllBytes(cam, dados);
        AssetDatabase.ImportAsset(cam, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = AssetImporter.GetAtPath(cam) as TextureImporter;
        if (ti != null) { ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.anisoLevel = 8; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(cam);
    }
}
#endif
