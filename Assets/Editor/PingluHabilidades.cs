using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// ==========================================================================================
//  Pinglu > 76. Habilidades especiais dos personagens
//  Liga as 3 habilidades (teclas 1, 2, 3) de cada personagem à barra azul de energia:
//    1) deixa os efeitos da pasta Assets/Ataques compatíveis com o URP (senão ficam rosa);
//    2) procura as animações do Mixamo (arquivos .fbx com o nome da animação) e as prepara como Humanoid;
//    3) põe o componente HabilidadesDoPersonagem em cada prefab de jogador (e nos jogadores soltos na cena)
//       e cria no Animator uma camada "Habilidades" com as animações.
//  Pode rodar de novo quando colocar mais animações do Mixamo: ele só refaz as ligações.
// ==========================================================================================
public static class PingluHabilidades
{
    const string PASTA_EFEITOS = "Assets/Ataques";
    const string PASTA_JOGADORES = "Assets/Personagens/Jogadores";

    class Def
    {
        public string nome, desc, vfx1, vfx2, estadoAlternativo = "attack_heavy";
        public string[][] animacao;            // tentativas de nome do arquivo do Mixamo (palavras que o nome precisa ter)
        public float custo, recarga, dano, raio, valor, duracaoEfeito, momento, duracao, escala = 1f;
        public Vector3 rot;
    }

    // ---- as 12 habilidades do guia
    static Def[] Cavaleiro() { return new[]
    {
        new Def { nome = "Corte Vertical", desc = "Um único grande corte vertical que causa dano.", custo = 0.33f, recarga = 1f, dano = 90f, momento = 0.45f, duracao = 1.4f,
                  vfx1 = "Stone slash", vfx2 = "Smoke vortex", rot = new Vector3(0f, 0f, 90f), animacao = new[] { new[] { "sword", "inward", "slash" }, new[] { "inward", "slash" } } },
        new Def { nome = "Bicuda", desc = "Um chute com dano extremamente elevado.", custo = 0.66f, recarga = 2f, dano = 160f, momento = 0.45f, duracao = 1.1f,
                  animacao = new[] { new[] { "melee", "attack", "kick" }, new[] { "melee", "kick" }, new[] { "kick" } } },
        new Def { nome = "Aplicar Julgamento", desc = "Ultimate: crava a espada no chão e causa dano em área.", custo = 1f, recarga = 4f, dano = 220f, raio = 9f, momento = 0.6f, duracao = 2f, escala = 1.5f,
                  vfx1 = "Ground AOE explosion", vfx2 = "Holy hit", animacao = new[] { new[] { "sword", "shield", "casting" } } },
    }; }

    static Def[] Tanque() { return new[]
    {
        new Def { nome = "Provocar", desc = "Provoca os inimigos, que passam a atacar só o Tank.", custo = 0.33f, recarga = 1.5f, raio = 20f, duracaoEfeito = 6f, momento = 0.4f, duracao = 1.8f,
                  vfx1 = "Red energy explosion", animacao = new[] { new[] { "taunt", "chest" }, new[] { "taunt" } } },
        new Def { nome = "Tremor", desc = "Mortal para trás; a explosão causa dano e deixa os inimigos lentos.", custo = 0.66f, recarga = 2f, dano = 70f, raio = 8f, duracaoEfeito = 5f, momento = 0.6f, duracao = 1.6f,
                  vfx1 = "Dust ground", animacao = new[] { new[] { "backflip" } } },
        new Def { nome = "O Imbatível", desc = "Ultimate: a vida não cai por 5 segundos.", custo = 1f, recarga = 4f, duracaoEfeito = 5f, momento = 0.35f, duracao = 1.8f,
                  vfx1 = "Lightning aura", animacao = new[] { new[] { "sword", "shield", "power" }, new[] { "power", "up" } } },
    }; }

    static Def[] Maga() { return new[]
    {
        new Def { nome = "Queda de Raios", desc = "Chuva de raios por 10 segundos, 2 por segundo.", custo = 0.33f, recarga = 1.5f, dano = 28f, raio = 12f, duracaoEfeito = 10f, momento = 0.5f, duracao = 2f,
                  vfx1 = "Electro hit", animacao = new[] { new[] { "2h", "magic", "area", "attack", "01" }, new[] { "2h", "area", "01" } } },
        new Def { nome = "Impulso Cortante", desc = "Atira um corte mágico para frente.", custo = 0.66f, recarga = 2f, dano = 90f, momento = 0.45f, duracao = 1.3f,
                  vfx1 = "Portal blue", vfx2 = "Charge slash purple", animacao = new[] { new[] { "1h", "magic", "attack", "01" }, new[] { "1h", "magic", "01" } } },
        new Def { nome = "Tormenta Final", desc = "Ultimate: grande círculo mágico que causa muito dano e some devagar.", custo = 1f, recarga = 4f, dano = 250f, raio = 11f, duracaoEfeito = 7f, momento = 0.5f, duracao = 2.2f, escala = 1.5f,
                  vfx1 = "Magic circle 2", animacao = new[] { new[] { "great", "sword", "casting" } } },
    }; }

