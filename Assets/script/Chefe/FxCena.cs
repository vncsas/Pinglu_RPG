using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Efeitos usados nas cenas de cinema do fim do jogo (céu de tempestade, relâmpagos, cristais, estilhaços, clarão branco, montanha).
public static class FxCena
{
    static Material matSprite; static Texture2D texNuvem, texPoeira, texBrilho; static Mesh quad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { matSprite = null; texNuvem = texPoeira = texBrilho = null; quad = null; Branco = 0f; }

    public static Material Sprite() { if (matSprite == null) { Shader s = Shader.Find("Sprites/Default"); if (s != null) matSprite = new Material(s); } return matSprite; }
    public static Mesh Quad()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "QuadFx" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
        quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }; quad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quad.RecalculateBounds();
        return quad;
    }

    // nuvem macia: círculo com bordas esfumadas e um pouco de ruído
    public static Texture2D TexNuvem()
    {
        if (texNuvem != null) return texNuvem;
        const int N = 128; texNuvem = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f);
            float ruido = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.7f + Mathf.PerlinNoise(x * 0.22f + 5f, y * 0.22f) * 0.3f;
            float a = Mathf.Clamp01(1f - d); a = Mathf.Pow(a, 1.3f) * (0.5f + 0.7f * ruido);
            texNuvem.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }
        texNuvem.Apply(); return texNuvem;
    }
    public static Texture2D TexBrilho()
    {
        if (texBrilho != null) return texBrilho;
        const int N = 128; texBrilho = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f); float a = Mathf.Clamp01(1f - d); texBrilho.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * a)); }
        texBrilho.Apply(); return texBrilho;
    }

    public static Camera CamOverride;   // câmera usada pelos quadros virados para a câmera (a abertura em 3D usa a sua)
    public class Billboard : MonoBehaviour { void LateUpdate() { Camera c = CamOverride != null ? CamOverride : Camera.main; if (c != null) transform.rotation = c.transform.rotation; } }

    // quadro desenhado virado para a câmera (nuvem, poeira, brilho)
    public static GameObject Nuvem(Vector3 pos, float tam, Color cor, Texture2D tex = null, int ordem = 0)
    {
        GameObject g = new GameObject("Fx"); g.transform.position = pos; g.transform.localScale = Vector3.one * tam;
        g.AddComponent<MeshFilter>().sharedMesh = Quad();
        MeshRenderer r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = Sprite(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.sortingOrder = ordem;
        MaterialPropertyBlock mp = new MaterialPropertyBlock(); mp.SetTexture("_MainTex", tex != null ? tex : TexNuvem()); mp.SetColor("_Color", cor); r.SetPropertyBlock(mp);
        g.AddComponent<Billboard>(); return g;
    }
    public static void Pintar(GameObject g, Color cor)
    {
        MeshRenderer r = g.GetComponent<MeshRenderer>(); if (r == null) return; MaterialPropertyBlock mp = new MaterialPropertyBlock(); r.GetPropertyBlock(mp); mp.SetColor("_Color", cor); r.SetPropertyBlock(mp);
    }

    // ------------------------------------------------------------------ clarão branco por cima da tela
    public static float Branco;
    class TelaBranca : MonoBehaviour
    {
        void OnGUI() { if (Branco <= 0.002f) return; GUI.depth = -9500; GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Branco)); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white; }
    }
    static GameObject telaBranca;
    public static void GarantirTela() { if (telaBranca == null) { telaBranca = new GameObject("Tela branca"); telaBranca.AddComponent<TelaBranca>(); } }

    // ------------------------------------------------------------------ céu de tempestade
    [DefaultExecutionOrder(5000)]
    public class CeuSombrio : MonoBehaviour
    {
        public float escuro, alvo, vel = 0.25f, relampago, limiteNeblina = -1f;
        float rel;
        void Update() { escuro = Mathf.MoveTowards(escuro, alvo, vel * Time.unscaledDeltaTime); rel = Mathf.MoveTowards(rel, 0f, Time.unscaledDeltaTime * 4f); }
        public void Relampago(float forca = 1f) { rel = Mathf.Max(rel, forca); }
        void LateUpdate()
        {
            float k = escuro; if (k <= 0.001f && rel <= 0.001f) return;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.intensity = l.intensity * (1f - 0.82f * k) + rel * 3.2f;
            RenderSettings.ambientIntensity = RenderSettings.ambientIntensity * (1f - 0.65f * k) + rel * 0.8f;
            Color fog = Color.Lerp(RenderSettings.fogColor, new Color(0.07f, 0.04f, 0.1f), k * 0.85f); fog = Color.Lerp(fog, new Color(0.7f, 0.75f, 1f), rel * 0.5f); RenderSettings.fogColor = fog;
            float dens = RenderSettings.fogDensity * (1f + 0.4f * k); if (limiteNeblina > 0f) dens = Mathf.Min(dens, limiteNeblina); RenderSettings.fogDensity = dens;
            Material sky = RenderSettings.skybox; if (sky != null && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Mathf.Lerp(sky.GetFloat("_Exposure"), 0.15f, k * 0.15f));
        }
    }

    // ------------------------------------------------------------------ nuvens da tempestade girando em volta de um ponto no alto
    public class Tempestade : MonoBehaviour
    {
        public Vector3 centro; public float raio = 320f, altura = 150f, intensidade; readonly List<Transform> quadros = new List<Transform>(); readonly List<float> ang = new List<float>(), rad = new List<float>(), vel = new List<float>(), tamanho = new List<float>(), fase = new List<float>();
        public void Montar(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float r = Mathf.Sqrt(Random.value) * raio, a = Random.value * 360f, t = Random.Range(110f, 230f);
                GameObject g = Nuvem(centro, t, new Color(0.07f, 0.05f, 0.1f, 0f), TexNuvem(), i % 5); g.transform.SetParent(transform, true);
                quadros.Add(g.transform); ang.Add(a); rad.Add(r); vel.Add(Mathf.Lerp(7f, 2f, r / raio) * Random.Range(0.8f, 1.2f)); tamanho.Add(t); fase.Add(Random.value * 6f);
            }
        }
        void Update()
        {
            for (int i = 0; i < quadros.Count; i++)
            {
                ang[i] += vel[i] * Time.unscaledDeltaTime * (0.6f + intensidade);
                float rr = rad[i] * (1f - 0.12f * intensidade);
                Vector3 p = centro + new Vector3(Mathf.Cos(ang[i] * Mathf.Deg2Rad), 0f, Mathf.Sin(ang[i] * Mathf.Deg2Rad)) * rr; p.y = centro.y + altura * 0.0f + Mathf.Sin(fase[i] + Time.unscaledTime * 0.3f) * 10f - (rr / raio) * 18f;
                quadros[i].position = p;
                float a = Mathf.Clamp01(intensidade) * Mathf.Lerp(0.8f, 0.45f, rad[i] / raio);
                Color cor = Color.Lerp(new Color(0.09f, 0.07f, 0.12f), new Color(0.2f, 0.1f, 0.28f), Mathf.PingPong(fase[i], 1f) * 0.6f); cor.a = a; Pintar(quadros[i].gameObject, cor);
            }
        }
    }
    public static Tempestade CriarTempestade(Vector3 centroNoAlto, int n = 70)
    {
        GameObject g = new GameObject("Nuvens da tempestade"); Tempestade t = g.AddComponent<Tempestade>(); t.centro = centroNoAlto; t.Montar(n); return t;
    }

    // ------------------------------------------------------------------ relâmpago: raio irregular
    public static void Raio(Vector3 de, Vector3 ate, float largura = 2f, float dur = 0.22f, Color? cor = null)
    {
        GameObject g = new GameObject("Raio"); LineRenderer lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = Sprite(); lr.useWorldSpace = true; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        int n = 22; lr.positionCount = n; Vector3 d = ate - de;
        for (int i = 0; i < n; i++) { float u = i / (float)(n - 1); Vector3 p = de + d * u; if (i > 0 && i < n - 1) p += new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f)) * d.magnitude * 0.025f; lr.SetPosition(i, p); }
        lr.startWidth = largura; lr.endWidth = largura * 0.35f; lr.startColor = lr.endColor = cor ?? new Color(0.85f, 0.9f, 1f, 1f);
        GameObject lg = new GameObject("Luz do raio"); lg.transform.position = ate + Vector3.up * 6f; lg.transform.SetParent(g.transform, true);
        Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = cor.HasValue ? Color.Lerp(cor.Value, Color.white, 0.25f) : new Color(0.7f, 0.8f, 1f); l.range = cor.HasValue ? 60f : 140f; l.intensity = cor.HasValue ? 6f : 14f; l.shadows = LightShadows.None;
        Object.Destroy(g, dur);
    }

    // ------------------------------------------------------------------ cristal flutuante (visual)
    public static GameObject Cristal(Color cor, float tam, Material materialBase)
    {
        GameObject g = new GameObject("Cristal da cena");
        Mesh m = new Mesh { name = "CristalFx" }; int lados = 6; var v = new List<Vector3>(); var t = new List<int>();
        v.Add(new Vector3(0, 1.5f, 0)); v.Add(new Vector3(0, -1.5f, 0));
        for (int i = 0; i < lados; i++) { float a = i / (float)lados * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * 0.6f, 0.15f, Mathf.Sin(a) * 0.6f)); }
        for (int i = 0; i < lados; i++) { int a = 2 + i, b = 2 + (i + 1) % lados; t.Add(0); t.Add(b); t.Add(a); t.Add(1); t.Add(a); t.Add(b); }
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        GameObject corpo = new GameObject("Corpo"); corpo.transform.SetParent(g.transform, false); corpo.transform.localScale = Vector3.one * tam;
        corpo.AddComponent<MeshFilter>().sharedMesh = m; MeshRenderer r = corpo.AddComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material mat = materialBase != null ? new Material(materialBase) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(cor.r * 1.1f, cor.g * 1.1f, cor.b * 1.1f, 1f)); if (mat.HasProperty("_Color")) mat.SetColor("_Color", cor); r.sharedMaterial = mat;
        GameObject brilho = Nuvem(g.transform.position, tam * 9f, new Color(cor.r, cor.g, cor.b, 0.55f), TexBrilho(), 8); brilho.transform.SetParent(g.transform, true); brilho.name = "Halo";
        GameObject lg = new GameObject("Luz"); lg.transform.SetParent(g.transform, false); Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = cor; l.range = tam * 40f; l.intensity = 3f; l.shadows = LightShadows.None;
        g.AddComponent<GiroDoCristal>(); return g;
    }
    public class GiroDoCristal : MonoBehaviour { void Update() { if (transform.childCount > 0) transform.GetChild(0).Rotate(0f, 90f * Time.unscaledDeltaTime, 0f, Space.Self); } }
    public static void Luz(GameObject cristal, float intensidade, float alcance) { Light l = cristal.GetComponentInChildren<Light>(); if (l != null) { l.intensity = intensidade; l.range = alcance; } }
    public static void Escala(GameObject cristal, float k) { cristal.transform.localScale = Vector3.one * k; }

    // ------------------------------------------------------------------ estilhaços de cristal caindo do céu (cinzas e escuros)
    public class Estilhaco : MonoBehaviour
    {
        public Vector3 v, giro; public float chao, fimEm = -1f; public System.Action<Vector3> aoPousar; bool pousou;
        void Update()
        {
            if (pousou) return;
            transform.position += v * Time.unscaledDeltaTime; transform.Rotate(giro * Time.unscaledDeltaTime, Space.Self);
            if (transform.position.y <= chao) { pousou = true; Vector3 p = transform.position; p.y = chao + 0.05f; transform.position = p; if (aoPousar != null) aoPousar(p); enabled = false; }
        }
    }
    static Mesh[] malhasEstilhaco;
    static Mesh MalhaEstilhaco(int i)
    {
        if (malhasEstilhaco == null)
        {
            malhasEstilhaco = new Mesh[4];
            for (int k = 0; k < 4; k++)
            {
                Mesh m = new Mesh(); var v = new List<Vector3>(); var t = new List<int>();
                float a = 0.35f + 0.15f * k, h = 0.9f + 0.5f * k;
                v.Add(new Vector3(0, h, 0)); v.Add(new Vector3(-a, 0, -a * 0.6f)); v.Add(new Vector3(a, 0.1f, -a * 0.4f)); v.Add(new Vector3(a * 0.2f, -0.1f, a));
                if (k >= 2) v.Add(new Vector3(0, -h * 0.5f, 0));
                t.AddRange(new[] { 0, 2, 1, 0, 3, 2, 0, 1, 3 }); if (k >= 2) t.AddRange(new[] { 4, 1, 2, 4, 2, 3, 4, 3, 1 }); else t.AddRange(new[] { 1, 2, 3 });
                m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); malhasEstilhaco[k] = m;
            }
        }
        return malhasEstilhaco[i % 4];
    }
    static Material matEstilhaco;
    public static GameObject NovoEstilhaco(Vector3 pos, float tam, float chao, Vector3 vel, System.Action<Vector3> aoPousar = null)
    {
        GameObject g = new GameObject("Estilhaco"); g.transform.position = pos; g.transform.rotation = Random.rotation; g.transform.localScale = Vector3.one * tam;
        g.AddComponent<MeshFilter>().sharedMesh = MalhaEstilhaco(Random.Range(0, 4));
        MeshRenderer r = g.AddComponent<MeshRenderer>();
        if (matEstilhaco == null) { Shader s = Shader.Find("Universal Render Pipeline/Lit"); if (s != null) { matEstilhaco = new Material(s); matEstilhaco.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.25f)); matEstilhaco.SetFloat("_Smoothness", 0.35f); matEstilhaco.SetFloat("_Metallic", 0.3f); } }
        if (matEstilhaco != null) r.sharedMaterial = matEstilhaco;
        Estilhaco e = g.AddComponent<Estilhaco>(); e.v = vel; e.giro = Random.insideUnitSphere * 180f; e.chao = chao; e.aoPousar = aoPousar; return g;
    }

    // ------------------------------------------------------------------ montanha que racha: abre uma cratera no terreno (e devolve tudo no fim)
    public class Montanha
    {
        Terrain t; TerrainData td; int x0, z0, w, h; float[,] orig; TreeInstance[] arvores; Vector3 pico; float raio; bool alterou, arvoresRetiradas;
        public Montanha(Vector3 picoMundo, float raioM)
        {
            t = Terrain.activeTerrain; if (t == null) return; td = t.terrainData; pico = picoMundo; raio = raioM;
            Vector3 o = t.transform.position, s = td.size; int res = td.heightmapResolution;
            x0 = Mathf.Clamp(Mathf.FloorToInt((pico.x - raio - o.x) / s.x * (res - 1)), 0, res - 2); z0 = Mathf.Clamp(Mathf.FloorToInt((pico.z - raio - o.z) / s.z * (res - 1)), 0, res - 2);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((pico.x + raio - o.x) / s.x * (res - 1)), x0 + 1, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((pico.z + raio - o.z) / s.z * (res - 1)), z0 + 1, res - 1);
            w = x1 - x0 + 1; h = z1 - z0 + 1; orig = td.GetHeights(x0, z0, w, h);
        }
        public bool Pronta { get { return td != null; } }
        public void TirarArvores()
        {
            if (td == null || arvoresRetiradas) return; arvores = td.treeInstances; arvoresRetiradas = true;
            Vector3 s = td.size; var ficam = new List<TreeInstance>(arvores.Length);
            foreach (TreeInstance ti in arvores) { float x = ti.position.x * s.x + t.transform.position.x, z = ti.position.z * s.z + t.transform.position.z; if (new Vector2(x - pico.x, z - pico.z).magnitude > raio * 0.9f) ficam.Add(ti); }
            td.SetTreeInstances(ficam.ToArray(), true);
        }
        // k de 0 (intacta) a 1 (cratera funda)
        public void Cratera(float k, float profundidade)
        {
            if (td == null) return; Vector3 o = t.transform.position, s = td.size; int res = td.heightmapResolution;
            var a = new float[h, w];
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
            {
                float wx = o.x + (x0 + i) / (float)(res - 1) * s.x, wz = o.z + (z0 + j) / (float)(res - 1) * s.z;
                float d = new Vector2(wx - pico.x, wz - pico.z).magnitude / raio;
                float queda = Mathf.Clamp01(1f - d); queda = queda * queda * (3f - 2f * queda);
                float ruido = 0.75f + 0.5f * Mathf.PerlinNoise(wx * 0.15f, wz * 0.15f);
                float novo = orig[j, i] - (profundidade * k * queda * ruido) / s.y;
                a[j, i] = Mathf.Max(0f, novo);
            }
            td.SetHeights(x0, z0, a); alterou = true;
        }
        public void Restaurar()
        {
            if (td == null) return;
            if (alterou) { td.SetHeights(x0, z0, orig); alterou = false; }
            if (arvoresRetiradas && arvores != null) { td.SetTreeInstances(arvores, true); arvoresRetiradas = false; }
            if (t != null) t.Flush();
        }
        public float Altura(float x, float z) { return t != null ? t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y : 0f; }
    }
}
