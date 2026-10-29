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
    private readonly List<Transform> jogadores = new List<Transform>();
    private float proximaBusca;

    private void Awake() { Instancia = this; }

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
        foreach (Transform t in jogadores) { PlayerLocomotion p = t.GetComponent<PlayerLocomotion>(); if (p != null && p.enabled) return t; }
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
        // espera a chegada pelo portal terminar antes de dar a dica
        if (!avisouInicio && Time.time - vistoDesde > 7f) { avisouInicio = true; HUDJogador.Mostrar("Vá até o cemitério para começar a horda (ou aperte " + teclaIrAteAqui + ")", 6f); }
        if (Input.GetKeyDown(teclaIrAteAqui)) LevarJogadorAteAqui(local);

        // no multiplayer, só o host comanda as hordas
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) return;
        if (Ativo) return;
        foreach (Transform t in jogadores)
            if (t != null && Vector3.Distance(t.position, transform.position) < distanciaParaComecar)
            {
                Ativo = true;
                StartCoroutine(Ciclo());
                break;
            }
    }

    private void LevarJogadorAteAqui(Transform jogador)
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
        Aviso("Os mortos despertam...", 3f);
        yield return new WaitForSeconds(2.5f);

        while (AlguemVivo())
        {
            OndaAtual++;
            int total = inimigosNaPrimeiraOnda + (OndaAtual - 1) * aumentoPorOnda;
            if (SessaoDeJogo.Online) total += (jogadores.Count - 1) * 2; // mais jogadores, mais mortos
            Restantes = total;
            Aviso("Onda " + OndaAtual, 2.5f);
            yield return new WaitForSeconds(1.5f);

            while (Restantes > 0)
            {
                if (!AlguemVivo()) break;
                if (vivos.Count < maximoAoMesmoTempo)
                {
                    Surgir();
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
            OndaAtual = 0;
            Ativo = false;
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
            Transform perto = MaisPerto(p.position);
            if (perto == null || Vector3.Distance(p.position, perto.position) > 5f) { ponto = p; break; }
        }
        Vector3 pos = ponto.position;
        Terrain t = Terrain.activeTerrain;
        if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y;
        Transform alvo = MaisPerto(pos);
        Vector3 olhar = alvo != null ? alvo.position - pos : Vector3.zero; olhar.y = 0f;
        GameObject go = Instantiate(prefabInimigo, pos, olhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(olhar) : Quaternion.identity);

        InimigoEsqueleto ini = go.GetComponent<InimigoEsqueleto>();
        if (ini == null) return;
        ini.MultiplicarVida(1f + aumentoDeVidaPorOnda * (OndaAtual - 1));
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
