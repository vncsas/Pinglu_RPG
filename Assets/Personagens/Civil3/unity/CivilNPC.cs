using UnityEngine;

// Controle simples de NPC para o Civil 3: chame os metodos abaixo pelo seu sistema de IA / dialogo.
// Os nomes dos estados sao os do Animator gerado pelo menu Civil > Montar tudo (Civil.controller).
[RequireComponent(typeof(Animator))]
public class CivilNPC : MonoBehaviour
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
    public void Morrer()            { Tocar("Morrer"); }               // fica deitado no ultimo quadro
}
