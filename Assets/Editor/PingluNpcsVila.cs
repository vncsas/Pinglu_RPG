using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ==========================================================================================
//  Pinglu > 73. NPCs da vila: montar e colocar   (Play desligado)
//  Usa os 3 NPCs do colega (Civil 2, Civil 3 e Mulher 1):
//   1) prepara cada um (Humanoid, texturas, materiais URP, animações e Animator) rodando os passos do menu
//      "Civil2/Civil/Woman1 > Montar tudo", MAS sem trocar a cena aberta (esses menus abrem uma cena de teste);
//   2) coloca os moradores na praça da vila da cena ABERTA: conversando, varrendo e passeando.
//  Pinglu > 74 remove os moradores colocados.
// ==========================================================================================
public static class PingluNpcsVila
{
    const string RAIZ = "NPCs da Vila";

    class Tipo { public string nome, pasta, modelo, controlador, montador; public GameObject prefab; public RuntimeAnimatorController ctrl; }

    static readonly Tipo[] TIPOS =
    {
        new Tipo { nome = "Civil 2", pasta = "Assets/Personagens/Civil2", modelo = "civil2_rigged.fbx", controlador = "Civil2.controller", montador = "MontarCivil2" },
        new Tipo { nome = "Civil 3", pasta = "Assets/Personagens/Civil3", modelo = "civil_rigged.fbx", controlador = "Civil.controller", montador = "MontarCivil" },
        new Tipo { nome = "Mulher 1", pasta = "Assets/Personagens/Woman1", modelo = "woman1_rigged.fbx", controlador = "Woman1.controller", montador = "MontarWoman1" },
    };

    [MenuItem("Pinglu/73. NPCs da vila: montar e colocar")]
    public static void Colocar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        Transform poco = AcharPoco();
        if (poco == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei a praça da vila (objeto \"Poco\") na cena aberta.\n\nAbra a cena do jogo (SampleScene_Ambientacao ou SampleScene) e rode de novo.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Pinglu", "Vou preparar os 3 NPCs (pode demorar alguns minutos na primeira vez, o Unity importa as animações) e colocar os moradores na vila desta cena.\n\nA cena será salva no final.", "Continuar", "Cancelar")) return;

