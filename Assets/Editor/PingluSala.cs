// =====================================================================
//  PingluSala.cs  -  Limpeza da sala de espera antiga
// ---------------------------------------------------------------------
//  A sala de espera agora funciona por mensagens e não precisa mais de
//  objeto na cena. Este menu remove o objeto antigo "Sala de Espera".
//
//  COMO USAR:  Pinglu > 25. Sala de espera (multiplayer)  e SALVE A CENA
// =====================================================================
#if UNITY_EDITOR && PINGLU_REDE
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluSala
{
    [MenuItem("Pinglu/25. Sala de espera (multiplayer)")]
    public static void Limpar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        int removidos = 0;
        foreach (SalaDeEspera s in Resources.FindObjectsOfTypeAll<SalaDeEspera>())
        {
            if (s == null || !s.gameObject.scene.IsValid() || EditorUtility.IsPersistent(s)) continue;
            EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
            Undo.DestroyObjectImmediate(s.gameObject);
            removidos++;
        }
        EditorUtility.DisplayDialog("Pinglu", removidos > 0
            ? "Objeto antigo da sala de espera removido.\n\nA sala agora funciona sozinha, sem objeto na cena.\n\nSALVE A CENA (Ctrl + S) e mande para o GitHub."
            : "A cena já está limpa: a sala de espera funciona sozinha, sem objeto na cena.", "OK");
    }
}
#endif
