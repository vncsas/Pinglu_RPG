using UnityEngine;

// Personagem com quem o jogador conversa: aparece "[E] Falar" por perto, vira o corpo para o jogador e abre o diálogo
// de acordo com o ponto da história (Roteiro.Caio).
public class NpcFalante : MonoBehaviour
{
    public string nomeExibido = "Caio";
    public float alcance = 3.6f;
    public Transform etiqueta;          // nome flutuando; sempre vira para a câmera
    public Font fonte;

    private Transform jogador; private float proximaBusca; private bool perto; private GUIStyle est;

    private void Update()
    {
        if (Time.time >= proximaBusca)
        {
            proximaBusca = Time.time + 0.4f;
            jogador = null; float melhor = 1e9f;
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            {
                if (!p.enabled || p.Companheiro) continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < melhor) { melhor = d; jogador = p.transform; }
            }
        }
        perto = jogador != null && !SessaoDeJogo.MenuAberto && !DialogoUI.Aberto && (jogador.position - transform.position).sqrMagnitude < alcance * alcance;
        if (jogador != null && (jogador.position - transform.position).sqrMagnitude < 36f)
        {
            Vector3 d = jogador.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 3f);
        }
        if (perto && Input.GetKeyDown(KeyCode.E) && Time.unscaledTime - DialogoUI.UltimoFechamento > 0.5f)
        {
            System.Action fim;
            Fala[] falas = Roteiro.Caio(Campanha.Atual, out fim);
            DialogoUI.Abrir(falas, fim);
        }
        if (etiqueta != null && Camera.main != null)
            etiqueta.rotation = Quaternion.LookRotation(etiqueta.position - Camera.main.transform.position);
    }

    private void OnGUI()
    {
        if (!perto) return;
        float e = Screen.height / 1080f;
        if (est == null) { est = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, font = fonte }; est.normal.textColor = new Color(1f, 0.9f, 0.6f); }
        Rect r = new Rect(Screen.width * 0.5f - 300f * e, Screen.height * 0.72f, 600f * e, 60f * e);
        GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
        GUIStyle s = new GUIStyle(est) { fontSize = Mathf.RoundToInt(34 * e) };
        GUI.Label(r, "[E] Falar com " + nomeExibido, s);
    }
}
