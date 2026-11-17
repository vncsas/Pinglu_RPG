using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

// Demo dos clipes do Angler: teclas 1-9 e 0 = Idle, Walk, Run, Attack_Mordida, Attack_Garras, Attack_Cauda, Attack_Salto, Rugido, Dano, Morte.
// Os ataques/dano/morte tocam uma vez e voltam para o Idle sozinhos (a Morte fica no ultimo quadro). Usa PlayableGraph, sem Animator Controller.
[RequireComponent(typeof(Animator))]
public class AnglerDemo : MonoBehaviour
{
    public AnimationClip[] clips;
    PlayableGraph g; AnimationClipPlayable atual; int idx = -1; float t0;

    void Start() { g = PlayableGraph.Create("AnglerDemo"); AnimationPlayableOutput.Create(g, "o", GetComponent<Animator>()); Tocar(0); g.Play(); }
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
        var teclas = new[] { k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key, k.digit5Key, k.digit6Key, k.digit7Key, k.digit8Key, k.digit9Key, k.digit0Key };
        for (int i = 0; i < teclas.Length; i++) if (teclas[i].wasPressedThisFrame) Tocar(i);
        if (idx >= 3 && idx != 9 && !clips[idx].isLooping && Time.time - t0 > clips[idx].length) Tocar(0);
    }
}
