using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Video;

// CINEMÁTICA EM VÍDEO: toca um vídeo pronto (feito em Blender, Unreal, ferramenta de IA, After Effects...) em tela cheia, com áudio e legendas.
//   Vídeo:    Assets/Resources/Videos/<nome>.webm   (também aceita .mp4/.mov no Windows)
//   Legendas: Assets/Resources/Videos/<nome>_legendas.txt  (opcional) — uma por linha:  inicio|fim|texto   (segundos, ex.: 4.5|9|Valverde, uma ilha esquecida...)
// Esc: pula.  Se o vídeo não existir, Tocar() devolve false e o jogo usa a cena em código de antes.
// Vídeos usados hoje: "Abertura" (história, antes da seleção de personagens) e "Final" (epílogo, antes do título e dos créditos). Veja CINEMATICAS.md.
public class CinematicaDeVideo : MonoBehaviour
{
    public static bool Ativa { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { Ativa = false; }

    public static bool Existe(string nome) { return Resources.Load<VideoClip>("Videos/" + nome) != null; }

    public static bool Tocar(string nome, Action aoTerminar)
    {
        if (Ativa) return false;
        VideoClip clip = Resources.Load<VideoClip>("Videos/" + nome); if (clip == null) return false;
        CinematicaDeVideo c = new GameObject("Cinemática de vídeo (" + nome + ")").AddComponent<CinematicaDeVideo>();
        c.Iniciar(clip, nome, aoTerminar); return true;
    }

    class Leg { public float ini, fim; public string texto; }
    List<Leg> legendas = new List<Leg>();
    VideoPlayer vp; RenderTexture rt; Action fim; bool menuAntes, tocou, acabou; float inicio, saida = -1f; Font fFell;

    void Iniciar(VideoClip clip, string nome, Action aoTerminar)
    {
        Ativa = true; fim = aoTerminar; menuAntes = SessaoDeJogo.MenuAberto; inicio = Time.unscaledTime;
        fFell = Resources.Load<Font>("Fontes/IMFellEnglish");
        TextAsset ta = Resources.Load<TextAsset>("Videos/" + nome + "_legendas");
        if (ta != null)
            foreach (string linha in ta.text.Split('\n'))
            {
                string[] p = linha.Trim().Split('|'); if (p.Length < 3) continue;
                float a, b;
                if (!float.TryParse(p[0].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out a)) continue;
                if (!float.TryParse(p[1].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out b)) continue;
                legendas.Add(new Leg { ini = a, fim = b, texto = p[2].Trim().Replace("\\n", "\n") });
            }
        rt = new RenderTexture((int)Mathf.Max(16, clip.width), (int)Mathf.Max(16, clip.height), 0) { hideFlags = HideFlags.HideAndDontSave };
        vp = gameObject.AddComponent<VideoPlayer>();
        vp.playOnAwake = false; vp.isLooping = false; vp.renderMode = VideoRenderMode.RenderTexture; vp.targetTexture = rt; vp.clip = clip;
        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
        AudioSource src = gameObject.AddComponent<AudioSource>(); src.playOnAwake = false; src.spatialBlend = 0f;
        vp.controlledAudioTrackCount = 1; vp.EnableAudioTrack(0, true); vp.SetTargetAudioSource(0, src);
        vp.loopPointReached += v => Terminar();
        vp.errorReceived += (v, msg) => { Debug.LogWarning("[Vídeo] erro: " + msg); Terminar(); };
        vp.prepareCompleted += v => { tocou = true; v.Play(); };
        vp.Prepare();
    }

    void Update()
    {
        if (acabou) { if (Time.unscaledTime - saida > 0.6f) Destroy(gameObject); return; }
        SessaoDeJogo.MenuAberto = true;
        if (Input.GetKeyDown(KeyCode.Escape)) { Terminar(); return; }
        if (!tocou && Time.unscaledTime - inicio > 15f) { Debug.LogWarning("[Vídeo] não preparou a tempo; pulando."); Terminar(); }
    }

    void Terminar()
    {
        if (acabou) return;
        acabou = true; saida = Time.unscaledTime; Ativa = false;
        if (vp != null) vp.Stop();
        SessaoDeJogo.MenuAberto = menuAntes;
        if (fim != null) { Action f = fim; fim = null; f(); }
    }

    void OnDestroy() { Ativa = false; if (rt != null) { rt.Release(); Destroy(rt); } }

    void OnGUI()
    {
        if (acabou) return;
        GUI.depth = -9500;
        Event ev = Event.current;
        if (ev.type == EventType.MouseDown || ev.type == EventType.KeyDown) ev.Use();
        if (ev.type != EventType.Repaint) return;
        float W = Screen.width, H = Screen.height;
        GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
        if (!tocou || rt == null) return;
        float asp = rt.width / (float)rt.height, w = W, h = W / asp; if (h > H) { h = H; w = H * asp; }
        GUI.DrawTexture(new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h), rt, ScaleMode.StretchToFill, false);
        float t = (float)vp.time, e = H / 1080f;
        foreach (Leg l in legendas)
        {
            if (t < l.ini || t > l.fim) continue;
            float a = Mathf.Clamp01(Mathf.Min((t - l.ini) / 0.4f, (l.fim - t) / 0.4f));
            GUIStyle st = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(44f * e), alignment = TextAnchor.LowerCenter, wordWrap = true };
            Rect r = new Rect(W * 0.12f, H - 250f * e, W * 0.76f, 200f * e);
            st.normal.textColor = new Color(0f, 0f, 0f, 0.9f * a); GUI.Label(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), l.texto, st);
            st.normal.textColor = new Color(0.97f, 0.93f, 0.82f, a); GUI.Label(r, l.texto, st);
        }
        GUIStyle d = new GUIStyle { font = fFell, fontSize = Mathf.RoundToInt(22f * e), alignment = TextAnchor.LowerRight };
        d.normal.textColor = new Color(0.8f, 0.77f, 0.7f, 0.5f); GUI.Label(new Rect(W - 400f * e, H - 50f * e, 380f * e, 36f * e), "[Esc] pular", d);
    }
}
