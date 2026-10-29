#if PINGLU_REDE
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Uma vaga da sala: quem é o jogador, qual personagem escolheu e a ordem
// de chegada (o host é o P1).
public struct VagaNaSala : IEquatable<VagaNaSala>, INetworkSerializeByMemcpy
{
    public ulong cliente;
    public int personagem;
    public int ordem;
    public bool Equals(VagaNaSala o) { return cliente == o.cliente && personagem == o.personagem && ordem == o.ordem; }
}

// Sala de espera: todos conectam, escolhem o personagem na tela de seleção
// e só nascem no mapa quando o host aperta COMEÇAR.
// Fica num objeto da cena com NetworkObject (o menu Pinglu > 25 cria).
public class SalaDeEspera : NetworkBehaviour
{
    public static SalaDeEspera Atual { get; private set; }

    // preenchidos pelo menu inicial no PC do host
    public static Func<int, GameObject> PrefabDoPersonagem;
    public static Func<int, Vector3> LugarDeNascimento;
    public static Quaternion RotacaoDeNascimento = Quaternion.identity;
    // personagem que cada um escolheu antes de conectar (vem na aprovação da conexão)
    public static readonly Dictionary<ulong, int> EscolhaInicial = new Dictionary<ulong, int>();

    public NetworkList<VagaNaSala> Vagas;
    public NetworkVariable<bool> Comecou = new NetworkVariable<bool>(false);
    private int proximaOrdem;
    private int nascidos;

    private void Awake()
    {
        Vagas = new NetworkList<VagaNaSala>();
    }

    public override void OnNetworkSpawn()
    {
        Atual = this;
        if (!IsServer) return;
        NetworkManager.OnClientConnectedCallback += Conectou;
        NetworkManager.OnClientDisconnectCallback += Saiu;
        foreach (ulong id in NetworkManager.ConnectedClientsIds) Conectou(id);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= Conectou;
            NetworkManager.OnClientDisconnectCallback -= Saiu;
        }
        if (Atual == this) Atual = null;
    }

    // ------------------------------------------------------------------
    //  só no host
    // ------------------------------------------------------------------
    private void Conectou(ulong id)
    {
        for (int i = 0; i < Vagas.Count; i++) if (Vagas[i].cliente == id) return;
        int p;
        if (!EscolhaInicial.TryGetValue(id, out p)) p = 0;
        Vagas.Add(new VagaNaSala { cliente = id, personagem = p, ordem = proximaOrdem++ });
        if (Comecou.Value) Nascer(id, p); // chegou com o jogo já rolando: nasce na hora
    }

    private void Saiu(ulong id)
    {
        for (int i = Vagas.Count - 1; i >= 0; i--) if (Vagas[i].cliente == id) Vagas.RemoveAt(i);
        EscolhaInicial.Remove(id);
    }

    public void Comecar()
    {
        if (!IsServer || Comecou.Value) return;
        Comecou.Value = true;
        for (int i = 0; i < Vagas.Count; i++) Nascer(Vagas[i].cliente, Vagas[i].personagem);
    }

    private void Nascer(ulong id, int personagem)
    {
        GameObject prefab = PrefabDoPersonagem != null ? PrefabDoPersonagem(personagem) : null;
        if (prefab == null) { Debug.LogWarning("[Pinglu] Sem prefab para o personagem " + personagem); return; }
        Vector3 pos = LugarDeNascimento != null ? LugarDeNascimento(nascidos) : Vector3.zero;
        nascidos++;
        GameObject go = Instantiate(prefab, pos, RotacaoDeNascimento);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
    }

    // ------------------------------------------------------------------
    //  qualquer jogador pode trocar o próprio personagem (antes de começar)
    // ------------------------------------------------------------------
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void EscolherRpc(int personagem, RpcParams p = default(RpcParams))
    {
        if (Comecou.Value) return;
        ulong id = p.Receive.SenderClientId;
        for (int i = 0; i < Vagas.Count; i++)
        {
            if (Vagas[i].cliente != id) continue;
            VagaNaSala v = Vagas[i];
            v.personagem = personagem;
            Vagas[i] = v;
        }
    }

    // vagas em ordem de chegada (P1, P2, ...)
    public List<VagaNaSala> EmOrdem()
    {
        List<VagaNaSala> l = new List<VagaNaSala>();
        for (int i = 0; i < Vagas.Count; i++) l.Add(Vagas[i]);
        l.Sort((a, b) => a.ordem.CompareTo(b.ordem));
        return l;
    }
}
#endif
