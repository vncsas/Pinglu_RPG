using System;
using System.Collections.Generic;
using UnityEngine;

// Caixa de diálogo na parte de baixo da tela: nome, texto que vai aparecendo e "E / clique" para continuar.
// Enquanto está aberta, o jogador fica parado (SessaoDeJogo.MenuAberto).
public class DialogoUI : MonoBehaviour
{
    public static DialogoUI Instancia { get; private set; }
    public static bool Aberto { get { return Instancia != null && Instancia.falas != null; } }
    public static float UltimoFechamento = -99f;      // quem abre conversas ignora o apertar de tecla logo depois de fechar

    public Font fonteTexto, fonteNome;
    public Texture2D retratoCaio;          // quando existir a foto/arte do Caio, é só colocar aqui

    private IList<Fala> falas; private int indice; private float inicioDaFala; private Action aoFim; private bool menuAntes;
    private GUIStyle estTexto, estNome, estDica; private Texture2D pixel;
    const float VELOCIDADE = 48f;      // letras por segundo

    private void Awake() { Instancia = this; }
    private void OnDestroy() { if (Instancia == this) Instancia = null; }

    public static void Abrir(IList<Fala> lista, Action quandoAcabar)
    {
        if (Instancia == null || lista == null || lista.Count == 0) { if (quandoAcabar != null) quandoAcabar(); return; }
        Instancia.abertoEm = Time.unscaledTime; Instancia.falas = lista; Instancia.indice = 0; Instancia.inicioDaFala = Time.unscaledTime; Instancia.aoFim = quandoAcabar;
        Instancia.menuAntes = SessaoDeJogo.MenuAberto; SessaoDeJogo.MenuAberto = true;
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
        bool avancar = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
        if (!avancar) return;
        if (!terminouDeEscrever) { inicioDaFala = -999f; return; }    // mostra a fala inteira
        if (ultima) { Fechar(); return; }                              // última fala: aperta qualquer botão e sai
        indice++;
        inicioDaFala = Time.unscaledTime;
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
            if (n.Contains("tanque")) return "Tanque";
            if (n.Contains("curandeiro")) return "Curandeiro";
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

    static float Hash(int i, int k) { float v = Mathf.Sin(i * 127.1f + k * 311.7f) * 43758.5453f; return v - Mathf.Floor(v); }

    // ponto na borda do balão (s de 0 a 1 dá a volta inteira)
    static Vector2 NaBorda(Rect r, float s, out Vector2 normal)
    {
        float P = 2f * (r.width + r.height), d = s * P;
        if (d < r.width) { normal = Vector2.down; return new Vector2(r.x + d, r.y); }
        d -= r.width; if (d < r.height) { normal = Vector2.right; return new Vector2(r.xMax, r.y + d); }
        d -= r.height; if (d < r.width) { normal = Vector2.up; return new Vector2(r.xMax - d, r.yMax); }
        d -= r.width; normal = Vector2.left; return new Vector2(r.x, r.yMax - d);
    }

    // partículas na borda: cada personagem tem a sua (brasas, fumaça, brilhos, faíscas de aço...)
    private void Particulas(Rect r, string tipo, EstiloDeFala est, float ap)
    {
        int n = 30; float t = Time.unscaledTime;
        for (int i = 0; i < n; i++)
        {
            float ciclo = 1.6f + Hash(i, 1) * 2.4f;
            float fase = Mathf.Repeat(t / ciclo + Hash(i, 2), 1f);
            Vector2 normal; Vector2 p = NaBorda(r, Hash(i, 3), out normal);
            float tam = 3f + Hash(i, 4) * 5f, a, dx = 0f, dy = 0f; Color c = est.brilho;
            switch (tipo)
            {
                case "demonio": case "tanque": case "cavaleirosombrio":      // brasas que sobem
                    dy = -fase * 70f; dx = Mathf.Sin(fase * 6f + i) * 8f; a = Mathf.Sin(fase * Mathf.PI); tam *= 0.8f; break;
                case "ladrao": case "morgana":                               // fumaça que se espalha para fora
                    dx = normal.x * fase * 36f + Mathf.Sin(fase * 4f + i) * 6f; dy = normal.y * fase * 36f - fase * 14f;
                    a = Mathf.Sin(fase * Mathf.PI) * 0.7f; tam = (tam + 5f) * (0.6f + fase); break;
                case "caio": case "curandeiro":                              // brilhos que piscam parados na moldura
                    a = Mathf.Pow(Mathf.Abs(Mathf.Sin(fase * Mathf.PI * 2f)), 3f); dx = normal.x * 4f; dy = normal.y * 4f; break;
                default:                                                     // faíscas de aço
                    a = Mathf.Pow(Mathf.Abs(Mathf.Sin(fase * Mathf.PI * 4f)), 6f); tam *= 0.7f; break;
            }
            c.a = a * 0.85f * ap; if (c.a <= 0.01f) continue;
            Retangulo(new Rect(p.x + dx - tam * 0.5f, p.y + dy - tam * 0.5f, tam, tam), c, tam);
        }
    }

    // enfeites dos cantos, diferentes por personagem
    private void Enfeites(Rect r, string tipo, EstiloDeFala est, float ap)
    {
        Color c = est.borda; c.a = ap;
        Vector2[] cantos = { new Vector2(r.x, r.y), new Vector2(r.xMax, r.y), new Vector2(r.x, r.yMax), new Vector2(r.xMax, r.yMax) };
        Matrix4x4 m0 = GUI.matrix;
        for (int k = 0; k < 4; k++)
        {
            Vector2 p = cantos[k];
            switch (tipo)
            {
                case "caio": case "curandeiro":     // losangos dourados
                    GUIUtility.RotateAroundPivot(45f, p); Retangulo(new Rect(p.x - 9f, p.y - 9f, 18f, 18f), c, 2f);
                    GUI.matrix = m0; GUIUtility.RotateAroundPivot(45f, p); Retangulo(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), new Color(est.fundo.r, est.fundo.g, est.fundo.b, ap), 1f); break;
                case "ladrao": case "morgana":      // garras (triângulos finos)
                    GUIUtility.RotateAroundPivot(45f, p); Retangulo(new Rect(p.x - 3f, p.y - 22f, 6f, 44f), c, 0f); break;
                case "demonio": case "tanque": case "cavaleirosombrio":    // espinhos
                    GUIUtility.RotateAroundPivot(45f + k * 0f, p); Retangulo(new Rect(p.x - 4f, p.y - 16f, 8f, 32f), c, 0f);
                    GUI.matrix = m0; GUIUtility.RotateAroundPivot(-45f, p); Retangulo(new Rect(p.x - 3f, p.y - 12f, 6f, 24f), c, 0f); break;
                default:                            // rebites
                    Retangulo(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), c, 14f);
                    Retangulo(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), new Color(est.fundo.r, est.fundo.g, est.fundo.b, ap), 6f); break;
            }
            GUI.matrix = m0;
        }
    }

    private void OnGUI()
    {
        if (falas == null) return;
        float e = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(e, e, 1f));
        float W = Screen.width / e, H = 1080f;
        if (pixel == null) { pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply(); }
        if (seta == null)
        {
            seta = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int yy = 0; yy < 32; yy++) for (int xx = 0; xx < 32; xx++)
            {
                float halfW = (yy / 31f) * 15f;                    // triângulo apontando para baixo (y=0 é a ponta)
                float a = Mathf.Clamp01(halfW - Mathf.Abs(xx - 15.5f) + 0.5f);
                seta.SetPixel(xx, yy, new Color(1, 1, 1, a));
            }
            seta.Apply();
        }
        if (estTexto == null)
        {
            estTexto = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true, alignment = TextAnchor.UpperLeft, font = fonteTexto };
            estNome = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, font = fonteNome != null ? fonteNome : fonteTexto };
            estDica = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.LowerRight, font = fonteTexto };
        }
        Fala f = falas[indice];
        string quem = NomeDe(f);
        string tipo = EstiloDeFala.Tipo(quem);
        EstiloDeFala est = EstiloDeFala.Para(quem);
        float ap = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - abertoEm) / 0.35f);      // entrada: sobe e aparece
        float t = Time.unscaledTime;
        Color tx = est.texto; tx.a = ap; estTexto.normal.textColor = tx;
        Color nm = est.nome; nm.a = ap; estNome.normal.textColor = nm;

        float larg = Mathf.Min(W - 260f, 1120f), alt = 190f, x = (W - larg) * 0.5f, y = H - alt - 30f + (1f - ap) * 50f;
        float retr = 132f, textoX = x + retr + 50f, textoL = larg - retr - 80f;
        Rect caixa = new Rect(x, y, larg, alt);

        // brilho pulsando atrás do balão (na cor do personagem)
        float pulso = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
        for (int g = 3; g >= 1; g--)
        {
            Color gc = est.brilho; gc.a = (0.05f + 0.04f * pulso) * ap * (4 - g) / 3f;
            Retangulo(new Rect(x - g * 6f, y - g * 6f, larg + g * 12f, alt + g * 12f), gc, est.cantos + g * 6f);
        }
        Particulas(caixa, tipo, est, ap);

        Retangulo(new Rect(x + 4f, y + 6f, larg, alt), new Color(0, 0, 0, 0.35f * ap), est.cantos);
        Color fundo = est.fundo; fundo.a *= ap; Retangulo(caixa, fundo, est.cantos);
        // faixa de luz em cima, como um reflexo
        Color luz = Color.white; luz.a = 0.05f * ap; Retangulo(new Rect(x + 6f, y + 6f, larg - 12f, alt * 0.38f), luz, Mathf.Max(0f, est.cantos - 4f));
        Color bd = est.borda; bd.a = ap;
        Contorno(caixa, bd, 3f, est.cantos);
        Color fina = est.borda; fina.a = 0.35f * ap;
        Contorno(new Rect(x + 8f, y + 8f, larg - 16f, alt - 16f), fina, 1.5f, Mathf.Max(0f, est.cantos - 6f));
        Enfeites(caixa, tipo, est, ap);

        // retrato: anel de brilho, pulinhos enquanto fala
        bool escrevendo = t - inicioDaFala < f.texto.Length / VELOCIDADE;
        float salto = escrevendo ? Mathf.Abs(Mathf.Sin(t * 11f)) * 3f : 0f;
        Rect rr = new Rect(x + 24f, y + 28f - salto, retr, retr);
        Color anel = est.brilho; anel.a = (0.25f + 0.25f * pulso + (escrevendo ? 0.2f : 0f)) * ap;
        Retangulo(new Rect(rr.x - 10f, rr.y - 10f, rr.width + 20f, rr.height + 20f), anel, est.cantos * 0.6f + 8f);
        Retangulo(new Rect(rr.x - 4f, rr.y - 4f, rr.width + 8f, rr.height + 8f), bd, est.cantos * 0.5f + 2f);
        Texture2D ret = (quem == "Caio" && retratoCaio != null) ? retratoCaio : RetratosDeDialogo.Obter(quem);
        GUI.color = new Color(1, 1, 1, ap);
        float cr = est.cantos * 0.4f;
        GUI.DrawTexture(rr, ret, ScaleMode.ScaleAndCrop, true, 0f, new Color(1, 1, 1, ap), Vector4.zero, new Vector4(cr, cr, cr, cr));
        GUI.color = Color.white;

        // nome com uma linha enfeitada embaixo
        GUI.Label(new Rect(textoX, y + 14f, textoL, 46f), quem.ToUpper(), estNome);
        Color lin = est.borda; lin.a = 0.6f * ap;
        Retangulo(new Rect(textoX, y + 58f, Mathf.Min(textoL, 60f + quem.Length * 22f), 2f), lin, 1f);
        Retangulo(new Rect(textoX, y + 56f, 8f, 6f), bd, 3f);

        float areaAlt = alt - 70f - 40f;
        int tam = 30; estTexto.fontSize = tam;
        while (tam > 20 && estTexto.CalcHeight(new GUIContent(f.texto), textoL) > areaAlt) { tam -= 2; estTexto.fontSize = tam; }
        GUI.Label(new Rect(textoX, y + 66f, textoL, areaAlt + 6f), TextoVisivel(), estTexto);

        // pontinhos (já ditas apagadas, a atual brilha, as que faltam só com contorno) e a seta que pula
        int total = falas.Count; float d = total > 14 ? 7f : 10f, espaco = d + 8f;
        float px = x + larg - 70f - total * espaco, py = y + alt - 36f;
        for (int i = 0; i < total; i++)
        {
            Rect p = new Rect(px + i * espaco, py, d, d);
            if (i < indice) Retangulo(p, new Color(est.borda.r, est.borda.g, est.borda.b, 0.35f * ap), d);
            else if (i == indice) Retangulo(p, new Color(est.brilho.r, est.brilho.g, est.brilho.b, ap), d);
            else Contorno(p, new Color(est.borda.r, est.borda.g, est.borda.b, 0.7f * ap), 1.5f, d);
        }
        if (!escrevendo)
        {
            float pulo = Mathf.Abs(Mathf.Sin(t * 4.5f)) * 7f;
            GUI.color = new Color(est.brilho.r, est.brilho.g, est.brilho.b, 0.95f * ap);
            GUI.DrawTexture(new Rect(x + larg - 58f, y + alt - 42f + pulo, 26f, 22f), seta);
            GUI.color = Color.white;
        }
    }
}
