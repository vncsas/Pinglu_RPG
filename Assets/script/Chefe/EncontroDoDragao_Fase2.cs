using System;
using System.Collections;
using UnityEngine;

// FASE 2 DO DRAGÃO. Quando Drakthar chega à METADE DA VIDA ele para, desce ao chão e começa a cena:
//  1) o Ladrão surge ao lado dele e o provoca: "você dormiu tempo demais, já não é o que era"; o dragão berra "O QUÊ?!";
//  2) o Ladrão diz que treinou muito e aperfeiçoou os poderes... e vira uma fumaça negra com uma aura roxa muito forte;
//  3) a fumaça é sugada para dentro do dragão, que CRESCE, escurece e se cerca de uma aura sombria (fumaça, brasas, raios, anéis no chão);
//  4) clarão, rugido enorme, título "A Fúria Sombria" e a luta recomeça com ataques novos.
// F8 (só no editor): pula direto para essa cena, com o dragão na metade da vida, onde você estiver.
public partial class EncontroDoDragao
{
    // ------------------------------------------------------------ atalho F8 (só para testar)
    void IrDiretoFase2()
    {
        if (DialogoUI.Aberto || fase == Fase.Cena) return;
        if (modelo == null) { Debug.LogWarning("[Dragão] F8: rode o menu Pinglu > 84 primeiro (o modelo do dragão ainda não foi ligado)."); return; }
        Transform local = JogadorLocal(); if (local == null) return;
        StopAllCoroutines(); LimparTudo();
        TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
        GerenciadorDeOndas.HordaVencida = true;
        Campanha.CemiterioFeito = Campanha.CasteloFeito = Campanha.PantanoFeito = true;
        if (Campanha.Atual != Etapa.Dragao) Campanha.Ir(Etapa.Dragao);
        cinematicaFeita = true;                                          // a primeira cinemática já "aconteceu"
        Vector3 fwd = Camera.main != null ? Achatar(Camera.main.transform.forward) : Achatar(local.forward); if (fwd.sqrMagnitude < 0.05f) fwd = Achatar(local.forward); fwd.Normalize();
        Vector3 centro = NoChao(local.position + fwd * 42f); centroArena = centro;
        Vector3 pontoR = NoChao(local.position - fwd * (distanciaParaAcordar + 25f)); pontoR.y += 0.3f; Renascimento.DefinirParaLuta(pontoR, Quaternion.LookRotation(fwd));
        GameObject g = new GameObject("Drakthar"); g.transform.position = centro + Vector3.up * 24f; g.transform.rotation = Quaternion.LookRotation(-fwd);
        chefe = g.AddComponent<ChefeDragao>();
        chefe.modelo = modelo; chefe.animVoo = animVoo; chefe.animPlanar = animPlanar; chefe.animSopro = animSopro; chefe.materialParticula = materialParticula;
        chefe.Preparar(centro, raioArena, QuantosJogadores());
        chefe.PrepararParaFase2();
        chefe.AoFase2 += c => StartCoroutine(CinematicaFase2(c));
        chefe.AoMorrer += c => StartCoroutine(AposODragao(c));
        fase = Fase.Esperando;
        StartCoroutine(CinematicaFase2(chefe));
    }

