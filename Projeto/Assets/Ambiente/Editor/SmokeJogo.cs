using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Teste de fumaca em batch: entra no Play, espera alguns quadros e confere o jogador (sem excecoes, no chao, animador tocando).
[InitializeOnLoad]
public static class SmokeJogo
{
    static int quadros; static Vector3 p0;
    static SmokeJogo()
    {
        if (EditorPrefs.GetInt("smoke_jogo", 0) != 1) return;
        EditorApplication.update += Passo;
        Application.logMessageReceived += (msg, st, tipo) => { if (tipo == LogType.Exception || tipo == LogType.Error) Debug.Log("SMOKE-ERRO " + msg.Split('\n')[0]); };
    }
    public static void Rodar()
    {
        EditorPrefs.SetInt("smoke_jogo", 1);
        EditorSceneManager.OpenScene("Assets/Ambiente/Teste_Terreno.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Passo()
    {
        if (!EditorApplication.isPlaying) return;
        quadros++;
        var j = Object.FindFirstObjectByType<JogadorMapa>();
        if (quadros == 5 && j) p0 = j.transform.position;
        if (quadros == 90)
        {
            var cc = j ? j.GetComponent<CharacterController>() : null; var an = j ? j.GetComponent<Animator>() : null;
            Debug.Log($"SMOKE jogador={(j != null)} pos0={p0} pos={(j ? j.transform.position : Vector3.zero)} noChao={(cc != null && cc.isGrounded)} estado={(an ? an.GetCurrentAnimatorClipInfo(0).Length > 0 ? an.GetCurrentAnimatorClipInfo(0)[0].clip.name : "sem-clipe" : "sem-anim")} cam={Camera.main?.transform.position}");
            EditorPrefs.SetInt("smoke_jogo", 0); EditorApplication.Exit(0);
        }
    }
}
