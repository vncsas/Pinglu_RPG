using System.Collections;
using UnityEngine;

// Esqueleto: sai do chão, persegue o jogador, ataca de perto, cambaleia ao
// levar golpe e desmorona quando morre. Mostra uma barra de vida em cima.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Vida))]
public class InimigoEsqueleto : MonoBehaviour
{
    [Header("Animações (nomes dos states no Animator)")]
    [SerializeField] private string animParado = "idle";
    [SerializeField] private string animAndar = "walk";
    [SerializeField] private string animAtaque = "attack";
    [SerializeField] private string animDano = "hit";
    [SerializeField] private string animMorte = "death";

    [Header("Movimento")]
    [SerializeField] private float velocidade = 1.8f;
    [SerializeField] private float velocidadeGiro = 6f;
    [SerializeField] private float distanciaParaAtacar = 1.9f;

    [Header("Ataque")]
    [SerializeField] private float dano = 12f;
    [SerializeField] private float alcanceDoGolpe = 2.4f;
    [Tooltip("Momento da animação (0 a 1) em que a espada acerta")]
    [SerializeField, Range(0f, 1f)] private float momentoDoAcerto = 0.5f;
    [SerializeField] private float intervaloEntreAtaques = 1.4f;

    [Header("Vida")]
    [SerializeField] private float vidaMaxima = 60f;
    [SerializeField] private float tempoAtordoado = 0.45f;

    [Header("Aparência")]
    [Tooltip("Cor que tinge o modelo (branco = sem mudança). Usada nas variações de monstro")]
    [SerializeField] private Color tom = Color.white;

    [Header("Barra de vida")]
    [SerializeField] private Material materialFundo;
    [SerializeField] private Material materialFrente;
    [SerializeField] private float alturaDaBarra = 2.35f;

    [Header("Ao surgir")]
    [SerializeField] private float tempoParaSurgir = 1.4f;
    [SerializeField] private float profundidadeInicial = 2.2f;

    private enum Estado { Surgindo, Perseguindo, Atacando, Atordoado, Morto }
    private Estado estado = Estado.Perseguindo;

    private CharacterController cc;
    private Animator anim;
    private Vida vida;
    private Transform alvo;
    private Vida vidaAlvo;
    private string animAtual;
    private float velocidadeVertical;
    private Vector3 empurrao;
    private float proximoAtaque;
    private float fimAtordoado;
    private float inicioAtaque;
    private bool golpeAplicado;
    private Transform barra, frente;

    public event System.Action<InimigoEsqueleto> AoMorrer;
    public Vida Vida => vida;

