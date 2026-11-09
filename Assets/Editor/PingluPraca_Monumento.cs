#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Reconstrói o monumento central do cemitério (degraus, coluna, cruz celta e velas) com as mesmas medidas do gerador original.
// Serve para a cena do Guardião poder quebrar o monumento em pedaços, mesmo que a malha do cemitério já tenha sido editada.
public static partial class PingluVila
{
    static Material CarregarMatCem(string nome)
    {
        foreach (string g in AssetDatabase.FindAssets(nome + " t:Material", new[] { PASTA_MAT }))
        {
            string c = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(c) == nome) return AssetDatabase.LoadAssetAtPath<Material>(c);
        }
        return null;
    }

    // topo = altura (local) do piso da praça; o monumento fica no centro (x = 0, z = 0)
    public static Mesh MonumentoDaPraca(float topo, out Material[] materiais)
    {
        if (rng == null) rng = new System.Random(7);
        Malha m = new Malha();
        float y = topo - 0.26f, k = 1.9f;      // no gerador original o topo do piso fica 0,26 m acima de "y"
        CemCamadas(m, new Vector3(0f, y + 0.27f * k, 0f), Quaternion.identity, 3.3f * k, 3.3f * k, new[] { 0.28f * k, 0.28f * k, 0.28f * k }, new[] { 0.9f * k, 0.55f * k, 0.2f * k }, CEM_PEDRA);
        CemCamadas(m, new Vector3(0f, y + 1.11f * k, 0f), Quaternion.identity, 1.4f * k, 1.4f * k, new[] { 0.22f * k, 0.12f * k, 0.1f * k }, new[] { 0.22f * k, 0.13f * k, 0.06f * k }, CEM_PEDRA);
        m.Caixa(new Vector3(0f, y + 2.35f * k, 0f), new Vector3(1.3f, 1.8f, 1.3f) * k, CEM_LAPIDE);
        float hc = CemCamadas(m, new Vector3(0f, y + 3.25f * k, 0f), Quaternion.identity, 1.3f * k, 1.3f * k, new[] { 0.12f * k, 0.08f * k, 0.14f * k, 0.10f * k, 0.20f * k }, new[] { 0.04f * k, 0.10f * k, 0.06f * k, 0.17f * k, 0.26f * k }, CEM_PEDRA);
        float yc = y + 3.25f * k + hc;
        m.Caixa(new Vector3(0f, yc + 1.7f * k, 0f), new Vector3(0.55f, 3.4f, 0.42f) * k, CEM_LAPIDE);
        m.Caixa(new Vector3(0f, yc + 2.5f * k, 0f), new Vector3(2.3f, 0.55f, 0.42f) * k, CEM_LAPIDE);
        for (int g = 0; g < 16; g++)
        {
            float a = g * Mathf.PI * 2f / 16f;
            m.Caixa(new Vector3(Mathf.Cos(a) * 0.8f * k, yc + 2.5f * k + Mathf.Sin(a) * 0.8f * k, 0f), new Vector3(0.2f, 0.4f, 0.36f) * k, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), CEM_PEDRA);
        }
        m.Caixa(new Vector3(0f, yc + 3.55f * k, 0f), new Vector3(0.42f, 0.42f, 0.36f) * k, Quaternion.Euler(0f, 0f, 45f), CEM_PEDRA);
        for (int c = 0; c < 8; c++)
        {
            float a = c * Mathf.PI * 0.25f + Mathf.PI * 0.125f;
            CemVelas(m, new Vector3(Mathf.Cos(a) * 3.3f * k, y + 0.6f, Mathf.Sin(a) * 3.3f * k), 7, 0.4f);
        }
        Material[] todos = new Material[TOTAL_MATS];
        todos[CEM_PEDRA] = CarregarMatCem("Cem_Pedra"); todos[CEM_LAPIDE] = CarregarMatCem("Cem_Lapide");
        todos[REBOCO_CREME] = CarregarMatCem("Reboco_Creme"); todos[BRASA] = CarregarMatCem("Brasa");
        Mesh mesh = m.Gerar("Monumento_inteiro");
        materiais = m.Materiais(todos);
        return mesh;
    }
}
#endif
