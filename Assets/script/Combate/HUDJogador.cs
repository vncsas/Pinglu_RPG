using UnityEngine;
using UnityEngine.SceneManagement;

// Interface do jogo: moldura com o retrato do cavaleiro (ao vivo), barra de
// vida (verde) e barra de especial (azul), onda atual, mensagens no meio da
// tela e tela de morte (R para tentar de novo).
public class HUDJogador : MonoBehaviour
{
    [Header("Imagens (o menu Pinglu > 11 preenche sozinho)")]
    [SerializeField] private Texture2D moldura;
    [SerializeField] private Texture2D preenchimentoVida;
    [SerializeField] private Texture2D preenchimentoEspecial;

    [Header("Posição e tamanho")]
    [SerializeField] private bool emCima = true;
    [SerializeField] private float largura = 560f;   // em pixels numa tela 1080p

    [Header("Retrato")]
    [SerializeField] private bool mostrarRetrato = true;
    [SerializeField] private float distanciaDaCamera = 0.85f;
    [SerializeField] private Color fundoDoRetrato = new Color(0.09f, 0.07f, 0.05f);

    // posições dentro da imagem da moldura (1024 x 372)
    static readonly Vector2 TAMANHO = new Vector2(1024f, 372f);
    static readonly Rect RETRATO = Rect.MinMaxRect(52f, 72f, 292f, 312f);
    static readonly Rect BARRA_VIDA = Rect.MinMaxRect(300f, 116f, 960f, 172f);
    static readonly Rect BARRA_ESPECIAL = Rect.MinMaxRect(300f, 186f, 900f, 230f);
    const int CAMADA_RETRATO = 31;

    private static string mensagem;
    private static float mensagemAte;

    private Vida vida;
    private CombateJogador combate;
    private PlayerLocomotion locoDono;
    private HabilidadesDoPersonagem hab;
    private StatusDeBatalha status;
    private float vidaMostrada = 1f, vidaAtrasada = 1f, especialMostrado;
    private GUIStyle estiloTexto, estiloGrande, estiloMenor, estiloBarra;

    private Camera camRetrato;
    private RenderTexture rtRetrato;
    private Transform cabeca;

    // os avisinhos de texto no meio da tela (GPS, "cristal recuperado"...) ficam desligados: só a faixa de capítulo aparece
    public static bool MensagensNaTela = false;
    public static void Mostrar(string texto, float segundos)
    {
        if (!MensagensNaTela) return;
        mensagem = texto;
        mensagemAte = Time.time + segundos;
    }

    private void Awake()
    {
        vida = GetComponent<Vida>();
        combate = GetComponent<CombateJogador>();
        locoDono = GetComponent<PlayerLocomotion>();
        hab = GetComponent<HabilidadesDoPersonagem>();
        status = GetComponent<StatusDeBatalha>();
    }

    private void Start()
    {
        if (mostrarRetrato) CriarCameraDoRetrato();
    }

    // câmera pequena que filma só o rosto do cavaleiro
    private void CriarCameraDoRetrato()
    {
        Animator a = GetComponentInChildren<Animator>();
        if (a != null && a.isHuman) cabeca = a.GetBoneTransform(HumanBodyBones.Head);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = CAMADA_RETRATO;

        rtRetrato = new RenderTexture(256, 256, 16);
        GameObject g = new GameObject("Camera do Retrato");
        camRetrato = g.AddComponent<Camera>();
        camRetrato.cullingMask = 1 << CAMADA_RETRATO;
        camRetrato.clearFlags = CameraClearFlags.SolidColor;
        camRetrato.backgroundColor = fundoDoRetrato;
        camRetrato.fieldOfView = 26f;
        camRetrato.nearClipPlane = 0.05f;
        camRetrato.farClipPlane = 10f;
        camRetrato.targetTexture = rtRetrato;
        camRetrato.depth = -10f;
    }

    private void LateUpdate()
    {
        if (camRetrato == null) return;
        Vector3 alvo = cabeca != null ? cabeca.position : transform.position + Vector3.up * 1.65f;
        Vector3 frente = transform.forward; frente.y = 0f; frente.Normalize();
        camRetrato.transform.position = alvo + frente * distanciaDaCamera + Vector3.up * 0.05f + transform.right * 0.12f;
        camRetrato.transform.LookAt(alvo - Vector3.up * 0.04f);
    }

