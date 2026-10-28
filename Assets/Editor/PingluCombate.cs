// =====================================================================
//  PingluCombate.cs  -  Monta o combate de hordas no cemitério
// ---------------------------------------------------------------------
//  1. Configura o modelo do esqueleto (animações andar/atacar/dano/morte)
//  2. Cria o prefab do esqueleto com IA, vida e barra de vida
//  3. Coloca vida, combate e interface no cavaleiro (+ animações de dano/morte)
//  4. Cria o gerenciador de ondas no cemitério, com pontos de onde os
//     esqueletos saem do chão
//
//  COMO USAR:  Pinglu > 11. Preparar combate no cemitério
//  (o cemitério precisa existir: rode antes o Pinglu > 9)
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluCombate
{
    const string PASTA = "Assets/Personagens/Inimigos/Esqueleto";
    const string FBX = PASTA + "/Esqueleto.fbx";
    const string CONTROLADOR = PASTA + "/Esqueleto_Animator.controller";
    const string PREFAB = PASTA + "/Esqueleto.prefab";
    const string MAT_FUNDO = PASTA + "/BarraVida_Fundo.mat";
    const string MAT_FRENTE = PASTA + "/BarraVida_Frente.mat";
    const string NOME_HORDA = "Horda do Cemiterio";
    const float ALTURA_ESQUELETO = 2.0f;

    const string PASTA_HUD = "Assets/script/Combate/HUD";

    [MenuItem("Pinglu/11. Preparar combate no cemitério")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro!\n\nTudo o que muda na cena durante o Play é desfeito quando ele termina. " +
                "Pare o Play, rode este menu de novo e salve a cena (Ctrl + S).", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FBX) == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei o modelo do esqueleto em " + FBX + ".\nExtraiu o zip do combate na pasta do projeto?", "OK");
            return;
        }
        List<string> avisos = new List<string>();
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Configurando o esqueleto...", 0.1f);
            Dictionary<string, AnimationClip> clipes = ConfigurarModelo();
            AnimatorController ctrl = CriarAnimator(clipes, avisos);
            Material fundo = MaterialBarra(MAT_FUNDO, new Color(0.05f, 0.05f, 0.05f));
            Material frente = MaterialBarra(MAT_FRENTE, new Color(0.75f, 0.08f, 0.06f));

            EditorUtility.DisplayProgressBar("Pinglu", "Criando o prefab do esqueleto...", 0.4f);
            GameObject prefab = CriarPrefab(ctrl, fundo, frente);

            EditorUtility.DisplayProgressBar("Pinglu", "Preparando o cavaleiro...", 0.6f);
            PrepararJogador(avisos);

            EditorUtility.DisplayProgressBar("Pinglu", "Montando a horda no cemitério...", 0.8f);
            CriarHorda(prefab, avisos);
            AdicionarCenaNoBuild();
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console).");
        }
        finally { EditorUtility.ClearProgressBar(); }

        string msg = "Combate pronto!\n\nAgora SALVE A CENA (Ctrl + S) antes de dar Play.\n\nDepois, dê Play e vá até o cemitério (ou aperte F2 para ir direto).\n\n" +
                     "Clique esquerdo: combo  |  Clique direito: golpe pesado\nQ: bloquear com o escudo  |  Ctrl: rolar (não leva dano)\n\nSe gostar, salve a cena com Ctrl + S.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        foreach (string a in avisos) Debug.LogWarning("[Pinglu] " + a);
    }

    // ------------------------------------------------------------------
    static Dictionary<string, AnimationClip> ConfigurarModelo()
    {
        ModelImporter mi = (ModelImporter)AssetImporter.GetAtPath(FBX);
        bool mudou = false;
        if (mi.animationType != ModelImporterAnimationType.Generic) { mi.animationType = ModelImporterAnimationType.Generic; mudou = true; }
        if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mudou = true; }
        if (!mi.importAnimation) { mi.importAnimation = true; mudou = true; }
        if (mudou) mi.SaveAndReimport();

        ModelImporterClipAnimation[] cl = mi.clipAnimations;
        if (cl == null || cl.Length == 0) cl = mi.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in cl)
        {
            string n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
            c.name = n;
            c.loopTime = n.Contains("walk") || n.Contains("idle");
        }
        mi.clipAnimations = cl;
        mi.SaveAndReimport();

        Dictionary<string, AnimationClip> d = new Dictionary<string, AnimationClip>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FBX))
        {
            AnimationClip c = o as AnimationClip;
            if (c == null || c.name.StartsWith("__preview__")) continue;
            d[c.name.ToLower()] = c;
        }
        return d;
    }

    static AnimationClip Achar(Dictionary<string, AnimationClip> d, params string[] nomes)
    {
        foreach (string n in nomes)
            foreach (KeyValuePair<string, AnimationClip> kv in d)
                if (kv.Key == n || kv.Key.EndsWith(n)) return kv.Value;
        foreach (string n in nomes)
            foreach (KeyValuePair<string, AnimationClip> kv in d)
                if (kv.Key.Contains(n)) return kv.Value;
        return null;
    }

    static AnimatorController CriarAnimator(Dictionary<string, AnimationClip> clipes, List<string> avisos)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLADOR) != null) AssetDatabase.DeleteAsset(CONTROLADOR);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(CONTROLADOR);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        string[] estados = { "idle", "walk", "attack", "hit", "death" };
        AnimationClip[] c = {
            Achar(clipes, "idle"), Achar(clipes, "walk"), Achar(clipes, "attack"),
            Achar(clipes, "hit1", "hit2"), Achar(clipes, "hit2", "hit1") };
        for (int i = 0; i < estados.Length; i++)
        {
            AnimatorState st = sm.AddState(estados[i], new Vector3(300f, 60f * i, 0f));
            st.motion = c[i];
            if (c[i] == null) avisos.Add("Não achei a animação \"" + estados[i] + "\" do esqueleto.");
            if (i == 0) sm.defaultState = st;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static Material MaterialBarra(string caminho, Color cor)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, caminho); }
        else if (sh != null) m.shader = sh;
        m.SetColor("_BaseColor", cor);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject CriarPrefab(AnimatorController ctrl, Material fundo, Material frente)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
        GameObject raiz = new GameObject("Esqueleto");
        GameObject modelo = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        modelo.name = "Modelo";
        modelo.transform.SetParent(raiz.transform, false);

        Bounds b;
        if (Medir(modelo, out b) && b.size.y > 0.01f)
        {
            float escala = ALTURA_ESQUELETO / b.size.y;
            modelo.transform.localScale *= escala;
            Medir(modelo, out b);
            modelo.transform.localPosition = new Vector3(0f, -b.min.y, 0f);
        }

        Animator anim = modelo.GetComponent<Animator>();
        if (anim == null) anim = modelo.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CharacterController cc = raiz.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0f, 0.95f, 0f);
        cc.stepOffset = 0.4f; cc.slopeLimit = 50f;
        raiz.AddComponent<Vida>();
        InimigoEsqueleto ie = raiz.AddComponent<InimigoEsqueleto>();
        SerializedObject so = new SerializedObject(ie);
        so.FindProperty("materialFundo").objectReferenceValue = fundo;
        so.FindProperty("materialFrente").objectReferenceValue = frente;
        so.FindProperty("alturaDaBarra").floatValue = ALTURA_ESQUELETO + 0.3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB);
        Object.DestroyImmediate(raiz);
        return prefab;
    }

    static bool Medir(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        bool achou = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!achou) { b = r.bounds; achou = true; } else b.Encapsulate(r.bounds);
        }
        return achou;
    }

    // ------------------------------------------------------------------
    static void PrepararJogador(List<string> avisos)
    {
        PlayerLocomotion pl = Object.FindFirstObjectByType<PlayerLocomotion>();
        if (pl == null) { avisos.Add("Não achei o jogador (Player Locomotion) na cena."); return; }
        GameObject g = pl.gameObject;
        if (g.GetComponent<Vida>() == null) Undo.AddComponent<Vida>(g);
        if (g.GetComponent<CombateJogador>() == null) Undo.AddComponent<CombateJogador>(g);
        HUDJogador hud = g.GetComponent<HUDJogador>();
        if (hud == null) hud = Undo.AddComponent<HUDJogador>(g);
        SerializedObject sh = new SerializedObject(hud);
        sh.FindProperty("moldura").objectReferenceValue = TexturaHUD("hud_moldura");
        sh.FindProperty("preenchimentoVida").objectReferenceValue = TexturaHUD("hud_vida");
        sh.FindProperty("preenchimentoEspecial").objectReferenceValue = TexturaHUD("hud_especial");
        sh.ApplyModifiedProperties();

        // animações de levar dano e morrer no cavaleiro (do pacote de animações)
        Animator anim = g.GetComponentInChildren<Animator>();
        AnimatorController ac = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
        if (ac == null) { avisos.Add("O cavaleiro não tem Animator Controller editável; ele não terá animação de dano/morte."); return; }
        AnimationClip hit = null, morte = null;
        foreach (string guid in AssetDatabase.FindAssets("UAL2_Standard"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.EndsWith(".fbx") || p.Contains("_RM")) continue;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
            {
                AnimationClip c = o as AnimationClip;
                if (c == null || c.name.StartsWith("__preview__")) continue;
                string n = c.name.ToLower();
                if (morte == null && n.Contains("death")) morte = c;
                if (n.Contains("hit") && !n.Contains("death") && (hit == null || n.Contains("chest") || n.Contains("head"))) hit = c;
            }
        }
        AnimatorStateMachine sm = ac.layers[0].stateMachine;
        AnimatorState locomocao = null;
        bool temHit = false, temMorte = false;
        foreach (ChildAnimatorState cs in sm.states)
        {
            if (cs.state.motion is BlendTree) locomocao = cs.state;
            if (cs.state.name == "hit") temHit = true;
            if (cs.state.name == "death") temMorte = true;
        }
        if (hit != null && !temHit)
        {
            AnimatorState st = sm.AddState("hit", new Vector3(650f, 0f, 0f));
            st.motion = hit;
            if (locomocao != null) { AnimatorStateTransition t = st.AddTransition(locomocao); t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f; }
        }
        if (morte != null && !temMorte)
        {
            AnimatorState st = sm.AddState("death", new Vector3(650f, 70f, 0f));
            st.motion = morte;
        }
        if (hit == null || morte == null) avisos.Add("Não achei animação de dano e/ou morte no pacote UAL2; o cavaleiro só vai parar quando morrer.");
        EditorUtility.SetDirty(ac);
    }

    static Texture2D TexturaHUD(string nome)
    {
        string caminho = PASTA_HUD + "/" + nome + ".png";
        TextureImporter ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (ti == null) return null;
        bool mudou = false;
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; mudou = true; }
        if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; mudou = true; }
        if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; mudou = true; }
        if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; mudou = true; }
        if (mudou) ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
    }

    // ------------------------------------------------------------------
    static void CriarHorda(GameObject prefab, List<string> avisos)
    {
        Transform cemiterio = null;
        GameObject mundo = GameObject.Find("Mundo_Gerado");
        if (mundo != null)
        {
            Transform pasta = mundo.transform.Find("Cemiterio");
            if (pasta != null) cemiterio = pasta.Find("Cemiterio");
        }
        if (cemiterio == null)
        {
            avisos.Add("Não achei o cemitério. Rode Pinglu > 9. Espalhar pontos pelo mapa e depois rode este menu de novo.");
            return;
        }

        GameObject antigo = GameObject.Find(NOME_HORDA);
        if (antigo != null) Undo.DestroyObjectImmediate(antigo);

        GameObject g = new GameObject(NOME_HORDA);
        Undo.RegisterCreatedObjectUndo(g, "Horda do cemitério");
        g.transform.SetPositionAndRotation(cemiterio.position, cemiterio.rotation);
        GerenciadorDeOndas ger = g.AddComponent<GerenciadorDeOndas>();

        List<Transform> pontos = new List<Transform>();
        float[] xs = { -7.5f, -4.5f, 4.5f, 7.5f };
        float[] zs = { 3.5f, 0.5f, -2.5f };
        foreach (float x in xs)
            foreach (float z in zs)
            {
                GameObject p = new GameObject("Ponto de Surgimento");
                p.transform.SetParent(g.transform, false);
                p.transform.localPosition = new Vector3(x, 0f, z);
                pontos.Add(p.transform);
            }

        SerializedObject so = new SerializedObject(ger);
        so.FindProperty("prefabInimigo").objectReferenceValue = prefab;
        SerializedProperty arr = so.FindProperty("pontosDeSurgimento");
        arr.arraySize = pontos.Count;
        for (int i = 0; i < pontos.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = pontos[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(g.scene);
    }

    static void AdicionarCenaNoBuild()
    {
        string cena = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(cena)) return;
        List<EditorBuildSettingsScene> lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in lista) if (s.path == cena) return;
        lista.Insert(0, new EditorBuildSettingsScene(cena, true));
        EditorBuildSettings.scenes = lista.ToArray();
    }
}
#endif
