#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 84. Dragão final (luta de chefe)
//  Liga a cinemática do vilão e a luta contra Drakthar: cria o objeto "Encontro do Dragao" com o modelo do dragão do lobby,
//  o prefab do Ladrão do castelo e a montanha que racha (o ponto mais alto do mapa).
//  Pré-requisito: o dragão do lobby já configurado (Pinglu > 30). Dá para rodar de novo (só atualiza).
// ==========================================================================================
public static class PingluDragaoFinal
{
    [MenuItem("Pinglu/84. Dragão final (luta de chefe)")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        DragaoDistante d = Object.FindObjectsByType<DragaoDistante>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.modelo != null);
        if (d == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o dragão do lobby com o modelo configurado.\nRode antes o Pinglu > 30 (Dragão modelo) e depois este de novo.", "OK"); return; }

        // a montanha que racha: o ponto mais alto do relevo (média de uma área, para pegar a montanha e não uma pedra)
        Vector3 pico = new Vector3(830f, 0f, 130f); string origem = "padrão";
        Terrain t = Terrain.activeTerrain;
        if (t != null)
        {
            TerrainData td = t.terrainData; Vector3 o = t.transform.position, s = td.size; float melhor = -1f;
            for (float x = 80f; x < s.x - 80f; x += 20f) for (float z = 80f; z < s.z - 80f; z += 20f)
            {
                float soma = 0f; int n = 0;
                for (float dx = -30f; dx <= 30f; dx += 15f) for (float dz = -30f; dz <= 30f; dz += 15f) { soma += t.SampleHeight(new Vector3(o.x + x + dx, 0f, o.z + z + dz)); n++; }
                float m = soma / n; if (m > melhor) { melhor = m; pico = new Vector3(o.x + x, 0f, o.z + z); }
            }
            pico.y = t.SampleHeight(pico) + o.y; origem = "ponto mais alto do mapa (" + Mathf.RoundToInt(pico.y) + " m)";
        }

        GameObject go = GameObject.Find("Encontro do Dragao");
        if (go == null) { go = new GameObject("Encontro do Dragao"); Undo.RegisterCreatedObjectUndo(go, "Encontro do Dragao"); }
        EncontroDoDragao e = go.GetComponent<EncontroDoDragao>(); if (e == null) e = go.AddComponent<EncontroDoDragao>();
        Undo.RecordObject(e, "Encontro do Dragao");
        e.modelo = d.modelo; e.animVoo = d.animVoo; e.animPlanar = d.animPlanar; e.animSopro = d.animSopro;
        e.materialParticula = d.materialParticula != null ? d.materialParticula : AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        EncontroDoCastelo castelo = Object.FindFirstObjectByType<EncontroDoCastelo>(FindObjectsInactive.Include);
        string ladrao = "(não achei o Encontro do Castelo: o Ladrão não aparece na cena)";
        if (castelo != null && castelo.prefabLadrao != null) { e.prefabLadrao = castelo.prefabLadrao; ladrao = "prefab do Ladrão do castelo"; }
        e.posicaoMontanha = pico;
        e.raioArena = 75f;
        go.transform.position = pico;
        EditorUtility.SetDirty(e);

        // tira o marcador antigo "Covil do Dragão" (agora a batalha é onde o grupo estiver)
        MiniMapa mm = Object.FindFirstObjectByType<MiniMapa>();
        if (mm != null) { MiniMapa.Marcador m = mm.marcadores.FirstOrDefault(x => x.nome == "Covil do Dragão"); if (m != null) { Undo.RecordObject(mm, "Covil do Dragão"); mm.marcadores.Remove(m); EditorUtility.SetDirty(mm); } }
        EditorSceneManager.MarkSceneDirty(go.scene); EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("Pinglu", "Luta e cinemática do dragão ligadas!\n\n• Montanha que racha: " + origem + " em (" + Mathf.RoundToInt(pico.x) + ", " + Mathf.RoundToInt(pico.z) + ")\n   (se não for a montanha certa, mude \"Posicao Montanha\" no objeto \"Encontro do Dragao\")\n• Ladrão: " + ladrao + "\n• Modelo e animações: do dragão do lobby\n\nPara testar: dê Play, escolha o personagem e aperte F6: toca a cinemática do vilão onde você estiver.\nA cena foi salva.", "OK");
    }
}
#endif
