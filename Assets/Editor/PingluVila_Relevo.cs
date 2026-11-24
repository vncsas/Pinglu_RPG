// =====================================================================
//  PingluVila_Relevo.cs  -  colinas suaves nos campos (estilo Zelda / Fable)
// ---------------------------------------------------------------------
//  - soma ao terreno um relevo de morros largos e ondulações (alguns metros), só nos CAMPOS:
//    a vila, o início do jogo, o cemitério, o castelo, os acampamentos, a Floresta Sombria, a água,
//    as montanhas (encostas íngremes) e a borda do mapa NÃO são tocados
//  - o relevo é guardado (Relevo_Delta.bytes) e pode ser retirado; aplicar duas vezes não soma duas vezes
//  - as estradas e a vegetação são refeitas depois em cima do relevo novo
//
//  COMO USAR:  Pinglu > 50. Relevo dos campos   (Play desligado; depois Ctrl+S)
//  Para tirar: Pinglu > 51. Remover relevo dos campos
// =====================================================================
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string ARQ_RELEVO = "Assets/Cenario/Vegetacao/Relevo_Delta.bytes";
    const float RELEVO_GRANDE = 10f, RELEVO_MEDIO = 3.2f, RELEVO_FINO = 0.5f;     // metros

    [MenuItem("Pinglu/50. Relevo dos campos")]
    public static void RelevoMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (Terrain.activeTerrain == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu - Relevo dos campos",
            "Vou criar colinas suaves e ondulações nos campos (o terreno hoje é muito liso).\n\n" +
            "NÃO mexo: vila, início do jogo, cemitério, castelo, acampamentos, Floresta Sombria, água e montanhas.\n\n" +
            "Depois refaço as estradas, o GPS e a vegetação em cima do relevo novo. Demora alguns minutos.", "Pode fazer", "Cancelar")) return;
        // as estradas guardam um backup do terreno: refazê-las aplica o relevo antes e deixa o backup já com ele
        EditorPrefs.SetBool("PingluRelevo_Pedido", true);     // vale também para as próximas vezes que as estradas forem refeitas (menus 31, 39, 45...)
        PingluVila.ConstruirEstradasPublico();
        EditorUtility.DisplayDialog("Pinglu", "Relevo dos campos pronto, com estradas e vegetação refeitas.\n\nSalve com Ctrl + S.\nPara voltar: Pinglu > 51.", "OK");
    }

    public static void ConstruirEstradasPublico() { ConstruirEstradas(false); }

    [MenuItem("Pinglu/51. Remover relevo dos campos")]
    public static void RemoverRelevoMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) return;
        dados = terreno.terrainData;
        if (!File.Exists(ARQ_RELEVO)) { EditorUtility.DisplayDialog("Pinglu", "Não há relevo guardado para remover.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu", "Tirar o relevo dos campos e refazer estradas e vegetação?", "Sim", "Cancelar")) return;
        if (GameObject.Find("Estradas") != null) DesfazerEstradas(false);
        terreno = Terrain.activeTerrain; dados = terreno.terrainData;
        if (RelevoAplicado()) SomarRelevo(-1f);
        File.Delete(ARQ_RELEVO); AssetDatabase.DeleteAsset(ARQ_RELEVO);
        EditorPrefs.SetBool("PingluRelevo_Pedido", false);
        EditorPrefs.DeleteKey(ChaveRelevo());
        terreno.Flush(); EditorUtility.SetDirty(dados); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
        ConstruirEstradas(false);
        EditorUtility.DisplayDialog("Pinglu", "Relevo removido. Salve com Ctrl + S.", "OK");
    }

    // ------------------------------------------------------------------
    static string ChaveRelevo() { return "PingluRelevo_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(dados)); }

    // o relevo "está no terreno" se as alturas de alguns pontos de teste ainda batem com as de quando foi aplicado
    static bool RelevoAplicado()
    {
        string s = EditorPrefs.GetString(ChaveRelevo(), "");
        if (string.IsNullOrEmpty(s)) return false;
        string[] it = s.Split(';'); int ok = 0, n = 0;
        foreach (string t in it)
        {
            string[] v = t.Split(',');
            if (v.Length != 3) continue;
            float x = float.Parse(v[0], System.Globalization.CultureInfo.InvariantCulture), z = float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture), h = float.Parse(v[2], System.Globalization.CultureInfo.InvariantCulture);
            n++;
            if (Mathf.Abs(Altura(x, z) - h) < 0.08f) ok++;
        }
        return n > 0 && ok >= Mathf.CeilToInt(n * 0.75f);
    }

    // chamado no começo de ConstruirEstradas (depois que o backup antigo foi restaurado)
    public static void AplicarRelevoSeFalta()
    {
        terreno = Terrain.activeTerrain; dados = terreno.terrainData;
        if (!RelevoPedido()) return;
        if (RelevoAplicado()) return;
        CalcularRelevo();
        SomarRelevo(1f);
    }

    static bool RelevoPedido() { return EditorPrefs.GetBool("PingluRelevo_Pedido", false); }

    static float Fbm2(float x, float z, float f, float off)
    {
        return 0.65f * Mathf.PerlinNoise(x * f + off, z * f + off * 0.7f) + 0.35f * Mathf.PerlinNoise(x * f * 2.3f + off + 40f, z * f * 2.3f + off * 0.7f + 17f);
    }

    static float FuncaoRelevo(float x, float z)
    {
        float a = (Fbm2(x, z, 0.0042f, 50f) - 0.5f) * 2f;      // morros largos (~230 m)
        float b = (Fbm2(x, z, 0.011f, 10f) - 0.5f) * 2f;       // ondulações (~90 m)
        float c = (Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 9f) - 0.5f) * 2f;   // textura fina (~20 m)
        return a * RELEVO_GRANDE + b * RELEVO_MEDIO + c * RELEVO_FINO;
    }

    // calcula (e guarda em arquivo) o quanto subir/descer cada ponto do terreno
    static void CalcularRelevo()
    {
        EditorUtility.DisplayProgressBar("Pinglu", "Calculando o relevo dos campos...", 0.05f);
        int res = dados.heightmapResolution;
        Vector3 o = terreno.transform.position, s = dados.size;
        zonas.Clear();
        MontarZonasProtegidas(AcharJogador());
        MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
        if (mm != null) foreach (MiniMapa.Marcador m in mm.marcadores) zonas.Add(new Zona { pos = m.posicao, raio = m.tipo == MiniMapa.Tipo.Vila ? 160f : (m.tipo == MiniMapa.Tipo.Cemiterio ? 150f : (m.tipo == MiniMapa.Tipo.Ruinas ? 125f : 70f)) });
        LimiteDaFloresta lim = Object.FindFirstObjectByType<LimiteDaFloresta>();
        float[,] h = dados.GetHeights(0, 0, res, res);
        float[] delta = new float[res * res];
        StringBuilder sonda = new StringBuilder(); int nSonda = 0;
        System.Random r2 = new System.Random(99);
        for (int z = 0; z < res; z++)
        {
            if (z % 64 == 0) EditorUtility.DisplayProgressBar("Pinglu", "Calculando o relevo dos campos... " + (z * 100 / res) + "%", 0.05f + 0.4f * z / res);
            for (int x = 0; x < res; x++)
            {
                float wx = o.x + x / (float)(res - 1) * s.x, wz = o.z + z / (float)(res - 1) * s.z;
                float w = 1f;
                // borda do mapa
                float bx = Mathf.Min(wx - o.x, o.x + s.x - wx), bz = Mathf.Min(wz - o.z, o.z + s.z - wz);
                w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 70f, Mathf.Min(bx, bz)));
                if (w <= 0f) continue;
                // zonas protegidas (com 60 m de transição)
                for (int k = 0; k < zonas.Count && w > 0f; k++)
                {
                    float dx = wx - zonas[k].pos.x, dz = wz - zonas[k].pos.z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) - zonas[k].raio;
                    if (d < 60f) w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 60f, d));
                }
                if (w <= 0f) continue;
                // Floresta Sombria
                if (lim != null && lim.Valido) w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, -50f, lim.DistanciaDaBorda(wx, wz)));
                if (w <= 0f) continue;
                // água / praia e montanhas
                float hm = h[z, x] * s.y;
                w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 9f, hm));
                if (w <= 0f) continue;
                float incl = dados.GetSteepness(x / (float)(res - 1), z / (float)(res - 1));
                w *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, 24f, incl));
                if (w <= 0.001f) continue;
                float d2 = FuncaoRelevo(wx, wz) * w;
                delta[z * res + x] = d2;
                if (nSonda < 30 && Mathf.Abs(d2) > 1.2f && r2.NextDouble() < 0.0004) { sonda.Append(wx.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(wz.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",PH;"); nSonda++; }
            }
        }
        // guarda o delta em arquivo
        byte[] bytes = new byte[delta.Length * 4];
        System.Buffer.BlockCopy(delta, 0, bytes, 0, bytes.Length);
        Directory.CreateDirectory(Path.GetDirectoryName(ARQ_RELEVO));
        File.WriteAllBytes(ARQ_RELEVO, bytes);
        AssetDatabase.ImportAsset(ARQ_RELEVO);
        relevoSondas = sonda.ToString();
        EditorUtility.ClearProgressBar();
    }
    static string relevoSondas = "";

    // soma (sinal +1) ou tira (sinal -1) o relevo guardado
    static void SomarRelevo(float sinal)
    {
        if (!File.Exists(ARQ_RELEVO)) return;
        int res = dados.heightmapResolution;
        byte[] bytes = File.ReadAllBytes(ARQ_RELEVO);
        if (bytes.Length != res * res * 4) { Debug.LogWarning("[Relevo] o arquivo guardado não bate com o terreno; ignorei."); return; }
        float[] delta = new float[res * res];
        System.Buffer.BlockCopy(bytes, 0, delta, 0, bytes.Length);
        Undo.RegisterCompleteObjectUndo(dados, "Relevo dos campos");
        float[,] h = dados.GetHeights(0, 0, res, res);
        float sy = dados.size.y;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float d = delta[z * res + x];
                if (d != 0f) h[z, x] = Mathf.Clamp01(h[z, x] + sinal * d / sy);
            }
        dados.SetHeights(0, 0, h);
        terreno.Flush();
        EditorUtility.SetDirty(dados);
        if (sinal > 0f)
        {
            // pontos de teste com a altura JÁ com o relevo (para saber depois se ele ainda está no terreno)
            Vector3 o = terreno.transform.position;
            StringBuilder sb = new StringBuilder();
            foreach (string t in relevoSondas.Split(';'))
            {
                string[] v = t.Split(',');
                if (v.Length != 3) continue;
                float x = float.Parse(v[0], System.Globalization.CultureInfo.InvariantCulture), z = float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture);
                sb.Append(v[0]).Append(',').Append(v[1]).Append(',').Append(Altura(x, z).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            }
            string txt = sb.ToString(); if (txt.EndsWith(";")) txt = txt.Substring(0, txt.Length - 1);
            if (txt.Length == 0 && !string.IsNullOrEmpty(EditorPrefs.GetString(ChaveRelevo(), ""))) txt = EditorPrefs.GetString(ChaveRelevo(), "");
            EditorPrefs.SetString(ChaveRelevo(), txt);
        }
    }
}
#endif