    static Def[] Curandeiro() { return new[]
    {
        new Def { nome = "Curar", desc = "Cura 20 pontos de vida de todos do grupo.", custo = 0.33f, recarga = 1.5f, valor = 20f, momento = 0.5f, duracao = 1.5f,
                  vfx1 = "Healing", animacao = new[] { new[] { "2h", "cast", "spell", "01" }, new[] { "2h", "cast", "spell" } } },
        new Def { nome = "Impulsionar", desc = "Por 10 segundos, o grupo causa 20% mais dano e anda 20% mais rápido.", custo = 0.66f, recarga = 2f, duracaoEfeito = 10f, momento = 0.45f, duracao = 1.3f,
                  vfx1 = "Buff", animacao = new[] { new[] { "1h", "magic", "attack", "03" }, new[] { "1h", "magic", "03" } } },
        new Def { nome = "Até a Morte", desc = "Ultimate: área que cura 5 de vida por segundo durante 30 segundos.", custo = 1f, recarga = 4f, valor = 5f, raio = 7.5f, duracaoEfeito = 30f, momento = 0.5f, duracao = 2f,
                  vfx1 = "Healing circle", animacao = new[] { new[] { "2h", "magic", "area", "attack", "02" }, new[] { "2h", "area", "02" } } },
    }; }

