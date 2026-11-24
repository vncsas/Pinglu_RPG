using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Monta a maga neste projeto: Humanoid (dedos da mao esquerda), texturas, material URP, as animacoes dela
// (loop + evento LancarMagia), o Animator e a cena de teste.
// Menu: Maga > Montar tudo   (ou em batch: -executeMethod MontarMaga.Montar)
public static class MontarMaga
{
    const string Raiz = "Assets/Personagens/Maga";
    const string Modelo = Raiz + "/maga_rigged.fbx";
    const string Cena = Raiz + "/Teste_Maga.unity";
    const string Clipes = Raiz + "/animacoes";
    static readonly string[] Loops = { "Parado", "Andar", "Correr", "Magia_Pronta", "Flutuar", "Flutuar_Frente", "Flutuar_Magia_Pronta" };
    const int QuadroDisparo = 7;   // Magia_Lancar e Flutuar_Magia_Lancar (16 quadros)
    const int QuadroRaio = 16;     // Raio_Cajado e Flutuar_Raio (40 quadros)

    [MenuItem("Maga/Abrir cena de teste")]
    public static void AbrirCena() { EditorSceneManager.OpenScene(Cena); }

    [MenuItem("Maga/Montar tudo")]
    public static void Montar()
    {
        Texturas();
        var avatar = Personagem();
        ImportarClipes(avatar);
        var ctrl = Controlador();
        MontarCena(ctrl);
        Debug.Log("[Maga] PRONTO: abra " + Cena + " e de Play.");
    }

    static void Texturas()
    {
        var n = (TextureImporter)AssetImporter.GetAtPath(Raiz + "/texturas_unity/maga_Normal.png");
        n.textureType = TextureImporterType.NormalMap; n.SaveAndReimport();
        var m = (TextureImporter)AssetImporter.GetAtPath(Raiz + "/texturas_unity/maga_MetallicSmoothness.png");
        m.sRGBTexture = false; m.SaveAndReimport();
    }

