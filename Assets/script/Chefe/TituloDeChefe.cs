using System.Collections.Generic;
using UnityEngine;

// Letreiro de apresentação do chefe: faixa escura embaixo, ornamentos que se abrem, as letras do nome
// caem uma a uma como pedra (cada batida treme a câmera e solta faíscas) e o epíteto aparece espaçado embaixo.
// Tudo desenhado por código; o estilo vem de um ApresentacaoDoChefe.
public class TituloDeChefe : MonoBehaviour
{
    private static TituloDeChefe inst;
    public static bool Ativo { get { return inst != null && inst.ativo; } }

    private struct Faisca { public Vector2 p, v; public float vida, max, tam; }

    private ApresentacaoDoChefe ap;
    private bool ativo;
    private float t0;
    private Font fonte;
    private GUIStyle st;
    private Texture2D gradiente;
    private AudioSource som; private AudioClip batida;
    private readonly List<Faisca> faiscas = new List<Faisca>();
    private float[] centrosX; private float baseY, larguraTotal, xIni;
    private int anunciadas;
    private const float PASSO = 0.085f, ENTRADA = 0.26f, INICIO = 0.7f;

    public static void Mostrar(ApresentacaoDoChefe a)
    {
        if (inst == null) { GameObject g = new GameObject("Título do chefe"); inst = g.AddComponent<TituloDeChefe>(); }
        inst.Iniciar(a);
    }

    public static void Esconder() { if (inst != null) inst.ativo = false; }

    // tarjas pretas de cinema (em cima e embaixo)
    private float barras, barrasAlvo;
    public static void Barras(bool ligar)
    {
        if (inst == null) { GameObject g = new GameObject("Título do chefe"); inst = g.AddComponent<TituloDeChefe>(); }
        inst.barrasAlvo = ligar ? 1f : 0f;
    }

    private void Iniciar(ApresentacaoDoChefe a)
    {
        ap = a; ativo = true; t0 = Time.unscaledTime; anunciadas = 0; faiscas.Clear(); centrosX = null; st = null;
        fonte = Resources.Load<Font>(a.fonte);
        if (som == null) { som = gameObject.AddComponent<AudioSource>(); som.spatialBlend = 0f; som.playOnAwake = false; batida = GerarBatida(); }
    }

    private float Atraso(int indice) { return INICIO + indice * PASSO; }

    private int QuantasLetras() { int n = 0; foreach (char c in ap.nome) if (c != ' ') n++; return n; }

    private void Update()
    {
        barras = Mathf.MoveTowards(barras, barrasAlvo, Time.unscaledDeltaTime * 1.3f);
        if (!ativo || ap == null) return;
        float t = Time.unscaledTime - t0;
        if (t > ap.duracao) { ativo = false; return; }
        float dt = Time.unscaledDeltaTime;
        // batidas: cada letra que "cai" faz um estrondo, treme a câmera e solta faíscas
        int caidas = 0;
        for (int i = 0; i < QuantasLetras(); i++) if (t >= Atraso(i) + ENTRADA * 0.75f) caidas = i + 1;
        while (anunciadas < caidas)
        {
            if (ap.animacao == AnimacaoDoTitulo.Estampar)
            {
                TremorDeCamera.Tremer(0.06f, 0.18f);
                if (som != null && batida != null) som.PlayOneShot(batida, 0.75f);
                if (centrosX != null)
                {
                    int vis = 0; float cx = Screen.width * 0.5f;
                    int idxChar = 0; foreach (char c in ap.nome) { if (c != ' ') { if (vis == anunciadas) break; vis++; } idxChar++; }
                    if (idxChar < centrosX.Length) cx = centrosX[idxChar];
                    for (int k = 0; k < 9; k++) Soltar(new Vector2(cx + Random.Range(-20f, 20f), baseY + Random.Range(-10f, 40f)), 1f);
                }
            }
            anunciadas++;
        }
        // brasas subindo do letreiro
        if (centrosX != null && t > INICIO)
        {
            float e = Screen.height / 1080f;
            int quantas = Mathf.RoundToInt(dt * 38f + Random.value);
            for (int i = 0; i < quantas; i++) Soltar(new Vector2(xIni + Random.value * larguraTotal, baseY + Random.Range(-30f, 40f) * e), 0.5f);
        }
        for (int i = faiscas.Count - 1; i >= 0; i--)
        {
            Faisca f = faiscas[i]; f.vida -= dt;
            if (f.vida <= 0f) { faiscas.RemoveAt(i); continue; }
            f.p += f.v * dt; f.v.x += Mathf.Sin(Time.unscaledTime * 6f + i) * 30f * dt; faiscas[i] = f;
        }
    }

    private void Soltar(Vector2 p, float forca)
    {
        float e = Screen.height / 1080f;
        float vida = Random.Range(0.9f, 2.0f);
        faiscas.Add(new Faisca { p = p, v = new Vector2(Random.Range(-60f, 60f), -Random.Range(70f, 230f) * forca) * e, vida = vida, max = vida, tam = Random.Range(2f, 6f) * e });
    }

