// =====================================================================
//  PingluVila_Lobby.cs  -  Cenário 3D do lobby: o mundo destruído pelo dragão
// ---------------------------------------------------------------------
//  Em volta do palco da seleção: chão de terra e grama seca com crateras,
//  vilas destruídas (as mesmas construções do jogo, com telhados desabados
//  e paredes quebradas), ruínas de castelo ao fundo, quase tudo pegando
//  fogo, fumaça subindo, árvores carbonizadas, rochas, entulho e o dragão
//  voando bem longe.
//
//  COMO USAR:  Pinglu > 28. Cenário do lobby   (e salve a cena)
//  Para refazer: rode de novo. Para remover: Pinglu > 29. Remover cenário do lobby
//  (precisa dos scripts da vila e do mapa: PingluVila.cs e PingluVila_Mundo.cs)
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string NOME_CENARIO_LOBBY = "Cenario do Lobby";
    const string PASTA_LOBBY = "Assets/Cenario/Lobby";
    static readonly Vector3 CENTRO_LOBBY = new Vector3(0f, -3000f, 0f); // mesmo lugar do palco da seleção
    static readonly Dictionary<Material, Material> queimados = new Dictionary<Material, Material>();
    static readonly List<Vector4> crateras = new List<Vector4>();       // x, z, raio, profundidade
    static readonly List<Vector3> ocupado = new List<Vector3>();        // x, z, raio
    static Material matCarvaoLobby;
    static int luzesLobby;

    [MenuItem("Pinglu/28. Cenário do lobby")]
    public static void ConstruirLobby()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        GameObject antigo = GameObject.Find(NOME_CENARIO_LOBBY);
        if (antigo != null)
        {
            if (!EditorUtility.DisplayDialog("Pinglu", "O cenário do lobby já existe. Quer refazer?", "Sim", "Cancelar")) return;
            Undo.DestroyObjectImmediate(antigo);
        }

        avisos.Clear(); formas.Clear(); tamanhoPrefab.Clear(); queimados.Clear(); crateras.Clear(); ocupado.Clear();
        contadorMalhas = 0; luzesLobby = 0;
        rng = new System.Random(4242);
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando...", 0.02f);
            CriarPastaSeFaltar("Assets", "Cenario"); CriarPastaSeFaltar("Assets/Cenario", "Vila"); CriarPastaSeFaltar(PASTA, "Materiais_v2");
            CriarPastaSeFaltar("Assets/Cenario", "Lobby"); CriarPastaSeFaltar(PASTA_LOBBY, "Queimados");
            pastaMalhas = PASTA_LOBBY + "/Malhas";
            if (AssetDatabase.IsValidFolder(pastaMalhas)) AssetDatabase.DeleteAsset(pastaMalhas);
            AssetDatabase.CreateFolder(PASTA_LOBBY, "Malhas");
            ConfigurarTexturas();
            CriarMateriais();
            matCarvaoLobby = MaterialLobby("Carvao", mats[MAD_ESC], new Color(0.2f, 0.18f, 0.17f), 0.05f);

            GameObject r = new GameObject(NOME_CENARIO_LOBBY);
            Undo.RegisterCreatedObjectUndo(r, "Cenario do lobby");
            r.transform.position = CENTRO_LOBBY;
            raiz = r.transform;

            // crateras (longe do palco)
            for (int i = 0; i < 18; i++)
            {
                Vector2 p = Anel(14f, 70f, -110f, 110f);
                crateras.Add(new Vector4(p.x, p.y, Faixa(2.5f, 6f), Faixa(0.8f, 1.6f)));
            }

            EditorUtility.DisplayProgressBar("Pinglu", "Chão de terra e grama...", 0.1f);
            ChaoDoLobby();
            EditorUtility.DisplayProgressBar("Pinglu", "Vilas destruídas...", 0.3f);
            VilasDestruidas();
            EditorUtility.DisplayProgressBar("Pinglu", "Ruínas de castelo...", 0.55f);
            CastelosEmRuinas();
            EditorUtility.DisplayProgressBar("Pinglu", "Árvores queimadas, rochas e grama...", 0.75f);
            NaturezaDoLobby();
            EditorUtility.DisplayProgressBar("Pinglu", "O dragão...", 0.92f);
            Dragao();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(r.scene);
            Selection.activeGameObject = r;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console).");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            pastaMalhas = PASTA_MALHAS;
        }
        EditorUtility.DisplayDialog("Pinglu", "Cenário do lobby pronto!\n\nVilas destruídas pegando fogo, ruínas de castelo, crateras, árvores queimadas e o dragão voando bem longe.\n\n" +
            "SALVE A CENA (Ctrl + S) e dê Play para ver no lobby." + (avisos.Count > 0 ? "\n\nAvisos:\n- " + string.Join("\n- ", avisos) : ""), "OK");
    }

    [MenuItem("Pinglu/29. Remover cenário do lobby")]
    public static void RemoverLobby()
    {
        GameObject g = GameObject.Find(NOME_CENARIO_LOBBY);
        if (g == null) { EditorUtility.DisplayDialog("Pinglu", "Não há cenário do lobby na cena.", "OK"); return; }
        Undo.DestroyObjectImmediate(g);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Pinglu", "Cenário do lobby removido. Salve a cena com Ctrl + S.", "OK");
    }

    // ==================================================================
    //  apoio
    // ==================================================================
    // ponto num anel em volta do palco (ângulo 180 = atrás do portal, para onde a câmera olha)
    static Vector2 Anel(float rMin, float rMax, float aMin, float aMax)
    {
        float a = (180f + Faixa(aMin, aMax)) * Mathf.Deg2Rad, r = Faixa(rMin, rMax);
        return new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
    }

    static bool Livre2D(Vector2 p, float raio)
    {
        if (DentroDoPalco(p.x, p.y) > 0.01f) return false;
        foreach (Vector3 o in ocupado) if (new Vector2(p.x - o.x, p.y - o.y).magnitude < raio + o.z) return false;
        return true;
    }

    // 1 = área do palco (personagens e portal), que fica plana e livre
    static float DentroDoPalco(float x, float z)
    {
        float d = new Vector2(x / 10f, (z + 3f) / 12f).magnitude;
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, d));
    }

    // altura do chão no lobby (coordenadas em relação ao centro do palco)
    static float AlturaLobby(float x, float z)
    {
        float h = (Mathf.PerlinNoise(x * 0.04f + 3f, z * 0.04f + 7f) - 0.5f) * 1.6f + (Mathf.PerlinNoise(x * 0.13f, z * 0.13f) - 0.5f) * 0.35f;
        foreach (Vector4 c in crateras)
        {
            float d = new Vector2(x - c.x, z - c.y).magnitude;
            if (d > c.z * 1.6f) continue;
            h -= c.w * (1f - Mathf.SmoothStep(0f, 1f, d / c.z));
            h += c.w * 0.35f * Mathf.Exp(-Mathf.Pow((d - c.z) / (c.z * 0.25f), 2f)); // borda levantada
        }
        float dist = new Vector2(x, z).magnitude;
        h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(55f, 95f, dist)) * 7f * Mathf.PerlinNoise(x * 0.02f + 9f, z * 0.02f);
        return Mathf.Lerp(h, 0f, DentroDoPalco(x, z));
    }

    static Vector3 NoChaoLobby(float x, float z) { return CENTRO_LOBBY + new Vector3(x, AlturaLobby(x, z), z); }

    static Material MaterialLobby(string nome, Material baseMat, Color cor, float lisura)
    {
        string caminho = PASTA_LOBBY + "/" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, caminho); }
        else m.CopyPropertiesFromMaterial(baseMat);
        m.SetColor("_BaseColor", cor);
        m.SetFloat("_Smoothness", lisura);
        EditorUtility.SetDirty(m);
        return m;
    }

    // versão chamuscada dos materiais (escurecida e fosca)
    static void Queimar(GameObject g, float quanto)
    {
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        if (r == null) return;
        Material[] arr = r.sharedMaterials;
        for (int i = 0; i < arr.Length; i++)
        {
            Material o = arr[i];
            if (o == null) continue;
            Material q;
            if (!queimados.TryGetValue(o, out q))
            {
                string caminho = PASTA_LOBBY + "/Queimados/" + o.name + "_Queimado.mat";
                q = AssetDatabase.LoadAssetAtPath<Material>(caminho);
                if (q == null) { q = new Material(o); AssetDatabase.CreateAsset(q, caminho); }
                else q.CopyPropertiesFromMaterial(o);
                if (q.HasProperty("_BaseColor")) { Color c = q.GetColor("_BaseColor"); q.SetColor("_BaseColor", new Color(c.r * 0.32f, c.g * 0.29f, c.b * 0.28f, c.a)); }
                if (q.HasProperty("_Smoothness")) q.SetFloat("_Smoothness", 0.05f);
                EditorUtility.SetDirty(q);
                queimados[o] = q;
            }
            if (rng.NextDouble() < quanto) arr[i] = q;
        }
        r.sharedMaterials = arr;
    }

    static void LuzDeFogo(Transform pai, Vector3 local, float alcance, float intensidade)
    {
        if (luzesLobby >= 12) return;
        luzesLobby++;
        GameObject g = new GameObject("Luz do Fogo");
        g.transform.SetParent(pai, false);
        g.transform.localPosition = local;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.15f); l.range = alcance; l.intensity = intensidade; l.shadows = LightShadows.None;
    }

    static void FumacaGrande(Transform pai, Vector3 mundo, float escala)
    {
        GameObject go = new GameObject("Coluna de Fumaca");
        go.transform.SetParent(pai, true);
        go.transform.position = mundo;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.loop = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * escala, 2.6f * escala);
        main.startSize = new ParticleSystem.MinMaxCurve(4f * escala, 8f * escala);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.1f, 0.1f, 0.55f), new Color(0.22f, 0.16f, 0.15f, 0.45f));
        main.maxParticles = 120;
        var em = ps.emission; em.rateOverTime = 7f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9f; sh.radius = 1.5f * escala;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.6f));
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.6f, 0.4f), 0f), new GradientColorKey(Color.white, 0.2f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var ruido = ps.noise; ruido.enabled = true; ruido.strength = 0.6f; ruido.frequency = 0.2f;
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PARTICULA_URP);
        if (mat != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
    }

    // ==================================================================
    //  CHÃO: terra, grama seca e manchas queimadas, com crateras
    // ==================================================================
    static Material MaterialDeTerreno(string camada, string nome, Color tom)
    {
        TerrainLayer tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(CAMADAS_TS + camada + "_TerrainLayer.terrainlayer");
        string caminho = PASTA_LOBBY + "/" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, caminho); }
        if (tl != null)
        {
            m.SetTexture("_BaseMap", tl.diffuseTexture);
            if (tl.normalMapTexture != null) { m.SetTexture("_BumpMap", tl.normalMapTexture); m.EnableKeyword("_NORMALMAP"); }
        }
        else avisos.Add("Não achei a textura de chão " + camada);
        m.SetColor("_BaseColor", tom);
        m.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void ChaoDoLobby()
    {
        const float TAM = 190f; const int RES = 150;
        float passo = TAM / RES;
        Vector3[] v = new Vector3[(RES + 1) * (RES + 1)];
        Vector2[] uv = new Vector2[v.Length];
        for (int j = 0; j <= RES; j++)
            for (int i = 0; i <= RES; i++)
            {
                float x = -TAM * 0.5f + i * passo, z = -TAM * 0.5f + j * passo;
                v[j * (RES + 1) + i] = new Vector3(x, AlturaLobby(x, z), z);
                uv[j * (RES + 1) + i] = new Vector2(x / 5f, z / 5f);
            }
        List<int>[] sub = { new List<int>(), new List<int>(), new List<int>() }; // 0 terra, 1 grama seca, 2 queimado
        for (int j = 0; j < RES; j++)
            for (int i = 0; i < RES; i++)
            {
                float x = -TAM * 0.5f + (i + 0.5f) * passo, z = -TAM * 0.5f + (j + 0.5f) * passo;
                int tipo = TipoDeChao(x, z);
                int a = j * (RES + 1) + i;
                sub[tipo].AddRange(new[] { a, a + RES + 1, a + 1, a + 1, a + RES + 1, a + RES + 2 });
            }
        Mesh m = new Mesh { name = "Chao do Lobby", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.vertices = v; m.uv = uv; m.subMeshCount = 3;
        for (int k = 0; k < 3; k++) m.SetTriangles(sub[k], k);
        m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        AssetDatabase.CreateAsset(m, pastaMalhas + "/Chao_do_Lobby.asset");
        GameObject g = new GameObject("Chao");
        g.transform.SetParent(raiz, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        g.AddComponent<MeshRenderer>().sharedMaterials = new[] {
            MaterialDeTerreno("Soil_Rocks", "Chao_Terra", new Color(0.55f, 0.48f, 0.42f)),
            MaterialDeTerreno("Grass_Dry", "Chao_Grama", new Color(0.62f, 0.58f, 0.48f)),
            MaterialDeTerreno("Black_Sand", "Chao_Queimado", new Color(0.42f, 0.38f, 0.36f)) };
        g.isStatic = true;
    }

    static int TipoDeChao(float x, float z)
    {
        foreach (Vector4 c in crateras) if (new Vector2(x - c.x, z - c.y).magnitude < c.z * 1.2f) return 2;
        float n = Mathf.PerlinNoise(x * 0.07f + 11f, z * 0.07f + 5f), q = Mathf.PerlinNoise(x * 0.05f + 40f, z * 0.05f + 2f);
        if (q > 0.66f) return 2;
        return n > 0.52f ? 1 : 0;
    }

    // ==================================================================
    //  VILAS DESTRUÍDAS
    // ==================================================================
    static void VilasDestruidas()
    {
        Transform pasta = Pasta("Vilas Destruidas");
        int feitas = 0;
        for (int t = 0; t < 300 && feitas < 18; t++)
        {
            Vector2 p = Anel(16f, 48f, -100f, 100f);
            float ang = Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(ang, 180f)) < 9f && p.magnitude < 28f) continue; // deixa o fundo do portal mais limpo
            if (!Livre2D(p, 7.5f)) continue;
            CasaArruinada(pasta, p, feitas);
            ocupado.Add(new Vector3(p.x, p.y, 6.5f));
            feitas++;
        }
    }

    static void CasaArruinada(Transform pasta, Vector2 p, int n)
    {
        Casa h = new Casa();
        double sorte = rng.NextDouble();
        int tipo = sorte < 0.4 ? 0 : sorte < 0.72 ? 1 : sorte < 0.86 ? 4 : sorte < 0.95 ? 5 : 3;
        if (tipo == 0 || tipo == 1) NovaCasa(h, tipo);
        else if (tipo == 4) { h.tipo = 4; h.W = 8f; h.D = 11f; h.inclinacao = 46f; h.telhado = TELHA_ARDOSIA; }
        else if (tipo == 5) { h.tipo = 5; h.W = 5f; h.D = 6f; h.inclinacao = 32f; h.telhado = TELHA_ARDOSIA; }
        else { h.tipo = 3; h.W = 6.2f; h.D = 10f; h.inclinacao = 48f; h.telhado = TELHA_MUSGO; }

        Malha m = new Malha();
        const float ALICERCE = 1.2f;
        string nome;
        switch (h.tipo)
        {
            case 0: ChalePedra(m, h, ALICERCE); nome = "Chale Destruido"; break;
            case 1: CasaAlta(m, h, ALICERCE, false); nome = "Casa Destruida"; break;
            case 3: Capela(m, h, ALICERCE); nome = "Capela Destruida"; break;
            case 4: Celeiro(m, h, ALICERCE); nome = "Celeiro Destruido"; break;
            default: Ferraria(m, h, ALICERCE); nome = "Ferraria Destruida"; break;
        }
        // estragos: telhado desabado ou com rombos, e um canto de parede caído
        float yTelhado = h.tipo == 1 ? 5.2f : h.tipo == 3 ? 5.5f : h.tipo == 4 ? 4.2f : 3.0f;
        bool semTelhado = rng.NextDouble() < 0.4;
        List<Vector4> rombos = new List<Vector4>();
        int nRombos = rng.Next(1, 3);
        for (int k = 0; k < nRombos; k++)
            rombos.Add(new Vector4(Faixa(-h.W / 3f, h.W / 3f), yTelhado + Faixa(0.4f, 1.8f), Faixa(-h.D / 3f, h.D / 3f), Faixa(1.6f, 2.8f)));
        float sx = rng.NextDouble() < 0.5 ? -1f : 1f, sz = rng.NextDouble() < 0.5 ? -1f : 1f;
        Vector4 canto = new Vector4(sx * h.W * 0.5f, Faixa(1.2f, yTelhado), sz * h.D * 0.5f, Faixa(1.6f, 2.8f));
        m.Danificar(c =>
        {
            if (semTelhado && c.y > yTelhado + 0.2f) return true;
            foreach (Vector4 r in rombos) if (Vector3.Distance(c, new Vector3(r.x, r.y, r.z)) < r.w) return true;
            return c.y > 0.6f && Vector3.Distance(c, new Vector3(canto.x, canto.y, canto.z)) < canto.w;
        });

        Vector3 pos = NoChaoLobby(p.x, p.y);
        Vector3 paraPalco = new Vector3(-p.x, 0f, -p.y).normalized;
        Quaternion rot = Quaternion.LookRotation(paraPalco) * Quaternion.Euler(0f, Faixa(-30f, 30f), Faixa(-2f, 2f));
        GameObject g = Instanciar(m, nome, pos, rot, pasta, false);
        Queimar(g, 0.75f);

        // entulho em volta (mais no canto que caiu) e vigas caídas
        Malha e = new Malha();
        int pecas = rng.Next(10, 18);
        for (int k = 0; k < pecas; k++)
        {
            bool perto = k < pecas / 2;
            Vector3 l = perto ? new Vector3(canto.x + Faixa(-2f, 2f), 0f, canto.z + Faixa(-2f, 2f))
                              : new Vector3(Faixa(-h.W * 0.8f, h.W * 0.8f), 0f, Faixa(-h.D * 0.8f, h.D * 0.8f));
            Vector3 mundo = pos + rot * l;
            float y = AlturaLobby(mundo.x - CENTRO_LOBBY.x, mundo.z - CENTRO_LOBBY.z) + CENTRO_LOBBY.y - pos.y;
            float t2 = Faixa(0.25f, 0.9f);
            int mat = rng.NextDouble() < 0.6 ? (rng.NextDouble() < 0.5 ? PEDRA_BLOCOS : PEDRA_RUSTICA) : (rng.NextDouble() < 0.5 ? MAD_ESC : TABUAS);
            e.Caixa(new Vector3(l.x, y + t2 * 0.25f, l.z), new Vector3(t2 * Faixa(0.8f, 1.5f), t2 * Faixa(0.4f, 0.8f), t2 * Faixa(0.7f, 1.3f)),
                    Quaternion.Euler(Faixa(-25f, 25f), Faixa(0f, 360f), Faixa(-25f, 25f)), mat);
        }
        for (int k = 0; k < rng.Next(1, 4); k++)
        {
            Vector3 l = new Vector3(Faixa(-h.W * 0.7f, h.W * 0.7f), 0f, Faixa(-h.D * 0.7f, h.D * 0.7f));
            Vector3 mundo = pos + rot * l;
            float y = AlturaLobby(mundo.x - CENTRO_LOBBY.x, mundo.z - CENTRO_LOBBY.z) + CENTRO_LOBBY.y - pos.y;
            e.Caixa(new Vector3(l.x, y + 0.5f, l.z), new Vector3(0.22f, 0.22f, Faixa(2.5f, 4.5f)),
                    Quaternion.Euler(Faixa(-30f, 30f), Faixa(0f, 360f), Faixa(-15f, 15f)), MAD_ESC);
        }
        GameObject ge = Instanciar(e, "Entulho", pos, rot, g.transform, false);
        ge.transform.SetParent(g.transform, true);
        Queimar(ge, 0.6f);

        // fogo
        if (rng.NextDouble() < 0.85)
        {
            int fogos = rng.Next(1, 3);
            for (int k = 0; k < fogos; k++)
            {
                Vector3 l = new Vector3(Faixa(-h.W * 0.3f, h.W * 0.3f), Faixa(0.3f, yTelhado * 0.8f), Faixa(-h.D * 0.3f, h.D * 0.3f));
                Fogo(g.transform, l);
                Fogo(g.transform, l + new Vector3(0.4f, 0.2f, -0.3f));
                if (k == 0) LuzDeFogo(g.transform, l + Vector3.up * 1.5f, 14f, 3.5f);
            }
        }
        if (n % 3 == 0) FumacaGrande(g.transform, pos + Vector3.up * (yTelhado + 1f), 1f);
    }

    // ==================================================================
    //  RUÍNAS DE CASTELO (ao fundo)
    // ==================================================================
    static void CastelosEmRuinas()
    {
        Transform pasta = Pasta("Ruinas de Castelo");
        float[] angulos = { -32f, 12f, 48f };
        for (int c = 0; c < angulos.Length; c++)
        {
            float a = (180f + angulos[c]) * Mathf.Deg2Rad, raio = Faixa(70f, 88f);
            Vector2 centro = new Vector2(Mathf.Sin(a) * raio, Mathf.Cos(a) * raio);
            Vector3 pos = NoChaoLobby(centro.x, centro.y) + Vector3.down * 1f;
            Quaternion rot = Quaternion.LookRotation(new Vector3(-centro.x, 0f, -centro.y).normalized) * Quaternion.Euler(0f, Faixa(-20f, 20f), 0f);
            Malha m = new Malha();
            int torres = rng.Next(3, 6);
            List<Vector3> pontos = new List<Vector3>();
            for (int t = 0; t < torres; t++)
            {
                Vector3 l = new Vector3(Faixa(-26f, 26f), 0f, Faixa(-14f, 10f));
                pontos.Add(l);
                TorreQuebrada(m, l, Faixa(5f, 8f), Faixa(13f, 26f));
            }
            pontos.Sort((p1, p2) => p1.x.CompareTo(p2.x));
            for (int t = 0; t + 1 < pontos.Count; t++) MuralhaQuebrada(m, pontos[t], pontos[t + 1], Faixa(6f, 9.5f));
            GameObject g = Instanciar(m, "Castelo em Ruinas", pos, rot, pasta, false);
            Queimar(g, 0.5f);
            // fogo no alto das torres e fumaça grossa
            foreach (Vector3 l in pontos)
            {
                if (rng.NextDouble() < 0.6) { Fogo(g.transform, l + Vector3.up * Faixa(8f, 14f)); Fogo(g.transform, l + new Vector3(1f, 9f, 0.5f)); }
            }
            LuzDeFogo(g.transform, pontos[0] + Vector3.up * 10f, 30f, 6f);
            FumacaGrande(g.transform, pos + rot * (pontos[rng.Next(pontos.Count)] + Vector3.up * 14f), 1.8f);
        }
    }

    static void TorreQuebrada(Malha m, Vector3 c, float lado, float h)
    {
        m.Caixa(c + Vector3.up * (h * 0.4f), new Vector3(lado, h * 0.8f + 2f, lado), PEDRA_BLOCOS);
        m.Caixa(c + Vector3.up * 1.2f, new Vector3(lado + 0.8f, 2.4f, lado + 0.8f), PEDRA_ESCURA);
        // topo quebrado: pedaços de alturas diferentes
        int pedacos = rng.Next(3, 6);
        for (int k = 0; k < pedacos; k++)
        {
            float px = Faixa(-lado * 0.35f, lado * 0.35f), pz = Faixa(-lado * 0.35f, lado * 0.35f);
            float ph = Faixa(1f, h * 0.25f);
            m.Caixa(c + new Vector3(px, h * 0.8f + ph * 0.5f, pz), new Vector3(lado * Faixa(0.25f, 0.5f), ph, lado * Faixa(0.25f, 0.5f)), PEDRA_BLOCOS);
        }
        // janelas escuras (rasgos) e blocos caídos no pé
        for (int k = 0; k < 3; k++) m.Caixa(c + new Vector3(0f, h * (0.35f + k * 0.15f), lado * 0.5f + 0.02f), new Vector3(0.6f, 1.4f, 0.1f), PEDRA_ESCURA);
        for (int k = 0; k < 5; k++)
            m.Caixa(c + new Vector3(Faixa(-lado, lado), 0.6f, Faixa(-lado, lado)), Vector3.one * Faixa(0.8f, 1.8f),
                    Quaternion.Euler(Faixa(-30f, 30f), Faixa(0f, 360f), Faixa(-30f, 30f)), PEDRA_RUSTICA);
    }

    static void MuralhaQuebrada(Malha m, Vector3 a, Vector3 b, float h)
    {
        Vector3 d = b - a; float comp = d.magnitude;
        Quaternion r = Quaternion.LookRotation(d.normalized);
        int seg = Mathf.Max(2, Mathf.RoundToInt(comp / 3f));
        for (int k = 0; k < seg; k++)
        {
            if (rng.NextDouble() < 0.22) continue; // rombo
            float t = (k + 0.5f) / seg;
            float hh = h * Faixa(0.45f, 1f);
            m.Caixa(Vector3.Lerp(a, b, t) + Vector3.up * (hh * 0.5f - 0.5f), new Vector3(2.4f, hh, comp / seg + 0.05f), r, PEDRA_BLOCOS);
        }
    }

    // ==================================================================
    //  ÁRVORES QUEIMADAS, ROCHAS E GRAMA
    // ==================================================================
    static void NaturezaDoLobby()
    {
        Transform pasta = Pasta("Natureza Queimada");
        Mesh[] cache = new Mesh[3]; Material[][] cacheMats = new Material[3][];
        int feitas = 0;
        for (int t = 0; t < 400 && feitas < 34; t++)
        {
            Vector2 p = Anel(11f, 70f, -150f, 150f);
            if (!Livre2D(p, 2.5f)) continue;
            int var = rng.Next(0, 3);
            Malha m = null;
            if (cache[var] == null) { m = new Malha(); ArvoreSeca(m, var); }
            GameObject g = Instanciar(m, "Arvore Queimada", NoChaoLobby(p.x, p.y) + Vector3.down * 0.1f,
                                      Quaternion.Euler(Faixa(-6f, 6f), Faixa(0f, 360f), Faixa(-6f, 6f)), pasta, false, ref cache[var], ref cacheMats[var]);
            g.transform.localScale = Vector3.one * Faixa(1.1f, 1.8f);
            TrocarMateriaisLobby(g, matCarvaoLobby);
            if (rng.NextDouble() < 0.25) Fogo(g.transform, new Vector3(0f, Faixa(1.5f, 3f), 0f));
            ocupado.Add(new Vector3(p.x, p.y, 1.5f));
            feitas++;
        }
        // rochas
        for (int t = 0; t < 200 && feitas < 70; t++)
        {
            Vector2 p = Anel(9f, 65f, -160f, 160f);
            if (!Livre2D(p, 1.5f)) continue;
            RochaLobby(pasta, PEDRAS_MUNDO[rng.Next(PEDRAS_MUNDO.Length)], NoChaoLobby(p.x, p.y), Faixa(1f, 3.5f));
            feitas++;
        }
        // tufos de grama seca nas manchas de grama
        GameObject[] grama = {
            AssetDatabase.LoadAssetAtPath<GameObject>(DETALHES_TS + "GrassDry_A.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>(DETALHES_TS + "GrassDry_B.prefab") };
        int tufos = 0;
        for (int t = 0; t < 3000 && tufos < 450; t++)
        {
            Vector2 p = Anel(3f, 55f, -170f, 170f);
            if (TipoDeChao(p.x, p.y) != 1 || DentroDoPalco(p.x, p.y) > 0.6f) continue;
            GameObject pref = grama[rng.Next(grama.Length)];
            if (pref == null) break;
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(pref, raiz.gameObject.scene);
            g.transform.SetParent(pasta, true);
            g.transform.position = NoChaoLobby(p.x, p.y);
            g.transform.rotation = Quaternion.Euler(0f, Faixa(0f, 360f), 0f);
            g.transform.localScale = Vector3.one * Faixa(0.9f, 1.6f);
            g.isStatic = true;
            tufos++;
        }
    }

    static void TrocarMateriaisLobby(GameObject g, Material m)
    {
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        if (r == null || m == null) return;
        Material[] arr = r.sharedMaterials;
        for (int i = 0; i < arr.Length; i++) arr[i] = m;
        r.sharedMaterials = arr;
    }

    static void RochaLobby(Transform pai, string caminho, Vector3 pos, float tamanho)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab == null) return;
        Vector3 nativo;
        if (!tamanhoPrefab.TryGetValue(caminho, out nativo))
        {
            GameObject tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Bounds b;
            nativo = MedirBounds(tmp, out b) ? b.size : Vector3.one;
            Object.DestroyImmediate(tmp);
            tamanhoPrefab[caminho] = nativo;
        }
        float esc = tamanho / Mathf.Max(0.01f, Mathf.Max(nativo.x, nativo.z));
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.gameObject.scene);
        g.transform.SetParent(pai, true);
        g.transform.rotation = Quaternion.Euler(Faixa(-10f, 10f), Faixa(0f, 360f), Faixa(-10f, 10f)) * prefab.transform.rotation;
        g.transform.localScale = prefab.transform.localScale * esc;
        g.transform.position = pos + Vector3.down * tamanho * 0.2f;
        g.isStatic = true;
    }

    // ==================================================================
    //  O DRAGÃO (bem longe)
    // ==================================================================
    static void Dragao()
    {
        string tex = PASTA_LOBBY + "/dragao_atlas.png";
        TextureImporter ti = AssetImporter.GetAtPath(tex) as TextureImporter;
        if (ti == null) { avisos.Add("Não achei a imagem do dragão (" + tex + ")."); return; }
        if (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        string caminho = PASTA_LOBBY + "/Dragao.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, caminho); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 2985;
        EditorUtility.SetDirty(m);
        GameObject g = new GameObject("Dragao (chefe final)");
        g.transform.SetParent(raiz, false);
        DragaoDistante d = g.AddComponent<DragaoDistante>();
        d.material = m;
        d.materialParticula = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PARTICULA_URP);
    }
}
#endif
