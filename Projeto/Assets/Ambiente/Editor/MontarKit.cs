using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Usa o Stylized Nature MegaKit (Quaternius, CC0) para arvores, pedras, arbustos, plantas, flores e cogumelos,
// misturado com o que ja foi feito (terreno, grama propria, agua, muro/escadas, vila, juncos).
public static partial class MontarAmbiente
{
    const string KitRaiz = Raiz + "/Kit";
    static bool UsarKit => System.Environment.GetEnvironmentVariable("AMBIENTE_SEM_KIT") == null && Directory.Exists(KitRaiz + "/Modelos");

    static Dictionary<string, Material> MatsKit;

    static Texture2D TexKit(string nome, bool normal = false, bool alpha = false)
    {
        var p = $"{KitRaiz}/Texturas/{nome}.png"; if (!File.Exists(p)) return null;
        var ti = (TextureImporter)AssetImporter.GetAtPath(p);
        if (ti != null && (normal && ti.textureType != TextureImporterType.NormalMap || alpha && !ti.alphaIsTransparency))
        { if (normal) ti.textureType = TextureImporterType.NormalMap; if (alpha) ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    static Material MKit(string nome, string cor, string normal, bool folha, Color tint, float suave)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", TexKit(cor, false, folha)); m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", suave); m.SetFloat("_Metallic", 0f);
        if (normal != null) { var n = TexKit(normal, true); if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.8f); } }
        if (folha) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0f); m.renderQueue = 2450; }
        AssetDatabase.DeleteAsset($"{Raiz}/Materiais/Kit_{nome}.mat"); AssetDatabase.CreateAsset(m, $"{Raiz}/Materiais/Kit_{nome}.mat");
        return m;
    }

    static void PrepararKit()
    {
        // tons um pouco mais frios/sujos para o clima medieval; rochas cinza
        var d = new Dictionary<string, Material>();
        d["Bark_NormalTree"] = MKit("Casca1", "Bark_NormalTree", "Bark_NormalTree_Normal", false, new Color(0.85f, 0.82f, 0.78f), 0.05f);
        d["Bark_DeadTree"] = MKit("Casca2", "Bark_DeadTree", "Bark_DeadTree_Normal", false, new Color(0.8f, 0.78f, 0.75f), 0.05f);
        d["Bark_TwistedTree"] = MKit("Casca3", "Bark_TwistedTree", "Bark_TwistedTree_Normal", false, new Color(0.8f, 0.78f, 0.75f), 0.05f);
        d["Leaves_NormalTree"] = MKit("Folha1", "Leaves_NormalTree_C", null, true, new Color(0.80f, 0.88f, 0.66f), 0.05f);
        d["Leaves_Pine"] = MKit("Pinheiro", "Leaf_Pine_C", null, true, new Color(0.72f, 0.84f, 0.66f), 0.05f);
        d["Leaves_TwistedTree"] = MKit("Folha3", "Leaves_TwistedTree_C", null, true, new Color(0.74f, 0.80f, 0.56f), 0.05f);
        d["Leaves"] = MKit("Plantas", "Leaves", null, true, new Color(0.78f, 0.9f, 0.68f), 0.05f);
        d["Flowers"] = MKit("Flores", "Flowers", null, true, new Color(0.92f, 0.9f, 0.88f), 0.05f);
        d["Mushrooms"] = MKit("Cogumelos", "Mushrooms", null, false, new Color(0.9f, 0.88f, 0.85f), 0.1f);
        d["Rocks"] = MKit("Rochas", "Rocks_Diffuse", null, false, new Color(0.92f, 0.92f, 0.92f), 0.0f);
        d["PathRocks"] = MKit("PedrasTrilha", "PathRocks_Diffuse", null, false, new Color(0.9f, 0.9f, 0.9f), 0.0f);
        d["Grass"] = MKit("Grama", "Grass", null, false, new Color(0.8f, 0.9f, 0.7f), 0.0f);
        MatsKit = d;
    }

    static GameObject InstKit(string nome, Transform pai, Vector3 pos, float rotY, float alturaAlvo, float jitter, Quaternion? alinhar = null)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitRaiz}/Modelos/{nome}.fbx"); if (!pf) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf); go.transform.SetParent(pai);
        go.transform.position = pos; go.transform.rotation = (alinhar ?? Quaternion.identity) * Quaternion.Euler(0, rotY, 0);
        foreach (var rd in go.GetComponentsInChildren<Renderer>())
        {
            var ms = rd.sharedMaterials; bool mudou = false;
            for (int i = 0; i < ms.Length; i++) { var chave = ms[i] != null ? ms[i].name : ""; if (MatsKit.TryGetValue(chave, out var novo)) { ms[i] = novo; mudou = true; } }
            if (mudou) rd.sharedMaterials = ms;
        }
        var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float h = Mathf.Max(0.01f, b.size.y); go.transform.localScale = Vector3.one * (alturaAlvo * jitter / h);
        return go;
    }

    class GrupoKit
    {
        public string[] nomes; public float[] bioma; public int qtd; public float hMin, hMax; public float declMax = 28; public float espaco = 0; public bool arvore, rocha, evitaCaminho = true, naAgua;
        public float alinhar = 0f; public float foraVila = 46f;
    }

    static void Kit(Terrain t)
    {
        if (!UsarKit) return;
        PrepararKit();
        var raiz = new GameObject("Kit_Natureza").transform; var r2 = new System.Random(777); var usados = new List<Vector3>(); int total = 0;
        string[] N(string pre, int n) => Enumerable.Range(1, n).Select(i => $"{pre}_{i}").ToArray();
        var grupos = new List<GrupoKit>
        {
            // [campo, floresta, pantano, seco]
            new GrupoKit { nomes = N("CommonTree", 5), bioma = new[] { 0.45f, 1.0f, 0.05f, 0.0f }, qtd = 55, hMin = 7f, hMax = 11f, espaco = 6.5f, arvore = true },
            new GrupoKit { nomes = N("Pine", 5),       bioma = new[] { 0.05f, 1.0f, 0.1f, 0.15f }, qtd = 55, hMin = 10f, hMax = 16f, espaco = 6f, arvore = true },
            new GrupoKit { nomes = N("TwistedTree", 5), bioma = new[] { 0.0f, 0.25f, 0.9f, 0.55f }, qtd = 30, hMin = 6f, hMax = 9f, espaco = 6f, arvore = true, naAgua = true },
            new GrupoKit { nomes = N("DeadTree", 5),   bioma = new[] { 0.0f, 0.1f, 0.9f, 1.0f }, qtd = 42, hMin = 5f, hMax = 9f, espaco = 5.5f, arvore = true, naAgua = true },
            // pedras grandes e redondas, cinzas
            new GrupoKit { nomes = N("Rock_Medium", 3), bioma = new[] { 0.35f, 0.55f, 0.3f, 0.9f }, qtd = 55, hMin = 1.2f, hMax = 3.4f, espaco = 9f, rocha = true, declMax = 45, evitaCaminho = false },
            new GrupoKit { nomes = N("Pebble_Round", 5).Concat(N("Pebble_Square", 6)).ToArray(), bioma = new[] { 0.7f, 0.5f, 0.4f, 1.0f }, qtd = 170, hMin = 0.18f, hMax = 0.45f, declMax = 50, evitaCaminho = false },
            // vegetacao baixa
            new GrupoKit { nomes = new[] { "Bush_Common", "Bush_Common_Flowers" }, bioma = new[] { 0.9f, 1.0f, 0.2f, 0.1f }, qtd = 90, hMin = 0.9f, hMax = 1.7f, espaco = 2.5f },
            new GrupoKit { nomes = new[] { "Fern_1", "Plant_1", "Plant_1_Big", "Plant_7", "Plant_7_Big" }, bioma = new[] { 0.15f, 1.0f, 0.8f, 0.0f }, qtd = 190, hMin = 0.6f, hMax = 1.2f },
            new GrupoKit { nomes = new[] { "Flower_3_Group", "Flower_4_Group", "Flower_3_Single", "Flower_4_Single" }, bioma = new[] { 1.0f, 0.3f, 0.0f, 0.0f }, qtd = 130, hMin = 0.35f, hMax = 0.7f },
            new GrupoKit { nomes = new[] { "Mushroom_Common", "Mushroom_Laetiporus" }, bioma = new[] { 0.05f, 1.0f, 0.6f, 0.0f }, qtd = 55, hMin = 0.22f, hMax = 0.45f },
        };
        foreach (var g in grupos)
        {
            int feitos = 0; Vector3 centro = Vector3.zero;
            for (int tent = 0; tent < g.qtd * 80 && feitos < g.qtd; tent++)
            {
                float x = 6 + (float)r2.NextDouble() * (Tam - 12), z = 6 + (float)r2.NextDouble() * (Tam - 12);
                var b = Bioma(x / Tam, z / Tam); float pr = b[0] * g.bioma[0] + b[1] * g.bioma[1] + b[2] * g.bioma[2] + b[3] * g.bioma[3];
                if (r2.NextDouble() > pr) continue;
                if (g.foraVila > 0 && Vector2.Distance(new Vector2(x, z), new Vector2(VilaX, VilaZ)) < g.foraVila) continue;
                if (g.evitaCaminho && DistCaminho(x, z) < 4.5f) continue;
                if (t.terrainData.GetSteepness(x / Tam, z / Tam) > g.declMax) continue;
                float y = t.SampleHeight(new Vector3(x, 0, z)); if (y < NivelAgua + (g.naAgua ? 0.1f : 0.45f)) continue;
                if (g.espaco > 0 && usados.Any(u => (new Vector2(u.x, u.z) - new Vector2(x, z)).sqrMagnitude < g.espaco * g.espaco)) continue;
                if (g.espaco > 0) usados.Add(new Vector3(x, y, z));
                var nrm = t.terrainData.GetInterpolatedNormal(x / Tam, z / Tam);
                var alin = g.rocha || g.alinhar > 0 ? Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, nrm), g.rocha ? 0.5f : g.alinhar) : Quaternion.identity;
                float h = Mathf.Lerp(g.hMin, g.hMax, (float)r2.NextDouble());
                var go = InstKit(g.nomes[r2.Next(g.nomes.Length)], raiz, new Vector3(x, y - (g.rocha ? 0.18f : 0.04f), z), (float)r2.NextDouble() * 360f, h, 1f, alin);
                if (go == null) break;
                if (g.arvore) AddTronco(go);
                else if (g.rocha) { var mc = go.GetComponentInChildren<MeshFilter>(); if (mc) { var col = mc.gameObject.AddComponent<MeshCollider>(); col.sharedMesh = mc.sharedMesh; } }
                go.isStatic = true; feitos++; total++;
            }
        }
        // trilha de pedras chatas (RockPath) no meio da estrada de barro, em zigue-zague, como na trilha da Zelda
        var pathNomes = new[] { "RockPath_Round_Wide", "RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3", "RockPath_Square_Wide", "RockPath_Square_Small_1", "RockPath_Square_Small_2", "RockPath_Square_Small_3" };
        int np = 0; float lado = 1;
        for (float x = 14; x < Tam - 14; x += 2.1f + (float)r2.NextDouble() * 1.2f)
        {
            float z = CaminhoZ(x) + lado * (0.5f + (float)r2.NextDouble() * 0.5f); lado = -lado; float xx = x + (float)(r2.NextDouble() - 0.5);
            if (Vector2.Distance(new Vector2(xx, z), new Vector2(VilaX, VilaZ)) < 34f) continue;
            float y = t.SampleHeight(new Vector3(xx, 0, z)); if (y < NivelAgua + 0.4f) continue;
            var nrm = t.terrainData.GetInterpolatedNormal(xx / Tam, z / Tam);
            var go = InstKit(pathNomes[r2.Next(pathNomes.Length)], raiz, new Vector3(xx, y - 0.04f, z), (float)r2.NextDouble() * 360f, 0.28f, 1f, Quaternion.FromToRotation(Vector3.up, nrm));
            if (go == null) break;
            // as lajes do kit sao largas: normaliza pela largura (~1.3 m) em vez da altura
            var rs = go.GetComponentsInChildren<Renderer>(); var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            float larg = Mathf.Max(bb.size.x, bb.size.z); go.transform.localScale *= Mathf.Clamp(1.4f / Mathf.Max(0.01f, larg), 0.2f, 20f) * (0.85f + (float)r2.NextDouble() * 0.5f);
            go.transform.position = new Vector3(xx, y - 0.04f, z); go.isStatic = true; np++; total++;
        }
        Debug.Log($"[Ambiente] kit natureza: {total} itens ({np} pedras de trilha)");
    }
}
