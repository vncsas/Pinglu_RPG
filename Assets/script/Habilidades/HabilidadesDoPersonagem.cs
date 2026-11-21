using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ClasseDeHabilidade { Cavaleiro, Tanque, Maga, Curandeiro }

// Dados de uma habilidade (o menu Pinglu > 76 preenche tudo; dá para ajustar no Inspector).
[System.Serializable]
public class DadosDeHabilidade
{
    public string nome = "Habilidade";
    [TextArea(1, 3)] public string descricao = "";
    [Tooltip("Quanto da barra azul ela gasta (0,33 = 33%)")]
    [Range(0f, 1f)] public float custo = 0.33f;
    [Tooltip("Segundos até poder usar de novo")]
    public float recarga = 1.5f;

    [Header("Animação")]
    [Tooltip("Animação do Mixamo (só usada para saber a duração; quem toca é o Animator, camada Habilidades)")]
    public AnimationClip clipe;
    [Tooltip("Se não houver animação do Mixamo, toca este estado do Animator")]
    public string estadoAlternativo = "attack_heavy";
    public float duracao = 1.4f;
    [Tooltip("Em que ponto da animação (0 a 1) o efeito acontece")]
    [Range(0.05f, 0.95f)] public float momentoDoEfeito = 0.5f;

    [Header("Números")]
    public float dano = 50f;
    public float raio = 6f;
    [Tooltip("Cura, curas por segundo, etc. (depende da habilidade)")]
    public float valor = 20f;
    [Tooltip("Quanto tempo o efeito dura, em segundos")]
    public float duracaoEfeito = 5f;

    [Header("Efeitos visuais")]
    public GameObject efeito;
    public GameObject efeito2;
    public Vector3 rotacaoDoEfeito;
    public float escalaDoEfeito = 1f;
}

// As 3 habilidades especiais de cada personagem. A barra azul (CombateJogador.Especial) é a energia:
// teclas 1, 2 e 3 usam a habilidade se a barra tiver o custo em %. Nenhuma habilidade machuca outro jogador.
//   Cavaleiro: Corte Vertical / Bicuda / Aplicar Julgamento      Tank: Provocar / Tremor / O Imbatível
//   Maga: Queda de Raios / Impulso Cortante / Tormenta Final      Curandeiro: Curar / Impulsionar / Até a Morte
[RequireComponent(typeof(PlayerLocomotion))]
[RequireComponent(typeof(CombateJogador))]
public class HabilidadesDoPersonagem : MonoBehaviour
{
    public ClasseDeHabilidade classe;
    public DadosDeHabilidade[] habilidades = new DadosDeHabilidade[3];
    [Tooltip("Material do desenho dos raios (Maga)")]
    public Material materialRaio;
    [Tooltip("Pontos de energia ganhos por segundo, parado ou lutando (personagens de apoio)")]
    public float regenPorSegundo = 0f;

    // modo "demonstração" (tutorial): o personagem de mostruário usa a habilidade sem gastar energia e sem olhar para a câmera do jogo
    [System.NonSerialized] public bool demonstracao;
    public void Demonstrar(int i) { if (Usando || Dados(i) == null) return; demonstracao = true; StartCoroutine(Executar(i)); }

    public bool Usando { get; private set; }
    public int IndiceEmUso { get; private set; } = -1;
    public event System.Action<int> AoUsar;

    PlayerLocomotion loco; CombateJogador combate; Vida vida; Animator anim; StatusDeBatalha status;
    readonly float[] recargaAte = new float[3];
    readonly float[] recargaTotal = new float[3];
    readonly float[] falhouEm = { -99f, -99f, -99f };
    bool ultimaProntaAvisada;

