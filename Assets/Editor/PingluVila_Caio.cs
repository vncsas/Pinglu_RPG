// =====================================================================
//  PingluVila_Caio.cs  -  liga a HISTÓRIA ao jogo e cria o Caio (ancião da vila)
// ---------------------------------------------------------------------
//  - objeto "Campanha": cutscene de abertura, objetivo na tela, avisos de capítulo, caixa de diálogo
//  - Caio: NPC provisório (ancião de barba branca, óculos redondos e cajado com cristal) na praça, perto da entrada
//    (quando o modelo 3D dele ficar pronto, é só trocar o corpo; a conversa continua funcionando)
//
//  COMO USAR:  Pinglu > 63. História: criar o Caio e ligar a campanha   (Play desligado)
//  Teste rápido dentro do jogo: F9 avança a história, F10 volta ao começo.
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    [MenuItem("Pinglu/63. História: criar o Caio e ligar a campanha")]
    public static void CaioMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain; GameObject vila = GameObject.Find(NOME_RAIZ);
        if (terreno == null || vila == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain ou a vila (Vila_Gerada).", "OK"); return; }
        dados = terreno.terrainData; avisos.Clear(); formas.Clear(); rng = new System.Random(7);
        raiz = vila.transform; contadorMalhas = 300; pastaMalhas = PASTA_MALHAS; prefixoOculto = PREFIXO_OCULTO;
        try
        {
            ConfigurarTexturas(); CriarMateriais();
            Font fTexto = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fontes/MarcellusSC-Regular.ttf");
            Font fTitulo = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fontes/CinzelDecorative-Bold.ttf");

            // ---- objeto Campanha
            foreach (string n in new[] { "Campanha" }) { GameObject velho = GameObject.Find(n); if (velho != null) Object.DestroyImmediate(velho); }
            GameObject camp = new GameObject("Campanha");
            CampanhaJogo cj = camp.AddComponent<CampanhaJogo>(); cj.fonteTexto = fTexto; cj.fonteTitulo = fTitulo;
            DialogoUI dui = camp.AddComponent<DialogoUI>(); dui.fonteTexto = fTexto; dui.fonteNome = fTitulo;
            Undo.RegisterCreatedObjectUndo(camp, "Campanha");

            // ---- Caio na praça, do lado de quem chega pela entrada
            Vector3 centro = vila.transform.position; Transform poco = vila.transform.Find("Praca/Poco"); if (poco != null) centro = poco.position;
            Vector3 dir = Vector3.forward;
            Transform portal = null; foreach (Transform t in vila.GetComponentsInChildren<Transform>(true)) if (t.name == NOME_PORTAL) { portal = t; break; }
            if (portal != null) dir = Plano(portal.position - centro).normalized;
            Vector3 lado = new Vector3(-dir.z, 0f, dir.x);
            Vector3 pos = centro + dir * 3.6f + lado * 1.6f;
            Transform pasta = Pasta("Personagens");
            for (int i = pasta.childCount - 1; i >= 0; i--) if (pasta.GetChild(i).name == "Caio") Object.DestroyImmediate(pasta.GetChild(i).gameObject);

            Malha m = new Malha(); CaioCorpo(m);
            GameObject caio = Instanciar(m, "Caio", NoChao(pos), Quaternion.LookRotation(-dir), pasta, false);
            caio.isStatic = false;
            CapsuleCollider cap = caio.AddComponent<CapsuleCollider>(); cap.radius = 0.4f; cap.height = 1.9f; cap.center = new Vector3(0f, 0.95f, 0f);
            NpcFalante npc = caio.AddComponent<NpcFalante>(); npc.nomeExibido = "Caio"; npc.fonte = fTexto;

            // nome flutuando
            Font fonte = FonteDasPlacas(); matLetras = MaterialDasLetras(fonte);
            GameObject et = new GameObject("Etiqueta"); et.transform.SetParent(caio.transform, false); et.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            Texto(et.transform, "CAIO", new Vector3(0f, 0.1f, 0f), 0f, 0.2f, 1.4f, new Color(1f, 0.85f, 0.45f), fonte);
            Texto(et.transform, "Anciao de " + Roteiro.NOME_DA_VILA, new Vector3(0f, -0.1f, 0f), 0f, 0.1f, 1.8f, new Color(0.85f, 0.8f, 0.65f), fonte);
            npc.etiqueta = et.transform;
            // brilho do cristal do cajado
            GameObject lz = new GameObject("Luz do cajado"); lz.transform.SetParent(caio.transform, false); lz.transform.localPosition = new Vector3(0.42f, 2.05f, 0.3f);
            Light l = lz.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.4f); l.range = 6f; l.intensity = 3.5f; l.shadows = LightShadows.None;

            EditorSceneManager.MarkSceneDirty(vila.scene); EditorSceneManager.SaveOpenScenes();
            avisos.Add("Campanha ligada e Caio criado na praça (" + Plano(pos - centro).magnitude.ToString("0.0") + " m do poço).");
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console)."); }
        foreach (string a in avisos) Debug.Log("[História] " + a);
        EditorUtility.DisplayDialog("Pinglu", "História ligada e cena salva!\n\n- " + string.Join("\n- ", avisos) + "\n\nAperte Play: aparece a cutscene de abertura, depois fale com o Caio (E).\nTeste: F9 avança a história, F10 volta ao começo.", "OK");
    }

    // corpo provisório do Caio (olha para +Z): túnica azul, barba branca, óculos redondos e cajado com cristal
    static void CaioCorpo(Malha m)
    {
        m.Torno(Vector3.zero, new[] { 0.44f, 0.46f, 0.4f, 0.32f, 0.27f, 0.3f, 0.26f, 0.12f }, new[] { 0f, 0.1f, 0.6f, 1.0f, 1.25f, 1.4f, 1.48f, 1.55f }, 18, TEC_AZUL, Quaternion.identity);
        m.Torno(new Vector3(0f, 0f, 0f), new[] { 0.45f, 0.47f, 0.47f, 0.45f }, new[] { 0f, 0.02f, 0.14f, 0.16f }, 18, TEC_OCRE, Quaternion.identity, false);      // barra bordada
        m.Torno(new Vector3(0f, 0.98f, 0f), new[] { 0.33f, 0.34f, 0.34f, 0.33f }, new[] { 0f, 0.01f, 0.09f, 0.1f }, 16, COURO, Quaternion.identity, false);           // cinto
        // braços
        for (int s = -1; s <= 1; s += 2)
        {
            Quaternion r = Quaternion.Euler(-35f, 0f, s * 14f);
            m.Cilindro(new Vector3(s * 0.28f, 1.38f, 0.02f), 0.075f, 0.55f, 10, TEC_AZUL, r);
            Vector3 mao = new Vector3(s * 0.28f, 1.38f, 0.02f) + r * (Vector3.up * 0.58f);
            m.Torno(mao - new Vector3(0f, 0.05f, 0f), new[] { 0.02f, 0.05f, 0.055f, 0.03f }, new[] { 0f, 0.03f, 0.07f, 0.1f }, 8, REBOCO_CREME, Quaternion.identity);
        }
        // cabeça, nariz, barba, boina
        m.Torno(new Vector3(0f, 1.5f, 0f), new[] { 0.06f, 0.1f, 0.14f, 0.15f, 0.13f, 0.08f, 0.01f }, new[] { 0f, 0.05f, 0.12f, 0.2f, 0.27f, 0.31f, 0.33f }, 14, REBOCO_CREME, Quaternion.identity);
        m.Torno(new Vector3(0f, 1.62f, 0.14f), new[] { 0.028f, 0.012f, 0.002f }, new[] { 0f, 0.05f, 0.08f }, 8, REBOCO_CREME, Quaternion.Euler(90f, 0f, 0f));
        m.Torno(new Vector3(0f, 1.14f, 0.07f), new[] { 0.01f, 0.05f, 0.11f, 0.15f, 0.14f }, new[] { 0f, 0.1f, 0.21f, 0.3f, 0.34f }, 12, REBOCO_CINZA, Quaternion.identity, false);
        m.Torno(new Vector3(0f, 1.75f, 0f), new[] { 0.17f, 0.16f, 0.11f, 0.04f, 0.005f }, new[] { 0f, 0.04f, 0.12f, 0.18f, 0.2f }, 14, TEC_AZUL, Quaternion.identity);
        m.Torno(new Vector3(0f, 1.68f, 0.115f), new[] { 0.13f, 0.14f, 0.12f }, new[] { 0f, 0.01f, 0.07f }, 8, REBOCO_CINZA, Quaternion.Euler(90f, 0f, 0f), false);    // sobrancelhas/bigode ralo
        // óculos redondos (a piada do coordenador)
        for (int s = -1; s <= 1; s += 2)
            m.Cilindro(new Vector3(s * 0.06f, 1.66f, 0.133f), 0.045f, 0.012f, 12, LAMINA, Quaternion.Euler(90f, 0f, 0f));
        m.Caixa(new Vector3(0f, 1.66f, 0.145f), new Vector3(0.05f, 0.01f, 0.01f), FERRO);
        // cajado com cristal brilhando
        m.Cilindro(new Vector3(0.45f, 0f, 0.3f), 0.025f, 1.95f, 8, MAD_ESC, Quaternion.identity);
        m.Torno(new Vector3(0.45f, 1.9f, 0.3f), new[] { 0.01f, 0.07f, 0.09f, 0.07f, 0.01f }, new[] { 0f, 0.06f, 0.14f, 0.22f, 0.3f }, 6, LANTERNA, Quaternion.identity, false);
    }
}
#endif
