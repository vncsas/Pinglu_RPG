using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Playables;
using UnityEngine.Animations;

// Monta o Cavaleiro Sombrio neste projeto: importacao Humanoid, texturas, materiais URP, clipes
// (loop + eventos da espada), Animator Controller e a cena de teste com botoes.
// Menu: Cavaleiro Sombrio > Montar tudo   (ou em batch: -executeMethod MontarCavaleiroSombrio.Montar)
public static class MontarCavaleiroSombrio
{
    const string Raiz = "Assets/Personagens/CavaleiroSombrio";
    const string Modelo = Raiz + "/cavaleiro_sombrio_rigged.fbx";
    const string Cena = Raiz + "/Teste_CavaleiroSombrio.unity";
    static readonly string[] Loops = { "Parado", "Parado_Sem_Espada", "Andar", "Andar_Sem_Espada", "Correr", "Correr_Com_Espada" };
    static readonly string[] Partes = { "tecido", "metal", "espada" };

    // No instante do evento, o encaixe da mao (RightHandWeapon) tem de coincidir com o das costas (SwordBack),
    // senao a espada "pula" quando troca de lugar.
    static void VerificarTroca(string nome, float tempoNormalizado)
    {
        // the character of the test scene just built (a loose prefab instance is not animated by the graph)
        var go = GameObject.Find("CavaleiroSombrio"); var anim = go.GetComponent<Animator>();
        var clip = AssetDatabase.LoadAllAssetsAtPath($"{Raiz}/animacoes/cavaleiro_sombrio@{nome}.fbx").OfType<AnimationClip>().First(a => !a.name.StartsWith("__preview__"));
        var grafo = PlayableGraph.Create("verificar"); var saida = AnimationPlayableOutput.Create(grafo, "a", anim);
        var cp = AnimationClipPlayable.Create(grafo, clip); cp.SetApplyFootIK(false); saida.SetSourcePlayable(cp);
        grafo.SetTimeUpdateMode(DirectorUpdateMode.Manual); cp.SetTime(clip.length * tempoNormalizado); grafo.Evaluate(0f);
        Transform A(string n) => go.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
        var m = A("RightHandWeapon"); var c = A("SwordBack");
        Debug.Log($"[CavaleiroSombrio] troca em {nome}: distancia mao/costas = {Vector3.Distance(m.position, c.position) * 100:0.0} cm, " +
                  $"diferenca de angulo = {Quaternion.Angle(m.rotation, c.rotation):0.0} graus");
        grafo.Destroy();
    }

    [MenuItem("Cavaleiro Sombrio/Abrir cena de teste")]
    public static void AbrirCena() { EditorSceneManager.OpenScene(Cena); }

    [MenuItem("Cavaleiro Sombrio/Montar tudo")]
    public static void Montar()
    {
        Texturas();
        var avatar = Personagem();
        var clipes = Clipes(avatar);
        var ctrl = Controlador(clipes);
        MontarCena(ctrl);
        VerificarTroca("Guardar_Espada", 31f / 45f); VerificarTroca("Sacar_Espada", 14f / 45f);
        Debug.Log("[CavaleiroSombrio] PRONTO: abra " + Cena + " e de Play.");
    }

    static void Texturas()
    {
        foreach (var p in Partes)
        {
            Ajustar(p, "Normal", t => t.textureType = TextureImporterType.NormalMap);
            Ajustar(p, "MetallicSmoothness", t => t.sRGBTexture = false);
            Ajustar(p, "Occlusion", t => t.sRGBTexture = false);
        }
    }

    static void Ajustar(string parte, string tipo, System.Action<TextureImporter> f)
    {
        var path = $"{Raiz}/texturas_unity/cavaleiro_sombrio_{parte}_{tipo}.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(path); f(ti); ti.SaveAndReimport();
    }

