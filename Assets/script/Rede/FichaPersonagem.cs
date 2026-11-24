using UnityEngine;

// "Ficha" de um personagem jogável. Cada personagem novo = uma ficha nova
// (botão direito na aba Project > Create > Pinglu > Ficha de Personagem).
[CreateAssetMenu(fileName = "NovoPersonagem", menuName = "Pinglu/Ficha de Personagem")]
public class FichaPersonagem : ScriptableObject
{
    [Header("Apresentação")]
    public string nome = "Novo personagem";
    [TextArea(2, 4)] public string descricao = "";
    [Tooltip("Uma linha por habilidade, mostradas no menu de seleção")]
    public string[] habilidades = new string[0];
    public Color corDestaque = new Color(0.8f, 0.6f, 0.25f);
    public Texture2D retrato;

    [Tooltip("Título que aparece embaixo do nome na tela de seleção")]
    public string titulo = "";
    [Tooltip("Fonte do nome na tela de seleção (cada personagem tem a sua)")]
    public Font fonteNome;
    public int tamanhoNome = 64;
    public Color corNome = new Color(0.97f, 0.93f, 0.86f, 1f);

    [Header("Atributos (0 a 10, só para mostrar na seleção)")]
    [Range(0, 10)] public int forca = 5;
    [Range(0, 10)] public int defesa = 5;
    [Range(0, 10)] public int velocidade = 5;
    [Range(0, 10)] public int alcance = 5;

    [Header("Palco da tela de seleção")]
    [Tooltip("O modelo 3D (o .fbx do personagem, sem scripts)")]
    public GameObject modeloVisual;
    public AnimationClip animacaoParado;
    [Tooltip("Golpe de apresentação tocado quando o personagem é escolhido")]
    public AnimationClip animacaoEntrada;
    [Tooltip("Caminhada usada quando o personagem entra no portal")]
    public AnimationClip animacaoAndar;

    [Header("Jogo")]
    [Tooltip("Prefab do jogador (precisa ter NetworkObject). O menu Pinglu > 15 cria o do cavaleiro.")]
    public GameObject prefabJogador;
    [Tooltip("Desmarque enquanto o personagem ainda não existe (aparece como 'em breve')")]
    public bool disponivel = true;
}
