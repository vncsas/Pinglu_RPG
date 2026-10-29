#if PINGLU_REDE
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu inicial: escolher personagem, jogar sozinho, criar partida (host)
// ou entrar na partida de outro PC digitando o IP dele (rede local).
public class MenuRede : MonoBehaviour
{
    [Header("Personagens (até 4)")]
    [SerializeField] private List<FichaPersonagem> personagens = new List<FichaPersonagem>();

    [Header("Cena")]
    [Tooltip("O personagem que já estava na cena; usado no modo sozinho e como ponto de nascimento")]
    [SerializeField] private GameObject jogadorDaCena;

    [Header("Palco da seleção (o menu Pinglu > 23 preenche)")]
    [SerializeField] private Material materialFundo;
    [SerializeField] private Material materialChao;
    [SerializeField] private Material materialParticula;

    [Header("Rede")]
    [SerializeField] private ushort porta = 7777;
    [SerializeField] private int maximoDeJogadores = 4;
    [SerializeField] private KeyCode teclaSair = KeyCode.F10;

    private enum Tela { Principal, Conectando, Sala, Jogando }
    private Tela tela = Tela.Principal;
    private int escolhido;
    private string ipDigitado = "127.0.0.1";
    private string erro;
    private Vector3 nascimento;
    private Quaternion rotacaoNascimento = Quaternion.identity;
    private int proximoLugar;
    private string meusIps = "";
    private GUIStyle titulo, texto, textoPequeno, botao, cartao;
    private GUIStyle estNome, estTituloPers, estSecao, estBotao, estCarta, estP1, estInterrogacao, estCampo;
    private PalcoSelecao palco;
    private string assinaturaSala = "";

    private void Awake()
    {
        Application.runInBackground = true; // para testar dois jogos abertos no mesmo PC
    }

