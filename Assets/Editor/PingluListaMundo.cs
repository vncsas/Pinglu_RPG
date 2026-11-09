#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PingluListaMundo
{
    public static void RodarEFechar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        GameObject m = GameObject.Find("Mundo_Gerado");
        string r = "[MUNDO]";
        foreach (Transform t in m.transform)
        {
            Bounds b = new Bounds(); bool tem = false;
            foreach (Renderer rd in t.GetComponentsInChildren<Renderer>()) { if (rd is ParticleSystemRenderer) continue; if (!tem) { b = rd.bounds; tem = true; } else b.Encapsulate(rd.bounds); }
            r += "\n  " + t.name + " ativo=" + t.gameObject.activeSelf + " filhos=" + t.childCount + (tem ? " centro=" + b.center + " extents=" + b.extents : "");
        }
        Debug.Log(r);
        EditorApplication.Exit(0);
    }
}
#endif
