#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Menu  Angler > Montar tudo : configura o FBX (rig Generic, clipes em loop), cria o material, o prefab com COLISOR (nao da para atravessar)
// e, se o projeto tiver o script AnimalAmbiente (jogo Pinglu), liga Idle/Walk/Comer nele.
public static class MontarAngler
{
    static string Pasta()
    {
        var g = AssetDatabase.FindAssets("Angler t:Model").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("/Angler.fbx"));
        return g == null ? null : Path.GetDirectoryName(g).Replace("\\", "/");
    }

    [MenuItem("Angler/Montar tudo")]
    public static void Montar()
    {
        string pasta = Pasta(); if (pasta == null) { Debug.LogError("[Angler] Angler.fbx nao encontrado."); return; }
        string fbx = pasta + "/Angler.fbx";
        // textura e material
        string texPath = pasta + "/Angler_cor.jpg";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(pasta + "/Angler.mat");
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh); AssetDatabase.CreateAsset(mat, pasta + "/Angler.mat");
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var nmPath = pasta + "/Angler_normal.jpg"; var nmi = AssetImporter.GetAtPath(nmPath) as TextureImporter; if (nmi != null && nmi.textureType != TextureImporterType.NormalMap) { nmi.textureType = TextureImporterType.NormalMap; nmi.SaveAndReimport(); }
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(nmPath); if (nrm != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex); if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f); EditorUtility.SetDirty(mat);

        // importador: Generic, clipes em loop
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; mi.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = c.name.Split('|').Last();
            c.loopTime = (c.name == "Idle" || c.name == "Walk" || c.name == "Run"); c.loopPose = c.loopTime;
            c.lockRootHeightY = true; c.lockRootPositionXZ = true; c.lockRootRotation = true;
        }
        mi.clipAnimations = clips; mi.SaveAndReimport();

        // prefab
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo); go.name = "Angler";
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; if (r is SkinnedMeshRenderer s) { s.updateWhenOffscreen = false; s.localBounds = new Bounds(new Vector3(0, 1.3f, 0.2f), new Vector3(3.6f, 3.4f, 4.8f)); } }
        var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>(); anim.applyRootMotion = false;

        // COLISAO (frente = +Z; ~2,5 m de altura, ~2,9 m de comprimento com a cauda): capsula no tronco, capsula na cabeca e caixa nas pernas/bracos.
        foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        var corpo = go.AddComponent<CapsuleCollider>(); corpo.direction = 2; corpo.center = new Vector3(0, 1.3f, 0.15f); corpo.radius = 0.6f; corpo.height = 1.9f;
        var cab = new GameObject("Colisor_Cabeca"); cab.transform.SetParent(go.transform, false);
        var cc = cab.AddComponent<CapsuleCollider>(); cc.direction = 2; cc.center = new Vector3(0, 2.0f, 0.95f); cc.radius = 0.32f; cc.height = 0.9f;
        var pernas = new GameObject("Colisor_Membros"); pernas.transform.SetParent(go.transform, false);
        var bp = pernas.AddComponent<BoxCollider>(); bp.center = new Vector3(0, 0.45f, 0.15f); bp.size = new Vector3(2.3f, 0.9f, 1.1f);
        var rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;   // anda por script, nao e empurrado

        AnimationClip Clip(string n) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == n && !c.name.StartsWith("__preview__"));
        var demo = go.AddComponent<AnglerDemo>(); demo.clips = new[] { "Idle", "Walk", "Run", "Attack_Mordida", "Attack_Garras", "Attack_Cauda", "Attack_Salto", "Rugido", "Dano", "Morte" }.Select(Clip).ToArray();
        string prefab = pasta + "/Angler.prefab"; PrefabUtility.SaveAsPrefabAsset(go, prefab); Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets(); Debug.Log("[Angler] prefab criado em " + prefab);
    }

    // cria uma CENA NOVA (Assets/.../Angler_Teste.unity) com chao, luz, camera e o Angler. Dê Play e tecle 1-9 e 0.
    [MenuItem("Angler/Cena de teste")]
    public static void Teste()
    {
        string pasta = Pasta(); var pf = AssetDatabase.LoadAssetAtPath<GameObject>(pasta + "/Angler.prefab"); if (pf == null) { Montar(); pf = AssetDatabase.LoadAssetAtPath<GameObject>(pasta + "/Angler.prefab"); }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var cena = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.transform.localScale = Vector3.one * 5; chao.name = "Chao";
        var c = (GameObject)PrefabUtility.InstantiatePrefab(pf); c.transform.position = Vector3.zero;
        var cam = Camera.main; if (cam != null) { cam.transform.position = new Vector3(-6.5f, 2.0f, 0.3f); cam.transform.LookAt(new Vector3(0, 1.2f, 0.3f)); }
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(cena, pasta + "/Angler_Teste.unity");
        Selection.activeGameObject = c; if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("[Angler] cena criada: " + pasta + "/Angler_Teste.unity. Aperte Play e use as teclas 1-9 e 0.");
    }
}
#endif
