using UnityEngine;

public enum PapelCPU { Corpo, Tanque, Maga, Curandeiro }

// O "cérebro" de um companheiro CPU. Ele nunca mexe no corpo direto: só preenche a EntradaCPU
// (andar, atacar, defender, rolar) e o PlayerLocomotion faz o resto, igual faria com o teclado.
// Por isso o personagem usa as mesmas animações, combos e habilidades de quando você joga com ele.
//
// Prioridade a cada quadro:  parado (morto/cena/menu) > teleporte > esquivar de golpe grande >
//                            curar (Curandeiro) > lutar > seguir o jogador.
[DefaultExecutionOrder(-50)]
public class CerebroCompanheiro : MonoBehaviour
{
    public PapelCPU papel;
    public string Estado { get; private set; }
    public bool Vivo { get { return vida != null && !vida.Morto; } }
    public Vida VidaDele { get { return vida; } }
    public PlayerLocomotion Loco { get { return loco; } }
    public GrupoDeAventureiros.InimigoVisto AlvoAtual { get { return alvo; } }

    PlayerLocomotion loco; EntradaCPU ent; Vida vida; CharacterController cc;
    GrupoDeAventureiros.InimigoVisto alvo;
    float proximaEscolha, proximoClique, guardaAte, semGuardaAte, atrasoAte, proximoRolar;
    float travado, ultimoTesteDeTravado, proximoPulo; Vector3 ultimaPos; bool quisAndar;
    bool andando, correndo; float lado = 1f;
    Vector3 dirHumano = Vector3.forward, posHumanoAntes; float velHumano;

    void Awake()
    {
        loco = GetComponent<PlayerLocomotion>(); ent = GetComponent<EntradaCPU>(); vida = GetComponent<Vida>(); cc = GetComponent<CharacterController>();
        Estado = ""; ultimaPos = transform.position;
    }

    static float Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    static Vector3 Dir(Vector3 de, Vector3 para) { Vector3 d = para - de; d.y = 0f; return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero; }

    // ------------------------------------------------------------------
    void Update()
    {
        ent.direcaoDeMundo = Vector3.zero; ent.correr = false; ent.defender = false; ent.temMira = false;
        Transform hum = GrupoDeAventureiros.Humano;
        if (!Vivo || !loco.Controlavel) { Estado = "Caído"; return; }
        if (SessaoDeJogo.MenuAberto || CameraDeCena.Ativa || hum == null) { Estado = "Aguardando"; return; }

        float dt = Time.deltaTime;
        Vector3 dh = hum.position - posHumanoAntes; dh.y = 0f; posHumanoAntes = hum.position;
        float v = dt > 0f ? dh.magnitude / dt : 0f;
        if (v < 40f) velHumano = Mathf.Lerp(velHumano, v, dt * 6f);
        if (v > 0.8f && v < 40f) dirHumano = Vector3.Slerp(dirHumano, dh.normalized, dt * 4f);

        Vector3 meu = transform.position;
        // longe demais (o jogador foi teleportado, passou por um portal...): volta para perto dele
        if (Plano(meu, hum.position) > 24f) { Teleportar(PosDoSlot(hum)); return; }
        if (Esquivar(meu)) return;

        if (papel == PapelCPU.Curandeiro) { Curandeiro(hum, meu); return; }

        bool reunir = GrupoDeAventureiros.Reunindo;
        if (!reunir) EscolherAlvo(hum, meu); else alvo = null;
        if (alvo != null)
        {
            if (papel == PapelCPU.Maga) LutarMaga(meu); else LutarCorpoACorpo(meu);
            return;
        }
        Seguir(hum, meu, reunir ? "Reunindo" : "Seguindo", reunir);
    }

    void LateUpdate()
    {
        if (!quisAndar) { travado = 0f; ultimaPos = transform.position; ultimoTesteDeTravado = Time.time; }
        quisAndar = false;
    }

