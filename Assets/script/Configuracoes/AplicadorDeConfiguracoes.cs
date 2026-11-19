using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Pega o que está em Configuracoes e faz valer no jogo: volumes, tela, qualidade gráfica, brilho, contador de FPS, vibração...
// Nasce sozinho ao abrir a cena (não precisa colocar em objeto nenhum).
public class AplicadorDeConfiguracoes : MonoBehaviour
{
    public static AplicadorDeConfiguracoes Instancia { get; private set; }

    class Reg { public AudioSource fonte; public float baseVol, aplicado = -1f; }
    readonly List<Reg> regs = new List<Reg>();
    float proxBusca;

    UniversalRenderPipelineAsset urp;
    int msaaOriginal; bool guardouUrp;
    float fps, acumFps; int quadrosFps; float proxFps;
    int ultimoControle = -1; string avisoTitulo, avisoLinha; float avisoInicio = -99f; bool avisoLigou;
    Font fCinzel, fFell;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar()
    {
        if (Instancia != null) return;
        GameObject g = new GameObject("Aplicador de configurações"); DontDestroyOnLoad(g); g.AddComponent<AplicadorDeConfiguracoes>();
    }

    void Awake()
    {
        if (Instancia != null) { Destroy(gameObject); return; }
        Instancia = this;
        Configuracoes.Garantir();
        Configuracoes.AoMudar += Aplicar;
        Aplicar();
        AplicarTela();
        Invoke(nameof(Aplicar), 1.5f);           // de novo depois que os outros sistemas (desempenho adaptativo) acordam
    }

    void OnDestroy()
    {
        if (Instancia != this) return;
        Configuracoes.AoMudar -= Aplicar;
        Devolver();
        Instancia = null;
    }

    void OnApplicationQuit() { Devolver(); }

    // devolve o asset do URP como estava (no Editor ele é compartilhado e ficaria alterado)
    void Devolver()
    {
        if (urp != null && guardouUrp) urp.msaaSampleCount = msaaOriginal;
        Comandos.ParaDeVibrar();
    }

    void OnApplicationFocus(bool foco)
    {
        if (!foco && Configuracoes.PausarSemFoco) MenuDePausa.PedirPausa();
    }

    // ------------------------------------------------------------------ aplicar
    public static void AplicarFps()
    {
        Configuracoes.Garantir();
        QualitySettings.vSyncCount = Configuracoes.VSync ? 1 : 0;
        Application.targetFrameRate = Configuracoes.LimiteFps > 0 ? Configuracoes.LimiteFps : -1;
    }

