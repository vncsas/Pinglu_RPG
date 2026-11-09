// =====================================================================
//  PingluCemiterio.cs  -  Cemitério grande no meio de uma floresta assombrada
// ---------------------------------------------------------------------
//  - troca o cemitério pequeno por um cemitério enorme (64 x 56 m): muralha
//    com grades, portal em arco, praça com cruz, altar, mausoléus, estátuas,
//    sarcófagos, lápides, carro funerário
//  - planta uma floresta densa de árvores mortas em volta, com neblina
//  - cria novos monstros (Carniceiro, Guardião, Brutamontes e o chefe Rei
//    dos Ossos) e liga o novo sistema de hordas
//  - cria a emboscada da floresta: esqueletos aparecem entre as árvores
//    quando você se aproxima do cemitério
//
//  COMO USAR:  Pinglu > 31. Cemitério grande e floresta  (Play desligado)
//  Para voltar ao cemitério antigo: Pinglu > 32. Desfazer cemitério grande
//  (precisa do mundo gerado: Pinglu > 3 e > 9, e do combate: Pinglu > 11)
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class PingluVila
{
    const string PASTA_CEM = "Assets/Cenario/Cemiterio";
    const string BACKUP_CEM = PASTA_CEM + "/Backup_Terreno_Cemiterio.asset";
    const string PREFIXO_OCULTO_CEM = "[oculto pelo cemiterio] ";
    const string NOME_ANTIGO_CEM = "Camposanto_Antigo_Oculto";
    const string NOME_BIOMA_CEM = "Floresta Assombrada";
    const float CEM_RAIO_FLORESTA = 260f;   // nas laterais e atrás
    const float CEM_RAIO_FUNDO = 820f;      // a floresta também enche o setor do mapa entre o cemitério e o resto do mundo
    const float CEM_ANGULO_FUNDO = 85f;     // meio-ângulo do setor (graus, a partir da frente do cemitério)
    const float CEM_RAIO_FRENTE = 560f;     // na frente (rumo ao início do mapa): a travessia longa

    const string PREFAB_ESQUELETO = "Assets/Personagens/Inimigos/Esqueleto/Esqueleto.prefab";
    const string PASTA_VARIANTES = "Assets/Personagens/Inimigos/Esqueleto/Variantes";
    const string LISTA_REDE_CEM = "Assets/Personagens/Jogadores/Prefabs de Rede.asset";
    const float CEM_LARGURA_CAMINHO = 120f; // metade da largura da faixa de floresta ao longo da trilha

    // densidade da floresta num ponto (0 = fora). Círculo em volta do cemitério + faixa longa na frente
    // a Floresta Sombria marcada no mapa (criada pelo menu 43): fora do contorno não nasce árvore sinistra
    static LimiteDaFloresta limiteFlorestaCache; static bool limiteFlorestaProcurado;
    static bool ForaDoLimiteDaFloresta(Vector3 p)
    {
        if (!limiteFlorestaProcurado) { limiteFlorestaCache = Object.FindFirstObjectByType<LimiteDaFloresta>(); limiteFlorestaProcurado = true; }
        return limiteFlorestaCache != null && limiteFlorestaCache.Valido && !limiteFlorestaCache.Contem(p.x, p.z);
    }

    static float CemDensidade(Vector3 p)
    {
        if (ForaDoLimiteDaFloresta(p)) return 0f;
        Vector3 d = p - cemC; d.y = 0f;
        float dist = d.magnitude;
        float dens = 0f;
        if (dist < CEM_RAIO_FLORESTA) dens = Mathf.Lerp(1f, 0.4f, Mathf.InverseLerp(40f, CEM_RAIO_FLORESTA, dist));
        Vector3 f = cemRot * Vector3.forward;
        float ao = Vector3.Dot(d, f);
        float lat = Mathf.Abs(d.x * f.z - d.z * f.x);
        if (ao > 0f && ao < CEM_RAIO_FRENTE && lat < CEM_LARGURA_CAMINHO)
        {
            float lado = 1f - (lat / CEM_LARGURA_CAMINHO) * (lat / CEM_LARGURA_CAMINHO);
            dens = Mathf.Max(dens, Mathf.Lerp(1f, 0.7f, ao / CEM_RAIO_FRENTE) * Mathf.Sqrt(Mathf.Max(0f, lado)));
        }
        return dens;
    }

    // esconde cenário gerado (prefabs) sem tocar em nada perto de lugares protegidos (início, portal, vila...)
    static bool PertoDeZonaProtegida(Vector3 p, float folga)
    {
        foreach (Zona z in zonas) if (Plano(p - z.pos).magnitude < z.raio + folga) return true;
        return false;
    }

    static bool TemPortalNoNome(Transform t)
    {
        for (Transform x = t; x != null; x = x.parent) if (x.name.ToLower().Contains("portal")) return true;
        return false;
    }

    static void OcultarCenarioSeguro(Vector3 c, float raio)
    {
        GameObject cen = GameObject.Find("Cenario_Gerado");
        if (cen == null) return;
        foreach (Transform t in cen.GetComponentsInChildren<Transform>())
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && t.GetComponent<ParticleSystem>() == null) continue;
            if (Plano(t.position - c).magnitude > raio || PertoDeZonaProtegida(t.position, 0f) || TemPortalNoNome(t)) continue;
            t.gameObject.name = prefixoOculto + t.gameObject.name;
            t.gameObject.SetActive(false);
        }
    }

    static Transform CemRaiz() { GameObject mundo = GameObject.Find(NOME_RAIZ_MUNDO); return mundo != null ? mundo.transform.Find("Cemiterio") : null; }

    static GameObject AcharInativo(string nome)
    {
        foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>())
            if (g != null && g.scene.IsValid() && g.name == nome) return g;
        return null;
    }

    // ==================================================================
    //  MENU 31
    // ==================================================================
    [MenuItem("Pinglu/31. Cemitério grande e floresta")]
    public static void ConstruirCemiterioGrandeMenu() { ConstruirCemiterioGrande(true); }

    static void ConstruirCemiterioGrande(bool interativo)
    {
        limiteFlorestaProcurado = false;
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.\n\nO que muda na cena durante o Play é perdido.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;

        // as estradas guardam um backup do terreno de QUANDO foram feitas: desfazê-las antes mantém o terreno coerente
        if (GameObject.Find("Estradas") != null) DesfazerEstradas(false);
        terreno = Terrain.activeTerrain; dados = terreno.terrainData;

        Transform existente = CemRaiz();
        if (existente != null && existente.Find("Muralha") != null)
        {
            if (interativo && !EditorUtility.DisplayDialog("Pinglu", "O cemitério grande já existe. Quer refazer?", "Sim, refazer", "Cancelar")) return;
            DesfazerCemiterioGrande(false);
        }
        Transform antigo = CemRaiz();
        if (antigo == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei o cemitério atual (Mundo_Gerado > Cemiterio).\nRode antes o Pinglu > 9. Espalhar pontos pelo mapa.", "OK");
            return;
        }
        if (interativo && !EditorUtility.DisplayDialog("Pinglu - Cemitério grande",
            "Vou construir o cemitério enorme no fundo do mapa, bem longe do início, e plantar a floresta de árvores mortas entre ele e a vila (o jogador precisa atravessá-la), " +
            "criar novos monstros e a emboscada da floresta.\n\nAntes guardo um backup do terreno. Pode demorar alguns minutos.", "Pode fazer", "Cancelar")) return;

        avisos.Clear(); formas.Clear(); zonas = new List<Zona>(); tamanhoPrefab.Clear();
        luzesCriadas = 0; contadorMalhas = 0;
        malhaProp = new Mesh[8]; matsProp = new Material[8][];
        malhaArvoreSeca = new Mesh[3]; matsArvoreSeca = new Material[3][];
        malhaTronco = new Mesh[3]; matsTronco = new Material[3][];
        malhaToco = new Mesh[2]; matsToco = new Material[2][];
        rng = new System.Random(6660);
        pastaMalhas = PASTA_CEM + "/Malhas";
        prefixoOculto = PREFIXO_OCULTO_CEM;

        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando...", 0.02f);
            CriarPastaSeFaltar("Assets", "Cenario"); CriarPastaSeFaltar("Assets/Cenario", "Vila"); CriarPastaSeFaltar(PASTA, "Materiais_v2");
            CriarPastaSeFaltar("Assets/Cenario", "Cemiterio");
            if (AssetDatabase.IsValidFolder(pastaMalhas)) AssetDatabase.DeleteAsset(pastaMalhas);
            AssetDatabase.CreateFolder(PASTA_CEM, "Malhas");
            ConfigurarTexturas();
            CriarMateriais();

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP_CEM) != null) AssetDatabase.DeleteAsset(BACKUP_CEM);
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dados), BACKUP_CEM)) throw new System.Exception("Não consegui fazer backup do terreno.");

            // zonas protegidas (vila, início, outros pontos): sem árvores novas em cima
            EditorUtility.DisplayProgressBar("Pinglu", "Escondendo o cemitério antigo...", 0.06f);
            antigo.name = NOME_ANTIGO_CEM;
            antigo.gameObject.SetActive(false);
            Vector3 spawn = AcharJogador();
            MontarZonasProtegidas(spawn);

            EditorUtility.DisplayProgressBar("Pinglu", "Procurando um lugar bem longe do início...", 0.09f);
            Transform internoAntigo = antigo.Find("Cemiterio");
            Vector3 centroAntigo = internoAntigo != null ? internoAntigo.position : antigo.position;
            if (!EscolherLugarDoCemiterio(spawn, centroAntigo, out cemC))
                avisos.Add("Não achei um lugar longe e plano o bastante: usei o lugar do cemitério antigo.");
            Vector3 paraInicio = Plano(spawn - cemC);
            cemRot = paraInicio.sqrMagnitude > 1f ? Quaternion.LookRotation(paraInicio.normalized, Vector3.up) : Quaternion.identity;

            EditorUtility.DisplayProgressBar("Pinglu", "Aplainando e limpando o terreno...", 0.12f);
            Vector3 frente = cemRot * Vector3.forward;
            Vector3 portao = cemC + frente * (CEM_P * 0.5f);
            AplainarLocal(cemC, 140f, 195f, 1f);   // peso 1: o chão do cemitério fica perfeitamente plano
            formas.Add(Circulo(cemC, 112f, 1));
            formas.Add(Segmento(portao, portao + frente * (CEM_RAIO_FRENTE - 10f), 2.6f, 1));
            LimparArvores(cemC, CEM_RAIO_FLORESTA);
            for (float k = 200f; k <= CEM_RAIO_FRENTE; k += 100f)
                if (!PertoDeZonaProtegida(cemC + frente * k, CEM_LARGURA_CAMINHO + 20f)) LimparArvores(cemC + frente * k, CEM_LARGURA_CAMINHO + 20f);
            LimparDetalhesFormas();
            PintarChao();
            OcultarCenarioSeguro(cemC, CEM_RAIO_FLORESTA);
            OcultarObjetosNoRaio(cemC, CEM_RAIO_FLORESTA);
            for (float k = 200f; k <= CEM_RAIO_FRENTE; k += 100f) { OcultarCenarioSeguro(cemC + frente * k, CEM_LARGURA_CAMINHO + 20f); OcultarObjetosNoRaio(cemC + frente * k, CEM_LARGURA_CAMINHO + 20f); }

            GameObject r = new GameObject("Cemiterio");
            r.transform.SetParent(GameObject.Find(NOME_RAIZ_MUNDO).transform, false);
            Undo.RegisterCreatedObjectUndo(r, "Cemiterio grande");
            raiz = r.transform;

            EditorUtility.DisplayProgressBar("Pinglu", "Construindo a muralha e o portal...", 0.22f);
            Transform luzes = Pasta("Luzes");
            Pasta("Colisores");
            Malha pedra = new Malha(), ferro = new Malha();
            CemMuralha(pedra, ferro);
            CemPortal(pedra, ferro, luzes);
            CemCaminho(pedra);

            EditorUtility.DisplayProgressBar("Pinglu", "Praça, altar e mausoléus...", 0.34f);
            float HX = CEM_L * 0.5f, HZ = CEM_P * 0.5f;
            cemOcupados.Clear();
            CemPraca(pedra, luzes);
            CemAltar(pedra, luzes);
            cemOcupados.Add(new Vector4(0f, -(HZ - 27f), 0f, 16f));                      // altar
            Malha maus = new Malha();
            System.Action<Vector3, float, float> mausoleu = (pos, yaw, esc) =>
            {
                CemMausoleu(maus, pos, yaw, esc, luzes);
                cemOcupados.Add(new Vector4(pos.x, pos.z, 0f, 9.5f * esc));
            };
            foreach (int sd in new[] { -1, 1 })
                foreach (float zz in new[] { 46f, 6f, -34f })
                    mausoleu(new Vector3(sd * (HX - 16f), 0f, zz), sd > 0 ? -90f : 90f, 1.3f);
            mausoleu(new Vector3(-50f, 0f, -HZ + 14f), 0f, 1.5f);
            mausoleu(new Vector3(50f, 0f, -HZ + 14f), 0f, 1.5f);
            mausoleu(new Vector3(0f, 0f, -HZ + 13f), 0f, 1.9f);

            EditorUtility.DisplayProgressBar("Pinglu", "Estátuas e sarcófagos...", 0.45f);
            Malha estatuas = new Malha();
            // estátuas (modelo real) em volta da arena e ao longo do caminho; bustos nas colunas
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f + Mathf.PI / 16f;
                Vector3 p = new Vector3(Mathf.Cos(a) * (CEM_PR + 7f), 0f, Mathf.Sin(a) * (CEM_PR + 7f));
                if (p.z > 18f && Mathf.Abs(p.x) < 10f) continue;          // não bloqueia o caminho
                if (p.z < -38f && Mathf.Abs(p.x) < 24f) continue;         // nem o altar
                CemEstatua(estatuas, p, Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg, Faixa(0.92f, 1.1f));
                cemOcupados.Add(new Vector4(p.x, p.z, 0f, 3.5f));
            }
            foreach (int sd in new[] { -1, 1 })
            {
                foreach (float zz in new[] { HZ - 14f, HZ - 30f, HZ - 46f })
                {
                    CemEstatua(estatuas, new Vector3(sd * 10f, 0f, zz), sd > 0 ? 200f : 160f, 1f);
                    cemOcupados.Add(new Vector4(sd * 10f, zz, 0f, 3.5f));
                }
                for (float zz = HZ - 8f; zz > CEM_PR + 4f; zz -= 8f) CemBusto(estatuas, new Vector3(sd * 5.8f, 0f, zz), sd > 0 ? 250f : 110f);
            }
            Malha sarc = new Malha();
            Malha carro = new Malha(), carroFerro = new Malha();
            // sarcófagos, covas abertas e carros funerários espalhados pelos campos de lápides
            System.Func<float, Vector3?> sortear = raio =>
            {
                for (int tent = 0; tent < 60; tent++)
                {
                    Vector3 q = new Vector3(Faixa(-HX + 9f, HX - 9f), 0f, Faixa(-HZ + 9f, HZ - 9f));
                    if (new Vector2(q.x, q.z).magnitude < CEM_PR + 6f + raio) continue;
                    if (Mathf.Abs(q.x) < 6f + raio && q.z > 0f) continue;
                    bool ruim = false;
                    foreach (Vector4 o in cemOcupados) if (new Vector2(q.x - o.x, q.z - o.y).magnitude < o.w + raio) { ruim = true; break; }
                    if (ruim) continue;
                    cemOcupados.Add(new Vector4(q.x, q.z, 0f, raio));
                    return q;
                }
                return null;
            };
            for (int i = 0; i < 18; i++) { Vector3? q = sortear(3.2f); if (q.HasValue) CemSarcofago(sarc, q.Value, Faixa(0f, 360f), i % 2); }
            for (int i = 0; i < 14; i++) { Vector3? q = sortear(3f); if (q.HasValue) CemCova(sarc, q.Value, Faixa(0f, 360f)); }
            for (int i = 0; i < 2; i++) { Vector3? q = sortear(5f); if (q.HasValue) CemCarro(carro, carroFerro, q.Value, Faixa(0f, 360f)); }

            EditorUtility.DisplayProgressBar("Pinglu", "Capelas, jazigos, chafarizes e outros detalhes...", 0.5f);
            Malha extras = new Malha();
            System.Func<Vector3, float> paraOCentro = pq => Mathf.Atan2(-pq.x, -pq.z) * Mathf.Rad2Deg;
            for (int i = 0; i < 2; i++) { Vector3? q = sortear(15f); if (q.HasValue) CemCapela(maus, q.Value, paraOCentro(q.Value) + Faixa(-25f, 25f), 1f, luzes); }
            for (int i = 0; i < 5; i++) { Vector3? q = sortear(3f); if (q.HasValue) CemAlcapao(extras, q.Value, Faixa(0f, 360f)); }
            for (int i = 0; i < 2; i++) { Vector3? q = sortear(5.5f); if (q.HasValue) CemChafariz(extras, q.Value); }
            for (int i = 0; i < 2; i++) { Vector3? q = sortear(6f); if (q.HasValue) CemCalvario(extras, q.Value, paraOCentro(q.Value)); }
            for (int i = 0; i < 9; i++) { Vector3? q = sortear(5f); if (q.HasValue) CemJazigo(extras, q.Value, Faixa(0f, 360f)); }
            for (int i = 0; i < 8; i++) { Vector3? q = sortear(2.8f); if (q.HasValue) CemOssario(extras, q.Value); }
            for (int i = 0; i < 5; i++) { Vector3? q = sortear(3f); if (q.HasValue) CemForca(extras, q.Value, Faixa(0f, 360f), luzes, i < 2); }
            for (int i = 0; i < 6; i++) { Vector3? q = sortear(4f); if (q.HasValue) CemArcoQuebrado(extras, q.Value, Faixa(0f, 360f)); }
            for (int i = 0; i < 6; i++) { Vector3? q = sortear(2.6f); if (q.HasValue) CemCatafalco(extras, q.Value, Faixa(0f, 360f)); }
            for (int i = 0; i < 6; i++) { Vector3? q = sortear(2.6f); if (q.HasValue) CemObeliscoAlto(extras, q.Value); }
            for (int i = 0; i < 24; i++) { Vector3? q = sortear(1.1f); if (q.HasValue) CemUrnaEmPedestal(extras, q.Value, Faixa(0f, 360f)); }
            // bancos em volta da arena e ao longo do caminho
            for (int i = 0; i < 14; i++)
            {
                float ang = i * Mathf.PI * 2f / 14f + 0.2f;
                Vector3 bp = new Vector3(Mathf.Cos(ang) * (CEM_PR + 2.8f), 0f, Mathf.Sin(ang) * (CEM_PR + 2.8f));
                if (bp.z > 14f && Mathf.Abs(bp.x) < 10f) continue;
                if (bp.z < -30f && Mathf.Abs(bp.x) < 26f) continue;
                CemBanco(extras, bp, paraOCentro(bp));
            }
            foreach (int sd in new[] { -1, 1 })
                for (float zz = HZ - 12f; zz > CEM_PR + 8f; zz -= 16f) CemBanco(extras, new Vector3(sd * 8.5f, 0f, zz), sd > 0 ? 90f : -90f);
            // muros de nichos (colunários) por dentro da muralha da frente, dos dois lados do portão
            foreach (int sd in new[] { -1, 1 })
            {
                CemColunario(extras, new Vector3(sd * 40f, 0f, HZ - 3.5f), 180f, 44f);
                for (int i = -2; i <= 2; i++) cemOcupados.Add(new Vector4(sd * 40f + i * 10f, HZ - 6f, 0f, 7f));
            }
            CemCaminhosSecundarios(pedra);

            EditorUtility.DisplayProgressBar("Pinglu", "Lápides...", 0.55f);
            Malha[] quadrantes = { new Malha(), new Malha(), new Malha(), new Malha() };
            float deslocamento = Faixa(0f, 100f);
            for (float x = -HX + 6f; x < HX - 6f; x += 3.1f)
                for (float z = -HZ + 6f; z < HZ - 6f; z += 3.7f)
                {
                    Vector3 q = new Vector3(x + Faixa(-0.4f, 0.4f), 0f, z + Faixa(-0.4f, 0.4f));
                    if (new Vector2(q.x, q.z).magnitude < CEM_PR + 5f) continue;                       // arena livre
                    if (Mathf.Abs(q.x) < 5.5f && q.z > 0f) continue;                                    // caminho
                    if (Mathf.Abs(q.z) < 3.8f && Mathf.Abs(q.x) > CEM_PR) continue;                     // caminhos laterais
                    if (Mathf.Abs(q.x) < 3.8f && q.z < 0f) continue;                                    // caminho do altar
                    float ruido = Mathf.PerlinNoise((q.x + deslocamento) * 0.045f, (q.z + deslocamento) * 0.045f);
                    double dens = ruido > 0.56f ? 0.75 : (ruido > 0.38f ? 0.3 : 0.05);                  // manchas de lápides e clareiras
                    if (rng.NextDouble() > dens) continue;
                    bool ruim = false;
                    foreach (Vector4 o in cemOcupados) if (new Vector2(q.x - o.x, q.z - o.y).magnitude < o.w + 1.2f) { ruim = true; break; }
                    if (ruim) continue;
                    CemTumulo(quadrantes[(q.x < 0f ? 0 : 1) + (q.z < 0f ? 0 : 2)], q, rng.Next(0, 8));
                }
            Malha tumulos = quadrantes[0];

            EditorUtility.DisplayProgressBar("Pinglu", "Criando os objetos da cena...", 0.62f);
            Instanciar(pedra, "Cemiterio", cemC, cemRot, raiz, true);
            Instanciar(ferro, "Muralha", cemC, cemRot, raiz, false);
            Instanciar(maus, "Mausoleus e capelas", cemC, cemRot, raiz, true);
            Instanciar(extras, "Elementos do cemiterio", cemC, cemRot, raiz, true);
            Instanciar(estatuas, "Estatuas", cemC, cemRot, raiz, true);
            Instanciar(sarc, "Sarcofagos", cemC, cemRot, raiz, true);
            for (int qd = 0; qd < 4; qd++) Instanciar(quadrantes[qd], "Tumulos " + (qd + 1), cemC, cemRot, raiz, true);
            Instanciar(carro, "Carro Funerario", cemC, cemRot, raiz, true);
            Instanciar(carroFerro, "Carro Funerario (ferro)", cemC, cemRot, raiz, false);

            // postes com lanterna ao longo do caminho e árvores gigantes dentro dos muros
            Transform arvores = Pasta("Arvores do Cemiterio");
            for (int i = 0; i < 4; i++)
                foreach (int sd in new[] { -1, 1 })
                    Poste(luzes, MundoDe(new Vector3(sd * 4.2f, 0f, HZ - 8f - i * 14f)), cemRot * Quaternion.Euler(0f, -sd * 90f, 0f));
            for (int i = 0; i < 16; i++)
            {
                Vector3? q = sortear(5f);
                if (q.HasValue) ColocarArvoreSinistra(arvores, MundoDe(q.Value), Faixa(1.3f, 1.9f), rng.Next(0, 6));
            }

            EditorUtility.DisplayProgressBar("Pinglu", "Plantando a floresta assombrada...", 0.72f);
            PlantarFlorestaAssombrada();
            EditorUtility.DisplayProgressBar("Pinglu", "Enchendo o fundo do mapa de floresta...", 0.78f);
            PlantarFlorestaDeFundo();

            EditorUtility.DisplayProgressBar("Pinglu", "Neblina...", 0.84f);
            CriarNevoaDoCemiterio();
            AdicionarBiomaDoCemiterio();

            EditorUtility.DisplayProgressBar("Pinglu", "Criando os monstros e as hordas...", 0.9f);
            GameObject[] variantes = CriarVariantesDeMonstros();
            CriarHordaGrande(variantes);

            avisos.Add(PingluDesempenho.Otimizar());
            terreno.Flush();
            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
            Selection.activeGameObject = r;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console). Use Pinglu > 32 para voltar ao cemitério antigo.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            pastaMalhas = PASTA_MALHAS; prefixoOculto = PREFIXO_OCULTO;
        }

        // as estradas e o GPS dependem do lugar do cemitério: refaz junto (assim nunca ficam desencontrados)
        List<string> avisosCemiterio = new List<string>(avisos);
        ConstruirEstradas(false);
        avisos.InsertRange(0, avisosCemiterio);

        string msg = "Cemitério grande pronto!\n\n" +
                     "- Na Hierarchy: Mundo_Gerado > Cemiterio (dê dois cliques para ir até lá)\n" +
                     "- Horda do Cemiterio: 5 tipos de monstro, ondas por orçamento, chefe a cada 5 ondas\n" +
                     "- Emboscada da Floresta: esqueletos saem das árvores quando você se aproxima\n\n" +
                     "As estradas e o GPS também foram refeitos para o novo lugar.\nSalve com Ctrl + S, dê Play e aperte F2 para ir à entrada da floresta.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        if (interativo) EditorUtility.DisplayDialog("Pinglu", msg, "OK"); else Debug.Log("[Cemiterio] " + string.Join(" | ", avisos));
    }

    // ==================================================================
    //  MENU 32
    // ==================================================================
    // para rodar sem ninguém: Unity -batchmode -executeMethod PingluVila.RefazerCemiterioEEstradasEFechar
    public static void RefazerCemiterioEEstradasEFechar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        ConstruirCemiterioGrande(false);   // já desfaz e refaz as estradas
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    [MenuItem("Pinglu/32. Desfazer cemitério grande")]
    public static void DesfazerCemiterioGrandeMenu() { DesfazerCemiterioGrande(true); }

    static void DesfazerCemiterioGrande(bool perguntar)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (perguntar && !EditorUtility.DisplayDialog("Pinglu", "Remover o cemitério grande e a floresta e voltar ao cemitério antigo?", "Sim", "Cancelar")) return;
        terreno = Terrain.activeTerrain;
        Transform novo = CemRaiz();
        if (novo != null && novo.Find("Muralha") != null) Undo.DestroyObjectImmediate(novo.gameObject);
        foreach (string n in new[] { "Horda do Cemiterio", "Emboscada da Floresta", "Nevoa do Cemiterio" })
        {
            GameObject g = AcharInativo(n);
            if (g != null) Undo.DestroyObjectImmediate(g);
        }
        RestaurarTerreno(BACKUP_CEM);
        GameObject velho = AcharInativo(NOME_ANTIGO_CEM);
        if (velho != null) { velho.name = "Cemiterio"; velho.SetActive(true); }
        foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g == null || !g.scene.IsValid() || !g.name.StartsWith(PREFIXO_OCULTO_CEM)) continue;
            g.name = g.name.Substring(PREFIXO_OCULTO_CEM.Length);
            g.SetActive(true);
        }
        ClimaDosBiomas clima = Object.FindAnyObjectByType<ClimaDosBiomas>();
        if (clima != null)
        {
            SerializedObject so = new SerializedObject(clima);
            SerializedProperty arr = so.FindProperty("biomas");
            for (int i = arr.arraySize - 1; i >= 0; i--)
                if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("nome").stringValue == NOME_BIOMA_CEM) arr.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        if (terreno != null) EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        if (perguntar) EditorUtility.DisplayDialog("Pinglu", "Cemitério antigo de volta.\nA horda foi removida: rode o Pinglu > 11 para criá-la de novo.\nSalve com Ctrl + S.", "OK");
    }

    // esconde (sem apagar) a natureza gerada que ficou dentro da floresta nova
    static void OcultarObjetosNoRaio(Vector3 c, float raio)
    {
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
                if (mundo && pasta.name != "Natureza") continue;
                if (t.GetComponent<Renderer>() == null && t.GetComponent<Light>() == null) continue;
                if (Plano(t.position - c).magnitude > raio || PertoDeZonaProtegida(t.position, 0f) || TemPortalNoNome(t)) continue;
                t.gameObject.name = PREFIXO_OCULTO_CEM + t.gameObject.name;
                t.gameObject.SetActive(false);
            }
        }
    }

    // ==================================================================
    //  ONDE FICA O CEMITÉRIO: longe do início, plano e longe de outros lugares
    // ==================================================================
    static bool EscolherLugarDoCemiterio(Vector3 spawn, Vector3 reserva, out Vector3 escolhido)
    {
        escolhido = reserva;
        Vector3 o = terreno.transform.position, s = dados.size;
        float[] margens = { 70f, 50f };   // bem no fundo do mapa, mas com o cemitério INTEIRO dentro do terreno
        foreach (float margem in margens)
        {
            if (s.x < margem * 2f + 60f || s.z < margem * 2f + 60f) continue;
            float melhor = float.MaxValue; bool achou = false;
            for (float x = o.x + margem; x <= o.x + s.x - margem; x += 30f)
                for (float z = o.z + margem; z <= o.z + s.z - margem; z += 30f)
                {
                    Vector3 c = new Vector3(x, 0f, z);
                    // o retângulo murado (e a floresta em volta dele) tem de caber inteiro no terreno
                    Vector3 paraIni = Plano(spawn - c);
                    Quaternion rotTeste = paraIni.sqrMagnitude > 1f ? Quaternion.LookRotation(paraIni.normalized, Vector3.up) : Quaternion.identity;
                    bool cabe = true;
                    foreach (int sx in new[] { -1, 1 })
                        foreach (int sz in new[] { -1, 1 })
                            if (!DentroDoTerreno(c + rotTeste * new Vector3(sx * (CEM_L * 0.5f + 45f), 0f, sz * (CEM_P * 0.5f + 45f)), 5f)) cabe = false;
                    if (!cabe) continue;
                    bool ruim = false;
                    foreach (Zona zn in zonas) if (Plano(c - zn.pos).magnitude < zn.raio + 80f) { ruim = true; break; }
                    if (ruim) continue;
                    float min = float.MaxValue, max = float.MinValue;
                    for (float dx = -135f; dx <= 135f; dx += 27f)
                        for (float dz = -135f; dz <= 135f; dz += 27f)
                        {
                            float h = Altura(c.x + dx, c.z + dz);
                            min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                        }
                    float dist = Plano(c - spawn).magnitude;
                    if (max - min > 34f) continue;   // terreno muito irregular (a área é grande, o menu aplaina o resto)
                    float nota = (max - min) * 0.8f - Mathf.Min(dist, 1300f) * 0.12f;   // quanto mais longe do início, melhor
                    if (nota < melhor) { melhor = nota; escolhido = new Vector3(x, Altura(x, z), z); achou = true; }
                }
            if (achou)
            {
                avisos.Add("Cemitério a " + Mathf.RoundToInt(Plano(escolhido - spawn).magnitude) + " m de onde o jogador nasce.");
                return true;
            }
        }
        return false;
    }

    // ==================================================================
    //  ÁRVORES SINISTRAS (6 tipos, cada um com versão simples para longe)
    // ==================================================================
    static Mesh[] malhaSinistra = new Mesh[12]; static Material[][] matsSinistra = new Material[12][];
    static float[] raioTroncoSinistra = new float[12];
    static int segsSinistra;

    static void ArvoreSinistra(Malha m, int variante)
    {
        System.Random r = new System.Random(900 + variante * 37);
        bool simples = variante >= 6;
        int tipo = variante % 6;
        segsSinistra = 0;
        float comp, raio, entorta, afina, pRamo, fatorRamo, anguloMin, anguloMax, subida; int maxNivel;
        switch (tipo)
        {
            case 0: comp = 10f; raio = 0.30f; entorta = 0.30f; afina = 0.22f; pRamo = 0.55f; fatorRamo = 0.5f; anguloMin = 30f; anguloMax = 65f; subida = 0.1f; maxNivel = 3; break;
            case 1: comp = 6f; raio = 0.55f; entorta = 0.38f; afina = 0.4f; pRamo = 0.8f; fatorRamo = 0.75f; anguloMin = 50f; anguloMax = 95f; subida = -0.05f; maxNivel = 3; break;
            case 2: comp = 6.5f; raio = 0.45f; entorta = 0.2f; afina = 0.75f; pRamo = 0.3f; fatorRamo = 0.5f; anguloMin = 40f; anguloMax = 75f; subida = 0f; maxNivel = 2; break;
            case 3: comp = 12f; raio = 0.22f; entorta = 0.12f; afina = 0.15f; pRamo = 0.9f; fatorRamo = 0.22f; anguloMin = 70f; anguloMax = 100f; subida = -0.2f; maxNivel = 1; break;
            case 4: comp = 7f; raio = 0.34f; entorta = 0.3f; afina = 0.3f; pRamo = 0.6f; fatorRamo = 0.55f; anguloMin = 35f; anguloMax = 75f; subida = 0.05f; maxNivel = 3; break;
            default: comp = 8.5f; raio = 0.85f; entorta = 0.22f; afina = 0.45f; pRamo = 0.7f; fatorRamo = 0.6f; anguloMin = 40f; anguloMax = 85f; subida = 0f; maxNivel = 3; break;
        }
        if (simples) { maxNivel = Mathf.Max(1, maxNivel - 1); pRamo *= 0.6f; }
        raioTroncoSinistra[variante] = Mathf.Clamp(raio * 2.6f, 0.6f, 1.5f);   // cobre as raízes grossas na base (o colisor segue a escala da árvore)

        if (tipo == 4)
            for (int s = -1; s <= 1; s += 2)
                RamoSinistro(m, r, new Vector3(s * 0.25f, 0f, 0f), Quaternion.Euler(0f, 0f, -s * 14f) * Vector3.up, raio, comp, 0, maxNivel, entorta, afina, pRamo, fatorRamo, anguloMin, anguloMax, subida);
        else
            RamoSinistro(m, r, Vector3.zero, Vector3.up, raio, comp, 0, maxNivel, entorta, afina, pRamo, fatorRamo, anguloMin, anguloMax, subida);

        // raízes grossas aparecendo na base
        int raizes = simples ? 3 : (tipo == 5 ? 7 : 5);
        for (int i = 0; i < raizes; i++)
        {
            float a = i * Mathf.PI * 2f / raizes + (float)r.NextDouble() * 0.5f;
            Vector3 d = new Vector3(Mathf.Cos(a), -0.3f, Mathf.Sin(a)).normalized;
            RamoSinistro(m, r, new Vector3(0f, 0.55f, 0f) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * raio * 0.5f, d, raio * 0.55f, raio * 4.5f, 2, 3, 0.25f, 0.3f, 0f, 0.5f, 30f, 60f, 0f);
        }
    }

    static void RamoSinistro(Malha m, System.Random r, Vector3 p, Vector3 dir, float raio, float comprimento, int nivel, int maxNivel,
                             float entorta, float afina, float pRamo, float fatorRamo, float anguloMin, float anguloMax, float subida)
    {
        if (segsSinistra > 260 || raio < 0.018f || comprimento < 0.25f) return;
        System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        int lados = nivel == 0 ? 7 : (nivel == 1 ? 5 : 4);
        int segs = Mathf.Max(2, Mathf.RoundToInt(comprimento / (nivel == 0 ? 1.5f : 0.85f)));
        float segLen = comprimento / segs;
        Vector3 pos = p, d = dir.normalized;
        for (int i = 0; i < segs; i++)
        {
            float t0 = (float)i / segs, t1 = (float)(i + 1) / segs;
            float r0 = Mathf.Lerp(raio, raio * afina, t0), r1 = Mathf.Lerp(raio, raio * afina, t1);
            Vector3 nd = (d + new Vector3(F(-entorta, entorta), F(-entorta * 0.4f, entorta * 0.5f) + subida * 0.1f, F(-entorta, entorta))).normalized;
            m.Torno(pos, new[] { r0, r1 }, new[] { 0f, segLen * 1.05f }, lados, CEM_CASCA, Quaternion.FromToRotation(Vector3.up, nd), false);
            segsSinistra++;
            pos += nd * segLen; d = nd;
            if (nivel < maxNivel && i >= (nivel == 0 ? 1 : 0) && i < segs && r.NextDouble() < pRamo)
            {
                Vector3 aleat = new Vector3(F(-1f, 1f), F(-0.3f, 0.5f), F(-1f, 1f));
                Vector3 eixo = Vector3.Cross(d, aleat);
                if (eixo.sqrMagnitude > 0.0001f)
                {
                    Vector3 filho = Quaternion.AngleAxis(F(anguloMin, anguloMax), eixo.normalized) * d;
                    filho.y += subida;
                    RamoSinistro(m, r, pos, filho, r1 * F(0.55f, 0.8f), comprimento * fatorRamo * F(0.7f, 1.05f) * (1f - 0.12f * nivel),
                                 nivel + 1, maxNivel, entorta * 1.15f, afina, pRamo * 0.9f, fatorRamo, anguloMin, anguloMax, subida);
                }
            }
        }
        if (nivel < maxNivel && afina > 0.2f)
        {
            // a ponta ainda se divide em dois galhos secos
            for (int k = -1; k <= 1; k += 2)
                RamoSinistro(m, r, pos, Quaternion.AngleAxis(k * F(20f, 40f), Vector3.Cross(d, Vector3.right).sqrMagnitude > 0.01f ? Vector3.Cross(d, Vector3.right) : Vector3.forward) * d,
                             raio * afina * 0.7f, comprimento * 0.35f, nivel + 1, maxNivel, entorta, afina, pRamo * 0.5f, fatorRamo, anguloMin, anguloMax, subida);
        }
    }

    static void ColocarArvoreSinistra(Transform pai, Vector3 p, float escala, int variante)
    {
        Malha m = null;
        if (malhaSinistra[variante] == null) { m = new Malha(); ArvoreSinistra(m, variante); }
        GameObject g = Instanciar(m, "Arvore Sinistra", NoChao(p) - Vector3.up * 0.3f, Quaternion.Euler(Faixa(-3f, 3f), Faixa(0f, 360f), Faixa(-3f, 3f)), pai, false, ref malhaSinistra[variante], ref matsSinistra[variante]);
        g.transform.localScale = Vector3.one * escala;
        CapsuleCollider cc = g.AddComponent<CapsuleCollider>();
        cc.radius = raioTroncoSinistra[variante]; cc.height = 6f; cc.center = Vector3.up * 3f;
    }

    // ==================================================================
    //  FLORESTA ASSOMBRADA
    // ==================================================================
    static void PlantarFlorestaAssombrada()
    {
        { string zl = ""; foreach (Zona zz in zonas) zl += "(" + Mathf.RoundToInt(zz.pos.x) + "," + Mathf.RoundToInt(zz.pos.z) + " r" + Mathf.RoundToInt(zz.raio) + ") "; avisos.Add("Zonas protegidas: " + zl); }
        Transform floresta = Pasta("Floresta Assombrada");
        Transform detalhes = Pasta("Detalhes da Floresta");
        malhaSinistra = new Mesh[12]; matsSinistra = new Material[12][]; raioTroncoSinistra = new float[12];
        Vector3 frente = cemRot * Vector3.forward;
        Vector3 portao = cemC + frente * (CEM_P * 0.5f);
        Forma corredor = Segmento(portao, portao + frente * CEM_RAIO_FRENTE, 0f, 0);

        const float CELULA = 8f;
        Dictionary<long, List<Vector3>> grade = new Dictionary<long, List<Vector3>>();
        System.Func<int, int, long> chave = (gx, gz) => ((long)gx << 32) ^ (uint)gz;
        System.Func<Vector3, float, bool> temVizinho = (pt, espaco) =>
        {
            int cx = Mathf.FloorToInt(pt.x / CELULA), cz = Mathf.FloorToInt(pt.z / CELULA);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    List<Vector3> l;
                    if (!grade.TryGetValue(chave(cx + dx, cz + dz), out l)) continue;
                    foreach (Vector3 o in l) if (Plano(o - pt).magnitude < espaco) return true;
                }
            return false;
        };

        int arvores = 0;
        for (int tentativa = 0; tentativa < 45000 && arvores < 3200; tentativa++)
        {
            Vector3 p; float dist;
            if (tentativa % 2 == 0)
            {   // círculo em volta do cemitério
                dist = Mathf.Lerp(40f, CEM_RAIO_FLORESTA, Mathf.Sqrt((float)rng.NextDouble()));
                float ang = Faixa(0f, Mathf.PI * 2f);
                p = cemC + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
            }
            else
            {   // faixa ao longo da trilha
                float ao = Faixa(30f, CEM_RAIO_FRENTE), lat = Faixa(-CEM_LARGURA_CAMINHO, CEM_LARGURA_CAMINHO);
                p = cemC + frente * ao + (cemRot * Vector3.right) * lat;
                dist = Plano(p - cemC).magnitude;
            }
            if (!DentroDoTerreno(p, 6f)) continue;
            if (DentroDoCemiterio(p, 9f)) continue;
            if (rng.NextDouble() > CemDensidade(p)) continue;
            if (!LivreDeZonas(p)) continue;
            if (Distancia(corredor, p) < 4.5f) continue;
            if (temVizinho(p, dist > 230f ? 6.5f : 5.2f)) continue;
            long k = chave(Mathf.FloorToInt(p.x / CELULA), Mathf.FloorToInt(p.z / CELULA));
            if (!grade.ContainsKey(k)) grade[k] = new List<Vector3>();
            grade[k].Add(p);

            bool longe = dist > 135f;
            int tipo = dist < 80f ? (rng.NextDouble() < 0.45 ? 5 : rng.Next(0, 6)) : rng.Next(0, 6);
            ColocarArvoreSinistra(floresta, p, dist < 80f ? Faixa(1.4f, 2.6f) : Faixa(1.0f, 2.2f), tipo + (longe ? 6 : 0));
            arvores++;
        }

        // detalhes do chão com modelos reais: troncos caídos, tocos, raízes, pedras com musgo, galhos secos
        string[] troncos = { "dead_tree_trunk_02", "dead_tree_trunk" };
        string[] tocos = { "tree_stump_01", "tree_stump_02" };
        string[] pedras = { "rock_moss_set_01", "rock_moss_set_02" };
        int[] cota = { 0, 0, 0, 24, 0 };   // só pedras com musgo (7 mil triângulos): troncos, tocos, raízes e galhos escaneados têm até 225 mil e travavam o jogo
        int[] feitos = new int[5];
        for (int tentativa = 0; tentativa < 4000; tentativa++)
        {
            int tipo = tentativa % 5;
            if (feitos[tipo] >= cota[tipo]) continue;
            if (feitos[3] >= cota[3]) break;
            Vector3 p = PontoNaFlorestaCem(); if (p == Vector3.zero) continue;
            Vector3 loc = Quaternion.Inverse(cemRot) * (p - cemC);
            float yaw = Faixa(0f, 360f);
            GameObject g = null;
            switch (tipo)
            {
                case 0: g = CemModelo(troncos[rng.Next(0, 2)], detalhes, loc, yaw, Faixa(2.5f, 4.5f), false, true); break;
                case 1: g = CemModelo(tocos[rng.Next(0, 2)], detalhes, loc, yaw, Faixa(1.0f, 1.8f), false, true); break;
                case 2: g = CemModelo("root_cluster_01", detalhes, loc, yaw, Faixa(2.2f, 4f), false, false); break;
                case 3: g = CemModelo(pedras[rng.Next(0, 2)], detalhes, loc, yaw, Faixa(1.6f, 3.4f), false, true); break;
                default: g = CemModelo("dry_branches_medium_01", detalhes, loc, yaw, Faixa(1.0f, 1.6f), false, false); break;
            }
            if (g == null) { feitos[tipo] = cota[tipo]; continue; }
            feitos[tipo]++;
        }
        avisos.Add("Floresta: " + arvores + " árvores sinistras, " + feitos[0] + " troncos, " + feitos[1] + " tocos, " + feitos[2] + " raízes, " + feitos[3] + " pedras, " + feitos[4] + " galhos.");
    }

    // ==================================================================
    //  FLORESTA DE FUNDO: árvores do TERRENO (instanciadas, com LOD e corte por distância),
    //  baratas para milhares de árvores. Enche o setor do mapa entre o cemitério e o resto do mundo.
    // ==================================================================
    const string PASTA_ARVORES_TERRENO = PASTA_CEM + "/ArvoresTerreno";

    static TreePrototype[] CriarPrototiposDeArvores()
    {
        if (AssetDatabase.IsValidFolder(PASTA_ARVORES_TERRENO)) AssetDatabase.DeleteAsset(PASTA_ARVORES_TERRENO);
        AssetDatabase.CreateFolder(PASTA_CEM, "ArvoresTerreno");
        TreePrototype[] protos = new TreePrototype[6];
        for (int v = 0; v < 6; v++)
        {
            Mesh[] ms = new Mesh[2]; Material[][] mats2 = new Material[2][];
            for (int k = 0; k < 2; k++)
            {
                Malha m = new Malha();
                ArvoreSinistra(m, v + 6 * k);
                ms[k] = m.Gerar("ArvoreSinistra_" + v + (k == 0 ? "_LOD0" : "_LOD1"));
                mats2[k] = m.Materiais(mats);
                AssetDatabase.CreateAsset(ms[k], PASTA_ARVORES_TERRENO + "/" + ms[k].name + ".asset");
            }
            GameObject raizArv = new GameObject("ArvoreSinistra_" + v);
            Renderer[] rs = new Renderer[2];
            for (int k = 0; k < 2; k++)
            {
                GameObject filho = new GameObject("LOD" + k);
                filho.transform.SetParent(raizArv.transform, false);
                filho.AddComponent<MeshFilter>().sharedMesh = ms[k];
                MeshRenderer mr = filho.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats2[k];
                if (k == 1) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rs[k] = mr;
            }
            LODGroup lg = raizArv.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.10f, new[] { rs[0] }), new LOD(0.045f, new[] { rs[1] }) });   // abaixo de 4,5% da tela: some
            lg.RecalculateBounds();
            CapsuleCollider cc = raizArv.AddComponent<CapsuleCollider>();
            cc.radius = raioTroncoSinistra[v]; cc.height = 6f; cc.center = Vector3.up * 3f;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raizArv, PASTA_ARVORES_TERRENO + "/ArvoreSinistra_" + v + ".prefab");
            Object.DestroyImmediate(raizArv);
            protos[v] = new TreePrototype { prefab = prefab };
        }
        return protos;
    }

    static void PlantarFlorestaDeFundo()
    {
        Vector3 frente = cemRot * Vector3.forward;
        Vector3 portao = cemC + frente * (CEM_P * 0.5f);
        Forma corredor = Segmento(portao, portao + frente * CEM_RAIO_FRENTE, 0f, 0);

        // registra os 6 tipos de árvore no terreno
        TreePrototype[] novos = CriarPrototiposDeArvores();
        List<TreePrototype> lista = new List<TreePrototype>(dados.treePrototypes);
        for (int i = lista.Count - 1; i >= 0; i--) if (lista[i].prefab == null || lista[i].prefab.name.StartsWith("ArvoreSinistra_")) lista.RemoveAt(i);
        int primeiro = lista.Count;
        lista.AddRange(novos);
        // as árvores existentes guardam o índice do tipo: se removemos algum prototype, é preciso reindexar
        TreeInstance[] existentes = dados.treeInstances;
        dados.treePrototypes = lista.ToArray();

        List<TreeInstance> todas = new List<TreeInstance>(existentes);
        Vector3 o = terreno.transform.position, tam = dados.size;
        const float PASSO = 6.5f;
        int criadas = 0;
        for (float x = o.x + 4f; x < o.x + tam.x - 4f; x += PASSO)
            for (float z = o.z + 4f; z < o.z + tam.z - 4f; z += PASSO)
            {
                Vector3 p = new Vector3(x + Faixa(-2.8f, 2.8f), 0f, z + Faixa(-2.8f, 2.8f));
                Vector3 d = p - cemC; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 120f || dist > CEM_RAIO_FUNDO) continue;
                if (Vector3.Angle(d, frente) > CEM_ANGULO_FUNDO) continue;
                if (ForaDoLimiteDaFloresta(p)) continue;
                if (CemDensidade(p) > 0f || DentroDoCemiterio(p, 9f)) continue;   // aí já tem as árvores grandes (ou é o cemitério)
                float prob = Mathf.Lerp(1f, 0.2f, Mathf.InverseLerp(CEM_RAIO_FUNDO * 0.55f, CEM_RAIO_FUNDO, dist));
                if (rng.NextDouble() > prob) continue;
                if (!DentroDoTerreno(p, 4f) || !LivreDeZonas(p)) continue;
                if (Distancia(corredor, p) < 5f) continue;
                float esc = Faixa(1.0f, 2.1f);
                todas.Add(new TreeInstance
                {
                    position = new Vector3((p.x - o.x) / tam.x, 0f, (p.z - o.z) / tam.z),
                    prototypeIndex = primeiro + rng.Next(0, 6),
                    widthScale = esc, heightScale = esc * Faixa(0.9f, 1.15f),
                    rotation = Faixa(0f, Mathf.PI * 2f),
                    color = Color.white, lightmapColor = Color.white
                });
                criadas++;
            }
        dados.SetTreeInstances(todas.ToArray(), true);
        terreno.treeMaximumFullLODCount = 400;
        avisos.Add("Floresta de fundo: " + criadas + " árvores do terreno (setor de " + Mathf.RoundToInt(CEM_RAIO_FUNDO) + " m).");
    }

    // ponto dentro (ou perto) do retângulo murado do cemitério? (as árvores da floresta ficam do lado de fora)
    static bool DentroDoCemiterio(Vector3 p, float margem)
    {
        Vector3 l = Quaternion.Inverse(cemRot) * (p - cemC);
        return Mathf.Abs(l.x) < CEM_L * 0.5f + margem && Mathf.Abs(l.z) < CEM_P * 0.5f + margem;
    }

    static bool LivreDeZonas(Vector3 p)
    {
        foreach (Zona z in zonas) if (Plano(p - z.pos).magnitude < z.raio) return false;
        return true;
    }

    static Vector3 PontoNaFlorestaCem()
    {
        Vector3 frente0 = cemRot * Vector3.forward;
        Vector3 p;
        if (rng.NextDouble() < 0.5)
        {
            float dist = Mathf.Lerp(38f, CEM_RAIO_FLORESTA, Mathf.Sqrt((float)rng.NextDouble()));
            float ang = Faixa(0f, Mathf.PI * 2f);
            p = cemC + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
        }
        else p = cemC + frente0 * Faixa(40f, CEM_RAIO_FRENTE) + (cemRot * Vector3.right) * Faixa(-CEM_LARGURA_CAMINHO, CEM_LARGURA_CAMINHO);
        if (CemDensidade(p) <= 0f || DentroDoCemiterio(p, 10f)) return Vector3.zero;
        Vector3 frente = cemRot * Vector3.forward;
        Vector3 portao = cemC + frente * (CEM_P * 0.5f);
        if (!DentroDoTerreno(p, 8f) || !LivreDeZonas(p)) return Vector3.zero;
        if (Distancia(Segmento(portao, portao + frente * CEM_RAIO_FRENTE, 0f, 0), p) < 3f) return Vector3.zero;
        return p;
    }

    // ==================================================================
    //  NEBLINA E CLIMA
    // ==================================================================
    static Material MaterialDaNevoa()
    {
        string tex = PASTA_CEM + "/nevoa_suave.png";
        if (!File.Exists(tex))
        {
            int n = 128;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f).magnitude * 2f;
                    float ruido = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.07f + 3f, y * 0.07f + 9f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f) * ruido;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            File.WriteAllBytes(tex, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(tex);
        }
        TextureImporter ti = AssetImporter.GetAtPath(tex) as TextureImporter;
        if (ti != null && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp))
        { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }

        string caminho = PASTA_CEM + "/Nevoa.mat";
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, caminho); }
        else if (sh != null) m.shader = sh;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void CriarNevoaDoCemiterio()
    {
        Material mat = MaterialDaNevoa();
        GameObject pai = new GameObject("Nevoa do Cemiterio");
        pai.transform.position = cemC;
        float[] caixa = { 100f, 380f };
        float[] altura = { 1.2f, 2.5f };
        float[] taxa = { 3.5f, 5f };
        float[] vida = { 22f, 34f };
        float[] tam = { 14f, 28f };
        int[] maximo = { 150, 300 };
        float[] alfa = { 0.17f, 0.13f };
        for (int k = 0; k < 2; k++)
        {
            GameObject go = new GameObject(k == 0 ? "Nevoa Rasteira" : "Nevoa da Floresta");
            go.transform.SetParent(pai.transform, false);
            go.transform.localPosition = Vector3.up * (k == 0 ? 0.7f : 1.4f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(vida[k] * 0.7f, vida[k]);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(tam[k] * 0.7f, tam[k]);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.6f, 0.66f, alfa[k]), new Color(0.62f, 0.68f, 0.7f, alfa[k] * 0.7f));
            main.maxParticles = maximo[k];
            var em = ps.emission; em.rateOverTime = taxa[k];
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(caixa[k], altura[k], caixa[k]);
            var col = ps.colorOverLifetime; col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var ruido = ps.noise; ruido.enabled = true; ruido.strength = 0.5f; ruido.frequency = 0.15f;
            ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }
    }

    static void AdicionarBiomaDoCemiterio()
    {
        ClimaDosBiomas clima = Object.FindAnyObjectByType<ClimaDosBiomas>();
        if (clima == null) { avisos.Add("Não achei o ClimaDosBiomas (Pinglu > 20): a neblina forte da floresta não foi ligada."); return; }
        SerializedObject so = new SerializedObject(clima);
        SerializedProperty arr = so.FindProperty("biomas");
        for (int i = arr.arraySize - 1; i >= 0; i--)
            if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("nome").stringValue == NOME_BIOMA_CEM) arr.DeleteArrayElementAtIndex(i);
        // um círculo no cemitério e uma grade de círculos pelo setor da floresta (a neblina forte acompanha as árvores)
        List<Vector4> circulos = new List<Vector4>();   // x, y, z, raio
        circulos.Add(new Vector4(cemC.x, cemC.y, cemC.z, 300f));
        Vector3 f0 = cemRot * Vector3.forward;
        foreach (float r in new[] { 260f, 520f, 760f })
            foreach (float ang in new[] { -70f, -35f, 0f, 35f, 70f })
            {
                Vector3 c = cemC + Quaternion.Euler(0f, ang, 0f) * f0 * r;
                if (!DentroDoTerreno(c, -150f)) continue;
                circulos.Add(new Vector4(c.x, c.y, c.z, 240f));
            }
        foreach (Vector4 c in circulos)
        {
            arr.arraySize++;
            SerializedProperty b = arr.GetArrayElementAtIndex(arr.arraySize - 1);
            b.FindPropertyRelative("nome").stringValue = NOME_BIOMA_CEM;
            b.FindPropertyRelative("centro").vector3Value = new Vector3(c.x, c.y, c.z);
            b.FindPropertyRelative("raio").floatValue = c.w;
            b.FindPropertyRelative("corDaNeblina").colorValue = new Color(0.30f, 0.42f, 0.46f, 1f);
            b.FindPropertyRelative("forcaDaNeblina").floatValue = 3.0f;
            b.FindPropertyRelative("vento").floatValue = 0.3f;
            b.FindPropertyRelative("efeito").enumValueIndex = (int)ClimaDosBiomas.Efeito.Esporos;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ==================================================================
    //  MONSTROS E HORDAS
    // ==================================================================
    struct DefMonstro
    {
        public string nome; public float escala, velocidade, vida, dano, intervalo, alcance, distAtaque; public Color tom;
        public int aPartirDaOnda, peso, custo; public bool chefe;
    }

    static GameObject[] CriarVariantesDeMonstros()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_ESQUELETO);
        if (basePrefab == null) { avisos.Add("Não achei o prefab do esqueleto (Pinglu > 11): só o esqueleto comum será usado."); return new GameObject[0]; }
        if (!AssetDatabase.IsValidFolder(PASTA_VARIANTES)) AssetDatabase.CreateFolder("Assets/Personagens/Inimigos/Esqueleto", "Variantes");

        DefMonstro[] defs = {
            new DefMonstro { nome = "Carniceiro", escala = 0.92f, velocidade = 3.7f, vida = 35f, dano = 8f, intervalo = 0.9f, alcance = 2.3f, distAtaque = 1.8f, tom = new Color(1f, 0.62f, 0.55f), aPartirDaOnda = 2, peso = 6, custo = 1 },
            new DefMonstro { nome = "Guardiao Amaldicoado", escala = 1.2f, velocidade = 2.2f, vida = 120f, dano = 20f, intervalo = 1.5f, alcance = 2.8f, distAtaque = 2.2f, tom = new Color(0.55f, 0.85f, 1.25f), aPartirDaOnda = 3, peso = 4, custo = 3 },
            new DefMonstro { nome = "Brutamontes", escala = 1.65f, velocidade = 1.3f, vida = 260f, dano = 32f, intervalo = 2.3f, alcance = 3.4f, distAtaque = 2.9f, tom = new Color(0.55f, 0.58f, 0.62f), aPartirDaOnda = 4, peso = 3, custo = 6 },
            new DefMonstro { nome = "Rei dos Ossos", escala = 2.3f, velocidade = 1.7f, vida = 800f, dano = 42f, intervalo = 2.0f, alcance = 4.2f, distAtaque = 3.6f, tom = new Color(1.35f, 0.5f, 0.35f), aPartirDaOnda = 5, peso = 1, custo = 25, chefe = true },
        };

        List<GameObject> lista = new List<GameObject>();
        lista.Add(basePrefab);
        foreach (DefMonstro d in defs)
        {
            string caminho = PASTA_VARIANTES + "/" + d.nome + ".prefab";
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            inst.transform.localScale = Vector3.one * d.escala;
            InimigoEsqueleto ini = inst.GetComponent<InimigoEsqueleto>();
            SerializedObject so = new SerializedObject(ini);
            DefinirFloat(so, "velocidade", d.velocidade);
            DefinirFloat(so, "vidaMaxima", d.vida);
            DefinirFloat(so, "dano", d.dano);
            DefinirFloat(so, "intervaloEntreAtaques", d.intervalo);
            DefinirFloat(so, "alcanceDoGolpe", d.alcance);
            DefinirFloat(so, "distanciaParaAtacar", d.distAtaque);
            SerializedProperty tom = so.FindProperty("tom");
            if (tom != null) tom.colorValue = d.tom;
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject salvo = PrefabUtility.SaveAsPrefabAsset(inst, caminho);
            Object.DestroyImmediate(inst);
            lista.Add(salvo);
        }
        RegistrarNaRede(lista);
        return lista.ToArray();
    }

    static void DefinirFloat(SerializedObject so, string nome, float valor)
    {
        SerializedProperty p = so.FindProperty(nome);
        if (p != null) p.floatValue = valor; else avisos.Add("Campo '" + nome + "' não achado no esqueleto.");
    }

    // multiplayer: as variações precisam estar na lista de prefabs de rede
    static void RegistrarNaRede(List<GameObject> prefabs)
    {
#if PINGLU_REDE
        Unity.Netcode.NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<Unity.Netcode.NetworkPrefabsList>(LISTA_REDE_CEM);
        if (lista == null) { avisos.Add("Lista de prefabs de rede não achada (rode o Pinglu > 15): os novos monstros não aparecerão no multiplayer."); return; }
        foreach (GameObject p in prefabs)
        {
            if (p == null || p.GetComponent<Unity.Netcode.NetworkObject>() == null) continue;
            bool tem = false;
            foreach (Unity.Netcode.NetworkPrefab np in lista.PrefabList) if (np.Prefab == p) tem = true;
            if (!tem) lista.Add(new Unity.Netcode.NetworkPrefab { Prefab = p });
        }
        EditorUtility.SetDirty(lista);
        AssetDatabase.SaveAssets();
        PingluCodigosDeRede.Consertar();
#endif
    }

    static void CriarHordaGrande(GameObject[] prefabs)
    {
        GameObject antiga = AcharInativo("Horda do Cemiterio");
        if (antiga != null) Undo.DestroyObjectImmediate(antiga);
        GameObject antigaEmb = AcharInativo("Emboscada da Floresta");
        if (antigaEmb != null) Undo.DestroyObjectImmediate(antigaEmb);
        if (prefabs.Length == 0) return;

        GameObject g = new GameObject("Horda do Cemiterio");
        Undo.RegisterCreatedObjectUndo(g, "Horda do cemitério");
        g.transform.SetPositionAndRotation(cemC, cemRot);
        GerenciadorDeOndas ger = g.AddComponent<GerenciadorDeOndas>();

        // pontos de surgimento: em anéis em volta da arena, dentro dos muros e longe dos mausoléus
        List<Transform> pontos = new List<Transform>();
        List<Vector3> candidatos = new List<Vector3>();
        float hx = CEM_L * 0.5f - 9f, hz = CEM_P * 0.5f - 9f;
        foreach (float raioAnel in new[] { CEM_PR + 14f, CEM_PR + 30f, CEM_PR + 46f })
        {
            int nAnel = Mathf.RoundToInt(raioAnel * 0.35f);
            for (int i = 0; i < nAnel; i++)
            {
                float ang = i * Mathf.PI * 2f / nAnel;
                Vector3 q = new Vector3(Mathf.Cos(ang) * raioAnel, 0f, Mathf.Sin(ang) * raioAnel);
                if (Mathf.Abs(q.x) > hx || Mathf.Abs(q.z) > hz) continue;
                if (Mathf.Abs(q.x) < 7f && q.z > 0f) continue;       // caminho do portão
                bool perto = false;
                foreach (Vector4 o in cemOcupados) if (new Vector2(q.x - o.x, q.z - o.y).magnitude < o.w + 2f) { perto = true; break; }
                if (!perto) candidatos.Add(q);
            }
        }
        int passo = Mathf.Max(1, candidatos.Count / 40);
        for (int i = 0; i < candidatos.Count; i += passo)
        {
            GameObject p = new GameObject("Ponto de Surgimento");
            p.transform.SetParent(g.transform, false);
            p.transform.localPosition = candidatos[i];
            pontos.Add(p.transform);
        }
        GameObject chegada = new GameObject("Chegada (F2)");
        chegada.transform.SetParent(g.transform, false);
        // F2 leva à entrada da floresta, no fim da trilha que vai reto até o portão do cemitério
        chegada.transform.localPosition = new Vector3(0f, 0f, CEM_RAIO_FRENTE - 25f);
        chegada.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        string[] nomes = { "Esqueleto", "Carniceiro", "Guardião Amaldiçoado", "Brutamontes", "Rei dos Ossos" };
        int[] ondas = { 1, 2, 3, 4, 5 }, pesos = { 10, 6, 4, 3, 1 }, custos = { 1, 1, 3, 6, 25 };

        SerializedObject so = new SerializedObject(ger);
        so.FindProperty("prefabInimigo").objectReferenceValue = prefabs[0];
        so.FindProperty("pontoDeChegada").objectReferenceValue = chegada.transform;
        so.FindProperty("distanciaParaComecar").floatValue = 80f;
        so.FindProperty("raioDosMonstros").floatValue = 175f;
        SerializedProperty arr = so.FindProperty("pontosDeSurgimento");
        arr.arraySize = pontos.Count;
        for (int i = 0; i < pontos.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = pontos[i];
        SerializedProperty tipos = so.FindProperty("tipos");
        int n = Mathf.Min(prefabs.Length, nomes.Length);
        tipos.arraySize = n;
        for (int i = 0; i < n; i++)
        {
            SerializedProperty e = tipos.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("nome").stringValue = nomes[i];
            e.FindPropertyRelative("prefab").objectReferenceValue = prefabs[i];
            e.FindPropertyRelative("aPartirDaOnda").intValue = ondas[i];
            e.FindPropertyRelative("peso").intValue = pesos[i];
            e.FindPropertyRelative("custo").intValue = custos[i];
            e.FindPropertyRelative("chefe").boolValue = i == nomes.Length - 1;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // emboscada da floresta (esqueleto comum, às vezes Carniceiro ou Guardião)
        GameObject emb = new GameObject("Emboscada da Floresta");
        Undo.RegisterCreatedObjectUndo(emb, "Emboscada da floresta");
        emb.transform.position = cemC;
        EmboscadaDaFloresta ec = emb.AddComponent<EmboscadaDaFloresta>();
        SerializedObject se = new SerializedObject(ec);
        SerializedProperty lp = se.FindProperty("prefabs");
        int quantos = Mathf.Min(3, prefabs.Length);
        lp.arraySize = quantos;
        for (int i = 0; i < quantos; i++) lp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        se.FindProperty("raioInterno").floatValue = 140f;   // a muralha tem ~128 m de meia diagonal
        se.FindProperty("raioExterno").floatValue = CEM_RAIO_FRENTE;
        se.FindProperty("raioLados").floatValue = CEM_RAIO_FLORESTA;
        se.FindProperty("larguraDoCaminho").floatValue = CEM_LARGURA_CAMINHO;
        se.FindProperty("direcaoDaFrente").vector3Value = cemRot * Vector3.forward;
        se.FindProperty("raioDoSetor").floatValue = CEM_RAIO_FUNDO;
        se.FindProperty("anguloDoSetor").floatValue = CEM_ANGULO_FUNDO;
        // lugares onde nunca aparecem monstros da floresta (início do mapa, vila, ruínas, acampamentos)
        SerializedProperty zp = se.FindProperty("zonasSemMonstros");
        zp.arraySize = zonas.Count;
        for (int i = 0; i < zonas.Count; i++) zp.GetArrayElementAtIndex(i).vector4Value = new Vector4(zonas[i].pos.x, zonas[i].pos.y, zonas[i].pos.z, zonas[i].raio + 15f);
        se.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(g.scene);
    }
}
#endif
