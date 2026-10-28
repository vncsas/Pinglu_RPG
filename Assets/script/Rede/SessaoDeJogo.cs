// Informações da partida atual, acessíveis de qualquer script
// (funciona mesmo sem o pacote de multiplayer instalado).
public static class SessaoDeJogo
{
    public static bool Online;            // true quando está numa partida em rede
    public static bool SouHost;           // true no PC que criou a partida
    public static int PersonagemEscolhido;
    public static bool MenuAberto;        // menu inicial na tela (cursor livre, sem controle)
    public static string MensagemAoVoltar;
}
