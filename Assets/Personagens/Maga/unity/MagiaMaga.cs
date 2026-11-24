using UnityEngine;

// Recebe o Animation Event "LancarMagia" (quadro do disparo de Magia_Lancar / Flutuar_Magia_Lancar)
// e solta uma bola de magia simples da palma esquerda (osso MagicHand_L), na direcao em que a mao aponta.
// O evento "SoltarRaio" (Raio_Cajado / Flutuar_Raio) solta um raio do cristal do cajado (osso StaffTip).
// E so um efeito de teste: no jogo troque pelo projetil/VFX de verdade.
public class MagiaMaga : MonoBehaviour
{
    public Transform maoMagica, pontaCajado;
    public Color cor = new Color(0.55f, 0.35f, 1f);
    public float velocidade = 9f, duracao = 2f, tamanho = 0.18f;
    Material mat;

    void Reset() { AcharOssos(); }

    public void AcharOssos()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "MagicHand_L") maoMagica = t;
            if (t.name == "StaffTip") pontaCajado = t;
        }
    }

    public float alcanceRaio = 10f;
    public Color corRaio = new Color(0.62f, 0.12f, 1f);      // roxo

    public void SoltarRaio()
    {
        if (!pontaCajado) AcharOssos();
        if (!pontaCajado) return;
        var de = pontaCajado.position;
        var dir = pontaCajado.up; dir.y = Mathf.Clamp(dir.y, -0.15f, 0.15f); dir.Normalize();   // o cristal aponta pro alvo
        Raio.Criar(pontaCajado, dir * alcanceRaio, corRaio, 0.6f);
    }

    public void LancarMagia()
    {
        if (!maoMagica) AcharOssos();
        if (!maoMagica) return;
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", cor * 2f);
        }
        var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(bola.GetComponent<Collider>());
        bola.transform.position = maoMagica.position;
        bola.transform.localScale = Vector3.one * tamanho;
        bola.GetComponent<Renderer>().sharedMaterial = mat;
        var luz = bola.AddComponent<Light>(); luz.color = cor; luz.range = 3f; luz.intensity = 3f;
        // direcao: para onde a palma aponta, sem subir/descer demais
        var dir = maoMagica.up; dir.y *= 0.3f; dir.Normalize();
        bola.AddComponent<BolaDeMagia>().Iniciar(dir * velocidade, duracao);
    }
}

public class Raio : MonoBehaviour
{
    // Raio roxo que sai do cristal do cajado:
    //  - cresce da ponta ate o alvo em ~0,07 s, fica, e some aos poucos (largura e brilho caem juntos)
    //  - forma de zigue-zague que ondula suave (ruido Perlin no tempo, nao sorteio a cada quadro)
    //  - duas camadas: nucleo fino quase branco + halo roxo largo e transparente; 3 galhos menores
    //  - o comeco acompanha o cajado se ela se mexer; uma luz roxa pisca junto
    const int N = 24;
    Transform origem; Vector3 desloc; Color cor; float t0, dur, semente;
    LineRenderer nucleo, halo; LineRenderer[] galhos; float[] galhoPos; Vector3[] galhoDir; Light luz;
    static Material mat;

