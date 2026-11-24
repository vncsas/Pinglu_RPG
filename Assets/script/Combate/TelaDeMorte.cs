using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Tela de morte, montada como nos jogos grandes (Dark Souls / Elden Ring / Bloodborne / Sekiro):
//  - o mundo NÃO é pintado de vermelho: a imagem do jogo é "corrigida" por pós-processamento (cor some, escurece, vinheta, aberração, grão, leve desfoque);
//  - câmera lenta no instante da morte (o jogo desacelera e volta ao normal) e o som abafa (filtro passa-baixa) com uma pancada grave;
//  - a mensagem entra devagar, em letras de inscrição que se afastam umas das outras, com brilho vermelho e um tremor de cor logo no começo;
//  - uma ESCURIDÃO de fumaça (shader) nasce nas bordas e se fecha até o centro, sem nenhum sangue;
//  - o texto de apertar R fica numa placa escura para sempre dar para ler.
// Linha do tempo: 0 s impacto | 0-3,5 s a escuridão se fecha | 1,3 s título | 3,2 s "Pressione R" (R só vale depois de 2,2 s).
public static class TelaDeMorte
{
    static float inicio = -1f, sairEm = -1f, entrarEm = -1f;
    static bool executou; static Action acao;
    static Texture2D vinheta, faixa;
    static Material matEscuridao; static Shader shaderEscuridao;
    static Font fonteTitulo, fonteTexto;
    static AudioClip som;

    // pós-processamento
    static Volume volume; static ColorAdjustments cores; static Vignette vinhetaPP; static ChromaticAberration aberracao; static FilmGrain grao; static DepthOfField desfoque;
    static AudioLowPassFilter abafar;
    static float seguraR; const float TEMPO_R = 0.85f;      // segurar R (como o V da missão) por 0,85 s

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { inicio = sairEm = entrarEm = -1f; executou = false; acao = null; volume = null; abafar = null; }

    public static bool Ativa { get { return inicio >= 0f; } }
    public static float Tempo { get { return inicio < 0f ? 0f : Time.unscaledTime - inicio; } }
    public static bool PodeRenascer { get { return inicio >= 0f && sairEm < 0f && Tempo > 2.2f; } }

