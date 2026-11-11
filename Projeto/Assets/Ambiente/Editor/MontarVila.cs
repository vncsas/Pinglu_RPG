using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Vila medieval, atmosfera (ceu de entardecer, neblina quente, pos-processamento), colisores e o jogador (Cavaleiro Sombrio).
public static partial class MontarAmbiente
{
    // ---------------------------------------------------------------- materiais dos predios (texturas CC0 em metros reais)
    static Material MatPBR(string nome, string pasta, float escala = 1f, Color? cor = null, float suave = 0.15f)
    {
        var path = $"{Raiz}/Materiais/{nome}.mat";
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (pasta != null)
        {
            string Achar(string suf) => Directory.GetFiles($"{Raiz}/Texturas/{pasta}").Select(f => f.Replace('\\', '/')).FirstOrDefault(f => f.EndsWith(suf));
            var c = Achar("_Color.jpg"); var n = Achar("_NormalGL.jpg");
            m.SetTexture("_BaseMap", Tex(c)); m.mainTextureScale = new Vector2(escala, escala);
            if (n != null)
            {
                var ni = (TextureImporter)AssetImporter.GetAtPath(n); if (ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
                m.SetTexture("_BumpMap", Tex(n)); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f);
            }
        }
        m.SetColor("_BaseColor", cor ?? Color.white); m.SetFloat("_Smoothness", suave);
        m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");   // sem brilho branco no sol baixo
        m.SetFloat("_Cull", 0f);          // dois lados: telhados/painel fino nao somem de nenhum angulo
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path); return m;
    }

    static Dictionary<string, Material> MateriaisPredio()
    {
        return new Dictionary<string, Material>
        {
            ["Reboco"] = MatPBR("Pred_Reboco", "Plaster001", 1f, new Color(1.0f, 0.9f, 0.72f)),
            ["Madeira"] = MatPBR("Pred_Madeira", "Wood051", 1f, new Color(0.62f, 0.50f, 0.42f)),
            ["Telhado"] = MatPBR("Pred_Telhado", "RoofingTiles014A", 1f, new Color(0.95f, 0.85f, 0.8f)),
            ["Pedra"] = MatPBR("Pred_Pedra", "Rock063", 1f, new Color(0.85f, 0.85f, 0.85f)),
            ["Porta"] = MatPBR("Pred_Porta", "Planks040", 1f, new Color(0.7f, 0.6f, 0.5f)),
            ["Janela"] = MatPBR("Pred_Janela", null, 1f, new Color(0.10f, 0.13f, 0.16f), 0.85f),
            ["Agua"] = MatPBR("Pred_AguaPoco", null, 1f, new Color(0.04f, 0.09f, 0.10f), 0.95f),
            ["Pano"] = MatPBR("Pred_Pano", null, 1f, new Color(0.55f, 0.16f, 0.13f), 0.05f),
        };
    }

    static void PrepararPredios(Dictionary<string, Material> mats)
    {
        foreach (var f in Directory.GetFiles($"{Raiz}/Predios", "*.fbx").Select(x => x.Replace('\\', '/')))
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(f);
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; mi.addCollider = true;
            mi.SaveAndReimport();
            foreach (var mm in AssetDatabase.LoadAllAssetsAtPath(f).OfType<Material>())
                if (mats.TryGetValue(mm.name, out var alvo)) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mm.name), alvo);
            mi.SaveAndReimport();
        }
    }

    static GameObject Predio(string nome, Vector3 pos, Vector3 olharPara, Transform pai)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Predios/{nome}.fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf); go.transform.SetParent(pai);
        var d = olharPara - pos; d.y = 0;
        go.transform.position = pos; go.transform.rotation = d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d) : Quaternion.identity;
        go.isStatic = true; return go;
    }

    static Vector3 NoChao(Terrain t, float x, float z, float dy = 0) => new Vector3(x, t.SampleHeight(new Vector3(x, 0, z)) + dy, z);

    static GameObject Prop(Terrain t, string fbx, string idTex, float x, float z, float escala, float giro, Transform pai, bool colidir = false)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Modelos/{fbx}.fbx"); if (!pf) { Debug.LogWarning("falta prop " + fbx); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf); go.transform.SetParent(pai);
        go.transform.position = NoChao(t, x, z, -0.02f); go.transform.rotation = Quaternion.Euler(0, giro, 0); go.transform.localScale = Vector3.one * escala;
        var mt = MatProp(idTex, new Mats()); foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mt;
        if (colidir) AddCollider(go);
        go.isStatic = true; return go;
    }

    static void AddTronco(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var cc = go.AddComponent<CapsuleCollider>(); float esc = go.transform.lossyScale.y;
        cc.radius = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.05f, 0.25f, 0.6f) / esc; cc.height = Mathf.Min(b.size.y, 6f) / esc; cc.center = new Vector3(0, cc.height / 2f, 0);
    }

    static void AddCollider(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.x < 0.6f && b.size.z < 0.6f && b.size.y < 0.6f) return;
        var bc = go.AddComponent<BoxCollider>(); bc.center = go.transform.InverseTransformPoint(b.center);
        bc.size = new Vector3(b.size.x / go.transform.lossyScale.x, b.size.y / go.transform.lossyScale.y, b.size.z / go.transform.lossyScale.z) * 0.9f;
    }

    static void Vila(Terrain t)
    {
        var mats = MateriaisPredio(); PrepararPredios(mats);
        PrepararModelos();
        var raiz = new GameObject("Vila").transform; var c = new Vector3(VilaX, 0, VilaZ);
        var centro = NoChao(t, c.x, c.z);
        Vector3 P(float dx, float dz) => NoChao(t, c.x + dx, c.z + dz);
        // predios: (nome, deslocamento) olhando para a praca
        var pred = new (string n, float dx, float dz)[]
        {
            ("Taverna", 2, 24), ("Casa_Grande", -21, 15), ("Casa_Grande", 22, 14), ("Casa_Pequena", -27, 0), ("Casa_Pequena", -15, -21),
            ("Casa_Pequena", 9, -23), ("Casa_Pequena", 27, -10), ("Casa_Pequena", 28, 1),
        };
        foreach (var (n, dx, dz) in pred) { var b = Predio(n, P(dx, dz), centro, raiz); PropsDoPredio(t, b.transform, raiz); }
        Predio("Poco", centro, centro + Vector3.forward, raiz);
        Predio("Torre_Vigia", P(38, 28), centro, raiz);
        foreach (var (dx, dz) in new[] { (8f, 5f), (-9f, 6f), (6f, -9f) }) Predio("Barraca", P(dx, dz), centro, raiz);
        Predio("Fogueira_Troncos", P(-5, -5), centro, raiz);
        var pit = Prop(t, "stone_fire_pit", "stone_fire_pit", c.x - 5, c.z - 5, 1f, 0, raiz);
        Fogueira(raiz, P(-5, -5));
        // cercas formando um horto
        for (int i = 0; i < 6; i++) { Predio("Cerca_3m", P(-34 + i * 3, -34), P(-34 + i * 3, -30), raiz); }
        for (int i = 0; i < 4; i++) { var f = Predio("Cerca_3m", P(-34 + 18, -34 + 1.5f + i * 3), P(-40, -30 + i * 3), raiz); f.transform.rotation = Quaternion.Euler(0, 90, 0); }
        for (int i = 0; i < 3; i++) Predio("Muro_4m", P(-14 + i * 4, 33), centro, raiz);
        // postes com lanternas ao redor da praca
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2 + 0.3f; Poste(t, raiz, c.x + Mathf.Cos(a) * 11.5f, c.z + Mathf.Sin(a) * 11.5f);
        }
        Terracos(t, raiz);
        Debug.Log("[Ambiente] vila montada");
    }

    // ---------------------------------------------------------------- muro de pedra seca feito de PEDRAS MODELADAS (uma a uma)
    // fileiras empilhadas e alternadas (juntas desencontradas), pedras lascadas e irregulares, entulho ao pe do muro,
    // pedras de remate no topo e um caminho de lajes subindo cada rampa. Tudo fundido em poucas malhas (1 material).
    class Fundidor
    {
        public List<Mesh> pronto = new(); List<Vector3> v = new(), n = new(); List<Vector2> uv = new(); List<int> tri = new();
        public void Add(Mesh m, Vector3 pos, Quaternion rot, Vector3 esc, Vector2 uvOff, Mesh src = null)
        {
            var mv = m.vertices; var mn = m.normals; var mu = m.uv; var mt = m.triangles; int b = v.Count;
            for (int i = 0; i < mv.Length; i++)
            {
                v.Add(pos + rot * Vector3.Scale(mv[i], esc)); n.Add(rot * Vector3.Scale(mn[i], new Vector3(1f / esc.x, 1f / esc.y, 1f / esc.z)).normalized); uv.Add(mu[i] + uvOff);
            }
            foreach (var t in mt) tri.Add(b + t);
            if (v.Count > 60000) Fechar();
        }
        public void Fechar()
        {
            if (v.Count == 0) return;
            var me = new Mesh { name = "MuroPedras" }; me.SetVertices(v); me.SetNormals(n); me.SetUVs(0, uv); me.SetTriangles(tri, 0); me.RecalculateBounds(); pronto.Add(me);
            v = new(); n = new(); uv = new(); tri = new();
        }
    }

    static void Terracos(Terrain t, Transform pai)
    {
        var pedras = new List<Mesh>(); var lajes = new List<Mesh>();
        foreach (var f in Directory.GetFiles($"{Raiz}/Muro", "*.fbx").Select(x => x.Replace("\\", "/")))
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(f); if (mi.materialImportMode != ModelImporterMaterialImportMode.None || !mi.isReadable) { mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.isReadable = true; mi.SaveAndReimport(); }
            var me = AssetDatabase.LoadAssetAtPath<GameObject>(f).GetComponentInChildren<MeshFilter>().sharedMesh;
            (Path.GetFileName(f).StartsWith("Laje") ? lajes : pedras).Add(me);
        }
        if (pedras.Count == 0) return;
        var mat = MatPBR("Muro_Pedra", "Rock030", 1f, new Color(2.0f, 2.0f, 1.95f), 0.0f); mat.enableInstancing = false;
        var matBase = MatPBR("Muro_Miolo", "Rock030", 1f, new Color(0.7f, 0.7f, 0.7f), 0.0f);
        var fund = new Fundidor(); var r2 = new System.Random(2024); var miolo = new List<Vector3>(); var mverts = new List<Vector3>(); var mtris = new List<int>();
        float Alt(float raio, float a) { float x = VilaX + Mathf.Cos(a * Mathf.Deg2Rad) * raio, z = VilaZ + Mathf.Sin(a * Mathf.Deg2Rad) * raio; return t.SampleHeight(new Vector3(x, 0, z)); }
        Vector2 UO() => new Vector2((float)r2.NextDouble() * 3f, (float)r2.NextDouble() * 3f);
        int nPedras = 0, nEntulho = 0, nLajes = 0; float comprimento = 0;
        foreach (var R in TerR)
        {
            float raioF = R + 0.55f;
            // -------- miolo escuro (fecha os vaos entre as pedras)
            bool aberto = false;
            for (float a = -180f; a <= 180f; a += 0.7f / raioF * Mathf.Rad2Deg)
            {
                float x = VilaX + Mathf.Cos(a * Mathf.Deg2Rad) * raioF, z = VilaZ + Mathf.Sin(a * Mathf.Deg2Rad) * raioF;
                bool ok = SetorTerraco(x, z) > 0.6f && RampaTerraco(a) < 0.25f && x > 5 && z > 5 && x < Tam - 5 && z < Tam - 5;
                if (!ok) { aberto = false; continue; }
                float baixo = Alt(R - 0.4f, a) - 0.25f, topo = Alt(R + 2.4f, a) + 0.05f; if (topo - baixo < 0.4f) { aberto = false; continue; }
                var dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)); var p0 = new Vector3(x, 0, z) + dir * 0.35f;
                int i0 = mverts.Count; mverts.Add(new Vector3(p0.x, baixo, p0.z)); mverts.Add(new Vector3(p0.x, topo, p0.z));
                if (aberto) { int q = i0 - 2; mtris.AddRange(new[] { q, q + 1, i0 + 1, q, i0 + 1, i0 }); mtris.AddRange(new[] { i0, i0 + 1, q + 1, i0, q + 1, q }); }
                aberto = true;
            }
            // -------- pedras em fileiras
            float passoA = 1f / raioF * Mathf.Rad2Deg;
            for (int fileira = 0; fileira < 5; fileira++)
            {
                float a = -180f + (fileira % 2) * 0.5f * passoA + (float)r2.NextDouble() * passoA * 0.4f;
                while (a < 180f)
                {
                    float larg = Mathf.Lerp(0.85f, 1.45f, (float)r2.NextDouble()); float da = larg * 1.06f / raioF * Mathf.Rad2Deg;
                    float am = a + da / 2f; a += da;
                    float x = VilaX + Mathf.Cos(am * Mathf.Deg2Rad) * raioF, z = VilaZ + Mathf.Sin(am * Mathf.Deg2Rad) * raioF;
                    if (SetorTerraco(x, z) < 0.6f || RampaTerraco(am) > 0.25f || x < 5 || z < 5 || x > Tam - 5 || z > Tam - 5) continue;
                    float baixo = Alt(R - 0.4f, am) - 0.12f, topo = Alt(R + 2.4f, am) + 0.12f; float altura = topo - baixo; if (altura < 0.4f) continue;
                    float hf = 0.52f + (float)r2.NextDouble() * 0.12f; int nf = Mathf.Max(1, Mathf.CeilToInt(altura / hf));
                    if (fileira >= nf) continue;
                    bool remate = fileira == nf - 1;
                    float y = baixo + fileira * (altura / nf);
                    var dir = new Vector3(Mathf.Cos(am * Mathf.Deg2Rad), 0, Mathf.Sin(am * Mathf.Deg2Rad));
                    float recuo = (float)(r2.NextDouble() * 0.16 - 0.08) + (remate ? 0.05f : 0f);
                    var pos = new Vector3(x, y, z) + dir * recuo;
                    var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler((float)(r2.NextDouble() * 10 - 5), (float)(r2.NextDouble() * 16 - 8), (float)(r2.NextDouble() * 10 - 5));
                    var esc = new Vector3(larg, (altura / nf) * (1.15f + (float)r2.NextDouble() * 0.25f) / 0.66f, Mathf.Lerp(0.55f, 0.85f, (float)r2.NextDouble()));
                    if (remate) esc.y *= 0.8f;
                    fund.Add(pedras[r2.Next(pedras.Count)], pos, rot, esc, UO()); nPedras++;
                    comprimento += larg;
                    // entulho: pedra pequena caida ao pe do muro
                    if (fileira == 0 && r2.NextDouble() < 0.28)
                    {
                        float off = -(0.35f + (float)r2.NextDouble() * 0.7f); var pe = new Vector3(x, 0, z) + dir * off; pe.y = t.SampleHeight(pe) - 0.04f;
                        float e = Mathf.Lerp(0.22f, 0.5f, (float)r2.NextDouble());
                        fund.Add(pedras[r2.Next(pedras.Count)], pe, Quaternion.Euler((float)r2.NextDouble() * 30, (float)r2.NextDouble() * 360, (float)r2.NextDouble() * 30), new Vector3(e * 1.1f, e, e), UO()); nEntulho++;
                    }
                }
            }
            // -------- ESCADAS de pedra subindo cada rampa: degraus de ~0,2 m, cada um feito de 3 lajes grossas lado a lado + collider
            foreach (var ac in new[] { 75f, 112f, -75f, -112f })
            {
                var dir = new Vector3(Mathf.Cos(ac * Mathf.Deg2Rad), 0, Mathf.Sin(ac * Mathf.Deg2Rad)); var tan = new Vector3(-dir.z, 0, dir.x);
                Vector3 P(float r) => new Vector3(VilaX, 0, VilaZ) + dir * r;
                float rIni = R - 1.5f, rFim = R + 11.5f; float h0 = t.SampleHeight(P(rIni)); const float sub = 0.2f;
                if (SetorTerraco(P(R).x, P(R).z) < 0.5f || P(R).x < 8 || P(R).z < 8 || P(R).x > Tam - 8 || P(R).z > Tam - 8) continue;
                int nivelAtual = 0; float rComeco = rIni;
                for (float r = rIni; r <= rFim + 0.001f; r += 0.1f)
                {
                    float h = t.SampleHeight(P(r)); int nivel = Mathf.Max(0, Mathf.CeilToInt((h - h0) / sub - 0.12f));
                    bool ultimo = r + 0.1f > rFim;
                    if (nivel != nivelAtual || ultimo)
                    {
                        if (nivelAtual > 0 && r - rComeco > 0.15f)
                        {
                            float run = r - rComeco, rc = (r + rComeco) / 2f; var c = P(rc); float topo = h0 + nivelAtual * sub; float fundo = t.SampleHeight(P(rComeco)) - 0.35f;
                            float esp = topo - fundo;
                            for (int k3 = -1; k3 <= 1; k3 += 2)
                            {
                                var pos = c + tan * (k3 * 0.72f) + dir * ((float)(r2.NextDouble() * 0.06 - 0.03)); pos.y = fundo;
                                var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, (float)(r2.NextDouble() * 4 - 2), 0);
                                fund.Add(lajes[r2.Next(lajes.Count)], pos, rot, new Vector3(1.3f, esp / 0.23f, Mathf.Max(0.5f, run * 1.1f / 0.95f)), UO()); nLajes++;
                            }
                            var bc = new GameObject("Degrau").transform; bc.SetParent(pai); bc.position = new Vector3(c.x, (topo + fundo) / 2f, c.z); bc.rotation = Quaternion.LookRotation(dir, Vector3.up);
                            bc.gameObject.AddComponent<BoxCollider>().size = new Vector3(2.9f, esp, Mathf.Max(0.3f, run)); bc.gameObject.isStatic = true;
                        }
                        nivelAtual = nivel; rComeco = r;
                    }
                }
            }
        }
        fund.Fechar();
        var raiz = new GameObject("MuroDosTerracos").transform; raiz.SetParent(pai);
        int k = 0; foreach (var me in fund.pronto)
        {
            AssetDatabase.DeleteAsset($"{Raiz}/MuroPedras{k}.asset"); AssetDatabase.CreateAsset(me, $"{Raiz}/MuroPedras{k}.asset");
            var go = new GameObject("Pedras" + k++); go.transform.SetParent(raiz); go.AddComponent<MeshFilter>().sharedMesh = me; go.AddComponent<MeshRenderer>().sharedMaterial = mat; go.isStatic = true;
        }
        if (mverts.Count > 0)
        {
            var mm = new Mesh { name = "MuroMiolo", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mm.SetVertices(mverts); mm.SetTriangles(mtris, 0); mm.RecalculateNormals(); mm.RecalculateBounds();
            AssetDatabase.DeleteAsset($"{Raiz}/MuroMiolo.asset"); AssetDatabase.CreateAsset(mm, $"{Raiz}/MuroMiolo.asset");
            var go = new GameObject("Miolo"); go.transform.SetParent(raiz); go.AddComponent<MeshFilter>().sharedMesh = mm; go.AddComponent<MeshRenderer>().sharedMaterial = matBase;
            go.AddComponent<MeshCollider>().sharedMesh = mm; go.isStatic = true;           // colisao do muro
        }
        Debug.Log($"[Ambiente] muro: {nPedras} pedras, {nEntulho} entulhos, {nLajes} lajes do caminho, ~{comprimento / 5f:0} m");
    }

    static void PropsDoPredio(Terrain t, Transform b, Transform pai)
    {
        var rnd2 = new System.Random(b.GetHashCode());
        var itens = new (string fbx, string id, float esc)[]
        {
            ("wooden_barrels_01_1", "wooden_barrels_01", 0.9f), ("wine_barrel_01_1", "wine_barrel_01", 0.9f), ("wooden_crate_01_1", "wooden_crate_01", 1f),
            ("wooden_crate_02_1", "wooden_crate_02", 1f), ("wooden_bucket_01_1", "wooden_bucket_01", 1f), ("wicker_basket_01", "wicker_basket_01", 1f),
            ("wooden_stool_01", "wooden_stool_01", 1f), ("wooden_ladder_1", "wooden_ladder", 1f), ("jug_01", "jug_01", 1.4f), ("ceramic_pot", "ceramic_pot", 1f),
        };
        int n = 5;
        for (int i = 0; i < n; i++)
        {
            var it = itens[rnd2.Next(itens.Length)];
            // na frente do predio, ao lado da porta (local +Z = frente)
            var local = new Vector3((float)(rnd2.NextDouble() * 6 - 3), 0, 3.0f + (float)rnd2.NextDouble() * 2.2f);
            if (Mathf.Abs(local.x + 1.2f) < 1.0f) local.x += 2.2f;
            var w = b.TransformPoint(local);
            Prop(t, it.fbx, it.id, w.x, w.z, it.esc, (float)rnd2.NextDouble() * 360f, pai, true);
        }
    }

    static void Poste(Terrain t, Transform pai, float x, float z)
    {
        var poste = GameObject.CreatePrimitive(PrimitiveType.Cylinder); poste.name = "Poste"; poste.transform.SetParent(pai);
        poste.transform.localScale = new Vector3(0.14f, 1.1f, 0.14f); poste.transform.position = NoChao(t, x, z, 1.1f);
        poste.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Raiz}/Materiais/Pred_Madeira.mat"); poste.isStatic = true;
        var lan = Prop(t, "wooden_lantern_01_1", "wooden_lantern_01", x, z, 1.5f, 0, pai); if (lan) lan.transform.position = NoChao(t, x, z, 2.1f);
        var luz = new GameObject("LuzLanterna").AddComponent<Light>(); luz.transform.SetParent(pai); luz.transform.position = NoChao(t, x, z, 2.35f);
        luz.type = LightType.Point; luz.range = 11; luz.intensity = 4.5f; luz.color = new Color(1f, 0.66f, 0.32f); luz.shadows = LightShadows.None;
        luz.gameObject.AddComponent<FogoLuz>();
    }

    static void Fogueira(Transform pai, Vector3 pos)
    {
        var go = new GameObject("Fogueira"); go.transform.SetParent(pai); go.transform.position = pos + Vector3.up * 0.3f;
        var luz = go.AddComponent<Light>(); luz.type = LightType.Point; luz.range = 16; luz.intensity = 7f; luz.color = new Color(1f, 0.55f, 0.2f); luz.shadows = LightShadows.None;
        go.AddComponent<FogoLuz>();
        var ps = go.AddComponent<ParticleSystem>(); var main = ps.main; main.startLifetime = 1.1f; main.startSpeed = 1.4f; main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.15f, 0.9f), new Color(1f, 0.25f, 0.05f, 0.6f)); main.gravityModifier = -0.25f; main.maxParticles = 120;
        var em = ps.emission; em.rateOverTime = 45; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10; sh.radius = 0.25f;
        var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.5f), new GradientColorKey(new Color(0.2f, 0.1f, 0.05f), 1) },
                   new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0, 1) }); col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.1f));
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var pm = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        pm.SetFloat("_Surface", 1); pm.SetFloat("_Blend", 2); pm.SetFloat("_SrcBlend", 1); pm.SetFloat("_DstBlend", 1); pm.SetFloat("_ZWrite", 0); pm.renderQueue = 3000;
        pm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); pm.SetOverrideTag("RenderType", "Transparent");
        var tex = TexChama(); pm.SetTexture("_BaseMap", tex);
        AssetDatabase.DeleteAsset($"{Raiz}/Materiais/Chama.mat"); AssetDatabase.CreateAsset(pm, $"{Raiz}/Materiais/Chama.mat"); rend.sharedMaterial = pm;
    }

    static Texture2D TexChama()
    {
        var path = $"{Raiz}/Texturas/chama.png";
        if (!File.Exists(path))
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 32f; float a = Mathf.Clamp01(1 - d); a *= a; t.SetPixel(x, y, new Color(1, 1, 1, a)); }
            File.WriteAllBytes(path, t.EncodeToPNG()); AssetDatabase.ImportAsset(path);
        }
        return Tex(path);
    }

    // ---------------------------------------------------------------- arvores (casca + folhas em cartoes com recorte de alfa)
    static void Arvores(Terrain t)
    {
        var casca = MatPBR("Arv_Casca", "Bark001", 1f, new Color(0.80f, 0.74f, 0.68f), 0.08f);
        var folha = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var lp = $"{Raiz}/Texturas/leaf_BaseColor.png"; var li = (TextureImporter)AssetImporter.GetAtPath(lp); if (!li.alphaIsTransparency) { li.alphaIsTransparency = true; li.SaveAndReimport(); }
        folha.SetTexture("_BaseMap", Tex(lp)); folha.SetColor("_BaseColor", new Color(0.78f, 0.95f, 0.62f)); folha.SetFloat("_Smoothness", 0.08f);
        folha.SetFloat("_AlphaClip", 1f); folha.SetFloat("_Cutoff", 0.45f); folha.EnableKeyword("_ALPHATEST_ON"); folha.SetFloat("_Cull", 0f); folha.renderQueue = 2450;
        AssetDatabase.DeleteAsset($"{Raiz}/Materiais/Arv_Folha.mat"); AssetDatabase.CreateAsset(folha, $"{Raiz}/Materiais/Arv_Folha.mat");
        var folhaSeca = new Material(folha); folhaSeca.SetColor("_BaseColor", new Color(0.85f, 0.70f, 0.38f));
        foreach (var f in Directory.GetFiles($"{Raiz}/Arvores", "*.fbx").Select(x => x.Replace('\\', '/')))
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(f); mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; mi.addCollider = false; mi.SaveAndReimport();
            foreach (var mm in AssetDatabase.LoadAllAssetsAtPath(f).OfType<Material>())
            {
                if (mm.name == "Casca") mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mm.name), casca);
                if (mm.name == "Folha") mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mm.name), folha);
            }
            mi.SaveAndReimport();
        }
        var raiz = new GameObject("Arvores").transform; var r2 = new System.Random(321); var usados = new List<Vector2>(); int total = 0;
        // [campo, floresta, pantano, seco]
        var tipos = new (string nome, float[] bioma, int qtd, float emin, float emax)[]
        {
            ("Arvore_Carvalho", new[] { 0.5f, 1.0f, 0.05f, 0.0f }, 20, 0.9f, 1.35f), ("Arvore_Carvalho2", new[] { 0.5f, 1.0f, 0.1f, 0.0f }, 18, 0.9f, 1.3f),
            ("Arvore_Esguia", new[] { 0.1f, 1.0f, 0.1f, 0.0f }, 34, 0.9f, 1.4f),
            ("Arvore_Seca", new[] { 0.0f, 0.1f, 0.9f, 1.0f }, 26, 0.9f, 1.4f), ("Arvore_Seca2", new[] { 0.0f, 0.1f, 0.9f, 1.0f }, 26, 0.9f, 1.3f),
        };
        foreach (var tp in tipos)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Raiz}/Arvores/{tp.nome}.fbx"); if (!pf) continue;
            int feitos = 0;
            for (int tent = 0; tent < tp.qtd * 60 && feitos < tp.qtd; tent++)
            {
                float x = 6 + (float)r2.NextDouble() * (Tam - 12), z = 6 + (float)r2.NextDouble() * (Tam - 12);
                var b = Bioma(x / Tam, z / Tam); float pr = b[0] * tp.bioma[0] + b[1] * tp.bioma[1] + b[2] * tp.bioma[2] + b[3] * tp.bioma[3];
                if (r2.NextDouble() > pr) continue;
                if (DistCaminho(x, z) < 4.5f || Vector2.Distance(new Vector2(x, z), new Vector2(VilaX, VilaZ)) < 46f) continue;
                if (t.terrainData.GetSteepness(x / Tam, z / Tam) > 24f) continue;
                float y = t.SampleHeight(new Vector3(x, 0, z)); if (y < NivelAgua + 0.4f) continue;
                if (usados.Any(u => (u - new Vector2(x, z)).sqrMagnitude < 36f)) continue;                   // 6 m entre arvores
                usados.Add(new Vector2(x, z));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf); go.transform.SetParent(raiz);
                go.transform.position = new Vector3(x, y - 0.1f, z); go.transform.rotation = Quaternion.Euler(0, (float)r2.NextDouble() * 360f, 0);
                go.transform.localScale = Vector3.one * Mathf.Lerp(tp.emin, tp.emax, (float)r2.NextDouble());
                if (b[3] > 0.5f && !tp.nome.Contains("Seca")) { foreach (var rd in go.GetComponentsInChildren<Renderer>()) { var ms = rd.sharedMaterials; for (int i = 0; i < ms.Length; i++) if (ms[i] == folha) ms[i] = folhaSeca; rd.sharedMaterials = ms; } }
                var cc = go.AddComponent<CapsuleCollider>(); float esc = go.transform.localScale.y; cc.radius = 0.4f; cc.height = 5f; cc.center = new Vector3(0, 2.5f, 0);
                go.isStatic = true; feitos++; total++;
            }
        }
        Debug.Log("[Ambiente] arvores: " + total);
    }

    // ---------------------------------------------------------------- atmosfera medieval
    static void Atmosfera()
    {
        var sol = Object.FindFirstObjectByType<Light>(); if (sol == null) return;
        sol.type = LightType.Directional; sol.color = new Color(1f, 0.70f, 0.42f); sol.intensity = 2.6f; sol.shadows = LightShadows.Soft;
        sol.transform.rotation = Quaternion.Euler(11f, 235f, 0f); sol.shadowStrength = 0.9f;
        // ceu: HDRI de por do sol (Poly Haven CC0)
        var hdr = $"{Raiz}/Texturas/belfast_sunset_puresky.hdr";
        var sky = new Material(Shader.Find("Skybox/Panoramic"));
        sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(hdr)); sky.SetFloat("_Exposure", 0.65f); sky.SetFloat("_Rotation", RotacaoCeu);
        sky.SetFloat("_ImageType", 0); sky.SetFloat("_Mapping", 1);
        AssetDatabase.DeleteAsset($"{Raiz}/Materiais/Ceu.mat"); AssetDatabase.CreateAsset(sky, $"{Raiz}/Materiais/Ceu.mat");
        RenderSettings.skybox = sky; RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.40f, 0.43f, 0.55f); RenderSettings.ambientEquatorColor = new Color(0.42f, 0.30f, 0.24f); RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.08f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.0105f; RenderSettings.fogColor = new Color(0.62f, 0.48f, 0.42f);
        RenderSettings.sun = sol;
        // pos-processamento (URP Volume global)
        var vol = new GameObject("PosProcessamento").AddComponent<Volume>(); vol.isGlobal = true;
        var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        var tm = perfil.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var bl = perfil.Add<Bloom>(true); bl.threshold.Override(0.9f); bl.intensity.Override(0.55f); bl.scatter.Override(0.7f); bl.tint.Override(new Color(1f, 0.82f, 0.6f));
        var ca = perfil.Add<ColorAdjustments>(true); ca.postExposure.Override(-0.1f); ca.contrast.Override(14f); ca.saturation.Override(-14f); ca.colorFilter.Override(new Color(1f, 0.93f, 0.84f));
        var vg = perfil.Add<Vignette>(true); vg.intensity.Override(0.32f); vg.smoothness.Override(0.55f);
        var wb = perfil.Add<WhiteBalance>(true); wb.temperature.Override(14f); wb.tint.Override(-4f);
        var fg = perfil.Add<FilmGrain>(true); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.18f);
        var smh = perfil.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.85f, 0.9f, 1.1f, 0f)); smh.highlights.Override(new Vector4(1.1f, 0.98f, 0.85f, 0f));
        AssetDatabase.DeleteAsset($"{Raiz}/PosProcessamento.asset"); AssetDatabase.CreateAsset(perfil, $"{Raiz}/PosProcessamento.asset");
        foreach (var comp in perfil.components) AssetDatabase.AddObjectToAsset(comp, perfil);
        vol.sharedProfile = perfil; EditorUtility.SetDirty(perfil); AssetDatabase.SaveAssets();
    }
    const float RotacaoCeu = 20f;

    static void Bordas()
    {
        var pai = new GameObject("Bordas").transform; float m = 4f;
        foreach (var (pos, esc) in new[] { (new Vector3(Tam / 2, 20, -m / 2), new Vector3(Tam, 80, m)), (new Vector3(Tam / 2, 20, Tam + m / 2), new Vector3(Tam, 80, m)),
                                          (new Vector3(-m / 2, 20, Tam / 2), new Vector3(m, 80, Tam)), (new Vector3(Tam + m / 2, 20, Tam / 2), new Vector3(m, 80, Tam)) })
        { var b = new GameObject("Parede"); b.transform.SetParent(pai); b.transform.position = pos; b.AddComponent<BoxCollider>().size = esc; }
    }

    // ---------------------------------------------------------------- jogador
    static void Jogador(Terrain t)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CavaleiroSombrio/cavaleiro_sombrio_rigged.fbx");
        if (!pf) { Debug.LogWarning("[Ambiente] cavaleiro_sombrio_rigged.fbx nao encontrado: jogador nao criado"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf); go.name = "Jogador";
        var pos = NoChao(t, VilaX, VilaZ - 6f, 0.3f); go.transform.position = pos; go.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/CavaleiroSombrio/CavaleiroSombrio.controller");
        anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var cc = go.AddComponent<CharacterController>(); cc.height = 1.9f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.95f, 0); cc.stepOffset = 0.45f; cc.slopeLimit = 50;
        var ge = go.GetComponent<GuardaEspada>() ?? go.AddComponent<GuardaEspada>(); ge.AcharOssos(); ge.guardada = true;
        var jm = go.AddComponent<JogadorMapa>(); jm.nivelAgua = NivelAgua;
        var at = new GameObject("Atalhos").transform; var L = Lugares(); var lst = new List<Transform>(); var nomes = new List<string>();
        foreach (var (nome, pos2, olhar) in L)
        {
            var m = new GameObject(nome).transform; m.SetParent(at); m.position = NoChao(t, pos2.x, pos2.z, 0.4f);
            var d = olhar - pos2; d.y = 0; m.rotation = d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d) : Quaternion.identity; lst.Add(m); nomes.Add(nome);
        }
        jm.atalhos = lst.ToArray(); jm.nomesAtalhos = nomes.ToArray();
    }
}
