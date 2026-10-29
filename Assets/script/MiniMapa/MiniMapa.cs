using System.Collections.Generic;
using UnityEngine;

// Mini-mapa de explorador: pergaminho desenhado a partir do terreno, que gira
// com a câmera e vai sendo revelado conforme o jogador explora.
//   M      -> abre/fecha o mapa completo
//   + / -  -> aproxima/afasta o mini-mapa
public class MiniMapa : MonoBehaviour
{
    public enum Tipo { Vila, Ruinas, Acampamento, Cemiterio }

    [System.Serializable]
    public class Marcador { public string nome; public Tipo tipo; public Vector3 posicao; }

    [Header("Mapa (o menu Pinglu > 22 preenche)")]
    public Texture2D mapa;
    public Material material;
    public Vector2 origem;          // canto do terreno (x, z)
    public Vector2 tamanho = new Vector2(1000f, 1000f);
    public List<Marcador> marcadores = new List<Marcador>();

    [Header("Imagens")]
    public Texture2D moldura;
    public Texture2D iconeNorte, iconeJogador, iconeAliado, iconeInimigo;
    public Texture2D iconeVila, iconeRuinas, iconeAcampamento, iconeCemiterio;

    [Header("Tela")]
    [Tooltip("Tamanho do mini-mapa numa tela 1080p")]
    public float tamanhoNaTela = 290f;
    [Tooltip("Quantos metros do mundo cabem no mini-mapa")]
    public float metrosVisiveis = 160f;
    public float metrosMinimo = 70f, metrosMaximo = 450f;
    public bool girarComACamera = true;
    public float distanciaParaVerInimigos = 70f;

    [Header("Exploração")]
    public bool usarNevoa = true;
    public float raioRevelado = 50f;

    [Header("Teclas")]
    [Tooltip("Desligado quando existe o Mapa do Mundo (ele usa a tecla M)")]
    public bool abrirComTecla = true;
    public KeyCode teclaMapaCompleto = KeyCode.M;
    public KeyCode teclaAproximar = KeyCode.Equals;
    public KeyCode teclaAfastar = KeyCode.Minus;

    private RenderTexture rtMini, rtGrande;
    private Texture2D nevoa;
    private Color32[] pixelsNevoa;
    private const int RES_NEVOA = 256;
    private bool nevoaMudou;
    private float proximaNevoa, proximaBusca;
    private Transform jogador;
    private readonly List<Transform> aliados = new List<Transform>();
    private readonly List<Transform> inimigos = new List<Transform>();
    private bool mapaAberto;
    private string regiaoAtual = "";
    private readonly HashSet<string> descobertos = new HashSet<string>();
    private ClimaDosBiomas biomas;
    private GUIStyle estiloRegiao, estiloNome, estiloLegenda;

    // usado pelo Mapa do Mundo
    public bool PergaminhoAberto { get { return mapaAberto; } set { mapaAberto = value; } }
    public HashSet<string> Descobertos { get { return descobertos; } }
    public Color32[] PixelsNevoa { get { return pixelsNevoa; } }
    public int ResolucaoNevoa { get { return RES_NEVOA; } }
    public string RegiaoAtual { get { return regiaoAtual; } }
    public float FracaoExplorada()
    {
        if (pixelsNevoa == null) return 0f;
        int n = 0;
        for (int i = 0; i < pixelsNevoa.Length; i++) if (pixelsNevoa[i].r > 100) n++;
        return n / (float)pixelsNevoa.Length;
    }