    static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    static float Saida(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

    // ------------------------------------------------------------------ controle (todo quadro, pelo HUD)
    public static void Atualizar(bool morto, bool online, Action aoCompletar)
    {
        if (morto && inicio < 0f && sairEm < 0f) Iniciar();
        if (!morto && inicio >= 0f && sairEm < 0f) Terminar();           // reviveu por outro caminho (online)

        if (inicio >= 0f)
        {
            float t = Tempo;
            if (!online) Time.timeScale = Mathf.Lerp(0.15f, 1f, Suave((t - 0.25f) / 2.4f));          // câmera lenta que volta ao normal
            AplicarPos(t);
            if (abafar != null) abafar.cutoffFrequency = Mathf.Lerp(22000f, 650f, Suave(t / 1.0f));
        }
        if (inicio >= 0f && sairEm < 0f && !online)          // segurar R enche o quadrado; cheio, renasce
        {
            if (PodeRenascer && Comandos.Segurando(Acao.Reviver)) seguraR = Mathf.Min(1f, seguraR + Time.unscaledDeltaTime / TEMPO_R);
            else seguraR = Mathf.Max(0f, seguraR - Time.unscaledDeltaTime * 3f);
            if (seguraR >= 1f) PedirRenascer(aoCompletar);
        }
        if (sairEm >= 0f)
        {
            if (volume != null) volume.weight = Mathf.Max(0f, volume.weight - Time.unscaledDeltaTime * 1.6f);
            if (!executou && Time.unscaledTime - sairEm > 0.55f)
            {
                executou = true;
                Terminar();
                if (acao != null) acao();
                entrarEm = Time.unscaledTime; sairEm = -1f; acao = null;
            }
        }
    }

    public static void PedirRenascer(Action oQueFazer)
    {
        if (!PodeRenascer) return;
        acao = oQueFazer; sairEm = Time.unscaledTime; executou = false;
    }

    static void Terminar()
    {
        inicio = -1f; seguraR = 0f; Time.timeScale = 1f;
        if (volume != null) volume.weight = 0f;
        if (abafar != null) { UnityEngine.Object.Destroy(abafar); abafar = null; }
    }

    static void Iniciar()
    {
        inicio = Time.unscaledTime; executou = false; seguraR = 0f;
        if (fonteTitulo == null) fonteTitulo = Resources.Load<Font>("Fontes/CinzelBold");
        if (fonteTexto == null) fonteTexto = Resources.Load<Font>("Fontes/IMFellEnglish");
        if (vinheta == null) GerarImagens();
        PrepararPos();
        if (som == null) som = GerarSom();
        Camera c = Camera.main; if (c != null && som != null) AudioSource.PlayClipAtPoint(som, c.transform.position, AplicadorDeConfiguracoes.FatorDeEfeitos);
        AudioListener ouvinte = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
        if (ouvinte != null) { abafar = ouvinte.GetComponent<AudioLowPassFilter>(); if (abafar == null) abafar = ouvinte.gameObject.AddComponent<AudioLowPassFilter>(); abafar.cutoffFrequency = 22000f; }
    }

    // ------------------------------------------------------------------ pós-processamento (como os jogos grandes fazem)
    static void PrepararPos()
    {
        if (volume != null) return;
        GameObject g = new GameObject("Efeito de morte"); UnityEngine.Object.DontDestroyOnLoad(g);
        volume = g.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 500f; volume.weight = 0f;
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        cores = perfil.Add<ColorAdjustments>(true);
        cores.saturation.value = -92f; cores.postExposure.value = -1.1f; cores.contrast.value = 28f; cores.colorFilter.value = new Color(0.9f, 0.92f, 1f);
        vinhetaPP = perfil.Add<Vignette>(true);
        vinhetaPP.intensity.value = 0.62f; vinhetaPP.smoothness.value = 0.55f; vinhetaPP.color.value = new Color(0.01f, 0f, 0.02f); vinhetaPP.rounded.value = false;
        aberracao = perfil.Add<ChromaticAberration>(true); aberracao.intensity.value = 0.5f;
        grao = perfil.Add<FilmGrain>(true); grao.type.value = FilmGrainLookup.Thin2; grao.intensity.value = 0.55f; grao.response.value = 0.6f;
        desfoque = perfil.Add<DepthOfField>(true); desfoque.mode.value = DepthOfFieldMode.Gaussian; desfoque.gaussianStart.value = 1.5f; desfoque.gaussianEnd.value = 9f; desfoque.gaussianMaxRadius.value = 1.4f;
        volume.sharedProfile = perfil;
    }

    static void AplicarPos(float t)
    {
        if (volume == null) return;
        volume.weight = Suave(t / 2.2f);
        if (aberracao != null) aberracao.intensity.value = 0.45f + 0.55f * Mathf.Clamp01(1f - t / 0.8f);      // estoura no impacto e acalma
    }

    // ------------------------------------------------------------------ desenho
    public static void Desenhar(float esc, bool online, string linhaExtra)
    {
        if (Event.current.type != EventType.Repaint) return;
        float W = Screen.width, H = Screen.height;
        Color antes = GUI.color;
        if (inicio >= 0f)
        {
            float t = Tempo;
            float escurece = Suave(t / 1.6f);

            // escurece o conjunto e fecha as bordas (preto avermelhado, o centro fica limpo)
            Pintar(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.18f * escurece));
            float batida = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            GUI.color = new Color(0.01f, 0f, 0.02f, escurece * (0.55f + 0.15f * batida)); GUI.DrawTexture(new Rect(0, 0, W, H), vinheta, ScaleMode.StretchToFill);
            DesenharEscuridao(t, W, H);

            // faixa suave atrás do título
            float fa = Suave((t - 1.0f) / 1.4f);
            float fh = H * 0.34f, fy = H * 0.45f - fh * 0.5f;
            GUI.color = new Color(0f, 0f, 0f, 0.82f * fa); GUI.DrawTexture(new Rect(0, fy, W, fh), faixa, ScaleMode.StretchToFill);

            // título
            float ta = Suave((t - 1.3f) / 2.2f);
            if (ta > 0f)
            {
                float u = t - 1.3f;
                float tam = 112f * esc;
                float espaco = tam * Mathf.Lerp(0.10f, 0.27f, Saida(u / 7f));                  // as letras se afastam devagar
                float zoom = 1f + 0.055f * Suave(u / 9f);
                Vector2 piv = new Vector2(W * 0.5f, H * 0.45f);
                Matrix4x4 m = GUI.matrix; GUIUtility.ScaleAroundPivot(new Vector2(zoom, zoom), piv);
                GUIStyle st = new GUIStyle { font = fonteTitulo, fontSize = Mathf.RoundToInt(tam), alignment = TextAnchor.UpperLeft };
                Rect area = new Rect(0, H * 0.45f - tam * 0.72f, W, tam * 1.5f);
                string txt = "VOCÊ MORREU";
                GUI.color = Color.white;
                for (int k = 0; k < 12; k++) { float ang = k * Mathf.PI / 6f; Letras(new Rect(area.x + Mathf.Cos(ang) * 9f * esc, area.y + Mathf.Sin(ang) * 9f * esc, area.width, area.height), txt, st, espaco, new Color(0.75f, 0.02f, 0.03f, 0.10f * ta)); }   // halo
                for (int k = 0; k < 8; k++) { float ang = k * Mathf.PI / 4f; Letras(new Rect(area.x + Mathf.Cos(ang) * 3.5f * esc, area.y + Mathf.Sin(ang) * 3.5f * esc, area.width, area.height), txt, st, espaco, new Color(0f, 0f, 0f, 0.95f * ta)); }      // contorno
                if (u < 0.7f)                                                                      // tremor de cor no começo
                {
                    float j = (1f - u / 0.7f) * 10f * esc * (Mathf.PerlinNoise(t * 30f, 1f) - 0.3f);
                    Letras(new Rect(area.x - j, area.y, area.width, area.height), txt, st, espaco, new Color(1f, 0.1f, 0.1f, 0.5f * ta));
                    Letras(new Rect(area.x + j, area.y, area.width, area.height), txt, st, espaco, new Color(0.1f, 0.8f, 1f, 0.35f * ta));
                }
                Letras(area, txt, st, espaco, new Color(0.80f, 0.11f, 0.10f, ta));
                Letras(new Rect(area.x, area.y - 1.5f * esc, area.width, area.height), txt, st, espaco, new Color(1f, 0.45f, 0.35f, 0.22f * ta));        // reflexo no alto das letras
                GUI.matrix = m;

                // fios finos acima e abaixo
                GUI.color = new Color(0.55f, 0.1f, 0.1f, 0.65f * ta);
                float lw = W * 0.34f * Mathf.Lerp(0.7f, 1f, Saida(u / 4f));
                GUI.DrawTexture(new Rect(W * 0.5f - lw * 0.5f, fy + fh * 0.14f, lw, 2f * esc), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W * 0.5f - lw * 0.5f, fy + fh * 0.86f, lw, 2f * esc), Texture2D.whiteTexture);
            }

            DesenharDica(esc, t, W, H, online, linhaExtra);
        }

