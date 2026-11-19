using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// CINEMÁTICA DO FIM: o último cristal é pego e o Ladrão aparece. Roteiro:
//  1) o Ladrão surge numa nuvem de fumaça e revela o plano (quem é, por que faz isso). Os heróis ironizam: "temos dois cristais";
//  2) ele gargalha, tira o cristal que roubou do cemitério, diz que "poder nas mãos de quem não sabe usá-lo é inútil" e o lança ao céu;
//  3) o cristal sobe, o céu se enche de nuvens negras e relâmpagos; ele chama os outros dois, que saem das mãos do grupo;
//  4) os três giram cada vez mais rápido, vem uma luz fortíssima e tudo fica branco;
//  5) quando a visão volta, estilhaços cinzentos e apagados chovem do céu; o Ladrão gargalha: "este mundo acabou!";
//  6) a câmera vai à montanha do fundo do mapa: ela racha, o cume desaba e Drakthar sai de dentro, ruge e voa até o grupo.
public partial class EncontroDoDragao
{
    bool montanhaQuebrada;
    FxCena.Montanha montanha; FxCena.CeuSombrio ceu; FxCena.Tempestade tempestade;
    readonly List<GameObject> restos = new List<GameObject>();
    float farOriginal = -1f;

    // ------------------------------------------------------------ limpeza e volta ao normal
    void LimparTudo()
    {
        StopAllCoroutines();
        if (chefe != null) Destroy(chefe.gameObject);
        chefe = null; fase = Fase.Esperando; cinematicaFeita = false; desdeDragao = -1f;
        TituloDeChefe.Barras(false); CameraDeCena.Soltar();
        RestaurarMundo();
        foreach (GameObject g in restos) if (g != null) Destroy(g);
        restos.Clear();
        if (ceu != null) { Destroy(ceu); ceu = null; }
        if (tempestade != null) { Destroy(tempestade.gameObject); tempestade = null; }
        FxCena.Branco = 0f;
    }

    // devolve o terreno e as árvores da montanha (o terreno é um arquivo do projeto: não pode ficar alterado)
    void RestaurarMundo()
    {
        if (montanha != null) { montanha.Restaurar(); montanha = null; }
        montanhaQuebrada = false;
        Camera c = Camera.main; if (c != null && farOriginal > 0f) { c.farClipPlane = farOriginal; farOriginal = -1f; }
    }

    // ------------------------------------------------------------ auxiliares de câmera e cena
    Vector3 NoChao(Vector3 p) { p.y = ChaoEm(p); return p; }
    static Vector3 Achatar(Vector3 v) { v.y = 0f; return v; }
    void GarantirFar(float far) { Camera c = Camera.main; if (c == null) return; if (farOriginal < 0f) farOriginal = c.farClipPlane; if (c.farClipPlane < far) c.farClipPlane = far; }

