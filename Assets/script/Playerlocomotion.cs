using System.Collections;
using UnityEngine;

// Requer um CharacterController no mesmo GameObject.
// Requer um Animator com os parâmetros:
//   Float   "Speed"       -> Blend Tree de locomoção (0 idle, 0.5 walk, 1 run)
//   Trigger "Jump"        -> state de pulo
//   Trigger "Roll"        -> state de esquiva/rolagem
//   Trigger "AttackLight" -> state de ataque leve (usado só se não houver combo)
//   Trigger "AttackHeavy" -> state de ataque pesado (usado só se não houver combo)
//   Bool    "Block"       -> true enquanto o jogador segura o botão direito (ou Q) para defender
//   Bool    "Grounded"    -> true quando o personagem está tocando o chão
//   Bool    "Rolling"     -> true enquanto a rolagem está em andamento
//
// COMBO: se o Animator tiver os states "attack", "attack_2" e "attack_3"
// (o menu Pinglu > 5. Animações do cavaleiro cria eles), cada clique do
// mouse esquerdo durante um golpe encadeia o próximo golpe do combo.
[RequireComponent(typeof(CharacterController))]
public class PlayerLocomotion : MonoBehaviour
{
    [Header("Velocidades")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Suavização da animação")]
    [SerializeField] private float speedSmoothTime = 0.1f;

    [Header("Gravidade")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Pulo")]
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Rolagem / Esquiva")]
    [SerializeField] private float rollSpeed = 8f;
    [SerializeField] private float rollDuration = 0.5f;
    [SerializeField] private float rollCooldown = 0.8f;

    [Header("Combo de ataques")]
    [SerializeField] private string[] estadosCombo = { "attack", "attack_2", "attack_3" };
    [SerializeField] private string estadoAtaquePesado = "attack_heavy";
    [Tooltip("A partir de quanto da animação (0 a 1) o próximo golpe pode sair")]
    [SerializeField, Range(0.2f, 0.95f)] private float pontoEncadear = 0.55f;
    [SerializeField] private float transicaoEntreGolpes = 0.12f;
    [Tooltip("Velocidade de movimento durante o golpe (fração da velocidade de andar)")]
    [SerializeField, Range(0f, 1f)] private float movimentoDuranteGolpe = 0.2f;

    private CharacterController controller;
    private Animator animator;

    private float currentAnimSpeed;   // valor suavizado enviado pro Animator (0 a 1)
    private float verticalVelocity;

    private bool isRolling;
    private float rollCooldownTimer;
    private Vector3 rollDirection;

    private bool isBlocking;

    // estado do combo
    private int golpesDisponiveis;     // quantos states de combo existem no Animator
    private bool temAtaquePesado;
    private bool atacando;
    private int golpeAtual = -1;
    private string estadoAtual;
    private bool proximoNaFila;
    private float tempoNoGolpe;

    // ---- usado pelo sistema de combate ----
    public bool Atacando => atacando;
    public bool Bloqueando => isBlocking;
    public bool Rolando => isRolling;
    public int GolpeAtual => golpeAtual;            // 0, 1, 2 = combo ; -1 = pesado
    public bool Controlavel { get; set; } = true;   // falso quando morto
    public event System.Action<int> AoIniciarGolpe;
    private Vector3 empurrao;                       // empurrão ao levar golpe
    private float atordoadoAte;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(); // o modelo pode estar num filho virado

        golpesDisponiveis = 0;
        if (animator != null)
        {
            foreach (string e in estadosCombo)
            {
                if (!TemEstado(e)) break;
                golpesDisponiveis++;
            }
            temAtaquePesado = TemEstado(estadoAtaquePesado);
        }
    }

    private bool TemEstado(string nome)
    {
        return animator.HasState(0, Animator.StringToHash(nome)) || animator.HasState(0, Animator.StringToHash("Base Layer." + nome));
    }

    private void Update()
    {
        if (!Controlavel || Time.time < atordoadoAte)
        {
            // morto ou atordoado: só gravidade e empurrão
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -0.5f; else verticalVelocity += gravity * Time.deltaTime;
            empurrao = Vector3.Lerp(empurrao, Vector3.zero, Time.deltaTime * 8f);
            controller.Move((empurrao + Vector3.up * verticalVelocity) * Time.deltaTime);
            if (animator != null) { animator.SetFloat("Speed", 0f); animator.SetBool("Block", false); }
            return;
        }

        // --- Input de movimento ---
        float h = Input.GetAxis("Horizontal"); // A/D ou setas
        float v = Input.GetAxis("Vertical");   // W/S ou setas
        Vector3 inputDir = new Vector3(h, 0f, v);
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // Direção de movimento relativa à câmera principal (se houver)
        Vector3 moveDir;
        Vector3 frenteCamera = transform.forward;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cam.transform.right; camRight.y = 0f; camRight.Normalize();
            moveDir = camForward * v + camRight * h;
            frenteCamera = camForward;
        }
        else
        {
            moveDir = transform.forward * v + transform.right * h;
        }
        if (moveDir.sqrMagnitude > 0.001f) moveDir.Normalize();

        // --- Rolagem (Esquiva) --- (cancela o combo)
        if (rollCooldownTimer > 0f) rollCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.LeftControl) && !isRolling && rollCooldownTimer <= 0f)
        {
            // Rola na direção que o jogador está segurando (WASD); se não estiver segurando nada, rola pra frente
            rollDirection = moveDir.sqrMagnitude > 0.001f ? moveDir : transform.forward;
            TerminarCombo();
            StartCoroutine(DoRoll());
        }

        // --- Pulo ---
        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded && !isRolling && !atacando)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
        }

        // --- Defesa com escudo (botão direito ou Q, segurando) ---
        bool querDefender = Input.GetMouseButton(1) || Input.GetKey(KeyCode.Q);
        if (querDefender && atacando && !isRolling)
        {
            // cancela o golpe para defender na hora
            TerminarCombo();
            if (animator != null && TemEstado("Blend Tree")) animator.CrossFadeInFixedTime("Blend Tree", 0.08f, 0, 0f);
        }
        isBlocking = querDefender && !isRolling && !atacando;
        if (animator != null) animator.SetBool("Block", isBlocking);

        // --- Ataques ---
        AtualizarAtaques(moveDir, frenteCamera);

        // --- Velocidade enviada pro Animator (Blend Tree) ---
        bool correndo = isRunning && !atacando && !isBlocking;
        float targetAnimSpeed = atacando ? 0f : inputMagnitude * (correndo ? 1f : 0.5f);
        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetAnimSpeed, Time.deltaTime / speedSmoothTime);
        if (animator != null) animator.SetFloat("Speed", currentAnimSpeed);

        // --- Rotação ---
        if (!isRolling && !atacando)
        {
            Vector3 olhar = isBlocking ? frenteCamera : moveDir;   // bloqueando, fica de frente para onde a câmera olha
            if (olhar.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(olhar, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        // --- Gravidade ---
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -0.5f; // pequena força pra manter no chão
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // --- Movimento final ---
        float targetMoveSpeed = inputMagnitude * (correndo ? runSpeed : walkSpeed);
        if (atacando) targetMoveSpeed = inputMagnitude * walkSpeed * movimentoDuranteGolpe;
        else if (isBlocking) targetMoveSpeed = inputMagnitude * walkSpeed * 0.5f;
        Vector3 finalMove = isRolling ? rollDirection * rollSpeed : moveDir * targetMoveSpeed;
        empurrao = Vector3.Lerp(empurrao, Vector3.zero, Time.deltaTime * 8f);
        finalMove += empurrao;
        finalMove.y = verticalVelocity;

        controller.Move(finalMove * Time.deltaTime);

        // --- Avisa o Animator se o personagem está no chão, DEPOIS do Move() ---
        // (precisa ser depois do Move, senão ele ainda reflete o chão do frame anterior)
        if (animator != null) animator.SetBool("Grounded", controller.isGrounded);
    }

    // ------------------------------------------------------------------
    //  Combo de ataques
    // ------------------------------------------------------------------
    private void AtualizarAtaques(Vector3 moveDir, Vector3 frenteCamera)
    {
        if (animator == null) return;

        // Sem os states de combo no Animator: comportamento antigo (um golpe por clique)
        if (golpesDisponiveis == 0)
        {
            if (!isRolling && !isBlocking && Input.GetMouseButtonDown(0)) animator.SetTrigger("AttackLight");
            return;
        }

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        bool emTransicao = animator.IsInTransition(0);

        // o golpe acabou? (o Animator já voltou para a locomoção)
        if (atacando)
        {
            tempoNoGolpe += Time.deltaTime;
            bool noGolpe = info.IsName(estadoAtual) || (emTransicao && animator.GetNextAnimatorStateInfo(0).IsName(estadoAtual));
            if (!noGolpe && tempoNoGolpe > 0.25f) TerminarCombo();
        }

        bool podeAtacar = !isRolling && !isBlocking && controller.isGrounded;
        Vector3 direcaoGolpe = moveDir.sqrMagnitude > 0.001f ? moveDir : frenteCamera;

        if (podeAtacar && Input.GetMouseButtonDown(0))
        {
            if (!atacando) Golpe(0, direcaoGolpe);
            else if (golpeAtual >= 0 && golpeAtual < golpesDisponiveis - 1) proximoNaFila = true;
        }

        // encadeia o próximo golpe quando o atual passa do ponto certo
        if (atacando && proximoNaFila && !emTransicao && info.IsName(estadoAtual) && info.normalizedTime >= pontoEncadear)
            Golpe(golpeAtual + 1, direcaoGolpe);
    }

    // indice -1 = ataque pesado
    private void Golpe(int indice, Vector3 direcao)
    {
        golpeAtual = indice;
        estadoAtual = indice < 0 ? estadoAtaquePesado : estadosCombo[indice];
        proximoNaFila = false;
        atacando = true;
        tempoNoGolpe = 0f;
        if (direcao.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direcao, Vector3.up);
        animator.CrossFadeInFixedTime(estadoAtual, transicaoEntreGolpes, 0, 0f);
        AoIniciarGolpe?.Invoke(indice);
    }

    // chamado pelo combate quando o jogador leva um golpe
    public void ReagirAoDano(Vector3 origem, bool bloqueado)
    {
        Vector3 dir = transform.position - origem; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) empurrao = dir.normalized * (bloqueado ? 2.5f : 4f);
        if (bloqueado)
        {
            EscudoAnimacao escudo = GetComponent<EscudoAnimacao>();
            if (escudo != null) escudo.Impacto();
            return;
        }
        if (animator == null) return;
        TerminarCombo();
        if (TemEstado("hit")) { animator.CrossFadeInFixedTime("hit", 0.08f, 0, 0f); atordoadoAte = Time.time + 0.35f; }
    }

    public void Morrer()
    {
        Controlavel = false;
        TerminarCombo();
        if (animator != null && TemEstado("death")) animator.CrossFadeInFixedTime("death", 0.15f, 0, 0f);
    }

    private void TerminarCombo()
    {
        atacando = false;
        golpeAtual = -1;
        proximoNaFila = false;
    }

    private IEnumerator DoRoll()
    {
        isRolling = true;
        rollCooldownTimer = rollCooldown;

        // Avisa o Animator que a rolagem começou (usado como condição de saída do state Roll)
        if (animator != null) animator.SetBool("Rolling", true);

        // Gira o personagem instantaneamente pra direção da rolagem, pra animação ficar coerente
        transform.rotation = Quaternion.LookRotation(rollDirection, Vector3.up);

        if (animator != null) animator.SetTrigger("Roll");

        yield return new WaitForSeconds(rollDuration);

        isRolling = false;

        // Avisa o Animator que a rolagem terminou, liberando a transição de volta pro Blend Tree
        if (animator != null) animator.SetBool("Rolling", false);
    }
}
