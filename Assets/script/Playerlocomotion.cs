using System.Collections;
using UnityEngine;

// Requer um CharacterController no mesmo GameObject.
// Requer um Animator com os parâmetros:
//   Float   "Speed"       -> Blend Tree de locomoção (0 idle, 0.5 walk, 1 run)
//   Trigger "Jump"        -> state de pulo
//   Trigger "Roll"        -> state de esquiva/rolagem
//   Trigger "AttackLight" -> state de ataque leve (Mouse esquerdo)
//   Trigger "AttackHeavy" -> state de ataque pesado (Mouse direito)
//   Bool    "Block"       -> true enquanto o jogador segura Q (bloqueio)
//   Bool    "Grounded"    -> true quando o personagem está tocando o chão
//   Bool    "Rolling"     -> true enquanto a rolagem está em andamento
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

    private CharacterController controller;
    private Animator animator;

    private float currentAnimSpeed;   // valor suavizado enviado pro Animator (0 a 1)
    private float verticalVelocity;

    private bool isRolling;
    private float rollCooldownTimer;
    private Vector3 rollDirection;

    private bool isBlocking;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        // --- Input de movimento ---
        float h = Input.GetAxis("Horizontal"); // A/D ou setas
        float v = Input.GetAxis("Vertical");   // W/S ou setas
        Vector3 inputDir = new Vector3(h, 0f, v);
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // Direção de movimento relativa à câmera principal (se houver)
        Vector3 moveDir;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cam.transform.right; camRight.y = 0f; camRight.Normalize();
            moveDir = camForward * v + camRight * h;
        }
        else
        {
            moveDir = transform.forward * v + transform.right * h;
        }
        if (moveDir.sqrMagnitude > 0.001f) moveDir.Normalize();

        // --- Rolagem (Esquiva) ---
        if (rollCooldownTimer > 0f) rollCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.LeftControl) && !isRolling && rollCooldownTimer <= 0f)
        {
            // Rola na direção que o jogador está segurando (WASD); se não estiver segurando nada, rola pra frente
            rollDirection = moveDir.sqrMagnitude > 0.001f ? moveDir : transform.forward;
            StartCoroutine(DoRoll());
        }

        // --- Pulo ---
        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded && !isRolling)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
        }

        // --- Bloqueio (Q segurando) ---
        isBlocking = Input.GetKey(KeyCode.Q) && !isRolling;
        if (animator != null) animator.SetBool("Block", isBlocking);

        // --- Ataques (Mouse esquerdo = leve, Mouse direito = pesado) ---
        // Não permite atacar durante rolagem ou bloqueio
        if (!isRolling && !isBlocking)
        {
            if (Input.GetMouseButtonDown(0)) // Botão esquerdo
            {
                if (animator != null) animator.SetTrigger("AttackLight");
            }
            else if (Input.GetMouseButtonDown(1)) // Botão direito
            {
                if (animator != null) animator.SetTrigger("AttackHeavy");
            }
        }

        // --- Velocidade enviada pro Animator (Blend Tree) ---
        float targetAnimSpeed = inputMagnitude * (isRunning ? 1f : 0.5f);
        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetAnimSpeed, Time.deltaTime / speedSmoothTime);
        if (animator != null) animator.SetFloat("Speed", currentAnimSpeed);

        // --- Rotação na direção do movimento (só fora da rolagem) ---
        if (!isRolling && moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
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
        float targetMoveSpeed = inputMagnitude * (isRunning ? runSpeed : walkSpeed);
        Vector3 finalMove = isRolling ? rollDirection * rollSpeed : moveDir * targetMoveSpeed;
        finalMove.y = verticalVelocity;

        controller.Move(finalMove * Time.deltaTime);

        // --- Avisa o Animator se o personagem está no chão, DEPOIS do Move() ---
        // (precisa ser depois do Move, senão ele ainda reflete o chão do frame anterior)
        if (animator != null) animator.SetBool("Grounded", controller.isGrounded);
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