using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// O TRAILER NOVO (Trial of the Forsaken). Gravado sem ninguém: PINGLU_TRAILER=1 Unity -projectPath <projeto> -executeMethod PingluTrailerFinal.GravarEFechar
// Partes (PINGLU_TRAILER_PARTES="prologo,heroi0,heroi1,heroi2,heroi3,guardiao,cavaleiro,ladrao,fera,dragao,final"; vazio = todas, em ordem):
//   prologo = frases de lore sobre cenas do mundo + cartão;  heroiN = à ESQUERDA a jogabilidade e à DIREITA o painel do personagem;
//   chefes = jogabilidade em tela cheia (barras de cinema), nome do chefe em letras grandes e os 4 heróis lutando;  final = título.
public static class TrailerModo
{
    public static readonly bool Ativo = Environment.GetEnvironmentVariable("PINGLU_TRAILER") == "1";
    public static string Partes { get { return Environment.GetEnvironmentVariable("PINGLU_TRAILER_PARTES") ?? ""; } }
}

public class TrailerFinalDiretor : MonoBehaviour
{
    public AudioClip trilha; public Action aoTerminar;
    enum Modo { Preto, Painel, Cheio, Cartao }
    Modo modo = Modo.Preto;

    Camera camT; RenderTexture rtP, rtF; Light sol; Font fCinzel, fFell; Texture2D gradV, gradH, gradVInv;
    const int RTW = 1152, RTH = 1080;
    readonly List<FichaPersonagem> fichas = new List<FichaPersonagem>();
    readonly List<GameObject> lixo = new List<GameObject>();
    Vector3 centro, praca, vila; Vector3 eixoDin = Vector3.forward;

    bool painelOn { get { return modo == Modo.Painel; } }
    float painelDesde; string pNome, pTitulo, pDesc; FichaPersonagem pFicha; HabilidadesDoPersonagem pHab; int pIndice, pTotal;
    readonly float[] brilhoSkill = new float[4]; float negro = 1f;
    string legendaTxt, grandeTxt, grandeSub, cartaoTxt; float legendaA, grandeA, cartaoA;
    Vector3 camPos, camAlvo; float camFov = 50f; bool camPronta;

    // procura, em volta da praça, o lugar mais livre de pedras e túmulos (para a câmera enxergar)
    Vector3 ArenaAberta(Vector3 c)
    {
        Vector3 melhor = c; int menor = int.MaxValue;
        for (int i = 0; i < 16; i++)
        {
            float a = i * 22.5f * Mathf.Deg2Rad; Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 24f; p.y = Chao(p);
            int n = 0; foreach (Collider col in Physics.OverlapSphere(p + Vector3.up * 2f, 11f, ~0, QueryTriggerInteraction.Ignore)) if (!(col is TerrainCollider)) n++;
            if (n < menor) { menor = n; melhor = p; }
        }
        return melhor;
    }
    static float chaoExtra;      // dentro do castelo o piso fica acima do terreno
    static float Chao(Vector3 p) { Terrain t = Terrain.activeTerrain; return (t != null ? t.SampleHeight(p) + t.transform.position.y : p.y) + chaoExtra; }

    // ---- eventos para o som: cada golpe/habilidade/impacto vai para o registro com o tempo do VÍDEO (o som é montado em cima disso)
    static int frame0 = -1; string tipoAtual = "";
    float TV { get { return (Time.frameCount - frame0) / 60f; } }
    void Som(string tipo, string arg) { Debug.Log("[SOM] " + TV.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + tipo + " " + arg); }

    void Awake() { if (frame0 < 0) frame0 = Time.frameCount; MenuRede m = FindAnyObjectByType<MenuRede>(); if (m != null) m.enabled = false; }      // o menu do jogo nunca aparece no vídeo

