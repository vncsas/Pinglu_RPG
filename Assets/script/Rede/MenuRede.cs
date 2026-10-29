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
    [SerializeField] private GameObject modeloPortal;
    [SerializeField] private Quaternion rotacaoPortal = Quaternion.identity;
    [SerializeField] private Material materialPortal;
    [SerializeField] private Color corPortal = new Color(0.55f, 0.45f, 1f);
    [SerializeField] private Material materialCeu, materialMeio, materialPerto, materialChaoCinzas, materialRaio;
    [SerializeField] private Material materialPortalFundo;

    [Header("Visual da tela (o menu Pinglu > 23 preenche)")]
    [SerializeField] private Font fonteInterface;
    [SerializeField] private Font fonteTexto;
    [SerializeField] private Texture2D texBotao, texBotaoSobre, texCarta, texCartaEscolhida, texPainel;

    [Header("Rede")]
    [SerializeField] private ushort porta = 7777;
    [SerializeField] private int maximoDeJogadores = 4;
    [SerializeField] private KeyCode teclaSair = KeyCode.F10;

    private enum Tela { Principal, CriarSala, ProcurarSalas, Conectando, Sala, Portal, Jogando }

    // salas na rede local
    private DescobertaDeSalas descoberta;
    private string nomeSala = "";
    private string senhaSala = "";        // do host
    private string senhaDigitada = "";    // de quem entra
    private DescobertaDeSalas.Sala salaEscolhida;
    private bool porIP;
    private Vector2 rolagemSalas;
    private float claraoMundo;   // tela branca clareando depois de atravessar o portal
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
    // painel de informações: só aparece com o mouse sobre o personagem ou uma carta
    private FichaPersonagem fichaSobMouse, fichaDoPainel;
    private float alfaPainel;

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
        // os jogadores chegam pelo portal do mundo
        if (PortalDoMundo.Atual != null)
        {
            nascimento = PortalDoMundo.Atual.LugarDeChegada(0);
            rotacaoNascimento = PortalDoMundo.Atual.RotacaoDeChegada;
        }
        // esconde qualquer outro personagem que tenha ficado na cena (ex.: o X Bot antigo)
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
            if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.GetComponent<NetworkObject>() == null)
                p.gameObject.SetActive(false);
        for (int i = 0; i < personagens.Count; i++)
            if (personagens[i] != null && personagens[i].disponivel) { escolhido = i; break; }
        meusIps = IpsDesteComputador();
        descoberta = gameObject.AddComponent<DescobertaDeSalas>();
        nomeSala = "Sala de " + System.Environment.UserName;
        palco = gameObject.AddComponent<PalcoSelecao>();
        palco.materialFundo = materialFundo; palco.materialChao = materialChao; palco.materialParticula = materialParticula;
        palco.modeloPortal = modeloPortal; palco.rotacaoPortal = rotacaoPortal; palco.materialPortal = materialPortal; palco.corPortal = corPortal;
        palco.materialCeu = materialCeu; palco.materialMeio = materialMeio; palco.materialPerto = materialPerto;
        palco.materialChaoCinzas = materialChaoCinzas; palco.materialRaio = materialRaio;
        palco.materialPortalFundo = materialPortalFundo;
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

    // ------------------------------------------------------------------
    //  entrada no portal
    // ------------------------------------------------------------------
    private void EntrarSozinho()
    {
        if (palco == null) { JogarSozinho(); ChegarNoMundo(); return; }
        tela = Tela.Portal;
        palco.EntrarNoPortal(() => { JogarSozinho(); ChegarNoMundo(); });
    }

    private void ChegarNoMundo()
    {
        claraoMundo = 1f;
        StartCoroutine(EfeitoDeChegada());
    }

    private System.Collections.IEnumerator EfeitoDeChegada()
    {
        // espera o personagem deste PC existir (no online ele nasce pela rede)
        for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
        {
            foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
                if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.enabled)
                {
                    yield return new WaitForSecondsRealtime(0.2f); // no online, deixa a posição de nascimento chegar
                    PortalDoMundo portal = PortalDoMundo.Atual;
                    if (portal != null)
                    {
                        // sai caminhando do redemoinho, com a câmera de frente para o portal
                        portal.Clarao();
                        ThirdPersonCameraFollow cam = FindFirstObjectByType<ThirdPersonCameraFollow>();
                        if (cam != null) { cam.DefinirAlvo(p.transform); cam.DefinirYaw(Quaternion.LookRotation(-portal.Frente).eulerAngles.y); }
                        p.CaminharAutomatico(portal.Frente, 2.4f);
                    }
                    ChegadaDoPortal.Criar(p.transform.position, materialParticula, corPortal);
                    yield break;
                }
            yield return null;
        }
    }

    private void Update()
    {
        if (tela == Tela.Jogando && claraoMundo > 0f) claraoMundo = Mathf.MoveTowards(claraoMundo, 0f, Time.unscaledDeltaTime / 1.4f);
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
        if (escolhido == 0 || f == null || f.prefabJogador == null)
        {
            if (jogadorDaCena != null)
            {
                // inativo, ainda dá para mover sem confundir o controlador de movimento
                if (PortalDoMundo.Atual != null) jogadorDaCena.transform.SetPositionAndRotation(nascimento, rotacaoNascimento);
                jogadorDaCena.SetActive(true);
            }
        }
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
        // personagem escolhido + senha da sala
        byte[] senha = System.Text.Encoding.UTF8.GetBytes(SessaoDeJogo.SouHost ? senhaSala : senhaDigitada);
        byte[] dados = new byte[1 + senha.Length];
        dados[0] = (byte)escolhido;
        System.Array.Copy(senha, 0, dados, 1, senha.Length);
        nm.NetworkConfig.ConnectionData = dados;
        SessaoDeJogo.PersonagemEscolhido = escolhido;
        return true;
    }

    private void CriarPartida()
    {
        erro = null;
        SessaoDeJogo.SouHost = true;
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
        if (nm.StartHost())
        {
            SalaDeEspera.Abrir(nm);
            tela = Tela.Sala; assinaturaSala = "";
            descoberta.PararDeOuvir();
            // anuncia a sala na rede (nome, jogadores, se tem senha, se já começou)
            descoberta.Anunciar(() => DescobertaDeSalas.Montar(nomeSala,
                SalaDeEspera.Atual != null ? SalaDeEspera.Atual.Vagas.Count : 1, maximoDeJogadores,
                !string.IsNullOrEmpty(senhaSala), SalaDeEspera.Atual != null && SalaDeEspera.Atual.Comecou, porta));
        }
        else { erro = "Não consegui criar a partida. A porta " + porta + " pode estar em uso."; SessaoDeJogo.Online = false; }
    }

    private void EntrarNaPartida()
    {
        erro = null;
        SessaoDeJogo.SouHost = false;
        if (!Preparar()) return;
        NetworkManager nm = NetworkManager.Singleton;
        nm.GetComponent<UnityTransport>().SetConnectionData(ipDigitado.Trim(), porta);
        nm.OnClientConnectedCallback += AoConectar;
        nm.OnClientDisconnectCallback += AoDesconectar;
        SessaoDeJogo.Online = true; SessaoDeJogo.SouHost = false;
        if (nm.StartClient()) { SalaDeEspera.Abrir(nm); tela = Tela.Conectando; descoberta.PararDeOuvir(); }
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
        string senhaRecebida = pedido.Payload != null && pedido.Payload.Length > 1 ? System.Text.Encoding.UTF8.GetString(pedido.Payload, 1, pedido.Payload.Length - 1) : "";
        if (!string.IsNullOrEmpty(senhaSala) && senhaRecebida != senhaSala)
        {
            resposta.Approved = false;
            resposta.Reason = "Senha incorreta.";
            return;
        }
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
        if (PortalDoMundo.Atual != null) return PortalDoMundo.Atual.LugarDeChegada(lugar);
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
            // o host começou: o grupo atravessa o portal e todo mundo vai para o mapa
            tela = Tela.Portal;
            System.Action entrar = () => { tela = Tela.Jogando; SessaoDeJogo.MenuAberto = false; SairDoMenu(); ChegarNoMundo(); };
            if (palco != null) palco.EntrarNoPortal(entrar); else entrar();
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
    // ==================================================================
    //  TELA
    // ==================================================================
    private GUIStyle estBotaoBronze, estPainel, estCartaFr, estCartaEscFr, estNomeCarta, estRotulo, estDica, estErro, estCampoTexto, estTituloPainel, estTextoPainel, estLinhaSala;

    private void Estilos()
    {
        float e = Screen.height / 1080f;
        Color bronze = new Color(0.93f, 0.74f, 0.42f), claro = new Color(0.94f, 0.90f, 0.82f);
        Font fi = fonteInterface, ft = fonteTexto != null ? fonteTexto : fonteInterface;

        titulo = new GUIStyle(GUI.skin.label) { font = fi, fontSize = Mathf.RoundToInt(30 * e), alignment = TextAnchor.MiddleCenter };
        titulo.normal.textColor = bronze;
        texto = new GUIStyle(GUI.skin.label) { font = ft, fontSize = Mathf.RoundToInt(19 * e), wordWrap = true };
        texto.normal.textColor = claro;
        textoPequeno = new GUIStyle(texto) { fontSize = Mathf.RoundToInt(15 * e) };
        textoPequeno.normal.textColor = new Color(0.78f, 0.74f, 0.66f);
        estTituloPers = new GUIStyle(GUI.skin.label) { font = fi, fontSize = Mathf.RoundToInt(22 * e), wordWrap = true };
        estTituloPers.normal.textColor = bronze;
        estTituloPainel = new GUIStyle(GUI.skin.label) { font = fi, fontSize = Mathf.RoundToInt(19 * e) };
        estTituloPainel.normal.textColor = bronze;
        estTextoPainel = new GUIStyle(texto) { fontSize = Mathf.RoundToInt(18 * e) };
        estP1 = new GUIStyle(GUI.skin.label) { font = fi, fontSize = Mathf.RoundToInt(15 * e), alignment = TextAnchor.MiddleCenter };
        estP1.normal.textColor = Color.white;
        estInterrogacao = new GUIStyle(GUI.skin.label) { font = fi, fontSize = Mathf.RoundToInt(78 * e), alignment = TextAnchor.MiddleCenter };
        estInterrogacao.normal.textColor = new Color(0.55f, 0.45f, 0.35f);
        estNomeCarta = new GUIStyle(GUI.skin.label) { font = fi, fontSize = Mathf.RoundToInt(16 * e), alignment = TextAnchor.LowerCenter, wordWrap = true };
        estNomeCarta.normal.textColor = claro;
        estRotulo = new GUIStyle(textoPequeno) { font = fi, fontSize = Mathf.RoundToInt(15 * e) };
        estRotulo.normal.textColor = bronze;
        estDica = new GUIStyle(textoPequeno) { alignment = TextAnchor.MiddleCenter };
        estErro = new GUIStyle(texto) { alignment = TextAnchor.MiddleCenter };
        estErro.normal.textColor = new Color(1f, 0.5f, 0.4f);
        estCampoTexto = new GUIStyle(GUI.skin.textField) { font = ft, fontSize = Mathf.RoundToInt(19 * e), alignment = TextAnchor.MiddleLeft };
        estCampoTexto.padding = new RectOffset(10, 10, 4, 4);

        // botão de bronze: a imagem estica sem deformar os cantos
        estBotaoBronze = new GUIStyle(GUI.skin.button) { font = fi, fontSize = Mathf.RoundToInt(23 * e), alignment = TextAnchor.MiddleCenter };
        if (texBotao != null)
        {
            estBotaoBronze.normal.background = texBotao;
            estBotaoBronze.hover.background = texBotaoSobre != null ? texBotaoSobre : texBotao;
            estBotaoBronze.active.background = estBotaoBronze.hover.background;
            estBotaoBronze.focused.background = texBotao;
            estBotaoBronze.border = new RectOffset(18, 18, 18, 18);
        }
        estBotaoBronze.normal.textColor = new Color(0.92f, 0.86f, 0.74f);
        estBotaoBronze.hover.textColor = new Color(1f, 0.86f, 0.55f);
        estBotaoBronze.active.textColor = Color.white;
        estPainel = new GUIStyle(GUI.skin.box);
        if (texPainel != null) { estPainel.normal.background = texPainel; estPainel.border = new RectOffset(22, 22, 22, 22); }
        estCartaFr = new GUIStyle(GUIStyle.none);
        if (texCarta != null) { estCartaFr.normal.background = texCarta; estCartaFr.border = new RectOffset(16, 16, 16, 16); }
        estCartaEscFr = new GUIStyle(GUIStyle.none);
        if (texCartaEscolhida != null) { estCartaEscFr.normal.background = texCartaEscolhida; estCartaEscFr.border = new RectOffset(18, 18, 18, 18); }
        estLinhaSala = new GUIStyle(GUIStyle.none) { font = ft, fontSize = Mathf.RoundToInt(19 * e), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 0, 0) };
        estLinhaSala.normal.textColor = claro;
        estLinhaSala.hover.textColor = new Color(1f, 0.86f, 0.55f);
        botao = estBotaoBronze; cartao = estNomeCarta; estBotao = estBotaoBronze; estCarta = estNomeCarta; estSecao = estTituloPainel; estCampo = estCampoTexto;
    }

    private void OnGUI()
    {
        if (titulo == null || Event.current.type == EventType.Layout) Estilos();
        float e = Screen.height / 1080f;
        Vector2 mouse = Event.current.mousePosition;

        // durante a entrada no portal: só a cena (e o clarão no fim)
        if (tela == Tela.Portal)
        {
            float c = palco != null ? palco.Clarao : 1f;
            if (c > 0f) Retangulo(new Rect(0, 0, Screen.width, Screen.height), new Color(0.94f, 0.92f, 1f, c));
            return;
        }
        if (tela == Tela.Jogando)
        {
            if (claraoMundo > 0f) { GUI.depth = -100; Retangulo(new Rect(0, 0, Screen.width, Screen.height), new Color(0.94f, 0.92f, 1f, claraoMundo)); }
            if (SessaoDeJogo.Online && !SessaoDeJogo.MenuAberto)
            {
                NetworkManager nm = NetworkManager.Singleton;
                int n = nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : 0;
                string info = SessaoDeJogo.SouHost ? nomeSala + "  •  Jogadores: " + n : "Conectado à sala";
                GUI.Label(new Rect(Screen.width - 760f * e, Screen.height - 40f * e, 740f * e, 30f * e), info + "  •  " + teclaSair + ": sair", new GUIStyle(textoPequeno) { alignment = TextAnchor.MiddleRight });
            }
            return;
        }

        // escurece as bordas para o texto ficar legível sobre o palco
        Gradiente(new Rect(0, Screen.height - 460f * e, Screen.width, 460f * e), 0.88f, false);
        Gradiente(new Rect(0, 0, Screen.width * 0.45f, Screen.height), 0.0f, true);

        FichaPersonagem atual = Ficha(escolhido);
        if (atual != null) NomeDoPersonagem(atual, e);
        // mouse sobre algum personagem do palco?
        if (palco != null && (tela == Tela.Principal || tela == Tela.Sala))
        {
            List<FichaPersonagem> noPalco = new List<FichaPersonagem>();
            if (tela == Tela.Sala && SalaDeEspera.Atual != null) foreach (VagaNaSala v in SalaDeEspera.Atual.EmOrdem()) noPalco.Add(Ficha(v.personagem) ?? Ficha(0));
            else noPalco.Add(atual);
            for (int k = 0; k < noPalco.Count; k++)
            {
                Rect rp;
                if (palco.RetanguloNaTela(k, out rp) && rp.Contains(mouse)) fichaSobMouse = noPalco[k];
            }
        }
        if (fichaSobMouse != null) fichaDoPainel = fichaSobMouse;
        if (Event.current.type == EventType.Repaint)
            alfaPainel = Mathf.MoveTowards(alfaPainel, fichaSobMouse != null ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (alfaPainel > 0.01f && fichaDoPainel != null)
        {
            Color antes = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alfaPainel);
            PainelInfo(fichaDoPainel, e);
            GUI.color = antes;
        }
        if (Event.current.type == EventType.Repaint) fichaSobMouse = null; // recalculado a cada quadro
        if (tela == Tela.Sala) EtiquetasDoPalco(e);
        Cartas(e, mouse);

        // ---- parte de baixo
        float bw = 330f * e, bh = 70f * e, gap = 26f * e;
        float bx = (Screen.width - (bw * 3 + gap * 2)) * 0.5f, by = Screen.height - 160f * e;
        switch (tela)
        {
            case Tela.Principal:
                if (GUI.Button(new Rect(bx, by, bw, bh), "JOGAR", estBotaoBronze)) EntrarSozinho();
                if (GUI.Button(new Rect(bx + bw + gap, by, bw, bh), "CRIAR SALA", estBotaoBronze)) { erro = null; tela = Tela.CriarSala; }
                if (GUI.Button(new Rect(bx + 2 * (bw + gap), by, bw, bh), "PROCURAR SALAS", estBotaoBronze)) { erro = null; salaEscolhida = null; senhaDigitada = ""; tela = Tela.ProcurarSalas; descoberta.Ouvir(); }
                GUI.Label(new Rect(0, by + bh + 14f * e, Screen.width, 26f * e), "‹ ›  setas do teclado trocam o personagem", estDica);
                break;
            case Tela.CriarSala: DialogoCriarSala(e); break;
            case Tela.ProcurarSalas: DialogoProcurarSalas(e); break;
            case Tela.Conectando:
                GUI.Box(new Rect(bx, by - 14f * e, bw * 3 + gap * 2, bh + 28f * e), GUIContent.none, estPainel);
                GUI.Label(new Rect(bx + 30f * e, by, bw * 2, bh), "Entrando na sala...", new GUIStyle(titulo) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(24 * e) });
                if (GUI.Button(new Rect(bx + 2 * (bw + gap), by, bw, bh), "CANCELAR", estBotaoBronze)) Voltar(null);
                break;
            case Tela.Sala: RodapeDaSala(e, bx, by, bw, bh, gap); break;
        }

        string msg = erro ?? SessaoDeJogo.MensagemAoVoltar;
        if (!string.IsNullOrEmpty(msg) && tela != Tela.ProcurarSalas && tela != Tela.CriarSala)
            GUI.Label(new Rect(0, Screen.height - 46f * e, Screen.width, 40f * e), msg, estErro);
    }

    // ------------------------------------------------------------------
    //  nome do personagem: fonte própria, num espaço só à esquerda
    // ------------------------------------------------------------------
    private void NomeDoPersonagem(FichaPersonagem f, float e)
    {
        float largura = Screen.width * 0.27f, x = 64f * e;
        GUIStyle st = new GUIStyle(GUI.skin.label) { font = f.fonteNome != null ? f.fonteNome : fonteInterface, wordWrap = true, alignment = TextAnchor.LowerLeft };
        st.normal.textColor = f.corNome.a > 0f ? f.corNome : new Color(0.97f, 0.93f, 0.86f);
        int tam = Mathf.RoundToInt((f.tamanhoNome > 0 ? f.tamanhoNome : 64) * e);
        GUIContent c = new GUIContent(f.nome);
        // diminui até caber em no máximo 2 linhas, sem invadir o personagem
        st.fontSize = tam;
        while (st.fontSize > 24 && st.CalcHeight(c, largura) > st.fontSize * 2.5f) st.fontSize -= 2;
        float h = st.CalcHeight(c, largura);
        float y = Screen.height * 0.42f - h;
        Sombra(new Rect(x, y, largura, h), f.nome, st, 3f * e);
        if (!string.IsNullOrEmpty(f.titulo))
        {
            Rect r = new Rect(x + 4f * e, y + h + 6f * e, largura, 60f * e);
            Retangulo(new Rect(x + 4f * e, y + h + 2f * e, Mathf.Min(largura, 220f * e), 2f * e), new Color(0.75f, 0.55f, 0.25f, 0.8f));
            Sombra(r, f.titulo, estTituloPers, 2f * e);
        }
        if (alfaPainel < 0.5f)
        {
            GUIStyle dica = new GUIStyle(textoPequeno) { fontSize = Mathf.RoundToInt(14 * e) };
            dica.normal.textColor = new Color(0.78f, 0.74f, 0.66f, 0.7f * (1f - alfaPainel * 2f));
            GUI.Label(new Rect(x + 4f * e, y + h + 44f * e, largura, 24f * e), "Passe o mouse sobre o personagem para ver os detalhes", dica);
        }
    }

    private void PainelInfo(FichaPersonagem f, float e)
    {
        float w = 470f * e;
        Rect r = new Rect(Screen.width - w - 56f * e, 120f * e, w, 430f * e);
        GUI.Box(r, GUIContent.none, estPainel);
        float x = r.x + 30f * e, y = r.y + 26f * e, lw = w - 60f * e;
        float hd = estTextoPainel.CalcHeight(new GUIContent(f.descricao), lw);
        GUI.Label(new Rect(x, y, lw, hd), f.descricao, estTextoPainel); y += hd + 16f * e;
        GUI.Label(new Rect(x, y, lw, 30f * e), "HABILIDADES", estTituloPainel); y += 32f * e;
        if (f.habilidades != null)
            foreach (string h in f.habilidades)
            {
                float hh = textoPequeno.CalcHeight(new GUIContent("•  " + h), lw);
                GUI.Label(new Rect(x, y, lw, hh), "•  " + h, textoPequeno); y += hh + 4f * e;
            }
        y += 12f * e;
        GUI.Label(new Rect(x, y, lw, 30f * e), "ATRIBUTOS", estTituloPainel); y += 34f * e;
        Barra(x, ref y, lw, e, "Força", f.forca);
        Barra(x, ref y, lw, e, "Defesa", f.defesa);
        Barra(x, ref y, lw, e, "Velocidade", f.velocidade);
        Barra(x, ref y, lw, e, "Alcance", f.alcance);
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

    // ------------------------------------------------------------------
    //  cartas dos personagens
    // ------------------------------------------------------------------
    private void Cartas(float e, Vector2 mouse)
    {
        int n = Mathf.Max(4, personagens.Count);
        float cw = 160f * e, ch = 214f * e, gap = 18f * e;
        int total = n + 1; // + a carta aleatória
        float x0 = (Screen.width - (total * cw + (total - 1) * gap)) * 0.5f, y = Screen.height - 420f * e;
        bool podeEscolher = tela == Tela.Principal || tela == Tela.Sala;
        for (int i = 0; i < total; i++)
        {
            Rect r = new Rect(x0 + i * (cw + gap), y, cw, ch);
            bool aleatoria = i == n;
            FichaPersonagem f = !aleatoria && i < personagens.Count ? personagens[i] : null;
            bool ok = aleatoria || (f != null && f.disponivel && f.prefabJogador != null);
            bool sel = !aleatoria && i == escolhido && ok;
            bool sobre = r.Contains(mouse) && ok && podeEscolher;
            if (r.Contains(mouse) && f != null && ok && !aleatoria) fichaSobMouse = f;
            List<int> donos = DonosDaCarta(i);
            // a escolhida fica maior e levantada; passar o mouse levanta um pouco
            float cresce = sel ? 1.1f : (sobre ? 1.04f : 1f);
            r = new Rect(r.center.x - cw * cresce * 0.5f, r.yMax - ch * cresce - (sel ? 14f * e : (sobre ? 6f * e : 0f)), cw * cresce, ch * cresce);

            if (sel) Retangulo(new Rect(r.x - 10f * e, r.y - 10f * e, r.width + 20f * e, r.height + 20f * e), new Color(1f, 0.55f, 0.2f, 0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f)));
            Rect dentro = new Rect(r.x + 6f * e, r.y + 6f * e, r.width - 12f * e, r.height - 12f * e);
            Retangulo(dentro, new Color(0.07f, 0.05f, 0.05f, 0.97f));
            Texture ret = f != null && palco != null ? palco.Retrato(f) : null;
            if (ret != null && ok) GUI.DrawTexture(dentro, ret, ScaleMode.ScaleAndCrop);
            else if (aleatoria) GUI.Label(dentro, "?", new GUIStyle(estInterrogacao) { normal = { textColor = new Color(0.93f, 0.74f, 0.42f) } });
            else GUI.Label(dentro, "?", estInterrogacao);
            Gradiente(new Rect(dentro.x, dentro.yMax - 64f * e, dentro.width, 64f * e), 0.95f, false);
            string nome = aleatoria ? "Aleatório" : (f != null && ok ? f.nome : "Em breve");
            GUI.Label(new Rect(dentro.x + 4f * e, dentro.y, dentro.width - 8f * e, dentro.height - 8f * e), nome.ToUpper(), estNomeCarta);
            GUI.Box(r, GUIContent.none, sel ? estCartaEscFr : estCartaFr);
            if (tela != Tela.Sala && sel) donos = new List<int> { 0 };
            for (int k = 0; k < donos.Count; k++)
            {
                float tw = 44f * e;
                float tx = r.center.x - (donos.Count * tw + (donos.Count - 1) * 4f * e) * 0.5f + k * (tw + 4f * e);
                Rect tag = new Rect(tx, r.yMax - 6f * e, tw, 26f * e);
                Retangulo(tag, CorDoJogador(donos[k]));
                Borda(tag, 2f * e, new Color(0.98f, 0.76f, 0.38f));
                GUI.Label(tag, "P" + (donos[k] + 1), estP1);
            }
            if (ok && podeEscolher && GUI.Button(r, GUIContent.none, GUIStyle.none)) { if (aleatoria) EscolherAleatorio(); else Escolher(i); }
        }
    }

    // ------------------------------------------------------------------
    //  criar sala / procurar salas
    // ------------------------------------------------------------------
    private void DialogoCriarSala(float e)
    {
        float w = 760f * e, h = 230f * e;
        Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 30f * e, w, h);
        GUI.Box(r, GUIContent.none, estPainel);
        float x = r.x + 34f * e, y = r.y + 26f * e, lw = w - 68f * e;
        GUI.Label(new Rect(x, y, 230f * e, 36f * e), "NOME DA SALA", estRotulo);
        nomeSala = GUI.TextField(new Rect(x + 230f * e, y, lw - 230f * e, 38f * e), nomeSala, 30, estCampoTexto); y += 52f * e;
        GUI.Label(new Rect(x, y, 230f * e, 36f * e), "SENHA (opcional)", estRotulo);
        senhaSala = GUI.PasswordField(new Rect(x + 230f * e, y, lw - 230f * e, 38f * e), senhaSala, '•', 20, estCampoTexto); y += 58f * e;
        float bw = (lw - 20f * e) * 0.5f;
        if (GUI.Button(new Rect(x, y, bw, 62f * e), "ABRIR SALA", estBotaoBronze))
        {
            if (string.IsNullOrEmpty(nomeSala.Trim())) nomeSala = "Sala de " + System.Environment.UserName;
            CriarPartida();
        }
        if (GUI.Button(new Rect(x + bw + 20f * e, y, bw, 62f * e), "VOLTAR", estBotaoBronze)) { tela = Tela.Principal; erro = null; }
        if (!string.IsNullOrEmpty(erro)) GUI.Label(new Rect(r.x, r.y - 44f * e, w, 40f * e), erro, estErro);
    }

    private void DialogoProcurarSalas(float e)
    {
        float w = 900f * e, h = 520f * e;
        Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 30f * e, w, h);
        GUI.Box(r, GUIContent.none, estPainel);
        float x = r.x + 34f * e, y = r.y + 24f * e, lw = w - 68f * e;
        GUI.Label(new Rect(x, y, lw, 36f * e), "SALAS NA REDE", new GUIStyle(titulo) { alignment = TextAnchor.MiddleLeft }); y += 46f * e;

        Rect lista = new Rect(x, y, lw, 220f * e);
        Retangulo(lista, new Color(0.04f, 0.03f, 0.03f, 0.75f));
        List<DescobertaDeSalas.Sala> salas = descoberta.Salas;
        if (salas.Count == 0)
            GUI.Label(lista, descoberta.Problema ?? "Procurando salas na rede...\nPeça para alguém clicar em CRIAR SALA (no mesmo Wi-Fi).", new GUIStyle(textoPequeno) { alignment = TextAnchor.MiddleCenter });
        float lh = 48f * e;
        rolagemSalas = GUI.BeginScrollView(lista, rolagemSalas, new Rect(0, 0, lw - 20f * e, Mathf.Max(lista.height, salas.Count * lh)));
        for (int i = 0; i < salas.Count; i++)
        {
            DescobertaDeSalas.Sala s = salas[i];
            Rect linha = new Rect(0, i * lh, lw - 20f * e, lh - 4f * e);
            bool esc = salaEscolhida != null && salaEscolhida.ip == s.ip && salaEscolhida.porta == s.porta;
            if (esc) Retangulo(linha, new Color(0.55f, 0.35f, 0.12f, 0.45f));
            string extra = (s.temSenha ? "   •  com senha" : "") + (s.emJogo ? "   •  em jogo" : "");
            if (GUI.Button(linha, s.nome + "      " + s.jogadores + " / " + s.maximo + " jogadores" + extra, estLinhaSala)) { salaEscolhida = s; porIP = false; }
        }
        GUI.EndScrollView();
        y += 232f * e;

        // senha (se a sala pedir) ou IP (modo avançado)
        if (porIP)
        {
            GUI.Label(new Rect(x, y, 200f * e, 38f * e), "IP DO HOST", estRotulo);
            ipDigitado = GUI.TextField(new Rect(x + 200f * e, y, 300f * e, 38f * e), ipDigitado, estCampoTexto);
            GUI.Label(new Rect(x + 520f * e, y, 120f * e, 38f * e), "SENHA", estRotulo);
            senhaDigitada = GUI.PasswordField(new Rect(x + 620f * e, y, lw - 620f * e, 38f * e), senhaDigitada, '•', 20, estCampoTexto);
        }
        else if (salaEscolhida != null && salaEscolhida.temSenha)
        {
            GUI.Label(new Rect(x, y, 200f * e, 38f * e), "SENHA", estRotulo);
            senhaDigitada = GUI.PasswordField(new Rect(x + 200f * e, y, lw - 200f * e, 38f * e), senhaDigitada, '•', 20, estCampoTexto);
        }
        y += 54f * e;

        float bw = (lw - 40f * e) / 3f;
        GUI.enabled = porIP || salaEscolhida != null;
        if (GUI.Button(new Rect(x, y, bw, 62f * e), "ENTRAR", estBotaoBronze))
        {
            if (!porIP && salaEscolhida != null) { ipDigitado = salaEscolhida.ip; porta = (ushort)salaEscolhida.porta; }
            EntrarNaPartida();
        }
        GUI.enabled = true;
        if (GUI.Button(new Rect(x + bw + 20f * e, y, bw, 62f * e), porIP ? "LISTA DE SALAS" : "ENTRAR POR IP", estBotaoBronze)) porIP = !porIP;
        if (GUI.Button(new Rect(x + 2 * (bw + 20f * e), y, bw, 62f * e), "VOLTAR", estBotaoBronze)) { descoberta.PararDeOuvir(); tela = Tela.Principal; erro = null; }
        if (!string.IsNullOrEmpty(erro ?? SessaoDeJogo.MensagemAoVoltar)) GUI.Label(new Rect(r.x, r.y - 44f * e, w, 40f * e), erro ?? SessaoDeJogo.MensagemAoVoltar, estErro);
    }

    private void RodapeDaSala(float e, float bx, float by, float bw, float bh, float gap)
    {
        NetworkManager nmS = NetworkManager.Singleton;
        bool host = nmS != null && nmS.IsServer;
        int n = SalaDeEspera.Atual != null ? SalaDeEspera.Atual.Vagas.Count : 0;
        if (host)
        {
            Rect rc = new Rect(bx + bw + gap - 40f * e, by - 6f * e, bw + 80f * e, bh + 12f * e);
            Retangulo(new Rect(rc.x - 8f * e, rc.y - 8f * e, rc.width + 16f * e, rc.height + 16f * e), new Color(1f, 0.5f, 0.15f, 0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f)));
            if (GUI.Button(rc, "COMEÇAR", estBotaoBronze) && SalaDeEspera.Atual != null) SalaDeEspera.Atual.Comecar();
            GUI.Label(new Rect(0, by + bh + 14f * e, Screen.width, 26f * e),
                nomeSala + "  •  " + n + " / " + maximoDeJogadores + " jogadores" + (string.IsNullOrEmpty(senhaSala) ? "  •  sala aberta" : "  •  com senha"), estDica);
        }
        else
        {
            GUI.Label(new Rect(bx, by, bw * 2 + gap, bh), "Aguardando o host começar...  (" + n + " na sala)", new GUIStyle(titulo) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(22 * e) });
            GUI.Label(new Rect(0, by + bh + 14f * e, Screen.width, 26f * e), "Você pode trocar de personagem até o jogo começar.", estDica);
        }
        if (GUI.Button(new Rect(bx + 2 * (bw + gap) + (host ? 60f * e : 0f), by, bw - (host ? 60f * e : 0f), bh), "SAIR DA SALA", estBotaoBronze)) Voltar(null);
    }

    // ------------------------------------------------------------------
    //  peças comuns
    // ------------------------------------------------------------------
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
        if (forca <= 0f) return;
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
