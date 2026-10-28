// =====================================================================
//  PingluEscudo.cs  -  Defesa com o escudo na frente, andando
// ---------------------------------------------------------------------
//  Cria uma camada "Escudo" no Animator do cavaleiro que mexe só o braço
//  do escudo (pose "Blocking"), enquanto as pernas continuam andando.
//  Também tira a transição antiga de defesa, que tinha atraso.
//
//  COMO USAR:  Pinglu > 16. Defesa com escudo
//  (se rodar o Pinglu > 5 de novo, rode depois o 11 e este 16)
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluEscudo
{
    const string PACOTE = "Assets/Personagens/Sword and Shield Pack";
    const string MASCARA = "Assets/Personagens/Cavaleiro/Mascara_BracoEscudo.mask";
    const string PREFAB_REDE = "Assets/Personagens/Jogadores/Jogador_Cavaleiro.prefab";
    const string CAMADA = "Escudo";

    [MenuItem("Pinglu/16. Defesa com escudo")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }

        GameObject jogador = AcharJogador();
        if (jogador == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o cavaleiro na cena.", "OK"); return; }
        Animator anim = null;
        foreach (Animator a in jogador.GetComponentsInChildren<Animator>(true)) if (a.runtimeAnimatorController != null) { anim = a; break; }
        AnimatorController ctrl = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
        if (ctrl == null) { EditorUtility.DisplayDialog("Pinglu", "O cavaleiro não tem um Animator Controller editável. Rode antes o Pinglu > 5.", "OK"); return; }

        AnimationClip segurar = Clipe("Blocking", true);
        AnimationClip impacto = Clipe("Block", false);
        if (segurar == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei a animação \"Blocking\" em " + PACOTE + ".", "OK");
            return;
        }

        // 1) máscara: só o braço e a mão do escudo (esquerdo)
        AvatarMask mascara = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASCARA);
        if (mascara == null) { mascara = new AvatarMask(); AssetDatabase.CreateAsset(mascara, MASCARA); }
        for (AvatarMaskBodyPart p = 0; p < AvatarMaskBodyPart.LastBodyPart; p++)
            mascara.SetHumanoidBodyPartActive(p, p == AvatarMaskBodyPart.LeftArm || p == AvatarMaskBodyPart.LeftFingers || p == AvatarMaskBodyPart.LeftHandIK);
        EditorUtility.SetDirty(mascara);

        // 2) tira a defesa antiga (corpo inteiro, com atraso) da camada principal
        int removidas = 0;
        AnimatorStateMachine baseSm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState cs in baseSm.states)
        {
            List<AnimatorStateTransition> tirar = new List<AnimatorStateTransition>();
            foreach (AnimatorStateTransition t in cs.state.transitions)
                if (UsaBlock(t.conditions) && t.destinationState != null && !(t.destinationState.motion is BlendTree)) tirar.Add(t);
            foreach (AnimatorStateTransition t in tirar) { cs.state.RemoveTransition(t); removidas++; }
        }
        List<AnimatorStateTransition> tirarAny = new List<AnimatorStateTransition>();
        foreach (AnimatorStateTransition t in baseSm.anyStateTransitions) if (UsaBlock(t.conditions)) tirarAny.Add(t);
        foreach (AnimatorStateTransition t in tirarAny) { baseSm.RemoveAnyStateTransition(t); removidas++; }

        // 3) camada nova "Escudo" (recria se já existir)
        AnimatorControllerLayer[] camadas = ctrl.layers;
        for (int i = camadas.Length - 1; i >= 1; i--) if (camadas[i].name == CAMADA) ctrl.RemoveLayer(i);
        AnimatorStateMachine sm = new AnimatorStateMachine { name = CAMADA, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, AssetDatabase.GetAssetPath(ctrl));
        AnimatorState stSegurar = sm.AddState("segurar", new Vector3(300f, 0f, 0f));
        stSegurar.motion = segurar;
        sm.defaultState = stSegurar;
        if (impacto != null)
        {
            AnimatorState stImpacto = sm.AddState("impacto", new Vector3(300f, 80f, 0f));
            stImpacto.motion = impacto;
            stImpacto.speed = 1.4f;
            AnimatorStateTransition volta = stImpacto.AddTransition(stSegurar);
            volta.hasExitTime = true; volta.exitTime = 0.6f; volta.duration = 0.12f; volta.hasFixedDuration = true;
        }
        ctrl.AddLayer(new AnimatorControllerLayer
        {
            name = CAMADA,
            stateMachine = sm,
            avatarMask = mascara,
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 0f
        });
        EditorUtility.SetDirty(ctrl);

        // 4) componente que liga a camada quando defende (cena e prefab de rede)
        if (jogador.GetComponent<EscudoAnimacao>() == null) Undo.AddComponent<EscudoAnimacao>(jogador);
        bool prefabOk = false;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_REDE) != null)
        {
            GameObject conteudo = PrefabUtility.LoadPrefabContents(PREFAB_REDE);
            if (conteudo.GetComponent<EscudoAnimacao>() == null) conteudo.AddComponent<EscudoAnimacao>();
            PrefabUtility.SaveAsPrefabAsset(conteudo, PREFAB_REDE);
            PrefabUtility.UnloadPrefabContents(conteudo);
            prefabOk = true;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(jogador.scene);
        EditorUtility.DisplayDialog("Pinglu", "Defesa com escudo pronta!\n\n- Escudo na frente com a pose \"Blocking\"\n- Dá para andar defendendo\n" +
            (impacto != null ? "- Tranco no escudo ao bloquear um golpe\n" : "") +
            "- Transições antigas removidas: " + removidas + (prefabOk ? "\n- Jogador do multiplayer atualizado também" : "") +
            "\n\nSalve a cena com Ctrl + S.", "OK");
    }

    static bool UsaBlock(AnimatorCondition[] conds)
    {
        foreach (AnimatorCondition c in conds) if (c.parameter == "Block") return true;
        return false;
    }

    static AnimationClip Clipe(string arquivo, bool loop)
    {
        string caminho = PACOTE + "/" + arquivo + ".fbx";
        ModelImporter mi = AssetImporter.GetAtPath(caminho) as ModelImporter;
        if (mi == null) return null;
        ModelImporterClipAnimation[] cl = mi.clipAnimations;
        if (cl == null || cl.Length == 0) cl = mi.defaultClipAnimations;
        bool mudou = false;
        foreach (ModelImporterClipAnimation c in cl)
            if (c.loopTime != loop) { c.loopTime = loop; mudou = true; }
        if (mudou) { mi.clipAnimations = cl; mi.SaveAndReimport(); }
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(caminho))
        {
            AnimationClip c = o as AnimationClip;
            if (c != null && !c.name.StartsWith("__preview__")) return c;
        }
        return null;
    }

    static GameObject AcharJogador()
    {
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || EditorUtility.IsPersistent(p)) continue;
            string n = p.gameObject.name.ToLower();
            if (n.Contains("x bot") || n.Contains("antigo")) continue;
            return p.gameObject;
        }
        return null;
    }
}
#endif