    private void Start()
    {
        if (jogadorDaCena != null)
        {
            nascimento = jogadorDaCena.transform.position;
            rotacaoNascimento = jogadorDaCena.transform.rotation;
            jogadorDaCena.SetActive(false);
        }
        // esconde qualquer outro personagem que tenha ficado na cena (ex.: o X Bot antigo)
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
            if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.GetComponent<NetworkObject>() == null)
                p.gameObject.SetActive(false);
        for (int i = 0; i < personagens.Count; i++)
            if (personagens[i] != null && personagens[i].disponivel) { escolhido = i; break; }
        meusIps = IpsDesteComputador();
        palco = gameObject.AddComponent<PalcoSelecao>();
        palco.materialFundo = materialFundo; palco.materialChao = materialChao; palco.materialParticula = materialParticula;
        palco.Montar(personagens);
        palco.Mostrar(Ficha(escolhido));
        SessaoDeJogo.MenuAberto = true;
        SessaoDeJogo.Online = false;
    }

    private void Escolher(int i)
    {
        if (Ficha(i) == null) return;
        escolhido = i;
        if (tela == Tela.Sala)
        {
            if (SalaDeEspera.Atual != null) SalaDeEspera.Atual.Escolher(i);
        }
        else if (palco != null) palco.Mostrar(Ficha(i));
    }

    private void EscolherAleatorio()
    {
        List<int> ok = new List<int>();
        for (int i = 0; i < personagens.Count; i++) if (Ficha(i) != null && Ficha(i).prefabJogador != null) ok.Add(i);
        if (ok.Count > 0) Escolher(ok[Random.Range(0, ok.Count)]);
    }

    // cria o personagem escolhido para jogar sozinho, sem as partes de rede
    private void NascerSozinho(FichaPersonagem f)
    {
        // a posição vem ANTES de o personagem começar a funcionar:
        // o CharacterController guarda onde nasceu e não acompanha uma troca depois
        Vector3 pos = nascimento;
        Terrain t = Terrain.activeTerrain;
        if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y + 0.1f;
        GameObject espera = new GameObject("espera");
        espera.SetActive(false); // assim os scripts só começam depois de tirarmos a parte de rede
        GameObject j = Instantiate(f.prefabJogador, pos, rotacaoNascimento, espera.transform);
        foreach (NetworkBehaviour nb in j.GetComponentsInChildren<NetworkBehaviour>(true)) DestroyImmediate(nb);
        foreach (NetworkObject no in j.GetComponentsInChildren<NetworkObject>(true)) DestroyImmediate(no);
        j.transform.SetParent(null, true);
        Physics.SyncTransforms();
        j.name = f.nome;
        Destroy(espera);
        ThirdPersonCameraFollow cam = FindFirstObjectByType<ThirdPersonCameraFollow>();
        if (cam != null) cam.DefinirAlvo(j.transform);
    }

    private void SairDoMenu()
    {
        if (palco != null) { palco.Desmontar(); Destroy(palco); palco = null; }
    }

    private void Update()
    {
        if (tela == Tela.Sala) AtualizarSala();
        if (tela == Tela.Principal || tela == Tela.Sala)
        {
            int dir = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : (Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 0);
            for (int passo = 1; dir != 0 && passo <= personagens.Count; passo++)
            {
                int i = ((escolhido + dir * passo) % personagens.Count + personagens.Count) % personagens.Count;
                if (Ficha(i) != null) { Escolher(i); break; }
            }
        }
        if (tela == Tela.Jogando && SessaoDeJogo.Online && Input.GetKeyDown(teclaSair)) SairDaPartida();
        if (SessaoDeJogo.MenuAberto) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    // ------------------------------------------------------------------
    //  Ações
    // ------------------------------------------------------------------
    private void JogarSozinho()
    {
        SessaoDeJogo.MensagemAoVoltar = null;
        SessaoDeJogo.Online = false;
        SessaoDeJogo.MenuAberto = false;
        SessaoDeJogo.PersonagemEscolhido = escolhido;
        FichaPersonagem f = Ficha(escolhido);
        // o 1º personagem é o que já está na cena; os outros nascem do prefab dele
        if (escolhido == 0 || f == null || f.prefabJogador == null) { if (jogadorDaCena != null) jogadorDaCena.SetActive(true); }
        else NascerSozinho(f);
        tela = Tela.Jogando;
        SairDoMenu();
    }

    private bool Preparar()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) { erro = "Não achei o NetworkManager na cena. Rode o menu Pinglu > 15."; return false; }
        FichaPersonagem f = Ficha(escolhido);
        if (f == null || f.prefabJogador == null) { erro = "Esse personagem ainda não tem prefab de jogador."; return false; }
        foreach (FichaPersonagem p in personagens)
            if (p != null && p.prefabJogador != null && !nm.NetworkConfig.Prefabs.Contains(p.prefabJogador))
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = p.prefabJogador });
        SessaoDeJogo.MensagemAoVoltar = null;
        nm.NetworkConfig.ConnectionApproval = true;
        nm.NetworkConfig.ConnectionData = new byte[] { (byte)escolhido };
        SessaoDeJogo.PersonagemEscolhido = escolhido;
        return true;
    }

    private void CriarPartida()
    {
        erro = null;
        if (!Preparar()) return;
        NetworkManager nm = NetworkManager.Singleton;
        nm.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", porta, "0.0.0.0");
        nm.ConnectionApprovalCallback = Aprovar;
        nm.OnClientDisconnectCallback += AoDesconectar;
        SalaDeEspera.EscolhaInicial.Clear();
        SalaDeEspera.PrefabDoPersonagem = PrefabDo;
        SalaDeEspera.LugarDeNascimento = LugarDeNascimento;
        SalaDeEspera.RotacaoDeNascimento = rotacaoNascimento;
        SessaoDeJogo.Online = true; SessaoDeJogo.SouHost = true;
        if (nm.StartHost()) { SalaDeEspera.Abrir(nm); tela = Tela.Sala; assinaturaSala = ""; }
        else { erro = "Não consegui criar a partida. A porta " + porta + " pode estar em uso."; SessaoDeJogo.Online = false; }
    }

    private void EntrarNaPartida()
    {
        erro = null;
        if (!Preparar()) return;
        NetworkManager nm = NetworkManager.Singleton;
        nm.GetComponent<UnityTransport>().SetConnectionData(ipDigitado.Trim(), porta);
        nm.OnClientConnectedCallback += AoConectar;
        nm.OnClientDisconnectCallback += AoDesconectar;
        SessaoDeJogo.Online = true; SessaoDeJogo.SouHost = false;
        if (nm.StartClient()) { SalaDeEspera.Abrir(nm); tela = Tela.Conectando; }
        else { erro = "IP inválido."; SessaoDeJogo.Online = false; }
    }

    // só roda no host: decide se aceita o jogador e com qual personagem ele nasce
    private void Aprovar(NetworkManager.ConnectionApprovalRequest pedido, NetworkManager.ConnectionApprovalResponse resposta)
    {
        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= maximoDeJogadores)
        {
            resposta.Approved = false;
            resposta.Reason = "A partida está cheia.";
            return;
        }
        int idx = pedido.Payload != null && pedido.Payload.Length > 0 ? pedido.Payload[0] : 0;
        if (Ficha(idx) == null) idx = escolhido;
        SalaDeEspera.EscolhaInicial[pedido.ClientNetworkId] = idx;
        resposta.Approved = true;
        resposta.CreatePlayerObject = false; // o personagem nasce quando o host aperta COMEÇAR
        resposta.Pending = false;
    }

    private void AoConectar(ulong id)
    {
        if (id != NetworkManager.Singleton.LocalClientId) return;
        tela = Tela.Sala; // entra na sala de espera
        assinaturaSala = "";
    }

    // ------------------------------------------------------------------
    //  sala de espera
    // ------------------------------------------------------------------
    private GameObject PrefabDo(int i)
    {
        FichaPersonagem f = Ficha(i);
        if (f == null || f.prefabJogador == null) f = Ficha(escolhido);
        return f != null ? f.prefabJogador : null;
    }

    private Vector3 LugarDeNascimento(int lugar)
    {
        Vector3 lado = rotacaoNascimento * Vector3.right;
        Vector3 pos = nascimento + lado * (2.5f * (lugar - 1.5f));
        Terrain t = Terrain.activeTerrain;
        if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y + 0.1f;
        return pos;
    }

    private void AtualizarSala()
    {
        SalaDeEspera sala = SalaDeEspera.Atual;
        if (sala == null) return;
        if (sala.Comecou)
        {
            // o host começou: todo mundo vai para o mapa
            tela = Tela.Jogando;
            SessaoDeJogo.MenuAberto = false;
            SairDoMenu();
            return;
        }
        List<VagaNaSala> vagas = sala.EmOrdem();
        string ass = "";
        List<FichaPersonagem> fichas = new List<FichaPersonagem>();
        foreach (VagaNaSala v in vagas) { ass += v.cliente + ":" + v.personagem + ";"; fichas.Add(Ficha(v.personagem) ?? Ficha(0)); }
        if (ass != assinaturaSala && palco != null) { assinaturaSala = ass; palco.MostrarGrupo(fichas); }
    }

    private void AoDesconectar(ulong id)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;
        if (!nm.IsServer && (id == nm.LocalClientId || id == NetworkManager.ServerClientId))
        {
            string motivo = string.IsNullOrEmpty(nm.DisconnectReason) ? "Confira o IP e se o host está com a partida aberta." : nm.DisconnectReason;
            Voltar(tela == Tela.Conectando ? "Não foi possível entrar. " + motivo : (tela == Tela.Sala ? "O host fechou a sala." : "A conexão com a partida caiu."));
        }
    }

    private void SairDaPartida() { Voltar(null); }

    private void Voltar(string mensagem)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.Shutdown();
            Destroy(nm.gameObject);
        }
        SessaoDeJogo.Online = false;
        SessaoDeJogo.MensagemAoVoltar = mensagem;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private FichaPersonagem Ficha(int i)
    {
        if (i < 0 || i >= personagens.Count) return null;
        FichaPersonagem f = personagens[i];
        return f != null && f.disponivel ? f : null;
    }

    private static string IpsDesteComputador()
    {
        List<string> ips = new List<string>();
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation a in ni.GetIPProperties().UnicastAddresses)
                    if (a.Address.AddressFamily == AddressFamily.InterNetwork) ips.Add(a.Address.ToString());
            }
        }
        catch { }
        return ips.Count > 0 ? string.Join("  ou  ", ips) : "(não encontrado)";
    }

    // ------------------------------------------------------------------
    //  Tela
    // ------------------------------------------------------------------
    private void Estilos()
    {
        float e = Screen.height / 1080f;
        Color bronze = new Color(0.93f, 0.74f, 0.42f), claro = new Color(0.94f, 0.90f, 0.82f);
        titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(44 * e), fontStyle = FontStyle.Bold };
        titulo.normal.textColor = bronze;
        texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * e), wordWrap = true };
        texto.normal.textColor = claro;
        textoPequeno = new GUIStyle(texto) { fontSize = Mathf.RoundToInt(16 * e) };
        textoPequeno.normal.textColor = new Color(0.78f, 0.74f, 0.66f);
        estNome = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(64 * e), fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.LowerLeft };
        estNome.normal.textColor = new Color(0.97f, 0.93f, 0.86f);
        estTituloPers = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * e), fontStyle = FontStyle.Italic };
        estTituloPers.normal.textColor = bronze;
        estSecao = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * e), fontStyle = FontStyle.Bold };
        estSecao.normal.textColor = bronze;
        estBotao = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * e), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        estCarta = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * e), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter, wordWrap = true };
        estCarta.normal.textColor = claro;
        estP1 = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * e), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        estP1.normal.textColor = Color.white;
        estInterrogacao = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(80 * e), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        estInterrogacao.normal.textColor = new Color(0.55f, 0.45f, 0.35f);
        estCampo = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(20 * e), alignment = TextAnchor.MiddleLeft };
    }

    private void OnGUI()
    {
        if (titulo == null || Event.current.type == EventType.Layout) Estilos();
        float e = Screen.height / 1080f;
        Vector2 mouse = Event.current.mousePosition;

        if (tela == Tela.Jogando)
        {
            if (SessaoDeJogo.Online && !SessaoDeJogo.MenuAberto)
            {
                NetworkManager nm = NetworkManager.Singleton;
                int n = nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : 0;
                string info = SessaoDeJogo.SouHost ? "Você é o host  |  IP: " + meusIps + "  |  Jogadores: " + n : "Conectado à partida";
                GUI.Label(new Rect(Screen.width - 760f * e, Screen.height - 40f * e, 740f * e, 30f * e), info + "  |  " + teclaSair + ": sair", new GUIStyle(textoPequeno) { alignment = TextAnchor.MiddleRight });
            }
            return;
        }

        // escurece as bordas para o texto ficar legível sobre o palco
        Gradiente(new Rect(0, 0, Screen.width, 160f * e), 0.75f, true);
        Gradiente(new Rect(0, Screen.height - 470f * e, Screen.width, 470f * e), 0.88f, false);

        // ---- topo
        GUI.Label(new Rect(48f * e, 26f * e, 600f * e, 60f * e), "PINGLU", titulo);
        GUI.Label(new Rect(52f * e, 80f * e, 600f * e, 30f * e), tela == Tela.Sala ? "SALA DE ESPERA" : "ESCOLHA SEU GUERREIRO", estSecao);

        FichaPersonagem atual = Ficha(escolhido);

        // ---- nome grande à esquerda
        if (atual != null)
        {
            // à esquerda do personagem (que fica um pouco à direita do centro)
            float yNome = Screen.height - 640f * e;
            Sombra(new Rect(56f * e, yNome, Screen.width * 0.36f, 160f * e), atual.nome.ToUpper(), estNome, 4f * e);
            if (!string.IsNullOrEmpty(atual.titulo)) Sombra(new Rect(62f * e, yNome + 164f * e, Screen.width * 0.36f, 40f * e), atual.titulo, estTituloPers, 2f * e);
        }

        // ---- painel de informações à direita
        if (atual != null) PainelInfo(atual, e);

        // ---- fileira de cartas
        Cartas(e, mouse);

        // ---- opções
        float bw = 330f * e, bh = 66f * e, gap = 22f * e;
        float bx = (Screen.width - (bw * 3 + gap * 2)) * 0.5f, by = Screen.height - 165f * e;
        if (tela == Tela.Conectando)
        {
            Moldura(new Rect(bx, by - 10f * e, bw * 3 + gap * 2, bh + 20f * e), 0.9f, false);
            GUI.Label(new Rect(bx, by, bw * 2, bh), "  Conectando em " + ipDigitado + "...", new GUIStyle(texto) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(24 * e) });
            if (Botao(new Rect(bx + 2 * (bw + gap), by, bw, bh), "CANCELAR", mouse, e)) Voltar(null);
            return;
        }
        if (tela == Tela.Sala)
        {
            EtiquetasDoPalco(e);
            NetworkManager nmS = NetworkManager.Singleton;
            bool host = nmS != null && nmS.IsServer;
            int n = SalaDeEspera.Atual != null ? SalaDeEspera.Atual.Vagas.Count : 0;
            if (host)
            {
                Rect rc = new Rect(bx + bw + gap - 40f * e, by - 6f * e, bw + 80f * e, bh + 12f * e);
                Retangulo(new Rect(rc.x - 6f * e, rc.y - 6f * e, rc.width + 12f * e, rc.height + 12f * e), new Color(1f, 0.5f, 0.15f, 0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f)));
                if (Botao(rc, "COMEÇAR", mouse, e) && SalaDeEspera.Atual != null) SalaDeEspera.Atual.Comecar();
                GUI.Label(new Rect(bx, by + bh + 14f * e, bw * 3 + gap * 2, 30f * e),
                    "Sala aberta  •  Jogadores: " + n + " / " + maximoDeJogadores + "  •  Os outros entram com o IP: " + meusIps, textoPequeno);
            }
            else
            {
                GUI.Label(new Rect(bx, by, bw * 2 + gap, bh), "Aguardando o host começar a partida...  (" + n + " na sala)",
                    new GUIStyle(texto) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(24 * e) });
                GUI.Label(new Rect(bx, by + bh + 14f * e, bw * 3 + gap * 2, 30f * e), "Você pode trocar de personagem até o jogo começar.", textoPequeno);
            }
            if (Botao(new Rect(bx + 2 * (bw + gap) + (host ? 60f * e : 0f), by, bw - (host ? 60f * e : 0f), bh), "SAIR DA SALA", mouse, e)) Voltar(null);
            return;
        }
        if (Botao(new Rect(bx, by, bw, bh), "JOGAR SOZINHO", mouse, e)) JogarSozinho();
        if (Botao(new Rect(bx + bw + gap, by, bw, bh), "CRIAR PARTIDA", mouse, e)) CriarPartida();
        Rect rEntrar = new Rect(bx + 2 * (bw + gap), by, bw, bh);
        if (Botao(rEntrar, "ENTRAR NA PARTIDA", mouse, e)) EntrarNaPartida();
        GUI.Label(new Rect(rEntrar.x, rEntrar.yMax + 10f * e, 120f * e, 34f * e), "IP do host:", textoPequeno);
        ipDigitado = GUI.TextField(new Rect(rEntrar.x + 105f * e, rEntrar.yMax + 8f * e, bw - 105f * e, 34f * e), ipDigitado, estCampo);

        GUI.Label(new Rect(bx, by + bh + 12f * e, bw * 2, 30f * e), "Este computador: " + meusIps + "  (porta " + porta + ")   •   ← → trocam o personagem", textoPequeno);

        string msg = erro ?? SessaoDeJogo.MensagemAoVoltar;
        if (!string.IsNullOrEmpty(msg))
        {
            GUIStyle st = new GUIStyle(texto); st.normal.textColor = new Color(1f, 0.5f, 0.4f);
            GUI.Label(new Rect(bx, by + bh + 44f * e, bw * 3 + gap * 2, 40f * e), msg, st); // embaixo das opções, sem cobrir as cartas
        }
    }

    private void PainelInfo(FichaPersonagem f, float e)
    {
        float w = 500f * e;
        Rect r = new Rect(Screen.width - w - 56f * e, 150f * e, w, 400f * e);
        Moldura(r, 0.78f, false);
        float x = r.x + 26f * e, y = r.y + 20f * e, lw = w - 52f * e;
        GUI.Label(new Rect(x, y, lw, 90f * e), f.descricao, texto); y += 92f * e;
        GUI.Label(new Rect(x, y, lw, 30f * e), "HABILIDADES", estSecao); y += 32f * e;
        if (f.habilidades != null)
            foreach (string h in f.habilidades) { GUI.Label(new Rect(x, y, lw, 28f * e), "◆  " + h, textoPequeno); y += 26f * e; }
        y += 14f * e;
        GUI.Label(new Rect(x, y, lw, 30f * e), "ATRIBUTOS", estSecao); y += 34f * e;
        Barra(x, ref y, lw, e, "Força", f.forca);
        Barra(x, ref y, lw, e, "Defesa", f.defesa);
        Barra(x, ref y, lw, e, "Velocidade", f.velocidade);
        Barra(x, ref y, lw, e, "Alcance", f.alcance);
        r.height = y - r.y + 16f * e; // ajusta a altura ao conteúdo (vale no próximo quadro)
    }

    private void Barra(float x, ref float y, float w, float e, string nome, int valor)
    {
        GUI.Label(new Rect(x, y - 3f * e, 140f * e, 28f * e), nome, textoPequeno);
        float bx = x + 140f * e, bw = w - 140f * e, h = 12f * e;
        Retangulo(new Rect(bx, y + 4f * e, bw, h), new Color(0.12f, 0.09f, 0.07f, 0.95f));
        float seg = bw / 10f;
        for (int i = 0; i < 10; i++)
            Retangulo(new Rect(bx + i * seg + 1f, y + 5f * e, seg - 2f, h - 2f * e),
                i < valor ? Color.Lerp(new Color(0.75f, 0.45f, 0.18f), new Color(0.98f, 0.78f, 0.42f), i / 9f) : new Color(0.2f, 0.16f, 0.13f));
        y += 28f * e;
    }

    private void Cartas(float e, Vector2 mouse)
    {
        int n = Mathf.Max(4, personagens.Count);
        float cw = 150f * e, ch = 190f * e, gap = 16f * e;
        int total = n + 1; // + a carta aleatória
        float x0 = (Screen.width - (total * cw + (total - 1) * gap)) * 0.5f, y = Screen.height - 400f * e;
        for (int i = 0; i < total; i++)
        {
            Rect r = new Rect(x0 + i * (cw + gap), y, cw, ch);
            bool aleatoria = i == n;
            FichaPersonagem f = !aleatoria && i < personagens.Count ? personagens[i] : null;
            bool ok = aleatoria || (f != null && f.disponivel && f.prefabJogador != null);
            bool sel = !aleatoria && i == escolhido && ok;
            List<int> donos = DonosDaCarta(i);
            bool sobre = r.Contains(mouse) && ok;
            if (sobre) r = new Rect(r.x - 4f * e, r.y - 8f * e, r.width + 8f * e, r.height + 8f * e);

            if (sel) Retangulo(new Rect(r.x - 8f * e, r.y - 8f * e, r.width + 16f * e, r.height + 16f * e), new Color(1f, 0.55f, 0.2f, 0.18f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f)));
            Retangulo(r, new Color(0.07f, 0.05f, 0.05f, 0.95f));
            Texture ret = f != null && palco != null ? palco.Retrato(f) : null;
            if (ret != null && ok) GUI.DrawTexture(r, ret, ScaleMode.ScaleAndCrop);
            else if (aleatoria) GUI.Label(r, "?", new GUIStyle(estInterrogacao) { normal = { textColor = new Color(0.93f, 0.74f, 0.42f) } });
            else GUI.Label(r, "?", estInterrogacao);
            Gradiente(new Rect(r.x, r.yMax - 70f * e, r.width, 70f * e), 0.9f, false);
            string nome = aleatoria ? "ALEATÓRIO" : (f != null && ok ? f.nome : "EM BREVE");
            GUI.Label(new Rect(r.x + 6f * e, r.y, r.width - 12f * e, r.height - 10f * e), nome.ToUpper(), estCarta);

            Borda(r, sel ? 4f * e : 2f * e, sel ? new Color(0.98f, 0.76f, 0.38f) : (sobre ? new Color(0.8f, 0.6f, 0.32f) : new Color(0.4f, 0.3f, 0.18f)));
            // etiquetas P1, P2... de quem escolheu esta carta
            if (tela != Tela.Sala && sel) donos = new List<int> { 0 };
            for (int k = 0; k < donos.Count; k++)
            {
                float tw = 44f * e;
                float tx = r.center.x - (donos.Count * tw + (donos.Count - 1) * 4f * e) * 0.5f + k * (tw + 4f * e);
                Rect tag = new Rect(tx, r.yMax - 4f * e, tw, 26f * e);
                Retangulo(tag, CorDoJogador(donos[k]));
                Borda(tag, 2f * e, new Color(0.98f, 0.76f, 0.38f));
                GUI.Label(tag, "P" + (donos[k] + 1), estP1);
            }
            if (ok && GUI.Button(r, GUIContent.none, GUIStyle.none)) { if (aleatoria) EscolherAleatorio(); else Escolher(i); }
        }
    }

    // quais jogadores da sala (posição 0 = P1) escolheram a carta i
    private List<int> DonosDaCarta(int i)
    {
        List<int> l = new List<int>();
        if (tela != Tela.Sala || SalaDeEspera.Atual == null) return l;
        List<VagaNaSala> vagas = SalaDeEspera.Atual.EmOrdem();
        for (int k = 0; k < vagas.Count; k++) if (vagas[k].personagem == i) l.Add(k);
        return l;
    }

    private static Color CorDoJogador(int k)
    {
        Color[] c = { new Color(0.62f, 0.08f, 0.06f), new Color(0.10f, 0.30f, 0.62f), new Color(0.12f, 0.45f, 0.18f), new Color(0.50f, 0.20f, 0.58f) };
        return c[Mathf.Clamp(k, 0, 3)];
    }

    // etiquetas sobre a cabeça de cada personagem no palco
    private void EtiquetasDoPalco(float e)
    {
        if (palco == null || SalaDeEspera.Atual == null) return;
        List<VagaNaSala> vagas = SalaDeEspera.Atual.EmOrdem();
        ulong eu = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
        for (int k = 0; k < vagas.Count; k++)
        {
            Vector2 p;
            if (!palco.PosicaoDaCabeca(k, out p)) continue;
            string txt = "P" + (k + 1) + (vagas[k].cliente == NetworkManager.ServerClientId ? "  HOST" : "") + (vagas[k].cliente == eu ? "  •  VOCÊ" : "");
            Vector2 tam = estP1.CalcSize(new GUIContent(txt));
            Rect r = new Rect(p.x - tam.x * 0.5f - 12f * e, p.y - 30f * e, tam.x + 24f * e, 28f * e);
            Retangulo(r, CorDoJogador(k));
            Borda(r, 2f * e, new Color(0.98f, 0.76f, 0.38f));
            GUI.Label(r, txt, estP1);
        }
    }

    private bool Botao(Rect r, string rotulo, Vector2 mouse, float e)
    {
        bool sobre = r.Contains(mouse);
        Moldura(r, sobre ? 0.95f : 0.85f, sobre);
        Color c = estBotao.normal.textColor;
        estBotao.normal.textColor = sobre ? new Color(1f, 0.86f, 0.55f) : new Color(0.9f, 0.84f, 0.74f);
        GUI.Label(r, rotulo, estBotao);
        estBotao.normal.textColor = c;
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    private static void Moldura(Rect r, float opacidade, bool destaque)
    {
        Retangulo(r, new Color(0.06f, 0.045f, 0.04f, opacidade));
        Borda(r, destaque ? 3f : 2f, destaque ? new Color(0.98f, 0.74f, 0.36f) : new Color(0.62f, 0.44f, 0.2f));
        Retangulo(new Rect(r.x + 5f, r.y + 5f, r.width - 10f, 1f), new Color(0.62f, 0.44f, 0.2f, 0.35f));
    }

    private static void Borda(Rect r, float b, Color c)
    {
        Retangulo(new Rect(r.x, r.y, r.width, b), c);
        Retangulo(new Rect(r.x, r.yMax - b, r.width, b), c);
        Retangulo(new Rect(r.x, r.y, b, r.height), c);
        Retangulo(new Rect(r.xMax - b, r.y, b, r.height), c);
    }

    // faixa que vai do preto (na borda) ao transparente
    private static void Gradiente(Rect r, float forca, bool deCima)
    {
        int passos = 24;
        for (int i = 0; i < passos; i++)
        {
            float t = i / (float)passos;
            float a = forca * (deCima ? 1f - t : t);
            Retangulo(new Rect(r.x, r.y + r.height * t, r.width, r.height / passos + 1f), new Color(0.02f, 0.015f, 0.02f, a * a));
        }
    }

    private static void Sombra(Rect r, string s, GUIStyle st, float d)
    {
        Color c = st.normal.textColor;
        st.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + d, r.y + d, r.width, r.height), s, st);
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    private static void Retangulo(Rect r, Color c)
    {
        Color antes = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = antes;
    }
}
#endif
