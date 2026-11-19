#if UNITY_EDITOR
using System.IO; using System.Linq; using UnityEditor; using UnityEngine;
public static class OvelhaTeste
{
    public static void Rodar()
    {
        MontarOvelha.Montar();
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ovelha/Ovelha.prefab");
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.transform.localScale = Vector3.one * 5;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(pf); g.transform.position = Vector3.zero;
        var luz = new GameObject("sol").AddComponent<Light>(); luz.type = LightType.Directional; luz.transform.rotation = Quaternion.Euler(50, -30, 0); luz.intensity = 1.3f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);
        var cam = new GameObject("cam").AddComponent<Camera>(); cam.backgroundColor = new Color(0.55f, 0.65f, 0.8f); cam.clearFlags = CameraClearFlags.SolidColor; cam.fieldOfView = 35;
        string pasta = "C:/Users/Thiago/Documentos/rig-ovelha/saida/unity_fotos"; Directory.CreateDirectory(pasta);
        var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/Ovelha/Ovelha.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToDictionary(c => c.name);
        Debug.Log("[C2] clips: " + string.Join(",", clips.Keys));
        var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); Debug.Log("[C2] bounds " + b);
        foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.forceMatrixRecalculationPerRender = true;
        foreach (var (nome, t) in new[] { ("Idle", 0f), ("Walk", 0.4f), ("Trot", 0.2f), ("Gallop", 0.3f), ("Comer", 1.0f) })
        {
            clips[nome].SampleAnimation(g, t * clips[nome].length); g.GetComponent<Animator>().enabled = false; cam.transform.position = new Vector3(-2.6f, 0.7f, 0f); cam.transform.LookAt(new Vector3(0, 0.35f, 0f));
            var rt = new RenderTexture(900, 600, 24); cam.targetTexture = rt; cam.Render(); cam.Render(); RenderTexture.active = rt; var tx = new Texture2D(900, 600, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 900, 600), 0, 0); tx.Apply();
            File.WriteAllBytes($"{pasta}/{nome}.png", tx.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null;
        }
        foreach (var (nome, t) in new[] { ("Walk", 0.15f), ("Walk", 0.4f), ("Trot", 0.3f), ("Gallop", 0.45f), ("Comer", 0.5f) })
        {
            clips[nome].SampleAnimation(g, t * clips[nome].length); cam.transform.position = new Vector3(-4.2f, 1.2f, 1.6f); cam.transform.LookAt(new Vector3(0, 0.9f, 0.2f));
            var rt = new RenderTexture(700, 800, 24); cam.targetTexture = rt; cam.Render(); cam.Render(); RenderTexture.active = rt; var tx = new Texture2D(700, 800, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 700, 800), 0, 0); tx.Apply();
            File.WriteAllBytes($"{pasta}/F_{nome}_{(int)(t * 100)}.png", tx.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null;
        }
        Physics.SyncTransforms(); int ok = 0, total = 0;
        foreach (var (de, alvoPt) in new[] { (new Vector3(-2, 0.1f, -0.1f), new Vector3(2, 0.1f, -0.1f)), (new Vector3(2, 0.1f, -0.1f), new Vector3(-2, 0.1f, -0.1f)), (new Vector3(0, 0.1f, 3), new Vector3(0, 0.1f, -3)), (new Vector3(0, 0.1f, -3), new Vector3(0, 0.1f, 3)), (new Vector3(-2, 0.1f, 0.3f), new Vector3(2, 0.1f, 0.3f)), (new Vector3(-2, 0.1f, -0.5f), new Vector3(2, 0.1f, -0.5f)) })
        {
            var p = new GameObject("jog"); p.transform.position = de; var cc = p.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
            var dir = (alvoPt - de).normalized; float dist = (alvoPt - de).magnitude; for (int i = 0; i < 200; i++) { cc.Move(dir * dist / 200f); Physics.SyncTransforms(); }
            bool cruzou = Vector3.Dot(p.transform.position - de, dir) > dist - 0.1f; total++; if (!cruzou) ok++; Debug.Log($"[C2] colisao {de} -> {p.transform.position} cruzou={cruzou}"); Object.DestroyImmediate(p);
        }
        Debug.Log($"[C2] colisao: {ok}/{total} bloqueados");
    }
}
#endif
