using System;
using System.Collections;
using UnityEngine;

// Chicote de aura negra: uma tira escura e ondulada que sai de um ponto, se estica até outro e solta fumaça pelo caminho.
// Também guarda uma nuvem de fumaça pronta (Nuvem) para as cenas.
public class ChicoteNegro : MonoBehaviour
{
    private LineRenderer lr; private ParticleSystem ps;
    private Func<Vector3> origem, destino;
    private float ext = 0f; private bool ativo = true; private float amp = 0.45f, fase, vel = 22f; private bool soltaFumaca = true;
    private const int N = 20;

    public static ChicoteNegro Criar(Func<Vector3> origem, Func<Vector3> destino, float largura = 0.34f, float amplitude = 0.45f, float fase = 0f, float velOnda = 22f, bool fumaca = true)
    {
        GameObject g = new GameObject("Chicote negro");
        ChicoteNegro c = g.AddComponent<ChicoteNegro>();
        c.origem = origem; c.destino = destino; c.amp = amplitude; c.fase = fase; c.vel = velOnda; c.soltaFumaca = fumaca;
        c.lr = g.AddComponent<LineRenderer>();
        c.lr.material = new Material(Shader.Find("Sprites/Default"));
        c.lr.positionCount = N; c.lr.numCapVertices = 6; c.lr.numCornerVertices = 4; c.lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        c.lr.startWidth = largura; c.lr.endWidth = largura * 0.3f;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(new Color(0.03f, 0f, 0.06f), 0f), new GradientColorKey(new Color(0.3f, 0.06f, 0.42f), 1f) },
                   new[] { new GradientAlphaKey(0.97f, 0f), new GradientAlphaKey(0.9f, 1f) });
        c.lr.colorGradient = gr;
        c.ps = g.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule m = c.ps.main; m.playOnAwake = false; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 500; m.startSpeed = 0f; m.startLifetime = 1f; m.startSize = 0.5f;
        ParticleSystem.EmissionModule em = c.ps.emission; em.enabled = false; ParticleSystem.ShapeModule sh = c.ps.shape; sh.enabled = false;
        ParticleSystem.ColorOverLifetimeModule col = c.ps.colorOverLifetime; col.enabled = true;
        Gradient g2 = new Gradient(); g2.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) }); col.color = g2;
        ParticleSystem.SizeOverLifetimeModule sz = c.ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.7f));
        c.ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        c.ps.Play();
        return c;
    }

    // o chicote se estica da origem até o destino
    public IEnumerator Estender(float seg)
    {
        for (float t = 0f; t < seg; t += Time.deltaTime) { ext = Mathf.Clamp01(t / seg); yield return null; }
        ext = 1f;
    }

    public void Desfazer() { ativo = false; if (lr != null) lr.enabled = false; Destroy(gameObject, 2f); }

    private void LateUpdate()
    {
        if (!ativo || origem == null || destino == null) return;
        Vector3 a = origem(), b = destino();
        Vector3 fim = Vector3.Lerp(a, b, ext);
        Vector3 dir = (fim - a); float comp = dir.magnitude; Vector3 n = comp > 0.01f ? dir / comp : Vector3.forward;
        Vector3 perp = Vector3.Cross(n, Vector3.up); if (perp.sqrMagnitude < 0.001f) perp = Vector3.right; perp.Normalize();
        Vector3 cima = Vector3.Cross(perp, n);
        for (int i = 0; i < N; i++)
        {
            float s = i / (float)(N - 1), env = Mathf.Sin(s * Mathf.PI);
            float am = amp * env * (ext < 1f ? 1.4f : 1f);
            Vector3 p = Vector3.Lerp(a, fim, s) + perp * Mathf.Sin(Time.time * vel + s * 11f + fase) * am + cima * Mathf.Cos(Time.time * vel * 0.77f + s * 9f + fase) * am * 0.7f;
            lr.SetPosition(i, p);
        }
        // fumaça escura soltando do chicote
        for (int k = 0; soltaFumaca && k < 3; k++)
        {
            float s = UnityEngine.Random.value;
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = Vector3.Lerp(a, fim, s) + UnityEngine.Random.insideUnitSphere * 0.25f;
            ep.velocity = UnityEngine.Random.insideUnitSphere * 0.8f + Vector3.up * 0.5f;
            ep.startSize = UnityEngine.Random.Range(0.3f, 0.8f); ep.startLifetime = UnityEngine.Random.Range(0.6f, 1.3f);
            ep.startColor = new Color(0.06f, 0.02f, 0.1f, 0.65f);
            ps.Emit(ep, 1);
        }
    }

    // nuvem de fumaça (explosão) numa posição
    public static void Nuvem(Vector3 pos, Color cor, int n, float raio)
    {
        GameObject g = new GameObject("Fumaça"); g.transform.position = pos;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main; main.duration = 0.4f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f); main.startColor = cor; main.gravityModifier = -0.03f; main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)n) });
        ParticleSystem.ShapeModule sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = raio;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }); col.color = gr;
        g.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        ps.Play(); Destroy(g, 4f);
    }
}
