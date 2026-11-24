using UnityEngine;

// Tela de teste da maga: um botao por animacao (as dela: magia, flutuar, andar...),
// camera girando em volta, velocidade, e a mao esquerda abrindo/fechando por cima da animacao.
[RequireComponent(typeof(Animator))]
public class DemoMaga : MonoBehaviour
{
    public Camera cam;
    public float anguloCamera = 25f, distancia = 3.6f, velocidade = 1f;
    public bool girarSozinha, moverPelaCena = true;
    [Range(-1, 1)] public float maoEsquerda;      // -1 aberta, 0 = a da animacao, 1 fechada

    public static readonly string[] NoChao = { "Parado", "Andar", "Correr", "Magia_Entrar", "Magia_Pronta", "Magia_Lancar", "Magia_Sair", "Raio_Cajado", "Levar_Golpe", "Morrer" };
    public static readonly string[] NoAr = { "Comecar_Flutuar", "Flutuar", "Flutuar_Frente", "Flutuar_Magia_Pronta", "Flutuar_Magia_Lancar", "Flutuar_Raio", "Pousar" };
    public static string[] Estados => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(NoChao, NoAr));
    static readonly System.Collections.Generic.Dictionary<string, float> Deslocamento = new()
    { { "Andar", 0.88f }, { "Correr", 1.33f }, { "Flutuar_Frente", 1.5f } };

    Animator anim; string tocando = "Parado", aviso = "";
    HumanPoseHandler pose; HumanPose hp;

    void Start()
    {
        anim = GetComponent<Animator>();
        pose = new HumanPoseHandler(anim.avatar, transform);
    }

    void Tocar(string nome)
    {
        if (!anim.HasState(0, Animator.StringToHash(nome))) { aviso = $"'{nome}' nao esta no Animator: rode o menu Maga > Montar tudo."; return; }
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
        if (Mathf.Abs(maoEsquerda) > 0.01f)          // musculos dos dedos da mao esquerda do Humanoid
        {
            pose.GetHumanPose(ref hp);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                var n = HumanTrait.MuscleName[i];
                if (n.StartsWith("Left") && (n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little")) && !n.Contains("Spread"))
                    hp.muscles[i] = -maoEsquerda;   // +1 = dedo esticado, -1 = dobrado
            }
            pose.SetHumanPose(ref hp);
        }
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
        GUILayout.Label("<b>Maga</b>  tocando: " + tocando, new GUIStyle(GUI.skin.label) { richText = true });
        if (aviso != "") GUILayout.Label("<color=yellow>" + aviso + "</color>", new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
        GUILayout.Label("No chao");
        foreach (var n in NoChao) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Label("Flutuando");
        foreach (var n in NoAr) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Space(6);
        GUILayout.Label("Mao esquerda: " + (maoEsquerda < -0.01f ? "abrindo" : maoEsquerda > 0.01f ? "fechando" : "a da animacao"));
        maoEsquerda = GUILayout.HorizontalSlider(maoEsquerda, -1f, 1f);
        if (GUILayout.Button("mao: voltar para a da animacao")) maoEsquerda = 0f;
        GUILayout.Space(6);
        GUILayout.Label("Camera: " + Mathf.RoundToInt(anguloCamera % 360) + " graus");
        anguloCamera = GUILayout.HorizontalSlider(anguloCamera % 360, 0, 360);
        girarSozinha = GUILayout.Toggle(girarSozinha, "girar sozinha");
        moverPelaCena = GUILayout.Toggle(moverPelaCena, "andar pela cena");
        GUILayout.Label("Zoom"); distancia = GUILayout.HorizontalSlider(distancia, 1.0f, 8f);
        GUILayout.Label("Velocidade: " + velocidade.ToString("0.00") + "x"); velocidade = GUILayout.HorizontalSlider(velocidade, 0.1f, 2f);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    Vector2 scroll;
}
