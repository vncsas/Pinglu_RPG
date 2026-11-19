using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 78. Ambiente da água do lago
//  - troca a água do lago pelo shader "Pinglu/AguaViva" (ondinhas, ondas onde se pisa, visível por baixo);
//  - cria as imagens/materiais das gotas e bolhas (Assets/Resources/Agua);
//  - põe o script AmbienteDaAgua no lago: andar devagar na água, NADAR onde é fundo, respingos, som,
//    e tela esverdeada com névoa e bolhas quando a câmera mergulha.
//  Precisa da cena com o lago (SampleScene_Ambientacao). Pode rodar de novo.
// ==========================================================================================
public static class PingluAgua
{
    const string PASTA = "Assets/Resources/Agua";

    [MenuItem("Pinglu/78. Ambiente da água do lago")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        GameObject lago = GameObject.Find("Lago do Pantano");
        Transform agua = lago != null ? lago.transform.Find("Agua") : null;
        Renderer ra = agua != null ? agua.GetComponent<Renderer>() : null;
        if (ra == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o \"Lago do Pantano\" na cena aberta.\nAbra a cena SampleScene_Ambientacao e rode de novo.", "OK"); return; }

        Shader viva = Shader.Find("Pinglu/AguaViva");
        if (viva == null) { EditorUtility.DisplayDialog("Pinglu", "O shader Pinglu/AguaViva ainda não foi compilado. Espere o Unity terminar de importar e rode de novo (veja o Console por erros do shader).", "OK"); return; }

        Directory.CreateDirectory(PASTA);
        // 1) material da água
        Material m = ra.sharedMaterial;
        if (m != null && m.shader != viva)
        {
            Color rasa = m.HasProperty("_ShallowColor") ? m.GetColor("_ShallowColor") : new Color(0.24f, 0.27f, 0.12f, 0.92f);
            Color funda = m.HasProperty("_DeepColor") ? m.GetColor("_DeepColor") : new Color(0.07f, 0.09f, 0.045f, 0.995f);
            Texture normal = m.HasProperty("_NormalMap") ? m.GetTexture("_NormalMap") : null;
            float escala = m.HasProperty("_NormalScale") ? m.GetFloat("_NormalScale") : 0.05f;
            float prof = m.HasProperty("_DepthDistance") ? m.GetFloat("_DepthDistance") : 1.1f;
            m.shader = viva;
            m.SetColor("_ShallowColor", rasa); m.SetColor("_DeepColor", funda); m.SetTexture("_NormalMap", normal);
            m.SetFloat("_NormalScale", escala); m.SetFloat("_DepthDistance", prof);
            m.SetFloat("_WaveSpeed", 0.12f); m.SetFloat("_Ondulacao", 0.07f);
            m.SetColor("_UnderColor", new Color(0.08f, 0.24f, 0.17f, 0.9f));
            EditorUtility.SetDirty(m);
        }
        if (m != null) { m.SetFloat("_WaveSpeed", 0.12f); }

        // 2) círculo macio das gotas/bolhas
        string png = PASTA + "/Gota.png";
        if (!File.Exists(png))
        {
            int n = 64; Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                float al = Mathf.Clamp01(1f - d); al = al * al * (3f - 2f * al);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, al));
            }
            File.WriteAllBytes(png, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(png);
        }
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        Material gota = AssetDatabase.LoadAssetAtPath<Material>(PASTA + "/Gota.mat");
        if (gota == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (sh == null) sh = Shader.Find("Sprites/Default");
            gota = new Material(sh); AssetDatabase.CreateAsset(gota, PASTA + "/Gota.mat");
        }
        if (gota.shader.name.StartsWith("Universal"))
        {
            gota.SetTexture("_BaseMap", tex); gota.SetColor("_BaseColor", Color.white);
            gota.SetFloat("_Surface", 1f); gota.SetFloat("_Blend", 0f); gota.SetFloat("_ZWrite", 0f); gota.SetFloat("_Cull", 0f);
            gota.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); gota.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            gota.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); gota.SetOverrideTag("RenderType", "Transparent"); gota.renderQueue = 3000;
        }
        else gota.mainTexture = tex;
        EditorUtility.SetDirty(gota);

        // 3) o script no lago
        var cena = EditorSceneManager.GetActiveScene();
        AmbienteDaAgua a = lago.GetComponent<AmbienteDaAgua>(); if (a == null) a = lago.AddComponent<AmbienteDaAgua>();
        a.superficie = ra; a.materialGota = gota;
        EditorUtility.SetDirty(lago);

        // a luz precisa gerar a textura de profundidade para a água (o colega já liga, conferimos)
        string aviso = "";
        var urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp != null && !urp.supportsCameraDepthTexture) { var so = new SerializedObject(urp); var p = so.FindProperty("m_RequireDepthTexture"); if (p != null) { p.boolValue = true; so.ApplyModifiedProperties(); aviso = "\n(Liguei a Depth Texture do URP, que a água precisa.)"; } }

        EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Pinglu", "Ambiente da água pronto!\n\n• água com ondinhas e ondas onde se pisa\n• andar na água fica mais lento; onde é fundo o personagem NADA\n• respingos com som e som de água no lago\n• tela esverdeada com névoa e bolhas ao mergulhar a câmera\n\nDê Play e entre no lago." + aviso, "OK");
    }
}
