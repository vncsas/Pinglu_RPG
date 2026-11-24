using System.Collections.Generic;
using UnityEngine;

// Skin do Granito, o golem de pedra (tanque), com as ANIMAÇÕES PRÓPRIAS do golem (Idle, Walk, Run, Attack_Pancada, Attack_Martelada,
// Defender_*, Levar_Golpe, Morrer, Provocar). O corpo antigo do Tanque continua escondido, rodando o Animator dele (é ele quem comanda
// o jogo: eventos de dano, escudo, voo). Este script só olha em que estado o corpo antigo está e toca o clipe equivalente do golem.
// Sem corpo antigo (tela de seleção): fica em Idle.
public class SkinProceduralGranito : MonoBehaviour
{
    private Animator mine, antigo;
    private string atual = "", alvoAnterior = "";
    private int golpe;
    private readonly Dictionary<string, float> duracao = new Dictionary<string, float>();
    private bool temSpeed, temVoando;

    // ---- postura de gorila (anda de 4 apoios, com os nós dos dedos): somada por cima do clipe, em graus, em torno do eixo "direita" do personagem
    [Header("Postura de gorila")]
    public float inclinaCintura = 10f, inclinaColuna1 = 12f, inclinaColuna2 = 10f;       // tronco para a frente (soma = inclinação total)
    public float cabecaPescoco = -12f, cabecaCabeca = -14f;                              // cabeça de volta para cima, olhando para a frente
    public float bracosFrente = -6f, balancoBracos = 24f;                               // braço estendido à frente + balanço alternado com as pernas
    public float coxaFrente = -7f, canelaTras = 12f, peAjuste = -5f, baixaQuadril = 0.08f;   // pernas dobradas, quadril mais baixo
    private float quadAlvo, quadAtual, corrida;
    private Transform raiz;
    private readonly Dictionary<string, Transform> ossos = new Dictionary<string, Transform>();
    private Transform Osso(string n) { Transform t; if (!ossos.TryGetValue(n, out t)) { t = Achar(transform, n); ossos[n] = t; } return t; }
    private static Transform Achar(Transform r, string nome) { foreach (Transform t in r.GetComponentsInChildren<Transform>(true)) if (t.name == nome) return t; return null; }
    // ossos que o clipe não anima não "voltam" sozinhos: guardo a pose de antes e desfaço no quadro seguinte, para a postura nunca se acumular
    private readonly Dictionary<Transform, Quaternion> antesRot = new Dictionary<Transform, Quaternion>();
    private Transform quadrilMexido; private Vector3 quadrilAntes;
    public void Desfazer() { foreach (KeyValuePair<Transform, Quaternion> kv in antesRot) if (kv.Key != null) kv.Key.localRotation = kv.Value; antesRot.Clear(); if (quadrilMexido != null) { quadrilMexido.localPosition = quadrilAntes; quadrilMexido = null; } }
    private void Update() { Desfazer(); }
    private void Gira(string osso, float graus, Vector3 eixo) { Transform t = Osso(osso); if (t == null || Mathf.Abs(graus) <= 0.01f) return; if (!antesRot.ContainsKey(t)) antesRot[t] = t.localRotation; t.rotation = Quaternion.AngleAxis(graus, eixo) * t.rotation; }

    /// <summary>Aplica a postura de quadrúpede (q de 0 a 1) em cima da pose atual. Chamado depois do Animator, a cada quadro.</summary>
    public void AplicarPostura(float q, float correndo)
    {
        if (q <= 0.001f) return;
        raiz = transform; Vector3 R = raiz.right; float k = q * (1f + 0.25f * correndo);
        Gira("CC_Base_Waist", inclinaCintura * k, R); Gira("CC_Base_Spine01", inclinaColuna1 * k, R); Gira("CC_Base_Spine02", inclinaColuna2 * k, R);
        Gira("CC_Base_NeckTwist01", cabecaPescoco * k, R); Gira("CC_Base_Head", cabecaCabeca * k, R);
        // balanço dos braços: o braço do lado oposto ao pé que está à frente vai para a frente
        Transform pe = Osso("CC_Base_L_Foot"), pd = Osso("CC_Base_R_Foot");
        float sw = 0f; if (pe != null && pd != null) sw = Mathf.Clamp((raiz.InverseTransformPoint(pe.position).z - raiz.InverseTransformPoint(pd.position).z) / 0.7f, -1f, 1f);
        Gira("CC_Base_L_Upperarm", (bracosFrente - balancoBracos * sw) * k, R); Gira("CC_Base_R_Upperarm", (bracosFrente + balancoBracos * sw) * k, R);
        Gira("CC_Base_L_Thigh", coxaFrente * k, R); Gira("CC_Base_R_Thigh", coxaFrente * k, R);
        Gira("CC_Base_L_Calf", canelaTras * k, R); Gira("CC_Base_R_Calf", canelaTras * k, R);
        Gira("CC_Base_L_Foot", peAjuste * k, R); Gira("CC_Base_R_Foot", peAjuste * k, R);
        Transform quad = Osso("CC_Base_Hip"); if (quad != null) { if (quadrilMexido == null) { quadrilMexido = quad; quadrilAntes = quad.localPosition; } quad.position -= raiz.up * (baixaQuadril * k); }
    }

