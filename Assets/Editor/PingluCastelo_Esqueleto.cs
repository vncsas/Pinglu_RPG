// =====================================================================
//  PingluCastelo_Esqueleto.cs  -  esqueletos de verdade (costelas, coluna, bacia, crânio, membros)
//  e o REI NO TRONO (esqueleto sentado, de coroa, com a espada entre as mãos).
//  Os ossos são desenhados entre "juntas" (pontos), então qualquer pose é só trocar os pontos.
// =====================================================================
#if UNITY_EDITOR
using UnityEngine;

public static partial class PingluVila
{
    struct CJuntas
    {
        public Vector3 pelve, pescoco, cabeca;
        public Vector3 quadE, quadD, joelhoE, joelhoD, tornE, tornD;
        public Vector3 ombroE, ombroD, cotE, cotD, punhoE, punhoD;
        public Vector3 frente;          // para onde o peito aponta
        public Quaternion rotCabeca;    // orientação do crânio (rosto = +Z)
        public float tamanho;           // 1 = adulto normal
    }

    // osso = cilindro fino entre dois pontos
    static void COsso(Malha m, Vector3 a, Vector3 b, float r, int mat = M_OSSO)
    {
        Vector3 d = b - a; float h = d.magnitude;
        if (h < 0.002f) return;
        m.Cilindro(a, r, h, 6, mat, Quaternion.FromToRotation(Vector3.up, d / h));
    }

    // junta = bolinha (losango) na articulação
    static void CNo(Malha m, Vector3 c, float r, int mat = M_OSSO)
    {
        m.Torno(c - Vector3.up * r, new[] { 0.001f, r, 0.001f }, new[] { 0f, r, 2f * r }, 6, mat, Quaternion.identity, false);
    }

    static void CCranio(Malha m, Vector3 c, Quaternion rot, float t)
    {
        // calota do crânio (elipsoide de revolução), mandíbula, órbitas escuras e nariz
        Vector3 up = rot * Vector3.up, fr = rot * Vector3.forward, dir = rot * Vector3.right;
        float[] raios = { 0.03f, 0.07f, 0.093f, 0.10f, 0.095f, 0.075f, 0.04f, 0.001f };
        float[] alts = { 0f, 0.03f, 0.07f, 0.11f, 0.15f, 0.19f, 0.215f, 0.23f };
        for (int i = 0; i < alts.Length; i++) { alts[i] *= t; raios[i] *= t; }
        m.Torno(c - up * 0.09f * t, raios, alts, 10, M_OSSO, rot, false);
        m.Caixa(c + up * -0.085f * t + fr * 0.045f * t, new Vector3(0.08f, 0.05f, 0.085f) * t, rot, M_OSSO);                    // maxilar
        m.Caixa(c + up * -0.125f * t + fr * 0.035f * t, new Vector3(0.075f, 0.025f, 0.08f) * t, rot * Quaternion.Euler(-4f, 0f, 0f), M_OSSO);   // mandíbula
        foreach (int s in new[] { -1, 1 })
            m.Caixa(c + up * -0.01f * t + fr * 0.085f * t + dir * s * 0.038f * t, new Vector3(0.036f, 0.04f, 0.03f) * t, rot, M_CINZA);   // órbitas
        m.Caixa(c + up * -0.055f * t + fr * 0.092f * t, new Vector3(0.02f, 0.03f, 0.02f) * t, rot, M_CINZA);                      // nariz
        for (int k = -2; k <= 2; k++)   // dentes
            m.Caixa(c + up * -0.103f * t + fr * 0.087f * t + dir * k * 0.013f * t, new Vector3(0.009f, 0.018f, 0.008f) * t, rot, M_OSSO);
    }

