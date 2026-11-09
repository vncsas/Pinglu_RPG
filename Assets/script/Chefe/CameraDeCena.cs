using UnityEngine;

// Câmera de cinema para qualquer cena: Ligar() pega a câmera de onde ela está, Definir/Olhar movem, Soltar() devolve suavemente ao jogo.
[DefaultExecutionOrder(1000)]
public class CameraDeCena : MonoBehaviour
{
    private static CameraDeCena inst;
    private bool ativa; private float saida; private Vector3 pos; private Quaternion rot = Quaternion.identity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Zerar() { inst = null; }

    private static CameraDeCena Inst { get { if (inst == null) inst = new GameObject("Câmera de cena").AddComponent<CameraDeCena>(); return inst; } }

    public static bool Ativa { get { return inst != null && inst.ativa; } }
    public static Vector3 Posicao { get { return Inst.pos; } }
    public static Quaternion Rotacao { get { return Inst.rot; } }

    public static void Ligar()
    {
        CameraDeCena i = Inst; Camera c = Camera.main;
        if (c != null && !i.ativa) { i.pos = c.transform.position; i.rot = c.transform.rotation; }
        i.ativa = true; i.saida = 0f;
    }
    public static void Definir(Vector3 p, Quaternion r) { CameraDeCena i = Inst; i.pos = p; i.rot = r; }
    public static void Olhar(Vector3 p, Vector3 alvo) { Definir(p, Quaternion.LookRotation(alvo - p)); }
    public static void Soltar() { if (inst != null && inst.ativa) { inst.ativa = false; inst.saida = 1f; } }

    private void LateUpdate()
    {
        if (!ativa && saida <= 0f) return;
        Camera cam = Camera.main; if (cam == null) return;
        Transform ct = cam.transform;
        if (ativa) ct.SetPositionAndRotation(pos, rot);
        else
        {
            saida = Mathf.Max(0f, saida - Time.unscaledDeltaTime / 1.4f);
            float k = Mathf.SmoothStep(0f, 1f, 1f - saida);
            ct.SetPositionAndRotation(Vector3.Lerp(pos, ct.position, k), Quaternion.Slerp(rot, ct.rotation, k));
        }
    }
}
