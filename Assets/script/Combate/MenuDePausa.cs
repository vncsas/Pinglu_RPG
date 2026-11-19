using UnityEngine;
using UnityEngine.SceneManagement;

// Menu de pausa (ESC): o jogo para e aparece um painel com Continuar, Configurações (ainda não funciona) e Sair.
// Sair pede confirmação e volta para a tela de seleção de personagem, com a história, as lutas e tudo o mais recomeçando do zero
// quando um personagem é escolhido de novo. Online o tempo não para (os outros jogadores continuam), mas o painel funciona igual.
public class MenuDePausa : MonoBehaviour
{
    bool aberto, confirmando, menuAntes;
    float abriuEm; int sel;
    Font fTitulo, fTexto;
    Rect[] caixas = new Rect[3];
    bool configAberta; TelaDeConfiguracoes config = new TelaDeConfiguracoes();
    static MenuDePausa instancia;

    // pausa pedida de fora (ex.: o jogador saiu da janela do jogo)
    public static void PedirPausa()
    {
        if (instancia != null && !instancia.aberto && PodeAbrir()) instancia.Abrir();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar()
    {
        if (FindFirstObjectByType<MenuDePausa>() != null) return;
        GameObject g = new GameObject("Menu de pausa"); DontDestroyOnLoad(g); g.AddComponent<MenuDePausa>();
    }

    void Awake() { instancia = this; SceneManager.sceneLoaded += (a, m) => { if (aberto) Fechar(false); }; }

    static bool AlguemJogando()
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) return true;
        return false;
    }

    static bool PodeAbrir() { return !SessaoDeJogo.MenuAberto && !DialogoUI.Aberto && !TelaDeMorte.Ativa && !CameraDeCena.Ativa && AlguemJogando(); }

    void Abrir()
    {
        aberto = true; confirmando = false; sel = 0; abriuEm = Time.unscaledTime;
        SessaoDeJogo.MenuAberto = true;
        if (!SessaoDeJogo.Online) { Time.timeScale = 0f; AudioListener.pause = true; }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    void Fechar(bool voltarAoJogo = true)
    {
        if (configAberta) config.Fechar();
        aberto = false; confirmando = false; configAberta = false;
        Time.timeScale = 1f; AudioListener.pause = false;
        if (voltarAoJogo)
        {
            SessaoDeJogo.MenuAberto = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
    }

    void Sair()
    {
        Time.timeScale = 1f; AudioListener.pause = false; aberto = false;
        Campanha.Reiniciar(); GerenciadorDeOndas.ReiniciarTudo(); Renascimento.Reiniciar(); GrupoDeAventureiros.Desfazer();
        if (SessaoDeJogo.SairParaSelecao != null) SessaoDeJogo.SairParaSelecao();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Ativar(int i)
    {
        if (confirmando) { if (i == 1) Sair(); else confirmando = false; return; }
        if (i == 0) Fechar();
        else if (i == 1) { configAberta = true; config.Abrir(); }
        else if (i == 2) { confirmando = true; sel = 0; }
    }

    void Update()
    {
        if (!aberto)
        {
            if ((Input.GetKeyDown(KeyCode.Escape) || Comandos.NavPausa()) && PodeAbrir()) Abrir();
            return;
        }
        if (Comandos.NavPausa() && !(configAberta && config.Capturando)) { Fechar(); return; }                 // Start do controle fecha tudo

        if (configAberta)
        {
            if (config.Atualizar() || config.ConsumirVoltar()) { config.Fechar(); configAberta = false; sel = 1; }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Comandos.NavVoltar()) { if (confirmando) confirmando = false; else Fechar(); return; }
        Vector2Int d = Comandos.NavDirecao();
        if (d.y != 0)
        {
            if (confirmando) sel = 1 - sel;
            else sel = (sel - d.y + 3) % 3;
        }
        else if (confirmando && d.x != 0) sel = d.x > 0 ? 1 : 0;
        if (Comandos.NavConfirmar()) Ativar(sel);
    }

    // ------------------------------------------------------------------ desenho
    static Texture2D degradeH;      // faixa que vai de opaca (esquerda) a transparente (direita)
    static Texture2D Degrade()
    {
        if (degradeH != null) return degradeH;
        degradeH = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 256; x++) { float t = 1f - x / 255f; degradeH.SetPixel(x, 0, new Color(1f, 1f, 1f, t * t * (3f - 2f * t))); }
        degradeH.Apply(); return degradeH;
    }
    static void Faixa(Rect r, Color c) { Color a = GUI.color; GUI.color = c; GUI.DrawTexture(r, Degrade()); GUI.color = a; }

    void OnGUI()
    {
        if (!aberto) return;
        GUI.depth = -3000;
        if (fTitulo == null) fTitulo = Resources.Load<Font>("Fontes/CinzelBold");
        if (fTexto == null) fTexto = Resources.Load<Font>("Fontes/IMFellEnglish");
        float W = Screen.width, H = Screen.height, esc = H / 1080f;
        float fade = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - abriuEm) / 0.25f);
        Event ev = Event.current;
        Color ouro = new Color(0.86f, 0.68f, 0.34f);

        if (configAberta) { config.Desenhar(new Rect(0, 0, W, H), esc, fade, ev, fTexto, fTitulo); GUI.color = Color.white; return; }

        if (ev.type == EventType.Repaint) Cor(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, (confirmando ? 0.72f : 0.5f) * fade));

        if (confirmando) { DesenharConfirmacao(W, H, esc, fade, ev, ouro); GUI.color = Color.white; return; }

        // ---- menu lateral (faixa escura na esquerda, itens como texto, sem caixas)
        float x0 = Mathf.Max(120f * esc, W * 0.085f), y0 = H * 0.16f;
        if (ev.type == EventType.Repaint)
        {
            Faixa(new Rect(0, 0, W * 0.62f, H), new Color(0.015f, 0.015f, 0.02f, 0.96f * fade));
            GUIStyle tit = new GUIStyle { font = fTitulo, fontSize = Mathf.RoundToInt(96f * esc), alignment = TextAnchor.UpperLeft };
            Letras(new Rect(x0, y0, 900f * esc, 120f * esc), "PAUSA", tit, 22f * esc, new Color(0.94f, 0.89f, 0.77f, fade), true);
            string cap = Campanha.Capitulo(Campanha.Atual);
            if (!string.IsNullOrEmpty(cap))
            {
                GUIStyle sc = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(28f * esc), alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Italic };
                GUI.Label(new Rect(x0 + 4f * esc, y0 + 126f * esc, 900f * esc, 40f * esc), cap, Colorir(sc, new Color(ouro.r, ouro.g, ouro.b, 0.95f * fade)));
            }
            float ly = y0 + 182f * esc;
            Cor(new Rect(x0 + 4f * esc, ly, 420f * esc, 1.5f * esc), new Color(ouro.r, ouro.g, ouro.b, 0.55f * fade));
            Matrix4x4 m = GUI.matrix; GUIUtility.RotateAroundPivot(45f, new Vector2(x0 + 4f * esc, ly + 0.75f * esc));
            Cor(new Rect(x0 - 2f * esc, ly - 6f * esc, 12f * esc, 12f * esc), new Color(ouro.r, ouro.g, ouro.b, fade)); GUI.matrix = m;
        }

        string[] nomes = { "CONTINUAR", "CONFIGURAÇÕES", "SAIR" };
        float iy = y0 + 232f * esc, ih = 88f * esc;
        for (int i = 0; i < 3; i++)
        {
            Rect r = new Rect(x0 - 30f * esc, iy + ih * i, 640f * esc, ih - 8f * esc);
            bool sobre = r.Contains(ev.mousePosition);
            if (sobre && ev.type == EventType.MouseMove) sel = i;
            if (ev.type == EventType.MouseDown && ev.button == 0 && sobre) { ev.Use(); Ativar(i); return; }
            if (ev.type != EventType.Repaint) continue;
            bool marcado = sel == i, perigo = i == 2;
            Color realce = perigo ? new Color(0.92f, 0.38f, 0.32f) : ouro;
            if (marcado)
            {
                Faixa(new Rect(r.x, r.y, r.width, r.height), new Color(realce.r, realce.g, realce.b, 0.2f * fade));
                Cor(new Rect(r.x, r.y + 8f * esc, 4f * esc, r.height - 16f * esc), new Color(realce.r, realce.g, realce.b, fade), 2f * esc);
            }
            GUIStyle st = new GUIStyle { font = fTitulo, fontSize = Mathf.RoundToInt(40f * esc), alignment = TextAnchor.UpperLeft };
            Color tc = marcado ? (perigo ? new Color(1f, 0.84f, 0.78f, fade) : new Color(1f, 0.95f, 0.82f, fade)) : new Color(0.7f, 0.66f, 0.58f, 0.95f * fade);
            Letras(new Rect(r.x + (marcado ? 52f : 40f) * esc, r.y + 14f * esc, 600f * esc, r.height), nomes[i], st, 6f * esc, tc, true);
        }

        // objetivo atual e dicas de comando
        if (ev.type == EventType.Repaint)
        {
            string obj = Campanha.Objetivo(Campanha.Atual);
            if (!string.IsNullOrEmpty(obj))
            {
                GUIStyle s0 = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(22f * esc), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
                GUIStyle s1 = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(28f * esc), alignment = TextAnchor.UpperLeft, wordWrap = true };
                float ow = 640f * esc, oh = s1.CalcHeight(new GUIContent(obj), ow);
                float oy = H - 118f * esc - oh - 38f * esc;
                GUI.Label(new Rect(x0, oy, ow, 30f * esc), "OBJETIVO ATUAL", Colorir(s0, new Color(ouro.r, ouro.g, ouro.b, 0.85f * fade)));
                GUI.Label(new Rect(x0, oy + 34f * esc, ow, oh), obj, Colorir(s1, new Color(0.84f, 0.8f, 0.71f, fade)));
            }
            GUIStyle dica = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(23f * esc), alignment = TextAnchor.MiddleLeft };
            string d = Comandos.UsandoControle
                ? Comandos.NomeDoBotao((int)UnityEngine.InputSystem.LowLevel.GamepadButton.South) + "  escolher        " + Comandos.NomeDoBotao((int)UnityEngine.InputSystem.LowLevel.GamepadButton.East) + "  continuar"
                : "W / S  escolher        Enter  confirmar        Esc  continuar";
            GUI.Label(new Rect(x0, H - 84f * esc, 900f * esc, 36f * esc), d, Colorir(dica, new Color(0.6f, 0.55f, 0.47f, 0.9f * fade)));
        }
        GUI.color = Color.white;
    }

    void DesenharConfirmacao(float W, float H, float esc, float fade, Event ev, Color ouro)
    {
        float pw = 820f * esc, pad = 56f * esc;
        GUIStyle t1 = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(36f * esc), alignment = TextAnchor.UpperCenter, wordWrap = true };
        GUIStyle t2 = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(27f * esc), alignment = TextAnchor.UpperCenter, wordWrap = true };
        string m1 = "Voltar para a seleção de personagem?", m2 = "O progresso desta partida será perdido.";
        float h1 = t1.CalcHeight(new GUIContent(m1), pw - pad * 2f), h2 = t2.CalcHeight(new GUIContent(m2), pw - pad * 2f);
        float ph = 44f * esc + 64f * esc + 34f * esc + h1 + 14f * esc + h2 + 44f * esc + 64f * esc + 44f * esc;
        Rect painel = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f + (1f - fade) * 16f * esc, pw, ph);
        if (ev.type == EventType.Repaint)
        {
            Cor(new Rect(painel.x - 1.5f, painel.y - 1.5f, pw + 3f, ph + 3f), new Color(ouro.r, ouro.g, ouro.b, 0.6f * fade), 9f * esc);
            Cor(painel, new Color(0.035f, 0.032f, 0.038f, 0.99f * fade), 8f * esc);
            GUIStyle tit = new GUIStyle { font = fTitulo, fontSize = Mathf.RoundToInt(46f * esc), alignment = TextAnchor.UpperLeft };
            Letras(new Rect(painel.x, painel.y + 40f * esc, pw, 64f * esc), "SAIR?", tit, 12f * esc, new Color(0.94f, 0.89f, 0.77f, fade));
            float ly = painel.y + 108f * esc;
            Cor(new Rect(painel.x + 80f * esc, ly, pw - 160f * esc, 1.5f * esc), new Color(ouro.r, ouro.g, ouro.b, 0.5f * fade));
            float ty = ly + 34f * esc;
            GUI.Label(new Rect(painel.x + pad, ty, pw - pad * 2f, h1), m1, Colorir(t1, new Color(0.94f, 0.9f, 0.82f, fade)));
            GUI.Label(new Rect(painel.x + pad, ty + h1 + 14f * esc, pw - pad * 2f, h2), m2, Colorir(t2, new Color(0.78f, 0.7f, 0.6f, fade)));
        }
        float by = painel.yMax - 44f * esc - 64f * esc, bw = 260f * esc, gap = 36f * esc;
        Botao(0, new Rect(painel.center.x - bw - gap * 0.5f, by, bw, 64f * esc), "Cancelar", true, esc, fade, ev);
        Botao(1, new Rect(painel.center.x + gap * 0.5f, by, bw, 64f * esc), "Sair", true, esc, fade, ev, true);
    }

    void Botao(int idx, Rect r, string texto, bool ativo, float esc, float fade, Event ev, bool perigo = false)
    {
        bool sobre = r.Contains(ev.mousePosition);
        if (ativo && sobre && ev.type == EventType.MouseMove) sel = idx;
        bool marcado = ativo && (sel == idx || sobre);
        if (ev.type == EventType.MouseDown && ev.button == 0 && sobre && ativo) { ev.Use(); Ativar(idx); return; }
        if (ev.type != EventType.Repaint) return;

        Color ouro = new Color(0.86f, 0.68f, 0.34f);
        Color realce = perigo ? new Color(0.92f, 0.38f, 0.32f) : ouro;
        Color borda = marcado ? realce : new Color(0.45f, 0.38f, 0.26f, 0.7f);
        Cor(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f), new Color(borda.r, borda.g, borda.b, borda.a * fade), 9f * esc);
        Cor(r, marcado ? new Color(realce.r * 0.2f, realce.g * 0.16f, realce.b * 0.12f, 0.99f * fade) : new Color(0.07f, 0.065f, 0.065f, 0.99f * fade), 8f * esc);
        GUIStyle st = new GUIStyle { font = fTexto, fontSize = Mathf.RoundToInt(36f * esc), alignment = TextAnchor.MiddleCenter };
        Color tc = marcado ? (perigo ? new Color(1f, 0.84f, 0.78f, fade) : new Color(1f, 0.95f, 0.82f, fade)) : new Color(0.84f, 0.8f, 0.72f, fade);
        GUI.Label(r, texto, Colorir(st, tc));
    }

    internal static GUIStyle Colorir(GUIStyle s, Color c) { GUIStyle n = new GUIStyle(s); n.normal.textColor = c; return n; }

    internal static void Cor(Rect r, Color c, float raio = 0f)
    {
        Color a = GUI.color; GUI.color = c;
        if (raio > 0f) GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(raio, raio, raio, raio));
        else GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = a;
    }

    // texto com espaço extra entre as letras (centralizado na área, ou colado à esquerda dela)
    internal static void Letras(Rect area, string txt, GUIStyle st, float espaco, Color cor, bool esquerda = false)
    {
        GUIStyle s = Colorir(st, cor); s.alignment = TextAnchor.UpperLeft;
        float[] w = new float[txt.Length]; float total = 0f;
        for (int i = 0; i < txt.Length; i++) { w[i] = txt[i] == ' ' ? st.fontSize * 0.4f : s.CalcSize(new GUIContent(txt[i].ToString())).x; total += w[i] + espaco; }
        total -= espaco; float x = esquerda ? area.x : area.center.x - total * 0.5f;
        for (int i = 0; i < txt.Length; i++) { if (txt[i] != ' ') GUI.Label(new Rect(x, area.y, w[i] + 10f, area.height), txt[i].ToString(), s); x += w[i] + espaco; }
    }
}
