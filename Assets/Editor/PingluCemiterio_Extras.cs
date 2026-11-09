// =====================================================================
//  PingluCemiterio_Extras.cs  -  Peças variadas do cemitério grande
// ---------------------------------------------------------------------
//  Capela com campanário, alçapões de cripta, chafarizes secos, calvários,
//  jazigos cercados de ferro, bancos, ossários, forcas com lanterna,
//  arcos quebrados, catafalcos, muros de nichos (colunários), obeliscos,
//  urnas em pedestais e caminhos de pedra pelo cemitério.
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public static partial class PingluVila
{
    static Quaternion Gira(float yaw) { return Quaternion.Euler(0f, yaw, 0f); }
    static Vector3 NoChaoCem(Vector3 p) { return new Vector3(p.x, Gy(p.x, p.z), p.z); }

    // ------------------------------------------------------------------
    //  capela com campanário
    // ------------------------------------------------------------------
    static void CemCapela(Malha m, Vector3 pos, float yaw, float e, Transform luzes)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        float W = 7f * e, D = 13f * e, H = 6f * e;
        CemCamadas(m, o + q * new Vector3(0f, -0.1f, 0f), q, W, D, BASE_H, BASE_S, CEM_PEDRA);
        foreach (int s in new[] { -1, 1 })
            Local(m, o, q, new Vector3(s * (W * 0.5f - 0.25f), H * 0.5f + 0.3f, 0f), new Vector3(0.5f, H, D), CEM_TIJOLO);
        Local(m, o, q, new Vector3(0f, H * 0.5f + 0.3f, -D * 0.5f + 0.25f), new Vector3(W, H, 0.5f), CEM_TIJOLO);
        // fachada com porta em arco e rosácea
        float abertura = 2.4f * e, alturaPorta = 3.6f * e, lado = (W - abertura) * 0.5f;
        foreach (int s in new[] { -1, 1 })
            Local(m, o, q, new Vector3(s * (abertura * 0.5f + lado * 0.5f), H * 0.5f + 0.3f, D * 0.5f - 0.25f), new Vector3(lado, H, 0.5f), CEM_TIJOLO);
        Local(m, o, q, new Vector3(0f, (alturaPorta + H) * 0.5f + 0.3f, D * 0.5f - 0.25f), new Vector3(abertura + 0.2f, H - alturaPorta, 0.5f), CEM_TIJOLO);
        Local(m, o, q, new Vector3(0f, alturaPorta * 0.5f + 0.3f, D * 0.5f - 1.0f), new Vector3(abertura, alturaPorta, 0.1f), COURO);
        int nv = 9;
        for (int k = 0; k < nv; k++)
        {
            float th = (k + 0.5f) / nv * Mathf.PI;
            Vector3 c = new Vector3(Mathf.Cos(th) * (abertura * 0.5f + 0.2f), alturaPorta + 0.3f + Mathf.Sin(th) * (abertura * 0.5f + 0.2f), D * 0.5f);
            m.Caixa(o + q * c, new Vector3(0.4f, Mathf.PI * (abertura * 0.5f + 0.2f) / nv * 1.1f, 0.7f), q * Quaternion.Euler(0f, 0f, th * Mathf.Rad2Deg), CEM_PEDRA);
        }
        m.Cilindro(o + q * new Vector3(0f, H - 1.5f * e + 0.3f, D * 0.5f - 0.05f), 1.0f * e, 0.12f, 18, CEM_PEDRA, q * Quaternion.Euler(90f, 0f, 0f));
        m.Cilindro(o + q * new Vector3(0f, H - 1.5f * e + 0.3f, D * 0.5f + 0.03f), 0.72f * e, 0.1f, 18, COURO, q * Quaternion.Euler(90f, 0f, 0f));
        // janelas altas nas laterais e contrafortes
        for (int i = 0; i < 3; i++)
        {
            float z = -D * 0.5f + 2.6f * e + i * (D - 5.2f * e) * 0.5f;
            foreach (int s in new[] { -1, 1 })
            {
                Local(m, o, q, new Vector3(s * (W * 0.5f + 0.02f), 3.2f * e, z), new Vector3(0.22f, 2.3f * e, 1.0f * e), COURO);
                Local(m, o, q, new Vector3(s * (W * 0.5f + 0.1f), 3.2f * e, z - 0.65f * e), new Vector3(0.3f, 2.5f * e, 0.2f), CEM_PEDRA);
                Local(m, o, q, new Vector3(s * (W * 0.5f + 0.1f), 3.2f * e, z + 0.65f * e), new Vector3(0.3f, 2.5f * e, 0.2f), CEM_PEDRA);
                Local(m, o, q, new Vector3(s * (W * 0.5f + 0.35f), H * 0.42f, z + (D - 5.2f * e) * 0.25f), new Vector3(0.7f, H * 0.84f, 0.7f), CEM_TIJOLO);
            }
        }
        // telhado de duas águas, cumeeira e empenas em degraus
        foreach (int s in new[] { -1, 1 })
            m.Caixa(o + q * new Vector3(s * W * 0.25f, H + 0.3f + W * 0.17f, 0f), new Vector3(W * 0.64f, 0.3f, D + 0.9f), q * Quaternion.Euler(0f, 0f, -s * 34f), CEM_TELHA);
        Local(m, o, q, new Vector3(0f, H + 0.3f + W * 0.335f, 0f), new Vector3(0.35f, 0.3f, D + 0.9f), CEM_PEDRA);
        for (int i = 0; i < 5; i++)
            Local(m, o, q, new Vector3(0f, H + 0.55f + i * 0.5f, D * 0.5f - 0.25f), new Vector3(W * (1f - 0.19f * i), 0.5f, 0.5f), CEM_TIJOLO);
        Local(m, o, q, new Vector3(0f, H + 3.3f, D * 0.5f - 0.25f), new Vector3(0.22f, 1.2f, 0.2f), CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, H + 3.55f, D * 0.5f - 0.25f), new Vector3(0.8f, 0.2f, 0.2f), CEM_PEDRA);
        // campanário ao lado
        Vector3 t = new Vector3(-(W * 0.5f + 2.2f * e), 0f, D * 0.5f - 2.2f * e);
        float TH = H + 7f * e;
        CemCamadas(m, o + q * new Vector3(t.x, -0.1f, t.z), q, 3.2f * e, 3.2f * e, BASE_H, BASE_S, CEM_PEDRA);
        Local(m, o, q, new Vector3(t.x, TH * 0.5f + 0.3f, t.z), new Vector3(3.2f * e, TH, 3.2f * e), CEM_TIJOLO);
        CemCamadas(m, o + q * new Vector3(t.x, TH + 0.3f, t.z), q, 3.2f * e, 3.2f * e, CORN_H, CORN_S, CEM_PEDRA);
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Local(m, o, q, new Vector3(t.x + sx * 1.2f * e, TH + 2.0f * e, t.z + sz * 1.2f * e), new Vector3(0.35f, 3.0f * e, 0.35f), CEM_PEDRA);
        m.Cilindro(o + q * new Vector3(t.x, TH + 1.2f * e, t.z), 0.5f * e, 0.8f * e, 10, FERRO, Quaternion.identity);
        m.Piramide(o + q * new Vector3(t.x, TH + 3.5f * e, t.z), 1.9f * e, 3.2f * e, CEM_TELHA);
        Local(m, o, q, new Vector3(t.x, TH + 6.9f * e, t.z), new Vector3(0.2f, 1.0f, 0.2f), FERRO);
        Local(m, o, q, new Vector3(t.x, TH + 7.0f * e, t.z), new Vector3(0.7f, 0.18f, 0.2f), FERRO);
        for (int i = 0; i < 4; i++) CemVelas(m, o + q * new Vector3((i - 1.5f) * 0.8f, 0.2f, D * 0.5f + 1.1f), 3, 0.2f);
        CemLuz(luzes, o + q * new Vector3(0f, 3f * e, 0f), new Color(1f, 0.62f, 0.34f), 14f * e, 2.6f);
    }

    // ------------------------------------------------------------------
    //  alçapão de cripta (portas inclinadas de madeira com ferragens)
    // ------------------------------------------------------------------
    static void CemAlcapao(Malha m, Vector3 pos, float yaw)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        CemCamadas(m, o + q * new Vector3(0f, -0.1f, 0f), q, 3.4f, 3.4f, BASE_H, BASE_S, CEM_PEDRA);
        foreach (int s in new[] { -1, 1 })
        {
            Local(m, o, q, new Vector3(s * 1.6f, 0.45f, 0f), new Vector3(0.3f, 0.9f, 2.6f), CEM_PEDRA);
            m.Caixa(o + q * new Vector3(s * 0.62f, 0.62f, 0.1f), new Vector3(1.15f, 0.12f, 2.5f), q * Quaternion.Euler(-14f, 0f, s * -6f), TABUAS);
            for (int k = 0; k < 3; k++)
                m.Caixa(o + q * new Vector3(s * 0.62f, 0.72f, -0.7f + k * 0.8f), new Vector3(1.2f, 0.05f, 0.1f), q * Quaternion.Euler(-14f, 0f, s * -6f), FERRO);
            m.Cilindro(o + q * new Vector3(s * 0.25f, 0.78f, 0.7f), 0.12f, 0.05f, 10, FERRO, q * Quaternion.Euler(90f, 0f, 0f));
        }
        Local(m, o, q, new Vector3(0f, 0.3f, -1.2f), new Vector3(3.2f, 0.6f, 0.4f), CEM_PEDRA);
        CemVelas(m, o + q * new Vector3(1.9f, 0.0f, 1.3f), 4, 0.2f);
    }

    // ------------------------------------------------------------------
    //  chafariz seco: bacia, coluna e taça
    // ------------------------------------------------------------------
    static void CemChafariz(Malha m, Vector3 pos)
    {
        Vector3 o = NoChaoCem(pos);
        m.Cilindro(o + new Vector3(0f, -0.2f, 0f), 3.6f, 0.45f, 28, CEM_PEDRA, Quaternion.identity);
        for (int i = 0; i < 20; i++)
        {
            float a = i * Mathf.PI * 2f / 20f;
            Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            m.Caixa(o + d * 3.0f + Vector3.up * 0.55f, new Vector3(2f * Mathf.PI * 3.0f / 20f * 1.1f, 0.9f, 0.5f), Quaternion.LookRotation(d), CEM_LAPIDE);
            m.Caixa(o + d * 3.0f + Vector3.up * 1.05f, new Vector3(2f * Mathf.PI * 3.0f / 20f * 1.12f, 0.12f, 0.7f), Quaternion.LookRotation(d), CEM_PEDRA);
        }
        m.Cilindro(o + new Vector3(0f, 0.2f, 0f), 2.7f, 0.05f, 24, AGUA, Quaternion.identity);
        m.Torno(o + new Vector3(0f, 0.2f, 0f), new[] { 0.9f, 0.6f, 0.4f, 0.35f, 0.5f, 0.9f, 1.5f, 1.4f, 1.1f }, new[] { 0f, 0.3f, 0.6f, 1.3f, 1.7f, 2.0f, 2.2f, 2.35f, 2.3f }, 14, CEM_PEDRA, Quaternion.identity);
        m.Torno(o + new Vector3(0f, 2.3f, 0f), new[] { 0.35f, 0.25f, 0.2f, 0.3f, 0.25f, 0.1f, 0f }, new[] { 0f, 0.3f, 0.6f, 1.0f, 1.3f, 1.6f, 1.9f }, 10, CEM_PEDRA, Quaternion.identity);
    }

    // ------------------------------------------------------------------
    //  calvário: três cruzes sobre degraus
    // ------------------------------------------------------------------
    static void CemCalvario(Malha m, Vector3 pos, float yaw)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        CemCamadas(m, o + q * new Vector3(0f, -0.1f, 0f), q, 6.4f, 2.6f, new[] { 0.3f, 0.3f, 0.3f }, new[] { 0.9f, 0.55f, 0.2f }, CEM_PEDRA);
        float[] altura = { 4.0f, 6.2f, 4.0f };
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 2.2f, h = altura[i];
            Local(m, o, q, new Vector3(x, 0.95f + h * 0.5f, 0f), new Vector3(0.38f, h, 0.3f), CEM_LAPIDE);
            Local(m, o, q, new Vector3(x, 0.95f + h * 0.72f, 0f), new Vector3(h * 0.4f, 0.32f, 0.3f), CEM_LAPIDE);
            Local(m, o, q, new Vector3(x, 0.95f + 0.4f, 0.3f), new Vector3(0.9f, 0.8f, 0.5f), CEM_PEDRA);
        }
        for (int i = 0; i < 4; i++) CemVelas(m, o + q * new Vector3(-2.8f + i * 1.9f, 0.85f, 1.2f), 5, 0.3f);
    }

    // ------------------------------------------------------------------
    //  jazigo cercado de ferro, com túmulo e lápides
    // ------------------------------------------------------------------
    static void CemJazigo(Malha m, Vector3 pos, float yaw)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        float W = 5f, D = 6f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Local(m, o, q, new Vector3(sx * W * 0.5f, 0.9f, sz * D * 0.5f), new Vector3(0.4f, 1.8f, 0.4f), CEM_PEDRA);
                Local(m, o, q, new Vector3(sx * W * 0.5f, 1.95f, sz * D * 0.5f), new Vector3(0.52f, 0.14f, 0.52f), CEM_LAPIDE);
                m.Piramide(o + q * new Vector3(sx * W * 0.5f, 2.02f, sz * D * 0.5f), 0.2f, 0.4f, CEM_PEDRA);
            }
        for (int lado = 0; lado < 4; lado++)
        {
            bool alongX = lado < 2; float sinal = lado % 2 == 0 ? -1f : 1f;
            float comp = alongX ? W : D;
            int barras = Mathf.RoundToInt(comp / 0.3f);
            if (lado == 0) { /* frente tem a entrada: abre um vão no meio */ }
            for (int k = 1; k < barras; k++)
            {
                float t = -comp * 0.5f + k * comp / barras;
                if (lado == 0 && Mathf.Abs(t) < 0.7f) continue;
                Vector3 p = alongX ? new Vector3(t, 0f, sinal * D * 0.5f) : new Vector3(sinal * W * 0.5f, 0f, t);
                m.Caixa(o + q * (p + Vector3.up * 0.7f), new Vector3(0.035f, 1.4f, 0.035f), FERRO);
                m.Piramide(o + q * (p + Vector3.up * 1.4f), 0.03f, 0.13f, FERRO);
            }
            Vector3 c = alongX ? new Vector3(0f, 1.2f, sinal * D * 0.5f) : new Vector3(sinal * W * 0.5f, 1.2f, 0f);
            m.Caixa(o + q * c, alongX ? new Vector3(W, 0.05f, 0.05f) : new Vector3(0.05f, 0.05f, D), q, FERRO);
        }
        Local(m, o, q, new Vector3(0f, 0.35f, 0f), new Vector3(1.4f, 0.7f, 2.6f), CEM_PEDRA);
        CemCamadas(m, o + q * new Vector3(0f, 0.7f, 0f), q, 1.4f, 2.6f, new[] { 0.1f, 0.12f }, new[] { 0.06f, -0.08f }, CEM_LAPIDE);
        foreach (int s in new[] { -1, 1 }) Local(m, o, q, new Vector3(s * 1.5f, 0.6f, -1.7f), new Vector3(0.6f, 1.2f, 0.16f), CEM_LAPIDE);
        CemUrna(m, o + q * new Vector3(0f, 0.9f, 1.0f), 0.6f);
    }

    // ------------------------------------------------------------------
    //  banco de pedra
    // ------------------------------------------------------------------
    static void CemBanco(Malha m, Vector3 pos, float yaw)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        Local(m, o, q, new Vector3(0f, 0.5f, 0f), new Vector3(2.0f, 0.14f, 0.6f), CEM_PEDRA);
        foreach (int s in new[] { -1, 1 })
        {
            Local(m, o, q, new Vector3(s * 0.85f, 0.22f, 0f), new Vector3(0.18f, 0.44f, 0.5f), CEM_LAPIDE);
            Local(m, o, q, new Vector3(s * 0.95f, 0.8f, -0.2f), new Vector3(0.12f, 0.6f, 0.12f), CEM_LAPIDE);
        }
        Local(m, o, q, new Vector3(0f, 0.9f, -0.26f), new Vector3(2.0f, 0.12f, 0.08f), CEM_PEDRA);
    }

    // ------------------------------------------------------------------
    //  ossário: pilha de caveiras e ossos
    // ------------------------------------------------------------------
    static void CemOssario(Malha m, Vector3 pos)
    {
        Vector3 o = NoChaoCem(pos);
        m.Cilindro(o + new Vector3(0f, -0.1f, 0f), 1.6f, 0.2f, 12, MAD_ESC, Quaternion.identity);
        for (int i = 0; i < 22; i++)
        {
            float a = Faixa(0f, Mathf.PI * 2f), r = Faixa(0f, 1.3f) * (1f - i * 0.02f);
            Vector3 c = o + new Vector3(Mathf.Cos(a) * r, 0.1f + (i < 12 ? 0.08f : 0.28f) + (i > 18 ? 0.2f : 0f), Mathf.Sin(a) * r);
            Quaternion rot = Quaternion.Euler(Faixa(-30f, 30f), Faixa(0f, 360f), Faixa(-25f, 25f));
            m.Torno(c, new[] { 0.06f, 0.1f, 0.13f, 0.12f, 0.07f, 0.0f }, new[] { 0f, 0.04f, 0.12f, 0.2f, 0.26f, 0.28f }, 8, REBOCO_CREME, rot);
            m.Caixa(c + rot * new Vector3(0f, -0.02f, 0.1f), new Vector3(0.11f, 0.06f, 0.1f), rot, REBOCO_CREME);
            m.Caixa(c + rot * new Vector3(-0.03f, 0.1f, 0.12f), new Vector3(0.03f, 0.04f, 0.02f), rot, COURO);
            m.Caixa(c + rot * new Vector3(0.03f, 0.1f, 0.12f), new Vector3(0.03f, 0.04f, 0.02f), rot, COURO);
        }
        for (int i = 0; i < 9; i++)
        {
            Quaternion rot = Quaternion.Euler(Faixa(-12f, 12f), Faixa(0f, 360f), Faixa(60f, 100f));
            m.Caixa(o + new Vector3(Faixa(-1.2f, 1.2f), 0.2f + Faixa(0f, 0.2f), Faixa(-1.2f, 1.2f)), new Vector3(0.05f, 0.6f, 0.05f), rot, REBOCO_CREME);
        }
    }

    // ------------------------------------------------------------------
    //  forca de madeira com lanterna (como na referência)
    // ------------------------------------------------------------------
    static void CemForca(Malha m, Vector3 pos, float yaw, Transform luzes, bool acesa)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        CemCamadas(m, o + q * new Vector3(0f, -0.1f, 0f), q, 2.4f, 2.4f, BASE_H, BASE_S, CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 1.2f, 1.4f), CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, 2.5f, 0f), new Vector3(0.28f, 3.8f, 0.28f), MAD_ESC);
        Local(m, o, q, new Vector3(1.0f, 4.4f, 0f), new Vector3(2.3f, 0.22f, 0.24f), MAD_ESC);
        m.Caixa(o + q * new Vector3(0.45f, 3.85f, 0f), new Vector3(0.14f, 1.1f, 0.14f), q * Quaternion.Euler(0f, 0f, -48f), MAD_ESC);
        Local(m, o, q, new Vector3(1.9f, 3.9f, 0f), new Vector3(0.025f, 0.9f, 0.025f), COURO);
        Local(m, o, q, new Vector3(1.9f, 3.3f, 0f), new Vector3(0.26f, 0.38f, 0.26f), LANTERNA);
        Local(m, o, q, new Vector3(1.9f, 3.56f, 0f), new Vector3(0.34f, 0.06f, 0.34f), FERRO);
        m.Piramide(o + q * new Vector3(1.9f, 3.58f, 0f), 0.22f, 0.22f, FERRO);
        if (acesa) CemLuz(luzes, o + q * new Vector3(1.9f, 3.3f, 0f), new Color(1f, 0.62f, 0.3f), 13f, 3f);
    }

    // ------------------------------------------------------------------
    //  arco quebrado de pedra
    // ------------------------------------------------------------------
    static void CemArcoQuebrado(Malha m, Vector3 pos, float yaw)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        float R = 1.8f, alt = 4.2f;
        foreach (int s in new[] { -1, 1 })
        {
            float h = s < 0 ? alt : alt * 0.62f;
            CemCamadas(m, o + q * new Vector3(s * R, -0.1f, 0f), q, 1.0f, 1.0f, BASE_H, BASE_S, CEM_PEDRA);
            Local(m, o, q, new Vector3(s * R, h * 0.5f + 0.3f, 0f), new Vector3(0.9f, h, 0.9f), CEM_TIJOLO);
            if (s < 0) CemCamadas(m, o + q * new Vector3(s * R, h + 0.3f, 0f), q, 0.9f, 0.9f, CORN_H, CORN_S, CEM_PEDRA);
        }
        int n = 11;
        for (int k = 0; k < n; k++)
        {
            float th = (k + 0.5f) / n * Mathf.PI;
            if (th < Mathf.PI * 0.5f) continue;   // só a metade esquerda do arco ficou de pé
            Vector3 c = new Vector3(R * Mathf.Cos(th), alt + 0.7f + R * Mathf.Sin(th), 0f);
            m.Caixa(o + q * c, new Vector3(0.55f, Mathf.PI * R / n * 1.05f, 0.9f), q * Quaternion.Euler(0f, 0f, th * Mathf.Rad2Deg), CEM_PEDRA);
        }
        for (int i = 0; i < 6; i++)
            m.Caixa(o + q * new Vector3(Faixa(0f, 2.8f), 0.2f, Faixa(-0.9f, 0.9f)), new Vector3(Faixa(0.3f, 0.6f), 0.35f, Faixa(0.3f, 0.6f)), Quaternion.Euler(Faixa(-20f, 20f), Faixa(0f, 360f), Faixa(-20f, 20f)), CEM_PEDRA);
    }

    // ------------------------------------------------------------------
    //  catafalco: caixão sobre base de pedra, com velas
    // ------------------------------------------------------------------
    static void CemCatafalco(Malha m, Vector3 pos, float yaw)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        CemCamadas(m, o + q * new Vector3(0f, -0.1f, 0f), q, 1.7f, 3.0f, BASE_H, BASE_S, CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, 0.7f, 0f), new Vector3(1.3f, 0.8f, 2.6f), CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, 1.3f, 0f), new Vector3(0.85f, 0.45f, 2.0f), TABUAS);
        m.Caixa(o + q * new Vector3(0.1f, 1.62f, -0.2f), new Vector3(0.9f, 0.1f, 2.0f), q * Quaternion.Euler(0f, 6f, 4f), MAD_ESC);
        Local(m, o, q, new Vector3(0f, 1.3f, -0.2f), new Vector3(0.9f, 0.05f, 0.08f), FERRO);
        CemVelas(m, o + q * new Vector3(-1.1f, 0.0f, 1.2f), 4, 0.2f);
        CemVelas(m, o + q * new Vector3(1.1f, 0.0f, -1.2f), 4, 0.2f);
    }

    // ------------------------------------------------------------------
    //  colunário: muro de nichos com velas
    // ------------------------------------------------------------------
    static void CemColunario(Malha m, Vector3 pos, float yaw, float comprimento)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        float H = 3.2f;
        CemCamadas(m, o + q * new Vector3(0f, -0.1f, 0f), q, comprimento, 1.6f, BASE_H, BASE_S, CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, H * 0.5f + 0.2f, 0f), new Vector3(comprimento, H, 1.1f), CEM_TIJOLO);
        CemCamadas(m, o + q * new Vector3(0f, H + 0.2f, 0f), q, comprimento, 1.1f, CORN_H, CORN_S, CEM_PEDRA);
        int cols = Mathf.RoundToInt(comprimento / 1.5f);
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < cols; c++)
            {
                float x = -comprimento * 0.5f + (c + 0.5f) * comprimento / cols, y = 0.9f + r * 0.85f;
                Local(m, o, q, new Vector3(x, y, 0.5f), new Vector3(1.1f, 0.6f, 0.2f), CEM_LAPIDE);
                Local(m, o, q, new Vector3(x, y, 0.58f), new Vector3(0.9f, 0.42f, 0.12f), COURO);
                if (rng.NextDouble() < 0.55) CemVelas(m, o + q * new Vector3(x, y - 0.2f, 0.68f), 2, 0.12f);
            }
        for (int i = 0; i <= cols; i += 3)
            Local(m, o, q, new Vector3(-comprimento * 0.5f + i * comprimento / cols, H * 0.5f + 0.2f, 0.6f), new Vector3(0.3f, H, 0.4f), CEM_PEDRA);
    }

    // ------------------------------------------------------------------
    //  obelisco alto e urna em pedestal
    // ------------------------------------------------------------------
    static void CemObeliscoAlto(Malha m, Vector3 pos)
    {
        Vector3 o = NoChaoCem(pos);
        float h0 = CemCamadas(m, o + Vector3.down * 0.1f, Quaternion.identity, 2.2f, 2.2f, new[] { 0.3f, 0.2f, 0.16f }, new[] { 0.6f, 0.4f, 0.25f }, CEM_PEDRA);
        m.Caixa(o + new Vector3(0f, h0 + 0.9f, 0f), new Vector3(1.7f, 1.8f, 1.7f), CEM_LAPIDE);
        m.Tronco(o + new Vector3(0f, h0 + 1.8f, 0f), 0.8f, 0.45f, 6.5f, CEM_LAPIDE);
        m.Piramide(o + new Vector3(0f, h0 + 8.3f, 0f), 0.45f, 0.9f, CEM_PEDRA);
    }

    static void CemUrnaEmPedestal(Malha m, Vector3 pos, float yaw)
    {
        Quaternion q = Gira(yaw); Vector3 o = NoChaoCem(pos);
        float h = CemCamadas(m, o + Vector3.down * 0.05f, q, 0.7f, 0.7f, new[] { 0.2f, 0.9f, 0.14f }, new[] { 0.16f, 0.0f, 0.1f }, CEM_PEDRA);
        CemUrna(m, o + new Vector3(0f, h - 0.05f, 0f), 1.5f);
    }

    // ------------------------------------------------------------------
    //  caminhos de pedra: eixos que ligam a arena às laterais e ao altar
    // ------------------------------------------------------------------
    static void CemCaminhosSecundarios(Malha m)
    {
        float HX = CEM_L * 0.5f, HZ = CEM_P * 0.5f;
        foreach (int s in new[] { -1, 1 })
        {
            float comp = HX - 32f - CEM_PR;
            m.Caixa(new Vector3(s * (CEM_PR + comp * 0.5f), Gy(s * (CEM_PR + comp * 0.5f), 0f) + 0.02f, 0f), new Vector3(comp, 0.12f, 3.6f), CEM_PISO);
            m.Caixa(new Vector3(s * (CEM_PR + comp * 0.5f), Gy(s * (CEM_PR + comp * 0.5f), 0f) + 0.06f, 2.0f), new Vector3(comp, 0.18f, 0.25f), CEM_PEDRA);
            m.Caixa(new Vector3(s * (CEM_PR + comp * 0.5f), Gy(s * (CEM_PR + comp * 0.5f), 0f) + 0.06f, -2.0f), new Vector3(comp, 0.18f, 0.25f), CEM_PEDRA);
        }
        float compZ = HZ - 42f - CEM_PR;
        float zc = -(CEM_PR + compZ * 0.5f);
        m.Caixa(new Vector3(0f, Gy(0f, zc) + 0.02f, zc), new Vector3(4.2f, 0.12f, compZ), CEM_PISO);
    }
}
#endif