    // ------------------------------------------------------------------ início
    IEnumerator Start()
    {
        fCinzel = Resources.Load<Font>("Fontes/CinzelBold"); fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
        gradV = Grad(false); gradH = Grad(true); gradVInv = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int i = 0; i < 256; i++) { float k = 1f - i / 255f; gradVInv.SetPixel(0, i, new Color(1f, 1f, 1f, k * k)); } gradVInv.Apply();
        for (float w = 0f; w < 15f && (Terrain.activeTerrain == null || FindAnyObjectByType<GerenciadorDeOndas>() == null); w += Dt) yield return null;
        yield return Esp(3f);
        MenuRede menu = FindAnyObjectByType<MenuRede>();
        if (menu != null)
        {
            FieldInfo f = menu.GetType().GetField("personagens", BindingFlags.NonPublic | BindingFlags.Instance);
            List<FichaPersonagem> todas = f != null ? f.GetValue(menu) as List<FichaPersonagem> : null;
            if (todas != null) foreach (FichaPersonagem p in todas) if (p != null && p.disponivel && p.prefabJogador != null) fichas.Add(p);
        }
        rtP = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32) { name = "Trailer painel", antiAliasing = 4 };
        rtF = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "Trailer cheio", antiAliasing = 4 };
        GameObject g = new GameObject("Câmera do trailer"); camT = g.AddComponent<Camera>(); camT.targetTexture = rtF; camT.fieldOfView = 50f; camT.nearClipPlane = 0.2f; camT.farClipPlane = 3000f;
        camT.clearFlags = CameraClearFlags.Skybox; camT.allowHDR = true; camT.depth = 100f; camT.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        FxCena.CamOverride = camT; sol = RenderSettings.sun;
        if (sol == null) foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sol = l; break; }
        PracaDoGuardiao pr = FindAnyObjectByType<PracaDoGuardiao>(); praca = pr != null ? pr.Centro() : Vector3.zero; centro = ArenaAberta(praca);
        NpcFalante caio = null; foreach (NpcFalante n in FindObjectsByType<NpcFalante>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (n != null && n.nomeExibido == "Caio") { caio = n; break; }
        vila = caio != null ? caio.transform.position : (PortalDoMundo.Atual != null ? PortalDoMundo.Atual.transform.position : centro);
        if (trilha != null) { AudioSource a = gameObject.AddComponent<AudioSource>(); a.clip = trilha; a.spatialBlend = 0f; a.Play(); }
        Debug.Log("[Trailer] pronto. heróis=" + fichas.Count + " arena=" + centro.ToString("F0") + " vila=" + vila.ToString("F0") + " partes=" + TrailerModo.Partes);
        yield return StartCoroutine(Roteiro());
        Debug.Log("[Trailer] fim do roteiro");
        if (aoTerminar != null) aoTerminar();
    }

    IEnumerator Roteiro()
    {
        string[] todas = { "prologo", "heroi0", "heroi1", "heroi2", "heroi3", "guardiao", "cavaleiro", "ladrao", "fera", "dragao", "final" };
        List<string> pedidas = new List<string>(); string env = TrailerModo.Partes;
        if (string.IsNullOrEmpty(env)) pedidas.AddRange(todas); else foreach (string s in env.Split(',')) pedidas.Add(s.Trim().ToLowerInvariant());
        foreach (string p in pedidas)
        {
            Debug.Log("[Trailer] parte: " + p); Som("parte", p);
            if (p == "prologo") yield return StartCoroutine(Prologo());
            else if (p.StartsWith("heroi")) { int i = p[5] - '0'; if (i < fichas.Count) yield return StartCoroutine(SegmentoHeroi(i)); }
            else if (p == "guardiao") yield return StartCoroutine(SegmentoChefe("O GUARDIÃO DAS LÁPIDES", "Vigia dos mortos", "guardiao", new Color(0.12f, 0.16f, 0.24f), 0.0055f));
            else if (p == "cavaleiro") yield return StartCoroutine(SegmentoChefe("O CAVALEIRO CAÍDO", "A ronda que nunca termina", "cavaleiro", new Color(0.22f, 0.15f, 0.14f), 0.005f));
            else if (p == "ladrao") yield return StartCoroutine(SegmentoChefe("MALAKAR, O LADRÃO", "Dono do último cristal", "ladrao", new Color(0.18f, 0.1f, 0.2f), 0.006f));
            else if (p == "fera") yield return StartCoroutine(SegmentoChefe("A FERA DO PÂNTANO", "Desperta sob as águas escuras", "fera", new Color(0.09f, 0.2f, 0.15f), 0.008f));
            else if (p == "dragao") yield return StartCoroutine(SegmentoDragao());
            else if (p == "final") yield return StartCoroutine(Final());
        }
        yield return Esp(0.5f);
    }

    // espera em segundos do VÍDEO (o gravador congela o relógio do jogo; o relógio real anda mais devagar que o vídeo)
    IEnumerator Esp(float s) { for (float x = 0f; x < s; x += Dt) yield return null; }

    // tempo de cada quadro: na gravação o relógio é fixo (1/60 s); o Time.unscaledDeltaTime seria o tempo REAL (6x maior) e os textos sumiriam depressa demais
    static float Dt { get { return Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime; } }
    float escalaBase = 1f;      // velocidade do mundo nas cenas de jogo: câmera lenta (0.6) para o trailer ficar cinematográfico
    bool introTocando;
    void Update() { if (Time.frameCount % 120 == 0) Debug.Log("[TEMPO] " + TV.ToString("F2") + " jogo=" + Time.time.ToString("F2") + " escala=" + Time.timeScale.ToString("F2")); if (introTocando) return; SessaoDeJogo.MenuAberto = false; if (modo != Modo.Preto) camT.targetTexture = modo == Modo.Painel ? rtP : rtF; }
    void SetModo(Modo m) { modo = m; camT.targetTexture = m == Modo.Painel ? rtP : rtF; camPronta = false; }

    // ------------------------------------------------------------------ ambiente (sol, céu, neblina)
    Material ceuMat; bool salvo;
    void Ambiente(Color corSol, float intSol, Vector3 rotSol, Color fog, float fogDen, Color amb, float exposicao, float atmosfera)
    {
        salvo = true;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = fog; RenderSettings.fogDensity = fogDen;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = amb; RenderSettings.ambientIntensity = 1f;
        if (sol != null) { sol.color = corSol; sol.intensity = intSol; sol.transform.rotation = Quaternion.Euler(rotSol); RenderSettings.sun = sol; }
        if (ceuMat == null) { Shader sh = Shader.Find("Skybox/Procedural"); if (sh != null) ceuMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave }; }
        if (ceuMat != null) { ceuMat.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f)); ceuMat.SetFloat("_Exposure", exposicao); ceuMat.SetFloat("_AtmosphereThickness", atmosfera); ceuMat.SetColor("_GroundColor", fog); RenderSettings.skybox = ceuMat; }
    }
    void AmbienteNoite() { Ambiente(new Color(0.75f, 0.82f, 1f), 1.7f, new Vector3(42f, 200f, 0f), new Color(0.16f, 0.2f, 0.29f), 0.0045f, new Color(0.34f, 0.4f, 0.55f), 0.55f, 1.1f); }

    // ------------------------------------------------------------------ textos e fade
    IEnumerator Fade(float de, float para, float dur) { for (float k = 0f; k < 1f; k += Dt / dur) { negro = Mathf.Lerp(de, para, Mathf.SmoothStep(0f, 1f, k)); yield return null; } negro = para; }
    IEnumerator Legenda(string txt, float espera, float dur) { yield return Esp(espera); legendaTxt = txt; for (float k = 0f; k < 1f; k += Dt / 0.7f) { legendaA = k; yield return null; } legendaA = 1f; yield return Esp(Mathf.Max(0.2f, dur - 1.4f)); for (float k = 0f; k < 1f; k += Dt / 0.7f) { legendaA = 1f - k; yield return null; } legendaA = 0f; }
    const bool SEM_TITULOS = true;       // o trailer v3 deixa a luta rolar sem o título do chefe na frente (o evento de som continua sendo registrado)
    IEnumerator NomeGrande(string nome, string sub, float espera, float dur) { yield return Esp(espera); Som("nome", nome.Replace(' ', '_')); if (SEM_TITULOS) yield break; grandeTxt = nome; grandeSub = sub; for (float k = 0f; k < 1f; k += Dt / 0.8f) { grandeA = k; yield return null; } grandeA = 1f; yield return Esp(Mathf.Max(0.2f, dur - 1.6f)); for (float k = 0f; k < 1f; k += Dt / 0.8f) { grandeA = 1f - k; yield return null; } grandeA = 0f; }
    IEnumerator Cartao(string txt, float dur) { SetModo(Modo.Cartao); cartaoTxt = txt; yield return StartCoroutine(Fade(1f, 0f, 0.5f)); for (float k = 0f; k < 1f; k += Dt / 0.8f) { cartaoA = k; yield return null; } cartaoA = 1f; yield return Esp(dur); for (float k = 0f; k < 1f; k += Dt / 0.6f) { cartaoA = 1f - k; yield return null; } cartaoA = 0f; yield return StartCoroutine(Fade(0f, 1f, 0.5f)); }

    // ------------------------------------------------------------------ câmera
    void Cam(Vector3 pos, Vector3 alvo, float fov) { camPos = pos; camAlvo = alvo; camFov = fov; camPronta = true; }
    void Posicionar(Vector3 pos, Vector3 alvo, float fov)
    {
        if (!camPronta) { camPos = pos; camAlvo = alvo; camFov = fov; camPronta = true; }
        float f = 1f - Mathf.Exp(-9f * Dt);
        camPos = Vector3.Lerp(camPos, pos, f); camAlvo = Vector3.Lerp(camAlvo, alvo, f); camFov = Mathf.Lerp(camFov, fov, f);
    }
    void LateUpdate()
    {
        if (camT == null || !camPronta) return;
        Vector3 p = camPos; p.y = Mathf.Max(p.y, Chao(p) + 0.7f);
        camT.transform.position = p; Vector3 d = camAlvo - p; if (d.sqrMagnitude > 0.001f) camT.transform.rotation = Quaternion.LookRotation(d, Vector3.up); camT.fieldOfView = camFov;
    }
    // um plano de câmera: posição e alvo em função de k (0 a 1) durante "dur" segundos
    IEnumerator Plano(float dur, Func<float, Vector3> pos, Func<float, Vector3> alvo, float fov0, float fov1)
    {
        for (float t = 0f; t < dur; t += Dt) { float k = t / dur; Vector3 al = alvo(k); Cam(EvitaPedra(al, pos(k)), al, Mathf.Lerp(fov0, fov1, k)); yield return null; }
    }
    Vector3 EvitaPedra(Vector3 de, Vector3 pos)
    {
        Vector3 d = pos - de; float dc = d.magnitude; RaycastHit hit;
        if (dc > 0.5f && Physics.SphereCast(de, 0.3f, d / dc, out hit, dc, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Vida>() == null) return de + d / dc * Mathf.Max(1.5f, hit.distance - 0.25f);
        return pos;
    }

    // ------------------------------------------------------------------ prólogo: frases de lore sobre o mundo (estilo Elden Ring) e o cartão (estilo Fable)
    IEnumerator Prologo()
    {
        // reaproveita as cenas 3D da abertura do jogo; a frase de cada cena vira um cartão de texto entre as tomadas
        SetModo(Modo.Preto); introTocando = true; bool fim = false;
        IntroDoJogo.TocarPrologoTrailer(() => fim = true);
        float limite = 0f; while (!fim && limite < 140f) { limite += Dt; yield return null; }
        introTocando = false; SessaoDeJogo.MenuAberto = false;
        yield return StartCoroutine(Cartao("REÚNA O SEU GRUPO", 2.8f));
        SetModo(Modo.Preto);
    }
    Vector3 Vg(Vector3 p, float h) { p.y = Chao(p) + h; return p; }

    // ------------------------------------------------------------------ o herói em ação: ataque básico + as 3 habilidades, sem tempo parado
    static void ConfigMomento(ClasseDeHabilidade c, int skill, out char layout, out int n)
    {
        layout = 'u'; n = 1;                                              // skill = -1 é o ataque básico
        if (skill < 0) return;
        switch (c)
        {
            case ClasseDeHabilidade.Cavaleiro: if (skill == 0) { layout = 'l'; n = 3; } else if (skill == 1) { layout = 'u'; n = 1; } else { layout = 'r'; n = 7; } break;
            case ClasseDeHabilidade.Tanque: layout = 'r'; n = skill == 2 ? 5 : 6; break;
            case ClasseDeHabilidade.Maga: if (skill == 1) { layout = 'l'; n = 4; } else { layout = 'r'; n = 7; } break;
            default: layout = 'r'; n = skill == 2 ? 5 : 4; break;
        }
    }

    PlayerLocomotion CriarHeroi(FichaPersonagem ficha, Vector3 pos, Vector3 olhar, out EntradaCPU ent, out HabilidadesDoPersonagem hab)
    {
        PlayerLocomotion loco = GrupoDeAventureiros.Criar(ficha, pos, Quaternion.LookRotation(olhar)); ent = null; hab = null;
        if (loco == null) return null;
        CerebroCompanheiro cb = loco.GetComponent<CerebroCompanheiro>(); if (cb != null) DestroyImmediate(cb);
        lixo.Add(loco.gameObject); loco.name = "Herói do trailer " + ficha.nome;
        ent = loco.GetComponent<EntradaCPU>(); hab = loco.GetComponent<HabilidadesDoPersonagem>(); Vida vida = loco.GetComponent<Vida>();
        if (vida != null) vida.FiltroDeDano = (d, o) => { if (d > 0.5f) Som("hurt", tipoAtual); return 0f; };
        HabilidadesDoPersonagem hb = hab; if (hb != null) hb.AoUsar += i => Som("skill", hb.classe + " " + i);
        Luz(loco.transform, new Vector3(-2.2f, 4.2f, -3f), new Color(1f, 0.86f, 0.7f), 22f, 18f); Luz(loco.transform, new Vector3(2.4f, 3.2f, 3.2f), new Color(0.5f, 0.7f, 1f), 14f, 16f);
        return loco;
    }

    IEnumerator SegmentoHeroi(int idx)
    {
        FichaPersonagem ficha = fichas[idx]; HabilidadesDoPersonagem habPref = ficha.prefabJogador.GetComponent<HabilidadesDoPersonagem>();
        AmbienteNoite(); SetModo(Modo.Painel); negro = 1f; escalaBase = 0.6f; Time.timeScale = escalaBase;
        Vector3 eixo = Vector3.forward; EntradaCPU ent; HabilidadesDoPersonagem hab;
        PlayerLocomotion loco = CriarHeroi(ficha, centro, eixo, out ent, out hab);
        if (loco == null) { Debug.LogWarning("[Trailer] não consegui criar o herói " + idx); yield break; }
        for (int i = 0; i < 4; i++) brilhoSkill[i] = 0f;
        if (hab != null) hab.AoUsar += i => { if (i >= 0 && i < 3) brilhoSkill[i + 1] = 1.6f; };
        pFicha = ficha; pHab = habPref; pNome = ficha.nome; pTitulo = ficha.titulo; pDesc = ficha.descricao; pIndice = idx + 1; pTotal = Mathf.Min(4, fichas.Count);
        List<InimigoEsqueleto> inimigos = new List<InimigoEsqueleto>();
        GameObject[] moldes = GerenciadorDeOndas.Instancia != null ? GerenciadorDeOndas.Instancia.PrefabsDeMonstros : new GameObject[0];
        eixoDin = eixo; painelDesde = Time.time; StartCoroutine(Fade(1f, 0f, 0.8f));
        ClasseDeHabilidade cls = habPref != null ? habPref.classe : ClasseDeHabilidade.Cavaleiro; float alcance = cls == ClasseDeHabilidade.Maga ? 7f : 2.4f;

        if (cls == ClasseDeHabilidade.Curandeiro) yield return StartCoroutine(SegmentoCurandeiro(loco, ent, hab, moldes, inimigos));
        for (int m = -1; m < 3 && loco != null && cls != ClasseDeHabilidade.Curandeiro; m++)                                                   // -1 = ataque básico; 0, 1, 2 = habilidades
        {
            char layout; int n; ConfigMomento(cls, m, out layout, out n);
            loco.transform.rotation = Quaternion.LookRotation(eixo); Vector3 hp = loco.transform.position;
            for (int i = 0; i < n; i++)
            {
                Vector3 p;
                if (layout == 'u') p = hp + eixo * (cls == ClasseDeHabilidade.Maga ? 8f : 5f);
                else if (layout == 'l') p = hp + eixo * (3.6f + i * 1.9f) + Vector3.Cross(Vector3.up, eixo) * ((i % 2 == 0 ? 1 : -1) * 0.5f);
                else { float a = (i + 0.3f) * Mathf.PI * 2f / n; p = hp + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * UnityEngine.Random.Range(4.4f, 5.6f); }
                SpawnEsqueleto(moldes, inimigos, p, hp);
            }
            float tm = 0f; bool usou = false; float proxAtk = 0f, pare = 0f; int extras = 0;
            while (tm < 10f && loco != null)
            {
                tm += Time.deltaTime;
                if (m < 0 && extras < 2 && tm > 1.5f + extras * 1.7f) { extras++; Vector3 hq = loco.transform.position; SpawnEsqueleto(moldes, inimigos, hq + eixo * 5f + Vector3.Cross(Vector3.up, eixo) * (extras == 1 ? 1.6f : -1.6f), hq); pare = 0f; } AtualizarEixoHeroi(loco.transform); CameraTerceiraPessoa(loco.transform, tm);
                bool usando = hab != null && hab.Usando; InimigoEsqueleto alvo = MaisPerto(inimigos, loco.transform.position);
                if (alvo == null) { pare += Time.deltaTime; if (pare > 0.7f && (m >= 0 || extras >= 2)) break; } else pare = 0f;
                if (m >= 0 && !usou && tm > 0.4f && hab != null && !usando && alvo != null)
                {
                    usou = true; Vector3 d0 = alvo.transform.position - loco.transform.position; d0.y = 0f;
                    if (d0.sqrMagnitude > 0.1f) loco.transform.rotation = Quaternion.LookRotation(d0.normalized);
                    if (ent != null) { ent.direcaoDeMundo = Vector3.zero; ent.correr = false; }
                    hab.Demonstrar(m); StartCoroutine(CamLenta(m == 2 ? 0.3f : 0.5f, m == 2 ? 0.9f : 0.45f)); usando = true;
                }
                if (!usando && ent != null)
                {
                    if (alvo == null) { ent.direcaoDeMundo = Vector3.zero; ent.correr = false; }
                    else
                    {
                        Vector3 d = alvo.transform.position - loco.transform.position; d.y = 0f; float dist = d.magnitude;
                        if (dist > alcance) { ent.direcaoDeMundo = d.normalized; ent.correr = dist > 6f; ent.temMira = true; ent.frenteDeMira = d.normalized; }
                        else
                        {
                            ent.direcaoDeMundo = Vector3.zero; ent.correr = false; ent.temMira = true; ent.frenteDeMira = d.normalized;
                            if (Time.time >= proxAtk && (m < 0 || usou)) { ent.Atacar(); Som("atk", hab != null ? hab.classe.ToString() : ""); if (m < 0) brilhoSkill[0] = 1.2f; proxAtk = Time.time + 0.75f; StartCoroutine(Acertar(alvo, loco.transform.position, 0.4f)); }
                        }
                    }
                }
                if (m >= 0 && usou && !usando) { foreach (InimigoEsqueleto e in inimigos) if (e != null && e.Vida != null && !e.Vida.Morto && (e.transform.position - loco.transform.position).sqrMagnitude < 64f) e.Vida.ReceberDano(9999f, loco.transform.position); }
                for (int i = 0; i < 4; i++) brilhoSkill[i] = Mathf.MoveTowards(brilhoSkill[i], 0f, Dt * 0.7f);
                yield return null;
            }
            foreach (InimigoEsqueleto e in inimigos) if (e != null && e.Vida != null && !e.Vida.Morto) e.Vida.ReceberDano(9999f, loco.transform.position);
            yield return new WaitForSeconds(0.8f);
            foreach (InimigoEsqueleto e in inimigos) if (e != null) Destroy(e.gameObject); inimigos.Clear();
        }
        yield return StartCoroutine(Fade(0f, 1f, 0.7f));
        Limpar(); foreach (InimigoEsqueleto e in inimigos) if (e != null) Destroy(e.gameObject);
        SetModo(Modo.Preto);
    }

    // O Curandeiro não ataca: um aliado (o Cavaleiro) luta contra esqueletos e ele fica logo atrás, curando e fortalecendo
    IEnumerator SegmentoCurandeiro(PlayerLocomotion loco, EntradaCPU ent, HabilidadesDoPersonagem hab, GameObject[] moldes, List<InimigoEsqueleto> inimigos)
    {
        Vector3 eixo = Vector3.forward, lado = Vector3.Cross(Vector3.up, eixo); eixoDin = eixo;
        EntradaCPU entA; HabilidadesDoPersonagem habA;
        PlayerLocomotion aliado = CriarHeroi(fichas[0], centro + eixo * 4.5f, eixo, out entA, out habA);
        if (aliado == null) yield break;
        Vida va = aliado.GetComponent<Vida>(); if (va != null) va.DefinirAtual(va.Maxima * 0.35f);           // ferido: a cura vai aparecer
        float tm = 0f, proxAtk = 0.5f, proxSk = 2.2f; int sk = 0, ondas = 1;
        Vector3 pa0 = aliado.transform.position;
        for (int i = 0; i < 3; i++) SpawnEsqueleto(moldes, inimigos, pa0 + eixo * (6f + i * 1.2f) + lado * ((i - 1) * 2f), pa0);
        while (tm < 19f && loco != null && aliado != null)
        {
            tm += Time.deltaTime;
            Vector3 pa = aliado.transform.position; InimigoEsqueleto alvo = MaisPerto(inimigos, pa);
            if (alvo == null && ondas < 3) { ondas++; for (int i = 0; i < 3; i++) SpawnEsqueleto(moldes, inimigos, pa + eixo * (7f + i * 1.2f) + lado * ((i - 1) * 2.2f), pa); }
            else if (alvo == null && ondas >= 3 && tm > 14f) break;
            if (entA != null)
            {
                if (alvo == null) { entA.direcaoDeMundo = Vector3.zero; entA.correr = false; }
                else
                {
                    Vector3 d = alvo.transform.position - pa; d.y = 0f; entA.temMira = true; entA.frenteDeMira = d.normalized;
                    if (d.magnitude > 2.3f) { entA.direcaoDeMundo = d.normalized; entA.correr = d.magnitude > 6f; }
                    else { entA.direcaoDeMundo = Vector3.zero; entA.correr = false; if (Time.time >= proxAtk) { entA.Atacar(); Som("atk", "Cavaleiro"); proxAtk = Time.time + 0.85f; StartCoroutine(Acertar(alvo, pa, 0.4f)); } }
                }
            }
            // o curandeiro acompanha o aliado, 3 m atrás, de frente para a luta
            Vector3 meta = pa - eixo * 3.2f + lado * 0.8f, dm = meta - loco.transform.position; dm.y = 0f;
            bool usando = hab != null && hab.Usando;
            if (ent != null && !usando) { ent.temMira = true; ent.frenteDeMira = eixo; if (dm.magnitude > 0.9f) { ent.direcaoDeMundo = dm.normalized; ent.correr = dm.magnitude > 5f; } else { ent.direcaoDeMundo = Vector3.zero; ent.correr = false; } }
            if (hab != null && !usando && tm >= proxSk) { hab.Demonstrar(sk % 3); sk++; proxSk = tm + 3.4f; StartCoroutine(CamLenta(sk % 3 == 0 ? 0.3f : 0.55f, 0.6f)); }
            for (int i = 0; i < 4; i++) brilhoSkill[i] = Mathf.MoveTowards(brilhoSkill[i], 0f, Dt * 0.7f);
            AtualizarEixoHeroi(loco.transform); CameraTerceiraPessoa(loco.transform, tm);
            yield return null;
        }
        foreach (InimigoEsqueleto e in inimigos) if (e != null && e.Vida != null && !e.Vida.Morto) e.Vida.ReceberDano(9999f, aliado != null ? aliado.transform.position : centro);
        yield return new WaitForSeconds(0.8f);
    }

    IEnumerator Acertar(InimigoEsqueleto alvo, Vector3 origem, float depois)
    {
        yield return new WaitForSeconds(depois);
        if (alvo != null && alvo.Vida != null && !alvo.Vida.Morto) { Som("hit", "esq"); alvo.Vida.ReceberDano(alvo.Vida.Maxima * 0.55f + 10f, origem); }
    }
    void Limpar() { foreach (GameObject g in lixo) if (g != null) Destroy(g); lixo.Clear(); Time.timeScale = 1f; escalaBase = 1f; }

    void SpawnEsqueleto(GameObject[] moldes, List<InimigoEsqueleto> lista, Vector3 p, Vector3 em)
    {
        if (moldes.Length == 0) return;
        p.y = Chao(p); RaycastHit h; if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out h, 80f, ~0, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
        GameObject g = Instantiate(moldes[0], p, Quaternion.LookRotation(em - p)); InimigoEsqueleto ini = g.GetComponent<InimigoEsqueleto>();
        if (ini == null) { Destroy(g); return; }
        lista.Add(ini);                                                       // já de pé (sem o efeito de sair do chão): a ação começa na hora
    }

    void Luz(Transform pai, Vector3 pos, Color cor, float intensidade, float alcance)
    {
        GameObject g = new GameObject("Luz de cinema"); g.transform.SetParent(pai, false); g.transform.localPosition = pos;
        Light l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = cor; l.intensity = intensidade; l.range = alcance; l.shadows = LightShadows.None;
    }

    static InimigoEsqueleto MaisPerto(List<InimigoEsqueleto> l, Vector3 p)
    {
        InimigoEsqueleto m = null; float melhor = float.MaxValue;
        for (int i = l.Count - 1; i >= 0; i--) { InimigoEsqueleto e = l[i]; if (e == null) { l.RemoveAt(i); continue; } if (e.Vida == null || e.Vida.Morto) continue; float d = (e.transform.position - p).sqrMagnitude; if (d < melhor) { melhor = d; m = e; } }
        return m;
    }

    IEnumerator CamLenta(float escala, float duracao)
    {
        Time.timeScale = escala * escalaBase; yield return Esp(duracao);
        for (float k = 0f; k < 1f; k += Dt / 0.4f) { Time.timeScale = Mathf.Lerp(escala * escalaBase, escalaBase, k); yield return null; }
        Time.timeScale = escalaBase;
    }

    void AtualizarEixoHeroi(Transform h)
    {
        Vector3 f = h.forward; f.y = 0f; if (f.sqrMagnitude < 0.1f) return;
        eixoDin = Vector3.Slerp(eixoDin, f.normalized, 1f - Mathf.Exp(-2.2f * Dt));
    }
    // câmera de cima e de trás, como a do jogo em terceira pessoa (bem perto do herói)
    void CameraTerceiraPessoa(Transform h, float t)
    {
        Vector3 H = h.position + Vector3.up * 1.2f, eixo = eixoDin, lado = Vector3.Cross(Vector3.up, eixo);
        float lat = Mathf.Sin(t * 0.4f) * 0.9f + (t > 1.2f ? 2.4f : 0f);           // depois do começo a câmera passa para o lado: dá para ver o rosto e os golpes
        Vector3 pos = H - eixo * 3.0f + lado * lat + Vector3.up * (1.9f + Mathf.Sin(t * 0.3f) * 0.25f), alvo = H + eixo * 3.0f - Vector3.up * 0.2f;
        Posicionar(EvitaPedra(H, pos), alvo, 50f);
    }

    // ------------------------------------------------------------------ os chefes (tela cheia): nome em letras grandes + os 4 heróis lutando
    GameObject SpawnChefe(string tipo, Vector3 pos, Quaternion rot)
    {
        GameObject prefab = null;
        if (tipo == "guardiao" && GerenciadorDeOndas.Instancia != null) prefab = GerenciadorDeOndas.Instancia.PrefabChefe;
        else if (tipo == "cavaleiro" && EncontroDoCastelo.Instancia != null) prefab = EncontroDoCastelo.Instancia.prefabCavaleiro;
        else if (tipo == "ladrao" && EncontroDoCastelo.Instancia != null) prefab = EncontroDoCastelo.Instancia.prefabLadrao;
        else if (tipo == "fera") { foreach (EncontroDoPantano ep in FindObjectsByType<EncontroDoPantano>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (ep != null && ep.prefabAngler != null) { prefab = ep.prefabAngler; break; } }
        if (prefab == null) { Debug.LogWarning("[Trailer] sem prefab do chefe " + tipo); return null; }
        GameObject g = Instantiate(prefab, pos, rot); lixo.Add(g); g.SetActive(true);
        Debug.Log("[Trailer] chefe " + tipo + ": " + g.name + " em " + pos.ToString("F0") + " renderers=" + g.GetComponentsInChildren<Renderer>(true).Length + " escala=" + g.transform.localScale.ToString("F2"));
        tipoAtual = tipo;
        Vida v = g.GetComponent<Vida>(); if (v != null) { v.DefinirMaxima(60000f); var prev = v.FiltroDeDano; v.FiltroDeDano = (d, o) => { if (d > 0.5f) Som("bosshit", tipo); return prev != null ? prev(d, o) : d; }; }
        ChefeGuardiao cg = g.GetComponent<ChefeGuardiao>(); if (cg != null) cg.Surgir(3.2f, 6f);            // o Guardião sai do chão, ruge e luta como no jogo
        if (tipo != "fera") foreach (MonoBehaviour mb in g.GetComponents<MonoBehaviour>())      // a Fera acorda sozinha depois de emergir do lago
        {
            MethodInfo mi = mb.GetType().GetMethod("Despertar", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (mi != null) { try { mi.Invoke(mb, null); } catch (Exception ex) { Debug.LogWarning("[Trailer] Despertar falhou: " + ex.Message); } }
        }
        return g;
    }

    Vector3 Piso(Vector3 p)
    {
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 14f, Vector3.down, out h, 90f, ~0, QueryTriggerInteraction.Ignore)) p.y = h.point.y; else p.y = Chao(p);
        return p;
    }

    IEnumerator Fase2Guardiao(GameObject boss)
    {
        yield return Esp(14f); if (boss == null) yield break;
        Vida v = boss.GetComponent<Vida>(); if (v != null) v.DefinirAtual(v.Maxima * 0.45f); Som("fase2", "guardiao");                  // ele racha: começa a 2ª fase
        Ambiente(new Color(1f, 0.45f, 0.28f), 1.7f, new Vector3(30f, 200f, 0f), new Color(0.22f, 0.07f, 0.05f), 0.007f, new Color(0.42f, 0.16f, 0.12f), 0.5f, 1.3f);
        yield return Esp(1.5f); StartCoroutine(NomeGrande("A FÚRIA DE PEDRA", "O Guardião desperta por inteiro", 0f, 3.6f));
    }

    IEnumerator FeraEmerge(GameObject g, Vector3 ponto, float espera)
    {
        ChefeAngler c = g != null ? g.GetComponent<ChefeAngler>() : null; if (c == null) yield break;
        yield return StartCoroutine(c.Emergir(ponto)); c.Despertar();
    }

    IEnumerator SegmentoChefe(string nome, string sub, string tipo, Color fogCor, float fogDen)
    {
        SetModo(Modo.Cheio); negro = 1f; chaoExtra = 0f; escalaBase = 0.6f; Time.timeScale = escalaBase;
        Ambiente(new Color(0.78f, 0.84f, 1f), 2.3f, new Vector3(40f, 210f, 0f), fogCor, fogDen, new Color(0.46f, 0.5f, 0.62f), 0.7f, 1.2f);
        // cada chefe luta no SEU lugar: o Guardião no cemitério, o Cavaleiro e o Ladrão no castelo, a Fera na margem do lago
        Vector3 hPos = centro, bPos = centro + Vector3.forward * 13f; float atraso = 0f; EncontroDoPantano lago = null;
        if (tipo == "cavaleiro" || tipo == "ladrao")
        {
            EncontroDoCastelo ec = EncontroDoCastelo.Instancia;
            if (ec != null && ec.estrutura != null)
            {
                bPos = ec.PontoLocal(0f, tipo == "cavaleiro" ? -2f : -31f); hPos = ec.PontoLocal(0f, tipo == "cavaleiro" ? 11f : -18f);
                chaoExtra = Mathf.Max(0f, hPos.y - Chao(hPos));
            }
        }
        else if (tipo == "fera")
        {
            foreach (EncontroDoPantano ep in FindObjectsByType<EncontroDoPantano>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (ep != null) { lago = ep; break; }
            if (lago != null)
            {
                bPos = lago.PontoDaMargem(lago.CentroLago + Vector3.right * 60f); Vector3 fora = bPos - lago.CentroLago; fora.y = 0f; fora.Normalize();
                hPos = bPos + fora * 13f; hPos.y = Chao(hPos); atraso = 6f;
            }
        }
        Vector3 eixo = bPos - hPos; eixo.y = 0f; eixo = eixo.sqrMagnitude > 1f ? eixo.normalized : Vector3.forward; Vector3 lado = Vector3.Cross(Vector3.up, eixo);
        GameObject boss;
        if (tipo == "fera" && lago != null) { boss = SpawnChefe(tipo, new Vector3(lago.CentroLago.x, lago.NivelAgua, lago.CentroLago.z), Quaternion.LookRotation(eixo)); if (boss != null) { ChefeAngler ca = boss.GetComponent<ChefeAngler>(); if (ca != null) ca.Preparar(lago.CentroLago, lago.NivelAgua, 88f, 4); StartCoroutine(FeraEmerge(boss, bPos, 0f)); } }
        else boss = SpawnChefe(tipo, bPos, Quaternion.LookRotation(-eixo));
        if (boss == null) { yield return StartCoroutine(Fade(1f, 1f, 0.1f)); SetModo(Modo.Preto); chaoExtra = 0f; yield break; }
        List<PlayerLocomotion> hs = new List<PlayerLocomotion>(); List<EntradaCPU> es = new List<EntradaCPU>(); List<HabilidadesDoPersonagem> habs = new List<HabilidadesDoPersonagem>();
        for (int i = 0; i < Mathf.Min(4, fichas.Count); i++)
        {
            EntradaCPU e; HabilidadesDoPersonagem h; Vector3 p = hPos + lado * ((i - 1.5f) * 2.4f); p = tipo == "cavaleiro" || tipo == "ladrao" ? Piso(p) : Vg(p, 0f);
            PlayerLocomotion l = CriarHeroi(fichas[i], p, eixo, out e, out h); if (l != null) { hs.Add(l); es.Add(e); habs.Add(h); StartCoroutine(Lutar(l, e, h, fichas[i], boss, i, eixo, atraso)); }
        }
        eixoDin = eixo; StartCoroutine(Fade(1f, 0f, 1.0f)); StartCoroutine(NomeGrande(nome, sub, 0.8f, 4.6f));
        if (tipo == "guardiao") StartCoroutine(Fase2Guardiao(boss));
        float zm = tipo == "cavaleiro" || tipo == "ladrao" ? 0.62f : 1f, ab = tipo == "cavaleiro" || tipo == "ladrao" ? 0.55f : 1f;      // chefes do tamanho de uma pessoa: câmera mais perto
        Func<Vector3> B = () => boss != null ? boss.transform.position : bPos;
        Func<Vector3> Cm = () => { Vector3 s = Vector3.zero; int n = 0; foreach (PlayerLocomotion l in hs) if (l != null) { s += l.transform.position; n++; } return n > 0 ? s / n : hPos; };
        // 1) plano baixo, olhando o chefe de baixo para cima, aproximando devagar (com o nome)
        yield return StartCoroutine(Plano(5f, k => Vg(B() - (eixo * Mathf.Lerp(10f, 6.5f, k) + lado * Mathf.Lerp(6f, 3.5f, k)) * zm, Mathf.Lerp(0.5f, 1.3f, k)), k => B() + Vector3.up * Mathf.Lerp(2.8f, 3.4f, k) * ab, 40f, 34f));
        // 2..7) luta com a câmera SEMPRE em movimento: varredura baixa, perseguição, órbita subindo, guindaste, ombro do herói, contra-plano de trás do chefe
        Func<float, Vector3, Vector3> Hh = (h, q) => new Vector3(q.x, Cm().y + h, q.z);
        Func<Vector3> H1 = () => hs.Count > 1 && hs[1] != null ? hs[1].transform.position : Cm();
        yield return StartCoroutine(Plano(4.2f, k => Hh(Mathf.Lerp(1.0f, 2.0f, k), B() - eixo * Mathf.Lerp(11f, 6.5f, k) * zm + lado * Mathf.Sin(Mathf.Lerp(-1.1f, 1.1f, k)) * 9f * zm), k => B() + Vector3.up * 3f * ab, 50f, 40f));
        yield return StartCoroutine(Plano(3.8f, k => Hh(Mathf.Lerp(1.0f, 1.7f, k), Cm() - eixo * Mathf.Lerp(6f, -1.5f, k) * zm + lado * Mathf.Lerp(-3f, 2f, k)), k => B() + Vector3.up * 2.2f * ab, 64f, 50f));
        yield return StartCoroutine(Plano(5f, k => { Vector3 m = Vector3.Lerp(Cm(), B(), 0.6f); float a = 0.2f + k * 3.4f, r = Mathf.Lerp(9f, 13f, k) * zm; return Hh(Mathf.Lerp(1.5f, 6.5f, k) * zm, m + new Vector3(Mathf.Sin(a) * r, 0f, -Mathf.Cos(a) * r)); }, k => Vector3.Lerp(Cm(), B(), 0.55f) + Vector3.up * 1.6f * ab, 52f, 46f));
        yield return StartCoroutine(Plano(3.4f, k => Hh(Mathf.Lerp(0.8f, 12f, k) * zm, B() - eixo * Mathf.Lerp(5f, 16f, k) * zm + lado * 4f * zm), k => Vector3.Lerp(B(), Cm(), Mathf.Lerp(0.1f, 0.4f, k)) + Vector3.up * Mathf.Lerp(2.2f, 0.6f, k), 44f, 58f));
        yield return StartCoroutine(Plano(3.4f, k => { Vector3 h0 = H1(); return Hh(1.7f, Vector3.Lerp(h0 - eixo * 3f + lado * 1.8f, h0 + eixo * 2.2f - lado * 2.2f, k)); }, k => B() + Vector3.up * 2.4f * ab, 56f, 44f));
        yield return StartCoroutine(Plano(3.2f, k => Hh(Mathf.Lerp(2f, 3.5f, k), B() + eixo * Mathf.Lerp(7f, 3f, k) * zm + lado * Mathf.Lerp(-4f, 4f, k) * zm), k => Cm() + Vector3.up * 1.4f, 56f, 50f));
        if (tipo == "guardiao" || tipo == "fera")       // as lutas mais longas: segunda fase do Guardião / a Fera enfurecida
        {
            yield return StartCoroutine(Plano(4f, k => Hh(2.2f, Vector3.Lerp(B() + lado * -14f * zm - eixo * 3f, B() + lado * 14f * zm - eixo * 3f, k)), k => B() + Vector3.up * 3.4f, 46f, 40f));
            yield return StartCoroutine(Plano(4.2f, k => { Vector3 m = Vector3.Lerp(Cm(), B(), 0.5f); float a = 2.2f + k * 2.4f; return Hh(Mathf.Lerp(3f, 8f, k), m + new Vector3(Mathf.Sin(a) * 14f, 0f, -Mathf.Cos(a) * 14f)); }, k => Vector3.Lerp(Cm(), B(), 0.5f) + Vector3.up * 1.8f, 52f, 46f));
        }
        yield return StartCoroutine(Fade(0f, 1f, 0.6f));
        Limpar(); SetModo(Modo.Preto); chaoExtra = 0f;
    }

    // um herói que luta com o chefe, cada um no seu papel:
    // cavaleiro e tanque vão para cima (corpo a corpo); a maga fica a média distância; o curandeiro fica atrás do grupo, curando
    IEnumerator Lutar(PlayerLocomotion loco, EntradaCPU ent, HabilidadesDoPersonagem hab, FichaPersonagem ficha, GameObject boss, int ordem, Vector3 eixo, float atraso)
    {
        HabilidadesDoPersonagem molde = ficha.prefabJogador.GetComponent<HabilidadesDoPersonagem>();
        ClasseDeHabilidade cls = molde != null ? molde.classe : ClasseDeHabilidade.Cavaleiro;
        bool maga = cls == ClasseDeHabilidade.Maga, corpo = cls == ClasseDeHabilidade.Cavaleiro || cls == ClasseDeHabilidade.Tanque, cura = !maga && !corpo;
        Vector3 lado = Vector3.Cross(Vector3.up, eixo);
        yield return Esp(atraso);
        float proxAtk = Time.time + 0.4f * ordem, proxSkill = Time.time + (cura ? 1.8f : 3f + ordem * 1.3f); int sk = ordem % 3;
        while (loco != null && boss != null)
        {
            if (hab != null && hab.Usando) { yield return null; continue; }
            Vector3 bp = boss.transform.position, d = bp - loco.transform.position; d.y = 0f; float dist = d.magnitude;
            Vector3 meta = corpo ? bp : (cura ? bp - eixo * 9f + lado * 3.5f : bp - eixo * 10f - lado * 3.5f);
            Vector3 dm = meta - loco.transform.position; dm.y = 0f; float dmeta = dm.magnitude, parar = corpo ? 3.2f : 1.3f;
            if (ent != null)
            {
                ent.temMira = true; ent.frenteDeMira = d.normalized;
                if (Time.time >= proxSkill && hab != null && dist < 18f) { loco.transform.rotation = Quaternion.LookRotation(d.normalized); ent.direcaoDeMundo = Vector3.zero; hab.Demonstrar(sk % 3); sk++; proxSkill = Time.time + (cura ? 3.2f : 4.5f); }
                else if (dmeta > parar) { ent.direcaoDeMundo = dm.normalized; ent.correr = dmeta > 7f; }
                else { ent.direcaoDeMundo = Vector3.zero; ent.correr = false; if (Time.time >= proxAtk) { ent.Atacar(); Som("atk", hab != null ? hab.classe.ToString() : ""); proxAtk = Time.time + (cura ? 1.6f : 0.8f); } }
            }
            yield return null;
        }
    }

    // ------------------------------------------------------------------ o dragão: fase 1 (fogo) e fase 2 (a fúria sombria)
    IEnumerator SegmentoDragao()
    {
        SetModo(Modo.Cheio); negro = 1f; escalaBase = 0.7f; Time.timeScale = escalaBase;
        Ambiente(new Color(1f, 0.6f, 0.35f), 2.4f, new Vector3(24f, 0f, 0f), new Color(0.3f, 0.14f, 0.1f), 0.0035f, new Color(0.5f, 0.3f, 0.25f), 0.9f, 2.4f);
        DragaoDistante origem = FindAnyObjectByType<DragaoDistante>(FindObjectsInactive.Include); GameObject dg = null; DragaoDistante dd = null;
        if (origem != null && origem.modelo != null)
        {
            dg = new GameObject("Drakthar do trailer"); dg.transform.position = centro; lixo.Add(dg); dd = dg.AddComponent<DragaoDistante>();
            dd.modelo = origem.modelo; dd.animVoo = origem.animVoo; dd.animPlanar = origem.animPlanar; dd.animSopro = origem.animSopro; dd.rotacaoDoModelo = origem.rotacaoDoModelo; dd.orientarAutomaticamente = origem.orientarAutomaticamente;
            dd.comprimentoDoModelo = 52f; dd.corDasEscamas = origem.corDasEscamas; dd.corDaMembrana = origem.corDaMembrana; dd.tamanho = origem.tamanho; dd.materialParticula = origem.materialParticula;
            dd.centroDoVoo = Vector3.zero; dd.raiosDoVoo = new Vector2(34f, 26f); dd.alturaDoVoo = 24f; dd.velocidadeDoVoo = 15f; dd.intervaloDoSopro = 3.8f;
        }
        List<PlayerLocomotion> hs = new List<PlayerLocomotion>();
        for (int i = 0; i < Mathf.Min(4, fichas.Count); i++) { EntradaCPU e; HabilidadesDoPersonagem h; PlayerLocomotion l = CriarHeroi(fichas[i], centro + Vector3.right * ((i - 1.5f) * 2.6f) - Vector3.forward * 3f, Vector3.forward, out e, out h); if (l != null) hs.Add(l); }
        Func<Vector3> D = () => { if (dg == null) return centro + Vector3.up * 40f; Transform c = dg.transform.Find("Dragao 3D"); return c != null ? c.position : centro + Vector3.up * 40f; };
        StartCoroutine(Fade(1f, 0f, 0.9f)); StartCoroutine(NomeGrande("DRAKTHAR, O DEVASTADOR", "O dragão que incendiou Valverde", 0.8f, 5f));
        yield return StartCoroutine(Plano(5.5f, k => Vg(centro - Vector3.forward * 16f + Vector3.right * 4f, Mathf.Lerp(0.7f, 1.6f, k)), k => Vector3.Lerp(centro + Vector3.up * 3f, D(), Suave(k)), 58f, 44f));       // de baixo: o dragão cruza o céu
        yield return StartCoroutine(Plano(4.5f, k => centro + new Vector3(Mathf.Lerp(-16f, 16f, k), 6f, -14f), k => D(), 56f, 50f));
        float proxRaio = 0f;
        // fase 2: a fúria sombria (o céu escurece e raios caem)
        flashBranco = 0.9f; Ambiente(new Color(0.9f, 0.35f, 1f), 1.6f, new Vector3(26f, 0f, 0f), new Color(0.12f, 0.05f, 0.15f), 0.006f, new Color(0.34f, 0.16f, 0.42f), 0.4f, 2.8f);
        if (dd != null) { dg.transform.localScale = Vector3.one * 1.25f; dd.intervaloDoSopro = 2.2f; dd.velocidadeDoVoo = 18f; }
        StartCoroutine(NomeGrande("A FÚRIA SOMBRIA", "Segunda fase", 0.2f, 3.6f));
        for (float t = 0f; t < 8f; t += Time.deltaTime)
        {
            float k = t / 8f; Vector3 cm = Vector3.zero; int n = 0; foreach (PlayerLocomotion l in hs) if (l != null) { cm += l.transform.position; n++; } if (n > 0) cm /= n; else cm = centro;
            if (k < 0.45f) Cam(Vg(cm - Vector3.forward * 9f + Vector3.right * Mathf.Lerp(-4f, 4f, k * 2f), 1.2f), Vector3.Lerp(cm + Vector3.up * 2f, D(), 0.55f), 52f);
            else Cam(cm + new Vector3(Mathf.Lerp(8f, -8f, (k - 0.45f) / 0.55f), 14f, -18f), D(), 50f);
            if (Time.time >= proxRaio) { proxRaio = Time.time + UnityEngine.Random.Range(0.5f, 1.2f); Vector3 q = centro + UnityEngine.Random.insideUnitSphere * 26f; q.y = Chao(q); Som("raio", ""); FxCena.Raio(q + Vector3.up * 150f, q, 2.4f, 0.22f, new Color(0.85f, 0.55f, 1f, 1f)); }
            flashBranco = Mathf.MoveTowards(flashBranco, 0f, Dt * 1.6f);
            yield return null;
        }
        yield return StartCoroutine(Fade(0f, 1f, 0.7f));
        Limpar(); SetModo(Modo.Preto);
    }
    float flashBranco; static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // ------------------------------------------------------------------ título final (estilo Elden Ring / Fable)
    IEnumerator Final()
    {
        AmbienteNoite(); SetModo(Modo.Cheio); negro = 1f;
        StartCoroutine(Plano(17f, k => Vg(centro + new Vector3(Mathf.Sin(k * 0.7f), 0f, Mathf.Cos(k * 0.7f)) * Mathf.Lerp(70f, 45f, k), Mathf.Lerp(30f, 18f, k)), k => Vg(centro, 8f), 46f, 40f));
        StartCoroutine(Fade(1f, 0f, 1.5f)); yield return Esp(1.2f);
        grandeTxt = "TRIAL OF THE FORSAKEN"; grandeSub = "Em breve"; for (float k = 0f; k < 1f; k += Dt / 1.6f) { grandeA = k; yield return null; } grandeA = 1f;
        yield return Esp(3.5f);
        legendaTxt = "Sem tostão no bolso. O prêmio? Um cabaré."; for (float k = 0f; k < 1f; k += Dt / 0.9f) { legendaA = k; yield return null; } legendaA = 1f;
        yield return Esp(3.5f);
        for (float k = 0f; k < 1f; k += Dt / 1.2f) { grandeA = 1f - k; legendaA = 1f - k; yield return null; } grandeA = legendaA = 0f;
        yield return StartCoroutine(Fade(0f, 1f, 0.8f)); SetModo(Modo.Preto);
    }

    // ------------------------------------------------------------------ a tela: jogabilidade à esquerda, painel à direita
    static Texture2D Grad(bool horizontal)
    {
        Texture2D t = new Texture2D(horizontal ? 256 : 1, horizontal ? 1 : 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int i = 0; i < 256; i++) t.SetPixel(horizontal ? i : 0, horizontal ? 0 : i, new Color(1f, 1f, 1f, i / 255f)); t.Apply(); return t;
    }
    void R(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
    void RR(Rect r, float rad, Color c) { GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(rad, rad, rad, rad)); }
    void RRc(Rect r, Vector4 rad, Color c) { GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, rad); }
    void Aro(Rect r, float rad, float esp, Color c) { GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, new Vector4(esp, esp, esp, esp), new Vector4(rad, rad, rad, rad)); }
    void Brilho(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, IconesDeHabilidade.Brilho); GUI.color = Color.white; }
    GUIStyle Est(Font f, float tam, TextAnchor al, bool quebra = false, FontStyle fs = FontStyle.Normal) { return new GUIStyle { font = f, fontSize = Mathf.RoundToInt(tam), alignment = al, wordWrap = quebra, fontStyle = fs }; }
    void Texto(Rect r, string t, Font f, float tam, TextAnchor al, Color cor, bool quebra = false, FontStyle fs = FontStyle.Normal, bool sombra = true)
    {
        GUIStyle s = Est(f, tam, al, quebra, fs);
        if (sombra) { s.normal.textColor = new Color(0f, 0f, 0f, 0.85f * cor.a); GUI.Label(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), t, s); }
        s.normal.textColor = cor; GUI.Label(r, t, s);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || introTocando) return;
        float W = Screen.width, H = Screen.height, e = H / 1080f; GUI.depth = -9500;
        R(new Rect(0, 0, W, H), Color.black);
        if (modo == Modo.Painel && rtP != null)
        {
            float gw = H * (RTW / (float)RTH);
            GUI.DrawTexture(new Rect(0, 0, gw, H), rtP, ScaleMode.StretchToFill, false);
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, H * 0.7f, gw, H * 0.3f), gradVInv, ScaleMode.StretchToFill, false); GUI.color = Color.white;
            DesenharPainel(gw, W, H, e);
        }
        else if (modo == Modo.Cheio && rtF != null)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), rtF, ScaleMode.StretchToFill, false);
            float b = Mathf.Max(0f, (H - W / 2.39f) * 0.5f); R(new Rect(0, 0, W, b), Color.black); R(new Rect(0, H - b, W, b), Color.black);       // barras de cinema 2,39:1
            GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(new Rect(0, H * 0.78f, W, H * 0.22f), gradVInv, ScaleMode.StretchToFill, false); GUI.color = Color.white;
        }
        else if (modo == Modo.Cartao)
        {
            GUI.color = new Color(0.05f, 0.14f, 0.17f, 1f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = new Color(0.12f, 0.27f, 0.31f, 0.85f); GUI.DrawTexture(new Rect(0, 0, W, H), gradV, ScaleMode.StretchToFill, false); GUI.color = Color.white;
            Brilho(new Rect(W * 0.3f, H * 0.25f, W * 0.4f, H * 0.5f), new Color(0.5f, 0.85f, 0.8f, 0.08f));
        }
        // textos por cima de tudo (menos do fade)
        if (legendaA > 0.01f && !string.IsNullOrEmpty(legendaTxt))
        {
            float b = modo == Modo.Cheio ? Mathf.Max(0f, (H - W / 2.39f) * 0.5f) : 0f; float cx = modo == Modo.Painel ? H * (RTW / (float)RTH) * 0.5f : W * 0.5f, lw = modo == Modo.Painel ? H * (RTW / (float)RTH) * 0.84f : W * 0.6f;
            Texto(new Rect(cx - lw * 0.5f, H - b - 150f * e, lw, 110f * e), legendaTxt, fFell, 40f * e, TextAnchor.MiddleCenter, new Color(0.96f, 0.93f, 0.85f, legendaA), true);
        }
        if (grandeA > 0.01f)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.5f * grandeA); GUI.DrawTexture(new Rect(0, H * 0.3f, W, H * 0.4f), gradH, ScaleMode.StretchToFill, false); GUI.color = Color.white;
            float tf = 124f * e; while (tf > 40f * e && Est(fCinzel, tf, TextAnchor.MiddleCenter).CalcSize(new GUIContent(grandeTxt ?? "")).x > W * 0.86f) tf *= 0.94f;
            Texto(new Rect(0, H * 0.5f - 100f * e, W, 160f * e), grandeTxt, fCinzel, tf, TextAnchor.MiddleCenter, new Color(0.98f, 0.9f, 0.7f, grandeA));
            Ornamento(W * 0.5f - 230f * e, H * 0.5f + 78f * e, 460f * e, e, new Color(0.95f, 0.85f, 0.6f, grandeA));
            Texto(new Rect(0, H * 0.5f + 92f * e, W, 60f * e), grandeSub, fFell, 40f * e, TextAnchor.MiddleCenter, new Color(0.92f, 0.88f, 0.78f, grandeA), false, FontStyle.Italic);
        }
        if (cartaoA > 0.01f) Texto(new Rect(W * 0.08f, H * 0.5f - 160f * e, W * 0.84f, 320f * e), cartaoTxt, fCinzel, 84f * e, TextAnchor.MiddleCenter, new Color(0.9f, 0.93f, 0.86f, cartaoA), true);
        if (negro > 0.002f) R(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, negro));
    }

    void DesenharPainel(float x0, float W, float H, float e)
    {
        float a = Mathf.SmoothStep(0f, 1f, (Time.time - painelDesde) / 0.9f), px = x0 + (1f - a) * 60f * e, pw = W - x0;
        Color ouro = pFicha != null ? pFicha.corDestaque : new Color(0.86f, 0.68f, 0.34f); ouro.a = 1f; Color ouroC = Color.Lerp(ouro, new Color(1f, 0.93f, 0.7f), 0.5f);
        R(new Rect(x0, 0, pw, H), new Color(0.035f, 0.03f, 0.04f, 1f));
        GUI.color = new Color(ouro.r, ouro.g, ouro.b, 0.12f * a); GUI.DrawTexture(new Rect(x0, 0, pw, H * 0.5f), gradV, ScaleMode.StretchToFill, false); GUI.color = Color.white;
        R(new Rect(x0, 0, 3f * e, H), new Color(ouro.r, ouro.g, ouro.b, 0.8f * a));
        float m = 54f * e, y = 46f * e, w = pw - 2f * m; px += m;
        Texto(new Rect(px, y, w, 34f * e), "PERSONAGEM  " + pIndice + " / " + pTotal, fCinzel, 22f * e, TextAnchor.MiddleLeft, new Color(ouro.r, ouro.g, ouro.b, a), false, FontStyle.Normal, false);
        float nf = 78f * e; while (nf > 30f * e && Est(fCinzel, nf, TextAnchor.MiddleLeft).CalcSize(new GUIContent(pNome ?? "")).x > w) nf *= 0.94f;      // o nome sempre cabe
        Texto(new Rect(px, y + 38f * e, w, 100f * e), pNome, fCinzel, nf, TextAnchor.MiddleLeft, new Color(0.98f, 0.93f, 0.8f, a));
        Ornamento(px, y + 148f * e, 340f * e, e, new Color(ouroC.r, ouroC.g, ouroC.b, a));
        Texto(new Rect(px, y + 162f * e, w, 42f * e), pTitulo, fFell, 34f * e, TextAnchor.MiddleLeft, new Color(0.93f, 0.89f, 0.78f, a), false, FontStyle.Italic);
        float dh = Est(fFell, 27f * e, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(pDesc ?? ""), w);
        Texto(new Rect(px, y + 212f * e, w, dh + 8f * e), pDesc ?? "", fFell, 27f * e, TextAnchor.UpperLeft, new Color(0.9f, 0.87f, 0.8f, a), true);
        // atributos
        float ay = y + 212f * e + dh + 22f * e; string[] nomes = { "FORÇA", "DEFESA", "VELOCIDADE", "ALCANCE" };
        int[] vals = pFicha != null ? new[] { pFicha.forca, pFicha.defesa, pFicha.velocidade, pFicha.alcance } : new int[4];
        for (int i = 0; i < 4; i++)
        {
            float yy = ay + i * 28f * e; Texto(new Rect(px, yy, 190f * e, 26f * e), nomes[i], fCinzel, 17f * e, TextAnchor.MiddleLeft, new Color(0.82f, 0.78f, 0.68f, a), false, FontStyle.Normal, false);
            for (int s = 0; s < 10; s++) { float prog = Mathf.Clamp01((a - 0.15f * i - 0.04f * s) * 4f); RR(new Rect(px + 190f * e + s * 25f * e, yy + 6f * e, 20f * e, 14f * e), 3f * e, s < vals[i] ? new Color(ouro.r, ouro.g, ouro.b, a * prog) : new Color(1f, 1f, 1f, 0.1f * a)); }
        }
        // ataque básico + habilidades (a linha acende quando a ação acontece na tela)
        float hy = ay + 4f * 28f * e + 20f * e;
        Texto(new Rect(px, hy, w, 30f * e), "COMBATE", fCinzel, 20f * e, TextAnchor.MiddleLeft, new Color(ouro.r, ouro.g, ouro.b, a), false, FontStyle.Normal, false);
        R(new Rect(px, hy + 34f * e, w, 1.5f * e), new Color(ouro.r, ouro.g, ouro.b, 0.45f * a));
        int ini0 = pHab != null && pHab.classe == ClasseDeHabilidade.Curandeiro ? 1 : 0;      // o Curandeiro só cura: sem linha de ataque básico
        for (int i = ini0; i < 4; i++)
        {
            DadosDeHabilidade d = i == 0 ? null : (pHab != null ? pHab.Dados(i - 1) : null); if (i > 0 && d == null) continue;
            float yy = hy + 44f * e + (i - ini0) * 100f * e, g = Mathf.Clamp01(brilhoSkill[i]); Rect box = new Rect(px, yy, w, 92f * e);
            RR(box, 14f * e, new Color(ouro.r, ouro.g, ouro.b, (0.08f + 0.35f * g) * a)); Aro(box, 14f * e, (1.5f + 2.5f * g) * e, new Color(ouro.r, ouro.g, ouro.b, (0.35f + 0.65f * g) * a));
            if (g > 0.01f) Brilho(new Rect(box.x - 40f * e, box.y - 40f * e, 160f * e, box.height + 80f * e), new Color(ouro.r, ouro.g, ouro.b, 0.5f * g * a));
            Rect ic = new Rect(box.x + 12f * e, box.y + 11f * e, 70f * e, 70f * e); RR(ic, 10f * e, new Color(ouro.r * 0.4f, ouro.g * 0.4f, ouro.b * 0.4f, a));
            if (i == 0) { RR(new Rect(ic.center.x - 13f * e, ic.y + 10f * e, 26f * e, 50f * e), 13f * e, new Color(1f, 0.96f, 0.85f, a)); RRc(new Rect(ic.center.x - 11f * e, ic.y + 12f * e, 11f * e, 20f * e), new Vector4(11f * e, 2f * e, 2f * e, 2f * e), new Color(ouro.r, ouro.g, ouro.b, a)); R(new Rect(ic.center.x - 1f * e, ic.y + 14f * e, 2f * e, 14f * e), new Color(0.2f, 0.18f, 0.15f, a)); }
            else { GUI.color = new Color(1f, 0.97f, 0.88f, a); GUI.DrawTexture(new Rect(ic.x + 7f * e, ic.y + 7f * e, ic.width - 14f * e, ic.height - 14f * e), IconesDeHabilidade.Obter(pHab.classe, i - 1), ScaleMode.ScaleToFit, true); GUI.color = Color.white; }
            string nome = i == 0 ? "ATAQUE BÁSICO" : d.nome.ToUpperInvariant(), chave = i == 0 ? "CLIQUE" : Comandos.Dica(Acao.Habilidade1 + i - 1), desc = i == 0 ? "Botão esquerdo do mouse." : d.descricao;
            Texto(new Rect(box.x + 96f * e, box.y + 6f * e, w - 200f * e, 36f * e), nome, fCinzel, 25f * e, TextAnchor.MiddleLeft, Color.Lerp(new Color(0.96f, 0.92f, 0.82f, a), new Color(1f, 0.93f, 0.6f, a), g), false, FontStyle.Normal, false);
            Texto(new Rect(box.xMax - 100f * e, box.y + 10f * e, 86f * e, 30f * e), chave, fCinzel, 19f * e, TextAnchor.MiddleRight, new Color(ouro.r, ouro.g, ouro.b, a), false, FontStyle.Normal, false);
            Texto(new Rect(box.x + 96f * e, box.y + 42f * e, w - 112f * e, 48f * e), desc, fFell, 20f * e, TextAnchor.UpperLeft, new Color(0.85f, 0.82f, 0.74f, a), true, FontStyle.Normal, false);
        }
    }

    void Ornamento(float x, float y, float w, float e, Color c)
    {
        R(new Rect(x, y, w * 0.46f, 2f * e), new Color(c.r, c.g, c.b, c.a * 0.7f)); R(new Rect(x + w * 0.54f, y, w * 0.46f, 2f * e), new Color(c.r, c.g, c.b, c.a * 0.7f));
        Matrix4x4 m = GUI.matrix; GUIUtility.RotateAroundPivot(45f, new Vector2(x + w * 0.5f, y + e)); R(new Rect(x + w * 0.5f - 7f * e, y + e - 7f * e, 14f * e, 14f * e), c); GUI.matrix = m;
    }

    void OnDestroy()
    {
        if (rtP != null) { rtP.Release(); Destroy(rtP); } if (rtF != null) { rtF.Release(); Destroy(rtF); }
        FxCena.CamOverride = null; Time.timeScale = 1f;
    }
}
