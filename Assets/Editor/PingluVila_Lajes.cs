// Textura procedural de lajes de pedra irregulares (estilo Zelda): pedras lisas grandes,
// terra e cascalho nas frestas, musgo. Ladrilha sem emendas.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class PingluVila
{
    static float SS(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    static void GarantirTexturaLajes()
    {
        string pasta = PASTA_CAM + "/Texturas";
        string pCor = pasta + "/Lajes_v2_Cor.png", pNor = pasta + "/Lajes_v2_Normal.png";
        if (File.Exists(pCor) && File.Exists(pNor)) return;
        const int N = 512, G = 5;
        System.Random r = new System.Random(2024);
        Vector2[] pt = new Vector2[G * G]; float[] tom = new float[G * G], quente = new float[G * G];
        for (int i = 0; i < G * G; i++)
        {
            pt[i] = new Vector2((i % G + 0.15f + 0.7f * (float)r.NextDouble()) / G, (i / G + 0.15f + 0.7f * (float)r.NextDouble()) / G);
            tom[i] = (float)r.NextDouble(); quente[i] = (float)r.NextDouble();
        }
        float[] h = new float[N * N]; Color[] cor = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / N, (y + 0.5f) / N);
                float d1 = 9f, d2 = 9f; int k1 = 0;
                for (int j = 0; j < G * G; j++)
                    for (int ox = -1; ox <= 1; ox++)
                        for (int oy = -1; oy <= 1; oy++)
                        {
                            Vector2 q = pt[j] + new Vector2(ox, oy);
                            float d = (q - p).magnitude;
                            if (d < d1) { d2 = d1; d1 = d; k1 = j; } else if (d < d2) d2 = d;
                        }
                float borda = (d2 - d1) * G;                           // 0 na fresta, cresce para dentro da pedra
                float ruido = Mathf.PerlinNoise(p.x * 40f % 40f + 3f, p.y * 40f % 40f + 7f);
                float grao = Mathf.PerlinNoise(p.x * 160f, p.y * 160f);
                float pedra = SS(0.05f, 0.16f, borda + (ruido - 0.5f) * 0.05f);
                float domo = Mathf.Clamp01(borda * 1.3f);
                h[y * N + x] = pedra * (0.55f + 0.45f * SS(0f, 1f, domo)) + grao * 0.03f;
                Color pedraC = Color.Lerp(new Color(0.56f, 0.54f, 0.50f), new Color(0.66f, 0.62f, 0.55f), tom[k1]);
                pedraC = Color.Lerp(pedraC, new Color(0.60f, 0.52f, 0.43f), quente[k1] * 0.35f);
                pedraC *= 0.88f + 0.22f * grao + 0.08f * (ruido - 0.5f);
                pedraC *= 0.82f + 0.18f * SS(0.05f, 0.5f, borda);                      // bordas mais escuras
                Color terra = Color.Lerp(new Color(0.27f, 0.21f, 0.15f), new Color(0.40f, 0.32f, 0.22f), grao);
                float musgo = SS(0.5f, 0.75f, Mathf.PerlinNoise(p.x * 14f + 11f, p.y * 14f + 5f)) * (1f - pedra * 0.55f);
                Color c = Color.Lerp(terra, pedraC, pedra);
                c = Color.Lerp(c, new Color(0.26f, 0.36f, 0.13f), musgo * 0.75f);
                c.a = 1f; cor[y * N + x] = c;
            }
        Color[] nor = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float hl = h[y * N + (x + N - 1) % N], hr = h[y * N + (x + 1) % N], hd = h[((y + N - 1) % N) * N + x], hu = h[((y + 1) % N) * N + x];
                Vector3 n = new Vector3((hl - hr) * 3.2f, (hd - hu) * 3.2f, 1f).normalized;
                nor[y * N + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        System.Action<Color[], string> salvar = (px, caminho) =>
        {
            Texture2D t = new Texture2D(N, N, TextureFormat.RGB24, false); t.SetPixels(px); t.Apply();
            File.WriteAllBytes(caminho, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
        };
        salvar(cor, pCor); salvar(nor, pNor);
        TextureImporter ti = AssetImporter.GetAtPath(pNor) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        TextureImporter tc = AssetImporter.GetAtPath(pCor) as TextureImporter;
        if (tc != null) { tc.anisoLevel = 8; tc.SaveAndReimport(); }
    }
}
#endif