    // ------------------------------------------------------------ efeitos da cena
    // um fiapo de fumaça negra que voa em curva até o dragão
    IEnumerator FumacaVoando(Vector3 de, Func<Vector3> para, float atraso, float dur, float tam)
    {
        if (atraso > 0f) yield return new WaitForSeconds(atraso);
        GameObject g = FxCena.Nuvem(de, tam, new Color(0.05f, 0.02f, 0.09f, 0f), FxCena.TexNuvem(), 5); restos.Add(g);
        Vector3 lado = UnityEngine.Random.onUnitSphere; float f0 = UnityEngine.Random.value * 6.28f;
        for (float t = 0f; t < dur && g != null; t += Time.deltaTime)
        {
            float k = t / dur, e = k * k * (3f - 2f * k); Vector3 p = Vector3.Lerp(de, para(), e);
            p += Vector3.up * Mathf.Sin(k * Mathf.PI) * 5f + lado * Mathf.Sin(k * Mathf.PI) * 4f + new Vector3(Mathf.Cos(f0 + k * 9f), Mathf.Sin(f0 + k * 7f), Mathf.Sin(f0 + k * 9f)) * (1f - k) * 1.6f;
            g.transform.position = p; g.transform.localScale = Vector3.one * Mathf.Lerp(tam, tam * 0.45f, k);
            FxCena.Pintar(g, new Color(0.09f, 0.02f, 0.14f, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.92f)); yield return null;
        }
        if (g != null) Destroy(g);
    }
    void ArcoRoxo(Vector3 c, float raio)
    {
        Vector3 a = c + UnityEngine.Random.insideUnitSphere * raio * 0.3f, b = c + UnityEngine.Random.onUnitSphere * raio; b.y = Mathf.Max(b.y, ChaoEm(b) + 0.5f);
        FxCena.Raio(a, b, UnityEngine.Random.Range(0.4f, 1.1f), 0.12f, new Color(0.78f, 0.4f, 1f, 1f)); if (ceu != null) ceu.Relampago(UnityEngine.Random.Range(0.3f, 0.7f));
    }

