using System.Collections.Generic;
using UnityEngine;

// AURA SOMBRIA do Drakthar (fase 2). Vários efeitos juntos, todos presos ao corpo do dragão:
//   fumaça negra que sobe dos ossos (asas, cabeça, cauda)  |  brasas roxas e vermelhas  |  halo escuro atrás do corpo com núcleo violeta que pulsa
//   três anéis de energia ondulando no chão                |  arcos de raio roxo estalando em volta  |  luz violeta no corpo e olhos vermelhos
// "intensidade" vai de 0 (apagada) a 1 (cheia); passar de 1 faz a aura "explodir" por um instante (ex.: no rugido).
public class AuraSombria : MonoBehaviour
{
    public float intensidade;
    public float tamanho = 26f;                        // comprimento do dragão (escala as partículas)
    public Transform centro, cabeca;
    public readonly List<Transform> pontos = new List<Transform>();

    float atual, ac1, ac2, ac3, proxRaio;
    ParticleSystem fumaca, brasas;
    Light luzCorpo, luzOlhos; GameObject halo, nucleo, olhoBrilho; readonly LineRenderer[] aneis = new LineRenderer[3];
    Material matParticula;

    static Color Roxo(float a) { return new Color(0.62f, 0.14f, 0.95f, a); }

    void Start() { Montar(); }

    ParticleSystem NovoSistema(string nome, int max)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(transform, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = max; m.startLifetime = 2f; m.startSize = 2f;
        var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = matParticula; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        ps.Play(); return ps;
    }

    void Montar()
    {
        Shader sh = Shader.Find("Sprites/Default");
        matParticula = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Particles/Unlit")); matParticula.mainTexture = FxCena.TexNuvem();
        // fumaça negra: cresce e some
        fumaca = NovoSistema("Fumaca negra", 1800);
        var cf = fumaca.colorOverLifetime; cf.enabled = true; Gradient gf = new Gradient();
        gf.SetKeys(new[] { new GradientColorKey(new Color(0.18f, 0.04f, 0.28f), 0f), new GradientColorKey(new Color(0.03f, 0.01f, 0.05f), 0.35f), new GradientColorKey(new Color(0.01f, 0f, 0.02f), 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
        cf.color = new ParticleSystem.MinMaxGradient(gf);
        var tf = fumaca.sizeOverLifetime; tf.enabled = true; tf.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.9f));
        var rf = fumaca.rotationOverLifetime; rf.enabled = true; rf.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        var nf = fumaca.noise; nf.enabled = true; nf.strength = 2.2f; nf.frequency = 0.25f;
        // brasas: pontinhos luminosos que sobem
        brasas = NovoSistema("Brasas sombrias", 900);
        var cb = brasas.colorOverLifetime; cb.enabled = true; Gradient gb = new Gradient();
        gb.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 1f), 0f), new GradientColorKey(new Color(0.8f, 0.25f, 1f), 0.3f), new GradientColorKey(new Color(0.45f, 0.05f, 0.5f), 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        cb.color = new ParticleSystem.MinMaxGradient(gb);
        var tb = brasas.sizeOverLifetime; tb.enabled = true; tb.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
        var nb = brasas.noise; nb.enabled = true; nb.strength = 3f; nb.frequency = 0.5f;
        // halo escuro e núcleo violeta (quadros virados para a câmera)
        halo = FxCena.Nuvem(transform.position, tamanho * 2.6f, new Color(0.03f, 0f, 0.06f, 0f), FxCena.TexBrilho(), -2); halo.transform.SetParent(transform, true);
        nucleo = FxCena.Nuvem(transform.position, tamanho * 1.5f, Roxo(0f), FxCena.TexBrilho(), -1); nucleo.transform.SetParent(transform, true);
        olhoBrilho = FxCena.Nuvem(transform.position, 3.2f, new Color(1f, 0.12f, 0.1f, 0f), FxCena.TexBrilho(), 3); olhoBrilho.transform.SetParent(transform, true);
        // luzes
        GameObject lc = new GameObject("Luz da aura"); lc.transform.SetParent(transform, false);
        luzCorpo = lc.AddComponent<Light>(); luzCorpo.type = LightType.Point; luzCorpo.color = new Color(0.6f, 0.15f, 1f); luzCorpo.range = tamanho * 3f; luzCorpo.intensity = 0f; luzCorpo.shadows = LightShadows.None;
        GameObject lo = new GameObject("Luz dos olhos"); lo.transform.SetParent(transform, false);
        luzOlhos = lo.AddComponent<Light>(); luzOlhos.type = LightType.Point; luzOlhos.color = new Color(1f, 0.1f, 0.12f); luzOlhos.range = 18f; luzOlhos.intensity = 0f; luzOlhos.shadows = LightShadows.None;
        // anéis de energia no chão
        for (int i = 0; i < aneis.Length; i++)
        {
            GameObject g = new GameObject("Anel de energia " + i); g.transform.SetParent(transform, false);
            LineRenderer lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = FxCena.Sprite(); lr.useWorldSpace = true; lr.loop = true; lr.positionCount = 56; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.enabled = false;
            aneis[i] = lr;
        }
        ac1 = 0f; ac2 = 0.33f; ac3 = 0.66f;
    }

