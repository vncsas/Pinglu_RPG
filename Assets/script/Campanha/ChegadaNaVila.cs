using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A CHEGADA AO MUNDO: depois da animação do portal da seleção, o personagem não sai mais do portal no meio do mapa.
// Ele aparece na estrada de pedra, caminha até o vilarejo passando pela placa "BEM-VINDO A VALVERDE" (câmera pelas costas, afastando)
// e então abre o tutorial (WASD e habilidades). O portal do meio do mapa fica escondido até a história precisar dele (Ladrão).
public static class ChegadaNaVila
{
    static readonly List<Vector3> pts = new List<Vector3>(); static float sPlaca; static bool calculado; static Vector3 vila; static bool temPlaca;
    const float PASSO = 2f, ANTES_DA_PLACA = 26f, DEPOIS_DA_PLACA = 9f;

    public static bool EmCena { get; private set; }        // true durante a cena de chegada e o tutorial (o objetivo da missão só aparece depois)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { pts.Clear(); calculado = false; sPlaca = 0f; EmCena = false; }

    public static bool Pronta { get { Calcular(); return pts.Count > 12; } }
    static float Comp { get { return (pts.Count - 1) * PASSO; } }
    static float SInicio { get { return Mathf.Max(0f, sPlaca - ANTES_DA_PLACA); } }
    static float SFim { get { return Mathf.Min(Comp, sPlaca + DEPOIS_DA_PLACA); } }

