using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if PINGLU_REDE
using Unity.Netcode;
#endif

// Diretor do trailer: roda DENTRO do jogo e conduz câmeras, cenas, legendas e título (~76 s).
// Usa as cenas reais do jogo (lobby, portal, mundo, cemitério) e dirige os heróis e os monstros por código.
// A trilha sonora (trilha_trailer.wav) toca daqui; o menu Pinglu > 35 grava tudo em vídeo.
[DefaultExecutionOrder(32000)]
public class TrailerDirector : MonoBehaviour
{
    public AudioClip trilha;
    public Font fonteLegenda, fonteTitulo;
    public Action AoTerminar;
    [Tooltip("Salva uma imagem por segundo em Recordings/Quadros (para conferir sem abrir o vídeo)")]
    public bool salvarQuadros = true;
    public float duracaoTotal = 76f;

    // estado
    private float t0 = -1f;
    private float T { get { return t0 < 0f ? 0f : Time.time - t0; } }
    private Camera cam;
    private MenuRede menu;
    private PalcoSelecao palco;
    private DragaoDistante dragaoLobby, dragaoMundo;
    private readonly List<GameObject> herois = new List<GameObject>();
    private readonly List<InimigoEsqueleto> inimigos = new List<InimigoEsqueleto>();
    private Vector3 centro, frente;   // centro do cemitério e para onde fica o portão
    private Vector3 luta;             // onde os heróis lutam (na praça, entre a cruz e o portão)
    private Vector3 direita { get { return Vector3.Cross(Vector3.up, frente); } }

    // sobreposições (IMGUI)
    private float negro = 1f, branco, tituloAlpha, subtituloAlpha;
    private string legenda = "";
    private float legendaAlpha;
    private GUIStyle estLegenda, estTitulo, estSub;
    private Texture2D pixel;
    private AudioSource som;

    private const BindingFlags PRIV = BindingFlags.NonPublic | BindingFlags.Instance;

    // ------------------------------------------------------------------
    private void Start() { StartCoroutine(Roteiro()); }

