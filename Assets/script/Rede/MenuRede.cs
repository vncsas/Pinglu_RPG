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

    [Header("Rede")]
    [SerializeField] private ushort porta = 7777;
    [SerializeField] private int maximoDeJogadores = 4;
    [SerializeField] private KeyCode teclaSair = KeyCode.F10;

    private enum Tela { Principal, Conectando, Jogando }
    private Tela tela = Tela.Principal;
    private int escolhido;
    private string ipDigitado = "127.0.0.1";
    private string erro;
    private Vector3 nascimento;
    private Quaternion rotacaoNascimento = Quaternion.identity;
    private int proximoLugar;
    private string meusIps = "";
    private GUIStyle titulo, texto, textoPequeno, botao, cartao;

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
        SessaoDeJogo.MenuAberto = true;
        SessaoDeJogo.Online = false;
    }

    private void Update()
    {
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
        if (jogadorDaCena != null) jogadorDaCena.SetActive(true);
        tela = Tela.Jogando;
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
        SessaoDeJogo.Online = true; SessaoDeJogo.SouHost = true;
        if (nm.StartHost()) { tela = Tela.Jogando; SessaoDeJogo.MenuAberto = false; }
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
        if (nm.StartClient()) tela = Tela.Conectando;
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
        FichaPersonagem f = Ficha(idx);
        if (f == null || f.prefabJogador == null) f = Ficha(escolhido);

        Vector3 lado = rotacaoNascimento * Vector3.right;
        Vector3 pos = nascimento + lado * (2.5f * proximoLugar);
        proximoLugar++;
        Terrain t = Terrain.activeTerrain;
        if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y + 0.1f;

        resposta.Approved = true;
        resposta.CreatePlayerObject = true;
        resposta.PlayerPrefabHash = f.prefabJogador.GetComponent<NetworkObject>().PrefabIdHash;
        resposta.Position = pos;
        resposta.Rotation = rotacaoNascimento;
        resposta.Pending = false;
    }

    private void AoConectar(ulong id)
    {
        if (id != NetworkManager.Singleton.LocalClientId) return;
        tela = Tela.Jogando;
        SessaoDeJogo.MenuAberto = false;
    }

    private void AoDesconectar(ulong id)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;
        if (!nm.IsServer && (id == nm.LocalClientId || id == NetworkManager.ServerClientId))
        {
            string motivo = string.IsNullOrEmpty(nm.DisconnectReason) ? "Confira o IP e se o host está com a partida aberta." : nm.DisconnectReason;
            Voltar(tela == Tela.Conectando ? "Não foi possível entrar. " + motivo : "A conexão com a partida caiu.");
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
        titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(64 * e), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titulo.normal.textColor = new Color(0.9f, 0.75f, 0.45f);
        texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * e), wordWrap = true };
        texto.normal.textColor = new Color(0.92f, 0.9f, 0.85f);
        textoPequeno = new GUIStyle(texto) { fontSize = Mathf.RoundToInt(17 * e) };
        botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * e), fontStyle = FontStyle.Bold };
        cartao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * e), fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.LowerCenter };
    }

    private void OnGUI()
    {
        if (titulo == null || Event.current.type == EventType.Layout) Estilos();
        float e = Screen.height / 1080f;

        if (tela == Tela.Jogando)
        {
            if (SessaoDeJogo.Online)
            {
                NetworkManager nm = NetworkManager.Singleton;
                int n = nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : 0;
                string info = SessaoDeJogo.SouHost ? "Você é o host  |  IP: " + meusIps + "  |  Jogadores: " + n : "Conectado à partida";
                GUI.Label(new Rect(16 * e, Screen.height - 60 * e, Screen.width, 30 * e), info + "  |  " + teclaSair + ": sair da partida", textoPequeno);
            }
            return;
        }

        Retangulo(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));
        float w = Mathf.Min(1100f * e, Screen.width - 40f);
        float x = (Screen.width - w) / 2f, y = 60f * e;
        GUI.Label(new Rect(0, y, Screen.width, 90 * e), "PINGLU", titulo);
        y += 110 * e;

        if (tela == Tela.Conectando)
        {
            GUI.Label(new Rect(x, y + 120 * e, w, 40 * e), "Conectando em " + ipDigitado + "...", new GUIStyle(texto) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(Screen.width / 2f - 120 * e, y + 200 * e, 240 * e, 56 * e), "Cancelar", botao)) Voltar(null);
            return;
        }

        // ---- escolha de personagem
        GUI.Label(new Rect(x, y, w, 34 * e), "Escolha seu personagem", texto);
        y += 40 * e;
        int qtd = Mathf.Max(4, personagens.Count);
        float cw = (w - 3 * 16 * e) / 4f, ch = 150 * e;
        for (int i = 0; i < 4 && i < qtd; i++)
        {
            FichaPersonagem f = i < personagens.Count ? personagens[i] : null;
            bool ok = f != null && f.disponivel && f.prefabJogador != null;
            Rect r = new Rect(x + i * (cw + 16 * e), y, cw, ch);
            Color cor = f != null ? f.corDestaque : Color.gray;
            Retangulo(new Rect(r.x - 4 * e, r.y - 4 * e, r.width + 8 * e, r.height + 8 * e), i == escolhido && ok ? cor : new Color(0.2f, 0.2f, 0.2f, 0.8f));
            GUI.enabled = ok;
            if (f != null && f.retrato != null) GUI.DrawTexture(r, f.retrato, ScaleMode.ScaleAndCrop);
            if (GUI.Button(r, f == null ? "Em breve" : (f.nome + (ok ? "" : "\n(em breve)")), cartao)) escolhido = i;
            GUI.enabled = true;
        }
        y += ch + 20 * e;
        FichaPersonagem atual = Ficha(escolhido);
        if (atual != null)
        {
            string hab = atual.habilidades != null && atual.habilidades.Length > 0 ? "\n• " + string.Join("\n• ", atual.habilidades) : "";
            GUI.Label(new Rect(x, y, w, 110 * e), atual.descricao + hab, textoPequeno);
        }
        y += 120 * e;

        // ---- botões
        float bw = (w - 32 * e) / 3f, bh = 60 * e;
        if (GUI.Button(new Rect(x, y, bw, bh), "Jogar sozinho", botao)) JogarSozinho();
        if (GUI.Button(new Rect(x + bw + 16 * e, y, bw, bh), "Criar partida", botao)) CriarPartida();
        Rect entrar = new Rect(x + 2 * (bw + 16 * e), y, bw, bh);
        if (GUI.Button(entrar, "Entrar na partida", botao)) EntrarNaPartida();
        y += bh + 14 * e;
        GUI.Label(new Rect(entrar.x, y, 140 * e, 34 * e), "IP do host:", textoPequeno);
        ipDigitado = GUI.TextField(new Rect(entrar.x + 110 * e, y, bw - 110 * e, 34 * e), ipDigitado, new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(20 * e) });
        y += 50 * e;

        GUI.Label(new Rect(x, y, w, 30 * e), "O IP deste computador é: " + meusIps + "   (porta " + porta + ")", textoPequeno);
        y += 34 * e;
        GUI.Label(new Rect(x, y, w, 60 * e), "Para jogar juntos, os PCs precisam estar na mesma rede (mesmo Wi-Fi). Para testar sozinho, abra o jogo duas vezes: uma em \"Criar partida\" e outra em \"Entrar\" com o IP 127.0.0.1.", textoPequeno);
        y += 64 * e;

        string msg = erro ?? SessaoDeJogo.MensagemAoVoltar;
        if (!string.IsNullOrEmpty(msg))
        {
            GUIStyle st = new GUIStyle(texto); st.normal.textColor = new Color(1f, 0.5f, 0.4f);
            GUI.Label(new Rect(x, y, w, 60 * e), msg, st);
        }
    }

    private static void Retangulo(Rect r, Color c)
    {
        Color antes = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = antes;
    }
}
#endif
