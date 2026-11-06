using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Monta o Golem do Cemiterio (mini-boss) neste projeto: Humanoid, texturas e material URP, as animacoes dele
// (loop + eventos de impacto, pisada e batida no peito), o Animator e a cena de teste.
// Menu: Golem > Montar tudo   (ou em batch: -executeMethod MontarGolem.Montar)
public static class MontarGolem
{
    const string Raiz = "Assets/Personagens/GolemCemiterio";
    const string Modelo = Raiz + "/golem_rigged.fbx";
    const string Cena = Raiz + "/Teste_Golem.unity";
    const string Clipes = Raiz + "/animacoes";
    static readonly string[] Loops = { "Parado", "Andar", "Correr", "Postura_Luta", "Atordoado" };

    [MenuItem("Golem/Abrir cena de teste")]
    public static void AbrirCena() { EditorSceneManager.OpenScene(Cena); }

    [MenuItem("Golem/Montar tudo")]
    public static void Montar()
    {
        Texturas();
        var avatar = Personagem();
        ImportarClipes(avatar);
        var ctrl = Controlador();
        MontarCena(ctrl);
        Debug.Log("[Golem] PRONTO: abra " + Cena + " e de Play.");
    }

    static void Texturas()
    {
        var n = (TextureImporter)AssetImporter.GetAtPath(Raiz + "/texturas_unity/golem_Normal.png");
        n.textureType = TextureImporterType.NormalMap; n.SaveAndReimport();
        var m = (TextureImporter)AssetImporter.GetAtPath(Raiz + "/texturas_unity/golem_MetallicSmoothness.png");
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
        var mpath = Raiz + "/Golem.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mpath);
        if (!mat) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, mpath); }
        Texture2D T(string tipo) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Raiz}/texturas_unity/golem_{tipo}.png");
        mat.SetTexture("_BaseMap", T("BaseColor")); mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_MetallicGlossMap", T("MetallicSmoothness")); mat.SetFloat("_SmoothnessTextureChannel", 0);
        mat.SetFloat("_Smoothness", 1f); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetTexture("_BumpMap", T("Normal")); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(mat);
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Golem"), mat);
        AssetDatabase.SaveAssets(); mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<Avatar>().First();
        Debug.Log($"[Golem] avatar valido={avatar.isValid} humanoid={avatar.isHuman} ossos mapeados={mi.humanDescription.human.Length}");
        foreach (var h in mi.humanDescription.human) Debug.Log($"[Golem]   {h.humanName} -> {h.boneName}");
        return avatar;
    }

    static void ImportarClipes(Avatar avatar)
    {
        // Humanoid clips drop the bones that are not part of the human body (sockets...). A mask with every
        // transform of the model keeps their curves.
        var mpath = Raiz + "/Golem_TodosOsOssos.mask";
        AssetDatabase.DeleteAsset(mpath);
        var mask = new AvatarMask();
        mask.AddTransformPath(AssetDatabase.LoadAssetAtPath<GameObject>(Modelo).transform, true);
        AssetDatabase.CreateAsset(mask, mpath);
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
            c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
            c.maskType = ClipAnimationMaskType.CopyFromOther; c.maskSource = mask;
            // eventos (tempo normalizado 0..1 = quadro de impacto / total): onde o golpe acerta
            var golpes = new System.Collections.Generic.Dictionary<string, (string, float)> {
                { "Soco_Direto", ("ImpactoSoco", 0.40f) }, { "Soco_Pesado", ("ImpactoSoco", 0.44f) }, { "Varrer", ("ImpactoSoco", 0.48f) },
                { "Esmagar", ("ImpactoSoco", 0.57f) }, { "Pisada", ("ImpactoPisada", 0.51f) } };
            if (golpes.ContainsKey(nome)) c.events = new[] { new AnimationEvent { functionName = golpes[nome].Item1, time = golpes[nome].Item2 } };
            if (nome == "Rugir") c.events = new[] { 0.36f, 0.51f, 0.67f }.Select(t => new AnimationEvent { functionName = "ImpactoPeito", time = t }).ToArray();
            mi.clipAnimations = new[] { c }; mi.SaveAndReimport();
        }
    }

    static AnimatorController Controlador()
    {
        var path = Raiz + "/Golem.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState parado = null; var estados = new System.Collections.Generic.Dictionary<string, AnimatorState>();
        foreach (var nome in DemoGolem.Estados)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath($"{Clipes}/golem@{nome}.fbx").OfType<AnimationClip>()
                .FirstOrDefault(a => !a.name.StartsWith("__preview__"));
            if (clip == null) { Debug.LogWarning($"[Golem] clipe {nome} nao encontrado em {Clipes}"); continue; }
            var st = sm.AddState(nome); st.motion = clip; estados[nome] = st;
            if (nome == "Parado") parado = st;
            Debug.Log($"[Golem] clipe {nome}: {clip.length:0.00}s humanoid={clip.isHumanMotion}");
        }
        if (parado != null)
        {
            sm.defaultState = parado;
            // sequencias naturais: um clipe de uma vez emenda no loop certo
            var depois = new System.Collections.Generic.Dictionary<string, string> {
                { "Levar_Golpe", "Postura_Luta" }, { "Levar_Golpe_Forte", "Postura_Luta" }, { "Soco_Direto", "Postura_Luta" }, { "Soco_Pesado", "Postura_Luta" },
                { "Varrer", "Postura_Luta" }, { "Esmagar", "Postura_Luta" }, { "Pisada", "Postura_Luta" }, { "Rugir", "Postura_Luta" } };
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
        go.name = "GolemCemiterio";
        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var demo = go.AddComponent<DemoGolem>(); demo.cam = Camera.main;
        go.AddComponent<ImpactoGolem>().AcharOssos();
        Camera.main.transform.position = new Vector3(3f, 2.6f, 7f); Camera.main.transform.LookAt(new Vector3(0, 1.3f, 0));
        Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = new Color(0.33f, 0.35f, 0.4f);
        EditorSceneManager.SaveScene(cena, Cena);
    }
}
