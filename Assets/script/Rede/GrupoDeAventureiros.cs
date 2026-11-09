using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// O grupo de 4 aventureiros. No modo solo, o jogador escolhe 1 e os outros 3 nascem como companheiros CPU
// (cada um com um CerebroCompanheiro). Esta classe também: vigia os inimigos, entende os comandos do jogador
// (G reunir, Y ajudar, T atacar o alvo da mira), revive todo mundo quando um chefe cai e desenha o painel do grupo.
public class GrupoDeAventureiros : MonoBehaviour
{
    public static bool Ativo = true;                       // desligue para jogar sem companheiros
    public static readonly List<PlayerLocomotion> Companheiros = new List<PlayerLocomotion>();
    public static readonly List<PlayerLocomotion> Membros = new List<PlayerLocomotion>();        // jogador + companheiros
    public static readonly List<InimigoVisto> Inimigos = new List<InimigoVisto>();
    public static Transform Humano { get; private set; }
    static readonly List<string> nomes = new List<string>();
    static readonly List<CerebroCompanheiro> cerebros = new List<CerebroCompanheiro>();
    static GrupoDeAventureiros instancia;

    // comandos
    static float reunirAte, ajudarAte, alvoForcadoAte;
    static InimigoVisto alvoForcado;
    static CerebroCompanheiro ajudanteLuta, ajudanteCura;
    public static bool Reunindo { get { return Time.time < reunirAte; } }
    public static InimigoVisto AlvoForcado { get { return Time.time < alvoForcadoAte && alvoForcado != null && alvoForcado.Vivo ? alvoForcado : null; } }
    public static bool AjudandoNaLuta(CerebroCompanheiro c) { return Time.time < ajudarAte && ajudanteLuta == c; }
    public static bool AjudandoNaCura(CerebroCompanheiro c) { return Time.time < ajudarAte && ajudanteCura == c; }

