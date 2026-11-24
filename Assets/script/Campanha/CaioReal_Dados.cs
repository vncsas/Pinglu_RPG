// ARQUIVO GERADO (não edite à mão): a especificação do Caio real fica em caio.py do scratchpad; rode "python3 caio.py cs <arquivo>" para atualizar.
using UnityEngine;

// Dados do modelo do Caio (coordenador do curso): juntas, peças e cores. O desenho e a animação ficam em CaioReal.cs.
public static class CaioRealDados
{
    public struct Junta { public string nome, pai; public Vector3 pos; public Junta(string n, string p, Vector3 q) { nome = n; pai = p; pos = q; } }
    public struct Parte
    {
        public string nome, junta, forma, mat, tag; public Vector3 pos, esc, rot; public float extra;
        public Parte(string n, string j, string fo, Vector3 p, Vector3 e, string m, Vector3 r, float ex, string t) { nome = n; junta = j; forma = fo; pos = p; esc = e; mat = m; rot = r; extra = ex; tag = t; }
    }

    public static readonly Junta[] Juntas =
    {
        new Junta("raiz", "", new Vector3(0f, 0f, 0f)),
        new Junta("pelvis", "raiz", new Vector3(0f, 0.95f, 0f)),
        new Junta("peito", "pelvis", new Vector3(0f, 0.1f, 0f)),
        new Junta("pescoco", "peito", new Vector3(0f, 0.5f, 0f)),
        new Junta("cabeca", "pescoco", new Vector3(0f, 0.07f, 0f)),
        new Junta("ombroE", "peito", new Vector3(0.215f, 0.43f, 0f)),
        new Junta("cotoveloE", "ombroE", new Vector3(0f, -0.3f, 0f)),
        new Junta("punhoE", "cotoveloE", new Vector3(0f, -0.27f, 0f)),
        new Junta("ombroD", "peito", new Vector3(-0.215f, 0.43f, 0f)),
        new Junta("cotoveloD", "ombroD", new Vector3(0f, -0.3f, 0f)),
        new Junta("punhoD", "cotoveloD", new Vector3(0f, -0.27f, 0f)),
        new Junta("quadrilE", "pelvis", new Vector3(0.095f, -0.02f, 0f)),
        new Junta("joelhoE", "quadrilE", new Vector3(0f, -0.45f, 0f)),
        new Junta("tornozeloE", "joelhoE", new Vector3(0f, -0.43f, 0f)),
        new Junta("quadrilD", "pelvis", new Vector3(-0.095f, -0.02f, 0f)),
        new Junta("joelhoD", "quadrilD", new Vector3(0f, -0.45f, 0f)),
        new Junta("tornozeloD", "joelhoD", new Vector3(0f, -0.43f, 0f)),
    };

    public static Color Cor(string mat)
    {
        switch (mat)
        {
            case "pele": return new Color(0.93f, 0.76f, 0.66f);
            case "pelesom": return new Color(0.84f, 0.64f, 0.55f);
            case "barba": return new Color(0.86f, 0.69f, 0.59f);
            case "cabelo": return new Color(0.46f, 0.29f, 0.17f);
            case "cabeloL": return new Color(0.6f, 0.42f, 0.24f);
            case "sobr": return new Color(0.38f, 0.25f, 0.15f);
            case "oculos": return new Color(0.05f, 0.05f, 0.06f);
            case "branco": return new Color(0.95f, 0.95f, 0.93f);
            case "iris": return new Color(0.28f, 0.2f, 0.12f);
            case "pupila": return new Color(0.03f, 0.03f, 0.03f);
            case "labio": return new Color(0.82f, 0.55f, 0.5f);
            case "boca": return new Color(0.25f, 0.08f, 0.08f);
            case "dente": return new Color(0.96f, 0.94f, 0.88f);
            case "tunica": return new Color(0.1f, 0.2f, 0.62f);
            case "tunicaE": return new Color(0.07f, 0.14f, 0.46f);
            case "colete": return new Color(0.16f, 0.12f, 0.1f);
            case "calca": return new Color(0.09f, 0.11f, 0.2f);
            case "bota": return new Color(0.25f, 0.15f, 0.09f);
            case "cinto": return new Color(0.3f, 0.18f, 0.09f);
            case "ouro": return new Color(0.85f, 0.68f, 0.25f);
            case "madeira": return new Color(0.38f, 0.24f, 0.12f);
            case "cristal": return new Color(0.35f, 0.65f, 1f);
        }
        return Color.white;
    }

