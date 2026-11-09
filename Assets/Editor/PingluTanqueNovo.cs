// =====================================================================
//  PingluTanqueNovo.cs  -  skin nova do Tanque (auric_nemesis__the_gilded_abyss.glb)
// ---------------------------------------------------------------------
//  O .glb não tem esqueleto nem animações e tem ~1,4 milhão de triângulos.
//  Este menu:
//   1. junta as 22 malhas numa só e reduz os triângulos (agrupando vértices),
//      com os pés no chão e ~1,9 m de altura
//   2. cria o prefab "Tanque_Auric" com um esqueleto simples (pélvis, torso, cabeça, braços, pernas, capa) e a animação por código
//   3. coloca a skin no Jogador_Tanque (o corpo antigo fica escondido, mas continua
//      rodando por baixo: escudo, voo e animator seguem funcionando)
//   4. troca o modelo da ficha do Tanque na tela de seleção
//
//  COMO USAR:  Pinglu > 42. Tanque - skin nova      (Play desligado, depois Ctrl+S)
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PingluTanqueNovo
{
    const string PASTA = "Assets/Personagens/novo Tanque";
    const string GLB = PASTA + "/auric_nemesis__the_gilded_abyss.glb";
    const string MALHA = PASTA + "/Tanque_Auric_Malha.asset";
    const string PREFAB_SKIN = PASTA + "/Tanque_Auric.prefab";
    const string PREFAB_JOGADOR = "Assets/Personagens/Jogadores/Jogador_Tanque.prefab";
    const string FICHA = "Assets/Personagens/Jogadores/Fichas/3_Personagem3.asset";
    const float ALTURA = 1.9f;          // altura final, em metros
    const float GIRO_Y = 0f;            // se ele ficar de costas, troque por 180
    const int ALVO_TRIANGULOS = 170000;

    [MenuItem("Pinglu/42. Tanque - skin nova")]
    public static void Montar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        GameObject glb = AssetDatabase.LoadAssetAtPath<GameObject>(GLB);
        if (glb == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei " + GLB + ".\nSe veio do git, rode: git lfs pull", "OK"); return; }
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Reduzindo a malha (1,4 milhão de triângulos)...", 0.2f);
            Material mat; Mesh malha = ReduzirMalha(glb, out mat);
            if (malha == null) return;
            EditorUtility.DisplayProgressBar("Pinglu", "Criando o prefab da skin...", 0.6f);
            GameObject skin = CriarPrefabDaSkin(malha, mat);
            EditorUtility.DisplayProgressBar("Pinglu", "Colocando no Jogador_Tanque e na seleção...", 0.8f);
            string rel = ColocarNoJogador(skin);
            FichaDaSelecao(skin);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Pinglu", "Skin nova do Tanque pronta!\n\nMalha: " + malha.triangles.Length / 3 + " triângulos, altura " + ALTURA + " m.\n" + rel +
                "\n\nComo o modelo não tem esqueleto, ele se move por código (balança, inclina, flutua, avança no ataque...).\nSe ele ficar de costas, troque GIRO_Y por 180 em PingluTanqueNovo.cs e rode de novo.\n\nSalve a cena com Ctrl + S.", "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ------------------------------------------------------------------
    //  junta e reduz: agrupa vértices numa grade 3D e fica com um por célula
    // ------------------------------------------------------------------
    static Mesh ReduzirMalha(GameObject glb, out Material material)
    {
        material = null;
        GameObject tmp = (GameObject)PrefabUtility.InstantiatePrefab(glb);
        tmp.transform.position = Vector3.zero; tmp.transform.rotation = Quaternion.identity; tmp.transform.localScale = Vector3.one;
        List<Vector3> pos = new List<Vector3>(), nor = new List<Vector3>();
        List<Vector4> tan = new List<Vector4>();
        List<Vector2>[] uv = { new List<Vector2>(), new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
        List<int> tri = new List<int>();
        Matrix4x4 mundo2raiz = tmp.transform.worldToLocalMatrix;
        foreach (MeshFilter mf in tmp.GetComponentsInChildren<MeshFilter>())
        {
            Mesh m = mf.sharedMesh; if (m == null) continue;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (material == null && mr != null) material = mr.sharedMaterial;
            Matrix4x4 M = mundo2raiz * mf.transform.localToWorldMatrix;
            bool inverte = M.determinant < 0f;
            int ini = pos.Count;
            Vector3[] v = m.vertices, n = m.normals; Vector4[] tg = m.tangents;
            if (v.Length == 0) { Debug.LogWarning("[TanqueNovo] malha ilegível: " + m.name + " (marque Read/Write no .glb)"); continue; }
            for (int i = 0; i < v.Length; i++)
            {
                pos.Add(M.MultiplyPoint3x4(v[i]));
                nor.Add(n != null && n.Length == v.Length ? M.MultiplyVector(n[i]).normalized : Vector3.up);
                if (tg != null && tg.Length == v.Length) { Vector3 d = M.MultiplyVector(tg[i]).normalized; tan.Add(new Vector4(d.x, d.y, d.z, tg[i].w)); }
            }
            for (int c = 0; c < 4; c++)
            {
                List<Vector2> u = new List<Vector2>(); m.GetUVs(c, u);
                if (u.Count == v.Length) uv[c].AddRange(u); else for (int i = 0; i < v.Length; i++) uv[c].Add(Vector2.zero);
            }
            int[] ti = m.triangles;
            for (int i = 0; i + 2 < ti.Length; i += 3)
            {
                tri.Add(ini + ti[i]); tri.Add(ini + (inverte ? ti[i + 2] : ti[i + 1])); tri.Add(ini + (inverte ? ti[i + 1] : ti[i + 2]));
            }
        }
        Object.DestroyImmediate(tmp);
        if (pos.Count == 0) { EditorUtility.DisplayDialog("Pinglu", "Não consegui ler as malhas do .glb. Selecione o arquivo, marque \"Read/Write\" (se houver) e rode de novo.", "OK"); return null; }
        bool temTan = tan.Count == pos.Count;

        // o modelo vem deitado: o eixo mais comprido vira o "para cima", com a ponta mais larga (pés/capa) embaixo
        Bounds b0 = new Bounds(pos[0], Vector3.zero);
        foreach (Vector3 p in pos) b0.Encapsulate(p);
        int eixo = b0.size.x >= b0.size.y && b0.size.x >= b0.size.z ? 0 : (b0.size.y >= b0.size.z ? 1 : 2);
        float comp = b0.size[eixo], faixa = comp * 0.12f;
        int o1 = (eixo + 1) % 3, o2 = (eixo + 2) % 3;
        Vector2 baixoMin = new Vector2(1e9f, 1e9f), baixoMax = -baixoMin, cimaMin = baixoMin, cimaMax = -baixoMin;
        foreach (Vector3 p in pos)
        {
            float h = p[eixo] - b0.min[eixo];
            Vector2 q2 = new Vector2(p[o1], p[o2]);
            if (h < faixa) { baixoMin = Vector2.Min(baixoMin, q2); baixoMax = Vector2.Max(baixoMax, q2); }
            else if (h > comp - faixa) { cimaMin = Vector2.Min(cimaMin, q2); cimaMax = Vector2.Max(cimaMax, q2); }
        }
        Vector2 tb = baixoMax - baixoMin, tc = cimaMax - cimaMin;
        bool baseNoMinimo = tb.x * tb.y >= tc.x * tc.y;
        Vector3 dirEixo = Vector3.zero; dirEixo[eixo] = baseNoMinimo ? 1f : -1f; // aponta da base para a cabeça
        Quaternion levanta = Quaternion.Euler(0f, GIRO_Y, 0f) * Quaternion.FromToRotation(dirEixo, Vector3.up);
        Debug.Log("[TanqueNovo] eixo mais comprido = " + "XYZ"[eixo] + ", base no " + (baseNoMinimo ? "mínimo" : "máximo") + " (área " + (tb.x * tb.y).ToString("F2") + " x " + (tc.x * tc.y).ToString("F2") + ")");
        for (int i = 0; i < pos.Count; i++)
        {
            pos[i] = levanta * pos[i]; nor[i] = levanta * nor[i];
            if (temTan) { Vector3 d = levanta * new Vector3(tan[i].x, tan[i].y, tan[i].z); tan[i] = new Vector4(d.x, d.y, d.z, tan[i].w); }
        }

        // pés no chão, centrado e na altura certa
        Bounds b = new Bounds(pos[0], Vector3.zero);
        foreach (Vector3 p in pos) b.Encapsulate(p);
        float esc = ALTURA / Mathf.Max(0.01f, b.size.y);
        Vector3 origem = new Vector3(b.center.x, b.min.y, b.center.z);
        for (int i = 0; i < pos.Count; i++) pos[i] = (pos[i] - origem) * esc;
        Debug.Log("[TanqueNovo] original: " + pos.Count + " vértices, " + tri.Count / 3 + " triângulos, tamanho " + b.size.ToString("F2") + " -> escala " + esc.ToString("F3"));

        // agrupamento (aumenta a célula até caber no alvo)
        float cel = ALTURA / 230f;
        List<int> nova = null; int[] mapa = null; List<int> escolhidos = null;
        for (int tentativa = 0; tentativa < 8; tentativa++)
        {
            Dictionary<long, int> celulas = new Dictionary<long, int>();
            List<Vector3> soma = new List<Vector3>(); List<int> cont = new List<int>();
            mapa = new int[pos.Count];
            for (int i = 0; i < pos.Count; i++)
            {
                long kx = Mathf.FloorToInt(pos[i].x / cel) + 4096, ky = Mathf.FloorToInt(pos[i].y / cel) + 4096, kz = Mathf.FloorToInt(pos[i].z / cel) + 4096;
                long k = (kx << 40) | (ky << 20) | kz;
                int id;
                if (!celulas.TryGetValue(k, out id)) { id = soma.Count; celulas[k] = id; soma.Add(Vector3.zero); cont.Add(0); }
                soma[id] += pos[i]; cont[id]++; mapa[i] = id;
            }
            // representante: o vértice mais perto do centro da célula (mantém UV e normal reais)
            int[] rep = new int[soma.Count]; float[] melhor = new float[soma.Count];
            for (int c = 0; c < rep.Length; c++) { rep[c] = -1; melhor[c] = float.MaxValue; }
            for (int i = 0; i < pos.Count; i++)
            {
                int c = mapa[i]; float d = (pos[i] - soma[c] / cont[c]).sqrMagnitude;
                if (d < melhor[c]) { melhor[c] = d; rep[c] = i; }
            }
            escolhidos = new List<int>(rep);
            nova = new List<int>();
            HashSet<long> vistos = new HashSet<long>();
            for (int i = 0; i + 2 < tri.Count; i += 3)
            {
                int a = mapa[tri[i]], bb = mapa[tri[i + 1]], c2 = mapa[tri[i + 2]];
                if (a == bb || bb == c2 || a == c2) continue;
                // descarta triângulo repetido (mesmos três grupos)
                int mn = Mathf.Min(a, Mathf.Min(bb, c2)), mx = Mathf.Max(a, Mathf.Max(bb, c2)), md = a + bb + c2 - mn - mx;
                long chave = ((long)mn * 2097152L + md) * 2097152L + mx;
                if (!vistos.Add(chave)) continue;
                nova.Add(a); nova.Add(bb); nova.Add(c2);
            }
            Debug.Log("[TanqueNovo] célula " + cel.ToString("F4") + " m -> " + escolhidos.Count + " vértices, " + nova.Count / 3 + " triângulos");
            if (nova.Count / 3 <= ALVO_TRIANGULOS) break;
            cel *= 1.2f;
        }

        Mesh r = new Mesh { name = "Tanque_Auric_Malha", indexFormat = escolhidos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        List<Vector3> rp = new List<Vector3>(), rn = new List<Vector3>(); List<Vector4> rt = new List<Vector4>();
        List<Vector2>[] ru = { new List<Vector2>(), new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
        foreach (int i in escolhidos)
        {
            rp.Add(pos[i]); rn.Add(nor[i]); if (temTan) rt.Add(tan[i]);
            for (int c = 0; c < 4; c++) ru[c].Add(uv[c][i]);
        }
        r.SetVertices(rp); r.SetNormals(rn); if (temTan) r.SetTangents(rt);
        for (int c = 0; c < 4; c++) r.SetUVs(c, ru[c]);
        r.SetTriangles(nova, 0);
        r.RecalculateBounds();
        // atualiza o mesmo arquivo (apagar e recriar deixa os prefabs apontando para uma malha destruída)
        Mesh existente = AssetDatabase.LoadAssetAtPath<Mesh>(MALHA);
        if (existente != null) { existente.Clear(); EditorUtility.CopySerialized(r, existente); EditorUtility.SetDirty(existente); AssetDatabase.SaveAssets(); Object.DestroyImmediate(r); return existente; }
        AssetDatabase.CreateAsset(r, MALHA);
        return AssetDatabase.LoadAssetAtPath<Mesh>(MALHA);
    }

    static float Suave(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3f - 2f * t); }

    static Transform Osso(string nome, Transform pai, Vector3 posMundo)
    {
        Transform t = new GameObject("Auric_" + nome).transform;
        t.SetParent(pai, false);
        t.position = posMundo; // a raiz fica na origem, sem rotação: posição do mundo = local da raiz
        return t;
    }

    static float DistSeg(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    // esqueleto humanoide simples (mesmos nomes de ossos do Tanque antigo, com prefixo Auric_),
    // com pesos por distância ao osso
    static GameObject CriarPrefabDaSkin(Mesh malha, Material mat)
    {
        GameObject g = new GameObject("Tanque_Auric");
        Transform raiz = g.transform;
        Transform hips = Osso("Hips", raiz, new Vector3(0f, 0.95f, 0f));
        Transform spine = Osso("Spine", hips, new Vector3(0f, 1.08f, 0f));
        Transform chest = Osso("Chest", spine, new Vector3(0f, 1.30f, 0f));
        Transform neck = Osso("Neck", chest, new Vector3(0f, 1.55f, 0.03f));
        Transform head = Osso("Head", neck, new Vector3(0f, 1.64f, 0.05f));
        Transform lua = Osso("LeftUpperArm", chest, new Vector3(-0.36f, 1.50f, 0f));
        Transform lla = Osso("LeftLowerArm", lua, new Vector3(-0.44f, 1.15f, 0f));
        Transform lh = Osso("LeftHand", lla, new Vector3(-0.42f, 0.86f, 0.02f));
        Transform rua = Osso("RightUpperArm", chest, new Vector3(0.36f, 1.50f, 0f));
        Transform rla = Osso("RightLowerArm", rua, new Vector3(0.42f, 1.15f, 0f));
        Transform rh = Osso("RightHand", rla, new Vector3(0.30f, 0.88f, 0.02f));
        Transform lul = Osso("LeftUpperLeg", hips, new Vector3(-0.15f, 0.95f, 0f));
        Transform lll = Osso("LeftLowerLeg", lul, new Vector3(-0.17f, 0.52f, 0.02f));
        Transform lf = Osso("LeftFoot", lll, new Vector3(-0.17f, 0.10f, 0f));
        Transform rul = Osso("RightUpperLeg", hips, new Vector3(0.15f, 0.95f, 0f));
        Transform rll = Osso("RightLowerLeg", rul, new Vector3(0.17f, 0.52f, 0.02f));
        Transform rf = Osso("RightFoot", rll, new Vector3(0.17f, 0.10f, 0f));
        Transform capa = Osso("Capa", chest, new Vector3(0f, 1.5f, -0.15f));
        Transform[] ossos = { hips, spine, chest, neck, head, lua, lla, lh, rua, rla, rh, lul, lll, lf, rul, rll, rf, capa };
        // segmento de cada osso (início, fim) e o raio de influência
        Vector3[] ini = { new Vector3(0, 0.9f, 0), new Vector3(0, 1.08f, 0), new Vector3(0, 1.30f, 0), new Vector3(0, 1.55f, 0.03f), new Vector3(0, 1.64f, 0.05f),
            new Vector3(-0.36f, 1.5f, 0), new Vector3(-0.44f, 1.15f, 0), new Vector3(-0.42f, 0.86f, 0.02f),
            new Vector3(0.36f, 1.5f, 0), new Vector3(0.42f, 1.15f, 0), new Vector3(0.30f, 0.88f, 0.02f),
            new Vector3(-0.15f, 0.95f, 0), new Vector3(-0.17f, 0.52f, 0.02f), new Vector3(-0.17f, 0.10f, 0),
            new Vector3(0.15f, 0.95f, 0), new Vector3(0.17f, 0.52f, 0.02f), new Vector3(0.17f, 0.10f, 0) };
        Vector3[] fim = { new Vector3(0, 1.08f, 0), new Vector3(0, 1.30f, 0), new Vector3(0, 1.55f, 0.03f), new Vector3(0, 1.64f, 0.05f), new Vector3(0, 1.85f, 0.05f),
            new Vector3(-0.44f, 1.15f, 0), new Vector3(-0.42f, 0.86f, 0.02f), new Vector3(-0.42f, 0.68f, 0.03f),
            new Vector3(0.42f, 1.15f, 0), new Vector3(0.30f, 0.88f, 0.02f), new Vector3(0.26f, 0.70f, 0.1f),
            new Vector3(-0.17f, 0.52f, 0.02f), new Vector3(-0.17f, 0.10f, 0), new Vector3(-0.17f, 0.02f, 0.2f),
            new Vector3(0.17f, 0.52f, 0.02f), new Vector3(0.17f, 0.10f, 0), new Vector3(0.17f, 0.02f, 0.2f) };
        float[] raio = { 0.22f, 0.22f, 0.26f, 0.10f, 0.20f, 0.13f, 0.11f, 0.10f, 0.13f, 0.11f, 0.10f, 0.15f, 0.13f, 0.12f, 0.15f, 0.13f, 0.12f };
        int nB = ossos.Length;
        Vector3[] v = malha.vertices;
        Unity.Collections.NativeArray<byte> porVertice = new Unity.Collections.NativeArray<byte>(v.Length, Unity.Collections.Allocator.Temp);
        Unity.Collections.NativeArray<BoneWeight1> pesos = new Unity.Collections.NativeArray<BoneWeight1>(v.Length * 4, Unity.Collections.Allocator.Temp);
        float[] w = new float[nB];
        int[] idx = new int[nB];
        for (int i = 0; i < v.Length; i++)
        {
            float x = v[i].x, y = v[i].y, z = v[i].z;
            float esq = 1f - Suave(-0.05f, 0.15f, x), dir = Suave(-0.15f, 0.05f, x);
            for (int k = 0; k < nB - 1; k++)
            {
                float d = DistSeg(v[i], ini[k], fim[k]) / raio[k];
                float f = 1f / (1f + d * d * d * d);
                if (k >= 5 && k <= 7) f *= esq; else if (k >= 8 && k <= 10) f *= dir;
                else if (k >= 11 && k <= 13) f *= esq; else if (k >= 14) f *= dir;
                w[k] = f;
            }
            // capa: o que está atrás das costas, abaixo dos ombros
            float cp = Suave(0.12f, 0.30f, -z) * (1f - Suave(1.5f, 1.65f, y));
            float somaK = 0f; for (int k = 0; k < nB - 1; k++) somaK += w[k];
            if (somaK < 1e-6f) { w[0] = 1f; somaK = 1f; }
            for (int k = 0; k < nB - 1; k++) w[k] = w[k] / somaK * (1f - cp);
            w[nB - 1] = cp;
            for (int k = 0; k < nB; k++) idx[k] = k;
            System.Array.Sort(idx, (p, q) => w[q].CompareTo(w[p]));
            float soma = w[idx[0]] + w[idx[1]] + w[idx[2]] + w[idx[3]];
            porVertice[i] = 4;
            for (int k = 0; k < 4; k++) pesos[i * 4 + k] = new BoneWeight1 { boneIndex = idx[k], weight = w[idx[k]] / soma };
        }
        Matrix4x4[] bind = new Matrix4x4[nB];
        for (int i = 0; i < nB; i++) bind[i] = ossos[i].worldToLocalMatrix * raiz.localToWorldMatrix;
        malha.bindposes = bind;
        malha.SetBoneWeights(porVertice, pesos);
        Debug.Log("[TanqueNovo] pesos gravados: " + malha.GetAllBoneWeights().Length + " (" + malha.vertexCount + " vértices, " + malha.bindposes.Length + " ossos)");
        porVertice.Dispose(); pesos.Dispose();
        EditorUtility.SetDirty(malha);
        AssetDatabase.SaveAssets();

        GameObject corpo = new GameObject("Malha");
        corpo.transform.SetParent(raiz, false);
        SkinnedMeshRenderer sr = corpo.AddComponent<SkinnedMeshRenderer>();
        sr.sharedMesh = malha; sr.sharedMaterial = mat;
        sr.bones = ossos; sr.rootBone = raiz;
        sr.shadowCastingMode = ShadowCastingMode.On;
        sr.updateWhenOffscreen = false;
        Bounds lb = malha.bounds; lb.Expand(1.2f); sr.localBounds = lb;
        g.AddComponent<SkinProceduralTanque>();
        GameObject p = PrefabUtility.SaveAsPrefabAsset(g, PREFAB_SKIN);
        Object.DestroyImmediate(g);
        AssetDatabase.ImportAsset(PREFAB_SKIN, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_SKIN);
    }

    static string ColocarNoJogador(GameObject skin)
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(PREFAB_JOGADOR);
        try
        {
            Transform velha = raiz.transform.Find("Skin Auric");
            if (velha != null) Object.DestroyImmediate(velha.gameObject);
            // esconde o corpo antigo (os ossos continuam animando por baixo)
            int escondidos = 0;
            foreach (SkinnedMeshRenderer s in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { s.enabled = false; escondidos++; }
            GameObject n = (GameObject)PrefabUtility.InstantiatePrefab(skin, raiz.transform);
            n.name = "Skin Auric";
            n.transform.localPosition = Vector3.zero; n.transform.localRotation = Quaternion.identity; n.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB_JOGADOR);
            return "Jogador_Tanque atualizado (" + escondidos + " partes do corpo antigo escondidas).";
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    static void FichaDaSelecao(GameObject skin)
    {
        FichaPersonagem f = AssetDatabase.LoadAssetAtPath<FichaPersonagem>(FICHA);
        if (f == null) { Debug.LogWarning("[TanqueNovo] não achei a ficha " + FICHA); return; }
        f.modeloVisual = skin;
        EditorUtility.SetDirty(f);
    }
}
#endif
