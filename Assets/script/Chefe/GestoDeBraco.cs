using System;
using System.Collections.Generic;
using UnityEngine;

// Gestos de braço feitos por código, por cima da animação (funciona em qualquer personagem humanoide):
//  - Alcancar: o braço direito estica na direção de um ponto (pegar o cristal)
//  - LevantarDedo: antebraço para cima e um dedo apontado (o ladrão levitando o cristal); a espada some enquanto isso
[DefaultExecutionOrder(600)]
public class GestoDeBraco : MonoBehaviour
{
    private enum Modo { Nenhum, Alcancar, Dedo }
    private Modo modo = Modo.Nenhum;
    private float peso, pesoAlvo, duracao = 1f;
    private Func<Vector3> alvo;
    private Transform braco, antebraco, mao, peito;
    private GameObject dedo; private Light luzDedo;
    private readonly List<GameObject> escondidos = new List<GameObject>();

    public static GestoDeBraco Em(GameObject g)
    {
        GestoDeBraco c = g.GetComponent<GestoDeBraco>(); if (c == null) c = g.AddComponent<GestoDeBraco>();
        c.Achar(); return c;
    }

    private void Achar()
    {
        Animator a = GetComponentInChildren<Animator>();
        if (a == null || !a.isHuman) return;
        braco = a.GetBoneTransform(HumanBodyBones.RightUpperArm); antebraco = a.GetBoneTransform(HumanBodyBones.RightLowerArm); mao = a.GetBoneTransform(HumanBodyBones.RightHand);
        peito = a.GetBoneTransform(HumanBodyBones.UpperChest); if (peito == null) peito = a.GetBoneTransform(HumanBodyBones.Chest);
    }

    public bool Pronto { get { return braco != null && antebraco != null && mao != null; } }
    public Vector3 PontaDoDedo { get { return dedo != null ? dedo.transform.position + Vector3.up * dedo.transform.localScale.y : (mao != null ? mao.position : transform.position); } }

    public void Alcancar(Func<Vector3> ponto, float seg) { modo = Modo.Alcancar; alvo = ponto; duracao = Mathf.Max(0.05f, seg); pesoAlvo = 1f; }

    public void LevantarDedo(float seg)
    {
        modo = Modo.Dedo; duracao = Mathf.Max(0.05f, seg); pesoAlvo = 1f;
        if (mao == null) return;
        if (dedo == null)
        {
            dedo = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(dedo.GetComponent<Collider>());
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", new Color(0.2f, 0.19f, 0.22f)); m.SetFloat("_Metallic", 0.8f); m.SetFloat("_Smoothness", 0.5f);
            dedo.GetComponent<Renderer>().sharedMaterial = m;
            float s = transform.lossyScale.y;
            dedo.transform.localScale = new Vector3(0.05f * s, 0.075f * s, 0.05f * s);     // cápsula: altura total = 2 x escala y
            GameObject lg = new GameObject("Luz do dedo"); lg.transform.SetParent(dedo.transform, false); lg.transform.localPosition = new Vector3(0f, 1f, 0f);
            luzDedo = lg.AddComponent<Light>(); luzDedo.type = LightType.Point; luzDedo.color = new Color(0.5f, 0.1f, 0.8f); luzDedo.range = 5f; luzDedo.intensity = 0f; luzDedo.shadows = LightShadows.None;
        }
        // a espada sai da mão enquanto ele faz o gesto
        escondidos.Clear();
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == "Sword" && t.gameObject.activeSelf) { t.gameObject.SetActive(false); escondidos.Add(t.gameObject); }
    }

    public void Soltar(float seg)
    {
        pesoAlvo = 0f; duracao = Mathf.Max(0.05f, seg);
        foreach (GameObject g in escondidos) if (g != null) g.SetActive(true);
        escondidos.Clear();
    }

    private void OnDestroy() { if (dedo != null) Destroy(dedo); }

    private static void Apontar(Transform osso, Transform filho, Vector3 dir, float w)
    {
        Vector3 atual = filho.position - osso.position; if (atual.sqrMagnitude < 1e-6f || dir.sqrMagnitude < 1e-6f) return;
        Quaternion q = Quaternion.FromToRotation(atual.normalized, dir.normalized);
        osso.rotation = Quaternion.Slerp(osso.rotation, q * osso.rotation, w);
    }

    private void LateUpdate()
    {
        if (!Pronto) return;
        peso = Mathf.MoveTowards(peso, pesoAlvo, Time.deltaTime / duracao);
        float w = Mathf.SmoothStep(0f, 1f, peso);
        if (dedo != null) { dedo.SetActive(w > 0.02f && modo == Modo.Dedo); }
        if (w <= 0.001f) return;
        Vector3 dir = transform.forward, dr = transform.right;
        if (modo == Modo.Alcancar && alvo != null)
        {
            Vector3 p = alvo();
            if (peito != null) peito.rotation = Quaternion.Slerp(peito.rotation, Quaternion.AngleAxis(14f, dr) * peito.rotation, w * 0.6f);
            Apontar(braco, antebraco, p - braco.position, w);
            Apontar(antebraco, mao, p - antebraco.position, w);
        }
        else if (modo == Modo.Dedo)
        {
            Apontar(braco, antebraco, dr * 0.5f + dir * 0.3f + Vector3.down * 0.7f, w);
            Apontar(antebraco, mao, Vector3.up * 0.92f + dir * 0.28f - dr * 0.12f, w);
            Apontar(mao, mao.childCount > 0 ? mao.GetChild(0) : mao, Vector3.up, w * 0.0f);
            if (dedo != null)
            {
                dedo.transform.position = mao.position + Vector3.up * (0.05f * transform.lossyScale.y);
                dedo.transform.rotation = Quaternion.identity;
                if (luzDedo != null) luzDedo.intensity = 2.5f * w * (0.8f + 0.2f * Mathf.Sin(Time.time * 7f));
            }
        }
    }
}
