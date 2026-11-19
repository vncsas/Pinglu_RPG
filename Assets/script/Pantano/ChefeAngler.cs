using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// O Devorador do Lago (o Angler do colega): chefe do Pântano. Fica escondido no fundo do lago e SOBE de lá (Emergir).
// Cada animação dele vira um ataque diferente:
//   Mordida  - bocada rápida à frente, dano alto
//   Garras   - duas patadas seguidas num leque à frente
//   Cauda    - giro com a cauda em volta dele (um anel vermelho avisa; dá para rolar por cima)
//   Salto    - salta para o lugar onde o jogador estava (círculo vermelho avisa) e faz explosão de água
//   Rugido   - treme tudo, empurra quem está perto e chama monstros da lama (na 2ª fase, com menos de 50% de vida)
//   Dano / Morte - reação quando apanha muito e a morte, afundando de volta no lago
public class ChefeAngler : MonoBehaviour, IAfetavelPorHabilidade
{
    [Header("Animações do FBX")]
    public AnimationClip parado, andar, correr, mordida, garras, cauda, salto, rugido, dano, morte;

    [Header("Efeitos (da pasta Ataques)")]
    public GameObject efeitoRespingo;      // água/fumaça subindo
    public GameObject efeitoPoeira;        // pouso do salto
    public GameObject efeitoRugido;
    public GameObject prefabMonstro;       // monstros que o rugido chama

    [Header("Números")]
    public float vidaBase = 2400f;
    public float velAndar = 2.3f, velCorrer = 5.4f;
    [Tooltip("Tamanho do chefe (1 = o do colega, ~2,5 m)")]
    public float escala = 1.6f;
    public Material materialClarao;        // brilho dos olhos (Resources/Agua/Gota)
    public int FaseDaIntro { get; private set; }   // 0 olhos no meio do lago, 1 nadando, 2 saltando, 3 pousou e ruge (a câmera acompanha)

    public Vida Vida { get; private set; }
    public bool Surgiu { get; private set; }
    public bool Lutando { get { return Surgiu && !morto; } }
    public bool Fase2 { get; private set; }
    public event System.Action<ChefeAngler> AoMorrer;

    MisturadorDeClipes mis;
    Vector3 centroLago; float nivelAgua, raioArena = 85f;
    Transform alvo;
    bool ocupado, morto, intro;
    float proxOnda, proxBusca, proxAtaque, proxSalto, proxRugido, lentoAte, fatorLento = 1f, chaoY;
    float danoRecente, danoReset; int ultimoAtaque = -1;
    readonly List<MonstroDoPantano> filhotes = new List<MonstroDoPantano>();
    static Material matLinha;

    float E { get { return escala; } }
    OlhosBrilhantes olhos;
    float MulVel { get { return (Fase2 ? 1.25f : 1f) * (Time.time < lentoAte ? fatorLento : 1f); } }
    float MulRecarga { get { return Fase2 ? 0.7f : 1f; } }

    // ---- IAfetavelPorHabilidade: chefe não é provocado nem empurrado e fica menos lento
    public void Provocar(Transform quem, float s) { }
    public void Empurrar(Vector3 v) { }
    public void Lentidao(float f, float s) { fatorLento = 1f - (1f - Mathf.Clamp01(f)) * 0.5f; lentoAte = Time.time + s * 0.5f; }