    static float Chao(Vector3 p) { Terrain t = Terrain.activeTerrain; return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y; }

    Vector3 PontoDoCorpo()
    {
        if (pontos.Count == 0) return centro != null ? centro.position : transform.position;
        Transform t = pontos[Random.Range(0, pontos.Count)]; return t != null ? t.position : transform.position;
    }

    void Update()
    {
        if (fumaca == null) return;
        atual = Mathf.MoveTowards(atual, intensidade, Time.deltaTime * 0.45f);
        float k = Mathf.Clamp01(atual), forte = Mathf.Max(0f, atual - 1f);
        float dt = Time.deltaTime; float esc = tamanho / 26f;
        Vector3 c = centro != null ? centro.position : transform.position;
        // fumaça e brasas saem dos ossos
        if (k > 0.01f)
        {
            int nf = Mathf.RoundToInt((38f * k + 90f * forte) * dt + Random.value);
            for (int i = 0; i < nf; i++)
            {
                var ep = new ParticleSystem.EmitParams(); ep.position = PontoDoCorpo() + Random.insideUnitSphere * 1.2f * esc;
                ep.velocity = new Vector3(Random.Range(-2f, 2f), Random.Range(3f, 9f), Random.Range(-2f, 2f)) * (0.7f + 0.5f * esc);
                ep.startSize = Random.Range(5f, 11f) * esc; ep.startLifetime = Random.Range(1.6f, 2.8f); ep.startColor = Color.white; fumaca.Emit(ep, 1);
            }
            int nb = Mathf.RoundToInt((30f * k + 70f * forte) * dt + Random.value);
            for (int i = 0; i < nb; i++)
            {
                var ep = new ParticleSystem.EmitParams(); ep.position = PontoDoCorpo() + Random.insideUnitSphere * 1.6f * esc;
                ep.velocity = new Vector3(Random.Range(-3f, 3f), Random.Range(5f, 14f), Random.Range(-3f, 3f)); ep.startSize = Random.Range(0.5f, 1.3f) * (0.6f + 0.6f * esc); ep.startLifetime = Random.Range(1.5f, 3.2f); ep.startColor = Random.value < 0.25f ? new Color(1f, 0.25f, 0.25f) : Color.white; brasas.Emit(ep, 1);
            }
            // arcos de raio roxo estalando em volta do corpo
            proxRaio -= dt;
            if (proxRaio <= 0f)
            {
                proxRaio = Random.Range(0.12f, 0.45f) / Mathf.Max(0.25f, k + forte);
                Vector3 a = PontoDoCorpo(), b = a + Random.onUnitSphere * Random.Range(5f, 12f) * esc; b.y = Mathf.Max(b.y, Chao(b) + 0.5f);
                FxCena.Raio(a, b, 0.35f * esc + 0.12f, 0.11f, new Color(0.75f, 0.35f, 1f, 1f));
            }
        }
        // anéis no chão: nascem pequenos, crescem ondulando e somem
        float velAnel = 0.42f;
        ac1 = (ac1 + dt * velAnel) % 1f; ac2 = (ac2 + dt * velAnel) % 1f; ac3 = (ac3 + dt * velAnel) % 1f;
        Anel(aneis[0], c, ac1, k, esc, 0f); Anel(aneis[1], c, ac2, k, esc, 2f); Anel(aneis[2], c, ac3, k, esc, 4f);
        // luzes e halo
        float pulso = 0.85f + 0.15f * Mathf.Sin(Time.time * 5f) + 0.25f * Mathf.Sin(Time.time * 13f) * 0.5f;
        luzCorpo.transform.position = c; luzCorpo.intensity = (2.4f * k + 6f * forte) * pulso;
        Transform cab = cabeca != null ? cabeca : transform;
        luzOlhos.transform.position = cab.position + Vector3.up * 1.5f; luzOlhos.intensity = 8f * k * pulso;
        Camera cam = Camera.main; Vector3 atras = cam != null ? (c - cam.transform.position).normalized : Vector3.forward;
        halo.transform.position = c + atras * tamanho * 0.35f; halo.transform.localScale = Vector3.one * tamanho * (2.4f + 0.3f * pulso) * (0.6f + 0.4f * k);
        FxCena.Pintar(halo, new Color(0.03f, 0f, 0.06f, 0.62f * k));
        nucleo.transform.position = c + atras * tamanho * 0.2f; nucleo.transform.localScale = Vector3.one * tamanho * (1.3f + 0.25f * pulso) * (0.5f + 0.5f * k);
        FxCena.Pintar(nucleo, Roxo(0.26f * k + 0.3f * forte));
        olhoBrilho.transform.position = cab.position + cab.forward * 0.6f * esc + Vector3.up * 1.1f * esc; olhoBrilho.transform.localScale = Vector3.one * 3.5f * (0.8f + 0.3f * pulso) * esc;
        FxCena.Pintar(olhoBrilho, new Color(1f, 0.12f, 0.12f, 0.9f * k));
    }

