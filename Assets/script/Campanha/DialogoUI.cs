using System;
using System.Collections.Generic;
using UnityEngine;

// Caixa de diálogo na parte de baixo da tela: nome, texto que vai aparecendo e "E / clique" para continuar.
// Enquanto está aberta, o jogador fica parado (SessaoDeJogo.MenuAberto).
public class DialogoUI : MonoBehaviour
{
    public static DialogoUI Instancia { get; private set; }
    public static bool Aberto { get { return Instancia != null && Instancia.falas != null; } }
    // quem está falando agora e se o texto ainda está sendo "digitado" (o Caio real mexe a boca nesse momento)
    public static string FalanteAtual { get { return Instancia != null && Instancia.falas != null && Instancia.indice >= 0 && Instancia.indice < Instancia.falas.Count ? Instancia.NomeDe(Instancia.falas[Instancia.indice]) : null; } }
    public static bool Digitando { get { return Instancia != null && Instancia.falas != null && Instancia.indice >= 0 && Instancia.indice < Instancia.falas.Count && Time.unscaledTime - Instancia.inicioDaFala < Instancia.falas[Instancia.indice].texto.Length / VELOCIDADE; } }
    public static float UltimoFechamento = -99f;      // quem abre conversas ignora o apertar de tecla logo depois de fechar

    public Font fonteTexto, fonteNome;
    public Texture2D retratoCaio;          // quando existir a foto/arte do Caio, é só colocar aqui

    private IList<Fala> falas; private int indice; private float inicioDaFala, inicioDaFala0; private Action aoFim; private bool menuAntes;
    private GUIStyle estTexto, estNome, estDica, estMedida; private Texture2D pixel;
    const float VELOCIDADE = 48f;      // letras por segundo

    // quebra as falas longas em balões curtos (uma ou duas frases), mantendo quem fala e a emoção
    const int MAX_BALAO = 96;
    private static IList<Fala> Paginar(IList<Fala> lista)
    {
        var res = new List<Fala>();
        foreach (Fala f in lista)
        {
            string t = f.texto; Emocao em = f.emocao;
            if (t.Length <= MAX_BALAO + 14) { res.Add(f); continue; }
            var frases = new List<string>(); int ini = 0;
            for (int i = 0; i < t.Length; i++)
            {
                bool fim = t[i] == '.' || t[i] == '!' || t[i] == '?';
                if (!fim) continue;
                int j = i; while (j + 1 < t.Length && (t[j + 1] == '.' || t[j + 1] == '!' || t[j + 1] == '?' || t[j + 1] == '"')) j++;
                if (j + 1 >= t.Length || t[j + 1] == ' ') { frases.Add(t.Substring(ini, j + 1 - ini).Trim()); ini = j + 1; i = j; }
            }
            if (ini < t.Length) frases.Add(t.Substring(ini).Trim());
            var pagina = new System.Text.StringBuilder();
            Action<string> fecha = txt => { if (txt.Length > 0) res.Add(new Fala(f.quem, txt, null, em)); };
            foreach (string fr0 in frases)
            {
                string fr = fr0;
                while (fr.Length > MAX_BALAO + 14)                          // frase enorme: corta na vírgula/espaço mais perto
                {
                    int corte = fr.LastIndexOf(", ", MAX_BALAO); if (corte < 30) corte = fr.LastIndexOf(' ', MAX_BALAO); if (corte < 30) corte = MAX_BALAO;
                    if (pagina.Length > 0) { fecha(pagina.ToString().Trim()); pagina.Length = 0; }
                    fecha(fr.Substring(0, corte + 1).Trim()); fr = fr.Substring(corte + 1).Trim();
                }
                if (pagina.Length > 0 && pagina.Length + 1 + fr.Length > MAX_BALAO) { fecha(pagina.ToString().Trim()); pagina.Length = 0; }
                if (pagina.Length > 0) pagina.Append(' ');
                pagina.Append(fr);
            }
            fecha(pagina.ToString().Trim());
        }
        return res;
    }

    private void Awake() { Instancia = this; }
    private void OnDestroy() { if (Instancia == this) Instancia = null; }

