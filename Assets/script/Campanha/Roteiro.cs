using System;
using System.Collections.Generic;

// Todas as falas da história. Para mudar um texto, é só editar aqui (as imagens e o 3D não dependem disso).
public class Fala
{
    public string quem, texto;
    public Fala(string quem, string texto) { this.quem = quem; this.texto = texto; }
}

public static class Roteiro
{
    public const string NOME_DA_VILA = "Valverde";
    public const string VOCE = "Aventureiro";

    // cutscene inicial (narração sobre tela preta)
    public static readonly string[] Abertura =
    {
        "Ao redor da vila de " + NOME_DA_VILA + ", três lugares antigos foram esquecidos pelo tempo: um cemitério, um castelo em ruínas e um templo afundado no pântano.",
        "Mas, de uma hora para a outra, os mortos acordaram. Esqueletos e corpos mumificados deixaram suas tumbas e atacam todos que encontram pela frente.",
        "Sem ter a quem recorrer, a vila se uniu e prometeu uma grande recompensa a quem destruir a ameaça e trazer a paz de volta.",
        "E é aí que chegam alguns aventureiros... sem um tostão no bolso."
    };

    // fala do Caio quando o jogador conversa com ele, de acordo com o ponto da história
    public static Fala[] Caio(Etapa e, out Action aoTerminar)
    {
        aoTerminar = null;
        switch (e)
        {
            case Etapa.FalarComCaio:
                aoTerminar = () => Campanha.Ir(Etapa.Cemiterio);
                return new[]
                {
                    new Fala("Caio", "Ora, ora... aventureiros! Finalmente alguém aparece. Eu sou o Caio, ancião de " + NOME_DA_VILA + ". E, olha, também coordeno... digo, aconselho todo mundo por aqui."),
                    new Fala(VOCE, "Ouvimos falar de uma recompensa. Estamos... digamos... sem um tostão."),
                    new Fala("Caio", "A recompensa é grande, sim! A vila inteira se uniu para pagá-la. Mas primeiro vocês precisam entender o que está acontecendo."),
                    new Fala("Caio", "Em volta de " + NOME_DA_VILA + " existem três lugares antigos: o cemitério, o castelo caído e o templo do pântano. Em cada um deles, um cristal de enorme poder mágico foi selado por um herói de muito tempo atrás."),
                    new Fala("Caio", "Ele não queria que ninguém pusesse as mãos naquele poder. Só que, agora, uma magia maligna que eu não conheço está corrompendo os cristais... e os mortos estão se levantando por causa deles."),
                    new Fala(VOCE, "Então é só pegar os cristais de volta."),
                    new Fala("Caio", "Exatamente. Comecem pelo cemitério, ao oeste. Voltem inteiros... e, de preferência, com os cristais."),
                };
            case Etapa.Cemiterio:
                return new[] { new Fala("Caio", "O cemitério fica a oeste da vila. Os mortos estão agitados por lá. Cuidado com o guardião das lápides!") };
            case Etapa.Castelo:
                return new[] { new Fala("Caio", "Pelo bilhete que vocês acharam, alguém já anda roubando os cristais. O castelo caído é o próximo lugar. Um velho cavaleiro ainda faz a ronda por lá.") };
            case Etapa.Panteano:
                return new[] { new Fala("Caio", "O último cristal está no templo do pântano. O ladrão disse que está tudo pronto... isso não me cheira bem.") };
            case Etapa.Demonio:
                return new[] { new Fala("Caio", "Aquele demônio acabou de renascer, ainda está fraco. É agora ou nunca!") };
            case Etapa.Recompensa:
                aoTerminar = () => Campanha.Ir(Etapa.Fim);
                return new[]
                {
                    new Fala("Caio", "Vocês conseguiram! " + NOME_DA_VILA + " está salva, e a vila inteira agradece."),
                    new Fala("Caio", "A recompensa prometida: a escritura do novo cabaré de " + NOME_DA_VILA + ". Agora ele é de vocês!"),
                    new Fala(VOCE, "Um cabaré?! ...Aceitamos."),
                    new Fala("Caio", "Gastem com responsabilidade. Bem-vindos à família de " + NOME_DA_VILA + "!"),
                };
            default:
                return new[] { new Fala("Caio", "A paz voltou a " + NOME_DA_VILA + ". Passem no cabaré quando quiserem!") };
        }
    }

    // cenas da história que acontecem nos lugares (usadas quando os locais forem ligados)
    public static readonly Fala[] BilheteDoCemiterio =
    {
        new Fala(VOCE, "O cristal não está aqui... já foi levado."),
        new Fala(VOCE, "Tem um bilhete preso na lápide: \"Vocês não vão conseguir recuperar os cristais. Ele vai acordar e destruir a vila.\""),
        new Fala(VOCE, "\"Ele\"? Quem é \"ele\"? Melhor levar isso ao Caio... mas antes, vamos atrás de quem escreveu."),
    };

