using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// A tela de Configurações (aberta pelo menu de pausa). Cinco abas: Jogabilidade, Áudio, Vídeo, Teclado e mouse, Controle.
// Funciona com mouse, teclado (setas/WASD, Enter, Esc, Q/E troca de aba) e controle (direcional, A/B, LB/RB).
public class TelaDeConfiguracoes
{
    enum Tipo { Cabecalho, Texto, Slider, Alternar, Escolha, Botao, Tecla }

    class Linha
    {
        public Tipo tipo; public string rotulo, desc;
        public System.Func<float> pegaF; public System.Action<float> poeF; public float min, max, passo; public System.Func<float, string> fmt;
        public System.Func<bool> pegaB; public System.Action<bool> poeB;
        public string[] opcoes; public System.Func<int> pegaI; public System.Action<int> poeI;
        public System.Action acao; public System.Func<string> texto;
        public Acao acaoDeTecla;
        public bool Selecionavel { get { return tipo != Tipo.Cabecalho && tipo != Tipo.Texto; } }
    }

    static readonly string[] nomesDasAbas = { "Jogabilidade", "Áudio", "Vídeo", "Teclado e mouse", "Controle" };

    public bool Capturando { get { return capturando; } }
    int aba, sel, slotSel;
    List<Linha> linhas = new List<Linha>();
    float scroll, esc = 1f, abriuEm;
    bool garantirVisivel;
    int arrastando = -1;
    float[] ys = new float[0], hs = new float[0]; float total; Rect areaLinhas;

    // remapear
    bool capturando; int capSlot; float capDesde;
    string aviso; float avisoAte;
    static KeyCode[] teclasValidas;

    // vídeo (só vale quando aperta "Aplicar tela")
    int pendModo, pendRes; List<Vector2Int> resolucoes = new List<Vector2Int>();

    // ------------------------------------------------------------------ abrir
    public void Abrir()
    {
        Configuracoes.Garantir();
        aba = 0; scroll = 0f; capturando = false; arrastando = -1; aviso = null; abriuEm = Time.unscaledTime;
        PrepararVideo();
        Montar();
    }

    public void Fechar() { capturando = false; arrastando = -1; Configuracoes.Salvar(); }

    void MudarAba(int n)
    {
        aba = n; scroll = 0f; capturando = false; arrastando = -1; slotSel = 0;
        Montar();
    }

    // ------------------------------------------------------------------ montar as linhas de cada aba
    Linha Cab(string t) { return new Linha { tipo = Tipo.Cabecalho, rotulo = t }; }
    Linha Txt(System.Func<string> t) { return new Linha { tipo = Tipo.Texto, texto = t }; }
    Linha Txt(string t) { return new Linha { tipo = Tipo.Texto, texto = () => t }; }
    Linha Btn(string r, string d, System.Action a) { return new Linha { tipo = Tipo.Botao, rotulo = r, desc = d, acao = a }; }
    Linha Alt(string r, string d, System.Func<bool> g, System.Action<bool> s) { return new Linha { tipo = Tipo.Alternar, rotulo = r, desc = d, pegaB = g, poeB = s }; }
    Linha Esc(string r, string d, string[] ops, System.Func<int> g, System.Action<int> s) { return new Linha { tipo = Tipo.Escolha, rotulo = r, desc = d, opcoes = ops, pegaI = g, poeI = s }; }
    Linha Tec(Acao a) { return new Linha { tipo = Tipo.Tecla, rotulo = Comandos.NomeDaAcao(a), acaoDeTecla = a }; }
    Linha Sli(string r, string d, System.Func<float> g, System.Action<float> s, float min, float max, float passo, System.Func<float, string> fmt)
    { return new Linha { tipo = Tipo.Slider, rotulo = r, desc = d, pegaF = g, poeF = s, min = min, max = max, passo = passo, fmt = fmt }; }

    static string Pct(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }
    static string Vezes(float v) { return v.ToString("0.00") + "x"; }

