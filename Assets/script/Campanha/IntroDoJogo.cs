using System;
using System.Collections.Generic;
using UnityEngine;

// ABERTURA ANIMADA DO JOGO: roda assim que o jogo começa, ANTES da seleção de personagens.
// Conta a história da ilha, do dragão e dos três cristais em cenas desenhadas por código (silhuetas, céu, brilhos, partículas),
// com câmera lenta, narração letra por letra e barras de cinema.
// Enter / Espaço / clique: avança a fala.  Esc: pula tudo.
public class IntroDoJogo : MonoBehaviour
{
    public static bool Ativa { get; private set; }
    public static bool JaViu { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { Ativa = false; JaViu = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar()
    {
        if (FindFirstObjectByType<MenuRede>() == null) return;                 // só nas cenas que têm a seleção de personagens
        GameObject g = new GameObject("Abertura do jogo"); g.AddComponent<IntroDoJogo>();
    }

    // ------------------------------------------------------------------ roteiro
    class Cena { public float dur; public string texto; public Action<float> desenha; public float z0 = 1f, z1 = 1.12f; public Vector2 p0, p1; }
    List<Cena> cenas;
    int atual; float inicioCena, fimGeral = -1f; bool digitado;
    const float VEL = 26f;               // letras por segundo
    float inicioTudo;
    Font fCinzel, fFell;

    void Awake()
    {
        Ativa = true; inicioTudo = Time.unscaledTime;
        fCinzel = Resources.Load<Font>("Fontes/CinzelBold"); fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
        Montar();
        atual = 0; inicioCena = Time.unscaledTime + 0.4f;
    }

    void Montar()
    {
        cenas = new List<Cena>
        {
            new Cena { texto = "Valverde... uma pequena ilha esquecida entre as brumas do mar.", desenha = CenaIlha, z0 = 1f, z1 = 1.25f, p0 = new Vector2(0, 30), p1 = new Vector2(-40, -10) },
            new Cena { texto = "Há muito tempo, um dragão colossal despertou e incendiou as vilas da ilha, uma por uma.", desenha = CenaDragao, z0 = 1f, z1 = 1.18f, p0 = new Vector2(40, 0), p1 = new Vector2(-40, 10) },
            new Cena { texto = "Um herói enfrentou a fera, mas nenhum aço foi capaz de matá-la.", desenha = CenaHeroi, z0 = 1.05f, z1 = 1.22f, p0 = new Vector2(-30, 10), p1 = new Vector2(20, -10) },
            new Cena { texto = "Então ele dividiu o poder do dragão em três cristais e os selou, bem longe uns dos outros.", desenha = CenaCristais, z0 = 1f, z1 = 1.12f, p0 = Vector2.zero, p1 = new Vector2(0, -20) },
            new Cena { texto = "Um deles ficou no cemitério, vigiado pelo Guardião das Lápides.", desenha = CenaCemiterio, z0 = 1f, z1 = 1.15f, p0 = new Vector2(50, 0), p1 = new Vector2(-50, 0) },
            new Cena { texto = "Outro, no castelo caído, onde um velho cavaleiro ainda faz a ronda.", desenha = CenaCastelo, z0 = 1.1f, z1 = 1f, p0 = new Vector2(-40, -10), p1 = new Vector2(30, 10) },
            new Cena { texto = "E o último, no templo do pântano, sob as águas escuras.", desenha = CenaPantano, z0 = 1f, z1 = 1.15f, p0 = new Vector2(40, 10), p1 = new Vector2(-30, -10) },
            new Cena { texto = "Séculos se passaram... até que uma magia maligna começou a corromper os cristais.", desenha = CenaCorrupcao, z0 = 1f, z1 = 1.2f, p0 = Vector2.zero, p1 = new Vector2(0, 10) },
            new Cena { texto = "Os mortos acordaram, e a vila de Valverde prometeu uma grande recompensa a quem trouxesse a paz de volta.", desenha = CenaMortos, z0 = 1f, z1 = 1.1f, p0 = new Vector2(70, 0), p1 = new Vector2(-70, 0) },
            new Cena { texto = "E é aí que chegam alguns aventureiros... sem um tostão no bolso.", desenha = CenaAventureiros, z0 = 1f, z1 = 1.12f, p0 = new Vector2(0, 20), p1 = new Vector2(0, -10) },
        };
        foreach (Cena c in cenas) c.dur = Mathf.Max(7f, c.texto.Length / VEL + 5f);
        cenas[0].dur += 3f; cenas[cenas.Count - 1].dur += 3f;
    }

    // ------------------------------------------------------------------ controle
    void Update()
    {
        if (fimGeral >= 0f) { if (Time.unscaledTime - fimGeral > 1.1f) Destroy(gameObject); return; }
        if (!Ativa) return;
        SessaoDeJogo.MenuAberto = true;
        if (Input.GetKeyDown(KeyCode.Escape)) { Terminar(); return; }
        float t = Time.unscaledTime - inicioCena; if (t < 0f) return;
        Cena c = cenas[atual];
        digitado = t * VEL >= c.texto.Length + 6f;
        bool avanca = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Comandos.NavConfirmar();
        if (avanca && t > 0.8f)
        {
            if (!digitado) inicioCena = Time.unscaledTime - (c.texto.Length + 6f) / VEL - 0.01f;      // mostra a fala inteira
            else Proxima();
            return;
        }
        if (t > c.dur) Proxima();
    }

    void Proxima()
    {
        atual++; inicioCena = Time.unscaledTime;
        if (atual >= cenas.Count) Terminar();
    }

    void Terminar()
    {
        if (fimGeral >= 0f) return;
        fimGeral = Time.unscaledTime; JaViu = true; Ativa = false;                // a seleção de personagens já pode receber o teclado
        encerrando = true;
    }
    bool encerrando;

    // ------------------------------------------------------------------ desenho (GL)
    Material matA, matAdd; bool modoAdd, glPronto; float sc, gz = 1f, gA = 1f; Vector2 gpan; float tempo;

    void PrepararGL()
    {
        if (matA == null)
        {
            Shader sh = Shader.Find("Hidden/Internal-Colored"); if (sh == null) sh = Shader.Find("UI/Default"); if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) { glPronto = false; return; }
            matA = new Material(sh) { hideFlags = HideFlags.HideAndDontSave }; matAdd = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            foreach (Material m in new[] { matA, matAdd })
            {
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); m.SetInt("_ZWrite", 0); m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            matA.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); matAdd.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }
        glPronto = true; modoAdd = false; matA.SetPass(0);
        GL.PushMatrix(); GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
    }
    void FimGL() { if (glPronto) GL.PopMatrix(); }
    void Modo(bool add) { if (!glPronto || add == modoAdd) return; modoAdd = add; (add ? matAdd : matA).SetPass(0); }

    static float Fr(float x) { return x - Mathf.Floor(x); }
    static float Rd(int i, int s) { return Fr(Mathf.Sin(i * 12.9898f + s * 78.233f) * 43758.5453f); }
    static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    static Color C(float r, float g, float b, float a = 1f) { return new Color(r, g, b, a); }

    Vector2 T(Vector2 p, float par)
    {
        float z = Mathf.Lerp(1f, gz, 0.2f + 0.8f * par);
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + ((p - new Vector2(960f, 540f)) * z + gpan * par) * sc;
    }
    float Lz(float len, float par) { return len * sc * Mathf.Lerp(1f, gz, 0.2f + 0.8f * par); }
    Color Ga(Color c) { c.a *= gA; return c; }
    static void V(Vector2 p) { GL.Vertex3(p.x, p.y, 0f); }

