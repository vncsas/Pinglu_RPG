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

    private float invulneravelAte;

    private void Awake() { Atual = vidaMaxima; }

    public void DefinirMaxima(float valor, bool encher = true)
    {
        vidaMaxima = Mathf.Max(1f, valor);
        if (encher) Atual = vidaMaxima;
    }

    public bool ReceberDano(float dano, Vector3 origem)
    {
        if (Morto || dano <= 0f || Time.time < invulneravelAte) return false;
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

    public void Curar(float valor)
    {
        if (Morto) return;
        Atual = Mathf.Min(vidaMaxima, Atual + valor);
    }
}
