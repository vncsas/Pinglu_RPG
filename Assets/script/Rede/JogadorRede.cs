#if PINGLU_REDE
using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Fica no prefab do jogador. No PC do dono, liga os controles, a câmera e a
// interface. Nos outros PCs, esse mesmo personagem vira uma "cópia" que se
// mexe com os dados da rede e repete os golpes, pulos e rolagens do dono
// (com os efeitos de espada e de magia).
// O dano dos monstros (calculado no host) é entregue ao PC do dono.
public class JogadorRede : NetworkBehaviour
{
    public static JogadorRede Local { get; private set; }

    private PlayerLocomotion loco;
    private Vida vida;

    private void Awake()
    {
        loco = GetComponent<PlayerLocomotion>();
        vida = GetComponent<Vida>();
    }

    public override void OnNetworkSpawn()
    {
        bool meu = IsOwner;
        gameObject.name = (meu ? "Eu - " : "Jogador " + OwnerClientId + " - ") + gameObject.name.Replace("(Clone)", "").Trim();

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
            if (loco != null) { loco.AoIniciarGolpe += EnviarGolpe; loco.AoAcao += EnviarAcao; }
            if (vida != null) vida.AoMorrer += EnviarMorte;
            // o controlador de movimento guarda a posição do molde: acerta para onde o personagem nasceu
            if (SalaDeEspera.PosicaoPendente.HasValue) { Teleportar(SalaDeEspera.PosicaoPendente.Value, SalaDeEspera.RotacaoPendente); SalaDeEspera.PosicaoPendente = null; }
            else StartCoroutine(Ressincronizar());
            HUDJogador.Mostrar(SessaoDeJogo.SouHost ? "Partida começou! Boa sorte." : "Você entrou na partida!", 4f);
        }
        else
        {
            if (IsServer && vida != null)
            {
                // no host: monstros batem nesta cópia, mas quem leva o dano é o PC do dono
                vida.Redirecionar = (d, o) => { LevarDanoRpc(d, o); return true; };
                vida.RedirecionarCura = v => CurarRpc(v);
            }
            HUDJogador.Mostrar("Um jogador entrou na partida", 3f);
            if (PortalDoMundo.Atual != null) PortalDoMundo.Atual.Clarao(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (loco != null) { loco.AoIniciarGolpe -= EnviarGolpe; loco.AoAcao -= EnviarAcao; }
        if (vida != null) vida.AoMorrer -= EnviarMorte;
        if (Local == this) Local = null;
        else HUDJogador.Mostrar("Um jogador saiu da partida", 3f);
    }

    // ------------------------------------------------------------------
    //  posição
    // ------------------------------------------------------------------
    public void Teleportar(Vector3 pos, Quaternion rot)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;
    }

    private IEnumerator Ressincronizar()
    {
        yield return null;
        Teleportar(transform.position, transform.rotation);
    }

    // ------------------------------------------------------------------
    //  golpes e ações: o dono avisa, os outros PCs repetem
    // ------------------------------------------------------------------
    private void EnviarGolpe(int indice) { GolpeRpc(indice); }
    private void EnviarAcao(int codigo) { if (codigo != 4) AcaoRpc(codigo); }
    private void EnviarMorte() { MorreuRpc(); }

    [Rpc(SendTo.NotOwner)]
    private void GolpeRpc(int indice) { if (loco != null) loco.TocarGolpeRemoto(indice); }

    [Rpc(SendTo.NotOwner)]
    private void AcaoRpc(int codigo) { if (loco != null) loco.TocarAcaoRemota(codigo); }

    [Rpc(SendTo.NotOwner)]
    private void MorreuRpc()
    {
        if (loco != null) loco.TocarAcaoRemota(4);
        if (vida != null) { vida.Redirecionar = null; vida.DefinirAtual(0f); }
    }

    [Rpc(SendTo.NotOwner)]
    private void ReviveuRpc()
    {
        if (vida != null)
        {
            vida.Reviver();
            if (IsServer) vida.Redirecionar = (d, o) => { LevarDanoRpc(d, o); return true; };
        }
        if (loco != null) loco.Reviver();
    }

    // dano e cura vindos do host (monstros), aplicados no PC do dono (com bloqueio, rolagem e interface)
    [Rpc(SendTo.Owner)]
    private void LevarDanoRpc(float dano, Vector3 origem) { if (vida != null) vida.ReceberDano(dano, origem); }

    [Rpc(SendTo.Owner)]
    private void CurarRpc(float valor) { if (vida != null) vida.Curar(valor); }

    // ------------------------------------------------------------------
    //  renascer no online (R), perto de onde caiu
    // ------------------------------------------------------------------
    private void Update()
    {
        if (!IsOwner || vida == null || !vida.Morto || !Input.GetKeyDown(KeyCode.R)) return;
        vida.Reviver();
        if (loco != null) loco.Reviver();
        Teleportar(transform.position + Vector3.up * 0.5f, transform.rotation);
        ReviveuRpc();
        HUDJogador.Mostrar("Você voltou à luta!", 2.5f);
    }
}
#endif
