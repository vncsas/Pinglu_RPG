using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A praça do cemitério e a cena de abertura do chefe: o chão treme, o monumento central racha e desaba,
// o piso cai dentro de um buraco gigante e o Guardião das Lápides sobe lá de baixo.
// As peças (monumento em pedaços, piso em placas, buraco) são montadas pelo menu Pinglu > 66.
// Tudo aqui é movido por código: terremoto, poeira, brasas, som grave, câmera de cinema e tarjas pretas.
[DefaultExecutionOrder(1000)]
public class PracaDoGuardiao : MonoBehaviour
{
    public static PracaDoGuardiao Instancia { get; private set; }

    [Header("Montado pelo menu Pinglu > 66")]
    public Transform[] pedacosMonumento;
    public Transform[] pedacosPiso;
    public Vector3[] posMonumento, posPiso;          // onde cada peça fica de fábrica (posição local)
    public Quaternion[] rotMonumento, rotPiso;
    public GameObject cratera;
    public Renderer brilhoDoFundo;
    public Light luzDoBuraco;
    public Material matPoeira, matBrasa, matPedra;
    public float raioDoBuraco = 10f, profundidade = 4.5f, raioDoFundo = 2.6f, topoDoPiso = 0f;

    // ------------------------------------------------------------ estado
    private class Corpo
    {
        public Transform t; public Vector3 v, w; public float raio; public bool pedrinha, pousou; public float fim = -1f; public bool ativo = true;
    }
    private readonly List<Corpo> corpos = new List<Corpo>();
    private ParticleSystem psPoeira, psBrasa;
    private AudioSource som, somBatida;
    private AudioClip clipRumble, clipBatida;
    private Material emissao;
    private bool buracoAberto, cineAtiva;
    private float saida, barras, barrasAlvo, flash, tituloAlfa;
    private Vector3 cinePos; private Quaternion cineRot = Quaternion.identity;
    private Vector3[] eixoPiso;
    private bool ocupada;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Zerar() { Instancia = null; }

    public bool Ocupada { get { return ocupada; } }
    public Vector3 Centro() { return transform.TransformPoint(new Vector3(0f, topoDoPiso, 0f)); }
    // onde o Guardião fica de pé quando termina de subir (no fundo do buraco)
    public Vector3 PontoDoChefe() { return transform.TransformPoint(new Vector3(0f, topoDoPiso - profundidade, 0f)); }

