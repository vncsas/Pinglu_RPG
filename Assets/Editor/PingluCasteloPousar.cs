#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 81. Castelo: pousar entulho, madeira e props no chão
//  O entulho, as vigas de madeira e o ouro do castelo são malhas únicas com dezenas de pedaços soltos dentro,
//  então não dá para descer "o objeto": cada pedaço fica no ar sozinho. Este menu separa os pedaços de cada malha
//  (ilhas de triângulos ligados) e desce cada um até encostar no chão de verdade: o terreno OU o piso do pátio/salão
//  do castelo, o que estiver mais perto. Também faz o mesmo com os props soltos do castelo (faixas, martelos, etc.).
//  NÃO mexe no terreno, nas muralhas, torres e portões. Copia as malhas para Backups antes. Pode rodar de novo.
// ==========================================================================================
public static class PingluCasteloPousar
{
    static readonly string[] DETRITOS = { "Entulho", "Madeira", "Ouro" };
    static readonly string[] NAO_MEXER = { "Lantern", "Lanterna", "Luz", "Tocha", "Lustre", "Fogo", "Efeito", "Fumaca", "Cadeira", "Cutscene", "Bandeira", "Estandarte", "Corda" };
    const float CEL = 0.5f;

    static Terrain ter; static float baseY;
    static Dictionary<long, List<float>> pisos = new Dictionary<long, List<float>>();
    static long Chave(int ix, int iz) { return ((long)(ix + 100000) << 24) | (long)(iz + 100000); }
    static bool Tem(string n, string[] l) { foreach (string s in l) if (n.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }

    static void AddPiso(float x, float z, float y)
    {
        long k = Chave(Mathf.FloorToInt(x / CEL), Mathf.FloorToInt(z / CEL));
        List<float> l; if (!pisos.TryGetValue(k, out l)) { l = new List<float>(); pisos[k] = l; }
        foreach (float v in l) if (Mathf.Abs(v - y) < 0.06f) return;
        l.Add(y);
    }

    // superfícies de cima (piso, degraus, topos de muro) das peças estruturais do castelo
    static void MontarPisos(Transform estrutura)
    {
        pisos.Clear();
        foreach (MeshFilter mf in estrutura.GetComponentsInChildren<MeshFilter>(false))
        {
            if (mf.sharedMesh == null || Tem(mf.transform.name, DETRITOS) || Tem(mf.transform.parent != null ? mf.transform.parent.name : "", DETRITOS)) continue;
            Mesh m = mf.sharedMesh; Matrix4x4 M = mf.transform.localToWorldMatrix;
            Vector3[] vs = m.vertices; var w = new Vector3[vs.Length]; for (int i = 0; i < vs.Length; i++) w[i] = M.MultiplyPoint3x4(vs[i]);
            int[] tr = m.triangles;
            for (int i = 0; i + 2 < tr.Length; i += 3)
            {
                Vector3 a = w[tr[i]], b = w[tr[i + 1]], c = w[tr[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a); float len = n.magnitude; if (len < 1e-6f) continue; n /= len;
                if (Mathf.Abs(n.y) < 0.6f) continue;                       // só superfícies viradas para cima ou para baixo (piso/teto)
                if (n.y < 0f) continue;
                float area = len * 0.5f; int N = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(area) / (CEL * 0.7f)), 1, 60);
                for (int p = 0; p <= N; p++) for (int q = 0; q <= N - p; q++)
                {
                    float u = p / (float)N, v = q / (float)N; Vector3 pt = a + (b - a) * u + (c - a) * v;
                    AddPiso(pt.x, pt.z, pt.y);
                }
            }
        }
    }

    // o chão mais próximo de yRef (terreno ou piso do castelo), de 12 m abaixo até 1,5 m acima
    static bool Suporte(float x, float z, float yRef, out float suporte)
    {
        suporte = 0f; float melhor = float.MaxValue; bool achou = false;
        float t = ter.SampleHeight(new Vector3(x, 0f, z)) + baseY;
        if (t >= yRef - 12f && t <= yRef + 1.5f) { melhor = Mathf.Abs(yRef - t); suporte = t; achou = true; }
        List<float> l;
        if (pisos.TryGetValue(Chave(Mathf.FloorToInt(x / CEL), Mathf.FloorToInt(z / CEL)), out l))
            foreach (float f in l)
                if (f >= yRef - 12f && f <= yRef + 1.5f && Mathf.Abs(yRef - f) < melhor) { melhor = Mathf.Abs(yRef - f); suporte = f; achou = true; }
        return achou;
    }

