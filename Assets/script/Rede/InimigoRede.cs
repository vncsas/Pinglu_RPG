#if PINGLU_REDE
using Unity.Netcode;
using UnityEngine;

// Liga o esqueleto à rede. O host roda a inteligência e manda para os outros
// PCs a vida e as animações; o movimento vai pelo NetworkTransform.
// Golpes dos outros jogadores no esqueleto são enviados ao host.
public class InimigoRede : NetworkBehaviour
{
    private InimigoEsqueleto inimigo;
    private Vida vida;
    private readonly NetworkVariable<float> vidaAtual = new NetworkVariable<float>(1f);
    private readonly NetworkVariable<float> vidaMaxima = new NetworkVariable<float>(1f);

    private void Awake()
    {
        inimigo = GetComponent<InimigoEsqueleto>();
        vida = GetComponent<Vida>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            vidaMaxima.Value = vida.Maxima;
            vidaAtual.Value = vida.Atual;
            vida.AoLevarDano += AoLevarDano;
            vida.AoMorrer += AoMorrer;
            if (inimigo != null) inimigo.AoTocar += TocarParaTodos;
        }
        else
        {
            if (inimigo != null) inimigo.SoVisual = true;
            vida.DefinirMaxima(vidaMaxima.Value, false);
            if (vidaAtual.Value > 0f) vida.DefinirAtual(vidaAtual.Value);
            // golpe neste esqueleto: quem decide é o host
            vida.Redirecionar = (d, o) => { DanoRpc(d, o); return true; };
            vidaAtual.OnValueChanged += AoMudarVida;
            vidaMaxima.OnValueChanged += (a, b) => vida.DefinirMaxima(b, false);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            vida.AoLevarDano -= AoLevarDano;
            vida.AoMorrer -= AoMorrer;
            if (inimigo != null) inimigo.AoTocar -= TocarParaTodos;
        }
        else vidaAtual.OnValueChanged -= AoMudarVida;
    }

    private void AoLevarDano(float d, Vector3 o) { vidaAtual.Value = vida.Atual; }
    private void AoMorrer() { vidaAtual.Value = 0f; }

    private void AoMudarVida(float antes, float agora)
    {
        if (agora <= 0f) vida.Redirecionar = null;
        vida.DefinirAtual(agora);
        if (inimigo != null) inimigo.AtualizarVisualDaVida();
    }

    private void TocarParaTodos(string nome) { TocarRpc(nome); }

    [Rpc(SendTo.NotServer)]
    private void TocarRpc(string nome) { if (inimigo != null) inimigo.TocarRemoto(nome); }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DanoRpc(float dano, Vector3 origem) { vida.ReceberDano(dano, origem); }
}
#endif
