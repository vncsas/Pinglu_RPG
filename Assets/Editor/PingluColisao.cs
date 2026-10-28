// =====================================================================
//  PingluColisao.cs  -  Faz o jogador colidir com as pedras
// ---------------------------------------------------------------------
//  1) Pedras pintadas no Terrain com a ferramenta de árvores não têm
//     colisão (o Terrain ignora colisores de malha). Este script troca
//     cada uma por um objeto de verdade, no mesmo lugar e tamanho.
//  2) Confere todas as pedras da cena e liga/conserta o colisor delas.
//
//  COMO USAR:  Pinglu > 8. Colisão nas pedras
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluColisao
{
    const string NOME_PASTA = "Pedras do Terreno (com colisao)";
    static readonly string[] PALAVRAS = { "rock", "pebble", "stone", "pedra", "boulder", "cliff" };

    [MenuItem("Pinglu/8. Colisão nas pedras")]
    public static void Corrigir()
    {
        Terrain terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain na cena. Abra a SampleScene.", "OK"); return; }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();

        int convertidas = ConverterPedrasDoTerreno(terreno);
        int consertadas = ConsertarColisores(terreno.gameObject.scene);

        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        EditorUtility.DisplayDialog("Pinglu",
            "Pronto!\n\n- Pedras do terreno transformadas em objetos com colisão: " + convertidas +
            "\n- Pedras soltas com colisor ligado ou consertado: " + consertadas +
            "\n\nDê Play e tente atravessar uma pedra. Se gostar, salve com Ctrl + S.\n(Ctrl + Z desfaz tudo.)", "OK");
    }

    static bool EhPedra(string nome)
    {
        string n = nome.ToLower();
        foreach (string p in PALAVRAS) if (n.Contains(p)) return true;
        return false;
    }

    // o Terrain só usa colisores simples (cápsula, caixa, esfera) nas "árvores"
    static bool TerrenoColide(GameObject prefab)
    {
        return prefab.GetComponent<CapsuleCollider>() != null || prefab.GetComponent<BoxCollider>() != null || prefab.GetComponent<SphereCollider>() != null;
    }

    static int ConverterPedrasDoTerreno(Terrain terreno)
    {
        TerrainData td = terreno.terrainData;
        TreePrototype[] protos = td.treePrototypes;
        bool[] converter = new bool[protos.Length];
        bool algum = false;
        for (int i = 0; i < protos.Length; i++)
        {
            GameObject p = protos[i].prefab;
            converter[i] = p != null && EhPedra(p.name) && !TerrenoColide(p);
            algum |= converter[i];
        }
        if (!algum) return 0;

        Undo.RegisterCompleteObjectUndo(td, "Colisão nas pedras");
        Transform pasta = null;
        GameObject existente = GameObject.Find(NOME_PASTA);
        if (existente != null) pasta = existente.transform;
        else
        {
            GameObject g = new GameObject(NOME_PASTA);
            Undo.RegisterCreatedObjectUndo(g, "Colisão nas pedras");
            pasta = g.transform;
        }

        Vector3 origem = terreno.transform.position, tam = td.size;
        List<TreeInstance> manter = new List<TreeInstance>();
        int n = 0;
        foreach (TreeInstance ti in td.treeInstances)
        {
            if (ti.prototypeIndex < 0 || ti.prototypeIndex >= protos.Length || !converter[ti.prototypeIndex]) { manter.Add(ti); continue; }
            GameObject prefab = protos[ti.prototypeIndex].prefab;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, terreno.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "Colisão nas pedras");
            go.transform.SetParent(pasta, true);
            Vector3 pos = new Vector3(origem.x + ti.position.x * tam.x, origem.y + ti.position.y * tam.y, origem.z + ti.position.z * tam.z);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, ti.rotation * Mathf.Rad2Deg, 0f) * prefab.transform.rotation;
            Vector3 s = prefab.transform.localScale;
            go.transform.localScale = new Vector3(s.x * ti.widthScale, s.y * ti.heightScale, s.z * ti.widthScale);
            go.isStatic = true;
            GarantirColisor(go);
            n++;
        }
        td.SetTreeInstances(manter.ToArray(), true);
        terreno.Flush();
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        return n;
    }

    static int ConsertarColisores(UnityEngine.SceneManagement.Scene cena)
    {
        int n = 0;
        foreach (GameObject raiz in cena.GetRootGameObjects())
            foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                // olha o nome do objeto e o nome do prefab de origem
                GameObject go = mf.gameObject;
                GameObject origem = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                string nome = go.name + " " + (origem != null ? origem.name : "") + " " + (mf.sharedMesh != null ? mf.sharedMesh.name : "");
                if (!EhPedra(nome)) continue;
                if (GarantirColisor(go)) n++;
            }
        return n;
    }

    // devolve true se precisou mexer
    static bool GarantirColisor(GameObject go)
    {
        bool mexeu = false;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Collider[] cols = mf.GetComponents<Collider>();
            MeshCollider mc = mf.GetComponent<MeshCollider>();
            bool temColisorBom = false;
            foreach (Collider c in cols)
            {
                if (c is MeshCollider) continue;
                if (!c.isTrigger && c.enabled) temColisorBom = true;
            }
            if (temColisorBom) continue;

            if (mc == null)
            {
                mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                mexeu = true;
                continue;
            }
            if (mc.sharedMesh == null || !mc.enabled || mc.isTrigger)
            {
                Undo.RecordObject(mc, "Colisão nas pedras");
                if (mc.sharedMesh == null) mc.sharedMesh = mf.sharedMesh;
                mc.enabled = true;
                mc.isTrigger = false;
                mexeu = true;
            }
        }
        return mexeu;
    }
}
#endif
