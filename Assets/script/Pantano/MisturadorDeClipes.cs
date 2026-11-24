using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Toca e mistura animações de um modelo com esqueleto Generic (os monstros do Pântano) sem precisar de Animator Controller.
// Tocar("Walk") troca de animação com uma mistura suave; clipes sem loop param no último quadro.
[RequireComponent(typeof(Animator))]
public class MisturadorDeClipes : MonoBehaviour
{
    PlayableGraph grafo;
    AnimationMixerPlayable mix;
    readonly Dictionary<string, int> indice = new Dictionary<string, int>();
    readonly List<AnimationClipPlayable> clipes = new List<AnimationClipPlayable>();
    readonly List<float> duracoes = new List<float>();
    float[] pesos = new float[0];
    int atual = -1; float fade = 0.2f;
    public string Atual { get; private set; } = "";

    public void Montar(Dictionary<string, AnimationClip> lista)
    {
        if (grafo.IsValid()) grafo.Destroy();
        indice.Clear(); clipes.Clear(); duracoes.Clear();
        grafo = PlayableGraph.Create("Misturador " + name);
        grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mix = AnimationMixerPlayable.Create(grafo, lista.Count);
        int i = 0;
        foreach (KeyValuePair<string, AnimationClip> kv in lista)
        {
            if (kv.Value == null) continue;
            AnimationClipPlayable p = AnimationClipPlayable.Create(grafo, kv.Value);
            p.SetApplyFootIK(false);
            grafo.Connect(p, 0, mix, i);
            mix.SetInputWeight(i, 0f);
            indice[kv.Key] = i; clipes.Add(p); duracoes.Add(kv.Value.length); i++;
        }
        pesos = new float[clipes.Count];
        AnimationPlayableOutput saida = AnimationPlayableOutput.Create(grafo, "saida", GetComponent<Animator>());
        saida.SetSourcePlayable(mix);
        grafo.Play();
        atual = -1;
    }

    public bool Tem(string nome) { return indice.ContainsKey(nome); }
    public float Duracao(string nome) { int i; return indice.TryGetValue(nome, out i) ? duracoes[i] : 1f; }

    // em que ponto (0 a 1) está a animação atual
    public float Progresso
    {
        get { if (atual < 0) return 0f; return Mathf.Clamp01((float)clipes[atual].GetTime() / Mathf.Max(0.01f, duracoes[atual])); }
    }

    public void Tocar(string nome, float mistura = 0.2f, float velocidade = 1f, bool reiniciar = false)
    {
        int i;
        if (!indice.TryGetValue(nome, out i)) return;
        if (i == atual && !reiniciar) { clipes[i].SetSpeed(velocidade); return; }
        if (i != atual || reiniciar) clipes[i].SetTime(0.0);
        clipes[i].SetSpeed(velocidade);
        atual = i; Atual = nome; fade = Mathf.Max(0.01f, mistura);
    }

    void Update()
    {
        if (!grafo.IsValid() || clipes.Count == 0) return;
        float passo = Time.deltaTime / fade;
        for (int i = 0; i < pesos.Length; i++)
        {
            pesos[i] = Mathf.MoveTowards(pesos[i], i == atual ? 1f : 0f, passo);
            mix.SetInputWeight(i, pesos[i]);
        }
    }

    void OnDestroy() { if (grafo.IsValid()) grafo.Destroy(); }
}

// Tudo que pode ser afetado pelas habilidades dos jogadores (Provocar, Tremor, empurrões)
public interface IAfetavelPorHabilidade
{
    void Provocar(Transform quem, float segundos);
    void Lentidao(float fator, float segundos);
    void Empurrar(Vector3 impulso);
}
