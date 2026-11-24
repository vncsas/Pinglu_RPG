#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluMapaDeArvores
{
    public static void ExportarEFechar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        StringBuilder sb = new StringBuilder();
        Terrain t = Terrain.activeTerrain; TerrainData td = t.terrainData; Vector3 o = t.transform.position, s = td.size;
        foreach (TreeInstance ti in td.treeInstances)
        {
            string nome = td.treePrototypes[ti.prototypeIndex].prefab != null ? td.treePrototypes[ti.prototypeIndex].prefab.name : "?";
            sb.AppendLine((nome.StartsWith("ArvoreSinistra") ? "N" : "O") + "," + (o.x + ti.position.x * s.x).ToString("0.0") + "," + (o.z + ti.position.z * s.z).ToString("0.0"));
        }
        foreach (Transform x in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (x.name == "Arvore Sinistra") sb.AppendLine("P," + x.position.x.ToString("0.0") + "," + x.position.z.ToString("0.0"));
        MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
        foreach (MiniMapa.Marcador m in mm.marcadores) sb.AppendLine("M," + m.posicao.x.ToString("0.0") + "," + m.posicao.z.ToString("0.0") + "," + m.nome);
        RedeDeEstradas rede = Object.FindAnyObjectByType<RedeDeEstradas>();
        if (rede != null) foreach (RedeDeEstradas.Estrada e in rede.estradas) foreach (Vector3 p in e.pontos) sb.AppendLine("R," + p.x.ToString("0.0") + "," + p.z.ToString("0.0"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Recordings/arvores.csv"), sb.ToString());
        EditorApplication.Exit(0);
    }
}
#endif
