using UnityEngine;

// Efeitos de teste chamados pelos Animation Events dos ataques do golem:
//   ImpactoSoco   (Soco_Direto, Soco_Pesado, Varrer, Esmagar): onda de choque no chao onde o punho bateu + tremor de camera
//   ImpactoPisada (Pisada): onda grande em volta dos pes + tremor forte
//   ImpactoPeito  (Rugir): pulso no peito (osso ShieldCenter) cada vez que ele bate no peito
// Os ossos MagicHand_L/R (punhos) dao o ponto do golpe. No jogo troque por VFX/som e use os mesmos eventos para dano.
public class ImpactoGolem : MonoBehaviour
{
    public Transform maoEsquerda, maoDireita, peito;
    public Color cor = new Color(0.85f, 0.78f, 0.6f);
    float tremor, tremorDur = 0.01f; Camera cam;

    void Reset() { AcharOssos(); }
    public void AcharOssos()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "MagicHand_L") maoEsquerda = t;
            if (t.name == "MagicHand_R") maoDireita = t;
            if (t.name == "ShieldCenter") peito = t;
        }
    }
    Vector3 PontoDoGolpe()
    {
        if (!maoEsquerda || !maoDireita) AcharOssos();
        Transform m = maoDireita;
        if (maoEsquerda && maoDireita && Vector3.Dot(maoEsquerda.position - transform.position, transform.forward) > Vector3.Dot(maoDireita.position - transform.position, transform.forward)) m = maoEsquerda;
        return m ? m.position : transform.position + transform.forward * 1.5f + Vector3.up;
    }
    public void ImpactoSoco()
    {
        var p = PontoDoGolpe();
        OndaGolem.Criar(new Vector3(p.x, transform.position.y + 0.04f, p.z), 2.2f, cor, 0.55f);
        Tremer(0.12f, 0.35f);
    }
    public void ImpactoPisada()
    {
        var c = transform.position + transform.forward * 0.6f + Vector3.up * 0.04f;
        OndaGolem.Criar(c, 4.5f, cor, 0.8f);
        OndaGolem.Criar(c, 2.5f, Color.white, 0.4f);
        Tremer(0.3f, 0.6f);
    }
    public void ImpactoPeito()
    {
        if (!peito) AcharOssos();
        if (peito) OndaGolem.Criar(peito.position, 1.0f, new Color(1f, 0.9f, 0.6f), 0.35f, true);
        Tremer(0.06f, 0.25f);
    }
    void Tremer(float forca, float dur) { tremor = forca; tremorDur = dur; if (!cam) cam = Camera.main; }
    void LateUpdate()
    {
        if (tremor <= 0.001f || !cam) return;
        cam.transform.position += Random.insideUnitSphere * tremor;
        tremor = Mathf.MoveTowards(tremor, 0f, tremor / tremorDur * Time.deltaTime);
    }
}

public class OndaGolem : MonoBehaviour
{
    LineRenderer lr; float t0, raio, dur; Color cor; Light luz; bool vertical; const int N = 48; static Material mat;
    public static void Criar(Vector3 centro, float raio, Color cor, float duracao, bool vertical = false)
    {
        if (!mat) mat = new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject("OndaGolem"); go.transform.position = centro;
        var o = go.AddComponent<OndaGolem>(); o.t0 = Time.time; o.raio = raio; o.cor = cor; o.dur = duracao; o.vertical = vertical;
        o.lr = go.AddComponent<LineRenderer>(); o.lr.loop = true; o.lr.positionCount = N; o.lr.useWorldSpace = false; o.lr.sharedMaterial = mat;
        o.luz = go.AddComponent<Light>(); o.luz.color = cor; o.luz.range = raio * 2f;
    }
    void Update()
    {
        float k = (Time.time - t0) / dur;
        if (k >= 1f) { Destroy(gameObject); return; }
        float r = raio * Mathf.SmoothStep(0.1f, 1f, k);
        for (int i = 0; i < N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            lr.SetPosition(i, vertical ? new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0) : new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
        }
        var c = cor; c.a = 1f - k; lr.startColor = lr.endColor = c; lr.widthMultiplier = 0.25f * (1f - k) + 0.03f;
        luz.intensity = 5f * (1f - k);
    }
}
