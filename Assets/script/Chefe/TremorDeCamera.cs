using UnityEngine;
using UnityEngine.Rendering;

// Terremoto: balança a câmera principal só na hora de desenhar (não atrapalha o script que segue o jogador).
public static class TremorDeCamera
{
    private static float forca, fimEm, duracao;
    private static Vector3 aplicado;
    private static Camera aplicadoEm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Ligar()
    {
        forca = 0f; fimEm = 0f; aplicado = Vector3.zero; aplicadoEm = null;
        RenderPipelineManager.beginCameraRendering -= Antes; RenderPipelineManager.endCameraRendering -= Depois;
        RenderPipelineManager.beginCameraRendering += Antes; RenderPipelineManager.endCameraRendering += Depois;
    }

    // força em metros (0.05 = leve, 0.4 = terremoto forte); o tremor vai sumindo até o fim do tempo
    public static void Tremer(float f, float segundos)
    {
        if (Time.unscaledTime + segundos < fimEm && f <= forca) return;
        forca = f; duracao = Mathf.Max(0.01f, segundos); fimEm = Time.unscaledTime + segundos;
    }

    public static void Parar() { fimEm = 0f; }

    private static void Antes(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != Camera.main || Time.unscaledTime >= fimEm) return;
        float resta = (fimEm - Time.unscaledTime) / duracao;
        float k = forca * Mathf.Clamp01(resta * 1.5f);
        Vector3 o = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * k;
        cam.transform.position += o; aplicado = o; aplicadoEm = cam;
    }

    private static void Depois(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != aplicadoEm) return;
        cam.transform.position -= aplicado; aplicado = Vector3.zero; aplicadoEm = null;
    }
}
