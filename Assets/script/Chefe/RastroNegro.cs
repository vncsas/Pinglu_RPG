using System.Collections.Generic;
using UnityEngine;

// RASTRO NEGRO do Ladrão: uma trilha escura e brilhante no chão (fita preta com borda roxa) e fumaça subindo dela,
// que vai de onde ele fugiu até o portal. "revelado" diz quantos pontos já aparecem (a trilha vai surgindo conforme ele corre).
// A fumaça só é criada perto da câmera (barato mesmo com a trilha longa). Dissipar() apaga tudo devagar.
public class RastroNegro : MonoBehaviour
{
    public readonly List<Vector3> pontos = new List<Vector3>();
    public float revelado;
    public float Espacamento { get; private set; }

    LineRenderer fita, brilho; ParticleSystem fumaca, faiscas; float fade = 1f, proxEmit, apagarEm = -1f; int contAplicado = -1;

    static float Chao(Vector3 p) { Terrain t = Terrain.activeTerrain; return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y; }

    public static RastroNegro Criar(Vector3 de, Vector3 ate)
    {
        GameObject g = new GameObject("Rastro negro"); RastroNegro r = g.AddComponent<RastroNegro>(); r.Montar(de, ate); return r;
    }

    // ponto da trilha na distância "s" (em metros) desde o começo
    public Vector3 Em(float s)
    {
        if (pontos.Count == 0) return transform.position;
        float idx = Mathf.Clamp(s / Mathf.Max(0.1f, Espacamento), 0f, pontos.Count - 1.001f); int i = Mathf.FloorToInt(idx);
        return Vector3.Lerp(pontos[i], pontos[i + 1], idx - i);
    }
    public float Comprimento { get { return Mathf.Max(0f, (pontos.Count - 1) * Espacamento); } }

    void Montar(Vector3 de, Vector3 ate)
    {
        Vector3 d = ate - de; d.y = 0f; float len = Mathf.Max(10f, d.magnitude); Vector3 dir = d / len, lado = Vector3.Cross(Vector3.up, dir);
        int n = Mathf.Max(3, Mathf.CeilToInt(len / 4f)); Espacamento = len / (n - 1);
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)(n - 1); Vector3 p = de + dir * len * k + lado * (Mathf.Sin(k * Mathf.PI * 5f + 1f) * 7f * Mathf.Sin(k * Mathf.PI));
            p.y = Chao(p) + 0.35f; pontos.Add(p);
        }
        fita = NovaFita("Fita", 3.0f, new Color(0.02f, 0f, 0.05f, 0.95f)); brilho = NovaFita("Brilho", 6.5f, new Color(0.5f, 0.12f, 0.9f, 0.3f));
        fumaca = NovoPS("Fumaca do rastro", 700, new Color(0.1f, 0.03f, 0.16f), true); faiscas = NovoPS("Faiscas do rastro", 300, new Color(0.85f, 0.45f, 1f), false);
    }

    LineRenderer NovaFita(string nome, float largura, Color cor)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(transform, false); g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // deita a fita no chão
        LineRenderer lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = FxCena.Sprite(); lr.useWorldSpace = true; lr.alignment = LineAlignment.TransformZ;
        lr.numCornerVertices = 3; lr.numCapVertices = 3; lr.startWidth = lr.endWidth = largura; lr.startColor = lr.endColor = cor; lr.positionCount = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; return lr;
    }
    ParticleSystem NovoPS(string nome, int max, Color baseCor, bool suave)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(transform, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = max; m.startLifetime = 2.5f; m.startSize = 2f;
        var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true; Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(suave ? 0.7f : 1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        if (suave) { var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f)); var nz = ps.noise; nz.enabled = true; nz.strength = 1.2f; nz.frequency = 0.3f; }
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        Material mt = new Material(FxCena.Sprite()); mt.mainTexture = suave ? FxCena.TexNuvem() : FxCena.TexBrilho(); r.sharedMaterial = mt;
        ps.Play(); return ps;
    }

    public void Dissipar() { if (apagarEm < 0f) apagarEm = Time.time; }

    void Update()
    {
        if (apagarEm >= 0f) { fade = 1f - (Time.time - apagarEm) / 3f; if (fade <= 0f) { Destroy(gameObject); return; } }
        int n = Mathf.Clamp(Mathf.FloorToInt(revelado) + 1, 0, pontos.Count);
        if (n != contAplicado)
        {
            contAplicado = n; Vector3[] arr = new Vector3[n]; for (int i = 0; i < n; i++) arr[i] = pontos[i];
            fita.positionCount = n; fita.SetPositions(arr); brilho.positionCount = n; brilho.SetPositions(arr);
        }
        float pulso = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
        fita.startColor = fita.endColor = new Color(0.02f, 0f, 0.05f, 0.95f * fade);
        brilho.startColor = brilho.endColor = new Color(0.5f, 0.12f, 0.9f, 0.3f * pulso * fade);
        // fumaça e faíscas só perto da câmera
        proxEmit -= Time.deltaTime; if (proxEmit > 0f || n < 2 || fade < 0.2f) return; proxEmit = 0.07f;
        Camera cam = Camera.main; Vector3 c = cam != null ? cam.transform.position : Vector3.zero;
        for (int k = 0; k < 7; k++)
        {
            int i = Random.Range(0, n); Vector3 p = pontos[i]; if ((p - c).sqrMagnitude > 85f * 85f) continue;
            var ep = new ParticleSystem.EmitParams(); ep.position = p + new Vector3(Random.Range(-1.6f, 1.6f), 0.2f, Random.Range(-1.6f, 1.6f));
            ep.velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(1.5f, 3.8f), Random.Range(-0.6f, 0.6f)); ep.startSize = Random.Range(2.2f, 4.5f); ep.startLifetime = Random.Range(2.2f, 3.4f);
            ep.startColor = new Color(0.12f, 0.03f, 0.2f, fade); fumaca.Emit(ep, 1);
            if (Random.value < 0.35f)
            {
                var fp = new ParticleSystem.EmitParams(); fp.position = p + new Vector3(Random.Range(-1.5f, 1.5f), 0.3f, Random.Range(-1.5f, 1.5f)); fp.velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(2f, 5f), Random.Range(-1f, 1f));
                fp.startSize = Random.Range(0.25f, 0.6f); fp.startLifetime = Random.Range(1f, 2f); fp.startColor = new Color(0.85f, 0.45f, 1f, fade); faiscas.Emit(fp, 1);
            }
        }
    }
}
