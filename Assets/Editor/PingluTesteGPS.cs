#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PingluTesteGPS
{
    const string SK = "PingluTesteGPSAtivo", SK_SAIR = "PingluTesteGPSSair";
    static PingluTesteGPS() { EditorApplication.playModeStateChanged += AoMudar; }

    [MenuItem("Pinglu/41. Testar GPS")]
    public static void Testar() { Iniciar(false); }
    public static void TestarEFechar() { Iniciar(true); }

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
            GameObject g = new GameObject("Teste do GPS");
            g.AddComponent<TesteDoGPS>().AoTerminar = () => { EditorApplication.delayCall += () => EditorApplication.isPlaying = false; };
        }
        else if (e == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(SK_SAIR, false))
        {
            SessionState.SetBool(SK_SAIR, false);
            EditorApplication.Exit(0);
        }
    }
}
#endif
