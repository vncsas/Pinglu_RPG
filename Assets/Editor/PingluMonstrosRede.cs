// =====================================================================
//  PingluMonstrosRede.cs  -  Monstros no multiplayer
// ---------------------------------------------------------------------
//  Prepara o esqueleto para aparecer para todos os jogadores: o host
//  comanda os monstros e os outros PCs veem o mesmo movimento, animações
//  e vida.
//
//  COMO USAR:  Pinglu > 26. Monstros no multiplayer  (e salve a cena)
// =====================================================================
#if UNITY_EDITOR && PINGLU_REDE
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluMonstrosRede
{
    const string LISTA = "Assets/Personagens/Jogadores/Prefabs de Rede.asset";

    [MenuItem("Pinglu/26. Monstros no multiplayer")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        GerenciadorDeOndas g = Object.FindFirstObjectByType<GerenciadorDeOndas>();
        if (g == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei a horda do cemitério na cena. Rode antes o Pinglu > 11.", "OK"); return; }
        SerializedObject so = new SerializedObject(g);
        GameObject prefab = so.FindProperty("prefabInimigo").objectReferenceValue as GameObject;
        string caminho = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
        if (string.IsNullOrEmpty(caminho)) { EditorUtility.DisplayDialog("Pinglu", "A horda não tem o molde do esqueleto. Rode antes o Pinglu > 11.", "OK"); return; }

        GameObject c = PrefabUtility.LoadPrefabContents(caminho);
        if (c.GetComponent<NetworkObject>() == null) c.AddComponent<NetworkObject>();
        NetworkTransform nt = c.GetComponent<NetworkTransform>();
        if (nt == null) nt = c.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Server; // quem move é o host
        nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
        NetworkAnimator na = c.GetComponent<NetworkAnimator>();
        if (na == null) na = c.AddComponent<NetworkAnimator>();
        na.AuthorityMode = NetworkAnimator.AuthorityModes.Server;
        na.Animator = c.GetComponentInChildren<Animator>();
        if (c.GetComponent<InimigoRede>() == null) c.AddComponent<InimigoRede>();
        GameObject salvo = PrefabUtility.SaveAsPrefabAsset(c, caminho);
        PrefabUtility.UnloadPrefabContents(c);

        NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(LISTA);
        bool ok = lista != null;
        if (ok)
        {
            bool tem = false;
            foreach (NetworkPrefab p in lista.PrefabList) if (p.Prefab == salvo) tem = true;
            if (!tem) lista.Add(new NetworkPrefab { Prefab = salvo });
            EditorUtility.SetDirty(lista);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        EditorUtility.DisplayDialog("Pinglu", ok
            ? "Esqueletos prontos para o multiplayer!\n\n- O host comanda as hordas; todos veem os mesmos monstros\n- Qualquer jogador perto do cemitério começa a horda (ou F2)\n- Morreu? Aperte R para renascer\n\nSalve (Ctrl + S) e mande para o GitHub: os dois PCs precisam desta versão."
            : "Esqueleto preparado, mas não achei a lista de prefabs de rede. Rode o Pinglu > 15 e depois este menu de novo.", "OK");
    }
}
#endif
