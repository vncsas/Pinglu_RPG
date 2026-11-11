using UnityEngine;
using UnityEngine.InputSystem;

// Andar pelo mapa com o Cavaleiro Sombrio: WASD move, Shift corre, mouse gira a camera (clique na tela para capturar o mouse, Esc solta),
// roda do mouse aproxima/afasta, F saca/guarda a espada, botao esquerdo ataca (combo A-B-C), botao direito defende.
// Usa o Input System novo (o projeto esta configurado so com ele).
[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class JogadorMapa : MonoBehaviour
{
    public float andar = 2.4f, correr = 5.4f, gravidade = -22f, giro = 12f;
    public float distanciaCamera = 4.2f, sensibilidade = 0.12f, alturaAlvo = 1.55f;
    public float nivelAgua = 8.8f;
    public Transform[] atalhos; public string[] nomesAtalhos;

    CharacterController cc; Animator anim; GuardaEspada espada; Camera cam;
    float yaw = 20f, pitch = 14f, velY; string atual = ""; float travaAte; int combo; float janelaCombo; bool defendendo;

    void Start()
    {
        cc = GetComponent<CharacterController>(); anim = GetComponent<Animator>(); espada = GetComponent<GuardaEspada>(); cam = Camera.main;
        if (cam) { var d = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); d.renderPostProcessing = true; }
        if (Application.isPlaying) { Cursor.lockState = CursorLockMode.None; }
    }

    void Tocar(string estado, float mistura = 0.15f)
    {
        if (estado == atual || !anim.HasState(0, Animator.StringToHash(estado))) return;
        anim.CrossFadeInFixedTime(estado, mistura); atual = estado;
    }

    void Update()
    {
        var kb = Keyboard.current; var ms = Mouse.current; if (kb == null || ms == null) return;
        if (ms.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; return; }
        if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
        bool capturado = Cursor.lockState == CursorLockMode.Locked;
        if (capturado)
        {
            var d = ms.delta.ReadValue(); yaw += d.x * sensibilidade; pitch = Mathf.Clamp(pitch - d.y * sensibilidade, -15f, 70f);
            distanciaCamera = Mathf.Clamp(distanciaCamera - ms.scroll.ReadValue().y * 0.004f, 2.2f, 9f);
        }
        bool sacada = espada != null && !espada.guardada;
        if (kb.fKey.wasPressedThisFrame && Time.time > travaAte) { Tocar(sacada ? "Guardar_Espada" : "Sacar_Espada", 0.1f); travaAte = Time.time + 1.1f; combo = 0; }
        defendendo = capturado && sacada && ms.rightButton.isPressed && Time.time > travaAte;
        if (capturado && sacada && ms.leftButton.wasPressedThisFrame && Time.time > travaAte)
        {
            if (Time.time > janelaCombo) combo = 0;
            string[] seq = { "Ataque_A", "Ataque_B", "Ataque_C" }; var nome = seq[combo % 3]; combo++;
            atual = ""; Tocar(nome, 0.08f); travaAte = Time.time + 0.75f; janelaCombo = Time.time + 1.4f;
        }

        // atalhos 1-6: teleporta para cada lugar do mapa
        if (atalhos != null)
        {
            var teclas = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key };
            for (int i = 0; i < teclas.Length && i < atalhos.Length; i++)
                if (teclas[i].wasPressedThisFrame) Teleportar(atalhos[i]);
        }
        // movimento relativo a camera
        Vector2 e = Vector2.zero;
        if (kb.wKey.isPressed) e.y += 1; if (kb.sKey.isPressed) e.y -= 1; if (kb.dKey.isPressed) e.x += 1; if (kb.aKey.isPressed) e.x -= 1;
        e = Vector2.ClampMagnitude(e, 1f);
        var f = Quaternion.Euler(0, yaw, 0); var dir = f * new Vector3(e.x, 0, e.y);
        bool travado = Time.time < travaAte || defendendo;
        bool corre = kb.leftShiftKey.isPressed && sacada == false || (kb.leftShiftKey.isPressed && sacada);
        float v = travado ? 0f : (corre ? correr : andar) * e.magnitude;
        bool naAgua = transform.position.y < nivelAgua - 0.25f; if (naAgua) v *= 0.55f;
        if (dir.sqrMagnitude > 0.01f && !travado) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-giro * Time.deltaTime));
        if (cc.isGrounded && velY < 0) velY = -2f; velY += gravidade * Time.deltaTime;
        cc.Move((dir * v + Vector3.up * velY) * Time.deltaTime);

        // animacao
        if (Time.time >= travaAte)
        {
            if (defendendo) Tocar("Defender", 0.12f);
            else if (e.sqrMagnitude < 0.01f) Tocar(sacada ? "Parado" : "Parado_Sem_Espada");
            else if (corre) Tocar(sacada ? "Correr_Com_Espada" : "Correr");
            else Tocar(sacada ? "Andar" : "Andar_Sem_Espada");
        }
    }

    void Teleportar(Transform destino)
    {
        cc.enabled = false; transform.position = destino.position; transform.rotation = Quaternion.Euler(0, destino.eulerAngles.y, 0); cc.enabled = true;
        yaw = destino.eulerAngles.y; velY = 0; travaAte = 0; atual = "";
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main; if (cam == null) return;
        var alvo = transform.position + Vector3.up * alturaAlvo;
        var rot = Quaternion.Euler(pitch, yaw, 0); var pos = alvo - rot * Vector3.forward * distanciaCamera;
        // nao entra no chao nem em paredes
        if (Physics.Linecast(alvo, pos, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.gameObject != gameObject) pos = hit.point + hit.normal * 0.2f;
        var t = Terrain.activeTerrain; if (t) { float h = t.SampleHeight(pos) + t.transform.position.y + 0.5f; if (pos.y < h) pos.y = h; }
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(alvo - pos));
    }

    void OnGUI()
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
        string atal = ""; if (nomesAtalhos != null) for (int i = 0; i < nomesAtalhos.Length; i++) atal += $"<color=#f2d9a0>{i + 1}</color> {nomesAtalhos[i]}   ";
        GUI.Label(new Rect(14, 10, 900, 110), "<color=#f2d9a0><b>Explorar</b></color>   WASD andar · Shift correr · mouse camera (clique p/ capturar, Esc solta)\nF sacar/guardar espada · botao esquerdo ataca (combo) · botao direito defende · roda do mouse zoom\n<b>Atalhos:</b> " + atal, st);
        if (Cursor.lockState != CursorLockMode.Locked) GUI.Label(new Rect(Screen.width / 2 - 140, Screen.height - 60, 300, 30), "<color=#ffffff>Clique para capturar o mouse</color>", st);
    }
}