    public void Aplicar()
    {
        AudioListener.volume = Mathf.Clamp01(Configuracoes.VolMestre);
        AplicarFps();
        QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(Configuracoes.Texturas, 0, 3);

        if (urp == null) urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            if (!guardouUrp) { msaaOriginal = urp.msaaSampleCount; guardouUrp = true; }
            urp.msaaSampleCount = Configuracoes.Msaa < 0 ? msaaOriginal : Configuracoes.Msaa;
        }
        if (DesempenhoAdaptativo.Instancia != null) DesempenhoAdaptativo.Instancia.Reaplicar();
    }

    // modo de tela e resolução (só quando o jogador aperta "Aplicar": trocar a resolução a cada clique atrapalha)
    public static void AplicarTela()
    {
        Configuracoes.Garantir();
        if (Configuracoes.ModoTela < 0 && Configuracoes.ResolucaoW <= 0) return;
        FullScreenMode modo = Configuracoes.ModoTela == 0 ? FullScreenMode.ExclusiveFullScreen
            : Configuracoes.ModoTela == 1 ? FullScreenMode.FullScreenWindow
            : Configuracoes.ModoTela == 2 ? FullScreenMode.Windowed : Screen.fullScreenMode;
        int w = Configuracoes.ResolucaoW > 0 ? Configuracoes.ResolucaoW : Screen.width;
        int h = Configuracoes.ResolucaoH > 0 ? Configuracoes.ResolucaoH : Screen.height;
        Screen.SetResolution(w, h, modo);
    }

    // ------------------------------------------------------------------ todo quadro
    void Update()
    {
        // controle ligado ou desligado: mostra um aviso
        var gp = UnityEngine.InputSystem.Gamepad.current;
        int idControle = gp != null ? gp.deviceId : 0;
        if (idControle != ultimoControle && Time.unscaledTime > 1f)
        {
            if (idControle != 0) { avisoLigou = true; avisoTitulo = "CONTROLE CONECTADO"; avisoLinha = Comandos.NomeDoTipo(Comandos.Tipo); avisoInicio = Time.unscaledTime; }
            else if (ultimoControle > 0) { avisoLigou = false; avisoTitulo = "CONTROLE DESCONECTADO"; avisoLinha = "Voltando para teclado e mouse"; avisoInicio = Time.unscaledTime; }
            ultimoControle = idControle;
        }
        Comandos.AtualizarAparelho();
        Comandos.AtualizarVibracao();
        acumFps += Time.unscaledDeltaTime; quadrosFps++;
        if (Time.unscaledTime >= proxFps) { fps = quadrosFps / Mathf.Max(0.001f, acumFps); acumFps = 0f; quadrosFps = 0; proxFps = Time.unscaledTime + 0.5f; }
    }

    // volume de música/ambiente (sons em loop) e de efeitos (o resto), por cima do volume que cada script escolheu
    void LateUpdate()
    {
        if (Time.unscaledTime >= proxBusca)
        {
            proxBusca = Time.unscaledTime + 1f;
            for (int i = regs.Count - 1; i >= 0; i--) if (regs[i].fonte == null) regs.RemoveAt(i);
            foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                bool tem = false;
                for (int i = 0; i < regs.Count; i++) if (regs[i].fonte == s) { tem = true; break; }
                if (!tem) regs.Add(new Reg { fonte = s, baseVol = s.volume });
            }
        }
        for (int i = 0; i < regs.Count; i++)
        {
            Reg r = regs[i]; AudioSource s = r.fonte; if (s == null) continue;
            if (Mathf.Abs(s.volume - r.aplicado) > 0.0005f) r.baseVol = s.volume;        // o jogo mexeu no volume: guarda o novo
            r.aplicado = r.baseVol * (s.loop ? Configuracoes.VolMusica : Configuracoes.VolEfeitos);
            s.volume = r.aplicado;
        }
    }

    // multiplicador para sons soltos no ar (PlayClipAtPoint), que o aplicador não alcança
    public static float FatorDeEfeitos { get { Configuracoes.Garantir(); return Configuracoes.VolEfeitos; } }

    void DesenharAviso()
    {
        float t = Time.unscaledTime - avisoInicio, dur = 5f;
        if (t < 0f || t > dur) return;
        if (fCinzel == null) fCinzel = Resources.Load<Font>("Fontes/CinzelBold");
        if (fFell == null) fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
        float esc = Screen.height / 1080f, W = Screen.width;
        float entra = Mathf.SmoothStep(0f, 1f, t / 0.35f) * Mathf.SmoothStep(0f, 1f, (dur - t) / 0.5f);
        float pw = 560f * esc, ph = 120f * esc;
        Rect r = new Rect(W - pw - 40f * esc + (1f - entra) * 60f * esc, 40f * esc, pw, ph);
        float a = entra; Color ouro = new Color(0.86f, 0.68f, 0.34f);
        GUI.depth = -3100;
        MenuDePausa.Cor(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f), new Color(ouro.r, ouro.g, ouro.b, 0.7f * a), 10f * esc);
        MenuDePausa.Cor(r, new Color(0.035f, 0.032f, 0.04f, 0.97f * a), 9f * esc);
        MenuDePausa.Cor(new Rect(r.x, r.y + 12f * esc, 4f * esc, r.height - 24f * esc), new Color(avisoLigou ? 0.55f : 0.9f, avisoLigou ? 0.9f : 0.5f, avisoLigou ? 0.55f : 0.4f, a), 2f * esc);
        if (avisoLigou) DesenhoDeControle.Icone(new Rect(r.x + 24f * esc, r.y + 22f * esc, 120f * esc, 76f * esc), a);
        GUIStyle s1 = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(24f * esc), alignment = TextAnchor.UpperLeft };
        GUIStyle s2 = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(27f * esc), alignment = TextAnchor.UpperLeft };
        GUIStyle s3 = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(20f * esc), alignment = TextAnchor.UpperLeft };
        float tx = r.x + (avisoLigou ? 166f : 32f) * esc, tw = r.xMax - tx - 16f * esc;
        GUI.Label(new Rect(tx, r.y + 18f * esc, tw, 34f * esc), avisoTitulo, MenuDePausa.Colorir(s1, new Color(ouro.r, ouro.g, ouro.b, a)));
        GUI.Label(new Rect(tx, r.y + 52f * esc, tw, 36f * esc), avisoLinha, MenuDePausa.Colorir(s2, new Color(0.95f, 0.91f, 0.82f, a)));
        if (avisoLigou) GUI.Label(new Rect(tx, r.y + 88f * esc, tw, 26f * esc), "Veja os botões em Configurações > Controle", MenuDePausa.Colorir(s3, new Color(0.65f, 0.6f, 0.5f, a)));
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        DesenharAviso();
        if (Mathf.Abs(Configuracoes.Brilho) > 0.01f)
        {
            GUI.depth = -2500;
            Color c = Configuracoes.Brilho > 0f ? new Color(1f, 1f, 1f, Configuracoes.Brilho * 0.45f) : new Color(0f, 0f, 0f, -Configuracoes.Brilho * 1.1f);
            Color a = GUI.color; GUI.color = c; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = a;
        }
        if (Configuracoes.MostrarFps)
        {
            GUI.depth = -2501;
            GUIStyle st = new GUIStyle { fontSize = Mathf.RoundToInt(20f * Screen.height / 1080f), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            Rect r = new Rect(Screen.width - 190f, 8f, 180f, 30f);
            st.normal.textColor = Color.black; GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), Mathf.RoundToInt(fps) + " FPS", st);
            st.normal.textColor = fps >= 50f ? new Color(0.7f, 1f, 0.7f) : fps >= 30f ? new Color(1f, 0.92f, 0.5f) : new Color(1f, 0.55f, 0.5f);
            GUI.Label(r, Mathf.RoundToInt(fps) + " FPS", st);
        }
    }
}
