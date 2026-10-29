// =====================================================================
//  PingluCavaleiroSombrio.cs  -  Cavaleiro Sombrio vira o personagem jogável
// ---------------------------------------------------------------------
//  1. Configura o modelo e as animações do Cavaleiro Sombrio (usando o
//     script do colega, mas SEM trocar de cena)
//  2. Monta um Animator próprio: combo A/B/C, defesa da cintura pra cima,
//     pulo, rolagem, dano e morte
//  3. Coloca ele no lugar do cavaleiro atual (controles, vida, combate,
//     interface e câmera) com velocidades que combinam com as animações
//  4. O cavaleiro atual é GUARDADO como mini-boss (prefab + desativado na
//     cena), com o efeito de chama carmesim
//  5. Efeito de espada novo, com a cor tirada das texturas do personagem
//  6. Atualiza o jogador do multiplayer e a ficha do personagem
//
//  COMO USAR:  Pinglu > 18. Trocar para o Cavaleiro Sombrio
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
#if PINGLU_REDE
using Unity.Netcode;
using Unity.Netcode.Components;
#endif

public static class PingluCavaleiroSombrio
{
    const string RAIZ = "Assets/Personagens/CavaleiroSombrio";
    const string MODELO = RAIZ + "/cavaleiro_sombrio_rigged.fbx";
    const string ANIMS = RAIZ + "/animacoes/cavaleiro_sombrio@";
    const string CONTROLADOR = RAIZ + "/Jogador_CavaleiroSombrio.controller";
    const string MASCARA = RAIZ + "/Mascara_Defesa.mask";
    const string PREFAB_MINIBOSS = "Assets/Personagens/Cavaleiro/Cavaleiro_MiniBoss.prefab";
    const string PREFAB_REDE = "Assets/Personagens/Jogadores/Jogador_CavaleiroSombrio.prefab";
    const string LISTA_REDE = "Assets/Personagens/Jogadores/Prefabs de Rede.asset";
    const string FICHA = "Assets/Personagens/Jogadores/Fichas/1_Cavaleiro.asset";
    const string PACOTE_XBOT = "Assets/Personagens/Sword and Shield Pack";
    const string NOME_NOVO = "Jogador (Cavaleiro Sombrio)";
    const string NOME_ANTIGO = "Cavaleiro Antigo (mini-boss)";

