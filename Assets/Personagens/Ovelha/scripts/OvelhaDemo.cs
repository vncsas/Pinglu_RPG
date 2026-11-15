using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

// Demo para ver os clipes do cavalo: teclas 1-7 (Idle, Walk, Trot, Gallop, Comer_Entrar, Comer, Comer_Sair). Usa PlayableGraph, sem Animator Controller.
[RequireComponent(typeof(Animator))]
public class OvelhaDemo : MonoBehaviour
{
    public AnimationClip[] clips;
    PlayableGraph g; AnimationClipPlayable atual; int idx = -1;

    void Start() { g = PlayableGraph.Create("OvelhaDemo"); var o = AnimationPlayableOutput.Create(g, "o", GetComponent<Animator>()); Tocar(0); g.Play(); }
    void OnDestroy() { if (g.IsValid()) g.Destroy(); }
    void Tocar(int i)
    {
        if (clips == null || i < 0 || i >= clips.Length || clips[i] == null || i == idx) return;
        idx = i; if (atual.IsValid()) atual.Destroy();
        atual = AnimationClipPlayable.Create(g, clips[i]); g.GetOutput(0).SetSourcePlayable(atual);
    }
    void Update()
    {
        var k = Keyboard.current; if (k == null) return;
        if (k.digit1Key.wasPressedThisFrame) Tocar(0); if (k.digit2Key.wasPressedThisFrame) Tocar(1); if (k.digit3Key.wasPressedThisFrame) Tocar(2);
        if (k.digit4Key.wasPressedThisFrame) Tocar(3); if (k.digit5Key.wasPressedThisFrame) Tocar(4); if (k.digit6Key.wasPressedThisFrame) Tocar(5); if (k.digit7Key.wasPressedThisFrame) Tocar(6);
    }
}