    void Montar()
    {
        linhas.Clear();
        switch (aba)
        {
            case 0:
                linhas.Add(Cab("Câmera"));
                linhas.Add(Sli("Sensibilidade do mouse", "Velocidade da câmera com o mouse.", () => Configuracoes.SensMouse, v => Configuracoes.SensMouse = v, 0.2f, 3f, 0.05f, Vezes));
                linhas.Add(Sli("Sensibilidade do controle", "Velocidade da câmera com o analógico direito.", () => Configuracoes.SensControle, v => Configuracoes.SensControle = v, 0.3f, 3f, 0.05f, Vezes));
                linhas.Add(Alt("Inverter câmera (vertical)", "Mouse para cima passa a olhar para baixo.", () => Configuracoes.InverterY, v => Configuracoes.InverterY = v));
                linhas.Add(Alt("Inverter câmera (horizontal)", "Inverte o giro para os lados.", () => Configuracoes.InverterX, v => Configuracoes.InverterX = v));
                linhas.Add(Sli("Campo de visão", "Quanto do mundo aparece na tela. Valores altos mostram mais, com a imagem mais esticada.", CampoDeVisao, v => Configuracoes.Fov = v, 50f, 100f, 1f, v => Mathf.RoundToInt(v) + "°"));
                linhas.Add(Sli("Tremor da câmera", "Força do tremor quando você bate ou apanha. Zero desliga.", () => Configuracoes.Tremor, v => Configuracoes.Tremor = v, 0f, 1f, 0.05f, Pct));
                linhas.Add(Cab("Interface"));
                linhas.Add(Sli("Tamanho da interface", "Aumenta ou diminui a vida, as habilidades e os textos da tela.", () => Configuracoes.EscalaUI, v => Configuracoes.EscalaUI = v, 0.8f, 1.4f, 0.05f, Pct));
                linhas.Add(Alt("Mostrar minimapa", "Liga ou desliga o minimapa no canto da tela.", () => Configuracoes.MostrarMinimapa, v => Configuracoes.MostrarMinimapa = v));
                linhas.Add(Cab("Jogo"));
                linhas.Add(Esc("Dispositivo de entrada", "Automático troca sozinho entre teclado e controle conforme você usa. Os textos de ajuda acompanham.", new[] { "Automático", "Teclado e mouse", "Controle" }, () => Configuracoes.Modo, v => Configuracoes.Modo = v));
                linhas.Add(Alt("Pausar ao sair da janela", "O jogo pausa sozinho quando você troca de janela.", () => Configuracoes.PausarSemFoco, v => Configuracoes.PausarSemFoco = v));
                break;

            case 1:
                linhas.Add(Cab("Volume"));
                linhas.Add(Sli("Volume geral", "Controla todos os sons do jogo.", () => Configuracoes.VolMestre, v => Configuracoes.VolMestre = v, 0f, 1f, 0.05f, Pct));
                linhas.Add(Sli("Música e ambiente", "Trilhas, vento, água e outros sons que ficam tocando.", () => Configuracoes.VolMusica, v => Configuracoes.VolMusica = v, 0f, 1f, 0.05f, Pct));
                linhas.Add(Sli("Efeitos sonoros", "Golpes, magias, passos e avisos.", () => Configuracoes.VolEfeitos, v => Configuracoes.VolEfeitos = v, 0f, 1f, 0.05f, Pct));
                break;

            case 2:
                linhas.Add(Cab("Tela"));
                linhas.Add(Esc("Modo de tela", "Escolha e aperte \"Aplicar tela\" para valer.", new[] { "Tela cheia", "Janela sem bordas", "Janela" }, () => pendModo, v => pendModo = v));
                linhas.Add(Esc("Resolução", "Escolha e aperte \"Aplicar tela\" para valer.", NomesDeResolucao(), () => pendRes, v => pendRes = v));
                linhas.Add(Btn("Aplicar tela", "Troca o modo de tela e a resolução escolhidos acima.", () => { AplicarTela(); }));
                linhas.Add(Alt("Sincronização vertical (VSync)", "Evita rasgos na imagem. Pode aumentar um pouco o atraso.", () => Configuracoes.VSync, v => Configuracoes.VSync = v));
                linhas.Add(Esc("Limite de FPS", "Quantos quadros por segundo no máximo. Menos quadros esquentam menos o computador.", new[] { "30", "60", "90", "120", "144", "Sem limite" }, () => IndiceFps(), v => Configuracoes.LimiteFps = new[] { 30, 60, 90, 120, 144, 0 }[v]));
                linhas.Add(Sli("Brilho", "Deixa a imagem mais clara ou mais escura.", () => Configuracoes.Brilho, v => Configuracoes.Brilho = v, -0.5f, 0.5f, 0.05f, v => (v > 0 ? "+" : "") + Mathf.RoundToInt(v * 200f)));
                linhas.Add(Cab("Gráficos"));
                linhas.Add(Esc("Escala de renderização", "Automática: o jogo ajusta sozinho para manter os quadros. Valores menores deixam mais leve, com imagem mais macia.", new[] { "Automática", "100%", "85%", "70%", "50%" }, () => IndiceEscala(), v => Configuracoes.Escala = new[] { 0, 100, 85, 70, 50 }[v]));
                linhas.Add(Esc("Suavização de bordas (MSAA)", "Tira o serrilhado das bordas. Pesa na placa de vídeo.", new[] { "Padrão do jogo", "Desligada", "2x", "4x", "8x" }, () => IndiceMsaa(), v => Configuracoes.Msaa = new[] { -1, 0, 2, 4, 8 }[v]));
                linhas.Add(Esc("Sombras", "Até onde as sombras aparecem. Desligar ajuda muito em computador fraco.", new[] { "Desligadas", "Curtas", "Médias", "Longas" }, () => Configuracoes.Sombras, v => Configuracoes.Sombras = v));
                linhas.Add(Esc("Qualidade das texturas", "Texturas menores usam menos memória de vídeo.", new[] { "Alta", "Média", "Baixa" }, () => Mathf.Clamp(Configuracoes.Texturas, 0, 2), v => Configuracoes.Texturas = v));
                linhas.Add(Esc("Distância de visão", "Até onde o mundo é desenhado. Curta deixa o jogo mais leve.", new[] { "Padrão", "Curta", "Média", "Longa" }, () => Configuracoes.Distancia, v => Configuracoes.Distancia = v));
                linhas.Add(Alt("Mostrar contador de FPS", "Mostra os quadros por segundo no canto da tela.", () => Configuracoes.MostrarFps, v => Configuracoes.MostrarFps = v));
                break;

            case 3:
                linhas.Add(Txt("Cada ação tem duas teclas: a principal (esquerda) e a alternativa (direita). Clique numa e aperte a nova (teclado ou botão do mouse). Delete limpa. Se a tecla já estiver em uso, as duas ações trocam de lugar. ESC é sempre a pausa."));
                linhas.Add(Cab("Movimento"));
                foreach (Acao a in new[] { Acao.Frente, Acao.Tras, Acao.Esquerda, Acao.Direita, Acao.Correr, Acao.Pular, Acao.Rolar }) linhas.Add(Tec(a));
                linhas.Add(Cab("Combate"));
                foreach (Acao a in new[] { Acao.Atacar, Acao.Defender, Acao.GuardarSacar, Acao.Habilidade1, Acao.Habilidade2, Acao.Habilidade3 }) linhas.Add(Tec(a));
                linhas.Add(Cab("Mundo"));
                foreach (Acao a in new[] { Acao.Interagir, Acao.Cristal, Acao.Voar, Acao.Subir, Acao.Descer, Acao.Mapa, Acao.ZoomMais, Acao.ZoomMenos, Acao.Reviver }) linhas.Add(Tec(a));
                break;

            case 4:
                linhas.Add(Txt(() => Comandos.NomeDoControle() != null ? "Controle conectado: " + Comandos.NomeDoControle() : "Nenhum controle detectado. Conecte um controle (USB ou Bluetooth) e aperte qualquer botão."));
                linhas.Add(Txt("Andar: analógico esquerdo.   Câmera: analógico direito.   Pausa: Start (Menu)."));
                linhas.Add(Cab("Ajustes"));
                linhas.Add(Sli("Zona morta", "Ignora o movimento pequeno do analógico. Aumente se a câmera ou o personagem andam sozinhos.", () => Configuracoes.ZonaMorta, v => Configuracoes.ZonaMorta = v, 0.05f, 0.5f, 0.01f, Pct));
                linhas.Add(Alt("Vibração", "O controle vibra quando você bate ou apanha.", () => Configuracoes.Vibracao, v => Configuracoes.Vibracao = v));
                linhas.Add(Btn("Testar vibração", "Faz o controle vibrar um instante.", () => Comandos.Vibrar(0.9f, 0.4f, true)));
                linhas.Add(Cab("Botões"));
                foreach (Acao a in Comandos.AcoesDoControle) linhas.Add(Tec(a));
                break;
        }
        sel = Proxima(-1, 1);
        total = 0f; garantirVisivel = true;
    }

