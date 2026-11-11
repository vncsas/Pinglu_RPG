using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Grama em volta da camera com GPU instancing (nao usa o detalhe do Terrain):
//  - 3 niveis por distancia: perto (tufo completo), meio e longe (tufos de poucos triangulos, mais largos, menos numerosos)
//  - chunks com frustum culling; densidade tirada das camadas de textura do Terrain (so nasce onde tem grama)
//  - grama VERDE e grama SECA (dois materiais), escolhida pelo peso das camadas; clareiras por ruido
// A cor (gradiente, manchas, vento) vem do shader Pinglu/GramaTufo.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class CampoDeGrama : MonoBehaviour
{
    public Mesh[] tufos;                 // perto: 0 baixo, 1 medio, 2 alto-medio, 3 alto
    public Mesh[] tufosLonge;            // meio/longe (poucos triangulos), mesma ordem
    public Material material, materialSeco;
    [Tooltip("Peso de cada camada de textura do Terrain para grama verde / seca (mesma ordem das camadas)")]
    public float[] pesoVerde = { 1f, 0.85f, 0f, 0.15f, 0f, 0.3f, 0f, 0f };
    public float[] pesoSeco = { 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f };
    public float tamanhoChunk = 16f;
    public float distPerto = 20f, distMeio = 48f, distLonge = 90f;
    public float densPerto = 5.5f, densMeio = 1.5f, densLonge = 0.45f;   // tufos por m2
    [Range(0, 1)] public float clareiras = 0.62f;
    public int semente = 7;

    class Chunk { public Bounds caixa; public Matrix4x4[][][][] flat; }   // [nivel][material][variante][]
    Dictionary<Vector2Int, Chunk> chunks = new();
    Terrain terreno; float[,,] alfa; int aw, ah, camadas;
    Plane[] planos = new Plane[6];

    void OnEnable() { RenderPipelineManager.beginCameraRendering += AntesDaCamera; terreno = GetComponent<Terrain>(); alfa = null; chunks.Clear(); }
    void OnDisable() { RenderPipelineManager.beginCameraRendering -= AntesDaCamera; }

    void Preparar()
    {
        var td = terreno.terrainData; aw = td.alphamapWidth; ah = td.alphamapHeight; camadas = td.alphamapLayers;
        alfa = td.GetAlphamaps(0, 0, aw, ah);
    }

    // retorna (verde, seca)
    Vector2 Densidade(Vector3 p)
    {
        var td = terreno.terrainData; var rel = p - terreno.transform.position;
        float u = rel.x / td.size.x, v = rel.z / td.size.z;
        if (u < 0 || v < 0 || u > 1 || v > 1) return Vector2.zero;
        int i = Mathf.Clamp((int)(u * (aw - 1)), 0, aw - 1), j = Mathf.Clamp((int)(v * (ah - 1)), 0, ah - 1);
        float dv = 0, ds = 0;
        for (int k = 0; k < camadas; k++) { float a = alfa[j, i, k]; if (k < pesoVerde.Length) dv += a * pesoVerde[k]; if (k < pesoSeco.Length) ds += a * pesoSeco[k]; }
        float livre = 1f - Mathf.Clamp01(alfa[j, i, 2] * 3f + (camadas > 7 ? alfa[j, i, 7] * 3f : 0f));   // caminho de barro e praca ficam limpos
        dv *= livre; ds *= livre;
        float n = Mathf.PerlinNoise(p.x * 0.04f + 11.3f, p.z * 0.04f + 4.1f), n2 = Mathf.PerlinNoise(p.x * 0.13f + 70f, p.z * 0.13f + 9f);
        float lim = Mathf.Lerp(0.0f, 0.36f, clareiras);
        float clump = Mathf.Clamp01((n * 0.7f + n2 * 0.3f - lim) / 0.22f);
        return new Vector2(dv, ds) * clump;
    }

    Chunk Criar(Vector2Int c)
    {
        var ch = new Chunk(); var origem = terreno.transform.position + new Vector3(c.x * tamanhoChunk, 0, c.y * tamanhoChunk);
        float altMin = float.MaxValue, altMax = float.MinValue;
        var rnd = new System.Random(semente + c.x * 7919 + c.y * 104729);
        float[] dens = { densPerto, densMeio, densLonge };
        var lista = new List<Matrix4x4>[3][][];
        for (int nv = 0; nv < 3; nv++)
        {
            lista[nv] = new List<Matrix4x4>[2][];
            for (int m = 0; m < 2; m++) { lista[nv][m] = new List<Matrix4x4>[4]; for (int k = 0; k < 4; k++) lista[nv][m][k] = new List<Matrix4x4>(); }
            int n = Mathf.RoundToInt(dens[nv] * tamanhoChunk * tamanhoChunk);
            for (int q = 0; q < n; q++)
            {
                float x = origem.x + (float)rnd.NextDouble() * tamanhoChunk, z = origem.z + (float)rnd.NextDouble() * tamanhoChunk;
                var p = new Vector3(x, 0, z); var d = Densidade(p); float tot = d.x + d.y;
                if (rnd.NextDouble() > Mathf.Min(1f, tot)) continue;
                int mat = rnd.NextDouble() < d.y / Mathf.Max(tot, 1e-4f) ? 1 : 0;
                p.y = terreno.SampleHeight(p) + terreno.transform.position.y; altMin = Mathf.Min(altMin, p.y); altMax = Mathf.Max(altMax, p.y);
                float r = (float)rnd.NextDouble(); int var = nv == 0 ? (r < 0.30f ? 0 : r < 0.62f ? 1 : r < 0.9f ? 2 : 3) : (r < 0.15f ? 0 : r < 0.5f ? 1 : r < 0.85f ? 2 : 3);
                float esc = nv == 0 ? 1f : nv == 1 ? 1.2f : 1.45f;                    // de longe: tufos maiores, menos numerosos, mesma cobertura
                float alt = (0.30f + (float)rnd.NextDouble() * 0.28f) * esc, lar = (0.85f + (float)rnd.NextDouble() * 0.4f) * esc;
                if (mat == 1) alt *= 0.9f;
                lista[nv][mat][var].Add(Matrix4x4.TRS(p - Vector3.up * 0.02f, Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0), new Vector3(lar, alt, lar)));
            }
        }
        if (altMin > altMax) { altMin = origem.y; altMax = origem.y + 1; }
        ch.caixa = new Bounds(new Vector3(origem.x + tamanhoChunk / 2, (altMin + altMax) / 2 + 0.5f, origem.z + tamanhoChunk / 2), new Vector3(tamanhoChunk, altMax - altMin + 2f, tamanhoChunk));
        ch.flat = new Matrix4x4[3][][][];
        for (int nv = 0; nv < 3; nv++)
        {
            ch.flat[nv] = new Matrix4x4[2][][];
            for (int m = 0; m < 2; m++) { ch.flat[nv][m] = new Matrix4x4[4][]; for (int k = 0; k < 4; k++) ch.flat[nv][m][k] = lista[nv][m][k].ToArray(); }
        }
        return ch;
    }

    void AntesDaCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
        if (tufos == null || tufos.Length < 4 || material == null) return;
        if (terreno == null) terreno = GetComponent<Terrain>();
        if (terreno == null || terreno.terrainData == null) return;
        if (alfa == null) Preparar();
        var pos = cam.transform.position; var td = terreno.terrainData;
        GeometryUtility.CalculateFrustumPlanes(cam, planos);
        int c0x = Mathf.FloorToInt((pos.x - distLonge - terreno.transform.position.x) / tamanhoChunk), c1x = Mathf.FloorToInt((pos.x + distLonge - terreno.transform.position.x) / tamanhoChunk);
        int c0z = Mathf.FloorToInt((pos.z - distLonge - terreno.transform.position.z) / tamanhoChunk), c1z = Mathf.FloorToInt((pos.z + distLonge - terreno.transform.position.z) / tamanhoChunk);
        int nx = Mathf.CeilToInt(td.size.x / tamanhoChunk), nz = Mathf.CeilToInt(td.size.z / tamanhoChunk);
        var mats = new[] { material, materialSeco != null ? materialSeco : material };
        var rps = new RenderParams[2];
        for (int m = 0; m < 2; m++) rps[m] = new RenderParams(mats[m]) { camera = cam, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = gameObject.layer };
        for (int cx = Mathf.Max(0, c0x); cx <= Mathf.Min(nx - 1, c1x); cx++)
            for (int cz = Mathf.Max(0, c0z); cz <= Mathf.Min(nz - 1, c1z); cz++)
            {
                var key = new Vector2Int(cx, cz);
                if (!chunks.TryGetValue(key, out var ch)) { ch = Criar(key); chunks[key] = ch; }
                float dist = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(ch.caixa.center.x, ch.caixa.center.z)) - tamanhoChunk * 0.7f;
                if (dist > distLonge) continue;
                if (!GeometryUtility.TestPlanesAABB(planos, ch.caixa)) continue;
                int nv = dist < distPerto ? 0 : dist < distMeio ? 1 : 2;
                var malhas = nv == 0 ? tufos : (tufosLonge != null && tufosLonge.Length >= 4 ? tufosLonge : tufos);
                for (int m = 0; m < 2; m++)
                    for (int k = 0; k < 4; k++)
                    {
                        var arr = ch.flat[nv][m][k];
                        for (int o = 0; o < arr.Length; o += 1023) Graphics.RenderMeshInstanced(rps[m], malhas[k], 0, arr, Mathf.Min(1023, arr.Length - o), o);
                    }
            }
    }
}
