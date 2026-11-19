using UnityEngine;

// O Caio entrega o mapa da ilha. Depois da conversa: aparece o aviso do item, o mapa grande abre e mostra como usar,
// o mini-mapa liga no canto da tela e é explicado também. No fim a história segue (os três lugares, em qualquer ordem).
// Nasce sozinho quando o Caio chama TutorialDoMapa.Iniciar(). Avançar: Enter / Espaço / botão A do controle (ou clique, no mini-mapa).
public class TutorialDoMapa : MonoBehaviour
{
    static TutorialDoMapa inst;
    public static bool Ativo { get { return inst != null && inst.fase != Fase.Nada; } }

    enum Fase { Nada, Recebendo, Mapa, FechandoMapa, MiniMapa }
    Fase fase = Fase.Nada; int passo; float inicio, passoInicio;
    MapaDoMundo mapa; MiniMapa mini;
    Font fCinzel, fFell; Texture2D px, texMapa, texBrilho, texRaio;
    const float DURACAO_RECEBIDO = 4.4f;

    class Passo { public string titulo, texto; }
    Passo[] passosMapa, passosMini;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { inst = null; MapaDoMundo.Travado = false; }

    public static void Iniciar()
    {
        if (Campanha.TemMapa && Campanha.TemMiniMapa) { Campanha.Ir(Etapa.Cristais); return; }          // já viu o tutorial
        if (inst == null) { GameObject g = new GameObject("Tutorial do mapa"); inst = g.AddComponent<TutorialDoMapa>(); }
        inst.Comecar();
    }

    void Comecar()
    {
        px = Texture2D.whiteTexture;
        fCinzel = Resources.Load<Font>("Fontes/CinzelBold"); fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
        mapa = FindFirstObjectByType<MapaDoMundo>(); mini = FindFirstObjectByType<MiniMapa>();
        string m = Comandos.Dica(Acao.Mapa);
        passosMapa = new[]
        {
            new Passo { titulo = "O MAPA DA ILHA", texto = "Este é o mapa de toda a ilha. Abra e feche quando quiser com [" + m + "]." },
            new Passo { titulo = "LOCAIS E BIOMAS", texto = "No painel da esquerda estão os locais e os biomas. Cada um aparece no mapa conforme você o descobre." },
            new Passo { titulo = "EXPLORAÇÃO", texto = "No painel da direita você vê quanto da ilha já explorou. As nuvens cobrem o que ainda não foi visitado." },
            new Passo { titulo = "COMO MEXER", texto = "Arraste com o botão esquerdo do mouse, gire com o direito (ou Q e E) e aproxime com a roda. [F] volta para onde você está." },
            new Passo { titulo = "ABAS E GPS", texto = "[Tab] troca entre as abas Mundo, Vila e Pergaminho. Clique num local da lista ou no mapa para traçar o GPS: uma linha dourada mostra o caminho." },
        };
        passosMini = new[]
        {
            new Passo { titulo = "O MINI-MAPA", texto = "Aqui no canto da tela fica o mini-mapa. Ele mostra a região em volta de você e acompanha seus passos o tempo todo." },
            new Passo { titulo = "O QUE APARECE", texto = "A seta no centro é você. Pontos azuis são aliados e pontos vermelhos, inimigos por perto. Os ícones marcam locais importantes e o GPS aparece como uma linha dourada." },
            new Passo { titulo = "ZOOM", texto = "Use [" + Comandos.Dica(Acao.ZoomMais) + "] para aproximar e [" + Comandos.Dica(Acao.ZoomMenos) + "] para afastar. Ele gira junto com a câmera. Para ver a ilha toda, abra o mapa com [" + m + "]." },
        };
        Campanha.TemMapa = true;
        fase = Fase.Recebendo; inicio = Time.unscaledTime; passo = 0;
    }

    bool Avancou()
    {
        bool tecla = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Comandos.NavConfirmar();
        if (fase == Fase.MiniMapa && Input.GetMouseButtonDown(0)) tecla = true;      // no mapa grande o clique é do mapa (GPS)
        return tecla && Time.unscaledTime - passoInicio > 0.45f;
    }

