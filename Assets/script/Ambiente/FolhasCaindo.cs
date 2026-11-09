using UnityEngine;

// Folhas caindo das árvores em volta do jogador (campos e matas). Um único sistema de partículas que acompanha a câmera:
// só existe perto de quem joga, então não pesa. Dentro da Floresta Sombria (contorno vermelho) as folhas param.
[RequireComponent(typeof(ParticleSystem))]
public class FolhasCaindo : MonoBehaviour
{
    public float alturaAcima = 13f;
    public float taxa = 22f;

    private ParticleSystem ps;
    private LimiteDaFloresta limite;
    private Transform alvo;
    private float proximaChecagem;

    private void Start()
    {
        ps = GetComponent<ParticleSystem>();
        limite = FindFirstObjectByType<LimiteDaFloresta>();
    }

    private void LateUpdate()
    {
        if (alvo == null)
        {
            Camera c = Camera.main;
            if (c != null) alvo = c.transform;
            if (alvo == null) return;
        }
        Vector3 p = alvo.position;
        transform.position = new Vector3(p.x, p.y + alturaAcima, p.z);
        if (Time.time >= proximaChecagem && ps != null)
        {
            proximaChecagem = Time.time + 0.7f;
            bool sombria = limite != null && limite.Valido && limite.Contem(p.x, p.z);
            var em = ps.emission; em.rateOverTime = sombria ? 0f : taxa;
        }
    }
}
