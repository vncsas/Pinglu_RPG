using System.Collections.Generic;
using UnityEngine;

// A floresta em volta do cemitério é infestada de esqueletos. Quanto mais o
// jogador se aproxima do cemitério, mais frequentes e maiores são os grupos
// que saem do meio das árvores (de preferência à frente dele, bloqueando o
// caminho). Esqueletos que ficam para trás somem. No multiplayer só o host
// cria os monstros.
public class EmboscadaDaFloresta : MonoBehaviour
{
    [Header("Monstros (o primeiro é o mais comum)")]
    [SerializeField] private GameObject[] prefabs;
    [Tooltip("Chance (0 a 1) de, em vez do primeiro, sair um dos outros tipos")]
    [SerializeField, Range(0f, 1f)] private float chanceDeVariacao = 0.2f;

    [Header("Onde acontece")]
    [Tooltip("Dentro deste raio (o próprio cemitério) não há emboscadas")]
    [SerializeField] private float raioInterno = 40f;
    [Tooltip("Fora deste raio a floresta fica em paz")]
    [SerializeField] private float raioExterno = 170f;
    [Tooltip("Se maior que 0, a floresta é mais curta nas laterais e atrás: este raio vale para os lados e o 'Raio Externo' para a frente")]
    [SerializeField] private float raioLados = 0f;
    [SerializeField] private Vector3 direcaoDaFrente = Vector3.zero;
    [Tooltip("Metade da largura da faixa de floresta na frente do cemitério")]
    [SerializeField] private float larguraDoCaminho = 120f;

    [Header("Quanto")]
    [SerializeField] private int maximoVivos = 10;
    [SerializeField] private int maximoExtraPorJogador = 4;
    [SerializeField] private float intervaloPerto = 7f;
    [SerializeField] private float intervaloLonge = 18f;
    [SerializeField] private int grupoMinimo = 2;
    [SerializeField] private int grupoMaximo = 4;
    [SerializeField] private float distanciaMinimaDoSurgimento = 18f;
    [SerializeField] private float distanciaMaximaDoSurgimento = 30f;
    [Tooltip("Esqueletos mais longe que isto de todos os jogadores somem")]
    [SerializeField] private float distanciaParaSumir = 95f;
    [SerializeField] private float vidaExtraPerto = 0.5f;

    private readonly List<InimigoEsqueleto> vivos = new List<InimigoEsqueleto>();
    private readonly List<Transform> jogadores = new List<Transform>();
    private float proximaBusca;
    private float proximaEmboscada;
    private bool avisou;

    private void Update()
    {
        if (SessaoDeJogo.MenuAberto) return;
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) return;
        if (Time.time < proximaBusca) return;
        proximaBusca = Time.time + 0.5f;

