// =====================================================================
//  PingluCurandeiro.cs  -  Curandeiro (serafim de ossos) como personagem jogável
// ---------------------------------------------------------------------
//  1. Configura o modelo e as animações (script do colega, sem trocar de cena)
//  2. Animator: andar/correr, cura, concentração (defesa), voo (decolar,
//     voar, voar à frente, pousar), dano e morte
//  3. Cria o jogador do Curandeiro (com rede) a partir do personagem da cena
//  4. Ficha 4 da seleção: nome, título, atributos, palco
//
//  COMO USAR:  Pinglu > 34. Curandeiro jogável
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

public static class PingluCurandeiro
{
    const string RAIZ = "Assets/Personagens/Curandeiro";
    const string MODELO = RAIZ + "/curandeiro_rigged.fbx";
    const string ANIMS = RAIZ + "/animacoes/curandeiro@";
    const string CONTROLADOR = RAIZ + "/Jogador_Curandeiro.controller";
    const string PREFAB = "Assets/Personagens/Jogadores/Jogador_Curandeiro.prefab";
    const string LISTA = "Assets/Personagens/Jogadores/Prefabs de Rede.asset";
    const string FICHA = "Assets/Personagens/Jogadores/Fichas/4_Personagem4.asset";
    const string FONTE = "Assets/Fontes/MarcellusSC-Regular.ttf";
    const float ANDAR = 2.0f, CORRER = 4.4f;
    static readonly Color VERDE = new Color(0.35f, 1f, 0.55f);