    private void Awake()
    {
        Instancia = this;
        System.Random r = new System.Random(11);
        int n = pedacosPiso != null ? pedacosPiso.Length : 0;
        eixoPiso = new Vector3[n];
        for (int i = 0; i < n; i++) eixoPiso[i] = new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() * 0.3f, (float)r.NextDouble() - 0.5f).normalized;
        if (matBrasa != null || matPoeira != null)
        {
            psPoeira = CriarParticulas("Poeira", matPoeira, false);
            psBrasa = CriarParticulas("Brasas", matBrasa != null ? matBrasa : matPoeira, true);
        }
        som = gameObject.AddComponent<AudioSource>(); som.spatialBlend = 0f; som.loop = true; som.playOnAwake = false; som.volume = 0f;
        somBatida = gameObject.AddComponent<AudioSource>(); somBatida.spatialBlend = 0f; somBatida.playOnAwake = false;
        clipRumble = GerarRumble(); clipBatida = GerarBatida();
        som.clip = clipRumble;
        if (brilhoDoFundo != null) emissao = brilhoDoFundo.material;
    }

    private void Start() { Reiniciar(); }

    // põe tudo de volta como estava: monumento em pé, piso inteiro, buraco escondido
    public void Reiniciar()
    {
        StopAllCoroutines();
        ocupada = false; cineAtiva = false; saida = 0f; barras = barrasAlvo = 0f; flash = tituloAlfa = 0f; buracoAberto = false;
        foreach (Corpo c in corpos) if (c.pedrinha && c.t != null) Destroy(c.t.gameObject);
        corpos.Clear();
        Restaurar(pedacosMonumento, posMonumento, rotMonumento);
        Restaurar(pedacosPiso, posPiso, rotPiso);
        if (cratera != null) cratera.SetActive(false);
        if (luzDoBuraco != null) luzDoBuraco.intensity = 0f;
        if (som != null) { som.Stop(); som.volume = 0f; }
        if (psPoeira != null) psPoeira.Clear();
        if (psBrasa != null) psBrasa.Clear();
    }

    private void Restaurar(Transform[] ts, Vector3[] ps, Quaternion[] rs)
    {
        if (ts == null) return;
        for (int i = 0; i < ts.Length; i++)
        {
            if (ts[i] == null) continue;
            ts[i].gameObject.SetActive(true);
            if (ps != null && i < ps.Length) ts[i].localPosition = ps[i];
            if (rs != null && i < rs.Length) ts[i].localRotation = rs[i];
            ts[i].localScale = Vector3.one;
            foreach (Collider c in ts[i].GetComponents<Collider>()) c.enabled = true;
        }
    }

    // atalho de teste: deixa a praça já aberta (monumento destruído, piso caído e buraco aberto), sem fazer a cena
    public void AbrirSemCena()
    {
        Reiniciar();
        buracoAberto = true;
        if (cratera != null) cratera.SetActive(true);
        if (pedacosMonumento != null) foreach (Transform p in pedacosMonumento) if (p != null) p.gameObject.SetActive(false);
        if (pedacosPiso != null) foreach (Transform p in pedacosPiso) if (p != null) p.gameObject.SetActive(false);
        if (luzDoBuraco != null) luzDoBuraco.intensity = 7f;
        MoverBrilho(0.8f);
    }

    // ============================================================ a cena
    // criarChefe: chamada na hora da erupção; cria o Guardião e manda ele subir (devolve o Transform dele).
    // chefeSurgiu: diz quando ele terminou de subir e rugir (aí a cena acaba e o jogo volta).
    public IEnumerator Abrir(IList<Transform> jogadores, Func<Transform> criarChefe, Func<bool> chefeSurgiu, ApresentacaoDoChefe apresentacao = null)
    {
        if (apresentacao == null) apresentacao = ApresentacaoDoChefe.Guardiao();
        Reiniciar();
        ocupada = true;
        bool menuAntes = SessaoDeJogo.MenuAberto;
        SessaoDeJogo.MenuAberto = true;
        Camera cam = Camera.main;
        Vector3 centro = Centro(); float pisoY = centro.y;
        Vector3 media = Vector3.zero; int quantos = 0;
        if (jogadores != null) foreach (Transform p in jogadores) if (p != null) { media += p.position; quantos++; }
        Vector3 dir = quantos > 0 ? media / quantos - centro : transform.forward; dir.y = 0f;
        if (dir.sqrMagnitude < 4f) { dir = transform.forward; dir.y = 0f; }
        dir.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, dir);
        Vector3 camIni = cam != null ? cam.transform.position : centro + dir * 20f + Vector3.up * 3f;
        Quaternion camRotIni = cam != null ? cam.transform.rotation : Quaternion.LookRotation(-dir);
        cinePos = camIni; cineRot = camRotIni; cineAtiva = true; saida = 0f;
        Transform chefe = null;
        try
        {
            cratera.SetActive(true);
            if (luzDoBuraco != null) luzDoBuraco.intensity = 0f;
            StartCoroutine(EmpurrarJogadores(jogadores, centro, raioDoBuraco + 3.5f));
            barrasAlvo = 1f;
            som.volume = 0.05f; som.Play();

            // ---------------------------------------------- 1) SUSPENSE e 2) RACHADURAS
            const float A = 6.5f, B = 3.4f;
            float t = 0f, proxTremor = 0f, acPoeira = 0f, acPedra = 0f;
            Vector3 baseLocal = new Vector3(0f, topoDoPiso, 0f);
            Vector3 eixoInclina = transform.InverseTransformDirection(lado);
            while (t < A + B)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / (A + B)), k2 = k * k;
                float inc = Mathf.SmoothStep(0f, 1f, (t - A) / B);
                if (t >= proxTremor) { proxTremor = t + 0.2f; TremorDeCamera.Tremer(Mathf.Lerp(0.012f, 0.3f, k2), 0.35f); }
                som.volume = Mathf.Lerp(0.05f, 1f, Mathf.Pow(k, 0.8f));

                // monumento: treme e, no fim, entorta e afunda
                Quaternion R = Quaternion.AngleAxis(inc * 5f, eixoInclina);
                float amp = 0.004f + 0.035f * k2;
                for (int i = 0; i < pedacosMonumento.Length; i++)
                {
                    Transform p = pedacosMonumento[i]; if (p == null) continue;
                    Vector3 jit = new Vector3(Mathf.Sin(Time.time * 37f + i * 1.7f), Mathf.Sin(Time.time * 41f + i * 2.3f), Mathf.Sin(Time.time * 33f + i * 3.1f)) * amp;
                    p.localPosition = baseLocal + R * (posMonumento[i] - baseLocal) + Vector3.down * (inc * 0.55f) + jit;
                    p.localRotation = R * rotMonumento[i];
                }
                // piso: as placas sobem e descem, se abrem e deixam o brilho vermelho escapar pelas frestas
                for (int i = 0; i < pedacosPiso.Length; i++)
                {
                    Transform p = pedacosPiso[i]; if (p == null) continue;
                    Vector3 fora = posPiso[i] - baseLocal; fora.y = 0f; fora = fora.sqrMagnitude > 0.01f ? fora.normalized : Vector3.zero;
                    float quica = Mathf.Sin(Time.time * 29f + i * 5.1f) * (0.003f + 0.05f * k2);
                    p.localPosition = posPiso[i] + Vector3.up * quica + fora * (0.32f * inc * inc);
                    p.localRotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * 23f + i * 2.7f) * (0.15f + 3.5f * k2 + 4f * inc), eixoPiso[i]) * rotPiso[i];
                }
                if (luzDoBuraco != null) luzDoBuraco.intensity = inc * (6f + Mathf.Sin(Time.time * 18f) * 1.8f);
                MoverBrilho(inc * 0.6f);

                // poeira saindo das frestas e pedrinhas caindo do alto
                acPoeira += Time.deltaTime * (1.5f + 26f * k2);
                while (acPoeira >= 1f) { acPoeira -= 1f; PufePoeira(centro, 2.5f, raioDoBuraco - 0.5f, 0.6f, 1.6f, 2.5f, 4.5f, 0.2f); }
                acPedra += Time.deltaTime * (0.4f + 9f * k2);
                while (acPedra >= 1f) { acPedra -= 1f; SoltarPedrinha(centro, 13f); }

                // câmera: plano aberto de longe, aproximando devagar; no fim, de baixo para cima
                float b1 = Mathf.SmoothStep(0f, 1f, t / 2.4f);
                float p1 = Mathf.Clamp01(t / A);
                Vector3 posA = centro + dir * Mathf.Lerp(33f, 25f, p1) + lado * Mathf.Sin(t * 0.35f) * 2f + Vector3.up * Mathf.Lerp(9f, 5.5f, p1);
                Vector3 olhaA = centro + Vector3.up * 5.5f;
                Vector3 posB = centro + dir * 18f + lado * 5f + Vector3.up * 1.7f;
                Vector3 olhaB = centro + Vector3.up * (6.5f + 3f * inc);
                float b2 = Mathf.SmoothStep(0f, 1f, (t - A) / (B * 0.75f));
                Vector3 alvoPos = Vector3.Lerp(posA, posB, b2), alvoOlha = Vector3.Lerp(olhaA, olhaB, b2);
                cinePos = Vector3.Lerp(camIni, alvoPos, b1);
                cineRot = Quaternion.Slerp(camRotIni, Quaternion.LookRotation(alvoOlha - alvoPos), b1);
                yield return null;
            }

            // ---------------------------------------------- 3) ERUPÇÃO
            buracoAberto = true; flash = 1f;
            TremorDeCamera.Tremer(0.65f, 3f);
            somBatida.PlayOneShot(clipBatida, 1f);
            Vector3 gira;
            foreach (Transform p in pedacosPiso)
            {
                if (p == null) continue;
                foreach (Collider c in p.GetComponents<Collider>()) c.enabled = false;
                Vector3 paraDentro = centro - p.position; paraDentro.y = 0f; paraDentro = paraDentro.sqrMagnitude > 0.01f ? paraDentro.normalized : Vector3.zero;
                gira = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0.4f, 1.8f);
                corpos.Add(new Corpo { t = p, v = paraDentro * UnityEngine.Random.Range(0.3f, 2.2f) + Vector3.down * UnityEngine.Random.Range(0.5f, 3f), w = gira, raio = 0.35f });
            }
            foreach (Transform p in pedacosMonumento)
            {
                if (p == null) continue;
                foreach (Collider c in p.GetComponents<Collider>()) c.enabled = false;
                Vector3 fora = p.position - centro; fora.y = 0f;
                fora = fora.sqrMagnitude > 0.05f ? fora.normalized : UnityEngine.Random.insideUnitSphere; fora.y = 0f;
                float alto = Mathf.Clamp01((p.position.y - pisoY) / 10f);
                Vector3 v = fora.normalized * UnityEngine.Random.Range(3f, 10f) + Vector3.up * UnityEngine.Random.Range(4f, 8f + 5f * (1f - alto));
                Renderer rd = p.GetComponent<Renderer>();
                float raio = rd != null ? Mathf.Clamp(Mathf.Min(rd.bounds.extents.x, rd.bounds.extents.y, rd.bounds.extents.z) * 0.8f, 0.2f, 2.5f) : 0.6f;
                corpos.Add(new Corpo { t = p, v = v, w = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, 4.5f), raio = raio });
            }
            for (int i = 0; i < 46; i++) PufePoeira(centro, 2f, raioDoBuraco, 4f, 12f, 4f, 8f, 0.4f);
            for (int i = 0; i < 26; i++) PufePoeira(centro + Vector3.down * 1f, 0f, 3f, 7f, 16f, 3f, 6f, 1.6f);
            for (int i = 0; i < 160; i++) Brasa(centro + new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), -1f, UnityEngine.Random.Range(-2.5f, 2.5f)), UnityEngine.Random.Range(5f, 22f));
            if (luzDoBuraco != null) luzDoBuraco.intensity = 30f;
            MoverBrilho(1f);

            float te = 0f; bool tituloMostrado = false; bool criou = false; float tituloIni = -1f; float orbita = 0f;
            Vector3 posIni2 = cinePos; Quaternion rotIni2 = cineRot;
            Vector3 chefePos0 = PontoDoChefe();
            while (te < 40f)
            {
                te += Time.deltaTime;
                if (!criou && te >= 0.55f) { criou = true; chefe = criarChefe != null ? criarChefe() : null; tituloIni = te + 2.65f; }
                if (chefe == null && criou) break;
                if (chefe != null && chefeSurgiu != null && chefeSurgiu()) break;
                flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 1.6f);
                if (luzDoBuraco != null) luzDoBuraco.intensity = Mathf.Lerp(luzDoBuraco.intensity, 9f + Mathf.Sin(Time.time * 11f) * 2.5f, Time.deltaTime * 1.4f);
                if (Time.frameCount % 3 == 0) Brasa(centro + new Vector3(UnityEngine.Random.Range(-3f, 3f), -2f, UnityEngine.Random.Range(-3f, 3f)), UnityEngine.Random.Range(4f, 12f));
                if (Time.frameCount % 7 == 0) PufePoeira(centro + Vector3.down * 2f, 0f, 3.4f, 1f, 3f, 3f, 5f, 0.3f);
                if (te < 3f && Time.frameCount % 5 == 0) SoltarPedrinha(centro, 14f);
                if (tituloIni > 0f && !tituloMostrado && te >= tituloIni) { tituloMostrado = true; TituloDeChefe.Mostrar(apresentacao); }

                // câmera: recua para um plano aberto e depois sobe junto com o Guardião, girando devagar
                orbita += Time.deltaTime * 4f;
                float b = Mathf.SmoothStep(0f, 1f, te / 1.8f);
                Quaternion giro = Quaternion.AngleAxis(orbita - 10f, Vector3.up);
                Vector3 alvoPos = centro + giro * (dir * 31f) + giro * lado * 3f + Vector3.up * Mathf.Lerp(11f, 6.5f, Mathf.Clamp01(te / 8f));
                float mira = chefe != null ? Mathf.Clamp(chefe.position.y + 3.8f, centro.y + 2f, centro.y + 7f) : centro.y + 3f;
                Vector3 alvoOlha = new Vector3(centro.x, Mathf.Lerp(centro.y + 3f, mira, Mathf.Clamp01(te / 2.5f)), centro.z);
                // plano do chefe: de frente para ele, mais perto e um pouco acima da borda, enquanto o letreiro aparece embaixo
                float h = Mathf.SmoothStep(0f, 1f, (te - 2.4f) / 1.8f);
                if (h > 0f)
                {
                    float apr = Mathf.Clamp01((te - 2.4f) / 6f);
                    Vector3 posH = centro + dir * Mathf.Lerp(15.5f, 12.5f, apr) + lado * 1.4f + Vector3.up * Mathf.Lerp(3.4f, 2.6f, apr);
                    float cabeca = chefe != null ? Mathf.Clamp(chefe.position.y + 4.4f, centro.y - 3.5f, centro.y + 1.4f) : centro.y;
                    Vector3 olhaH = new Vector3(centro.x, cabeca + 0.8f, centro.z);
                    alvoPos = Vector3.Lerp(alvoPos, posH, h); alvoOlha = Vector3.Lerp(alvoOlha, olhaH, h);
                }
                cinePos = Vector3.Lerp(posIni2, alvoPos, b);
                cineRot = Quaternion.Slerp(rotIni2, Quaternion.LookRotation(alvoOlha - alvoPos), b);
                yield return null;
            }
            tituloAlfa = 0f; TituloDeChefe.Esconder();
        }
        finally
        {
            TituloDeChefe.Esconder();
            barrasAlvo = 0f; cineAtiva = false; saida = 1f; ocupada = false;
            SessaoDeJogo.MenuAberto = menuAntes;
            if (som != null) StartCoroutine(SumirSom());
        }
    }

    private IEnumerator SumirSom()
    {
        float v = som.volume;
        while (som.volume > 0.01f) { som.volume = Mathf.MoveTowards(som.volume, 0f, Time.deltaTime * 0.35f); yield return null; }
        som.Stop();
    }

    private IEnumerator EmpurrarJogadores(IList<Transform> jogadores, Vector3 centro, float raio)
    {
        if (jogadores == null) yield break;
        List<Transform> lista = new List<Transform>(jogadores);
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            foreach (Transform p in lista)
            {
                if (p == null) continue;
                Vector3 d = p.position - centro; d.y = 0f;
                if (d.magnitude >= raio) continue;
                Vector3 alvo = centro + (d.sqrMagnitude < 0.01f ? transform.forward : d.normalized) * raio; alvo.y = p.position.y;
                Vector3 novo = Vector3.MoveTowards(p.position, alvo, Time.deltaTime * 14f);
                CharacterController cc = p.GetComponent<CharacterController>();
                if (cc != null && cc.enabled) { cc.enabled = false; p.position = novo; cc.enabled = true; } else p.position = novo;
            }
            yield return null;
        }
    }

    // ============================================================ física simples das peças
    private float Chao(Vector3 p)
    {
        float pisoY = transform.position.y + topoDoPiso;
        if (!buracoAberto) return pisoY;
        Vector3 c = Centro();
        float d = new Vector2(p.x - c.x, p.z - c.z).magnitude;
        float t = Mathf.Clamp01((raioDoBuraco - d) / (raioDoBuraco - 3.4f));
        return pisoY - profundidade * t;
    }

    private void Update()
    {
        barras = Mathf.MoveTowards(barras, barrasAlvo, Time.deltaTime * 1.3f);
        if (emissao != null && cratera != null && cratera.activeSelf)
        {
            float pulso = 1.1f + Mathf.Sin(Time.time * 3f) * 0.3f + Mathf.Sin(Time.time * 7.3f) * 0.15f;
            emissao.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.03f) * pulso * Mathf.Max(0.15f, brilhoAtual));
        }
        float dt = Time.deltaTime;
        for (int i = corpos.Count - 1; i >= 0; i--)
        {
            Corpo c = corpos[i];
            if (c.t == null) { corpos.RemoveAt(i); continue; }
            if (c.fim > 0f && Time.time >= c.fim)
            {
                if (c.pedrinha) Destroy(c.t.gameObject); else c.t.gameObject.SetActive(false);
                corpos.RemoveAt(i); continue;
            }
            if (c.pousou) continue;
            c.v += Vector3.down * 20f * dt;
            Vector3 p = c.t.position + c.v * dt;
            float chao = Chao(p) + c.raio * 0.7f;
            float dentro = new Vector2(p.x - Centro().x, p.z - Centro().z).magnitude;
            if (p.y <= chao)
            {
                p.y = chao;
                if (c.pedrinha) { Emitir(p, 0.6f, 1.2f, 1.2f, 2f, 0.12f); c.t.position = p; c.fim = Time.time; c.pousou = true; continue; }
                if (Mathf.Abs(c.v.y) > 4f && dentro > raioDoBuraco + 0.5f)
                {
                    TremorDeCamera.Tremer(Mathf.Clamp(c.raio * 0.12f, 0.03f, 0.2f), 0.4f);
                    Emitir(p, c.raio * 3f, c.raio * 5f, 2f, 4f, 0.5f);
                    c.v = new Vector3(c.v.x * 0.35f, -c.v.y * 0.25f, c.v.z * 0.35f); c.w *= 0.4f;
                }
                else
                {
                    c.pousou = true;
                    if (dentro < raioDoBuraco + 0.5f) { c.fim = Time.time + UnityEngine.Random.Range(1.5f, 3.2f); Emitir(p, 2f, 4f, 3f, 5f, 0.4f); }
                }
            }
            c.t.position = p;
            if (c.w.sqrMagnitude > 0.0001f) c.t.rotation = Quaternion.AngleAxis(c.w.magnitude * Mathf.Rad2Deg * dt, c.w.normalized) * c.t.rotation;
        }
    }

    private float brilhoAtual = 0.15f;
    private void MoverBrilho(float v) { brilhoAtual = v; }

    // ============================================================ câmera, tarjas e título
    private void LateUpdate()
    {
        if (!cineAtiva && saida <= 0f) return;
        Camera cam = Camera.main; if (cam == null) return;
        Transform ct = cam.transform;
        if (cineAtiva) ct.SetPositionAndRotation(cinePos, cineRot);
        else
        {
            saida = Mathf.Max(0f, saida - Time.unscaledDeltaTime / 1.4f);
            float k = Mathf.SmoothStep(0f, 1f, 1f - saida);      // da câmera do cinema para a câmera normal (que o outro script já calculou)
            ct.SetPositionAndRotation(Vector3.Lerp(cinePos, ct.position, k), Quaternion.Slerp(cineRot, ct.rotation, k));
        }
    }

    private void OnGUI()
    {
        if (barras < 0.002f && flash < 0.002f) return;
        GUI.depth = -50;
        float W = Screen.width, H = Screen.height, e = H / 1080f;
        Texture2D br = Texture2D.whiteTexture;
        float h = H * 0.115f * Mathf.SmoothStep(0f, 1f, barras);
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, W, h), br); GUI.DrawTexture(new Rect(0f, H - h, W, h), br);
        if (flash > 0.002f) { GUI.color = new Color(1f, 0.85f, 0.7f, flash * 0.8f); GUI.DrawTexture(new Rect(0f, 0f, W, H), br); }
        GUI.color = Color.white;
    }

    // ============================================================ partículas e pedrinhas
    private ParticleSystem CriarParticulas(string nome, Material m, bool brasa)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(transform, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = brasa ? 1200 : 1500; main.startLifetime = 4f; main.startSpeed = 0f; main.startSize = 3f;
        main.gravityModifier = brasa ? 0.5f : -0.02f;
        ParticleSystem.EmissionModule em = ps.emission; em.enabled = false;
        ParticleSystem.ShapeModule sh = ps.shape; sh.enabled = false;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, brasa ? 0.05f : 0.18f), new GradientAlphaKey(brasa ? 0.8f : 0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, brasa ? AnimationCurve.Linear(0f, 1f, 1f, 0.2f) : AnimationCurve.Linear(0f, 0.55f, 1f, 1.5f));
        if (!brasa)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule lim = ps.limitVelocityOverLifetime; lim.enabled = true;
            lim.limit = 1.2f; lim.dampen = 0.04f;
        }
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard; r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    private void Emitir(Vector3 pos, float tamMin, float tamMax, float vidaMin, float vidaMax, float sobe)
    {
        if (psPoeira == null) return;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        ep.position = pos + UnityEngine.Random.insideUnitSphere * 0.4f;
        Vector3 v = UnityEngine.Random.insideUnitSphere * 1.5f; v.y = Mathf.Abs(v.y) + sobe; ep.velocity = v;
        ep.startSize = UnityEngine.Random.Range(tamMin, tamMax); ep.startLifetime = UnityEngine.Random.Range(vidaMin, vidaMax);
        ep.rotation = UnityEngine.Random.Range(0f, 360f);
        float g = UnityEngine.Random.Range(0.36f, 0.5f);
        ep.startColor = new Color(g, g * 0.92f, g * 0.84f, UnityEngine.Random.Range(0.22f, 0.4f));
        psPoeira.Emit(ep, 1);
    }

    // nuvem de poeira numa posição aleatória entre raio mínimo e máximo, com velocidade horizontal/vertical
    private void PufePoeira(Vector3 centro, float rMin, float rMax, float vMin, float vMax, float tamMin, float tamMax, float sobe)
    {
        if (psPoeira == null) return;
        float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f), r = UnityEngine.Random.Range(rMin, rMax);
        Vector3 pos = centro + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r);
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        ep.position = pos;
        float velo = UnityEngine.Random.Range(vMin * 0.2f, vMax * 0.5f);
        ep.velocity = new Vector3(Mathf.Cos(a) * velo, UnityEngine.Random.Range(sobe, sobe * 4f + vMax * 0.35f), Mathf.Sin(a) * velo);
        ep.startSize = UnityEngine.Random.Range(tamMin, tamMax); ep.startLifetime = UnityEngine.Random.Range(3f, 6.5f);
        ep.rotation = UnityEngine.Random.Range(0f, 360f);
        float g = UnityEngine.Random.Range(0.34f, 0.48f);
        ep.startColor = new Color(g, g * 0.92f, g * 0.84f, UnityEngine.Random.Range(0.2f, 0.38f));
        psPoeira.Emit(ep, 1);
    }

    private void Brasa(Vector3 pos, float velY)
    {
        if (psBrasa == null) return;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        ep.position = pos;
        ep.velocity = new Vector3(UnityEngine.Random.Range(-3.5f, 3.5f), velY, UnityEngine.Random.Range(-3.5f, 3.5f));
        ep.startSize = UnityEngine.Random.Range(0.12f, 0.4f); ep.startLifetime = UnityEngine.Random.Range(1.4f, 3.2f);
        ep.startColor = new Color(1f, UnityEngine.Random.Range(0.35f, 0.65f), 0.1f, 1f);
        psBrasa.Emit(ep, 1);
    }

    // brasa de uma cor qualquer (usada pelo cristal)
    public void EmitirBrasa(Vector3 pos, Vector3 vel, Color cor, float tamanho)
    {
        if (psBrasa == null) return;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        ep.position = pos; ep.velocity = vel; ep.startSize = tamanho; ep.startLifetime = UnityEngine.Random.Range(1.2f, 2.6f); ep.startColor = cor;
        psBrasa.Emit(ep, 1);
    }

    // depois do chefe cair: o cristal que ele guardava sobe do buraco e fica flutuando até alguém pegar
    public CristalDeHistoria RevelarCristal(Color cor, Action aoPegar, bool podePegar = true)
    {
        Material molde = brilhoDoFundo != null ? brilhoDoFundo.sharedMaterial : matPedra;
        TremorDeCamera.Tremer(0.12f, 2f);
        CristalDeHistoria c = CristalDeHistoria.Criar(this, molde, PontoDoChefe() + Vector3.down * 0.2f, Centro() + Vector3.up * 2.2f, cor, aoPegar);
        c.podePegar = podePegar;
        return c;
    }

    private void SoltarPedrinha(Vector3 centro, float altura)
    {
        if (matPedra == null) return;
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
        g.GetComponent<Renderer>().sharedMaterial = matPedra;
        float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(UnityEngine.Random.value) * 13f;
        float s = UnityEngine.Random.Range(0.12f, 0.5f);
        g.transform.SetParent(transform, true);
        g.transform.position = centro + new Vector3(Mathf.Cos(a) * r, altura + UnityEngine.Random.Range(0f, 4f), Mathf.Sin(a) * r);
        g.transform.localScale = new Vector3(s, s * UnityEngine.Random.Range(0.6f, 1.2f), s * UnityEngine.Random.Range(0.7f, 1.3f));
        g.transform.rotation = UnityEngine.Random.rotation;
        corpos.Add(new Corpo { t = g.transform, v = Vector3.down * UnityEngine.Random.Range(1f, 4f), w = UnityEngine.Random.onUnitSphere * 3f, raio = s * 0.5f, pedrinha = true });
    }

    // ============================================================ sons feitos por código
    private static AudioClip GerarRumble()
    {
        int sr = 22050, n = sr * 4; float[] d = new float[n];
        System.Random rnd = new System.Random(7); float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = (float)(rnd.NextDouble() * 2.0 - 1.0), t = i / (float)sr;
            lp += (w - lp) * 0.012f; lp2 += (lp - lp2) * 0.05f;
            float s = lp2 * 16f + Mathf.Sin(t * Mathf.PI * 2f * 36f) * 0.32f + Mathf.Sin(t * Mathf.PI * 2f * 53f + Mathf.Sin(t * 1.7f) * 2f) * 0.18f;
            float e = Mathf.Min(1f, Mathf.Min(i, n - i) / (sr * 0.4f));
            d[i] = Mathf.Clamp(s * e * 0.6f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("RumbleDoChao", n, 1, sr, false); c.SetData(d, 0); return c;
    }

    private static AudioClip GerarBatida()
    {
        int sr = 22050, n = sr * 4; float[] d = new float[n];
        System.Random rnd = new System.Random(13); float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = (float)(rnd.NextDouble() * 2.0 - 1.0), t = i / (float)sr;
            lp += (w - lp) * 0.3f; lp2 += (w - lp2) * 0.03f;
            float s = lp * 0.9f * Mathf.Exp(-t * 3.2f) + lp2 * 7f * Mathf.Exp(-t * 1.5f) + Mathf.Sin(t * Mathf.PI * 2f * 42f) * 0.8f * Mathf.Exp(-t * 1.1f);
            float ataque = Mathf.Clamp01(t / 0.01f);
            d[i] = Mathf.Clamp(s * ataque * 0.75f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("DesabaDaPraca", n, 1, sr, false); c.SetData(d, 0); return c;
    }

}
