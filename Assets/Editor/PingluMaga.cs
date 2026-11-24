// =====================================================================
//  PingluMaga.cs  -  Maga como personagem jogável (sozinho e online)
// ---------------------------------------------------------------------
//  1. Configura o modelo e as animações (script do colega, sem trocar de cena)
//  2. Animator: esfera arcana, relâmpago no 3º golpe, barreira mágica,
//     levitar para correr, deslize flutuando, dano e morte
//  3. Cria o jogador da Maga (com rede) a partir do personagem da cena
//  4. Ficha 2 da seleção: nome, título, atributos, palco
//
//  COMO USAR:  Pinglu > 24. Maga jogável
// =====================================================================
#if UNITY_EDITOR && PINGLU_REDE
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluMaga
{
    const string RAIZ = "Assets/Personagens/Maga";
    const string MODELO = RAIZ + "/maga_rigged.fbx";
    const string ANIMS = RAIZ + "/animacoes/maga@";
    const string CONTROLADOR = RAIZ + "/Jogador_Maga.controller";
    const string MASCARA = RAIZ + "/Mascara_Conjurar.mask";
    const string BARREIRA = RAIZ + "/Maga_Barreira.mat";
    const string PREFAB = "Assets/Personagens/Jogadores/Jogador_Maga.prefab";
    const string LISTA = "Assets/Personagens/Jogadores/Prefabs de Rede.asset";
    const string FICHA = "Assets/Personagens/Jogadores/Fichas/2_Personagem2.asset";
    const float ANDAR = 1.3f, CORRER = 4.2f;
    static readonly Color ROXO = new Color(0.62f, 0.32f, 1f);

    [MenuItem("Pinglu/24. Maga jogável")]
    public static void Montar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MODELO) == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei " + MODELO + ".\n\nTraga a pasta da Maga da branch \"rigs\" e rode o git lfs pull (veja as instruções do Claude).", "OK");
            return;
        }
        GameObject baseJogador = AcharJogadorDaCena();
        if (baseJogador == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o personagem jogável na cena (o Cavaleiro Sombrio). Abra a SampleScene.", "OK"); return; }

        List<string> avisos = new List<string>();
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Configurando o modelo e as animações da Maga...", 0.15f);
            ConfigurarComScriptDoColega(avisos);
            EditorUtility.DisplayProgressBar("Pinglu", "Montando o Animator...", 0.4f);
            AnimatorController ctrl = MontarAnimator(avisos);
            EditorUtility.DisplayProgressBar("Pinglu", "Criando o jogador da Maga...", 0.65f);
            GameObject prefab = CriarJogador(baseJogador, ctrl, avisos);
            EditorUtility.DisplayProgressBar("Pinglu", "Preenchendo a ficha da seleção...", 0.9f);
            AtualizarFicha(prefab, avisos);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(baseJogador.scene);
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console)."); }
        finally { EditorUtility.ClearProgressBar(); }

        EditorUtility.DisplayDialog("Pinglu", "Maga pronta para jogar!\n\n" +
            "- Clique esquerdo: esfera arcana (3º golpe: relâmpago do cajado)\n- Botão direito: barreira mágica\n" +
            "- Shift: levitar e deslizar  |  Ctrl: deslize flutuando  |  Espaço: impulso flutuando\n\n" +
            "Ela aparece como personagem 2 na tela de seleção, sozinho e online.\n\nSalve a cena com Ctrl + S." +
            (avisos.Count > 0 ? "\n\nAvisos:\n- " + string.Join("\n- ", avisos) : ""), "OK");
    }

    // ------------------------------------------------------------------
    static void ConfigurarComScriptDoColega(List<string> avisos)
    {
        System.Type t = null;
        foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("MontarMaga"); if (t != null) break; }
        if (t == null) { avisos.Add("Não achei o script MontarMaga do colega; usei a configuração que já estava."); return; }
        BindingFlags f = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        MethodInfo texturas = t.GetMethod("Texturas", f), personagem = t.GetMethod("Personagem", f), clipes = t.GetMethod("ImportarClipes", f);
        if (texturas == null || personagem == null || clipes == null) { avisos.Add("O script do colega mudou; rode \"Maga > Montar tudo\" e depois este menu de novo."); return; }
        texturas.Invoke(null, null);
        object avatar = personagem.Invoke(null, null);
        clipes.Invoke(null, new[] { avatar });
    }

    static AnimationClip Clipe(string nome)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ANIMS + nome + ".fbx"))
        {
            AnimationClip c = o as AnimationClip;
            if (c != null && !c.name.StartsWith("__preview__")) return c;
        }
        return null;
    }

    static AnimatorState Estado(AnimatorStateMachine sm, string nome, string clipe, Vector3 pos, List<string> avisos, float vel = 1f)
    {
        AnimationClip c = Clipe(clipe);
        if (c == null) { avisos.Add("Faltou a animação " + clipe); return null; }
        AnimatorState s = sm.AddState(nome, pos); s.motion = c; s.speed = vel;
        return s;
    }

    static AnimatorStateTransition Transicao(AnimatorState de, AnimatorState para, float tempo, float duracao, AnimatorConditionMode modo = AnimatorConditionMode.If, string parametro = null)
    {
        AnimatorStateTransition t = de.AddTransition(para);
        t.hasExitTime = tempo >= 0f; if (tempo >= 0f) t.exitTime = tempo;
        t.duration = duracao; t.hasFixedDuration = true;
        if (!string.IsNullOrEmpty(parametro)) t.AddCondition(modo, 0f, parametro);
        return t;
    }

    static AnimatorController MontarAnimator(List<string> avisos)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLADOR) != null) AssetDatabase.DeleteAsset(CONTROLADOR);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(CONTROLADOR);
        foreach (string p in new[] { "Jump", "Roll", "AttackLight", "AttackHeavy" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Trigger);
        foreach (string p in new[] { "Block", "Grounded", "Rolling" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // parada / andando / levitando para frente
        BlendTree bt;
        AnimatorState loco = ctrl.CreateBlendTreeInController("Blend Tree", out bt, 0);
        bt.blendParameter = "Speed";
        bt.AddChild(Clipe("Parado"), 0f);
        bt.AddChild(Clipe("Andar"), 0.5f);
        bt.AddChild(Clipe("Flutuar_Frente"), 1f);
        ChildMotion[] filhos = bt.children;
        if (filhos.Length == 3) { filhos[1].timeScale = ANDAR / 0.88f; filhos[2].timeScale = 1.2f; bt.children = filhos; }
        sm.defaultState = loco;

        // magias: 1º e 2º golpes = esfera arcana, 3º = relâmpago do cajado
        AnimatorState a1 = Estado(sm, "attack", "Magia_Lancar", new Vector3(450f, 0f, 0f), avisos, 1.15f);
        AnimatorState a2 = Estado(sm, "attack_2", "Magia_Lancar", new Vector3(450f, 60f, 0f), avisos, 1.15f);
        AnimatorState a3 = Estado(sm, "attack_3", "Raio_Cajado", new Vector3(450f, 120f, 0f), avisos, 1.1f);
        AnimatorState ap = Estado(sm, "attack_heavy", "Raio_Cajado", new Vector3(450f, 180f, 0f), avisos);
        foreach (AnimatorState s in new[] { a1, a2, a3, ap }) if (s != null) Transicao(s, loco, 0.9f, 0.15f);

        // pulo: impulso flutuando
        AnimatorState pulo = Estado(sm, "Jump", "Comecar_Flutuar", new Vector3(0f, 250f, 0f), avisos, 1.4f);
        if (pulo != null) { Transicao(loco, pulo, -1f, 0.1f, AnimatorConditionMode.If, "Jump"); Transicao(pulo, loco, 0.5f, 0.2f, AnimatorConditionMode.If, "Grounded"); }
        // esquiva: desliza flutuando
        AnimatorState desvio = Estado(sm, "roll", "Flutuar_Frente", new Vector3(200f, 250f, 0f), avisos, 1.6f);
        if (desvio != null)
        {
            AnimatorStateTransition ida = sm.AddAnyStateTransition(desvio);
            ida.hasExitTime = false; ida.duration = 0.08f; ida.canTransitionToSelf = false; ida.AddCondition(AnimatorConditionMode.If, 0f, "Roll");
            Transicao(desvio, loco, -1f, 0.15f, AnimatorConditionMode.IfNot, "Rolling");
        }
        AnimatorState hit = Estado(sm, "hit", "Levar_Golpe", new Vector3(650f, 0f, 0f), avisos);
        if (hit != null) Transicao(hit, loco, 0.85f, 0.15f);
        Estado(sm, "death", "Morrer", new Vector3(650f, 70f, 0f), avisos);

        // barreira: pose de conjurar só da cintura pra cima (as pernas continuam andando)
        AvatarMask mascara = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASCARA);
        if (mascara == null) { mascara = new AvatarMask(); AssetDatabase.CreateAsset(mascara, MASCARA); }
        for (AvatarMaskBodyPart p = 0; p < AvatarMaskBodyPart.LastBodyPart; p++)
            mascara.SetHumanoidBodyPartActive(p, p == AvatarMaskBodyPart.Body || p == AvatarMaskBodyPart.Head ||
                p == AvatarMaskBodyPart.LeftArm || p == AvatarMaskBodyPart.RightArm || p == AvatarMaskBodyPart.LeftFingers ||
                p == AvatarMaskBodyPart.RightFingers || p == AvatarMaskBodyPart.LeftHandIK || p == AvatarMaskBodyPart.RightHandIK);
        EditorUtility.SetDirty(mascara);
        AnimationClip pronta = Clipe("Magia_Pronta");
        if (pronta != null)
        {
            AnimatorStateMachine smB = new AnimatorStateMachine { name = "Escudo", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(smB, CONTROLADOR);
            AnimatorState s = smB.AddState("segurar", new Vector3(300f, 0f, 0f));
            s.motion = pronta;
            smB.defaultState = s;
            ctrl.AddLayer(new AnimatorControllerLayer { name = "Escudo", stateMachine = smB, avatarMask = mascara, blendingMode = AnimatorLayerBlendingMode.Override, defaultWeight = 0f });
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // ------------------------------------------------------------------
    //  jogador da Maga: modelo dela + os scripts do personagem da cena
    // ------------------------------------------------------------------
    static GameObject CriarJogador(GameObject baseJogador, AnimatorController ctrl, List<string> avisos)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MODELO));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        go.name = "Jogador_Maga";
        Animator anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MODELO)) if (o is Avatar) anim.avatar = (Avatar)o;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CharacterController ccBase = baseJogador.GetComponent<CharacterController>();
        CharacterController cc = go.AddComponent<CharacterController>();
        float pele = ccBase != null ? ccBase.skinWidth : 0.08f;
        cc.height = 1.72f; cc.radius = 0.33f; cc.center = new Vector3(0f, 0.86f + pele, 0f); cc.skinWidth = pele;
        if (ccBase != null) { cc.stepOffset = ccBase.stepOffset; cc.slopeLimit = ccBase.slopeLimit; }
        go.tag = baseJogador.tag;

        // copia os scripts comuns a todo jogador (controles, vida, combate, interface)
        foreach (MonoBehaviour mb in baseJogador.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n != "PlayerLocomotion" && n != "Vida" && n != "CombateJogador" && n != "HUDJogador") continue;
            Component c = go.GetComponent(mb.GetType());
            if (c == null) c = go.AddComponent(mb.GetType());
            if (UnityEditorInternal.ComponentUtility.CopyComponent(mb)) UnityEditorInternal.ComponentUtility.PasteComponentValues(c);
        }
        PlayerLocomotion loco = go.GetComponent<PlayerLocomotion>();
        if (loco != null)
        {
            SerializedObject so = new SerializedObject(loco);
            so.FindProperty("walkSpeed").floatValue = ANDAR;
            so.FindProperty("runSpeed").floatValue = CORRER;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        CombateJogador comb = go.GetComponent<CombateJogador>();
        if (comb != null) comb.ataqueCorpoACorpo = false;
        go.AddComponent<EscudoAnimacao>();

        MagiaJogador mag = go.AddComponent<MagiaJogador>();
        mag.cor = ROXO;
        mag.materialBrilho = PingluEfeitos.Mat("Maga_Brilho", "faisca", true, new Color(ROXO.r * 4f, ROXO.g * 4f, ROXO.b * 4f, 1f));
        mag.materialRastro = PingluEfeitos.Mat("Maga_Rastro", "rastro_energia", true, new Color(ROXO.r * 3f, ROXO.g * 3f, ROXO.b * 3f, 1f));
        mag.materialBarreira = MaterialBarreira();

        // rede
        go.AddComponent<NetworkObject>();
        NetworkTransform nt = go.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
        NetworkAnimator na = go.AddComponent<NetworkAnimator>();
        na.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
        na.Animator = anim;
        go.AddComponent<JogadorRede>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PREFAB);
        Object.DestroyImmediate(go);

        NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(LISTA);
        if (lista != null)
        {
            bool tem = false;
            foreach (NetworkPrefab p in lista.PrefabList) if (p.Prefab == prefab) tem = true;
            if (!tem) lista.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(lista);
        }
        else avisos.Add("Não achei a lista de prefabs de rede (rode o Pinglu > 15).");
        return prefab;
    }

    static Material MaterialBarreira()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(BARREIRA);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, BARREIRA); }
        m.SetColor("_BaseColor", new Color(0.62f, 0.38f, 1f, 0.16f));
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // soma luz: brilha
        m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void AtualizarFicha(GameObject prefab, List<string> avisos)
    {
        FichaPersonagem f = AssetDatabase.LoadAssetAtPath<FichaPersonagem>(FICHA);
        if (f == null) { avisos.Add("Não achei a ficha do personagem 2 (rode o Pinglu > 15)."); return; }
        f.nome = "Morgana";
        f.titulo = "A Tecelã do Relâmpago";
        f.descricao = "Feiticeira encapuzada que dispara esferas arcanas com a mão e invoca relâmpagos pelo cajado. Flutua para se mover rápido.";
        f.habilidades = new[] { "Esfera arcana com mira automática (clique esquerdo)", "3º golpe: relâmpago que atinge todos na linha",
                                "Barreira mágica (segurar o botão direito)", "Levitar (Shift) e deslize flutuando (Ctrl)" };
        f.corDestaque = ROXO;
        f.forca = 6; f.defesa = 4; f.velocidade = 7; f.alcance = 10;
        f.modeloVisual = AssetDatabase.LoadAssetAtPath<GameObject>(MODELO);
        f.animacaoParado = Clipe("Parado");
        f.animacaoEntrada = Clipe("Raio_Cajado");
        f.prefabJogador = prefab;
        f.disponivel = true;
        EditorUtility.SetDirty(f);
    }

    static GameObject AcharJogadorDaCena()
    {
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || EditorUtility.IsPersistent(p) || !p.gameObject.activeInHierarchy) continue;
            string n = p.gameObject.name.ToLower();
            if (n.Contains("x bot") || n.Contains("antigo")) continue;
            return p.gameObject;
        }
        return null;
    }
}
#endif
