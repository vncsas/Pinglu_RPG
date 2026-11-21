using System;
using System.Collections.Generic;
using UnityEngine;

// SEQUÊNCIAS ILUSTRADAS: usa o mesmo motor de desenho da abertura do jogo (silhuetas, céu, brilhos, legenda letra por letra)
// para contar cenas especiais, como a morte do dragão. Uso: IntroDoJogo.TocarMorteDoDragao(() => { ... });
public partial class IntroDoJogo
{
    static string seqPendente; static Action seqAoTerminar; string seqId;
    bool sequencia; Action aoTerminarSeq; bool menuAntesSeq; string dicaTexto = "[Enter] avançar     [Esc] pular a abertura";

    // rotação aplicada pelo Poli (só o dragão caindo usa): gira em volta de gRotC
    float gRot; Vector2 gRotC;
    Vector2 RotM(Vector2 p)
    {
        if (gRot == 0f) return p;
        Vector2 d = p - gRotC; float c = Mathf.Cos(gRot), s = Mathf.Sin(gRot);
        return gRotC + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
    }

    /// <summary>Toca a morte do dragão em cenas ilustradas (≈ 44 s). aoTerminar roda no fim (ou quando o jogador pular com Esc).</summary>
    public static void TocarMorteDoDragao(Action aoTerminar)
    {
        if (Ativa) { if (aoTerminar != null) aoTerminar(); return; }
        bool mundo = Terrain.activeTerrain != null && (PortalDoMundo.Atual != null || FindAnyObjectByType<NpcFalante>() != null);
        seqPendente = mundo ? "morte3d" : "morte"; seqAoTerminar = aoTerminar;
        new GameObject("Cena ilustrada").AddComponent<IntroDoJogo>();
    }

    /// <summary>Toca o epílogo do final do jogo em 3D (4 cenas). Devolve false se o mundo 3D não existir (o FinalDoJogo usa o epílogo antigo).</summary>
    public static bool TocarFinal(Action aoTerminar)
    {
        if (Ativa || Terrain.activeTerrain == null) return false;
        bool achou = false; foreach (NpcFalante n in FindObjectsByType<NpcFalante>(FindObjectsSortMode.None)) if (n != null && n.nomeExibido == "Caio") { achou = true; break; }
        if (!achou && PortalDoMundo.Atual == null) return false;
        seqPendente = "final"; seqAoTerminar = aoTerminar;
        new GameObject("Final em 3D").AddComponent<IntroDoJogo>();
        return true;
    }