        jogadores.Clear();
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            Vida v = p.GetComponent<Vida>();
            if (v != null && v.Morto) continue;
            jogadores.Add(p.transform);
        }
        if (jogadores.Count == 0) return;

        Sumir();

        // escolhe o jogador que está mais dentro da floresta (mais perto do cemitério, sem estar nele)
        Transform alvo = null; float melhorIntensidade = 0f;
        foreach (Transform t in jogadores)
        {
            float intensidade;
            if (!DentroDaFloresta(t.position, out intensidade)) continue;
            if (alvo == null || intensidade > melhorIntensidade) { alvo = t; melhorIntensidade = intensidade; }
        }
        if (alvo == null) { proximaEmboscada = Mathf.Max(proximaEmboscada, Time.time + 2f); return; }

        if (!avisou)
        {
            avisou = true;
            Aviso("Algo se move entre as árvores...", 3.5f);
            proximaEmboscada = Time.time + 3f;
        }
        int limite = maximoVivos + Mathf.Max(0, jogadores.Count - 1) * maximoExtraPorJogador;
        if (Time.time < proximaEmboscada || vivos.Count >= limite) return;

        proximaEmboscada = Time.time + Mathf.Lerp(intervaloLonge, intervaloPerto, melhorIntensidade) * Random.Range(0.8f, 1.2f);
        int quantos = Mathf.RoundToInt(Mathf.Lerp(grupoMinimo, grupoMaximo, melhorIntensidade));
        quantos = Mathf.Min(quantos + (jogadores.Count - 1), limite - vivos.Count);
        CriarGrupo(alvo, quantos, melhorIntensidade);
    }

    // a floresta é um círculo em volta do cemitério (raioLados) mais uma faixa larga e longa na frente (raioExterno x larguraDoCaminho)
    private bool DentroDaFloresta(Vector3 p, out float intensidade)
    {
        intensidade = 0f;
        Vector3 d = p - transform.position; d.y = 0f;
        float dist = d.magnitude;
        if (dist <= raioInterno) return false;
        float alcance = raioLados > 0f ? raioLados : raioExterno;
        bool dentro = dist < alcance;
        if (raioLados > 0f && direcaoDaFrente.sqrMagnitude > 0.01f)
        {
            Vector3 f = direcaoDaFrente; f.y = 0f; f.Normalize();
            float ao = Vector3.Dot(d, f);
            float lat = Mathf.Abs(d.x * f.z - d.z * f.x);
            if (ao > 0f && ao < raioExterno && lat < larguraDoCaminho) { dentro = true; alcance = Mathf.Max(alcance, raioExterno); }
        }
        if (!dentro) return false;
        intensidade = 1f - Mathf.InverseLerp(raioInterno, alcance, dist);
        return true;
    }

    private float Distancia(Vector3 p)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        return d.magnitude;
    }

    private void CriarGrupo(Transform alvo, int quantos, float intensidade)
    {
        if (prefabs == null || prefabs.Length == 0 || prefabs[0] == null) return;

        // 60% das vezes sai à frente do jogador, no rumo do cemitério (impedindo a passagem)
        Vector3 paraCemiterio = transform.position - alvo.position; paraCemiterio.y = 0f;
        Vector3 rumo = paraCemiterio.sqrMagnitude > 1f && Random.value < 0.6f
            ? Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * paraCemiterio.normalized
            : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        Vector3 centro = alvo.position + rumo * Random.Range(distanciaMinimaDoSurgimento, distanciaMaximaDoSurgimento);

        for (int i = 0; i < quantos; i++)
        {
            Vector3 pos = centro + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
            if (Distancia(pos) < raioInterno - 6f) continue; // não nasce dentro do cemitério
            Terrain t = Terrain.activeTerrain;
            if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y;

            GameObject prefab = prefabs[0];
            if (prefabs.Length > 1 && Random.value < chanceDeVariacao) prefab = prefabs[Random.Range(1, prefabs.Length)];
            if (prefab == null) continue;

            Vector3 olhar = alvo.position - pos; olhar.y = 0f;
            GameObject go = Instantiate(prefab, pos, olhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(olhar) : Quaternion.identity);
            InimigoEsqueleto ini = go.GetComponent<InimigoEsqueleto>();
            if (ini == null) { Destroy(go); continue; }
            ini.MultiplicarVida(1f + vidaExtraPerto * intensidade);
            ini.DefinirArea(pt => { float inten; return DentroDaFloresta(pt, out inten); }, transform.position); // não saem da floresta
            ini.AoMorrer += e => vivos.Remove(e);
            vivos.Add(ini);
            ini.Surgir();
            if (SessaoDeJogo.Online && SessaoDeJogo.SpawnarNaRede != null) SessaoDeJogo.SpawnarNaRede(go);
        }
    }

    // apaga quem ficou para trás
    private void Sumir()
    {
        for (int i = vivos.Count - 1; i >= 0; i--)
        {
            InimigoEsqueleto e = vivos[i];
            if (e == null) { vivos.RemoveAt(i); continue; }
            bool perto = false;
            foreach (Transform t in jogadores)
                if (Vector3.Distance(t.position, e.transform.position) < distanciaParaSumir) { perto = true; break; }
            if (perto) continue;
            vivos.RemoveAt(i);
            Destroy(e.gameObject);
        }
    }

    private static void Aviso(string texto, float tempo)
    {
        if (SessaoDeJogo.Online && SessaoDeJogo.AvisarTodos != null) SessaoDeJogo.AvisarTodos(texto, tempo);
        else HUDJogador.Mostrar(texto, tempo);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, raioInterno);
        Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(transform.position, raioExterno);
    }
}
