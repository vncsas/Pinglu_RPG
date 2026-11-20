using System.Collections;
using UnityEngine;

// A LUTA FINAL. Quando o último cristal é pego (em qualquer lugar) toca a CINEMÁTICA DO VILÃO (EncontroDoDragao_Cinematica.cs):
// o Ladrão revela o plano, os cristais se juntam e viram pó, a montanha racha e o dragão Drakthar sai de dentro dela e voa até o grupo.
// Depois vem o título e a luta. Vencido, o dragão cai, há uma conversa e a história segue para a recompensa do Caio.
// Se o grupo morrer, o dragão volta quando eles se aproximarem do lugar da batalha de novo (sem repetir a cinemática).
// F6 (só no editor): toca a cinemática do vilão na hora, onde você estiver.
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
        if (Input.GetKeyDown(KeyCode.F6)) IrDireto();
        if (Input.GetKeyDown(KeyCode.F8)) IrDiretoFase2();
        if (Input.GetKeyDown(KeyCode.F9)) FinalDoJogo.Tocar();                  // testa o final do jogo (epílogo + créditos)                 // vem antes de tudo: funciona em qualquer etapa da história
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
            if (Time.time - desdeDragao > 2.5f) StartCoroutine(CinematicaDoVilao(local));          // espera o pegar do cristal terminar
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

    // ------------------------------------------------------------ depois da luta
    IEnumerator AposODragao(ChefeDragao c)
    {
        Renascimento.Liberar();
        fase = Fase.Fim;
        yield return new WaitForSeconds(3.5f);
        bool menu0 = SessaoDeJogo.MenuAberto;
        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar(); Proteger(true);
        try
        {
            Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao;
            for (float t = 0f; t < 4.5f && c != null; t += Time.deltaTime)
            {
                Vector3 dpos = c.transform.position; Vector3 dir = Plano(CameraDeCena.Posicao, dpos) > 1f ? CameraDeCena.Posicao - dpos : Vector3.back; dir.y = 0f;
                Vector3 pos = dpos + dir.normalized * 34f; pos.y = ChaoEm(pos) + 4f + t * 0.6f;
                float b = Mathf.SmoothStep(0f, 1f, t / 1.5f);
                CameraDeCena.Definir(Vector3.Lerp(p0, pos, b), Quaternion.Slerp(r0, Quaternion.LookRotation(dpos + Vector3.up * 2f - pos), b));
                yield return null;
            }
            DialogoUI.Abrir(Roteiro.DragaoDerrotado, null);
            while (DialogoUI.Aberto) yield return null;
        }
        finally { TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menu0; Proteger(false); }
        Campanha.Ir(Etapa.Recompensa);
    }

    // ------------------------------------------------------------ atalho F6 (só para testar)
    void IrDireto()
    {
        if (DialogoUI.Aberto || fase == Fase.Cena) return;
        if (modelo == null) { Debug.LogWarning("[Dragão] F6: rode o menu Pinglu > 84 primeiro (o modelo do dragão ainda não foi ligado)."); return; }
        Transform local = JogadorLocal(); if (local == null) return;
        StopAllCoroutines(); LimparTudo();
        TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
        GerenciadorDeOndas.HordaVencida = true;
        Campanha.CemiterioFeito = Campanha.CasteloFeito = Campanha.PantanoFeito = true;
        if (Campanha.Atual != Etapa.Dragao) Campanha.Ir(Etapa.Dragao);
        fase = Fase.Esperando; desdeDragao = Time.time;
        StartCoroutine(CinematicaDoVilao(local));                       // toca a cinemática agora, onde você estiver
    }
}
