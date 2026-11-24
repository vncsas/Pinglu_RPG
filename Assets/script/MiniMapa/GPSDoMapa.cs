using System.Collections.Generic;
using UnityEngine;

// GPS do jogo: marque um lugar no mapa (M) e o caminho aparece no mapa e no mini-mapa,
// seguindo as estradas. Recalcula sozinho se você sair da rota. Tecla X cancela (com o mapa aberto).
public class GPSDoMapa : MonoBehaviour
{
    public static GPSDoMapa Instancia { get; private set; }

    public bool Ativo { get; private set; }
    public string NomeDoDestino { get; private set; }
    public Vector3 Destino { get; private set; }
    public float DistanciaRestante { get; private set; }
    public List<Vector3> Rota { get { return rota; } }
    public KeyCode teclaCancelar = KeyCode.X;

    private bool temEntrada; private Vector3 entrada;
    private readonly List<Vector3> rota = new List<Vector3>();
    private PlanejadorDeRotas plano;
    private Transform jogador;
    private float proximaBusca, proximoCalculo;
    private Vector3 posUltimoCalculo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Criar()
    {
        if (Instancia != null) return;
        GameObject g = new GameObject("GPS do Mapa");
        DontDestroyOnLoad(g);
        g.AddComponent<GPSDoMapa>();
    }

    private void Awake() { if (Instancia != null) { Destroy(gameObject); return; } Instancia = this; }

    // ------------------------------------------------------------------
    public void Definir(Vector3 destino, string nome, bool comEntrada, Vector3 portao)
    {
        Destino = destino; NomeDoDestino = nome; temEntrada = comEntrada; entrada = portao;
        Ativo = true;
        proximoCalculo = 0f;
        Recalcular();
        HUDJogador.Mostrar("GPS: " + nome + (DistanciaRestante > 0f ? " (" + Mathf.RoundToInt(DistanciaRestante) + " m)" : ""), 2.5f);
    }

    public void DefinirPorMarcador(MiniMapa.Marcador m)
    {
        RedeDeEstradas rede = RedeDeEstradas.Instancia;
        RedeDeEstradas.Destino d = rede != null ? rede.DestinoMaisPerto(m.posicao, m.tipo, 400f) : null;
        if (d != null) Definir(d.posicao, m.nome, d.temEntrada, d.entrada);
        else Definir(m.posicao, m.nome, false, Vector3.zero);
    }

    // clicar de novo no mesmo lugar desmarca (como em GTA V e RDR2)
    public void AlternarPorMarcador(MiniMapa.Marcador m)
    {
        if (Ativo && NomeDoDestino == m.nome) Cancelar(); else DefinirPorMarcador(m);
    }

    public void Cancelar() { Ativo = false; rota.Clear(); DistanciaRestante = 0f; }

    // ------------------------------------------------------------------
    private void Update()
    {
        if (Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + 0.5f;
            jogador = null;
            foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>())
                if (p != null && p.enabled && !p.Companheiro && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy) { jogador = p.transform; break; }
        }
        if (!Ativo) return;
        if (Input.GetKeyDown(teclaCancelar) && SessaoDeJogo.MenuAberto) { Cancelar(); return; }
        if (jogador == null) return;

        Vector3 jp = jogador.position;
        if (new Vector2(jp.x - Destino.x, jp.z - Destino.z).magnitude < 12f)
        {
            HUDJogador.Mostrar("Você chegou: " + NomeDoDestino, 3f);
            Cancelar();
            return;
        }
        Aparar(jp);   // o que você já percorreu some da linha, em tempo real
        if (Time.unscaledTime < proximoCalculo) return;
        proximoCalculo = Time.unscaledTime + 1f;

        bool mexeu = new Vector2(jp.x - posUltimoCalculo.x, jp.z - posUltimoCalculo.z).magnitude > 15f;
        if (rota.Count < 2 || mexeu || DistanciaAteARota(jp) > 30f) Recalcular();
        else AtualizarDistancia(jp);
    }

    // apaga o trecho já percorrido: a linha começa sempre no ponto da rota mais perto do jogador
    private void Aparar(Vector3 jp)
    {
        if (rota.Count < 2) return;
        int limite = Mathf.Min(rota.Count - 1, 40);
        float melhor = float.MaxValue; int idx = 0; Vector3 proj = rota[0];
        for (int i = 0; i < limite; i++)
        {
            Vector3 a = rota[i], b = rota[i + 1];
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
            float l2 = ab.sqrMagnitude;
            float t = l2 < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(new Vector2(jp.x - a.x, jp.z - a.z), ab) / l2);
            Vector3 p = Vector3.Lerp(a, b, t);
            float d = new Vector2(p.x - jp.x, p.z - jp.z).sqrMagnitude;
            if (d < melhor) { melhor = d; idx = i; proj = p; }
        }
        if (idx > 0) rota.RemoveRange(0, idx);
        rota[0] = proj;
        if (Time.frameCount % 15 == 0) AtualizarDistancia(jp);
    }

    private void Recalcular()
    {
        if (jogador == null) { foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>()) if (p != null && p.enabled && !p.Companheiro && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy) { jogador = p.transform; break; } }
        Terrain t = Terrain.activeTerrain;
        if (jogador == null || t == null) return;
        if (plano == null)
        {
            plano = new PlanejadorDeRotas(t, 8f);
            if (RedeDeEstradas.Instancia != null) plano.MarcarEstradas(RedeDeEstradas.Instancia.Polilinhas());
        }
        Vector3 de = jogador.position;
        Vector3 ate = temEntrada ? entrada : Destino;
        List<Vector3> r = plano.Rota(de, ate, true);
        if (temEntrada) r.Add(new Vector3(Destino.x, plano.AlturaEm(Destino), Destino.z));
        rota.Clear(); rota.AddRange(r);
        posUltimoCalculo = de;
        AtualizarDistancia(de);
    }

    private float DistanciaAteARota(Vector3 p)
    {
        float melhor = float.MaxValue;
        for (int i = 0; i < rota.Count; i++)
        {
            float d = new Vector2(rota[i].x - p.x, rota[i].z - p.z).magnitude;
            if (d < melhor) melhor = d;
        }
        return melhor;
    }

    private void AtualizarDistancia(Vector3 p)
    {
        if (rota.Count < 2) { DistanciaRestante = 0f; return; }
        int mais = 0; float melhor = float.MaxValue;
        for (int i = 0; i < rota.Count; i++)
        {
            float d = new Vector2(rota[i].x - p.x, rota[i].z - p.z).sqrMagnitude;
            if (d < melhor) { melhor = d; mais = i; }
        }
        float total = Mathf.Sqrt(melhor);
        for (int i = mais; i + 1 < rota.Count; i++) total += Vector3.Distance(rota[i], rota[i + 1]);
        DistanciaRestante = total;
    }
}

