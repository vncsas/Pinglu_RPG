using System.Collections.Generic;
using UnityEngine;

// O limite da Floresta Sombria (a área marcada de vermelho no mapa).
// Fica na cena como "Limite da Floresta Sombria", com os pontos do contorno como filhos (P01, P02...).
// Para mudar o contorno: arraste os pontos na janela Scene e rode Pinglu > 43 de novo.
// O jogo usa a lista "pontos" (X e Z do mundo), que o menu 43 grava a partir dos filhos.
[ExecuteAlways]
public class LimiteDaFloresta : MonoBehaviour
{
    public Vector2[] pontos = new Vector2[0];

    public static LimiteDaFloresta Instancia { get; private set; }
    public bool Valido { get { return pontos != null && pontos.Length >= 3; } }

    private void OnEnable() { Instancia = this; }
    private void OnDisable() { if (Instancia == this) Instancia = null; }

    // lê os filhos (P01, P02...) e grava na lista
    public void Recalcular()
    {
        List<Vector2> l = new List<Vector2>();
        foreach (Transform t in transform) l.Add(new Vector2(t.position.x, t.position.z));
        if (l.Count >= 3) pontos = l.ToArray();
    }

    public bool Contem(float x, float z)
    {
        if (!Valido) return false;
        bool dentro = false;
        for (int i = 0, j = pontos.Length - 1; i < pontos.Length; j = i++)
        {
            Vector2 a = pontos[i], b = pontos[j];
            if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) dentro = !dentro;
        }
        return dentro;
    }

    // distância até a borda (positiva dentro, negativa fora)
    public float DistanciaDaBorda(float x, float z)
    {
        if (!Valido) return -9999f;
        float menor = float.MaxValue;
        Vector2 p = new Vector2(x, z);
        for (int i = 0, j = pontos.Length - 1; i < pontos.Length; j = i++)
        {
            Vector2 a = pontos[j], ab = pontos[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            menor = Mathf.Min(menor, (p - (a + ab * t)).magnitude);
        }
        return Contem(x, z) ? menor : -menor;
    }

    private void OnDrawGizmos()
    {
        Vector2[] l = pontos;
        if (transform.childCount >= 3)
        {
            l = new Vector2[transform.childCount];
            for (int i = 0; i < l.Length; i++) l[i] = new Vector2(transform.GetChild(i).position.x, transform.GetChild(i).position.z);
        }
        if (l == null || l.Length < 3) return;
        Terrain ter = Terrain.activeTerrain;
        Gizmos.color = new Color(1f, 0.15f, 0.15f);
        for (int i = 0; i < l.Length; i++)
        {
            Vector2 a = l[i], b = l[(i + 1) % l.Length];
            float ya = (ter != null ? ter.SampleHeight(new Vector3(a.x, 0f, a.y)) : 0f) + 12f, yb = (ter != null ? ter.SampleHeight(new Vector3(b.x, 0f, b.y)) : 0f) + 12f;
            Gizmos.DrawLine(new Vector3(a.x, ya, a.y), new Vector3(b.x, yb, b.y));
            Gizmos.DrawSphere(new Vector3(a.x, ya, a.y), 4f);
        }
    }
}