    private void Start()
    {
        rtMini = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        rtGrande = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
        nevoa = new Texture2D(RES_NEVOA, RES_NEVOA, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        pixelsNevoa = new Color32[RES_NEVOA * RES_NEVOA];
        byte inicial = usarNevoa ? (byte)0 : (byte)255;
        for (int i = 0; i < pixelsNevoa.Length; i++) pixelsNevoa[i] = new Color32(inicial, inicial, inicial, 255);
        nevoa.SetPixels32(pixelsNevoa); nevoa.Apply();
        biomas = FindFirstObjectByType<ClimaDosBiomas>();
    }

    private void OnDestroy()
    {
        if (rtMini != null) rtMini.Release();
        if (rtGrande != null) rtGrande.Release();
    }

    // ------------------------------------------------------------------
    private void Update()
    {
        if (Time.time >= proximaBusca) { proximaBusca = Time.time + 0.4f; Procurar(); }
        if (SessaoDeJogo.MenuAberto || jogador == null) return;

        if (abrirComTecla && Input.GetKeyDown(teclaMapaCompleto)) mapaAberto = !mapaAberto;
        if (Input.GetKey(teclaAproximar) || Input.GetKey(KeyCode.KeypadPlus)) metrosVisiveis = Mathf.Max(metrosMinimo, metrosVisiveis - 120f * Time.deltaTime);
        if (Input.GetKey(teclaAfastar) || Input.GetKey(KeyCode.KeypadMinus)) metrosVisiveis = Mathf.Min(metrosMaximo, metrosVisiveis + 120f * Time.deltaTime);

        if (usarNevoa && Time.time >= proximaNevoa) { proximaNevoa = Time.time + 0.25f; Revelar(jogador.position); }
        AtualizarRegiao();
    }

    // o jogador local é o que tem o Player Locomotion LIGADO (no multiplayer, os outros ficam desligados)
    private void Procurar()
    {
        aliados.Clear(); inimigos.Clear();
        Transform local = null;
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            if (p.enabled && local == null) local = p.transform; else aliados.Add(p.transform);
        }
        jogador = local;
        foreach (InimigoEsqueleto e in Resources.FindObjectsOfTypeAll<InimigoEsqueleto>())
        {
            if (e == null || !e.gameObject.scene.IsValid() || !e.gameObject.activeInHierarchy) continue;
            Vida v = e.GetComponent<Vida>();
            if (v == null || !v.Morto) inimigos.Add(e.transform);
        }
    }

    private Vector2 ParaUV(Vector3 p) { return new Vector2((p.x - origem.x) / tamanho.x, (p.z - origem.y) / tamanho.y); }

    private void Revelar(Vector3 p)
    {
        Vector2 uv = ParaUV(p);
        int cx = Mathf.RoundToInt(uv.x * RES_NEVOA), cy = Mathf.RoundToInt(uv.y * RES_NEVOA);
        float r = raioRevelado / tamanho.x * RES_NEVOA;
        int ri = Mathf.CeilToInt(r);
        for (int y = cy - ri; y <= cy + ri; y++)
            for (int x = cx - ri; x <= cx + ri; x++)
            {
                if (x < 0 || y < 0 || x >= RES_NEVOA || y >= RES_NEVOA) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                if (d > 1f) continue;
                byte v = (byte)(255 * Mathf.Clamp01((1f - d) * 2.5f));
                int i = y * RES_NEVOA + x;
                if (v > pixelsNevoa[i].r) { pixelsNevoa[i] = new Color32(v, v, v, 255); nevoaMudou = true; }
            }
        if (nevoaMudou) { nevoa.SetPixels32(pixelsNevoa); nevoa.Apply(false); nevoaMudou = false; }
    }

    public bool Explorado(Vector3 p)
    {
        if (!usarNevoa) return true;
        Vector2 uv = ParaUV(p);
        int x = Mathf.Clamp(Mathf.RoundToInt(uv.x * RES_NEVOA), 0, RES_NEVOA - 1), y = Mathf.Clamp(Mathf.RoundToInt(uv.y * RES_NEVOA), 0, RES_NEVOA - 1);
        return pixelsNevoa[y * RES_NEVOA + x].r > 100;
    }

    // nome do lugar onde o jogador está + aviso ao descobrir um bioma
    private void AtualizarRegiao()
    {
        Vector3 p = jogador.position;
        string nome = "Campos";
        foreach (Marcador m in marcadores)
            if (PlanoDist(p, m.posicao) < (m.tipo == Tipo.Vila ? 55f : 30f)) { nome = m.nome; break; }
        if (nome == "Campos" && biomas != null)
        {
            float melhor = 0.5f;
            foreach (ClimaDosBiomas.Bioma b in biomas.biomas)
            {
                float w = 1f - Mathf.InverseLerp(b.raio * 0.8f, b.raio + 20f, PlanoDist(p, b.centro));
                if (w > melhor) { melhor = w; nome = b.nome; }
            }
            if (nome != "Campos" && descobertos.Add(nome)) HUDJogador.Mostrar("Você descobriu: " + nome, 4f);
        }
        regiaoAtual = nome;
    }

