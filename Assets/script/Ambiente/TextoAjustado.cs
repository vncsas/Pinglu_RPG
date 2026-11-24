using UnityEngine;

// Letreiros em 3D (TextMesh): encolhe o texto, se preciso, para ele caber na largura da placa.
[ExecuteAlways]
[RequireComponent(typeof(TextMesh))]
public class TextoAjustado : MonoBehaviour
{
    public float larguraMax = 2f;
    private int tentativas;

    private void OnEnable() { tentativas = 0; }

    private void LateUpdate()
    {
        if (tentativas > 6) return;
        tentativas++;
        Renderer r = GetComponent<Renderer>();
        if (r == null) return;
        // largura do texto no espaço do objeto pai
        float w = r.bounds.size.magnitude > 0f ? Mathf.Max(r.bounds.size.x, r.bounds.size.z) : 0f;
        if (w < 0.01f) return;
        float f = larguraMax / w;
        if (f < 0.98f) { transform.localScale *= f; tentativas = 0; }
    }
}
