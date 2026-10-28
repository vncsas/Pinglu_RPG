using UnityEngine;

// Câmera de ombro em terceira pessoa (estilo jogos de ação medievais).
// - Mexa o mouse para girar a câmera em volta do personagem.
// - Roda do mouse aproxima/afasta.
// - Esc solta o cursor; clique na tela do jogo para prender de novo.
// - A câmera não atravessa paredes, casas nem o chão.
public class ThirdPersonCameraFollow : MonoBehaviour
{
    [Header("Alvo a seguir")]
    [SerializeField] private Transform target;

    [Header("Posição (câmera de ombro)")]
    [SerializeField] private float distancia = 2.8f;
    [SerializeField] private float distanciaMinima = 1.5f;
    [SerializeField] private float distanciaMaxima = 5f;
    [SerializeField] private float alturaDoPivo = 1.6f;      // altura do ponto que a câmera orbita (perto dos ombros)
    [SerializeField] private float deslocamentoOmbro = 0.5f; // positivo = personagem um pouco à esquerda da tela

    [Header("Mouse")]
    [SerializeField] private float sensibilidadeX = 3f;
    [SerializeField] private float sensibilidadeY = 2f;
    [SerializeField] private bool inverterY = false;
    [SerializeField] private float anguloMinimo = -35f;
    [SerializeField] private float anguloMaximo = 65f;

    [Header("Suavização")]
    [SerializeField] private float suavizacaoSeguir = 0.05f;

    [Header("Colisão")]
    [SerializeField] private float raioColisao = 0.25f;
    [SerializeField] private LayerMask camadasColisao = ~0;

    [Header("Cursor")]
    [SerializeField] private bool prenderCursor = true;

    // tremor da câmera (golpes, dano)
    private static float forcaTremor, fimTremor, duracaoTremor;
    public static void Tremer(float forca, float duracao)
    {
        if (Time.unscaledTime < fimTremor && forca < forcaTremor) return;
        forcaTremor = forca; duracaoTremor = Mathf.Max(0.01f, duracao); fimTremor = Time.unscaledTime + duracao;
    }

    private float yaw;
    private float pitch = 12f;
    private float distanciaAtual;
    private Vector3 pivoAtual;
    private Vector3 velocidadePivo;

    private void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            pivoAtual = target.position + Vector3.up * alturaDoPivo;
        }
        distanciaAtual = distancia;
        if (prenderCursor) PrenderCursor(true);
    }

    private void Update()
    {
        if (!prenderCursor) return;
        if (Input.GetKeyDown(KeyCode.Escape)) PrenderCursor(false);
        else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) PrenderCursor(true);
    }

    private static void PrenderCursor(bool prender)
    {
        Cursor.lockState = prender ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !prender;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // --- Mouse gira a câmera ---
        bool controlando = !prenderCursor || Cursor.lockState == CursorLockMode.Locked;
        if (controlando)
        {
            yaw += Input.GetAxis("Mouse X") * sensibilidadeX;
            pitch -= Input.GetAxis("Mouse Y") * sensibilidadeY * (inverterY ? -1f : 1f);
        }
        pitch = Mathf.Clamp(pitch, anguloMinimo, anguloMaximo);

        distancia = Mathf.Clamp(distancia - Input.GetAxis("Mouse ScrollWheel") * 3f, distanciaMinima, distanciaMaxima);

        // --- Ponto que a câmera orbita (segue o personagem com suavidade) ---
        Vector3 pivoAlvo = target.position + Vector3.up * alturaDoPivo;
        pivoAtual = Vector3.SmoothDamp(pivoAtual, pivoAlvo, ref velocidadePivo, suavizacaoSeguir);

        Quaternion rotacao = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 ombro = rotacao * Vector3.right * deslocamentoOmbro;
        Vector3 desejada = pivoAtual + ombro - rotacao * Vector3.forward * distancia;

        // --- Não deixa a câmera atravessar paredes/terreno ---
        Vector3 dir = desejada - pivoAtual;
        float comprimento = dir.magnitude;
        float livre = comprimento;
        if (comprimento > 0.001f)
        {
            dir /= comprimento;
            RaycastHit[] hits = Physics.SphereCastAll(pivoAtual, raioColisao, dir, comprimento, camadasColisao, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits)
            {
                if (h.transform == target || h.transform.IsChildOf(target)) continue;
                if (h.distance > 0f && h.distance < livre) livre = h.distance;
            }
        }
        // aproxima na hora quando bate em algo, e volta devagar
        distanciaAtual = livre < distanciaAtual ? livre : Mathf.Lerp(distanciaAtual, livre, Time.deltaTime * 6f);

        transform.position = pivoAtual + dir * distanciaAtual;
        transform.rotation = rotacao;

        if (Time.unscaledTime < fimTremor)
        {
            float f = forcaTremor * ((fimTremor - Time.unscaledTime) / duracaoTremor);
            transform.position += Random.insideUnitSphere * f;
        }
    }
}
