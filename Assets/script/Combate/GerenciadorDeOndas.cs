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
        foreach (Transform t in jogadores)
        {
            if (t == null) continue; // jogador destruído desde a última busca
            PlayerLocomotion p = t.GetComponent<PlayerLocomotion>();
            if (p != null && p.enabled) return t;
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
            OndaAtual = 0;
            Ativo = false;
        }
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