// Desenho de linhas grossas e bandeira por cima da interface (OnGUI), com recorte em círculo para o mini-mapa.
public static class DesenhoMapa
{
    private static Material mat;

    private static bool Preparar()
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return false;
        if (mat == null)
        {
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) return false;
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            if (mat.HasProperty("_SrcBlend")) { mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); }
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", 0);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }
        return true;
    }

    // pts em coordenadas da interface (origem no canto de cima, y para baixo)
    public static void Linha(IList<Vector2> pts, float largura, Color cor) { Poli(pts, largura, cor, false, Vector2.zero, 0f); }

    // igual, mas só desenha o que está dentro do círculo
    public static void LinhaNoCirculo(IList<Vector2> pts, float largura, Color cor, Vector2 centro, float raio) { Poli(pts, largura, cor, true, centro, raio); }

    private static void Poli(IList<Vector2> pts, float largura, Color cor, bool recortar, Vector2 centro, float raio)
    {
        if (pts == null || pts.Count < 2 || !Preparar()) return;
        GL.PushMatrix();
        mat.SetPass(0);
        GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
        GL.Begin(GL.QUADS);
        GL.Color(cor);
        float meia = largura * 0.5f;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1];
            if (recortar && !RecortarNoCirculo(ref a, ref b, centro, raio)) continue;
            Vector2 d = b - a;
            if (d.sqrMagnitude < 0.01f) continue;
            Vector2 n = new Vector2(-d.y, d.x).normalized * meia;
            Vector2 prol = d.normalized * meia * 0.6f;      // prolonga um pouco para as emendas não ficarem abertas
            a -= prol; b += prol;
            GL.Vertex3(a.x + n.x, a.y + n.y, 0f); GL.Vertex3(a.x - n.x, a.y - n.y, 0f);
            GL.Vertex3(b.x - n.x, b.y - n.y, 0f); GL.Vertex3(b.x + n.x, b.y + n.y, 0f);
        }
        GL.End();
        GL.PopMatrix();
    }

    private static bool RecortarNoCirculo(ref Vector2 a, ref Vector2 b, Vector2 c, float r)
    {
        Vector2 d = b - a, f = a - c;
        float A = Vector2.Dot(d, d);
        if (A < 0.0001f) return (a - c).sqrMagnitude <= r * r;
        float B = 2f * Vector2.Dot(f, d), C = Vector2.Dot(f, f) - r * r;
        float disc = B * B - 4f * A * C;
        if (disc < 0f) return false;
        float s = Mathf.Sqrt(disc);
        float t0 = (-B - s) / (2f * A), t1 = (-B + s) / (2f * A);
        if (t1 < 0f || t0 > 1f) return false;
        t0 = Mathf.Max(0f, t0); t1 = Mathf.Min(1f, t1);
        Vector2 na = a + d * t0, nb = a + d * t1;
        a = na; b = nb;
        return true;
    }

    // losango com um ponto no meio (destino do GPS)
    public static void Marca(Vector2 p, float tam, Color cor)
    {
        if (!Preparar()) return;
        GL.PushMatrix();
        mat.SetPass(0);
        GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
        GL.Begin(GL.QUADS);
        GL.Color(Color.black);
        Losango(p, tam * 1.25f);
        GL.Color(cor);
        Losango(p, tam);
        GL.Color(Color.white);
        Losango(p, tam * 0.35f);
        GL.End();
        GL.PopMatrix();
    }

    private static void Losango(Vector2 p, float t)
    {
        GL.Vertex3(p.x, p.y - t, 0f); GL.Vertex3(p.x + t * 0.75f, p.y, 0f);
        GL.Vertex3(p.x, p.y + t, 0f); GL.Vertex3(p.x - t * 0.75f, p.y, 0f);
    }
}
