#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

// ==========================================================================================
//  Pinglu > 85. Cena da Timeline - Morte do Dragão
//  Cria o prefab Assets/Resources/Cenas/MorteDoDragao.prefab com uma Timeline pronta: 5 câmeras Cinemachine (planos) + animação de movimento.
//  O jogo toca esse prefab sozinho quando o dragão morre (F9); se o prefab não existe, usa a câmera em código.
//
//  COMO EDITAR (visualmente): dê dois cliques no prefab > menu Window > Sequencing > Timeline.
//   - Trilha "Câmeras": cada bloco é um plano (uma câmera). Arraste as bordas para mudar o tempo. Blocos que se sobrepõem fazem transição suave.
//   - Trilha "Movimentos": animação de cada "Camera N" e "Alvo N" (posição). Clique na trilha e use a janela Animation / botão de gravar para ajustar.
//   - Cada "Camera N" é uma Cinemachine Camera: no Inspector mude o FOV (lente). "Alvo N" é o ponto que a câmera olha.
//   - O ponto laranja (esfera) é o corpo do dragão; o raio azul mostra a direção em que a câmera olha. Linha roxa = coluna de luz para o céu.
//   - O tempo da Timeline é o mesmo tempo da morte do dragão (0 s = ele começa a cair): 1,6 s cai no chão, 4,6 s coluna negra, 7,6 s vira cinza, 14,6 s luz dourada, ~19 s fim.
//  Cuidado: rodar este menu de novo RECRIA tudo (perde seus ajustes) — ele pergunta antes.
// ==========================================================================================
public static class PingluCenaMorteDragao
{
    const string Pasta = "Assets/Resources/Cenas";
    const string Prefab = Pasta + "/MorteDoDragao.prefab";
    const string TimelinePath = Pasta + "/MorteDoDragao_Timeline.playable";
    const float Duracao = 19f;

    struct Plano
    {
        public string nome; public float ini, fim, fov;
        public Func<float, Vector3> cam, alvo;
    }

