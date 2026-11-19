using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 75. Animais novos: ovelhas e cavalos com esqueleto   (Play desligado)
//  O colega fez ovelha, cavalo e cavalo de tração com esqueleto e animações (parado, andar, trotar, galopar, comer).
//  Este menu:
//   1) prepara os 3 (roda os "Montar tudo" dele, sem trocar a cena aberta);
//   2) troca, na cena ABERTA, as ovelhas e os cavalos antigos (sem esqueleto, só balançavam) pelos novos, nos mesmos lugares;
//   3) acrescenta MAIS rebanhos de ovelhas e cavalos pelos campos em volta da vila (grupo "Animais Novos").
//  Os animais usam o script AnimalVivo: parado, andando, pastando (abaixa a cabeça), e fogem trotando/galopando de gente e monstros.
//  Rodar de novo refaz o grupo "Animais Novos". Antes de mexer, guarda uma cópia da cena em Backups/ (fora do Assets).
// ==========================================================================================
public static class PingluAnimaisNovos
{
    class Tipo
    {
        public string nome, montador, pasta, prefab;
        public float lod = 0.006f;          // some quando ocupa menos que isso da tela (menor = aparece de mais longe)
        public float raio, andar, passoAndar, trote, passoTrote, galope, passoGalope;
        public GameObject modelo;
    }

    static readonly Tipo OVELHA = new Tipo { nome = "Ovelha", montador = "MontarOvelha", pasta = "Assets/Personagens/Ovelha", prefab = "Ovelha.prefab", lod = 0.002f, raio = 12f, andar = 0.7f, passoAndar = 0.8f, trote = 2.0f, passoTrote = 2.2f, galope = 4.2f, passoGalope = 5.0f };
    static readonly Tipo CAVALO = new Tipo { nome = "Cavalo", montador = "MontarCavalo", pasta = "Assets/Personagens/Cavalo", prefab = "Cavalo.prefab", raio = 16f, andar = 1.2f, passoAndar = 1.4f, trote = 3.0f, passoTrote = 3.5f, galope = 6.5f, passoGalope = 7.5f };
    static readonly Tipo CAVALO2 = new Tipo { nome = "Cavalo", montador = "MontarCavalo2", pasta = "Assets/Personagens/Cavalo2", prefab = "Cavalo2.prefab", raio = 16f, andar = 1.1f, passoAndar = 1.3f, trote = 2.8f, passoTrote = 3.3f, galope = 6.0f, passoGalope = 7.0f };

    // quantos rebanhos a mais (além dos animais antigos trocados) e quantos animais por rebanho
    const int REBANHOS_OVELHA = 13, OVELHA_MIN = 4, OVELHA_MAX = 7;
    const int REBANHOS_CAVALO = 5, CAVALO_MIN = 2, CAVALO_MAX = 3;

    [MenuItem("Pinglu/75. Animais novos: ovelhas e cavalos com esqueleto")]
    public static void Trocar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        Terrain terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o terreno na cena aberta. Abra a cena do jogo (SampleScene ou SampleScene_Ambientacao) e rode de novo.", "OK"); return; }

        // limpeza de rodadas anteriores (e de qualquer animal novo deixado pela metade)
        GameObject velho = GameObject.Find("Animais Novos"); if (velho != null) Object.DestroyImmediate(velho);
        foreach (AnimalAmbiente a in Object.FindObjectsByType<AnimalAmbiente>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (a != null && (a.gameObject.name == "Ovelha" || a.gameObject.name == "Cavalo")) Object.DestroyImmediate(a.gameObject);

        AnimalDeFazenda[] antigos = Object.FindObjectsByType<AnimalDeFazenda>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(a => a.gameObject.name == "Ovelha" || a.gameObject.name == "Cavalo").ToArray();
        int estimativa = antigos.Length + REBANHOS_OVELHA * (OVELHA_MIN + OVELHA_MAX) / 2 + REBANHOS_CAVALO * (CAVALO_MIN + CAVALO_MAX) / 2;
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou trocar " + antigos.Length + " animais antigos (ovelhas e cavalos) pelos novos e acrescentar mais rebanhos pelos campos: uns " + estimativa + " animais com esqueleto no total.\n\nPrimeiro o Unity prepara os modelos (pode demorar). Uma cópia da cena fica na pasta Backups do projeto.", "Continuar", "Cancelar")) return;

        var log = new System.Text.StringBuilder();
        OVELHA.modelo = Preparar(OVELHA, log); CAVALO.modelo = Preparar(CAVALO, log); CAVALO2.modelo = Preparar(CAVALO2, log);

        var cena = EditorSceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(cena.path) && File.Exists(cena.path))
        {
            Directory.CreateDirectory("Backups");
            string copia = "Backups/" + Path.GetFileNameWithoutExtension(cena.path) + "_antes_dos_animais.unity";
            if (!File.Exists(copia)) { File.Copy(cena.path, copia, false); log.AppendLine("Cópia da cena em " + copia); }
        }