    public static readonly Fala[] CristalDoCemiterio =
    {
        new Fala(VOCE, "Era isso que o Guardião protegia: o primeiro cristal! Ele brilha como se estivesse vivo."),
        new Fala(VOCE, "Tem um bilhete preso na base dele: \"Vocês não vão conseguir recuperar todos os cristais. Ele vai acordar e destruir a vila.\""),
        new Fala(VOCE, "\"Ele\"? Quem é \"ele\"? Melhor guardar o cristal e ir atrás de quem escreveu isso... e dos outros dois cristais."),
    };

    // o ladrão aparece quando tentamos pegar o cristal do cemitério
    public static readonly Fala[] LadraoAgradece =
    {
        new Fala("Ladrão", "Hah... hah hah hah! Muito obrigado, aventureiros. De coração."),
        new Fala("Ladrão", "Esse Guardião vigiava o cemitério há séculos. Eu tentei derrubá-lo durante semanas e não passei nem da primeira lápide."),
        new Fala(VOCE, "Você ficou nos observando esse tempo todo?!"),
        new Fala("Ladrão", "Cada golpe. Vocês me pouparam um trabalhão enorme. E o melhor: eu nem precisei sujar as mãos."),
        new Fala(VOCE, "Devolva o cristal! Ele não é seu!"),
        new Fala("Ladrão", "Meu? Ah, não. Eu só carrego. Quem precisa dele está dormindo há muito tempo... e está quase acordando."),
        new Fala("Ladrão", "Os selos já estão cedendo. Quando o último cair, nem todos vocês juntos vão conseguir impedir."),
        new Fala("Ladrão", "Me procurem no Castelo Destruído, se tiverem coragem. O Cavaleiro Antigo adora receber visitas."),
    };

    // depois que ele some
    public static readonly Fala[] RouboDoCristal =
    {
        new Fala(VOCE, "Ele sumiu na fumaça... e levou o cristal."),
        new Fala(VOCE, "Tem um bilhete cravado no chão, onde ele estava: \"Vocês não vão conseguir recuperar todos os cristais. Ele vai acordar e destruir a vila.\""),
        new Fala(VOCE, "\"Ele\"? Quem é \"ele\"? Não temos escolha: vamos ao Castelo Destruído atrás desse ladrão antes que seja tarde."),
    };

    // o Cavaleiro Antigo cai e o Salão do Trono se revela, com o ladrão lá no fundo
    public static readonly Fala[] SalaoSeAbre =
    {
        new Fala(VOCE, "O Cavaleiro Antigo caiu... Olhem: o portão do Salão do Trono se abriu. Tem alguém lá no fundo."),
        new Fala("Ladrão", "Hah... hah hah! Impressionante, de verdade. Eu achei que ele seguraria vocês por mais tempo."),
        new Fala("Ladrão", "Venham, venham, aventureiros. O salão está aberto. Eu espero aqui dentro."),
        new Fala(VOCE, "É a voz do ladrão do cemitério. Chegou a hora de acertar as contas. Vamos!"),
    };

    public static readonly Fala[] EncontroComOLadrao =
    {
        new Fala("Ladrão", "Vocês de novo?! Eu já disse que não vão pegar os cristais. Sumam daqui!"),
        new Fala(VOCE, "Foi você quem roubou o cristal do cemitério!"),
        new Fala("Ladrão", "E daí? Vão ter que me tirar daqui à força."),
    };

    public static readonly Fala[] LadraoDerrotado =
    {
        new Fala("Ladrão", "Argh... tudo bem, vocês venceram. Mas é tarde demais: eu já quebrei o selo dos três cristais. Está tudo pronto."),
        new Fala("Ladrão", "Muito tempo atrás, o herói enfrentou um demônio que não conseguiu derrotar. Então dividiu o poder dele em três partes e as selou nos cristais."),
        new Fala("Ladrão", "Agora... ele pode renascer."),
    };

    public static readonly Fala[] DemonioSurge =
    {
        new Fala("Demônio", "Livre... enfim livre! Mesmo sem todo o meu poder, vou retalhar quem tentou impedir o meu retorno!"),
    };

    public static readonly Fala[] DemonioDerrotado =
    {
        new Fala(VOCE, "Acabou... Os cristais pararam de brilhar."),
        new Fala(VOCE, "Hora de voltar a " + NOME_DA_VILA + " e falar com o Caio."),
    };
}
