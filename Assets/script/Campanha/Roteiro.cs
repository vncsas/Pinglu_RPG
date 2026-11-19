using System;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Collections.Generic;

// Todas as falas da história. Para mudar um texto, é só editar aqui (as imagens e o 3D não dependem disso).
// como o personagem está se sentindo: muda o formato do balão de fala. Auto = deduz pelo texto ("?!" vira surpresa, "..." vira pensativo)
public enum Emocao { Auto, Normal, Surpresa, Raiva, Alegre, Triste, Pensando, Medo }

public class Fala
{
    public string quem;
    readonly string grupo, solo; readonly Emocao emo;
    public Emocao emocao
    {
        get
        {
            if (emo != Emocao.Auto) return emo;
            string t = texto;
            if (t.Contains("?!") || t.Contains("!?")) return Emocao.Surpresa;
            if (t.StartsWith("...") || t.EndsWith("...")) return Emocao.Pensando;
            return Emocao.Normal;
        }
    }
    // texto de acordo com o modo: "grupo" quando há mais de um jogador (vocês), "solo" quando joga sozinho (você)
    public string texto { get { return solo != null && Roteiro.Solo ? solo : grupo; } }
    public Fala(string quem, string grupo, string solo = null, Emocao emocao = Emocao.Auto) { this.quem = quem; this.grupo = grupo; this.solo = solo; this.emo = emocao; }
}

