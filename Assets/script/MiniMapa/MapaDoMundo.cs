using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Mapa do mundo em 3D: o próprio mundo visto de cima, com as construções de
// verdade, nuvens sobre o que ainda não foi explorado, alfinetes nos lugares,
// lista de locais (clique para voar até lá) e progresso de exploração.
//   M ou Esc       abre/fecha          Tab           troca de aba
//   roda do mouse  aproxima            botão direito gira (ou Q/E)
//   botão esquerdo / WASD   arrasta    F             volta para você
public class MapaDoMundo : MonoBehaviour
{
    [Header("Referências (o menu Pinglu > 22 preenche)")]
    public MiniMapa miniMapa;
    public Material materialNuvens;
    public Texture2D pino, pulso;

    [Header("Câmera do mapa")]
    public float distanciaMinima = 45f;
    public float distanciaMaxima = 950f;
    public float distanciaInicial = 380f;
    public float duracaoDoVoo = 0.8f;
    public float alturaDasNuvens = 14f;

    [Header("Teclas")]
    public KeyCode tecla = KeyCode.M;
    public KeyCode teclaAba = KeyCode.Tab;
    public KeyCode teclaCentralizar = KeyCode.F;

    private enum Aba { Mundo, Vila, Pergaminho }
    private static readonly string[] NOMES_ABAS = { "MUNDO", "VILA", "PERGAMINHO" };
    private Aba aba = Aba.Mundo;

    private bool aberto, fechando;
    private float t;                      // 0 = câmera do jogo, 1 = mapa
    private Camera camMapa, camJogo;
    private Volume volume;
    private GameObject nuvens;
    private Texture2D texNuvens;
    private float[] ruidoNuvens;

    private Vector3 pivo, alvoPivo;
    private float yaw, alvoYaw, dist, alvoDist;
    private Vector3 posInicio; private Quaternion rotInicio;

    // o que é restaurado ao fechar
    private float escalaTempo; private bool neblina; private int mascaraJogo; private float lodBias;
    private CursorLockMode cursorAntes; private bool cursorVisivel;

    private Transform jogador;
    private readonly List<Transform> aliados = new List<Transform>();
    private ClimaDosBiomas biomas;
    private GUIStyle estTitulo, estItem, estPequeno, estBioma, estAba, estRotulo;
    private Rect rPainelEsq, rPainelDir, rAbas;
    private Vector3 ultimoMouse;
    private bool arrastando;

    public bool Aberto { get { return aberto; } }

    // ------------------------------------------------------------------
    private void Start()
    {
        biomas = FindFirstObjectByType<ClimaDosBiomas>();
        if (miniMapa == null) miniMapa = FindFirstObjectByType<MiniMapa>();
        if (miniMapa != null) miniMapa.abrirComTecla = false;

        GameObject gc = new GameObject("Camera do Mapa");
        gc.transform.SetParent(transform, false);
        camMapa = gc.AddComponent<Camera>();
        camMapa.enabled = false;
        camMapa.fieldOfView = 40f;
        camMapa.nearClipPlane = 1f;
        camMapa.farClipPlane = 4000f;
        camMapa.cullingMask = ~(1 << 31); // a camada 31 é só do retrato da interface
        camMapa.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        // pós-processamento do mapa: mais claro e nítido que o clima sombrio do jogo
        GameObject gv = new GameObject("Volume do Mapa");
        gv.transform.SetParent(transform, false);
        volume = gv.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 100f; volume.weight = 0f;
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments ca = perfil.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.9f); ca.saturation.Override(10f); ca.contrast.Override(8f);
        Vignette vg = perfil.Add<Vignette>(true); vg.intensity.Override(0.32f); vg.smoothness.Override(0.5f);
        Tonemapping tm = perfil.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
        volume.sharedProfile = perfil;