    // desenha o esqueleto inteiro a partir das juntas; "partes" sorteia ossos que faltam (0 = completo)
    static void CDesenharEsqueleto(Malha m, CJuntas j, float faltam)
    {
        float t = j.tamanho;
        Vector3 cima = (j.pescoco - j.pelve).normalized;
        Quaternion Q = Quaternion.LookRotation(Vector3.ProjectOnPlane(j.frente, cima).sqrMagnitude < 0.001f ? Vector3.forward : j.frente, cima);
        float L = (j.pescoco - j.pelve).magnitude;
        System.Func<float, float, float, Vector3> T = (x, y, z) => j.pelve + Q * new Vector3(x, y, z);

        // coluna: vértebras
        int nv = 12;
        for (int i = 0; i < nv; i++)
            m.Caixa(T(0f, L * (i + 0.5f) / nv, -0.035f * t - 0.012f * Mathf.Sin(i * 0.5f)), new Vector3(0.042f, L / nv * 0.78f, 0.038f) * t, Q, M_OSSO);
        // bacia
        foreach (int s in new[] { -1, 1 })
        {
            m.Caixa(T(s * 0.085f * t, 0.035f * t, 0f), new Vector3(0.1f, 0.09f, 0.045f) * t, Q * Quaternion.Euler(0f, s * 25f, s * -15f), M_OSSO);
            m.Caixa(T(s * 0.06f * t, -0.03f * t, 0.015f * t), new Vector3(0.05f, 0.07f, 0.04f) * t, Q * Quaternion.Euler(0f, s * 20f, 0f), M_OSSO);
        }
        m.Caixa(T(0f, 0.0f, -0.04f * t), new Vector3(0.07f, 0.1f, 0.03f) * t, Q, M_OSSO);
        // costelas: 7 pares, curvas, algumas quebradas
        float[] larg = { 0.09f, 0.115f, 0.135f, 0.145f, 0.145f, 0.125f, 0.095f };
        float[] prof = { 0.07f, 0.085f, 0.095f, 0.1f, 0.1f, 0.09f, 0.07f };
        for (int k = 0; k < 7; k++)
        {
            float y = L * (0.93f - 0.095f * k);
            foreach (int s in new[] { -1, 1 })
            {
                if (rng.NextDouble() < faltam * 0.5) continue;
                Vector3 ant = T(0f, y, -prof[k] * t);
                int passos = 6; float fim = (rng.NextDouble() < faltam) ? 90f : 165f;
                for (int p = 1; p <= passos; p++)
                {
                    float f0 = fim * (p - 1) / passos * Mathf.Deg2Rad, f1 = fim * p / passos * Mathf.Deg2Rad;
                    Vector3 a = T(s * larg[k] * t * Mathf.Sin(f0), y - 0.035f * t * (f0 / 2.9f), -prof[k] * t * Mathf.Cos(f0));
                    Vector3 b = T(s * larg[k] * t * Mathf.Sin(f1), y - 0.035f * t * (f1 / 2.9f), -prof[k] * t * Mathf.Cos(f1));
                    COsso(m, a, b, 0.0085f * t);
                }
            }
        }
        m.Caixa(T(0f, L * 0.72f, prof[3] * t + 0.005f), new Vector3(0.04f, L * 0.34f, 0.014f), Q, M_OSSO);   // esterno
        // clavículas e ombros
        COsso(m, T(0f, L * 0.97f, 0f), j.ombroE, 0.011f * t); COsso(m, T(0f, L * 0.97f, 0f), j.ombroD, 0.011f * t);
        // pescoço e crânio
        COsso(m, j.pescoco, j.pescoco + (j.cabeca - j.pescoco) * 0.55f, 0.016f * t);
        if (faltam < 0.99f) CCranio(m, j.cabeca, j.rotCabeca, t);
        // braços (úmero, rádio/ulna), pernas (fêmur, tíbia), mãos e pés
        System.Action<Vector3, Vector3, float, bool> membro = (a, b, r, no) => { COsso(m, a, b, r); if (no) CNo(m, b, r * 1.5f); };
        Vector3[] ombro = { j.ombroE, j.ombroD }, cot = { j.cotE, j.cotD }, pun = { j.punhoE, j.punhoD };
        Vector3[] quad = { j.quadE, j.quadD }, joe = { j.joelhoE, j.joelhoD }, tor = { j.tornE, j.tornD };
        for (int i = 0; i < 2; i++)
        {
            CNo(m, ombro[i], 0.02f * t);
            membro(ombro[i], cot[i], 0.014f * t, true);
            Vector3 lado = Vector3.Cross(cot[i] - pun[i], cima).normalized * 0.01f * t;
            COsso(m, cot[i] + lado, pun[i], 0.008f * t); COsso(m, cot[i] - lado, pun[i], 0.008f * t);
            m.Caixa(pun[i] + (pun[i] - cot[i]).normalized * 0.05f * t, new Vector3(0.05f, 0.025f, 0.09f) * t, Quaternion.LookRotation((pun[i] - cot[i]).normalized, Vector3.up), M_OSSO);   // mão
            CNo(m, quad[i], 0.022f * t);
            membro(quad[i], joe[i], 0.019f * t, true);
            Vector3 l2 = Vector3.Cross(joe[i] - tor[i], Vector3.up).normalized * 0.012f * t;
            COsso(m, joe[i] + l2, tor[i], 0.013f * t); COsso(m, joe[i] - l2 * 0.5f, tor[i], 0.009f * t);
            Vector3 fr = Vector3.ProjectOnPlane(j.frente, Vector3.up); if (fr.sqrMagnitude < 0.01f) fr = Vector3.forward;
            m.Caixa(tor[i] + fr.normalized * 0.07f * t + Vector3.down * 0.015f, new Vector3(0.075f, 0.035f, 0.19f) * t, Quaternion.LookRotation(fr.normalized, Vector3.up), M_OSSO);   // pé
        }
    }