    // ------------------------------------------------------------------
    //  esquivar dos golpes grandes dos chefes
    // ------------------------------------------------------------------
    bool Esquivar(Vector3 meu)
    {
        foreach (PerigoNoChao.Perigo p in PerigoNoChao.Ativos)
        {
            Vector3 c; if (!PerigoNoChao.Centro(p, out c)) continue;
            Vector3 d = meu - c; d.y = 0f; float dist = d.magnitude;
            Vector3 fuga = dist > 0.05f ? d / dist : -transform.forward;
            if (p.linha)
            {
                // raio em linha: sai para o lado, para longe da linha
                Vector3 dl; if (!PerigoNoChao.Direcao(p, out dl) || Time.time < p.inicio) continue;
                float ao = Vector3.Dot(d, dl);
                if (ao < -1.5f || ao > p.comprimento + 1.5f) continue;
                Vector3 perp = d - dl * ao; float lateral = perp.magnitude;
                if (lateral < p.raio + 1.6f)
                {
                    ent.direcaoDeMundo = lateral > 0.05f ? perp / lateral : Vector3.Cross(Vector3.up, dl); ent.correr = true; Estado = "Fugindo do raio";
                    if (lateral < p.raio + 0.5f && Time.time >= proximoRolar) { ent.Rolar(); proximoRolar = Time.time + 0.9f; }
                    return true;
                }
                continue;
            }
            if (p.anel)
            {
                float chega = p.impacto + dist / p.velocidade - Time.time;      // quando a onda passa por mim
                if (dist < p.raio + 1f && chega > -0.05f && chega < 0.32f && Time.time >= proximoRolar)
                {
                    ent.direcaoDeMundo = fuga; ent.Rolar(); proximoRolar = Time.time + 0.9f; Estado = "Esquivando"; return true;
                }
            }
            else if (dist < p.raio + 0.8f && Time.time - p.inicio > 0.25f && p.impacto - Time.time > -0.1f)
            {
                ent.direcaoDeMundo = fuga; ent.correr = true; Estado = "Fugindo do golpe";
                if (p.impacto - Time.time < 0.3f && Time.time >= proximoRolar) { ent.Rolar(); proximoRolar = Time.time + 0.9f; }
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    //  seguir o jogador em formação (atrás e ao lado, cada papel no seu lugar)
    // ------------------------------------------------------------------
    Vector3 PosDoSlot(Transform hum)
    {
        Vector2 o;
        switch (papel)
        {
            case PapelCPU.Tanque: o = new Vector2(-2.6f, 0.8f); break;
            case PapelCPU.Corpo: o = new Vector2(2.6f, 0.4f); break;
            case PapelCPU.Maga: o = new Vector2(-2.2f, -2.8f); break;
            default: o = new Vector2(2.2f, -3.2f); break;
        }
        Vector3 f = dirHumano; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        return hum.position + r * o.x + f * o.y;
    }

    void Seguir(Transform hum, Vector3 meu, string rotulo, bool apressado)
    {
        Vector3 slot = PosDoSlot(hum);
        float d = Plano(meu, slot);
        if (d > (andando ? 0.9f : 1.8f)) andando = true; else andando = false;
        if (!andando) { Estado = "Parado"; return; }
        if (d > 3.2f || velHumano > 3.6f || (apressado && d > 2f)) correndo = true; else if (d < 1.4f) correndo = false;
        IrPara(slot, 0.8f, correndo);
        Estado = rotulo;
    }

    // anda em linha reta, desviando de obstáculos; se travar, pula e, em último caso, teleporta
    bool IrPara(Vector3 destino, float parar, bool correr)
    {
        Vector3 d = destino - transform.position; d.y = 0f; float dist = d.magnitude;
        if (dist <= parar) return false;
        Vector3 dir = Desviar(d / dist);
        ent.direcaoDeMundo = dir; ent.correr = correr; quisAndar = true;
        if (Time.time - ultimoTesteDeTravado > 1f)
        {
            if (Plano(transform.position, ultimaPos) < 0.35f) travado++; else travado = 0f;
            ultimaPos = transform.position; ultimoTesteDeTravado = Time.time;
            if (travado >= 3f) loco.ResetarEstado();             // se o corpo ficou preso num estado (golpe, defesa...), solta
            if (travado >= 2f && Time.time >= proximoPulo) { ent.Pular(); proximoPulo = Time.time + 1.2f; }
            if (travado >= 5f && GrupoDeAventureiros.Humano != null && Plano(transform.position, GrupoDeAventureiros.Humano.position) > 8f)
            { travado = 0f; Teleportar(PosDoSlot(GrupoDeAventureiros.Humano)); }
        }
        return true;
    }

    Vector3 Desviar(Vector3 dir)
    {
        Vector3 o = transform.position + Vector3.up * 0.9f;
        if (Livre(o, dir)) return dir;
        for (int i = 1; i <= 3; i++)
            for (int s = 0; s < 2; s++)
            {
                float sinal = s == 0 ? lado : -lado;
                Vector3 t = Quaternion.Euler(0f, sinal * 40f * i, 0f) * dir;
                if (Livre(o, t)) { lado = sinal; return t; }
            }
        return dir;
    }

    bool Livre(Vector3 origem, Vector3 dir)
    {
        RaycastHit h;
        if (!Physics.SphereCast(origem, 0.3f, dir, out h, 1.5f, ~0, QueryTriggerInteraction.Ignore)) return true;
        if (h.collider.transform.IsChildOf(transform)) return true;
        if (h.collider.GetComponentInParent<Vida>() != null) return true;      // gente não é parede
        return h.normal.y > 0.7f;                                              // subida suave dá para andar
    }

    public void Teleportar(Vector3 p)
    {
        Terrain t = Terrain.activeTerrain;
        if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y + 0.3f;
        if (cc != null) cc.enabled = false;
        transform.position = p;
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;
        travado = 0f; ultimaPos = p;
    }

    // ------------------------------------------------------------------
    //  escolher quem atacar
    // ------------------------------------------------------------------
    void EscolherAlvo(Transform hum, Vector3 meu)
    {
        if (alvo != null && !alvo.Vivo) alvo = null;
        if (Time.time < proximaEscolha && alvo != null) return;
        proximaEscolha = Time.time + 0.45f;
        GrupoDeAventureiros.InimigoVisto antigo = alvo;

        GrupoDeAventureiros.InimigoVisto forcado = GrupoDeAventureiros.AlvoForcado;
        if (forcado != null) alvo = forcado;
        else
        {
            bool ajudando = GrupoDeAventureiros.AjudandoNaLuta(this);
            float raioCombate = ajudando ? 40f : (alvo != null ? 24f : 18f);          // não corre atrás de ninguém para longe do jogador
            Vector3 protegido = hum.position;
            if (papel == PapelCPU.Tanque) { Vector3 cura; if (GrupoDeAventureiros.PosDoCurandeiro(out cura)) protegido = cura; }
            GrupoDeAventureiros.InimigoVisto melhor = null; float menor = 1e9f;
            foreach (GrupoDeAventureiros.InimigoVisto e in GrupoDeAventureiros.Inimigos)
            {
                if (!e.Vivo) continue;
                Vector3 ep = e.t.position;
                if (Plano(ep, hum.position) > raioCombate || Plano(ep, meu) > 35f) continue;
                float s = papel == PapelCPU.Tanque ? Plano(ep, protegido) + Plano(ep, meu) * 0.3f : Plano(ep, meu);
                if (ajudando) s = Plano(ep, hum.position);
                s += GrupoDeAventureiros.QuantosAtacam(e, this) * 3f;
                if (s < menor) { menor = s; melhor = e; }
            }
            alvo = melhor;
        }
        if (alvo != null && alvo != antigo) atrasoAte = Time.time + Random.Range(0.15f, 0.45f);    // reação de gente, não de máquina
    }

    // ------------------------------------------------------------------
    //  lutar
    // ------------------------------------------------------------------
    void LutarCorpoACorpo(Vector3 meu)
    {
        Vector3 ap = alvo.t.position; float raioAlvo = alvo.Raio;
        float d = Mathf.Max(0f, Plano(meu, ap) - raioAlvo);
        Vector3 para = Dir(meu, ap);
        if (d > 1.9f) { IrPara(ap, raioAlvo + 1.3f, true); Estado = "Indo ao inimigo"; return; }

        ent.temMira = true; ent.frenteDeMira = para;
        // defesa quando está ferido (a cada tanto, para poder atacar também)
        float limite = papel == PapelCPU.Tanque ? 0.6f : 0.4f;
        if (Time.time >= guardaAte && vida.Fracao < limite && Time.time >= semGuardaAte) { guardaAte = Time.time + 1.3f; semGuardaAte = guardaAte + 1.0f; }
        if (Time.time < guardaAte) { ent.defender = true; Estado = "Defendendo"; return; }
        Estado = "Atacando";
        if (Time.time >= atrasoAte && Time.time >= proximoClique) { ent.Atacar(); proximoClique = Time.time + Random.Range(0.35f, 0.7f); }
    }

    void LutarMaga(Vector3 meu)
    {
        Vector3 ap = alvo.t.position; float raioAlvo = alvo.Raio;
        float d = Mathf.Max(0f, Plano(meu, ap) - raioAlvo);
        Vector3 para = Dir(meu, ap);
        if (d < 4.5f)
        {
            Vector3 fuga = -para; Estado = "Recuando";
            IrPara(meu + fuga * 6f, 0.5f, true);
            if (d < 2.2f && Time.time >= proximoRolar) { ent.direcaoDeMundo = fuga; ent.Rolar(); proximoRolar = Time.time + 3f; }
            return;
        }
        if (d > 13f) { IrPara(ap, raioAlvo + 11f, true); Estado = "Chegando perto"; return; }
        ent.temMira = true; ent.frenteDeMira = para; Estado = "Lançando magia";
        if (Time.time >= atrasoAte && Time.time >= proximoClique) { ent.Atacar(); proximoClique = Time.time + Random.Range(0.9f, 1.4f); }
    }

    // ------------------------------------------------------------------
    //  Curandeiro: cuida dos feridos, e fora isso acompanha o grupo
    // ------------------------------------------------------------------
    void Curandeiro(Transform hum, Vector3 meu)
    {
        Transform alvoCura = null; float pior = 0.8f;
        foreach (PlayerLocomotion p in GrupoDeAventureiros.Membros)
        {
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto) continue;
            float f = v.Fracao;
            if (p.gameObject == gameObject) f += 0.15f;         // ele mesmo vem por último
            if (f < pior) { pior = f; alvoCura = p.transform; }
        }
        if (GrupoDeAventureiros.AjudandoNaCura(this)) { Vida hv = hum.GetComponent<Vida>(); if (hv != null && !hv.Morto && hv.Fracao < 0.99f) alvoCura = hum; }

        if (alvoCura != null && !GrupoDeAventureiros.Reunindo)
        {
            float d = Plano(meu, alvoCura.position);
            if (d > 5.2f) { IrPara(alvoCura.position, 4f, true); Estado = "Indo curar"; return; }
            Estado = "Curando";
            if (Time.time >= proximoClique) { ent.Atacar(); proximoClique = Time.time + 1.6f; }      // o golpe do Curandeiro é a cura
            return;
        }
        Seguir(hum, meu, GrupoDeAventureiros.Reunindo ? "Reunindo" : "Seguindo", GrupoDeAventureiros.Reunindo);
    }
}