    private static float PlanoDist(Vector3 a, Vector3 b) { return new Vector2(a.x - b.x, a.z - b.z).magnitude; }

    // ------------------------------------------------------------------
    //  Desenho
    // ------------------------------------------------------------------
    private void Estilos(float e)
    {
        estiloRegiao = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(21 * e), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        estiloRegiao.normal.textColor = new Color(0.93f, 0.80f, 0.55f);
        estiloNome = new GUIStyle(estiloRegiao) { fontSize = Mathf.RoundToInt(17 * e) };
        estiloNome.normal.textColor = new Color(0.20f, 0.14f, 0.08f);
        estiloLegenda = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * e) };
        estiloLegenda.normal.textColor = new Color(0.93f, 0.85f, 0.7f);
    }

    private void OnGUI()
    {
        if ((SessaoDeJogo.MenuAberto && !mapaAberto) || jogador == null || mapa == null || material == null) return;
        float e = Screen.height / 1080f;
        if (estiloRegiao == null || Event.current.type == EventType.Layout) Estilos(e);
        Camera cam = Camera.main;
        float yaw = girarComACamera && cam != null ? cam.transform.eulerAngles.y : 0f;


        if (SessaoDeJogo.MenuAberto) { if (mapaAberto) MapaCompleto(e); return; } // aberto como aba do Mapa do Mundo

        // ---- mini-mapa (canto inferior esquerdo)
        float lado = tamanhoNaTela * e;
        Rect r = new Rect(24f * e, Screen.height - lado - 60f * e, lado, lado);
        GUI.DrawTexture(r, rtMini);
        Vector2 centro = r.center;
        float raio = lado * 0.47f;
        float rad = yaw * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)), frente = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        System.Func<Vector3, Vector2> NaTela = w =>
        {
            Vector2 d = new Vector2(w.x - jogador.position.x, w.z - jogador.position.z);
            return new Vector2(Vector2.Dot(d, dir), -Vector2.Dot(d, frente)) / metrosVisiveis * lado;
        };

        foreach (Marcador m in marcadores)
        {
            if (!Explorado(m.posicao)) continue;
            Vector2 p = NaTela(m.posicao);
            bool longe = p.magnitude > raio * 0.88f;
            if (longe) p = p.normalized * raio * 0.88f; // preso na borda, apontando a direção
            Icone(IconeDo(m.tipo), centro + p, (longe ? 20f : 28f) * e, 0f, longe ? 0.7f : 1f);
        }
        foreach (Transform t in inimigos)
        {
            if (PlanoDist(t.position, jogador.position) > distanciaParaVerInimigos) continue;
            Vector2 p = NaTela(t.position);
            if (p.magnitude < raio * 0.95f) Icone(iconeInimigo, centro + p, 18f * e, 0f, 1f);
        }
        foreach (Transform t in aliados)
        {
            Vector2 p = NaTela(t.position);
            if (p.magnitude > raio * 0.9f) p = p.normalized * raio * 0.9f;
            Icone(iconeAliado, centro + p, 22f * e, t.eulerAngles.y - yaw, 1f);
        }
        Icone(iconeJogador, centro, 30f * e, jogador.eulerAngles.y - yaw, 1f);

        if (moldura != null) GUI.DrawTexture(new Rect(r.x - lado * 0.06f, r.y - lado * 0.06f, lado * 1.12f, lado * 1.12f), moldura);
        Vector2 norte = NaTela(jogador.position + Vector3.forward * 1000f).normalized * lado * 0.53f;
        Icone(iconeNorte, centro + norte, 30f * e, 0f, 1f);

        GUI.Label(new Rect(r.x - 20f * e, r.yMax + 12f * e, lado + 40f * e, 30f * e), regiaoAtual, estiloRegiao);

        if (mapaAberto) MapaCompleto(e);
    }

    private void LateUpdate()
    {
        if ((SessaoDeJogo.MenuAberto && !mapaAberto) || jogador == null || mapa == null || material == null) return;
        Camera cam = Camera.main;
        float yaw = girarComACamera && cam != null ? cam.transform.eulerAngles.y : 0f;
        Desenhar(rtMini, ParaUV(jogador.position), metrosVisiveis / tamanho.x, yaw * Mathf.Deg2Rad, true);
        if (mapaAberto) Desenhar(rtGrande, new Vector2(0.5f, 0.5f), 1f, 0f, false);
    }

    private void Desenhar(RenderTexture alvo, Vector2 centroUV, float zoom, float rot, bool circulo)
    {
        material.SetTexture("_Nevoa", nevoa);
        material.SetVector("_Centro", new Vector4(centroUV.x, centroUV.y, 0f, 0f));
        material.SetFloat("_Zoom", zoom);
        material.SetFloat("_Rot", rot);
        material.SetFloat("_Circulo", circulo ? 1f : 0f);
        RenderTexture antes = RenderTexture.active;
        Graphics.Blit(mapa, alvo, material);
        RenderTexture.active = antes;
    }

    private void MapaCompleto(float e)
    {
        float lado = Screen.height * 0.82f;
        Rect r = new Rect((Screen.width - lado) * 0.5f, (Screen.height - lado) * 0.5f, lado, lado);
        Retangulo(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
        Retangulo(new Rect(r.x - 10f * e, r.y - 10f * e, r.width + 20f * e, r.height + 20f * e), new Color(0.45f, 0.30f, 0.12f));
        GUI.DrawTexture(r, rtGrande);
        System.Func<Vector3, Vector2> NoMapa = w => { Vector2 uv = ParaUV(w); return new Vector2(r.x + uv.x * r.width, r.yMax - uv.y * r.height); };

        if (biomas != null)
            foreach (ClimaDosBiomas.Bioma b in biomas.biomas)
                GUI.Label(new Rect(NoMapa(b.centro).x - 120f * e, NoMapa(b.centro).y - 15f * e, 240f * e, 30f * e), descobertos.Contains(b.nome) ? b.nome : "???", estiloNome);
        foreach (Marcador m in marcadores)
            if (Explorado(m.posicao)) Icone(IconeDo(m.tipo), NoMapa(m.posicao), 30f * e, 0f, 1f);
        foreach (Transform t in aliados) Icone(iconeAliado, NoMapa(t.position), 24f * e, t.eulerAngles.y, 1f);
        Icone(iconeJogador, NoMapa(jogador.position), 32f * e, jogador.eulerAngles.y, 1f);

        // legenda
        float x = r.xMax + 24f * e, y = r.y + 10f * e;
        if (x + 220f * e > Screen.width) return;
        GUI.Label(new Rect(x, y, 260f * e, 30f * e), "LEGENDA", estiloLegenda); y += 40f * e;
        Texture2D[] ic = { iconeJogador, iconeAliado, iconeInimigo, iconeVila, iconeRuinas, iconeAcampamento, iconeCemiterio };
        string[] nomes = { "Você", "Outro jogador", "Inimigo", "Vila", "Ruínas", "Acampamento", "Cemitério" };
        for (int i = 0; i < ic.Length; i++)
        {
            Icone(ic[i], new Vector2(x + 14f * e, y + 14f * e), 26f * e, 0f, 1f);
            GUI.Label(new Rect(x + 36f * e, y, 220f * e, 30f * e), nomes[i], estiloLegenda);
            y += 34f * e;
        }
    }

    private Texture2D IconeDo(Tipo t)
    {
        switch (t)
        {
            case Tipo.Vila: return iconeVila;
            case Tipo.Ruinas: return iconeRuinas;
            case Tipo.Acampamento: return iconeAcampamento;
            default: return iconeCemiterio;
        }
    }

    private static void Icone(Texture2D t, Vector2 centro, float tam, float angulo, float alfa)
    {
        if (t == null) return;
        Matrix4x4 antes = GUI.matrix;
        Color corAntes = GUI.color;
        if (angulo != 0f) GUIUtility.RotateAroundPivot(angulo, centro);
        GUI.color = new Color(1f, 1f, 1f, alfa);
        GUI.DrawTexture(new Rect(centro.x - tam * 0.5f, centro.y - tam * 0.5f, tam, tam), t);
        GUI.color = corAntes;
        GUI.matrix = antes;
    }

    private static void Retangulo(Rect r, Color c)
    {
        Color a = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = a;
    }
}