    private void OnDestroy()
    {
        if (rtRetrato != null) rtRetrato.Release();
        if (camRetrato != null) Destroy(camRetrato.gameObject);
    }

    private void Update()
    {
        if (vida != null)
        {
            vidaMostrada = Mathf.MoveTowards(vidaMostrada, vida.Fracao, Time.unscaledDeltaTime * 2.5f);
            vidaAtrasada = vidaAtrasada > vidaMostrada ? Mathf.MoveTowards(vidaAtrasada, vidaMostrada, Time.unscaledDeltaTime * 0.6f) : vidaMostrada;
        }
        if (combate != null) especialMostrado = Mathf.MoveTowards(especialMostrado, combate.EspecialFracao, Time.unscaledDeltaTime * 2f);

        // morte: a tela de morte cuida do visual; R devolve o jogador ao último ponto de descanso (sem recarregar a história)
        if (EhJogadorLocal)
        {
            TelaDeMorte.Atualizar(vida != null && vida.Morto, SessaoDeJogo.Online, () => { if (!Renascimento.Renascer()) { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); } });
        }
    }

    private bool EhJogadorLocal { get { return locoDono == null || (locoDono.enabled && !locoDono.Companheiro); } }

    private void CriarEstilos()
    {
        float esc = Screen.height / 1080f * Configuracoes.EscalaUI;
        estiloTexto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * esc), fontStyle = FontStyle.Bold };
        estiloTexto.normal.textColor = new Color(0.95f, 0.88f, 0.72f);
        estiloGrande = new GUIStyle(estiloTexto) { fontSize = Mathf.RoundToInt(54 * esc), alignment = TextAnchor.MiddleCenter };
        estiloMenor = new GUIStyle(estiloTexto) { fontSize = Mathf.RoundToInt(19 * esc), fontStyle = FontStyle.Normal };
        estiloBarra = new GUIStyle(estiloMenor) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
    }

    private void OnGUI()
    {
        if (SessaoDeJogo.MenuAberto) return;
        if (estiloTexto == null || Event.current.type == EventType.Layout) CriarEstilos();
        float esc = Screen.height / 1080f * Configuracoes.EscalaUI;

        if (EhJogadorLocal && (TelaDeMorte.Ativa || vida != null && vida.Morto)) GUI.depth = -1000;      // a tela de morte fica por cima de tudo
        if (EhJogadorLocal && vida != null && !vida.Morto && !vida.Imortal && !SessaoDeJogo.MenuAberto) AvisoDeVidaBaixa.Desenhar(vida.Fracao);
        if (!TelaDeMorte.Ativa)
        {
            DesenharMoldura(esc);
            DesenharHabilidades(esc);
        }

        // ---- onda (canto superior direito)
        GerenciadorDeOndas g = GerenciadorDeOndas.Instancia;
        if (g != null && g.Ativo && g.OndaAtual > 0)
        {
            GUI.Label(new Rect(Screen.width - 330 * esc, 30 * esc, 300 * esc, 40 * esc), "Onda " + g.OndaAtual, estiloTexto);
            GUI.Label(new Rect(Screen.width - 330 * esc, 64 * esc, 300 * esc, 40 * esc), "Inimigos: " + (g.Vivos + g.Restantes), estiloMenor);
        }

        // ---- mensagem no meio da tela
        if (!string.IsNullOrEmpty(mensagem) && Time.time < mensagemAte)
        {
            float alfa = Mathf.Clamp01((mensagemAte - Time.time) / 0.6f);
            Color antes = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alfa);
            GUI.Label(new Rect(0, Screen.height * 0.22f, Screen.width, 80 * esc), mensagem, estiloGrande);
            GUI.color = antes;
        }

        // ---- tela de morte (e a transição de volta)
        if (EhJogadorLocal)
        {
            string extra = g != null && g.Ativo && g.OndaAtual > 0 ? "Onda " + g.OndaAtual : "";
            TelaDeMorte.Desenhar(esc, SessaoDeJogo.Online, extra);
        }
    }

    private void DesenharMoldura(float esc)
    {
        if (vida == null) return;
        float k = largura * esc / TAMANHO.x;
        float alturaTotal = TAMANHO.y * k;
        Vector2 origem = new Vector2(16f * esc, emCima ? 10f * esc : Screen.height - alturaTotal - 10f * esc);
        System.Func<Rect, Rect> R = r => new Rect(origem.x + r.x * k, origem.y + r.y * k, r.width * k, r.height * k);

        // retrato
        Rect rr = R(RETRATO);
        Retangulo(rr, fundoDoRetrato);
        if (rtRetrato != null) GUI.DrawTexture(rr, rtRetrato, ScaleMode.ScaleAndCrop);

        // barra de vida
        Rect bv = R(BARRA_VIDA);
        Retangulo(bv, new Color(0.04f, 0.04f, 0.04f, 0.92f));
        Retangulo(new Rect(bv.x, bv.y, bv.width * vidaAtrasada, bv.height), new Color(0.92f, 0.85f, 0.7f, 0.75f));
        Preencher(bv, vidaMostrada, preenchimentoVida, new Color(0.25f, 0.55f, 0.12f));

        // barra de especial
        Rect be = R(BARRA_ESPECIAL);
        Retangulo(be, new Color(0.04f, 0.04f, 0.04f, 0.92f));
        Preencher(be, especialMostrado, preenchimentoEspecial, new Color(0.1f, 0.35f, 0.65f));

        // marcas dos custos das habilidades (33%, 66%, 100%) na barra azul
        if (hab != null)
            for (int i = 0; i < 3; i++)
            {
                DadosDeHabilidade d = hab.Dados(i); if (d == null) continue;
                float x = be.x + be.width * Mathf.Clamp01(d.custo);
                bool pronta = hab.Disponivel(i);
                Retangulo(new Rect(x - 1.5f * esc, be.y, 3f * esc, be.height), pronta ? new Color(1f, 0.9f, 0.45f, 0.95f) : new Color(0.9f, 0.8f, 0.5f, 0.55f));
            }

        // moldura por cima de tudo
        if (moldura != null) GUI.DrawTexture(new Rect(origem.x, origem.y, TAMANHO.x * k, alturaTotal), moldura);
        else
        {
            Contorno(rr, new Color(0.6f, 0.42f, 0.16f), 4f * esc);
            Contorno(bv, new Color(0.6f, 0.42f, 0.16f), 3f * esc);
            Contorno(be, new Color(0.6f, 0.42f, 0.16f), 3f * esc);
        }

        GUI.Label(bv, Mathf.CeilToInt(vida.Atual) + " / " + Mathf.CeilToInt(vida.Maxima), estiloBarra);
        if (combate != null) GUI.Label(be, Mathf.FloorToInt(combate.EspecialFracao * 100f + 0.01f) + "%", estiloBarra);
    }

    // ---- habilidades no estilo do Fable: losango de círculos em volta de um botão central, com um visual para cada personagem
    //      esquerda = habilidade 1, em cima = 2, direita = 3 (ultimate), embaixo = energia; o losango de cima mostra quantas estão prontas
    struct EstiloPainel { public Color principal, aro2, fundo; public int adorno; }

    static EstiloPainel EstiloDe(ClasseDeHabilidade c)
    {
        switch (c)
        {
            case ClasseDeHabilidade.Cavaleiro: return new EstiloPainel { principal = new Color(1f, 0.78f, 0.36f), aro2 = new Color(0.72f, 0.55f, 0.26f), fundo = new Color(0.13f, 0.085f, 0.03f), adorno = 0 };      // ouro de cavaleiro, pontas de espada
            case ClasseDeHabilidade.Tanque: return new EstiloPainel { principal = new Color(0.46f, 0.78f, 1f), aro2 = new Color(0.3f, 0.5f, 0.7f), fundo = new Color(0.03f, 0.07f, 0.13f), adorno = 1 };               // aço azulado, rebites
            case ClasseDeHabilidade.Maga: return new EstiloPainel { principal = new Color(0.74f, 0.54f, 1f), aro2 = new Color(0.5f, 0.34f, 0.78f), fundo = new Color(0.075f, 0.03f, 0.13f), adorno = 2 };               // violeta arcano, runas girando
            default: return new EstiloPainel { principal = new Color(0.4f, 0.95f, 0.7f), aro2 = new Color(0.25f, 0.65f, 0.5f), fundo = new Color(0.02f, 0.11f, 0.08f), adorno = 3 };                                  // verde esmeralda, folhas
        }
    }

    private float auraAtual;

    private void DesenharHabilidades(float esc)
    {
        if (hab == null || vida == null || vida.Morto) return;
        if (status == null) status = GetComponent<StatusDeBatalha>();
        float W = Screen.width, H = Screen.height;
        float R = 34f * esc, d = 61f * esc;
        Vector2 c = new Vector2(W - 205f * esc, H - 235f * esc);        // canto inferior direito, com folga para a borda (igual ao Fable)
        float pct = combate != null ? combate.EspecialFracao : 0f;
        float t = Time.unscaledTime, pulso = 0.5f + 0.5f * Mathf.Sin(t * 4f);
        EstiloPainel est = EstiloDe(hab.classe);
        Color tom0 = est.principal;
        bool maximo = pct >= 0.999f;
        auraAtual = Mathf.MoveTowards(auraAtual, maximo ? 1f : 0f, Time.unscaledDeltaTime * 1.6f);

        // base escura suave para ler sobre qualquer cenário e o fio que liga os círculos
        Pintura(new Rect(c.x - (d + R) * 1.5f, c.y - (d + R) * 1.5f, (d + R) * 3f, (d + R) * 3f), IconesDeHabilidade.Brilho, new Color(0f, 0f, 0f, 0.42f));
        Pintura(new Rect(c.x - d, c.y - d, d * 2f, d * 2f), IconesDeHabilidade.Anel, new Color(est.aro2.r, est.aro2.g, est.aro2.b, 0.3f));

        // AURA do poder máximo (densa): halo duplo, três camadas de feixes girando, ondas se expandindo e muitas faíscas, na cor do painel
        if (auraAtual > 0.01f)
        {
            float tam = (d + R) * 2f;
            Color clara = new Color(Mathf.Lerp(tom0.r, 1f, 0.45f), Mathf.Lerp(tom0.g, 1f, 0.45f), Mathf.Lerp(tom0.b, 1f, 0.45f));
            Pintura(new Rect(c.x - tam * 1.15f, c.y - tam * 1.15f, tam * 2.3f, tam * 2.3f), IconesDeHabilidade.Brilho, new Color(tom0.r, tom0.g, tom0.b, (0.42f + 0.2f * pulso) * auraAtual));
            Pintura(new Rect(c.x - tam * 0.75f, c.y - tam * 0.75f, tam * 1.5f, tam * 1.5f), IconesDeHabilidade.Brilho, new Color(clara.r, clara.g, clara.b, (0.4f + 0.2f * pulso) * auraAtual));
            Matrix4x4 m0 = GUI.matrix;
            GUIUtility.RotateAroundPivot(t * 16f, c);
            Pintura(new Rect(c.x - tam * 1.0f, c.y - tam * 1.0f, tam * 2f, tam * 2f), IconesDeHabilidade.Raios, new Color(tom0.r, tom0.g, tom0.b, 0.6f * auraAtual));
            GUI.matrix = m0; GUIUtility.RotateAroundPivot(-t * 11f, c);
            Pintura(new Rect(c.x - tam * 0.8f, c.y - tam * 0.8f, tam * 1.6f, tam * 1.6f), IconesDeHabilidade.Raios, new Color(clara.r, clara.g, clara.b, 0.55f * auraAtual));
            GUI.matrix = m0; GUIUtility.RotateAroundPivot(t * 27f + 20f, c);
            Pintura(new Rect(c.x - tam * 0.6f, c.y - tam * 0.6f, tam * 1.2f, tam * 1.2f), IconesDeHabilidade.Raios, new Color(1f, 1f, 1f, 0.32f * auraAtual));
            GUI.matrix = m0;
            for (int k = 0; k < 2; k++)          // ondas de luz que se expandem
            {
                float fase = Mathf.Repeat(t * 0.55f + k * 0.5f, 1f), rr = (d + R) * (0.7f + 0.9f * fase);
                Pintura(new Rect(c.x - rr, c.y - rr, rr * 2f, rr * 2f), IconesDeHabilidade.Anel, new Color(clara.r, clara.g, clara.b, (1f - fase) * 0.6f * auraAtual));
            }
            for (int k = 0; k < 30; k++)         // faíscas subindo em volta
            {
                float fase = Mathf.Repeat(t * (0.3f + 0.12f * (k % 3)) + k * 0.0333f, 1f), ang = (k * 83f + t * 22f) * Mathf.Deg2Rad;
                float rad = (d + R * 0.7f) * (0.75f + 0.6f * fase);
                Vector2 pf = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                float sz = (7f - 4f * fase) * esc;
                Pintura(new Rect(pf.x - sz, pf.y - sz, sz * 2f, sz * 2f), IconesDeHabilidade.Brilho, new Color(1f, 0.97f, 0.88f, Mathf.Sin(fase * Mathf.PI) * 1f * auraAtual));
            }
        }

        Vector2[] pos = { c + new Vector2(-d, 0f), c + new Vector2(0f, -d), c + new Vector2(d, 0f) };
        int prontas = 0;
        for (int i = 0; i < 3; i++)
        {
            DadosDeHabilidade h = hab.Dados(i); if (h == null) continue;
            bool pronta = hab.Disponivel(i), usando = hab.IndiceEmUso == i;
            if (pronta) prontas++;
            Color tom = i == 2 ? Color.Lerp(tom0, new Color(1f, 0.86f, 0.5f), 0.55f) : tom0;
            float k2 = usando ? 1.1f : 1f;
            Rect r = new Rect(pos[i].x - R * k2, pos[i].y - R * k2, R * 2f * k2, R * 2f * k2);
            if (pronta || usando) Pintura(new Rect(r.x - R * 0.95f, r.y - R * 0.95f, r.width + R * 1.9f, r.height + R * 1.9f), IconesDeHabilidade.Brilho, new Color(tom.r, tom.g, tom.b, (0.24f + 0.2f * pulso) * (usando ? 1.6f : 1f) * (1f + 1.2f * auraAtual)));

            // vidro: escuro e colorido quando pronta; fosco acinzentado quando não
            Pintura(r, IconesDeHabilidade.Gradiente, pronta ? new Color(est.fundo.r * 2.2f, est.fundo.g * 2.2f, est.fundo.b * 2.2f, 0.96f) : new Color(0.7f, 0.76f, 0.78f, 0.34f));
            if (!pronta)
            {
                float f = Mathf.Clamp01(h.custo > 0f ? pct / h.custo : 1f);
                if (f > 0.01f) PreencherDeBaixo(r, IconesDeHabilidade.Disco, f, new Color(tom.r, tom.g, tom.b, 0.3f));
            }
            // aro interno fino
            float ri = R * 0.8f * k2; Pintura(new Rect(pos[i].x - ri, pos[i].y - ri, ri * 2f, ri * 2f), IconesDeHabilidade.Anel, new Color(est.aro2.r, est.aro2.g, est.aro2.b, pronta ? 0.55f : 0.25f));
            // ícone com um brilho atrás
            float ic = r.width * 0.6f;
            Rect rIc = new Rect(r.center.x - ic * 0.5f, r.center.y - ic * 0.5f, ic, ic);
            if (pronta) Pintura(new Rect(rIc.x - ic * 0.25f, rIc.y - ic * 0.25f, ic * 1.5f, ic * 1.5f), IconesDeHabilidade.Brilho, new Color(tom.r, tom.g, tom.b, 0.32f + 0.12f * pulso));
            Pintura(rIc, IconesDeHabilidade.Obter(hab.classe, i), pronta ? Color.Lerp(tom, Color.white, 0.78f) : new Color(0.93f, 0.96f, 0.96f, 0.6f));
            // reflexo de vidro no alto
            Pintura(r, IconesDeHabilidade.Vidro, new Color(1f, 1f, 1f, pronta ? 0.3f : 0.42f));
            // recarga: cai de cima
            float rc = hab.RecargaFracao(i);
            if (rc > 0.001f) PreencherDeCima(r, IconesDeHabilidade.Disco, rc, new Color(0f, 0f, 0f, 0.6f));
            // aro externo + adornos do personagem
            float falha = hab.FalhouHa(i);
            Color aro = usando ? Color.white : falha < 0.35f ? new Color(0.95f, 0.3f, 0.25f) : pronta ? Color.Lerp(tom, Color.white, 0.2f * pulso) : new Color(0.86f, 0.9f, 0.9f, 0.55f);
            Pintura(r, IconesDeHabilidade.Anel, aro);
            if (pronta) Pintura(new Rect(r.x - 2.5f * esc, r.y - 2.5f * esc, r.width + 5f * esc, r.height + 5f * esc), IconesDeHabilidade.Anel, new Color(tom.r, tom.g, tom.b, 0.38f + 0.3f * auraAtual));
            Adornos(pos[i], R * k2, est, pronta ? tom : new Color(0.85f, 0.9f, 0.9f, 0.5f), t, esc, i);

            // tecla: losango pequeno no canto de baixo
            Vector2 kc = new Vector2(r.xMax - R * 0.2f, r.yMax - R * 0.12f);
            Losango(kc, R * 0.2f, new Color(0.02f, 0.04f, 0.04f, 0.97f), new Color(tom.r, tom.g, tom.b, pronta ? 0.95f : 0.45f), esc);
            GUIStyle ks = new GUIStyle(estiloBarra) { fontSize = Mathf.RoundToInt(15f * esc), fontStyle = FontStyle.Bold };
            string kl = Comandos.Dica(Acao.Habilidade1 + i); if (kl.Length > 2) ks.fontSize = Mathf.RoundToInt((kl.Length > 4 ? 9f : 12f) * esc); ks.clipping = TextClipping.Overflow;
            ks.normal.textColor = pronta ? Color.white : new Color(0.8f, 0.86f, 0.86f, 0.8f); GUI.Label(new Rect(kc.x - 14f * esc, kc.y - 11f * esc, 28f * esc, 22f * esc), kl, ks);
        }

        // embaixo: a energia (círculo que enche de líquido da cor do personagem)
        {
            Rect r = new Rect(c.x - R, c.y + d - R, R * 2f, R * 2f);
            Pintura(r, IconesDeHabilidade.Gradiente, new Color(est.fundo.r * 2f, est.fundo.g * 2f, est.fundo.b * 2f, 0.95f));
            PreencherDeBaixo(r, IconesDeHabilidade.Gradiente, pct, new Color(tom0.r, tom0.g, tom0.b, 0.9f));
            float ri = R * 0.8f; Pintura(new Rect(c.x - ri, c.y + d - ri, ri * 2f, ri * 2f), IconesDeHabilidade.Anel, new Color(est.aro2.r, est.aro2.g, est.aro2.b, 0.5f));
            Pintura(r, IconesDeHabilidade.Vidro, new Color(1f, 1f, 1f, 0.32f));
            Pintura(r, IconesDeHabilidade.Anel, Color.Lerp(tom0, Color.white, 0.2f * pulso * auraAtual));
            if (maximo) Pintura(new Rect(r.x - 3f * esc, r.y - 3f * esc, r.width + 6f * esc, r.height + 6f * esc), IconesDeHabilidade.Anel, new Color(tom0.r, tom0.g, tom0.b, 0.5f + 0.4f * pulso));
            Adornos(new Vector2(c.x, c.y + d), R, est, tom0, t, esc, 3);
            GUIStyle es = new GUIStyle(estiloBarra) { fontSize = Mathf.RoundToInt(23f * esc), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow, wordWrap = false };
            string txtPct = Mathf.FloorToInt(pct * 100f + 0.01f) + "%";
            Rect rt = new Rect(c.x - R * 1.5f, c.y + d - R * 0.5f, R * 3f, R);
            es.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            for (int k = 0; k < 4; k++) GUI.Label(new Rect(rt.x + (k < 2 ? -1.3f : 1.3f), rt.y + (k % 2 == 0 ? -1.3f : 1.3f), rt.width, rt.height), txtPct, es);
            es.normal.textColor = Color.white; GUI.Label(rt, txtPct, es);
        }

        // botão central: losango com um miolo (gira devagar quando o poder está no máximo)
        Losango(c, 10f * esc, est.fundo, new Color(tom0.r, tom0.g, tom0.b, 0.85f), esc);
        Losango(c, 4.5f * esc, new Color(tom0.r, tom0.g, tom0.b, 0.5f + 0.5f * auraAtual * pulso), new Color(0f, 0f, 0f, 0f), esc);

        // losango de cima, com borda dupla: quantas habilidades estão prontas
        Vector2 cima = new Vector2(c.x, c.y - d - R - 40f * esc);
        if (maximo) Pintura(new Rect(cima.x - 40f * esc, cima.y - 40f * esc, 80f * esc, 80f * esc), IconesDeHabilidade.Brilho, new Color(tom0.r, tom0.g, tom0.b, 0.35f * auraAtual));
        Color bordaCima = prontas > 0 ? tom0 : new Color(0.8f, 0.86f, 0.86f, 0.5f);
        Losango(cima, 21f * esc, est.fundo, new Color(bordaCima.r, bordaCima.g, bordaCima.b, 0.5f), esc);
        Losango(cima, 17f * esc, new Color(est.fundo.r * 1.4f, est.fundo.g * 1.4f, est.fundo.b * 1.4f, 0.95f), bordaCima, esc);
        GUIStyle ns = new GUIStyle(estiloBarra) { fontSize = Mathf.RoundToInt(24f * esc), fontStyle = FontStyle.Bold }; ns.normal.textColor = prontas > 0 ? Color.white : new Color(0.8f, 0.86f, 0.86f, 0.7f);
        GUI.Label(new Rect(cima.x - 20f * esc, cima.y - 16f * esc, 40f * esc, 32f * esc), prontas.ToString(), ns);

        // efeitos ativos
        float ya = cima.y - 56f * esc;
        if (vida.Imortal) { Aviso(new Rect(c.x - 130f * esc, ya, 260f * esc, 32f * esc), "IMBATÍVEL  " + vida.ImortalRestante.ToString("0.0") + "s", new Color(0.4f, 0.7f, 1f), esc); ya -= 36f * esc; }
        if (status != null && status.Impulsionado) Aviso(new Rect(c.x - 130f * esc, ya, 260f * esc, 32f * esc), "IMPULSIONADO  " + Mathf.CeilToInt(status.ImpulsoRestante) + "s", new Color(1f, 0.75f, 0.25f), esc);
        GUI.color = Color.white;
    }

    // enfeites do aro de cada personagem: pontas de espada (Cavaleiro), rebites (Tank), runas girando (Maga), folhas (Curandeiro)
    private static void Adornos(Vector2 c, float R, EstiloPainel est, Color cor, float t, float esc, int semente)
    {
        Matrix4x4 m0 = GUI.matrix; Color a0 = GUI.color;
        switch (est.adorno)
        {
            case 0:     // losangos nos 4 pontos cardeais, o de cima maior
                for (int k = 0; k < 4; k++)
                {
                    float ang = k * 90f * Mathf.Deg2Rad; Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R;
                    float s = (k == 3 ? 5.5f : 3.8f) * esc;
                    GUI.matrix = m0; GUIUtility.RotateAroundPivot(45f, p); GUI.color = cor; GUI.DrawTexture(new Rect(p.x - s, p.y - s, s * 2f, s * 2f), Texture2D.whiteTexture);
                }
                break;
            case 1:     // 8 rebites quadrados
                for (int k = 0; k < 8; k++)
                {
                    float ang = (k * 45f + 22.5f) * Mathf.Deg2Rad; Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R;
                    float s = 2.6f * esc; GUI.matrix = m0; GUIUtility.RotateAroundPivot(k * 45f + 22.5f, p); GUI.color = cor; GUI.DrawTexture(new Rect(p.x - s, p.y - s, s * 2f, s * 2f), Texture2D.whiteTexture);
                }
                break;
            case 2:     // 12 pontinhos de runa, girando e cintilando
                for (int k = 0; k < 12; k++)
                {
                    float ang = (k * 30f + t * (semente % 2 == 0 ? 12f : -12f)) * Mathf.Deg2Rad; Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (R * 1.04f);
                    float tw = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 2f + k * 1.7f)); float s = 2.2f * esc * (0.7f + 0.5f * tw);
                    GUI.matrix = m0; GUI.color = new Color(cor.r, cor.g, cor.b, cor.a * tw); GUI.DrawTexture(new Rect(p.x - s, p.y - s, s * 2f, s * 2f), IconesDeHabilidade.Brilho);
                }
                break;
            default:    // 6 folhinhas inclinadas
                for (int k = 0; k < 6; k++)
                {
                    float grau = k * 60f + 30f; float ang = grau * Mathf.Deg2Rad; Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R;
                    GUI.matrix = m0; GUIUtility.RotateAroundPivot(grau + 90f + 35f, p); GUI.color = cor;
                    GUI.DrawTexture(new Rect(p.x - 1.6f * esc, p.y - 5f * esc, 3.2f * esc, 10f * esc), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, cor, Vector4.zero, new Vector4(1.6f * esc, 1.6f * esc, 1.6f * esc, 1.6f * esc));
                }
                break;
        }
        GUI.matrix = m0; GUI.color = a0;
    }

    private static void Pintura(Rect r, Texture2D tex, Color cor)
    {
        Color a = GUI.color; GUI.color = cor; GUI.DrawTexture(r, tex, ScaleMode.StretchToFill); GUI.color = a;
    }

    // enche de baixo para cima (fração 0-1), recortando a textura redonda
    private static void PreencherDeBaixo(Rect r, Texture2D tex, float f, Color cor)
    {
        f = Mathf.Clamp01(f); if (f <= 0.001f) return;
        Color a = GUI.color; GUI.color = cor;
        GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y + r.height * (1f - f), r.width, r.height * f), tex, new Rect(0f, 0f, 1f, f));
        GUI.color = a;
    }

    // cobre de cima para baixo (recarga)
    private static void PreencherDeCima(Rect r, Texture2D tex, float f, Color cor)
    {
        f = Mathf.Clamp01(f); if (f <= 0.001f) return;
        Color a = GUI.color; GUI.color = cor;
        GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, r.width, r.height * f), tex, new Rect(0f, 1f - f, 1f, f));
        GUI.color = a;
    }

    // quadrado girado 45° com borda fina
    private static void Losango(Vector2 c, float meio, Color fundo, Color borda, float esc)
    {
        Matrix4x4 m = GUI.matrix; GUIUtility.RotateAroundPivot(45f, c);
        float l = meio * 1.25f;
        Color a = GUI.color;
        Caixa(new Rect(c.x - l - 2f * esc, c.y - l - 2f * esc, (l + 2f * esc) * 2f, (l + 2f * esc) * 2f), borda, 4f * esc);
        Caixa(new Rect(c.x - l, c.y - l, l * 2f, l * 2f), fundo, 3f * esc);
        GUI.color = a; GUI.matrix = m;
    }

    private void Aviso(Rect r, string texto, Color cor, float esc)
    {
        Caixa(r, new Color(0.05f, 0.05f, 0.07f, 0.85f), 10f * esc);
        Caixa(new Rect(r.x, r.y, 6f * esc, r.height), cor, 3f * esc);
        Color antes = GUI.color; GUI.color = cor;
        GUI.Label(r, texto, new GUIStyle(estiloBarra) { fontSize = Mathf.RoundToInt(19 * esc) });
        GUI.color = antes;
    }

    private static void Caixa(Rect r, Color c, float raio)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(raio, raio, raio, raio));
    }

    private static void Preencher(Rect r, float fracao, Texture2D tex, Color cor)
    {
        if (fracao <= 0.001f) return;
        Rect f = new Rect(r.x, r.y, r.width * Mathf.Clamp01(fracao), r.height);
        if (tex != null) GUI.DrawTextureWithTexCoords(f, tex, new Rect(0f, 0f, Mathf.Clamp01(fracao), 1f));
        else Retangulo(f, cor);
    }

    private static void Retangulo(Rect r, Color c)
    {
        Color antes = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = antes;
    }

    private static void Contorno(Rect r, Color c, float e)
    {
        Retangulo(new Rect(r.x - e, r.y - e, r.width + 2 * e, e), c);
        Retangulo(new Rect(r.x - e, r.yMax, r.width + 2 * e, e), c);
        Retangulo(new Rect(r.x - e, r.y, e, r.height), c);
        Retangulo(new Rect(r.xMax, r.y, e, r.height), c);
    }
}
