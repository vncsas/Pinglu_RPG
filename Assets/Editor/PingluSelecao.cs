// =====================================================================
//  PingluSelecao.cs  -  Tela de seleção de personagens
// ---------------------------------------------------------------------
//  Prepara o palco 3D da seleção (cenário pintado, chão de pedra,
//  brasas) e preenche a ficha do Cavaleiro Sombrio para o palco.
//
//  COMO USAR:  Pinglu > 23. Tela de seleção
// =====================================================================
#if UNITY_EDITOR && PINGLU_REDE
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PingluSelecao
{
    const string PASTA = "Assets/Cenario/Selecao";
    const string FUNDO = PASTA + "/fundo_selecao.png";
    const string PEDRA = "Assets/Cenario/Vila/Texturas/pedra_escura_cor.png";
    const string PEDRA_N = "Assets/Cenario/Vila/Texturas/pedra_escura_normal.png";
    const string PARTICULA = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    const string SOMBRIO = "Assets/Personagens/CavaleiroSombrio";

    [MenuItem("Pinglu/23. Tela de seleção")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        MenuRede menu = Object.FindFirstObjectByType<MenuRede>();
        if (menu == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o menu inicial na cena. Rode antes o Pinglu > 15.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(FUNDO) == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei " + FUNDO + ". Extraiu o zip na pasta do projeto?", "OK"); return; }

        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(FUNDO);
        if (ti.maxTextureSize < 2048 || ti.wrapMode != TextureWrapMode.Clamp) { ti.maxTextureSize = 2048; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }

        // cenário pintado (sem luz, aparece com as cores da pintura)
        Material fundo = Mat(PASTA + "/Fundo_Selecao.mat", "Universal Render Pipeline/Unlit");
        fundo.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FUNDO));
        fundo.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f));
        EditorUtility.SetDirty(fundo);

        // chão de pedra escura
        Material chao = Mat(PASTA + "/Chao_Selecao.mat", "Universal Render Pipeline/Lit");
        Texture2D pedra = AssetDatabase.LoadAssetAtPath<Texture2D>(PEDRA);
        if (pedra != null) { chao.SetTexture("_BaseMap", pedra); chao.SetTextureScale("_BaseMap", new Vector2(3f, 3f)); }
        Texture2D pn = AssetDatabase.LoadAssetAtPath<Texture2D>(PEDRA_N);
        if (pn != null) { chao.SetTexture("_BumpMap", pn); chao.EnableKeyword("_NORMALMAP"); }
        chao.SetColor("_BaseColor", new Color(0.45f, 0.42f, 0.42f));
        chao.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(chao);

        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("materialFundo").objectReferenceValue = fundo;
        so.FindProperty("materialChao").objectReferenceValue = chao;
        so.FindProperty("materialParticula").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(PARTICULA);
        so.ApplyModifiedProperties();

        // ficha do Cavaleiro Sombrio
        string resumo = "";
        SerializedProperty lista = so.FindProperty("personagens");
        for (int i = 0; i < lista.arraySize; i++)
        {
            FichaPersonagem f = lista.GetArrayElementAtIndex(i).objectReferenceValue as FichaPersonagem;
            if (f == null || !f.nome.ToLower().Contains("sombrio")) continue;
            f.modeloVisual = AssetDatabase.LoadAssetAtPath<GameObject>(SOMBRIO + "/cavaleiro_sombrio_rigged.fbx");
            f.animacaoParado = Clipe(SOMBRIO + "/animacoes/cavaleiro_sombrio@Parado.fbx");
            f.animacaoEntrada = Clipe(SOMBRIO + "/animacoes/cavaleiro_sombrio@Ataque_Pesado.fbx");
            if (string.IsNullOrEmpty(f.titulo)) f.titulo = "A Lâmina do Crepúsculo";
            f.forca = 9; f.defesa = 7; f.velocidade = 4; f.alcance = 9;
            EditorUtility.SetDirty(f);
            resumo = "\n\nFicha do " + f.nome + ": modelo " + (f.modeloVisual != null ? "ok" : "NÃO ACHADO") +
                     ", animação parada " + (f.animacaoParado != null ? "ok" : "NÃO ACHADA") +
                     ", golpe de apresentação " + (f.animacaoEntrada != null ? "ok" : "NÃO ACHADO") + ".";
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorUtility.DisplayDialog("Pinglu", "Tela de seleção pronta!" + resumo +
            "\n\nPara os próximos personagens, preencha na ficha deles: Titulo, Atributos, Modelo Visual (o .fbx), Animacao Parado e Animacao Entrada." +
            "\n\nSalve a cena com Ctrl + S.", "OK");
    }

    static Material Mat(string caminho, string shader)
    {
        if (!AssetDatabase.IsValidFolder(PASTA)) AssetDatabase.CreateFolder("Assets/Cenario", "Selecao");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        Shader sh = Shader.Find(shader);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, caminho); }
        else if (sh != null) m.shader = sh;
        return m;
    }

    static AnimationClip Clipe(string caminho)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(caminho))
        {
            AnimationClip c = o as AnimationClip;
            if (c != null && !c.name.StartsWith("__preview__")) return c;
        }
        return null;
    }
}
#endif
