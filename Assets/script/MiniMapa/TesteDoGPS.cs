using System.Collections;
using System.Reflection;
using UnityEngine;

// Teste automático do GPS (só usado pelo menu Pinglu > 41): entra no mundo, marca o cemitério, tira prints do mini-mapa e dos mapas.
public class TesteDoGPS : MonoBehaviour
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
        yield return new WaitForSecondsRealtime(1f);
        MethodInfo entrar = menu.GetType().GetMethod("EntrarSozinho", PRIV);
        entrar.Invoke(menu, null);
        yield return new WaitForSecondsRealtime(13f);
        // F2: leva à entrada da floresta?
        GerenciadorDeOndas ger0 = FindAnyObjectByType<GerenciadorDeOndas>();
        PlayerLocomotion p0 = null;
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>()) if (p != null && p.enabled && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy) { p0 = p; break; }
        if (ger0 != null && p0 != null)
        {
            MethodInfo f2 = ger0.GetType().GetMethod("LevarJogadorAteAqui", PRIV);
            f2.Invoke(ger0, new object[] { p0.transform });
            Vector3 d = p0.transform.position - ger0.transform.position; d.y = 0f;
            Debug.Log("[GPS] F2 levou o jogador a " + d.magnitude.ToString("0") + " m do centro do cemitério (cemitério em " + ger0.transform.position + ")");
            yield return new WaitForSecondsRealtime(1f);
        }
        // câmera fixa atrás do jogador (o mouse preso no Linux gira a câmera do jogo)
        ThirdPersonCameraFollow follow = FindAnyObjectByType<ThirdPersonCameraFollow>();
        if (follow != null) follow.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        PlayerLocomotion jog = null;
        foreach (PlayerLocomotion p in Resources.FindObjectsOfTypeAll<PlayerLocomotion>()) if (p != null && p.enabled && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy) { jog = p; break; }
        Debug.Log("[GPS] menuAberto=" + SessaoDeJogo.MenuAberto + " jogador=" + (jog != null));

        GameObject portalGo = null;
        foreach (GameObject g in Resources.FindObjectsOfTypeAll<GameObject>()) if (g != null && g.scene.IsValid() && g.name.Contains("Portal de Ruina")) { portalGo = g; break; }
        Debug.Log("[GPS] portal: " + (portalGo != null ? portalGo.name + " ativo=" + portalGo.activeInHierarchy : "NAO ENCONTRADO"));
        MiniMapa mm = FindAnyObjectByType<MiniMapa>();
        GPSDoMapa gps = GPSDoMapa.Instancia;
        Debug.Log("[GPS] minimapa=" + (mm != null) + " gps=" + (gps != null) + " rede=" + (RedeDeEstradas.Instancia != null ? RedeDeEstradas.Instancia.estradas.Count + " estradas, " + RedeDeEstradas.Instancia.destinos.Count + " destinos" : "NENHUMA") + " usarNevoa=" + (mm != null && mm.usarNevoa));
        foreach (MiniMapa.Marcador m in mm.marcadores) Debug.Log("[GPS] marcador " + m.nome + " " + m.tipo + " " + m.posicao);
        foreach (MiniMapa.Marcador m in mm.marcadores)
            if (m.tipo == MiniMapa.Tipo.Cemiterio) { float t0 = Time.realtimeSinceStartup; gps.DefinirPorMarcador(m); Debug.Log("[GPS] rota calculada em " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms, pontos=" + gps.Rota.Count + ", distancia=" + gps.DistanciaRestante.ToString("0") + " m"); }
        for (int i = 0; i < 90; i++)
        {
            if (jog != null && Camera.main != null)
            {
                Camera.main.transform.position = jog.transform.position - jog.transform.forward * 5f + Vector3.up * 2.4f;
                Camera.main.transform.LookAt(jog.transform.position + Vector3.up * 1.2f);
            }
            yield return null;
        }
        Foto("minimapa");
        yield return new WaitForSecondsRealtime(0.5f);

        MapaDoMundo mapa = FindAnyObjectByType<MapaDoMundo>();
        if (mapa != null)
        {
            mapa.GetType().GetMethod("Abrir", PRIV).Invoke(mapa, null);
            yield return new WaitForSecondsRealtime(1.5f);
            mapa.GetType().GetMethod("VoarPara", PRIV).Invoke(mapa, new object[] { new Vector3(500f, 0f, 560f), 900f });
            yield return new WaitForSecondsRealtime(3.5f);
            Foto("mapa3d");
            // enquadra a rota: voa para o meio dela
            if (gps.Rota.Count > 1) { Vector3 meio = gps.Rota[gps.Rota.Count / 2]; mapa.GetType().GetMethod("VoarPara", PRIV).Invoke(mapa, new object[] { meio, 700f }); }
            yield return new WaitForSecondsRealtime(2.5f);
            Foto("mapa3d_rota");
            System.Type aba = mapa.GetType().GetNestedType("Aba", BindingFlags.NonPublic);
            mapa.GetType().GetMethod("TrocarAba", PRIV).Invoke(mapa, new object[] { System.Enum.ToObject(aba, 2) });
            yield return new WaitForSecondsRealtime(1.5f);
            Foto("pergaminho");
        }
        if (AoTerminar != null) AoTerminar();
    }

    private void Foto(string nome)
    {
        string pasta = System.IO.Path.Combine(Application.dataPath, "../Recordings/Teste");
        System.IO.Directory.CreateDirectory(pasta);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(pasta, nome + ".png"), 1);
    }
}
