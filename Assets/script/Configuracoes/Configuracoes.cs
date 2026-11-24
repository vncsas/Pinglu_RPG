using UnityEngine;

// Todas as opções do jogo (áudio, vídeo, câmera, controles). Ficam salvas no computador (PlayerPrefs) e valem em todas as partidas.
// A tela para mexer nelas é a TelaDeConfiguracoes (aberta pelo menu de pausa); quem aplica no jogo é o AplicadorDeConfiguracoes.
public static class Configuracoes
{
    const string P = "pinglu.cfg.";

    // ---- áudio (0 a 1)
    public static float VolMestre = 1f, VolMusica = 0.8f, VolEfeitos = 1f;

    // ---- vídeo
    public static int ModoTela = -1;            // -1 = como está; 0 tela cheia, 1 janela sem bordas, 2 janela
    public static int ResolucaoW = 0, ResolucaoH = 0;   // 0 = como está
    public static bool VSync = true;
    public static int LimiteFps = 60;           // 0 = sem limite
    public static int Escala = 0;               // 0 = automática (o jogo ajusta sozinho); senão a porcentagem fixa (100, 85, 70, 50)
    public static int Msaa = -1;                // -1 = como o projeto veio; 0 desligado; 2, 4 ou 8
    public static int Sombras = 3;              // 0 desligadas, 1 curtas, 2 médias, 3 longas (padrão)
    public static int Texturas = 0;             // 0 alta, 1 média, 2 baixa
    public static int Distancia = 0;            // distância de visão: 0 padrão, 1 curta, 2 média, 3 longa
    public static float Brilho = 0f;            // -0,5 a 0,5
    public static bool MostrarFps = false;

    // ---- câmera e controles
    public static float SensMouse = 1f, SensControle = 1f;
    public static bool InverterY = false, InverterX = false;
    public static float Fov = 0f;               // 0 = o da cena
    public static float Tremor = 1f;            // 0 a 1: força do tremor de câmera
    public static float ZonaMorta = 0.2f;       // analógicos do controle
    public static bool Vibracao = true;
    public static int Modo = 0;                 // 0 automático, 1 teclado e mouse, 2 controle

    // ---- interface e jogo
    public static float EscalaUI = 1f;
    public static bool MostrarMinimapa = true;
    public static bool PausarSemFoco = true;

    public static event System.Action AoMudar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { carregado = false; }

    static bool carregado;
    public static void Garantir() { if (!carregado) Carregar(); }

    static float F(string k, float p) { return PlayerPrefs.GetFloat(P + k, p); }
    static int I(string k, int p) { return PlayerPrefs.GetInt(P + k, p); }
    static bool B(string k, bool p) { return PlayerPrefs.GetInt(P + k, p ? 1 : 0) == 1; }
    static void S(string k, float v) { PlayerPrefs.SetFloat(P + k, v); }
    static void S(string k, int v) { PlayerPrefs.SetInt(P + k, v); }
    static void S(string k, bool v) { PlayerPrefs.SetInt(P + k, v ? 1 : 0); }

    public static void Carregar()
    {
        carregado = true;
        VolMestre = F("volMestre", 1f); VolMusica = F("volMusica", 0.8f); VolEfeitos = F("volEfeitos", 1f);
        ModoTela = I("modoTela", -1); ResolucaoW = I("resW", 0); ResolucaoH = I("resH", 0);
        VSync = B("vsync", true); LimiteFps = I("fps", 60); Escala = I("escala", 0); Msaa = I("msaa", -1);
        Sombras = I("sombras", 3); Texturas = I("texturas", 0); Distancia = I("distancia", 0);
        Brilho = F("brilho", 0f); MostrarFps = B("mostrarFps", false);
        SensMouse = F("sensMouse", 1f); SensControle = F("sensControle", 1f);
        InverterY = B("invY", false); InverterX = B("invX", false);
        Fov = F("fov", 0f); Tremor = F("tremor", 1f); ZonaMorta = F("zona", 0.2f);
        Vibracao = B("vibracao", true); Modo = I("modo", 0);
        EscalaUI = F("escalaUI", 1f); MostrarMinimapa = B("minimapa", true); PausarSemFoco = B("pausarSemFoco", true);
        Comandos.Carregar();
    }

    public static void Salvar()
    {
        S("volMestre", VolMestre); S("volMusica", VolMusica); S("volEfeitos", VolEfeitos);
        S("modoTela", ModoTela); S("resW", ResolucaoW); S("resH", ResolucaoH);
        S("vsync", VSync); S("fps", LimiteFps); S("escala", Escala); S("msaa", Msaa);
        S("sombras", Sombras); S("texturas", Texturas); S("distancia", Distancia);
        S("brilho", Brilho); S("mostrarFps", MostrarFps);
        S("sensMouse", SensMouse); S("sensControle", SensControle);
        S("invY", InverterY); S("invX", InverterX);
        S("fov", Fov); S("tremor", Tremor); S("zona", ZonaMorta);
        S("vibracao", Vibracao); S("modo", Modo);
        S("escalaUI", EscalaUI); S("minimapa", MostrarMinimapa); S("pausarSemFoco", PausarSemFoco);
        Comandos.Salvar();
        PlayerPrefs.Save();
    }

    // chamado pela tela depois de cada mudança: aplica no jogo e guarda
    public static void Mudou(bool salvar = true)
    {
        if (AoMudar != null) AoMudar();
        if (salvar) Salvar();
    }

    // volta uma parte para o padrão de fábrica. aba: 0 jogabilidade, 1 áudio, 2 vídeo, 3 teclado e mouse, 4 controle
    public static void RestaurarPadroes(int aba)
    {
        switch (aba)
        {
            case 0:
                SensMouse = 1f; SensControle = 1f; InverterY = false; InverterX = false; Fov = 0f; Tremor = 1f;
                EscalaUI = 1f; MostrarMinimapa = true; PausarSemFoco = true; Modo = 0; break;
            case 1: VolMestre = 1f; VolMusica = 0.8f; VolEfeitos = 1f; break;
            case 2:
                ModoTela = -1; ResolucaoW = 0; ResolucaoH = 0; VSync = true; LimiteFps = 60; Escala = 0; Msaa = -1;
                Sombras = 3; Texturas = 0; Distancia = 0; Brilho = 0f; MostrarFps = false; break;
            case 3: Comandos.RestaurarTeclado(); break;
            case 4: Comandos.RestaurarControle(); ZonaMorta = 0.2f; Vibracao = true; break;
        }
        Mudou();
    }
}
