using System;
using System.Collections.Generic;
using UnityEngine;

// Ícones das 12 habilidades, desenhados por código (formas vetoriais suaves, brancas sobre transparente: o HUD pinta na cor que quiser),
// mais as imagens de círculo, anel e brilho usadas no HUD de habilidades.
public static class IconesDeHabilidade
{
    const int N = 96;
    static readonly Texture2D[,] cache = new Texture2D[4, 3];
    static Texture2D disco, anel, brilho, gradiente, vidro, raios;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { Array.Clear(cache, 0, cache.Length); disco = anel = brilho = gradiente = vidro = raios = null; }

    public static Texture2D Disco { get { if (disco == null) disco = Redonda(0f, 0f); return disco; } }
    public static Texture2D Anel { get { if (anel == null) anel = Redonda(0.055f, 0f); return anel; } }
    // disco "de vidro": claro no meio e escuro nas bordas (a cor vem do tom aplicado)
    public static Texture2D Gradiente { get { if (gradiente == null) gradiente = Feita(false); return gradiente; } }
    // reflexo curvo de luz no alto do círculo
    public static Texture2D Vidro { get { if (vidro == null) vidro = Feita(true); return vidro; } }
    // feixes de luz que giram (aura do poder máximo)
    public static Texture2D Raios { get { if (raios == null) raios = FeixesDeLuz(); return raios; } }
    public static Texture2D Brilho { get { if (brilho == null) brilho = Redonda(0f, 1f); return brilho; } }

    public static Texture2D Obter(ClasseDeHabilidade classe, int i)
    {
        int c = (int)classe; i = Mathf.Clamp(i, 0, 2);
        if (cache[c, i] == null) cache[c, i] = Gerar(classe, i);
        return cache[c, i];
    }

    // círculo cheio (espessura 0), anel (espessura > 0) ou brilho macio (macio = 1)
    static Texture2D Redonda(float espessura, float macio)
    {
        int n = 192; Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float r = n * 0.5f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
            float a;
            if (macio > 0.5f) a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
            else if (espessura > 0f) a = Mathf.Clamp01((d - (0.985f - espessura)) * r * 0.6f) * Mathf.Clamp01((0.985f - d) * r * 0.6f + 0.5f);
            else a = Mathf.Clamp01((0.985f - d) * r * 0.6f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }
        t.Apply(true); return t;
    }

