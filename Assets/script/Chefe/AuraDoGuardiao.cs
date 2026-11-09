using System.Collections.Generic;
using UnityEngine;

// A aura da 2ª fase do Guardião: sinistra e radiante ao mesmo tempo.
//  - fumaça negra-violeta saindo de toda a superfície do corpo;
//  - chamas e fagulhas vermelho-ouro, também saindo do corpo, que o deixam brilhando por dentro;
//  - orbes de energia com rastro girando em espiral em volta dele;
//  - círculo de runas brilhando no chão, girando;
//  - colunas de luz subindo atrás dele, e duas luzes (vermelha e violeta) que pulsam.
// Tudo nasce do próprio objeto do chefe e some junto com ele.
public class AuraDoGuardiao : MonoBehaviour
{
    // ---------------------------------------------------------------- texturas feitas por código
    static Texture2D texSuave, texChama, texRunas, texFeixe, texPilar;

    static Texture2D Fazer(int w, int h, System.Func<float, float, float> alfa)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color[] c = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) { float a = Mathf.Clamp01(alfa((x + 0.5f) / w, (y + 0.5f) / h)); c[y * w + x] = new Color(1f, 1f, 1f, a); }
        t.SetPixels(c); t.Apply(); return t;
    }

    static float Anel(float r, float centro, float esp) { return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(r - centro) / esp); }

    static void FazerTexturas()
    {
        if (texSuave != null) return;
        texSuave = Fazer(64, 64, (u, v) => { float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f; return Mathf.Pow(Mathf.Clamp01(1f - r), 2f); });
        // chama: fina embaixo e em cima, larga no meio, com ponta
        texChama = Fazer(64, 128, (u, v) =>
        {
            float larg = Mathf.Lerp(0.42f, 0.04f, Mathf.Pow(v, 0.8f)) * (v < 0.15f ? v / 0.15f : 1f);
            float d = Mathf.Abs(u - 0.5f) / Mathf.Max(0.02f, larg);
            return Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f) * (1f - v * 0.6f);
        });
        // runas: anéis, raios e marcas, vistos de cima
        texRunas = Fazer(256, 256, (u, v) =>
        {
            float x = u * 2f - 1f, y = v * 2f - 1f, r = Mathf.Sqrt(x * x + y * y), ang = Mathf.Atan2(y, x);
            float a = Anel(r, 0.97f, 0.018f) + Anel(r, 0.9f, 0.01f) * 0.8f + Anel(r, 0.56f, 0.012f) + Anel(r, 0.3f, 0.01f) * 0.7f;
            float setor = ang / (Mathf.PI * 2f) * 12f; int idx = Mathf.FloorToInt(setor + 12f); float f = setor - Mathf.Floor(setor);
            if (r > 0.64f && r < 0.84f && f > 0.3f && f < 0.7f) a += (idx % 2 == 0 ? 0.9f : 0.5f) * (1f - Mathf.Abs(f - 0.5f) * 2f * 0.6f);
            if (r > 0.3f && r < 0.56f) { float s = ang / (Mathf.PI * 2f) * 6f; s -= Mathf.Floor(s); if (Mathf.Abs(s - 0.5f) > 0.488f) a += 0.9f; }
            if (r > 0.93f && r < 0.96f) { float s = ang / (Mathf.PI * 2f) * 48f; s -= Mathf.Floor(s); if (s < 0.35f) a += 0.8f; }
            return a * (r < 1f ? 1f : 0f);
        });
        // feixe visto de lado (para o LineRenderer): brilha no meio da largura
        texFeixe = Fazer(8, 64, (u, v) => Mathf.Pow(1f - Mathf.Abs(v * 2f - 1f), 1.4f));
        // coluna de luz: brilha no meio e some para cima
        texPilar = Fazer(32, 128, (u, v) => Mathf.Pow(1f - Mathf.Abs(u * 2f - 1f), 2f) * Mathf.Pow(1f - v, 1.3f) * Mathf.Clamp01(v * 8f));
    }

    public static Texture2D TexSuave { get { FazerTexturas(); return texSuave; } }
    public static Texture2D TexFeixe { get { FazerTexturas(); return texFeixe; } }

    public static Material Mat(Texture2D tex, bool aditivo, Color cor)
    {
        Shader sh = Shader.Find("Pinglu/Aura");
        Material m;
        if (sh != null)
        {
            m = new Material(sh); m.SetTexture("_MainTex", tex); m.SetColor("_Cor", cor);
            m.SetFloat("_Src", 5f); m.SetFloat("_Dst", aditivo ? 1f : 10f);
        }
        else
        {
            m = new Material(Shader.Find("Sprites/Default")); m.mainTexture = tex; m.color = cor;
        }
        m.renderQueue = 3100;
        return m;
    }

    // brilho que sempre olha para a câmera (esfera de luz, clarão no fim do raio...)
    public class OlhaParaCamera : MonoBehaviour
    {
        void LateUpdate() { Camera c = Camera.main; if (c != null) transform.rotation = c.transform.rotation; }
    }

    public static GameObject Brilho(Vector3 pos, float tam, Color cor, Transform pai = null)
    {
        FazerTexturas();
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(g.GetComponent<Collider>());
        g.name = "Brilho"; if (pai != null) g.transform.SetParent(pai, true);
        g.transform.position = pos; g.transform.localScale = Vector3.one * tam;
        Renderer r = g.GetComponent<Renderer>(); r.sharedMaterial = Mat(texSuave, true, cor); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.AddComponent<OlhaParaCamera>();
        return g;
    }

    // ---------------------------------------------------------------- a aura
    SkinnedMeshRenderer pele;
    ParticleSystem fumaca, chamas, fagulhas;
    float[] taxaBase = new float[3];
    Light luzRubra, luzVioleta;
    Transform runas, ultimoPilar;
    Material matRunas;
    readonly List<Transform> orbes = new List<Transform>();
    readonly List<Transform> pilares = new List<Transform>();
    readonly List<Material> matPilares = new List<Material>();
    float forca, alvoForca = 1f, tempoFade = 2f, spin, semente;
    bool desligando;

    public static AuraDoGuardiao Criar(Transform chefe, SkinnedMeshRenderer pele)
    {
        FazerTexturas();
        GameObject g = new GameObject("Aura sinistra");
        g.transform.SetParent(chefe, false);
        AuraDoGuardiao a = g.AddComponent<AuraDoGuardiao>();
        a.pele = pele; a.Montar();
        return a;
    }

    public void Ligar(float segundos) { alvoForca = 1f; tempoFade = Mathf.Max(0.1f, segundos); }
    public void Desligar() { alvoForca = 0f; tempoFade = 0.9f; desligando = true; }

    ParticleSystem Sistema(string nome, Material mat, float taxa, float vidaMin, float vidaMax, float velMin, float velMax, float tamMin, float tamMax,
        Color cor, float gravidade, bool esticar)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(transform, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false; main.duration = 5f; main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vidaMin, vidaMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velMin, velMax);
        main.startSize = new ParticleSystem.MinMaxCurve(tamMin, tamMax);
        main.startColor = cor; main.gravityModifier = gravidade;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var em = ps.emission; em.rateOverTime = taxa;
        var sh = ps.shape;
        if (pele != null) { sh.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer; sh.skinnedMeshRenderer = pele; sh.normalOffset = 0.15f; }
        else { sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 30f; sh.radius = 1.6f; }
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (esticar) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.1f; r.lengthScale = 2.4f; }
        return ps;
    }

    void Montar()
    {
        semente = Random.value * 20f;
        // 1) fumaça negra-violeta, subindo devagar (cobre o corpo, dá peso)
        Material mSombra = Mat(texSuave, false, new Color(1f, 1f, 1f, 1f));
        fumaca = Sistema("Fumaça", mSombra, 85f, 1.8f, 3.0f, 0.2f, 1.1f, 1.8f, 3.6f, new Color(0.07f, 0.02f, 0.11f, 0.62f), -0.07f, false);
        var cs = fumaca.colorOverLifetime; cs.enabled = true;
        Gradient g1 = new Gradient();
        g1.SetKeys(new[] { new GradientColorKey(new Color(0.16f, 0.04f, 0.2f), 0f), new GradientColorKey(new Color(0.02f, 0.01f, 0.04f), 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.2f), new GradientAlphaKey(0f, 1f) });
        cs.color = g1;
        var ns = fumaca.noise; ns.enabled = true; ns.strength = 0.9f; ns.frequency = 0.35f;
        var sz = fumaca.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 1.5f));

        // 2) chamas vermelho-ouro, aditivas: o corpo brilha por dentro e solta labaredas
        Material mChama = Mat(texChama, true, new Color(2.6f, 0.8f, 0.2f, 1f));
        chamas = Sistema("Chamas", mChama, 70f, 0.7f, 1.4f, 1.2f, 3.4f, 1.3f, 2.8f, new Color(1f, 0.55f, 0.18f, 0.4f), -0.12f, false);
        var cc = chamas.colorOverLifetime; cc.enabled = true;
        Gradient g2 = new Gradient();
        g2.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.08f), 0.35f), new GradientColorKey(new Color(0.55f, 0.05f, 0.08f), 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
        cc.color = g2;
        var ns2 = chamas.noise; ns2.enabled = true; ns2.strength = 0.7f; ns2.frequency = 0.8f;
        var sz2 = chamas.sizeOverLifetime; sz2.enabled = true; sz2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.25f));

        // 3) fagulhas claras e rápidas, esticadas (riscos de luz)
        Material mFag = Mat(texSuave, true, new Color(4f, 1.8f, 0.6f, 1f));
        fagulhas = Sistema("Fagulhas", mFag, 110f, 0.8f, 2.0f, 2.5f, 6.5f, 0.1f, 0.34f, new Color(1f, 0.8f, 0.45f, 1f), -0.25f, true);
        var cf = fagulhas.colorOverLifetime; cf.enabled = true;
        Gradient g3 = new Gradient();
        g3.SetKeys(new[] { new GradientColorKey(new Color(1f, 1f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        cf.color = g3;
        var nf = fagulhas.noise; nf.enabled = true; nf.strength = 1.4f; nf.frequency = 0.6f;
        taxaBase[0] = 85f; taxaBase[1] = 70f; taxaBase[2] = 110f;

        // 4) orbes com rastro, girando em espiral
        Material mRastro = Mat(texFeixe, true, new Color(3f, 1.1f, 0.35f, 1f));
        for (int i = 0; i < 7; i++)
        {
            GameObject o = new GameObject("Orbe " + i); o.transform.SetParent(transform, false);
            TrailRenderer tr = o.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mRastro; tr.time = 1.1f; tr.startWidth = 0.42f; tr.endWidth = 0f; tr.minVertexDistance = 0.12f;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.textureMode = LineTextureMode.Stretch;
            Gradient gt = new Gradient();
            gt.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.15f, 0.1f), 1f) },
                       new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = gt;
            GameObject nucleo = Brilho(o.transform.position, 0.9f, new Color(3f, 1.4f, 0.5f, 1f), o.transform);
            orbes.Add(o.transform);
        }

        // 5) círculo de runas no chão
        GameObject gr = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(gr.GetComponent<Collider>());
        gr.name = "Runas"; gr.transform.SetParent(transform, false); runas = gr.transform;
        matRunas = Mat(texRunas, true, new Color(2.2f, 0.45f, 0.18f, 1f));
        Renderer rr = gr.GetComponent<Renderer>(); rr.sharedMaterial = matRunas; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        runas.localScale = Vector3.one * 17f;

        // 6) colunas de luz subindo em volta (sempre viradas para a câmera)
        for (int i = 0; i < 6; i++)
        {
            GameObject gp = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(gp.GetComponent<Collider>());
            gp.name = "Coluna de luz"; gp.transform.SetParent(transform, false);
            Material mp = Mat(texPilar, true, new Color(2f, 0.5f, 0.2f, 0.55f));
            Renderer pr = gp.GetComponent<Renderer>(); pr.sharedMaterial = mp; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gp.transform.localScale = new Vector3(2.2f, 15f, 1f);
            pilares.Add(gp.transform); matPilares.Add(mp);
        }

        // 7) luzes
        GameObject l1 = new GameObject("Luz rubra"); l1.transform.SetParent(transform, false); l1.transform.localPosition = Vector3.up * 2.2f;
        luzRubra = l1.AddComponent<Light>(); luzRubra.type = LightType.Point; luzRubra.color = new Color(1f, 0.38f, 0.12f); luzRubra.range = 28f; luzRubra.shadows = LightShadows.None; luzRubra.intensity = 0f;
        GameObject l2 = new GameObject("Luz violeta"); l2.transform.SetParent(transform, false); l2.transform.localPosition = Vector3.up * 4.5f;
        luzVioleta = l2.AddComponent<Light>(); luzVioleta.type = LightType.Point; luzVioleta.color = new Color(0.55f, 0.2f, 1f); luzVioleta.range = 18f; luzVioleta.shadows = LightShadows.None; luzVioleta.intensity = 0f;

        fumaca.Play(); chamas.Play(); fagulhas.Play();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        forca = Mathf.MoveTowards(forca, alvoForca, dt / tempoFade);
        float t = Time.time + semente;
        float pulso = 0.8f + 0.2f * Mathf.Sin(t * 3.4f) + 0.08f * Mathf.Sin(t * 11f);

        SetTaxa(fumaca, taxaBase[0] * forca); SetTaxa(chamas, taxaBase[1] * forca); SetTaxa(fagulhas, taxaBase[2] * forca);
        luzRubra.intensity = 7f * forca * pulso; luzVioleta.intensity = 5f * forca * (1.1f - pulso * 0.4f);

        Vector3 pe = transform.position;
        // runas: ficam no chão, giram devagar e pulsam
        RaycastHit h; float chao = pe.y;
        if (Physics.Raycast(pe + Vector3.up * 3f, Vector3.down, out h, 12f, ~0, QueryTriggerInteraction.Ignore)) chao = h.point.y;
        spin += dt * 14f;
        runas.position = new Vector3(pe.x, chao + 0.07f, pe.z);
        runas.rotation = Quaternion.Euler(90f, spin, 0f);
        float cresce = Mathf.SmoothStep(0f, 1f, forca);
        runas.localScale = Vector3.one * (17f * (0.55f + 0.45f * cresce));
        matRunas.SetColor("_Cor", new Color(2.2f * pulso, 0.45f * pulso, 0.18f * pulso, cresce));

        // orbes em espiral
        for (int i = 0; i < orbes.Count; i++)
        {
            float a = t * (1.5f + 0.18f * i) + i * (Mathf.PI * 2f / orbes.Count);
            float raio = 2.7f + 0.7f * Mathf.Sin(t * 0.9f + i * 1.7f);
            float alt = 0.6f + (i / (float)orbes.Count) * 4.6f + Mathf.Sin(t * 1.3f + i) * 0.5f;
            orbes[i].position = pe + new Vector3(Mathf.Cos(a) * raio, alt, Mathf.Sin(a) * raio);
            orbes[i].gameObject.SetActive(forca > 0.25f);
        }

        // colunas: viradas para a câmera, em círculo, piscando em ritmos diferentes
        Camera cam = Camera.main;
        for (int i = 0; i < pilares.Count; i++)
        {
            float a = i * (Mathf.PI * 2f / pilares.Count) + t * 0.12f, r = 3.6f + (i % 2) * 2f;
            Vector3 p = pe + new Vector3(Mathf.Cos(a) * r, 7f, Mathf.Sin(a) * r);
            pilares[i].position = new Vector3(p.x, chao + 7f, p.z);
            if (cam != null) { Vector3 f = pilares[i].position - cam.transform.position; f.y = 0f; if (f.sqrMagnitude > 0.01f) pilares[i].rotation = Quaternion.LookRotation(f); }
            float pisca = 0.55f + 0.45f * Mathf.Sin(t * (1.1f + 0.3f * i) + i * 2f);
            matPilares[i].SetColor("_Cor", new Color(2f, 0.5f, 0.2f, 0.5f * pisca * cresce));
        }

        if (desligando && forca <= 0.001f) Destroy(gameObject);
    }

    static void SetTaxa(ParticleSystem ps, float taxa) { var em = ps.emission; em.rateOverTime = taxa; }
}
