#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 82. Corrigir ruas da vila
//  O menu 79 levantou 5 m as malhas das ruas ("Ruas - lajes" e "Ruas - borda de terra"): elas já nascem na altura do chão
//  (a malha é feita com as coordenadas do mundo), então o certo é a posição Y delas ficar em 0. Este menu devolve as duas ao chão.
// ==========================================================================================
public static class PingluRuasDaVila
{
    [MenuItem("Pinglu/82. Corrigir ruas da vila (voltar ao chão)")]
    public static void Corrigir()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        int n = 0; string lista = "";
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Ruas - ") || t.parent == null || t.parent.name != "Caminhos da Vila") continue;
            if (Mathf.Abs(t.localPosition.y) < 0.001f) continue;
            Undo.RecordObject(t, "Corrigir ruas da vila");
            lista += "\n• " + t.name + ": " + t.localPosition.y.ToString("0.00") + " m → 0";
            Vector3 p = t.localPosition; p.y = 0f; t.localPosition = p; n++;
        }
        if (n == 0) { EditorUtility.DisplayDialog("Pinglu", "As ruas já estão na altura certa (nada a corrigir).", "OK"); return; }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Ruas da vila] corrigidas:" + lista);
        EditorUtility.DisplayDialog("Pinglu", "Pronto! Ruas devolvidas ao chão:" + lista + "\n\nA cena foi salva.", "OK");
    }
}
#endif
