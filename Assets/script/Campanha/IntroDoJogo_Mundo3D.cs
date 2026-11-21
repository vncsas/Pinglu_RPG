using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;

// ABERTURA EM 3D: em vez de silhuetas desenhadas, a câmera voa pelo MUNDO REAL do jogo (vilarejo, cemitério, castelo, pântano, montanha),
// com o modelo 3D do dragão, os heróis de verdade, fogo, relâmpagos e luzes/sombras reais. O texto, o tempo e as barras continuam os mesmos.
// Se o mundo não for encontrado, a abertura antiga (2D) toca como reserva.
public partial class IntroDoJogo
{
    // clima de uma cena: sol, céu, neblina e luz ambiente
    class Amb { public Color sol, fog, amb, ceu; public float solInt, fogDen, exposicao = 1f, atmosfera = 1f; public Vector3 solRot; }

    bool modo3D; Camera cam3; Light sol3;
    Vector3 vila, cemiterio, castelo, pantano, montanha;
    DragaoDistante dragaoOrigem, dragao3; GameObject dragaoObj;
    readonly List<GameObject> lixo3 = new List<GameObject>();      // tudo da cena atual (apagado ao sair dela)
    readonly List<PlayableGraph> grafos3 = new List<PlayableGraph>();
    readonly List<GameObject> herois3 = new List<GameObject>();
    readonly List<FichaPersonagem> fichas3 = new List<FichaPersonagem>();
    readonly List<GameObject> cristais3 = new List<GameObject>();
    float proxRaio3, flash3; MenuRede menuEscondido;
    Vector3 dirIn3 = Vector3.forward;                              // do vilarejo para o meio do mapa (a câmera fica do lado de fora olhando para dentro: sem ver a borda do mapa)
    readonly List<Bounds> obst3 = new List<Bounds>();             // casas, árvores e muros: a câmera e os heróis desviam deles
    Vector3 Rg3 { get { return Vector3.Cross(Vector3.up, dirIn3); } }
    Vector3 Fr3(Vector3 p, float dir, float fre) { return p + Rg3 * dir + dirIn3 * fre; }
    // ambiente original (restaurado no fim)
    Color oFog, oAmb; float oFogDen, oAmbInt; bool oFogOn; UnityEngine.Rendering.AmbientMode oAmbMode; FogMode oFogMode;
    Color oSolC; float oSolI; Quaternion oSolR; Material oCeu, ceu3; bool ambSalvo;

    static float Chao3(Vector3 p) { Terrain t = Terrain.activeTerrain; return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y; }
    static Vector3 G3(Vector3 p, float h = 0f) { p.y = Chao3(p) + h; return p; }
    static Vector3 Sv(Vector3 a, Vector3 b, float k) { return Vector3.Lerp(a, b, Suave(k)); }

