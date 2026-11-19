using System;
using UnityEngine;

// O Caio entrega o mapa da ilha. Depois da conversa: aparece o "item obtido", o mapa grande abre e o tutorial ensina TUDO na prática
// (como no Hogwarts Legacy: o jogo explica, pede para você fazer a ação e só avança quando você faz) e depois o mini-mapa liga e é explicado.
// Nasce sozinho quando o Caio chama TutorialDoMapa.Iniciar(). Pular tudo: Backspace.
public class TutorialDoMapa : MonoBehaviour
{
    static TutorialDoMapa inst;
    public static bool Ativo { get { return inst != null && inst.fase != Fase.Nada; } }

    enum Fase { Nada, Recebendo, Passos }
    enum Icone { Nada, MouseEsq, MouseDir, Roda, Teclas }
    Fase fase = Fase.Nada; int passo; float inicio, passoInicio, feitoEm = -1f;
    MapaDoMundo mapa; MiniMapa mini;
    Font fCinzel, fFell; Texture2D px, texMapa, texBrilho, texRaio;
    const float DURACAO_RECEBIDO = 4.4f;

    class Passo
    {
        public string titulo, texto, acao;                    // acao = o que o jogador precisa fazer ("" = só ler e apertar Enter)
        public Icone icone; public string[] teclas; public bool arrastar;
        public Func<bool> feito;                              // null = passo de leitura
        public Func<Rect> alvo;                               // moldura dourada em volta de uma parte da tela
        public bool noMapa = true;                            // true: card embaixo no centro; false: card ao lado do mini-mapa
        public Action aoComecar;
    }
    Passo[] passos;
    Rect rCartao, rBotao;                                    // onde o cartão e o botão "continuar" estão na tela (para o clique)
    // referências do começo de cada passo (para saber se o jogador mexeu)
    Vector3 refPivo; float refYaw, refDist, refMini; int refAba; bool gpsFoi;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { inst = null; MapaDoMundo.Travado = false; MapaDoMundo.BloqueioExtra = new Rect(); }

    public static void Iniciar()
    {
        if (Campanha.TemMapa && Campanha.TemMiniMapa) { Campanha.Ir(Etapa.Cristais); return; }          // já viu o tutorial
        if (inst == null) { GameObject g = new GameObject("Tutorial do mapa"); inst = g.AddComponent<TutorialDoMapa>(); }
        inst.Comecar();
    }

    GPSDoMapa Gps { get { return GPSDoMapa.Instancia; } }

