using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

// Desenha, só com código, o controle do jogador (PlayStation 4, PlayStation 5, Xbox ou genérico) com a função de cada botão ao lado.
// O formato muda conforme o controle conectado; sem controle conectado aparece o do PlayStation como exemplo.
public static class DesenhoDeControle
{
    static readonly Color OURO = new Color(0.86f, 0.68f, 0.34f);

    // ---- símbolos do PlayStation (texturas brancas pintadas por código)
    static Texture2D simCruz, simCirculo, simQuadrado, simTriangulo;
    static float Seg(Vector2 p, Vector2 a, Vector2 b) { Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); return (p - (a + ab * t)).magnitude; }
    static Texture2D Simbolo(int tipo)
    {
        const int N = 64; Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[N * N]; float meia = 4.6f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f), c = new Vector2(32f, 32f); float d;
                switch (tipo)
                {
                    case 0: d = Mathf.Min(Seg(p, new Vector2(15, 15), new Vector2(49, 49)), Seg(p, new Vector2(49, 15), new Vector2(15, 49))); break;
                    case 1: d = Mathf.Abs((p - c).magnitude - 18f); break;
                    case 2: { Vector2 q = p - c; d = Mathf.Abs(Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.y)) - 17f); break; }
                    default:
                    {
                        Vector2 a = new Vector2(32, 52), b = new Vector2(52, 19), cc = new Vector2(12, 19);
                        d = Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, cc), Seg(p, cc, a))); break;
                    }
                }
                byte al = (byte)(Mathf.Clamp01((meia - d) / 1.2f + 0.5f) * 255f);
                px[y * N + x] = new Color32(255, 255, 255, al);
            }
        t.SetPixels32(px); t.Apply(false, true); return t;
    }
    static Texture2D Sim(int i)
    {
        if (simCruz == null) { simCruz = Simbolo(0); simCirculo = Simbolo(1); simQuadrado = Simbolo(2); simTriangulo = Simbolo(3); }
        return i == 0 ? simCruz : i == 1 ? simCirculo : i == 2 ? simQuadrado : simTriangulo;
    }

    static void Cor(Rect r, Color c, float raio = 0f) { MenuDePausa.Cor(r, c, raio); }
    static void Disco(Vector2 c, float raio, Color cor) { Cor(new Rect(c.x - raio, c.y - raio, raio * 2f, raio * 2f), cor, raio); }
    static void Linha(Vector2 a, Vector2 b, float w, Color cor)
    {
        Matrix4x4 m = GUI.matrix; float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(ang, a); Cor(new Rect(a.x, a.y - w * 0.5f, (b - a).magnitude, w), cor); GUI.matrix = m;
    }

    static readonly Color FUNDO = new Color(0.028f, 0.027f, 0.034f, 1f);

    // contorno fino (só a linha): desenha o formato maior na cor da linha e o formato certo na cor do fundo
    static void Contorno(Rect r, float raio, float st, Color linha) { Cor(new Rect(r.x - st, r.y - st, r.width + st * 2f, r.height + st * 2f), linha, raio + st); Cor(r, FUNDO, raio); }
    static void Anel(Vector2 c, float raio, float st, Color linha) { Disco(c, raio, linha); Disco(c, Mathf.Max(0.5f, raio - st), FUNDO); }

    // um botão do controle com etiqueta
    class Item
    {
        public string tag, acao; public Vector2 ponto, dir; public int lado, tipo, sim; public Color cor = Color.white;   // tipo: 0 caixa, 1 círculo com texto, 2 símbolo PlayStation, 3 direcional
        public bool destaque, vazio;
    }

    static string AcoesDo(GamepadButton b)
    {
        List<string> n = new List<string>();
        foreach (Acao a in Comandos.AcoesDoControle) if (Comandos.Botao(a) == (int)b) n.Add(Comandos.NomeCurto(a));
        return n.Count == 0 ? null : string.Join(" / ", n);
    }

    // area: onde cabe o desenho inteiro (controle no meio, etiquetas dos lados). destaque: botão da ação selecionada na lista (-1 = nenhum)
    public static void Desenhar(Rect area, float esc, float fade, Font fonte, int destaque)
    {
        Comandos.TipoDeControle tipo = Comandos.Tipo;
        bool ps = tipo == Comandos.TipoDeControle.PlayStation4 || tipo == Comandos.TipoDeControle.PlayStation5;

        float sc = Mathf.Min(area.width / 1000f, area.height / 600f);
        const float K = 0.86f;                                                  // o controle ocupa só o miolo; as etiquetas ficam nos lados
        Vector2 org = new Vector2(area.center.x - 500f * sc, area.center.y - 300f * sc);
        System.Func<float, float, Vector2> P = (x, y) => new Vector2(org.x + (500f + (x - 500f) * K) * sc, org.y + (300f + (y - 300f) * K) * sc);
        System.Func<float, float, float, float, Rect> R = (x, y, w, h) => { Vector2 a = P(x, y); return new Rect(a.x, a.y, w * K * sc, h * K * sc); };
        float ks = K * sc, st = Mathf.Max(1.8f, 2.3f * sc);
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

        Color L = new Color(0.84f, 0.87f, 0.94f, 0.92f * fade);               // cor das linhas do controle
        Color gold = new Color(1f, 0.82f, 0.38f, fade);
        Color Cd(bool d) { return d ? Color.Lerp(L, gold, 0.6f + 0.4f * pulso) : L; }
        bool D(GamepadButton b) { return destaque == (int)b; }

        // ---- corpo (primeiro todas as bordas, depois todos os miolos, para a linha só aparecer no contorno de fora)
        Contorno(R(318f, 138f, 108f, 56f), 20f * ks, st, Cd(D(GamepadButton.LeftTrigger)));
        Contorno(R(574f, 138f, 108f, 56f), 20f * ks, st, Cd(D(GamepadButton.RightTrigger)));
        Vector2 gL = P(338f, 420f), gR = P(662f, 420f); float gr = 88f * ks; Rect corpo = R(300f, 215f, 400f, 200f);
        Disco(gL, gr + st, L); Disco(gR, gr + st, L); Cor(new Rect(corpo.x - st, corpo.y - st, corpo.width + st * 2f, corpo.height + st * 2f), L, 70f * ks + st);
        Disco(gL, gr, FUNDO); Disco(gR, gr, FUNDO); Cor(corpo, FUNDO, 70f * ks);
        Contorno(R(310f, 196f, 126f, 30f), 12f * ks, st, Cd(D(GamepadButton.LeftShoulder)));
        Contorno(R(564f, 196f, 126f, 30f), 12f * ks, st, Cd(D(GamepadButton.RightShoulder)));

        // ---- peças
        Vector2 pDir = ps ? P(372f, 290f) : P(430f, 365f);
        Vector2 pL = ps ? P(432f, 368f) : P(372f, 290f);
        Vector2 pR = P(568f, 368f), pFace = P(628f, 290f);
        Vector2 pSel = ps ? P(398f, 252f) : P(464f, 292f), pSta = ps ? P(602f, 252f) : P(536f, 292f);

        if (ps)
        {
            Contorno(R(432f, 232f, 136f, 78f), 12f * ks, st * 0.8f, new Color(L.r, L.g, L.b, 0.7f * fade));         // touchpad
            Anel(P(500f, 372f), 10f * ks, st * 0.8f, new Color(L.r, L.g, L.b, 0.7f * fade));                       // botão PS
        }
        else
        {
            Anel(P(500f, 252f), 15f * ks, st, new Color(L.r, L.g, L.b, 0.9f * fade));                              // botão Xbox
            Disco(P(500f, 252f), 5f * ks, new Color(L.r, L.g, L.b, 0.9f * fade));
        }
        Rect rs1 = new Rect(pSel.x - 10f * ks, pSel.y - 7f * ks, 20f * ks, 14f * ks), rs2 = new Rect(pSta.x - 10f * ks, pSta.y - 7f * ks, 20f * ks, 14f * ks);
        Contorno(rs1, 6f * ks, st * 0.8f, Cd(D(GamepadButton.Select))); Contorno(rs2, 6f * ks, st * 0.8f, L);

        // direcional: cruz
        float arm = 15f * ks, comp = 44f * ks;
        Rect cv = new Rect(pDir.x - arm, pDir.y - comp, arm * 2f, comp * 2f), ch = new Rect(pDir.x - comp, pDir.y - arm, comp * 2f, arm * 2f);
        Cor(new Rect(cv.x - st, cv.y - st, cv.width + st * 2f, cv.height + st * 2f), L, 5f * ks); Cor(new Rect(ch.x - st, ch.y - st, ch.width + st * 2f, ch.height + st * 2f), L, 5f * ks);
        Cor(cv, FUNDO, 5f * ks); Cor(ch, FUNDO, 5f * ks);
        GamepadButton[] dirs = { GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight };
        Vector2[] dv = { new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0) };
        for (int i = 0; i < 4; i++)
            if (D(dirs[i])) { Vector2 e = pDir + dv[i] * 28f * ks; Cor(new Rect(e.x - 11f * ks, e.y - 11f * ks, 22f * ks, 22f * ks), new Color(gold.r, gold.g, gold.b, 0.5f + 0.4f * pulso), 4f * ks); }

        // analógicos
        Anel(pL, 38f * ks, st, Cd(D(GamepadButton.LeftStick))); Anel(pL, 25f * ks, st * 0.8f, new Color(L.r, L.g, L.b, 0.6f * fade));
        Anel(pR, 38f * ks, st, Cd(D(GamepadButton.RightStick))); Anel(pR, 25f * ks, st * 0.8f, new Color(L.r, L.g, L.b, 0.6f * fade));

        // botões de ação (símbolos coloridos)
        string[] letraX = { "A", "B", "X", "Y" };
        Vector2[] fp = { pFace + new Vector2(0, 38f * ks), pFace + new Vector2(38f * ks, 0), pFace + new Vector2(-38f * ks, 0), pFace + new Vector2(0, -38f * ks) };
        GamepadButton[] fb = { GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North };
        Color[] corPs = { new Color(0.5f, 0.66f, 1f), new Color(1f, 0.45f, 0.47f), new Color(0.96f, 0.56f, 0.88f), new Color(0.42f, 0.92f, 0.65f) };
        Color[] corXb = { new Color(0.45f, 0.85f, 0.4f), new Color(0.95f, 0.38f, 0.33f), new Color(0.4f, 0.65f, 1f), new Color(1f, 0.85f, 0.3f) };
        for (int i = 0; i < 4; i++)
        {
            Anel(fp[i], 20f * ks, st, Cd(D(fb[i])));
            Color cc = ps ? corPs[i] : corXb[i]; cc.a = fade;
            SimboloOuLetra(fp[i], 20f * ks, i, ps, letraX[i], cc, fonte, ks);
        }

        // ---- etiquetas
        string lt = ps ? "L2" : "LT", rt = ps ? "R2" : "RT", lb = ps ? "L1" : "LB", rb = ps ? "R1" : "RB";
        List<Item> itens = new List<Item>();
        System.Action<int, string, string, Vector2, int, GamepadButton, int, Color> add = (t, tag, acao, ponto, lado, btn, sim, cor) =>
            itens.Add(new Item { tipo = t, tag = tag, acao = acao ?? "—", vazio = acao == null, ponto = ponto, lado = lado, destaque = D(btn), sim = sim, cor = cor });

        add(0, lt, AcoesDo(GamepadButton.LeftTrigger), P(350f, 152f), -1, GamepadButton.LeftTrigger, 0, L);
        add(0, lb, AcoesDo(GamepadButton.LeftShoulder), P(330f, 211f), -1, GamepadButton.LeftShoulder, 0, L);
        string sSel = AcoesDo(GamepadButton.Select);
        if (sSel != null || D(GamepadButton.Select)) add(0, ps ? "Share" : "View", sSel, pSel, -1, GamepadButton.Select, 0, L);
        add(1, "L", "Andar", pL, -1, GamepadButton.LeftStick + 1000, 0, L);                 // movimento: sem botão para remapear (nunca fica em destaque)
        string l3 = AcoesDo(GamepadButton.LeftStick); if (l3 != null || D(GamepadButton.LeftStick)) add(1, "L3", l3, pL, -1, GamepadButton.LeftStick, 0, L);
        for (int i = 0; i < 4; i++)
        {
            string a = AcoesDo(dirs[i]); if (a == null && !D(dirs[i])) continue;
            Item it = new Item { tipo = 3, tag = "", acao = a ?? "—", vazio = a == null, ponto = pDir + dv[i] * 28f * ks, lado = -1, destaque = D(dirs[i]), dir = dv[i], cor = L };
            itens.Add(it);
        }
        add(0, rt, AcoesDo(GamepadButton.RightTrigger), P(650f, 152f), 1, GamepadButton.RightTrigger, 0, L);
        add(0, rb, AcoesDo(GamepadButton.RightShoulder), P(670f, 211f), 1, GamepadButton.RightShoulder, 0, L);
        add(0, ps ? "Options" : "Menu", "Pausar", pSta, 1, GamepadButton.Start + 1000, 0, L);
        for (int i = 3; i >= 0; i--) add(ps ? 2 : 1, ps ? "" : letraX[i], AcoesDo(fb[i]), fp[i], 1, fb[i], i, ps ? corPs[i] : corXb[i]);
        add(1, "R", "Câmera", pR, 1, GamepadButton.RightStick + 1000, 0, L);
        string r3 = AcoesDo(GamepadButton.RightStick); if (r3 != null || D(GamepadButton.RightStick)) add(1, "R3", r3, pR, 1, GamepadButton.RightStick, 0, L);

        GUIStyle s2 = new GUIStyle { font = fonte, fontSize = Mathf.RoundToInt(23f * sc), clipping = TextClipping.Overflow };
        float cx = org.x + 500f * sc;
        for (int lado = -1; lado <= 1; lado += 2)
        {
            List<Item> col = itens.FindAll(x => x.lado == lado);
            col.Sort((a, b) => a.ponto.y.CompareTo(b.ponto.y));
            if (col.Count == 0) continue;
            float passo = Mathf.Min(62f * sc, 500f * sc / col.Count);
            float y0 = org.y + 305f * sc - passo * (col.Count - 1) * 0.5f;
            float xb = cx + lado * 262f * sc;                                      // centro da etiqueta redonda/quadrada
            float xc = cx + lado * 214f * K * sc * 1.12f;                          // onde a linha "dobra" (perto da borda do controle)
            for (int i = 0; i < col.Count; i++)
            {
                Item it = col[i]; float y = y0 + passo * i;
                bool d = it.destaque;
                Color cl = d ? gold : new Color(L.r, L.g, L.b, (it.vazio ? 0.35f : 0.7f) * fade);
                // tamanho da etiqueta
                float bw = it.tipo == 0 ? Mathf.Max(46f, it.tag.Length * 13f + 18f) * sc : 38f * sc, bh = (it.tipo == 0 ? 32f : 38f) * sc;
                Rect br = new Rect(xb - bw * 0.5f, y - bh * 0.5f, bw, bh);
                Vector2 ini = new Vector2(lado < 0 ? br.xMax : br.x, y);
                Vector2 dob = new Vector2(xc, y);
                Linha(ini, dob, 1.7f * sc, cl); Linha(dob, it.ponto, 1.7f * sc, cl);
                Anel(it.ponto, 5.5f * sc, 1.8f * sc, cl); Disco(it.ponto, 2.2f * sc, cl);
                // etiqueta
                Color bc = d ? gold : new Color(L.r, L.g, L.b, 0.9f * fade);
                if (it.tipo == 3)
                {
                    Anel(br.center, bh * 0.5f, 1.8f * sc, bc);
                    float ar = bh * 0.1f, cp = bh * 0.3f;
                    Cor(new Rect(br.center.x - ar, br.center.y - cp, ar * 2f, cp * 2f), bc, 2f); Cor(new Rect(br.center.x - cp, br.center.y - ar, cp * 2f, ar * 2f), bc, 2f);
                    Vector2 e = br.center + it.dir * cp * 0.8f; Disco(e, ar * 1.35f, d ? gold : Color.white);
                }
                else if (it.tipo == 0)
                {
                    Cor(new Rect(br.x - 1.5f, br.y - 1.5f, br.width + 3f, br.height + 3f), bc, 8f * sc); Cor(br, FUNDO, 7f * sc);
                    GUIStyle sb = new GUIStyle { font = fonte, fontSize = Mathf.RoundToInt(20f * sc), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
                    GUI.Label(br, it.tag, MenuDePausa.Colorir(sb, d ? gold : new Color(0.92f, 0.94f, 0.98f, fade)));
                }
                else
                {
                    Anel(br.center, bh * 0.5f, 1.8f * sc, bc);
                    if (it.tipo == 2) SimboloOuLetra(br.center, bh * 0.5f, it.sim, true, "", it.cor, fonte, sc);
                    else
                    {
                        GUIStyle sb = new GUIStyle { font = fonte, fontSize = Mathf.RoundToInt((it.tag.Length > 1 ? 17f : 22f) * sc), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
                        GUI.Label(br, it.tag, MenuDePausa.Colorir(sb, d ? gold : (it.cor.r != 1f || it.cor.g != 1f ? it.cor : new Color(0.92f, 0.94f, 0.98f, fade))));
                    }
                }
                // texto da ação
                string txt = it.acao; if (txt.Length > 26) txt = txt.Substring(0, 25) + "…";
                s2.alignment = lado < 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                float tw = 290f * sc;
                Rect rt2 = lado < 0 ? new Rect(br.x - 14f * sc - tw, y - 16f * sc, tw, 32f * sc) : new Rect(br.xMax + 14f * sc, y - 16f * sc, tw, 32f * sc);
                GUI.Label(rt2, txt, MenuDePausa.Colorir(s2, it.vazio ? new Color(0.5f, 0.48f, 0.44f, 0.85f * fade) : d ? gold : new Color(0.95f, 0.93f, 0.88f, fade)));
            }
        }
    }

    // desenha o símbolo do botão: PlayStation = cruz/círculo/quadrado/triângulo; Xbox = letra
    static void SimboloOuLetra(Vector2 c, float raio, int i, bool ps, string letra, Color cor, Font fonte, float ks)
    {
        if (ps)
        {
            Color a = GUI.color; GUI.color = cor; float t = raio * 0.95f;
            GUI.DrawTexture(new Rect(c.x - t, c.y - t, t * 2f, t * 2f), Sim(i)); GUI.color = a;
        }
        else
        {
            GUIStyle sl = new GUIStyle { font = fonte, fontSize = Mathf.RoundToInt(raio * 1.15f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            GUI.Label(new Rect(c.x - raio, c.y - raio, raio * 2f, raio * 2f), letra, MenuDePausa.Colorir(sl, cor));
        }
    }

    // versão pequena (só o contorno do controle, sem etiquetas) para avisos
    public static void Icone(Rect r, float fade)
    {
        Comandos.TipoDeControle tipo = Comandos.Tipo;
        bool ps = tipo == Comandos.TipoDeControle.PlayStation4 || tipo == Comandos.TipoDeControle.PlayStation5;
        Color corpo = tipo == Comandos.TipoDeControle.PlayStation5 ? new Color(0.9f, 0.91f, 0.94f) : new Color(0.2f, 0.21f, 0.25f);
        Color borda = new Color(0.55f, 0.58f, 0.66f);
        Color detalhe = tipo == Comandos.TipoDeControle.PlayStation5 ? new Color(0.15f, 0.16f, 0.2f) : new Color(0.8f, 0.83f, 0.9f);
        float w = r.width, h = r.height;
        System.Action<float> forma = (e) =>
        {
            Color c = e > 0f ? borda : corpo; c.a = fade;
            Disco(new Vector2(r.x + w * 0.22f, r.y + h * 0.66f), h * 0.3f + e, c);
            Disco(new Vector2(r.x + w * 0.78f, r.y + h * 0.66f), h * 0.3f + e, c);
            Cor(new Rect(r.x + w * 0.16f - e, r.y + h * 0.2f - e, w * 0.68f + e * 2f, h * 0.5f + e * 2f), c, h * 0.22f);
        };
        forma(2f); forma(0f);
        Color d = detalhe; d.a = fade;
        Vector2 sL = ps ? new Vector2(r.x + w * 0.38f, r.y + h * 0.66f) : new Vector2(r.x + w * 0.28f, r.y + h * 0.4f);
        Vector2 sR = new Vector2(r.x + w * 0.62f, r.y + h * 0.66f);
        Disco(sL, h * 0.11f, d); Disco(sR, h * 0.11f, d);
        Vector2 dp = ps ? new Vector2(r.x + w * 0.28f, r.y + h * 0.4f) : new Vector2(r.x + w * 0.4f, r.y + h * 0.66f);
        Cor(new Rect(dp.x - h * 0.04f, dp.y - h * 0.11f, h * 0.08f, h * 0.22f), d, 2f); Cor(new Rect(dp.x - h * 0.11f, dp.y - h * 0.04f, h * 0.22f, h * 0.08f), d, 2f);
        Vector2 fc = new Vector2(r.x + w * 0.72f, r.y + h * 0.4f);
        foreach (Vector2 o in new[] { new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0) }) Disco(fc + o * h * 0.1f, h * 0.045f, d);
    }
}
