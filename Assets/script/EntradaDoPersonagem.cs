using UnityEngine;

// "Quem decide" o que o personagem faz. O PlayerLocomotion (e o voo do Tanque) não lê mais o teclado
// direto: perguntam para esta camada. Assim o MESMO corpo, animações, combos e habilidades podem ser
// controlados por uma pessoa (EntradaHumana) ou por um companheiro CPU (EntradaCPU, fase 2 do plano).
public abstract class EntradaDoPersonagem : MonoBehaviour
{
    // movimento: x = esquerda/direita, y = frente/trás (relativo à câmera, como o teclado)
    public abstract Vector2 Mover { get; }
    public abstract bool Correr { get; }
    public abstract bool PularApertou { get; }
    public abstract bool RolarApertou { get; }
    public abstract bool AtaqueApertou { get; }
    public abstract bool DefenderSegurando { get; }
    public abstract bool GuardarSacarApertou { get; }
    // voo do Tanque
    public abstract bool VoarApertou { get; }
    public abstract bool SubirSegurando { get; }
    public abstract bool DescerSegurando { get; }

    // habilidades especiais (0, 1 e 2 = teclas 1, 2 e 3). CPU: ainda não usa.
    public virtual bool HabilidadeApertou(int indice) { return false; }

    // CPUs andam em direções do MUNDO (não existe câmera para elas): se TemDirecaoDeMundo, ela vale no lugar de Mover.
    // O tamanho do vetor (0 a 1) é a intensidade (0,5 = andar devagar).
    public virtual bool TemDirecaoDeMundo { get { return false; } }
    public virtual Vector3 DirecaoDeMundo { get { return Vector3.zero; } }
    // para onde olha ao atacar/defender (CPU: na direção do inimigo). Humano: usa a câmera.
    public virtual bool TemFrenteDeMira { get { return false; } }
    public virtual Vector3 FrenteDeMira { get { return Vector3.forward; } }
}

// Teclado, mouse e controle: lê tudo pela camada Comandos, então vale o que o jogador escolheu em Configurações > Teclado e mouse / Controle.
public class EntradaHumana : EntradaDoPersonagem
{
    public override Vector2 Mover { get { return Comandos.Mover(); } }
    public override bool Correr { get { return Comandos.Segurando(Acao.Correr); } }
    public override bool PularApertou { get { return Comandos.Apertou(Acao.Pular); } }
    public override bool RolarApertou { get { return Comandos.Apertou(Acao.Rolar); } }
    public override bool AtaqueApertou { get { return Comandos.Apertou(Acao.Atacar); } }
    public override bool DefenderSegurando { get { return Comandos.Segurando(Acao.Defender); } }
    public override bool GuardarSacarApertou { get { return Comandos.Apertou(Acao.GuardarSacar); } }
    public override bool HabilidadeApertou(int indice) { return Comandos.Apertou(Acao.Habilidade1 + indice); }
    public override bool VoarApertou { get { return Comandos.Apertou(Acao.Voar); } }
    public override bool SubirSegurando { get { return Comandos.Segurando(Acao.Subir); } }
    public override bool DescerSegurando { get { return Comandos.Segurando(Acao.Descer); } }
}
