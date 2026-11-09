using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildBoarDemo.Editor
{
    public static class WildBoarMenu
    {
        private const string Root = "Assets/Wild Boar";

        [MenuItem("Tools/Wild Boar/Open demo for current pipeline")]
        public static void OpenCurrent()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            string name = pipeline == null ? "Built-in" :
                pipeline.GetType().Name.Contains("HDRender") ? "HDRP" : "URP";
            OpenScene(name);
        }

        [MenuItem("Tools/Wild Boar/Setup showcase/Built-in")]
        public static void BuiltIn() => Setup("Built-in");

        [MenuItem("Tools/Wild Boar/Setup showcase/URP")]
        public static void URP() => Setup("URP");

        [MenuItem("Tools/Wild Boar/Setup showcase/HDRP")]
        public static void HDRP() => Setup("HDRP");

        [MenuItem("Tools/Wild Boar/Read documentation")]
        public static void Documentation()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Documentation/Readme.txt");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Tools/Wild Boar/View full animal collection")]
        public static void FullCollection() => Application.OpenURL(WildBoarLinks.FullCollectionUrl);

        private static void OpenScene(string name)
        {
            string path = Root + "/Demo/Scenes/WildBoar_Demo_" + name + ".unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
            {
                EditorUtility.DisplayDialog("Wild Boar", "The demo scene is missing. Reimport the package.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }

        private static void Setup(string name)
        {
            var asset = name == "Built-in" ? null :
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Root + "/Demo/Settings/Showcase_" + name + ".asset");
            if (name != "Built-in" && !asset)
            {
                EditorUtility.DisplayDialog("Wild Boar", "Install the " + name +
                    " render pipeline from Unity Package Manager, then retry setup. See Readme for supported versions.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            QualitySettings.renderPipeline = null;
            GraphicsSettings.defaultRenderPipeline = asset;
            EditorSceneManager.OpenScene(Root + "/Demo/Scenes/WildBoar_Demo_" + name + ".unity");
        }
    }
}
