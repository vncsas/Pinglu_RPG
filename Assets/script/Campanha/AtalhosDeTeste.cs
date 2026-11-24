using UnityEngine;

// TECLAS DE TESTE (só no Editor e em build de desenvolvimento; não existem no jogo final).
//
//   LUGARES                         ANIMAÇÕES
//   F1  vilarejo (perto do Caio)    F5  Guardião do cemitério, direto na 2ª fase
//   F2  cemitério                   F6  cena depois do Guardião (o Ladrão rouba o cristal)
//   F3  castelo                     F7  cena do Ladrão no portal / começo da luta final   (Shift+F7: o Ladrão foge e deixa o rastro negro)
//   F4  pântano                     F8  2ª fase do dragão (a fusão com o Ladrão)
//                                   F9  morte do dragão em CENAS ILUSTRADAS (Shift+F9: versão 3D)
//   F10 volta ao começo da história · F11 final do jogo (epílogo e créditos) · F12 modo leve
//
// O F1 é tratado aqui; as outras teclas ficam nos scripts de cada luta (GerenciadorDeOndas, EncontroDoCastelo, EncontroDoPantano,
// ChefeGuardiao_Fase2 e EncontroDoDragao*), porque precisam de coisas privadas de cada cena.
public class AtalhosDeTeste : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar() { if (FindFirstObjectByType<AtalhosDeTeste>() == null) new GameObject("Atalhos de teste").AddComponent<AtalhosDeTeste>(); }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) IrParaOVilarejo();
        if (Input.GetKeyDown(KeyCode.F11)) FinalDoJogo.Tocar();
    }

    static Transform JogadorLocal()
    {
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsSortMode.None)) if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) return p.transform;
        return null;
    }

    // leva o jogador para a praça da vila, de frente para o Caio
    void IrParaOVilarejo()
    {
        if (DialogoUI.Aberto) return;
        Transform local = JogadorLocal(); if (local == null) return;
        NpcFalante caio = FindFirstObjectByType<NpcFalante>();
        Vector3 alvo, olhar;
        if (caio != null) { alvo = caio.transform.position + caio.transform.forward * 4.5f; olhar = caio.transform.position - alvo; }
        else if (PortalDoMundo.Atual != null) { alvo = PortalDoMundo.Atual.CentroNoChao + PortalDoMundo.Atual.Frente * 12f; olhar = -PortalDoMundo.Atual.Frente; }
        else return;
        Terrain t = Terrain.activeTerrain; alvo.y = (t != null ? t.SampleHeight(alvo) + t.transform.position.y : alvo.y) + 0.3f; olhar.y = 0f;
        CharacterController cc = local.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
        local.position = alvo; if (olhar.sqrMagnitude > 0.01f) local.rotation = Quaternion.LookRotation(olhar);
        if (cc != null) cc.enabled = true;
        CameraDeCena.Soltar(); SessaoDeJogo.MenuAberto = false;
    }
#endif
}
