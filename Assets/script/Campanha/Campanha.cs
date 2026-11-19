using System;
using UnityEngine;

// Em que ponto da história o jogo está. Quem manda é o host (ou o jogador no solo).
// Depois de falar com o Caio, os três lugares (cemitério, castelo, pântano) podem ser feitos em QUALQUER ordem.
public enum Etapa
{
    Intro = 0,           // cutscene inicial
    FalarComCaio = 1,    // conversar com o ancião da vila (ele entrega o mapa da ilha)
    Cristais = 2,        // os 3 mini-bosses, em qualquer ordem
    Dragao = 3,          // chefe final: o dragão
    Recompensa = 4,      // voltar ao Caio
    Fim = 5
}

public enum Local { Cemiterio = 0, Castelo = 1, Pantano = 2 }

public static class Campanha
{
    const string CHAVE = "Pinglu_Campanha_Etapa";

    public static Etapa Atual { get; private set; } = Etapa.Intro;
    // objetivo curto do momento (ex.: "Entre no Salão do Trono"); vale até a etapa mudar
    public static string Dica;
    public static event Action<Etapa> AoMudar;

    // multiplayer: o host avisa os outros jogadores (a rede liga isso depois)
    public static Action<int> EnviarParaTodos;

    // o que já foi feito nos três lugares
    public static bool CemiterioFeito, CasteloFeito, PantanoFeito;
    // o mapa da ilha (que o Caio entrega) e o mini-mapa só existem depois do tutorial
    public static bool TemMapa, TemMiniMapa;

    public static bool Feito(Local l) { return l == Local.Cemiterio ? CemiterioFeito : l == Local.Castelo ? CasteloFeito : PantanoFeito; }
    public static int QuantosFeitos { get { return (CemiterioFeito ? 1 : 0) + (CasteloFeito ? 1 : 0) + (PantanoFeito ? 1 : 0); } }
    public static bool TodosFeitos { get { return QuantosFeitos >= 3; } }

    static void LimparTudo()
    {
        Atual = Etapa.Intro; Dica = null;
        CemiterioFeito = CasteloFeito = PantanoFeito = false; TemMapa = TemMiniMapa = false;
    }

    // Cada vez que o jogo começa (Play ou abrir o programa) a história volta do ZERO. O "checkpoint" é só a etapa
    // guardada em memória: se o jogador morrer e a cena recarregar, continua de onde estava.
    // (Com "Enter Play Mode Options" desligando o reload de domínio, os valores estáticos sobrevivem entre Plays; por isso o reset aqui.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ZerarAoIniciar()
    {
        LimparTudo(); AoMudar = null; EnviarParaTodos = null;
        PlayerPrefs.DeleteKey(CHAVE);
    }

    // volta tudo ao começo (usado ao sair para a seleção de personagem)
    public static void Reiniciar()
    {
        LimparTudo(); AoMudar = null;
        PlayerPrefs.DeleteKey(CHAVE);
    }

    public static void Carregar()
    {
        if (SessaoDeJogo.Online && !SessaoDeJogo.SouHost) { Atual = Etapa.FalarComCaio; TemMapa = TemMiniMapa = true; }     // convidado: recebe do host
    }

    // etapa + o que já foi feito, num número só (para mandar pela rede)
    static int Codificar(Etapa e) { return (int)e | (CemiterioFeito ? 0x100 : 0) | (CasteloFeito ? 0x200 : 0) | (PantanoFeito ? 0x400 : 0); }

    public static void Ir(Etapa e, bool salvar = true)
    {
        if (e >= Etapa.Cristais) TemMapa = TemMiniMapa = true;         // quem pulou o tutorial (teste) ganha o mapa
        if (e == Atual) return;
        Atual = e; Dica = null;
        if (EnviarParaTodos != null && (!SessaoDeJogo.Online || SessaoDeJogo.SouHost)) EnviarParaTodos(Codificar(e));
        AoMudar?.Invoke(e);
        if (e == Etapa.Cristais) Verificar();
    }

