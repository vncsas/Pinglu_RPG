using System;
using System.Collections.Generic;
using UnityEngine;

// PRÓLOGO DO TRAILER: reaproveita as cenas 3D da abertura do jogo (ilha, dragão e vila em chamas, cristais, corrupção, mortos, aventureiros),
// mas a frase de cada cena vira um CARTÃO de texto entre as tomadas (em vez de legenda embaixo), no estilo Elden Ring / Fable.
public partial class IntroDoJogo
{
    string cartaoPend; float cartaoA;

    // relógio da abertura: soma o tempo de cada quadro (assim a gravação em quadros fixos do trailer anda na velocidade certa; no jogo é igual ao Time.unscaledTime)
    static float tuT; static int tuQuadro = -1;
    static float TU { get { if (Time.frameCount != tuQuadro) { tuQuadro = Time.frameCount; tuT += Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime; } return tuT; } }

    public static void TocarPrologoTrailer(Action aoTerminar)
    {
        if (Ativa) { if (aoTerminar != null) aoTerminar(); return; }
        seqPendente = "prologo"; seqAoTerminar = aoTerminar;
        new GameObject("Prólogo do trailer").AddComponent<IntroDoJogo>();
    }

    static float JanelaT(float t, float a, float b, float fade) { return Mathf.Clamp01(Mathf.Min((t - a) / fade, (b - t) / fade)); }

    void BarrasCinema()
    {
        float W = Screen.width, H = Screen.height, b = Mathf.Max(0f, (H - W / 2.39f) * 0.5f);
        QuadG(new Vector2(0, 0), new Vector2(W, 0), new Vector2(W, b), new Vector2(0, b), C(0, 0, 0), C(0, 0, 0), C(0, 0, 0), C(0, 0, 0));
        QuadG(new Vector2(0, H - b), new Vector2(W, H - b), new Vector2(W, H), new Vector2(0, H), C(0, 0, 0), C(0, 0, 0), C(0, 0, 0), C(0, 0, 0));
    }

    // [cartão, tomada, cartão, tomada...]: o texto da cena vira o cartão que vem antes dela.
    // Versão curta e sem mostrar o dragão: ilha -> vila de perto (NPCs e Caio) -> vila em chamas (sombra passando) -> cristais -> corrupção -> aventureiros
    List<Cena> MontarPrologoFinal(List<Cena> intro)
    {
        List<Cena> l = new List<Cena>();
        Action<string, Cena> par = (texto, cena) =>
        {
            float dc = 2.4f;
            l.Add(new Cena { dur = dc, texto = "", semBarras = true, fadeDur = 0.35f, desenha = k => { cartaoPend = texto; cartaoA = JanelaT(k * dc, 0.2f, dc - 0.2f, 0.45f); } });
            cena.semBarras = true; cena.fadeDur = 0.5f; l.Add(cena);
        };
        // 1) a ilha vista de cima (sem neblina branca: névoa fraca e a câmera olha sempre para baixo, nunca para o horizonte)
        Cena ilha = intro[0]; ilha.dur = 5.5f; ilha.texto = "";
        ilha.amb = new Amb { sol = new Color(1f, 0.82f, 0.6f), solInt = 1.8f, solRot = new Vector3(18f, 55f, 0f), fog = new Color(0.62f, 0.66f, 0.74f), fogDen = 0.0006f, amb = new Color(0.55f, 0.55f, 0.62f), ceu = new Color(0.5f, 0.6f, 0.85f), exposicao = 1.0f, atmosfera = 1f };
        Vector3 v0 = vila;
        ilha.cam3 = k => Cam(Sv(G3(Fr3(v0, -45f, -170f), 120f), G3(Fr3(v0, -15f, -70f), 34f), k), G3(v0, -6f), Mathf.Lerp(50f, 42f, k));
        par("Valverde... uma pequena ilha esquecida entre as brumas do mar.", ilha);
        // 2) a vila de perto
        par("Em Valverde, o povo vivia em paz.", CenaVilaDePerto(8.5f));
        // 3) a vila em chamas e a sombra
        par("Até que uma fera enorme destruiu tudo.", CenaVilaEmChamas(11.5f));
        // 4) cristais
        Cena cr = intro[3]; cr.dur = 5f; cr.texto = ""; par("Um herói selou o seu poder em três cristais.", cr);
        // 5) corrupção
        Cena co = intro[7]; co.dur = 7.5f; co.texto = ""; par("Uma magia maligna corrompeu os cristais... e os mortos acordaram.", co);
        // 6) aventureiros
        Cena av = intro[9]; av.dur = 9.5f; av.texto = ""; par("Então chegam aventureiros... sem ouro, sem fama, mas com muito a provar.", av);
        return l;
    }

    void DesenharCartaoTrailer()
    {
        float W = Screen.width, H = Screen.height, e = H / 1080f;
        GUIStyle st = new GUIStyle { font = fCinzel, fontSize = Mathf.RoundToInt(60f * e), alignment = TextAnchor.MiddleCenter, wordWrap = true };
        Rect r = new Rect(W * 0.14f, H * 0.5f - 200f * e, W * 0.72f, 400f * e);
        st.normal.textColor = new Color(0f, 0f, 0f, 0.7f * cartaoA); GUI.Label(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), cartaoPend, st);
        st.normal.textColor = new Color(0.95f, 0.9f, 0.76f, cartaoA); GUI.Label(r, cartaoPend, st);
    }
}
