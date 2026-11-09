using System.Linq;
using UnityEngine;

// Tela de teste do Mulher 1 (NPC): um botao por animacao, camera girando em volta, velocidade e andar pela cena.
[RequireComponent(typeof(Animator))]
public class DemoWoman1 : MonoBehaviour
{
    public Camera cam;
    public float anguloCamera = 25f, distancia = 4.2f, velocidade = 1f;
    public bool girarSozinha, moverPelaCena = true;

    public static readonly string[] Movimento = { "Parado", "Andar", "Andar_Calmo", "Correr", "Correr_Desesperado", "Carregar_Andar", "Agachado" };
    public static readonly string[] Conversa = { "Falar", "Ouvir", "Concordar", "Negar", "Interagir", "Assustado", "Dancar" };
    public static readonly string[] Trabalho = { "Beber", "Varrer", "Cortar_Lenha", "Colher", "Plantar", "Regar", "Consertar", "Abrir_Bau", "Pegar_Objeto", "Empurrar", "Tocha", "Lanterna" };
    public static readonly string[] Sentar = { "Sentar_Entrar", "Sentado", "Sentado_Falando", "Sentar_Sair" };
    public static readonly string[] Dano = { "Levar_Golpe", "Levar_Golpe_Cabeca", "Levar_Golpe_Forte", "Morrer" };
    public static string[] Estados => Movimento.Concat(Conversa).Concat(Trabalho).Concat(Sentar).Concat(Dano).ToArray();
    static readonly System.Collections.Generic.Dictionary<string, float> Deslocamento = new()
    { { "Andar", 1.3f }, { "Andar_Calmo", 1.1f }, { "Carregar_Andar", 1.0f }, { "Correr", 3.5f }, { "Correr_Desesperado", 5.0f } };

    Animator anim; string tocando = "Parado", aviso = ""; Vector2 scroll;

    void Start() { anim = GetComponent<Animator>(); }

    void Tocar(string nome)
    {
        if (!anim.HasState(0, Animator.StringToHash(nome))) { aviso = $"'{nome}' nao esta no Animator: rode o menu Woman1 > Montar tudo."; return; }
        aviso = ""; anim.CrossFadeInFixedTime(nome, 0.15f); tocando = nome;
    }

    void Update()
    {
        if (!moverPelaCena) return;
        var info = anim.GetCurrentAnimatorStateInfo(0); float v = 0f;
        foreach (var kv in Deslocamento) if (info.IsName(kv.Key)) v = kv.Value;
        transform.position += transform.forward * v * anim.speed * Time.deltaTime;
        if (transform.position.z > 25f) transform.position -= transform.forward * 50f;
    }

    void LateUpdate()
    {
        if (girarSozinha) anguloCamera += 20f * Time.deltaTime;
        if (!cam) return;
        var alvo = transform.position + Vector3.up * 1.0f;
        cam.transform.position = alvo + Quaternion.Euler(0, anguloCamera, 0) * Vector3.forward * distancia + Vector3.up * 0.3f;
        cam.transform.LookAt(alvo);
    }

    void OnGUI()
    {
        anim.speed = velocidade;
        GUILayout.BeginArea(new Rect(10, 10, 240, Screen.height - 20), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("<b>Mulher 1 (NPC)</b>  tocando: " + tocando, new GUIStyle(GUI.skin.label) { richText = true });
        if (aviso != "") GUILayout.Label("<color=yellow>" + aviso + "</color>", new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
        GUILayout.Label("Movimento");
        foreach (var n in Movimento) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Label("Conversa / interacao");
        foreach (var n in Conversa) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Label("Trabalho / dia a dia");
        foreach (var n in Trabalho) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Label("Sentar");
        foreach (var n in Sentar) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Label("Levar dano");
        foreach (var n in Dano) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Space(6);
        GUILayout.Label("Camera: " + Mathf.RoundToInt(anguloCamera % 360) + " graus");
        anguloCamera = GUILayout.HorizontalSlider(anguloCamera % 360, 0, 360);
        girarSozinha = GUILayout.Toggle(girarSozinha, "girar sozinha");
        moverPelaCena = GUILayout.Toggle(moverPelaCena, "andar pela cena");
        GUILayout.Label("Zoom"); distancia = GUILayout.HorizontalSlider(distancia, 1.5f, 10f);
        GUILayout.Label("Velocidade: " + velocidade.ToString("0.00") + "x"); velocidade = GUILayout.HorizontalSlider(velocidade, 0.1f, 2f);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
