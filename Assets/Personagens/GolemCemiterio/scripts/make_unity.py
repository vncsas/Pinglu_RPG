# Generates the Unity helper scripts of the stone golem from the Auric ones (renamed classes so every character can
# live in the same project) plus the golem impact / stomp / chest-pound effects.
# usage: python make_unity.py <Auric unity dir> <out dir>
import sys, os
T, O = sys.argv[1].rstrip("/\\")+"/", sys.argv[2].rstrip("/\\")+"/"
os.makedirs(O+"Editor", exist_ok=True)
def rd(p): return open(T+p, encoding="utf-8").read()
def rep(s, a, b):
    assert a in s, a[:70]; return s.replace(a, b)
def wr(p, s): open(O+p, "w", encoding="utf-8").write(s)

# ---------------- demo screen
d = rd("DemoAuric.cs").replace("DemoAuric", "DemoGolem")
d = rep(d, "// Tela de teste do Auric Nemesis: um botao por animacao (ataques, defesa, escudo, andar...),", "// Tela de teste do Golem do Cemiterio (mini-boss): um botao por animacao (ataques, rugido, pisada...),")
i = d.index("    public static readonly string[] NoChao"); j = d.index("    Animator anim;")
d = d[:i]+'''    public static readonly string[] NoChao = { "Parado", "Andar", "Correr", "Levar_Golpe", "Levar_Golpe_Forte", "Atordoado", "Morrer" };
    public static readonly string[] Ataque = { "Postura_Luta", "Soco_Direto", "Soco_Pesado", "Varrer", "Esmagar", "Pisada", "Rugir" };
    public static string[] Estados => NoChao.Concat(Ataque).ToArray();
    static readonly System.Collections.Generic.Dictionary<string, float> Deslocamento = new()
    { { "Andar", 1.0f }, { "Correr", 2.2f } };

'''+d[j:]
d = rep(d, "anguloCamera = 25f, distancia = 3.6f", "anguloCamera = 25f, distancia = 8f")
d = rep(d, "var alvo = transform.position + Vector3.up * 1.0f;", "var alvo = transform.position + Vector3.up * 1.3f;")
d = rep(d, "distancia = GUILayout.HorizontalSlider(distancia, 1.0f, 8f)", "distancia = GUILayout.HorizontalSlider(distancia, 2.0f, 16f)")
d = d.replace("rode o menu Auric Nemesis > Montar tudo", "rode o menu Golem > Montar tudo").replace('"<b>Auric Nemesis</b>  tocando: "', '"<b>Golem do Cemiterio</b>  tocando: "')
d = rep(d, '''        GUILayout.Label("Ataque");
        foreach (var n in Ataque) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Label("Defesa");
        foreach (var n in Defesa) if (GUILayout.Button(n)) Tocar(n);
        GUILayout.Label("Escudo (habilidade)");
        foreach (var n in Escudo) if (GUILayout.Button(n)) Tocar(n);
''', '''        GUILayout.Label("Ataques do mini-boss");
        foreach (var n in Ataque) if (GUILayout.Button(n)) Tocar(n);
''')
wr("DemoGolem.cs", d)