    static Texture2D Feita(bool reflexo)
    {
        int n = 192; Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float r = n * 0.5f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = (x + 0.5f - r) / r, v = (y + 0.5f - r) / r;             // v > 0 = parte de cima da textura
            float d = Mathf.Sqrt(u * u + v * v);
            float borda = Mathf.Clamp01((0.985f - d) * r * 0.6f);
            if (!reflexo)
            {
                float g = Mathf.Lerp(1f, 0.38f, Mathf.Pow(Mathf.SmoothStep(0f, 1f, d), 1.3f));
                g += 0.18f * Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.25f, 0.35f)) * 1.6f);
                t.SetPixel(x, y, new Color(Mathf.Clamp01(g), Mathf.Clamp01(g), Mathf.Clamp01(g), borda));
            }
            else
            {
                float e = Mathf.Pow(u / 0.62f, 2f) + Mathf.Pow((v - 0.55f) / 0.3f, 2f);
                float a = Mathf.Clamp01(1f - e); a = a * a * 0.85f;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * borda));
            }
        }
        t.Apply(true); return t;
    }

    static Texture2D FeixesDeLuz()
    {
        int n = 256; Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float r = n * 0.5f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = (x + 0.5f - r) / r, v = (y + 0.5f - r) / r; float d = Mathf.Sqrt(u * u + v * v);
            float th = Mathf.Atan2(v, u);
            float feixe = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(th * 9f), 3f) * 0.8f + 0.2f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.3f) * feixe * Mathf.SmoothStep(0.05f, 0.35f, d);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }
        t.Apply(true); return t;
    }

    // ---------------------------------------------------------------- pintor de formas
    class Pintor
    {
        public float[] a = new float[N * N];
        float Cob(float d, float meio) { return Mathf.Clamp01(meio - d + 0.5f); }                  // cobertura suave (1 pixel de borda)
        void Aplicar(Func<float, float, float> distancia, float meio)
        {
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float v = Cob(distancia(x + 0.5f, y + 0.5f), meio);
                if (v > a[y * N + x]) a[y * N + x] = v;
            }
        }
        static float K(float v) { return v * N / 100f; }
        public void Linha(float x0, float y0, float x1, float y1, float w)
        {
            Vector2 p = new Vector2(K(x0), K(y0)), q = new Vector2(K(x1), K(y1)); float meio = K(w) * 0.5f;
            Aplicar((x, y) => { Vector2 pq = q - p, pp = new Vector2(x, y) - p; float h = Mathf.Clamp01(Vector2.Dot(pp, pq) / Mathf.Max(0.0001f, pq.sqrMagnitude)); return (pp - pq * h).magnitude; }, meio);
        }
        public void Poli(float w, params float[] pts) { for (int i = 0; i + 3 < pts.Length; i += 2) Linha(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], w); }
        public void Disco(float cx, float cy, float r) { Vector2 c = new Vector2(K(cx), K(cy)); float rr = K(r); Aplicar((x, y) => Vector2.Distance(new Vector2(x, y), c), rr); }
        public void Anel(float cx, float cy, float r, float w) { Vector2 c = new Vector2(K(cx), K(cy)); float rr = K(r), meio = K(w) * 0.5f; Aplicar((x, y) => Mathf.Abs(Vector2.Distance(new Vector2(x, y), c) - rr), meio); }
        // arco entre dois ângulos em graus (0 = direita, cresce no sentido horário na imagem)
        public void Arco(float cx, float cy, float r, float w, float graus0, float graus1)
        {
            Vector2 c = new Vector2(K(cx), K(cy)); float rr = K(r), meio = K(w) * 0.5f, a0 = graus0 * Mathf.Deg2Rad, a1 = graus1 * Mathf.Deg2Rad;
            Vector2 p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rr, p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rr;
            Aplicar((x, y) =>
            {
                Vector2 v = new Vector2(x, y) - c; float ang = Mathf.Atan2(v.y, v.x);
                float lo = Mathf.Min(a0, a1), hi = Mathf.Max(a0, a1);
                float t = ang; while (t < lo) t += 2f * Mathf.PI; while (t > lo + 2f * Mathf.PI) t -= 2f * Mathf.PI;
                if (t <= hi) return Mathf.Abs(v.magnitude - rr);
                return Mathf.Min(Vector2.Distance(new Vector2(x, y), p0), Vector2.Distance(new Vector2(x, y), p1));
            }, meio);
        }
    }

    static Texture2D Gerar(ClasseDeHabilidade classe, int i)
    {
        Pintor p = new Pintor();
        switch (classe)
        {
            case ClasseDeHabilidade.Cavaleiro:
                if (i == 0)       // espada de pé com riscos de corte
                {
                    p.Linha(50, 8, 50, 62, 10); p.Linha(50, 4, 50, 12, 5); p.Linha(33, 65, 67, 65, 7); p.Linha(50, 67, 50, 85, 7); p.Disco(50, 90, 5);
                    p.Linha(74, 18, 84, 38, 3.5f); p.Linha(26, 18, 16, 38, 3.5f);
                }
                else if (i == 1)  // bota chutando
                {
                    p.Linha(38, 16, 44, 56, 20); p.Linha(42, 63, 78, 67, 16); p.Disco(38, 66, 9);
                    p.Linha(84, 38, 96, 28, 4); p.Linha(88, 55, 99, 55, 4); p.Linha(84, 72, 96, 82, 4);
                }
                else              // espada cravada no chão
                {
                    p.Linha(50, 8, 50, 20, 6); p.Linha(32, 22, 68, 22, 7); p.Linha(50, 25, 50, 72, 10); p.Linha(50, 70, 50, 80, 5); p.Linha(14, 86, 86, 86, 5); p.Arco(50, 86, 30, 3.5f, 205, 335);
                }
                break;
            case ClasseDeHabilidade.Tanque:
                if (i == 0)       // grito: "!" com ondas
                {
                    p.Linha(50, 18, 50, 54, 10); p.Disco(50, 73, 6);
                    p.Arco(50, 46, 34, 4, -38, 38); p.Arco(50, 46, 47, 4, -34, 34); p.Arco(50, 46, 34, 4, 142, 218); p.Arco(50, 46, 47, 4, 146, 214);
                }
                else if (i == 1)  // chão rachando
                {
                    p.Poli(5.5f, 6, 58, 28, 46, 40, 66, 58, 42, 72, 62, 94, 48);
                    p.Disco(22, 26, 4); p.Disco(50, 20, 5); p.Disco(78, 28, 4); p.Disco(64, 82, 4); p.Disco(30, 84, 5);
                }
                else              // escudo com raio
                {
                    p.Poli(6, 50, 8, 86, 20, 82, 58, 50, 92, 18, 58, 14, 20, 50, 8);
                    p.Poli(6.5f, 57, 24, 40, 52, 55, 52, 43, 78);
                }
                break;
            case ClasseDeHabilidade.Maga:
                if (i == 0)       // raio caindo
                {
                    p.Poli(10, 64, 4, 38, 48, 58, 48, 34, 94); p.Poli(4.5f, 24, 28, 14, 48); p.Poli(4.5f, 80, 34, 88, 52); p.Disco(78, 80, 4); p.Disco(18, 74, 3.5f);
                }
                else if (i == 1) // corte mágico em arco
                {
                    p.Arco(26, 80, 52, 12, -82, -14); p.Arco(26, 80, 37, 4.5f, -76, -22); p.Linha(14, 92, 5, 98, 4.5f); p.Disco(80, 18, 4); p.Disco(90, 36, 3);
                }
                else              // círculo mágico
                {
                    p.Anel(50, 50, 42, 5); p.Anel(50, 50, 27, 3.5f);
                    p.Linha(50, 6, 50, 94, 3); p.Linha(6, 50, 94, 50, 3); p.Linha(19, 19, 81, 81, 3); p.Linha(81, 19, 19, 81, 3); p.Disco(50, 50, 7);
                }
                break;
            default:
                if (i == 0)       // cura: cruz brilhante
                {
                    p.Linha(50, 20, 50, 80, 17); p.Linha(20, 50, 80, 50, 17); p.Disco(82, 18, 4.5f); p.Disco(18, 82, 3.5f); p.Disco(84, 78, 3);
                }
                else if (i == 1) // reforço: setas para cima
                {
                    p.Poli(10, 20, 54, 50, 28, 80, 54); p.Poli(10, 20, 80, 50, 54, 80, 80); p.Disco(50, 10, 4.5f);
                }
                else              // área de cura contínua
                {
                    p.Anel(50, 50, 42, 5); p.Linha(50, 28, 50, 72, 12); p.Linha(28, 50, 72, 50, 12);
                    for (int k = 0; k < 8; k++) { float a = k * 45f * Mathf.Deg2Rad; p.Disco(50 + Mathf.Cos(a) * 33f, 50 + Mathf.Sin(a) * 33f, 2.4f); }
                }
                break;
        }
        Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) px[(N - 1 - y) * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(p.a[y * N + x]) * 255f));
        t.SetPixels32(px); t.Apply(true); return t;
    }
}
