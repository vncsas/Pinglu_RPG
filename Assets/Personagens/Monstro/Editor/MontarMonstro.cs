#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Menu  Monstro > Montar tudo : configura o FBX (rig Generic, clipes em loop), cria o material, o prefab com COLISOR (nao da para atravessar)
// e, se o projeto tiver o script AnimalAmbiente (jogo Pinglu), liga Idle/Walk/Comer nele.
public static class MontarMonstro
{
    static string Pasta()
    {
        var g = AssetDatabase.FindAssets("Monstro t:Model").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("/Monstro.fbx"));
        return g == null ? null : Path.GetDirectoryName(g).Replace("\\", "/");
    }

    [MenuItem("Monstro/Montar tudo")]
    public static void Montar()
    {
        string pasta = Pasta(); if (pasta == null) { Debug.LogError("[Monstro] Monstro.fbx nao encontrado."); return; }
        string fbx = pasta + "/Monstro.fbx";
        // textura e material
        string texPath = pasta + "/Monstro_cor.jpg";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(pasta + "/Monstro.mat");
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh); AssetDatabase.CreateAsset(mat, pasta + "/Monstro.mat");
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var nmPath = pasta + "/Monstro_normal.jpg"; var nmi = AssetImporter.GetAtPath(nmPath) as TextureImporter; if (nmi != null && nmi.textureType != TextureImporterType.NormalMap) { nmi.textureType = TextureImporterType.NormalMap; nmi.SaveAndReimport(); }
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(nmPath); if (nrm != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex); if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f); EditorUtility.SetDirty(mat);

        // importador: Generic, clipes em loop
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; mi.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = c.name.Split('|').Last();
            c.loopTime = !(c.name == "Attack" || c.name == "Death"); c.loopPose = c.loopTime;
            c.lockRootHeightY = true; c.lockRootPositionXZ = true; c.lockRootRotation = true;
        }
        mi.clipAnimations = clips; mi.SaveAndReimport();

        // prefab
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo); go.name = "Monstro";
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; if (r is SkinnedMeshRenderer s) { s.updateWhenOffscreen = false; s.localBounds = new Bounds(new Vector3(0, 1.15f, 0.1f), new Vector3(1.8f, 2.4f, 2.6f)); } }
        var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>(); anim.applyRootMotion = false;

        // COLISAO (frente = +Z; monstro ~2,3 m, curvado): capsula no tronco/pernas + capsula da cabeca/bracos a frente.
        foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        var corpo = go.AddComponent<CapsuleCollider>(); corpo.direction = 1; corpo.center = new Vector3(0, 1.05f, 0.0f); corpo.radius = 0.42f; corpo.height = 2.1f;
        var frente = new GameObject("Colisor_Cabeca"); frente.transform.SetParent(go.transform, false);
        var cp = frente.AddComponent<CapsuleCollider>(); cp.direction = 1; cp.center = new Vector3(0.15f, 1.9f, 0.5f); cp.radius = 0.28f; cp.height = 0.7f;
        var rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;   // anda por script, nao e empurrado

        AnimationClip Clip(string n) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == n && !c.name.StartsWith("__preview__"));
        var demo = go.AddComponent<MonstroDemo>(); demo.clips = new[] { "Idle", "Walk", "Run", "Attack", "Death" }.Select(Clip).ToArray();
        string prefab = pasta + "/Monstro.prefab"; PrefabUtility.SaveAsPrefabAsset(go, prefab); Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets(); Debug.Log("[Monstro] prefab criado em " + prefab);
    }

    // cria uma CENA NOVA (Assets/.../Monstro_Teste.unity) com chao, luz, camera e o cavalo. Dê Play e tecle 1-5.
    [MenuItem("Monstro/Cena de teste")]
    public static void Teste()
    {
        string pasta = Pasta(); var pf = AssetDatabase.LoadAssetAtPath<GameObject>(pasta + "/Monstro.prefab"); if (pf == null) { Montar(); pf = AssetDatabase.LoadAssetAtPath<GameObject>(pasta + "/Monstro.prefab"); }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var cena = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.transform.localScale = Vector3.one * 5; chao.name = "Chao";
        var c = (GameObject)PrefabUtility.InstantiatePrefab(pf); c.transform.position = Vector3.zero;
        var cam = Camera.main; if (cam != null) { cam.transform.position = new Vector3(-4.5f, 1.6f, 0f); cam.transform.LookAt(new Vector3(0, 1.1f, 0f)); }
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(cena, pasta + "/Monstro_Teste.unity");
        Selection.activeGameObject = c; if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("[Monstro] cena criada: " + pasta + "/Monstro_Teste.unity. Aperte Play e use as teclas 1-5.");
    }
}
#endif