    void Comecar()
    {
        px = Texture2D.whiteTexture;
        fCinzel = Resources.Load<Font>("Fontes/CinzelBold"); fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
        mapa = FindFirstObjectByType<MapaDoMundo>(); mini = FindFirstObjectByType<MiniMapa>();
        string m = Comandos.Dica(Acao.Mapa), zm = Comandos.Dica(Acao.ZoomMais), zn = Comandos.Dica(Acao.ZoomMenos);
        passos = new[]
        {
            new Passo { titulo = "O MAPA DA ILHA", texto = "Este é o mapa de toda a ilha de " + Roteiro.NOME_DA_VILA + ". Antes de sair explorando, vamos aprender a usá-lo, passo a passo." },
            new Passo { titulo = "LOCAIS E BIOMAS", texto = "Neste painel ficam os lugares importantes e os biomas. O que você ainda não descobriu aparece como \"???\". Clicar num local já descoberto faz o mapa voar até ele.", alvo = () => mapa != null ? mapa.RetanguloPainelEsq : new Rect() },
            new Passo { titulo = "EXPLORAÇÃO", texto = "Aqui você vê o quanto da ilha já explorou. As nuvens cobrem o que ainda não foi visitado e vão sumindo conforme você anda por lá.", alvo = () => mapa != null ? mapa.RetanguloPainelDir : new Rect() },
            new Passo { titulo = "MOVER O MAPA", texto = "Para ver outras partes da ilha, arraste o mapa.", acao = "Segure o botão esquerdo do mouse e arraste", icone = Icone.MouseEsq, arrastar = true,
                feito = () => mapa != null && (mapa.PivoAlvo - refPivo).magnitude > 30f },
            new Passo { titulo = "GIRAR O MAPA", texto = "Você também pode girar o mapa para olhar de outro lado.", acao = "Segure o botão direito e arraste (ou use Q e E)", icone = Icone.MouseDir, arrastar = true,
                feito = () => mapa != null && Mathf.Abs(Mathf.DeltaAngle(mapa.YawAlvo, refYaw)) > 25f },
            new Passo { titulo = "ZOOM", texto = "Aproxime para ver os detalhes ou afaste para ver a ilha inteira.", acao = "Gire a roda do mouse", icone = Icone.Roda,
                feito = () => mapa != null && Mathf.Abs(mapa.DistAlvo / Mathf.Max(1f, refDist) - 1f) > 0.18f },
            new Passo { titulo = "VOLTAR PARA VOCÊ", texto = "Se se perder no mapa, é só pedir para ele voltar para onde o seu personagem está.", acao = "Aperte F", icone = Icone.Teclas, teclas = new[] { "F" },
                feito = () => Input.GetKeyDown(KeyCode.F) },
            new Passo { titulo = "MARCAR UM DESTINO (GPS)", texto = "Quer ir a algum lugar? Marque o destino e o jogo traça o caminho para você.", acao = "Clique num ponto do mapa (ou num local da lista)", icone = Icone.MouseEsq,
                feito = () => Gps != null && Gps.Ativo },
            new Passo { titulo = "O CAMINHO", texto = "Uma linha dourada mostra a rota, seguindo as estradas, aqui no mapa e também no mini-mapa. A distância que falta aparece no painel GPS. Se você sair da rota, ela se recalcula sozinha.", alvo = () => mapa != null ? mapa.RetanguloPainelEsq : new Rect() },
            new Passo { titulo = "DESMARCAR O DESTINO", texto = "Mudou de ideia? É fácil cancelar.", acao = "Clique de novo no ponto marcado (ou aperte X)", icone = Icone.Teclas, teclas = new[] { "X" },
                feito = () => Gps != null && !Gps.Ativo },
            new Passo { titulo = "TROCAR DE ABA", texto = "O mapa tem três abas: Mundo, Vila e Pergaminho (a história até aqui).", acao = "Aperte Tab", icone = Icone.Teclas, teclas = new[] { "Tab" },
                alvo = () => mapa != null ? mapa.RetanguloAbas : new Rect(), feito = () => mapa != null && mapa.AbaAtual != refAba },
            new Passo { titulo = "FECHAR O MAPA", texto = "Para voltar ao jogo, feche o mapa. Você pode abri-lo de novo sempre que quiser.", acao = "Aperte " + m, icone = Icone.Teclas, teclas = new[] { m },
                aoComecar = () => MapaDoMundo.Travado = false, feito = () => mapa == null || !mapa.Aberto },
            new Passo { titulo = "O MINI-MAPA", texto = "Aqui no canto da tela fica o mini-mapa: ele mostra a região em volta de você e acompanha seus passos o tempo todo.", noMapa = false,
                aoComecar = () => Campanha.TemMiniMapa = true, alvo = RetanguloMini },
            new Passo { titulo = "O QUE APARECE NELE", texto = "A seta no centro é você. Pontos azuis são aliados e pontos vermelhos, inimigos por perto. Os ícones marcam lugares importantes e o GPS aparece como a linha dourada.", noMapa = false, alvo = RetanguloMini },
            new Passo { titulo = "ZOOM DO MINI-MAPA", texto = "Você pode aproximar e afastar o mini-mapa sem abrir o mapa grande.", acao = "Segure " + zm + " ou " + zn, icone = Icone.Teclas, teclas = new[] { zm, zn }, noMapa = false, alvo = RetanguloMini,
                feito = () => mini != null && Mathf.Abs(mini.metrosVisiveis - refMini) > 15f },
            new Passo { titulo = "TUDO PRONTO!", texto = "Agora você sabe tudo: abra o mapa com " + m + " quando quiser se orientar. Boa viagem, aventureiro!", noMapa = false },
        };
        Campanha.TemMapa = true;
        fase = Fase.Recebendo; inicio = Time.unscaledTime; passo = 0;
    }