    void Update()
    {
        if (fase == Fase.Nada) return;
        if (fase == Fase.Recebendo)
        {
            if (Time.unscaledTime - inicio > DURACAO_RECEBIDO && !DialogoUI.Aberto)
            {
                if (mapa != null) { MapaDoMundo.Travado = true; if (mapa.AbrirTutorial()) { fase = Fase.Mapa; passo = 0; passoInicio = Time.unscaledTime + 0.8f; return; } }
                ComecarMini();
            }
            return;
        }
        if (fase == Fase.Mapa)
        {
            if (Avancou()) { passo++; passoInicio = Time.unscaledTime; if (passo >= passosMapa.Length) { MapaDoMundo.Travado = false; if (mapa != null) mapa.FecharTutorial(); fase = Fase.FechandoMapa; } }
            return;
        }
        if (fase == Fase.FechandoMapa)
        {
            if (mapa == null || !mapa.Aberto) ComecarMini();
            return;
        }
        if (fase == Fase.MiniMapa && Avancou())
        {
            passo++; passoInicio = Time.unscaledTime;
            if (passo >= passosMini.Length)
            {
                fase = Fase.Nada;
                Campanha.Ir(Etapa.Cristais);
            }
        }
    }

    void ComecarMini()
    {
        MapaDoMundo.Travado = false;
        Campanha.TemMiniMapa = true; fase = Fase.MiniMapa; passo = 0; passoInicio = Time.unscaledTime + 0.6f;
    }

    // ------------------------------------------------------------------ desenho
    static Color Ouro { get { return new Color(0.86f, 0.68f, 0.34f); } }
    void Cx(Rect r, Color c) { Color a = GUI.color; GUI.color = c; GUI.DrawTexture(r, px); GUI.color = a; }

    void OnGUI()
    {
        if (fase == Fase.Nada || Event.current.type != EventType.Repaint) return;
        GUI.depth = -3200;
        float esc = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        if (fase == Fase.Recebendo) { Recebido(W, H, esc); return; }
        if (fase == Fase.Mapa)
        {
            if (passo >= passosMapa.Length || Time.unscaledTime < passoInicio) return;
            float a = Mathf.Clamp01((Time.unscaledTime - passoInicio) / 0.3f);
            float w = Mathf.Min(820f * esc, W - 60f * esc);
            Cartao(new Rect((W - w) * 0.5f, H - 250f * esc, w, 0f), passosMapa[passo], passo + 1, passosMapa.Length, a, esc, false);
        }
        else if (fase == Fase.MiniMapa)
        {
            if (passo >= passosMini.Length || Time.unscaledTime < passoInicio) return;
            float a = Mathf.Clamp01((Time.unscaledTime - passoInicio) / 0.3f);
            float lado = (mini != null ? mini.tamanhoNaTela : 290f) * esc;
            Rect m = new Rect(24f * esc, H - lado - 60f * esc, lado, lado);
            float pulso = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            Color o = new Color(Ouro.r, Ouro.g, Ouro.b, pulso * a);
            float g = 4f * esc;
            Cx(new Rect(m.x - g, m.y - g, m.width + 2 * g, g), o); Cx(new Rect(m.x - g, m.yMax, m.width + 2 * g, g), o);
            Cx(new Rect(m.x - g, m.y, g, m.height), o); Cx(new Rect(m.xMax, m.y, g, m.height), o);
            float w = Mathf.Min(640f * esc, W - m.xMax - 80f * esc);
            Cartao(new Rect(m.xMax + 40f * esc, m.y + m.height * 0.5f - 90f * esc, w, 0f), passosMini[passo], passo + 1, passosMini.Length, a, esc, true);
        }
    }

