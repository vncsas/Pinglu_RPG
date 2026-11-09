using UnityEngine;

// Coloque UM destes na cena. Cuida da cutscene de abertura, do objetivo na tela e dos avisos de capítulo.
// Teste rápido (só no editor): F9 avança a história, F10 volta ao começo.
public class CampanhaJogo : MonoBehaviour
{
    public Font fonteTexto, fonteTitulo;
    public bool pularAbertura = false;

    private enum Fase { Esperando, Abertura, Jogando }
    private Fase fase = Fase.Esperando;
    private int slide; private float slideInicio, fade = 1f;
    private float faixaAte; private string faixaTexto = "", faixaSub = "";
    private GUIStyle estNarra, estObj, estObjT, estFaixa, estFaixaSub; private Texture2D pixel;
    private bool menuAntes;

    private void Awake() { Campanha.Carregar(); }
    private void OnEnable() { Campanha.AoMudar += AoMudarEtapa; }
    private void OnDisable() { Campanha.AoMudar -= AoMudarEtapa; }

    private void Start() { pixel = Texture2D.whiteTexture; }

    private void AoMudarEtapa(Etapa e)
    {
        if (e == Etapa.Intro) return;
        faixaTexto = Campanha.Capitulo(e); faixaSub = Campanha.Objetivo(e); faixaAte = Time.unscaledTime + 5f;
    }