    private static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (barras > 0.002f)
        {
            float hb = Screen.height * 0.115f * Mathf.SmoothStep(0f, 1f, barras);
            GUI.depth = -40; GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, hb), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0f, Screen.height - hb, Screen.width, hb), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        if (!ativo || ap == null) return;
        float t = Time.unscaledTime - t0;
        float W = Screen.width, H = Screen.height, e = H / 1080f;
        if (st == null)
        {
            st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            st.font = fonte;
        }
        if (gradiente == null)
        {
            gradiente = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) { float k = i / 63f; gradiente.SetPixel(0, i, new Color(0f, 0f, 0f, Mathf.Pow(1f - k, 1.6f) * 0.82f)); }
            gradiente.Apply();
        }
        float gA = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((ap.duracao - t) / 0.9f);
        Color brilho = ap.corBrilho, letra = ap.corLetra;

        // escurece a parte de baixo para o letreiro aparecer bem em qualquer cena
        GUI.color = new Color(1f, 1f, 1f, gA);
        GUI.DrawTexture(new Rect(0f, H * 0.5f, W, H * 0.5f), gradiente);
        GUI.color = Color.white;

        // tamanho do texto: cabe em 80% da largura
        int tam = Mathf.RoundToInt(128f * e * ap.tamanho);
        st.fontSize = tam;
        string nome = ap.nome;
        float[] larg = new float[nome.Length]; float total = 0f; float trilho = tam * 0.035f;
        for (int i = 0; i < nome.Length; i++) { larg[i] = nome[i] == ' ' ? tam * 0.3f : st.CalcSize(new GUIContent(nome[i].ToString())).x; total += larg[i] + trilho; }
        if (total > W * 0.82f) { float f = W * 0.82f / total; tam = Mathf.RoundToInt(tam * f); st.fontSize = tam; total = 0f; trilho = tam * 0.035f; for (int i = 0; i < nome.Length; i++) { larg[i] = nome[i] == ' ' ? tam * 0.3f : st.CalcSize(new GUIContent(nome[i].ToString())).x; total += larg[i] + trilho; } }
        float altura = tam * 1.3f;
        float xAtual = (W - total) * 0.5f; xIni = xAtual; larguraTotal = total;
        float yNome = H * 0.775f; baseY = yNome;
        if (centrosX == null || centrosX.Length != nome.Length) centrosX = new float[nome.Length];

        // ornamento de cima: linha que se abre do centro, com losango
        float abre = Suave((t - 0.05f) / 0.8f);
        DesenharOrnamento(W * 0.5f, yNome - altura * 0.62f, W * 0.40f * abre, e, brilho, gA, false);

        int vis = 0; float fimLetras = 0f;
        for (int i = 0; i < nome.Length; i++)
        {
            float cx = xAtual + larg[i] * 0.5f; centrosX[i] = cx; xAtual += larg[i] + trilho;
            if (nome[i] == ' ') continue;
            float atraso = Atraso(vis); vis++;
            fimLetras = atraso + ENTRADA;
            float q = t - atraso; if (q < 0f) continue;
            float p = Mathf.Clamp01(q / ENTRADA), inv = 1f - p;
            float escala, yo, a;
            if (ap.animacao == AnimacaoDoTitulo.Estampar) { escala = 1f + inv * inv * 1.3f; yo = -inv * inv * 80f * e; a = Mathf.Clamp01(p * 1.6f) * gA; }
            else { escala = 1f; yo = inv * inv * 40f * e; a = p * gA; }
            float treme = ap.animacao == AnimacaoDoTitulo.Estampar && q > ENTRADA ? Mathf.Exp(-(q - ENTRADA) * 9f) : 0f;
            float dx = Mathf.Sin(t * 90f + i * 3.1f) * treme * 6f * e, dy = Mathf.Cos(t * 83f + i * 1.7f) * treme * 5f * e;
            float flash = Mathf.Exp(-Mathf.Max(0f, q - ENTRADA * 0.7f) * 5f);
            float pulso = 0.5f + 0.5f * Mathf.Sin(t * 2.2f + i * 0.7f);
            Rect r = new Rect(cx - larg[i] * 0.5f - tam * 0.2f + dx, yNome - altura * 0.5f + yo + dy, larg[i] + tam * 0.4f, altura);
            Matrix4x4 antes = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(escala, escala), r.center);
            string ch = nome[i].ToString();
            // brilho em volta (mais forte na hora da batida)
            float raio = (4f + 8f * flash) * e;
            for (int k = 0; k < 8; k++)
            {
                float ang = k * Mathf.PI * 0.25f;
                Letra(ch, new Rect(r.x + Mathf.Cos(ang) * raio, r.y + Mathf.Sin(ang) * raio, r.width, r.height), new Color(brilho.r, brilho.g, brilho.b, (0.07f + 0.08f * pulso + 0.38f * flash) * a));
            }
            Letra(ch, new Rect(r.x + 5f * e, r.y + 6f * e, r.width, r.height), new Color(0f, 0f, 0f, 0.9f * a));                    // sombra
            Letra(ch, new Rect(r.x + 2.5f * e, r.y + 3f * e, r.width, r.height), new Color(letra.r * 0.25f, letra.g * 0.22f, letra.b * 0.2f, a));   // lado escuro (relevo)
            Letra(ch, new Rect(r.x - 2f * e, r.y - 2f * e, r.width, r.height), new Color(Mathf.Lerp(letra.r, 1f, 0.55f), Mathf.Lerp(letra.g, 1f, 0.55f), Mathf.Lerp(letra.b, 1f, 0.5f), 0.9f * a));   // lado claro
            Color corTopo = Color.Lerp(letra, new Color(1f, 0.92f, 0.8f), flash * 0.7f);
            Letra(ch, r, new Color(corTopo.r, corTopo.g, corTopo.b, a));
            Letra(ch, r, new Color(brilho.r, brilho.g, brilho.b, (0.10f + 0.14f * pulso) * a));                                       // rachaduras de brasa
            GUI.matrix = antes;
        }

        // epíteto: aparece depois do nome, com as letras se aproximando
        if (!string.IsNullOrEmpty(ap.epiteto))
        {
            float tE = fimLetras + 0.3f;
            float pe = Suave((t - tE) / 1.1f);
            if (pe > 0f)
            {
                int tam2 = Mathf.RoundToInt(tam * 0.3f); st.fontSize = tam2;
                string ep = ap.epiteto;
                float esp = tam2 * Mathf.Lerp(0.55f, 0.12f, pe);
                float[] l2 = new float[ep.Length]; float tot2 = 0f;
                for (int i = 0; i < ep.Length; i++) { l2[i] = ep[i] == ' ' ? tam2 * 0.35f : st.CalcSize(new GUIContent(ep[i].ToString())).x; tot2 += l2[i] + esp; }
                float x2 = (W - tot2) * 0.5f, y2 = yNome + altura * 0.58f, h2 = tam2 * 1.4f, a2 = pe * gA;
                for (int i = 0; i < ep.Length; i++)
                {
                    Rect r2 = new Rect(x2 - tam2 * 0.2f, y2 - h2 * 0.5f, l2[i] + tam2 * 0.4f, h2); x2 += l2[i] + esp;
                    if (ep[i] == ' ') continue;
                    string c2 = ep[i].ToString();
                    Letra(c2, new Rect(r2.x + 2f * e, r2.y + 2.5f * e, r2.width, r2.height), new Color(0f, 0f, 0f, 0.9f * a2));
                    Letra(c2, r2, new Color(0.88f, 0.84f, 0.78f, a2));
                }
                DesenharOrnamento(W * 0.5f, y2 + h2 * 0.62f, W * 0.22f * pe, e, brilho, gA * pe, true);
            }
        }

        // faíscas
        foreach (Faisca f in faiscas)
        {
            float k = f.vida / f.max;
            GUI.color = new Color(brilho.r, Mathf.Lerp(brilho.g, 0.85f, 1f - k), 0.15f, Mathf.Clamp01(k * 1.6f) * gA);
            GUI.DrawTexture(new Rect(f.p.x, f.p.y, f.tam, f.tam), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private void Letra(string c, Rect r, Color cor) { st.normal.textColor = cor; GUI.Label(r, c, st); }

    // linha fina que se abre do centro, com um losango no meio e pontas
    private void DesenharOrnamento(float cx, float y, float meia, float e, Color cor, float a, bool pequeno)
    {
        if (meia < 1f) return;
        float esp = Mathf.Max(1.5f, (pequeno ? 1.5f : 2.5f) * e);
        GUI.color = new Color(cor.r, cor.g, cor.b, 0.85f * a);
        GUI.DrawTexture(new Rect(cx - meia, y - esp * 0.5f, meia - 14f * e, esp), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + 14f * e, y - esp * 0.5f, meia - 14f * e, esp), Texture2D.whiteTexture);
        float d = (pequeno ? 7f : 11f) * e;
        Matrix4x4 antes = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, new Vector2(cx, y));
        GUI.DrawTexture(new Rect(cx - d * 0.5f, y - d * 0.5f, d, d), Texture2D.whiteTexture);
        GUI.matrix = antes;
        GUI.DrawTexture(new Rect(cx - meia - 2f, y - esp * 1.5f, 3f * e, esp * 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + meia - 1f * e, y - esp * 1.5f, 3f * e, esp * 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static AudioClip GerarBatida()
    {
        int sr = 22050, n = (int)(sr * 0.5f); float[] d = new float[n];
        System.Random rnd = new System.Random(21); float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, w = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * 0.18f;
            float s = Mathf.Sin(t * Mathf.PI * 2f * (62f - 20f * t)) * Mathf.Exp(-t * 11f) * 0.95f + lp * Mathf.Exp(-t * 30f) * 0.5f;
            d[i] = Mathf.Clamp(s * Mathf.Clamp01(t / 0.003f), -1f, 1f);
        }
        AudioClip c = AudioClip.Create("BatidaDePedra", n, 1, sr, false); c.SetData(d, 0); return c;
    }
}