    // ---------------------------------------------------------------- preparo
    public void Preparar(Vector3 centro, float nivel, float raio, int jogadores)
    {
        centroLago = centro; nivelAgua = nivel; raioArena = raio;
        Vida = GetComponent<Vida>(); if (Vida == null) Vida = gameObject.AddComponent<Vida>();
        Vida.DefinirMaxima(vidaBase * (1f + 0.6f * (Mathf.Max(1, jogadores) - 1)));
        Vida.AoMorrer += Morrer;
        Vida.AoLevarDano += AoLevarDano;
        mis = GetComponent<MisturadorDeClipes>(); if (mis == null) mis = gameObject.AddComponent<MisturadorDeClipes>();
        mis.Montar(new Dictionary<string, AnimationClip> { { "Idle", parado }, { "Walk", andar }, { "Run", correr }, { "Mordida", mordida }, { "Garras", garras }, { "Cauda", cauda }, { "Salto", salto }, { "Rugido", rugido }, { "Dano", dano }, { "Morte", morte } });
        mis.Tocar("Idle", 0.01f);
        BarraDeChefe.Criar(gameObject, Vida, "Devorador do Lago", ApresentacaoDoChefe.Angler().fonte, () => Lutando).Cores(new Color(0.25f, 0.72f, 0.5f), new Color(0.05f, 0.28f, 0.2f));
        transform.localScale = Vector3.one * escala;
        MontarOlhos();
        // escondido no fundo: o topo do modelo fica abaixo da superfície
        transform.position = new Vector3(transform.position.x, nivelAgua - 2.6f * escala - 0.5f, transform.position.z);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    // ---------------------------------------------------------------- a subida do lago (a cena de câmera chama e espera)
    // 1) debaixo d'água os olhos vermelhos acendem e a água ferve; 2) ele sobe devagar, treme, se agacha;
    // 3) SALTA para fora numa coluna d'água e 4) mergulha de novo mais perto, levantando uma onda enorme; 5) ruge.
    void MontarOlhos()
    {
        Transform cabeca = null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == "Head") { cabeca = t; break; }
        if (cabeca == null) return;
        olhos = gameObject.AddComponent<OlhosBrilhantes>();
        olhos.materialClarao = materialClarao != null ? materialClarao : Resources.Load<Material>("Agua/Gota");
        olhos.alcanceDaLuz = 6f * escala; olhos.intensidadeDaLuz = 5f; olhos.tamanho = 0.22f;
        // o modelo nasce com a cabeça perto de (0, 2,05, 1,0) e o focinho para +Z: olhos nos lados da cabeça
        olhos.Montar(cabeca, transform, new Vector3(-0.3f, 2.1f, 1.02f), new Vector3(0.3f, 2.1f, 1.02f));
        olhos.AcenderJa(0f);
    }