    public static void Abrir(IList<Fala> lista, Action quandoAcabar)
    {
        if (Instancia == null || lista == null || lista.Count == 0) { if (quandoAcabar != null) quandoAcabar(); return; }
        Instancia.abertoEm = Time.unscaledTime; Instancia.falas = Paginar(lista); Instancia.indice = 0; Instancia.inicioDaFala = Instancia.inicioDaFala0 = Time.unscaledTime; Instancia.ancoraDaFala = -1; Instancia.aoFim = quandoAcabar;
        Instancia.menuAntes = SessaoDeJogo.MenuAberto; SessaoDeJogo.MenuAberto = true;
    }

    // divide o texto em linhas que cabem em "largura" (troca o espaço por \n, então o tamanho do texto não muda)
    private string Quebrar(string txt, float largura, out float maiorLinha, out int linhas)
    {
        var sb = new System.Text.StringBuilder(); string linha = ""; maiorLinha = 0f; linhas = 1;
        foreach (string palavra in txt.Split(' '))
        {
            string teste = linha.Length == 0 ? palavra : linha + " " + palavra;
            float w = estMedida.CalcSize(new GUIContent(teste)).x;
            if (w > largura && linha.Length > 0) { sb.Append(linha).Append('\n'); linha = palavra; linhas++; maiorLinha = Mathf.Max(maiorLinha, estMedida.CalcSize(new GUIContent(linha)).x); }
            else { linha = teste; maiorLinha = Mathf.Max(maiorLinha, w); }
        }
        sb.Append(linha); return sb.ToString();
    }

    private string TextoVisivel()
    {
        string t = falas[indice].texto;
        int n = Mathf.Min(t.Length, Mathf.FloorToInt((Time.unscaledTime - inicioDaFala) * VELOCIDADE));
        return t.Substring(0, n);
    }

    private void Update()
    {
        if (falas == null) return;
        string t = falas[indice].texto;
        bool terminouDeEscrever = Time.unscaledTime - inicioDaFala >= t.Length / VELOCIDADE;
        bool ultima = indice >= falas.Count - 1;
        if (Input.GetKeyDown(KeyCode.Escape)) { Fechar(); return; }           // Esc pula a conversa toda
        // na última fala a conversa se encerra sozinha pouco depois de ler
        if (ultima && terminouDeEscrever && Time.unscaledTime - inicioDaFala > t.Length / VELOCIDADE + 3.5f) { Fechar(); return; }
        bool avancar = Comandos.Apertou(Acao.Interagir) || Comandos.NavConfirmar() || Input.GetMouseButtonDown(0);
        if (!avancar) return;
        if (!terminouDeEscrever) { inicioDaFala = -999f; return; }    // mostra a fala inteira
        if (ultima) { Fechar(); return; }                              // última fala: aperta qualquer botão e sai
        indice++;
        inicioDaFala = inicioDaFala0 = Time.unscaledTime;
    }

    private void Fechar()
    {
        falas = null; SessaoDeJogo.MenuAberto = menuAntes; UltimoFechamento = Time.unscaledTime;
        Action a = aoFim; aoFim = null; if (a != null) a();
    }

    private Texture2D seta; private float abertoEm;

    // "Aventureiro" nas falas = o personagem que o jogador escolheu (nome, rosto e balão dele)
    private static string NomeDoMeuPersonagem()
    {
        foreach (EntradaHumana h in FindObjectsByType<EntradaHumana>(FindObjectsSortMode.None))
        {
            PlayerLocomotion l = h.GetComponent<PlayerLocomotion>();
            if (l == null || !l.enabled) continue;
            string n = h.gameObject.name.ToLowerInvariant();
            if (n.Contains("maga") || n.Contains("morgana")) return "Morgana";
            if (n.Contains("tanque")) return "Granito";
            if (n.Contains("curandeiro")) return "Ossian";
            if (n.Contains("cavaleiro")) return "Cavaleiro Sombrio";
        }
        return Roteiro.VOCE;
    }
    private string NomeDe(Fala f) { return f.quem == Roteiro.VOCE ? NomeDoMeuPersonagem() : f.quem; }