    List<Cena> MontarSequencia(string id)
    {
        if (id == "final")
        {
            Action<float> reserva = k => { Ceu(C(0.35f, 0.5f, 0.8f), C(0.95f, 0.72f, 0.5f), C(1f, 0.85f, 0.6f), 860f); Brilho(new Vector2(960f, 640f), 900f, C(1f, 0.85f, 0.5f, 0.45f), 0.1f, 500f); Modo(false); };
            return new List<Cena>
            {
                new Cena { dur = 12f, texto = "Drakthar caiu. O poder dos cristais, já sem dono, se desfez no vento para nunca mais voltar.", desenha = reserva },
                new Cena { dur = 11f, texto = "Valverde acordou numa manhã sem cinzas, sem mortos e sem medo.", desenha = reserva },
                new Cena { dur = 12f, texto = "E no centro da vila, uma placa nova brilhava: CABARÉ DOS AVENTUREIROS.", desenha = reserva },
                new Cena { dur = 13f, texto = "Os heróis, enfim, tinham um tostão no bolso... e um balcão inteiro para gastá-lo.", desenha = reserva },
            };
        }
        if (id == "morte3d")
        {
            Action<float> reserva = k => { Ceu(C(0.35f, 0.5f, 0.8f), C(0.95f, 0.72f, 0.5f), C(1f, 0.85f, 0.6f), 860f); Brilho(new Vector2(960f, 640f), 900f, C(1f, 0.85f, 0.5f, 0.45f), 0.1f, 500f); Modo(false); };
            List<Cena> l = MontarSequencia("morte");
            l.Add(new Cena { dur = 10f, texto = "Na vila, ninguém acreditava. Então todos correram em direção ao herói.", desenha = reserva });
            l.Add(new Cena { dur = 6f, texto = "", desenha = reserva });
            l.Add(new Cena { dur = 8f, texto = "", desenha = reserva });
            return l;
        }
        return new List<Cena>
        {
            new Cena { dur = 8f,  texto = "O gigante não resistiu. Sem forças, Drakthar despencou do céu.", desenha = SeqQueda, z0 = 1.05f, z1 = 1.2f, p0 = new Vector2(30, -10), p1 = new Vector2(-30, 20) },
            new Cena { dur = 8f,  texto = "Seu corpo tremia. O poder do Ladrão ainda lutava dentro dele.", desenha = SeqConvulsao, z0 = 1f, z1 = 1.25f, p0 = Vector2.zero, p1 = new Vector2(0, -30) },
            new Cena { dur = 9f,  texto = "Então a escuridão rasgou o peito do dragão e subiu aos céus...", desenha = SeqColuna, z0 = 1.15f, z1 = 1f, p0 = new Vector2(0, -60), p1 = new Vector2(0, 40) },
            new Cena { dur = 9f,  texto = "E o que restou da fera virou cinzas, levadas pelo vento.", desenha = SeqCinzas, z0 = 1.1f, z1 = 1.2f, p0 = new Vector2(-40, 0), p1 = new Vector2(30, 0) },
            new Cena { dur = 10f, texto = "O céu clareou. Pela primeira vez em séculos, Valverde podia respirar.", desenha = SeqLuz, z0 = 1f, z1 = 1.12f, p0 = new Vector2(0, 30), p1 = new Vector2(0, -20) },
        };
    }

    // ---- peças comuns
    void SeqChao(Color c, float par = 0.6f) { Relevo(-200f, 80f, Serra(34, 810f, 45f, 5, 0.35f), 1300f, c, par); }
    void SeqRaio(float lamp, float x0, int sem, Color cor)
    {
        if (lamp < 0.1f) return;
        Vector2 a = new Vector2(x0, -50f);
        for (int i = 0; i < 9; i++) { Vector2 b = a + new Vector2((Rd(i, sem) - 0.5f) * 150f, 100f); Linha(a, b, 7f, new Color(cor.r, cor.g, cor.b, lamp), 0.2f); Brilho(b, 90f, new Color(cor.r, cor.g, cor.b, 0.3f * lamp), 0.2f); a = b; }
    }
    // raio que sai de um ponto, em zigue-zague
    void SeqArco(Vector2 a, Vector2 b, float w, Color cor, float par, int sem)
    {
        Vector2 p = a;
        for (int i = 1; i <= 7; i++)
        {
            Vector2 q = Vector2.Lerp(a, b, i / 7f); if (i < 7) q += new Vector2(Rd(i, sem) - 0.5f, Rd(i, sem + 9) - 0.5f) * 70f;
            Linha(p, q, w, cor, par); p = q;
        }
        Brilho(b, w * 7f, new Color(cor.r, cor.g, cor.b, cor.a * 0.4f), par);
    }
    void SeqDragaoCaido(Vector2 c, float s, float tremor, Color cor, float fase = -1.5708f)
    {
        Vector2 off = new Vector2((Rd((int)(tempo * 30f), 1) - 0.5f), (Rd((int)(tempo * 30f), 2) - 0.5f)) * tremor;
        Dragao(c + off, s, fase, false, cor, 0.45f);
    }

