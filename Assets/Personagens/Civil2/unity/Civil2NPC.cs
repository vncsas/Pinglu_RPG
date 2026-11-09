using UnityEngine;

// Controle simples de NPC para o Civil 2: chame os metodos abaixo pelo seu sistema de IA / dialogo.
// Os nomes dos estados sao os do Animator gerado pelo menu Civil2 > Montar tudo (Civil2.controller).
[RequireComponent(typeof(Animator))]
public class Civil2NPC : MonoBehaviour
{
    public float duracaoMistura = 0.15f;
    Animator anim;
    void Awake() { anim = GetComponent<Animator>(); }

    public void Tocar(string estado) { if (anim.HasState(0, Animator.StringToHash(estado))) anim.CrossFadeInFixedTime(estado, duracaoMistura); }

    public void Parar()             { Tocar("Parado"); }
    public void Andar()             { Tocar("Andar"); }
    public void AndarCalmo()        { Tocar("Andar_Calmo"); }
    public void Correr()            { Tocar("Correr"); }
    public void CorrerDesesperado() { Tocar("Correr_Desesperado"); }   // fugindo de um ataque / monstro
    public void Assustar()          { Tocar("Assustado"); }            // parado de medo, maos na frente do rosto
    public void Falar()             { Tocar("Falar"); }                // gesticulando enquanto conversa (loop)
    public void Ouvir()             { Tocar("Ouvir"); }                // bracos cruzados, ouvindo o outro NPC (loop)
    public void Concordar()         { Tocar("Concordar"); }            // balanca a cabeca (volta sozinho pro Parado)
    public void Negar()             { Tocar("Negar"); }
    public void Interagir()         { Tocar("Interagir"); }            // pega / usa / abre algo
    public void LevarGolpe(bool forte = false, bool cabeca = false)    // voltam sozinhos pro Parado
    { Tocar(forte ? "Levar_Golpe_Forte" : cabeca ? "Levar_Golpe_Cabeca" : "Levar_Golpe"); }
    public void Beber()             { Tocar("Beber"); }                // bebe de um copo/caneca (volta sozinho pro Parado)
    public void Varrer()            { Tocar("Varrer"); }               // loop, segurando uma vassoura (o prop e do jogo)
    public void CortarLenha()       { Tocar("Cortar_Lenha"); }         // loop, com machado
    public void Colher()            { Tocar("Colher"); }
    public void Plantar()           { Tocar("Plantar"); }
    public void Regar()             { Tocar("Regar"); }
    public void Consertar()         { Tocar("Consertar"); }
    public void AbrirBau()          { Tocar("Abrir_Bau"); }
    public void PegarObjeto()       { Tocar("Pegar_Objeto"); }
    public void Empurrar()          { Tocar("Empurrar"); }             // loop, empurrando carroca / porta
    public void Carregar()          { Tocar("Carregar_Andar"); }       // andando carregando uma caixa / saco
    public void Dancar()            { Tocar("Dancar"); }
    public void Sentar()            { Tocar("Sentar_Entrar"); }        // vai pro Sentado sozinho
    public void Levantar()          { Tocar("Sentar_Sair"); }
    public void SentadoFalando()    { Tocar("Sentado_Falando"); }
    public void Agachar()           { Tocar("Agachado"); }
    public void Morrer()            { Tocar("Morrer"); }               // fica deitado no ultimo quadro
}
