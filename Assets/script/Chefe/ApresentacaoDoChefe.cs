using System;
using UnityEngine;

public enum AnimacaoDoTitulo { Estampar, Surgir }

// Como o chefe é apresentado na tela (nome, frase, fonte, cores e jeito de entrar).
// Cada chefe novo cria a sua própria apresentação aqui (um "preset") e passa para o TituloDeChefe.
[Serializable]
public class ApresentacaoDoChefe
{
    public string nome = "Chefe";
    public string epiteto = "";
    [Tooltip("Fonte dentro de Assets/Resources (sem a extensão)")]
    public string fonte = "Fontes/UncialAntiqua";
    public AnimacaoDoTitulo animacao = AnimacaoDoTitulo.Estampar;
    public Color corLetra = new Color(0.82f, 0.78f, 0.7f);
    public Color corBrilho = new Color(1f, 0.45f, 0.1f);
    public float duracao = 5.4f;
    public float tamanho = 1f;

    // O Guardião das Lápides: letras de pedra antiga estampadas uma a uma, com rachaduras de brasa.
    public static ApresentacaoDoChefe Guardiao()
    {
        return new ApresentacaoDoChefe
        {
            nome = "Guardião das Lápides", epiteto = "O Vigia dos Mortos",
            fonte = "Fontes/UncialAntiqua", animacao = AnimacaoDoTitulo.Estampar,
            corLetra = new Color(0.80f, 0.77f, 0.70f), corBrilho = new Color(1f, 0.42f, 0.1f), duracao = 5.4f, tamanho = 1f
        };
    }

    // O Cavaleiro Antigo: letras góticas pesadas, brilho carmesim (a cor da chama dele)
    public static ApresentacaoDoChefe CavaleiroAntigo()
    {
        return new ApresentacaoDoChefe
        {
            nome = "Cavaleiro Antigo", epiteto = "Guardião do Trono Caído",
            fonte = "Fontes/PirataOne", animacao = AnimacaoDoTitulo.Estampar,
            corLetra = new Color(0.78f, 0.74f, 0.72f), corBrilho = new Color(1f, 0.12f, 0.16f), duracao = 4.6f, tamanho = 1.05f
        };
    }

    // O Ladrão: aparece de repente (sem estampar), tom frio e discreto
    public static ApresentacaoDoChefe Ladrao()
    {
        return new ApresentacaoDoChefe
        {
            nome = "O Ladrão", epiteto = "Quem roubou os cristais",
            fonte = "Fontes/PirataOne", animacao = AnimacaoDoTitulo.Surgir,
            corLetra = new Color(0.82f, 0.82f, 0.86f), corBrilho = new Color(0.35f, 0.6f, 1f), duracao = 3.8f, tamanho = 0.9f
        };
    }
}
