// =====================================================================
//  PingluVila_Caminhos.cs  -  caminhos de pedra e postes de luz do vilarejo
// ---------------------------------------------------------------------
//  - praça de pedra (miolo de seixos + anel de pedras) e rua em anel em volta dela
//  - ruas até a porta de cada casa e a estrada principal chegando na vila (ruas de paralelepípedo com meio-fio)
//  - o chão pintado antigo da vila (as "marcações" de terra em volta das coisas) sai
//  - postes de rua NOVOS (base de pedra, haste de ferro, lanterna de vidro) com LUZ de verdade; os postes antigos saem
//  - sem árvores em cima dos caminhos; o capim também não nasce em cima deles (menu 54)
//
//  COMO USAR:  Pinglu > 56. Caminhos e postes da vila   (Play desligado; depois Ctrl+S)
//  Para tirar: Pinglu > 57. Remover caminhos e postes da vila
// =====================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class PingluVila
{
    const string NOME_CAMINHOS = "Caminhos da Vila";
    const string NOME_POSTES = "Postes da Vila";
    const string PASTA_CAM = "Assets/Cenario/Vila/Caminhos";
    const float RAIO_LIMPEZA_VILA = 62f;      // a rua de pedra vai até aqui; depois dela a estrada de terra do mapa continua

    static bool tudoEmUm;      // true quando o menu 62 refaz tudo de uma vez (ele mesmo refaz o terreno e salva no fim)

    class RuaV { public List<Vector3> pts = new List<Vector3>(); public float largura = 3.4f; public bool fechada; }

    [MenuItem("Pinglu/56. Caminhos e postes da vila")]
    public static void CaminhosVilaMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        terreno = Terrain.activeTerrain;
        if (terreno == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei o Terrain. Abra a SampleScene.", "OK"); return; }
        dados = terreno.terrainData;
        GameObject vila = GameObject.Find(NOME_RAIZ);
        if (vila == null) { EditorUtility.DisplayDialog("Pinglu", "Não achei a vila (Vila_Gerada). Rode antes o Pinglu > 3.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Pinglu - Caminhos e postes da vila",
            "Vou refazer os caminhos da vila: praça de pedra, rua em anel, ruas até cada casa e a estrada principal, com meio-fio.\n" +
            "Os postes antigos saem e entram postes novos com luz de verdade.\n\nO chão pintado antigo da vila é limpo. Pode demorar um pouco.", "Pode fazer", "Cancelar")) return;
        CaminhosVila(vila, true);
    }

    [MenuItem("Pinglu/57. Remover caminhos e postes da vila")]
    public static void RemoverCaminhosVilaMenu()
    {
        GameObject vila = GameObject.Find(NOME_RAIZ);
        if (vila == null) return;
        foreach (string n in new[] { NOME_CAMINHOS, NOME_POSTES })
        { Transform t = vila.transform.Find(n); if (t != null) Undo.DestroyObjectImmediate(t.gameObject); }
        EditorSceneManager.MarkSceneDirty(vila.scene);
        EditorUtility.DisplayDialog("Pinglu", "Caminhos e postes novos removidos. (Os postes antigos não voltam sozinhos: use Pinglu > 3 se quiser a vila original.)\nSalve com Ctrl + S.", "OK");
    }

    // ------------------------------------------------------------------
    static Material MatCam(string nome, string tex, float lisura, string ext = ".jpg", string pastaTex = null)
    {
        string caminho = PASTA_CAM + "/" + nome + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, caminho); }
        string pt = pastaTex == null ? PASTA_CAM + "/Texturas" : "Assets/Cenario/" + pastaTex + "/Texturas";
        string cor = pt + "/" + tex + "_Cor" + ext, nor = pt + "/" + tex + "_Normal" + ext;
        TextureImporter ti = AssetImporter.GetAtPath(nor) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        TextureImporter tc = AssetImporter.GetAtPath(cor) as TextureImporter;
        if (tc != null && tc.anisoLevel < 8) { tc.anisoLevel = 8; tc.SaveAndReimport(); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(cor)); m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nor)); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f);
        m.SetFloat("_Smoothness", lisura); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static List<Vector3> Suavizar(List<Vector3> p, float passo)
    {
        // Catmull-Rom e reamostragem a cada "passo" metros
        List<Vector3> densa = new List<Vector3>();
        int n = p.Count;
        if (n < 3) { densa.AddRange(p); }
        else
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(n - 1, i + 2)];
                int sub = Mathf.Max(2, Mathf.CeilToInt(Plano(p2 - p1).magnitude / 1.5f));
                for (int s = 0; s < sub; s++)
                {
                    float t = s / (float)sub, t2 = t * t, t3 = t2 * t;
                    densa.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
                if (i == n - 2) densa.Add(p2);
            }
        List<Vector3> r = new List<Vector3>();
        if (densa.Count == 0) return r;
        r.Add(densa[0]);
        float acum = 0f;
        for (int i = 1; i < densa.Count; i++)
        {
            float seg = Plano(densa[i] - densa[i - 1]).magnitude;
            while (acum + seg >= passo && seg > 0.0001f)
            {
                float t = (passo - acum) / seg;
                Vector3 q = Vector3.Lerp(densa[i - 1], densa[i], t);
                r.Add(q); densa[i - 1] = q; seg = Plano(densa[i] - q).magnitude; acum = 0f;
            }
            acum += seg;
        }
        if (Plano(r[r.Count - 1] - densa[densa.Count - 1]).magnitude > 0.3f) r.Add(densa[densa.Count - 1]);
        return r;
    }

    // fita de rua sobre o terreno: u = atravessa (em metros / "tile"), v = ao longo
    static void FitaDaRua(RuaV rua, float tile, List<Vector3> V, List<Vector2> UV, List<int> T, List<Vector3> Vc, List<Vector2> UVc, List<int> Tc, float extra = 0f, float off = 0.07f, float semente = 0f)
    {
        List<Vector3> pts = Suavizar(rua.pts, 1.1f);
        if (rua.fechada && pts.Count > 2) pts.Add(pts[0]);
        if (pts.Count < 2) return;
        float w0 = rua.largura * 0.5f + extra, dist = 0f, w = w0;
        float alturaMeioFio = 0.14f, larguraMeioFio = 0.24f;
        int baseV = V.Count, baseC = Vc.Count;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 tg = i == 0 ? pts[1] - pts[0] : (i == pts.Count - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1]);
            tg.y = 0f; tg.Normalize();
            Vector3 nrm = new Vector3(-tg.z, 0f, tg.x);
            if (i > 0) dist += Plano(pts[i] - pts[i - 1]).magnitude;
            // borda irregular, como caminho de pedra de verdade (a largura oscila ao longo da rua)
            w = w0 * (1f + 0.22f * (Mathf.PerlinNoise(dist * 0.45f + semente, pts[0].x * 0.13f + pts[0].z * 0.07f) - 0.5f) * 2f);
            Vector3 L = pts[i] + nrm * w, R = pts[i] - nrm * w;
            L.y = Altura(L.x, L.z) + off; R.y = Altura(R.x, R.z) + off;
            V.Add(L); V.Add(R);
            UV.Add(new Vector2(0f, dist / tile)); UV.Add(new Vector2(2f * w0 / tile, dist / tile));
            // meio-fio dos dois lados: base interna, topo interno, topo externo, base externa
            foreach (int lado in new[] { 1, -1 })
            {
                Vector3 b0 = pts[i] + nrm * (w * lado), b1 = pts[i] + nrm * ((w + larguraMeioFio) * lado);
                float y0 = Altura(b0.x, b0.z) + off, y1 = Altura(b1.x, b1.z) + off;
                Vc.Add(new Vector3(b0.x, y0, b0.z)); Vc.Add(new Vector3(b0.x, y0 + alturaMeioFio, b0.z));
                Vc.Add(new Vector3(b1.x, y1 + alturaMeioFio, b1.z)); Vc.Add(new Vector3(b1.x, y1 - 0.05f, b1.z));
                float vv = dist / 0.9f;
                UVc.Add(new Vector2(0f, vv)); UVc.Add(new Vector2(0.25f, vv)); UVc.Add(new Vector2(0.5f, vv)); UVc.Add(new Vector2(0.75f, vv));
            }
        }
        for (int i = 0; i < pts.Count - 1; i++)
        {
            int a = baseV + i * 2;
            T.AddRange(new[] { a, a + 3, a + 1, a, a + 2, a + 3 });
            // meio-fio: 8 vértices por linha (4 de cada lado)
            int c = baseC + i * 8, d = baseC + (i + 1) * 8;
            for (int lado = 0; lado < 2; lado++)
            {
                int o0 = c + lado * 4, o1 = d + lado * 4;
                for (int q = 0; q < 3; q++)
                {
                    if (lado == 1) Tc.AddRange(new[] { o0 + q, o1 + q, o1 + q + 1, o0 + q, o1 + q + 1, o0 + q + 1 });
                    else Tc.AddRange(new[] { o0 + q, o1 + q + 1, o1 + q, o0 + q, o0 + q + 1, o1 + q + 1 });
                }
            }
        }
    }

    // disco/anel de pedra sobre o terreno
    static void Anel(Vector3 c, float r0, float r1, float tile, List<Vector3> V, List<Vector2> UV, List<int> T, float off = 0.07f)
    {
        int seg = 112, aneis = Mathf.Max(1, Mathf.CeilToInt((r1 - r0) / 1.5f));
        int b0 = V.Count;
        for (int a = 0; a <= aneis; a++)
        {
            float r = Mathf.Lerp(r0, r1, a / (float)aneis);
            for (int s = 0; s <= seg; s++)
            {
                float ang = s * Mathf.PI * 2f / seg;
                Vector3 p = c + new Vector3(Mathf.Sin(ang) * r, 0f, Mathf.Cos(ang) * r);
                p.y = Altura(p.x, p.z) + off;
                V.Add(p); UV.Add(new Vector2(p.x / tile, p.z / tile));
            }
        }
        for (int a = 0; a < aneis; a++)
            for (int s = 0; s < seg; s++)
            {
                int i0 = b0 + a * (seg + 1) + s, i1 = i0 + 1, i2 = i0 + seg + 1, i3 = i2 + 1;
                T.AddRange(new[] { i0, i2, i3, i0, i3, i1 });
            }
    }

    static GameObject MeshGo(string nome, Transform pai, List<Vector3> V, List<Vector2> UV, List<int> T, Material mat)
    {
        if (V.Count == 0 || T.Count == 0) return null;
        Mesh m = new Mesh { name = nome };
        if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(V); m.SetUVs(0, UV); m.SetTriangles(T, 0);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        string cam = PASTA_CAM + "/Malhas/" + nome + "_" + (++contadorMalhas).ToString("000") + ".asset";
        AssetDatabase.CreateAsset(m, cam);
        GameObject g = new GameObject(nome);
        g.transform.SetParent(pai, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        MeshRenderer mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        g.isStatic = true;
        return g;
    }

    static float DistAoRetangulo(Vector3 p, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(b.min.x - p.x, p.x - b.max.x)), dz = Mathf.Max(0f, Mathf.Max(b.min.z - p.z, p.z - b.max.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static bool DentroDeCasa(Vector3 p, List<Bounds> casas, float margem)
    {
        foreach (Bounds b in casas)
            if (p.x > b.min.x - margem && p.x < b.max.x + margem && p.z > b.min.z - margem && p.z < b.max.z + margem) return true;
        return false;
    }

    // ------------------------------------------------------------------
    static void CaminhosVila(GameObject vila, bool interativo)
    {
        avisos.Clear(); formas.Clear();
        raiz = vila.transform; contadorMalhas = 0;
        pastaMalhas = PASTA_MALHAS; prefixoOculto = PREFIXO_OCULTO;
        try
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Preparando...", 0.05f);
            foreach (string n in new[] { NOME_CAMINHOS, NOME_POSTES }) { Transform t = vila.transform.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject); }
            if (!AssetDatabase.IsValidFolder(PASTA_CAM + "/Malhas")) AssetDatabase.CreateFolder(PASTA_CAM, "Malhas");
            // postes antigos
            int velhos = 0;
            foreach (Transform t in vila.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == "Poste" && t.parent != null) { Object.DestroyImmediate(t.gameObject); velhos++; }
            avisos.Add(velhos + " postes antigos removidos.");

            // centro e casas
            Vector3 centro = Vector3.zero; bool temCentro = false;
            MiniMapa mm = Object.FindAnyObjectByType<MiniMapa>();
            if (mm != null) foreach (MiniMapa.Marcador k in mm.marcadores) if (k.tipo == MiniMapa.Tipo.Vila) { centro = k.posicao; temCentro = true; }
            Transform poco = vila.transform.Find("Praca/Poco");
            if (poco != null) { centro = poco.position; temCentro = true; }
            if (!temCentro) throw new System.Exception("Não achei o centro da vila.");
            centro.y = Altura(centro.x, centro.z);

            List<Bounds> casas = new List<Bounds>();
            foreach (string pasta in new[] { "Construcoes", NOME_CASAS_NOVAS })
            {
                Transform t = vila.transform.Find(pasta);
                if (t == null) continue;
                foreach (Transform c in t)
                {
                    Bounds b; bool ok = false; b = new Bounds(c.position, Vector3.zero);
                    foreach (Renderer r in c.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds); }
                    if (ok && b.size.y > 2f) casas.Add(b);
                }
            }
            float minDist = 999f;
            foreach (Bounds b in casas) { Vector3 q = b.ClosestPoint(new Vector3(centro.x, b.center.y, centro.z)); minDist = Mathf.Min(minDist, Plano(q - centro).magnitude); }
            float raioAnel = Mathf.Clamp(minDist - 1.8f, 13.5f, 17f), raioPraca = Mathf.Min(RAIO_PRACA + 1f, raioAnel - 4f);   // raioAnel = borda de fora do calçamento redondo

            EditorUtility.DisplayProgressBar("Pinglu", "Planejando as ruas...", 0.15f);
            List<RuaV> ruas = new List<RuaV>(); List<Vector3> pontosDeRua = new List<Vector3>();
            // o calçamento redondo em volta da praça é UM disco/anel inteiro (feito mais abaixo); aqui só marcamos pontos na borda dele para os ramais
            for (int ang = 0; ang < 360; ang += 6)
                pontosDeRua.Add(centro + new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0f, Mathf.Cos(ang * Mathf.Deg2Rad)) * (raioAnel + 0.2f));
            // estrada principal (vem da rede de estradas)
            RedeDeEstradas rede = Object.FindFirstObjectByType<RedeDeEstradas>();
            if (rede != null)
                foreach (RedeDeEstradas.Estrada e in rede.estradas)
                {
                    if (e.pontos == null || e.pontos.Length < 2) continue;
                    // lado da vila: o ponto mais perto do centro; anda para fora até RAIO_LIMPEZA_VILA
                    int im = 0; float dm = 1e9f;
                    for (int i = 0; i < e.pontos.Length; i++) { float d = Plano(e.pontos[i] - centro).magnitude; if (d < dm) { dm = d; im = i; } }
                    if (dm > 40f) continue;
                    int passo = (im == 0) ? 1 : (im == e.pontos.Length - 1 ? -1 : (Plano(e.pontos[im + 1] - centro).magnitude > Plano(e.pontos[im - 1] - centro).magnitude ? 1 : -1));
                    RuaV r = new RuaV { largura = 3.8f };
                    Vector3 primeiro = Vector3.zero; bool temPrimeiro = false;
                    for (int i = im; i >= 0 && i < e.pontos.Length; i += passo)
                    {
                        Vector3 p = e.pontos[i]; float d = Plano(p - centro).magnitude;
                        if (d < raioAnel + 1f) continue;
                        if (DentroDeCasa(p, casas, 1.8f)) break;       // a estrada de pedra para antes de bater numa casa
                        if (!temPrimeiro) { Vector3 dir = Plano(p - centro).normalized; r.pts.Add(centro + dir * (raioAnel - 1.2f)); temPrimeiro = true; }
                        r.pts.Add(p);
                        if (d > RAIO_LIMPEZA_VILA) break;
                    }
                    if (r.pts.Count >= 2) { ruas.Add(r); foreach (Vector3 q in r.pts) pontosDeRua.Add(q); }
                }
            // ruas até cada casa: escolhe o ponto de rua mais perto de onde o caminho NÃO atravessa outra casa; termina na parede, nunca dentro
            // (as casas mais perto do centro primeiro, assim as mais longe se ligam aos ramais já feitos)
            List<Bounds> ordem = new List<Bounds>(casas);
            ordem.Sort((x, y) => Plano(x.center - centro).magnitude.CompareTo(Plano(y.center - centro).magnitude));
            int semRua = 0;
            foreach (Bounds b in ordem)
            {
                List<Vector3> cand = new List<Vector3>(pontosDeRua);
                cand.Sort((u, v) => DistAoRetangulo(u, b).CompareTo(DistAoRetangulo(v, b)));
                RuaV feita = null;
                for (int ci = 0; ci < cand.Count && ci < 40 && feita == null; ci++)
                {
                    Vector3 p0 = cand[ci];
                    float dist = DistAoRetangulo(p0, b);
                    if (dist > 45f) break;
                    if (dist < 1.6f) { feita = new RuaV(); break; }       // a rua já passa colada na casa
                    if (DentroDeCasa(p0, casas, 0.5f)) continue;
                    Vector3 q = new Vector3(Mathf.Clamp(p0.x, b.min.x, b.max.x), 0f, Mathf.Clamp(p0.z, b.min.z, b.max.z));
                    Vector3 alvo = q + Plano(p0 - q).normalized * 0.9f;
                    Vector3 meio = Vector3.Lerp(p0, alvo, 0.5f); Vector3 lado = new Vector3(-(alvo.z - p0.z), 0f, alvo.x - p0.x).normalized;
                    meio += lado * Mathf.Sin(ci * 1.7f + b.center.x) * 0.05f * Plano(alvo - p0).magnitude;
                    bool livre = true;
                    for (int t = 0; t <= 2 && livre; t++)
                    {
                        Vector3 a0 = t == 0 ? p0 : (t == 1 ? meio : meio), a1 = t == 0 ? meio : alvo;
                        if (t == 2) break;
                        float len = Plano(a1 - a0).magnitude;
                        for (float d = 0f; d <= len && livre; d += 0.7f)
                        {
                            Vector3 pp = Vector3.Lerp(a0, a1, d / Mathf.Max(0.01f, len));
                            foreach (Bounds o in casas) { if (o.center == b.center && o.size == b.size) continue; if (DentroDeCasa(pp, new List<Bounds> { o }, 1.3f)) { livre = false; break; } }
                            if (Plano(pp - centro).magnitude < raioAnel - 0.3f) livre = false;
                        }
                    }
                    if (!livre) continue;
                    feita = new RuaV { largura = 2.3f };
                    feita.pts.Add(p0); feita.pts.Add(meio); feita.pts.Add(alvo);
                }
                if (feita == null) { semRua++; continue; }
                if (feita.pts.Count == 0) continue;
                ruas.Add(feita);
                for (int k = 1; k <= 4; k++) pontosDeRua.Add(Vector3.Lerp(feita.pts[0], feita.pts[2], k / 4f));
            }
            if (semRua > 0) avisos.Add(semRua + " casas ficaram sem rua própria (qualquer caminho atravessaria outra casa).");

            EditorUtility.DisplayProgressBar("Pinglu", "Construindo as ruas e a praça...", 0.35f);
            Transform pastaCam = new GameObject(NOME_CAMINHOS).transform; pastaCam.SetParent(vila.transform, false);
            Undo.RegisterCreatedObjectUndo(pastaCam.gameObject, "Caminhos da vila");
            GarantirTexturaLajes();
            Material mRua = MatCam("Rua_Lajes", "Lajes_v2", 0.22f, ".png"), mAnel = mRua, mCentro = MatCam("Praca_Centro", "PracaCentro", 0.2f), mTerra = MatCam("Rua_BordaDeTerra", "Caminho", 0.05f, ".jpg", "Terreno");
            List<Vector3> V = new List<Vector3>(), Vc = new List<Vector3>(); List<Vector2> UV = new List<Vector2>(), UVc = new List<Vector2>(); List<int> T = new List<int>(), Tc = new List<int>();
            // faixa de terra batida e cascalho, um pouco mais larga e irregular, por baixo das lajes
            List<Vector3> Vb = new List<Vector3>(), Vcb = new List<Vector3>(); List<Vector2> UVb = new List<Vector2>(), UVcb = new List<Vector2>(); List<int> Tb = new List<int>(), Tcb = new List<int>();
            foreach (RuaV r in ruas) FitaDaRua(r, 3f, Vb, UVb, Tb, Vcb, UVcb, Tcb, 0.55f, 0.04f, 37f);
            MeshGo("Ruas - borda de terra", pastaCam, Vb, UVb, Tb, mTerra);
            foreach (RuaV r in ruas) FitaDaRua(r, 3.4f, V, UV, T, Vc, UVc, Tc, 0f, 0.085f);
            MeshGo("Ruas - lajes", pastaCam, V, UV, T, mRua);
            // praça
            V = new List<Vector3>(); UV = new List<Vector2>(); T = new List<int>();
            Anel(centro, 0f, raioPraca - 1.0f, 1.8f, V, UV, T);
            MeshGo("Praca - miolo", pastaCam, V, UV, T, mCentro);
            V = new List<Vector3>(); UV = new List<Vector2>(); T = new List<int>();
            Anel(centro, raioAnel - 0.2f, raioAnel + 0.9f, 3f, V, UV, T, 0.04f);
            MeshGo("Praca - borda de terra", pastaCam, V, UV, T, mTerra);
            V = new List<Vector3>(); UV = new List<Vector2>(); T = new List<int>();
            Anel(centro, raioPraca - 1.0f, raioAnel, 3.4f, V, UV, T);
            MeshGo("Praca - anel", pastaCam, V, UV, T, mAnel);
            // meio-fio redondo da praça
            {
                List<Vector3> vc = new List<Vector3>(); List<Vector2> uc = new List<Vector2>(); List<int> tc = new List<int>();
                int seg = 72;
                for (int s = 0; s <= seg; s++)
                {
                    float ang = s * Mathf.PI * 2f / seg; Vector3 d = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    Vector3 a0 = centro + d * raioPraca, a1 = centro + d * (raioPraca + 0.3f);
                    float y0 = Altura(a0.x, a0.z) + 0.07f, y1 = Altura(a1.x, a1.z) + 0.07f;
                    vc.Add(new Vector3(a0.x, y0, a0.z)); vc.Add(new Vector3(a0.x, y0 + 0.16f, a0.z)); vc.Add(new Vector3(a1.x, y1 + 0.16f, a1.z)); vc.Add(new Vector3(a1.x, y1 - 0.05f, a1.z));
                    float vv = s * raioPraca * Mathf.PI * 2f / seg / 0.9f;
                    uc.Add(new Vector2(0f, vv)); uc.Add(new Vector2(0.25f, vv)); uc.Add(new Vector2(0.5f, vv)); uc.Add(new Vector2(0.75f, vv));
                }
                for (int s = 0; s < seg; s++) for (int q = 0; q < 3; q++) { int o0 = s * 4 + q, o1 = (s + 1) * 4 + q; tc.AddRange(new[] { o0, o1 + 1, o1, o0, o0 + 1, o1 + 1 }); }
                // (sem meio-fio: a pedra afunda macia na terra, como no Zelda)
            }

            // ----- postes de rua com luz
            EditorUtility.DisplayProgressBar("Pinglu", "Postes de luz...", 0.6f);
            ConfigurarTexturas(); CriarMateriais();
            Transform pastaPostes = new GameObject(NOME_POSTES).transform; pastaPostes.SetParent(vila.transform, false);
            Undo.RegisterCreatedObjectUndo(pastaPostes.gameObject, "Postes da vila");
            Malha poste = new Malha(); PosteDeRua(poste);
            Mesh mp = null; Material[] msp = null;
            List<Vector3> postes = new List<Vector3>();
            // em volta da praça
            for (int k = 0; k < 8; k++)
            {
                float ang = (22.5f + k * 45f) * Mathf.Deg2Rad;
                postes.Add(centro + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (raioAnel - 1.0f));
            }
            // ao longo do anel e das ruas principais (do lado de fora, a cada ~12 m)
            foreach (RuaV r in ruas)
            {
                if (r.largura < 3f) continue;
                List<Vector3> sm = Suavizar(r.pts, 1.1f);
                float acum = 0f; int lado = 1;
                for (int i = 1; i < sm.Count; i++)
                {
                    acum += Plano(sm[i] - sm[i - 1]).magnitude;
                    if (acum < 12.5f) continue;
                    acum = 0f;
                    Vector3 tg = Plano(sm[i] - sm[i - 1]).normalized; Vector3 nrm = new Vector3(-tg.z, 0f, tg.x) * lado; lado = -lado;
                    Vector3 p = sm[i] + nrm * (r.largura * 0.5f + 1.05f);
                    if (DentroDeCasa(p, casas, 0.8f)) continue;
                    if (Plano(p - centro).magnitude < raioAnel + 1.2f) continue;
                    bool perto = false; foreach (Vector3 q in postes) if (Plano(q - p).magnitude < 7f) { perto = true; break; }
                    if (!perto) postes.Add(p);
                }
            }
            // um poste na porta de cada casa
            foreach (Bounds b in casas)
            {
                Vector3 alvo = b.ClosestPoint(new Vector3(centro.x, b.center.y, centro.z)); alvo.y = 0f;
                Vector3 paraCentro = Plano(centro - alvo).normalized, lado = new Vector3(-paraCentro.z, 0f, paraCentro.x);
                Vector3 p = alvo + paraCentro * 1.4f + lado * 2.0f;
                if (DentroDeCasa(p, casas, 0.5f)) continue;
                bool perto = false; foreach (Vector3 q in postes) if (Plano(q - p).magnitude < 6f) { perto = true; break; }
                if (!perto) postes.Add(p);
            }
            if (postes.Count > 45) postes.RemoveRange(45, postes.Count - 45);
            foreach (Vector3 p in postes)
            {
                Vector3 paraC = Plano(centro - p).normalized;
                GameObject g = Instanciar(mp == null ? poste : null, "Poste de Rua", NoChao(p), Quaternion.LookRotation(paraC), pastaPostes, true, ref mp, ref msp);
                GameObject lg = new GameObject("Luz do Poste"); lg.transform.SetParent(g.transform, false);
                lg.transform.localPosition = new Vector3(0f, 3.4f, 0f);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.74f, 0.44f); l.range = 17f; l.intensity = 18f; l.shadows = LightShadows.None;
                PosteDeLuz pl = g.AddComponent<PosteDeLuz>(); pl.luz = l; pl.intensidadeBase = 18f; pl.distanciaLigada = 70f;
            }

            // ----- chão: limpa as marcações antigas, tira árvores dos caminhos
            EditorUtility.DisplayProgressBar("Pinglu", "Limpando o chão e as árvores...", 0.8f);
            List<Vector3[]> segs = new List<Vector3[]>(); List<float> larg = new List<float>();
            foreach (RuaV r in ruas) { List<Vector3> sm = Suavizar(r.pts, 2f); for (int i = 1; i < sm.Count; i++) { segs.Add(new[] { sm[i - 1], sm[i] }); larg.Add(r.largura); } }
            TirarArvoresDosCaminhos(segs, larg, centro, raioAnel + 1.5f, postes);
            EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
            AssetDatabase.SaveAssets();
            avisos.Add(ruas.Count + " ruas, praça com raio " + raioPraca.ToString("0.0") + " m, anel em " + raioAnel.ToString("0.0") + " m, " + postes.Count + " postes com luz.");
        }
        catch (System.Exception e) { Debug.LogException(e); avisos.Add("Deu um erro: " + e.Message + " (veja o Console)."); }
        finally { EditorUtility.ClearProgressBar(); }

        if (!tudoEmUm) try { if (PingluTerreno.Ativo()) PingluTerreno.RefazerSilencioso(); } catch (System.Exception e) { Debug.LogException(e); }
        try { AdicionarBrilhos(); } catch (System.Exception e) { Debug.LogException(e); }
        if (!tudoEmUm) try { PingluAssentar.Executar(false); } catch (System.Exception e) { Debug.LogException(e); }
        foreach (string a in avisos) Debug.Log("[Vila] " + a);
        if (interativo) { EditorSceneManager.SaveOpenScenes(); EditorUtility.DisplayDialog("Pinglu", "Caminhos e postes da vila prontos e cena salva!\n\n- " + string.Join("\n- ", avisos), "OK"); }
    }

    static void TirarArvoresDosCaminhos(List<Vector3[]> segs, List<float> larg, Vector3 centro, float raioPraca, List<Vector3> postes)
    {
        Vector3 o = terreno.transform.position, s = dados.size;
        TreeInstance[] arv = dados.treeInstances;
        List<TreeInstance> manter = new List<TreeInstance>();
        int tirou = 0;
        foreach (TreeInstance ti in arv)
        {
            Vector3 p = new Vector3(o.x + ti.position.x * s.x, 0f, o.z + ti.position.z * s.z);
            bool tira = Plano(p - centro).magnitude < raioPraca;
            for (int i = 0; i < segs.Count && !tira; i++)
            {
                Vector2 a = new Vector2(segs[i][0].x, segs[i][0].z), b = new Vector2(segs[i][1].x, segs[i][1].z), q = new Vector2(p.x, p.z);
                Vector2 ab = b - a; float t = ab.sqrMagnitude < 0.01f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                if ((a + ab * t - q).magnitude < larg[i] * 0.5f + 1.8f) tira = true;
            }
            if (tira) { tirou++; continue; }
            manter.Add(ti);
        }
        if (tirou > 0) dados.SetTreeInstances(manter.ToArray(), true);
        avisos.Add(tirou + " árvores/arbustos tirados de cima dos caminhos.");
    }

    // poste de rua: base de pedra, haste de ferro com anéis, lanterna de vidro (que brilha) com telhadinho e pináculo
    static void PosteDeRua(Malha m)
    {
        // base de pedra (octógono chanfrado)
        m.Torno(Vector3.zero, new[] { 0.32f, 0.32f, 0.26f, 0.22f }, new[] { 0f, 0.14f, 0.24f, 0.55f }, 8, PEDRA_ESCURA, Quaternion.identity);
        m.Torno(new Vector3(0f, 0.55f, 0f), new[] { 0.14f, 0.14f, 0.09f }, new[] { 0f, 0.1f, 0.2f }, 12, FERRO, Quaternion.identity);
        // haste fina afunilando para cima
        m.Torno(new Vector3(0f, 0.75f, 0f), new[] { 0.075f, 0.062f, 0.052f, 0.046f }, new[] { 0f, 0.7f, 1.6f, 2.35f }, 12, FERRO, Quaternion.identity);
        // anéis decorativos
        foreach (float y in new[] { 0.95f, 1.6f, 2.55f })
            m.Torno(new Vector3(0f, y, 0f), new[] { 0.1f, 0.1f }, new[] { 0f, 0.07f }, 12, FERRO, Quaternion.identity);
        // encaixe da lanterna
        m.Torno(new Vector3(0f, 3.05f, 0f), new[] { 0.06f, 0.13f, 0.2f }, new[] { 0f, 0.07f, 0.12f }, 12, FERRO, Quaternion.identity);
        // lanterna: vidro (emissivo) com quatro hastes nos cantos
        m.Torno(new Vector3(0f, 3.17f, 0f), new[] { 0.17f, 0.22f, 0.22f, 0.17f }, new[] { 0f, 0.05f, 0.4f, 0.5f }, 4, LANTERNA, Quaternion.Euler(0f, 45f, 0f), false);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.Caixa(new Vector3(sx * 0.2f, 3.42f, sz * 0.2f), new Vector3(0.035f, 0.5f, 0.035f), Quaternion.identity, FERRO);
        m.Caixa(new Vector3(0f, 3.17f, 0f), new Vector3(0.5f, 0.04f, 0.5f), Quaternion.identity, FERRO);
        // telhadinho e pináculo
        m.Piramide(new Vector3(0f, 3.67f, 0f), 0.3f, 0.26f, FERRO);
        m.Cilindro(new Vector3(0f, 3.92f, 0f), 0.025f, 0.2f, 8, FERRO, Quaternion.identity);
        m.Torno(new Vector3(0f, 4.08f, 0f), new[] { 0.001f, 0.05f, 0.001f }, new[] { 0f, 0.05f, 0.1f }, 8, FERRO, Quaternion.identity, false);
        // braço decorativo (suporte de plaquinha/estandarte)
        m.Caixa(new Vector3(0.18f, 2.2f, 0f), new Vector3(0.36f, 0.03f, 0.03f), Quaternion.identity, FERRO);
        m.Caixa(new Vector3(0.1f, 2.1f, 0f), new Vector3(0.03f, 0.03f, 0.03f), Quaternion.identity, FERRO);
    }
}
#endif