    // ---- 1) a queda
    void SeqQueda(float k)
    {
        float lamp = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(tempo * 2.3f) * Mathf.Sin(tempo * 5.1f + 1f)), 6f);
        Ceu(C(0.06f + lamp * 0.3f, 0.02f + lamp * 0.3f, 0.1f + lamp * 0.4f), C(0.25f + lamp * 0.2f, 0.05f, 0.12f), C(0.62f, 0.16f, 0.08f), 860f);
        for (int i = 0; i < 7; i++) Brilho(new Vector2(Mathf.Repeat(Rd(i, 2) * 2600f + tempo * 25f, 2800f) - 400f, 110f + i * 85f), 430f, C(0.05f, 0.02f, 0.07f, 0.6f), 0.15f, 90f);
        Modo(false);
        SeqRaio(lamp, 400f + Rd((int)(tempo * 2f), 5) * 1100f, 31, C(1f, 0.85f, 0.85f));
        SeqChao(C(0.02f, 0.012f, 0.015f));

        float u = Suave(Mathf.Clamp01(k / 0.72f)); float cai = u * u;
        Vector2 pos = Vector2.Lerp(new Vector2(1500f, -260f), new Vector2(980f, 720f), cai);
        float imp = Mathf.Clamp01((k - 0.72f) / 0.28f);
        // rastro de fogo e fumaça atrás dele
        for (int i = 0; i < 26 && k < 0.8f; i++)
        {
            float ph = i / 26f; Vector2 q = pos + new Vector2(300f * ph + Mathf.Sin(tempo * 8f + i) * 16f, -420f * ph);
            Brilho(q, 40f + 70f * ph, C(1f, 0.5f - 0.3f * ph, 0.1f, 0.5f * (1f - ph)), 0.45f);
            Brilho(q + new Vector2(30f, -20f), 50f + 90f * ph, C(0.04f, 0.02f, 0.04f, 0.45f * (1f - ph)), 0.45f);
        }
        Modo(false);
        gRot = k < 0.72f ? Mathf.Lerp(-0.1f, -0.85f, u) : Mathf.Lerp(-0.85f, -0.12f, Suave(imp * 2.5f)); gRotC = pos;
        Dragao(pos, 2.8f, tempo * (k < 0.72f ? Mathf.Lerp(7f, 1.5f, u) : 0f), false, C(0.03f, 0.015f, 0.025f, 0.98f), 0.45f);
        gRot = 0f;
        if (k >= 0.72f)
        {
            float a = 1f - imp;
            for (int i = 0; i < 12; i++) Brilho(new Vector2(980f + (Rd(i, 3) - 0.5f) * (500f + imp * 1500f), 800f - Rd(i, 4) * 120f * imp), 120f + imp * 220f, C(0.45f, 0.36f, 0.3f, 0.45f * a), 0.5f, 60f + imp * 90f);
            Modo(false); Brilho(new Vector2(980f, 790f), 600f * (0.3f + imp), C(1f, 0.7f, 0.4f, 0.8f * a), 0.5f, 140f);
            if (imp < 0.3f) Grad(-800f, -600f, 2720f, 1500f, C(1f, 0.95f, 0.9f, (0.3f - imp) / 0.3f * 0.85f), C(1f, 0.95f, 0.9f, (0.3f - imp) / 0.3f * 0.85f));
            gpan += new Vector2(Rd((int)(tempo * 40f), 7) - 0.5f, Rd((int)(tempo * 40f), 8) - 0.5f) * 60f * a;
        }
        Brasas(40, new Vector2(960f, 900f), 2200f, 700f, C(1f, 0.5f, 0.15f, 0.8f), 0.8f, 41);
    }

    // ---- 2) convulsão
    void SeqConvulsao(float k)
    {
        Ceu(C(0.05f, 0.02f, 0.1f), C(0.18f, 0.06f, 0.2f), C(0.4f, 0.12f, 0.22f), 860f);
        for (int i = 0; i < 6; i++) Brilho(new Vector2(Mathf.Repeat(Rd(i, 12) * 2600f + tempo * 12f, 2800f) - 400f, 120f + i * 90f), 420f, C(0.06f, 0.02f, 0.09f, 0.55f), 0.15f, 90f);
        Modo(false); SeqChao(C(0.015f, 0.01f, 0.02f));
        Vector2 c = new Vector2(960f, 720f); float pulso = 0.75f + 0.25f * Mathf.Sin(tempo * 22f);
        // aura falhando: aparece e some
        for (int i = 0; i < 10; i++) Brilho(c + new Vector2((Rd(i, 20) - 0.5f) * 700f, -60f - Rd(i, 21) * 260f), 150f + Rd(i, 22) * 100f, C(0.45f, 0.05f, 0.7f, Mathf.Abs(Mathf.Sin(tempo * 11f + i)) * 0.35f * (1f - 0.6f * k)), 0.42f, 130f);
        SeqDragaoCaido(c, 3.0f, 6f + 22f * k, C(0.03f, 0.02f, 0.05f, 0.98f));
        // luz roxa vazando do peito, cada vez mais forte
        Brilho(c + new Vector2(10f, -20f), (140f + 420f * k) * pulso, C(0.75f, 0.45f, 1f, 0.45f + 0.5f * k), 0.46f, (110f + 300f * k) * pulso);
        Brilho(c + new Vector2(10f, -20f), (50f + 120f * k) * pulso, C(1f, 0.95f, 1f, 0.6f + 0.4f * k), 0.46f);
        int n = 3 + (int)(8 * k);
        for (int i = 0; i < n; i++)
        {
            float ang = (Rd((int)(tempo * 8f), i) * 6.283f); float len = 120f + 220f * Rd(i, 40) + 200f * k;
            SeqArco(c + new Vector2(0f, -20f), c + new Vector2(Mathf.Cos(ang) * len * 1.4f, Mathf.Sin(ang) * len * 0.8f - 40f), 4f, C(0.9f, 0.7f, 1f, 0.9f), 0.46f, 50 + i);
        }
        Brasas(50, c + new Vector2(0f, 60f), 1300f, 700f, C(0.8f, 0.5f, 1f, 0.9f), 0.7f, 61);
    }

    // ---- 3) a coluna negra sobe aos céus
    void SeqColuna(float k)
    {
        float lamp = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(tempo * 3.1f) * Mathf.Sin(tempo * 6.3f + 1f)), 4f);
        Ceu(C(0.03f + lamp * 0.3f, 0.01f + lamp * 0.2f, 0.07f + lamp * 0.4f), C(0.14f, 0.04f, 0.18f), C(0.35f, 0.1f, 0.2f), 860f);
        Modo(false); SeqChao(C(0.015f, 0.01f, 0.02f));
        Vector2 c = new Vector2(960f, 720f);
        SeqDragaoCaido(c, 3.0f, 8f * (1f - k), C(0.03f, 0.02f, 0.05f, 0.98f));
        // ondas de choque no chão
        for (int i = 0; i < 3; i++) { float ph = Fr(tempo * 0.8f + i * 0.33f); Brilho(new Vector2(960f, 830f), 120f + 1500f * ph, C(0.85f, 0.7f, 1f, 0.5f * (1f - ph)), 0.5f, 22f + 120f * ph); }
        // a coluna: bolas de fumaça negra com borda roxa, subindo
        float alt = Suave(Mathf.Clamp01(k * 1.5f));
        int total = 44;
        for (int j = 0; j < total; j++)
        {
            float f = j / (float)total; if (f > alt) break;
            float y = 700f - f * 1100f, x = 960f + Mathf.Sin(tempo * 2.3f + j * 0.55f) * (14f + 70f * f), r = 70f + 55f * Mathf.Sin(f * 3f + tempo) + f * 40f;
            Brilho(new Vector2(x + 10f, y), r * 1.25f, C(0.55f, 0.15f, 0.9f, 0.22f), 0.5f);
            Brilho(new Vector2(x, y), r, C(0.03f, 0f, 0.05f, 0.92f), 0.5f);
            Modo(false);
        }
        Brilho(c + new Vector2(0f, -20f), 400f, C(0.8f, 0.55f, 1f, 0.6f * (1f - 0.5f * k)), 0.5f, 260f);
        // raios do céu
        if (lamp > 0.1f) SeqArco(new Vector2(960f + (Rd((int)(tempo * 4f), 3) - 0.5f) * 800f, -80f), c + new Vector2(0f, -30f), 7f, C(0.9f, 0.75f, 1f, lamp), 0.5f, 70);
        Brasas(70, new Vector2(960f, 800f), 600f, 1000f, C(0.8f, 0.4f, 1f, 0.9f), 0.6f, 71);
        // clarão branco no meio
        float fl = Mathf.Max(0f, 1f - Mathf.Abs(k - 0.55f) / 0.09f);
        if (fl > 0f) Grad(-800f, -600f, 2720f, 1500f, C(1f, 1f, 1f, fl * 0.95f), C(1f, 1f, 1f, fl * 0.95f));
        gpan += new Vector2(Rd((int)(tempo * 40f), 7) - 0.5f, Rd((int)(tempo * 40f), 8) - 0.5f) * 18f * Mathf.Sin(Mathf.Clamp01(k * 1.3f) * Mathf.PI);
    }

    // ---- 4) o corpo vira cinzas
    void SeqCinzas(float k)
    {
        float e = Suave(Mathf.Clamp01(k * 1.08f));
        Ceu(Color.Lerp(C(0.05f, 0.03f, 0.08f), C(0.2f, 0.15f, 0.2f), e), Color.Lerp(C(0.16f, 0.07f, 0.14f), C(0.45f, 0.3f, 0.28f), e), Color.Lerp(C(0.3f, 0.1f, 0.12f), C(0.75f, 0.5f, 0.35f), e), 860f);
        for (int i = 0; i < 6; i++) Brilho(new Vector2(Mathf.Repeat(Rd(i, 14) * 2600f + tempo * 40f, 2800f) - 400f, 120f + i * 90f), 420f, C(0.08f, 0.05f, 0.08f, 0.5f * (1f - e)), 0.15f, 90f);
        Modo(false); SeqChao(C(0.02f, 0.015f, 0.02f));
        Vector2 c = new Vector2(960f, 740f - e * 40f); float s = Mathf.Lerp(3.0f, 0.4f, e);
        // brasa por dentro, rachaduras laranja
        float brilho = Mathf.Sin(Mathf.Clamp01(k * 1.2f) * Mathf.PI);
        Brilho(c + new Vector2(0f, -20f * s), 300f * s, C(1f, 0.4f, 0.08f, 0.6f * brilho), 0.46f, 160f * s);
        SeqDragaoCaido(c, s, 0f, Color.Lerp(C(0.03f, 0.02f, 0.05f, 0.98f), C(0.25f, 0.22f, 0.22f, 0.9f), e * 0.8f));
        for (int i = 0; i < 9; i++)
        {
            float a = i * 0.7f; Vector2 q = c + new Vector2(Mathf.Cos(a) * 190f * s, Mathf.Sin(a) * 50f * s - 20f * s);
            Linha(q, q + new Vector2(Mathf.Cos(a + 0.6f), Mathf.Sin(a + 0.6f)) * 60f * s, 4f, C(1f, 0.55f, 0.1f, 0.9f * brilho), 0.47f);
        }
        // pilha de cinzas que fica
        Poli(Elipse(260f, 34f), C(0.28f, 0.26f, 0.26f, 0.95f * e), 0.5f, new Vector2(960f, 815f), 1f);
        // cinzas e brasas levadas pelo vento
        for (int i = 0; i < 110; i++)
        {
            float vel = 0.1f + Rd(i, 2) * 0.22f, ph = Fr(tempo * vel + Rd(i, 3));
            float x = 960f + (Rd(i, 4) - 0.5f) * 420f * s + ph * (1300f + Rd(i, 5) * 600f), y = 760f - Rd(i, 6) * 160f * s - ph * (120f + Rd(i, 7) * 360f) + Mathf.Sin(ph * 9f + i) * 24f;
            float a = Mathf.Sin(ph * Mathf.PI) * Mathf.Clamp01(0.15f + e * 1.4f);
            bool brasa = Rd(i, 8) > 0.55f;
            Brilho(new Vector2(x, y), 5f + Rd(i, 9) * 9f, brasa ? C(1f, 0.55f, 0.15f, a) : C(0.7f, 0.66f, 0.64f, a * 0.8f), 0.6f);
        }
    }

    // ---- 5) a luz e a paz
    void SeqLuz(float k)
    {
        float p = Suave(Mathf.Clamp01(k * 1.15f));
        Ceu(Color.Lerp(C(0.1f, 0.08f, 0.16f), C(0.3f, 0.5f, 0.85f), p), Color.Lerp(C(0.3f, 0.15f, 0.22f), C(0.95f, 0.72f, 0.5f), p), Color.Lerp(C(0.45f, 0.18f, 0.14f), C(1f, 0.88f, 0.6f), p), 860f);
        Brilho(new Vector2(960f, 560f), 1200f, C(1f, 0.85f, 0.5f, 0.55f * p), 0.1f, 700f);
        for (int i = 0; i < 6; i++) Brilho(new Vector2(Mathf.Repeat(Rd(i, 15) * 2600f + tempo * 15f, 2800f) - 400f, 110f + i * 80f), 430f, Color.Lerp(C(0.1f, 0.06f, 0.12f, 0.45f), C(1f, 0.9f, 0.8f, 0.3f), p), 0.15f, 85f);
        Modo(false);
        // pilar de luz dourada
        Brilho(new Vector2(960f, 330f), 160f, C(1f, 0.92f, 0.6f, 0.55f * p), 0.3f, 900f);
        Brilho(new Vector2(960f, 330f), 60f, C(1f, 1f, 0.9f, 0.75f * p), 0.3f, 900f);
        SeqChao(Color.Lerp(C(0.02f, 0.015f, 0.02f), C(0.1f, 0.09f, 0.07f), p));
        Poli(Elipse(260f, 30f), C(0.28f, 0.26f, 0.26f, 0.8f * (1f - p * 0.6f)), 0.5f, new Vector2(960f, 815f), 1f);
        // almas subindo
        for (int i = 0; i < 28; i++)
        {
            float ph = Fr(tempo * 0.07f + Rd(i, 3)), x = 960f + (Rd(i, 4) - 0.5f) * 420f + Mathf.Sin(tempo * 0.8f + i) * 30f, y = 800f - ph * 1000f;
            Brilho(new Vector2(x, y), 9f + Rd(i, 5) * 12f, C(1f, 0.95f, 0.8f, Mathf.Sin(ph * Mathf.PI) * p * 0.95f), 0.5f);
        }
        // os aventureiros, de costas, olhando a luz
        for (int i = 0; i < 4; i++) Heroi(new Vector2(380f + i * 105f, 815f + (i % 2) * 12f), 210f - i * 10f, i == 0, C(0.01f, 0.01f, 0.02f), 0.62f, 0.3f);
        Brasas(30, new Vector2(960f, 900f), 2000f, 700f, C(1f, 0.8f, 0.4f, 0.7f), 0.8f, 91);
        if (k > 0.5f) Titulo("Drakthar foi derrotado", 250f, 84f, Suave((k - 0.5f) / 0.2f) * Suave((1f - k) / 0.1f));
    }
}
