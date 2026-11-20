using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

// Toca uma cena feita na Timeline do Unity (câmeras Cinemachine + animações) no meio do jogo.
// A cena é um prefab em Assets/Resources/Cenas/ (criado pelos menus "Pinglu > 85..."). Você edita o prefab com a janela Timeline.
// O código do jogo manda o tempo (Definir) e a Timeline só cuida das câmeras, então os efeitos do código e a câmera ficam sincronizados.
// A origem do prefab é o ponto da cena (ex.: o corpo do dragão); o eixo Z do prefab aponta da câmera para a cena.
[DefaultExecutionOrder(1100)]
public class CenaTimeline : MonoBehaviour
{
    public PlayableDirector diretor;

    private CinemachineBrain brain; private Camera cam; private float fovOriginal; private float tempo; private bool tocando;

    public float Duracao { get { return diretor != null ? (float)diretor.duration : 0f; } }

    /// <summary>Cria a cena a partir do prefab "Resources/Cenas/nome". Devolve null se o prefab ainda não existe (rode o menu Pinglu > 85).</summary>
    public static CenaTimeline Criar(string nome, Vector3 posicao, Quaternion rotacao, float escala)
    {
        GameObject pre = Resources.Load<GameObject>("Cenas/" + nome);
        if (pre == null) { Debug.LogWarning("[Cena] Não achei Resources/Cenas/" + nome + " (rode o menu Pinglu > 85). Usando a câmera em código."); return null; }
        Camera c = Camera.main; if (c == null) return null;
        GameObject g = Instantiate(pre, posicao, rotacao); g.transform.localScale = Vector3.one * escala;
        CenaTimeline ct = g.GetComponent<CenaTimeline>();
        if (ct == null || ct.diretor == null || ct.diretor.playableAsset == null) { Destroy(g); return null; }
        ct.Iniciar(c); Debug.Log("[Cena] Tocando a Timeline \"" + nome + "\" (" + ct.Duracao.ToString("0.0") + " s)."); return ct;
    }

    void Iniciar(Camera c)
    {
        cam = c; fovOriginal = c.fieldOfView;
        brain = c.GetComponent<CinemachineBrain>(); bool nova = brain == null;
        if (nova) brain = c.gameObject.AddComponent<CinemachineBrain>();
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
        TimelineAsset tl = diretor.playableAsset as TimelineAsset;
        if (tl != null) foreach (TrackAsset tr in tl.GetOutputTracks()) if (tr is CinemachineTrack) diretor.SetGenericBinding(tr, brain);
        diretor.playOnAwake = false; diretor.timeUpdateMode = DirectorUpdateMode.Manual; diretor.extrapolationMode = DirectorWrapMode.Hold;
        CameraDeCena.Ligar(); CameraDeCena.Externa = true;
        tempo = 0f; tocando = true; diretor.Play(); Aplicar();
    }

    /// <summary>Define o ponto (em segundos) da Timeline. Chame todo quadro.</summary>
    public void Definir(float t) { tempo = Mathf.Max(0f, t); }

    void LateUpdate() { if (tocando) Aplicar(); }

    void Aplicar()
    {
        if (diretor == null || brain == null || cam == null) return;
        diretor.time = tempo; diretor.Evaluate(); brain.ManualUpdate();
        CameraDeCena.Definir(cam.transform.position, cam.transform.rotation);        // o jogo continua sabendo onde a câmera está
    }

    /// <summary>Termina a cena e devolve a câmera ao controle normal (CameraDeCena.Soltar faz a volta suave).</summary>
    public void Encerrar()
    {
        if (!tocando && brain == null) return;
        tocando = false; CameraDeCena.Externa = false;
        if (brain != null) { Destroy(brain); brain = null; }
        if (cam != null) cam.fieldOfView = fovOriginal;
        Destroy(gameObject);
    }

    void OnDestroy() { if (tocando) { tocando = false; CameraDeCena.Externa = false; if (brain != null) Destroy(brain); if (cam != null) cam.fieldOfView = fovOriginal; } }

    // referência visual no editor: o ponto da cena (corpo do dragão) e uma linha para o alto
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f); Gizmos.DrawWireSphere(transform.position, 6f * transform.lossyScale.x);
        Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.5f); Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 80f * transform.lossyScale.x);
        Gizmos.color = Color.cyan; Gizmos.DrawRay(transform.position, transform.forward * 8f);
    }
}
