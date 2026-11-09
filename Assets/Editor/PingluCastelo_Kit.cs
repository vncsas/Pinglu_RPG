// =====================================================================
//  PingluCastelo_Kit.cs  -  props de castelo prontos (Kenney "Castle Kit", licença CC0)
//  Máquinas de cerco destruídas, estandartes, troncos e pedras em volta do pátio.
//  Os modelos ficam em Assets/Cenario/Castelo/Kit_Kenney. O material é escurecido para combinar com o clima sombrio.
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static partial class PingluVila
{
    const string PASTA_KIT = PASTA_CASTELO + "/Kit_Kenney";
    static Material kitMaterial;
    static int kitFaltando;

    static Material KitMaterial()
    {
        if (kitMaterial != null) return kitMaterial;
        string caminho = PASTA_CASTELO + "/Materiais/Kit_Escuro.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, caminho); }
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PASTA_KIT + "/Textures/colormap.png");
        if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
        m.SetColor("_BaseColor", new Color(0.46f, 0.42f, 0.40f, 1f));     // escurece e dessatura as cores vivas do kit
        m.SetFloat("_Smoothness", 0.05f); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        kitMaterial = m;
        return m;
    }

    // coloca um modelo do kit no castelo. "local" é a posição no castelo; "alvo" é o maior tamanho (m) que o modelo deve ter.
    static GameObject CKit(string nome, Vector3 local, float yaw, float alvo, Transform pai, bool colisor, Vector3 inclinar)
    {
        return CModelo(PASTA_KIT + "/" + nome + ".glb", nome, castC, castRot, local, yaw, alvo, false, true, float.NaN, pai, colisor, inclinar);
    }
    static GameObject CKit(string nome, Vector3 local, float yaw, float alvo, Transform pai, bool colisor) { return CKit(nome, local, yaw, alvo, pai, colisor, Vector3.zero); }

    // coloca QUALQUER modelo (.glb) numa posição: "origem/rotOrigem" é o sistema local (o castelo), "local" a posição nele.
    // alvo = tamanho final em metros (do maior lado, ou da altura se porAltura). tingir = usa o material escuro do kit.
    // yBase = altura local da base (NaN = encosta no chão). Devolve null se o modelo não foi importado.
    static GameObject CModelo(string caminho, string nome, Vector3 origem, Quaternion rotOrigem, Vector3 local, float yaw, float alvo, bool porAltura,
                              bool tingir, float yBase, Transform pai, bool colisor, Vector3 inclinar)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab == null) { kitFaltando++; return null; }
        GameObject go = PrefabUtility.InstantiatePrefab(prefab, pai) as GameObject;
        if (go == null) { kitFaltando++; return null; }
        go.name = nome;
        if (tingir)
        {
            Material mat = KitMaterial();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                Material[] ms = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                r.sharedMaterials = ms;
            }
        }
        // o modelo .glb traz a própria rotação no objeto raiz (ex.: Sketchfab: eixo Z para cima -> Y para cima). Preciso MANTER essa rotação,
        // senão a casa/cadeira fica deitada ou de cabeça para baixo.
        Quaternion rotDoModelo = prefab.transform.localRotation;
        go.transform.rotation = rotOrigem * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(inclinar) * rotDoModelo;
        go.transform.localScale = Vector3.one;
        go.transform.position = origem + rotOrigem * local;
        Bounds b = BoundsDoKit(go);
        float medida = porAltura ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (medida > 0.001f) go.transform.localScale = Vector3.one * (alvo / medida);
        b = BoundsDoKit(go);
        float yMundo = origem.y + (float.IsNaN(yBase) ? Cg(local.x, local.z) : yBase);
        go.transform.position += Vector3.up * (yMundo - 0.1f * (float.IsNaN(yBase) ? 1f : 0f) - b.min.y);        // a base do modelo toca o chão
        foreach (Transform t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        if (colisor)
        {
            Bounds bl = new Bounds(); bool pri = true;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 q = go.transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (pri) { bl = new Bounds(q, Vector3.zero); pri = false; } else bl.Encapsulate(q);
                }
            }
            if (!pri)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();      // uma caixa só (modelos com milhões de triângulos não podem ter MeshCollider)
                bc.center = bl.center; bc.size = bl.size;
            }
        }
        return go;
    }

    static Bounds BoundsDoKit(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(go.transform.position, Vector3.zero); bool primeiro = true;
        foreach (Renderer r in rs) { if (primeiro) { b = r.bounds; primeiro = false; } else b.Encapsulate(r.bounds); }
        return b;
    }

    // espalha as peças do kit pelo pátio (chamado de CPatio). Devolve false se o kit não foi importado.
    static bool CKitPatio()
    {
        kitMaterial = null; kitFaltando = 0;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PASTA_KIT + "/siege-ram-demolished.glb") == null) return false;
        Transform pai = Pasta("Props do Kit");
        float xi = CAST_L / 2 - 14f;
        System.Func<float, float, float, float, float, Vector3> ponto = (x0, x1, z0, z1, r) => CPonto(x0, x1, z0, z1, r);
        Vector3 p;
        p = ponto(-xi + 6f, -12f, -22f, 12f, 9f);   if (!float.IsNaN(p.x)) CKit("siege-catapult-demolished", p, Faixa(0f, 360f), 8f, pai, true);
        p = ponto(12f, xi - 6f, -26f, -8f, 11f);    if (!float.IsNaN(p.x)) CKit("siege-trebuchet-demolished", p, Faixa(0f, 360f), 13f, pai, true);
        p = ponto(-14f, 14f, CAST_P / 2 - 44f, CAST_P / 2 - 16f, 8f); if (!float.IsNaN(p.x)) CKit("siege-ram-demolished", p, Faixa(-30f, 30f), 7f, pai, true);
        p = ponto(-xi + 4f, xi - 4f, 6f, 36f, 10f); if (!float.IsNaN(p.x)) CKit("siege-tower-demolished", p, Faixa(0f, 360f), 11f, pai, true);
        for (int i = 0; i < 2; i++) { p = ponto(-xi + 4f, xi - 4f, -30f, 40f, 4f); if (!float.IsNaN(p.x)) CKit("siege-ballista-demolished", p, Faixa(0f, 360f), 4.2f, pai, true); }
        for (int i = 0; i < 6; i++) { p = ponto(-xi, xi, -CAST_P / 2 + 10f, CAST_P / 2 - 10f, 3f); if (!float.IsNaN(p.x)) CKit("tree-log", p, Faixa(0f, 360f), Faixa(3.5f, 6f), pai, false); }
        for (int i = 0; i < 12; i++) { p = ponto(-xi, xi, -CAST_P / 2 + 10f, CAST_P / 2 - 10f, 1.5f); if (!float.IsNaN(p.x)) CKit(i % 3 == 0 ? "rocks-large" : "rocks-small", p, Faixa(0f, 360f), i % 3 == 0 ? Faixa(2.5f, 4f) : Faixa(1.2f, 2f), pai, false); }
        // estandartes: alguns de pé inclinados, outros tombados
        for (int i = 0; i < 8; i++)
        {
            p = ponto(-xi + 4f, xi - 4f, -CAST_P / 2 + 14f, CAST_P / 2 - 14f, 3f);
            if (float.IsNaN(p.x)) continue;
            string[] nomes = { "flag-banner-long", "flag-banner-short", "flag-pennant", "flag" };
            CKit(nomes[i % 4], p, Faixa(0f, 360f), Faixa(4.5f, 7f), pai, false, i % 2 == 0 ? new Vector3(Faixa(-14f, 14f), 0f, Faixa(-14f, 14f)) : new Vector3(Faixa(60f, 85f), 0f, 0f));
        }
        return true;
    }
}
#endif
