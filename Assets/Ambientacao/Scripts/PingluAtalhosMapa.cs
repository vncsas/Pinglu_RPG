using UnityEngine;

// Atalhos para ver cada regiao do mapa: teclas 1 a 9 e 0 (durante o Play) levam o jogador ate la.
// Tambem existe a janela  Pinglu > Atalhos do mapa  (funciona no Play e fora dele, movendo a Scene view).
public class PingluAtalhosMapa : MonoBehaviour
{
    [System.Serializable] public struct Lugar { public string nome; public Vector2 xz; public KeyCode tecla; }
    public Lugar[] lugares = Padrao();

    public static Lugar[] Padrao() => new[]
    {
        new Lugar { nome = "1  Vila (praca)", xz = new Vector2(571f, 150f), tecla = KeyCode.Alpha1 },
        new Lugar { nome = "2  Entrada da vila e estrada", xz = new Vector2(584f, 235f), tecla = KeyCode.Alpha2 },
        new Lugar { nome = "3  Colheita e moinho", xz = new Vector2(512f, 225f), tecla = KeyCode.Alpha3 },
        new Lugar { nome = "4  Campos do leste (area aberta)", xz = new Vector2(850f, 330f), tecla = KeyCode.Alpha4 },
        new Lugar { nome = "5  Pantano e lago", xz = new Vector2(190f, 425f), tecla = KeyCode.Alpha5 },
        new Lugar { nome = "6  Floresta Sombria", xz = new Vector2(150f, 735f), tecla = KeyCode.Alpha6 },
        new Lugar { nome = "7  Terras Queimadas", xz = new Vector2(520f, 550f), tecla = KeyCode.Alpha7 },
        new Lugar { nome = "8  Reino Destruido (castelo)", xz = new Vector2(140f, 190f), tecla = KeyCode.Alpha8 },
        new Lugar { nome = "9  Cemiterio", xz = new Vector2(316f, 635f), tecla = KeyCode.Alpha9 },
        new Lugar { nome = "0  Acampamento de bandidos", xz = new Vector2(59f, 290f), tecla = KeyCode.Alpha0 },
    };

    public static Vector3 Ponto(Vector2 xz)
    {
        var t = Terrain.activeTerrain; float y = t != null ? t.SampleHeight(new Vector3(xz.x, 0, xz.y)) + t.transform.position.y : 20f;
        if (Physics.Raycast(new Vector3(xz.x, y + 80f, xz.y), Vector3.down, out var h, 200f, ~0, QueryTriggerInteraction.Ignore)) y = Mathf.Max(y, h.point.y);
        return new Vector3(xz.x, y + 1.5f, xz.y);
    }

    // o jogador humano: os companheiros CPU tambem tem PlayerLocomotion, entao nao da para pegar "o primeiro"
    static Transform AcharJogador()
    {
        if (GrupoDeAventureiros.Humano != null) return GrupoDeAventureiros.Humano;
        if (JogadorRede.Local != null) return JogadorRede.Local.transform;
        foreach (var j in FindObjectsByType<PlayerLocomotion>(FindObjectsInactive.Exclude)) if (j.GetComponent<EntradaCPU>() == null) return j.transform;
        return null;
    }

    public static void IrPara(Vector2 xz)
    {
        var p = Ponto(xz); var jog = AcharJogador();
        if (jog == null) { Debug.LogWarning("[Atalhos] nao achei o jogador"); return; }
        var cc = jog.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
        var jr = jog.GetComponent<JogadorRede>(); if (jr != null) jr.Teleportar(p, jog.rotation); else jog.SetPositionAndRotation(p, jog.rotation);
        Physics.SyncTransforms(); if (cc != null) cc.enabled = true;
        Debug.Log($"[Atalhos] {jog.name} -> {p}");
    }

    void Update()
    {
        foreach (var l in lugares) if (Input.GetKeyDown(l.tecla)) { IrPara(l.xz); break; }
    }
}