    private void Start()
    {
        mine = GetComponent<Animator>();
        if (mine == null) return;
        mine.applyRootMotion = false;
        if (mine.runtimeAnimatorController != null) foreach (AnimationClip c in mine.runtimeAnimatorController.animationClips) duracao[c.name] = c.length;
        Animator pai = transform.parent != null ? transform.parent.GetComponentInParent<Animator>() : null;
        if (pai != null && pai != mine) antigo = pai;
        if (antigo != null) foreach (AnimatorControllerParameter p in antigo.parameters) { if (p.name == "Speed") temSpeed = true; if (p.name == "Voando") temVoando = true; }
        Debug.Log("[Granito] animações próprias do golem: " + duracao.Count + " clipes; corpo antigo " + (antigo != null ? "ligado" : "ausente"));
        Tocar("Idle", 0f);
    }

    private void Tocar(string estado, float mistura)
    {
        if (estado == atual || mine == null) return;
        atual = estado;
        if (mistura <= 0f) mine.Play(estado, 0, 0f); else mine.CrossFadeInFixedTime(estado, mistura, 0, 0f);
    }

    private void LateUpdate()
    {
        if (mine == null || mine.runtimeAnimatorController == null) return;
        if (antigo == null) { Tocar("Idle", 0.2f); quadAtual = Mathf.MoveTowards(quadAtual, 0.5f, Time.deltaTime * 3f); AplicarPostura(quadAtual, 0f); return; }
        AnimatorStateInfo e = antigo.GetCurrentAnimatorStateInfo(0);
        string alvo; float mistura = 0.15f; float vel = 1f;
        float v = temSpeed ? antigo.GetFloat("Speed") : 0f;
        if (e.IsName("death")) { alvo = "Morrer"; mistura = 0.1f; }
        else if (e.IsName("hit") || e.IsName("escudo_impacto")) { alvo = "Levar_Golpe"; mistura = 0.05f; }
        else if (e.IsName("escudo_erguer")) alvo = "Defender_Entrar";
        else if (e.IsName("escudo_segurar")) alvo = "Defender_Segurar";
        else if (e.IsName("escudo_baixar")) alvo = "Defender_Sair";
        else if (e.IsName("attack") || e.IsName("attack_2") || e.IsName("attack_3") || e.IsName("attack_heavy"))
        {
            // cada golpe do corpo antigo vira uma pancada ou martelada, alternando; o clipe se ajusta à duração do golpe antigo
            if (alvoAnterior != "atk") { golpe++; }
            alvo = e.IsName("attack_heavy") || golpe % 3 == 0 ? "Attack_Martelada" : "Attack_Pancada"; mistura = 0.06f;
            float d; if (duracao.TryGetValue(alvo, out d) && e.length > 0.05f) vel = Mathf.Clamp(d / e.length, 0.7f, 2.2f);
            alvoAnterior = "atk";
        }
        else if (e.IsName("roll")) alvo = "Run";
        else if (temVoando && antigo.GetBool("Voando")) { alvo = v > 0.3f ? "Run" : "Idle"; }
        else alvo = v < 0.08f ? "Idle" : (v < 0.6f ? "Walk" : "Run");
        if (alvo != "Attack_Martelada" && alvo != "Attack_Pancada") alvoAnterior = "";
        if (alvo == "Walk" || alvo == "Run") vel = Mathf.Clamp(0.6f + v * 0.8f, 0.7f, 1.5f);
        mine.speed = vel;
        Tocar(alvo, mistura);
        bool livre = alvo == "Idle" || alvo == "Walk" || alvo == "Run";
        quadAlvo = !livre ? 0f : (alvo == "Idle" ? 0.5f : 1f); corrida = alvo == "Run" ? 1f : 0f;
        quadAtual = Mathf.MoveTowards(quadAtual, quadAlvo, Time.deltaTime * 3f);
        AplicarPostura(quadAtual, corrida);
    }
}
