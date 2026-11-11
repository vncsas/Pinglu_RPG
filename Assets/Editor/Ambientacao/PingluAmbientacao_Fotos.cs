#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tira fotos de pontos do mapa (so para conferir o resultado; nao altera a cena)
public static class PingluAmbientacao_Fotos
{
    static Bounds Limites(GameObject go) { var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 10); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
    public static void Rodar()
    {
        EditorSceneManager.OpenScene(System.Environment.GetEnvironmentVariable("FOTOS_CENA") ?? "Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var t = Terrain.activeTerrain; string pasta = System.Environment.GetEnvironmentVariable("FOTOS_PASTA") ?? "C:/Users/Thiago/ambiente/fotos_pinglu"; Directory.CreateDirectory(pasta);
        var alvos = new (string nome, string objeto, float dist, float alt)[] {
            ("vila", "Vila_Gerada/Praca", 38, 7), ("vila_rua", "Vila_Gerada/Estrada", 30, 4), ("colheita", "Vila_Gerada/Colheita e Arredores", 40, 6),
            ("cemiterio", "Mundo_Gerado/Cemiterio", 70, 14), ("natureza", "Mundo_Gerado/Natureza", 60, 8), ("castelo", "Mundo_Gerado/Castelo Destruido", 60, 12),
            ("pantano", "Biomas_Gerados/Pantano", 45, 8), ("floresta", "Biomas_Gerados/Floresta Sombria", 50, 8), ("queimadas", "Biomas_Gerados/Terras Queimadas", 50, 10), ("colinas", "Biomas_Gerados/Colinas de Pedra", 50, 10),
            ("bandidos", "Mundo_Gerado/Acampamento de Bandidos 1", 25, 5), ("chao_colheita", "Vila_Gerada/Colheita e Arredores", 55, 1.8f), ("chao_rua", "Vila_Gerada/Caminhos da Vila", 28, 1.8f), ("praca_perto", "Vila_Gerada/Praca", 13, 3f), ("lago", "Ambientacao_Extra/Lago do Pantano", 75, 16f), ("lago2", "Ambientacao_Extra/Lago do Pantano", 40, 6f), ("casa_perto", "Vila_Gerada/Construcoes", 14, 2.2f), ("casa2", "Vila_Gerada/Casas Novas", 11, 2f), ("casa3", "Vila_Gerada/Detalhes", 12, 2f), ("aereo", "Vila_Gerada/Praca", 330, 150f), ("aereo2", "Mundo_Gerado/Natureza", 300, 110f), ("rua_perto", "Vila_Gerada/Caminhos da Vila", 18, 2.5f), ("rua_perto2", "Vila_Gerada/Estrada", 16, 2.5f), ("chao_natureza", "Mundo_Gerado/Natureza", 20, 1.8f) };
        var esc = System.Environment.GetEnvironmentVariable("FOTOS_ESCONDER"); if (esc != null) foreach (var rr in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) if (rr.name.StartsWith(esc)) rr.enabled = false;
        var camGO = new GameObject("FotoCam"); var cam = camGO.AddComponent<Camera>(); cam.fieldOfView = 62; cam.farClipPlane = 900; cam.clearFlags = CameraClearFlags.Skybox;
        camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var filtro = System.Environment.GetEnvironmentVariable("FOTOS_FILTRO");
        if (filtro != null && filtro.Split(',').Contains("mapa"))
        {
            RenderSettings.fog = false; cam.orthographic = true; cam.orthographicSize = 520; cam.farClipPlane = 3000; cam.transform.position = new Vector3(500, 1500, 500); cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            var rt0 = new RenderTexture(1400, 1400, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt0; cam.Render(); RenderTexture.active = rt0; var tx = new Texture2D(1400, 1400, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 1400, 1400), 0, 0); tx.Apply();
            File.WriteAllBytes($"{pasta}/mapa.png", tx.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null; cam.orthographic = false; Debug.Log("[FOTO] mapa");
        }
        var livres = System.Environment.GetEnvironmentVariable("FOTOS_PONTOS");
        if (livres != null)
            foreach (var item in livres.Split(';'))
            {
                var q = item.Split(':'); if (q.Length < 6) continue; float x = float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture), z = float.Parse(q[2], System.Globalization.CultureInfo.InvariantCulture), alt = float.Parse(q[3], System.Globalization.CultureInfo.InvariantCulture), yaw = float.Parse(q[4], System.Globalization.CultureInfo.InvariantCulture), pit = float.Parse(q[5], System.Globalization.CultureInfo.InvariantCulture);
                var pos = new Vector3(x, t.SampleHeight(new Vector3(x, 0, z)) + alt, z); cam.transform.position = pos; cam.transform.rotation = Quaternion.Euler(pit, yaw, 0);
                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes($"{pasta}/{q[0]}.png", tex.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Debug.Log("[FOTO] livre " + q[0]);
            }
        foreach (var a in alvos)
        {
            if (filtro != null && !filtro.Split(',').Contains(a.nome)) continue;
            var go = GameObject.Find(a.objeto.Split('/').Last()); var partes = a.objeto.Split('/'); Transform tr = GameObject.Find(partes[0])?.transform; for (int i = 1; tr != null && i < partes.Length; i++) tr = tr.Find(partes[i]);
            if (tr == null) { Debug.Log("[FOTO] nao achei " + a.objeto); continue; }
            var b = Limites(tr.gameObject); var c = b.center;
            var pos = c + new Vector3(-a.dist * 0.7f, 0, -a.dist * 0.7f); pos.y = t.SampleHeight(pos) + a.alt;
            cam.transform.position = pos; cam.transform.LookAt(c + Vector3.up * 1.5f);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes($"{pasta}/{a.nome}.png", tex.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log($"[FOTO] {a.nome} centro={c}");
        }
        Object.DestroyImmediate(camGO);
    }
}
#endif