    // ------------------------------------------------------------------
    //  esqueleto caído no chão (cada chamada gera uma pose diferente)
    // ------------------------------------------------------------------
    static void CEsqueleto(Malha m, Vector3 p, float yaw)
    {
        float gy = Cg(p.x, p.z);
        float t = Faixa(0.93f, 1.08f);
        int estilo = rng.Next(4);                  // 0 de costas, 1 de bruços, 2 de lado, 3 dobrado
        float roll = estilo == 2 ? (rng.Next(2) * 2 - 1) * 90f : 0f;
        Quaternion R = Quaternion.Euler(0f, yaw, 0f) * Quaternion.AngleAxis(estilo == 1 ? 90f : -90f, Vector3.right) * Quaternion.AngleAxis(roll, Vector3.up);
        Vector3 piv = new Vector3(0f, 0.95f * t, 0f);
        Vector3 b0 = new Vector3(p.x, gy + 0.075f, p.z);
        System.Func<Vector3, Vector3> W = v => { Vector3 w = b0 + R * (v - piv); if (w.y < gy + 0.03f) w.y = gy + 0.03f; return w; };

        CJuntas j = new CJuntas { tamanho = t };
        Vector3 pelve = new Vector3(0f, 0.95f, 0f) * t, pesc = new Vector3(0f, 1.45f, 0.01f) * t;
        j.pelve = W(pelve); j.pescoco = W(pesc);
        j.frente = (R * Vector3.forward);
        // cabeça: perto do pescoço, às vezes rolada para longe
        Vector3 cab = new Vector3(0f, 1.60f, 0.03f) * t;
        if (rng.NextDouble() < 0.35) cab += new Vector3(Faixa(-0.5f, 0.5f), Faixa(-0.15f, 0f), Faixa(-0.2f, 0.4f)) * t;
        j.cabeca = W(cab);
        j.rotCabeca = R * Quaternion.Euler(Faixa(-30f, 30f), Faixa(-70f, 70f), Faixa(-20f, 20f));
        Vector3[] hipD = new Vector3[2], knD = new Vector3[2], anD = new Vector3[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            Vector3 hip = new Vector3(s * 0.1f, 0.93f, 0f) * t;
            float abre = Faixa(3f, estilo == 3 ? 55f : 22f), dob = Faixa(0f, estilo == 3 ? 100f : 45f);
            Vector3 coxa = Quaternion.Euler(Faixa(-15f, estilo == 3 ? 70f : 20f), 0f, s * abre) * (Vector3.down * 0.45f * t);
            Vector3 knee = hip + coxa;
            Vector3 canela = Quaternion.Euler(dob, Faixa(-10f, 10f), s * Faixa(-8f, 12f)) * (Vector3.down * 0.42f * t);
            hipD[i] = W(hip); knD[i] = W(knee); anD[i] = W(knee + canela);
        }
        j.quadE = hipD[0]; j.quadD = hipD[1]; j.joelhoE = knD[0]; j.joelhoD = knD[1]; j.tornE = anD[0]; j.tornD = anD[1];
        Vector3[] om = new Vector3[2], co = new Vector3[2], pu = new Vector3[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            Vector3 sh = new Vector3(s * 0.19f, 1.42f, 0f) * t;
            Vector3 brac = Quaternion.Euler(Faixa(-30f, 40f), 0f, s * Faixa(8f, 75f)) * (Vector3.down * 0.29f * t);
            Vector3 elb = sh + brac;
            Vector3 anteb = Quaternion.Euler(Faixa(-90f, 10f), Faixa(-40f, 40f), s * Faixa(-30f, 40f)) * (Vector3.down * 0.26f * t);
            om[i] = W(sh); co[i] = W(elb); pu[i] = W(elb + anteb);
        }
        j.ombroE = om[0]; j.ombroD = om[1]; j.cotE = co[0]; j.cotD = co[1]; j.punhoE = pu[0]; j.punhoD = pu[1];
        // com ombros e quadril ligados ao tronco
        CDesenharEsqueleto(m, j, Faixa(0f, 0.35f));

        // restos de equipamento: pedaço de armadura, escudo ou espada ao lado
        Quaternion qy = Quaternion.Euler(0f, yaw + Faixa(-40f, 40f), 0f);
        Vector3 lado = new Vector3(p.x, gy, p.z) + qy * new Vector3(Faixa(0.5f, 0.9f), 0f, Faixa(-0.4f, 0.4f));
        double d = rng.NextDouble();
        if (d < 0.3)
        {   // escudo redondo caído
            m.Cilindro(lado + Vector3.up * 0.04f, 0.42f, 0.05f, 14, (rng.Next(2) == 0) ? TEC_VERM : TEC_AZUL, Quaternion.Euler(Faixa(-8f, 8f), 0f, Faixa(-8f, 8f)));
            m.Cilindro(lado + Vector3.up * 0.1f, 0.08f, 0.05f, 8, FERRO, Quaternion.identity);
        }
        else if (d < 0.6)
        {   // espada no chão
            Quaternion qs = qy * Quaternion.Euler(0f, 0f, 0f);
            m.Caixa(lado + Vector3.up * 0.03f + qs * new Vector3(0f, 0f, 0.5f), new Vector3(0.07f, 0.02f, 1.0f), qs, LAMINA);
            m.Caixa(lado + Vector3.up * 0.04f, new Vector3(0.28f, 0.03f, 0.04f), qs, FERRO);
            m.Caixa(lado + Vector3.up * 0.04f + qs * new Vector3(0f, 0f, -0.14f), new Vector3(0.04f, 0.04f, 0.2f), qs, COURO);
        }
        else if (d < 0.8)
        {   // elmo caído
            m.Torno(new Vector3(lado.x, gy + 0.02f, lado.z), new[] { 0.16f, 0.17f, 0.15f, 0.09f, 0.001f }, new[] { 0f, 0.1f, 0.2f, 0.27f, 0.3f }, 10, FERRO, Quaternion.Euler(Faixa(60f, 100f), Faixa(0f, 360f), 0f), false);
        }
    }

