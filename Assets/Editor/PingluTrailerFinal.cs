// =====================================================================
//  PingluTrailerFinal.cs  -  grava o trailer NOVO (Trial of the Forsaken) com o Unity Recorder
//  Pinglu > 37. Trailer novo: gravar vídeo     (grava Recordings/trailer_final.webm e para sozinho)
//  Pinglu > 38. Trailer novo: só assistir
//  Sem ninguém:  PINGLU_TRAILER=1 Unity -projectPath <projeto> -executeMethod PingluTrailerFinal.GravarEFechar
//  Um herói só:  PINGLU_TRAILER_HEROI=2  (0 a 3)    Grava em 60 quadros por segundo.
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PingluTrailerFinal
{
    static string CENA { get { string s = System.Environment.GetEnvironmentVariable("PINGLU_TRAILER_CENA"); return string.IsNullOrEmpty(s) ? "Assets/Scenes/SampleScene.unity" : s; } }   // a Fera do Pântano só existe na cena Ambientacao
    const string TRILHA = "Assets/Trailer/trilha_trailer.wav";
    const string SK_GRAVAR = "PingluTrailerFinalGravar", SK_ATIVO = "PingluTrailerFinalAtivo", SK_SAIR = "PingluTrailerFinalSair";
    static RecorderController gravador;

    static PingluTrailerFinal() { EditorApplication.playModeStateChanged += AoMudarPlay; }

    [MenuItem("Pinglu/37. Trailer novo: gravar vídeo")] public static void Gravar() { Iniciar(true, false); }
    [MenuItem("Pinglu/38. Trailer novo: só assistir")] public static void Assistir() { Iniciar(false, false); }
    public static void GravarEFechar() { Iniciar(true, true); }

    static void Iniciar(bool gravar, bool sair)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        System.Environment.SetEnvironmentVariable("PINGLU_TRAILER", "1");                 // a abertura e o tutorial do jogo não tocam
        if (EditorSceneManager.GetActiveScene().path != CENA) EditorSceneManager.OpenScene(CENA);
        SessionState.SetBool(SK_GRAVAR, gravar); SessionState.SetBool(SK_ATIVO, true); SessionState.SetBool(SK_SAIR, sair);
        EditorApplication.isPlaying = true;
    }

    static void AoMudarPlay(PlayModeStateChange e)
    {
        if (e == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SK_ATIVO, false)) { SessionState.SetBool(SK_ATIVO, false); Comecar(SessionState.GetBool(SK_GRAVAR, false)); }
        else if (e == PlayModeStateChange.ExitingPlayMode) { if (gravador != null && gravador.IsRecording()) gravador.StopRecording(); gravador = null; }
        else if (e == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(SK_SAIR, false)) { SessionState.SetBool(SK_SAIR, false); EditorApplication.Exit(0); }
    }

    static void Comecar(bool gravar)
    {
        GameObject go = new GameObject("Diretor do Trailer Novo");
        TrailerFinalDiretor d = go.AddComponent<TrailerFinalDiretor>();
        d.trilha = AssetDatabase.LoadAssetAtPath<AudioClip>(TRILHA);
        d.aoTerminar = () => { EditorApplication.delayCall += () => EditorApplication.isPlaying = false; };
        if (!gravar) return;
        RecorderControllerSettings cs = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        MovieRecorderSettings mov = ScriptableObject.CreateInstance<MovieRecorderSettings>(); mov.name = "Trailer novo do Pinglu";
        cs.hideFlags = HideFlags.HideAndDontSave; mov.hideFlags = HideFlags.HideAndDontSave; mov.Enabled = true;
#if UNITY_EDITOR_LINUX
        mov.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.WebM;
#else
        mov.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
#endif
        mov.VideoBitRateMode = VideoBitrateMode.High;
        mov.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        mov.AudioInputSettings.PreserveAudio = true;
        mov.OutputFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Recordings/trailer_final"));
        cs.AddRecorderSettings(mov); cs.SetRecordModeToManual(); cs.FrameRate = 60f; cs.CapFrameRate = true; cs.ExitPlayMode = false;
        gravador = new RecorderController(cs); gravador.PrepareRecording(); gravador.StartRecording();
    }
}
#endif
