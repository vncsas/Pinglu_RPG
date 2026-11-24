using System;
using System.Collections.Generic;
using UnityEngine;

// Cenas novas do prólogo do trailer: a vila de perto (moradores e Caio) e a vila em chamas vista de cima com a SOMBRA do dragão passando (sem mostrar o dragão).
public partial class IntroDoJogo
{
    Cena CenaVilaDePerto(float dur)
    {
        Cena c = new Cena { dur = dur, texto = "", desenha = k => { } };
        c.amb = new Amb { sol = new Color(1f, 0.86f, 0.62f), solInt = 1.9f, solRot = new Vector3(28f, -55f, 0f), fog = new Color(0.78f, 0.74f, 0.66f), fogDen = 0.0010f, amb = new Color(0.6f, 0.56f, 0.5f), ceu = new Color(0.55f, 0.65f, 0.9f), exposicao = 1.0f, atmosfera = 1f };
        Vector3 v = vila; List<Transform> alvos = new List<Transform>(); List<float> angs = new List<float>(); Transform caio = null;
        c.entra = () =>
        {
            alvos.Clear();
            foreach (NpcFalante n in FindObjectsByType<NpcFalante>(FindObjectsSortMode.None)) if (n != null && n.nomeExibido == "Caio") { caio = n.transform; break; }
            Vector3 centro = caio != null ? caio.position : v;
            List<Transform> perto = new List<Transform>();
            foreach (NpcDaVila n in FindObjectsByType<NpcDaVila>(FindObjectsSortMode.None)) if (n != null && n.gameObject.activeInHierarchy && Vector3.Distance(n.transform.position, centro) < 60f && Vector3.Distance(n.transform.position, centro) > 5f) perto.Add(n.transform);
            perto.Sort((a, b) => Vector3.Distance(b.position, centro).CompareTo(Vector3.Distance(a.position, centro)));
            for (int i = 0; i < perto.Count && alvos.Count < 3; i += Mathf.Max(1, perto.Count / 3)) alvos.Add(perto[i]);
            if (caio != null) alvos.Add(caio);
            angs.Clear(); foreach (Transform t in alvos) angs.Add(AnguloLivre(t.position, 3.8f));
            Debug.Log("[Trailer] vila de perto: " + alvos.Count + " alvos (moradores + Caio)");
        };
        c.cam3 = k =>
        {
            int n = alvos.Count + 1; float f = k * n; int i = Mathf.Min(n - 1, (int)f); float kk = f - i;
            if (i == 0) { Cam(Sv(G3(Fr3(v, -22f, -38f), 3f), G3(Fr3(v, -8f, -26f), 6f), kk), G3(v, 4f), 52f); return; }   // plano geral da rua
            Transform t = alvos[i - 1]; if (t == null) return;
            float ang = (angs[i - 1] + (kk - 0.5f) * 36f) * Mathf.Deg2Rad; Vector3 p = t.position + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * Mathf.Lerp(4.2f, 3.2f, kk);
            CamDireta(G3(p, 1.5f), t.position + Vector3.up * 1.45f, 46f);
        };
        return c;
    }

    // câmera sem o desvio automático de casas (que jogava a câmera para cima): o ângulo já foi escolhido livre de obstáculos
    void CamDireta(Vector3 pos, Vector3 alvo, float fov)
    {
        if (cam3 == null) return;
        pos.y = Mathf.Max(pos.y, Chao3(pos) + 1.2f); cam3.transform.position = pos;
        Vector3 d = alvo - pos; if (d.sqrMagnitude > 0.001f) cam3.transform.rotation = Quaternion.LookRotation(d, Vector3.up); cam3.fieldOfView = fov;
    }