    public IEnumerator Emergir(Vector3 pouso)
    {
        intro = true; FaseDaIntro = 0;
        pouso.y = Mathf.Min(AlturaDoChao(pouso), nivelAgua - 0.7f);          // pousa de pé no fundo raso, água na cintura
        Vector3 inicio = new Vector3(centroLago.x, nivelAgua, centroLago.z);
        Vector3 rumo = pouso - inicio; rumo.y = 0f; float total = rumo.magnitude; Vector3 dir = total > 0.1f ? rumo / total : transform.forward;
        float distSalto = Mathf.Clamp(total * 0.45f, 7f, 10f * escala * 0.7f);            // distância da margem onde ele salta
        Vector3 decolagem = pouso - dir * distSalto; decolagem.y = nivelAgua;
        float yEscondido = transform.position.y;
        float yNado = nivelAgua + 0.15f - 2.1f * escala;                                  // só os olhos e a cabeça ficam para fora
        transform.rotation = Quaternion.LookRotation(dir);
        mis.Tocar("Idle", 0.05f);

        // 1) no meio do lago: a água ferve e os olhos vermelhos acendem no fundo
        olhos?.Acender(1f);
        float dur0 = 3.4f, prox = 0f;
        for (float t = 0f; t < dur0; t += Time.deltaTime)
        {
            float k = t / dur0;
            float sobe = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.45f) / 0.55f));
            transform.position = new Vector3(inicio.x, Mathf.Lerp(yEscondido, yNado, sobe), inicio.z);
            if (Time.time >= prox)
            {
                prox = Time.time + Mathf.Lerp(0.5f, 0.18f, k);
                AmbienteDaAgua.Ondular(inicio + new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-2.5f, 2.5f)), 0.5f + 1.2f * k);
                if (k > 0.5f) AmbienteDaAgua.Respingo(inicio + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)), 0.5f + k);
                TremorDeCamera.Tremer(0.02f + 0.06f * k, 0.5f);
                Anel(inicio + Vector3.up * 0.05f, 1f, 6f + 6f * k, 2f, new Color(0.7f, 0.9f, 0.8f), 0.25f);
            }
            yield return null;
        }

        // 2) estilo TUBARÃO: desliza pela água só com os olhos e a cabeça de fora, deixando um rastro em V, até a distância do salto
        FaseDaIntro = 1;
        mis.Tocar("Run", 0.3f, 1.3f);
        Vector3 lado = Vector3.Cross(Vector3.up, dir);
        float nadar = Mathf.Max(0.1f, Vector3.Distance(new Vector3(inicio.x, 0f, inicio.z), new Vector3(decolagem.x, 0f, decolagem.z)));
        float vel0 = 4f, vel1 = 12f, andado = 0f; prox = 0f;
        while (andado < nadar)
        {
            float k = Mathf.Clamp01(andado / nadar);
            float v = Mathf.Lerp(vel0, vel1, k * k);
            andado += v * Time.deltaTime;
            Vector3 p = inicio + dir * Mathf.Min(andado, nadar);
            p.y = yNado + Mathf.Sin(Time.time * 7f) * 0.06f;
            transform.position = p;
            if (Time.time >= prox)
            {
                prox = Time.time + 0.09f;
                Vector3 s = new Vector3(p.x, nivelAgua, p.z);
                AmbienteDaAgua.Ondular(s, 1.1f);
                AmbienteDaAgua.Ondular(s - dir * 2.2f * escala + lado * 1.4f, 0.8f);          // o V do rastro
                AmbienteDaAgua.Ondular(s - dir * 2.2f * escala - lado * 1.4f, 0.8f);
                AmbienteDaAgua.Respingo(s + dir * 1.2f, 0.45f);
                TremorDeCamera.Tremer(0.03f + 0.05f * k, 0.3f);
            }
            yield return null;
        }

        // 3) toma impulso: afunda um pouco e começa a animação de salto
        mis.Tocar("Salto", 0.15f, 1f, true);
        Vector3 base1 = transform.position;
        for (float t = 0f; t < 0.55f; t += Time.deltaTime) { transform.position = base1 + Vector3.down * 0.9f * Mathf.Sin(t / 0.55f * Mathf.PI * 0.5f); AmbienteDaAgua.Ondular(new Vector3(base1.x, nivelAgua, base1.z), 1.5f); yield return null; }

        // 4) O SALTO: sai da água numa coluna d'água, faz um arco e mergulha na margem
        FaseDaIntro = 2;
        Vector3 ini = transform.position;
        float dur2 = 1.6f; bool saiu = false;
        float topo = nivelAgua + 4.6f * escala;
        for (float t = 0f; t < dur2; t += Time.deltaTime)
        {
            float k = t / dur2;
            Vector3 p = Vector3.Lerp(new Vector3(ini.x, 0f, ini.z), new Vector3(pouso.x, 0f, pouso.z), k);
            float y0 = ini.y, y1 = pouso.y;
            p.y = Mathf.Lerp(y0, y1, k) + (topo - Mathf.Lerp(y0, y1, 0.5f)) * 4f * k * (1f - k);
            transform.position = p;
            if (!saiu && p.y + 2.0f * escala > nivelAgua) { saiu = true; ColunaDagua(new Vector3(ini.x, nivelAgua, ini.z), 3.4f); TremorDeCamera.Tremer(0.4f, 0.8f); }
            yield return null;
        }
        transform.position = pouso; chaoY = pouso.y;

        // 5) o mergulho: onda enorme saindo do ponto de pouso
        Vector3 impacto = new Vector3(pouso.x, nivelAgua, pouso.z);
        ColunaDagua(impacto, 4.5f);
        for (int i = 0; i < 4; i++) { AmbienteDaAgua.Ondular(impacto, 3f - i * 0.4f); Anel(impacto + Vector3.up * 0.06f, 1.5f, 34f - i * 5f, 2.6f + i * 0.3f, new Color(0.75f, 0.95f, 0.85f), 0.8f - i * 0.12f); yield return new WaitForSeconds(0.12f); }
        TremorDeCamera.Tremer(0.6f, 1.4f);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = true;

        // 6) ruge com os olhos em chamas
        FaseDaIntro = 3;
        mis.Tocar("Rugido", 0.15f, 1f, true);
        if (olhos != null) olhos.Pulsar = 1f;
        yield return new WaitForSeconds(0.35f * mis.Duracao("Rugido"));
        if (efeitoRugido != null) Destroy(Instantiate(efeitoRugido, transform.position + Vector3.up * 1.4f * escala, Quaternion.identity), 3f);
        TremorDeCamera.Tremer(0.5f, 1.4f);
        Anel(new Vector3(pouso.x, nivelAgua + 0.06f, pouso.z), 2f, 28f, 1.6f, new Color(1f, 0.7f, 0.6f), 0.6f);
        yield return new WaitForSeconds(Mathf.Max(0.5f, 0.65f * mis.Duracao("Rugido")));
        if (olhos != null) olhos.Pulsar = 0.3f;
        mis.Tocar("Idle", 0.3f);
    }

    // coluna d'água: respingos grandes, gotas, onda
    void ColunaDagua(Vector3 pos, float forca)
    {
        Respingo(pos, forca);
        for (int i = 0; i < 3; i++) AmbienteDaAgua.Respingo(pos + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)) * escala, forca);
        AmbienteDaAgua.Ondular(pos, 3f);
        if (efeitoRespingo != null) { GameObject g = Instantiate(efeitoRespingo, pos, Quaternion.identity); g.transform.localScale *= forca * 0.8f; Destroy(g, 5f); }
    }

    // a luta começa (depois da cena)
    public void Despertar()
    {
        intro = false; Surgiu = true; if (olhos != null) olhos.Pulsar = 0.2f; proxAtaque = Time.time + 1.2f; proxSalto = Time.time + 6f; proxRugido = Time.time + 999f;
    }

    // ---------------------------------------------------------------- inteligência
    void Update()
    {
        if (!Lutando || intro) return;
        if (Time.time >= proxBusca) { proxBusca = Time.time + 0.4f; AcharAlvo(); }
        if (Time.time > danoReset) { danoReset = Time.time + 6f; danoRecente = 0f; }
        if (ocupado) return;
        Grudar();

        if (!Fase2 && Vida.Fracao <= 0.5f) { StartCoroutine(Rugir(true)); return; }
        if (alvo == null) { mis.Tocar("Idle", 0.3f); return; }

        Vector3 para = alvo.position - transform.position; para.y = 0f;
        float dist = para.magnitude;
        Virar(para, 6f);

        if (Fase2 && Time.time >= proxRugido && Vivos() < 2) { StartCoroutine(Rugir(false)); return; }
        if (dist <= 3.7f * E && Time.time >= proxAtaque) { EscolherAtaque(); return; }
        if (dist > 9f * E && Time.time >= proxSalto) { StartCoroutine(Saltar()); return; }
        if (dist > 3.2f * E)
        {
            bool corre = dist > 8f * E;
            mis.Tocar(corre ? "Run" : "Walk", 0.25f);
            Mover(transform.forward, (corre ? velCorrer : velAndar) * MulVel * (0.6f + 0.4f * E));
            if (transform.position.y < nivelAgua + 0.5f && Time.time >= proxOnda) { proxOnda = Time.time + 0.35f; AmbienteDaAgua.Ondular(new Vector3(transform.position.x, nivelAgua, transform.position.z), corre ? 1.6f : 1f); }
        }
        else mis.Tocar("Idle", 0.25f);
    }

    int Vivos() { filhotes.RemoveAll(m => m == null || !m.Vivo); return filhotes.Count; }

    void AcharAlvo()
    {
        float melhor = float.MaxValue; Transform t = null;
        foreach (PlayerLocomotion p in Jogadores())
        {
            Vector3 d = p.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude < melhor) { melhor = d.sqrMagnitude; t = p.transform; }
        }
        alvo = t;
    }

    List<PlayerLocomotion> Jogadores()
    {
        List<PlayerLocomotion> r = new List<PlayerLocomotion>();
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            Vida v = p.GetComponent<Vida>(); if (v != null && v.Morto) continue;
            r.Add(p);
        }
        return r;
    }

    void EscolherAtaque()
    {
        int perto = 0;
        foreach (PlayerLocomotion p in Jogadores()) if ((p.transform.position - transform.position).sqrMagnitude < 6.5f * E * 6.5f * E) perto++;
        int n;
        if (perto >= 2 && ultimoAtaque != 2 && Random.value < 0.6f) n = 2;
        else { n = Random.Range(0, 3); if (n == ultimoAtaque) n = (n + 1) % 3; }
        ultimoAtaque = n;
        StartCoroutine(n == 0 ? Golpe("Mordida", new[] { 0.4f }, 1.1f) : n == 1 ? Golpe("Garras", new[] { 0.3f, 0.62f }, 1.1f) : Golpe("Cauda", new[] { 0.5f }, 1f));
    }

    void AoLevarDano(float d, Vector3 de)
    {
        danoRecente += d;
        if (!ocupado && !intro && !morto && danoRecente > Vida.Maxima * 0.1f) { danoRecente = 0f; StartCoroutine(Cambalear()); }
    }

    // ---------------------------------------------------------------- ataques
    IEnumerator Golpe(string clipe, float[] momentos, float velocidade)
    {
        ocupado = true;
        float vel = velocidade * (Fase2 ? 1.15f : 1f);
        mis.Tocar(clipe, 0.1f, vel, true);
        float dur = mis.Duracao(clipe) / vel, t = 0f; int prox = 0;
        GameObject aviso = null;
        if (clipe == "Cauda") aviso = AnelFixo(transform.position, 5.8f * E, new Color(1f, 0.25f, 0.15f));
        while (t < dur && !morto)
        {
            t += Time.deltaTime;
            float k = t / dur;
            if (k < 0.4f && alvo != null) Virar(alvo.position - transform.position, 8f);
            if (aviso != null) { aviso.transform.position = new Vector3(transform.position.x, ChaoParaAnel(transform.position), transform.position.z); }
            if (prox < momentos.Length && k >= momentos[prox])
            {
                if (clipe == "Mordida") Machucar(transform.position + transform.forward * 2.6f * E, 1.8f * E, 30f, 360f);
                else if (clipe == "Garras") Machucar(transform.position + transform.forward * 2.0f * E, 2.8f * E, 18f, 85f);
                else if (clipe == "Cauda") { Machucar(transform.position, 5.8f * E, 26f, 360f); Respingo(transform.position, 1.4f); TremorDeCamera.Tremer(0.2f, 0.4f); }
                prox++;
            }
            yield return null;
        }
        if (aviso != null) Destroy(aviso);
        mis.Tocar("Idle", 0.2f);
        proxAtaque = Time.time + Random.Range(0.8f, 1.5f) * MulRecarga;
        ocupado = false;
        // 2ª fase: às vezes emenda um segundo golpe
        if (Fase2 && clipe == "Mordida" && Random.value < 0.5f) { proxAtaque = 0f; }
    }

    IEnumerator Saltar()
    {
        ocupado = true;
        Vector3 alvoPos = alvo != null ? alvo.position : transform.position + transform.forward * 8f;
        Vector3 fora = alvoPos - centroLago; fora.y = 0f;
        if (fora.magnitude > raioArena - 4f) alvoPos = centroLago + fora.normalized * (raioArena - 4f);
        GameObject marca = AnelFixo(alvoPos, 4.8f * E, new Color(1f, 0.2f, 0.1f));
        mis.Tocar("Salto", 0.1f, 1f, true);
        float espera = 0.8f;
        for (float t = 0f; t < espera && !morto; t += Time.deltaTime) { Virar(alvoPos - transform.position, 10f); yield return null; }
        Vector3 de = transform.position; float dur = 0.85f;
        Vector3 pouso = new Vector3(alvoPos.x, AlturaDoChao(alvoPos), alvoPos.z);
        for (float t = 0f; t < dur && !morto; t += Time.deltaTime)
        {
            float k = t / dur;
            Vector3 p = Vector3.Lerp(de, pouso, k); p.y += Mathf.Sin(k * Mathf.PI) * 4.5f * E;
            transform.position = p; yield return null;
        }
        transform.position = pouso; chaoY = pouso.y;
        if (marca != null) Destroy(marca);
        Machucar(pouso, 4.8f * E, 36f, 360f);
        Respingo(pouso, 2.2f);
        if (efeitoPoeira != null) Destroy(Instantiate(efeitoPoeira, pouso, Quaternion.identity), 4f);
        Anel(new Vector3(pouso.x, Mathf.Max(pouso.y, nivelAgua) + 0.05f, pouso.z), 1f, 14f, 1.2f, new Color(0.8f, 0.95f, 0.85f), 0.45f);
        TremorDeCamera.Tremer(0.4f, 0.6f);
        mis.Tocar("Idle", 0.2f);
        yield return new WaitForSeconds(1.0f);      // cansado depois do pulo
        proxSalto = Time.time + Random.Range(7f, 11f) * MulRecarga;
        proxAtaque = Time.time + 0.4f;
        ocupado = false;
    }

    IEnumerator Rugir(bool transformacao)
    {
        ocupado = true;
        if (transformacao) { Fase2 = true; HUDJogador.Mostrar("O Devorador do Lago enfurece!", 2.6f); if (olhos != null) { olhos.intensidadeDaLuz *= 1.5f; olhos.Pulsar = 0.6f; } }
        mis.Tocar("Rugido", 0.1f, 1f, true);
        float dur = mis.Duracao("Rugido");
        yield return new WaitForSeconds(dur * 0.35f);
        if (efeitoRugido != null) Destroy(Instantiate(efeitoRugido, transform.position + Vector3.up * 1.4f, Quaternion.identity), 3f);
        Anel(new Vector3(transform.position.x, Mathf.Max(chaoY, nivelAgua) + 0.05f, transform.position.z), 1.5f, 22f, 1.1f, new Color(1f, 0.8f, 0.6f), 0.5f);
        TremorDeCamera.Tremer(0.5f, 1.3f);
        foreach (PlayerLocomotion p in Jogadores())
        {
            Vector3 d = p.transform.position - transform.position; d.y = 0f;
            if (d.magnitude > 14f * E) continue;
            Vida v = p.GetComponent<Vida>();
            if (v != null) v.ReceberDano(8f, transform.position);
            p.ReagirAoDano(transform.position, false);
        }
        // chama os monstros da lama
        if (prefabMonstro != null && Vivos() < 4)
        {
            int quantos = transformacao ? 3 : 2;
            for (int i = 0; i < quantos; i++)
            {
                float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad, r = Random.Range(7f, 12f);
                Vector3 p = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                p.y = AlturaDoChao(p);
                if (p.y < nivelAgua - 1.8f) { p = transform.position + transform.forward * Random.Range(4f, 6f); p.y = AlturaDoChao(p); }
                GameObject g = Instantiate(prefabMonstro, p, Quaternion.identity);
                MonstroDoPantano m = g.GetComponent<MonstroDoPantano>();
                if (m != null) { m.comecaEnterrado = true; m.distanciaDeEmboscada = 999f; filhotes.Add(m); }
            }
        }
        yield return new WaitForSeconds(Mathf.Max(0.3f, dur * 0.65f));
        mis.Tocar("Idle", 0.3f);
        proxRugido = Time.time + Random.Range(26f, 36f);
        proxAtaque = Time.time + 0.8f;
        ocupado = false;
    }

    IEnumerator Cambalear()
    {
        ocupado = true;
        mis.Tocar("Dano", 0.08f, 1f, true);
        yield return new WaitForSeconds(Mathf.Min(0.9f, mis.Duracao("Dano")));
        mis.Tocar("Idle", 0.2f);
        proxAtaque = Mathf.Max(proxAtaque, Time.time + 0.5f);
        ocupado = false;
    }

    // dano a jogadores (nunca a monstros): esfera em volta do centro, com ângulo opcional à frente do chefe
    void Machucar(Vector3 centro, float raio, float valor, float anguloMax)
    {
        float mult = Fase2 ? 1.2f : 1f;
        foreach (PlayerLocomotion p in Jogadores())
        {
            Vector3 alvoPt = p.transform.position + Vector3.up;
            if ((alvoPt - centro).magnitude > raio + 0.5f) continue;
            if (anguloMax < 359f)
            {
                Vector3 d = p.transform.position - transform.position; d.y = 0f;
                if (Vector3.Angle(transform.forward, d) > anguloMax * 0.5f + (d.magnitude < 1.5f ? 90f : 0f)) continue;
            }
            Vida v = p.GetComponent<Vida>(); if (v != null) v.ReceberDano(valor * mult, transform.position);
        }
    }

    // ---------------------------------------------------------------- morte
    void Morrer()
    {
        morto = true; intro = false; ocupado = true;
        StopAllCoroutines();
        mis.Tocar("Morte", 0.1f, 1f, true);
        if (olhos != null) { olhos.Pulsar = 0f; olhos.Acender(0f); }
        TremorDeCamera.Tremer(0.4f, 1.5f);
        foreach (MonstroDoPantano m in filhotes) if (m != null && m.Vivo) m.Vida.ReceberDano(99999f, transform.position);
        if (AoMorrer != null) AoMorrer(this);
        StartCoroutine(Afundar());
    }

    IEnumerator Afundar()
    {
        yield return new WaitForSeconds(Mathf.Max(2f, mis.Duracao("Morte")) + 1.5f);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Vector3 de = transform.position, ate = new Vector3(de.x, nivelAgua - 3.6f, de.z);
        float prox = 0f;
        for (float t = 0f; t < 4f; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(de, ate, Mathf.SmoothStep(0f, 1f, t / 4f));
            if (Time.time >= prox) { prox = Time.time + 0.6f; Anel(new Vector3(de.x, nivelAgua + 0.05f, de.z), 1f, 9f, 1.8f, new Color(0.7f, 0.9f, 0.8f), 0.3f); }
            yield return null;
        }
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- movimento e chão
    void Virar(Vector3 dir, float vel)
    {
        dir.y = 0f; if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), vel * Time.deltaTime);
    }

    void Mover(Vector3 dir, float vel)
    {
        Vector3 p = transform.position + dir * vel * Time.deltaTime;
        Vector3 fora = p - centroLago; fora.y = 0f;
        if (fora.magnitude > raioArena) { fora = fora.normalized * raioArena; p = new Vector3(centroLago.x + fora.x, p.y, centroLago.z + fora.z); }
        transform.position = p;
    }

    void Grudar()
    {
        chaoY = Mathf.Lerp(chaoY, AlturaDoChao(transform.position), Time.deltaTime * 8f);
        Vector3 p = transform.position; p.y = chaoY; transform.position = p;
    }

    float AlturaDoChao(Vector3 p)
    {
        float melhor = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 4f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.GetComponentInParent<Vida>() != null) continue;
            if (h.point.y > melhor) melhor = h.point.y;
        }
        if (melhor > float.NegativeInfinity) return melhor;
        Terrain t = Terrain.activeTerrain; return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y;
    }

    float ChaoParaAnel(Vector3 p) { return Mathf.Max(AlturaDoChao(p), nivelAgua) + 0.06f; }

    // ---------------------------------------------------------------- efeitos
    void Respingo(Vector3 pos, float escala)
    {
        AmbienteDaAgua.Respingo(pos, escala * 1.4f); AmbienteDaAgua.Ondular(new Vector3(pos.x, nivelAgua, pos.z), escala);
        if (efeitoRespingo == null) return;
        GameObject g = Instantiate(efeitoRespingo, pos, Quaternion.identity);
        g.transform.localScale *= escala; Destroy(g, 5f);
    }

    static Material Linha()
    {
        if (matLinha == null) { Shader s = Shader.Find("Sprites/Default"); if (s != null) matLinha = new Material(s); }
        return matLinha;
    }

    static void Circulo(LineRenderer lr, float raio)
    {
        int n = 48; lr.positionCount = n + 1; lr.loop = false; lr.useWorldSpace = false;
        for (int i = 0; i <= n; i++) { float a = i / (float)n * Mathf.PI * 2f; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * raio, 0f, Mathf.Sin(a) * raio)); }
    }

    // círculo de aviso parado (o jogador vê onde vai bater)
    GameObject AnelFixo(Vector3 centro, float raio, Color cor)
    {
        GameObject g = new GameObject("Aviso de ataque");
        g.transform.position = new Vector3(centro.x, ChaoParaAnel(centro), centro.z);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        Circulo(lr, raio); lr.startWidth = lr.endWidth = 0.3f; lr.sharedMaterial = Linha(); lr.startColor = lr.endColor = cor;
        StartCoroutine(PiscarAnel(lr, cor));
        return g;
    }
    IEnumerator PiscarAnel(LineRenderer lr, Color cor)
    {
        while (lr != null) { float a = 0.55f + 0.45f * Mathf.Sin(Time.time * 14f); Color c = new Color(cor.r, cor.g, cor.b, a); lr.startColor = lr.endColor = c; yield return null; }
    }

    // anel que cresce e some (ondas na água, onda de choque do rugido)
    void Anel(Vector3 centro, float r0, float r1, float tempo, Color cor, float largura)
    {
        StartCoroutine(AnelCrescendo(centro, r0, r1, tempo, cor, largura));
    }
    IEnumerator AnelCrescendo(Vector3 centro, float r0, float r1, float tempo, Color cor, float largura)
    {
        GameObject g = new GameObject("Onda");
        g.transform.position = centro;
        LineRenderer lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = Linha();
        for (float t = 0f; t < tempo; t += Time.deltaTime)
        {
            float k = t / tempo;
            Circulo(lr, Mathf.Lerp(r0, r1, 1f - (1f - k) * (1f - k)));
            lr.startWidth = lr.endWidth = largura * (1f - k * 0.6f);
            lr.startColor = lr.endColor = new Color(cor.r, cor.g, cor.b, 0.8f * (1f - k));
            yield return null;
        }
        Destroy(g);
    }
}
