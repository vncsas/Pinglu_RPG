using System.Collections.Generic;
using UnityEngine;

// Nomes dos territórios no mapa: um nome para cada território, cada um num lugar próprio, sem um em cima do outro.
// Os biomas do jogo são vários círculos (a Floresta Assombrada, por exemplo, são uns 14); aqui escolhemos UM ponto
// por nome, dentro do território, o mais longe possível dos outros nomes e dos ícones dos locais.
public static class NomesDoMapa
{
    // nome bonito (com acento) para mostrar; o nome "de dentro" do bioma continua o mesmo
    public static string Exibir(string bioma)
    {
        if (string.IsNullOrEmpty(bioma)) return bioma;
        switch (bioma)
        {
            case "Pantano": return "Pântano";
            case "Pantano Sombrio": return "Pântano Sombrio";
            case "Reino Destruido": return "Reino Destruído";
            case "Cemiterio": return "Cemitério";
            default: return bioma;
        }
    }

    static Dictionary<string, Vector3> cache; static int assinatura;

    // ponto no mundo onde o nome do território fica
    public static Vector3 Ancora(ClimaDosBiomas clima, IList<MiniMapa.Marcador> marcadores, string nome, Vector3 padrao)
    {
        if (clima == null || clima.biomas == null) return padrao;
        int ass = clima.biomas.Length * 31 + (marcadores != null ? marcadores.Count : 0);
        if (cache == null || ass != assinatura) { cache = Calcular(clima, marcadores); assinatura = ass; }
        Vector3 v; return cache.TryGetValue(nome, out v) ? v : padrao;
    }

    // "tamanho" de um nome no mapa, em metros (largura e altura aproximadas) e de um ícone
    const float LARG_NOME = 330f, ALT_NOME = 46f, LARG_ICONE = 190f, ALT_ICONE = 70f;

    static Dictionary<string, Vector3> Calcular(ClimaDosBiomas clima, IList<MiniMapa.Marcador> marcadores)
    {
        // nomes únicos e seus círculos
        List<string> nomes = new List<string>(); Dictionary<string, List<ClimaDosBiomas.Bioma>> circulos = new Dictionary<string, List<ClimaDosBiomas.Bioma>>();
        foreach (ClimaDosBiomas.Bioma b in clima.biomas)
        {
            if (b == null || string.IsNullOrEmpty(b.nome)) continue;
            if (!circulos.ContainsKey(b.nome)) { circulos[b.nome] = new List<ClimaDosBiomas.Bioma>(); nomes.Add(b.nome); }
            circulos[b.nome].Add(b);
        }
        // quem tem menos círculos escolhe primeiro (fica no centro dele); os grandes escolhem depois, nos espaços que sobraram
        nomes.Sort((a, b) => circulos[a].Count.CompareTo(circulos[b].Count));

        List<Vector2> fixos = new List<Vector2>();            // ícones dos locais
        if (marcadores != null) foreach (MiniMapa.Marcador m in marcadores) fixos.Add(new Vector2(m.posicao.x, m.posicao.z));
        List<Vector2> postos = new List<Vector2>();           // nomes já colocados
        Dictionary<string, Vector3> res = new Dictionary<string, Vector3>();

        foreach (string nome in nomes)
        {
            Vector2 melhor = Vector2.zero; float melhorNota = float.NegativeInfinity; float y = 0f;
            foreach (ClimaDosBiomas.Bioma b in circulos[nome])
            {
                y = b.centro.y;
                Vector2 c = new Vector2(b.centro.x, b.centro.z);
                // candidatos: o centro e pontos em volta, dentro do círculo
                for (int i = -1; i < 24; i++)
                {
                    Vector2 p = c;
                    if (i >= 0) { float a = i * Mathf.PI * 2f / 24f; float r = (i % 2 == 0 ? 0.35f : 0.6f) * b.raio; p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }
                    if (p.x < 90f || p.x > 910f || p.y < 40f || p.y > 960f) continue;               // não encosta na borda do mapa
                    float folga = Mathf.Min(Folga(p, postos, LARG_NOME, ALT_NOME), Folga(p, fixos, LARG_ICONE, ALT_ICONE));
                    float nota = Mathf.Min(folga, 3f) * 100f - (p - c).magnitude * 0.05f;            // longe dos outros; perto do centro como desempate
                    if (nota > melhorNota) { melhorNota = nota; melhor = p; }
                }
            }
            if (float.IsNegativeInfinity(melhorNota)) { Bioma0(circulos[nome], out melhor, out y); }
            postos.Add(melhor); res[nome] = new Vector3(melhor.x, y, melhor.y);
        }
        return res;
    }

    static void Bioma0(List<ClimaDosBiomas.Bioma> l, out Vector2 p, out float y) { p = new Vector2(l[0].centro.x, l[0].centro.z); y = l[0].centro.y; }

    // quanto o ponto está livre dos outros: >= 1 = não sobrepõe nenhum
    static float Folga(Vector2 p, List<Vector2> outros, float largura, float altura)
    {
        float menor = 10f;
        foreach (Vector2 o in outros)
        {
            float f = Mathf.Max(Mathf.Abs(p.x - o.x) / largura, Mathf.Abs(p.y - o.y) / altura);
            if (f < menor) menor = f;
        }
        return menor;
    }

    // afasta na tela os nomes que ficaram se tocando (por causa da perspectiva do mapa 3D)
    public static void Desamontoar(List<Rect> rects)
    {
        List<int> ordem = new List<int>(); for (int i = 0; i < rects.Count; i++) ordem.Add(i);
        ordem.Sort((a, b) => rects[a].y.CompareTo(rects[b].y));
        for (int ii = 0; ii < ordem.Count; ii++)
        {
            Rect r = rects[ordem[ii]];
            for (int jj = 0; jj < ii; jj++)
            {
                Rect o = rects[ordem[jj]];
                if (r.Overlaps(o)) { r.y = o.yMax + 2f; }
            }
            rects[ordem[ii]] = r;
        }
    }
}
