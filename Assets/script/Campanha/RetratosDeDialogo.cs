using System.Collections.Generic;
using UnityEngine;

// Aparência do balão de conversa de cada personagem (cores, cantos) + retratos desenhados por código.
// Se existir uma imagem em Assets/Resources/Retratos/<nome>.png (ex.: Caio.png), ela é usada no lugar do desenho.
public class EstiloDeFala
{
    public Color fundo, borda, nome, texto, brilho;
    public float cantos;

    static readonly Dictionary<string, EstiloDeFala> cache = new Dictionary<string, EstiloDeFala>();

    public static EstiloDeFala Para(string quem)
    {
        string k = Tipo(quem);
        EstiloDeFala e;
        if (cache.TryGetValue(k, out e)) return e;
        e = new EstiloDeFala();
        switch (k)
        {
            case "caio":
                e.fundo = new Color(0.10f, 0.075f, 0.05f, 0.94f); e.borda = new Color(0.85f, 0.65f, 0.28f); e.nome = new Color(1f, 0.82f, 0.4f);
                e.texto = new Color(0.96f, 0.92f, 0.82f); e.brilho = new Color(1f, 0.8f, 0.35f); e.cantos = 26f; break;
            case "ladrao":
                e.fundo = new Color(0.065f, 0.035f, 0.10f, 0.95f); e.borda = new Color(0.62f, 0.3f, 0.92f); e.nome = new Color(0.8f, 0.55f, 1f);
                e.texto = new Color(0.9f, 0.86f, 0.97f); e.brilho = new Color(0.7f, 0.35f, 1f); e.cantos = 3f; break;
            case "cavaleiro":
                e.fundo = new Color(0.065f, 0.075f, 0.095f, 0.95f); e.borda = new Color(0.62f, 0.7f, 0.8f); e.nome = new Color(0.78f, 0.87f, 0.97f);
                e.texto = new Color(0.9f, 0.93f, 0.97f); e.brilho = new Color(0.85f, 0.25f, 0.2f); e.cantos = 10f; break;
            case "demonio":
                e.fundo = new Color(0.12f, 0.025f, 0.025f, 0.95f); e.borda = new Color(0.9f, 0.18f, 0.1f); e.nome = new Color(1f, 0.38f, 0.25f);
                e.texto = new Color(1f, 0.9f, 0.86f); e.brilho = new Color(1f, 0.5f, 0.1f); e.cantos = 0f; break;
            case "cavaleirosombrio":
                e.fundo = new Color(0.08f, 0.05f, 0.05f, 0.95f); e.borda = new Color(0.85f, 0.45f, 0.2f); e.nome = new Color(1f, 0.6f, 0.3f);
                e.texto = new Color(0.97f, 0.9f, 0.85f); e.brilho = new Color(1f, 0.5f, 0.15f); e.cantos = 6f; break;
            case "morgana":
                e.fundo = new Color(0.07f, 0.04f, 0.12f, 0.95f); e.borda = new Color(0.75f, 0.35f, 0.7f); e.nome = new Color(0.95f, 0.6f, 0.9f);
                e.texto = new Color(0.95f, 0.9f, 0.97f); e.brilho = new Color(0.85f, 0.4f, 1f); e.cantos = 20f; break;
            case "tanque":
                e.fundo = new Color(0.1f, 0.05f, 0.03f, 0.95f); e.borda = new Color(1f, 0.55f, 0.1f); e.nome = new Color(1f, 0.7f, 0.2f);
                e.texto = new Color(1f, 0.93f, 0.82f); e.brilho = new Color(1f, 0.6f, 0.1f); e.cantos = 4f; break;
            case "curandeiro":
                e.fundo = new Color(0.07f, 0.09f, 0.07f, 0.95f); e.borda = new Color(0.85f, 0.85f, 0.7f); e.nome = new Color(0.8f, 1f, 0.75f);
                e.texto = new Color(0.95f, 0.97f, 0.9f); e.brilho = new Color(0.5f, 1f, 0.6f); e.cantos = 16f; break;
            default:
                e.fundo = new Color(0.04f, 0.09f, 0.07f, 0.94f); e.borda = new Color(0.4f, 0.75f, 0.5f); e.nome = new Color(0.6f, 0.95f, 0.7f);
                e.texto = new Color(0.92f, 0.97f, 0.93f); e.brilho = new Color(0.5f, 1f, 0.6f); e.cantos = 18f; break;
        }
        cache[k] = e; return e;
    }