    // um dos três lugares foi vencido
    public static void Concluir(Local l)
    {
        if (Feito(l)) return;
        if (l == Local.Cemiterio) CemiterioFeito = true; else if (l == Local.Castelo) CasteloFeito = true; else PantanoFeito = true;
        if (EnviarParaTodos != null && (!SessaoDeJogo.Online || SessaoDeJogo.SouHost)) EnviarParaTodos(Codificar(Atual));
        AoMudar?.Invoke(Atual);
        Verificar();
    }

    // com os três feitos, a história segue para o dragão (depois da conversa com o Caio no fim)
    static void Verificar()
    {
        if (Atual == Etapa.Cristais && TodosFeitos) Ir(Etapa.Dragao);
    }

    // chamado pela rede no PC dos convidados
    public static void ReceberDoHost(int v)
    {
        CemiterioFeito = (v & 0x100) != 0; CasteloFeito = (v & 0x200) != 0; PantanoFeito = (v & 0x400) != 0;
        Ir((Etapa)Mathf.Clamp(v & 0xFF, 0, (int)Etapa.Fim), false);
    }

    public static void Avancar() { if (Atual < Etapa.Fim) Ir(Atual + 1); }
    public static void Recomecar() { Ir(Etapa.Intro); }

    public static string Capitulo(Etapa e)
    {
        switch (e)
        {
            case Etapa.Intro: return "Prólogo";
            case Etapa.FalarComCaio: return "Capítulo 1 - Uma vila em apuros";
            case Etapa.Cristais: return "Capítulo 2 - Os três lugares esquecidos";
            case Etapa.Dragao: return "Capítulo 3 - O despertar do dragão";
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
            case Etapa.Cristais: return "Visite os três lugares, na ordem que quiser (" + QuantosFeitos + "/3): " + Pendentes() + ".";
            case Etapa.Dragao: return "Enfrente o dragão antes que ele destrua tudo!";
            case Etapa.Recompensa: return "Volte à vila e fale com Caio para receber a recompensa.";
            default: return "A paz voltou a Valverde.";
        }
    }

    static string Pendentes()
    {
        var l = new System.Collections.Generic.List<string>();
        if (!CemiterioFeito) l.Add("o cemitério");
        if (!CasteloFeito) l.Add("o Castelo Destruído");
        if (!PantanoFeito) l.Add("o templo do pântano");
        return l.Count == 0 ? "tudo feito" : string.Join(", ", l.ToArray());
    }

    // texto mais longo da missão atual (mostrado ao segurar V)
    public static string Resumo(Etapa e)
    {
        switch (e)
        {
            case Etapa.FalarComCaio: return "Valverde está ameaçada por mortos-vivos que saem de três lugares abandonados, onde antigos cristais foram selados. Caio, o ancião, está na praça da vila e sabe o que fazer.";
            case Etapa.Cristais: return "Os cristais estão selados em três lugares: o cemitério (Guardião das Lápides), o Castelo Destruído (Cavaleiro Antigo e o ladrão) e o templo do pântano (Devorador do Lago). Vá na ordem que preferir: faltam " + Pendentes() + ". Use o mapa da ilha para se orientar.";
            case Etapa.Dragao: return "Dois cristais estão com vocês, mas o terceiro ficou com o ladrão. Com ele, o dragão que destruiu as vilas de outrora está acordando. É a batalha final.";
            case Etapa.Recompensa: return "A ameaça acabou. Caio espera na vila para entregar a recompensa prometida: a escritura do cabaré.";
            default: return "";
        }
    }

    // lista de passos da história: texto e se já foi cumprido
    public static void Tarefas(Etapa atual, System.Collections.Generic.List<string> textos, System.Collections.Generic.List<bool> feitas)
    {
        textos.Clear(); feitas.Clear();
        textos.Add("Falar com Caio, o ancião"); feitas.Add((int)atual > (int)Etapa.FalarComCaio);
        textos.Add("Cemitério: o Guardião das Lápides"); feitas.Add(CemiterioFeito);
        textos.Add("Castelo: o Cavaleiro Antigo e o ladrão"); feitas.Add(CasteloFeito);
        textos.Add("Pântano: o Devorador do Lago"); feitas.Add(PantanoFeito);
        textos.Add("Derrotar o dragão"); feitas.Add((int)atual > (int)Etapa.Dragao);
        textos.Add("Receber a recompensa"); feitas.Add((int)atual > (int)Etapa.Recompensa);
    }
}
