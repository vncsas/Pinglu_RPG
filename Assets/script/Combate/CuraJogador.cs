using UnityEngine;

// Cura do Curandeiro. O Animation Event "LancarCura" (disparo de Curar_Lancar) chama LancarCura(),
// que cura os jogadores vivos perto dele (e ele mesmo, um pouco menos). O efeito visual (orbe e anéis)
// fica por conta do CuraCurandeiro, que recebe o mesmo evento.
// No multiplayer, curar a cópia de outro jogador envia a cura ao PC do dono (veja JogadorRede).
[RequireComponent(typeof(PlayerLocomotion))]
public class CuraJogador : MonoBehaviour
{
    [SerializeField] private float cura = 35f;
    [Tooltip("Fração da cura que ele recebe em si mesmo")]
    [SerializeField, Range(0f, 1f)] private float curaEmSi = 0.6f;
    [SerializeField] private float raio = 7f;
    [Tooltip("Tempo mínimo entre uma cura e outra (segundos)")]
    [SerializeField] private float intervalo = 1.4f;

    private PlayerLocomotion loco;
    private Vida minhaVida;
    private float proxima;

    private void Awake()
    {
        loco = GetComponent<PlayerLocomotion>();
        minhaVida = GetComponent<Vida>();
    }

    // chamado pelo Animation Event do clipe (só o dono do personagem aplica a cura)
    public void LancarCura()
    {
        if (loco == null || !loco.enabled || Time.time < proxima) return;
        proxima = Time.time + intervalo;
        loco.MarcarCombate();
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            Vida v = p.GetComponent<Vida>();
            if (v == null || v.Morto) continue;
            Vector3 d = p.transform.position - transform.position; d.y = 0f;
            if (d.magnitude > raio) continue;
            v.Curar(v == minhaVida ? cura * curaEmSi : cura);
        }
    }
}