    // um inimigo que os companheiros enxergam
    public class InimigoVisto
    {
        public Transform t; public Vida vida; public Collider col; public string nome;
        public bool Vivo { get { return t != null && vida != null && !vida.Morto && t.gameObject.activeInHierarchy; } }
        public float Raio { get { if (col == null) return 0.5f; Bounds b = col.bounds; return Mathf.Max(0.3f, Mathf.Min(b.extents.x, b.extents.z)); } }
    }
    static readonly Dictionary<Vida, InimigoVisto> cacheInimigos = new Dictionary<Vida, InimigoVisto>();
    static readonly HashSet<Vida> chefesOuvidos = new HashSet<Vida>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Companheiros.Clear(); Membros.Clear(); Inimigos.Clear(); nomes.Clear(); cerebros.Clear(); cacheInimigos.Clear(); chefesOuvidos.Clear();
        instancia = null; Ativo = true; Humano = null; reunirAte = ajudarAte = alvoForcadoAte = 0f; alvoForcado = null; ajudanteLuta = ajudanteCura = null;
    }

    // ------------------------------------------------------------------
    //  formar o grupo (modo solo)
    // ------------------------------------------------------------------
    public static void Formar(IList<FichaPersonagem> fichas, int escolhido, Vector3 pos, Quaternion rot)
    {
        Desfazer();
        if (!Ativo || SessaoDeJogo.Online || fichas == null) return;
        Vector3 lado = rot * Vector3.right;
        float[] fila = { -2.2f, 2.2f, -4.4f };
        int n = 0;
        for (int i = 0; i < fichas.Count && n < fila.Length; i++)
        {
            FichaPersonagem f = fichas[i];
            if (i == escolhido || f == null || !f.disponivel || f.prefabJogador == null) continue;
            PlayerLocomotion c = Criar(f, pos + lado * fila[n], rot);
            if (c != null) { Companheiros.Add(c); nomes.Add(f.nome); cerebros.Add(c.GetComponent<CerebroCompanheiro>()); n++; }
        }
        if (instancia == null) { GameObject g = new GameObject("Grupo de Aventureiros"); instancia = g.AddComponent<GrupoDeAventureiros>(); }
    }

    public static void Desfazer()
    {
        foreach (PlayerLocomotion c in Companheiros) if (c != null) Destroy(c.gameObject);
        Companheiros.Clear(); nomes.Clear(); cerebros.Clear(); Membros.Clear();
    }

    static PapelCPU PapelDe(string nome)
    {
        string n = nome.ToLowerInvariant();
        if (n.Contains("tanque")) return PapelCPU.Tanque;
        if (n.Contains("morgana") || n.Contains("maga")) return PapelCPU.Maga;
        if (n.Contains("curandeiro")) return PapelCPU.Curandeiro;
        return PapelCPU.Corpo;
    }

    static PlayerLocomotion Criar(FichaPersonagem f, Vector3 pos, Quaternion rot)
    {
        Terrain t = Terrain.activeTerrain;
        if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y + 0.1f;
        // nasce dentro de um objeto desligado: os scripts só começam depois de tirarmos a parte de rede
        GameObject espera = new GameObject("espera");
        espera.SetActive(false);
        GameObject j = Instantiate(f.prefabJogador, pos, rot, espera.transform);
        foreach (Unity.Netcode.NetworkBehaviour nb in j.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true)) DestroyImmediate(nb);
        foreach (Unity.Netcode.NetworkObject no in j.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true)) DestroyImmediate(no);
        HUDJogador hud = j.GetComponent<HUDJogador>(); if (hud != null) DestroyImmediate(hud);   // o painel pessoal é só do jogador
        PlayerLocomotion loco = j.GetComponent<PlayerLocomotion>();
        if (loco == null) { Destroy(espera); Destroy(j); return null; }
        loco.Companheiro = true;
        EntradaCPU e = j.GetComponent<EntradaCPU>(); if (e == null) e = j.AddComponent<EntradaCPU>();
        EntradaHumana h = j.GetComponent<EntradaHumana>(); if (h != null) DestroyImmediate(h);
        loco.TrocarEntrada(e);
        CerebroCompanheiro cb = j.AddComponent<CerebroCompanheiro>(); cb.papel = PapelDe(f.nome);
        j.transform.SetParent(null, true);
        Physics.SyncTransforms();
        j.name = "Companheiro - " + f.nome;
        Destroy(espera);                       // ao destruir o pai desligado, o personagem (já solto) acorda
        return loco;
    }

    // ------------------------------------------------------------------
    //  consultas usadas pelos cérebros
    // ------------------------------------------------------------------
    public static int QuantosAtacam(InimigoVisto e, CerebroCompanheiro eu)
    {
        int n = 0;
        foreach (CerebroCompanheiro c in cerebros) if (c != null && c != eu && c.Vivo && c.AlvoAtual == e) n++;
        return n;
    }

    public static bool PosDoCurandeiro(out Vector3 pos)
    {
        foreach (CerebroCompanheiro c in cerebros) if (c != null && c.Vivo && c.papel == PapelCPU.Curandeiro) { pos = c.transform.position; return true; }
        pos = Vector3.zero; return false;
    }

    // ------------------------------------------------------------------
    //  rotina do grupo
    // ------------------------------------------------------------------
    float proximaBuscaHumano, proximaBuscaInimigos;

    void Update()
    {
        if (Time.time >= proximaBuscaHumano) { proximaBuscaHumano = Time.time + 0.5f; AcharHumano(); }
        if (Time.time >= proximaBuscaInimigos) { proximaBuscaInimigos = Time.time + 0.35f; VigiarInimigos(); }
        if (SessaoDeJogo.MenuAberto || DialogoUI.Aberto || Humano == null) return;
        if (Input.GetKeyDown(KeyCode.G)) Reunir();
        if (Input.GetKeyDown(KeyCode.Y)) Ajudar();
        if (Input.GetKeyDown(KeyCode.T)) AtacarAlvoDaMira();
    }

    void AcharHumano()
    {
        Humano = null; Membros.Clear();
        PlayerLocomotion humano = null;
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) { humano = p; break; }
        if (humano != null) { Humano = humano.transform; Membros.Add(humano); }
        foreach (PlayerLocomotion c in Companheiros) if (c != null) Membros.Add(c);
    }

    void VigiarInimigos()
    {
        Inimigos.Clear();
        foreach (InimigoEsqueleto e in FindObjectsByType<InimigoEsqueleto>(FindObjectsSortMode.None)) Registrar(e.Vida, e.Lutando, "Esqueleto", false);
        foreach (ChefeGuardiao e in FindObjectsByType<ChefeGuardiao>(FindObjectsSortMode.None)) Registrar(e.Vida, e.Lutando, e.nome, true);
        foreach (InimigoCavaleiro e in FindObjectsByType<InimigoCavaleiro>(FindObjectsSortMode.None)) Registrar(e.Vida, e.Lutando, "Cavaleiro", true);
    }

    void Registrar(Vida v, bool lutando, string nome, bool chefe)
    {
        if (v == null) return;
        if (chefe && chefesOuvidos.Add(v)) v.AoMorrer += AoChefeCair;       // chefe derrotado = grupo recupera tudo
        if (!lutando || v.Morto) return;
        InimigoVisto iv;
        if (!cacheInimigos.TryGetValue(v, out iv) || iv.t == null)
        {
            iv = new InimigoVisto { t = v.transform, vida = v, nome = nome };
            Collider c = v.GetComponent<Collider>(); if (c == null) c = v.GetComponentInChildren<Collider>();
            iv.col = c; cacheInimigos[v] = iv;
        }
        Inimigos.Add(iv);
    }

    // ------------------------------------------------------------------
    //  chefe derrotado: todos recuperam a vida, quem caiu levanta
    // ------------------------------------------------------------------
    void AoChefeCair() { StartCoroutine(RevivarDepois()); }

    IEnumerator RevivarDepois()
    {
        yield return new WaitForSeconds(2f);
        bool alguem = false;
        foreach (PlayerLocomotion c in Companheiros)
        {
            if (c == null) continue;
            Vida v = c.GetComponent<Vida>(); if (v == null) continue;
            if (v.Morto)
            {
                v.Reviver(); c.Reviver(); alguem = true;
                CerebroCompanheiro cb = c.GetComponent<CerebroCompanheiro>();
                if (cb != null && Humano != null) cb.Teleportar(Humano.position + Humano.right * Random.Range(-2.5f, 2.5f) - Humano.forward * 2.5f);
            }
            else v.DefinirAtual(v.Maxima);
        }
        if (Humano != null) { Vida hv = Humano.GetComponent<Vida>(); if (hv != null && !hv.Morto) hv.DefinirAtual(hv.Maxima); }
        if (Companheiros.Count > 0) HUDJogador.Mostrar(alguem ? "O grupo se recuperou e os caídos se levantaram!" : "O grupo recuperou as forças!", 4f);
    }

    // ------------------------------------------------------------------
    //  comandos
    // ------------------------------------------------------------------
    static void Reunir()
    {
        if (Companheiros.Count == 0) return;
        reunirAte = Time.time + 5f; alvoForcado = null; ajudarAte = 0f;
    }

    static void Ajudar()
    {
        if (Companheiros.Count == 0 || Humano == null) return;
        ajudanteLuta = ajudanteCura = null; ajudarAte = Time.time + 8f; reunirAte = 0f;
        Vida hv = Humano.GetComponent<Vida>();
        CerebroCompanheiro curandeiro = null, melhor = null; float menor = 1e9f; int perto = 0;
        foreach (InimigoVisto e in Inimigos) if (e.Vivo && Vector3.Distance(e.t.position, Humano.position) < 12f) perto++;
        foreach (CerebroCompanheiro c in cerebros)
        {
            if (c == null || !c.Vivo) continue;
            if (c.papel == PapelCPU.Curandeiro) { curandeiro = c; continue; }
            float d = Vector3.Distance(c.transform.position, Humano.position);
            if (c.papel == PapelCPU.Tanque && perto >= 2) d *= 0.4f;            // vários inimigos: o Tanque é o mais útil
            if (d < menor) { menor = d; melhor = c; }
        }
        if (hv != null && hv.Fracao < 0.7f && curandeiro != null) { ajudanteCura = curandeiro; if (perto > 0 && melhor != null) ajudanteLuta = melhor; }
        else if (melhor != null) ajudanteLuta = melhor;
        else if (curandeiro != null) ajudanteCura = curandeiro;
    }

    static void AtacarAlvoDaMira()
    {
        Camera cam = Camera.main; if (cam == null) return;
        InimigoVisto melhor = null; float menorAngulo = 25f;
        foreach (InimigoVisto e in Inimigos)
        {
            if (!e.Vivo) continue;
            Vector3 para = e.t.position + Vector3.up - cam.transform.position;
            if (para.magnitude > 45f) continue;
            float a = Vector3.Angle(cam.transform.forward, para);
            if (a < menorAngulo) { menorAngulo = a; melhor = e; }
        }
        if (melhor == null) return;
        alvoForcado = melhor; alvoForcadoAte = Time.time + 12f; reunirAte = 0f;
    }

    // ------------------------------------------------------------------
    //  interface: nome e vida sobre a cabeça + painel do grupo no canto
    // ------------------------------------------------------------------
    GUIStyle estNome, estPapel, estTecla, estNumero; Texture2D pixel;

    void Retangulo(Rect r, Color c, float canto)
    {
        canto = Mathf.Min(canto, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, pixel, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(canto, canto, canto, canto));
    }
    void Contorno(Rect r, Color c, float esp, float canto)
    {
        canto = Mathf.Min(canto, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, pixel, ScaleMode.StretchToFill, true, 0f, c, new Vector4(esp, esp, esp, esp), new Vector4(canto, canto, canto, canto));
    }

    static string PapelTexto(string nome)
    {
        switch (PapelDe(nome))
        {
            case PapelCPU.Tanque: return "LINHA DE FRENTE";
            case PapelCPU.Maga: return "MAGIA À DISTÂNCIA";
            case PapelCPU.Curandeiro: return "CURA";
            default: return "CORPO A CORPO";
        }
    }

    // escreve o texto diminuindo a letra até caber na largura
    void TextoAjustado(Rect r, string texto, GUIStyle st, int tamMax, int tamMin)
    {
        int t = tamMax; st.fontSize = t;
        while (t > tamMin && st.CalcSize(new GUIContent(texto)).x > r.width) { t -= 2; st.fontSize = t; }
        GUI.Label(r, texto, st);
    }

    void OnGUI()
    {
        if (Companheiros.Count == 0 || SessaoDeJogo.MenuAberto) return;
        if (pixel == null) { pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply(); }
        Font fonte = DialogoUI.Instancia != null ? DialogoUI.Instancia.fonteTexto : null;
        Font fonteNome = DialogoUI.Instancia != null && DialogoUI.Instancia.fonteNome != null ? DialogoUI.Instancia.fonteNome : fonte;
        if (estNome == null)
        {
            estNome = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleLeft, font = fonteNome, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            estPapel = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, font = fonte, wordWrap = false, clipping = TextClipping.Overflow };
            estTecla = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, font = fonteNome, fontStyle = FontStyle.Bold, wordWrap = false };
            estNumero = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleRight, font = fonte, wordWrap = false };
        }
        float e = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(e, e, 1f));
        float W = Screen.width / e;
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);

        // nome e barra de vida sobre a cabeça
        Camera cam = Camera.main;
        if (cam != null && !CameraDeCena.Ativa)
        {
            for (int i = 0; i < Companheiros.Count; i++)
            {
                PlayerLocomotion c = Companheiros[i]; if (c == null || !c.gameObject.activeInHierarchy) continue;
                Vector3 sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * 2.35f);
                if (sp.z < 0.5f || sp.z > 45f) continue;
                float x = sp.x / e, y = (Screen.height - sp.y) / e;
                Vida v = c.GetComponent<Vida>();
                EstiloDeFala est = EstiloDeFala.Para(nomes[i]);
                estNome.alignment = TextAnchor.MiddleCenter; estNome.normal.textColor = est.nome;
                TextoAjustado(new Rect(x - 110f, y - 30f, 220f, 28f), nomes[i], estNome, 22, 16);
                estNome.alignment = TextAnchor.MiddleLeft;
                float fr = v != null ? v.Fracao : 1f;
                Retangulo(new Rect(x - 42f, y, 84f, 9f), new Color(0, 0, 0, 0.7f), 4f);
                if (fr > 0.01f) Retangulo(new Rect(x - 41f, y + 1f, 82f * fr, 7f), Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.85f, 0.35f), fr), 3f);
            }
        }

        // painel do grupo, no canto de cima à direita
        float cw = 316f, ch = 92f, gap = 10f, px = W - cw - 30f, py = 36f;
        for (int i = 0; i < Companheiros.Count; i++)
        {
            PlayerLocomotion c = Companheiros[i]; if (c == null) continue;
            Vida v = c.GetComponent<Vida>();
            EstiloDeFala est = EstiloDeFala.Para(nomes[i]);
            bool morto = v != null && v.Morto;
            bool curando = !morto && cerebros[i] != null && cerebros[i].Estado == "Curando";
            Rect card = new Rect(px, py + i * (ch + gap), cw, ch);
            // sombra, fundo, faixa de luz e contorno na cor do personagem
            Retangulo(new Rect(card.x + 3f, card.y + 5f, cw, ch), new Color(0, 0, 0, 0.35f), 14f);
            Retangulo(card, new Color(est.fundo.r, est.fundo.g, est.fundo.b, 0.93f), 14f);
            Retangulo(new Rect(card.x + 4f, card.y + 4f, cw - 8f, ch * 0.4f), new Color(1, 1, 1, 0.04f), 10f);
            Color borda = est.borda; borda.a = morto ? 0.3f : 0.9f;
            Contorno(card, borda, 2.5f, 14f);
            if (curando) { Color g = est.brilho; g.a = 0.12f + 0.18f * pulso; Contorno(new Rect(card.x - 3f, card.y - 3f, cw + 6f, ch + 6f), g, 4f, 17f); }

            // retrato grande, que sai um pouco da moldura
            Rect rr = new Rect(card.x + 10f, card.y + 8f, 76f, 76f);
            Retangulo(new Rect(rr.x - 3f, rr.y - 3f, 82f, 82f), borda, 12f);
            GUI.color = morto ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
            GUI.DrawTexture(rr, RetratosDeDialogo.Obter(nomes[i]), ScaleMode.ScaleAndCrop, true, 0f, GUI.color, Vector4.zero, new Vector4(10f, 10f, 10f, 10f));
            GUI.color = Color.white;
            if (morto) { estNome.alignment = TextAnchor.MiddleCenter; estNome.normal.textColor = new Color(1f, 0.85f, 0.85f); estNome.fontSize = 18; GUI.Label(rr, "CAÍDO", estNome); estNome.alignment = TextAnchor.MiddleLeft; }

            float tx = card.x + 100f, tw = cw - 100f - 14f;
            estNome.normal.textColor = morto ? new Color(0.7f, 0.7f, 0.7f) : est.nome;
            TextoAjustado(new Rect(tx, card.y + 6f, tw, 32f), nomes[i], estNome, 26, 16);
            Color pc = est.borda; pc.a = 0.85f; estPapel.normal.textColor = curando ? new Color(0.55f, 1f, 0.65f) : pc;
            GUI.Label(new Rect(tx, card.y + 36f, tw, 18f), curando ? "CURANDO..." : PapelTexto(nomes[i]), estPapel);

            float fr = v != null ? v.Fracao : 1f;
            Rect barra = new Rect(tx, card.y + 60f, tw, 16f);
            Retangulo(barra, new Color(0, 0, 0, 0.75f), 8f);
            if (fr > 0.01f) Retangulo(new Rect(barra.x + 2f, barra.y + 2f, (barra.width - 4f) * fr, barra.height - 4f), Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.85f, 0.35f), fr), 6f);
        }

        // comandos, com teclas desenhadas
        string[] teclas = { "G", "Y", "T" }, rotulos = { "Reunir", "Ajudar", "Atacar alvo" };
        float larguraTotal = 0f; float[] lw = new float[3];
        estPapel.fontSize = 17; estPapel.normal.textColor = new Color(0.92f, 0.88f, 0.78f);
        for (int i = 0; i < 3; i++) { lw[i] = estPapel.CalcSize(new GUIContent(rotulos[i])).x; larguraTotal += 28f + 6f + lw[i] + 16f; }
        float hy = py + Companheiros.Count * (ch + gap) + 2f;
        Retangulo(new Rect(px + cw - larguraTotal - 8f, hy, larguraTotal + 8f, 34f), new Color(0.05f, 0.04f, 0.03f, 0.8f), 10f);
        float hx = px + cw - larguraTotal;
        for (int i = 0; i < 3; i++)
        {
            Rect k = new Rect(hx, hy + 5f, 26f, 24f);
            Retangulo(k, new Color(0.85f, 0.72f, 0.4f), 6f);
            estTecla.normal.textColor = new Color(0.12f, 0.08f, 0.04f); GUI.Label(k, teclas[i], estTecla);
            GUI.Label(new Rect(hx + 32f, hy + 5f, lw[i] + 6f, 24f), rotulos[i], estPapel);
            hx += 28f + 6f + lw[i] + 16f;
        }
    }
}