    // multiplayer: nos PCs que não são o host, o esqueleto só mostra o que o host manda
    [System.NonSerialized] public bool SoVisual;
    public event System.Action<string> AoTocar;   // o host avisa as animações para os outros PCs
    private float proximaBusca;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();
        vida = GetComponent<Vida>();
        vida.DefinirMaxima(vidaMaxima);
        vida.AoLevarDano += AoLevarDano;
        vida.AoMorrer += Morrer;
        AplicarTom();
        CriarBarra();
    }

    private void Start() { AcharAlvo(); }

    private void AplicarTom()
    {
        if (tom == Color.white) return;
        MaterialPropertyBlock bloco = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(bloco);
            bloco.SetColor("_BaseColor", tom);
            r.SetPropertyBlock(bloco);
        }
    }

    // o jogador vivo mais perto (no multiplayer, qualquer um dos jogadores)
    private void AcharAlvo()
    {
        float melhor = float.MaxValue;
        Transform achado = null; Vida vAchada = null;
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            Vida v = p.GetComponent<Vida>();
            if (v != null && v.Morto) continue;
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < melhor) { melhor = d; achado = p.transform; vAchada = v; }
        }
        alvo = achado; vidaAlvo = vAchada;
    }

    // área onde este monstro pode agir: ele só persegue quem está dentro dela (ex.: a floresta)
    private bool temLimite;
    private Vector3 limiteCentro;
    private float limiteRaio;
    private System.Func<Vector3, bool> areaDentro;
    public void DefinirLimite(Vector3 centro, float raio) { temLimite = true; limiteCentro = centro; limiteRaio = raio; areaDentro = null; }
    // área de qualquer formato; "refugio" é para onde o monstro volta se ficar fora dela
    public void DefinirArea(System.Func<Vector3, bool> dentro, Vector3 refugio) { temLimite = true; areaDentro = dentro; limiteCentro = refugio; limiteRaio = 0f; }
    private bool EstaDentro(Vector3 p) { return areaDentro != null ? areaDentro(p) : PlanoDist(p, limiteCentro) <= limiteRaio; }
    private static float PlanoDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    public void MultiplicarVida(float fator) { vida.DefinirMaxima(vidaMaxima * fator); }

    // ------------------------------------------------------------------
    //  Sair do chão (usado pelo gerenciador de ondas)
    // ------------------------------------------------------------------
    public void Surgir() { StartCoroutine(RotinaSurgir()); }

    private IEnumerator RotinaSurgir()
    {
        estado = Estado.Surgindo;
        cc.enabled = false;
        Vector3 fim = transform.position;
        Vector3 inicio = fim - Vector3.up * profundidadeInicial;
        transform.position = inicio;
        Tocar(animAndar);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / tempoParaSurgir;
            transform.position = Vector3.Lerp(inicio, fim, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        transform.position = fim;
        cc.enabled = true;
        estado = Estado.Perseguindo;
        proximoAtaque = Time.time + 0.5f;
    }

    // ------------------------------------------------------------------
    private void Update()
    {
        if (SoVisual || estado == Estado.Morto || estado == Estado.Surgindo) return;
        if (Time.time >= proximaBusca && estado != Estado.Atacando) { proximaBusca = Time.time + 0.5f; AcharAlvo(); }

        Vector3 mover = Vector3.zero;
        bool alvoVivo = alvo != null && (vidaAlvo == null || !vidaAlvo.Morto);
        if (alvoVivo && temLimite && !EstaDentro(alvo.position)) alvoVivo = false; // o jogador saiu da área deste monstro
        Vector3 paraAlvo = alvoVivo ? alvo.position - transform.position : Vector3.zero;
        paraAlvo.y = 0f;
        float dist = paraAlvo.magnitude;

        switch (estado)
        {
            case Estado.Perseguindo:
                if (!alvoVivo)
                {
                    // sem alvo: se estiver perto da borda da área, volta para dentro; senão fica parado
                    Vector3 paraCentro = limiteCentro - transform.position; paraCentro.y = 0f;
                    if (temLimite && (areaDentro != null ? !EstaDentro(transform.position) : paraCentro.magnitude > limiteRaio - 8f))
                    {
                        Girar(paraCentro);
                        mover = transform.forward * velocidade;
                        Tocar(animAndar);
                    }
                    else Tocar(animParado);
                    break;
                }
                Girar(paraAlvo);
                if (dist > distanciaParaAtacar)
                {
                    mover = transform.forward * velocidade + Separacao();
                    Tocar(animAndar);
                }
                else if (Time.time >= proximoAtaque)
                {
                    estado = Estado.Atacando;
                    golpeAplicado = false;
                    inicioAtaque = Time.time;
                    Tocar(animAtaque, true);
                }
                else Tocar(animParado);
                break;

            case Estado.Atacando:
                float n = TempoDoEstado(animAtaque);
                if (n < momentoDoAcerto * 0.6f && alvoVivo) Girar(paraAlvo);
                if (!golpeAplicado && n >= momentoDoAcerto)
                {
                    golpeAplicado = true;
                    if (alvoVivo && dist <= alcanceDoGolpe && Vector3.Angle(transform.forward, paraAlvo) < 75f)
                        vidaAlvo?.ReceberDano(dano, transform.position);
                }
                if (n >= 0.95f || Time.time - inicioAtaque > 4f)
                {
                    estado = Estado.Perseguindo;
                    proximoAtaque = Time.time + intervaloEntreAtaques;
                }
                break;

            case Estado.Atordoado:
                if (Time.time >= fimAtordoado) estado = Estado.Perseguindo;
                break;
        }

        // gravidade e empurrão
        if (cc.isGrounded && velocidadeVertical < 0f) velocidadeVertical = -1f;
        else velocidadeVertical += -9.81f * Time.deltaTime;
        empurrao = Vector3.Lerp(empurrao, Vector3.zero, Time.deltaTime * 6f);
        if (cc.enabled) cc.Move((mover + empurrao + Vector3.up * velocidadeVertical) * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (barra == null) return;
        Camera cam = Camera.main;
        if (cam != null) barra.rotation = cam.transform.rotation;
        float f = Mathf.Clamp01(vida.Fracao);
        frente.localScale = new Vector3(0.96f * f, 0.07f, 1f);
        frente.localPosition = new Vector3(-0.48f * (1f - f), 0f, -0.01f);
    }

    private void Girar(Vector3 direcao)
    {
        if (direcao.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direcao), velocidadeGiro * Time.deltaTime);
    }

    // afasta um pouco dos outros esqueletos para não ficarem um dentro do outro
    private Vector3 Separacao()
    {
        Vector3 soma = Vector3.zero;
        foreach (Collider c in Physics.OverlapSphere(transform.position + Vector3.up, 1.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.transform == transform) continue;
            InimigoEsqueleto outro = c.GetComponentInParent<InimigoEsqueleto>();
            if (outro == null || outro == this) continue;
            Vector3 d = transform.position - outro.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) soma += d.normalized * (1.2f - Mathf.Min(1.2f, d.magnitude));
        }
        return soma * 2f;
    }

    private float TempoDoEstado(string nome)
    {
        if (anim == null) return Mathf.Clamp01((Time.time - inicioAtaque) / 1.4f);
        AnimatorStateInfo atual = anim.GetCurrentAnimatorStateInfo(0);
        if (anim.IsInTransition(0))
        {
            AnimatorStateInfo prox = anim.GetNextAnimatorStateInfo(0);
            if (prox.IsName(nome)) return prox.normalizedTime;
        }
        return atual.IsName(nome) ? atual.normalizedTime : 0f;
    }

    private void Tocar(string nome, bool reiniciar = false)
    {
        if (anim == null || string.IsNullOrEmpty(nome)) return;
        if (animAtual == nome && !reiniciar) return;
        animAtual = nome;
        anim.CrossFadeInFixedTime(nome, 0.15f, 0, 0f);
        AoTocar?.Invoke(nome);
    }

    // multiplayer: animação mandada pelo host
    public void TocarRemoto(string nome)
    {
        if (anim == null || string.IsNullOrEmpty(nome)) return;
        animAtual = nome;
        anim.CrossFadeInFixedTime(nome, 0.15f, 0, 0f);
    }

    // multiplayer: mostra a barra de vida quando a vida (vinda do host) baixa
    public void AtualizarVisualDaVida()
    {
        if (barra != null && vida.Fracao < 0.999f) barra.gameObject.SetActive(true);
    }

    // ------------------------------------------------------------------
    private void AoLevarDano(float valor, Vector3 origem)
    {
        if (barra != null) barra.gameObject.SetActive(true);
        if (estado == Estado.Morto || estado == Estado.Surgindo) return;
        Vector3 d = transform.position - origem; d.y = 0f;
        if (d.sqrMagnitude > 0.001f) empurrao = d.normalized * 3.5f;
        estado = Estado.Atordoado;
        fimAtordoado = Time.time + tempoAtordoado;
        proximoAtaque = Mathf.Max(proximoAtaque, Time.time + tempoAtordoado + 0.4f);
        Tocar(animDano, true);
    }

    private void Morrer()
    {
        estado = Estado.Morto;
        cc.enabled = false;
        if (barra != null) Destroy(barra.gameObject);
        AoMorrer?.Invoke(this);
        if (SoVisual) TocarRemoto(animMorte); else Tocar(animMorte, true);
        StartCoroutine(Afundar());
    }

    private IEnumerator Afundar()
    {
        yield return new WaitForSeconds(4f);
        Vector3 inicio = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 2.5f;
            transform.position = inicio - Vector3.up * (1.5f * t);
            yield return null;
        }
        if (!SoVisual) Destroy(gameObject); // no multiplayer, só o host apaga (e some para todos)
    }

    // ------------------------------------------------------------------
    private void CriarBarra()
    {
        if (materialFundo == null || materialFrente == null) return;
        barra = new GameObject("Barra de Vida").transform;
        barra.SetParent(transform, false);
        barra.localPosition = Vector3.up * alturaDaBarra;
        Transform fundo = Quad("Fundo", materialFundo);
        fundo.localScale = new Vector3(1f, 0.11f, 1f);
        frente = Quad("Vida", materialFrente);
        frente.localScale = new Vector3(0.96f, 0.07f, 1f);
        frente.localPosition = new Vector3(0f, 0f, -0.01f);
        barra.gameObject.SetActive(false); // só aparece depois do primeiro golpe
    }

    private Transform Quad(string nome, Material mat)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = nome;
        Destroy(q.GetComponent<Collider>());
        q.GetComponent<Renderer>().sharedMaterial = mat;
        q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        q.transform.SetParent(barra, false);
        return q.transform;
    }
}
