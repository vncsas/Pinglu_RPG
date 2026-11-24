// =====================================================================
//  PingluAnimacoes.cs  -  Coloca as animações da Universal Animation
//  Library 2 no cavaleiro (Pinglu RPG)
// ---------------------------------------------------------------------
//  COMO USAR:
//   1. Coloque este arquivo em  Assets/Editor/
//   2. Clique em:  Pinglu > 5. Animações do cavaleiro
//   3. Confira as animações escolhidas na janela (dá para trocar)
//   4. Clique em "Aplicar no cavaleiro", dê Play e salve a cena (Ctrl + S)
//
//  Os controles continuam os mesmos: o script cria uma cópia do Animator
//  do X Bot (com os mesmos comandos) e só troca as animações.
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PingluAnimacoes : EditorWindow
{
    const string CONTROLADOR_XBOT = "Assets/Personagens/Sword and Shield Pack/Animator Controller/XBot_Animator.controller";
    const string CONTROLADOR_NOVO = "Assets/Personagens/Cavaleiro/Cavaleiro_Animator.controller";

    // ação, nome do estado no Animator, se repete em loop
    static readonly string[] ACOES = { "Parado", "Andar", "Correr", "Pular", "Rolar", "Combo: golpe 1", "Ataque pesado", "Bloquear", "Combo: golpe 2", "Combo: golpe 3" };
    static readonly bool[] LOOP = { true, true, true, false, false, false, false, true, false, false };

    string caminhoFbx;
    List<AnimationClip> clipes = new List<AnimationClip>();
    List<string> origens = new List<string>();      // arquivo de onde veio cada animação
    List<string> fontesUAL = new List<string>();    // arquivos que este script configura (Humanoid)
    const string PASTA_ANIMACOES = "Assets/Personagens/Animacoes";
    const string PASTA_XBOT = "Assets/Personagens/Sword and Shield Pack";
    string[] nomes = new string[0];
    int[] escolha = new int[ACOES.Length];   // 0 = manter a animação atual (do X Bot)
    Vector2 rolagem;
    string erro;

    [MenuItem("Pinglu/5. Animações do cavaleiro")]
    public static void Abrir()
    {
        PingluAnimacoes w = GetWindow<PingluAnimacoes>(true, "Animações do Cavaleiro");
        w.minSize = new Vector2(460, 420);
        w.Carregar();
    }

    // ------------------------------------------------------------------
    void Carregar()
    {
        erro = null;
        caminhoFbx = AcharFbx();
        if (caminhoFbx == null)
        {
            erro = "Não achei o arquivo UAL2_Standard.fbx no projeto. Ele deve estar em Assets/Personagens/Animacoes. " +
                   "Se o arquivo tiver só alguns bytes, rode 'git lfs pull' na pasta do projeto.";
            return;
        }
        // fontes: o pacote UAL2, outros pacotes na pasta Animacoes (ex.: UAL1) e as animações humanas do X Bot
        fontesUAL.Clear();
        fontesUAL.Add(caminhoFbx);
        if (AssetDatabase.IsValidFolder(PASTA_ANIMACOES))
            foreach (string g in AssetDatabase.FindAssets("t:Model", new[] { PASTA_ANIMACOES }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.ToLower().EndsWith(".fbx") || p.Contains("_RM") || p.ToLower().Contains("mannequin") || fontesUAL.Contains(p)) continue;
                fontesUAL.Add(p);
            }
        foreach (string f in fontesUAL) ConfigurarFbx(f, null);

        List<string> todas = new List<string>(fontesUAL);
        if (AssetDatabase.IsValidFolder(PASTA_XBOT))
            foreach (string g in AssetDatabase.FindAssets("t:Model", new[] { PASTA_XBOT }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.ToLower().EndsWith(".fbx") && !p.Contains("X Bot")) todas.Add(p);
            }

        clipes.Clear(); origens.Clear();
        List<KeyValuePair<AnimationClip, string>> lista = new List<KeyValuePair<AnimationClip, string>>();
        foreach (string f in todas)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(f))
            {
                AnimationClip c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) lista.Add(new KeyValuePair<AnimationClip, string>(c, f));
            }
        lista.Sort((a, b) => string.Compare(Rotulo(a.Key, a.Value), Rotulo(b.Key, b.Value), System.StringComparison.OrdinalIgnoreCase));
        foreach (var kv in lista) { clipes.Add(kv.Key); origens.Add(kv.Value); }
        nomes = new string[clipes.Count + 1];
        nomes[0] = "(manter a atual do X Bot)";
        for (int i = 0; i < clipes.Count; i++) nomes[i + 1] = Rotulo(clipes[i], origens[i]);

        if (clipes.Count == 0) { erro = "O arquivo foi encontrado, mas não tem animações dentro. O Git LFS baixou ele certo?"; return; }
        EscolherAutomatico();
    }

    static string Rotulo(AnimationClip c, string arquivo)
    {
        string pacote = arquivo.Contains("Sword and Shield") ? "X Bot" : System.IO.Path.GetFileNameWithoutExtension(arquivo);
        return pacote + " / " + c.name;
    }

    static string AcharFbx()
    {
        string melhor = null;
        foreach (string g in AssetDatabase.FindAssets("UAL2_Standard"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.EndsWith(".fbx")) continue;
            if (p.Contains("_RM")) { if (melhor == null) melhor = p; continue; } // a versão sem root motion é melhor aqui
            return p;
        }
        return melhor;
    }

    // Humanoid + avatar próprio + raiz travada (quem move o cavaleiro é o script, não a animação)
    static void ConfigurarFbx(string caminho, HashSet<string> emLoop)
    {
        ModelImporter mi = AssetImporter.GetAtPath(caminho) as ModelImporter;
        if (mi == null) return;
        bool mudou = false;
        if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; mudou = true; }
        if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mudou = true; }
        if (mudou) mi.SaveAndReimport();

        ModelImporterClipAnimation[] cl = mi.clipAnimations;
        if (cl == null || cl.Length == 0) cl = mi.defaultClipAnimations;
        bool mudouClipes = false;
        foreach (ModelImporterClipAnimation c in cl)
        {
            bool loop = emLoop != null ? emLoop.Contains(c.name) : Regex.IsMatch(c.name.ToLower(), "loop|idle|walk|jog|run|sprint");
            if (c.loopTime != loop) { c.loopTime = loop; mudouClipes = true; }
            if (AcertarRaiz(c)) mudouClipes = true;
        }
        if (mudouClipes) { mi.clipAnimations = cl; mi.SaveAndReimport(); }
    }

    // Raiz travada e corpo olhando para a frente do personagem
    // ("Based Upon: Body Orientation", igual às animações do X Bot)
    static bool AcertarRaiz(ModelImporterClipAnimation c)
    {
        if (c.lockRootRotation && c.lockRootHeightY && c.lockRootPositionXZ && !c.keepOriginalOrientation && c.keepOriginalPositionY) return false;
        c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
        c.keepOriginalOrientation = false; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = false;
        return true;
    }

    [MenuItem("Pinglu/7. Corrigir direção das animações")]
    public static void CorrigirDirecao()
    {
        string caminho = AcharFbx();
        if (caminho == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o arquivo UAL2_Standard.fbx.", "OK"); return; }
        ModelImporter mi = AssetImporter.GetAtPath(caminho) as ModelImporter;
        ModelImporterClipAnimation[] cl = mi.clipAnimations;
        if (cl == null || cl.Length == 0) cl = mi.defaultClipAnimations;
        bool mudou = false;
        foreach (ModelImporterClipAnimation c in cl) if (AcertarRaiz(c)) mudou = true;
        if (mudou) { mi.clipAnimations = cl; mi.SaveAndReimport(); }

        // se o modelo foi virado pelo menu 6, desvira (agora não precisa mais)
        CharacterController cc = Object.FindFirstObjectByType<CharacterController>();
        string extra = "";
        if (cc != null)
        {
            Transform filho = cc.transform.Find("Modelo (virado)");
            if (filho != null && Mathf.Abs(Mathf.DeltaAngle(filho.localEulerAngles.y, 0f)) > 1f)
            {
                Undo.RecordObject(filho, "Corrigir direção");
                filho.localRotation = Quaternion.identity;
                EditorSceneManager.MarkSceneDirty(cc.gameObject.scene);
                extra = "\n\nTambém desvirei o modelo que o menu 6 tinha girado.";
            }
        }
        EditorUtility.DisplayDialog("Pinglu", "Pronto! As animações agora deixam o corpo olhando para a frente do personagem." + extra +
            "\n\nDê Play e aperte W. Se gostar, salve a cena com Ctrl + S.", "OK");
    }

    // ------------------------------------------------------------------
    //  escolha automática por palavras no nome da animação
    // ------------------------------------------------------------------
    void EscolherAutomatico()
    {
        string[] ruimGeral = { "turn", "injured", "limp", "tired", "sneak", "stealth", "crouch", "swim", "pistol", "rifle", "gun", "zombie", "sit", "spell", "bow", "fish", "farm", "dance", "drunk", "carry", "push", "climb", "ladder", "injur", "death", "dead", "hit", "emote", "talk", "wave" };
        string[] direcoes = { "back", "bwd", "left", "right", "strafe", "l", "r" };

        escolha[0] = Melhor(new[] { "idle" }, new[] { "breathing", "loop", "neutral", "stand" }, Juntar(ruimGeral, new[] { "block", "sleep" }), -1);
        escolha[1] = Melhor(new[] { "walk" }, new[] { "fwd", "forward", "loop" }, Juntar(ruimGeral, direcoes), -1);
        escolha[2] = Melhor(new[] { "jog", "run" }, new[] { "fwd", "forward", "loop", "jog" }, Juntar(ruimGeral, direcoes), -1);
        if (escolha[2] == 0) escolha[2] = Melhor(new[] { "sprint" }, new[] { "loop" }, Juntar(ruimGeral, direcoes), -1);
        escolha[3] = Melhor(new[] { "jump" }, new[] { "start", "up" }, Juntar(ruimGeral, new[] { "land", "fall", "loop", "vault", "parkour", "wall" }), -1);
        escolha[4] = Melhor(new[] { "roll", "dodge", "dive" }, new[] { "fwd", "forward" }, Juntar(ruimGeral, direcoes), -1);
        string[] ruimAtaque = Juntar(ruimGeral, new[] { "recovery", "_rec", "shoot", "kick", "idle", "block", "walk", "run" });
        escolha[5] = Melhor(new[] { "sword", "slash", "attack", "melee", "strike" }, new[] { "sword", "sword", "slash", "attack", "1", "a", "01", "light", "quick", "regular", "simple" },
                            Juntar(ruimAtaque, new[] { "combo", "heavy", "spin", "power", "strong", "2", "3", "4", "b", "c" }), -1);
        escolha[6] = Melhor(new[] { "sword", "slash", "attack", "melee", "strike" }, new[] { "sword", "sword", "heavy", "power", "strong", "spin", "overhead", "slam", "2", "b", "02", "combo" },
                            ruimAtaque, escolha[5]);
        escolha[7] = Melhor(new[] { "block", "guard", "shield", "parry", "defend" }, new[] { "loop", "idle", "shield" }, Juntar(ruimGeral, new[] { "break", "hit", "bash" }), -1);
        string[] obrigAtaque = { "sword", "slash", "attack", "melee", "strike" };
        escolha[8] = Melhor(obrigAtaque, new[] { "sword", "sword", "b", "2", "02", "second" },
                            Juntar(ruimAtaque, new[] { "combo", "a", "1", "01", "c", "3", "03", "heavy" }), escolha[5], escolha[6]);
        escolha[9] = Melhor(obrigAtaque, new[] { "sword", "sword", "c", "3", "03", "third", "finisher" },
                            Juntar(ruimAtaque, new[] { "combo", "a", "1", "01", "b", "2", "02" }), escolha[5], escolha[8]);
        // sem golpes 2 e 3 próprios: usa outros ataques de espada para o combo não repetir
        if (escolha[8] == 0) escolha[8] = Melhor(obrigAtaque, new[] { "sword", "sword" }, ruimAtaque, escolha[5], escolha[6]);
        if (escolha[9] == 0) escolha[9] = Melhor(obrigAtaque, new[] { "sword", "sword" }, ruimAtaque, escolha[5], escolha[8]);
    }

    int Melhor(string[] obrigatorio, string[] bom, string[] ruim, int evitar) { return Melhor(obrigatorio, bom, ruim, evitar, -1); }

    int Melhor(string[] obrigatorio, string[] bom, string[] ruim, int evitar, int evitar2)
    {
        int melhor = 0; float nota = float.MinValue;
        for (int i = 0; i < clipes.Count; i++)
        {
            if (i + 1 == evitar || i + 1 == evitar2) continue;
            string n = clipes[i].name.ToLower();
            string[] partes = Regex.Split(n, "[^a-z0-9]+");
            bool tem = false;
            foreach (string o in obrigatorio) if (n.Contains(o)) tem = true;
            if (!tem) continue;
            bool proibido = false;
            foreach (string r in ruim) if (Tem(n, partes, r)) { proibido = true; break; }
            if (proibido) continue; // zumbi, ferido, agachado etc. nunca são escolhidos
            float s = 0f;
            foreach (string b in bom) s += Tem(n, partes, b) ? 2f : 0f;
            s -= n.Length * 0.02f; // nomes mais simples primeiro
            if (s > nota) { nota = s; melhor = i + 1; }
        }
        return melhor;
    }

    static bool Tem(string nome, string[] partes, string palavra)
    {
        if (palavra.Length <= 2 && !palavra.StartsWith("_")) { foreach (string p in partes) if (p == palavra) return true; return false; }
        return nome.Contains(palavra);
    }

    static string[] Juntar(string[] a, string[] b) { List<string> l = new List<string>(a); l.AddRange(b); return l.ToArray(); }

    // ------------------------------------------------------------------
    void OnGUI()
    {
        EditorGUILayout.Space();
        if (erro != null)
        {
            EditorGUILayout.HelpBox(erro, MessageType.Error);
            if (GUILayout.Button("Tentar de novo")) Carregar();
            return;
        }
        EditorGUILayout.HelpBox("Escolhi as animações automaticamente pelo nome. Confira e troque o que quiser.\n" +
                                "Dica: selecione uma animação na aba Project (dentro do UAL2_Standard) para ver ela no Inspector.", MessageType.Info);
        EditorGUILayout.LabelField("Animações encontradas: " + clipes.Count, EditorStyles.miniLabel);
        EditorGUILayout.Space();

        rolagem = EditorGUILayout.BeginScrollView(rolagem);
        for (int i = 0; i < ACOES.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ACOES[i], GUILayout.Width(110));
            escolha[i] = EditorGUILayout.Popup(escolha[i], nomes);
            GUI.enabled = escolha[i] > 0;
            if (GUILayout.Button("Ver", GUILayout.Width(40))) { Selection.activeObject = clipes[escolha[i] - 1]; EditorGUIUtility.PingObject(Selection.activeObject); }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Escolher automático de novo")) EscolherAutomatico();
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Aplicar no cavaleiro", GUILayout.Height(32))) Aplicar();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();
    }

    // ------------------------------------------------------------------
    void Aplicar()
    {
        AnimatorController original = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLADOR_XBOT);
        if (original == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Animator do X Bot em " + CONTROLADOR_XBOT, "OK"); return; }

        Animator anim = AcharAnimatorDoJogador();
        if (anim == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o cavaleiro na cena. Abra a SampleScene.", "OK"); return; }
        if (anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.name.ToLower().Contains("sombrio"))
        {
            EditorUtility.DisplayDialog("Pinglu", "O personagem da cena é o Cavaleiro Sombrio, que já usa as animações feitas para ele (Pinglu > 18). Este menu era para o cavaleiro antigo.", "OK");
            return;
        }

        // 1) acerta o loop das animações escolhidas
        string[] nomesEscolhidos = new string[ACOES.Length];
        for (int i = 0; i < ACOES.Length; i++) nomesEscolhidos[i] = escolha[i] > 0 ? origens[escolha[i] - 1] + "|" + clipes[escolha[i] - 1].name : null;
        foreach (string fonte in fontesUAL)
        {
            HashSet<string> emLoop = new HashSet<string>();
            HashSet<string> semLoop = new HashSet<string>();
            for (int i = 0; i < ACOES.Length; i++)
                if (escolha[i] > 0 && origens[escolha[i] - 1] == fonte) (LOOP[i] ? emLoop : semLoop).Add(clipes[escolha[i] - 1].name);
            for (int i = 0; i < clipes.Count; i++)
            {
                string nm = clipes[i].name;
                if (origens[i] == fonte && !semLoop.Contains(nm) && !emLoop.Contains(nm) && Regex.IsMatch(nm.ToLower(), "loop|idle|walk|jog|run|sprint")) emLoop.Add(nm);
            }
            ConfigurarFbx(fonte, emLoop);
        }

        // recarrega os clipes depois de reimportar
        Dictionary<string, AnimationClip> porNome = new Dictionary<string, AnimationClip>();
        HashSet<string> arquivos = new HashSet<string>(origens);
        foreach (string arq in arquivos)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(arq))
            {
                AnimationClip c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) porNome[arq + "|" + c.name] = c;
            }

        // 2) cópia do Animator do X Bot (mesmos parâmetros e transições)
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLADOR_NOVO) != null) AssetDatabase.DeleteAsset(CONTROLADOR_NOVO);
        if (!AssetDatabase.CopyAsset(CONTROLADOR_XBOT, CONTROLADOR_NOVO)) { EditorUtility.DisplayDialog("Pinglu", "Não consegui copiar o Animator.", "OK"); return; }
        AnimatorController novo = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLADOR_NOVO);

        List<string> trocadas = new List<string>();
        foreach (ChildAnimatorState cs in novo.layers[0].stateMachine.states)
        {
            AnimatorState st = cs.state;
            string n = st.name.ToLower();
            BlendTree bt = st.motion as BlendTree;
            if (bt != null)
            {
                ChildMotion[] filhos = bt.children;
                for (int k = 0; k < filhos.Length && k < 3; k++)
                {
                    AnimationClip c = Pegar(porNome, nomesEscolhidos[k]);
                    if (c != null) { filhos[k].motion = c; trocadas.Add(ACOES[k] + ": " + c.name); }
                }
                bt.children = filhos;
                EditorUtility.SetDirty(bt);
                continue;
            }
            int acao = -1;
            if (n == "jump") acao = 3;
            else if (n == "roll") acao = 4;
            else if (n == "attack") acao = 5;
            else if (n == "attack_heavy") acao = 6;
            else if (n.Contains("mixamo") || n.Contains("block")) acao = 7;
            if (acao < 0) continue;
            AnimationClip clip = Pegar(porNome, nomesEscolhidos[acao]);
            if (clip != null) { st.motion = clip; trocadas.Add(ACOES[acao] + ": " + clip.name); }
        }
        // golpes 2 e 3 do combo
        AnimatorStateMachine sm = novo.layers[0].stateMachine;
        AnimatorState locomocao = null, golpe1 = null, pesado = null;
        foreach (ChildAnimatorState cs in sm.states)
        {
            if (cs.state.motion is BlendTree) locomocao = cs.state;
            if (cs.state.name == "attack") golpe1 = cs.state;
            if (cs.state.name == "attack_heavy") pesado = cs.state;
        }
        if (locomocao != null && golpe1 != null)
        {
            Motion[] reserva = { pesado != null ? pesado.motion : golpe1.motion, golpe1.motion };
            for (int k = 0; k < 2; k++)
            {
                AnimatorState st = sm.AddState(k == 0 ? "attack_2" : "attack_3", new Vector3(500f, 120f + k * 70f, 0f));
                AnimationClip c = Pegar(porNome, nomesEscolhidos[8 + k]);
                st.motion = c != null ? (Motion)c : reserva[k];
                if (c != null) trocadas.Add(ACOES[8 + k] + ": " + c.name);
                AnimatorStateTransition t = st.AddTransition(locomocao);
                t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.15f; t.hasFixedDuration = true;
            }
            // os golpes voltam para a locomoção só no fim da animação (o combo encadeia antes disso)
            AnimatorState[] golpes = { golpe1, pesado };
            foreach (AnimatorState g in golpes)
            {
                if (g == null) continue;
                foreach (AnimatorStateTransition t in g.transitions)
                    if (t.destinationState == locomocao) { t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.15f; }
            }
        }

        EditorUtility.SetDirty(novo);
        AssetDatabase.SaveAssets();

        // 3) liga no cavaleiro
        Undo.RecordObject(anim, "Animações do cavaleiro");
        anim.runtimeAnimatorController = novo;
        anim.applyRootMotion = false;
        EditorSceneManager.MarkSceneDirty(anim.gameObject.scene);

        string msg = trocadas.Count > 0 ? "Animações trocadas:\n- " + string.Join("\n- ", trocadas) : "Nenhuma animação foi trocada (todas ficaram as do X Bot).";
        msg += "\n\nCombo: clique várias vezes no botão esquerdo do mouse durante o golpe.\nDê Play para testar. Se gostar, salve a cena com Ctrl + S.\nPara voltar, abra esta janela e deixe tudo em \"manter a atual\", ou coloque o XBot_Animator no Animator do cavaleiro.";
        Avatar av = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(caminhoFbx)) if (o is Avatar) av = (Avatar)o;
        if (av == null || !av.isHuman || !av.isValid)
            msg += "\n\nAVISO: o esqueleto das animações não ficou Humanoid válido. Clique no UAL2_Standard > aba Rig > Configure... e veja se tem osso vermelho.";
        EditorUtility.DisplayDialog("Pinglu", msg, "OK");
    }

    static AnimationClip Pegar(Dictionary<string, AnimationClip> d, string nome)
    {
        if (string.IsNullOrEmpty(nome)) return null;
        AnimationClip c; return d.TryGetValue(nome, out c) ? c : null;
    }

    static Animator AcharAnimatorDoJogador()
    {
        GameObject g = GameObject.Find("Jogador (Cavaleiro)");
        if (g != null && g.GetComponentInChildren<Animator>() != null) return g.GetComponentInChildren<Animator>();
        CharacterController cc = Object.FindFirstObjectByType<CharacterController>();
        return cc != null ? cc.GetComponentInChildren<Animator>() : null;
    }
}
#endif