    private void Update()
    {
        // só começa depois que o menu inicial fechou e o jogador existe
        if (fase == Fase.Esperando)
        {
            if (SessaoDeJogo.MenuAberto || FindFirstObjectByType<PlayerLocomotion>() == null) return;
            if (Campanha.Atual == Etapa.Intro && !pularAbertura) { fase = Fase.Abertura; slide = 0; slideInicio = Time.unscaledTime; menuAntes = SessaoDeJogo.MenuAberto; SessaoDeJogo.MenuAberto = true; }
            else { fase = Fase.Jogando; if (Campanha.Atual == Etapa.Intro) Campanha.Ir(Etapa.FalarComCaio); else AoMudarEtapa(Campanha.Atual); }
        }
        if (fase == Fase.Abertura)
        {
            bool pula = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
            float t = Time.unscaledTime - slideInicio, total = Roteiro.Abertura[slide].Length / 38f + 2.5f;
            if (pula) { if (t < Roteiro.Abertura[slide].Length / 38f) slideInicio = -999f; else slide = ProximoSlide(); }
            else if (t > total) slide = ProximoSlide();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F9)) Campanha.Avancar();
        if (Input.GetKeyDown(KeyCode.F10)) Campanha.Recomecar();
#endif
    }

    private int ProximoSlide()
    {
        slideInicio = Time.unscaledTime;
        if (slide + 1 < Roteiro.Abertura.Length) return slide + 1;
        fase = Fase.Jogando; SessaoDeJogo.MenuAberto = menuAntes; Campanha.Ir(Etapa.FalarComCaio);
        return slide;
    }

    private void OnGUI()
    {
        float e = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(e, e, 1f));
        float W = Screen.width / e;
        if (estNarra == null)
        {
            estNarra = new GUIStyle(GUI.skin.label) { fontSize = 44, wordWrap = true, alignment = TextAnchor.MiddleCenter, font = fonteTexto }; estNarra.normal.textColor = new Color(0.95f, 0.9f, 0.78f);
            estObj = new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = true, font = fonteTexto }; estObj.normal.textColor = new Color(0.96f, 0.92f, 0.82f);
            estObjT = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, font = fonteTitulo != null ? fonteTitulo : fonteTexto }; estObjT.normal.textColor = new Color(1f, 0.8f, 0.38f);
            estFaixa = new GUIStyle(GUI.skin.label) { fontSize = 70, alignment = TextAnchor.MiddleCenter, font = fonteTitulo != null ? fonteTitulo : fonteTexto }; estFaixa.normal.textColor = new Color(1f, 0.85f, 0.45f);
            estFaixaSub = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter, wordWrap = true, font = fonteTexto }; estFaixaSub.normal.textColor = new Color(0.96f, 0.92f, 0.82f);
        }
        if (fase == Fase.Abertura)
        {
            GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, W, 1080f), pixel);
            string t = Roteiro.Abertura[slide]; float s = Time.unscaledTime - slideInicio;
            int n = Mathf.Min(t.Length, Mathf.FloorToInt(s * 38f));
            GUI.color = Color.white; GUI.Label(new Rect(W * 0.15f, 300f, W * 0.7f, 460f), t.Substring(0, n), estNarra);
            GUI.color = new Color(1f, 1f, 1f, 0.5f); GUI.Label(new Rect(W - 520f, 1000f, 480f, 40f), "[E] continuar", new GUIStyle(estObj) { fontSize = 24, alignment = TextAnchor.MiddleRight });
            GUI.color = Color.white;
            return;
        }
        if (fase != Fase.Jogando || SessaoDeJogo.MenuAberto || DialogoUI.Aberto) return;
        DesenharObjetivo(W);
        // faixa grande quando muda de capítulo
        float resta = faixaAte - Time.unscaledTime;
        if (resta > 0f)
        {
            float a = Mathf.Clamp01(Mathf.Min(resta, 5f - resta + 0.01f) / 0.8f);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0, 250f, W, 100f), faixaTexto, estFaixa);
            GUI.Label(new Rect(W * 0.2f, 350f, W * 0.6f, 120f), faixaSub, estFaixaSub);
            GUI.color = Color.white;
        }
    }

    // ------------------------------------------------------------ objetivo discreto em cima do mapa; segure V (o anel enche) para ver os detalhes
    private float progressoV, painel;                  // anel de carregamento (0 a 1) e abertura do painel (0 a 1)
    private const float TEMPO_V = 0.85f;
    private Texture2D anelTex, gradCentro;
    private GUIStyle estTexto, estResumo, estRotulo, estTecla;

    private void Construir()
    {
        anelTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
            anelTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(d - 28f) / 1.4f)));
        }
        anelTex.Apply();
        gradCentro = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 64; x++) gradCentro.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Pow(1f - Mathf.Abs(x - 31.5f) / 32f, 1.2f)));
        gradCentro.Apply();
        estTexto = new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true, font = fonteTexto, alignment = TextAnchor.UpperLeft };
        estResumo = new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true, font = fonteTexto, alignment = TextAnchor.UpperLeft };
        estRotulo = new GUIStyle(GUI.skin.label) { fontSize = 15, font = fonteTitulo != null ? fonteTitulo : fonteTexto, alignment = TextAnchor.MiddleLeft };
        estTecla = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, font = fonteTitulo != null ? fonteTitulo : fonteTexto };
    }

    // arco de círculo (começa no topo e gira para a direita), feito de pedacinhos
    private void Arco(Vector2 c, float raio, float fracao, float esp, Color cor)
    {
        const int N = 64;
        int n = Mathf.CeilToInt(fracao * N);
        float len = 2f * Mathf.PI * raio / N + 1.2f;
        for (int i = 0; i < n; i++)
        {
            float ang = (i + 0.5f) / N * 360f - 90f;
            float rad = ang * Mathf.Deg2Rad;
            Vector2 p = c + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * raio;
            Matrix4x4 m0 = GUI.matrix; GUIUtility.RotateAroundPivot(ang + 90f, p);
            GUI.color = cor; GUI.DrawTexture(new Rect(p.x - len * 0.5f, p.y - esp * 0.5f, len, esp), pixel);
            GUI.matrix = m0;
        }
    }

    private Texture2D gLado2, gFim;
    private Texture2D gradLado2() { if (gLado2 == null) { gLado2 = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; for (int x = 0; x < 64; x++) gLado2.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - x / 63f)); gLado2.Apply(); } return gLado2; }
    private Texture2D gradFim() { if (gFim == null) { gFim = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; for (int x = 0; x < 64; x++) gFim.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - Mathf.Pow(x / 63f, 1.6f))); gFim.Apply(); } return gFim; }

    private void Contorno(Rect r, float e, Color cor)
    {
        GUI.color = cor;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, e), pixel); GUI.DrawTexture(new Rect(r.x, r.yMax - e, r.width, e), pixel);
        GUI.DrawTexture(new Rect(r.x, r.y, e, r.height), pixel); GUI.DrawTexture(new Rect(r.xMax - e, r.y, e, r.height), pixel);
    }

    // contorno quadrado que vai enchendo: começa no meio de cima e gira para a direita
    private void ArcoQuadrado(Rect r, float fracao, float e, Color cor)
    {
        float L = r.width, resta = fracao * L * 4f;
        GUI.color = cor;
        float d;
        d = Mathf.Min(resta, L * 0.5f); GUI.DrawTexture(new Rect(r.center.x, r.y, d, e), pixel); resta -= d; if (resta <= 0f) return;                       // topo, direita
        d = Mathf.Min(resta, L); GUI.DrawTexture(new Rect(r.xMax - e, r.y, e, d), pixel); resta -= d; if (resta <= 0f) return;                              // lado direito, descendo
        d = Mathf.Min(resta, L); GUI.DrawTexture(new Rect(r.xMax - d, r.yMax - e, d, e), pixel); resta -= d; if (resta <= 0f) return;                       // embaixo, indo para a esquerda
        d = Mathf.Min(resta, L); GUI.DrawTexture(new Rect(r.x, r.yMax - d, e, d), pixel); resta -= d; if (resta <= 0f) return;                              // lado esquerdo, subindo
        d = Mathf.Min(resta, L * 0.5f); GUI.DrawTexture(new Rect(r.x, r.y, d, e), pixel);                                                                   // topo, esquerda até o meio
    }

    private void Losango(Vector2 c, float tam, Color cor)
    {
        Matrix4x4 m0 = GUI.matrix; GUIUtility.RotateAroundPivot(45f, c);
        GUI.color = cor; GUI.DrawTexture(new Rect(c.x - tam * 0.5f, c.y - tam * 0.5f, tam, tam), pixel);
        GUI.matrix = m0;
    }

    private void Quina(float x, float y, int sx, int sy, float len, Color cor)
    {
        GUI.color = cor;
        GUI.DrawTexture(new Rect(sx > 0 ? x : x - len, y - (sy > 0 ? 0f : 2f), len, 2f), pixel);
        GUI.DrawTexture(new Rect(x - (sx > 0 ? 0f : 2f), sy > 0 ? y : y - len, 2f, len), pixel);
    }

    private void DesenharObjetivo(float W)
    {
        string obj = Campanha.Objetivo(Campanha.Atual);
        if (string.IsNullOrEmpty(obj)) return;
        if (anelTex == null) Construir();
        bool segurando = Input.GetKey(KeyCode.V);
        float dt = Time.unscaledDeltaTime;
        progressoV = segurando ? Mathf.Min(1f, progressoV + dt / TEMPO_V) : Mathf.Max(0f, progressoV - dt * 3f);
        bool cheio = segurando && progressoV >= 1f;
        painel = Mathf.MoveTowards(painel, cheio ? 1f : 0f, dt * 5f);
        float tempo = Time.unscaledTime;
        Color ouro = new Color(0.86f, 0.68f, 0.34f), ouroFraco = new Color(0.86f, 0.68f, 0.34f, 0.55f);
        float x0 = 34f, ya = 600f;

        // ---- compacto: botão V com anel de carregamento + texto discreto
        Vector2 cb = new Vector2(x0 + 22f, ya + 24f);
        float raio = 22f;                                   // metade do lado do botão quadrado
        Rect quad = new Rect(cb.x - raio, cb.y - raio, raio * 2f, raio * 2f);
        GUI.color = new Color(0.07f, 0.055f, 0.04f, 0.78f); GUI.DrawTexture(quad, pixel);
        Contorno(quad, 2f, new Color(ouro.r, ouro.g, ouro.b, 0.4f));
        if (progressoV > 0f) ArcoQuadrado(quad, progressoV, 3.5f, Color.Lerp(ouro, new Color(1f, 0.93f, 0.7f), progressoV));
        if (cheio && painel < 1f) Contorno(new Rect(quad.x - 6f, quad.y - 6f, quad.width + 12f, quad.height + 12f), 2f, new Color(1f, 0.9f, 0.6f, 1f - painel));
        estTecla.normal.textColor = Color.Lerp(new Color(0.9f, 0.84f, 0.7f), Color.white, progressoV);
        GUI.color = Color.white; GUI.Label(quad, "V", estTecla);

        float tx = x0 + 62f, tw = Mathf.Min(430f, W - tx - 24f);
        // rótulo pequeno e linha fina que some nas pontas
        estRotulo.normal.textColor = new Color(ouro.r, ouro.g, ouro.b, 0.85f);
        GUI.color = Color.white; GUI.Label(new Rect(tx, ya - 4f, 300f, 20f), "M I S S Ã O", estRotulo);
        GUI.color = new Color(ouro.r, ouro.g, ouro.b, 0.55f); GUI.DrawTexture(new Rect(tx, ya + 15f, tw * 0.9f, 1.5f), gradCentro);
        Rect rt = new Rect(tx, ya + 19f, tw, 64f);
        estTexto.normal.textColor = new Color(0f, 0f, 0f, 0.75f); GUI.color = Color.white; GUI.Label(new Rect(rt.x + 1.5f, rt.y + 1.5f, rt.width, rt.height), obj, estTexto);
        estTexto.normal.textColor = new Color(0.94f, 0.91f, 0.84f, 0.92f); GUI.Label(rt, obj, estTexto);
        GUIStyle dica = new GUIStyle(estRotulo) { fontSize = 13 }; dica.normal.textColor = new Color(0.8f, 0.77f, 0.7f, 0.6f * (1f - progressoV));
        GUI.Label(new Rect(x0 + 4f, cb.y + raio + 4f, 70f, 16f), "segure", dica);

        // ---- painel: só a descrição da missão atual (altura calculada pelo texto, nada fica cortado)
        if (painel > 0.01f)
        {
            float pa = Mathf.SmoothStep(0f, 1f, painel);
            float pw = Mathf.Min(500f, W - 2f * x0), padX = 30f, padY = 22f;
            float larg = pw - padX - 24f;
            string resumo = Campanha.Resumo(Campanha.Atual);
            string titulo = Campanha.Capitulo(Campanha.Atual);
            estObjT.fontSize = 22; estObjT.wordWrap = true; estObjT.alignment = TextAnchor.UpperLeft;
            estResumo.fontSize = 22;
            float hTit = estObjT.CalcHeight(new GUIContent(titulo), larg);
            float hTxt = estResumo.CalcHeight(new GUIContent(resumo), larg);
            float ph = padY + hTit + 14f + 12f + hTxt + padY;
            float px = x0, py = ya - ph - 20f + (1f - pa) * 12f;
            // fundo escuro com faixa dourada na esquerda (sem quinas nem enfeites soltos)
            GUI.color = new Color(0.035f, 0.03f, 0.028f, 0.84f * pa); GUI.DrawTexture(new Rect(px, py, pw, ph), pixel);
            GUI.color = new Color(ouro.r, ouro.g, ouro.b, 0.09f * pa); GUI.DrawTexture(new Rect(px, py, pw, ph), gradLado2());
            GUI.color = new Color(ouro.r, ouro.g, ouro.b, 0.95f * pa); GUI.DrawTexture(new Rect(px, py, 3f, ph), pixel);
            GUI.color = new Color(ouro.r, ouro.g, ouro.b, 0.25f * pa);
            GUI.DrawTexture(new Rect(px, py, pw, 1f), pixel); GUI.DrawTexture(new Rect(px, py + ph - 1f, pw, 1f), pixel); GUI.DrawTexture(new Rect(px + pw - 1f, py, 1f, ph), pixel);
            Losango(new Vector2(px + 1.5f, py + padY + 11f), 9f, new Color(1f, 0.86f, 0.5f, pa));
            estObjT.normal.textColor = new Color(1f, 0.84f, 0.46f, pa);
            GUI.color = Color.white; GUI.Label(new Rect(px + padX, py + padY, larg, hTit + 4f), titulo, estObjT);
            float dy = py + padY + hTit + 8f;
            GUI.color = new Color(ouro.r, ouro.g, ouro.b, 0.75f * pa); GUI.DrawTexture(new Rect(px + padX, dy, larg * 0.9f, 1.5f), gradFim());
            float varre = Mathf.Repeat(tempo * 0.35f, 1f);
            GUI.color = new Color(1f, 0.95f, 0.75f, 0.5f * pa * Mathf.Sin(varre * Mathf.PI)); GUI.DrawTexture(new Rect(px + padX + larg * 0.9f * varre - 16f, dy - 0.5f, 32f, 2.5f), gradCentro);
            estResumo.normal.textColor = new Color(0.93f, 0.9f, 0.82f, pa);
            GUI.color = Color.white; GUI.Label(new Rect(px + padX, dy + 12f, larg, hTxt + 6f), resumo, estResumo);
        }
        GUI.color = Color.white;
    }
}