        // a tela escurece até o preto ao renascer e clareia no novo lugar
        if (sairEm >= 0f) Pintar(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, Suave((Time.unscaledTime - sairEm) / 0.5f)));
        else if (entrarEm >= 0f)
        {
            float k = (Time.unscaledTime - entrarEm) / 1.4f;
            if (k >= 1f) entrarEm = -1f; else Pintar(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 1f - Suave(k)));
        }
        GUI.color = antes;
    }

    // fumaça escura que avança das bordas para o centro (shader); sem o shader, só a vinheta
    static void DesenharEscuridao(float t, float W, float H)
    {
        if (shaderEscuridao == null) shaderEscuridao = Resources.Load<Shader>("PingluEscuridao");
        if (shaderEscuridao == null || !shaderEscuridao.isSupported) return;
        if (matEscuridao == null) matEscuridao = new Material(shaderEscuridao) { hideFlags = HideFlags.HideAndDontSave };
        matEscuridao.SetFloat("_Progresso", Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 3.4f)));
        matEscuridao.SetFloat("_Aspecto", W / H);
        matEscuridao.SetFloat("_Tempo", Time.unscaledTime);
        Graphics.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture, matEscuridao);
    }

    // canto inferior direito, discreto, igual ao "segure V" da missão: quadrado com a tecla que vai enchendo o contorno
    static void DesenharDica(float esc, float t, float W, float H, bool online, string linhaExtra)
    {
        float pa = Suave((t - 3.0f) / 1.0f); if (pa <= 0f) return;
        float lado = 54f * esc, margem = 46f * esc;
        Rect quad = new Rect(W - margem - lado, H - margem - lado, lado, lado);
        Color ouro = new Color(0.86f, 0.68f, 0.34f);
        bool cheio = seguraR >= 1f || sairEm >= 0f;

        GUIStyle rot = new GUIStyle { font = fonteTexto, fontSize = Mathf.RoundToInt(34f * esc), alignment = TextAnchor.MiddleRight };
        GUIStyle pequeno = new GUIStyle { font = fonteTexto, fontSize = Mathf.RoundToInt(22f * esc), alignment = TextAnchor.MiddleRight };
        float xr = quad.x - 20f * esc;
        Texto(new Rect(xr - 520f * esc, quad.y - 2f * esc, 520f * esc, lado * 0.58f), online ? "Voltar à luta" : "Renascer no último ponto", rot, new Color(0.92f, 0.88f, 0.8f, 0.92f * pa), esc * 0.6f);
        Texto(new Rect(xr - 520f * esc, quad.y + lado * 0.52f, 520f * esc, lado * 0.42f), online ? "aperte" : "segure", pequeno, new Color(0.78f, 0.74f, 0.66f, 0.7f * pa * (1f - seguraR)), esc * 0.5f);
        if (!string.IsNullOrEmpty(linhaExtra)) Texto(new Rect(xr - 520f * esc, quad.y - 34f * esc, 520f * esc, 30f * esc), linhaExtra, pequeno, new Color(0.7f, 0.66f, 0.58f, 0.7f * pa), esc * 0.5f);

        GUI.color = new Color(0.05f, 0.04f, 0.035f, 0.85f * pa); GUI.DrawTexture(quad, Texture2D.whiteTexture);
        ContornoQuadrado(quad, 2f * esc, new Color(ouro.r, ouro.g, ouro.b, 0.4f * pa));
        if (seguraR > 0f) ArcoQuadrado(quad, seguraR, 4f * esc, Color.Lerp(ouro, new Color(1f, 0.93f, 0.7f), seguraR));
        if (cheio) ContornoQuadrado(new Rect(quad.x - 6f * esc, quad.y - 6f * esc, quad.width + 12f * esc, quad.height + 12f * esc), 2f * esc, new Color(1f, 0.9f, 0.6f, 0.9f));
        GUIStyle tecla = new GUIStyle { font = fonteTitulo, fontSize = Mathf.RoundToInt(32f * esc), alignment = TextAnchor.MiddleCenter };
        GUI.color = Color.white; GUI.Label(quad, Comandos.Dica(Acao.Reviver), Colorir(tecla, Color.Lerp(new Color(0.9f, 0.84f, 0.7f, pa), Color.white, seguraR)));
    }

    // contorno quadrado que vai enchendo: começa no meio de cima e gira para a direita (igual ao botão V)
    static void ArcoQuadrado(Rect r, float fracao, float e, Color cor)
    {
        Texture2D px = Texture2D.whiteTexture; float L = r.width, resta = fracao * L * 4f; GUI.color = cor; float d;
        d = Mathf.Min(resta, L * 0.5f); GUI.DrawTexture(new Rect(r.center.x, r.y, d, e), px); resta -= d; if (resta <= 0f) return;
        d = Mathf.Min(resta, L); GUI.DrawTexture(new Rect(r.xMax - e, r.y, e, d), px); resta -= d; if (resta <= 0f) return;
        d = Mathf.Min(resta, L); GUI.DrawTexture(new Rect(r.xMax - d, r.yMax - e, d, e), px); resta -= d; if (resta <= 0f) return;
        d = Mathf.Min(resta, L); GUI.DrawTexture(new Rect(r.x, r.yMax - d, e, d), px); resta -= d; if (resta <= 0f) return;
        d = Mathf.Min(resta, L * 0.5f); GUI.DrawTexture(new Rect(r.x, r.y, d, e), px);
    }

    static void ContornoQuadrado(Rect r, float e, Color c)
    {
        Texture2D px = Texture2D.whiteTexture; GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, e), px); GUI.DrawTexture(new Rect(r.x, r.yMax - e, r.width, e), px);
        GUI.DrawTexture(new Rect(r.x, r.y, e, r.height), px); GUI.DrawTexture(new Rect(r.xMax - e, r.y, e, r.height), px);
    }

    // ------------------------------------------------------------------ ajudas de desenho
    static GUIStyle Colorir(GUIStyle s, Color c) { GUIStyle n = new GUIStyle(s); n.normal.textColor = c; return n; }

    static void Pintar(Rect r, Color c) { Color a = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = a; }

    static void Caixa(Rect r, float raio) { GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, GUI.color, Vector4.zero, new Vector4(raio, raio, raio, raio)); }

    // texto com contorno preto (sempre legível sobre qualquer fundo)
    static void Texto(Rect r, string txt, GUIStyle st, Color cor, float esc)
    {
        GUI.color = Color.white;
        float o = 2.4f * esc;
        for (int k = 0; k < 8; k++) { float ang = k * Mathf.PI / 4f; GUI.Label(new Rect(r.x + Mathf.Cos(ang) * o, r.y + Mathf.Sin(ang) * o, r.width, r.height), txt, Colorir(st, new Color(0f, 0f, 0f, cor.a))); }
        GUI.Label(r, txt, Colorir(st, cor));
    }

    // letras uma a uma, com espaço extra entre elas e centralizadas na área
    static void Letras(Rect area, string txt, GUIStyle st, float espaco, Color cor)
    {
        GUIStyle s = Colorir(st, cor); s.alignment = TextAnchor.UpperLeft;
        float[] w = new float[txt.Length]; float total = 0f;
        for (int i = 0; i < txt.Length; i++) { w[i] = txt[i] == ' ' ? st.fontSize * 0.42f : s.CalcSize(new GUIContent(txt[i].ToString())).x; total += w[i] + espaco; }
        total -= espaco;
        float x = area.center.x - total * 0.5f;
        for (int i = 0; i < txt.Length; i++) { if (txt[i] != ' ') GUI.Label(new Rect(x, area.y, w[i] + 10f, area.height), txt[i].ToString(), s); x += w[i] + espaco; }
    }

    // ------------------------------------------------------------------ imagens e som feitos por código
    static void GerarImagens()
    {
        vinheta = GerarVinheta(); faixa = GerarFaixa();
    }

    static Texture2D GerarVinheta()
    {
        int n = 128; Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.2f, d))));
        }
        t.Apply(); return t;
    }

    static Texture2D GerarFaixa()
    {
        int n = 64; Texture2D t = new Texture2D(1, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) { float u = y / (float)(n - 1); float a = Mathf.Clamp01(Mathf.Sin(u * Mathf.PI) * 1.5f); t.SetPixel(0, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a))); }
        t.Apply(); return t;
    }

    // pancada grave que cai de tom, com um batimento fraco depois e um sopro
    static AudioClip GerarSom()
    {
        int sr = 22050, n = (int)(sr * 4f); float[] d = new float[n]; System.Random r = new System.Random(3); float fase = 0f, lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float f = Mathf.Lerp(90f, 30f, Mathf.Clamp01(t / 1.6f)); fase += 2f * Mathf.PI * f / sr;
            float v = Mathf.Sin(fase) * Mathf.Exp(-t * 1.25f) * Mathf.Clamp01(t / 0.008f);
            float ruido = (float)(r.NextDouble() * 2.0 - 1.0); lp += (ruido - lp) * 0.06f; lp2 += (lp - lp2) * 0.15f;
            v += lp * Mathf.Exp(-t * 11f) * 1.1f;                                            // estalo do impacto
            v += lp2 * 0.55f * Mathf.Clamp01((t - 0.3f) / 1.2f) * Mathf.Exp(-(t - 0.3f) * 0.9f) * 0.5f;   // sopro grave
            float t2 = t - 0.7f; if (t2 > 0f) v += Mathf.Sin(2f * Mathf.PI * 46f * t2) * Mathf.Exp(-t2 * 6f) * 0.5f;                  // batimento
            float t3 = t - 1.15f; if (t3 > 0f) v += Mathf.Sin(2f * Mathf.PI * 40f * t3) * Mathf.Exp(-t3 * 6f) * 0.32f;
            d[i] = Mathf.Clamp(v * 0.95f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("morte", n, 1, sr, false); c.SetData(d, 0); return c;
    }
}
