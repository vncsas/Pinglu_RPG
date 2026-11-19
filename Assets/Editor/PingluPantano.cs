using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 77. Pântano: Devorador do Lago e monstros da lama
//  Usa os modelos com esqueleto do colega (Monstro e Angler) para montar:
//   - o chefe do Pântano (Angler): sobe do lago em cena de câmera, 5 ataques diferentes, 2ª fase;
//   - os monstros da lama (Monstro): ficam enterrados na beira do lago e saem de repente, com bote.
//  Precisa da cena com o lago do colega aberta (SampleScene_Ambientacao). Pode rodar de novo (refaz os grupos).
// ==========================================================================================
public static class PingluPantano
{
    const int MONSTROS = 12;

    [MenuItem("Pinglu/77. Pântano: Devorador do Lago e monstros da lama")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        GameObject lago = GameObject.Find("Lago do Pantano");
        Transform agua = lago != null ? lago.transform.Find("Agua") : null;
        Renderer ra = agua != null ? agua.GetComponent<Renderer>() : null;
        if (ra == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o \"Lago do Pantano\" na cena aberta.\nAbra a cena SampleScene_Ambientacao (a do terreno novo) e rode de novo.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou montar o Devorador do Lago (chefe que sobe da água) e " + MONSTROS + " monstros da lama enterrados na beira do lago, usando os modelos do seu colega.\n\nPode demorar um pouco. Uma cópia da cena fica em Backups.", "Continuar", "Cancelar")) return;

        var log = new StringBuilder();
        try
        {
            EditorUtility.DisplayProgressBar("Pântano", "Preparando os efeitos...", 0.1f);
            PingluHabilidades.ConverterMateriais(log);

            EditorUtility.DisplayProgressBar("Pântano", "Montando o Monstro...", 0.25f);
            string pm = Montar("Monstro", "MontarMonstro", log); if (pm == null) return;
            EditorUtility.DisplayProgressBar("Pântano", "Montando o Angler...", 0.45f);
            string pa = Montar("Angler", "MontarAngler", log); if (pa == null) return;

            GameObject prefabMonstro = CriarMonstro(pm, log);
            GameObject prefabAngler = CriarAngler(pa, prefabMonstro, log);
            if (prefabMonstro == null || prefabAngler == null) { EditorUtility.DisplayDialog("Pinglu", "Não consegui criar os prefabs:\n" + log, "OK"); return; }

            // cópia da cena
            var cena = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(cena.path) && File.Exists(cena.path))
            {
                Directory.CreateDirectory("Backups");
                string copia = "Backups/" + Path.GetFileNameWithoutExtension(cena.path) + "_antes_do_pantano.unity";
                if (!File.Exists(copia)) File.Copy(cena.path, copia, false);
            }

            EditorUtility.DisplayProgressBar("Pântano", "Colocando na cena...", 0.8f);
            foreach (string nome in new[] { "Encontro do Pantano", "Monstros do Pantano" }) { GameObject velho = GameObject.Find(nome); if (velho != null) Object.DestroyImmediate(velho); }

            GameObject enc = new GameObject("Encontro do Pantano");
            EncontroDoPantano e = enc.AddComponent<EncontroDoPantano>(); e.prefabAngler = prefabAngler;
            Undo.RegisterCreatedObjectUndo(enc, "Encontro do Pantano");

            Bounds b = ra.bounds; Vector3 centro = b.center; float nivel = b.center.y, raioAgua = Mathf.Min(b.extents.x, b.extents.z) * 0.9f;
            GameObject grupo = new GameObject("Monstros do Pantano"); Undo.RegisterCreatedObjectUndo(grupo, "Monstros do Pantano");
            int n = ColocarMonstros(prefabMonstro, grupo.transform, centro, nivel, raioAgua);

            EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            string msg = "Pronto!\n\n• Devorador do Lago: acorda quando o grupo chega perto do lago na etapa do Pântano da história\n• " + n + " monstros da lama enterrados em volta do lago\n\nPara testar já: Play e aperte F5 (vai direto para a beira do lago na etapa do Pântano).";
            if (log.Length > 0) msg += "\n\nAvisos:\n" + log;
            Debug.Log("[Pântano]\n" + msg);
            EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // roda o "Montar tudo" do colega (cria material, prefab e colisores) se o prefab ainda não existe
    static string Montar(string pasta, string tipo, StringBuilder log)
    {
        string dir = "Assets/Personagens/" + pasta;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + pasta + ".prefab") == null)
        {
            System.Type t = typeof(PingluPantano).Assembly.GetType(tipo);
            if (t == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o script " + tipo + ". Espere o Unity terminar de importar e rode de novo.", "OK"); return null; }
            try { t.GetMethod("Montar", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(null, null); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            catch (System.Exception ex) { log.AppendLine("- " + pasta + ": erro ao montar (" + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")"); }
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + pasta + ".prefab") == null) { EditorUtility.DisplayDialog("Pinglu", "Não consegui montar o " + pasta + ":\n" + log, "OK"); return null; }
        return dir;
    }

    static AnimationClip Clipe(string fbx, string nome)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == nome && !c.name.StartsWith("__preview__"));
    }

    static GameObject Efeito(string nome, StringBuilder log)
    {
        foreach (string g in AssetDatabase.FindAssets(nome + " t:Prefab", new[] { "Assets/Ataques" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == nome) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        log.AppendLine("- não achei o efeito \"" + nome + "\"");
        return null;
    }

    static GameObject Preparar(string prefabOriginal, System.Action<GameObject> aoMontar, string destino)
    {
        GameObject raiz = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabOriginal));
        foreach (MonoBehaviour m in raiz.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null && m.GetType().Name.EndsWith("Demo")) Object.DestroyImmediate(m);
        PrefabUtility.UnpackPrefabInstance(raiz, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        aoMontar(raiz);
        GameObject salvo = PrefabUtility.SaveAsPrefabAsset(raiz, destino);
        Object.DestroyImmediate(raiz);
        return salvo;
    }

    static GameObject CriarMonstro(string pasta, StringBuilder log)
    {
        string fbx = pasta + "/Monstro.fbx";
        return Preparar(pasta + "/Monstro.prefab", g =>
        {
            Vida v = g.GetComponent<Vida>(); if (v == null) g.AddComponent<Vida>();
            g.AddComponent<MisturadorDeClipes>();
            MonstroDoPantano m = g.AddComponent<MonstroDoPantano>();
            m.parado = Clipe(fbx, "Idle"); m.andar = Clipe(fbx, "Walk"); m.correr = Clipe(fbx, "Run"); m.atacar = Clipe(fbx, "Attack"); m.morrer = Clipe(fbx, "Death");
            m.efeitoLama = Efeito("Dust puff", log);
            if (m.parado == null || m.atacar == null) log.AppendLine("- Monstro: faltam clipes no FBX.");
        }, pasta + "/Monstro_Jogo.prefab");
    }

    static GameObject CriarAngler(string pasta, GameObject prefabMonstro, StringBuilder log)
    {
        string fbx = pasta + "/Angler.fbx";
        return Preparar(pasta + "/Angler.prefab", g =>
        {
            if (g.GetComponent<Vida>() == null) g.AddComponent<Vida>();
            g.AddComponent<MisturadorDeClipes>();
            ChefeAngler c = g.AddComponent<ChefeAngler>();
            c.parado = Clipe(fbx, "Idle"); c.andar = Clipe(fbx, "Walk"); c.correr = Clipe(fbx, "Run");
            c.mordida = Clipe(fbx, "Attack_Mordida"); c.garras = Clipe(fbx, "Attack_Garras"); c.cauda = Clipe(fbx, "Attack_Cauda"); c.salto = Clipe(fbx, "Attack_Salto");
            c.rugido = Clipe(fbx, "Rugido"); c.dano = Clipe(fbx, "Dano"); c.morte = Clipe(fbx, "Morte");
            c.efeitoRespingo = Efeito("Smoke AOE explosion", log); c.efeitoPoeira = Efeito("Dust ground", log); c.efeitoRugido = Efeito("Red energy explosion", log);
            c.prefabMonstro = prefabMonstro;
            if (c.mordida == null || c.salto == null || c.morte == null) log.AppendLine("- Angler: faltam clipes no FBX.");
        }, pasta + "/Angler_Jogo.prefab");
    }

    // monstros enterrados na beira do lago (terreno firme, fora da água, sem árvores/prédios em cima)
    static int ColocarMonstros(GameObject prefab, Transform grupo, Vector3 centro, float nivel, float raioAgua)
    {
        Terrain t = Terrain.activeTerrain; if (t == null) return 0;
        float baseY = t.transform.position.y; int n = 0;
        var rnd = new System.Random(77);
        for (int tent = 0; tent < 4000 && n < MONSTROS; tent++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = raioAgua + 6f + (float)rnd.NextDouble() * 40f;
            Vector3 p = centro + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
            if (p.x < 20f || p.z < 20f || p.x > t.terrainData.size.x - 20f || p.z > t.terrainData.size.z - 20f) continue;
            float h = t.SampleHeight(p) + baseY;
            if (h < nivel + 0.4f) continue;
            if (t.terrainData.GetSteepness(p.x / t.terrainData.size.x, p.z / t.terrainData.size.z) > 18f) continue;
            if (Physics.OverlapSphere(new Vector3(p.x, h + 1f, p.z), 2f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider))) continue;
            bool perto = false; foreach (Transform o in grupo) if ((o.position - new Vector3(p.x, h, p.z)).sqrMagnitude < 14f * 14f) { perto = true; break; }
            if (perto) continue;
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
            g.name = "Monstro da Lama";
            g.transform.SetPositionAndRotation(new Vector3(p.x, h, p.z), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f));
            Undo.RegisterCreatedObjectUndo(g, "Monstro da lama"); n++;
        }
        return n;
    }
}