    // ---------------------------------------------------------------- para a interface
    public DadosDeHabilidade Dados(int i) { return habilidades != null && i >= 0 && i < habilidades.Length ? habilidades[i] : null; }
    public float RecargaFracao(int i) { return recargaTotal[i] > 0f ? Mathf.Clamp01((recargaAte[i] - Time.time) / recargaTotal[i]) : 0f; }
    public float FalhouHa(int i) { return Time.time - falhouEm[i]; }
    public bool Disponivel(int i)
    {
        DadosDeHabilidade h = Dados(i);
        return h != null && combate != null && Time.time >= recargaAte[i] && combate.EspecialFracao >= h.custo - 0.001f;
    }

    // ---------------------------------------------------------------- ciclo de vida
    void Awake()
    {
        loco = GetComponent<PlayerLocomotion>(); combate = GetComponent<CombateJogador>(); vida = GetComponent<Vida>();
        anim = GetComponent<Animator>(); if (anim == null) anim = GetComponentInChildren<Animator>();
        status = GetComponent<StatusDeBatalha>(); if (status == null) status = gameObject.AddComponent<StatusDeBatalha>();
        vida.AoMorrer += Abortar;
    }

    bool Ativo
    {
        get { return loco != null && loco.enabled && !loco.Companheiro && loco.Controlavel && !vida.Morto && !SessaoDeJogo.MenuAberto && !DialogoUI.Aberto; }
    }

    void Update()
    {
        if (combate == null) return;
        if (regenPorSegundo > 0f && !vida.Morto) combate.GanharEspecial(regenPorSegundo * Time.deltaTime);
        if (!Ativo) return;

        // avisa uma vez quando a barra chega no custo da ultimate
        DadosDeHabilidade ult = Dados(2);
        bool pronta = ult != null && combate.EspecialFracao >= ult.custo - 0.001f;
        if (pronta && !ultimaProntaAvisada) HUDJogador.Mostrar("ULTIMATE PRONTA!  (3)", 1.6f);
        ultimaProntaAvisada = pronta;

        if (Usando) return;
        EntradaDoPersonagem ent = loco.Entrada;
        for (int i = 0; i < 3; i++) if (ent.HabilidadeApertou(i)) Tentar(i);
    }

    void Tentar(int i)
    {
        DadosDeHabilidade h = Dados(i);
        if (h == null || Usando || loco.Voando || loco.Rolando) return;
        if (Time.time < recargaAte[i] || combate.EspecialFracao < h.custo - 0.001f) { falhouEm[i] = Time.time; return; }
        combate.GastarEspecial(Mathf.Min(h.custo * combate.EspecialMaximo, combate.Especial));
        StartCoroutine(Executar(i));
    }

    void Abortar()
    {
        if (!Usando) return;
        StopAllCoroutines();
        Usando = false; IndiceEmUso = -1;
        if (loco != null) loco.TravadoPorHabilidade = false;
    }

    void OnDisable() { if (Usando) Abortar(); }

    // ---------------------------------------------------------------- uso
    IEnumerator Executar(int i)
    {
        DadosDeHabilidade h = habilidades[i];
        Usando = true; IndiceEmUso = i;
        recargaTotal[i] = h.recarga; recargaAte[i] = Time.time + h.recarga + 60f;   // só começa a contar quando a animação acaba
        loco.ResetarEstado();
        loco.TravadoPorHabilidade = true;
        loco.MarcarCombate();
        float sacar = loco.SacarSeGuardada();
        if (sacar > 0f) yield return new WaitForSeconds(sacar);
        VirarParaAlvo(i);
        AoUsar?.Invoke(i);

        bool camada = TocarAnimacao(i, h);
        float dur = h.clipe != null ? h.clipe.length : h.duracao;
        if (classe == ClasseDeHabilidade.Tanque && i == 1) StartCoroutine(Deslizar(-transform.forward, 5.5f, dur * h.momentoDoEfeito));   // mortal para trás

        yield return new WaitForSeconds(dur * h.momentoDoEfeito);
        yield return StartCoroutine(Efeito(i, h));
        yield return new WaitForSeconds(Mathf.Max(0.05f, dur * (1f - h.momentoDoEfeito)));

        if (!camada && anim != null && anim.HasState(0, Animator.StringToHash("Blend Tree"))) anim.CrossFadeInFixedTime("Blend Tree", 0.2f, 0, 0f);
        loco.TravadoPorHabilidade = false;
        Usando = false; IndiceEmUso = -1;
        recargaAte[i] = Time.time + h.recarga;
    }

