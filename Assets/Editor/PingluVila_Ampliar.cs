// =====================================================================
//  PingluVila_Ampliar.cs  -  aumenta a vila com as casas que você baixou (taverna e casa do ferreiro)
// ---------------------------------------------------------------------
//  - NÃO mexe na vila que já existe nem no formato dela: as casas novas entram em volta, viradas para o centro (a praça)
//  - só usa trechos de terreno quase planos, longe de estradas e das casas antigas; NÃO mexe no terreno (nada afunda nem flutua)
//  - cada casa recebe LOD (some de longe) e uma caixa de colisão simples (os modelos têm milhões de triângulos)
//
//  COMO USAR:  Pinglu > 48. Ampliar vila (casas novas)   (Play desligado; depois Ctrl+S)
//  Para tirar: Pinglu > 49. Remover casas novas da vila
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string NOME_CASAS_NOVAS = "Casas Novas";
    const string CAMINHO_TAVERNA = PASTA + "/medieval__tavern_east.glb";
    const string CAMINHO_FERREIRO = PASTA + "/medieval_blacksmith_house.glb";
    // se a FRENTE (porta) de um modelo não ficar virada para o centro da vila, mude o giro dele aqui: 0, 90, 180 ou 270
    const float GIRO_TAVERNA = 0f, GIRO_FERREIRO = 0f;
    const int QTD_TAVERNAS = 2, QTD_FERREIROS = 6;
    const float TAM_TAVERNA = 15f, TAM_FERREIRO = 13f;      // maior lado da casa, em metros

    [MenuItem("Pinglu/48. Ampliar vila (casas novas)")]
    public static void AmpliarVilaMenu() { AmpliarVila(true); }

    [MenuItem("Pinglu/49. Remover casas novas da vila")]
    public static void RemoverCasasNovasMenu()
    {
        GameObject vila = GameObject.Find(NOME_RAIZ);
        Transform t = vila != null ? vila.transform.Find(NOME_CASAS_NOVAS) : null;
        if (t == null) { EditorUtility.DisplayDialog("Pinglu", "Não há casas novas na vila.", "OK"); return; }
        Undo.DestroyObjectImmediate(t.gameObject);
        EditorSceneManager.MarkSceneDirty(vila.scene);
        EditorUtility.DisplayDialog("Pinglu", "Casas novas removidas. Salve com Ctrl + S.", "OK");
    }

    class CasaNova { public string caminho, nome; public float tam, giro; }

    static void AmpliarVila(bool interativo)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;
        GameObject vila = GameObject.Find(NOME_RAIZ);
        if (vila == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei a vila (Vila_Gerada). Rode antes o Pinglu > 3.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CAMINHO_TAVERNA) == null && AssetDatabase.LoadAssetAtPath<GameObject>(CAMINHO_FERREIRO) == null)
        { EditorUtility.DisplayDialog("Pinglu", "Os modelos das casas ainda não foram importados. Espere o Unity terminar de importar (barra no canto de baixo) e rode de novo.\n\nEsperado em:\n" + CAMINHO_TAVERNA + "\n" + CAMINHO_FERREIRO, "OK"); return; }
        if (interativo && !EditorUtility.DisplayDialog("Pinglu - Ampliar vila",
            "Vou colocar " + QTD_TAVERNAS + " tavernas e " + QTD_FERREIROS + " casas de ferreiro em volta da vila, viradas para a praça.\nA vila que já existe não muda.\n\nOs modelos são pesados (a taverna tem 1,6 milhão de triângulos): cada casa some de longe (LOD).", "Pode fazer", "Cancelar")) return;

        Transform velhas = vila.transform.Find(NOME_CASAS_NOVAS);
        if (velhas != null) Object.DestroyImmediate(velhas.gameObject);

        avisos.Clear(); formas.Clear();
        rng = new System.Random(4242);
        Vector3 o = terreno.transform.position;

        // centro da vila (praça)
        Vector3 centro = Vector3.zero; bool temCentro = false;
        MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
        if (mm != null) foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Vila) { centro = k.posicao; temCentro = true; }
        Transform construcoes = vila.transform.Find("Construcoes");
        if (!temCentro && construcoes != null && construcoes.childCount > 0)
        {
            foreach (Transform c in construcoes) centro += c.position;
            centro /= construcoes.childCount; temCentro = true;
        }
        if (!temCentro) { EditorUtility.DisplayDialog("Pinglu", "Não consegui achar o centro da vila.", "OK"); return; }
        centro.y = Altura(centro.x, centro.z);

        // o que já ocupa espaço na vila (casas, poço, treino...) e as estradas
        List<Bounds> ocupado = new List<Bounds>();
        foreach (Renderer r in vila.GetComponentsInChildren<Renderer>())
        {
            Bounds b = r.bounds;
            if (b.size.x > 45f || b.size.z > 45f) continue;
            b.Expand(new Vector3(5f, 500f, 5f)); ocupado.Add(b);
        }
        List<KeyValuePair<Vector3[], float>> estradas = new List<KeyValuePair<Vector3[], float>>();
        RedeDeEstradas rede = Object.FindFirstObjectByType<RedeDeEstradas>();
        if (rede != null) foreach (RedeDeEstradas.Estrada e in rede.estradas) if (e.pontos != null) estradas.Add(new KeyValuePair<Vector3[], float>(e.pontos, e.largura));

        // quais casas, intercaladas
        List<CasaNova> pedidos = new List<CasaNova>();
        for (int i = 0; i < QTD_TAVERNAS; i++) pedidos.Add(new CasaNova { caminho = CAMINHO_TAVERNA, nome = "Taverna", tam = TAM_TAVERNA, giro = GIRO_TAVERNA });
        for (int i = 0; i < QTD_FERREIROS; i++) pedidos.Add(new CasaNova { caminho = CAMINHO_FERREIRO, nome = "Casa do Ferreiro", tam = TAM_FERREIRO, giro = GIRO_FERREIRO });

        // candidatos: anéis em volta da praça, os mais próximos primeiro (a vila continua compacta, em torno do centro)
        List<KeyValuePair<float, Vector3>> cands = new List<KeyValuePair<float, Vector3>>();
        for (float r = 30f; r <= 120f; r += 6f)
        {
            int n = Mathf.Max(6, Mathf.FloorToInt(Mathf.PI * 2f * r / 12f));
            float off = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = off + i * Mathf.PI * 2f / n;
                Vector3 p = centro + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                cands.Add(new KeyValuePair<float, Vector3>(r + (float)rng.NextDouble() * 10f, p));
            }
        }
        cands.Sort((x, y) => x.Key.CompareTo(y.Key));

        List<Vector4> colocadas = new List<Vector4>();     // x, z, raio, giro
        List<CasaNova> quais = new List<CasaNova>();
        foreach (CasaNova cn in pedidos)
        {
            foreach (KeyValuePair<float, Vector3> kv in cands)
            {
                Vector3 p = kv.Value;
                float meio = cn.tam * 0.5f;
                if (!DentroDoTerreno(p, 25f)) continue;
                float hmin = float.MaxValue, hmax = float.MinValue;
                for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++)
                { float h = Altura(p.x + dx * meio * 0.5f, p.z + dz * meio * 0.5f); hmin = Mathf.Min(hmin, h); hmax = Mathf.Max(hmax, h); }
                if (hmax - hmin > 1.6f || hmin < o.y + 0.6f) continue;     // só terreno quase plano
                Bounds quad = new Bounds(new Vector3(p.x, 0f, p.z), new Vector3(cn.tam + 6f, 1000f, cn.tam + 6f));
                bool bate = false;
                foreach (Bounds b in ocupado) if (b.Intersects(quad)) { bate = true; break; }
                if (bate) continue;
                foreach (Vector4 c in colocadas) if (new Vector2(p.x - c.x, p.z - c.y).magnitude < meio + c.z + 5f) { bate = true; break; }
                if (bate) continue;
                foreach (KeyValuePair<Vector3[], float> e in estradas)
                {
                    for (int i = 0; i + 1 < e.Key.Length && !bate; i++)
                    {
                        Vector2 a = new Vector2(e.Key[i].x, e.Key[i].z), b = new Vector2(e.Key[i + 1].x, e.Key[i + 1].z), q = new Vector2(p.x, p.z);
                        Vector2 ab = b - a; float t = ab.sqrMagnitude < 0.01f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                        if ((a + ab * t - q).magnitude < meio + e.Value * 0.5f + 2f) bate = true;
                    }
                    if (bate) break;
                }
                if (bate) continue;
                colocadas.Add(new Vector4(p.x, p.z, meio, Mathf.Lerp(hmin, hmax, 0.7f))); quais.Add(cn);
                break;
            }
        }

        Transform pasta = new GameObject(NOME_CASAS_NOVAS).transform;
        pasta.SetParent(vila.transform, false);
        Undo.RegisterCreatedObjectUndo(pasta.gameObject, "Casas novas");
        int feitas = 0;
        for (int i = 0; i < colocadas.Count; i++)
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Colocando casas... (" + (i + 1) + "/" + colocadas.Count + ")", (i + 1f) / colocadas.Count);
            Vector3 p = new Vector3(colocadas[i].x, 0f, colocadas[i].y);
            CasaNova cn = quais[i];
            Vector3 para = Plano(centro - p).normalized;
            float yaw = Mathf.Atan2(para.x, para.z) * Mathf.Rad2Deg + cn.giro;
            float y = colocadas[i].w - 0.1f;     // base perto do ponto mais alto do terreno sob a casa: não afunda (os pilares da casa cobrem o vão)
            GameObject g = CModelo(cn.caminho, cn.nome, Vector3.zero, Quaternion.identity, new Vector3(p.x, y, p.z), yaw, cn.tam, false, false, y, pasta, true, Vector3.zero);
            if (g == null) { avisos.Add("Não consegui carregar " + cn.caminho); continue; }
            Renderer[] rs = g.GetComponentsInChildren<Renderer>();
            LODGroup lg = g.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.012f, rs) });     // some quando ocupa menos de ~1% da tela
            lg.RecalculateBounds();
            formas.Add(Circulo(p, cn.tam * 0.75f, -1));
            feitas++;
        }
        EditorUtility.ClearProgressBar();

        Vector3 longe = new Vector3(-100000f, 0f, -100000f);
        LimparArvores(longe, 0f);        // tira árvores e capim embaixo das casas novas
        LimparDetalhesFormas();
        terreno.Flush();
        EditorUtility.SetDirty(dados);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        try { PingluAssentar.Executar(false); } catch (System.Exception e) { Debug.LogException(e); }

        int nT = 0, nF = 0; foreach (Transform c in pasta) { if (c.name == "Taverna") nT++; else nF++; }
        avisos.Add(nT + " tavernas e " + nF + " casas de ferreiro colocadas.");
        if (feitas < pedidos.Count) avisos.Add("Só coube " + feitas + " de " + pedidos.Count + " casas (o resto não achou terreno plano e livre perto da vila).");
        string msg = "Vila ampliada: " + feitas + " casas novas em volta da praça (Vila_Gerada > Casas Novas).\n\nSe a porta de alguma casa não estiver virada para o centro, me avise qual (ou mude GIRO_TAVERNA / GIRO_FERREIRO no código).\nSalve com Ctrl + S.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        if (interativo) EditorUtility.DisplayDialog("Pinglu", msg, "OK"); else Debug.Log("[Vila] " + msg);
        Selection.activeGameObject = pasta.gameObject;
    }
}
#endif