    // ------------------------------------------------------------------
    //  trono + o rei (esqueleto sentado com coroa, manto rasgado e espada)
    // ------------------------------------------------------------------
    static void CTrono(Malha m, Malha ouro, Vector3 pos)
    {
        // pódio de cinco degraus
        float g = Cg(pos.x, pos.z) + 0.15f;
        for (int k = 0; k < 5; k++)
            CBloco(m, pos.x, pos.z + (4 - k) * 0.9f, g - 1f, g + (k + 1) * 0.32f, 16f - k * 2.2f, 11f - k * 1.6f, Quaternion.identity, k % 2 == 0 ? CEM_PEDRA : CEM_PISO);
        float topo = g + 5 * 0.32f;
        Vector3 t = new Vector3(pos.x, topo, pos.z - 0.6f);
        const float ESC_CADEIRA = 1.9f;                      // a cadeira (0,98 m) vira uma cadeira de rei de ~1,9 m
        float assento = 0.44f + 0.44f * ESC_CADEIRA;       // altura do assento a partir de t (plataforma 0,44 + assento do modelo)

        // plataforma + degrau para os pés do rei
        m.Caixa(t + new Vector3(0f, 0.22f, 0.1f), new Vector3(3.4f, 0.44f, 3.0f), Quaternion.identity, M_PED_Q);
        m.Caixa(t + new Vector3(0f, 0.6f, 0.6f), new Vector3(1.5f, 0.33f, 0.62f), Quaternion.identity, CEM_PEDRA);
        // cadeira do rei: modelo que você baixou (Assets/Cenario/Castelo/Cadeira_do_rei.glb); sem ele, um assento de pedra
        GameObject cadeira = raiz != null ? CModelo(PASTA_CASTELO + "/Cadeira_do_rei.glb", "Cadeira do Rei", castC, castRot, t + new Vector3(0f, 0f, -0.2f), 0f, 0.98f * ESC_CADEIRA, true, false, t.y + 0.44f, Pasta("Cadeira do Rei"), false, Vector3.zero) : null;
        if (cadeira == null)
        {
            m.Caixa(t + new Vector3(0f, assento * 0.5f, -0.2f), new Vector3(1.0f, assento, 0.9f), Quaternion.identity, CEM_PEDRA);
            m.Caixa(t + new Vector3(0f, assento * 0.5f + 0.6f, -0.62f), new Vector3(1.0f, assento + 1.2f, 0.12f), Quaternion.identity, CEM_PEDRA);
        }
        // encosto monumental de pedra por trás da cadeira, com espinhos
        foreach (int s in new[] { -1, 1 })
        {
            m.Caixa(t + new Vector3(s * 1.1f, 3.4f, -1.1f), new Vector3(0.5f, 5.6f, 0.55f), Quaternion.Euler(0f, 0f, -s * 4f), CEM_PEDRA);
            for (int k = 0; k < 4; k++) m.Piramide(t + new Vector3(s * (1.25f + k * 0.38f), 5.0f + k * 0.55f, -1.1f), 0.17f, 1.4f + k * 0.35f, FERRO);
        }
        m.Caixa(t + new Vector3(0f, 2.3f, -1.1f), new Vector3(2.0f, 3.2f, 0.55f), Quaternion.identity, CEM_PEDRA);
        m.Caixa(t + new Vector3(0f, 4.4f, -1.1f), new Vector3(2.5f, 1.0f, 0.5f), Quaternion.identity, CEM_PEDRA);
        m.Caixa(t + new Vector3(0f, 5.2f, -1.1f), new Vector3(1.5f, 0.7f, 0.45f), Quaternion.identity, M_PED_Q);
        m.Piramide(t + new Vector3(0f, 5.55f, -1.1f), 0.6f, 1.9f, CEM_PEDRA);
        m.Caixa(t + new Vector3(-0.3f, 3.0f, -0.81f), new Vector3(0.05f, 3.0f, 0.06f), Quaternion.Euler(0f, 0f, 12f), M_CINZA);   // rachaduras
        m.Caixa(t + new Vector3(0.5f, 4.2f, -0.81f), new Vector3(0.05f, 2.0f, 0.06f), Quaternion.Euler(0f, 0f, -20f), M_CINZA);

        // ---- o REI (tamanho 1.2), sentado na cadeira ----
        float k2 = 1.2f;
        Vector3 S = t + new Vector3(0f, assento, 0f);
        CJuntas j = new CJuntas { tamanho = k2 };
        j.pelve = S + new Vector3(0f, 0.12f, -0.30f);
        j.pescoco = S + new Vector3(0f, 0.12f + 0.54f * k2, -0.38f);
        j.frente = new Vector3(0f, -0.1f, 1f);
        Vector3 cabeca = j.pescoco + new Vector3(0f, 0.12f * k2, 0.09f);
        j.cabeca = cabeca;
        j.rotCabeca = Quaternion.Euler(28f, 8f, 4f);       // cabeça caída para a frente
        foreach (int s in new[] { -1, 1 })
        {
            Vector3 hip = j.pelve + new Vector3(s * 0.11f * k2, -0.02f, 0.02f);
            Vector3 knee = hip + new Vector3(s * 0.03f, 0.0f, 0.47f * k2);
            Vector3 ank = knee + new Vector3(s * 0.01f, -0.45f * k2, 0.04f);
            Vector3 sh = j.pelve + new Vector3(s * 0.2f * k2, 0.52f * k2, -0.07f);
            Vector3 elb = sh + new Vector3(s * 0.12f, -0.30f * k2, 0.12f);
            Vector3 wr = new Vector3(t.x + (s < 0 ? 0.12f : 0.28f), S.y + 0.27f, t.z + 0.02f);   // mãos no punho da espada, sobre o colo
            if (s < 0) { j.quadE = hip; j.joelhoE = knee; j.tornE = ank; j.ombroE = sh; j.cotE = elb; j.punhoE = wr; }
            else { j.quadD = hip; j.joelhoD = knee; j.tornD = ank; j.ombroD = sh; j.cotD = elb; j.punhoD = wr; }
        }
        CDesenharEsqueleto(m, j, 0f);

        // armadura enferrujada: ombreiras, gorjal e peitoral aberto
        foreach (int s in new[] { -1, 1 })
        {
            Vector3 sh = s < 0 ? j.ombroE : j.ombroD;
            m.Caixa(sh + new Vector3(s * 0.03f, 0.05f, 0f), new Vector3(0.2f, 0.035f, 0.17f), Quaternion.Euler(0f, 0f, -s * 18f), FERRO);
            m.Caixa(sh + new Vector3(s * 0.07f, 0.0f, 0f), new Vector3(0.1f, 0.03f, 0.15f), Quaternion.Euler(0f, 0f, -s * 40f), FERRO);
        }
        m.Caixa(j.pescoco + new Vector3(0f, -0.04f, 0.02f), new Vector3(0.24f, 0.07f, 0.17f), Quaternion.identity, FERRO);
        m.Caixa(j.pelve + new Vector3(0f, 0.04f, 0.0f), new Vector3(0.34f, 0.05f, 0.2f), Quaternion.identity, COURO);   // cinto
        // manto vermelho rasgado: sobre o encosto, no colo e caindo pelas pernas
        m.Caixa(S + new Vector3(0f, 0.95f, -0.52f), new Vector3(0.85f, 1.4f, 0.04f), Quaternion.Euler(-4f, 0f, 0f), TEC_VERM);
        m.Caixa(S + new Vector3(0f, 0.17f, 0.0f), new Vector3(0.6f, 0.03f, 0.8f), Quaternion.Euler(4f, 0f, 0f), TEC_VERM);
        m.Caixa(S + new Vector3(0.2f, -0.2f, 0.36f), new Vector3(0.22f, 0.55f, 0.03f), Quaternion.Euler(0f, 6f, 3f), TEC_VERM);
        m.Caixa(S + new Vector3(-0.32f, -0.3f, 0.34f), new Vector3(0.28f, 0.6f, 0.03f), Quaternion.Euler(0f, -8f, -4f), TEC_VERM);

        // coroa de ouro no crânio
        Quaternion rc = j.rotCabeca; Vector3 uc = rc * Vector3.up, fc = rc * Vector3.forward;
        Vector3 cb = cabeca + uc * 0.085f * k2 + fc * 0.01f;
        ouro.Torno(cb, new[] { 0.108f * k2, 0.112f * k2, 0.108f * k2 }, new[] { 0f, 0.035f * k2, 0.07f * k2 }, 14, M_OURO, rc, false);
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f;
            Vector3 off = rc * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.1f * k2;
            if (i == 5) continue;     // uma ponta quebrada
            ouro.Piramide(cb + uc * 0.065f * k2 + off, 0.02f * k2, (i % 2 == 0 ? 0.11f : 0.08f) * k2, M_OURO);
        }
        m.Caixa(cb + uc * 0.04f * k2 + fc * 0.108f * k2, new Vector3(0.025f, 0.025f, 0.012f), rc, TEC_VERM);   // rubi

