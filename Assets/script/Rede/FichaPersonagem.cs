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

    [Header("Jogo")]
    [Tooltip("Prefab do jogador (precisa ter NetworkObject). O menu Pinglu > 15 cria o do cavaleiro.")]
    public GameObject prefabJogador;
    [Tooltip("Desmarque enquanto o personagem ainda não existe (aparece como 'em breve')")]
    public bool disponivel = true;
}