    [MenuItem("Pinglu/18. Trocar para o Cavaleiro Sombrio")]
    public static void Trocar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MODELO) == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei " + MODELO + ".\n\nTraga a pasta do Cavaleiro Sombrio da branch \"rigs\" (veja as instruções do Claude) e rode o git lfs pull.", "OK");
            return;
        }
        GameObject antigo = AcharJogadorDaCena();
        if (antigo == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o personagem atual na cena. Abra a SampleScene.", "OK"); return; }
        if (antigo.name == NOME_NOVO) { EditorUtility.DisplayDialog("Pinglu", "O Cavaleiro Sombrio já é o personagem jogável.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou colocar o Cavaleiro Sombrio como personagem jogável.\n\nO cavaleiro atual NÃO será apagado: ele fica guardado como mini-boss (prefab em " +
            PREFAB_MINIBOSS + " e desativado na cena), com o efeito de chama carmesim.", "Pode trocar", "Cancelar")) return;

        List<string> avisos = new List<string>();
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Configurando o modelo e as animações...", 0.1f);
            ConfigurarComScriptDoColega(avisos);

            EditorUtility.DisplayProgressBar("Pinglu", "Montando o Animator...", 0.35f);
            AnimatorController ctrl = MontarAnimator(avisos);

            EditorUtility.DisplayProgressBar("Pinglu", "Guardando o cavaleiro antigo como mini-boss...", 0.5f);
            GuardarMiniBoss(antigo);

            EditorUtility.DisplayProgressBar("Pinglu", "Colocando o Cavaleiro Sombrio na cena...", 0.6f);
            GameObject novo = ColocarNaCena(antigo, ctrl, avisos);

            EditorUtility.DisplayProgressBar("Pinglu", "Criando o efeito da espada...", 0.75f);
            string cor = EfeitoDaEspada(novo, avisos);

            EditorUtility.DisplayProgressBar("Pinglu", "Atualizando o multiplayer...", 0.9f);
            AtualizarMultiplayer(novo, avisos);

            antigo.name = NOME_ANTIGO;
            antigo.SetActive(false);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(novo.scene);
            Selection.activeGameObject = novo;
            avisos.Insert(0, "Cor dos efeitos (tirada das texturas): " + cor);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console).");
        }
        finally { EditorUtility.ClearProgressBar(); }

        EditorUtility.DisplayDialog("Pinglu", "Cavaleiro Sombrio pronto para jogar!\n\nSALVE A CENA (Ctrl + S).\n\n" + string.Join("\n", avisos), "OK");
    }

    // ------------------------------------------------------------------
    //  1) usa os passos do script do colega (Texturas, Personagem, Clipes),
    //     menos o que cria a cena de teste
    // ------------------------------------------------------------------
    static void ConfigurarComScriptDoColega(List<string> avisos)
    {
        System.Type t = null;
        foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            t = a.GetType("MontarCavaleiroSombrio");
            if (t != null) break;
        }
        if (t == null) { avisos.Add("Não achei o script MontarCavaleiroSombrio do colega; usei a configuração que já estava."); return; }
        BindingFlags f = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        MethodInfo texturas = t.GetMethod("Texturas", f), personagem = t.GetMethod("Personagem", f), clipes = t.GetMethod("Clipes", f);
        if (texturas == null || personagem == null || clipes == null) { avisos.Add("O script do colega mudou; rode \"Cavaleiro Sombrio > Montar tudo\" e depois este menu de novo."); return; }
        texturas.Invoke(null, null);
        object avatar = personagem.Invoke(null, null);
        clipes.Invoke(null, new[] { avatar });
    }

    static AnimationClip Clipe(string caminho)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(caminho))
        {
            AnimationClip c = o as AnimationClip;
            if (c != null && !c.name.StartsWith("__preview__")) return c;
        }
        return null;
    }
    static AnimationClip DoSombrio(string nome) { return Clipe(ANIMS + nome + ".fbx"); }

    // ------------------------------------------------------------------
    //  2) Animator com os mesmos nomes que o nosso script de movimento usa
    // ------------------------------------------------------------------
    static AnimatorController MontarAnimator(List<string> avisos)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLADOR) != null) AssetDatabase.DeleteAsset(CONTROLADOR);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(CONTROLADOR);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("AttackLight", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("AttackHeavy", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Block", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Rolling", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Armado", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // locomoção: parado / andar / correr com a espada na mão
        BlendTree bt;
        AnimatorState loco = ctrl.CreateBlendTreeInController("Blend Tree", out bt, 0);
        bt.blendParameter = "Speed";
        bt.AddChild(DoSombrio("Parado"), 0f);
        bt.AddChild(DoSombrio("Andar"), 0.5f);
        bt.AddChild(DoSombrio("Correr_Com_Espada"), 1f);
        ChildMotion[] filhos = bt.children;
        if (filhos.Length == 3) { filhos[1].timeScale = VELOCIDADE_ANDAR / 0.73f; filhos[2].timeScale = VELOCIDADE_CORRER / 3.0f; bt.children = filhos; }
        sm.defaultState = loco;

        // locomoção com a espada nas costas + guardar/sacar
        AnimatorState semEspada = null;
        AnimationClip pSem = DoSombrio("Parado_Sem_Espada"), aSem = DoSombrio("Andar_Sem_Espada"), cSem = DoSombrio("Correr");
        AnimationClip cGuardar = DoSombrio("Guardar_Espada"), cSacar = DoSombrio("Sacar_Espada");
        if (pSem != null && aSem != null && cSem != null && cGuardar != null && cSacar != null)
        {
            BlendTree bt2;
            semEspada = ctrl.CreateBlendTreeInController("Locomocao Sem Espada", out bt2, 0);
            bt2.blendParameter = "Speed";
            bt2.AddChild(pSem, 0f); bt2.AddChild(aSem, 0.5f); bt2.AddChild(cSem, 1f);
            ChildMotion[] f2 = bt2.children;
            if (f2.Length == 3) { f2[1].timeScale = VELOCIDADE_ANDAR / 0.73f; f2[2].timeScale = VELOCIDADE_CORRER / 3.19f; bt2.children = f2; }
            sm.defaultState = semEspada; // começa fora de combate

            AnimatorState guardar = sm.AddState("guardar", new Vector3(250f, -120f, 0f)); guardar.motion = cGuardar;
            AnimatorState sacar = sm.AddState("sacar", new Vector3(250f, -60f, 0f)); sacar.motion = cSacar; sacar.speed = 1.3f;
            Transicao(loco, guardar, -1f, 0.15f, AnimatorConditionMode.IfNot, "Armado");
            Transicao(guardar, semEspada, 0.95f, 0.15f);
            Transicao(guardar, sacar, -1f, 0.1f, AnimatorConditionMode.If, "Armado");     // mudou de ideia no meio
            Transicao(semEspada, sacar, -1f, 0.1f, AnimatorConditionMode.If, "Armado");
            Transicao(sacar, loco, 0.9f, 0.12f);
            Transicao(sacar, guardar, -1f, 0.1f, AnimatorConditionMode.IfNot, "Armado");
        }
        else avisos.Add("Faltou alguma animação sem espada / guardar / sacar; o personagem fica sempre com a espada na mão.");

        // combo, golpe pesado, investida
        string[] golpes = { "attack", "attack_2", "attack_3", "attack_heavy", "investida" };
        string[] clipes = { "Ataque_A", "Ataque_B", "Ataque_C", "Ataque_Pesado", "Investida" };
        for (int i = 0; i < golpes.Length; i++)
        {
            AnimationClip c = DoSombrio(clipes[i]);
            if (c == null) { avisos.Add("Faltou a animação " + clipes[i]); continue; }
            AnimatorState st = sm.AddState(golpes[i], new Vector3(450f, i * 60f, 0f));
            st.motion = c;
            Saida(st, loco, 0.9f);
        }

        // pulo e rolagem (do pacote do X Bot; o esqueleto é Humanoid, então servem)
        AnimationClip pulo = Clipe(PACOTE_XBOT + "/Jump.fbx");
        if (pulo != null)
        {
            AnimatorState st = sm.AddState("Jump", new Vector3(0f, 250f, 0f)); st.motion = pulo;
            Transicao(loco, st, -1f, 0.1f, AnimatorConditionMode.If, "Jump");
            if (semEspada != null) Transicao(semEspada, st, -1f, 0.1f, AnimatorConditionMode.If, "Jump");
            VoltarParaLocomocao(st, loco, semEspada, 0.35f, AnimatorConditionMode.If, "Grounded");
        }
        AnimationClip rolar = Clipe(PACOTE_XBOT + "/Roll.fbx");
        if (rolar != null)
        {
            AnimatorState st = sm.AddState("roll", new Vector3(200f, 250f, 0f)); st.motion = rolar;
            AnimatorStateTransition ida = sm.AddAnyStateTransition(st); ida.hasExitTime = false; ida.duration = 0.08f; ida.canTransitionToSelf = false; ida.AddCondition(AnimatorConditionMode.If, 0f, "Roll");
            VoltarParaLocomocao(st, loco, semEspada, -1f, AnimatorConditionMode.IfNot, "Rolling");
        }

        // dano (tranco do pacote UAL2) e morte (Levar_Golpe: é arremessado e cai)
        AnimationClip hit = AcharDanoUAL();
        if (hit != null) { AnimatorState st = sm.AddState("hit", new Vector3(650f, 0f, 0f)); st.motion = hit; VoltarParaLocomocao(st, loco, null, 0.85f, AnimatorConditionMode.If, null); }
        AnimationClip morte = DoSombrio("Levar_Golpe");
        if (morte != null) { AnimatorState st = sm.AddState("death", new Vector3(650f, 70f, 0f)); st.motion = morte; }

        // defesa da cintura pra cima (as pernas continuam andando)
        AvatarMask mascara = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASCARA);
        if (mascara == null) { mascara = new AvatarMask(); AssetDatabase.CreateAsset(mascara, MASCARA); }
        for (AvatarMaskBodyPart p = 0; p < AvatarMaskBodyPart.LastBodyPart; p++)
            mascara.SetHumanoidBodyPartActive(p, p == AvatarMaskBodyPart.Body || p == AvatarMaskBodyPart.Head ||
                p == AvatarMaskBodyPart.LeftArm || p == AvatarMaskBodyPart.RightArm || p == AvatarMaskBodyPart.LeftFingers ||
                p == AvatarMaskBodyPart.RightFingers || p == AvatarMaskBodyPart.LeftHandIK || p == AvatarMaskBodyPart.RightHandIK);
        EditorUtility.SetDirty(mascara);
        AnimationClip defender = DoSombrio("Defender");
        if (defender != null)
        {
            AnimatorStateMachine smDef = new AnimatorStateMachine { name = "Escudo", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(smDef, CONTROLADOR);
            AnimatorState segurar = smDef.AddState("segurar", new Vector3(300f, 0f, 0f));
            segurar.motion = defender;
            segurar.speed = 0f;            // congela na pose de guarda
            segurar.cycleOffset = 0.35f;   // ponto da animação com a espada erguida (ajustável no Animator)
            smDef.defaultState = segurar;
            ctrl.AddLayer(new AnimatorControllerLayer { name = "Escudo", stateMachine = smDef, avatarMask = mascara, blendingMode = AnimatorLayerBlendingMode.Override, defaultWeight = 0f });
        }
        else avisos.Add("Faltou a animação Defender.");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    const float VELOCIDADE_ANDAR = 1.25f;
    const float VELOCIDADE_CORRER = 3.6f;

    // transição com (ou sem, se tempo < 0) tempo de saída e uma condição opcional
    static AnimatorStateTransition Transicao(AnimatorState de, AnimatorState para, float tempo, float duracao, AnimatorConditionMode modo = AnimatorConditionMode.If, string parametro = null)
    {
        AnimatorStateTransition t = de.AddTransition(para);
        t.hasExitTime = tempo >= 0f; if (tempo >= 0f) t.exitTime = tempo;
        t.duration = duracao; t.hasFixedDuration = true;
        if (!string.IsNullOrEmpty(parametro)) t.AddCondition(modo, 0f, parametro);
        return t;
    }

    // volta para a locomoção com espada (se Armado) ou sem espada (se não)
    static void VoltarParaLocomocao(AnimatorState de, AnimatorState comEspada, AnimatorState semEspada, float tempo, AnimatorConditionMode modo, string parametro)
    {
        AnimatorStateTransition a = Transicao(de, comEspada, tempo, 0.15f, modo, parametro);
        if (semEspada == null) return;
        a.AddCondition(AnimatorConditionMode.If, 0f, "Armado");
        AnimatorStateTransition b = Transicao(de, semEspada, tempo, 0.15f, modo, parametro);
        b.AddCondition(AnimatorConditionMode.IfNot, 0f, "Armado");
    }

    static void Saida(AnimatorState de, AnimatorState para, float tempo)
    {
        AnimatorStateTransition t = de.AddTransition(para);
        t.hasExitTime = true; t.exitTime = tempo; t.duration = 0.15f; t.hasFixedDuration = true;
    }

    static AnimationClip AcharDanoUAL()
    {
        AnimationClip melhor = null;
        foreach (string g in AssetDatabase.FindAssets("UAL2_Standard"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.EndsWith(".fbx") || p.Contains("_RM")) continue;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
            {
                AnimationClip c = o as AnimationClip;
                if (c == null || c.name.StartsWith("__preview__")) continue;
                string n = c.name.ToLower();
                if (n.Contains("hit") && !n.Contains("death") && (melhor == null || n.Contains("chest") || n.Contains("head"))) melhor = c;
            }
        }
        return melhor;
    }

    // script do colega que troca a espada de lugar (mão/costas) + nosso sincronizador
    static void PrepararBainha(GameObject go)
    {
        System.Type t = null;
        foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("GuardaEspada"); if (t != null) break; }
        if (t != null)
        {
            Component g = go.GetComponent(t);
            if (g == null) g = go.AddComponent(t);
            MethodInfo achar = t.GetMethod("AcharOssos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (achar != null) achar.Invoke(g, null);
        }
        if (go.GetComponent<SincronizarEspada>() == null) go.AddComponent<SincronizarEspada>();
    }

    // ------------------------------------------------------------------
    //  Pinglu > 19: aplica guardar/sacar no Cavaleiro Sombrio que já está na cena
    // ------------------------------------------------------------------
    [MenuItem("Pinglu/19. Guardar e sacar espada")]
    public static void AplicarBainha()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        GameObject jogador = AcharJogadorDaCena();
        if (jogador == null || jogador.name != NOME_NOVO) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Cavaleiro Sombrio na cena. Rode antes o Pinglu > 18.", "OK"); return; }
        List<string> avisos = new List<string>();
        AnimatorController ctrl = MontarAnimator(avisos);
        Animator anim = jogador.GetComponent<Animator>();
        Undo.RecordObject(anim, "Guardar e sacar");
        anim.runtimeAnimatorController = ctrl;
        PrepararBainha(jogador);
#if PINGLU_REDE
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_REDE) != null)
        {
            GameObject conteudo = PrefabUtility.LoadPrefabContents(PREFAB_REDE);
            Animator ap = conteudo.GetComponent<Animator>();
            if (ap != null) ap.runtimeAnimatorController = ctrl;
            PrepararBainha(conteudo);
            PrefabUtility.SaveAsPrefabAsset(conteudo, PREFAB_REDE);
            PrefabUtility.UnloadPrefabContents(conteudo);
            avisos.Add("Jogador do multiplayer atualizado também.");
        }
