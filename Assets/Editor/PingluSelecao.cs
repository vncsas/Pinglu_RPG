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
    const string MAGA = "Assets/Personagens/Maga";
    const string PORTAL_FBX = "Assets/Personagens/Maps/ancient-stone-gate-ruin-moss-covered/source/Zyklus4_Zeile9/tripo_convert_7554d447-81d6-4be4-8541-ffc25d5d5324.fbx";

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

        // portal: o mesmo portão de pedra da cena ("Portal de Ruina"), com o redemoinho mágico
        string avisoPortal = "";
        GameObject portalCena = null;
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t != null && t.gameObject.scene.IsValid() && t.name == "Portal de Ruina") { portalCena = t.gameObject; break; }
        GameObject modeloPortal = null;
        Quaternion rotPortal = Quaternion.Euler(-90f, 0f, 0f);
        if (portalCena != null)
        {
            modeloPortal = PrefabUtility.GetCorrespondingObjectFromOriginalSource(portalCena);
            rotPortal = portalCena.transform.rotation;
        }
        if (modeloPortal == null) modeloPortal = AssetDatabase.LoadAssetAtPath<GameObject>(PORTAL_FBX);
        if (modeloPortal == null) avisoPortal = "\n\nNão achei o portão de pedra; o palco fica sem portal.";
        else if (portalCena == null) avisoPortal = "\n\nNão achei o \"Portal de Ruina\" na cena; usei o modelo com uma rotação padrão (confira no Play).";
        so.FindProperty("modeloPortal").objectReferenceValue = modeloPortal;
        so.FindProperty("rotacaoPortal").quaternionValue = rotPortal;
        Color violeta = new Color(0.55f, 0.45f, 1f);
        so.FindProperty("materialPortal").objectReferenceValue = MaterialDoPortal(violeta);
        so.FindProperty("corPortal").colorValue = violeta;
        so.ApplyModifiedProperties();

        // o portão do mundo vira o portal de chegada (os jogadores nascem nele)
        if (portalCena != null)
        {
            PortalDoMundo pm = portalCena.GetComponent<PortalDoMundo>();
            if (pm == null) pm = Undo.AddComponent<PortalDoMundo>(portalCena);
            Undo.RecordObject(pm, "Portal do mundo");
            pm.materialPortal = (Material)so.FindProperty("materialPortal").objectReferenceValue;
            pm.materialParticula = AssetDatabase.LoadAssetAtPath<Material>(PARTICULA);
            pm.cor = violeta;
            EditorUtility.SetDirty(pm);
            avisoPortal += "\n\nO \"Portal de Ruina\" do mapa agora é o portal de chegada: os jogadores nascem nele.";
        }

        // ficha do Cavaleiro Sombrio
        string resumo = "";
        SerializedProperty lista = so.FindProperty("personagens");
        for (int i = 0; i < lista.arraySize; i++)
        {
            FichaPersonagem f = lista.GetArrayElementAtIndex(i).objectReferenceValue as FichaPersonagem;
            if (f != null && f.nome.ToLower().Contains("maga"))
            {
                f.animacaoAndar = Clipe(MAGA + "/animacoes/maga@Andar.fbx");
                EditorUtility.SetDirty(f);
            }
            if (f == null || !f.nome.ToLower().Contains("sombrio")) continue;
            f.animacaoAndar = Clipe(SOMBRIO + "/animacoes/cavaleiro_sombrio@Andar.fbx");
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
        EditorUtility.DisplayDialog("Pinglu", "Tela de seleção pronta!" + resumo + avisoPortal +
            "\n\nPara os próximos personagens, preencha na ficha deles: Titulo, Atributos, Modelo Visual (o .fbx), Animacao Parado e Animacao Entrada." +
            "\n\nSalve a cena com Ctrl + S.", "OK");
    }

    // redemoinho no formato do arco (shader Pinglu/Portal)
    static Material MaterialDoPortal(Color cor)
    {
        Shader sh = Shader.Find("Pinglu/Portal");
        if (sh == null) return PingluEfeitos.Mat("Portal_Vortice", "portal_vortice", true, new Color(cor.r * 2f, cor.g * 2f, cor.b * 2f, 1f));
        Material m = Mat(PASTA + "/Portal_Arco.mat", "Pinglu/Portal");
        m.SetColor("_Cor", cor);
        EditorUtility.SetDirty(m);
        return m;
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
