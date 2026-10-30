using UnityEngine;

// Liga a camada "Escudo" do Animator quando o personagem defende.
// As pernas continuam com a animação normal (parado/andando) e só o braço
// do escudo usa a pose de defesa. Funciona também no multiplayer, porque
// lê o parâmetro "Block", que é sincronizado pela rede.
public class EscudoAnimacao : MonoBehaviour
{
    [Tooltip("Quão rápido o escudo sobe/desce (maior = mais rápido)")]
    [SerializeField] private float velocidade = 14f;

    private Animator anim;
    private int camada = -1;
    private float peso;
    private static readonly int HASH_BLOCK = Animator.StringToHash("Block");

    private void Awake()
    {
        foreach (Animator a in GetComponentsInChildren<Animator>(true))
            if (a.runtimeAnimatorController != null) { anim = a; break; }
        if (anim != null) camada = anim.GetLayerIndex("Escudo");
    }

    private void Update()
    {
        if (anim == null || camada < 0) return;
        bool defendendo = anim.GetBool(HASH_BLOCK);
        peso = Mathf.MoveTowards(peso, defendendo ? 1f : 0f, Time.deltaTime * velocidade);
        anim.SetLayerWeight(camada, peso);
    }

    // tranco de golpe batendo no escudo
    public void Impacto()
    {
        if (anim == null) return;
        // personagens com a defesa no corpo todo (ex.: Tanque) têm o estado "escudo_impacto" na camada base
        if (camada < 0)
        {
            if (anim.HasState(0, Animator.StringToHash("escudo_impacto"))) anim.CrossFadeInFixedTime("escudo_impacto", 0.05f, 0, 0f);
            return;
        }
        if (!anim.HasState(camada, Animator.StringToHash("impacto"))) return;
        anim.CrossFadeInFixedTime("impacto", 0.05f, camada, 0f);
    }
}