    static Avatar Personagem()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(Modelo);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        var limpo = mi.humanDescription; limpo.human = new HumanBone[0]; limpo.skeleton = new SkeletonBone[0];
        mi.humanDescription = limpo;                     // mapeamento refeito do zero (automatico do Unity)
        mi.SaveAndReimport();
        // materiais URP Lit com as texturas
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var p in Partes)
        {
            var nome = "CavaleiroSombrio_" + char.ToUpper(p[0]) + p.Substring(1);
            var mpath = $"{Raiz}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mpath);
            if (!m) { m = new Material(lit); AssetDatabase.CreateAsset(m, mpath); }
            m.shader = lit;
            string T(string tipo) => $"{Raiz}/texturas_unity/cavaleiro_sombrio_{p}_{tipo}.png";
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(T("BaseColor"))); m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(T("MetallicSmoothness")));
            m.SetFloat("_SmoothnessTextureChannel", 0); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(T("Normal"))); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(T("Occlusion"))); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nome), m);
        }
        AssetDatabase.SaveAssets();
        mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<Avatar>().First();
        Debug.Log($"[CavaleiroSombrio] avatar valido={avatar.isValid} humanoid={avatar.isHuman} ossos mapeados={mi.humanDescription.human.Length} (dedos das duas maos incluidos)");
        foreach (var h in mi.humanDescription.human) Debug.Log($"[CavaleiroSombrio]   {h.humanName} -> {h.boneName}");
        return avatar;
    }

    static AnimationClip[] Clipes(Avatar avatar)
    {
        var lista = new System.Collections.Generic.List<AnimationClip>();
        foreach (var path in Directory.GetFiles(Raiz + "/animacoes", "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
        {
            var nome = Path.GetFileNameWithoutExtension(path).Split('@')[1];
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; mi.sourceAvatar = avatar;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = true;
            mi.SaveAndReimport();
            var c = mi.defaultClipAnimations.First();
            c.name = nome;
            c.loopTime = c.loopPose = Loops.Contains(nome);
            c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;      // tudo no lugar ("Bake Into Pose")
            c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
            if (nome == "Guardar_Espada") c.events = new[] { new AnimationEvent { functionName = "GuardarEspada", time = 31f / 45f } };
            if (nome == "Sacar_Espada") c.events = new[] { new AnimationEvent { functionName = "SacarEspada", time = 14f / 45f } };
            mi.clipAnimations = new[] { c };
            mi.SaveAndReimport();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(a => !a.name.StartsWith("__preview__"));
            lista.Add(clip);
            Debug.Log($"[CavaleiroSombrio] clipe {clip.name}: {clip.length:0.00}s loop={clip.isLooping} humanoid={clip.isHumanMotion} eventos={clip.events.Length}");
        }
        return lista.ToArray();
    }

    static AnimatorController Controlador(AnimationClip[] clipes)
    {
        var path = Raiz + "/CavaleiroSombrio.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var estados = clipes.ToDictionary(c => c.name, c => { var s = sm.AddState(c.name); s.motion = c; return s; });
        sm.defaultState = estados["Parado"];
        foreach (var kv in estados)
        {
            if (Loops.Contains(kv.Key) || kv.Key == "Levar_Golpe") continue;       // caido: fica no chao
            var destino = kv.Key == "Guardar_Espada" ? "Parado_Sem_Espada" : "Parado";
            var t = kv.Value.AddTransition(estados[destino]); t.hasExitTime = true; t.exitTime = 0.92f; t.duration = 0.15f;
        }
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static void MontarCena(AnimatorController ctrl)
    {
        var cena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        // chao quadriculado (1 m por casa) para dar para ver o deslocamento
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.name = "Chao"; chao.transform.localScale = Vector3.one * 6;
        var xadrez = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        Color a = new Color(0.30f, 0.30f, 0.33f), b = new Color(0.20f, 0.20f, 0.22f);
        xadrez.SetPixels(new[] { a, b, b, a }); xadrez.Apply();
        File.WriteAllBytes(Raiz + "/Chao_xadrez.png", xadrez.EncodeToPNG()); AssetDatabase.ImportAsset(Raiz + "/Chao_xadrez.png");
        var ti = (TextureImporter)AssetImporter.GetAtPath(Raiz + "/Chao_xadrez.png"); ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.SaveAndReimport();
        AssetDatabase.DeleteAsset(Raiz + "/Chao.mat");
        var mchao = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mchao.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Raiz + "/Chao_xadrez.png")); mchao.SetTextureScale("_BaseMap", new Vector2(30, 30));
        mchao.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mchao, Raiz + "/Chao.mat"); chao.GetComponent<Renderer>().sharedMaterial = mchao;
        var luz = Object.FindFirstObjectByType<Light>(); luz.transform.rotation = Quaternion.Euler(40, 150, 0); luz.intensity = 2f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Modelo));
        go.name = "CavaleiroSombrio";
        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var guarda = go.AddComponent<GuardaEspada>(); guarda.AcharOssos();
        var demo = go.AddComponent<DemoCavaleiro>(); demo.cam = Camera.main;
        Camera.main.transform.position = new Vector3(1.8f, 1.35f, 3.8f); Camera.main.transform.LookAt(new Vector3(0, 1, 0));
        Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = new Color(0.33f, 0.35f, 0.4f);
        EditorSceneManager.SaveScene(cena, Cena);
        Debug.Log($"[CavaleiroSombrio] cena salva; espada achada={guarda.sword != null}, encaixes={guarda.rightHandWeapon != null}/{guarda.swordBack != null}");
    }

}
