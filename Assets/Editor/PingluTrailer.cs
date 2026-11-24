// =====================================================================
//  PingluTrailer.cs  -  Grava o trailer do jogo em vídeo (Unity Recorder)
// ---------------------------------------------------------------------
//  Pinglu > 35. Trailer: gravar vídeo   -> entra no Play, roda o TrailerDirector
//                                          e salva Recordings/trailer_pinglu.mp4
//  Pinglu > 36. Trailer: só assistir    -> roda sem gravar (para testar)
//  Precisa do pacote Unity Recorder (já está no manifest do projeto).
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PingluTrailer
{
    const string CENA = "Assets/Scenes/SampleScene.unity";
    const string TRILHA = "Assets/Trailer/trilha_trailer.wav";
    const string SK_GRAVAR = "PingluTrailerGravar", SK_ATIVO = "PingluTrailerAtivo", SK_SAIR = "PingluTrailerSair";
    static RecorderController gravador;

    static PingluTrailer() { EditorApplication.playModeStateChanged += AoMudarPlay; }

    [MenuItem("Pinglu/35. Trailer: gravar vídeo")]
    public static void Gravar() { Iniciar(true, false); }

    [MenuItem("Pinglu/36. Trailer: só assistir")]
    public static void Assistir() { Iniciar(false, false); }

    // para rodar sem ninguém: Unity -executeMethod PingluTrailer.GravarEFechar
    public static void GravarEFechar() { Iniciar(true, true); }

    static void Iniciar(bool gravar, bool sair)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        if (EditorSceneManager.GetActiveScene().path != CENA) EditorSceneManager.OpenScene(CENA);
        SessionState.SetBool(SK_GRAVAR, gravar);
        SessionState.SetBool(SK_ATIVO, true);
        SessionState.SetBool(SK_SAIR, sair);
        EditorApplication.isPlaying = true;
    }

    static void AoMudarPlay(PlayModeStateChange e)
    {
        if (e == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SK_ATIVO, false))
        {
            SessionState.SetBool(SK_ATIVO, false);
            Comecar(SessionState.GetBool(SK_GRAVAR, false));
        }
        else if (e == PlayModeStateChange.ExitingPlayMode)
        {
            if (gravador != null && gravador.IsRecording()) gravador.StopRecording();
            gravador = null;
        }
        else if (e == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(SK_SAIR, false))
        {
            SessionState.SetBool(SK_SAIR, false);
            EditorApplication.Exit(0);
        }
    }

    static void Comecar(bool gravar)
    {
        GameObject go = new GameObject("Diretor do Trailer");
        TrailerDirector d = go.AddComponent<TrailerDirector>();
        d.trilha = AssetDatabase.LoadAssetAtPath<AudioClip>(TRILHA);
        d.fonteLegenda = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fontes/MarcellusSC-Regular.ttf");
        d.fonteTitulo = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fontes/CinzelDecorative-Bold.ttf");
        d.AoTerminar = () => { EditorApplication.delayCall += () => EditorApplication.isPlaying = false; };

        if (!gravar) return;
        RecorderControllerSettings cs = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        MovieRecorderSettings mov = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        mov.name = "Trailer do Pinglu";
        // sem isto o Unity apaga as configurações quando descarrega assets não usados (ao entrar no mundo) e a gravação morre
        cs.hideFlags = HideFlags.HideAndDontSave;
        mov.hideFlags = HideFlags.HideAndDontSave;
        mov.Enabled = true;
        #if UNITY_EDITOR_LINUX
        mov.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.WebM;   // o Linux não grava MP4
#else
        mov.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
#endif
        mov.VideoBitRateMode = VideoBitrateMode.High;
        mov.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        mov.AudioInputSettings.PreserveAudio = true;
        mov.OutputFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Recordings/trailer_pinglu"));
        cs.AddRecorderSettings(mov);
        cs.SetRecordModeToManual();
        cs.FrameRate = 30f;
        cs.CapFrameRate = true;
        cs.ExitPlayMode = false;
        gravador = new RecorderController(cs);
        gravador.PrepareRecording();
        gravador.StartRecording();
    }
}
#endif
