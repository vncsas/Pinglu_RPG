using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Monstro do Pântano (modelo com esqueleto do colega). Jeito próprio de lutar:
//  - fica ENTERRADO na lama e sai de lá, de repente, quando alguém chega perto (emboscada);
//  - persegue o jogador andando e, quando está a uns 4-7 m, se AGACHA e dá um BOTE: um salto para cima do alvo;
//  - depois do bote fica um instante exposto, tomando mais dano;
//  - quando morre, desaba e afunda na lama.
// Usa as animações Idle, Walk, Run, Attack e Death do FBX.
[RequireComponent(typeof(Vida))]
public class MonstroDoPantano : MonoBehaviour, IAfetavelPorHabilidade
{
    public AnimationClip parado, andar, correr, atacar, morrer;
    public GameObject efeitoLama;                       // poeira/lama ao sair, ao pousar e ao morrer (opcional)

    [Header("Números")]
    public float vidaMaxima = 150f;
    public float velAndar = 1.7f, velCorrer = 4.4f;
    public float danoGarra = 12f, danoBote = 20f;
    public float alcance = 2.1f;
    public float distanciaDeEmboscada = 18f;
    public bool comecaEnterrado = true;

    enum Est { Enterrado, Surgindo, Perseguindo, Agachando, Bote, Atacando, Exposto, Morto }
    Est est = Est.Enterrado;

    Vida vida; MisturadorDeClipes mis;
    Transform alvo; Vida vidaAlvo;
    float proxOnda, proximaBusca, proximoBote, fimDoEstado, inicioEstado, lentoAte, fator = 1f;
    Transform provocador; float provocadoAte;
    Vector3 empurrao, saltoDe, saltoPara; bool golpeDado;
    float chaoY;
    public event System.Action<MonstroDoPantano> AoMorrer;
    public Vida Vida { get { return vida; } }
    public bool Vivo { get { return est != Est.Morto; } }

    // ---- IAfetavelPorHabilidade
    public void Provocar(Transform quem, float s) { provocador = quem; provocadoAte = Time.time + s; proximaBusca = 0f; }
    public void Lentidao(float f, float s) { fator = Mathf.Clamp(f, 0.2f, 1f); lentoAte = Time.time + s; }
    public void Empurrar(Vector3 v) { if (est != Est.Bote && est != Est.Surgindo) empurrao += v; }

    void Awake()
    {
        vida = GetComponent<Vida>(); vida.DefinirMaxima(vidaMaxima);
        vida.AoMorrer += Morrer;
        vida.FiltroDeDano = (d, o) => est == Est.Exposto ? d * 1.5f : d;     // depois do bote fica exposto: leva 50% a mais
        mis = GetComponent<MisturadorDeClipes>(); if (mis == null) mis = gameObject.AddComponent<MisturadorDeClipes>();
        mis.Montar(new Dictionary<string, AnimationClip> { { "Idle", parado }, { "Walk", andar }, { "Run", correr }, { "Attack", atacar }, { "Death", morrer } });
    }

    void Start()
    {
        chaoY = AlturaDoChao(transform.position);
        if (comecaEnterrado)
        {
            est = Est.Enterrado;
            transform.position = new Vector3(transform.position.x, chaoY - 2.4f, transform.position.z);
            mis.Tocar("Idle", 0.01f);
            SetVisivel(false);
        }
        else { est = Est.Perseguindo; mis.Tocar("Idle", 0.01f); }
    }

    void SetVisivel(bool v) { foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = v; }

