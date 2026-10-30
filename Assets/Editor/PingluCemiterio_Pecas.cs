// =====================================================================
//  PingluCemiterio_Pecas.cs  -  As peças do cemitério grande
// ---------------------------------------------------------------------
//  Muralha de tijolo com grades de ferro, portal em arco (inspirado nas
//  referências), praça com cruz, altar, mausoléus, sarcófagos, lápides.
//  As estátuas, o portão de ferro e as lanternas são modelos reais
//  (Poly Haven, CC0) em Assets/Cenario/Cemiterio/Modelos.
//  Coordenadas "locais": origem no centro do cemitério, +Z = lado do portão.
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class PingluVila
{
    const float CEM_L = 64f;   // largura (eixo X)
    const float CEM_P = 56f;   // profundidade (eixo Z)

    static Vector3 cemC;
    static Quaternion cemRot;

    // altura do chão num ponto local (relativa ao centro do cemitério)
    static float Gy(float x, float z) { return ChaoLocal(cemC, cemRot, new Vector3(x, 0f, z)); }
    static Vector3 MundoDe(Vector3 local) { return cemC + cemRot * local; }

    // perfis de moldura (alturas e quanto cada camada sai para fora)
    static readonly float[] CORN_H = { 0.12f, 0.08f, 0.14f, 0.10f, 0.20f };
    static readonly float[] CORN_S = { 0.04f, 0.10f, 0.06f, 0.17f, 0.26f };
    static readonly float[] BASE_H = { 0.22f, 0.12f, 0.10f };
    static readonly float[] BASE_S = { 0.22f, 0.13f, 0.06f };

    // camadas de moldura empilhadas (cornijas, bases, capitéis); devolve a altura total
    static float CemCamadas(Malha m, Vector3 baseCentro, Quaternion rot, float w, float d, float[] alturas, float[] saliencias, int mat)
    {
        float y = 0f;
        for (int i = 0; i < alturas.Length; i++)
        {
            m.Caixa(baseCentro + rot * new Vector3(0f, y + alturas[i] * 0.5f, 0f), new Vector3(w + 2f * saliencias[i], alturas[i], d + 2f * saliencias[i]), rot, mat);
            y += alturas[i];
        }
        return y;
    }

    // coloca um modelo importado (Poly Haven) na cena. alvo = tamanho desejado (altura ou maior largura)
    static GameObject CemModelo(string id, Transform pai, Vector3 local, float yaw, float alvo, bool porAltura, bool colisor)
    {
        string caminho = PASTA_CEM + "/Modelos/" + id + "/" + id + "_1k.gltf";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab == null)
        {
            string aviso = "Modelo não importado: " + caminho + " (confira se o pacote glTFast está instalado)";
            if (!avisos.Contains(aviso)) avisos.Add(aviso);
            return null;
        }
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.gameObject.scene);
        go.transform.SetParent(pai, true);
        go.transform.SetPositionAndRotation(Vector3.zero, cemRot * Quaternion.Euler(0f, yaw, 0f));
        Bounds b;
        if (!MedirBounds(go, out b)) { Object.DestroyImmediate(go); return null; }
        float ref0 = porAltura ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        float esc = ref0 > 0.0001f ? alvo / ref0 : 1f;
        go.transform.localScale = go.transform.localScale * esc;
        MedirBounds(go, out b);
        Vector3 mundo = MundoDe(new Vector3(local.x, 0f, local.z));
        float chao = Altura(mundo.x, mundo.z) + local.y;
        go.transform.position += new Vector3(mundo.x - b.center.x, chao - b.min.y, mundo.z - b.center.z);
        go.isStatic = true;
        if (colisor)
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        return go;
    }

    // ==================================================================
    //  MURALHA
    // ==================================================================
    static void CemMuro(Malha pedra, Malha ferro, Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; float comp = d.magnitude; d /= comp;
        Quaternion rd = Quaternion.LookRotation(d, Vector3.up);
        int n = Mathf.Max(1, Mathf.RoundToInt(comp / 4f));
        float vao = comp / n;

        for (int i = 0; i <= n; i++)
        {
            Vector3 p = a + d * (vao * i);
            float gy = Gy(p.x, p.z);
            bool caido = i > 0 && i < n && rng.NextDouble() < 0.12;
            float h = caido ? Faixa(0.6f, 1.4f) : 1.95f;
            pedra.Caixa(new Vector3(p.x, (gy - 0.7f + gy + h) * 0.5f, p.z), new Vector3(0.82f, h + 0.7f, 0.82f), rd, CEM_TIJOLO);
            if (caido) { CemCamadas(pedra, new Vector3(p.x, gy, p.z), rd, 0.82f, 0.82f, BASE_H, BASE_S, CEM_PEDRA); continue; }
            CemCamadas(pedra, new Vector3(p.x, gy, p.z), rd, 0.82f, 0.82f, BASE_H, BASE_S, CEM_PEDRA);
            float topo = gy + h;
            CemCamadas(pedra, new Vector3(p.x, topo, p.z), rd, 0.82f, 0.82f, CORN_H, CORN_S, CEM_PEDRA);
            if (i % 2 == 0) CemUrna(pedra, new Vector3(p.x, topo + 0.64f, p.z), 0.9f);
            else pedra.Piramide(new Vector3(p.x, topo + 0.64f, p.z), 0.32f, 0.36f, CEM_PEDRA);
        }

        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = a + d * (vao * i), p1 = a + d * (vao * (i + 1)), meio = (p0 + p1) * 0.5f;
            float L = vao - 0.82f;
            float gy = Gy(meio.x, meio.z);
            bool ruina = rng.NextDouble() < 0.15;
            float h = ruina ? Faixa(0.35f, 0.9f) : 1.2f;
            pedra.Caixa(new Vector3(meio.x, (gy - 0.7f + gy + h) * 0.5f, meio.z), new Vector3(0.5f, h + 0.7f, L), rd, CEM_TIJOLO);
            pedra.Caixa(new Vector3(meio.x, gy + 0.2f, meio.z), new Vector3(0.62f, 0.4f, L), rd, CEM_PEDRA);
            if (ruina)
            {
                for (int k = 0; k < 4; k++)
                {
                    Vector3 o = meio + rd * new Vector3(Faixa(-0.8f, 0.8f), 0f, Faixa(-L * 0.45f, L * 0.45f));
                    float t = Faixa(0.28f, 0.6f);
                    pedra.Caixa(new Vector3(o.x, Gy(o.x, o.z) + t * 0.3f, o.z), new Vector3(t, t * 0.7f, t * 1.1f),
                        Quaternion.Euler(Faixa(-20f, 20f), Faixa(0f, 360f), Faixa(-20f, 20f)), CEM_PEDRA);
                }
                continue;
            }
            pedra.Caixa(new Vector3(meio.x, gy + h + 0.06f, meio.z), new Vector3(0.68f, 0.12f, L + 0.02f), rd, CEM_PEDRA);
            float yb = gy + h + 0.12f;
            int barras = Mathf.Max(3, Mathf.RoundToInt(L / 0.2f));
            for (int k = 0; k < barras; k++)
            {
                Vector3 q = p0 + d * (0.41f + L * (k + 0.5f) / barras);
                float alt = 1.1f + (k % 4 == 0 ? 0.14f : 0f);
                ferro.Caixa(new Vector3(q.x, yb + alt * 0.5f, q.z), new Vector3(0.035f, alt, 0.035f), FERRO);
                ferro.Piramide(new Vector3(q.x, yb + alt, q.z), 0.028f, 0.14f, FERRO);
            }
            ferro.Caixa(new Vector3(meio.x, yb + 0.2f, meio.z), new Vector3(0.045f, 0.045f, L), rd, FERRO);
            ferro.Caixa(new Vector3(meio.x, yb + 0.9f, meio.z), new Vector3(0.045f, 0.045f, L), rd, FERRO);
        }

        // parede invisível alta, para ninguém pular a grade
        GameObject col = new GameObject("Colisor da Muralha");
        col.transform.SetParent(Pasta("Colisores"), false);
        Vector3 mundo = MundoDe((a + b) * 0.5f);
        col.transform.position = new Vector3(mundo.x, Altura(mundo.x, mundo.z) + 1.6f, mundo.z);
        col.transform.rotation = cemRot * rd;
        BoxCollider bc = col.AddComponent<BoxCollider>();
        bc.size = new Vector3(0.7f, 3.2f, comp);
    }

    static void CemMuralha(Malha pedra, Malha ferro)
    {
        float hx = CEM_L * 0.5f, hz = CEM_P * 0.5f;
        float inicioFrente = 11.5f;   // onde a muralha começa ao lado do portal
        float vaoLado = 2.2f;
        float zLado = 6f;
        CemMuro(pedra, ferro, new Vector3(-hx, 0f, hz), new Vector3(-inicioFrente, 0f, hz));
        CemMuro(pedra, ferro, new Vector3(inicioFrente, 0f, hz), new Vector3(hx, 0f, hz));
        CemMuro(pedra, ferro, new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, -hz));
        foreach (int s in new[] { -1, 1 })
        {
            CemMuro(pedra, ferro, new Vector3(s * hx, 0f, hz), new Vector3(s * hx, 0f, zLado + vaoLado));
            CemMuro(pedra, ferro, new Vector3(s * hx, 0f, zLado - vaoLado), new Vector3(s * hx, 0f, -hz));
        }
    }

    // ==================================================================
    //  PORTAL EM ARCO (como a referência)
    // ==================================================================
    static void CemPortal(Malha pedra, Malha ferro, Transform pastaLuzes)
    {
        float z0 = CEM_P * 0.5f;
        float R = 3.0f, springY = 4.95f;
        float gy = Gy(0f, z0);

        // dois pilares grandes com cornija em camadas
        foreach (int s in new[] { -1, 1 })
        {
            float x = s * 4.6f;
            pedra.Caixa(new Vector3(x, (gy - 0.8f + 4.3f) * 0.5f, z0), new Vector3(3.0f, 4.3f - gy + 0.8f, 2.8f), CEM_TIJOLO);
            CemCamadas(pedra, new Vector3(x, gy, z0), Quaternion.identity, 3.0f, 2.8f, BASE_H, BASE_S, CEM_PEDRA);
            CemCamadas(pedra, new Vector3(x, 4.3f, z0), Quaternion.identity, 3.0f, 2.8f, new[] { 0.12f, 0.08f, 0.14f, 0.10f, 0.22f }, new[] { 0.04f, 0.12f, 0.07f, 0.2f, 0.3f }, CEM_PEDRA);
            // faixas verticais nas quinas
            foreach (int sz in new[] { -1, 1 })
                pedra.Caixa(new Vector3(x, 2.2f, z0 + sz * 1.43f), new Vector3(2.5f, 3.4f, 0.06f), CEM_PEDRA);
            pedra.Cilindro(new Vector3(x - s * 1.2f, gy, z0 + 1.52f), 0.17f, 4.1f - gy, 10, CEM_PEDRA, Quaternion.identity);
        }
        // bloco superior com abertura em arco
        for (float y = springY; y < springY + 3.7f; y += 0.3f)
        {
            float t0 = y - springY;
            float hw = t0 < R + 0.45f ? Mathf.Sqrt(Mathf.Max(0f, (R + 0.45f) * (R + 0.45f) - t0 * t0)) : 0f;
            if (hw <= 0.01f) { pedra.Caixa(new Vector3(0f, y + 0.15f, z0), new Vector3(12.4f, 0.3f, 2.4f), CEM_TIJOLO); continue; }
            float larg = 6.2f - hw;
            if (larg < 0.05f) continue;
            foreach (int s in new[] { -1, 1 })
                pedra.Caixa(new Vector3(s * (hw + 6.2f) * 0.5f, y + 0.15f, z0), new Vector3(larg, 0.3f, 2.4f), CEM_TIJOLO);
        }
        // arco: duas voltas de aduelas, a de fora mais saliente
        for (int volta = 0; volta < 2; volta++)
        {
            int N = 21;
            float r0 = volta == 0 ? R : R + 0.5f, esp = volta == 0 ? 0.5f : 0.4f, prof = volta == 0 ? 2.5f : 2.7f;
            float rc = r0 + esp * 0.5f;
            for (int k = 0; k < N; k++)
            {
                float th = (k + 0.5f) / N * Mathf.PI;
                Vector3 c = new Vector3(rc * Mathf.Cos(th), springY + rc * Mathf.Sin(th), z0);
                float arco = Mathf.PI * rc / N * 1.04f;
                pedra.Caixa(c, new Vector3(esp, arco, prof), Quaternion.Euler(0f, 0f, th * Mathf.Rad2Deg), CEM_PEDRA);
            }
        }
        // fecho do arco com mão-francesa
        pedra.Caixa(new Vector3(0f, springY + R + 0.55f, z0 + 0.1f), new Vector3(0.9f, 1.2f, 2.9f), CEM_PEDRA);
        pedra.Tronco(new Vector3(0f, springY + R - 0.7f, z0 + 1.2f), 0.14f, 0.36f, 1.2f, CEM_PEDRA);
        // medalhões nos cantos do arco
        foreach (int s in new[] { -1, 1 })
            foreach (int f in new[] { -1, 1 })
            {
                Vector3 c = new Vector3(s * 4.2f, springY + 2.7f, z0 + f * 1.2f);
                pedra.Cilindro(c, 0.55f, 0.08f, 18, CEM_PEDRA, Quaternion.Euler(f * 90f, 0f, 0f));
                pedra.Cilindro(c + new Vector3(0f, 0f, f * 0.05f), 0.38f, 0.07f, 18, CEM_LAPIDE, Quaternion.Euler(f * 90f, 0f, 0f));
            }
        // cornija do ático e telhado de telhas
        CemCamadas(pedra, new Vector3(0f, springY + 3.7f, z0), Quaternion.identity, 12.4f, 2.4f, new[] { 0.14f, 0.10f, 0.16f, 0.12f, 0.24f }, new[] { 0.05f, 0.14f, 0.08f, 0.22f, 0.34f }, CEM_PEDRA);
        float yt = springY + 3.7f + 0.76f;
        float[] lg = { 12.6f, 11.4f, 9.6f, 7.0f, 4.4f };
        float[] pf = { 3.0f, 2.6f, 2.1f, 1.6f, 1.1f };
        for (int i = 0; i < lg.Length; i++)
            pedra.Caixa(new Vector3(0f, yt + 0.16f + i * 0.3f, z0), new Vector3(lg[i], 0.3f, pf[i]), CEM_TELHA);
        pedra.Cilindro(new Vector3(-2.4f, yt + 1.8f, z0 - 0.35f), 0.28f, 4.8f, 12, CEM_PEDRA, Quaternion.Euler(0f, 0f, -90f));
        pedra.Cilindro(new Vector3(-2.4f, yt + 1.8f, z0 + 0.35f), 0.28f, 4.8f, 12, CEM_PEDRA, Quaternion.Euler(0f, 0f, -90f));
        pedra.Caixa(new Vector3(0f, yt + 2.15f, z0), new Vector3(1.7f, 1.0f, 0.5f), CEM_PEDRA);
        pedra.Caixa(new Vector3(0f, yt + 2.8f, z0), new Vector3(0.9f, 0.4f, 0.5f), CEM_PEDRA);

        // vãos laterais com portinhola de ferro e pilares com urnas
        foreach (int s in new[] { -1, 1 })
        {
            float xb = s * 7.4f;
            float aberturaL = 1.3f, aberturaH = 2.5f;
            // bloco do vão dividido ao redor da portinhola
            pedra.Caixa(new Vector3(xb - s * (aberturaL * 0.5f + 0.3f), (gy - 0.7f + 3.4f) * 0.5f, z0 - 0.15f), new Vector3(0.6f, 3.4f - gy + 0.7f, 2.2f), CEM_TIJOLO);
            pedra.Caixa(new Vector3(xb + s * (aberturaL * 0.5f + 0.3f), (gy - 0.7f + 3.4f) * 0.5f, z0 - 0.15f), new Vector3(0.6f, 3.4f - gy + 0.7f, 2.2f), CEM_TIJOLO);
            pedra.Caixa(new Vector3(xb, (gy + aberturaH + 3.4f) * 0.5f, z0 - 0.15f), new Vector3(aberturaL + 1.2f, 3.4f - gy - aberturaH, 2.2f), CEM_TIJOLO);
            pedra.Caixa(new Vector3(xb, gy + aberturaH + 0.06f, z0 + 1.0f), new Vector3(aberturaL + 0.5f, 0.12f, 0.3f), CEM_PEDRA);
            foreach (int sj in new[] { -1, 1 })
                pedra.Caixa(new Vector3(xb + sj * (aberturaL * 0.5f + 0.1f), gy + aberturaH * 0.5f, z0 + 0.98f), new Vector3(0.2f, aberturaH, 0.2f), CEM_PEDRA);
            pedra.Caixa(new Vector3(xb, gy + aberturaH * 0.5f, z0 - 0.7f), new Vector3(aberturaL, aberturaH, 0.08f), COURO);  // fundo escuro atrás das grades
            for (int k = 0; k <= 8; k++)
            {
                float bx = xb - aberturaL * 0.5f + 0.08f + k * (aberturaL - 0.16f) / 8f;
                ferro.Caixa(new Vector3(bx, gy + aberturaH * 0.5f, z0 + 0.7f), new Vector3(0.03f, aberturaH, 0.03f), FERRO);
                ferro.Piramide(new Vector3(bx, gy + aberturaH, z0 + 0.7f), 0.025f, 0.1f, FERRO);
            }
            foreach (float yy in new[] { 0.3f, 1.2f, 2.2f })
                ferro.Caixa(new Vector3(xb, gy + yy, z0 + 0.7f), new Vector3(aberturaL, 0.04f, 0.04f), FERRO);
            CemCamadas(pedra, new Vector3(xb, 3.4f, z0 - 0.15f), Quaternion.identity, 2.4f, 2.2f, new[] { 0.1f, 0.08f, 0.12f, 0.16f }, new[] { 0.04f, 0.1f, 0.06f, 0.2f }, CEM_PEDRA);

            // pilar do canto com urna
            float xp = s * 9.9f;
            pedra.Caixa(new Vector3(xp, (gy - 0.7f + 3.4f) * 0.5f, z0), new Vector3(2.0f, 3.4f - gy + 0.7f, 2.0f), CEM_TIJOLO);
            CemCamadas(pedra, new Vector3(xp, gy, z0), Quaternion.identity, 2.0f, 2.0f, BASE_H, BASE_S, CEM_PEDRA);
            float h2 = CemCamadas(pedra, new Vector3(xp, 3.4f, z0), Quaternion.identity, 2.0f, 2.0f, new[] { 0.12f, 0.08f, 0.14f, 0.10f, 0.2f }, new[] { 0.04f, 0.12f, 0.06f, 0.18f, 0.26f }, CEM_PEDRA);
            CemUrna(pedra, new Vector3(xp, 3.4f + h2, z0), 2.1f);
        }

        // portão de ferro de verdade (modelo), aberto
        bool portao = CemPortaoDeFerro();
        if (!portao)
            foreach (int s in new[] { -1, 1 })
            {
                float ang = s < 0 ? 78f : 38f;
                Quaternion rg = Quaternion.Euler(0f, -s * ang, 0f);
                Vector3 dob = new Vector3(s * 2.85f, 0f, z0 - 0.2f);
                float yg = Gy(dob.x, dob.z) + 0.1f;
                for (int k = 0; k <= 12; k++)
                {
                    Vector3 off = rg * new Vector3(-s * (0.1f + k * 0.22f), 0f, 0f);
                    ferro.Caixa(dob + off + Vector3.up * (yg + 1.7f), new Vector3(0.04f, 3.4f, 0.04f), FERRO);
                    ferro.Piramide(dob + off + Vector3.up * (yg + 3.4f), 0.03f, 0.15f, FERRO);
                }
            }

        // postes de pedra com lanternas de verdade na frente
        foreach (int s in new[] { -1, 1 })
        {
            Vector3 p = new Vector3(s * 3.9f, 0f, z0 + 2.2f);
            float g = Gy(p.x, p.z);
            pedra.Caixa(new Vector3(p.x, (g - 0.5f + 2.0f) * 0.5f, p.z), new Vector3(0.7f, 2.0f - g + 0.5f, 0.7f), CEM_TIJOLO);
            CemCamadas(pedra, new Vector3(p.x, g, p.z), Quaternion.identity, 0.7f, 0.7f, BASE_H, BASE_S, CEM_PEDRA);
            CemCamadas(pedra, new Vector3(p.x, 2.0f, p.z), Quaternion.identity, 0.7f, 0.7f, new[] { 0.1f, 0.08f, 0.14f }, new[] { 0.04f, 0.1f, 0.16f }, CEM_PEDRA);
            CemModelo("Lantern_01", Pasta("Modelos"), new Vector3(p.x, 2.32f, p.z), 0f, 0.7f, true, false);
            CemLuz(pastaLuzes, new Vector3(p.x, 2.55f, p.z), new Color(1f, 0.55f, 0.25f), 13f, 3.4f);
        }
    }

    // modelo "large_iron_gate", escalado para o vão do arco, com as duas folhas abertas
    static bool CemPortaoDeFerro()
    {
        GameObject go = CemModelo("large_iron_gate", Pasta("Modelos"), new Vector3(0f, 0.05f, CEM_P * 0.5f - 0.1f), 0f, 5.7f, false, false);
        if (go == null) return false;
        Bounds total;
        if (!MedirBounds(go, out total)) { Object.DestroyImmediate(go); return false; }
        List<Renderer> folhas = new List<Renderer>();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            if (r.bounds.size.y > total.size.y * 0.5f && r.bounds.size.x > total.size.x * 0.3f && r.bounds.size.x < total.size.x * 0.7f) folhas.Add(r);
        if (folhas.Count != 2) { avisos.Add("O portão de ferro não abriu (achei " + folhas.Count + " folhas); usei o portão simples."); Object.DestroyImmediate(go); return false; }
        foreach (Renderer r in folhas)
        {
            Bounds b = r.bounds;
            Vector3 eixoLargura = cemRot * Vector3.right;
            float lado = Vector3.Dot(b.center - total.center, eixoLargura);
            Vector3 dobradica = lado < 0f ? b.center - eixoLargura * (b.extents.x) : b.center + eixoLargura * (b.extents.x);
            dobradica = new Vector3(dobradica.x, b.center.y, dobradica.z);
            r.transform.RotateAround(dobradica, Vector3.up, lado < 0f ? 80f : -80f);
        }
        return true;
    }

    // urna decorativa (peça de revolução)
    static void CemUrna(Malha m, Vector3 baseCentro, float escala)
    {
        float[] r = { 0.2f, 0.13f, 0.24f, 0.3f, 0.24f, 0.14f, 0.17f, 0.1f, 0.05f, 0f };
        float[] h = { 0f, 0.1f, 0.3f, 0.5f, 0.72f, 0.86f, 0.94f, 1.04f, 1.12f, 1.2f };
        float[] rr = new float[r.Length], hh = new float[h.Length];
        for (int i = 0; i < r.Length; i++) { rr[i] = r[i] * escala; hh[i] = h[i] * escala; }
        m.Torno(baseCentro, rr, hh, 12, CEM_PEDRA, Quaternion.identity);
        foreach (int s in new[] { -1, 1 })
            m.Caixa(baseCentro + new Vector3(s * 0.3f * escala, 0.62f * escala, 0f), new Vector3(0.05f * escala, 0.28f * escala, 0.05f * escala), CEM_PEDRA);
    }

    // ==================================================================
    //  VELAS E LUZES
    // ==================================================================
    static void CemVelas(Malha m, Vector3 p, int n, float raio)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 o = p + new Vector3(Faixa(-raio, raio), 0f, Faixa(-raio, raio));
            float h = Faixa(0.07f, 0.38f), r = Faixa(0.024f, 0.042f);
            m.Cilindro(o, r * 2.4f, 0.025f, 7, REBOCO_CREME, Quaternion.identity);
            m.Cilindro(o, r, h, 7, REBOCO_CREME, Quaternion.identity);
            m.Piramide(o + Vector3.up * h, r * 0.7f, r * 3.2f, BRASA);
        }
    }

    static void CemLuz(Transform pai, Vector3 local, Color cor, float alcance, float intensidade)
    {
        GameObject lg = new GameObject("Luz");
        lg.transform.SetParent(pai, false);
        lg.transform.position = MundoDe(local);
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = alcance; l.intensity = intensidade; l.color = cor; l.shadows = LightShadows.None;
        luzesCriadas++;
    }

    // ==================================================================
    //  PRAÇA, CRUZ E ALTAR
    // ==================================================================
    static void CemPraca(Malha m, Transform pastaLuzes)
    {
        float y = Gy(0f, 0f);
        m.Cilindro(new Vector3(0f, y - 0.4f, 0f), 11.8f, 0.5f, 36, CEM_PEDRA, Quaternion.identity);
        m.Cilindro(new Vector3(0f, y - 0.4f, 0f), 11.2f, 0.56f, 36, CEM_PRACA, Quaternion.identity);
        m.Cilindro(new Vector3(0f, y - 0.4f, 0f), 7.2f, 0.62f, 32, CEM_PISO, Quaternion.identity);
        m.Cilindro(new Vector3(0f, y - 0.4f, 0f), 6.6f, 0.66f, 32, CEM_PRACA, Quaternion.identity);
        // monumento central: degraus, coluna e cruz celta
        CemCamadas(m, new Vector3(0f, y + 0.27f, 0f), Quaternion.identity, 3.3f, 3.3f, new[] { 0.28f, 0.28f, 0.28f }, new[] { 0.9f, 0.55f, 0.2f }, CEM_PEDRA);
        CemCamadas(m, new Vector3(0f, y + 1.11f, 0f), Quaternion.identity, 1.4f, 1.4f, BASE_H, BASE_S, CEM_PEDRA);
        m.Caixa(new Vector3(0f, y + 2.35f, 0f), new Vector3(1.3f, 1.8f, 1.3f), CEM_LAPIDE);
        float hc = CemCamadas(m, new Vector3(0f, y + 3.25f, 0f), Quaternion.identity, 1.3f, 1.3f, CORN_H, CORN_S, CEM_PEDRA);
        float yc = y + 3.25f + hc;
        m.Caixa(new Vector3(0f, yc + 1.7f, 0f), new Vector3(0.55f, 3.4f, 0.42f), CEM_LAPIDE);
        m.Caixa(new Vector3(0f, yc + 2.5f, 0f), new Vector3(2.3f, 0.55f, 0.42f), CEM_LAPIDE);
        for (int g = 0; g < 16; g++)
        {
            float a = g * Mathf.PI * 2f / 16f;
            m.Caixa(new Vector3(Mathf.Cos(a) * 0.8f, yc + 2.5f + Mathf.Sin(a) * 0.8f, 0f), new Vector3(0.2f, 0.4f, 0.36f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), CEM_PEDRA);
        }
        m.Caixa(new Vector3(0f, yc + 3.55f, 0f), new Vector3(0.42f, 0.42f, 0.36f), Quaternion.Euler(0f, 0f, 45f), CEM_PEDRA);
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
            CemVelas(m, new Vector3(Mathf.Cos(a) * 2.1f, y + 0.6f, Mathf.Sin(a) * 2.1f), 6, 0.35f);
        }
        CemLuz(pastaLuzes, new Vector3(0f, 2.2f, 0f), new Color(1f, 0.72f, 0.45f), 16f, 2.6f);
    }

    static void CemAltar(Malha m, Transform pastaLuzes)
    {
        float z = -14f, y = Gy(0f, z);
        CemCamadas(m, new Vector3(0f, y - 0.05f, z), Quaternion.identity, 5.8f, 2.6f, BASE_H, BASE_S, CEM_PEDRA);
        m.Caixa(new Vector3(0f, y + 0.9f, z), new Vector3(5.6f, 1.2f, 2.4f), CEM_PEDRA);
        for (int k = -1; k <= 1; k++)
            m.Caixa(new Vector3(k * 1.8f, y + 0.85f, z + 1.22f), new Vector3(1.4f, 0.8f, 0.08f), CEM_LAPIDE);
        CemCamadas(m, new Vector3(0f, y + 1.5f, z), Quaternion.identity, 5.6f, 2.4f, CORN_H, CORN_S, CEM_PEDRA);
        m.Caixa(new Vector3(0f, y + 3.5f, z - 0.8f), new Vector3(0.9f, 3.6f, 0.5f), CEM_LAPIDE);
        m.Caixa(new Vector3(0f, y + 4.1f, z - 0.8f), new Vector3(2.8f, 0.7f, 0.5f), CEM_LAPIDE);
        for (int k = 0; k < 5; k++) CemVelas(m, new Vector3(-2.2f + k * 1.1f, y + 2.3f, z + Faixa(-0.5f, 0.5f)), 5, 0.3f);
        foreach (int s in new[] { -1, 1 })
        {
            float x = s * 6.8f;
            CemCamadas(m, new Vector3(x, y - 0.05f, z - 1.2f), Quaternion.identity, 1.3f, 1.3f, BASE_H, BASE_S, CEM_PEDRA);
            m.Caixa(new Vector3(x, y + 1.7f, z - 1.2f), new Vector3(1.0f, 2.2f, 1.0f), CEM_LAPIDE);
            m.Caixa(new Vector3(x, y + 3.7f, z - 1.2f), new Vector3(0.38f, 1.9f, 0.3f), CEM_LAPIDE);
            m.Caixa(new Vector3(x, y + 4.15f, z - 1.2f), new Vector3(1.4f, 0.38f, 0.3f), CEM_LAPIDE);
            CemVelas(m, new Vector3(x, y + 0.9f, z - 0.4f), 5, 0.4f);
        }
        CemLuz(pastaLuzes, new Vector3(0f, 2.8f, z + 1.5f), new Color(1f, 0.7f, 0.42f), 14f, 2.6f);
    }

    // ==================================================================
    //  CAMINHO DE PARALELEPÍPEDOS DO PORTÃO ATÉ A PRAÇA
    // ==================================================================
    static void CemCaminho(Malha m)
    {
        float z0 = CEM_P * 0.5f + 2f, z1 = 11f;
        for (float z = z0; z > z1; z -= 4f)
        {
            float zc = z - 2f, comp = Mathf.Min(4f, z - z1);
            if (comp < 0.5f) break;
            float y = Gy(0f, zc);
            m.Caixa(new Vector3(0f, y + 0.0f, z - comp * 0.5f), new Vector3(5.2f, 0.14f, comp + 0.02f), CEM_PISO);
        }
        foreach (int s in new[] { -1, 1 })
            m.Caixa(new Vector3(s * 2.7f, Gy(s * 2.7f, 20f) + 0.05f, (z0 + z1) * 0.5f), new Vector3(0.28f, 0.2f, z0 - z1), CEM_PEDRA);
    }

    // ==================================================================
    //  MAUSOLÉU COM NICHOS
    // ==================================================================
    static void CemMausoleu(Malha m, Vector3 pos, float yaw, float escala, Transform pastaLuzes)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        float W = 5.4f * escala, D = 5.2f * escala, H = 4.3f * escala;
        float y = Gy(pos.x, pos.z);
        Vector3 o = new Vector3(pos.x, y, pos.z);

        CemCamadas(m, o + q * new Vector3(0f, -0.1f, 0f), q, W, D, BASE_H, BASE_S, CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, H * 0.5f + 0.25f, -D * 0.5f + 0.2f), new Vector3(W, H, 0.4f), CEM_TIJOLO);
        foreach (int s in new[] { -1, 1 })
        {
            Local(m, o, q, new Vector3(s * (W * 0.5f - 0.2f), H * 0.5f + 0.25f, 0f), new Vector3(0.4f, H, D), CEM_TIJOLO);
            for (int r = 0; r < 3; r++)
            {
                Local(m, o, q, new Vector3(s * (W * 0.5f - 0.75f), 0.75f + r * 1.1f, 0f), new Vector3(0.7f, 0.07f, D - 1.2f), CEM_PEDRA);
                for (int k = 0; k < 2; k++)
                    CemVelas(m, o + q * new Vector3(s * (W * 0.5f - 0.75f), 0.8f + r * 1.1f, (k - 0.5f) * (D - 2f)), 3, 0.18f);
            }
        }
        float abertura = 2.5f * escala;
        float pil = (W - abertura) * 0.5f;
        foreach (int s in new[] { -1, 1 })
        {
            Local(m, o, q, new Vector3(s * (abertura * 0.5f + pil * 0.5f), H * 0.5f + 0.25f, D * 0.5f - 0.2f), new Vector3(pil, H, 0.5f), CEM_TIJOLO);
            Local(m, o, q, new Vector3(s * (abertura * 0.5f + 0.05f), 1.6f, D * 0.5f + 0.1f), new Vector3(0.3f, 3.0f, 0.4f), CEM_PEDRA);
            m.Caixa(o + q * new Vector3(s * (abertura * 0.5f - 0.2f), 3.2f * escala + 0.3f, D * 0.5f - 0.1f), new Vector3(0.9f, 0.4f, 0.6f), q * Quaternion.Euler(0f, 0f, -s * 40f), CEM_PEDRA);
        }
        float yAbertura = 3.0f * escala;
        Local(m, o, q, new Vector3(0f, (yAbertura + H) * 0.5f + 0.25f, D * 0.5f - 0.2f), new Vector3(abertura + 0.2f, H - yAbertura, 0.5f), CEM_TIJOLO);
        Local(m, o, q, new Vector3(0f, yAbertura + 0.1f, D * 0.5f + 0.1f), new Vector3(abertura + 0.5f, 0.28f, 0.5f), CEM_PEDRA);
        CemCamadas(m, o + q * new Vector3(0f, H + 0.25f, 0f), q, W, D, CORN_H, CORN_S, CEM_PEDRA);
        float[] lg = { 1f, 0.78f, 0.56f, 0.34f };
        for (int i = 0; i < lg.Length; i++)
            Local(m, o, q, new Vector3(0f, H + 1.15f + i * 0.5f, D * 0.5f - 0.2f), new Vector3(W * lg[i], 0.5f, 0.55f), CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, H + 3.5f, D * 0.5f - 0.2f), new Vector3(0.22f, 1.1f, 0.2f), CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, H + 3.7f, D * 0.5f - 0.2f), new Vector3(0.7f, 0.2f, 0.2f), CEM_PEDRA);
        foreach (int s in new[] { -1, 1 })
            m.Caixa(o + q * new Vector3(s * W * 0.25f, H + 1.55f, -0.2f), new Vector3(W * 0.62f, 0.26f, D + 0.5f), q * Quaternion.Euler(0f, 0f, -s * 24f), CEM_TELHA);
        Local(m, o, q, new Vector3(0f, H + 1.8f, -0.2f), new Vector3(0.3f, 0.26f, D + 0.5f), CEM_PEDRA);
        for (int k = 0; k < 3; k++)
            Local(m, o, q, new Vector3(0f, 0.05f - k * 0.18f, D * 0.5f + 0.6f + k * 0.45f), new Vector3(abertura + 1.2f, 0.28f, 0.5f), CEM_PEDRA);
        CemVelas(m, o + q * new Vector3(-abertura * 0.5f - 0.5f, 0.25f, D * 0.5f + 0.6f), 5, 0.3f);
        CemVelas(m, o + q * new Vector3(abertura * 0.5f + 0.5f, 0.25f, D * 0.5f + 0.6f), 5, 0.3f);
        Local(m, o, q, new Vector3(0f, 0.2f, 0f), new Vector3(W - 0.5f, 0.1f, D - 0.5f), CEM_PRACA);
        CemLuz(pastaLuzes, o + q * new Vector3(0f, 2.0f, 0.3f), new Color(1f, 0.66f, 0.38f), 9f * escala, 2.4f);
    }

    // ==================================================================
    //  SARCÓFAGOS
    // ==================================================================
    static void CemSarcofago(Malha m, Vector3 pos, float yaw, int estilo)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 o = new Vector3(pos.x, Gy(pos.x, pos.z), pos.z);
        CemCamadas(m, o + q * new Vector3(0f, -0.05f, 0f), q, 1.3f, 2.7f, BASE_H, BASE_S, CEM_PEDRA);
        if (estilo == 0) // aberto: paredes grossas e fundo escuro
        {
            Local(m, o, q, new Vector3(0f, 0.7f, 0f), new Vector3(1.25f, 1.0f, 2.7f), CEM_PEDRA);
            Local(m, o, q, new Vector3(0f, 1.22f, 0f), new Vector3(1.3f, 0.06f, 2.75f), CEM_LAPIDE);
            foreach (int s in new[] { -1, 1 })
            {
                Local(m, o, q, new Vector3(s * 0.52f, 1.28f, 0f), new Vector3(0.2f, 0.14f, 2.7f), CEM_PEDRA);
                Local(m, o, q, new Vector3(0f, 1.28f, s * 1.28f), new Vector3(1.25f, 0.14f, 0.2f), CEM_PEDRA);
            }
            Local(m, o, q, new Vector3(0f, 1.1f, 0f), new Vector3(0.85f, 0.04f, 2.3f), COURO);
            m.Caixa(o + q * new Vector3(1.4f, 0.55f, 0.4f), new Vector3(1.3f, 0.2f, 2.8f), q * Quaternion.Euler(0f, 8f, 52f), CEM_PEDRA);
        }
        else // fechado, tampa em camadas
        {
            Local(m, o, q, new Vector3(0f, 0.7f, 0f), new Vector3(1.25f, 1.0f, 2.7f), CEM_PEDRA);
            CemCamadas(m, o + q * new Vector3(0f, 1.2f, 0f), q, 1.25f, 2.7f, new[] { 0.1f, 0.14f, 0.12f }, new[] { 0.08f, 0.04f, -0.12f }, CEM_PEDRA);
        }
        // baixos-relevos em molduras
        for (int k = 0; k < 3; k++)
            foreach (int s in new[] { -1, 1 })
            {
                Local(m, o, q, new Vector3(s * 0.64f, 0.7f, -0.85f + k * 0.85f), new Vector3(0.05f, 0.62f, 0.62f), CEM_PEDRA);
                Local(m, o, q, new Vector3(s * 0.67f, 0.7f, -0.85f + k * 0.85f), new Vector3(0.05f, 0.44f, 0.44f), CEM_LAPIDE);
            }
    }

    // ==================================================================
    //  LÁPIDES
    // ==================================================================
    static void CemObelisco(Malha m, Vector3 pos)
    {
        float y = Gy(pos.x, pos.z);
        Quaternion r = Quaternion.Euler(Faixa(-2.5f, 2.5f), Faixa(0f, 360f), Faixa(-2.5f, 2.5f));
        Vector3 b = new Vector3(pos.x, y - 0.1f, pos.z);
        float h0 = CemCamadas(m, b, r, 0.9f, 0.9f, BASE_H, BASE_S, CEM_PEDRA);
        m.Caixa(b + r * new Vector3(0f, h0 + 0.35f, 0f), new Vector3(0.78f, 0.7f, 0.78f), r, CEM_LAPIDE);
        m.Tronco(b + r * new Vector3(0f, h0 + 0.7f, 0f), 0.34f, 0.2f, 1.9f, CEM_LAPIDE);
        m.Piramide(b + r * new Vector3(0f, h0 + 2.6f, 0f), 0.2f, 0.32f, CEM_PEDRA);
    }

    static void CemCruzCelta(Malha m, Vector3 pos, float escala)
    {
        float y = Gy(pos.x, pos.z);
        Quaternion r = Quaternion.Euler(Faixa(-3f, 3f), Faixa(-30f, 30f), Faixa(-3f, 3f));
        Vector3 b = new Vector3(pos.x, y - 0.1f, pos.z);
        float h0 = CemCamadas(m, b, r, 0.8f * escala, 0.6f * escala, BASE_H, BASE_S, CEM_PEDRA);
        m.Caixa(b + r * new Vector3(0f, h0 + 0.7f * escala, 0f), new Vector3(0.24f, 1.4f, 0.18f) * escala, r, CEM_LAPIDE);
        m.Caixa(b + r * new Vector3(0f, h0 + 1.15f * escala, 0f), new Vector3(0.9f, 0.22f, 0.18f) * escala, r, CEM_LAPIDE);
        for (int g = 0; g < 12; g++)
        {
            float a = g * Mathf.PI * 2f / 12f;
            m.Caixa(b + r * (new Vector3(Mathf.Cos(a) * 0.3f, h0 + 1.15f * escala + Mathf.Sin(a) * 0.3f, 0f) * 1f), new Vector3(0.08f, 0.14f, 0.12f) * escala, r * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), CEM_PEDRA);
        }
    }

    // estilo: 0 arredondada, 1 gótica (pontuda), 2 com cruz no alto, 3 arredondada baixa, 4 obelisco, 5 cruz celta, 6 caída/quebrada, 7 túmulo em caixa
    static void CemTumulo(Malha m, Vector3 pos, int estilo)
    {
        float y = Gy(pos.x, pos.z);
        if (estilo == 4) { CemObelisco(m, pos); return; }
        if (estilo == 5) { CemCruzCelta(m, pos, Faixa(0.9f, 1.3f)); return; }
        Quaternion r = Quaternion.Euler(Faixa(-5f, 5f), Faixa(-12f, 12f), Faixa(-5f, 5f));
        Vector3 b = new Vector3(pos.x, y - 0.1f, pos.z);
        float h0 = CemCamadas(m, b, r, 0.8f, 0.34f, new[] { 0.16f, 0.1f }, new[] { 0.16f, 0.08f }, CEM_PEDRA);
        Vector3 c0 = b + r * new Vector3(0f, h0, 0f);
        switch (estilo)
        {
            case 0:
            case 3:
            {
                float alt = estilo == 0 ? 0.95f : 0.6f;
                m.Caixa(c0 + r * new Vector3(0f, alt * 0.5f, 0f), new Vector3(0.68f, alt, 0.16f), r, CEM_LAPIDE);
                m.Cilindro(c0 + r * new Vector3(0f, alt, -0.08f), 0.34f, 0.16f, 14, CEM_LAPIDE, r * Quaternion.Euler(90f, 0f, 0f));
                m.Caixa(c0 + r * new Vector3(0f, alt * 0.55f, 0.1f), new Vector3(0.5f, alt * 0.55f, 0.04f), r, CEM_PEDRA);
                break;
            }
            case 1:
                m.Caixa(c0 + r * new Vector3(0f, 0.45f, 0f), new Vector3(0.66f, 0.9f, 0.17f), r, CEM_LAPIDE);
                foreach (int s in new[] { -1, 1 })
                    m.Caixa(c0 + r * new Vector3(s * 0.18f, 1.02f, 0f), new Vector3(0.42f, 0.12f, 0.17f), r * Quaternion.Euler(0f, 0f, -s * 42f), CEM_LAPIDE);
                m.Piramide(c0 + r * new Vector3(0f, 1.22f, 0f), 0.06f, 0.2f, CEM_PEDRA);
                m.Caixa(c0 + r * new Vector3(0f, 0.5f, 0.1f), new Vector3(0.38f, 0.5f, 0.04f), r, CEM_PEDRA);
                break;
            case 2:
                m.Caixa(c0 + r * new Vector3(0f, 0.4f, 0f), new Vector3(0.7f, 0.8f, 0.17f), r, CEM_LAPIDE);
                m.Caixa(c0 + r * new Vector3(0f, 0.98f, 0f), new Vector3(0.14f, 0.5f, 0.12f), r, CEM_LAPIDE);
                m.Caixa(c0 + r * new Vector3(0f, 1.05f, 0f), new Vector3(0.45f, 0.13f, 0.12f), r, CEM_LAPIDE);
                break;
            case 6:
                m.Caixa(c0 + r * new Vector3(0f, 0.3f, 0f), new Vector3(0.66f, 0.6f, 0.16f), r * Quaternion.Euler(0f, 0f, Faixa(8f, 16f)), CEM_LAPIDE);
                m.Caixa(c0 + new Vector3(0.5f, 0.1f, -0.3f), new Vector3(0.3f, 0.2f, 0.22f), r * Quaternion.Euler(10f, 20f, 15f), CEM_LAPIDE);
                break;
            default:
                m.Caixa(c0 + r * new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 1.8f), r, CEM_PEDRA);
                CemCamadas(m, c0 + r * new Vector3(0f, 0.6f, 0f), r, 0.9f, 1.8f, new[] { 0.1f, 0.12f }, new[] { 0.06f, -0.08f }, CEM_LAPIDE);
                break;
        }
    }

    // ==================================================================
    //  PEDESTAIS E ESTÁTUAS (modelos reais)
    // ==================================================================
    // pedestal de pedra com molduras; devolve a altura do topo
    static float CemPedestal(Malha m, Vector3 pos, float larg, float alt, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 o = new Vector3(pos.x, Gy(pos.x, pos.z) - 0.05f, pos.z);
        float hb = CemCamadas(m, o, q, larg, larg, BASE_H, BASE_S, CEM_PEDRA);
        Local(m, o, q, new Vector3(0f, hb + alt * 0.5f, 0f), new Vector3(larg, alt, larg), CEM_PEDRA);
        foreach (int s in new[] { -1, 1 })
        {
            Local(m, o, q, new Vector3(0f, hb + alt * 0.5f, s * (larg * 0.5f + 0.015f)), new Vector3(larg * 0.62f, alt * 0.62f, 0.06f), CEM_LAPIDE);
            Local(m, o, q, new Vector3(s * (larg * 0.5f + 0.015f), hb + alt * 0.5f, 0f), new Vector3(0.06f, alt * 0.62f, larg * 0.62f), CEM_LAPIDE);
        }
        float hc = CemCamadas(m, o + q * new Vector3(0f, hb + alt, 0f), q, larg, larg, CORN_H, CORN_S, CEM_PEDRA);
        return hb + alt + hc - 0.05f;
    }

    static void CemEstatua(Malha m, Vector3 pos, float yaw, float escala)
    {
        float topo = CemPedestal(m, pos, 1.5f, 1.1f, yaw);
        CemModelo("gothic_statue", Pasta("Modelos"), new Vector3(pos.x, topo, pos.z), yaw, 2.3f * escala, true, true);
    }

    static void CemBusto(Malha m, Vector3 pos, float yaw)
    {
        // coluna alta com capitel e um busto de mármore em cima
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 o = new Vector3(pos.x, Gy(pos.x, pos.z) - 0.05f, pos.z);
        float hb = CemCamadas(m, o, q, 0.62f, 0.62f, BASE_H, BASE_S, CEM_PEDRA);
        m.Cilindro(o + new Vector3(0f, hb, 0f), 0.24f, 1.0f, 12, CEM_LAPIDE, Quaternion.identity);
        float hc = CemCamadas(m, o + q * new Vector3(0f, hb + 1.0f, 0f), q, 0.5f, 0.5f, CORN_H, CORN_S, CEM_PEDRA);
        CemModelo("marble_bust_01", Pasta("Modelos"), new Vector3(pos.x, hb + 1.0f + hc - 0.05f, pos.z), yaw, 0.85f, true, false);
    }

    // ==================================================================
    //  CARRO FUNERÁRIO (como na referência)
    // ==================================================================
    static void CemCarro(Malha m, Malha ferro, Vector3 pos, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 o = new Vector3(pos.x, Gy(pos.x, pos.z), pos.z);
        Local(m, o, q, new Vector3(0f, 0.95f, 0f), new Vector3(1.9f, 0.18f, 4.0f), MAD_ESC);
        Local(m, o, q, new Vector3(0f, 1.15f, -0.2f), new Vector3(1.7f, 0.3f, 3.4f), MAD_ESC);
        foreach (int s in new[] { -1, 1 })
        {
            for (int k = 0; k < 4; k++) Local(m, o, q, new Vector3(s * 0.8f, 2.0f, -1.6f + k * 1.07f), new Vector3(0.12f, 1.5f, 0.12f), MAD_ESC);
            Local(m, o, q, new Vector3(s * 0.85f, 1.9f, -0.1f), new Vector3(0.03f, 1.1f, 3.2f), COURO);
        }
        Local(m, o, q, new Vector3(0f, 2.8f, -0.2f), new Vector3(2.0f, 0.2f, 3.6f), MAD_ESC);
        Local(m, o, q, new Vector3(0f, 2.98f, -0.2f), new Vector3(1.7f, 0.18f, 3.3f), MAD_ESC);
        for (int k = 0; k < 12; k++)
            foreach (int s in new[] { -1, 1 })
            {
                ferro.Caixa(o + q * new Vector3(s * 0.75f, 3.25f, -1.7f + k * 0.3f), new Vector3(0.05f, 0.4f, 0.1f), q, FERRO);
                ferro.Piramide(o + q * new Vector3(s * 0.75f, 3.45f, -1.7f + k * 0.3f), 0.05f, 0.14f, FERRO);
            }
        foreach (int s in new[] { -1, 1 })
            foreach (float z in new[] { -1.4f, 1.3f })
            {
                float raio = z < 0 ? 0.85f : 0.65f;
                Vector3 c = o + q * new Vector3(s * 1.0f, raio, z);
                m.Cilindro(c - q * new Vector3(0.06f, 0f, 0f), raio, 0.12f, 16, MAD_ESC, q * Quaternion.Euler(0f, 0f, -90f));
                for (int k = 0; k < 4; k++)
                    m.Caixa(c, new Vector3(0.1f, raio * 2f, 0.07f), q * Quaternion.Euler(k * 45f, 0f, 0f), FERRO);
            }
        ferro.Caixa(o + q * new Vector3(0f, 2.6f, 2.1f), new Vector3(0.2f, 0.34f, 0.2f), q, LANTERNA);
        ferro.Caixa(o + q * new Vector3(0f, 2.15f, 2.1f), new Vector3(0.05f, 0.9f, 0.05f), q, FERRO);
        m.Caixa(o + q * new Vector3(0f, 0.75f, -3.1f), new Vector3(0.8f, 0.5f, 2.2f), q * Quaternion.Euler(18f, 0f, 0f), MAD_ESC);
    }

    // ==================================================================
    //  COVAS ABERTAS COM CAIXÃO
    // ==================================================================
    static void CemCova(Malha m, Vector3 pos, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 o = new Vector3(pos.x, Gy(pos.x, pos.z), pos.z);
        Local(m, o, q, new Vector3(0f, -0.1f, 0f), new Vector3(1.1f, 0.12f, 2.3f), COURO);
        Local(m, o, q, new Vector3(0f, 0.55f, 0.4f), new Vector3(0.85f, 0.4f, 1.9f), TABUAS);
        m.Caixa(o + q * new Vector3(0.6f, 0.4f, 0.1f), new Vector3(0.9f, 0.1f, 1.9f), q * Quaternion.Euler(0f, 6f, -48f), TABUAS);
        foreach (int s in new[] { -1, 1 })
            Local(m, o, q, new Vector3(s * 0.8f, 0.12f, 0f), new Vector3(0.9f, 0.4f, 2.5f), MAD_ESC);
        Local(m, o, q, new Vector3(0f, 0.2f, -1.4f), new Vector3(1.5f, 0.55f, 0.9f), MAD_ESC);
    }
}
#endif