    // devolve true se tocou pela camada "Habilidades" (animação do Mixamo)
    bool TocarAnimacao(int i, DadosDeHabilidade h)
    {
        if (anim == null) return false;
        string estado = "Hab" + (i + 1);
        int cam = anim.GetLayerIndex("Habilidades");
        if (cam >= 0 && (anim.HasState(cam, Animator.StringToHash(estado)) || anim.HasState(cam, Animator.StringToHash("Habilidades." + estado))))
        {
            anim.SetLayerWeight(cam, 1f);
            anim.CrossFadeInFixedTime(estado, 0.1f, cam, 0f);
            return true;
        }
        if (!string.IsNullOrEmpty(h.estadoAlternativo) && (anim.HasState(0, Animator.StringToHash(h.estadoAlternativo)) || anim.HasState(0, Animator.StringToHash("Base Layer." + h.estadoAlternativo))))
            anim.CrossFadeInFixedTime(h.estadoAlternativo, 0.1f, 0, 0f);
        return false;
    }

    // ---------------------------------------------------------------- mira e alvos
    // para onde a câmera olha (plano), puxando para o inimigo mais perto dessa direção
    void VirarParaAlvo(int i)
    {
        if (demonstracao) return;
        Camera cam = Camera.main;
        Vector3 f = cam != null ? cam.transform.forward : transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.01f) f = transform.forward;
        f.Normalize();
        bool ataque = classe == ClasseDeHabilidade.Cavaleiro || (classe == ClasseDeHabilidade.Maga && i == 1);
        if (ataque)
        {
            Vida melhor = null; float melhorAng = 55f;
            foreach (Collider c in Physics.OverlapSphere(transform.position, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vida v = c.GetComponentInParent<Vida>();
                if (v == null || v.Morto || EhAliado(v.transform)) continue;
                Vector3 d = v.transform.position - transform.position; d.y = 0f;
                float ang = Vector3.Angle(f, d);
                if (ang < melhorAng) { melhorAng = ang; melhor = v; }
            }
            if (melhor != null) { Vector3 d = melhor.transform.position - transform.position; d.y = 0f; f = d.normalized; }
        }
        transform.rotation = Quaternion.LookRotation(f, Vector3.up);
    }

    public bool EhAliado(Transform t) { return t == transform || t.IsChildOf(transform) || t.GetComponentInParent<PlayerLocomotion>() != null; }

    // inimigos (nunca jogadores) tocados pelos colisores
    List<Vida> Inimigos(Collider[] cols)
    {
        List<Vida> r = new List<Vida>();
        foreach (Collider c in cols)
        {
            Vida v = c.GetComponentInParent<Vida>();
            if (v == null || v.Morto || EhAliado(v.transform) || r.Contains(v)) continue;
            r.Add(v);
        }
        return r;
    }
    List<Vida> InimigosNaEsfera(Vector3 centro, float raio) { return Inimigos(Physics.OverlapSphere(centro, raio, ~0, QueryTriggerInteraction.Ignore)); }

    bool Ferir(Vida v, float dano, Vector3 origem)
    {
        if (v == null || !v.ReceberDano(dano * StatusDeBatalha.DanoDe(gameObject), origem)) return false;
        loco.MarcarCombate();
        return true;
    }

