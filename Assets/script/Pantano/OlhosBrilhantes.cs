using System.Collections.Generic;
using UnityEngine;

// Dois olhos que brilham em vermelho (luz vermelha + clarão), presos à cabeça de um modelo com esqueleto.
// Brilho(0..1) controla a intensidade: o Devorador do Lago acende os olhos ainda debaixo d'água, antes de sair.
public class OlhosBrilhantes : MonoBehaviour
{
    public Material materialClarao;                         // círculo macio (Resources/Agua/Gota)
    public Color cor = new Color(1f, 0.1f, 0.05f);
    public float alcanceDaLuz = 7f, intensidadeDaLuz = 6f, tamanho = 0.55f;

    class Olho { public Transform t; public Light luz; public ParticleSystem nucleo, halo; }
    readonly List<Olho> olhos = new List<Olho>();
    readonly ParticleSystem.Particle[] um = new ParticleSystem.Particle[1];
    float brilho, alvoBrilho;
    public float Pulsar { get; set; }

    // cabeca: osso da cabeça; posicoesNaRaiz: onde ficam os olhos, em coordenadas da raiz do modelo (na pose de repouso)
    public void Montar(Transform cabeca, Transform raiz, params Vector3[] posicoesNaRaiz)
    {
        foreach (Vector3 p in posicoesNaRaiz)
        {
            GameObject g = new GameObject("Olho brilhante");
            g.transform.SetParent(cabeca, false);
            g.transform.position = raiz.TransformPoint(p);               // fixa o ponto no mundo e deixa o osso carregar
            Olho o = new Olho { t = g.transform };
            o.luz = g.AddComponent<Light>(); o.luz.type = LightType.Point; o.luz.color = cor; o.luz.range = alcanceDaLuz; o.luz.intensity = 0f; o.luz.shadows = LightShadows.None;
            o.nucleo = NovoClarao(g.transform, "Núcleo", tamanho * 0.7f);
            o.halo = NovoClarao(g.transform, "Halo", tamanho * 2.5f);
            olhos.Add(o);
        }
    }

    ParticleSystem NovoClarao(Transform pai, string nome, float tam)
    {
        GameObject g = new GameObject(nome); g.transform.SetParent(pai, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 100000f; main.startSpeed = 0f; main.startSize = tam; main.startColor = Color.white; main.maxParticles = 1;
        var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        Material m = materialClarao;
        if (m == null) { Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (s == null) s = Shader.Find("Sprites/Default"); m = new Material(s); }
        r.sharedMaterial = m; r.renderMode = ParticleSystemRenderMode.Billboard; r.sortingFudge = -10f;
        ps.Emit(1); ps.Play();
        return ps;
    }

    public void Acender(float alvo) { alvoBrilho = Mathf.Clamp01(alvo); }
    public void AcenderJa(float v) { brilho = alvoBrilho = Mathf.Clamp01(v); }

    void LateUpdate()
    {
        brilho = Mathf.MoveTowards(brilho, alvoBrilho, Time.deltaTime * 0.8f);
        float p = 1f + Pulsar * (0.25f * Mathf.Sin(Time.time * 9f)) + Mathf.Sin(Time.time * 2.3f) * 0.05f;
        float k = brilho * p;
        foreach (Olho o in olhos)
        {
            if (o.luz != null) o.luz.intensity = intensidadeDaLuz * k;
            Pintar(o.nucleo, new Color(1f, 0.55f, 0.35f, Mathf.Clamp01(k)));
            Pintar(o.halo, new Color(cor.r, cor.g, cor.b, Mathf.Clamp01(0.55f * k)));
        }
    }

    void Pintar(ParticleSystem ps, Color c)
    {
        if (ps == null) return;
        if (ps.GetParticles(um) < 1) { ps.Emit(1); if (ps.GetParticles(um) < 1) return; }
        um[0].startColor = c; um[0].remainingLifetime = 100000f;
        ps.SetParticles(um, 1);
    }
}