# ---------------- impact effects (ImpactoSoco, ImpactoPisada, ImpactoPeito)
wr("ImpactoGolem.cs", '''using UnityEngine;

// Efeitos de teste chamados pelos Animation Events dos ataques do golem:
//   ImpactoSoco   (Soco_Direto, Soco_Pesado, Varrer, Esmagar): onda de choque no chao onde o punho bateu + tremor de camera
//   ImpactoPisada (Pisada): onda grande em volta dos pes + tremor forte
//   ImpactoPeito  (Rugir): pulso no peito (osso ShieldCenter) cada vez que ele bate no peito
// Os ossos MagicHand_L/R (punhos) dao o ponto do golpe. No jogo troque por VFX/som e use os mesmos eventos para dano.
public class ImpactoGolem : MonoBehaviour
{
    public Transform maoEsquerda, maoDireita, peito;
    public Color cor = new Color(0.85f, 0.78f, 0.6f);
    float tremor, tremorDur = 0.01f; Camera cam;

    void Reset() { AcharOssos(); }
    public void AcharOssos()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "MagicHand_L") maoEsquerda = t;
            if (t.name == "MagicHand_R") maoDireita = t;
            if (t.name == "ShieldCenter") peito = t;
        }
    }
    Vector3 PontoDoGolpe()
    {
        if (!maoEsquerda || !maoDireita) AcharOssos();
        Transform m = maoDireita;
        if (maoEsquerda && maoDireita && Vector3.Dot(maoEsquerda.position - transform.position, transform.forward) > Vector3.Dot(maoDireita.position - transform.position, transform.forward)) m = maoEsquerda;
        return m ? m.position : transform.position + transform.forward * 1.5f + Vector3.up;
    }
    public void ImpactoSoco()
    {
        var p = PontoDoGolpe();
        OndaGolem.Criar(new Vector3(p.x, transform.position.y + 0.04f, p.z), 2.2f, cor, 0.55f);
        Tremer(0.12f, 0.35f);
    }
    public void ImpactoPisada()
    {
        var c = transform.position + transform.forward * 0.6f + Vector3.up * 0.04f;
        OndaGolem.Criar(c, 4.5f, cor, 0.8f);
        OndaGolem.Criar(c, 2.5f, Color.white, 0.4f);
        Tremer(0.3f, 0.6f);
    }
    public void ImpactoPeito()
    {
        if (!peito) AcharOssos();
        if (peito) OndaGolem.Criar(peito.position, 1.0f, new Color(1f, 0.9f, 0.6f), 0.35f, true);
        Tremer(0.06f, 0.25f);
    }
    void Tremer(float forca, float dur) { tremor = forca; tremorDur = dur; if (!cam) cam = Camera.main; }
    void LateUpdate()
    {
        if (tremor <= 0.001f || !cam) return;
        cam.transform.position += Random.insideUnitSphere * tremor;
        tremor = Mathf.MoveTowards(tremor, 0f, tremor / tremorDur * Time.deltaTime);
    }
}

public class OndaGolem : MonoBehaviour
{
    LineRenderer lr; float t0, raio, dur; Color cor; Light luz; bool vertical; const int N = 48; static Material mat;
    public static void Criar(Vector3 centro, float raio, Color cor, float duracao, bool vertical = false)
    {
        if (!mat) mat = new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject("OndaGolem"); go.transform.position = centro;
        var o = go.AddComponent<OndaGolem>(); o.t0 = Time.time; o.raio = raio; o.cor = cor; o.dur = duracao; o.vertical = vertical;
        o.lr = go.AddComponent<LineRenderer>(); o.lr.loop = true; o.lr.positionCount = N; o.lr.useWorldSpace = false; o.lr.sharedMaterial = mat;
        o.luz = go.AddComponent<Light>(); o.luz.color = cor; o.luz.range = raio * 2f;
    }
    void Update()
    {
        float k = (Time.time - t0) / dur;
        if (k >= 1f) { Destroy(gameObject); return; }
        float r = raio * Mathf.SmoothStep(0.1f, 1f, k);
        for (int i = 0; i < N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            lr.SetPosition(i, vertical ? new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0) : new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
        }
        var c = cor; c.a = 1f - k; lr.startColor = lr.endColor = c; lr.widthMultiplier = 0.25f * (1f - k) + 0.03f;
        luz.intensity = 5f * (1f - k);
    }
}
''')

# ---------------- editor setup (menu "Golem > Montar tudo")
m = rd("Editor/MontarAuric.cs").replace("MontarAuric", "MontarGolem").replace("DemoAuric", "DemoGolem")
m = rep(m, "// Monta o Auric Nemesis neste projeto: Humanoid, texturas e material URP com brilho dourado, as animacoes dele\n// (loop + eventos de impacto e do escudo), o Animator e a cena de teste.\n// Menu: Auric Nemesis > Montar tudo",
        "// Monta o Golem do Cemiterio (mini-boss) neste projeto: Humanoid, texturas e material URP, as animacoes dele\n// (loop + eventos de impacto, pisada e batida no peito), o Animator e a cena de teste.\n// Menu: Golem > Montar tudo")
