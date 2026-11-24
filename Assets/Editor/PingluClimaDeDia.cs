// =====================================================================
//  PingluClimaDeDia.cs  -  instala a luz e a neblina de DIA nos campos
//  Pinglu > 52. Clima de dia nos campos      (depois Ctrl+S)
//  Pinglu > 53. Remover clima de dia
//  Depois de instalar, clique no objeto "Clima de Dia" e ajuste sol, céu, neblina e imagem no Inspector.
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluClimaDeDia
{
    [MenuItem("Pinglu/52. Clima de dia nos campos")]
    public static void Instalar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (Object.FindFirstObjectByType<ClimaSombrio>() == null)
        { EditorUtility.DisplayDialog("Pinglu", "Não achei o \"Clima Sombrio\" na cena. Rode antes o Pinglu > 12.", "OK"); return; }
        ClimaDeDia c = Object.FindFirstObjectByType<ClimaDeDia>();
        if (c != null) { Selection.activeGameObject = c.gameObject; EditorUtility.DisplayDialog("Pinglu", "O clima de dia já está instalado (objeto \"Clima de Dia\"). Ajuste os valores no Inspector.", "OK"); return; }
        GameObject go = new GameObject("Clima de Dia");
        Undo.RegisterCreatedObjectUndo(go, "Clima de dia");
        go.AddComponent<ClimaDeDia>();
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorUtility.DisplayDialog("Pinglu", "Clima de dia instalado!\n\nÉ DIA nos campos; a Floresta Sombria, o cemitério, o pantano, as terras queimadas e o reino destruído continuam sombrios (a mudança é gradual).\n\nÉ aplicado durante o Play. Aperte Play e ande pelos campos. Salve com Ctrl + S.", "OK");
    }

    [MenuItem("Pinglu/53. Remover clima de dia")]
    public static void Remover()
    {
        ClimaDeDia c = Object.FindFirstObjectByType<ClimaDeDia>();
        if (c == null) { EditorUtility.DisplayDialog("Pinglu", "Não há clima de dia na cena.", "OK"); return; }
        Undo.DestroyObjectImmediate(c.gameObject);
        EditorUtility.DisplayDialog("Pinglu", "Clima de dia removido. Salve com Ctrl + S.", "OK");
    }
}
#endif
