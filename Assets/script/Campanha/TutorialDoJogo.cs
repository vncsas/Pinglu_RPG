using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TUTORIAL ("COMO JOGAR"): aparece depois da chegada em Valverde e também no menu de pausa.
// Layout inspirado no menu de feitiços de Hogwarts Legacy: coluna de ícones à esquerda (agrupados), nome e descrição curtos à direita e,
// embaixo, uma PRÉVIA ANIMADA do personagem usando a habilidade (num quadro de papel levemente torto), com a tecla para usar.
// Fundo = o próprio jogo congelado e desfocado (e a câmera do mundo é desligada enquanto o tutorial está aberto: roda liso).
// Barra de comandos embaixo (trocar / continuar / fechar). Setas ou A D trocam de página, Enter avança, Esc fecha, o mouse também funciona.
// A prévia animada fica em TutorialDoJogo_Mostruario.cs.
public partial class TutorialDoJogo : MonoBehaviour
{
    public static bool Aberto { get; private set; }
    public static FichaPersonagem FichaAtual;        // o personagem que o jogador escolheu (o menu de pausa abre o tutorial com ele)

    FichaPersonagem ficha; HabilidadesDoPersonagem hab; Action fim;
    int pag, pagAnt; float abriu, trocouEm; bool menu0, fechado, pronto; Font fCinzel, fFell;
    const int NB = 3;                                  // páginas básicas: 0 Andar, 1 Combate, 2 Ações; depois as 3 habilidades
    int Paginas { get { return hab != null ? NB + 3 : NB; } }
    readonly Rect[] tiles = new Rect[6]; readonly float[] realce = new float[6];
    TelaDeConfiguracoes config = new TelaDeConfiguracoes(); bool configAberta; Rect botaoConfig;
    Vector2 tokPos = Vector2.zero, tokFace = Vector2.down; bool demoCorrendo; Vector2 demoDir;        // boneco de treino da página "Andar"
    RenderTexture fundo; readonly List<Camera> camsDesligadas = new List<Camera>();
    Texture2D gradH, gradV;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { Aberto = false; }

    public static void Abrir(FichaPersonagem ficha, Action aoFechar)
    {
        if (Aberto) { if (aoFechar != null) aoFechar(); return; }
        new GameObject("Tutorial do jogo").AddComponent<TutorialDoJogo>().Iniciar(ficha, aoFechar);
    }

    void Iniciar(FichaPersonagem f, Action aoFechar)
    {
        Aberto = true; ficha = f; fim = aoFechar; abriu = Time.unscaledTime;
        hab = f != null && f.prefabJogador != null ? f.prefabJogador.GetComponent<HabilidadesDoPersonagem>() : null;
        menu0 = SessaoDeJogo.MenuAberto; SessaoDeJogo.MenuAberto = true;
        fCinzel = Resources.Load<Font>("Fontes/CinzelBold"); fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
        gradH = Gradiente(true); gradV = Gradiente(false);
        StartCoroutine(Abertura());
    }