    static Avatar Personagem()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(Modelo);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        var limpo = mi.humanDescription; limpo.human = new HumanBone[0]; limpo.skeleton = new SkeletonBone[0];
        mi.humanDescription = limpo;
        mi.SaveAndReimport();
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var mpath = Raiz + "/Maga.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mpath);
        if (!mat) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, mpath); }
        Texture2D T(string tipo) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Raiz}/texturas_unity/maga_{tipo}.png");
        mat.SetTexture("_BaseMap", T("BaseColor")); mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_MetallicGlossMap", T("MetallicSmoothness")); mat.SetFloat("_SmoothnessTextureChannel", 0);
        mat.SetFloat("_Smoothness", 1f); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetTexture("_BumpMap", T("Normal")); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(mat);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Maga"), mat);
        AssetDatabase.SaveAssets(); mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<Avatar>().First();
        var dedos = mi.humanDescription.human.Count(h => h.humanName.StartsWith("Left") &&
            new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Any(f => h.humanName.Contains(f)));
        Debug.Log($"[Maga] avatar valido={avatar.isValid} humanoid={avatar.isHuman} ossos mapeados={mi.humanDescription.human.Length} (dedos da mao esquerda: {dedos})");
        return avatar;
    }

    static void ImportarClipes(Avatar avatar)
    {
        foreach (var path in Directory.GetFiles(Clipes, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
        {
            var nome = Path.GetFileNameWithoutExtension(path).Split('@')[1];
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; mi.sourceAvatar = avatar;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = true;
            mi.SaveAndReimport();
            var c = mi.defaultClipAnimations.First(); c.name = nome;
            c.loopTime = c.loopPose = Loops.Contains(nome);
            c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;   // flutuar: a altura vem do clipe
            if (nome.EndsWith("Magia_Lancar")) c.events = new[] { new AnimationEvent { functionName = "LancarMagia", time = (QuadroDisparo - 1) / 15f } };
            if (nome.EndsWith("Raio_Cajado") || nome == "Flutuar_Raio") c.events = new[] { new AnimationEvent { functionName = "SoltarRaio", time = (QuadroRaio - 1) / 39f } };
            mi.clipAnimations = new[] { c }; mi.SaveAndReimport();
        }
    }

    static AnimatorController Controlador()
    {
        var path = Raiz + "/Maga.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState parado = null; var estados = new System.Collections.Generic.Dictionary<string, AnimatorState>();
        foreach (var nome in DemoMaga.Estados)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath($"{Clipes}/maga@{nome}.fbx").OfType<AnimationClip>()
                .FirstOrDefault(a => !a.name.StartsWith("__preview__"));
            if (clip == null) { Debug.LogWarning($"[Maga] clipe {nome} nao encontrado em {Clipes}"); continue; }
            var st = sm.AddState(nome); st.motion = clip; estados[nome] = st;
            if (nome == "Parado") parado = st;
            Debug.Log($"[Maga] clipe {nome}: {clip.length:0.00}s humanoid={clip.isHumanMotion}");
        }
        if (parado != null)
        {
            sm.defaultState = parado;
            // sequencias naturais: um clipe de uma vez emenda no loop certo
            var depois = new System.Collections.Generic.Dictionary<string, string> {
                { "Magia_Entrar", "Magia_Pronta" }, { "Magia_Lancar", "Magia_Pronta" }, { "Magia_Sair", "Parado" },
                { "Levar_Golpe", "Parado" }, { "Comecar_Flutuar", "Flutuar" }, { "Flutuar_Magia_Lancar", "Flutuar_Magia_Pronta" },
                { "Pousar", "Parado" }, { "Raio_Cajado", "Parado" }, { "Flutuar_Raio", "Flutuar" } };
            foreach (var kv in depois)
            {
                if (!estados.ContainsKey(kv.Key) || !estados.ContainsKey(kv.Value)) continue;
                var t = estados[kv.Key].AddTransition(estados[kv.Value]); t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.12f;
            }
        }
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static void MontarCena(AnimatorController ctrl)
    {
        var cena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.name = "Chao"; chao.transform.localScale = Vector3.one * 6;
        var mchao = AssetDatabase.LoadAssetAtPath<Material>("Assets/Personagens/CavaleiroSombrio/Chao.mat");
        if (mchao) chao.GetComponent<Renderer>().sharedMaterial = mchao;
        var luz = Object.FindFirstObjectByType<Light>(); luz.transform.rotation = Quaternion.Euler(40, 150, 0); luz.intensity = 2f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Modelo));
        go.name = "Maga";
        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var demo = go.AddComponent<DemoMaga>(); demo.cam = Camera.main;
        go.AddComponent<MagiaMaga>().AcharOssos();
        Camera.main.transform.position = new Vector3(1.5f, 1.3f, 3.2f); Camera.main.transform.LookAt(new Vector3(0, 1, 0));
        Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = new Color(0.33f, 0.35f, 0.4f);
        EditorSceneManager.SaveScene(cena, Cena);
    }

    // Fotos de conferencia (batch): T-pose e um quadro de cada animacao, de frente e de lado.
    public static void Fotos()
    {
        EditorSceneManager.OpenScene(Cena);
        var go = GameObject.Find("Maga"); var anim = go.GetComponent<Animator>(); var cam = Camera.main;
        var rt = new RenderTexture(600, 800, 24); cam.targetTexture = rt; Directory.CreateDirectory("Fotos");
        void Foto(string nome, float ang)
        {
            var alvo = go.transform.position + Vector3.up * 0.95f;
            cam.transform.position = alvo + Quaternion.Euler(0, ang, 0) * Vector3.forward * 3.4f + Vector3.up * 0.2f; cam.transform.LookAt(alvo);
            // skinned meshes are frozen into temporary copies (in batch mode the skinning is not refreshed for cam.Render)
            var tmp = new System.Collections.Generic.List<GameObject>();
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in smrs)
            {
                var m = new Mesh(); smr.BakeMesh(m, true);
                var t = new GameObject("tmp"); t.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                t.AddComponent<MeshFilter>().sharedMesh = m; t.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                smr.enabled = false; tmp.Add(t);
            }
            cam.Render();
            foreach (var smr in smrs) smr.enabled = true;
            foreach (var t in tmp) Object.DestroyImmediate(t);
            RenderTexture.active = rt; var tex = new Texture2D(600, 800, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 600, 800), 0, 0); tex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes($"Fotos/maga_{nome}.png", tex.EncodeToPNG());
        }
        Foto("tpose_frente", 15); Foto("tpose_lado", 90);
        var grafo = PlayableGraph.Create("foto"); var saida = AnimationPlayableOutput.Create(grafo, "a", anim);
        grafo.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        foreach (var (nome, t) in new[] { ("Magia_Lancar", 0.4f), ("Flutuar", 0.3f), ("Flutuar_Magia_Lancar", 0.4f), ("Andar", 0.3f) })
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath($"{Clipes}/maga@{nome}.fbx").OfType<AnimationClip>().First(a => !a.name.StartsWith("__preview__"));
            var cp = AnimationClipPlayable.Create(grafo, clip); cp.SetApplyFootIK(false); saida.SetSourcePlayable(cp);
            cp.SetTime(clip.length * t); grafo.Evaluate(0f);
            Foto(nome, 30);
            Vector3 Dir(HumanBodyBones a, HumanBodyBones b) => (anim.GetBoneTransform(b).position-anim.GetBoneTransform(a).position).normalized;
            Debug.Log($"[Maga] foto {nome}: braco esq {Dir(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm)} braco dir {Dir(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm)} " +
                      $"coxa esq {Dir(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg)} quadril y {anim.GetBoneTransform(HumanBodyBones.Hips).position.y:0.00}");
        }
        grafo.Destroy(); cam.targetTexture = null;
    }
}
