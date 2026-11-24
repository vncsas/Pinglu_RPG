#if PINGLU_REDE
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Uma vaga da sala: quem é o jogador, qual personagem escolheu e a ordem
// de chegada (o host é o P1).
public struct VagaNaSala
{
    public ulong cliente;
    public int personagem;
    public int ordem;
}

// Sala de espera: todos conectam, escolhem o personagem na tela de seleção
// e só nascem no mapa quando o host aperta COMEÇAR.
// Funciona por mensagens diretas entre os PCs (não depende de nenhum objeto
// de rede na cena), então não tem como os PCs "não se acharem".
//   cliente -> host : "troquei de personagem"
//   host -> todos   : "a sala agora está assim" (vagas + se já começou)
public class SalaDeEspera : MonoBehaviour
{
    public static SalaDeEspera Atual { get; private set; }

    // preenchidos pelo menu inicial no PC do host
    public static Func<int, GameObject> PrefabDoPersonagem;
    public static Func<int, Vector3> LugarDeNascimento;
    public static Quaternion RotacaoDeNascimento = Quaternion.identity;
    // personagem que cada um escolheu antes de conectar (vem na aprovação da conexão)
    public static readonly Dictionary<ulong, int> EscolhaInicial = new Dictionary<ulong, int>();

    private const string MSG_ESCOLHA = "Pinglu.Escolha";
    private const string MSG_SALA = "Pinglu.Sala";
    private const string MSG_POS = "Pinglu.Nascer";
    private const string MSG_AVISO = "Pinglu.Aviso";

    // lugar de nascimento que o host mandou (se a mensagem chegar antes do personagem)
    public static Vector3? PosicaoPendente;
    public static Quaternion RotacaoPendente;

    public readonly List<VagaNaSala> Vagas = new List<VagaNaSala>();
    public bool Comecou { get; private set; }

    private NetworkManager nm;
    private bool souHost;
    private int proximaOrdem, nascidos;

    // chamado logo depois de StartHost / StartClient
    public static SalaDeEspera Abrir(NetworkManager nm)
    {
        if (Atual != null) Destroy(Atual.gameObject);
        GameObject g = new GameObject("Sala de Espera (rede)");
        SalaDeEspera s = g.AddComponent<SalaDeEspera>();
        s.nm = nm;
        s.souHost = nm.IsServer;
        Atual = s;
        CustomMessagingManager cm = nm.CustomMessagingManager;
        if (s.souHost)
        {
            cm.RegisterNamedMessageHandler(MSG_ESCOLHA, s.AoReceberEscolha);
            nm.OnClientConnectedCallback += s.Conectou;
            nm.OnClientDisconnectCallback += s.Saiu;
            foreach (ulong id in nm.ConnectedClientsIds) s.Conectou(id);
            SessaoDeJogo.AvisarTodos = s.AvisarTodos;
            SessaoDeJogo.SpawnarNaRede = obj => { NetworkObject no = obj.GetComponent<NetworkObject>(); if (no != null && !no.IsSpawned) no.Spawn(true); };
        }
        else
        {
            cm.RegisterNamedMessageHandler(MSG_SALA, s.AoReceberSala);
            cm.RegisterNamedMessageHandler(MSG_POS, s.AoReceberPosicao);
            cm.RegisterNamedMessageHandler(MSG_AVISO, s.AoReceberAviso);
        }
        PosicaoPendente = null;
        return s;
    }

    private void Awake()
    {
        // versão antiga deixou um objeto "Sala de Espera" com NetworkObject na cena: não é mais usado
        if (GetComponent<NetworkObject>() != null) { Destroy(gameObject); }
    }

