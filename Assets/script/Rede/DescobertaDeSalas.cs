using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Salas na rede local sem digitar IP:
//  - quem cria a sala ANUNCIA uma vez por segundo "existe a sala X aqui"
//  - quem procura ESCUTA esses anúncios e monta a lista de salas
// Funciona em computadores no mesmo Wi-Fi/roteador (porta UDP 47777).
public class DescobertaDeSalas : MonoBehaviour
{
    public const int PORTA_ANUNCIOS = 47777;
    private const string ASSINATURA = "PINGLU1";

    public class Sala
    {
        public string nome, ip;
        public int porta, jogadores, maximo;
        public bool temSenha, emJogo;
        public float vistaEm;
    }

    public readonly List<Sala> Salas = new List<Sala>();
    public string Problema { get; private set; }

    private UdpClient ouvinte, anunciante;
    private Func<string> mensagem;
    private float proximoAnuncio;

    // ------------------------------------------------------------------
    //  procurar salas
    // ------------------------------------------------------------------
    public void Ouvir()
    {
        if (ouvinte != null) return;
        try
        {
            ouvinte = new UdpClient();
            ouvinte.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            ouvinte.EnableBroadcast = true;
            ouvinte.Client.Bind(new IPEndPoint(IPAddress.Any, PORTA_ANUNCIOS));
            Problema = null;
        }
        catch (Exception e)
        {
            Problema = "Não consegui procurar salas (" + e.Message + ").";
            ouvinte = null;
        }
    }

    public void PararDeOuvir()
    {
        if (ouvinte != null) { try { ouvinte.Close(); } catch { } }
        ouvinte = null;
        Salas.Clear();
    }

    // ------------------------------------------------------------------
    //  anunciar a sala criada (a mensagem é montada na hora: jogadores, se já começou...)
    // ------------------------------------------------------------------
    public static string Montar(string nome, int jogadores, int maximo, bool temSenha, bool emJogo, int porta)
    {
        nome = (nome ?? "Sala").Replace("|", "/");
        return ASSINATURA + "|" + nome + "|" + jogadores + "|" + maximo + "|" + (temSenha ? 1 : 0) + "|" + (emJogo ? 1 : 0) + "|" + porta;
    }

    public void Anunciar(Func<string> montarMensagem)
    {
        mensagem = montarMensagem;
        try
        {
            if (anunciante == null) { anunciante = new UdpClient(); anunciante.EnableBroadcast = true; }
            proximoAnuncio = 0f;
        }
        catch (Exception e) { Problema = "Não consegui anunciar a sala (" + e.Message + ")."; }
    }

    public void PararDeAnunciar()
    {
        mensagem = null;
        if (anunciante != null) { try { anunciante.Close(); } catch { } }
        anunciante = null;
    }

    private void Update()
    {
        if (anunciante != null && mensagem != null && Time.unscaledTime >= proximoAnuncio)
        {
            proximoAnuncio = Time.unscaledTime + 1f;
            byte[] b = Encoding.UTF8.GetBytes(mensagem());
            try { anunciante.Send(b, b.Length, new IPEndPoint(IPAddress.Broadcast, PORTA_ANUNCIOS)); } catch { }
            try { anunciante.Send(b, b.Length, new IPEndPoint(IPAddress.Loopback, PORTA_ANUNCIOS)); } catch { } // mesmo PC (testes)
        }

        if (ouvinte != null)
        {
            try
            {
                while (ouvinte.Available > 0)
                {
                    IPEndPoint de = new IPEndPoint(IPAddress.Any, 0);
                    byte[] b = ouvinte.Receive(ref de);
                    Ler(Encoding.UTF8.GetString(b), de.Address.ToString());
                }
            }
            catch (Exception e) { Problema = "Erro ao procurar salas (" + e.Message + ")."; }
            Salas.RemoveAll(s => Time.unscaledTime - s.vistaEm > 4f); // sala sumiu
        }
    }

    private void Ler(string texto, string ip)
    {
        string[] p = texto.Split('|');
        if (p.Length < 7 || p[0] != ASSINATURA) return;
        int jog, max, porta;
        if (!int.TryParse(p[2], out jog) || !int.TryParse(p[3], out max) || !int.TryParse(p[6], out porta)) return;
        // anúncio vindo do próprio PC pelo "loopback": mostra como 127.0.0.1
        Sala s = Salas.Find(x => x.porta == porta && (x.ip == ip || (ip == "127.0.0.1" || x.ip == "127.0.0.1") && x.nome == p[1]));
        if (s == null) { s = new Sala(); Salas.Add(s); s.ip = ip; }
        if (ip != "127.0.0.1") s.ip = ip; // prefere o IP real da rede
        s.nome = p[1]; s.jogadores = jog; s.maximo = max; s.porta = porta;
        s.temSenha = p[4] == "1"; s.emJogo = p[5] == "1";
        s.vistaEm = Time.unscaledTime;
    }

    private void OnDestroy()
    {
        PararDeOuvir();
        PararDeAnunciar();
    }
}