    // no lobby, a neblina do clima do mundo esconde o dragão e os castelos: aqui ela fica fina e quente
    private float proximoLog;
    private void LateUpdate()
    {
        if (t0 >= 0f && Time.time >= proximoLog) { proximoLog = Time.time + 5f; Debug.Log("[Trailer] T=" + T.ToString("0.0")); }
        if (t0 < 0f || T > 27f) return;
        float k = Mathf.Clamp01((T - 5f) / 4f);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.17f, 0.10f, 0.09f), k);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0.0042f, k);
    }

    private IEnumerator Roteiro()
    {
        pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
        som = gameObject.AddComponent<AudioSource>();
        som.clip = trilha; som.playOnAwake = false; som.spatialBlend = 0f; som.volume = 1f;

        // espera o menu montar o palco do lobby
        for (float w = 0f; w < 15f && (palco == null || !palco.Pronto); w += Time.deltaTime)
        {
            if (menu == null) menu = FindAnyObjectByType<MenuRede>();
            if (palco == null) palco = FindAnyObjectByType<PalcoSelecao>();
            yield return null;
        }
        yield return new WaitForSeconds(0.5f);
        if (menu != null) menu.enabled = false;          // esconde toda a interface do menu
        GameObject gcPalco = GameObject.Find("Camera do Palco");
        cam = gcPalco != null ? gcPalco.GetComponent<Camera>() : Camera.main;
        dragaoLobby = FindAnyObjectByType<DragaoDistante>();
        if (dragaoLobby != null) dragaoLobby.intervaloDoSopro = 99f;

        // ---- começa ----
        t0 = Time.time;
        som.Play();
        StartCoroutine(Legendas());
        if (salvarQuadros) StartCoroutine(Quadros());

        // 0-6 s: tela preta (só som)
        PosicionarLobby(new Vector3(-30f, 9f, 50f), new Vector3(0f, 7f, -10f), 50f);
        yield return Ate(5.4f);
        StartCoroutine(Fade(1f, 0f, 1.2f));                                       // abre o preto: 5,4 s -> 6,6 s

        // 6-14 s: sobrevoo lento do lobby em ruínas
        yield return Cena(14f, 5.4f,
            k => LobbyPos(Vector3.Lerp(new Vector3(-34f, 7.5f, 46f), new Vector3(30f, 11f, 34f), k)),
            k => LobbyPos(Vector3.Lerp(new Vector3(0f, 7f, -6f), new Vector3(-6f, 9f, -30f), k)), 52f, 44f);
        // 14-20 s: o dragão cruza o céu e sopra fogo
        ForcarSopro(dragaoLobby, 14.4f);
        yield return Cena(20f, 14f,
            k => LobbyPos(new Vector3(-8f + 12f * k, 12f, 10f)),
            k => PosicaoDragao(dragaoLobby, LobbyPos(new Vector3(0f, 22f, -118f))), 44f, 34f);
        // 18,5 s: os heróis aparecem em frente ao portal
        yield return Ate(18.4f);
        if (palco != null) palco.MostrarGrupo(FichasDisponiveis());
        dragaoMundo = ClonarDragao(dragaoLobby);                                   // para a cena final
        // nada de monstros nem hordas enquanto filmamos o início
        foreach (EmboscadaDaFloresta em in FindObjectsByType<EmboscadaDaFloresta>(FindObjectsSortMode.None)) em.enabled = false;
        foreach (GerenciadorDeOndas go in FindObjectsByType<GerenciadorDeOndas>(FindObjectsSortMode.None)) go.enabled = false;
        StartCoroutine(SemInterface(25.5f, 62f));
        Debug.Log("[Trailer] dragão do lobby: " + (dragaoLobby != null) + ", clone: " + (dragaoMundo != null));
        // 20-22,5 s: close no portal, a câmera recua revelando os heróis de costas
        Vector3 luga = new Vector3(0f, -3000f, 0f);
        yield return Cena(22.5f, 20f,
            k => luga + Vector3.Lerp(new Vector3(2.4f, 1.1f, -3.4f), new Vector3(0.2f, 1.35f, 6.2f), k * k * (3f - 2f * k)),
            k => luga + new Vector3(-0.25f, Mathf.Lerp(2.4f, 1.15f, k), -7.5f), 40f, 30f);
        // 22,6 s: entram no portal (clarão ~26,3 s) e chegam ao mundo
        yield return Ate(22.6f);
        if (menu != null)
        {
            menu.enabled = true;
            MethodInfo entrar = menu.GetType().GetMethod("EntrarSozinho", PRIV);
            if (entrar != null) entrar.Invoke(menu, null);
        }
        yield return Ate(27.5f);
        Interface(false);
        if (menu != null) menu.enabled = false;
        StartCoroutine(CaminhadaInicial(29.4f, 35.8f));
        // 28,5-36 s: o herói sai do redemoinho e o portal se fecha (câmera do próprio jogo)
        yield return Ate(35.8f);

        // ---- 36-62 s: cemitério ----
        yield return PrepararCemiterio();
        yield return Combate();

        // 62-68 s: silêncio, a sombra do dragão
        yield return Dragao();

        // 68-76 s: título
        negro = 1f;
        yield return Ate(68.6f);
        StartCoroutine(Titulo());
        yield return Ate(duracaoTotal);
        Debug.Log("[Trailer] fim do roteiro em T=" + T.ToString("0.0"));
        if (AoTerminar != null) AoTerminar();
    }

    private void OnDestroy() { Debug.Log("[Trailer] diretor destruído em T=" + T.ToString("0.0")); }

    // ------------------------------------------------------------------
    //  utilidades de tempo e câmera
    // ------------------------------------------------------------------
    private IEnumerator Ate(float tempo) { while (T < tempo) yield return null; }

    private IEnumerator FadeEm(float quando, float dur) { yield return Ate(quando); yield return Fade(0f, 1f, dur); }

    private IEnumerator Fade(float de, float para, float dur)
    {
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; negro = Mathf.Lerp(de, para, Mathf.SmoothStep(0f, 1f, t / dur)); yield return null; }
        negro = para;
    }

    // câmera: pos(k) e alvo(k) com k de 0 a 1, do instante "inicio" até "fim" (em T)
    private IEnumerator Cena(float fim, float inicio, Func<float, Vector3> pos, Func<float, Vector3> alvo, float fov0, float fov1)
    {
        while (T < inicio) yield return null;
        while (T < fim)
        {
            float k = Mathf.Clamp01((T - inicio) / Mathf.Max(0.01f, fim - inicio));
            if (cam != null)
            {
                cam.transform.position = pos(k);
                Vector3 d = alvo(k) - cam.transform.position;
                if (d.sqrMagnitude > 0.0001f) cam.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
                cam.fieldOfView = Mathf.Lerp(fov0, fov1, k);
            }
            yield return null;
        }
    }

    private static Vector3 LobbyPos(Vector3 local) { return new Vector3(0f, -3000f, 0f) + local; }
    private void PosicionarLobby(Vector3 p, Vector3 alvo, float fov)
    {
        if (cam == null) return;
        cam.transform.position = LobbyPos(p);
        cam.transform.LookAt(LobbyPos(alvo));
        cam.fieldOfView = fov;
    }

    private Vector3 PosicaoDragao(DragaoDistante d, Vector3 padrao)
    {
        if (d == null) return padrao;
        Transform c = d.transform.Find("Dragao 3D");
        return c != null ? c.position : padrao;
    }

    private void ForcarSopro(DragaoDistante d, float quando)
    {
        if (d != null) StartCoroutine(SoproEm(d, quando));
    }

    private IEnumerator SoproEm(DragaoDistante d, float quando)
    {
        yield return Ate(quando);
        FieldInfo f = typeof(DragaoDistante).GetField("proximoFogo", PRIV);
        if (f != null) f.SetValue(d, Time.time - 0.1f);
    }

    private DragaoDistante ClonarDragao(DragaoDistante origem)
    {
        if (origem == null) return null;
        GameObject g = Instantiate(origem.gameObject);
        g.transform.SetParent(null, true);
        g.SetActive(false);
        g.name = "Dragao do Trailer";
        return g.GetComponent<DragaoDistante>();
    }

    private List<FichaPersonagem> FichasDisponiveis()
    {
        List<FichaPersonagem> r = new List<FichaPersonagem>();
        if (menu == null) return r;
        FieldInfo f = menu.GetType().GetField("personagens", PRIV);
        List<FichaPersonagem> todas = f != null ? (List<FichaPersonagem>)f.GetValue(menu) : null;
        if (todas != null)
            foreach (FichaPersonagem p in todas)
                if (p != null && p.disponivel && p.prefabJogador != null && p.modeloVisual != null) r.Add(p);
        if (r.Count > 4) r.RemoveRange(4, r.Count - 4);
        return r;
    }

    private IEnumerator SemInterface(float de, float ate)
    {
        yield return Ate(de);
        while (T < ate) { Interface(false); yield return null; }
    }

    // some com a interface do jogo (HUD, mini-mapa, mapa)
    private void Interface(bool ligar)
    {
        foreach (HUDJogador h in Resources.FindObjectsOfTypeAll<HUDJogador>()) if (h != null && h.gameObject.scene.IsValid()) h.enabled = ligar;
        foreach (MiniMapa m in Resources.FindObjectsOfTypeAll<MiniMapa>()) if (m != null && m.gameObject.scene.IsValid()) m.enabled = ligar;
        foreach (MapaDoMundo m in Resources.FindObjectsOfTypeAll<MapaDoMundo>()) if (m != null && m.gameObject.scene.IsValid()) m.enabled = ligar;
    }

    // o herói sai andando do portal, com a câmera atrás dele
    private IEnumerator CaminhadaInicial(float de, float ate)
    {
        yield return Ate(de);
        PlayerLocomotion real = AcharJogador();
        if (real == null) yield break;
        PortalDoMundo portal = PortalDoMundo.Atual;
        Vector3 dir = portal != null ? portal.Frente : real.transform.forward;
        dir.y = 0f; dir.Normalize();
        ThirdPersonCameraFollow follow = FindAnyObjectByType<ThirdPersonCameraFollow>();
        if (follow != null) follow.enabled = false;
        real.enabled = false;
        cam = Camera.main;
        Animator a = real.GetComponentInChildren<Animator>();
        CharacterController cc = real.GetComponent<CharacterController>();
        Vector3 lado = Vector3.Cross(Vector3.up, dir);
        while (T < ate)
        {
            float k = Mathf.Clamp01((T - de) / (ate - de));
            if (a != null) a.SetFloat("Speed", 0.5f);
            real.transform.rotation = Quaternion.Slerp(real.transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 6f * Time.deltaTime);
            if (cc != null && cc.enabled) cc.Move((dir * 1.7f + Vector3.down * 2f) * Time.deltaTime);
            if (cam != null)
            {
                Vector3 h = real.transform.position;
                cam.transform.position = h - dir * Mathf.Lerp(3.2f, 6.5f, k) + lado * Mathf.Lerp(1.3f, -1.1f, k) + Vector3.up * Mathf.Lerp(1.3f, 1.8f, k);
                cam.transform.rotation = Quaternion.LookRotation(h + Vector3.up * 1.3f + dir * 2f - cam.transform.position, Vector3.up);
                cam.fieldOfView = 46f;
            }
            yield return null;
        }
        if (a != null) a.SetFloat("Speed", 0f);
    }

    // ------------------------------------------------------------------
    //  cemitério: heróis e horda
    // ------------------------------------------------------------------
    private PlayerLocomotion AcharJogador()
    {
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
            if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && !herois.Contains(p.gameObject)) return p;
        return null;
    }

    private IEnumerator PrepararCemiterio()
    {
        GerenciadorDeOndas ger = GerenciadorDeOndas.Instancia != null ? GerenciadorDeOndas.Instancia : FindAnyObjectByType<GerenciadorDeOndas>();
        if (ger != null)
        {
            centro = ger.transform.position; frente = ger.transform.forward; frente.y = 0f; frente.Normalize();
            ger.enabled = false;
        }
        luta = centro + frente * 7.5f;
        EmboscadaDaFloresta emb = FindAnyObjectByType<EmboscadaDaFloresta>();
        if (emb != null) emb.enabled = false;

        ThirdPersonCameraFollow follow = FindAnyObjectByType<ThirdPersonCameraFollow>();
        if (follow != null) follow.enabled = false;
        cam = Camera.main;

        PlayerLocomotion real = AcharJogador();
        List<GameObject> prefabs = new List<GameObject>();
        List<FichaPersonagem> fichas = FichasDisponiveis();
        int escolhido = SessaoDeJogo.PersonagemEscolhido;
        if (real != null) herois.Add(real.gameObject);
        for (int i = 0; i < fichas.Count && herois.Count < 4; i++)
        {
            if (real != null && real.gameObject.name.Contains(fichas[i].nome)) continue;
            GameObject h = CriarHeroi(fichas[i].prefabJogador);
            if (h != null) herois.Add(h);
        }
        // forma o grupo na praça, de frente para o fundo do cemitério
        for (int i = 0; i < herois.Count; i++)
        {
            Vector3 p = luta + direita * ((i - (herois.Count - 1) * 0.5f) * 2.3f) - frente * (0.4f * Mathf.Abs(i - 1.5f));
            Colocar(herois[i], p, Quaternion.LookRotation(frente, Vector3.up));
            PreparaHeroi(herois[i]);
        }
        yield return null;
    }

    private GameObject CriarHeroi(GameObject prefab)
    {
        if (prefab == null) return null;
        GameObject espera = new GameObject("espera");
        espera.SetActive(false);
        GameObject j = Instantiate(prefab, centro, Quaternion.identity, espera.transform);
#if PINGLU_REDE
        foreach (NetworkBehaviour nb in j.GetComponentsInChildren<NetworkBehaviour>(true)) DestroyImmediate(nb);
        foreach (NetworkObject no in j.GetComponentsInChildren<NetworkObject>(true)) DestroyImmediate(no);
#endif
        j.transform.SetParent(null, true);
        Destroy(espera);
        return j;
    }

    private void Colocar(GameObject h, Vector3 p, Quaternion r)
    {
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.08f;
        CharacterController cc = h.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        h.transform.SetPositionAndRotation(p, r);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;
    }

    private void PreparaHeroi(GameObject h)
    {
        PlayerLocomotion loco = h.GetComponent<PlayerLocomotion>();
        if (loco != null) loco.enabled = false;               // sem controles: tudo é dirigido daqui
        HUDJogador hud = h.GetComponent<HUDJogador>(); if (hud != null) hud.enabled = false;
        Vida v = h.GetComponent<Vida>();
        if (v != null) { v.FiltroDeDano = (d, o) => 0f; }    // heróis invencíveis no trailer
        Animator a = h.GetComponentInChildren<Animator>();
        if (a != null) { a.SetFloat("Speed", 0f); a.SetBool("Block", false); a.SetBool("Grounded", true); }
    }

    private GameObject Heroi(int i) { return herois.Count == 0 ? null : herois[Mathf.Clamp(i, 0, herois.Count - 1)]; }

    private IEnumerator Combate()
    {
        GameObject[] pref = GerenciadorDeOndas.Instancia != null ? GerenciadorDeOndas.Instancia.PrefabsDeMonstros : new GameObject[0];
        if (pref.Length == 0) yield break;

        // ---- 36-40: a horda sai do chão em volta dos heróis; câmera atrás do grupo ----
        StartCoroutine(Horda(pref, 12, 8.5f, 36.2f, 3.2f));
        StartCoroutine(Luta(36.0f, 62f));
        yield return Cena(40f, 36f,
            k => luta - frente * Mathf.Lerp(5.8f, 3.4f, k) + Vector3.up * Mathf.Lerp(2.6f, 1.6f, k),
            k => luta + frente * 3.5f + Vector3.up * 1.2f, 50f, 38f);
        // ---- 40-43: Cavaleiro Sombrio, combo de espada ----
        yield return CenaHeroi(0, 43f, 40f, new Vector3(-1.9f, 0.9f, 2.4f), new Vector3(-1.2f, 1.2f, 1.8f), new Vector3(0f, 1.3f, 1.5f), 46f, 40f);
        // ---- 43-46: Morgana, raio ----
        yield return CenaHeroi(1, 46f, 43f, new Vector3(2.8f, 1.5f, 3.4f), new Vector3(3.6f, 1.9f, 2.2f), new Vector3(0f, 1.4f, 2.5f), 48f, 40f);
        // ---- 46-48,5: Tanque, escudo ----
        StartCoroutine(Defender(2, 46.2f, 2.3f));
        yield return CenaHeroi(2, 48.5f, 46f, new Vector3(0.2f, 1.1f, 2.3f), new Vector3(0.9f, 1.4f, 3.1f), new Vector3(0f, 1.1f, 0f), 42f, 36f);
        // ---- 48,5-51: rolamento ----
        StartCoroutine(Rolar(0, 48.7f));
        yield return CenaHeroi(0, 51f, 48.5f, new Vector3(2.4f, 1.4f, 4.4f), new Vector3(-2f, 1.1f, 3.4f), new Vector3(0f, 0.9f, 0.5f), 44f, 44f);
        // ---- 51-55: guindaste subindo, horda cresce ----
        StartCoroutine(Horda(pref, 10, 10.5f, 51f, 2.5f));
        yield return Cena(55f, 51f,
            k => luta - frente * Mathf.Lerp(6f, 9f, k) + direita * Mathf.Lerp(-3f, 2f, k) + Vector3.up * Mathf.Lerp(2.5f, 11f, k * k),
            k => luta + Vector3.up * 1f, 52f, 46f);
        // ---- 55-62: os quatro cercados, câmera orbitando ----
        StartCoroutine(Horda(pref, 14, 7f, 55.2f, 3f));
        yield return Cena(62f, 55f,
            k => luta + Quaternion.Euler(0f, Mathf.Lerp(20f, 250f, k), 0f) * (frente * Mathf.Lerp(6.2f, 4.2f, k)) + Vector3.up * Mathf.Lerp(1.9f, 1.4f, k),
            k => luta + Vector3.up * 1.3f, 54f, 40f);
    }

    private IEnumerator CenaHeroi(int i, float fim, float inicio, Vector3 de, Vector3 para, Vector3 olhar, float fov0, float fov1)
    {
        GameObject h = Heroi(i);
        if (h == null) yield break;
        Quaternion r0 = h.transform.rotation;           // fixa o referencial no começo do plano
        Vector3 p0 = h.transform.position;
        yield return Cena(fim, inicio,
            k => (h != null ? h.transform.position : p0) + r0 * Vector3.Lerp(de, para, Mathf.SmoothStep(0f, 1f, k)),
            k => (h != null ? h.transform.position : p0) + r0 * olhar + Vector3.up * 0.0f, fov0, fov1);
    }

    // horda: esqueletos surgem do chão em volta do centro e correm para os heróis
    private IEnumerator Horda(GameObject[] prefabs, int n, float raio, float quando, float duracao)
    {
        yield return Ate(quando);
        for (int i = 0; i < n; i++)
        {
            float a = (i + UnityEngine.Random.value * 0.6f) * Mathf.PI * 2f / n;
            Vector3 p = luta + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (raio + UnityEngine.Random.Range(-1.2f, 1.5f));
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            GameObject pf = prefabs[0];
            if (prefabs.Length > 1 && UnityEngine.Random.value < 0.25f) pf = prefabs[UnityEngine.Random.Range(1, Mathf.Min(prefabs.Length, 4))];
            GameObject g = Instantiate(pf, p, Quaternion.LookRotation(luta - p));
            InimigoEsqueleto ini = g.GetComponent<InimigoEsqueleto>();
            if (ini == null) { Destroy(g); continue; }
            ini.Surgir();
            inimigos.Add(ini);
            yield return new WaitForSeconds(duracao / n);
        }
    }

    // os heróis golpeiam o inimigo mais perto, em ciclos, e o inimigo cai
    private IEnumerator Luta(float inicio, float fim)
    {
        yield return Ate(inicio + 1.2f);
        float[] prox = new float[4];
        int[] golpe = new int[4];
        for (int i = 0; i < 4; i++) prox[i] = Time.time + i * 0.25f;
        while (T < fim)
        {
            for (int i = 0; i < herois.Count; i++)
            {
                GameObject h = herois[i];
                if (h == null || Time.time < prox[i]) continue;
                InimigoEsqueleto alvo = MaisPerto(h.transform.position);
                if (alvo == null) { prox[i] = Time.time + 0.4f; continue; }
                Vector3 d = alvo.transform.position - h.transform.position; d.y = 0f;
                if (d.magnitude > 6.5f) { prox[i] = Time.time + 0.4f; continue; }
                if (d.sqrMagnitude > 0.01f) h.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                PlayerLocomotion loco = h.GetComponent<PlayerLocomotion>();
                if (loco != null) loco.TocarGolpeRemoto(golpe[i] % 3);
                golpe[i]++;
                prox[i] = Time.time + UnityEngine.Random.Range(0.9f, 1.3f);
                StartCoroutine(Acertar(alvo, h.transform.position, 0.45f));
            }
            yield return null;
        }
    }

    private IEnumerator Acertar(InimigoEsqueleto alvo, Vector3 origem, float depois)
    {
        yield return new WaitForSeconds(depois);
        if (alvo == null) yield break;
        Vida v = alvo.Vida;
        if (v != null && !v.Morto) v.ReceberDano(v.Maxima * 0.55f + 10f, origem);
    }

    private InimigoEsqueleto MaisPerto(Vector3 p)
    {
        InimigoEsqueleto m = null; float melhor = float.MaxValue;
        for (int i = inimigos.Count - 1; i >= 0; i--)
        {
            InimigoEsqueleto e = inimigos[i];
            if (e == null) { inimigos.RemoveAt(i); continue; }
            if (e.Vida == null || e.Vida.Morto) continue;
            float d = (e.transform.position - p).sqrMagnitude;
            if (d < melhor) { melhor = d; m = e; }
        }
        return m;
    }

    private IEnumerator Defender(int i, float quando, float dur)
    {
        yield return Ate(quando);
        GameObject h = Heroi(i);
        if (h == null) yield break;
        Animator a = h.GetComponentInChildren<Animator>();
        if (a != null) a.SetBool("Block", true);
        yield return new WaitForSeconds(dur);
        if (a != null) a.SetBool("Block", false);
    }

    private IEnumerator Rolar(int i, float quando)
    {
        yield return Ate(quando);
        GameObject h = Heroi(i);
        if (h == null) yield break;
        PlayerLocomotion loco = h.GetComponent<PlayerLocomotion>();
        Animator a = h.GetComponentInChildren<Animator>();
        if (loco != null) loco.TocarAcaoRemota(2);
        if (a != null) a.SetBool("Rolling", true);
        Vector3 d = h.transform.right;
        CharacterController cc = h.GetComponent<CharacterController>();
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (cc != null && cc.enabled) cc.Move(d * 7f * Time.deltaTime);
            yield return null;
        }
        if (a != null) a.SetBool("Rolling", false);
    }

    // ------------------------------------------------------------------
    //  62-68 s: tudo para; a sombra do dragão passa sobre os heróis
    // ------------------------------------------------------------------
    private IEnumerator Dragao()
    {
        yield return Ate(62f);
        // congela os esqueletos e os heróis olham para cima
        foreach (InimigoEsqueleto e in inimigos)
        {
            if (e == null) continue;
            Animator an = e.GetComponentInChildren<Animator>();
            if (an != null) an.speed = 0f;
            e.enabled = false;
        }
        foreach (GameObject h in herois)
        {
            if (h == null) continue;
            Animator an = h.GetComponentInChildren<Animator>();
            if (an != null) { an.SetFloat("Speed", 0f); an.SetBool("Block", false); }
        }
        if (dragaoMundo != null)
        {
            dragaoMundo.transform.position = luta - dragaoMundo.centroDoVoo + frente * 18f;
            dragaoMundo.intervaloDoSopro = 99f;
            dragaoMundo.gameObject.SetActive(true);
        }
        Vector3 baixo = luta - frente * 3.5f + Vector3.up * 0.7f;
        StartCoroutine(FadeEm(67.2f, 0.7f));
        yield return Cena(68f, 62f,
            k => baixo + direita * Mathf.Lerp(-0.6f, 0.6f, k),
            k => dragaoMundo != null ? PosicaoDragao(dragaoMundo, luta + Vector3.up * 30f) : Vector3.Lerp(luta + Vector3.up * 8f, luta + Vector3.up * 32f, k), 58f, 62f);
    }

    // ------------------------------------------------------------------
    //  legendas, título e quadros
    // ------------------------------------------------------------------
    private IEnumerator Legendas()
    {
        yield return Legenda(7.0f, 11.8f, "Ele veio do céu...");
        yield return Legenda(14.8f, 19.2f, "...e nada restou.");
        yield return Legenda(28.8f, 34.0f, "Não há caminho de volta.");
        yield return Legenda(55.6f, 61.4f, "Juntos... ou esquecidos.");
    }

    private IEnumerator Legenda(float de, float ate, string texto)
    {
        yield return Ate(de);
        legenda = texto;
        while (T < ate)
        {
            legendaAlpha = Mathf.Clamp01(Mathf.Min((T - de) / 0.6f, (ate - T) / 0.6f));
            yield return null;
        }
        legendaAlpha = 0f;
    }

    private IEnumerator Titulo()
    {
        float t = 0f;
        while (t < 7f)
        {
            t += Time.deltaTime;
            tituloAlpha = Mathf.Clamp01(t / 2.2f);
            subtituloAlpha = Mathf.Clamp01((t - 3.2f) / 1.5f);
            yield return null;
        }
    }

    private IEnumerator Quadros()
    {
        string pasta = System.IO.Path.Combine(Application.dataPath, "../Recordings/Quadros");
        System.IO.Directory.CreateDirectory(pasta);
        int n = 0;
        while (T < duracaoTotal)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(pasta, "q" + n.ToString("00") + ".png"), 1);
            n++;
            float proximo = T + 1f;
            while (T < proximo) yield return null;
        }
    }

    private void OnGUI()
    {
        if (pixel == null) return;
        GUI.depth = -1000;
        float e = Screen.height / 1080f;
        if (estLegenda == null)
        {
            estLegenda = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 46, font = fonteLegenda };
            estTitulo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 118, fontStyle = FontStyle.Bold, font = fonteTitulo };
            estSub = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 40, font = fonteLegenda };
        }
        // faixas de cinema
        Color c0 = GUI.color;
        float faixa = Screen.height * 0.075f * (T > 5f && T < 68f ? 1f : 0f);
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, faixa), pixel);
        GUI.DrawTexture(new Rect(0, Screen.height - faixa, Screen.width, faixa), pixel);

        if (legendaAlpha > 0f)
        {
            estLegenda.fontSize = Mathf.RoundToInt(46 * e);
            Rect r = new Rect(0, Screen.height - faixa - 130f * e, Screen.width, 90f * e);
            GUI.color = new Color(0f, 0f, 0f, legendaAlpha); GUI.Label(new Rect(r.x + 3f * e, r.y + 3f * e, r.width, r.height), legenda, estLegenda);
            GUI.color = new Color(0.96f, 0.92f, 0.84f, legendaAlpha); GUI.Label(r, legenda, estLegenda);
        }
        if (negro > 0f) { GUI.color = new Color(0f, 0f, 0f, negro); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), pixel); }

        if (tituloAlpha > 0f)
        {
            GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), pixel);
            DesenharBrasas(e);
            estTitulo.fontSize = Mathf.RoundToInt(118 * e);
            Rect r = new Rect(0, Screen.height * 0.34f, Screen.width, 160f * e);
            GUI.color = new Color(0f, 0f, 0f, tituloAlpha); GUI.Label(new Rect(r.x + 4f * e, r.y + 4f * e, r.width, r.height), "TRIAL OF THE FORSAKEN", estTitulo);
            GUI.color = new Color(0.93f, 0.74f, 0.32f, tituloAlpha); GUI.Label(r, "TRIAL OF THE FORSAKEN", estTitulo);
            if (subtituloAlpha > 0f)
            {
                estSub.fontSize = Mathf.RoundToInt(40 * e);
                GUI.color = new Color(0.85f, 0.8f, 0.72f, subtituloAlpha);
                GUI.Label(new Rect(0, Screen.height * 0.34f + 170f * e, Screen.width, 60f * e), "Em breve", estSub);
            }
        }
        GUI.color = c0;
    }

    // brasas subindo (IMGUI, bem simples)
    private void DesenharBrasas(float e)
    {
        for (int i = 0; i < 90; i++)
        {
            float seed = i * 7.31f;
            float vel = 40f + (i * 37 % 90);
            float y = Screen.height - ((Time.time * vel + seed * 53f) % (Screen.height * 1.1f));
            float x = (Mathf.PerlinNoise(seed, Time.time * 0.25f) * 0.5f + (i * 0.6180339f % 1f) * 0.55f) * Screen.width;
            float tam = (2f + (i % 4)) * e;
            float a = tituloAlpha * Mathf.Clamp01(1f - Mathf.Abs(y / Screen.height - 0.5f) * 1.2f) * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f + seed));
            GUI.color = new Color(1f, 0.45f + 0.3f * ((i % 5) / 5f), 0.12f, a);
            GUI.DrawTexture(new Rect(x, y, tam, tam), pixel);
        }
    }
}
