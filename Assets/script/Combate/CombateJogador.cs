using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Combate do jogador: acerta os inimigos durante os golpes, bloqueia com o
// escudo (Q), não leva dano rolando (Ctrl) e dá aquela "travadinha" no acerto.
[RequireComponent(typeof(PlayerLocomotion))]
[RequireComponent(typeof(Vida))]
public class CombateJogador : MonoBehaviour
{
    [Header("Dano")]
    [SerializeField] private float[] danoCombo = { 20f, 25f, 40f };
    [SerializeField] private float danoPesado = 45f;

    [Header("Alcance do golpe")]
    [SerializeField] private float alcance = 1.6f;   // distância à frente do personagem
    [SerializeField] private float raio = 1.3f;      // tamanho da área do golpe
    [Tooltip("Parte da animação em que a espada machuca (0 a 1)")]
    [SerializeField, Range(0f, 1f)] private float inicioDoDano = 0.25f;
    [SerializeField, Range(0f, 1f)] private float fimDoDano = 0.6f;

    [Header("Defesa com escudo (botão direito)")]
    [Tooltip("Quanto do dano o escudo segura (1 = não toma dano nenhum)")]
    [SerializeField, Range(0f, 1f)] private float bloqueio = 1f;
    [Tooltip("Se marcado, o escudo só protege de golpes vindos da frente")]
    [SerializeField] private bool defendeSoDeFrente = false;
    [SerializeField] private float ganhoAoDefender = 6f;

    [Header("Especial (para os ataques especiais que virão)")]
    [SerializeField] private float especialMaximo = 100f;
    [SerializeField] private float ganhoPorAcerto = 8f;
    [SerializeField] private float ganhoAoLevarDano = 4f;

    public float Especial { get; private set; }

    // ponto do acerto (usado pelos efeitos visuais da espada)
    public event System.Action<Vector3> AoAcertar;
    public float EspecialFracao => especialMaximo > 0f ? Especial / especialMaximo : 0f;
    public bool EspecialCheio => Especial >= especialMaximo - 0.01f;

    // gasta a energia do especial; devolve falso se não tiver o suficiente
    public bool GastarEspecial(float quanto)
    {
        if (Especial < quanto) return false;
        Especial -= quanto;
        return true;
    }

    private void GanharEspecial(float quanto) { Especial = Mathf.Min(especialMaximo, Especial + quanto); }

    [Header("Sensação de impacto")]
    [SerializeField] private float pausaNoAcerto = 0.06f;

    private PlayerLocomotion loco;
    private Vida vida;
    private Animator anim;
    private Coroutine rotinaGolpe;
    private bool ultimoFoiBloqueado;

    private void Awake()
    {
        loco = GetComponent<PlayerLocomotion>();
        vida = GetComponent<Vida>();
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        vida.FiltroDeDano = FiltrarDano;
        vida.AoLevarDano += AoLevarDano;
        vida.AoMorrer += () => loco.Morrer();
        loco.AoIniciarGolpe += AoIniciarGolpe;
    }

    private float FiltrarDano(float dano, Vector3 origem)
    {
        ultimoFoiBloqueado = false;
        if (loco.Rolando) return 0f; // rolando não toma dano
        Vector3 paraOrigem = origem - transform.position; paraOrigem.y = 0f;
        bool deFrente = paraOrigem.sqrMagnitude < 0.0001f || Vector3.Dot(transform.forward, paraOrigem.normalized) > 0.2f;
        if (loco.Bloqueando && (deFrente || !defendeSoDeFrente))
        {
            ultimoFoiBloqueado = true;
            // reação de "golpe no escudo": empurrãozinho, tremor leve e energia de especial
            loco.ReagirAoDano(origem, true);
            ThirdPersonCameraFollow.Tremer(0.05f, 0.15f);
            GanharEspecial(ganhoAoDefender);
            return dano * (1f - bloqueio);
        }
        return dano;
    }

    private void AoLevarDano(float dano, Vector3 origem)
    {
        if (ultimoFoiBloqueado) return; // a reação de defesa já foi feita
        loco.ReagirAoDano(origem, false);
        GanharEspecial(ganhoAoLevarDano);
        ThirdPersonCameraFollow.Tremer(0.15f, 0.2f);
    }

    private void AoIniciarGolpe(int indice)
    {
        if (rotinaGolpe != null) StopCoroutine(rotinaGolpe);
        rotinaGolpe = StartCoroutine(JanelaDeDano(indice));
    }

    private IEnumerator JanelaDeDano(int indice)
    {
        float dano = indice < 0 ? danoPesado : danoCombo[Mathf.Clamp(indice, 0, danoCombo.Length - 1)];
        HashSet<Vida> atingidos = new HashSet<Vida>();
        float inicio = Time.time;
        yield return null;

        while (loco.Atacando && loco.GolpeAtual == indice && Time.time - inicio < 3f)
        {
            float n = TempoNormalizadoDoGolpe();
            if (n >= inicioDoDano && n <= fimDoDano) Acertar(dano, atingidos);
            if (n > fimDoDano) break;
            yield return null;
        }
    }

    // em que ponto (0 a 1) está a animação do golpe atual
    private float TempoNormalizadoDoGolpe()
    {
        if (anim == null) return 0.5f;
        AnimatorStateInfo atual = anim.GetCurrentAnimatorStateInfo(0);
        if (anim.IsInTransition(0))
        {
            AnimatorStateInfo prox = anim.GetNextAnimatorStateInfo(0);
            if (!prox.loop) return prox.normalizedTime;
        }
        return atual.loop ? 0f : atual.normalizedTime;
    }

    private void Acertar(float dano, HashSet<Vida> atingidos)
    {
        Vector3 centro = transform.position + transform.forward * alcance * 0.75f + Vector3.up * 1.0f;
        bool acertou = false;
        foreach (Collider c in Physics.OverlapSphere(centro, raio, ~0, QueryTriggerInteraction.Ignore))
        {
            Vida alvo = c.GetComponentInParent<Vida>();
            if (alvo == null || alvo == vida || atingidos.Contains(alvo) || alvo.Morto) continue;
            atingidos.Add(alvo);
            if (alvo.ReceberDano(dano, transform.position))
            {
                acertou = true;
                loco.MarcarCombate();
                GanharEspecial(ganhoPorAcerto);
                AoAcertar?.Invoke(Vector3.Lerp(alvo.transform.position + Vector3.up * 1.2f, centro, 0.35f));
            }
        }
        if (acertou)
        {
            StartCoroutine(PausaDeImpacto());
            ThirdPersonCameraFollow.Tremer(0.08f, 0.12f);
        }
    }

    private IEnumerator PausaDeImpacto()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(pausaNoAcerto);
        Time.timeScale = 1f;
    }

    private void OnDisable() { Time.timeScale = 1f; }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + transform.forward * alcance * 0.75f + Vector3.up, raio);
    }
}
