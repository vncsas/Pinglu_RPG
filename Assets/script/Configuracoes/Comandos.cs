using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Tudo o que o jogador pode fazer e qual tecla (ou botão do controle) dispara cada ação.
public enum Acao
{
    Frente, Tras, Esquerda, Direita, Correr, Pular, Rolar,
    Atacar, Defender, GuardarSacar, Habilidade1, Habilidade2, Habilidade3,
    Interagir, Cristal, Voar, Subir, Descer, Mapa, ZoomMais, ZoomMenos, Reviver
}

// Camada única de leitura de entrada: o resto do jogo pergunta "Comandos.Apertou(Acao.Pular)" em vez de olhar uma tecla fixa.
// Cada ação tem 2 teclas/botões de mouse (principal e alternativa) e 1 botão de controle. Mover usa o analógico esquerdo, a câmera o direito.
public static class Comandos
{
    const string P = "pinglu.cmd.";
    static readonly int N = System.Enum.GetValues(typeof(Acao)).Length;
    static KeyCode[] t0, t1;
    static int[] pad;           // GamepadButton, ou -1 = sem botão

    // botões do controle que podem ser escolhidos
    public static readonly GamepadButton[] BotoesDoControle =
    {
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        GamepadButton.LeftShoulder, GamepadButton.RightShoulder, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
        GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
        GamepadButton.LeftStick, GamepadButton.RightStick, GamepadButton.Select
    };

    // as ações que aparecem na lista do controle (movimento e câmera ficam nos analógicos)
    public static readonly Acao[] AcoesDoControle =
    {
        Acao.Pular, Acao.Rolar, Acao.Atacar, Acao.Defender, Acao.Correr, Acao.GuardarSacar,
        Acao.Habilidade1, Acao.Habilidade2, Acao.Habilidade3, Acao.Interagir, Acao.Cristal,
        Acao.Voar, Acao.Subir, Acao.Descer, Acao.Mapa, Acao.Reviver
    };

