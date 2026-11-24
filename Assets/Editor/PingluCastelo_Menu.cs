// =====================================================================
//  PingluCastelo_Menu.cs  -  menus 45 e 46 (Castelo Destruído)
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    [MenuItem("Pinglu/45. Castelo destruído")]
    public static void ConstruirCasteloMenu() { ConstruirCastelo(true); }

    static void ConstruirCastelo(bool interativo)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;
        GameObject mundo = GameObject.Find(NOME_RAIZ_MUNDO);
        if (mundo == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o mundo gerado (Mundo_Gerado). Rode antes o Pinglu > 9.", "OK"); return; }

        if (GameObject.Find("Estradas") != null) DesfazerEstradas(false);   // as estradas guardam um backup do terreno: desfaz antes
        terreno = Terrain.activeTerrain; dados = terreno.terrainData;

        // onde ficavam as ruínas menores (elas saem; o castelo grande vai para a região delas)
        Vector3 preferido = Vector3.zero; bool temPreferido = false;
        {
            Transform velhoCastelo = mundo.transform.Find(NOME_CASTELO);
            Transform rm = velhoCastelo != null ? velhoCastelo.Find("Ruinas Menores") : null;
            if (rm != null)
            {
                Vector3 soma = Vector3.zero; int cont = 0;
                foreach (Transform t in rm) if (t.name == "Ruina de Castelo") { soma += t.position; cont++; }
                if (cont > 0) { preferido = soma / cont; temPreferido = true; }
            }
        }
        if (mundo.transform.Find(NOME_CASTELO) != null)
        {
            if (interativo && !EditorUtility.DisplayDialog("Pinglu", "O castelo destruído já existe. Quer refazer?", "Sim, refazer", "Cancelar")) return;
            DesfazerCastelo(false, false);
        }
        Transform antigo = mundo.transform.Find("Ruinas da Fortaleza");
        MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
        if (antigo == null && mm == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei as Ruínas da Fortaleza. Rode antes o Pinglu > 9.", "OK"); return; }
        if (interativo && !EditorUtility.DisplayDialog("Pinglu - Castelo Destruído",
            "Vou reconstruir as Ruínas da Fortaleza como um castelo enorme (~140 x 120 m): muralhas e torres destruídas, portaria, torre de menagem com o Salão do Trono, pátio com marcas de batalha.\n\n" +
            "- O castelo vai para o trecho mais plano do Reino Destruído; o terreno só é alisado de leve, o castelo se adapta à colina e as fundações descem até o chão (nada fica voando).\n- As ruínas menores saem.\n- As estradas, o GPS e a vegetação são refeitos.\n\nGuardo um backup do terreno. Pode demorar alguns minutos.", "Pode fazer", "Cancelar")) return;

        avisos.Clear(); formas.Clear(); zonas = new List<Zona>(); tamanhoPrefab.Clear(); castOcupado.Clear();
        luzesCriadas = 0; luzesLobby = 0; contadorMalhas = 0;
        rng = new System.Random(8080);
        pastaMalhas = PASTA_CASTELO + "/Malhas";
        prefixoOculto = PREFIXO_OCULTO_CASTELO;
        Vector3 centroCastelo = Vector3.zero;

        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando...", 0.02f);
            CriarPastaSeFaltar("Assets", "Cenario"); CriarPastaSeFaltar("Assets/Cenario", "Vila"); CriarPastaSeFaltar(PASTA, "Materiais_v2");
            CriarPastaSeFaltar("Assets/Cenario", "Castelo"); CriarPastaSeFaltar(PASTA_CASTELO, "Materiais");
            if (AssetDatabase.IsValidFolder(pastaMalhas)) AssetDatabase.DeleteAsset(pastaMalhas);
            AssetDatabase.CreateFolder(PASTA_CASTELO, "Malhas");
            semFumacaNoFogo = true;   // o castelo não tem fumaça
            ConfigurarTexturas();
            CriarMateriais();
            CastMateriais();

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP_CASTELO) != null) AssetDatabase.DeleteAsset(BACKUP_CASTELO);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dados), BACKUP_CASTELO)) throw new System.Exception("Não consegui fazer backup do terreno.");

            Vector3 spawn = AcharJogador();
            MontarZonasProtegidas(spawn);

            // lugar do castelo: a colina no centro do Reino Destruído (adaptado ao relevo)
            Transform fortaleza = antigo != null ? antigo.Find("Fortaleza em Ruinas") : null;
            Vector3 c = fortaleza != null ? fortaleza.position : (antigo != null ? antigo.position : Vector3.zero);
            if (antigo == null && mm != null) foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Ruinas) c = k.posicao;
            Vector3 o = terreno.transform.position, s = dados.size;
            ClimaDosBiomas clima = Object.FindAnyObjectByType<ClimaDosBiomas>();
            Vector3 centroReino = temPreferido ? preferido : c; float raioReino = 230f;   // o centro do bioma (a colina marcada pelo usuário) vale mais
            if (clima != null)
                foreach (ClimaDosBiomas.Bioma b in clima.biomas)
                    if (b.nome == "Colinas de Pedra" || b.nome == NOME_REINO) { centroReino = b.centro; raioReino = Mathf.Max(150f, b.raio); }
            LimiteDaFloresta limiteF = Object.FindFirstObjectByType<LimiteDaFloresta>();
            float melhorNota = float.MaxValue; Vector3 melhor = Vector3.zero; bool achou = false;
            for (float x = centroReino.x - 70f; x <= centroReino.x + 70f; x += 10f)
                for (float z = centroReino.z - 70f; z <= centroReino.z + 70f; z += 10f)
                {
                    Vector3 p = new Vector3(x, 0f, z);
                    if (!DentroDoTerreno(p, 115f) || PertoDeZonaProtegida(p, 95f)) continue;
                    float mn = float.MaxValue, mx = float.MinValue;
                    for (int ring = 0; ring < 3; ring++)
                        for (int k = 0; k < 16; k++)
                        {
                            float rr = ring * 48f, ang = k * Mathf.PI / 8f;
                            float h = Altura(x + Mathf.Cos(ang) * rr, z + Mathf.Sin(ang) * rr);
                            mn = Mathf.Min(mn, h); mx = Mathf.Max(mx, h);
                            if (ring == 0) break;
                        }
                    float nota = Plano(p - centroReino).magnitude * 0.5f + Mathf.Max(0f, (mx - mn) - 30f) * 2f;   // perto do centro da colina; só foge de paredões
                    if (limiteF != null && limiteF.Valido && limiteF.Contem(x, z)) nota += 150f;   // longe da Floresta Sombria
                    if (mx - mn > 80f) continue;
                    if (nota < melhorNota) { melhorNota = nota; melhor = p; achou = true; }
                }
            if (achou) c = melhor;
            else avisos.Add("Não achei um trecho plano no Reino Destruído; usei o lugar antigo das ruínas.");
            c.x = Mathf.Clamp(c.x, o.x + 105f, o.x + s.x - 105f); c.z = Mathf.Clamp(c.z, o.z + 105f, o.z + s.z - 105f);
            Vector3 alvoOlhar = spawn;
            if (mm != null) foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Vila) alvoOlhar = k.posicao;
            Vector3 paraVila = Plano(alvoOlhar - c);
            Quaternion rot = paraVila.sqrMagnitude > 1f ? Quaternion.LookRotation(paraVila.normalized, Vector3.up) : Quaternion.identity;

            EditorUtility.DisplayProgressBar("Pinglu", "Aplainando o terreno e limpando...", 0.1f);
            AplainarLocal(c, 70f, 150f, 0.5f);   // só alisa um pouco: o castelo se adapta à colina (muros e fundações seguem o chão)
            c.y = Altura(c.x, c.z);
            centroCastelo = c;
            List<Vector3> menores = new List<Vector3>();   // sem ruínas menores: o castelo grande ocupa o Reino Destruído

            formas.Add(Circulo(c, 100f, -1));            // só limpa árvores e capim (a vegetação do bioma volta em volta depois)
            foreach (Vector3 p in menores) { formas.Add(Circulo(p, 36f, -1)); formas.Add(Circulo(p, 24f, 1)); }
            Vector3 longe = new Vector3(-100000f, 0f, -100000f);
            LimparArvores(longe, 0f);
            LimparDetalhesFormas();
            PintarChao();
            CastOcultar(c, 112f);
            foreach (Vector3 p in menores) CastOcultar(p, 40f);
            if (antigo != null) { antigo.name = PREFIXO_OCULTO_CASTELO + antigo.name; antigo.gameObject.SetActive(false); }

            GameObject r = new GameObject(NOME_CASTELO);
            r.transform.SetParent(mundo.transform, false);
            Undo.RegisterCreatedObjectUndo(r, "Castelo destruido");
            raiz = r.transform;
            Transform luzes = Pasta("Luzes"), efeitos = Pasta("Efeitos"), estrutura = Pasta("Estrutura");
            estrutura.position = c; estrutura.rotation = rot;
            GameObject entradaGo = new GameObject("Entrada");
            entradaGo.transform.SetParent(raiz, false);
            castC = c; castRot = rot;
            entradaGo.transform.position = new Vector3(0f, 0f, 0f) + CW(new Vector3(0f, 0f, CAST_P * 0.5f + 16f));
            entradaGo.transform.position = new Vector3(entradaGo.transform.position.x, Altura(entradaGo.transform.position.x, entradaGo.transform.position.z), entradaGo.transform.position.z);
            entradaGo.transform.rotation = rot;

            EditorUtility.DisplayProgressBar("Pinglu", "Muralhas e torres...", 0.3f);
            Malha muralha = new Malha(), entulho = new Malha(), madeira = new Malha(), menagem = new Malha(), ouro = new Malha(), patio = new Malha();
            float HX = CAST_L * 0.5f, HZ = CAST_P * 0.5f, H = 11f, T = 3.2f;
            // torres: cantos grandes, torres do meio dos muros e portaria
            float[] alturas = { 25f, 17f, 21f, 12f };
            float[] quebras = { 0.3f, 0.8f, 0.55f, 0.95f };
            Vector3[] cantos = { new Vector3(-HX, 0f, HZ), new Vector3(HX, 0f, HZ), new Vector3(HX, 0f, -HZ), new Vector3(-HX, 0f, -HZ) };
            for (int k = 0; k < 4; k++) CTorre(muralha, entulho, cantos[k], 9f, alturas[k], 22, quebras[k], Faixa(0f, 1f));
            Vector3[] meios = { new Vector3(-HX, 0f, 0f), new Vector3(HX, 0f, 0f), new Vector3(-45f, 0f, -HZ), new Vector3(45f, 0f, -HZ) };
            foreach (Vector3 mt in meios) CTorre(muralha, entulho, mt, 5.8f, Faixa(14f, 19f), 16, Faixa(0.4f, 0.9f), Faixa(0f, 1f));
            CPortaria(muralha, entulho, HZ);
            // muros entre as torres
            System.Action<Vector3, Vector3, Vector3, float, float> muro = (a, b, fora, ra, rb) =>
            {
                Vector3 d = (b - a).normalized;
                CMuro(muralha, entulho, a + d * ra * 0.9f, b - d * rb * 0.9f, fora, H, T, 0.55f, null, true);
            };
            muro(cantos[0], new Vector3(-13.4f - 7.2f, 0f, HZ) + Vector3.zero, Vector3.forward, 9f, 0f);
            muro(new Vector3(13.4f + 7.2f, 0f, HZ), cantos[1], Vector3.forward, 0f, 9f);
            muro(cantos[1], meios[1], Vector3.right, 9f, 5.8f);
            muro(meios[1], cantos[2], Vector3.right, 5.8f, 9f);
            muro(cantos[2], meios[3], Vector3.back, 9f, 5.8f);
            muro(meios[3], meios[2], Vector3.back, 5.8f, 5.8f);
            muro(meios[2], cantos[3], Vector3.back, 5.8f, 9f);
            muro(cantos[3], meios[0], Vector3.left, 9f, 5.8f);
            muro(meios[0], cantos[0], Vector3.left, 5.8f, 9f);

            EditorUtility.DisplayProgressBar("Pinglu", "Torre de menagem e Salão do Trono...", 0.5f);
            CMenagem(menagem, entulho, ouro, -26f, luzes, efeitos);
            EditorUtility.DisplayProgressBar("Pinglu", "Pátio de batalha...", 0.66f);
            CPatio(patio, entulho, madeira, efeitos, raiz);

            EditorUtility.DisplayProgressBar("Pinglu", "Criando os modelos...", 0.74f);
            Instanciar(muralha, "Muralhas e Torres", c, rot, estrutura, true);
            Instanciar(menagem, "Torre de Menagem e Salao do Trono", c, rot, estrutura, true);
            Instanciar(patio, "Patio do Castelo", c, rot, estrutura, true);
            Instanciar(entulho, "Entulho do Castelo", c, rot, estrutura, false);
            Instanciar(madeira, "Madeira do Castelo", c, rot, estrutura, true);
            Instanciar(ouro, "Ouro do Trono", c, rot, estrutura, false);

            EditorUtility.DisplayProgressBar("Pinglu", "Ruínas menores nas colinas...", 0.84f);
            Transform pastaMenores = Pasta("Ruinas Menores");
            for (int i = 0; i < menores.Count; i++)
            {
                Quaternion rm = Quaternion.Euler(0f, Faixa(0f, 360f), 0f);
                CRuinaMenor(pastaMenores, menores[i], rm, i % 2);
            }
            castC = c; castRot = rot;

            // nomes no mapa, mini-mapa e clima
            if (mm != null)
            {
                foreach (MiniMapa.Marcador k in mm.marcadores)
                    if (k.tipo == MiniMapa.Tipo.Ruinas) { k.nome = NOME_PONTO_CASTELO; k.posicao = c; }
                EditorUtility.SetDirty(mm);
            }
            RenomearBioma("Colinas de Pedra", NOME_REINO);
            avisos.Add("Castelo Destruído em (" + Mathf.RoundToInt(c.x) + ", " + Mathf.RoundToInt(c.z) + "), " + menores.Count + " ruínas menores nas colinas.");

            terreno.Flush();
            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
            Selection.activeGameObject = r;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console). Use Pinglu > 46 para voltar ao que era.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            pastaMalhas = PASTA_MALHAS; prefixoOculto = PREFIXO_OCULTO; semFumacaNoFogo = false;
        }

        try { avisos.Add("Assentar: " + PingluAssentar.Executar(false).Split('\n')[0]); } catch (System.Exception e) { Debug.LogException(e); }
        List<string> avisosCastelo = new List<string>(avisos);
        ConstruirEstradas(false);   // as estradas e o GPS dependem do lugar do castelo
        avisos.InsertRange(0, avisosCastelo);

        string msg = "Castelo Destruído pronto!\n\n- Na Hierarchy: Mundo_Gerado > Castelo Destruido (dois cliques para ir até lá)\n" +
                     "- Muralhas e torres quebradas, portaria, torre de menagem com o Salão do Trono, pátio de batalha\n" +
                     "- Castelo no " + NOME_REINO + " (as ruínas menores saíram)\n- Estradas, GPS e vegetação refeitos\n\nSalve com Ctrl + S.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        if (interativo) EditorUtility.DisplayDialog("Pinglu", msg, "OK"); else Debug.Log("[Castelo] " + string.Join(" | ", avisos));
    }

    static void RenomearBioma(string de, string para)
    {
        ClimaDosBiomas clima = Object.FindAnyObjectByType<ClimaDosBiomas>();
        if (clima == null) return;
        SerializedObject so = new SerializedObject(clima);
        SerializedProperty arr = so.FindProperty("biomas");
        for (int i = 0; i < arr.arraySize; i++)
        {
            SerializedProperty n = arr.GetArrayElementAtIndex(i).FindPropertyRelative("nome");
            if (n.stringValue == de) n.stringValue = para;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // esconde (sem apagar) objetos de natureza já gerados dentro do raio
    static void CastOcultar(Vector3 c, float raio)
    {
        foreach (string nomeRaiz in new[] { NOME_RAIZ_MUNDO, NOME_RAIZ_BIOMAS })
        {
            GameObject rt = GameObject.Find(nomeRaiz);
            if (rt == null) continue;
            bool mundo = nomeRaiz == NOME_RAIZ_MUNDO;
            foreach (Transform t in rt.GetComponentsInChildren<Transform>())
            {
                if (t == rt.transform) continue;
                Transform pasta = t;
                while (pasta.parent != null && pasta.parent != rt.transform) pasta = pasta.parent;
                if (mundo && pasta.name != "Natureza") continue;
                if (t.GetComponent<Renderer>() == null && t.GetComponent<Light>() == null) continue;
                if (Plano(t.position - c).magnitude > raio || TemPortalNoNome(t)) continue;
                t.gameObject.name = PREFIXO_OCULTO_CASTELO + t.gameObject.name;
                t.gameObject.SetActive(false);
            }
        }
    }

    [MenuItem("Pinglu/46. Desfazer castelo destruído")]
    public static void DesfazerCasteloMenu() { DesfazerCastelo(true); }

    static void DesfazerCastelo(bool perguntar, bool refazerEstradas = true)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (perguntar && !EditorUtility.DisplayDialog("Pinglu", "Remover o castelo destruído e voltar às ruínas pequenas e à colina como eram?", "Sim", "Cancelar")) return;
        terreno = Terrain.activeTerrain;
        if (GameObject.Find("Estradas") != null) DesfazerEstradas(false);
        GameObject mundo = GameObject.Find(NOME_RAIZ_MUNDO);
        if (mundo != null)
        {
            Transform novo = mundo.transform.Find(NOME_CASTELO);
            if (novo != null) Undo.DestroyObjectImmediate(novo.gameObject);
        }
        RestaurarTerreno(BACKUP_CASTELO);
        foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g == null || !g.scene.IsValid() || !g.name.StartsWith(PREFIXO_OCULTO_CASTELO)) continue;
            g.name = g.name.Substring(PREFIXO_OCULTO_CASTELO.Length);
            g.SetActive(true);
        }
        MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
        if (mm != null) { foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Ruinas) k.nome = "Ruínas da Fortaleza"; EditorUtility.SetDirty(mm); }
        RenomearBioma(NOME_REINO, "Colinas de Pedra");
        AssetDatabase.SaveAssets();
        if (terreno != null) EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        if (refazerEstradas) ConstruirEstradas(false);
        if (perguntar) EditorUtility.DisplayDialog("Pinglu", "Castelo removido. As ruínas antigas voltaram.\nSalve com Ctrl + S.", "OK");
    }
}
#endif
