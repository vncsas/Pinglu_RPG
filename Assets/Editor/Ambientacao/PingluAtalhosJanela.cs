#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PingluAtalhosJanela : EditorWindow
{
    [MenuItem("Pinglu/Atalhos do mapa")]
    static void Abrir() { GetWindow<PingluAtalhosJanela>("Atalhos do mapa"); }

    void OnGUI()
    {
        GUILayout.Label(Application.isPlaying ? "Play: leva o jogador (ou use as teclas 1-9 e 0)" : "Fora do Play: move a Scene view", EditorStyles.boldLabel);
        foreach (var l in PingluAtalhosMapa.Padrao())
            if (GUILayout.Button(l.nome))
            {
                if (Application.isPlaying) PingluAtalhosMapa.IrPara(l.xz);
                else { var sv = SceneView.lastActiveSceneView; if (sv != null) { var p = PingluAtalhosMapa.Ponto(l.xz); sv.LookAt(p + Vector3.up * 3f, Quaternion.Euler(20f, 40f, 0f), 60f); } }
            }
    }
}
#endif