    // ------------------------------------------------------------------ "item obtido" (estilo Zelda / Elden Ring)
    // imagens desenhadas por código: o mapa dobrado, o brilho redondo e o raio de luz
    void CriarImagens()
    {
        if (texMapa != null) return;
        const int N = 256;
        texMapa = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var cores = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)(N - 1), v = y / (float)(N - 1);
                // folha levemente torta, com cantos queimados
                float margem = 0.07f, dx = Mathf.Min(u - margem, 1f - margem - u), dy = Mathf.Min(v - margem, 1f - margem - v);
                float ruidoBorda = (Mathf.PerlinNoise(u * 14f, v * 14f) - 0.5f) * 0.035f;
                float dentro = Mathf.Min(dx, dy) + ruidoBorda;
                if (dentro < 0f) { cores[y * N + x] = new Color(0, 0, 0, 0); continue; }
                float alfa = Mathf.Clamp01(dentro / 0.006f);
                // papel: tom areia, mais escuro nas bordas e nas dobras (3 faixas verticais)
                float grao = Mathf.PerlinNoise(u * 40f, v * 40f) * 0.08f;
                float dobra = Mathf.Abs(Mathf.Repeat(u * 3f, 1f) - 0.5f) * 2f;                 // 1 no meio da faixa, 0 na dobra
                float sombraDobra = Mathf.Pow(1f - dobra, 6f) * 0.28f;
                float queimado = Mathf.Clamp01(1f - dentro / 0.09f);
                Color papel = Color.Lerp(new Color(0.93f, 0.84f, 0.62f), new Color(0.58f, 0.4f, 0.2f), queimado * 0.85f);
                papel -= new Color(1, 1, 1, 0) * (grao + sombraDobra);
                // mar em volta da ilha: azul bem clarinho; ilha: verde/areia com costa escura
                float cx = u - 0.5f, cy = v - 0.5f;
                float forma = 0.30f + (Mathf.PerlinNoise(u * 4.2f + 3f, v * 4.2f) - 0.5f) * 0.34f - Mathf.Sqrt(cx * cx * 1.1f + cy * cy) ;
                if (dentro > 0.04f)
                {
                    if (forma > 0f)
                    {
                        float alt = Mathf.PerlinNoise(u * 9f, v * 9f);
                        Color terra = Color.Lerp(new Color(0.55f, 0.62f, 0.3f), new Color(0.42f, 0.5f, 0.24f), alt);
                        if (alt > 0.66f) terra = Color.Lerp(terra, new Color(0.55f, 0.45f, 0.32f), (alt - 0.66f) * 4f);
                        papel = Color.Lerp(papel, terra, 0.92f);
                        if (forma < 0.018f) papel = Color.Lerp(papel, new Color(0.25f, 0.17f, 0.09f), 0.8f);        // costa
                    }
                    else if (forma > -0.05f) papel = Color.Lerp(papel, new Color(0.45f, 0.6f, 0.65f), (forma + 0.05f) * 6f);      // águas rasas
                }
                // trilha pontilhada vermelha e o X do tesouro
                float tx = 0.28f + 0.5f * u, caminho = 0.62f - 0.28f * Mathf.Sin((u - 0.2f) * 4.2f);
                bool trilha = u > 0.2f && u < 0.74f && Mathf.Abs(v - caminho) < 0.011f && Mathf.Repeat(u * 34f, 1f) < 0.55f;
                if (trilha && dentro > 0.05f) papel = new Color(0.62f, 0.1f, 0.08f);
                float xx = u - 0.74f, xy = v - (0.62f - 0.28f * Mathf.Sin(0.54f * 4.2f));
                if (Mathf.Abs(xx) < 0.05f && (Mathf.Abs(xy - xx) < 0.012f || Mathf.Abs(xy + xx) < 0.012f)) papel = new Color(0.72f, 0.08f, 0.06f);
                // rosa dos ventos, no canto de baixo à esquerda
                float rx = u - 0.22f, ry = v - 0.2f, rr = Mathf.Sqrt(rx * rx + ry * ry);
                if (rr < 0.075f)
                {
                    float ang = Mathf.Atan2(ry, rx), pontas = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2f)), 6f) * 0.075f;
                    if (rr < pontas * 1.0f + 0.01f) papel = new Color(0.2f, 0.13f, 0.08f);
                    if (Mathf.Abs(rr - 0.062f) < 0.003f) papel = new Color(0.25f, 0.17f, 0.1f);
                }
                papel.a = alfa; cores[y * N + x] = papel;
            }
        texMapa.SetPixels(cores); texMapa.Apply();

        const int B = 128;
        texBrilho = new Texture2D(B, B, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < B; y++) for (int x = 0; x < B; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(B / 2f - 0.5f, B / 2f - 0.5f)) / (B / 2f); float a = Mathf.Clamp01(1f - d); texBrilho.SetPixel(x, y, new Color(1, 1, 1, a * a * a)); }
        texBrilho.Apply();
        texRaio = new Texture2D(64, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 8; y++) for (int x = 0; x < 64; x++) { float a = Mathf.Pow(1f - x / 63f, 1.5f) * Mathf.Sin((y + 0.5f) / 8f * Mathf.PI); texRaio.SetPixel(x, y, new Color(1, 1, 1, a)); }
        texRaio.Apply();
    }

    void Recebido(float W, float H, float esc)
    {
        CriarImagens();
        float t = Time.unscaledTime - inicio, D = DURACAO_RECEBIDO;
        float entra = Mathf.SmoothStep(0f, 1f, t / 0.45f), sai = Mathf.SmoothStep(0f, 1f, (D - t) / 0.7f), a = entra * sai;
        // escurece a tela e fecha as bordas
        Cx(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f * a));
        Cx(new Rect(0, 0, W, 90f * esc * a), new Color(0f, 0f, 0f, 0.6f * a)); Cx(new Rect(0, H - 90f * esc * a, W, 90f * esc * a), new Color(0f, 0f, 0f, 0.6f * a));
        Vector2 c = new Vector2(W * 0.5f, H * 0.43f);
        // brilho de trás e raios girando devagar
        float gs = 640f * esc * (0.85f + 0.15f * Mathf.Sin(t * 2f));
        Color ouroC = new Color(1f, 0.82f, 0.4f);
        GUI.color = new Color(ouroC.r, ouroC.g, ouroC.b, 0.55f * a); GUI.DrawTexture(new Rect(c.x - gs * 0.5f, c.y - gs * 0.5f, gs, gs), texBrilho);
        Matrix4x4 m0 = GUI.matrix;
        for (int i = 0; i < 14; i++)
        {
            float ang = t * 9f + i * (360f / 14f), comp = (i % 2 == 0 ? 560f : 380f) * esc * Mathf.SmoothStep(0f, 1f, t / 0.8f);
            GUIUtility.RotateAroundPivot(ang, c);
            GUI.color = new Color(1f, 0.9f, 0.6f, (i % 2 == 0 ? 0.32f : 0.2f) * a);
            GUI.DrawTexture(new Rect(c.x, c.y - 22f * esc, comp, 44f * esc), texRaio);
            GUI.matrix = m0;
        }
        // o mapa: "estoura" de pequeno para grande, depois flutua e balança
        float k = Mathf.Clamp01(t / 0.7f), pop = k < 1f ? 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f) : 1f;
        float tam = 270f * esc * Mathf.LerpUnclamped(0.25f, 1f, pop) * (1f + 0.015f * Mathf.Sin(t * 3f));
        Vector2 ic = c + new Vector2(0f, Mathf.Sin(t * 2.4f) * 7f * esc);
        float giro = Mathf.Sin(t * 1.6f) * 4f + (1f - pop) * -25f;
        GUIUtility.RotateAroundPivot(giro, ic);
        GUI.color = new Color(0f, 0f, 0f, 0.35f * a); GUI.DrawTexture(new Rect(ic.x - tam * 0.5f + 8f * esc, ic.y - tam * 0.5f + 12f * esc, tam, tam), texMapa);
        GUI.color = new Color(1f, 1f, 1f, a); GUI.DrawTexture(new Rect(ic.x - tam * 0.5f, ic.y - tam * 0.5f, tam, tam), texMapa);
        // brilho que passa por cima do mapa
        float passa = Mathf.Repeat((t - 0.6f) * 0.7f, 2.2f) - 0.6f;
        if (passa > -0.3f && passa < 1.3f) { GUI.color = new Color(1f, 1f, 0.9f, 0.22f * a * Mathf.Sin(Mathf.Clamp01((passa + 0.3f) / 1.6f) * Mathf.PI)); GUI.DrawTexture(new Rect(ic.x - tam * 0.5f + passa * tam - tam * 0.06f, ic.y - tam * 0.45f, tam * 0.12f, tam * 0.9f), px); }
        GUI.matrix = m0;
        // faíscas subindo
        for (int i = 0; i < 18; i++)
        {
            float ph = Mathf.Repeat(t * (0.35f + (i % 5) * 0.06f) + i * 0.173f, 1f), ox = (Mathf.Sin(i * 12.9898f) * 43758.5453f % 1f), oy = Mathf.Cos(i * 4.1f);
            Vector2 p = new Vector2(c.x + (ox - Mathf.Floor(ox) - 0.5f) * 520f * esc + Mathf.Sin(ph * 6f + i) * 14f * esc, c.y + 110f * esc - ph * 330f * esc);
            float s = (4f + (i % 3) * 2f) * esc, al = Mathf.Sin(ph * Mathf.PI) * a;
            GUIUtility.RotateAroundPivot(45f, p); GUI.color = new Color(1f, 0.9f, 0.55f, 0.9f * al); GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), px); GUI.matrix = m0;
        }
        // textos
        float txt = Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.5f) * sai; Vector2 baseT = new Vector2(c.x, c.y + 215f * esc + (1f - txt) * 14f * esc);
        GUIStyle sl = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(26f * esc), alignment = TextAnchor.MiddleCenter };
        GUIStyle sn = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(66f * esc), alignment = TextAnchor.MiddleCenter };
        GUIStyle sd = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(30f * esc), alignment = TextAnchor.MiddleCenter };
        GUI.color = Color.white;
        sl.normal.textColor = new Color(0.8f, 0.74f, 0.6f, txt); GUI.Label(new Rect(baseT.x - 400f * esc, baseT.y - 22f * esc, 800f * esc, 36f * esc), "I T E M   O B T I D O", sl);
        sn.normal.textColor = new Color(0f, 0f, 0f, 0.6f * txt); GUI.Label(new Rect(baseT.x - 600f * esc + 3f, baseT.y + 18f * esc + 3f, 1200f * esc, 90f * esc), "Mapa da Ilha", sn);
        sn.normal.textColor = new Color(1f, 0.86f, 0.5f, txt); GUI.Label(new Rect(baseT.x - 600f * esc, baseT.y + 18f * esc, 1200f * esc, 90f * esc), "Mapa da Ilha", sn);
        float lw = 360f * esc * txt;
        Cx(new Rect(baseT.x - lw, baseT.y + 112f * esc, lw * 2f, 1.6f * esc), new Color(1f, 0.85f, 0.5f, 0.6f * txt));
        GUIUtility.RotateAroundPivot(45f, new Vector2(baseT.x, baseT.y + 112.8f * esc)); Cx(new Rect(baseT.x - 6f * esc, baseT.y + 106.8f * esc, 12f * esc, 12f * esc), new Color(1f, 0.9f, 0.6f, txt)); GUI.matrix = m0;
        sd.normal.textColor = new Color(0.93f, 0.9f, 0.82f, txt); GUI.Label(new Rect(baseT.x - 500f * esc, baseT.y + 126f * esc, 1000f * esc, 40f * esc), "Presente de Caio, o ancião de " + Roteiro.NOME_DA_VILA, sd);
        GUI.color = Color.white;
    }

    // cartão de explicação (altura calculada pelo texto)
    void Cartao(Rect r, Passo p, int n, int total, float a, float esc, bool setaEsquerda)
    {
        GUIStyle st = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(26f * esc), wordWrap = true, alignment = TextAnchor.UpperLeft };
        GUIStyle tt = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(26f * esc), alignment = TextAnchor.UpperLeft };
        GUIStyle dc = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(21f * esc), alignment = TextAnchor.MiddleRight };
        float pad = 28f * esc, larg = r.width - pad * 2f;
        float hTxt = st.CalcHeight(new GUIContent(p.texto), larg);
        float h = pad + 34f * esc + 10f * esc + hTxt + 14f * esc + 30f * esc + pad * 0.7f;
        r.height = h; if (setaEsquerda == false) r.y = Mathf.Min(r.y, Screen.height - h - 40f * esc);
        Cx(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(Ouro.r, Ouro.g, Ouro.b, 0.75f * a));
        Cx(r, new Color(0.04f, 0.035f, 0.04f, 0.96f * a));
        Cx(new Rect(r.x, r.y + 10f * esc, 4f * esc, r.height - 20f * esc), new Color(Ouro.r, Ouro.g, Ouro.b, a));
        if (setaEsquerda)
        {
            Matrix4x4 m0 = GUI.matrix; GUIUtility.RotateAroundPivot(45f, new Vector2(r.x - 1f, r.center.y));
            Cx(new Rect(r.x - 1f - 11f * esc, r.center.y - 11f * esc, 22f * esc, 22f * esc), new Color(Ouro.r, Ouro.g, Ouro.b, 0.9f * a)); GUI.matrix = m0;
        }
        tt.normal.textColor = new Color(Ouro.r, Ouro.g, Ouro.b, a);
        GUI.Label(new Rect(r.x + pad, r.y + pad * 0.8f, larg * 0.8f, 36f * esc), p.titulo, tt);
        dc.normal.textColor = new Color(0.65f, 0.6f, 0.5f, a); GUI.Label(new Rect(r.xMax - pad - 120f * esc, r.y + pad * 0.8f, 120f * esc, 34f * esc), n + " / " + total, dc);
        Cx(new Rect(r.x + pad, r.y + pad * 0.8f + 40f * esc, larg * 0.9f, 1.5f * esc), new Color(Ouro.r, Ouro.g, Ouro.b, 0.5f * a));
        st.normal.textColor = new Color(0.95f, 0.92f, 0.84f, a);
        GUI.Label(new Rect(r.x + pad, r.y + pad * 0.8f + 50f * esc, larg, hTxt + 4f), p.texto, st);
        dc.normal.textColor = new Color(0.8f, 0.77f, 0.7f, 0.85f * a);
        GUI.Label(new Rect(r.x + pad, r.yMax - 38f * esc, larg, 28f * esc), n < total ? "[Enter] continuar" : "[Enter] terminar", dc);
    }
}
