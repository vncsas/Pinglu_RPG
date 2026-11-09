using System;
using System.Collections.Generic;
using UnityEngine;

// Ataques grandes (dos chefes) avisam aqui "vou bater neste lugar daqui a pouco".
// Os companheiros CPU leem esta lista para fugir ou rolar na hora certa.
public static class PerigoNoChao
{
    public class Perigo
    {
        public Func<Vector3> centro;
        public float raio;          // golpe: raio da área / anel: raio máximo da onda
        public float impacto;       // Time.time em que o golpe acerta (ou em que a onda nasce)
        public bool anel;           // onda de choque que se espalha
        public bool linha;          // raio em linha reta: "centro" é a origem, "direcao" para onde vai; raio = meia largura
        public Func<Vector3> direcao; public float comprimento;
        public float velocidade;    // velocidade do anel (m/s)
        public float expira, inicio;
    }

    public static readonly List<Perigo> Ativos = new List<Perigo>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { Ativos.Clear(); }

    public static void Golpe(Func<Vector3> centro, float raio, float emSegundos)
    {
        Limpar();
        Ativos.Add(new Perigo { centro = centro, raio = raio, impacto = Time.time + emSegundos, inicio = Time.time, expira = Time.time + emSegundos + 0.4f });
    }

    public static void Anel(Func<Vector3> centro, float raioMax, float emSegundos, float velocidade)
    {
        Limpar();
        Ativos.Add(new Perigo { centro = centro, raio = raioMax, impacto = Time.time + emSegundos, inicio = Time.time, anel = true, velocidade = velocidade,
            expira = Time.time + emSegundos + raioMax / Mathf.Max(1f, velocidade) + 0.4f });
    }

    // raio em linha reta (começa daqui a "inicioEm" segundos e dura "duracao")
    public static void Linha(Func<Vector3> origem, Func<Vector3> direcao, float comprimento, float largura, float inicioEm, float duracao)
    {
        Limpar();
        Ativos.Add(new Perigo { centro = origem, direcao = direcao, linha = true, comprimento = comprimento, raio = largura * 0.5f,
            inicio = Time.time + inicioEm, impacto = Time.time + inicioEm, expira = Time.time + inicioEm + duracao });
    }

    public static bool Direcao(Perigo p, out Vector3 d)
    {
        d = Vector3.forward;
        if (p.direcao == null) return false;
        try { d = p.direcao(); d.y = 0f; if (d.sqrMagnitude < 0.0001f) return false; d.Normalize(); return true; } catch (System.Exception) { p.expira = 0f; return false; }
    }

    // o centro pode apontar para um chefe que já foi destruído: nesse caso o perigo é ignorado
    public static bool Centro(Perigo p, out Vector3 c)
    {
        c = Vector3.zero;
        if (Time.time > p.expira || p.centro == null) return false;
        try { c = p.centro(); return true; } catch (System.Exception) { p.expira = 0f; return false; }
    }

    static void Limpar() { Ativos.RemoveAll(p => Time.time > p.expira); }
}
