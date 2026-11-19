using System.Collections;
using UnityEngine;

// A história do Pântano: quando o grupo chega perto do lago, o Devorador do Lago sobe da água (cena de câmera),
// aparece o título e a luta começa. Derrotado, ele afunda e o 3º cristal surge sobre a água.
// F5 (só no editor): vai direto para a beira do lago como se o resto da história já tivesse sido feito.
public class EncontroDoPantano : MonoBehaviour
{
    public static EncontroDoPantano Instancia { get; private set; }
    public GameObject prefabAngler;
    [Tooltip("Distância do centro do lago em que o chefe acorda")]
    public float distanciaParaAcordar = 78f;

    enum Fase { Esperando, Cena, Luta, Cristal, Feito }
    Fase fase = Fase.Esperando;
    ChefeAngler chefe;
    Vector3 centro; float nivel, raioAgua; bool temLago;
    float proxBusca; bool chefeAdormecido;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { Instancia = null; }

    void Awake() { Instancia = this; }
    void OnEnable() { Renascimento.AoRenascer += ReiniciarLuta; }
    void OnDisable() { Renascimento.AoRenascer -= ReiniciarLuta; }

    // o jogador morreu e renasceu (R): o chefe e os monstros chamados somem e ele volta a esperar pelo grupo
    void ReiniciarLuta()
    {
        if (fase != Fase.Cena && fase != Fase.Luta) return;
        StopAllCoroutines();
        if (chefe != null) Destroy(chefe.gameObject);
        chefe = null; fase = Fase.Esperando; proxBusca = Time.time + 2f;
        foreach (MonstroDoPantano m in FindObjectsByType<MonstroDoPantano>(FindObjectsSortMode.None)) if (m != null && m.transform.parent == null) Destroy(m.gameObject);     // os chamados pelo chefe (os da beira ficam dentro de um grupo)
        TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) { Vida v = p.GetComponent<Vida>(); if (v != null) v.DesfazerImortal(); }
    }

    void Start() { AcharLago(); }

    // o lago do colega: objeto "Lago do Pantano" com a malha "Agua" (plana, na altura da água)
    void AcharLago()
    {
        GameObject lago = GameObject.Find("Lago do Pantano");
        Transform agua = lago != null ? lago.transform.Find("Agua") : null;
        Renderer r = agua != null ? agua.GetComponent<Renderer>() : null;
        if (r == null) { temLago = false; return; }
        Bounds b = r.bounds;
        centro = new Vector3(b.center.x, b.center.y, b.center.z); nivel = b.center.y;
        raioAgua = Mathf.Min(b.extents.x, b.extents.z) * 0.9f;
        temLago = true;
    }

    Transform JogadorLocal()
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) return p.transform;
        return null;
    }

    int QuantosJogadores() { return Mathf.Max(1, FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None).Length); }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F5)) IrDiretoParaOLago();
        if (Input.GetKeyDown(KeyCode.F7)) IrParaOPantano();
#endif
        if (!temLago || fase != Fase.Esperando || Time.time < proxBusca || SessaoDeJogo.MenuAberto || DialogoUI.Aberto) return;
        proxBusca = Time.time + 0.4f;
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) return;     // por enquanto só o host cuida do chefe (rede vem depois)
        if (prefabAngler == null || chefeAdormecido) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // para testar: no editor ele acorda perto do lago em qualquer etapa da história
#else
        if (Campanha.Atual != Etapa.Cristais || Campanha.PantanoFeito) return;