    public static readonly Parte[] Partes =
    {
        new Parte("craneo", "cabeca", "ell", new Vector3(0f, 0.118f, 0f), new Vector3(0.192f, 0.255f, 0.215f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("queixo", "cabeca", "ell", new Vector3(0f, 0.03f, 0.03f), new Vector3(0.15f, 0.15f, 0.15f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("barba", "cabeca", "ell", new Vector3(0f, 0.035f, 0.03f), new Vector3(0.158f, 0.12f, 0.14f), "barba", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("bigode", "cabeca", "ell", new Vector3(0f, 0.064f, 0.104f), new Vector3(0.07f, 0.018f, 0.03f), "barba", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("orelhaE", "cabeca", "ell", new Vector3(0.103f, 0.115f, 0f), new Vector3(0.03f, 0.062f, 0.042f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("orelhaD", "cabeca", "ell", new Vector3(-0.103f, 0.115f, 0f), new Vector3(0.03f, 0.062f, 0.042f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("cabeloTopo", "cabeca", "ell", new Vector3(0f, 0.22f, -0.015f), new Vector3(0.205f, 0.14f, 0.235f), "cabelo", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("topete", "cabeca", "ell", new Vector3(0f, 0.262f, 0.045f), new Vector3(0.17f, 0.075f, 0.13f), "cabeloL", new Vector3(-18f, 0f, 0f), 1f, ""),
        new Parte("topete2", "cabeca", "ell", new Vector3(0.02f, 0.255f, 0.085f), new Vector3(0.11f, 0.05f, 0.08f), "cabeloL", new Vector3(-34f, 10f, 0f), 1f, ""),
        new Parte("cabeloLadoE", "cabeca", "ell", new Vector3(0.088f, 0.165f, -0.015f), new Vector3(0.05f, 0.13f, 0.17f), "cabelo", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("cabeloLadoD", "cabeca", "ell", new Vector3(-0.088f, 0.165f, -0.015f), new Vector3(0.05f, 0.13f, 0.17f), "cabelo", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("tufo1", "cabeca", "ell", new Vector3(0.045f, 0.28f, 0.02f), new Vector3(0.09f, 0.06f, 0.11f), "cabeloL", new Vector3(-10f, 0f, 15f), 1f, ""),
        new Parte("tufo2", "cabeca", "ell", new Vector3(-0.05f, 0.275f, 0f), new Vector3(0.09f, 0.055f, 0.11f), "cabelo", new Vector3(-8f, 0f, -15f), 1f, ""),
        new Parte("tufo3", "cabeca", "ell", new Vector3(0f, 0.29f, -0.035f), new Vector3(0.1f, 0.06f, 0.1f), "cabelo", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("cabeloNuca", "cabeca", "ell", new Vector3(0f, 0.105f, -0.085f), new Vector3(0.19f, 0.17f, 0.12f), "cabelo", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("olhoBrancoE", "cabeca", "ell", new Vector3(0.046f, 0.128f, 0.103f), new Vector3(0.042f, 0.03f, 0.026f), "branco", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("irisE", "cabeca", "ell", new Vector3(0.046f, 0.128f, 0.1155f), new Vector3(0.024f, 0.024f, 0.012f), "iris", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("pupilaE", "cabeca", "ell", new Vector3(0.046f, 0.128f, 0.1215f), new Vector3(0.012f, 0.012f, 0.006f), "pupila", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("palpebraE", "cabeca", "ell", new Vector3(0.046f, 0.1415f, 0.106f), new Vector3(0.046f, 0.004f, 0.03f), "pele", new Vector3(0f, 0f, 0f), 1f, "palpebra"),
        new Parte("sobrancelhaE", "cabeca", "cap", new Vector3(0.047f, 0.158f, 0.108f), new Vector3(0.012f, 0.056f, 0.014f), "sobr", new Vector3(0f, 0f, 98f), 1f, "sobrancelha"),
        new Parte("olhoBrancoD", "cabeca", "ell", new Vector3(-0.046f, 0.128f, 0.103f), new Vector3(0.042f, 0.03f, 0.026f), "branco", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("irisD", "cabeca", "ell", new Vector3(-0.046f, 0.128f, 0.1155f), new Vector3(0.024f, 0.024f, 0.012f), "iris", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("pupilaD", "cabeca", "ell", new Vector3(-0.046f, 0.128f, 0.1215f), new Vector3(0.012f, 0.012f, 0.006f), "pupila", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("palpebraD", "cabeca", "ell", new Vector3(-0.046f, 0.1415f, 0.106f), new Vector3(0.046f, 0.004f, 0.03f), "pele", new Vector3(0f, 0f, 0f), 1f, "palpebra"),
        new Parte("sobrancelhaD", "cabeca", "cap", new Vector3(-0.047f, 0.158f, 0.108f), new Vector3(0.012f, 0.056f, 0.014f), "sobr", new Vector3(0f, 0f, 82f), 1f, "sobrancelha"),
        new Parte("nariz", "cabeca", "ell", new Vector3(0f, 0.092f, 0.117f), new Vector3(0.034f, 0.055f, 0.048f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("narizPonta", "cabeca", "ell", new Vector3(0f, 0.075f, 0.134f), new Vector3(0.034f, 0.028f, 0.032f), "pelesom", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("bocaInterna", "cabeca", "ell", new Vector3(0f, 0.042f, 0.1f), new Vector3(0.056f, 0.004f, 0.022f), "boca", new Vector3(0f, 0f, 0f), 1f, "boca"),
        new Parte("dentes", "cabeca", "ell", new Vector3(0f, 0.046f, 0.109f), new Vector3(0.046f, 0.008f, 0.012f), "dente", new Vector3(0f, 0f, 0f), 1f, "dentes"),
        new Parte("labioSup", "cabeca", "ell", new Vector3(0f, 0.052f, 0.109f), new Vector3(0.052f, 0.009f, 0.026f), "labio", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("labioInf", "cabeca", "ell", new Vector3(0f, 0.037f, 0.108f), new Vector3(0.046f, 0.01f, 0.026f), "labio", new Vector3(0f, 0f, 0f), 1f, "labioInf"),
        new Parte("cantoE", "cabeca", "ell", new Vector3(0.032f, 0.049f, 0.104f), new Vector3(0.02f, 0.012f, 0.022f), "labio", new Vector3(0f, 0f, 25f), 1f, ""),
        new Parte("cantoD", "cabeca", "ell", new Vector3(-0.032f, 0.049f, 0.104f), new Vector3(0.02f, 0.012f, 0.022f), "labio", new Vector3(0f, 0f, -25f), 1f, ""),
        new Parte("bochechaE", "cabeca", "ell", new Vector3(0.052f, 0.07f, 0.092f), new Vector3(0.03f, 0.03f, 0.026f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("bochechaD", "cabeca", "ell", new Vector3(-0.052f, 0.07f, 0.092f), new Vector3(0.03f, 0.03f, 0.026f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("aroE", "cabeca", "tor", new Vector3(0.047f, 0.128f, 0.121f), new Vector3(0.082f, 0.06f, 0.008f), "oculos", new Vector3(0f, 0f, 0f), 3.2f, ""),
        new Parte("hasteE", "cabeca", "cap", new Vector3(0.0985f, 0.132f, 0.05f), new Vector3(0.007f, 0.145f, 0.007f), "oculos", new Vector3(90f, 0f, 0f), 1f, ""),
        new Parte("aroD", "cabeca", "tor", new Vector3(-0.047f, 0.128f, 0.121f), new Vector3(0.082f, 0.06f, 0.008f), "oculos", new Vector3(0f, 0f, 0f), 3.2f, ""),
        new Parte("hasteD", "cabeca", "cap", new Vector3(-0.0985f, 0.132f, 0.05f), new Vector3(0.007f, 0.145f, 0.007f), "oculos", new Vector3(90f, 0f, 0f), 1f, ""),
        new Parte("ponte", "cabeca", "cap", new Vector3(0f, 0.134f, 0.127f), new Vector3(0.007f, 0.025f, 0.007f), "oculos", new Vector3(0f, 0f, 90f), 1f, ""),
        new Parte("pescoco", "pescoco", "cap", new Vector3(0f, 0f, 0f), new Vector3(0.105f, 0.17f, 0.105f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("torso", "peito", "fru", new Vector3(0f, 0.26f, 0f), new Vector3(0.3f, 0.52f, 0.205f), "tunica", new Vector3(0f, 0f, 0f), 1.3f, ""),
        new Parte("ombros", "peito", "cap", new Vector3(0f, 0.465f, 0f), new Vector3(0.115f, 0.38f, 0.13f), "tunica", new Vector3(0f, 0f, 90f), 1f, ""),
        new Parte("gola", "peito", "tor", new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.15f, 0.04f), "colete", new Vector3(90f, 0f, 0f), 1f, ""),
        new Parte("cintoC", "pelvis", "fru", new Vector3(0f, 0.07f, 0f), new Vector3(0.3f, 0.07f, 0.205f), "cinto", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("fivela", "pelvis", "box", new Vector3(0f, 0.07f, 0.108f), new Vector3(0.05f, 0.05f, 0.012f), "ouro", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("saia", "pelvis", "fru", new Vector3(0f, -0.14f, 0f), new Vector3(0.37f, 0.34f, 0.27f), "tunica", new Vector3(0f, 0f, 0f), 0.84f, ""),
        new Parte("bolsa", "pelvis", "box", new Vector3(-0.19f, -0.04f, 0.03f), new Vector3(0.07f, 0.11f, 0.06f), "cinto", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("ombroE", "ombroE", "ell", new Vector3(0f, 0f, 0f), new Vector3(0.115f, 0.115f, 0.115f), "tunica", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("bracoE", "ombroE", "cap", new Vector3(0f, -0.14f, 0f), new Vector3(0.088f, 0.31f, 0.088f), "tunica", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("mangaE", "cotoveloE", "fru", new Vector3(0f, 0f, 0f), new Vector3(0.093f, 0.06f, 0.093f), "tunicaE", new Vector3(0f, 0f, 0f), 0.95f, ""),
        new Parte("antebracoE", "cotoveloE", "cap", new Vector3(0f, -0.14f, 0f), new Vector3(0.07f, 0.3f, 0.07f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("maoE", "punhoE", "ell", new Vector3(0f, -0.055f, 0.005f), new Vector3(0.078f, 0.115f, 0.05f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("dedosE", "punhoE", "ell", new Vector3(0f, -0.105f, 0.012f), new Vector3(0.07f, 0.07f, 0.045f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("polegarE", "punhoE", "cap", new Vector3(0.036f, -0.045f, 0.02f), new Vector3(0.024f, 0.07f, 0.024f), "pele", new Vector3(0f, 0f, 20f), 1f, ""),
        new Parte("ombroD", "ombroD", "ell", new Vector3(0f, 0f, 0f), new Vector3(0.115f, 0.115f, 0.115f), "tunica", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("bracoD", "ombroD", "cap", new Vector3(0f, -0.14f, 0f), new Vector3(0.088f, 0.31f, 0.088f), "tunica", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("mangaD", "cotoveloD", "fru", new Vector3(0f, 0f, 0f), new Vector3(0.093f, 0.06f, 0.093f), "tunicaE", new Vector3(0f, 0f, 0f), 0.95f, ""),
        new Parte("antebracoD", "cotoveloD", "cap", new Vector3(0f, -0.14f, 0f), new Vector3(0.07f, 0.3f, 0.07f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("maoD", "punhoD", "ell", new Vector3(0f, -0.055f, 0.005f), new Vector3(0.078f, 0.115f, 0.05f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("dedosD", "punhoD", "ell", new Vector3(0f, -0.105f, 0.012f), new Vector3(0.07f, 0.07f, 0.045f), "pele", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("polegarD", "punhoD", "cap", new Vector3(-0.036f, -0.045f, 0.02f), new Vector3(0.024f, 0.07f, 0.024f), "pele", new Vector3(0f, 0f, -20f), 1f, ""),
        new Parte("coxaE", "quadrilE", "cap", new Vector3(0f, -0.215f, 0f), new Vector3(0.15f, 0.5f, 0.155f), "calca", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("canelaE", "joelhoE", "cap", new Vector3(0f, -0.215f, 0f), new Vector3(0.115f, 0.46f, 0.12f), "calca", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("canoE", "tornozeloE", "fru", new Vector3(0f, 0.07f, 0f), new Vector3(0.125f, 0.16f, 0.13f), "bota", new Vector3(0f, 0f, 0f), 1.08f, ""),
        new Parte("peE", "tornozeloE", "ell", new Vector3(0f, -0.015f, 0.05f), new Vector3(0.115f, 0.085f, 0.29f), "bota", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("coxaD", "quadrilD", "cap", new Vector3(0f, -0.215f, 0f), new Vector3(0.15f, 0.5f, 0.155f), "calca", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("canelaD", "joelhoD", "cap", new Vector3(0f, -0.215f, 0f), new Vector3(0.115f, 0.46f, 0.12f), "calca", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("canoD", "tornozeloD", "fru", new Vector3(0f, 0.07f, 0f), new Vector3(0.125f, 0.16f, 0.13f), "bota", new Vector3(0f, 0f, 0f), 1.08f, ""),
        new Parte("peD", "tornozeloD", "ell", new Vector3(0f, -0.015f, 0.05f), new Vector3(0.115f, 0.085f, 0.29f), "bota", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("cajado", "punhoD", "cap", new Vector3(0f, -0.05f, 0.02f), new Vector3(0.032f, 1.9f, 0.032f), "madeira", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("cajadoTopo", "punhoD", "ell", new Vector3(0f, 0.9f, 0.02f), new Vector3(0.09f, 0.09f, 0.09f), "ouro", new Vector3(0f, 0f, 0f), 1f, ""),
        new Parte("cristal", "punhoD", "ell", new Vector3(0f, 0.99f, 0.02f), new Vector3(0.075f, 0.13f, 0.075f), "cristal", new Vector3(0f, 0f, 0f), 1f, "cristal"),
    };
}