    // ---------------------------------------------------------------- alvo e chão
    void AcharAlvo()
    {
        float melhor = float.MaxValue; Transform t = null; Vida vv = null;
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            Vida v = p.GetComponent<Vida>(); if (v != null && v.Morto) continue;
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < melhor) { melhor = d; t = p.transform; vv = v; }
        }
        if (provocador != null && Time.time < provocadoAte) { Vida pv = provocador.GetComponent<Vida>(); if (pv == null || !pv.Morto) { t = provocador; vv = pv; } }
        alvo = t; vidaAlvo = vv;
    }

    float AlturaDoChao(Vector3 p)
    {
        float melhor = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.GetComponentInParent<Vida>() != null) continue;
            if (h.point.y > melhor) melhor = h.point.y;
        }
        if (melhor > float.NegativeInfinity) return melhor;
        Terrain t = Terrain.activeTerrain; return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y;
    }

    Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

    void Virar(Vector3 dir, float velocidade)
    {
        dir = Plano(dir); if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), velocidade * Time.deltaTime);
    }

    void Andar(Vector3 dir, float vel)
    {
        dir = Plano(dir); if (dir.sqrMagnitude < 0.001f) return;
        transform.position += dir.normalized * vel * (Time.time < lentoAte ? fator : 1f) * Time.deltaTime;
    }

    void Grudar()
    {
        chaoY = Mathf.Lerp(chaoY, AlturaDoChao(transform.position), Time.deltaTime * 10f);
        Vector3 p = transform.position; p.y = chaoY; transform.position = p;
    }

    // ---------------------------------------------------------------- comportamento
    void Update()
    {
        if (est == Est.Morto) return;
        if (SessaoDeJogo.MenuAberto || CameraDeCena.Ativa) return;       // cena de câmera: fica parado
        if (Time.time >= proximaBusca) { proximaBusca = Time.time + 0.5f; if (est != Est.Bote) AcharAlvo(); }
        float dist = alvo != null ? Plano(alvo.position - transform.position).magnitude : 999f;
        Vector3 para = alvo != null ? alvo.position - transform.position : transform.forward;

        if (empurrao.sqrMagnitude > 0.01f) { transform.position += empurrao * Time.deltaTime; empurrao = Vector3.Lerp(empurrao, Vector3.zero, Time.deltaTime * 6f); }

        switch (est)
        {
            case Est.Enterrado:
                if (alvo != null && dist < distanciaDeEmboscada) StartCoroutine(Surgir());
                break;

            case Est.Perseguindo:
                Grudar();
                if (alvo == null) { mis.Tocar("Idle"); break; }
                Virar(para, 7f);
                if (dist <= alcance) { Iniciar(Est.Atacando); mis.Tocar("Attack", 0.1f, 1f, true); golpeDado = false; }
                else if (dist > 4f && dist < 7.5f && Time.time >= proximoBote) { Iniciar(Est.Agachando); mis.Tocar("Idle", 0.1f); }
                else
                {
                    bool corre = dist > 9f || Time.time - inicioEstado > 0f && dist > 3f;
                    mis.Tocar(corre ? "Run" : "Walk", 0.2f, corre ? 1f : 1f);
                    Andar(transform.forward, corre ? velCorrer : velAndar);
                    if (AmbienteDaAgua.Existe && Time.time >= proxOnda && AmbienteDaAgua.Profundidade(transform.position) > 0.1f) { proxOnda = Time.time + 0.4f; AmbienteDaAgua.Ondular(transform.position, 0.8f); }
                }
                break;

            case Est.Agachando:     // se encolhe, tremendo, antes do bote
                Grudar();
                Virar(para, 10f);
                transform.position += Vector3.down * 0f;
                if (Time.time - inicioEstado > 0.6f)
                {
                    saltoDe = transform.position;
                    Vector3 d = Plano(alvo != null ? alvo.position - transform.position : transform.forward);
                    saltoPara = transform.position + d.normalized * Mathf.Min(d.magnitude, 7.5f);
                    Iniciar(Est.Bote); golpeDado = false; mis.Tocar("Attack", 0.08f, 1.3f, true);
                }
                else transform.localScale = Vector3.one * (1f - 0.08f * Mathf.Sin((Time.time - inicioEstado) / 0.6f * Mathf.PI * 0.5f));
                break;

            case Est.Bote:
            {
                transform.localScale = Vector3.one;
                float k = Mathf.Clamp01((Time.time - inicioEstado) / 0.5f);
                Vector3 p = Vector3.Lerp(saltoDe, saltoPara, k);
                p.y = Mathf.Lerp(saltoDe.y, AlturaDoChao(saltoPara), k) + Mathf.Sin(k * Mathf.PI) * 2.2f;
                transform.position = p;
                Virar(saltoPara - saltoDe, 20f);
                if (k >= 1f)
                {
                    chaoY = AlturaDoChao(transform.position);
                    Acertar(transform.position, 2.2f, danoBote);
                    if (efeitoLama != null) Destroy(Instantiate(efeitoLama, transform.position, Quaternion.identity), 3f);
                    ThirdPersonCameraFollow.Tremer(0.12f, 0.2f);
                    proximoBote = Time.time + Random.Range(5f, 8f);
                    Iniciar(Est.Exposto); mis.Tocar("Idle", 0.15f);
                }
                break;
            }

            case Est.Atacando:
                Grudar();
                Virar(para, 4f);
                float prog = mis.Progresso;
                if (!golpeDado && prog > 0.45f) { golpeDado = true; Acertar(transform.position + transform.forward * 1.4f, 1.6f, danoGarra); }
                if (prog >= 0.97f || Time.time - inicioEstado > mis.Duracao("Attack") + 0.3f) { Iniciar(Est.Perseguindo); proximoBote = Mathf.Max(proximoBote, Time.time + 1.5f); }
                break;

            case Est.Exposto:       // depois do bote: ofegando, tomando mais dano
                Grudar();
                if (Time.time - inicioEstado > 1.1f) Iniciar(Est.Perseguindo);
                break;
        }
    }

    void Iniciar(Est e) { est = e; inicioEstado = Time.time; if (e != Est.Agachando) transform.localScale = Vector3.one; }

    void Acertar(Vector3 centro, float raio, float dano)
    {
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto) continue;
            Vector3 d = p.transform.position + Vector3.up - centro;
            if (d.magnitude <= raio + 0.5f) v.ReceberDano(dano, transform.position);
        }
    }

    // ---------------------------------------------------------------- sair da lama / morrer
    IEnumerator Surgir()
    {
        est = Est.Surgindo;
        SetVisivel(true);
        Vector3 de = transform.position, ate = new Vector3(de.x, AlturaDoChao(de + Vector3.up * 3f), de.z);
        if (alvo != null) { Vector3 d = Plano(alvo.position - transform.position); if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized); }
        if (efeitoLama != null) Destroy(Instantiate(efeitoLama, ate, Quaternion.identity), 3f);
        mis.Tocar("Idle", 0.05f);
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.5f);
            transform.position = Vector3.Lerp(de, ate, k) + new Vector3(Mathf.Sin(t * 50f), 0f, Mathf.Cos(t * 43f)) * 0.04f * (1f - k);   // treme ao sair
            if (t > 0.6f) mis.Tocar("Walk", 0.3f);
            yield return null;
        }
        transform.position = ate; chaoY = ate.y;
        mis.Tocar("Idle", 0.1f);
        yield return new WaitForSeconds(0.25f);
        Iniciar(Est.Perseguindo);
    }

    void Morrer()
    {
        StopAllCoroutines();
        est = Est.Morto; transform.localScale = Vector3.one;
        mis.Tocar("Death", 0.1f);
        if (AoMorrer != null) AoMorrer(this);
        StartCoroutine(Afundar());
    }

    IEnumerator Afundar()
    {
        yield return new WaitForSeconds(Mathf.Max(1f, mis.Duracao("Death")) + 1.5f);
        if (efeitoLama != null) Destroy(Instantiate(efeitoLama, transform.position, Quaternion.identity), 3f);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Vector3 de = transform.position;
        for (float t = 0f; t < 2.2f; t += Time.deltaTime) { transform.position = de + Vector3.down * 2.2f * (t / 2.2f); yield return null; }
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- barra de vida (só depois de levar dano)
    void OnGUI()
    {
        if (est == Est.Morto || est == Est.Enterrado || vida == null || vida.Fracao >= 0.999f || Event.current.type != EventType.Repaint) return;
        Camera cam = Camera.main; if (cam == null) return;
        Vector3 alto = transform.position + Vector3.up * 2.6f;
        Vector3 sp = cam.WorldToScreenPoint(alto);
        if (sp.z < 0.5f || sp.z > 40f) return;
        float e = Screen.height / 1080f, w = 90f * e, h = 9f * e;
        Rect r = new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h * 0.5f, w, h);
        Color antes = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f); GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Texture2D.whiteTexture);
        GUI.color = new Color(0.45f, 0.1f, 0.08f); GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.5f, 0.75f, 0.2f); GUI.DrawTexture(new Rect(r.x, r.y, r.width * vida.Fracao, r.height), Texture2D.whiteTexture);
        GUI.color = antes;
    }
}
