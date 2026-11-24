using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ataques do Guardião contra quem fica PERTO dele (e o dano mais pesado).
//  - VARREDURA: gira o corpo com os braços esticados, varrendo todo o arco da frente.
//  - COMBO DE SOCOS: três socos rápidos, dando um passo à frente a cada um.
//  - GIRATÓRIA (fase 2): rodopia como um furacão de pedra, avançando e machucando tudo em volta; depois fica tonto (aberto a ataques).
//  - PULSO DE AURA (fase 2): quem fica colado nele por muito tempo é punido com uma explosão de aura em volta.
//  - SALTO ESMAGADOR (fase 2): pula em cima de quem está no meio do caminho e cai com uma onda de choque.
public partial class ChefeGuardiao
{
    [Tooltip("Multiplica o dano do golpe e do pisão (e os ataques novos já vêm balanceados). 1 = como era antes.")]
    public float multDano = 1.45f;
    public float danoVarredura = 46f, danoSoco = 30f, danoGiro = 17f, danoPulso = 52f, danoSalto = 48f;

    private int ultimoPerto = -1;
    private float tempoPerto, proximoSalto;

    // quanto tempo os heróis estão colados nele (usado para punir quem "fica na barriga")
    private float proximaMedida;
    private void AcompanharProximidade()
    {
        if (Time.time < proximaMedida) return;
        proximaMedida = Time.time + 0.25f;
        bool perto = false;
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto) continue;
            Vector3 d = p.transform.position - transform.position; d.y = 0f;
            if (d.magnitude < 7.5f) { perto = true; break; }
        }
        tempoPerto = perto ? tempoPerto + 0.25f : Mathf.Max(0f, tempoPerto - 0.5f);
    }

    // escolhe um ataque de perto, sem repetir o anterior
    private bool EscolherAtaquePerto(float dist)
    {
        List<int> op = new List<int>();
        if (dist <= alcanceGolpe) { op.Add(0); op.Add(2); op.Add(3); }
        if (dist <= raioPisao * 0.9f) op.Add(1);
        if (Fase2) { if (dist <= 9f) op.Add(4); if (dist <= 8.5f) op.Add(5); }
        if (op.Count == 0) return false;
        if (op.Count > 1) op.Remove(ultimoPerto);
        int e = op[UnityEngine.Random.Range(0, op.Count)];
        if (Fase2 && tempoPerto > 3f && op.Contains(5)) { e = 5; tempoPerto = 0f; }
        else if (Fase2 && tempoPerto > 3.5f && op.Contains(4)) { e = 4; tempoPerto = 0f; }
        ultimoPerto = e;
        switch (e)
        {
            case 0: StartCoroutine(AtaqueGolpe()); break;
            case 1: StartCoroutine(AtaquePisao()); break;
            case 2: StartCoroutine(AtaqueVarredura()); break;
            case 3: StartCoroutine(AtaqueCombo()); break;
            case 4: StartCoroutine(AtaqueGiratoria()); break;
            default: StartCoroutine(AtaquePulso()); break;
        }
        return true;
    }

    // dá dano em quem está dentro do alcance (e do ângulo à frente, se for menor que 180)
    private void Atingir(float raio, float anguloMax, float dano, HashSet<Vida> jaAcertou)
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
        {
            Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto || jaAcertou.Contains(v)) continue;
            Vector3 d = p.transform.position - transform.position;
            if (d.y > 3.6f) continue; d.y = 0f;
            float dist = d.magnitude; if (dist > raio) continue;
            if (anguloMax < 179f && dist > 1.2f && Vector3.Angle(transform.forward, d) > anguloMax) continue;
            if (v.ReceberDano(dano, transform.position)) jaAcertou.Add(v);
        }
    }

    // anel de choque com dano (dá para pular por cima); o do pisão é o AnelDeChoque original
    private IEnumerator AnelDeDano(Vector3 c, float raioMax, float vel, float dano, Color cor)
    {
        HashSet<Vida> acertados = new HashSet<Vida>();
        GameObject anel = Ef(GameObject.CreatePrimitive(PrimitiveType.Cylinder)); Destroy(anel.GetComponent<Collider>());
        Renderer rd = anel.GetComponent<Renderer>(); rd.sharedMaterial = new Material(Shader.Find("Sprites/Default")); rd.sharedMaterial.color = cor;
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (float r = 0.5f; r < raioMax; r += vel * Time.deltaTime)
        {
            anel.transform.position = c + Vector3.up * 0.12f; anel.transform.localScale = new Vector3(r * 2f, 0.08f, r * 2f);
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            {
                Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto || acertados.Contains(v)) continue;
                Vector3 d = p.transform.position - c; d.y = 0f;
                if (Mathf.Abs(d.magnitude - r) < 1.7f && p.transform.position.y - c.y < 1.2f) { acertados.Add(v); v.ReceberDano(dano, c); }
            }
            yield return null;
        }
        Destroy(anel);
    }

    private void BracosPara(float x, float zAberto, float anteb)
    {
        P("BracoSup_D", x, 0f, -zAberto); P("BracoSup_E", x, 0f, zAberto); P("Antebraco_D", anteb); P("Antebraco_E", anteb);
    }

    // ------------------------------------------------------------ VARREDURA
    private IEnumerator AtaqueVarredura()
    {
        estado = Estado.Golpe; Transform meuAlvo = alvo;
        PerigoNoChao.Golpe(() => transform.position, 7.6f, 0.75f);
        Quaternion baseRot = transform.rotation;
        // 1) vira de lado, braços esticados para trás do giro
        const float T1 = 0.7f;
        for (float t = 0f; t < T1; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T1);
            if (meuAlvo != null && t < 0.3f) { Vector3 d = meuAlvo.position - transform.position; d.y = 0f; if (d.sqrMagnitude > 0.01f) baseRot = Quaternion.LookRotation(d); }
            transform.rotation = baseRot * Quaternion.Euler(0f, 88f * k, 0f);
            BracosPara(-84f * k, 6f * k, -6f); P("Chest", -10f * k); P("Head", -6f * k);
            P("Coxa_D", -12f * k); P("Coxa_E", 10f * k); P("Canela_D", 14f * k);
            yield return null;
        }
        // 2) varre da direita para a esquerda, rápido
        HashSet<Vida> acertados = new HashSet<Vida>(); bool tremeu = false;
        const float T2 = 0.32f;
        for (float t = 0f; t < T2; t += Time.deltaTime)
        {
            float k = t / T2, e = 1f - (1f - k) * (1f - k);
            transform.rotation = baseRot * Quaternion.Euler(0f, Mathf.Lerp(88f, -88f, e), 0f);
            BracosPara(-86f, 4f, -4f); P("Chest", Mathf.Lerp(-10f, 16f, k));
            Atingir(7.2f, 62f, danoVarredura * (Fase2 ? 1.15f : 1f), acertados);
            if (!tremeu && k > 0.3f) { tremeu = true; TremorDeCamera.Tremer(0.3f, 0.5f); Poeira(transform.position + transform.forward * 4f, 3f); }
            if (Time.frameCount % 2 == 0) Poeira(transform.position + transform.forward * 4.5f + Vector3.up * 0.3f, 1.6f);
            yield return null;
        }
        yield return new WaitForSeconds(0.25f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { PoseDeRepouso(0.2f); yield return null; }
        proximoAtaque = Time.time + UnityEngine.Random.Range(1.0f, 1.8f) * ritmo;
        estado = Estado.Perseguindo;
    }

    // ------------------------------------------------------------ COMBO DE 3 SOCOS
    private IEnumerator AtaqueCombo()
    {
        estado = Estado.Golpe; Transform meuAlvo = alvo;
        for (int i = 0; i < 3; i++)
        {
            bool dir = i % 2 == 0; string braco = dir ? "BracoSup_D" : "BracoSup_E", ante = dir ? "Antebraco_D" : "Antebraco_E";
            float lado = dir ? -1f : 1f;
            PerigoNoChao.Golpe(() => transform.position + transform.forward * 3.1f, 3.6f, 0.32f);
            // prepara o soco: cotovelo para trás, tronco gira
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.3f);
                if (meuAlvo != null) GiraPara(meuAlvo.position, 280f);
                P(braco, -22f * k, 0f, lado * 18f * k); P(ante, -112f * k); P("Chest", -8f * k, -lado * 22f * k); P("Spine", 0f, -lado * 8f * k); P("Head", -6f * k);
                P("Coxa_D", dir ? 12f * k : -14f * k); P("Coxa_E", dir ? -14f * k : 12f * k);
                yield return null;
            }
            // soco: dá um passo à frente e estica o braço
            bool bateu = false; HashSet<Vida> acertados = new HashSet<Vida>();
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                float k = t / 0.16f;
                P(braco, Mathf.Lerp(-22f, -94f, k), 0f, lado * Mathf.Lerp(18f, 2f, k)); P(ante, Mathf.Lerp(-112f, -4f, k)); P("Chest", Mathf.Lerp(-8f, 18f, k), lado * 18f * k); P("Spine", 8f * k);
                if (cc.enabled) cc.Move(transform.forward * 9f * Time.deltaTime + Vector3.up * vy * Time.deltaTime);
                if (k > 0.4f) Atingir(4.2f, 55f, danoSoco * (Fase2 ? 1.2f : 1f), acertados);
                if (!bateu && acertados.Count > 0) { bateu = true; TremorDeCamera.Tremer(0.25f, 0.3f); }
                yield return null;
            }
            TremorDeCamera.Tremer(0.14f, 0.2f); Poeira(transform.position + transform.forward * 3f, 1.4f);
            yield return new WaitForSeconds(Fase2 ? 0.1f : 0.2f);
        }
        for (float t = 0f; t < 0.8f; t += Time.deltaTime) { PoseDeRepouso(0.2f); yield return null; }
        proximoAtaque = Time.time + UnityEngine.Random.Range(1.0f, 1.9f) * ritmo;
        estado = Estado.Perseguindo;
    }

    // ------------------------------------------------------------ GIRATÓRIA (fase 2)
    private IEnumerator AtaqueGiratoria()
    {
        estado = Estado.Golpe; Transform meuAlvo = alvo;
        PerigoNoChao.Golpe(() => transform.position, 7.4f, 0.8f);
        // 1) se agacha e abre os braços
        const float T1 = 0.8f;
        for (float t = 0f; t < T1; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T1);
            if (meuAlvo != null) GiraPara(meuAlvo.position, 140f);
            BracosPara(-8f * k, 84f * k, -6f); P("Chest", 14f * k); P("Spine", 8f * k); P("Head", 8f * k);
            P("Coxa_D", -20f * k); P("Coxa_E", -20f * k); P("Canela_D", 28f * k); P("Canela_E", 28f * k);
            if (esqueleto != null) esqueleto.localPosition = new Vector3(0f, -0.6f * k, 0f);
            if (Time.frameCount % 3 == 0) Brasas(transform.position + UnityEngine.Random.insideUnitSphere * 3f + Vector3.up * 0.5f, 2, 4f);
            yield return null;
        }
        // 2) gira como um furacão, avançando devagar na direção do alvo, e machuca todo mundo em volta
        const float T2 = 2.4f; float prox = 0f, giro = 0f;
        Dictionary<Vida, float> proximoDano = new Dictionary<Vida, float>();
        for (float t = 0f; t < T2; t += Time.deltaTime)
        {
            giro += 760f * Time.deltaTime; transform.Rotate(0f, 760f * Time.deltaTime, 0f, Space.Self);
            BracosPara(-6f, 86f, -4f); P("Chest", 10f); P("Coxa_D", -14f); P("Coxa_E", -14f); P("Canela_D", 20f); P("Canela_E", 20f);
            Vector3 mover = Vector3.zero;
            if (meuAlvo != null) { Vector3 d = meuAlvo.position - transform.position; d.y = 0f; if (d.magnitude > 2.5f) mover = d.normalized * 3.6f; }
            if (cc.enabled) cc.Move((mover + Vector3.up * vy) * Time.deltaTime);
            if (Time.time >= prox) { prox = Time.time + 0.55f; PerigoNoChao.Golpe(() => transform.position, 7.2f, 0.45f); }
            foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None))
            {
                Vida v = p.GetComponent<Vida>(); if (v == null || v.Morto) continue;
                Vector3 d = p.transform.position - transform.position; if (d.y > 3.2f) continue; d.y = 0f;
                if (d.magnitude > 6.6f) continue;
                float pr; if (proximoDano.TryGetValue(v, out pr) && Time.time < pr) continue;
                proximoDano[v] = Time.time + 0.4f; v.ReceberDano(danoGiro, transform.position);
            }
            if (Time.frameCount % 2 == 0) { Poeira(transform.position + UnityEngine.Random.insideUnitSphere * 4f, 1.4f); Brasas(transform.position + Vector3.up * 1.5f + UnityEngine.Random.insideUnitSphere * 3.5f, 2, 5f); }
            if (Time.frameCount % 5 == 0) TremorDeCamera.Tremer(0.2f, 0.25f);
            yield return null;
        }
        // 3) fica tonto: janela para atacá-lo
        if (esqueleto != null) esqueleto.localPosition = Vector3.zero;
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            float b = Mathf.Sin(t * 5f) * 6f * (1f - t / 1.5f);
            BracosPara(10f, 12f, -16f); P("Chest", 26f + b, b); P("Head", 22f + b, -b * 2f); P("Spine", 10f);
            P("Coxa_D", -10f); P("Coxa_E", -10f); P("Canela_D", 14f); P("Canela_E", 14f);
            yield return null;
        }
        proximoAtaque = Time.time + UnityEngine.Random.Range(0.8f, 1.4f) * ritmo;
        estado = Estado.Perseguindo;
    }

    // ------------------------------------------------------------ PULSO DE AURA (fase 2)
    private IEnumerator AtaquePulso()
    {
        estado = Estado.Golpe;
        PerigoNoChao.Golpe(() => transform.position, 10.5f, 1.0f);
        GameObject luz = Ef(new GameObject("Luz do pulso")); Light l = luz.AddComponent<Light>(); l.color = new Color(1f, 0.4f, 0.14f); l.range = 24f; l.shadows = LightShadows.None;
        // 1) puxa a energia para o peito: punhos fechados, corpo encolhido, tudo brilhando
        const float T1 = 1.0f;
        for (float t = 0f; t < T1; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T1), tr = Mathf.Sin(Time.time * 50f) * 2f * k;
            BracosPara(-40f * k, -10f * k, -112f * k); P("Chest", 24f * k + tr, tr); P("Spine", 12f * k); P("Head", 20f * k);
            P("Coxa_D", -22f * k); P("Coxa_E", -22f * k); P("Canela_D", 30f * k); P("Canela_E", 30f * k);
            if (esqueleto != null) esqueleto.localPosition = new Vector3(0f, -0.7f * k, 0f);
            Rachar(1.5f); SetFuria(1f);
            l.transform.position = transform.position + Vector3.up * 2.5f; l.intensity = 22f * k;
            PracaDoGuardiao pr = PracaDoGuardiao.Instancia;
            if (pr != null && Time.frameCount % 2 == 0) { Vector3 p = transform.position + UnityEngine.Random.onUnitSphere * 7f + Vector3.up * 2f; pr.EmitirBrasa(p, (transform.position + Vector3.up * 2.5f - p).normalized * 9f, new Color(1f, 0.5f, 0.15f, 1f), 0.3f); }
            if (Time.frameCount % 4 == 0) TremorDeCamera.Tremer(0.05f + 0.12f * k, 0.3f);
            yield return null;
        }
        // 2) explode: braços abertos, onda de luz que dá para pular
        BracosPara(-96f, 70f, -10f); P("Chest", -22f); P("Head", -32f);
        if (esqueleto != null) esqueleto.localPosition = Vector3.zero;
        HashSet<Vida> pertinho = new HashSet<Vida>(); Atingir(3.6f, 180f, danoPulso * 0.5f, pertinho);      // quem está colado leva um golpe a mais
        StartCoroutine(AnelDeDano(transform.position, 11.5f, 19f, danoPulso, new Color(1f, 0.5f, 0.18f, 0.55f)));
        StartCoroutine(AnelVisual(transform.position, 12f, 14f, new Color(0.6f, 0.25f, 1f, 0.45f)));
        GameObject flash = Ef(AuraDoGuardiao.Brilho(transform.position + Vector3.up * 2.5f, 12f, new Color(3.2f, 1.2f, 0.45f, 1f)));
        TremorDeCamera.Tremer(0.7f, 1.2f); Poeira(transform.position + Vector3.up * 0.3f, 4.5f); Brasas(transform.position + Vector3.up * 2f, 70, 9f);
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            l.intensity = Mathf.Lerp(40f, 0f, t / 0.9f); flash.transform.localScale = Vector3.one * Mathf.Lerp(12f, 0.1f, Mathf.Clamp01(t / 0.5f));
            yield return null;
        }
        Destroy(flash); Destroy(luz);
        for (float t = 0f; t < 0.7f; t += Time.deltaTime) { PoseDeRepouso(0.2f); yield return null; }
        proximoAtaque = Time.time + UnityEngine.Random.Range(0.9f, 1.5f) * ritmo;
        estado = Estado.Perseguindo;
    }

    // ------------------------------------------------------------ SALTO ESMAGADOR (fase 2)
    private IEnumerator AtaqueSalto()
    {
        estado = Estado.Golpe; proximoSalto = Time.time + 9f;
        Transform meuAlvo = alvo; if (meuAlvo == null) { estado = Estado.Perseguindo; yield break; }
        // 1) agacha, mirando
        const float T1 = 0.65f;
        for (float t = 0f; t < T1; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / T1);
            GiraPara(meuAlvo.position, 220f);
            P("Chest", 30f * k); P("Spine", 12f * k); P("Head", 10f * k); BracosPara(26f * k, 6f * k, -10f);
            P("Coxa_D", -34f * k); P("Coxa_E", -34f * k); P("Canela_D", 52f * k); P("Canela_E", 52f * k);
            if (esqueleto != null) esqueleto.localPosition = new Vector3(0f, -1.0f * k, 0f);
            yield return null;
        }
        // 2) salta em arco até onde o alvo estava
        Vector3 ini = transform.position, fim = meuAlvo.position; fim.y = ChaoEm(fim);
        Vector3 plano = fim - ini; plano.y = 0f; float dist = plano.magnitude;
        if (dist > 22f) fim = ini + plano.normalized * 22f; fim.y = ChaoEm(fim);
        Vector3 pontoFinal = fim;
        PerigoNoChao.Golpe(() => pontoFinal, 9.5f, 0.95f);
        cc.enabled = false;
        const float T2 = 0.9f; HashSet<Vida> nada = new HashSet<Vida>();
        for (float t = 0f; t < T2; t += Time.deltaTime)
        {
            float k = t / T2;
            Vector3 p = Vector3.Lerp(ini, fim, k); p.y = Mathf.Lerp(ini.y, fim.y, k) + Mathf.Sin(k * Mathf.PI) * 8f;
            transform.position = p;
            if (plano.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(plano), 400f * Time.deltaTime);
            BracosPara(-150f, 40f, -20f); P("Chest", -20f + 30f * k); P("Head", -24f);
            P("Coxa_D", -50f); P("Coxa_E", -50f); P("Canela_D", 70f); P("Canela_E", 70f);
            if (esqueleto != null) esqueleto.localPosition = Vector3.Lerp(esqueleto.localPosition, Vector3.zero, Time.deltaTime * 10f);
            if (Time.frameCount % 2 == 0) Brasas(transform.position + Vector3.up, 2, 3f);
            yield return null;
        }
        // 3) cai: onda de choque grande
        transform.position = fim; cc.enabled = true;
        Impacto(fim + Vector3.up * 0.3f, 5.2f, danoSalto);
        StartCoroutine(AnelDeDano(fim, 10.5f, 20f, danoSalto * 0.7f, new Color(0.9f, 0.65f, 0.4f, 0.5f)));
        TremorDeCamera.Tremer(0.7f, 1.0f); Poeira(fim, 4.5f); Brasas(fim + Vector3.up, 40, 7f);
        for (int i = 0; i < 14; i++) Lasca(fim + Vector3.up * 0.5f + UnityEngine.Random.insideUnitSphere * 1.5f, new Vector3(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(3f, 9f), UnityEngine.Random.Range(-6f, 6f)), UnityEngine.Random.Range(0.25f, 0.7f));
        for (float t = 0f; t < 1.1f; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 0.2f); P("Chest", Mathf.Lerp(10f, 34f, k)); BracosPara(Mathf.Lerp(-60f, 30f, k), 6f, -10f);
            P("Coxa_D", -30f * k); P("Coxa_E", -30f * k); P("Canela_D", 40f * k); P("Canela_E", 40f * k);
            if (esqueleto != null) esqueleto.localPosition = new Vector3(0f, -0.8f * k * (1f - t / 1.1f), 0f);
            SimularPedacos(fim.y); yield return null;
        }
        proximoAtaque = Time.time + UnityEngine.Random.Range(0.8f, 1.5f) * ritmo;
        estado = Estado.Perseguindo;
    }
}