    private void OnDestroy()
    {
        if (Atual == this) Atual = null;
        if (nm == null) return;
        if (nm.CustomMessagingManager != null)
        {
            if (souHost) nm.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_ESCOLHA);
            else foreach (string m in new[] { MSG_SALA, MSG_POS, MSG_AVISO }) nm.CustomMessagingManager.UnregisterNamedMessageHandler(m);
        }
        if (souHost) { SessaoDeJogo.AvisarTodos = null; SessaoDeJogo.SpawnarNaRede = null; }
        if (souHost)
        {
            nm.OnClientConnectedCallback -= Conectou;
            nm.OnClientDisconnectCallback -= Saiu;
        }
    }

    // ------------------------------------------------------------------
    //  ações (qualquer PC)
    // ------------------------------------------------------------------
    public void Escolher(int personagem)
    {
        if (Comecou || nm == null) return;
        if (souHost) { MudarEscolha(nm.LocalClientId, personagem); return; }
        using (FastBufferWriter w = new FastBufferWriter(8, Allocator.Temp))
        {
            w.WriteValueSafe(personagem);
            nm.CustomMessagingManager.SendNamedMessage(MSG_ESCOLHA, NetworkManager.ServerClientId, w);
        }
    }

    public void Comecar()
    {
        if (!souHost || Comecou) return;
        Comecou = true;
        foreach (VagaNaSala v in EmOrdem()) Nascer(v.cliente, v.personagem);
        Enviar();
    }

    // vagas em ordem de chegada (P1, P2, ...)
    public List<VagaNaSala> EmOrdem()
    {
        List<VagaNaSala> l = new List<VagaNaSala>(Vagas);
        l.Sort((a, b) => a.ordem.CompareTo(b.ordem));
        return l;
    }

    // ------------------------------------------------------------------
    //  host
    // ------------------------------------------------------------------
    private void Conectou(ulong id)
    {
        for (int i = 0; i < Vagas.Count; i++) if (Vagas[i].cliente == id) { Enviar(); return; }
        int p;
        if (!EscolhaInicial.TryGetValue(id, out p)) p = 0;
        Vagas.Add(new VagaNaSala { cliente = id, personagem = p, ordem = proximaOrdem++ });
        if (Comecou) Nascer(id, p); // chegou com o jogo já rolando: nasce na hora
        Enviar();
    }

    private void Saiu(ulong id)
    {
        for (int i = Vagas.Count - 1; i >= 0; i--) if (Vagas[i].cliente == id) Vagas.RemoveAt(i);
        EscolhaInicial.Remove(id);
        Enviar();
    }

    private void AoReceberEscolha(ulong remetente, FastBufferReader leitor)
    {
        int p;
        leitor.ReadValueSafe(out p);
        MudarEscolha(remetente, p);
    }

    private void MudarEscolha(ulong id, int personagem)
    {
        if (Comecou) return;
        for (int i = 0; i < Vagas.Count; i++)
        {
            if (Vagas[i].cliente != id) continue;
            VagaNaSala v = Vagas[i];
            v.personagem = personagem;
            Vagas[i] = v;
        }
        Enviar();
    }

    private void Nascer(ulong id, int personagem)
    {
        GameObject prefab = PrefabDoPersonagem != null ? PrefabDoPersonagem(personagem) : null;
        if (prefab == null) { Debug.LogWarning("[Pinglu] Sem prefab para o personagem " + personagem); return; }
        Vector3 pos = LugarDeNascimento != null ? LugarDeNascimento(nascidos) : Vector3.zero;
        nascidos++;
        GameObject go = Instantiate(prefab, pos, RotacaoDeNascimento);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
        // garante o lugar certo no PC do jogador (o controlador de movimento lá pode guardar a posição do molde)
        if (id != nm.LocalClientId)
            using (FastBufferWriter w = new FastBufferWriter(32, Allocator.Temp))
            {
                w.WriteValueSafe(pos.x); w.WriteValueSafe(pos.y); w.WriteValueSafe(pos.z);
                w.WriteValueSafe(RotacaoDeNascimento.eulerAngles.y);
                nm.CustomMessagingManager.SendNamedMessage(MSG_POS, id, w);
            }
    }

    // mensagem na tela de todo mundo (ondas, avisos)
    private void AvisarTodos(string texto, float tempo)
    {
        HUDJogador.Mostrar(texto, tempo);
        if (nm == null || nm.CustomMessagingManager == null) return;
        List<ulong> outros = new List<ulong>();
        foreach (ulong id in nm.ConnectedClientsIds) if (id != nm.LocalClientId) outros.Add(id);
        if (outros.Count == 0) return;
        using (FastBufferWriter w = new FastBufferWriter(16 + texto.Length * 2 + 8, Allocator.Temp))
        {
            w.WriteValueSafe(texto);
            w.WriteValueSafe(tempo);
            nm.CustomMessagingManager.SendNamedMessage(MSG_AVISO, outros, w);
        }
    }

    // manda o estado da sala para todos os outros PCs
    private void Enviar()
    {
        if (!souHost || nm == null || nm.CustomMessagingManager == null) return;
        List<ulong> outros = new List<ulong>();
        foreach (ulong id in nm.ConnectedClientsIds) if (id != nm.LocalClientId) outros.Add(id);
        if (outros.Count == 0) return;
        using (FastBufferWriter w = new FastBufferWriter(16 + Vagas.Count * 24, Allocator.Temp))
        {
            w.WriteValueSafe(Comecou);
            w.WriteValueSafe(Vagas.Count);
            foreach (VagaNaSala v in Vagas)
            {
                w.WriteValueSafe(v.cliente);
                w.WriteValueSafe(v.personagem);
                w.WriteValueSafe(v.ordem);
            }
            nm.CustomMessagingManager.SendNamedMessage(MSG_SALA, outros, w);
        }
    }

    // ------------------------------------------------------------------
    //  outros PCs
    // ------------------------------------------------------------------
    private void AoReceberPosicao(ulong remetente, FastBufferReader leitor)
    {
        float x, y, z, ry;
        leitor.ReadValueSafe(out x); leitor.ReadValueSafe(out y); leitor.ReadValueSafe(out z); leitor.ReadValueSafe(out ry);
        Vector3 p = new Vector3(x, y, z);
        Quaternion r = Quaternion.Euler(0f, ry, 0f);
        if (JogadorRede.Local != null) JogadorRede.Local.Teleportar(p, r);
        else { PosicaoPendente = p; RotacaoPendente = r; }
    }

    private void AoReceberAviso(ulong remetente, FastBufferReader leitor)
    {
        string texto; float tempo;
        leitor.ReadValueSafe(out texto);
        leitor.ReadValueSafe(out tempo);
        HUDJogador.Mostrar(texto, tempo);
    }
    private void AoReceberSala(ulong remetente, FastBufferReader leitor)
    {
        bool comecou; int n;
        leitor.ReadValueSafe(out comecou);
        leitor.ReadValueSafe(out n);
        Vagas.Clear();
        for (int i = 0; i < n; i++)
        {
            VagaNaSala v;
            leitor.ReadValueSafe(out v.cliente);
            leitor.ReadValueSafe(out v.personagem);
            leitor.ReadValueSafe(out v.ordem);
            Vagas.Add(v);
        }
        Comecou = comecou;
    }
}
#endif