    Rect RetanguloMini()
    {
        float e = Screen.height / 1080f, lado = (mini != null ? mini.tamanhoNaTela : 290f) * e;
        return new Rect(24f * e, Screen.height - lado - 60f * e, lado, lado);
    }

    void IniciarPasso(int i)
    {
        passo = i; passoInicio = Time.unscaledTime; feitoEm = -1f;
        if (i >= passos.Length) { Terminar(); return; }
        if (mapa != null) { refPivo = mapa.PivoAlvo; refYaw = mapa.YawAlvo; refDist = mapa.DistAlvo; refAba = mapa.AbaAtual; }
        if (mini != null) refMini = mini.metrosVisiveis;
        if (passos[i].aoComecar != null) passos[i].aoComecar();
    }

    void Terminar()
    {
        fase = Fase.Nada; MapaDoMundo.Travado = false; MapaDoMundo.BloqueioExtra = new Rect();
        if (mapa != null && mapa.Aberto) mapa.FecharTutorial();
        Campanha.TemMapa = Campanha.TemMiniMapa = true;
        Campanha.Ir(Etapa.Cristais);
    }

    bool Confirmou()
    {
        bool clique = Input.GetMouseButtonDown(0) && (!passos[passo].noMapa ? true : rBotao.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)));
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Comandos.NavConfirmar() || clique;
    }

    void Update()
    {
        if (fase == Fase.Nada) return;
        if (fase == Fase.Recebendo)
        {
            if (Time.unscaledTime - inicio > DURACAO_RECEBIDO && !DialogoUI.Aberto)
            {
                fase = Fase.Passos;
                if (mapa != null) { MapaDoMundo.Travado = true; mapa.AbrirTutorial(); }
                IniciarPasso(0); passoInicio = Time.unscaledTime + 0.7f;
            }
            return;
        }
        if (passo >= passos.Length) return;
        MapaDoMundo.BloqueioExtra = rCartao;
        if (Input.GetKeyDown(KeyCode.Backspace)) { Terminar(); return; }                        // pular o tutorial
        if (Time.unscaledTime < passoInicio) return;
        Passo p = passos[passo];
        if (p.feito == null) { if (Time.unscaledTime - passoInicio > 0.5f && Confirmou()) IniciarPasso(passo + 1); return; }
        if (feitoEm < 0f) { if (Time.unscaledTime - passoInicio > 0.35f && p.feito()) feitoEm = Time.unscaledTime; }
        else if (Time.unscaledTime - feitoEm > 1.1f) IniciarPasso(passo + 1);                  // mostra o "muito bem" e avança
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


    // ------------------------------------------------------------------ desenho
    static Color Ouro { get { return new Color(0.86f, 0.68f, 0.34f); } }
    void Cx(Rect r, Color c) { Color a = GUI.color; GUI.color = c; GUI.DrawTexture(r, px); GUI.color = a; }
    void RR(Rect r, Color c, float raio, float borda = 0f)
    {
        raio = Mathf.Min(raio, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, px, ScaleMode.StretchToFill, true, 0f, c, borda > 0f ? new Vector4(borda, borda, borda, borda) : Vector4.zero, new Vector4(raio, raio, raio, raio));
    }

    void OnGUI()
    {
        if (fase == Fase.Nada || Event.current.type != EventType.Repaint) return;
        GUI.depth = -3200;
        float esc = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        if (fase == Fase.Recebendo) { Recebido(W, H, esc); return; }
        if (passo >= passos.Length || Time.unscaledTime < passoInicio) return;
        Passo p = passos[passo];
        float a = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - passoInicio) / 0.35f);
        float t = Time.unscaledTime;
        // moldura dourada pulsando na parte da tela de que o passo fala
        if (p.alvo != null)
        {
            Rect r = p.alvo(); if (r.width > 1f)
            {
                float pulso = 0.55f + 0.45f * Mathf.Sin(t * 4f), g = 5f * esc, ex = 5f * esc;
                r = new Rect(r.x - ex, r.y - ex, r.width + 2f * ex, r.height + 2f * ex);
                Color o = new Color(Ouro.r, Ouro.g, Ouro.b, pulso * a);
                RR(r, o, 10f * esc, g * 0.7f);
                RR(new Rect(r.x - 6f * esc, r.y - 6f * esc, r.width + 12f * esc, r.height + 12f * esc), new Color(o.r, o.g, o.b, o.a * 0.35f), 14f * esc, 3f * esc);
            }
        }
        float w = Mathf.Min(720f * esc, W - 80f * esc);
        float x = p.noMapa ? (W - w) * 0.5f : RetanguloMini().xMax + 44f * esc;
        if (!p.noMapa) w = Mathf.Min(700f * esc, W - x - 40f * esc);
        Cartao(x, p.noMapa ? -1f : RetanguloMini().center.y, w, p, a, esc, t);
    }

    // cartão de tutorial: título, texto, a ação pedida (com ícone do mouse/tecla), progresso e "pular"
    void Cartao(float x, float centroY, float w, Passo p, float a, float esc, float t)
    {
        GUIStyle st = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(26f * esc), wordWrap = true, alignment = TextAnchor.UpperLeft };
        GUIStyle tt = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(27f * esc), alignment = TextAnchor.MiddleLeft };
        GUIStyle sm = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(21f * esc), alignment = TextAnchor.MiddleRight };
        GUIStyle ac = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(21f * esc), wordWrap = true, alignment = TextAnchor.MiddleLeft };
        float pad = 28f * esc, larg = w - pad * 2f;
        bool leitura = p.feito == null; bool concluido = feitoEm >= 0f;
        float hTxt = st.CalcHeight(new GUIContent(p.texto), larg);
        float hAcao = leitura ? 0f : 84f * esc;
        float h = pad * 0.8f + 44f * esc + hTxt + 14f * esc + hAcao + 40f * esc + pad * 0.5f;
        float y = centroY < 0f ? Screen.height - h - 96f * esc : Mathf.Clamp(centroY - h * 0.5f, 40f * esc, Screen.height - h - 40f * esc);
        Rect r = new Rect(x, y + (1f - a) * 16f * esc, w, h); rCartao = r; rBotao = new Rect(r.xMax - pad - 190f * esc, r.yMax - 46f * esc, 190f * esc, 36f * esc);
        // sombra, fundo, borda dourada e faixa lateral
        RR(new Rect(r.x + 5f * esc, r.y + 7f * esc, r.width, r.height), new Color(0f, 0f, 0f, 0.4f * a), 12f * esc);
        RR(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(Ouro.r, Ouro.g, Ouro.b, 0.8f * a), 13f * esc);
        RR(r, new Color(0.045f, 0.04f, 0.045f, 0.97f * a), 12f * esc);
        RR(new Rect(r.x + 12f * esc, r.y + 12f * esc, 5f * esc, 44f * esc), new Color(Ouro.r, Ouro.g, Ouro.b, a), 3f * esc);
        // título e número do passo
        tt.normal.textColor = new Color(1f, 0.84f, 0.48f, a);
        GUI.Label(new Rect(r.x + pad + 8f * esc, r.y + pad * 0.5f, larg - 130f * esc, 44f * esc), p.titulo, tt);
        sm.normal.textColor = new Color(0.65f, 0.6f, 0.5f, a);
        GUI.Label(new Rect(r.xMax - pad - 140f * esc, r.y + pad * 0.5f, 140f * esc, 44f * esc), (passo + 1) + " / " + passos.Length, sm);
        Cx(new Rect(r.x + pad, r.y + pad * 0.5f + 46f * esc, larg, 1.5f * esc), new Color(Ouro.r, Ouro.g, Ouro.b, 0.45f * a));
        st.normal.textColor = new Color(0.95f, 0.92f, 0.84f, a);
        float yy = r.y + pad * 0.5f + 56f * esc;
        GUI.Label(new Rect(r.x + pad, yy, larg, hTxt + 4f), p.texto, st); yy += hTxt + 14f * esc;
        // a ação pedida: ícone + texto + marca de feito
        if (!leitura)
        {
            Rect ab = new Rect(r.x + pad, yy, larg, 72f * esc);
            Color fundo = concluido ? new Color(0.12f, 0.3f, 0.16f, a) : new Color(0.1f, 0.085f, 0.07f, a);
            RR(ab, fundo, 10f * esc);
            RR(ab, concluido ? new Color(0.45f, 0.9f, 0.5f, a) : new Color(Ouro.r, Ouro.g, Ouro.b, (0.55f + 0.3f * Mathf.Sin(t * 4f)) * a), 10f * esc, 2f * esc);
            DesenharIcone(new Rect(ab.x + 16f * esc, ab.y + 8f * esc, 150f * esc, 56f * esc), p, a, t);
            ac.normal.textColor = concluido ? new Color(0.75f, 1f, 0.78f, a) : new Color(1f, 0.92f, 0.7f, a);
            GUI.Label(new Rect(ab.x + 180f * esc, ab.y, ab.width - 180f * esc - 70f * esc, ab.height), p.acao, ac);
            // círculo que vira "visto"
            Vector2 c = new Vector2(ab.xMax - 40f * esc, ab.center.y); float rr = 20f * esc;
            RR(new Rect(c.x - rr, c.y - rr, rr * 2f, rr * 2f), concluido ? new Color(0.35f, 0.85f, 0.45f, a) : new Color(0.3f, 0.27f, 0.22f, a), rr);
            if (concluido)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - feitoEm) / 0.35f);
                Vector2 p1 = c + new Vector2(-9f, 1f) * esc, p2 = c + new Vector2(-3f, 8f) * esc, p3 = c + new Vector2(10f, -8f) * esc;
                float l1 = Mathf.Clamp01(k / 0.4f), l2 = Mathf.Clamp01((k - 0.4f) / 0.6f);
                Traco(p1, Vector2.Lerp(p1, p2, l1), 4.5f * esc, new Color(1f, 1f, 1f, a));
                if (l2 > 0f) Traco(p2, Vector2.Lerp(p2, p3, l2), 4.5f * esc, new Color(1f, 1f, 1f, a));
                // anel que se abre quando acerta
                float onda = Mathf.Clamp01((Time.unscaledTime - feitoEm) / 0.6f);
                RR(new Rect(c.x - rr * (1f + onda * 0.9f), c.y - rr * (1f + onda * 0.9f), rr * 2f * (1f + onda * 0.9f), rr * 2f * (1f + onda * 0.9f)), new Color(0.5f, 1f, 0.6f, (1f - onda) * 0.8f * a), rr * 2f, 2.5f * esc);
            }
            yy += 84f * esc;
        }
        // rodapé: continuar / pular
        GUIStyle ro = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(21f * esc), alignment = TextAnchor.MiddleLeft };
        ro.normal.textColor = new Color(0.62f, 0.58f, 0.5f, a * 0.9f);
        GUI.Label(new Rect(r.x + pad, r.yMax - 38f * esc, 300f * esc, 28f * esc), "[Backspace] pular tutorial", ro);
        if (leitura)
        {
            Rect bt = new Rect(r.xMax - pad - 190f * esc, r.yMax - 46f * esc, 190f * esc, 36f * esc);
            float pul = 0.75f + 0.25f * Mathf.Sin(t * 5f);
            RR(bt, new Color(Ouro.r * 0.5f, Ouro.g * 0.4f, Ouro.b * 0.25f, a * pul), 8f * esc); RR(bt, new Color(Ouro.r, Ouro.g, Ouro.b, a), 8f * esc, 2f * esc);
            GUIStyle bs = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(19f * esc), alignment = TextAnchor.MiddleCenter };
            bs.normal.textColor = new Color(1f, 0.93f, 0.7f, a);
            GUI.Label(bt, passo == passos.Length - 1 ? "ENTER  terminar" : "ENTER  continuar", bs);
        }
    }

    // linha grossa de um ponto a outro (para o "visto" e as setas)
    void Traco(Vector2 a, Vector2 b, float esp, Color cor)
    {
        Vector2 d = b - a; float len = d.magnitude; if (len < 0.5f) return;
        Matrix4x4 m0 = GUI.matrix; GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        RR(new Rect(a.x - esp * 0.5f, a.y - esp * 0.5f, len + esp, esp), cor, esp * 0.5f); GUI.matrix = m0;
    }
    // cantos com raios diferentes (cima-esq, cima-dir, baixo-dir, baixo-esq)
    void RRc(Rect r, Color c, float tl, float tr, float br, float bl)
    {
        GUI.DrawTexture(r, px, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(tl, tr, br, bl));
    }

    // ícone da ação: mouse (botão/roda destacados) ou teclas
    void DesenharIcone(Rect r, Passo p, float a, float t)
    {
        float pulso = 0.5f + 0.5f * Mathf.Sin(t * 5f);
        Color hi = new Color(1f, 0.8f, 0.3f, a * (0.65f + 0.35f * pulso)), linha = new Color(0.88f, 0.83f, 0.72f, a), corpo = new Color(0.17f, 0.15f, 0.13f, a);
        if (p.icone == Icone.MouseEsq || p.icone == Icone.MouseDir || p.icone == Icone.Roda)
        {
            float mh = r.height, mw = mh * 0.6f; Rect m = new Rect(r.x + 4f, r.y, mw, mh);
            float rad = mw * 0.5f, topo = mh * 0.44f;
            RR(m, corpo, rad); RR(m, linha, rad, 2.2f);
            // botões: só o botão pedido acende, com o canto de fora arredondado como o corpo
            if (p.icone == Icone.MouseEsq) RRc(new Rect(m.x + 2.5f, m.y + 2.5f, mw * 0.5f - 3f, topo - 2.5f), hi, rad - 2f, 0f, 0f, 0f);
            if (p.icone == Icone.MouseDir) RRc(new Rect(m.center.x + 0.5f, m.y + 2.5f, mw * 0.5f - 3f, topo - 2.5f), hi, 0f, rad - 2f, 0f, 0f);
            Cx(new Rect(m.x + 2f, m.y + topo, mw - 4f, 2f), linha); Cx(new Rect(m.center.x - 1f, m.y + 3f, 2f, topo - 3f), linha);
            if (p.icone == Icone.Roda) RR(new Rect(m.center.x - 3.5f, m.y + topo * 0.22f, 7f, topo * 0.56f), hi, 3.5f);
            Vector2 cb = p.icone == Icone.MouseEsq ? new Vector2(m.x + mw * 0.25f, m.y + topo * 0.5f) : p.icone == Icone.MouseDir ? new Vector2(m.x + mw * 0.75f, m.y + topo * 0.5f) : new Vector2(m.center.x, m.y + topo * 0.5f);
            Color sa = new Color(1f, 0.9f, 0.6f, a);
            float ox = m.xMax + 22f, oy = m.center.y;
            if (p.icone == Icone.Roda)
            {
                float mov = Mathf.Repeat(t * 1.4f, 1f);                         // duas setas que sobem e descem em sequência
                Seta(new Vector2(ox + 12f, oy - 6f), 0, 11f, new Color(sa.r, sa.g, sa.b, a * (0.35f + 0.65f * Mathf.Sin(mov * Mathf.PI))));
                Seta(new Vector2(ox + 12f, oy + 6f), 1, 11f, new Color(sa.r, sa.g, sa.b, a * (0.35f + 0.65f * Mathf.Sin(Mathf.Repeat(mov + 0.5f, 1f) * Mathf.PI))));
            }
            else if (p.arrastar)
            {
                // arrastar: um pontinho (o cursor) vai e volta deixando rastro, com pontas de seta nas duas pontas
                float larg = 78f, x0 = ox, f = Mathf.PingPong(t * 0.9f, 1f); f = f * f * (3f - 2f * f);
                Cx(new Rect(x0, oy - 1f, larg, 2f), new Color(sa.r, sa.g, sa.b, a * 0.35f));
                Seta(new Vector2(x0 - 4f, oy), 2, 10f, new Color(sa.r, sa.g, sa.b, a * 0.8f)); Seta(new Vector2(x0 + larg + 4f, oy), 3, 10f, new Color(sa.r, sa.g, sa.b, a * 0.8f));
                for (int i = 3; i >= 0; i--) { float ff = Mathf.Clamp01(f - i * 0.05f * (Mathf.PingPong(t * 0.9f + 0.001f, 1f) > Mathf.PingPong(t * 0.9f, 1f) ? 1f : -1f)); float px2 = x0 + 6f + ff * (larg - 12f); float rr2 = (7f - i * 1.4f); RR(new Rect(px2 - rr2, oy - rr2, rr2 * 2f, rr2 * 2f), new Color(hi.r, hi.g, hi.b, a * (i == 0 ? 1f : 0.25f / i)), rr2); }
            }
            else
            {
                // clique: anéis que se abrem em cima do botão pedido
                for (int i = 0; i < 2; i++)
                {
                    float k = Mathf.Repeat(t * 1.1f + i * 0.5f, 1f), rr2 = Mathf.Lerp(5f, 17f, k);
                    RR(new Rect(cb.x - rr2, cb.y - rr2, rr2 * 2f, rr2 * 2f), new Color(1f, 0.9f, 0.55f, a * (1f - k) * 0.9f), rr2, 2f);
                }
                Cx(new Rect(ox + 6f, oy - 1f, 28f, 2f), new Color(sa.r, sa.g, sa.b, a * 0.5f));
                Seta(new Vector2(ox + 2f, oy), 2, 9f, new Color(sa.r, sa.g, sa.b, a * 0.8f));
            }
            return;
        }
        if (p.teclas == null) return;
        float x = r.x; float h = r.height * 0.82f; GUIStyle ks = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(h * 0.42f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        for (int i = 0; i < p.teclas.Length; i++)
        {
            string k = p.teclas[i]; float kw = Mathf.Max(h, ks.CalcSize(new GUIContent(k)).x + 26f);
            Rect kr = new Rect(x, r.y + (r.height - h) * 0.5f - pulso * 2f, kw, h);
            RR(new Rect(kr.x, kr.y + 5f, kr.width, kr.height), new Color(0.05f, 0.04f, 0.04f, a), 10f);
            RR(kr, new Color(0.26f, 0.23f, 0.2f, a), 10f); RR(kr, new Color(Ouro.r, Ouro.g, Ouro.b, a), 10f, 2.4f);
            ks.normal.textColor = new Color(1f, 0.93f, 0.72f, a); GUI.Label(kr, k, ks);
            x += kw + 10f;
            if (i < p.teclas.Length - 1) { GUIStyle ou = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(h * 0.34f), alignment = TextAnchor.MiddleCenter }; ou.normal.textColor = new Color(0.8f, 0.75f, 0.65f, a); GUI.Label(new Rect(x - 4f, r.y, 30f, r.height), "ou", ou); x += 30f; }
        }
    }
    // ponta de seta: dir 0 cima, 1 baixo, 2 esquerda, 3 direita
    void Seta(Vector2 c, int dir, float s, Color cor)
    {
        for (int i = 0; i < 7; i++)
        {
            float k = i / 6f, ww = (1f - k) * s * 1.5f, off = -s * 0.5f + k * s;
            if (dir == 0) Cx(new Rect(c.x - ww * 0.5f, c.y + off, ww, s / 6f + 0.6f), cor);
            else if (dir == 1) Cx(new Rect(c.x - ww * 0.5f, c.y - off - s / 6f, ww, s / 6f + 0.6f), cor);
            else if (dir == 2) Cx(new Rect(c.x + off, c.y - ww * 0.5f, s / 6f + 0.6f, ww), cor);
            else Cx(new Rect(c.x - off - s / 6f, c.y - ww * 0.5f, s / 6f + 0.6f, ww), cor);
        }
    }
}
