using System;
using UnityEngine;

// Barra de vida de chefe (estilo Elden Ring): embaixo, fina e comprida, com o nome em cima à esquerda,
// rastro dourado do dano e o dano acumulado à direita. Serve para qualquer chefe: basta chamar BarraDeChefe.Criar.
public class BarraDeChefe : MonoBehaviour
{
    private Vida vida; private string nome; private Font fonte; private Func<bool> visivel;
    private Color corVida = new Color(0.82f, 0.12f, 0.1f), corBase = new Color(0.5f, 0.05f, 0.05f);
    private float danoAcum, danoAte, ultimoDano = -10f, trilha = 1f, exibir;

    public static BarraDeChefe Criar(GameObject alvo, Vida vida, string nome, string fonteResources, Func<bool> visivel)
    {
        BarraDeChefe b = alvo.AddComponent<BarraDeChefe>();
        b.vida = vida; b.nome = nome; b.visivel = visivel;
        b.fonte = string.IsNullOrEmpty(fonteResources) ? null : Resources.Load<Font>(fonteResources);
        vida.AoLevarDano += (d, o) => { b.danoAcum += d; b.danoAte = Time.time + 2.2f; b.ultimoDano = Time.time; };
        return b;
    }

    public BarraDeChefe Cores(Color vivo, Color escuro) { corVida = vivo; corBase = escuro; return this; }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || vida == null) return;
        bool mostrar = visivel != null && visivel() && !SessaoDeJogo.MenuAberto;
        exibir = Mathf.MoveTowards(exibir, mostrar ? 1f : 0f, Time.unscaledDeltaTime * (mostrar ? 0.9f : 2.5f));
        if (exibir <= 0.01f) return;
        float fr = Mathf.Clamp01(vida.Fracao);
        if (fr >= trilha) trilha = fr;
        else if (Time.time - ultimoDano > 0.55f) trilha = Mathf.MoveTowards(trilha, fr, Time.unscaledDeltaTime * 0.3f);
        if (Time.time > danoAte) danoAcum = 0f;

        float W = Screen.width, H = Screen.height, e = H / 1080f, a = exibir;
        float w = W * 0.44f, h = 15f * e, x = (W - w) * 0.5f, y = H * 0.875f;
        Texture2D px = Texture2D.whiteTexture;
        GUIStyle sn = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontSize = Mathf.RoundToInt(27 * e), font = fonte, clipping = TextClipping.Overflow };
        sn.normal.textColor = new Color(0f, 0f, 0f, 0.85f * a); GUI.Label(new Rect(x + 2f * e, y - 38f * e + 2f * e, w, 34f * e), nome, sn);
        sn.normal.textColor = new Color(0.93f, 0.9f, 0.84f, a); GUI.Label(new Rect(x, y - 38f * e, w, 34f * e), nome, sn);
        if (danoAcum > 0.5f)
        {
            GUIStyle sd = new GUIStyle(sn) { alignment = TextAnchor.LowerRight, fontSize = Mathf.RoundToInt(24 * e), font = null };
            float va = Mathf.Clamp01((danoAte - Time.time) / 0.5f);
            sd.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a * va); GUI.Label(new Rect(x + 2f * e, y - 36f * e + 2f * e, w, 32f * e), Mathf.RoundToInt(danoAcum).ToString(), sd);
            sd.normal.textColor = new Color(0.95f, 0.82f, 0.55f, a * va); GUI.Label(new Rect(x, y - 36f * e, w, 32f * e), Mathf.RoundToInt(danoAcum).ToString(), sd);
        }
        GUI.color = new Color(0f, 0f, 0f, 0.7f * a); GUI.DrawTexture(new Rect(x - 4f * e, y - 4f * e, w + 8f * e, h + 8f * e), px);
        GUI.color = new Color(0.62f, 0.55f, 0.42f, 0.9f * a);
        GUI.DrawTexture(new Rect(x - 4f * e, y - 4f * e, w + 8f * e, 1.5f * e), px); GUI.DrawTexture(new Rect(x - 4f * e, y + h + 2.5f * e, w + 8f * e, 1.5f * e), px);
        GUI.DrawTexture(new Rect(x - 4f * e, y - 4f * e, 1.5f * e, h + 8f * e), px); GUI.DrawTexture(new Rect(x + w + 2.5f * e, y - 4f * e, 1.5f * e, h + 8f * e), px);
        for (int lado = 0; lado < 2; lado++)
        {
            Vector2 c = new Vector2(lado == 0 ? x - 12f * e : x + w + 12f * e, y + h * 0.5f);
            Matrix4x4 antes = GUI.matrix; GUIUtility.RotateAroundPivot(45f, c);
            GUI.color = new Color(0.75f, 0.66f, 0.46f, a); GUI.DrawTexture(new Rect(c.x - 5f * e, c.y - 5f * e, 10f * e, 10f * e), px);
            GUI.color = new Color(0.08f, 0.05f, 0.04f, a); GUI.DrawTexture(new Rect(c.x - 2.5f * e, c.y - 2.5f * e, 5f * e, 5f * e), px);
            GUI.matrix = antes;
        }
        GUI.color = new Color(0.86f, 0.66f, 0.3f, 0.95f * a); GUI.DrawTexture(new Rect(x, y, w * trilha, h), px);
        GUI.color = new Color(corBase.r, corBase.g, corBase.b, a); GUI.DrawTexture(new Rect(x, y, w * fr, h), px);
        GUI.color = new Color(corVida.r, corVida.g, corVida.b, a); GUI.DrawTexture(new Rect(x, y, w * fr, h * 0.6f), px);
        GUI.color = new Color(1f, 0.4f, 0.3f, 0.5f * a); GUI.DrawTexture(new Rect(x, y, w * fr, h * 0.22f), px);
        float golpe = Mathf.Clamp01(1f - (Time.time - ultimoDano) * 4f);
        if (golpe > 0f) { GUI.color = new Color(1f, 0.9f, 0.8f, 0.45f * golpe * a); GUI.DrawTexture(new Rect(x, y, w * fr, h), px); }
        GUI.color = Color.white;
    }
}