    void Tri(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
    {
        if (!glPronto) return;
        GL.Begin(GL.TRIANGLES); GL.Color(Ga(ca)); V(a); GL.Color(Ga(cb)); V(b); GL.Color(Ga(cc)); V(c); GL.End();
    }
    void QuadG(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
    {
        if (!glPronto) return;
        GL.Begin(GL.TRIANGLES);
        GL.Color(Ga(ca)); V(a); GL.Color(Ga(cb)); V(b); GL.Color(Ga(cc)); V(c);
        GL.Color(Ga(ca)); V(a); GL.Color(Ga(cc)); V(c); GL.Color(Ga(cd)); V(d);
        GL.End();
    }
    // retângulo com degradê vertical (coordenadas virtuais)
    void Grad(float x0, float y0, float x1, float y1, Color topo, Color base_, float par = 0f)
    {
        QuadG(T(new Vector2(x0, y0), par), T(new Vector2(x1, y0), par), T(new Vector2(x1, y1), par), T(new Vector2(x0, y1), par), topo, topo, base_, base_);
    }
    void Poli(IList<Vector2> pts, Color c, float par, Vector2 pos = default(Vector2), float escala = 1f, bool espelha = false)
    {
        if (!glPronto || pts.Count < 3) return;
        var s = new Vector2[pts.Count]; Vector2 cen = Vector2.zero;
        for (int i = 0; i < pts.Count; i++) { Vector2 p = pts[i]; if (espelha) p.x = -p.x; s[i] = T(pos + p * escala, par); cen += s[i]; }
        cen /= s.Length;
        GL.Begin(GL.TRIANGLES); GL.Color(Ga(c));
        for (int i = 0; i < s.Length; i++) { V(cen); V(s[i]); V(s[(i + 1) % s.Length]); }
        GL.End();
    }
    void Relevo(float x0, float dx, float[] ys, float baseY, Color c, float par)
    {
        if (!glPronto) return;
        GL.Begin(GL.TRIANGLES); GL.Color(Ga(c));
        for (int i = 0; i + 1 < ys.Length; i++)
        {
            Vector2 a = T(new Vector2(x0 + i * dx, ys[i]), par), b = T(new Vector2(x0 + (i + 1) * dx, ys[i + 1]), par), a2 = T(new Vector2(x0 + i * dx, baseY), par), b2 = T(new Vector2(x0 + (i + 1) * dx, baseY), par);
            V(a); V(b); V(b2); V(a); V(b2); V(a2);
        }
        GL.End();
    }
    void Linha(Vector2 a, Vector2 b, float w, Color c, float par)
    {
        if (!glPronto) return;
        Vector2 pa = T(a, par), pb = T(b, par), d = pb - pa; if (d.sqrMagnitude < 0.01f) return; d.Normalize();
        Vector2 n = new Vector2(-d.y, d.x) * Lz(w, par) * 0.5f;
        GL.Begin(GL.TRIANGLES); GL.Color(Ga(c));
        V(pa - n); V(pa + n); V(pb + n); V(pa - n); V(pb + n); V(pb - n); GL.End();
    }
    void Disco(Vector2 c, float r, Color col, float par, int seg = 22)
    {
        if (!glPronto) return;
        Vector2 pc = T(c, par); float rr = Lz(r, par);
        GL.Begin(GL.TRIANGLES); GL.Color(Ga(col));
        for (int i = 0; i < seg; i++)
        {
            float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
            V(pc); V(pc + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rr); V(pc + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rr);
        }
        GL.End();
    }
    // brilho macio (centro forte, borda transparente), somando luz
    void Brilho(Vector2 c, float r, Color col, float par, float ry = -1f)
    {
        if (!glPronto) return;
        Modo(true);
        Vector2 pc = T(c, par); float rx = Lz(r, par), rY = ry < 0f ? rx : Lz(ry, par);
        Color borda = col; borda.a = 0f; const int N = 28;
        GL.Begin(GL.TRIANGLES);
        for (int i = 0; i < N; i++)
        {
            float a0 = i / (float)N * Mathf.PI * 2f, a1 = (i + 1) / (float)N * Mathf.PI * 2f;
            GL.Color(Ga(col)); V(pc); GL.Color(Ga(borda)); V(pc + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * rY)); V(pc + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * rY));
        }
        GL.End(); Modo(false);
    }
    static List<Vector2> Elipse(float rx, float ry, int n = 18) { var l = new List<Vector2>(); for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2f; l.Add(new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry)); } return l; }
    static Vector2[] V2(params float[] xy) { var l = new Vector2[xy.Length / 2]; for (int i = 0; i < l.Length; i++) l[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]); return l; }

    // ---- peças de cenário reaproveitadas
    void Ceu(Color topo, Color meio, Color base_, float y1 = 760f)
    {
        Grad(-800f, -600f, 2720f, 380f, topo, meio); Grad(-800f, 380f, 2720f, y1, meio, base_);
    }
    void Estrelas(int n, float yMax, float par, float bril = 1f)
    {
        for (int i = 0; i < n; i++)
        {
            float x = Rd(i, 1) * 2200f - 140f, y = Rd(i, 2) * yMax, tw = 0.45f + 0.55f * Mathf.Sin(tempo * (1.2f + Rd(i, 3) * 2.2f) + i);
            float r = 1.1f + Rd(i, 4) * 1.8f; Disco(new Vector2(x, y), r, C(1f, 0.97f, 0.88f, 0.75f * tw * bril), par, 6);
        }
    }
    void Lua(Vector2 c, float r, float par, Color corLua, Color halo)
    {
        Brilho(c, r * 3.6f, halo, par); Disco(c, r, corLua, par, 36);
        Disco(c + new Vector2(-r * 0.3f, -r * 0.2f), r * 0.22f, C(0.8f, 0.82f, 0.85f, 0.35f), par); Disco(c + new Vector2(r * 0.28f, r * 0.3f), r * 0.3f, C(0.78f, 0.8f, 0.84f, 0.3f), par);
    }
    void Nevoa(float y, float altura, Color cor, float par, float vel, int semente)
    {
        for (int i = 0; i < 5; i++)
        {
            float x = Mathf.Repeat(Rd(i, semente) * 2600f + tempo * vel * (0.6f + Rd(i, semente + 1)), 2800f) - 440f;
            Brilho(new Vector2(x, y + Rd(i, semente + 2) * 40f), 380f, cor, par, altura);
        }
        Modo(false);
    }
    void Brasas(int n, Vector2 origem, float largura, float altura, Color cor, float par, int sem)
    {
        for (int i = 0; i < n; i++)
        {
            float ph = Fr(tempo * (0.18f + Rd(i, sem) * 0.25f) + Rd(i, sem + 1)), x = origem.x + (Rd(i, sem + 2) - 0.5f) * largura + Mathf.Sin(ph * 7f + i) * 22f;
            float y = origem.y - ph * altura; float a = Mathf.Sin(ph * Mathf.PI);
            Brilho(new Vector2(x, y), 7f + Rd(i, sem + 3) * 7f, new Color(cor.r, cor.g, cor.b, cor.a * a), par);
        }
    }
    void Luzinhas(int n, Rect area, Color cor, float par, int sem)
    {
        for (int i = 0; i < n; i++)
        {
            float x = area.x + Rd(i, sem) * area.width + Mathf.Sin(tempo * 0.5f + i * 1.7f) * 30f, y = area.y + Rd(i, sem + 1) * area.height + Mathf.Cos(tempo * 0.6f + i) * 20f;
            float pisca = 0.5f + 0.5f * Mathf.Sin(tempo * (1.5f + Rd(i, sem + 2)) + i * 3f);
            Brilho(new Vector2(x, y), 14f, new Color(cor.r, cor.g, cor.b, cor.a * pisca), par);
        }
    }
    static float[] Serra(int n, float baseY, float amp, int sem, float suavidade = 0.55f)
    {
        var ys = new float[n]; for (int i = 0; i < n; i++) ys[i] = baseY - amp * (Mathf.PerlinNoise(i * suavidade + sem * 7.3f, sem * 1.7f) * 0.8f + Mathf.PerlinNoise(i * suavidade * 2.3f, sem) * 0.2f); return ys;
    }
    void Cristal(Vector2 c, float s, Color cor, float par, float giro = 0f)
    {
        float k = 1f + 0.06f * Mathf.Sin(tempo * 3f + c.x);
        Brilho(c, s * 3.2f * k, new Color(cor.r, cor.g, cor.b, 0.6f), par);
        Vector2[] forma = V2(0, -1.5f, 0.62f, -0.4f, 0.5f, 0.9f, 0, 1.5f, -0.5f, 0.9f, -0.62f, -0.4f);
        var pts = new List<Vector2>(); foreach (Vector2 p in forma) { float ca = Mathf.Cos(giro), sa = Mathf.Sin(giro); pts.Add(new Vector2(p.x * ca - p.y * sa, p.x * sa + p.y * ca) * s); }
        Poli(pts, new Color(cor.r * 0.8f, cor.g * 0.8f, cor.b * 0.8f, 0.95f), par, c);
        var luz = new List<Vector2>(); foreach (Vector2 p in V2(0, -1.5f, 0.62f, -0.4f, 0.05f, 0.1f, 0, 0.2f)) luz.Add(p * s);
        Poli(luz, new Color(Mathf.Min(1f, cor.r + 0.4f), Mathf.Min(1f, cor.g + 0.4f), Mathf.Min(1f, cor.b + 0.4f), 0.85f), par, c);
        Brilho(c + new Vector2(-s * 0.2f, -s * 0.5f), s * 0.5f, new Color(1f, 1f, 1f, 0.5f), par);
    }

    // ---- personagens em silhueta
    void Dragao(Vector2 c, float s, float fase, bool direita, Color cor, float par)
    {
        float f = direita ? 1f : -1f; bool esp = !direita;
        float asa = Mathf.Sin(fase) * 0.75f;
        // asa de trás
        Poli(RodarAsa(asa - 0.18f), new Color(cor.r * 0.8f, cor.g * 0.8f, cor.b * 0.8f, cor.a), par, c + new Vector2(20f * f, -30f) * s, s, esp);
        Poli(Elipse(95f, 40f), cor, par, c, s, esp);
        Poli(V2(-80, -20, -170, -10, -250, 12, -330, 42, -395, 36, -340, 55, -255, 38, -170, 20, -80, 24), cor, par, c, s, esp);       // cauda
        Poli(V2(-395, 36, -440, 10, -420, 56), cor, par, c, s, esp);                                                                 // ponta da cauda
        Poli(V2(60, -14, 115, -50, 165, -78, 205, -80, 245, -66, 205, -52, 172, -42, 125, -6), cor, par, c, s, esp);                   // pescoço e cabeça
        Poli(V2(172, -80, 150, -122, 190, -84), cor, par, c, s, esp);                                                                 // chifre
        Poli(V2(110, 20, 135, 62, 118, 64, 90, 28), cor, par, c, s, esp);                                                             // pata
        Poli(V2(-40, 28, -15, 72, -34, 74, -64, 30), cor, par, c, s, esp);
        Poli(RodarAsa(asa), cor, par, c + new Vector2(10f * f, -34f) * s, s, esp);
        Brilho(c + new Vector2(208f * f, -66f) * s, 14f * s, C(1f, 0.55f, 0.1f, 0.95f), par);
    }
    List<Vector2> RodarAsa(float ang)
    {
        Vector2[] w = V2(0, 0, -45, -120, -130, -215, -270, -250, -205, -178, -235, -120, -160, -105, -175, -50, -95, -50, -40, -20);
        var l = new List<Vector2>(); float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
        foreach (Vector2 p in w) { Vector2 q = new Vector2(p.x * ca - p.y * sa, p.x * sa + p.y * ca); l.Add(new Vector2(-q.x * 0.0f + q.x, q.y)); }
        return l;
    }

    void Heroi(Vector2 pe, float h, bool espadaAlta, Color cor, float par, float vento = 0f, bool deCostas = false)
    {
        float step = 0f;
        Linha(pe + new Vector2(-0.07f, -0.45f) * h, pe + new Vector2(-0.1f - step, 0f), h * 0.07f, cor, par); Linha(pe + new Vector2(0.07f, -0.45f) * h, pe + new Vector2(0.11f + step, 0f), h * 0.07f, cor, par);
        Poli(V2(-0.13f, -0.46f, 0.13f, -0.46f, 0.18f, -0.8f, -0.18f, -0.8f), cor, par, pe, h);
        Poli(Elipse(0.085f, 0.085f, 12), cor, par, pe + new Vector2(0f, -0.9f) * h, h);
        Poli(V2(-0.16f, -0.78f, -0.4f - vento * 0.25f, -0.55f + vento * 0.05f, -0.55f - vento * 0.35f, -0.2f, -0.3f - vento * 0.1f, -0.1f, -0.12f, -0.4f), new Color(cor.r + 0.03f, cor.g, cor.b + 0.05f, cor.a), par, pe, h);   // capa ao vento
        if (espadaAlta)
        {
            Linha(pe + new Vector2(0.14f, -0.74f) * h, pe + new Vector2(0.3f, -1.05f) * h, h * 0.05f, cor, par);
            Linha(pe + new Vector2(0.3f, -1.05f) * h, pe + new Vector2(0.4f, -1.55f) * h, h * 0.035f, C(0.85f, 0.9f, 1f, 0.95f), par);
            Brilho(pe + new Vector2(0.38f, -1.45f) * h, h * 0.4f, C(0.7f, 0.85f, 1f, 0.6f), par);
        }
        else Linha(pe + new Vector2(0.12f, -0.7f) * h, pe + new Vector2(0.22f, -0.35f) * h, h * 0.05f, cor, par);
    }

    // aventureiros caminhando: 0 cavaleiro, 1 maga, 2 tanque, 3 curandeiro
    void Viajante(Vector2 pe, float h, int tipo, Color cor, float par, float passo)
    {
        float bob = Mathf.Abs(Mathf.Sin(passo)) * h * 0.03f; pe.y -= bob;
        float sw = Mathf.Sin(passo) * 0.09f;
        Linha(pe + new Vector2(-0.06f, -0.42f) * h, pe + new Vector2(-0.06f + sw, 0f), h * 0.07f, cor, par); Linha(pe + new Vector2(0.06f, -0.42f) * h, pe + new Vector2(0.06f - sw, 0f), h * 0.07f, cor, par);
        float larg = tipo == 2 ? 0.22f : 0.16f;
        Poli(V2(-larg * 0.8f, -0.42f, larg * 0.8f, -0.42f, larg, -0.78f, -larg, -0.78f), cor, par, pe, h);
        if (tipo == 1) Poli(V2(-0.2f, -0.78f, 0.2f, -0.78f, 0.28f, -0.1f, -0.28f, -0.1f), cor, par, pe, h);          // manto
        Poli(Elipse(0.08f, 0.085f, 12), cor, par, pe + new Vector2(0f, -0.88f) * h, h);
        if (tipo == 1) Poli(V2(-0.1f, -0.88f, 0f, -1.12f, 0.1f, -0.88f), cor, par, pe, h);                                // capuz
        if (tipo == 0) { Linha(pe + new Vector2(0.05f, -0.97f) * h, pe + new Vector2(-0.08f, -1.1f) * h, h * 0.04f, cor, par); Linha(pe + new Vector2(0.18f, -0.75f) * h, pe + new Vector2(0.22f, -1.1f) * h, h * 0.03f, cor, par); }
        if (tipo == 2) Poli(Elipse(0.17f, 0.24f, 14), new Color(cor.r + 0.05f, cor.g + 0.05f, cor.b + 0.05f, cor.a), par, pe + new Vector2(-0.22f, -0.55f) * h, h);
        if (tipo == 1 || tipo == 3)
        {
            Linha(pe + new Vector2(0.2f, -0.05f) * h, pe + new Vector2(0.22f, -1.15f) * h, h * 0.03f, cor, par);
            Brilho(pe + new Vector2(0.22f, -1.18f) * h, h * 0.3f, tipo == 1 ? C(0.6f, 0.4f, 1f, 0.7f) : C(0.4f, 1f, 0.55f, 0.7f), par);
        }
    }

    void Esqueleto(Vector2 pe, float h, float braco, Color cor, float par, float olho = 1f)
    {
        Poli(Elipse(0.075f, 0.085f, 10), cor, par, pe + new Vector2(0f, -0.92f) * h, h);
        Linha(pe + new Vector2(0f, -0.82f) * h, pe + new Vector2(0f, -0.45f) * h, h * 0.035f, cor, par);
        for (int i = 0; i < 3; i++) Linha(pe + new Vector2(-0.1f, -0.74f + i * 0.08f) * h, pe + new Vector2(0.1f, -0.74f + i * 0.08f) * h, h * 0.03f, cor, par);
        Linha(pe + new Vector2(0.08f, -0.76f) * h, pe + new Vector2(0.22f, -0.76f + braco * 0.55f) * h, h * 0.03f, cor, par); Linha(pe + new Vector2(-0.08f, -0.76f) * h, pe + new Vector2(-0.22f, -0.55f) * h, h * 0.03f, cor, par);
        Linha(pe + new Vector2(0f, -0.45f) * h, pe + new Vector2(-0.07f, 0f), h * 0.035f, cor, par); Linha(pe + new Vector2(0f, -0.45f) * h, pe + new Vector2(0.08f, 0f), h * 0.035f, cor, par);
        Brilho(pe + new Vector2(-0.03f, -0.93f) * h, h * 0.07f, C(1f, 0.55f, 0.1f, 0.9f * olho), par); Brilho(pe + new Vector2(0.03f, -0.93f) * h, h * 0.07f, C(1f, 0.55f, 0.1f, 0.9f * olho), par);
    }

    void Arvore(Vector2 b, float h, Color cor, float par, int sem)
    {
        Galho(b, -Mathf.PI * 0.5f, h, h * 0.045f, 5, cor, par, sem);
    }
    void Galho(Vector2 a, float ang, float len, float larg, int nivel, Color cor, float par, int sem)
    {
        Vector2 b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
        Linha(a, b, larg, cor, par);
        if (nivel <= 0) return;
        float vento = Mathf.Sin(tempo * 0.7f + sem) * 0.03f;
        Galho(b, ang - 0.45f + vento + (Rd(nivel, sem) - 0.5f) * 0.3f, len * 0.72f, larg * 0.7f, nivel - 1, cor, par, sem + 3);
        Galho(b, ang + 0.5f + vento + (Rd(nivel, sem + 1) - 0.5f) * 0.3f, len * 0.68f, larg * 0.7f, nivel - 1, cor, par, sem + 5);
        if (nivel > 3) Galho(a + (b - a) * 0.55f, ang + 0.9f, len * 0.5f, larg * 0.5f, nivel - 2, cor, par, sem + 9);
    }

    // ================================================================== AS CENAS
    // t = progresso da cena (0 a 1)
    void CenaIlha(float k)
    {
        Ceu(C(0.015f, 0.02f, 0.07f), C(0.07f, 0.06f, 0.18f), C(0.22f, 0.14f, 0.3f), 700f);
        Estrelas(130, 520f, 0.0f);
        Lua(new Vector2(1420, 230), 78f, 0.05f, C(0.93f, 0.93f, 0.85f), C(0.5f, 0.55f, 0.8f, 0.35f));
        Nevoa(620f, 90f, C(0.5f, 0.5f, 0.7f, 0.12f), 0.15f, 8f, 1);
        // mar
        Grad(-800f, 660f, 2720f, 1500f, C(0.07f, 0.1f, 0.2f), C(0.01f, 0.02f, 0.05f), 0.2f);
        for (int i = 0; i < 22; i++)
        {
            float y = 680f + i * i * 1.5f + i * 14f; float amp = 6f + i * 0.8f, off = Mathf.Sin(tempo * 0.6f + i) * 40f;
            Linha(new Vector2(-300f + off, y), new Vector2(2300f + off, y + Mathf.Sin(tempo + i) * amp * 0.3f), 1.5f + i * 0.14f, C(0.45f, 0.55f, 0.85f, 0.06f + 0.003f * i), 0.25f);
        }
        for (int i = 0; i < 16; i++)                                              // reflexo da lua
            Linha(new Vector2(1420f + Mathf.Sin(tempo * 2f + i) * (10f + i * 3f) - 40f - i * 2f, 690f + i * 22f), new Vector2(1420f + Mathf.Sin(tempo * 2f + i) * (10f + i * 3f) + 40f + i * 2f, 690f + i * 22f), 4f, C(0.9f, 0.92f, 0.85f, 0.25f - i * 0.013f), 0.25f);
        // a ilha
        float[] ilha = new float[40]; for (int i = 0; i < 40; i++) { float u = i / 39f; float env = Mathf.Sin(u * Mathf.PI); ilha[i] = 700f - env * (150f + 90f * Mathf.PerlinNoise(i * 0.35f, 4.2f)) + 6f * Mathf.PerlinNoise(i * 1.7f, 1f); }
        Relevo(430f, 21f, ilha, 800f, C(0.015f, 0.02f, 0.04f), 0.35f);
        // lugares com luzes: cemitério (roxo), castelo (azul), pântano (verde), vila (amarelo)
        Cristal(new Vector2(560f, 598f), 6f, C(0.8f, 0.45f, 1f), 0.35f); Cristal(new Vector2(790f, 548f), 6f, C(0.4f, 0.7f, 1f), 0.35f); Cristal(new Vector2(1120f, 608f), 6f, C(0.35f, 1f, 0.6f), 0.35f);
        for (int i = 0; i < 9; i++) Disco(new Vector2(900f + i * 11f + Rd(i, 3) * 6f, 622f + Rd(i, 4) * 8f), 2.6f, C(1f, 0.8f, 0.4f, 0.9f), 0.35f, 6);
        Brilho(new Vector2(940f, 625f), 70f, C(1f, 0.7f, 0.3f, 0.25f), 0.35f);
        Nevoa(750f, 70f, C(0.45f, 0.5f, 0.7f, 0.1f), 0.4f, -10f, 5);
        // título
        float tt = Suave((k * 28f - 3f) / 4f);
        if (tt > 0f) { Titulo("V A L V E R D E", 190f, 150f, tt); }
    }

    void CenaDragao(float k)
    {
        Ceu(C(0.18f, 0.02f, 0.03f), C(0.55f, 0.12f, 0.05f), C(1f, 0.55f, 0.15f), 760f);
        Brilho(new Vector2(960f, 600f), 700f, C(1f, 0.55f, 0.15f, 0.55f), 0.1f, 380f);
        Disco(new Vector2(960f, 470f), 150f, C(1f, 0.82f, 0.45f, 0.9f), 0.1f, 40);
        for (int i = 0; i < 7; i++) Brilho(new Vector2(Mathf.Repeat(Rd(i, 8) * 2600f + tempo * (6f + i * 2f), 2800f) - 400f, 160f + i * 70f), 330f, C(0.12f, 0.03f, 0.03f, 0.55f), 0.2f, 70f);   // fumaça
        Modo(false);
        // o dragão atravessa o sol
        float u = Suave(k * 1.15f - 0.05f); Vector2 pos = new Vector2(Mathf.Lerp(-380f, 2250f, u), 360f + Mathf.Sin(tempo * 0.9f) * 24f - u * 40f);
        Dragao(pos, 2.2f, tempo * 3.4f, true, C(0.03f, 0.01f, 0.01f, 0.97f), 0.4f);
        // vilas em chamas
        for (int v = 0; v < 4; v++)
        {
            float bx = 120f + v * 520f;
            for (int i = 0; i < 6; i++)
            {
                float hx = bx + i * 70f + Rd(i, v) * 20f, hh = 70f + Rd(i, v + 4) * 60f;
                Poli(V2(-34, 0, -34, -hh, 0, -hh - 36, 34, -hh, 34, 0), C(0.02f, 0.005f, 0.005f), 0.6f, new Vector2(hx, 830f));
                Brilho(new Vector2(hx, 830f - hh * 0.7f), 60f, C(1f, 0.45f, 0.1f, 0.35f + 0.15f * Mathf.Sin(tempo * 9f + i + v)), 0.6f);
                for (int f = 0; f < 2; f++) { float fl = Mathf.Sin(tempo * 11f + i * 2f + f * 3f); Poli(V2(-14, 0, -8, -50 - fl * 8f, 0, -86 - fl * 14f, 8, -48 + fl * 6f, 14, 0), C(1f, 0.55f + 0.2f * fl, 0.1f, 0.9f), 0.62f, new Vector2(hx - 12f + f * 24f, 830f - hh - 20f)); }
            }
        }
        Grad(-800f, 820f, 2720f, 1300f, C(0.03f, 0.01f, 0.01f), C(0.0f, 0f, 0f), 0.7f);
        Brasas(60, new Vector2(960f, 900f), 2200f, 900f, C(1f, 0.6f, 0.15f, 0.9f), 0.8f, 21);
    }

    void CenaHeroi(float k)
    {
        // relâmpagos
        float lamp = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(tempo * 2.3f) * Mathf.Sin(tempo * 5.1f + 1f)), 6f);
        Ceu(C(0.05f + lamp * 0.35f, 0.06f + lamp * 0.35f, 0.1f + lamp * 0.45f), C(0.14f + lamp * 0.3f, 0.16f + lamp * 0.3f, 0.24f + lamp * 0.35f), C(0.28f + lamp * 0.3f, 0.26f + lamp * 0.25f, 0.34f + lamp * 0.3f), 860f);
        for (int i = 0; i < 6; i++) Brilho(new Vector2(Mathf.Repeat(Rd(i, 2) * 2600f + tempo * 20f, 2800f) - 400f, 120f + i * 90f), 420f, C(0.07f, 0.08f, 0.12f, 0.6f), 0.15f, 90f);
        Modo(false);
        if (lamp > 0.1f) { Vector2 a = new Vector2(1350f, -50f); for (int i = 0; i < 8; i++) { Vector2 b = a + new Vector2((Rd(i, 31) - 0.5f) * 140f, 95f); Linha(a, b, 7f, C(1f, 1f, 1f, lamp), 0.2f); Brilho(b, 90f, C(0.7f, 0.8f, 1f, 0.35f * lamp), 0.2f); a = b; } }
        // o dragão grandão, ameaçador, e seu fogo
        Vector2 dpos = new Vector2(1420f + Mathf.Sin(tempo * 0.6f) * 30f, 320f + Mathf.Sin(tempo * 1.1f) * 20f);
        Dragao(dpos, 3.0f, tempo * 2.6f, false, C(0.02f, 0.015f, 0.02f, 0.98f), 0.35f);
        for (int i = 0; i < 22; i++)
        {
            float ph = Fr(tempo * 1.4f + i * 0.045f); Vector2 b = dpos + new Vector2(-725f, -190f), alvo = new Vector2(620f, 640f);
            Vector2 p = Vector2.Lerp(b, alvo, ph) + new Vector2(0f, Mathf.Sin(ph * 5f + i) * 24f * ph);
            Brilho(p, 20f + ph * 52f, C(1f, 0.5f + 0.3f * (1f - ph), 0.1f, 0.5f * (1f - ph * 0.5f)), 0.5f);
        }
        // penhasco e herói
        Relevo(-200f, 80f, Serra(34, 760f, 120f, 3, 0.35f), 1200f, C(0.012f, 0.012f, 0.02f), 0.6f);
        Heroi(new Vector2(520f, 700f), 230f, true, C(0.01f, 0.01f, 0.015f), 0.65f, Mathf.Sin(tempo * 2f) * 0.5f + 0.7f);
        // faíscas do choque
        Brasas(40, new Vector2(700f, 640f), 300f, 420f, C(1f, 0.8f, 0.4f, 0.9f), 0.7f, 41);
        if (lamp > 0.3f) Grad(-800f, -600f, 2720f, 1500f, C(1f, 1f, 1f, lamp * 0.25f), C(1f, 1f, 1f, lamp * 0.25f), 0f);
    }

    void CenaCristais(float k)
    {
        Ceu(C(0.01f, 0.01f, 0.04f), C(0.05f, 0.03f, 0.12f), C(0.12f, 0.07f, 0.22f), 900f);
        Estrelas(100, 700f, 0f, 0.6f);
        Vector2 orbe = new Vector2(960f, 410f);
        float fase1 = Suave(k / 0.35f), rachar = Suave((k - 0.35f) / 0.18f), lanca = Suave((k - 0.52f) / 0.35f);
        Grad(-800f, 840f, 2720f, 1500f, C(0.01f, 0.01f, 0.02f), C(0f, 0f, 0f), 0.5f);
        Heroi(new Vector2(960f, 880f), 280f, false, C(0.01f, 0.01f, 0.015f), 0.55f, 0.4f);
        Linha(new Vector2(960f + 0.13f * 280f, 880f - 0.76f * 280f), new Vector2(940f, 600f), 14f, C(0.01f, 0.01f, 0.015f), 0.55f);
        Linha(new Vector2(960f - 0.13f * 280f, 880f - 0.76f * 280f), new Vector2(985f, 600f), 14f, C(0.01f, 0.01f, 0.015f), 0.55f);
        if (k < 0.55f)
        {
            float r = 20f + fase1 * 55f + Mathf.Sin(tempo * 8f) * 3f * fase1;
            Vector2 sh = new Vector2(Mathf.Sin(tempo * 60f), Mathf.Cos(tempo * 53f)) * rachar * 6f;
            Brilho(orbe + sh, r * 4.5f, C(1f, 0.85f, 0.5f, 0.5f * fase1 + 0.1f), 0.6f); Disco(orbe + sh, r, C(1f, 0.96f, 0.8f, 1f), 0.6f, 32);
            for (int i = 0; i < 3; i++) { float rr = r * (1.4f + Fr(tempo * 0.8f + i * 0.33f) * 2.5f); Brilho(orbe, rr, C(1f, 0.8f, 0.4f, 0.12f * (1f - Fr(tempo * 0.8f + i * 0.33f))), 0.6f); }
            for (int i = 0; i < 9; i++) { float a = i * 0.7f + 0.3f; Linha(orbe + sh + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.2f, orbe + sh + new Vector2(Mathf.Cos(a + 0.2f * Rd(i, 3)), Mathf.Sin(a)) * r * (0.5f + rachar), 3f, C(0.25f, 0.1f, 0.05f, rachar), 0.6f); }
        }
        // os três cristais saem em arcos
        Color[] cores = { C(0.35f, 0.65f, 1f), C(0.3f, 1f, 0.6f), C(0.8f, 0.45f, 1f) };
        Vector2[] destinos = { new Vector2(300f, 220f), new Vector2(1620f, 200f), new Vector2(960f, 90f) };
        float flash = Mathf.Clamp01(1f - Mathf.Abs(k - 0.54f) / 0.04f);
        for (int i = 0; i < 3; i++)
        {
            if (k < 0.5f) continue;
            Vector2 p = Vector2.Lerp(orbe, destinos[i], lanca) + new Vector2(0f, -Mathf.Sin(lanca * Mathf.PI) * 90f);
            for (int tr = 6; tr >= 1; tr--) { float l2 = Mathf.Max(0f, lanca - tr * 0.03f); Vector2 q = Vector2.Lerp(orbe, destinos[i], l2) + new Vector2(0f, -Mathf.Sin(l2 * Mathf.PI) * 90f); Brilho(q, 22f - tr * 2.5f, new Color(cores[i].r, cores[i].g, cores[i].b, 0.35f - tr * 0.045f), 0.6f); }
            Cristal(p, 14f + 10f * lanca, cores[i], 0.6f, Mathf.Sin(tempo * 2f + i) * 0.3f);
        }
        if (flash > 0f) Grad(-800f, -600f, 2720f, 1500f, C(1f, 0.95f, 0.8f, flash * 0.9f), C(1f, 0.95f, 0.8f, flash * 0.9f), 0f);
        Brasas(40, new Vector2(960f, 880f), 700f, 700f, C(1f, 0.85f, 0.5f, 0.7f), 0.7f, 52);
    }

    void CenaCemiterio(float k)
    {
        Ceu(C(0.02f, 0.05f, 0.08f), C(0.06f, 0.13f, 0.16f), C(0.14f, 0.22f, 0.24f), 760f);
        Estrelas(80, 420f, 0f, 0.7f);
        Lua(new Vector2(420f, 200f), 70f, 0.05f, C(0.85f, 0.9f, 0.88f), C(0.5f, 0.7f, 0.7f, 0.3f));
        Nevoa(640f, 100f, C(0.45f, 0.6f, 0.6f, 0.13f), 0.2f, 9f, 6);
        Brilho(new Vector2(1100f, 640f), 560f, C(0.8f, 0.35f, 1f, 0.35f), 0.3f, 220f);         // brilho do cristal sob a terra
        // o Guardião das Lápides
        Vector2 g = new Vector2(1100f, 760f); float s = 1.05f + 0.01f * Mathf.Sin(tempo * 1.2f); Color c0 = C(0.01f, 0.012f, 0.018f);
        Poli(V2(-90, 0, -80, -330, -120, -420, 120, -420, 80, -330, 90, 0), c0, 0.4f, g, s * 1f);                           // corpo
        Poli(V2(-150, -420, -80, -470, 80, -470, 150, -420, 100, -380, -100, -380), c0, 0.4f, g, s);                         // ombreiras
        Poli(Elipse(55f, 60f, 14), c0, 0.4f, g + new Vector2(0f, -500f) * s, s);                                             // elmo
        Poli(V2(-45, -540, -90, -640, -25, -560), c0, 0.4f, g, s); Poli(V2(45, -540, 90, -640, 25, -560), c0, 0.4f, g, s);   // chifres
        Poli(V2(-190, -380, -120, -400, -110, -80, -150, -30, -200, -80), C(0.02f, 0.025f, 0.035f), 0.4f, g, s);                // escudo
        Brilho(g + new Vector2(-22f, -502f) * s, 26f, C(1f, 0.35f, 0.15f, 0.95f), 0.4f); Brilho(g + new Vector2(22f, -502f) * s, 26f, C(1f, 0.35f, 0.15f, 0.95f), 0.4f);
        Linha(g + new Vector2(150f, -300f) * s, g + new Vector2(210f, 40f) * s, 22f, c0, 0.4f); Poli(V2(-30, 0, 30, 0, 38, -80, 0, -110, -38, -80), c0, 0.4f, g + new Vector2(150f, -300f) * s, s);   // maça
        // lápides em três fileiras
        for (int row = 0; row < 3; row++)
            for (int i = 0; i < 9 - row * 2; i++)
            {
                float x = -150f + i * (260f + row * 40f) + Rd(i, row) * 90f, y = 800f + row * 85f, h = 70f + row * 26f + Rd(i, row + 5) * 30f, w = 34f + row * 8f;
                Color cl = C(0.015f + row * 0.01f, 0.02f + row * 0.01f, 0.03f + row * 0.01f);
                if (Rd(i, row + 9) > 0.5f) Poli(V2(-w, 0, -w, -h, 0, -h - w * 0.9f, w, -h, w, 0), cl, 0.5f + row * 0.12f, new Vector2(x, y));
                else { Poli(V2(-w * 0.25f, 0, -w * 0.25f, -h, w * 0.25f, -h, w * 0.25f, 0), cl, 0.5f + row * 0.12f, new Vector2(x, y)); Poli(V2(-w * 0.7f, -h * 0.72f, -w * 0.7f, -h * 0.58f, w * 0.7f, -h * 0.58f, w * 0.7f, -h * 0.72f), cl, 0.5f + row * 0.12f, new Vector2(x, y)); }
            }
        Arvore(new Vector2(200f, 900f), 360f, C(0.01f, 0.015f, 0.02f), 0.6f, 4); Arvore(new Vector2(1700f, 880f), 300f, C(0.01f, 0.015f, 0.02f), 0.6f, 9);
        Luzinhas(14, new Rect(100f, 600f, 1700f, 300f), C(0.5f, 1f, 0.9f, 0.7f), 0.55f, 61);
        Nevoa(900f, 90f, C(0.5f, 0.65f, 0.65f, 0.16f), 0.8f, -14f, 7);
        Grad(-800f, 950f, 2720f, 1400f, C(0f, 0f, 0f, 0f), C(0f, 0f, 0f, 0.85f), 0.8f);
    }

    void CenaCastelo(float k)
    {
        Ceu(C(0.05f, 0.03f, 0.14f), C(0.28f, 0.12f, 0.3f), C(0.8f, 0.4f, 0.35f), 760f);
        Disco(new Vector2(1500f, 560f), 110f, C(1f, 0.75f, 0.5f, 0.55f), 0.05f, 36); Brilho(new Vector2(1500f, 560f), 520f, C(1f, 0.55f, 0.35f, 0.35f), 0.05f, 300f);
        for (int i = 0; i < 5; i++) Brilho(new Vector2(Mathf.Repeat(Rd(i, 2) * 2600f + tempo * 12f, 2800f) - 400f, 160f + i * 100f), 420f, C(0.2f, 0.1f, 0.25f, 0.4f), 0.15f, 60f);
        Modo(false);
        Relevo(-200f, 90f, Serra(34, 800f, 160f, 8, 0.3f), 1300f, C(0.035f, 0.02f, 0.06f), 0.3f);
        // a colina e o castelo
        Relevo(300f, 60f, Serra(28, 860f, 120f, 2, 0.5f), 1300f, C(0.012f, 0.008f, 0.02f), 0.5f);
        Color cc = C(0.008f, 0.006f, 0.014f);
        Poli(V2(-420, 0, -420, -300, 420, -300, 420, 0), cc, 0.5f, new Vector2(960f, 800f));                                  // muralha
        for (int i = 0; i < 14; i++) Poli(V2(-14, 0, -14, -26, 14, -26, 14, 0), cc, 0.5f, new Vector2(560f + i * 60f, 500f));  // ameias
        Torre(new Vector2(560f, 800f), 70f, 480f, cc, false); Torre(new Vector2(1360f, 800f), 70f, 420f, cc, true); Torre(new Vector2(960f, 800f), 110f, 620f, cc, false);
        Poli(V2(-70, 0, -70, -130, -35, -190, 35, -190, 70, -130, 70, 0), C(0f, 0f, 0f), 0.5f, new Vector2(960f, 800f));         // portão
        // cristal azul na janela da torre central
        Brilho(new Vector2(960f, 330f), 260f, C(0.35f, 0.65f, 1f, 0.5f), 0.5f); Cristal(new Vector2(960f, 330f), 22f, C(0.4f, 0.7f, 1f), 0.5f);
        Brilho(new Vector2(960f, 330f), 600f, C(0.35f, 0.6f, 1f, 0.12f), 0.5f, 700f);
        // o cavaleiro que faz a ronda
        float pos = Mathf.Lerp(680f, 1240f, Mathf.PingPong(tempo * 0.05f, 1f));
        Viajante(new Vector2(pos, 500f), 120f, 0, cc, 0.52f, tempo * 3f);
        // bandeiras rasgadas
        for (int i = 0; i < 3; i++) { Vector2 b = new Vector2(560f + i * 400f, 320f - i * 20f); Linha(b, b + new Vector2(0f, -110f), 4f, cc, 0.5f); Poli(V2(0, 0, 70, 12 + Mathf.Sin(tempo * 3f + i) * 8f, 50, 40, 78, 52, 0, 60), C(0.12f, 0.02f, 0.03f), 0.5f, b + new Vector2(0f, -110f)); }
        for (int i = 0; i < 6; i++) { float bx = Mathf.Repeat(i * 420f + tempo * 60f, 2400f) - 200f; Linha(new Vector2(bx, 200f + i * 30f + Mathf.Sin(tempo * 4f + i) * 6f), new Vector2(bx + 20f, 192f + i * 30f), 3f, cc, 0.2f); }
        Nevoa(950f, 100f, C(0.5f, 0.35f, 0.5f, 0.2f), 0.8f, 10f, 11);
        Grad(-800f, 960f, 2720f, 1400f, C(0f, 0f, 0f, 0f), C(0f, 0f, 0f, 0.9f), 0.8f);
    }
    void Torre(Vector2 b, float larg, float alt, Color cor, bool quebrada)
    {
        List<Vector2> p = new List<Vector2> { new Vector2(-larg, 0), new Vector2(-larg, -alt) };
        if (quebrada) { p.Add(new Vector2(-larg * 0.5f, -alt - 40f)); p.Add(new Vector2(-larg * 0.2f, -alt + 10f)); p.Add(new Vector2(larg * 0.3f, -alt - 70f)); p.Add(new Vector2(larg, -alt - 10f)); }
        else { for (int i = -2; i <= 2; i++) { p.Add(new Vector2(i * larg * 0.4f - larg * 0.15f, -alt - 30f)); p.Add(new Vector2(i * larg * 0.4f + larg * 0.15f, -alt - 30f)); } p.Add(new Vector2(larg, -alt)); }
        p.Add(new Vector2(larg, 0));
        // desenha como duas partes simples (corpo + coroa) para não depender de polígono côncavo
        Poli(V2(-larg, 0, -larg, -alt, larg, -alt, larg, 0), cor, 0.5f, b);
        for (int i = -2; i <= 2; i++) if (!quebrada || i < 1) Poli(V2(-larg * 0.15f, 0, -larg * 0.15f, -34, larg * 0.15f, -34, larg * 0.15f, 0), cor, 0.5f, b + new Vector2(i * larg * 0.4f, -alt));
        if (quebrada) Poli(V2(-larg * 0.9f, 0, -larg * 0.3f, -60, larg * 0.2f, 0), cor, 0.5f, b + new Vector2(0f, -alt));
        Brilho(b + new Vector2(0f, -alt * 0.7f), 40f, C(1f, 0.7f, 0.3f, 0.35f), 0.5f);
    }

    void CenaPantano(float k)
    {
        Ceu(C(0.01f, 0.04f, 0.04f), C(0.05f, 0.12f, 0.1f), C(0.12f, 0.24f, 0.18f), 700f);
        Estrelas(40, 300f, 0f, 0.4f);
        Nevoa(560f, 140f, C(0.35f, 0.6f, 0.45f, 0.18f), 0.15f, 7f, 3);
        Relevo(-200f, 90f, Serra(34, 640f, 120f, 5, 0.3f), 800f, C(0.015f, 0.035f, 0.03f), 0.25f);
        Brilho(new Vector2(960f, 700f), 620f, C(0.3f, 1f, 0.55f, 0.35f), 0.35f, 300f);
        // templo afundado
        Color tc = C(0.01f, 0.02f, 0.018f);
        for (int i = 0; i < 5; i++) { float x = 640f + i * 160f, h = 280f - Mathf.Abs(i - 2) * 40f + Rd(i, 3) * 30f; Poli(V2(-30, 0, -30, -h, -12, -h - 14, 20, -h + 10, 30, -h, 30, 0), tc, 0.4f, new Vector2(x, 780f)); }
        Poli(V2(-340, 0, -340, -30, 340, -30, 340, 0), tc, 0.4f, new Vector2(960f, 470f)); Poli(V2(-300, 0, -300, -50, -250, -90, 250, -90, 300, -50, 300, 0), tc, 0.4f, new Vector2(960f, 440f));
        Cristal(new Vector2(960f, 640f), 20f, C(0.35f, 1f, 0.6f), 0.4f);
        for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI; Linha(new Vector2(960f, 640f), new Vector2(960f + Mathf.Cos(a) * 480f * (i % 2 == 0 ? 1 : -1), 640f - Mathf.Sin(a) * 300f), 5f, C(0.4f, 1f, 0.6f, 0.07f + 0.04f * Mathf.Sin(tempo * 2f + i)), 0.4f); }
        // água
        Grad(-800f, 780f, 2720f, 1500f, C(0.03f, 0.09f, 0.07f, 0.96f), C(0.0f, 0.02f, 0.02f, 1f), 0.5f);
        for (int i = 0; i < 18; i++) { float y = 800f + i * 18f + i * i * 0.5f; float w = 90f + i * 28f; Linha(new Vector2(960f - w + Mathf.Sin(tempo + i) * 30f, y), new Vector2(960f + w + Mathf.Sin(tempo + i) * 30f, y), 2f + i * 0.2f, C(0.4f, 0.9f, 0.6f, 0.12f), 0.5f); }
        Brilho(new Vector2(960f, 880f), 420f, C(0.3f, 1f, 0.55f, 0.22f), 0.5f, 90f);
        // olhos do Devorador do Lago
        float ol = Suave((k - 0.45f) / 0.2f) * (0.7f + 0.3f * Mathf.Sin(tempo * 3f));
        if (ol > 0f) { for (int i = 0; i < 2; i++) { Vector2 e = new Vector2(1320f + i * 70f, 880f); Brilho(e, 38f, C(1f, 0.85f, 0.2f, 0.9f * ol), 0.6f); Disco(e, 6f, C(1f, 0.95f, 0.5f, ol), 0.6f, 8); } for (int i = 0; i < 3; i++) { float r = Fr(tempo * 0.5f + i * 0.33f); Linha(new Vector2(1320f - 130f * r, 905f), new Vector2(1460f + 130f * r, 905f), 2f, C(0.7f, 1f, 0.8f, 0.3f * (1f - r) * ol), 0.6f); } }
        // juncos e luzinhas
        for (int i = 0; i < 26; i++) { float x = Rd(i, 3) * 2000f - 40f, h = 160f + Rd(i, 4) * 180f; Vector2 b = new Vector2(x, 1000f + Rd(i, 5) * 80f); Linha(b, b + new Vector2(Mathf.Sin(tempo * 0.9f + i) * 16f, -h), 4f, C(0.005f, 0.02f, 0.015f), 0.8f); }
        Luzinhas(18, new Rect(100f, 500f, 1700f, 400f), C(0.6f, 1f, 0.5f, 0.8f), 0.6f, 71);
        Nevoa(930f, 90f, C(0.4f, 0.7f, 0.5f, 0.17f), 0.8f, -9f, 13);
    }

    void CenaCorrupcao(float k)
    {
        float p = Suave(k);
        Ceu(C(0.02f + 0.1f * p, 0.01f, 0.04f), C(0.07f + 0.15f * p, 0.02f, 0.1f), C(0.15f + 0.22f * p, 0.03f, 0.14f), 900f);
        Estrelas(60, 600f, 0f, 1f - p * 0.8f);
        Color[] cores = { C(0.35f, 0.65f, 1f), C(0.3f, 1f, 0.6f), C(0.8f, 0.45f, 1f) };
        Vector2[] pos = { new Vector2(560f, 520f), new Vector2(1360f, 520f), new Vector2(960f, 330f) };
        for (int i = 0; i < 3; i++)
        {
            Vector2 c = pos[i] + new Vector2(0f, Mathf.Sin(tempo * 1.3f + i * 2f) * 16f);
            Color cor = Color.Lerp(cores[i], C(0.85f, 0.1f, 0.18f), p * 0.85f);
            // fumaça escura enrolando no cristal
            for (int j = 0; j < 12; j++)
            {
                float a = tempo * (0.8f + 0.1f * j) + j * 0.8f + i, r = 70f + 80f * Fr(j * 0.37f + tempo * 0.1f);
                Brilho(c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.7f), 55f + 25f * Mathf.Sin(tempo + j), C(0.05f, 0.0f, 0.08f, 0.5f * (0.3f + p)), 0.5f);
            }
            Modo(false);
            Cristal(c, 38f, cor, 0.55f, Mathf.Sin(tempo + i) * 0.25f);
            // rachaduras
            for (int j = 0; j < 5; j++) { float a = j * 1.3f + i; Vector2 q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12f; Linha(q, q + new Vector2(Mathf.Cos(a + 0.4f), Mathf.Sin(a + 0.4f)) * 40f * p, 3f, C(0.05f, 0f, 0.05f, p), 0.55f); }
            // energia sombria subindo
            Brasas(10, c + new Vector2(0f, 40f), 120f, 260f, C(0.8f, 0.1f, 0.3f, 0.8f * p), 0.55f, 80 + i);
        }
        // tentáculos de sombra pelo céu
        for (int i = 0; i < 6; i++)
        {
            Vector2 a = new Vector2(i * 360f - 60f, 1150f);
            for (int s = 0; s < 14; s++) { Vector2 b = a + new Vector2(Mathf.Sin(tempo * 0.8f + i + s * 0.4f) * 50f, -85f); Linha(a, b, 36f - s * 2.2f, C(0.02f, 0f, 0.04f, 0.8f * p), 0.65f); a = b; }
        }
        Brilho(new Vector2(960f, 540f), 1400f, C(0.7f, 0.05f, 0.15f, 0.18f * p), 0.9f, 900f);
    }

    void CenaMortos(float k)
    {
        Ceu(C(0.02f, 0.03f, 0.08f), C(0.09f, 0.08f, 0.16f), C(0.3f, 0.2f, 0.25f), 760f);
        Estrelas(90, 460f, 0f);
        Lua(new Vector2(560f, 210f), 80f, 0.05f, C(0.9f, 0.9f, 0.84f), C(0.55f, 0.6f, 0.85f, 0.3f));
        Nevoa(640f, 100f, C(0.5f, 0.55f, 0.7f, 0.12f), 0.2f, 8f, 15);
        // a vila, no fundo à direita, com luzes quentes e uma placa
        Relevo(900f, 60f, Serra(24, 760f, 90f, 6, 0.4f), 900f, C(0.02f, 0.015f, 0.03f), 0.35f);
        for (int i = 0; i < 7; i++)
        {
            float x = 1250f + i * 95f, h = 60f + Rd(i, 2) * 50f;
            Poli(V2(-34, 0, -34, -h, 0, -h - 40, 34, -h, 34, 0), C(0.015f, 0.01f, 0.02f), 0.4f, new Vector2(x, 700f));
            Disco(new Vector2(x, 700f - h * 0.55f), 7f, C(1f, 0.78f, 0.4f, 0.95f), 0.4f, 8); Brilho(new Vector2(x, 700f - h * 0.55f), 50f, C(1f, 0.7f, 0.3f, 0.35f), 0.4f);
        }
        float pisc = 0.7f + 0.3f * Mathf.Sin(tempo * 5f); Brilho(new Vector2(1160f, 640f), 150f, C(1f, 0.75f, 0.35f, 0.2f * pisc), 0.4f);
        // chão e os mortos se levantando
        Relevo(-200f, 80f, Serra(34, 900f, 40f, 9, 0.6f), 1200f, C(0.01f, 0.012f, 0.018f), 0.55f);
        for (int i = 0; i < 11; i++)
        {
            float x = 100f + i * 150f + Rd(i, 1) * 50f, y = 930f + Rd(i, 2) * 70f, subir = Suave((k * 1.6f - i * 0.08f) / 0.35f);
            float h = 190f + Rd(i, 3) * 40f; float marcha = Mathf.Max(0f, k * 1.6f - 0.4f) * 90f * (1f - i % 2 * 0.3f);
            Esqueleto(new Vector2(x + marcha, y + (1f - subir) * 230f), h, Mathf.Sin(tempo * 2f + i) * 0.5f, C(0.012f, 0.015f, 0.02f), 0.6f + 0.02f * (i % 3), subir);
        }
        for (int i = 0; i < 8; i++) { float x = i * 290f, y = 960f + Rd(i, 4) * 60f; Poli(V2(-14, 0, -14, -50, 14, -50, 14, 0), C(0.01f, 0.01f, 0.015f), 0.7f, new Vector2(x, y)); }
        Grad(-800f, 1000f, 2720f, 1400f, C(0f, 0f, 0f, 0f), C(0f, 0f, 0f, 0.9f), 0.8f);
        Brasas(18, new Vector2(960f, 960f), 1800f, 500f, C(0.5f, 1f, 0.7f, 0.5f), 0.7f, 91);
    }

    void CenaAventureiros(float k)
    {
        Ceu(C(0.09f, 0.06f, 0.2f), C(0.55f, 0.25f, 0.35f), C(1f, 0.65f, 0.35f), 760f);
        Brilho(new Vector2(960f, 600f), 800f, C(1f, 0.6f, 0.3f, 0.4f), 0.1f, 280f);
        Estrelas(40, 260f, 0f, 0.6f);
        Relevo(-200f, 90f, Serra(34, 700f, 150f, 12, 0.3f), 1000f, C(0.12f, 0.06f, 0.16f), 0.2f);
        // a vila na colina, de longe, com janelas acesas
        Relevo(400f, 60f, Serra(28, 640f, 80f, 14, 0.5f), 900f, C(0.04f, 0.025f, 0.05f), 0.35f);
        for (int i = 0; i < 9; i++) { float x = 760f + i * 48f + Rd(i, 1) * 10f, h = 40f + Rd(i, 2) * 40f; Poli(V2(-20, 0, -20, -h, 0, -h - 26, 20, -h, 20, 0), C(0.02f, 0.012f, 0.025f), 0.4f, new Vector2(x, 620f - Rd(i, 3) * 20f)); Disco(new Vector2(x, 600f - h * 0.5f), 4f, C(1f, 0.8f, 0.4f), 0.4f, 6); }
        Brilho(new Vector2(960f, 580f), 200f, C(1f, 0.7f, 0.35f, 0.3f), 0.4f);
        // a estrada
        Poli(V2(-900, 0, -80, -330, 80, -330, 900, 0), C(0.09f, 0.05f, 0.06f), 0.5f, new Vector2(960f, 1080f) + new Vector2(0f, 0f));
        Poli(V2(-720, 0, -40, -300, 40, -300, 720, 0), C(0.2f, 0.12f, 0.1f, 0.9f), 0.5f, new Vector2(960f, 1080f));
        Relevo(-200f, 80f, Serra(34, 1000f, 60f, 16, 0.6f), 1300f, C(0.03f, 0.02f, 0.04f), 0.6f);
        // os quatro aventureiros, caminhando para a vila
        float andar = k * 220f;
        Color sil = C(0.015f, 0.01f, 0.02f);
        Vector2[] bases = { new Vector2(820f, 900f), new Vector2(920f, 880f), new Vector2(1010f, 885f), new Vector2(1100f, 905f) };
        int[] tipos = { 0, 1, 2, 3 };
        for (int i = 0; i < 4; i++)
        {
            float prog = Mathf.Clamp01(k * 1.15f - 0.05f * i);
            Vector2 pe = Vector2.Lerp(bases[i], new Vector2(960f + (i - 1.5f) * 22f, 660f), prog * 0.55f);
            float h = Mathf.Lerp(280f, 150f, prog * 0.55f);
            Viajante(pe, h, tipos[i], sil, 0.7f, tempo * 3.4f + i * 0.6f);
        }
        Luzinhas(26, new Rect(100f, 400f, 1700f, 560f), C(1f, 0.9f, 0.5f, 0.8f), 0.65f, 100);
        // fecha com o título
        float tt = Suave((k - 0.62f) / 0.25f);
        if (tt > 0f) Titulo("V A L V E R D E", 340f, 130f, tt);
    }

    // ------------------------------------------------------------------ texto
    string tituloPend; float tituloY, tituloTam, tituloA;
    void Titulo(string texto, float y, float tam, float a) { tituloPend = texto; tituloY = y; tituloTam = tam; tituloA = a; }
    void DesenharTitulo()
    {
        float escala = Screen.height / 1080f;
        GUIStyle st = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(tituloTam * escala), alignment = TextAnchor.MiddleCenter };
        Rect r = new Rect(0, tituloY * escala, Screen.width, tituloTam * 1.4f * escala);
        st.normal.textColor = new Color(0f, 0f, 0f, 0.7f * tituloA); GUI.Label(new Rect(r.x + 4f, r.y + 6f, r.width, r.height), tituloPend, st);
        st.normal.textColor = new Color(1f, 0.86f, 0.5f, tituloA); GUI.Label(r, tituloPend, st);
    }

    void OnGUI()
    {
        GUI.depth = -9000;                                            // por cima de tudo (inclusive da seleção de personagens)
        if (!Ativa && fimGeral < 0f) return;
        Event ev = Event.current;
        if (ev.type == EventType.MouseDown || ev.type == EventType.MouseUp || ev.type == EventType.KeyDown || ev.type == EventType.KeyUp || ev.type == EventType.ScrollWheel)
        {
            if (Ativa) ev.Use();                                      // a seleção de personagens por trás não recebe clique nem tecla
        }
        if (ev.type != EventType.Repaint) return;
        float W = Screen.width, H = Screen.height; sc = Mathf.Max(W / 1920f, H / 1080f); tempo = Time.unscaledTime - inicioTudo;
        if (encerrando && !Ativa)
        {
            float f = Mathf.Clamp01(1f - (Time.unscaledTime - fimGeral) / 1.0f);
            PrepararGL(); gA = f; Grad(-800f, -600f, 2720f, 1500f, C(0f, 0f, 0f), C(0f, 0f, 0f)); FimGL(); return;
        }
        if (atual >= cenas.Count) return;
        Cena c = cenas[atual]; float t = Time.unscaledTime - inicioCena;
        PrepararGL(); tituloPend = null;
        gA = 1f;
        Grad(-800f, -600f, 2720f, 1500f, C(0f, 0f, 0f), C(0f, 0f, 0f));
        float k = Mathf.Clamp01(Mathf.Max(0f, t) / c.dur);
        gz = Mathf.Lerp(c.z0, c.z1, Suave(k)); gpan = Vector2.Lerp(c.p0, c.p1, k);
        float fade = Mathf.Min(Suave(t / 0.9f), Suave((c.dur - t) / 0.9f));
        if (t >= 0f) { gA = 1f; c.desenha(k); }
        gA = 1f;
        // barras de cinema e degradê para a legenda
        float barra = H * 0.1f;
        QuadG(new Vector2(0, 0), new Vector2(W, 0), new Vector2(W, barra), new Vector2(0, barra), C(0, 0, 0), C(0, 0, 0), C(0, 0, 0), C(0, 0, 0));
        QuadG(new Vector2(0, H - barra), new Vector2(W, H - barra), new Vector2(W, H), new Vector2(0, H), C(0, 0, 0), C(0, 0, 0), C(0, 0, 0), C(0, 0, 0));
        QuadG(new Vector2(0, H - barra - H * 0.22f), new Vector2(W, H - barra - H * 0.22f), new Vector2(W, H - barra), new Vector2(0, H - barra), C(0, 0, 0, 0), C(0, 0, 0, 0), C(0, 0, 0, 0.78f), C(0, 0, 0, 0.78f));
        // fade de entrada/saída da cena (por cima de tudo, menos do texto)
        if (fade < 1f) QuadG(new Vector2(0, 0), new Vector2(W, 0), new Vector2(W, H), new Vector2(0, H), C(0, 0, 0, 1f - fade), C(0, 0, 0, 1f - fade), C(0, 0, 0, 1f - fade), C(0, 0, 0, 1f - fade));
        FimGL();
        if (tituloPend != null) DesenharTitulo();
        // legenda (letra por letra)
        float e = H / 1080f;
        GUIStyle ls = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(44f * e), alignment = TextAnchor.UpperCenter, wordWrap = true };
        int n = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, t - 0.7f) * VEL), 0, c.texto.Length);
        float aT = Mathf.Min(Suave((t - 0.5f) / 0.5f), Suave((c.dur - t) / 0.7f));
        Rect lr = new Rect(W * 0.14f, H - barra - 190f * e, W * 0.72f, 170f * e);
        ls.normal.textColor = new Color(0f, 0f, 0f, 0.85f * aT); GUI.Label(new Rect(lr.x + 2f, lr.y + 3f, lr.width, lr.height), c.texto.Substring(0, n), ls);
        ls.normal.textColor = new Color(0.97f, 0.93f, 0.82f, aT); GUI.Label(lr, c.texto.Substring(0, n), ls);
        GUIStyle dica = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(22f * e), alignment = TextAnchor.MiddleRight };
        dica.normal.textColor = new Color(0.8f, 0.77f, 0.7f, 0.65f);
        GUI.Label(new Rect(W - 640f * e, H - barra * 0.85f, 600f * e, 40f * e), "[Enter] avançar     [Esc] pular a abertura", dica);
        // indicador de cenas
        for (int i = 0; i < cenas.Count; i++) { Color pc = i == atual ? new Color(1f, 0.85f, 0.5f, 0.95f) : new Color(1f, 1f, 1f, i < atual ? 0.45f : 0.18f); GUI.color = pc; GUI.DrawTexture(new Rect(40f * e + i * 24f * e, H - barra * 0.55f, 14f * e, 4f * e), Texture2D.whiteTexture); }
        GUI.color = Color.white;
    }
}

