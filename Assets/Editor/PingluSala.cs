// =====================================================================
//  PingluSala.cs  -  Sala de espera do multiplayer
// ---------------------------------------------------------------------
//  Coloca na cena o objeto que guarda quem está na sala, o personagem de
//  cada um e o "começar" do host.
//
//  COMO USAR:  Pinglu > 25. Sala de espera (multiplayer)  e SALVE A CENA
// =====================================================================
#if UNITY_EDITOR && PINGLU_REDE
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluSala
{
    [MenuItem("Pinglu/25. Sala de espera (multiplayer)")]
    public static void Criar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o NetworkManager na cena. Rode antes o Pinglu > 15.", "OK"); return; }
        Undo.RecordObject(nm, "Sala de espera");
        nm.NetworkConfig.EnableSceneManagement = true; // objetos de rede da cena aparecem para todos
        EditorUtility.SetDirty(nm);

        SalaDeEspera sala = Object.FindFirstObjectByType<SalaDeEspera>();
        if (sala == null)
        {
            GameObject g = new GameObject("Sala de Espera");
            Undo.RegisterCreatedObjectUndo(g, "Sala de espera");
            g.AddComponent<NetworkObject>();
            sala = g.AddComponent<SalaDeEspera>();
        }
        EditorSceneManager.MarkSceneDirty(sala.gameObject.scene);
        Selection.activeGameObject = sala.gameObject;
        EditorUtility.DisplayDialog("Pinglu", "Sala de espera pronta!\n\n" +
            "- Criar partida: o host fica na tela de seleção com o botão COMEÇAR\n" +
            "- Entrar na partida: o outro jogador aparece na mesma tela e escolhe o personagem\n" +
            "- Todos aparecem lado a lado no palco; o jogo começa quando o host aperta COMEÇAR\n\n" +
            "SALVE A CENA (Ctrl + S) e mande para o GitHub: os dois PCs precisam da mesma cena.", "OK");
    }
}
#endif