    static float Chao(Vector3 p) { Terrain t = Terrain.activeTerrain; return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y; }
    static Vector3 Pt(float s)
    {
        if (pts.Count == 0) return Vector3.zero;
        float f = Mathf.Clamp(s / PASSO, 0f, pts.Count - 1.001f); int i = (int)f; Vector3 p = Vector3.Lerp(pts[i], pts[i + 1], f - i); p.y = Chao(p); return p;
    }
    static Vector3 Tg(float s) { Vector3 d = Pt(s + 3f) - Pt(s - 3f); d.y = 0f; return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward; }
    // posição CONTÍNUA (em metros) do ponto do caminho mais perto de p: projeta no segmento mais próximo, sem saltos
    static float SMaisPerto(Vector3 p)
    {
        float melhorD = 1e9f, melhorS = 0f;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 a = new Vector2(pts[i].x, pts[i].z), b = new Vector2(pts[i + 1].x, pts[i + 1].z), q = new Vector2(p.x, p.z), ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            float d = (a + ab * t - q).sqrMagnitude;
            if (d < melhorD) { melhorD = d; melhorS = (i + t) * PASSO; }
        }
        return melhorS;
    }

    // ponto de nascimento do jogador i (0 no meio, os outros ao lado) e para onde ele olha
    public static Vector3 Lugar(int i)
    {
        float[] lados = { 0f, 1.1f, -1.1f, 2.2f, -2.2f };
        float s = SInicio; Vector3 p = Pt(s) + Vector3.Cross(Vector3.up, Tg(s)) * lados[Mathf.Clamp(i, 0, lados.Length - 1)]; p.y = Chao(p) + 0.1f; return p;
    }
    public static Quaternion Rotacao { get { return Quaternion.LookRotation(Tg(SInicio)); } }

    static void Calcular()
    {
        if (calculado) return;
        PortalDoMundo pm = PortalDoMundo.Atual;
        NpcFalante caio = null; foreach (NpcFalante n in Object.FindObjectsByType<NpcFalante>(FindObjectsSortMode.None)) if (n != null && n.nomeExibido == "Caio") { caio = n; break; }
        if (caio != null) vila = caio.transform.position; else if (pm != null) vila = pm.transform.position; else return;     // tenta de novo mais tarde
        calculado = true; pts.Clear();
        GameObject gp = GameObject.Find("Placa de Boas-vindas"); temPlaca = gp != null; Vector3 placa = temPlaca ? gp.transform.position : vila;
        RedeDeEstradas rede = Object.FindAnyObjectByType<RedeDeEstradas>();
        Vector3[] melhor = null; float melhorD = 1e9f;
        if (rede != null)
            foreach (RedeDeEstradas.Estrada e in rede.estradas)
            {
                if (e.pontos == null || e.pontos.Length < 2) continue;
                foreach (Vector3 q in e.pontos) { float d = new Vector2(q.x - placa.x, q.z - placa.z).magnitude; if (d < melhorD) { melhorD = d; melhor = e.pontos; } }
            }
        if (melhor == null || melhorD > 60f) return;                       // sem estrada: volta ao nascimento no portal (Pronta = false)
        List<Vector3> r = new List<Vector3> { melhor[0] }; float resto = 0f;
        for (int i = 1; i < melhor.Length; i++)
        {
            Vector3 a = melhor[i - 1], b = melhor[i]; float len = Vector3.Distance(a, b); if (len < 0.01f) continue;
            float pos = PASSO - resto;
            while (pos <= len) { r.Add(Vector3.Lerp(a, b, pos / len)); pos += PASSO; }
            resto = len - (pos - PASSO);
        }
        if (r.Count < 12) return;
        int ip = 0; float md = 1e9f;
        for (int i = 0; i < r.Count; i++) { float d = new Vector2(r[i].x - placa.x, r[i].z - placa.z).magnitude; if (d < md) { md = d; ip = i; } }
        int ja = Mathf.Min(r.Count - 1, ip + 10), jb = Mathf.Max(0, ip - 10);
        float da = new Vector2(r[ja].x - vila.x, r[ja].z - vila.z).magnitude, db = new Vector2(r[jb].x - vila.x, r[jb].z - vila.z).magnitude;
        if (db < da) { r.Reverse(); ip = r.Count - 1 - ip; }
        int ini = Mathf.Max(0, ip - 30), fim = Mathf.Min(r.Count - 1, ip + 25);
        for (int i = ini; i <= fim; i++) pts.Add(r[i]);
        sPlaca = (ip - ini) * PASSO;
    }

    // ------------------------------------------------------------------ a cena de chegada
    public static void Tocar(PlayerLocomotion jogador, FichaPersonagem ficha)
    {
        if (jogador == null) return;
        TutorialDoJogo.FichaAtual = ficha;
        GameObject g = new GameObject("Chegada na vila"); g.AddComponent<CenaDeChegada>().Iniciar(jogador, ficha);
    }

    [DefaultExecutionOrder(500)]
    class CenaDeChegada : MonoBehaviour
    {
        PlayerLocomotion eu; FichaPersonagem ficha; float titulo; bool menu0, parado; Font fCinzel, fFell;
        float s, sIni, sFim; Vector3 camPos, camAlvo; bool camIniciada;

        public void Iniciar(PlayerLocomotion p, FichaPersonagem f)
        {
            eu = p; ficha = f; fCinzel = Resources.Load<Font>("Fontes/CinzelBold"); fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
            StartCoroutine(Rodar());
        }

        // a câmera é calculada depois que o personagem andou (LateUpdate) e suavizada: nada de pulos
        void LateUpdate()
        {
            if (eu == null || !CameraDeCena.Ativa) return;
            Vector3 a = eu.transform.position, T = Tg(s), R = Vector3.Cross(Vector3.up, T);
            float k = Mathf.Clamp01((s - sIni) / Mathf.Max(1f, sFim - sIni)), e = k * k * (3f - 2f * k);
            float d = Mathf.Lerp(5.5f, 17f, e), h = Mathf.Lerp(1.7f, 6.5f, e);
            Vector3 alvoPos = a - T * d + R * Mathf.Lerp(1.2f, -3.2f, e); alvoPos.y = Mathf.Max(Chao(alvoPos) + 1.2f, a.y + h);
            Vector3 alvoOlhar = a + Vector3.up * Mathf.Lerp(1.5f, 2.6f, e) + T * Mathf.Lerp(4f, 14f, e);
            if (!camIniciada) { camPos = CameraDeCena.Posicao; camAlvo = alvoOlhar; camIniciada = true; }
            float f = 1f - Mathf.Exp(-5f * Time.deltaTime);                       // suavização independente do FPS
            camPos = Vector3.Lerp(camPos, alvoPos, f); camAlvo = Vector3.Lerp(camAlvo, alvoOlhar, f);
            CameraDeCena.Olhar(camPos, camAlvo);
        }

        IEnumerator Rodar()
        {
            EmCena = true; menu0 = SessaoDeJogo.MenuAberto; SessaoDeJogo.MenuAberto = false;
            TituloDeChefe.Barras(true); CameraDeCena.Ligar();
            sIni = SInicio; sFim = SFim; s = SMaisPerto(eu.transform.position);
            float t = 0f;
            while (t < 40f && s < sFim - 1.5f && eu != null)
            {
                t += Time.deltaTime; s = SMaisPerto(eu.transform.position);
                eu.CaminharAutomatico(Tg(s + 2f), 0.25f);
                foreach (PlayerLocomotion c in GrupoDeAventureiros.Companheiros) { if (c == null) continue; float sc = SMaisPerto(c.transform.position); c.CaminharAutomatico(Tg(sc + 2f), 0.25f); }
                titulo = Mathf.Clamp01((s - (sPlaca - 9f)) / 3f);
                yield return null;
            }
            // chegou na placa: para e deixa o título aparecer
            parado = true;
            for (float w = 0f; w < 2.2f; w += Time.deltaTime) { titulo = 1f; yield return null; }
            // o tutorial (WASD e habilidades)
            bool fechou = false;
            TutorialDoJogo.Abrir(ficha, () => fechou = true);
            while (!fechou) yield return null;
            for (float w = 0f; w < 0.6f; w += Time.deltaTime) { titulo = Mathf.Max(0f, titulo - Time.deltaTime * 2f); yield return null; }
            titulo = 0f;
            TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menu0;
            EmCena = false;
            Destroy(gameObject);
        }

        void OnDestroy() { EmCena = false; }

        void OnGUI()
        {
            if (titulo <= 0.01f) return;
            GUI.depth = -8000; float e = Screen.height / 1080f;
            GUIStyle st = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(84f * e), alignment = TextAnchor.MiddleCenter };
            GUIStyle sb = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(36f * e), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
            Rect r = new Rect(0f, Screen.height * 0.16f, Screen.width, 110f * e), r2 = new Rect(0f, Screen.height * 0.16f + 100f * e, Screen.width, 50f * e);
            st.normal.textColor = new Color(0f, 0f, 0f, 0.7f * titulo); GUI.Label(new Rect(r.x + 4f, r.y + 6f, r.width, r.height), "VALVERDE", st);
            st.normal.textColor = new Color(1f, 0.88f, 0.55f, titulo); GUI.Label(r, "VALVERDE", st);
            sb.normal.textColor = new Color(0f, 0f, 0f, 0.7f * titulo); GUI.Label(new Rect(r2.x + 2f, r2.y + 3f, r2.width, r2.height), "Bem-vindos, aventureiros", sb);
            sb.normal.textColor = new Color(0.97f, 0.93f, 0.82f, titulo); GUI.Label(r2, "Bem-vindos, aventureiros", sb);
        }
    }
}
