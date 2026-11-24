#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using System.IO;
using UnityEngine;
using UnityEditor.SceneManagement;

// Instala o GRANITO (golem de pedra, pasta Assets/Personagens/GolemRocha) como o visual do Tanque.
//  1. roda o "Golem Rocha > Montar tudo" do amigo se o prefab ainda não existe (Generic, materiais, clipes)
//  2. cria Granito_Skin.prefab: o golem sem colisor/Animator/demo + SkinProceduralGranito (copia o movimento do corpo antigo do Tanque)
//  3. põe a skin no Jogador_Tanque (esconde o corpo antigo, tira a skin do Auric) e na ficha da seleção (nome Granito)
// Menu: Pinglu > Granito (golem) como Tanque.  Sem editor aberto: Unity -batchmode -executeMethod PingluGranito.Executar
public static class PingluGranito
{
    const string PASTA = "Assets/Personagens/GolemRocha";
    const string PREFAB_GOLEM = PASTA + "/GolemRocha.prefab";
    const string PREFAB_SKIN = PASTA + "/Granito_Skin.prefab";
    const string PREFAB_JOGADOR = "Assets/Personagens/Jogadores/Jogador_Tanque.prefab";
    const string FICHA = "Assets/Personagens/Jogadores/Fichas/3_Personagem3.asset";