    void Empurrar(Vida v, Vector3 de, float forca)
    {
        IAfetavelPorHabilidade e = v.GetComponent<IAfetavelPorHabilidade>();
        if (e == null) return;
        Vector3 d = v.transform.position - de; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) d = transform.forward;
        e.Empurrar(d.normalized * forca);
    }

    List<PlayerLocomotion> Aliados()
    {
        List<PlayerLocomotion> r = new List<PlayerLocomotion>();
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            Vida v = p.GetComponent<Vida>();
            if (v == null || v.Morto) continue;
            r.Add(p);
        }
        return r;
    }

    static Vector3 NoChao(Vector3 p)
    {
        float melhor = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<Vida>() != null) continue;
            if (h.point.y > melhor) melhor = h.point.y;
        }
        if (melhor > float.NegativeInfinity) p.y = melhor;
        return p;
    }

    GameObject Surgir(GameObject prefab, Vector3 pos, Quaternion rot, float vidaUtil, float escala = 1f, Transform pai = null)
    {
        if (prefab == null) return null;
        GameObject g = Instantiate(prefab, pos, rot);
        if (Mathf.Abs(escala - 1f) > 0.001f) g.transform.localScale *= escala;
        if (pai != null) g.transform.SetParent(pai, true);
        Destroy(g, vidaUtil);
        return g;
    }

    IEnumerator Deslizar(Vector3 dir, float distancia, float tempo)
    {
        CharacterController cc = GetComponent<CharacterController>();
        dir.y = 0f; dir.Normalize();
        float t = 0f;
        while (t < tempo && cc != null)
        {
            float dt = Time.deltaTime; t += dt;
            if (cc.enabled) cc.Move(dir * (distancia / tempo) * dt);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- as 12 habilidades
    IEnumerator Efeito(int i, DadosDeHabilidade h)
    {
        switch (classe)
        {
            case ClasseDeHabilidade.Cavaleiro:
                if (i == 0) CorteVertical(h); else if (i == 1) Bicuda(h); else Julgamento(h);
                break;
            case ClasseDeHabilidade.Tanque:
                if (i == 0) Provocar(h); else if (i == 1) Tremor(h); else Imbativel(h);
                break;
            case ClasseDeHabilidade.Maga:
                if (i == 0) StartCoroutine(QuedaDeRaios(h)); else if (i == 1) StartCoroutine(ImpulsoCortante(h)); else StartCoroutine(TormentaFinal(h));
                break;
            case ClasseDeHabilidade.Curandeiro:
                if (i == 0) Curar(h); else if (i == 1) Impulsionar(h); else StartCoroutine(AteAMorte(h));
                break;
        }
        yield break;
    }

    // ---- Cavaleiro
    void CorteVertical(DadosDeHabilidade h)
    {
        Vector3 f = transform.forward, centro = transform.position + f * 2.6f + Vector3.up;
        Quaternion rot = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(h.rotacaoDoEfeito);
        Surgir(h.efeito, transform.position + Vector3.up * 1.1f + f * 1.5f, rot, 3f, h.escalaDoEfeito);
        Surgir(h.efeito2, NoChao(transform.position + f * 2.8f), Quaternion.identity, 4f, h.escalaDoEfeito);
        foreach (Vida v in Inimigos(Physics.OverlapBox(centro, new Vector3(1.3f, 1.8f, 2.8f), transform.rotation, ~0, QueryTriggerInteraction.Ignore)))
            if (Ferir(v, h.dano, transform.position)) Empurrar(v, transform.position, 3f);
        ThirdPersonCameraFollow.Tremer(0.18f, 0.25f);
    }

    void Bicuda(DadosDeHabilidade h)
    {
        Vector3 f = transform.forward;
        foreach (Vida v in InimigosNaEsfera(transform.position + f * 1.4f + Vector3.up, 1.5f))
            if (Ferir(v, h.dano, transform.position)) Empurrar(v, transform.position, 11f);
        ThirdPersonCameraFollow.Tremer(0.2f, 0.2f);
    }

    void Julgamento(DadosDeHabilidade h)
    {
        Vector3 f = transform.forward, centro = transform.position + f * 1.5f;
        Surgir(h.efeito, NoChao(centro), Quaternion.Euler(h.rotacaoDoEfeito), 5f, h.escalaDoEfeito);
        Surgir(h.efeito2, NoChao(centro) + Vector3.up, Quaternion.identity, 3f, h.escalaDoEfeito);
        foreach (Vida v in InimigosNaEsfera(centro, h.raio))
            if (Ferir(v, h.dano, centro)) Empurrar(v, centro, 7f);
        ThirdPersonCameraFollow.Tremer(0.4f, 0.5f);
    }

    // ---- Tanque
    void Provocar(DadosDeHabilidade h)
    {
        Surgir(h.efeito, transform.position + Vector3.up * 1.3f, Quaternion.Euler(h.rotacaoDoEfeito), 3f, h.escalaDoEfeito);
        foreach (Vida v in InimigosNaEsfera(transform.position, h.raio))
        {
            IAfetavelPorHabilidade e = v.GetComponent<IAfetavelPorHabilidade>();
            if (e != null) e.Provocar(transform, h.duracaoEfeito);
        }
        ThirdPersonCameraFollow.Tremer(0.15f, 0.3f);
    }

    void Tremor(DadosDeHabilidade h)
    {
        Surgir(h.efeito, NoChao(transform.position), Quaternion.identity, 4f, h.escalaDoEfeito);
        foreach (Vida v in InimigosNaEsfera(transform.position, h.raio))
        {
            if (!Ferir(v, h.dano, transform.position)) continue;
            Empurrar(v, transform.position, 5f);
            IAfetavelPorHabilidade e = v.GetComponent<IAfetavelPorHabilidade>();
            if (e != null) e.Lentidao(0.5f, h.duracaoEfeito);
        }
        ThirdPersonCameraFollow.Tremer(0.35f, 0.4f);
    }

    void Imbativel(DadosDeHabilidade h)
    {
        vida.TornarImortal(h.duracaoEfeito);
        Surgir(h.efeito, transform.position, transform.rotation, h.duracaoEfeito, h.escalaDoEfeito, transform);
        HUDJogador.Mostrar("IMBATÍVEL!", 1.2f);
        ThirdPersonCameraFollow.Tremer(0.12f, 0.3f);
    }

    // ---- Maga
    IEnumerator QuedaDeRaios(DadosDeHabilidade h)
    {
        Vector3 centro = transform.position;
        float fim = Time.time + h.duracaoEfeito;
        while (Time.time < fim)
        {
            Vector3 alvo;
            List<Vida> perto = InimigosNaEsfera(centro, h.raio + 3f);
            if (perto.Count > 0 && Random.value < 0.7f) alvo = perto[Random.Range(0, perto.Count)].transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            else { Vector2 c = Random.insideUnitCircle * h.raio; alvo = centro + new Vector3(c.x, 0f, c.y); }
            alvo = NoChao(alvo);
            DesenharRaio(alvo);
            Surgir(h.efeito, alvo, Quaternion.identity, 2.5f, h.escalaDoEfeito);
            foreach (Vida v in InimigosNaEsfera(alvo, 2.3f)) Ferir(v, h.dano, alvo);
            ThirdPersonCameraFollow.Tremer(0.05f, 0.12f);
            yield return new WaitForSeconds(0.5f);   // 2 raios por segundo
        }
    }

    static Material materialReserva;
    void DesenharRaio(Vector3 alvo)
    {
        GameObject g = new GameObject("Raio caindo");
        LineRenderer lr = g.AddComponent<LineRenderer>();
        int n = 10; lr.positionCount = n; lr.useWorldSpace = true; lr.numCapVertices = 2; lr.textureMode = LineTextureMode.Stretch;
        Vector3 topo = alvo + Vector3.up * 30f + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
        for (int j = 0; j < n; j++)
        {
            Vector3 p = Vector3.Lerp(topo, alvo, j / (float)(n - 1));
            if (j > 0 && j < n - 1) p += new Vector3(Random.Range(-1.4f, 1.4f), 0f, Random.Range(-1.4f, 1.4f));
            lr.SetPosition(j, p);
        }
        lr.startWidth = 0.5f; lr.endWidth = 0.22f;
        Color c = new Color(0.75f, 0.85f, 1f, 1f); lr.startColor = c; lr.endColor = c;
        if (materialRaio != null) lr.sharedMaterial = materialRaio;
        else
        {
            if (materialReserva == null) materialReserva = new Material(Shader.Find("Sprites/Default"));
            lr.sharedMaterial = materialReserva;
        }
        Destroy(g, 0.22f);
    }

    IEnumerator ImpulsoCortante(DadosDeHabilidade h)
    {
        Vector3 f = transform.forward, inicio = transform.position + Vector3.up * 1.3f + f * 1.1f;
        Surgir(h.efeito, inicio, Quaternion.LookRotation(f, Vector3.up), 1.8f);                                  // portal azul
        yield return new WaitForSeconds(0.25f);
        GameObject corte = Surgir(h.efeito2, inicio, Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(h.rotacaoDoEfeito), 3f, h.escalaDoEfeito);
        List<Vida> atingidos = new List<Vida>();
        Vector3 pos = inicio; float t = 0f;
        while (t < 1.2f)
        {
            float dt = Time.deltaTime; t += dt;
            pos += f * 24f * dt;
            if (corte != null) corte.transform.position = pos;
            foreach (Vida v in InimigosNaEsfera(pos, 1.7f))
            {
                if (atingidos.Contains(v)) continue;
                atingidos.Add(v);
                Ferir(v, h.dano, pos - f);
            }
            yield return null;
        }
        if (corte != null) Destroy(corte);
    }

    IEnumerator TormentaFinal(DadosDeHabilidade h)
    {
        Vector3 centro = NoChao(transform.position);
        Surgir(h.efeito, centro + Vector3.up * 0.05f, Quaternion.Euler(h.rotacaoDoEfeito), h.duracaoEfeito, h.escalaDoEfeito);
        yield return new WaitForSeconds(1.0f);
        foreach (Vida v in InimigosNaEsfera(centro, h.raio)) Ferir(v, h.dano, centro);
        ThirdPersonCameraFollow.Tremer(0.4f, 0.6f);
    }

    // ---- Curandeiro
    void Curar(DadosDeHabilidade h)
    {
        foreach (PlayerLocomotion p in Aliados())
        {
            p.GetComponent<Vida>().Curar(h.valor);
            Surgir(h.efeito, p.transform.position, Quaternion.identity, 3f, h.escalaDoEfeito, p.transform);
        }
        loco.MarcarCombate();
    }

    void Impulsionar(DadosDeHabilidade h)
    {
        foreach (PlayerLocomotion p in Aliados())
        {
            StatusDeBatalha s = p.GetComponent<StatusDeBatalha>();
            if (s == null) s = p.gameObject.AddComponent<StatusDeBatalha>();
            s.Impulsionar(h.duracaoEfeito, h.efeito);
        }
        loco.MarcarCombate();
    }

    IEnumerator AteAMorte(DadosDeHabilidade h)
    {
        Vector3 centro = NoChao(transform.position);
        GameObject circulo = Surgir(h.efeito, centro + Vector3.up * 0.05f, Quaternion.Euler(h.rotacaoDoEfeito), h.duracaoEfeito + 1f, h.escalaDoEfeito);
        float fim = Time.time + h.duracaoEfeito;
        while (Time.time < fim)
        {
            foreach (PlayerLocomotion p in Aliados())
            {
                Vector3 d = p.transform.position - centro; d.y = 0f;
                if (d.magnitude <= h.raio) p.GetComponent<Vida>().Curar(h.valor);
            }
            yield return new WaitForSeconds(1f);
        }
        if (circulo != null) Destroy(circulo);
    }
}