    static Vector3 Orbita(float graus, float raio, float alt) { float a = graus * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * raio, alt, -Mathf.Cos(a) * raio); }
    static float Suave(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

    [MenuItem("Pinglu/85. Cena da Timeline - Morte do Dragão")]
    public static void Gerar()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pinglu", "Desligue o Play e rode de novo.", "OK"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) != null &&
            !EditorUtility.DisplayDialog("Pinglu", "A cena da morte do dragão já existe.\nRecriar apaga os seus ajustes na Timeline. Continuar?", "Recriar", "Cancelar")) return;

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/Resources", "Cenas");
        AssetDatabase.DeleteAsset(Prefab); AssetDatabase.DeleteAsset(TimelinePath);

        // os 5 planos (coordenadas locais: origem = corpo do dragão, a câmera fica em -Z olhando para +Z)
        Plano[] planos =
        {
            new Plano { nome = "Queda", ini = 0f, fim = 1.6f, fov = 55f,
                cam = u => new Vector3(14f, 2.5f, -34f), alvo = u => new Vector3(0f, Mathf.Lerp(14f, 3f, u * u), 0f) },
            new Plano { nome = "Convulsão", ini = 1.6f, fim = 4.6f, fov = 48f,
                cam = u => Vector3.Lerp(new Vector3(-11f, 4f, -24f), new Vector3(-6f, 5.5f, -16f), Suave(u)), alvo = u => new Vector3(0f, 5f, 0f) },
            new Plano { nome = "Coluna negra", ini = 4.6f, fim = 7.6f, fov = 72f,
                cam = u => Vector3.Lerp(new Vector3(0f, 1.2f, -34f), new Vector3(0f, 1f, -42f), Suave(u)), alvo = u => new Vector3(0f, Mathf.Lerp(8f, 75f, Suave(u)), 0f) },
            new Plano { nome = "Cinzas ao vento", ini = 7.6f, fim = 14.6f, fov = 50f,
                cam = u => Orbita(Mathf.Lerp(10f, 70f, Suave(u)), Mathf.Lerp(40f, 28f, Suave(u)), Mathf.Lerp(5f, 10f, Suave(u))), alvo = u => new Vector3(0f, 5f, 0f) },
            new Plano { nome = "Luz dourada", ini = 14.6f, fim = Duracao, fov = 60f,
                cam = u => Orbita(70f, Mathf.Lerp(28f, 55f, Suave(u)), Mathf.Lerp(10f, 30f, Suave(u))), alvo = u => new Vector3(0f, Mathf.Lerp(8f, 80f, Suave(u)), 0f) },
        };

        // ---- objeto raiz e filhos
        GameObject raiz = new GameObject("Cena - Morte do Dragao");
        Animator anim = raiz.AddComponent<Animator>();
        PlayableDirector dir = raiz.AddComponent<PlayableDirector>(); dir.playOnAwake = false;
        CenaTimeline ct = raiz.AddComponent<CenaTimeline>(); ct.diretor = dir;

        CinemachineCamera[] cams = new CinemachineCamera[planos.Length];
        for (int i = 0; i < planos.Length; i++)
        {
            GameObject al = new GameObject("Alvo " + (i + 1)); al.transform.SetParent(raiz.transform, false); al.transform.localPosition = planos[i].alvo(0f);
            GameObject go = new GameObject("Camera " + (i + 1) + " - " + planos[i].nome); go.transform.SetParent(raiz.transform, false); go.transform.localPosition = planos[i].cam(0f);
            CinemachineCamera cc = go.AddComponent<CinemachineCamera>();
            LensSettings l = cc.Lens; l.FieldOfView = planos[i].fov; cc.Lens = l;
            cc.Target.LookAtTarget = al.transform;
            CinemachineRotationComposer rc = go.AddComponent<CinemachineRotationComposer>(); rc.Damping = Vector2.zero;
            cams[i] = cc;
        }

        // ---- a Timeline
        TimelineAsset tl = ScriptableObject.CreateInstance<TimelineAsset>(); tl.name = "MorteDoDragao_Timeline";
        AssetDatabase.CreateAsset(tl, TimelinePath);
        dir.playableAsset = tl;

        // trilha das câmeras (cada plano começa 0,5 s antes do fim do anterior: a sobreposição vira transição suave)
        CinemachineTrack trCam = tl.CreateTrack<CinemachineTrack>("Câmeras");
        for (int i = 0; i < planos.Length; i++)
        {
            TimelineClip clip = trCam.CreateClip<CinemachineShot>();
            float ini = i == 0 ? planos[i].ini : planos[i].ini - 0.5f;
            clip.start = ini; clip.duration = planos[i].fim - ini; clip.displayName = planos[i].nome;
            CinemachineShot shot = (CinemachineShot)clip.asset;
            shot.DisplayName = planos[i].nome;
            shot.VirtualCamera.exposedName = GUID.Generate().ToString();
            dir.SetReferenceValue(shot.VirtualCamera.exposedName, cams[i]);
        }

        // trilha de movimento (curvas de posição de cada câmera e de cada alvo)
        AnimationTrack trAn = tl.CreateTrack<AnimationTrack>("Movimentos");
        trAn.CreateInfiniteClip("Movimentos");
        AnimationClip ac = trAn.infiniteClip;
        dir.SetGenericBinding(trAn, anim);
        for (int i = 0; i < planos.Length; i++)
        {
            float t0 = Mathf.Max(0f, planos[i].ini - 0.7f), t1 = Mathf.Min(Duracao, planos[i].fim + 0.7f);
            Curvas(ac, raiz.transform.GetChild(i * 2 + 1).name, t0, t1, planos[i].ini, planos[i].fim, planos[i].cam);
            Curvas(ac, raiz.transform.GetChild(i * 2).name, t0, t1, planos[i].ini, planos[i].fim, planos[i].alvo);
        }
        EditorUtility.SetDirty(ac); EditorUtility.SetDirty(tl);

        PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
        UnityEngine.Object.DestroyImmediate(raiz);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        Debug.Log("[Cena] Criado " + Prefab + ". Dê dois cliques nele e abra Window > Sequencing > Timeline para editar. Teste no jogo com F9.");
        EditorUtility.DisplayDialog("Pinglu", "Cena criada:\n" + Prefab + "\n\nPara editar: dois cliques no prefab e menu Window > Sequencing > Timeline.\nPara ver: Play e tecla F9.", "OK");
    }

    // amostra a função de posição de 0,25 em 0,25 s (fora do plano, a câmera fica parada no início/fim)
    static void Curvas(AnimationClip ac, string caminho, float t0, float t1, float ini, float fim, Func<float, Vector3> f)
    {
        AnimationCurve cx = new AnimationCurve(), cy = new AnimationCurve(), cz = new AnimationCurve();
        for (float t = t0; t <= t1 + 0.001f; t += 0.25f)
        {
            float u = Mathf.Clamp01((t - ini) / Mathf.Max(0.01f, fim - ini)); Vector3 v = f(u);
            cx.AddKey(t, v.x); cy.AddKey(t, v.y); cz.AddKey(t, v.z);
        }
        foreach (AnimationCurve c in new[] { cx, cy, cz }) for (int k = 0; k < c.length; k++) c.SmoothTangents(k, 0f);
        AnimationUtility.SetEditorCurve(ac, EditorCurveBinding.FloatCurve(caminho, typeof(Transform), "localPosition.x"), cx);
        AnimationUtility.SetEditorCurve(ac, EditorCurveBinding.FloatCurve(caminho, typeof(Transform), "localPosition.y"), cy);
        AnimationUtility.SetEditorCurve(ac, EditorCurveBinding.FloatCurve(caminho, typeof(Transform), "localPosition.z"), cz);
    }
}
#endif