    // ------------------------------------------------------------------ preparação
    void Preparar3D()
    {
        try
        {
            Terrain terr = Terrain.activeTerrain; if (terr == null) return;
            NpcFalante caio = null;
            foreach (NpcFalante n in FindObjectsByType<NpcFalante>(FindObjectsSortMode.None)) if (n != null && n.nomeExibido == "Caio") { caio = n; break; }
            PortalDoMundo pm = PortalDoMundo.Atual;
            if (caio != null) vila = caio.transform.position; else if (pm != null) vila = pm.transform.position; else return;
            if (Mathf.Abs(vila.y) > 1500f) return;                                   // o palco do lobby fica a -3000 m: isso não é o mundo
            Vector3 tc = terr.transform.position + terr.terrainData.size * 0.5f; Vector3 di = tc - vila; di.y = 0f;
            if (di.sqrMagnitude > 25f) dirIn3 = di.normalized;
            obst3.Clear();
            foreach (Renderer rd in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (rd == null || rd is ParticleSystemRenderer || !rd.enabled) continue;
                Bounds b = rd.bounds; if (b.size.y < 2.5f || b.size.x > 90f || b.size.z > 90f) continue;
                if (Mathf.Abs(b.center.y) > 1500f) continue;
                obst3.Add(b);
            }
            PracaDoGuardiao pr = FindAnyObjectByType<PracaDoGuardiao>();
            cemiterio = pr != null ? pr.Centro() : G3(vila + new Vector3(180f, 0f, 60f));
            EncontroDoCastelo ec = EncontroDoCastelo.Instancia;
            castelo = ec != null && ec.estrutura != null ? ec.estrutura.position : G3(vila + new Vector3(-200f, 0f, 150f));
            GameObject lago = GameObject.Find("Lago do Pantano"); Transform ag = lago != null ? lago.transform.Find("Agua") : null; Renderer ra = ag != null ? ag.GetComponent<Renderer>() : null;
            pantano = ra != null ? ra.bounds.center : G3(vila + new Vector3(60f, 0f, 250f));
            EncontroDoDragao ed = FindAnyObjectByType<EncontroDoDragao>();
            montanha = G3(ed != null ? ed.posicaoMontanha : new Vector3(830f, 0f, 130f));
            dragaoOrigem = FindAnyObjectByType<DragaoDistante>(FindObjectsInactive.Include);
            MenuRede menu = FindAnyObjectByType<MenuRede>();
            if (menu != null)
            {
                FieldInfo f = menu.GetType().GetField("personagens", BindingFlags.NonPublic | BindingFlags.Instance);
                List<FichaPersonagem> todas = f != null ? f.GetValue(menu) as List<FichaPersonagem> : null;
                if (todas != null) foreach (FichaPersonagem p in todas) if (p != null && p.disponivel && p.modeloVisual != null) fichas3.Add(p);
            }
            // câmera própria, por cima de todas, com pós-processamento (brilho do fogo)
            GameObject g = new GameObject("Câmera da abertura"); cam3 = g.AddComponent<Camera>();
            cam3.fieldOfView = 55f; cam3.nearClipPlane = 0.3f; cam3.farClipPlane = 3500f; cam3.clearFlags = CameraClearFlags.Skybox; cam3.allowHDR = true;
            float d = 0f; foreach (Camera c in Camera.allCameras) d = Mathf.Max(d, c.depth); cam3.depth = d + 20f;
            cam3.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            FxCena.CamOverride = cam3;
            sol3 = RenderSettings.sun;
            if (sol3 == null) foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sol3 = l; break; }
            // salva o ambiente
            oFogOn = RenderSettings.fog; oFog = RenderSettings.fogColor; oFogDen = RenderSettings.fogDensity; oFogMode = RenderSettings.fogMode;
            oAmbMode = RenderSettings.ambientMode; oAmb = RenderSettings.ambientLight; oAmbInt = RenderSettings.ambientIntensity; oCeu = RenderSettings.skybox;
            if (sol3 != null) { oSolC = sol3.color; oSolI = sol3.intensity; oSolR = sol3.transform.rotation; }
            ambSalvo = true;
            Shader sh = Shader.Find("Skybox/Procedural"); if (sh != null) ceu3 = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            if (seqId == "final") AtribuirCenasFinal3D(); else if (seqId == "morte3d") AtribuirCenasMorte3D(); else AtribuirCenas3D();
            menuEscondido = menu; if (menuEscondido != null) menuEscondido.enabled = false;      // a seleção de personagens não aparece durante a abertura
            modo3D = true; Entra3D();
            Debug.Log("[Abertura 3D] ligada. vila=" + vila.ToString("F0") + " cemitério=" + cemiterio.ToString("F0") + " castelo=" + castelo.ToString("F0") + " pântano=" + pantano.ToString("F0") + " montanha=" + montanha.ToString("F0") + " dragão=" + (dragaoOrigem != null && dragaoOrigem.modelo != null) + " heróis=" + fichas3.Count);
        }
        catch (Exception ex) { Debug.LogWarning("[Abertura 3D] não foi possível preparar: " + ex.Message + " (usando a abertura em 2D)"); Limpar3D(); modo3D = false; }
    }

    void Aviso3D() { if (!modo3D) Debug.Log("[Abertura 3D] mundo (terreno/Caio/portal) não encontrado nesta cena; usando a abertura em 2D."); }
    void Entra3D() { if (!modo3D || atual < 0 || atual >= cenas.Count) return; Cena c = cenas[atual]; if (c.entra != null) c.entra(); }
    void Sai3D()
    {
        if (!modo3D) return;
        if (atual >= 0 && atual < cenas.Count && cenas[atual].sai != null) cenas[atual].sai();
        foreach (GameObject o in lixo3) if (o != null) Destroy(o);
        lixo3.Clear(); herois3.Clear(); cristais3.Clear();
        foreach (PlayableGraph pg in grafos3) if (pg.IsValid()) pg.Destroy();
        grafos3.Clear();
        if (dragaoObj != null) Destroy(dragaoObj); dragaoObj = null; dragao3 = null;
    }

    void Limpar3D()
    {
        Sai3D(); modo3D = false;
        foreach (GameObject o in povoObj) if (o != null) Destroy(o);
        povoObj.Clear();
        if (realCaio != null) { DialogoUI.Registrar("Caio", realCaio); realCaio = null; }          // o Caio de verdade volta a ser o dono das falas
        if (menuEscondido != null) { menuEscondido.enabled = true; menuEscondido = null; }
        if (cam3 != null) Destroy(cam3.gameObject); cam3 = null; FxCena.CamOverride = null;
        if (ambSalvo)
        {
            RenderSettings.fog = oFogOn; RenderSettings.fogColor = oFog; RenderSettings.fogDensity = oFogDen; RenderSettings.fogMode = oFogMode;
            RenderSettings.ambientMode = oAmbMode; RenderSettings.ambientLight = oAmb; RenderSettings.ambientIntensity = oAmbInt; RenderSettings.skybox = oCeu;
            if (sol3 != null) { sol3.color = oSolC; sol3.intensity = oSolI; sol3.transform.rotation = oSolR; }
            ambSalvo = false;
        }
    }

    void OnDestroy() { if (modo3D || ambSalvo) Limpar3D(); }

    // ------------------------------------------------------------------ a cada quadro: câmera e clima da cena atual
    void LateUpdate()
    {
        if (!modo3D || cam3 == null || atual >= cenas.Count) return;
        Cena c = cenas[atual]; float t = Time.unscaledTime - inicioCena; float k = Mathf.Clamp01(Mathf.Max(0f, t) / Mathf.Max(0.1f, c.dur));
        if (c.amb != null) AplicarAmb(c.amb);
        if (c.vivo != null) c.vivo(k);
        if (c.cam3 != null) c.cam3(k);
        if (flash3 > 0f) { flash3 = Mathf.Max(0f, flash3 - Time.unscaledDeltaTime * 4f); if (sol3 != null) sol3.intensity += flash3 * 4f; RenderSettings.ambientLight += Color.white * flash3 * 0.6f; }
    }

    void AplicarAmb(Amb a)
    {
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = a.fog; RenderSettings.fogDensity = a.fogDen;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = a.amb; RenderSettings.ambientIntensity = 1f;
        if (sol3 != null) { sol3.color = a.sol; sol3.intensity = a.solInt; sol3.transform.rotation = Quaternion.Euler(a.solRot); RenderSettings.sun = sol3; }
        if (ceu3 != null)
        {
            ceu3.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f)); ceu3.SetFloat("_Exposure", a.exposicao); ceu3.SetFloat("_AtmosphereThickness", a.atmosfera); ceu3.SetFloat("_SunSize", 0.06f);
            ceu3.SetColor("_GroundColor", a.fog); RenderSettings.skybox = ceu3;
        }
    }

    // se a câmera estiver dentro de uma casa/árvore, sobe até passar por cima
    Vector3 Livre3(Vector3 p)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            bool moveu = false;
            for (int i = 0; i < obst3.Count; i++)
            {
                Bounds b = obst3[i]; Bounds e = new Bounds(b.center, b.size + new Vector3(3f, 3f, 3f));
                if (e.Contains(p)) { p.y = b.max.y + 2.5f; moveu = true; }
            }
            if (!moveu) break;
        }
        return p;
    }
    // ponto aberto (sem casas nem árvores) a "raio" metros de um centro, preferindo o lado de fora do mapa
    Vector3 Aberto3(Vector3 centro, float raio)
    {
        Terrain t = Terrain.activeTerrain; Vector3 melhor = G3(centro - dirIn3 * raio); float melhorNota = 1e9f;
        for (int i = 0; i < 36; i++)
        {
            float ang = (i % 2 == 0 ? 1 : -1) * (i / 2) * 10f; Vector3 d = Quaternion.Euler(0f, ang, 0f) * (-dirIn3);
            Vector3 q = centro + d * raio; q.y = Chao3(q);
            if (t != null) { Vector3 o = t.transform.position, sz = t.terrainData.size; if (q.x < o.x + 40f || q.z < o.z + 40f || q.x > o.x + sz.x - 40f || q.z > o.z + sz.z - 40f) continue; }
            float nota = 0f;
            for (int j = 0; j < obst3.Count; j++) { Bounds b = obst3[j]; float dx = Mathf.Max(0f, Mathf.Abs(q.x - b.center.x) - b.extents.x), dz = Mathf.Max(0f, Mathf.Abs(q.z - b.center.z) - b.extents.z); if (dx * dx + dz * dz < 64f) nota += 1f; }
            nota += Mathf.Abs(ang) * 0.01f;
            if (nota < melhorNota) { melhorNota = nota; melhor = q; if (nota < 0.2f) break; }
        }
        return melhor;
    }

    void Cam(Vector3 pos, Vector3 alvo, float fov)
    {
        if (cam3 == null) return;
        pos = Livre3(pos);
        pos.y = Mathf.Max(pos.y, Chao3(pos) + 1.2f);                 // a câmera nunca passa para dentro do terreno
        cam3.transform.position = pos; Vector3 d = alvo - pos; if (d.sqrMagnitude > 0.001f) cam3.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        cam3.fieldOfView = fov;
    }

    // ------------------------------------------------------------------ peças reutilizáveis
    class Tremeluz : MonoBehaviour
    {
        public float baseI = 4f, fase;
        Light l; void Awake() { l = GetComponent<Light>(); fase = UnityEngine.Random.value * 10f; }
        void Update() { if (l != null) l.intensity = baseI * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 17f + fase) + 0.12f * Mathf.Sin(Time.unscaledTime * 31f + fase * 2f)); }
    }
    class Flutua : MonoBehaviour
    {
        public Vector3 baseP; public float amp = 0.4f, vel = 1.2f, fase; void Start() { baseP = transform.position; fase = UnityEngine.Random.value * 6f; }
        void Update() { transform.position = baseP + Vector3.up * Mathf.Sin(Time.unscaledTime * vel + fase) * amp; }
    }

    ParticleSystem NovoPS(Transform pai, string nome, Texture2D tex, Color cor, float vida, float vel, float tam, float taxa, float grav, bool cresce, float raio)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(pai, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>(); ps.Stop();
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material m = new Material(FxCena.Sprite()); m.mainTexture = tex; r.sharedMaterial = m;
        var main = ps.main; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = vida; main.startSpeed = vel;
        main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.6f, tam); main.startColor = cor; main.gravityModifier = grav; main.maxParticles = 500;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        var em = ps.emission; em.rateOverTime = taxa;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = raio;
        var col = ps.colorOverLifetime; col.enabled = true; Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.55f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, cresce ? AnimationCurve.Linear(0f, 0.4f, 1f, 1.8f) : AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        ps.Play(); return ps;
    }

    GameObject Fogueira(Vector3 pos, float esc)
    {
        GameObject g = new GameObject("Fogo da abertura"); g.transform.position = pos; lixo3.Add(g);
        NovoPS(g.transform, "Chama", FxCena.TexBrilho(), new Color(1f, 0.55f, 0.15f, 0.95f), 1.2f, 2.4f * esc, 2.6f * esc, 34f, -0.25f, false, 0.9f * esc);
        NovoPS(g.transform, "Chama interna", FxCena.TexBrilho(), new Color(1f, 0.9f, 0.5f, 0.9f), 0.8f, 2f * esc, 1.3f * esc, 20f, -0.3f, false, 0.4f * esc);
        NovoPS(g.transform, "Fumaça", FxCena.TexNuvem(), new Color(0.09f, 0.08f, 0.08f, 0.6f), 5f, 3f * esc, 4.5f * esc, 7f, -0.12f, true, 1f * esc);
        GameObject lg = new GameObject("Luz"); lg.transform.SetParent(g.transform, false); lg.transform.localPosition = Vector3.up * 2.5f * esc;
        Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.range = 26f * esc; l.intensity = 4f; l.shadows = LightShadows.None;
        lg.AddComponent<Tremeluz>().baseI = 5f; return g;
    }

    GameObject Cristal3(Vector3 pos, Color cor, float tam)
    {
        GameObject c = FxCena.Cristal(cor, tam, null); c.transform.position = pos; lixo3.Add(c); cristais3.Add(c);
        Flutua f = c.AddComponent<Flutua>(); f.amp = 0.35f; return c;
    }

    // o dragão de verdade (o mesmo modelo do lobby), voando em círculos em volta de um ponto
    void CriarDragao3(Vector3 centro, Vector2 raios, float alt, float intervaloSopro)
    {
        if (dragaoOrigem == null) return;
        dragaoObj = new GameObject("Dragão da abertura"); dragaoObj.transform.position = centro;
        DragaoDistante d = dragaoObj.AddComponent<DragaoDistante>();
        d.modelo = dragaoOrigem.modelo; d.animVoo = dragaoOrigem.animVoo; d.animPlanar = dragaoOrigem.animPlanar; d.animSopro = dragaoOrigem.animSopro;
        d.rotacaoDoModelo = dragaoOrigem.rotacaoDoModelo; d.orientarAutomaticamente = dragaoOrigem.orientarAutomaticamente; d.comprimentoDoModelo = 48f;
        d.corDasEscamas = dragaoOrigem.corDasEscamas; d.corDaMembrana = dragaoOrigem.corDaMembrana; d.tamanho = dragaoOrigem.tamanho; d.materialParticula = dragaoOrigem.materialParticula;
        d.centroDoVoo = Vector3.zero; d.raiosDoVoo = raios; d.alturaDoVoo = alt; d.velocidadeDoVoo = 15f; d.intervaloDoSopro = intervaloSopro;
        dragao3 = d;
    }
    Vector3 PosDragao3(Vector3 padrao)
    {
        if (dragaoObj == null) return padrao;
        Transform c = dragaoObj.transform.Find("Dragao 3D"); return c != null ? c.position : padrao;
    }

    GameObject Heroi3(int i, Vector3 pos, Quaternion rot, bool andando)
    {
        if (fichas3.Count == 0) return null;
        FichaPersonagem f = fichas3[i % fichas3.Count];
        GameObject h = Instantiate(f.modeloVisual, pos, rot); lixo3.Add(h); herois3.Add(h);
        foreach (Collider cl in h.GetComponentsInChildren<Collider>()) cl.enabled = false;
        AnimationClip clip = andando ? (f.animacaoAndar != null ? f.animacaoAndar : f.animacaoParado) : f.animacaoParado;
        if (clip != null)
        {
            Animator an = h.GetComponent<Animator>(); if (an == null) an = h.AddComponent<Animator>();
            PlayableGraph pg = PlayableGraph.Create("abertura heroi"); pg.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            AnimationPlayableOutput o = AnimationPlayableOutput.Create(pg, "a", an); o.SetSourcePlayable(AnimationClipPlayable.Create(pg, clip)); pg.Play(); grafos3.Add(pg);
        }
        return h;
    }

    void Relampago(Vector3 centro, float raio)
    {
        if (Time.unscaledTime < proxRaio3) return;
        proxRaio3 = Time.unscaledTime + UnityEngine.Random.Range(1.2f, 3.5f);
        Vector2 o = UnityEngine.Random.insideUnitCircle * raio; Vector3 p = G3(centro + new Vector3(o.x, 0f, o.y));
        FxCena.Raio(p + Vector3.up * 220f, p, 3f, 0.3f, new Color(0.85f, 0.9f, 1f, 1f)); flash3 = 1f;
    }

    // ------------------------------------------------------------------ as 10 cenas em 3D
    void AtribuirCenas3D()
    {
        Vector3 v = vila;
        // 0) a ilha ao amanhecer
        cenas[0].amb = new Amb { sol = new Color(1f, 0.78f, 0.55f), solInt = 1.7f, solRot = new Vector3(11f, 55f, 0f), fog = new Color(0.75f, 0.72f, 0.8f), fogDen = 0.0022f, amb = new Color(0.5f, 0.5f, 0.6f), ceu = new Color(0.55f, 0.5f, 0.75f), exposicao = 1.1f, atmosfera = 1.6f };
        cenas[0].cam3 = k => Cam(Sv(G3(Fr3(v, -60f, -340f), 150f), G3(Fr3(v, -25f, -125f), 42f), k), G3(v, 10f), Mathf.Lerp(52f, 42f, k));
        // 1) o dragão incendeia a vila
        cenas[1].amb = new Amb { sol = new Color(1f, 0.45f, 0.2f), solInt = 1.7f, solRot = new Vector3(7f, -70f, 0f), fog = new Color(0.34f, 0.14f, 0.09f), fogDen = 0.0042f, amb = new Color(0.42f, 0.2f, 0.15f), ceu = new Color(0.85f, 0.35f, 0.22f), exposicao = 0.95f, atmosfera = 2.6f };
        cenas[1].entra = () =>
        {
            CriarDragao3(v, new Vector2(60f, 45f), 58f, 4.5f);
            for (int i = 0; i < 18; i++) { Vector2 o = UnityEngine.Random.insideUnitCircle * 75f; Fogueira(G3(v + new Vector3(o.x, 0f, o.y)), UnityEngine.Random.Range(1.2f, 2.4f)); }
        };
        cenas[1].cam3 = k => Cam(Sv(G3(Fr3(v, -70f, -85f), 16f), G3(Fr3(v, 70f, -75f), 24f), k), PosDragao3(v + Vector3.up * 55f), Mathf.Lerp(62f, 52f, k));
        // 2) o herói enfrenta o dragão (relâmpagos)
        cenas[2].amb = new Amb { sol = new Color(0.55f, 0.6f, 0.85f), solInt = 0.5f, solRot = new Vector3(40f, 20f, 0f), fog = new Color(0.12f, 0.13f, 0.18f), fogDen = 0.0055f, amb = new Color(0.2f, 0.22f, 0.32f), ceu = new Color(0.3f, 0.32f, 0.45f), exposicao = 0.55f, atmosfera = 1.2f };
        Vector3 hp = Aberto3(v, 62f); Vector3 hf = v - hp; hf.y = 0f; hf = hf.sqrMagnitude > 1f ? hf.normalized : dirIn3;
        cenas[2].entra = () =>
        {
            CriarDragao3(v, new Vector2(34f, 26f), 38f, 4f);
            Heroi3(0, hp, Quaternion.LookRotation(hf), false);
            for (int i = 0; i < 10; i++) { Vector2 o = UnityEngine.Random.insideUnitCircle * 40f; Fogueira(G3(v + new Vector3(o.x, 0f, o.y)), UnityEngine.Random.Range(0.8f, 1.6f)); }
        };
        cenas[2].vivo = k => Relampago(v, 60f);
        cenas[2].cam3 = k => { float a = Mathf.Lerp(-25f, 22f, Suave(k)) * Mathf.Deg2Rad; Vector3 r = Vector3.Cross(Vector3.up, hf); Vector3 baixo = hp - hf * Mathf.Cos(a) * 6.5f + r * Mathf.Sin(a) * 6.5f + Vector3.up * (1.6f + 0.7f * k); Cam(baixo, Vector3.Lerp(hp + Vector3.up * 1.6f, PosDragao3(v + Vector3.up * 38f), 0.65f), Mathf.Lerp(58f, 48f, k)); };
        // 3) os três cristais
        cenas[3].amb = new Amb { sol = new Color(0.3f, 0.4f, 0.8f), solInt = 0.15f, solRot = new Vector3(50f, 10f, 0f), fog = new Color(0.03f, 0.04f, 0.09f), fogDen = 0.011f, amb = new Color(0.1f, 0.12f, 0.22f), ceu = new Color(0.2f, 0.2f, 0.4f), exposicao = 0.18f, atmosfera = 1f };
        Vector3 cc = G3(Aberto3(v, 48f), 3.4f);
        cenas[3].entra = () =>
        {
            Color[] cores = { new Color(0.35f, 0.65f, 1f), new Color(0.3f, 1f, 0.6f), new Color(0.8f, 0.45f, 1f) };
            for (int i = 0; i < 3; i++) { float a = i * 2.094f; Cristal3(cc + new Vector3(Mathf.Cos(a) * 2.6f, 0f, Mathf.Sin(a) * 2.6f), cores[i], 0.9f); }
            Heroi3(0, G3(cc + new Vector3(-6f, 0f, -4f)), Quaternion.LookRotation(new Vector3(6f, 0f, 4f)), false);
        };
        cenas[3].cam3 = k => Cam(Sv(cc + new Vector3(0f, 0.4f, -12f), cc + new Vector3(3f, 1.8f, -6.5f), k), cc, Mathf.Lerp(50f, 42f, k));
        // 4) o cemitério (cristal azul)
        cenas[4].amb = new Amb { sol = new Color(0.5f, 0.6f, 0.95f), solInt = 0.3f, solRot = new Vector3(35f, 200f, 0f), fog = new Color(0.1f, 0.14f, 0.19f), fogDen = 0.012f, amb = new Color(0.14f, 0.17f, 0.26f), ceu = new Color(0.25f, 0.3f, 0.5f), exposicao = 0.3f, atmosfera = 1.1f };
        cenas[4].entra = () => Cristal3(cemiterio + Vector3.up * 2.4f, new Color(0.35f, 0.65f, 1f), 0.8f);
        cenas[4].cam3 = k => { float a = Mathf.Lerp(200f, 265f, Suave(k)) * Mathf.Deg2Rad, r = Mathf.Lerp(24f, 16f, k); Cam(G3(cemiterio + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r), Mathf.Lerp(6f, 3f, k)), cemiterio + Vector3.up * 2f, 52f); };
        // 5) o castelo (cristal verde)
        cenas[5].amb = new Amb { sol = new Color(1f, 0.6f, 0.35f), solInt = 1.2f, solRot = new Vector3(8f, -120f, 0f), fog = new Color(0.3f, 0.22f, 0.2f), fogDen = 0.0048f, amb = new Color(0.35f, 0.28f, 0.28f), ceu = new Color(0.8f, 0.45f, 0.3f), exposicao = 0.9f, atmosfera = 2.2f };
        cenas[5].entra = () => Cristal3(castelo + Vector3.up * 9f, new Color(0.3f, 1f, 0.6f), 0.9f);
        cenas[5].cam3 = k => Cam(Sv(G3(castelo + new Vector3(0f, 0f, -75f), 4f), G3(castelo + new Vector3(28f, 0f, -62f), 38f), k), castelo + Vector3.up * Mathf.Lerp(8f, 14f, k), Mathf.Lerp(55f, 46f, k));
        // 6) o pântano (cristal roxo sob a água)
        cenas[6].amb = new Amb { sol = new Color(0.5f, 0.8f, 0.6f), solInt = 0.4f, solRot = new Vector3(25f, 100f, 0f), fog = new Color(0.09f, 0.2f, 0.14f), fogDen = 0.02f, amb = new Color(0.12f, 0.2f, 0.16f), ceu = new Color(0.3f, 0.45f, 0.4f), exposicao = 0.4f, atmosfera = 1f };
        cenas[6].entra = () => Cristal3(pantano + Vector3.up * 0.3f, new Color(0.8f, 0.45f, 1f), 0.9f);
        cenas[6].cam3 = k => Cam(Sv(pantano + new Vector3(42f, 2.6f, -30f), pantano + new Vector3(20f, 2.2f, -14f), k), pantano + Vector3.up * 0.8f, 56f);
        // 7) a corrupção: cada cristal no SEU lugar (cemitério, castelo e pântano), um de cada vez, ficando com a aura estranha
        cenas[7].dur = 13.5f;
        cenas[7].amb = new Amb { sol = new Color(0.9f, 0.2f, 0.25f), solInt = 0.6f, solRot = new Vector3(30f, 60f, 0f), fog = new Color(0.14f, 0.04f, 0.07f), fogDen = 0.0085f, amb = new Color(0.22f, 0.08f, 0.13f), ceu = new Color(0.5f, 0.1f, 0.18f), exposicao = 0.35f, atmosfera = 2.8f };
        Vector3[] pCrist = { cemiterio + Vector3.up * 2.4f, castelo + Vector3.up * 9f, pantano + Vector3.up * 0.4f };
        Color[] corCrist = { new Color(0.35f, 0.65f, 1f), new Color(0.3f, 1f, 0.6f), new Color(0.8f, 0.45f, 1f) };
        int[] fumacaDe = { 0, 0, 0 }; ParticleSystem[] fumacas = new ParticleSystem[3]; ParticleSystem[] fagulhas = new ParticleSystem[3];
        cenas[7].entra = () =>
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject c = Cristal3(pCrist[i], corCrist[i], 0.9f);
                fumacas[i] = NovoPS(c.transform, "Fumaça negra", FxCena.TexNuvem(), new Color(0.03f, 0f, 0.05f, 0.75f), 4f, 2.5f, 3.2f, 0f, -0.1f, true, 1.2f);
                fagulhas[i] = NovoPS(c.transform, "Faíscas escuras", FxCena.TexBrilho(), new Color(0.95f, 0.1f, 0.2f, 0.95f), 1.6f, 4f, 0.5f, 0f, -0.15f, false, 0.8f);
            }
        };
        cenas[7].vivo = k =>
        {
            int sh = Mathf.Min(2, (int)(k * 3f)); float kk = k * 3f - sh;
            if (sh != fumacaDe[0]) { fumacaDe[0] = sh; flash3 = 0.7f; }                    // um clarão a cada troca de lugar
            for (int i = 0; i < cristais3.Count && i < 3; i++)
            {
                GameObject cr = cristais3[i]; if (cr == null) continue;
                float p = i < sh ? 1f : (i == sh ? Suave(kk) : 0f);                          // quanto esse cristal já está corrompido
                Color cor = Color.Lerp(corCrist[i], new Color(0.9f, 0.05f, 0.12f), p * 0.92f);
                Light l = cr.GetComponentInChildren<Light>(); if (l != null) { l.color = cor; l.intensity = 3f + 6f * p * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 18f + i)); }
                MeshRenderer mr = cr.GetComponentInChildren<MeshRenderer>(); if (mr != null && mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", cor);
                cr.transform.localScale = Vector3.one * (1f + 0.06f * p * Mathf.Sin(Time.unscaledTime * 25f + i));       // treme quando corrompe
                if (fumacas[i] != null) { var em = fumacas[i].emission; em.rateOverTime = 26f * p; }
                if (fagulhas[i] != null) { var em = fagulhas[i].emission; em.rateOverTime = 40f * p; }
            }
            if (sh < 3 && Time.unscaledTime >= proxRaio3 && kk > 0.35f) { proxRaio3 = Time.unscaledTime + 0.7f; Vector3 a = pCrist[sh] + UnityEngine.Random.onUnitSphere * 7f; FxCena.Raio(a + Vector3.up * 40f, pCrist[sh], 1.4f, 0.2f, new Color(0.95f, 0.2f, 0.3f, 1f)); }
        };
        cenas[7].cam3 = k =>
        {
            int sh = Mathf.Min(2, (int)(k * 3f)); float kk = k * 3f - sh; Vector3 c = pCrist[sh];
            float ang = (sh * 120f + 30f + kk * 45f) * Mathf.Deg2Rad, dist = Mathf.Lerp(12f, 6.5f, Suave(kk));
            Cam(c + new Vector3(Mathf.Sin(ang) * dist, 1.2f + kk * 1.5f, Mathf.Cos(ang) * dist), c, Mathf.Lerp(52f, 42f, kk));
        };
        // 8) os mortos acordam (cemitério, fogos-fátuos)
        cenas[8].amb = new Amb { sol = new Color(0.55f, 0.65f, 0.95f), solInt = 0.35f, solRot = new Vector3(30f, 230f, 0f), fog = new Color(0.12f, 0.17f, 0.18f), fogDen = 0.016f, amb = new Color(0.15f, 0.2f, 0.24f), ceu = new Color(0.25f, 0.32f, 0.45f), exposicao = 0.3f, atmosfera = 1.1f };
        cenas[8].entra = () =>
        {
            for (int i = 0; i < 22; i++)
            {
                Vector2 o = UnityEngine.Random.insideUnitCircle * 26f; GameObject w = FxCena.Nuvem(G3(cemiterio + new Vector3(o.x, 0f, o.y), UnityEngine.Random.Range(0.8f, 3.2f)), 1.6f, new Color(0.6f, 1f, 0.8f, 0.8f), FxCena.TexBrilho(), 8);
                lixo3.Add(w); Light l = w.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.5f, 1f, 0.75f); l.range = 7f; l.intensity = 1.6f; l.shadows = LightShadows.None; w.AddComponent<Flutua>().amp = 0.6f;
            }
            Cristal3(cemiterio + Vector3.up * 2.4f, new Color(0.9f, 0.2f, 0.3f), 0.6f);
        };
        cenas[8].cam3 = k => Cam(Sv(G3(cemiterio + new Vector3(30f, 0f, 26f), 1.5f), G3(cemiterio + new Vector3(9f, 0f, 10f), 1.5f), k), cemiterio + Vector3.up * 2f, 62f);
        // 9) os aventureiros chegam ao vilarejo pela estrada, passam pela placa de boas-vindas; a câmera vai se afastando pelas costas deles
        cenas[9].dur = 17f;
        cenas[9].amb = new Amb { sol = new Color(1f, 0.72f, 0.42f), solInt = 1.4f, solRot = new Vector3(6f, -85f, 0f), fog = new Color(0.5f, 0.36f, 0.28f), fogDen = 0.0028f, amb = new Color(0.45f, 0.36f, 0.32f), ceu = new Color(0.85f, 0.55f, 0.4f), exposicao = 1f, atmosfera = 2f };
        MontarCaminho3();
        float sA = Mathf.Max(0f, sPlaca3 - 26f), sB = Mathf.Min(CompCaminho3(), sPlaca3 + 14f);
        cenas[9].entra = () =>
        {
            for (int i = 0; i < 4; i++) Heroi3(i, PosHeroi3(i, sA), Quaternion.LookRotation(TgCaminho3(sA)), true);
        };
        cenas[9].vivo = k =>
        {
            float sL = Mathf.Lerp(sA, sB, k);                                          // andam em ritmo constante
            for (int i = 0; i < herois3.Count; i++)
            {
                if (herois3[i] == null) continue;
                herois3[i].transform.position = PosHeroi3(i, sL); herois3[i].transform.rotation = Quaternion.LookRotation(TgCaminho3(sL));
            }
        };
        cenas[9].cam3 = k =>
        {
            float e = Suave(k), sL = Mathf.Lerp(sA, sB, k); Vector3 L = PtCaminho3(sL), T = TgCaminho3(sL), R = Vector3.Cross(Vector3.up, T);
            float d = Mathf.Lerp(6.5f, 30f, e), h = Mathf.Lerp(1.6f, 12f, e);
            Vector3 cp = L - T * d + R * Mathf.Lerp(1.3f, -4f, e); cp.y = Chao3(cp) + h;
            Cam(cp, L + Vector3.up * Mathf.Lerp(1.5f, 4f, e) + T * Mathf.Lerp(6f, 22f, e), Mathf.Lerp(46f, 56f, e));
        };
    }

    // ------------------------------------------------------------------ o caminho de pedra até a vila (e a placa de boas-vindas no meio dele)
    readonly List<Vector3> caminho3 = new List<Vector3>(); float sPlaca3; const float PASSO3 = 2f;
    float CompCaminho3() { return (caminho3.Count - 1) * PASSO3; }
    Vector3 PtCaminho3(float s)
    {
        if (caminho3.Count == 0) return vila;
        float f = Mathf.Clamp(s / PASSO3, 0f, caminho3.Count - 1.001f); int i = (int)f; return G3(Vector3.Lerp(caminho3[i], caminho3[i + 1], f - i));
    }
    Vector3 TgCaminho3(float s)
    {
        Vector3 a = PtCaminho3(s - 3f), b = PtCaminho3(s + 3f), d = b - a; d.y = 0f; return d.sqrMagnitude > 0.01f ? d.normalized : dirIn3;
    }
    Vector3 PosHeroi3(int i, float sL)
    {
        float lado = (i % 2 == 0 ? -1f : 1f) * (0.9f + 0.45f * (i / 2)), atras = i * 1.3f; float s = sL - atras;
        return G3(PtCaminho3(s) + Vector3.Cross(Vector3.up, TgCaminho3(s)) * lado);
    }

    void MontarCaminho3()
    {
        caminho3.Clear(); sPlaca3 = 0f;
        GameObject gp = GameObject.Find("Placa de Boas-vindas"); Vector3 placa = gp != null ? gp.transform.position : vila;
        RedeDeEstradas rede = FindAnyObjectByType<RedeDeEstradas>();
        Vector3[] melhor = null; float melhorD = 1e9f;
        if (rede != null)
            foreach (RedeDeEstradas.Estrada e in rede.estradas)
            {
                if (e.pontos == null || e.pontos.Length < 2) continue;
                foreach (Vector3 q in e.pontos) { float d = new Vector2(q.x - placa.x, q.z - placa.z).magnitude; if (d < melhorD) { melhorD = d; melhor = e.pontos; } }
            }
        List<Vector3> bruto = new List<Vector3>();
        if (melhor != null && melhorD < 60f) bruto.AddRange(melhor);
        else { Vector3 a0 = Aberto3(vila, 125f); bruto.Add(a0); bruto.Add(vila); }
        // reamostra de 2 em 2 m
        List<Vector3> r = new List<Vector3>(); r.Add(bruto[0]); float resto = 0f;
        for (int i = 1; i < bruto.Count; i++)
        {
            Vector3 a = bruto[i - 1], b = bruto[i]; float len = Vector3.Distance(a, b); if (len < 0.01f) continue;
            float pos = PASSO3 - resto;
            while (pos <= len) { r.Add(Vector3.Lerp(a, b, pos / len)); pos += PASSO3; }
            resto = len - (pos - PASSO3);
        }
        if (r.Count < 12) { r.Clear(); r.Add(Aberto3(vila, 125f)); r.Add(vila); Vector3 a = r[0], b = r[1]; r.Clear(); float L = Vector3.Distance(a, b); for (float d = 0f; d <= L; d += PASSO3) r.Add(Vector3.Lerp(a, b, d / L)); }
        // ponto mais perto da placa e sentido (queremos andar em direção à vila)
        int ip = 0; float md = 1e9f;
        for (int i = 0; i < r.Count; i++) { float d = new Vector2(r[i].x - placa.x, r[i].z - placa.z).magnitude; if (d < md) { md = d; ip = i; } }
        int ja = Mathf.Min(r.Count - 1, ip + 10), jb = Mathf.Max(0, ip - 10);
        float da = new Vector2(r[ja].x - vila.x, r[ja].z - vila.z).magnitude, db = new Vector2(r[jb].x - vila.x, r[jb].z - vila.z).magnitude;
        if (db < da) { r.Reverse(); ip = r.Count - 1 - ip; }
        // só o trecho útil (para não guardar quilômetros de estrada)
        int ini = Mathf.Max(0, ip - 30), fim = Mathf.Min(r.Count - 1, ip + 25);
        for (int i = ini; i <= fim; i++) caminho3.Add(r[i]);
        sPlaca3 = (ip - ini) * PASSO3;
        if (gp == null) sPlaca3 = Mathf.Min(CompCaminho3(), 26f + 8f);     // sem placa: o "meio" do trecho
    }
}
