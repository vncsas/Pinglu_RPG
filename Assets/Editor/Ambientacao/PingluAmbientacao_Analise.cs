#if UNITY_EDITOR
using System.Linq; using System.Text; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine;
// Analise somente-leitura do mapa original (nao altera nada)
public static class PingluAmbientacao_Analise
{
    public static void Rodar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var t = Terrain.activeTerrain; var td = t.terrainData; var sb = new StringBuilder("[ANALISE]\n");
        int res = 200; float W = td.size.x; var hist = new int[6]; float hmin = 1e9f, hmax = -1e9f, hsum = 0; int n = 0; var hs = new float[res, res];
        for (int j = 0; j < res; j++) for (int i = 0; i < res; i++)
        {
            float u = (i + 0.5f) / res, v = (j + 0.5f) / res; float h = td.GetInterpolatedHeight(u, v); hs[j, i] = h; float s = td.GetSteepness(u, v);
            hist[s < 2 ? 0 : s < 5 ? 1 : s < 10 ? 2 : s < 20 ? 3 : s < 35 ? 4 : 5]++; hmin = Mathf.Min(hmin, h); hmax = Mathf.Max(hmax, h); hsum += h; n++;
        }
        sb.AppendLine($"ALTURA min={hmin:0.0} media={hsum / n:0.0} max={hmax:0.0} (terreno {td.size.y} m)");
        sb.AppendLine($"INCLINACAO: <2°={100f * hist[0] / n:0}%  2-5°={100f * hist[1] / n:0}%  5-10°={100f * hist[2] / n:0}%  10-20°={100f * hist[3] / n:0}%  20-35°={100f * hist[4] / n:0}%  >35°={100f * hist[5] / n:0}%");
        // rugosidade local: desvio da altura numa janela de ~100 m
        float rug = 0; int rn = 0; for (int j = 10; j < res - 10; j += 5) for (int i = 10; i < res - 10; i += 5) { float m = 0; int c = 0; for (int dj = -10; dj <= 10; dj += 5) for (int di = -10; di <= 10; di += 5) { m += hs[j + dj, i + di]; c++; } m /= c; float v2 = 0; for (int dj = -10; dj <= 10; dj += 5) for (int di = -10; di <= 10; di += 5) v2 += (hs[j + dj, i + di] - m) * (hs[j + dj, i + di] - m); rug += Mathf.Sqrt(v2 / c); rn++; }
        sb.AppendLine($"RUGOSIDADE media (desvio de altura em janela de 100 m) = {rug / rn:0.0} m");
        var a = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight); int L = td.alphamapLayers; var tot = new double[L];
        for (int j = 0; j < td.alphamapHeight; j += 4) for (int i = 0; i < td.alphamapWidth; i += 4) for (int k = 0; k < L; k++) tot[k] += a[j, i, k];
        double soma = tot.Sum(); for (int k = 0; k < L; k++) sb.AppendLine($"CAMADA {k} {td.terrainLayers[k].name}: {100 * tot[k] / soma:0.0}%");
        sb.AppendLine($"ARVORES (terrain trees)={td.treeInstanceCount}  detalhes={td.detailPrototypes.Length}");
        // mapa de ocupacao 25x25 (40 m): quantos renderers pequenos por celula
        int G = 25; var oc = new int[G, G]; int total = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) { var b = r.bounds; if (b.center.y < -500 || r is ParticleSystemRenderer || b.size.x > 140) continue; int i = Mathf.Clamp((int)(b.center.x / W * G), 0, G - 1), j = Mathf.Clamp((int)(b.center.z / W * G), 0, G - 1); oc[j, i]++; total++; }
        foreach (var ti in td.treeInstances) { int i = Mathf.Clamp((int)(ti.position.x * G), 0, G - 1), j = Mathf.Clamp((int)(ti.position.z * G), 0, G - 1); oc[j, i] += 1; }
        sb.AppendLine("OCUPACAO (objetos+arvores por celula de 40 m; linha de cima = z alto/norte):");
        for (int j = G - 1; j >= 0; j--) { var sbl = new StringBuilder(); for (int i = 0; i < G; i++) { int c = oc[j, i]; sbl.Append(c == 0 ? " . " : c < 15 ? " - " : c < 60 ? " o " : c < 200 ? " O " : " # "); } sb.AppendLine(sbl.ToString()); }
        int vazias = 0; for (int j = 0; j < G; j++) for (int i = 0; i < G; i++) if (oc[j, i] < 15) vazias++; sb.AppendLine($"celulas pouco ocupadas (<15): {vazias}/{G * G} = {100f * vazias / (G * G):0}%  | renderers total={total}");
        // agua
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) if (r.sharedMaterials.Any(m => m != null && m.name.ToLower().Contains("agua"))) sb.AppendLine($"AGUA {r.name} centro={r.bounds.center} tam={r.bounds.size} mat={string.Join("|", r.sharedMaterials.Select(m => m.name))}");
        // luzes e efeitos
        sb.AppendLine($"LUZES pontuais={Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Count(l => l.type != LightType.Directional)}");
        sb.AppendLine($"PARTICULAS={Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude).Length}");
        sb.AppendLine("MESH COLLIDERS=" + Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude).Length + "  LODGroups=" + Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude).Length);
        sb.AppendLine($"BASEMAP dist={t.basemapDistance} detalhes dist={t.detailObjectDistance} dens={t.detailObjectDensity} treeDist={t.treeDistance} pixelError={t.heightmapPixelError} drawInstanced={t.drawInstanced}");
        Debug.Log(sb);
    }
}
#endif
