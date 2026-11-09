using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 70. Retratos dos diálogos   (Play desligado)
//  Tira uma foto do rosto de cada personagem (o próprio modelo 3D) e salva em
//  Assets/Resources/Retratos/<nome>.png. O balão de conversa usa essas fotos no lugar do desenho.
//  Personagens: os 4 jogáveis (das fichas), o Cavaleiro Antigo, o Ladrão (por enquanto o mesmo modelo
//  do cavaleiro, com luz roxa) e o Caio (se ele estiver na cena aberta).
//  Se alguma foto não ficar boa, apague o arquivo dela em Resources/Retratos: volta o desenho.
// ==========================================================================================
public static class PingluRetratos
{
    const string PASTA = "Assets/Resources/Retratos";
    const string MOLDE_CAVALEIRO = "Assets/Personagens/Cavaleiro/Cavaleiro_MiniBoss.prefab";
    const int TAM = 256;

    class Item
    {
        public string arquivo; public GameObject modelo; public AnimationClip pose;
        public Color fundo, luz1, luz2; public float giro = 18f;
        public float zoom = 1f, subir = 0f;            // enquadramento com cabeça: distância (menor = mais perto) e altura extra
        public float topo = 0.14f, distTam = 0.75f;    // sem esqueleto humano: quanto abaixo do topo mirar e distância (fração da altura)
    }

    [MenuItem("Pinglu/70. Retratos dos diálogos")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        Directory.CreateDirectory(PASTA);
        List<Item> itens = new List<Item>();

        foreach (string g in AssetDatabase.FindAssets("t:FichaPersonagem"))
        {
            FichaPersonagem f = AssetDatabase.LoadAssetAtPath<FichaPersonagem>(AssetDatabase.GUIDToAssetPath(g));
            if (f == null || f.modeloVisual == null) continue;
            Color c = f.corDestaque;
            itens.Add(new Item { arquivo = Chave(f.nome), modelo = f.modeloVisual, pose = f.animacaoParado,
                fundo = new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f), luz1 = Color.Lerp(Color.white, c, 0.3f), luz2 = c });
        }

        // cada personagem tem o seu enquadramento
        foreach (Item it in itens)
        {
            switch (it.arquivo)
            {
                case "cavaleirosombrio": it.zoom = 0.92f; it.subir = 0.02f; it.giro = 24f; break;        // elmo e capuz, busto
                case "morgana": it.zoom = 0.78f; it.subir = 0.0f; it.giro = -12f; break;                 // rosto de perto
                case "curandeiro": it.zoom = 0.82f; it.subir = 0.03f; it.giro = 15f; break;              // caveira
                case "tanque": it.topo = 0.30f; it.distTam = 0.52f; it.giro = 20f; break;                // elmo com chifres
            }
        }

        GameObject cav = AssetDatabase.LoadAssetAtPath<GameObject>(MOLDE_CAVALEIRO);
        if (cav != null)
        {
            itens.Add(new Item { arquivo = "cavaleiro", modelo = cav, fundo = new Color(0.1f, 0.12f, 0.16f), luz1 = new Color(0.85f, 0.9f, 1f), luz2 = new Color(1f, 0.25f, 0.2f) });
            itens.Add(new Item { arquivo = "ladrao", modelo = cav, fundo = new Color(0.09f, 0.03f, 0.14f), luz1 = new Color(0.55f, 0.35f, 0.9f), luz2 = new Color(0.8f, 0.3f, 1f), giro = -22f });
        }
        GameObject caio = GameObject.Find("Caio");
        if (caio != null) itens.Add(new Item { arquivo = "caio", modelo = caio, fundo = new Color(0.2f, 0.14f, 0.08f), luz1 = new Color(1f, 0.92f, 0.75f), luz2 = new Color(1f, 0.7f, 0.3f), giro = 180f });

        List<string> feitos = new List<string>(), falhas = new List<string>();
        foreach (Item it in itens)
        {
            try
            {
                Texture2D t = Tirar(it);
                string caminho = PASTA + "/" + it.arquivo + ".png";
                File.WriteAllBytes(caminho, t.EncodeToPNG());
                AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
                TextureImporter imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
                if (imp != null) { imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.alphaSource = TextureImporterAlphaSource.None; imp.SaveAndReimport(); }
                Object.DestroyImmediate(t);
                feitos.Add(it.arquivo);
            }
            catch (System.Exception e) { falhas.Add(it.arquivo + ": " + e.Message); Debug.LogWarning("[Retratos] " + it.arquivo + ": " + e); }
        }
        AssetDatabase.Refresh();
        string msg = "Retratos salvos em " + PASTA + ":\n- " + string.Join("\n- ", feitos);
        if (caio == null) msg += "\n\n(O Caio não está na cena aberta: abra a cena do jogo e rode de novo para tirar a foto dele. Enquanto isso fica o desenho.)";
        if (falhas.Count > 0) msg += "\n\nFalharam:\n- " + string.Join("\n- ", falhas);
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
    }

    static string Chave(string nome)
    {
        string q = nome.ToLowerInvariant().Replace(" ", "");
        return q.Replace("ã", "a").Replace("á", "a").Replace("é", "e").Replace("ô", "o").Replace("ç", "c").Replace("í", "i").Replace("ú", "u");
    }

    static Texture2D Tirar(Item it)
    {
        PreviewRenderUtility pru = new PreviewRenderUtility();
        GameObject go = null;
        try
        {
            go = Object.Instantiate(it.modelo);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, it.giro, 0f));
            go.SetActive(true);
            pru.AddSingleGO(go);

            Animator a = go.GetComponentInChildren<Animator>();
            if (it.pose != null)
            {
                try { it.pose.SampleAnimation(a != null ? a.gameObject : go, Mathf.Min(0.4f, it.pose.length)); } catch { }
            }

            Bounds b = new Bounds(go.transform.position, Vector3.zero); bool tem = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
            }
            Transform cabeca = (a != null && a.isHuman) ? a.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 alvo; float dist;
            if (cabeca != null) { alvo = cabeca.position + Vector3.up * (0.03f + it.subir); dist = 1.25f * it.zoom; }
            else { alvo = new Vector3(b.center.x, b.max.y - b.size.y * it.topo, b.center.z); dist = Mathf.Max(0.8f, b.size.y * it.distTam); }

            Camera cam = pru.camera;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(it.fundo.r, it.fundo.g, it.fundo.b, 1f);
            cam.fieldOfView = 26f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
            Vector3 frente = go.transform.forward;
            cam.transform.position = alvo + frente * dist + go.transform.right * -0.12f * dist + Vector3.up * 0.05f;
            cam.transform.LookAt(alvo + Vector3.down * 0.1f * dist);

            pru.lights[0].transform.rotation = Quaternion.Euler(28f, -40f, 0f); pru.lights[0].intensity = 1.5f; pru.lights[0].color = it.luz1;
            pru.lights[1].transform.rotation = Quaternion.Euler(-15f, 150f, 0f); pru.lights[1].intensity = 1.4f; pru.lights[1].color = it.luz2;
            pru.ambientColor = new Color(it.fundo.r * 2.5f + 0.12f, it.fundo.g * 2.5f + 0.12f, it.fundo.b * 2.5f + 0.12f);

            pru.BeginStaticPreview(new Rect(0, 0, TAM, TAM));
            cam.Render();
            Texture2D t = pru.EndStaticPreview();
            return t;
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            pru.Cleanup();
        }
    }
}