        // espada do rei deitada sobre o colo, mãos no punho
        Vector3 colo = new Vector3(t.x, S.y + 0.2f, t.z + 0.02f);
        m.Caixa(colo + new Vector3(-0.5f, 0f, 0f), new Vector3(1.0f, 0.016f, 0.07f), Quaternion.Euler(0f, 4f, 0f), LAMINA);
        m.Caixa(colo + new Vector3(0.05f, 0f, 0f), new Vector3(0.04f, 0.03f, 0.3f), Quaternion.Euler(0f, 4f, 0f), FERRO);
        m.Caixa(colo + new Vector3(0.22f, 0f, 0f), new Vector3(0.3f, 0.04f, 0.04f), Quaternion.Euler(0f, 4f, 0f), COURO);
        ouro.Cilindro(colo + new Vector3(0.37f, -0.02f, 0f), 0.035f, 0.04f, 8, M_OURO, Quaternion.Euler(0f, 0f, 90f));

        // tapete vermelho rasgado até o trono
        float zTapete = pos.z + 6f;
        for (int k = 0; k < 6; k++)
        {
            if (k == 3) continue;
            m.Caixa(new Vector3(pos.x + Faixa(-0.2f, 0.2f), Cg(pos.x, zTapete + k * 4.2f) + 0.08f, zTapete + k * 4.2f), new Vector3(3.2f, 0.04f, Faixa(2.4f, 3.8f)), Quaternion.Euler(0f, Faixa(-6f, 6f), 0f), TEC_VERM);
        }

        // marcadores para a cena de corte (cutscene) com o rei
        if (raiz != null)
        {
            Transform cena = Pasta("Cutscene do Rei");
            GameObject rei = new GameObject("Rei (ponto da cena)"); rei.transform.SetParent(cena, false);
            rei.transform.position = CW(S + new Vector3(0f, 0.2f, -0.2f));
            GameObject cam = new GameObject("Camera da cena (de frente)"); cam.transform.SetParent(cena, false);
            Vector3 camLocal = new Vector3(S.x, S.y + 1.2f, S.z + 6.5f);
            cam.transform.position = CW(camLocal);
            cam.transform.rotation = Quaternion.LookRotation((CW(S + Vector3.up * 1.1f) - cam.transform.position).normalized, Vector3.up);
        }
    }
}
#endif
