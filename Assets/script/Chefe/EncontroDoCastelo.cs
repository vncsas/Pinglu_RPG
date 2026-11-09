using System.Collections;
using UnityEngine;

// A história do Castelo Destruído: o Cavaleiro Antigo guarda o pátio; depois dele, o Ladrão espera no Salão do Trono.
// Derrotando o Ladrão, o 2º cristal surge; ao pegá-lo a história segue para o Pântano.
// F4 (só no editor): vai direto para o castelo como se o resto da história já tivesse sido feito.
public class EncontroDoCastelo : MonoBehaviour
{
    public static EncontroDoCastelo Instancia { get; private set; }

    public Transform estrutura;                 // o "Estrutura" do castelo (posição e giro; +Z = lado do portão)
    public GameObject prefabCavaleiro, prefabLadrao;

    private enum Fase { Esperando, Cavaleiro, Salao, Ladrao, Cristal, Feito }
    private Fase fase = Fase.Esperando;
    private InimigoCavaleiro cav, lad;
    private float proxBusca;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Zerar() { Instancia = null; }

    private void Awake() { Instancia = this; }

    // ponto do castelo (coordenadas locais x, z) com a altura do chão
    private Vector3 Ponto(float x, float z)
    {
        Vector3 p = estrutura.TransformPoint(new Vector3(x, 0f, z));
        RaycastHit h;
        if (Physics.Raycast(new Vector3(p.x, estrutura.position.y + 14f, p.z), Vector3.down, out h, 90f, ~0, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
        else { Terrain t = Terrain.activeTerrain; if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y; }
        return p;
    }

    private Transform JogadorLocal()
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) return p.transform;
        return null;
    }

    private int QuantosJogadores() { return Mathf.Max(1, FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None).Length); }

    private InimigoCavaleiro Criar(GameObject prefab, Vector3 pos, float vidaBase)
    {
        if (prefab == null) return null;
        GameObject g = Instantiate(prefab, pos, estrutura.rotation);   // olha para o lado do portão (+Z local)
        InimigoCavaleiro e = g.GetComponent<InimigoCavaleiro>();
        if (e == null) { Destroy(g); return null; }
        e.Vida.DefinirMaxima(vidaBase * (1f + 0.6f * (QuantosJogadores() - 1)));
        return e;
    }

    private void Update()
    {
        if (estrutura == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F4)) IrDiretoParaOCastelo();