        var log = new System.Text.StringBuilder();
        try
        {
            for (int i = 0; i < TIPOS.Length; i++)
            {
                Tipo t = TIPOS[i];
                EditorUtility.DisplayProgressBar("NPCs da vila", "Preparando " + t.nome + "...", i / (float)TIPOS.Length);
                Preparar(t, log);
                t.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(t.pasta + "/" + t.modelo);
                t.ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(t.pasta + "/" + t.controlador);
                if (t.prefab == null || t.ctrl == null) log.AppendLine("- " + t.nome + ": faltou " + (t.prefab == null ? "o modelo" : "o Animator") + ".");
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        int n = Posicionar(poco, log);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("Pinglu", n + " moradores colocados ao redor da praça (grupo \"" + RAIZ + "\").\n\nAperte Play e vá até a praça.\n" + (log.Length > 0 ? "\nAvisos:\n" + log : ""), "OK");
    }

    [MenuItem("Pinglu/74. NPCs da vila: remover")]
    public static void Remover()
    {
        GameObject r = GameObject.Find(RAIZ);
        if (r == null) { EditorUtility.DisplayDialog("Pinglu", "Nenhum morador na cena aberta.", "OK"); return; }
        Object.DestroyImmediate(r);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
    }

    static Transform AcharPoco()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.name == "Poco") return t;
        return null;
    }

    // roda os mesmos passos do "Montar tudo" do colega, menos a criação da cena de teste
    static void Preparar(Tipo t, System.Text.StringBuilder log)
    {
        if (File.Exists(t.pasta + "/" + t.controlador) && AssetDatabase.LoadAssetAtPath<GameObject>(t.pasta + "/" + t.modelo) != null) return;   // já montado
        System.Type tipo = typeof(PingluNpcsVila).Assembly.GetType(t.montador);
        if (tipo == null) { log.AppendLine("- " + t.nome + ": não achei o script " + t.montador + " do colega."); return; }
        const BindingFlags F = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        try
        {
            tipo.GetMethod("Texturas", F).Invoke(null, null);
            object avatar = tipo.GetMethod("Personagem", F).Invoke(null, null);
            tipo.GetMethod("ImportarClipes", F).Invoke(null, new[] { avatar });
            tipo.GetMethod("Controlador", F).Invoke(null, null);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        catch (System.Exception e) { log.AppendLine("- " + t.nome + ": erro ao montar (" + (e.InnerException != null ? e.InnerException.Message : e.Message) + ")."); Debug.LogWarning("[NPCs] " + t.nome + ": " + e); }
    }

    static Vector3 Chao(Vector3 p)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.NegativeInfinity;
        foreach (RaycastHit h in hits) if (h.point.y > melhor) melhor = h.point.y;
        if (!float.IsNegativeInfinity(melhor)) { p.y = melhor; return p; }
        Terrain t = Terrain.activeTerrain; if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y;
        return p;
    }

    static int Posicionar(Transform poco, System.Text.StringBuilder log)
    {
        GameObject velho = GameObject.Find(RAIZ); if (velho != null) Object.DestroyImmediate(velho);
        GameObject raiz = new GameObject(RAIZ);
        Undo.RegisterCreatedObjectUndo(raiz, "NPCs da vila");

        // referência: a praça (poço) e o lado do portão, como o Caio
        Vector3 c = poco.position; Vector3 dir = Vector3.forward;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.name == "Portal da Vila") { dir = t.position - c; dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward; break; }
        Vector3 lado = new Vector3(-dir.z, 0f, dir.x);

        int n = 0;
        // 1) conversa: o Civil 3 fala e a Mulher 1 ouve, de frente um para o outro
        Vector3 pConversa = c - dir * 4.2f + lado * 3.4f;
        GameObject falante = Criar(raiz, TIPOS[1], "Civil 3 (conversando)", pConversa, 0f, NpcDaVila.Modo.Parado, "Falar", false, false); n++;
        GameObject ouvinte = Criar(raiz, TIPOS[2], "Mulher 1 (ouvindo)", pConversa + lado * 1.35f, 0f, NpcDaVila.Modo.Parado, "Ouvir", false, false); n++;
        Olhar(falante, ouvinte); Olhar(ouvinte, falante);
        if (falante != null && ouvinte != null) { falante.GetComponent<NpcDaVila>().olharPara = ouvinte.transform; ouvinte.GetComponent<NpcDaVila>().olharPara = falante.transform; }

        // 2) varredor: Civil 2 varrendo a calçada na beira da praça
        GameObject varre = Criar(raiz, TIPOS[0], "Civil 2 (varrendo)", c - dir * 2.2f - lado * 5.4f, 0f, NpcDaVila.Modo.Parado, "Varrer", true, true); n++;
        if (varre != null) varre.transform.rotation = Quaternion.LookRotation(lado * 0.7f + dir * 0.7f);

        // 3) dois passeando em volta do poço, em sentidos diferentes
        GameObject p1 = Criar(raiz, TIPOS[0], "Civil 2 (passeando)", c + dir * 6.5f, 0f, NpcDaVila.Modo.Ronda, "Parado", false, true); n++;
        if (p1 != null) p1.GetComponent<NpcDaVila>().pontos = Anel(c, 6.8f, 7, 0f, false);
        GameObject p2 = Criar(raiz, TIPOS[2], "Mulher 1 (passeando)", c - dir * 6.8f, 0f, NpcDaVila.Modo.Ronda, "Parado", false, true); n++;
        if (p2 != null) p2.GetComponent<NpcDaVila>().pontos = Anel(c, 7.4f, 7, 3.14f, true);

        // 4) um Civil 3 parado perto do poço, escutando quem chega
        GameObject parado = Criar(raiz, TIPOS[1], "Civil 3 (na praça)", c + lado * 3.6f + dir * 1.2f, 0f, NpcDaVila.Modo.Parado, "Parado", false, true); n++;
        if (parado != null) parado.transform.rotation = Quaternion.LookRotation(-lado);
        return n;
    }

    static Vector3[] Anel(Vector3 c, float raio, int quantos, float inicio, bool contrario)
    {
        Vector3[] v = new Vector3[quantos];
        for (int i = 0; i < quantos; i++)
        {
            float a = inicio + (contrario ? -1f : 1f) * i * Mathf.PI * 2f / quantos;
            v[i] = Chao(c + new Vector3(Mathf.Cos(a) * raio, 0f, Mathf.Sin(a) * raio));
        }
        return v;
    }

    static void Olhar(GameObject quem, GameObject para)
    {
        if (quem == null || para == null) return;
        Vector3 d = para.transform.position - quem.transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) quem.transform.rotation = Quaternion.LookRotation(d);
    }

    static GameObject Criar(GameObject raiz, Tipo t, string nome, Vector3 pos, float giro, NpcDaVila.Modo modo, string atividade, bool vassoura, bool reagir)
    {
        if (t.prefab == null || t.ctrl == null) return null;
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(t.prefab);
        g.name = nome; g.transform.SetParent(raiz.transform, false);
        g.transform.position = Chao(pos); g.transform.rotation = Quaternion.Euler(0f, giro, 0f);
        Animator a = g.GetComponent<Animator>(); if (a == null) a = g.AddComponent<Animator>();
        a.runtimeAnimatorController = t.ctrl; a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        CapsuleCollider cap = g.AddComponent<CapsuleCollider>(); cap.radius = 0.35f; cap.height = 1.8f; cap.center = new Vector3(0f, 0.9f, 0f);
        NpcDaVila npc = g.AddComponent<NpcDaVila>();
        npc.modo = modo; npc.atividade = atividade; npc.temVassoura = vassoura; npc.reagirAoJogador = reagir; npc.foge = true;
        Undo.RegisterCreatedObjectUndo(g, "NPC");
        return g;
    }
}
