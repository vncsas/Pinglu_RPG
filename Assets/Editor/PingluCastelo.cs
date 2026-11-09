// =====================================================================
//  PingluCastelo.cs  -  Castelo Destruído (as antigas "Ruínas da Fortaleza", agora enormes)
// ---------------------------------------------------------------------
//  - reconstrói o castelo inteiro (~140 x 120 m): muralhas quebradas, torres redondas destruídas,
//    portaria, torre de menagem com o Salão do Trono, pátio com marcas de batalha
//  - aplaina a colina por baixo (nada fica voando) e as fundações descem até o chão mais baixo
//  - espalha ruínas menores de castelos pelas Colinas de Pedra, que passam a se chamar "Reino Destruído"
//  - renomeia o lugar no mapa, no mini-mapa e no GPS, e refaz as estradas
//
//  COMO USAR:  Pinglu > 45. Castelo destruído   (Play desligado; depois Ctrl+S)
//  Para voltar: Pinglu > 46. Desfazer castelo destruído
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string NOME_CASTELO = "Castelo Destruido";
    const string NOME_PONTO_CASTELO = "Castelo Destruído";
    const string NOME_REINO = "Reino Destruído";
    const string PASTA_CASTELO = "Assets/Cenario/Castelo";
    const string BACKUP_CASTELO = PASTA_CASTELO + "/Backup_Terreno_Castelo.asset";
    const string PREFIXO_OCULTO_CASTELO = "[oculto pelo castelo] ";
    const float CAST_L = 140f, CAST_P = 120f;           // muralha externa (x, z)
    const int M_TIJ_Q = 37, M_PED_Q = 38, M_OSSO = 39, M_OURO = 40, M_CINZA = 41;

    static Vector3 castC; static Quaternion castRot;     // contexto atual (castelo ou ruína menor)
    static readonly List<Vector4> castOcupado = new List<Vector4>();

    // ------------------------------------------------------------------
    //  apoio
    // ------------------------------------------------------------------
    static float Cg(float x, float z) { return ChaoLocal(castC, castRot, new Vector3(x, 0f, z)); }
    static float CgMin(float x, float z, float r)
    {
        float m = Cg(x, z);
        for (int k = 0; k < 8; k++) { float a = k * Mathf.PI * 0.25f; m = Mathf.Min(m, Cg(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r)); }
        return m;
    }
    static Vector3 CW(Vector3 l) { return castC + castRot * l; }

    static void CastMateriais()
    {
        System.Array.Resize(ref mats, 42);
        mats[M_TIJ_Q] = CastDerivado(mats[CEM_TIJOLO], "Tijolo_Queimado", 0.30f, Color.white);
        mats[M_PED_Q] = CastDerivado(mats[CEM_PEDRA], "Pedra_Queimada", 0.28f, Color.white);
        mats[M_OSSO] = CastCor("Osso", new Color(0.78f, 0.73f, 0.60f), 0.25f, 0f);
        mats[M_OURO] = CastCor("Ouro_Velho", new Color(0.80f, 0.60f, 0.16f), 0.65f, 0.9f);
        mats[M_CINZA] = CastCor("Cinza_Queimada", new Color(0.09f, 0.085f, 0.08f), 0.05f, 0f);
    }

    static Material CastDerivado(Material orig, string nome, float escurece, Color mistura)
    {
        string caminho = PASTA_CASTELO + "/Materiais/" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(orig); AssetDatabase.CreateAsset(m, caminho); } else m.CopyPropertiesFromMaterial(orig);
        if (m.HasProperty("_BaseColor")) { Color c = orig.GetColor("_BaseColor"); m.SetColor("_BaseColor", new Color(c.r * escurece, c.g * escurece, c.b * escurece, 1f)); }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material CastCor(string nome, Color cor, float lisura, float metal)
    {
        string caminho = PASTA_CASTELO + "/Materiais/" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, caminho); }
        m.SetColor("_BaseColor", cor); m.SetFloat("_Smoothness", lisura); m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void CBloco(Malha m, float cx, float cz, float yBase, float yTopo, float w, float d, Quaternion r, int mat)
    {
        if (yTopo <= yBase + 0.01f) return;
        m.Caixa(new Vector3(cx, (yBase + yTopo) * 0.5f, cz), new Vector3(w, yTopo - yBase, d), r, mat);
    }

    // pedra solta no chão
    static void CEntulho(Malha m, float x, float z, float tam)
    {
        float y = Cg(x, z);
        int[] mt = { CEM_PEDRA, CEM_TIJOLO, M_PED_Q, CEM_PEDRA };
        m.Caixa(new Vector3(x, y + tam * 0.22f, z), new Vector3(tam * Faixa(0.8f, 1.5f), tam * Faixa(0.45f, 0.9f), tam * Faixa(0.7f, 1.3f)),
                Quaternion.Euler(Faixa(-30f, 30f), Faixa(0f, 360f), Faixa(-30f, 30f)), mt[rng.Next(mt.Length)]);
    }

    static void CMonte(Malha m, float x, float z, float raio, int n)
    {
        for (int k = 0; k < n; k++)
        {
            float a = Faixa(0f, Mathf.PI * 2f), d = Mathf.Sqrt((float)rng.NextDouble()) * raio;
            CEntulho(m, x + Mathf.Cos(a) * d, z + Mathf.Sin(a) * d, Faixa(0.35f, 1.5f) * Mathf.Lerp(1.2f, 0.6f, d / raio));
        }
    }

    // marca de batalha: mancha de fuligem / tijolo queimado / rachadura colada numa parede
    static void CMarca(Malha m, Vector3 centro, Vector3 normal, float w, float h)
    {
        Quaternion r = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(0f, 0f, Faixa(-12f, 12f));
        m.Caixa(centro + normal * 0.03f, new Vector3(w, h, 0.05f), r, rng.NextDouble() < 0.6 ? M_TIJ_Q : M_CINZA);
        if (rng.NextDouble() < 0.5) m.Caixa(centro + normal * 0.05f + Vector3.down * h * 0.7f, new Vector3(w * 0.25f, h * 0.9f, 0.05f), r, M_CINZA);   // escorrido
    }

    // ------------------------------------------------------------------
    //  muralha: painéis de ~5 m (inteiro, danificado ou desabado)
    // ------------------------------------------------------------------
    static void CMuro(Malha m, Malha ent, Vector3 a, Vector3 b, Vector3 fora, float H, float T, float pQuebra, int[] forcar, bool merlons)
    {
        Vector3 d = b - a; d.y = 0f; float comp = d.magnitude; d /= comp;
        int n = Mathf.Max(1, Mathf.RoundToInt(comp / 5f));
        float len = comp / n;
        Quaternion r = Quaternion.LookRotation(fora, Vector3.up);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = a + d * (len * (i + 0.5f));
            float gc = Cg(p.x, p.z), gmin = CgMin(p.x, p.z, len * 0.5f);
            float y0 = gmin - 1.3f;
            int estado;
            if (forcar != null && i < forcar.Length && forcar[i] >= 0) estado = forcar[i];
            else { double s = rng.NextDouble(); estado = s < pQuebra * 0.35 ? 2 : (s < pQuebra ? 1 : 0); }
            float topo = gc + H;
            // soco (base) de pedra mais escura e saliente
            CBloco(m, p.x, p.z, y0, gc + 1.5f, len + 0.04f, T + 0.5f, r, CEM_PEDRA);
            if (estado == 0)
            {
                CBloco(m, p.x, p.z, y0, topo, len + 0.03f, T, r, CEM_TIJOLO);
                CBloco(m, p.x, p.z, gc + H * 0.58f, gc + H * 0.58f + 0.28f, len + 0.04f, T + 0.4f, r, CEM_PEDRA);      // friso
                CBloco(m, p.x, p.z, topo, topo + 0.35f, len + 0.04f, T + 0.5f, r, CEM_PEDRA);                           // cornija
                if (merlons)
                {
                    int nm = Mathf.Max(2, Mathf.RoundToInt(len / 1.8f));
                    for (int k = 0; k < nm; k++)
                    {
                        float off = -len * 0.5f + (k + 0.5f) * len / nm;
                        if (rng.NextDouble() < 0.18) continue;
                        Vector3 q = p + d * off + fora * (T * 0.27f);
                        CBloco(m, q.x, q.z, topo + 0.35f, topo + 0.35f + Faixa(1.0f, 1.45f), len / nm * 0.58f, T * 0.45f, r, CEM_TIJOLO);
                    }
                }
                if (i % 2 == 0)   // contraforte pelo lado de dentro
                {
                    Vector3 q = p - fora * (T * 0.5f + 0.7f);
                    CBloco(m, q.x, q.z, y0, gc + H * 0.8f, 1.5f, 1.4f, r, CEM_PEDRA);
                }
                Vector3 face = p + fora * (T * 0.5f + 0.02f);
                m.Painel(new Vector3(face.x, gc + H * 0.62f, face.z), fora, 0.34f, 1.3f, P_ARCO_SINO);
            }
            else if (estado == 1)
            {
                // três pedaços de alturas diferentes + topo irregular
                float larg = len / 3f;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 q = p + d * ((k - 1) * larg);
                    float h = H * Faixa(0.3f, 0.95f);
                    CBloco(m, q.x, q.z, y0, gc + h, larg + 0.02f, T * Faixa(0.85f, 1f), r, CEM_TIJOLO);
                    for (int j = 0; j < 3; j++)
                    {
                        float w2 = Faixa(0.5f, 1.3f);
                        Vector3 q2 = q + d * Faixa(-larg * 0.4f, larg * 0.4f);
                        CBloco(m, q2.x, q2.z, gc + h, gc + h + Faixa(0.25f, 1.3f), w2, T * Faixa(0.5f, 0.9f), r * Quaternion.Euler(0f, Faixa(-12f, 12f), 0f), rng.NextDouble() < 0.5 ? CEM_TIJOLO : CEM_PEDRA);
                    }
                    CMonte(ent, q.x + fora.x * 2.2f, q.z + fora.z * 2.2f, 2.2f, 3);
                }
            }
            else
            {
                float h = Faixa(0.35f, 1.7f);
                CBloco(m, p.x, p.z, y0, gc + h, len * Faixa(0.5f, 0.85f), T * Faixa(0.6f, 0.9f), r * Quaternion.Euler(0f, Faixa(-8f, 8f), 0f), CEM_TIJOLO);
                CMonte(ent, p.x, p.z, len * 0.55f + 1.5f, 14);
            }
            if (estado < 2 && rng.NextDouble() < 0.7)   // marcas de batalha na face de fora (e de dentro)
            {
                float hh = Faixa(1.2f, 3.2f), ww = Faixa(1.4f, 3.6f);
                Vector3 sinal = rng.NextDouble() < 0.7 ? fora : -fora;
                Vector3 q = p + d * Faixa(-len * 0.3f, len * 0.3f) + sinal * (T * 0.5f);
                CMarca(m, new Vector3(q.x, gc + Faixa(2f, Mathf.Max(2.5f, H * 0.7f)), q.z), sinal, ww, hh);
            }
        }
    }

    // ------------------------------------------------------------------
    //  torre redonda destruída
    // ------------------------------------------------------------------
    static void CTorre(Malha m, Malha ent, Vector3 c, float r, float H, int lados, float quebra, float giro)
    {
        float gc = Cg(c.x, c.z), gmin = CgMin(c.x, c.z, r);
        float y0 = gmin - 1.4f, T = 1.9f;
        float sl = 2f * r * Mathf.Tan(Mathf.PI / lados) + 0.18f;
        float arcoBrecha = Faixa(0f, Mathf.PI * 2f);
        float semente = Faixa(0f, 50f);
        for (int k = 0; k < lados; k++)
        {
            float ang = giro + k * Mathf.PI * 2f / lados;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            Vector3 p = c + dir * r;
            Quaternion rr = Quaternion.LookRotation(dir, Vector3.up);
            float diff = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, arcoBrecha * Mathf.Rad2Deg));
            float ruido = Mathf.PerlinNoise(semente + k * 0.45f, 3.7f);
            float f = quebra <= 0.01f ? 1f : Mathf.Clamp01(1f - quebra * (0.1f + 0.9f * ruido));
            if (diff < 40f && quebra > 0.2f) f = Mathf.Min(f, Faixa(0.22f, 0.45f));
            float h = H * f;
            CBloco(m, p.x, p.z, y0, gc + 1.9f, sl + 0.06f, T + 0.7f, rr, CEM_PEDRA);
            CBloco(m, p.x, p.z, y0, gc + h, sl, T, rr, CEM_TIJOLO);
            if (f > 0.9f)
            {
                CBloco(m, p.x, p.z, gc + h, gc + h + 0.4f, sl + 0.04f, T + 0.5f, rr, CEM_PEDRA);
                if (k % 2 == 0) CBloco(m, p.x, p.z, gc + h + 0.4f, gc + h + 1.7f, sl * 0.6f, T * 0.55f, rr, CEM_TIJOLO);
            }
            else
            {
                for (int j = 0; j < 2; j++)
                    CBloco(m, p.x + Faixa(-0.6f, 0.6f), p.z + Faixa(-0.6f, 0.6f), gc + h, gc + h + Faixa(0.3f, 1.6f), Faixa(0.6f, 1.4f), T * Faixa(0.5f, 0.9f), rr * Quaternion.Euler(0f, Faixa(-15f, 15f), 0f), CEM_TIJOLO);
            }
            if (h > 7f && k % 3 == 0)
            {
                Vector3 face = p + dir * (T * 0.5f + 0.02f);
                m.Painel(new Vector3(face.x, gc + Mathf.Min(h - 2f, 7f), face.z), dir, 0.4f, 1.4f, P_ARCO_SINO);
                if (h > 15f) m.Painel(new Vector3(face.x, gc + 13f, face.z), dir, 0.4f, 1.4f, P_ARCO_SINO);
            }
            if (rng.NextDouble() < 0.35 && h > 5f) CMarca(m, new Vector3(p.x, gc + Faixa(2f, h - 1f), p.z) + dir * (T * 0.5f), dir, Faixa(1.2f, 2.6f), Faixa(1.2f, 3f));
        }
        // vigas dos andares que caíram, saindo da parede
        for (int nivel = 0; nivel < 2; nivel++)
        {
            float yv = gc + H * (0.45f + nivel * 0.3f);
            if (yv > gc + H * (1f - quebra * 0.4f)) continue;
            for (int k = 0; k < 6; k++)
            {
                float ang = Faixa(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float comp = Faixa(r * 0.4f, r * 1.2f);
                m.Caixa(new Vector3(c.x, yv, c.z) + dir * (r - comp * 0.5f - 0.5f), new Vector3(0.32f, 0.38f, comp), Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(Faixa(-18f, 8f), 0f, 0f), MAD_ESC);
            }
        }
        CMonte(ent, c.x, c.z, r * 0.9f, 12);
        CMonte(ent, c.x + Mathf.Cos(arcoBrecha) * (r + 3f), c.z + Mathf.Sin(arcoBrecha) * (r + 3f), 4f, 10);
    }

    // ------------------------------------------------------------------
    //  portaria: duas torres, arco quebrado, grade de ferro e portas caídas
    // ------------------------------------------------------------------
    static void CPortaria(Malha m, Malha ent, float zFrente)
    {
        float meio = 6.2f, rt = 7.2f;
        CTorre(m, ent, new Vector3(-(meio + rt), 0f, zFrente), rt, 23f, 20, 0.45f, 0.2f);
        CTorre(m, ent, new Vector3(meio + rt, 0f, zFrente), rt, 19f, 20, 0.75f, 0.9f);
        // passagem: arco de aduelas, uma parte desabada
        float g = Cg(0f, zFrente), gmin = CgMin(0f, zFrente, 5f), y0 = gmin - 1.3f;
        Quaternion r0 = Quaternion.identity;
        CBloco(m, -meio + 0.5f, zFrente, y0, g + 7f, 2.2f, 4.6f, r0, CEM_PEDRA);
        CBloco(m, meio - 0.5f, zFrente, y0, g + 7f, 2.2f, 4.6f, r0, CEM_PEDRA);
        for (int k = 0; k < 9; k++)
        {
            float t = (k + 0.5f) / 9f, ang = Mathf.Lerp(Mathf.PI, 0f, t);
            float x = Mathf.Cos(ang) * (meio - 0.2f), y = g + 7f + Mathf.Sin(ang) * 2.6f;
            if (k >= 5 && k <= 7) { CEntulho(ent, x * 0.7f, zFrente + Faixa(-2f, 3f), 1.0f); continue; }   // aduelas caídas
            m.Caixa(new Vector3(x, y, zFrente), new Vector3(1.5f, 1.15f, 4.4f), Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg - 90f), CEM_PEDRA);
        }
        CBloco(m, -meio * 0.5f, zFrente, g + 9.6f, g + 15f, meio + 4f, 4.2f, r0, CEM_TIJOLO);
        CBloco(m, -meio * 0.5f, zFrente, g + 15f, g + 15.4f, meio + 4.3f, 4.5f, r0, CEM_PEDRA);
        for (int k = 0; k < 5; k++) CBloco(m, -meio + k * 1.8f, zFrente, g + 15.4f, g + 15.4f + Faixa(0.4f, 1.4f), 1.2f, 3.0f, r0, CEM_TIJOLO);
        // grade de ferro meio caída e portas de madeira arrancadas
        for (int k = 0; k < 13; k++)
        {
            float x = -meio + 1.2f + k * 0.78f;
            float alt = k % 4 == 0 ? Faixa(2.2f, 3.0f) : Faixa(3.3f, 4.4f);
            m.Caixa(new Vector3(x, g + 7.2f - alt * 0.5f, zFrente + 1.4f), new Vector3(0.09f, alt, 0.09f), Quaternion.identity, FERRO);
            m.Piramide(new Vector3(x, g + 7.2f - alt, zFrente + 1.4f), 0.07f, -0.3f, FERRO);
        }
        for (int k = 0; k < 3; k++) m.Caixa(new Vector3(0f, g + 6.4f - k * 1.0f, zFrente + 1.4f), new Vector3(meio * 2f - 2f, 0.1f, 0.1f), Quaternion.identity, FERRO);
        m.Caixa(new Vector3(-3.4f, g + 1.2f, zFrente + 6f), new Vector3(3.2f, 0.4f, 6.2f), Quaternion.Euler(-14f, -8f, 4f), TABUAS);
        m.Caixa(new Vector3(3.8f, g + 3.2f, zFrente + 0.8f), new Vector3(0.45f, 6.4f, 3.0f), Quaternion.Euler(0f, 0f, 8f), TABUAS);
        m.Caixa(new Vector3(3.0f, g + 0.3f, zFrente + 5.5f), new Vector3(3.0f, 0.4f, 5.6f), Quaternion.Euler(6f, 20f, -3f), TABUAS);
        for (int k = 0; k < 3; k++) CMonte(ent, Faixa(-5f, 5f), zFrente + Faixa(-3f, 4f), 3f, 6);
    }

    // ------------------------------------------------------------------
    //  Salão do Trono dentro da torre de menagem
    // ------------------------------------------------------------------
    // ------------------------------------------------------------------
    //  torre de menagem + salão do trono
    // ------------------------------------------------------------------
    static void CMenagem(Malha m, Malha ent, Malha ouro, float zc, Transform pastaLuzes, Transform efeitos)
    {
        float W = 44f, D = 34f, H = 30f, T = 3.4f;
        Vector3[] cantos = { new Vector3(-W / 2, 0f, D / 2 + zc), new Vector3(W / 2, 0f, D / 2 + zc), new Vector3(W / 2, 0f, -D / 2 + zc), new Vector3(-W / 2, 0f, -D / 2 + zc) };
        // frente (para o pátio) tem a grande entrada em arco e um rombo no alto
        CMuro(m, ent, new Vector3(-W / 2, 0f, zc + D / 2), new Vector3(-5f, 0f, zc + D / 2), Vector3.forward, H, T, 0.5f, new[] { 0, 0, 1, 0, 1 }, false);
        CMuro(m, ent, new Vector3(5f, 0f, zc + D / 2), new Vector3(W / 2, 0f, zc + D / 2), Vector3.forward, H, T, 0.5f, new[] { 1, 2, 0, 0, 1 }, false);
        CMuro(m, ent, new Vector3(W / 2, 0f, zc + D / 2), new Vector3(W / 2, 0f, zc - D / 2), Vector3.right, H, T, 0.55f, null, false);
        CMuro(m, ent, new Vector3(W / 2, 0f, zc - D / 2), new Vector3(-W / 2, 0f, zc - D / 2), Vector3.back, H, T, 0.35f, null, false);
        CMuro(m, ent, new Vector3(-W / 2, 0f, zc - D / 2), new Vector3(-W / 2, 0f, zc + D / 2), Vector3.left, H, T, 0.45f, null, false);
        // grande arco da entrada
        float zf = zc + D / 2, g = Cg(0f, zf), y0 = CgMin(0f, zf, 6f) - 1.3f;
        CBloco(m, -4.4f, zf, y0, g + 10f, 2.4f, T + 0.4f, Quaternion.identity, CEM_PEDRA);
        CBloco(m, 4.4f, zf, y0, g + 10f, 2.4f, T + 0.4f, Quaternion.identity, CEM_PEDRA);
        for (int k = 0; k < 11; k++)
        {
            float t = (k + 0.5f) / 11f, ang = Mathf.Lerp(Mathf.PI, 0f, t);
            if (k == 8) { CEntulho(ent, 1.5f, zf + 3f, 1.3f); continue; }
            m.Caixa(new Vector3(Mathf.Cos(ang) * 4.4f, g + 10f + Mathf.Sin(ang) * 3.2f, zf), new Vector3(1.7f, 1.3f, T + 0.4f), Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg - 90f), CEM_PEDRA);
        }
        // andares: pedaços de piso de tábuas com vigas, janelas em arco
        for (int nivel = 1; nivel <= 2; nivel++)
        {
            float yp = g + nivel * 10f;
            for (int k = 0; k < 5; k++)
            {
                float px = Faixa(-W / 2 + T + 3f, W / 2 - T - 3f), pz = zc + Faixa(-D / 2 + T + 2f, D / 2 - T - 4f);
                if (nivel == 1 && Mathf.Abs(px) < 6f && pz > zc - 6f) continue;
                float sx = Faixa(5f, 11f), sz = Faixa(4f, 8f);
                m.Caixa(new Vector3(px, yp, pz), new Vector3(sx, 0.4f, sz), Quaternion.Euler(Faixa(-5f, 5f), Faixa(-5f, 5f), Faixa(-6f, 6f)), TABUAS);
                for (int j = 0; j < 4; j++) m.Caixa(new Vector3(px - sx * 0.4f + j * sx * 0.27f, yp - 0.4f, pz), new Vector3(0.3f, 0.4f, sz + 0.5f), Quaternion.identity, MAD_ESC);
            }
        }
        foreach (int lado in new[] { -1, 1 })
            for (int k = 0; k < 4; k++)
            {
                float x = -W / 2 + 7f + k * 10f;
                float zlado = lado > 0 ? zc + D / 2 + T * 0.5f + 0.03f : zc - D / 2 - T * 0.5f - 0.03f;
                for (int nivel = 0; nivel < 3; nivel++)
                {
                    if (lado > 0 && nivel == 0 && Mathf.Abs(x) < 8f) continue;
                    m.Painel(new Vector3(x, Cg(x, zlado) + 5.5f + nivel * 9f, zlado), lado > 0 ? Vector3.forward : Vector3.back, 1.4f, 3.2f, P_JANELA_ARCO);
                }
            }
        // dentro: piso de pedra, colunas quebradas, estandartes, trono no fundo
        float gi = Cg(0f, zc);
        m.Caixa(new Vector3(0f, gi + 0.06f, zc), new Vector3(W - 2 * T, 0.2f, D - 2 * T), Quaternion.identity, CEM_PISO);
        foreach (int s in new[] { -1, 1 })
            for (int k = 0; k < 4; k++)
            {
                float cx = s * 9.5f, cz = zc - D / 2 + T + 4f + k * 6.8f;
                float hc = k == 1 ? Faixa(1.2f, 3.0f) : (k == 2 ? 6.5f : Faixa(4f, 9f));
                m.Cilindro(new Vector3(cx, Cg(cx, cz) - 0.2f, cz), 0.95f, hc, 14, CEM_PEDRA, Quaternion.identity);
                m.Caixa(new Vector3(cx, Cg(cx, cz) + 0.2f, cz), new Vector3(2.4f, 0.5f, 2.4f), Quaternion.identity, CEM_PEDRA);
                if (hc > 6f) m.Caixa(new Vector3(cx, Cg(cx, cz) + hc, cz), new Vector3(2.2f, 0.6f, 2.2f), Quaternion.identity, CEM_PEDRA);
                // faixas de tecido penduradas (estandartes rasgados)
                if (k != 1) m.Caixa(new Vector3(cx + (s > 0 ? -1.05f : 1.05f), Cg(cx, cz) + hc * 0.55f, cz), new Vector3(0.04f, Mathf.Min(hc * 0.5f, 3.2f), 1.1f), Quaternion.Euler(0f, 0f, s * 3f), k % 2 == 0 ? TEC_VERM : TEC_AZUL);
            }
        // coluna caída atravessada no salão
        m.Cilindro(new Vector3(-5f, gi + 0.95f, zc + 4f), 0.95f, 7f, 14, CEM_PEDRA, Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, 28f, 0f));
        CTrono(m, ouro, new Vector3(0f, 0f, zc - D / 2 + T + 4.3f));
        // grande vitral quebrado atrás do trono
        float zb = zc - D / 2 + T * 0.5f + 0.05f;    // face de dentro da parede dos fundos
        m.Painel(new Vector3(0f, gi + 11f, zb), Vector3.forward, 5.4f, 11f, P_VITRAL);
        CMonte(ent, 0f, zc + D / 2 + 5f, 9f, 24);
        CMonte(ent, 0f, zc, 14f, 40);
        // fogueiras de brasa: braseiros junto ao trono e à entrada
        foreach (Vector3 bp in new[] { new Vector3(-7.5f, 0f, zc - D / 2 + T + 3f), new Vector3(7.5f, 0f, zc - D / 2 + T + 3f), new Vector3(-6f, 0f, zc + D / 2 - T - 1.5f), new Vector3(6f, 0f, zc + D / 2 - T - 1.5f) })
        {
            float gy = Cg(bp.x, bp.z);
            m.Cilindro(new Vector3(bp.x, gy, bp.z), 0.05f, 1.3f, 6, FERRO, Quaternion.Euler(0f, 0f, 14f));
            m.Cilindro(new Vector3(bp.x, gy + 1.2f, bp.z), 0.55f, 0.35f, 12, FERRO, Quaternion.identity);
            m.Cilindro(new Vector3(bp.x, gy + 1.5f, bp.z), 0.5f, 0.06f, 12, BRASA, Quaternion.identity);
            Fogo(efeitos, CW(new Vector3(bp.x, gy + 1.6f, bp.z)) - efeitos.position);
            LuzDeFogo(efeitos, CW(new Vector3(bp.x, gy + 2.5f, bp.z)) - efeitos.position, 16f, 3.5f);
        }
    }

    // ------------------------------------------------------------------
    //  pátio: poço, estábulo, ferraria, quartel, catapulta, aríete, escadas, flechas, escudos, corpos
    // ------------------------------------------------------------------
    static bool CLivre(float x, float z, float raio)
    {
        foreach (Vector4 o in castOcupado) if (new Vector2(x - o.x, z - o.y).magnitude < o.w + raio) return false;
        return true;
    }
    static Vector3 CPonto(float x0, float x1, float z0, float z1, float raio)
    {
        for (int t = 0; t < 80; t++)
        {
            float x = Faixa(x0, x1), z = Faixa(z0, z1);
            if (!CLivre(x, z, raio)) continue;
            castOcupado.Add(new Vector4(x, z, 0f, raio));
            return new Vector3(x, 0f, z);
        }
        return new Vector3(float.NaN, 0f, 0f);
    }

    static void CPatio(Malha m, Malha ent, Malha mad, Transform efeitos, Transform pasta)
    {
        float xi = CAST_L / 2 - 14f;
        // poço de pedra com cobertura quebrada
        Vector3 p = CPonto(-30f, 30f, 6f, 30f, 7f);
        if (!float.IsNaN(p.x))
        {
            float gy = Cg(p.x, p.z);
            m.Cilindro(new Vector3(p.x, gy - 0.5f, p.z), 1.9f, 1.7f, 16, CEM_PEDRA, Quaternion.identity);
            m.Cilindro(new Vector3(p.x, gy + 1.1f, p.z), 1.5f, 0.1f, 16, CEM_CINZA_OU(), Quaternion.identity);
            foreach (int s in new[] { -1, 1 }) mad.Caixa(new Vector3(p.x + s * 1.7f, gy + 1.9f, p.z), new Vector3(0.22f, 2.4f, 0.22f), Quaternion.Euler(0f, 0f, s * 4f), MAD_ESC);
            mad.Caixa(new Vector3(p.x - 0.4f, gy + 3.0f, p.z), new Vector3(3.6f, 0.2f, 0.25f), Quaternion.Euler(0f, 0f, 12f), MAD_ESC);
        }
        // estábulo em ruínas (paredes baixas + vigas)
        p = CPonto(-xi + 8f, -xi + 30f, 2f, 28f, 12f);
        if (!float.IsNaN(p.x)) CEdificioArruinado(m, ent, mad, p, 18f, 9f, Faixa(-8f, 8f), 2);
        // ferraria: forja com brasas e bigorna
        p = CPonto(xi - 28f, xi - 8f, 2f, 28f, 10f);
        if (!float.IsNaN(p.x))
        {
            CEdificioArruinado(m, ent, mad, p, 14f, 9f, Faixa(-8f, 8f), 1);
            float gy = Cg(p.x, p.z);
            m.Caixa(new Vector3(p.x - 3f, gy + 0.6f, p.z), new Vector3(2.2f, 1.2f, 1.8f), Quaternion.identity, CEM_PEDRA);
            m.Cilindro(new Vector3(p.x - 3f, gy + 1.2f, p.z), 0.7f, 0.08f, 10, BRASA, Quaternion.identity);
            Fogo(efeitos, CW(new Vector3(p.x - 3f, gy + 1.3f, p.z)) - efeitos.position);
            mad.Cilindro(new Vector3(p.x + 1.5f, gy, p.z + 1f), 0.5f, 0.8f, 8, MAD_ESC, Quaternion.identity);
            m.Caixa(new Vector3(p.x + 1.5f, gy + 1.0f, p.z + 1f), new Vector3(0.5f, 0.3f, 1.1f), Quaternion.Euler(0f, 30f, 0f), FERRO);
        }
        // quartéis: alicerces e telhado desabado
        for (int i = 0; i < 2; i++)
        {
            p = CPonto(-xi + 4f, xi - 4f, -CAST_P / 2 + 8f, -6f, 13f);
            if (float.IsNaN(p.x) || Mathf.Abs(p.x) < 28f && p.z < -8f) continue;
            CEdificioArruinado(m, ent, mad, p, 22f, 10f, Faixa(-6f, 6f), 3);
        }
        // máquinas de cerco destruídas, estandartes, troncos e pedras: modelos prontos do kit (se não estiver importado, uso as peças simples)
        bool kit = CKitPatio();
        if (!kit) { p = CPonto(-xi + 6f, -10f, -20f, 10f, 8f);
        if (!float.IsNaN(p.x)) CCatapulta(mad, m, p, Faixa(0f, 360f)); }
        if (!kit) p = CPonto(-12f, 12f, CAST_P / 2 - 40f, CAST_P / 2 - 14f, 8f); else p = new Vector3(float.NaN, 0f, 0f);
        if (!float.IsNaN(p.x))
        {
            float gy = Cg(p.x, p.z); Quaternion q = Quaternion.Euler(0f, Faixa(-25f, 25f), 0f);
            mad.Caixa(new Vector3(p.x, gy + 1.15f, p.z), new Vector3(0.85f, 0.85f, 7.5f), q, MAD_ESC);
            m.Caixa(new Vector3(p.x, gy + 1.15f, p.z) + q * Vector3.forward * 3.9f, new Vector3(1.0f, 1.0f, 0.9f), q, FERRO);
            foreach (int s in new[] { -1, 1 }) foreach (float zz in new[] { -2.2f, 2.2f })
                mad.Cilindro(new Vector3(p.x, gy, p.z) + q * new Vector3(s * 0.9f, 0f, zz), 0.55f, 0.15f, 12, MAD_ESC, Quaternion.Euler(0f, 0f, 90f));
        }
        // escadas de assalto encostadas / caídas, flechas, escudos, espadas, corpos, estandartes
        for (int i = 0; i < 0; i++)   // (escadas de madeira sem textura saíram: ficavam parecendo vigas voando)
        {
            float lado = rng.Next(4);
            Vector3 q;
            float ang = Faixa(0f, Mathf.PI * 2f);
            q = new Vector3(Mathf.Cos(ang) * (CAST_L / 2 - 8f), 0f, Mathf.Sin(ang) * (CAST_P / 2 - 8f));
            if (Mathf.Abs(q.x) < 14f && q.z > 0f) continue;
            float gy = Cg(q.x, q.z);
            Quaternion rl = Quaternion.LookRotation(new Vector3(-q.x, 0f, -q.z).normalized) * Quaternion.Euler(Faixa(-50f, -28f), 0f, 0f);
            mad.Caixa(new Vector3(q.x, gy + 3.2f, q.z), new Vector3(1.0f, 7.5f, 0.12f), rl, MAD_ESC);
            for (int k = 0; k < 8; k++) mad.Caixa(new Vector3(q.x, gy + 0.6f + k * 0.85f, q.z), new Vector3(1.0f, 0.07f, 0.1f), rl, MAD_ESC);
        }
        for (int i = 0; i < 70; i++)   // flechas cravadas no chão, em grupos
        {
            float cx = Faixa(-CAST_L / 2 + 10f, CAST_L / 2 - 10f), cz = Faixa(-CAST_P / 2 + 10f, CAST_P / 2 - 8f);
            if (!CLivre(cx, cz, 1f)) continue;
            int nf = rng.Next(3, 8);
            for (int k = 0; k < nf; k++)
            {
                float x = cx + Faixa(-2.5f, 2.5f), z = cz + Faixa(-2.5f, 2.5f);
                Quaternion q = Quaternion.Euler(Faixa(-30f, 30f), Faixa(0f, 360f), Faixa(-30f, 30f));
                m.Caixa(new Vector3(x, Cg(x, z) + 0.5f, z), new Vector3(0.025f, 1.05f, 0.025f), q, MAD_ESC);
                m.Caixa(new Vector3(x, Cg(x, z) + 0.95f, z) + q * Vector3.up * 0.1f, new Vector3(0.08f, 0.16f, 0.01f), q, COURO);
            }
            i += 1;
        }
        for (int i = 0; i < 16; i++)   // escudos e espadas abandonados
        {
            Vector3 q = CPonto(-CAST_L / 2 + 10f, CAST_L / 2 - 10f, -CAST_P / 2 + 10f, CAST_P / 2 - 10f, 1.5f);
            if (float.IsNaN(q.x)) continue;
            float gy = Cg(q.x, q.z);
            if (i % 3 == 0) { m.Caixa(new Vector3(q.x, gy + 0.55f, q.z), new Vector3(0.08f, 1.1f, 0.03f), Quaternion.Euler(Faixa(-12f, 12f), Faixa(0f, 360f), Faixa(-8f, 8f)), LAMINA); m.Caixa(new Vector3(q.x, gy + 1.05f, q.z), new Vector3(0.5f, 0.07f, 0.07f), Quaternion.identity, FERRO); }
            else m.Cilindro(new Vector3(q.x, gy + 0.05f, q.z), 0.55f, 0.07f, 12, i % 2 == 0 ? TEC_VERM : TEC_AZUL, Quaternion.Euler(Faixa(-14f, 14f), Faixa(0f, 360f), 0f));
        }
        for (int i = 0; i < 7; i++)   // corpos: pilhas de ossos e armaduras
        {
            Vector3 q = CPonto(-CAST_L / 2 + 12f, CAST_L / 2 - 12f, -CAST_P / 2 + 12f, CAST_P / 2 - 10f, 2.5f);
            if (float.IsNaN(q.x)) continue;
            CEsqueleto(m, q, Faixa(0f, 360f));
        }
        for (int i = 0; i < 0; i++)    // (estandartes tombados agora vêm do kit)
        {
            Vector3 q = CPonto(-CAST_L / 2 + 14f, CAST_L / 2 - 14f, -CAST_P / 2 + 14f, CAST_P / 2 - 14f, 2.5f);
            if (float.IsNaN(q.x)) continue;
            float gy = Cg(q.x, q.z); Quaternion r = Quaternion.Euler(Faixa(-70f, -20f), Faixa(0f, 360f), 0f);
            mad.Caixa(new Vector3(q.x, gy + 1.3f, q.z), new Vector3(0.08f, 4.2f, 0.08f), r, MAD_ESC);
            m.Caixa(new Vector3(q.x, gy + 1.5f, q.z) + r * new Vector3(0f, 0.9f, 0.45f), new Vector3(0.03f, 1.8f, 1.2f), r, i % 2 == 0 ? TEC_VERM : TEC_AZUL);
        }
        // manchas de fogo e cinza pelo chão (onde algo queimou)
        for (int i = 0; i < 14; i++)
        {
            Vector3 q = CPonto(-CAST_L / 2 + 10f, CAST_L / 2 - 10f, -CAST_P / 2 + 10f, CAST_P / 2 - 10f, 3f);
            if (float.IsNaN(q.x)) continue;
            ent.Cilindro(new Vector3(q.x, Cg(q.x, q.z) + 0.03f, q.z), Faixa(1.6f, 4.2f), 0.04f, 14, M_CINZA, Quaternion.identity);
        }
        // focos de fogo nas ruínas (sem fumaça)
        for (int i = 0; i < 3; i++)
        {
            Vector3 q = CPonto(-40f, 40f, -40f, 30f, 5f);
            if (float.IsNaN(q.x)) continue;
            Fogo(efeitos, CW(new Vector3(q.x, Cg(q.x, q.z) + 0.4f, q.z)) - efeitos.position);
        }
    }

    static int CEM_CINZA_OU() { return M_CINZA; }

    // construção em ruínas: paredes baixas irregulares, vigas do telhado caídas e telhas
    static void CEdificioArruinado(Malha m, Malha ent, Malha mad, Vector3 c, float w, float d, float yaw, int tipo)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        for (int lado = 0; lado < 4; lado++)
        {
            bool alongX = lado % 2 == 0;
            float len = alongX ? w : d;
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 2.2f));
            for (int i = 0; i < n; i++)
            {
                if (rng.NextDouble() < 0.18) continue;
                float off = -len * 0.5f + (i + 0.5f) * len / n;
                Vector3 lp = alongX ? new Vector3(off, 0f, (lado == 0 ? d : -d) * 0.5f) : new Vector3((lado == 1 ? w : -w) * 0.5f, 0f, off);
                if (tipo == 2 && lado == 2 && i % 3 == 0) continue;
                Vector3 wp = c + q * lp;
                float gy = Cg(wp.x, wp.z), gmin = CgMin(wp.x, wp.z, 1.3f);
                float h = Faixa(0.5f, 3.8f);
                Quaternion r = q * (alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));
                CBloco(m, wp.x, wp.z, gmin - 0.8f, gy + h, len / n + 0.05f, 0.7f, r, rng.NextDouble() < 0.7 ? CEM_TIJOLO : CEM_PEDRA);
                if (rng.NextDouble() < 0.3) CMarca(m, new Vector3(wp.x, gy + h * 0.6f, wp.z) + r * Vector3.forward * 0.37f, r * Vector3.forward, 1.0f, 0.8f);
            }
        }
        // vigas do telhado que despencou, em diagonal, e pilhas de telhas
        for (int k = 0; k < 7; k++)
        {
            Vector3 lp = new Vector3(Faixa(-w * 0.4f, w * 0.4f), 0f, Faixa(-d * 0.4f, d * 0.4f)); Vector3 wp = c + q * lp;
            float gy = Cg(wp.x, wp.z);
            mad.Caixa(new Vector3(wp.x, gy + Faixa(0.3f, 1.8f), wp.z), new Vector3(0.28f, 0.3f, Faixa(3f, 7f)), q * Quaternion.Euler(Faixa(-30f, 30f), Faixa(0f, 180f), Faixa(-10f, 10f)), MAD_ESC);
        }
        for (int k = 0; k < 10; k++)
        {
            Vector3 lp = new Vector3(Faixa(-w * 0.45f, w * 0.45f), 0f, Faixa(-d * 0.45f, d * 0.45f)); Vector3 wp = c + q * lp;
            ent.Caixa(new Vector3(wp.x, Cg(wp.x, wp.z) + 0.1f, wp.z), new Vector3(Faixa(1f, 2f), 0.12f, Faixa(0.8f, 1.4f)), Quaternion.Euler(Faixa(-12f, 12f), Faixa(0f, 360f), Faixa(-12f, 12f)), CEM_TELHA);
        }
        CMonte(ent, c.x, c.z, Mathf.Max(w, d) * 0.4f, 8);
    }

    static void CCatapulta(Malha mad, Malha m, Vector3 p, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        float gy = Cg(p.x, p.z);
        foreach (int s in new[] { -1, 1 })
        {
            mad.Caixa(new Vector3(p.x, gy + 0.6f, p.z) + q * new Vector3(s * 1.5f, 0f, 0f), new Vector3(0.35f, 0.5f, 5.5f), q, MAD_ESC);
            mad.Caixa(new Vector3(p.x, gy + 1.6f, p.z) + q * new Vector3(s * 1.5f, 0f, 1.5f), new Vector3(0.3f, 2.4f, 0.3f), q * Quaternion.Euler(0f, 0f, s * 6f), MAD_ESC);
            mad.Cilindro(new Vector3(p.x, gy, p.z) + q * new Vector3(s * 1.8f, 0f, -1.5f), 0.65f, 0.18f, 12, MAD_ESC, q * Quaternion.Euler(0f, 0f, 90f));
        }
        mad.Caixa(new Vector3(p.x, gy + 2.9f, p.z) + q * new Vector3(0f, 0f, 1.5f), new Vector3(3.0f, 0.28f, 0.28f), q, MAD_ESC);
        mad.Caixa(new Vector3(p.x, gy + 1.6f, p.z) + q * new Vector3(0f, 0f, -1.6f), new Vector3(0.28f, 0.28f, 5.2f), q * Quaternion.Euler(-48f, 0f, 0f), MAD_ESC);   // braço quebrado
        m.Caixa(new Vector3(p.x, gy + 0.5f, p.z) + q * new Vector3(2.2f, 0f, 3.8f), new Vector3(0.9f, 0.9f, 0.9f), Quaternion.Euler(10f, 30f, 5f), CEM_PEDRA);
        for (int k = 0; k < 4; k++) m.Caixa(new Vector3(p.x, gy + 0.3f, p.z) + q * new Vector3(Faixa(-3f, 3f), 0f, Faixa(3f, 7f)), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(Faixa(0f, 40f), Faixa(0f, 90f), 0f), CEM_PEDRA);
    }

    // ------------------------------------------------------------------
    //  ruína menor de castelo (para o Reino Destruído): torre quebrada + muros + arco
    // ------------------------------------------------------------------
    static void CRuinaMenor(Transform pasta, Vector3 c, Quaternion rot, int variante)
    {
        castC = c; castRot = rot;
        Malha m = new Malha(), ent = new Malha(), mad = new Malha();
        float w = Faixa(26f, 38f), d = Faixa(20f, 28f);
        int[] nenhum = null;
        Vector3[] cant = { new Vector3(-w / 2, 0f, d / 2), new Vector3(w / 2, 0f, d / 2), new Vector3(w / 2, 0f, -d / 2), new Vector3(-w / 2, 0f, -d / 2) };
        Vector3[] fora = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        for (int l = 0; l < 4; l++)
        {
            if (variante == 1 && l == 3) continue;
            CMuro(m, ent, cant[l] + (cant[(l + 1) % 4] - cant[l]).normalized * 3f, cant[(l + 1) % 4] - (cant[(l + 1) % 4] - cant[l]).normalized * 3f, fora[l], 8.5f, 2.4f, 0.8f, nenhum, true);
        }
        for (int k = 0; k < 4; k++) if (!(variante == 1 && k == 3)) CTorre(m, ent, cant[k], 4.6f, Faixa(9f, 17f), 14, Faixa(0.5f, 0.95f), Faixa(0f, 1f));
        if (variante == 0) CTorre(m, ent, new Vector3(0f, 0f, -d * 0.1f), 7f, 24f, 18, 0.7f, 0.3f);   // torre de menagem quebrada
        else CEdificioArruinado(m, ent, mad, new Vector3(0f, 0f, -2f), 12f, 8f, Faixa(-10f, 10f), 3);
        CMonte(ent, 0f, 0f, 9f, 24);
        for (int k = 0; k < 6; k++) CEsqueleto(m, new Vector3(Faixa(-w * 0.35f, w * 0.35f), 0f, Faixa(-d * 0.35f, d * 0.35f)), Faixa(0f, 360f));
        Instanciar(m, "Ruina de Castelo", c, rot, pasta, true);
        Instanciar(ent, "Entulho da Ruina", c, rot, pasta, false);
        Instanciar(mad, "Madeira da Ruina", c, rot, pasta, false);
    }
}
#endif
