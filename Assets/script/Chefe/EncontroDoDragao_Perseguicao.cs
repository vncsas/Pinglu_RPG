using System.Collections;
using UnityEngine;

// A FUGA DO LADRÃO. Depois do último guardião (e do último cristal), o Ladrão aparece perto do grupo, debocha, diz que está com o terceiro cristal
// e FOGE correndo, deixando um RASTRO NEGRO no chão que vai até o portal, no meio do mapa. O jogador segue o rastro; ao chegar ao portal
// toca a cinemática do vilão (EncontroDoDragao_Cinematica.cs), em que ele conta o plano.
public partial class EncontroDoDragao
{
    bool perseguicaoFeita; RastroNegro rastro;

    // onde a trilha termina: na frente do portal do mundo (ou no centro do terreno, se o portal não existir)
    Vector3 PosicaoDoPortal()
    {
        PortalDoMundo p = PortalDoMundo.Atual;
        if (p != null) { Vector3 v = p.CentroNoChao + p.Frente * 14f; v.y = ChaoEm(v); return v; }
        Terrain t = Terrain.activeTerrain;
        if (t != null) { Vector3 c = t.transform.position + new Vector3(t.terrainData.size.x * 0.5f, 0f, t.terrainData.size.z * 0.5f); c.y = ChaoEm(c); return c; }
        return posicaoMontanha;
    }

    void LimparRastro() { if (rastro != null) Destroy(rastro.gameObject); rastro = null; }

    // F7 com Shift (só no editor): toca a fuga na hora
    void IrDiretoFuga()
    {
        PortalDoMundo.Revelar();
        if (DialogoUI.Aberto || fase == Fase.Cena) return;
        Transform local = JogadorLocal(); if (local == null) return;
        StopAllCoroutines(); LimparTudo();
        TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
        GerenciadorDeOndas.HordaVencida = true;
        Campanha.CemiterioFeito = Campanha.CasteloFeito = Campanha.PantanoFeito = true;
        if (Campanha.Atual != Etapa.Dragao) Campanha.Ir(Etapa.Dragao);
        fase = Fase.Esperando; StartCoroutine(CenaDaFuga(local));
    }

