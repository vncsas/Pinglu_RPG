using System;
using UnityEngine;

// Em que ponto da história o jogo está. Quem manda é o host (ou o jogador no solo); ele salva o progresso.
public enum Etapa
{
    Intro = 0,           // cutscene inicial
    FalarComCaio = 1,    // conversar com o ancião da vila
    Cemiterio = 2,       // 1º cristal: o cemitério (horda + guardião)
    Castelo = 3,         // 2º cristal: o castelo (Cavaleiro Antigo + o ladrão)
    Panteano = 4,        // 3º cristal: o templo do pântano
    Demonio = 5,         // o demônio renasce dos três cristais
    Recompensa = 6,      // voltar ao Caio
    Fim = 7
}

public static class Campanha
{
    const string CHAVE = "Pinglu_Campanha_Etapa";

    public static Etapa Atual { get; private set; } = Etapa.Intro;
    // objetivo curto do momento (ex.: "Entre no Salão do Trono"); vale até a etapa mudar
    public static string Dica;
    public static event Action<Etapa> AoMudar;

    // multiplayer: o host avisa os outros jogadores (a rede liga isso depois)
    public static Action<int> EnviarParaTodos;

    // Cada vez que o jogo começa (Play ou abrir o programa) a história volta do ZERO. O "checkpoint" é só a etapa
    // guardada em memória: se o jogador morrer e a cena recarregar, continua de onde estava.
    // (Com "Enter Play Mode Options" desligando o reload de domínio, os valores estáticos sobrevivem entre Plays; por isso o reset aqui.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ZerarAoIniciar()
    {
        Atual = Etapa.Intro; AoMudar = null; EnviarParaTodos = null; Dica = null;
        PlayerPrefs.DeleteKey(CHAVE);
    }

    public static void Carregar()
    {
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) Atual = Etapa.FalarComCaio;     // convidado: recebe do host
    }

    public static void Ir(Etapa e, bool salvar = true)
    {
        if (e == Atual) return;
        Atual = e; Dica = null;
        if (EnviarParaTodos != null && (!SessaoDeJogo.Online || SessaoDeJogo.SouHost)) EnviarParaTodos((int)e);
        AoMudar?.Invoke(e);
    }

    // chamado pela rede no PC dos convidados
    public static void ReceberDoHost(int e) { Ir((Etapa)Mathf.Clamp(e, 0, (int)Etapa.Fim), false); }

    public static void Avancar() { if (Atual < Etapa.Fim) Ir(Atual + 1); }
    public static void Recomecar() { Ir(Etapa.Intro); }

    public static string Capitulo(Etapa e)
    {
        switch (e)
        {
            case Etapa.Intro: return "Prólogo";
            case Etapa.FalarComCaio: return "Capítulo 1 - Uma vila em apuros";
            case Etapa.Cemiterio: return "Capítulo 2 - O cemitério esquecido";
            case Etapa.Castelo: return "Capítulo 3 - O castelo caído";
            case Etapa.Panteano: return "Capítulo 4 - O templo do pântano";
            case Etapa.Demonio: return "Capítulo 5 - O despertar";
            case Etapa.Recompensa: return "Epílogo - A recompensa";
            default: return "Fim";
        }
    }

    public static string Objetivo(Etapa e)
    {
        if (!string.IsNullOrEmpty(Dica) && e == Atual) return Dica;
        switch (e)
        {
            case Etapa.Intro: return "";
            case Etapa.FalarComCaio: return "Fale com Caio, o ancião, na praça da vila.";
            case Etapa.Cemiterio: return "Vá ao cemitério e recupere o primeiro cristal.";
            case Etapa.Castelo: return "Siga até o Castelo Destruído e recupere o segundo cristal.";
            case Etapa.Panteano: return "Chegue ao templo do pântano, onde o terceiro cristal está.";
            case Etapa.Demonio: return "Derrote o demônio que surgiu dos cristais!";
            case Etapa.Recompensa: return "Volte à vila e fale com Caio para receber a recompensa.";
            default: return "A paz voltou a Valverde.";
        }
    }

    // texto mais longo da missão atual (mostrado ao segurar V)
    public static string Resumo(Etapa e)
    {
        switch (e)
        {
            case Etapa.FalarComCaio: return "Valverde está ameaçada por mortos-vivos que saem de três lugares abandonados, onde antigos cristais foram selados. Caio, o ancião, está na praça da vila e sabe o que fazer.";
            case Etapa.Cemiterio: return "O primeiro cristal estava guardado no cemitério esquecido. Enfrente as cinco hordas de mortos e depois o Guardião das Lápides, que protege o centro do cemitério.";
            case Etapa.Castelo: return "Um bilhete avisou que \"ele\" vai acordar. Quem o escreveu fugiu para o Castelo Destruído, onde o Cavaleiro Antigo guarda o segundo cristal.";
            case Etapa.Panteano: return "O último cristal repousa no templo do pântano. Os selos estão se rompendo e o poder do demônio, dividido em três cristais, está voltando.";
            case Etapa.Demonio: return "Os cristais brilham e o demônio renasceu. É agora ou nunca: impeça que ele destrua a vila.";
            case Etapa.Recompensa: return "A ameaça acabou. Caio espera na vila para entregar a recompensa prometida: a escritura do cabaré.";
            default: return "";
        }
    }

    // lista de passos da história: texto e se já foi cumprido
    public static void Tarefas(Etapa atual, System.Collections.Generic.List<string> textos, System.Collections.Generic.List<bool> feitas)
    {
        textos.Clear(); feitas.Clear();
        Etapa[] ordem = { Etapa.FalarComCaio, Etapa.Cemiterio, Etapa.Castelo, Etapa.Panteano, Etapa.Demonio, Etapa.Recompensa };
        string[] nomes = { "Falar com Caio, o ancião", "Cemitério: recuperar o 1º cristal", "Castelo: recuperar o 2º cristal", "Pântano: recuperar o 3º cristal", "Derrotar o demônio", "Receber a recompensa" };
        for (int i = 0; i < ordem.Length; i++) { textos.Add(nomes[i]); feitas.Add((int)atual > (int)ordem[i]); }
    }
}