    public static void Criar(Transform origem, Vector3 deslocamento, Color cor, float duracao)
    {
        var go = new GameObject("Raio"); var r = go.AddComponent<Raio>();
        r.origem = origem; r.desloc = deslocamento; r.cor = cor; r.dur = duracao; r.t0 = Time.time; r.semente = Random.value * 100f;
        if (!mat) mat = new Material(Shader.Find("Sprites/Default"));   // usa a cor do vertice, com transparencia
        r.halo = r.Linha(go, N, 0.9f);
        r.nucleo = r.Linha(go, N, 0.25f);
        r.galhos = new LineRenderer[3]; r.galhoPos = new float[3]; r.galhoDir = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            r.galhos[i] = r.Linha(go, 8, 0.12f); r.galhoPos[i] = Random.Range(0.25f, 0.8f);
            r.galhoDir[i] = (Random.onUnitSphere + deslocamento.normalized * 0.8f).normalized * Random.Range(0.8f, 1.6f);
        }
        r.luz = go.AddComponent<Light>(); r.luz.color = cor; r.luz.range = 7f;
    }

    LineRenderer Linha(GameObject pai, int n, float largura)
    {
        var g = new GameObject("linha"); g.transform.SetParent(pai.transform, false);
        var lr = g.AddComponent<LineRenderer>(); lr.positionCount = n; lr.sharedMaterial = mat; lr.useWorldSpace = true;
        lr.numCapVertices = 4; lr.numCornerVertices = 2; lr.widthMultiplier = largura;
        lr.widthCurve = new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.1f, 1f), new Keyframe(1, 0.25f));
        return lr;
    }

    Vector3 Onda(Vector3 a, Vector3 dir, float t, float amp, float fase, float tempo)
    {
        var f = dir.normalized; var lado = Vector3.Cross(f, Vector3.up); if (lado.sqrMagnitude < 1e-4f) lado = Vector3.right;
        lado.Normalize(); var cima = Vector3.Cross(lado, f);
        float env = Mathf.Sin(Mathf.PI * t);                                   // presa nas duas pontas
        float nx = Mathf.PerlinNoise(t * 6f + fase, tempo * 3f) * 2f - 1f;    // ondula suave no tempo
        float ny = Mathf.PerlinNoise(t * 6f + fase + 40f, tempo * 3f) * 2f - 1f;
        float zig = (Mathf.PerlinNoise(t * 22f + fase, tempo * 9f) * 2f - 1f) * 0.35f;   // dentinhos finos
        return a + dir * t + (lado * (nx + zig) + cima * ny) * amp * env;
    }

    void Update()
    {
        float idade = Time.time - t0, k = idade / dur;
        if (k >= 1f) { Destroy(gameObject); return; }
        float cresce = Mathf.Clamp01(idade / 0.07f);                            // 0..1 nos primeiros quadros
        float some = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, k));
        var a = origem ? origem.position : transform.position;
        var dir = desloc * cresce; float amp = 0.35f * cresce;
        var nucleoCor = Color.Lerp(cor, Color.white, 0.6f);
        for (int i = 0; i < N; i++)
        {
            var p = Onda(a, dir, i / (float)(N - 1), amp, semente, idade);
            halo.SetPosition(i, p); nucleo.SetPosition(i, p);
        }
        Pintar(halo, cor, 0.35f * some); Pintar(nucleo, nucleoCor, some);
        halo.widthMultiplier = 0.9f * (0.4f + 0.6f * some); nucleo.widthMultiplier = 0.09f * (0.3f + 0.7f * some);
        for (int g = 0; g < galhos.Length; g++)
        {
            var b = Onda(a, dir, galhoPos[g], amp, semente, idade);
            var gd = galhoDir[g] * cresce;
            for (int i = 0; i < 8; i++) galhos[g].SetPosition(i, Onda(b, gd, i / 7f, 0.15f, semente + g * 13f, idade));
            Pintar(galhos[g], nucleoCor, 0.8f * some * (1f - Mathf.InverseLerp(0.2f, 0.7f, k)));
            galhos[g].widthMultiplier = 0.05f * some;
        }
        luz.intensity = 8f * some * (0.75f + 0.25f * Mathf.PerlinNoise(idade * 20f, semente));
        luz.transform.position = a + dir * 0.4f;
    }

    static void Pintar(LineRenderer lr, Color c, float alfa)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                  new[] { new GradientAlphaKey(alfa, 0f), new GradientAlphaKey(alfa, 0.8f), new GradientAlphaKey(0f, 1f) });
        lr.colorGradient = g;
    }
}

public class BolaDeMagia : MonoBehaviour
{
    Vector3 vel; float fim;
    public void Iniciar(Vector3 v, float d) { vel = v; fim = Time.time + d; }
    void Update()
    {
        transform.position += vel * Time.deltaTime;
        if (Time.time > fim) Destroy(gameObject);
    }
}
