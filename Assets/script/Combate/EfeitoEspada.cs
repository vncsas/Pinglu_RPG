using System.Collections.Generic;
using UnityEngine;

// Corte de chama carmesim e sombra nos golpes de espada.
// Desenha um arco que segue a lâmina durante o golpe, solta brasas e
// faz uma explosão de faíscas quando acerta um inimigo.
[RequireComponent(typeof(PlayerLocomotion))]
public class EfeitoEspada : MonoBehaviour
{
    [Header("Lâmina (no espaço do osso da mão direita) - o menu Pinglu > 17 calcula")]
    public Vector3 baseDaLamina = new Vector3(0f, 0.12f, 0f);
    public Vector3 pontaDaLamina = new Vector3(0f, 1.0f, 0f);

    [Header("Materiais (o menu Pinglu > 17 preenche)")]
    public Material materialChama;
    public Material materialSombra;
    public Material materialBrasa;
    public Material materialFumaca;

    [Header("Tempo do efeito em cada golpe")]
    [Tooltip("Espera depois do clique até o corte aparecer (segundos)")]
    public float atrasoDoCorte = 0.08f;
    [Tooltip("Quanto tempo o corte fica sendo desenhado em cada golpe do combo")]
    public float[] duracaoDoCorte = { 0.35f, 0.35f, 0.5f };
    [Tooltip("Quanto tempo o rastro demora para sumir")]
    public float vidaDoRastro = 0.18f;
    public float vidaDaSombra = 0.35f;

    [Header("Aparência")]
    [ColorUsage(true, true)] public Color corDaChama = new Color(5f, 0.35f, 0.4f, 1f);
    [Tooltip("O 3º golpe do combo é maior e mais forte")]
    public float forcaDoFinalizador = 1.4f;

    private PlayerLocomotion loco;
    private CombateJogador combate;
    private Transform mao;
    private Rastro chama, sombra;
    private ParticleSystem brasas, faiscas, fumaca;
    private Light luz;
    private float comecaEm, terminaEm = -1f;
    private int golpe;

    private void Awake()
    {
        loco = GetComponent<PlayerLocomotion>();
        combate = GetComponent<CombateJogador>();
        Animator a = null;
        foreach (Animator x in GetComponentsInChildren<Animator>(true)) if (x.runtimeAnimatorController != null) { a = x; break; }
        if (a != null && a.isHuman) mao = a.GetBoneTransform(HumanBodyBones.RightHand);
        if (mao == null) { enabled = false; return; }

        chama = new Rastro("Rastro de Chama", materialChama, vidaDoRastro, 1f);
        sombra = new Rastro("Rastro de Sombra", materialSombra, vidaDaSombra, 0f);
        if (materialChama != null) materialChama.SetColor("_BaseColor", corDaChama);

        brasas = CriarParticulas("Brasas", materialBrasa, false);
        faiscas = CriarParticulas("Faiscas do Impacto", materialBrasa, true);
        fumaca = CriarParticulas("Fumaca do Impacto", materialFumaca, true);
        GameObject gl = new GameObject("Luz do Corte");
        luz = gl.AddComponent<Light>();
        luz.type = LightType.Point; luz.range = 6f; luz.intensity = 0f;
        luz.color = new Color(1f, 0.15f, 0.18f); luz.shadows = LightShadows.None;

        loco.AoIniciarGolpe += AoIniciarGolpe;
        if (combate != null) combate.AoAcertar += AoAcertar;
    }

    private void OnDestroy()
    {
        if (loco != null) loco.AoIniciarGolpe -= AoIniciarGolpe;
        if (combate != null) combate.AoAcertar -= AoAcertar;
        if (chama != null) chama.Destruir();
        if (sombra != null) sombra.Destruir();
        if (brasas != null) Destroy(brasas.gameObject);
        if (faiscas != null) Destroy(faiscas.gameObject);
        if (fumaca != null) Destroy(fumaca.gameObject);
        if (luz != null) Destroy(luz.gameObject);
    }

    private void AoIniciarGolpe(int indice)
    {
        golpe = indice;
        float dur = indice >= 0 && indice < duracaoDoCorte.Length ? duracaoDoCorte[indice] : 0.4f;
        comecaEm = Time.time + atrasoDoCorte;
        terminaEm = comecaEm + dur;
    }

    private void AoAcertar(Vector3 ponto)
    {
        bool forte = golpe == 2;
        faiscas.transform.position = ponto;
        fumaca.transform.position = ponto;
        faiscas.Emit(forte ? 45 : 25);
        fumaca.Emit(forte ? 8 : 4);
        luz.intensity = forte ? 6f : 4f;
    }