    [MenuItem("Pinglu/76. Habilidades especiais dos personagens")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou ligar as habilidades especiais (teclas 1, 2, 3) aos 5 personagens:\n\n• deixar os efeitos da pasta Ataques compatíveis com o URP\n• procurar as animações do Mixamo e preparar como Humanoid\n• colocar as habilidades nos prefabs de jogador e criar a camada \"Habilidades\" no Animator\n\nO que o Mixamo ainda não tiver usa uma animação reserva. Pode rodar de novo depois de colocar mais animações.", "Continuar", "Cancelar")) return;

        var log = new StringBuilder();
        try
        {
            EditorUtility.DisplayProgressBar("Habilidades", "Ajustando os efeitos para o URP...", 0.1f);
            int convertidos = ConverterMateriais(log);

            EditorUtility.DisplayProgressBar("Habilidades", "Procurando as animações do Mixamo...", 0.3f);
            int total = 0, achadas = 0;
            string[] nomes = { "Cavaleiro", "CavaleiroSombrio", "Tanque", "Maga", "Curandeiro" };
            for (int n = 0; n < nomes.Length; n++)
            {
                string caminho = PASTA_JOGADORES + "/Jogador_" + nomes[n] + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(caminho) == null) { log.AppendLine("- não achei " + caminho); continue; }
                EditorUtility.DisplayProgressBar("Habilidades", "Preparando " + nomes[n] + "...", 0.4f + 0.1f * n);
                GameObject raiz = PrefabUtility.LoadPrefabContents(caminho);
                try
                {
                    int a, t; Configurar(raiz, log, out a, out t); achadas += a; total += t;
                    PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
            }

            // jogadores soltos na cena que não vêm de um prefab com as habilidades
            int naCena = 0;
            foreach (PlayerLocomotion p in Object.FindObjectsByType<PlayerLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (p == null || p.GetComponent<HabilidadesDoPersonagem>() != null) continue;
                int a, t; Configurar(p.gameObject, log, out a, out t); naCena++;
                EditorUtility.SetDirty(p.gameObject);
            }
            if (naCena > 0)
            {
                var cena = EditorSceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(cena.path) && File.Exists(cena.path))
                {
                    Directory.CreateDirectory("Backups");
                    string copia = "Backups/" + Path.GetFileNameWithoutExtension(cena.path) + "_antes_das_habilidades.unity";
                    if (!File.Exists(copia)) File.Copy(cena.path, copia, false);
                }
                EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveOpenScenes();
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();

            string msg = "Habilidades ligadas!\n\n• " + convertidos + " materiais de efeito convertidos para o URP\n• animações do Mixamo encontradas: " + achadas + " de " + total + " (o resto usa uma animação reserva)\n• " + naCena + " jogador(es) solto(s) na cena configurado(s)\n\nPlay > escolha o personagem > ganhe energia batendo (barra azul) e aperte 1, 2 ou 3.";
            if (achadas < total) msg += "\n\nPara as animações que faltam, baixe do Mixamo (formato FBX, \"In Place\" marcado) e coloque na pasta Assets/Personagens/Habilidades. Rode o menu 76 de novo.";
            if (log.Length > 0) msg += "\n\nAvisos:\n" + log;
            Debug.Log("[Habilidades]\n" + msg);
            EditorUtility.DisplayDialog("Pinglu", msg, "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ------------------------------------------------------------------ personagem
    static void Configurar(GameObject raiz, StringBuilder log, out int achadas, out int total)
    {
        achadas = 0; total = 0;
        ClasseDeHabilidade classe = ClasseDe(raiz);
        Def[] defs = classe == ClasseDeHabilidade.Maga ? Maga() : classe == ClasseDeHabilidade.Tanque ? Tanque() : classe == ClasseDeHabilidade.Curandeiro ? Curandeiro() : Cavaleiro();

        HabilidadesDoPersonagem h = raiz.GetComponent<HabilidadesDoPersonagem>();
        if (h == null) h = raiz.AddComponent<HabilidadesDoPersonagem>();
        h.classe = classe;
        h.regenPorSegundo = classe == ClasseDeHabilidade.Curandeiro ? 1f : classe == ClasseDeHabilidade.Maga ? 0.5f : 0f;
        h.materialRaio = Material("Electro");
        h.habilidades = new DadosDeHabilidade[3];
        AnimationClip[] clipes = new AnimationClip[3];
        for (int i = 0; i < 3; i++)
        {
            Def d = defs[i]; total++;
            AnimationClip clipe = ProcurarAnimacao(d.animacao);
            clipes[i] = clipe; if (clipe != null) achadas++;
            h.habilidades[i] = new DadosDeHabilidade
            {
                nome = d.nome, descricao = d.desc, custo = d.custo, recarga = d.recarga, clipe = clipe, estadoAlternativo = d.estadoAlternativo,
                duracao = d.duracao, momentoDoEfeito = d.momento, dano = d.dano, raio = d.raio, valor = d.valor, duracaoEfeito = d.duracaoEfeito,
                efeito = string.IsNullOrEmpty(d.vfx1) ? null : Prefab(d.vfx1, log), efeito2 = string.IsNullOrEmpty(d.vfx2) ? null : Prefab(d.vfx2, log),
                rotacaoDoEfeito = d.rot, escalaDoEfeito = d.escala,
            };
        }

        Animator anim = raiz.GetComponent<Animator>(); if (anim == null) anim = raiz.GetComponentInChildren<Animator>();
        AnimatorController ctrl = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
        if (ctrl != null) LigarAnimacoes(ctrl, clipes);
        else log.AppendLine("- " + raiz.name + ": o Animator não usa um AnimatorController direto (sem camada de habilidades; vai usar a animação reserva).");
        EditorUtility.SetDirty(raiz);
    }

    static ClasseDeHabilidade ClasseDe(GameObject g)
    {
        if (g.GetComponent<MagiaJogador>() != null) return ClasseDeHabilidade.Maga;
        if (g.GetComponent<CuraJogador>() != null) return ClasseDeHabilidade.Curandeiro;
        System.Type tq = System.Type.GetType("EscudoTanque");
        if (tq != null && g.GetComponent(tq) != null) return ClasseDeHabilidade.Tanque;
        if (g.GetComponent<VooTanque>() != null) return ClasseDeHabilidade.Tanque;
        return ClasseDeHabilidade.Cavaleiro;
    }

    // ------------------------------------------------------------------ Animator
    static void LigarAnimacoes(AnimatorController c, AnimationClip[] clipes)
    {
        for (int i = c.layers.Length - 1; i >= 0; i--) if (c.layers[i].name == "Habilidades") c.RemoveLayer(i);
        if (clipes.All(x => x == null)) return;

        c.AddLayer("Habilidades");
        AnimatorControllerLayer[] ls = c.layers;
        int idx = ls.Length - 1;
        ls[idx].defaultWeight = 1f; ls[idx].blendingMode = AnimatorLayerBlendingMode.Override;
        c.layers = ls;

        AnimatorStateMachine sm = c.layers[idx].stateMachine;
        AnimatorState vazio = sm.AddState("Vazio");              // estado sem animação: deixa as outras camadas aparecerem
        sm.defaultState = vazio;
        for (int i = 0; i < clipes.Length; i++)
        {
            if (clipes[i] == null) continue;
            AnimatorState s = sm.AddState("Hab" + (i + 1));
            s.motion = clipes[i]; s.writeDefaultValues = false;
            AnimatorStateTransition t = s.AddTransition(vazio);
            t.hasExitTime = true; t.exitTime = 0.95f; t.hasFixedDuration = true; t.duration = 0.12f;
        }
        EditorUtility.SetDirty(c);
    }

    // ------------------------------------------------------------------ Mixamo
    static string Norm(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s.ToLowerInvariant()) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    static AnimationClip ProcurarAnimacao(string[][] tentativas)
    {
        string[] guids = AssetDatabase.FindAssets("t:Model");
        foreach (string[] palavras in tentativas)
        {
            string melhor = null; int menor = int.MaxValue;
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) || p.StartsWith(PASTA_EFEITOS)) continue;
                string n = Norm(Path.GetFileNameWithoutExtension(p));
                if (n.Contains("rigged") || n.Contains("@")) continue;
                if (!palavras.All(w => n.Contains(w))) continue;
                if (n.Length < menor) { menor = n.Length; melhor = p; }
            }
            if (melhor == null) continue;
            PrepararFbx(melhor);
            AnimationClip clipe = AssetDatabase.LoadAllAssetsAtPath(melhor).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clipe != null) return clipe;
        }
        return null;
    }

    // Humanoid (para servir em qualquer personagem), sem sair do lugar e sem repetir
    static void PrepararFbx(string caminho)
    {
        ModelImporter mi = AssetImporter.GetAtPath(caminho) as ModelImporter;
        if (mi == null) return;
        bool mudou = false;
        if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mudou = true; }
        ModelImporterClipAnimation[] clips = mi.clipAnimations;
        if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in clips)
        {
            if (c.loopTime || !c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ) mudou = true;
            c.loopTime = false; c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
        }
        mi.clipAnimations = clips;
        if (mudou) mi.SaveAndReimport();
    }

    // ------------------------------------------------------------------ efeitos (Hovl) no URP
    static GameObject Prefab(string nome, StringBuilder log)
    {
        foreach (string g in AssetDatabase.FindAssets(nome + " t:Prefab", new[] { PASTA_EFEITOS }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == nome) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        log.AppendLine("- não achei o efeito \"" + nome + "\" em " + PASTA_EFEITOS);
        return null;
    }

    static Material Material(string nome)
    {
        foreach (string g in AssetDatabase.FindAssets(nome + " t:Material", new[] { PASTA_EFEITOS }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == nome) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }

    public static int ConverterMateriais(StringBuilder log)
    {
        Shader particulas = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (particulas == null || lit == null) { log.AppendLine("- o URP não está instalado; os efeitos podem ficar rosa."); return 0; }
        int n = 0, estranhos = 0;
        foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { PASTA_EFEITOS }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null || m.shader == null) continue;
            string sn = m.shader.name;
            if (sn.StartsWith("Universal Render Pipeline")) continue;

            Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Color cor = Color.white;
            if (m.HasProperty("_Color")) cor = m.GetColor("_Color"); else if (m.HasProperty("_TintColor")) cor = m.GetColor("_TintColor");
            Vector2 esc = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 des = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;

            bool particula = sn.StartsWith("Particles/") || sn.StartsWith("Legacy Shaders/Particles") || sn.Contains("Particle");
            if (sn == "Standard" || sn.StartsWith("Legacy Shaders/Diffuse") || sn.StartsWith("Mobile/"))
            {
                m.shader = lit;
                m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", cor);
                n++; EditorUtility.SetDirty(m); continue;
            }
            if (!particula) { estranhos++; continue; }

            bool aditivo = sn.Contains("Additive") || (m.HasProperty("_DstBlend") && Mathf.Approximately(m.GetFloat("_DstBlend"), 1f)) || (m.HasProperty("_Mode") && Mathf.Approximately(m.GetFloat("_Mode"), 4f));
            m.shader = particulas;
            m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", esc); m.SetTextureOffset("_BaseMap", des);
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", aditivo ? 2f : 0f); m.SetFloat("_Cull", 0f); m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", aditivo ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", aditivo ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            n++; EditorUtility.SetDirty(m);
        }
        if (estranhos > 0) log.AppendLine("- " + estranhos + " materiais de efeito com shader próprio (distorção) ficaram como estavam; se algum aparecer rosa, me avise o nome.");
        return n;
    }
}
