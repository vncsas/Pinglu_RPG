using UnityEngine;

// Último ponto de descanso do jogador. Ao morrer e apertar R (jogo solo), ele volta para cá SEM recarregar a cena:
// a história, o personagem escolhido e o que já foi vencido continuam como estavam; só a luta que estava em andamento recomeça.
//  - sozinho, o ponto é atualizado enquanto o jogador anda em paz (longe de inimigos, sem levar dano há um tempo);
//  - quando começa uma luta de chefe (ou a horda do cemitério) o ponto fica TRAVADO um pouco antes dela, até a luta acabar.
// Quem tem uma luta para reiniciar assina AoRenascer.
public static class Renascimento
{
    public static bool Tem { get; private set; }
    public static Vector3 Posicao { get; private set; }
    public static Quaternion Rotacao { get; private set; } = Quaternion.identity;
    public static bool Travado { get; private set; }
    public static event System.Action AoRenascer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { Tem = false; Travado = false; AoRenascer = null; Posicao = Vector3.zero; Rotacao = Quaternion.identity; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar()
    {
        if (Object.FindFirstObjectByType<RastreadorDeDescanso>() != null) return;
        GameObject g = new GameObject("Rastreador de descanso"); Object.DontDestroyOnLoad(g); g.AddComponent<RastreadorDeDescanso>();
    }

    public static void Reiniciar() { Zerar(); }

    // ponto comum (ignorado enquanto uma luta de chefe estiver travando)
    public static void Definir(Vector3 pos, Quaternion rot)
    {
        if (Travado) return;
        Posicao = pos; Rotacao = rot; Tem = true;
    }

    // ponto de uma luta: vale até Liberar()
    public static void DefinirParaLuta(Vector3 pos, Quaternion rot)
    {
        Posicao = pos; Rotacao = rot; Tem = true; Travado = true;
    }

    public static void Liberar() { Travado = false; }

    // devolve o jogador local ao ponto, vivo e com a vida cheia; as lutas se reiniciam pelo evento
    public static bool Renascer()
    {
        bool algum = false;
        foreach (PlayerLocomotion p in Object.FindObjectsByType<PlayerLocomotion>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (p == null || !p.enabled || p.Companheiro) continue;
            Vida v = p.GetComponent<Vida>(); if (v == null || !v.Morto) continue;
            if (Tem)
            {
                Vector3 d = Posicao;
                RaycastHit h;
                if (Physics.Raycast(d + Vector3.up * 6f, Vector3.down, out h, 40f, ~0, QueryTriggerInteraction.Ignore)) d.y = h.point.y + 0.15f;
                CharacterController cc = p.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                p.transform.SetPositionAndRotation(d, Rotacao);
                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();
            }
            v.Reviver(); p.Reviver();
            algum = true;
        }
        Time.timeScale = 1f;
        if (algum && AoRenascer != null) AoRenascer();
        return algum;
    }
}

// Atualiza o ponto de descanso sozinho enquanto o jogador está em paz.
public class RastreadorDeDescanso : MonoBehaviour
{
    float proxima, ultimoDano = -99f;
    Vida vidaOuvida;

    void Update()
    {
        if (Time.unscaledTime < proxima) return;
        proxima = Time.unscaledTime + 1f;
        if (Renascimento.Travado || SessaoDeJogo.MenuAberto || CameraDeCena.Ativa) return;

        PlayerLocomotion jogador = null;
        foreach (PlayerLocomotion p in FindObjectsByType<PlayerLocomotion>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (p.enabled && !p.Companheiro && p.gameObject.activeInHierarchy) { jogador = p; break; }
        if (jogador == null) return;
        Vida v = jogador.GetComponent<Vida>(); if (v == null || v.Morto) return;
        if (v != vidaOuvida) { vidaOuvida = v; v.AoLevarDano += (d, o) => ultimoDano = Time.time; }

        CharacterController cc = jogador.GetComponent<CharacterController>();
        if (cc != null && !cc.isGrounded) return;
        if (jogador.Nadando || AmbienteDaAgua.Profundidade(jogador.transform.position) > 0.3f) return;
        if (Time.time - ultimoDano < 10f) return;
        // inimigos por perto? então não é um lugar de descanso
        foreach (Collider c in Physics.OverlapSphere(jogador.transform.position, 32f, ~0, QueryTriggerInteraction.Ignore))
        {
            Vida o = c.GetComponentInParent<Vida>();
            if (o != null && !o.Morto && o != v && o.GetComponentInParent<PlayerLocomotion>() == null) return;
        }
        if (Renascimento.Tem && (Renascimento.Posicao - jogador.transform.position).sqrMagnitude < 12f * 12f) return;
        Renascimento.Definir(jogador.transform.position, jogador.transform.rotation);
    }
}