    // ------------------------------------------------------------ a cena
    IEnumerator CinematicaFase2(ChefeDragao d)
    {
        fase = Fase.Cena; menuAntes = SessaoDeJogo.MenuAberto;
        Transform local = JogadorLocal();
        if (d == null) { fase = Fase.Luta; yield break; }
        if (local == null) { d.IniciarFase2(); fase = Fase.Luta; yield break; }
        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar(); Proteger(true); FxCena.GarantirTela();
        if (Camera.main != null && farOriginal < 0f) farOriginal = Camera.main.farClipPlane;
        if (ceu == null) { GameObject cg = new GameObject("Ceu sombrio"); ceu = cg.AddComponent<FxCena.CeuSombrio>(); restos.Add(cg); }
        ceu.alvo = 0.55f; ceu.vel = 0.3f;
        d.AoMorrer += x => { if (ceu != null) { ceu.alvo = 0f; ceu.vel = 0.05f; } };
        AuraSombria aura = d.CriarAura(); aura.intensidade = 0f;
        GameObject ladrao = null;
        try
        {
            // ================= 1) o dragão desce e pousa; a câmera acompanha
            bool pousou = false; StartCoroutine(Rodar(d.IrAoChaoParaCena(), () => pousou = true));
            Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao; float tt = 0f;
            while (!pousou && tt < 12f)
            {
                tt += Time.deltaTime; Vector3 dp = d.transform.position, pj = local.position;
                Vector3 ate = Achatar(dp - pj); if (ate.sqrMagnitude < 4f) ate = Vector3.forward; ate.Normalize(); Vector3 lado = Vector3.Cross(Vector3.up, ate);
                Vector3 pos = Vector3.Lerp(pj, dp, 0.12f) - ate * 7f + lado * 4f + Vector3.up * 3.2f; float b = Mathf.SmoothStep(0f, 1f, tt / 1.4f);
                CameraDeCena.Definir(Vector3.Lerp(p0, pos, b), Quaternion.Slerp(r0, Quaternion.LookRotation(dp + Vector3.up * 3f - pos), b)); yield return null;
            }
            yield return new WaitForSeconds(0.4f);

            // ================= 2) o Ladrão aparece ao lado dele
            Vector3 dpos = d.transform.position; Vector3 paraJog = Achatar(local.position - dpos); if (paraJog.sqrMagnitude < 4f) paraJog = Vector3.back; paraJog.Normalize();
            Vector3 lat = Vector3.Cross(Vector3.up, paraJog);
            float dist = Mathf.Min(Achatar(local.position - dpos).magnitude * 0.55f, 24f);
            Vector3 pLad = NoChao(dpos + paraJog * Mathf.Max(14f, dist) + lat * 9f * d.Escala);
            Vector3 escala0 = Vector3.one;
            if (prefabLadrao != null)
            {
                ladrao = Instantiate(prefabLadrao, pLad, Quaternion.LookRotation(Achatar(dpos - pLad))); ladrao.name = "Ladrao (fusao)"; restos.Add(ladrao); escala0 = ladrao.transform.localScale;
                InimigoCavaleiro ic = ladrao.GetComponent<InimigoCavaleiro>(); if (ic != null) { ic.ModoCena(); ic.Fumaca(); ic.Intensificar(0.6f); }
            }
            TremorDeCamera.Tremer(0.3f, 1.0f);
            for (int i = 0; i < 6; i++) StartCoroutine(Poeira(pLad + UnityEngine.Random.insideUnitSphere * 1.6f + Vector3.up * 0.4f, 7f, new Color(0.1f, 0.05f, 0.14f, 0.85f), 2.0f, 2.5f));
            StartCoroutine(Anel(pLad, 14f, 1.3f, new Color(0.7f, 0.3f, 1f, 0.8f)));
            Vector3 cabecaLad = pLad + Vector3.up * 1.7f;
            Vector3 camLad = pLad + (paraJog * 6.5f) + lat * 2.4f + Vector3.up * 1.9f;
            yield return Voo(2.6f, k => Vector3.Lerp(camLad + paraJog * 2f, camLad, k), k => cabecaLad + Vector3.Lerp(Vector3.zero, (dpos - pLad) * 0.12f, k), true);
            DialogoUI.Abrir(Roteiro.FusaoComDragao, null);
            float o = 0f;
            while (DialogoUI.Aberto)
            {
                o += Time.deltaTime * 0.1f;
                Vector3 meio = Vector3.Lerp(pLad, dpos, 0.4f);
                Vector3 p = meio + Quaternion.Euler(0f, 90f + Mathf.Sin(o) * 20f, 0f) * paraJog * (13f + 4f * d.Escala) + Vector3.up * (3.5f + Mathf.Sin(o * 1.3f));
                CameraDeCena.Definir(p, Quaternion.LookRotation(meio + Vector3.up * 3f - p));
                if (ladrao != null) ladrao.transform.rotation = Quaternion.Slerp(ladrao.transform.rotation, Quaternion.LookRotation(Achatar(dpos - pLad)), Time.deltaTime * 3f);
                yield return null;
            }

            // ================= 3) o Ladrão vira fumaça negra e é sugado pelo dragão
            Func<Vector3> peitoDragao = () => d != null ? d.transform.position + Vector3.up * 3.2f * d.Escala : dpos;
            Vector3 camA = Vector3.Lerp(pLad, dpos, 0.5f) + paraJog * 24f + lat * 14f + Vector3.up * 7f;
            float cont = 0f; d.RugirCena(3.4f);
            yield return Voo(3.6f, k => Vector3.Lerp(camA + Vector3.up * 1f, camA - paraJog * 6f + Vector3.up * 3f, k), k => Vector3.Lerp(pLad + Vector3.up * 3f, Vector3.Lerp(pLad, dpos, 0.5f) + Vector3.up * 5f, k), true, k =>
            {
                // o Ladrão levita, gira e se desfaz em fumaça
                if (ladrao != null)
                {
                    float sobe = Mathf.SmoothStep(0f, 1f, k); ladrao.transform.position = pLad + Vector3.up * (sobe * 3f + Mathf.Sin(Time.time * 7f) * 0.1f);
                    ladrao.transform.Rotate(0f, 160f * k * Time.deltaTime, 0f, Space.World);
                    ladrao.transform.localScale = escala0 * Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f));
                }
                cont += Time.deltaTime * (10f + 60f * k);
                Vector3 corpo = (ladrao != null ? ladrao.transform.position : pLad) + Vector3.up * 1.2f;
                while (cont >= 1f) { cont -= 1f; StartCoroutine(FumacaVoando(corpo + UnityEngine.Random.insideUnitSphere * 0.9f, peitoDragao, 0f, UnityEngine.Random.Range(1.1f, 2.0f), UnityEngine.Random.Range(3f, 6.5f))); }
                aura.intensidade = Mathf.Lerp(0f, 0.7f, k); d.Tinta(k * 0.4f);
                TremorDeCamera.Tremer(0.08f + 0.18f * k, 0.25f);
                if (UnityEngine.Random.value < 0.1f + 0.2f * k) ArcoRoxo(corpo, 9f);
            });
            if (ladrao != null) ladrao.SetActive(false);

