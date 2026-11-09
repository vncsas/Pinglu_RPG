// =====================================================================
//  PingluEstradas.cs  -  Estradas do mundo e destinos do GPS
// ---------------------------------------------------------------------
//  Pinglu > 39. Estradas e GPS
//   - liga com estradas de terra: portal, vila, ruínas, acampamentos e o cemitério
//     (o cemitério pela trilha reta da floresta)
//   - pinta a estrada no terreno e tira árvores, grama e pedras de cima dela
//   - cria o objeto "Estradas" (usado pelo GPS e pelo mapa)
//  Pinglu > 40. Desfazer estradas
//  Rode com o Play desligado, depois Ctrl + S.
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string BACKUP_EST = "Assets/Cenario/Cemiterio/Backup_Terreno_Estradas.asset";
    const string PREFIXO_OCULTO_EST = "[oculto pelas estradas] ";

    [MenuItem("Pinglu/39. Estradas e GPS")]
    public static void ConstruirEstradasMenu() { ConstruirEstradas(true); }

    [MenuItem("Pinglu/40. Desfazer estradas")]
    public static void DesfazerEstradasMenu() { DesfazerEstradas(true); }

    // para rodar sem ninguém: Unity -batchmode -executeMethod PingluVila.ConstruirEstradasEFechar
    public static void ConstruirEstradasEFechar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        ConstruirEstradas(false);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    static void ConstruirEstradas(bool interativo)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) { if (interativo) EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;
        MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
        if (mm == null) { if (interativo) EditorUtility.DisplayDialog("Pinglu", "Não achei o mini-mapa. Rode antes o Pinglu > 22.", "OK"); return; }
        if (GameObject.Find("Estradas") != null)
        {
            if (interativo && !EditorUtility.DisplayDialog("Pinglu", "As estradas já existem. Quer refazer?", "Sim", "Cancelar")) return;
            DesfazerEstradas(false);
        }
        terreno = Terrain.activeTerrain; dados = terreno.terrainData;
        try { AplicarRelevoSeFalta(); } catch (System.Exception e) { Debug.LogException(e); }   // relevo dos campos (menu 50): antes das rotas e do backup
        if (interativo && !EditorUtility.DisplayDialog("Pinglu - Estradas e GPS",
            "Vou ligar o portal, a vila, as ruínas, os acampamentos e o cemitério com estradas de terra (pintadas no terreno, sem árvores em cima) e preparar o GPS do mapa.\n\nAntes guardo um backup do terreno.", "Pode fazer", "Cancelar")) return;

        avisos.Clear(); formas.Clear();
        prefixoOculto = PREFIXO_OCULTO_EST;
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Planejando as estradas...", 0.1f);
            PlanejadorDeRotas plano = new PlanejadorDeRotas(terreno, 8f);

            // ---- lugares
            Vector3 vila = Vector3.zero, ruinas = Vector3.zero; bool temVila = false, temRuinas = false;
            List<MiniMapa.Marcador> acamp = new List<MiniMapa.Marcador>();
            foreach (MiniMapa.Marcador m in mm.marcadores)
            {
                if (m.tipo == MiniMapa.Tipo.Vila) { vila = m.posicao; temVila = true; }
                else if (m.tipo == MiniMapa.Tipo.Ruinas) { ruinas = m.posicao; temRuinas = true; }
                else if (m.tipo == MiniMapa.Tipo.Acampamento) acamp.Add(m);
            }
            GameObject mundo = GameObject.Find(NOME_RAIZ_MUNDO);
            Transform estrutura = mundo != null ? mundo.transform.Find("Cemiterio/Cemiterio") : null;
            // castelo destruído (menu 45): a estrada vai até o portão e o mapa desenha a muralha
            Transform castelo = mundo != null ? mundo.transform.Find(NOME_CASTELO) : null;
            Transform estruturaCastelo = castelo != null ? castelo.Find("Estrutura") : null, entradaCastelo = castelo != null ? castelo.Find("Entrada") : null;
            if (entradaCastelo != null) { ruinas = entradaCastelo.position; temRuinas = true; }
            GameObject portalGo = GameObject.Find("Portal de Ruina");
            Vector3 portal = portalGo != null ? portalGo.transform.position : (temVila ? vila : terreno.transform.position + dados.size * 0.5f);
            if (!temVila) { avisos.Add("Não achei a vila no mini-mapa; as estradas ficam sem ela."); vila = portal; }

            List<KeyValuePair<string, List<Vector3>>> estradas = new List<KeyValuePair<string, List<Vector3>>>();
            System.Action<string, Vector3, Vector3> ligar = (nome, a, b) => estradas.Add(new KeyValuePair<string, List<Vector3>>(nome, plano.Rota(a, b, false)));
            if ((portal - vila).sqrMagnitude > 400f) ligar("Portal - Vila", portal, vila);
            if (temRuinas) ligar(entradaCastelo != null ? "Vila - Castelo Destruído" : "Vila - Ruínas da Fortaleza", vila, ruinas);
            for (int i = 0; i < acamp.Count; i++) ligar("Vila - Acampamento " + (i + 1), vila, acamp[i].posicao);

            Vector3 cemC = Vector3.zero, entrada = Vector3.zero; bool temCem = estrutura != null;
            if (temCem)
            {
                cemC = estrutura.position;
                Vector3 frente = estrutura.forward; frente.y = 0f; frente.Normalize();
                Vector3 portao = cemC + frente * (CEM_P * 0.5f + 1f);
                entrada = portao + frente * 9f;
                // dentro do cemitério e pela trilha reta da floresta
                List<Vector3> trilha = new List<Vector3>();
                float fim = CEM_RAIO_FRENTE - 20f;
                for (float d = CEM_PR + 6f; d <= fim; d += 12f)
                {
                    Vector3 p = cemC + frente * d;
                    if (!DentroDoTerreno(p, 4f)) break;
                    p.y = plano.AlturaEm(p);
                    trilha.Add(p);
                }
                if (trilha.Count > 2)
                {
                    estradas.Add(new KeyValuePair<string, List<Vector3>>("Cemitério - Trilha da floresta", trilha));
                    Vector3 pontaDaTrilha = trilha[trilha.Count - 1];
                    Vector3 alvo = (pontaDaTrilha - vila).sqrMagnitude < (pontaDaTrilha - portal).sqrMagnitude ? vila : portal;
                    ligar("Trilha - " + (alvo == vila ? "Vila" : "Portal"), pontaDaTrilha, alvo);
                }
            }
            else avisos.Add("Não achei o cemitério (Pinglu > 31): sem estrada para ele.");

            // ---- terreno: backup, pintura e limpeza
            EditorUtility.DisplayProgressBar("Pinglu", "Pintando as estradas no terreno...", 0.4f);
            CriarPastaSeFaltar("Assets", "Cenario"); CriarPastaSeFaltar("Assets/Cenario", "Cemiterio");
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP_EST) != null) AssetDatabase.DeleteAsset(BACKUP_EST);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dados), BACKUP_EST)) throw new System.Exception("Não consegui fazer backup do terreno.");

            List<RedeDeEstradas.Estrada> dadosEstradas = new List<RedeDeEstradas.Estrada>();
            foreach (KeyValuePair<string, List<Vector3>> kv in estradas)
            {
                List<Vector3> pts = PlanejadorDeRotas.Reamostrar(kv.Value, 8f);
                dadosEstradas.Add(new RedeDeEstradas.Estrada { nome = kv.Key, pontos = pts.ToArray(), largura = 4.5f });
                List<Vector3> simples = PlanejadorDeRotas.Simplificar(pts, 4f);
                for (int i = 0; i + 1 < simples.Count; i++) formas.Add(Segmento(simples[i], simples[i + 1], 2.3f, 1));
            }
            if (formas.Count > 0)
            {
                Vector3 longe = new Vector3(-100000f, 0f, -100000f);
                LimparArvores(longe, 0f);
                LimparDetalhesFormas();
                PintarChao();
                OcultarCenarioNaArea(longe, 0f);
                OcultarNasEstradas();
                terreno.Flush();
                EditorUtility.SetDirty(dados);
            }

            // ---- objeto da cena com as estradas e os destinos
            EditorUtility.DisplayProgressBar("Pinglu", "Criando os destinos do GPS...", 0.85f);
            GameObject g = new GameObject("Estradas");
            Undo.RegisterCreatedObjectUndo(g, "Estradas");
            RedeDeEstradas rede = g.AddComponent<RedeDeEstradas>();
            rede.estradas = dadosEstradas;
            foreach (MiniMapa.Marcador m in mm.marcadores)
            {
                bool cem = m.tipo == MiniMapa.Tipo.Cemiterio;
                if (cem && temCem) m.posicao = cemC;     // o alfinete fica no cemitério, não no meio da floresta
                RedeDeEstradas.Destino dest = new RedeDeEstradas.Destino { nome = m.nome, tipo = m.tipo, posicao = cem && temCem ? cemC : m.posicao, temEntrada = cem && temCem, entrada = cem && temCem ? entrada : Vector3.zero };
                if (cem && temCem) { dest.temArea = true; dest.areaTamanho = new Vector2(CEM_L, CEM_P); dest.areaYaw = estrutura.eulerAngles.y; }   // o mapa desenha a muralha do cemitério
                if (m.tipo == MiniMapa.Tipo.Ruinas && estruturaCastelo != null && entradaCastelo != null)
                {
                    m.posicao = estruturaCastelo.position; dest.posicao = estruturaCastelo.position;
                    dest.temEntrada = true; dest.entrada = entradaCastelo.position;
                    dest.temArea = true; dest.areaTamanho = new Vector2(CAST_L, CAST_P); dest.areaYaw = estruturaCastelo.eulerAngles.y;
                }
                rede.destinos.Add(dest);
            }
            EditorUtility.SetDirty(mm);
            EditorSceneManager.MarkSceneDirty(g.scene);
            avisos.Add(estradas.Count + " estradas criadas.");
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console). Use Pinglu > 40."); }
        finally { EditorUtility.ClearProgressBar(); prefixoOculto = PREFIXO_OCULTO; }

        // as estradas desfazem o terreno para um backup de antes da vegetação: refaz a vegetação (sem árvores em cima das estradas)
        PingluTerreno.CaminhosPrecisamSuavizar();     // estrada recém-pintada: o terreno novo suaviza as bordas
        try { if (PingluVegetacao.JaFoiCriada()) PingluVegetacao.RefazerSilencioso(); else if (PingluTerreno.Ativo()) PingluTerreno.RefazerSilencioso(); } catch (System.Exception ev) { Debug.LogException(ev); }
        string msg = "Estradas prontas!\n\n- Estradas de terra ligando portal, vila, ruínas, acampamentos e cemitério\n" +
                     "- No mapa (M), clique num lugar para traçar o caminho (GPS); ele aparece também no mini-mapa\n- Mapa e mini-mapa sem nuvens: tudo aparece\n\n" +
                     "Salve com Ctrl + S." + (avisos.Count > 0 ? "\n\n" + string.Join("\n", avisos) : "");
        if (interativo) EditorUtility.DisplayDialog("Pinglu", msg, "OK"); else Debug.Log("[Estradas] " + string.Join(" | ", avisos));
    }

    // objetos de natureza e árvores da floresta em cima da estrada
    static void OcultarNasEstradas()
    {
        int n = 0;
        foreach (string nomeRaiz in new[] { NOME_RAIZ_MUNDO, NOME_RAIZ_BIOMAS })
        {
            GameObject r = GameObject.Find(nomeRaiz);
            if (r == null) continue;
            bool mundo = nomeRaiz == NOME_RAIZ_MUNDO;
            foreach (Transform t in r.GetComponentsInChildren<Transform>())
            {
                if (t == r.transform) continue;
                Transform pasta = t;
                while (pasta.parent != null && pasta.parent != r.transform) pasta = pasta.parent;
                if (mundo)
                {
                    bool vegetacao = pasta.name == "Natureza" || (pasta.name == "Cemiterio" && (t.parent != null && (t.parent.name == "Floresta Assombrada" || t.parent.name == "Detalhes da Floresta")));
                    if (!vegetacao) continue;
                }
                if (t.GetComponent<Renderer>() == null && t.GetComponent<LODGroup>() == null) continue;
                if (TemPortalNoNome(t)) continue;
                bool noCaminho = false;
                foreach (Forma f in formas) if (Distancia(f, t.position) < f.raio + 1.8f) { noCaminho = true; break; }
                if (!noCaminho) continue;
                t.gameObject.name = PREFIXO_OCULTO_EST + t.gameObject.name;
                t.gameObject.SetActive(false);
                n++;
            }
        }
        avisos.Add(n + " objetos de vegetação tirados do caminho.");
    }

    static void DesfazerEstradas(bool perguntar)
    {
        if (EditorApplication.isPlaying) return;
        if (perguntar && !EditorUtility.DisplayDialog("Pinglu", "Remover as estradas e voltar o terreno como estava antes delas?", "Sim", "Cancelar")) return;
        terreno = Terrain.activeTerrain;
        GameObject e = GameObject.Find("Estradas");
        if (e != null) Undo.DestroyObjectImmediate(e);
        RestaurarTerreno(BACKUP_EST);
        foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g == null || !g.scene.IsValid() || !g.name.StartsWith(PREFIXO_OCULTO_EST)) continue;
            g.name = g.name.Substring(PREFIXO_OCULTO_EST.Length);
            g.SetActive(true);
        }
        AssetDatabase.SaveAssets();
        if (terreno != null) EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        if (perguntar) { try { if (PingluVegetacao.JaFoiCriada()) PingluVegetacao.RefazerSilencioso(); } catch (System.Exception ev) { Debug.LogException(ev); } }
        if (perguntar) EditorUtility.DisplayDialog("Pinglu", "Estradas removidas. Salve a cena com Ctrl + S.", "OK");
    }
}
#endif
