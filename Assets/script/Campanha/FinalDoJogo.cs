using System;
using UnityEngine;

// FINAL DO JOGO: toca quando a história chega ao fim (depois que o Caio entrega a recompensa).
//   1) EPÍLOGO animado: a madrugada vira manhã sobre Valverde, com narração letra por letra;
//   2) TÍTULO "Pinglu RPG";
//   3) CRÉDITOS subindo (edite a lista "creditos" logo abaixo);
//   4) "Fim - Obrigado por jogar": Enter devolve o jogo (dá para continuar explorando a ilha).
// Enter / Espaço / clique avança a fala. Esc pula direto para a tela final.
// F9 (só no editor): toca o final na hora para testar.
public class FinalDoJogo : MonoBehaviour
{
    public static bool Ativo { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Iniciar()
    {
        Ativo = false;
        Campanha.AoMudar -= AoMudarEtapa; Campanha.AoMudar += AoMudarEtapa;
    }
    static void AoMudarEtapa(Etapa e) { if (e == Etapa.Fim) Tocar(); }

    public static void Tocar()
    {
        if (Ativo) return;
        new GameObject("Final do jogo").AddComponent<FinalDoJogo>();
    }

    // ------------------------------------------------------------------ roteiro
    static readonly string[] narracao =
    {
        "Drakthar caiu. O poder dos cristais, já sem dono, se desfez no vento para nunca mais voltar.",
        "Valverde acordou numa manhã sem cinzas, sem mortos e sem medo.",
        "E no centro da vila, uma placa nova brilhava: CABARÉ DOS AVENTUREIROS.",
        "Os heróis, enfim, tinham um tostão no bolso... e um balcão inteiro para gastá-lo.",
    };

    // linha que começa com "#" é um título dourado; as outras são nomes
    static readonly string[] creditos =
    {
        "#PINGLU RPG", "A lenda de Valverde", "",
        "#Criação, programação e cinemáticas", "Equipe Pinglu", "",
        "#Roteiro", "Equipe Pinglu", "",
        "#Cenários, personagens e efeitos", "Equipe Pinglu", "",
        "#Elenco", "Caio, o Ancião", "Malakar, o Ladrão", "O Guardião das Lápides", "O Cavaleiro Caído", "A Fera do Pântano", "Drakthar, o Devastador", "",
        "#Testes", "Todos os aventureiros que sobreviveram", "",
        "#Agradecimento especial", "A você, que chegou até o fim.", "",
    };

    enum Etapas { Epilogo, Titulo, Creditos, Fim }
    Etapas etapa = Etapas.Epilogo; float inicioEtapa, inicioCena, inicioTudo; int cena; bool saindo; float fimSaida; bool menuAntes;
    const float VEL = 24f;
    GUIStyle estNarra, estTitulo, estSub, estCred, estCredT, estDica, estDicaC; Font fCinzel, fFell;
    Material mat, matAdd; bool glOk, modoAdd; float W, H;

    void Awake()
    {
        Ativo = true; inicioTudo = inicioEtapa = inicioCena = Time.unscaledTime;
        menuAntes = SessaoDeJogo.MenuAberto;
        fCinzel = Resources.Load<Font>("Fontes/CinzelBold"); fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
    }

    void Update()
    {
        SessaoDeJogo.MenuAberto = true;
        if (saindo) { if (Time.unscaledTime - fimSaida > 0.8f) { SessaoDeJogo.MenuAberto = menuAntes; Ativo = false; Destroy(gameObject); } return; }
        bool ok = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Comandos.NavConfirmar();
        if (Input.GetKeyDown(KeyCode.Escape) && etapa != Etapas.Fim) { Ir(Etapas.Fim); return; }
        float t = Time.unscaledTime - inicioCena;
        switch (etapa)
        {
            case Etapas.Epilogo:
            {
                string tx = narracao[cena]; bool digitado = t * VEL >= tx.Length + 4f;
                if (ok && t > 0.8f) { if (!digitado) inicioCena = Time.unscaledTime - (tx.Length + 4f) / VEL - 0.01f; else ProximaCena(); }
                else if (t > tx.Length / VEL + 5.5f) ProximaCena();
                break;
            }
            case Etapas.Titulo: if (Time.unscaledTime - inicioEtapa > 5.5f || (ok && Time.unscaledTime - inicioEtapa > 1.5f)) Ir(Etapas.Creditos); break;
            case Etapas.Creditos: if (Time.unscaledTime - inicioEtapa > TempoCreditos()) Ir(Etapas.Fim); break;
            case Etapas.Fim: if (ok && Time.unscaledTime - inicioEtapa > 1.5f) { saindo = true; fimSaida = Time.unscaledTime; } break;
        }
    }
    void ProximaCena() { cena++; inicioCena = Time.unscaledTime; if (cena >= narracao.Length) Ir(Etapas.Titulo); }
    void Ir(Etapas e) { etapa = e; inicioEtapa = Time.unscaledTime; }
    float TempoCreditos() { return (creditos.Length * 0.075f * 1080f + 1080f) / (1080f * 0.055f) ; }

    // ------------------------------------------------------------------ desenho
    static Color C(float r, float g, float b, float a = 1f) { return new Color(r, g, b, a); }
    static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    static float Rd(int i, int s) { float x = Mathf.Sin(i * 12.9898f + s * 78.233f) * 43758.5453f; return x - Mathf.Floor(x); }
    static void V(float x, float y) { GL.Vertex3(x, y, 0f); }

    bool PrepararGL()
    {
        if (mat == null)
        {
            Shader sh = Shader.Find("Hidden/Internal-Colored"); if (sh == null) sh = Shader.Find("UI/Default"); if (sh == null) sh = Shader.Find("Sprites/Default"); if (sh == null) return false;
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave }; matAdd = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            foreach (Material m in new[] { mat, matAdd }) { m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); m.SetInt("_ZWrite", 0); m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always); }
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); matAdd.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }
        modoAdd = false; mat.SetPass(0); GL.PushMatrix(); GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f); return true;
    }
    void Modo(bool add) { if (add == modoAdd) return; modoAdd = add; (add ? matAdd : mat).SetPass(0); }
    void Quad(float x0, float y0, float x1, float y1, Color cTopo, Color cBase)
    {
        GL.Begin(GL.TRIANGLES);
        GL.Color(cTopo); V(x0, y0); GL.Color(cTopo); V(x1, y0); GL.Color(cBase); V(x1, y1);
        GL.Color(cTopo); V(x0, y0); GL.Color(cBase); V(x1, y1); GL.Color(cBase); V(x0, y1); GL.End();
    }
    void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color c) { GL.Begin(GL.TRIANGLES); GL.Color(c); V(ax, ay); V(bx, by); V(cx, cy); GL.End(); }
    void Disco(float x, float y, float r, Color centro, Color borda, int seg = 28)
    {
        GL.Begin(GL.TRIANGLES);
        for (int i = 0; i < seg; i++)
        {
            float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
            GL.Color(centro); V(x, y); GL.Color(borda); V(x + Mathf.Cos(a0) * r, y + Mathf.Sin(a0) * r); GL.Color(borda); V(x + Mathf.Cos(a1) * r, y + Mathf.Sin(a1) * r);
        }
        GL.End();
    }
    // colina: faixa preenchida até o chão, altura por ruído suave
    void Colina(float baseY, float amp, float freq, float desloc, int semente, Color c, float xMin = 0f)
    {
        int n = 48; GL.Begin(GL.TRIANGLES); GL.Color(c);
        for (int i = 0; i < n; i++)
        {
            float x0 = W * i / n, x1 = W * (i + 1) / n;
            float y0 = baseY - Altura(x0, amp, freq, desloc, semente), y1 = baseY - Altura(x1, amp, freq, desloc, semente);
            V(x0, y0); V(x1, y1); V(x1, H); V(x0, y0); V(x1, H); V(x0, H);
        }
        GL.End();
    }
    float Altura(float x, float amp, float freq, float desloc, int s) { float u = (x + desloc) / W * freq; return amp * (0.5f + 0.5f * Mathf.Sin(u * 3.1f + s) * 0.6f + 0.5f * Mathf.Sin(u * 7.3f + s * 2f) * 0.4f); }

    void OnGUI()
    {
        GUI.depth = -9500;
        if (Event.current.type != EventType.Repaint) return;
        W = Screen.width; H = Screen.height;
        if (!PrepararGL()) return;
        float agora = Time.unscaledTime - inicioTudo;
        switch (etapa)
        {
            case Etapas.Epilogo: DesenhaEpilogo(agora); break;
            case Etapas.Titulo: DesenhaTitulo(); break;
            case Etapas.Creditos: DesenhaCreditos(); break;
            case Etapas.Fim: DesenhaFim(); break;
        }
        // fade de entrada no começo e de saída no fim
        float fade = 0f; if (etapa == Etapas.Epilogo) fade = 1f - Suave(agora / 1.6f); if (saindo) fade = Suave((Time.unscaledTime - fimSaida) / 0.7f);
        if (fade > 0.001f) { Modo(false); Quad(0f, 0f, W, H, C(0f, 0f, 0f, fade), C(0f, 0f, 0f, fade)); }
        GL.PopMatrix();
        Estilos();
        // textos (IMGUI) por cima do GL
        switch (etapa)
        {
            case Etapas.Epilogo: TextoEpilogo(); break;
            case Etapas.Titulo: TextoTitulo(); break;
            case Etapas.Creditos: TextoCreditos(); break;
            case Etapas.Fim: TextoFim(); break;
        }
    }

    // ---- epílogo: amanhecer sobre Valverde
    void DesenhaEpilogo(float agora)
    {
        // progresso do dia: 0 madrugada -> 1 manhã, ao longo das 4 falas
        float total = 0f; for (int i = 0; i < narracao.Length; i++) total += narracao[i].Length / VEL + 5.5f;
        float feito = 0f; for (int i = 0; i < cena; i++) feito += narracao[i].Length / VEL + 5.5f; feito += Time.unscaledTime - inicioCena;
        float p = Suave(Mathf.Clamp01(feito / Mathf.Max(1f, total * 0.9f)));
        Color topN = C(0.02f, 0.03f, 0.09f), horN = C(0.13f, 0.1f, 0.24f), topA = C(0.22f, 0.26f, 0.5f), horA = C(1f, 0.6f, 0.34f), topD = C(0.32f, 0.58f, 0.92f), horD = C(0.98f, 0.9f, 0.76f);
        float q = p < 0.5f ? p / 0.5f : (p - 0.5f) / 0.5f;
        Color top = p < 0.5f ? Color.Lerp(topN, topA, q) : Color.Lerp(topA, topD, q), hor = p < 0.5f ? Color.Lerp(horN, horA, q) : Color.Lerp(horA, horD, q);
        float horizonte = H * 0.66f;
        Modo(false); Quad(0f, 0f, W, horizonte, top, hor); Quad(0f, horizonte, W, H, hor, hor);
        // estrelas somem com a luz
        float aEst = Mathf.Clamp01(1f - p * 2.4f);
        if (aEst > 0.01f) { Modo(true); GL.Begin(GL.TRIANGLES); for (int i = 0; i < 90; i++) { float x = Rd(i, 1) * W, y = Rd(i, 2) * horizonte * 0.85f, r = 1f + Rd(i, 3) * 1.8f, tw = 0.5f + 0.5f * Mathf.Sin(agoraT() * 2f + i); GL.Color(C(1f, 1f, 1f, aEst * tw)); V(x, y - r); V(x + r, y + r); V(x - r, y + r); } GL.End(); }
        // sol subindo, com brilho
        float sx = W * 0.64f, sy = Mathf.Lerp(horizonte + H * 0.1f, horizonte - H * 0.34f, p), sr = H * 0.055f;
        Modo(true);
        Disco(sx, sy, sr * 9f, C(1f, 0.7f, 0.35f, 0.35f * (0.4f + 0.6f * p)), C(1f, 0.5f, 0.2f, 0f), 36);
        Disco(sx, sy, sr * 3.2f, C(1f, 0.85f, 0.55f, 0.55f), C(1f, 0.6f, 0.3f, 0f), 30);
        Modo(false); Disco(sx, sy, sr, C(1f, 0.97f, 0.85f, 1f), C(1f, 0.93f, 0.7f, 1f), 30);
        // nuvens que ganham cor
        Modo(true);
        for (int i = 0; i < 7; i++)
        {
            float cx = (Rd(i, 5) * W * 1.4f + agoraT() * (6f + i * 2f)) % (W * 1.4f) - W * 0.2f, cy = horizonte * (0.18f + 0.5f * Rd(i, 6)), cw = W * (0.1f + 0.08f * Rd(i, 7));
            Disco(cx, cy, cw, C(Mathf.Lerp(0.3f, 1f, p), Mathf.Lerp(0.3f, 0.75f, p), Mathf.Lerp(0.5f, 0.6f, p), 0.18f), C(0f, 0f, 0f, 0f), 20);
        }
        // montanhas e colinas (mais escuras no escuro)
        Color neb = Color.Lerp(hor, top, 0.35f);
        Modo(false);
        Colina(horizonte + H * 0.02f, H * 0.2f, 1.6f, 0f, 3, Color.Lerp(neb, C(0.1f, 0.12f, 0.22f), 0.35f));
        Colina(horizonte + H * 0.07f, H * 0.12f, 2.3f, 300f, 7, Color.Lerp(neb, C(0.07f, 0.16f, 0.16f), 0.55f));
        Colina(H * 0.8f, H * 0.07f, 1.4f, 80f, 11, Color.Lerp(C(0.01f, 0.03f, 0.03f), C(0.1f, 0.3f, 0.14f), p));
        Vila(p);
        // pássaros ao amanhecer
        if (p > 0.35f)
        {
            Modo(false); float ap = Mathf.Clamp01((p - 0.35f) * 2f);
            for (int i = 0; i < 6; i++)
            {
                float bx = (agoraT() * (40f + 8f * i) + i * 160f) % (W + 200f) - 100f, by = H * (0.2f + 0.22f * Rd(i, 9)) + Mathf.Sin(agoraT() * 2f + i) * 8f, a = 7f + Mathf.Sin(agoraT() * 9f + i * 2f) * 5f, s = H * 0.012f;
                Color cb = C(0.08f, 0.07f, 0.1f, ap);
                Tri(bx - s * 2f, by + a * 0.4f, bx, by, bx, by + 2f, cb); Tri(bx + s * 2f, by + a * 0.4f, bx, by, bx, by + 2f, cb);
            }
        }
        // barras de cinema
        Modo(false); Quad(0f, 0f, W, H * 0.1f, C(0f, 0f, 0f), C(0f, 0f, 0f)); Quad(0f, H * 0.82f, W, H, C(0f, 0f, 0f), C(0f, 0f, 0f));
    }
    float agoraT() { return Time.unscaledTime - inicioTudo; }

    // casas de Valverde, com janelas acesas e o cabaré no meio (a placa acende na terceira fala)
    void Vila(float p)
    {
        float chao = H * 0.79f; float luz = Mathf.Clamp01(1f - p * 0.7f);
        for (int i = 0; i < 9; i++)
        {
            bool cab = i == 4;
            float w = H * (cab ? 0.2f : 0.09f + 0.03f * Rd(i, 12)), h = H * (cab ? 0.15f : 0.07f + 0.03f * Rd(i, 13)); float x = W * (0.12f + 0.095f * i) + (cab ? -H * 0.03f : 0f);
            Color corpo = Color.Lerp(C(0.05f, 0.04f, 0.07f), C(0.55f, 0.42f, 0.32f), p * 0.9f), telhado = cab ? Color.Lerp(C(0.15f, 0.02f, 0.03f), C(0.75f, 0.15f, 0.12f), p) : Color.Lerp(C(0.08f, 0.04f, 0.05f), C(0.5f, 0.24f, 0.18f), p);
            Modo(false);
            Quad(x, chao - h, x + w, chao, corpo, Color.Lerp(corpo, C(0f, 0f, 0f), 0.2f));
            Tri(x - w * 0.08f, chao - h, x + w * 0.5f, chao - h - h * 0.55f, x + w * 1.08f, chao - h, telhado);
            // janelas
            int nj = cab ? 3 : 1; for (int j = 0; j < nj; j++)
            {
                float jx = x + w * (cab ? 0.14f + 0.3f * j : 0.35f), jw = w * (cab ? 0.2f : 0.28f), jy = chao - h * 0.7f, jh = h * 0.35f;
                Modo(false); Quad(jx, jy, jx + jw, jy + jh, C(1f, 0.82f, 0.4f, 0.4f + 0.6f * luz), C(1f, 0.6f, 0.25f, 0.4f + 0.6f * luz));
                Modo(true); Disco(jx + jw * 0.5f, jy + jh * 0.5f, jw * 1.4f, C(1f, 0.7f, 0.3f, 0.22f * luz), C(1f, 0.6f, 0.2f, 0f), 14);
            }
            if (cab)
            {
                // a placa do cabaré: acende na terceira fala
                float acende = cena >= 2 ? Suave((cena == 2 ? Time.unscaledTime - inicioCena : 99f) / 1.5f) : 0f; float pisca = 0.8f + 0.2f * Mathf.Sin(agoraT() * 6f);
                Modo(false); Quad(x + w * 0.2f, chao - h - h * 0.35f, x + w * 0.8f, chao - h - h * 0.12f, C(0.12f, 0.05f, 0.06f), C(0.12f, 0.05f, 0.06f));
                Modo(true);
                Quad(x + w * 0.24f, chao - h - h * 0.31f, x + w * 0.76f, chao - h - h * 0.16f, C(1f, 0.35f, 0.4f, 0.9f * acende * pisca), C(1f, 0.6f, 0.25f, 0.9f * acende * pisca));
                Disco(x + w * 0.5f, chao - h - h * 0.24f, w * 1.1f, C(1f, 0.35f, 0.35f, 0.3f * acende), C(1f, 0.3f, 0.3f, 0f), 24);
                Disco(x + w * 0.5f, chao - h * 0.2f, w * 0.9f, C(1f, 0.75f, 0.35f, 0.28f * acende), C(1f, 0.6f, 0.2f, 0f), 24);
            }
        }
        Modo(false); Quad(0f, chao, W, H, Color.Lerp(C(0.02f, 0.04f, 0.03f), C(0.22f, 0.4f, 0.18f), p), Color.Lerp(C(0.01f, 0.02f, 0.02f), C(0.12f, 0.25f, 0.1f), p));
    }

    void DesenhaTitulo()
    {
        Modo(false); Quad(0f, 0f, W, H, C(0.02f, 0.01f, 0.03f), C(0.08f, 0.03f, 0.05f));
        float t = Time.unscaledTime - inicioEtapa; float a = Suave(t / 1.5f) * (1f - Suave((t - 4.6f) / 0.9f));
        Modo(true); Disco(W * 0.5f, H * 0.46f, H * 0.7f, C(1f, 0.5f, 0.15f, 0.2f * a), C(1f, 0.3f, 0.05f, 0f), 36);
        Brasas(a);
    }
    void Brasas(float a)
    {
        Modo(true); GL.Begin(GL.TRIANGLES);
        for (int i = 0; i < 70; i++)
        {
            float v = 25f + Rd(i, 21) * 55f, y = H - ((agoraT() * v + Rd(i, 22) * H * 2f) % (H * 1.1f)), x = Rd(i, 23) * W + Mathf.Sin(agoraT() * 0.8f + i) * 18f, r = 1.2f + Rd(i, 24) * 2.2f, al = Mathf.Clamp01(1f - y / H) * 0.9f * a;
            GL.Color(C(1f, 0.55f + 0.3f * Rd(i, 25), 0.2f, al)); V(x, y - r); V(x + r, y + r); V(x - r, y + r);
        }
        GL.End();
    }
    void DesenhaCreditos() { Modo(false); Quad(0f, 0f, W, H, C(0.02f, 0.01f, 0.03f), C(0.06f, 0.03f, 0.05f)); Brasas(0.8f); }
    void DesenhaFim()
    {
        Modo(false); Quad(0f, 0f, W, H, C(0.02f, 0.01f, 0.03f), C(0.08f, 0.04f, 0.06f));
        float a = Suave((Time.unscaledTime - inicioEtapa) / 1.5f); Modo(true); Disco(W * 0.5f, H * 0.5f, H * 0.6f, C(1f, 0.6f, 0.25f, 0.14f * a), C(1f, 0.4f, 0.1f, 0f), 36); Brasas(a);
    }

    // ---- textos
    void Estilos()
    {
        if (estNarra != null) return;
        int u = Mathf.Max(14, Mathf.RoundToInt(H / 30f));
        estNarra = new GUIStyle(GUI.skin.label) { font = fFell, fontSize = Mathf.RoundToInt(u * 1.25f), alignment = TextAnchor.UpperCenter, wordWrap = true, richText = false };
        estTitulo = new GUIStyle(GUI.skin.label) { font = fCinzel, fontSize = Mathf.RoundToInt(u * 4.2f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        estSub = new GUIStyle(GUI.skin.label) { font = fFell, fontSize = Mathf.RoundToInt(u * 1.5f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        estCred = new GUIStyle(GUI.skin.label) { font = fFell, fontSize = Mathf.RoundToInt(u * 1.4f), alignment = TextAnchor.MiddleCenter };
        estCredT = new GUIStyle(GUI.skin.label) { font = fCinzel, fontSize = Mathf.RoundToInt(u * 1.25f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        estDica = new GUIStyle(GUI.skin.label) { font = fFell, fontSize = Mathf.RoundToInt(u * 0.9f), alignment = TextAnchor.MiddleRight };
        estDicaC = new GUIStyle(estDica) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(u * 1.1f) };
    }
    static void Sombra(Rect r, string t, GUIStyle s, Color c, float a)
    {
        Color antes = GUI.color; GUI.color = new Color(0f, 0f, 0f, a * 0.85f); GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), t, s);
        GUI.color = new Color(c.r, c.g, c.b, c.a * a); GUI.Label(r, t, s); GUI.color = antes;
    }
    void TextoEpilogo()
    {
        string tx = narracao[Mathf.Min(cena, narracao.Length - 1)]; float t = Time.unscaledTime - inicioCena;
        int n = Mathf.Clamp(Mathf.FloorToInt(t * VEL), 0, tx.Length); float a = Suave(t / 0.5f) * (1f - Suave((t - (tx.Length / VEL + 4.6f)) / 0.8f));
        Rect r = new Rect(W * 0.15f, H * 0.845f, W * 0.7f, H * 0.14f);
        Sombra(r, tx.Substring(0, n), estNarra, new Color(1f, 0.96f, 0.88f), Mathf.Clamp01(a));
        if (n >= tx.Length) Sombra(new Rect(W * 0.6f, H * 0.945f, W * 0.37f, H * 0.04f), "Enter  ▶", estDica, new Color(1f, 0.85f, 0.5f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
    }
    void TextoTitulo()
    {
        float t = Time.unscaledTime - inicioEtapa; float a = Suave(t / 1.5f) * (1f - Suave((t - 4.6f) / 0.9f));
        Sombra(new Rect(0f, H * 0.36f, W, H * 0.16f), "PINGLU RPG", estTitulo, new Color(1f, 0.82f, 0.45f), a);
        Sombra(new Rect(0f, H * 0.52f, W, H * 0.08f), "A lenda de Valverde", estSub, new Color(0.95f, 0.85f, 0.75f), Suave((t - 1f) / 1.2f) * (1f - Suave((t - 4.6f) / 0.9f)));
    }
    void TextoCreditos()
    {
        float t = Time.unscaledTime - inicioEtapa; float passo = H * 0.075f, vel = H * 0.055f, y0 = H + 20f - t * vel;
        for (int i = 0; i < creditos.Length; i++)
        {
            string l = creditos[i]; if (l.Length == 0) continue; float y = y0 + i * passo;
            if (y < -passo || y > H + passo) continue;
            float a = Mathf.Clamp01(Mathf.Min(y / (H * 0.15f), (H - y) / (H * 0.15f)));
            bool tit = l.StartsWith("#"); Sombra(new Rect(0f, y, W, passo), tit ? l.Substring(1).ToUpper() : l, tit ? estCredT : estCred, tit ? new Color(1f, 0.8f, 0.42f) : new Color(0.96f, 0.93f, 0.9f), a);
        }
    }
    void TextoFim()
    {
        float t = Time.unscaledTime - inicioEtapa;
        Sombra(new Rect(0f, H * 0.33f, W, H * 0.16f), "FIM", estTitulo, new Color(1f, 0.82f, 0.45f), Suave(t / 1.2f));
        Sombra(new Rect(0f, H * 0.5f, W, H * 0.07f), "Obrigado por jogar!", estSub, new Color(0.96f, 0.9f, 0.82f), Suave((t - 0.8f) / 1.2f));
        Sombra(new Rect(0f, H * 0.62f, W, H * 0.05f), "Enter  -  continuar explorando a ilha", estDicaC, new Color(1f, 0.85f, 0.5f), Suave((t - 1.8f) / 1f) * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f)));
    }
}
