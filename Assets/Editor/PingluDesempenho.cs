// =====================================================================
//  PingluDesempenho.cs  -  Otimização para notebooks (vídeo integrado)
// ---------------------------------------------------------------------
//  Pinglu > 37. Otimizar para notebook
//   - sombras mais leves, sem SSAO, sem desfoque de movimento, HDR desligado
//   - terreno: menos grama, menos detalhes distantes, instanciamento
//   - corta árvores e objetos pequenos longe da câmera (LODGroup com distância)
//   - luzes sem sombra, fumaça/neblina com menos partículas, partículas pausam fora da tela
//  Rode com o Play desligado e salve a cena (Ctrl + S).
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PingluDesempenho
{
    [MenuItem("Pinglu/37. Otimizar para notebook")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        string r = Otimizar();
        EditorUtility.DisplayDialog("Pinglu", "Otimização feita:\n\n" + r + "\nSalve a cena com Ctrl + S e mande para o GitHub.\n" +
            "Dentro do jogo: F9 liga o modo leve. A resolução se ajusta sozinha se o FPS cair.", "OK");
    }

    // para rodar sem ninguém: Unity -batchmode -executeMethod PingluDesempenho.AplicarNaCenaEFechar
    public static void AplicarNaCenaEFechar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Debug.Log("[Desempenho]\n" + Otimizar());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    // chamado também pelo menu do cemitério
    public static string Otimizar()
    {
        string rel = "";
        rel += "- " + RemoverModelosPesados() + "\n";
        rel += "- " + Urp() + "\n";
        rel += "- " + Efeitos() + "\n";
        rel += "- " + Terreno() + "\n";
        rel += "- " + Cena() + "\n";
        AssetDatabase.SaveAssets();
        return rel;
    }

    // modelos escaneados (Poly Haven) com 40 mil a 225 mil triângulos cada eram o maior peso do jogo:
    // somem da floresta (as árvores e a decoração do cemitério continuam)
    static string RemoverModelosPesados()
    {
        GameObject cem = GameObject.Find("Mundo_Gerado");
        Transform pasta = cem != null ? cem.transform.Find("Cemiterio/Detalhes da Floresta") : null;
        if (pasta == null) return "Detalhes pesados da floresta: nada para remover";
        int n = 0;
        string[] pesados = { "root_cluster", "dead_tree_trunk", "tree_stump", "dry_branches" };
        for (int i = pasta.childCount - 1; i >= 0; i--)
        {
            Transform f = pasta.GetChild(i);
            foreach (string p in pesados)
                if (f.name.StartsWith(p)) { Object.DestroyImmediate(f.gameObject); n++; break; }
        }
        // das pedras com musgo (7 mil triângulos cada) mantém só uma parte
        int pedras = 0;
        for (int i = pasta.childCount - 1; i >= 0; i--)
        {
            Transform f = pasta.GetChild(i);
            if (!f.name.StartsWith("rock_moss")) continue;
            if (++pedras % 2 == 0) { Object.DestroyImmediate(f.gameObject); n++; }
        }
        return "Floresta: " + n + " modelos pesados removidos (raízes, troncos, tocos, galhos)";
    }

    // ------------------------------------------------------------------
    static string Urp()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            UniversalRenderPipelineAsset a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(caminho);
            if (a == null) continue;
            SerializedObject so = new SerializedObject(a);
            Definir(so, "m_MainLightShadowmapResolution", 1024);
            Definir(so, "m_ShadowDistance", 35f);
            Definir(so, "m_ShadowCascadeCount", 2);
            Definir(so, "m_SoftShadowsSupported", false);
            Definir(so, "m_AdditionalLightShadowsSupported", false);
            Definir(so, "m_AdditionalLightsPerObjectLimit", 2);
            Definir(so, "m_SupportsHDR", false);
            Definir(so, "m_ColorGradingLutSize", 16);
            Definir(so, "m_RenderScale", 0.9f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(a);

            // renderer: tira o SSAO (muito pesado para vídeo integrado)
            SerializedProperty lista = so.FindProperty("m_RendererDataList");
            for (int i = 0; lista != null && i < lista.arraySize; i++)
            {
                ScriptableRendererData rd = lista.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                if (rd == null) continue;
                foreach (ScriptableRendererFeature f in rd.rendererFeatures)
                    if (f != null && f.name.ToLower().Contains("ambientocclusion")) { f.SetActive(false); EditorUtility.SetDirty(rd); }
            }
            n++;
        }
        return "URP (" + n + " assets): sombras 1024 / 2 cascatas / 35 m, sem sombra suave, sem SSAO, sem HDR, escala 90%";
    }

    static void Definir(SerializedObject so, string nome, object valor)
    {
        SerializedProperty p = so.FindProperty(nome);
        if (p == null) return;
        if (valor is int) p.intValue = (int)valor;
        else if (valor is float) p.floatValue = (float)valor;
        else if (valor is bool) p.boolValue = (bool)valor;
    }

    static string Efeitos()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:VolumeProfile"))
        {
            VolumeProfile vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (vp == null) continue;
            MotionBlur mb;
            if (vp.TryGet(out mb)) { mb.active = false; n++; }
            DepthOfField dof;
            if (vp.TryGet(out dof)) { dof.active = false; n++; }
            Bloom bl;
            if (vp.TryGet(out bl)) { bl.highQualityFiltering.Override(false); bl.highQualityFiltering.overrideState = true; }
            EditorUtility.SetDirty(vp);
        }
        return "Pós-processamento: desfoque de movimento e profundidade de campo desligados (" + n + "), bloom leve";
    }

    static string Terreno()
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null) return "Terreno não encontrado";
        t.detailObjectDistance = 60f;
        t.detailObjectDensity = 0.5f;
        t.treeDistance = 600f;
        t.treeBillboardDistance = 40f;
        t.basemapDistance = 100f;
        t.heightmapPixelError = 10f;
        t.drawInstanced = true;
        t.shadowCastingMode = ShadowCastingMode.Off;
        EditorUtility.SetDirty(t);
        return "Terreno: grama a 60 m com metade da densidade, pixel error 10, instanciado, sem projetar sombra";
    }

    // ------------------------------------------------------------------
    static string Cena()
    {
        int lods = 0, luzes = 0, particulas = 0, semSombra = 0;
        Camera cam = Camera.main;
        if (cam != null) { cam.farClipPlane = 320f; EditorUtility.SetDirty(cam); }

        // 1) objetos de natureza: cortados por distância (a neblina esconde o corte)
        string[] raizes = { "Mundo_Gerado", "Biomas_Gerados", "Cenario_Gerado" };
        foreach (string nome in raizes)
        {
            GameObject r = GameObject.Find(nome);
            if (r == null) continue;
            foreach (Transform t in r.transform) lods += CortarPorDistancia(t, nome == "Mundo_Gerado");
        }
        // 2) luzes sem sombra
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) continue;
            if (l.shadows != LightShadows.None) { l.shadows = LightShadows.None; semSombra++; }
            luzes++;
        }
        // 3) partículas: pausam fora da tela; a neblina grande fica mais rala
        foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            if (ps.name.StartsWith("Nevoa"))
            {
                main.maxParticles = Mathf.Min(main.maxParticles, ps.name == "Nevoa Rasteira" ? 70 : 100);
                var em = ps.emission; em.rateOverTimeMultiplier *= 0.5f;
            }
            particulas++;
        }
        if (lods + luzes + particulas > 0) EditorSceneManager.MarkAllScenesDirty();
        return "Cena: " + lods + " objetos com corte por distância, " + semSombra + "/" + luzes + " luzes sem sombra, " + particulas + " sistemas de partículas ajustados";
    }

    // raiz = pasta (Natureza, Floresta, Biomas...) ou objeto direto; aplica nos objetos com malha dentro
    static int CortarPorDistancia(Transform pasta, bool soNatureza)
    {
        int n = 0;
        string nome = pasta.name.ToLower();
        bool ehPasta = pasta.childCount > 0 && pasta.GetComponent<Renderer>() == null && !PrefabUtility.IsAnyPrefabInstanceRoot(pasta.gameObject);
        if (soNatureza && ehPasta)
        {
            // dentro do Mundo_Gerado só mexe na natureza e no cemitério (floresta e detalhes), nunca nos prédios
            bool pode = nome == "natureza" || nome == "cemiterio";
            if (!pode) return 0;
        }
        if (ehPasta)
        {
            if (nome == "cemiterio")
            {   // só as pastas da floresta; a estrutura do cemitério fica inteira
                foreach (Transform f in pasta)
                    if (f.name == "Floresta Assombrada" || f.name == "Detalhes da Floresta" || f.name == "Arvores do Cemiterio")
                        foreach (Transform filho in f) n += CortarObjeto(filho);
                return n;
            }
            foreach (Transform filho in pasta) n += CortarPorDistancia(filho, false);
            return n;
        }
        return CortarObjeto(pasta);
    }

    static int CortarObjeto(Transform t)
    {
        GameObject go = t.gameObject;
        if (go.GetComponentInParent<LODGroup>() != null || go.GetComponentInChildren<LODGroup>() != null) return 0;
        if (go.GetComponentInChildren<ParticleSystem>() != null || go.GetComponentInChildren<Light>() != null) return 0;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        float tamanho = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (tamanho > 40f) return 0;                       // construções grandes ficam como estão
        string n = go.name.ToLower();
        bool arvore = n.Contains("arvore") || n.Contains("tree") || n.Contains("pine") || tamanho > 7f;
        float dist = arvore ? 150f : (tamanho < 3f ? 65f : 110f);
        float frac = Mathf.Clamp(tamanho / (1.155f * dist), 0.004f, 0.6f);
        if (!arvore) foreach (Renderer r in rs) r.shadowCastingMode = ShadowCastingMode.Off;   // pedras, troncos e arbustos não projetam sombra
        LODGroup g = go.AddComponent<LODGroup>();
        g.SetLODs(new[] { new LOD(frac, rs) });
        g.RecalculateBounds();
        return 1;
    }
}
#endif
