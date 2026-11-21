using UnityEngine;

// O FINAL DO JOGO EM 3D (epílogo): usa a mesma câmera e os mesmos efeitos da abertura em 3D.
// 1) o fim do poder dos cristais, na montanha; 2) a manhã sobre Valverde; 3) a placa nova do CABARÉ DOS AVENTUREIROS; 4) os heróis comemorando.
// Quem chama é o FinalDoJogo (IntroDoJogo.TocarFinal). Se o mundo não for achado, o FinalDoJogo usa o epílogo antigo em 2D.
public partial class IntroDoJogo
{
    class AjustaLetreiro : MonoBehaviour
    {
        public float larguraAlvo = 3.9f, alturaAlvo = 1.0f; Renderer r; bool ok; int quadros;
        void Update()
        {
            if (ok) return;
            if (r == null) r = GetComponent<Renderer>(); if (r == null) return;
            Vector3 b = r.bounds.size; quadros++;
            if (b.x > 0.01f) { float s = Mathf.Min(larguraAlvo / b.x, alturaAlvo / Mathf.Max(0.01f, b.y)); transform.localScale *= s; ok = true; }
            else if (quadros > 30) ok = true;
        }
    }

    static Material MatLit3(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); if (m.HasProperty("_Color")) m.SetColor("_Color", c); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        return m;
    }

    GameObject Peca3(GameObject pai, PrimitiveType tipo, Vector3 pos, Vector3 esc, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(tipo); Destroy(g.GetComponent<Collider>()); g.transform.SetParent(pai.transform, false);
        g.transform.localPosition = pos; g.transform.localScale = esc; g.GetComponent<MeshRenderer>().sharedMaterial = m; return g;
    }

    // a placa nova: dois postes, tábua de madeira, letreiro dourado, lanternas e brilho. A frente aponta para "f".
    GameObject PlacaDoCabare3(Vector3 pos, Vector3 f)
    {
        GameObject raiz = new GameObject("Placa do Cabaré dos Aventureiros"); raiz.transform.position = pos; raiz.transform.rotation = Quaternion.LookRotation(f); lixo3.Add(raiz);
        Material madeira = MatLit3(new Color(0.34f, 0.21f, 0.1f)), escuro = MatLit3(new Color(0.16f, 0.1f, 0.06f)), ferro = MatLit3(new Color(0.12f, 0.12f, 0.13f));
        Peca3(raiz, PrimitiveType.Cube, new Vector3(-2.3f, 1.7f, 0f), new Vector3(0.3f, 3.4f, 0.3f), escuro);
        Peca3(raiz, PrimitiveType.Cube, new Vector3(2.3f, 1.7f, 0f), new Vector3(0.3f, 3.4f, 0.3f), escuro);
        Peca3(raiz, PrimitiveType.Cube, new Vector3(0f, 2.65f, 0f), new Vector3(4.9f, 1.55f, 0.2f), madeira);
        Peca3(raiz, PrimitiveType.Cube, new Vector3(0f, 3.5f, 0f), new Vector3(5.2f, 0.22f, 0.32f), escuro);                 // viga de cima
        Peca3(raiz, PrimitiveType.Cube, new Vector3(0f, 1.82f, 0f), new Vector3(5.0f, 0.14f, 0.26f), escuro);                // moldura de baixo
        for (int i = -1; i <= 1; i += 2) { Peca3(raiz, PrimitiveType.Cylinder, new Vector3(i * 2.3f, 3.25f, 0.3f), new Vector3(0.22f, 0.12f, 0.22f), ferro); }
        GameObject t = new GameObject("Letreiro"); t.transform.SetParent(raiz.transform, false); t.transform.localPosition = new Vector3(0f, 2.66f, 0.12f); t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        TextMesh tm = t.AddComponent<TextMesh>(); Font f2 = Resources.Load<Font>("Fontes/CinzelBold"); if (f2 != null) tm.font = f2;
        tm.text = "CABARÉ\nDOS AVENTUREIROS"; tm.fontSize = 120; tm.characterSize = 0.05f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = new Color(1f, 0.84f, 0.45f);
        MeshRenderer mr = t.GetComponent<MeshRenderer>(); if (tm.font != null) mr.sharedMaterial = tm.font.material;
        t.AddComponent<AjustaLetreiro>();
        // luz de lanternas + brilho dourado
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject lg = new GameObject("Lanterna"); lg.transform.SetParent(raiz.transform, false); lg.transform.localPosition = new Vector3(i * 2.3f, 3.0f, 0.6f);
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.7f, 0.35f); l.range = 14f; l.intensity = 5f; l.shadows = LightShadows.None; lg.AddComponent<Tremeluz>().baseI = 5f;
            GameObject h = FxCena.Nuvem(raiz.transform.TransformPoint(new Vector3(i * 2.3f, 3.0f, 0.6f)), 2.2f, new Color(1f, 0.75f, 0.35f, 0.7f), FxCena.TexBrilho(), 8); h.transform.SetParent(raiz.transform, true);
        }
        NovoPS(raiz.transform, "Brilhos da placa", FxCena.TexBrilho(), new Color(1f, 0.88f, 0.5f, 0.95f), 2.4f, 0.8f, 0.3f, 22f, -0.04f, false, 2.6f).transform.localPosition = new Vector3(0f, 2.6f, 0.3f);
        return raiz;
    }

    // a taverna mais perto do centro da vila (o cabaré fica na frente dela); sem taverna, uma ponta da praça
    Vector3 LugarDaPlaca3(out Vector3 frente)
    {
        Transform melhor = null; float md = 1e9f;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t == null) continue; string n = t.name.ToLowerInvariant();
            if (!(n.Contains("tavern") || n.Contains("taverna"))) continue;
            if (t.parent != null && t.parent.name.ToLowerInvariant().Contains("tavern")) continue;
            float d = new Vector2(t.position.x - vila.x, t.position.z - vila.z).magnitude; if (d < md) { md = d; melhor = t; }
        }
        Vector3 pos;
        if (melhor != null && md < 160f) { Vector3 paraVila = vila - melhor.position; paraVila.y = 0f; paraVila = paraVila.sqrMagnitude > 1f ? paraVila.normalized : Vector3.forward; pos = melhor.position + paraVila * 9f; frente = paraVila; }
        else { pos = Aberto3(vila, 22f); Vector3 d = vila - pos; d.y = 0f; frente = -(d.sqrMagnitude > 1f ? d.normalized : dirIn3); pos = G3(pos); }
        pos.y = Chao3(pos); return pos;
    }

    void AtribuirCenasFinal3D()
    {
        Vector3 v = vila; Terrain terr = Terrain.activeTerrain; Vector3 tc = terr != null ? terr.transform.position + terr.terrainData.size * 0.5f : v;
        // 0) o fim do poder dos cristais: eles se desfazem em luz sobre a montanha, cinzas ao vento, o céu clareando
        Vector3 mc = G3(montanha, 8f); Vector3 dm = tc - mc; dm.y = 0f; dm = dm.sqrMagnitude > 1f ? dm.normalized : dirIn3;
        cenas[0].dur = 12f;
        cenas[0].amb = new Amb { sol = new Color(1f, 0.72f, 0.45f), solInt = 1.5f, solRot = new Vector3(9f, 245f, 0f), fog = new Color(0.55f, 0.46f, 0.42f), fogDen = 0.0042f, amb = new Color(0.4f, 0.36f, 0.4f), ceu = new Color(0.8f, 0.55f, 0.4f), exposicao = 0.95f, atmosfera = 2.2f };
        cenas[0].entra = () =>
        {
            Color[] cores = { new Color(0.35f, 0.65f, 1f), new Color(0.3f, 1f, 0.6f), new Color(0.8f, 0.45f, 1f) };
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.094f; GameObject c = Cristal3(mc + new Vector3(Mathf.Cos(a) * 3.2f, 0f, Mathf.Sin(a) * 3.2f), cores[i], 0.9f);
                NovoPS(c.transform, "Luz que se desfaz", FxCena.TexBrilho(), new Color(1f, 0.95f, 0.75f, 0.95f), 3.2f, 5f, 0.5f, 0f, -0.1f, false, 1.1f);
            }
            GameObject vento = new GameObject("Cinzas ao vento"); vento.transform.position = mc + Vector3.up * 2f; lixo3.Add(vento);
            NovoPS(vento.transform, "Cinzas", FxCena.TexNuvem(), new Color(0.72f, 0.66f, 0.6f, 0.5f), 7f, 4f, 1.6f, 22f, -0.02f, true, 26f);
        };
        cenas[0].vivo = k =>
        {
            for (int i = 0; i < cristais3.Count; i++)
            {
                GameObject cr = cristais3[i]; if (cr == null) continue;
                float p = Suave((k - 0.1f) / 0.8f);
                cr.transform.localScale = Vector3.one * Mathf.Max(0.001f, 1f - p) * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 22f + i));
                Light l = cr.GetComponentInChildren<Light>(); if (l != null) l.intensity = Mathf.Lerp(3f, 0f, p);
                foreach (ParticleSystem ps in cr.GetComponentsInChildren<ParticleSystem>()) { var em = ps.emission; em.rateOverTime = 70f * Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI); }
            }
        };
        cenas[0].cam3 = k => Cam(Sv(G3(mc + dm * 62f, 3f), G3(mc + dm * 36f + Vector3.Cross(Vector3.up, dm) * 12f, 26f), k), mc + Vector3.up * Mathf.Lerp(8f, 18f, k), Mathf.Lerp(50f, 42f, k));
        // 1) a manhã sobre Valverde
        cenas[1].dur = 11f;
        cenas[1].amb = new Amb { sol = new Color(1f, 0.86f, 0.62f), solInt = 2f, solRot = new Vector3(24f, 70f, 0f), fog = new Color(0.78f, 0.78f, 0.84f), fogDen = 0.0016f, amb = new Color(0.55f, 0.55f, 0.65f), ceu = new Color(0.55f, 0.6f, 0.8f), exposicao = 1.15f, atmosfera = 1.3f };
        cenas[1].entra = () =>
        {
            GameObject m = new GameObject("Poeira de luz"); m.transform.position = v + Vector3.up * 14f; lixo3.Add(m);
            NovoPS(m.transform, "Brilhos da manhã", FxCena.TexBrilho(), new Color(1f, 0.95f, 0.75f, 0.7f), 9f, 0.5f, 0.55f, 35f, -0.005f, false, 70f);
        };
        cenas[1].cam3 = k => Cam(Sv(G3(Fr3(v, -50f, -300f), 120f), G3(Fr3(v, -20f, -110f), 30f), k), G3(v, 8f), Mathf.Lerp(54f, 44f, k));
        // 2) a placa nova do cabaré aparece
        Vector3 fp; Vector3 pp = LugarDaPlaca3(out fp);
        cenas[2].dur = 12f;
        cenas[2].amb = new Amb { sol = new Color(1f, 0.7f, 0.42f), solInt = 1.6f, solRot = new Vector3(8f, -100f, 0f), fog = new Color(0.52f, 0.38f, 0.3f), fogDen = 0.0028f, amb = new Color(0.45f, 0.36f, 0.34f), ceu = new Color(0.85f, 0.55f, 0.4f), exposicao = 1f, atmosfera = 2f };
        GameObject[] placa = new GameObject[1];
        cenas[2].entra = () => { placa[0] = PlacaDoCabare3(pp, fp); flash3 = 0.8f; };
        cenas[2].vivo = k => { if (placa[0] != null) { float p = Suave(k / 0.16f); placa[0].transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, p); } };
        cenas[2].cam3 = k => Cam(Sv(G3(pp + fp * 19f, 4.5f), G3(pp + fp * 9f + Vector3.Cross(Vector3.up, fp) * 2f, 2.6f), k), pp + Vector3.up * 2.7f, Mathf.Lerp(52f, 44f, k));
        // 3) os heróis comemoram, chovem moedas
        cenas[3].dur = 13f;
        cenas[3].amb = new Amb { sol = new Color(1f, 0.6f, 0.35f), solInt = 1.3f, solRot = new Vector3(5f, -100f, 0f), fog = new Color(0.45f, 0.3f, 0.26f), fogDen = 0.0032f, amb = new Color(0.4f, 0.3f, 0.3f), ceu = new Color(0.9f, 0.5f, 0.35f), exposicao = 0.95f, atmosfera = 2.4f };
        Vector3 hc = G3(pp + fp * 8f);
        cenas[3].entra = () =>
        {
            placa[0] = PlacaDoCabare3(pp, fp);
            for (int i = 0; i < 4; i++) { Vector3 q = hc + Vector3.Cross(Vector3.up, fp) * ((i - 1.5f) * 1.7f); Heroi3(i, G3(q), Quaternion.LookRotation(-fp), false); }
            GameObject chuva = new GameObject("Moedas"); chuva.transform.position = hc + Vector3.up * 9f; lixo3.Add(chuva);
            NovoPS(chuva.transform, "Moedas", FxCena.TexBrilho(), new Color(1f, 0.85f, 0.3f, 1f), 3f, 1f, 0.38f, 70f, 0.4f, false, 4.2f);
            NovoPS(chuva.transform, "Brilho das moedas", FxCena.TexBrilho(), new Color(1f, 1f, 0.8f, 0.9f), 2f, 0.5f, 0.2f, 40f, 0.3f, false, 4.5f);
        };
        cenas[3].cam3 = k => { float e = Suave(k); Vector3 cp = hc + fp * Mathf.Lerp(5f, 26f, e); cp.y = Chao3(cp) + Mathf.Lerp(1.8f, 13f, e); Cam(cp + Vector3.Cross(Vector3.up, fp) * Mathf.Lerp(1f, -4f, e), pp + Vector3.up * Mathf.Lerp(2.4f, 4f, e), Mathf.Lerp(48f, 56f, e)); };
    }
}
