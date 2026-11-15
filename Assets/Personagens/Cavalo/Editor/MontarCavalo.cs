#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Menu  Cavalo > Montar tudo : configura o FBX (rig Generic, clipes em loop), cria o material, o prefab com COLISOR (nao da para atravessar)
// e, se o projeto tiver o script AnimalAmbiente (jogo Pinglu), liga Idle/Walk/Comer nele.
public static class MontarCavalo
{
    static string Pasta()
    {
        var g = AssetDatabase.FindAssets("Cavalo t:Model").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("/Cavalo.fbx"));
        return g == null ? null : Path.GetDirectoryName(g).Replace("\\", "/");
    }

    [MenuItem("Cavalo/Montar tudo")]
    public static void Montar()
    {
        string pasta = Pasta(); if (pasta == null) { Debug.LogError("[Cavalo] Cavalo.fbx nao encontrado."); return; }
        string fbx = pasta + "/Cavalo.fbx";
        // textura e material
        string texPath = pasta + "/Cavalo_cor.jpg";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(pasta + "/Cavalo.mat");
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh); AssetDatabase.CreateAsset(mat, pasta + "/Cavalo.mat");
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex); if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f); EditorUtility.SetDirty(mat);

        // importador: Generic, clipes em loop
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
        mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; mi.materialImportMode = ModelImporterMaterialImportMode.None;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = c.name.Split('|').Last();
            c.loopTime = !(c.name == "Comer_Entrar" || c.name == "Comer_Sair"); c.loopPose = c.loopTime;
            c.lockRootHeightY = true; c.lockRootPositionXZ = true; c.lockRootRotation = true;
        }
        mi.clipAnimations = clips; mi.SaveAndReimport();

        // prefab
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo); go.name = "Cavalo";
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; if (r is SkinnedMeshRenderer s) { s.updateWhenOffscreen = false; s.localBounds = new Bounds(new Vector3(0, 1f, -0.6f), new Vector3(1.4f, 2.2f, 3.4f)); } }
        var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>(); anim.applyRootMotion = false;

        // COLISAO (frente do cavalo = +Z; cabeca em z~+0.8, cauda em z~-2): cavalo ~2,0 m de corpo, ~1,45 m na cernelha. Capsula deitada no corpo + capsula do pescoco/cabeca.
        foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        var corpo = go.AddComponent<CapsuleCollider>(); corpo.direction = 2; corpo.center = new Vector3(0, 1.0f, -0.75f); corpo.radius = 0.33f; corpo.height = 1.8f;
        var pescoco = new GameObject("Colisor_Pescoco"); pescoco.transform.SetParent(go.transform, false);
        var cp = pescoco.AddComponent<CapsuleCollider>(); cp.direction = 2; cp.center = new Vector3(0, 1.5f, 0.38f); cp.radius = 0.2f; cp.height = 0.85f;
        var pernas = new GameObject("Colisor_Pernas"); pernas.transform.SetParent(go.transform, false);
        var bp = pernas.AddComponent<BoxCollider>(); bp.center = new Vector3(0, 0.3f, -0.55f); bp.size = new Vector3(0.5f, 0.6f, 1.6f);
        var rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;   // anda por script, empurra o jogador (CharacterController) e nao e empurrado

        // ligacao com o jogo (se existir): AnimalAmbiente (parado/andando/comendo)
        var tipo = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } }).FirstOrDefault(t => t.Name == "AnimalAmbiente");
        AnimationClip Clip(string n) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == n && !c.name.StartsWith("__preview__"));
        if (tipo != null)
        {
            var comp = go.AddComponent(tipo);
            tipo.GetField("parado")?.SetValue(comp, Clip("Idle")); tipo.GetField("andando")?.SetValue(comp, Clip("Walk")); tipo.GetField("comendo")?.SetValue(comp, Clip("Comer"));
        }
        else
        {
            var demo = go.AddComponent<CavaloDemo>(); demo.clips = new[] { "Idle", "Walk", "Trot", "Gallop", "Comer_Entrar", "Comer", "Comer_Sair" }.Select(Clip).ToArray();
        }
        string prefab = pasta + "/Cavalo.prefab"; PrefabUtility.SaveAsPrefabAsset(go, prefab); Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets(); Debug.Log("[Cavalo] prefab criado em " + prefab + " (AnimalAmbiente: " + (tipo != null) + ")");
    }

    // cria uma CENA NOVA (Assets/.../Cavalo_Teste.unity) com chao, luz, camera e o cavalo. Dê Play e tecle 1-7.
    [MenuItem("Cavalo/Cena de teste")]
    public static void Teste()
    {
        string pasta = Pasta(); var pf = AssetDatabase.LoadAssetAtPath<GameObject>(pasta + "/Cavalo.prefab"); if (pf == null) { Montar(); pf = AssetDatabase.LoadAssetAtPath<GameObject>(pasta + "/Cavalo.prefab"); }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var cena = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.transform.localScale = Vector3.one * 5; chao.name = "Chao";
        var c = (GameObject)PrefabUtility.InstantiatePrefab(pf); c.transform.position = Vector3.zero;
        var cam = Camera.main; if (cam != null) { cam.transform.position = new Vector3(-5.5f, 1.8f, -1.2f); cam.transform.LookAt(new Vector3(0, 1.0f, -0.6f)); }
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(cena, pasta + "/Cavalo_Teste.unity");
        Selection.activeGameObject = c; if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("[Cavalo] cena criada: " + pasta + "/Cavalo_Teste.unity. Aperte Play e use as teclas 1-7.");
    }
}
#endif
