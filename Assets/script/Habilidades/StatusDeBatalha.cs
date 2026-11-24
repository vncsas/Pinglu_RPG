using UnityEngine;

// Efeitos temporários em um jogador (hoje: o buff "Impulsionar" do Curandeiro = +20% de dano e de velocidade).
// O "Imbatível" do Tanque fica dentro do Vida (Vida.TornarImortal).
public class StatusDeBatalha : MonoBehaviour
{
    public const float BONUS = 1.2f;

    float impulsoAte;
    GameObject aura;
    PlayerLocomotion loco;

    public bool Impulsionado { get { return Time.time < impulsoAte; } }
    public float ImpulsoRestante { get { return Mathf.Max(0f, impulsoAte - Time.time); } }

    // multiplicador de dano de quem está com o buff (1 se não tiver)
    public static float DanoDe(GameObject g)
    {
        StatusDeBatalha s = g != null ? g.GetComponent<StatusDeBatalha>() : null;
        return s != null && s.Impulsionado ? BONUS : 1f;
    }

    public void Impulsionar(float segundos, GameObject efeito)
    {
        impulsoAte = Time.time + segundos;
        if (aura != null) Destroy(aura);
        if (efeito != null)
        {
            aura = Instantiate(efeito, transform.position, transform.rotation, transform);
            Destroy(aura, segundos);
        }
    }

    void Awake() { loco = GetComponent<PlayerLocomotion>(); }

    void Update()
    {
        if (loco != null) loco.BonusDeVelocidade = Impulsionado ? BONUS : 1f;
    }

    void OnDisable() { if (loco != null) loco.BonusDeVelocidade = 1f; }
}
