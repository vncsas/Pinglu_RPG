#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluAuditoria
{
    public static void RodarEFechar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        StringBuilder sb = new StringBuilder("[AUDITORIA]\n");
        long total = 0;
        // por grupo de topo e por subpasta
        Dictionary<string, long> tris = new Dictionary<string, long>();
        Dictionary<string, int> rends = new Dictionary<string, int>();
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null) continue;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled) continue;
            long t = 0; for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) t += mf.sharedMesh.GetIndexCount(s) / 3;
            string chave = Caminho(mf.transform, 2);
            tris[chave] = (tris.ContainsKey(chave) ? tris[chave] : 0) + t;
            rends[chave] = (rends.ContainsKey(chave) ? rends[chave] : 0) + 1;
            total += t;
        }
        sb.AppendLine("Triangulos de malhas na cena (todos os objetos): " + total / 1000 + "k");
        foreach (var kv in tris.OrderByDescending(k => k.Value).Take(28)) sb.AppendLine("  " + (kv.Value / 1000) + "k tris, " + rends[kv.Key] + " objetos: " + kv.Key);

        // terreno: árvores e detalhes
        Terrain terr = Terrain.activeTerrain;
        if (terr != null)
        {
            TerrainData td = terr.terrainData;
            sb.AppendLine("Terreno: tamanho " + td.size + ", arvores(instancias)=" + td.treeInstanceCount + ", prototipos de arvore=" + td.treePrototypes.Length + ", prototipos de grama/detalhe=" + td.detailPrototypes.Length + ", detailRes=" + td.detailResolution + ", heightmapRes=" + td.heightmapResolution);
            int[] cont = new int[td.treePrototypes.Length];
            foreach (TreeInstance ti in td.treeInstances) cont[ti.prototypeIndex]++;
            for (int i = 0; i < td.treePrototypes.Length; i++)
            {
                GameObject p = td.treePrototypes[i].prefab; long tt = 0; int lods = 0;
                if (p != null) foreach (MeshFilter mf in p.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tt += mf.sharedMesh.GetIndexCount(s) / 3;
                if (p != null && p.GetComponent<LODGroup>() != null) lods = p.GetComponent<LODGroup>().lodCount;
                sb.AppendLine("  arvore proto " + i + ": " + (p != null ? p.name : "null") + " x" + cont[i] + " (~" + tt + " tris somando todos os LODs, lods=" + lods + ")");
            }
            for (int i = 0; i < td.detailPrototypes.Length; i++)
            {
                DetailPrototype d = td.detailPrototypes[i];
                long tt = 0; if (d.prototype != null) foreach (MeshFilter mf in d.prototype.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tt += mf.sharedMesh.GetIndexCount(s) / 3;
                sb.AppendLine("  detalhe " + i + ": " + (d.prototype != null ? d.prototype.name : "textura") + " modo=" + d.renderMode + " usaMalha=" + d.usePrototypeMesh + " (" + tt + " tris)");
            }
        }
        // LODGroups e malhas mais pesadas
        var pesadas = new List<KeyValuePair<string, long>>();
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null) continue;
            long t = 0; for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) t += mf.sharedMesh.GetIndexCount(s) / 3;
            if (t > 40000) pesadas.Add(new KeyValuePair<string, long>(Caminho(mf.transform, 4) + " [" + mf.sharedMesh.name + "]", t));
        }
        sb.AppendLine("Malhas individuais com mais de 40k triangulos: " + pesadas.Count);
        foreach (var kv in pesadas.OrderByDescending(k => k.Value).Take(15)) sb.AppendLine("  " + (kv.Value / 1000) + "k  " + kv.Key);
        sb.AppendLine("LODGroups: " + Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length);
        // dentro do cemitério: por nome de malha
        Dictionary<string, long> porMalha = new Dictionary<string, long>(); Dictionary<string, int> qtd = new Dictionary<string, int>(); Dictionary<string, long> uni = new Dictionary<string, long>();
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null || !Caminho(mf.transform, 2).StartsWith("Mundo_Gerado/Cemiterio")) continue;
            long t = 0; for (int s2 = 0; s2 < mf.sharedMesh.subMeshCount; s2++) t += mf.sharedMesh.GetIndexCount(s2) / 3;
            string n = mf.sharedMesh.name; if (n.StartsWith("Arvore Sinistra")) n = "Arvore Sinistra (procedural)";
            porMalha[n] = (porMalha.ContainsKey(n) ? porMalha[n] : 0) + t; qtd[n] = (qtd.ContainsKey(n) ? qtd[n] : 0) + 1; uni[n] = t;
        }
        sb.AppendLine("Cemiterio por malha:");
        foreach (var kv in porMalha.OrderByDescending(k => k.Value).Take(22)) sb.AppendLine("  " + (kv.Value / 1000) + "k total = " + qtd[kv.Key] + " x ~" + (kv.Value / qtd[kv.Key] / 1000) + "k  " + kv.Key);
        Debug.Log(sb.ToString());
        EditorApplication.Exit(0);
    }

    static string Caminho(Transform t, int niveis)
    {
        List<string> n = new List<string>();
        for (Transform x = t; x != null; x = x.parent) n.Add(x.name);
        n.Reverse();
        return string.Join("/", n.Take(niveis));
    }
}
#endif