    void Anel(LineRenderer lr, Vector3 c, float f, float k, float esc, float fase)
    {
        if (lr == null) return; lr.enabled = k > 0.02f; if (!lr.enabled) return;
        float r = Mathf.Lerp(3f, 22f, f) * esc * 1.1f;
        for (int i = 0; i < lr.positionCount; i++)
        {
            float a = i / (float)lr.positionCount * Mathf.PI * 2f; float rr = r * (1f + 0.09f * Mathf.Sin(a * 9f + Time.time * 5f + fase) + 0.04f * Mathf.Sin(a * 17f - Time.time * 3f));
            Vector3 p = new Vector3(c.x + Mathf.Cos(a) * rr, 0f, c.z + Mathf.Sin(a) * rr); p.y = Chao(p) + 0.35f; lr.SetPosition(i, p);
        }
        float al = Mathf.Sin(f * Mathf.PI) * k; lr.startWidth = lr.endWidth = Mathf.Lerp(1.4f, 0.4f, f) * Mathf.Max(0.6f, esc);
        lr.startColor = lr.endColor = Color.Lerp(new Color(0.75f, 0.2f, 1f, al), new Color(1f, 0.15f, 0.3f, al), f * 0.5f);
    }

    void OnDestroy()
    {
        if (halo != null) Destroy(halo); if (nucleo != null) Destroy(nucleo); if (olhoBrilho != null) Destroy(olhoBrilho);
    }
}
