// =====================================================================
//  PingluTerreno.cs  -  TERRENO NOVO: chão, capim, flores, caminhos e chão da floresta
// ---------------------------------------------------------------------
//  Refaz o visual do terreno do zero (referências: Zelda, Fable...):
//   - texturas novas de chão (ambientCG, licença CC0): campo, campo vivo, caminho de terra, chão de mata, musgo, rocha
//   - capim novo: tufos de lâminas com shader próprio (Pinglu/Capim): raiz escura, ponta clara, manchas de cor, vento
//   - campos floridos: manchas grandes de flores amarelas, brancas, rosas, vermelhas, azuis e lavanda roxa
//   - caminhos: textura nova, bordas irregulares, capim invadindo as beiras
//   - Floresta Sombria: chão escuro de folhas e musgo, só com capim seco e raro
//   - o capim ANTIGO é removido de todo o mapa
//
//  COMO USAR:  Pinglu > 54. Terreno novo   (Play desligado; depois Ctrl+S)
//  Para voltar: Pinglu > 55. Voltar ao terreno antigo
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluTerreno
{
    const string PASTA = "Assets/Cenario/Terreno";
    const string PASTA_TEX = PASTA + "/Texturas";
    const string PASTA_CAMADAS = PASTA + "/Camadas";
    const string PASTA_CAPIM = PASTA + "/Capim";
    public const string CAMADA_CAMINHO = PASTA_CAMADAS + "/Caminho_Soil_Rocks.terrainlayer";   // o nome precisa conter "Soil_Rocks": é como as estradas são reconhecidas
    const string BACKUP = PASTA + "/Backup_Terreno_Antigo.asset";
    const string CHAVE_ATIVO = "PingluTerrenoNovo_Ativo";
    const string CHAVE_CAMINHOS = "PingluTerrenoNovo_CaminhosSuaves";

    // quantidade máxima de tufos por metro quadrado (1 m por célula de detalhe)
    const float TUFOS_POR_M2 = 5f;
    const float DISTANCIA_DETALHES = 90f;

    static Terrain terreno; static TerrainData dados;
    static LimiteDaFloresta limite;
    static System.Random rng;
    static readonly List<string> avisos = new List<string>();

    public static bool Ativo() { return EditorPrefs.GetBool(CHAVE_ATIVO, false) && File.Exists(CAMADA_CAMINHO); }
    public static void CaminhosPrecisamSuavizar() { EditorPrefs.SetBool(CHAVE_CAMINHOS, false); }

    // ------------------------------------------------------------------
    //  MENUS
    // ------------------------------------------------------------------
    [MenuItem("Pinglu/54. Terreno novo")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<Shader>(PASTA + "/Shaders/PingluCapim.shader") == null && Shader.Find("Pinglu/Capim") == null)
        { EditorUtility.DisplayDialog("Pinglu", "O shader do capim ainda não foi importado. Espere o Unity terminar de compilar e rode de novo.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu - Terreno novo",
            "Vou refazer o terreno:\n- chão novo (campo, caminho de terra, chão de mata, musgo, rocha)\n- capim NOVO e denso, com vento; o capim antigo sai\n- campos floridos e folhas no chão\n- caminhos com bordas irregulares\n- chão escuro na Floresta Sombria\n\n" +
            "Guardo um backup do terreno. Pode demorar alguns minutos.", "Pode fazer", "Cancelar")) return;
        Executar(true);
    }

    [MenuItem("Pinglu/55. Voltar ao terreno antigo")]
    public static void Desfazer()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) return;
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP) == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o backup do terreno antigo.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu", "Voltar o terreno ao que era antes do terreno novo? (relevo, capim e chão antigos)", "Sim", "Cancelar")) return;
        EditorPrefs.SetBool(CHAVE_ATIVO, false);
        string caminho = AssetDatabase.GetAssetPath(terreno.terrainData);
        AssetDatabase.DeleteAsset(caminho);
        AssetDatabase.CopyAsset(BACKUP, caminho);
        AssetDatabase.Refresh();
        terreno.terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(caminho);
        terreno.Flush();
        EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        EditorUtility.DisplayDialog("Pinglu", "Terreno antigo de volta. Salve com Ctrl + S.", "OK");
    }

    static void Executar(bool interativo)
    {
        terreno = Terrain.activeTerrain; dados = terreno.terrainData;
        avisos.Clear();
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Backup do terreno...", 0.02f);
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP) == null || !Ativo())
            {
                if (AssetDatabase.LoadAssetAtPath<TerrainData>(BACKUP) != null) AssetDatabase.DeleteAsset(BACKUP);
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(dados), BACKUP)) throw new System.Exception("Não consegui fazer backup do terreno.");
            }
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando texturas e camadas...", 0.08f);
            GarantirCamadas();
            EditorPrefs.SetBool(CHAVE_ATIVO, true);
            PintarChao();
            if (!EditorPrefs.GetBool(CHAVE_CAMINHOS, false)) SuavizarCaminhos();
            PintarDetalhes();
            AjustarTerreno();
            terreno.Flush();
            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console). Use Pinglu > 55 para voltar."); }
        finally { EditorUtility.ClearProgressBar(); }
        foreach (string a in avisos) Debug.LogWarning("[Terreno] " + a);
        if (interativo)
        {
            string msg = "Terreno novo pronto!\n\n" + relatorioChao + "\nSalve com Ctrl + S.\nPara voltar: Pinglu > 55.";
            if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
            EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        }
    }

    // usado quando a vegetação (menu 43) ou as estradas são refeitas: reaplica só o chão e o capim, sem perguntar
    public static void RefazerSilencioso()
    {
        terreno = Terrain.activeTerrain; if (terreno == null) return;
        dados = terreno.terrainData; avisos.Clear();
        try
        {
            GarantirCamadas();
            PintarChao();
            if (!EditorPrefs.GetBool(CHAVE_CAMINHOS, false)) SuavizarCaminhos();
            PintarDetalhes();
            AjustarTerreno();
        }
        finally { EditorUtility.ClearProgressBar(); }      // a barra de progresso ficava parada em 97% depois de terminar
        foreach (string a in avisos) Debug.LogWarning("[Terreno] " + a);
        Debug.Log("[Terreno] chão e capim refeitos.");
    }

    // ------------------------------------------------------------------
    //  CAMADAS DE TEXTURA
    // ------------------------------------------------------------------
    static void ConfigurarTextura(string caminho, bool normal)
    {
        TextureImporter ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (ti == null) return;
        bool mudou = false;
        TextureImporterType tipo = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType != tipo) { ti.textureType = tipo; mudou = true; }
        if (ti.anisoLevel != 8) { ti.anisoLevel = 8; mudou = true; }
        if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; mudou = true; }
        if (!normal && !ti.sRGBTexture) { ti.sRGBTexture = true; mudou = true; }
        if (mudou) ti.SaveAndReimport();
    }

    static TerrainLayer Camada(string nome, string tex, float tile, Vector4 corMax, float normal, float lisura)
    {
        string cor = PASTA_TEX + "/" + tex + "_Cor.jpg", nor = PASTA_TEX + "/" + tex + "_Normal.jpg";
        ConfigurarTextura(cor, false); ConfigurarTextura(nor, true);
        string caminho = PASTA_CAMADAS + "/" + nome + ".terrainlayer";
        TerrainLayer l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(caminho);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, caminho); }
        l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(cor);
        l.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(nor);
        l.maskMapTexture = null;
        l.tileSize = new Vector2(tile, tile);
        l.tileOffset = Vector2.zero;
        l.diffuseRemapMin = Vector4.zero; l.diffuseRemapMax = corMax;
        l.normalScale = normal; l.smoothness = lisura; l.metallic = 0f;
        EditorUtility.SetDirty(l);
        return l;
    }

    static TerrainLayer lCampo, lVivo, lCaminho, lMata, lMataSombria, lMusgoSombrio, lRocha, lSeco, lMorta;

    static void CriarCamadas()
    {
        // o "corMax" multiplica a cor da textura: valores maiores que 1 deixam o verde mais vivo
        lCampo = Camada("Campo", "Campo", 6f, new Vector4(1.35f, 1.9f, 0.62f, 1f), 1.0f, 0.12f);
        lVivo = Camada("CampoVivo", "CampoVivo", 5f, new Vector4(1.25f, 1.85f, 0.7f, 1f), 1.0f, 0.12f);
        lCaminho = Camada("Caminho_Soil_Rocks", "Caminho", 3.5f, new Vector4(1.0f, 0.93f, 0.82f, 1f), 1.1f, 0.08f);
        lMata = Camada("Mata", "Mata", 5f, new Vector4(0.95f, 0.9f, 0.82f, 1f), 1.0f, 0.06f);
        lMataSombria = Camada("MataSombria", "Mata", 4.5f, new Vector4(0.34f, 0.36f, 0.38f, 1f), 1.1f, 0.05f);
        lMusgoSombrio = Camada("MusgoSombrio", "Musgo", 3f, new Vector4(0.36f, 0.58f, 0.34f, 1f), 1.0f, 0.05f);
        lRocha = Camada("Rocha", "Rocha", 8f, new Vector4(0.95f, 0.98f, 0.95f, 1f), 1.2f, 0.1f);
        // chão MORTO: capim seco mostarda (Floresta Sombria, ruínas) e terra cinza rachada (Castelo Destruído)
        lSeco = Camada("CampoSeco", "Campo", 5f, new Vector4(1.05f, 0.82f, 0.22f, 1f), 1.0f, 0.05f);
        lMorta = Camada("TerraMorta", "Morta", 4f, new Vector4(0.62f, 0.58f, 0.54f, 1f), 1.2f, 0.05f);
        AssetDatabase.SaveAssets();
    }

    // troca as camadas antigas pelas novas MANTENDO a posição delas (o que já estava pintado - estradas, praça - continua no lugar)
    static void GarantirCamadas()
    {
        CriarCamadas();
        // texturas do terreno mais nítidas
        if (dados.alphamapResolution < 1024) dados.alphamapResolution = 1024;
        if (dados.baseMapResolution < 1024) dados.baseMapResolution = 1024;
        List<TerrainLayer> lista = new List<TerrainLayer>(dados.terrainLayers);
        for (int i = 0; i < lista.Count; i++)
        {
            if (lista[i] == null) continue;
            string n = lista[i].name;
            if (n.Contains("Soil_Rocks")) lista[i] = lCaminho;
            else if (n == "Grass_A_TerrainLayer" || n == "Grass_A") lista[i] = lCampo;
            else if (n == "Grass_B_TerrainLayer" || n == "Grass_B") lista[i] = lVivo;
            else if (n.StartsWith("Grass_Soil")) lista[i] = lMata;
            else if (n == "Rock" || n.StartsWith("Rock_") || n == "Rock_TerrainLayer") lista[i] = lRocha;
        }
        foreach (TerrainLayer l in new[] { lCampo, lVivo, lCaminho, lMata, lMataSombria, lMusgoSombrio, lRocha, lSeco, lMorta })
            if (!lista.Contains(l)) lista.Add(l);
        dados.terrainLayers = lista.ToArray();
    }

    static int Indice(TerrainLayer l)
    {
        TerrainLayer[] t = dados.terrainLayers;
        for (int i = 0; i < t.Length; i++) if (t[i] == l) return i;
        return -1;
    }

    // ------------------------------------------------------------------
    //  PINTURA DO CHÃO
    // ------------------------------------------------------------------
    static float PesoFora(float x, float z)
    {
        if (limite == null || !limite.Valido) return 1f;
        float d = limite.DistanciaDaBorda(x, z);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, -28f, d));
    }

    // ------------------------------------------------------------------
    //  REGIÕES: cada parte do mapa tem o seu tipo de terreno
    //   sombria = Floresta Sombria (e cemitério)   ruina = Reino Destruído / Castelo   pantano   queimada = Terras Queimadas
    //   vila = terra fértil e feliz em volta da vila   (o resto é campo comum, bonito)
    // ------------------------------------------------------------------
    static float[,] rSombria, rRuina, rPantano, rQueimada, rVila;

    static float CirculoSuave(Vector3 centro, float raio, float x, float z, float transicao)
    {
        float d = Mathf.Sqrt((x - centro.x) * (x - centro.x) + (z - centro.z) * (z - centro.z));
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(raio - transicao * 0.5f, raio + transicao * 0.5f, d));
    }

    static void CalcularRegioes(int res)
    {
        limite = Object.FindFirstObjectByType<LimiteDaFloresta>();
        ClimaDosBiomas clima = Object.FindFirstObjectByType<ClimaDosBiomas>();
        MiniMapa mm = Object.FindFirstObjectByType<MiniMapa>();
        Vector3 vila = Vector3.zero; bool temVila = false; Vector3 castelo = Vector3.zero; bool temCastelo = false; Vector3 cemiterio = Vector3.zero; bool temCem = false;
        if (mm != null)
            foreach (MiniMapa.Marcador k in mm.marcadores)
            {
                if (k.tipo == MiniMapa.Tipo.Vila) { vila = k.posicao; temVila = true; }
                else if (k.tipo == MiniMapa.Tipo.Ruinas) { castelo = k.posicao; temCastelo = true; }
                else if (k.tipo == MiniMapa.Tipo.Cemiterio) { cemiterio = k.posicao; temCem = true; }
            }
        ClimaDosBiomas.Bioma bRuina = null, bPantano = null, bQueimada = null;
        if (clima != null)
            foreach (ClimaDosBiomas.Bioma b in clima.biomas)
            {
                if (b.nome.Contains("Reino") || b.nome.Contains("Colinas")) bRuina = b;
                else if (b.nome.Contains("Pantano")) bPantano = b;
                else if (b.nome.Contains("Queimadas")) bQueimada = b;
            }
        rSombria = new float[res, res]; rRuina = new float[res, res]; rPantano = new float[res, res]; rQueimada = new float[res, res]; rVila = new float[res, res];
        Vector3 o = terreno.transform.position, sz = dados.size;
        for (int z = 0; z < res; z++)
        {
            if (z % 128 == 0) EditorUtility.DisplayProgressBar("Pinglu", "Medindo as regiões do mapa... " + (z * 100 / res) + "%", 0.1f + 0.1f * z / res);
            for (int x = 0; x < res; x++)
            {
                float px = o.x + (x + 0.5f) / res * sz.x, pz = o.z + (z + 0.5f) / res * sz.z;
                float sombria = 1f - PesoFora(px, pz);
                if (temCem) sombria = Mathf.Max(sombria, CirculoSuave(cemiterio, 150f, px, pz, 80f));
                float ruina = 0f;
                if (bRuina != null) ruina = CirculoSuave(bRuina.centro, bRuina.raio * 0.85f, px, pz, 140f);
                if (temCastelo) ruina = Mathf.Max(ruina, CirculoSuave(castelo, 190f, px, pz, 110f));
                float pantano = bPantano != null ? CirculoSuave(bPantano.centro, bPantano.raio * 0.85f, px, pz, 140f) : 0f;
                float queimada = bQueimada != null ? CirculoSuave(bQueimada.centro, bQueimada.raio * 0.85f, px, pz, 140f) : 0f;
                rSombria[z, x] = sombria; rRuina[z, x] = ruina; rPantano[z, x] = pantano; rQueimada[z, x] = queimada;
                rVila[z, x] = temVila ? CirculoSuave(vila, 230f, px, pz, 200f) : 0f;      // terra fértil em volta da vila
            }
        }
    }

    static void PintarChao()
    {
        int res = dados.alphamapResolution;
        LimparPinturaDaVila();
        CalcularRegioes(res);
        TerrainLayer[] camadas = dados.terrainLayers;
        int n = camadas.Length;
        int iCampo = Indice(lCampo), iVivo = Indice(lVivo), iMata = Indice(lMata), iMataS = Indice(lMataSombria), iMus = Indice(lMusgoSombrio), iRocha = Indice(lRocha), iSeco = Indice(lSeco), iMorta = Indice(lMorta);
        bool[] chao = new bool[n];
        // TUDO é repintado, menos as estradas, a praça (pedrinhas) e a rocha: o chão antigo (musgo, lama, areia preta...) sai de todas as regiões
        for (int k = 0; k < n; k++)
        {
            string nm = camadas[k] != null ? camadas[k].name : "";
            chao[k] = !(nm.Contains("Soil_Rocks") || nm.Contains("Pebbles") || nm.Contains("Rock") || k == iRocha);
        }
        float[,,] a = dados.GetAlphamaps(0, 0, res, res);
        Vector3 o = terreno.transform.position, s = dados.size;
        float[] dest = new float[n];
        for (int z = 0; z < res; z++)
        {
            if (z % 64 == 0) EditorUtility.DisplayProgressBar("Pinglu", "Pintando o chão de cada região... " + (z * 100 / res) + "%", 0.2f + 0.2f * z / res);
            for (int x = 0; x < res; x++)
            {
                // G = tudo o que NÃO é estrada/praça/rocha. Calculado pelo que sobra (1 - resto), e não pela soma do chão antigo:
                // assim células que ficaram com peso zero (chão preto) são consertadas.
                float resto = 0f;
                for (int k = 0; k < n; k++) if (!chao[k]) resto += a[z, x, k];
                float G = Mathf.Clamp01(1f - resto);
                if (G < 0.0005f) continue;
                float px = o.x + (x + 0.5f) / res * s.x, pz = o.z + (z + 0.5f) / res * s.z;
                float incl = dados.GetSteepness((x + 0.5f) / res, (z + 0.5f) / res);
                float rocha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(27f, 40f, incl));
                float n1 = Mathf.PerlinNoise(px * 0.011f + 3f, pz * 0.011f + 9f), n2 = Mathf.PerlinNoise(px * 0.045f + 7f, pz * 0.045f + 1f), n3 = Mathf.PerlinNoise(px * 0.09f + 40f, pz * 0.09f + 13f);
                float mataN = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.64f, Mathf.PerlinNoise(px * 0.0035f + 11f, pz * 0.0035f + 5f)));

                // pesos das regiões (somam no máximo 1; o que sobra é campo comum)
                float wS = rSombria[z, x], wR = Mathf.Min(rRuina[z, x], 1f - wS), wP = Mathf.Min(rPantano[z, x], 1f - wS - wR), wQ = Mathf.Min(rQueimada[z, x], 1f - wS - wR - wP);
                float wM = Mathf.Max(0f, 1f - wS - wR - wP - wQ);
                float vila = rVila[z, x] * wM;       // terra fértil: campo mais vivo e florido em volta da vila

                // campo comum: verde vivo (mais vivo ainda perto da vila), chão de folhas sob as matas
                float sMata = mataN * 0.7f * (0.45f + 0.55f * n2) * (1f - 0.8f * vila);
                float sVivo = (1f - sMata) * Mathf.Clamp01(Mathf.SmoothStep(0.35f, 0.7f, n1) + vila * 0.55f);
                float sCampo = Mathf.Max(0f, 1f - sMata - sVivo);

                for (int k = 0; k < n; k++) dest[k] = 0f;
                if (iCampo >= 0) dest[iCampo] += wM * sCampo;
                if (iVivo >= 0) dest[iVivo] += wM * sVivo;
                if (iMata >= 0) dest[iMata] += wM * sMata + wP * 0.25f + wS * 0.08f;
                // Floresta Sombria: terra escura de folhas, musgo e manchas de capim seco mostarda
                float sSeco = Mathf.SmoothStep(0.5f, 0.75f, n3) * 0.35f;
                float sMus = 0.22f * Mathf.SmoothStep(0.4f, 0.7f, n1);
                if (iMataS >= 0) dest[iMataS] += wS * (1f - sSeco - sMus - 0.08f);
                if (iMus >= 0) dest[iMus] += wS * sMus + wP * 0.5f;
                if (iSeco >= 0) dest[iSeco] += wS * sSeco + wR * (0.35f + 0.3f * n3) + wQ * 0.2f;
                // Castelo Destruído / Terras Queimadas: terra cinza morta, rachada
                if (iMorta >= 0) dest[iMorta] += wR * (0.65f - 0.3f * n3) + wQ * 0.8f;
                if (iMataS >= 0) dest[iMataS] += wP * 0.25f;
                float soma = 0f; for (int k = 0; k < n; k++) soma += dest[k];
                if (soma < 0.0001f) { if (iCampo >= 0) dest[iCampo] = 1f; soma = 1f; }
                for (int k = 0; k < n; k++) if (chao[k]) a[z, x, k] = 0f;
                for (int k = 0; k < n; k++) if (dest[k] > 0f) a[z, x, k] += G * dest[k] / soma * (1f - rocha);
                if (iRocha >= 0) a[z, x, iRocha] += G * rocha;
            }
        }
        dados.SetAlphamaps(0, 0, a);
        PintarEstradasDeTerra();

        // relatório: quanto do mapa caiu em cada região e que chão ficou no centro de cada uma
        System.Text.StringBuilder rel = new System.Text.StringBuilder();
        long cS = 0, cR = 0, cP = 0, cQ = 0, cV = 0;
        for (int z = 0; z < res; z += 4) for (int x = 0; x < res; x += 4) { if (rSombria[z, x] > 0.5f) cS++; if (rRuina[z, x] > 0.5f) cR++; if (rPantano[z, x] > 0.5f) cP++; if (rQueimada[z, x] > 0.5f) cQ++; if (rVila[z, x] > 0.5f) cV++; }
        float tot = (res / 4) * (res / 4);
        rel.AppendLine("Regiões do mapa: Floresta Sombria " + Mathf.RoundToInt(100f * cS / tot) + "%, Ruínas/Castelo " + Mathf.RoundToInt(100f * cR / tot) + "%, Pantano " + Mathf.RoundToInt(100f * cP / tot) + "%, Terras Queimadas " + Mathf.RoundToInt(100f * cQ / tot) + "%, em volta da Vila " + Mathf.RoundToInt(100f * cV / tot) + "%.");
        System.Func<float[,], string, string> onde = (reg, nome) =>
        {
            float best = 0f; int bx = -1, bz = -1;
            for (int z = 0; z < res; z += 8) for (int x = 0; x < res; x += 8) if (reg[z, x] > best) { best = reg[z, x]; bx = x; bz = z; }
            if (bx < 0) return nome + ": região NÃO encontrada";
            int dom = 0; float dv = -1f;
            for (int k = 0; k < n; k++) if (a[bz, bx, k] > dv) { dv = a[bz, bx, k]; dom = k; }
            return nome + ": chão dominante = " + (camadas[dom] != null ? camadas[dom].name : "?") + " (" + Mathf.RoundToInt(dv * 100f) + "%)";
        };
        rel.AppendLine(onde(rSombria, "Floresta Sombria")); rel.AppendLine(onde(rRuina, "Castelo/Ruínas")); rel.AppendLine(onde(rVila, "Vila"));
        relatorioChao = rel.ToString();
        Debug.Log("[Terreno] " + relatorioChao.Replace("\n", " | "));
    }
    // estradas de terra do mundo (Portal - Vila, Trilha - Portal, Vila - Castelo...): repinta o caminho de terra em todo o comprimento,
    // para as estradas sempre se encontrarem com a rua de pedra da vila (o miolo da vila, até 40 m, é das lajes)
    static void PintarEstradasDeTerra()
    {
        RedeDeEstradas rede = Object.FindFirstObjectByType<RedeDeEstradas>();
        if (rede == null) return;
        TerrainLayer[] camadas = dados.terrainLayers; int n = camadas.Length, iE = -1;
        for (int k = 0; k < n; k++) if (camadas[k] != null && camadas[k].name.Contains("Soil_Rocks")) iE = k;
        if (iE < 0) return;
        Vector3 vila = Vector3.zero; bool temVila = false;
        MiniMapa mm = Object.FindFirstObjectByType<MiniMapa>();
        if (mm != null) foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Vila) { vila = k.posicao; temVila = true; }
        int res = dados.alphamapResolution;
        float[,,] a = dados.GetAlphamaps(0, 0, res, res);
        Vector3 o = terreno.transform.position, sz = dados.size;
        float cel = sz.x / res;
        int pintadas = 0;
        foreach (RedeDeEstradas.Estrada e in rede.estradas)
        {
            if (e == null || e.pontos == null || e.pontos.Length < 2) continue;
            float meia = Mathf.Max(1.6f, e.largura * 0.5f * 0.85f);
            for (int i = 0; i + 1 < e.pontos.Length; i++)
            {
                Vector2 A = new Vector2(e.pontos[i].x, e.pontos[i].z), B = new Vector2(e.pontos[i + 1].x, e.pontos[i + 1].z);
                float margem = meia + 1.6f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(A.x, B.x) - margem - o.x) / cel)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(A.x, B.x) + margem - o.x) / cel));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(A.y, B.y) - margem - o.z) / cel)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(A.y, B.y) + margem - o.z) / cel));
                Vector2 AB = B - A; float len2 = Mathf.Max(0.0001f, AB.sqrMagnitude);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 P = new Vector2(o.x + (x + 0.5f) * cel, o.z + (z + 0.5f) * cel);
                        if (temVila && (P.x - vila.x) * (P.x - vila.x) + (P.y - vila.z) * (P.y - vila.z) < 40f * 40f) continue;
                        float t = Mathf.Clamp01(Vector2.Dot(P - A, AB) / len2);
                        float d = (A + AB * t - P).magnitude;
                        // borda irregular: a largura oscila de leve ao longo da estrada
                        float oscila = 1f + 0.25f * (Mathf.PerlinNoise(P.x * 0.21f + 3f, P.y * 0.21f + 8f) - 0.5f);
                        float w = Mathf.Clamp01((meia * oscila + 0.9f - d) / 1.6f);
                        if (w <= 0.01f) continue;
                        float novo = Mathf.Max(a[z, x, iE], w);
                        float outros = 0f; for (int k = 0; k < n; k++) if (k != iE) outros += a[z, x, k];
                        float alvoOutros = 1f - novo;
                        if (outros > 0.0001f) { float f = alvoOutros / outros; for (int k = 0; k < n; k++) if (k != iE) a[z, x, k] *= f; }
                        else if (alvoOutros > 0f) { int iC = Indice(lCampo); if (iC >= 0 && iC != iE) a[z, x, iC] = alvoOutros; }
                        a[z, x, iE] = novo; pintadas++;
                    }
            }
        }
        dados.SetAlphamaps(0, 0, a);
        Debug.Log("[Terreno] estradas de terra repintadas: " + pintadas + " células.");
    }

    static string relatorioChao = "";

    // caminhos: bordas macias e irregulares, com capim entrando pelas beiras
    static void SuavizarCaminhos()
    {
        int iE = -1;
        TerrainLayer[] camadas = dados.terrainLayers;
        for (int k = 0; k < camadas.Length; k++) if (camadas[k] != null && camadas[k].name.Contains("Soil_Rocks")) iE = k;
        if (iE < 0) return;
        int res = dados.alphamapResolution, n = camadas.Length;
        float[,,] a = dados.GetAlphamaps(0, 0, res, res);
        float[,] e = new float[res, res], t = new float[res, res], velho = new float[res, res];
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) { e[z, x] = a[z, x, iE]; velho[z, x] = e[z, x]; }
        for (int passo = 0; passo < 2; passo++)
        {
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float soma = 0f, peso = 0f;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = Mathf.Clamp(x + dx, 0, res - 1), zz = Mathf.Clamp(z + dz, 0, res - 1);
                            float w = (dx == 0 ? 2f : 1f) * (dz == 0 ? 2f : 1f);
                            soma += e[zz, xx] * w; peso += w;
                        }
                    t[z, x] = soma / peso;
                }
            var tmp = e; e = t; t = tmp;
        }
        Vector3 o = terreno.transform.position, s = dados.size;
        int iCampo = Indice(lCampo);
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float b = e[z, x];
                if (b < 0.01f && velho[z, x] < 0.01f) continue;
                float px = o.x + (x + 0.5f) / res * s.x, pz = o.z + (z + 0.5f) / res * s.z;
                float ruido = Mathf.PerlinNoise(px * 0.3f + 17f, pz * 0.3f + 4f) * 0.6f + Mathf.PerlinNoise(px * 0.9f + 2f, pz * 0.9f + 31f) * 0.4f;
                float novo = Mathf.Clamp01((b - 0.14f + (ruido - 0.5f) * 0.45f) * 1.9f);
                if (velho[z, x] > 0.9f) novo = Mathf.Max(novo, 0.85f);      // o miolo do caminho continua firme
                float outros = 0f;
                for (int k = 0; k < n; k++) if (k != iE) outros += a[z, x, k];
                a[z, x, iE] = novo;
                float resto = 1f - novo;
                if (outros > 0.0001f) { float f = resto / outros; for (int k = 0; k < n; k++) if (k != iE) a[z, x, k] *= f; }
                else if (iCampo >= 0) a[z, x, iCampo] = resto;
            }
        dados.SetAlphamaps(0, 0, a);
        EditorPrefs.SetBool(CHAVE_CAMINHOS, true);
    }

    // ------------------------------------------------------------------
    //  CAPIM E FLORES (malhas feitas por código + shader Pinglu/Capim)
    // ------------------------------------------------------------------
    class Gm
    {
        public List<Vector3> v = new List<Vector3>(); public List<Vector3> nrm = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>();
        public List<Color> cor = new List<Color>(); public List<int> tri = new List<int>();
        public int Add(Vector3 p, Vector2 u, Color c) { v.Add(p); nrm.Add(Vector3.up); uv.Add(u); cor.Add(c); return v.Count - 1; }
        public void Quad(int a, int b, int c, int d) { tri.AddRange(new[] { a, b, c, a, c, d }); }
        public Mesh Gerar(string nome)
        {
            Mesh m = new Mesh { name = nome };
            m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetColors(cor); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    // uma lâmina: fita que afina até a ponta, curvada; uv.y = altura (0 raiz, 1 ponta); cor.r = variação de tom
    static void Lamina(Gm g, Vector3 raiz, float yaw, float altura, float largura, float curva, float inclina, int seg, Color c, bool ponta = true)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 lado = q * Vector3.right, frente = q * Vector3.forward;
        int ant0 = -1, ant1 = -1;
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            float w = largura * 0.5f * Mathf.Pow(1f - t, 0.8f);
            Vector3 centro = raiz + Vector3.up * (altura * t * Mathf.Cos(inclina)) + frente * (altura * Mathf.Sin(inclina) * t + curva * t * t);
            centro.y -= curva * t * t * 0.35f;
            if (i == seg && ponta)
            {
                int p = g.Add(centro, new Vector2(0.5f, 1f), c);
                if (ant0 >= 0) { g.tri.AddRange(new[] { ant0, p, ant1 }); }
            }
            else
            {
                int a0 = g.Add(centro - lado * w, new Vector2(0f, t), c), a1 = g.Add(centro + lado * w, new Vector2(1f, t), c);
                if (ant0 >= 0) g.Quad(ant0, a0, a1, ant1);
                ant0 = a0; ant1 = a1;
            }
        }
    }

    static Mesh MalhaDeTufo(string nome, int lamina, float hMin, float hMax, float largura, float raioTufo, int seed, int seg = 3, float inclinacaoMax = 0.4f)
    {
        System.Random r = new System.Random(seed);
        System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        Gm g = new Gm();
        for (int i = 0; i < lamina; i++)
        {
            float ang = F(0f, 6.283f), d = F(0f, raioTufo) * (i == 0 ? 0f : 1f);
            Vector3 raiz = new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
            float h = F(hMin, hMax);
            float tom = F(0f, 1f);
            Lamina(g, raiz, F(0f, 360f), h, largura * F(0.8f, 1.25f), h * F(0.1f, 0.5f), F(0.05f, inclinacaoMax) * (d / Mathf.Max(0.01f, raioTufo) + 0.4f), seg, new Color(tom, 1f, 1f, 1f));
        }
        return g.Gerar(nome);
    }

    static Mesh MalhaDeMargarida(string nome, Color petala, Color miolo, int petalas, float tamanhoCabeca, int hastes, int seed)
    {
        System.Random r = new System.Random(seed);
        System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        Gm g = new Gm();
        Color verde = new Color(0.16f, 0.5f, 0.12f, 1f);
        for (int hIdx = 0; hIdx < hastes; hIdx++)
        {
            float ang = F(0f, 6.283f), d = F(0.02f, 0.16f);
            Vector3 b = new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
            float H = F(0.28f, 0.5f);
            Vector3 topo = b + new Vector3(F(-0.04f, 0.04f), H, F(-0.04f, 0.04f));
            // haste
            Vector3 lado = Quaternion.Euler(0f, F(0f, 180f), 0f) * Vector3.right * 0.006f;
            int h0 = g.Add(b - lado, new Vector2(0f, 0f), verde), h1 = g.Add(b + lado, new Vector2(1f, 0f), verde);
            int h2 = g.Add(topo - lado, new Vector2(0f, 0.95f), verde), h3 = g.Add(topo + lado, new Vector2(1f, 0.95f), verde);
            g.Quad(h0, h2, h3, h1);
            // folhinha na haste
            Vector3 fp = Vector3.Lerp(b, topo, 0.35f); Quaternion qf = Quaternion.Euler(0f, F(0f, 360f), 0f);
            int f0 = g.Add(fp, new Vector2(0.5f, 0.3f), verde), f1 = g.Add(fp + qf * new Vector3(0.05f, 0.05f, 0f) + qf * Vector3.forward * 0.015f, new Vector2(0.5f, 0.4f), verde), f2 = g.Add(fp + qf * new Vector3(0.05f, 0.05f, 0f) - qf * Vector3.forward * 0.015f, new Vector2(0.5f, 0.4f), verde);
            g.tri.AddRange(new[] { f0, f1, f2 });
            // cabeça: pétalas em disco levemente inclinado + miolo
            Quaternion inc = Quaternion.Euler(F(-25f, 25f), F(0f, 360f), F(-25f, 25f));
            float R = tamanhoCabeca * F(0.85f, 1.15f);
            int centro = g.Add(topo, new Vector2(0.5f, 1f), miolo);
            for (int p = 0; p < petalas; p++)
            {
                float a0 = p * 6.283f / petalas, a1 = (p + 0.5f) * 6.283f / petalas, a2 = (p + 1f) * 6.283f / petalas;
                Vector3 pa = topo + inc * new Vector3(Mathf.Cos(a0) * R * 0.3f, 0f, Mathf.Sin(a0) * R * 0.3f);
                Vector3 pb = topo + inc * new Vector3(Mathf.Cos(a1) * R, 0.004f, Mathf.Sin(a1) * R);
                Vector3 pc = topo + inc * new Vector3(Mathf.Cos(a2) * R * 0.3f, 0f, Mathf.Sin(a2) * R * 0.3f);
                int i0 = g.Add(pa, new Vector2(0.5f, 1f), petala), i1 = g.Add(pb, new Vector2(0.5f, 1f), petala), i2 = g.Add(pc, new Vector2(0.5f, 1f), petala);
                g.tri.AddRange(new[] { i0, i1, i2 });
            }
            // miolo (hexágono)
            for (int p = 0; p < 6; p++)
            {
                float a0 = p * 6.283f / 6f, a1 = (p + 1) * 6.283f / 6f;
                int i1 = g.Add(topo + inc * new Vector3(Mathf.Cos(a0) * R * 0.32f, 0.006f, Mathf.Sin(a0) * R * 0.32f), new Vector2(0.5f, 1f), miolo);
                int i2 = g.Add(topo + inc * new Vector3(Mathf.Cos(a1) * R * 0.32f, 0.006f, Mathf.Sin(a1) * R * 0.32f), new Vector2(0.5f, 1f), miolo);
                g.tri.AddRange(new[] { centro, i1, i2 });
            }
        }
        return g.Gerar(nome);
    }

    static Mesh MalhaDeLavanda(string nome, int hastes, int seed)
    {
        System.Random r = new System.Random(seed);
        System.Func<float, float, float> F = (a, b) => a + (float)r.NextDouble() * (b - a);
        Gm g = new Gm();
        Color verde = new Color(0.2f, 0.45f, 0.18f, 1f);
        for (int hIdx = 0; hIdx < hastes; hIdx++)
        {
            float ang = F(0f, 6.283f), d = F(0f, 0.18f);
            Vector3 b = new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
            float H = F(0.4f, 0.75f);
            Quaternion q = Quaternion.Euler(F(-14f, 14f), F(0f, 360f), F(-14f, 14f));
            Vector3 topo = b + q * Vector3.up * H;
            Vector3 lado = q * Vector3.right * 0.007f;
            int h0 = g.Add(b - lado, new Vector2(0f, 0f), verde), h1 = g.Add(b + lado, new Vector2(1f, 0f), verde), h2 = g.Add(topo - lado, new Vector2(0f, 1f), verde), h3 = g.Add(topo + lado, new Vector2(1f, 1f), verde);
            g.Quad(h0, h2, h3, h1);
            // espiga: pequenos losangos roxos empilhados no terço de cima
            int n = 11;
            for (int k = 0; k < n; k++)
            {
                float t = Mathf.Lerp(0.6f, 1f, k / (float)(n - 1));
                Vector3 c = b + q * Vector3.up * (H * t);
                float rr = Mathf.Lerp(0.032f, 0.012f, k / (float)(n - 1));
                Color pc = Color.Lerp(new Color(0.42f, 0.22f, 0.85f), new Color(0.62f, 0.45f, 0.95f), (float)r.NextDouble());
                for (int s = 0; s < 2; s++)
                {
                    Quaternion qs = q * Quaternion.Euler(0f, s * 90f + k * 37f, 0f);
                    Vector3 e1 = qs * Vector3.right * rr, e2 = qs * Vector3.up * rr * 1.5f;
                    int a0 = g.Add(c - e1, new Vector2(0.5f, t), pc), a1 = g.Add(c + e2, new Vector2(0.5f, t), pc), a2 = g.Add(c + e1, new Vector2(0.5f, t), pc), a3 = g.Add(c - e2 * 0.6f, new Vector2(0.5f, t), pc);
                    g.Quad(a0, a1, a2, a3);
                }
            }
        }
        return g.Gerar(nome);
    }

    static Material MaterialCapim(string nome, Color baseC, Color ponta, Color variacao, float manchas, float cor, float vento, float transl)
    {
        string caminho = PASTA_CAPIM + "/" + nome + ".mat";
        Shader sh = Shader.Find("Pinglu/Capim");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, caminho); }
        m.shader = sh;
        m.SetColor("_CorBase", baseC); m.SetColor("_CorPonta", ponta); m.SetColor("_CorVariacao", variacao);
        m.SetFloat("_ForcaManchas", manchas); m.SetFloat("_UsarCorDoVertice", cor); m.SetFloat("_VentoForca", vento); m.SetFloat("_Translucidez", transl);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Prefab(string nome, Mesh malha, Material mat)
    {
        string pm = PASTA_CAPIM + "/" + nome + ".asset", pp = PASTA_CAPIM + "/Pinglu_" + nome + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(pm) != null) AssetDatabase.DeleteAsset(pm);
        AssetDatabase.CreateAsset(malha, pm);
        GameObject go = new GameObject("Pinglu_" + nome);
        go.AddComponent<MeshFilter>().sharedMesh = malha;
        MeshRenderer mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObject pf = PrefabUtility.SaveAsPrefabAsset(go, pp);
        Object.DestroyImmediate(go);
        return pf;
    }

    // textura de um tufinho baixo de capim (lâminas finas em leque), feita por código: usada como "grama de textura" do Terrain (balança com o vento do terreno)
    static Texture2D TexturaDeCapimBaixo()
    {
        string caminho = PASTA_CAPIM + "/CapimBaixo.png";
        Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        if (t != null) return t;
        const int N = 128;
        System.Random r = new System.Random(77);
        int lam = 11;
        float[] x0 = new float[lam], alt = new float[lam], larg = new float[lam], curva = new float[lam];
        for (int i = 0; i < lam; i++)
        {
            x0[i] = N * (0.5f + ((float)r.NextDouble() - 0.5f) * 0.55f);
            alt[i] = N * (0.55f + (float)r.NextDouble() * 0.42f);
            larg[i] = 5f + (float)r.NextDouble() * 4f;
            curva[i] = ((float)r.NextDouble() - 0.5f) * N * 0.55f + (x0[i] - N * 0.5f) * 0.5f;
        }
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float melhor = 0f; bool cobre = false;
                for (int i = 0; i < lam; i++)
                {
                    float tt = y / alt[i];
                    if (tt < 0f || tt > 1f) continue;
                    float xc = x0[i] + curva[i] * tt * tt;
                    float w = larg[i] * 0.5f * Mathf.Pow(1f - tt, 0.75f) + 0.35f;
                    if (Mathf.Abs(x - xc) < w) { cobre = true; melhor = Mathf.Max(melhor, 0.5f + 0.5f * tt); }
                }
                tex.SetPixel(x, y, cobre ? new Color(melhor, melhor, melhor, 1f) : new Color(1f, 1f, 1f, 0f));
            }
        tex.Apply();
        File.WriteAllBytes(caminho, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(caminho);
        TextureImporter ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (ti != null) { ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.4f; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
    }

    class Tipo { public string nome; public GameObject prefab; public float minL, maxL, minA, maxA; public int indice; }

    static void PintarDetalhes()
    {
        EditorUtility.DisplayProgressBar("Pinglu", "Criando capim e flores...", 0.45f);
        rng = new System.Random(2024);
        // capim bonito e vivo (campos e vila)
        Material mCampo = MaterialCapim("Capim_Campo", new Color(0.07f, 0.27f, 0.04f), new Color(0.52f, 0.80f, 0.18f), new Color(0.78f, 0.82f, 0.18f), 0.5f, 0f, 0.14f, 0.55f);
        Material mVivo = MaterialCapim("Capim_Vivo", new Color(0.05f, 0.24f, 0.05f), new Color(0.40f, 0.72f, 0.16f), new Color(0.35f, 0.75f, 0.35f), 0.4f, 0f, 0.14f, 0.55f);
        // capim DOENTE, mostarda, caído (Floresta Sombria e ruínas)
        Material mSeco = MaterialCapim("Capim_Seco", new Color(0.16f, 0.10f, 0.03f), new Color(0.66f, 0.54f, 0.12f), new Color(0.42f, 0.36f, 0.10f), 0.6f, 0f, 0.05f, 0.05f);
        Material mPantano = MaterialCapim("Capim_Pantano", new Color(0.03f, 0.13f, 0.07f), new Color(0.22f, 0.44f, 0.26f), new Color(0.18f, 0.30f, 0.12f), 0.5f, 0f, 0.1f, 0.2f);
        Material mQueimado = MaterialCapim("Capim_Queimado", new Color(0.03f, 0.025f, 0.02f), new Color(0.26f, 0.19f, 0.12f), new Color(0.12f, 0.08f, 0.06f), 0.5f, 0f, 0.05f, 0.0f);
        Material mFlor = MaterialCapim("Flor", Color.white, Color.white, Color.white, 0f, 1f, 0.1f, 0.2f);

        Tipo tA = new Tipo { nome = "CapimA", prefab = Prefab("CapimA", MalhaDeTufo("CapimA", 7, 0.5f, 0.85f, 0.07f, 0.16f, 11), mCampo), minL = 0.9f, maxL = 1.5f, minA = 0.85f, maxA = 1.4f };
        Tipo tB = new Tipo { nome = "CapimB", prefab = Prefab("CapimB", MalhaDeTufo("CapimB", 9, 0.32f, 0.55f, 0.06f, 0.2f, 23), mVivo), minL = 0.9f, maxL = 1.6f, minA = 0.8f, maxA = 1.3f };
        Tipo tC = new Tipo { nome = "CapimC", prefab = Prefab("CapimC", MalhaDeTufo("CapimC", 5, 0.95f, 1.35f, 0.08f, 0.12f, 37), mCampo), minL = 0.9f, maxL = 1.4f, minA = 0.8f, maxA = 1.2f };
        Tipo tS1 = new Tipo { nome = "CapimSeco1", prefab = Prefab("CapimSeco1", MalhaDeTufo("CapimSeco1", 8, 0.3f, 0.6f, 0.05f, 0.2f, 41, 3, 1.0f), mSeco), minL = 0.8f, maxL = 1.4f, minA = 0.7f, maxA = 1.3f };
        Tipo tS2 = new Tipo { nome = "CapimSeco2", prefab = Prefab("CapimSeco2", MalhaDeTufo("CapimSeco2", 6, 0.55f, 0.95f, 0.045f, 0.14f, 43, 3, 1.1f), mSeco), minL = 0.8f, maxL = 1.3f, minA = 0.7f, maxA = 1.2f };
        Tipo tP = new Tipo { nome = "CapimPantano", prefab = Prefab("CapimPantano", MalhaDeTufo("CapimPantano", 6, 0.8f, 1.3f, 0.06f, 0.15f, 47, 3, 0.5f), mPantano), minL = 0.9f, maxL = 1.4f, minA = 0.8f, maxA = 1.3f };
        Tipo tQ = new Tipo { nome = "CapimQueimado", prefab = Prefab("CapimQueimado", MalhaDeTufo("CapimQueimado", 7, 0.2f, 0.45f, 0.05f, 0.18f, 49, 3, 0.9f), mQueimado), minL = 0.8f, maxL = 1.3f, minA = 0.7f, maxA = 1.2f };
        List<Tipo> flores = new List<Tipo>();
        flores.Add(new Tipo { nome = "BotaoDeOuro", prefab = Prefab("BotaoDeOuro", MalhaDeMargarida("BotaoDeOuro", new Color(1f, 0.82f, 0.08f), new Color(0.95f, 0.6f, 0.05f), 6, 0.05f, 6, 51), mFlor), minL = 0.9f, maxL = 1.3f, minA = 0.9f, maxA = 1.3f });
        flores.Add(new Tipo { nome = "Margarida", prefab = Prefab("Margarida", MalhaDeMargarida("Margarida", new Color(0.98f, 0.98f, 0.94f), new Color(1f, 0.8f, 0.1f), 9, 0.075f, 4, 52), mFlor), minL = 0.9f, maxL = 1.3f, minA = 0.9f, maxA = 1.3f });
        flores.Add(new Tipo { nome = "Rosa", prefab = Prefab("Rosa", MalhaDeMargarida("Rosa", new Color(0.95f, 0.42f, 0.66f), new Color(0.98f, 0.85f, 0.3f), 8, 0.07f, 4, 53), mFlor), minL = 0.9f, maxL = 1.3f, minA = 0.9f, maxA = 1.3f });
        flores.Add(new Tipo { nome = "Papoula", prefab = Prefab("Papoula", MalhaDeMargarida("Papoula", new Color(0.95f, 0.22f, 0.1f), new Color(0.1f, 0.06f, 0.05f), 5, 0.085f, 3, 54), mFlor), minL = 0.9f, maxL = 1.3f, minA = 0.9f, maxA = 1.3f });
        flores.Add(new Tipo { nome = "Campanula", prefab = Prefab("Campanula", MalhaDeMargarida("Campanula", new Color(0.35f, 0.5f, 0.97f), new Color(0.95f, 0.95f, 0.7f), 5, 0.05f, 6, 55), mFlor), minL = 0.9f, maxL = 1.3f, minA = 0.9f, maxA = 1.3f });
        flores.Add(new Tipo { nome = "Lavanda", prefab = Prefab("Lavanda", MalhaDeLavanda("Lavanda", 6, 56), mFlor), minL = 0.9f, maxL = 1.4f, minA = 0.9f, maxA = 1.4f });
        AssetDatabase.SaveAssets();
        GameObject[] folhas = PingluVegetacao.PrefabsFolhasNoChaoPublico();

        List<DetailPrototype> protos = new List<DetailPrototype>();
        System.Action<Tipo> addTipo = t =>
        {
            DetailPrototype dp = new DetailPrototype
            {
                prototype = t.prefab, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                minWidth = t.minL * (flores.Contains(t) ? 0.75f : 0.8f), maxWidth = t.maxL * (flores.Contains(t) ? 0.75f : 0.8f), minHeight = t.minA * (flores.Contains(t) ? 0.6f : 0.5f), maxHeight = t.maxA * (flores.Contains(t) ? 0.6f : 0.5f), noiseSpread = 0.4f, density = 1f, alignToGround = 0.15f,
                healthyColor = Color.white, dryColor = Color.white
            };
            string erro;
            if (!dp.Validate(out erro)) { avisos.Add(t.nome + ": " + erro); t.indice = -1; return; }
            t.indice = protos.Count; protos.Add(dp);
        };
        foreach (Tipo t in new[] { tA, tB, tC, tS1, tS2, tP, tQ }) addTipo(t);
        foreach (Tipo t in flores) addTipo(t);
        // GRAMA BAIXA POR CIMA (textura, balança com o vento do terreno): efeito de capim rasteiro por todo o campo
        Texture2D texBaixo = TexturaDeCapimBaixo();
        System.Func<float, float, float, float, Color, Color, int> addBaixo = (wMin, wMax, hMin, hMax, sadio, seco) =>
        {
            DetailPrototype dp = new DetailPrototype
            {
                prototypeTexture = texBaixo, usePrototypeMesh = false, renderMode = DetailRenderMode.GrassBillboard,
                minWidth = wMin, maxWidth = wMax, minHeight = hMin, maxHeight = hMax, noiseSpread = 0.35f, density = 1f, holeEdgePadding = 0.2f,
                healthyColor = sadio, dryColor = seco
            };
            protos.Add(dp);
            return protos.Count - 1;
        };
        int bVivo1 = addBaixo(0.55f, 0.95f, 0.14f, 0.26f, new Color(0.40f, 0.74f, 0.18f), new Color(0.62f, 0.78f, 0.22f));
        int bVivo2 = addBaixo(0.50f, 0.85f, 0.18f, 0.32f, new Color(0.30f, 0.62f, 0.16f), new Color(0.58f, 0.74f, 0.24f));
        int bSeco = addBaixo(0.50f, 0.85f, 0.12f, 0.22f, new Color(0.62f, 0.52f, 0.14f), new Color(0.46f, 0.38f, 0.12f));
        dados.wavingGrassStrength = 0.55f; dados.wavingGrassSpeed = 0.55f; dados.wavingGrassAmount = 0.45f; dados.wavingGrassTint = new Color(0.78f, 0.88f, 0.62f);

        List<int> idxFolhas = new List<int>();
        foreach (GameObject pf in folhas)
        {
            DetailPrototype dp = new DetailPrototype { prototype = pf, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true, minWidth = 0.9f, maxWidth = 1.5f, minHeight = 1f, maxHeight = 1f, noiseSpread = 0.5f, density = 1f, alignToGround = 0f, healthyColor = Color.white, dryColor = Color.white };
            string erro;
            if (dp.Validate(out erro)) { idxFolhas.Add(protos.Count); protos.Add(dp); }
        }

        int res = 1024;      // ~1 m por célula; múltiplo de 16 (exigência do Unity)
        dados.SetDetailResolution(res, 16);
        dados.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        dados.detailPrototypes = protos.ToArray();
        int[][,] L = new int[protos.Count][,];
        for (int i = 0; i < L.Length; i++) L[i] = new int[res, res];

        int ares = dados.alphamapResolution;
        if (rSombria == null || rSombria.GetLength(0) != ares) CalcularRegioes(ares);
        float[,,] al = dados.GetAlphamaps(0, 0, ares, ares);
        int iCampo = Indice(lCampo), iVivo = Indice(lVivo), iMata = Indice(lMata), iMataS = Indice(lMataSombria), iMus = Indice(lMusgoSombrio), iSeco = Indice(lSeco), iMorta = Indice(lMorta);
        Vector3 o = terreno.transform.position, s = dados.size;
        float cel = s.x / res;
        System.Func<float, float, float, float> Rand01 = (x, z, k) => Mathf.Repeat(Mathf.Sin(x * 12.9898f + z * 78.233f + k * 37.719f) * 43758.5453f, 1f);
        System.Func<float[,,], int, int, int, float> W = (arr, zz, xx, k) => k >= 0 ? arr[zz, xx, k] : 0f;
        bool[,] semCapim = MascaraSemCapim(res);
        EditorUtility.DisplayProgressBar("Pinglu", "Espalhando capim e flores...", 0.55f);
        for (int z = 0; z < res; z++)
        {
            if (z % 50 == 0) EditorUtility.DisplayProgressBar("Pinglu", "Espalhando capim e flores... " + (z * 100 / res) + "%", 0.55f + 0.4f * z / res);
            for (int x = 0; x < res; x++)
            {
                float px = o.x + (x + 0.5f) * cel, pz = o.z + (z + 0.5f) * cel;
                if (semCapim[z, x]) continue;       // ruas, praças, pisos, construções, túmulos...: sem capim
                int ax = Mathf.Min(ares - 1, x * ares / res), az = Mathf.Min(ares - 1, z * ares / res);
                float incl = dados.GetSteepness((x + 0.5f) / res, (z + 0.5f) / res);
                float declive = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(32f, 44f, incl));
                if (declive <= 0.01f) continue;
                float wS = rSombria[az, ax], wR = Mathf.Min(rRuina[az, ax], 1f - wS), wP = Mathf.Min(rPantano[az, ax], 1f - wS - wR), wQ = Mathf.Min(rQueimada[az, ax], 1f - wS - wR - wP);
                float wM = Mathf.Max(0f, 1f - wS - wR - wP - wQ);
                float vila = rVila[az, ax] * wM;

                // ---- campo comum: capim vivo, denso e bonito, flores e folhas no chão
                float campo = W(al, az, ax, iCampo) + W(al, az, ax, iVivo), mata = W(al, az, ax, iMata);
                float cobertura = (campo + mata * 0.35f) * wM * declive;
                if (cobertura > 0.02f)
                {
                    float total = TUFOS_POR_M2 * (1f + 0.2f * vila) * Mathf.Clamp01(cobertura) * Mathf.Lerp(0.75f, 1.15f, Mathf.PerlinNoise(px * 0.05f + 5f, pz * 0.05f + 8f));
                    int q = Mathf.FloorToInt(total + Rand01(px, pz, 1f));
                    float va = Mathf.PerlinNoise(px * 0.04f + 1f, pz * 0.04f + 2f) + 0.2f, vb = Mathf.PerlinNoise(px * 0.04f + 50f, pz * 0.04f + 12f) + 0.2f, vc = Mathf.PerlinNoise(px * 0.04f + 90f, pz * 0.04f + 77f) * 0.7f;
                    float sv = va + vb + vc;
                    if (tA.indice >= 0) L[tA.indice][z, x] = Mathf.FloorToInt(q * va / sv + Rand01(px, pz, 2f));
                    if (tB.indice >= 0) L[tB.indice][z, x] = Mathf.FloorToInt(q * vb / sv + Rand01(px, pz, 3f));
                    if (tC.indice >= 0) L[tC.indice][z, x] = Mathf.FloorToInt(q * vc / sv + Rand01(px, pz, 4f));
                    // capim baixo por cima de tudo (efeito de gramado se mexendo)
                    float baixo = 11f * Mathf.Clamp01(cobertura) * Mathf.Lerp(0.8f, 1.2f, Mathf.PerlinNoise(px * 0.09f + 3f, pz * 0.09f + 11f));
                    float mixB = Mathf.PerlinNoise(px * 0.03f + 61f, pz * 0.03f + 7f);
                    L[bVivo1][z, x] = Mathf.FloorToInt(baixo * (0.45f + 0.4f * mixB) + Rand01(px, pz, 11f));
                    L[bVivo2][z, x] = Mathf.FloorToInt(baixo * (0.55f - 0.4f * mixB) + Rand01(px, pz, 12f));
                    if (cobertura > 0.4f)
                        for (int k = 0; k < flores.Count; k++)
                        {
                            if (flores[k].indice < 0) continue;
                            float f = Mathf.PerlinNoise(px * 0.024f + 100f + k * 41f, pz * 0.024f + 60f + k * 17f);
                            float mancha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f - 0.06f * vila, 0.66f - 0.06f * vila, f));
                            if (mancha <= 0f) continue;
                            float dens = (k == flores.Count - 1 ? 3.5f : 4.5f) * mancha * Mathf.Clamp01(cobertura) * (1f + 0.6f * vila);
                            L[flores[k].indice][z, x] = Mathf.FloorToInt(dens + Rand01(px, pz, 20f + k));
                        }
                    for (int k = 0; k < idxFolhas.Count; k++)
                    {
                        float ruido = Mathf.PerlinNoise(px * 0.035f + 300f + k * 31f, pz * 0.035f + 210f);
                        float mn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.7f, ruido)) * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(mata * 2f));
                        L[idxFolhas[k]][z, x] = Mathf.FloorToInt(1.6f * mn + Rand01(px, pz, 40f + k));
                    }
                }

                // ---- terra morta: capim mostarda doente (Floresta Sombria e Castelo Destruído) e restolho queimado
                float morto = (W(al, az, ax, iSeco) + W(al, az, ax, iMorta) * 0.5f + W(al, az, ax, iMataS) * 0.4f + W(al, az, ax, iMus) * 0.2f) * (wS + wR + wQ * 0.4f) * declive;
                if (morto > 0.02f)
                {
                    float total = 3.4f * Mathf.Clamp01(morto) * (0.6f + 0.8f * Mathf.PerlinNoise(px * 0.07f + 9f, pz * 0.07f + 4f));
                    int q = Mathf.FloorToInt(total + Rand01(px, pz, 5f));
                    float mix = Mathf.PerlinNoise(px * 0.05f + 20f, pz * 0.05f + 30f);
                    if (tS1.indice >= 0) L[tS1.indice][z, x] = Mathf.FloorToInt(q * (0.4f + 0.4f * mix) + Rand01(px, pz, 6f));
                    if (tS2.indice >= 0) L[tS2.indice][z, x] = Mathf.FloorToInt(q * (0.6f - 0.4f * mix) + Rand01(px, pz, 7f));
                    L[bSeco][z, x] = Mathf.FloorToInt(6f * Mathf.Clamp01(morto) * (0.6f + 0.8f * Mathf.PerlinNoise(px * 0.08f + 5f, pz * 0.08f + 9f)) + Rand01(px, pz, 13f));
                }
                if (wQ > 0.15f && tQ.indice >= 0)
                {
                    float dq = 3f * wQ * declive * (0.6f + 0.8f * Mathf.PerlinNoise(px * 0.08f + 3f, pz * 0.08f + 6f));
                    L[tQ.indice][z, x] = Mathf.FloorToInt(dq + Rand01(px, pz, 8f));
                }
                if (wP > 0.15f && tP.indice >= 0)
                {
                    float dp2 = 3.5f * wP * declive * (0.5f + W(al, az, ax, iMus) + W(al, az, ax, iMata) * 0.5f);
                    L[tP.indice][z, x] = Mathf.FloorToInt(dp2 + Rand01(px, pz, 9f));
                }
            }
        }
        for (int i = 0; i < protos.Count; i++) dados.SetDetailLayer(0, 0, i, L[i]);
        dados.RefreshPrototypes();
    }

    // ------------------------------------------------------------------
    //  ONDE NÃO NASCE CAPIM: em cima de ruas, praças e pisos de pedra (malhas planas) e embaixo/em volta de construções, estátuas, túmulos, postes...
    // ------------------------------------------------------------------
    static void PreencherPoligono(bool[,] m, List<Vector2> poli, float margem, Vector3 o, float cel, int res)
    {
        if (poli.Count < 3) return;
        Vector2 c = Vector2.zero; foreach (Vector2 p in poli) c += p; c /= poli.Count;
        List<Vector2> q = new List<Vector2>();
        foreach (Vector2 p in poli) { Vector2 d = p - c; q.Add(p + (d.sqrMagnitude > 0.0001f ? d.normalized * margem : Vector2.zero)); }
        // envoltória convexa (cadeia monótona)
        q.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        List<Vector2> h = new List<Vector2>();
        System.Func<Vector2, Vector2, Vector2, float> cruz = (a, b, d) => (b.x - a.x) * (d.y - a.y) - (b.y - a.y) * (d.x - a.x);
        for (int i = 0; i < q.Count; i++) { while (h.Count >= 2 && cruz(h[h.Count - 2], h[h.Count - 1], q[i]) <= 0f) h.RemoveAt(h.Count - 1); h.Add(q[i]); }
        int baixo = h.Count + 1;
        for (int i = q.Count - 2; i >= 0; i--) { while (h.Count >= baixo && cruz(h[h.Count - 2], h[h.Count - 1], q[i]) <= 0f) h.RemoveAt(h.Count - 1); h.Add(q[i]); }
        if (h.Count > 1) h.RemoveAt(h.Count - 1);
        if (h.Count < 3) return;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector2 p in h) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
        int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - o.x) / cel)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxX - o.x) / cel));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((minZ - o.z) / cel)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxZ - o.z) / cel));
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 pc = new Vector2(o.x + (x + 0.5f) * cel, o.z + (z + 0.5f) * cel);
                bool dentro = true;
                for (int i = 0; i < h.Count; i++) { Vector2 a = h[i], b = h[(i + 1) % h.Count]; if (cruz(a, b, pc) < -0.0001f) { dentro = false; break; } }
                if (dentro) m[z, x] = true;
            }
    }

    static void PreencherMalhaPlana(bool[,] m, Mesh me, Matrix4x4 mtx, Vector3 o, float cel, int res)
    {
        Vector3[] vs = me.vertices; int[] tr = me.triangles;
        for (int i = 0; i + 2 < tr.Length; i += 3)
        {
            Vector3 a = mtx.MultiplyPoint3x4(vs[tr[i]]), b = mtx.MultiplyPoint3x4(vs[tr[i + 1]]), c = mtx.MultiplyPoint3x4(vs[tr[i + 2]]);
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)), minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - o.x) / cel) - 1), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxX - o.x) / cel) + 1);
            int z0 = Mathf.Max(0, Mathf.FloorToInt((minZ - o.z) / cel) - 1), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxZ - o.z) / cel) + 1);
            Vector2 A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), C = new Vector2(c.x, c.z);
            float area = (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
            if (Mathf.Abs(area) < 1e-5f) continue;
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 P = new Vector2(o.x + (x + 0.5f) * cel, o.z + (z + 0.5f) * cel);
                    float w0 = ((B.x - P.x) * (C.y - P.y) - (B.y - P.y) * (C.x - P.x)) / area, w1 = ((C.x - P.x) * (A.y - P.y) - (C.y - P.y) * (A.x - P.x)) / area, w2 = 1f - w0 - w1;
                    float t = -0.35f * cel / Mathf.Sqrt(Mathf.Abs(area));       // pequena folga: pega também as células da beirada
                    if (w0 >= t && w1 >= t && w2 >= t) m[z, x] = true;
                }
        }
    }

    static bool[,] MascaraSemCapim(int res)
    {
        bool[,] m = new bool[res, res];
        Vector3 o = terreno.transform.position, s = dados.size;
        float cel = s.x / res;
        int n = 0;
        foreach (string raiz in new[] { "Vila_Gerada", "Mundo_Gerado" })
        {
            GameObject g = GameObject.Find(raiz);
            if (g == null) continue;
            foreach (MeshRenderer r in g.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (r == null || !r.enabled) continue;
                bool natureza = false;
                for (Transform t = r.transform; t != null && t != g.transform; t = t.parent) if (t.name == "Natureza") { natureza = true; break; }
                if (natureza) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                Mesh me = mf != null ? mf.sharedMesh : null;
                Bounds b = r.bounds;
                bool doCaminho = false;
                for (Transform t = r.transform; t != null && t != g.transform; t = t.parent) if (t.name == "Caminhos da Vila") { doCaminho = true; break; }
                // ruas e praça da vila acompanham o relevo (podem ter mais de 1,3 m de altura na caixa): sempre usar a forma exata, nunca o contorno
                // plantações, pomar, moinho...: usa o contorno exato das peças (não a caixa), senão sobra um "quadrado" sem capim em volta; cercas não limpam capim
                bool daColheita = false;
                for (Transform t = r.transform; t != null && t != g.transform; t = t.parent) if (t.name == "Colheita e Arredores") { daColheita = true; break; }
                if (daColheita && r.name.Contains("Cerca")) continue;
                bool plana = (doCaminho || daColheita || (b.size.y < 1.3f && Mathf.Max(b.size.x, b.size.z) > 2.5f)) && me != null && me.isReadable;
                if (plana) { PreencherMalhaPlana(m, me, r.transform.localToWorldMatrix, o, cel, res); n++; continue; }
                // construções, estátuas, túmulos, postes, bancos...: o contorno (caixa girada) com uma folga de 0,6 m
                List<Vector2> pts = new List<Vector2>();
                if (me != null)
                {
                    Bounds lb = me.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 cant = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 w = r.transform.TransformPoint(cant); pts.Add(new Vector2(w.x, w.z));
                    }
                }
                else { pts.Add(new Vector2(b.min.x, b.min.z)); pts.Add(new Vector2(b.max.x, b.min.z)); pts.Add(new Vector2(b.max.x, b.max.z)); pts.Add(new Vector2(b.min.x, b.max.z)); }
                PreencherPoligono(m, pts, 0.6f, o, cel, res); n++;
            }
        }
        Debug.Log("[Terreno] sem capim: " + n + " objetos/pisos marcados.");
        return m;
    }

    // ruas e praça antigas da vila (as "marcações" de terra em volta das coisas) saem do chão pintado; as ruas novas são de pedra (menu 56)
    static void LimparPinturaDaVila()
    {
        GameObject vilaGo = GameObject.Find("Vila_Gerada");
        if (vilaGo == null || vilaGo.transform.Find("Caminhos da Vila") == null) return;
        MiniMapa mm = Object.FindFirstObjectByType<MiniMapa>(); if (mm == null) return;
        Vector3 c = Vector3.zero; bool tem = false;
        foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Vila) { c = k.posicao; tem = true; }
        if (!tem) return;
        int res = dados.alphamapResolution; TerrainLayer[] camadas = dados.terrainLayers; int n = camadas.Length;
        int iCampo = Indice(lCampo); if (iCampo < 0) return;
        float[,,] a = dados.GetAlphamaps(0, 0, res, res);
        Vector3 o = terreno.transform.position, sz = dados.size;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float px = o.x + (x + 0.5f) / res * sz.x, pz = o.z + (z + 0.5f) / res * sz.z;
                float d = Mathf.Sqrt((px - c.x) * (px - c.x) + (pz - c.z) * (pz - c.z));
                if (d > 46f) continue;    // só a praça e o miolo da vila; a estrada de terra que chega continua
                float tira = 0f;
                for (int k = 0; k < n; k++) { string nm = camadas[k] != null ? camadas[k].name : ""; if (nm.Contains("Soil_Rocks") || nm.Contains("Pebbles")) { tira += a[z, x, k]; a[z, x, k] = 0f; } }
                if (tira > 0f) a[z, x, iCampo] += tira;
            }
        dados.SetAlphamaps(0, 0, a);
    }

    static void AjustarTerreno()
    {
        terreno.detailObjectDistance = DISTANCIA_DETALHES;
        terreno.detailObjectDensity = 1f;
        terreno.heightmapPixelError = 4f;
        terreno.basemapDistance = 1000f;
    }
}
#endif
