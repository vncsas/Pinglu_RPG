using System.Collections.Generic;
using UnityEngine;

// Skin nova do Tanque (modelo sem animações próprias).
// No jogo: o corpo antigo do Tanque continua escondido, rodando o Animator com TODAS as animações
// que já existem (andar, correr, escudo, voo, dano, morte...). Este script copia o movimento
// desse corpo para o esqueleto da skin, osso por osso: gira cada osso pela diferença entre a pose
// atual e a pose parada (idle) do corpo antigo. A capa balança por código.
// Na tela de seleção (sem Animator) a skin só respira e balança a capa.
public class SkinProceduralTanque : MonoBehaviour
{
    private static readonly string[] OSSOS = { "Hips", "Spine", "Chest", "Neck", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" };

    private Animator anim;
    private Transform raizJogador, capa;
    private readonly List<Transform> meus = new List<Transform>(), antigos = new List<Transform>();
    private readonly List<Quaternion> repousoMeu = new List<Quaternion>(), idleAntigo = new List<Quaternion>();
    private Vector3 hipsMeu0, hipsIdleAntigo;
    private bool referenciaPronta, temVelocidade;
    private float fase, quando, suavVel;

    private static Transform Achar(Transform raiz, string nome, Transform ignorar)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != nome) continue;
            if (ignorar != null && t.IsChildOf(ignorar)) continue;
            return t;
        }
        return null;
    }

    private void Start()
    {
        anim = GetComponentInParent<Animator>();
        raizJogador = anim != null ? anim.transform : transform;
        capa = Achar(transform, "Auric_Capa", null);
        fase = Random.value * 10f;
        quando = Time.time;
        if (anim != null)
        {
            foreach (AnimatorControllerParameter p in anim.parameters) if (p.name == "Speed") temVelocidade = true;
            foreach (string o in OSSOS)
            {
                Transform m = Achar(transform, "Auric_" + o, null), a = Achar(raizJogador, o, transform);
                if (m == null || a == null) continue;
                meus.Add(m); antigos.Add(a);
                repousoMeu.Add(Quaternion.Inverse(raizJogador.rotation) * m.rotation);
            }
            if (meus.Count > 0 && meus[0].name == "Auric_Hips") hipsMeu0 = meus[0].localPosition;
            Debug.Log("[TanqueNovo] ossos ligados ao corpo antigo: " + meus.Count + " de " + OSSOS.Length);
        }
    }

    // a pose parada do corpo antigo (depois do primeiro quadro do Animator) é a referência: braços para baixo, como na skin
    private bool CapturarReferencia()
    {
        AnimatorStateInfo e = anim.GetCurrentAnimatorStateInfo(0);
        float v = temVelocidade ? anim.GetFloat("Speed") : 0f;
        bool parado = !anim.IsInTransition(0) && v < 0.05f && !anim.GetBool("Voando") && e.normalizedTime >= 0f;
        if (!parado && Time.time - quando < 2f) return false;
        idleAntigo.Clear();
        for (int i = 0; i < antigos.Count; i++) idleAntigo.Add(Quaternion.Inverse(raizJogador.rotation) * antigos[i].rotation);
        if (antigos.Count > 0 && antigos[0].name == "Hips") hipsIdleAntigo = raizJogador.InverseTransformPoint(antigos[0].position);
        return true;
    }

    private void LateUpdate()
    {
        float t = Time.time + fase, dt = Time.deltaTime;
        float vel = anim != null && temVelocidade ? anim.GetFloat("Speed") : 0f;
        suavVel = Mathf.Lerp(suavVel, vel, dt * 8f);

        if (anim != null && meus.Count > 0)
        {
            if (!referenciaPronta) { if (CapturarReferencia()) referenciaPronta = true; }
            if (referenciaPronta)
            {
                Quaternion rr = raizJogador.rotation;
                for (int i = 0; i < meus.Count; i++)
                {
                    Quaternion agora = Quaternion.Inverse(rr) * antigos[i].rotation;
                    Quaternion delta = agora * Quaternion.Inverse(idleAntigo[i]);
                    meus[i].rotation = rr * delta * repousoMeu[i];
                }
                if (meus[0].name == "Auric_Hips")
                {
                    Vector3 agoraPos = raizJogador.InverseTransformPoint(antigos[0].position);
                    meus[0].localPosition = hipsMeu0 + (agoraPos - hipsIdleAntigo);
                }
            }
        }
        else
        {
            // seleção de personagem: só respira
            Transform tronco = Achar(transform, "Auric_Chest", null);
            if (tronco != null) tronco.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.5f) * 1.2f, 0f, 0f);
        }
        // capa: balança parada e voa para trás correndo
        if (capa != null)
            capa.localRotation = Quaternion.Euler(6f + 30f * suavVel + Mathf.Sin(t * 3f) * (3f + 6f * suavVel), Mathf.Sin(t * 1.7f) * 4f, 0f);
    }
}
