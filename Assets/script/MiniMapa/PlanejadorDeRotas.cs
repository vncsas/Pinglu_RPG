using System.Collections.Generic;
using UnityEngine;

// Acha caminhos pelo terreno (A*) numa grade de células de ~8 m: evita ladeiras muito inclinadas,
// prefere estradas (como o GPS de GTA e Witcher) e suaviza o resultado.
public class PlanejadorDeRotas
{
    private readonly Terrain terreno;
    private readonly int nx, nz;
    private readonly float cel;
    private readonly Vector3 origem;
    private readonly float[] altura;
    private readonly bool[] estrada;
    private const float PENDENCIA_MAXIMA = 0.9f;   // ~42 graus

    public PlanejadorDeRotas(Terrain t, float celula = 8f)
    {
        terreno = t; cel = celula;
        origem = t.transform.position;
        Vector3 s = t.terrainData.size;
        nx = Mathf.Max(8, Mathf.CeilToInt(s.x / celula) + 1);
        nz = Mathf.Max(8, Mathf.CeilToInt(s.z / celula) + 1);
        altura = new float[nx * nz];
        estrada = new bool[nx * nz];
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
                altura[z * nx + x] = t.SampleHeight(new Vector3(origem.x + x * cel, 0f, origem.z + z * cel)) + origem.y;
    }

    private int Cx(float x) { return Mathf.Clamp(Mathf.RoundToInt((x - origem.x) / cel), 0, nx - 1); }
    private int Cz(float z) { return Mathf.Clamp(Mathf.RoundToInt((z - origem.z) / cel), 0, nz - 1); }
    private int Indice(Vector3 p) { return Cz(p.z) * nx + Cx(p.x); }
    private Vector3 Centro(int i) { int x = i % nx, z = i / nx; return new Vector3(origem.x + x * cel, altura[i], origem.z + z * cel); }

    public float AlturaEm(Vector3 p) { return terreno.SampleHeight(p) + origem.y; }

    // marca as células por onde passam as estradas (o caminho dá preferência a elas)
    public void MarcarEstradas(IEnumerable<Vector3[]> polilinhas)
    {
        System.Array.Clear(estrada, 0, estrada.Length);
        foreach (Vector3[] pl in polilinhas)
            for (int i = 0; i + 1 < pl.Length; i++)
            {
                float d = Vector3.Distance(pl[i], pl[i + 1]);
                int passos = Mathf.Max(1, Mathf.CeilToInt(d / (cel * 0.5f)));
                for (int k = 0; k <= passos; k++)
                {
                    Vector3 p = Vector3.Lerp(pl[i], pl[i + 1], k / (float)passos);
                    estrada[Indice(p)] = true;
                }
            }
    }

    // ------------------------------------------------------------------
    public List<Vector3> Rota(Vector3 de, Vector3 para, bool usarEstradas)
    {
        int s = Indice(de), g = Indice(para);
        List<Vector3> saida = new List<Vector3>();
        if (s == g) { saida.Add(de); saida.Add(para); return saida; }

        int n = nx * nz;
        float[] custo = new float[n];
        int[] pai = new int[n];
        bool[] fechado = new bool[n];
        for (int i = 0; i < n; i++) { custo[i] = float.MaxValue; pai[i] = -1; }
        float fatorH = usarEstradas ? 0.25f : 1f;
        Vector3 alvo = Centro(g);

        // fila de prioridade (heap binário)
        List<int> heap = new List<int>(); List<float> prio = new List<float>();
        System.Action<int, float> empurrar = (idx, f) =>
        {
            heap.Add(idx); prio.Add(f);
            int c = heap.Count - 1;
            while (c > 0)
            {
                int p = (c - 1) / 2;
                if (prio[p] <= prio[c]) break;
                int ti = heap[p]; heap[p] = heap[c]; heap[c] = ti;
                float tf = prio[p]; prio[p] = prio[c]; prio[c] = tf;
                c = p;
            }
        };
        System.Func<int> tirar = () =>
        {
            int topo = heap[0];
            int ultimo = heap.Count - 1;
            heap[0] = heap[ultimo]; prio[0] = prio[ultimo];
            heap.RemoveAt(ultimo); prio.RemoveAt(ultimo);
            int c = 0;
            while (true)
            {
                int l = c * 2 + 1, r = l + 1, m = c;
                if (l < heap.Count && prio[l] < prio[m]) m = l;
                if (r < heap.Count && prio[r] < prio[m]) m = r;
                if (m == c) break;
                int ti = heap[m]; heap[m] = heap[c]; heap[c] = ti;
                float tf = prio[m]; prio[m] = prio[c]; prio[c] = tf;
                c = m;
            }
            return topo;
        };

        custo[s] = 0f;
        empurrar(s, 0f);
        bool achou = false;
        while (heap.Count > 0)
        {
            int i = tirar();
            if (fechado[i]) continue;
            fechado[i] = true;
            if (i == g) { achou = true; break; }
            int x = i % nx, z = i / nx;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int xx = x + dx, zz = z + dz;
                    if (xx < 0 || zz < 0 || xx >= nx || zz >= nz) continue;
                    int j = zz * nx + xx;
                    if (fechado[j]) continue;
                    float passo = cel * (dx != 0 && dz != 0 ? 1.4142f : 1f);
                    float pend = Mathf.Abs(altura[j] - altura[i]) / passo;
                    if (pend > PENDENCIA_MAXIMA) continue;
                    float mult = 1f + (pend * 3f) * (pend * 3f);
                    if (usarEstradas && estrada[i] && estrada[j]) mult *= 0.25f;
                    float c = custo[i] + passo * mult;
                    if (c >= custo[j]) continue;
                    custo[j] = c; pai[j] = i;
                    Vector3 pj = Centro(j);
                    float h = new Vector2(pj.x - alvo.x, pj.z - alvo.z).magnitude * fatorH;
                    empurrar(j, c + h);
                }
        }