    static Comandos() { PadroesTeclado(); PadroesControle(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarEstatico() { mv = Vector2.zero; quadroMover = -1; dirAnt = Vector2Int.zero; usouControle = false; }

    // ------------------------------------------------------------------ padrões
    static void Tec(Acao a, KeyCode k0, KeyCode k1 = KeyCode.None) { t0[(int)a] = k0; t1[(int)a] = k1; }

    static void PadroesTeclado()
    {
        t0 = new KeyCode[N]; t1 = new KeyCode[N];
        Tec(Acao.Frente, KeyCode.W, KeyCode.UpArrow); Tec(Acao.Tras, KeyCode.S, KeyCode.DownArrow);
        Tec(Acao.Esquerda, KeyCode.A, KeyCode.LeftArrow); Tec(Acao.Direita, KeyCode.D, KeyCode.RightArrow);
        Tec(Acao.Correr, KeyCode.LeftShift); Tec(Acao.Pular, KeyCode.Space); Tec(Acao.Rolar, KeyCode.LeftControl);
        Tec(Acao.Atacar, KeyCode.Mouse0); Tec(Acao.Defender, KeyCode.Mouse1, KeyCode.Q); Tec(Acao.GuardarSacar, KeyCode.H);
        Tec(Acao.Habilidade1, KeyCode.Alpha1, KeyCode.Keypad1); Tec(Acao.Habilidade2, KeyCode.Alpha2, KeyCode.Keypad2); Tec(Acao.Habilidade3, KeyCode.Alpha3, KeyCode.Keypad3);
        Tec(Acao.Interagir, KeyCode.E); Tec(Acao.Cristal, KeyCode.B); Tec(Acao.Voar, KeyCode.F);
        Tec(Acao.Subir, KeyCode.Space); Tec(Acao.Descer, KeyCode.C); Tec(Acao.Mapa, KeyCode.M);
        Tec(Acao.ZoomMais, KeyCode.Equals, KeyCode.KeypadPlus); Tec(Acao.ZoomMenos, KeyCode.Minus, KeyCode.KeypadMinus);
        Tec(Acao.Reviver, KeyCode.R);
    }

    static void Bot(Acao a, GamepadButton b) { pad[(int)a] = (int)b; }

    static void PadroesControle()
    {
        pad = new int[N]; for (int i = 0; i < N; i++) pad[i] = -1;
        Bot(Acao.Pular, GamepadButton.South); Bot(Acao.Rolar, GamepadButton.East);
        Bot(Acao.Atacar, GamepadButton.RightTrigger); Bot(Acao.Defender, GamepadButton.LeftTrigger);
        Bot(Acao.Correr, GamepadButton.LeftStick); Bot(Acao.GuardarSacar, GamepadButton.DpadUp);
        Bot(Acao.Habilidade1, GamepadButton.West); Bot(Acao.Habilidade2, GamepadButton.North); Bot(Acao.Habilidade3, GamepadButton.RightShoulder);
        Bot(Acao.Interagir, GamepadButton.DpadRight); Bot(Acao.Cristal, GamepadButton.DpadRight); Bot(Acao.Voar, GamepadButton.DpadLeft);
        Bot(Acao.Subir, GamepadButton.South); Bot(Acao.Descer, GamepadButton.East);
        Bot(Acao.Mapa, GamepadButton.Select); Bot(Acao.Reviver, GamepadButton.South);
    }

    public static void RestaurarTeclado() { PadroesTeclado(); }
    public static void RestaurarControle() { PadroesControle(); }

    // ------------------------------------------------------------------ salvar / carregar
    public static void Carregar()
    {
        PadroesTeclado(); PadroesControle();
        for (int i = 0; i < N; i++)
        {
            t0[i] = (KeyCode)PlayerPrefs.GetInt(P + "a" + i, (int)t0[i]);
            t1[i] = (KeyCode)PlayerPrefs.GetInt(P + "b" + i, (int)t1[i]);
            pad[i] = PlayerPrefs.GetInt(P + "p" + i, pad[i]);
        }
    }

    public static void Salvar()
    {
        for (int i = 0; i < N; i++)
        {
            PlayerPrefs.SetInt(P + "a" + i, (int)t0[i]);
            PlayerPrefs.SetInt(P + "b" + i, (int)t1[i]);
            PlayerPrefs.SetInt(P + "p" + i, pad[i]);
        }
    }

    // ------------------------------------------------------------------ escolher teclas
    public static KeyCode Tecla(Acao a, int slot) { Configuracoes.Garantir(); return slot == 0 ? t0[(int)a] : t1[(int)a]; }
    public static int Botao(Acao a) { Configuracoes.Garantir(); return pad[(int)a]; }

    // coloca a tecla na ação; se outra ação já usava essa tecla, ela fica com a que esta tinha antes (troca). Devolve o nome da ação trocada, ou null.
    public static string DefinirTecla(Acao a, int slot, KeyCode k)
    {
        Configuracoes.Garantir();
        int ia = (int)a;
        KeyCode antiga = slot == 0 ? t0[ia] : t1[ia];
        string trocou = null;
        if (k != KeyCode.None)
        {
            for (int i = 0; i < N; i++)
            {
                if (i == ia || !Conflita((Acao)i, a)) continue;
                if (t0[i] == k) { t0[i] = antiga; trocou = NomeDaAcao((Acao)i); }
                if (t1[i] == k) { t1[i] = antiga; trocou = NomeDaAcao((Acao)i); }
            }
            if (slot == 0 && t1[ia] == k) t1[ia] = KeyCode.None;
            if (slot == 1 && t0[ia] == k) t0[ia] = KeyCode.None;
        }
        if (slot == 0) t0[ia] = k; else t1[ia] = k;
        return trocou;
    }

    public static string DefinirBotao(Acao a, GamepadButton b)
    {
        Configuracoes.Garantir();
        int antigo = pad[(int)a]; string trocou = null;
        for (int i = 0; i < N; i++)
            if (i != (int)a && pad[i] == (int)b && Conflita((Acao)i, a)) { pad[i] = antigo; trocou = NomeDaAcao((Acao)i); }
        pad[(int)a] = (int)b;
        return trocou;
    }

    public static void LimparBotao(Acao a) { Configuracoes.Garantir(); pad[(int)a] = -1; }

    // quais ações "disputam" a mesma tecla: o voo do Tanque (Subir/Descer) usa Pular/Descer de propósito, e Reviver só vale morto
    static bool Conflita(Acao x, Acao y)
    {
        if (x == y) return true;
        if (Par(x, y, Acao.Subir, Acao.Pular)) return false;
        if (x == Acao.Reviver || y == Acao.Reviver) return false;
        if (Par(x, y, Acao.Interagir, Acao.Cristal)) return false;
        if (Par(x, y, Acao.Subir, Acao.Rolar) || Par(x, y, Acao.Descer, Acao.Rolar)) return false;
        if (Par(x, y, Acao.Voar, Acao.Mapa)) return false;
        return true;
    }
    static bool Par(Acao x, Acao y, Acao a, Acao b) { return (x == a && y == b) || (x == b && y == a); }

    // ------------------------------------------------------------------ ler
    static bool TecladoAtivo { get { return Configuracoes.Modo != 2 || Gamepad.current == null; } }
    static bool ControleAtivo { get { return Configuracoes.Modo != 1 && Gamepad.current != null; } }

    public static bool Apertou(Acao a)
    {
        Configuracoes.Garantir(); int i = (int)a;
        if (TecladoAtivo && ((t0[i] != KeyCode.None && Input.GetKeyDown(t0[i])) || (t1[i] != KeyCode.None && Input.GetKeyDown(t1[i])))) return true;
        if (ControleAtivo && pad[i] >= 0) { ButtonControlLer(pad[i], out bool ap, out bool seg); if (ap) return true; }
        return false;
    }

    public static bool Segurando(Acao a)
    {
        Configuracoes.Garantir(); int i = (int)a;
        if (TecladoAtivo && ((t0[i] != KeyCode.None && Input.GetKey(t0[i])) || (t1[i] != KeyCode.None && Input.GetKey(t1[i])))) return true;
        if (ControleAtivo && pad[i] >= 0) { ButtonControlLer(pad[i], out bool ap, out bool seg); if (seg) return true; }
        return false;
    }

    static void ButtonControlLer(int botao, out bool apertou, out bool segurando)
    {
        var c = Gamepad.current[(GamepadButton)botao];
        apertou = c.wasPressedThisFrame; segurando = c.isPressed;
    }

    // movimento: teclado (com um pouco de suavização, como o eixo antigo) + analógico esquerdo
    static Vector2 mv; static int quadroMover = -1;
    public static Vector2 Mover()
    {
        Configuracoes.Garantir();
        if (quadroMover == Time.frameCount) return mv;
        quadroMover = Time.frameCount;
        Vector2 alvo = Vector2.zero;
        if (TecladoAtivo)
        {
            alvo.x = (Segurando(Acao.Direita) ? 1f : 0f) - (Segurando(Acao.Esquerda) ? 1f : 0f);
            alvo.y = (Segurando(Acao.Frente) ? 1f : 0f) - (Segurando(Acao.Tras) ? 1f : 0f);
        }
        mv = Vector2.MoveTowards(mv, alvo, 9f * Time.unscaledDeltaTime);
        if (ControleAtivo)
        {
            Vector2 s = ComZonaMorta(Gamepad.current.leftStick.ReadValue());
            if (s.sqrMagnitude > 0.0001f) { Vector2 r = mv + s; if (r.sqrMagnitude > 1f) r.Normalize(); return mv = r; }
        }
        return mv;
    }

    // câmera pelo analógico direito (graus por segundo já com a sensibilidade)
    public static Vector2 OlharControle()
    {
        Configuracoes.Garantir();
        if (!ControleAtivo) return Vector2.zero;
        Vector2 s = ComZonaMorta(Gamepad.current.rightStick.ReadValue());
        return s * s.magnitude * 170f * Configuracoes.SensControle;      // curva suave: pequenos toques viram ajustes finos
    }

    // o mouse só gira a câmera se o jogador não escolheu jogar só com o controle (e tiver um conectado)
    public static bool MouseAtivo { get { return Configuracoes.Modo != 2 || Gamepad.current == null; } }

    static Vector2 ComZonaMorta(Vector2 v)
    {
        float z = Mathf.Clamp(Configuracoes.ZonaMorta, 0.05f, 0.6f), m = v.magnitude;
        if (m <= z) return Vector2.zero;
        return v / m * Mathf.Clamp01((m - z) / (1f - z));
    }

    // ------------------------------------------------------------------ qual aparelho está em uso (para mostrar as dicas certas)
    static bool usouControle;
    public static bool UsandoControle
    {
        get
        {
            if (Gamepad.current == null || Configuracoes.Modo == 1) return false;
            if (Configuracoes.Modo == 2) return true;
            return usouControle;
        }
    }

    // chamado todo quadro pelo AplicadorDeConfiguracoes
    public static void AtualizarAparelho()
    {
        Gamepad g = Gamepad.current;
        if (g != null)
        {
            bool ativo = g.leftStick.ReadValue().sqrMagnitude > 0.36f || g.rightStick.ReadValue().sqrMagnitude > 0.36f;
            if (!ativo) foreach (GamepadButton b in BotoesDoControle) if (g[b].wasPressedThisFrame) { ativo = true; break; }
            if (ativo) { usouControle = true; return; }
        }
        Keyboard k = Keyboard.current; Mouse m = Mouse.current;
        if ((k != null && k.anyKey.wasPressedThisFrame) || (m != null && (m.delta.ReadValue().sqrMagnitude > 9f || m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)))
            usouControle = false;
    }

    // ------------------------------------------------------------------ navegação de menus (teclado e controle)
    static Vector2Int dirAnt; static float proxRepeticao;
    // devolve a direção apertada neste quadro (x: direita +, y: cima +), com repetição enquanto segura
    public static Vector2Int NavDirecao()
    {
        Vector2Int g = Vector2Int.zero;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) g.y = 1;
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) g.y = -1;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) g.x = 1;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) g.x = -1;
        Gamepad p = Gamepad.current;
        if (p != null)
        {
            Vector2 s = p.leftStick.ReadValue();
            if (p.dpad.up.isPressed || s.y > 0.6f) g.y = 1; else if (p.dpad.down.isPressed || s.y < -0.6f) g.y = -1;
            if (p.dpad.right.isPressed || s.x > 0.6f) g.x = 1; else if (p.dpad.left.isPressed || s.x < -0.6f) g.x = -1;
        }
        Vector2Int d = Vector2Int.zero;
        if (g != Vector2Int.zero)
        {
            if (g != dirAnt || Time.unscaledTime >= proxRepeticao) { d = g; proxRepeticao = Time.unscaledTime + (g != dirAnt ? 0.38f : 0.09f); }
        }
        dirAnt = g;
        return d;
    }

    public static bool NavConfirmar()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) return true;
        return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    }
    public static bool NavVoltar() { return Input.GetKeyDown(KeyCode.Escape) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame); }
    public static bool NavPausa() { return Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame; }
    // trocar de aba: Q/E, Tab ou LB/RB
    public static int NavAba()
    {
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab)) return 1;
        if (Input.GetKeyDown(KeyCode.Q)) return -1;
        Gamepad p = Gamepad.current;
        if (p != null) { if (p.rightShoulder.wasPressedThisFrame) return 1; if (p.leftShoulder.wasPressedThisFrame) return -1; }
        return 0;
    }

    // ------------------------------------------------------------------ vibração
    static float fimVibracao;
    public static void Vibrar(float forca, float duracao, bool mesmoPausado = false)
    {
        if (!Configuracoes.Vibracao || !ControleAtivo || (Time.timeScale <= 0f && !mesmoPausado)) return;
        forca = Mathf.Clamp01(forca);
        Gamepad.current.SetMotorSpeeds(forca * 0.6f, forca);
        fimVibracao = Mathf.Max(fimVibracao, Time.unscaledTime + Mathf.Clamp(duracao, 0.05f, 0.6f));
    }
    public static void AtualizarVibracao()
    {
        if (fimVibracao > 0f && Time.unscaledTime >= fimVibracao) { fimVibracao = 0f; if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f); }
    }
    public static void ParaDeVibrar() { fimVibracao = 0f; foreach (Gamepad g in Gamepad.all) g.SetMotorSpeeds(0f, 0f); }

    // ------------------------------------------------------------------ nomes
    public static string NomeDaAcao(Acao a)
    {
        switch (a)
        {
            case Acao.Frente: return "Andar para frente";
            case Acao.Tras: return "Andar para trás";
            case Acao.Esquerda: return "Andar para a esquerda";
            case Acao.Direita: return "Andar para a direita";
            case Acao.Correr: return "Correr";
            case Acao.Pular: return "Pular";
            case Acao.Rolar: return "Rolar / esquivar";
            case Acao.Atacar: return "Atacar";
            case Acao.Defender: return "Defender";
            case Acao.GuardarSacar: return "Guardar / sacar arma";
            case Acao.Habilidade1: return "Habilidade 1";
            case Acao.Habilidade2: return "Habilidade 2";
            case Acao.Habilidade3: return "Habilidade 3";
            case Acao.Interagir: return "Falar / interagir";
            case Acao.Cristal: return "Pegar o cristal";
            case Acao.Voar: return "Voar (Tanque)";
            case Acao.Subir: return "Subir (voando)";
            case Acao.Descer: return "Descer (voando)";
            case Acao.Mapa: return "Mapa do mundo";
            case Acao.ZoomMais: return "Aproximar minimapa";
            case Acao.ZoomMenos: return "Afastar minimapa";
            case Acao.Reviver: return "Renascer";
        }
        return a.ToString();
    }

    // nomes curtos para o desenho do controle
    public static string NomeCurto(Acao a)
    {
        switch (a)
        {
            case Acao.GuardarSacar: return "Guardar arma";
            case Acao.Habilidade1: return "Habilidade 1";
            case Acao.Habilidade2: return "Habilidade 2";
            case Acao.Habilidade3: return "Habilidade 3";
            case Acao.Interagir: return "Falar";
            case Acao.Cristal: return "Pegar cristal";
            case Acao.Voar: return "Voar";
            case Acao.Subir: return "Subir";
            case Acao.Descer: return "Descer";
            case Acao.Rolar: return "Rolar";
            case Acao.Mapa: return "Mapa";
            case Acao.Reviver: return "Renascer";
        }
        return NomeDaAcao(a);
    }

    public static string NomeDaTecla(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.None: return "—";
            case KeyCode.Mouse0: return "Mouse esq.";
            case KeyCode.Mouse1: return "Mouse dir.";
            case KeyCode.Mouse2: return "Mouse meio";
            case KeyCode.Mouse3: return "Mouse 4";
            case KeyCode.Mouse4: return "Mouse 5";
            case KeyCode.Space: return "Espaço";
            case KeyCode.LeftShift: return "Shift esq.";
            case KeyCode.RightShift: return "Shift dir.";
            case KeyCode.LeftControl: return "Ctrl esq.";
            case KeyCode.RightControl: return "Ctrl dir.";
            case KeyCode.LeftAlt: return "Alt esq.";
            case KeyCode.RightAlt: return "Alt dir.";
            case KeyCode.Return: return "Enter";
            case KeyCode.KeypadEnter: return "Enter (num)";
            case KeyCode.Backspace: return "Backspace";
            case KeyCode.Tab: return "Tab";
            case KeyCode.CapsLock: return "Caps Lock";
            case KeyCode.UpArrow: return "Seta cima";
            case KeyCode.DownArrow: return "Seta baixo";
            case KeyCode.LeftArrow: return "Seta esq.";
            case KeyCode.RightArrow: return "Seta dir.";
            case KeyCode.Equals: return "=";
            case KeyCode.Minus: return "-";
            case KeyCode.KeypadPlus: return "Num +";
            case KeyCode.KeypadMinus: return "Num -";
            case KeyCode.BackQuote: return "`";
            case KeyCode.Comma: return ",";
            case KeyCode.Period: return ".";
            case KeyCode.Slash: return "/";
            case KeyCode.Semicolon: return ";";
        }
        string s = k.ToString();
        if (s.StartsWith("Alpha")) return s.Substring(5);
        if (s.StartsWith("Keypad")) return "Num " + s.Substring(6);
        return s;
    }

    // ---- que tipo de controle está ligado (sem controle: mostra PlayStation como exemplo)
    public enum TipoDeControle { PlayStation4, PlayStation5, Xbox, Generico }

    public static TipoDeControle Tipo
    {
        get
        {
            Gamepad g = Gamepad.current;
            if (g == null) return TipoDeControle.PlayStation4;
            string s = (g.layout + " " + g.displayName + " " + g.description.product + " " + g.description.manufacturer).ToLowerInvariant();
            if (s.Contains("dualsense") || s.Contains("ps5")) return TipoDeControle.PlayStation5;
            if (s.Contains("dualshock") || s.Contains("ps4") || s.Contains("playstation") || s.Contains("sony") || s.Contains("wireless controller")) return TipoDeControle.PlayStation4;
            if (s.Contains("xinput") || s.Contains("xbox") || s.Contains("microsoft")) return TipoDeControle.Xbox;
            return TipoDeControle.Generico;
        }
    }

    public static string NomeDoTipo(TipoDeControle t)
    {
        switch (t)
        {
            case TipoDeControle.PlayStation4: return "PlayStation 4 (DualShock 4)";
            case TipoDeControle.PlayStation5: return "PlayStation 5 (DualSense)";
            case TipoDeControle.Xbox: return "Xbox One / Series";
        }
        return "Controle genérico";
    }

    static bool PlayStation { get { TipoDeControle t = Tipo; return t == TipoDeControle.PlayStation4 || t == TipoDeControle.PlayStation5; } }
    public static bool EstiloPlayStation { get { return PlayStation; } }

    public static string NomeDoBotao(int b)
    {
        if (b < 0) return "—";
        bool ps = PlayStation;
        switch ((GamepadButton)b)
        {
            case GamepadButton.South: return ps ? "Cruz" : "A";
            case GamepadButton.East: return ps ? "Círculo" : "B";
            case GamepadButton.West: return ps ? "Quadrado" : "X";
            case GamepadButton.North: return ps ? "Triângulo" : "Y";
            case GamepadButton.LeftShoulder: return ps ? "L1" : "LB";
            case GamepadButton.RightShoulder: return ps ? "R1" : "RB";
            case GamepadButton.LeftTrigger: return ps ? "L2" : "LT";
            case GamepadButton.RightTrigger: return ps ? "R2" : "RT";
            case GamepadButton.DpadUp: return "Direcional cima";
            case GamepadButton.DpadDown: return "Direcional baixo";
            case GamepadButton.DpadLeft: return "Direcional esq.";
            case GamepadButton.DpadRight: return "Direcional dir.";
            case GamepadButton.LeftStick: return ps ? "L3" : "LS (clique)";
            case GamepadButton.RightStick: return ps ? "R3" : "RS (clique)";
            case GamepadButton.Select: return ps ? "Share" : "Voltar";
            case GamepadButton.Start: return ps ? "Options" : "Menu";
        }
        return ((GamepadButton)b).ToString();
    }

    // nome curto para caber nas dicas da tela ("E", "1", "RB")
    public static string Dica(Acao a)
    {
        Configuracoes.Garantir();
        if (UsandoControle)
        {
            int b = pad[(int)a];
            if (b >= 0) return CurtoDoBotao(b);
        }
        KeyCode k = t0[(int)a] != KeyCode.None ? t0[(int)a] : t1[(int)a];
        if (k == KeyCode.None && pad[(int)a] >= 0 && Gamepad.current != null) return CurtoDoBotao(pad[(int)a]);
        return NomeDaTecla(k);
    }

    static string CurtoDoBotao(int b)
    {
        switch ((GamepadButton)b)
        {
            case GamepadButton.DpadUp: return "↑";
            case GamepadButton.DpadDown: return "↓";
            case GamepadButton.DpadLeft: return "←";
            case GamepadButton.DpadRight: return "→";
            case GamepadButton.LeftStick: return "L3";
            case GamepadButton.RightStick: return "R3";
            case GamepadButton.Select: return PlayStation ? "Share" : "Voltar";
            case GamepadButton.South: return PlayStation ? "Cruz" : "A";
            case GamepadButton.East: return PlayStation ? "Círc." : "B";
            case GamepadButton.West: return PlayStation ? "Quad." : "X";
            case GamepadButton.North: return PlayStation ? "Tri." : "Y";
        }
        return NomeDoBotao(b);
    }

    public static string NomeDoControle() { return Gamepad.current != null ? Gamepad.current.displayName : null; }
}