            // ================= 4) a fusão: o dragão cresce, escurece e a aura explode
            StartCoroutine(d.Crescer(4.4f, 1.5f));
            float aneis = 0f; Vector3 baseDragao = d.transform.position;
            Vector3 dirC = paraJog;
            yield return Voo(5.2f, k => baseDragao + dirC * Mathf.Lerp(30f, 58f, k) + lat * Mathf.Lerp(6f, 14f, k) + Vector3.up * Mathf.Lerp(3f, 9f, k), k => d.transform.position + Vector3.up * Mathf.Lerp(3.5f, 7f, k), true, k =>
            {
                cont += Time.deltaTime * Mathf.Lerp(50f, 10f, k);
                while (cont >= 1f && k < 0.45f) { cont -= 1f; StartCoroutine(FumacaVoando(pLad + Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 2f, peitoDragao, 0f, UnityEngine.Random.Range(0.8f, 1.4f), UnityEngine.Random.Range(3f, 6f))); }
                cont = Mathf.Min(cont, 1f);
                aura.intensidade = Mathf.Lerp(0.7f, 1.7f, k); d.Tinta(Mathf.Lerp(0.4f, 1f, k));
                if (ceu != null) ceu.alvo = Mathf.Lerp(0.55f, 0.8f, k);
                TremorDeCamera.Tremer(0.2f + 0.3f * k, 0.3f);
                aneis -= Time.deltaTime; if (aneis <= 0f) { aneis = 0.9f; StartCoroutine(Anel(d.transform.position, 70f, 1.8f, new Color(0.7f, 0.25f, 1f, 0.9f))); }
                if (UnityEngine.Random.value < 0.25f) ArcoRoxo(d.transform.position + Vector3.up * 5f, 18f * d.Escala);
                if (k > 0.82f) FxCena.Branco = Mathf.Clamp01((k - 0.82f) / 0.18f);          // o clarão final
            });
            // pico do clarão: a onda empurra o grupo e o mundo balança
            FxCena.Branco = 1f; d.Repelir(9f); TremorDeCamera.Tremer(0.7f, 1.6f);
            StartCoroutine(Anel(d.transform.position, 120f, 2.4f, new Color(0.8f, 0.4f, 1f, 1f))); StartCoroutine(Anel(d.transform.position, 60f, 1.6f, new Color(1f, 0.3f, 0.5f, 1f)));
            yield return new WaitForSeconds(0.25f);

            // ================= 5) o rugido: a câmera olha de baixo o dragão enorme
            d.RugirCena(4.2f); aura.intensidade = 1.8f; StartCoroutine(TituloNoRugidoSombrio());
            Vector3 posD = d.transform.position; Vector3 frenteD = Achatar(d.transform.forward).normalized;
            Vector3 ladoD = Vector3.Cross(Vector3.up, frenteD);
            yield return Voo(5.2f, k => NoChao(posD + frenteD * Mathf.Lerp(44f, 58f, k) + ladoD * 10f) + Vector3.up * Mathf.Lerp(2.2f, 5f, k), k => d.transform.position + Vector3.up * Mathf.Lerp(8f, 12f, k), false, k =>
            {
                FxCena.Branco = Mathf.Max(0f, 1f - k * 3.5f);
                TremorDeCamera.Tremer(0.25f, 0.3f); aura.intensidade = Mathf.Lerp(1.8f, 1.1f, k);
                if (UnityEngine.Random.value < 0.2f) ArcoRoxo(d.transform.position + Vector3.up * 6f, 24f);
                if (ceu != null) ceu.alvo = 0.7f;
            });
        }
        finally
        {
            FxCena.Branco = 0f; TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menuAntes; Proteger(false);
            if (Camera.main != null && farOriginal > 0f) { Camera.main.farClipPlane = farOriginal; }
            if (ladrao != null) Destroy(ladrao);
        }
        if (d == null) { fase = Fase.Luta; yield break; }
        Campanha.Dica = "Drakthar absorveu o Ladrão! Quando ele concentrar poder, ataque para interromper e derrubá-lo.";
        fase = Fase.Luta; d.IniciarFase2();
    }

    IEnumerator TituloNoRugidoSombrio()
    {
        yield return new WaitForSeconds(0.8f);
        TituloDeChefe.Mostrar(ApresentacaoDoChefe.DragaoSombrio());
    }
}
