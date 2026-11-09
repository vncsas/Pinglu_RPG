// =====================================================================
//  PingluEncontroCastelo.cs  -  Prepara o Cavaleiro Antigo e o Ladrão e o encontro do Castelo
// ---------------------------------------------------------------------
//  Pinglu > 69: cria os dois inimigos (a partir do Cavaleiro_MiniBoss, sem os controles de jogador)
//               e coloca "Encontro do Castelo" na cena. Depois, no Play, F4 vai direto para o castelo.
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluEncontroCastelo
{
    const string MOLDE = "Assets/Personagens/Cavaleiro/Cavaleiro_MiniBoss.prefab";
    const string PASTA = "Assets/Personagens/Inimigos/Castelo";

    [MenuItem("Pinglu/69. Castelo: preparar o Cavaleiro Antigo e o Ladrão")]
    public static void Preparar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MOLDE) == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei " + MOLDE + ".", "OK"); return; }
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Criando os inimigos do castelo...", 0.2f);
            if (!AssetDatabase.IsValidFolder("Assets/Personagens/Inimigos/Castelo")) AssetDatabase.CreateFolder("Assets/Personagens/Inimigos", "Castelo");
            GameObject cav = Fazer("CavaleiroAntigo", PerfilHumanoide.CavaleiroAntigo);
            GameObject lad = Fazer("Ladrao", PerfilHumanoide.Ladrao);

            EditorUtility.DisplayProgressBar("Pinglu", "Colocando o encontro na cena...", 0.8f);
            GameObject mundo = GameObject.Find("Mundo_Gerado");
            Transform castelo = mundo != null ? mundo.transform.Find("Castelo Destruido") : null;
            Transform estrutura = castelo != null ? castelo.Find("Estrutura") : null;
            if (estrutura == null) { EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("Pinglu", "Não achei o castelo na cena (Mundo_Gerado > Castelo Destruido > Estrutura). Rode antes o Pinglu > 45.", "OK"); return; }
            EncontroDoCastelo velho = Object.FindFirstObjectByType<EncontroDoCastelo>();
            if (velho != null) Object.DestroyImmediate(velho.gameObject);
            GameObject g = new GameObject("Encontro do Castelo");
            EncontroDoCastelo e = g.AddComponent<EncontroDoCastelo>();
            e.estrutura = estrutura; e.prefabCavaleiro = cav; e.prefabLadrao = lad;
            EditorUtility.SetDirty(e);
            EditorSceneManager.MarkSceneDirty(g.scene); EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Pinglu", "Castelo pronto!\n\n- Cavaleiro Antigo (mini-boss do pátio) e Ladrão (Salão do Trono, provisório)\n- Depois do Ladrão, o 2º cristal surge e a história vai para o Pântano\n\nPara testar: Play e aperte F4.", "OK");
        }
        catch (System.Exception ex) { Debug.LogException(ex); EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("Pinglu", "Deu um erro: " + ex.Message + "\n(veja o Console)", "OK"); }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static GameObject Fazer(string nome, PerfilHumanoide perfil)
    {
        string destino = PASTA + "/" + nome + ".prefab";
        GameObject raiz = PrefabUtility.LoadPrefabContents(MOLDE);
        try
        {
            // tira tudo que é de jogador (o efeito de espada exige o controle do jogador, por isso sai primeiro)
            foreach (EfeitoEspada c in raiz.GetComponentsInChildren<EfeitoEspada>(true)) Object.DestroyImmediate(c);
            foreach (EscudoAnimacao c in raiz.GetComponentsInChildren<EscudoAnimacao>(true)) Object.DestroyImmediate(c);
            foreach (HUDJogador c in raiz.GetComponentsInChildren<HUDJogador>(true)) Object.DestroyImmediate(c);
            foreach (CombateJogador c in raiz.GetComponentsInChildren<CombateJogador>(true)) Object.DestroyImmediate(c);
            foreach (PlayerLocomotion c in raiz.GetComponentsInChildren<PlayerLocomotion>(true)) Object.DestroyImmediate(c);
            if (raiz.GetComponent<CharacterController>() == null) raiz.AddComponent<CharacterController>();
            if (raiz.GetComponent<Vida>() == null) raiz.AddComponent<Vida>();
            InimigoCavaleiro e = raiz.GetComponent<InimigoCavaleiro>(); if (e == null) e = raiz.AddComponent<InimigoCavaleiro>();
            e.perfil = perfil;
            if (perfil == PerfilHumanoide.CavaleiroAntigo)
            {
                e.nome = "Cavaleiro Antigo"; e.vidaBase = 1100f; e.escala = 1.4f; e.velAndar = 2.2f; e.velCorrer = 4.6f; e.alcance = 2.4f; e.danoLeve = 20f; e.danoPesado = 42f;
                e.tinta = new Color(0.62f, 0.66f, 0.8f); e.corAura = new Color(1f, 0.14f, 0.1f);
            }
            else
            {
                e.nome = "O Ladrão"; e.vidaBase = 520f; e.escala = 0.98f; e.velAndar = 3.2f; e.velCorrer = 6.4f; e.alcance = 2.2f; e.danoLeve = 13f; e.danoPesado = 24f;
                e.tinta = new Color(0.4f, 0.36f, 0.46f); e.corAura = new Color(0.3f, 0.55f, 1f);
            }
            SerializedObject sv = new SerializedObject(raiz.GetComponent<Vida>());
            SerializedProperty vm = sv.FindProperty("vidaMaxima"); if (vm != null) vm.floatValue = e.vidaBase;
            SerializedProperty inv = sv.FindProperty("invulneravelAposDano"); if (inv != null) inv.floatValue = 0.1f;
            sv.ApplyModifiedPropertiesWithoutUndo();
            raiz.name = nome;
            AssetDatabase.DeleteAsset(destino);
            return PrefabUtility.SaveAsPrefabAsset(raiz, destino);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }
}
#endif
