#if PINGLU_REDE
using Unity.Netcode;
using UnityEngine;

// Fica no prefab do jogador. No PC do dono, liga os controles, a câmera e a
// interface. Nos outros PCs, esse mesmo personagem vira só uma "cópia" que
// se mexe sozinha com os dados que chegam pela rede.
public class JogadorRede : NetworkBehaviour
{
    public static JogadorRede Local { get; private set; }

    public override void OnNetworkSpawn()
    {
        bool meu = IsOwner;
        gameObject.name = (meu ? "Eu - " : "Jogador " + OwnerClientId + " - ") + gameObject.name.Replace("(Clone)", "").Trim();

        PlayerLocomotion loco = GetComponent<PlayerLocomotion>();
        CombateJogador combate = GetComponent<CombateJogador>();
        HUDJogador hud = GetComponent<HUDJogador>();
        if (loco != null) loco.enabled = meu;
        if (combate != null) combate.enabled = meu;
        if (hud != null) hud.enabled = meu;

        if (meu)
        {
            Local = this;
            ThirdPersonCameraFollow cam = FindFirstObjectByType<ThirdPersonCameraFollow>();
            if (cam != null) cam.DefinirAlvo(transform);
            HUDJogador.Mostrar(SessaoDeJogo.SouHost ? "Partida criada! Esperando os outros jogadores..." : "Você entrou na partida!", 4f);
        }
        else
        {
            HUDJogador.Mostrar("Um jogador entrou na partida", 3f);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this) Local = null;
        else if (IsSpawned || NetworkManager != null) HUDJogador.Mostrar("Um jogador saiu da partida", 3f);
    }
}
#endif
