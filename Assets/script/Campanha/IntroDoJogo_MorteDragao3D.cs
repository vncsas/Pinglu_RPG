using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// A MORTE DO DRAGÃO EM 3D (F9): mesmo motor da abertura (câmera própria voando pelo mundo real) com o MODELO 3D do dragão.
// 1) ele despenca e bate no chão; 2) convulsão, o poder escapando; 3) coluna de fumaça negra para o céu; 4) vira cinzas ao vento; 5) a luz e a paz.
public partial class IntroDoJogo
{
    // quando a morte acontece de verdade (na luta), AposODragao diz onde o dragão estava e onde ele caiu
    public static Vector3? MorteChao, MorteInicio;
    class AnelDeChoque : MonoBehaviour
    {
        public float dur = 2.4f, raioMax = 80f; float t; LineRenderer lr; const int N = 72;
        void Awake()
        {
            lr = gameObject.AddComponent<LineRenderer>(); lr.positionCount = N; lr.loop = true; lr.useWorldSpace = false; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sharedMaterial = FxCena.Sprite(); lr.alignment = LineAlignment.TransformZ; transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        void Update()
        {
            t += Time.unscaledDeltaTime; float k = t / dur; if (k >= 1f) { Destroy(gameObject); return; }
            float r = Mathf.Lerp(2f, raioMax, 1f - (1f - k) * (1f - k));
            for (int i = 0; i < N; i++) { float a = i / (float)N * 6.2832f; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f)); }
            lr.startWidth = lr.endWidth = Mathf.Lerp(2.4f, 0.5f, k); Color c = new Color(0.85f, 0.7f, 1f, (1f - k) * 0.8f); lr.startColor = lr.endColor = c;
        }
    }

    void Anel3(Vector3 c) { GameObject g = new GameObject("Onda de choque"); g.transform.position = c + Vector3.up * 0.6f; g.AddComponent<AnelDeChoque>(); }

    // o modelo do dragão, na escala certa e com a animação rodando devagar; "pe" = o ponto do chão onde ele fica
    GameObject ModeloDoDragao3(Transform pai, float comprimento, List<Material> mats)
    {
        if (dragaoOrigem == null || dragaoOrigem.modelo == null) return null;
        GameObject m = Instantiate(dragaoOrigem.modelo); m.transform.SetParent(pai, false); m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.Euler(dragaoOrigem.rotacaoDoModelo);
        foreach (Collider c in m.GetComponentsInChildren<Collider>()) c.enabled = false;
        Bounds b = new Bounds(m.transform.position, Vector3.zero); bool tem = false;
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds); }
        if (tem)
        {
            float maior = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); if (maior > 0.01f) m.transform.localScale *= comprimento / maior;
            // recalcula e apoia o ponto mais baixo no "pai"
            b = new Bounds(m.transform.position, Vector3.zero); tem = false;
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds); }
            // o corpo fica deitado no chão: o centro do modelo um pouco acima do terreno (cauda e patas que pendem afundam no chão, não ficam no ar)
            m.transform.position += new Vector3(pai.position.x - b.center.x, pai.position.y + Mathf.Min(3.2f, b.size.y * 0.2f) - b.center.y, pai.position.z - b.center.z);
        }
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; foreach (Material mt in r.materials) mats.Add(mt); }
        AnimationClip clip = dragaoOrigem.animPlanar != null ? dragaoOrigem.animPlanar : dragaoOrigem.animVoo;
        if (clip != null)
        {
            Animator an = m.GetComponentInChildren<Animator>(); if (an == null) an = m.AddComponent<Animator>();
            PlayableGraph pg = PlayableGraph.Create("dragao da morte"); pg.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            AnimationClipPlayable cp = AnimationClipPlayable.Create(pg, clip); cp.SetSpeed(0.25f); AnimationPlayableOutput.Create(pg, "a", an).SetSourcePlayable(cp); pg.Play(); grafos3.Add(pg);
        }
        return m;
    }

    static void CorDoDragao(List<Material> mats, Color cor, Color brilho)
    {
        foreach (Material m in mats)
        {
            if (m == null) continue;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor); else if (m.HasProperty("_Color")) m.SetColor("_Color", cor);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", brilho); }
        }
    }

    // ------------------------------------------------------------------ pedaços e ossos
    void Pedacos3(Vector3 pos, Vector3 direcao, int n, float tam)
    {
        Material mat = MatLit3(new Color(0.22f, 0.1f, 0.09f));
        if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.05f) * 0.7f); }
        for (int i = 0; i < n; i++)
        {
            GameObject g = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            g.transform.position = pos + Random.insideUnitSphere * tam * 0.5f; g.transform.rotation = Random.rotation;
            g.transform.localScale = new Vector3(Random.Range(0.4f, 1.2f), Random.Range(0.4f, 1.1f), Random.Range(0.5f, 1.4f)) * tam * Random.Range(0.4f, 1f);
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Rigidbody rb = g.AddComponent<Rigidbody>(); rb.mass = 30f;
            rb.AddForce((Random.onUnitSphere + Vector3.up * 1.2f + direcao * 0.7f).normalized * Random.Range(10f, 26f), ForceMode.VelocityChange); rb.AddTorque(Random.onUnitSphere * 8f, ForceMode.VelocityChange);
            if (i < 3) NovoPS(g.transform, "Fumaça do pedaço", FxCena.TexNuvem(), new Color(0.07f, 0.06f, 0.06f, 0.6f), 3f, 0.5f, 2.2f, 10f, -0.05f, true, 0.3f);
            Destroy(g, 18f); lixo3.Add(g);
        }
        GameObject ex = new GameObject("Explosão de pedaço"); ex.transform.position = pos; lixo3.Add(ex); Destroy(ex, 6f);
        NovoPS(ex.transform, "Brasas", FxCena.TexBrilho(), new Color(1f, 0.5f, 0.15f, 1f), 1.8f, 16f, 1.6f, 0f, 0.15f, false, 1.5f).Emit(80);
        NovoPS(ex.transform, "Fumaça", FxCena.TexNuvem(), new Color(0.06f, 0.05f, 0.06f, 0.75f), 4f, 9f, 5.5f, 0f, -0.05f, true, 2f).Emit(30);
        GameObject lg = new GameObject("Clarão do pedaço"); lg.transform.SetParent(ex.transform, false); Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.range = 40f; l.intensity = 25f; l.shadows = LightShadows.None; Destroy(lg, 0.35f);
        flash3 = Mathf.Max(flash3, 0.3f);
    }

    static List<Transform> OssosPor3(Transform raiz, params string[] prefixos)
    {
        List<Transform> r = new List<Transform>();
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) { string n = t.name.ToLowerInvariant(); foreach (string p in prefixos) if (n.StartsWith(p)) { r.Add(t); break; } }
        return r;
    }

    // ------------------------------------------------------------------ o povo e o jogador
    class Figurante { public GameObject go; public Animator an; public Vector3 ini, alvo; public float fase, atraso, vel; public bool caio, chegou; public string estado; }
    readonly List<Figurante> povo = new List<Figurante>(); readonly List<GameObject> povoObj = new List<GameObject>(); Transform realCaio; Transform jogador3;

    static Transform JogadorLocal3()
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p != null && p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) return p.transform;
        return null;
    }

    void TocarEstado3(Figurante f, string est, float vel)
    {
        if (f.an == null || f.estado == est) return;
        f.estado = est; f.an.speed = vel; string alt = est;
        if (!f.an.HasState(0, Animator.StringToHash(est))) alt = "Parado";
        if (f.an.HasState(0, Animator.StringToHash(alt))) f.an.CrossFadeInFixedTime(alt, 0.2f);
    }

    void CriarPovo3(Vector3 jp, Vector3 f)
    {
        foreach (GameObject o in povoObj) if (o != null) Destroy(o);
        povoObj.Clear(); povo.Clear(); Vector3 R = Vector3.Cross(Vector3.up, f); Vector3 dv = -f;                    // a vila fica atrás do personagem
        GameObject caioSrc = null; List<GameObject> fontes = new List<GameObject>();
        foreach (NpcFalante n in FindObjectsByType<NpcFalante>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (n != null && n.nomeExibido == "Caio") { caioSrc = n.gameObject; realCaio = n.transform; break; }
        foreach (NpcDaVila n in FindObjectsByType<NpcDaVila>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (n != null) fontes.Add(n.gameObject);
        if (caioSrc == null && fontes.Count > 0) { caioSrc = fontes[0]; fontes.RemoveAt(0); }
        if (caioSrc == null) return;
        int total = Mathf.Max(7, fontes.Count + 1);
        for (int i = 0; i < total; i++)
        {
            bool caio = i == 0; GameObject src = caio ? caioSrc : fontes.Count > 0 ? fontes[(i - 1) % fontes.Count] : caioSrc;
            GameObject g = Instantiate(src); g.name = caio ? "Caio (comemoração)" : "Morador (comemoração)"; povoObj.Add(g);          // fica até o fim da sequência (não some ao trocar de cena)
            g.SetActive(true);                                                                           // o original pode estar desligado (longe do jogador)
            foreach (MonoBehaviour mb in g.GetComponents<MonoBehaviour>()) if (mb != null) DestroyImmediate(mb);
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (Renderer rd in g.GetComponentsInChildren<Renderer>(true)) { rd.enabled = true; if (rd is SkinnedMeshRenderer) ((SkinnedMeshRenderer)rd).updateWhenOffscreen = true; }
            foreach (LODGroup lg in g.GetComponentsInChildren<LODGroup>(true)) lg.ForceLOD(0);
            foreach (Animator an0 in g.GetComponentsInChildren<Animator>(true)) { an0.enabled = true; an0.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            CharacterController cc = g.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            if (caio) DialogoUI.Registrar("Caio", g.transform);                                       // o balão de fala sai do Caio que está aqui
            Figurante fg = new Figurante { go = g, an = g.GetComponent<Animator>() != null ? g.GetComponent<Animator>() : g.GetComponentInChildren<Animator>(), caio = caio, fase = Random.value * 6f, atraso = caio ? 0f : 0.6f + Random.value * 2.2f, vel = caio ? 6.2f : Random.Range(5f, 6.4f) };
            if (caio) fg.alvo = jp - f * 2.4f + R * 1.7f;
            else { float ang = Random.Range(105f, 255f) * Mathf.Deg2Rad, raio = Random.Range(4.5f, 9f); fg.alvo = jp + Quaternion.LookRotation(f) * new Vector3(Mathf.Sin(ang) * raio, 0f, Mathf.Cos(ang) * raio); }
            fg.ini = fg.alvo + dv * (caio ? 34f : Random.Range(36f, 52f)) + R * (caio ? 0f : Random.Range(-9f, 9f));
            fg.alvo = G3(fg.alvo); fg.ini = G3(fg.ini); g.transform.position = fg.ini; g.transform.rotation = Quaternion.LookRotation(fg.alvo - fg.ini);
            povo.Add(fg);
        }
    }

    // ------------------------------------------------------------------ as cenas
    void AtribuirCenasMorte3D()
    {
        EncontroDoDragao ed = FindAnyObjectByType<EncontroDoDragao>();
        // onde ele cai: na luta de verdade, onde o dragão morreu; no teste (F9), no campo aberto do centro do mapa (perto do portal e da vila), onde dá para ver tudo
        PortalDoMundo pmo = PortalDoMundo.Atual; Transform jt = JogadorLocal3();
        Vector3 padrao = pmo != null ? pmo.CentroNoChao + pmo.Frente * 24f : (ed != null && ed.centroArena.sqrMagnitude > 4f ? ed.centroArena : (jt != null ? jt.position : montanha));
        Vector3 ac = G3(MorteChao.HasValue ? MorteChao.Value : padrao);
        Terrain terr = Terrain.activeTerrain; Vector3 tc = terr != null ? terr.transform.position + terr.terrainData.size * 0.5f : ac;
        Vector3 dm = tc - ac; dm.y = 0f; dm = dm.sqrMagnitude > 1f ? dm.normalized : dirIn3; Vector3 lado = Vector3.Cross(Vector3.up, dm);
        Vector3 peito = ac + Vector3.up * 6f;
        float h0 = MorteInicio.HasValue ? MorteInicio.Value.y - ac.y : 90f;
        Vector3 p0 = MorteInicio.HasValue && h0 >= 8f ? MorteInicio.Value : ac + Vector3.up * 95f + lado * 40f - dm * 30f;
        bool caiDoAr = (p0 - ac).y >= 8f;
        GameObject[] raiz = new GameObject[1]; List<Material> mats = new List<Material>(); Light[] luz = new Light[1];
        bool[] fora = new bool[3];                                  // partes já arrancadas: 0 asas, 1 cauda, 2 cabeça
        List<Transform>[] ossos = { new List<Transform>(), new List<Transform>(), new List<Transform>() };
        System.Action criarDragao = () =>
        {
            raiz[0] = new GameObject("Dragão derrotado"); lixo3.Add(raiz[0]); raiz[0].transform.position = ac; mats.Clear();
            GameObject m = ModeloDoDragao3(raiz[0].transform, 40f, mats);
            if (m == null) { GameObject cor = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(cor.GetComponent<Collider>()); cor.transform.SetParent(raiz[0].transform, false); cor.transform.localPosition = Vector3.up * 3f; cor.transform.localScale = new Vector3(8f, 6f, 20f); cor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); mats.Add(cor.GetComponent<MeshRenderer>().material); }
            else { ossos[0] = OssosPor3(m.transform, "wingpoll", "upperwing"); ossos[1] = OssosPor3(m.transform, "tail"); ossos[2] = OssosPor3(m.transform, "neck3"); }
            raiz[0].transform.rotation = Quaternion.LookRotation(lado);
            CorDoDragao(mats, new Color(0.14f, 0.1f, 0.12f), Color.black);
        };
        System.Action aplicaRasgos = () => { for (int g = 0; g < 3; g++) if (fora[g]) foreach (Transform t in ossos[g]) if (t != null) t.localScale = Vector3.zero; };
        System.Action<int> arranca = g =>
        {
            if (fora[g]) return; fora[g] = true;
            Vector3 dir = Vector3.up; Transform t0 = ossos[g].Count > 0 ? ossos[g][0] : null; Vector3 pos = t0 != null ? t0.position : peito;
            if (g == 0) { foreach (Transform t in ossos[0]) if (t != null && t.name.ToLowerInvariant().StartsWith("wingpoll")) Pedacos3(t.position, (t.position - peito).normalized, 6, 2.2f); }
            else Pedacos3(pos, (pos - peito).normalized, 9, g == 2 ? 3f : 2.4f);
        };
        Amb tempestade = new Amb { sol = new Color(0.55f, 0.45f, 0.7f), solInt = 0.5f, solRot = new Vector3(35f, 40f, 0f), fog = new Color(0.1f, 0.06f, 0.12f), fogDen = 0.007f, amb = new Color(0.2f, 0.14f, 0.26f), ceu = new Color(0.3f, 0.2f, 0.4f), exposicao = 0.35f, atmosfera = 2.6f };

        // 0) a queda
        cenas[0].amb = new Amb { sol = tempestade.sol, solInt = 0.6f, solRot = tempestade.solRot, fog = new Color(0.14f, 0.07f, 0.1f), fogDen = 0.0065f, amb = new Color(0.26f, 0.15f, 0.2f), ceu = tempestade.ceu, exposicao = 0.45f, atmosfera = 2.8f };
        bool[] bateu = { false };
        cenas[0].entra = () =>
        {
            criarDragao(); bateu[0] = false; fora[0] = fora[1] = fora[2] = false;
            if (caiDoAr)
            {
                NovoPS(raiz[0].transform, "Fogo da queda", FxCena.TexBrilho(), new Color(1f, 0.5f, 0.15f, 0.95f), 1.3f, 3f, 9f, 55f, -0.1f, false, 6f);
                NovoPS(raiz[0].transform, "Fumaça da queda", FxCena.TexNuvem(), new Color(0.06f, 0.05f, 0.06f, 0.65f), 4.5f, 4f, 11f, 14f, -0.08f, true, 7f);
            }
            GameObject lg = new GameObject("Luz da queda"); lg.transform.SetParent(raiz[0].transform, false); lg.transform.localPosition = Vector3.up * 3f; Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.2f); l.range = 70f; l.intensity = 9f; l.shadows = LightShadows.None;
        };
        cenas[0].vivo = k =>
        {
            if (raiz[0] == null) return;
            float kk = Mathf.Clamp01(k / 0.7f), q = caiDoAr ? kk * kk : Suave(kk * 0.9f); Vector3 pos = Vector3.Lerp(caiDoAr ? p0 : ac + Vector3.up * 2f, ac, q);
            float pitch = k < 0.7f ? Mathf.Lerp(6f, caiDoAr ? 48f : 18f, Suave(kk)) : Mathf.Lerp(caiDoAr ? 48f : 18f, 6f, Suave((k - 0.7f) / 0.12f)), roll = k < 0.7f ? Mathf.Lerp(0f, 28f, kk) : 12f;
            Vector3 f = ac - p0; f.y = 0f; f = f.sqrMagnitude > 1f ? f.normalized : lado;
            raiz[0].transform.position = pos; raiz[0].transform.rotation = Quaternion.LookRotation(f) * Quaternion.Euler(pitch, 0f, roll);
            if (k >= 0.7f && !bateu[0])
            {
                bateu[0] = true; flash3 = 1f;
                ParticleSystem poeira = NovoPS(raiz[0].transform, "Poeira do impacto", FxCena.TexNuvem(), new Color(0.5f, 0.42f, 0.34f, 0.7f), 4.5f, 14f, 9f, 0f, -0.05f, true, 7f); poeira.Emit(160);
                foreach (ParticleSystem ps in raiz[0].GetComponentsInChildren<ParticleSystem>()) if (ps.gameObject.name.StartsWith("Fogo") || ps.gameObject.name.StartsWith("Fumaça da")) { var em = ps.emission; em.rateOverTime = 4f; }
                Anel3(ac); Pedacos3(ac + Vector3.up * 2f, Vector3.up, 6, 2f);
            }
        };
        cenas[0].cam3 = k =>
        {
            Vector3 alvo = raiz[0] != null ? raiz[0].transform.position + Vector3.up * 3f : ac;
            Vector3 base0 = caiDoAr ? p0 : ac;
            Vector3 cp = Sv(base0 + lado * 78f + dm * 40f + Vector3.up * 8f, ac + lado * 52f + dm * 34f + Vector3.up * 8f, k);
            if (k > 0.7f) cp += new Vector3(Mathf.Sin(Time.unscaledTime * 55f), Mathf.Cos(Time.unscaledTime * 47f), Mathf.Sin(Time.unscaledTime * 61f)) * 1.1f * (1f - Mathf.Clamp01((k - 0.7f) / 0.3f));
            Cam(cp, alvo, Mathf.Lerp(52f, 46f, k));
        };

        // 1) a convulsão: as asas são arrancadas
        cenas[1].amb = tempestade; float[] proxArco = { 0f };
        cenas[1].entra = () =>
        {
            criarDragao(); raiz[0].transform.rotation = Quaternion.LookRotation(lado) * Quaternion.Euler(0f, 0f, 10f);
            GameObject lg = new GameObject("Luz interna"); lg.transform.SetParent(raiz[0].transform, true); lg.transform.position = peito; luz[0] = lg.AddComponent<Light>(); luz[0].type = LightType.Point; luz[0].color = new Color(0.7f, 0.4f, 1f); luz[0].range = 80f; luz[0].shadows = LightShadows.None;
            NovoPS(raiz[0].transform, "Aura que falha", FxCena.TexBrilho(), new Color(0.6f, 0.2f, 1f, 0.7f), 2.2f, 4f, 3.5f, 30f, -0.05f, false, 14f).transform.position = ac + Vector3.up * 6f;
        };
        cenas[1].vivo = k =>
        {
            if (raiz[0] == null) return;
            raiz[0].transform.position = ac + Random.insideUnitSphere * (0.12f + 0.5f * k);
            float pulso = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 28f);
            if (luz[0] != null) luz[0].intensity = Mathf.Lerp(3f, 60f, k) * pulso;
            CorDoDragao(mats, Color.Lerp(new Color(0.14f, 0.1f, 0.12f), new Color(0.3f, 0.26f, 0.34f), k), new Color(0.65f, 0.4f, 1f) * (k * 2f * pulso));
            if (k > 0.5f) arranca(0);
            aplicaRasgos();
            if (Time.unscaledTime >= proxArco[0]) { proxArco[0] = Time.unscaledTime + Mathf.Lerp(0.3f, 0.1f, k); Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f; FxCena.Raio(peito, peito + d * Random.Range(12f, 26f), 0.7f, 0.12f, new Color(0.85f, 0.6f, 1f, 1f)); }
        };
        cenas[1].cam3 = k => { float a = Mathf.Lerp(20f, 80f, Suave(k)) * Mathf.Deg2Rad, r = Mathf.Lerp(52f, 32f, Suave(k)); Cam(ac + new Vector3(Mathf.Sin(a) * r, Mathf.Lerp(14f, 9f, k), Mathf.Cos(a) * r), peito, Mathf.Lerp(50f, 42f, k)); };

        // 2) a coluna negra: a cauda e a cabeça são arrancadas
        cenas[2].amb = new Amb { sol = new Color(0.6f, 0.4f, 0.8f), solInt = 0.45f, solRot = tempestade.solRot, fog = new Color(0.07f, 0.03f, 0.1f), fogDen = 0.0085f, amb = new Color(0.16f, 0.1f, 0.22f), ceu = tempestade.ceu, exposicao = 0.3f, atmosfera = 2.8f };
        bool[] clarao = { false }; float[] proxOnda = { 0f };
        cenas[2].entra = () =>
        {
            criarDragao(); clarao[0] = false; CorDoDragao(mats, new Color(0.3f, 0.26f, 0.34f), new Color(0.65f, 0.4f, 1f) * 1.5f);
            GameObject c = new GameObject("Coluna negra"); c.transform.position = peito; lixo3.Add(c); c.transform.SetParent(raiz[0].transform, true);
            NovoPS(c.transform, "Fumaça negra", FxCena.TexNuvem(), new Color(0.03f, 0f, 0.05f, 0.92f), 4.5f, 5f, 11f, 80f, -3.2f, true, 2.2f);
            NovoPS(c.transform, "Borda roxa", FxCena.TexNuvem(), new Color(0.6f, 0.15f, 0.95f, 0.35f), 4f, 5f, 13f, 40f, -3.2f, true, 2.6f);
            NovoPS(c.transform, "Faíscas", FxCena.TexBrilho(), new Color(1f, 0.85f, 1f, 0.95f), 2f, 6f, 0.9f, 60f, -2.2f, false, 2f);
            GameObject lg = new GameObject("Luz"); lg.transform.SetParent(raiz[0].transform, true); lg.transform.position = peito + Vector3.up * 3f; luz[0] = lg.AddComponent<Light>(); luz[0].type = LightType.Point; luz[0].color = new Color(0.85f, 0.6f, 1f); luz[0].range = 100f; luz[0].shadows = LightShadows.None;
        };
        cenas[2].vivo = k =>
        {
            if (luz[0] != null) luz[0].intensity = Mathf.Lerp(50f, 25f, k) * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 21f));
            if (k > 0.2f) arranca(1); if (k > 0.58f) arranca(2);
            aplicaRasgos();
            Relampago(ac, 60f);
            if (Time.unscaledTime >= proxOnda[0]) { proxOnda[0] = Time.unscaledTime + 1.1f; Anel3(ac); }
            if (!clarao[0] && k > 0.5f) { clarao[0] = true; flash3 = 1f; }
        };
        cenas[2].cam3 = k => Cam(ac + dm * Mathf.Lerp(60f, 70f, k) + Vector3.up * Mathf.Lerp(3f, 2f, k) + lado * 8f, Vector3.Lerp(ac + Vector3.up * 10f, ac + Vector3.up * 110f, Suave(k)), Mathf.Lerp(64f, 74f, k));

        // 3) o que sobrou vai sumindo devagar, virando cinzas
        Amb cinza = new Amb { sol = new Color(0.9f, 0.62f, 0.45f), solInt = 0.8f, solRot = new Vector3(14f, 215f, 0f), fog = new Color(0.22f, 0.15f, 0.16f), fogDen = 0.006f, amb = new Color(0.3f, 0.22f, 0.25f), ceu = new Color(0.6f, 0.4f, 0.4f), exposicao = 0.55f, atmosfera = 2.4f };
        cenas[3].amb = cinza; float[] proxMigalha = { 0f };
        cenas[3].entra = () =>
        {
            criarDragao(); fora[0] = fora[1] = fora[2] = true; aplicaRasgos(); CorDoDragao(mats, new Color(0.3f, 0.26f, 0.34f), new Color(0.6f, 0.3f, 0.2f));
            GameObject vento = new GameObject("Cinzas e brasas"); vento.transform.position = ac + Vector3.up * 4f; lixo3.Add(vento);
            foreach (ParticleSystem ps in new[] {
                NovoPS(vento.transform, "Cinzas", FxCena.TexNuvem(), new Color(0.62f, 0.58f, 0.55f, 0.6f), 6f, 2f, 2.2f, 70f, -0.03f, true, 15f),
                NovoPS(vento.transform, "Brasas", FxCena.TexBrilho(), new Color(1f, 0.5f, 0.15f, 1f), 5f, 2f, 0.55f, 60f, -0.05f, false, 14f) })
            { var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; Vector3 w = lado * 14f + Vector3.up * 3f; v.x = new ParticleSystem.MinMaxCurve(w.x); v.y = new ParticleSystem.MinMaxCurve(w.y); v.z = new ParticleSystem.MinMaxCurve(w.z); }
        };
        cenas[3].vivo = k =>
        {
            if (raiz[0] == null) return;
            float e = Suave(k * 1.05f);
            raiz[0].transform.localScale = Vector3.one * Mathf.Max(0.02f, 1f - e * 0.97f);
            raiz[0].transform.position = ac + Random.insideUnitSphere * 0.1f * (1f - e);
            aplicaRasgos();
            CorDoDragao(mats, Color.Lerp(new Color(0.3f, 0.26f, 0.34f), new Color(0.2f, 0.18f, 0.18f), e), new Color(1f, 0.38f, 0.07f) * (2.4f * Mathf.Sin(Mathf.Clamp01(k * 1.15f) * Mathf.PI) + 0.1f));
            if (Time.unscaledTime >= proxMigalha[0] && k < 0.85f) { proxMigalha[0] = Time.unscaledTime + 0.7f; Pedacos3(ac + Vector3.up * 4f * (1f - e) + Random.insideUnitSphere * 3f * (1f - e), Vector3.up, 3, 1f * (1f - 0.6f * e)); }
            cinza.solInt = Mathf.Lerp(0.8f, 1.3f, e); cinza.exposicao = Mathf.Lerp(0.55f, 0.8f, e);
        };
        cenas[3].cam3 = k => { float a = Mathf.Lerp(20f, 80f, Suave(k)) * Mathf.Deg2Rad, r = Mathf.Lerp(46f, 30f, Suave(k)); Cam(ac + new Vector3(Mathf.Sin(a) * r, Mathf.Lerp(8f, 13f, k), Mathf.Cos(a) * r), ac + Vector3.up * 6f, 50f); };

        // 4) o céu clareia
        Amb paz = new Amb { sol = new Color(0.9f, 0.6f, 0.45f), solInt = 0.9f, solRot = new Vector3(14f, 215f, 0f), fog = new Color(0.3f, 0.2f, 0.2f), fogDen = 0.005f, amb = new Color(0.32f, 0.26f, 0.3f), ceu = new Color(0.6f, 0.45f, 0.5f), exposicao = 0.8f, atmosfera = 2.2f };
        cenas[4].dur = 8f; cenas[4].amb = paz;
        GameObject[] pilar = new GameObject[2];
        cenas[4].entra = () =>
        {
            pilar[0] = FxCena.Nuvem(ac + Vector3.up * 70f, 1f, new Color(1f, 0.9f, 0.6f, 0f), FxCena.TexBrilho(), 8); pilar[0].transform.localScale = new Vector3(22f, 170f, 1f); lixo3.Add(pilar[0]);
            pilar[1] = FxCena.Nuvem(ac + Vector3.up * 60f, 1f, new Color(1f, 1f, 0.9f, 0f), FxCena.TexBrilho(), 9); pilar[1].transform.localScale = new Vector3(8f, 150f, 1f); lixo3.Add(pilar[1]);
            GameObject s = new GameObject("Almas"); s.transform.position = ac + Vector3.up * 1f; lixo3.Add(s);
            NovoPS(s.transform, "Almas", FxCena.TexBrilho(), new Color(1f, 0.95f, 0.8f, 0.95f), 10f, 1f, 0.9f, 28f, -0.08f, false, 12f);
        };
        cenas[4].vivo = k =>
        {
            float p = Suave(k / 0.8f);
            paz.solInt = Mathf.Lerp(0.9f, 1.9f, p); paz.exposicao = Mathf.Lerp(0.8f, 1.1f, p); paz.sol = Color.Lerp(new Color(0.9f, 0.6f, 0.45f), new Color(1f, 0.88f, 0.62f), p);
            paz.fog = Color.Lerp(new Color(0.3f, 0.2f, 0.2f), new Color(0.75f, 0.68f, 0.7f), p); paz.fogDen = Mathf.Lerp(0.005f, 0.0022f, p); paz.amb = Color.Lerp(new Color(0.32f, 0.26f, 0.3f), new Color(0.55f, 0.52f, 0.6f), p);
            paz.solRot = new Vector3(Mathf.Lerp(14f, 28f, p), 215f, 0f);
            if (pilar[0] != null) FxCena.Pintar(pilar[0], new Color(1f, 0.9f, 0.6f, 0.5f * Suave(k / 0.3f) * (1f - 0.25f * p)));
            if (pilar[1] != null) FxCena.Pintar(pilar[1], new Color(1f, 1f, 0.92f, 0.75f * Suave(k / 0.3f)));
        };
        cenas[4].cam3 = k => Cam(Sv(ac + dm * 40f + lado * 6f + Vector3.up * 5f, ac + dm * 62f + lado * 16f + Vector3.up * 24f, k), ac + Vector3.up * Mathf.Lerp(10f, 55f, Suave(k)), Mathf.Lerp(58f, 66f, k));

        // 5) o povo da vila chega correndo, vindo de trás do personagem, com o Caio na frente
        jogador3 = JogadorLocal3(); Vector3 jp = jogador3 != null ? jogador3.position : G3(ac + dm * 18f);
        Vector3 dvil = vila - jp; dvil.y = 0f; Vector3 ffrente = dvil.sqrMagnitude > 25f ? -dvil.normalized : dm;        // o personagem olha para longe da vila
        Vector3 Rj = Vector3.Cross(Vector3.up, ffrente);
        Amb dia = new Amb { sol = new Color(1f, 0.85f, 0.6f), solInt = 1.9f, solRot = new Vector3(22f, 215f, 0f), fog = new Color(0.78f, 0.7f, 0.68f), fogDen = 0.0022f, amb = new Color(0.55f, 0.52f, 0.6f), ceu = new Color(0.55f, 0.6f, 0.8f), exposicao = 1.1f, atmosfera = 1.4f };
        cenas[5].semBarras = cenas[6].semBarras = cenas[7].semBarras = true;
        cenas[5].amb = dia; cenas[5].texto = "Na vila, ninguém acreditava. Então todos correram em direção ao herói."; cenas[5].dur = 10f;
        cenas[5].entra = () =>
        {
            jogador3 = JogadorLocal3(); if (jogador3 != null) jogador3.rotation = Quaternion.LookRotation(ffrente);
            CriarPovo3(jp, ffrente);
        };
        cenas[5].vivo = k =>
        {
            float t = Time.unscaledTime - inicioCena;
            foreach (Figurante f in povo)
            {
                if (f.go == null) continue;
                float p = Mathf.Clamp01((t - f.atraso) * f.vel / Mathf.Max(1f, Vector3.Distance(f.ini, f.alvo)));
                Vector3 pos = Vector3.Lerp(f.ini, f.alvo, p); pos.y = Chao3(pos); f.go.transform.position = pos;
                Vector3 dir = f.alvo - f.ini; dir.y = 0f; if (p < 1f) { f.go.transform.rotation = Quaternion.Slerp(f.go.transform.rotation, Quaternion.LookRotation(dir), 0.2f); TocarEstado3(f, "Andar_Calmo", 1.9f); }
                else { Vector3 pj = jogador3 != null ? jogador3.position - pos : -dir; pj.y = 0f; if (pj.sqrMagnitude > 0.1f) f.go.transform.rotation = Quaternion.Slerp(f.go.transform.rotation, Quaternion.LookRotation(pj), 0.2f); TocarEstado3(f, "Falar", 1.6f); }
            }
        };
        cenas[5].cam3 = k => { Vector3 cj = jp + ffrente * Mathf.Lerp(11f, 8f, Suave(k)) + Rj * 3.2f; cj.y = Chao3(cj) + 2.6f; Cam(cj, jp - ffrente * Mathf.Lerp(7f, 4f, k) + Vector3.up * 2f, Mathf.Lerp(52f, 46f, k)); };

        // 6) comemoração e o agradecimento do Caio (a cena espera o diálogo acabar)
        cenas[6].amb = dia; cenas[6].texto = ""; cenas[6].dur = 400f; bool[] falou = { false };
        cenas[6].entra = () =>
        {
            falou[0] = false;
            GameObject chuva = new GameObject("Confete"); chuva.transform.position = jp + Vector3.up * 11f; lixo3.Add(chuva);
            Color[] cs = { new Color(1f, 0.8f, 0.25f), new Color(1f, 0.4f, 0.4f), new Color(0.45f, 0.8f, 1f), new Color(0.6f, 1f, 0.55f) };
            foreach (Color c in cs) NovoPS(chuva.transform, "Confete", FxCena.TexBrilho(), new Color(c.r, c.g, c.b, 1f), 4.5f, 1f, 0.34f, 22f, 0.25f, false, 8f);
        };
        cenas[6].vivo = k =>
        {
            float t = Time.unscaledTime - inicioCena;
            foreach (Figurante f in povo)
            {
                if (f.go == null) continue;
                float pulo = f.caio ? 0f : Mathf.Abs(Mathf.Sin(t * 4.2f + f.fase)) * 0.5f;
                Vector3 pos = f.alvo; if (f.caio) pos = Vector3.Lerp(f.alvo, jp - ffrente * 1.2f + Rj * 1.1f, Suave((t - 0.8f) / 1.6f));
                pos.y = Chao3(pos) + pulo; f.go.transform.position = pos;
                Vector3 pj = jogador3 != null ? jogador3.position - pos : -ffrente; pj.y = 0f; if (pj.sqrMagnitude > 0.1f) f.go.transform.rotation = Quaternion.Slerp(f.go.transform.rotation, Quaternion.LookRotation(pj), 0.15f);
                TocarEstado3(f, "Falar", f.caio ? 1f : 1.7f);
            }
            if (jogador3 != null && povo.Count > 0 && povo[0].go != null)                       // o personagem olha para o Caio enquanto ele fala
            { Vector3 dc = povo[0].go.transform.position - jogador3.position; dc.y = 0f; if (dc.sqrMagnitude > 0.05f) jogador3.rotation = Quaternion.Slerp(jogador3.rotation, Quaternion.LookRotation(dc), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime)); }
            if (!falou[0] && t > 2.8f) { falou[0] = true; DialogoUI.Abrir(Roteiro.CaioAgradeceNoCampo, () => { if (atual == 6) Proxima(); }); }
        };
        cenas[6].cam3 = k =>
        {
            float t = Time.unscaledTime - inicioCena, a = (20f + Mathf.Min(t, 20f) * 5f) * Mathf.Deg2Rad, r = Mathf.Lerp(11f, 8.5f, Suave(t / 6f));
            Vector3 centro = jp - ffrente * 1.2f + Vector3.up * 1.6f; Vector3 q = centro + Quaternion.LookRotation(ffrente) * new Vector3(Mathf.Sin(a) * r, 0.8f, Mathf.Cos(a) * r);
            Cam(q, centro, 48f);
        };

        // 7) a câmera sobe, sai de cima de todos e olha para o céu; aparece o nome do jogo
        cenas[7].amb = dia; cenas[7].texto = ""; cenas[7].dur = 12f;
        cenas[7].vivo = k =>
        {
            float t = Time.unscaledTime - inicioCena;
            foreach (Figurante f in povo) { if (f.go == null) continue; float pulo = f.caio ? 0f : Mathf.Abs(Mathf.Sin(t * 4.2f + f.fase)) * 0.45f; Vector3 pos = f.go.transform.position; pos.y = Chao3(pos) + pulo; f.go.transform.position = pos; }
        };
        cenas[7].cam3 = k =>
        {
            float e = Suave(k / 0.85f); Vector3 centro = jp - ffrente * 1.2f + Vector3.up * 1.6f;
            Vector3 cp = centro + ffrente * Mathf.Lerp(8f, 14f, e) + Vector3.up * Mathf.Lerp(2.4f, 95f, e * e);
            Vector3 olhaGrupo = centro, olhaCeu = cp + (ffrente * 0.38f + Vector3.up).normalized * 60f;
            Cam(cp, Vector3.Lerp(olhaGrupo, olhaCeu, Suave((k - 0.2f) / 0.65f)), Mathf.Lerp(48f, 62f, e));
        };
        cenas[7].hud = k =>
        {
            if (k > 0.5f) Titulo("PINGLU RPG", 400f, 150f, Suave((k - 0.5f) / 0.18f) * (1f - Suave((k - 0.84f) / 0.1f)), "A lenda de Valverde");
            if (k > 0.82f) { float a = Suave((k - 0.82f) / 0.17f); Grad(-800f, -600f, 2720f, 1500f, C(0f, 0f, 0f, a), C(0f, 0f, 0f, a)); }        // a tela escurece até ficar toda preta
        };
    }
}
