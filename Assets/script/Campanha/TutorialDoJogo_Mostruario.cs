using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A PRÉVIA ANIMADA do tutorial: um pequeno palco escondido bem alto no céu (fora do mapa), com uma câmera que desenha numa imagem.
// Nele nasce uma cópia do personagem escolhido (o mesmo prefab do jogo) e alguns inimigos de treino parados; a cada ciclo o personagem usa
// a habilidade de verdade (HabilidadesDoPersonagem.Demonstrar), então a animação, os efeitos e o alcance são exatamente os do jogo.
public partial class TutorialDoJogo
{
    static readonly Vector3 BASE = new Vector3(2600f, 8000f, 2600f);
    Camera camD; RenderTexture rtD; GameObject palcoD; PlayerLocomotion ator; HabilidadesDoPersonagem habAtor; Vida vidaAtor;
    readonly List<GameObject> alvosD = new List<GameObject>();
    Coroutine cicloD; bool mostruarioOk; Vector3 offCam, olharPara; float inicioPagina;

    static Material MatD(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); if (m.HasProperty("_Color")) m.SetColor("_Color", c); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
        return m;
    }

    void LuzD(string nome, LightType tipo, Vector3 pos, Color cor, float intensidade, float alcance)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(palcoD.transform, false); g.transform.localPosition = pos;
        Light l = g.AddComponent<Light>(); l.type = tipo; l.color = cor; l.intensity = intensidade; l.range = alcance; l.shadows = LightShadows.None;
    }

    void MostruarioIniciar()
    {
        mostruarioOk = false;
        if (hab == null || ficha == null) return;
        try
        {
            palcoD = new GameObject("Mostruário de habilidades"); palcoD.transform.position = BASE;
            GameObject chao = GameObject.CreatePrimitive(PrimitiveType.Cube); chao.name = "Chão"; chao.transform.SetParent(palcoD.transform, false);
            chao.transform.localPosition = new Vector3(0f, -0.5f, 0f); chao.transform.localScale = new Vector3(70f, 1f, 70f); chao.GetComponent<MeshRenderer>().sharedMaterial = MatD(new Color(0.2f, 0.18f, 0.16f));
            // ruínas em volta: pilares quebrados e tochas
            Material mp = MatD(new Color(0.3f, 0.28f, 0.26f));
            for (int i = 0; i < 10; i++)
            {
                float a = (i * 36f + 8f) * Mathf.Deg2Rad, r = 15f + (i % 3) * 3f, h = 2.5f + (i * 37 % 5);
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder); p.name = "Pilar"; p.transform.SetParent(palcoD.transform, false);
                p.transform.localPosition = new Vector3(Mathf.Sin(a) * r, h, 6f + Mathf.Cos(a) * r); p.transform.localScale = new Vector3(1.4f, h, 1.4f);
                Destroy(p.GetComponent<Collider>()); p.GetComponent<MeshRenderer>().sharedMaterial = mp;
                if (i % 3 == 0) LuzD("Tocha", LightType.Point, new Vector3(Mathf.Sin(a) * (r - 2f), 2.6f, 6f + Mathf.Cos(a) * (r - 2f)), new Color(1f, 0.55f, 0.2f), 3f, 14f);
            }
            LuzD("Luz principal", LightType.Point, new Vector3(-4f, 5.5f, -4.5f), new Color(1f, 0.9f, 0.78f), 9f, 24f);
            LuzD("Contraluz", LightType.Point, new Vector3(5f, 4.5f, 9f), new Color(0.5f, 0.62f, 1f), 6f, 22f);
            // câmera que desenha na imagem
            GameObject gc = new GameObject("Câmera do mostruário"); gc.transform.SetParent(palcoD.transform, false);
            camD = gc.AddComponent<Camera>(); camD.clearFlags = CameraClearFlags.SolidColor; camD.backgroundColor = new Color(0.045f, 0.05f, 0.07f);
            camD.fieldOfView = 42f; camD.nearClipPlane = 0.2f; camD.farClipPlane = 140f; camD.depth = -30f; camD.allowHDR = true;
            rtD = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { name = "Prévia da habilidade" }; camD.targetTexture = rtD;
            camD.GetUniversalAdditionalCameraData().renderPostProcessing = true; camD.enabled = false;
            // o personagem de mostruário (sem cérebro de companheiro: só faz o que mandarmos)
            ator = GrupoDeAventureiros.Criar(ficha, BASE + Vector3.up * 0.2f, Quaternion.identity, false);
            if (ator == null) return;
            CerebroCompanheiro cb = ator.GetComponent<CerebroCompanheiro>(); if (cb != null) DestroyImmediate(cb);
            ator.name = "Personagem de mostruário"; habAtor = ator.GetComponent<HabilidadesDoPersonagem>(); vidaAtor = ator.GetComponent<Vida>();
            mostruarioOk = habAtor != null;
        }
        catch (System.Exception ex) { Debug.LogWarning("[Tutorial] a prévia das habilidades não pôde ser montada: " + ex.Message); mostruarioOk = false; }
    }

    // inimigos de treino parados, no lugar certo para cada habilidade
    Vector3[] LugaresDosAlvos(int idx)
    {
        ClasseDeHabilidade c = hab.classe;
        if (c == ClasseDeHabilidade.Cavaleiro && idx == 0) return new[] { new Vector3(0f, 0f, 3.1f), new Vector3(-1.1f, 0f, 4.5f) };
        if (c == ClasseDeHabilidade.Cavaleiro && idx == 1) return new[] { new Vector3(0f, 0f, 1.9f) };
        if (c == ClasseDeHabilidade.Maga && idx == 1) return new[] { new Vector3(0f, 0f, 4.5f), new Vector3(0f, 0f, 7.5f) };
        if (c == ClasseDeHabilidade.Curandeiro) return new[] { new Vector3(0f, 0f, 6f) };
        return new[] { new Vector3(0f, 0f, 3.4f), new Vector3(2.7f, 0f, 2.6f), new Vector3(-2.7f, 0f, 2.6f) };
    }

    void LimparAlvos() { foreach (GameObject g in alvosD) if (g != null) Destroy(g); alvosD.Clear(); }

    void CriarAlvos(int idx)
    {
        LimparAlvos();
        GerenciadorDeOndas go = GerenciadorDeOndas.Instancia; GameObject molde = null;
        if (go != null) { GameObject[] ps = go.PrefabsDeMonstros; foreach (GameObject p in ps) if (p != null && p.name.ToLowerInvariant().Contains("esqueleto")) { molde = p; break; } if (molde == null && ps.Length > 0) molde = ps[0]; }
        foreach (Vector3 lugar in LugaresDosAlvos(idx))
        {
            Vector3 pos = BASE + lugar + Vector3.up * 0.05f; GameObject g;
            if (molde != null)
            {
                g = Instantiate(molde, pos, Quaternion.LookRotation(Vector3.back));
                foreach (MonoBehaviour mb in g.GetComponents<MonoBehaviour>()) if (mb != null && !(mb is Vida)) mb.enabled = false;      // sem cérebro: fica parado (desligado, não destruído: a Vida ainda chama os eventos dele)
            }
            else { g = GameObject.CreatePrimitive(PrimitiveType.Capsule); g.transform.position = pos + Vector3.up; g.GetComponent<MeshRenderer>().sharedMaterial = MatD(new Color(0.7f, 0.68f, 0.6f)); }
            Vida v = g.GetComponent<Vida>(); if (v == null) v = g.AddComponent<Vida>(); v.DefinirMaxima(100000f);
            alvosD.Add(g);
        }
    }

    void MostruarioPagina(int p)
    {
        if (!mostruarioOk) return;
        if (cicloD != null) StopCoroutine(cicloD); cicloD = null;
        habAtor.enabled = false; habAtor.enabled = true;              // aborta uma habilidade em andamento
        camD.enabled = p > 0; LimparAlvos(); inicioPagina = Time.unscaledTime;
        if (p <= 0) return;
        int idx = p - 1; DadosDeHabilidade d = hab.Dados(idx);
        bool largo = d != null && (d.raio >= 5f || hab.classe == ClasseDeHabilidade.Tanque || (hab.classe == ClasseDeHabilidade.Maga && idx != 1));
        offCam = largo ? new Vector3(6.8f, 3.8f, -8.2f) : new Vector3(4.4f, 2.3f, -4.8f);
        olharPara = new Vector3(0f, 1.25f, largo ? 3.4f : 2.9f); camD.fieldOfView = largo ? 50f : 42f;
        cicloD = StartCoroutine(CicloDemo(idx));
    }

    IEnumerator CicloDemo(int idx)
    {
        DadosDeHabilidade d = hab.Dados(idx);
        while (true)
        {
            CriarAlvos(idx);
            ator.GetComponent<CharacterController>().enabled = false; ator.transform.SetPositionAndRotation(BASE + Vector3.up * 0.2f, Quaternion.identity); ator.GetComponent<CharacterController>().enabled = true;
            if (hab.classe == ClasseDeHabilidade.Curandeiro && vidaAtor != null) vidaAtor.DefinirAtual(vidaAtor.Maxima * 0.35f);     // para a cura aparecer
            yield return new WaitForSecondsRealtime(1.2f);
            habAtor.Demonstrar(idx);
            float t0 = Time.unscaledTime; yield return new WaitForSecondsRealtime(0.3f);
            while (habAtor.Usando && Time.unscaledTime - t0 < 9f) yield return null;
            yield return new WaitForSecondsRealtime(Mathf.Clamp(d != null ? d.duracaoEfeito * 0.5f : 1.5f, 1.4f, 3.2f));
        }
    }

    void MostruarioAtualizar()
    {
        if (!mostruarioOk || camD == null || !camD.enabled) return;
        float t = Time.unscaledTime - inicioPagina;
        Vector3 off = Quaternion.Euler(0f, Mathf.Sin(t * 0.35f) * 9f, 0f) * offCam;                    // a câmera balança devagar
        camD.transform.position = BASE + off; camD.transform.rotation = Quaternion.LookRotation((BASE + olharPara) - camD.transform.position);
    }

    void MostruarioDesenhar(Rect r, float al)
    {
        if (mostruarioOk && rtD != null && camD != null && camD.enabled)
        {
            GUI.color = new Color(1f, 1f, 1f, al); GUI.DrawTexture(r, rtD, ScaleMode.StretchToFill, false); GUI.color = Color.white;
            R(new Rect(r.x, r.yMax - r.height * 0.14f, r.width, r.height * 0.14f), new Color(0f, 0f, 0f, 0.3f * al));
        }
        else
        {
            R(r, new Color(0.05f, 0.05f, 0.07f, al));
            Texto(r, "(prévia indisponível)", fFell, 28f * (Screen.height / 1080f), TextAnchor.MiddleCenter, new Color(0.7f, 0.66f, 0.58f, al));
        }
    }

    void MostruarioEncerrar()
    {
        if (cicloD != null) StopCoroutine(cicloD); cicloD = null;
        LimparAlvos();
        if (ator != null) Destroy(ator.gameObject); ator = null;
        if (palcoD != null) Destroy(palcoD); palcoD = null;
        if (rtD != null) { rtD.Release(); Destroy(rtD); rtD = null; }
        mostruarioOk = false;
    }
}
