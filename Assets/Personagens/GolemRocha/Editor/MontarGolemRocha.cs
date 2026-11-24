#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Menu  Golem > Montar tudo : configura o FBX (rig Generic), cria os materiais (pedra com rachaduras de lava + olhos), os clipes (loop onde precisa)
// e o prefab com colisor. Menu  Golem > Cena de teste : chao + luz + golem; Play e teclas 1-9 e 0.
public static class MontarGolemRocha
{
    static string Pasta()
    {
        var g = AssetDatabase.FindAssets("GolemRocha t:Model").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("/GolemRocha.fbx"));
        return g == null ? null : Path.GetDirectoryName(g).Replace("\\", "/");
    }

    static Texture2D Tex(string caminho, bool normal = false, bool srgb = true)
    {
        var ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (ti != null)
        {
            bool mudou = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; mudou = true; }
            if (!normal && ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; mudou = true; }
            if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; mudou = true; }
            if (mudou) ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
    }

    static Material Criar(string caminho)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard"); m = new Material(sh); AssetDatabase.CreateAsset(m, caminho); }
        return m;
    }

    [MenuItem("Golem Rocha/Montar tudo")]
    public static void Montar()
    {
        string pasta = Pasta(); if (pasta == null) { Debug.LogError("[Golem] Golem.fbx nao encontrado."); return; }
        string fbx = pasta + "/GolemRocha.fbx";

        // materiais
        var pedra = Criar(pasta + "/Golem_Pedra.mat");
        var cor = Tex(pasta + "/Golem_cor.jpg"); var nrm = Tex(pasta + "/Golem_normal.png", true); var emi = Tex(pasta + "/Golem_emissivo.jpg");
        if (pedra.HasProperty("_BaseMap")) pedra.SetTexture("_BaseMap", cor); if (pedra.HasProperty("_MainTex")) pedra.SetTexture("_MainTex", cor);
        if (pedra.HasProperty("_BumpMap")) { pedra.SetTexture("_BumpMap", nrm); pedra.EnableKeyword("_NORMALMAP"); if (pedra.HasProperty("_BumpScale")) pedra.SetFloat("_BumpScale", 1.2f); }
        if (pedra.HasProperty("_EmissionMap")) { pedra.SetTexture("_EmissionMap", emi); pedra.SetColor("_EmissionColor", Color.white * 0.8f); pedra.EnableKeyword("_EMISSION"); pedra.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        if (pedra.HasProperty("_Smoothness")) pedra.SetFloat("_Smoothness", 0.3f); if (pedra.HasProperty("_Metallic")) pedra.SetFloat("_Metallic", 0f);
        var olhos = Criar(pasta + "/Golem_Olhos.mat");
        var eEmi = Tex(pasta + "/Golem_olhos_emissivo.png"); var eNrm = Tex(pasta + "/Golem_olhos_normal.png", true);
        if (olhos.HasProperty("_BaseColor")) olhos.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f));
        if (olhos.HasProperty("_BumpMap")) { olhos.SetTexture("_BumpMap", eNrm); olhos.EnableKeyword("_NORMALMAP"); }
        if (olhos.HasProperty("_EmissionMap")) { olhos.SetTexture("_EmissionMap", eEmi); olhos.SetColor("_EmissionColor", Color.white * 3f); olhos.EnableKeyword("_EMISSION"); }
        EditorUtility.SetDirty(pedra); EditorUtility.SetDirty(olhos);

        // importador: Generic, clipes em loop
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = true; mi.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = c.name.Split('|').Last();
            c.loopTime = (c.name == "Idle" || c.name == "Walk" || c.name == "Run" || c.name == "Defender_Segurar"); c.loopPose = c.loopTime;
            c.lockRootHeightY = true; c.lockRootPositionXZ = true; c.lockRootRotation = true;
        }
        mi.clipAnimations = clips; mi.SaveAndReimport();

        // prefab
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo); go.name = "GolemRocha";
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = r.name.ToLower().Contains("eyes") ? olhos : pedra;
            if (r is SkinnedMeshRenderer s) { s.updateWhenOffscreen = false; s.localBounds = new Bounds(new Vector3(0, 1.25f, 0f), new Vector3(3.6f, 3.2f, 3.2f)); }
        }
        var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>(); anim.applyRootMotion = false;

        // COLISAO (frente = +Z; ~2,5 m, tronco largo): capsula no corpo + caixa nos bracos que pendem dos lados.
        foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        var corpo = go.AddComponent<CapsuleCollider>(); corpo.direction = 1; corpo.center = new Vector3(0, 1.25f, 0.1f); corpo.radius = 0.68f; corpo.height = 2.45f;
        var bracos = new GameObject("Colisor_Bracos"); bracos.transform.SetParent(go.transform, false);
        var bb = bracos.AddComponent<BoxCollider>(); bb.center = new Vector3(0, 1.0f, 0.25f); bb.size = new Vector3(2.7f, 1.4f, 0.9f);
        var rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;   // demo: nao e empurrado (no jogo, use o CharacterController do jogador)

        AnimationClip Clip(string n) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == n && !c.name.StartsWith("__preview__"));
        var demo = go.AddComponent<GolemRochaDemo>();
        demo.clips = new[] { "Idle", "Walk", "Run", "Attack_Pancada", "Attack_Martelada", "Defender_Entrar", "Provocar", "Levar_Golpe", "Morrer", "Defender_Sair", "Defender_Segurar" }.Select(Clip).ToArray();
        string prefab = pasta + "/GolemRocha.prefab"; PrefabUtility.SaveAsPrefabAsset(go, prefab); Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets(); Debug.Log("[Golem] prefab criado em " + prefab);
    }

    [MenuItem("Golem Rocha/Cena de teste")]
    public static void Teste()
    {
        string pasta = Pasta(); var pf = AssetDatabase.LoadAssetAtPath<GameObject>(pasta + "/GolemRocha.prefab"); if (pf == null) { Montar(); pf = AssetDatabase.LoadAssetAtPath<GameObject>(pasta + "/GolemRocha.prefab"); }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var cena = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.transform.localScale = Vector3.one * 5; chao.name = "Chao";
        var c = (GameObject)PrefabUtility.InstantiatePrefab(pf); c.transform.position = Vector3.zero;
        var cam = Camera.main; if (cam != null) { cam.transform.position = new Vector3(-5.5f, 1.8f, 1.5f); cam.transform.LookAt(new Vector3(0, 1.2f, 0f)); }
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(cena, pasta + "/GolemRocha_Teste.unity");
        Selection.activeGameObject = c; if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("[Golem] cena criada: " + pasta + "/GolemRocha_Teste.unity. Aperte Play e use as teclas 1-9 e 0.");
    }
}
#endif
