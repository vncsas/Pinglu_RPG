using UnityEngine;

// Voo do Tanque: F decola e pousa. Voando, Espaço sobe, C desce, WASD move (Shift mais rápido).
// Sem ataque nem defesa no ar. A animação (Decolar, Voar, Voar_Frente, Pousar) vem do parâmetro
// "Voando" do Animator, que o multiplayer sincroniza sozinho.
[RequireComponent(typeof(PlayerLocomotion))]
public class VooTanque : MonoBehaviour
{
    [SerializeField] private KeyCode teclaVoar = KeyCode.F;
    [SerializeField] private KeyCode teclaSubir = KeyCode.Space;
    [SerializeField] private KeyCode teclaDescer = KeyCode.C;
    [SerializeField] private float velocidadeVertical = 4f;
    [SerializeField] private float alturaMaxima = 30f;
    [Tooltip("Quanto mais rápido que andando ele voa")]
    [SerializeField] private float multiplicadorDeVelocidade = 1.5f;
    [SerializeField] private float tempoDeDecolagem = 0.8f;

    private PlayerLocomotion loco;
    private CharacterController cc;
    private Animator anim;
    private bool voando;
    private float inicioVoo;
    private static readonly int HASH_VOANDO = Animator.StringToHash("Voando");

    private void Awake()
    {
        loco = GetComponent<PlayerLocomotion>();
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        // só o dono do personagem controla o voo (nas cópias do multiplayer o locomotion fica desligado)
        if (!loco.enabled || !loco.Controlavel)
        {
            if (voando) Pousar();
            return;
        }
        if (SessaoDeJogo.MenuAberto) return;

        if (Input.GetKeyDown(teclaVoar))
        {
            if (!voando) { if (cc.isGrounded && !loco.Atacando && !loco.Bloqueando && !loco.Rolando) Decolar(); }
            else Pousar();
        }
        if (!voando) return;

        float v = 0f;
        if (Input.GetKey(teclaSubir)) v = velocidadeVertical;
        else if (Input.GetKey(teclaDescer)) v = -velocidadeVertical;
        if (v > 0f && AlturaAcimaDoChao() > alturaMaxima) v = 0f;
        if (Time.time - inicioVoo < tempoDeDecolagem) v = Mathf.Max(v, 2.5f);
        loco.VelocidadeVerticalDeVoo = v;

        // tocou o chão descendo: pousa
        if (v < 0f && cc.isGrounded && Time.time - inicioVoo > 1f) Pousar();
    }

    private void Decolar()
    {
        voando = true;
        inicioVoo = Time.time;
        loco.Voando = true;
        loco.MultiplicadorDeVoo = multiplicadorDeVelocidade;
        if (anim != null) anim.SetBool(HASH_VOANDO, true);
    }

    private void Pousar()
    {
        voando = false;
        loco.Voando = false;
        loco.VelocidadeVerticalDeVoo = 0f;
        if (anim != null) anim.SetBool(HASH_VOANDO, false);
    }

    private float AlturaAcimaDoChao()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore)) return hit.distance - 0.5f;
        return 0f;
    }

    private void OnDisable() { if (voando) Pousar(); }
}