        GameObject grupo = new GameObject("Animais Novos"); Undo.RegisterCreatedObjectUndo(grupo, "Animais novos");
        int cavaloN = 0, trocados = 0, novos = 0;

        // 1) troca os antigos, no mesmo lugar
        foreach (AnimalDeFazenda a in antigos)
        {
            if (a == null) continue;
            Tipo t = a.gameObject.name == "Ovelha" ? OVELHA : (cavaloN++ % 2 == 0 ? CAVALO : CAVALO2);
            if (t.modelo == null && t != OVELHA) t = CAVALO.modelo != null ? CAVALO : CAVALO2;
            if (t.modelo == null) continue;
            Vector3 pos = a.transform.position;
            Object.DestroyImmediate(a.gameObject);
            if (Colocar(t, pos, grupo.transform) != null) trocados++;
        }

        // 2) rebanhos novos
        Vector3 vila = CentroDaVila(terreno);
        HashSet<long> arvores = CelulasDeArvores(terreno);
        List<Vector3> usados = new List<Vector3>();
        novos += Rebanhos(terreno, vila, arvores, usados, grupo.transform, OVELHA, null, REBANHOS_OVELHA, OVELHA_MIN, OVELHA_MAX);
        novos += Rebanhos(terreno, vila, arvores, usados, grupo.transform, CAVALO, CAVALO2, REBANHOS_CAVALO, CAVALO_MIN, CAVALO_MAX);

        EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("Pinglu", trocados + " animais antigos trocados e " + novos + " novos espalhados pelos campos (grupo \"Animais Novos\").\n\nAperte Play e vá a um pasto: eles ficam parados, andam, abaixam a cabeça para comer e fogem trotando ou galopando de quem chega perto.\n" + (log.Length > 0 ? "\n" + log : ""), "OK");
    }

    // ------------------------------------------------------------------ modelos
    static GameObject Preparar(Tipo t, System.Text.StringBuilder log)
    {
        string caminho = t.pasta + "/" + t.prefab;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(caminho) == null)
        {
            EditorUtility.DisplayProgressBar("Animais novos", "Preparando " + t.nome + "...", 0.5f);
            try
            {
                System.Type tipo = typeof(PingluAnimaisNovos).Assembly.GetType(t.montador);
                if (tipo == null) { log.AppendLine("- não achei o script " + t.montador + " do colega."); return null; }
                tipo.GetMethod("Montar", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            }
            catch (System.Exception e) { log.AppendLine("- " + t.nome + ": erro ao montar (" + (e.InnerException != null ? e.InnerException.Message : e.Message) + ")."); }
            finally { EditorUtility.ClearProgressBar(); }
        }
        GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (p == null) log.AppendLine("- faltou o prefab " + caminho);
        return p;
    }

    static AnimationClip Clipe(string fbx, string nome)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == nome && !c.name.StartsWith("__preview__"));
    }

    // coloca um animal novo: prefab do colega + script AnimalVivo com as animações ligadas
    static GameObject Colocar(Tipo t, Vector3 pos, Transform grupo)
    {
        if (t.modelo == null) return null;
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(t.modelo, grupo);
        g.name = t.nome;
        g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        foreach (AnimalAmbiente velho in g.GetComponents<AnimalAmbiente>()) Object.DestroyImmediate(velho);      // o movimento agora é do AnimalVivo
        foreach (Component c in g.GetComponents<MonoBehaviour>()) if (c != null && c.GetType().Name.EndsWith("Demo")) Object.DestroyImmediate(c);

        string fbx = AssetDatabase.FindAssets("t:Model", new[] { t.pasta }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith(".fbx"));
        AnimalVivo v = g.AddComponent<AnimalVivo>();
        if (fbx != null)
        {
            v.parado = Clipe(fbx, "Idle"); v.andando = Clipe(fbx, "Walk"); v.trote = Clipe(fbx, "Trot"); v.galope = Clipe(fbx, "Gallop");
            v.comerEntrar = Clipe(fbx, "Comer_Entrar"); v.comer = Clipe(fbx, "Comer"); v.comerSair = Clipe(fbx, "Comer_Sair");
        }
        float var = Random.Range(0.88f, 1.12f);
        v.raio = t.raio; v.velAndar = t.andar * var; v.velTrote = t.trote * var; v.velGalope = t.galope * var;
        v.passoDoAndar = t.passoAndar; v.passoDoTrote = t.passoTrote; v.passoDoGalope = t.passoGalope;
        v.fugirGalopando = true;
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        LODGroup lg = g.GetComponent<LODGroup>(); if (lg == null) lg = g.AddComponent<LODGroup>();
        lg.SetLODs(new[] { new LOD(t.lod, rs) }); lg.RecalculateBounds();                  // some de longe (modelos pesados)
        Undo.RegisterCreatedObjectUndo(g, "Animal novo");
        return g;
    }

    // ------------------------------------------------------------------ onde colocar
    static Vector3 CentroDaVila(Terrain t)
    {
        foreach (Transform x in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (x.name == "Poco") return x.position;
        Vector3 o = t.transform.position, s = t.terrainData.size; return o + new Vector3(s.x * 0.5f, 0f, s.z * 0.5f);
    }

    static long Chave(int ix, int iz) { return (long)ix * 100000L + iz; }

    // células de 4 m ocupadas por árvores do terreno (os animais não nascem dentro do mato fechado)
    static HashSet<long> CelulasDeArvores(Terrain t)
    {
        HashSet<long> s = new HashSet<long>(); TerrainData td = t.terrainData; Vector3 o = t.transform.position, sz = td.size;
        foreach (TreeInstance ti in td.treeInstances) s.Add(Chave(Mathf.FloorToInt((ti.position.x * sz.x + o.x) / 4f), Mathf.FloorToInt((ti.position.z * sz.z + o.z) / 4f)));
        return s;
    }

    static readonly string[] LAYERS_RUINS = { "estrada", "caminho", "rua", "barro", "lama", "calc", "pavi", "pedra", "rocha", "areia", "lago", "agua", "road", "path", "mud", "paving", "rock", "sand", "stone", "cascalho", "gravel", "pebble" };

    static bool PontoBom(Terrain t, HashSet<long> arvores, float x, float z, float folgaArvore, out Vector3 p)
    {
        p = Vector3.zero; TerrainData td = t.terrainData; Vector3 o = t.transform.position, sz = td.size;
        if (x < o.x + 30f || z < o.z + 30f || x > o.x + sz.x - 30f || z > o.z + sz.z - 30f) return false;
        float nx = (x - o.x) / sz.x, nz = (z - o.z) / sz.z;
        if (td.GetSteepness(nx, nz) > 11f) return false;
        float y = td.GetInterpolatedHeight(nx, nz) + o.y; if (td.GetInterpolatedHeight(nx, nz) < 1.2f) return false;
        if (Vector2.Distance(new Vector2(x, z), new Vector2(190f, 500f)) < 75f) return false;              // lago do Pântano
        int ix = Mathf.FloorToInt(x / 4f), iz = Mathf.FloorToInt(z / 4f); int r = Mathf.CeilToInt(folgaArvore / 4f);
        for (int dx = -r; dx <= r; dx++) for (int dz = -r; dz <= r; dz++) if (arvores.Contains(Chave(ix + dx, iz + dz))) return false;
        // chão: não nasce em estrada, barro, pedra...
        int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1), az = Mathf.Clamp(Mathf.RoundToInt(nz * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);
        float[,,] w = td.GetAlphamaps(ax, az, 1, 1); int melhor = 0;
        for (int i = 1; i < w.GetLength(2); i++) if (w[0, 0, i] > w[0, 0, melhor]) melhor = i;
        TerrainLayer[] camadas = td.terrainLayers;
        if (camadas != null && melhor < camadas.Length && camadas[melhor] != null)
        {
            string n = camadas[melhor].name.ToLowerInvariant();
            foreach (string ruim in LAYERS_RUINS) if (n.Contains(ruim)) return false;
        }
        // nada sólido por perto (casa, muro, pedra grande)
        foreach (Collider c in Physics.OverlapSphere(new Vector3(x, y + 1.2f, z), 2.5f, ~0, QueryTriggerInteraction.Ignore)) if (!(c is TerrainCollider)) return false;
        p = new Vector3(x, y, z); return true;
    }

    static int Rebanhos(Terrain t, Vector3 vila, HashSet<long> arvores, List<Vector3> usados, Transform grupo, Tipo a, Tipo b, int rebanhos, int min, int max)
    {
        int total = 0, alt = 0;
        for (int h = 0; h < rebanhos; h++)
        {
            Vector3 centro = Vector3.zero; bool achou = false;
            for (int tent = 0; tent < 800 && !achou; tent++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(45f, 430f);
                Vector3 p;
                if (!PontoBom(t, arvores, vila.x + Mathf.Cos(ang) * r, vila.z + Mathf.Sin(ang) * r, 12f, out p)) continue;
                bool longe = true; foreach (Vector3 u in usados) if ((u - p).sqrMagnitude < 45f * 45f) { longe = false; break; }
                if (!longe) continue;
                centro = p; achou = true;
            }
            if (!achou) continue;
            usados.Add(centro);
            int n = Random.Range(min, max + 1);
            for (int i = 0; i < n; i++)
            {
                for (int tent = 0; tent < 20; tent++)
                {
                    Vector2 d = Random.insideUnitCircle * 7f; Vector3 p;
                    if (!PontoBom(t, arvores, centro.x + d.x, centro.z + d.y, 4f, out p)) continue;
                    Tipo tipo = (b != null && alt++ % 2 == 1) ? b : a;
                    if (Colocar(tipo, p, grupo) != null) total++;
                    break;
                }
            }
        }
        return total;
    }
}