#endif
        Transform local = JogadorLocal(); if (local == null) return;
        Vector3 d = local.position - centro; d.y = 0f;
        if (d.magnitude < distanciaParaAcordar) StartCoroutine(Cena(local));
    }

    // onde ele sobe: do lado do jogador, onde a água dá na cintura dele (1 a 2 m de fundo)
    Vector3 PontoDeSubida(Vector3 jogador)
    {
        Vector3 dir = jogador - centro; dir.y = 0f; if (dir.sqrMagnitude < 1f) dir = Vector3.right; dir.Normalize();
        Terrain t = Terrain.activeTerrain; float base0 = t != null ? t.transform.position.y : 0f;
        for (float r = raioAgua * 0.75f; r >= 12f; r -= 3f)
        {
            Vector3 p = centro + dir * r;
            float h = t != null ? t.SampleHeight(p) + base0 : nivel - 1.5f;
            if (h <= nivel - 1.0f && h >= nivel - 2.2f) return new Vector3(p.x, nivel, p.z);
        }
        Vector3 q = centro + dir * Mathf.Min(30f, raioAgua * 0.5f); return new Vector3(q.x, nivel, q.z);
    }

    IEnumerator Cena(Transform local)
    {
        fase = Fase.Cena;
        bool menuAntes = SessaoDeJogo.MenuAberto;
        // se morrer na luta, renasce um pouco antes do lago (fora da distância que acorda o chefe)
        Vector3 recuo = local.position - centro; recuo.y = 0f; if (recuo.sqrMagnitude < 1f) recuo = Vector3.right;
        Vector3 ponto0 = centro + recuo.normalized * (distanciaParaAcordar + 28f);
        Terrain tt = Terrain.activeTerrain; ponto0.y = (tt != null ? tt.SampleHeight(ponto0) + tt.transform.position.y : local.position.y) + 0.3f;
        Renascimento.DefinirParaLuta(ponto0, Quaternion.LookRotation(-recuo.normalized));
        Vector3 ponto = PontoDeSubida(local.position);                       // onde ele vai POUSAR (água na cintura, do lado do grupo)
        Vector3 inicio = new Vector3(centro.x, nivel, centro.z);             // ele acorda no MEIO do lago
        GameObject g = Instantiate(prefabAngler, inicio, Quaternion.identity);
        chefe = g.GetComponent<ChefeAngler>();
        if (chefe == null) { Destroy(g); fase = Fase.Feito; yield break; }
        Vector3 rumo = ponto - inicio; rumo.y = 0f; if (rumo.sqrMagnitude > 0.1f) g.transform.rotation = Quaternion.LookRotation(rumo.normalized);
        chefe.Preparar(centro, nivel, 88f, QuantosJogadores());

        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar();
        ProtegerJogadores(true);
        try
        {
            Vector3 dir = rumo.sqrMagnitude > 0.1f ? rumo.normalized : Vector3.right;
            Vector3 lado = Vector3.Cross(Vector3.up, dir);
            Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao;
            bool acabou = false;
            StartCoroutine(Rodar(chefe.Emergir(ponto), () => acabou = true));

            // 1) a câmera começa ALTA, no meio do lago, olhando os olhos vermelhos acenderem;
            //    depois segue de cima o "tubarão" nadando até a margem; vê o salto de lado e desce para o rugido
            float t = 0f;
            Vector3 posCam = p0; Vector3 olhaCam = inicio;
            while (!acabou && t < 40f)
            {
                t += Time.deltaTime;
                Vector3 bp = chefe.transform.position, alvoPos, alvoOlhar;
                float tempoSeg = 1.6f;
                switch (chefe.FaseDaIntro)
                {
                    case 0:     // de cima, quase em pé sobre o lago, olhando os olhos
                        alvoPos = new Vector3(inicio.x, nivel + 26f, inicio.z) - dir * 6f; alvoOlhar = new Vector3(bp.x, nivel + 0.3f, bp.z); tempoSeg = 1.2f; break;
                    case 1:     // seguindo o tubarão de cima e um pouco atrás
                        alvoPos = bp - dir * 15f + lado * 7f + Vector3.up * 11f; alvoOlhar = bp + dir * 5f + Vector3.up * 0.2f; tempoSeg = 2.6f; break;
                    case 2:     // o salto, visto de cima e de lado
                        alvoPos = bp + lado * 20f + Vector3.up * 13f - dir * 4f; alvoOlhar = bp + Vector3.up * 3f; tempoSeg = 1.8f; break;
                    default:    // pousou: câmera desce para o rugido
                        alvoPos = bp + chefe.transform.forward * 11f + lado * 4f + Vector3.up * 3.4f; alvoOlhar = bp + Vector3.up * 3.6f; tempoSeg = 2.2f; break;
                }
                alvoPos.y = Mathf.Max(alvoPos.y, nivel + 2.2f);
                float b = Mathf.SmoothStep(0f, 1f, t / 1.5f);
                posCam = t < 0.05f ? p0 : Vector3.Lerp(posCam, alvoPos, 1f - Mathf.Exp(-tempoSeg * Time.deltaTime));
                olhaCam = Vector3.Lerp(olhaCam, alvoOlhar, 1f - Mathf.Exp(-4f * Time.deltaTime));
                CameraDeCena.Definir(Vector3.Lerp(p0, posCam, b), Quaternion.Slerp(r0, Quaternion.LookRotation(olhaCam - posCam), b));
                yield return null;
            }
            // 2) plano baixo de frente: ele já está de pé e aparece o título
            Vector3 pA = CameraDeCena.Posicao; Quaternion rA = CameraDeCena.Rotacao;
            bool titulo = false;
            for (float u = 0f; u < 4.6f; u += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, u / 0.9f);
                Vector3 pos = chefe.transform.position + chefe.transform.forward * Mathf.Lerp(11f, 9f, u / 4.6f) + lado * 2.2f; pos.y = nivel + 2.4f;
                Vector3 olha = chefe.transform.position + Vector3.up * 3.6f;
                CameraDeCena.Definir(Vector3.Lerp(pA, pos, b), Quaternion.Slerp(rA, Quaternion.LookRotation(olha - pos), b));
                if (!titulo && u > 0.6f) { titulo = true; TituloDeChefe.Mostrar(ApresentacaoDoChefe.Angler()); }
                yield return null;
            }
        }
        finally
        {
            TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menuAntes;
            ProtegerJogadores(false);
        }
        if (chefe == null) yield break;
        fase = Fase.Luta;
        chefe.Despertar();
        chefe.AoMorrer += c => StartCoroutine(AposOChefe(c));
    }

    // durante a cena os jogadores não levam dano de ninguém (e os monstros ficam parados)
    void ProtegerJogadores(bool ligar)
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            Vida v = p.GetComponent<Vida>(); if (v == null) continue;
            if (ligar) v.TornarImortal(120f); else { v.DesfazerImortal(); v.TornarImortal(1.5f); }
        }
    }

    IEnumerator Rodar(IEnumerator rotina, System.Action fim) { yield return StartCoroutine(rotina); fim(); }

    IEnumerator AposOChefe(ChefeAngler c)
    {
        Renascimento.Liberar();
        Vector3 onde = c != null ? c.transform.position : centro;
        yield return new WaitForSeconds(5.5f);
        fase = Fase.Cristal;
        Vector3 flutua = new Vector3(onde.x, nivel + 2.2f, onde.z);
        HUDJogador.Mostrar("O terceiro cristal surge sobre a água!", 4f);
        CristalDeHistoria.Criar(null, null, flutua + Vector3.down * 3f, flutua, new Color(0.3f, 1f, 0.6f), () =>
        {
            fase = Fase.Feito;
            HUDJogador.Mostrar("Terceiro cristal recuperado!", 4f);
            Campanha.Concluir(Local.Pantano);
        });
    }

    // ------------------------------------------------------------ atalho F7: vai para a beira da água (o chefe fica dormindo)
    void IrParaOPantano()
    {
        if (!temLago) AcharLago();
        if (!temLago || DialogoUI.Aberto) return;
        Transform local = JogadorLocal(); if (local == null) return;
        StopAllCoroutines();
        if (chefe != null) Destroy(chefe.gameObject);
        chefe = null; fase = Fase.Esperando; chefeAdormecido = true;
        TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
        Vector3 dir = new Vector3(1f, 0f, -0.6f).normalized;
        Vector3 p = centro + dir * (raioAgua + 5f);
        Terrain t = Terrain.activeTerrain; p.y = (t != null ? t.SampleHeight(p) + t.transform.position.y : centro.y) + 0.4f;
        CharacterController cc = local.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        local.position = p; local.rotation = Quaternion.LookRotation(-dir);
        if (cc != null) cc.enabled = true;
        HUDJogador.Mostrar("Pântano  (o chefe dorme; F5 acorda o Devorador do Lago)", 3.5f);
    }

    // ------------------------------------------------------------ atalho de teste (F5)
    void IrDiretoParaOLago()
    {
        chefeAdormecido = false;
        if (!temLago) AcharLago();
        if (!temLago || DialogoUI.Aberto) return;
        StopAllCoroutines();
        if (chefe != null) Destroy(chefe.gameObject);
        foreach (MonstroDoPantano m in FindObjectsByType<MonstroDoPantano>(FindObjectsSortMode.None)) if (m.name.Contains("Filhote") || m.comecaEnterrado) { }
        chefe = null; fase = Fase.Esperando;
        TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
        foreach (CristalDeHistoria c in FindObjectsByType<CristalDeHistoria>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        GerenciadorDeOndas.HordaVencida = true;
        Campanha.Ir(Etapa.Cristais);
        Transform local = JogadorLocal();
        if (local != null)
        {
            Vector3 dir = new Vector3(1f, 0f, -0.6f).normalized;              // lado da vila (leste/sul do lago)
            Vector3 p = centro + dir * (distanciaParaAcordar + 22f);
            Terrain t = Terrain.activeTerrain; p.y = (t != null ? t.SampleHeight(p) + t.transform.position.y : centro.y) + 0.4f;
            CharacterController cc = local.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            local.position = p; local.rotation = Quaternion.LookRotation(-dir);
            if (cc != null) cc.enabled = true;
        }
        proxBusca = 0f;
    }
}