    // ângulo (graus) de onde a câmera enxerga o alvo sem parede, casa ou árvore no meio
    float AnguloLivre(Vector3 alvo, float raio)
    {
        float melhor = 0f, nota = 1e9f; Vector3 olho = alvo + Vector3.up * 1.5f;
        for (int a = 0; a < 360; a += 20)
        {
            Vector3 q = alvo + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * raio; q.y = Chao3(q) + 1.5f; float n = 0f;
            for (int i = 0; i < obst3.Count; i++)
            {
                Bounds b = obst3[i]; b.Expand(2.2f);
                if (b.Contains(q)) n += 10f;
                Ray r = new Ray(q, olho - q); float d; if (b.IntersectRay(r, out d) && d < raio) n += 4f;
            }
            if (n < nota) { nota = n; melhor = a; }
        }
        return melhor;
    }

    // textura da sombra do dragão visto de cima: contorno (metade direita; a esquerda é o espelho), preenchido por código
    static readonly Vector2[] CONTORNO = {
        new Vector2(0.00f, 1.00f), new Vector2(0.05f, 0.93f), new Vector2(0.07f, 0.84f), new Vector2(0.05f, 0.70f), new Vector2(0.09f, 0.50f),
        new Vector2(0.30f, 0.58f), new Vector2(0.62f, 0.76f), new Vector2(0.98f, 0.66f),                       // borda da frente da asa até a ponta
        new Vector2(0.90f, 0.40f), new Vector2(0.80f, 0.42f), new Vector2(0.80f, 0.20f), new Vector2(0.66f, 0.26f), new Vector2(0.64f, 0.04f), new Vector2(0.50f, 0.12f),
        new Vector2(0.46f, -0.12f), new Vector2(0.32f, -0.02f), new Vector2(0.16f, -0.16f), new Vector2(0.07f, -0.30f),
        new Vector2(0.04f, -0.62f), new Vector2(0.015f, -0.90f), new Vector2(0.00f, -1.00f) };
    static bool Dentro(Vector2[] poly, float x, float y)
    {
        bool d = false; for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) if (((poly[i].y > y) != (poly[j].y > y)) && (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) d = !d; return d;
    }
    Texture2D TexSombraDragao()
    {
        int N = CONTORNO.Length; Vector2[] full = new Vector2[N * 2 - 2];
        for (int i = 0; i < N; i++) full[i] = CONTORNO[i];
        for (int i = 1; i < N - 1; i++) full[N - 1 + i] = new Vector2(-CONTORNO[N - 1 - i].x, CONTORNO[N - 1 - i].y);
        int W = 192, H = 192; Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        Color[] px = new Color[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            int c = 0; for (int sy = 0; sy < 3; sy++) for (int sx = 0; sx < 3; sx++) { float u = ((x + (sx + 0.5f) / 3f) / W - 0.5f) * 2f, w = ((y + (sy + 0.5f) / 3f) / H - 0.5f) * 2f; if (Dentro(full, u, w)) c++; }
            px[y * W + x] = new Color(0f, 0f, 0f, c / 9f * 0.92f);
        }
        t.SetPixels(px); t.Apply(); return t;
    }

