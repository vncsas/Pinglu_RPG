using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Mantém o jogo fluido em computadores fracos (notebook com vídeo integrado):
//  - limita a 60 quadros e liga o vsync (sem isso a placa de vídeo trabalha 100% o tempo todo e o notebook esquenta e trava)
//  - corta o que está longe da câmera (distância máxima de desenho, coberta pela neblina)
//  - ajusta sozinho a resolução da imagem: se o FPS cai, a imagem fica um pouco mais "macia" (mais leve); se sobra, volta
//  Teclas: F9 liga/desliga o MODO LEVE (resolução 60%, sombras curtas).
// Nasce sozinho ao abrir a cena; não precisa colocar em objeto nenhum.
public class DesempenhoAdaptativo : MonoBehaviour
{
    [SerializeField] private int fpsAlvo = 60;
    [SerializeField] private float escalaMinima = 0.55f;
    [SerializeField] private float escalaMaxima = 1f;
    [SerializeField] private float distanciaMaximaDaCamera = 320f;
    [SerializeField] private KeyCode teclaModoLeve = KeyCode.F9;

    private UniversalRenderPipelineAsset urp;
    private float escalaOriginal, sombraOriginal;
    private float escala = 1f;
    private bool modoLeve;
    private float acumulado, proximaAvaliacao;
    private int quadros;
    private int avaliacoesRuins;

    public static DesempenhoAdaptativo Instancia { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Criar()
    {
        if (Instancia != null) return;
        GameObject g = new GameObject("Desempenho Adaptativo");
        DontDestroyOnLoad(g);
        g.AddComponent<DesempenhoAdaptativo>();
    }

    private void Awake()
    {
        if (Instancia != null) { Destroy(gameObject); return; }
        Instancia = this;
        Application.targetFrameRate = fpsAlvo;
        QualitySettings.vSyncCount = 1;
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null) { escalaOriginal = urp.renderScale; sombraOriginal = urp.shadowDistance; escala = Mathf.Min(escalaMaxima, escalaOriginal); }
        proximaAvaliacao = Time.unscaledTime + 6f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaModoLeve)) { modoLeve = !modoLeve; Aplicar(); }

        // distância máxima de desenho em todas as câmeras do jogo (a neblina esconde o corte)
        Camera c = Camera.main;
        if (c != null && c.farClipPlane > distanciaMaximaDaCamera) c.farClipPlane = distanciaMaximaDaCamera;

        acumulado += Time.unscaledDeltaTime; quadros++;
        if (Time.unscaledTime < proximaAvaliacao || modoLeve || urp == null) return;
        float fps = quadros / Mathf.Max(0.01f, acumulado);
        acumulado = 0f; quadros = 0;
        proximaAvaliacao = Time.unscaledTime + 3f;

        if (fps < fpsAlvo * 0.6f)
        {
            avaliacoesRuins++;
            escala = Mathf.Max(escalaMinima, escala - 0.1f);
            if (avaliacoesRuins >= 3 && escala <= escalaMinima + 0.01f) urp.shadowDistance = Mathf.Min(urp.shadowDistance, 20f); // último recurso: sombras bem curtas
        }
        else if (fps > fpsAlvo * 0.92f && escala < escalaMaxima)
        {
            avaliacoesRuins = 0;
            escala = Mathf.Min(escalaMaxima, escala + 0.05f);
        }
        Aplicar();
    }

    private void Aplicar()
    {
        if (urp == null) return;
        urp.renderScale = modoLeve ? 0.6f : escala;
        if (modoLeve) urp.shadowDistance = 15f;
        else if (urp.shadowDistance < sombraOriginal && avaliacoesRuins < 3) urp.shadowDistance = sombraOriginal;
    }

    // devolve o asset do jeito que estava (no Editor ele é compartilhado e ficaria alterado)
    private void OnDestroy()
    {
        if (Instancia != this) return;
        if (urp != null) { urp.renderScale = escalaOriginal; urp.shadowDistance = sombraOriginal; }
        Instancia = null;
    }

    private void OnGUI()
    {
        if (!modoLeve) return;
        GUI.Label(new Rect(10, Screen.height - 28, 300, 24), "Modo leve (F9)");
    }
}