    // calcula o deslocamento vertical de um conjunto de pontos (mundo). 0 = deixa como está
    static float Deslocar(List<Vector3> pts, out string motivo)
    {
        motivo = "";
        float yb = float.MaxValue; foreach (Vector3 p in pts) yb = Mathf.Min(yb, p.y);
        var pe = new List<Vector3>(); foreach (Vector3 p in pts) if (p.y < yb + 0.4f) pe.Add(p);
        int passo = Mathf.Max(1, pe.Count / 40);
        float gMin = float.MaxValue, gMax = float.MinValue; int validos = 0;
        for (int i = 0; i < pe.Count; i += passo)
        {
            float s; if (!Suporte(pe[i].x, pe[i].z, pe[i].y, out s)) continue;
            float g = pe[i].y - s; gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g); validos++;
        }
        if (validos == 0) { motivo = "sem chão por perto"; return 0f; }
        if (gMin > 0.08f) { motivo = "no ar"; return -(gMin - 0.02f); }
        if (gMax < -0.8f) { motivo = "enterrado"; return -gMax - 0.05f; }
        return 0f;
    }

    class Ilha { public List<int> verts = new List<int>(); }

    [MenuItem("Pinglu/81. Castelo: pousar entulho, madeira e props no chão")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        ter = Terrain.activeTerrain;
        if (ter == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o terreno. Abra a cena SampleScene_Ambientacao.", "OK"); return; }
        baseY = ter.transform.position.y;
        Transform raiz = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (t.name == "Castelo Destruido") { raiz = t; break; }
        Transform estrutura = raiz != null ? raiz.Find("Estrutura") : null;
        if (estrutura == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Castelo Destruido (pasta Estrutura) na cena.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou descer até o chão os pedaços de entulho, madeira e ouro do castelo e os props soltos.\n\nNão mexo no terreno, nas muralhas, nas torres nem no portão. Faço cópia das malhas em Backups.", "Continuar", "Cancelar")) return;

        Directory.CreateDirectory("Backups");
        var sb = new StringBuilder(); int ilhasTotal = 0, desceu = 0, subiu = 0, props = 0; float maior = 0f;
        try
        {
            Physics.SyncTransforms();
            EditorUtility.DisplayProgressBar("Castelo", "Medindo pisos do castelo...", 0.1f);
            MontarPisos(estrutura);
            sb.AppendLine("pisos medidos em " + pisos.Count + " pontos");

            // ---- pedaços soltos das malhas de detritos
            var feitas = new HashSet<Mesh>();
            foreach (MeshFilter mf in estrutura.GetComponentsInChildren<MeshFilter>(false))
            {
                string nome = mf.transform.name, nomePai = mf.transform.parent != null ? mf.transform.parent.name : "";
                if (mf.sharedMesh == null || !(Tem(nome, DETRITOS) || Tem(nomePai, DETRITOS))) continue;
                Mesh m = mf.sharedMesh; if (!feitas.Add(m)) continue;
                string caminho = AssetDatabase.GetAssetPath(m);
                if (!string.IsNullOrEmpty(caminho) && File.Exists(caminho)) { string c = "Backups/" + Path.GetFileName(caminho) + ".antes_de_pousar"; if (!File.Exists(c)) File.Copy(caminho, c, false); }
                EditorUtility.DisplayProgressBar("Castelo", "Pedaços de \"" + nome + "\"...", 0.3f);
                int d, s2, n; float mx;
                Pousar(mf, m, out n, out d, out s2, out mx, sb);
                ilhasTotal += n; desceu += d; subiu += s2; maior = Mathf.Max(maior, mx);
            }

            // ---- props soltos
            Transform pasta = raiz.Find("Props do Kit");
            if (pasta != null)
                foreach (Transform p in pasta)
                {
                    if (!p.gameObject.activeInHierarchy || Tem(p.name, NAO_MEXER)) continue;
                    var pts = new List<Vector3>();
                    foreach (MeshFilter mf in p.GetComponentsInChildren<MeshFilter>(false))
                    {
                        if (mf.sharedMesh == null) continue; Matrix4x4 M = mf.transform.localToWorldMatrix;
                        Vector3[] vs = mf.sharedMesh.vertices; int st = Mathf.Max(1, vs.Length / 4000);
                        for (int i = 0; i < vs.Length; i += st) pts.Add(M.MultiplyPoint3x4(vs[i]));
                    }
                    if (pts.Count == 0) continue;
                    string motivo; float dy = Deslocar(pts, out motivo);
                    if (Mathf.Abs(dy) < 0.03f) continue;
                    Undo.RecordObject(p, "Pousar prop"); p.position += Vector3.up * dy; props++; maior = Mathf.Max(maior, Mathf.Abs(dy));
                    sb.AppendLine("- prop \"" + p.name + "\" " + motivo + ": " + (dy < 0 ? "desceu " : "subiu ") + Mathf.Abs(dy).ToString("0.00") + " m");
                }

            Physics.SyncTransforms();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
            string resumo = "Pronto!\n• Pedaços de entulho/madeira/ouro analisados: " + ilhasTotal + "\n   - desceram: " + desceu + "\n   - subiram (enterrados): " + subiu + "\n• Props soltos ajustados: " + props + "\n• Maior ajuste: " + maior.ToString("0.0") + " m";
            Debug.Log("[Castelo pousar]\n" + resumo + "\n" + sb);
            EditorUtility.DisplayDialog("Pinglu", resumo + "\n\nDetalhes no Console.", "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static void Pousar(MeshFilter mf, Mesh mesh, out int ilhas, out int desceu, out int subiu, out float maior, StringBuilder sb)
    {
        ilhas = 0; desceu = 0; subiu = 0; maior = 0f;
        Vector3[] vs = mesh.vertices; int[] tr = mesh.triangles;
        if (vs.Length == 0 || tr.Length == 0) return;
        Matrix4x4 M = mf.transform.localToWorldMatrix, Minv = mf.transform.worldToLocalMatrix;

        // solda vértices na mesma posição e junta os triângulos ligados
        var soldado = new Dictionary<long, int>(); var id = new int[vs.Length];
        for (int i = 0; i < vs.Length; i++)
        {
            long k = ((long)Mathf.RoundToInt(vs[i].x * 100f) * 73856093L) ^ ((long)Mathf.RoundToInt(vs[i].y * 100f) * 19349663L) ^ ((long)Mathf.RoundToInt(vs[i].z * 100f) * 83492791L);
            int v; if (!soldado.TryGetValue(k, out v)) { v = soldado.Count; soldado[k] = v; }
            id[i] = v;
        }
        var pai = new int[soldado.Count]; for (int i = 0; i < pai.Length; i++) pai[i] = i;
        int Achar(int x) { while (pai[x] != x) { pai[x] = pai[pai[x]]; x = pai[x]; } return x; }
        var usado = new bool[vs.Length];
        for (int i = 0; i + 2 < tr.Length; i += 3)
        {
            int a = Achar(id[tr[i]]), b = Achar(id[tr[i + 1]]), c = Achar(id[tr[i + 2]]);
            if (a != b) pai[b] = a; if (Achar(a) != Achar(c)) pai[Achar(c)] = Achar(a);
            usado[tr[i]] = usado[tr[i + 1]] = usado[tr[i + 2]] = true;
        }
        var grupos = new Dictionary<int, Ilha>();
        for (int i = 0; i < vs.Length; i++)
        {
            if (!usado[i]) continue;
            int r = Achar(id[i]); Ilha il; if (!grupos.TryGetValue(r, out il)) { il = new Ilha(); grupos[r] = il; }
            il.verts.Add(i);
        }

        bool mudou = false; var novos = (Vector3[])vs.Clone();
        foreach (Ilha il in grupos.Values)
        {
            var pts = new List<Vector3>(il.verts.Count); Bounds bb = new Bounds();
            for (int i = 0; i < il.verts.Count; i++) { Vector3 w = M.MultiplyPoint3x4(vs[il.verts[i]]); pts.Add(w); if (i == 0) bb = new Bounds(w, Vector3.zero); else bb.Encapsulate(w); }
            if (bb.size.x > 28f || bb.size.z > 28f) continue;                 // peça grande (muro): não é entulho
            ilhas++;
            string motivo; float dy = Deslocar(pts, out motivo);
            if (Mathf.Abs(dy) < 0.03f) continue;
            Vector3 dl = Minv.MultiplyVector(Vector3.up * dy);
            foreach (int i in il.verts) novos[i] = vs[i] + dl;
            mudou = true; maior = Mathf.Max(maior, Mathf.Abs(dy));
            if (dy < 0f) desceu++; else subiu++;
            if (Mathf.Abs(dy) > 1f) sb.AppendLine("- " + mf.name + ": pedaço em (" + Mathf.RoundToInt(bb.center.x) + ", " + Mathf.RoundToInt(bb.center.z) + ") " + motivo + ", " + (dy < 0 ? "desceu " : "subiu ") + Mathf.Abs(dy).ToString("0.0") + " m");
        }
        if (!mudou) return;
        mesh.vertices = novos; mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        foreach (MeshCollider mc in mf.GetComponentsInChildren<MeshCollider>(true)) if (mc.sharedMesh == mesh) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
        MeshCollider own = mf.GetComponent<MeshCollider>(); if (own != null) { own.sharedMesh = null; own.sharedMesh = mesh; }
    }
}
#endif
