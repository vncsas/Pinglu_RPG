using UnityEngine;

// Efeito de cura de teste, chamado pelo Animation Event "LancarCura" (quadro do disparo de Curar_Lancar):
//  - um anel verde se abre no chao em volta do curandeiro e brilhos sobem em espiral (a cura "carregando")
//  - uma esfera de luz sai da palma esquerda (osso MagicHand_L) PARA A FRENTE do personagem, com rastro, numa
//    curva suave, e ao chegar se abre num anel de cura no aliado
// Tudo e so visual de teste: no jogo troque por particulas/VFX de verdade usando os mesmos ossos.
public class CuraCurandeiro : MonoBehaviour
{
    public Transform maoEsquerda, maoDireita, centro;
    public Color cor = new Color(0.35f, 1f, 0.55f);
    public float alcance = 6f;

    void Reset() { AcharOssos(); }

    public void AcharOssos()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "MagicHand_L") maoEsquerda = t;
            if (t.name == "MagicHand_R") maoDireita = t;
            if (t.name == "HealCenter") centro = t;
        }
    }

    public void LancarCura()
    {
        if (!maoEsquerda) AcharOssos();
        var chao = new Vector3(transform.position.x, transform.position.y + 0.02f, transform.position.z);
        AnelCura.Criar(chao, 1.4f, cor, 0.9f);
        for (int i = 0; i < 10; i++) BrilhoCura.Criar(chao, i * 36f, cor);
        if (maoEsquerda)
        {
            var alvo = maoEsquerda.position + transform.forward * alcance;
            alvo.y = transform.position.y + 1.0f;                                  // altura do peito do aliado
            OrbeCura.Criar(maoEsquerda.position, alvo, cor);
        }
    }

    public static Material Mat()
    {
        if (!mat) mat = new Material(Shader.Find("Sprites/Default"));
        return mat;
    }
    static Material mat;
}

public class OrbeCura : MonoBehaviour
{
    Vector3 a, b; float t0; const float dur = 0.7f; Color cor; Light luz; Renderer rd;
    public static void Criar(Vector3 de, Vector3 para, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>());
        go.transform.position = de; go.transform.localScale = Vector3.one * 0.18f;
        var o = go.AddComponent<OrbeCura>(); o.a = de; o.b = para; o.t0 = Time.time; o.cor = cor;
        o.rd = go.GetComponent<Renderer>(); o.rd.sharedMaterial = CuraCurandeiro.Mat(); o.rd.material.color = Color.Lerp(cor, Color.white, 0.4f);
        var tr = go.AddComponent<TrailRenderer>(); tr.time = 0.35f; tr.sharedMaterial = CuraCurandeiro.Mat();
        tr.widthCurve = new AnimationCurve(new Keyframe(0, 0.16f), new Keyframe(1, 0f));
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(cor, 0), new GradientColorKey(cor, 1) },
                                          new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0f, 1) });
        tr.colorGradient = g;
        o.luz = go.AddComponent<Light>(); o.luz.color = cor; o.luz.range = 3f; o.luz.intensity = 3f;
    }
    void Update()
    {
        float k = (Time.time - t0) / dur;
        if (k >= 1f) { AnelCura.Criar(b - Vector3.up * 0.98f, 1.0f, cor, 0.6f); Destroy(gameObject); return; }
        float e = Mathf.SmoothStep(0f, 1f, k);
        transform.position = Vector3.Lerp(a, b, e) + Vector3.up * Mathf.Sin(Mathf.PI * e) * 0.6f;   // arco suave
        transform.localScale = Vector3.one * (0.18f + 0.03f * Mathf.Sin(Time.time * 30f));
    }
}

public class AnelCura : MonoBehaviour
{
    LineRenderer lr; float t0, raio, dur; Color cor; const int N = 48;
    public static void Criar(Vector3 centro, float raio, Color cor, float duracao)
    {
        var go = new GameObject("AnelCura"); go.transform.position = centro;
        var r = go.AddComponent<AnelCura>(); r.t0 = Time.time; r.raio = raio; r.cor = cor; r.dur = duracao;
        r.lr = go.AddComponent<LineRenderer>(); r.lr.loop = true; r.lr.positionCount = N; r.lr.useWorldSpace = false;
        r.lr.sharedMaterial = CuraCurandeiro.Mat(); r.lr.widthMultiplier = 0.08f;
        var l = go.AddComponent<Light>(); l.color = cor; l.range = raio * 2f; l.intensity = 2f;
    }
    void Update()
    {
        float k = (Time.time - t0) / dur;
        if (k >= 1f) { Destroy(gameObject); return; }
        float r = raio * Mathf.SmoothStep(0.15f, 1f, k);
        for (int i = 0; i < N; i++) { float ang = i * Mathf.PI * 2f / N; lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r)); }
        var c = cor; c.a = 1f - k; lr.startColor = lr.endColor = c; lr.widthMultiplier = 0.12f * (1f - k) + 0.02f;
        GetComponent<Light>().intensity = 2f * (1f - k);
    }
}

public class BrilhoCura : MonoBehaviour
{
    Vector3 centro; float ang0, t0; Material m; Color cor; const float dur = 1.2f;
    public static void Criar(Vector3 centro, float angulo, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * 0.06f;
        var b = go.AddComponent<BrilhoCura>(); b.centro = centro; b.ang0 = angulo; b.t0 = Time.time + Random.Range(0f, 0.25f); b.cor = cor;
        b.m = new Material(CuraCurandeiro.Mat()); go.GetComponent<Renderer>().sharedMaterial = b.m;
        go.transform.position = centro;
    }
    void Update()
    {
        float k = (Time.time - t0) / dur;
        if (k < 0f) { m.color = Color.clear; return; }
        if (k >= 1f) { Destroy(m); Destroy(gameObject); return; }
        float ang = (ang0 + 220f * k) * Mathf.Deg2Rad, r = 0.7f * (1f - 0.5f * k);
        transform.position = centro + new Vector3(Mathf.Cos(ang) * r, 2.0f * k, Mathf.Sin(ang) * r);   // sobe em espiral
        var c = Color.Lerp(cor, Color.white, 0.3f); c.a = Mathf.Sin(Mathf.PI * k); m.color = c;
    }
}
