#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PingluMedidor
{
    const string SK = "PingluMedidorAtivo", SK_SAIR = "PingluMedidorSair";
    static PingluMedidor() { EditorApplication.playModeStateChanged += AoMudar; }

    [MenuItem("Pinglu/38. Medir desempenho")]
    public static void Medir() { Iniciar(false); }
    public static void MedirEFechar() { Iniciar(true); }

    static void Iniciar(bool sair)
    {
        if (EditorApplication.isPlaying) return;
        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity") EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(SK, true); SessionState.SetBool(SK_SAIR, sair);
        EditorApplication.isPlaying = true;
    }

    static void AoMudar(PlayModeStateChange e)
    {
        if (e == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SK, false))
        {
            SessionState.SetBool(SK, false);
            GameObject g = new GameObject("Medidor");
            g.AddComponent<MedidorDeDesempenho>().AoTerminar = () => { EditorApplication.delayCall += () => EditorApplication.isPlaying = false; };
        }
        else if (e == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(SK_SAIR, false))
        {
            SessionState.SetBool(SK_SAIR, false);
            EditorApplication.Exit(0);
        }
    }
}
#endif
