// =====================================================================
//  PingluRedeInstalar.cs  -  Instala o pacote de multiplayer da Unity
//  (Netcode for GameObjects) e liga os scripts de rede do Pinglu.
//
//  COMO USAR:  Pinglu > 14. Instalar multiplayer
//  Depois:     Pinglu > 15. Preparar multiplayer
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

public static class PingluRedeInstalar
{
    const string PACOTE = "com.unity.netcode.gameobjects";
    const string SIMBOLO = "PINGLU_REDE";
    static AddRequest pedido;

    [MenuItem("Pinglu/14. Instalar multiplayer")]
    public static void Instalar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Pare o Play primeiro.", "OK"); return; }
        pedido = Client.Add(PACOTE);
        EditorApplication.update += Acompanhar;
    }

    static void Acompanhar()
    {
        if (pedido == null) { EditorApplication.update -= Acompanhar; return; }
        if (!pedido.IsCompleted)
        {
            EditorUtility.DisplayProgressBar("Pinglu", "Baixando o pacote de multiplayer da Unity...", 0.5f);
            return;
        }
        EditorApplication.update -= Acompanhar;
        EditorUtility.ClearProgressBar();
        if (pedido.Status == StatusCode.Success)
        {
            LigarScriptsDeRede();
            EditorUtility.DisplayDialog("Pinglu", "Pacote de multiplayer instalado (versão " + pedido.Result.version + ").\n\n" +
                "A Unity vai recompilar os scripts agora. Espere terminar e rode Pinglu > 15. Preparar multiplayer.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Pinglu", "Não consegui instalar o pacote:\n" + (pedido.Error != null ? pedido.Error.message : "erro desconhecido") +
                "\n\nConfira a internet e tente de novo.", "OK");
        }
        pedido = null;
    }

    static void LigarScriptsDeRede()
    {
        NamedBuildTarget[] alvos = {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup) };
        foreach (NamedBuildTarget alvo in alvos)
        {
            string defs = PlayerSettings.GetScriptingDefineSymbols(alvo);
            if ((";" + defs + ";").Contains(";" + SIMBOLO + ";")) continue;
            PlayerSettings.SetScriptingDefineSymbols(alvo, string.IsNullOrEmpty(defs) ? SIMBOLO : defs + ";" + SIMBOLO);
        }
    }
}
#endif
