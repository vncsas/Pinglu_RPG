// =====================================================================
//  PingluPersonagem.cs  -  Troca o X Bot pelo Cavaleiro (Pinglu RPG)
// ---------------------------------------------------------------------
//  COMO USAR:
//   1. Traga a pasta do cavaleiro da branch "rigs" (Assets/Personagens/Cavaleiro)
//   2. Coloque este arquivo em  Assets/Editor/
//   3. Abra a SampleScene e clique em:  Pinglu > Trocar jogador pelo Cavaleiro
//   4. Dê Play para testar e salve a cena (Ctrl + S)
//
//  O script faz sozinho os passos 1, 2 e 3 do LEIAME_ANIMADORES.txt
//  (esqueleto Humanoid, texturas e material) e depois troca o boneco,
//  mantendo os controles, as animações e a câmera que já existem.
//
//  Para voltar:  Pinglu > Voltar para o X Bot
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluPersonagem
{
    const string PASTA = "Assets/Personagens/Cavaleiro";
    const string FBX = PASTA + "/cavaleiro_rigged.fbx";
    const string TEX_COR = PASTA + "/texturas_unity/cavaleiro_BaseColor.png";
    const string TEX_METAL = PASTA + "/texturas_unity/cavaleiro_MetallicSmoothness.png";
    const string TEX_NORMAL = PASTA + "/texturas_unity/cavaleiro_Normal.png";
    const string MATERIAL = PASTA + "/Cavaleiro_Material.mat";

    const string NOME_NOVO = "Jogador (Cavaleiro)";
    const string SUFIXO_ANTIGO = " (antigo - desativado)";

    // ==================================================================
    //  TROCAR
    // ==================================================================
    [MenuItem("Pinglu/Trocar jogador pelo Cavaleiro")]
    public static void Trocar()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FBX) == null)
        {
            EditorUtility.DisplayDialog("Pinglu",
                "Não achei o arquivo " + FBX + ".\n\nPrimeiro traga a pasta do cavaleiro da branch \"rigs\" (veja as instruções que o Claude passou).",
                "OK");
            return;
        }

        CharacterController antigoCC = UnityEngine.Object.FindFirstObjectByType<CharacterController>();
        if (antigoCC == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei o jogador atual na cena (o objeto com Character Controller). Abra a SampleScene e tente de novo.", "OK");
            return;
        }
        GameObject antigo = antigoCC.gameObject;
        if (antigo.name == NOME_NOVO)
        {
            EditorUtility.DisplayDialog("Pinglu", "O cavaleiro já é o jogador desta cena.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Pinglu - Trocar personagem",
            "Vou configurar o cavaleiro (esqueleto, texturas e material) e colocar ele no lugar de \"" + antigo.name + "\".\n\n" +
            "O boneco antigo não será apagado, só desativado. Dá para voltar em Pinglu > Voltar para o X Bot.",
            "Pode fazer", "Cancelar"))
            return;

        List<string> avisos = new List<string>();
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Configurando texturas...", 0.1f);
            ConfigurarTexturas();

            EditorUtility.DisplayProgressBar("Pinglu", "Criando o material...", 0.25f);
            Material mat = CriarMaterial(avisos);

            EditorUtility.DisplayProgressBar("Pinglu", "Configurando o esqueleto Humanoid...", 0.4f);
            Avatar avatar = ConfigurarModelo(mat, avisos);

            EditorUtility.DisplayProgressBar("Pinglu", "Trocando o personagem na cena...", 0.75f);
            GameObject novo = ColocarNaCena(antigo, avatar, avisos);

            Selection.activeGameObject = novo;
            EditorGUIUtility.PingObject(novo);
            EditorSceneManager.MarkSceneDirty(novo.scene);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            avisos.Add("Deu um erro: " + e.Message + " (veja o Console).");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string msg = "Pronto! Dê Play para testar o cavaleiro.\nSe estiver tudo certo, salve a cena com Ctrl + S.";
        if (avisos.Count > 0) msg += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        foreach (string a in avisos) Debug.LogWarning("[Pinglu] " + a);
    }

    // Passo 2a e 2b do LEIAME: normal map e metallic sem sRGB
    static void ConfigurarTexturas()
    {
        TextureImporter normal = AssetImporter.GetAtPath(TEX_NORMAL) as TextureImporter;
        if (normal != null && normal.textureType != TextureImporterType.NormalMap)
        {
            normal.textureType = TextureImporterType.NormalMap;
            normal.SaveAndReimport();
        }
        TextureImporter metal = AssetImporter.GetAtPath(TEX_METAL) as TextureImporter;
        if (metal != null && metal.sRGBTexture)
        {
            metal.sRGBTexture = false;
            metal.SaveAndReimport();
        }
    }

    // Passo 2c e 2d do LEIAME: material URP com as três texturas
    static Material CriarMaterial(List<string> avisos)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
        if (mat != null) return mat;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { avisos.Add("Não achei o shader URP/Lit. O cavaleiro pode ficar branco."); return null; }

        mat = new Material(lit);
        mat.name = "Cavaleiro_Material";

        Texture2D cor = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_COR);
        Texture2D metal = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_METAL);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_NORMAL);

        mat.SetColor("_BaseColor", Color.white);
        if (cor != null) { mat.SetTexture("_BaseMap", cor); mat.SetTexture("_MainTex", cor); }
        else avisos.Add("Não achei " + TEX_COR);

        if (metal != null)
        {
            mat.SetTexture("_MetallicGlossMap", metal);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_SmoothnessTextureChannel", 0f); // Metallic Alpha
            mat.SetFloat("_Smoothness", 0.8f);
        }
        else avisos.Add("Não achei " + TEX_METAL);

        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }
        else avisos.Add("Não achei " + TEX_NORMAL);

        AssetDatabase.CreateAsset(mat, MATERIAL);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Passo 1 do LEIAME: Humanoid + Create From This Model, e liga o material ao FBX
    static Avatar ConfigurarModelo(Material mat, List<string> avisos)
    {
        ModelImporter mi = AssetImporter.GetAtPath(FBX) as ModelImporter;
        if (mi == null) throw new System.Exception("O arquivo " + FBX + " não é um modelo válido. O Git LFS estava instalado quando vocês baixaram?");

        bool mudou = false;
        if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; mudou = true; }
        if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mudou = true; }
        if (mudou) mi.SaveAndReimport();

        if (mat != null)
        {
            bool remapeou = false;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FBX))
            {
                Material embutido = o as Material;
                if (embutido == null) continue;
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embutido.name), mat);
                remapeou = true;
            }
            if (remapeou) mi.SaveAndReimport();
        }

        Avatar avatar = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FBX))
            if (o is Avatar) avatar = (Avatar)o;

        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            avisos.Add("O esqueleto Humanoid não ficou válido. Clique no cavaleiro_rigged > aba Rig > Configure... e veja se tem osso vermelho (o LEIAME explica como arrumar).");
        return avatar;
    }

    static GameObject ColocarNaCena(GameObject antigo, Avatar avatar, List<string> avisos)
    {
        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();

        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
        GameObject novo = (GameObject)PrefabUtility.InstantiatePrefab(modelo, antigo.scene);
        Undo.RegisterCreatedObjectUndo(novo, "Trocar personagem");
        novo.name = NOME_NOVO;
        novo.transform.SetParent(antigo.transform.parent, false);
        novo.transform.position = antigo.transform.position;
        novo.transform.rotation = antigo.transform.rotation;
        novo.tag = antigo.tag;
        novo.layer = antigo.layer;

        // Animator: mesmo controlador do X Bot (as animações são Humanoid, então servem no cavaleiro)
        Animator animAntigo = antigo.GetComponent<Animator>();
        Animator anim = novo.GetComponent<Animator>();
        if (anim == null) anim = novo.AddComponent<Animator>();
        if (avatar != null) anim.avatar = avatar;
        if (animAntigo != null)
        {
            anim.runtimeAnimatorController = animAntigo.runtimeAnimatorController;
            anim.applyRootMotion = animAntigo.applyRootMotion;
            anim.updateMode = animAntigo.updateMode;
            anim.cullingMode = animAntigo.cullingMode;
        }

        // Character Controller ajustado para 1,8 m
        CharacterController ccAntigo = antigo.GetComponent<CharacterController>();
        CharacterController cc = novo.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.center = new Vector3(0f, 0.9f + ccAntigo.skinWidth, 0f);
        cc.radius = 0.35f;
        cc.slopeLimit = ccAntigo.slopeLimit;
        cc.stepOffset = ccAntigo.stepOffset;
        cc.skinWidth = ccAntigo.skinWidth;
        cc.minMoveDistance = ccAntigo.minMoveDistance;

        // Copia os scripts do jogador (PlayerLocomotion etc.) com os mesmos valores
        foreach (MonoBehaviour mb in antigo.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue; // script faltando
            Component copia = novo.GetComponent(mb.GetType());
            if (copia == null) copia = novo.AddComponent(mb.GetType());
            if (UnityEditorInternal.ComponentUtility.CopyComponent(mb))
                UnityEditorInternal.ComponentUtility.PasteComponentValues(copia);
        }

        // Tudo na cena que apontava para o boneco antigo (câmera etc.) passa a apontar para o novo
        int trocas = TrocarReferencias(antigo, novo);
        if (trocas == 0) avisos.Add("Não achei a câmera apontando para o boneco. Verifique o campo Target do Third Person Camera Follow na Main Camera.");

        // Desativa o antigo (não apaga)
        Undo.RecordObject(antigo, "Trocar personagem");
        antigo.name = antigo.name + SUFIXO_ANTIGO;
        antigo.SetActive(false);

        // Confere a altura
        Bounds b;
        if (MedirBounds(novo, out b) && (b.size.y < 1.2f || b.size.y > 2.6f))
            avisos.Add("O cavaleiro ficou com " + b.size.y.ToString("0.00") + " m de altura (o esperado é 1,8 m). Confira o Scale Factor do FBX na aba Model.");

        Undo.CollapseUndoOperations(grupo);
        return novo;
    }

    // ==================================================================
    //  VOLTAR
    // ==================================================================
    [MenuItem("Pinglu/Voltar para o X Bot")]
    public static void Voltar()
    {
        GameObject novo = GameObject.Find(NOME_NOVO);
        GameObject antigo = null;
        foreach (GameObject raiz in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(SUFIXO_ANTIGO)) antigo = t.gameObject;

        if (novo == null || antigo == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei os dois personagens na cena para desfazer a troca.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.RecordObject(antigo, "Voltar personagem");
        antigo.SetActive(true);
        antigo.name = antigo.name.Substring(0, antigo.name.Length - SUFIXO_ANTIGO.Length);
        TrocarReferencias(novo, antigo);
        Undo.DestroyObjectImmediate(novo);
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(antigo.scene);
        EditorUtility.DisplayDialog("Pinglu", "Voltei para o personagem antigo. Salve a cena (Ctrl + S) para confirmar.", "OK");
    }

    // ==================================================================
    //  APOIO
    // ==================================================================
    static int TrocarReferencias(GameObject de, GameObject para)
    {
        int trocas = 0;
        foreach (GameObject raiz in de.scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour mb in raiz.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.gameObject == de || mb.gameObject == para) continue;
                SerializedObject so = new SerializedObject(mb);
                SerializedProperty it = so.GetIterator();
                bool mudou = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object valor = it.objectReferenceValue;
                    if (valor == null) continue;
                    if (valor == de) { it.objectReferenceValue = para; mudou = true; }
                    else if (valor == de.transform) { it.objectReferenceValue = para.transform; mudou = true; }
                }
                if (mudou)
                {
                    Undo.RecordObject(mb, "Trocar personagem");
                    so.ApplyModifiedProperties();
                    trocas++;
                }
            }
        }
        return trocas;
    }

    static bool MedirBounds(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        bool achou = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!achou) { b = r.bounds; achou = true; } else b.Encapsulate(r.bounds);
        }
        return achou;
    }
}
#endif
