using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ondas de monstros. Começa quando o jogador chega perto (ou aperta F2 para
// ir direto até aqui). Cada onda tem mais inimigos e eles ficam mais fortes.
public class GerenciadorDeOndas : MonoBehaviour
{
    public static GerenciadorDeOndas Instancia { get; private set; }

    [System.Serializable]
    public class TipoDeMonstro
    {
        public string nome = "Esqueleto";
        public GameObject prefab;
        [Tooltip("A partir de qual onda este monstro pode aparecer")]
        public int aPartirDaOnda = 1;
        [Tooltip("Chance relativa de ser sorteado (maior = mais comum)")]
        public int peso = 10;
        [Tooltip("Quanto ele gasta do orçamento da onda (monstros fortes custam mais)")]
        public int custo = 1;
        [Tooltip("Chefe: aparece sozinho nas ondas de chefe, não entra no sorteio comum")]
        public bool chefe;
    }

    [Header("Monstro e pontos de onde ele sai")]
    [SerializeField] private GameObject prefabInimigo;
    [Tooltip("Tipos de monstro. Se estiver vazio, usa só o 'Prefab Inimigo'")]
    [SerializeField] private TipoDeMonstro[] tipos;
    [SerializeField] private Transform[] pontosDeSurgimento;
    [Tooltip("Onde o jogador aparece ao apertar F2 (se vazio, fica na frente deste objeto)")]
    [SerializeField] private Transform pontoDeChegada;

    [Header("Quando começa")]
    [SerializeField] private float distanciaParaComecar = 30f;
    [Tooltip("Os monstros da horda só perseguem quem está a esta distância do cemitério (0 = sem limite)")]
    [SerializeField] private float raioDosMonstros = 75f;
    [SerializeField] private KeyCode teclaIrAteAqui = KeyCode.F2;

    [Header("Chefe (história)")]
    [Tooltip("Quantas ondas comuns antes do chefe (0 = ondas sem fim, sem chefe)")]
    [SerializeField] private int ondasAntesDoChefe = 5;
    [Tooltip("Molde do Guardião das Lápides (o menu Pinglu > 64 preenche)")]
    [SerializeField] private GameObject prefabChefe;
    [Tooltip("Centro da arena, de onde o chefe sai do chão (o menu Pinglu > 64 preenche)")]
    [SerializeField] private Transform centroDaArena;

    [Header("Ondas")]
    [Tooltip("Orçamento da onda 1 (cada monstro gasta o seu custo)")]
    [SerializeField] private int inimigosNaPrimeiraOnda = 5;
    [Tooltip("Quanto o orçamento cresce a cada onda")]
    [SerializeField] private int aumentoPorOnda = 3;
    [Tooltip("Orçamento extra por jogador além do primeiro")]
    [SerializeField] private int extraPorJogador = 4;
    [SerializeField] private int maximoAoMesmoTempo = 10;
    [Tooltip("Limite extra ao mesmo tempo por jogador além do primeiro")]
    [SerializeField] private int maximoExtraPorJogador = 3;
    [Tooltip("A cada quantas ondas vem um chefe (0 = nunca)")]
    [SerializeField] private int chefeACadaNOndas = 5;
    [SerializeField] private float intervaloEntreSurgimentos = 1.2f;
    [SerializeField] private float descansoEntreOndas = 6f;
    [Tooltip("Quanto a vida dos monstros aumenta a cada onda (0.15 = +15%)")]
    [SerializeField] private float aumentoDeVidaPorOnda = 0.15f;
    [Tooltip("Quanto da vida o jogador recupera ao vencer uma onda (0.3 = 30%)")]
    [SerializeField] private float curaAoVencerOnda = 0.3f;

    // moldes dos monstros (usado pelo trailer)
    public GameObject[] PrefabsDeMonstros
    {
        get
        {
            List<GameObject> l = new List<GameObject>();
            foreach (TipoDeMonstro t in TiposValidos()) l.Add(t.prefab);
            return l.ToArray();
        }
    }

    public int OndaAtual { get; private set; }
    public int Vivos => vivos.Count;
    public int Restantes { get; private set; }
    public bool Ativo { get; private set; }

    private readonly List<InimigoEsqueleto> vivos = new List<InimigoEsqueleto>();
    private readonly List<Transform> jogadores = new List<Transform>();
    private float proximaBusca;

    // CHECKPOINT: a onda em que o grupo estava. Se todos morrem e a cena recarrega (R), a horda volta NESSA onda, não da primeira.
    // Fica na memória só enquanto o jogo está aberto; ao começar um Play novo, volta a zero.
    public static int CheckpointOnda;
    private static Etapa checkpointEtapa;
    private bool voltouAoCheckpoint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ZerarCheckpoint() { CheckpointOnda = 0; HordaVencida = false; }

    // true depois que o chefe cai: a horda não recomeça mais
    public static bool HordaVencida;
    public static event System.Action AoVencerHorda;

    private void Awake()
    {
        Instancia = this;
        if (checkpointEtapa != Campanha.Atual) CheckpointOnda = 0;       // outra parte da história: começa limpo
        checkpointEtapa = Campanha.Atual;
        OndaAtual = Mathf.Max(0, CheckpointOnda - 1);                     // o próximo "OndaAtual++" cai exatamente na onda do checkpoint
    }

    private bool avisouInicio;
    private float vistoDesde = -1f;

    // todos os jogadores (no multiplayer, as cópias dos outros também)
    private void AtualizarJogadores()
    {
        jogadores.Clear();
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
            if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy) jogadores.Add(p.transform);
    }

    private Transform JogadorLocal()
    {
        foreach (Transform t in jogadores)
        {
            if (t == null) continue; // jogador destruído desde a última busca
            PlayerLocomotion p = t.GetComponent<PlayerLocomotion>();
            if (p != null && p.enabled && !p.Companheiro) return t;
        }
        return null;
    }

    private bool AlguemVivo()
    {
        foreach (Transform t in jogadores) { Vida v = t != null ? t.GetComponent<Vida>() : null; if (v != null && !v.Morto) return true; }
        return false;
    }

    private Transform MaisPerto(Vector3 p)
    {
        Transform melhor = null; float d = float.MaxValue;
        foreach (Transform t in jogadores) { if (t == null) continue; float dd = (t.position - p).sqrMagnitude; if (dd < d) { d = dd; melhor = t; } }
        return melhor;
    }

    // mensagem na tela (no multiplayer, aparece para todos)
    private static void Aviso(string texto, float tempo)
    {
        if (SessaoDeJogo.Online && SessaoDeJogo.AvisarTodos != null) SessaoDeJogo.AvisarTodos(texto, tempo);
        else HUDJogador.Mostrar(texto, tempo);
    }

    private void Update()
    {
        if (SessaoDeJogo.MenuAberto) return;
        if (Time.time >= proximaBusca) { proximaBusca = Time.time + 0.5f; AtualizarJogadores(); }
        Transform local = JogadorLocal();
        if (local == null) return;
        if (vistoDesde < 0f) vistoDesde = Time.time;
        // renasceu depois de morrer numa onda: volta direto para o cemitério, onde a horda continua da onda do checkpoint
        if (!voltouAoCheckpoint && CheckpointOnda > 0 && !Ativo && Time.time - vistoDesde > 2f) { voltouAoCheckpoint = true; LevarJogadorAteAqui(local); }
        // espera a chegada pelo portal terminar antes de dar a dica
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(teclaIrAteAqui)) LevarJogadorAteAqui(local);      // atalho só para testar; não existe no jogo final
        if (Input.GetKeyDown(KeyCode.F3)) IrDiretoParaOChefe(local);
        if (Input.GetKeyDown(KeyCode.F1)) IrDiretoParaORoubo();               // idem: vai direto para a cena do roubo do cristal           // idem: pula as 5 ondas e vai para o chefe
#endif

        // no multiplayer, só o host comanda as hordas
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) return;
        if (Ativo || HordaVencida) return;
        foreach (Transform t in jogadores)
            if (t != null && Vector3.Distance(t.position, transform.position) < distanciaParaComecar)
            {
                Ativo = true;
                StartCoroutine(Ciclo());
                break;
            }
    }

    // F3 (só no editor): cancela as ondas, leva o jogador ao cemitério e começa a cena do chefe
    private void IrDiretoParaOChefe(Transform jogador)
    {
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) return;
        if (PracaDoGuardiao.Instancia != null && PracaDoGuardiao.Instancia.Ocupada) return;   // a cena já está rodando
        StopAllCoroutines();
        foreach (InimigoEsqueleto e in vivos.ToArray()) if (e != null) Destroy(e.gameObject);
        vivos.Clear();
        if (jogador != null)
        {
            // vai para a praça, do lado do portão, olhando para o monumento (não para a entrada do cemitério)
            PracaDoGuardiao pr = PracaDoGuardiao.Instancia != null ? PracaDoGuardiao.Instancia : FindFirstObjectByType<PracaDoGuardiao>();
            Transform centroT = pr != null ? pr.transform : centroDaArena;
            if (centroT == null) LevarJogadorAteAqui(jogador);
            else
            {
                Vector3 c = pr != null ? pr.Centro() : centroT.position;
                Vector3 destino = c + centroT.forward * 28f;
                Terrain t = Terrain.activeTerrain;
                destino.y = (t != null ? t.SampleHeight(destino) + t.transform.position.y : c.y) + 0.2f;
                CharacterController cc = jogador.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                jogador.position = destino;
                Vector3 olhar = c - destino; olhar.y = 0f;
                if (olhar.sqrMagnitude > 0.01f) jogador.rotation = Quaternion.LookRotation(olhar);
                if (cc != null) cc.enabled = true;
            }
        }
        AtualizarJogadores();
        HordaVencida = false; Ativo = true; OndaAtual = ondasAntesDoChefe + 1; Restantes = 0;
        StartCoroutine(RotinaDoChefe());
    }

    private void LevarJogadorAteAqui(Transform jogador)
    {
        CharacterController cc = jogador.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        Vector3 destino = pontoDeChegada != null ? pontoDeChegada.position : transform.position + transform.forward * 16f;
        Terrain t = Terrain.activeTerrain;
        if (t != null) destino.y = t.SampleHeight(destino) + t.transform.position.y + 0.2f;
        jogador.position = destino;
        jogador.rotation = Quaternion.LookRotation(-transform.forward);
        if (cc != null) cc.enabled = true;
    }

    private IEnumerator Ciclo()
    {
        Aviso("Os mortos despertam...", 3f);
        yield return new WaitForSeconds(2.5f);

        while (AlguemVivo())
        {
            OndaAtual++;
            CheckpointOnda = OndaAtual;          // marca a onda onde o grupo está; se cair, volta aqui
            if (ondasAntesDoChefe > 0 && OndaAtual > ondasAntesDoChefe) { yield return RotinaDoChefe(); break; }
            List<TipoDeMonstro> fila = MontarOnda();
            Restantes = fila.Count;
            bool temChefe = fila.Exists(t => t.chefe);
            Aviso(temChefe ? "Onda " + OndaAtual + " - algo enorme desperta..." : "Onda " + OndaAtual, 2.5f);
            yield return new WaitForSeconds(1.5f);

            int indice = 0;
            while (Restantes > 0)
            {
                if (!AlguemVivo()) break;
                if (vivos.Count < LimiteAoMesmoTempo())
                {
                    Surgir(fila[indice++]);
                    Restantes--;
                    yield return new WaitForSeconds(intervaloEntreSurgimentos);
                }
                else yield return null;
            }
            while (vivos.Count > 0 && AlguemVivo()) yield return null;
            if (!AlguemVivo()) break;

            Aviso("Onda " + OndaAtual + " vencida!", 3f);
            foreach (Transform t in jogadores)
            {
                Vida v = t != null ? t.GetComponent<Vida>() : null;
                if (v != null) v.Curar(v.Maxima * curaAoVencerOnda);
            }
            yield return new WaitForSeconds(descansoEntreOndas);
        }
        if (SessaoDeJogo.Online)
        {
            // todos caíram: dá para renascer (R) e recomeçar chegando perto do cemitério
            Aviso("O grupo caiu na onda " + OndaAtual + "... Aperte R para renascer.", 5f);
            OndaAtual = Mathf.Max(0, CheckpointOnda - 1);       // renasce e a horda recomeça na onda em que caiu
            Ativo = false;
        }
    }

    // para testar a cena do chefe sem jogar as 5 ondas (menu Pinglu > 67)
    public void TestarChefeAgora() { AtualizarJogadores(); StartCoroutine(RotinaDoChefe()); }

    // última onda: o Guardião das Lápides sai do chão no centro do cemitério
    private IEnumerator RotinaDoChefe()
    {
        if (prefabChefe == null) { Aviso("Os mortos foram vencidos.", 3f); HordaVencida = true; Ativo = false; yield break; }
        Aviso("Onda " + OndaAtual + " - O Guardião desperta...", 3f);
        PracaDoGuardiao praca = PracaDoGuardiao.Instancia != null ? PracaDoGuardiao.Instancia : FindFirstObjectByType<PracaDoGuardiao>();
        ChefeGuardiao chefe = null;
        if (praca != null)
        {
            // cena de abertura: terremoto, o monumento desaba, o piso cai no buraco e o Guardião sobe de lá
            yield return new WaitForSeconds(2f);
            AtualizarJogadores();
            System.Func<Transform> criar = () =>
            {
                Vector3 ponto = praca.PontoDoChefe();
                Vector3 dirOlhar = Vector3.forward;
                Transform pj = MaisPerto(praca.Centro()); if (pj != null) { dirOlhar = pj.position - ponto; dirOlhar.y = 0f; }
                GameObject obj = Instantiate(prefabChefe, ponto, dirOlhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dirOlhar) : Quaternion.identity);
                chefe = obj.GetComponent<ChefeGuardiao>();
                if (chefe == null) { Destroy(obj); return null; }
                chefe.Vida.DefinirMaxima(chefe.vidaBase * (1f + 0.7f * Mathf.Max(0, jogadores.Count - 1)));
                chefe.Surgir(6.5f, 9f);
                return obj.transform;
            };
            yield return praca.Abrir(jogadores, criar, () => chefe != null && chefe.Surgiu);
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning("[Chefe] A praça do Guardião não está na cena: rode Pinglu > 66 (Play desligado) para ter a cena do buraco.");
            Aviso("Sem a cena do buraco: rode Pinglu > 66 com o Play desligado.", 6f);
#endif
            Vector3 centro = centroDaArena != null ? centroDaArena.position : transform.position;
            Terrain terr = Terrain.activeTerrain;
            if (terr != null) centro.y = terr.SampleHeight(centro) + terr.transform.position.y;
            Vector3 olhar = Vector3.forward;
            Transform perto = MaisPerto(centro); if (perto != null) { olhar = perto.position - centro; olhar.y = 0f; }
            GameObject go = Instantiate(prefabChefe, centro, olhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(olhar) : Quaternion.identity);
            chefe = go.GetComponent<ChefeGuardiao>();
            if (chefe != null) { chefe.Vida.DefinirMaxima(chefe.vidaBase * (1f + 0.7f * Mathf.Max(0, jogadores.Count - 1))); chefe.Surgir(6.5f, 7.5f); }
        }
        if (chefe == null) { HordaVencida = true; Ativo = false; yield break; }
        bool caiu = false; chefe.AoMorrer += c => caiu = true;
        while (!caiu && AlguemVivo()) yield return null;
        if (!caiu) yield break;                       // o grupo morreu: segue o fluxo normal de morte (volta na onda do chefe)
        yield return new WaitForSeconds(7.5f);        // espera o Guardião estilhaçar
        Aviso("O Guardião das Lápides caiu!", 3.5f);
        HordaVencida = true; CheckpointOnda = 0; OndaAtual = 0; Ativo = false;
        if (AoVencerHorda != null) AoVencerHorda();
        if ((int)Campanha.Atual <= (int)Etapa.Cemiterio && (!SessaoDeJogo.Online || SessaoDeJogo.SouHost))
        {
            yield return new WaitForSeconds(1.5f);
            System.Action seguir = () => DialogoUI.Abrir(Roteiro.RouboDoCristal, () => Campanha.Ir(Etapa.Castelo));
            if (praca != null) { Aviso("Um cristal surge do buraco!", 4f); yield return RotinaDoRoubo(praca, seguir); }
            else seguir();
        }
    }

    // o cristal sobe do buraco. Quando o jogador aperta B para pegá-lo, o personagem estica a mão; o ladrão aparece, levanta o dedo
    // e, com magia negra, puxa o cristal devagar para cima do dedo dele. Depois agradece e some na fumaça.
    private IEnumerator RotinaDoRoubo(PracaDoGuardiao praca, System.Action aoTerminar, bool direto = false)
    {
        Vector3 centro = praca.Centro();
        CristalDeHistoria cri = praca.RevelarCristal(new Color(0.25f, 1f, 0.45f), null, true);
        if (direto) cri.PularSubida();
        GameObject prefabLadrao = EncontroDoCastelo.Instancia != null ? EncontroDoCastelo.Instancia.prefabLadrao : null;
        bool apertou = direto; cri.tecla = KeyCode.B; cri.aoApertar = () => apertou = true;
        if (prefabLadrao == null) { cri.aoApertar = null; cri.podePegar = false; cri.Roubar(null, null); yield return new WaitForSeconds(2f); if (aoTerminar != null) aoTerminar(); yield break; }
        if (direto) yield return new WaitForSeconds(1.2f);
        while (!apertou) yield return null;

        bool menuAntes = SessaoDeJogo.MenuAberto;
        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar();
        InimigoCavaleiro lad = null; GestoDeBraco gladrao = null, gjogador = null; ChicoteNegro[] fios = null;
        try
        {
            AtualizarJogadores();
            Transform pj = JogadorLocal(); if (pj == null) pj = MaisPerto(centro);
            Vector3 flat(Vector3 v) { v.y = 0f; return v; }
            Vector3 paraCri = flat(cri.transform.position - pj.position); if (paraCri.sqrMagnitude < 0.25f) paraCri = flat(pj.forward); paraCri.Normalize();
            Vector3 lado = Vector3.Cross(Vector3.up, paraCri);
            // 1) o cristal vem até o personagem, que anda e estica a mão para pegar
            Vector3 alvoCri = pj.position + paraCri * 3.0f + Vector3.up * 1.5f;
            StartCoroutine(cri.PlanarPara(alvoCri, 2.0f));
            PlayerLocomotion loco = pj.GetComponent<PlayerLocomotion>();
            if (loco != null) loco.CaminharAutomatico(paraCri, 1.4f);
            gjogador = GestoDeBraco.Em(pj.gameObject);
            gjogador.Alcancar(() => cri.transform.position, 1.6f);
            Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao;
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, t / 1.0f);
                Vector3 pos = pj.position + lado * 3.0f - paraCri * 1.6f + Vector3.up * 1.6f;
                Vector3 olha = Vector3.Lerp(pj.position + Vector3.up * 1.4f, cri.transform.position, 0.6f);
                CameraDeCena.Definir(Vector3.Lerp(p0, pos, b), Quaternion.Slerp(r0, Quaternion.LookRotation(olha - pos), b));
                yield return null;
            }
            // 2) a mão quase toca o cristal... silêncio tenso
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                TremorDeCamera.Tremer(0.02f + 0.05f * t, 0.2f);
                Vector3 pos = pj.position + lado * (3.0f - 0.9f * t) - paraCri * 1.6f + Vector3.up * 1.6f;
                CameraDeCena.Definir(pos, Quaternion.LookRotation(Vector3.Lerp(pj.position + Vector3.up * 1.3f, cri.transform.position, 0.7f) - pos));
                yield return null;
            }
            // 3) o ladrão aparece do outro lado do buraco numa nuvem negra e levanta o dedo
            Vector3 dirP = flat(pj.position - centro); if (dirP.sqrMagnitude < 1f) dirP = praca.transform.forward; dirP.Normalize();
            Vector3 posL = centro - dirP * 15f;
            RaycastHit h; if (Physics.Raycast(posL + Vector3.up * 30f, Vector3.down, out h, 80f, ~0, QueryTriggerInteraction.Ignore)) posL.y = h.point.y;
            GameObject g = Instantiate(prefabLadrao, posL, Quaternion.LookRotation(dirP));
            lad = g.GetComponent<InimigoCavaleiro>();
            lad.ModoCena(); lad.MudarAura(new Color(0.35f, 0.07f, 0.5f)); lad.Fumaca();
            TremorDeCamera.Tremer(0.3f, 1.2f);
            gladrao = GestoDeBraco.Em(g); gladrao.LevantarDedo(1.4f);
            Vector3 posCam = pj.position - paraCri * 2.6f + lado * 1.4f + Vector3.up * 2.1f;
            Vector3 pA = CameraDeCena.Posicao; Quaternion rA = CameraDeCena.Rotacao;
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                Vector3 olha = Vector3.Lerp(cri.transform.position, lad.transform.position + Vector3.up * 1.6f, 0.55f);
                CameraDeCena.Definir(Vector3.Lerp(pA, posCam, b), Quaternion.Slerp(rA, Quaternion.LookRotation(olha - posCam), b));
                yield return null;
            }
            // 4) magia negra (devagar): fios de energia saem do dedo até o cristal e a aura negra cresce em volta dele
            fios = new ChicoteNegro[3];
            for (int i = 0; i < 3; i++) fios[i] = ChicoteNegro.Criar(() => gladrao.PontaDoDedo, () => cri.transform.position, 0.09f, 0.55f, i * 2.1f, 6f, i == 0);
            cri.AuraNegra(2.4f);
            for (int i = 0; i < 3; i++) StartCoroutine(fios[i].Estender(2.2f));
            for (float t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                TremorDeCamera.Tremer(0.04f + 0.07f * (t / 2.6f), 0.25f);
                if (t > 1.2f) gjogador.Soltar(1.2f);          // o braço do personagem cai: o cristal escapa da mão dele
                Vector3 olha = Vector3.Lerp(cri.transform.position, lad.transform.position + Vector3.up * 1.6f, 0.5f);
                CameraDeCena.Definir(posCam, Quaternion.Slerp(CameraDeCena.Rotacao, Quaternion.LookRotation(olha - posCam), Time.deltaTime * 4f));
                yield return null;
            }
            // 5) o cristal é puxado devagar, cruzando o buraco, até ficar flutuando em cima do dedo
            bool chegou = false;
            cri.manter = true; cri.encolher = false; cri.tempoVoo = 3.6f; cri.alvoCustom = () => gladrao.PontaDoDedo + Vector3.up * 0.65f;
            cri.Roubar(lad.transform, () => chegou = true);
            for (float t = 0f; !chegou && t < 6f; t += Time.deltaTime)
            {
                Vector3 olha = Vector3.Lerp(cri.transform.position, lad.transform.position + Vector3.up * 1.5f, 0.25f + 0.5f * Mathf.Clamp01(t / 3.6f));
                CameraDeCena.Definir(posCam, Quaternion.Slerp(CameraDeCena.Rotacao, Quaternion.LookRotation(olha - posCam), Time.deltaTime * 4f));
                if (Time.frameCount % 8 == 0) TremorDeCamera.Tremer(0.04f, 0.25f);
                yield return null;
            }
            foreach (ChicoteNegro f in fios) if (f != null) f.Desfazer();
            fios = null;
            // 6) plano fechado no ladrão, com o cristal negro flutuando em cima do dedo, e a conversa
            Vector3 posL2 = lad.transform.position + lad.transform.forward * 5.6f + Vector3.Cross(Vector3.up, lad.transform.forward) * 1.4f + Vector3.up * 1.0f;
            Vector3 pB = CameraDeCena.Posicao; Quaternion rB = CameraDeCena.Rotacao;
            bool fim = false;
            DialogoUI.Abrir(Roteiro.LadraoAgradece, () => fim = true);
            for (float t = 0f; !fim; t += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, t / 1.3f);
                Vector3 pos = Vector3.Lerp(pB, posL2 + lad.transform.forward * -Mathf.Min(t, 25f) * 0.035f, b);
                Vector3 olha = lad.transform.position + Vector3.up * 2.1f;
                CameraDeCena.Definir(pos, Quaternion.Slerp(rB, Quaternion.LookRotation(olha - pos), b));
                yield return null;
            }
            // 7) fecha a mão, a fumaça sobe e os dois somem
            lad.Fumaca(); TremorDeCamera.Tremer(0.2f, 0.6f);
            cri.Largar(); gladrao.Soltar(0.2f); lad.Sumir(); lad = null;
            yield return new WaitForSeconds(1.2f);
        }
        finally
        {
            if (fios != null) foreach (ChicoteNegro f in fios) if (f != null) f.Desfazer();
            if (gjogador != null) gjogador.Soltar(0.3f);
            if (lad != null) lad.Sumir();
            TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menuAntes;
        }
        yield return new WaitForSeconds(0.5f);
        if (aoTerminar != null) aoTerminar();
    }

    // F1 (só no editor): vai direto para a cena do roubo, com a praça já aberta
    private void IrDiretoParaORoubo()
    {
        PracaDoGuardiao pr = PracaDoGuardiao.Instancia != null ? PracaDoGuardiao.Instancia : FindFirstObjectByType<PracaDoGuardiao>();
        if (pr == null || pr.Ocupada || DialogoUI.Aberto) return;
        StopAllCoroutines();
        TituloDeChefe.Barras(false); TituloDeChefe.Esconder(); CameraDeCena.Soltar();
        foreach (CristalDeHistoria c in FindObjectsByType<CristalDeHistoria>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        foreach (InimigoCavaleiro e in FindObjectsByType<InimigoCavaleiro>(FindObjectsSortMode.None)) Destroy(e.gameObject);
        foreach (InimigoEsqueleto e in vivos.ToArray()) if (e != null) Destroy(e.gameObject);
        vivos.Clear();
        pr.AbrirSemCena();
        AtualizarJogadores();
        Transform jogador = JogadorLocal();
        if (jogador != null)
        {
            Vector3 destino = pr.Centro() + pr.transform.forward * 22f;
            Terrain t = Terrain.activeTerrain; destino.y = (t != null ? t.SampleHeight(destino) + t.transform.position.y : pr.Centro().y) + 0.3f;
            CharacterController cc = jogador.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            jogador.position = destino; Vector3 olhar = pr.Centro() - destino; olhar.y = 0f; jogador.rotation = Quaternion.LookRotation(olhar);
            if (cc != null) cc.enabled = true;
        }
        HordaVencida = true; CheckpointOnda = 0; Ativo = false;
        if ((int)Campanha.Atual < (int)Etapa.Cemiterio) Campanha.Ir(Etapa.Cemiterio);
        System.Action seguir = () => DialogoUI.Abrir(Roteiro.RouboDoCristal, () => Campanha.Ir(Etapa.Castelo));
        StartCoroutine(RotinaDoRoubo(pr, seguir, true));
    }

    private int LimiteAoMesmoTempo()
    {
        int extra = Mathf.Max(0, jogadores.Count - 1) * maximoExtraPorJogador;
        return Mathf.Min(maximoAoMesmoTempo + extra, 30);
    }

    private List<TipoDeMonstro> TiposValidos()
    {
        List<TipoDeMonstro> l = new List<TipoDeMonstro>();
        if (tipos != null) foreach (TipoDeMonstro t in tipos) if (t != null && t.prefab != null) l.Add(t);
        if (l.Count == 0 && prefabInimigo != null) l.Add(new TipoDeMonstro { nome = "Esqueleto", prefab = prefabInimigo });
        return l;
    }

    // sorteia os monstros da onda gastando um orçamento que cresce a cada onda
    private List<TipoDeMonstro> MontarOnda()
    {
        List<TipoDeMonstro> todos = TiposValidos();
        List<TipoDeMonstro> fila = new List<TipoDeMonstro>();
        if (todos.Count == 0) return fila;

        int orcamento = inimigosNaPrimeiraOnda + (OndaAtual - 1) * aumentoPorOnda;
        if (SessaoDeJogo.Online) orcamento += (jogadores.Count - 1) * extraPorJogador;

        if (chefeACadaNOndas > 0 && OndaAtual % chefeACadaNOndas == 0)
        {
            TipoDeMonstro chefe = todos.Find(t => t.chefe);
            if (chefe != null) { int quantos = 1 + OndaAtual / (chefeACadaNOndas * 2); for (int i = 0; i < quantos; i++) fila.Add(chefe); orcamento /= 2; }
        }

        List<TipoDeMonstro> comuns = todos.FindAll(t => !t.chefe && t.aPartirDaOnda <= OndaAtual);
        if (comuns.Count == 0) comuns = todos.FindAll(t => !t.chefe);
        if (comuns.Count == 0) return fila;

        int guarda = 0;
        while (orcamento > 0 && fila.Count < 80 && guarda++ < 400)
        {
            List<TipoDeMonstro> cabem = comuns.FindAll(t => Mathf.Max(1, t.custo) <= orcamento);
            if (cabem.Count == 0) break;
            int soma = 0; foreach (TipoDeMonstro t in cabem) soma += Mathf.Max(1, t.peso);
            int sorte = Random.Range(0, soma);
            TipoDeMonstro escolhido = cabem[0];
            foreach (TipoDeMonstro t in cabem) { sorte -= Mathf.Max(1, t.peso); if (sorte < 0) { escolhido = t; break; } }
            fila.Add(escolhido);
            orcamento -= Mathf.Max(1, escolhido.custo);
        }
        // mistura, deixando o chefe por último
        for (int i = fila.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            if (fila[i].chefe || fila[j].chefe) continue;
            TipoDeMonstro tmp = fila[i]; fila[i] = fila[j]; fila[j] = tmp;
        }
        fila.Sort((a, b) => a.chefe.CompareTo(b.chefe));
        return fila;
    }

    private void Surgir(TipoDeMonstro tipo)
    {
        if (tipo == null || tipo.prefab == null || pontosDeSurgimento == null || pontosDeSurgimento.Length == 0) return;

        // prefere pontos a uma boa distância dos jogadores (nem colados, nem longe demais)
        Transform ponto = pontosDeSurgimento[Random.Range(0, pontosDeSurgimento.Length)];
        float melhorNota = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Transform p = pontosDeSurgimento[Random.Range(0, pontosDeSurgimento.Length)];
            Transform perto = MaisPerto(p.position);
            float d = perto == null ? 20f : Vector3.Distance(p.position, perto.position);
            float nota = d < 8f ? 1000f - d : Mathf.Abs(d - 20f);
            if (nota < melhorNota) { melhorNota = nota; ponto = p; }
        }
        Vector3 pos = ponto.position;
        Terrain t = Terrain.activeTerrain;
        if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y;
        Transform alvo = MaisPerto(pos);
        Vector3 olhar = alvo != null ? alvo.position - pos : Vector3.zero; olhar.y = 0f;
        GameObject go = Instantiate(tipo.prefab, pos, olhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(olhar) : Quaternion.identity);

        InimigoEsqueleto ini = go.GetComponent<InimigoEsqueleto>();
        if (ini == null) return;
        ini.MultiplicarVida(1f + aumentoDeVidaPorOnda * (OndaAtual - 1));
        if (raioDosMonstros > 0f) ini.DefinirLimite(transform.position, raioDosMonstros);
        ini.AoMorrer += e => vivos.Remove(e);
        vivos.Add(ini);
        ini.Surgir();
        if (SessaoDeJogo.Online && SessaoDeJogo.SpawnarNaRede != null) SessaoDeJogo.SpawnarNaRede(go); // aparece para todos
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaParaComecar);
        if (pontosDeSurgimento == null) return;
        foreach (Transform p in pontosDeSurgimento) if (p != null) Gizmos.DrawSphere(p.position, 0.3f);
    }
}
