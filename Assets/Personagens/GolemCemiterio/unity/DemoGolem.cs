using System.Linq;
using UnityEngine;

// Tela de teste do Golem do Cemiterio (mini-boss): um botao por animacao (ataques, rugido, pisada...),
// camera girando em volta, velocidade e andar pela cena.
[RequireComponent(typeof(Animator))]
public class DemoGolem : MonoBehaviour
{
    public Camera cam;
    public float anguloCamera = 25f, distancia = 8f, velocidade = 1f;
    public bool girarSozinha, moverPelaCena = true;

    public static readonly string[] NoChao = { "Parado", "Andar", "Correr", "Levar_Golpe", "Levar_Golpe_Forte", "Atordoado", "Morrer" };
    public static readonly string[] Ataque = { "Postura_Luta", "Soco_Direto", "Soco_Pesado", "Varrer", "Esmagar", "Pisada", "Rugir" };
    public static string[] Estados => NoChao.Concat(Ataque).ToArray();
    static readonly System.Collections.Generic.Dictionary<string, float> Deslocamento = new()
    { { "Andar", 1.0f }, { "Correr", 2.2f } };

    Animator anim; string tocando = "Parado", aviso = "";

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Tocar(string nome)
    {
        if (!anim.HasState(0, Animator.StringToHash(nome))) { aviso = $"'{nome}' nao esta no Animator: rode o menu Golem > Montar tudo."; return; }
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
        var alvo = transform.position + Vector3.up * 1.3f;
        cam.transform.position = alvo + Quaternion.Euler(0, anguloCamera, 0) * Vector3.forward * distancia + Vector3.up * 0.3f;
        cam.transform.LookAt(alvo);
    }

    void OnGUI()
    {
        anim.speed = velocidade;
        GUILayout.BeginArea(new Rect(10, 10, 240, Screen.height - 20), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("<b>Golem do Cemiterio</b>  tocando: " + tocando, new GUIStyle(GUI.skin.label) { richText = true });
        if (aviso != "") GUILayout.Label("<color=yellow>" + aviso + "</color>", new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
        GUILayout.Label("Movimento");
        foreach (var n in NoChao) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Label("Ataques do mini-boss");
        foreach (var n in Ataque) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Space(6);
        GUILayout.Label("Camera: " + Mathf.RoundToInt(anguloCamera % 360) + " graus");
        anguloCamera = GUILayout.HorizontalSlider(anguloCamera % 360, 0, 360);
        girarSozinha = GUILayout.Toggle(girarSozinha, "girar sozinha");
        moverPelaCena = GUILayout.Toggle(moverPelaCena, "andar pela cena");
        GUILayout.Label("Zoom"); distancia = GUILayout.HorizontalSlider(distancia, 2.0f, 16f);
        GUILayout.Label("Velocidade: " + velocidade.ToString("0.00") + "x"); velocidade = GUILayout.HorizontalSlider(velocidade, 0.1f, 2f);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    Vector2 scroll;
}
