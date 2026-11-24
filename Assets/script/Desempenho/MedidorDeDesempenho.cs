using System.Collections;
using System.Reflection;
using UnityEngine;

// Mede o FPS e o custo de desenho em três lugares (lobby, campo, cemitério) e escreve no Console/log.
// Usado pelo menu Pinglu > 38 (só no Editor).
public class MedidorDeDesempenho : MonoBehaviour
{
    public System.Action AoTerminar;
    private const BindingFlags PRIV = BindingFlags.NonPublic | BindingFlags.Instance;

    private IEnumerator Start()
    {
        MenuRede menu = null; PalcoSelecao palco = null;
        for (float w = 0f; w < 20f && (palco == null || !palco.Pronto); w += Time.unscaledDeltaTime)
        {
            if (menu == null) menu = FindAnyObjectByType<MenuRede>();
            if (palco == null) palco = FindAnyObjectByType<PalcoSelecao>();
            yield return null;
        }
        yield return Medir("LOBBY", 6f);
        MethodInfo entrar = menu != null ? menu.GetType().GetMethod("EntrarSozinho", PRIV) : null;
        if (entrar != null) entrar.Invoke(menu, null);
        yield return new WaitForSecondsRealtime(9f);
        yield return Medir("CAMPO (inicio do mundo)", 6f);

        GerenciadorDeOndas ger = FindAnyObjectByType<GerenciadorDeOndas>();
        PlayerLocomotion p = null;
        foreach (PlayerLocomotion x in Resources.FindObjectsOfTypeAll<PlayerLocomotion>()) if (x != null && x.gameObject.scene.IsValid() && x.gameObject.activeInHierarchy) { p = x; break; }
        if (ger != null && p != null)
        {
            CharacterController cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Vector3 f = ger.transform.forward; f.y = 0f;
            Vector3 destino = ger.transform.position + f.normalized * 18f;
            RaycastHit hit;
            if (Physics.Raycast(destino + Vector3.up * 40f, Vector3.down, out hit, 100f)) destino.y = hit.point.y + 0.3f;
            p.transform.position = destino;
            p.transform.rotation = Quaternion.LookRotation(-f.normalized);
            if (cc != null) cc.enabled = true;
        }
        yield return new WaitForSecondsRealtime(5f);
        yield return Medir("CEMITERIO (entrada)", 6f);
        if (AoTerminar != null) AoTerminar();
    }

    private IEnumerator Medir(string nome, float dur)
    {
        float t = 0f; int q = 0; float pior = 0f;
        while (t < dur)
        {
            float dt = Time.unscaledDeltaTime; t += dt; q++; pior = Mathf.Max(pior, dt);
            yield return null;
        }
        string stats = "";
#if UNITY_EDITOR
        stats = " | drawcalls=" + UnityEditor.UnityStats.drawCalls + " setpass=" + UnityEditor.UnityStats.setPassCalls
              + " tris=" + (UnityEditor.UnityStats.triangles / 1000) + "k vert=" + (UnityEditor.UnityStats.vertices / 1000) + "k sombras=" + UnityEditor.UnityStats.shadowCasters;
#endif
        Debug.Log("[MEDIDA] " + nome + ": " + (q / t).ToString("0.0") + " fps (pior quadro " + (pior * 1000f).ToString("0") + " ms)" + stats);
    }
}