    public static string Tipo(string quem)
    {
        string q = (quem ?? "").ToLowerInvariant();
        if (q.Contains("caio")) return "caio";
        if (q.Contains("sombrio")) return "cavaleirosombrio";
        if (q.Contains("morgana") || q.Contains("maga")) return "morgana";
        if (q.Contains("tanque")) return "tanque";
        if (q.Contains("curandeiro")) return "curandeiro";
        if (q.Contains("lad")) return "ladrao";
        if (q.Contains("cav")) return "cavaleiro";
        if (q.Contains("dem")) return "demonio";
        return "heroi";
    }
}

public static class RetratosDeDialogo
{
    const int N = 256;
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    public static Texture2D Obter(string quem)
    {
        string k = EstiloDeFala.Tipo(quem);
        Texture2D t;
        if (cache.TryGetValue(k, out t) && t != null) return t;
        t = Resources.Load<Texture2D>("Retratos/" + quem);
        if (t == null) t = Resources.Load<Texture2D>("Retratos/" + k);
        if (t == null) t = Desenhar(k);
        cache[k] = t; return t;
    }

    // ---------- mini rasterizador (coordenadas de 0 a 1, y para cima) ----------
    class Tela
    {
        public Color[] p = new Color[N * N];

        public void Fundo(Color baixo, Color cima)
        {
            for (int y = 0; y < N; y++) { Color c = Color.Lerp(baixo, cima, y / (float)(N - 1)); for (int x = 0; x < N; x++) p[y * N + x] = c; }
        }

        void Mistura(int i, Color c, float a) { p[i] = Color.Lerp(p[i], new Color(c.r, c.g, c.b, 1f), a * c.a); }

