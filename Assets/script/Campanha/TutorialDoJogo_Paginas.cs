using UnityEngine;

// As páginas do tutorial: Andar, Combate e Ações (com pequenas animações explicando cada comando) e as 3 habilidades do personagem.
public partial class TutorialDoJogo
{
    static float Fr(float x) { return x - Mathf.Floor(x); }
    float Ent(int i) { return Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - trocouEm - 0.09f * i) / 0.4f); }    // entrada escalonada dos elementos

    // ------------------------------------------------------------------ o boneco de treino da página "Andar"
    bool demoReal; Rect botaoSair;
    void AtualizarDemos()
    {
        if (pag != 0) return;
        float dt = Time.unscaledDeltaTime;
        Vector2 real = new Vector2((Comandos.Segurando(Acao.Direita) ? 1f : 0f) - (Comandos.Segurando(Acao.Esquerda) ? 1f : 0f), (Comandos.Segurando(Acao.Frente) ? 1f : 0f) - (Comandos.Segurando(Acao.Tras) ? 1f : 0f));
        demoReal = real.sqrMagnitude > 0.01f;
        if (demoReal) { demoDir = real.normalized; demoCorrendo = Comandos.Segurando(Acao.Correr); }
        else
        {
            float c = Time.unscaledTime % 6.4f; demoCorrendo = false;
            if (c < 1f) demoDir = Vector2.up; else if (c < 2f) demoDir = Vector2.right; else if (c < 3f) demoDir = Vector2.down; else if (c < 4f) demoDir = Vector2.left;
            else if (c < 5.6f) { demoDir = new Vector2(1f, 0.6f).normalized; demoCorrendo = true; } else demoDir = Vector2.zero;
        }
        if (demoDir.sqrMagnitude > 0.01f) { tokFace = Vector2.Lerp(tokFace, demoDir, 1f - Mathf.Exp(-12f * dt)); tokPos += demoDir * (demoCorrendo ? 1.2f : 0.6f) * dt; }
        tokPos.x = Mathf.Clamp(tokPos.x, -0.85f, 0.85f); tokPos.y = Mathf.Clamp(tokPos.y, -0.78f, 0.78f);
        if (!demoReal && demoDir == Vector2.zero) tokPos = Vector2.Lerp(tokPos, Vector2.zero, 1f - Mathf.Exp(-2.2f * dt));      // volta ao meio
    }

    // ------------------------------------------------------------------ pecinhas de desenho
    void Token(Vector2 c, float r, Vector2 face, float al, Color cor)
    {
        Disco(c.x, c.y + r * 0.85f, r * 0.9f, new Color(0f, 0f, 0f, 0.35f * al));
        Disco(c.x, c.y, r, new Color(cor.r * 0.5f, cor.g * 0.45f, cor.b * 0.35f, al)); Disco(c.x, c.y, r * 0.83f, new Color(cor.r, cor.g, cor.b, al));
        Vector2 n = face.sqrMagnitude > 0.01f ? face.normalized : Vector2.down;
        Disco(c.x + n.x * r * 0.55f, c.y + n.y * r * 0.55f, r * 0.3f, new Color(1f, 0.97f, 0.86f, al));
    }
    void Onda(Vector2 c, float k, float raioMax, Color cor)
    {
        if (k <= 0f || k >= 1f) return; float r = raioMax * k;
        GUI.color = new Color(cor.r, cor.g, cor.b, cor.a * (1f - k)); GUI.DrawTexture(new Rect(c.x - r, c.y - r, 2f * r, 2f * r), IconesDeHabilidade.Anel); GUI.color = Color.white;
    }
    void Arco(Vector2 c, float raio, float a0, float a1, float esp, Color cor)
    {
        for (int i = 0; i <= 16; i++) { float a = Mathf.Lerp(a0, a1, i / 16f) * Mathf.Deg2Rad; Disco(c.x + Mathf.Cos(a) * raio, c.y + Mathf.Sin(a) * raio, esp * 0.5f, cor); }
    }
    void Painel(Rect r, float e, Color ouro, float al, float rad = 18f)
    {
        RR(new Rect(r.x - 2f * e, r.y - 2f * e, r.width + 4f * e, r.height + 4f * e), rad * e + 2f * e, new Color(ouro.r, ouro.g, ouro.b, 0.4f * al));
        RR(r, rad * e, new Color(0.04f, 0.037f, 0.05f, 0.88f * al));
    }
    void Etiqueta(Rect r, string t, Color cor, float e, float al)
    {
        RR(r, 12f * e, new Color(cor.r * 0.9f, cor.g * 0.9f, cor.b * 0.9f, 0.7f * al)); RR(new Rect(r.x + 2f * e, r.y + 2f * e, r.width - 4f * e, r.height - 4f * e), 10f * e, new Color(0.06f, 0.06f, 0.08f, 0.92f * al));
        Disco(r.x + 26f * e, r.center.y, 8f * e, new Color(cor.r, cor.g, cor.b, al));
        Texto(new Rect(r.x + 46f * e, r.y, r.width - 54f * e, r.height), t, fFell, 28f * e, TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.85f, al));
    }
    // ornamento com um brilho que percorre a linha
    void OrnamentoVivo(float x, float y, float w, float e, Color c)
    {
        Ornamento(x, y, w, e, c);
        float k = Fr(Time.unscaledTime * 0.35f); float bx = x + w * k; float a = Mathf.Sin(k * Mathf.PI);
        Brilho(new Rect(bx - 40f * e, y - 38f * e, 80f * e, 80f * e), new Color(1f, 0.95f, 0.7f, 0.5f * a * c.a));
    }

    // ------------------------------------------------------------------ a tela
    void OnGUI()
    {
        if (fechado) return;
        GUI.depth = -9100;
        Event ev = Event.current; float W = Screen.width, H = Screen.height, e = H / 1080f;
        if (configAberta)                                                  // as configurações de comandos (as mesmas do menu de pausa)
        {
            if (ev.type == EventType.Repaint) { if (fundo != null) { GUI.DrawTexture(new Rect(0, 0, W, H), fundo, ScaleMode.StretchToFill, false); } R(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.7f)); }
            config.Desenhar(new Rect(0, 0, W, H), e, 1f, ev, fFell, fCinzel); GUI.color = Color.white; return;
        }
        if (pronto && ev.type == EventType.MouseDown && ev.button == 0)
        {
            for (int i = 0; i < Paginas; i++) if (tiles[i].Contains(ev.mousePosition)) { IrParaPagina(i); ev.Use(); break; }
            if (botaoConfig.Contains(ev.mousePosition)) { AbrirConfig(); ev.Use(); }
            else if (botaoSair.Contains(ev.mousePosition)) { Fechar(); ev.Use(); }
        }
        if (ev.type == EventType.KeyDown || ev.type == EventType.KeyUp) ev.Use();
        if (ev.type != EventType.Repaint || !pronto) return;
        float al = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - abriu) / 0.4f);
        Color ouro = ficha != null ? ficha.corDestaque : new Color(0.86f, 0.68f, 0.34f); ouro.a = 1f;
        Color ouroClaro = Color.Lerp(ouro, new Color(1f, 0.93f, 0.7f), 0.55f);

        if (fundo != null) { GUI.color = new Color(0.6f, 0.6f, 0.65f, 1f); GUI.DrawTexture(new Rect(0, 0, W, H), fundo, ScaleMode.StretchToFill, false); GUI.color = Color.white; }
        else R(new Rect(0, 0, W, H), new Color(0.03f, 0.03f, 0.04f, 1f));
        R(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.03f, 0.55f * al));
        Matrix4x4 m0 = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), new Vector2(W * 0.17f, 0f));
        GUI.color = new Color(0.01f, 0.01f, 0.015f, 0.92f * al); GUI.DrawTexture(new Rect(0, 0, W * 0.34f, H), gradH, ScaleMode.StretchToFill, false);
        GUI.matrix = m0;
        GUIUtility.ScaleAroundPivot(new Vector2(1f, -1f), new Vector2(0f, H * 0.91f));
        GUI.color = new Color(0f, 0f, 0f, 0.88f * al); GUI.DrawTexture(new Rect(0, H * 0.82f, W, H * 0.18f), gradV, ScaleMode.StretchToFill, false);
        GUI.matrix = m0; GUI.color = Color.white;

        float L = W * 0.055f;
        Texto(new Rect(L, H * 0.04f, 700f * e, 46f * e), "COMO JOGAR", fCinzel, 34f * e, TextAnchor.MiddleLeft, new Color(ouroClaro.r, ouroClaro.g, ouroClaro.b, al));
        if (ficha != null) Texto(new Rect(L, H * 0.04f + 44f * e, 700f * e, 36f * e), ficha.nome + (string.IsNullOrEmpty(ficha.titulo) ? "" : "  ·  " + ficha.titulo), fFell, 27f * e, TextAnchor.MiddleLeft, new Color(0.92f, 0.88f, 0.78f, 0.95f * al), false, FontStyle.Italic);
        // botão SAIR DO TUTORIAL (canto de cima, à direita) + contador de páginas
        float sw = 330f * e; botaoSair = new Rect(W - L - sw, H * 0.035f, sw, 56f * e);
        bool sobreSair = botaoSair.Contains(ev.mousePosition);
        RR(botaoSair, 14f * e, new Color(0.04f, 0.037f, 0.05f, (sobreSair ? 0.95f : 0.75f) * al)); Aro(botaoSair, 14f * e, 1.8f * e, new Color(ouro.r, ouro.g, ouro.b, (sobreSair ? 1f : 0.55f) * al));
        Texto(new Rect(botaoSair.x + 22f * e, botaoSair.y, sw - 110f * e, botaoSair.height), "SAIR DO TUTORIAL", fCinzel, 21f * e, TextAnchor.MiddleLeft, new Color(0.97f, 0.93f, 0.82f, al), false, FontStyle.Normal, false);
        Tecla(new Rect(botaoSair.xMax - 78f * e, botaoSair.y + 6f * e, 64f * e, 44f * e), "Esc", false, e, ouro, 20f * e);
        Texto(new Rect(botaoSair.x - 130f * e, H * 0.035f, 110f * e, 56f * e), (pag + 1) + " / " + Paginas, fCinzel, 26f * e, TextAnchor.MiddleRight, new Color(0.85f, 0.8f, 0.68f, 0.85f * al));

        DesenharColunaDeIcones(L, H * 0.185f, e, ouro, ouroClaro, al, ev.mousePosition);
        float dx = W * 0.245f; GUI.color = new Color(ouro.r, ouro.g, ouro.b, 0.5f * al); GUI.DrawTexture(new Rect(dx, H * 0.14f, 2f * e, H * 0.68f), gradV, ScaleMode.StretchToFill, false); GUI.color = Color.white;

        float X0 = W * 0.275f, CW = W * 0.945f - X0, Y0 = H * 0.115f;
        if (pag == 0) PaginaAndar(X0, Y0, CW, H, e, ouro, ouroClaro, al);
        else if (pag == 1) PaginaCombate(X0, Y0, CW, H, e, ouro, ouroClaro, al);
        else if (pag == 2) PaginaAcoes(X0, Y0, CW, H, e, ouro, ouroClaro, al);
        else DesenharHabilidade(X0, Y0 + 12f * e, CW, e, ouro, ouroClaro, al, pag - NB);

        DesenharBarraDeComandos(W, H, e, ouro, al);
        GUI.color = Color.white;
    }

    // ------------------------------------------------------------------ coluna de ícones
    void DesenharColunaDeIcones(float x, float y, float e, Color ouro, Color ouroClaro, float al, Vector2 mouse)
    {
        float tam = 76f * e, passo = 100f * e;
        Texto(new Rect(x, y - 40f * e, 360f * e, 30f * e), "BÁSICO", fCinzel, 20f * e, TextAnchor.MiddleLeft, new Color(ouro.r, ouro.g, ouro.b, 0.9f * al), false, FontStyle.Normal, false);
        R(new Rect(x, y - 10f * e, 340f * e, 1.5f * e), new Color(ouro.r, ouro.g, ouro.b, 0.45f * al));
        string[] nomes = { "Andar", "Combate", "Ações" };
        for (int i = 0; i < Paginas; i++)
        {
            float py = y + i * passo + (i >= NB ? 78f * e : 0f);
            if (i == NB) { Texto(new Rect(x, py - 44f * e, 380f * e, 30f * e), "HABILIDADES ESPECIAIS", fCinzel, 20f * e, TextAnchor.MiddleLeft, new Color(ouro.r, ouro.g, ouro.b, 0.9f * al), false, FontStyle.Normal, false); R(new Rect(x, py - 14f * e, 340f * e, 1.5f * e), new Color(ouro.r, ouro.g, ouro.b, 0.45f * al)); }
            float sel = realce[i]; tiles[i] = new Rect(x - 8f * e, py - 8f * e, 410f * e, tam + 16f * e);
            bool sobre = tiles[i].Contains(mouse); float lev = (sobre ? 0.5f : 0f) + sel; float aum = 1f + 0.08f * Mathf.Min(1f, lev);
            float sz = tam * aum; Rect t = new Rect(x + (tam - sz) * 0.5f, py + (tam - sz) * 0.5f - 3f * e * Mathf.Min(1f, lev), sz, sz);
            Color base_ = i < NB ? new Color(0.62f, 0.5f, 0.22f) : Color.Lerp(ouro, new Color(0.3f, 0.3f, 0.35f), 0.35f);
            if (sel > 0.01f) Brilho(new Rect(t.x - 44f * e, t.y - 44f * e, t.width + 88f * e, t.height + 88f * e), new Color(ouro.r, ouro.g, ouro.b, (0.45f + 0.12f * Mathf.Sin(Time.unscaledTime * 3f)) * sel * al));
            RR(new Rect(t.x - 4f * e, t.y - 4f * e, t.width + 8f * e, t.height + 8f * e), 12f * e, Color.Lerp(new Color(0.42f, 0.36f, 0.24f, al), new Color(1f, 0.9f, 0.55f, al), Mathf.Min(1f, lev)));
            RR(t, 9f * e, new Color(base_.r * 0.38f, base_.g * 0.38f, base_.b * 0.38f, al));
            RR(new Rect(t.x + 3f * e, t.y + 3f * e, t.width - 6f * e, t.height * 0.5f), 8f * e, new Color(base_.r * 0.75f, base_.g * 0.75f, base_.b * 0.75f, 0.5f * al));
            if (i == 0) Texto(t, "W\nASD", fCinzel, 22f * e * aum, TextAnchor.MiddleCenter, new Color(1f, 0.96f, 0.85f, al));
            else if (i == 1) { RR(new Rect(t.center.x - 15f * e, t.center.y - 24f * e, 30f * e, 48f * e), 14f * e, new Color(1f, 0.96f, 0.85f, al)); R(new Rect(t.center.x - 1f * e, t.center.y - 22f * e, 2f * e, 18f * e), new Color(0.2f, 0.18f, 0.15f, al)); }
            else if (i == 2) { Matrix4x4 mm = GUI.matrix; GUIUtility.RotateAroundPivot(45f, t.center); R(new Rect(t.center.x - 15f * e, t.center.y - 15f * e, 30f * e, 30f * e), new Color(1f, 0.96f, 0.85f, al)); GUI.matrix = mm; R(new Rect(t.center.x - 6f * e, t.center.y - 6f * e, 12f * e, 12f * e), new Color(base_.r * 0.4f, base_.g * 0.4f, base_.b * 0.4f, al)); }
            else { GUI.color = new Color(1f, 0.97f, 0.88f, al); GUI.DrawTexture(new Rect(t.x + 10f * e, t.y + 10f * e, t.width - 20f * e, t.height - 20f * e), IconesDeHabilidade.Obter(hab.classe, i - NB), ScaleMode.ScaleToFit, true); GUI.color = Color.white; }
            string nome = i < NB ? nomes[i] : (hab.Dados(i - NB) != null ? hab.Dados(i - NB).nome : "");
            Texto(new Rect(x + tam + 26f * e, py, 270f * e, i < NB ? tam : tam * 0.62f), nome, fCinzel, 23f * e, TextAnchor.MiddleLeft, Color.Lerp(new Color(0.7f, 0.67f, 0.6f, al), new Color(1f, 0.95f, 0.8f, al), Mathf.Min(1f, lev)), true);
            if (i >= NB) { string tec = Comandos.Dica(Acao.Habilidade1 + (i - NB)); float kw = LarguraTecla(tec, e, 48f * e, 22f * e); Tecla(new Rect(x + tam + 26f * e, py + tam * 0.6f, kw, 42f * e), tec, false, e, ouro, 22f * e); }
        }
        // botão de CONFIGURAÇÕES (a mesma tela do menu de pausa: teclas, controle, vídeo e áudio)
        float yb = y + (Paginas - 1) * passo + 78f * e + tam + 36f * e;
        botaoConfig = new Rect(x - 4f * e, yb, 384f * e, 62f * e); bool sc = botaoConfig.Contains(mouse);
        RR(botaoConfig, 14f * e, new Color(ouro.r, ouro.g, ouro.b, (sc ? 0.34f : 0.14f) * al)); Aro(botaoConfig, 14f * e, 1.8f * e, new Color(ouro.r, ouro.g, ouro.b, (sc ? 1f : 0.6f) * al));
        Tecla(new Rect(botaoConfig.x + 14f * e, botaoConfig.y + 7f * e, 48f * e, 46f * e), "C", false, e, ouro, 22f * e);
        Texto(new Rect(botaoConfig.x + 76f * e, botaoConfig.y, 300f * e, botaoConfig.height), "CONFIGURAÇÕES", fCinzel, 22f * e, TextAnchor.MiddleLeft, new Color(0.97f, 0.93f, 0.82f, al), false, FontStyle.Normal, false);
    }

    // ------------------------------------------------------------------ barra de comandos
    void DesenharBarraDeComandos(float W, float H, float e, Color ouro, float al)
    {
        float y = H - 92f * e, cx = W * 0.5f;
        for (int i = 0; i < Paginas; i++) Disco(cx + (i - (Paginas - 1) * 0.5f) * 28f * e, y - 24f * e, (i == pag ? 7f : 4.5f) * e, i == pag ? new Color(ouro.r, ouro.g, ouro.b, al) : new Color(1f, 1f, 1f, 0.3f * al));
        float x = W * 0.055f;
        x = Prompt(x, y, e, ouro, al, new[] { "←", "→" }, "Trocar página");
        x = Prompt(x + 46f * e, y, e, ouro, al, new[] { "Enter" }, pag < Paginas - 1 ? "Continuar" : "Começar a aventura");
        Prompt(x + 46f * e, y, e, ouro, al, new[] { "Esc" }, "Fechar");
    }

    float Prompt(float x, float y, float e, Color ouro, float al, string[] teclas, string rotulo)
    {
        GUI.color = new Color(1f, 1f, 1f, al);
        foreach (string k in teclas) { float w = LarguraTecla(k, e, 56f * e, 26f * e); Tecla(new Rect(x, y, w, 56f * e), k, false, e, ouro, 26f * e); x += w + 10f * e; }
        float lw = Est(fFell, 28f * e, TextAnchor.MiddleLeft).CalcSize(new GUIContent(rotulo)).x + 8f * e;
        Texto(new Rect(x + 6f * e, y - 2f * e, lw, 56f * e), rotulo, fFell, 28f * e, TextAnchor.MiddleLeft, new Color(0.94f, 0.9f, 0.8f, al));
        GUI.color = Color.white; return x + 6f * e + lw;
    }

    // título da página: categoria, nome grande, ornamento e uma frase curta. Devolve o y onde o conteúdo começa.
    float Cabecalho(float x, float y, float w, float e, Color ouro, Color ouroClaro, string categoria, string titulo, string frase)
    {
        float a0 = Ent(0), a1 = Ent(1), a2 = Ent(2);
        Texto(new Rect(x, y + (1f - a0) * 14f * e, w, 34f * e), categoria, fCinzel, 22f * e, TextAnchor.MiddleLeft, new Color(ouro.r, ouro.g, ouro.b, a0), false, FontStyle.Normal, false);
        Texto(new Rect(x, y + 36f * e + (1f - a1) * 14f * e, w, 80f * e), titulo, fCinzel, 62f * e, TextAnchor.MiddleLeft, new Color(0.98f, 0.93f, 0.8f, a1));
        OrnamentoVivo(x, y + 128f * e, 480f * e * a1, e, new Color(ouroClaro.r, ouroClaro.g, ouroClaro.b, a1));
        Texto(new Rect(x, y + 146f * e + (1f - a2) * 10f * e, w, 44f * e), frase, fFell, 30f * e, TextAnchor.MiddleLeft, new Color(0.92f, 0.88f, 0.78f, a2), false, FontStyle.Italic);
        return y + 214f * e;
    }

    // ------------------------------------------------------------------ página 1: ANDAR
    void PaginaAndar(float x, float y, float w, float H, float e, Color ouro, Color ouroClaro, float al)
    {
        float cy0 = Cabecalho(x, y, w, e, ouro, ouroClaro, "BÁSICO  ·  1 de 3", "ANDAR", "Use as quatro teclas. O personagem anda para onde você mandar.");
        float k = 124f * e, gap = 12f * e;
        bool real = demoReal, auto = !demoReal;
        bool kw = Comandos.Segurando(Acao.Frente) || (auto && demoDir.y > 0.3f), ks = Comandos.Segurando(Acao.Tras) || (auto && demoDir.y < -0.3f);
        bool ka = Comandos.Segurando(Acao.Esquerda) || (auto && demoDir.x < -0.3f), kd = Comandos.Segurando(Acao.Direita) || (auto && demoDir.x > 0.3f);
        float a = Ent(3), cx = x + 20f * e, cy = cy0 + 20f * e + (1f - a) * 30f * e;
        Tecla(new Rect(cx + k + gap, cy, k, k), Comandos.Dica(Acao.Frente), kw, e, ouro, 46f * e);
        Tecla(new Rect(cx, cy + k + gap, k, k), Comandos.Dica(Acao.Esquerda), ka, e, ouro, 46f * e);
        Tecla(new Rect(cx + k + gap, cy + k + gap, k, k), Comandos.Dica(Acao.Tras), ks, e, ouro, 46f * e);
        Tecla(new Rect(cx + 2f * (k + gap), cy + k + gap, k, k), Comandos.Dica(Acao.Direita), kd, e, ouro, 46f * e);
        Texto(new Rect(cx, cy + 2f * k + gap + 22f * e, 3f * k + 2f * gap, 36f * e), "frente · esquerda · trás · direita", fFell, 26f * e, TextAnchor.MiddleCenter, new Color(0.88f, 0.84f, 0.74f, a), false, FontStyle.Italic);
        // correr
        float b = Ent(4); string tc = Comandos.Dica(Acao.Correr); float kwid = LarguraTecla(tc, e, 190f * e, 34f * e);
        bool corr = Comandos.Segurando(Acao.Correr) || (auto && demoCorrendo);
        Tecla(new Rect(cx, cy + 2f * k + gap + 90f * e + (1f - b) * 20f * e, kwid, 76f * e), tc, corr, e, ouro, 34f * e);
        Texto(new Rect(cx + kwid + 20f * e, cy + 2f * k + gap + 90f * e, 360f * e, 76f * e), "segure para correr", fFell, 32f * e, TextAnchor.MiddleLeft, new Color(0.94f, 0.9f, 0.8f, b));
        // campo de treino: o bonequinho obedece as teclas (as de verdade ou as da demonstração)
        float c2 = Ent(3); float fw = w * 0.4f, fh = fw * 0.82f; Rect campo = new Rect(x + w - fw - 10f * e, cy0 + 6f * e + (1f - c2) * 30f * e, fw, fh);
        Painel(campo, e, ouro, al * c2);
        for (int i = 1; i < 6; i++) { R(new Rect(campo.x + campo.width * i / 6f, campo.y + 14f * e, 1.5f * e, campo.height - 28f * e), new Color(1f, 1f, 1f, 0.06f * c2)); R(new Rect(campo.x + 14f * e, campo.y + campo.height * i / 6f, campo.width - 28f * e, 1.5f * e), new Color(1f, 1f, 1f, 0.06f * c2)); }
        Vector2 p = new Vector2(campo.center.x + tokPos.x * (campo.width * 0.5f - 40f * e), campo.center.y - tokPos.y * (campo.height * 0.5f - 40f * e));
        if (demoCorrendo || corr) for (int i = 1; i <= 5; i++) { Vector2 d = new Vector2(-tokFace.x, tokFace.y) * 22f * e * i; Disco(p.x + d.x, p.y + d.y, 26f * e * (1f - i * 0.15f), new Color(1f, 0.9f, 0.6f, 0.18f * (1f - i * 0.18f) * c2)); }
        Token(p, 30f * e, new Vector2(tokFace.x, -tokFace.y), c2, new Color(0.95f, 0.8f, 0.4f));
        Texto(new Rect(campo.x, campo.yMax - 54f * e, campo.width, 40f * e), real ? "Você está no controle!" : "demonstração", fFell, 26f * e, TextAnchor.MiddleCenter, new Color(real ? 1f : 0.75f, real ? 0.88f : 0.72f, real ? 0.5f : 0.62f, c2), false, FontStyle.Italic);
    }

    // ------------------------------------------------------------------ página 2: COMBATE (o mouse clica sozinho)
    void PaginaCombate(float x, float y, float w, float H, float e, Color ouro, Color ouroClaro, float al)
    {
        float cy0 = Cabecalho(x, y, w, e, ouro, ouroClaro, "BÁSICO  ·  2 de 3", "COMBATE", "O mouse ataca e defende. Veja o que cada botão faz.");
        float t = Time.unscaledTime, c = t % 6f;
        bool realE = Comandos.Segurando(Acao.Atacar), realD = Comandos.Segurando(Acao.Defender);
        float pAtk = -1f, pDef = -1f;
        if (realE) pAtk = Fr(t * 2.2f); else if (realD) pDef = Fr(t * 0.9f);
        else { if (c >= 0.5f && c < 1.1f) pAtk = (c - 0.5f) / 0.6f; else if (c >= 1.9f && c < 2.5f) pAtk = (c - 1.9f) / 0.6f; else if (c >= 3.3f && c < 5.3f) pDef = (c - 3.3f) / 2f; }
        bool esq = pAtk >= 0f, dir = pDef >= 0f;
        // o mouse: os botões afundam, acendem e soltam "faíscas de clique"
        float a = Ent(3), mw = 150f * e, mh = 240f * e, mx = x + 70f * e, my = cy0 + 34f * e + (1f - a) * 30f * e;
        float pE = realE ? 1f : Pressao(pAtk), pD = realD ? 1f : Pressao(pDef < 0f ? -1f : Fr(pDef));
        DesenharMouse(mx, my, mw, mh, pE, pD, a, ouro);
        if (pAtk >= 0f && pAtk < 0.5f && !realE) Onda(new Vector2(mx + mw * 0.26f, my + mh * 0.2f), pAtk * 2f, 80f * e, new Color(1f, 0.9f, 0.5f, a));
        // as três linhas de comandos (nome da ação em destaque, a explicação pequena embaixo)
        float ly = my + mh + 40f * e, b = Ent(4), lw = 400f * e;
        LinhaCmd(new Rect(x, ly, lw, 74f * e), 0, "", Comandos.NomeCurto(Acao.Atacar), "botão esquerdo do mouse", esq, ouro, e, b);
        LinhaCmd(new Rect(x, ly + 84f * e, lw, 74f * e), 1, "", Comandos.NomeCurto(Acao.Defender), "botão direito do mouse", dir, ouro, e, b);
        LinhaCmd(new Rect(x, ly + 168f * e, lw, 74f * e), 2, Comandos.Dica(Acao.GuardarSacar), Comandos.NomeCurto(Acao.GuardarSacar), "tecla " + Comandos.Dica(Acao.GuardarSacar), Comandos.Segurando(Acao.GuardarSacar), ouro, e, b);
        // o palco do treino: o boneco bate e se defende
        float c2 = Ent(3); float fw = w * 0.5f, fh = fw * 0.6f; Rect palco = new Rect(x + w - fw - 10f * e, cy0 + 10f * e + (1f - c2) * 30f * e, fw, fh);
        Painel(palco, e, ouro, al * c2);
        float cho = palco.y + palco.height * 0.72f; R(new Rect(palco.x + 18f * e, cho, palco.width - 36f * e, 2f * e), new Color(1f, 1f, 1f, 0.12f * c2));
        float r = 38f * e; Vector2 heroi = new Vector2(palco.x + palco.width * 0.3f, cho - r), alvo = new Vector2(palco.x + palco.width * 0.7f, cho - r);
        if (pAtk >= 0f) heroi.x += Mathf.Sin(pAtk * Mathf.PI) * palco.width * 0.1f;
        float golpeInim = pDef >= 0f ? Fr(pDef * 2f) : -1f;
        if (golpeInim >= 0f) alvo.x -= Mathf.Sin(golpeInim * Mathf.PI) * palco.width * 0.1f;
        bool acertou = pAtk > 0.4f && pAtk < 0.85f;
        Vector2 sac = acertou ? new Vector2(Mathf.Sin(t * 80f), 0f) * 5f * e : Vector2.zero;
        Token(heroi, r, Vector2.right, c2, new Color(0.95f, 0.8f, 0.4f));
        Token(alvo + sac, r, Vector2.left, c2, acertou ? new Color(1f, 0.35f, 0.3f) : new Color(0.62f, 0.6f, 0.66f));
        if (pAtk >= 0.25f && pAtk <= 0.8f) { float s = (pAtk - 0.25f) / 0.55f; Arco(heroi, r * 1.9f, Mathf.Lerp(-70f, 60f, s), Mathf.Lerp(-30f, 80f, s), 11f * e, new Color(1f, 0.95f, 0.7f, (1f - s) * c2)); }
        if (pAtk > 0.45f) Onda(alvo, Mathf.Clamp01((pAtk - 0.45f) * 2.2f), 80f * e, new Color(1f, 0.6f, 0.4f, c2));
        if (pDef >= 0f)
        {
            float sh = Mathf.Min(1f, pDef * 6f) * c2; Arco(heroi, r * 1.8f, -62f, 62f, 13f * e, new Color(0.55f, 0.78f, 1f, sh));
            if (golpeInim > 0.4f && golpeInim < 0.8f) Onda(heroi + new Vector2(r * 1.8f, 0f), (golpeInim - 0.4f) * 2.5f, 70f * e, new Color(0.7f, 0.9f, 1f, c2));
        }
        Texto(new Rect(palco.x, palco.yMax - 54f * e, palco.width, 40f * e), esq ? "ATACAR" : dir ? "DEFENDER" : "demonstração", fCinzel, 24f * e, TextAnchor.MiddleCenter, esq ? new Color(1f, 0.88f, 0.5f, c2) : dir ? new Color(0.6f, 0.82f, 1f, c2) : new Color(0.75f, 0.72f, 0.62f, c2), false, FontStyle.Normal, false);
    }

    // curva do clique: desce rápido, segura e solta (p de 0 a 1; p < 0 = sem clique)
    static float Pressao(float p)
    {
        if (p < 0f) return 0f;
        return Mathf.SmoothStep(0f, 1f, p / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (p - 0.62f) / 0.3f));
    }

    void Aro(Rect r, float rad, float esp, Color c)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, new Vector4(esp, esp, esp, esp), new Vector4(rad, rad, rad, rad));
    }

    // traços que saem de um ponto para cima (efeito de "clique")
    void RaiosDeClique(Vector2 c, float k, float e, Color cor)
    {
        if (k <= 0.02f) return;
        for (int i = -1; i <= 1; i++)
        {
            Matrix4x4 m = GUI.matrix; GUIUtility.RotateAroundPivot(-90f + i * 38f, c);
            float ini = 24f * e + 6f * e * (1f - k), len = 16f * e * k;
            GUI.color = new Color(cor.r, cor.g, cor.b, cor.a * k); GUI.DrawTexture(new Rect(c.x + ini, c.y - 2f * e, len, 4f * e), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(cor.r, cor.g, cor.b, cor.a * k), Vector4.zero, new Vector4(2f * e, 2f * e, 2f * e, 2f * e));
            GUI.color = Color.white; GUI.matrix = m;
        }
    }

    void DesenharMouse(float mx, float my, float mw, float mh, float pE, float pD, float a, Color ouro)
    {
        float e = Screen.height / 1080f, rad = mw * 0.5f;
        // cabo
        Arco(new Vector2(mx + mw * 0.5f, my - 30f * e), 30f * e, 180f, 360f, 5f * e, new Color(0.35f, 0.33f, 0.36f, a));
        RR(new Rect(mx + 8f * e, my + 18f * e, mw, mh), rad, new Color(0f, 0f, 0f, 0.4f * a));                                    // sombra
        RR(new Rect(mx - 3f * e, my - 3f * e, mw + 6f * e, mh + 6f * e), rad + 3f * e, new Color(0.58f, 0.52f, 0.42f, a));       // borda clara
        RR(new Rect(mx, my, mw, mh), rad, new Color(0.115f, 0.11f, 0.13f, a));
        RR(new Rect(mx + 10f * e, my + mh * 0.5f, mw - 20f * e, mh * 0.44f), rad - 10f * e, new Color(1f, 1f, 1f, 0.035f * a));    // reflexo embaixo
        float bw = mw * 0.5f - 9f * e, bh = mh * 0.44f; Color neutro = new Color(0.27f, 0.26f, 0.3f, a);
        Rect bl = new Rect(mx + 6f * e, my + 6f * e + pE * 6f * e, bw, bh - pE * 6f * e), br = new Rect(mx + mw * 0.5f + 3f * e, my + 6f * e + pD * 6f * e, bw, bh - pD * 6f * e);
        if (pE > 0.05f) Brilho(new Rect(bl.x - 40f * e, bl.y - 40f * e, bl.width + 80f * e, bl.height + 80f * e), new Color(ouro.r, ouro.g, ouro.b, 0.55f * pE * a));
        if (pD > 0.05f) Brilho(new Rect(br.x - 40f * e, br.y - 40f * e, br.width + 80f * e, br.height + 80f * e), new Color(0.5f, 0.75f, 1f, 0.55f * pD * a));
        RRc(bl, new Vector4(rad - 6f * e, 8f * e, 8f * e, 8f * e), Color.Lerp(neutro, new Color(ouro.r, ouro.g, ouro.b, a), pE));
        RRc(br, new Vector4(8f * e, rad - 6f * e, 8f * e, 8f * e), Color.Lerp(neutro, new Color(0.45f, 0.7f, 1f, a), pD));
        RRc(new Rect(bl.x + 4f * e, bl.y + 4f * e, bl.width - 8f * e, bl.height * 0.45f), new Vector4(rad - 10f * e, 6f * e, 4f * e, 4f * e), new Color(1f, 1f, 1f, 0.12f * a));
        RRc(new Rect(br.x + 4f * e, br.y + 4f * e, br.width - 8f * e, br.height * 0.45f), new Vector4(6f * e, rad - 10f * e, 4f * e, 4f * e), new Color(1f, 1f, 1f, 0.12f * a));
        RR(new Rect(mx + mw * 0.5f - 7f * e, my + 30f * e, 14f * e, 46f * e), 7f * e, new Color(0.5f, 0.46f, 0.4f, a));               // rodinha
        RaiosDeClique(new Vector2(bl.center.x, bl.y - 2f * e), pE, e, new Color(1f, 0.9f, 0.5f, a));
        RaiosDeClique(new Vector2(br.center.x, br.y - 2f * e), pD, e, new Color(0.6f, 0.82f, 1f, a));
    }

    // uma linha da lista de comandos: ícone pequeno + nome da ação + explicação
    void LinhaCmd(Rect r, int glifo, string tecla, string acao, string legenda, bool ativa, Color ouro, float e, float al)
    {
        RR(r, 16f * e, ativa ? new Color(ouro.r, ouro.g, ouro.b, 0.32f * al) : new Color(0.04f, 0.037f, 0.05f, 0.72f * al));
        Aro(r, 16f * e, 1.5f * e, new Color(ouro.r, ouro.g, ouro.b, (ativa ? 0.9f : 0.3f) * al));
        Rect g = new Rect(r.x + 18f * e, r.y + 10f * e, 54f * e, r.height - 20f * e);
        if (glifo == 2) Tecla(new Rect(g.x, g.y + 4f * e, g.width, g.height - 6f * e), tecla, ativa, e, ouro, 26f * e);
        else
        {
            RR(new Rect(g.center.x - 15f * e, g.y, 30f * e, g.height), 15f * e, new Color(0.62f, 0.56f, 0.45f, al)); RR(new Rect(g.center.x - 13f * e, g.y + 2f * e, 26f * e, g.height - 4f * e), 13f * e, new Color(0.13f, 0.125f, 0.145f, al));
            RRc(new Rect(g.center.x - 13f * e, g.y + 2f * e, 13f * e, g.height * 0.42f), new Vector4(13f * e, 2f * e, 2f * e, 2f * e), glifo == 0 && ativa ? new Color(ouro.r, ouro.g, ouro.b, al) : new Color(0.3f, 0.29f, 0.33f, al));
            RRc(new Rect(g.center.x, g.y + 2f * e, 13f * e, g.height * 0.42f), new Vector4(2f * e, 13f * e, 2f * e, 2f * e), glifo == 1 && ativa ? new Color(0.45f, 0.7f, 1f, al) : new Color(0.3f, 0.29f, 0.33f, al));
        }
        Texto(new Rect(r.x + 90f * e, r.y + 6f * e, r.width - 100f * e, 36f * e), acao, fCinzel, 26f * e, TextAnchor.MiddleLeft, ativa ? new Color(1f, 0.93f, 0.65f, al) : new Color(0.97f, 0.93f, 0.82f, al), false, FontStyle.Normal, false);
        Texto(new Rect(r.x + 90f * e, r.y + 40f * e, r.width - 100f * e, 28f * e), legenda, fFell, 22f * e, TextAnchor.MiddleLeft, new Color(0.78f, 0.74f, 0.64f, al), false, FontStyle.Italic, false);
    }

    // ------------------------------------------------------------------ página 3: AÇÕES (4 miniaturas animadas)
    void PaginaAcoes(float x, float y, float w, float H, float e, Color ouro, Color ouroClaro, float al)
    {
        float cy0 = Cabecalho(x, y, w, e, ouro, ouroClaro, "BÁSICO  ·  3 de 3", "AÇÕES", "Quatro comandos úteis. Cada quadro mostra como funciona.");
        Acao[] ac = { Acao.Pular, Acao.Rolar, Acao.Interagir, Acao.Mapa };
        string[] frases = { "Pula obstáculos.", "Desvia dos golpes.", "Fala com as pessoas.", "Abre o mapa do mundo." };
        float gap = 26f * e, tw = (w - gap) / 2f, th = (H * 0.8f - cy0 - gap) / 2f; float t = Time.unscaledTime;
        for (int i = 0; i < 4; i++)
        {
            float a = Ent(3 + i); Rect r = new Rect(x + (i % 2) * (tw + gap), cy0 + (i / 2) * (th + gap) + (1f - a) * 26f * e, tw, th);
            Painel(r, e, ouro, al * a);
            bool press = Comandos.Segurando(ac[i]) || Fr(t * 0.45f + i * 0.2f) < 0.1f;
            string tx = Comandos.Dica(ac[i]); float kw = LarguraTecla(tx, e, 100f * e, 30f * e);
            Tecla(new Rect(r.x + 24f * e, r.y + 20f * e, kw, 64f * e), tx, press, e, ouro, 30f * e);
            Texto(new Rect(r.x + 24f * e + kw + 18f * e, r.y + 16f * e, r.width - kw - 60f * e, 40f * e), Comandos.NomeCurto(ac[i]), fCinzel, 24f * e, TextAnchor.MiddleLeft, new Color(0.98f, 0.93f, 0.8f, a), false, FontStyle.Normal, false);
            Texto(new Rect(r.x + 24f * e + kw + 18f * e, r.y + 50f * e, r.width - kw - 60f * e, 36f * e), frases[i], fFell, 25f * e, TextAnchor.MiddleLeft, new Color(0.86f, 0.82f, 0.72f, a), false, FontStyle.Italic);
            // miniatura animada
            Rect pic = new Rect(r.x + 20f * e, r.y + 100f * e, r.width - 40f * e, r.height - 120f * e); float cho = pic.yMax - 26f * e;
            R(new Rect(pic.x, cho, pic.width, 2f * e), new Color(1f, 1f, 1f, 0.12f * a));
            float rr = Mathf.Min(26f * e, pic.height * 0.2f); Color cor = new Color(0.95f, 0.8f, 0.4f);
            if (i == 0) { float s = Fr(t * 0.9f); float alt = Mathf.Sin(s * Mathf.PI) * pic.height * 0.5f; if (s < 0.5f) { } Token(new Vector2(pic.center.x, cho - rr - alt), rr, Vector2.right, a, cor); if (alt < 3f * e) Onda(new Vector2(pic.center.x, cho), Fr(t * 0.9f) < 0.12f ? Fr(t * 0.9f) * 8f : 0f, 60f * e, new Color(1f, 1f, 1f, 0.6f * a)); R(new Rect(pic.center.x + 80f * e, cho - 34f * e, 40f * e, 34f * e), new Color(0.35f, 0.3f, 0.26f, a)); }
            else if (i == 1) { float s = Fr(t * 0.55f); Vector2 p = new Vector2(Mathf.Lerp(pic.x + 60f * e, pic.xMax - 60f * e, s), cho - rr * 0.85f); Token(p, rr * 0.85f, Vector2.right, a, cor); Arco(p, rr * 1.2f, t * 700f, t * 700f + 120f, 6f * e, new Color(1f, 1f, 1f, 0.7f * a)); for (int d = 1; d <= 3; d++) Disco(p.x - d * 22f * e, cho - 6f * e, (8f - d * 1.5f) * e, new Color(0.8f, 0.72f, 0.55f, 0.35f * a)); }
            else if (i == 2) { Vector2 hp = new Vector2(pic.center.x - 70f * e, cho - rr), np = new Vector2(pic.center.x + 70f * e, cho - rr); Token(hp, rr, Vector2.right, a, cor); Token(np, rr, Vector2.left, a, new Color(0.55f, 0.75f, 0.9f)); float bb = 0.5f + 0.5f * Mathf.Sin(t * 3f); RR(new Rect(np.x - 50f * e, np.y - rr - 64f * e, 100f * e, 46f * e), 20f * e, new Color(1f, 0.97f, 0.88f, a)); for (int d = 0; d < 3; d++) Disco(np.x - 24f * e + d * 24f * e, np.y - rr - 41f * e, 6f * e, new Color(0.25f, 0.22f, 0.2f, a * (0.35f + 0.65f * Mathf.Clamp01(Mathf.Sin(t * 4f - d * 0.9f) * 0.5f + 0.6f)))); }
            else { Rect mp = new Rect(pic.center.x - 100f * e, pic.y + 6f * e, 200f * e, pic.height - 26f * e); RR(mp, 10f * e, new Color(0.78f, 0.7f, 0.5f, a)); RR(new Rect(mp.x + 4f * e, mp.y + 4f * e, mp.width - 8f * e, mp.height - 8f * e), 8f * e, new Color(0.2f, 0.26f, 0.2f, a)); R(new Rect(mp.x + 20f * e, mp.center.y, mp.width - 40f * e, 4f * e), new Color(0.75f, 0.65f, 0.4f, a)); R(new Rect(mp.center.x - 2f * e, mp.y + 14f * e, 4f * e, mp.height - 28f * e), new Color(0.75f, 0.65f, 0.4f, a)); float bob = Mathf.Abs(Mathf.Sin(t * 3f)) * 10f * e; Disco(mp.center.x + 30f * e, mp.center.y - 8f * e - bob, 9f * e, new Color(1f, 0.4f, 0.3f, a)); Onda(new Vector2(mp.center.x + 30f * e, mp.center.y), Fr(t * 0.8f), 50f * e, new Color(1f, 0.5f, 0.4f, a)); }
        }
    }

    // ------------------------------------------------------------------ habilidades
    void DesenharHabilidade(float x, float y, float w, float e, Color ouro, Color ouroClaro, float al, int idx)
    {
        DadosDeHabilidade d = hab.Dados(idx); if (d == null) return;
        float a0 = Ent(0), a1 = Ent(1), a2 = Ent(2), a3 = Ent(3), colE = w * 0.36f;
        Texto(new Rect(x, y + (1f - a0) * 12f * e, colE, 34f * e), idx == 2 ? "HABILIDADE FINAL" : "HABILIDADE ESPECIAL", fCinzel, 22f * e, TextAnchor.MiddleLeft, new Color(ouro.r, ouro.g, ouro.b, a0), false, FontStyle.Normal, false);
        float nh = Est(fCinzel, 56f * e, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(d.nome.ToUpperInvariant()), colE);
        Texto(new Rect(x, y + 40f * e + (1f - a1) * 14f * e, colE, nh + 8f * e), d.nome.ToUpperInvariant(), fCinzel, 56f * e, TextAnchor.UpperLeft, new Color(0.98f, 0.93f, 0.8f, a1), true);
        float oy = y + 40f * e + nh + 22f * e; OrnamentoVivo(x, oy, colE * 0.9f * a1, e, new Color(ouroClaro.r, ouroClaro.g, ouroClaro.b, a1));
        string desc = string.IsNullOrEmpty(d.descricao) ? "" : d.descricao;
        float dh = Est(fFell, 31f * e, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(desc), colE);
        Texto(new Rect(x, oy + 24f * e + (1f - a2) * 12f * e, colE, dh + 8f * e), desc, fFell, 31f * e, TextAnchor.UpperLeft, new Color(0.94f, 0.91f, 0.84f, a2), true);
        float ey = oy + 24f * e + dh + 34f * e;
        Etiqueta(new Rect(x, ey + (1f - a3) * 12f * e, 230f * e, 54f * e), "Custo  " + Mathf.RoundToInt(d.custo * 100f) + "%", new Color(0.42f, 0.66f, 1f), e, a3);
        Etiqueta(new Rect(x + 246f * e, ey + (1f - a3) * 12f * e, 250f * e, 54f * e), "Recarga  " + d.recarga.ToString("0.#") + " s", new Color(0.85f, 0.75f, 0.5f), e, a3);
        float fw = w * 0.575f, fh = fw * 9f / 16f, fx = x + w - fw - 18f * e, fy = y + 10f * e + (1f - a1) * 24f * e;
        Rect pr = new Rect(fx, fy, fw, fh);
        Matrix4x4 m = GUI.matrix; GUIUtility.RotateAroundPivot(-0.9f, new Vector2(pr.center.x, pr.center.y));
        RR(new Rect(pr.x - 8f * e, pr.y + 8f * e, pr.width + 36f * e, pr.height + 36f * e), 10f * e, new Color(0f, 0f, 0f, 0.55f * a1));
        RR(new Rect(pr.x - 20f * e, pr.y - 20f * e, pr.width + 40f * e, pr.height + 40f * e), 6f * e, new Color(0.84f, 0.74f, 0.54f, a1));
        RR(new Rect(pr.x - 14f * e, pr.y - 14f * e, pr.width + 28f * e, pr.height + 28f * e), 4f * e, new Color(0.7f, 0.58f, 0.38f, a1));
        MostruarioDesenhar(pr, a1);
        GUI.matrix = m;
        // "como usar": a tecla é apertada de tempos em tempos (e acende quando VOCÊ aperta)
        float uy = fy + fh + 56f * e, ux = fx, b = Ent(4);
        RR(new Rect(ux, uy + (1f - b) * 16f * e, fw + 8f * e, 96f * e), 16f * e, new Color(0.03f, 0.03f, 0.045f, 0.88f * b));
        Texto(new Rect(ux + 28f * e, uy + 6f * e, 260f * e, 84f * e), "COMO USAR", fCinzel, 24f * e, TextAnchor.MiddleLeft, new Color(ouro.r, ouro.g, ouro.b, b), false, FontStyle.Normal, false);
        string tecla = Comandos.Dica(Acao.Habilidade1 + idx); float kw = LarguraTecla(tecla, e, 88f * e, 36f * e);
        float ciclo = Fr(Time.unscaledTime * 0.5f); bool aperta = Comandos.Segurando(Acao.Habilidade1 + idx) || ciclo < 0.14f;
        Tecla(new Rect(ux + 300f * e, uy + 12f * e, kw, 76f * e), tecla, aperta, e, ouro, 36f * e);
        if (ciclo < 0.5f) Onda(new Vector2(ux + 300f * e + kw * 0.5f, uy + 40f * e), ciclo * 2f, 90f * e, new Color(1f, 0.9f, 0.5f, b));
        Texto(new Rect(ux + 300f * e + kw + 20f * e, uy + 6f * e, fw - kw - 340f * e, 84f * e), "aperte para usar", fFell, 30f * e, TextAnchor.MiddleLeft, new Color(0.94f, 0.9f, 0.8f, b), false, FontStyle.Italic);
    }
}
