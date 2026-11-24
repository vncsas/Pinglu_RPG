using System.Collections;
using UnityEngine;

// A LUTA FINAL. Quando o último cristal é pego (em qualquer lugar) toca a CINEMÁTICA DO VILÃO (EncontroDoDragao_Cinematica.cs):
// o Ladrão revela o plano, os cristais se juntam e viram pó, a montanha racha e o dragão Drakthar sai de dentro dela e voa até o grupo.
// Depois vem o título e a luta. Vencido, o dragão cai, há uma conversa e a história segue para a recompensa do Caio.
// Se o grupo morrer, o dragão volta quando eles se aproximarem do lugar da batalha de novo (sem repetir a cinemática).
// Depois do último guardião: o Ladrão aparece, fala e FOGE deixando um RASTRO NEGRO até o portal, no meio do mapa (EncontroDoDragao_Perseguicao.cs).
// Lá toca a cinemática do vilão. F7 (só no editor): toca a cinemática do vilão na hora, onde você estiver (Shift+F7: a fuga).
// F8 (só no editor): vai direto para a FASE 2 (cena da fusão do Ladrão com o dragão e luta sombria). Ver EncontroDoDragao_Fase2.cs.
public partial class EncontroDoDragao : MonoBehaviour
{
    public static EncontroDoDragao Instancia { get; private set; }

    [Header("Preenchido pelo menu Pinglu > 84")]
    public GameObject modelo;
    public AnimationClip animVoo, animPlanar, animSopro;
    public Material materialParticula;
    public GameObject prefabLadrao;
    [Tooltip("Onde fica a montanha que racha (o ponto mais alto do mapa, por padrão). Dá para mover à mão.")]
    public Vector3 posicaoMontanha = new Vector3(830f, 0f, 130f);
    public float raioArena = 75f;
    [Tooltip("Se o grupo morrer, o dragão volta quando ele chega a essa distância do lugar da batalha")]
    public float distanciaParaAcordar = 105f;
    [HideInInspector] public Vector3 centroArena;

    enum Fase { Esperando, Cena, Luta, Fim }
    Fase fase = Fase.Esperando;
    ChefeDragao chefe; float proxBusca; bool menuAntes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { Instancia = null; }

    void Awake() { Instancia = this; }
    void OnEnable() { Renascimento.AoRenascer += ReiniciarLuta; }
    void OnDisable() { Renascimento.AoRenascer -= ReiniciarLuta; }

    float ChaoEm(Vector3 p) { Terrain t = Terrain.activeTerrain; return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y; }
    Transform JogadorLocal()
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) return p.transform;
        return null;
    }
    int QuantosJogadores() { int n = 0; foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) n++; return Mathf.Max(1, n); }
    static float Plano(Vector3 a, Vector3 b) { return new Vector2(a.x - b.x, a.z - b.z).magnitude; }

    // o jogador morreu e renasceu: o dragão some e volta a esperar
    void ReiniciarLuta()
    {
        if (fase != Fase.Cena && fase != Fase.Luta) return;
        StopAllCoroutines();
        if (chefe != null) Destroy(chefe.gameObject);
        chefe = null; fase = Fase.Esperando; proxBusca = Time.time + 2f;
        TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) { Vida v = p.GetComponent<Vida>(); if (v != null) v.DesfazerImortal(); }
    }

    bool cinematicaFeita; float desdeDragao = -1f;

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // estas teclas vêm antes de tudo: funcionam em qualquer etapa da história
        if (Input.GetKeyDown(KeyCode.F7)) { if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) IrDiretoFuga(); else IrDireto(); }   // F7: Ladrão no portal / Shift+F7: a fuga e o rastro negro
        if (Input.GetKeyDown(KeyCode.F8)) IrDiretoFase2();                      // F8: 2ª fase do dragão
        if (Input.GetKeyDown(KeyCode.F9)) { if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) IrDiretoMorteDoDragao(); else IntroDoJogo.TocarMorteDoDragao(() => FinalDoJogo.Tocar(true)); }   // F9: morte do dragão ilustrada em código (fraca) / Shift+F9: a versão 3D
