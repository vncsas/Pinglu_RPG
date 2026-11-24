using System.Collections.Generic;
using UnityEngine;

// As estradas do mundo (criadas pelo menu Pinglu > 39) e os destinos do GPS.
// O GPS dá preferência a estas estradas ao traçar o caminho.
public class RedeDeEstradas : MonoBehaviour
{
    [System.Serializable]
    public class Estrada { public string nome; public Vector3[] pontos; public float largura = 4f; }

    [System.Serializable]
    public class Destino
    {
        public string nome;
        public MiniMapa.Tipo tipo;
        public Vector3 posicao;
        [Tooltip("Por onde se chega ao lugar (ex.: o portão do cemitério). Se vazio, vai direto à posição.")]
        public bool temEntrada;
        public Vector3 entrada;
        [Tooltip("Lugares grandes (cemitério): o mapa e o mini-mapa desenham a área murada")]
        public bool temArea;
        public Vector2 areaTamanho;
        public float areaYaw;
    }

    public List<Estrada> estradas = new List<Estrada>();
    public List<Destino> destinos = new List<Destino>();

    public static RedeDeEstradas Instancia { get; private set; }
    private void Awake() { Instancia = this; }
    private void OnDestroy() { if (Instancia == this) Instancia = null; }

    public IEnumerable<Vector3[]> Polilinhas()
    {
        foreach (Estrada e in estradas) if (e != null && e.pontos != null && e.pontos.Length > 1) yield return e.pontos;
    }

    public Destino DestinoMaisPerto(Vector3 p, MiniMapa.Tipo tipo, float maximo)
    {
        Destino melhor = null; float d = maximo;
        foreach (Destino x in destinos)
        {
            if (x.tipo != tipo) continue;
            float dd = new Vector2(x.posicao.x - p.x, x.posicao.z - p.z).magnitude;
            if (dd < d) { d = dd; melhor = x; }
        }
        return melhor;
    }
}