    private void Retangulo(Rect r, Color c, float canto)
    {
        canto = Mathf.Min(canto, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, pixel, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(canto, canto, canto, canto));
    }
    private void Contorno(Rect r, Color c, float esp, float canto)
    {
        canto = Mathf.Min(canto, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, pixel, ScaleMode.StretchToFill, true, 0f, c, new Vector4(esp, esp, esp, esp), new Vector4(canto, canto, canto, canto));
    }

    // ====================================================================================================
    //  Balão de fala no estilo Octopath Traveler: pergaminho com ponta, plaquinha com o nome, ♦ para continuar.
    //  Fica em cima da cabeça de quem fala (ou ao lado / embaixo, se faltar espaço na tela) e o FORMATO muda com a emoção:
    //  surpresa = estrela, raiva = pontas afiadas, alegre = nuvenzinha saltitante, triste = pálido e caído, pensando = nuvem com bolinhas...
    // ====================================================================================================
    static readonly Dictionary<string, Transform> registro = new Dictionary<string, Transform>();
    public static void Registrar(string nome, Transform t) { if (!string.IsNullOrEmpty(nome) && t != null) registro[Normaliza(nome)] = t; }
    static string Normaliza(string s)
    {
        if (s == null) return "";
        s = s.ToLowerInvariant().Replace('ã', 'a').Replace('á', 'a').Replace('â', 'a').Replace('à', 'a').Replace('é', 'e').Replace('ê', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ô', 'o').Replace('õ', 'o').Replace('ú', 'u').Replace('ç', 'c');
        return s.Trim();
    }

    // quem está falando (cache por fala)
    private int ancoraDaFala = -1; private Transform ancora; private Renderer[] ancoraRend;
    private void ResolverAncora(Fala f)
    {
        ancora = null; ancoraRend = null;
        string q = Normaliza(f.quem);
        if (f.quem == Roteiro.VOCE || q == "aventureiro")
        {
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) { ancora = p.transform; break; }
        }
        else
        {
            Transform t; if (registro.TryGetValue(q, out t) && t != null && t.gameObject.activeInHierarchy) ancora = t;
            if (ancora == null && q.Length > 2 && q != "???")
            {
                float melhor = float.MaxValue;
                foreach (Transform tr in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (!tr.gameObject.activeInHierarchy || !Normaliza(tr.name).Contains(q)) continue;
                    if (tr.GetComponentInChildren<Renderer>() == null) continue;
                    float d = tr.position.sqrMagnitude * 0f + (tr.GetComponent<Animator>() != null || tr.GetComponentInChildren<Animator>() != null ? 0f : 1000f) + tr.GetComponentsInParent<Transform>().Length;
                    if (d < melhor) { melhor = d; ancora = tr; }
                }
            }
        }
        if (ancora != null) ancoraRend = System.Array.FindAll(ancora.GetComponentsInChildren<Renderer>(false), r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer));
    }

    // retângulos (em tela) de quem está na cena: o balão não deve cobri-los
    private readonly Dictionary<Transform, Renderer[]> cacheRend = new Dictionary<Transform, Renderer[]>();
    private bool TelaDe(Transform t, out Vector2 cabeca, out Vector2 pe)
    {
        cabeca = pe = Vector2.zero; Camera cam = FxCena.CamOverride != null ? FxCena.CamOverride : Camera.main; if (t == null || cam == null || !t.gameObject.activeInHierarchy) return false;
        Renderer[] rs; if (!cacheRend.TryGetValue(t, out rs)) { rs = System.Array.FindAll(t.GetComponentsInChildren<Renderer>(false), q => !(q is ParticleSystemRenderer) && !(q is TrailRenderer) && !(q is LineRenderer)); cacheRend[t] = rs; }
        Vector3 topo = t.position + Vector3.up * 2f, base0 = t.position;
        if (rs.Length > 0)
        {
            Bounds b = new Bounds(); bool ok = false;
            foreach (Renderer r in rs) { if (r == null) continue; if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds); }
            if (ok) { topo = new Vector3(b.center.x, b.max.y, b.center.z); base0 = new Vector3(b.center.x, b.min.y, b.center.z); }
        }
        Vector3 a = cam.WorldToScreenPoint(topo), c = cam.WorldToScreenPoint(base0);
        if (a.z < 0.3f || c.z < 0.3f) return false;
        cabeca = new Vector2(a.x, Screen.height - a.y); pe = new Vector2(c.x, Screen.height - c.y); return true;
    }
    private List<Rect> Protegidos(Vector2 cabFalante, Vector2 peFalante, float e)
    {
        var l = new List<Rect>();
        Action<Vector2, Vector2> add = (c, p) => { float alt = Mathf.Max(60f * e, p.y - c.y), w = Mathf.Max(70f * e, alt * 0.55f); l.Add(new Rect(c.x - w * 0.5f, c.y - 8f * e, w, alt + 8f * e)); };
        add(cabFalante, peFalante);
        Vector2 a, b;
        foreach (var kv in registro) if (kv.Value != ancora && TelaDe(kv.Value, out a, out b)) add(a, b);
        foreach (PlayerLocomotion pl in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            if (pl.enabled && !pl.Companheiro && pl.transform != ancora && TelaDe(pl.transform, out a, out b)) add(a, b);
        return l;
    }
    private static Rect Sobre(Rect a, Rect b)
    {
        float x0 = Mathf.Max(a.x, b.x), x1 = Mathf.Min(a.xMax, b.xMax), y0 = Mathf.Max(a.y, b.y), y1 = Mathf.Min(a.yMax, b.yMax);
        return x1 > x0 && y1 > y0 ? new Rect(x0, y0, x1 - x0, y1 - y0) : new Rect(0, 0, 0, 0);
    }

    // topo da cabeça e pé do falante em coordenadas de tela (y para baixo). false = fora da câmera
    private bool NaTela(out Vector2 cabeca, out Vector2 pe)
    {
        cabeca = pe = Vector2.zero;
        Camera cam = FxCena.CamOverride != null ? FxCena.CamOverride : Camera.main; if (ancora == null || cam == null) return false;
        Vector3 topo, base0;
        if (ancoraRend != null && ancoraRend.Length > 0)
        {
            Bounds b = new Bounds(); bool ok = false;
            foreach (Renderer r in ancoraRend) { if (r == null) continue; if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds); }
            if (!ok) { topo = ancora.position + Vector3.up * 2f; base0 = ancora.position; }
            else { topo = new Vector3(b.center.x, b.max.y, b.center.z); base0 = new Vector3(b.center.x, b.min.y, b.center.z); }
        }
        else { topo = ancora.position + Vector3.up * 2f; base0 = ancora.position; }
        Vector3 a = cam.WorldToScreenPoint(topo), c = cam.WorldToScreenPoint(base0);
        if (a.z < 0.3f || c.z < 0.3f) return false;
        cabeca = new Vector2(a.x, Screen.height - a.y); pe = new Vector2(c.x, Screen.height - c.y);
        return true;
    }

    // ---------------------------------------------------------------- desenho com GL (polígonos)
    private Material matGL; private bool semShader; private static bool glOk;
    private void PrepararGL()
    {
        if (matGL == null)
        {
            Shader sh = Shader.Find("Hidden/Internal-Colored"); if (sh == null) sh = Shader.Find("UI/Default"); if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) { semShader = true; return; }
            matGL = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            matGL.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); matGL.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            matGL.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); matGL.SetInt("_ZWrite", 0);
        }
        glOk = matGL != null; if (!glOk) return;
        matGL.SetPass(0);
        GL.PushMatrix(); GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
    }
    private void FimGL() { if (matGL != null) GL.PopMatrix(); }

    private static void Preencher(List<Vector2> pts, Color c)
    {
        if (!glOk) return;
        Vector2 cen = Vector2.zero; foreach (Vector2 p in pts) cen += p; cen /= pts.Count;
        GL.Begin(GL.TRIANGLES); GL.Color(c);
        for (int i = 0; i < pts.Count; i++) { Vector2 a = pts[i], b = pts[(i + 1) % pts.Count]; GL.Vertex3(cen.x, cen.y, 0f); GL.Vertex3(a.x, a.y, 0f); GL.Vertex3(b.x, b.y, 0f); }
        GL.End();
    }
    private static void Linha(Vector2 a, Vector2 b, float esp, Color c)
    {
        if (!glOk) return;
        Vector2 d = (b - a); if (d.sqrMagnitude < 0.0001f) return; d.Normalize(); Vector2 n = new Vector2(-d.y, d.x) * esp * 0.5f;
        GL.Begin(GL.TRIANGLES); GL.Color(c);
        GL.Vertex3(a.x - n.x, a.y - n.y, 0f); GL.Vertex3(a.x + n.x, a.y + n.y, 0f); GL.Vertex3(b.x + n.x, b.y + n.y, 0f);
        GL.Vertex3(a.x - n.x, a.y - n.y, 0f); GL.Vertex3(b.x + n.x, b.y + n.y, 0f); GL.Vertex3(b.x - n.x, b.y - n.y, 0f);
        GL.End();
    }
    private static void Contornar(List<Vector2> pts, float esp, Color c, bool fechar = true)
    {
        int n = fechar ? pts.Count : pts.Count - 1;
        for (int i = 0; i < n; i++) Linha(pts[i], pts[(i + 1) % pts.Count], esp, c);
    }
    private static void Disco(Vector2 c, float r, Color cor)
    {
        var l = new List<Vector2>(); for (int i = 0; i < 14; i++) { float a = i / 14f * Mathf.PI * 2f; l.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
        Preencher(l, cor);
    }

    // contorno do balão: retângulo com cantos cortados e pontas nas laterais; a emoção deforma a borda
    private static List<Vector2> Perimetro(Rect r, Emocao em, float esc, float t)
    {
        float c = (em == Emocao.Triste ? 26f : 16f) * esc, tip = (em == Emocao.Triste ? 4f : 12f) * esc, cy = r.center.y;
        Vector2[] baseV = {
            new Vector2(r.x + c, r.y), new Vector2(r.xMax - c, r.y), new Vector2(r.xMax, r.y + c), new Vector2(r.xMax + tip, cy), new Vector2(r.xMax, r.yMax - c),
            new Vector2(r.xMax - c, r.yMax), new Vector2(r.x + c, r.yMax), new Vector2(r.x, r.yMax - c), new Vector2(r.x - tip, cy), new Vector2(r.x, r.y + c) };
        float passo = 5f * esc; var res = new List<Vector2>(); float s = 0f;
        for (int i = 0; i < baseV.Length; i++)
        {
            Vector2 a = baseV[i], b = baseV[(i + 1) % baseV.Length], d = b - a; float len = d.magnitude; if (len < 0.01f) continue; d /= len;
            Vector2 n = new Vector2(d.y, -d.x);                       // para fora (contorno no sentido horário, y para baixo)
            int k = Mathf.Max(1, Mathf.CeilToInt(len / passo));
            for (int j = 0; j < k; j++)
            {
                float u = j / (float)k; Vector2 p = a + d * (len * u); float ss = s + len * u, off = 0f;
                switch (em)
                {
                    case Emocao.Surpresa: off = 9f * esc * Tri(ss / (27f * esc)); break;
                    case Emocao.Raiva: off = 12f * esc * Tri(ss / (17f * esc)); break;
                    case Emocao.Alegre: off = 3.5f * esc * Mathf.Abs(Mathf.Sin(Mathf.PI * ss / (46f * esc))); break;
                    case Emocao.Pensando: off = 6f * esc * Mathf.Abs(Mathf.Sin(Mathf.PI * ss / (60f * esc))); break;
                    case Emocao.Triste: off = -2.5f * esc * Mathf.Abs(Mathf.Sin(Mathf.PI * ss / (80f * esc))); break;
                    case Emocao.Medo: off = 2.5f * esc * Tri(ss / (11f * esc)); break;
                }
                res.Add(p + n * off);
            }
            s += len;
        }
        return res;
    }
    static float Tri(float x) { x = x - Mathf.Floor(x); return x < 0.5f ? x * 2f : (1f - x) * 2f; }

    static Color Pergaminho(Emocao e) { return new Color(0.93f, 0.89f, 0.8f); }

    private void OnGUI()
    {
        if (falas == null) return;
        if (pixel == null) { pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply(); }
        float e = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        Fala f = falas[indice];
        if (ancoraDaFala != indice) { ancoraDaFala = indice; ResolverAncora(f); }
        string quem = NomeDe(f);
        EstiloDeFala est = EstiloDeFala.Para(quem);
        Emocao em = f.emocao;
        float t = Time.unscaledTime;
        float ap = Mathf.Clamp01((t - inicioDaFala0) / 0.2f);                       // entrada do balão desta fala
        float pop = ap < 1f ? Mathf.LerpUnclamped(0.55f, 1f, 1f + 2.2f * Mathf.Pow(ap - 1f, 3f) + 1.2f * Mathf.Pow(ap - 1f, 2f)) : 1f;   // "estoura" e assenta
        if (em == Emocao.Surpresa && ap >= 1f) pop = 1f + 0.03f * Mathf.Sin((t - inicioDaFala0) * 16f) * Mathf.Exp(-(t - inicioDaFala0) * 2f);

        // ---- medidas do texto
        if (estTexto == null)
        {
            estTexto = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = true, font = fonteTexto };
            estNome = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, font = fonteNome != null ? fonteNome : fonteTexto, alignment = TextAnchor.MiddleCenter };
        }
        estTexto.fontSize = Mathf.RoundToInt(29f * e); estNome.fontSize = Mathf.RoundToInt(26f * e);
        // quebra de linha PRÓPRIA (palavra por palavra, medindo cada linha): o texto nunca passa da borda do balão
        if (estMedida == null) estMedida = new GUIStyle(estTexto);
        estMedida.font = estTexto.font; estMedida.fontSize = estTexto.fontSize; estMedida.wordWrap = false; estTexto.wordWrap = false;
        Font fnt = estTexto.font != null ? estTexto.font : GUI.skin.font; if (fnt != null) fnt.RequestCharactersInTexture(f.texto, estTexto.fontSize, FontStyle.Normal);
        float maxW = Mathf.Min(W - 160f * e, 600f * e), padX = 32f * e, padY = 22f * e;
        int linhas; float maiorLinha; string completo = Quebrar(f.texto, maxW * 0.94f, out maiorLinha, out linhas);
        int nVis = TextoVisivel().Length; string visivel = completo.Substring(0, Mathf.Min(completo.Length, nVis));
        float textoW = Mathf.Clamp(maiorLinha + 10f * e, 260f * e, maxW);
        float linhaH = estMedida.CalcSize(new GUIContent("Ág")).y;
        float textoH = linhaH * linhas;
        float bw = textoW + padX * 2f + 22f * e, bh = textoH + padY * 2f;

        // ---- onde fica: testa várias posições e escolhe a melhor (sem cobrir quem está na cena, sempre dentro da tela)
        Vector2 cab, pe; bool temAncora = NaTela(out cab, out pe);
        Rect r; Vector2 alvo = Vector2.zero; int lado = 0;          // 0 sem ponta, 1 ponta embaixo (balão em cima), 2 ponta à esquerda (balão à direita), 3 ponta à direita, 4 ponta em cima (balão embaixo)
        float margem = 36f * e, topoMin = 60f * e;
        if (temAncora)
        {
            var proibidos = Protegidos(cab, pe, e);
            float folga = 40f * e, meio = cab.y + (pe.y - cab.y) * 0.3f;
            var cand = new List<KeyValuePair<Rect, KeyValuePair<int, float>>>();     // retângulo, (lado, custo base)
            Action<float, float, int, float> add = (x, y, ld, custo) => cand.Add(new KeyValuePair<Rect, KeyValuePair<int, float>>(new Rect(x, y, bw, bh), new KeyValuePair<int, float>(ld, custo)));
            float ya = cab.y - folga - bh;
            add(cab.x - bw * 0.5f, ya, 1, 0f);
            add(cab.x - bw * 0.25f, ya, 1, 2500f); add(cab.x - bw * 0.75f, ya, 1, 2500f);
            add(cab.x - bw + 70f * e, ya, 1, 5000f); add(cab.x - 70f * e, ya, 1, 5000f);
            add(cab.x + 70f * e, meio - bh * 0.5f, 2, 7000f); add(cab.x - 70f * e - bw, meio - bh * 0.5f, 3, 7000f);
            add(cab.x - bw * 0.5f, pe.y + 34f * e, 4, 11000f);
            float melhor = float.MaxValue; r = new Rect((W - bw) * 0.5f, H - bh - 90f * e, bw, bh); lado = 0;
            foreach (var c in cand)
            {
                Rect q = c.Key; float custo = c.Value.Value;
                float fx = Mathf.Clamp(q.x, margem, W - bw - margem), fy = Mathf.Clamp(q.y, topoMin, H - bh - margem);
                custo += (Mathf.Abs(fx - q.x) + Mathf.Abs(fy - q.y)) * 40f;                     // empurrar para dentro da tela custa
                q.x = fx; q.y = fy;
                foreach (Rect pr in proibidos) { Rect o = Sobre(q, pr); custo += o.width * o.height; }
                if (custo < melhor) { melhor = custo; r = q; lado = c.Value.Key; }
            }
            alvo = lado == 1 ? cab + new Vector2(0f, -8f * e) : lado == 4 ? pe + new Vector2(0f, 6f * e) : new Vector2(cab.x + (lado == 2 ? 14f : -14f) * e, meio);
        }
        else { r = new Rect((W - bw) * 0.5f, H - bh - 90f * e, bw, bh); }

        // movimento próprio de cada emoção
        Vector2 mov = Vector2.zero;
        if (em == Emocao.Raiva || em == Emocao.Medo) mov = new Vector2(Mathf.Sin(t * 53f), Mathf.Cos(t * 47f)) * 1.2f * e;    // só um tremidinho
        r.position += mov;
        Vector2 pivo = lado == 0 ? r.center : alvo;

        if (Event.current.type == EventType.Repaint)
        {
            List<Vector2> pol = Perimetro(r, em, e, t);
            Color perg = Pergaminho(em), borda = new Color(0.22f, 0.17f, 0.14f), sombra = new Color(0f, 0f, 0f, 0.3f * ap);
            Func<Vector2, Vector2> esc = p => pivo + (p - pivo) * pop;
            for (int i = 0; i < pol.Count; i++) pol[i] = esc(pol[i]);
            // ponta (ou bolinhas, se estiver pensando)
            List<Vector2> ponta = null; Vector2 b1 = Vector2.zero, b2 = Vector2.zero, apice = alvo;
            if (lado != 0)
            {
                float semi = 13f * e; Vector2 b0;
                if (lado == 1) { b0 = new Vector2(Mathf.Clamp(alvo.x, r.x + 40f * e, r.xMax - 40f * e), r.yMax); b1 = b0 + new Vector2(-semi, 0f); b2 = b0 + new Vector2(semi, 0f); }
                else if (lado == 4) { b0 = new Vector2(Mathf.Clamp(alvo.x, r.x + 40f * e, r.xMax - 40f * e), r.y); b1 = b0 + new Vector2(-semi, 0f); b2 = b0 + new Vector2(semi, 0f); }
                else if (lado == 2) { b0 = new Vector2(r.x - 8f * e, Mathf.Clamp(alvo.y, r.y + 30f * e, r.yMax - 30f * e)); b1 = b0 + new Vector2(0f, -semi); b2 = b0 + new Vector2(0f, semi); }
                else { b0 = new Vector2(r.xMax + 8f * e, Mathf.Clamp(alvo.y, r.y + 30f * e, r.yMax - 30f * e)); b1 = b0 + new Vector2(0f, -semi); b2 = b0 + new Vector2(0f, semi); }
                b1 = esc(b1); b2 = esc(b2); apice = esc(alvo);
                ponta = new List<Vector2> { b1, apice, b2 };
            }
            PrepararGL();
            // sombra
            var sh = new List<Vector2>(pol.Count); foreach (Vector2 p in pol) sh.Add(p + new Vector2(4f, 6f) * e); Preencher(sh, sombra);
            if (ponta != null && em != Emocao.Pensando) Preencher(new List<Vector2> { b1 + new Vector2(4f, 6f) * e, apice + new Vector2(4f, 6f) * e, b2 + new Vector2(4f, 6f) * e }, sombra);
            // corpo
            Color corpo = perg; corpo.a = ap; Color bd = borda; bd.a = ap;
            Preencher(pol, corpo);
            // ponta: triângulo fino (ou três bolinhas que diminuem, se estiver pensando)
            if (ponta != null)
            {
                if (em == Emocao.Pensando)
                {
                    Vector2 b0 = (b1 + b2) * 0.5f;
                    for (int k = 0; k < 3; k++) { Vector2 pc = Vector2.Lerp(b0, apice, 0.25f + k * 0.35f); float rr = (11f - k * 3f) * e * pop; Disco(pc, rr + 1.8f * e, bd); Disco(pc, rr, corpo); }
                }
                else
                {
                    Preencher(ponta, corpo);
                    Linha(b1, apice, 2.5f * e, bd); Linha(b2, apice, 2.5f * e, bd);
                }
            }
            Contornar(pol, 3f * e, bd);
            if (ponta != null && em != Emocao.Pensando) { Linha(b1, apice, 2.5f * e, bd); Linha(b2, apice, 2.5f * e, bd); Preencher(new List<Vector2> { b1 + (apice - b1) * 0.02f, b1 + (apice - b1) * 0.55f + (b2 - b1) * 0.16f, b2 + (apice - b2) * 0.55f - (b2 - b1) * 0.16f, b2 + (apice - b2) * 0.02f }, corpo); }
            // plaquinha do nome (colada no canto de cima, à esquerda)
            float nw = estNome.CalcSize(new GUIContent(quem)).x + 54f * e, nh = 34f * e;
            Rect tab = new Rect(r.x + 14f * e, r.y - nh * 0.62f, nw, nh);
            var tp = new List<Vector2> { new Vector2(tab.x + 12f * e, tab.y), new Vector2(tab.xMax - 12f * e, tab.y), new Vector2(tab.xMax, tab.yMax), new Vector2(tab.x, tab.yMax) };
            for (int i = 0; i < tp.Count; i++) tp[i] = esc(tp[i]);
            Preencher(tp, corpo); Contornar(tp, 3f * e, bd, false);
            // marquinhas de emoção
            FimGL();

            // texto
            if (ap >= 0.8f)
            {
                Color tc = new Color(0.16f, 0.12f, 0.1f, 1f); estTexto.normal.textColor = tc;
                GUI.Label(new Rect(r.x + padX, r.y + padY, textoW + 2f, textoH + 6f), visivel, estTexto);
                estNome.normal.textColor = tc;
                GUI.Label(new Rect(tab.x, tab.y, tab.width, tab.height), quem, estNome);
                // ♦ de continuar
                bool escrevendo = t - inicioDaFala < f.texto.Length / VELOCIDADE;
                if (!escrevendo)
                {
                    float pulo = Mathf.Abs(Mathf.Sin(t * 4.5f)) * 5f * e;
                    Vector2 dc = new Vector2(r.xMax - 30f * e, r.yMax - 28f * e + pulo);
                    PrepararGL();
                    Color a1 = new Color(0.18f, 0.3f, 0.25f), a2 = new Color(0.4f, 0.65f, 0.5f);
                    Preencher(new List<Vector2> { dc + new Vector2(0, -12f * e), dc + new Vector2(10f * e, 0), dc + new Vector2(0, 12f * e), dc + new Vector2(-10f * e, 0) }, a1);
                    Preencher(new List<Vector2> { dc + new Vector2(0, -7f * e), dc + new Vector2(6f * e, 0), dc + new Vector2(0, 7f * e), dc + new Vector2(-6f * e, 0) }, a2);
                    FimGL();
                }
            }
        }
        else if (ap >= 0.8f)
        {
            Color tc = new Color(0.16f, 0.12f, 0.1f, 1f); estTexto.normal.textColor = tc; estNome.normal.textColor = tc;
            GUI.Label(new Rect(r.x + padX, r.y + padY, textoW + 2f, textoH + 6f), visivel, estTexto);
        }
    }

}
