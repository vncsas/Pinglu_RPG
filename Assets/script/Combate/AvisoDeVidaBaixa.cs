using UnityEngine;

// Vida baixando: as bordas da tela pulsam em vermelho no ritmo de um coração (que acelera quanto menos vida), com batida grave.
// Começa com 35% de vida; fica mais forte e mais rápido perto de zero. Some assim que a vida sobe.
public static class AvisoDeVidaBaixa
{
    const float LIMITE = 0.35f;
    static Texture2D borda; static AudioClip batida; static float faseAntes = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { faseAntes = 1f; }

    public static void Desenhar(float fracao)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (fracao > LIMITE || fracao <= 0f) { faseAntes = 1f; return; }
        if (borda == null) borda = GerarBorda();
        float sev = Mathf.Clamp01(1f - fracao / LIMITE);
        float periodo = Mathf.Lerp(1.3f, 0.58f, sev);
        float fase = (Time.unscaledTime % periodo) / periodo;
        // "tum-tum" do coração: uma batida forte e outra mais fraca logo depois
        float p1 = Mathf.Exp(-Mathf.Pow((fase - 0.05f) / 0.055f, 2f));
        float p2 = 0.65f * Mathf.Exp(-Mathf.Pow((fase - 0.30f) / 0.065f, 2f));
        float pulso = Mathf.Clamp01(p1 + p2);
        float alfa = Mathf.Lerp(0.16f, 0.5f, sev) + pulso * Mathf.Lerp(0.18f, 0.45f, sev);

        Color antes = GUI.color;
        GUI.color = new Color(0.78f, 0.02f, 0.04f, Mathf.Clamp01(alfa));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), borda, ScaleMode.StretchToFill);
        GUI.color = antes;

        if (fase < faseAntes - 0.5f || (faseAntes > 0.9f && fase < 0.2f))      // começou uma batida nova
        {
            if (batida == null) batida = GerarBatida();
            Camera c = Camera.main;
            if (c != null) AudioSource.PlayClipAtPoint(batida, c.transform.position, Mathf.Lerp(0.15f, 0.5f, sev) * AplicadorDeConfiguracoes.FatorDeEfeitos);
        }
        faseAntes = fase;
    }

    static Texture2D GerarBorda()
    {
        int n = 128; Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            float a = Mathf.Pow(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.18f, d)), 1.3f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    // "tum-tum" grave, curto
    static AudioClip GerarBatida()
    {
        int sr = 22050, n = (int)(sr * 0.55f); float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float v = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(62f, 40f, Mathf.Clamp01(t / 0.2f)) * t) * Mathf.Exp(-t * 16f);
            float t2 = t - 0.2f; if (t2 > 0f) v += 0.7f * Mathf.Sin(2f * Mathf.PI * 50f * t2) * Mathf.Exp(-t2 * 18f);
            d[i] = v * 0.9f;
        }
        AudioClip c = AudioClip.Create("batida", n, 1, sr, false); c.SetData(d, 0); return c;
    }
}
