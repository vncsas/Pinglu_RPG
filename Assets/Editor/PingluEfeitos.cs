// =====================================================================
//  PingluEfeitos.cs  -  Corte de chama carmesim nos golpes do cavaleiro
// ---------------------------------------------------------------------
//  Cria os materiais do efeito, descobre onde fica a lâmina da espada no
//  modelo e coloca o componente EfeitoEspada no cavaleiro (cena e
//  jogador do multiplayer).
//
//  COMO USAR:  Pinglu > 17. Efeitos da espada
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PingluEfeitos
{
    const string PASTA = "Assets/Efeitos/Espada";
    const string PREFAB_REDE = "Assets/Personagens/Jogadores/Jogador_Cavaleiro.prefab";

    [MenuItem("Pinglu/17. Efeitos da espada")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(PASTA + "/rastro_chama.png") == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei as texturas em " + PASTA + ". Extraiu o zip na pasta do projeto?", "OK");
            return;
        }
        GameObject jogador = AcharJogador();
        if (jogador == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o cavaleiro na cena.", "OK"); return; }

        // 1) materiais
        Material chama = Mat("Corte_Chama", "rastro_chama", true, new Color(5f, 0.35f, 0.4f, 1f));
        Material sombra = Mat("Corte_Sombra", "rastro_sombra", false, Color.white);
        Material brasa = Mat("Brasa", "faisca", true, new Color(4f, 0.45f, 0.45f, 1f));
        Material fumaca = Mat("Fumaca_Escura", "fumaca", false, Color.white);

        // 2) onde fica a lâmina
        Vector3 baseL, pontaL; float comprimento;
        bool achou = DetectarLamina(jogador, out baseL, out pontaL, out comprimento);
        if (!achou) { baseL = new Vector3(0f, 0.12f, 0f); pontaL = new Vector3(0f, 1f, 0f); }

        // 3) componente no cavaleiro da cena e no jogador do multiplayer
        EfeitoEspada ef = jogador.GetComponent<EfeitoEspada>();
        if (ef == null) ef = Undo.AddComponent<EfeitoEspada>(jogador);
        Undo.RecordObject(ef, "Efeitos da espada");
        Preencher(ef, chama, sombra, brasa, fumaca, baseL, pontaL);
        EditorUtility.SetDirty(ef);

        bool prefabOk = false;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_REDE) != null)
        {
            GameObject conteudo = PrefabUtility.LoadPrefabContents(PREFAB_REDE);
            EfeitoEspada efp = conteudo.GetComponent<EfeitoEspada>();
            if (efp == null) efp = conteudo.AddComponent<EfeitoEspada>();
            Preencher(efp, chama, sombra, brasa, fumaca, baseL, pontaL);
            PrefabUtility.SaveAsPrefabAsset(conteudo, PREFAB_REDE);
            PrefabUtility.UnloadPrefabContents(conteudo);
            prefabOk = true;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(jogador.scene);
        Selection.activeGameObject = jogador;

        string msg = "Efeito de chama carmesim pronto!\n\n";
        if (achou && comprimento > 0.4f && comprimento < 2.5f)
            msg += "Achei a lâmina da espada (" + comprimento.ToString("0.00") + " m).";
        else if (achou)
            msg += "Achei algo preso na mão direita, mas com tamanho estranho (" + comprimento.ToString("0.00") + " m). Confira a linha vermelha na janela Scene.";
        else
            msg += "Não consegui achar a lâmina sozinho; usei uma posição padrão. Confira a linha vermelha na janela Scene.";
        msg += "\n\nCom o cavaleiro selecionado, a janela Scene mostra uma LINHA VERMELHA onde o efeito vai sair. " +
               "Se ela não estiver em cima da lâmina, ajuste \"Base Da Lamina\" e \"Ponta Da Lamina\" no componente Efeito Espada." +
               (prefabOk ? "\n\nO jogador do multiplayer também foi atualizado." : "") + "\n\nSalve a cena com Ctrl + S.";
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
    }

    static void Preencher(EfeitoEspada ef, Material chama, Material sombra, Material brasa, Material fumaca, Vector3 b, Vector3 p)
    {
        ef.materialChama = chama; ef.materialSombra = sombra; ef.materialBrasa = brasa; ef.materialFumaca = fumaca;
        ef.baseDaLamina = b; ef.pontaDaLamina = p;
    }

    public static Material Mat(string nome, string textura, bool aditivo, Color cor)
    {
        string texCaminho = PASTA + "/" + textura + ".png";
        TextureImporter ti = AssetImporter.GetAtPath(texCaminho) as TextureImporter;
        if (ti != null && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp))
        {
            ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
        }
        string caminho = PASTA + "/" + nome + ".mat";
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, caminho); }
        else if (sh != null) m.shader = sh;

        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texCaminho));
        m.SetColor("_BaseColor", cor);
        m.SetFloat("_Surface", 1f);                 // transparente
        m.SetFloat("_Blend", aditivo ? 2f : 0f);    // 2 = aditivo (brilha), 0 = alfa (fumaça escura)
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", aditivo ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", aditivo ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);                    // aparece dos dois lados
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // procura as partes do modelo presas à mão direita e pega a ponta mais distante
    public static bool DetectarLamina(GameObject go, out Vector3 baseL, out Vector3 pontaL, out float comprimento)
    {
        baseL = pontaL = Vector3.zero; comprimento = 0f;
        Animator a = null;
        foreach (Animator x in go.GetComponentsInChildren<Animator>(true)) if (x.isHuman && x.runtimeAnimatorController != null) { a = x; break; }
        if (a == null) foreach (Animator x in go.GetComponentsInChildren<Animator>(true)) if (x.isHuman) { a = x; break; }
        if (a == null) return false;
        Transform mao = a.GetBoneTransform(HumanBodyBones.RightHand);
        if (mao == null) return false;
        HashSet<Transform> ossos = new HashSet<Transform>(mao.GetComponentsInChildren<Transform>(true));

        List<Vector3> pontos = new List<Vector3>();
        foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh m = smr.sharedMesh;
            if (m == null) continue;
            Transform[] bones = smr.bones;
            Matrix4x4[] bind = m.bindposes;
            Vector3[] vs = m.vertices;
            BoneWeight[] pesos = m.boneWeights;
            if (pesos.Length != vs.Length) continue;
            for (int i = 0; i < vs.Length; i++)
            {
                int idx = pesos[i].boneIndex0;
                if (pesos[i].weight0 < 0.6f || idx >= bones.Length || idx >= bind.Length || bones[idx] == null || !ossos.Contains(bones[idx])) continue;
                Vector3 noOsso = bind[idx].MultiplyPoint3x4(vs[i]);
                pontos.Add(mao.InverseTransformPoint(bones[idx].TransformPoint(noOsso)));
            }
        }
        foreach (MeshFilter mf in mao.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds bb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                pontos.Add(mao.InverseTransformPoint(mf.transform.TransformPoint(c)));
            }
        }
        if (pontos.Count < 8) return false;

        Vector3 ponta = pontos[0];
        foreach (Vector3 p in pontos) if (p.sqrMagnitude > ponta.sqrMagnitude) ponta = p;
        pontaL = ponta;
        baseL = ponta * 0.14f; // começo da lâmina, logo depois da guarda
        comprimento = mao.TransformVector(ponta - baseL).magnitude;
        return true;
    }

    static GameObject AcharJogador()
    {
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
        {
            if (p == null || !p.gameObject.scene.IsValid() || EditorUtility.IsPersistent(p)) continue;
            string n = p.gameObject.name.ToLower();
            if (n.Contains("x bot") || n.Contains("antigo")) continue;
            if (!p.gameObject.activeInHierarchy) continue;
            return p.gameObject;
        }
        return null;
    }
}
#endif