    // mostra a lâmina detectada (linha vermelha) quando o personagem está selecionado
    private void OnDrawGizmosSelected()
    {
        Transform m = mao;
        if (m == null)
        {
            foreach (Animator x in GetComponentsInChildren<Animator>(true))
                if (x.isHuman) { m = x.GetBoneTransform(HumanBodyBones.RightHand); if (m != null) break; }
        }
        if (m == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(m.TransformPoint(baseDaLamina), m.TransformPoint(pontaDaLamina));
        Gizmos.DrawSphere(m.TransformPoint(pontaDaLamina), 0.04f);
    }

    private void LateUpdate()
    {
        // (nas cópias dos outros jogadores o Player Locomotion fica desligado: vale só a janela do golpe)
        bool emitindo = (loco.Atacando || !loco.enabled) && Time.time >= comecaEm && Time.time <= terminaEm;
        float forca = golpe == 2 ? forcaDoFinalizador : 1f;

        Vector3 b = mao.TransformPoint(baseDaLamina);
        Vector3 p = mao.TransformPoint(pontaDaLamina);
        // no finalizador o arco passa um pouco da ponta da lâmina
        Vector3 pChama = b + (p - b) * (forca > 1f ? 1.12f : 1f);
        Vector3 pSombra = b + (p - b) * (1.25f * forca);

        chama.Atualizar(b, pChama, emitindo);
        sombra.Atualizar(Vector3.Lerp(b, p, -0.1f), pSombra, emitindo);

        // brasas saindo da lâmina
        brasas.transform.position = (b + p) * 0.5f;
        Vector3 dir = p - b;
        if (dir.sqrMagnitude > 0.0001f) brasas.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);
        var sh = brasas.shape; sh.radius = dir.magnitude * 0.5f;
        var em = brasas.emission; em.rateOverTime = emitindo ? 140f * forca : 0f;

        // luz vermelha acompanhando o corte
        luz.transform.position = (b + p) * 0.5f;
        float alvo = emitindo ? 2.5f * forca : 0f;
        luz.intensity = Mathf.MoveTowards(luz.intensity, alvo, Time.deltaTime * 25f);
    }

    private ParticleSystem CriarParticulas(string nome, Material mat, bool soNoImpacto)
    {
        GameObject go = new GameObject(nome);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        bool ehFumaca = mat == materialFumaca;
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 400;
        if (ehFumaca)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startColor = new Color(1f, 1f, 1f, 0.7f);
        }
        else
        {
            main.startLifetime = soNoImpacto ? new ParticleSystem.MinMaxCurve(0.25f, 0.5f) : new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = soNoImpacto ? new ParticleSystem.MinMaxCurve(4f, 9f) : new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = soNoImpacto ? new ParticleSystem.MinMaxCurve(0.04f, 0.1f) : new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.25f, 0.25f), new Color(1f, 0.6f, 0.5f));
            main.gravityModifier = soNoImpacto ? 0.6f : -0.15f;
        }
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape;
        sh.shapeType = soNoImpacto ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.SingleSidedEdge;
        sh.radius = soNoImpacto ? 0.15f : 0.5f;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var tam = ps.sizeOverLifetime; tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, ehFumaca ? AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f) : AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        if (!soNoImpacto) { var ruido = ps.noise; ruido.enabled = true; ruido.strength = 0.8f; ruido.frequency = 1.5f; }
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------------
    //  Faixa que segue a lâmina (base -> ponta), suavizada entre os quadros
    // ------------------------------------------------------------------
    private class Rastro
    {
        struct Amostra { public Vector3 b, p; public float t; }
        readonly List<Amostra> amostras = new List<Amostra>();
        readonly Mesh mesh;
        readonly GameObject go;
        readonly float vida;
        readonly float curvaAlfa;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color> cor = new List<Color>();
        readonly List<int> tri = new List<int>();
        const int SUBDIVISOES = 4;

        public Rastro(string nome, Material mat, float vida, float curvaAlfa)
        {
            this.vida = vida; this.curvaAlfa = curvaAlfa;
            go = new GameObject(nome);
            mesh = new Mesh { name = nome };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public void Destruir() { if (go != null) Object.Destroy(go); }

        public void Atualizar(Vector3 b, Vector3 p, bool emitindo)
        {
            float agora = Time.time;
            if (emitindo) amostras.Add(new Amostra { b = b, p = p, t = agora });
            while (amostras.Count > 0 && agora - amostras[0].t > vida) amostras.RemoveAt(0);

            mesh.Clear();
            if (amostras.Count < 2) return;
            v.Clear(); uv.Clear(); cor.Clear(); tri.Clear();
            int n = amostras.Count;
            for (int i = 0; i < n - 1; i++)
            {
                Amostra a0 = amostras[Mathf.Max(0, i - 1)], a1 = amostras[i], a2 = amostras[i + 1], a3 = amostras[Mathf.Min(n - 1, i + 2)];
                int passos = i == n - 2 ? SUBDIVISOES + 1 : SUBDIVISOES;
                for (int s = 0; s < passos; s++)
                {
                    float f = s / (float)SUBDIVISOES;
                    Vector3 pb = CatmullRom(a0.b, a1.b, a2.b, a3.b, f);
                    Vector3 pp = CatmullRom(a0.p, a1.p, a2.p, a3.p, f);
                    float t = Mathf.Lerp(a1.t, a2.t, f);
                    float idade = Mathf.Clamp01((agora - t) / vida);
                    float alfa = curvaAlfa > 0f ? Mathf.Pow(1f - idade, 0.6f) : 1f - idade;
                    v.Add(pb); v.Add(pp);
                    uv.Add(new Vector2(idade, 0f)); uv.Add(new Vector2(idade, 1f));
                    Color c = new Color(1f, 1f, 1f, alfa);
                    cor.Add(c); cor.Add(c);
                }
            }
            for (int k = 0; k + 3 < v.Count; k += 2)
            {
                tri.Add(k); tri.Add(k + 1); tri.Add(k + 2);
                tri.Add(k + 1); tri.Add(k + 3); tri.Add(k + 2);
            }
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetColors(cor);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
