using System;
using UnityEngine;

// Vida de qualquer personagem (jogador ou monstro).
public class Vida : MonoBehaviour
{
    [SerializeField] private float vidaMaxima = 100f;
    [Tooltip("Tempo invulnerável depois de levar um golpe (segundos)")]
    [SerializeField] private float invulneravelAposDano = 0.15f;

    public float Atual { get; private set; }
    public float Maxima => vidaMaxima;
    public float Fracao => vidaMaxima > 0f ? Atual / vidaMaxima : 0f;
    public bool Morto { get; private set; }

    // dano recebido, posição de quem bateu
    public event Action<float, Vector3> AoLevarDano;
    public event Action AoMorrer;

    // quem estiver controlando pode bloquear/reduzir o dano (ex.: escudo, rolagem)
    public Func<float, Vector3, float> FiltroDeDano;

    // multiplayer: o dano/cura vai pelo rede para o PC que manda neste personagem
    public Func<float, Vector3, bool> Redirecionar;
    public Action<float> RedirecionarCura;

    private float invulneravelAte;

    private void Awake() { Atual = vidaMaxima; }

    public void DefinirMaxima(float valor, bool encher = true)
    {
        vidaMaxima = Mathf.Max(1f, valor);
        if (encher) Atual = vidaMaxima;
    }

    public bool ReceberDano(float dano, Vector3 origem)
    {
        if (Morto || dano <= 0f) return false;
        if (Redirecionar != null) return Redirecionar(dano, origem);
        if (Time.time < invulneravelAte) return false;
        if (FiltroDeDano != null) dano = FiltroDeDano(dano, origem);
        if (dano <= 0f) return false;

        Atual = Mathf.Max(0f, Atual - dano);
        invulneravelAte = Time.time + invulneravelAposDano;
        AoLevarDano?.Invoke(dano, origem);
        if (Atual <= 0f)
        {
            Morto = true;
            AoMorrer?.Invoke();
        }
        return true;
    }

    // multiplayer: cópia de um personagem controlado em outro PC recebe a vida pela rede
    public void DefinirAtual(float valor)
    {
        Atual = Mathf.Clamp(valor, 0f, vidaMaxima);
        bool morreu = Atual <= 0f && !Morto;
        if (Atual <= 0f) Morto = true;
        if (morreu) AoMorrer?.Invoke();
    }

    // volta a viver com a vida cheia (renascer no multiplayer), com 2 s de proteção
    public void Reviver()
    {
        Morto = false;
        Atual = vidaMaxima;
        invulneravelAte = Time.time + 2f;
    }

    public void Curar(float valor)
    {
        if (RedirecionarCura != null) { RedirecionarCura(valor); return; }
        if (Morto) return;
        Atual = Mathf.Min(vidaMaxima, Atual + valor);
    }
}
