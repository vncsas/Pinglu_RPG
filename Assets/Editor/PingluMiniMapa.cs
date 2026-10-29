// =====================================================================
//  PingluMiniMapa.cs  -  Desenha o mapa do jogo e monta o mini-mapa
// ---------------------------------------------------------------------
//  Gera um mapa estilo pergaminho a partir do terreno (cores do chão,
//  relevo, curvas de nível, florestas, estradas, lagoas e construções)
//  e coloca o mini-mapa no canto inferior esquerdo.
//
//  COMO USAR:  Pinglu > 22. Mini-mapa
//  Rode de novo sempre que mudar o terreno (biomas, vila...).
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluMiniMapa
{
    const string PASTA = "Assets/Cenario/MiniMapa";
    const string MAPA = PASTA + "/mapa_pergaminho.png";
    const string MATERIAL = PASTA + "/MiniMapa.mat";
    const string ICONES = "Assets/script/MiniMapa/Icones/";
    const int N = 1024;

    [MenuItem("Pinglu/22. Mini-mapa e mapa do mundo")]
    public static void Criar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        Terrain t = Terrain.activeTerrain;
        if (t == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        Shader sh = Shader.Find("Pinglu/MiniMapa");
        if (sh == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o shader do mini-mapa. Extraiu o zip na pasta do projeto?", "OK"); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Cenario")) AssetDatabase.CreateFolder("Assets", "Cenario");
        if (!AssetDatabase.IsValidFolder(PASTA)) AssetDatabase.CreateFolder("Assets/Cenario", "MiniMapa");

        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Desenhando o mapa...", 0.2f);
            Texture2D mapa = DesenharMapa(t);

            EditorUtility.DisplayProgressBar("Pinglu", "Montando o mini-mapa...", 0.8f);
            foreach (string n in new[] { "mm_moldura", "mm_norte", "mm_jogador", "mm_aliado", "mm_inimigo", "mm_vila", "mm_ruinas", "mm_acampamento", "mm_cemiterio", "mapa_pino", "mapa_pulso" })
            {
                TextureImporter ti = AssetImporter.GetAtPath(ICONES + n + ".png") as TextureImporter;
                if (ti == null || (ti.alphaIsTransparency && !ti.mipmapEnabled)) continue;
                ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
            if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, MATERIAL); }
            mat.shader = sh;

            MiniMapa mm = Object.FindFirstObjectByType<MiniMapa>();
            if (mm == null)
            {
                GameObject go = new GameObject("Mini Mapa");
                Undo.RegisterCreatedObjectUndo(go, "Mini-mapa");
                mm = go.AddComponent<MiniMapa>();
            }
            Undo.RecordObject(mm, "Mini-mapa");
            mm.mapa = mapa; mm.material = mat;
            mm.origem = new Vector2(t.transform.position.x, t.transform.position.z);
            mm.tamanho = new Vector2(t.terrainData.size.x, t.terrainData.size.z);
            mm.moldura = Icone("mm_moldura"); mm.iconeNorte = Icone("mm_norte");
            mm.iconeJogador = Icone("mm_jogador"); mm.iconeAliado = Icone("mm_aliado"); mm.iconeInimigo = Icone("mm_inimigo");
            mm.iconeVila = Icone("mm_vila"); mm.iconeRuinas = Icone("mm_ruinas");
            mm.iconeAcampamento = Icone("mm_acampamento"); mm.iconeCemiterio = Icone("mm_cemiterio");
            mm.marcadores = Marcadores();
            mm.abrirComTecla = false; // a tecla M abre o Mapa do Mundo
            EditorUtility.SetDirty(mm);

            // mapa do mundo em 3D
            MapaDoMundo md = Object.FindFirstObjectByType<MapaDoMundo>();
            if (md == null)
            {
                GameObject gm = new GameObject("Mapa do Mundo");
                Undo.RegisterCreatedObjectUndo(gm, "Mapa do mundo");
                md = gm.AddComponent<MapaDoMundo>();
            }
            Undo.RecordObject(md, "Mapa do mundo");
            md.miniMapa = mm;
            md.materialNuvens = MaterialNuvens();
            md.pino = Icone("mapa_pino"); md.pulso = Icone("mapa_pulso");
            EditorUtility.SetDirty(md);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(mm.gameObject.scene);

            EditorUtility.DisplayDialog("Pinglu", "Mini-mapa e mapa do mundo prontos! (" + mm.marcadores.Count + " lugares marcados)\n\n" +
                "- Mini-mapa no canto inferior esquerdo, gira com a câmera (+ e - aproximam)\n- M: mapa do mundo em 3D (abas Mundo, Vila e Pergaminho)\n" +
                "- O mapa começa escondido e vai aparecendo conforme você explora\n\nSalve a cena com Ctrl + S.\n\n" +
                "Se mudar o terreno depois (biomas, vila...), rode este menu de novo para redesenhar o mapa.", "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static Texture2D Icone(string n) { return AssetDatabase.LoadAssetAtPath<Texture2D>(ICONES + n + ".png"); }

    // material transparente das nuvens que cobrem o que não foi explorado
    static Material MaterialNuvens()
    {
        string caminho = PASTA + "/Nuvens_do_Mapa.mat";
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, caminho); }
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------
    //  lugares marcados no mapa
    // ------------------------------------------------------------------
    static List<MiniMapa.Marcador> Marcadores()
    {
        List<MiniMapa.Marcador> l = new List<MiniMapa.Marcador>();
        Bounds b;
        GameObject vila = GameObject.Find("Vila_Gerada");
        if (vila != null && Medir(vila, out b)) l.Add(new MiniMapa.Marcador { nome = "Vila", tipo = MiniMapa.Tipo.Vila, posicao = b.center });
        GameObject mundo = GameObject.Find("Mundo_Gerado");
        if (mundo != null)
            foreach (Transform c in mundo.transform)
            {
                if (c.name == "Natureza" || !Medir(c.gameObject, out b)) continue;
                string n = c.name.ToLower();
                if (n.Contains("ruina")) l.Add(new MiniMapa.Marcador { nome = "Ruínas da Fortaleza", tipo = MiniMapa.Tipo.Ruinas, posicao = b.center });
                else if (n.Contains("acampamento")) l.Add(new MiniMapa.Marcador { nome = "Acampamento de Bandidos", tipo = MiniMapa.Tipo.Acampamento, posicao = b.center });
                else if (n.Contains("cemiterio")) l.Add(new MiniMapa.Marcador { nome = "Cemitério", tipo = MiniMapa.Tipo.Cemiterio, posicao = b.center });
            }
        return l;
    }

    static bool Medir(GameObject g, out Bounds b)
    {
        b = new Bounds(); bool tem = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        return tem;
    }

    // ------------------------------------------------------------------
    //  desenho do mapa
    // ------------------------------------------------------------------
    static Color CorDaCamada(TerrainLayer l)
    {
        string n = l != null ? l.name : "";
        if (n.Contains("Grass_Dry")) return new Color(0.66f, 0.60f, 0.40f);
        if (n.Contains("Grass_Moss")) return new Color(0.34f, 0.42f, 0.27f);
        if (n.Contains("Muddy")) return new Color(0.38f, 0.36f, 0.25f);
        if (n.Contains("Black_Sand")) return new Color(0.24f, 0.21f, 0.20f);
        if (n.Contains("Rock")) return new Color(0.52f, 0.50f, 0.47f);
        if (n.Contains("Heather")) return new Color(0.52f, 0.43f, 0.43f);
        if (n.Contains("Soil_Rocks")) return new Color(0.76f, 0.62f, 0.42f);   // estradas
        if (n.Contains("Pebbles")) return new Color(0.80f, 0.72f, 0.58f);      // praças
        if (n.Contains("Sand")) return new Color(0.72f, 0.66f, 0.50f);
        return new Color(0.48f, 0.52f, 0.36f);
    }

    static Texture2D DesenharMapa(Terrain t)
    {
        TerrainData d = t.terrainData;
        Vector3 o = t.transform.position, s = d.size;
        int ar = d.alphamapResolution;
        float[,,] al = d.GetAlphamaps(0, 0, ar, ar);
        TerrainLayer[] camadas = d.terrainLayers;
        Color[] corCamada = new Color[camadas.Length];
        for (int i = 0; i < camadas.Length; i++) corCamada[i] = CorDaCamada(camadas[i]);
        int hr = d.heightmapResolution;
        float[,] h = d.GetHeights(0, 0, hr, hr);
        System.Func<float, float, float> H = (u, v) =>
        {
            float fx = Mathf.Clamp01(u) * (hr - 1), fz = Mathf.Clamp01(v) * (hr - 1);
            int x0 = Mathf.Min((int)fx, hr - 2), z0 = Mathf.Min((int)fz, hr - 2);
            float tx = fx - x0, tz = fz - z0;
            return Mathf.Lerp(Mathf.Lerp(h[z0, x0], h[z0, x0 + 1], tx), Mathf.Lerp(h[z0 + 1, x0], h[z0 + 1, x0 + 1], tx), tz) * s.y;
        };

        Color papel = new Color(0.86f, 0.78f, 0.60f);
        Color[] px = new Color[N * N];
        float metrosPorPixel = s.x / N;
        float du = 1f / N;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                int ax = Mathf.Min((int)(u * ar), ar - 1), ay = Mathf.Min((int)(v * ar), ar - 1);
                Color chao = Color.black;
                for (int k = 0; k < corCamada.Length; k++) chao += corCamada[k] * al[ay, ax, k];

                float hx = H(u + du, v) - H(u - du, v), hz = H(u, v + du) - H(u, v - du);
                float sombra = Mathf.Clamp(1f + (-hx * 0.6f + hz * 0.6f) / (2f * metrosPorPixel) * 0.9f, 0.68f, 1.22f);

                float grao = 0.93f + 0.07f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f) + 0.03f * Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                Color c = Color.Lerp(papel * grao, chao, 0.6f) * sombra;

                // curvas de nível a cada 5 m (linhas finas, independentes da inclinação)
                float alt = H(u, v);
                float f = alt / 5f - Mathf.Floor(alt / 5f);
                float distM = Mathf.Min(f, 1f - f) * 5f;
                float grad = Mathf.Sqrt(hx * hx + hz * hz) / 2f + 0.0001f;
                if (distM / grad < 0.7f) c *= 0.8f;

                // grade de 100 m bem clarinha
                float mx = o.x + u * s.x, mz = o.z + v * s.z;
                if (Mathf.Abs(mx - Mathf.Round(mx / 100f) * 100f) < metrosPorPixel * 0.5f || Mathf.Abs(mz - Mathf.Round(mz / 100f) * 100f) < metrosPorPixel * 0.5f) c *= 0.93f;

                // bordas envelhecidas
                float bx = Mathf.Min(u, 1f - u), by = Mathf.Min(v, 1f - v);
                c *= Mathf.Lerp(0.6f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(bx, by) * 14f));
                c.a = 1f;
                px[y * N + x] = c;
            }

        // árvores: pontinhos de tinta escura (florestas aparecem como manchas)
        Color tinta = new Color(0.10f, 0.13f, 0.08f);
        foreach (TreeInstance ti in d.treeInstances)
            Ponto(px, ti.position.x * N, ti.position.z * N, 1.4f * Mathf.Clamp(ti.widthScale, 0.6f, 1.6f), tinta, 0.55f);

        // lagoas
        foreach (MeshRenderer r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
        {
            if (r == null || !r.gameObject.scene.IsValid() || !r.gameObject.activeInHierarchy || !r.name.StartsWith("Lagoa")) continue;
            float cx = (r.bounds.center.x - o.x) / s.x * N, cz = (r.bounds.center.z - o.z) / s.z * N;
            float rr = r.bounds.extents.x / s.x * N;
            Ponto(px, cx, cz, rr + 1f, new Color(0.10f, 0.13f, 0.12f), 0.9f);
            Ponto(px, cx, cz, rr, new Color(0.20f, 0.27f, 0.28f), 1f);
        }

        // construções (vila e pontos do mapa): manchas escuras no formato da planta
        foreach (string raiz in new[] { "Vila_Gerada", "Mundo_Gerado" })
        {
            GameObject g = GameObject.Find(raiz);
            if (g == null) continue;
            foreach (MeshRenderer r in g.GetComponentsInChildren<MeshRenderer>())
            {
                if (EhNatureza(r.transform, g.transform)) continue;
                Bounds lb = r.localBounds;
                Vector3 tam = Vector3.Scale(lb.size, r.transform.lossyScale);
                if (r.bounds.size.y < 2.2f || tam.x * tam.z < 5f) continue;
                Vector3[] cs = new Vector3[4];
                cs[0] = r.transform.TransformPoint(lb.center + new Vector3(-lb.extents.x, 0f, -lb.extents.z));
                cs[1] = r.transform.TransformPoint(lb.center + new Vector3(lb.extents.x, 0f, -lb.extents.z));
                cs[2] = r.transform.TransformPoint(lb.center + new Vector3(lb.extents.x, 0f, lb.extents.z));
                cs[3] = r.transform.TransformPoint(lb.center + new Vector3(-lb.extents.x, 0f, lb.extents.z));
                Vector2[] q = new Vector2[4];
                for (int i = 0; i < 4; i++) q[i] = new Vector2((cs[i].x - o.x) / s.x * N, (cs[i].z - o.z) / s.z * N);
                Quad(px, q, new Color(0.28f, 0.20f, 0.13f));
            }
        }

        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(MAPA, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(MAPA);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(MAPA);
        imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 2048; imp.mipmapEnabled = true;
        imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.sRGBTexture = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(MAPA);
    }

    static bool EhNatureza(Transform t, Transform raiz)
    {
        for (Transform p = t; p != null && p != raiz; p = p.parent) if (p.name == "Natureza") return true;
        return false;
    }

    static void Ponto(Color[] px, float cx, float cy, float r, Color cor, float forca)
    {
        int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1), y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(N - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(N - 1, x1); x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = Mathf.Clamp01(r + 0.5f - d) * forca;
                if (a <= 0f) continue;
                px[y * N + x] = Color.Lerp(px[y * N + x], cor, a);
            }
    }

    static void Quad(Color[] px, Vector2[] q, Color cor)
    {
        float minX = Mathf.Min(Mathf.Min(q[0].x, q[1].x), Mathf.Min(q[2].x, q[3].x)), maxX = Mathf.Max(Mathf.Max(q[0].x, q[1].x), Mathf.Max(q[2].x, q[3].x));
        float minY = Mathf.Min(Mathf.Min(q[0].y, q[1].y), Mathf.Min(q[2].y, q[3].y)), maxY = Mathf.Max(Mathf.Max(q[0].y, q[1].y), Mathf.Max(q[2].y, q[3].y));
        for (int y = Mathf.Max(0, Mathf.FloorToInt(minY)); y <= Mathf.Min(N - 1, Mathf.CeilToInt(maxY)); y++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt(minX)); x <= Mathf.Min(N - 1, Mathf.CeilToInt(maxX)); x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool dentro = true; float sinal = 0f;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = q[i], b = q[(i + 1) % 4];
                    float cr = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                    if (sinal == 0f) sinal = Mathf.Sign(cr);
                    else if (Mathf.Sign(cr) != sinal && cr != 0f) { dentro = false; break; }
                }
                if (dentro) px[y * N + x] = Color.Lerp(px[y * N + x], cor, 0.85f);
            }
    }
}
#endif