    IEnumerator CenaDaFuga(Transform local)
    {
        PortalDoMundo.Revelar();                                       // o portal do meio do mapa volta a aparecer (a história precisa dele)
        fase = Fase.Cena; perseguicaoFeita = true; menuAntes = SessaoDeJogo.MenuAberto;
        Vector3 ancora = NoChao(local.position), destino = PosicaoDoPortal();
        Vector3 paraPortal = Achatar(destino - ancora); if (paraPortal.sqrMagnitude < 25f) paraPortal = Achatar(local.forward); paraPortal.Normalize();
        Vector3 dir = Vector3.Cross(Vector3.up, paraPortal);
        SessaoDeJogo.MenuAberto = true; TituloDeChefe.Barras(true); CameraDeCena.Ligar(); Proteger(true); FxCena.GarantirTela();
        GameObject ladrao = null; InimigoCavaleiro ic = null;
        try
        {
            // ================= 1) o Ladrão surge à frente, entre o grupo e o portal
            Vector3 pLad = NoChao(ancora + paraPortal * 9f);
            if (prefabLadrao != null)
            {
                ladrao = Instantiate(prefabLadrao, pLad, Quaternion.LookRotation(-paraPortal)); ladrao.name = "Ladrao (fuga)"; restos.Add(ladrao);
                ic = ladrao.GetComponent<InimigoCavaleiro>(); if (ic != null) { ic.ModoCena(); ic.Fumaca(); ic.Intensificar(0.6f); }
            }
            local.rotation = Quaternion.LookRotation(Achatar(pLad - local.position));
            TremorDeCamera.Tremer(0.3f, 1.0f);
            for (int i = 0; i < 6; i++) StartCoroutine(Poeira(pLad + UnityEngine.Random.insideUnitSphere * 1.6f + Vector3.up * 0.4f, 7f, new Color(0.1f, 0.05f, 0.14f, 0.85f), 2.0f, 2.5f));
            StartCoroutine(Anel(pLad, 14f, 1.3f, new Color(0.7f, 0.3f, 1f, 0.8f)));
            Vector3 cabecaLad = pLad + Vector3.up * 1.8f;
            yield return Voo(2.8f, k => Vector3.Lerp(ancora - paraPortal * 2.5f + dir * 2.2f + Vector3.up * 2.1f, ancora + paraPortal * 1.5f + dir * 3.3f + Vector3.up * 1.7f, k), k => Vector3.Lerp(pLad + Vector3.up * 1.5f, cabecaLad, k), true);
            DialogoUI.Abrir(Roteiro.LadraoFoge, null);
            float o = 0f;
            while (DialogoUI.Aberto)
            {
                o += Time.deltaTime * 0.12f;
                Vector3 p = Vector3.Lerp(ancora, pLad, 0.5f) + Quaternion.Euler(0f, 90f + Mathf.Sin(o) * 18f, 0f) * paraPortal * 5.6f + Vector3.up * 1.9f;
                CameraDeCena.Definir(p, Quaternion.LookRotation(Vector3.Lerp(ancora, pLad, 0.5f) + Vector3.up * 1.6f - p)); yield return null;
            }

            // ================= 2) ele corre pela trilha (que vai surgindo atrás dele) e some numa nuvem de fumaça
            LimparRastro(); rastro = RastroNegro.Criar(pLad, destino); rastro.revelado = 0f;
            if (ladrao != null) ladrao.transform.rotation = Quaternion.LookRotation(Achatar(rastro.Em(8f) - pLad));
            TremorDeCamera.Tremer(0.2f, 0.6f);
            float s = 0f, vel = 8f, tempo = 0f, duracao = 5.2f; Vector3 camPos = CameraDeCena.Posicao;
            while (tempo < duracao)
            {
                tempo += Time.deltaTime; vel = Mathf.Min(26f, vel + 30f * Time.deltaTime); s += vel * Time.deltaTime;
                Vector3 p = rastro.Em(s); p.y = ChaoEm(p); Vector3 frente = Achatar(rastro.Em(s + 6f) - p); if (frente.sqrMagnitude < 0.1f) frente = paraPortal; frente.Normalize();
                if (ladrao != null) { ladrao.transform.position = p; ladrao.transform.rotation = Quaternion.Slerp(ladrao.transform.rotation, Quaternion.LookRotation(frente), Time.deltaTime * 10f); }
                rastro.revelado = s / rastro.Espacamento + 1f;
                if (UnityEngine.Random.value < 0.6f) StartCoroutine(Poeira(p + UnityEngine.Random.insideUnitSphere * 0.8f, 3.5f, new Color(0.08f, 0.03f, 0.12f, 0.8f), 1.4f, 1.5f));
                // câmera atrás e acima, seguindo a corrida
                Vector3 alvoCam = p - frente * 10f + Vector3.Cross(Vector3.up, frente) * 3.5f + Vector3.up * 4.6f; alvoCam.y = Mathf.Max(alvoCam.y, ChaoEm(alvoCam) + 1.5f);
                camPos = Vector3.Lerp(camPos, alvoCam, 1f - Mathf.Exp(-2.4f * Time.deltaTime));
                CameraDeCena.Definir(camPos, Quaternion.LookRotation(p + Vector3.up * 1.6f + frente * 4f - camPos));
                TremorDeCamera.Tremer(0.04f, 0.2f);
                yield return null;
            }
            // some: nuvem de fumaça e onda roxa
            Vector3 pf = rastro.Em(s); pf.y = ChaoEm(pf);
            for (int i = 0; i < 6; i++) StartCoroutine(Poeira(pf + UnityEngine.Random.insideUnitSphere * 1.4f + Vector3.up * 1f, 8f, new Color(0.1f, 0.04f, 0.16f, 0.9f), 2.2f, 3f));
            StartCoroutine(Anel(pf, 16f, 1.5f, new Color(0.7f, 0.3f, 1f, 0.9f)));
            if (ladrao != null) { if (ic != null) ic.Sumir(); else Destroy(ladrao); ladrao = null; }
            // ================= 3) o rastro inteiro aparece até o portal
            float inicio = rastro.revelado, total = rastro.pontos.Count + 1f; Vector3 camFixa = CameraDeCena.Posicao;
            for (float t = 0f; t < 3.2f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 3.2f); rastro.revelado = Mathf.Lerp(inicio, total, k);
                Vector3 cabeca = rastro.Em(Mathf.Min(rastro.revelado * rastro.Espacamento, rastro.Comprimento));
                CameraDeCena.Definir(camFixa + Vector3.up * (k * 6f), Quaternion.LookRotation(Vector3.Lerp(pf, cabeca, 0.7f) + Vector3.up * 2f - (camFixa + Vector3.up * (k * 6f)))); yield return null;
            }
            rastro.revelado = total; yield return new WaitForSeconds(0.6f);
        }
        finally
        {
            TituloDeChefe.Barras(false); CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = menuAntes; Proteger(false);
            if (ladrao != null) Destroy(ladrao);
        }
        Campanha.Dica = "Siga o rastro negro do Ladrão até o portal, no meio do mapa.";
        fase = Fase.Esperando; proxBusca = Time.time + 1f;
    }
}