        public void Elipse(float cx, float cy, float rx, float ry, Color c)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((cx - rx) * N) - 2), x1 = Mathf.Min(N - 1, Mathf.CeilToInt((cx + rx) * N) + 2);
            int y0 = Mathf.Max(0, Mathf.FloorToInt((cy - ry) * N) - 2), y1 = Mathf.Min(N - 1, Mathf.CeilToInt((cy + ry) * N) + 2);
            float menor = Mathf.Min(rx, ry) * N;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = ((x + 0.5f) / N - cx) / rx, dy = ((y + 0.5f) / N - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * menor + 0.5f);
                    if (a > 0f) Mistura(y * N + x, c, a);
                }
        }

        // halo suave (para olhos que brilham)
        public void Halo(float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((cx - r) * N)), x1 = Mathf.Min(N - 1, Mathf.CeilToInt((cx + r) * N));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((cy - r) * N)), y1 = Mathf.Min(N - 1, Mathf.CeilToInt((cy + r) * N));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f) / N - cx, dy = (y + 0.5f) / N - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (d < 1f) Mistura(y * N + x, c, (1f - d) * (1f - d));
                }
        }

        public void Poli(Vector2[] v, Color c)
        {
            float minx = 1, maxx = 0, miny = 1, maxy = 0;
            for (int i = 0; i < v.Length; i++) { minx = Mathf.Min(minx, v[i].x); maxx = Mathf.Max(maxx, v[i].x); miny = Mathf.Min(miny, v[i].y); maxy = Mathf.Max(maxy, v[i].y); }
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minx * N) - 1), x1 = Mathf.Min(N - 1, Mathf.CeilToInt(maxx * N) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(miny * N) - 1), y1 = Mathf.Min(N - 1, Mathf.CeilToInt(maxy * N) + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int dentro = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            if (Dentro(v, (x + (sx + 0.5f) / 3f) / N, (y + (sy + 0.5f) / 3f) / N)) dentro++;
                    if (dentro > 0) Mistura(y * N + x, c, dentro / 9f);
                }
        }

        static bool Dentro(Vector2[] v, float px, float py)
        {
            bool r = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
                if ((v[i].y > py) != (v[j].y > py) && px < (v[j].x - v[i].x) * (py - v[i].y) / (v[j].y - v[i].y) + v[i].x) r = !r;
            return r;
        }

        public void Retangulo(float x0, float y0, float x1, float y1, Color c)
        {
            Poli(new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) }, c);
        }

        public void Vinheta(float forca)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                    float f = 1f - forca * Mathf.Clamp01((dx * dx + dy * dy) * 3.2f);
                    Color c = p[y * N + x]; p[y * N + x] = new Color(c.r * f, c.g * f, c.b * f, 1f);
                }
        }

        public Texture2D Pronto()
        {
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels(p); t.Apply(false, false); return t;
        }
    }

    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    static Texture2D Desenhar(string k)
    {
        Tela t = new Tela();
        switch (k)
        {
            case "caio": Caio(t); break;
            case "ladrao": Ladrao(t); break;
            case "cavaleiro": Cavaleiro(t); break;
            case "demonio": Demonio(t); break;
            default: Heroi(t); break;
        }
        t.Vinheta(0.55f);
        return t.Pronto();
    }

    static void Caio(Tela t)
    {
        Color pele = new Color(0.93f, 0.74f, 0.6f), branco = new Color(0.93f, 0.93f, 0.95f), ouro = new Color(0.9f, 0.7f, 0.25f);
        t.Fundo(new Color(0.22f, 0.15f, 0.08f), new Color(0.45f, 0.32f, 0.16f));
        t.Halo(0.5f, 0.6f, 0.5f, new Color(1f, 0.85f, 0.5f, 0.35f));
        t.Elipse(0.5f, -0.02f, 0.47f, 0.34f, new Color(0.17f, 0.27f, 0.55f));
        t.Elipse(0.5f, 0.33f, 0.08f, 0.09f, new Color(0.8f, 0.6f, 0.48f));
        t.Elipse(0.5f, 0.56f, 0.21f, 0.23f, branco);                                   // cabelo atrás
        t.Elipse(0.5f, 0.54f, 0.165f, 0.2f, pele);                                     // rosto
        t.Elipse(0.335f, 0.5f, 0.045f, 0.1f, branco); t.Elipse(0.665f, 0.5f, 0.045f, 0.1f, branco);
        t.Poli(new[] { V(0.335f, 0.5f), V(0.665f, 0.5f), V(0.65f, 0.3f), V(0.57f, 0.13f), V(0.5f, 0.09f), V(0.43f, 0.13f), V(0.35f, 0.3f) }, branco); // barba
        t.Elipse(0.5f, 0.505f, 0.032f, 0.038f, new Color(0.84f, 0.62f, 0.5f));          // nariz
        t.Elipse(0.455f, 0.455f, 0.065f, 0.026f, branco); t.Elipse(0.545f, 0.455f, 0.065f, 0.026f, branco);   // bigode
        t.Elipse(0.43f, 0.585f, 0.062f, 0.062f, ouro); t.Elipse(0.57f, 0.585f, 0.062f, 0.062f, ouro);           // óculos
        t.Elipse(0.43f, 0.585f, 0.05f, 0.05f, new Color(0.95f, 0.85f, 0.75f)); t.Elipse(0.57f, 0.585f, 0.05f, 0.05f, new Color(0.95f, 0.85f, 0.75f));
        t.Retangulo(0.485f, 0.58f, 0.515f, 0.59f, ouro);
        t.Elipse(0.43f, 0.585f, 0.014f, 0.018f, new Color(0.15f, 0.1f, 0.08f)); t.Elipse(0.57f, 0.585f, 0.014f, 0.018f, new Color(0.15f, 0.1f, 0.08f));
        t.Elipse(0.43f, 0.665f, 0.05f, 0.013f, branco); t.Elipse(0.57f, 0.665f, 0.05f, 0.013f, branco);
        // cajado com cristal
        t.Retangulo(0.86f, 0f, 0.885f, 0.8f, new Color(0.35f, 0.22f, 0.1f));
        t.Poli(new[] { V(0.8725f, 0.95f), V(0.91f, 0.84f), V(0.8725f, 0.76f), V(0.835f, 0.84f) }, new Color(0.4f, 0.95f, 0.7f));
    }

    static void Ladrao(Tela t)
    {
        Color capa = new Color(0.1f, 0.065f, 0.15f), borda = new Color(0.22f, 0.13f, 0.32f), roxo = new Color(0.75f, 0.4f, 1f);
        t.Fundo(new Color(0.05f, 0.02f, 0.09f), new Color(0.2f, 0.1f, 0.3f));
        t.Halo(0.5f, 0.55f, 0.55f, new Color(0.6f, 0.25f, 1f, 0.35f));
        t.Elipse(0.5f, -0.02f, 0.47f, 0.36f, capa);
        t.Elipse(0.5f, 0.56f, 0.28f, 0.33f, borda);                                    // capuz (borda clara)
        t.Elipse(0.5f, 0.56f, 0.265f, 0.315f, capa);
        t.Poli(new[] { V(0.5f, 0.98f), V(0.4f, 0.8f), V(0.6f, 0.8f) }, capa);          // ponta do capuz
        t.Elipse(0.5f, 0.52f, 0.17f, 0.2f, new Color(0.015f, 0.01f, 0.025f));          // rosto na sombra
        t.Halo(0.43f, 0.56f, 0.09f, new Color(0.8f, 0.4f, 1f, 0.9f)); t.Halo(0.57f, 0.56f, 0.09f, new Color(0.8f, 0.4f, 1f, 0.9f));
        t.Poli(new[] { V(0.375f, 0.575f), V(0.47f, 0.55f), V(0.46f, 0.56f), V(0.39f, 0.585f) }, roxo);   // olhos em fenda
        t.Poli(new[] { V(0.625f, 0.575f), V(0.53f, 0.55f), V(0.54f, 0.56f), V(0.61f, 0.585f) }, roxo);
        t.Elipse(0.5f, 0.4f, 0.1f, 0.045f, new Color(0.95f, 0.93f, 1f));              // sorriso
        t.Elipse(0.5f, 0.425f, 0.11f, 0.05f, new Color(0.015f, 0.01f, 0.025f));
    }

    static void Cavaleiro(Tela t)
    {
        Color aco = new Color(0.55f, 0.6f, 0.68f), escuro = new Color(0.3f, 0.34f, 0.4f), claro = new Color(0.75f, 0.8f, 0.88f);
        t.Fundo(new Color(0.07f, 0.08f, 0.1f), new Color(0.22f, 0.25f, 0.3f));
        t.Elipse(0.17f, 0.12f, 0.2f, 0.17f, escuro); t.Elipse(0.83f, 0.12f, 0.2f, 0.17f, escuro);   // ombreiras
        t.Elipse(0.5f, -0.02f, 0.36f, 0.3f, aco);                                       // peitoral
        t.Elipse(0.5f, 0.52f, 0.2f, 0.27f, aco);                                        // elmo
        t.Elipse(0.5f, 0.6f, 0.17f, 0.17f, claro);                                      // brilho do topo
        t.Poli(new[] { V(0.5f, 0.97f), V(0.47f, 0.78f), V(0.53f, 0.78f) }, new Color(0.7f, 0.1f, 0.1f)); // crista
        t.Retangulo(0.31f, 0.5f, 0.69f, 0.56f, new Color(0.02f, 0.02f, 0.03f));        // fenda da viseira
        t.Halo(0.43f, 0.53f, 0.07f, new Color(1f, 0.2f, 0.1f, 0.9f)); t.Halo(0.57f, 0.53f, 0.07f, new Color(1f, 0.2f, 0.1f, 0.9f));
        t.Retangulo(0.493f, 0.3f, 0.507f, 0.5f, escuro);                                // nasal
        for (int i = 0; i < 4; i++) t.Retangulo(0.4f, 0.3f - i * 0.03f, 0.6f, 0.315f - i * 0.03f, escuro);   // grade
    }

    static void Demonio(Tela t)
    {
        Color pele = new Color(0.62f, 0.1f, 0.08f), escuro = new Color(0.3f, 0.03f, 0.03f);
        t.Fundo(new Color(0.12f, 0.01f, 0.01f), new Color(0.5f, 0.12f, 0.04f));
        t.Halo(0.5f, 0.55f, 0.6f, new Color(1f, 0.4f, 0.05f, 0.45f));
        t.Poli(new[] { V(0.28f, 0.7f), V(0.12f, 0.98f), V(0.2f, 0.6f) }, new Color(0.15f, 0.08f, 0.06f));   // chifres
        t.Poli(new[] { V(0.72f, 0.7f), V(0.88f, 0.98f), V(0.8f, 0.6f) }, new Color(0.15f, 0.08f, 0.06f));
        t.Elipse(0.5f, -0.02f, 0.44f, 0.32f, escuro);
        t.Elipse(0.5f, 0.52f, 0.2f, 0.26f, pele);
        t.Poli(new[] { V(0.3f, 0.64f), V(0.46f, 0.57f), V(0.44f, 0.62f), V(0.33f, 0.68f) }, new Color(0.02f, 0f, 0f));   // sobrancelhas
        t.Poli(new[] { V(0.7f, 0.64f), V(0.54f, 0.57f), V(0.56f, 0.62f), V(0.67f, 0.68f) }, new Color(0.02f, 0f, 0f));
        t.Halo(0.4f, 0.56f, 0.09f, new Color(1f, 0.85f, 0.1f, 0.9f)); t.Halo(0.6f, 0.56f, 0.09f, new Color(1f, 0.85f, 0.1f, 0.9f));
        t.Elipse(0.4f, 0.55f, 0.04f, 0.017f, new Color(1f, 0.95f, 0.3f)); t.Elipse(0.6f, 0.55f, 0.04f, 0.017f, new Color(1f, 0.95f, 0.3f));
        t.Elipse(0.5f, 0.34f, 0.12f, 0.06f, new Color(0.08f, 0f, 0f));                  // boca
        for (int i = 0; i < 4; i++) { float x = 0.42f + i * 0.053f; t.Poli(new[] { V(x, 0.375f), V(x + 0.03f, 0.375f), V(x + 0.015f, 0.32f) }, new Color(1f, 0.95f, 0.85f)); }
    }

    static void Heroi(Tela t)
    {
        Color pele = new Color(0.9f, 0.7f, 0.55f), cabelo = new Color(0.35f, 0.2f, 0.1f);
        t.Fundo(new Color(0.06f, 0.14f, 0.1f), new Color(0.18f, 0.38f, 0.28f));
        t.Elipse(0.5f, -0.02f, 0.44f, 0.32f, new Color(0.15f, 0.45f, 0.3f));
        t.Elipse(0.5f, 0.32f, 0.08f, 0.09f, new Color(0.78f, 0.58f, 0.45f));
        t.Elipse(0.5f, 0.55f, 0.18f, 0.22f, pele);
        t.Elipse(0.5f, 0.68f, 0.2f, 0.14f, cabelo);
        t.Elipse(0.43f, 0.55f, 0.016f, 0.02f, new Color(0.1f, 0.07f, 0.05f)); t.Elipse(0.57f, 0.55f, 0.016f, 0.02f, new Color(0.1f, 0.07f, 0.05f));
        t.Elipse(0.5f, 0.42f, 0.05f, 0.02f, new Color(0.65f, 0.3f, 0.25f));
    }
}