#endif
        if (fase == Fase.Cena) return;
        if (Campanha.Atual < Etapa.Dragao) { if (cinematicaFeita || montanhaQuebrada) LimparTudo(); desdeDragao = -1f; return; }
        if (fase != Fase.Esperando || Time.time < proxBusca || SessaoDeJogo.MenuAberto || DialogoUI.Aberto) return;
        proxBusca = Time.time + 0.4f;
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) return;
        if (Campanha.Atual != Etapa.Dragao || modelo == null) return;
        Transform local = JogadorLocal(); if (local == null) return;
        if (!cinematicaFeita)
        {
            if (desdeDragao < 0f) desdeDragao = Time.time;
            if (!perseguicaoFeita) { if (Time.time - desdeDragao > 2.5f) StartCoroutine(CenaDaFuga(local)); }       // espera o pegar do cristal terminar; o Ladrão aparece e foge
            else if (Plano(local.position, PosicaoDoPortal()) < 32f) StartCoroutine(CinematicaDoVilao(local));    // seguiu o rastro até o portal: a cena do plano dele
        }
        else if (Plano(local.position, centroArena) < distanciaParaAcordar) StartCoroutine(Cena(local));
    }

    void OnDestroy() { RestaurarMundo(); }
    void OnApplicationQuit() { RestaurarMundo(); }

    // ------------------------------------------------------------ a cena de chegada
    IEnumerator Cena(Transform local)
    {
        fase = Fase.Cena; menuAntes = SessaoDeJogo.MenuAberto;
        // se o grupo morrer na luta, renasce fora da distância que chama o dragão
        Vector3 recuo = local.position - centroArena; recuo.y = 0f; if (recuo.sqrMagnitude < 1f) recuo = Vector3.right;
        Vector3 ponto0 = centroArena + recuo.normalized * (distanciaParaAcordar + 30f); ponto0.y = ChaoEm(ponto0) + 0.3f;
        Renascimento.DefinirParaLuta(ponto0, Quaternion.LookRotation(-recuo.normalized));

        Vector3 dirEntrada = -recuo.normalized;                                       // ele vem do lado oposto ao do grupo
        Vector3 origem = centroArena + dirEntrada * 320f + Vector3.up * 90f;
        GameObject g = new GameObject("Drakthar"); g.transform.position = origem;
        chefe = g.AddComponent<ChefeDragao>();
        chefe.modelo = modelo; chefe.animVoo = animVoo; chefe.animPlanar = animPlanar; chefe.animSopro = animSopro; chefe.materialParticula = materialParticula;
        chefe.Preparar(centroArena, raioArena, QuantosJogadores());

        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar();
        Proteger(true);
        try
        {
            bool pousou = false;
            StartCoroutine(Rodar(chefe.Chegada(origem), () => pousou = true));
            Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao;
            Vector3 camPos = p0; Vector3 olha = origem;
            float t = 0f;
            while (!pousou && t < 40f)
            {
                t += Time.deltaTime;
                Vector3 dp = chefe.transform.position;
                // a câmera fica atrás do grupo, olhando o céu, e acompanha o dragão chegando
                Vector3 alvoPos = local.position - dirEntrada * 7f + Vector3.up * 2.2f + Vector3.Cross(Vector3.up, dirEntrada) * 3f;
                float distD = Vector3.Distance(dp, alvoPos);
                if (distD < 70f) alvoPos += Vector3.up * Mathf.Lerp(0f, 5f, 1f - distD / 70f);
                camPos = Vector3.Lerp(camPos, alvoPos, 1f - Mathf.Exp(-1.8f * Time.deltaTime));
                olha = Vector3.Lerp(olha, dp, 1f - Mathf.Exp(-3.5f * Time.deltaTime));
                float b = Mathf.SmoothStep(0f, 1f, t / 1.6f);
                CameraDeCena.Definir(Vector3.Lerp(p0, camPos, b), Quaternion.Slerp(r0, Quaternion.LookRotation(olha - camPos), b));
                yield return null;
            }
            // plano baixo de frente: o título
            Vector3 pA = CameraDeCena.Posicao; Quaternion rA = CameraDeCena.Rotacao;
            bool titulo = false; float dur = 5.2f;
            for (float u = 0f; u < dur; u += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, u / 1.0f);
                Vector3 dpos = chefe.transform.position;
                Vector3 pos = dpos + chefe.transform.forward * Mathf.Lerp(40f, 33f, u / dur) + Vector3.Cross(Vector3.up, chefe.transform.forward) * 6f; pos.y = ChaoEm(pos) + 3.2f;
                Vector3 alvo = dpos + Vector3.up * 3f;
                CameraDeCena.Definir(Vector3.Lerp(pA, pos, b), Quaternion.Slerp(rA, Quaternion.LookRotation(alvo - pos), b));
                if (!titulo && u > 0.5f) { titulo = true; TituloDeChefe.Mostrar(ApresentacaoDoChefe.Dragao()); }
                yield return null;
            }
            // conversa
            DialogoUI.Abrir(Roteiro.DragaoAparece, null);
            while (DialogoUI.Aberto) { CameraDeCena.Olhar(CameraDeCena.Posicao, chefe.transform.position + Vector3.up * 3f); yield return null; }
        }
        finally
        {
            TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menuAntes;
            Proteger(false);
        }
        if (chefe == null) yield break;
        fase = Fase.Luta;
        chefe.Despertar();
        chefe.AoFase2 += c => StartCoroutine(CinematicaFase2(c));
        chefe.AoMorrer += c => StartCoroutine(AposODragao(c));
    }

    IEnumerator Rodar(IEnumerator rotina, System.Action fim) { yield return StartCoroutine(rotina); fim(); }

    void Proteger(bool ligar)
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            Vida v = p.GetComponent<Vida>(); if (v == null) continue;
            if (ligar) v.TornarImortal(120f); else { v.DesfazerImortal(); v.TornarImortal(1.5f); }
        }
    }

    // ------------------------------------------------------------ a morte do dragão (a câmera segue o roteiro do tempo de ChefeDragao.Queda)
    // true: a morte do dragão toca em cenas ilustradas (estilo da abertura). false: câmera 3D (Timeline/código)
    public static bool MorteIlustrada = true;

    IEnumerator AposODragao(ChefeDragao c)
    {
        Renascimento.Liberar();
        fase = Fase.Fim;
        bool menu0 = SessaoDeJogo.MenuAberto;
        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar(); Proteger(true); FxCena.GarantirTela();
        CenaTimeline tl = null; bool finalEmCena = false;
        try
        {
            if (MorteIlustrada)                                      // versão em cenas desenhadas (estilo da abertura) por cima do jogo
            {
                bool fimCena = false; float espera = 0f; bool limpo = false;
                TituloDeChefe.Barras(false);
                // a cena começa já: o dragão da luta some e o dragão da cena cai de onde ele estava
                Vector3 chaoMorte = c != null ? c.PontoDaQueda : centroArena, iniMorte = c != null ? c.transform.position : chaoMorte + Vector3.up * 60f;
                if (c != null) { c.gameObject.SetActive(false); Destroy(c.gameObject, 60f); }
                IntroDoJogo.MorteChao = chaoMorte; IntroDoJogo.MorteInicio = iniMorte;
                // quando a tela fica toda preta (fim da cena), os créditos já começam (no mesmo instante, sem mostrar o jogo de novo)
                IntroDoJogo.TocarMorteDoDragao(() => { fimCena = true; FinalDoJogo.Tocar(true); });
                while (!fimCena && espera < 120f)
                {
                    espera += Time.unscaledDeltaTime;
                    if (!limpo && espera > 30f) { limpo = true; if (ceu != null) { ceu.alvo = 0f; ceu.vel = 0.15f; } if (tempestade != null) StartCoroutine(DissiparTempestade()); }
                    yield return null;
                }
                if (ceu != null && !limpo) { ceu.alvo = 0f; ceu.vel = 0.15f; }
                if (tempestade != null && !limpo) StartCoroutine(DissiparTempestade());
                IntroDoJogo.MorteChao = null; IntroDoJogo.MorteInicio = null; finalEmCena = true;
                goto fimDaMorte;
            }
            Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao;
            Vector3 corpo = c != null ? c.PontoDaQueda : centroArena;
            Vector3 d0 = Achatar(p0 - corpo); if (d0.sqrMagnitude < 25f) d0 = Vector3.back; d0.Normalize();
            float esc = c != null ? c.Escala : 1f; bool ceuLimpo = false; float giro = 0f, tt = 0f;
            // se a cena da Timeline existe (menu Pinglu > 85), ela comanda a câmera; senão vale o roteiro de câmera em código
            tl = CenaTimeline.Criar("MorteDoDragao", corpo, Quaternion.LookRotation(-d0), 0.7f + 0.3f * esc);
            while (c != null && !c.MorteTerminou && tt < 40f)
            {
                tt += Time.deltaTime; float T = Mathf.Max(0f, c.TempoDeMorte);
                if (tl != null) { tl.Definir(c.TempoDeMorte); if (!ceuLimpo && c.TempoDeMorte > 12f) { ceuLimpo = true; if (ceu != null) { ceu.alvo = 0f; ceu.vel = 0.15f; } if (tempestade != null) StartCoroutine(DissiparTempestade()); } yield return null; continue; }
                if (c != null && c.Vida != null) { }
                Vector3 dirCam = d0; float dist, alt; Vector3 alvo;
                if (T < 4.6f) { float k = T / 4.6f; dist = Mathf.Lerp(52f, 40f, k) * (0.7f + 0.3f * esc); alt = 6f; alvo = corpo + Vector3.up * 5f * esc; }
                else if (T < 7.6f) { float k = (T - 4.6f) / 3f; dist = Mathf.Lerp(40f, 46f, k) * (0.7f + 0.3f * esc); alt = Mathf.Lerp(6f, 2f, k); alvo = Vector3.Lerp(corpo + Vector3.up * 6f * esc, corpo + Vector3.up * 70f, Mathf.SmoothStep(0f, 1f, k)); }
                else if (T < 14.6f) { float k = (T - 7.6f) / 7f; giro += Time.deltaTime * 9f; dirCam = Quaternion.Euler(0f, giro, 0f) * d0; dist = Mathf.Lerp(40f, 28f, k); alt = Mathf.Lerp(5f, 10f, k); alvo = corpo + Vector3.up * 5f; }
                else { float k = Mathf.Clamp01((T - 14.6f) / 4.4f); dirCam = Quaternion.Euler(0f, giro, 0f) * d0; dist = Mathf.Lerp(30f, 55f, k); alt = Mathf.Lerp(9f, 30f, k); alvo = Vector3.Lerp(corpo + Vector3.up * 8f, corpo + Vector3.up * 80f, Mathf.SmoothStep(0f, 1f, k)); }
                Vector3 pos = corpo + dirCam * dist; pos.y = Mathf.Max(ChaoEm(pos) + 2.5f, corpo.y + alt);
                float b = Mathf.SmoothStep(0f, 1f, tt / 1.2f);
                CameraDeCena.Definir(Vector3.Lerp(p0, pos, b), Quaternion.Slerp(r0, Quaternion.LookRotation(alvo - pos), b));
                // o céu de tempestade clareia depois que o poder negro vai embora
                if (!ceuLimpo && T > 12f) { ceuLimpo = true; if (ceu != null) { ceu.alvo = 0f; ceu.vel = 0.15f; } if (tempestade != null) StartCoroutine(DissiparTempestade()); }
                yield return null;
            }
            if (tl != null) { tl.Encerrar(); tl = null; }
            if (ceu != null && !ceuLimpo) { ceu.alvo = 0f; ceu.vel = 0.15f; }
            yield return new WaitForSeconds(0.8f);
            DialogoUI.Abrir(Roteiro.DragaoDerrotado, null);
            Vector3 pc = corpo + Vector3.up * 10f; Vector3 pf = CameraDeCena.Posicao;
            while (DialogoUI.Aberto) { CameraDeCena.Olhar(pf, pc); yield return null; }
            fimDaMorte: ;
        }
        finally { if (tl != null) tl.Encerrar(); FxCena.Branco = 0f; TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menu0; Proteger(false); }
        if (finalEmCena) Campanha.Ir(Etapa.Fim); else Campanha.Ir(Etapa.Recompensa);          // (os créditos já estão rodando; o Ir(Fim) não toca nada de novo)
    }

    // ------------------------------------------------------------ atalho F6 (só para testar)
    void IrDireto()
    {
        if (DialogoUI.Aberto || fase == Fase.Cena) return;
        if (modelo == null) { Debug.LogWarning("[Dragão] F7: rode o menu Pinglu > 84 primeiro (o modelo do dragão ainda não foi ligado)."); return; }
        Transform local = JogadorLocal(); if (local == null) return;
        StopAllCoroutines(); LimparTudo();
        TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
        GerenciadorDeOndas.HordaVencida = true;
        Campanha.CemiterioFeito = Campanha.CasteloFeito = Campanha.PantanoFeito = true;
        if (Campanha.Atual != Etapa.Dragao) Campanha.Ir(Etapa.Dragao);
        perseguicaoFeita = true; fase = Fase.Esperando; desdeDragao = Time.time;
        StartCoroutine(CinematicaDoVilao(local));                       // toca a cinemática agora, onde você estiver
    }
}