    float CampoDeVisao()
    {
        if (Configuracoes.Fov > 0f) return Configuracoes.Fov;
        Camera c = Camera.main; return c != null ? c.fieldOfView : 60f;
    }

    // ------------------------------------------------------------------ vídeo
    void PrepararVideo()
    {
        resolucoes.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.height < 480) continue;
            Vector2Int v = new Vector2Int(r.width, r.height);
            if (!resolucoes.Contains(v)) resolucoes.Add(v);
        }
        Vector2Int atual = new Vector2Int(Screen.width, Screen.height);
        if (!resolucoes.Contains(atual)) resolucoes.Add(atual);
        resolucoes.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
        Vector2Int alvo = Configuracoes.ResolucaoW > 0 ? new Vector2Int(Configuracoes.ResolucaoW, Configuracoes.ResolucaoH) : atual;
        pendRes = Mathf.Max(0, resolucoes.IndexOf(alvo));
        if (Configuracoes.ModoTela >= 0) pendModo = Configuracoes.ModoTela;
        else
        {
            FullScreenMode m = Screen.fullScreenMode;
            pendModo = m == FullScreenMode.ExclusiveFullScreen ? 0 : m == FullScreenMode.Windowed ? 2 : 1;
        }
    }

    string[] NomesDeResolucao()
    {
        string[] n = new string[resolucoes.Count];
        for (int i = 0; i < n.Length; i++) n[i] = resolucoes[i].x + " x " + resolucoes[i].y;
        return n;
    }

    void AplicarTela()
    {
        Configuracoes.ModoTela = pendModo;
        if (pendRes >= 0 && pendRes < resolucoes.Count) { Configuracoes.ResolucaoW = resolucoes[pendRes].x; Configuracoes.ResolucaoH = resolucoes[pendRes].y; }
        Configuracoes.Mudou();
        AplicadorDeConfiguracoes.AplicarTela();
        Avisar("Tela aplicada. No editor do Unity a janela do Game não muda; no jogo montado, sim.");
    }

    static int IndiceFps() { int f = Configuracoes.LimiteFps; return f == 30 ? 0 : f == 60 ? 1 : f == 90 ? 2 : f == 120 ? 3 : f == 144 ? 4 : f == 0 ? 5 : 1; }
    static int IndiceEscala() { int e = Configuracoes.Escala; return e == 100 ? 1 : e == 85 ? 2 : e == 70 ? 3 : e == 50 ? 4 : 0; }
    static int IndiceMsaa() { int m = Configuracoes.Msaa; return m < 0 ? 0 : m == 0 ? 1 : m == 2 ? 2 : m == 4 ? 3 : 4; }

    void Avisar(string t) { aviso = t; avisoAte = Time.unscaledTime + 4f; }

    // ------------------------------------------------------------------ valores
    void PoeSlider(Linha l, float v, bool salvar)
    {
        v = Mathf.Clamp(l.min + Mathf.Round((v - l.min) / l.passo) * l.passo, l.min, l.max);
        l.poeF(v); Configuracoes.Mudou(salvar);
    }

    void Alterar(Linha l, int dir)
    {
        switch (l.tipo)
        {
            case Tipo.Slider: PoeSlider(l, l.pegaF() + dir * l.passo, true); break;
            case Tipo.Alternar: l.poeB(!l.pegaB()); Configuracoes.Mudou(); break;
            case Tipo.Escolha:
                int n = l.opcoes.Length; l.poeI(((l.pegaI() + dir) % n + n) % n); Configuracoes.Mudou(); break;
        }
    }

    // ------------------------------------------------------------------ navegação
    int Proxima(int de, int passo)
    {
        if (linhas.Count == 0) return 0;
        int i = de;
        for (int k = 0; k < linhas.Count; k++)
        {
            i = ((i + passo) % linhas.Count + linhas.Count) % linhas.Count;
            if (linhas[i].Selecionavel) return i;
        }
        return Mathf.Max(0, de);
    }

    void Ativar(Linha l)
    {
        switch (l.tipo)
        {
            case Tipo.Alternar: case Tipo.Escolha: Alterar(l, 1); break;
            case Tipo.Botao: if (l.acao != null) l.acao(); break;
            case Tipo.Tecla: IniciarCaptura(aba == 4 ? 0 : slotSel); break;
        }
    }

    void IniciarCaptura(int slot)
    {
        if (aba == 4 && Gamepad.current == null) { Avisar("Conecte um controle para escolher os botões."); return; }
        capturando = true; capSlot = slot; capDesde = Time.unscaledTime;
    }

    // chamado todo quadro (Update). Devolve true quando o jogador quer voltar ao menu de pausa.
    public bool Atualizar()
    {
        if (Time.unscaledTime - abriuEm < 0.2f) return false;
        if (capturando) { AtualizarCaptura(); return false; }

        int ta = Comandos.NavAba();
        if (ta != 0) { MudarAba(((aba + ta) % nomesDasAbas.Length + nomesDasAbas.Length) % nomesDasAbas.Length); return false; }

        Vector2Int d = Comandos.NavDirecao();
        if (d.y != 0) { sel = Proxima(sel, -d.y); garantirVisivel = true; slotSel = Mathf.Min(slotSel, 1); }
        if (d.x != 0 && sel >= 0 && sel < linhas.Count)
        {
            Linha l = linhas[sel];
            if (l.tipo == Tipo.Tecla) { if (aba == 3) slotSel = d.x > 0 ? 1 : 0; }
            else Alterar(l, d.x);
        }
        if (Comandos.NavConfirmar() && sel >= 0 && sel < linhas.Count) Ativar(linhas[sel]);

        float roda = Input.mouseScrollDelta.y;
        if (Mathf.Abs(roda) > 0.01f) scroll -= roda * 70f * esc;

        return Comandos.NavVoltar();
    }

    // ------------------------------------------------------------------ remapear
    void AtualizarCaptura()
    {
        if (Time.unscaledTime - capDesde < 0.25f) return;
        Linha l = linhas[sel];
        if (Input.GetKeyDown(KeyCode.Escape) || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)) { capturando = false; return; }

        if (aba == 4)
        {
            Gamepad g = Gamepad.current;
            if (g == null) { capturando = false; return; }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) { Comandos.LimparBotao(l.acaoDeTecla); capturando = false; Configuracoes.Mudou(); return; }
            foreach (GamepadButton b in Comandos.BotoesDoControle)
                if (g[b].wasPressedThisFrame)
                {
                    string troca = Comandos.DefinirBotao(l.acaoDeTecla, b);
                    if (troca != null) Avisar("\"" + troca + "\" ficou com o botão que esta ação usava antes.");
                    capturando = false; Configuracoes.Mudou(); return;
                }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
        {
            Comandos.DefinirTecla(l.acaoDeTecla, capSlot, KeyCode.None); capturando = false; Configuracoes.Mudou(); return;
        }
        if (teclasValidas == null)
        {
            List<KeyCode> v = new List<KeyCode>();
            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                if (k != KeyCode.None && k != KeyCode.Escape && (int)k < (int)KeyCode.JoystickButton0 && !v.Contains(k)) v.Add(k);
            teclasValidas = v.ToArray();
        }
        foreach (KeyCode k in teclasValidas)
            if (Input.GetKeyDown(k))
            {
                string troca = Comandos.DefinirTecla(l.acaoDeTecla, capSlot, k);
                if (troca != null) Avisar("\"" + troca + "\" ficou com a tecla que esta ação usava antes.");
                capturando = false; Configuracoes.Mudou(); return;
            }
    }

    // ------------------------------------------------------------------ desenho e mouse
    Rect CaixaDaTecla(Rect r, int slot)
    {
        if (aba == 4) return new Rect(r.xMax - 250f * esc, r.y + 9f * esc, 230f * esc, r.height - 18f * esc);
        return new Rect(r.xMax - (slot == 0 ? 440f : 225f) * esc, r.y + 9f * esc, 205f * esc, r.height - 18f * esc);
    }
    Rect Trilho(Rect r) { return new Rect(r.x + r.width * 0.5f, r.center.y - 3f * esc, r.width * 0.31f, 6f * esc); }
    Rect CaixaEscolha(Rect r) { return new Rect(r.xMax - 390f * esc, r.y + 8f * esc, 370f * esc, r.height - 16f * esc); }
    Rect Pilula(Rect r) { return new Rect(r.xMax - 92f * esc, r.center.y - 16f * esc, 70f * esc, 32f * esc); }

    void Calcular()
    {
        if (ys.Length != linhas.Count) { ys = new float[linhas.Count]; hs = new float[linhas.Count]; }
        float y = 0f;
        for (int i = 0; i < linhas.Count; i++)
        {
            Linha l = linhas[i];
            float h = l.tipo == Tipo.Cabecalho ? 52f * esc : l.tipo == Tipo.Texto ? (l.texto().Length > 80 ? 70f : 42f) * esc : 60f * esc;
            if (l.tipo == Tipo.Cabecalho && i > 0) y += 10f * esc;
            ys[i] = y; hs[i] = h; y += h + 3f * esc;
        }
        total = y;
    }

    // tela inteira: título e abas em cima, lista à esquerda, painel de informações (ou o desenho do controle) à direita, barra de comandos embaixo
    public void Desenhar(Rect tela, float escala, float fade, Event ev, Font fTexto, Font fTitulo)
    {
        esc = escala;
        Color ouro = new Color(0.86f, 0.68f, 0.34f);
        bool repinta = ev.type == EventType.Repaint;
        float W = tela.width, H = tela.height;
        float mx = Mathf.Max(70f * esc, (W - 1700f * esc) * 0.5f), cw = W - mx * 2f;

        if (repinta)
        {
            Cor(tela, new Color(0.028f, 0.027f, 0.034f, 0.975f * fade));
            Cor(new Rect(0, 0, W, 205f * esc), new Color(1f, 1f, 1f, 0.022f * fade));
            Cor(new Rect(0, H - 104f * esc, W, 104f * esc), new Color(0f, 0f, 0f, 0.35f * fade));
            Cor(new Rect(mx, 204f * esc, cw, 1.5f * esc), new Color(ouro.r, ouro.g, ouro.b, 0.45f * fade));
            Cor(new Rect(mx, H - 104f * esc, cw, 1.5f * esc), new Color(ouro.r, ouro.g, ouro.b, 0.25f * fade));
            GUIStyle tit = new GUIStyle { font = fTitulo, fontSize = Mathf.RoundToInt(46f * esc), alignment = TextAnchor.UpperLeft };
            MenuDePausa.Letras(new Rect(mx, 38f * esc, 900f * esc, 70f * esc), "CONFIGURAÇÕES", tit, 9f * esc, new Color(0.93f, 0.87f, 0.74f, fade), true);
        }

        // ---- abas
        float ty = 122f * esc, th = 58f * esc, tabsX = mx + 70f * esc, tw = (cw - 140f * esc) / nomesDasAbas.Length;
        GUIStyle sa = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(30f * esc), alignment = TextAnchor.MiddleCenter };
        for (int i = 0; i < nomesDasAbas.Length; i++)
        {
            Rect r = new Rect(tabsX + tw * i, ty, tw, th);
            bool atual = i == aba, sobre = r.Contains(ev.mousePosition);
            if (ev.type == EventType.MouseDown && ev.button == 0 && sobre) { ev.Use(); MudarAba(i); return; }
            if (!repinta) continue;
            if (sobre && !atual) Cor(r, new Color(1f, 1f, 1f, 0.045f * fade));
            GUI.Label(r, nomesDasAbas[i], Col(sa, atual ? new Color(1f, 0.93f, 0.76f, fade) : new Color(0.66f, 0.62f, 0.54f, 0.95f * fade)));
            if (atual) Cor(new Rect(r.x + 10f * esc, r.yMax - 3f * esc, r.width - 20f * esc, 3f * esc), new Color(ouro.r, ouro.g, ouro.b, fade), 2f * esc);
        }
        if (repinta)
        {
            GUIStyle dq = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(22f * esc), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            Color cd = new Color(ouro.r, ouro.g, ouro.b, 0.9f * fade);
            Rect e1 = new Rect(mx, ty + 8f * esc, 62f * esc, th - 16f * esc), e2 = new Rect(mx + cw - 62f * esc, ty + 8f * esc, 62f * esc, th - 16f * esc);
            Cor(e1, new Color(1f, 1f, 1f, 0.07f * fade), 6f * esc); Cor(e2, new Color(1f, 1f, 1f, 0.07f * fade), 6f * esc);
            GUI.Label(e1, Comandos.UsandoControle ? (Comandos.EstiloPlayStation ? "L1" : "LB") : "Q", Col(dq, cd));
            GUI.Label(e2, Comandos.UsandoControle ? (Comandos.EstiloPlayStation ? "R1" : "RB") : "E", Col(dq, cd));
        }

        // ---- corpo: lista + painel direito
        float corpoY = 224f * esc, corpoH = H - 104f * esc - corpoY - 14f * esc;
        float fracao = aba == 4 ? 0.37f : 0.56f, gap = 44f * esc;
        areaLinhas = new Rect(mx, corpoY, cw * fracao, corpoH);
        Rect direita = new Rect(areaLinhas.xMax + gap, corpoY, cw - areaLinhas.width - gap, corpoH);

        Calcular();
        float maxScroll = Mathf.Max(0f, total - areaLinhas.height);
        if (garantirVisivel && sel >= 0 && sel < linhas.Count && repinta)
        {
            if (ys[sel] - scroll < 0f) scroll = ys[sel] - 8f * esc;
            else if (ys[sel] + hs[sel] - scroll > areaLinhas.height) scroll = ys[sel] + hs[sel] - areaLinhas.height + 8f * esc;
            garantirVisivel = false;
        }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);

        GUI.BeginGroup(areaLinhas);
        float gw = areaLinhas.width - (maxScroll > 0f ? 18f * esc : 0f);
        Vector2 mp = ev.mousePosition;
        bool dentro = mp.y >= 0f && mp.y <= areaLinhas.height && mp.x >= 0f && mp.x <= areaLinhas.width;

        if (ev.type == EventType.MouseUp && ev.button == 0 && arrastando >= 0) { arrastando = -1; Configuracoes.Salvar(); }
        if (arrastando >= 0 && arrastando < linhas.Count && (ev.type == EventType.MouseDrag || ev.type == EventType.MouseMove))
        {
            Linha ls = linhas[arrastando]; Rect tr = Trilho(new Rect(0, ys[arrastando] - scroll, gw, hs[arrastando]));
            PoeSlider(ls, ls.min + Mathf.Clamp01((mp.x - tr.x) / tr.width) * (ls.max - ls.min), false);
            ev.Use();
        }
        for (int i = 0; i < linhas.Count; i++)
        {
            Linha l = linhas[i];
            Rect r = new Rect(0, ys[i] - scroll, gw, hs[i]);
            if (r.yMax < 0f || r.y > areaLinhas.height) continue;
            if (l.Selecionavel && dentro && r.Contains(mp))
            {
                if (ev.type == EventType.MouseMove && !capturando && sel != i) { sel = i; slotSel = Mathf.Min(slotSel, 1); }
                if (ev.type == EventType.MouseDown && ev.button == 0 && !capturando) { sel = i; Clique(l, r, mp); ev.Use(); }
            }
            if (repinta) DesenharLinha(l, r, i == sel, fade, fTexto, ouro);
        }
        if (repinta && maxScroll > 0f)
        {
            float bh = Mathf.Max(40f * esc, areaLinhas.height * areaLinhas.height / total);
            float by = (areaLinhas.height - bh) * (scroll / maxScroll);
            Cor(new Rect(areaLinhas.width - 6f * esc, 0f, 3f * esc, areaLinhas.height), new Color(1f, 1f, 1f, 0.06f * fade), 2f * esc);
            Cor(new Rect(areaLinhas.width - 6f * esc, by, 3f * esc, bh), new Color(ouro.r, ouro.g, ouro.b, 0.7f * fade), 2f * esc);
        }
        GUI.EndGroup();

        // ---- lado direito
        Linha atualL = sel >= 0 && sel < linhas.Count ? linhas[sel] : null;
        string textoAviso = (aviso != null && Time.unscaledTime < avisoAte) ? aviso : null;
        if (capturando) textoAviso = aba == 4 ? "Aperte o novo botão do controle…  (Start cancela, Delete limpa)" : "Aperte a nova tecla ou botão do mouse…  (Esc cancela, Delete limpa)";
        if (repinta)
        {
            Cor(new Rect(direita.x - gap * 0.5f, corpoY + 4f * esc, 1f * esc, corpoH - 8f * esc), new Color(1f, 1f, 1f, 0.07f * fade));
            if (aba == 4)
            {
                GUIStyle sc = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(25f * esc), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                bool tem = Comandos.NomeDoControle() != null;
                string cab = tem ? Comandos.NomeDoTipo(Comandos.Tipo) + "  —  " + Comandos.NomeDoControle() : "Exemplo: PlayStation 4.  Conecte um controle (USB ou Bluetooth) e o desenho muda para o seu.";
                GUI.Label(new Rect(direita.x, direita.y, direita.width, 40f * esc), cab, Col(sc, tem ? new Color(0.7f, 0.9f, 0.7f, fade) : new Color(0.78f, 0.7f, 0.58f, fade)));
            }
        }
        if (aba == 4)
        {
            int dest = -1;
            if (atualL != null && atualL.tipo == Tipo.Tecla) dest = Comandos.Botao(atualL.acaoDeTecla);
            if (repinta) DesenhoDeControle.Desenhar(new Rect(direita.x, direita.y + 46f * esc, direita.width, direita.height - 46f * esc - 70f * esc), esc, fade, fTexto, dest);
        }
        else if (repinta) CartaoDeInfo(direita, atualL, fade, fTexto, fTitulo, ouro);

        // ---- barra de baixo
        float by0 = H - 104f * esc;
        if (repinta)
        {
            string txt = textoAviso != null ? textoAviso : (aba == 4 && atualL != null ? atualL.desc : null);
            if (!string.IsNullOrEmpty(txt))
            {
                GUIStyle sd = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(24f * esc), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                GUI.Label(new Rect(direita.x, by0 - 62f * esc, direita.width, 56f * esc), txt, Col(sd, textoAviso != null ? new Color(1f, 0.86f, 0.52f, fade) : new Color(0.78f, 0.72f, 0.62f, 0.95f * fade)));
            }
        }
        Rect rVoltar = new Rect(mx, by0 + 24f * esc, 190f * esc, 52f * esc);
        Rect rPadrao = new Rect(mx + cw - 300f * esc, by0 + 24f * esc, 300f * esc, 52f * esc);
        BotaoPequeno(rVoltar, "Voltar", fade, fTexto, ev, ref voltarPedido);
        bool pedidoPadrao = false;
        BotaoPequeno(rPadrao, "Restaurar padrões", fade, fTexto, ev, ref pedidoPadrao);
        if (pedidoPadrao) { Configuracoes.RestaurarPadroes(aba); Montar(); Avisar("Esta aba voltou ao padrão."); }
        if (repinta)
        {
            GUIStyle dq = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(22f * esc), alignment = TextAnchor.MiddleCenter };
            string dicas = Comandos.UsandoControle
                ? "Direcional: mover e ajustar     " + Comandos.NomeDoBotao((int)GamepadButton.South) + ": escolher     " + Comandos.NomeDoBotao((int)GamepadButton.East) + ": voltar     " + (Comandos.EstiloPlayStation ? "L1/R1" : "LB/RB") + ": abas"
                : "W / S  mover        A / D  ajustar        Enter  escolher        Esc  voltar        Q / E  abas";
            GUI.Label(new Rect(rVoltar.xMax + 20f * esc, rVoltar.y, rPadrao.x - rVoltar.xMax - 40f * esc, rVoltar.height), dicas, Col(dq, new Color(0.62f, 0.57f, 0.48f, 0.9f * fade)));
        }
    }

    // painel de informações da linha selecionada (abas sem o desenho do controle)
    void CartaoDeInfo(Rect r, Linha l, float fade, Font fTexto, Font fTitulo, Color ouro)
    {
        Cor(new Rect(r.x, r.y, r.width, 3f * esc), new Color(ouro.r, ouro.g, ouro.b, 0.8f * fade), 1.5f * esc);
        float y = r.y + 22f * esc;
        GUIStyle s0 = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(22f * esc), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        GUI.Label(new Rect(r.x, y, r.width, 30f * esc), nomesDasAbas[aba].ToUpperInvariant(), Col(s0, new Color(ouro.r, ouro.g, ouro.b, 0.8f * fade)));
        y += 46f * esc;
        if (l == null || !l.Selecionavel) return;

        GUIStyle s1 = new GUIStyle { font = fTitulo, fontSize = Mathf.RoundToInt(38f * esc), alignment = TextAnchor.UpperLeft, wordWrap = true };
        float h1 = s1.CalcHeight(new GUIContent(l.rotulo), r.width);
        GUI.Label(new Rect(r.x, y, r.width, h1), l.rotulo, Col(s1, new Color(0.95f, 0.9f, 0.78f, fade)));
        y += h1 + 14f * esc;

        string desc = l.desc;
        if (l.tipo == Tipo.Tecla) desc = aba == 3
            ? "Principal:  " + Comandos.NomeDaTecla(Comandos.Tecla(l.acaoDeTecla, 0)) + "\nAlternativa:  " + Comandos.NomeDaTecla(Comandos.Tecla(l.acaoDeTecla, 1)) + "\n\nClique numa das caixas e aperte a nova tecla. Delete limpa."
            : desc;
        if (!string.IsNullOrEmpty(desc))
        {
            GUIStyle s2 = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(29f * esc), alignment = TextAnchor.UpperLeft, wordWrap = true };
            float h2 = s2.CalcHeight(new GUIContent(desc), r.width);
            GUI.Label(new Rect(r.x, y, r.width, h2), desc, Col(s2, new Color(0.8f, 0.75f, 0.66f, fade)));
            y += h2 + 20f * esc;
        }
        if (l.tipo == Tipo.Slider || l.tipo == Tipo.Escolha || l.tipo == Tipo.Alternar)
        {
            string v = l.tipo == Tipo.Slider ? l.fmt(l.pegaF()) : l.tipo == Tipo.Escolha ? l.opcoes[Mathf.Clamp(l.pegaI(), 0, l.opcoes.Length - 1)] : (l.pegaB() ? "Ligado" : "Desligado");
            GUIStyle s3 = new GUIStyle { font = fTitulo, fontSize = Mathf.RoundToInt(34f * esc), alignment = TextAnchor.UpperLeft };
            GUI.Label(new Rect(r.x, y, r.width, 50f * esc), "Atual:  " + v, Col(s3, new Color(1f, 0.88f, 0.55f, fade)));
        }
        if (aba == 2)
        {
            GUIStyle s4 = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(24f * esc), alignment = TextAnchor.LowerLeft };
            GUI.Label(new Rect(r.x, r.yMax - 40f * esc, r.width, 36f * esc), "Tela atual: " + Screen.width + " x " + Screen.height + "   (" + Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)) + " FPS)", Col(s4, new Color(0.6f, 0.56f, 0.48f, fade)));
        }
        if (textoDoAviso() != null)
        {
            GUIStyle s5 = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(25f * esc), alignment = TextAnchor.LowerLeft, wordWrap = true };
            GUI.Label(new Rect(r.x, r.yMax - (aba == 2 ? 130f : 90f) * esc, r.width, 80f * esc), textoDoAviso(), Col(s5, new Color(1f, 0.86f, 0.52f, fade)));
        }
    }
    string textoDoAviso()
    {
        if (capturando) return aba == 4 ? "Aperte o novo botão do controle…" : "Aperte a nova tecla ou botão do mouse…  (Esc cancela, Delete limpa)";
        return (aviso != null && Time.unscaledTime < avisoAte) ? aviso : null;
    }

    bool voltarPedido;
    // o menu de pausa pergunta se o botão "Voltar" da tela foi clicado
    public bool ConsumirVoltar() { bool v = voltarPedido; voltarPedido = false; return v; }

    void BotaoPequeno(Rect r, string texto, float fade, Font f, Event ev, ref bool clicou)
    {
        bool sobre = r.Contains(ev.mousePosition);
        if (ev.type == EventType.MouseDown && ev.button == 0 && sobre) { ev.Use(); clicou = true; return; }
        if (ev.type != EventType.Repaint) return;
        Color ouro = new Color(0.86f, 0.68f, 0.34f);
        Cor(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f), new Color(ouro.r, ouro.g, ouro.b, (sobre ? 0.95f : 0.4f) * fade), 7f * esc);
        Cor(r, sobre ? new Color(0.17f, 0.13f, 0.09f, 0.98f * fade) : new Color(0.06f, 0.055f, 0.055f, 0.98f * fade), 6f * esc);
        GUIStyle st = new GUIStyle { font = f, fontSize = Mathf.RoundToInt(27f * esc), alignment = TextAnchor.MiddleCenter };
        GUI.Label(r, texto, Col(st, sobre ? new Color(1f, 0.95f, 0.82f, fade) : new Color(0.86f, 0.82f, 0.74f, fade)));
    }

    // clique do mouse numa linha
    void Clique(Linha l, Rect r, Vector2 mp)
    {
        switch (l.tipo)
        {
            case Tipo.Slider:
                Rect tr = Trilho(r);
                if (mp.x >= tr.x - 20f * esc && mp.x <= tr.xMax + 20f * esc)
                { arrastando = sel; PoeSlider(l, l.min + Mathf.Clamp01((mp.x - tr.x) / tr.width) * (l.max - l.min), false); }
                break;
            case Tipo.Alternar: Alterar(l, 1); break;
            case Tipo.Escolha:
                Rect ce = CaixaEscolha(r);
                if (mp.x >= ce.x - 10f * esc && mp.x <= ce.xMax + 10f * esc) Alterar(l, mp.x < ce.x + ce.width * 0.3f ? -1 : 1);
                break;
            case Tipo.Botao: if (l.acao != null) l.acao(); break;
            case Tipo.Tecla:
                if (aba == 4) IniciarCaptura(0);
                else if (CaixaDaTecla(r, 0).Contains(mp)) { slotSel = 0; IniciarCaptura(0); }
                else if (CaixaDaTecla(r, 1).Contains(mp)) { slotSel = 1; IniciarCaptura(1); }
                break;
        }
    }

    void DesenharLinha(Linha l, Rect r, bool marcada, float fade, Font fTexto, Color ouro)
    {
        if (l.tipo == Tipo.Cabecalho)
        {
            GUIStyle sc = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(25f * esc), alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(r.x + 6f * esc, r.y, 600f * esc, r.height - 8f * esc), l.rotulo.ToUpperInvariant(), Col(sc, new Color(ouro.r, ouro.g, ouro.b, 0.9f * fade)));
            Cor(new Rect(r.x + 6f * esc, r.yMax - 4f * esc, r.width - 12f * esc, 1.5f * esc), new Color(ouro.r, ouro.g, ouro.b, 0.3f * fade));
            return;
        }
        if (l.tipo == Tipo.Texto)
        {
            GUIStyle st = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(23f * esc), alignment = TextAnchor.MiddleLeft, wordWrap = true };
            GUI.Label(new Rect(r.x + 8f * esc, r.y, r.width - 16f * esc, r.height), l.texto(), Col(st, new Color(0.68f, 0.63f, 0.54f, 0.95f * fade)));
            return;
        }

        // linha "solta": só uma faixa dourada na esquerda e um fundo suave quando está marcada
        if (marcada)
        {
            Cor(r, new Color(ouro.r, ouro.g, ouro.b, 0.11f * fade), 4f * esc);
            Cor(new Rect(r.x, r.y + 6f * esc, 4f * esc, r.height - 12f * esc), new Color(ouro.r, ouro.g, ouro.b, fade), 2f * esc);
        }
        else Cor(new Rect(r.x + 8f * esc, r.yMax - 1f, r.width - 16f * esc, 1f), new Color(1f, 1f, 1f, 0.05f * fade));

        GUIStyle sl = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(29f * esc), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        Color cl = marcada ? new Color(1f, 0.95f, 0.82f, fade) : new Color(0.84f, 0.8f, 0.72f, fade);
        if (l.tipo != Tipo.Botao) GUI.Label(new Rect(r.x + 24f * esc, r.y, r.width * 0.5f, r.height), l.rotulo, Col(sl, cl));

        switch (l.tipo)
        {
            case Tipo.Slider:
            {
                Rect tr = Trilho(r); float v = l.pegaF(); float t = Mathf.InverseLerp(l.min, l.max, v);
                Cor(tr, new Color(1f, 1f, 1f, 0.14f * fade), 3f * esc);
                Cor(new Rect(tr.x, tr.y, tr.width * t, tr.height), new Color(ouro.r, ouro.g, ouro.b, 0.95f * fade), 3f * esc);
                float kr = (marcada ? 11f : 9f) * esc;
                Cor(new Rect(tr.x + tr.width * t - kr, tr.center.y - kr, kr * 2f, kr * 2f), new Color(1f, 0.93f, 0.76f, fade), kr);
                GUIStyle sv = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(27f * esc), alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(tr.xMax + 12f * esc, r.y, r.xMax - tr.xMax - 24f * esc, r.height), l.fmt(v), Col(sv, cl));
                break;
            }
            case Tipo.Alternar:
            {
                bool on = l.pegaB(); Rect p = Pilula(r);
                Cor(p, on ? new Color(ouro.r, ouro.g, ouro.b, 0.95f * fade) : new Color(1f, 1f, 1f, 0.16f * fade), p.height * 0.5f);
                float kr = p.height * 0.5f - 4f * esc;
                Cor(new Rect(on ? p.xMax - kr * 2f - 4f * esc : p.x + 4f * esc, p.center.y - kr, kr * 2f, kr * 2f), on ? new Color(0.15f, 0.1f, 0.04f, fade) : new Color(0.85f, 0.82f, 0.75f, fade), kr);
                GUIStyle sv = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(24f * esc), alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(p.x - 150f * esc, r.y, 136f * esc, r.height), on ? "Ligado" : "Desligado", Col(sv, on ? cl : new Color(0.58f, 0.54f, 0.48f, fade)));
                break;
            }
            case Tipo.Escolha:
            {
                Rect c = CaixaEscolha(r); int i = Mathf.Clamp(l.pegaI(), 0, l.opcoes.Length - 1);
                GUIStyle sv = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(27f * esc), alignment = TextAnchor.MiddleCenter };
                GUI.Label(c, l.opcoes[i], Col(sv, cl));
                Color seta = marcada ? new Color(ouro.r, ouro.g, ouro.b, fade) : new Color(0.5f, 0.46f, 0.4f, fade);
                GUIStyle ss = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(34f * esc), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(c.x, c.y, 44f * esc, c.height), "<", Col(ss, seta));
                GUI.Label(new Rect(c.xMax - 44f * esc, c.y, 44f * esc, c.height), ">", Col(ss, seta));
                break;
            }
            case Tipo.Botao:
            {
                GUIStyle sb = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(30f * esc), alignment = TextAnchor.MiddleLeft };
                GUI.Label(new Rect(r.x + 24f * esc, r.y, r.width, r.height), "> " + l.rotulo, Col(sb, marcada ? new Color(1f, 0.9f, 0.62f, fade) : new Color(ouro.r, ouro.g, ouro.b, 0.9f * fade)));
                break;
            }
            case Tipo.Tecla:
            {
                int slots = aba == 4 ? 1 : 2;
                for (int s = 0; s < slots; s++)
                {
                    Rect c = CaixaDaTecla(r, s);
                    bool esteSlot = marcada && (aba == 4 || slotSel == s);
                    bool cap = capturando && marcada && (aba == 4 || capSlot == s);
                    Cor(new Rect(c.x - 1.5f, c.y - 1.5f, c.width + 3f, c.height + 3f), cap ? new Color(1f, 0.85f, 0.4f, fade) : esteSlot ? new Color(ouro.r, ouro.g, ouro.b, 0.85f * fade) : new Color(0.35f, 0.31f, 0.26f, 0.7f * fade), 6f * esc);
                    Cor(c, cap ? new Color(0.22f, 0.16f, 0.06f, 0.98f * fade) : new Color(0.04f, 0.035f, 0.035f, 0.96f * fade), 5f * esc);
                    string nome;
                    if (cap) nome = (Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 2 == 0) ? "Aperte…" : "";
                    else nome = aba == 4 ? Comandos.NomeDoBotao(Comandos.Botao(l.acaoDeTecla)) : Comandos.NomeDaTecla(Comandos.Tecla(l.acaoDeTecla, s));
                    GUIStyle sv = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(25f * esc), alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
                    GUI.Label(c, nome, Col(sv, cap ? new Color(1f, 0.9f, 0.6f, fade) : esteSlot ? new Color(1f, 0.95f, 0.82f, fade) : new Color(0.8f, 0.76f, 0.68f, fade)));
                }
                break;
            }
        }
    }

    static GUIStyle Col(GUIStyle s, Color c) { return MenuDePausa.Colorir(s, c); }
    static void Cor(Rect r, Color c, float raio = 0f) { MenuDePausa.Cor(r, c, raio); }
}