#endif
        if (Time.time < proxBusca || SessaoDeJogo.MenuAberto) return;
        proxBusca = Time.time + 0.4f;
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) return;     // por enquanto só o host cuida do castelo (rede vem depois)
        Transform local = JogadorLocal(); if (local == null) return;

        if (fase == Fase.Esperando && Campanha.Atual == Etapa.Castelo)
        {
            if (cav == null && (local.position - estrutura.position).sqrMagnitude < 150f * 150f) cav = Criar(prefabCavaleiro, Ponto(0f, -2f), PrefabVida(prefabCavaleiro, 1100f));
            if (cav != null && !cav.Despertou && (local.position - cav.transform.position).sqrMagnitude < 34f * 34f) AcordarCavaleiro();
        }
        else if (fase == Fase.Salao)
        {
            if (lad != null && !DialogoUI.Aberto && (local.position - lad.transform.position).sqrMagnitude < 16f * 16f) EncontrarOLadrao();
        }
    }

    private float PrefabVida(GameObject prefab, float padrao)
    {
        if (prefab == null) return padrao;
        InimigoCavaleiro i = prefab.GetComponent<InimigoCavaleiro>(); return i != null ? i.vidaBase : padrao;
    }

    private void AcordarCavaleiro() { fase = Fase.Cavaleiro; StartCoroutine(CenaDoCavaleiro()); }

    // cena de apresentação do Cavaleiro Antigo: a aura acende, a câmera mostra o pátio, vai para o rosto dele, ele faz a pose de desafio e aparece o título
    private IEnumerator CenaDoCavaleiro()
    {
        bool menuAntes = SessaoDeJogo.MenuAberto;
        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar();
        try
        {
            Transform local = JogadorLocal();
            Vector3 k = cav.transform.position;
            Vector3 dir = local != null ? local.position - k : estrutura.forward; dir.y = 0f; if (dir.sqrMagnitude < 4f) dir = estrutura.forward; dir.Normalize();
            Vector3 lado = Vector3.Cross(Vector3.up, dir);
            Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao;
            // 1) plano aberto: a aura vermelha acende e o chão treme de leve
            float prox = 0f;
            for (float t = 0f; t < 3.2f; t += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, t / 1.3f);
                Vector3 pos = k + dir * Mathf.Lerp(26f, 21f, t / 3.2f) + lado * Mathf.Sin(t * 0.4f) * 2f + Vector3.up * 6.5f;
                Vector3 olha = k + Vector3.up * 3.2f;
                CameraDeCena.Definir(Vector3.Lerp(p0, pos, b), Quaternion.Slerp(r0, Quaternion.LookRotation(olha - pos), b));
                cav.Intensificar(t / 3.2f);
                if (Time.time >= prox) { prox = Time.time + 0.25f; TremorDeCamera.Tremer(0.03f + 0.08f * (t / 3.2f), 0.3f); }
                yield return null;
            }
            // 2) plano baixo, de frente: ele ergue a arma e o título aparece
            Vector3 pA = CameraDeCena.Posicao; Quaternion rA = CameraDeCena.Rotacao;
            cav.Desafio();
            bool titulo = false;
            for (float t = 0f; t < 4.4f; t += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                Vector3 pos = k + dir * Mathf.Lerp(10.5f, 8.2f, t / 4.4f) + lado * 1.6f + Vector3.up * 1.0f;
                Vector3 olha = k + Vector3.up * 2.9f;
                CameraDeCena.Definir(Vector3.Lerp(pA, pos, b), Quaternion.Slerp(rA, Quaternion.LookRotation(olha - pos), b));
                if (!titulo && t > 0.7f) { titulo = true; TituloDeChefe.Mostrar(ApresentacaoDoChefe.CavaleiroAntigo()); }
                yield return null;
            }
        }
        finally
        {
            TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menuAntes;
        }
        if (cav != null)
        {
            cav.Despertar();
            cav.AoMorrer += c => StartCoroutine(AposOCavaleiro());
        }
    }

    // o Cavaleiro Antigo caiu: a câmera mostra o Salão do Trono, o ladrão aparece lá no fundo e eles conversam
    private IEnumerator AposOCavaleiro()
    {
        yield return new WaitForSeconds(3.2f);
        lad = Criar(prefabLadrao, Ponto(0f, -31f), PrefabVida(prefabLadrao, 520f));
        fase = Fase.Salao;
        Campanha.Dica = "Entre no Salão do Trono e enfrente o ladrão.";
        if (lad == null) { HUDJogador.Mostrar("O Cavaleiro Antigo caiu. O Salão do Trono está logo à frente...", 4.5f); yield break; }

        bool menuAntes = SessaoDeJogo.MenuAberto;
        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar();
        try
        {
            Transform local = JogadorLocal();
            Vector3 ladPos = lad.transform.position;
            Vector3 ev = local != null ? local.position : ladPos - estrutura.forward * 25f;
            Vector3 dirL = ladPos - ev; dirL.y = 0f; if (dirL.sqrMagnitude < 4f) dirL = -estrutura.forward; dirL.Normalize();
            Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao;
            lad.Fumaca();
            // a câmera sobe, passa por cima do ombro do grupo e revela o ladrão no fundo do salão, no meio da fumaça
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, t / 1.4f), v = Mathf.SmoothStep(0f, 1f, t / 3.2f);
                Vector3 pos = ev - dirL * Mathf.Lerp(8f, 4f, t / 5f) + Vector3.up * Mathf.Lerp(4f, 2.4f, t / 5f);
                Vector3 olha = Vector3.Lerp(ev + Vector3.up * 1.2f, ladPos + Vector3.up * 1.9f, v);
                CameraDeCena.Definir(Vector3.Lerp(p0, pos, b), Quaternion.Slerp(r0, Quaternion.LookRotation(olha - pos), b));
                lad.Intensificar(t / 5f);
                if (t > 2.2f && t < 2.35f) lad.Fumaca();
                yield return null;
            }
            bool fim = false;
            DialogoUI.Abrir(Roteiro.SalaoSeAbre, () => fim = true);
            while (!fim) { CameraDeCena.Definir(CameraDeCena.Posicao, Quaternion.Slerp(CameraDeCena.Rotacao, Quaternion.LookRotation(ladPos + Vector3.up * 1.9f - CameraDeCena.Posicao), 2f * Time.deltaTime)); yield return null; }
        }
        finally
        {
            TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menuAntes;
        }
        HUDJogador.Mostrar("Entre no Salão do Trono.", 3.5f);
    }

    private void EncontrarOLadrao()
    {
        fase = Fase.Ladrao;
        DialogoUI.Abrir(Roteiro.EncontroComOLadrao, () =>
        {
            if (lad == null) return;
            Campanha.Dica = null;
            TituloDeChefe.Mostrar(ApresentacaoDoChefe.Ladrao());
            lad.Despertar();
            lad.AoMorrer += c => StartCoroutine(AposOLadrao());
        });
    }

    private IEnumerator AposOLadrao()
    {
        yield return new WaitForSeconds(2.8f);
        fase = Fase.Cristal;
        DialogoUI.Abrir(Roteiro.LadraoDerrotado, () =>
        {
            Vector3 flutua = Ponto(0f, -33f) + Vector3.up * 1.9f;
            HUDJogador.Mostrar("O segundo cristal surge!", 4f);
            CristalDeHistoria.Criar(null, null, flutua + Vector3.down * 2.6f, flutua, new Color(0.3f, 0.62f, 1f), () =>
            {
                fase = Fase.Feito;
                HUDJogador.Mostrar("Segundo cristal recuperado!", 4f);
                Campanha.Ir(Etapa.Panteano);
            });
        });
    }

    // ------------------------------------------------------------ atalho de teste (F4)
    private void IrDiretoParaOCastelo()
    {
        if (DialogoUI.Aberto) return;
        StopAllCoroutines();
        if (cav != null) Destroy(cav.gameObject);
        if (lad != null) Destroy(lad.gameObject);
        cav = lad = null; fase = Fase.Esperando;
        TituloDeChefe.Barras(false); CameraDeCena.Soltar();
        foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsSortMode.None)) if (go.name == "Espada cravada") Destroy(go);
        foreach (CristalDeHistoria c in FindObjectsByType<CristalDeHistoria>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        GerenciadorDeOndas.HordaVencida = true;                 // o cemitério conta como feito
        Campanha.Ir(Etapa.Castelo);                              // e a conversa com o Caio também
        Transform local = JogadorLocal();
        if (local != null)
        {
            Vector3 destino = Ponto(0f, 46f) + Vector3.up * 0.3f;
            CharacterController cc = local.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            local.position = destino; local.rotation = Quaternion.LookRotation(-estrutura.forward);
            if (cc != null) cc.enabled = true;
        }
        cav = Criar(prefabCavaleiro, Ponto(0f, -2f), PrefabVida(prefabCavaleiro, 1100f));
        proxBusca = 0f;
    }
}