        CriarNuvens();
    }

    // camada de nuvens que segue o relevo, com buracos onde já foi explorado
    private void CriarNuvens()
    {
        Terrain ter = Terrain.activeTerrain;
        if (ter == null || materialNuvens == null || miniMapa == null || !miniMapa.usarNevoa) return;
        const int RES = 96;
        Vector3 o = ter.transform.position, s = ter.terrainData.size;
        Vector3[] v = new Vector3[(RES + 1) * (RES + 1)];
        Vector2[] uv = new Vector2[v.Length];
        for (int z = 0; z <= RES; z++)
            for (int x = 0; x <= RES; x++)
            {
                float u = x / (float)RES, w = z / (float)RES;
                Vector3 p = new Vector3(o.x + u * s.x, 0f, o.z + w * s.z);
                p.y = ter.SampleHeight(p) + o.y + alturaDasNuvens;
                v[z * (RES + 1) + x] = p; uv[z * (RES + 1) + x] = new Vector2(u, w);
            }
        int[] tri = new int[RES * RES * 6]; int k = 0;
        for (int z = 0; z < RES; z++)
            for (int x = 0; x < RES; x++)
            {
                int a = z * (RES + 1) + x;
                tri[k++] = a; tri[k++] = a + RES + 1; tri[k++] = a + 1;
                tri[k++] = a + 1; tri[k++] = a + RES + 1; tri[k++] = a + RES + 2;
            }
        Mesh m = new Mesh { name = "Nuvens do Mapa", vertices = v, uv = uv, triangles = tri };
        m.RecalculateBounds();
        nuvens = new GameObject("Nuvens do Mapa");
        nuvens.transform.SetParent(transform, false);
        nuvens.AddComponent<MeshFilter>().sharedMesh = m;
        MeshRenderer mr = nuvens.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(materialNuvens);
        mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        nuvens.SetActive(false);

        int n = miniMapa.ResolucaoNevoa;
        texNuvens = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        mr.sharedMaterial.SetTexture("_BaseMap", texNuvens);
        ruidoNuvens = new float[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                ruidoNuvens[y * n + x] = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.7f + Mathf.PerlinNoise(x * 0.17f + 9f, y * 0.17f + 3f) * 0.3f;
    }

    private void AtualizarNuvens()
    {
        if (texNuvens == null) return;
        Color32[] fog = miniMapa.PixelsNevoa;
        Color32[] px = new Color32[fog.Length];
        for (int i = 0; i < fog.Length; i++)
        {
            float e = fog[i].r / 255f, nn = ruidoNuvens[i];
            float a = (1f - e) * (0.82f + 0.18f * nn);
            Color c = Color.Lerp(new Color(0.09f, 0.10f, 0.13f), new Color(0.32f, 0.32f, 0.36f), nn);
            px[i] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(Mathf.Clamp01(a) * 255));
        }
        texNuvens.SetPixels32(px); texNuvens.Apply(false);
    }

    // ------------------------------------------------------------------
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!aberto)
        {
            if (Input.GetKeyDown(tecla) && !SessaoDeJogo.MenuAberto) Abrir();
            return;
        }
        if (!fechando && (Input.GetKeyDown(tecla) || Input.GetKeyDown(KeyCode.Escape))) { Fechar(); }

        // voo de abertura/fechamento
        t = Mathf.MoveTowards(t, fechando ? 0f : 1f, dt / duracaoDoVoo);
        volume.weight = Mathf.SmoothStep(0f, 1f, t);
        if (fechando && t <= 0f) { Restaurar(); return; }

        if (!fechando)
        {
            if (Input.GetKeyDown(teclaAba)) TrocarAba((Aba)(((int)aba + 1) % 3));
            if (miniMapa != null) miniMapa.PergaminhoAberto = aba == Aba.Pergaminho;
            if (aba != Aba.Pergaminho) Controles(dt);
        }

        float k = 1f - Mathf.Exp(-8f * dt);
        pivo = Vector3.Lerp(pivo, alvoPivo, k);
        yaw = Mathf.LerpAngle(yaw, alvoYaw, k);
        dist = Mathf.Lerp(dist, alvoDist, k);
        Terrain ter = Terrain.activeTerrain;
        if (ter != null) pivo.y = ter.SampleHeight(pivo) + ter.transform.position.y;

        float incl = Mathf.Lerp(42f, 70f, Mathf.InverseLerp(distanciaMinima, distanciaMaxima, dist));
        Quaternion rotMapa = Quaternion.Euler(incl, yaw, 0f);
        Vector3 posMapa = pivo - rotMapa * Vector3.forward * dist;
        float e = Mathf.SmoothStep(0f, 1f, t);
        if (camJogo != null) { posInicio = camJogo.transform.position; rotInicio = camJogo.transform.rotation; }
        camMapa.transform.SetPositionAndRotation(Vector3.Lerp(posInicio, posMapa, e), Quaternion.Slerp(rotInicio, rotMapa, e));
        camMapa.fieldOfView = Mathf.Lerp(camJogo != null ? camJogo.fieldOfView : 60f, 40f, e);
        AtualizarAliados();
    }

    private void Controles(float dt)
    {
        Vector3 mouse = Input.mousePosition;
        Vector2 mg = new Vector2(mouse.x, Screen.height - mouse.y);
        bool sobreUI = rPainelEsq.Contains(mg) || rPainelDir.Contains(mg) || rAbas.Contains(mg);

        float roda = Input.mouseScrollDelta.y;
        if (Mathf.Abs(roda) > 0.01f && !sobreUI) alvoDist = Mathf.Clamp(alvoDist * (1f - roda * 0.12f), distanciaMinima, distanciaMaxima);

        Vector3 delta = mouse - ultimoMouse;
        ultimoMouse = mouse;
        if (Input.GetMouseButtonDown(0)) arrastando = !sobreUI;
        if (!Input.GetMouseButton(0)) arrastando = false;
        Vector3 frente = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, direita = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        if (arrastando) alvoPivo -= (direita * delta.x + frente * delta.y) * (dist * 0.0016f);
        if (Input.GetMouseButton(1)) alvoYaw += delta.x * 0.25f;
        if (Input.GetKey(KeyCode.Q)) alvoYaw -= 70f * dt;
        if (Input.GetKey(KeyCode.E)) alvoYaw += 70f * dt;
        Vector3 mov = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) mov += frente; if (Input.GetKey(KeyCode.S)) mov -= frente;
        if (Input.GetKey(KeyCode.D)) mov += direita; if (Input.GetKey(KeyCode.A)) mov -= direita;
        alvoPivo += mov * dist * 0.9f * dt;
        if (Input.GetKeyDown(teclaCentralizar) && jogador != null) { alvoPivo = jogador.position; alvoDist = Mathf.Min(alvoDist, 220f); }

        Terrain ter = Terrain.activeTerrain;
        if (ter != null)
        {
            Vector3 o = ter.transform.position, s = ter.terrainData.size;
            alvoPivo.x = Mathf.Clamp(alvoPivo.x, o.x, o.x + s.x);
            alvoPivo.z = Mathf.Clamp(alvoPivo.z, o.z, o.z + s.z);
        }
    }

    private void Abrir()
    {
        camJogo = Camera.main;
        jogador = AcharJogador();
        if (camJogo == null || jogador == null) return;
        aberto = true; fechando = false; t = 0f; aba = Aba.Mundo;
        SessaoDeJogo.MenuAberto = true;
        escalaTempo = Time.timeScale; if (!SessaoDeJogo.Online) Time.timeScale = 0f;
        neblina = RenderSettings.fog; RenderSettings.fog = false;
        lodBias = QualitySettings.lodBias; QualitySettings.lodBias = Mathf.Max(lodBias, 2.5f);
        cursorAntes = Cursor.lockState; cursorVisivel = Cursor.visible;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

        alvoPivo = pivo = jogador.position;
        alvoYaw = yaw = camJogo.transform.eulerAngles.y;
        alvoDist = dist = distanciaInicial;
        ultimoMouse = Input.mousePosition;
        camMapa.depth = camJogo.depth + 5f;
        camMapa.enabled = true;
        mascaraJogo = camJogo.cullingMask; camJogo.cullingMask = 0; // o jogo para de desenhar o mundo por trás do mapa
        if (nuvens != null) { AtualizarNuvens(); nuvens.SetActive(true); }
    }

    private void Fechar()
    {
        fechando = true;
        if (miniMapa != null) miniMapa.PergaminhoAberto = false;
    }

    private void Restaurar()
    {
        aberto = false; fechando = false;
        camMapa.enabled = false;
        if (camJogo != null) camJogo.cullingMask = mascaraJogo;
        if (nuvens != null) nuvens.SetActive(false);
        volume.weight = 0f;
        Time.timeScale = escalaTempo <= 0f ? 1f : escalaTempo;
        RenderSettings.fog = neblina;
        QualitySettings.lodBias = lodBias;
        Cursor.lockState = cursorAntes; Cursor.visible = cursorVisivel;
        SessaoDeJogo.MenuAberto = false;
    }

    private void TrocarAba(Aba nova)
    {
        aba = nova;
        if (aba == Aba.Vila && miniMapa != null)
            foreach (MiniMapa.Marcador m in miniMapa.marcadores)
                if (m.tipo == MiniMapa.Tipo.Vila) { alvoPivo = m.posicao; alvoDist = 110f; }
        if (aba == Aba.Mundo) alvoDist = Mathf.Max(alvoDist, distanciaInicial);
    }

    private Transform AcharJogador()
    {
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
            if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.enabled) return p.transform;
        return null;
    }

    private void AtualizarAliados()
    {
        aliados.Clear();
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
            if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.transform != jogador) aliados.Add(p.transform);
    }

    // ------------------------------------------------------------------
    //  Interface
    // ------------------------------------------------------------------
    private void Estilos(float e)
    {
        Color bronze = new Color(0.93f, 0.76f, 0.45f), claro = new Color(0.93f, 0.89f, 0.80f);
        estTitulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * e), fontStyle = FontStyle.Bold };
        estTitulo.normal.textColor = bronze;
        estItem = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * e) };
        estItem.normal.textColor = claro;
        estPequeno = new GUIStyle(estItem) { fontSize = Mathf.RoundToInt(15 * e) };
        estPequeno.normal.textColor = new Color(0.8f, 0.76f, 0.68f);
        estBioma = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * e), fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
        estBioma.normal.textColor = new Color(0.96f, 0.88f, 0.70f);
        estAba = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19 * e), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        estRotulo = new GUIStyle(estItem) { fontSize = Mathf.RoundToInt(16 * e), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        estRotulo.normal.textColor = claro;
    }

    private void OnGUI()
    {
        if (!aberto) return;
        float e = Screen.height / 1080f;
        if (estTitulo == null || Event.current.type == EventType.Layout) Estilos(e);
        float alfa = Mathf.SmoothStep(0f, 1f, t);
        GUI.color = new Color(1f, 1f, 1f, alfa);
        Vector2 mouse = Event.current.mousePosition;

        if (aba != Aba.Pergaminho)
        {
            NomesDosBiomas(e);
            Marcadores(e, mouse);
        }
        PainelEsquerdo(e);
        PainelDireito(e);
        Abas(e);
        Dicas(e);
        GUI.color = Color.white;
    }

    private bool NaTela(Vector3 mundo, out Vector2 tela)
    {
        Vector3 s = camMapa.WorldToScreenPoint(mundo);
        tela = new Vector2(s.x, Screen.height - s.y);
        return s.z > 0f;
    }

    private void NomesDosBiomas(float e)
    {
        if (biomas == null || miniMapa == null) return;
        foreach (ClimaDosBiomas.Bioma b in biomas.biomas)
        {
            if (!miniMapa.Descobertos.Contains(b.nome)) continue;
            Vector2 p;
            if (!NaTela(b.centro + Vector3.up * 40f, out p)) continue;
            Rect r = new Rect(p.x - 220f * e, p.y - 22f * e, 440f * e, 44f * e);
            Sombra(r, b.nome.ToUpper(), estBioma, 2f * e);
        }
    }

    private void Marcadores(float e, Vector2 mouse)
    {
        if (miniMapa == null) return;
        float t0 = Time.unscaledTime;
        foreach (MiniMapa.Marcador m in miniMapa.marcadores)
        {
            if (!miniMapa.Explorado(m.posicao)) continue;
            Vector2 p;
            if (!NaTela(m.posicao + Vector3.up * 12f, out p)) continue;
            float tam = 54f * e;
            Rect rp = new Rect(p.x - tam * 0.5f, p.y - tam, tam, tam);
            bool sobre = rp.Contains(mouse);
            if (sobre) { rp = new Rect(p.x - tam * 0.6f, p.y - tam * 1.2f, tam * 1.2f, tam * 1.2f); }
            if (pino != null) GUI.DrawTexture(rp, pino);
            Texture2D ic = Icone(m.tipo);
            if (ic != null) GUI.DrawTexture(new Rect(rp.center.x - rp.width * 0.27f, rp.y + rp.height * 0.13f, rp.width * 0.54f, rp.width * 0.54f), ic);
            if (sobre || dist < 260f) Etiqueta(new Vector2(p.x, rp.y - 16f * e), m.nome, e);
            if (sobre && Event.current.type == EventType.MouseDown && Event.current.button == 0) { VoarPara(m.posicao, 110f); Event.current.Use(); }
        }
        foreach (Transform a in aliados)
        {
            Vector2 p;
            if (NaTela(a.position + Vector3.up * 3f, out p)) Seta(miniMapa.iconeAliado, a, p, 34f * e);
        }
        if (jogador != null)
        {
            Vector2 p;
            if (NaTela(jogador.position + Vector3.up * 3f, out p))
            {
                float pul = (t0 * 0.9f) % 1f;
                Color antes = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, (1f - pul) * antes.a);
                float rr = Mathf.Lerp(30f, 90f, pul) * e;
                if (pulso != null) GUI.DrawTexture(new Rect(p.x - rr * 0.5f, p.y - rr * 0.5f, rr, rr), pulso);
                GUI.color = antes;
                Seta(miniMapa.iconeJogador, jogador, p, 42f * e);
            }
        }
    }

    private void Seta(Texture2D ic, Transform alvo, Vector2 p, float tam)
    {
        if (ic == null) return;
        Vector2 frente;
        float ang = 0f;
        if (NaTela(alvo.position + Vector3.up * 3f + alvo.forward * 5f, out frente))
        {
            Vector2 d = frente - p;
            ang = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
        }
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, p);
        GUI.DrawTexture(new Rect(p.x - tam * 0.5f, p.y - tam * 0.5f, tam, tam), ic);
        GUI.matrix = m;
    }

    private void Etiqueta(Vector2 base_, string texto, float e)
    {
        Vector2 tam = estRotulo.CalcSize(new GUIContent(texto));
        Rect r = new Rect(base_.x - tam.x * 0.5f - 10f * e, base_.y - tam.y, tam.x + 20f * e, tam.y + 4f * e);
        Painel(r, 0.85f);
        GUI.Label(r, texto, estRotulo);
    }

    private void PainelEsquerdo(float e)
    {
        float w = 320f * e;
        float y0 = 130f * e;
        List<MiniMapa.Marcador> locais = miniMapa != null ? miniMapa.marcadores : new List<MiniMapa.Marcador>();
        int nb = biomas != null ? biomas.biomas.Length : 0;
        float h = (90f + (locais.Count + nb) * 34f + 40f) * e;
        rPainelEsq = new Rect(28f * e, y0, w, h);
        Painel(rPainelEsq, 0.82f);
        float x = rPainelEsq.x + 18f * e, y = y0 + 14f * e;
        GUI.Label(new Rect(x, y, w, 30f * e), "LOCAIS", estTitulo); y += 38f * e;
        foreach (MiniMapa.Marcador m in locais)
        {
            bool conhecido = miniMapa.Explorado(m.posicao);
            Texture2D ic = Icone(m.tipo);
            if (ic != null) { Color a = GUI.color; if (!conhecido) GUI.color = new Color(0.5f, 0.5f, 0.5f, a.a); GUI.DrawTexture(new Rect(x, y + 3f * e, 24f * e, 24f * e), ic); GUI.color = a; }
            Rect rr = new Rect(x + 34f * e, y, w - 60f * e, 30f * e);
            if (GUI.Button(rr, conhecido ? m.nome : "???", estItem) && conhecido) { aba = Aba.Mundo; VoarPara(m.posicao, 110f); }
            y += 34f * e;
        }
        if (biomas != null)
        {
            y += 8f * e;
            GUI.Label(new Rect(x, y, w, 30f * e), "BIOMAS", estTitulo); y += 38f * e;
            foreach (ClimaDosBiomas.Bioma b in biomas.biomas)
            {
                bool conhecido = miniMapa != null && miniMapa.Descobertos.Contains(b.nome);
                if (GUI.Button(new Rect(x + 34f * e, y, w - 60f * e, 30f * e), conhecido ? b.nome : "???", estItem) && conhecido) { aba = Aba.Mundo; VoarPara(b.centro, 420f); }
                y += 34f * e;
            }
        }
    }

    private void PainelDireito(float e)
    {
        float w = 330f * e;
        rPainelDir = new Rect(Screen.width - w - 28f * e, 130f * e, w, 190f * e);
        Painel(rPainelDir, 0.82f);
        float x = rPainelDir.x + 18f * e, y = rPainelDir.y + 14f * e;
        GUI.Label(new Rect(x, y, w, 30f * e), "EXPLORAÇÃO", estTitulo); y += 42f * e;
        int locais = 0, conhecidos = 0;
        if (miniMapa != null) foreach (MiniMapa.Marcador m in miniMapa.marcadores) { locais++; if (miniMapa.Explorado(m.posicao)) conhecidos++; }
        int nb = biomas != null ? biomas.biomas.Length : 0;
        int bd = 0;
        if (biomas != null && miniMapa != null) foreach (ClimaDosBiomas.Bioma b in biomas.biomas) if (miniMapa.Descobertos.Contains(b.nome)) bd++;
        float pct = miniMapa != null ? miniMapa.FracaoExplorada() * 100f : 0f;
        Linha(x, ref y, w, e, "Mapa explorado", Mathf.RoundToInt(pct) + "%");
        Linha(x, ref y, w, e, "Biomas descobertos", bd + " / " + nb);
        Linha(x, ref y, w, e, "Locais descobertos", conhecidos + " / " + locais);
    }

    private void Linha(float x, ref float y, float w, float e, string nome, string valor)
    {
        GUI.Label(new Rect(x, y, w, 30f * e), nome, estItem);
        GUIStyle dir = new GUIStyle(estItem) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(x, y, w - 40f * e, 30f * e), valor, dir);
        y += 36f * e;
    }

    private void Abas(float e)
    {
        float bw = 200f * e, bh = 46f * e, gap = 14f * e;
        float total = bw * 3 + gap * 2;
        float x = (Screen.width - total) * 0.5f, y = 34f * e;
        rAbas = new Rect(x, y, total, bh);
        for (int i = 0; i < 3; i++)
        {
            Rect r = new Rect(x + i * (bw + gap), y, bw, bh);
            bool atual = (int)aba == i;
            Painel(r, atual ? 0.95f : 0.7f, atual);
            Color c = estAba.normal.textColor;
            estAba.normal.textColor = atual ? new Color(1f, 0.85f, 0.5f) : new Color(0.75f, 0.7f, 0.62f);
            if (GUI.Button(r, NOMES_ABAS[i], estAba)) TrocarAba((Aba)i);
            estAba.normal.textColor = c;
        }
        GUI.Label(new Rect(x + total + 16f * e, y + 10f * e, 120f * e, 30f * e), "[" + teclaAba + "]", estPequeno);
    }

    private void Dicas(float e)
    {
        string txt = aba == Aba.Pergaminho
            ? teclaAba + ": trocar aba     " + tecla + " / Esc: fechar"
            : "Roda: zoom     Botão direito ou Q/E: girar     Botão esquerdo ou WASD: mover     " + teclaCentralizar + ": você     " + teclaAba + ": aba     " + tecla + " / Esc: fechar";
        Vector2 tam = estPequeno.CalcSize(new GUIContent(txt));
        Rect r = new Rect((Screen.width - tam.x) * 0.5f - 20f * e, Screen.height - 58f * e, tam.x + 40f * e, 36f * e);
        Painel(r, 0.75f);
        GUI.Label(new Rect(r.x + 20f * e, r.y + 7f * e, tam.x + 10f, tam.y), txt, estPequeno);
    }

    private void VoarPara(Vector3 p, float distancia)
    {
        alvoPivo = p;
        alvoDist = Mathf.Clamp(distancia, distanciaMinima, distanciaMaxima);
    }

    private Texture2D Icone(MiniMapa.Tipo t)
    {
        if (miniMapa == null) return null;
        switch (t)
        {
            case MiniMapa.Tipo.Vila: return miniMapa.iconeVila;
            case MiniMapa.Tipo.Ruinas: return miniMapa.iconeRuinas;
            case MiniMapa.Tipo.Acampamento: return miniMapa.iconeAcampamento;
            default: return miniMapa.iconeCemiterio;
        }
    }

    // painel escuro com borda de bronze
    private static void Painel(Rect r, float opacidade, bool destaque = false)
    {
        Color antes = GUI.color;
        float a = antes.a;
        GUI.color = new Color(0.06f, 0.05f, 0.04f, opacidade * a);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = destaque ? new Color(0.95f, 0.72f, 0.35f, a) : new Color(0.62f, 0.44f, 0.20f, a);
        float b = destaque ? 3f : 2f;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, b), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - b, r.width, b), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, b, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - b, r.y, b, r.height), Texture2D.whiteTexture);
        GUI.color = antes;
    }

    private static void Sombra(Rect r, string texto, GUIStyle st, float d)
    {
        Color c = st.normal.textColor;
        st.normal.textColor = new Color(0f, 0f, 0f, 0.8f * GUI.color.a);
        GUI.Label(new Rect(r.x + d, r.y + d, r.width, r.height), texto, st);
        st.normal.textColor = c;
        GUI.Label(r, texto, st);
    }

    private void OnDisable() { if (aberto) Restaurar(); }
}