    // tira uma foto do jogo, desfoca e desliga as câmeras do mundo (a tela só desenha o tutorial: sem queda de FPS)
    IEnumerator Abertura()
    {
        yield return new WaitForEndOfFrame();
        try
        {
            Texture2D foto = ScreenCapture.CaptureScreenshotAsTexture();
            RenderTexture a = RenderTexture.GetTemporary(256, 144, 0), b = RenderTexture.GetTemporary(64, 36, 0);
            foto.filterMode = FilterMode.Bilinear; Graphics.Blit(foto, a); a.filterMode = FilterMode.Bilinear; Graphics.Blit(a, b);
            fundo = new RenderTexture(64, 36, 0) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; Graphics.Blit(b, fundo);
            RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b); Destroy(foto);
        }
        catch (Exception ex) { Debug.LogWarning("[Tutorial] sem foto de fundo: " + ex.Message); fundo = null; }
        if (fundo != null) foreach (Camera c in Camera.allCameras) if (c != null && c.targetTexture == null && c.enabled) { c.enabled = false; camsDesligadas.Add(c); }
        MostruarioIniciar();
        IrParaPagina(0);
        pronto = true; abriu = Time.unscaledTime;
    }

    static Texture2D Gradiente(bool horizontal)
    {
        Texture2D t = new Texture2D(horizontal ? 256 : 1, horizontal ? 1 : 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int i = 0; i < 256; i++) t.SetPixel(horizontal ? i : 0, horizontal ? 0 : i, new Color(1f, 1f, 1f, i / 255f));
        t.Apply(); return t;
    }

    void IrParaPagina(int p)
    {
        p = Mathf.Clamp(p, 0, Paginas - 1); if (p == pag && pronto) return;
        pagAnt = pag; pag = p; trocouEm = Time.unscaledTime; MostruarioPagina(pag);
    }

    void Fechar()
    {
        if (fechado) return;
        Encerrar();
        if (fim != null) { Action a = fim; fim = null; a(); }
        Destroy(gameObject);
    }

    void Encerrar()
    {
        if (fechado) return;
        fechado = true; Aberto = false; MostruarioEncerrar();
        foreach (Camera c in camsDesligadas) if (c != null) c.enabled = true;
        camsDesligadas.Clear();
        if (fundo != null) { fundo.Release(); Destroy(fundo); fundo = null; }
        SessaoDeJogo.MenuAberto = menu0;
    }

    void OnDestroy() { Encerrar(); }

    void Update()
    {
        if (fechado) return;
        SessaoDeJogo.MenuAberto = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (!pronto) return;
        if (configAberta) { if (config.Atualizar() || config.ConsumirVoltar()) { config.Fechar(); configAberta = false; } return; }
        MostruarioAtualizar();
        for (int i = 0; i < 6; i++) realce[i] = Mathf.MoveTowards(realce[i], i == pag ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        AtualizarDemos();
        if (Time.unscaledTime - abriu < 0.45f) return;
        Vector2Int nav = Comandos.NavDirecao();
        bool dir = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.DownArrow) || nav.x > 0 || nav.y < 0;
        bool esq = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.UpArrow) || nav.x < 0 || nav.y > 0;
        if (dir && pag < Paginas - 1) IrParaPagina(pag + 1);
        else if (esq && pag > 0) IrParaPagina(pag - 1);
        bool ok = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Comandos.NavConfirmar();
        if (ok) { if (pag < Paginas - 1) IrParaPagina(pag + 1); else Fechar(); }
        if (Input.GetKeyDown(KeyCode.C)) AbrirConfig();
        if (Input.GetKeyDown(KeyCode.Escape) || Comandos.NavVoltar() || Comandos.NavPausa()) Fechar();
    }

    void AbrirConfig() { configAberta = true; config.Abrir(); }

    // ------------------------------------------------------------------ ferramentas de desenho
    readonly Dictionary<int, GUIStyle> estilos = new Dictionary<int, GUIStyle>();
    GUIStyle Est(Font f, float tam, TextAnchor al, bool quebra = false, FontStyle fs = FontStyle.Normal)
    {
        int chave = (f == fCinzel ? 1 : 2) | (Mathf.RoundToInt(tam) << 2) | ((int)al << 14) | ((quebra ? 1 : 0) << 20) | ((int)fs << 21);
        GUIStyle s; if (!estilos.TryGetValue(chave, out s)) { s = new GUIStyle { font = f, fontSize = Mathf.RoundToInt(tam), alignment = al, wordWrap = quebra, fontStyle = fs }; estilos[chave] = s; }
        return s;
    }
    void Texto(Rect r, string t, Font f, float tam, TextAnchor al, Color cor, bool quebra = false, FontStyle fs = FontStyle.Normal, bool sombra = true)
    {
        GUIStyle s = Est(f, tam, al, quebra, fs);
        if (sombra) { s.normal.textColor = new Color(0f, 0f, 0f, 0.85f * cor.a); GUI.Label(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), t, s); }
        s.normal.textColor = cor; GUI.Label(r, t, s);
    }
    void R(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
    void Disco(float cx, float cy, float rad, Color c) { GUI.color = c; GUI.DrawTexture(new Rect(cx - rad, cy - rad, rad * 2f, rad * 2f), IconesDeHabilidade.Disco); GUI.color = Color.white; }
    // retângulo de cantos arredondados de verdade (sem sobreposição: a transparência fica certa)
    void RR(Rect r, float rad, Color c) { RRc(r, new Vector4(rad, rad, rad, rad), c); }
    void RRc(Rect r, Vector4 rad, Color c)
    {
        float m = Mathf.Min(r.width, r.height) * 0.5f; rad = new Vector4(Mathf.Min(rad.x, m), Mathf.Min(rad.y, m), Mathf.Min(rad.z, m), Mathf.Min(rad.w, m));
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, rad);       // ordem dos cantos: cima-esq, cima-dir, baixo-dir, baixo-esq
    }
    void Brilho(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, IconesDeHabilidade.Brilho); GUI.color = Color.white; }
    // linha com losango no meio (separador de título)
    void Ornamento(float x, float y, float w, float e, Color c)
    {
        R(new Rect(x, y, w * 0.46f, 2f * e), new Color(c.r, c.g, c.b, c.a * 0.7f)); R(new Rect(x + w * 0.54f, y, w * 0.46f, 2f * e), new Color(c.r, c.g, c.b, c.a * 0.7f));
        Matrix4x4 m = GUI.matrix; GUIUtility.RotateAroundPivot(45f, new Vector2(x + w * 0.5f, y + e)); R(new Rect(x + w * 0.5f - 7f * e, y + e - 7f * e, 14f * e, 14f * e), c); GUI.matrix = m;
    }

    // tecla de teclado (ou botão): corpo arredondado com bisel; acende em dourado quando está sendo apertada
    void Tecla(Rect r, string txt, bool ativa, float e, Color ouro, float tam = 0f)
    {
        float rad = 12f * e, afunda = ativa ? 4f * e : 0f;
        RR(new Rect(r.x, r.y + 8f * e, r.width, r.height - 4f * e), rad, new Color(0f, 0f, 0f, 0.55f));
        Rect c = new Rect(r.x, r.y + afunda, r.width, r.height - 6f * e);
        RR(c, rad, ativa ? new Color(0.7f, 0.55f, 0.2f) : new Color(0.1f, 0.095f, 0.11f));
        RR(new Rect(c.x + 3f * e, c.y + 3f * e, c.width - 6f * e, c.height - 6f * e), rad - 2f * e, ativa ? ouro : new Color(0.26f, 0.25f, 0.28f));
        RR(new Rect(c.x + 6f * e, c.y + 6f * e, c.width - 12f * e, (c.height - 12f * e) * 0.5f), rad - 4f * e, new Color(1f, 1f, 1f, ativa ? 0.28f : 0.1f));
        Texto(c, txt, fCinzel, tam > 0f ? tam : Mathf.Min(40f * e, c.height * 0.52f), TextAnchor.MiddleCenter, ativa ? new Color(0.08f, 0.06f, 0.03f) : new Color(0.97f, 0.93f, 0.84f), false, FontStyle.Normal, !ativa);
    }
    float LarguraTecla(string t, float e, float min, float tam) { return Mathf.Max(min, Est(fCinzel, tam, TextAnchor.MiddleCenter).CalcSize(new GUIContent(t)).x + 38f * e); }

}
