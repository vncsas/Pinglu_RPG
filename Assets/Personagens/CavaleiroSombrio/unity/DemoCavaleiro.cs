using UnityEngine;

// Tela de teste: um botao para cada animacao, camera girando em volta e velocidade.
// As animacoes sao "no lugar"; aqui o cavaleiro anda pra frente na velocidade de cada uma (como o script
// de controle do jogo vai fazer), com a camera seguindo. So para ver as animacoes.
[RequireComponent(typeof(Animator))]
public class DemoCavaleiro : MonoBehaviour
{
    public Camera cam;
    public float anguloCamera = 25f, distancia = 4.2f, velocidade = 1f;
    public bool girarSozinha, moverPelaCena = true;

    // metros por segundo em que os pes nao escorregam (vem do gerador das animacoes)
    static readonly System.Collections.Generic.Dictionary<string, float> Deslocamento = new()
    { { "Andar", 0.73f }, { "Andar_Sem_Espada", 0.73f }, { "Correr", 3.19f }, { "Correr_Com_Espada", 3.0f } };

    static readonly string[] ComEspada = { "Parado", "Andar", "Correr_Com_Espada", "Ataque_A", "Ataque_B", "Ataque_C", "Ataque_Combo",
                                           "Ataque_Pesado", "Defender", "Investida", "Levar_Golpe" };
    static readonly string[] SemEspada = { "Parado_Sem_Espada", "Andar_Sem_Espada", "Correr" };
    static readonly string[] Troca = { "Guardar_Espada", "Sacar_Espada" };

    Animator anim; GuardaEspada guarda; string tocando = "Parado";

    void Start() { anim = GetComponent<Animator>(); guarda = GetComponent<GuardaEspada>(); }

    string aviso = "";

    void Tocar(string nome, bool? espadaNasCostas)
    {
        if (!anim.HasState(0, Animator.StringToHash(nome)))
        {   // controller antigo: falta rodar o menu Cavaleiro Sombrio > Montar tudo
            aviso = $"A animacao '{nome}' nao esta no Animator. Saia do Play e rode o menu Cavaleiro Sombrio > Montar tudo.";
            return;
        }
        aviso = "";
        if (espadaNasCostas.HasValue && guarda) guarda.guardada = espadaNasCostas.Value;
        anim.CrossFadeInFixedTime(nome, 0.15f); tocando = nome;
    }

    void Update()
    {
        if (!moverPelaCena) return;
        var info = anim.GetCurrentAnimatorStateInfo(0); float v = 0f;
        foreach (var kv in Deslocamento) if (info.IsName(kv.Key)) v = kv.Value;
        transform.position += transform.forward * v * anim.speed * Time.deltaTime;
        if (transform.position.z > 25f) transform.position -= transform.forward * 50f;   // volta para o comeco do chao
    }

    void LateUpdate()
    {
        if (girarSozinha) anguloCamera += 20f * Time.deltaTime;
        if (!cam) return;
        var alvo = transform.position + Vector3.up * 1.0f;
        var dir = Quaternion.Euler(0, anguloCamera, 0) * Vector3.forward;
        cam.transform.position = alvo + dir * distancia + Vector3.up * 0.35f;
        cam.transform.LookAt(alvo);
    }

    void OnGUI()
    {
        anim.speed = velocidade;
        GUILayout.BeginArea(new Rect(10, 10, 230, Screen.height - 20), GUI.skin.box);
        GUILayout.Label("<b>Cavaleiro Sombrio</b>  tocando: " + tocando, new GUIStyle(GUI.skin.label) { richText = true });
        if (aviso != "") GUILayout.Label("<color=yellow>" + aviso + "</color>", new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
        GUILayout.Label("Espada na mao");
        foreach (var n in ComEspada) if (GUILayout.Button(n)) Tocar(n, false);
        GUILayout.Label("Espada nas costas");
        foreach (var n in SemEspada) if (GUILayout.Button(n)) Tocar(n, true);
        GUILayout.Label("Trocar (a espada muda de lugar no meio)");
        foreach (var n in Troca) if (GUILayout.Button(n)) Tocar(n, null);
        GUILayout.Space(8);
        GUILayout.Label("Camera: " + Mathf.RoundToInt(anguloCamera % 360) + " graus");
        anguloCamera = GUILayout.HorizontalSlider(anguloCamera % 360, 0, 360);
        girarSozinha = GUILayout.Toggle(girarSozinha, "girar sozinha");
        moverPelaCena = GUILayout.Toggle(moverPelaCena, "andar pela cena");
        GUILayout.Label("Zoom");
        distancia = GUILayout.HorizontalSlider(distancia, 1.5f, 8f);
        GUILayout.Label("Velocidade: " + velocidade.ToString("0.00") + "x");
        velocidade = GUILayout.HorizontalSlider(velocidade, 0.1f, 2f);
        GUILayout.EndArea();
    }
}