m = m.replace('const string Raiz = "Assets/Personagens/AuricNemesis";', 'const string Raiz = "Assets/Personagens/GolemCemiterio";')
m = m.replace('Raiz + "/auric_rigged.fbx"', 'Raiz + "/golem_rigged.fbx"').replace('Raiz + "/Teste_Auric.unity"', 'Raiz + "/Teste_Golem.unity"')
m = rep(m, '{ "Parado", "Andar", "Correr", "Postura_Luta", "Guarda", "Escudo_Segurar" }', '{ "Parado", "Andar", "Correr", "Postura_Luta", "Atordoado" }')
m = m.replace('[MenuItem("Auric Nemesis/', '[MenuItem("Golem/').replace("[Auric]", "[Golem]")
m = m.replace("/texturas_unity/auric_", "/texturas_unity/golem_")
m = m.replace('Raiz + "/Auric.mat"', 'Raiz + "/Golem.mat"').replace('typeof(Material), "Auric")', 'typeof(Material), "Golem")')
m = m.replace('Raiz + "/Auric_TodosOsOssos.mask"', 'Raiz + "/Golem_TodosOsOssos.mask"').replace('Raiz + "/Auric.controller"', 'Raiz + "/Golem.controller"')
m = m.replace('$"{Clipes}/auric@{nome}.fbx"', '$"{Clipes}/golem@{nome}.fbx"')
m = rep(m, '        mat.SetTexture("_EmissionMap", T("Emissive")); mat.SetColor("_EmissionColor", Color.white); mat.EnableKeyword("_EMISSION");\n        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;\n', '')
i = m.index("            // impacto dos golpes"); j = m.index("            mi.clipAnimations = new[] { c };")
m = m[:i]+'''            // eventos (tempo normalizado 0..1 = quadro de impacto / total): onde o golpe acerta
            var golpes = new System.Collections.Generic.Dictionary<string, (string, float)> {
                { "Soco_Direto", ("ImpactoSoco", 0.40f) }, { "Soco_Pesado", ("ImpactoSoco", 0.43f) }, { "Varrer", ("ImpactoSoco", 0.48f) },
                { "Esmagar", ("ImpactoSoco", 0.57f) }, { "Pisada", ("ImpactoPisada", 0.53f) } };
            if (golpes.ContainsKey(nome)) c.events = new[] { new AnimationEvent { functionName = golpes[nome].Item1, time = golpes[nome].Item2 } };
            if (nome == "Rugir") c.events = new[] { 0.39f, 0.56f, 0.73f }.Select(t => new AnimationEvent { functionName = "ImpactoPeito", time = t }).ToArray();
'''+m[j:]
i = m.index("            var depois = new System.Collections.Generic.Dictionary<string, string> {"); j = m.index("            foreach (var kv in depois)")
m = m[:i]+'''            var depois = new System.Collections.Generic.Dictionary<string, string> {
                { "Levar_Golpe", "Postura_Luta" }, { "Levar_Golpe_Forte", "Postura_Luta" }, { "Soco_Direto", "Postura_Luta" }, { "Soco_Pesado", "Postura_Luta" },
                { "Varrer", "Postura_Luta" }, { "Esmagar", "Postura_Luta" }, { "Pisada", "Postura_Luta" }, { "Rugir", "Postura_Luta" } };
'''+m[j:]
m = rep(m, 'go.name = "AuricNemesis";', 'go.name = "GolemCemiterio";')
m = rep(m, "go.AddComponent<EscudoAuric>().AcharOssos(); go.AddComponent<ImpactoAuric>().AcharOssos(); go.AddComponent<CapaAuric>();", "go.AddComponent<ImpactoGolem>().AcharOssos();")
m = rep(m, "Camera.main.transform.position = new Vector3(1.5f, 1.3f, 3.2f); Camera.main.transform.LookAt(new Vector3(0, 1, 0));", "Camera.main.transform.position = new Vector3(3f, 2.6f, 7f); Camera.main.transform.LookAt(new Vector3(0, 1.3f, 0));")
wr("Editor/MontarGolem.cs", m)
print("ok")
