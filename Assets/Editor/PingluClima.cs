// =====================================================================
//  PingluClima.cs  -  Clima sombrio e nublado (Pinglu RPG)
// ---------------------------------------------------------------------
//  Pinglu > 12. Clima sombrio        -> instala o clima novo
//  Pinglu > 13. Voltar clima anterior -> desfaz tudo
//
//  Depois de instalar, clique no objeto "Clima Sombrio" na Hierarchy e
//  ajuste "Escuridão", "Neblina" e "Cinzento" no Inspector.
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PingluClima
{
    const string PASTA = "Assets/Cenario/Clima";
    const string TEX_CEU = PASTA + "/ceu_nublado.png";
    const string MAT_CEU = PASTA + "/Ceu_Nublado.mat";
    const string PERFIL = PASTA + "/Clima_Sombrio_PosProcessamento.asset";
    const string BACKUP = PASTA + "/Backup_Clima.json";
    const string NOME = "Clima Sombrio";
    const string MAT_PARTICULA = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    [System.Serializable]
    class Guardado
    {
        public Quaternion solRotacao; public Color solCor; public float solIntensidade, solSombra;
        public bool fog; public FogMode fogModo; public Color fogCor; public float fogDensidade;
        public AmbientMode ambienteModo; public Color ambCeu, ambEquador, ambChao; public float ambIntensidade, reflexo;
        public string ceuCaminho;
        public List<Color> gramaSaudavel = new List<Color>(), gramaSeca = new List<Color>();
    }

    [MenuItem("Pinglu/12. Clima sombrio")]
    public static void Instalar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro, rode o menu de novo e salve a cena.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_CEU) == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei a imagem do céu em " + TEX_CEU + ". Extraiu o zip na pasta do projeto?", "OK");
            return;
        }
        Light sol = RenderSettings.sun;
        if (sol == null) { GameObject g = GameObject.Find("Directional Light"); if (g != null) sol = g.GetComponent<Light>(); }
        Terrain terreno = Terrain.activeTerrain;

        // 1) guarda o clima de agora (só na primeira vez, para sempre poder voltar ao original)
        if (!System.IO.File.Exists(BACKUP))
        {
            Guardado b = new Guardado();
            if (sol != null) { b.solRotacao = sol.transform.rotation; b.solCor = sol.color; b.solIntensidade = sol.intensity; b.solSombra = sol.shadowStrength; }
            b.fog = RenderSettings.fog; b.fogModo = RenderSettings.fogMode; b.fogCor = RenderSettings.fogColor; b.fogDensidade = RenderSettings.fogDensity;
            b.ambienteModo = RenderSettings.ambientMode; b.ambCeu = RenderSettings.ambientSkyColor; b.ambEquador = RenderSettings.ambientEquatorColor;
            b.ambChao = RenderSettings.ambientGroundColor; b.ambIntensidade = RenderSettings.ambientIntensity; b.reflexo = RenderSettings.reflectionIntensity;
            b.ceuCaminho = RenderSettings.skybox != null ? AssetDatabase.GetAssetPath(RenderSettings.skybox) : "";
            if (terreno != null)
                foreach (DetailPrototype dp in terreno.terrainData.detailPrototypes) { b.gramaSaudavel.Add(dp.healthyColor); b.gramaSeca.Add(dp.dryColor); }
            System.IO.File.WriteAllText(BACKUP, JsonUtility.ToJson(b, true));
            AssetDatabase.ImportAsset(BACKUP);
        }

        // 2) céu nublado
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(TEX_CEU);
        if (ti.mipmapEnabled || ti.wrapModeV != TextureWrapMode.Clamp || ti.maxTextureSize < 2048)
        {
            ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }
        Material ceu = AssetDatabase.LoadAssetAtPath<Material>(MAT_CEU);
        Shader shCeu = Shader.Find("Skybox/Panoramic");
        if (ceu == null) { ceu = new Material(shCeu); AssetDatabase.CreateAsset(ceu, MAT_CEU); }
        ceu.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_CEU));
        if (ceu.HasProperty("_Mapping")) ceu.SetFloat("_Mapping", 1f); // latitude-longitude
        EditorUtility.SetDirty(ceu);

        // 3) pós-processamento: frio, menos saturado, contraste, vinheta, brilho nas luzes
        VolumeProfile perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PERFIL);
        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, PERFIL);
            Tonemapping tm = perfil.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments ca = perfil.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.2f); ca.contrast.Override(10f); ca.saturation.Override(-25f); ca.colorFilter.Override(new Color(0.93f, 0.97f, 1f));
            SplitToning st = perfil.Add<SplitToning>(true);
            st.shadows.Override(new Color(0.42f, 0.52f, 0.60f)); st.highlights.Override(new Color(0.64f, 0.56f, 0.46f)); st.balance.Override(-15f);
            Vignette vg = perfil.Add<Vignette>(true); vg.intensity.Override(0.32f); vg.smoothness.Override(0.45f);
            Bloom bl = perfil.Add<Bloom>(true); bl.threshold.Override(0.85f); bl.intensity.Override(0.7f); bl.scatter.Override(0.65f);
            FilmGrain fg = perfil.Add<FilmGrain>(true); fg.type.Override(FilmGrainLookup.Medium1); fg.intensity.Override(0.18f);
            foreach (VolumeComponent c in perfil.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, perfil);
            }
            EditorUtility.SetDirty(perfil);
        }

        // 4) objeto do clima na cena
        GameObject antigo = GameObject.Find(NOME);
        if (antigo != null) Undo.DestroyObjectImmediate(antigo);
        GameObject go = new GameObject(NOME);
        Undo.RegisterCreatedObjectUndo(go, "Clima sombrio");
        Volume vol = go.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 5f; vol.sharedProfile = perfil;
        ClimaSombrio clima = go.AddComponent<ClimaSombrio>();
        clima.sol = sol; clima.ceu = ceu; clima.volume = vol;
        clima.materialParticula = AssetDatabase.LoadAssetAtPath<Material>(MAT_PARTICULA);
        clima.Aplicar();

        // 5) grama menos "verde neon"
        if (terreno != null)
        {
            Guardado b = JsonUtility.FromJson<Guardado>(System.IO.File.ReadAllText(BACKUP));
            DetailPrototype[] dps = terreno.terrainData.detailPrototypes;
            Undo.RecordObject(terreno.terrainData, "Clima sombrio");
            for (int i = 0; i < dps.Length; i++)
            {
                Color s = i < b.gramaSaudavel.Count ? b.gramaSaudavel[i] : dps[i].healthyColor;
                Color d = i < b.gramaSeca.Count ? b.gramaSeca[i] : dps[i].dryColor;
                dps[i].healthyColor = new Color(s.r * 0.62f, s.g * 0.66f, s.b * 0.55f, s.a);
                dps[i].dryColor = new Color(d.r * 0.68f, d.g * 0.62f, d.b * 0.50f, d.a);
            }
            terreno.terrainData.detailPrototypes = dps;
            EditorUtility.SetDirty(terreno.terrainData);
        }

        DynamicGI.UpdateEnvironment();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Pinglu", "Clima sombrio instalado!\n\nO objeto \"Clima Sombrio\" está selecionado: no Inspector, mexa em " +
            "Escuridão (0 = dia nublado, 1 = quase noite), Neblina e Cinzento até ficar do jeito que vocês querem.\n\n" +
            "A névoa rasteira e a poeira no ar aparecem durante o Play.\n\nSalve a cena com Ctrl + S.", "OK");
    }

    [MenuItem("Pinglu/13. Voltar clima anterior")]
    public static void Voltar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (!System.IO.File.Exists(BACKUP)) { EditorUtility.DisplayDialog("Pinglu", "Não achei o backup do clima anterior.", "OK"); return; }
        Guardado b = JsonUtility.FromJson<Guardado>(System.IO.File.ReadAllText(BACKUP));

        GameObject go = GameObject.Find(NOME);
        if (go != null) Undo.DestroyObjectImmediate(go);

        Light sol = RenderSettings.sun;
        if (sol == null) { GameObject g = GameObject.Find("Directional Light"); if (g != null) sol = g.GetComponent<Light>(); }
        if (sol != null)
        {
            Undo.RecordObject(sol, "Voltar clima"); Undo.RecordObject(sol.transform, "Voltar clima");
            sol.transform.rotation = b.solRotacao; sol.color = b.solCor; sol.intensity = b.solIntensidade; sol.shadowStrength = b.solSombra;
        }
        RenderSettings.fog = b.fog; RenderSettings.fogMode = b.fogModo; RenderSettings.fogColor = b.fogCor; RenderSettings.fogDensity = b.fogDensidade;
        RenderSettings.ambientMode = b.ambienteModo; RenderSettings.ambientSkyColor = b.ambCeu; RenderSettings.ambientEquatorColor = b.ambEquador;
        RenderSettings.ambientGroundColor = b.ambChao; RenderSettings.ambientIntensity = b.ambIntensidade; RenderSettings.reflectionIntensity = b.reflexo;
        RenderSettings.skybox = string.IsNullOrEmpty(b.ceuCaminho) ? null : AssetDatabase.LoadAssetAtPath<Material>(b.ceuCaminho);

        Terrain terreno = Terrain.activeTerrain;
        if (terreno != null)
        {
            DetailPrototype[] dps = terreno.terrainData.detailPrototypes;
            Undo.RecordObject(terreno.terrainData, "Voltar clima");
            for (int i = 0; i < dps.Length && i < b.gramaSaudavel.Count; i++) { dps[i].healthyColor = b.gramaSaudavel[i]; dps[i].dryColor = b.gramaSeca[i]; }
            terreno.terrainData.detailPrototypes = dps;
            EditorUtility.SetDirty(terreno.terrainData);
        }
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Pinglu", "Clima anterior restaurado. Salve a cena com Ctrl + S.", "OK");
    }
}
#endif
