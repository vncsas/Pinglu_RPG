// =====================================================================
//  PingluCodigosDeRede.cs  -  Conserta os códigos de identificação de rede
// ---------------------------------------------------------------------
//  Cada objeto de rede (personagens, esqueleto) tem um código que precisa
//  ser IGUAL em todos os PCs. Se ele ficar salvo como 0, cada PC calcula
//  o seu e o host recusa a conexão ("disconnected by server").
//  Este menu calcula e grava os códigos nos arquivos.
//
//  COMO USAR:  Pinglu > 27. Consertar códigos de rede
//  (rode em UM PC só, salve e mande para o GitHub)
// =====================================================================
#if UNITY_EDITOR && PINGLU_REDE
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public static class PingluCodigosDeRede
{
    [MenuItem("Pinglu/27. Consertar códigos de rede")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        string relatorio = Consertar();
        EditorUtility.DisplayDialog("Pinglu", "Códigos de rede gravados:\n\n" + relatorio +
            "\nAgora mande para o GitHub (git add . / commit / push) e faça git pull no outro PC.", "OK");
    }

    // chamado também pelo menu 26
    public static string Consertar()
    {
        MethodInfo validar = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        HashSet<GameObject> prefabs = new HashSet<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:NetworkPrefabsList"))
        {
            NetworkPrefabsList l = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(guid));
            if (l == null) continue;
            foreach (NetworkPrefab p in l.PrefabList) if (p != null && p.Prefab != null) prefabs.Add(p.Prefab);
        }
        string rel = "";
        foreach (GameObject go in prefabs)
        {
            NetworkObject no = go.GetComponent<NetworkObject>();
            if (no == null) continue;
            uint antes = no.PrefabIdHash;
            if (validar != null) validar.Invoke(no, null);
            EditorUtility.SetDirty(no);
            EditorUtility.SetDirty(go);
            rel += "- " + go.name + ": " + no.PrefabIdHash + (antes != no.PrefabIdHash ? "  (era " + antes + ", consertado)" : "") + "\n";
        }
        AssetDatabase.SaveAssets();
        return rel;
    }
}
#endif