    [MenuItem("Pinglu/34. Curandeiro jogável")]
    public static void Montar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MODELO) == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei " + MODELO + ".\n\nTraga a pasta do Curandeiro da branch \"rigs\":\ngit checkout origin/rigs -- Assets/Personagens/Curandeiro\ne rode o git lfs pull.", "OK");
            return;
        }
        GameObject baseJogador = AcharJogadorDaCena();
        if (baseJogador == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o personagem jogável na cena (o Cavaleiro Sombrio). Abra a SampleScene.", "OK"); return; }

        List<string> avisos = new List<string>();
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Configurando o modelo e as animações do Curandeiro...", 0.15f);
            ConfigurarComScriptDoColega(avisos);
            EditorUtility.DisplayProgressBar("Pinglu", "Montando o Animator...", 0.4f);
            AnimatorController ctrl = MontarAnimator(avisos);
            EditorUtility.DisplayProgressBar("Pinglu", "Criando o jogador do Curandeiro...", 0.65f);
            GameObject prefab = CriarJogador(baseJogador, ctrl, avisos);
            EditorUtility.DisplayProgressBar("Pinglu", "Preenchendo a ficha da seleção...", 0.9f);
            AtualizarFicha(prefab, avisos);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(baseJogador.scene);
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console)."); }
        finally { EditorUtility.ClearProgressBar(); }

        EditorUtility.DisplayDialog("Pinglu", "Curandeiro pronto para jogar!\n\n" +
            "- Clique esquerdo: cura em área (7 m): ele e os aliados vivos perto dele (a cada 1,4 s)\n- Botão direito: concentração, segura 50% do dano\n" +
            "- F: decolar e pousar  |  Voando: Espaço sobe, C desce, Shift mais rápido\n- Ctrl: investida (invulnerável)\n\n" +
            "Ele aparece como personagem 4 na tela de seleção, sozinho e online.\n\nSalve a cena com Ctrl + S." +
            (avisos.Count > 0 ? "\n\nAvisos:\n- " + string.Join("\n- ", avisos) : ""), "OK");
    }

    // ------------------------------------------------------------------
    static void ConfigurarComScriptDoColega(List<string> avisos)
    {
        System.Type t = null;
        foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("MontarCurandeiro"); if (t != null) break; }
        if (t == null) { avisos.Add("Não achei o script MontarCurandeiro do colega; usei a configuração que já estava."); return; }
        BindingFlags f = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        MethodInfo texturas = t.GetMethod("Texturas", f), personagem = t.GetMethod("Personagem", f), clipes = t.GetMethod("ImportarClipes", f);
        if (texturas == null || personagem == null || clipes == null) { avisos.Add("O script do colega mudou; rode \"Curandeiro > Montar tudo\" e depois este menu de novo."); return; }
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

    static AnimatorStateTransition Transicao(AnimatorState de, AnimatorState para, float tempo, float duracao, AnimatorConditionMode modo = AnimatorConditionMode.If, string parametro = null, float valor = 0f)
    {
        if (de == null || para == null) return null;
        AnimatorStateTransition t = de.AddTransition(para);
        t.hasExitTime = tempo >= 0f; if (tempo >= 0f) t.exitTime = tempo;
        t.duration = duracao; t.hasFixedDuration = true;
        if (!string.IsNullOrEmpty(parametro)) t.AddCondition(modo, valor, parametro);
        return t;
    }

    static AnimatorController MontarAnimator(List<string> avisos)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLADOR) != null) AssetDatabase.DeleteAsset(CONTROLADOR);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(CONTROLADOR);
        foreach (string p in new[] { "Jump", "Roll", "AttackLight", "AttackHeavy" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Trigger);
        foreach (string p in new[] { "Block", "Grounded", "Rolling", "Voando" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // no chão: parado / andando / correndo
        BlendTree bt;
        AnimatorState loco = ctrl.CreateBlendTreeInController("Blend Tree", out bt, 0);
        bt.blendParameter = "Speed";
        bt.AddChild(Clipe("Parado"), 0f);
        bt.AddChild(Clipe("Andar"), 0.5f);
        bt.AddChild(Clipe("Correr"), 1f);
        ChildMotion[] filhos = bt.children;
        if (filhos.Length == 3) { filhos[1].timeScale = ANDAR / 1.1f; filhos[2].timeScale = CORRER / 2.2f; bt.children = filhos; }
        sm.defaultState = loco;

        // cura: o disparo (evento LancarCura) acontece no meio da animação
        AnimatorState a1 = Estado(sm, "attack", "Curar_Lancar", new Vector3(450f, 0f, 0f), avisos, 1.1f);
        AnimatorState a2 = Estado(sm, "attack_2", "Curar_Lancar", new Vector3(450f, 60f, 0f), avisos, 1.1f);
        AnimatorState a3 = Estado(sm, "attack_3", "Curar_Lancar", new Vector3(450f, 120f, 0f), avisos, 1.1f);
        AnimatorState ap = Estado(sm, "attack_heavy", "Curar_Lancar", new Vector3(450f, 180f, 0f), avisos, 0.9f);
        foreach (AnimatorState s in new[] { a1, a2, a3, ap }) Transicao(s, loco, 0.9f, 0.15f);

        // pulinho (usa a decolagem rápida) e investida (corrida acelerada)
        AnimatorState pulo = Estado(sm, "Jump", "Decolar", new Vector3(0f, 250f, 0f), avisos, 2.2f);
        Transicao(loco, pulo, -1f, 0.1f, AnimatorConditionMode.If, "Jump");
        Transicao(pulo, loco, 0.5f, 0.2f, AnimatorConditionMode.If, "Grounded");
        AnimatorState desvio = Estado(sm, "roll", "Correr", new Vector3(200f, 250f, 0f), avisos, 2f);
        if (desvio != null)
        {
            AnimatorStateTransition ida = sm.AddAnyStateTransition(desvio);
            ida.hasExitTime = false; ida.duration = 0.08f; ida.canTransitionToSelf = false; ida.AddCondition(AnimatorConditionMode.If, 0f, "Roll");
            Transicao(desvio, loco, -1f, 0.15f, AnimatorConditionMode.IfNot, "Rolling");
        }

        // concentração (botão direito): entrar > segurar > sair
        AnimatorState erguer = Estado(sm, "escudo_erguer", "Curar_Entrar", new Vector3(300f, 320f, 0f), avisos);
        AnimatorState segurar = Estado(sm, "escudo_segurar", "Curar_Pronta", new Vector3(500f, 320f, 0f), avisos);
        AnimatorState baixar = Estado(sm, "escudo_baixar", "Curar_Sair", new Vector3(300f, 400f, 0f), avisos);
        Transicao(loco, erguer, -1f, 0.1f, AnimatorConditionMode.If, "Block");
        Transicao(erguer, baixar, -1f, 0.08f, AnimatorConditionMode.IfNot, "Block");
        Transicao(erguer, segurar, 0.9f, 0.1f);
        Transicao(segurar, baixar, -1f, 0.08f, AnimatorConditionMode.IfNot, "Block");
        Transicao(baixar, loco, 0.9f, 0.12f);

        // voo: decolar > voar / voar à frente > pousar
        AnimatorState decolar = Estado(sm, "Decolar", "Decolar", new Vector3(0f, 500f, 0f), avisos);
        AnimatorState voar = Estado(sm, "Voar", "Voar", new Vector3(200f, 500f, 0f), avisos);
        AnimatorState voarFrente = Estado(sm, "Voar_Frente", "Voar_Frente", new Vector3(400f, 500f, 0f), avisos);
        AnimatorState pousar = Estado(sm, "Pousar", "Pousar", new Vector3(200f, 600f, 0f), avisos);
        Transicao(loco, decolar, -1f, 0.1f, AnimatorConditionMode.If, "Voando");
        Transicao(decolar, voar, 0.9f, 0.1f);
        Transicao(voar, voarFrente, -1f, 0.2f, AnimatorConditionMode.Greater, "Speed", 0.1f);
        Transicao(voarFrente, voar, -1f, 0.2f, AnimatorConditionMode.Less, "Speed", 0.1f);
        foreach (AnimatorState s in new[] { decolar, voar, voarFrente }) Transicao(s, pousar, -1f, 0.15f, AnimatorConditionMode.IfNot, "Voando");
        Transicao(pousar, loco, 0.9f, 0.15f);

        // dano e morte (se estiver voando, volta a voar)
        AnimatorState hit = Estado(sm, "hit", "Levar_Golpe", new Vector3(650f, 0f, 0f), avisos);
        Transicao(hit, voar, 0.85f, 0.15f, AnimatorConditionMode.If, "Voando");
        Transicao(hit, loco, 0.85f, 0.15f, AnimatorConditionMode.IfNot, "Voando");
        Estado(sm, "death", "Morrer", new Vector3(650f, 70f, 0f), avisos);

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // ------------------------------------------------------------------
    //  jogador do Curandeiro: modelo dele + os scripts do personagem da cena
    // ------------------------------------------------------------------
    static GameObject CriarJogador(GameObject baseJogador, AnimatorController ctrl, List<string> avisos)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MODELO));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        go.name = "Jogador_Curandeiro";
        Animator anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MODELO)) if (o is Avatar) anim.avatar = (Avatar)o;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CharacterController ccBase = baseJogador.GetComponent<CharacterController>();
        CharacterController cc = go.AddComponent<CharacterController>();
        float pele = ccBase != null ? ccBase.skinWidth : 0.08f;
        cc.height = 1.75f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.875f + pele, 0f); cc.skinWidth = pele;
        if (ccBase != null) { cc.stepOffset = ccBase.stepOffset; cc.slopeLimit = ccBase.slopeLimit; }
        go.tag = baseJogador.tag;

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
            so.FindProperty("rollSpeed").floatValue = 7f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Vida vida = go.GetComponent<Vida>();
        if (vida != null)
        {
            SerializedObject sv = new SerializedObject(vida);
            sv.FindProperty("vidaMaxima").floatValue = 110f;
            sv.ApplyModifiedPropertiesWithoutUndo();
            vida.DefinirMaxima(110f);
        }
        CombateJogador comb = go.GetComponent<CombateJogador>();
        if (comb != null)
        {
            comb.ataqueCorpoACorpo = false;
            SerializedObject sc = new SerializedObject(comb);
            sc.FindProperty("bloqueio").floatValue = 0.5f;
            sc.FindProperty("defendeSoDeFrente").boolValue = false;
            sc.ApplyModifiedPropertiesWithoutUndo();
        }
        CuraCurandeiro visual = go.AddComponent<CuraCurandeiro>();
        visual.cor = VERDE;
        visual.AcharOssos();
        go.AddComponent<CuraJogador>();
        go.AddComponent<VooTanque>();

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
        PingluCodigosDeRede.Consertar(); // grava o código de rede do prefab novo
        return prefab;
    }

    static void AtualizarFicha(GameObject prefab, List<string> avisos)
    {
        FichaPersonagem f = AssetDatabase.LoadAssetAtPath<FichaPersonagem>(FICHA);
        if (f == null) { avisos.Add("Não achei a ficha do personagem 4 (rode o Pinglu > 15)."); return; }
        f.nome = "Curandeiro";
        f.titulo = "O Serafim de Ossos";
        f.descricao = "Serafim de ossos com auréola e asas rasgadas. Lança uma luz que cura a ele e a todos os aliados por perto, se concentra para resistir aos golpes e voa sobre o campo de batalha para socorrer o grupo.";
        f.habilidades = new[] { "Cura em área: aliados e ele mesmo (clique esquerdo)", "Concentração: segura 50% do dano (botão direito)",
                                "Voo livre: F decola, Espaço sobe, C desce", "Investida rápida (Ctrl)" };
        f.corDestaque = VERDE;
        f.forca = 2; f.defesa = 5; f.velocidade = 6; f.alcance = 6;
        f.fonteNome = AssetDatabase.LoadAssetAtPath<Font>(FONTE);
        f.tamanhoNome = 76;
        f.corNome = new Color(0.80f, 1f, 0.86f, 1f);
        f.modeloVisual = AssetDatabase.LoadAssetAtPath<GameObject>(MODELO);
        f.animacaoParado = Clipe("Parado");
        f.animacaoEntrada = Clipe("Curar_Entrar");
        f.animacaoAndar = Clipe("Andar");
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
