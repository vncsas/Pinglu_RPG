using System;
using System.Collections;
using UnityEngine;

public enum PerfilHumanoide { CavaleiroAntigo, Ladrao }

// Inimigo humanoide de espada: usa o corpo e as animações do cavaleiro (andar, ataque leve, ataque pesado, rolar, levar golpe).
// Dois perfis: o Cavaleiro Antigo (grande, lento, golpes pesados e investida) e o Ladrão (rápido, esquiva rolando).
// O Ladrão é provisório: quando o colega mandar o modelo dele, é só trocar o prefab.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Vida))]
public class InimigoCavaleiro : MonoBehaviour
{
    public string nome = "Cavaleiro Antigo";
    public PerfilHumanoide perfil = PerfilHumanoide.CavaleiroAntigo;
    public float vidaBase = 1100f, escala = 1.4f;
    public float velAndar = 2.2f, velCorrer = 4.6f, alcance = 2.4f;
    public float danoLeve = 20f, danoPesado = 42f;
    public Color tinta = Color.white, corAura = new Color(1f, 0.15f, 0.1f);

    public event Action<InimigoCavaleiro> AoMorrer;
    public Vida Vida { get; private set; }
    public bool Lutando { get { return estado != Estado.Dormindo && estado != Estado.Morto; } }
    public bool Despertou { get; private set; }
    public float Escala { get { return escala; } }

    private enum Estado { Dormindo, Lutando, Atacando, Esquivando, Morto }
    private Estado estado = Estado.Dormindo;
    private CharacterController cc; private Animator anim;
    private Transform alvo; private float vy, velAnim, proximoAtaque, proximaEsquiva, proximaInvestida, ultimoHit; private int golpes;
    private Light luz; private ParticleSystem brasas;
    private bool esquivando; private float semente;
    private bool modoCena, enfurecido; private float intensidade, picoLuz = -10f;
    public bool Chegou { get; private set; }

