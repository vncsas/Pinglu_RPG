// =====================================================================
//  PingluRede.cs  -  Prepara o multiplayer (rede local)
// ---------------------------------------------------------------------
//  1. Cria o prefab de jogador de rede a partir do cavaleiro da cena
//  2. Cria as fichas dos 4 personagens (1 pronto + 3 "em breve")
//  3. Coloca o NetworkManager e o menu inicial na cena
//
//  COMO USAR:  Pinglu > 15. Preparar multiplayer   (depois do 14)
// =====================================================================
#if UNITY_EDITOR && PINGLU_REDE
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluRede
{
    const string PASTA = "Assets/Personagens/Jogadores";
    const string PASTA_FICHAS = PASTA + "/Fichas";
    const string PREFAB = PASTA + "/Jogador_Cavaleiro.prefab";
    const string LISTA = PASTA + "/Prefabs de Rede.asset";
    const string CONTROLADOR_CAVALEIRO = "Assets/Personagens/Cavaleiro/Cavaleiro_Animator.controller";

    [MenuItem("Pinglu/15. Preparar multiplayer")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }

        // o X Bot antigo não é mais usado: tira da cena para não confundir
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || EditorUtility.IsPersistent(p)) continue;
            string n = p.gameObject.name.ToLower();
            if (n.Contains("x bot") || n.Contains("antigo")) Undo.DestroyObjectImmediate(p.gameObject);
        }

        GameObject cavaleiro = AcharJogadorDaCena();
        if (cavaleiro == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o jogador (Player Locomotion) na cena. Abra a SampleScene.", "OK"); return; }

        CriarPasta("Assets/Personagens", "Jogadores");
        CriarPasta(PASTA, "Fichas");

        // 1) prefab do jogador de rede
        // garante que o Animator tenha as animações ANTES de criar os componentes de rede
        Animator animCena = AnimatorComAnimacoes(cavaleiro);
        if (animCena == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "O personagem \"" + cavaleiro.name + "\" não tem Animator com animações.\nRode antes o Pinglu > 5 (Aplicar no cavaleiro).", "OK");
            return;
        }
        GameObject copia = Object.Instantiate(cavaleiro);
        copia.name = "Jogador_Cavaleiro";
        copia.SetActive(true);
        if (copia.GetComponent<NetworkObject>() == null) copia.AddComponent<NetworkObject>();
        NetworkTransform nt = copia.GetComponent<NetworkTransform>();
        if (nt == null) nt = copia.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner; // cada jogador manda a própria posição
        nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
        NetworkAnimator na = copia.GetComponent<NetworkAnimator>();
        if (na == null) na = copia.AddComponent<NetworkAnimator>();
        na.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
        na.Animator = AnimatorComAnimacoes(copia);
        if (copia.GetComponent<JogadorRede>() == null) copia.AddComponent<JogadorRede>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copia, PREFAB);
        Object.DestroyImmediate(copia);

        // 2) fichas dos personagens
        List<FichaPersonagem> fichas = new List<FichaPersonagem>();
        FichaPersonagem cav = Ficha("1_Cavaleiro", "Cavaleiro", true);
        cav.descricao = "Guerreiro de armadura pesada, com espada e escudo. (Personagem provisório: será trocado pelo cavaleiro definitivo.)";
        cav.habilidades = new[] { "Combo de 3 golpes de espada (clique esquerdo)", "Golpe pesado (clique direito)", "Bloqueio com escudo (Q)", "Rolamento sem levar dano (Ctrl)" };
        cav.corDestaque = new Color(0.85f, 0.6f, 0.25f);
        cav.prefabJogador = prefab;
        EditorUtility.SetDirty(cav);
        fichas.Add(cav);
        for (int i = 2; i <= 4; i++)
        {
            FichaPersonagem f = Ficha(i + "_Personagem" + i, "Personagem " + i, false);
            fichas.Add(f);
        }

        // 3) lista de prefabs de rede
        NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(LISTA);
        if (lista == null) { lista = ScriptableObject.CreateInstance<NetworkPrefabsList>(); AssetDatabase.CreateAsset(lista, LISTA); }
        bool tem = false;
        foreach (NetworkPrefab p in lista.PrefabList) if (p.Prefab == prefab) tem = true;
        if (!tem) lista.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(lista);

        // 4) NetworkManager na cena
        NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null)
        {
            GameObject g = new GameObject("NetworkManager");
            Undo.RegisterCreatedObjectUndo(g, "Multiplayer");
            nm = g.AddComponent<NetworkManager>();
        }
        UnityTransport ut = nm.GetComponent<UnityTransport>();
        if (ut == null) ut = nm.gameObject.AddComponent<UnityTransport>();
        if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
        nm.NetworkConfig.NetworkTransport = ut;
        nm.NetworkConfig.PlayerPrefab = null;
        nm.NetworkConfig.ConnectionApproval = true;
        if (!nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(lista)) nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(lista);
        EditorUtility.SetDirty(nm);

        // 5) menu inicial
        MenuRede menu = Object.FindFirstObjectByType<MenuRede>();
        if (menu == null)
        {
            GameObject g = new GameObject("Menu Inicial");
            Undo.RegisterCreatedObjectUndo(g, "Multiplayer");
            menu = g.AddComponent<MenuRede>();
        }
        SerializedObject so = new SerializedObject(menu);
        SerializedProperty lp = so.FindProperty("personagens");
        lp.arraySize = fichas.Count;
        for (int i = 0; i < fichas.Count; i++) lp.GetArrayElementAtIndex(i).objectReferenceValue = fichas[i];
        so.FindProperty("jogadorDaCena").objectReferenceValue = cavaleiro;
        so.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(cavaleiro.scene);
        EditorUtility.DisplayDialog("Pinglu", "Multiplayer preparado!\n\nSALVE A CENA (Ctrl + S).\n\n" +
            "Ao dar Play, aparece o menu inicial com:\n- Jogar sozinho\n- Criar partida (você vira o host)\n- Entrar na partida (digitando o IP do host)\n\n" +
            "As fichas dos personagens ficam em " + PASTA_FICHAS + ".", "OK");
    }

    // o cavaleiro da cena (ignora o X Bot antigo desativado e prefabs)
    static GameObject AcharJogadorDaCena()
    {
        GameObject melhor = null; int notaMelhor = -1;
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || EditorUtility.IsPersistent(p)) continue;
            string n = p.gameObject.name.ToLower();
            if (n.Contains("antigo") || n.Contains("x bot")) continue;
            int nota = 0;
            if (n.Contains("cavaleiro")) nota += 2;
            if (p.GetComponent<NetworkObject>() == null) nota += 1;
            if (nota > notaMelhor) { notaMelhor = nota; melhor = p.gameObject; }
        }
        return melhor;
    }

    // o Animator que realmente tem a lista de animações ligada
    static Animator AnimatorComAnimacoes(GameObject go)
    {
        Animator primeiro = null;
        foreach (Animator a in go.GetComponentsInChildren<Animator>(true))
        {
            if (primeiro == null) primeiro = a;
            if (a.runtimeAnimatorController != null) return a;
        }
        if (primeiro != null)
        {
            RuntimeAnimatorController c = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CONTROLADOR_CAVALEIRO);
            if (c != null) { primeiro.runtimeAnimatorController = c; return primeiro; }
        }
        return null;
    }

    static FichaPersonagem Ficha(string arquivo, string nome, bool disponivel)
    {
        string caminho = PASTA_FICHAS + "/" + arquivo + ".asset";
        FichaPersonagem f = AssetDatabase.LoadAssetAtPath<FichaPersonagem>(caminho);
        if (f == null)
        {
            f = ScriptableObject.CreateInstance<FichaPersonagem>();
            f.nome = nome;
            f.disponivel = disponivel;
            f.descricao = disponivel ? "" : "Personagem ainda em criação.";
            AssetDatabase.CreateAsset(f, caminho);
        }
        return f;
    }

    static void CriarPasta(string pai, string nome)
    {
        if (!AssetDatabase.IsValidFolder(pai + "/" + nome)) AssetDatabase.CreateFolder(pai, nome);
    }
}
#endif
