// =====================================================================
//  PingluDragao.cs  -  Usa o modelo do dragão (.glb) no lobby
// ---------------------------------------------------------------------
//  Pinglu > 30. Dragão (modelo)
//   1ª vez: instala o pacote glTFast (a Unity precisa dele para abrir .glb).
//           Espere instalar e importar, e rode o menu de novo.
//   2ª vez: configura as animações do modelo, mostra o que tem nele
//           (animações, ossos, tamanho) e coloca o dragão no lobby.
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PingluDragao
{
    const string PASTA = "Assets/Personagens/Dragao";
    const string PACOTE = "com.unity.cloud.gltfast";
    static AddRequest pedido;

    [MenuItem("Pinglu/30. Dragão (modelo)")]
    public static void Rodar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        string caminho = AcharModelo();
        if (caminho == null)
        {
            EditorUtility.DisplayDialog("Pinglu", "Não achei nenhum modelo (.glb, .gltf ou .fbx) em " + PASTA + ".", "OK");
            return;
        }

        // 1) a Unity ainda não sabe abrir .glb: instala o glTFast
        AssetImporter imp = AssetImporter.GetAtPath(caminho);
        bool ehGltf = caminho.EndsWith(".glb") || caminho.EndsWith(".gltf");
        if (ehGltf && (imp == null || !imp.GetType().FullName.Contains("Gltf")))
        {
            if (!EditorUtility.DisplayDialog("Pinglu", "Para abrir o dragão (.glb), a Unity precisa do pacote glTFast (gratuito, da própria Unity).\n\nInstalar agora? Depois de instalar, a Unity vai importar o dragão (é grande, pode demorar alguns minutos). Quando terminar, rode este menu de novo.", "Instalar", "Cancelar")) return;
            pedido = Client.Add(PACOTE);
            EditorApplication.update += AcompanharInstalacao;
            return;
        }

        // 2) animações no formato que dá para tocar (Mecanim)
        if (ehGltf) ConfigurarAnimacoes(imp, caminho);

        // 3) o que tem no modelo
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (modelo == null) { EditorUtility.DisplayDialog("Pinglu", "O modelo ainda não foi importado. Espere a Unity terminar e rode de novo.", "OK"); return; }
        AnimationClip[] clipes = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        StringBuilder rel = new StringBuilder();
        Relatorio(modelo, clipes, rel);
        Debug.Log("[Pinglu] Dragão:\n" + rel);

        // 4) coloca no lobby
        string resultado = ColocarNoLobby(modelo, clipes);
        EditorUtility.DisplayDialog("Pinglu", "Dragão analisado!\n\n" + rel + "\n" + resultado + "\n\n(O relatório completo também está no Console.)", "OK");
    }

    static string AcharModelo()
    {
        if (!AssetDatabase.IsValidFolder(PASTA)) return null;
        string[] tipos = { ".glb", ".gltf", ".fbx" };
        foreach (string t in tipos)
            foreach (string guid in AssetDatabase.FindAssets("", new[] { PASTA }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.ToLower().EndsWith(t)) return p;
            }
        return null;
    }

    static void AcompanharInstalacao()
    {
        if (pedido == null || !pedido.IsCompleted) return;
        EditorApplication.update -= AcompanharInstalacao;
        if (pedido.Status == StatusCode.Success)
            EditorUtility.DisplayDialog("Pinglu", "glTFast instalado!\n\nA Unity vai importar o dragão agora (barra de progresso embaixo à direita). Quando terminar, rode Pinglu > 30. Dragão (modelo) de novo.", "OK");
        else
            EditorUtility.DisplayDialog("Pinglu", "Não consegui instalar o glTFast: " + (pedido.Error != null ? pedido.Error.message : "erro desconhecido") +
                "\n\nInstale pelo Window > Package Manager > + > Add package by name: " + PACOTE, "OK");
        pedido = null;
    }

    // muda o importador para gerar animações Mecanim (as que o lobby consegue tocar)
    static void ConfigurarAnimacoes(AssetImporter imp, string caminho)
    {
        SerializedObject so = new SerializedObject(imp);
        SerializedProperty p = so.FindProperty("importSettings.animationMethod") ?? so.FindProperty("m_ImportSettings.animationMethod");
        if (p == null)
        {
            SerializedProperty it = so.GetIterator();
            while (it.Next(true)) if (it.name.ToLower().Contains("animationmethod")) { p = it.Copy(); break; }
        }
        if (p == null || p.propertyType != SerializedPropertyType.Enum) return;
        int mecanim = System.Array.FindIndex(p.enumNames, n => n.ToLower().Contains("mecanim"));
        if (mecanim < 0 || p.enumValueIndex == mecanim) return;
        p.enumValueIndex = mecanim;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.DisplayProgressBar("Pinglu", "Reimportando o dragão com as animações...", 0.5f);
        try { AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate); }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static void Relatorio(GameObject m, AnimationClip[] clipes, StringBuilder r)
    {
        int verts = 0, ossos = 0, malhas = 0;
        foreach (SkinnedMeshRenderer s in m.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            malhas++; ossos = Mathf.Max(ossos, s.bones != null ? s.bones.Length : 0);
            if (s.sharedMesh != null) verts += s.sharedMesh.vertexCount;
        }
        foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>(true)) { malhas++; if (f.sharedMesh != null) verts += f.sharedMesh.vertexCount; }
        Bounds b = Medir(m);
        r.AppendLine("Malhas: " + malhas + "   Vértices: " + verts.ToString("N0"));
        r.AppendLine("Esqueleto: " + (ossos > 0 ? ossos + " ossos" : "NÃO TEM (o dragão fica parado, só voando)"));
        r.AppendLine("Tamanho original: " + b.size.x.ToString("0.0") + " x " + b.size.y.ToString("0.0") + " x " + b.size.z.ToString("0.0"));
        r.AppendLine("Animações: " + (clipes.Length == 0 ? "nenhuma" : ""));
        foreach (AnimationClip c in clipes) r.AppendLine("  - " + c.name + " (" + c.length.ToString("0.0") + " s)");
        Transform boca = AcharOsso(m.transform, new[] { "jaw", "mouth", "tongue", "head", "boca", "cabeca", "mandibula" });
        r.AppendLine("Osso da boca/cabeça: " + (boca != null ? boca.name : "não achei (o fogo sai da frente do modelo)"));
    }

    public static Transform AcharOsso(Transform raiz, string[] nomes)
    {
        foreach (string n in nomes)
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains(n)) return t;
        return null;
    }

    static Bounds Medir(GameObject g)
    {
        Bounds b = new Bounds(); bool tem = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rb = r.bounds;
            SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
            if (sk != null && sk.sharedMesh != null) rb = new Bounds(r.transform.TransformPoint(sk.sharedMesh.bounds.center), Vector3.Scale(sk.sharedMesh.bounds.size, r.transform.lossyScale));
            if (!tem) { b = rb; tem = true; } else b.Encapsulate(rb);
        }
        return b;
    }

    // escolhe as animações pelo nome: voo (bater asas), planar e sopro de fogo
    static AnimationClip Escolher(AnimationClip[] c, string[] chaves, AnimationClip evitar = null)
    {
        foreach (string k in chaves)
            foreach (AnimationClip a in c)
                if (a != evitar && a.name.ToLower().Contains(k)) return a;
        return null;
    }

    static string ColocarNoLobby(GameObject modelo, AnimationClip[] clipes)
    {
        GameObject cen = GameObject.Find("Cenario do Lobby");
        DragaoDistante d = cen != null ? cen.GetComponentInChildren<DragaoDistante>(true) : null;
        if (d == null)
        {
            GameObject g = new GameObject("Dragao (chefe final)");
            if (cen != null) g.transform.SetParent(cen.transform, false);
            else g.transform.position = new Vector3(0f, -3000f, 0f);
            d = g.AddComponent<DragaoDistante>();
        }
        Undo.RecordObject(d, "Dragao");
        d.modelo = modelo;
        d.animVoo = Escolher(clipes, new[] { "fly", "flap", "voar", "voo", "wing" });
        d.animPlanar = Escolher(clipes, new[] { "glide", "planar", "hover", "soar" }, d.animVoo);
        d.animSopro = Escolher(clipes, new[] { "fire", "breath", "flame", "sopro", "fogo", "attack", "roar" }, d.animVoo);
        if (d.animVoo == null && clipes.Length > 0) d.animVoo = clipes[0];
        if (d.materialParticula == null) d.materialParticula = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        EditorUtility.SetDirty(d);
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
        return "No lobby: voo = " + Nome(d.animVoo) + ", planar = " + Nome(d.animPlanar) + ", sopro = " + Nome(d.animSopro) +
               "\nSALVE A CENA (Ctrl + S) e dê Play.";
    }

    static string Nome(AnimationClip c) { return c != null ? c.name : "(nenhuma)"; }
}
#endif