public static class Roteiro
{
    // true quando só há UM jogador humano na partida (os companheiros CPU não contam): a história usa "você" em vez de "vocês"
    static bool solo = true; static float proximaConta = -1f;
    public static bool Solo
    {
        get
        {
            if (!SessaoDeJogo.Online) return true;
            if (Time.unscaledTime >= proximaConta)
            {
                proximaConta = Time.unscaledTime + 1f;
                int n = 0;
                foreach (PlayerLocomotion p in Object.FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (!p.Companheiro && p.gameObject.activeInHierarchy) n++;
                solo = n <= 1;
            }
            return solo;
        }
    }

    public const string NOME_DA_VILA = "Valverde";
    public const string VOCE = "Aventureiro";

    // cutscene inicial (narração sobre tela preta)
    static readonly string[] AberturaGrupo =
    {
        "Ao redor da vila de " + NOME_DA_VILA + ", três lugares antigos foram esquecidos pelo tempo: um cemitério, um castelo em ruínas e um templo afundado no pântano.",
        "Mas, de uma hora para a outra, os mortos acordaram. Esqueletos e corpos mumificados deixaram suas tumbas e atacam todos que encontram pela frente.",
        "Sem ter a quem recorrer, a vila se uniu e prometeu uma grande recompensa a quem destruir a ameaça e trazer a paz de volta.",
        "E é aí que chegam alguns aventureiros... sem um tostão no bolso."
    };

    static readonly string[] AberturaSolo =
    {
        AberturaGrupo[0], AberturaGrupo[1], AberturaGrupo[2],
        "E é aí que chega um aventureiro... sem um tostão no bolso."
    };
    public static string[] Abertura { get { return Solo ? AberturaSolo : AberturaGrupo; } }

    // fala do Caio quando o jogador conversa com ele, de acordo com o ponto da história
    public static Fala[] Caio(Etapa e, out Action aoTerminar)
    {
        aoTerminar = null;
        switch (e)
        {
            case Etapa.FalarComCaio:
                aoTerminar = () => TutorialDoMapa.Iniciar();          // entrega o mapa da ilha, explica o mapa e o mini-mapa; depois a história segue
                return new[]
                {
                    new Fala("Caio", "Ora, ora... aventureiros! Finalmente alguém aparece. Eu sou o Caio, ancião de " + NOME_DA_VILA + ". E, olha, também coordeno... digo, aconselho todo mundo por aqui.", "Ora, ora... um aventureiro! Finalmente alguém aparece. Eu sou o Caio, ancião de " + NOME_DA_VILA + ". E, olha, também coordeno... digo, aconselho todo mundo por aqui.", emocao: Emocao.Alegre),
                    new Fala(VOCE, "Ouvimos falar de uma recompensa. Estamos... digamos... sem um tostão.", "Ouvi falar de uma recompensa. Estou... digamos... sem um tostão.", emocao: Emocao.Pensando),
                    new Fala("Caio", "A recompensa é grande, sim! A vila inteira se uniu para pagá-la. Mas primeiro vocês precisam entender o que está acontecendo.", "A recompensa é grande, sim! A vila inteira se uniu para pagá-la. Mas primeiro você precisa entender o que está acontecendo.", emocao: Emocao.Alegre),
                    new Fala("Caio", "Em volta de " + NOME_DA_VILA + " existem três lugares antigos: o cemitério, o castelo caído e o templo do pântano. Em cada um deles, um cristal de enorme poder mágico foi selado por um herói de muito tempo atrás."),
                    new Fala("Caio", "Ele não queria que ninguém pusesse as mãos naquele poder. Só que, agora, uma magia maligna que eu não conheço está corrompendo os cristais... e os mortos estão se levantando por causa deles."),
                    new Fala(VOCE, "Então é só pegar os cristais de volta."),
                    new Fala("Caio", "Exatamente. E não importa por qual lugar vocês começam: cemitério, castelo ou pântano, a ordem é de vocês. Só voltem inteiros!", "Exatamente. E não importa por qual lugar você começa: cemitério, castelo ou pântano, a ordem é sua. Só volte inteiro!"),
                    new Fala("Caio", "Ah, e antes que se percam por aí: tomem isto. É o mapa da ilha. Está um pouco amassado, mas ainda funciona.", "Ah, e antes que se perca por aí: tome isto. É o mapa da ilha. Está um pouco amassado, mas ainda funciona."),
                    new Fala(VOCE, "Um mapa! Isso ajuda muito.", emocao: Emocao.Alegre),
                    new Fala("Caio", "Deixe que eu mostro como se usa."),
                };
            case Etapa.Cristais:
            {
                var l = new List<Fala>();
                if (Campanha.QuantosFeitos == 0)
                    l.Add(new Fala("Caio", "Cemitério, castelo caído e templo do pântano. Vocês escolhem a ordem, mas não demorem: os mortos estão cada vez mais agitados. Se esquecerem o caminho, abram o mapa com [" + Comandos.Dica(Acao.Mapa) + "].", "Cemitério, castelo caído e templo do pântano. Você escolhe a ordem, mas não demore: os mortos estão cada vez mais agitados. Se esquecer o caminho, abra o mapa com [" + Comandos.Dica(Acao.Mapa) + "]."));
                else
                {
                    var feitos = new List<string>(); var faltam = new List<string>();
                    if (Campanha.CemiterioFeito) feitos.Add("o cemitério"); else faltam.Add("o cemitério, onde o Guardião das Lápides vigia");
                    if (Campanha.CasteloFeito) feitos.Add("o castelo"); else faltam.Add("o castelo caído, onde um velho cavaleiro ainda faz a ronda");
                    if (Campanha.PantanoFeito) feitos.Add("o pântano"); else faltam.Add("o templo do pântano, onde algo vive debaixo da água");
                    l.Add(new Fala("Caio", "Já cuidaram d" + string.Join(" e d", feitos.ToArray()) + ". Faltam: " + string.Join("; ", faltam.ToArray()) + ".", "Já cuidou d" + string.Join(" e d", feitos.ToArray()) + ". Faltam: " + string.Join("; ", faltam.ToArray()) + "."));
                }
                return l.ToArray();
            }
            case Etapa.Dragao:
                return new[]
                {
                    new Fala("Caio", "Eu senti o chão tremer daqui. O dragão que destruiu as vilas de outrora está acordando... e o ladrão ainda tem o terceiro cristal!", emocao: Emocao.Medo),
                    new Fala("Caio", "Vão, rápido! Vocês têm dois cristais e coragem de sobra. Detenham-no!", "Vá, rápido! Você tem dois cristais e coragem de sobra. Detenha-o!"),
                };
            case Etapa.Recompensa:
                aoTerminar = () => Campanha.Ir(Etapa.Fim);
                return new[]
                {
                    new Fala("Caio", "Vocês conseguiram! " + NOME_DA_VILA + " está salva, e a vila inteira agradece.", "Você conseguiu! " + NOME_DA_VILA + " está salva, e a vila inteira agradece."),
                    new Fala("Caio", "A recompensa prometida: a escritura do novo cabaré de " + NOME_DA_VILA + ". Agora ele é de vocês!", "A recompensa prometida: a escritura do novo cabaré de " + NOME_DA_VILA + ". Agora ele é seu!"),
                    new Fala(VOCE, "Um cabaré?! ...Aceitamos.", "Um cabaré?! ...Aceito."),
                    new Fala("Caio", "Gastem com responsabilidade. Bem-vindos à família de " + NOME_DA_VILA + "!", "Gaste com responsabilidade. Boas-vindas à família de " + NOME_DA_VILA + "!"),
                };
            default:
                return new[] { new Fala("Caio", "A paz voltou a " + NOME_DA_VILA + ". Passem no cabaré quando quiserem!", "A paz voltou a " + NOME_DA_VILA + ". Passe no cabaré quando quiser!") };
        }
    }

    // os três lugares foram vencidos: dois cristais com o grupo, o terceiro com o vilão
    public static readonly Fala[] TresLugaresVencidos =
    {
        new Fala(VOCE, "Dois cristais estão conosco... mas o do cemitério ficou com o ladrão.", "Dois cristais estão comigo... mas o do cemitério ficou com o ladrão.", emocao: Emocao.Pensando),
        new Fala(VOCE, "Os cristais brilham, quentes. Alguma coisa muito grande se mexe lá longe. O chão está tremendo...", emocao: Emocao.Medo),
        new Fala("???", "Os selos se foram. O poder dos três cristais vai me despertar. Eu fui o terror das vilas... e serei de novo!", emocao: Emocao.Raiva),
        new Fala(VOCE, "O dragão! É ele que o ladrão queria acordar. Precisamos detê-lo agora!", "O dragão! É ele que o ladrão queria acordar. Preciso detê-lo agora!", emocao: Emocao.Surpresa),
    };

    // cenas da história que acontecem nos lugares (usadas quando os locais forem ligados)
    public static readonly Fala[] BilheteDoCemiterio =
    {
        new Fala(VOCE, "O cristal não está aqui... já foi levado.", emocao: Emocao.Triste),
        new Fala(VOCE, "Tem um bilhete preso na lápide: \"Vocês não vão conseguir recuperar os cristais. Ele vai acordar e destruir a vila.\"", "Tem um bilhete preso na lápide: \"Você não vai conseguir recuperar os cristais. Ele vai acordar e destruir a vila.\""),
        new Fala(VOCE, "\"Ele\"? Quem é \"ele\"? Melhor levar isso ao Caio... mas antes, vamos atrás de quem escreveu.", "\"Ele\"? Quem é \"ele\"? Melhor levar isso ao Caio... mas antes, vou atrás de quem escreveu."),
    };

    public static readonly Fala[] CristalDoCemiterio =
    {
        new Fala(VOCE, "Era isso que o Guardião protegia: o primeiro cristal! Ele brilha como se estivesse vivo.", emocao: Emocao.Surpresa),
        new Fala(VOCE, "Tem um bilhete preso na base dele: \"Vocês não vão conseguir recuperar todos os cristais. Ele vai acordar e destruir a vila.\"", "Tem um bilhete preso na base dele: \"Você não vai conseguir recuperar todos os cristais. Ele vai acordar e destruir a vila.\""),
        new Fala(VOCE, "\"Ele\"? Quem é \"ele\"? Melhor guardar o cristal e ir atrás de quem escreveu isso... e dos outros dois cristais."),
    };

    // o ladrão aparece quando tentamos pegar o cristal do cemitério
    public static Fala[] LadraoAgradece { get { if (!Campanha.CasteloFeito) return LadraoAgradeceBase; var l = new List<Fala>(LadraoAgradeceBase); l.RemoveAt(l.Count - 1); l.Add(new Fala("Ladrão", "Já nos conhecemos, não é? No castelo vocês me derrotaram... mas, desta vez, quem saiu ganhando fui eu.", "Já nos conhecemos, não é? No castelo você me derrotou... mas, desta vez, quem saiu ganhando fui eu.")); return l.ToArray(); } }
    static readonly Fala[] LadraoAgradeceBase =
    {
        new Fala("Ladrão", "Hah... hah hah hah! Muito obrigado, aventureiros. De coração.", "Hah... hah hah hah! Muito obrigado, aventureiro. De coração.", emocao: Emocao.Alegre),
        new Fala("Ladrão", "Esse Guardião vigiava o cemitério há séculos. Eu tentei derrubá-lo durante semanas e não passei nem da primeira lápide."),
        new Fala(VOCE, "Você ficou nos observando esse tempo todo?!", "Você ficou me observando esse tempo todo?!"),
        new Fala("Ladrão", "Cada golpe. Vocês me pouparam um trabalhão enorme. E o melhor: eu nem precisei sujar as mãos.", "Cada golpe. Você me poupou um trabalhão enorme. E o melhor: eu nem precisei sujar as mãos."),
        new Fala(VOCE, "Devolva o cristal! Ele não é seu!", emocao: Emocao.Raiva),
        new Fala("Ladrão", "Meu? Ah, não. Eu só carrego. Quem precisa dele está dormindo há muito tempo... e está quase acordando.", emocao: Emocao.Alegre),
        new Fala("Ladrão", "Os selos já estão cedendo. Quando o último cair, nem todos vocês juntos vão conseguir impedir.", "Os selos já estão cedendo. Quando o último cair, nem você vai conseguir impedir."),
        new Fala("Ladrão", "Me procurem por aí, se tiverem coragem. Eu também gosto de visitas.", "Me procure por aí, se tiver coragem. Eu também gosto de visitas."),
    };

    // depois que ele some
    public static readonly Fala[] RouboDoCristal =
    {
        new Fala(VOCE, "Ele sumiu na fumaça... e levou o cristal.", emocao: Emocao.Triste),
        new Fala(VOCE, "Tem um bilhete cravado no chão, onde ele estava: \"Vocês não vão conseguir recuperar todos os cristais. Ele vai acordar e destruir a vila.\"", "Tem um bilhete cravado no chão, onde ele estava: \"Você não vai conseguir recuperar todos os cristais. Ele vai acordar e destruir a vila.\""),
        new Fala(VOCE, "\"Ele\"? Quem é \"ele\"? Precisamos encontrar esse ladrão e os outros cristais antes que seja tarde.", "\"Ele\"? Quem é \"ele\"? Preciso encontrar esse ladrão e os outros cristais antes que seja tarde."),
    };

    // o Cavaleiro Antigo cai e o Salão do Trono se revela, com o ladrão lá no fundo
    public static readonly Fala[] SalaoSeAbre =
    {
        new Fala(VOCE, "O Cavaleiro Antigo caiu... Olhem: o portão do Salão do Trono se abriu. Tem alguém lá no fundo.", "O Cavaleiro Antigo caiu... Olhe: o portão do Salão do Trono se abriu. Tem alguém lá no fundo.", emocao: Emocao.Surpresa),
        new Fala("Ladrão", "Hah... hah hah! Impressionante, de verdade. Eu achei que ele seguraria vocês por mais tempo.", "Hah... hah hah! Impressionante, de verdade. Eu achei que ele seguraria você por mais tempo.", emocao: Emocao.Alegre),
        new Fala("Ladrão", "Venham, venham, aventureiros. O salão está aberto. Eu espero aqui dentro.", "Venha, venha, aventureiro. O salão está aberto. Eu espero aqui dentro.", emocao: Emocao.Alegre),
        new Fala(VOCE, "É a voz do ladrão do cemitério. Chegou a hora de acertar as contas. Vamos!", "É a voz do ladrão do cemitério. Chegou a hora de acertar as contas. Lá vou eu!"),
    };

    public static Fala[] EncontroComOLadrao { get { return Campanha.CemiterioFeito ? EncontroComOLadraoBase : EncontroSemCemiterio; } }
    static readonly Fala[] EncontroSemCemiterio =
    {
        new Fala("Ladrão", "Hah! Visitas no meu castelinho. Vieram atrás de cristais? Que pena, eu cheguei primeiro.", emocao: Emocao.Alegre),
        new Fala(VOCE, "Quem é você? Entregue o cristal!"),
        new Fala("Ladrão", "Eu sou só quem faz o trabalho sujo. E vão ter que me tirar daqui à força.", "Eu sou só quem faz o trabalho sujo. E vai ter que me tirar daqui à força."),
    };
    static readonly Fala[] EncontroComOLadraoBase =
    {
        new Fala("Ladrão", "Vocês de novo?! Eu já disse que não vão pegar os cristais. Sumam daqui!", "Você de novo?! Eu já disse que não vai pegar os cristais. Suma daqui!", emocao: Emocao.Raiva),
        new Fala(VOCE, "Foi você quem roubou o cristal do cemitério!"),
        new Fala("Ladrão", "E daí? Vão ter que me tirar daqui à força.", "E daí? Vai ter que me tirar daqui à força.", emocao: Emocao.Raiva),
    };

    public static readonly Fala[] LadraoDerrotado =
    {
        new Fala("Ladrão", "Argh... tudo bem, vocês venceram. Mas é tarde demais: eu já quebrei o selo dos três cristais. Está tudo pronto.", "Argh... tudo bem, você venceu. Mas é tarde demais: eu já quebrei o selo dos três cristais. Está tudo pronto.", emocao: Emocao.Raiva),
        new Fala("Ladrão", "Muito tempo atrás, o herói enfrentou um dragão que não conseguiu derrotar. Então dividiu o poder dele em três partes e as selou nos cristais."),
        new Fala("Ladrão", "Agora... o dragão pode renascer.", emocao: Emocao.Alegre),
    };

    public static readonly Fala[] DemonioSurge =
    {
        new Fala("Demônio", "Livre... enfim livre! Mesmo sem todo o meu poder, vou retalhar quem tentou impedir o meu retorno!", emocao: Emocao.Raiva),
    };

    public static readonly Fala[] DemonioDerrotado =
    {
        new Fala(VOCE, "Acabou... Os cristais pararam de brilhar.", emocao: Emocao.Triste),
        new Fala(VOCE, "Hora de voltar a " + NOME_DA_VILA + " e falar com o Caio."),
    };
}
