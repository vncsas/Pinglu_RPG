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
    private float vidaMostrada = 1f, vidaAtrasada = 1f, especialMostrado;
    private GUIStyle estiloTexto, estiloGrande, estiloMenor, estiloBarra;

    private Camera camRetrato;
    private RenderTexture rtRetrato;
    private Transform cabeca;

    public static void Mostrar(string texto, float segundos)
    {
        mensagem = texto;
        mensagemAte = Time.time + segundos;
    }

    private void Awake()
    {
        vida = GetComponent<Vida>();
        combate = GetComponent<CombateJogador>();
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

        if (vida != null && vida.Morto && Input.GetKeyDown(KeyCode.R))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void CriarEstilos()
    {
        float esc = Screen.height / 1080f;
        estiloTexto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * esc), fontStyle = FontStyle.Bold };
        estiloTexto.normal.textColor = new Color(0.95f, 0.88f, 0.72f);
        estiloGrande = new GUIStyle(estiloTexto) { fontSize = Mathf.RoundToInt(54 * esc), alignment = TextAnchor.MiddleCenter };
        estiloMenor = new GUIStyle(estiloTexto) { fontSize = Mathf.RoundToInt(19 * esc), fontStyle = FontStyle.Normal };
        estiloBarra = new GUIStyle(estiloMenor) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
    }

    private void OnGUI()
    {
        if (estiloTexto == null || Event.current.type == EventType.Layout) CriarEstilos();
        float esc = Screen.height / 1080f;

        DesenharMoldura(esc);

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

        // ---- tela de morte
        if (vida != null && vida.Morto)
        {
            Retangulo(new Rect(0, 0, Screen.width, Screen.height), new Color(0.15f, 0f, 0f, 0.45f));
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 90 * esc), "VOCÊ MORREU", estiloGrande);
            string onda = g != null && g.OndaAtual > 0 ? "Você chegou até a onda " + g.OndaAtual + ". " : "";
            GUIStyle centro = new GUIStyle(estiloMenor) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.4f + 90 * esc, Screen.width, 40 * esc), onda + "Aperte R para tentar de novo.", centro);
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

        // moldura por cima de tudo
        if (moldura != null) GUI.DrawTexture(new Rect(origem.x, origem.y, TAMANHO.x * k, alturaTotal), moldura);
        else
        {
            Contorno(rr, new Color(0.6f, 0.42f, 0.16f), 4f * esc);
            Contorno(bv, new Color(0.6f, 0.42f, 0.16f), 3f * esc);
            Contorno(be, new Color(0.6f, 0.42f, 0.16f), 3f * esc);
        }

        GUI.Label(bv, Mathf.CeilToInt(vida.Atual) + " / " + Mathf.CeilToInt(vida.Maxima), estiloBarra);
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
