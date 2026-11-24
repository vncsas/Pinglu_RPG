using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

// Demo dos clipes do Golem (tanque): 1 Idle, 2 Walk, 3 Run, 4 Pancada, 5 Martelada, 6 Defender (segura ate apertar 0), 7 Provocar, 8 Levar_Golpe, 9 Morrer, 0 sair da defesa.
// Os golpes/reacoes tocam uma vez e voltam para o Idle (Morrer fica no ultimo quadro). Usa PlayableGraph, sem Animator Controller.
[RequireComponent(typeof(Animator))]
public class GolemRochaDemo : MonoBehaviour
{
    // ordem em clips: 0 Idle, 1 Walk, 2 Run, 3 Pancada, 4 Martelada, 5 Defender_Entrar, 6 Provocar, 7 Levar_Golpe, 8 Morrer, 9 Defender_Sair, 10 Defender_Segurar
    public AnimationClip[] clips;
    PlayableGraph g; AnimationClipPlayable atual; int idx = -1; float t0;

    void Start() { g = PlayableGraph.Create("GolemRochaDemo"); AnimationPlayableOutput.Create(g, "o", GetComponent<Animator>()); Tocar(0); g.Play(); }
    void OnDestroy() { if (g.IsValid()) g.Destroy(); }
    void Tocar(int i)
    {
        if (clips == null || i < 0 || i >= clips.Length || clips[i] == null) return;
        idx = i; if (atual.IsValid()) atual.Destroy();
        atual = AnimationClipPlayable.Create(g, clips[i]); g.GetOutput(0).SetSourcePlayable(atual); t0 = Time.time;
    }
    void Update()
    {
        var k = Keyboard.current; if (k == null) return;
        if (k.digit1Key.wasPressedThisFrame) Tocar(0); if (k.digit2Key.wasPressedThisFrame) Tocar(1); if (k.digit3Key.wasPressedThisFrame) Tocar(2);
        if (k.digit4Key.wasPressedThisFrame) Tocar(3); if (k.digit5Key.wasPressedThisFrame) Tocar(4); if (k.digit6Key.wasPressedThisFrame) Tocar(5);
        if (k.digit7Key.wasPressedThisFrame) Tocar(6); if (k.digit8Key.wasPressedThisFrame) Tocar(7); if (k.digit9Key.wasPressedThisFrame) Tocar(8);
        if (k.digit0Key.wasPressedThisFrame && idx == 10) Tocar(9);
        if (clips == null || idx < 0 || clips[idx] == null || clips[idx].isLooping) return;
        if (Time.time - t0 > clips[idx].length)
        {
            if (idx == 5) Tocar(10);                      // entrou na defesa: segura em loop
            else if (idx == 8) { }                        // morreu: fica no chao
            else Tocar(0);
        }
    }
}