    // ------------------------------------------------------------------ fogo de verdade: chamas (textura de labareda, aditivas), brilho, brasas, coluna de fumaça e luz
    Texture2D texChama;
    Texture2D TexChama()
    {
        if (texChama != null) return texChama;
        int W = 96, H = 160; texChama = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        Color[] px = new Color[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            float v = y / (H - 1f), u = (x / (W - 1f) - 0.5f) * 2f;
            u += 0.16f * Mathf.Sin(v * 9f + 1.3f) * v;                                                  // a labareda ondula
            float larg = 0.62f * Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Min(v, 1f), 0.55f)) * (1f - 0.35f * v);
            float inten = larg > 0.001f ? Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(u) / larg, 2f)) : 0f;
            inten *= 1f - Mathf.Pow(v, 2.2f) * 0.85f;
            float quente = Mathf.Clamp01(inten * 1.5f - v * 0.35f);                                       // miolo amarelo, borda vermelha
            Color cor = Color.Lerp(new Color(0.85f, 0.12f, 0.02f), Color.Lerp(new Color(1f, 0.5f, 0.08f), new Color(1f, 0.95f, 0.65f), Mathf.Clamp01((quente - 0.5f) * 2f)), Mathf.Clamp01(quente * 1.6f));
            float a = Mathf.Pow(inten, 0.8f); px[y * W + x] = new Color(cor.r * a, cor.g * a, cor.b * a, a);
        }
        texChama.SetPixels(px); texChama.Apply(); return texChama;
    }
    Material matAd;
    Material MatAditivo(Texture2D tex)
    {
        Shader sh = Shader.Find("Legacy Shaders/Particles/Additive"); if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh); m.mainTexture = tex; if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); return m;
    }
    ParticleSystem PSAd(Transform pai, string nome, Texture2D tex, Color cor, float vida, float vel, float tam, float taxa, float grav, float raio, bool chama)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(pai, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>(); ps.Stop();
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.sharedMaterial = MatAditivo(tex);
        var main = ps.main; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = vida; main.startSpeed = vel;
        main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.65f, tam); main.startColor = cor; main.gravityModifier = grav; main.maxParticles = 300;
        main.startRotation = chama ? new ParticleSystem.MinMaxCurve(-0.22f, 0.22f) : new ParticleSystem.MinMaxCurve(0f, 6.28f);
        var em = ps.emission; em.rateOverTime = taxa;
        var sh = ps.shape; sh.shapeType = chama ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere; if (chama) { sh.angle = 8f; sh.rotation = new Vector3(-90f, 0f, 0f); } sh.radius = raio;
        var col = ps.colorOverLifetime; col.enabled = true; Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, chama ? 0.35f : 1.4f));
        ps.Play(); return ps;
    }
    GameObject FogoVila(Vector3 pos, float esc)
    {
        GameObject g = new GameObject("Fogo da vila"); g.transform.position = pos; lixo3.Add(g);
        PSAd(g.transform, "Labaredas", TexChama(), new Color(1f, 1f, 1f, 1f), 1.1f, 3.2f * esc, 4.2f * esc, 22f, -0.15f, 1.2f * esc, true);
        PSAd(g.transform, "Labaredas altas", TexChama(), new Color(1f, 0.85f, 0.7f, 1f), 1.5f, 4.5f * esc, 3.4f * esc, 9f, -0.2f, 0.8f * esc, true);
        PSAd(g.transform, "Brilho", FxCena.TexBrilho(), new Color(1f, 0.45f, 0.12f, 0.55f), 1.4f, 0.2f, 11f * esc, 2.5f, 0f, 0.5f * esc, false);
        PSAd(g.transform, "Brasas", FxCena.TexBrilho(), new Color(1f, 0.8f, 0.35f, 1f), 3.2f, 7f * esc * 0.5f, 0.45f * esc, 16f, -0.12f, 1.5f * esc, false);
        NovoPS(g.transform, "Fumaça negra", FxCena.TexNuvem(), new Color(0.05f, 0.045f, 0.045f, 0.75f), 8f, 4.5f * esc * 0.6f, 6f * esc, 5f, -0.1f, true, 1.5f * esc);
        GameObject lg = new GameObject("Luz"); lg.transform.SetParent(g.transform, false); lg.transform.localPosition = Vector3.up * 3f * esc;
        Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.18f); l.range = 22f * esc; l.intensity = 5f; l.shadows = LightShadows.None; lg.AddComponent<Tremeluz>().baseI = 6f;
        return g;
    }

    Cena CenaVilaEmChamas(float dur)
    {
        Cena c = new Cena { dur = dur, texto = "", desenha = k => { } };
        Vector3 v = vila;
        Amb a1 = new Amb { sol = new Color(1f, 0.45f, 0.2f), solInt = 1.5f, solRot = new Vector3(14f, -70f, 0f), fog = new Color(0.34f, 0.14f, 0.09f), fogDen = 0.0030f, amb = new Color(0.45f, 0.22f, 0.16f), ceu = new Color(0.85f, 0.35f, 0.22f), exposicao = 1.1f, atmosfera = 1.4f };
        Amb a2 = new Amb { sol = new Color(1f, 0.6f, 0.35f), solInt = 1.6f, solRot = new Vector3(55f, -70f, 0f), fog = new Color(0.30f, 0.16f, 0.12f), fogDen = 0.0012f, amb = new Color(0.45f, 0.26f, 0.2f), ceu = new Color(0.7f, 0.3f, 0.2f), exposicao = 1.0f, atmosfera = 1.4f };
        GameObject sombra = null; Texture2D tex = null; Material mat = null;
        c.entra = () =>
        {
            int n = 0;
            foreach (Bounds b in obst3)                                                          // as casas pegam fogo
            {
                if (n >= 16) break;
                if (b.size.y < 3f || b.size.y > 16f || b.size.x > 28f || b.size.z > 28f || b.size.x < 3f) continue;
                if (Vector3.Distance(new Vector3(b.center.x, v.y, b.center.z), v) > 80f) continue;
                FogoVila(new Vector3(b.center.x, b.max.y - 0.6f, b.center.z), UnityEngine.Random.Range(2.2f, 3.2f)); FogoVila(G3(new Vector3(b.center.x + b.extents.x, 0f, b.center.z)), 1.6f); n++;
            }
            for (int i = 0; i < 10; i++) { Vector2 o = UnityEngine.Random.insideUnitCircle * 70f; FogoVila(G3(v + new Vector3(o.x, 0f, o.y)), UnityEngine.Random.Range(1.4f, 2.2f)); }
            tex = TexSombraDragao(); mat = new Material(FxCena.Sprite()) { mainTexture = tex };
            sombra = new GameObject("Sombra do dragão"); lixo3.Add(sombra);
            MeshFilter mf = sombra.AddComponent<MeshFilter>(); mf.sharedMesh = FxCena.Quad(); MeshRenderer mr = sombra.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sombra.SetActive(false);
            Debug.Log("[Trailer] vila em chamas: " + n + " casas incendiadas");
        };
        Vector3 lado = Rg3, ao = dirIn3;
        c.vivo = k =>
        {
            AplicarAmb(k < 0.4f ? a1 : a2);
            if (sombra == null) return;
            bool cima = k >= 0.4f; sombra.SetActive(cima);
            if (!cima) return;
            float u = Mathf.InverseLerp(0.4f, 1f, k);                                            // 0..1: a sombra cruza a vila de um lado ao outro
            float s = Mathf.Lerp(-95f, 95f, Mathf.Clamp01((u - 0.10f) / 0.80f));
            Vector3 p = v + lado * s * 0.35f + ao * s; p.y = Chao3(p) + 0.5f;
            float bate = 1f + 0.10f * Mathf.Sin(TU * 5.5f);
            sombra.transform.position = p; sombra.transform.rotation = Quaternion.LookRotation(Vector3.up, ao + lado * 0.35f) * Quaternion.Euler(0f, 0f, 0f);
            sombra.transform.localScale = new Vector3(84f * bate, 70f, 1f);
        };
        c.cam3 = k =>
        {
            if (k < 0.4f) { float kk = k / 0.4f; Cam(Sv(G3(Fr3(v, -38f, -62f), 7f), G3(Fr3(v, 26f, -48f), 11f), kk), G3(v, 9f), Mathf.Lerp(58f, 50f, kk)); return; }
            float u = Mathf.InverseLerp(0.4f, 1f, k); Vector3 topo = v + Vector3.up * Mathf.Lerp(165f, 130f, u) + ao * (-16f + u * 8f);
            Cam(topo, G3(v, 0f), 52f);                                                            // câmera lá de cima, olhando para a vila queimando
        };
        return c;
    }
}