    // câmera: calcula posição e alvo a cada instante; "suave" mistura com a pose anterior nos primeiros instantes (senão, corte seco)
    IEnumerator Voo(float dur, Func<float, Vector3> pos, Func<float, Vector3> alvo, bool suave = true, Action<float> aCada = null)
    {
        Vector3 p0 = CameraDeCena.Posicao; Quaternion r0 = CameraDeCena.Rotacao;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = dur > 0f ? t / dur : 1f; Vector3 p = pos(k), a = alvo(k);
            Quaternion r = Quaternion.LookRotation(a - p);
            if (suave) { float b = Mathf.SmoothStep(0f, 1f, t / 1.0f); p = Vector3.Lerp(p0, p, b); r = Quaternion.Slerp(r0, r, b); }
            CameraDeCena.Definir(p, r);
            if (farOriginal > 0f) GarantirFar(farOriginal + 1000f);
            if (aCada != null) aCada(k);
            yield return null;
        }
    }

    // pedra que voa e rola (gravidade simples)
    IEnumerator Pedra(Vector3 pos, Vector3 vel, float tam)
    {
        GameObject g = FxCena.NovoEstilhaco(pos, tam, -10000f, Vector3.zero); Destroy(g.GetComponent<FxCena.Estilhaco>()); restos.Add(g);
        float quica = 0f;
        for (float t = 0f; t < 9f && g != null; t += Time.deltaTime)
        {
            vel.y -= 26f * Time.deltaTime; g.transform.position += vel * Time.deltaTime; g.transform.Rotate(vel.magnitude * 12f * Time.deltaTime, 40f * Time.deltaTime, 0f);
            float chao = ChaoEm(g.transform.position) + tam * 0.3f;
            if (g.transform.position.y < chao) { Vector3 p = g.transform.position; p.y = chao; g.transform.position = p; if (vel.y < -4f && quica < 2f) { vel.y = -vel.y * 0.35f; vel.x *= 0.6f; vel.z *= 0.6f; quica++; } else vel = Vector3.zero; }
            yield return null;
        }
    }
    IEnumerator Poeira(Vector3 pos, float tam, Color cor, float dur, float sobe = 6f)
    {
        GameObject g = FxCena.Nuvem(pos, tam * 0.4f, cor, FxCena.TexNuvem(), 3); Vector3 v = UnityEngine.Random.insideUnitSphere * 3f + Vector3.up * sobe;
        for (float t = 0f; t < dur && g != null; t += Time.deltaTime)
        {
            float k = t / dur; g.transform.position += v * Time.deltaTime; g.transform.localScale = Vector3.one * Mathf.Lerp(tam * 0.4f, tam, Mathf.Sqrt(k));
            FxCena.Pintar(g, new Color(cor.r, cor.g, cor.b, cor.a * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.9f)); yield return null;
        }
        if (g != null) Destroy(g);
    }
    IEnumerator Anel(Vector3 c, float r1, float dur, Color cor, bool vertical = false)
    {
        GameObject g = new GameObject("Onda"); LineRenderer lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = FxCena.Sprite(); lr.useWorldSpace = true; lr.positionCount = 64; lr.loop = true;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur, r = Mathf.Lerp(1f, r1, 1f - (1f - k) * (1f - k));
            for (int i = 0; i < 64; i++) { float a = i / 64f * Mathf.PI * 2f; Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); if (!vertical) p.y = ChaoEm(p) + 0.6f; lr.SetPosition(i, p); }
            lr.startWidth = lr.endWidth = Mathf.Lerp(3f, 0.5f, k) * (r1 > 60f ? 3f : 1f); lr.startColor = lr.endColor = new Color(cor.r, cor.g, cor.b, cor.a * (1f - k)); yield return null;
        }
        Destroy(g);
    }
    IEnumerator Gargalhar(Transform t, float dur)
    {
        Quaternion r0 = t.rotation; Vector3 p0 = t.position;
        for (float u = 0f; u < dur && t != null; u += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.Clamp01(u / dur) * Mathf.PI);
            t.rotation = r0 * Quaternion.Euler(-14f * k + Mathf.Sin(u * 22f) * 6f * k, 0f, Mathf.Sin(u * 11f) * 4f * k); yield return null;
        }
        if (t != null) t.rotation = r0;
    }
    void RaioNoCeu(Vector3 perto, float raioMin, float raioMax)
    {
        Vector2 d = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(raioMin, raioMax);
        Vector3 alvo = NoChao(perto + new Vector3(d.x, 0f, d.y)); FxCena.Raio(alvo + Vector3.up * 190f + new Vector3(UnityEngine.Random.Range(-30f, 30f), 0f, UnityEngine.Random.Range(-30f, 30f)), alvo, UnityEngine.Random.Range(1.5f, 3.2f));
        if (ceu != null) ceu.Relampago(UnityEngine.Random.Range(0.6f, 1f)); TremorDeCamera.Tremer(0.12f, 0.35f);
    }

    // ------------------------------------------------------------ a cinemática
    IEnumerator CinematicaDoVilao(Transform local)
    {
        fase = Fase.Cena; cinematicaFeita = true; menuAntes = SessaoDeJogo.MenuAberto;
        Vector3 ancora = NoChao(local.position); centroArena = ancora;
        Vector3 fwd = Camera.main != null ? Achatar(Camera.main.transform.forward) : Achatar(local.forward); if (fwd.sqrMagnitude < 0.05f) fwd = Achatar(local.forward); fwd.Normalize();
        Vector3 dir = Vector3.Cross(Vector3.up, fwd);
        // se morrer na luta, renasce fora da distância que chama o dragão de volta
        Vector3 pontoR = ancora - fwd * (distanciaParaAcordar + 25f); pontoR.y = ChaoEm(pontoR) + 0.3f; Renascimento.DefinirParaLuta(pontoR, Quaternion.LookRotation(fwd));
        // a montanha: o ponto mais alto do mapa (ou o que foi definido no menu)
        Vector3 pico = posicaoMontanha; pico.y = ChaoEm(pico);
        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar(); Proteger(true); FxCena.GarantirTela();
        if (Camera.main != null) farOriginal = Camera.main.farClipPlane;
        GameObject ceuGo = new GameObject("Ceu de tempestade"); ceu = ceuGo.AddComponent<FxCena.CeuSombrio>(); restos.Add(ceuGo);
        GameObject ladrao = null; GameObject cristalV = null; GameObject c1 = null, c2 = null;
        try
        {
            // ================= 1) o Ladrão aparece
            Vector3 pLad = NoChao(ancora + fwd * 7.5f);
            if (prefabLadrao != null)
            {
                ladrao = Instantiate(prefabLadrao, pLad, Quaternion.LookRotation(-fwd)); ladrao.name = "Ladrao (cena)"; restos.Add(ladrao);
                InimigoCavaleiro ic = ladrao.GetComponent<InimigoCavaleiro>(); if (ic != null) { ic.ModoCena(); ic.Fumaca(); ic.Intensificar(0.6f); }
            }
            local.rotation = Quaternion.LookRotation(Achatar(pLad - local.position));
            TremorDeCamera.Tremer(0.35f, 1.2f);
            for (int i = 0; i < 7; i++) StartCoroutine(Poeira(pLad + UnityEngine.Random.insideUnitSphere * 1.6f + Vector3.up * 0.4f, 7f, new Color(0.1f, 0.05f, 0.14f, 0.85f), 2.2f, 2.5f));
            StartCoroutine(Anel(pLad, 14f, 1.4f, new Color(0.7f, 0.3f, 1f, 0.8f)));
            Vector3 cabecaLad = pLad + Vector3.up * 1.8f;
            yield return Voo(3.2f, k => Vector3.Lerp(ancora - fwd * 2.5f + dir * 2.2f + Vector3.up * 2.1f, ancora + fwd * 1.5f + dir * 3.3f + Vector3.up * 1.7f, k), k => Vector3.Lerp(pLad + Vector3.up * 1.5f, cabecaLad, k), true);
            DialogoUI.Abrir(Roteiro.VilaoRevela, null);
            float o = 0f;
            while (DialogoUI.Aberto)
            {
                o += Time.deltaTime * 0.12f;
                Vector3 p = Vector3.Lerp(ancora, pLad, 0.5f) + Quaternion.Euler(0f, 90f + Mathf.Sin(o) * 18f, 0f) * fwd * 5.6f + Vector3.up * 1.9f;
                CameraDeCena.Definir(p, Quaternion.LookRotation(Vector3.Lerp(ancora, pLad, 0.5f) + Vector3.up * 1.6f - p)); yield return null;
            }

            // ================= 2) ele tira o cristal e o lança ao céu
            Vector3 mao = pLad + Vector3.up * 1.7f - fwd * 0.55f;
            cristalV = FxCena.Cristal(new Color(0.75f, 0.35f, 1f), 0.55f, materialParticula); restos.Add(cristalV); cristalV.transform.position = mao; FxCena.Escala(cristalV, 0.01f);
            Vector3 camA = pLad + Vector3.up * 1.7f + fwd * 3.6f + dir * 0.8f;
            yield return Voo(2.0f, k => camA, k => mao, false, k => { FxCena.Escala(cristalV, Mathf.SmoothStep(0f, 1f, k)); FxCena.Luz(cristalV, 3f + 6f * k, 14f); cristalV.transform.position = mao + Vector3.up * Mathf.Sin(k * 3f) * 0.05f; if (ladrao != null) ladrao.transform.rotation = Quaternion.LookRotation(Achatar(local.position - ladrao.transform.position)); });
            StartCoroutine(Gargalhar(ladrao != null ? ladrao.transform : null, 1.8f));
            yield return new WaitForSeconds(1.0f);
            // lançado: sobe girando em espiral até 150 m
            Vector3 alto = ancora + Vector3.up * 155f; ceu.alvo = 1f; ceu.vel = 0.18f;
            tempestade = FxCena.CriarTempestade(alto + Vector3.up * 5f, 80); restos.Add(tempestade.gameObject); tempestade.intensidade = 0f;
            float subida = 5.5f; float proxRaio = 3f, proxAnel = 0f;
            Vector3 camBase = ancora - fwd * 12f + dir * 4f; camBase = NoChao(camBase) + Vector3.up * 1.4f;
            yield return Voo(subida, k => camBase, k => Vector3.Lerp(mao, alto, Mathf.Pow(k, 0.8f)), true, k =>
            {
                float e = k * k * (3f - 2f * k); Vector3 p = Vector3.Lerp(mao, alto, e); p += new Vector3(Mathf.Cos(k * 14f), 0f, Mathf.Sin(k * 14f)) * (1f - k) * 3f; cristalV.transform.position = p;
                FxCena.Escala(cristalV, 1f + 3.5f * k); FxCena.Luz(cristalV, 9f + 30f * k, 40f + 140f * k);
                tempestade.intensidade = Mathf.Clamp01(k * 1.3f); TremorDeCamera.Tremer(0.05f + 0.16f * k, 0.3f);
                proxAnel -= Time.deltaTime; if (proxAnel < 0f && k > 0.45f) { proxAnel = 0.7f; StartCoroutine(Anel(p, 120f, 2.2f, new Color(0.8f, 0.45f, 1f, 0.8f), true)); }
                proxRaio -= Time.deltaTime; if (proxRaio < 0f && k > 0.35f) { proxRaio = UnityEngine.Random.Range(0.5f, 1.2f); RaioNoCeu(ancora, 60f, 170f); }
            });
            // o cristal "pulsa" lá em cima, chamando os outros
            yield return Voo(2.2f, k => camBase + Vector3.up * 2f * k, k => alto + Vector3.down * 20f * (1f - k), true, k =>
            {
                float pulso = 1f + 0.35f * Mathf.Sin(k * 40f); FxCena.Escala(cristalV, 4.5f * pulso); FxCena.Luz(cristalV, 40f, 190f); TremorDeCamera.Tremer(0.2f, 0.3f);
                if (UnityEngine.Random.value < 0.07f) RaioNoCeu(ancora, 50f, 150f);
                if (k > 0.2f && UnityEngine.Random.value < 0.04f) StartCoroutine(Anel(alto, 160f, 1.6f, new Color(0.8f, 0.45f, 1f, 0.9f), true));
            });

            // ================= 3) os dois cristais do grupo sobem
            Vector3 pe1 = ancora + Vector3.up * 1.4f + dir * 0.6f, pe2 = ancora + Vector3.up * 1.4f - dir * 0.6f;
            c1 = FxCena.Cristal(new Color(0.35f, 0.65f, 1f), 0.5f, materialParticula); c2 = FxCena.Cristal(new Color(0.3f, 1f, 0.6f), 0.5f, materialParticula); restos.Add(c1); restos.Add(c2);
            c1.transform.position = pe1; c2.transform.position = pe2; FxCena.Escala(c1, 0.1f); FxCena.Escala(c2, 0.1f);
            Vector3 camLonge = ancora - fwd * 14f + Vector3.up * 2f;
            float chamado = 4.6f;
            yield return Voo(chamado, k => camLonge + Vector3.up * 3f * k, k => Vector3.Lerp(ancora + Vector3.up * 10f, alto, Mathf.SmoothStep(0f, 1f, k)), true, k =>
            {
                float e = k * k * (3f - 2f * k), cs = Mathf.Min(1f, k * 5f);
                FxCena.Escala(c1, Mathf.Lerp(0.3f, 3.2f, cs)); FxCena.Escala(c2, Mathf.Lerp(0.3f, 3.2f, cs)); FxCena.Luz(c1, 20f, 90f); FxCena.Luz(c2, 20f, 90f);
                Vector3 q1 = Vector3.Lerp(pe1, alto + new Vector3(6f, 0f, 0f), e) + dir * Mathf.Sin(k * Mathf.PI) * 14f; Vector3 q2 = Vector3.Lerp(pe2, alto + new Vector3(-6f, 0f, 0f), e) - dir * Mathf.Sin(k * Mathf.PI) * 14f;
                c1.transform.position = q1; c2.transform.position = q2; TremorDeCamera.Tremer(0.18f, 0.3f);
                if (UnityEngine.Random.value < 0.08f) RaioNoCeu(ancora, 40f, 140f);
            });
            // ================= 4) giram cada vez mais rápido... e tudo fica branco
            float giro = 5.8f; float ang = 0f;
            Vector3 camOrbita = ancora - fwd * 10f + Vector3.up * 1.3f;
            yield return Voo(giro, k => camOrbita, k => alto, true, k =>
            {
                float raio = Mathf.Lerp(12f, 2.2f, k * k), vel = Mathf.Lerp(120f, 1300f, k * k); ang += vel * Time.deltaTime;
                cristalV.transform.position = alto + Vector3.up * Mathf.Sin(ang * 0.01f) * 0.5f;
                c1.transform.position = alto + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad)) * raio;
                c2.transform.position = alto + new Vector3(Mathf.Cos((ang + 180f) * Mathf.Deg2Rad), 0f, Mathf.Sin((ang + 180f) * Mathf.Deg2Rad)) * raio;
                float f = 1f + 5f * k * k; FxCena.Luz(cristalV, 40f + 90f * k, 200f + 400f * k); FxCena.Luz(c1, 30f + 70f * k, 150f + 300f * k); FxCena.Luz(c2, 30f + 70f * k, 150f + 300f * k);
                FxCena.Escala(cristalV, 4.5f * f * 0.5f);
                tempestade.intensidade = 1f; if (ceu != null) { ceu.alvo = 1f; }
                TremorDeCamera.Tremer(0.2f + 0.5f * k, 0.3f);
                if (UnityEngine.Random.value < 0.15f + 0.3f * k) RaioNoCeu(ancora, 30f, 120f);
                FxCena.Branco = Mathf.Clamp01((k - 0.62f) / 0.38f); FxCena.Branco *= FxCena.Branco;
            });
            FxCena.Branco = 1f; TremorDeCamera.Tremer(0.7f, 2.4f);
            // tudo branco: aproveita para trocar o mundo
            if (cristalV != null) Destroy(cristalV); if (c1 != null) Destroy(c1); if (c2 != null) Destroy(c2);
            ceu.alvo = 0.9f; ceu.vel = 5f; tempestade.intensidade = 0.8f;
            yield return new WaitForSeconds(1.4f);

            // ================= 5) estilhaços cinzentos caem do céu
            Vector3 camChuva = NoChao(ancora - fwd * 5f + dir * 3f) + Vector3.up * 1.2f;
            StartCoroutine(ChuvaDeEstilhacos(ancora, 150, 6f));
            float sai = 0f;
            yield return Voo(7.0f, k => camChuva + Vector3.up * 0.4f * k, k => ancora + Vector3.up * Mathf.Lerp(46f, 22f, k) + fwd * 3f, false, k => { sai += Time.deltaTime; FxCena.Branco = Mathf.Clamp01(1f - sai / 2.4f); TremorDeCamera.Tremer(0.05f, 0.3f); if (UnityEngine.Random.value < 0.04f) RaioNoCeu(ancora, 60f, 160f); });
            FxCena.Branco = 0f;
            // o Ladrão gargalha: plano baixo, de baixo para cima
            Vector3 cabecaL = (ladrao != null ? ladrao.transform.position : pLad) + Vector3.up * 1.8f; Vector3 frenteL = ladrao != null ? ladrao.transform.forward : -fwd;
            StartCoroutine(Gargalhar(ladrao != null ? ladrao.transform : null, 6f));
            DialogoUI.Abrir(Roteiro.VilaoGargalha, null);
            o = 0f;
            while (DialogoUI.Aberto)
            {
                o += Time.deltaTime;
                Vector3 p = cabecaL + frenteL * Mathf.Lerp(3.6f, 2.6f, Mathf.Clamp01(o / 14f)) - Vector3.up * 1.1f + Vector3.Cross(Vector3.up, frenteL) * 0.6f;
                p = NoChao(p) + Vector3.up * Mathf.Max(0.35f, p.y - ChaoEm(p));
                CameraDeCena.Definir(p, Quaternion.LookRotation(cabecaL + Vector3.up * 0.1f - p));
                TremorDeCamera.Tremer(0.07f + Mathf.Clamp01(o / 12f) * 0.18f, 0.3f); yield return null;
            }

            // ================= 6) a montanha racha e o dragão sai de dentro dela
            if (ladrao != null) { ladrao.GetComponent<InimigoCavaleiro>()?.Sumir(); ladrao = null; }
            montanha = new FxCena.Montanha(pico, 85f); montanhaQuebrada = true;
            Vector3 paraJog = Achatar(ancora - pico); if (paraJog.sqrMagnitude < 4f) paraJog = Vector3.right; paraJog.Normalize();
            ceu.limiteNeblina = 0.0014f; GarantirFar(1600f);
            Vector3 camM0 = pico + paraJog * 360f + Vector3.up * 105f, camM1 = pico + paraJog * 250f + Vector3.up * 80f;
            camM0.y = Mathf.Max(camM0.y, ChaoEm(camM0) + 40f); camM1.y = Mathf.Max(camM1.y, ChaoEm(camM1) + 35f);
            // prepara o dragão, escondido sob o cume
            GameObject dg = new GameObject("Drakthar"); dg.transform.position = pico + Vector3.down * 70f;
            chefe = dg.AddComponent<ChefeDragao>(); chefe.modelo = modelo; chefe.animVoo = animVoo; chefe.animPlanar = animPlanar; chefe.animSopro = animSopro; chefe.materialParticula = materialParticula;
            chefe.Preparar(centroArena, raioArena, QuantosJogadores());
            // rachaduras brilhantes
            var rachas = new List<LineRenderer>(); var rachaPts = new List<Vector3[]>();
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f), comp = UnityEngine.Random.Range(45f, 100f); int n = 26; var pts = new Vector3[n]; Vector3 p = pico; float ang2 = a;
                for (int j = 0; j < n; j++) { pts[j] = new Vector3(p.x, ChaoEm(p) + 0.8f, p.z); ang2 += UnityEngine.Random.Range(-0.35f, 0.35f); p += new Vector3(Mathf.Cos(ang2), 0f, Mathf.Sin(ang2)) * (comp / n); }
                GameObject g = new GameObject("Rachadura"); restos.Add(g); LineRenderer lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = FxCena.Sprite(); lr.useWorldSpace = true; lr.positionCount = 2; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.startWidth = 2.2f; lr.endWidth = 0.4f; lr.startColor = new Color(1f, 0.75f, 0.2f); lr.endColor = new Color(1f, 0.25f, 0.05f, 0.8f); rachas.Add(lr); rachaPts.Add(pts);
            }
            GameObject luzPico = new GameObject("Luz do pico"); luzPico.transform.position = pico + Vector3.up * 10f; restos.Add(luzPico);
            Light lp = luzPico.AddComponent<Light>(); lp.type = LightType.Point; lp.color = new Color(1f, 0.45f, 0.1f); lp.range = 260f; lp.intensity = 0f; lp.shadows = LightShadows.None;
            montanha.TirarArvores();
            float proxPedra = 0f, proxPoeira = 0f, proxRaioP = 2.5f; bool estourou = false, dragaoSaiu = false; float tEst = -1f;
            yield return Voo(18f, k => Vector3.Lerp(camM0, camM1, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * Mathf.Sin(k * 3f) * 3f,
                k =>
                {
                    Vector3 foco = pico + Vector3.up * 18f;
                    if (dragaoSaiu && chefe != null) foco = Vector3.Lerp(foco, chefe.transform.position, Mathf.Clamp01((k - 0.62f) / 0.12f));
                    return foco;
                }, false, k =>
                {
                    float t = k * 18f;
                    TremorDeCamera.Tremer(Mathf.Lerp(0.08f, 0.5f, Mathf.Clamp01(t / 7f)), 0.3f);
                    // as rachaduras crescem (0 a 6 s)
                    float cresc = Mathf.Clamp01(t / 6.2f);
                    for (int i = 0; i < rachas.Count; i++) { int n = Mathf.Max(2, Mathf.RoundToInt(rachaPts[i].Length * cresc)); rachas[i].positionCount = n; for (int j = 0; j < n; j++) rachas[i].SetPosition(j, rachaPts[i][j]); }
                    lp.intensity = Mathf.Lerp(0f, 18f, Mathf.Clamp01(t / 7f)) * (0.8f + 0.2f * Mathf.Sin(t * 12f)) + (estourou ? Mathf.Max(0f, 60f - (t - tEst) * 20f) : 0f);
                    proxPedra -= Time.deltaTime; if (proxPedra < 0f && t > 1.2f && t < 12f) { proxPedra = estourou ? 0.12f : 0.4f; Vector2 u = UnityEngine.Random.insideUnitCircle * 35f; Vector3 pp = pico + new Vector3(u.x, 0f, u.y); pp.y = ChaoEm(pp) + 2f; StartCoroutine(Pedra(pp, (pp - pico).normalized * UnityEngine.Random.Range(6f, 20f) + Vector3.up * UnityEngine.Random.Range(8f, 26f), UnityEngine.Random.Range(1.8f, 5.5f))); }
                    proxPoeira -= Time.deltaTime; if (proxPoeira < 0f && t > 0.5f) { proxPoeira = 0.18f; Vector2 u = UnityEngine.Random.insideUnitCircle * 40f; Vector3 pp = pico + new Vector3(u.x, 0f, u.y); pp.y = ChaoEm(pp); StartCoroutine(Poeira(pp, UnityEngine.Random.Range(35f, 70f), new Color(0.32f, 0.26f, 0.22f, 0.55f), 4.5f, 9f)); }
                    proxRaioP -= Time.deltaTime; if (proxRaioP < 0f && t > 2.5f && t < 14f) { proxRaioP = UnityEngine.Random.Range(1.4f, 2.6f); FxCena.Raio(pico + Vector3.up * 260f + new Vector3(UnityEngine.Random.Range(-40f, 40f), 0f, UnityEngine.Random.Range(-40f, 40f)), pico + Vector3.up * UnityEngine.Random.Range(0f, 8f), 6f, 0.3f); if (ceu != null) ceu.Relampago(1f); }
                    // o cume desaba (6 a 10 s)
                    if (t > 6.3f)
                    {
                        float kc = Mathf.Clamp01((t - 6.3f) / 4.2f); montanha.Cratera(kc * kc * (3f - 2f * kc), 34f);
                        if (!estourou)
                        {
                            estourou = true; tEst = t; TremorDeCamera.Tremer(0.9f, 3f);
                            StartCoroutine(Anel(pico, 260f, 2.4f, new Color(1f, 0.65f, 0.25f, 0.9f)));
                            for (int i = 0; i < 38; i++) StartCoroutine(Pedra(pico + Vector3.up * 5f + UnityEngine.Random.insideUnitSphere * 8f, new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(1.4f, 3f), UnityEngine.Random.Range(-1f, 1f)).normalized * UnityEngine.Random.Range(22f, 50f), UnityEngine.Random.Range(2.5f, 8f)));
                            for (int i = 0; i < 14; i++) StartCoroutine(Poeira(pico + UnityEngine.Random.insideUnitSphere * 30f, UnityEngine.Random.Range(60f, 110f), new Color(1f, 0.55f, 0.2f, 0.5f), 3.2f, 12f));
                        }
                    }
                    // o dragão sobe da cratera (a partir dos 9 s)
                    if (t > 9.2f && !dragaoSaiu)
                    {
                        dragaoSaiu = true; Vector3 fundo = pico + Vector3.down * 28f; fundo.y = Mathf.Min(fundo.y, ChaoEm(pico) - 8f);
                        StartCoroutine(chefe.Emergir(fundo, ChaoEm(pico) + 70f, centroArena));
                        StartCoroutine(TituloNoRugido());
                    }
                });
            // ================= 7) ele voa até o grupo
            foreach (GameObject g in restos) { }
            Vector3 camJ = NoChao(ancora - paraJog * 0f) + Vector3.up * 1.6f; Vector3 olharMontanha = Achatar(pico - ancora).normalized;
            bool pousou = false; StartCoroutine(Rodar(chefe.Chegada(chefe.transform.position, false), () => pousou = true));
            Vector3 camPos = ancora + olharMontanha * -6f + Vector3.up * 2.4f; Vector3 camOlha = ancora + olharMontanha * 60f + Vector3.up * 40f;
            float tt = 0f; ceu.limiteNeblina = -1f; ceu.alvo = 0.7f; ceu.vel = 0.2f;
            while (!pousou && tt < 40f)
            {
                tt += Time.deltaTime; Vector3 dp = chefe.transform.position;
                float distD = Vector3.Distance(dp, camPos);
                camOlha = Vector3.Lerp(camOlha, dp, 1f - Mathf.Exp(-3f * Time.deltaTime));
                CameraDeCena.Definir(Vector3.Lerp(CameraDeCena.Posicao, camPos, 1f - Mathf.Exp(-2f * Time.deltaTime)), Quaternion.LookRotation(camOlha - camPos));
                GarantirFar(1600f); if (UnityEngine.Random.value < 0.015f) RaioNoCeu(ancora, 60f, 160f);
                yield return null;
            }
            if (Camera.main != null && farOriginal > 0f) Camera.main.farClipPlane = farOriginal;
            // pousou: plano baixo de frente e a conversa
            Vector3 pA = CameraDeCena.Posicao; Quaternion rA = CameraDeCena.Rotacao;
            for (float u = 0f; u < 3.2f; u += Time.deltaTime)
            {
                float b = Mathf.SmoothStep(0f, 1f, u / 1.0f); Vector3 dpos = chefe.transform.position;
                Vector3 pos = dpos + chefe.transform.forward * Mathf.Lerp(40f, 34f, u / 3.2f) + Vector3.Cross(Vector3.up, chefe.transform.forward) * 7f; pos.y = ChaoEm(pos) + 3.2f;
                CameraDeCena.Definir(Vector3.Lerp(pA, pos, b), Quaternion.Slerp(rA, Quaternion.LookRotation(dpos + Vector3.up * 3f - pos), b)); yield return null;
            }
            DialogoUI.Abrir(Roteiro.DragaoAparece, null);
            while (DialogoUI.Aberto) { CameraDeCena.Olhar(CameraDeCena.Posicao, chefe.transform.position + Vector3.up * 3f); yield return null; }
        }
        finally
        {
            FxCena.Branco = 0f; TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menuAntes; Proteger(false);
            if (ceu != null) ceu.limiteNeblina = -1f;
            if (Camera.main != null && farOriginal > 0f) { Camera.main.farClipPlane = farOriginal; }
            if (ladrao != null) { Destroy(ladrao); }
            if (cristalV != null) Destroy(cristalV); if (c1 != null) Destroy(c1); if (c2 != null) Destroy(c2);
        }
        if (chefe == null) yield break;
        Campanha.Dica = "Derrote Drakthar, o dragão que despertou!";
        fase = Fase.Luta; chefe.Despertar();
        chefe.AoFase2 += c => StartCoroutine(CinematicaFase2(c));
        chefe.AoMorrer += c => { StartCoroutine(AposODragao(c)); if (ceu != null) { ceu.alvo = 0f; ceu.vel = 0.05f; } if (tempestade != null) StartCoroutine(DissiparTempestade()); };
    }

    IEnumerator TituloNoRugido()
    {
        yield return new WaitForSeconds(3.6f);
        TituloDeChefe.Mostrar(ApresentacaoDoChefe.Dragao());
    }

    IEnumerator DissiparTempestade()
    {
        FxCena.Tempestade t = tempestade;
        for (float u = 0f; u < 12f && t != null; u += Time.deltaTime) { t.intensidade = Mathf.Lerp(0.8f, 0f, u / 12f); yield return null; }
        if (t != null) Destroy(t.gameObject);
        if (tempestade == t) tempestade = null;
    }

    // chuva de estilhaços cinzentos do céu
    IEnumerator ChuvaDeEstilhacos(Vector3 centro, int total, float dur)
    {
        int feitos = 0; float cont = 0f;
        while (feitos < total)
        {
            cont += Time.deltaTime / dur * total;
            while (feitos < cont && feitos < total)
            {
                feitos++; Vector2 u = UnityEngine.Random.insideUnitCircle * 80f; Vector3 p = centro + new Vector3(u.x, UnityEngine.Random.Range(95f, 200f), u.y);
                float chao = ChaoEm(new Vector3(p.x, 0f, p.z));
                GameObject g = FxCena.NovoEstilhaco(p, UnityEngine.Random.Range(0.5f, 1.8f), chao, new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), -UnityEngine.Random.Range(13f, 26f), UnityEngine.Random.Range(-1.5f, 1.5f)),
                    pousou => StartCoroutine(Poeira(pousou + Vector3.up * 0.3f, UnityEngine.Random.Range(3f, 6f), new Color(0.35f, 0.34f, 0.36f, 0.5f), 1.6f, 1.5f)));
                restos.Add(g);
            }
            yield return null;
        }
    }
}