    [MenuItem("Pinglu/Granito (golem) como Tanque")]
    public static void Executar()
    {
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_GOLEM) == null) MontarGolemRocha.Montar();
        GameObject golem = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_GOLEM);
        if (golem == null) { Debug.LogError("[Granito] não consegui montar o prefab do golem."); return; }

        // 2) skin
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(golem); g.name = "Granito";
        foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        Transform braços = g.transform.Find("Colisor_Bracos"); if (braços != null) Object.DestroyImmediate(braços.gameObject);
        Rigidbody rb = g.GetComponent<Rigidbody>(); if (rb != null) Object.DestroyImmediate(rb);
        foreach (MonoBehaviour m in g.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(m);       // GolemRochaDemo
        // animações próprias do golem: um estado por clipe, sem transições (o script SkinProceduralGranito escolhe qual tocar)
        string fbx = PASTA + "/GolemRocha.fbx";
        Animator an = g.GetComponent<Animator>(); if (an == null) an = g.AddComponent<Animator>();
        an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Avatar av = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault(); if (av != null) an.avatar = av;
        UnityEditor.Animations.AnimatorController ctl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(PASTA + "/Granito.controller");
        foreach (AnimationClip c in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
            ctl.layers[0].stateMachine.AddState(c.name).motion = c;
        an.runtimeAnimatorController = ctl;
        Debug.Log("[Granito] controlador com " + ctl.layers[0].stateMachine.states.Length + " estados, avatar " + (av != null ? av.name : "NÃO ACHADO"));
        g.AddComponent<SkinProceduralGranito>();
        GameObject skin = PrefabUtility.SaveAsPrefabAsset(g, PREFAB_SKIN); Object.DestroyImmediate(g);
        Debug.Log("[Granito] skin criada: " + PREFAB_SKIN);

        // 3) jogador
        GameObject raiz = PrefabUtility.LoadPrefabContents(PREFAB_JOGADOR);
        try
        {
            foreach (string velha in new[] { "Skin Auric", "Skin Granito" }) { Transform t = raiz.transform.Find(velha); if (t != null) Object.DestroyImmediate(t.gameObject); }
            int escondidos = 0;
            foreach (SkinnedMeshRenderer s in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { s.enabled = false; escondidos++; }
            GameObject n = (GameObject)PrefabUtility.InstantiatePrefab(skin, raiz.transform);
            n.name = "Skin Granito"; n.transform.localPosition = Vector3.zero; n.transform.localRotation = Quaternion.identity; n.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB_JOGADOR);
            Debug.Log("[Granito] Jogador_Tanque atualizado (" + escondidos + " partes do corpo antigo escondidas).");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }

        // ficha da seleção
        FichaPersonagem f = AssetDatabase.LoadAssetAtPath<FichaPersonagem>(FICHA);
        if (f != null) { f.nome = "Granito"; f.titulo = "O Colosso de Pedra"; f.modeloVisual = skin; EditorUtility.SetDirty(f); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Granito] pronto: ficha " + (f != null ? "atualizada" : "NÃO ACHADA"));
    }

    // fotos de teste da postura de gorila (sem entrar no jogo): Unity -batchmode -executeMethod PingluGranito.Vitrine  (sem -nographics)
    // grava em ~/.cache/pinglu_audio/gt/vit_<estado>_<quadro>_<lado|frente>.png
    public static void Vitrine()
    {
        string saida = Path.Combine(System.Environment.GetEnvironmentVariable("HOME"), ".cache/pinglu_audio/gt"); Directory.CreateDirectory(saida);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject luz = new GameObject("Sol"); Light l = luz.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 2.2f; luz.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.9f, 0.9f, 0.95f);
        GameObject chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.transform.localScale = Vector3.one * 2f;
        GameObject skin = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_SKIN);
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(skin); g.transform.position = Vector3.zero;
        Animator an = g.GetComponent<Animator>(); SkinProceduralGranito sk = g.GetComponent<SkinProceduralGranito>();
        foreach (SkinnedMeshRenderer smr in g.GetComponentsInChildren<SkinnedMeshRenderer>()) { smr.forceMatrixRecalculationPerRender = true; smr.updateWhenOffscreen = true; }
        GameObject gc = new GameObject("Cam"); Camera cam = gc.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.22f, 0.28f); cam.orthographic = true; cam.orthographicSize = 1.9f; cam.enabled = false;
        RenderTexture rt = new RenderTexture(640, 640, 24); cam.targetTexture = rt;
        string ep = System.Environment.GetEnvironmentVariable("GRANITO_P");                                  // ajuste rápido: 11 números separados por vírgula, na ordem dos campos
        if (!string.IsNullOrEmpty(ep))
        {
            string[] n = ep.Split(','); float[] v = new float[n.Length]; for (int i = 0; i < n.Length; i++) float.TryParse(n[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i]);
            if (v.Length >= 11) { sk.inclinaCintura = v[0]; sk.inclinaColuna1 = v[1]; sk.inclinaColuna2 = v[2]; sk.cabecaPescoco = v[3]; sk.cabecaCabeca = v[4]; sk.bracosFrente = v[5]; sk.balancoBracos = v[6]; sk.coxaFrente = v[7]; sk.canelaTras = v[8]; sk.peAjuste = v[9]; sk.baixaQuadril = v[10]; }
        }
        string[] estados = { "Idle", "Walk", "Run" }; float[] quads = { 0.5f, 1f, 1f }; float[] corr = { 0f, 0f, 1f };
        for (int e = 0; e < estados.Length; e++)
            for (int q = 0; q < 4; q++)
                for (int lado = 0; lado < 2; lado++)
                {
                    sk.Desfazer(); an.Play(estados[e], 0, q * 0.25f); an.Update(0f); an.Update(0.0001f);
                    sk.AplicarPostura(quads[e] * (e == 0 && q == 3 ? 0f : 1f), corr[e]);          // Idle quadro 3 = sem postura (referência)
                    if (lado == 0) { gc.transform.position = new Vector3(8f, 1.5f, 0f); gc.transform.rotation = Quaternion.Euler(0f, -90f, 0f); }    // perfil (o golem olha para +Z)
                    else { gc.transform.position = new Vector3(0f, 1.5f, 8f); gc.transform.rotation = Quaternion.Euler(0f, 180f, 0f); }                 // de frente
                    Transform sp = g.GetComponentsInChildren<Transform>().First(x => x.name == "CC_Base_Spine01"); Debug.Log("[Granito] " + estados[e] + " q" + q + " lado" + lado + " spine01 euler " + sp.localEulerAngles.ToString("F1") + " world fwd " + sp.up.ToString("F2"));
                    cam.Render();
                    RenderTexture.active = rt; Texture2D t = new Texture2D(640, 640, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); t.Apply(); RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(saida, "vit_" + estados[e] + "_" + q + "_" + (lado == 0 ? "lado" : "frente") + ".png"), t.EncodeToPNG()); Object.DestroyImmediate(t);
                }
        // retrato do rosto (como a seleção e a barra de vida enquadram): cabeça + folga, câmera de frente
        sk.Desfazer(); an.Play("Idle", 0, 0.2f); an.Update(0f); an.Update(0.0001f); sk.AplicarPostura(0.5f, 0f);
        Transform cab = g.GetComponentsInChildren<Transform>().First(x => x.name == "CC_Base_Head");
        cam.orthographic = false; cam.fieldOfView = 26f; cam.nearClipPlane = 0.05f; cam.backgroundColor = new Color(0.09f, 0.07f, 0.05f);
        Vector3 fr = g.transform.forward; Vector3 alvo = cab.position + Vector3.up * 0.38f + fr * 0.15f;
        cam.transform.position = alvo + fr * 1.55f * 1.4f + g.transform.right * -0.2f * 1.4f + Vector3.up * 0.06f; cam.transform.LookAt(alvo - Vector3.up * 0.18f);
        cam.Render(); RenderTexture.active = rt; Texture2D tr = new Texture2D(640, 640, TextureFormat.RGB24, false); tr.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tr.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(saida, "vit_rosto.png"), tr.EncodeToPNG()); Debug.Log("[Granito] cabeça em " + alvo.ToString("F2"));
        Debug.Log("[Granito] vitrine gravada em " + saida);
    }
}
#endif
