using UnityEngine;

// Garante que a espada esteja no lugar certo (mão ou costas) de acordo com o
// parâmetro "Armado" do Animator. As animações de guardar/sacar já trocam a
// espada de lugar por evento; este script só corrige se algo pular o evento
// (golpe interrompido, rolagem no meio da animação, jogador entrando no
// multiplayer no meio de uma troca...). Usa o script GuardaEspada do modelo.
public class SincronizarEspada : MonoBehaviour
{
    private Animator anim;
    private bool temParametro;
    private float proximaChecagem;
    private static readonly int HASH_ARMADO = Animator.StringToHash("Armado");
    private static readonly int HASH_GUARDAR = Animator.StringToHash("guardar");
    private static readonly int HASH_SACAR = Animator.StringToHash("sacar");

    private void Awake()
    {
        foreach (Animator a in GetComponentsInChildren<Animator>(true))
            if (a.runtimeAnimatorController != null) { anim = a; break; }
        if (anim == null) return;
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.nameHash == HASH_ARMADO) temParametro = true;
    }

    private void Update()
    {
        if (!temParametro || Time.time < proximaChecagem) return;
        proximaChecagem = Time.time + 0.2f;
        if (anim.IsInTransition(0)) return;
        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
        if (st.shortNameHash == HASH_GUARDAR || st.shortNameHash == HASH_SACAR) return; // a própria animação troca
        anim.gameObject.SendMessage(anim.GetBool(HASH_ARMADO) ? "SacarEspada" : "GuardarEspada", SendMessageOptions.DontRequireReceiver);
    }
}
