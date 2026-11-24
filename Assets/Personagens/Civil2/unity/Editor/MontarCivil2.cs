using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Monta o Civil 2 (NPC) neste projeto: Humanoid, texturas e material URP, as animacoes dele
// (loops e volta sozinha pro Parado), o Animator e a cena de teste.
// Menu: Civil2 > Montar tudo   (ou em batch: -executeMethod MontarCivil2.Montar)
public static class MontarCivil2
{
    const string Raiz = "Assets/Personagens/Civil2";
    const string Modelo = Raiz + "/civil2_rigged.fbx";
    const string Cena = Raiz + "/Teste_Civil2.unity";
    const string Clipes = Raiz + "/animacoes";
    static readonly string[] Loops = { "Parado", "Andar", "Andar_Calmo", "Correr", "Correr_Desesperado", "Carregar_Andar", "Agachado", "Falar", "Ouvir", "Assustado", "Dancar", "Varrer", "Cortar_Lenha", "Empurrar", "Tocha", "Lanterna", "Sentado", "Sentado_Falando" };

    [MenuItem("Civil2/Abrir cena de teste")]
    public static void AbrirCena() { EditorSceneManager.OpenScene(Cena); }

    [MenuItem("Civil2/Montar tudo")]
    public static void Montar()
    {
        Texturas();
        var avatar = Personagem();
        ImportarClipes(avatar);
        var ctrl = Controlador();
        MontarCena(ctrl);
        Debug.Log("[Civil2] PRONTO: abra " + Cena + " e de Play.");
    }

    static readonly string[] Partes = { "Body", "Bottom", "Top", "Shoes", "Hair", "Beard", "Moustache" };
    static readonly string[] ComAlfa = { "Hair", "Beard", "Moustache" };

    static void Texturas()
    {
        foreach (var p in Partes)
        {
            var n = (TextureImporter)AssetImporter.GetAtPath($"{Raiz}/texturas_unity/civil2_{p}_Normal.png");
            n.textureType = TextureImporterType.NormalMap; n.SaveAndReimport();
            var s = (TextureImporter)AssetImporter.GetAtPath($"{Raiz}/texturas_unity/civil2_{p}_Specular.png");
            s.sRGBTexture = false; s.alphaSource = TextureImporterAlphaSource.FromInput; s.SaveAndReimport();
            var c = (TextureImporter)AssetImporter.GetAtPath($"{Raiz}/texturas_unity/civil2_{p}_BaseColor.png");
            c.alphaIsTransparency = ComAlfa.Contains(p); c.SaveAndReimport();
        }
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
        foreach (var p in Partes)
        {
            var mpath = $"{Raiz}/Civil2_{p}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(mpath);
            if (!mat) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, mpath); }
            Texture2D T(string tipo) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Raiz}/texturas_unity/civil2_{p}_{tipo}.png");
            mat.SetTexture("_BaseMap", T("BaseColor")); mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_WorkflowMode", 0f); mat.EnableKeyword("_SPECULAR_SETUP");          // Specular: RGB = especular, alfa = brilho
            mat.SetTexture("_SpecGlossMap", T("Specular")); mat.SetColor("_SpecColor", Color.white);
            mat.SetFloat("_SmoothnessTextureChannel", 0f); mat.SetFloat("_Smoothness", 1f); mat.EnableKeyword("_SPECGLOSSMAP");
            mat.SetTexture("_BumpMap", T("Normal")); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
            if (ComAlfa.Contains(p))                                                          // cabelo / barba: recorte pelo alfa, dos dois lados
            {
                mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.4f); mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f); mat.renderQueue = 2450;
            }
            EditorUtility.SetDirty(mat);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Civil2_" + p), mat);
        }
        AssetDatabase.SaveAssets(); mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<Avatar>().First();
        var dedos = mi.humanDescription.human.Count(h => new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Any(f => h.humanName.Contains(f)));
        Debug.Log($"[Civil2] avatar valido={avatar.isValid} humanoid={avatar.isHuman} ossos mapeados={mi.humanDescription.human.Length} (dedos: {dedos})");
        return avatar;
    }

    static void ImportarClipes(Avatar avatar)
    {
        // Humanoid clips drop the bones that are not part of the human body (sockets...). A mask with every
        // transform of the model keeps their curves.
        var mpath = Raiz + "/Civil2_TodosOsOssos.mask";
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
            mi.clipAnimations = new[] { c }; mi.SaveAndReimport();
        }
    }

    static AnimatorController Controlador()
    {
        var path = Raiz + "/Civil2.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState parado = null; var estados = new System.Collections.Generic.Dictionary<string, AnimatorState>();
        foreach (var nome in DemoCivil2.Estados)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath($"{Clipes}/civil2@{nome}.fbx").OfType<AnimationClip>()
                .FirstOrDefault(a => !a.name.StartsWith("__preview__"));
            if (clip == null) { Debug.LogWarning($"[Civil2] clipe {nome} nao encontrado em {Clipes}"); continue; }
            var st = sm.AddState(nome); st.motion = clip; estados[nome] = st;
            if (nome == "Parado") parado = st;
            Debug.Log($"[Civil2] clipe {nome}: {clip.length:0.00}s humanoid={clip.isHumanMotion}");
        }
        if (parado != null)
        {
            sm.defaultState = parado;
            // sequencias naturais: um clipe de uma vez emenda no loop certo
            var depois = new System.Collections.Generic.Dictionary<string, string> { { "Levar_Golpe", "Parado" }, { "Levar_Golpe_Cabeca", "Parado" }, { "Levar_Golpe_Forte", "Parado" }, { "Concordar", "Parado" }, { "Negar", "Parado" }, { "Interagir", "Parado" }, { "Beber", "Parado" }, { "Colher", "Parado" }, { "Plantar", "Parado" }, { "Regar", "Parado" }, { "Consertar", "Parado" }, { "Abrir_Bau", "Parado" }, { "Pegar_Objeto", "Parado" }, { "Sentar_Entrar", "Sentado" }, { "Sentar_Sair", "Parado" } };
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
        go.name = "Civil2";
        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var demo = go.AddComponent<DemoCivil2>(); demo.cam = Camera.main;
        go.AddComponent<Civil2NPC>();
        Camera.main.transform.position = new Vector3(1.5f, 1.3f, 3.6f); Camera.main.transform.LookAt(new Vector3(0, 1f, 0));
        Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = new Color(0.33f, 0.35f, 0.4f);
        EditorSceneManager.SaveScene(cena, Cena);
    }
}