    private void Awake()
    {
        cc = GetComponent<CharacterController>(); Vida = GetComponent<Vida>(); semente = UnityEngine.Random.value * 10f;
        anim = GetComponentInChildren<Animator>();
        transform.localScale = Vector3.one * escala;
        if (anim != null) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; if (HasParam("Grounded")) anim.SetBool("Grounded", true); }
        if (tinta != Color.white) Pintar();
        Vida.AoMorrer += Morrer;
        Vida.AoLevarDano += (d, o) => ReagirAoGolpe();
        Vida.FiltroDeDano = (d, o) => esquivando ? 0f : d;
        CriarAura();
    }

    private bool HasParam(string n) { if (anim == null) return false; foreach (AnimatorControllerParameter p in anim.parameters) if (p.name == n) return true; return false; }

    private void Pintar()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            Material[] ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null || !ms[i].HasProperty("_BaseColor")) continue;
                MaterialPropertyBlock b = new MaterialPropertyBlock(); r.GetPropertyBlock(b, i);
                b.SetColor("_BaseColor", ms[i].GetColor("_BaseColor") * tinta); r.SetPropertyBlock(b, i);
            }
        }
    }

    private void CriarAura()
    {
        GameObject g = new GameObject("Aura"); g.transform.SetParent(transform, false); g.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        luz = g.AddComponent<Light>(); luz.type = LightType.Point; luz.color = corAura; luz.range = 9f * Mathf.Max(1f, escala); luz.intensity = 0.6f; luz.shadows = LightShadows.None;
        brasas = g.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule m = brasas.main; m.playOnAwake = false; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        m.startColor = corAura; m.gravityModifier = -0.12f; m.maxParticles = 160;
        ParticleSystem.EmissionModule em = brasas.emission; em.rateOverTime = 0f;
        ParticleSystem.ShapeModule sh = brasas.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.5f * escala; sh.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.ColorOverLifetimeModule col = brasas.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        brasas.Play();
    }

    // acorda: começa a lutar e mostra a barra de vida
    public void Despertar()
    {
        if (Despertou) return;
        Despertou = true; estado = Estado.Lutando; proximoAtaque = Time.time + 1.2f; proximaInvestida = Time.time + 6f; proximaEsquiva = Time.time + 3f;
        ParticleSystem.EmissionModule em = brasas.emission; em.rateOverTime = 22f;
        BarraDeChefe b = BarraDeChefe.Criar(gameObject, Vida, nome, "Fontes/PirataOne", () => estado != Estado.Dormindo && estado != Estado.Morto);
        if (perfil == PerfilHumanoide.Ladrao) b.Cores(new Color(0.55f, 0.15f, 0.2f), new Color(0.3f, 0.06f, 0.08f));
        TremorDeCamera.Tremer(0.2f, 0.8f);
    }

    private void Update()
    {
        if (luz != null) luz.intensity = (Despertou ? 2.4f : 0.7f + 1.8f * intensidade) + (enfurecido ? 1.6f : 0f) + Mathf.Max(0f, 9f - (Time.time - picoLuz) * 12f) + Mathf.Sin(Time.time * 9f + semente) * 0.4f;
        if (estado == Estado.Morto) return;
        if (cc.isGrounded) vy = -1f; else vy += Physics.gravity.y * Time.deltaTime;
        if (estado == Estado.Dormindo || estado == Estado.Atacando || estado == Estado.Esquivando)
        {
            if (estado == Estado.Dormindo && !modoCena) { cc.Move(Vector3.up * vy * Time.deltaTime); Animar(0f); }
            return;
        }
        if (perfil == PerfilHumanoide.CavaleiroAntigo && !enfurecido && Vida.Fracao < 0.5f) { StartCoroutine(Furia()); return; }
        BuscarAlvo();
        if (alvo == null) { cc.Move(Vector3.up * vy * Time.deltaTime); Animar(0f); return; }
        Vector3 pa = alvo.position - transform.position; pa.y = 0f; float dist = pa.magnitude;
        float giro = perfil == PerfilHumanoide.Ladrao ? 320f : 150f;
        if (pa.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(pa), giro * Time.deltaTime);
        float alcEf = alcance * escala, parar = alcEf * 0.8f;
        float vel = 0f;
        if (dist > parar) vel = perfil == PerfilHumanoide.Ladrao || dist > 9f ? velCorrer : velAndar;
        cc.Move((transform.forward * vel + Vector3.up * vy) * Time.deltaTime);
        Animar(vel <= 0.01f ? 0f : (vel > velAndar + 0.1f ? 1f : 0.5f));

        if (perfil == PerfilHumanoide.Ladrao && Time.time >= proximaEsquiva && dist < alcEf * 2.6f) { StartCoroutine(Esquiva()); return; }
        if (perfil == PerfilHumanoide.CavaleiroAntigo && Time.time >= proximaInvestida && dist > 9f && dist < 30f) { StartCoroutine(Investida()); return; }
        if (Time.time >= proximoAtaque && dist <= alcEf * 1.15f)
        {
            golpes++;
            bool pesado = perfil == PerfilHumanoide.CavaleiroAntigo && golpes % 3 == 0;
            StartCoroutine(pesado ? AtaquePesado() : AtaqueLeve());
        }
    }

    // ---------------------------------------------------------------- para as cenas
    public void ModoCena() { modoCena = true; }

    // 0 a 1: a aura vai ficando mais forte antes de ele acordar
    public void Intensificar(float k)
    {
        intensidade = k;
        ParticleSystem.EmissionModule em = brasas.emission; em.rateOverTime = Mathf.Lerp(0f, 16f, k);
    }

    // anda até um ponto (cena): pára ao chegar perto ou quando acaba o tempo
    public IEnumerator IrAte(Vector3 destino, float vel, float parar, float tempoMax)
    {
        modoCena = true; Chegou = false;
        for (float t = 0f; t < tempoMax; t += Time.deltaTime)
        {
            Vector3 d = destino - transform.position; d.y = 0f;
            if (d.magnitude <= parar) break;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 540f * Time.deltaTime);
            if (cc.enabled) cc.Move((transform.forward * vel + Vector3.up * -2f) * Time.deltaTime);
            if (anim != null) anim.SetFloat("Speed", 1f);
            yield return null;
        }
        if (anim != null) anim.SetFloat("Speed", 0f);
        Chegou = true;
    }

    // aura de outra cor (ex.: negra, na cena do roubo)
    public void MudarAura(Color c)
    {
        corAura = c; if (luz != null) luz.color = c;
        ParticleSystem.MainModule m = brasas.main; m.startColor = c; brasas.Emit(40);
    }

    public void Fumaca() { ChicoteNegro.Nuvem(transform.position + Vector3.up * 0.8f * escala, new Color(0.05f, 0.02f, 0.09f, 0.7f), 34, 1.2f); }

    // some numa nuvem de fumaça
    public void Sumir()
    {
        for (int i = 0; i < 5; i++) Poeira(transform.position + new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), UnityEngine.Random.Range(0f, 1.6f), UnityEngine.Random.Range(-0.8f, 0.8f)));
        Destroy(gameObject);
    }

    // pose de desafio: ergue a arma, treme o chão e a aura explode (sem causar dano)
    public void Desafio()
    {
        if (anim != null) { anim.SetFloat("Speed", 0f); anim.SetTrigger("AttackHeavy"); }
        picoLuz = Time.time; TremorDeCamera.Tremer(0.25f, 0.9f); Poeira(transform.position);
        brasas.Emit(70);
    }

    private IEnumerator Furia()
    {
        enfurecido = true; estado = Estado.Atacando;
        Desafio();
        yield return new WaitForSeconds(1.4f);
        velAndar *= 1.25f; velCorrer *= 1.25f; danoLeve *= 1.15f; danoPesado *= 1.15f;
        ParticleSystem.EmissionModule em = brasas.emission; em.rateOverTime = 50f;
        proximoAtaque = Time.time + 0.5f;
        estado = Estado.Lutando;
    }

    private void Animar(float alvoVel)
    {
        if (anim == null) return;
        velAnim = Mathf.MoveTowards(velAnim, alvoVel, Time.deltaTime * 4f);
        anim.SetFloat("Speed", velAnim);
    }

    private void BuscarAlvo()
    {
        alvo = null; float melhor = 1e9f;
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto) continue;
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < melhor) { melhor = d; alvo = p.transform; }
        }
    }

    private IEnumerator Virar(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            if (alvo != null) { Vector3 pa = alvo.position - transform.position; pa.y = 0f; if (pa.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(pa), 360f * Time.deltaTime); }
            cc.Move(Vector3.up * -1f * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator AtaqueLeve()
    {
        estado = Estado.Atacando; Animar(0f); if (anim != null) { anim.SetFloat("Speed", 0f); velAnim = 0f; anim.SetTrigger("AttackLight"); }
        float k = perfil == PerfilHumanoide.Ladrao ? 0.75f : 1f;
        yield return Virar(0.34f * k);
        Golpe(danoLeve, alcance * escala * 1.3f, 110f);
        yield return new WaitForSeconds(0.5f * k);
        proximoAtaque = Time.time + (perfil == PerfilHumanoide.Ladrao ? 0.7f : 1.3f);
        estado = Estado.Lutando;
    }

    private IEnumerator AtaquePesado()
    {
        estado = Estado.Atacando; if (anim != null) { anim.SetFloat("Speed", 0f); velAnim = 0f; anim.SetTrigger("AttackHeavy"); }
        yield return Virar(0.7f);
        Golpe(danoPesado, alcance * escala * 1.6f, 150f);
        TremorDeCamera.Tremer(0.22f, 0.5f); Poeira(transform.position + transform.forward * alcance * escala);
        yield return new WaitForSeconds(0.75f);
        proximoAtaque = Time.time + 1.6f;
        estado = Estado.Lutando;
    }

    private IEnumerator Investida()
    {
        estado = Estado.Atacando; proximaInvestida = Time.time + 9f;
        if (anim != null) anim.SetFloat("Speed", 1f);
        Poeira(transform.position);
        for (float t = 0f; t < 1.3f; t += Time.deltaTime)
        {
            BuscarAlvo(); if (alvo == null) break;
            Vector3 pa = alvo.position - transform.position; pa.y = 0f;
            if (pa.magnitude <= alcance * escala * 1.1f) break;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(pa), 200f * Time.deltaTime);
            cc.Move((transform.forward * 10.5f + Vector3.up * -1f) * Time.deltaTime);
            if (Time.frameCount % 6 == 0) TremorDeCamera.Tremer(0.05f, 0.2f);
            yield return null;
        }
        estado = Estado.Lutando;
        yield return AtaquePesado();
    }

    private IEnumerator Esquiva()
    {
        estado = Estado.Esquivando; esquivando = true; proximaEsquiva = Time.time + UnityEngine.Random.Range(3.2f, 5f);
        if (anim != null) { anim.SetFloat("Speed", 0f); anim.SetTrigger("Roll"); }
        Vector3 lado = Vector3.Cross(Vector3.up, transform.forward) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        Vector3 dir = (lado + (alvo != null ? -transform.forward * 0.6f : Vector3.zero)).normalized;
        for (float t = 0f; t < 0.55f; t += Time.deltaTime) { cc.Move((dir * 8f + Vector3.up * -1f) * Time.deltaTime); yield return null; }
        esquivando = false;
        estado = Estado.Lutando; proximoAtaque = Time.time + 0.15f;    // revida logo depois de rolar
    }

    private void Golpe(float dano, float alc, float angulo)
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto) continue;
            Vector3 d = p.transform.position - transform.position; d.y = 0f;
            if (d.magnitude <= alc && Vector3.Angle(transform.forward, d) <= angulo * 0.5f) v.ReceberDano(dano, transform.position);
        }
    }

    private void ReagirAoGolpe()
    {
        if (estado == Estado.Morto || estado == Estado.Atacando || anim == null || Time.time - ultimoHit < 1.4f) return;
        ultimoHit = Time.time;
        if (anim.HasState(0, Animator.StringToHash("hit"))) anim.CrossFadeInFixedTime("hit", 0.08f, 0, 0f);
    }

    private void Morrer()
    {
        if (estado == Estado.Morto) return;
        estado = Estado.Morto; StopAllCoroutines(); esquivando = false;
        if (AoMorrer != null) AoMorrer(this);
        StartCoroutine(Cair());
    }

    // ---------------------------------------------------------------- derrota (o Cavaleiro Antigo vira cinzas; a espada fica cravada)
    private class Estilhaco { public Transform t; public Vector3 v, w; public float tam; public float pousou = -1f; }
    private readonly System.Collections.Generic.List<Estilhaco> estilhacos = new System.Collections.Generic.List<Estilhaco>();
    private static Material matMetal;

    private void Lasca(Vector3 pos, Vector3 vel, float tam)
    {
        if (matMetal == null)
        {
            matMetal = new Material(Shader.Find("Universal Render Pipeline/Lit")); matMetal.SetColor("_BaseColor", new Color(0.13f, 0.12f, 0.14f)); matMetal.SetFloat("_Metallic", 0.7f); matMetal.SetFloat("_Smoothness", 0.45f);
        }
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = matMetal; g.transform.position = pos; g.transform.rotation = UnityEngine.Random.rotation;
        g.transform.localScale = new Vector3(tam, tam * UnityEngine.Random.Range(0.3f, 0.9f), tam * UnityEngine.Random.Range(0.5f, 1.2f));
        estilhacos.Add(new Estilhaco { t = g.transform, v = vel, w = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 8f), tam = tam });
    }

    private void SimularEstilhacos(float chaoPadrao)
    {
        float dt = Time.deltaTime;
        for (int i = estilhacos.Count - 1; i >= 0; i--)
        {
            Estilhaco e = estilhacos[i];
            if (e.t == null) { estilhacos.RemoveAt(i); continue; }
            if (e.pousou >= 0f)
            {
                float resta = 4.5f - (Time.time - e.pousou);
                if (resta < 1f) e.t.localScale *= Mathf.Clamp01(1f - dt * 3f);
                if (resta <= 0f) { Destroy(e.t.gameObject); estilhacos.RemoveAt(i); }
                continue;
            }
            e.v += Vector3.down * 18f * dt;
            Vector3 p = e.t.position + e.v * dt; float chao = chaoPadrao; RaycastHit h;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out h, 40f, ~0, QueryTriggerInteraction.Ignore)) chao = h.point.y;
            if (p.y <= chao + e.tam * 0.3f && e.v.y < 0f) { p.y = chao + e.tam * 0.3f; e.pousou = Time.time; e.v = Vector3.zero; }
            e.t.position = p;
            if (e.pousou < 0f) e.t.rotation = Quaternion.AngleAxis(e.w.magnitude * Mathf.Rad2Deg * dt, e.w.normalized) * e.t.rotation;
        }
    }

    private Vector3 PontoNoCorpo(float alto) { Vector2 c = UnityEngine.Random.insideUnitCircle * 0.7f * escala; return transform.position + new Vector3(c.x, alto * escala * UnityEngine.Random.value, c.y); }

    private IEnumerator Cair()
    {
        cc.enabled = false;
        float chao = transform.position.y;
        Vector3 pos0 = transform.position; Quaternion rot0 = transform.rotation; Vector3 eixo = transform.right;
        ParticleSystem.EmissionModule em = brasas.emission; em.rateOverTime = 35f;
        if (anim != null && anim.HasState(0, Animator.StringToHash("hit"))) anim.CrossFadeInFixedTime("hit", 0.05f, 0, 0f);
        TremorDeCamera.Tremer(0.25f, 1.5f);
        // 1) cambaleia: treme, a aura acende e caem pedaços de armadura
        float prox = 0f;
        for (float t = 0f; t < 1.8f; t += Time.deltaTime)
        {
            float k = t / 1.8f;
            transform.position = pos0 + UnityEngine.Random.insideUnitSphere * 0.04f * k;
            if (luz != null) luz.intensity = 3f + 5f * k;
            if (Time.time >= prox) { prox = Time.time + 0.12f; Lasca(PontoNoCorpo(2.2f), UnityEngine.Random.insideUnitSphere * 1.5f, UnityEngine.Random.Range(0.06f, 0.16f)); }
            SimularEstilhacos(chao); yield return null;
        }
        if (anim != null) anim.speed = 0f;
        // 2) cai de joelhos, inclinado para a frente
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.6f);
            transform.rotation = Quaternion.AngleAxis(30f * k, eixo) * rot0;
            transform.position = pos0 + Vector3.down * (0.45f * k) + UnityEngine.Random.insideUnitSphere * 0.03f;
            if (luz != null) luz.intensity = 8f + 6f * k;
            if (Time.time >= prox) { prox = Time.time + 0.07f; Lasca(PontoNoCorpo(2.2f), UnityEngine.Random.insideUnitSphere * 2f, UnityEngine.Random.Range(0.06f, 0.2f)); }
            if (Time.frameCount % 8 == 0) Poeira(transform.position + Vector3.up * 0.2f);
            SimularEstilhacos(chao); yield return null;
        }
        // 3) último brilho
        for (float t = 0f; t < 1.0f; t += Time.deltaTime)
        {
            transform.position = pos0 + Vector3.down * 0.45f + UnityEngine.Random.insideUnitSphere * (0.04f + 0.12f * t);
            if (luz != null) luz.intensity = 14f + 26f * t * t;
            TremorDeCamera.Tremer(0.1f + 0.25f * t, 0.25f);
            if (Time.frameCount % 2 == 0) brasas.Emit(4);
            SimularEstilhacos(chao); yield return null;
        }
        // 4) vira cinzas: corpo some, chuva de metal, onda vermelha e a espada fica cravada no chão
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r is SkinnedMeshRenderer || r is MeshRenderer) r.enabled = false;
        Vector3 centro = pos0 + Vector3.up * 1.2f * escala;
        TremorDeCamera.Tremer(0.55f, 2f); picoLuz = Time.time; if (luz != null) luz.intensity = 40f;
        for (int i = 0; i < 38; i++)
        {
            Vector3 p = PontoNoCorpo(2.3f); Vector3 fora = p - centro; fora.y = 0f; fora = fora.sqrMagnitude > 0.01f ? fora.normalized : UnityEngine.Random.insideUnitSphere;
            Lasca(p, fora * UnityEngine.Random.Range(2f, 7f) + Vector3.up * UnityEngine.Random.Range(3f, 9f), UnityEngine.Random.Range(0.1f, 0.4f));
        }
        for (int i = 0; i < 6; i++) Poeira(pos0 + Vector3.up * UnityEngine.Random.Range(0.2f, 2.5f));
        brasas.Emit(150);
        StartCoroutine(OndaVermelha(pos0 + Vector3.up * 0.15f));
        CravarEspada(pos0 + transform.forward * 0.4f * escala, chao);
        float ini = Time.time;
        while (Time.time - ini < 5f) { if (luz != null) luz.intensity = Mathf.Lerp(luz.intensity, 0f, Time.deltaTime * 2f); SimularEstilhacos(chao); yield return null; }
        foreach (Estilhaco e in estilhacos) if (e.t != null) Destroy(e.t.gameObject);
        estilhacos.Clear();
        Destroy(gameObject, 0.1f);
    }

    private IEnumerator OndaVermelha(Vector3 c)
    {
        GameObject anel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(anel.GetComponent<Collider>());
        Renderer rd = anel.GetComponent<Renderer>(); rd.sharedMaterial = new Material(Shader.Find("Sprites/Default")); rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (float r = 0.5f; r < 9f; r += 16f * Time.deltaTime)
        {
            anel.transform.position = c; anel.transform.localScale = new Vector3(r * 2f, 0.05f, r * 2f);
            rd.sharedMaterial.color = new Color(corAura.r, corAura.g * 0.6f, corAura.b * 0.6f, 0.55f * (1f - r / 9f));
            yield return null;
        }
        Destroy(anel);
    }

    // a espada do cavaleiro fica cravada no chão, com um brilho vermelho (some quando o castelo reinicia)
    private void CravarEspada(Vector3 pos, float chao)
    {
        RaycastHit h; if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out h, 20f, ~0, QueryTriggerInteraction.Ignore)) pos.y = h.point.y; else pos.y = chao;
        GameObject raiz = new GameObject("Espada cravada"); raiz.transform.position = pos; raiz.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-4f, 4f));
        raiz.transform.localScale = Vector3.one * escala;
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.26f)); m.SetFloat("_Metallic", 0.9f); m.SetFloat("_Smoothness", 0.7f);
        Material ouro = new Material(Shader.Find("Universal Render Pipeline/Lit")); ouro.SetColor("_BaseColor", new Color(0.45f, 0.33f, 0.15f)); ouro.SetFloat("_Metallic", 0.9f); ouro.SetFloat("_Smoothness", 0.5f);
        System.Action<Vector3, Vector3, Material> peca = (lp, esc, mat) =>
        {
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(raiz.transform, false); c.transform.localPosition = lp; c.transform.localScale = esc; c.GetComponent<Renderer>().sharedMaterial = mat;
        };
        peca(new Vector3(0f, 0.85f, 0f), new Vector3(0.13f, 1.7f, 0.035f), m);        // lâmina
        peca(new Vector3(0f, 1.72f, 0f), new Vector3(0.62f, 0.09f, 0.07f), ouro);      // guarda
        peca(new Vector3(0f, 1.98f, 0f), new Vector3(0.06f, 0.45f, 0.06f), ouro);      // cabo
        peca(new Vector3(0f, 2.24f, 0f), new Vector3(0.13f, 0.13f, 0.13f), ouro);      // pomo
        GameObject lg = new GameObject("Luz"); lg.transform.SetParent(raiz.transform, false); lg.transform.localPosition = new Vector3(0f, 1.5f, 0.2f);
        Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = corAura; l.range = 7f; l.intensity = 1.6f; l.shadows = LightShadows.None;
    }

    private void Poeira(Vector3 pos)
    {
        GameObject g = new GameObject("Poeira"); g.transform.position = pos + Vector3.up * 0.2f;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main; main.duration = 0.5f; main.loop = false; main.startLifetime = 1.4f; main.startSpeed = 3f; main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startColor = new Color(0.5f, 0.47f, 0.42f, 0.5f); main.gravityModifier = -0.02f; main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
        ParticleSystem.ShapeModule sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.4f;
        g.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        ps.Play(); Destroy(g, 2.5f);
    }
}