#endif
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(jogador.scene);
        EditorUtility.DisplayDialog("Pinglu", "Guardar/sacar espada pronto!\n\n- Começa com a espada nas costas\n- Atacar, defender ou levar golpe: saca\n- 1 minuto sem lutar: guarda sozinho\n- Tecla H: guarda/saca na hora\n\nSalve a cena com Ctrl + S." +
            (avisos.Count > 0 ? "\n\n" + string.Join("\n", avisos) : ""), "OK");
    }

    // ------------------------------------------------------------------
    //  4) o cavaleiro atual vira prefab de mini-boss (com o efeito carmesim)
    // ------------------------------------------------------------------
    static void GuardarMiniBoss(GameObject antigo)
    {
        GameObject copia = Object.Instantiate(antigo);
        copia.name = "Cavaleiro_MiniBoss";
        copia.SetActive(true);
        PrefabUtility.SaveAsPrefabAsset(copia, PREFAB_MINIBOSS);
        Object.DestroyImmediate(copia);
    }

    // ------------------------------------------------------------------
    //  3) o Cavaleiro Sombrio entra no lugar do atual
    // ------------------------------------------------------------------
    static GameObject ColocarNaCena(GameObject antigo, AnimatorController ctrl, List<string> avisos)
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(MODELO);
        GameObject novo = (GameObject)PrefabUtility.InstantiatePrefab(modelo, antigo.scene);
        Undo.RegisterCreatedObjectUndo(novo, "Cavaleiro Sombrio");
        novo.name = NOME_NOVO;
        novo.transform.SetParent(antigo.transform.parent, false);
        novo.transform.SetPositionAndRotation(antigo.transform.position, antigo.transform.rotation);
        novo.tag = antigo.tag;

        Animator anim = novo.GetComponent<Animator>();
        if (anim == null) anim = novo.AddComponent<Animator>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MODELO)) if (o is Avatar) anim.avatar = (Avatar)o;
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (anim.avatar == null || !anim.avatar.isHuman) avisos.Add("O esqueleto do Cavaleiro Sombrio não ficou Humanoid; confira o modelo (aba Rig > Configure...).");

        CharacterController ccAntigo = antigo.GetComponent<CharacterController>();
        CharacterController cc = novo.AddComponent<CharacterController>();
        float pele = ccAntigo != null ? ccAntigo.skinWidth : 0.08f;
        cc.height = 1.8f; cc.radius = 0.38f; cc.center = new Vector3(0f, 0.9f + pele, 0f);
        cc.skinWidth = pele; cc.stepOffset = ccAntigo != null ? ccAntigo.stepOffset : 0.3f; cc.slopeLimit = ccAntigo != null ? ccAntigo.slopeLimit : 45f;

        // copia os scripts do jogador (controles, vida, combate, interface, defesa), menos o efeito antigo
        foreach (MonoBehaviour mb in antigo.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb is EfeitoEspada) continue;
            Component c = novo.GetComponent(mb.GetType());
            if (c == null) c = novo.AddComponent(mb.GetType());
            if (UnityEditorInternal.ComponentUtility.CopyComponent(mb)) UnityEditorInternal.ComponentUtility.PasteComponentValues(c);
        }
        if (novo.GetComponent<EscudoAnimacao>() == null) novo.AddComponent<EscudoAnimacao>();
        PrepararBainha(novo);

        // velocidades que combinam com as animações dele, e alcance de montante
        PlayerLocomotion loco = novo.GetComponent<PlayerLocomotion>();
        if (loco != null)
        {
            SerializedObject so = new SerializedObject(loco);
            so.FindProperty("walkSpeed").floatValue = VELOCIDADE_ANDAR;
            so.FindProperty("runSpeed").floatValue = VELOCIDADE_CORRER;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        CombateJogador comb = novo.GetComponent<CombateJogador>();
        if (comb != null)
        {
            SerializedObject so = new SerializedObject(comb);
            so.FindProperty("alcance").floatValue = 2.3f;
            so.FindProperty("raio").floatValue = 1.5f;
            SerializedProperty d = so.FindProperty("danoCombo");
            if (d != null && d.arraySize >= 3) { d.GetArrayElementAtIndex(0).floatValue = 25f; d.GetArrayElementAtIndex(1).floatValue = 30f; d.GetArrayElementAtIndex(2).floatValue = 45f; }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // tudo que apontava para o cavaleiro antigo (câmera, menu inicial...) passa a apontar para o novo
        foreach (GameObject raiz in antigo.scene.GetRootGameObjects())
            foreach (MonoBehaviour mb in raiz.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.gameObject == antigo || mb.transform.IsChildOf(antigo.transform) || mb.transform.IsChildOf(novo.transform)) continue;
                SerializedObject so = new SerializedObject(mb);
                SerializedProperty it = so.GetIterator();
                bool mudou = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    if (it.objectReferenceValue == antigo) { it.objectReferenceValue = novo; mudou = true; }
                    else if (it.objectReferenceValue == antigo.transform) { it.objectReferenceValue = novo.transform; mudou = true; }
                }
                if (mudou) so.ApplyModifiedProperties();
            }
        return novo;
    }

    // ------------------------------------------------------------------
    //  5) efeito de espada com a cor do personagem
    // ------------------------------------------------------------------
    static string EfeitoDaEspada(GameObject novo, List<string> avisos)
    {
        Color destaque; string descricao;
        CorDasTexturas(out destaque, out descricao);
        float h, s, v;
        Color.RGBToHSV(destaque, out h, out s, out v);
        Color chama = Color.HSVToRGB(h, Mathf.Clamp(s, 0.45f, 0.8f), 1f) * 4.5f; chama.a = 1f;
        Color sombra = Color.HSVToRGB(h, 0.55f, 0.18f); sombra.a = 1f;
        Color brasa = Color.HSVToRGB(h, 0.6f, 1f) * 3.5f; brasa.a = 1f;

        Material mChama = PingluEfeitos.Mat("Sombrio_Corte", "rastro_energia", true, chama);
        Material mSombra = PingluEfeitos.Mat("Sombrio_Rastro", "rastro_sombra", false, new Color(sombra.r * 6f, sombra.g * 6f, sombra.b * 6f, 1f));
        Material mBrasa = PingluEfeitos.Mat("Sombrio_Faisca", "faisca", true, brasa);
        Material mFumaca = PingluEfeitos.Mat("Sombrio_Fumaca", "fumaca", false, Color.HSVToRGB(h, 0.4f, 0.9f));

        EfeitoEspada ef = novo.GetComponent<EfeitoEspada>();
        if (ef == null) ef = novo.AddComponent<EfeitoEspada>();
        ef.materialChama = mChama; ef.materialSombra = mSombra; ef.materialBrasa = mBrasa; ef.materialFumaca = mFumaca;
        ef.corDaChama = chama;
        ef.vidaDoRastro = 0.22f;                       // montante: arco mais longo e pesado
        ef.duracaoDoCorte = new[] { 0.45f, 0.45f, 0.6f };
        Vector3 b, p; float comp;
        if (PingluEfeitos.DetectarLamina(novo, out b, out p, out comp)) { ef.baseDaLamina = b; ef.pontaDaLamina = p; }
        else avisos.Add("Não achei a lâmina sozinho: confira a linha vermelha do Efeito Espada na janela Scene.");
        return descricao;
    }

    // a cor mais marcante das texturas (ignora preto, cinza e branco)
    static void CorDasTexturas(out Color cor, out string descricao)
    {
        float[] pesoHue = new float[36];
        float total = 0f;
        foreach (string parte in new[] { "tecido", "metal", "espada" })
        {
            string caminho = RAIZ + "/texturas_unity/cavaleiro_sombrio_" + parte + "_BaseColor.png";
            if (!System.IO.File.Exists(caminho)) continue;
            Texture2D t = new Texture2D(2, 2);
            if (!t.LoadImage(System.IO.File.ReadAllBytes(caminho))) continue;
            Color[] px = t.GetPixels();
            for (int i = 0; i < px.Length; i += 37)
            {
                float h, s, v;
                Color.RGBToHSV(px[i], out h, out s, out v);
                if (s < 0.3f || v < 0.18f) continue;
                float w = s * v;
                pesoHue[Mathf.Min(35, (int)(h * 36f))] += w;
                total += w;
            }
            Object.DestroyImmediate(t);
        }
        if (total < 50f)
        {
            cor = new Color(0.55f, 0.72f, 1f);
            descricao = "o personagem é quase todo preto/cinza, então usei um azul-aço frio (dá para trocar em \"Cor Da Chama\")";
            return;
        }
        int melhor = 0;
        for (int i = 1; i < 36; i++)
            if (pesoHue[(i + 35) % 36] + pesoHue[i] + pesoHue[(i + 1) % 36] > pesoHue[(melhor + 35) % 36] + pesoHue[melhor] + pesoHue[(melhor + 1) % 36]) melhor = i;
        float hue = (melhor + 0.5f) / 36f;
        cor = Color.HSVToRGB(hue, 0.75f, 1f);
        descricao = NomeDaCor(hue) + " (dá para trocar em \"Cor Da Chama\" no componente Efeito Espada)";
    }

    static string NomeDaCor(float h)
    {
        float g = h * 360f;
        if (g < 15 || g >= 345) return "vermelho";
        if (g < 45) return "laranja";
        if (g < 70) return "dourado/amarelo";
        if (g < 160) return "verde";
        if (g < 200) return "ciano";
        if (g < 255) return "azul";
        if (g < 290) return "roxo";
        return "magenta";
    }

    // ------------------------------------------------------------------
    //  6) multiplayer: prefab de rede novo e ficha do personagem
    // ------------------------------------------------------------------
    static void AtualizarMultiplayer(GameObject novo, List<string> avisos)
    {
#if PINGLU_REDE
        if (!AssetDatabase.IsValidFolder("Assets/Personagens/Jogadores")) AssetDatabase.CreateFolder("Assets/Personagens", "Jogadores");
        GameObject copia = Object.Instantiate(novo);
        copia.name = "Jogador_CavaleiroSombrio";
        copia.SetActive(true);
        copia.AddComponent<NetworkObject>();
        NetworkTransform nt = copia.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
        NetworkAnimator na = copia.AddComponent<NetworkAnimator>();
        na.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
        na.Animator = copia.GetComponent<Animator>();
        copia.AddComponent<JogadorRede>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copia, PREFAB_REDE);
        Object.DestroyImmediate(copia);

        NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(LISTA_REDE);
        if (lista != null)
        {
            bool tem = false;
            foreach (NetworkPrefab p in lista.PrefabList) if (p.Prefab == prefab) tem = true;
            if (!tem) lista.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(lista);
        }
        else avisos.Add("Não achei a lista de prefabs de rede; rode o Pinglu > 15 depois.");

        FichaPersonagem ficha = AssetDatabase.LoadAssetAtPath<FichaPersonagem>(FICHA);
        if (ficha != null)
        {
            ficha.nome = "Cavaleiro Sombrio";
            ficha.descricao = "Guerreiro encapuzado com coroa e armadura pesada, que luta com um montante de duas mãos.";
            ficha.habilidades = new[] { "Combo de 3 golpes de montante (clique esquerdo)", "Guarda com a espada (segurar o botão direito)", "Rolamento sem levar dano (Ctrl)" };
            ficha.prefabJogador = prefab;
            ficha.disponivel = true;
            EditorUtility.SetDirty(ficha);
        }
        else avisos.Add("Não achei a ficha do personagem 1; rode o Pinglu > 15 depois.");
        avisos.Add("Jogador do multiplayer atualizado (Jogador_CavaleiroSombrio).");
#else
        avisos.Add("O multiplayer não está instalado neste projeto; pulei essa parte.");
#endif
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
