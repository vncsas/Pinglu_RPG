// =====================================================================
//  PingluVila_Portal.cs  -  portão de entrada e placa de boas-vindas do vilarejo
// ---------------------------------------------------------------------
//  - o portão antigo sai e um portão NOVO é montado em cima da estrada de pedra que chega na vila (Portal - Vila),
//    virado ao longo da estrada, com letreiro "BEM-VINDO" nos dois lados, estandartes e lanternas
//  - uma placa de beira de estrada (madeira, pedra e telhadinho) com o NOME do vilarejo, antes de chegar à vila
//
//  COMO USAR:  Pinglu > 58. Portal e placa de boas-vindas   (Play desligado; depois Ctrl+S)
//  Para mudar o nome do vilarejo: troque NOME_DA_VILA abaixo e rode o menu de novo.
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string NOME_DA_VILA = "VALVERDE";
    const string NOME_PORTAL = "Portal da Vila";
    const string NOME_PLACA = "Placa de Boas-vindas";
    const float DIST_PLACA = 98f;     // quantos metros antes do centro da vila fica a placa da estrada

    [MenuItem("Pinglu/58. Portal e placa de boas-vindas")]
    public static void PortalEPlacaMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        PortalEPlaca(true);
    }

    static void PortalEPlaca(bool interativo)
    {
        terreno = Terrain.activeTerrain;
        GameObject vila = GameObject.Find(NOME_RAIZ);
        if (terreno == null || vila == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain ou a vila (Vila_Gerada).", "OK"); return; }
        dados = terreno.terrainData;
        avisos.Clear(); formas.Clear();
        raiz = vila.transform; contadorMalhas = 100;
        pastaMalhas = PASTA_MALHAS; prefixoOculto = PREFIXO_OCULTO;
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Montando o portão e a placa...", 0.2f);
            ConfigurarTexturas(); CriarMateriais();

            // estrada Portal - Vila
            RedeDeEstradas rede = Object.FindFirstObjectByType<RedeDeEstradas>();
            Vector3 centro = vila.transform.position; bool temCentro = false;
            MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
            if (mm != null) foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Vila) { centro = k.posicao; temCentro = true; }
            Transform poco = vila.transform.Find("Praca/Poco"); if (poco != null) { centro = poco.position; temCentro = true; }
            if (rede == null || !temCentro) throw new System.Exception("Não achei a rede de estradas ou o centro da vila (rode o Pinglu > 39).");
            Vector3[] estrada = null; float melhorD = 1e9f;
            foreach (RedeDeEstradas.Estrada e in rede.estradas)
            {
                if (e == null || e.pontos == null || e.pontos.Length < 2 || !e.nome.Contains("Portal - Vila")) continue;
                estrada = e.pontos;
            }
            if (estrada == null)
                foreach (RedeDeEstradas.Estrada e in rede.estradas)   // plano B: a estrada que chega mais perto da vila
                {
                    if (e == null || e.pontos == null || e.pontos.Length < 2) continue;
                    foreach (Vector3 p in e.pontos) { float d = Plano(p - centro).magnitude; if (d < melhorD) { melhorD = d; estrada = e.pontos; } }
                }
            if (estrada == null) throw new System.Exception("Não achei nenhuma estrada chegando na vila.");
            List<Vector3> lin = new List<Vector3>(estrada);
            if (Plano(lin[0] - centro).magnitude > Plano(lin[lin.Count - 1] - centro).magnitude) lin.Reverse();   // começa no lado da vila

            // casas (para o portão não nascer em cima de uma)
            List<Bounds> casas = new List<Bounds>();
            foreach (string pasta in new[] { "Construcoes", NOME_CASAS_NOVAS })
            {
                Transform t = vila.transform.Find(pasta); if (t == null) continue;
                foreach (Transform c in t)
                {
                    bool ok = false; Bounds b = new Bounds(c.position, Vector3.zero);
                    foreach (Renderer r in c.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds); }
                    if (ok && b.size.y > 2f) casas.Add(b);
                }
            }

            Transform pastaEst = Pasta("Estrada");
            foreach (string n in new[] { NOME_PORTAL, NOME_PLACA })
                for (int i = pastaEst.childCount - 1; i >= 0; i--) if (pastaEst.GetChild(i).name == n) Object.DestroyImmediate(pastaEst.GetChild(i).gameObject);
            // o portão velho pode estar em outra pasta
            foreach (Transform t in vila.GetComponentsInChildren<Transform>(true)) if (t != null && t.name == NOME_PORTAL && t.parent != null) { Object.DestroyImmediate(t.gameObject); break; }

            // ---------- portão: sobre a estrada de pedra, onde não bate em casa
            Vector3 pg = Vector3.zero, tg = Vector3.forward; bool achou = false;
            foreach (float dist in new[] { 36f, 42f, 48f, 54f, 32f })
            {
                Vector3 p, t; PontoNaEstrada(lin, centro, dist, out p, out t);
                bool livre = true; Vector3 lado = new Vector3(-t.z, 0f, t.x);
                for (float a = -4.5f; a <= 4.5f && livre; a += 1.5f) for (float b = -1.2f; b <= 1.2f && livre; b += 1.2f)
                    if (DentroDeCasa(p + lado * a + t * b, casas, 0.8f)) livre = false;
                if (livre || !achou) { pg = p; tg = t; achou = true; }
                if (livre) break;
            }
            Malha portal = new Malha(); PortalNovo(portal);
            GameObject gPortal = Instanciar(portal, NOME_PORTAL, NoChao(pg), Quaternion.LookRotation(tg), pastaEst, true);
            Letreiros(gPortal.transform, true);
            TirarArvoresPerto(pg, 4.5f);
            TirarPostesPerto(vila.transform, pg, 6f);

            // ---------- placa de beira de estrada
            Vector3 pp, tp; PontoNaEstrada(lin, centro, DIST_PLACA, out pp, out tp);
            Vector3 direita = new Vector3(-tp.z, 0f, tp.x) * -1f;          // à direita de quem anda rumo à vila (tp aponta para fora da vila)
            Vector3 posPlaca = pp + direita * 4.6f;
            Vector3 frente = (tp - direita * 0.35f).normalized;          // de frente para quem chega, um pouco virada para a estrada
            Malha placa = new Malha(); PlacaDeEstrada(placa);
            GameObject gPlaca = Instanciar(placa, NOME_PLACA, NoChao(posPlaca), Quaternion.LookRotation(frente), pastaEst, true);
            Letreiros(gPlaca.transform, false);
            TirarArvoresPerto(posPlaca, 3.5f);
            TirarPostesPerto(vila.transform, posPlaca, 5f);
            AjustarLuzesDosPostes();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(vila.scene);
            avisos.Add("portão novo a " + Plano(pg - centro).magnitude.ToString("0") + " m do centro, placa a " + Plano(posPlaca - centro).magnitude.ToString("0") + " m. Nome: " + NOME_DA_VILA + ".");
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console)."); }
        finally { EditorUtility.ClearProgressBar(); }
        foreach (string a in avisos) Debug.Log("[Portal] " + a);
        if (interativo)
        {
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog("Pinglu", "Portão e placa de boas-vindas prontos e cena salva!\n\n- " + string.Join("\n- ", avisos) + "\n(Rode o 54 de novo se quiser o capim sair de baixo deles.)", "OK");
        }
    }

    // ponto da polilinha (que começa no lado da vila) a "dist" metros do centro, e a direção da estrada apontando PARA FORA da vila
    static void PontoNaEstrada(List<Vector3> lin, Vector3 centro, float dist, out Vector3 pos, out Vector3 dir)
    {
        pos = lin[lin.Count - 1]; dir = Plano(lin[lin.Count - 1] - lin[Mathf.Max(0, lin.Count - 2)]).normalized;
        for (int i = 1; i < lin.Count; i++)
        {
            float d0 = Plano(lin[i - 1] - centro).magnitude, d1 = Plano(lin[i] - centro).magnitude;
            if (d1 >= dist && d0 < dist || (d0 <= dist && d1 >= dist))
            {
                float t = Mathf.InverseLerp(d0, d1, dist);
                pos = Vector3.Lerp(lin[i - 1], lin[i], t);
                int a = Mathf.Max(0, i - 2), b = Mathf.Min(lin.Count - 1, i + 1);
                dir = Plano(lin[b] - lin[a]).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Plano(lin[i] - lin[i - 1]).normalized;
                return;
            }
        }
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
    }

    static void TirarPostesPerto(Transform vila, Vector3 p, float raio)
    {
        Transform pasta = vila.Find(NOME_POSTES); if (pasta == null) return;
        for (int i = pasta.childCount - 1; i >= 0; i--) { Transform t = pasta.GetChild(i); if (t.name == "Poste de Rua" && Plano(t.position - p).magnitude < raio) Object.DestroyImmediate(t.gameObject); }
    }

    static void TirarArvoresPerto(Vector3 p, float raio)
    {
        Vector3 o = terreno.transform.position, s = dados.size;
        List<TreeInstance> manter = new List<TreeInstance>();
        foreach (TreeInstance ti in dados.treeInstances)
        {
            Vector3 q = new Vector3(o.x + ti.position.x * s.x, 0f, o.z + ti.position.z * s.z);
            if (Plano(q - p).magnitude > raio) manter.Add(ti);
        }
        if (manter.Count != dados.treeInstances.Length) dados.SetTreeInstances(manter.ToArray(), true);
    }

    // ---------------------------------------------------------------- portão
    static void PortalNovo(Malha m)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            // base de pedra em degraus, poste grosso com ferragens e escora
            m.Caixa(new Vector3(s * 2.45f, 0.18f, 0f), new Vector3(1.1f, 0.36f, 1.1f), PEDRA_ESCURA);
            m.Caixa(new Vector3(s * 2.45f, 0.5f, 0f), new Vector3(0.82f, 0.28f, 0.82f), PEDRA_RUSTICA);
            m.Caixa(new Vector3(s * 2.45f, 2.7f, 0f), new Vector3(0.42f, 4.4f, 0.42f), MAD_ESC);
            Diagonal(m, new Vector3(s * 2.4f, 3.55f, 0f), new Vector3(s * 1.6f, 4.5f, 0f), 0.2f, MAD_ESC);
            Diagonal(m, new Vector3(s * 2.4f, 3.55f, 0.01f), new Vector3(s * 1.6f, 4.5f, 0.01f), 0.07f, FERRO);
            foreach (float y in new[] { 1.1f, 2.4f, 3.7f }) m.Caixa(new Vector3(s * 2.45f, y, 0f), new Vector3(0.46f, 0.1f, 0.46f), FERRO);
            // estandarte
            m.Caixa(new Vector3(s * 2.45f, 4.3f, 0.46f), new Vector3(0.9f, 0.05f, 0.05f), MAD_ESC);
            m.Caixa(new Vector3(s * 2.45f, 3.5f, 0.48f), new Vector3(0.75f, 1.55f, 0.02f), TEC_VERM);
            m.Painel(new Vector3(s * 2.45f, 3.6f, 0.5f), Vector3.forward, 0.45f, 0.56f, P_BRASAO);
            // braço de ferro com lanterna (lado de dentro do portão)
            m.Caixa(new Vector3(s * 2.0f, 3.0f, 0.2f), new Vector3(0.9f, 0.04f, 0.04f), FERRO);
            m.Caixa(new Vector3(s * 1.55f, 2.9f, 0.2f), new Vector3(0.02f, 0.2f, 0.02f), FERRO);
            Lanterna(m, new Vector3(s * 1.55f, 2.4f, 0.2f));
        }
        // viga dupla e telhado
        m.Caixa(new Vector3(0f, 4.7f, 0f), new Vector3(6.2f, 0.34f, 0.46f), MAD_ESC);
        m.Caixa(new Vector3(0f, 4.38f, 0f), new Vector3(5.0f, 0.18f, 0.36f), MAD_VERM);
        Telhado(m, new Vector3(0f, 4.87f, 0f), 1.1f, 6.0f, 40f, false, 0.28f, 0.2f, TELHA_ARDOSIA, -1, MAD_ESC, false, false);
        // corrente e placa pendurada (o texto vem de TextMesh, dos dois lados)
        foreach (float x in new[] { -1.15f, 1.15f }) { m.Caixa(new Vector3(x, 4.04f, 0f), new Vector3(0.03f, 0.62f, 0.03f), FERRO); }
        m.Caixa(new Vector3(0f, 3.45f, 0f), new Vector3(2.8f, 1.05f, 0.12f), MAD_ESC);
        // moldura de tábuas claras
        m.Caixa(new Vector3(0f, 3.97f, 0.02f), new Vector3(2.9f, 0.1f, 0.15f), TABUAS);
        m.Caixa(new Vector3(0f, 2.93f, 0.02f), new Vector3(2.9f, 0.1f, 0.15f), TABUAS);
        m.Caixa(new Vector3(-1.45f, 3.45f, 0.02f), new Vector3(0.1f, 1.05f, 0.15f), TABUAS);
        m.Caixa(new Vector3(1.45f, 3.45f, 0.02f), new Vector3(0.1f, 1.05f, 0.15f), TABUAS);
        foreach (float x in new[] { -1.38f, 1.38f }) foreach (float y in new[] { 3.9f, 3.0f }) m.Caixa(new Vector3(x, y, 0.1f), new Vector3(0.07f, 0.07f, 0.04f), FERRO);
    }

    static void Lanterna(Malha m, Vector3 c)
    {
        m.Torno(c, new[] { 0.1f, 0.15f, 0.15f, 0.1f }, new[] { 0f, 0.04f, 0.3f, 0.36f }, 4, LANTERNA, Quaternion.Euler(0f, 45f, 0f), false);
        m.Caixa(c + new Vector3(0f, 0.01f, 0f), new Vector3(0.28f, 0.03f, 0.28f), FERRO);
        m.Piramide(c + new Vector3(0f, 0.37f, 0f), 0.19f, 0.16f, FERRO);
    }

    // ---------------------------------------------------------------- placa de beira de estrada
    static void PlacaDeEstrada(Malha m)
    {
        // base de pedra
        m.Caixa(new Vector3(0f, 0.22f, 0f), new Vector3(3.6f, 0.44f, 1.0f), PEDRA_RUSTICA);
        m.Caixa(new Vector3(-1.2f, 0.1f, 0.55f), new Vector3(0.9f, 0.2f, 0.5f), PEDRA_ESCURA);
        m.Caixa(new Vector3(1.3f, 0.1f, 0.5f), new Vector3(0.7f, 0.2f, 0.45f), PEDRA_ESCURA);
        // postes
        foreach (int s in new[] { -1, 1 })
        {
            m.Caixa(new Vector3(s * 1.55f, 1.75f, 0f), new Vector3(0.3f, 3.0f, 0.3f), MAD_ESC);
            m.Caixa(new Vector3(s * 1.55f, 0.5f, 0f), new Vector3(0.36f, 0.08f, 0.36f), FERRO);
            m.Caixa(new Vector3(s * 1.55f, 2.8f, 0f), new Vector3(0.36f, 0.08f, 0.36f), FERRO);
            Diagonal(m, new Vector3(s * 1.5f, 2.55f, 0f), new Vector3(s * 1.05f, 3.05f, 0f), 0.13f, MAD_ESC);
        }
        // viga e telhadinho
        m.Caixa(new Vector3(0f, 3.1f, 0f), new Vector3(3.7f, 0.26f, 0.36f), MAD_ESC);
        Telhado(m, new Vector3(0f, 3.23f, 0f), 0.95f, 3.9f, 38f, false, 0.2f, 0.15f, TELHA_ESCAMA, -1, MAD_ESC, false, false);
        // placa principal (fundo escuro de madeira, moldura clara)
        m.Caixa(new Vector3(0f, 1.95f, 0f), new Vector3(2.75f, 1.5f, 0.14f), MAD_ESC);
        m.Caixa(new Vector3(0f, 2.73f, 0.02f), new Vector3(2.9f, 0.1f, 0.18f), TABUAS);
        m.Caixa(new Vector3(0f, 1.17f, 0.02f), new Vector3(2.9f, 0.1f, 0.18f), TABUAS);
        m.Caixa(new Vector3(-1.4f, 1.95f, 0.02f), new Vector3(0.1f, 1.6f, 0.18f), TABUAS);
        m.Caixa(new Vector3(1.4f, 1.95f, 0.02f), new Vector3(0.1f, 1.6f, 0.18f), TABUAS);
        foreach (float x in new[] { -1.3f, 1.3f }) foreach (float y in new[] { 1.27f, 2.63f }) m.Caixa(new Vector3(x, y, 0.1f), new Vector3(0.08f, 0.08f, 0.05f), FERRO);
        // plaquinha pendurada embaixo
        foreach (float x in new[] { -0.75f, 0.75f }) m.Caixa(new Vector3(x, 1.02f, 0.02f), new Vector3(0.03f, 0.3f, 0.03f), FERRO);
        m.Caixa(new Vector3(0f, 0.78f, 0.02f), new Vector3(1.9f, 0.34f, 0.08f), MAD_ESC);
        m.Caixa(new Vector3(0f, 0.97f, 0.03f), new Vector3(2.0f, 0.04f, 0.1f), TABUAS);
        // lanterna pendurada num braço de ferro
        m.Caixa(new Vector3(1.55f, 2.75f, 0.45f), new Vector3(0.04f, 0.04f, 0.9f), FERRO);
        m.Caixa(new Vector3(1.55f, 2.62f, 0.85f), new Vector3(0.02f, 0.2f, 0.02f), FERRO);
        Lanterna(m, new Vector3(1.55f, 2.15f, 0.85f));
    }

    // ---------------------------------------------------------------- letras (TextMesh) e luzes
    static void Letreiros(Transform pai, bool portao)
    {
        Font fonte = FonteDasPlacas(); matLetras = MaterialDasLetras(fonte);
        Color creme = new Color(0.9f, 0.8f, 0.56f), claro = new Color(0.78f, 0.7f, 0.52f);
        if (portao)
        {
            Texto(pai, "BEM-VINDO", new Vector3(0f, 3.62f, 0.085f), 180f, 0.28f, 2.5f, creme, fonte);
            Texto(pai, "AO VILAREJO DE " + NOME_DA_VILA, new Vector3(0f, 3.2f, 0.085f), 180f, 0.14f, 2.55f, claro, fonte);
            Texto(pai, "VOLTE SEMPRE", new Vector3(0f, 3.62f, -0.085f), 0f, 0.26f, 2.5f, creme, fonte);
            Texto(pai, "Que a estrada lhe seja leve", new Vector3(0f, 3.2f, -0.085f), 0f, 0.13f, 2.55f, claro, fonte);
            Luz(pai, new Vector3(-1.55f, 2.6f, 0.3f)); Luz(pai, new Vector3(1.55f, 2.6f, 0.3f));
        }
        else
        {
            Texto(pai, "BEM-VINDO AO VILAREJO DE", new Vector3(0f, 2.45f, 0.085f), 180f, 0.13f, 2.45f, claro, fonte);
            Texto(pai, NOME_DA_VILA, new Vector3(0f, 1.9f, 0.085f), 180f, 0.4f, 2.5f, creme, fonte);
            Texto(pai, "~ Descanse, viajante ~", new Vector3(0f, 1.4f, 0.085f), 180f, 0.12f, 2.4f, claro, fonte);
            Texto(pai, "Estalagem  -  Ferreiro  -  Feira", new Vector3(0f, 0.78f, 0.075f), 180f, 0.1f, 1.8f, claro, fonte);
            Texto(pai, "BOA VIAGEM,", new Vector3(0f, 2.2f, -0.085f), 0f, 0.2f, 2.4f, creme, fonte);
            Texto(pai, "VIAJANTE", new Vector3(0f, 1.8f, -0.085f), 0f, 0.24f, 2.4f, creme, fonte);
            Luz(pai, new Vector3(1.55f, 2.3f, 0.85f));
        }
    }

    // altura: tamanho aproximado da letra em metros; o componente TextoAjustado encolhe se passar da largura
    static void Texto(Transform pai, string txt, Vector3 pos, float giroY, float altura, float larguraMax, Color cor, Font fonte)
    {
        GameObject g = new GameObject("Texto - " + txt);
        g.transform.SetParent(pai, false);
        g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0f, giroY, 0f);
        TextMesh tm = g.AddComponent<TextMesh>();
        tm.text = txt; tm.font = fonte; tm.fontSize = 100; tm.characterSize = altura * 0.1f;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = cor;
        MeshRenderer mr = g.GetComponent<MeshRenderer>();
        if (matLetras != null) mr.sharedMaterial = matLetras; else if (fonte != null) mr.sharedMaterial = fonte.material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        TextoAjustado aj = g.AddComponent<TextoAjustado>(); aj.larguraMax = larguraMax;
    }

    static Material matLetras;

    // cópia da fonte só para as placas, com atlas fixo (ASCII): assim o material próprio com teste de profundidade sempre funciona
    static Font FonteDasPlacas()
    {
        string cam = "Assets/Cenario/Vila/Placas/CinzelPlaca.ttf";
        if (!System.IO.File.Exists(cam)) AssetDatabase.CopyAsset("Assets/Fontes/CinzelDecorative-Bold.ttf", cam);
        TrueTypeFontImporter ti = AssetImporter.GetAtPath(cam) as TrueTypeFontImporter;
        if (ti != null && (ti.fontTextureCase != FontTextureCase.ASCII || ti.fontSize != 96))
        {
            ti.fontTextureCase = FontTextureCase.ASCII; ti.fontSize = 96; ti.fontRenderingMode = FontRenderingMode.Smooth;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Font>(cam);
    }

    static Material MaterialDasLetras(Font fonte)
    {
        if (fonte == null) return null;
        string cam = "Assets/Cenario/Vila/Placas/Letras_da_Placa.mat";
        Shader sh = Shader.Find("Pinglu/Letreiro");
        if (sh == null) return null;
        Material m = AssetDatabase.LoadAssetAtPath<Material>(cam);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, cam); }
        m.shader = sh; m.SetTexture("_MainTex", fonte.material.mainTexture); m.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- luz dos postes
    [MenuItem("Pinglu/59. Postes que iluminam de verdade")]
    public static void AjustarLuzesMenu()
    {
        int n = AjustarLuzesDosPostes();
        EditorUtility.DisplayDialog("Pinglu", n + " luzes ajustadas (postes, portão e placa).\nSalve com Ctrl + S.", "OK");
    }

    // halo macio em volta de cada lanterna: faz o poste PARECER aceso (o brilho olha sempre para a câmera)
    static int AdicionarBrilhos()
    {
        Shader sh = Shader.Find("Pinglu/Brilho");
        if (sh == null) { Debug.LogWarning("[Postes] shader Pinglu/Brilho não encontrado."); return 0; }
        string pasta = "Assets/Cenario/Vila/Placas";
        Material mPoste = MatBrilho(pasta + "/Brilho_Poste.mat", sh, 2.3f, 1.35f), mLant = MatBrilho(pasta + "/Brilho_Lanterna.mat", sh, 1.1f, 1.1f);
        string cq = pasta + "/Brilho_Quad.asset";
        Mesh quad = AssetDatabase.LoadAssetAtPath<Mesh>(cq);
        if (quad == null)
        {
            quad = new Mesh { name = "Brilho_Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.bounds = new Bounds(Vector3.zero, new Vector3(6f, 6f, 6f));
            AssetDatabase.CreateAsset(quad, cq);
        }
        int n = 0;
        foreach (PosteDeLuz p in Object.FindObjectsByType<PosteDeLuz>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool rua = p.name == "Poste de Rua";
            Transform pai = rua ? p.transform : (p.luz != null ? p.luz.transform : p.transform);
            Transform velho = pai.Find("Brilho da Lanterna"); if (velho != null) Object.DestroyImmediate(velho.gameObject);
            GameObject g = new GameObject("Brilho da Lanterna");
            g.transform.SetParent(pai, false);
            g.transform.localPosition = rua ? new Vector3(0f, 3.42f, 0f) : Vector3.zero;
            g.AddComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = rua ? mPoste : mLant;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            n++;
        }
        return n;
    }

    static Material MatBrilho(string cam, Shader sh, float tamanho, float intensidade)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(cam);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, cam); }
        m.shader = sh; m.SetColor("_Cor", new Color(1f, 0.7f, 0.36f, 1f)); m.SetFloat("_Tamanho", tamanho); m.SetFloat("_Intensidade", intensidade);
        EditorUtility.SetDirty(m);
        return m;
    }

    static int AjustarLuzesDosPostes()
    {
        int n = 0;
        foreach (PosteDeLuz p in Object.FindObjectsByType<PosteDeLuz>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p.luz == null) p.luz = p.GetComponentInChildren<Light>(true);
            if (p.luz == null) continue;
            bool rua = p.name == "Poste de Rua";
            p.luz.type = LightType.Point; p.luz.color = new Color(1f, 0.72f, 0.4f); p.luz.shadows = LightShadows.None;
            p.luz.range = rua ? 17f : 11f; p.luz.intensity = rua ? 18f : 7f; p.intensidadeBase = p.luz.intensity; p.distanciaLigada = 70f;
            EditorUtility.SetDirty(p.luz); EditorUtility.SetDirty(p); n++;
        }
        n += AdicionarBrilhos();
        // a luz extra do URP: pontos de luz por pixel (por vértice o chão quase não recebe a luz do poste)
        foreach (string g in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            Object a = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(g));
            if (a == null) continue;
            SerializedObject so = new SerializedObject(a);
            SerializedProperty modo = so.FindProperty("m_AdditionalLightsRenderingMode"), lim = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (modo != null) modo.intValue = 2;
            if (lim != null) lim.intValue = Mathf.Max(lim.intValue, 8);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(a);
        }
        AssetDatabase.SaveAssets();
        UnityEngine.SceneManagement.Scene cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (cena.IsValid()) EditorSceneManager.MarkSceneDirty(cena);
        return n;
    }

    static void Luz(Transform pai, Vector3 pos)
    {
        GameObject g = new GameObject("Luz da lanterna");
        g.transform.SetParent(pai, false); g.transform.localPosition = pos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.74f, 0.44f); l.range = 11f; l.intensity = 7f; l.shadows = LightShadows.None;
        PosteDeLuz pl = g.AddComponent<PosteDeLuz>(); pl.luz = l; pl.intensidadeBase = 7f;
    }

    // ---------------------------------------------------------------- tudo de uma vez
    [MenuItem("Pinglu/62. Refazer TUDO da vila (ruas, portão, postes, colheita, capim) e salvar")]
    public static void TudoDaVilaMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain; GameObject vila = GameObject.Find(NOME_RAIZ);
        if (terreno == null || vila == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain ou a vila (Vila_Gerada).", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu - Refazer tudo da vila",
            "Vou refazer, nesta ordem: ruas e praça (56), portão e placa (58), postes com luz e brilho (59), colheita e arredores (60) e o capim do terreno (54). No fim a cena é SALVA.\n\nDemora alguns minutos. Pode fazer?", "Pode fazer", "Cancelar")) return;
        dados = terreno.terrainData;
        tudoEmUm = true;
        try
        {
            CaminhosVila(vila, false);
            PortalEPlaca(false);
            AjustarLuzesDosPostes();
            Colheita(false);
            EditorUtility.DisplayProgressBar("Pinglu", "Refazendo o capim do terreno...", 0.9f);
            if (PingluTerreno.Ativo()) PingluTerreno.RefazerSilencioso();
            EditorSceneManager.MarkSceneDirty(vila.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
        }
        catch (System.Exception e) { Debug.LogException(e); }
        finally { tudoEmUm = false; EditorUtility.ClearProgressBar(); }
        EditorUtility.DisplayDialog("Pinglu", "Tudo refeito e a cena foi salva!\nVeja o Console: linhas [Vila], [Portal], [Colheita] e [Terreno].", "OK");
    }
}
#endif
