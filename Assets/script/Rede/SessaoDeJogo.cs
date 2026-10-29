// Informações da partida atual, acessíveis de qualquer script
// (funciona mesmo sem o pacote de multiplayer instalado).
public static class SessaoDeJogo
{
    public static bool Online;            // true quando está numa partida em rede
    public static bool SouHost;           // true no PC que criou a partida
    public static int PersonagemEscolhido;
    public static bool MenuAberto;        // menu inicial na tela (cursor livre, sem controle)
    public static string MensagemAoVoltar;

    // preenchidos pela sala de espera no PC do host (o resto do jogo usa sem precisar saber de rede)
    public static System.Action<string, float> AvisarTodos;              // mensagem na tela de todos os jogadores
    public static System.Action<UnityEngine.GameObject> SpawnarNaRede;   // faz um monstro aparecer para todos
}