        if (!achou) { saida.Add(de); saida.Add(para); return saida; }   // sem caminho: linha reta

        List<Vector3> celulas = new List<Vector3>();
        for (int i = g; i != -1; i = pai[i]) celulas.Add(Centro(i));
        celulas.Reverse();
        celulas[0] = de; celulas[celulas.Count - 1] = para;

        List<Vector3> simples = Simplificar(celulas, 3.5f);
        for (int k = 0; k < 2; k++) simples = Suavizar(simples);
        for (int i = 0; i < simples.Count; i++) { Vector3 p = simples[i]; p.y = AlturaEm(p); simples[i] = p; }
        return simples;
    }

    // ------------------------------------------------------------------
    public static List<Vector3> Simplificar(List<Vector3> pts, float tolerancia)
    {
        if (pts.Count < 3) return new List<Vector3>(pts);
        bool[] manter = new bool[pts.Count];
        manter[0] = manter[pts.Count - 1] = true;
        Stack<int[]> pilha = new Stack<int[]>();
        pilha.Push(new[] { 0, pts.Count - 1 });
        while (pilha.Count > 0)
        {
            int[] seg = pilha.Pop();
            float maior = 0f; int idx = -1;
            Vector2 a = new Vector2(pts[seg[0]].x, pts[seg[0]].z), b = new Vector2(pts[seg[1]].x, pts[seg[1]].z);
            for (int i = seg[0] + 1; i < seg[1]; i++)
            {
                float d = DistanciaAoSegmento(new Vector2(pts[i].x, pts[i].z), a, b);
                if (d > maior) { maior = d; idx = i; }
            }
            if (idx >= 0 && maior > tolerancia) { manter[idx] = true; pilha.Push(new[] { seg[0], idx }); pilha.Push(new[] { idx, seg[1] }); }
        }
        List<Vector3> r = new List<Vector3>();
        for (int i = 0; i < pts.Count; i++) if (manter[i]) r.Add(pts[i]);
        return r;
    }

    private static float DistanciaAoSegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (a + ab * t - p).magnitude;
    }

    // suaviza os cantos (Chaikin), mantendo o começo e o fim
    public static List<Vector3> Suavizar(List<Vector3> pts)
    {
        if (pts.Count < 3) return new List<Vector3>(pts);
        List<Vector3> r = new List<Vector3> { pts[0] };
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            r.Add(Vector3.Lerp(pts[i], pts[i + 1], 0.25f));
            r.Add(Vector3.Lerp(pts[i], pts[i + 1], 0.75f));
        }
        r.Add(pts[pts.Count - 1]);
        return r;
    }

    // pontos a cada "passo" metros (para pintar/desenhar estradas)
    public static List<Vector3> Reamostrar(List<Vector3> pts, float passo)
    {
        List<Vector3> r = new List<Vector3>();
        if (pts.Count == 0) return r;
        r.Add(pts[0]);
        float acumulado = 0f;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            float d = Vector3.Distance(pts[i], pts[i + 1]);
            acumulado += d;
            if (acumulado >= passo) { r.Add(pts[i + 1]); acumulado = 0f; }
        }
        if (r[r.Count - 1] != pts[pts.Count - 1]) r.Add(pts[pts.Count - 1]);
        return r;
    }
}
