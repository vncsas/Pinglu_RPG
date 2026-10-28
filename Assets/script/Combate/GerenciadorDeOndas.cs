using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ondas de monstros. Começa quando o jogador chega perto (ou aperta F2 para
// ir direto até aqui). Cada onda tem mais inimigos e eles ficam mais fortes.
public class GerenciadorDeOndas : MonoBehaviour
{
    public static GerenciadorDeOndas Instancia { get; private set; }

    [Header("Monstro e pontos de onde ele sai")]
    [SerializeField] private GameObject prefabInimigo;
    [SerializeField] private Transform[] pontosDeSurgimento;

    [Header("Quando começa")]
    [SerializeField] private float distanciaParaComecar = 35f;
    [SerializeField] private KeyCode teclaIrAteAqui = KeyCode.F2;

    [Header("Ondas")]
    [SerializeField] private int inimigosNaPrimeiraOnda = 3;
    [SerializeField] private int aumentoPorOnda = 2;
    [SerializeField] private int maximoAoMesmoTempo = 8;
    [SerializeField] private float intervaloEntreSurgimentos = 1.2f;
    [SerializeField] private float descansoEntreOndas = 6f;
    [Tooltip("Quanto a vida dos monstros aumenta a cada onda (0.15 = +15%)")]
    [SerializeField] private float aumentoDeVidaPorOnda = 0.15f;
    [Tooltip("Quanto da vida o jogador recupera ao vencer uma onda (0.3 = 30%)")]
    [SerializeField] private float curaAoVencerOnda = 0.3f;

    public int OndaAtual { get; private set; }
    public int Vivos => vivos.Count;
    public int Restantes { get; private set; }
    public bool Ativo { get; private set; }

    private readonly List<InimigoEsqueleto> vivos = new List<InimigoEsqueleto>();
    private Transform jogador;
    private Vida vidaJogador;

    private void Awake() { Instancia = this; }

    private void Start()
    {
        PlayerLocomotion p = FindFirstObjectByType<PlayerLocomotion>();
        if (p != null) { jogador = p.transform; vidaJogador = p.GetComponent<Vida>(); }
        HUDJogador.Mostrar("Vá até o cemitério para começar a horda (ou aperte " + teclaIrAteAqui + ")", 6f);
    }

    private void Update()
    {
        if (jogador == null) return;
        if (Input.GetKeyDown(teclaIrAteAqui) && !Ativo) LevarJogadorAteAqui();
        if (!Ativo && Vector3.Distance(jogador.position, transform.position) < distanciaParaComecar)
        {
            Ativo = true;
            StartCoroutine(Ciclo());
        }
    }

    private void LevarJogadorAteAqui()
    {
        CharacterController cc = jogador.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        Vector3 destino = transform.position + transform.forward * 16f;
        Terrain t = Terrain.activeTerrain;
        if (t != null) destino.y = t.SampleHeight(destino) + t.transform.position.y + 0.2f;
        jogador.position = destino;
        jogador.rotation = Quaternion.LookRotation(-transform.forward);
        if (cc != null) cc.enabled = true;
    }

    private IEnumerator Ciclo()
    {
        HUDJogador.Mostrar("Os mortos despertam...", 3f);
        yield return new WaitForSeconds(2.5f);

        while (vidaJogador == null || !vidaJogador.Morto)
        {
            OndaAtual++;
            int total = inimigosNaPrimeiraOnda + (OndaAtual - 1) * aumentoPorOnda;
            Restantes = total;
            HUDJogador.Mostrar("Onda " + OndaAtual, 2.5f);
            yield return new WaitForSeconds(1.5f);

            while (Restantes > 0)
            {
                if (vidaJogador != null && vidaJogador.Morto) yield break;
                if (vivos.Count < maximoAoMesmoTempo)
                {
                    Surgir();
                    Restantes--;
                    yield return new WaitForSeconds(intervaloEntreSurgimentos);
                }
                else yield return null;
            }
            while (vivos.Count > 0)
            {
                if (vidaJogador != null && vidaJogador.Morto) yield break;
                yield return null;
            }

            HUDJogador.Mostrar("Onda " + OndaAtual + " vencida!", 3f);
            if (vidaJogador != null) vidaJogador.Curar(vidaJogador.Maxima * curaAoVencerOnda);
            yield return new WaitForSeconds(descansoEntreOndas);
        }
    }

    private void Surgir()
    {
        if (prefabInimigo == null || pontosDeSurgimento == null || pontosDeSurgimento.Length == 0) return;

        // prefere pontos que não estão colados no jogador
        Transform ponto = pontosDeSurgimento[Random.Range(0, pontosDeSurgimento.Length)];
        for (int i = 0; i < 6; i++)
        {
            Transform p = pontosDeSurgimento[Random.Range(0, pontosDeSurgimento.Length)];
            if (Vector3.Distance(p.position, jogador.position) > 5f) { ponto = p; break; }
        }
        Vector3 pos = ponto.position;
        Terrain t = Terrain.activeTerrain;
        if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y;
        Vector3 olhar = jogador.position - pos; olhar.y = 0f;
        GameObject go = Instantiate(prefabInimigo, pos, olhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(olhar) : Quaternion.identity);

        InimigoEsqueleto ini = go.GetComponent<InimigoEsqueleto>();
        if (ini == null) return;
        ini.MultiplicarVida(1f + aumentoDeVidaPorOnda * (OndaAtual - 1));
        ini.AoMorrer += e => vivos.Remove(e);
        vivos.Add(ini);
        ini.Surgir();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanciaParaComecar);
        if (pontosDeSurgimento == null) return;
        foreach (Transform p in pontosDeSurgimento) if (p != null) Gizmos.DrawSphere(p.position, 0.3f);
    }
}
