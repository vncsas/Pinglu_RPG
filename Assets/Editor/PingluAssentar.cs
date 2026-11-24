// =====================================================================
//  PingluAssentar.cs  -  assenta no chão os objetos do mapa que ficaram voando (ou enterrados)
// ---------------------------------------------------------------------
//  Os morros, colinas e lagoas mudam a altura do terreno depois que as coisas foram colocadas.
//  Este menu confere cada objeto gerado (casas, barris, tendas, pedras, árvores, animais...):
//  se o ponto mais baixo dele está no ar, desce até encostar no chão; se está enterrado
//  fundo demais, sobe. Estruturas enormes feitas com fundação (castelo, cemitério, muralhas)
//  não são mexidas. As árvores do terreno também são recolocadas no chão.
//
//  COMO USAR:  Pinglu > 47. Assentar objetos no chão   (Play desligado; depois Ctrl+S)
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluAssentar
{
    static readonly string[] RAIZES = { "Mundo_Gerado", "Vila_Gerada", "Biomas_Gerados", "Vegetacao_Gerada" };
    static readonly string[] IGNORAR = { "luz", "fogo", "fumaca", "vagalume", "portal", "colisor", "castelo", "muralha", "fortaleza", "cemiterio", "estrada", "bando", "[oculto", "limite da floresta", "ponto de surgimento", "estrutura", "entrada" };

    [MenuItem("Pinglu/47. Assentar objetos no chão")]
    public static void Menu() { Executar(true); }

    static float Chao(Terrain t, float x, float z) { return t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y; }

    static bool Ignorar(string nome)
    {
        string n = nome.ToLower();
        foreach (string s in IGNORAR) if (n.Contains(s)) return true;
        return false;
    }

    static bool Medir(Transform t, out Bounds b)
    {
        b = new Bounds(t.position, Vector3.zero); bool achou = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!achou) { b = r.bounds; achou = true; } else b.Encapsulate(r.bounds);
        }
        return achou;
    }

    public static string Executar(bool dialogo)
    {
        if (EditorApplication.isPlaying) { if (dialogo) EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return ""; }
        Terrain ter = Terrain.activeTerrain;
        if (ter == null) { if (dialogo) EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain.", "OK"); return ""; }
        int verificados = 0, descidos = 0, subidos = 0;
        float maiorDesce = 0f, maiorSobe = 0f;
        Dictionary<string, int> porNome = new Dictionary<string, int>();
        List<string> exemplos = new List<string>();
        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        foreach (string nr in RAIZES)
        {
            GameObject raiz = GameObject.Find(nr);
            if (raiz == null) continue;
            Pilha(raiz.transform, ter, ref verificados, ref descidos, ref subidos, ref maiorDesce, ref maiorSobe, porNome, exemplos);
        }
        // árvores do terreno: recoloca todas no chão
        TerrainData td = ter.terrainData;
        Undo.RegisterCompleteObjectUndo(td, "Assentar");
        td.SetTreeInstances(td.treeInstances, true);
        ter.Flush();
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(ter.gameObject.scene);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(verificados + " objetos conferidos: " + descidos + " desceram (até " + maiorDesce.ToString("0.0") + " m) e " + subidos + " subiram (até " + maiorSobe.ToString("0.0") + " m).");
        sb.AppendLine("As árvores do terreno foram recolocadas no chão.");
        if (porNome.Count > 0)
        {
            sb.Append("Mais mexidos: ");
            List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>(porNome);
            l.Sort((a, c) => c.Value.CompareTo(a.Value));
            for (int i = 0; i < Mathf.Min(8, l.Count); i++) sb.Append(l[i].Key + " x" + l[i].Value + (i < Mathf.Min(8, l.Count) - 1 ? ", " : ""));
        }
        string txt = sb.ToString();
        Debug.Log("[Assentar] " + txt.Replace("\n", " | "));
        foreach (string e in exemplos) Debug.Log("[Assentar] " + e);
        if (dialogo) EditorUtility.DisplayDialog("Pinglu", txt + "\n\nSalve com Ctrl + S.", "OK");
        return txt;
    }

    // percorre as pastas; cada objeto de até ~45 m é tratado como uma peça só
    static void Pilha(Transform pai, Terrain ter, ref int verificados, ref int descidos, ref int subidos, ref float maiorDesce, ref float maiorSobe, Dictionary<string, int> porNome, List<string> exemplos)
    {
        for (int i = 0; i < pai.childCount; i++)
        {
            Transform c = pai.GetChild(i);
            if (!c.gameObject.activeInHierarchy || Ignorar(c.name)) continue;
            Bounds b;
            if (!Medir(c, out b)) continue;
            if (Mathf.Max(b.size.x, b.size.z) > 45f)
            {
                if (c.childCount > 0) Pilha(c, ter, ref verificados, ref descidos, ref subidos, ref maiorDesce, ref maiorSobe, porNome, exemplos);
                continue;
            }
            if (c.GetComponentInChildren<LODGroup>() == null && c.GetComponentInChildren<Renderer>() == null) continue;
            verificados++;
            // chão mais alto e mais baixo embaixo da peça
            float gmin = float.MaxValue, gmax = float.MinValue;
            for (int ix = 0; ix <= 4; ix++)
                for (int iz = 0; iz <= 4; iz++)
                {
                    float x = Mathf.Lerp(b.min.x, b.max.x, ix / 4f), z = Mathf.Lerp(b.min.z, b.max.z, iz / 4f);
                    float g = Chao(ter, x, z);
                    gmin = Mathf.Min(gmin, g); gmax = Mathf.Max(gmax, g);
                }
            float fundo = b.min.y;
            float alvo = Mathf.Lerp(gmax, gmin, 0.35f) - 0.1f;
            float delta = 0f;
            bool pequena = Mathf.Max(b.size.x, b.size.z) < 6f && b.size.y < 8f;
            if (fundo > gmax + (pequena ? 0.12f : 0.3f)) delta = alvo - fundo;                // voando
            else if (fundo < gmin - 1.5f && b.size.y < 25f) delta = (gmin - 0.2f) - fundo;   // enterrado fundo demais
            else if (Mathf.Max(b.size.x, b.size.z) < 6f && fundo < gmin - 0.3f && b.size.y < 8f) delta = (gmin - 0.05f) - fundo;   // peça pequena (boneco, poste, barril) meio afundada
            if (Mathf.Abs(delta) < 0.05f) continue;
            Undo.RecordObject(c, "Assentar objeto");
            c.position += Vector3.up * delta;
            if (delta < 0f) { descidos++; maiorDesce = Mathf.Max(maiorDesce, -delta); } else { subidos++; maiorSobe = Mathf.Max(maiorSobe, delta); }
            string chave = System.Text.RegularExpressions.Regex.Replace(c.name, @"\s*\(\d+\)$|\s*\d+$", "");
            int v; porNome.TryGetValue(chave, out v); porNome[chave] = v + 1;
            if (exemplos.Count < 12) exemplos.Add(c.name + " em (" + Mathf.RoundToInt(c.position.x) + ", " + Mathf.RoundToInt(c.position.z) + ") " + (delta < 0f ? "desceu " : "subiu ") + Mathf.Abs(delta).ToString("0.0") + " m");
        }
    }
}
#endif
